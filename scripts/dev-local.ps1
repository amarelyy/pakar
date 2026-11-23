<#
.SYNOPSIS
  Starts everything for local development: the postgres container, the API and the web app.

.NOTES
  First time only: copy infra/.env.example to infra/.env and set POSTGRES_PASSWORD, then store the connection string
  and the other secrets in the API's user-secrets (see README.md, section "Pakar.Api").
  Put best.onnx in src/Pakar.Web/wwwroot/models/ to use the browser feeder with a real model.
#>
param([switch]$NoDocker)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if (-not $NoDocker) {
    if (-not (Test-Path (Join-Path $root "infra/.env"))) { throw "infra/.env is missing. Copy infra/.env.example and set POSTGRES_PASSWORD." }
    docker compose -f (Join-Path $root "infra/docker-compose.yml") up -d
    if ($LASTEXITCODE -ne 0) { throw "docker compose failed. Is Docker Desktop running?" }
}

# One window per process, so each keeps its own log and Ctrl+C stops only that process.
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$root'; dotnet run --project src/Pakar.Api"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$root'; dotnet run --project src/Pakar.Web"

Write-Host "API : http://localhost:5249  (Swagger at the root)"
Write-Host "Web : http://localhost:5184"
Write-Host "Log in as the admin from your user-secrets, then open /admin/feeder."
