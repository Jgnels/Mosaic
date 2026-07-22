[CmdletBinding()]
param(
    [string]$PackagePath = "",

    [string]$RimWorldPath = "",

    [switch]$Launch
)

$ErrorActionPreference = "Stop"
$DagmayVersion = "0.2-prealpha"
$Root = Split-Path -Parent $PSScriptRoot
$Artifacts = Join-Path $Root "artifacts"
$RunStamp = Get-Date -Format "yyyyMMdd-HHmmss"

function Assert-DagmayPackageDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    $RequiredFiles = @(
        "About\About.xml",
        "Assemblies\Dagmay.Core.dll",
        "Assemblies\Dagmay.Providers.dll",
        "Assemblies\Dagmay.RimWorld.dll"
    )
    foreach ($RelativePath in $RequiredFiles) {
        $Candidate = Join-Path $Path $RelativePath
        if (-not (Test-Path -LiteralPath $Candidate -PathType Leaf)) {
            throw "The Dagmay package is incomplete. Missing '$RelativePath'."
        }
    }

    $AboutPath = Join-Path $Path "About\About.xml"
    [xml]$About = Get-Content -LiteralPath $AboutPath -Raw
    if ($About.ModMetaData.packageId -ne "dagmay.research.observer") {
        throw "The package About.xml does not contain Dagmay's expected package ID. Installation was refused."
    }
}

if ([string]::IsNullOrWhiteSpace($RimWorldPath)) {
    $RimWorldPath = Join-Path ${env:ProgramFiles(x86)} "Steam\steamapps\common\RimWorld"
}
$RimWorldPath = [IO.Path]::GetFullPath($RimWorldPath)

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $Artifacts "Dagmay-RimWorld-$DagmayVersion.zip"
}
if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    throw "Dagmay package not found at '$PackagePath'. Run tools\build.ps1 first."
}
$PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path

$ModsRoot = Join-Path $RimWorldPath "Mods"
if (-not (Test-Path -LiteralPath $ModsRoot -PathType Container)) {
    throw "RimWorld's Mods folder was not found at '$ModsRoot'. Pass the correct -RimWorldPath."
}
$ModsRoot = (Resolve-Path -LiteralPath $ModsRoot).Path
$Destination = Join-Path $ModsRoot "Dagmay"

if ((Split-Path -Leaf $Destination) -ne "Dagmay") {
    throw "Install safety check failed: the destination leaf is not exactly 'Dagmay'."
}
$ExpectedParent = [IO.Path]::GetFullPath($ModsRoot).TrimEnd('\')
$ActualParent = [IO.Path]::GetFullPath((Split-Path -Parent $Destination)).TrimEnd('\')
if (-not [string]::Equals($ExpectedParent, $ActualParent, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Install safety check failed: the Dagmay destination is not directly beneath RimWorld's Mods folder."
}

$RunningGame = Get-Process -Name "RimWorldWin64" -ErrorAction SilentlyContinue
if ($null -ne $RunningGame) {
    throw "RimWorld is running. Close it completely before installing Dagmay so no DLL is replaced while loaded."
}

if (Test-Path -LiteralPath $Destination) {
    $DestinationItem = Get-Item -LiteralPath $Destination -Force
    if (-not $DestinationItem.PSIsContainer) {
        throw "Install safety check failed: '$Destination' exists but is not a directory. Move that exact file manually before continuing."
    }
    if (($DestinationItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Install safety check failed: the existing Dagmay mod folder is a link or reparse point. Move it manually before continuing."
    }
}

$UniqueSuffix = [Guid]::NewGuid().ToString("N")
$StagingPath = Join-Path $ModsRoot "Dagmay.__installing_$UniqueSuffix"
$PreviousPath = Join-Path $ModsRoot "Dagmay.__previous_$UniqueSuffix"
$Backups = Join-Path $Artifacts "install-backups"
$BackupPath = Join-Path $Backups "Dagmay-installed-before-$RunStamp.zip"
$PreviousMoved = $false
$NewInstalled = $false

New-Item -ItemType Directory -Path $Backups -Force | Out-Null

try {
    New-Item -ItemType Directory -Path $StagingPath -Force | Out-Null
    Expand-Archive -LiteralPath $PackagePath -DestinationPath $StagingPath -Force
    Assert-DagmayPackageDirectory -Path $StagingPath

    if (Test-Path -LiteralPath $Destination) {
        Compress-Archive -Path $Destination -DestinationPath $BackupPath -CompressionLevel Optimal
        Move-Item -LiteralPath $Destination -Destination $PreviousPath
        $PreviousMoved = $true
        Write-Host "Previous installed Dagmay package backed up to: $BackupPath"
    }

    Move-Item -LiteralPath $StagingPath -Destination $Destination
    $NewInstalled = $true
    Assert-DagmayPackageDirectory -Path $Destination

    if ($PreviousMoved -and (Test-Path -LiteralPath $PreviousPath)) {
        try {
            Remove-Item -LiteralPath $PreviousPath -Recurse -Force
        }
        catch {
            Write-Warning "The verified new package is installed, but Windows could not remove the temporary prior-package folder '$PreviousPath'. The backup archive is also retained. Close programs using that folder and remove only that exact temporary folder later."
        }
    }

    Write-Host "Dagmay $DagmayVersion installed successfully at: $Destination"
    Write-Host "RimWorld saves, Dagmay identity sidecars, and API-key configuration were not touched."
}
catch {
    $InstallError = $_
    $RollbackFailures = New-Object System.Collections.Generic.List[string]

    if ($NewInstalled -and (Test-Path -LiteralPath $Destination)) {
        try {
            Remove-Item -LiteralPath $Destination -Recurse -Force
        }
        catch {
            $RollbackFailures.Add("could not remove the failed new package: $($_.Exception.Message)") | Out-Null
        }
    }
    if ($PreviousMoved -and (Test-Path -LiteralPath $PreviousPath) -and -not (Test-Path -LiteralPath $Destination)) {
        try {
            Move-Item -LiteralPath $PreviousPath -Destination $Destination
            Write-Warning "The new install failed; the prior Dagmay mod folder was restored."
        }
        catch {
            $RollbackFailures.Add("could not restore the prior package: $($_.Exception.Message)") | Out-Null
        }
    }
    if (Test-Path -LiteralPath $StagingPath) {
        try {
            Remove-Item -LiteralPath $StagingPath -Recurse -Force
        }
        catch {
            $RollbackFailures.Add("could not remove the staging folder: $($_.Exception.Message)") | Out-Null
        }
    }

    if ($RollbackFailures.Count -gt 0) {
        $RollbackSummary = $RollbackFailures -join "; "
        throw "Dagmay installation failed: $($InstallError.Exception.Message). Automatic rollback was incomplete: $RollbackSummary. Do not launch RimWorld. The prior-package backup, when created, is '$BackupPath'."
    }
    if ($InstallError.Exception -is [System.UnauthorizedAccessException]) {
        throw "Windows denied access to RimWorld's Mods folder. Reopen the ChatGPT/Codex app or PowerShell as administrator, then run the same command again. No successful install was recorded."
    }
    throw $InstallError
}

if ($Launch) {
    Write-Host "Launching RimWorld through Steam."
    Start-Process "steam://rungameid/294100"
}
