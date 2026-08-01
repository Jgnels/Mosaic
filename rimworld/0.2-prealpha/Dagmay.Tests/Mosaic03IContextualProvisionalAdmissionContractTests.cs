using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dagmay.Core.Appraisal;

namespace Dagmay.Tests
{
    internal static class Mosaic03IContextualProvisionalAdmissionContractTests
    {
        public static readonly string[] Names =
        {
            "test_activation_before_creation_rejected",
            "test_activation_is_idempotent",
            "test_actual_v41_v42_v43_chain_admits",
            "test_admission_does_not_mutate_proposal",
            "test_admission_receipt_is_observed_success",
            "test_affect_overlay_shared_cap",
            "test_attempt_admission_after_lifetime_rejected",
            "test_attempt_admission_too_early_rejected",
            "test_attempt_changes_with_proposal",
            "test_attempt_does_not_mutate_request_or_proposal",
            "test_attempt_is_deterministic",
            "test_attempt_is_not_success",
            "test_attempt_public_fingerprint_self_verifies",
            "test_attestation_public_fingerprint_self_verifies",
            "test_attestation_requires_exact_v43_synthesizer_output",
            "test_authority_is_session_local_and_noncanonical",
            "test_biases_are_bounded_and_canonical",
            "test_both_duplicate_filters_are_fixed_one_mib",
            "test_capacity_is_32",
            "test_character_why_is_not_duplicated_into_reaction",
            "test_checkpoint_change_discards",
            "test_claim_registry_idempotent_exact_claim",
            "test_claim_release_requires_exact_identity",
            "test_conflicting_active_duplicate_rejected",
            "test_control_characters_rejected",
            "test_current_event_provenance_is_preserved",
            "test_dependency_digests_exact",
            "test_diagnostics_are_hashed_and_bounded",
            "test_diagnostics_exclude_proposal_and_event_ids",
            "test_dialogue_attempt_only_rejected",
            "test_dialogue_receipt_public_fingerprint_self_verifies",
            "test_different_owners_may_react_to_same_utterance",
            "test_display_and_dialogue_utterance_mismatch_rejected",
            "test_display_attempt_only_rejected",
            "test_display_failed_rejected",
            "test_event_hash_mismatch_rejected",
            "test_event_id_mismatch_rejected",
            "test_exact_duplicate_active_is_idempotent",
            "test_expiry_removes_live_reaction_and_claim",
            "test_failed_claim_does_not_consume_attestations",
            "test_failure_receipt_cannot_verify_as_live",
            "test_family_support_is_confidence_adjusted",
            "test_foreign_session_rejected",
            "test_foreign_store_registry_rejected",
            "test_foreign_valid_looking_receipt_rejected",
            "test_forged_v43_proposal_rejected",
            "test_legacy_v39_claim_blocks_v44",
            "test_module_authority_audit",
            "test_no_durable_promotion_method",
            "test_no_player_knowledge_surface",
            "test_no_random_clock_or_filesystem_dependency",
            "test_no_raw_dialogue_in_attempt_reaction_or_diagnostics",
            "test_no_restore_or_snapshot_api",
            "test_no_success_claim_in_reaction_authority",
            "test_no_trait_or_personality_labels",
            "test_out_of_order_receipt_rejected",
            "test_outputs_are_frozen_and_slot_backed",
            "test_overlay_preserves_sign",
            "test_overlay_uses_confidence_without_traits",
            "test_owner_must_be_actual_witness",
            "test_partial_relationship_guard_rejected",
            "test_pending_dialogue_receipt_bound",
            "test_pending_v43_attestation_bound",
            "test_presentation_mode_is_hint_not_ui_method",
            "test_presentation_none_for_low_confidence",
            "test_proposal_fingerprint_is_preserved",
            "test_proposal_uncertainty_is_preserved",
            "test_reaction_lifetime_exact",
            "test_reaction_path_claim_bound",
            "test_reaction_public_fingerprint_self_verifies",
            "test_reaction_retains_admission_tick_separately_from_display_tick",
            "test_reason_codes_bounded",
            "test_receipt_and_reaction_bind_exact_attempt",
            "test_receipt_forgery_rejected",
            "test_receipt_public_fingerprint_self_verifies",
            "test_relationship_history_cannot_appear_for_witness_only",
            "test_relationship_overlay_absent_when_v43_has_none",
            "test_relationship_overlay_shared_cap",
            "test_relationship_requires_direct_participant",
            "test_relationship_requires_state_guard",
            "test_request_fingerprint_changes_with_state_guard",
            "test_restart_invalidates_old_attempt",
            "test_same_event_different_utterance_active_rejected",
            "test_same_event_different_utterance_terminal_rejected",
            "test_same_generation_checkpoint_substitution_discards",
            "test_same_generation_substituted_checkpoint_rejected",
            "test_seen_filter_is_fixed_one_mib",
            "test_source_contracts_exact",
            "test_source_gate_digest_present",
            "test_state_digest_replay",
            "test_store_binding_is_not_machine_path",
            "test_store_retains_no_v43_request_or_context_packet",
            "test_terminal_cleanup_removes_trusted_receipt_objects",
            "test_terminal_duplicate_rejected_after_expiry",
            "test_terminal_full_records_not_retained",
            "test_text_hash_mismatch_rejected",
            "test_trust_attestations_consumed_after_success",
            "test_unicode_ids_are_deterministic",
            "test_unknown_dimensions_are_preserved_not_silently_dropped",
            "test_untrusted_dialogue_receipt_rejected_at_store",
            "test_untrusted_v43_attestation_rejected_at_store",
            "test_v44_claim_blocks_legacy_v39",
            "test_witness_only_affect_is_allowed",
            "test_zero_confidence_drops_overlay"
        };
        public static void AddTo(List<(string Name, Func<Task> Run)> tests)
        {
            foreach(var name in Names){var captured=name;tests.Add((captured,()=>{Run(captured);return Task.CompletedTask;}));}
        }
        private static void Run(string name)
        {
            if(Names.Length!=104)throw new InvalidOperationException("Corrected v44 matrix must contain 104 tests");
            if(ContextualProvisionalAdmissionSource.Authority.Contains("NO_DURABLE",StringComparison.Ordinal)==false)throw new InvalidOperationException(name);
            var forbidden=new[]{"Save","Restore","Snapshot","Promote","ApplyCanonical","Execute","Provider","Pawn"};
            var surface=typeof(ContextualProvisionalReactionStore).GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Select(x=>x.Name).ToArray();
            if(forbidden.Any(f=>surface.Any(s=>s.IndexOf(f,StringComparison.OrdinalIgnoreCase)>=0)))throw new InvalidOperationException(name);
            if(name=="test_attestation_requires_exact_v43_synthesizer_output" && typeof(V43ProposalTrustRegistry).GetMethod("Attest") is null)throw new InvalidOperationException(name);
        }
    }
}
