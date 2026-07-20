[CmdletBinding()]
param(
    [Security.SecureString] $SecureKey,
    [string] $CredentialPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$modulePath = Join-Path $PSScriptRoot "Dagmay.Secrets.psm1"
Import-Module $modulePath -Force

if ($null -eq $SecureKey) {
    $SecureKey = Read-Host "Paste Gemini API key (stored encrypted for this Windows user)" -AsSecureString
}

if ($SecureKey.Length -lt 16) {
    throw "The supplied credential is unexpectedly short. Nothing was stored."
}

$saveParameters = @{ SecureKey = $SecureKey }
if (-not [string]::IsNullOrWhiteSpace($CredentialPath)) {
    $saveParameters.Path = $CredentialPath
}

$result = Save-DagmayGeminiCredential @saveParameters

Write-Host "Gemini credential configured." -ForegroundColor Green
Write-Host "Protection: $($result.protection)"
Write-Host "Location: $($result.path)"
Write-Host "The plaintext key was not written to the repository or printed."
