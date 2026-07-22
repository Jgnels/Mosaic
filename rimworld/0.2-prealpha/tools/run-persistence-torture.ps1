[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $Root "artifacts\Dagmay-persistence-torture-latest.json"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK 8 or newer was not found."
}

$Arguments = @(
    "run",
    "--project", "Dagmay.IntegrationHarness/Dagmay.IntegrationHarness.csproj",
    "--configuration", $Configuration,
    "--",
    "--scenario", "PersistenceTorture",
    "--output", $OutputPath,
    "--fail-fast"
)

Push-Location $Root
try {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Dagmay persistence-torture harness failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
    throw "Persistence-torture report was not created at '$OutputPath'."
}
$Report = Get-Content -LiteralPath $OutputPath -Raw | ConvertFrom-Json
if (-not ($Report.scenarioCount -eq 1 -and $Report.failedCount -eq 0 -and $Report.scenarios.Count -eq 1 -and $Report.scenarios[0].name -eq "PersistenceTorture" -and $Report.scenarios[0].passed)) {
    throw "Persistence-torture report did not contain one successful PersistenceTorture scenario."
}

Write-Host "Dagmay 0.1L persistence-torture harness passed."
Write-Host "Machine-readable report: $OutputPath"