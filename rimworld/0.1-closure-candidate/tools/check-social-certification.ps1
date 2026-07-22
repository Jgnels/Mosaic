[CmdletBinding()]
param(
    [string]$PlayerLogPath = "",

    [string]$StoreId = ""
)

$ErrorActionPreference = "Stop"
$ExpectedDagmayVersion = "0.1K"
$ExpectedCertificationVersion = "2"

function Get-RimWorldDataRoot {
    param([string]$ResolvedPlayerLogPath)

    if (-not [string]::IsNullOrWhiteSpace($ResolvedPlayerLogPath)) {
        return Split-Path -Parent $ResolvedPlayerLogPath
    }

    $LocalApplicationData = [Environment]::GetFolderPath("LocalApplicationData")
    if ([string]::IsNullOrWhiteSpace($LocalApplicationData)) {
        throw "Windows LocalApplicationData could not be resolved. Pass -PlayerLogPath explicitly."
    }

    $LocalLow = Join-Path (Split-Path -Parent $LocalApplicationData) "LocalLow"
    return Join-Path $LocalLow "Ludeon Studios\RimWorld by Ludeon Studios"
}

function Get-ActiveStoreId {
    param([string]$ResolvedPlayerLogPath)

    if ([string]::IsNullOrWhiteSpace($ResolvedPlayerLogPath) -or -not (Test-Path -LiteralPath $ResolvedPlayerLogPath -PathType Leaf)) {
        return ""
    }

    $StoreIds = @(
        Get-Content -LiteralPath $ResolvedPlayerLogPath |
            ForEach-Object {
                if ($_ -match 'IdentityPath=.*(?<store>[0-9A-Fa-f]{32})\.dagmay(?:;|$)') {
                    $Matches['store']
                }
            }
    )
    if ($StoreIds.Count -eq 0) { return "" }
    return $StoreIds[-1].ToLowerInvariant()
}
function Read-KeyValueReport {
    param([Parameter(Mandatory = $true)][string]$Path)

    $Values = @{}
    foreach ($Line in (Get-Content -LiteralPath $Path)) {
        $Separator = $Line.IndexOf('=')
        if ($Separator -le 0) { continue }
        $Key = $Line.Substring(0, $Separator)
        $Value = $Line.Substring($Separator + 1)
        if ($Values.ContainsKey($Key)) {
            throw "Certification report '$Path' contains duplicate key '$Key'."
        }
        $Values[$Key] = $Value
    }
    return $Values
}

$ResolvedPlayerLogPath = ""
if ([string]::IsNullOrWhiteSpace($PlayerLogPath)) {
    $DefaultRoot = Get-RimWorldDataRoot -ResolvedPlayerLogPath ""
    $DefaultPlayerLogPath = Join-Path $DefaultRoot "Player.log"
    if (Test-Path -LiteralPath $DefaultPlayerLogPath -PathType Leaf) {
        $ResolvedPlayerLogPath = (Resolve-Path -LiteralPath $DefaultPlayerLogPath).Path
    }
}
else {
    if (-not (Test-Path -LiteralPath $PlayerLogPath -PathType Leaf)) {
        throw "RimWorld Player.log was not found at '$PlayerLogPath'."
    }
    $ResolvedPlayerLogPath = (Resolve-Path -LiteralPath $PlayerLogPath).Path
}

if ([string]::IsNullOrWhiteSpace($StoreId)) {
    $StoreId = Get-ActiveStoreId -ResolvedPlayerLogPath $ResolvedPlayerLogPath
}

$ParsedStoreId = [Guid]::Empty
if (-not [Guid]::TryParse($StoreId, [ref]$ParsedStoreId) -or $ParsedStoreId -eq [Guid]::Empty) {
    Write-Host "The active Dagmay store could not be identified safely from Player.log."
    Write-Host "Load the test save, close RimWorld, and run this command again, or pass the exact store ID with -StoreId."
    exit 2
}
$StoreId = $ParsedStoreId.ToString("N")

$RimWorldDataRoot = Get-RimWorldDataRoot -ResolvedPlayerLogPath $ResolvedPlayerLogPath
$Candidates = @(
    (Join-Path $RimWorldDataRoot "Config\Dagmay\Diagnostics"),
    (Join-Path $RimWorldDataRoot "Dagmay\Diagnostics")
)

$Files = @()
foreach ($Candidate in $Candidates) {
    $ExactPath = Join-Path $Candidate ($StoreId + "-social-certification.txt")
    if (Test-Path -LiteralPath $ExactPath -PathType Leaf) {
        $Files += @(Get-Item -LiteralPath $ExactPath)
    }
}

if ($Files.Count -eq 0) {
    Write-Host "No Dagmay social-path certification report exists for the active store '$StoreId'."
    Write-Host "Generate at least one material social event between enrolled colonists, save the game, reload it, then run this command again."
    exit 2
}

$Report = $Files |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
$Values = Read-KeyValueReport -Path $Report.FullName

Write-Host "Active-store social-path certification:"
Write-Host $Report.FullName
Write-Host ""
Get-Content -LiteralPath $Report.FullName

$RequiredExact = [ordered]@{
    DagmaySocialPathCertificationVersion = $ExpectedCertificationVersion
    StoreId = $StoreId
    DagmayVersion = $ExpectedDagmayVersion
    Status = "PASS"
    PostLoadAudit = "True"
    "Gate.EventToMemory" = "True"
    "Gate.StableCounterpartLink" = "True"
    "Gate.RelationshipSensitivePrivacy" = "True"
    "Gate.CounterpartProvenance" = "True"
    "Gate.ReflectionPathObserved" = "True"
    "Gate.PostLoadPersistenceObserved" = "True"
    "Gate.StorageHealthy" = "True"
    "Gate.Overall" = "True"
}

$Failures = New-Object System.Collections.Generic.List[string]
foreach ($Pair in $RequiredExact.GetEnumerator()) {
    if (-not $Values.ContainsKey($Pair.Key)) {
        $Failures.Add("missing $($Pair.Key)") | Out-Null
    }
    elseif (-not [string]::Equals([string]$Values[$Pair.Key], [string]$Pair.Value, [StringComparison]::OrdinalIgnoreCase)) {
        $Failures.Add("$($Pair.Key)=$($Values[$Pair.Key]) expected $($Pair.Value)") | Out-Null
    }
}

if ($Failures.Count -eq 0) {
    Write-Host ""
    Write-Host "PASS: the active RimWorld store certifies event -> memory -> exact counterpart provenance -> reflection eligibility -> healthy post-load persistence."
    exit 0
}

Write-Host ""
Write-Host "Not yet PASS for the active store. " -NoNewline
Write-Host ($Failures -join "; ")
exit 1