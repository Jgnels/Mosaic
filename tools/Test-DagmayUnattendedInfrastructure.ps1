[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("Dagmay-Unattended-Test-" + [Guid]::NewGuid().ToString("N"))
$credentialPath = Join-Path $testRoot "credential.dpapi"

try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
    Import-Module (Join-Path $PSScriptRoot "Dagmay.Secrets.psm1") -Force

    $dummy = ConvertTo-SecureString "test-only-not-a-real-provider-key-123456" -AsPlainText -Force
    Save-DagmayGeminiCredential -SecureKey $dummy -Path $credentialPath | Out-Null

    if (-not (Test-DagmayGeminiCredential -Path $credentialPath)) {
        throw "DPAPI credential round-trip failed."
    }

    $observed = $null
    Invoke-WithDagmayGeminiCredential -Path $credentialPath -ScriptBlock {
        $script:observed = $env:DAGMAY_GEMINI_API_KEY
    }

    if ($observed -ne "test-only-not-a-real-provider-key-123456") {
        throw "Credential was not exposed correctly to the bounded child scope."
    }

    if (-not [string]::IsNullOrEmpty($env:DAGMAY_GEMINI_API_KEY)) {
        throw "Credential remained in the process environment after the bounded child scope."
    }

    $providerRunner = Get-Content -LiteralPath (Join-Path $PSScriptRoot "Invoke-DagmayApprovedProviderRun.ps1") -Raw
    if ($providerRunner -notmatch 'ValidateSet\("HARDENED_PROVIDER_SMOKE_V1"\)') {
        throw "Provider runner permits an unreviewed protocol surface."
    }
    if ($providerRunner -notmatch "real_provider_authorized") {
        throw "Provider runner does not enforce the canonical provider gate."
    }

    Write-Host "PASS: DPAPI credential lifecycle and bounded provider controls." -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
