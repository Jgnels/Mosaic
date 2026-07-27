[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [switch]$SkipExecution
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Fail([string]$Message) { throw "DETERMINISM CLOSURE FAIL: $Message" }

$ReadPaths = @(
    "Dagmay.Core\Dialogue",
    "Dagmay.Core\Observer",
    "Dagmay.Core\Presentation",
    "Dagmay.Core\Views",
    "Dagmay.RimWorld\Dialogue"
)
$RandomPatterns = @(
    '(?<![A-Za-z0-9_])System\.Random(?![A-Za-z0-9_])',
    '(?<![A-Za-z0-9_])new\s+Random\s*\(',
    '(?<![A-Za-z0-9_])UnityEngine\.Random(?![A-Za-z0-9_])',
    '(?<![A-Za-z0-9_])Verse\.Rand(?![A-Za-z0-9_])',
    '(?<![A-Za-z0-9_])Rand\.(Value|Range|RangeInclusive|Chance|Gaussian)\b'
)

$violations = New-Object System.Collections.Generic.List[string]
foreach ($Relative in $ReadPaths) {
    $Path = Join-Path $Root $Relative
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { continue }
    foreach ($File in Get-ChildItem -LiteralPath $Path -Recurse -Filter *.cs -File) {
        $Text = [IO.File]::ReadAllText($File.FullName)
        foreach ($Pattern in $RandomPatterns) {
            if ([Regex]::IsMatch($Text, $Pattern)) {
                $violations.Add("$($File.FullName): $Pattern")
            }
        }
    }
}
if ($violations.Count -gt 0) {
    Fail ("simulation RNG dependency found in read/presentation paths:`n" + ($violations -join "`n"))
}

$ViewerFiles = @(
    "Dagmay.Core\Dialogue\ConversationHistoryViewer.cs",
    "Dagmay.RimWorld\Dialogue\MosaicConversationHistoryWindow.cs",
    "Dagmay.RimWorld\Dialogue\RimWorldConversationHistorySnapshot.cs"
)
$MutationPatterns = @(
    'GenerateStructuredAsync\s*\(',
    'TryQueueDialogueAdmission\s*\(',
    '\.Append\s*\(',
    '\.Save\s*\(',
    '\.Enqueue\s*\(',
    'TryCommit\s*\(',
    'WithAffect\s*\(',
    'TransitionLifecycle\s*\(',
    'StartJob\s*\(',
    '\.jobs\b',
    '\.pather\b'
)
foreach ($Relative in $ViewerFiles) {
    $Path = Join-Path $Root $Relative
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail "required read-only viewer file is missing: $Relative"
    }
    $Text = [IO.File]::ReadAllText($Path)
    foreach ($Pattern in $MutationPatterns) {
        if ([Regex]::IsMatch($Text, $Pattern)) {
            Fail "read-only viewer contains forbidden mutation/provider surface: $Relative matched $Pattern"
        }
    }
}

Write-Host "PASS source RNG firewall and read-only viewer mutation firewall."
if ($SkipExecution) { exit 0 }

$Dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$TestsProject = Join-Path $Root "Dagmay.Tests\Dagmay.Tests.csproj"
$HarnessProject = Join-Path $Root "Dagmay.IntegrationHarness\Dagmay.IntegrationHarness.csproj"
$Temp = Join-Path ([IO.Path]::GetTempPath()) ("mosaic-determinism-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $Temp -Force | Out-Null
try {
    $testOutputs = @()
    for ($Run = 1; $Run -le 2; $Run++) {
        $Output = & $Dotnet run --project $TestsProject -c Release --no-restore 2>&1
        if ($LASTEXITCODE -ne 0) { Fail "contract test run $Run failed.`n$($Output -join "`n")" }
        $testOutputs += ,(($Output | Where-Object { $_ -match '^(PASS|Executed)' }) -join "`n")
    }
    if ($testOutputs[0] -cne $testOutputs[1]) {
        Fail "two contract-test process runs produced different normalized output."
    }

    $digests = @()
    for ($Run = 1; $Run -le 2; $Run++) {
        $Report = Join-Path $Temp ("determinism-$Run.json")
        $Output = & $Dotnet run --project $HarnessProject -c Release --no-restore -- `
            --scenario DeterminismObserverLifecycleClosure `
            --output $Report 2>&1
        if ($LASTEXITCODE -ne 0) { Fail "integration determinism run $Run failed.`n$($Output -join "`n")" }
        $Json = Get-Content -LiteralPath $Report -Raw | ConvertFrom-Json
        $Scenario = @($Json.Scenarios | Where-Object { $_.Name -eq 'DeterminismObserverLifecycleClosure' })
        if ($Scenario.Count -ne 1 -or -not $Scenario[0].Passed) {
            Fail "integration determinism report did not contain one passing scenario."
        }
        $digests += [string]$Scenario[0].Details.deterministicDigest
    }
    if ([string]::IsNullOrWhiteSpace($digests[0]) -or $digests[0] -cne $digests[1]) {
        Fail "two independent integration processes did not reproduce one deterministic digest."
    }
    Write-Host "PASS repeated contracts and integration replay; digest=$($digests[0])."
}
finally {
    Remove-Item -LiteralPath $Temp -Recurse -Force -ErrorAction SilentlyContinue
}
