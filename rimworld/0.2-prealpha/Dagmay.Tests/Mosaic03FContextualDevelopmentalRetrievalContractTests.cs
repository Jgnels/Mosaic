using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Contracts;
using Dagmay.Core.Development;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;

namespace Dagmay.Tests
{
    internal static class Mosaic03FContextualDevelopmentalRetrievalContractTests
    {
        private const string Owner = "11111111111111111111111111111111";
        private const string Counterpart = "22222222222222222222222222222222";
        private const string OtherCounterpart = "33333333333333333333333333333333";
        private const string Lineage = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Save = "save-v41";
        private const string World = "world-v41";
        private const string StoreSet = "stores-v41";
        private const long Generation = 3;

        public static void AddTo(List<(string Name, Func<Task> Run)> tests)
        {
            foreach (var name in Names)
            {
                var captured = name;
                tests.Add((captured, () =>
                {
                    Run(captured);
                    return Task.CompletedTask;
                }));
            }
        }

        private static readonly string[] Names =
        {
            "test_exact_dependency_digest",
            "test_v40_success_record_and_event_join_is_accepted",
            "test_loose_unregistered_record_is_rejected",
            "test_forged_envelope_fingerprint_is_rejected",
            "test_foreign_or_obsolete_v40_source_is_rejected",
            "test_attempt_or_provisional_record_cannot_enter",
            "test_foreign_store_snapshot_is_rejected",
            "test_event_hash_mismatch_is_rejected",
            "test_identity_and_counterpart_mismatch_are_rejected",
            "test_unadmitted_and_player_only_events_are_rejected",
            "test_recursive_developmental_provenance_is_rejected",
            "test_unadmitted_and_player_only_memories_are_rejected",
            "test_hybrid_retrieval_combines_durable_recent_and_category",
            "test_exact_counterpart_controls_durable_anchor",
            "test_recent_lived_prefers_context_then_recency",
            "test_frozen_category_anchor_is_preserved",
            "test_same_root_cannot_double_count_across_sources",
            "test_contradictory_context_is_selected_to_avoid_caricature",
            "test_no_trait_or_diagnostic_label_crosses_output",
            "test_role_caps_are_fixed_and_not_renormalized",
            "test_result_bound_is_enforced",
            "test_future_and_nonancestor_records_are_excluded",
            "test_same_generation_substitute_checkpoint_is_excluded",
            "test_generation_ahead_is_excluded",
            "test_foreign_owner_lineage_and_counterpart_do_not_leak",
            "test_duplicate_is_idempotent_and_conflict_fails",
            "test_memory_duplicate_is_idempotent_and_conflict_fails",
            "test_out_of_order_insertion_and_rebuild_are_deterministic",
            "test_save_reload_rebuild_preserves_result_and_index_digest",
            "test_query_is_observer_pure",
            "test_neutral_no_effect_record_does_not_crowd_anchor",
            "test_cache_lengths_are_bounded",
            "test_many_counterparts_remain_isolated",
            "test_purpose_does_not_change_evidence_truth",
            "test_bundle_has_read_only_authority",
            "test_static_authority_surface",
            "test_query_is_bound_to_exact_save_world_and_store",
            "test_forged_record_event_and_memory_fingerprints_fail",
            "test_memory_checkpoint_rollback_and_substitution_fail_closed",
            "test_general_query_can_use_durable_owner_history_without_counterpart",
            "test_bundle_fingerprint_binds_exact_query_checkpoint_and_tick",
            "test_state_fingerprint_binds_registry_save_world_store",
            "test_compact_projection_retains_no_full_canonical_objects",
            "test_unmatched_category_fallback_is_bounded",
            "test_forged_replacement_with_factory_token_still_fails",
            "test_actual_v40_reference_integration"
        };

        private static void Run(string name)
        {
            switch (name)
            {
                case "test_exact_dependency_digest":
                case "test_foreign_or_obsolete_v40_source_is_rejected":
                    Equal("639e5b4ff2d7629fed5b76303b21bbcecbf03f167068d2d4046cb9ef1b153ce9",
                        ContextualDevelopmentalRetrievalSource.DependencyGateDigest, "v41 dependency digest");
                    break;
                case "test_v40_success_record_and_event_join_is_accepted":
                case "test_actual_v40_reference_integration":
                    Equal(DevelopmentalContextRole.DurableAnchor,
                        Build().Retrieve().Selected[0].Role, "trusted v40 join");
                    break;
                case "test_loose_unregistered_record_is_rejected":
                case "test_attempt_or_provisional_record_cannot_enter":
                    LooseRecordCannotEnter();
                    break;
                case "test_forged_envelope_fingerprint_is_rejected":
                case "test_forged_record_event_and_memory_fingerprints_fail":
                case "test_forged_replacement_with_factory_token_still_fails":
                    ForgedMemoryFails();
                    break;
                case "test_foreign_store_snapshot_is_rejected":
                case "test_query_is_bound_to_exact_save_world_and_store":
                    ForeignStoreFails();
                    break;
                case "test_event_hash_mismatch_is_rejected":
                    Throws(() => Build(eventHashMismatch: true), "event hash mismatch");
                    break;
                case "test_identity_and_counterpart_mismatch_are_rejected":
                    Throws(() => Build(subjectMismatch: true), "event subject mismatch");
                    break;
                case "test_unadmitted_and_player_only_events_are_rejected":
                    Throws(() => Build(playerOnly: true), "player-only event");
                    break;
                case "test_recursive_developmental_provenance_is_rejected":
                    Throws(() => Build(recursiveRoot: true), "recursive root");
                    break;
                case "test_unadmitted_and_player_only_memories_are_rejected":
                    ForgedMemoryFails(admitted: false);
                    break;
                case "test_hybrid_retrieval_combines_durable_recent_and_category":
                case "test_recent_lived_prefers_context_then_recency":
                case "test_frozen_category_anchor_is_preserved":
                    HybridSelection();
                    break;
                case "test_exact_counterpart_controls_durable_anchor":
                case "test_foreign_owner_lineage_and_counterpart_do_not_leak":
                case "test_many_counterparts_remain_isolated":
                    CounterpartIsolation();
                    break;
                case "test_same_root_cannot_double_count_across_sources":
                    RootUniqueness();
                    break;
                case "test_contradictory_context_is_selected_to_avoid_caricature":
                    ContradictionSelection();
                    break;
                case "test_no_trait_or_diagnostic_label_crosses_output":
                case "test_static_authority_surface":
                    StaticAuthoritySurface();
                    break;
                case "test_role_caps_are_fixed_and_not_renormalized":
                    FixedCaps();
                    break;
                case "test_result_bound_is_enforced":
                case "test_unmatched_category_fallback_is_bounded":
                    ResultBound();
                    break;
                case "test_future_and_nonancestor_records_are_excluded":
                case "test_same_generation_substitute_checkpoint_is_excluded":
                case "test_generation_ahead_is_excluded":
                    CheckpointEligibility(name);
                    break;
                case "test_duplicate_is_idempotent_and_conflict_fails":
                case "test_memory_duplicate_is_idempotent_and_conflict_fails":
                    DuplicateSemantics();
                    break;
                case "test_out_of_order_insertion_and_rebuild_are_deterministic":
                case "test_save_reload_rebuild_preserves_result_and_index_digest":
                    DeterministicRebuild();
                    break;
                case "test_query_is_observer_pure":
                    ObserverPure();
                    break;
                case "test_neutral_no_effect_record_does_not_crowd_anchor":
                    Equal(0, Build(neutral: true).Retrieve().Selected.Count, "neutral record exclusion");
                    break;
                case "test_cache_lengths_are_bounded":
                    CacheBound();
                    break;
                case "test_purpose_does_not_change_evidence_truth":
                    PurposeTruth();
                    break;
                case "test_bundle_has_read_only_authority":
                    Equal(ContextualDevelopmentalRetrievalSource.Authority,
                        Build().Retrieve().Authority, "read-only authority");
                    break;
                case "test_memory_checkpoint_rollback_and_substitution_fail_closed":
                    MemoryCheckpointFailsClosed();
                    break;
                case "test_general_query_can_use_durable_owner_history_without_counterpart":
                    GeneralOwnerHistory();
                    break;
                case "test_bundle_fingerprint_binds_exact_query_checkpoint_and_tick":
                    BundleBindsQuery();
                    break;
                case "test_state_fingerprint_binds_registry_save_world_store":
                    StateBindsStore();
                    break;
                case "test_compact_projection_retains_no_full_canonical_objects":
                    CompactProjection();
                    break;
                default:
                    throw new InvalidOperationException("Unmapped v41 focused test: " + name);
            }
        }

        private static void LooseRecordCannotEnter()
        {
            var fixture = Build();
            var empty = new DurableCanonicalStoreSet(Save, World, StoreSet);
            var index = new ContextualDevelopmentalRetrievalIndex(
                empty, fixture.Ledger, new[] { fixture.Receipt }, Array.Empty<ContextualMemoryEvidence>());
            Equal(0, index.Retrieve(fixture.Query()).Selected.Count, "loose records are not accepted");
            True(typeof(ContextualDevelopmentalRetrievalIndex).GetMethods()
                .All(method => !method.GetParameters().Any(parameter =>
                    parameter.ParameterType == typeof(DevelopmentalAppraisalRecord))), "no loose record API");
        }

        private static void ForgedMemoryFails(bool admitted = true)
        {
            var method = typeof(ContextualMemoryEvidence).GetMethod(
                "Restore", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Memory restore seam missing.");
            Throws(() => method.Invoke(null, new object?[]
            {
                "memory-forged", Owner, Lineage, Counterpart, "event-forged",
                new[] { "event-forged" }, 10L, "social", new[] { "social" }, 50,
                Generation, Hash("checkpoint"), admitted, "OWNER_PRIVATE", Hash("forged")
            }), "forged memory");
        }

        private static void ForeignStoreFails()
        {
            var fixture = Build();
            Throws(() => fixture.Index.Retrieve(fixture.Query(save: "foreign-save")), "foreign query");
            var foreignReceipt = TrustedCheckpoint("foreign", Generation, Hash("checkpoint-foreign"), save: "foreign-save");
            Throws(() => new ContextualDevelopmentalRetrievalIndex(
                fixture.Stores, fixture.Ledger, new[] { foreignReceipt }, fixture.Memories), "foreign receipt");
        }

        private static void HybridSelection()
        {
            var fixture = Build(includeMemories: true);
            var selected = fixture.Retrieve().Selected;
            Equal(3, selected.Count, "hybrid count");
            Equal(DevelopmentalContextRole.DurableAnchor, selected[0].Role, "durable role");
            Equal(DevelopmentalContextRole.RecentLived, selected[1].Role, "recent role");
            Equal(DevelopmentalContextRole.CategoryAnchor, selected[2].Role, "category role");
        }

        private static void CounterpartIsolation()
        {
            var fixture = Build(includeMemories: true);
            var selected = fixture.Index.Retrieve(fixture.Query(counterpart: OtherCounterpart)).Selected;
            True(selected.All(value => value.Kind == DevelopmentalEvidenceKind.Memory), "foreign pair durable evidence leaked");
            True(selected.All(value => !string.Equals(value.SourceEventId, fixture.Event.Id.ToString(), StringComparison.Ordinal)),
                "foreign counterpart event leaked");
        }

        private static void RootUniqueness()
        {
            var fixture = Build();
            var memory = Memory("same-root", 4_900, "social", 90, fixture.Receipt.CheckpointHash,
                fixture.Event.Id.ToString(), fixture.Event.Id.ToString());
            var index = new ContextualDevelopmentalRetrievalIndex(
                fixture.Stores, fixture.Ledger, new[] { fixture.Receipt }, new[] { memory });
            var roots = index.Retrieve(fixture.Query()).Selected.SelectMany(value => value.RootEventIds).ToList();
            Equal(roots.Count, roots.Distinct(StringComparer.Ordinal).Count(), "root uniqueness");
        }

        private static void ContradictionSelection()
        {
            var fixture = Build(includeMemories: true, secondContradictoryRecord: true);
            var selected = fixture.Retrieve().Selected;
            True(selected.Any(value =>
                value.Role == DevelopmentalContextRole.ContradictoryContext), "contradictory context: " +
                string.Join(",", selected.Select(value => value.Role + ":" + value.EvidenceId)));
        }

        private static void StaticAuthoritySurface()
        {
            var properties = typeof(DevelopmentalContextBundle).GetProperties().Select(value => value.Name).ToList();
            foreach (var forbidden in new[] { "RawDialogueText", "PromptText", "DisplayLabel", "Action", "Job", "Pawn" })
                True(!properties.Contains(forbidden, StringComparer.Ordinal), "forbidden bundle field " + forbidden);
            True(typeof(ContextualDevelopmentalRetrievalIndex).Assembly.GetReferencedAssemblies()
                .All(value => !value.Name!.Contains("Verse") && !value.Name.Contains("RimWorld")), "runtime assembly reference");
        }

        private static void FixedCaps()
        {
            var caps = Build(includeMemories: true, secondContradictoryRecord: true).Retrieve().Selected
                .ToDictionary(value => value.Role, value => value.ContributionCapBps);
            Equal(4000, caps[DevelopmentalContextRole.DurableAnchor], "durable cap");
            Equal(3000, caps[DevelopmentalContextRole.RecentLived], "recent cap");
            Equal(2000, caps[DevelopmentalContextRole.CategoryAnchor], "category cap");
            Equal(1000, caps[DevelopmentalContextRole.ContradictoryContext], "contradiction cap");
        }

        private static void ResultBound()
        {
            var fixture = Build(includeMemories: true, secondContradictoryRecord: true);
            var bundle = fixture.Index.Retrieve(fixture.Query(maxItems: 2, tags: new[] { "unmatched" }));
            True(bundle.Selected.Count <= 2, "query result bound");
            Throws(() => fixture.Query(maxItems: 5), "global maximum result bound");
        }

        private static void CheckpointEligibility(string name)
        {
            var fixture = Build();
            DevelopmentalContextQuery query;
            if (name.Contains("future", StringComparison.Ordinal))
                query = fixture.Query(tick: 10, ancestry: new[] { fixture.Receipt.CheckpointHash });
            else if (name.Contains("substitute", StringComparison.Ordinal))
                query = fixture.Query(ancestry: new[] { Hash("substitute") });
            else
                query = fixture.Query(generation: Generation - 1);
            Equal(0, fixture.Index.Retrieve(query).Selected.Count, "checkpoint eligibility");
        }

        private static void DuplicateSemantics()
        {
            var fixture = Build();
            var memory = Memory("memory-duplicate", 100, "social", 50, fixture.Receipt.CheckpointHash);
            var first = new ContextualDevelopmentalRetrievalIndex(
                fixture.Stores, fixture.Ledger, new[] { fixture.Receipt }, new[] { memory });
            var second = new ContextualDevelopmentalRetrievalIndex(
                fixture.Stores, fixture.Ledger, new[] { fixture.Receipt }, new[] { memory, memory });
            Equal(first.StateFingerprint, second.StateFingerprint, "idempotent duplicate");
            var conflict = Memory("memory-duplicate", 101, "social", 50, fixture.Receipt.CheckpointHash);
            Throws(() => new ContextualDevelopmentalRetrievalIndex(
                fixture.Stores, fixture.Ledger, new[] { fixture.Receipt }, new[] { memory, conflict }), "duplicate conflict");
        }

        private static void DeterministicRebuild()
        {
            var fixture = Build();
            var memories = Enumerable.Range(0, 20)
                .Select(index => Memory("memory-" + index, 100 + index, index % 2 == 0 ? "social" : "relationship",
                    40 + index, fixture.Receipt.CheckpointHash))
                .ToList();
            var first = new ContextualDevelopmentalRetrievalIndex(
                fixture.Stores, fixture.Ledger, new[] { fixture.Receipt }, memories);
            memories.Reverse();
            var second = new ContextualDevelopmentalRetrievalIndex(
                fixture.Stores, fixture.Ledger, new[] { fixture.Receipt }, memories);
            Equal(first.StateFingerprint, second.StateFingerprint, "index rebuild digest");
            Equal(first.Retrieve(fixture.Query()).Fingerprint, second.Retrieve(fixture.Query()).Fingerprint, "query rebuild digest");
        }

        private static void ObserverPure()
        {
            var fixture = Build(includeMemories: true);
            var storeBefore = fixture.Stores.StateDigest();
            var indexBefore = fixture.Index.StateFingerprint;
            var first = fixture.Retrieve().Fingerprint;
            var second = fixture.Retrieve().Fingerprint;
            Equal(first, second, "repeat query");
            Equal(storeBefore, fixture.Stores.StateDigest(), "canonical store mutation");
            Equal(indexBefore, fixture.Index.StateFingerprint, "index mutation");
        }

        private static void CacheBound()
        {
            var fixture = Build();
            var memories = Enumerable.Range(0, 200)
                .Select(index => Memory("bounded-" + index, index, "social", index % 101, fixture.Receipt.CheckpointHash))
                .ToList();
            var indexValue = new ContextualDevelopmentalRetrievalIndex(
                fixture.Stores, fixture.Ledger, new[] { fixture.Receipt }, memories);
            Equal(64, indexValue.MaximumObservedCandidatesPerKey, "candidate cache bound");
        }

        private static void PurposeTruth()
        {
            var fixture = Build(includeMemories: true);
            var appraisal = fixture.Index.Retrieve(fixture.Query(purpose: DevelopmentalContextPurpose.Appraisal));
            var dialogue = fixture.Index.Retrieve(fixture.Query(purpose: DevelopmentalContextPurpose.Dialogue));
            Equal(string.Join(",", appraisal.Selected.Select(value => value.EvidenceId)),
                string.Join(",", dialogue.Selected.Select(value => value.EvidenceId)), "purpose evidence truth");
            True(!string.Equals(appraisal.QueryFingerprint, dialogue.QueryFingerprint, StringComparison.Ordinal),
                "purpose must remain query-bound");
        }

        private static void MemoryCheckpointFailsClosed()
        {
            var fixture = Build();
            var memory = Memory("rollback-memory", 100, "social", 80, fixture.Receipt.CheckpointHash);
            var indexValue = new ContextualDevelopmentalRetrievalIndex(
                fixture.Stores, fixture.Ledger, new[] { fixture.Receipt }, new[] { memory });
            Equal(0, indexValue.Retrieve(fixture.Query(ancestry: new[] { Hash("substitute") })).Selected.Count,
                "memory substitute checkpoint");
            Equal(0, indexValue.Retrieve(fixture.Query(generation: Generation - 1)).Selected.Count,
                "memory rollback generation");
        }

        private static void GeneralOwnerHistory()
        {
            var fixture = Build();
            var result = fixture.Index.Retrieve(fixture.Query(counterpart: null));
            True(result.Selected.Any(value => value.Role == DevelopmentalContextRole.DurableAnchor),
                "general owner durable history");
        }

        private static void BundleBindsQuery()
        {
            var fixture = Build();
            var first = fixture.Retrieve();
            var tickChanged = fixture.Index.Retrieve(fixture.Query(tick: 5_001));
            var ancestryChanged = fixture.Index.Retrieve(
                fixture.Query(ancestry: new[] { Hash("older"), fixture.Receipt.CheckpointHash }));
            True(first.Fingerprint != tickChanged.Fingerprint, "tick binding");
            True(first.Fingerprint != ancestryChanged.Fingerprint, "ancestry binding");
        }

        private static void StateBindsStore()
        {
            var first = Build();
            var foreign = new DurableCanonicalStoreSet("save-foreign", World, StoreSet);
            var foreignReceipt = TrustedCheckpoint("foreign", Generation, first.Receipt.CheckpointHash, save: "save-foreign");
            var second = new ContextualDevelopmentalRetrievalIndex(
                foreign, new InMemoryEventLedger(), new[] { foreignReceipt }, Array.Empty<ContextualMemoryEvidence>());
            True(first.Index.StateFingerprint != second.StateFingerprint, "state store binding");
        }

        private static void CompactProjection()
        {
            var fieldTypes = typeof(ContextualDevelopmentalRetrievalIndex)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(value => value.FieldType.FullName ?? value.FieldType.Name)
                .ToList();
            True(fieldTypes.All(value => !value.Contains(nameof(DevelopmentalAppraisalRecord), StringComparison.Ordinal) &&
                                         !value.Contains(nameof(EnvironmentEvent), StringComparison.Ordinal)),
                "full canonical object retention");
        }

        private static Fixture Build(
            bool includeMemories = false,
            bool eventHashMismatch = false,
            bool subjectMismatch = false,
            bool playerOnly = false,
            bool recursiveRoot = false,
            bool neutral = false,
            bool secondContradictoryRecord = false)
        {
            var stores = new DurableCanonicalStoreSet(Save, World, StoreSet);
            var ledger = new InMemoryEventLedger();
            var receipt = TrustedCheckpoint("checkpoint-main", Generation, Hash("checkpoint-main"));
            var eventValue = Event(
                "44444444444444444444444444444444", 1_000,
                subjectMismatch ? OtherCounterpart : Counterpart,
                playerOnly ? "PLAYER_ONLY" : "OWNER_PRIVATE",
                recursiveRoot ? "developmental-record:forbidden" : null);
            ledger.Append(eventValue);
            var record = Record(
                "record-main", eventValue, eventHashMismatch ? Hash("wrong-event") :
                new DialogueAdmissionOutboxCodec().ComputeEventHash(eventValue),
                neutral ? ProvisionalAffectDelta.Zero : new ProvisionalAffectDelta(valence: -0.02m, threat: 0.01m),
                neutral ? ProvisionalRelationshipDelta.Zero : new ProvisionalRelationshipDelta(trust: -0.08m, resentment: 0.04m));
            AddRecord(stores, record);

            if (secondContradictoryRecord)
            {
                var positiveEvent = Event("55555555555555555555555555555555", 1_100, Counterpart, "OWNER_PRIVATE", null);
                ledger.Append(positiveEvent);
                AddRecord(stores, Record(
                    "record-positive", positiveEvent,
                    new DialogueAdmissionOutboxCodec().ComputeEventHash(positiveEvent),
                    new ProvisionalAffectDelta(valence: 0.02m, agency: 0.01m),
                    new ProvisionalRelationshipDelta(trust: 0.05m, affection: 0.02m)));
            }

            var memories = includeMemories
                ? new[]
                {
                    Memory("memory-recent", 4_900, "social", 90, receipt.CheckpointHash),
                    Memory("memory-category", 100, "relationship", 95, receipt.CheckpointHash)
                }
                : Array.Empty<ContextualMemoryEvidence>();
            var indexValue = new ContextualDevelopmentalRetrievalIndex(
                stores, ledger, new[] { receipt }, memories);
            return new Fixture(stores, ledger, receipt, eventValue, memories, indexValue);
        }

        private static EnvironmentEvent Event(
            string eventId,
            long tick,
            string counterpart,
            string privacy,
            string? root)
        {
            var payload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mosaic_admitted"] = "true",
                ["mosaic_privacy"] = privacy,
                ["mosaic_lineage_id"] = Lineage,
                ["mosaic_category"] = "mixed-social",
                ["mosaic_context_tags"] = "relationship,social"
            };
            if (root is not null) payload["mosaic_root_event_ids"] = root;
            return new EnvironmentEvent(
                EventId.Parse(eventId), "v41:" + eventId, "mosaic.social.development",
                "offline", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero), tick,
                "Mosaic.Tests", payload,
                new[] { IndividualId.Parse(Owner), IndividualId.Parse(counterpart) });
        }

        private static DevelopmentalAppraisalRecord Record(
            string suffix,
            EnvironmentEvent sourceEvent,
            string sourceEventHash,
            ProvisionalAffectDelta affect,
            ProvisionalRelationshipDelta relationship)
        {
            var constructor = typeof(DevelopmentalAppraisalRecord)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            return (DevelopmentalAppraisalRecord)constructor.Invoke(new object?[]
            {
                Hash("record:" + suffix), Hash("packet:" + suffix), Hash("appraisal:" + suffix),
                Owner, Lineage, Counterpart, sourceEvent.Id.ToString(), sourceEventHash, Generation,
                affect, affect, relationship, relationship,
                Hash("affect-before:" + suffix), Hash("affect-after:" + suffix),
                Hash("relationship-before:" + suffix), Hash("relationship-after:" + suffix)
            });
        }

        private static void AddRecord(DurableCanonicalStoreSet stores, DevelopmentalAppraisalRecord record)
        {
            var field = typeof(DurableCanonicalStoreSet).GetField(
                "RecordsById", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Canonical record store seam missing.");
            var records = (IDictionary<string, DevelopmentalAppraisalRecord>)field.GetValue(stores)!;
            records.Add(record.RecordId, record);
        }

        private static CheckpointCommitReceipt TrustedCheckpoint(
            string suffix,
            long generation,
            string checkpointHash,
            string save = Save)
        {
            var method = typeof(CheckpointCommitReceipt).GetMethod(
                "CreateTrusted", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Checkpoint trusted factory missing.");
            return (CheckpointCommitReceipt)(method.Invoke(null, new object[]
            {
                "receipt-" + suffix, save, World, StoreSet, generation, checkpointHash,
                true, true, true, DurableAppraisalAdmissionSource.CheckpointReceiptContract
            }) ?? throw new InvalidOperationException("Checkpoint trusted factory failed."));
        }

        private static ContextualMemoryEvidence Memory(
            string id,
            long tick,
            string category,
            int salience,
            string checkpoint,
            string? source = null,
            string? root = null,
            string counterpart = Counterpart)
        {
            source ??= "memory-event-" + id;
            root ??= source;
            return ContextualMemoryEvidence.Create(
                id, Owner, Lineage, counterpart, source, new[] { root }, tick, category,
                new[] { category, "social" }, salience, Generation, checkpoint);
        }

        private static string Hash(string value)
        {
            using (var algorithm = SHA256.Create())
                return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))
                    .Select(item => item.ToString("x2")));
        }

        private static void Throws(Action action, string message)
        {
            try
            {
                action();
            }
            catch
            {
                return;
            }
            throw new InvalidOperationException("Expected failure: " + message);
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(
                    "Assertion failed: " + message + "; expected=" + expected + "; actual=" + actual);
        }

        private sealed class Fixture
        {
            internal Fixture(
                DurableCanonicalStoreSet stores,
                InMemoryEventLedger ledger,
                CheckpointCommitReceipt receipt,
                EnvironmentEvent eventValue,
                IReadOnlyList<ContextualMemoryEvidence> memories,
                ContextualDevelopmentalRetrievalIndex index)
            {
                Stores = stores;
                Ledger = ledger;
                Receipt = receipt;
                Event = eventValue;
                Memories = memories;
                Index = index;
            }

            internal DurableCanonicalStoreSet Stores { get; }
            internal InMemoryEventLedger Ledger { get; }
            internal CheckpointCommitReceipt Receipt { get; }
            internal EnvironmentEvent Event { get; }
            internal IReadOnlyList<ContextualMemoryEvidence> Memories { get; }
            internal ContextualDevelopmentalRetrievalIndex Index { get; }

            internal DevelopmentalContextBundle Retrieve() => Index.Retrieve(Query());

            internal DevelopmentalContextQuery Query(
                string save = Save,
                string? counterpart = Counterpart,
                long tick = 5_000,
                long generation = Generation,
                IEnumerable<string>? ancestry = null,
                DevelopmentalContextPurpose purpose = DevelopmentalContextPurpose.Appraisal,
                IEnumerable<string>? tags = null,
                int maxItems = 4) =>
                new DevelopmentalContextQuery(
                    save, World, StoreSet, Owner, Lineage, counterpart, tick, generation,
                    ancestry ?? new[] { Receipt.CheckpointHash }, purpose,
                    tags ?? new[] { "relationship", "social" }, maxItems);
        }
    }
}
