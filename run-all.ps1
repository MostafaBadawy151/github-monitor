<#
.SYNOPSIS
  Starts the WCF host (with Quartz), the ASP.NET Core API and the Angular dev server, each in its own window.

.EXAMPLE
  ./run-all.ps1 -Token ghp_xxx
  ./run-all.ps1                       # uses user-secrets / GitHub__Token / GITHUB_TOKEN if already configured
  ./run-all.ps1 -Owner dotnet -Repository runtime   # demo against a public repo (no token needed)
#>
param(
    [string]$Token,
    [string]$Owner,
    [string]$Repository,
    [int]$UiPort = 4200
)

$root = $PSScriptRoot
# Set in this process so the child windows inherit them. Keeps the token off any command line.
if ($Token)      { $env:GitHub__Token = $Token }
if ($Owner)      { $env:GitHub__Owner = $Owner }
if ($Repository) { $env:GitHub__Repository = $Repository }

Write-Host "Building .NET solution..." -ForegroundColor Cyan
dotnet build "$root/GitHubMonitor.slnx" -v q
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

if (-not (Test-Path "$root/client/github-monitor-ui/node_modules")) {
    Write-Host "Installing Angular dependencies..." -ForegroundColor Cyan
    Push-Location "$root/client/github-monitor-ui"; npm install; Pop-Location
}

Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run --no-build --project '$root/src/GitHubMonitor.WcfHost' --launch-profile http"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run --no-build --project '$root/src/GitHubMonitor.Api' --launch-profile http"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Location '$root/client/github-monitor-ui'; npx ng serve --port $UiPort --open"

Write-Host ""
Write-Host "WCF + Quartz : http://localhost:5100/GitHubCommitService.svc?wsdl  (health: /health)" -ForegroundColor Green
Write-Host "API (Swagger): http://localhost:5200/swagger" -ForegroundColor Green
Write-Host "Angular UI   : http://localhost:$UiPort" -ForegroundColor Green
