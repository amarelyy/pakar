<#
.SYNOPSIS
  Copies the compose file, scripts, production settings, model and videos to the server.
.EXAMPLE
  ./infra/vm/upload.ps1 -VmHost pakar.example.cloudapp.azure.com -User azureuser
#>
param(
    [Parameter(Mandatory)][string]$VmHost,
    [Parameter(Mandatory)][string]$User,
    [string]$Key = "$HOME/.ssh/id_ed25519",
    [string]$EnvFile = "infra/vm/.env.production",
    [string]$Assets = "artifacts/vm-assets"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

# Check everything locally before touching the server.
$required = @("infra/vm/compose.yml", "infra/vm/setup.sh", "infra/vm/deploy.sh", $EnvFile, "$Assets/models/best.onnx", $Key)
foreach ($path in $required) {
    if (-not (Test-Path $path)) { throw "Not found: $path" }
}
$videos = @(Get-ChildItem "$Assets/videos" -File -ErrorAction SilentlyContinue)
if ($videos.Count -eq 0) { Write-Warning "No video in $Assets/videos; the map will show no live data until videos are added." }
if ((Split-Path -Leaf $Assets) -ne "vm-assets") { throw "The assets folder must be named vm-assets (the runbook expects it)." }

$target = "$User@$VmHost"
function Send-File($from, $to) {
    scp -i $Key -r $from "${target}:$to"
    if ($LASTEXITCODE -ne 0) { throw "Upload failed: $from" }
}

ssh -i $Key $target "mkdir -p pakar-upload"
if ($LASTEXITCODE -ne 0) { throw "Cannot connect to $target" }
Send-File "infra/vm/compose.yml" "pakar-upload/compose.yml"
Send-File "infra/vm/setup.sh" "pakar-upload/setup.sh"
Send-File "infra/vm/deploy.sh" "pakar-upload/deploy.sh"
Send-File $EnvFile "pakar-upload/.env"
Send-File $Assets "pakar-upload/"

Write-Host "Uploaded $($videos.Count) video(s). Next, on the server: see infra/vm/README.md, step 4."
