[CmdletBinding()]
param(
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$DagmayVersion = "0.1K"
$Root = Split-Path -Parent $PSScriptRoot
$Artifacts = Join-Path $Root "artifacts"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $Artifacts "Dagmay-v$DagmayVersion-source.zip"
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$TemporaryPath = $OutputPath + ".tmp"

New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null
if (Test-Path -LiteralPath $TemporaryPath) { Remove-Item -LiteralPath $TemporaryPath -Force }

$ExcludedDirectories = @("bin", "obj", "artifacts", ".git", ".vs", "TestResults")
$ExcludedExtensions = @(".user", ".suo", ".userprefs")

$SourceFiles = @(
    Get-ChildItem -LiteralPath $Root -File -Recurse -Force | Where-Object {
        $RelativePath = $_.FullName.Substring($Root.Length + 1)
        $NormalizedRelativePath = $RelativePath.Replace('\', '/')
        $Segments = @($RelativePath -split '[\\/]')
        $DirectoryExcluded = $false
        foreach ($Segment in $Segments) {
            if ($ExcludedDirectories -contains $Segment) {
                $DirectoryExcluded = $true
                break
            }
        }

        -not $DirectoryExcluded `
            -and -not ($ExcludedExtensions -contains $_.Extension.ToLowerInvariant()) `
            -and -not ($NormalizedRelativePath -like "Dagmay.RimWorld/Package/Assemblies/*.dll") `
            -and -not ($NormalizedRelativePath -like "Dagmay.RimWorld/Package/Assemblies/*.pdb")
    }
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Archive = [IO.Compression.ZipFile]::Open($TemporaryPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($File in $SourceFiles) {
        $RelativePath = $File.FullName.Substring($Root.Length + 1).Replace('\', '/')
        $EntryName = "Dagmay/" + $RelativePath
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $Archive,
            $File.FullName,
            $EntryName,
            [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $Archive.Dispose()
}

if (Test-Path -LiteralPath $OutputPath) { Remove-Item -LiteralPath $OutputPath -Force }
Move-Item -LiteralPath $TemporaryPath -Destination $OutputPath
$Hash = (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
$HashPath = $OutputPath + ".sha256.txt"
("{0}  {1}" -f $Hash, (Split-Path -Leaf $OutputPath)) | Set-Content -LiteralPath $HashPath -Encoding UTF8

Write-Host "Source package: $OutputPath"
Write-Host "Source SHA-256: $Hash"
