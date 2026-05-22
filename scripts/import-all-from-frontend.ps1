# Full import pipeline: Zeus frontend bundled data → seed JSON → Cosmos CMS draft → publish.
#
# Prerequisites:
#   - Node.js + npx (for esbuild export)
#   - Cosmos emulator running
#   - Backoffice running and you are logged in (for HTTP steps), OR use Swagger after login
#
# Usage:
#   .\scripts\import-all-from-frontend.ps1
#   .\scripts\import-all-from-frontend.ps1 -SkipPublish
#   .\scripts\import-all-from-frontend.ps1 -BackofficeUrl https://localhost:7161

param(
    [switch]$SkipPublish,
    [string]$BackofficeUrl = ""
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "Step 1/3: Export bundled data from frontend to Data/Seeds/*.payload.json"
& (Join-Path $RepoRoot "scripts\sync-seeds-from-frontend.ps1")

Write-Host ""
Write-Host "Step 2/3: Import seeds into Cosmos CMS draft entities"
Write-Host "  Call one of these while authenticated (browser session or Swagger):"
Write-Host "  POST $BackofficeUrl/api/admin/import-all-from-seed"
if (-not $SkipPublish) {
    Write-Host ""
    Write-Host "Step 3/3: Publish all brands to public API snapshots"
    Write-Host "  POST $BackofficeUrl/api/admin/bootstrap-from-seed"
    Write-Host "  (imports + publishes in one call)"
} else {
    Write-Host ""
    Write-Host "Step 3/3: Skipped (-SkipPublish). Publish manually:"
    Write-Host "  POST $BackofficeUrl/api/admin/publish-all-seeds"
}

Write-Host ""
Write-Host "Per brand:"
Write-Host "  POST /api/admin/import-from-seed?brand=magnet&language=en-GB"
Write-Host "  POST /api/admin/publish-seed?brand=magnet&language=en-GB"
Write-Host ""
Write-Host "Verify public API:"
Write-Host "  GET /api/config/v1?brand=magnet&language=en-GB"
