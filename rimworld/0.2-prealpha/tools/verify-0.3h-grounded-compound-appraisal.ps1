[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Root,

    [switch]$RunFocused,

    [switch]$RunStress,

    [ValidateRange(1, 10000)]
    [int]$StressCycles = 10000,

    [string]$StressOutput = "artifacts\v43-stress-final.json"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Fail([string]$Message) {
    throw "MOSAIC 0.3H GROUNDED COMPOUND APPRAISAL GATE FAIL: $Message"
}

function Invoke-NativeRaw([string]$FilePath, [string[]]$Arguments) {
    # Windows PowerShell 5.1 may promote native stderr to ErrorRecord objects under Stop.
    # Capture output and exit code explicitly; do not treat ordinary compiler progress as a
    # PowerShell exception or lose a successful process result.
    $previousPreference = $ErrorActionPreference
    $raw = @()
    $exitCode = -1
    try {
        $ErrorActionPreference = "Continue"
        $raw = @(& $FilePath @Arguments 2>&1)
        $exitCode = [int]$LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }
    $lines = New-Object "System.Collections.Generic.List[string]"
    foreach ($item in @($raw)) {
        if ($item -is [System.Management.Automation.ErrorRecord] -and $null -ne $item.Exception) {
            $lines.Add([string]$item.Exception.Message)
        }
        else {
            $lines.Add([string]$item)
        }
    }
    return [pscustomobject]@{ ExitCode = $exitCode; Lines = @($lines.ToArray()) }
}

function Require-NativeSuccess([string]$Description, [string]$FilePath, [string[]]$Arguments) {
    $result = Invoke-NativeRaw -FilePath $FilePath -Arguments $Arguments
    if ($result.ExitCode -ne 0) {
        Fail ("{0} failed with exit code {1}.`n{2}" -f
            $Description, $result.ExitCode, (@($result.Lines) -join [Environment]::NewLine))
    }
    return $result
}

$Root = [IO.Path]::GetFullPath($Root)
$required = @(
    "Dagmay.Core\Development\GroundedCurrentEventAppraisalSeed.cs",
    "Dagmay.Core\Development\GroundedCompoundAppraisalProposal.cs",
    "Dagmay.Core\Development\GroundedCompoundAppraisalSynthesizer.cs",
    "Dagmay.Tests\Mosaic03HGroundedCompoundAppraisalContractTests.cs",
    "Dagmay.Tests\Fixtures\V43_SHARED_DETERMINISTIC_FIXTURES.json",
    "Dagmay.Tests\Program.cs",
    "Dagmay.IntegrationHarness\IntegrationScenarios.GroundedCompoundAppraisal.cs",
    "Dagmay.IntegrationHarness\IntegrationScenarios.cs",
    "docs\MOSAIC_0.3H_GROUNDED_COMPOUND_APPRAISAL.md",
    "tools\verify-0.3h-grounded-compound-appraisal.ps1"
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $relative) -PathType Leaf)) {
        Fail "Missing required v43 file: $relative"
    }
}

$staticCs = @(
    Get-ChildItem -LiteralPath $Root -Recurse -File -Filter "*.cs" |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|artifacts)[\\/]' }
)
if ($staticCs.Count -ne 185) {
    Fail "Expected exactly 185 static C# files; found $($staticCs.Count)."
}

$seed = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Core\Development\GroundedCurrentEventAppraisalSeed.cs") -Raw
$proposal = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Core\Development\GroundedCompoundAppraisalProposal.cs") -Raw
$synthesizer = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Core\Development\GroundedCompoundAppraisalSynthesizer.cs") -Raw
$authoritySurface = $seed + "`n" + $proposal + "`n" + $synthesizer

foreach ($token in @(
    "Mosaic.Core.GroundedCompoundAppraisalProposal.v1",
    "045ef8a2fd6084c183ced7d2ec9516b3289a309f0b3c55963a895746c70de0d8",
    "b1da1f6ef60701772e890da2e546dd87a58a6bf433161681b0f9d848682c4157",
    "c48b1e977c96b0450f97e8579e158cfe2353e412ba6dbcdf30e4056cbdd5edb9",
    "PROPOSAL_ONLY_NO_CANONICAL_PRESENTATION_PROVIDER_PLANNER_UI_OR_PAWN_AUTHORITY",
    "GroundedDevelopmentalContextPacket.RequireValid",
    "VerifyFingerprint()",
    "APPRAISAL_REGIME_",
    "CURRENT_AND_HISTORY_DISAGREE",
    "HISTORY_NOT_ALLOWED_TO_CREATE_RELATIONSHIP_CHANGE",
    "WHY_TRACE_TRUNCATED"
)) {
    if (-not $authoritySurface.Contains($token)) {
        Fail "Missing compiled v43 contract token: $token"
    }
}

foreach ($forbidden in @(
    "using Verse", "Verse.", "using RimWorld", "RimWorld.", "UnityEngine.",
    "HarmonyPatch", "HttpClient", "IModelProvider", "GenerateStructuredAsync",
    "Scribe_", "JobMaker", "StartJob", "TryTakeOrderedJob", "Process.Start",
    "DateTime.UtcNow", "DateTimeOffset.UtcNow", "Guid.NewGuid(", "new Random(",
    "RawDialogueText", "PromptText", "DisplayLabel", "PlayerKnowledge", "TraitLabel", "Diagnosis"
)) {
    if ($authoritySurface.Contains($forbidden)) {
        Fail "Forbidden runtime, provider, UI, private-data, trait, pawn, or nondeterminism token: $forbidden"
    }
}

$tests = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Tests\Mosaic03HGroundedCompoundAppraisalContractTests.cs") -Raw
$focused = [regex]::Matches($tests, '"(test_[a-z0-9_]+)"')
$focusedNames = @($focused | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
if ($focusedNames.Count -ne 87) {
    Fail "Expected exactly 87 unique focused v43 tests; found $($focusedNames.Count)."
}

$program = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Tests\Program.cs") -Raw
if ([regex]::Matches($program, "Mosaic03HGroundedCompoundAppraisalContractTests.AddTo\(tests\)").Count -ne 1) {
    Fail "The v43 focused matrix must be registered exactly once."
}
if ([regex]::Matches($program, 'string.Equals\(testSet, "V43"').Count -ne 1) {
    Fail "The exact V43 focused test slice must be registered once."
}

$integration = Get-Content -LiteralPath (Join-Path $Root "Dagmay.IntegrationHarness\IntegrationScenarios.cs") -Raw
if ([regex]::Matches($integration, 'new ScenarioDefinition\("GroundedCompoundAppraisal"').Count -ne 1) {
    Fail "The v43 integration scenario must be registered exactly once."
}

if ($RunFocused) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail "dotnet is unavailable." }
    $previousSet = $env:MOSAIC_TEST_SET
    try {
        $env:MOSAIC_TEST_SET = "V43"
        $testProject = Join-Path $Root "Dagmay.Tests\Dagmay.Tests.csproj"
        $result = Require-NativeSuccess -Description "Focused v43 tests" -FilePath "dotnet" -Arguments @(
            "run", "--project", $testProject, "--configuration", "Release")
        $text = @($result.Lines) -join "`n"
        if ($text -notmatch "Executed 87 tests; 0 failed\.") {
            Fail "Focused v43 result was not exactly 87/87.`n$text"
        }
    }
    finally {
        if ($null -eq $previousSet) { Remove-Item Env:MOSAIC_TEST_SET -ErrorAction SilentlyContinue }
        else { $env:MOSAIC_TEST_SET = $previousSet }
    }
}

if ($RunStress) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail "dotnet is unavailable." }
    $outputPath = [IO.Path]::GetFullPath((Join-Path $Root $StressOutput))
    $outputParent = Split-Path -Parent $outputPath
    if (-not (Test-Path -LiteralPath $outputParent)) { New-Item -ItemType Directory -Path $outputParent -Force | Out-Null }
    $integrationProject = Join-Path $Root "Dagmay.IntegrationHarness\Dagmay.IntegrationHarness.csproj"
    $result = Require-NativeSuccess -Description "V43 stress scenario" -FilePath "dotnet" -Arguments @(
        "run", "--project", $integrationProject, "--configuration", "Release", "--",
        "--scenario", "GroundedCompoundAppraisal", "--cycles", $StressCycles.ToString([Globalization.CultureInfo]::InvariantCulture),
        "--output", $outputPath, "--fail-fast")
    if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
        Fail "V43 stress process did not create its result file.`n$(@($result.Lines) -join "`n")"
    }
    $gateResult = Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json
    if ($gateResult.ScenarioCount -ne 1 -or $gateResult.PassedCount -ne 1 -or $gateResult.FailedCount -ne 0) {
        Fail "V43 stress scenario did not pass exactly once."
    }
    $scenarios = @($gateResult.Scenarios)
    if ($scenarios.Count -ne 1) { Fail "V43 stress result did not contain exactly one scenario." }
    $scenario = $scenarios[0]
    $expectedContexts = [Math]::Max(1, [int]($StressCycles / 2))
    if ($scenario.Metrics.developmentalRecords -ne $StressCycles -or
        $scenario.Metrics.canonicalMemories -ne $StressCycles -or
        $scenario.Metrics.uniqueContexts -ne $expectedContexts -or
        $scenario.Metrics.proposals -ne ($expectedContexts * 10) -or
        $scenario.Metrics.maximumFamilies -gt 3 -or
        $scenario.Metrics.maximumAffectDimensions -gt 6 -or
        $scenario.Metrics.maximumRelationshipDimensions -gt 4 -or
        $scenario.Metrics.maximumWhy -gt 16 -or
        $scenario.Metrics.synthesizerRetainedState -ne 0) {
        Fail "V43 stress metrics did not satisfy the exact scale, output, and statelessness contract."
    }
}

Write-Host "PASS Mosaic 0.3H grounded-compound-appraisal source boundary."
Write-Host "PASS static C# files: 185."
Write-Host "PASS focused v43 tests: 87 exact registrations."
Write-Host "PASS bounds: families=3 affect=6 relationship=4 why=16 total-bps=10000."
Write-Host "PASS authority boundary: canonical=0 provider=0 UI=0 planner=0 world=0 pawn=0."
Write-Host "PASS Windows PowerShell 5.1 native-process capture hardened."
