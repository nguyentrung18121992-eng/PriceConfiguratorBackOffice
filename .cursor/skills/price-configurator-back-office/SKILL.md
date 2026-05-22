---
name: price-configurator-back-office
description: >-
  Guides migration of price-configurator from hardcoded src/brands to
  price-configurator-back-office (ASP.NET 8, CmsToolkit, Cosmos). Covers phased
  delivery, published snapshot API, draft/scheduled publish, and frontend
  integration. Use when building the backoffice service, migrating brand data,
  wiring GET /api/config/v1, publish workflow, or planning price-configurator
  frontend changes.
---

# Price Configurator Back Office

## When to use

- Creating or extending **price-configurator-back-office** (.NET CMS service in this repo)
- Migrating data from the Zeus frontend at `C:\Niteco-Project\Nobia\price-configurator`
- Changing **price-configurator** to load config from API instead of static imports
- Implementing **draft / publish / scheduled publish**
- Reviewing architecture against **kitchen-quiz-backoffice**

## Canonical docs (this repo)

| Document | Path |
|----------|------|
| Strategy (all phases) | `docs/price-configurator-back-office-strategy.md` |
| Design spec entry | `docs/superpowers/specs/2026-05-21-price-configurator-back-office-design.md` |
| Data / seeds from frontend | `docs/DATA.md` |
| Detailed reference | `.cursor/skills/price-configurator-back-office/reference.md` |

Read the strategy doc before implementing. Do not duplicate price tables in the frontend after a brand is API-live.

## Source frontend (authoritative bundled data)

| Resource | Path |
|----------|------|
| Zeus React app | `C:\Niteco-Project\Nobia\price-configurator` |
| Brand catalog + cards | `src/brands/{brand}/index.js` |
| Price tables | `src/brands/{brand}/prices.js` |
| UI copy | `config/{brand}/messages.json` |
| Price calculation (unchanged) | `src/components/cards/utils.js` |
| API loader | `src/brands/loadBrandConfig.js` |

Regenerate Cosmos seeds from frontend: `.\scripts\sync-seeds-from-frontend.ps1` (see `docs/DATA.md`).

## Locked decisions

| Topic | Choice |
|-------|--------|
| New service | `price-configurator-back-office` — mirror kitchen-quiz-backoffice stack |
| Data authority | Backoffice Cosmos + CMS UI; public site reads **published snapshot only** |
| Price totals | **Frontend** — keep `utils.js` (`getPrice`, installation bands) |
| API | `GET /api/config/v1?brand=&language=` returns JSON shaped like today's `src/brands/data.js` output |
| Publish | Draft → Publish; **scheduled publish** in v1; **preview URLs** later (P4) |
| Rollout | **P1 Magnet** → P2 other brands → P3 copy in CMS → P4 preview |

## Reference implementation

Copy patterns from `C:\Niteco-Project\Nobia\kitchen-quiz-backoffice`:

- `Startup.cs` — `AddCmsToolkit`, brand AD groups, Cosmos connection
- `Models/` + `[Translated]` / `CloudinaryImage` attributes
- `ViewModels/` — stable public API records
- `Controllers/ApiController.cs` — `GET /api/settings/v2` mapping pattern

Kitchen-quiz reads live Cosmos entities; price-configurator-back-office MUST use **PublishedConfiguration snapshots** (draft + scheduled publish requirement).

## Phase checklist

Use this order; do not skip publish validation on Magnet.

### P0 — Foundation

- [x] New .NET 8 web project, Nobia packages, `infra/`, pipeline — this repo
- [x] CmsToolkit + Cosmos container — `Startup.cs`, connection `priceconfigurator`
- [x] Auth (Azure AD per brand) — Auth Scripts + CmsToolkit policy
- [x] Health check `/api/healthcheck`

### P1 — Magnet

- [x] CMS entities — `Models/Configurator*.cs`, `PublishedConfiguration`, `ScheduledPublish`
- [x] Magnet seed JSON — `Data/Seeds/magnet-en-GB.sections.json` (esbuild export from frontend)
- [x] Snapshot builder + publish — `Services/ConfigSnapshotBuilder`, `PublishService`
- [x] Scheduled publish job — `ScheduledPublishHostedService`
- [x] `GET /api/config/v1` — `Controllers/ConfigController.cs`
- [x] Frontend API loader — `USE_CONFIG_API=true`, `loadBrandConfig.js`, `ConfigBootstrap.js`
- [ ] Remove bundled magnet from prod deploy (after first published config in Cosmos)

### P2 — Other brands

- [x] Seeds: `sync-seeds-from-frontend` → all `Data/Seeds/*.payload.json`
- [x] `POST /api/admin/publish-all-seeds`
- [x] Frontend: `USE_CONFIG_API_BRANDS` or `USE_CONFIG_API=true`
- [ ] Delete `src/brands/{brand}` per brand after prod cutover (manual, in frontend repo)

### P3 — Copy

- [x] `ConfiguratorMessages` CMS entity + `messages` in API payload
- [x] Frontend `ApiMessagesProvider` + `useTranslation` override
- [ ] Remove `config/{brand}/messages.json` per brand after CMS copy is authoritative

### P4 — Preview

- [x] `POST /api/preview/v1/token` + `GET /api/config/v1/preview`
- [x] Frontend `?previewToken=` + banner in `ConfigBootstrap`

## Implementation rules

### Backoffice

1. **Stable keys** — Every range/card/option needs `rangeKey` / `cardKey` matching current JS ids where possible.
2. **Publish is atomic** — Public API reads `PublishedConfiguration` only, never half-updated draft joins.
3. **Validate on publish** — Images (Cloudinary PublicId), price array length vs unit tiers (small/medium/large/xlarge).
4. **Version field** — Increment on each publish; expose in API for frontend cache busting.

### Frontend (price-configurator)

1. Map API response into the same selection shape `getPrice` expects (`prices`, `prebuiltPrices`, `handlelessPrices`, `storeBand`, `card.prices[size]`, etc.).
2. **No silent fallback** to `src/brands` in production when API fails.
3. Do not change GraphQL submit schema in early phases unless explicitly required.
4. Keep `createCloudinaryUrl` behavior consistent (server returns PublicId vs full URL — pick one and document in API).

### Migration script

- One-time import: frontend `index.js` + `prices.js` → draft entities or seed JSON.
- First production go-live requires explicit **Publish** after editorial review.

## API contract (v1)

```
GET /api/config/v1?brand={brand}&language={language}
→ 200 PublishedConfiguration JSON (sections/cards/prices)
→ 404 if unpublished
```

Optional: `ETag` / `version` in body.

## Anti-patterns

- Serving draft entities on the public GET (breaks publish model)
- Storing only a giant singular blob without listing pages (Magnet is too large)
- Moving `getPrice` to the server without an explicit security/compliance request
- Editing `prices.js` in the frontend repo for brands already on API
- Copying kitchen-quiz’s live-read API without adding publish snapshots

## Verification

- [ ] Golden JSON test: API output for Magnet matches legacy export (minus env-specific URLs)
- [ ] Editor changes price → publish → site shows new value within cache TTL
- [ ] Scheduled publish fires at `ScheduledPublishAt`
- [ ] Quote email still submits via existing GraphQL with same field shapes

## Related repos

| Repo | Role |
|------|------|
| `C:\Niteco-Project\Nobia\price-configurator` | Zeus React frontend (bundled data source for seeds) |
| `PriceConfiguratorBackOffice` (this repo) | CMS + public config API |
| `kitchen-quiz-backoffice` | Stack and CmsToolkit reference |
