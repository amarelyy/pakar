<#
.SYNOPSIS
  Builds the Blazor WebAssembly site into a folder that can be deployed to Vercel as a static site.

.EXAMPLE
  ./scripts/publish-vercel.ps1 -ApiUrl https://pakar-api.example.com -ModelPath C:\models\best.onnx -VideoDir C:\videos
  vercel deploy artifacts/vercel --prod

.NOTES
  The model (*.onnx) and the videos are not in git. They are copied into the published site here, so the
  deploy must be made from this folder (Vercel's git build would not have them).
#>
param(
    [Parameter(Mandatory)][string]$ApiUrl,
    [string]$ModelPath,
    [string]$VideoDir,
    [string]$OutDir = "artifacts/vercel"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$web = Join-Path $root "src/Pakar.Web"

if ($ApiUrl -notmatch '^https://') { throw "ApiUrl must be an https:// address (the site is served over https)." }

# 1. Model and videos go into the git-ignored asset folders of the web project.
if ($ModelPath) {
    if (-not (Test-Path $ModelPath)) { throw "Model not found: $ModelPath" }
    Copy-Item $ModelPath (Join-Path $web "wwwroot/models/best.onnx") -Force
}
if ($VideoDir) {
    if (-not (Test-Path $VideoDir)) { throw "Video folder not found: $VideoDir" }
    Get-ChildItem (Join-Path $VideoDir '*') -Include *.mp4, *.webm -File | Copy-Item -Destination (Join-Path $web "wwwroot/videos") -Force
}

# 2. Publish. Compression is left to Vercel's CDN, so the .br and .gz copies are not generated.
$publish = Join-Path ([IO.Path]::GetTempPath()) "pakar-publish"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish (Join-Path $web "Pakar.Web.csproj") -c Release -o $publish -p:CompressionEnabled=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

# 3. Assemble the deployable folder.
$out = Join-Path $root $OutDir
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null
Copy-Item (Join-Path $publish "wwwroot/*") $out -Recurse
Copy-Item (Join-Path $root "infra/vercel/vercel.json") $out

# 4. Point the site at the real API.
$settingsPath = Join-Path $out "appsettings.json"
$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
$settings.Api.BaseUrl = $ApiUrl.TrimEnd('/') + "/"
[IO.File]::WriteAllText($settingsPath, ($settings | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))

$hasModel = Test-Path (Join-Path $out "models/best.onnx")
$videos = @(Get-ChildItem (Join-Path $out "videos") -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -in ".mp4", ".webm" })
Write-Host "Site folder : $out"
Write-Host "API address : $($settings.Api.BaseUrl)"
Write-Host "Model       : $(if ($hasModel) { 'included' } else { 'MISSING - the feeder page cannot run without models/best.onnx' })"
Write-Host "Videos      : $($videos.Count) file(s)"
Write-Host "Deploy with : vercel deploy `"$OutDir`" --prod   (add the site's address to the API's Cors__Origins__0 setting)"
