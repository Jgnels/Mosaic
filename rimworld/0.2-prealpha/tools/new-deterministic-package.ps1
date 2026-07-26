[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,

    [Parameter(Mandatory = $true)]
    [string]$DestinationPath
)

$ErrorActionPreference = "Stop"
$SourceDirectory = [IO.Path]::GetFullPath($SourceDirectory)
$DestinationPath = [IO.Path]::GetFullPath($DestinationPath)

if (-not (Test-Path -LiteralPath $SourceDirectory -PathType Container)) {
    throw "Package source directory does not exist: $SourceDirectory"
}
if (Test-Path -LiteralPath $DestinationPath) {
    throw "Deterministic package destination already exists: $DestinationPath"
}

$DestinationDirectory = Split-Path -Parent $DestinationPath
New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null

$FilesByEntry = New-Object 'System.Collections.Generic.SortedDictionary[string,string]' (
    [StringComparer]::Ordinal)
foreach ($File in Get-ChildItem -LiteralPath $SourceDirectory -File -Recurse) {
    $EntryName = $File.FullName.Substring($SourceDirectory.Length + 1).Replace('\', '/')
    if ($FilesByEntry.ContainsKey($EntryName)) {
        throw "Package source contains a duplicate normalized path: $EntryName"
    }
    $FilesByEntry.Add($EntryName, $File.FullName)
}
if ($FilesByEntry.Count -eq 0) {
    throw "Package source directory is empty."
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$FixedTimestamp = [DateTimeOffset]::new(
    2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
$Output = [IO.File]::Open(
    $DestinationPath,
    [IO.FileMode]::CreateNew,
    [IO.FileAccess]::ReadWrite,
    [IO.FileShare]::None)
try {
    $Archive = [IO.Compression.ZipArchive]::new(
        $Output,
        [IO.Compression.ZipArchiveMode]::Create,
        $false)
    try {
        foreach ($Pair in $FilesByEntry.GetEnumerator()) {
            $Entry = $Archive.CreateEntry(
                $Pair.Key,
                [IO.Compression.CompressionLevel]::Optimal)
            $Entry.LastWriteTime = $FixedTimestamp
            $Entry.ExternalAttributes = 0
            $Input = [IO.File]::OpenRead($Pair.Value)
            try {
                $EntryStream = $Entry.Open()
                try {
                    $Input.CopyTo($EntryStream)
                }
                finally {
                    $EntryStream.Dispose()
                }
            }
            finally {
                $Input.Dispose()
            }
        }
    }
    finally {
        $Archive.Dispose()
    }
}
finally {
    $Output.Dispose()
}

Write-Host "Deterministic package created with $($FilesByEntry.Count) entries."
