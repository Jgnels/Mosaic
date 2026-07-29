[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Root
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Fail([string]$Message) {
    throw "MOSAIC 0.3D PROVISIONAL APPRAISAL SOURCE GATE FAIL: $Message"
}

$Root = [IO.Path]::GetFullPath($Root)
$required = @(
    "Dagmay.Core\Appraisal\ProvisionalDialogueAppraisalContracts.cs",
    "Dagmay.Core\Appraisal\ProvisionalDialogueAppraisalStore.cs",
    "Dagmay.Tests\Mosaic03DProvisionalDialogueAppraisalContractTests.cs",
    "Dagmay.Tests\Program.cs",
    "Dagmay.IntegrationHarness\IntegrationScenarios.cs",
    "docs\MOSAIC_0.3D_PROVISIONAL_DIALOGUE_APPRAISAL_BOUNDARY.md",
    "tools\verify-0.3d-provisional-appraisal.ps1"
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $relative) -PathType Leaf)) {
        Fail "Missing required v39 file: $relative"
    }
}

$staticCs = @(
    Get-ChildItem -LiteralPath $Root -Recurse -File -Filter "*.cs" |
        Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj|artifacts)[\\/]'
        }
)
if ($staticCs.Count -ne 166) {
    Fail "Expected exactly 166 static C# files; found $($staticCs.Count)."
}

$contractsPath = Join-Path $Root "Dagmay.Core\Appraisal\ProvisionalDialogueAppraisalContracts.cs"
$storePath = Join-Path $Root "Dagmay.Core\Appraisal\ProvisionalDialogueAppraisalStore.cs"
$contracts = Get-Content -LiteralPath $contractsPath -Raw
$store = Get-Content -LiteralPath $storePath -Raw
$authoritySurface = $contracts + "`n" + $store
foreach ($forbidden in @(
    "using Verse",
    "Verse.",
    "UnityEngine.",
    "HarmonyPatch",
    "HttpClient",
    "IModelProvider",
    "GenerateStructuredAsync",
    "Scribe_",
    "JobMaker",
    "StartJob",
    "TryTakeOrderedJob",
    "System.IO",
    "File.",
    "Directory.",
    "DateTime.UtcNow",
    "DateTimeOffset.UtcNow",
    "Guid.NewGuid(",
    "new Random(",
    "Process.Start"
)) {
    if ($authoritySurface.Contains($forbidden)) {
        Fail "Forbidden authority, provider, environment, I/O, or nondeterminism token: $forbidden"
    }
}

foreach ($requiredContract in @(
    "c8557ab218dc0a6136a960cfb495dd4853e5dd3ad81d8eadd7b36082b1b47819",
    "Mosaic.Core.CanonicalMutationReceipt.v1",
    "PROPOSAL_ONLY_NO_MUTATION_AUTHORITY",
    "READ_ONLY_PROVISIONAL_NO_CANONICAL_OR_PAWN_AUTHORITY",
    "PerUtteranceAffectBound = 0.15m",
    "PerUtteranceRelationshipBound = 0.08m",
    "ConversationAffectBound = 0.35m",
    "ConversationRelationshipBound = 0.20m",
    "MaximumActivePerOwner = 32",
    "LifetimeTicks = 15_000",
    "MaximumCompletedRecent = 64",
    "ByteCount = 1 << 20",
    "HashCount = 8"
)) {
    if (-not $authoritySurface.Contains($requiredContract)) {
        Fail "Missing corrected compiled contract token: $requiredContract"
    }
}

$v38Source = Get-Content -LiteralPath (
    Join-Path $Root "Dagmay.Core\Presentation\BoundedReactionLifecycleContracts.cs") -Raw
if (-not $store.Contains("ObservedDisplayReceipt.SourceContractValue") -or
    -not $store.Contains("ObservedDisplayReceipt.SourceGateDigestValue") -or
    -not $v38Source.Contains('Contract = "Mosaic.Core.BoundedReactionLifecycle.v1"') -or
    -not $v38Source.Contains('SourceGateDigestValue = "f44b00eee5a2fe8d7609bbd80caa79948caf388760f2e33765ec64ad50c3e0d8"')) {
    Fail "The v39 store is not bound to the authoritative compiled v38 source contract and gate digest."
}

if (-not $contracts.Contains("internal static CanonicalApplicationReceipt CreateTrusted(")) {
    Fail "Trusted canonical-success construction must remain internal."
}
if ($contracts.Contains("public static CanonicalApplicationReceipt CreateTrusted(")) {
    Fail "Trusted canonical-success construction became public."
}
if (-not $store.Contains("Relationship proposal requires an existing canonical relationship state.")) {
    Fail "The corrected relationship-state prerequisite is missing."
}
if (-not $store.Contains("Successful receipt must preserve untargeted affect state.")) {
    Fail "The corrected no-op/untargeted affect rule is missing."
}
if (-not $store.Contains("Successful receipt must preserve untargeted relationship state.")) {
    Fail "The corrected untargeted relationship rule is missing."
}

$focusedNames = @(
    "AttemptIsNotSuccess",
    "FailedReleaseIsNotSuccess",
    "NonWitnessFailsClosed",
    "StaleSessionReceiptFails",
    "ForeignKnowledgeFails",
    "MissingKnowledgeFails",
    "UnadmittedKnowledgeFails",
    "FutureKnowledgeFails",
    "DuplicateIsIdempotent",
    "CueOrderIsDeterministic",
    "QuotedInsultIsNotAttributed",
    "ClaimDoesNotBecomeFact",
    "PerUtteranceBounds",
    "ConversationBoundsAndRunawayPrevention",
    "OutOfOrderReceiptsFailClosed",
    "TerminalDuplicateIsRejectedAfterCompaction",
    "SeenFilterIsFixedMemoryAndDeterministic",
    "CapacityEvictionDeterministic",
    "DecayAndExpiry",
    "CheckpointChangeDiscards",
    "RestartDiscardsAndOldReceiptCannotReplay",
    "TraitModifiersAreBoundedAndNoSignFlip",
    "SeverityHybridPresentation",
    "PromotionPacketIsProposalOnly",
    "FailedApplicationIsNotSuccessAndOverlayRemains",
    "SuccessfulZeroEffectReconcilesWithoutDoubleCounting",
    "DuplicateSuccessReceiptIdempotent",
    "SuccessfulPromotionRetentionIsBounded",
    "ForeignApplicationReceiptFails",
    "StaleSuccessReceiptFails",
    "TargetedAffectRequiresVersionAdvance",
    "RelationshipOnlySuccessPreservesAffectVersion",
    "NoopSuccessRejectsArtificialVersionChurn",
    "DiagnosticProjectionDoesNotLeakRawTextOrIds",
    "ForgedDisplayReceiptFailsFingerprint",
    "ForeignDisplayReceiptContractFails",
    "ForgedApplicationReceiptFailsFingerprint",
    "StaticAuthorityContract",
    "V37V38V39Integration",
    "DeterministicReplayAcrossSourcePermutations",
    "ScalingFiftyThousandAdmissionsBounded",
    "ScalingFiftyThousandSuccessfulPromotionsBounded"
)
if ($focusedNames.Count -ne 42) { Fail "Focused matrix count is not exactly 42." }
$tests = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Tests\Mosaic03DProvisionalDialogueAppraisalContractTests.cs") -Raw
$program = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Tests\Program.cs") -Raw
foreach ($name in $focusedNames) {
    if ([regex]::Matches($tests, "public static void " + [regex]::Escape($name) + "\(").Count -ne 1) {
        Fail "Focused test method must exist exactly once: $name"
    }
    if ([regex]::Matches($program, "nameof\(Mosaic03DProvisionalDialogueAppraisalContractTests\." + [regex]::Escape($name) + "\)").Count -ne 1) {
        Fail "Focused test registration must exist exactly once: $name"
    }
}
$firstFocused = $program.IndexOf(
    "nameof(Mosaic03DProvisionalDialogueAppraisalContractTests.AttemptIsNotSuccess)",
    [StringComparison]::Ordinal)
$providerBoundary = $program.IndexOf(
    "nameof(ProviderContractTests.CurrentUserConfigurationOverridesStaleProcessValues)",
    [StringComparison]::Ordinal)
if ($firstFocused -lt 0 -or $providerBoundary -lt 0 -or $firstFocused -gt $providerBoundary) {
    Fail "The corrected v39 focused matrix must precede the provider boundary."
}

$integration = Get-Content -LiteralPath (Join-Path $Root "Dagmay.IntegrationHarness\IntegrationScenarios.cs") -Raw
$v38 = $integration.IndexOf('new ScenarioDefinition("BoundedReactionLifecycle"', [StringComparison]::Ordinal)
$v39 = $integration.IndexOf('new ScenarioDefinition("ProvisionalDialogueAppraisal"', [StringComparison]::Ordinal)
$after = $integration.IndexOf('new ScenarioDefinition("LongHistoryRetrievalBenchmark"', [StringComparison]::Ordinal)
if ($v38 -lt 0 -or $v39 -lt 0 -or $after -lt 0 -or -not ($v38 -lt $v39 -and $v39 -lt $after)) {
    Fail "The v37-v38-v39 integration scenario is absent or out of order."
}
if ([regex]::Matches($integration, 'new ScenarioDefinition\("ProvisionalDialogueAppraisal"').Count -ne 1) {
    Fail "ProvisionalDialogueAppraisal must be registered exactly once."
}

Write-Host "PASS Mosaic 0.3D corrected provisional appraisal source boundary."
Write-Host "PASS static C# files: 166."
Write-Host "PASS focused v39 tests: 42 registered."
Write-Host "PASS corrected semantics: selective versions, relationship prerequisite, packet removal, bounded completion."
Write-Host "PASS authority boundary: provider=0 persistence=0 UI=0 runtime=0 canonical=0 pawn=0."
