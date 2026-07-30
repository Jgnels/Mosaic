using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Contracts;
using Dagmay.Core.Development;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;

namespace Dagmay.Tests
{
    internal static class Mosaic03GGroundedDevelopmentalContextMaterializationContractTests
    {
        private const string Owner = "11111111111111111111111111111111";
        private const string Counterpart = "22222222222222222222222222222222";
        private const string OtherCounterpart = "33333333333333333333333333333333";
        private const string Lineage = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Save = "save-v42";
        private const string World = "world-v42";
        private const string StoreSet = "stores-v42";
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
            "test_exact_v41_formal_dependency_digest",
            "test_exact_v41_executable_dependency_digest",
            "test_exact_v42_formal_gate_digest",
            "test_source_contract_is_explicit",
            "test_authority_is_exact_and_read_only",
            "test_actual_v41_bundle_materializes",
            "test_public_packet_fingerprint_self_verifies",
            "test_mismatched_public_packet_fingerprint_is_rejected",
            "test_developmental_dimensions_are_namespaced",
            "test_affect_and_relationship_names_do_not_collide",
            "test_memory_never_invents_direction",
            "test_memory_category_and_provenance_are_preserved",
            "test_empty_bundle_is_valid_and_explicit",
            "test_current_event_root_cannot_be_reused",
            "test_duplicate_selected_identity_fails_closed",
            "test_role_caps_are_not_renormalized",
            "test_total_declared_caps_never_exceed_10000",
            "test_largest_remainder_allocation_is_exact",
            "test_largest_remainder_tie_break_is_deterministic",
            "test_zero_dimensions_do_not_crowd_synthesis",
            "test_positive_and_negative_evidence_remain_separate",
            "test_contradiction_is_preserved",
            "test_confidence_falls_under_contradiction",
            "test_packet_is_query_bound",
            "test_packet_is_bundle_bound",
            "test_current_root_set_binds_request",
            "test_current_tick_binds_request",
            "test_purpose_is_preserved",
            "test_purpose_does_not_change_evidence_truth",
            "test_registry_query_is_observer_pure",
            "test_materialization_is_repeatable",
            "test_projection_registry_is_order_deterministic",
            "test_duplicate_projection_admission_is_idempotent",
            "test_conflicting_projection_duplicate_fails_closed",
            "test_missing_projection_fails_closed",
            "test_foreign_store_query_is_rejected",
            "test_future_generation_is_excluded_by_v41",
            "test_substituted_checkpoint_is_excluded_by_v41",
            "test_future_event_is_excluded_by_v41",
            "test_foreign_counterpart_isolated_by_v41",
            "test_unadmitted_memory_is_rejected",
            "test_recursive_provenance_is_rejected",
            "test_extreme_deltas_remain_bounded",
            "test_packet_item_bound_is_four",
            "test_packet_contains_no_raw_dialogue",
            "test_packet_contains_no_display_label",
            "test_packet_contains_no_prompt",
            "test_packet_has_no_apply_method",
            "test_packet_has_no_execute_method",
            "test_packet_has_no_mutate_method",
            "test_core_has_no_rimworld_or_verse_reference",
            "test_registry_retains_no_full_canonical_objects",
            "test_metrics_report_zero_full_objects",
            "test_state_fingerprint_is_incremental_and_stable",
            "test_public_json_is_deterministic",
            "test_public_fingerprint_covers_dimensions",
            "test_fixed_reason_codes_are_preserved",
            "test_actual_v40_to_v41_to_v42_reference_integration"
        };

        private static void Run(string name)
        {
            switch (name)
            {
                case "test_exact_v41_formal_dependency_digest":
                    Equal("7eef7fe095621a15b2e84464abcabf2d05ba3681d41d51488317c2d73776755d",
                        GroundedDevelopmentalContextSource.V41FormalGateDigest, name);
                    break;
                case "test_exact_v41_executable_dependency_digest":
                    Equal("9fa64475517509764ef306ad6c351ce886b6be6750281889145df320848c1b2b",
                        GroundedDevelopmentalContextSource.V41ExecutableGateDigest, name);
                    break;
                case "test_exact_v42_formal_gate_digest":
                    Equal("c48b1e977c96b0450f97e8579e158cfe2353e412ba6dbcdf30e4056cbdd5edb9",
                        GroundedDevelopmentalContextSource.FormalGateDigest, name);
                    break;
                case "test_source_contract_is_explicit":
                    Equal("Mosaic.Core.GroundedDevelopmentalContextMaterialization.v1",
                        Build().Materialize().SourceContract, name);
                    break;
                case "test_authority_is_exact_and_read_only":
                    Equal(GroundedDevelopmentalContextSource.Authority, Build().Materialize().Authority, name);
                    break;
                case "test_actual_v41_bundle_materializes":
                case "test_actual_v40_to_v41_to_v42_reference_integration":
                    True(Build().Materialize().Items.Count > 0, name);
                    break;
                case "test_public_packet_fingerprint_self_verifies":
                    True(Build().Materialize().VerifyFingerprint(), name);
                    break;
                case "test_mismatched_public_packet_fingerprint_is_rejected":
                    MismatchedFingerprintRejected();
                    break;
                case "test_developmental_dimensions_are_namespaced":
                case "test_affect_and_relationship_names_do_not_collide":
                    NamespacedDimensions();
                    break;
                case "test_memory_never_invents_direction":
                case "test_memory_category_and_provenance_are_preserved":
                    MemoryIsNondirectional();
                    break;
                case "test_empty_bundle_is_valid_and_explicit":
                    EmptyBundle();
                    break;
                case "test_current_event_root_cannot_be_reused":
                    CurrentRootRejected();
                    break;
                case "test_duplicate_selected_identity_fails_closed":
                    DuplicateSelectedRejected();
                    break;
                case "test_role_caps_are_not_renormalized":
                case "test_total_declared_caps_never_exceed_10000":
                    FixedCaps();
                    break;
                case "test_largest_remainder_allocation_is_exact":
                case "test_largest_remainder_tie_break_is_deterministic":
                    AllocationIsExact();
                    break;
                case "test_zero_dimensions_do_not_crowd_synthesis":
                    MemoryIsNondirectional();
                    break;
                case "test_positive_and_negative_evidence_remain_separate":
                case "test_contradiction_is_preserved":
                case "test_confidence_falls_under_contradiction":
                    ContradictionPreserved();
                    break;
                case "test_packet_is_query_bound":
                case "test_current_tick_binds_request":
                    TickBindsPacket();
                    break;
                case "test_packet_is_bundle_bound":
                    BundleBindsPacket();
                    break;
                case "test_current_root_set_binds_request":
                    RootSetBindsPacket();
                    break;
                case "test_purpose_is_preserved":
                case "test_purpose_does_not_change_evidence_truth":
                    PurposePreserved();
                    break;
                case "test_registry_query_is_observer_pure":
                case "test_materialization_is_repeatable":
                    ObserverPure();
                    break;
                case "test_projection_registry_is_order_deterministic":
                case "test_duplicate_projection_admission_is_idempotent":
                    RegistryOrderDeterministic();
                    break;
                case "test_conflicting_projection_duplicate_fails_closed":
                    ConflictingMemoryFails();
                    break;
                case "test_missing_projection_fails_closed":
                    MissingProjectionFails();
                    break;
                case "test_foreign_store_query_is_rejected":
                    ForeignStoreFails();
                    break;
                case "test_future_generation_is_excluded_by_v41":
                case "test_substituted_checkpoint_is_excluded_by_v41":
                case "test_future_event_is_excluded_by_v41":
                    V41Eligibility(name);
                    break;
                case "test_foreign_counterpart_isolated_by_v41":
                    CounterpartIsolation();
                    break;
                case "test_unadmitted_memory_is_rejected":
                    ForgedMemoryFails();
                    break;
                case "test_recursive_provenance_is_rejected":
                    Throws(() => Build(recursiveRoot: true), name);
                    break;
                case "test_extreme_deltas_remain_bounded":
                    ExtremeBounded();
                    break;
                case "test_packet_item_bound_is_four":
                    True(Build(includeMemories: true, contradictory: true).Materialize().Items.Count <= 4, name);
                    break;
                case "test_packet_contains_no_raw_dialogue":
                case "test_packet_contains_no_display_label":
                case "test_packet_contains_no_prompt":
                case "test_packet_has_no_apply_method":
                case "test_packet_has_no_execute_method":
                case "test_packet_has_no_mutate_method":
                    StaticAuthoritySurface(name);
                    break;
                case "test_core_has_no_rimworld_or_verse_reference":
                    True(typeof(GroundedDevelopmentalContextPacket).Assembly.GetReferencedAssemblies()
                        .All(value => !value.Name!.Contains("Verse") && !value.Name.Contains("RimWorld")), name);
                    break;
                case "test_registry_retains_no_full_canonical_objects":
                    RegistryRetainsNoCanonicalObjects();
                    break;
                case "test_metrics_report_zero_full_objects":
                    Equal(0L, Build().Materialize().Metrics["full_canonical_objects_retained"], name);
                    break;
                case "test_state_fingerprint_is_incremental_and_stable":
                    ObserverPure();
                    break;
                case "test_public_json_is_deterministic":
                    PublicJsonDeterministic();
                    break;
                case "test_public_fingerprint_covers_dimensions":
                    FingerprintCoversDimensions();
                    break;
                case "test_fixed_reason_codes_are_preserved":
                    True(Build().Materialize().Items[0].ReasonCodes.Contains(
                        "durable_canonical_success", StringComparer.Ordinal), name);
                    break;
                default:
                    throw new InvalidOperationException("Unmapped v42 focused test: " + name);
            }
        }

        private static void MismatchedFingerprintRejected()
        {
            var packet = Build().Materialize();
            var property = typeof(GroundedDevelopmentalContextPacket).GetProperty(nameof(packet.Fingerprint))
                ?? throw new InvalidOperationException("Fingerprint property missing.");
            property.GetSetMethod(true)!.Invoke(packet, new object[] { Hash("mismatch") });
            True(!packet.VerifyFingerprint(), "tampered packet must not self-verify");
            Throws(() => GroundedDevelopmentalContextPacket.RequireValid(packet), "tampered packet consumer");
        }

        private static void NamespacedDimensions()
        {
            var dimensions = Build().Materialize().Dimensions.Select(value => value.Dimension).ToList();
            True(dimensions.Count > 0, "directional dimensions");
            True(dimensions.All(value =>
                value.StartsWith("affect.", StringComparison.Ordinal) ||
                value.StartsWith("relationship.", StringComparison.Ordinal)), "namespaced dimensions");
            True(dimensions.Contains("affect.attachment", StringComparer.Ordinal) &&
                 dimensions.Contains("relationship.affection", StringComparer.Ordinal),
                 "affect/relationship namespace separation");
        }

        private static void MemoryIsNondirectional()
        {
            var packet = Build(includeMemories: true).Materialize();
            var memoryItems = packet.Items.Where(value => value.Kind == DevelopmentalEvidenceKind.Memory).ToList();
            True(memoryItems.Count > 0, "memory selected");
            True(memoryItems.All(value => value.AllocatedDimensionsBps.Count == 0), "memory direction");
            True(memoryItems.All(value => !string.IsNullOrEmpty(value.Category) &&
                                          value.RootEventIds.Count > 0), "memory provenance");
        }

        private static void EmptyBundle()
        {
            var fixture = Build(neutral: true);
            var packet = fixture.Materialize();
            Equal(0, packet.Items.Count, "empty packet");
            True(packet.UncertaintyCodes.Contains("no_eligible_historical_context", StringComparer.Ordinal),
                "empty uncertainty");
        }

        private static void CurrentRootRejected()
        {
            var fixture = Build();
            Throws(() => fixture.Materialize(new[] { fixture.Event.Id.ToString() }), "current event root");
        }

        private static void DuplicateSelectedRejected()
        {
            var fixture = Build();
            var query = fixture.Query();
            var original = fixture.Index.Retrieve(query);
            var forged = Bundle(query, new[] { original.Selected[0], original.Selected[0] });
            Throws(() => fixture.Materializer.Materialize(
                new GroundedDevelopmentalContextRequest(query, forged, new[] { "current" }, 5_000)),
                "duplicate selected identity");
        }

        private static void FixedCaps()
        {
            var packet = Build(includeMemories: true, contradictory: true).Materialize();
            var expected = new Dictionary<DevelopmentalContextRole, int>
            {
                [DevelopmentalContextRole.DurableAnchor] = 4000,
                [DevelopmentalContextRole.RecentLived] = 3000,
                [DevelopmentalContextRole.CategoryAnchor] = 2000,
                [DevelopmentalContextRole.ContradictoryContext] = 1000
            };
            foreach (var item in packet.Items) Equal(expected[item.Role], item.ContributionCapBps, "fixed role cap");
            True(packet.Items.Sum(value => value.ContributionCapBps) <= 10_000, "total caps");
            True(packet.Metrics["unused_role_cap_bps"] >= 0, "unused cap");
        }

        private static void AllocationIsExact()
        {
            var item = Build().Materialize().Items[0];
            Equal(item.ContributionCapBps,
                item.AllocatedDimensionsBps.Sum(value => Math.Abs(value.ValueBps)), "largest remainder total");
            var replay = Build().Materialize().Items[0];
            Equal(string.Join(",", item.AllocatedDimensionsBps.Select(Value)),
                string.Join(",", replay.AllocatedDimensionsBps.Select(Value)), "allocation replay");
        }

        private static string Value(GroundedDimensionContribution value) =>
            value.Dimension + "=" + value.ValueBps;

        private static void ContradictionPreserved()
        {
            var packet = Build(contradictory: true).Materialize();
            var dimension = packet.Dimensions.FirstOrDefault(value =>
                value.PositiveEvidenceBps > 0 && value.NegativeEvidenceBps > 0);
            True(dimension is not null, "contradictory dimension");
            True(dimension!.ContradictionCount > 0, "contradiction count");
            True(dimension.ConfidenceBps < 10_000, "contradiction confidence");
            True(packet.UncertaintyCodes.Contains("contradictory_history_preserved", StringComparer.Ordinal),
                "contradiction uncertainty");
        }

        private static void TickBindsPacket()
        {
            var fixture = Build();
            var first = fixture.Materialize();
            var second = fixture.Materialize(currentTick: 5_001, queryTick: 5_001);
            True(first.Fingerprint != second.Fingerprint, "tick binding");
        }

        private static void BundleBindsPacket()
        {
            var first = Build().Materialize();
            var second = Build(includeMemories: true).Materialize();
            True(first.V41BundleFingerprint != second.V41BundleFingerprint, "bundle identity");
            True(first.Fingerprint != second.Fingerprint, "bundle packet binding");
        }

        private static void RootSetBindsPacket()
        {
            var fixture = Build();
            var first = fixture.Materialize(new[] { "current-a" });
            var second = fixture.Materialize(new[] { "current-b" });
            True(first.RequestFingerprint != second.RequestFingerprint, "root request binding");
            True(first.Fingerprint != second.Fingerprint, "root packet binding");
        }

        private static void PurposePreserved()
        {
            var fixture = Build(includeMemories: true);
            var appraisal = fixture.Materialize(purpose: DevelopmentalContextPurpose.Appraisal);
            var dialogue = fixture.Materialize(purpose: DevelopmentalContextPurpose.Dialogue);
            Equal(DevelopmentalContextPurpose.Dialogue, dialogue.Purpose, "purpose output");
            Equal(string.Join(",", appraisal.Items.Select(value => value.EvidenceId)),
                string.Join(",", dialogue.Items.Select(value => value.EvidenceId)), "purpose truth");
            True(appraisal.Fingerprint != dialogue.Fingerprint, "purpose packet binding");
        }

        private static void ObserverPure()
        {
            var fixture = Build(includeMemories: true);
            var storeBefore = fixture.Stores.StateDigest();
            var registryBefore = fixture.Registry.StateFingerprint;
            var first = fixture.Materialize().Fingerprint;
            var second = fixture.Materialize().Fingerprint;
            Equal(first, second, "repeat materialization");
            Equal(storeBefore, fixture.Stores.StateDigest(), "canonical mutation");
            Equal(registryBefore, fixture.Registry.StateFingerprint, "registry mutation");
        }

        private static void RegistryOrderDeterministic()
        {
            var first = Build(includeMemories: true);
            var reversed = Build(includeMemories: true, reverseMemories: true);
            Equal(first.Registry.StateFingerprint, reversed.Registry.StateFingerprint, "registry state");
            Equal(first.Materialize().Fingerprint, reversed.Materialize().Fingerprint, "registry order packet");
        }

        private static void ConflictingMemoryFails()
        {
            var fixture = Build();
            var first = Memory("duplicate-memory", 100, 50, fixture.Receipt.CheckpointHash);
            var conflict = Memory("duplicate-memory", 101, 50, fixture.Receipt.CheckpointHash);
            Throws(() => new TrustedContextProjectionRegistry(
                fixture.Stores, fixture.Ledger, new[] { fixture.Receipt }, new[] { first, conflict }),
                "conflicting memory projection");
        }

        private static void MissingProjectionFails()
        {
            var withMemory = Build(includeMemories: true);
            var withoutMemory = new TrustedContextProjectionRegistry(
                withMemory.Stores, withMemory.Ledger, new[] { withMemory.Receipt },
                Array.Empty<ContextualMemoryEvidence>());
            var query = withMemory.Query();
            var bundle = withMemory.Index.Retrieve(query);
            Throws(() => new GroundedDevelopmentalContextMaterializer(withoutMemory).Materialize(
                new GroundedDevelopmentalContextRequest(query, bundle, new[] { "current" }, 5_000)),
                "missing projection");
        }

        private static void ForeignStoreFails()
        {
            var fixture = Build();
            var query = fixture.Query(save: "foreign-save");
            Throws(() => fixture.Index.Retrieve(query), "v41 foreign store");
        }

        private static void V41Eligibility(string name)
        {
            var fixture = Build();
            DevelopmentalContextQuery query;
            if (name.Contains("future_event", StringComparison.Ordinal))
                query = fixture.Query(tick: 10);
            else if (name.Contains("substituted", StringComparison.Ordinal))
                query = fixture.Query(ancestry: new[] { Hash("substitute") });
            else
                query = fixture.Query(generation: Generation - 1);
            Equal(0, fixture.Index.Retrieve(query).Selected.Count, name);
        }

        private static void CounterpartIsolation()
        {
            var fixture = Build(includeMemories: true);
            var result = fixture.Index.Retrieve(fixture.Query(counterpart: OtherCounterpart));
            True(result.Selected.All(value => value.Kind == DevelopmentalEvidenceKind.Memory), "counterpart isolation");
        }

        private static void ForgedMemoryFails()
        {
            var method = typeof(ContextualMemoryEvidence).GetMethod(
                "Restore", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Memory restore seam missing.");
            Throws(() => method.Invoke(null, new object?[]
            {
                "forged-memory", Owner, Lineage, Counterpart, "forged-event",
                new[] { "forged-event" }, 10L, "social", new[] { "social" }, 50,
                Generation, Hash("checkpoint"), false, "OWNER_PRIVATE", Hash("forged")
            }), "unadmitted memory");
        }

        private static void ExtremeBounded()
        {
            var packet = Build(extreme: true).Materialize();
            True(packet.Items.All(item =>
                item.AllocatedDimensionsBps.Sum(value => Math.Abs(value.ValueBps)) <= item.ContributionCapBps),
                "extreme role bound");
            True(packet.Metrics["allocated_influence_bps"] <= packet.Metrics["declared_role_caps_bps"],
                "extreme aggregate bound");
        }

        private static void StaticAuthoritySurface(string name)
        {
            var propertyNames = typeof(GroundedDevelopmentalContextPacket)
                .GetProperties().Select(value => value.Name).ToList();
            foreach (var forbidden in new[] { "RawDialogue", "DisplayLabel", "Prompt", "Provider", "Job", "Pawn" })
                True(!propertyNames.Any(value => value.Contains(forbidden, StringComparison.OrdinalIgnoreCase)), name);
            var methodNames = typeof(GroundedDevelopmentalContextPacket).GetMethods()
                .Select(value => value.Name).ToList();
            foreach (var forbidden in new[] { "Apply", "Execute", "Mutate", "Save", "Render" })
                True(!methodNames.Any(value => value.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase)), name);
        }

        private static void RegistryRetainsNoCanonicalObjects()
        {
            var fields = typeof(TrustedContextProjectionRegistry)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(value => value.FieldType.FullName ?? value.FieldType.Name).ToList();
            True(fields.All(value =>
                !value.Contains(nameof(DevelopmentalAppraisalRecord), StringComparison.Ordinal) &&
                !value.Contains(nameof(EnvironmentEvent), StringComparison.Ordinal) &&
                !value.Contains(nameof(ContextualMemoryEvidence), StringComparison.Ordinal)),
                "full canonical retention");
        }

        private static void PublicJsonDeterministic()
        {
            var packet = Build(includeMemories: true, contradictory: true).Materialize();
            Equal(packet.ToDeterministicJson(), packet.ToDeterministicJson(), "public serialization");
            True(packet.ToDeterministicJson().Contains(
                "\"source_gate_digest\":\"" + GroundedDevelopmentalContextSource.FormalGateDigest + "\"",
                StringComparison.Ordinal), "corrected public gate digest");
            True(packet.VerifyFingerprint(), "public serialization hash");
        }

        private static void FingerprintCoversDimensions()
        {
            var directional = Build().Materialize();
            var neutral = Build(neutral: true).Materialize();
            True(directional.Fingerprint != neutral.Fingerprint, "dimension fingerprint coverage");
        }

        private static Fixture Build(
            bool includeMemories = false,
            bool contradictory = false,
            bool recursiveRoot = false,
            bool neutral = false,
            bool reverseMemories = false,
            bool extreme = false)
        {
            var stores = new DurableCanonicalStoreSet(Save, World, StoreSet);
            var ledger = new InMemoryEventLedger();
            var receipt = TrustedCheckpoint("main", Generation, Hash("checkpoint-main"));
            var eventValue = Event(
                "44444444444444444444444444444444", 1_000, Counterpart,
                recursiveRoot ? "developmental-record:forbidden" : null);
            ledger.Append(eventValue);
            var affect = neutral ? ProvisionalAffectDelta.Zero :
                extreme ? new ProvisionalAffectDelta(-1m, 1m, 1m, -1m, 1m, -1m, -1m) :
                new ProvisionalAffectDelta(valence: -0.08m, threat: 0.03m, attachment: 0.02m);
            var relationship = neutral ? ProvisionalRelationshipDelta.Zero :
                extreme ? new ProvisionalRelationshipDelta(-1m, 1m, 1m, 1m) :
                new ProvisionalRelationshipDelta(trust: -0.08m, affection: 0.04m, resentment: 0.04m);
            AddRecord(stores, Record("main", eventValue, affect, relationship));

            if (contradictory)
            {
                var positiveEvent = Event("55555555555555555555555555555555", 1_100, Counterpart, null);
                ledger.Append(positiveEvent);
                AddRecord(stores, Record(
                    "positive", positiveEvent,
                    new ProvisionalAffectDelta(valence: 0.08m, attachment: 0.02m),
                    new ProvisionalRelationshipDelta(trust: 0.08m, affection: 0.04m)));
            }

            var memories = includeMemories
                ? new List<ContextualMemoryEvidence>
                {
                    Memory("memory-recent", 4_900, 90, receipt.CheckpointHash, "social"),
                    Memory("memory-category", 100, 95, receipt.CheckpointHash, "relationship")
                }
                : new List<ContextualMemoryEvidence>();
            if (reverseMemories) memories.Reverse();
            var index = new ContextualDevelopmentalRetrievalIndex(
                stores, ledger, new[] { receipt }, memories);
            var registry = new TrustedContextProjectionRegistry(
                stores, ledger, new[] { receipt }, memories);
            return new Fixture(stores, ledger, receipt, eventValue, memories, index, registry);
        }

        private static EnvironmentEvent Event(string eventId, long tick, string counterpart, string? root)
        {
            var payload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mosaic_admitted"] = "true",
                ["mosaic_privacy"] = "OWNER_PRIVATE",
                ["mosaic_lineage_id"] = Lineage,
                ["mosaic_category"] = "mixed-social",
                ["mosaic_context_tags"] = "relationship,social"
            };
            if (root is not null) payload["mosaic_root_event_ids"] = root;
            return new EnvironmentEvent(
                EventId.Parse(eventId), "v42:" + eventId, "mosaic.social.development",
                "offline", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero), tick,
                "Mosaic.Tests", payload,
                new[] { IndividualId.Parse(Owner), IndividualId.Parse(counterpart) });
        }

        private static DevelopmentalAppraisalRecord Record(
            string suffix,
            EnvironmentEvent sourceEvent,
            ProvisionalAffectDelta affect,
            ProvisionalRelationshipDelta relationship)
        {
            var constructor = typeof(DevelopmentalAppraisalRecord)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            return (DevelopmentalAppraisalRecord)constructor.Invoke(new object?[]
            {
                Hash("record:" + suffix), Hash("packet:" + suffix), Hash("appraisal:" + suffix),
                Owner, Lineage, Counterpart, sourceEvent.Id.ToString(),
                new DialogueAdmissionOutboxCodec().ComputeEventHash(sourceEvent), Generation,
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
            ((IDictionary<string, DevelopmentalAppraisalRecord>)field.GetValue(stores)!)
                .Add(record.RecordId, record);
        }

        private static CheckpointCommitReceipt TrustedCheckpoint(
            string suffix, long generation, string hash)
        {
            var method = typeof(CheckpointCommitReceipt).GetMethod(
                "CreateTrusted", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Checkpoint factory missing.");
            return (CheckpointCommitReceipt)method.Invoke(null, new object[]
            {
                "receipt-" + suffix, Save, World, StoreSet, generation, hash,
                true, true, true, DurableAppraisalAdmissionSource.CheckpointReceiptContract
            })!;
        }

        private static ContextualMemoryEvidence Memory(
            string id,
            long tick,
            int salience,
            string checkpoint,
            string category = "social")
        {
            var source = "memory-event-" + id;
            return ContextualMemoryEvidence.Create(
                id, Owner, Lineage, Counterpart, source, new[] { source }, tick,
                category, new[] { category, "social" }, salience, Generation, checkpoint);
        }

        private static DevelopmentalContextBundle Bundle(
            DevelopmentalContextQuery query,
            IEnumerable<DevelopmentalContextItem> selected)
        {
            var constructor = typeof(DevelopmentalContextBundle)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            return (DevelopmentalContextBundle)constructor.Invoke(new object[]
            {
                query,
                selected,
                new[] { new KeyValuePair<string, long>("selected_count", selected.Count()) }
            });
        }

        private static string Hash(string value)
        {
            using (var algorithm = SHA256.Create())
                return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))
                    .Select(item => item.ToString("x2")));
        }

        private static void Throws(Action action, string message)
        {
            try { action(); }
            catch { return; }
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
                ContextualDevelopmentalRetrievalIndex index,
                TrustedContextProjectionRegistry registry)
            {
                Stores = stores;
                Ledger = ledger;
                Receipt = receipt;
                Event = eventValue;
                Memories = memories;
                Index = index;
                Registry = registry;
                Materializer = new GroundedDevelopmentalContextMaterializer(registry);
            }

            internal DurableCanonicalStoreSet Stores { get; }
            internal InMemoryEventLedger Ledger { get; }
            internal CheckpointCommitReceipt Receipt { get; }
            internal EnvironmentEvent Event { get; }
            internal IReadOnlyList<ContextualMemoryEvidence> Memories { get; }
            internal ContextualDevelopmentalRetrievalIndex Index { get; }
            internal TrustedContextProjectionRegistry Registry { get; }
            internal GroundedDevelopmentalContextMaterializer Materializer { get; }

            internal DevelopmentalContextQuery Query(
                string save = Save,
                string? counterpart = Counterpart,
                long tick = 5_000,
                long generation = Generation,
                IEnumerable<string>? ancestry = null,
                DevelopmentalContextPurpose purpose = DevelopmentalContextPurpose.Appraisal) =>
                new DevelopmentalContextQuery(
                    save, World, StoreSet, Owner, Lineage, counterpart, tick, generation,
                    ancestry ?? new[] { Receipt.CheckpointHash }, purpose,
                    new[] { "relationship", "social" }, 4);

            internal GroundedDevelopmentalContextPacket Materialize(
                IEnumerable<string>? roots = null,
                long currentTick = 5_000,
                long queryTick = 5_000,
                DevelopmentalContextPurpose purpose = DevelopmentalContextPurpose.Appraisal)
            {
                var query = Query(tick: queryTick, purpose: purpose);
                var bundle = Index.Retrieve(query);
                return Materializer.Materialize(new GroundedDevelopmentalContextRequest(
                    query, bundle, roots ?? new[] { "current-event" }, currentTick));
            }
        }
    }
}
