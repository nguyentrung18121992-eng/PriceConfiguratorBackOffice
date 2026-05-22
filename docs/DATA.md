# Data handling — frontend source → backoffice

This service was created from **`C:\Niteco-Project\Nobia\price-configurator`**. Bundled catalog, prices, and copy still live in that Zeus app until each brand is fully CMS-driven.

## Authority model

| Layer | What it stores | Who reads it |
|-------|----------------|--------------|
| **Frontend repo** (`price-configurator`) | `src/brands/{brand}/index.js`, `prices.js`, `config/{brand}/messages.json` | Build-time bundle; dev fallback |
| **Seed JSON** (`Data/Seeds/*.payload.json`) | Export snapshot for import/publish | `ConfigSnapshotBuilder.BuildFromSeedFile`, `POST /api/admin/publish-seed` |
| **Cosmos draft** | `ConfiguratorSection`, `ConfiguratorCard`, `ConfiguratorMessages`, etc. | CMS **Edit** menu |
| **Cosmos published** | `PublishedConfiguration` | `GET /api/config/v1` (public site) |

Public traffic must never read draft entities directly.

## Frontend paths (source of truth for migration)

```
C:\Niteco-Project\Nobia\price-configurator\
  src/brands/{brand}/index.js      → sections + cards (esbuild-bundled for export)
  src/brands/{brand}/prices.js      → range/worktop/installation tables (embedded in index today)
  config/{brand}/messages.json      → UI strings
  src/components/cards/utils.js     → getPrice (stays in frontend)
  scripts/export-all-brands.mjs     → upstream export script (sibling-path layout)
```

## Regenerate seeds into this repo

From **this repo root**:

```powershell
.\scripts\sync-seeds-from-frontend.ps1
```

Defaults:

- **Frontend:** `C:\Niteco-Project\Nobia\price-configurator`
- **Output:** `price-configurator-back-office\Data\Seeds\{brand}-{language}.payload.json`

Override frontend path:

```powershell
$env:PRICE_CONFIGURATOR_FRONTEND = "D:\other\price-configurator"
.\scripts\sync-seeds-from-frontend.ps1
```

Brands exported (same as frontend `export-all-brands.mjs`):

| Brand | Language | Seed file |
|-------|----------|-----------|
| magnet | en-GB | `magnet-en-GB.payload.json` |
| invita | da-DK | `invita-da-DK.payload.json` |
| sigdal | nb-NO | `sigdal-nb-NO.payload.json` |
| norema | nb-NO | `norema-nb-NO.payload.json` |
| novart | fi-FI | `novart-fi-FI.payload.json` |
| marbodal | sv-SE | `marbodal-sv-SE.payload.json` |

## Import bundled data into CMS (draft entities)

Seeds alone do not populate the **Edit** menu until you import into Cosmos:

```http
POST /api/admin/import-from-seed?brand=magnet&language=en-GB
POST /api/admin/import-all-from-seed
```

This creates/updates:

- **Configurator sections** — one per wizard step
- **Configurator cards** — one per option; prices/options in `CardDataJson`
- **Configurator messages** — full `messages.json` copy
- **Configurator settings** — section order from payload order

Use `replaceExisting=true` (default) to remove CMS rows that are no longer in the seed.

### One-shot pipeline (PowerShell + API)

```powershell
.\scripts\import-all-from-frontend.ps1
# Then while logged into the backoffice:
POST /api/admin/bootstrap-from-seed
```

`bootstrap-from-seed` = import all brands from seeds + publish each.

## Publish seeds to Cosmos

After import (or if you only need published JSON without CMS draft rows):

```http
POST /api/admin/publish-all-seeds
POST /api/admin/publish-seed?brand=magnet&language=en-GB
```

Requires authenticated CMS user.

## CMS vs seed workflow

1. **Bootstrap / refresh from frontend:** sync seeds → `publish-seed` or `publish-all-seeds`.
2. **Day-to-day editing:** CMS **Edit** menu entities → **Publish** (or scheduled publish).
3. **Snapshot build:** `ConfigSnapshotBuilder` joins draft sections/cards/messages; if empty, publish can use seed file.

## API shape

Published JSON must match what the frontend loader expects (`loadBrandConfig.js`):

- `sections[]` with `cards[]`, price fields (`prices`, `prebuiltPrices`, `handlelessPrices`, `card.prices`, etc.)
- `messages` dictionary (P3)

See `.cursor/skills/price-configurator-back-office/reference.md` for field-level notes.

## When editing prices

| Brand status | Edit here | Do not edit |
|--------------|-----------|-------------|
| Not on API yet | Frontend `src/brands/{brand}/` then re-sync seeds | — |
| On API (prod) | CMS + publish | Frontend `prices.js` / `index.js` for that brand |
| Copy (P3) | `ConfiguratorMessages` in CMS | `config/{brand}/messages.json` after cutover |

Frontend flags: `USE_CONFIG_API`, `USE_CONFIG_API_BRANDS`, `configuratorBackofficeUrl` in Zeus settings.
