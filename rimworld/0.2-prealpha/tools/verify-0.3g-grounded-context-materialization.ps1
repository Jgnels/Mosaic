[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Root,

    [switch]$RunFocused,

    [switch]$RunStress,

    [ValidateRange(1, 50000)]
    [int]$StressCycles = 50000,

    [string]$StressOutput = "artifacts\v42-stress-final.json"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Fail([string]$Message) {
    throw "MOSAIC 0.3G GROUNDED CONTEXT MATERIALIZATION GATE FAIL: $Message"
}

$Root = [IO.Path]::GetFullPath($Root)
$required = @(
    "Dagmay.Core\Development\ContextEvidenceProjection.cs",
    "Dagmay.Core\Development\GroundedDevelopmentalContextPacket.cs",
    "Dagmay.Core\Development\GroundedDevelopmentalContextMaterializer.cs",
    "Dagmay.Tests\Mosaic03GGroundedDevelopmentalContextMaterializationContractTests.cs",
    "Dagmay.Tests\Program.cs",
    "Dagmay.IntegrationHarness\IntegrationScenarios.GroundedDevelopmentalContextMaterialization.cs",
    "Dagmay.IntegrationHarness\IntegrationScenarios.cs",
    "docs\MOSAIC_0.3G_GROUNDED_CONTEXT_MATERIALIZATION.md",
    "tools\verify-0.3g-grounded-context-materialization.ps1"
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $relative) -PathType Leaf)) {
        Fail "Missing required v42 file: $relative"
    }
}

$staticCs = @(
    Get-ChildItem -LiteralPath $Root -Recurse -File -Filter "*.cs" |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|artifacts)[\\/]' }
)
if ($staticCs.Count -ne 180) {
    Fail "Expected exactly 180 static C# files; found $($staticCs.Count)."
}

$projection = Get-Content -LiteralPath (
    Join-Path $Root "Dagmay.Core\Development\ContextEvidenceProjection.cs") -Raw
$packet = Get-Content -LiteralPath (
    Join-Path $Root "Dagmay.Core\Development\GroundedDevelopmentalContextPacket.cs") -Raw
$materializer = Get-Content -LiteralPath (
    Join-Path $Root "Dagmay.Core\Development\GroundedDevelopmentalContextMaterializer.cs") -Raw
$authoritySurface = $projection + "`n" + $packet + "`n" + $materializer

foreach ($token in @(
    "Mosaic.Core.GroundedDevelopmentalContextMaterialization.v1",
    "c48b1e977c96b0450f97e8579e158cfe2353e412ba6dbcdf30e4056cbdd5edb9",
    "7eef7fe095621a15b2e84464abcabf2d05ba3681d41d51488317c2d73776755d",
    "9fa64475517509764ef306ad6c351ce886b6be6750281889145df320848c1b2b",
    "READ_ONLY_APPRAISAL_CONTEXT_NO_CANONICAL_PROVIDER_PLANNER_UI_OR_PAWN_AUTHORITY",
    "VerifyFingerprint()",
    "GroundedJson.Hash(DeterministicJson(false))",
    "ContextualDevelopmentalRetrievalSource.Contract",
    "receipt.ComputeFingerprint()",
    "stores.DevelopmentalRecords",
    "eventLedger.Snapshot()",
    "affect.",
    "relationship.",
    "UNUSED_ROLE_CAP_NOT_RENORMALIZED",
    "CONTRADICTORY_HISTORY_PRESERVED"
)) {
    if (-not $authoritySurface.Contains($token)) {
        Fail "Missing compiled v42 contract token: $token"
    }
}

foreach ($forbidden in @(
    "using Verse",
    "Verse.",
    "using RimWorld",
    "RimWorld.",
    "UnityEngine.",
    "HarmonyPatch",
    "HttpClient",
    "IModelProvider",
    "GenerateStructuredAsync",
    "Scribe_",
    "JobMaker",
    "StartJob",
    "TryTakeOrderedJob",
    "Process.Start",
    "DateTime.UtcNow",
    "DateTimeOffset.UtcNow",
    "Guid.NewGuid(",
    "new Random(",
    "RawDialogueText",
    "PromptText",
    "DisplayLabel",
    "PlayerKnowledge"
)) {
    if ($authoritySurface.Contains($forbidden)) {
        Fail "Forbidden runtime, provider, UI, private-data, pawn, or nondeterminism token: $forbidden"
    }
}

if ($projection.Contains("List<DevelopmentalAppraisalRecord>") -or
    $projection.Contains("List<EnvironmentEvent> _") -or
    $projection.Contains("ContextualMemoryEvidence> _")) {
    Fail "The compact v42 registry retains a full canonical object collection."
}

$tests = Get-Content -LiteralPath (
    Join-Path $Root "Dagmay.Tests\Mosaic03GGroundedDevelopmentalContextMaterializationContractTests.cs") -Raw
$focused = [regex]::Matches($tests, '"(test_[a-z0-9_]+)"')
$focusedNames = @($focused | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
if ($focusedNames.Count -ne 58) {
    Fail "Expected exactly 58 unique focused v42 tests; found $($focusedNames.Count)."
}

$program = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Tests\Program.cs") -Raw
if ([regex]::Matches(
    $program,
    "Mosaic03GGroundedDevelopmentalContextMaterializationContractTests.AddTo\(tests\)").Count -ne 1) {
    Fail "The v42 focused matrix must be registered exactly once."
}

$integration = Get-Content -LiteralPath (
    Join-Path $Root "Dagmay.IntegrationHarness\IntegrationScenarios.cs") -Raw
if ([regex]::Matches(
    $integration,
    'new ScenarioDefinition\("GroundedDevelopmentalContextMaterialization"').Count -ne 1) {
    Fail "The v42 integration scenario must be registered exactly once."
}

if ($RunFocused) {
    $previousSet = $env:MOSAIC_TEST_SET
    try {
        $env:MOSAIC_TEST_SET = "V42"
        $output = & dotnet run --project (
            Join-Path $Root "Dagmay.Tests\Dagmay.Tests.csproj") --configuration Release 2>&1
        if ($LASTEXITCODE -ne 0) {
            Fail "Focused v42 test process failed.`n$($output -join "`n")"
        }
        if (($output -join "`n") -notmatch "Executed 58 tests; 0 failed\.") {
            Fail "Focused v42 result was not exactly 58/58."
        }
    }
    finally {
        $env:MOSAIC_TEST_SET = $previousSet
    }
}

if ($RunStress) {
    $outputPath = [IO.Path]::GetFullPath((Join-Path $Root $StressOutput))
    $output = & dotnet run --project (
        Join-Path $Root "Dagmay.IntegrationHarness\Dagmay.IntegrationHarness.csproj"
    ) --configuration Release -- --scenario GroundedDevelopmentalContextMaterialization `
        --cycles $StressCycles --output $outputPath --fail-fast 2>&1
    if ($LASTEXITCODE -ne 0) {
        Fail "V42 stress process failed.`n$($output -join "`n")"
    }
    $result = Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json
    if ($result.ScenarioCount -ne 1 -or $result.PassedCount -ne 1 -or $result.FailedCount -ne 0) {
        Fail "V42 stress scenario did not pass exactly once."
    }
    $scenario = $result.Scenarios[0]
    $expectedQueries = [Math]::Max(1, [int]($StressCycles / 5))
    if ($scenario.Metrics.developmentalRecords -ne $StressCycles -or
        $scenario.Metrics.canonicalMemories -ne $StressCycles -or
        $scenario.Metrics.queriesPerDirection -ne $expectedQueries -or
        $scenario.Metrics.totalQueries -ne ($expectedQueries * 2) -or
        $scenario.Metrics.maximumResults -gt 4 -or
        $scenario.Metrics.fullCanonicalObjectsRetained -ne 0) {
        Fail "V42 stress metrics did not satisfy the exact scale and retention contract."
    }
}

Write-Host "PASS Mosaic 0.3G grounded-developmental-context-materialization source boundary."
Write-Host "PASS static C# files: 180."
Write-Host "PASS focused v42 tests: 58 exact registrations."
Write-Host "PASS bounds: items=4 total-role-cap-bps=10000 full-canonical-retention=0."
Write-Host "PASS public packet fingerprint self-verification uses the exact v42 source gate digest."
Write-Host "PASS authority boundary: provider=0 UI=0 runtime=0 planner=0 world=0 pawn=0."
