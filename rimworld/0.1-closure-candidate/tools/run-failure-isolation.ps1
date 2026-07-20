[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $Root "artifacts\Dagmay-failure-isolation-latest.json"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK 8 or newer was not found."
}

$Arguments = @(
    "run",
    "--project", "Dagmay.IntegrationHarness/Dagmay.IntegrationHarness.csproj",
    "--configuration", $Configuration,
    "--",
    "--scenario", "FailureIsolation",
    "--output", $OutputPath,
    "--fail-fast"
)

Push-Location $Root
try {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Dagmay failure-isolation harness failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
    throw "Failure-isolation report was not created at '$OutputPath'."
}
$Report = Get-Content -LiteralPath $OutputPath -Raw | ConvertFrom-Json
$Scenario = $Report.scenarios[0]
if (-not ($Report.scenarioCount -eq 1 -and $Report.failedCount -eq 0 -and $Report.scenarios.Count -eq 1 -and $Scenario.name -eq "FailureIsolation" -and $Scenario.passed)) {
    throw "Failure-isolation report did not contain one successful FailureIsolation scenario."
}
if (-not ($Scenario.metrics.providerFailureCases -eq 6 -and
          $Scenario.metrics.pendingCommitRecoveryCases -eq 3 -and
          $Scenario.metrics.canonicalRecoveryCommits -eq 1 -and
          $Scenario.metrics.storageMismatchAutoAdoptions -eq 0 -and
          $Scenario.metrics.shutdownReloadChecks -ge 7)) {
    throw "Failure-isolation report did not contain the required fail-closed metrics."
}

Write-Host "Dagmay 0.1M failure-isolation harness passed."
Write-Host "Machine-readable report: $OutputPath"