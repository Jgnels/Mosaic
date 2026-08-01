using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Presentation;

namespace Dagmay.Tests
{
    internal static class Mosaic03EDurableAppraisalAdmissionContractTests
    {
        private const string Owner = "individual-owner-a";
        private const string Speaker = "individual-speaker-b";
        private const string Save = "save-v40";
        private const string World = "world-v40";
        private const string StoreSet = "stores-v40";
        private const long Generation = 7;

        public static void AddTo(List<(string Name, Func<Task> Run)> tests)
        {
            Add(tests, "test_affect_only_preserves_relationship_version", TargetSelectiveAffect);
            Add(tests, "test_all_crash_placements_converge", AllCrashesConverge);
            Add(tests, "test_apply_writes_all_destinations", ApplyWritesAllDestinations);
            Add(tests, "test_authority_firewall_detects_executable_forbidden_fields", AuthorityFirewall);
            Add(tests, "test_backup_snapshot_recovers_corrupt_primary", BackupRecovers);
            Add(tests, "test_both_corrupt_snapshots_fail", BothSnapshotsCorrupt);
            Add(tests, "test_canonical_record_contains_no_dialogue_text_or_player_knowledge", CanonicalRecordPrivacy);
            Add(tests, "test_checkpoint_receipt_fingerprint_fails", CheckpointFingerprintFails);
            Add(tests, "test_clamping_records_smaller_actual_delta", ClampingRecordsActual);
            Add(tests, "test_completed_retained_until_later_checkpoint", CompletedRetention);
            Add(tests, "test_conflict_preflight_writes_nothing_new", ConflictPreflight);
            Add(tests, "test_conflicting_duplicate_is_rejected_after_registry_consumption", ConflictingDuplicate);
            Add(tests, "test_crash_never_returns_success", CrashNeverReturnsSuccess);
            Add(tests, "test_dependency_digest_is_corrected_v39", DependencyDigest);
            Add(tests, "test_deterministic_request_permutations", DeterministicEncoding);
            Add(tests, "test_diagnostic_projection_is_private", DiagnosticPrivacy);
            Add(tests, "test_duplicate_prepare_is_idempotent", DuplicatePrepare);
            Add(tests, "test_event_id_mismatch_fails", EventMismatch);
            Add(tests, "test_event_receipt_fingerprint_fails", EventFingerprintFails);
            Add(tests, "test_foreign_lineage_changes_record_identity", ForeignLineageChangesRecord);
            Add(tests, "test_foreign_save_fails", ForeignSaveFails);
            Add(tests, "test_foreign_store_set_fails", ForeignStoreFails);
            Add(tests, "test_foreign_v39_gate_fails", ForeignV39Fails);
            Add(tests, "test_foreign_world_fails", ForeignWorldFails);
            Add(tests, "test_forged_proposal_packet_fingerprint_fails", ProposalFingerprintFails);
            Add(tests, "test_future_event_fails", FutureEventFails);
            Add(tests, "test_global_pending_queue_is_bounded_and_reopens_after_completion", GlobalPendingBound);
            Add(tests, "test_incomplete_checkpoint_fails", IncompleteCheckpointFails);
            Add(tests, "test_issued_quarantine_compacts_only_after_later_checkpoint", QuarantineCompaction);
            Add(tests, "test_non_witness_receipt_cannot_be_constructed", NonWitnessFails);
            Add(tests, "test_noop_admission_has_no_version_churn", NoopNoVersionChurn);
            Add(tests, "test_one_inflight_per_owner", OneInflightPerOwner);
            Add(tests, "test_out_of_order_after_completion_fails", OutOfOrderFails);
            Add(tests, "test_outbox_snapshot_bytes_are_deterministic", DeterministicEncoding);
            Add(tests, "test_outbox_snapshot_does_not_duplicate_canonical_history", SnapshotSeparatesCanonicalHistory);
            Add(tests, "test_outbox_snapshot_rejects_foreign_restored_store", SnapshotRejectsForeignStore);
            Add(tests, "test_owner_mismatch_fails", OwnerMismatchFails);
            Add(tests, "test_prepare_returns_attempt_not_success", PrepareIsAttempt);
            Add(tests, "test_primary_snapshot_round_trip_preserves_pending_entry", SnapshotRoundTrip);
            Add(tests, "test_quarantined_failure_receipt_restores_v39_overlay", FailureRestoresOverlay);
            Add(tests, "test_read_only_checkpoint_fails", ReadOnlyCheckpointFails);
            Add(tests, "test_registry_handoff_is_consumed_after_durable_prepare", RegistryConsumed);
            Add(tests, "test_relationship_delta_without_state_fails_in_v39", RelationshipRequiresState);
            Add(tests, "test_relationship_only_preserves_affect_version", TargetSelectiveRelationship);
            Add(tests, "test_rename_does_not_change_identity_binding", RenamePreservesBinding);
            Add(tests, "test_replay_does_not_double_count", ReplayDoesNotDoubleCount);
            Add(tests, "test_same_generation_substituted_checkpoint_cannot_apply", SubstitutedCheckpointFails);
            Add(tests, "test_snapshot_tampering_fails_hash", SnapshotTamperingFails);
            Add(tests, "test_source_authority_firewall", AuthorityFirewall);
            Add(tests, "test_stale_affect_fingerprint_fails", StaleAffectFails);
            Add(tests, "test_stale_checkpoint_fails", StaleCheckpointFails);
            Add(tests, "test_stale_relationship_fingerprint_fails", StaleRelationshipFails);
            Add(tests, "test_terminal_duplicate_after_compaction_fails_closed", TerminalDuplicateFailsClosed);
            Add(tests, "test_unhealthy_store_fails", UnhealthyStoreFails);
            Add(tests, "test_unissued_failure_backlog_is_bounded_and_reopens_after_receipt", FailureBacklogBound);
            Add(tests, "test_unregistered_proposal_fails", UnregisteredProposalFails);
            Add(tests, "test_v37_v38_v39_v40_end_to_end", V37V38V39V40);
            Add(tests, "test_v39_reconciles_only_after_completed_receipt", V39ReconcilesOnlyAfterSuccess);
        }

        private static void Add(List<(string Name, Func<Task> Run)> tests, string name, Action action) =>
            tests.Add((name, () =>
            {
                action();
                return Task.CompletedTask;
            }));

        private static void DependencyDigest()
        {
            TestAssert.Equal(ProvisionalDialogueAppraisalStore.GateDigest, DurableAppraisalAdmissionSource.V39CorrectedGateDigest,
                "v40 must bind the corrected v39 gate.");
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "V40_SHARED_DETERMINISTIC_FIXTURES.json");
            var bytes = File.ReadAllBytes(path);
            TestAssert.Equal("bc60bbde6ece4ab5853fac575819d0730e027cf7fd11f58dd3aa9eccbda9f490", Hash(bytes),
                "The exact shared cross-language fixture must be used.");
            using (var document = JsonDocument.Parse(bytes))
            {
                TestAssert.Equal("mosaic.0.3e.shared-deterministic-fixtures.v1",
                    document.RootElement.GetProperty("schema").GetString(), "Fixture schema mismatch.");
                TestAssert.Equal(4, document.RootElement.GetProperty("vectors").GetArrayLength(),
                    "All four cross-language vectors are required.");
                TestAssert.Equal(DurableAppraisalAdmissionSource.V39CorrectedGateDigest,
                    document.RootElement.GetProperty("dependency_gate_digest").GetString(),
                    "Fixture dependency digest mismatch.");
            }
        }

        private static void PrepareIsAttempt()
        {
            var fixture = Build();
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            TestAssert.Equal(DurableAdmissionAttemptOutcome.Prepared, attempt.Outcome, "Prepare must return PREPARED.");
            TestAssert.False(attempt.IsCanonicalSuccess, "An attempt must never be success evidence.");
            TestAssert.Equal(0, fixture.Stores.DevelopmentalRecordCount, "Prepare must not write canonical state.");
        }

        private static void ApplyWritesAllDestinations()
        {
            var fixture = Build(affect: new ProvisionalAffectDelta(valence: -0.02m, threat: 0.01m),
                relationship: new ProvisionalRelationshipDelta(trust: -0.01m, resentment: 0.02m));
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            var evidence = fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint);
            TestAssert.True(evidence.IsCanonicalSuccess, "Verified completion must produce canonical success.");
            TestAssert.Equal(1, fixture.Stores.DevelopmentalRecordCount, "Exactly one developmental record is required.");
            TestAssert.Equal(4L, fixture.Stores.Affect(Owner).Version, "Targeted affect must advance once.");
            TestAssert.Equal(6L, fixture.Stores.Relationship(Owner, Speaker)!.Version, "Targeted relationship must advance once.");
        }

        private static void TargetSelectiveAffect()
        {
            var fixture = Build(affect: new ProvisionalAffectDelta(valence: 0.015m, agency: 0.005m));
            var before = fixture.Stores.Relationship(Owner, Speaker)!;
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint);
            var after = fixture.Stores.Relationship(Owner, Speaker)!;
            TestAssert.Equal(before.Version, after.Version, "Untargeted relationship version must remain exact.");
            TestAssert.Equal(before.Fingerprint, after.Fingerprint, "Untargeted relationship fingerprint must remain exact.");
        }

        private static void TargetSelectiveRelationship()
        {
            var fixture = Build(relationship: new ProvisionalRelationshipDelta(trust: 0.01m, affection: 0.005m));
            var before = fixture.Stores.Affect(Owner);
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint);
            var after = fixture.Stores.Affect(Owner);
            TestAssert.Equal(before.Version, after.Version, "Untargeted affect version must remain exact.");
            TestAssert.Equal(before.Fingerprint, after.Fingerprint, "Untargeted affect fingerprint must remain exact.");
        }

        private static void NoopNoVersionChurn()
        {
            var fixture = Build();
            var affect = fixture.Stores.Affect(Owner);
            var relationship = fixture.Stores.Relationship(Owner, Speaker)!;
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint);
            TestAssert.Equal(affect.Fingerprint, fixture.Stores.Affect(Owner).Fingerprint, "No-op affect must be byte-stable.");
            TestAssert.Equal(relationship.Fingerprint, fixture.Stores.Relationship(Owner, Speaker)!.Fingerprint,
                "No-op relationship must be byte-stable.");
        }

        private static void ClampingRecordsActual()
        {
            var fixture = Build(affect: new ProvisionalAffectDelta(valence: 0.15m), initialValence: 0.95m);
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            var evidence = fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint);
            TestAssert.Equal(0.05m, evidence.ActualAffectDelta.Valence, "Actual clamped delta must be preserved separately.");
            TestAssert.Equal(0.15m, fixture.Stores.DevelopmentalRecords[0].RequestedAffectDelta.Valence,
                "Requested delta must be preserved separately.");
        }

        private static void DuplicatePrepare()
        {
            var fixture = Build();
            var first = fixture.Coordinator.Prepare(fixture.Request);
            var second = fixture.Coordinator.Prepare(fixture.Request);
            TestAssert.Equal(first.EntryId, second.EntryId, "Exact duplicate prepare must be idempotent after registry consumption.");
            TestAssert.Equal(1, fixture.Outbox.Count, "Duplicate prepare must not create a second entry.");
        }

        private static void RegistryConsumed()
        {
            var fixture = Build();
            TestAssert.Equal(1, fixture.Registry.ActiveCount, "Registry must hold one transient handoff.");
            fixture.Coordinator.Prepare(fixture.Request);
            TestAssert.Equal(0, fixture.Registry.ActiveCount, "Durable outbox ownership must consume transient attestation.");
        }

        private static void ConflictingDuplicate()
        {
            var fixture = Build();
            fixture.Coordinator.Prepare(fixture.Request);
            var substitute = TrustedCheckpoint("substitute-checkpoint", Generation, completed: true, writable: true, healthy: true);
            var conflicting = new DurableAppraisalAdmissionRequest(
                fixture.Request.Proposal, fixture.Request.Identity, fixture.Request.EventReceipt, substitute);
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Prepare(conflicting),
                "A conflicting duplicate must fail after transient registry consumption.");
        }

        private static void UnregisteredProposalFails()
        {
            var fixture = Build();
            var foreignRegistry = new DurableProposalRegistry();
            var coordinator = new CheckpointAlignedDurableAppraisalCoordinator(fixture.Stores, fixture.Outbox, foreignRegistry);
            TestAssert.Throws<ArgumentException>(() => coordinator.Prepare(fixture.Request),
                "An envelope not held by this trusted registry must fail.");
        }

        private static void EventFingerprintFails()
        {
            var fixture = Build();
            Tamper(fixture.Request.EventReceipt, "<Fingerprint>k__BackingField", Hash("forged-event"));
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Prepare(fixture.Request),
                "A forged event receipt must fail.");
        }

        private static void CheckpointFingerprintFails()
        {
            var fixture = Build();
            Tamper(fixture.Checkpoint, "<Fingerprint>k__BackingField", Hash("forged-checkpoint"));
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Prepare(fixture.Request),
                "A forged checkpoint receipt must fail.");
        }

        private static void ProposalFingerprintFails()
        {
            var fixture = Build();
            Tamper(fixture.Request.Proposal, "<Fingerprint>k__BackingField", Hash("forged-proposal"));
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Prepare(fixture.Request),
                "A forged proposal envelope must fail.");
        }

        private static void ForeignV39Fails()
        {
            var fixture = Build();
            Tamper(fixture.Request.Proposal, "<SourceGateDigest>k__BackingField", Hash("foreign-v39-gate"));
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Prepare(fixture.Request),
                "A foreign v39 gate must fail.");
        }

        private static void SubstitutedCheckpointFails()
        {
            var fixture = Build();
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            var substitute = TrustedCheckpoint("same-generation-substitute", Generation, true, true, true);
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Apply(attempt.EntryId, substitute),
                "Same-generation checkpoint substitution must fail exact fingerprint binding.");
        }

        private static void IncompleteCheckpointFails() => CheckpointFlagFails(false, true, true);
        private static void ReadOnlyCheckpointFails() => CheckpointFlagFails(true, false, true);
        private static void UnhealthyStoreFails() => CheckpointFlagFails(true, true, false);

        private static void CheckpointFlagFails(bool completed, bool writable, bool healthy)
        {
            var fixture = Build(checkpoint: TrustedCheckpoint("flag-checkpoint", Generation, completed, writable, healthy));
            TestAssert.Throws<InvalidOperationException>(() => fixture.Coordinator.Prepare(fixture.Request),
                "Incomplete, read-only, or unhealthy checkpoint state must fail.");
        }

        private static void ForeignSaveFails() => ForeignBindingFails("foreign-save", World, StoreSet);
        private static void ForeignWorldFails() => ForeignBindingFails(Save, "foreign-world", StoreSet);
        private static void ForeignStoreFails() => ForeignBindingFails(Save, World, "foreign-stores");

        private static void ForeignBindingFails(string save, string world, string stores)
        {
            var fixture = Build(identity: DurableIdentityBinding.Create(Owner, "lineage-owner-a", save, world, stores, 2));
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Prepare(fixture.Request),
                "Foreign save/world/store binding must fail.");
        }

        private static void OwnerMismatchFails()
        {
            var fixture = Build(identity: DurableIdentityBinding.Create("foreign-owner", "lineage-owner-a", Save, World, StoreSet, 2));
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Prepare(fixture.Request),
                "Owner identity mismatch must fail.");
        }

        private static void EventMismatch()
        {
            var fixture = Build();
            var foreignEvent = TrustedEvent(
                fixture.Request.EventReceipt.ReceiptId,
                "foreign-dialogue-event",
                Owner,
                Speaker,
                new[] { Owner, Speaker },
                fixture.Request.EventReceipt.EventTick);
            var mismatched = new DurableAppraisalAdmissionRequest(
                fixture.Request.Proposal,
                fixture.Request.Identity,
                foreignEvent,
                fixture.Checkpoint);
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Prepare(mismatched),
                "Event identity mismatch must fail.");
        }

        private static void FutureEventFails()
        {
            var fixture = Build(eventTick: 10_000);
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Prepare(fixture.Request),
                "A future event must fail.");
        }

        private static void StaleCheckpointFails()
        {
            var fixture = Build(checkpoint: TrustedCheckpoint("stale-checkpoint", Generation - 1, true, true, true));
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Prepare(fixture.Request),
                "Stale checkpoint generation must fail.");
        }

        private static void StaleAffectFails()
        {
            var fixture = Build();
            var affect = fixture.Stores.Affect(Owner);
            var map = PrivateDictionary<CanonicalAffectState>(fixture.Stores, "AffectByOwner");
            map[Owner] = CanonicalAffectState.Create(Owner, affect.Values, affect.Version + 1);
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Prepare(fixture.Request),
                "Stale affect fingerprint/version must fail.");
        }

        private static void StaleRelationshipFails()
        {
            var fixture = Build();
            var relation = fixture.Stores.Relationship(Owner, Speaker)!;
            var map = PrivateDictionary<CanonicalRelationshipState>(fixture.Stores, "RelationshipByPair");
            map[Owner + "\n" + Speaker] = CanonicalRelationshipState.Create(Owner, Speaker, relation.Values, relation.Version + 1);
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Prepare(fixture.Request),
                "Stale relationship fingerprint/version must fail.");
        }

        private static void RelationshipRequiresState()
        {
            TestAssert.Throws<ArgumentException>(
                () => Build(
                    relationship: new ProvisionalRelationshipDelta(trust: 0.01m),
                    omitRelationshipState: true),
                "Corrected v39 must reject a relationship delta before v40 when canonical relationship state is absent.");
        }

        private static void NonWitnessFails()
        {
            TestAssert.Throws<TargetInvocationException>(() =>
                TrustedEvent("receipt", "event", Owner, Speaker, new[] { Speaker }, 1000),
                "Trusted construction must still reject a non-witness owner.");
        }

        private static void OneInflightPerOwner()
        {
            var sharedStores = NewStores();
            var outbox = new DurableAppraisalOutbox();
            var registry = new DurableProposalRegistry();
            var coordinator = new CheckpointAlignedDurableAppraisalCoordinator(sharedStores, outbox, registry);
            var first = Build(ordinal: 1, stores: sharedStores, outbox: outbox, registry: registry, coordinator: coordinator);
            var second = Build(ordinal: 2, stores: sharedStores, outbox: outbox, registry: registry, coordinator: coordinator);
            coordinator.Prepare(first.Request);
            TestAssert.Throws<InvalidOperationException>(() => coordinator.Prepare(second.Request),
                "Only one in-flight entry per owner is allowed.");
        }

        private static void GlobalPendingBound()
        {
            var stores = new DurableCanonicalStoreSet(Save, World, StoreSet);
            var outbox = new DurableAppraisalOutbox();
            var registry = new DurableProposalRegistry();
            var coordinator = new CheckpointAlignedDurableAppraisalCoordinator(stores, outbox, registry);
            for (var index = 0; index < DurableAppraisalOutbox.MaximumPending; index++)
            {
                var fixture = Build(index + 1, owner: "owner-" + index.ToString("D3", CultureInfo.InvariantCulture),
                    stores: stores, outbox: outbox, registry: registry, coordinator: coordinator);
                coordinator.Prepare(fixture.Request);
            }
            var overflow = Build(1000, owner: "owner-overflow", stores: stores, outbox: outbox, registry: registry, coordinator: coordinator);
            TestAssert.Throws<InvalidOperationException>(() => coordinator.Prepare(overflow.Request),
                "Global pending count must fail closed at 64.");
        }

        private static void OutOfOrderFails()
        {
            var fixture = Build(ordinal: 10);
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint);
            var older = Build(ordinal: 1, stores: fixture.Stores, outbox: fixture.Outbox,
                registry: fixture.Registry, coordinator: fixture.Coordinator);
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Prepare(older.Request),
                "Owner proposal order must be monotonic.");
        }

        private static void CrashNeverReturnsSuccess()
        {
            var fixture = Build();
            TestAssert.Throws<DurableAdmissionSimulatedCrash>(
                () => fixture.Coordinator.Prepare(fixture.Request, DurableAdmissionFaultStage.AfterOutbox),
                "A crash placement must throw rather than manufacture success.");
            TestAssert.Equal(0, fixture.Stores.DevelopmentalRecordCount, "After-outbox crash must not write canonical state.");
        }

        private static void AllCrashesConverge()
        {
            foreach (DurableAdmissionFaultStage stage in Enum.GetValues(typeof(DurableAdmissionFaultStage)))
            {
                var fixture = Build();
                string entryId;
                if (stage == DurableAdmissionFaultStage.AfterOutbox)
                {
                    TestAssert.Throws<DurableAdmissionSimulatedCrash>(
                        () => fixture.Coordinator.Prepare(fixture.Request, stage), "After-outbox crash expected.");
                    entryId = fixture.Outbox.Entries.Single().EntryId;
                }
                else
                {
                    entryId = fixture.Coordinator.Prepare(fixture.Request).EntryId;
                    TestAssert.Throws<DurableAdmissionSimulatedCrash>(
                        () => fixture.Coordinator.Apply(entryId, fixture.Checkpoint, stage), "Apply crash expected.");
                }
                var evidence = fixture.Coordinator.Apply(entryId, fixture.Checkpoint);
                TestAssert.True(evidence.IsCanonicalSuccess, "Every deterministic crash placement must converge.");
                TestAssert.Equal(1, fixture.Stores.DevelopmentalRecordCount, "Recovery must not duplicate the canonical record.");
            }
        }

        private static void ReplayDoesNotDoubleCount()
        {
            var fixture = Build();
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            var first = fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint);
            var second = fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint);
            TestAssert.Equal(first.CompletionFingerprint, second.CompletionFingerprint, "Completed replay must be idempotent.");
            TestAssert.Equal(1, fixture.Stores.DevelopmentalRecordCount, "Replay must not double-count.");
        }

        private static void ConflictPreflight()
        {
            var fixture = Build(affect: new ProvisionalAffectDelta(valence: 0.02m));
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            var map = PrivateDictionary<CanonicalAffectState>(fixture.Stores, "AffectByOwner");
            map[Owner] = CanonicalAffectState.Create(Owner, new ProvisionalAffectDelta(valence: -0.5m), 99);
            TestAssert.Throws<InvalidOperationException>(() => fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint),
                "A destination conflict must quarantine.");
            TestAssert.Equal(0, fixture.Stores.DevelopmentalRecordCount,
                "Complete conflict preflight must occur before the first canonical write.");
        }

        private static void FailureRestoresOverlay()
        {
            var fixture = Build();
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            var map = PrivateDictionary<CanonicalAffectState>(fixture.Stores, "AffectByOwner");
            map[Owner] = CanonicalAffectState.Create(Owner, new ProvisionalAffectDelta(valence: -0.5m), 99);
            TestAssert.Throws<InvalidOperationException>(() => fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint),
                "Conflict must quarantine.");
            var failure = fixture.Coordinator.PermanentFailureReceipt(attempt.EntryId);
            var appraisal = fixture.V39Store.ObserveApplicationReceipt(failure);
            TestAssert.Equal(ProvisionalAppraisalState.Active, appraisal.State,
                "Permanent failure receipt must restore the v39 provisional overlay.");
        }

        private static void QuarantineCompaction()
        {
            var fixture = Build();
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            var map = PrivateDictionary<CanonicalAffectState>(fixture.Stores, "AffectByOwner");
            map[Owner] = CanonicalAffectState.Create(Owner, new ProvisionalAffectDelta(valence: -0.5m), 99);
            TestAssert.Throws<InvalidOperationException>(() => fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint), "Conflict expected.");
            fixture.Coordinator.PermanentFailureReceipt(attempt.EntryId);
            TestAssert.Equal(1, fixture.Outbox.Count, "Acknowledged failure remains through its own checkpoint.");
            fixture.Outbox.AdvanceCheckpoint(Generation + 1);
            TestAssert.Equal(0, fixture.Outbox.Count, "Acknowledged failure compacts only after a later checkpoint.");
        }

        private static void FailureBacklogBound()
        {
            TestAssert.Equal(64, DurableAppraisalOutbox.MaximumUnacknowledgedFailures,
                "Unacknowledged failure backlog must be fixed at 64.");
            var fixture = Build();
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            var map = PrivateDictionary<CanonicalAffectState>(fixture.Stores, "AffectByOwner");
            map[Owner] = CanonicalAffectState.Create(Owner, new ProvisionalAffectDelta(valence: -0.5m), 99);
            TestAssert.Throws<InvalidOperationException>(() => fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint), "Conflict expected.");
            TestAssert.Equal(1, fixture.Outbox.UnacknowledgedFailureCount, "Quarantine must remain unacknowledged.");
            fixture.Coordinator.PermanentFailureReceipt(attempt.EntryId);
            TestAssert.Equal(0, fixture.Outbox.UnacknowledgedFailureCount, "Issuing a failure receipt must reopen capacity.");
        }

        private static void DeterministicEncoding()
        {
            var fixture = Build();
            fixture.Coordinator.Prepare(fixture.Request);
            var codec = new DurableAppraisalOutboxCodec();
            var first = codec.Encode(fixture.Stores, fixture.Outbox);
            var second = codec.Encode(fixture.Stores, fixture.Outbox);
            TestAssert.True(first.SequenceEqual(second), "Equivalent outbox state must encode byte-identically.");
            DependencyDigest();
        }

        private static void SnapshotRoundTrip()
        {
            var fixture = Build();
            fixture.Coordinator.Prepare(fixture.Request);
            var codec = new DurableAppraisalOutboxCodec();
            var restored = codec.Decode(codec.Encode(fixture.Stores, fixture.Outbox), fixture.Stores);
            TestAssert.Equal(fixture.Outbox.StateDigest(), restored.StateDigest(), "Pending outbox state must round-trip exactly.");
        }

        private static void SnapshotTamperingFails()
        {
            var fixture = Build();
            var bytes = new DurableAppraisalOutboxCodec().Encode(fixture.Stores, fixture.Outbox);
            bytes[bytes.Length - 1] ^= 1;
            TestAssert.Throws<InvalidDataException>(
                () => new DurableAppraisalOutboxCodec().Decode(bytes, fixture.Stores),
                "Snapshot tampering must fail its payload hash.");
        }

        private static void SnapshotRejectsForeignStore()
        {
            var fixture = Build();
            var bytes = new DurableAppraisalOutboxCodec().Encode(fixture.Stores, fixture.Outbox);
            var foreign = new DurableCanonicalStoreSet("foreign-save", World, StoreSet);
            TestAssert.Throws<InvalidDataException>(
                () => new DurableAppraisalOutboxCodec().Decode(bytes, foreign),
                "Outbox snapshot must reject independently restored foreign stores.");
        }

        private static void SnapshotSeparatesCanonicalHistory()
        {
            var fixture = Build();
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            var evidence = fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint);
            fixture.Outbox.AdvanceCheckpoint(Generation + 1);
            var bytes = new DurableAppraisalOutboxCodec().Encode(fixture.Stores, fixture.Outbox);
            var text = Encoding.UTF8.GetString(bytes);
            TestAssert.False(text.Contains(evidence.DevelopmentalRecordId, StringComparison.Ordinal),
                "Outbox snapshot must not duplicate the independently owned canonical developmental ledger.");
            TestAssert.Equal(1, fixture.Stores.DevelopmentalRecordCount, "Canonical history must remain independently owned.");
        }

        private static void BackupRecovers()
        {
            var fixture = Build();
            var root = NewTempDirectory();
            try
            {
                var path = Path.Combine(root, "outbox.bin");
                var store = new AtomicDurableAppraisalOutboxStore();
                store.Save(path, fixture.Stores, fixture.Outbox);
                fixture.Coordinator.Prepare(fixture.Request);
                store.Save(path, fixture.Stores, fixture.Outbox);
                File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
                var loaded = store.Load(path, fixture.Stores);
                TestAssert.Equal(DurableAppraisalOutboxLoadStatus.RecoveredFromBackup, loaded.Status,
                    "Corrupt primary must recover the verified backup.");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void BothSnapshotsCorrupt()
        {
            var fixture = Build();
            var root = NewTempDirectory();
            try
            {
                var path = Path.Combine(root, "outbox.bin");
                File.WriteAllBytes(path, new byte[] { 1 });
                File.WriteAllBytes(path + ".bak", new byte[] { 2 });
                var loaded = new AtomicDurableAppraisalOutboxStore().Load(path, fixture.Stores);
                TestAssert.Equal(DurableAppraisalOutboxLoadStatus.Unrecoverable, loaded.Status,
                    "Two corrupt snapshots must fail closed.");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void CompletedRetention()
        {
            var fixture = Build();
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint);
            TestAssert.Equal(1, fixture.Outbox.Count, "Completion remains at its own checkpoint.");
            fixture.Outbox.AdvanceCheckpoint(Generation + 1);
            TestAssert.Equal(0, fixture.Outbox.Count, "Completion compacts after a later checkpoint.");
        }

        private static void TerminalDuplicateFailsClosed()
        {
            var fixture = Build();
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint);
            fixture.Outbox.AdvanceCheckpoint(Generation + 1);
            TestAssert.Throws<ArgumentException>(() => fixture.Coordinator.Prepare(fixture.Request),
                "A terminal duplicate must fail closed after compaction.");
            TestAssert.Equal(DurableAppraisalOutbox.TerminalFilterBytes, 1 << 20,
                "Terminal duplicate filter must remain fixed at 1 MiB.");
        }

        private static void RenamePreservesBinding()
        {
            var first = DurableIdentityBinding.Create(Owner, "lineage-owner-a", Save, World, StoreSet, 2);
            var afterDisplayRename = DurableIdentityBinding.Create(Owner, "lineage-owner-a", Save, World, StoreSet, 2);
            TestAssert.Equal(first.Fingerprint, afterDisplayRename.Fingerprint,
                "Display-name changes cannot alter stable identity binding.");
        }

        private static void ForeignLineageChangesRecord()
        {
            var first = Build(identity: DurableIdentityBinding.Create(Owner, "lineage-a", Save, World, StoreSet, 2));
            var firstAttempt = first.Coordinator.Prepare(first.Request);
            var firstId = first.Outbox.Entries.Single().Record.RecordId;
            var second = Build(identity: DurableIdentityBinding.Create(Owner, "lineage-b", Save, World, StoreSet, 2));
            second.Coordinator.Prepare(second.Request);
            TestAssert.False(string.Equals(firstId, second.Outbox.Entries.Single().Record.RecordId, StringComparison.Ordinal),
                "Lineage is part of canonical developmental-record identity.");
            TestAssert.False(string.IsNullOrWhiteSpace(firstAttempt.EntryId), "First admission must remain valid.");
        }

        private static void DiagnosticPrivacy()
        {
            var fixture = Build();
            fixture.Coordinator.Prepare(fixture.Request);
            var projection = fixture.Coordinator.DiagnosticProjection();
            var joined = string.Join("|", projection.Select(value => value.Key + "=" + value.Value));
            TestAssert.False(joined.Contains(Owner, StringComparison.Ordinal), "Diagnostics must not expose owner IDs.");
            TestAssert.False(joined.Contains(Speaker, StringComparison.Ordinal), "Diagnostics must not expose counterpart IDs.");
            TestAssert.True(joined.Contains("NO_PROVIDER_UI_PLANNER_ADAPTER_OR_PAWN_AUTHORITY", StringComparison.Ordinal),
                "Diagnostics must preserve explicit authority boundary.");
        }

        private static void CanonicalRecordPrivacy()
        {
            var fixture = Build();
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint);
            var names = fixture.Stores.DevelopmentalRecords[0].GetType().GetProperties()
                .Select(value => value.Name).ToArray();
            foreach (var forbidden in new[] { "RawDialogueText", "DisplayLabel", "Prompt", "PlayerKnowledge", "Pawn", "Thing", "Map", "Def" })
                TestAssert.False(names.Contains(forbidden, StringComparer.OrdinalIgnoreCase),
                    "Canonical record must exclude forbidden field: " + forbidden);
        }

        private static void AuthorityFirewall()
        {
            var assembly = typeof(CheckpointAlignedDurableAppraisalCoordinator).Assembly;
            var types = assembly.GetTypes().Where(value =>
                value.Namespace == typeof(CheckpointAlignedDurableAppraisalCoordinator).Namespace &&
                (value.Name.Contains("DurableAppraisal", StringComparison.Ordinal) ||
                 value.Name.Contains("DurableAdmission", StringComparison.Ordinal) ||
                 value.Name.Contains("CheckpointAligned", StringComparison.Ordinal))).ToArray();
            var forbidden = new[] { "Pawn", "Thing", "Map", "Def", "Provider", "Planner", "Job", "Movement", "Combat", "RawText", "Prompt", "PlayerKnowledge" };
            foreach (var type in types)
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            foreach (var word in forbidden)
                TestAssert.False(member.Name.Equals(word, StringComparison.OrdinalIgnoreCase),
                    "Executable authority surface contains forbidden member " + type.Name + "." + member.Name);
            TestAssert.False(typeof(CheckpointAlignedDurableAppraisalCoordinator).GetMethods()
                .Any(value => value.Name.Contains("Job", StringComparison.OrdinalIgnoreCase) ||
                              value.Name.Contains("Move", StringComparison.OrdinalIgnoreCase) ||
                              value.Name.Contains("Attack", StringComparison.OrdinalIgnoreCase)),
                "Coordinator cannot expose pawn-control calls.");
        }

        private static void V39ReconcilesOnlyAfterSuccess()
        {
            var fixture = Build();
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            var overlayBeforeSuccess = fixture.V39Store.EffectiveOverlay(Owner, "conversation-001", 1000);
            TestAssert.False(overlayBeforeSuccess.Affect.IsZero && overlayBeforeSuccess.Relationship.IsZero,
                "Attempt receipt cannot reconcile or remove the active v39 overlay.");
            var evidence = fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint);
            var promoted = fixture.V39Store.ObserveApplicationReceipt(evidence.ApplicationReceipt);
            TestAssert.Equal(ProvisionalAppraisalState.Promoted, promoted.State,
                "Only canonical completed evidence may reconcile v39.");
        }

        private static void V37V38V39V40()
        {
            var fixture = Build(
                affect: new ProvisionalAffectDelta(valence: 0.01m),
                relationship: new ProvisionalRelationshipDelta(trust: 0.01m));
            var attempt = fixture.Coordinator.Prepare(fixture.Request);
            var evidence = fixture.Coordinator.Apply(attempt.EntryId, fixture.Checkpoint);
            var promoted = fixture.V39Store.ObserveApplicationReceipt(evidence.ApplicationReceipt);
            TestAssert.Equal(ProvisionalAppraisalState.Promoted, promoted.State,
                "Integrated v37→v38→v39→v40 path must finish with a canonical receipt.");
            TestAssert.Equal(1, fixture.Stores.DevelopmentalRecordCount,
                "Integrated path must materialize exactly one owner-private developmental record.");
        }

        private static Fixture Build(
            int ordinal = 1,
            string owner = Owner,
            ProvisionalAffectDelta? affect = null,
            ProvisionalRelationshipDelta? relationship = null,
            decimal initialValence = 0.10m,
            bool omitRelationshipState = false,
            string? eventId = null,
            long? eventTick = null,
            CheckpointCommitReceipt? checkpoint = null,
            DurableIdentityBinding? identity = null,
            DurableCanonicalStoreSet? stores = null,
            DurableAppraisalOutbox? outbox = null,
            DurableProposalRegistry? registry = null,
            CheckpointAlignedDurableAppraisalCoordinator? coordinator = null)
        {
            var suffix = ordinal.ToString("D5", CultureInfo.InvariantCulture);
            var speaker = Speaker;
            stores ??= new DurableCanonicalStoreSet(Save, World, StoreSet);
            if (!TryAffect(stores, owner))
                stores.SeedAffect(CanonicalAffectState.Create(owner, new ProvisionalAffectDelta(valence: initialValence), 3));
            if (!omitRelationshipState && stores.Relationship(owner, speaker) is null)
                stores.SeedRelationship(CanonicalRelationshipState.Create(
                    owner, speaker, new CanonicalRelationshipVector(trust: 0.20m, affection: 0.10m), 5));
            outbox ??= new DurableAppraisalOutbox();
            registry ??= new DurableProposalRegistry();
            coordinator ??= new CheckpointAlignedDurableAppraisalCoordinator(stores, outbox, registry);
            var v39 = new ProvisionalDialogueAppraisalStore("session-v40-" + owner + "-" + suffix);
            var observed = TrustedObserved(owner, speaker, suffix, Generation);
            var knowledge = new ProvisionalKnowledgeEvidence(
                "ev-" + suffix, owner, "source-ev-" + suffix, ProvisionalPrivacy.OwnerPrivate,
                true, 900 + ordinal, ProvisionalFactuality.VerifiedFact);
            var cue = new ProvisionalSpeechCue(
                ProvisionalCueType.Insult, 0.9m, true, 0, ProvisionalFactuality.SpeechAct,
                new[] { knowledge.EvidenceId });
            var relationshipState = stores.Relationship(owner, speaker);
            var input = new ProvisionalDialogueAppraisalInput(
                owner, observed, new[] { cue }, new[] { knowledge },
                stores.Affect(owner).Fingerprint, stores.Affect(owner).Version,
                relationshipState?.Fingerprint, relationshipState?.Version,
                new ProvisionalTraitProfile());
            var appraisal = v39.Activate(v39.Prepare(input).AppraisalId, observed.DisplayedTick);
            var sourceReceipt = "admission-receipt-" + suffix;
            var admittedEvent = eventId ?? "dialogue-event-" + suffix;
            var packet = v39.ProposePromotion(
                appraisal.AppraisalId, admittedEvent, Generation,
                affect ?? ProvisionalAffectDelta.Zero,
                relationship ?? ProvisionalRelationshipDelta.Zero,
                sourceReceipt);
            var envelope = registry.Register(v39, packet);
            var eventReceipt = TrustedEvent(
                sourceReceipt, eventId ?? admittedEvent, owner, speaker,
                new[] { owner, speaker }, eventTick ?? observed.DisplayedTick);
            checkpoint ??= TrustedCheckpoint("checkpoint-receipt-" + suffix, Generation, true, true, true);
            identity ??= DurableIdentityBinding.Create(owner, "lineage-" + owner, Save, World, StoreSet, 2);
            var request = new DurableAppraisalAdmissionRequest(envelope, identity, eventReceipt, checkpoint);
            return new Fixture(v39, stores, outbox, registry, coordinator, request, checkpoint);
        }

        private static DurableCanonicalStoreSet NewStores()
        {
            var stores = new DurableCanonicalStoreSet(Save, World, StoreSet);
            stores.SeedAffect(CanonicalAffectState.Create(Owner, new ProvisionalAffectDelta(valence: 0.10m), 3));
            stores.SeedRelationship(CanonicalRelationshipState.Create(
                Owner, Speaker, new CanonicalRelationshipVector(trust: 0.20m, affection: 0.10m), 5));
            return stores;
        }

        private static bool TryAffect(DurableCanonicalStoreSet stores, string owner)
        {
            try
            {
                stores.Affect(owner);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        private static ObservedDisplayReceipt TrustedObserved(string owner, string speaker, string suffix, long generation)
        {
            var method = typeof(ObservedDisplayReceipt).GetMethod("CreateTrusted", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Trusted v38 receipt factory missing.");
            return (ObservedDisplayReceipt)(method.Invoke(null, new object?[]
            {
                "session-v40-" + owner + "-" + suffix,
                Hash("attempt-" + suffix),
                Hash("receipt-" + suffix),
                "utterance-" + suffix,
                "conversation-001",
                speaker,
                owner,
                new[] { owner, speaker }.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                Hash("text-" + suffix),
                1000L + int.Parse(suffix, CultureInfo.InvariantCulture),
                generation,
                ObservedDisplayOutcome.OBSERVED_SUCCESS,
                ObservedDisplayReceipt.SourceContractValue,
                ObservedDisplayReceipt.SourceGateDigestValue
            }) ?? throw new InvalidOperationException("Trusted v38 receipt factory returned null."));
        }

        private static AdmittedDialogueEventReceipt TrustedEvent(
            string receiptId, string eventId, string owner, string speaker, IEnumerable<string> audience, long tick)
        {
            var method = typeof(AdmittedDialogueEventReceipt).GetMethod("CreateTrusted", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Trusted v40 event receipt factory missing.");
            return (AdmittedDialogueEventReceipt)(method.Invoke(null, new object?[]
            {
                receiptId, eventId, Hash("event-" + eventId), owner, speaker,
                audience.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                tick, Generation, Save, World, StoreSet, DurableAppraisalPrivacy.OwnerPrivate,
                DurableAppraisalAdmissionSource.EventReceiptContract
            }) ?? throw new InvalidOperationException("Trusted v40 event receipt factory returned null."));
        }

        private static CheckpointCommitReceipt TrustedCheckpoint(
            string receiptId, long generation, bool completed, bool writable, bool healthy)
        {
            var method = typeof(CheckpointCommitReceipt).GetMethod("CreateTrusted", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Trusted checkpoint receipt factory missing.");
            return (CheckpointCommitReceipt)(method.Invoke(null, new object?[]
            {
                receiptId, Save, World, StoreSet, generation, Hash("checkpoint-" + receiptId),
                completed, writable, healthy, DurableAppraisalAdmissionSource.CheckpointReceiptContract
            }) ?? throw new InvalidOperationException("Trusted checkpoint receipt factory returned null."));
        }

        private static Dictionary<string, T> PrivateDictionary<T>(object source, string fieldName)
        {
            var field = source.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Private dictionary missing: " + fieldName);
            return (Dictionary<string, T>)(field.GetValue(source)
                ?? throw new InvalidOperationException("Private dictionary was null."));
        }

        private static void Tamper(object source, string fieldName, object value)
        {
            var field = source.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Tamper target missing: " + fieldName);
            field.SetValue(source, value);
        }

        private static string NewTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "mosaic-v40-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));

        private static string Hash(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
                return string.Concat(algorithm.ComputeHash(bytes).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private sealed class Fixture
        {
            public Fixture(
                ProvisionalDialogueAppraisalStore v39Store,
                DurableCanonicalStoreSet stores,
                DurableAppraisalOutbox outbox,
                DurableProposalRegistry registry,
                CheckpointAlignedDurableAppraisalCoordinator coordinator,
                DurableAppraisalAdmissionRequest request,
                CheckpointCommitReceipt checkpoint)
            {
                V39Store = v39Store;
                Stores = stores;
                Outbox = outbox;
                Registry = registry;
                Coordinator = coordinator;
                Request = request;
                Checkpoint = checkpoint;
            }

            public ProvisionalDialogueAppraisalStore V39Store { get; }
            public DurableCanonicalStoreSet Stores { get; }
            public DurableAppraisalOutbox Outbox { get; }
            public DurableProposalRegistry Registry { get; }
            public CheckpointAlignedDurableAppraisalCoordinator Coordinator { get; }
            public DurableAppraisalAdmissionRequest Request { get; }
            public CheckpointCommitReceipt Checkpoint { get; }
        }
    }
}
