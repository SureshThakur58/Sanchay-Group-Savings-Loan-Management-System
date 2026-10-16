[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$ResultsDirectory = "TestResults/Phase27"
)

$ErrorActionPreference = "Stop"

Write-Host "=== GroupFinance Phase 27 — Comprehensive Testing ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"

New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null

Write-Host "`n[1/3] Building solution..." -ForegroundColor Yellow
dotnet build GroupFinance.sln --configuration $Configuration --no-restore

Write-Host "`n[2/3] Running unit tests..." -ForegroundColor Yellow
dotnet test tests/GroupFinance.UnitTests/GroupFinance.UnitTests.csproj `
    --configuration $Configuration `
    --no-build `
    --logger "trx;LogFileName=Phase27-UnitTests.trx" `
    --results-directory $ResultsDirectory

Write-Host "`n[3/3] Running integration/security tests..." -ForegroundColor Yellow
if ([string]::IsNullOrWhiteSpace($env:GROUPFINANCE_TEST_CONNECTION_STRING)) {
    Write-Warning "GROUPFINANCE_TEST_CONNECTION_STRING is not set. SQL Server integration tests will be skipped."
} else {
    dotnet test tests/GroupFinance.IntegrationTests/GroupFinance.IntegrationTests.csproj `
        --configuration $Configuration `
        --no-build `
        --logger "trx;LogFileName=Phase27-IntegrationTests.trx" `
        --results-directory $ResultsDirectory
}

Write-Host "`nPhase 27 automated test execution completed." -ForegroundColor Green
