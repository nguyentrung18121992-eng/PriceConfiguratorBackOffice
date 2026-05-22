# Price Configurator Back Office

CMS and publish API for the Zeus **price-configurator** app. Editors manage catalog, prices, and copy; the public site reads **published** configuration from `GET /api/config/v1`.

| Repo | Path | Role |
|------|------|------|
| **Frontend (bundled data source)** | `C:\Niteco-Project\Nobia\price-configurator` | `src/brands/{brand}/`, `config/{brand}/messages.json` |
| **This repo** | `PriceConfiguratorBackOffice` | CMS, Cosmos, publish API |
| **Stack reference** | `kitchen-quiz-backoffice` | Nobia.CmsToolkit + Cosmos patterns |

**Docs & agent skill:** `docs/price-configurator-back-office-strategy.md`, `.cursor/skills/price-configurator-back-office/`. **Data flow:** `docs/DATA.md`.

## Build

Requires access to the **omni-backend** Azure DevOps NuGet feed (same as [kitchen-quiz-backoffice](../kitchen-quiz-backoffice)).

```powershell
cd price-configurator-back-office

# If restore fails with NU1301 on Nobia transitive packages, build kitchen-quiz once
# (populates the local NuGet cache), then:
dotnet restore
dotnet build
```

The project sets `RestoreIgnoreFailedSources` so restore can use cached Nobia packages when a legacy feed URL returns 401.

## Quick start (local)

1. Ensure `dotnet build` succeeds (see above).
2. Set a **Cosmos DB** connection string (not MongoDB):

   | Environment | File |
   |-------------|------|
   | `local` (default in launchSettings) | `appsettings.local.json` — copy from `appsettings.local.json.example` |
   | Azure dev/test | User secrets or platform config |

   **Cosmos DB Emulator** (local):

   ```json
   "ConnectionStrings": {
     "priceconfigurator": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
   }
   ```

   **Docker Desktop (back office + Cosmos emulator):**

   ```powershell
   cd PriceConfiguratorBackOffice
   copy .env.example .env
   # .env: FEED_ACCESSTOKEN (build), ASPNETCORE_ENVIRONMENT=local
   docker compose build back-office
   docker compose up -d
   curl http://localhost:18080/ready
   ```

   Back office connects to **`http://cosmos-emulator:8081`** on the Docker network (default). Override `ConnectionStrings__priceconfigurator` in `.env` for Windows emulator or Azure.

   | Service | URL |
   |---------|-----|
   | Back office (CMS + API) | http://localhost:7161 |
   | Swagger | http://localhost:7161/swagger |
   | Cosmos (from host) | http://localhost:18081 |
   | Cosmos Data Explorer | http://localhost:11234 |

   **Cosmos emulator only** (API with `dotnet run` on host): `http://localhost:18081` in `appsettings.local.json`.

   **Cosmos on host + `dotnet run`:** Windows emulator → `https://localhost:8081` in `appsettings.local.json`.

   **Windows desktop emulator:** use `https://localhost:8081` and copy `appsettings.WindowsCosmosEmulator.json` over `appsettings.local.json`, or set:

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = "WindowsCosmosEmulator"
   ```

   Start the **Azure Cosmos DB Emulator** app from the Start menu before running the API.

   ### Troubleshooting `CosmosException 503` / `GatewayStoreClient Request Timeout`

   | Cause | Fix |
   |-------|-----|
   | Emulator not running | `docker compose up -d` (includes cosmos-emulator) or start Windows emulator |
   | Still starting | Wait for `http://localhost:18080/ready` (up to ~2 min) |
   | API in Docker, external Cosmos | Use service hostname or `host.docker.internal`, not `localhost` |
   | `Connection refused 127.0.0.1:8081` from API container | Recreate stack: `GATEWAY_PUBLIC_ENDPOINT=cosmos-emulator` on the emulator service (see `docker-compose.yml`) |
   | `Collection not found` / import fails | Wait for Cosmos ready, then restart back-office or retry Import from seed (EnsureCreated retries on startup) |
   | Wrong protocol | Docker vNext → **http** (8081 in network, 18081 on host); Windows app → **https**://8081 |
   | MongoDB URL in config | Remove `mongodb://...`; only Cosmos connection strings work |

   The `mongodb://...` URL in **price-configurator** `docker-compose.yml` is **not** this service.

3. Run:

   ```bash
   cd price-configurator-back-office
   dotnet run --project price-configurator-back-office
   ```

4. Log in to the CMS admin UI (Azure AD).
5. Bootstrap Magnet published config from seed (authenticated):

   ```http
   POST https://localhost:7161/api/admin/publish-seed?brand=magnet&language=en-GB
   ```

6. Verify public API:

   ```http
   GET https://localhost:7161/api/config/v1?brand=magnet&language=en-GB
   ```

## Regenerate all brand seeds (P2)

From **this repo** (reads frontend at `C:\Niteco-Project\Nobia\price-configurator` by default):

```powershell
.\scripts\sync-seeds-from-frontend.ps1
```

Writes `price-configurator-back-office/Data/Seeds/{brand}-{language}.payload.json` (sections + messages).

Override frontend path: `$env:PRICE_CONFIGURATOR_FRONTEND = "D:\path\to\price-configurator"`.

Alternatively, from the frontend repo if it sits next to a `price-configurator-back-office` folder: `npm run export-brand-config`.

### Import all frontend data into CMS + publish

```powershell
# 1) Export FE bundled data → Data/Seeds/*.payload.json
.\scripts\sync-seeds-from-frontend.ps1

# 2) Start backoffice, log in, then (authenticated):
POST /api/admin/import-all-from-seed
POST /api/admin/bootstrap-from-seed
```

Or use `.\scripts\import-all-from-frontend.ps1` for step 1 and API instructions.

| Endpoint | Description |
|----------|-------------|
| `POST /api/admin/import-from-seed?brand=&language=` | Seed JSON → CMS sections/cards/messages/settings |
| `POST /api/admin/import-all-from-seed` | All brands |
| `POST /api/admin/bootstrap-from-seed` | Import all + publish all |
| `POST /api/admin/publish-all-seeds` | Publish from draft or seed only |

## API

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/config/v1?brand=&language=` | No | Published config for frontend |
| POST | `/api/publish/v1?brand=&language=` | Yes | Publish draft (or seed) now |
| POST | `/api/publish/v1/schedule?brand=&language=&scheduledAtUtc=` | Yes | Schedule publish |
| POST | `/api/admin/publish-seed?brand=&language=` | Yes | Publish from `Data/Seeds/*.json` |
| POST | `/api/admin/publish-all-seeds` | Yes | Publish every brand seed (P2) |
| POST | `/api/preview/v1/token?brand=&language=` | Yes | Create draft preview URL (P4) |
| GET | `/api/config/v1/preview?brand=&language=&token=` | No | Draft config for preview token |
| GET | `/api/healthcheck` | No | Health |

## Phases

| Phase | Status | Notes |
|-------|--------|-------|
| P0 Foundation | Done | Project, CMS entities, auth, infra template |
| P1 Magnet | Done | Seed + API + frontend loader |
| P2 Other brands | Done | All `*.payload.json` seeds + `publish-all-seeds` + `USE_CONFIG_API_BRANDS` |
| P3 Copy in CMS | Done | `messages` in API + `ConfiguratorMessages` entity; optional `USE_LOCAL_MESSAGES` |
| P4 Preview | Done | `POST /api/preview/v1/token`, `GET /api/config/v1/preview` |

See `docs/price-configurator-back-office-strategy.md` and `docs/DATA.md`.

## Frontend integration

Set in **price-configurator** (`C:\Niteco-Project\Nobia\price-configurator`) Zeus settings:

- `configuratorBackofficeUrl` — backoffice base URL per environment
- `USE_CONFIG_API=true` — Magnet loads from API (see `src/brands/loadBrandConfig.js`)
