<#
.SYNOPSIS
  Runs Pakar.Api in Production mode for the public demo: the database is Neon, the API listens on localhost:5249,
  and a tunnel (see infra/vercel/README.md) makes it reachable from the Vercel site.

.DESCRIPTION
  Secrets are read from this machine only and passed to the API process as environment variables.
  Nothing is printed or written to a file:
    - Jwt:Key, Ingest:ApiKey, Admin:Email, Admin:Password come from the API's user-secrets.
    - DATABASE_URL comes from the git-ignored .env.local (run `npx neon@9.3.2 env pull` once to create it).
  Production mode means: no Swagger, no dev endpoints, no automatic migrations.

.EXAMPLE
  ./scripts/run-api-tunnel.ps1 -CorsOrigin https://pakar-parkir.vercel.app
#>
param([Parameter(Mandatory)][string]$CorsOrigin)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

# The user-secrets id is declared in the API project file.
$csproj = [xml](Get-Content (Join-Path $root "src/Pakar.Api/Pakar.Api.csproj"))
$secretsId = ($csproj.Project.PropertyGroup.UserSecretsId | Where-Object { $_ })[0]
$secretsFile = Join-Path $env:APPDATA "Microsoft/UserSecrets/$secretsId/secrets.json"
if (-not (Test-Path $secretsFile)) { throw "No user-secrets found. Set Jwt:Key, Ingest:ApiKey, Admin:Email and Admin:Password first (README.md)." }
$secrets = Get-Content $secretsFile -Raw | ConvertFrom-Json

foreach ($pair in @{ "Jwt__Key" = "Jwt:Key"; "Ingest__ApiKey" = "Ingest:ApiKey"; "Admin__Email" = "Admin:Email"; "Admin__Password" = "Admin:Password" }.GetEnumerator()) {
    $value = $secrets.($pair.Value)
    if ([string]::IsNullOrWhiteSpace($value)) { throw "User-secret '$($pair.Value)' is not set." }
    Set-Item "env:$($pair.Key)" $value
}

$envFile = Join-Path $root ".env.local"
if (-not (Test-Path $envFile)) { throw ".env.local is missing. Run: npx neon@9.3.2 env pull" }
foreach ($line in Get-Content $envFile) {
    if ($line -match '^(DATABASE_URL)="(.*)"$') { Set-Item "env:$($Matches[1])" $Matches[2] }
}
if (-not $env:DATABASE_URL) { throw "DATABASE_URL was not found in .env.local." }

$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://localhost:5249"
$env:Cors__Origins__0 = $CorsOrigin

Set-Location $root
dotnet run --project src/Pakar.Api -c Release --no-launch-profile
