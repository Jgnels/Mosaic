[CmdletBinding()]
param(
    [string]$PlayerLogPath = "",

    [string]$TestRunId = "",

    [ValidateRange(0, 20)]
    [int]$ContextLines = 4,

    [switch]$CurrentOnly,

    [switch]$IncludeFullLog
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Artifacts = Join-Path $Root "artifacts"
$Diagnostics = Join-Path $Artifacts "diagnostics"
$RunStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$ReportPath = Join-Path $Diagnostics "Dagmay-diagnostics-$RunStamp.txt"
$LatestReportPath = Join-Path $Artifacts "Dagmay-diagnostics-latest.txt"

function Protect-PrivateText {
    param([AllowEmptyString()][string]$Value)

    $Protected = $Value
    $Protected = $Protected -replace '\bAIza[A-Za-z0-9_-]{20,}\b', '[REDACTED_GOOGLE_API_KEY]'
    $Protected = $Protected -replace '\bsk-[A-Za-z0-9_-]{20,}\b', '[REDACTED_API_KEY]'
    $Protected = $Protected -replace '(?i)(DAGMAY_GOOGLE_API_KEY\s*[=:]\s*)\S+', '$1[REDACTED]'

    $UserProfile = [Environment]::GetFolderPath("UserProfile")
    if (-not [string]::IsNullOrWhiteSpace($UserProfile)) {
        $Protected = $Protected.Replace($UserProfile, "%USERPROFILE%")
    }
    if (-not [string]::IsNullOrWhiteSpace($env:COMPUTERNAME)) {
        $Protected = $Protected.Replace($env:COMPUTERNAME, "%COMPUTERNAME%")
    }
    return $Protected
}

if ([string]::IsNullOrWhiteSpace($TestRunId)) {
    $TestRunId = "gate3-" + (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
}
if ($TestRunId -notmatch '^[A-Za-z0-9._-]{1,64}$') {
    throw "TestRunId may contain only letters, numbers, period, underscore, and hyphen (maximum 64 characters)."
}

function Get-ContextIndices {
    param(
        [string[]]$Lines,
        [string]$Pattern,
        [int]$Radius
    )

    $Indices = New-Object System.Collections.Generic.HashSet[int]
    for ($Index = 0; $Index -lt $Lines.Count; $Index++) {
        if ($Lines[$Index] -match $Pattern) {
            $Start = [Math]::Max(0, $Index - $Radius)
            $End = [Math]::Min($Lines.Count - 1, $Index + $Radius)
            for ($ContextIndex = $Start; $ContextIndex -le $End; $ContextIndex++) {
                $Indices.Add($ContextIndex) | Out-Null
            }
        }
    }
    return @($Indices | Sort-Object)
}

function Get-ActiveStoreId {
    param([Parameter(Mandatory = $true)][string]$ResolvedPlayerLogPath)

    $StoreIds = @(
        Get-Content -LiteralPath $ResolvedPlayerLogPath |
            ForEach-Object {
                if ($_ -match 'IdentityPath=.*[\\/](?<store>[0-9A-Fa-f]{32})\.dagmay(?:;|$)') {
                    $Matches['store']
                }
            }
    )
    if ($StoreIds.Count -eq 0) { return "" }
    return $StoreIds[-1].ToLowerInvariant()
}

if ([string]::IsNullOrWhiteSpace($PlayerLogPath)) {
    $LocalApplicationData = [Environment]::GetFolderPath("LocalApplicationData")
    if ([string]::IsNullOrWhiteSpace($LocalApplicationData)) {
        throw "Windows LocalApplicationData could not be resolved. Pass -PlayerLogPath explicitly."
    }
    $LocalLow = Join-Path (Split-Path -Parent $LocalApplicationData) "LocalLow"
    $LogDirectory = Join-Path $LocalLow "Ludeon Studios\RimWorld by Ludeon Studios"
    $PlayerLogPath = Join-Path $LogDirectory "Player.log"
}
if (-not (Test-Path -LiteralPath $PlayerLogPath -PathType Leaf)) {
    throw "RimWorld Player.log was not found at '$PlayerLogPath'. Start RimWorld once or pass -PlayerLogPath explicitly."
}
$PlayerLogPath = (Resolve-Path -LiteralPath $PlayerLogPath).Path

$SourceLogs = New-Object System.Collections.Generic.List[string]
$SourceLogs.Add($PlayerLogPath) | Out-Null
if (-not $CurrentOnly) {
    $PreviousLogPath = Join-Path (Split-Path -Parent $PlayerLogPath) "Player-prev.log"
    if (Test-Path -LiteralPath $PreviousLogPath -PathType Leaf) {
        $SourceLogs.Add((Resolve-Path -LiteralPath $PreviousLogPath).Path) | Out-Null
    }
}

New-Item -ItemType Directory -Path $Diagnostics -Force | Out-Null
$Report = New-Object System.Collections.Generic.List[string]
$SourceVersion = "0.2-prealpha"
$Report.Add("Dagmay $SourceVersion diagnostic evidence") | Out-Null
$Report.Add("Mosaic Gate 3 test run ID: $TestRunId") | Out-Null
$Report.Add("Diagnostic source version: $SourceVersion") | Out-Null
$Report.Add("Created UTC: $([DateTimeOffset]::UtcNow.ToString('o'))") | Out-Null
$Report.Add("This report redacts Google-style API keys, the Windows user profile path, and the computer name.") | Out-Null
$Report.Add("") | Out-Null

$BuildResultPath = Join-Path $Artifacts "Dagmay-build-result-latest.json"
if (Test-Path -LiteralPath $BuildResultPath -PathType Leaf) {
    $Report.Add("=== Latest structured build result ===") | Out-Null
    foreach ($Line in (Get-Content -LiteralPath $BuildResultPath)) {
        $Report.Add((Protect-PrivateText $Line)) | Out-Null
    }
    $Report.Add("") | Out-Null
    try {
        $BuildResult = Get-Content -LiteralPath $BuildResultPath -Raw | ConvertFrom-Json
        if ($BuildResult.dagmayVersion -ne $SourceVersion) {
            $Report.Add("WARNING: Structured build result version '$($BuildResult.dagmayVersion)' does not match diagnostic source version '$SourceVersion'. Run .\\tools\\dev-loop.ps1 from this source folder before relying on the build evidence.") | Out-Null
            $Report.Add("") | Out-Null
        }
    }
    catch {
        $Report.Add("WARNING: Structured build result could not be parsed for version comparison.") | Out-Null
        $Report.Add("") | Out-Null
    }
}


$ActiveStoreId = Get-ActiveStoreId -ResolvedPlayerLogPath $PlayerLogPath
$RimWorldDataRoot = Split-Path -Parent $PlayerLogPath
$DagmayConfigCandidates = @(
    (Join-Path $RimWorldDataRoot "Config\Dagmay\Diagnostics"),
    (Join-Path $RimWorldDataRoot "Dagmay\Diagnostics")
)

$SocialCertificationFiles = @()
if (-not [string]::IsNullOrWhiteSpace($ActiveStoreId)) {
    foreach ($Candidate in $DagmayConfigCandidates) {
        $ExactPath = Join-Path $Candidate ($ActiveStoreId + "-social-certification.txt")
        if (Test-Path -LiteralPath $ExactPath -PathType Leaf) {
            $SocialCertificationFiles += @(Get-Item -LiteralPath $ExactPath)
        }
    }
}

if ($SocialCertificationFiles.Count -gt 0) {
    $LatestSocialCertification = $SocialCertificationFiles |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    $Report.Add("=== Active-store social-path certification ===") | Out-Null
    $Report.Add("Active StoreId: $ActiveStoreId") | Out-Null
    $Report.Add("Source: $(Protect-PrivateText $LatestSocialCertification.FullName)") | Out-Null
    foreach ($Line in (Get-Content -LiteralPath $LatestSocialCertification.FullName)) {
        $Report.Add((Protect-PrivateText $Line)) | Out-Null
    }
    $Report.Add("") | Out-Null
}
elseif ([string]::IsNullOrWhiteSpace($ActiveStoreId)) {
    $Report.Add("=== Active-store social-path certification ===") | Out-Null
    $Report.Add("Player.log did not identify an active Dagmay StoreId. No unrelated historical certification sidecar was embedded.") | Out-Null
    $Report.Add("") | Out-Null
}
else {
    $Report.Add("=== Active-store social-path certification ===") | Out-Null
    $Report.Add("No social-path certification sidecar was found for active StoreId $ActiveStoreId. Generate a material social event, save, reload, then collect logs again.") | Out-Null
    $Report.Add("") | Out-Null
}
$NoteworthyPattern = '(?i)\[Dagmay\]|Exception|\berror\b|Could not resolve|Could not load|quarantin|mismatch|read-only|ThreadAbort|SOCIAL PATH CERTIFICATION'

foreach ($SourceLog in $SourceLogs) {
    $Lines = @(Get-Content -LiteralPath $SourceLog)
    $Report.Add("=== Source: $(Protect-PrivateText $SourceLog) ===") | Out-Null
    $Report.Add("Lines: $($Lines.Count)") | Out-Null
    $Report.Add("") | Out-Null

    $Report.Add("--- Dagmay lines ---") | Out-Null
    $DagmayFound = $false
    for ($Index = 0; $Index -lt $Lines.Count; $Index++) {
        if ($Lines[$Index] -match '\[Dagmay\]') {
            $DagmayFound = $true
            $Report.Add(("{0:D6}: {1}" -f ($Index + 1), (Protect-PrivateText $Lines[$Index]))) | Out-Null
        }
    }
    if (-not $DagmayFound) {
        $Report.Add("No [Dagmay] lines were found in this log.") | Out-Null
    }
    $Report.Add("") | Out-Null

    $Report.Add("--- Noteworthy context ---") | Out-Null
    $ContextIndices = @(Get-ContextIndices -Lines $Lines -Pattern $NoteworthyPattern -Radius $ContextLines)
    if ($ContextIndices.Count -eq 0) {
        $Report.Add("No noteworthy error or exception context was found.") | Out-Null
    }
    else {
        $PreviousIndex = -2
        foreach ($Index in $ContextIndices) {
            if ($Index -gt ($PreviousIndex + 1)) { $Report.Add("...") | Out-Null }
            $Report.Add(("{0:D6}: {1}" -f ($Index + 1), (Protect-PrivateText $Lines[$Index]))) | Out-Null
            $PreviousIndex = $Index
        }
    }

    if ($IncludeFullLog) {
        $Report.Add("") | Out-Null
        $Report.Add("--- Full redacted log ---") | Out-Null
        for ($Index = 0; $Index -lt $Lines.Count; $Index++) {
            $Report.Add(("{0:D6}: {1}" -f ($Index + 1), (Protect-PrivateText $Lines[$Index]))) | Out-Null
        }
    }
    $Report.Add("") | Out-Null
}

$Report | Set-Content -LiteralPath $ReportPath -Encoding UTF8
Copy-Item -LiteralPath $ReportPath -Destination $LatestReportPath -Force
$LatestHash = (Get-FileHash -LiteralPath $LatestReportPath -Algorithm SHA256).Hash.ToLowerInvariant()
$EvidenceManifestPath = Join-Path $Artifacts "Dagmay-gate3-evidence-$TestRunId.json"
$EvidenceManifest = [ordered]@{
    schema = "mosaic.gate3-runtime-evidence.v1"
    testRunId = $TestRunId
    createdUtc = [DateTimeOffset]::UtcNow.ToString("o")
    mosaicVersion = $SourceVersion
    diagnosticReportFile = Split-Path -Leaf $LatestReportPath
    diagnosticReportSha256 = $LatestHash
    includedLogFileNames = @($SourceLogs | ForEach-Object { Split-Path -Leaf $_ })
    includesFullLog = [bool]$IncludeFullLog
    includesSave = $false
    includesCredential = $false
}
$EvidenceManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $EvidenceManifestPath -Encoding UTF8

Write-Host "Dagmay diagnostic evidence created: $LatestReportPath"
Write-Host "Evidence manifest created: $EvidenceManifestPath"
Write-Host "Attach that one file when asking Codex or ChatGPT to diagnose a RimWorld run."
if (-not $CurrentOnly -and $SourceLogs.Count -eq 1) {
    Write-Warning "Player-prev.log was not present, so only the current RimWorld process was included."
}
