# Start Cosmos emulator from docker-compose (API can use host port 18081).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "Starting Cosmos DB emulator..."
docker compose up -d cosmos-emulator

Write-Host "Waiting for http://localhost:18080/ready ..."
$deadline = (Get-Date).AddMinutes(3)
while ((Get-Date) -lt $deadline) {
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:18080/ready" -UseBasicParsing -TimeoutSec 5
        if ($r.StatusCode -eq 200) {
            Write-Host "Cosmos emulator is ready."
            Write-Host "API (host):    http://localhost:18081"
            Write-Host "Data Explorer: http://localhost:11234"
            exit 0
        }
    }
    catch {
        Start-Sleep -Seconds 5
    }
}

Write-Host "Timed out. Check: docker compose logs -f cosmos-emulator"
exit 1
