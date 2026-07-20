[CmdletBinding()]
param(
    [string] $CredentialPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "Dagmay.Secrets.psm1") -Force

$parameters = @{}
if (-not [string]::IsNullOrWhiteSpace($CredentialPath)) {
    $parameters.Path = $CredentialPath
}

if (Test-DagmayGeminiCredential @parameters) {
    Write-Host "PASS: encrypted Gemini credential is present and decryptable by this Windows user." -ForegroundColor Green
    exit 0
}

Write-Error "Encrypted Gemini credential is absent or cannot be decrypted by this Windows user."
exit 1
