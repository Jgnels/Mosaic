[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Root
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Fail([string]$Message) {
    throw "MOSAIC 0.3E DURABLE APPRAISAL SOURCE GATE FAIL: $Message"
}

$Root = [IO.Path]::GetFullPath($Root)
$required = @(
    "Dagmay.Core\Appraisal\DurableAppraisalAdmissionContracts.cs",
    "Dagmay.Core\Appraisal\DurableAppraisalAdmissionCoordinator.cs",
    "Dagmay.Core\Appraisal\DurableAppraisalOutboxPersistence.cs",
    "Dagmay.Core\Appraisal\ProvisionalDialogueAppraisalStore.cs",
    "Dagmay.Tests\Mosaic03EDurableAppraisalAdmissionContractTests.cs",
    "Dagmay.Tests\Fixtures\V40_SHARED_DETERMINISTIC_FIXTURES.json",
    "Dagmay.IntegrationHarness\DurableAppraisalAdmissionScenario.cs",
    "Dagmay.IntegrationHarness\IntegrationScenarios.cs",
    "docs\MOSAIC_0.3E_DURABLE_APPRAISAL_ADMISSION_BOUNDARY.md",
    "tools\verify-0.3e-durable-appraisal.ps1"
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $relative) -PathType Leaf)) {
        Fail "Missing required v40 file: $relative"
    }
}

$staticCs = @(
    Get-ChildItem -LiteralPath $Root -Recurse -File -Filter "*.cs" |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|artifacts)[\\/]' }
)
if ($staticCs.Count -ne 171) {
    Fail "Expected exactly 171 static C# files; found $($staticCs.Count)."
}

$fixturePath = Join-Path $Root "Dagmay.Tests\Fixtures\V40_SHARED_DETERMINISTIC_FIXTURES.json"
$fixtureHash = (Get-FileHash -LiteralPath $fixturePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($fixtureHash -ne "bc60bbde6ece4ab5853fac575819d0730e027cf7fd11f58dd3aa9eccbda9f490") {
    Fail "Shared deterministic fixture hash mismatch: $fixtureHash"
}

$contractsPath = Join-Path $Root "Dagmay.Core\Appraisal\DurableAppraisalAdmissionContracts.cs"
$coordinatorPath = Join-Path $Root "Dagmay.Core\Appraisal\DurableAppraisalAdmissionCoordinator.cs"
$persistencePath = Join-Path $Root "Dagmay.Core\Appraisal\DurableAppraisalOutboxPersistence.cs"
$v39StorePath = Join-Path $Root "Dagmay.Core\Appraisal\ProvisionalDialogueAppraisalStore.cs"
$contracts = Get-Content -LiteralPath $contractsPath -Raw
$coordinator = Get-Content -LiteralPath $coordinatorPath -Raw
$persistence = Get-Content -LiteralPath $persistencePath -Raw
$v39Store = Get-Content -LiteralPath $v39StorePath -Raw
$authoritySurface = $contracts + "`n" + $coordinator + "`n" + $persistence

foreach ($token in @(
    "mosaic.0.3e.checkpoint-aligned-durable-appraisal-admission.v1",
    "639e5b4ff2d7629fed5b76303b21bbcecbf03f167068d2d4046cb9ef1b153ce9",
    "Mosaic.Core.ProvisionalDialogueAppraisal.v1",
    "c8557ab218dc0a6136a960cfb495dd4853e5dd3ad81d8eadd7b36082b1b47819",
    "Mosaic.Core.CheckpointAlignedDialogueAdmission.v1",
    "Mosaic.Core.CheckpointCommitReceipt.v1",
    "MaximumPending = 64",
    "MaximumUnacknowledgedFailures = 64",
    "MaximumRecentTerminal = 64",
    "ByteCount = 1 << 20",
    "INTERNAL_CANONICAL_COORDINATOR_NO_PROVIDER_UI_PLANNER_ADAPTER_OR_PAWN_AUTHORITY"
)) {
    if (-not $authoritySurface.Contains($token)) {
        Fail "Missing compiled v40 contract token: $token"
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
    "new Random("
)) {
    if ($authoritySurface.Contains($forbidden)) {
        Fail "Forbidden provider, UI, runtime, adapter, pawn, or nondeterminism token: $forbidden"
    }
}

foreach ($privateDataToken in @("RawDialogueText", "DisplayLabel", "PlayerKnowledge", "PromptText")) {
    if ($authoritySurface.Contains($privateDataToken)) {
        Fail "Canonical/outbox surface contains forbidden private dialogue field: $privateDataToken"
    }
}

if (-not $contracts.Contains("internal static AdmittedDialogueEventReceipt CreateTrusted(") -or
    $contracts.Contains("public static AdmittedDialogueEventReceipt CreateTrusted(")) {
    Fail "Admitted-event trusted construction must exist and remain internal."
}
if (-not $contracts.Contains("internal static CheckpointCommitReceipt CreateTrusted(") -or
    $contracts.Contains("public static CheckpointCommitReceipt CreateTrusted(")) {
    Fail "Checkpoint trusted construction must exist and remain internal."
}
if (-not [regex]::IsMatch(
    $v39Store,
    "internal\s+ProvisionalDialogueAppraisal\s+RequirePromotionPendingPacket\(")) {
    Fail "The internal v39 proposal-attestation seam is missing."
}
$applyIndex = $coordinator.IndexOf(
    "public CanonicalDurableAdmissionEvidence Apply(",
    [StringComparison]::Ordinal)
$preflightIndex = $coordinator.IndexOf(
    "Complete conflict preflight under this same coordinator lock.",
    $applyIndex,
    [StringComparison]::Ordinal)
$firstWriteIndex = $coordinator.IndexOf(
    "_stores.RecordsById.Add(",
    $applyIndex,
    [StringComparison]::Ordinal)
$rereadIndex = $coordinator.IndexOf(
    "Post-write reread verification failed.",
    $firstWriteIndex,
    [StringComparison]::Ordinal)
if (-not $coordinator.Contains("lock (_gate)") -or
    $applyIndex -lt 0 -or $preflightIndex -lt 0 -or $firstWriteIndex -lt 0 -or $rereadIndex -lt 0 -or
    -not ($applyIndex -lt $preflightIndex -and $preflightIndex -lt $firstWriteIndex -and
        $firstWriteIndex -lt $rereadIndex)) {
    Fail "The single coordinator lock, complete preflight, or reread verification is missing."
}
if ($persistence.Contains("DevelopmentalRecords") -or $persistence.Contains("RecordsById")) {
    Fail "The outbox snapshot attempts to serialize the full canonical developmental ledger."
}

$testsPath = Join-Path $Root "Dagmay.Tests\Mosaic03EDurableAppraisalAdmissionContractTests.cs"
$tests = Get-Content -LiteralPath $testsPath -Raw
$focused = [regex]::Matches($tests, 'Add\(tests, "(test_[a-z0-9_]+)"')
if ($focused.Count -ne 58) {
    Fail "Expected exactly 58 focused v40 registrations; found $($focused.Count)."
}
$focusedNames = @($focused | ForEach-Object { $_.Groups[1].Value })
if (@($focusedNames | Sort-Object -Unique).Count -ne 58) {
    Fail "Focused v40 test registrations are not unique."
}
$unitResultNames = @(
    "test_affect_only_preserves_relationship_version",
    "test_all_crash_placements_converge",
    "test_apply_writes_all_destinations",
    "test_authority_firewall_detects_executable_forbidden_fields",
    "test_backup_snapshot_recovers_corrupt_primary",
    "test_both_corrupt_snapshots_fail",
    "test_canonical_record_contains_no_dialogue_text_or_player_knowledge",
    "test_checkpoint_receipt_fingerprint_fails",
    "test_clamping_records_smaller_actual_delta",
    "test_completed_retained_until_later_checkpoint",
    "test_conflict_preflight_writes_nothing_new",
    "test_conflicting_duplicate_is_rejected_after_registry_consumption",
    "test_crash_never_returns_success",
    "test_dependency_digest_is_corrected_v39",
    "test_deterministic_request_permutations",
    "test_diagnostic_projection_is_private",
    "test_duplicate_prepare_is_idempotent",
    "test_event_id_mismatch_fails",
    "test_event_receipt_fingerprint_fails",
    "test_foreign_lineage_changes_record_identity",
    "test_foreign_save_fails",
    "test_foreign_store_set_fails",
    "test_foreign_v39_gate_fails",
    "test_foreign_world_fails",
    "test_forged_proposal_packet_fingerprint_fails",
    "test_future_event_fails",
    "test_global_pending_queue_is_bounded_and_reopens_after_completion",
    "test_incomplete_checkpoint_fails",
    "test_issued_quarantine_compacts_only_after_later_checkpoint",
    "test_non_witness_receipt_cannot_be_constructed",
    "test_noop_admission_has_no_version_churn",
    "test_one_inflight_per_owner",
    "test_out_of_order_after_completion_fails",
    "test_outbox_snapshot_bytes_are_deterministic",
    "test_outbox_snapshot_does_not_duplicate_canonical_history",
    "test_outbox_snapshot_rejects_foreign_restored_store",
    "test_owner_mismatch_fails",
    "test_prepare_returns_attempt_not_success",
    "test_primary_snapshot_round_trip_preserves_pending_entry",
    "test_quarantined_failure_receipt_restores_v39_overlay",
    "test_read_only_checkpoint_fails",
    "test_registry_handoff_is_consumed_after_durable_prepare",
    "test_relationship_delta_without_state_fails_in_v39",
    "test_relationship_only_preserves_affect_version",
    "test_rename_does_not_change_identity_binding",
    "test_replay_does_not_double_count",
    "test_same_generation_substituted_checkpoint_cannot_apply",
    "test_snapshot_tampering_fails_hash",
    "test_source_authority_firewall",
    "test_stale_affect_fingerprint_fails",
    "test_stale_checkpoint_fails",
    "test_stale_relationship_fingerprint_fails",
    "test_terminal_duplicate_after_compaction_fails_closed",
    "test_unhealthy_store_fails",
    "test_unissued_failure_backlog_is_bounded_and_reopens_after_receipt",
    "test_unregistered_proposal_fails",
    "test_v37_v38_v39_v40_end_to_end",
    "test_v39_reconciles_only_after_completed_receipt"
)
foreach ($name in $unitResultNames) {
    if ($focusedNames -notcontains $name) {
        Fail "Missing exact focused test from v40-final-unit-run1.json: $name"
    }
}

$program = Get-Content -LiteralPath (Join-Path $Root "Dagmay.Tests\Program.cs") -Raw
if ([regex]::Matches($program, "Mosaic03EDurableAppraisalAdmissionContractTests.AddTo\(tests\)").Count -ne 1) {
    Fail "The v40 focused matrix must be registered exactly once."
}

$integration = Get-Content -LiteralPath (
    Join-Path $Root "Dagmay.IntegrationHarness\IntegrationScenarios.cs") -Raw
if ([regex]::Matches($integration, 'new ScenarioDefinition\("DurableAppraisalAdmission"').Count -ne 1 -or
    [regex]::Matches($integration, 'new ScenarioDefinition\(\s*"DurableAppraisalAdmissionStress"').Count -ne 1 -or
    -not $integration.Contains("DurableAppraisalAdmissionStressAsync(50_000)")) {
    Fail "The v37-v38-v39-v40 integration or fixed 50,000-admission stress executable is missing."
}

Write-Host "PASS Mosaic 0.3E durable-appraisal source boundary."
Write-Host "PASS static C# files: 171."
Write-Host "PASS focused v40 tests: 58 exact registrations."
Write-Host "PASS shared fixture SHA-256: $fixtureHash."
Write-Host "PASS bounds: pending=64 failures=64 recent-terminal=64 fixed-filter=1048576 bytes."
Write-Host "PASS authority boundary: provider=0 UI=0 planner=0 adapter=0 pawn=0."
Write-Host "PASS persistence boundary: outbox protocol only; canonical developmental ledger excluded."
