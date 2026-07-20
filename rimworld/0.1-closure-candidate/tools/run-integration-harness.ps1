[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Scenario = "",

    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $Root "artifacts\Dagmay-integration-latest.json"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK 8 or newer was not found."
}

$Arguments = @(
    "run",
    "--project", "Dagmay.IntegrationHarness/Dagmay.IntegrationHarness.csproj",
    "--configuration", $Configuration,
    "--",
    "--output", $OutputPath
)
if (-not [string]::IsNullOrWhiteSpace($Scenario)) {
    $Arguments += @("--scenario", $Scenario)
}

Push-Location $Root
try {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Dagmay integration harness failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

Write-Host "Integration harness passed."
Write-Host "Report: $OutputPath"
