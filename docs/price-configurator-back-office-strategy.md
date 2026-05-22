# Price Configurator Back Office — Strategy & Phased Delivery

**Document version:** 1.0  
**Date:** 2026-05-21  
**Status:** Approved direction (brainstorming)  
**Repositories:** `price-configurator` (frontend), `price-configurator-back-office` (new), `kitchen-quiz-backoffice` (reference)

---

## 1. Executive summary

Today, kitchen catalog data, price tables, and brand configuration live as **hardcoded JavaScript** under `src/brands/{brand}/` and `config/{brand}/messages.json` in the Zeus React **price-configurator** app. Business users cannot change prices without a developer deploy.

The strategy is to introduce **price-configurator-back-office**: an ASP.NET Core CMS service (same stack as **kitchen-quiz-backoffice**) that becomes the **source of truth** for all configurator content. The public website loads **published** configuration via a read API; price **calculation** stays in the frontend.

| Goal | Approach |
|------|----------|
| Editable prices & catalog | Nobia.CmsToolkit + Cosmos DB |
| Safe go-live | Draft → Publish, with scheduled publish |
| Minimal frontend risk | API returns same shape the UI uses today |
| Rollout | Magnet first, then five other brands |

---

## 2. Problem statement

### Current state

| Concern | Location today |
|---------|----------------|
| Sections, cards, tooltips | `src/brands/{brand}/index.js` (Magnet ~1000 lines) |
| Price lookup tables | `src/brands/{brand}/prices.js` |
| Cloudinary image IDs | `src/brands/{brand}/cloudinaryImages.js` |
| UI copy / labels | `config/{brand}/messages.json` |
| Price calculation rules | `src/components/cards/utils.js` |
| Quote submit | GraphQL → price-configurator-api (unchanged) |

**Brands:** magnet, marbodal, invita, sigdal, norema, novart.

There is **no configuration API** at runtime; data is bundled at build time.

### Desired state

- Editors use **price-configurator-back-office** to manage catalog, prices, images, and (eventually) all copy.
- **price-configurator** fetches published config from the backoffice API on load.
- Price changes go live after **publish** (immediately or on a **schedule**).
- No production dependency on redeploying the React app for data changes.

---

## 3. Architecture

### 3.1 System context

**Data flow (top to bottom):**

1. **Editors** — Azure AD, per-brand admin groups  
2. **price-configurator-back-office** — CmsToolkit admin UI; draft entities in Cosmos; publish / scheduled publish to `PublishedConfiguration`; public `GET /api/config/v1` (published only)  
3. **Azure Cosmos DB** — draft entities and published snapshots  
4. **price-configurator (Zeus React)** — fetch published config at init; `getPrice` / `utils.js`; GraphQL quote submit to price-configurator-api (unchanged)

```
Editors
   |
   v
price-configurator-back-office
   |
   v
Azure Cosmos DB
   |
   v
price-configurator (Zeus React)  --->  price-configurator-api (quote submit)
```

### 3.2 Boundaries (what each system owns)

| System | Owns | Does not own |
|--------|------|----------------|
| **price-configurator-back-office** | Catalog structure, prices, CMS copy (phase 3+), images (Cloudinary IDs), publish lifecycle, public read API | Live total calculation, customer quote storage |
| **price-configurator** | UX, selection state, `getPrice` logic, email/PDF client calls | Authoritative price tables (after migration) |
| **price-configurator-api** | Quote persistence, email, Marketo, Cosmos customer artifacts | Product catalog (unchanged role) |

### 3.3 Reference stack (kitchen-quiz-backoffice)

| Layer | Technology |
|-------|------------|
| Runtime | .NET 8, ASP.NET Core |
| CMS | Nobia.CmsToolkit 1.26.x |
| Database | Cosmos DB via EF Core Cosmos provider |
| Auth | Cookie + Azure AD (Auth Scripts API) |
| Images | Cloudinary via CmsToolkit `CloudinaryImage` |
| API docs | NSwag (non-production) |
| Deploy | Nobia Digital platform (`infra/template.yaml`, Azure Pipelines) |

**New service name:** `price-configurator-back-office` (repo and platform service id aligned with kitchen-quiz naming).

---

## 4. Key design decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | **Normalized draft entities + published snapshot** on publish | Atomic go-live; supports schedule; avoids half-published catalog on public API |
| D2 | **Frontend-only price calculation** | Reuses proven `utils.js`; smaller API; matches current UX latency |
| D3 | **Public API shape matches today’s `data` export** | Minimal React refactor; same `getPrice` field names (`prices`, `prebuiltPrices`, `handlelessPrices`, etc.) |
| D4 | **Draft + Publish + Scheduled publish** (v1) | Business control; kitchen-quiz does not implement this — must be built explicitly |
| D5 | **Preview URLs deferred** (post-v1) | Reduce v1 scope; editors validate in CMS until preview phase |
| D6 | **Stable keys** on all cards/ranges (`rangeKey`, `cardKey`) | Maps CMS rows to frontend selection and price logic |
| D7 | **API versioned** (`/api/config/v1`) | Safe evolution during Magnet pilot |

---

## 5. Content model (CMS entities)

Editors work with **normalized** entities. The public site never reads draft rows directly.

### 5.1 Entity overview

| Entity | Cardinality | Purpose |
|--------|-------------|---------|
| `ConfiguratorSettings` | Singular per brand + language | Section order, currency, months-to-pay, header height, global flags |
| `ConfiguratorSection` | Many | Wizard step: `units`, `range`, `worktop`, `installation`, … |
| `ConfiguratorCard` | Many | One selectable option; links to section; Cloudinary image |
| `RangePriceSet` | Many (Magnet-heavy) | `prices`, `prebuiltPrices`, `handlelessPrices` per unit tier |
| `PriceTable` | Few per brand | Shared matrices: worktops, installation bands, appliances, sinks, flooring |
| `CardOption` | Many | Handles, sink types, appliance tiers with nested prices |

### 5.2 Published snapshot

On **Publish** (manual or scheduled):

1. Validate required fields (images, price array lengths vs unit tiers, references).
2. Run **snapshot builder**: draft entities → single `PublishedConfiguration` document.
3. Store with `version`, `publishedAt`, `brand`, `language`.
4. Public `GET /api/config/v1` reads **only** this snapshot.

---

## 6. Public API

### 6.1 Endpoint

```
GET /api/config/v1?brand={brand}&language={language}
```

| Response | Condition |
|----------|-----------|
| `200` + JSON body | Published snapshot exists |
| `404` | No published config for brand/language |

**Headers (recommended):** `ETag` or body field `version` for cache busting after publish.

### 6.2 Response contract

JSON MUST be consumable by the existing frontend pipeline:

- Equivalent to what `src/brands/data.js` produces after `createCloudinaryUrl` mapping (or document whether image URLs are built client-side from PublicIds).
- Preserve fields required by `getPrice` in `src/components/cards/utils.js`.

### 6.3 Out of scope for v1

- `POST /api/estimate` (server-side totals)
- Preview token endpoints (phase 4)

---

## 7. Publish & schedule workflow

| State | Visible to |
|-------|------------|
| **Draft** | CMS editors only |
| **Scheduled** | `ScheduledPublishAt` set; worker publishes at that time |
| **Published** | `PublishedConfiguration` snapshot; public API |

### 7.1 Manual publish

1. Editor clicks **Publish** in admin UI.
2. Validation runs on draft aggregate.
3. Snapshot written; `version` incremented.
4. CDN/API cache invalidated or short TTL (target: live site within minutes).

### 7.2 Scheduled publish

Same snapshot build at `ScheduledPublishAt` via background job (Azure Function, `IHostedService`, or platform scheduler — align with Nobia standards).

### 7.3 Frontend caching

- Fetch config on app load (and optional refresh interval).
- Use `version` query param or `ETag` after publish.
- Avoid long-lived cache that hides price updates for hours.

---

## 8. Frontend migration (price-configurator)

| Today | Target |
|--------|--------|
| Static `import` from `src/brands/{brand}/index.js` | `fetch(CONFIGURATOR_API_URL + '/api/config/v1?...')` at init |
| `prices.js` per brand | Removed when brand is API-driven |
| `messages.json` | Retained until phase 3; then CMS |
| `utils.js` | Unchanged responsibility; verify mapped API fields |
| GraphQL submit | Unchanged |

### 8.1 Environment

Add Zeus/settings key, e.g. `configuratorApiUrl`, per environment (dev/test/stage/prod).

### 8.2 Error handling

Production MUST NOT silently fall back to bundled `src/brands` if API fails (configurable dev-only fallback optional).

---

## 9. Phased delivery

### Phase P0 — Platform foundation

**Goal:** Deployable empty backoffice matching kitchen-quiz patterns.

| Deliverable | Details |
|-------------|---------|
| New solution `price-configurator-back-office` | .NET 8 web, csproj, sln, nuget.config |
| CmsToolkit + Cosmos | Connection string, container, brand registration |
| Auth | Azure AD groups per brand (mirror Constants pattern) |
| Health + infra | `infra/template.yaml`, Azure Pipelines |
| Admin UI shell | Entities registered; no public data yet |

**Exit criteria:** Service deploys to dev; admins can log in; health check passes.

---

### Phase P1 — Magnet pilot (catalog + prices + publish)

**Goal:** Magnet live on API; frontend uses published config only for Magnet.

| Deliverable | Details |
|-------------|---------|
| CMS models | All Magnet sections/cards/price matrices |
| Import tool | One-time migration from `src/brands/magnet/*` + `prices.js` |
| Snapshot builder + publish | Manual publish works |
| Scheduled publish | Job promotes scheduled drafts |
| `GET /api/config/v1` | Magnet + en-GB (and required languages) |
| Frontend | Magnet brand loads from API; loading/error states |
| Validation | Publish blocked on missing images/prices |

**Exit criteria:** Editor changes range price → publish → Magnet site shows new price without React deploy.

**Still in repo for Magnet:** `messages.json` copy (phase 3).

---

### Phase P2 — Remaining brands

**Goal:** All six brands on published API; remove hardcoded brand folders as each goes live.

| Order (suggested) | Relative effort |
|-------------------|-----------------|
| invita | Lower (~300 lines index) |
| sigdal, norema, novart | Medium |
| marbodal | High (similar to Magnet) |
| magnet | Done in P1 |

Per brand: import → CMS → publish → frontend switch → delete `src/brands/{brand}` + `prices.js`.

**Exit criteria:** No brand relies on bundled `src/brands` for catalog/prices in production.

---

### Phase P3 — Copy & translations in CMS

**Goal:** Retire `config/{brand}/messages.json`; tooltips and section copy in CmsToolkit with `[Translated]`.

| Deliverable | Details |
|-------------|---------|
| `ConfiguratorMessages` or fields on Settings/Sections | Per kitchen-quiz translation pattern |
| API extension | v1 payload includes message keys or inline copy |
| Frontend | Remove messages.json imports per brand |

**Exit criteria:** Copy changes do not require price-configurator repo deploy.

---

### Phase P4 — Preview

**Goal:** Editors preview draft config before publish.

| Deliverable | Details |
|-------------|---------|
| Authenticated preview API or tokenized URL | Draft snapshot, not public cache |
| Optional Zeus preview mode | Query param or separate host |

**Exit criteria:** Marketing can sign off without publishing to production.

---

### Phase summary table

| Phase | Focus | Public API | Frontend |
|-------|--------|------------|----------|
| P0 | Skeleton + deploy | — | Unchanged |
| P1 | Magnet + publish | v1 Magnet | Magnet → API |
| P2 | All brands | v1 all brands | All → API |
| P3 | Copy in CMS | v1 + messages | No messages.json |
| P4 | Preview | preview endpoint | Preview mode |

---

## 10. Migration strategy

### 10.1 Data import

1. Parse existing `index.js` + `prices.js` into intermediate JSON (Node script or C# tool).
2. Upsert draft Cosmos entities with stable keys.
3. Review in CMS admin.
4. **Publish** to create first `PublishedConfiguration`.

### 10.2 Rollback

- Keep previous `PublishedConfiguration` version N-1; republish to rollback.
- Frontend can pin `?version=` in emergencies (optional operational runbook).

### 10.3 Parallel run (Magnet pilot)

- Feature flag: `USE_CONFIG_API=true` for Magnet in dev/stage before prod cutover.

---

## 11. Security & operations

| Area | Approach |
|------|----------|
| Admin access | Azure AD groups per brand (same as kitchen-quiz `Startup.cs` pattern) |
| Public API | Read-only; CORS for Zeus origins; no auth on GET config (published data only) |
| Secrets | Cosmos, Cloudinary, Auth Scripts API via platform secrets |
| Observability | Application Insights, Serilog (Nobia.Backend.* packages) |

---

## 12. Risks & mitigations

| Risk | Mitigation |
|------|------------|
| API shape drift breaks `getPrice` | Contract tests: snapshot JSON vs golden fixture from current Magnet export |
| Large Magnet model hard to edit | Section/card listing pages in CMS; not one giant form |
| CmsToolkit lacks native schedule/publish | Explicit `PublishedConfiguration` entity + hosted scheduler |
| Editors publish broken config | Publish-time validation (images, price dimensions) |
| Stale CDN cache after publish | Short TTL + version in URL |
| Quote submit price mismatch | Document that submit stores client totals; server re-validation future hardening |

---

## 13. Success criteria (program level)

1. Business users edit kitchen prices in backoffice without developer involvement.
2. Published changes appear on the live configurator within agreed SLA (minutes, not deploy cycle).
3. All six brands served from Cosmos published snapshots.
4. price-configurator repo contains UI/code only — no authoritative `prices.js`.
5. Draft/scheduled publish prevents accidental live price changes.

---

## 14. References

| Resource | Path |
|----------|------|
| **This repo** — backoffice | `PriceConfiguratorBackOffice/` (CMS + `GET /api/config/v1`) |
| **This repo** — data / seeds | `docs/DATA.md`, `scripts/sync-seeds-from-frontend.ps1` |
| Price configurator (frontend) | `C:\Niteco-Project\Nobia\price-configurator` |
| Price configurator README | `C:\Niteco-Project\Nobia\price-configurator\README.md` |
| Brand data (example) | `...\price-configurator\src\brands\magnet\` |
| Price logic | `...\price-configurator\src\components\cards\utils.js` |
| Agent skill | `.cursor/skills/price-configurator-back-office/SKILL.md` |
| Kitchen quiz stack doc | `kitchen-quiz-backoffice/.planning/codebase/STACK.md` |
| Kitchen quiz architecture | `kitchen-quiz-backoffice/.planning/codebase/ARCHITECTURE.md` |
| Kitchen quiz API pattern | `GET /api/settings/v2` in `ApiController.cs` |

---

## 15. Glossary

| Term | Meaning |
|------|---------|
| **GAGP** | Magnet’s name for Price Configurator |
| **Published snapshot** | Immutable JSON document served to the public API |
| **Stable key** | Machine id (`ascoli`, `worktop-laminate`) surviving CMS edits to display title |
| **Zeus** | Nobia frontend hosting/tooling for React apps |

---

*End of strategy document*
