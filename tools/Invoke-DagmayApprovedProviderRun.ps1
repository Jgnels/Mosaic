[CmdletBinding()]
param(
    [ValidateSet("HARDENED_PROVIDER_SMOKE_V1", "MOSAIC_MEMORY_GROUNDING_PILOT_V1", "MOSAIC_RELATIONSHIP_BALANCE_PILOT_V1")]
    [string] $Protocol = "HARDENED_PROVIDER_SMOKE_V1",
    [string] $Python
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$approvalPath = Join-Path $repoRoot "research\approvals\free_tier_provider_approval.json"
$gatePath = Join-Path $repoRoot "research\provider_gate_status.json"

if ([string]::IsNullOrWhiteSpace($Python)) {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $pythonCommand) {
        throw "Python 3 was not found. Pass -Python with an explicit executable path."
    }
    $Python = $pythonCommand.Source
}

$approval = Get-Content -LiteralPath $approvalPath -Raw | ConvertFrom-Json
$gate = Get-Content -LiteralPath $gatePath -Raw | ConvertFrom-Json
if ($approval.status -ne "APPROVED") {
    throw "Provider approval is not active."
}
if ($approval.allowed_protocols -notcontains $Protocol) {
    throw "The selected protocol is not approved."
}
if (-not $gate.real_provider_authorized) {
    throw "The canonical provider gate does not authorize a real-provider smoke test."
}
if ($gate.authorized_protocols -notcontains $Protocol) {
    throw "The canonical provider gate does not authorize the selected protocol."
}

$runId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$runRoot = Join-Path $env:LOCALAPPDATA "Dagmay\provider-runs\$runId"
$output = Join-Path $runRoot "provider-result.json"
$archive = Join-Path $runRoot "payloads"
$checkpoint = Join-Path $runRoot "checkpoint.json"
$ledger = Join-Path $env:LOCALAPPDATA "Dagmay\provider-budget\gemini-3.1-flash-lite.json"
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null

Import-Module (Join-Path $PSScriptRoot "Dagmay.Secrets.psm1") -Force

Invoke-WithDagmayGeminiCredential -ScriptBlock {
    if ($Protocol -eq "MOSAIC_RELATIONSHIP_BALANCE_PILOT_V1") {
        & $Python (Join-Path $repoRoot "syntheticlab\run_mosaic_relationship_balance_pilot.py") `
            --approval $approvalPath --output $output --checkpoint $checkpoint `
            --ledger $ledger --payload-archive $archive
    }
    elseif ($Protocol -eq "MOSAIC_MEMORY_GROUNDING_PILOT_V1") {
        & $Python (Join-Path $repoRoot "syntheticlab\run_mosaic_memory_grounding_pilot.py") `
            --approval $approvalPath --output $output --checkpoint $checkpoint `
            --ledger $ledger --payload-archive $archive
    }
    else {
        & $Python (Join-Path $repoRoot "syntheticlab\run_hardened_provider_smoke.py") `
            --approval $approvalPath --output $output --ledger $ledger `
            --payload-archive $archive
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Hardened provider smoke test failed with exit code $LASTEXITCODE."
    }
}

Write-Host "PASS: bounded hardened provider smoke test completed." -ForegroundColor Green
Write-Host "Result: $output"
