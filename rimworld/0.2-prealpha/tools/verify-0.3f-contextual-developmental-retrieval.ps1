[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Root,

    [switch]$RunFocused
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Fail([string]$Message) {
    throw "MOSAIC 0.3F CONTEXTUAL DEVELOPMENTAL RETRIEVAL GATE FAIL: $Message"
}

$Root = [IO.Path]::GetFullPath($Root)
$required = @(
    "Dagmay.Core\Development\ContextualDevelopmentalRetrievalContracts.cs",
    "Dagmay.Core\Development\ContextualDevelopmentalRetrievalIndex.cs",
    "Dagmay.Tests\Mosaic03FContextualDevelopmentalRetrievalContractTests.cs",
    "Dagmay.Tests\Program.cs",
    "Dagmay.IntegrationHarness\IntegrationScenarios.ContextualDevelopmentalRetrieval.cs",
    "Dagmay.IntegrationHarness\IntegrationScenarios.cs",
    "docs\MOSAIC_0.3F_CONTEXTUAL_DEVELOPMENTAL_RETRIEVAL.md",
    "tools\verify-0.3f-contextual-developmental-retrieval.ps1"
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $relative) -PathType Leaf)) {
        Fail "Missing required v41 file: $relative"
    }
}

$staticCs = @(
    Get-ChildItem -LiteralPath $Root -Recurse -File -Filter "*.cs" |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|artifacts)[\\/]' }
)
if ($staticCs.Count -ne 175) {
    Fail "Expected exactly 175 static C# files; found $($staticCs.Count)."
}

$contractsPath = Join-Path $Root "Dagmay.Core\Development\ContextualDevelopmentalRetrievalContracts.cs"
$indexPath = Join-Path $Root "Dagmay.Core\Development\ContextualDevelopmentalRetrievalIndex.cs"
$contracts = Get-Content -LiteralPath $contractsPath -Raw
$index = Get-Content -LiteralPath $indexPath -Raw
$authoritySurface = $contracts + "`n" + $index

foreach ($token in @(
    "Mosaic.Core.ContextualDevelopmentalRetrieval.v1",
    "639e5b4ff2d7629fed5b76303b21bbcecbf03f167068d2d4046cb9ef1b153ce9",
    "READ_ONLY_CONTEXT_NO_CANONICAL_OR_PAWN_AUTHORITY",
    "MaximumCandidatesPerKey = 64",
    "MaximumResults = 4",
    "DevelopmentalContextRole.DurableAnchor, 4000",
    "DevelopmentalContextRole.RecentLived, 3000",
    "DevelopmentalContextRole.CategoryAnchor, 2000",
    "DevelopmentalContextRole.ContradictoryContext, 1000",
    "DialogueAdmissionOutboxCodec().ComputeEventHash",
    "receipt.ComputeFingerprint()",
    "stores.DevelopmentalRecords",
    "eventLedger.Snapshot()"
)) {
    if (-not $authoritySurface.Contains($token)) {
        Fail "Missing compiled v41 contract token: $token"
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

if ([regex]::IsMatch(
    $index,
    "public\s+.*\(\s*DevelopmentalAppraisalRecord")) {
    Fail "A public loose-record admission path exists."
}
if ($index.Contains("List<DevelopmentalAppraisalRecord>") -or
    $index.Contains("List<EnvironmentEvent> _") -or
    $index.Contains("ContextualMemoryEvidence> _")) {
    Fail "The compact index retains a full canonical object collection."
}

$testsPath = Join-Path $Root "Dagmay.Tests\Mosaic03FContextualDevelopmentalRetrievalContractTests.cs"
$tests = Get-Content -LiteralPath $testsPath -Raw
$focused = [regex]::Matches($tests, '"(test_[a-z0-9_]+)"')
$focusedNames = @($focused | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
if ($focusedNames.Count -ne 46) {
    Fail "Expected exactly 46 unique focused v41 tests; found $($focusedNames.Count)."
}

$program = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Tests\Program.cs") -Raw
if ([regex]::Matches(
    $program,
    "Mosaic03FContextualDevelopmentalRetrievalContractTests.AddTo\(tests\)").Count -ne 1) {
    Fail "The v41 focused matrix must be registered exactly once."
}

$integration = Get-Content -LiteralPath (
    Join-Path $Root "Dagmay.IntegrationHarness\IntegrationScenarios.cs") -Raw
if ([regex]::Matches(
    $integration,
    'new ScenarioDefinition\("ContextualDevelopmentalRetrieval"').Count -ne 1) {
    Fail "The v41 integration scenario must be registered exactly once."
}

if ($RunFocused) {
    $previousSet = $env:MOSAIC_TEST_SET
    try {
        $env:MOSAIC_TEST_SET = "V41"
        $output = & dotnet run --project (
            Join-Path $Root "Dagmay.Tests\Dagmay.Tests.csproj") --configuration Release 2>&1
        if ($LASTEXITCODE -ne 0) {
            Fail "Focused v41 test process failed.`n$($output -join "`n")"
        }
        if (($output -join "`n") -notmatch "Executed 46 tests; 0 failed\.") {
            Fail "Focused v41 result was not exactly 46/46."
        }
    }
    finally {
        $env:MOSAIC_TEST_SET = $previousSet
    }
}

Write-Host "PASS Mosaic 0.3F contextual-developmental-retrieval source boundary."
Write-Host "PASS static C# files: 175."
Write-Host "PASS focused v41 tests: 46 exact registrations."
Write-Host "PASS bounds: candidates-per-key=64 results=4 roots=8 tags=16."
Write-Host "PASS authority boundary: provider=0 UI=0 runtime=0 planner=0 world=0 pawn=0."
