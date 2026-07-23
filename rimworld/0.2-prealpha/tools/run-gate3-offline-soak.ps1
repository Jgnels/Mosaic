[CmdletBinding()]
param(
    [ValidateSet("Short", "Long")]
    [string]$Preset = "Short",

    [int]$Seed = 2031,

    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Cycles = if ($Preset -eq "Long") { 5000 } else { 250 }
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $Root ("artifacts\Dagmay-gate3-offline-soak-{0}.json" -f $Preset.ToLowerInvariant())
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK 8 or newer was not found."
}

Push-Location $Root
try {
    dotnet run `
        --project Dagmay.IntegrationHarness/Dagmay.IntegrationHarness.csproj `
        --configuration Release `
        -- `
        --scenario Gate3OfflineSoak `
        --cycles $Cycles `
        --seed $Seed `
        --output $OutputPath `
        --fail-fast
    if ($LASTEXITCODE -ne 0) {
        throw "Gate 3 offline soak failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

$Report = Get-Content -LiteralPath $OutputPath -Raw | ConvertFrom-Json
$Scenario = $Report.scenarios[0]
$Passed = $Report.scenarioCount -eq 1 `
    -and $Report.failedCount -eq 0 `
    -and $Scenario.name -eq "Gate3OfflineSoak" `
    -and $Scenario.passed `
    -and $Scenario.details.providerMode -eq "offline" `
    -and $Scenario.details.saveFilesTouched -eq "none" `
    -and $Scenario.details.finalStateSha256 -match '^[0-9a-f]{64}$'
if (-not $Passed) {
    throw "Gate 3 offline soak report did not contain the required bounded offline evidence."
}

Write-Host "Mosaic Gate 3 offline soak passed."
Write-Host "Preset: $Preset; cycles: $Cycles; seed: $Seed"
Write-Host "Final-state SHA-256: $($Scenario.details.finalStateSha256)"
Write-Host "Sampled peak managed bytes: $($Scenario.metrics.sampledPeakManagedBytes)"
Write-Host "Report: $OutputPath"
