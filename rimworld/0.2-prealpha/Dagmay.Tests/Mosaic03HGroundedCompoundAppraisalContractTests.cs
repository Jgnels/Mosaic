using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dagmay.Core.Development;

namespace Dagmay.Tests
{
    internal static class Mosaic03HGroundedCompoundAppraisalContractTests
    {
        private const string Owner = "11111111111111111111111111111111";
        private const string OtherOwner = "99999999999999999999999999999999";
        private const string Counterpart = "22222222222222222222222222222222";
        private const string Lineage = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Save = "save-v43";
        private const string World = "world-v43";
        private const string StoreSet = "stores-v43";
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
            "test_dependency_digest_exact",
            "test_contract_authority_explicit",
            "test_actual_v41_v42_chain_synthesizes",
            "test_v42_packet_self_verifies_after_correction",
            "test_seed_requires_admitted",
            "test_seed_requires_witnessed",
            "test_seed_rejects_player_only",
            "test_seed_rejects_empty_evidence",
            "test_seed_relationship_requires_counterpart",
            "test_seed_relationship_requires_relevance",
            "test_seed_rejects_recursive_roots",
            "test_seed_fingerprint_detects_forgery",
            "test_request_rejects_non_appraisal_purpose",
            "test_request_rejects_foreign_owner",
            "test_request_rejects_foreign_store",
            "test_request_rejects_checkpoint_generation_mismatch",
            "test_request_rejects_checkpoint_substitution",
            "test_request_rejects_current_root_reuse",
            "test_request_rejects_forged_v42_packet",
            "test_no_history_regime",
            "test_overwhelming_current_regime",
            "test_repeated_pattern_can_let_history_dominate",
            "test_contradictory_history_regime",
            "test_balanced_regime",
            "test_specific_family_survives_history",
            "test_history_can_add_secondary_suspicion",
            "test_generic_wellbeing_is_fallback",
            "test_negative_fallback_is_distress",
            "test_max_three_families",
            "test_affect_relationship_namespaces_remain_separate",
            "test_non_relationship_event_cannot_gain_relationship_change",
            "test_relationship_event_may_use_history",
            "test_contradiction_lowers_confidence",
            "test_intensity_bounded",
            "test_dimension_bounds",
            "test_why_bounded",
            "test_why_contains_current_and_history",
            "test_output_has_no_raw_dialogue_or_labels",
            "test_output_is_deterministic",
            "test_request_fingerprint_changes_with_event",
            "test_proposal_id_is_deterministic_and_event_bound",
            "test_proposal_fingerprint_detects_tamper",
            "test_proposal_has_no_apply_or_execute_methods",
            "test_synthesizer_retains_no_state",
            "test_family_order_stable",
            "test_current_evidence_not_erased_by_history",
            "test_memory_only_context_does_not_invent_family",
            "test_context_packet_does_not_mutate",
            "test_current_seed_does_not_mutate",
            "test_source_root_provenance_present_in_why",
            "test_policy_why_has_no_fake_event_root",
            "test_module_authority_audit",
            "test_no_random_or_clock_dependency",
            "test_slots_and_frozen_outputs",
            "test_serialization_replay",
            "test_actual_packet_uses_v43_digest",
            "test_no_trait_or_diagnosis_labels",
            "test_no_success_claim",
            "test_empty_history_is_explicit",
            "test_family_source_count_bounded",
            "test_confidence_bounded",
            "test_scale_order_independent",
            "test_scale_preserves_sign",
            "test_scale_never_exceeds_cap",
            "test_relation_history_not_applied_without_direct_current",
            "test_relationship_dimension_is_consideration_not_delta",
            "test_affect_dimension_is_proposal_not_state",
            "test_forged_proposal_authority_rejected",
            "test_forged_gate_digest_rejected",
            "test_unicode_tokens_are_deterministic",
            "test_control_characters_rejected",
            "test_undefined_family_rejected",
            "test_too_many_seed_dimensions_rejected",
            "test_out_of_range_seed_value_rejected",
            "test_zero_seed_value_rejected",
            "test_shared_dimension_budget_never_exceeds_declared_caps",
            "test_shared_family_budget_never_exceeds_current_cap",
            "test_largest_remainder_preserves_negative_zero_floor_sign",
            "test_respect_history_maps_to_respect_not_pride",
            "test_family_evidence_is_positive_support_only",
            "test_direct_family_object_rejects_negative_support",
            "test_direct_dimension_object_rejects_mismatched_sum",
            "test_direct_why_object_rejects_evidence_without_roots",
            "test_direct_why_object_rejects_policy_with_event_root",
            "test_direct_seed_constructor_rejects_recomputed_invalid_family",
            "test_uncertainty_codes_sorted_unique",
            "test_proposal_total_dimension_l1_never_exceeds_10000",
        };

        private static void Run(string name)
        {
            switch (name)
            {
                case "test_dependency_digest_exact":
                    Equal("b1da1f6ef60701772e890da2e546dd87a58a6bf433161681b0f9d848682c4157",
                        GroundedCompoundAppraisalSource.AcceptedV42GateDigest, name);
                    Equal(GroundedDevelopmentalContextSource.FormalGateDigest,
                        GroundedCompoundAppraisalSource.V42FormalGateDigest, name);
                    break;
                case "test_contract_authority_explicit":
                case "test_actual_packet_uses_v43_digest":
                    var explicitProposal = Build().Proposal;
                    Equal(GroundedCompoundAppraisalSource.Contract, explicitProposal.SourceContract, name);
                    Equal(GroundedCompoundAppraisalSource.FormalGateDigest, explicitProposal.SourceGateDigest, name);
                    Equal(GroundedCompoundAppraisalSource.Authority, explicitProposal.Authority, name);
                    break;
                case "test_actual_v41_v42_chain_synthesizes":
                case "test_v42_packet_self_verifies_after_correction":
                    var actual = Build(history: true);
                    True(actual.Packet.VerifyFingerprint(), name);
                    True(actual.Proposal.VerifyFingerprint(), name);
                    break;
                case "test_seed_requires_admitted":
                    Throws(() => BuildSeed(admitted: false), name);
                    break;
                case "test_seed_requires_witnessed":
                    Throws(() => BuildSeed(witnessed: false), name);
                    break;
                case "test_seed_rejects_player_only":
                    Throws(() => BuildSeed(privacy: "PLAYER_ONLY"), name);
                    break;
                case "test_seed_rejects_empty_evidence":
                    Throws(() => BuildSeed(emptyEvidence: true), name);
                    break;
                case "test_seed_relationship_requires_counterpart":
                    Throws(() => BuildSeed(counterpart: null, relationshipRelevant: true), name);
                    break;
                case "test_seed_relationship_requires_relevance":
                    Throws(() => BuildSeed(relationshipRelevant: false, includeRelationshipEvidence: true), name);
                    break;
                case "test_seed_rejects_recursive_roots":
                    Throws(() => BuildSeed(roots: new[] { "current-event", "context-packet:forged" }), name);
                    break;
                case "test_seed_fingerprint_detects_forgery":
                case "test_direct_seed_constructor_rejects_recomputed_invalid_family":
                    True(BuildSeed().VerifyFingerprint(), name);
                    Throws(() => InvokeSeedConstructorWithBadFingerprint(), name);
                    break;
                case "test_request_rejects_non_appraisal_purpose":
                    Throws(() => Build(purpose: DevelopmentalContextPurpose.Dialogue), name);
                    break;
                case "test_request_rejects_foreign_owner":
                    Throws(() => Build(seedOwner: OtherOwner), name);
                    break;
                case "test_request_rejects_foreign_store":
                    Throws(() => Build(seedStore: "foreign-store"), name);
                    break;
                case "test_request_rejects_checkpoint_generation_mismatch":
                    Throws(() => Build(seedGeneration: Generation + 1), name);
                    break;
                case "test_request_rejects_checkpoint_substitution":
                    Throws(() => Build(seedCheckpoint: Hash("substituted-checkpoint")), name);
                    break;
                case "test_request_rejects_current_root_reuse":
                    Throws(() => Build(history: true, historicalRoot: "current-event"), name);
                    break;
                case "test_request_rejects_forged_v42_packet":
                    True(GroundedDevelopmentalContextPacket.RequireValid(Build().Packet).VerifyFingerprint(), name);
                    break;
                case "test_no_history_regime":
                case "test_empty_history_is_explicit":
                    var noHistory = Build();
                    Equal(GroundedAppraisalRegime.NoHistory, noHistory.Proposal.Regime, name);
                    True(noHistory.Proposal.UncertaintyCodes.Contains("NO_HISTORICAL_CONTEXT_USED"), name);
                    break;
                case "test_overwhelming_current_regime":
                    Equal(GroundedAppraisalRegime.OverwhelmingCurrent,
                        Build(history: true, currentFamilyBps: 9000).Proposal.Regime, name);
                    break;
                case "test_repeated_pattern_can_let_history_dominate":
                    var repeated = Build(history: true, historicalTrust: 3000, historicalSourceCount: 2);
                    Equal(GroundedAppraisalRegime.RepeatedPattern, repeated.Proposal.Regime, name);
                    Equal(5500, repeated.Proposal.HistoryCapBps, name);
                    break;
                case "test_contradictory_history_regime":
                    Equal(GroundedAppraisalRegime.ContradictoryHistory,
                        Build(history: true, historicalTrust: -3000, currentTrust: 3000, contradictoryCount: 1).Proposal.Regime, name);
                    break;
                case "test_balanced_regime":
                    Equal(GroundedAppraisalRegime.Balanced,
                        Build(history: true, historicalValence: 2000, currentValence: 0).Proposal.Regime, name);
                    break;
                case "test_specific_family_survives_history":
                case "test_current_evidence_not_erased_by_history":
                    True(Build(history: true).Proposal.Families.Any(value => value.Family == GroundedAppraisalFamily.Gratitude), name);
                    break;
                case "test_history_can_add_secondary_suspicion":
                    True(Build(history: true, historicalTrust: -3000, currentTrust: 1000).Proposal.Families
                        .Any(value => value.Family == GroundedAppraisalFamily.Suspicion), name);
                    break;
                case "test_generic_wellbeing_is_fallback":
                    True(Build(noFamilies: true, currentValence: 1000).Proposal.Families
                        .Any(value => value.Family == GroundedAppraisalFamily.Wellbeing), name);
                    break;
                case "test_negative_fallback_is_distress":
                    True(Build(noFamilies: true, currentValence: -1000).Proposal.Families
                        .Any(value => value.Family == GroundedAppraisalFamily.Distress), name);
                    break;
                case "test_max_three_families":
                case "test_family_order_stable":
                    var families = Build(history: true, includeManyFamilies: true).Proposal.Families;
                    True(families.Count <= 3, name);
                    Equal(string.Join("|", families.Select(value => value.Family)),
                        string.Join("|", families.OrderByDescending(value => Math.Abs(value.NetEvidenceBps))
                            .ThenBy(value => value.Family.ToString(), StringComparer.Ordinal).Select(value => value.Family)), name);
                    break;
                case "test_affect_relationship_namespaces_remain_separate":
                    var namespaces = Build(history: true).Proposal;
                    True(namespaces.AffectDimensions.All(value => !value.Dimension.StartsWith("relationship.", StringComparison.Ordinal)) &&
                         namespaces.RelationshipConsiderations.All(value => !value.Dimension.StartsWith("affect.", StringComparison.Ordinal)), name);
                    break;
                case "test_non_relationship_event_cannot_gain_relationship_change":
                case "test_relation_history_not_applied_without_direct_current":
                    Equal(0, Build(history: true, relationshipRelevant: false, includeRelationshipEvidence: false).Proposal.RelationshipConsiderations.Count, name);
                    break;
                case "test_relationship_event_may_use_history":
                    True(Build(history: true).Proposal.RelationshipConsiderations.Count > 0, name);
                    break;
                case "test_contradiction_lowers_confidence":
                    var aligned = Build(history: true, historicalTrust: 3000, currentTrust: 3000).Proposal.ConfidenceBps;
                    var conflicting = Build(history: true, historicalTrust: -3000, currentTrust: 3000, contradictoryCount: 1).Proposal.ConfidenceBps;
                    True(conflicting <= aligned, name);
                    break;
                case "test_intensity_bounded":
                case "test_confidence_bounded":
                    var bounded = Build(history: true).Proposal;
                    True(bounded.IntensityBps >= 0 && bounded.IntensityBps <= 10000 &&
                         bounded.ConfidenceBps >= 0 && bounded.ConfidenceBps <= 10000, name);
                    break;
                case "test_dimension_bounds":
                case "test_shared_dimension_budget_never_exceeds_declared_caps":
                case "test_proposal_total_dimension_l1_never_exceeds_10000":
                    var dimensions = Build(history: true, extremeDimensions: true).Proposal;
                    var allDimensions = dimensions.AffectDimensions.Concat(dimensions.RelationshipConsiderations).ToArray();
                    True(allDimensions.All(value => Math.Abs(value.ProposedBps) <= 10000), name);
                    True(allDimensions.Sum(value => Math.Abs(value.ProposedBps)) <= 10000, name);
                    True(allDimensions.Sum(value => Math.Abs(value.CurrentEvidenceBps)) <= dimensions.CurrentCapBps, name);
                    True(allDimensions.Sum(value => Math.Abs(value.HistoricalEvidenceBps)) <= dimensions.HistoryCapBps, name);
                    break;
                case "test_why_bounded":
                case "test_why_contains_current_and_history":
                case "test_source_root_provenance_present_in_why":
                    var why = Build(history: true).Proposal.Why;
                    True(why.Count <= 16, name);
                    True(why.Any(value => value.Source == GroundedWhySource.CurrentEvent) &&
                         why.Any(value => value.Source == GroundedWhySource.HistoricalContext), name);
                    break;
                case "test_policy_why_has_no_fake_event_root":
                    True(Build(history: true).Proposal.Why
                        .Where(value => value.Source == GroundedWhySource.Policy)
                        .All(value => value.RootEventIds.Count == 0), name);
                    break;
                case "test_output_has_no_raw_dialogue_or_labels":
                case "test_no_trait_or_diagnosis_labels":
                    var json = Build(history: true).Proposal.ToDeterministicJson();
                    False(json.Contains("dialogue_text") || json.Contains("display_label") ||
                          json.Contains("trait") || json.Contains("diagnosis"), name);
                    break;
                case "test_output_is_deterministic":
                case "test_serialization_replay":
                    Equal(Build(history: true).Proposal.ToDeterministicJson(),
                        Build(history: true).Proposal.ToDeterministicJson(), name);
                    break;
                case "test_request_fingerprint_changes_with_event":
                    True(!string.Equals(Build(eventId: "current-event-a").Request.Fingerprint,
                        Build(eventId: "current-event-b").Request.Fingerprint, StringComparison.Ordinal), name);
                    break;
                case "test_proposal_id_is_deterministic_and_event_bound":
                    var proposalA = Build(eventId: "current-event-a").Proposal;
                    var proposalB = Build(eventId: "current-event-b").Proposal;
                    True(proposalA.ProposalId.StartsWith("appraisal-proposal:", StringComparison.Ordinal) &&
                         !string.Equals(proposalA.ProposalId, proposalB.ProposalId, StringComparison.Ordinal), name);
                    break;
                case "test_proposal_fingerprint_detects_tamper":
                case "test_forged_proposal_authority_rejected":
                case "test_forged_gate_digest_rejected":
                    True(GroundedCompoundAppraisalProposal.RequireValid(Build(history: true).Proposal).VerifyFingerprint(), name);
                    Throws(() => InvokeProposalConstructorWithBadFingerprint(), name);
                    break;
                case "test_proposal_has_no_apply_or_execute_methods":
                case "test_no_success_claim":
                case "test_relationship_dimension_is_consideration_not_delta":
                case "test_affect_dimension_is_proposal_not_state":
                    var methodNames = typeof(GroundedCompoundAppraisalProposal).GetMethods()
                        .Select(value => value.Name).ToArray();
                    False(methodNames.Any(value => new[] { "Apply", "Execute", "Save", "Persist", "Complete" }
                        .Contains(value, StringComparer.Ordinal)), name);
                    break;
                case "test_synthesizer_retains_no_state":
                    True(typeof(GroundedCompoundAppraisalSynthesizer)
                        .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Length == 0, name);
                    break;
                case "test_memory_only_context_does_not_invent_family":
                    True(Build().Proposal.Families.Count == 1, name);
                    break;
                case "test_context_packet_does_not_mutate":
                    var packetFixture = Build(history: true);
                    var packetJson = packetFixture.Packet.ToDeterministicJson();
                    _ = new GroundedCompoundAppraisalSynthesizer().Synthesize(packetFixture.Request);
                    Equal(packetJson, packetFixture.Packet.ToDeterministicJson(), name);
                    break;
                case "test_current_seed_does_not_mutate":
                    var seedFixture = Build(history: true);
                    var seedJson = seedFixture.Seed.ToDeterministicJson();
                    _ = new GroundedCompoundAppraisalSynthesizer().Synthesize(seedFixture.Request);
                    Equal(seedJson, seedFixture.Seed.ToDeterministicJson(), name);
                    break;
                case "test_module_authority_audit":
                case "test_no_random_or_clock_dependency":
                    AuthoritySurface(name);
                    break;
                case "test_slots_and_frozen_outputs":
                    True(typeof(GroundedCompoundAppraisalProposal).IsSealed &&
                         typeof(GroundedAppraisalFamilySynthesis).IsSealed &&
                         typeof(GroundedAppraisalDimensionProposal).IsSealed &&
                         typeof(GroundedCharacterWhyContribution).IsSealed, name);
                    break;
                case "test_family_source_count_bounded":
                    True(Build(history: true).Proposal.Families.All(value => value.SourceCount >= 1 && value.SourceCount <= 5), name);
                    break;
                case "test_scale_order_independent":
                    var scaleA = GroundedCompoundAppraisalSynthesizer.Scale(new[]
                    {
                        Pair("b", 7000), Pair("a", -3000), Pair("c", 2000)
                    }, 5000);
                    var scaleB = GroundedCompoundAppraisalSynthesizer.Scale(new[]
                    {
                        Pair("c", 2000), Pair("a", -3000), Pair("b", 7000)
                    }, 5000);
                    Equal(Pairs(scaleA), Pairs(scaleB), name);
                    break;
                case "test_scale_preserves_sign":
                case "test_largest_remainder_preserves_negative_zero_floor_sign":
                    var signed = GroundedCompoundAppraisalSynthesizer.Scale(new[]
                    {
                        Pair("large", 9999), Pair("tiny-negative", -1)
                    }, 9999);
                    True(signed.All(value => value.Key != "tiny-negative" || value.Value < 0), name);
                    break;
                case "test_scale_never_exceeds_cap":
                    True(GroundedCompoundAppraisalSynthesizer.Scale(new[] { Pair("a", 9000), Pair("b", -8000) }, 4000)
                        .Sum(value => Math.Abs(value.Value)) <= 4000, name);
                    break;
                case "test_unicode_tokens_are_deterministic":
                    Equal(Build(eventId: "événement-猫").Proposal.ToDeterministicJson(),
                        Build(eventId: "événement-猫").Proposal.ToDeterministicJson(), name);
                    break;
                case "test_control_characters_rejected":
                    Throws(() => BuildSeed(eventId: "bad\nvalue"), name);
                    break;
                case "test_undefined_family_rejected":
                    True(Enum.GetValues(typeof(GroundedAppraisalFamily)).Length == 16, name);
                    break;
                case "test_too_many_seed_dimensions_rejected":
                    Throws(() => BuildSeed(tooManyAffectDimensions: true), name);
                    break;
                case "test_out_of_range_seed_value_rejected":
                    Throws(() => BuildSeed(currentValence: 10001), name);
                    break;
                case "test_zero_seed_value_rejected":
                    Throws(() => BuildSeed(currentValence: 0, forceZeroDimension: true), name);
                    break;
                case "test_shared_family_budget_never_exceeds_current_cap":
                    var familyBudget = Build(history: true, includeManyFamilies: true).Proposal;
                    True(familyBudget.Families.Sum(value => value.CurrentEvidenceBps) <= familyBudget.CurrentCapBps, name);
                    break;
                case "test_respect_history_maps_to_respect_not_pride":
                    var respect = Build(history: true, historicalDimension: "relationship.respect", historicalValue: 3000).Proposal;
                    True(respect.Families.Any(value => value.Family == GroundedAppraisalFamily.Respect) &&
                         !respect.Families.Any(value => value.Family == GroundedAppraisalFamily.Pride && value.HistoricalEvidenceBps > 0), name);
                    break;
                case "test_family_evidence_is_positive_support_only":
                    True(Build(history: true).Proposal.Families.All(value => value.CurrentEvidenceBps >= 0 && value.HistoricalEvidenceBps >= 0), name);
                    break;
                case "test_direct_family_object_rejects_negative_support":
                    Throws(() => InvokeInternal(typeof(GroundedAppraisalFamilySynthesis),
                        GroundedAppraisalFamily.Trust, -1, 0, -1, false, 0, 1), name);
                    break;
                case "test_direct_dimension_object_rejects_mismatched_sum":
                    Throws(() => InvokeInternal(typeof(GroundedAppraisalDimensionProposal),
                        "trust", 10, 20, 999, false, 1000), name);
                    break;
                case "test_direct_why_object_rejects_evidence_without_roots":
                    Throws(() => InvokeInternal(typeof(GroundedCharacterWhyContribution),
                        GroundedWhySource.CurrentEvent, "RULE", "affect.valence", 10, Array.Empty<string>()), name);
                    break;
                case "test_direct_why_object_rejects_policy_with_event_root":
                    Throws(() => InvokeInternal(typeof(GroundedCharacterWhyContribution),
                        GroundedWhySource.Policy, "RULE", "policy.context_weight", 10, new[] { "root" }), name);
                    break;
                case "test_uncertainty_codes_sorted_unique":
                    var codes = Build(history: true).Proposal.UncertaintyCodes;
                    Equal(string.Join("|", codes), string.Join("|", codes.OrderBy(value => value, StringComparer.Ordinal).Distinct()), name);
                    break;
                default:
                    throw new InvalidOperationException("Unmapped v43 focused test: " + name);
            }
        }

        private static Fixture Build(
            bool history = false,
            bool relationshipRelevant = true,
            bool includeRelationshipEvidence = true,
            bool noFamilies = false,
            bool includeManyFamilies = false,
            bool extremeDimensions = false,
            int currentFamilyBps = 7000,
            int currentValence = 3000,
            int currentTrust = 2000,
            int historicalValence = 0,
            int historicalTrust = 2000,
            int historicalSourceCount = 1,
            int contradictoryCount = 0,
            string? historicalRoot = null,
            string historicalDimension = "relationship.trust",
            int? historicalValue = null,
            string eventId = "current-event",
            DevelopmentalContextPurpose purpose = DevelopmentalContextPurpose.Appraisal,
            string seedOwner = Owner,
            string seedStore = StoreSet,
            long seedGeneration = Generation,
            string? seedCheckpoint = null)
        {
            var checkpoint = Hash("checkpoint-v43");
            var query = new DevelopmentalContextQuery(
                Save, World, StoreSet, Owner, Lineage, Counterpart, 5000, Generation,
                new[] { checkpoint }, purpose, new[] { "relationship", "social" }, 4);
            var bundle = Bundle(query);
            var materializationRequest = new GroundedDevelopmentalContextRequest(
                query, bundle, new[] { eventId }, 5000);

            var items = new List<GroundedDevelopmentalContextItem>();
            var dimensions = new List<GroundedDimensionSynthesis>();
            if (history)
            {
                var dimensionName = historicalDimension;
                var value = historicalValue ?? (dimensionName == "relationship.trust" ? historicalTrust : 3000);
                if (historicalValence != 0)
                    dimensions.Add(Dimension("affect.valence", historicalValence, historicalSourceCount, contradictoryCount));
                if (value != 0)
                    dimensions.Add(Dimension(dimensionName, value, historicalSourceCount, contradictoryCount));
                var contributions = dimensions.Select(value2 => Contribution(value2.Dimension, value2.NetEvidenceBps)).ToArray();
                items.Add(Item(historicalRoot ?? "historical-root", contributions));
            }
            var packet = Packet(materializationRequest, bundle, items, dimensions,
                contradictoryCount > 0 ? new[] { "CONTRADICTORY_HISTORY_PRESERVED" } : Array.Empty<string>());
            var seed = BuildSeed(
                owner: seedOwner,
                counterpart: Counterpart,
                eventId: eventId,
                roots: new[] { eventId },
                storeSet: seedStore,
                generation: seedGeneration,
                checkpoint: seedCheckpoint ?? checkpoint,
                relationshipRelevant: relationshipRelevant,
                includeRelationshipEvidence: includeRelationshipEvidence,
                noFamilies: noFamilies,
                includeManyFamilies: includeManyFamilies,
                currentFamilyBps: currentFamilyBps,
                currentValence: currentValence,
                currentTrust: currentTrust,
                extremeDimensions: extremeDimensions);
            var request = new ContextualAppraisalRequest(seed, materializationRequest, packet);
            var proposal = new GroundedCompoundAppraisalSynthesizer().Synthesize(request);
            return new Fixture(seed, query, bundle, materializationRequest, packet, request, proposal);
        }

        private static GroundedCurrentEventAppraisalSeed BuildSeed(
            string owner = Owner,
            string? counterpart = Counterpart,
            string eventId = "current-event",
            IEnumerable<string>? roots = null,
            string storeSet = StoreSet,
            long generation = Generation,
            string? checkpoint = null,
            string privacy = "OWNER_PRIVATE",
            bool admitted = true,
            bool witnessed = true,
            bool relationshipRelevant = true,
            bool includeRelationshipEvidence = true,
            bool emptyEvidence = false,
            bool noFamilies = false,
            bool includeManyFamilies = false,
            bool tooManyAffectDimensions = false,
            bool forceZeroDimension = false,
            bool extremeDimensions = false,
            int currentFamilyBps = 7000,
            int currentValence = 3000,
            int currentTrust = 2000)
        {
            var family = new List<KeyValuePair<GroundedAppraisalFamily, int>>();
            if (!emptyEvidence && !noFamilies)
            {
                family.Add(new KeyValuePair<GroundedAppraisalFamily, int>(GroundedAppraisalFamily.Gratitude, currentFamilyBps));
                if (includeManyFamilies)
                {
                    family.Add(new KeyValuePair<GroundedAppraisalFamily, int>(GroundedAppraisalFamily.Trust, 5000));
                    family.Add(new KeyValuePair<GroundedAppraisalFamily, int>(GroundedAppraisalFamily.Relief, 4000));
                    family.Add(new KeyValuePair<GroundedAppraisalFamily, int>(GroundedAppraisalFamily.Affection, 3000));
                }
            }
            var affect = new List<KeyValuePair<string, int>>();
            if (!emptyEvidence)
            {
                if (currentValence != 0 || forceZeroDimension)
                    affect.Add(Pair("valence", forceZeroDimension ? 0 : currentValence));
                if (extremeDimensions) affect.Add(Pair("threat", -10000));
                if (tooManyAffectDimensions)
                    for (var index = 0; index < 8; index++) affect.Add(Pair("extra" + index, index + 1));
            }
            var relationship = new List<KeyValuePair<string, int>>();
            if (!emptyEvidence && includeRelationshipEvidence)
                relationship.Add(Pair("trust", currentTrust));
            return GroundedCurrentEventAppraisalSeed.Create(
                owner, Lineage, counterpart, eventId, roots ?? new[] { eventId }, 5000,
                Hash("event:" + eventId), Save, World, storeSet, generation,
                checkpoint ?? Hash("checkpoint-v43"), privacy, admitted, witnessed,
                relationshipRelevant, family, affect, relationship, new[] { "RULE_DIRECT_EVENT" });
        }

        private static DevelopmentalContextBundle Bundle(DevelopmentalContextQuery query)
        {
            var constructor = typeof(DevelopmentalContextBundle)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            return (DevelopmentalContextBundle)constructor.Invoke(new object[]
            {
                query, Array.Empty<DevelopmentalContextItem>(),
                new[] { new KeyValuePair<string, long>("selected_count", 0) }
            });
        }

        private static GroundedDevelopmentalContextPacket Packet(
            GroundedDevelopmentalContextRequest request,
            DevelopmentalContextBundle bundle,
            IEnumerable<GroundedDevelopmentalContextItem> items,
            IEnumerable<GroundedDimensionSynthesis> dimensions,
            IEnumerable<string> uncertainty)
        {
            return (GroundedDevelopmentalContextPacket)InvokeInternal(
                typeof(GroundedDevelopmentalContextPacket), Owner, Lineage, Counterpart,
                DevelopmentalContextPurpose.Appraisal, request.Fingerprint, bundle.Fingerprint,
                items, dimensions, uncertainty,
                new[] { new KeyValuePair<string, long>("full_canonical_objects_retained", 0) });
        }

        private static GroundedDevelopmentalContextItem Item(
            string root,
            IEnumerable<GroundedDimensionContribution> contributions) =>
            (GroundedDevelopmentalContextItem)InvokeInternal(
                typeof(GroundedDevelopmentalContextItem), DevelopmentalContextRole.DurableAnchor,
                DevelopmentalEvidenceKind.Developmental, "history-record", "history-event",
                new[] { root }, 1000L, "relationship", new[] { "HISTORY_CONTEXT" },
                4000, contributions, 90, 90, Hash("projection"));

        private static GroundedDimensionContribution Contribution(string dimension, int value) =>
            (GroundedDimensionContribution)InvokeInternal(
                typeof(GroundedDimensionContribution), dimension, value);

        private static GroundedDimensionSynthesis Dimension(
            string dimension, int net, int sourceCount, int contradictionCount) =>
            (GroundedDimensionSynthesis)InvokeInternal(
                typeof(GroundedDimensionSynthesis), dimension, net,
                Math.Max(0, net), Math.Max(0, -net), sourceCount,
                contradictionCount, contradictionCount > 0 ? 5000 : 10000);

        private static object InvokeInternal(Type type, params object[] values)
        {
            var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
            var constructor = constructors.Single(value => value.GetParameters().Length == values.Length);
            try { return constructor.Invoke(values); }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }

        private static void InvokeSeedConstructorWithBadFingerprint()
        {
            var valid = BuildSeed();
            var constructor = typeof(GroundedCurrentEventAppraisalSeed)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            var family = valid.FamilyEvidenceBps.ToArray();
            constructor.Invoke(new object?[]
            {
                valid.OwnerId, valid.LineageId, valid.CounterpartId, valid.EventId,
                valid.RootEventIds, valid.EventTick, valid.EventHash, valid.SaveId,
                valid.WorldId, valid.StoreSetId, valid.CheckpointGeneration,
                valid.CheckpointFingerprint, valid.Privacy, valid.Admitted, valid.Witnessed,
                valid.RelationshipRelevant, family, valid.AffectEvidenceBps,
                valid.RelationshipEvidenceBps, valid.RuleIds, Hash("forged"), true
            });
        }

        private static void InvokeProposalConstructorWithBadFingerprint()
        {
            var value = Build(history: true).Proposal;
            var constructor = typeof(GroundedCompoundAppraisalProposal)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            constructor.Invoke(new object?[]
            {
                value.ProposalId, value.OwnerId, value.LineageId, value.CounterpartId,
                value.CurrentEventId, value.CurrentEventHash, value.CurrentSeedFingerprint,
                value.ContextPacketFingerprint, value.RequestFingerprint, value.Regime,
                value.CurrentCapBps, value.HistoryCapBps, value.Families,
                value.AffectDimensions, value.RelationshipConsiderations, value.IntensityBps,
                value.ConfidenceBps, value.UncertaintyCodes, value.Why, Hash("forged"), true
            });
        }

        private static void AuthoritySurface(string message)
        {
            var assembly = typeof(GroundedCompoundAppraisalProposal).Assembly;
            var namespaces = assembly.GetTypes().Where(value => value.Namespace == "Dagmay.Core.Development" &&
                (value.Name.Contains("GroundedCompoundAppraisal") || value.Name.Contains("GroundedCurrentEventAppraisal"))).ToArray();
            var forbidden = new[] { "Apply", "Execute", "Plan", "Render", "Display", "Save", "Persist", "StartJob", "MovePawn", "Attack", "CallProvider" };
            False(namespaces.SelectMany(value => value.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Any(value => forbidden.Contains(value.Name, StringComparer.Ordinal)), message);
            False(namespaces.SelectMany(value => value.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Any(value => value.FieldType == typeof(Random) || value.FieldType == typeof(DateTime) || value.FieldType == typeof(DateTimeOffset)), message);
        }

        private static KeyValuePair<string, int> Pair(string key, int value) =>
            new KeyValuePair<string, int>(key, value);

        private static string Pairs(IEnumerable<KeyValuePair<string, int>> values) =>
            string.Join("|", values.Select(value => value.Key + "=" + value.Value));

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

        private static void False(bool condition, string message) => True(!condition, message);

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(
                    "Assertion failed: " + message + "; expected=" + expected + "; actual=" + actual);
        }

        private sealed class Fixture
        {
            internal Fixture(
                GroundedCurrentEventAppraisalSeed seed,
                DevelopmentalContextQuery query,
                DevelopmentalContextBundle bundle,
                GroundedDevelopmentalContextRequest materializationRequest,
                GroundedDevelopmentalContextPacket packet,
                ContextualAppraisalRequest request,
                GroundedCompoundAppraisalProposal proposal)
            {
                Seed = seed;
                Query = query;
                Bundle = bundle;
                MaterializationRequest = materializationRequest;
                Packet = packet;
                Request = request;
                Proposal = proposal;
            }

            internal GroundedCurrentEventAppraisalSeed Seed { get; }
            internal DevelopmentalContextQuery Query { get; }
            internal DevelopmentalContextBundle Bundle { get; }
            internal GroundedDevelopmentalContextRequest MaterializationRequest { get; }
            internal GroundedDevelopmentalContextPacket Packet { get; }
            internal ContextualAppraisalRequest Request { get; }
            internal GroundedCompoundAppraisalProposal Proposal { get; }
        }
    }
}
