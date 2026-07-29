[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Root
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Fail([string]$Message) {
    throw "MOSAIC 0.3C BOUNDED REACTION SOURCE GATE FAIL: $Message"
}

function Is-LowerSha256([object]$Value) {
    if (-not ($Value -is [string]) -or $Value.Length -ne 64) { return $false }
    return $Value -cmatch '^[0-9a-f]{64}$'
}

function Is-BoundedText([object]$Value) {
    return ($Value -is [string]) -and
        -not [string]::IsNullOrWhiteSpace($Value) -and
        $Value.Length -le 256
}

function Test-Receipt([hashtable]$Receipt) {
    $allowed = @(
        "session_id",
        "release_attempt_id",
        "receipt_id",
        "utterance_id",
        "conversation_id",
        "speaker_id",
        "recipient_id",
        "audience_ids",
        "exact_text_hash",
        "displayed_tick",
        "checkpoint_generation",
        "outcome",
        "source_contract",
        "source_gate_digest"
    )
    if (@($Receipt.Keys | Where-Object { $allowed -notcontains $_ }).Count -ne 0) { return $false }
    if (@($allowed | Where-Object { -not $Receipt.ContainsKey($_) }).Count -ne 0) { return $false }
    foreach ($name in @("session_id", "utterance_id", "conversation_id", "speaker_id")) {
        if (-not (Is-BoundedText $Receipt[$name])) { return $false }
    }
    foreach ($name in @("release_attempt_id", "receipt_id", "exact_text_hash", "source_gate_digest")) {
        if (-not (Is-LowerSha256 $Receipt[$name])) { return $false }
    }
    if ($null -ne $Receipt.recipient_id -and -not (Is-BoundedText $Receipt.recipient_id)) { return $false }
    if (-not ($Receipt.audience_ids -is [array]) -or $Receipt.audience_ids.Count -lt 1 -or $Receipt.audience_ids.Count -gt 32) {
        return $false
    }
    if (@($Receipt.audience_ids | Where-Object { -not (Is-BoundedText $_) }).Count -ne 0) { return $false }
    $canonicalAudience = @($Receipt.audience_ids | Sort-Object -Unique -CaseSensitive)
    if ($canonicalAudience.Count -ne $Receipt.audience_ids.Count) { return $false }
    if (($canonicalAudience -join "`n") -cne (@($Receipt.audience_ids) -join "`n")) { return $false }
    if ($Receipt.audience_ids -cnotcontains $Receipt.speaker_id) { return $false }
    if ($null -ne $Receipt.recipient_id -and $Receipt.audience_ids -cnotcontains $Receipt.recipient_id) { return $false }
    if (-not ($Receipt.displayed_tick -is [long] -or $Receipt.displayed_tick -is [int]) -or $Receipt.displayed_tick -lt 0) {
        return $false
    }
    if (-not ($Receipt.checkpoint_generation -is [long] -or $Receipt.checkpoint_generation -is [int]) -or
        $Receipt.checkpoint_generation -lt 0) {
        return $false
    }
    if (@("OBSERVED_SUCCESS", "ATTEMPT_ONLY", "FAILED") -cnotcontains $Receipt.outcome) { return $false }
    if ($Receipt.source_contract -cne "Mosaic.Core.BoundedReactionLifecycle.v1") { return $false }
    if ($Receipt.source_gate_digest -cne "f44b00eee5a2fe8d7609bbd80caa79948caf388760f2e33765ec64ad50c3e0d8") {
        return $false
    }
    return $true
}

function New-ValidReceipt {
    return @{
        session_id = "session-a"
        release_attempt_id = ("1" * 64)
        receipt_id = ("2" * 64)
        utterance_id = "utterance-a"
        conversation_id = "conversation-a"
        speaker_id = "owner-a"
        recipient_id = $null
        audience_ids = @("owner-a")
        exact_text_hash = ("3" * 64)
        displayed_tick = [long]120
        checkpoint_generation = [long]7
        outcome = "OBSERVED_SUCCESS"
        source_contract = "Mosaic.Core.BoundedReactionLifecycle.v1"
        source_gate_digest = "f44b00eee5a2fe8d7609bbd80caa79948caf388760f2e33765ec64ad50c3e0d8"
    }
}

function Apply-Mutation([hashtable]$Receipt, [string]$Mutation) {
    switch ($Mutation) {
        "missing_session" { $Receipt.Remove("session_id") }
        "blank_attempt" { $Receipt.release_attempt_id = "" }
        "malformed_receipt_id" { $Receipt.receipt_id = "XYZ" }
        "missing_utterance" { $Receipt.utterance_id = "" }
        "missing_conversation" { $Receipt.conversation_id = "" }
        "missing_speaker" { $Receipt.speaker_id = "" }
        "recipient_not_audience" { $Receipt.recipient_id = "recipient-x" }
        "empty_audience" { $Receipt.audience_ids = @() }
        "unsorted_audience" { $Receipt.audience_ids = @("witness-b", "owner-a") }
        "duplicate_audience" { $Receipt.audience_ids = @("owner-a", "owner-a") }
        "speaker_not_witness" { $Receipt.audience_ids = @("witness-a") }
        "malformed_text_hash" { $Receipt.exact_text_hash = ("A" * 64) }
        "negative_displayed_tick" { $Receipt.displayed_tick = [long]-1 }
        "negative_checkpoint" { $Receipt.checkpoint_generation = [long]-1 }
        "unknown_outcome" { $Receipt.outcome = "SHOWN" }
        "foreign_contract" { $Receipt.source_contract = "Foreign.Contract.v1" }
        "foreign_digest" { $Receipt.source_gate_digest = ("f" * 64) }
        "raw_dialogue_field" { $Receipt.Add("raw_text", "forbidden") }
        "unknown_field" { $Receipt.Add("extra", "forbidden") }
        default { Fail "Unknown fixture mutation: $Mutation" }
    }
}

$Root = [IO.Path]::GetFullPath($Root)
$required = @(
    "Dagmay.Core\Presentation\BoundedReactionLifecycleContracts.cs",
    "Dagmay.Tests\Mosaic03BoundedReactionLifecycleContractTests.cs",
    "Dagmay.Tests\Program.cs",
    "Dagmay.IntegrationHarness\BoundedReactionLifecycleScenario.cs",
    "Dagmay.IntegrationHarness\IntegrationScenarios.cs",
    "docs\MOSAIC_0.3C_BOUNDED_REACTION_LIFECYCLE_BOUNDARY.md",
    "tools\mosaic-0.3c-bounded-reaction-fixtures.json",
    "tools\verify-0.3c-bounded-reaction-lifecycle.ps1"
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $relative) -PathType Leaf)) {
        Fail "Missing required v38 file: $relative"
    }
}

$corePath = Join-Path $Root "Dagmay.Core\Presentation\BoundedReactionLifecycleContracts.cs"
$scenarioPath = Join-Path $Root "Dagmay.IntegrationHarness\BoundedReactionLifecycleScenario.cs"
$core = Get-Content -LiteralPath $corePath -Raw
$scenario = Get-Content -LiteralPath $scenarioPath -Raw
$authoritySurface = $core + "`n" + $scenario
foreach ($forbidden in @(
    "using Verse",
    "Verse.",
    "UnityEngine.",
    "HarmonyPatch",
    "HttpClient",
    "GenerateStructuredAsync",
    "Scribe_",
    "JobMaker",
    "StartJob",
    "TryTakeOrderedJob",
    "DateTime.UtcNow",
    "DateTimeOffset.UtcNow",
    "Guid.NewGuid(",
    "new Random(",
    "Process.Start",
    "File.Write",
    "Directory.CreateDirectory"
)) {
    if ($authoritySurface.Contains($forbidden)) {
        Fail "Forbidden authority, environment, I/O, or nondeterminism token: $forbidden"
    }
}
if (-not $core.Contains("internal static ObservedDisplayReceipt CreateTrusted(")) {
    Fail "Trusted observed-success construction must remain internal."
}
if (-not $core.Contains("Public Core callers cannot manufacture observed success.")) {
    Fail "The public non-success boundary is missing."
}
foreach ($constant in @(
    "Mosaic.Core.BoundedReactionLifecycle.v1",
    "f44b00eee5a2fe8d7609bbd80caa79948caf388760f2e33765ec64ad50c3e0d8",
    "OBSERVED_SUCCESS",
    "ATTEMPT_ONLY",
    "FAILED",
    "MaximumLiveTickets = 8",
    "MaximumAttempts = 3"
)) {
    if (-not $core.Contains($constant)) { Fail "Missing compiled contract constant: $constant" }
}

$focusedNames = @(
    "SourceRejectsDefaultForeignAndUngroundedIdentity",
    "TicketIdentityIsDeterministicAcrossEquivalentInputOrder",
    "TicketCarriesHashOnlyAndExcludesRawDialogue",
    "TicketAudienceIsOwnerScopedSortedAndUnique",
    "QueueIsGloballyBoundedToEightWithDeterministicEviction",
    "PrivateHistoryProjectionRequiresRecordedPerspectiveOwnership",
    "ReleaseAttemptIsExplicitlyNotSuccess",
    "AttemptIdentifiersAreDeterministicAndOrdinal",
    "ObservedReceiptRequiresExactAttemptTicketAndUtterance",
    "ObservedReceiptRequiresTrustedSourceContractAndGateDigest",
    "ObservedReceiptFingerprintCoversEverySemanticField",
    "ObservedReceiptRequiresActualSortedAudienceAndSpeakerWitness",
    "AttemptOnlyAndFailedOutcomesNeverCompletePresentation",
    "ExactSuccessfulDuplicateIsIdempotent",
    "ConflictingDuplicateReceiptFailsClosed",
    "ForeignOwnerSessionConversationOrCheckpointFailsClosed",
    "StaleFutureAndOutOfOrderReceiptsFailClosed",
    "RetryScheduleIsCallerTickDrivenBoundedAndDeterministic",
    "ExpiredTicketCannotBeRevivedByLateReceipt",
    "OrphanRecoveryCannotManufactureSuccess",
    "SnapshotRoundTripPreservesRecoverableInflightState",
    "CorruptTruncatedAndUnsupportedSnapshotsFailClosed",
    "CheckpointRollbackCannotApplyFutureLifecycleState",
    "SaveReloadAtEveryLifecycleBoundaryReplaysIdentically",
    "FiftyThousandAdmissionsRemainBoundedAndDeterministic",
    "AuthorityFirewallExcludesProviderCanonicalUiAndPawnControl"
)
$tests = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Tests\Mosaic03BoundedReactionLifecycleContractTests.cs") -Raw
$program = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Tests\Program.cs") -Raw
foreach ($name in $focusedNames) {
    if ([regex]::Matches($tests, "public static void " + [regex]::Escape($name) + "\(").Count -ne 1) {
        Fail "Focused test method must exist exactly once: $name"
    }
    if ([regex]::Matches($program, "nameof\(Mosaic03BoundedReactionLifecycleContractTests\." + [regex]::Escape($name) + "\)").Count -ne 1) {
        Fail "Focused test registration must exist exactly once: $name"
    }
}
if ($focusedNames.Count -ne 26) { Fail "Focused matrix count is not exactly 26." }
$firstFocused = $program.IndexOf("nameof(Mosaic03BoundedReactionLifecycleContractTests.SourceRejectsDefaultForeignAndUngroundedIdentity)", [StringComparison]::Ordinal)
$providerBoundary = $program.IndexOf("nameof(ProviderContractTests.CurrentUserConfigurationOverridesStaleProcessValues)", [StringComparison]::Ordinal)
if ($firstFocused -lt 0 -or $providerBoundary -lt 0 -or $firstFocused -gt $providerBoundary) {
    Fail "The focused matrix must be immediately before the required provider test boundary."
}

$integration = Get-Content -LiteralPath (Join-Path $Root "Dagmay.IntegrationHarness\IntegrationScenarios.cs") -Raw
$before = $integration.IndexOf('new ScenarioDefinition("ReadOnlySocialEventEnvelope"', [StringComparison]::Ordinal)
$bounded = $integration.IndexOf('new ScenarioDefinition("BoundedReactionLifecycle"', [StringComparison]::Ordinal)
$after = $integration.IndexOf('new ScenarioDefinition("LongHistoryRetrievalBenchmark"', [StringComparison]::Ordinal)
if ($before -lt 0 -or $bounded -lt 0 -or $after -lt 0 -or -not ($before -lt $bounded -and $bounded -lt $after)) {
    Fail "BoundedReactionLifecycle integration registration is absent or out of order."
}
if ([regex]::Matches($integration, 'new ScenarioDefinition\("BoundedReactionLifecycle"').Count -ne 1) {
    Fail "BoundedReactionLifecycle must be registered exactly once."
}

$fixturePath = Join-Path $Root "tools\mosaic-0.3c-bounded-reaction-fixtures.json"
$fixture = Get-Content -LiteralPath $fixturePath -Raw | ConvertFrom-Json
if ($fixture.schema -cne "mosaic.v38.bounded-reaction-fixture-firewall.v1") {
    Fail "Fixture firewall schema is wrong."
}
if (@($fixture.cases).Count -ne 20) { Fail "Fixture firewall must contain exactly 20 cases." }
$accepted = 0
$rejected = 0
foreach ($case in $fixture.cases) {
    $receipt = New-ValidReceipt
    if ($case.expected -ceq "accepted") {
        $result = Test-Receipt $receipt
    }
    elseif ($case.expected -ceq "rejected") {
        Apply-Mutation $receipt $case.mutation
        $result = Test-Receipt $receipt
    }
    else {
        Fail "Fixture has an unsupported expectation: $($case.id)"
    }
    if ($result -and $case.expected -ceq "accepted") { $accepted++ }
    elseif (-not $result -and $case.expected -ceq "rejected") { $rejected++ }
    else { Fail "Fixture expectation failed: $($case.id)" }
}
if ($accepted -ne 1 -or $rejected -ne 19) {
    Fail "Fixture firewall result was $accepted accepted / $rejected rejected."
}

Write-Host "PASS Mosaic 0.3C bounded reaction lifecycle source boundary."
Write-Host "PASS focused grounded-social-affect lifecycle tests: 26 registered."
Write-Host "PASS fixture firewall: 1 accepted / 19 rejected."
Write-Host "PASS authority boundary: provider=0 canonical=0 UI=0 runtime=0 pawn=0."
