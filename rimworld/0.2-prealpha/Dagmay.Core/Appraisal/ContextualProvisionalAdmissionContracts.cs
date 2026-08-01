using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dagmay.Core.Development;
using Dagmay.Core.Presentation;

namespace Dagmay.Core.Appraisal
{
    public enum ContextualAdmissionOutcome { AttemptOnly, ObservedSuccess, Failed }
    public enum ContextualReactionState { Prepared, Active, Expired, Discarded }
    public enum ContextualPresentationMode { None, Icon, Thought }
    public enum ProvisionalReactionPath { LegacyV39Cue, ContextualV44 }

    public static class ContextualProvisionalAdmissionSource
    {
        public const string Contract = "Mosaic.Core.ContextualProvisionalReactionAdmission.v1";
        public const string FormalGateDigest = "50fcd9611b04799e50a2e3e71d2402abbbade4343a96e8b56daf479805361ef5";
        public const string Authority = "SESSION_LOCAL_PROVISIONAL_ONLY_NO_DURABLE_NO_CANONICAL_NO_PERSISTENCE_NO_PROVIDER_NO_PLANNER_NO_UI_NO_PAWN_AUTHORITY";
        public const string DialogueAdmissionContract = "Mosaic.Core.DialogueEventAdmissionReceipt.v1";
        public const string DialogueAdmissionAuthority = "TRUSTED_CANONICAL_EVENT_ADMISSION_EVIDENCE_ONLY";
        public const string AttestationContract = "Mosaic.Core.GroundedCompoundAppraisalAttestation.v1";
        public const string AttestationAuthority = "TRUSTED_V43_PROPOSAL_BINDING_NO_MUTATION_AUTHORITY";
        public const string ReceiptContract = "Mosaic.Core.ContextualProvisionalAdmissionReceipt.v1";
        public const string ReceiptAuthority = "STORE_OBSERVED_PROVISIONAL_ADMISSION_NO_CANONICAL_OR_PAWN_AUTHORITY";
        public const int MaximumActivePerOwner = 32;
        public const int MaximumGlobalActive = 1024;
        public const int MaximumTrackedOwners = 256;
        public const int MaximumPendingAttestations = 64;
        public const int MaximumPathClaims = 1024;
        public const int LifetimeTicks = 15000;
        public const int AffectOverlayCapBps = 1500;
        public const int RelationshipOverlayCapBps = 800;
        public const int FilterBytes = ProvisionalSeenFilter.ByteCount;
        public const int MaximumBiases = 4;
        public const int MaximumReasonCodes = 24;

        internal static string Token(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.Any(character => character < 32))
                throw new ArgumentException("Invalid token.", name);
            return value;
        }

        internal static string Sha(string value, string name)
        {
            if (value is null || value.Length != 64 ||
                value.Any(character => !char.IsDigit(character) && (character < 'a' || character > 'f')))
                throw new ArgumentException("Invalid canonical SHA-256.", name);
            return value;
        }

        internal static string Hash(string value)
        {
            using (var algorithm = SHA256.Create())
                return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))
                    .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }

        internal static string Json(string value) => GroundedJson.String(value);
        internal static string Optional(string? value) => value is null ? "null" : Json(value);
        internal static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    }

    public sealed class V43ProposalAttestation
    {
        internal V43ProposalAttestation(
            GroundedCompoundAppraisalProposal proposal, long checkpointGeneration,
            string checkpointFingerprint, string saveId, string worldId, string storeSetId)
        {
            ProposalFingerprint = proposal.Fingerprint;
            ProposalId = proposal.ProposalId;
            OwnerId = proposal.OwnerId;
            LineageId = proposal.LineageId;
            CounterpartId = proposal.CounterpartId;
            CurrentEventId = proposal.CurrentEventId;
            CurrentEventHash = proposal.CurrentEventHash;
            CurrentSeedFingerprint = proposal.CurrentSeedFingerprint;
            ContextPacketFingerprint = proposal.ContextPacketFingerprint;
            RequestFingerprint = proposal.RequestFingerprint;
            CheckpointGeneration = checkpointGeneration;
            CheckpointFingerprint = ContextualProvisionalAdmissionSource.Sha(checkpointFingerprint, nameof(checkpointFingerprint));
            SaveId = ContextualProvisionalAdmissionSource.Token(saveId, nameof(saveId));
            WorldId = ContextualProvisionalAdmissionSource.Token(worldId, nameof(worldId));
            StoreSetId = ContextualProvisionalAdmissionSource.Token(storeSetId, nameof(storeSetId));
            SourceContract = ContextualProvisionalAdmissionSource.AttestationContract;
            SourceGateDigest = GroundedCompoundAppraisalSource.FormalGateDigest;
            Authority = ContextualProvisionalAdmissionSource.AttestationAuthority;
            Fingerprint = ComputeFingerprint();
        }

        public string ProposalFingerprint { get; }
        public string ProposalId { get; }
        public string OwnerId { get; }
        public string LineageId { get; }
        public string? CounterpartId { get; }
        public string CurrentEventId { get; }
        public string CurrentEventHash { get; }
        public string CurrentSeedFingerprint { get; }
        public string ContextPacketFingerprint { get; }
        public string RequestFingerprint { get; }
        public long CheckpointGeneration { get; }
        public string CheckpointFingerprint { get; }
        public string SaveId { get; }
        public string WorldId { get; }
        public string StoreSetId { get; }
        public string SourceContract { get; }
        public string SourceGateDigest { get; }
        public string Authority { get; }
        public string Fingerprint { get; }

        public bool VerifyFingerprint() => string.Equals(Fingerprint, ComputeFingerprint(), StringComparison.Ordinal);

        private string ComputeFingerprint() => ContextualProvisionalAdmissionSource.Hash(
            "{" +
            "\"authority\":" + ContextualProvisionalAdmissionSource.Json(Authority) + "," +
            "\"checkpoint_fingerprint\":" + ContextualProvisionalAdmissionSource.Json(CheckpointFingerprint) + "," +
            "\"checkpoint_generation\":" + ContextualProvisionalAdmissionSource.Number(CheckpointGeneration) + "," +
            "\"context_packet_fingerprint\":" + ContextualProvisionalAdmissionSource.Json(ContextPacketFingerprint) + "," +
            "\"counterpart_id\":" + ContextualProvisionalAdmissionSource.Optional(CounterpartId) + "," +
            "\"current_event_hash\":" + ContextualProvisionalAdmissionSource.Json(CurrentEventHash) + "," +
            "\"current_event_id\":" + ContextualProvisionalAdmissionSource.Json(CurrentEventId) + "," +
            "\"current_seed_fingerprint\":" + ContextualProvisionalAdmissionSource.Json(CurrentSeedFingerprint) + "," +
            "\"lineage_id\":" + ContextualProvisionalAdmissionSource.Json(LineageId) + "," +
            "\"owner_id\":" + ContextualProvisionalAdmissionSource.Json(OwnerId) + "," +
            "\"proposal_fingerprint\":" + ContextualProvisionalAdmissionSource.Json(ProposalFingerprint) + "," +
            "\"proposal_id\":" + ContextualProvisionalAdmissionSource.Json(ProposalId) + "," +
            "\"request_fingerprint\":" + ContextualProvisionalAdmissionSource.Json(RequestFingerprint) + "," +
            "\"save_id\":" + ContextualProvisionalAdmissionSource.Json(SaveId) + "," +
            "\"source_contract\":" + ContextualProvisionalAdmissionSource.Json(SourceContract) + "," +
            "\"source_gate_digest\":" + ContextualProvisionalAdmissionSource.Json(SourceGateDigest) + "," +
            "\"store_set_id\":" + ContextualProvisionalAdmissionSource.Json(StoreSetId) + "," +
            "\"world_id\":" + ContextualProvisionalAdmissionSource.Json(WorldId) +
            "}");
    }

    public sealed class ContextualAdmittedDialogueEventReceipt
    {
        internal ContextualAdmittedDialogueEventReceipt(
            string eventId, string eventHash, ObservedDisplayReceipt displayReceipt,
            long admittedTick, string checkpointFingerprint, string saveId, string worldId, string storeSetId)
        {
            EventId = ContextualProvisionalAdmissionSource.Token(eventId, nameof(eventId));
            EventHash = ContextualProvisionalAdmissionSource.Sha(eventHash, nameof(eventHash));
            DisplayReceipt = displayReceipt ?? throw new ArgumentNullException(nameof(displayReceipt));
            if (admittedTick < displayReceipt.DisplayedTick) throw new ArgumentOutOfRangeException(nameof(admittedTick));
            AdmittedTick = admittedTick;
            CheckpointFingerprint = ContextualProvisionalAdmissionSource.Sha(checkpointFingerprint, nameof(checkpointFingerprint));
            SaveId = ContextualProvisionalAdmissionSource.Token(saveId, nameof(saveId));
            WorldId = ContextualProvisionalAdmissionSource.Token(worldId, nameof(worldId));
            StoreSetId = ContextualProvisionalAdmissionSource.Token(storeSetId, nameof(storeSetId));
            Outcome = ContextualAdmissionOutcome.ObservedSuccess;
            SourceContract = ContextualProvisionalAdmissionSource.DialogueAdmissionContract;
            Authority = ContextualProvisionalAdmissionSource.DialogueAdmissionAuthority;
            Fingerprint = ComputeFingerprint();
        }

        public string EventId { get; }
        public string EventHash { get; }
        public ObservedDisplayReceipt DisplayReceipt { get; }
        public string UtteranceId => DisplayReceipt.UtteranceId;
        public string ConversationId => DisplayReceipt.ConversationId;
        public string SpeakerId => DisplayReceipt.SpeakerId;
        public string? RecipientId => DisplayReceipt.RecipientId;
        public IReadOnlyList<string> AudienceIds => DisplayReceipt.AudienceIds;
        public string ExactTextHash => DisplayReceipt.ExactTextHash;
        public long DisplayedTick => DisplayReceipt.DisplayedTick;
        public long AdmittedTick { get; }
        public long CheckpointGeneration => DisplayReceipt.CheckpointGeneration;
        public string CheckpointFingerprint { get; }
        public string SaveId { get; }
        public string WorldId { get; }
        public string StoreSetId { get; }
        public ContextualAdmissionOutcome Outcome { get; }
        public string SourceContract { get; }
        public string Authority { get; }
        public string Fingerprint { get; }
        public bool VerifyFingerprint() => string.Equals(Fingerprint, ComputeFingerprint(), StringComparison.Ordinal);

        private string ComputeFingerprint() => ContextualProvisionalAdmissionSource.Hash(
            "{" +
            "\"admitted_tick\":" + ContextualProvisionalAdmissionSource.Number(AdmittedTick) + "," +
            "\"audience_ids\":[" + string.Join(",", AudienceIds.Select(ContextualProvisionalAdmissionSource.Json)) + "]," +
            "\"authority\":" + ContextualProvisionalAdmissionSource.Json(Authority) + "," +
            "\"checkpoint_fingerprint\":" + ContextualProvisionalAdmissionSource.Json(CheckpointFingerprint) + "," +
            "\"checkpoint_generation\":" + ContextualProvisionalAdmissionSource.Number(CheckpointGeneration) + "," +
            "\"conversation_id\":" + ContextualProvisionalAdmissionSource.Json(ConversationId) + "," +
            "\"displayed_tick\":" + ContextualProvisionalAdmissionSource.Number(DisplayedTick) + "," +
            "\"event_hash\":" + ContextualProvisionalAdmissionSource.Json(EventHash) + "," +
            "\"event_id\":" + ContextualProvisionalAdmissionSource.Json(EventId) + "," +
            "\"exact_text_hash\":" + ContextualProvisionalAdmissionSource.Json(ExactTextHash) + "," +
            "\"outcome\":\"OBSERVED_SUCCESS\"," +
            "\"recipient_id\":" + ContextualProvisionalAdmissionSource.Optional(RecipientId) + "," +
            "\"save_id\":" + ContextualProvisionalAdmissionSource.Json(SaveId) + "," +
            "\"source_contract\":" + ContextualProvisionalAdmissionSource.Json(SourceContract) + "," +
            "\"speaker_id\":" + ContextualProvisionalAdmissionSource.Json(SpeakerId) + "," +
            "\"store_set_id\":" + ContextualProvisionalAdmissionSource.Json(StoreSetId) + "," +
            "\"utterance_id\":" + ContextualProvisionalAdmissionSource.Json(UtteranceId) + "," +
            "\"world_id\":" + ContextualProvisionalAdmissionSource.Json(WorldId) +
            "}");
    }

    public sealed class ContextualAdmissionRequest
    {
        public ContextualAdmissionRequest(
            string sessionId, GroundedCompoundAppraisalProposal proposal,
            V43ProposalAttestation proposalAttestation, ObservedDisplayReceipt displayReceipt,
            ContextualAdmittedDialogueEventReceipt dialogueReceipt,
            string canonicalAffectFingerprint, long canonicalAffectVersion,
            string? relationshipFingerprint, long? relationshipVersion)
        {
            SessionId = ContextualProvisionalAdmissionSource.Token(sessionId, nameof(sessionId));
            Proposal = GroundedCompoundAppraisalProposal.RequireValid(proposal);
            ProposalAttestation = proposalAttestation ?? throw new ArgumentNullException(nameof(proposalAttestation));
            DisplayReceipt = displayReceipt ?? throw new ArgumentNullException(nameof(displayReceipt));
            DialogueReceipt = dialogueReceipt ?? throw new ArgumentNullException(nameof(dialogueReceipt));
            CanonicalAffectFingerprint = ContextualProvisionalAdmissionSource.Sha(canonicalAffectFingerprint, nameof(canonicalAffectFingerprint));
            if (canonicalAffectVersion < 0) throw new ArgumentOutOfRangeException(nameof(canonicalAffectVersion));
            CanonicalAffectVersion = canonicalAffectVersion;
            RelationshipFingerprint = relationshipFingerprint is null ? null :
                ContextualProvisionalAdmissionSource.Sha(relationshipFingerprint, nameof(relationshipFingerprint));
            if (relationshipVersion.HasValue && relationshipVersion.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(relationshipVersion));
            RelationshipVersion = relationshipVersion;
            Validate();
            Fingerprint = ComputeFingerprint();
        }

        public string SessionId { get; }
        public GroundedCompoundAppraisalProposal Proposal { get; }
        public V43ProposalAttestation ProposalAttestation { get; }
        public ObservedDisplayReceipt DisplayReceipt { get; }
        public ContextualAdmittedDialogueEventReceipt DialogueReceipt { get; }
        public string CanonicalAffectFingerprint { get; }
        public long CanonicalAffectVersion { get; }
        public string? RelationshipFingerprint { get; }
        public long? RelationshipVersion { get; }
        public string Fingerprint { get; }

        private void Validate()
        {
            var display = DisplayReceipt;
            var dialogue = DialogueReceipt;
            var attestation = ProposalAttestation;
            if (display.Outcome != ObservedDisplayOutcome.OBSERVED_SUCCESS ||
                !string.Equals(display.SourceContract, ObservedDisplayReceipt.SourceContractValue, StringComparison.Ordinal) ||
                !string.Equals(display.SourceGateDigest, ObservedDisplayReceipt.SourceGateDigestValue, StringComparison.Ordinal))
                throw new ArgumentException("Display receipt is not observed success.");
            if (!string.Equals(display.SessionId, SessionId, StringComparison.Ordinal) ||
                !display.AudienceIds.Contains(Proposal.OwnerId, StringComparer.Ordinal))
                throw new ArgumentException("Foreign session or proposal owner was not a witness.");
            if (dialogue.Outcome != ContextualAdmissionOutcome.ObservedSuccess ||
                !ReferenceEquals(dialogue.DisplayReceipt, display) || !dialogue.VerifyFingerprint())
                throw new ArgumentException("Untrusted display/dialogue binding.");
            if (!string.Equals(Proposal.CurrentEventId, dialogue.EventId, StringComparison.Ordinal) ||
                !string.Equals(Proposal.CurrentEventHash, dialogue.EventHash, StringComparison.Ordinal))
                throw new ArgumentException("Current event mismatch.");
            if (!string.Equals(Proposal.Fingerprint, attestation.ProposalFingerprint, StringComparison.Ordinal) ||
                !string.Equals(Proposal.OwnerId, attestation.OwnerId, StringComparison.Ordinal) ||
                !string.Equals(Proposal.LineageId, attestation.LineageId, StringComparison.Ordinal) ||
                !string.Equals(Proposal.CounterpartId, attestation.CounterpartId, StringComparison.Ordinal) ||
                !string.Equals(Proposal.CurrentSeedFingerprint, attestation.CurrentSeedFingerprint, StringComparison.Ordinal) ||
                !string.Equals(Proposal.ContextPacketFingerprint, attestation.ContextPacketFingerprint, StringComparison.Ordinal) ||
                !string.Equals(Proposal.RequestFingerprint, attestation.RequestFingerprint, StringComparison.Ordinal) ||
                !attestation.VerifyFingerprint())
                throw new ArgumentException("V43 attestation mismatch.");
            if (attestation.CheckpointGeneration != dialogue.CheckpointGeneration ||
                !string.Equals(attestation.CheckpointFingerprint, dialogue.CheckpointFingerprint, StringComparison.Ordinal) ||
                !string.Equals(attestation.SaveId, dialogue.SaveId, StringComparison.Ordinal) ||
                !string.Equals(attestation.WorldId, dialogue.WorldId, StringComparison.Ordinal) ||
                !string.Equals(attestation.StoreSetId, dialogue.StoreSetId, StringComparison.Ordinal))
                throw new ArgumentException("Checkpoint or store mismatch.");
            var directOther = string.Equals(Proposal.OwnerId, display.SpeakerId, StringComparison.Ordinal)
                ? display.RecipientId
                : string.Equals(Proposal.OwnerId, display.RecipientId, StringComparison.Ordinal) ? display.SpeakerId : null;
            if (Proposal.RelationshipConsiderations.Count > 0 &&
                (directOther is null || !string.Equals(Proposal.CounterpartId, directOther, StringComparison.Ordinal)))
                throw new ArgumentException("Relationship proposal lacks direct participant alignment.");
            if (Proposal.RelationshipConsiderations.Count > 0 &&
                (RelationshipFingerprint is null || RelationshipVersion is null))
                throw new ArgumentException("Relationship state guard missing.");
            if ((RelationshipFingerprint is null) != (RelationshipVersion is null))
                throw new ArgumentException("Partial relationship state guard.");
        }

        private string ComputeFingerprint() => ContextualProvisionalAdmissionSource.Hash(
            "{" +
            "\"attestation_fingerprint\":" + ContextualProvisionalAdmissionSource.Json(ProposalAttestation.Fingerprint) + "," +
            "\"canonical_affect_fingerprint\":" + ContextualProvisionalAdmissionSource.Json(CanonicalAffectFingerprint) + "," +
            "\"canonical_affect_version\":" + ContextualProvisionalAdmissionSource.Number(CanonicalAffectVersion) + "," +
            "\"dialogue_receipt_fingerprint\":" + ContextualProvisionalAdmissionSource.Json(DialogueReceipt.Fingerprint) + "," +
            "\"display_receipt_fingerprint\":" + ContextualProvisionalAdmissionSource.Json(DisplayReceipt.ReceiptFingerprint) + "," +
            "\"proposal_fingerprint\":" + ContextualProvisionalAdmissionSource.Json(Proposal.Fingerprint) + "," +
            "\"relationship_fingerprint\":" + ContextualProvisionalAdmissionSource.Optional(RelationshipFingerprint) + "," +
            "\"relationship_version\":" + (RelationshipVersion.HasValue ? ContextualProvisionalAdmissionSource.Number(RelationshipVersion.Value) : "null") + "," +
            "\"session_id\":" + ContextualProvisionalAdmissionSource.Json(SessionId) +
            "}");
    }

    public sealed class MappedOverlayDimension
    {
        internal MappedOverlayDimension(string dimension, int sourceBps, int adjustedBps, int overlayBps, int capBps)
        {
            Dimension = ContextualProvisionalAdmissionSource.Token(dimension, nameof(dimension));
            SourceBps = sourceBps;
            ConfidenceAdjustedBps = adjustedBps;
            OverlayBps = overlayBps;
            CapBps = capBps;
            if (Math.Abs(sourceBps) > 10000 || Math.Abs(adjustedBps) > 10000 ||
                Math.Abs(overlayBps) > capBps ||
                (capBps != ContextualProvisionalAdmissionSource.AffectOverlayCapBps &&
                 capBps != ContextualProvisionalAdmissionSource.RelationshipOverlayCapBps))
                throw new ArgumentOutOfRangeException(nameof(overlayBps));
        }
        public string Dimension { get; }
        public int SourceBps { get; }
        public int ConfidenceAdjustedBps { get; }
        public int OverlayBps { get; }
        public int CapBps { get; }
    }

    public sealed class ContextualProvisionalAdmissionAttempt
    {
        internal ContextualProvisionalAdmissionAttempt(
            string attemptId, ContextualAdmissionRequest request,
            IEnumerable<KeyValuePair<string, int>> families,
            IEnumerable<MappedOverlayDimension> affect, IEnumerable<MappedOverlayDimension> relationship,
            IEnumerable<string> biases, ContextualPresentationMode presentation,
            IEnumerable<string> reasons, string fingerprint)
        {
            AttemptId = attemptId;
            RequestFingerprint = request.Fingerprint;
            ProposalFingerprint = request.Proposal.Fingerprint;
            OwnerId = request.Proposal.OwnerId;
            LineageId = request.Proposal.LineageId;
            CounterpartId = request.Proposal.CounterpartId;
            UtteranceId = request.DisplayReceipt.UtteranceId;
            ConversationId = request.DisplayReceipt.ConversationId;
            SpeakerId = request.DisplayReceipt.SpeakerId;
            CurrentEventId = request.Proposal.CurrentEventId;
            CurrentEventHash = request.Proposal.CurrentEventHash;
            DisplayedTick = request.DisplayReceipt.DisplayedTick;
            CheckpointGeneration = request.ProposalAttestation.CheckpointGeneration;
            CheckpointFingerprint = request.ProposalAttestation.CheckpointFingerprint;
            Families = new ReadOnlyCollection<KeyValuePair<string, int>>(families.ToList());
            AffectOverlay = new ReadOnlyCollection<MappedOverlayDimension>(affect.ToList());
            RelationshipOverlay = new ReadOnlyCollection<MappedOverlayDimension>(relationship.ToList());
            Biases = new ReadOnlyCollection<string>(biases.ToList());
            PresentationMode = presentation;
            ConfidenceBps = request.Proposal.ConfidenceBps;
            IntensityBps = request.Proposal.IntensityBps;
            UncertaintyCodes = new ReadOnlyCollection<string>(request.Proposal.UncertaintyCodes.ToList());
            ReasonCodes = new ReadOnlyCollection<string>(reasons.ToList());
            SourceContract = ContextualProvisionalAdmissionSource.Contract;
            SourceGateDigest = ContextualProvisionalAdmissionSource.FormalGateDigest;
            Authority = ContextualProvisionalAdmissionSource.Authority;
            Outcome = ContextualAdmissionOutcome.AttemptOnly;
            Fingerprint = fingerprint;
        }
        public string AttemptId { get; }
        public string RequestFingerprint { get; }
        public string ProposalFingerprint { get; }
        public string OwnerId { get; }
        public string LineageId { get; }
        public string? CounterpartId { get; }
        public string UtteranceId { get; }
        public string ConversationId { get; }
        public string SpeakerId { get; }
        public string CurrentEventId { get; }
        public string CurrentEventHash { get; }
        public long DisplayedTick { get; }
        public long CheckpointGeneration { get; }
        public string CheckpointFingerprint { get; }
        public IReadOnlyList<KeyValuePair<string, int>> Families { get; }
        public IReadOnlyList<MappedOverlayDimension> AffectOverlay { get; }
        public IReadOnlyList<MappedOverlayDimension> RelationshipOverlay { get; }
        public IReadOnlyList<string> Biases { get; }
        public ContextualPresentationMode PresentationMode { get; }
        public int ConfidenceBps { get; }
        public int IntensityBps { get; }
        public IReadOnlyList<string> UncertaintyCodes { get; }
        public IReadOnlyList<string> ReasonCodes { get; }
        public string SourceContract { get; }
        public string SourceGateDigest { get; }
        public string Authority { get; }
        public ContextualAdmissionOutcome Outcome { get; }
        public string Fingerprint { get; }
    }

    public sealed class ObservedProvisionalAdmissionReceipt
    {
        internal ObservedProvisionalAdmissionReceipt(
            string receiptId, string attemptId, string reactionId, string ownerId,
            string utteranceId, long admittedTick, long generation, string checkpoint, string fingerprint)
        {
            ReceiptId = receiptId; AttemptId = attemptId; ReactionId = reactionId; OwnerId = ownerId;
            UtteranceId = utteranceId; AdmittedTick = admittedTick; CheckpointGeneration = generation;
            CheckpointFingerprint = checkpoint; Outcome = ContextualAdmissionOutcome.ObservedSuccess;
            SourceContract = ContextualProvisionalAdmissionSource.ReceiptContract;
            SourceGateDigest = ContextualProvisionalAdmissionSource.FormalGateDigest;
            Authority = ContextualProvisionalAdmissionSource.ReceiptAuthority; Fingerprint = fingerprint;
        }
        public string ReceiptId { get; }
        public string AttemptId { get; }
        public string ReactionId { get; }
        public string OwnerId { get; }
        public string UtteranceId { get; }
        public long AdmittedTick { get; }
        public long CheckpointGeneration { get; }
        public string CheckpointFingerprint { get; }
        public ContextualAdmissionOutcome Outcome { get; }
        public string SourceContract { get; }
        public string SourceGateDigest { get; }
        public string Authority { get; }
        public string Fingerprint { get; }
    }

    public sealed class ContextualProvisionalReaction
    {
        internal ContextualProvisionalReaction(
            string reactionId, string receiptFingerprint, ContextualProvisionalAdmissionAttempt attempt,
            long admittedTick, ContextualReactionState state, string fingerprint)
        {
            ReactionId = reactionId; AttemptId = attempt.AttemptId; AttemptFingerprint = attempt.Fingerprint; AdmissionReceiptFingerprint = receiptFingerprint;
            RequestFingerprint = attempt.RequestFingerprint; ProposalFingerprint = attempt.ProposalFingerprint;
            OwnerId = attempt.OwnerId; LineageId = attempt.LineageId; CounterpartId = attempt.CounterpartId;
            UtteranceId = attempt.UtteranceId; ConversationId = attempt.ConversationId; SpeakerId = attempt.SpeakerId;
            CurrentEventId = attempt.CurrentEventId; CurrentEventHash = attempt.CurrentEventHash;
            CreatedTick = attempt.DisplayedTick; AdmittedTick = admittedTick;
            ExpiresTick = checked(attempt.DisplayedTick + ContextualProvisionalAdmissionSource.LifetimeTicks);
            CheckpointGeneration = attempt.CheckpointGeneration; CheckpointFingerprint = attempt.CheckpointFingerprint;
            Families = attempt.Families; AffectOverlay = attempt.AffectOverlay; RelationshipOverlay = attempt.RelationshipOverlay;
            Biases = attempt.Biases; PresentationMode = attempt.PresentationMode; ConfidenceBps = attempt.ConfidenceBps;
            IntensityBps = attempt.IntensityBps; UncertaintyCodes = attempt.UncertaintyCodes;
            ReasonCodes = attempt.ReasonCodes; State = state; Authority = ContextualProvisionalAdmissionSource.Authority;
            Fingerprint = fingerprint;
        }

        private ContextualProvisionalReaction(ContextualProvisionalReaction source, ContextualReactionState state, string fingerprint)
        {
            ReactionId=source.ReactionId; AttemptId=source.AttemptId; AttemptFingerprint=source.AttemptFingerprint; AdmissionReceiptFingerprint=source.AdmissionReceiptFingerprint;
            RequestFingerprint=source.RequestFingerprint; ProposalFingerprint=source.ProposalFingerprint; OwnerId=source.OwnerId;
            LineageId=source.LineageId; CounterpartId=source.CounterpartId; UtteranceId=source.UtteranceId;
            ConversationId=source.ConversationId; SpeakerId=source.SpeakerId; CurrentEventId=source.CurrentEventId;
            CurrentEventHash=source.CurrentEventHash; CreatedTick=source.CreatedTick; AdmittedTick=source.AdmittedTick;
            ExpiresTick=source.ExpiresTick; CheckpointGeneration=source.CheckpointGeneration;
            CheckpointFingerprint=source.CheckpointFingerprint; Families=source.Families; AffectOverlay=source.AffectOverlay;
            RelationshipOverlay=source.RelationshipOverlay; Biases=source.Biases; PresentationMode=source.PresentationMode;
            ConfidenceBps=source.ConfidenceBps; IntensityBps=source.IntensityBps; UncertaintyCodes=source.UncertaintyCodes;
            ReasonCodes=source.ReasonCodes; State=state; Authority=source.Authority; Fingerprint=fingerprint;
        }
        public string ReactionId { get; }
        public string AttemptId { get; }
        public string AttemptFingerprint { get; }
        public string AdmissionReceiptFingerprint { get; }
        public string RequestFingerprint { get; }
        public string ProposalFingerprint { get; }
        public string OwnerId { get; }
        public string LineageId { get; }
        public string? CounterpartId { get; }
        public string UtteranceId { get; }
        public string ConversationId { get; }
        public string SpeakerId { get; }
        public string CurrentEventId { get; }
        public string CurrentEventHash { get; }
        public long CreatedTick { get; }
        public long AdmittedTick { get; }
        public long ExpiresTick { get; }
        public long CheckpointGeneration { get; }
        public string CheckpointFingerprint { get; }
        public IReadOnlyList<KeyValuePair<string,int>> Families { get; }
        public IReadOnlyList<MappedOverlayDimension> AffectOverlay { get; }
        public IReadOnlyList<MappedOverlayDimension> RelationshipOverlay { get; }
        public IReadOnlyList<string> Biases { get; }
        public ContextualPresentationMode PresentationMode { get; }
        public int ConfidenceBps { get; }
        public int IntensityBps { get; }
        public IReadOnlyList<string> UncertaintyCodes { get; }
        public IReadOnlyList<string> ReasonCodes { get; }
        public ContextualReactionState State { get; }
        public string Authority { get; }
        public string Fingerprint { get; }
        internal ContextualProvisionalReaction WithState(ContextualReactionState state, string fingerprint) =>
            new ContextualProvisionalReaction(this, state, fingerprint);
    }
}
