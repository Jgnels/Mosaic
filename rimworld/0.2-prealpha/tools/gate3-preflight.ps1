[CmdletBinding()]
param(
    [string]$TestRunId = "",

    [string]$PackagePath = "",

    [string]$RimWorldPath = "",

    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Artifacts = Join-Path $Root "artifacts"
$ExpectedVersion = "0.2-prealpha"

if ([string]::IsNullOrWhiteSpace($TestRunId)) {
    $TestRunId = "gate3-" + (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
}
if ($TestRunId -notmatch '^[A-Za-z0-9._-]{1,64}$') {
    throw "TestRunId may contain only letters, numbers, period, underscore, and hyphen (maximum 64 characters)."
}
if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $Artifacts "Dagmay-RimWorld-$ExpectedVersion.zip"
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $Artifacts "Dagmay-gate3-preflight-$TestRunId.json"
}
if ([string]::IsNullOrWhiteSpace($RimWorldPath)) {
    $RimWorldPath = Join-Path ${env:ProgramFiles(x86)} "Steam\steamapps\common\RimWorld"
}

if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    throw "The non-installing package was not found. Run tools\build.ps1 without -SkipRimWorld first."
}
$PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$PackageHash = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$FirewallResult = & (Join-Path $PSScriptRoot "package-firewall.ps1") `
    -PackagePath $PackagePath `
    -SourceRoot $Root

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$RequiredEntries = @(
    "About/About.xml",
    "Assemblies/Dagmay.Core.dll",
    "Assemblies/Dagmay.Providers.dll",
    "Assemblies/Dagmay.RimWorld.dll",
    "README.txt"
)
$ProhibitedExtensions = @(".rws", ".dagmay", ".journal", ".reflection", ".key", ".pfx", ".pem")
$Inventory = New-Object System.Collections.Generic.List[object]
$EntryNames = New-Object System.Collections.Generic.List[string]
$Archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
try {
    foreach ($Entry in $Archive.Entries) {
        if ([string]::IsNullOrWhiteSpace($Entry.Name)) { continue }
        $NormalizedName = $Entry.FullName.Replace('\', '/')
        $EntryNames.Add($NormalizedName) | Out-Null
        if ($ProhibitedExtensions -contains [IO.Path]::GetExtension($NormalizedName).ToLowerInvariant()) {
            throw "Package contains prohibited private/runtime artifact '$NormalizedName'."
        }

        $Stream = $Entry.Open()
        try {
            $Algorithm = [Security.Cryptography.SHA256]::Create()
            try {
                $EntryHash = ([BitConverter]::ToString($Algorithm.ComputeHash($Stream))).Replace("-", "").ToLowerInvariant()
            }
            finally {
                $Algorithm.Dispose()
            }
        }
        finally {
            $Stream.Dispose()
        }

        $Inventory.Add([ordered]@{
            path = $NormalizedName
            length = $Entry.Length
            sha256 = $EntryHash
        }) | Out-Null
    }
}
finally {
    $Archive.Dispose()
}

$MissingEntries = @($RequiredEntries | Where-Object { -not $EntryNames.Contains($_) })
if ($MissingEntries.Count -gt 0) {
    throw "Package is missing required entries: $($MissingEntries -join ', ')."
}

$ManagedPath = Join-Path $RimWorldPath "RimWorldWin64_Data\Managed"
$RequiredReferences = @(
    "Assembly-CSharp.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.TextRenderingModule.dll"
)
$MissingReferences = @(
    $RequiredReferences | Where-Object { -not (Test-Path -LiteralPath (Join-Path $ManagedPath $_) -PathType Leaf) }
)

$AssemblyInventory = New-Object System.Collections.Generic.List[object]
foreach ($Name in @("Dagmay.Core.dll", "Dagmay.Providers.dll", "Dagmay.RimWorld.dll")) {
    $Path = Join-Path $Root "Dagmay.RimWorld\Package\Assemblies\$Name"
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Built package assembly '$Name' is missing."
    }
    $Version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path -LiteralPath $Path).Path)
    $AssemblyInventory.Add([ordered]@{
        name = $Name
        fileVersion = $Version.FileVersion
        sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    }) | Out-Null
}

$Report = [ordered]@{
    schema = "mosaic.gate3-preflight.v1"
    testRunId = $TestRunId
    createdUtc = [DateTimeOffset]::UtcNow.ToString("o")
    mosaicVersion = $ExpectedVersion
    status = if ($MissingReferences.Count -eq 0) { "PASS" } else { "LIMITED" }
    package = [ordered]@{
        fileName = Split-Path -Leaf $PackagePath
        sha256 = $PackageHash
        entries = [object[]]$Inventory
        prohibitedRuntimeArtifactsPresent = $false
        machineSpecificBuildPathsPresent = $false
        firewallSchema = $FirewallResult.schema
    }
    assemblies = [object[]]$AssemblyInventory
    rimWorldReferences = [ordered]@{
        expectedVersion = "1.6"
        present = $MissingReferences.Count -eq 0
        missingFileNames = @($MissingReferences)
    }
    safety = [ordered]@{
        installedMod = $false
        touchedSave = $false
        inspectedCredentials = $false
        invokedProvider = $false
    }
    expectedSuccessMarkers = @(
        "[Dagmay] Version 0.2-prealpha loaded.",
        "IndividualId=",
        "LineageId="
    )
    expectedFailureMarkers = @(
        "identity storage entered read-only safety mode",
        "does not match the RimWorld save checkpoint",
        "external identity archive is missing"
    )
}

$OutputDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$Report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding UTF8

Write-Host "Mosaic Gate 3 preflight: $($Report.status)"
Write-Host "Test run ID: $TestRunId"
Write-Host "Package SHA-256: $PackageHash"
Write-Host "Package entries: $($Inventory.Count)"
Write-Host "RimWorld 1.6 references present: $($MissingReferences.Count -eq 0)"
Write-Host "No mod was installed, no save was touched, and no credential was inspected."
Write-Host "Report: $OutputPath"
if ($MissingReferences.Count -gt 0) { exit 2 }
