[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [string]$SourceRoot = "",

    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = Split-Path -Parent $PSScriptRoot
}
$SourceRoot = [IO.Path]::GetFullPath($SourceRoot)
$PackagePath = [IO.Path]::GetFullPath($PackagePath)
if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    throw "Package firewall input does not exist: $PackagePath"
}

$ManifestPath = Join-Path $SourceRoot "docs\provenance\MOSAIC_DIRECT_ADAPTATION_MANIFEST.json"
$NoticePath = Join-Path $SourceRoot "Dagmay.RimWorld\Package\THIRD_PARTY_NOTICES.txt"
if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Direct-adaptation manifest is missing."
}
if (-not (Test-Path -LiteralPath $NoticePath -PathType Leaf)) {
    throw "Third-party notice source is missing."
}

$Manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if ($Manifest.schema -ne "mosaic.direct-adaptation-manifest.v1") {
    throw "Direct-adaptation manifest has an unsupported schema."
}
if ($null -eq $Manifest.entries) {
    throw "Direct-adaptation manifest must contain an entries array."
}

$RequiredAdaptationFields = @(
    "component",
    "sourceRepository",
    "sourceCommit",
    "sourcePath",
    "sourceGitBlobSha1",
    "sourceSha256",
    "sourceLicense",
    "destinationPath",
    "modifications",
    "dependenciesReviewed",
    "noticeLocation",
    "approval"
)
foreach ($Entry in @($Manifest.entries)) {
    foreach ($Field in $RequiredAdaptationFields) {
        $Property = $Entry.PSObject.Properties[$Field]
        if ($null -eq $Property -or [string]::IsNullOrWhiteSpace([string]$Property.Value)) {
            throw "Direct-adaptation manifest entry is missing required field '$Field'."
        }
    }
    if ([string]$Entry.sourceCommit -notmatch '^[0-9a-f]{40}$' -or
        [string]$Entry.sourceGitBlobSha1 -notmatch '^[0-9a-f]{40}$' -or
        [string]$Entry.sourceSha256 -notmatch '^[0-9a-f]{64}$') {
        throw "Direct-adaptation manifest entry has an invalid commit or content hash."
    }
    if ([string]$Entry.approval -ne "approved") {
        throw "Direct-adaptation manifest contains an unapproved entry."
    }
}

$AllowedEntries = @(
    "About/About.xml",
    "Assemblies/Dagmay.Core.dll",
    "Assemblies/Dagmay.Providers.dll",
    "Assemblies/Dagmay.RimWorld.dll",
    "Assemblies/Dagmay.RimWorld.pdb",
    "README.txt",
    "THIRD_PARTY_NOTICES.txt"
)
$Allowed = New-Object 'System.Collections.Generic.HashSet[string]' (
    [StringComparer]::OrdinalIgnoreCase)
foreach ($AllowedEntry in $AllowedEntries) {
    $Allowed.Add($AllowedEntry) | Out-Null
}
$Seen = New-Object 'System.Collections.Generic.HashSet[string]' (
    [StringComparer]::OrdinalIgnoreCase)
$Inventory = New-Object System.Collections.Generic.List[object]
$MachinePathLeaks = New-Object System.Collections.Generic.List[string]
$MissingMappedDebugPaths = New-Object System.Collections.Generic.List[string]
$ProhibitedPathPatterns = @(
    '(^|/)research(/|$)',
    '(^|/)local-recovery(/|$)',
    '(^|/)\.git(/|$)',
    '(^|/)Player\.log$',
    '\.(rws|dagmay|journal|reflection|key|pfx|pem|env)$'
)
$MachineMarkers = @(
    $SourceRoot,
    $SourceRoot.Replace('\', '/'),
    'C:\Users\',
    'C:/Users/',
    '/Users/',
    '/home/',
    '\AppData\',
    '/AppData/',
    '\agent\_work\',
    '/agent/_work/'
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$NoticeHash = (Get-FileHash -LiteralPath $NoticePath -Algorithm SHA256).Hash.ToLowerInvariant()

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
try {
    foreach ($ZipEntry in $Archive.Entries) {
        if ([string]::IsNullOrWhiteSpace($ZipEntry.Name)) { continue }
        $Name = $ZipEntry.FullName.Replace('\', '/')
        if ($Name.StartsWith("/") -or $Name.Contains(":") -or
            @($Name.Split('/')) -contains "..") {
            throw "Package contains unsafe path '$Name'."
        }
        if (-not $Seen.Add($Name)) {
            throw "Package contains a duplicate entry '$Name'."
        }
        foreach ($Pattern in $ProhibitedPathPatterns) {
            if ($Name -match $Pattern) {
                throw "Package contains prohibited private/runtime artifact '$Name'."
            }
        }
        if (-not $Allowed.Contains($Name)) {
            throw "Package contains undeclared entry '$Name'."
        }

        $Input = $ZipEntry.Open()
        try {
            $Memory = New-Object IO.MemoryStream
            try {
                $Input.CopyTo($Memory)
                $Bytes = $Memory.ToArray()
            }
            finally {
                $Memory.Dispose()
            }
        }
        finally {
            $Input.Dispose()
        }

        $Hasher = [Security.Cryptography.SHA256]::Create()
        try {
            $Hash = ([BitConverter]::ToString($Hasher.ComputeHash($Bytes))).Replace(
                "-",
                "").ToLowerInvariant()
        }
        finally {
            $Hasher.Dispose()
        }

        if ($Name -eq "THIRD_PARTY_NOTICES.txt" -and $Hash -ne $NoticeHash) {
            throw "Packaged third-party notice does not match the reviewed source notice."
        }

        if ([IO.Path]::GetExtension($Name).ToLowerInvariant() -in @(".dll", ".pdb")) {
            $Utf8Text = [Text.Encoding]::UTF8.GetString($Bytes)
            $Utf16Text = [Text.Encoding]::Unicode.GetString($Bytes)
            foreach ($Marker in $MachineMarkers) {
                if ($Utf8Text.IndexOf($Marker, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
                    $Utf16Text.IndexOf($Marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $MachinePathLeaks.Add($Name) | Out-Null
                    break
                }
            }
            if ($Name.EndsWith(".dll", [StringComparison]::OrdinalIgnoreCase) -and
                $Utf8Text.IndexOf("/_/", [StringComparison]::Ordinal) -lt 0 -and
                $Utf16Text.IndexOf("/_/", [StringComparison]::Ordinal) -lt 0) {
                $MissingMappedDebugPaths.Add($Name) | Out-Null
            }
        }

        $Inventory.Add([ordered]@{
            path = $Name
            length = $ZipEntry.Length
            sha256 = $Hash
        }) | Out-Null
    }
}
finally {
    $Archive.Dispose()
}

$Missing = @($AllowedEntries | Where-Object { -not $Seen.Contains($_) })
if ($Missing.Count -gt 0) {
    throw "Package is missing declared entries: $($Missing -join ', ')."
}
if ($MachinePathLeaks.Count -gt 0) {
    throw "Package contains machine-specific build paths in: $($MachinePathLeaks -join ', ')."
}
if ($MissingMappedDebugPaths.Count -gt 0) {
    throw "Package assemblies are missing deterministic mapped debug paths: $($MissingMappedDebugPaths -join ', ')."
}

$Result = [ordered]@{
    schema = "mosaic.package-firewall.v1"
    status = "PASS"
    packageSha256 = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    manifestSha256 = (Get-FileHash -LiteralPath $ManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    noticeSha256 = $NoticeHash
    declaredEntryCount = $AllowedEntries.Count
    adaptationEntryCount = @($Manifest.entries).Count
    entries = [object[]]$Inventory
}
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $Result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
}

Write-Host "Mosaic package firewall: PASS ($($Inventory.Count) declared entries; $(@($Manifest.entries).Count) direct adaptations)."
$Result
