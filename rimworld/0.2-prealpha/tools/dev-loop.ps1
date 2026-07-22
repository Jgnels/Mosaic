[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$RimWorldPath = "",

    [switch]$Install,

    [switch]$Launch,

    [switch]$SkipRimWorld
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

if ($Launch -and -not $Install) {
    throw "-Launch requires -Install so RimWorld never starts with an unverified package."
}
if ($SkipRimWorld -and ($Install -or $Launch)) {
    throw "-SkipRimWorld cannot be combined with -Install or -Launch."
}

$BuildArguments = @{
    Configuration = $Configuration
}
if (-not [string]::IsNullOrWhiteSpace($RimWorldPath)) {
    $BuildArguments.RimWorldPath = $RimWorldPath
}
if ($SkipRimWorld) {
    $BuildArguments.SkipRimWorld = $true
}

& (Join-Path $PSScriptRoot "build.ps1") @BuildArguments

if ($Install) {
    $InstallArguments = @{}
    if (-not [string]::IsNullOrWhiteSpace($RimWorldPath)) {
        $InstallArguments.RimWorldPath = $RimWorldPath
    }
    if ($Launch) {
        $InstallArguments.Launch = $true
    }
    & (Join-Path $PSScriptRoot "install.ps1") @InstallArguments
}

Write-Host ""
Write-Host "Dagmay development loop completed."
Write-Host "Build evidence: $(Join-Path $Root 'artifacts\Dagmay-build-latest.txt')"
Write-Host "Structured result: $(Join-Path $Root 'artifacts\Dagmay-build-result-latest.json')"
if ($Install) {
    Write-Host "After the human RimWorld test, run: .\tools\collect-logs.ps1"
}
