# Regenerate Data/Seeds/*.payload.json from the Zeus frontend repo.
# Default frontend: C:\Niteco-Project\Nobia\price-configurator
#
# Usage:
#   .\scripts\sync-seeds-from-frontend.ps1
#   $env:PRICE_CONFIGURATOR_FRONTEND = "D:\other\price-configurator"; .\scripts\sync-seeds-from-frontend.ps1

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Frontend = if ($env:PRICE_CONFIGURATOR_FRONTEND) { $env:PRICE_CONFIGURATOR_FRONTEND } else { "C:\Niteco-Project\Nobia\price-configurator" }

if (-not (Test-Path $Frontend)) {
    Write-Error "Frontend repo not found: $Frontend. Set PRICE_CONFIGURATOR_FRONTEND or clone price-configurator."
}

Push-Location $Frontend
try {
    node (Join-Path $RepoRoot "scripts\sync-seeds-from-frontend.mjs")
}
finally {
    Pop-Location
}
