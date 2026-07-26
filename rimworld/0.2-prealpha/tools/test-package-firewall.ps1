[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Verifier = Join-Path $PSScriptRoot "package-firewall.ps1"
$FixtureSpec = Get-Content -LiteralPath (
    Join-Path $PSScriptRoot "package-firewall-fixtures.json") -Raw |
    ConvertFrom-Json
if ($FixtureSpec.schema -ne "mosaic.package-firewall-fixtures.v1") {
    throw "Package-firewall fixture schema is unsupported."
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$TempRoot = Join-Path (
    [IO.Path]::GetTempPath()) (
    "mosaic-package-firewall-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $TempRoot -Force | Out-Null

function New-FixturePackage {
    param(
        [string]$Path,
        [string]$ExtraEntry = "",
        [string]$ReplaceEntry = "",
        [string]$ReplacementContent = "",
        [string]$OmitEntry = ""
    )

    $Entries = [ordered]@{
        "About/About.xml" = "<ModMetaData><name>Mosaic</name></ModMetaData>"
        "Assemblies/Dagmay.Core.dll" = "fixture-core /_/"
        "Assemblies/Dagmay.Providers.dll" = "fixture-providers /_/"
        "Assemblies/Dagmay.RimWorld.dll" = "fixture-adapter /_/"
        "Assemblies/Dagmay.RimWorld.pdb" = "fixture-symbols"
        "README.txt" = "fixture-readme"
    }
    if (-not [string]::IsNullOrWhiteSpace($ReplaceEntry)) {
        $Entries[$ReplaceEntry] = $ReplacementContent
    }
    if (-not [string]::IsNullOrWhiteSpace($OmitEntry)) {
        $Entries.Remove($OmitEntry)
    }
    if (-not [string]::IsNullOrWhiteSpace($ExtraEntry)) {
        $Entries[$ExtraEntry] = "adversarial fixture"
    }

    $File = [IO.File]::Open($Path, [IO.FileMode]::CreateNew)
    try {
        $Archive = [IO.Compression.ZipArchive]::new(
            $File,
            [IO.Compression.ZipArchiveMode]::Create,
            $false)
        try {
            foreach ($Pair in $Entries.GetEnumerator()) {
                $ZipEntry = $Archive.CreateEntry($Pair.Key)
                $Writer = [IO.StreamWriter]::new(
                    $ZipEntry.Open(),
                    [Text.UTF8Encoding]::new($false))
                try {
                    $Writer.Write([string]$Pair.Value)
                }
                finally {
                    $Writer.Dispose()
                }
            }
        }
        finally {
            $Archive.Dispose()
        }
    }
    finally {
        $File.Dispose()
    }
}

try {
    $ValidPath = Join-Path $TempRoot "valid.zip"
    New-FixturePackage -Path $ValidPath
    $null = & $Verifier -PackagePath $ValidPath -SourceRoot $Root

    $Passed = 0
    foreach ($Case in @($FixtureSpec.negativeCases)) {
        $CasePath = Join-Path $TempRoot ("$($Case.name).zip")
        $Content = [string]$Case.content
        if ([string]$Case.contentKind -eq "sourceRoot") {
            $Content = $Root + "\private\source.cs"
        }
        New-FixturePackage `
            -Path $CasePath `
            -ExtraEntry ([string]$Case.entry) `
            -ReplaceEntry ([string]$Case.replace) `
            -ReplacementContent $Content `
            -OmitEntry ([string]$Case.omit)
        $Rejected = $false
        try {
            $null = & $Verifier -PackagePath $CasePath -SourceRoot $Root
        }
        catch {
            if ($_.Exception.Message.IndexOf(
                [string]$Case.expected,
                [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $Rejected = $true
            }
            else {
                throw "Fixture '$($Case.name)' failed for an unexpected reason: $($_.Exception.Message)"
            }
        }
        if (-not $Rejected) {
            throw "Package firewall accepted negative fixture '$($Case.name)'."
        }
        $Passed++
    }

    Write-Host "Mosaic package-firewall fixtures: PASS (1 valid; $Passed rejected)."
}
finally {
    if (Test-Path -LiteralPath $TempRoot) {
        Remove-Item -LiteralPath $TempRoot -Recurse -Force
    }
}
