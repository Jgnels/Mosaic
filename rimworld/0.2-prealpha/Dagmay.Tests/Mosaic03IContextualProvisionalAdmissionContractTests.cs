using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Development;
using Dagmay.Core.Presentation;

namespace Dagmay.Tests
{
    internal static class Mosaic03IContextualProvisionalAdmissionContractTests
    {
        private const string Owner="11111111111111111111111111111111";
        private const string OtherOwner="99999999999999999999999999999999";
        private const string Counterpart="22222222222222222222222222222222";
        private const string Lineage="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Save="save-v43";
        private const string World="world-v43";
        private const string Stores="stores-v43";
        private const string Session="session-v44";
        private const long Generation=3;
        private static readonly string Checkpoint=Hash("checkpoint-v43");
        private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;

        public static readonly string[] Names =
        {
            "test_dependency_digests_exact",
            "test_authority_is_session_local_and_noncanonical",
            "test_actual_v41_v42_v43_chain_admits",
            "test_attempt_is_not_success",
            "test_admission_receipt_is_observed_success",
            "test_display_attempt_only_rejected",
            "test_display_failed_rejected",
            "test_dialogue_attempt_only_rejected",
            "test_forged_v43_proposal_rejected",
            "test_untrusted_v43_attestation_rejected_at_store",
            "test_untrusted_dialogue_receipt_rejected_at_store",
            "test_owner_must_be_actual_witness",
            "test_display_and_dialogue_utterance_mismatch_rejected",
            "test_text_hash_mismatch_rejected",
            "test_event_id_mismatch_rejected",
            "test_event_hash_mismatch_rejected",
            "test_same_generation_substituted_checkpoint_rejected",
            "test_foreign_session_rejected",
            "test_foreign_store_registry_rejected",
            "test_relationship_requires_direct_participant",
            "test_witness_only_affect_is_allowed",
            "test_relationship_requires_state_guard",
            "test_partial_relationship_guard_rejected",
            "test_affect_overlay_shared_cap",
            "test_relationship_overlay_shared_cap",
            "test_overlay_preserves_sign",
            "test_overlay_uses_confidence_without_traits",
            "test_unknown_dimensions_are_preserved_not_silently_dropped",
            "test_biases_are_bounded_and_canonical",
            "test_presentation_none_for_low_confidence",
            "test_attempt_is_deterministic",
            "test_attempt_changes_with_proposal",
            "test_exact_duplicate_active_is_idempotent",
            "test_conflicting_active_duplicate_rejected",
            "test_out_of_order_receipt_rejected",
            "test_terminal_duplicate_rejected_after_expiry",
            "test_activation_is_idempotent",
            "test_activation_before_creation_rejected",
            "test_expiry_removes_live_reaction_and_claim",
            "test_checkpoint_change_discards",
            "test_same_generation_checkpoint_substitution_discards",
            "test_restart_invalidates_old_attempt",
            "test_capacity_is_32",
            "test_legacy_v39_claim_blocks_v44",
            "test_v44_claim_blocks_legacy_v39",
            "test_different_owners_may_react_to_same_utterance",
            "test_no_durable_promotion_method",
            "test_no_raw_dialogue_in_attempt_reaction_or_diagnostics",
            "test_diagnostics_are_hashed_and_bounded",
            "test_receipt_forgery_rejected",
            "test_foreign_valid_looking_receipt_rejected",
            "test_proposal_uncertainty_is_preserved",
            "test_character_why_is_not_duplicated_into_reaction",
            "test_proposal_fingerprint_is_preserved",
            "test_current_event_provenance_is_preserved",
            "test_store_retains_no_v43_request_or_context_packet",
            "test_module_authority_audit",
            "test_no_random_clock_or_filesystem_dependency",
            "test_outputs_are_frozen_and_slot_backed",
            "test_control_characters_rejected",
            "test_unicode_ids_are_deterministic",
            "test_state_digest_replay",
            "test_source_gate_digest_present",
            "test_attempt_public_fingerprint_self_verifies",
            "test_reaction_public_fingerprint_self_verifies",
            "test_receipt_public_fingerprint_self_verifies",
            "test_dialogue_receipt_public_fingerprint_self_verifies",
            "test_attestation_public_fingerprint_self_verifies",
            "test_reason_codes_bounded",
            "test_terminal_full_records_not_retained",
            "test_seen_filter_is_fixed_one_mib",
            "test_attempt_admission_too_early_rejected",
            "test_attempt_admission_after_lifetime_rejected",
            "test_relationship_history_cannot_appear_for_witness_only",
            "test_family_support_is_confidence_adjusted",
            "test_no_trait_or_personality_labels",
            "test_no_player_knowledge_surface",
            "test_no_success_claim_in_reaction_authority",
            "test_failure_receipt_cannot_verify_as_live",
            "test_claim_registry_idempotent_exact_claim",
            "test_claim_release_requires_exact_identity",
            "test_diagnostics_exclude_proposal_and_event_ids",
            "test_source_contracts_exact",
            "test_request_fingerprint_changes_with_state_guard",
            "test_attempt_does_not_mutate_request_or_proposal",
            "test_admission_does_not_mutate_proposal",
            "test_presentation_mode_is_hint_not_ui_method",
            "test_relationship_overlay_absent_when_v43_has_none",
            "test_zero_confidence_drops_overlay",
            "test_receipt_and_reaction_bind_exact_attempt",
            "test_reaction_lifetime_exact",
            "test_no_restore_or_snapshot_api",
            "test_store_binding_is_not_machine_path",
            "test_same_event_different_utterance_active_rejected",
            "test_same_event_different_utterance_terminal_rejected",
            "test_both_duplicate_filters_are_fixed_one_mib",
            "test_trust_attestations_consumed_after_success",
            "test_failed_claim_does_not_consume_attestations",
            "test_terminal_cleanup_removes_trusted_receipt_objects",
            "test_pending_v43_attestation_bound",
            "test_pending_dialogue_receipt_bound",
            "test_reaction_path_claim_bound",
            "test_reaction_retains_admission_tick_separately_from_display_tick",
            "test_v43_factory_rejects_rehashed_non_synthesizer_output"
        };

        public static void AddTo(List<(string Name,Func<Task> Run)> tests)
        {
            foreach(var testName in Names)
            {
                var captured=testName;
                tests.Add((captured,()=>{Run(captured);return Task.CompletedTask;}));
            }
        }

        private static void Run(string name)
        {
            switch(name)
            {
                case "test_dependency_digests_exact": { Equal("045ef8a2fd6084c183ced7d2ec9516b3289a309f0b3c55963a895746c70de0d8",GroundedCompoundAppraisalSource.FormalGateDigest,name);Equal("f44b00eee5a2fe8d7609bbd80caa79948caf388760f2e33765ec64ad50c3e0d8",ObservedDisplayReceipt.SourceGateDigestValue,name); break; }
                case "test_authority_is_session_local_and_noncanonical": { Equal("SESSION_LOCAL_PROVISIONAL_ONLY_NO_DURABLE_NO_CANONICAL_NO_PERSISTENCE_NO_PROVIDER_NO_PLANNER_NO_UI_NO_PAWN_AUTHORITY",ContextualProvisionalAdmissionSource.Authority,name); break; }
                case "test_actual_v41_v42_v43_chain_admits": { var c=Chain();var admitted=c.Admit();Equal(ContextualReactionState.Prepared,admitted.Item1.State,name); break; }
                case "test_attempt_is_not_success": { Equal(ContextualAdmissionOutcome.AttemptOnly,Chain().Attempt.Outcome,name); break; }
                case "test_admission_receipt_is_observed_success": { Equal(ContextualAdmissionOutcome.ObservedSuccess,Chain().Admit().Item2.Outcome,name); break; }
                case "test_display_attempt_only_rejected": { var h=Harness();Throws(()=>h.Make("event-a","utt-a",displayOutcome:ObservedDisplayOutcome.ATTEMPT_ONLY),name); break; }
                case "test_display_failed_rejected": { var h=Harness();Throws(()=>h.Make("event-a","utt-a",displayOutcome:ObservedDisplayOutcome.FAILED),name); break; }
                case "test_dialogue_attempt_only_rejected": { var c=Chain();var bad=Tamper(c.Request.DialogueReceipt,"Outcome",ContextualAdmissionOutcome.AttemptOnly);Throws(()=>Request(c,dialogue:bad),name); break; }
                case "test_forged_v43_proposal_rejected": { var v=V43();var bad=Tamper(v.Proposal,"Fingerprint",Hash("forged"));Throws(()=>GroundedCompoundAppraisalProposal.RequireValid(bad),name); break; }
                case "test_untrusted_v43_attestation_rejected_at_store": { var c=Chain();var foreign=new V43ProposalTrustRegistry(Save,World,Stores).Attest(c.V43.Request,c.V43.Proposal);var bad=Request(c,attestation:foreign);Throws(()=>c.Store.PrepareAttempt(bad),name); break; }
                case "test_untrusted_dialogue_receipt_rejected_at_store": { var c=Chain();var registry=new DialogueAdmissionTrustRegistry(Save,World,Stores);var foreign=registry.Create(c.V43.Proposal.CurrentEventId,c.V43.Proposal.CurrentEventHash,c.Request.DisplayReceipt,1001,Checkpoint);var bad=Request(c,dialogue:foreign);Throws(()=>c.Store.PrepareAttempt(bad),name); break; }
                case "test_owner_must_be_actual_witness": { var h=Harness();Throws(()=>h.Make("event-a","utt-a",witness:false,direct:false,includeRelationship:false),name); break; }
                case "test_display_and_dialogue_utterance_mismatch_rejected": { var c=Chain();var other=TrustedDisplay("other",1000,Owner);var bad=Tamper(c.Request.DialogueReceipt,"DisplayReceipt",other);Throws(()=>Request(c,dialogue:bad),name); break; }
                case "test_text_hash_mismatch_rejected": { var c=Chain();var display=Tamper(c.Request.DisplayReceipt,"ExactTextHash",Hash("other-text"));var bad=Tamper(c.Request.DialogueReceipt,"DisplayReceipt",display);Throws(()=>Request(c,display:display,dialogue:bad),name); break; }
                case "test_event_id_mismatch_rejected": { var c=Chain();var bad=Tamper(c.Request.DialogueReceipt,"EventId","other-event");Throws(()=>Request(c,dialogue:bad),name); break; }
                case "test_event_hash_mismatch_rejected": { var c=Chain();var bad=Tamper(c.Request.DialogueReceipt,"EventHash",Hash("other-event"));Throws(()=>Request(c,dialogue:bad),name); break; }
                case "test_same_generation_substituted_checkpoint_rejected": { var c=Chain();var bad=Tamper(c.Request.DialogueReceipt,"CheckpointFingerprint",Hash("substitute"));Throws(()=>Request(c,dialogue:bad),name); break; }
                case "test_foreign_session_rejected": { var h=Harness();Throws(()=>h.Make("event-a","utt-a",session:"foreign-session"),name); break; }
                case "test_foreign_store_registry_rejected": { Throws(()=>new ContextualProvisionalReactionStore(Session,Save,World,Stores,Generation,Checkpoint,new V43ProposalTrustRegistry("foreign",World,Stores),new DialogueAdmissionTrustRegistry(Save,World,Stores)),name); break; }
                case "test_relationship_requires_direct_participant": { var h=Harness();Throws(()=>h.Make("event-a","utt-a",direct:false,includeRelationship:true),name); break; }
                case "test_witness_only_affect_is_allowed": { var h=Harness();var b=h.Make("event-a","utt-a",direct:false,includeRelationship:false);True(b.Attempt.AffectOverlay.Count>0,name); break; }
                case "test_relationship_requires_state_guard": { var c=Chain();Throws(()=>new ContextualAdmissionRequest(Session,c.V43.Proposal,c.Request.ProposalAttestation,c.Request.DisplayReceipt,c.Request.DialogueReceipt,Hash("affect"),1,null,null),name); break; }
                case "test_partial_relationship_guard_rejected": { var c=Chain();Throws(()=>new ContextualAdmissionRequest(Session,c.V43.Proposal,c.Request.ProposalAttestation,c.Request.DisplayReceipt,c.Request.DialogueReceipt,Hash("affect"),1,Hash("rel"),null),name); break; }
                case "test_affect_overlay_shared_cap": { var a=Chain().Attempt.AffectOverlay;True(a.Sum(x=>Math.Abs(x.OverlayBps))<=1500,name); break; }
                case "test_relationship_overlay_shared_cap": { var a=Chain().Attempt.RelationshipOverlay;True(a.Sum(x=>Math.Abs(x.OverlayBps))<=800,name); break; }
                case "test_overlay_preserves_sign": { var b=Harness().Make("negative","negative",valence:-4000);True(b.Attempt.AffectOverlay.All(x=>x.OverlayBps<0),name); break; }
                case "test_overlay_uses_confidence_without_traits": { var a=Chain().Attempt.AffectOverlay.Single();Equal(a.SourceBps*Chain().Attempt.ConfidenceBps/10000,a.ConfidenceAdjustedBps,name); break; }
                case "test_unknown_dimensions_are_preserved_not_silently_dropped": { var b=Harness().Make("unknown","unknown",affectDimension:"affect.unknown");True(b.Attempt.AffectOverlay.Any(x=>x.Dimension.Contains("unknown")),name); break; }
                case "test_biases_are_bounded_and_canonical": { var x=Chain().Attempt.Biases;True(x.Count<=4&&x.SequenceEqual(x.OrderBy(v=>v,StringComparer.Ordinal)),name); break; }
                case "test_presentation_none_for_low_confidence": { var b=Harness().Make("low","low",family:1000,valence:1000,trust:0,includeRelationship:false);Equal(ContextualPresentationMode.None,b.Attempt.PresentationMode,name); break; }
                case "test_attempt_is_deterministic": { Equal(Chain().Attempt.Fingerprint,Chain().Attempt.Fingerprint,name); break; }
                case "test_attempt_changes_with_proposal": { True(Chain("event-a").Attempt.Fingerprint!=Chain("event-b").Attempt.Fingerprint,name); break; }
                case "test_exact_duplicate_active_is_idempotent": { var c=Chain();var first=c.Admit();var second=c.Store.Admit(c.Request,c.Attempt,1002);True(ReferenceEquals(first.Item1,second.Item1)&&ReferenceEquals(first.Item2,second.Item2),name); break; }
                case "test_conflicting_active_duplicate_rejected": { var c=Chain();c.Admit();var bad=Tamper(c.Attempt,"Fingerprint",Hash("conflict"));Throws(()=>c.Store.Admit(c.Request,bad,1002),name); break; }
                case "test_out_of_order_receipt_rejected": { var h=Harness();var first=h.Make("late","late",tick:2000);h.Store.Admit(first.Request,first.Attempt,2002);var early=h.Make("early","early",tick:1000);Throws(()=>h.Store.Admit(early.Request,early.Attempt,1002),name); break; }
                case "test_terminal_duplicate_rejected_after_expiry": { var c=Chain();c.Admit();c.Store.Expire(16000);Throws(()=>c.Store.Admit(c.Request,c.Attempt,1002),name); break; }
                case "test_activation_is_idempotent": { var c=Chain();var r=c.Admit().Item1;var a=c.Store.Activate(r.ReactionId,1002);True(ReferenceEquals(a,c.Store.Activate(r.ReactionId,1003)),name); break; }
                case "test_activation_before_creation_rejected": { Throws(()=>Chain().Store.Activate("missing",1000),name); break; }
                case "test_expiry_removes_live_reaction_and_claim": { var c=Chain();c.Admit();Equal(1,c.Store.Expire(16000),name);Equal(0,c.Store.ActiveCount(),name); break; }
                case "test_checkpoint_change_discards": { var c=Chain();c.Admit();Equal(1,c.Store.InvalidateCheckpoint(4,Hash("new-checkpoint")),name); break; }
                case "test_same_generation_checkpoint_substitution_discards": { var c=Chain();c.Admit();Equal(1,c.Store.InvalidateCheckpoint(Generation,Hash("substituted")),name); break; }
                case "test_restart_invalidates_old_attempt": { var c=Chain();var restart=Harness();Throws(()=>restart.Store.Admit(c.Request,c.Attempt,1002),name); break; }
                case "test_capacity_is_32": { var h=Harness();for(var i=0;i<33;i++){var b=h.Make("cap-"+i,"cap-"+i,tick:1000+i);h.Store.Admit(b.Request,b.Attempt,1002+i);}Equal(32,h.Store.ActiveCount(Owner),name); break; }
                case "test_legacy_v39_claim_blocks_v44": { var h=Harness();var b=h.Make("claim","claim");h.Claims.Claim(Owner,"claim",ProvisionalReactionPath.LegacyV39Cue,"legacy");Throws(()=>h.Store.Admit(b.Request,b.Attempt,1002),name); break; }
                case "test_v44_claim_blocks_legacy_v39": { var c=Chain();c.Admit();Throws(()=>c.Claims.Claim(Owner,"utt",ProvisionalReactionPath.LegacyV39Cue,"legacy"),name); break; }
                case "test_different_owners_may_react_to_same_utterance": { var h=Harness();var a=h.Make("e-a","same",owner:Owner);h.Store.Admit(a.Request,a.Attempt,1002);var b=h.Make("e-b","same",owner:OtherOwner,tick:1010);h.Store.Admit(b.Request,b.Attempt,1012);Equal(2,h.Store.ActiveCount(),name); break; }
                case "test_no_durable_promotion_method": { NoMethods(typeof(ContextualProvisionalReactionStore),name,"Promote","Apply","Commit"); break; }
                case "test_no_raw_dialogue_in_attempt_reaction_or_diagnostics": { var c=Chain();var r=c.Admit().Item1;False(string.Join("|",r.ReasonCodes).Contains("transient",StringComparison.OrdinalIgnoreCase),name); break; }
                case "test_diagnostics_are_hashed_and_bounded": { var d=Chain().Store.DiagnosticProjection();True(d.SessionIdHash.Length==64&&d.StateDigest.Length==64&&d.SeenFilterBytes==1048576,name); break; }
                case "test_receipt_forgery_rejected": { var c=Chain();var receipt=c.Admit().Item2;var bad=Tamper(receipt,"Fingerprint",Hash("forged"));Throws(()=>c.Store.VerifyReceipt(bad),name); break; }
                case "test_foreign_valid_looking_receipt_rejected": { var a=Chain();var receipt=a.Admit().Item2;var b=Chain("other");Throws(()=>b.Store.VerifyReceipt(receipt),name); break; }
                case "test_proposal_uncertainty_is_preserved": { var c=Chain();True(c.Attempt.UncertaintyCodes.SequenceEqual(c.V43.Proposal.UncertaintyCodes),name); break; }
                case "test_character_why_is_not_duplicated_into_reaction": { var c=Chain();var r=c.Admit().Item1;True(r.GetType().GetProperty("Why")==null,name); break; }
                case "test_proposal_fingerprint_is_preserved": { var c=Chain();Equal(c.V43.Proposal.Fingerprint,c.Attempt.ProposalFingerprint,name); break; }
                case "test_current_event_provenance_is_preserved": { var c=Chain();var r=c.Admit().Item1;Equal(c.V43.Proposal.CurrentEventId,r.CurrentEventId,name);Equal(c.V43.Proposal.CurrentEventHash,r.CurrentEventHash,name); break; }
                case "test_store_retains_no_v43_request_or_context_packet": { False(typeof(ContextualProvisionalReactionStore).GetFields(Flags).Any(f=>f.FieldType==typeof(ContextualAppraisalRequest)||f.FieldType==typeof(GroundedDevelopmentalContextPacket)),name); break; }
                case "test_module_authority_audit": { NoMethods(typeof(ContextualProvisionalReactionStore),name,"Save","Restore","Snapshot","Provider","Plan","Execute","Render","Display","Pawn","Job"); break; }
                case "test_no_random_clock_or_filesystem_dependency": { False(typeof(ContextualProvisionalReactionStore).Assembly.GetTypes().Where(t=>t.Namespace=="Dagmay.Core.Appraisal"&&t.Name.Contains("Contextual")).SelectMany(t=>t.GetFields(Flags)).Any(f=>f.FieldType==typeof(Random)||f.FieldType==typeof(DateTime)||f.FieldType==typeof(DateTimeOffset)),name); break; }
                case "test_outputs_are_frozen_and_slot_backed": { True(typeof(ContextualProvisionalReaction).GetProperties().All(p=>p.SetMethod==null)&&typeof(ContextualProvisionalAdmissionAttempt).GetProperties().All(p=>p.SetMethod==null),name); break; }
                case "test_control_characters_rejected": { Throws(()=>new V43ProposalTrustRegistry("bad\nvalue",World,Stores),name); break; }
                case "test_unicode_ids_are_deterministic": { Equal(Chain("événement-猫").Attempt.Fingerprint,Chain("événement-猫").Attempt.Fingerprint,name); break; }
                case "test_state_digest_replay": { var a=Chain();a.Admit();var b=Chain();b.Admit();Equal(a.Store.StateDigest(),b.Store.StateDigest(),name); break; }
                case "test_source_gate_digest_present": { Equal(64,ContextualProvisionalAdmissionSource.FormalGateDigest.Length,name); break; }
                case "test_attempt_public_fingerprint_self_verifies": { var c=Chain();Equal(c.Attempt.Fingerprint,c.Store.PrepareAttempt(c.Request).Fingerprint,name); break; }
                case "test_reaction_public_fingerprint_self_verifies": { var a=Chain().Admit().Item1;var b=Chain().Admit().Item1;Equal(a.Fingerprint,b.Fingerprint,name); break; }
                case "test_receipt_public_fingerprint_self_verifies": { var a=Chain().Admit().Item2;var b=Chain().Admit().Item2;Equal(a.Fingerprint,b.Fingerprint,name); break; }
                case "test_dialogue_receipt_public_fingerprint_self_verifies": { True(Chain().Request.DialogueReceipt.VerifyFingerprint(),name); break; }
                case "test_attestation_public_fingerprint_self_verifies": { True(Chain().Request.ProposalAttestation.VerifyFingerprint(),name); break; }
                case "test_reason_codes_bounded": { var r=Chain().Attempt.ReasonCodes;True(r.Count<=24&&r.SequenceEqual(r.OrderBy(x=>x,StringComparer.Ordinal)),name); break; }
                case "test_terminal_full_records_not_retained": { var c=Chain();var id=c.Admit().Item1.ReactionId;c.Store.Expire(16000);Throws(()=>c.Store.Get(id),name); break; }
                case "test_seen_filter_is_fixed_one_mib": { Equal(1048576,ContextualProvisionalAdmissionSource.FilterBytes,name); break; }
                case "test_attempt_admission_too_early_rejected": { var c=Chain();Throws(()=>c.Store.Admit(c.Request,c.Attempt,1000),name); break; }
                case "test_attempt_admission_after_lifetime_rejected": { var c=Chain();Throws(()=>c.Store.Admit(c.Request,c.Attempt,16000),name); break; }
                case "test_relationship_history_cannot_appear_for_witness_only": { var b=Harness().Make("w","w",direct:false,includeRelationship:false);Equal(0,b.Attempt.RelationshipOverlay.Count,name); break; }
                case "test_family_support_is_confidence_adjusted": { var c=Chain();var expected=c.V43.Proposal.Families[0].NetEvidenceBps*c.V43.Proposal.ConfidenceBps/10000;Equal(expected,c.Attempt.Families[0].Value,name); break; }
                case "test_no_trait_or_personality_labels": { var text=string.Join("|",Chain().Attempt.Biases.Concat(Chain().Attempt.ReasonCodes));False(text.Contains("TRAIT",StringComparison.OrdinalIgnoreCase)||text.Contains("PERSONALITY",StringComparison.OrdinalIgnoreCase),name); break; }
                case "test_no_player_knowledge_surface": { False(typeof(ContextualProvisionalReactionStore).GetMethods(Flags).Any(m=>m.Name.Contains("Player",StringComparison.OrdinalIgnoreCase)||m.Name.Contains("Knowledge",StringComparison.OrdinalIgnoreCase)),name); break; }
                case "test_no_success_claim_in_reaction_authority": { False(ContextualProvisionalAdmissionSource.Authority.Contains("SUCCESS",StringComparison.Ordinal),name); break; }
                case "test_failure_receipt_cannot_verify_as_live": { var c=Chain();var receipt=c.Admit().Item2;var failed=Tamper(receipt,"Outcome",ContextualAdmissionOutcome.Failed);Throws(()=>c.Store.VerifyReceipt(failed),name); break; }
                case "test_claim_registry_idempotent_exact_claim": { var r=new ReactionPathClaimRegistry();r.Claim(Owner,"u",ProvisionalReactionPath.ContextualV44,"id");r.Claim(Owner,"u",ProvisionalReactionPath.ContextualV44,"id");Equal(1,r.Count,name); break; }
                case "test_claim_release_requires_exact_identity": { var r=new ReactionPathClaimRegistry();r.Claim(Owner,"u",ProvisionalReactionPath.ContextualV44,"id");r.Release(Owner,"u",ProvisionalReactionPath.ContextualV44,"wrong");Equal(1,r.Count,name); break; }
                case "test_diagnostics_exclude_proposal_and_event_ids": { var c=Chain();c.Admit();var d=c.Store.DiagnosticProjection();False(d.GetType().GetProperties().Any(p=>p.Name.Contains("Proposal")||p.Name.Contains("EventId")),name); break; }
                case "test_source_contracts_exact": { Equal("Mosaic.Core.DialogueEventAdmissionReceipt.v1",ContextualProvisionalAdmissionSource.DialogueAdmissionContract,name);Equal("Mosaic.Core.GroundedCompoundAppraisalAttestation.v1",ContextualProvisionalAdmissionSource.AttestationContract,name);Equal("Mosaic.Core.ContextualProvisionalAdmissionReceipt.v1",ContextualProvisionalAdmissionSource.ReceiptContract,name); break; }
                case "test_request_fingerprint_changes_with_state_guard": { var c=Chain();var changed=new ContextualAdmissionRequest(Session,c.V43.Proposal,c.Request.ProposalAttestation,c.Request.DisplayReceipt,c.Request.DialogueReceipt,Hash("different-affect"),2,Hash("relationship"),2);True(c.Request.Fingerprint!=changed.Fingerprint,name); break; }
                case "test_attempt_does_not_mutate_request_or_proposal": { var c=Chain();var request=c.Request.Fingerprint;var proposal=c.V43.Proposal.ToDeterministicJson();c.Store.PrepareAttempt(c.Request);Equal(request,c.Request.Fingerprint,name);Equal(proposal,c.V43.Proposal.ToDeterministicJson(),name); break; }
                case "test_admission_does_not_mutate_proposal": { var c=Chain();var before=c.V43.Proposal.ToDeterministicJson();c.Admit();Equal(before,c.V43.Proposal.ToDeterministicJson(),name); break; }
                case "test_presentation_mode_is_hint_not_ui_method": { True(Enum.IsDefined(typeof(ContextualPresentationMode),Chain().Attempt.PresentationMode),name);NoMethods(typeof(ContextualProvisionalReactionStore),name,"Render","Display"); break; }
                case "test_relationship_overlay_absent_when_v43_has_none": { Equal(0,Harness().Make("norel","norel",includeRelationship:false).Attempt.RelationshipOverlay.Count,name); break; }
                case "test_zero_confidence_drops_overlay": { var b=Harness().Make("zero","zero",valence:0,trust:0,includeRelationship:false,family:1000);Equal(0,b.Attempt.AffectOverlay.Count,name); break; }
                case "test_receipt_and_reaction_bind_exact_attempt": { var c=Chain();var x=c.Admit();Equal(x.Item1.AttemptId,x.Item2.AttemptId,name);Equal(x.Item1.ReactionId,x.Item2.ReactionId,name); break; }
                case "test_reaction_lifetime_exact": { var r=Chain().Admit().Item1;Equal(15000L,r.ExpiresTick-r.CreatedTick,name); break; }
                case "test_no_restore_or_snapshot_api": { NoMethods(typeof(ContextualProvisionalReactionStore),name,"Restore","Snapshot"); break; }
                case "test_store_binding_is_not_machine_path": { var d=Chain().Store.DiagnosticProjection();False(d.BindingHash.Contains("\\")||d.BindingHash.Contains(":"),name); break; }
                case "test_same_event_different_utterance_active_rejected": { var h=Harness();var a=h.Make("event","u1");h.Store.Admit(a.Request,a.Attempt,1002);var b=h.Make("event","u2",tick:1010);Throws(()=>h.Store.Admit(b.Request,b.Attempt,1012),name); break; }
                case "test_same_event_different_utterance_terminal_rejected": { var h=Harness();var a=h.Make("event","u1");h.Store.Admit(a.Request,a.Attempt,1002);h.Store.Expire(16000);var b=h.Make("event","u2",tick:17000);Throws(()=>h.Store.Admit(b.Request,b.Attempt,17002),name); break; }
                case "test_both_duplicate_filters_are_fixed_one_mib": { var d=Chain().Store.DiagnosticProjection();Equal(1048576,d.SeenFilterBytes,name);Equal(1048576,d.EventFilterBytes,name); break; }
                case "test_trust_attestations_consumed_after_success": { var c=Chain();c.Admit();Equal(0,c.Proposals.Count,name);Equal(0,c.Dialogue.Count,name); break; }
                case "test_failed_claim_does_not_consume_attestations": { var h=Harness();var b=h.Make("blocked","blocked");h.Claims.Claim(Owner,"blocked",ProvisionalReactionPath.LegacyV39Cue,"legacy");Throws(()=>h.Store.Admit(b.Request,b.Attempt,1002),name);Equal(1,h.Proposals.Count,name);Equal(1,h.Dialogue.Count,name); break; }
                case "test_terminal_cleanup_removes_trusted_receipt_objects": { var c=Chain();var receipt=c.Admit().Item2;c.Store.Expire(16000);Throws(()=>c.Store.VerifyReceipt(receipt),name); break; }
                case "test_pending_v43_attestation_bound": { var r=new V43ProposalTrustRegistry(Save,World,Stores);for(var i=0;i<64;i++){var v=V43("v43-"+i);r.Attest(v.Request,v.Proposal);}var extra=V43("v43-over");Throws(()=>r.Attest(extra.Request,extra.Proposal),name); break; }
                case "test_pending_dialogue_receipt_bound": { var r=new DialogueAdmissionTrustRegistry(Save,World,Stores);for(var i=0;i<64;i++){var d=TrustedDisplay("d-"+i,1000+i,Owner);r.Create("e-"+i,Hash("event:e-"+i),d,1001+i,Checkpoint);}var x=TrustedDisplay("over",2000,Owner);Throws(()=>r.Create("over",Hash("event:over"),x,2001,Checkpoint),name); break; }
                case "test_reaction_path_claim_bound": { var r=new ReactionPathClaimRegistry();for(var i=0;i<1024;i++)r.Claim(Owner,"u-"+i,ProvisionalReactionPath.ContextualV44,"c-"+i);Throws(()=>r.Claim(Owner,"overflow",ProvisionalReactionPath.ContextualV44,"overflow"),name); break; }
                case "test_reaction_retains_admission_tick_separately_from_display_tick": { var c=Chain();var r=c.Admit().Item1;Equal(1000L,r.CreatedTick,name);Equal(1002L,r.AdmittedTick,name); break; }
                case "test_v43_factory_rejects_rehashed_non_synthesizer_output": { var v=V43();var forged=Tamper(v.Proposal,"ProposalId","appraisal-proposal:"+Hash("alternate").Substring(0,32));Throws(()=>new V43ProposalTrustRegistry(Save,World,Stores).Attest(v.Request,forged),name); break; }
                default: throw new InvalidOperationException("Unmapped v44 semantic test: "+name);
            }
        }

        private static ChainFixture Chain(string eventId="event",string utterance="utt")
        {
            var harness=Harness();
            return harness.Make(eventId,utterance);
        }

        private static HarnessFixture Harness() => new HarnessFixture();

        private static ContextualAdmissionRequest Request(
            ChainFixture source,V43ProposalAttestation? attestation=null,ObservedDisplayReceipt? display=null,
            ContextualAdmittedDialogueEventReceipt? dialogue=null)
        {
            return new ContextualAdmissionRequest(source.Request.SessionId,source.V43.Proposal,
                attestation??source.Request.ProposalAttestation,display??source.Request.DisplayReceipt,
                dialogue??source.Request.DialogueReceipt,source.Request.CanonicalAffectFingerprint,
                source.Request.CanonicalAffectVersion,source.Request.RelationshipFingerprint,source.Request.RelationshipVersion);
        }

        private static V43Fixture V43(string eventId="event",string owner=Owner,bool includeRelationship=true,
            int family=7000,int valence=3000,int trust=2000,string affectDimension="affect.valence")
        {
            var lineage=owner==Owner?Lineage:Hash("lineage:"+owner).Substring(0,32);
            var query=new DevelopmentalContextQuery(Save,World,Stores,owner,lineage,Counterpart,5000,Generation,
                new[]{Checkpoint},DevelopmentalContextPurpose.Appraisal,new[]{"relationship","social"},4);
            var bundle=(DevelopmentalContextBundle)InvokeInternal(typeof(DevelopmentalContextBundle),query,
                Array.Empty<DevelopmentalContextItem>(),new[]{new KeyValuePair<string,long>("selected_count",0)});
            var materialization=new GroundedDevelopmentalContextRequest(query,bundle,new[]{eventId},5000);
            var packet=(GroundedDevelopmentalContextPacket)InvokeInternal(typeof(GroundedDevelopmentalContextPacket),
                owner,lineage,Counterpart,DevelopmentalContextPurpose.Appraisal,materialization.Fingerprint,bundle.Fingerprint,
                Array.Empty<GroundedDevelopmentalContextItem>(),Array.Empty<GroundedDimensionSynthesis>(),Array.Empty<string>(),
                new[]{new KeyValuePair<string,long>("full_canonical_objects_retained",0)});
            var families=family==0?Array.Empty<KeyValuePair<GroundedAppraisalFamily,int>>():
                new[]{new KeyValuePair<GroundedAppraisalFamily,int>(GroundedAppraisalFamily.Gratitude,family)};
            var affect=valence==0?Array.Empty<KeyValuePair<string,int>>():
                new[]{new KeyValuePair<string,int>(affectDimension,valence)};
            var relationship=includeRelationship&&trust!=0?
                new[]{new KeyValuePair<string,int>("relationship.trust",trust)}:Array.Empty<KeyValuePair<string,int>>();
            var seed=GroundedCurrentEventAppraisalSeed.Create(owner,lineage,Counterpart,eventId,new[]{eventId},5000,
                Hash("event:"+eventId),Save,World,Stores,Generation,Checkpoint,"OWNER_PRIVATE",true,true,
                includeRelationship,families,affect,relationship,new[]{"RULE_DIRECT_EVENT"});
            var request=new ContextualAppraisalRequest(seed,materialization,packet);
            return new V43Fixture(request,new GroundedCompoundAppraisalSynthesizer().Synthesize(request));
        }

        private static ObservedDisplayReceipt TrustedDisplay(
            string utterance,long tick,string owner,ObservedDisplayOutcome outcome=ObservedDisplayOutcome.OBSERVED_SUCCESS,
            string session=Session,bool witness=true,bool direct=true)
        {
            var speaker=direct?Counterpart:"33333333333333333333333333333333";
            var recipient=direct?owner:"44444444444444444444444444444444";
            var audience=witness?new[]{owner,speaker,recipient}.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray():
                new[]{speaker,recipient}.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            if(outcome!=ObservedDisplayOutcome.OBSERVED_SUCCESS)
                return ObservedDisplayReceipt.CreateNonSuccess(session,Hash("attempt:"+utterance),Hash("receipt:"+utterance),
                    utterance,"conversation:"+utterance,speaker,recipient,audience,Hash("text:"+utterance),tick,Generation,
                    outcome,ObservedDisplayReceipt.SourceContractValue,ObservedDisplayReceipt.SourceGateDigestValue);
            var method=typeof(ObservedDisplayReceipt).GetMethod("CreateTrusted",BindingFlags.Static|BindingFlags.NonPublic)
                ??throw new InvalidOperationException("Trusted display factory missing.");
            return (ObservedDisplayReceipt)(method.Invoke(null,new object?[]{session,Hash("attempt:"+utterance),
                Hash("receipt:"+utterance),utterance,"conversation:"+utterance,speaker,recipient,audience,
                Hash("text:"+utterance),tick,Generation,outcome,ObservedDisplayReceipt.SourceContractValue,
                ObservedDisplayReceipt.SourceGateDigestValue})??throw new InvalidOperationException("Trusted display factory failed."));
        }

        private static object InvokeInternal(Type type,params object[] values)
        {
            var constructor=type.GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic)
                .Single(candidate=>candidate.GetParameters().Length==values.Length);
            try{return constructor.Invoke(values);}
            catch(TargetInvocationException exception)when(exception.InnerException is not null){throw exception.InnerException;}
        }

        private static T Tamper<T>(T source,string propertyName,object? replacement) where T:class
        {
            var clone=(T)RuntimeHelpers.GetUninitializedObject(typeof(T));
            foreach(var field in typeof(T).GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public))
                field.SetValue(clone,field.GetValue(source));
            var target=typeof(T).GetField("<"+propertyName+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)
                ??throw new InvalidOperationException("Backing field missing: "+propertyName);
            target.SetValue(clone,replacement);
            return clone;
        }

        private static void NoMethods(Type type,string message,params string[] fragments)
        {
            var names=type.GetMethods(Flags).Select(x=>x.Name).ToArray();
            True(!fragments.Any(fragment=>names.Any(value=>value.IndexOf(fragment,StringComparison.OrdinalIgnoreCase)>=0)),message);
        }
        private static string Hash(string value)
        {
            using(var algorithm=SHA256.Create())
                return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(x=>x.ToString("x2")));
        }
        private static void Throws(Action action,string message){try{action();}catch{return;}throw new InvalidOperationException("Expected failure: "+message);}
        private static void True(bool condition,string message){if(!condition)throw new InvalidOperationException("Assertion failed: "+message);}
        private static void False(bool condition,string message)=>True(!condition,message);
        private static void Equal<T>(T expected,T actual,string message)
        {if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new InvalidOperationException("Assertion failed: "+message+" expected="+expected+" actual="+actual);}

        private sealed class V43Fixture
        {
            internal V43Fixture(ContextualAppraisalRequest request,GroundedCompoundAppraisalProposal proposal){Request=request;Proposal=proposal;}
            internal ContextualAppraisalRequest Request{get;} internal GroundedCompoundAppraisalProposal Proposal{get;}
        }

        private sealed class ChainFixture
        {
            internal ChainFixture(HarnessFixture harness,V43Fixture v43,ContextualAdmissionRequest request,ContextualProvisionalAdmissionAttempt attempt)
            {Harness=harness;V43=v43;Request=request;Attempt=attempt;}
            internal HarnessFixture Harness{get;} internal V43Fixture V43{get;} internal ContextualAdmissionRequest Request{get;}
            internal ContextualProvisionalAdmissionAttempt Attempt{get;} internal ContextualProvisionalReactionStore Store=>Harness.Store;
            internal V43ProposalTrustRegistry Proposals=>Harness.Proposals; internal DialogueAdmissionTrustRegistry Dialogue=>Harness.Dialogue;
            internal ReactionPathClaimRegistry Claims=>Harness.Claims;
            internal Tuple<ContextualProvisionalReaction,ObservedProvisionalAdmissionReceipt> Admit()=>Store.Admit(Request,Attempt,Request.DisplayReceipt.DisplayedTick+2);
        }

        private sealed class HarnessFixture
        {
            internal HarnessFixture()
            {
                Proposals=new V43ProposalTrustRegistry(Save,World,Stores);
                Dialogue=new DialogueAdmissionTrustRegistry(Save,World,Stores);
                Claims=new ReactionPathClaimRegistry();
                Store=new ContextualProvisionalReactionStore(Session,Save,World,Stores,Generation,Checkpoint,Proposals,Dialogue,Claims);
            }
            internal V43ProposalTrustRegistry Proposals{get;} internal DialogueAdmissionTrustRegistry Dialogue{get;}
            internal ReactionPathClaimRegistry Claims{get;} internal ContextualProvisionalReactionStore Store{get;}
            internal ChainFixture Make(string eventId,string utterance,string owner=Owner,bool includeRelationship=true,
                int family=7000,int valence=3000,int trust=2000,string affectDimension="affect.valence",long tick=1000,
                string session=Session,ObservedDisplayOutcome displayOutcome=ObservedDisplayOutcome.OBSERVED_SUCCESS,
                bool witness=true,bool direct=true)
            {
                var v43=V43(eventId,owner,includeRelationship,family,valence,trust,affectDimension);
                var attestation=Proposals.Attest(v43.Request,v43.Proposal);
                var display=TrustedDisplay(utterance,tick,owner,displayOutcome,session,witness,direct);
                var dialogue=Dialogue.Create(v43.Proposal.CurrentEventId,v43.Proposal.CurrentEventHash,display,tick+1,Checkpoint);
                var request=new ContextualAdmissionRequest(session,v43.Proposal,attestation,display,dialogue,Hash("affect"),1,
                    includeRelationship?Hash("relationship"):null,includeRelationship?1L:(long?)null);
                return new ChainFixture(this,v43,request,Store.PrepareAttempt(request));
            }
        }
    }
}
