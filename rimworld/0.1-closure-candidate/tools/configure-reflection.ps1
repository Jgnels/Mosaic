[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Offline", "Fake", "Google")]
    [string]$Mode,

    [string]$Model = "gemini-3.1-flash-lite",

    [switch]$KeepExistingGoogleKey,

    [switch]$RemoveStoredGoogleKey
)

$ErrorActionPreference = "Stop"
$normalizedMode = $Mode.ToLowerInvariant()

if ($normalizedMode -eq "google" -and $RemoveStoredGoogleKey) {
    throw "Google mode cannot be enabled while removing its stored key. Choose Offline or Fake."
}

if ($normalizedMode -eq "google") {
    if ([string]::IsNullOrWhiteSpace($Model) -or $Model -notmatch '^[A-Za-z0-9._-]{1,128}$') {
        throw "The model ID contains unsupported characters."
    }

    if (-not $KeepExistingGoogleKey) {
        $secureKey = Read-Host "Paste your Google AI Studio API key (it will not be displayed)" -AsSecureString
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
        try {
            $plainKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
            if ([string]::IsNullOrWhiteSpace($plainKey)) {
                throw "No API key was entered. No Google configuration was saved."
            }

            $normalizedKey = $plainKey.Trim()
            [Environment]::SetEnvironmentVariable("DAGMAY_GOOGLE_API_KEY", $normalizedKey, "User")
            $env:DAGMAY_GOOGLE_API_KEY = $normalizedKey
        }
        finally {
            if ($null -ne $pointer -and $pointer -ne [IntPtr]::Zero) {
                [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
            }

            Remove-Variable plainKey -ErrorAction SilentlyContinue
            Remove-Variable normalizedKey -ErrorAction SilentlyContinue
        }
    }
    else {
        $existingKey = [Environment]::GetEnvironmentVariable("DAGMAY_GOOGLE_API_KEY", "User")
        if ([string]::IsNullOrWhiteSpace($existingKey)) {
            throw "No stored Google API key exists. Run Google mode once without -KeepExistingGoogleKey."
        }

        $env:DAGMAY_GOOGLE_API_KEY = $existingKey
        Remove-Variable existingKey -ErrorAction SilentlyContinue
    }

    [Environment]::SetEnvironmentVariable("DAGMAY_GOOGLE_MODEL", $Model.Trim(), "User")
    $env:DAGMAY_GOOGLE_MODEL = $Model.Trim()
}

if ($RemoveStoredGoogleKey) {
    [Environment]::SetEnvironmentVariable("DAGMAY_GOOGLE_API_KEY", $null, "User")
    [Environment]::SetEnvironmentVariable("DAGMAY_GOOGLE_MODEL", $null, "User")
    Remove-Item Env:DAGMAY_GOOGLE_API_KEY -ErrorAction SilentlyContinue
    Remove-Item Env:DAGMAY_GOOGLE_MODEL -ErrorAction SilentlyContinue
}

[Environment]::SetEnvironmentVariable("DAGMAY_REFLECTION_MODE", $normalizedMode, "User")
$env:DAGMAY_REFLECTION_MODE = $normalizedMode

Write-Host "Dagmay reflection mode is now '$normalizedMode'."
if ($normalizedMode -eq "google") {
    Write-Host "Google model: $Model"
    Write-Host "The key was stored in your Windows user environment and was not written into Dagmay source or settings files."
}
elseif ($RemoveStoredGoogleKey) {
    Write-Host "The stored Dagmay Google API key and model selection were removed from your Windows user environment."
}

Write-Host "Close RimWorld completely and start it normally from Steam before testing this change."
