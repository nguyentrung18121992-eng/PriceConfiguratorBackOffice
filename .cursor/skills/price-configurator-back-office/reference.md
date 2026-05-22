# Price Configurator Back Office — Reference

Supplement to `SKILL.md`. Full narrative: `docs/price-configurator-back-office-strategy.md`.

## Frontend source repo

**Path:** `C:\Niteco-Project\Nobia\price-configurator`

This backoffice was split from that monorepo. All bundled catalog/prices/copy still originate there until a brand is fully CMS-driven.

## Current frontend data map

```
C:\Niteco-Project\Nobia\price-configurator\
  src/brands/data.js          → aggregates brand index by settings.brand
  src/brands/{brand}/index.js → sections[] with cards[], nested prices on cards
  src/brands/{brand}/prices.js → RANGE_PRICES, WORKTOP_PRICES, INSTALLATION_*, bands
  src/brands/{brand}/cloudinaryImages.js → image keys for Cloudinary
  config/{brand}/messages.json → i18n strings (P3: also in ConfiguratorMessages)
  src/components/cards/utils.js → getPrice, getInstallationPrice, getCabinetPrice
```

### Magnet-specific price dimensions

- Per range: `prices`, `prebuiltPrices`, `handlelessPrices` keyed by unit type (`small`, `medium`, `large`, `xlarge`)
- Worktops/flooring/installation: `card.prices[unitType]`
- Installation: `storeBand` + `DRY_FIT_BAND_PRICES` / `WET_FIT_BAND_PRICES` + `INSTALLATION_FEE` (still from price data in API payload)
- Appliances/sinks: matrix `card.prices[0][option.key]`
- Storage: per-card `amount * price` in section context

API snapshot MUST supply these fields after mapping so `utils.js` stays unchanged.

## Backoffice seed files (this repo)

| File | Source |
|------|--------|
| `price-configurator-back-office/Data/Seeds/{brand}-{language}.payload.json` | `npm run export-brand-config` or `scripts/sync-seeds-from-frontend.ps1` |
| `price-configurator-back-office/Data/Seeds/magnet-en-GB.sections.json` | Legacy sections-only export |

`ConfigSnapshotBuilder.BuildFromSeedFile` reads `.payload.json` first, then `.sections.json` + optional `.messages.json`.

## Proposed Cosmos entities (draft)

| Type | Notes |
|------|-------|
| `ConfiguratorSettings` | `[Singular]`, brand + language |
| `ConfiguratorSection` | `id`, `type`, translated titles/tooltips |
| `ConfiguratorCard` | FK to section, `cardKey`, Cloudinary image |
| `RangePriceSet` | Linked to range card; tier arrays |
| `PublishedConfiguration` | Snapshot JSON + `version`, `publishedAt`, `brand`, `language` |
| `ScheduledPublish` | Queue entry with `ScheduledPublishAt` |
| `ConfiguratorMessages` | `[Singular]` messages JSON (P3) |
| `PreviewToken` | Draft preview tokens (P4) |

Registered in `Startup.cs` via `c.AddCmsEntity<T>()`.

## Publish flow (implementation detail)

```
Editor saves → Draft entities updated in Cosmos
Editor Publish (or scheduler) →
  1. Load all draft entities for brand/lang
  2. Validate
  3. Map to ConfigV1 ViewModel (same as frontend needs)
  4. Upsert PublishedConfiguration (version++)
  5. Log / notify
Public GET → read latest PublishedConfiguration by brand+lang
```

If draft is empty, publish can fall back to seed file (`PublishService` / `POST /api/admin/publish-seed`).

## kitchen-quiz-backoffice packages (copy versions from their csproj)

- `Nobia.CmsToolkit`
- `Nobia.Backend.Authentication.AspNetCore`
- `Nobia.Backend.ApplicationInsights.AspNetCore`
- `Nobia.Backend.Logging`
- `Nobia.Backend.DataProtection`
- `NSwag.AspNetCore` (non-prod)

Connection string: `ConnectionStrings:priceconfigurator` (see `appsettings.local.json.example`).

## Frontend integration sketch

```javascript
// price-configurator — src/brands/loadBrandConfig.js
const res = await fetch(
  `${settings.configuratorBackofficeUrl}/api/config/v1?brand=${settings.brand}&language=${settings.language}`
)
if (!res.ok) throw new Error('Config unavailable')
const { sections, messages } = await res.json()
export const data = mapSections(sections)
```

Zeus settings: `configuratorBackofficeUrl`, `USE_CONFIG_API`, `USE_CONFIG_API_BRANDS`.

## Phase exit criteria (short)

| Phase | Done when |
|-------|-----------|
| P0 | Backoffice deploys; admin login works |
| P1 | Magnet price change → publish → visible on site without FE deploy |
| P2 | `POST /api/admin/publish-all-seeds`; frontend `USE_CONFIG_API_BRANDS` |
| P3 | API returns `messages`; `useTranslation` uses API copy |
| P4 | `POST /api/preview/v1/token`; site loads `?previewToken=` |

## P2–P4 commands

```powershell
# Regenerate seeds into this repo (from repo root)
.\scripts\sync-seeds-from-frontend.ps1

# Or from frontend repo (if sibling folder layout exists)
cd C:\Niteco-Project\Nobia\price-configurator
npm run export-brand-config
```

```http
# Publish all (backoffice, authenticated)
POST /api/admin/publish-all-seeds

# Preview link (backoffice, authenticated)
POST /api/preview/v1/token?brand=magnet&language=en-GB
```

## Open items for implementation plan

- Confirm CmsToolkit built-in publish vs custom `PublishedConfiguration`
- Choose scheduler (hosted service vs Azure Function) — **using `ScheduledPublishHostedService`**
- Decide API returns Cloudinary PublicId vs resolved URL
- Contract test location (backoffice integration test vs golden file in price-configurator)
