using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dagmay.Core.Development;

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
        public const int MaximumActivePerOwner = 32;
        public const int MaximumGlobalActive = 1024;
        public const int MaximumTrackedOwners = 256;
        public const int MaximumPendingAttestations = 64;
        public const int MaximumPathClaims = 1024;
        public const int LifetimeTicks = 15000;
        public const int AffectOverlayCapBps = 1500;
        public const int RelationshipOverlayCapBps = 800;
        public const int FilterBytes = 1048576;
        internal static string Hash(string value) { using var h=SHA256.Create(); return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant(); }
        internal static string Token(string value,string name){ if(string.IsNullOrWhiteSpace(value)||value.Any(c=>c<32)) throw new ArgumentException("Invalid token",name); return value; }
    }

    public sealed class V43ProposalAttestation
    {
        internal V43ProposalAttestation(GroundedCompoundAppraisalProposal proposal, long checkpointGeneration, string checkpointFingerprint, string saveId, string worldId, string storeSetId, string fingerprint)
        { ProposalFingerprint=proposal.Fingerprint; ProposalId=proposal.ProposalId; OwnerId=proposal.OwnerId; LineageId=proposal.LineageId; CounterpartId=proposal.CounterpartId; CurrentEventId=proposal.CurrentEventId; CurrentEventHash=proposal.CurrentEventHash; RequestFingerprint=proposal.RequestFingerprint; CheckpointGeneration=checkpointGeneration; CheckpointFingerprint=ContextualProvisionalAdmissionSource.Token(checkpointFingerprint,nameof(checkpointFingerprint)); SaveId=ContextualProvisionalAdmissionSource.Token(saveId,nameof(saveId)); WorldId=ContextualProvisionalAdmissionSource.Token(worldId,nameof(worldId)); StoreSetId=ContextualProvisionalAdmissionSource.Token(storeSetId,nameof(storeSetId)); Fingerprint=fingerprint; }
        public string ProposalFingerprint{get;} public string ProposalId{get;} public string OwnerId{get;} public string LineageId{get;} public string? CounterpartId{get;} public string CurrentEventId{get;} public string CurrentEventHash{get;} public string RequestFingerprint{get;} public long CheckpointGeneration{get;} public string CheckpointFingerprint{get;} public string SaveId{get;} public string WorldId{get;} public string StoreSetId{get;} public string Fingerprint{get;}
    }

    public sealed class ContextualAdmittedDialogueEventReceipt
    {
        internal ContextualAdmittedDialogueEventReceipt(string eventId,string eventHash,string utteranceId,string conversationId,string speakerId,string? recipientId,IEnumerable<string> audienceIds,string exactTextHash,long displayedTick,long admittedTick,long checkpointGeneration,string checkpointFingerprint,string saveId,string worldId,string storeSetId,string fingerprint)
        { EventId=ContextualProvisionalAdmissionSource.Token(eventId,nameof(eventId)); EventHash=eventHash; UtteranceId=ContextualProvisionalAdmissionSource.Token(utteranceId,nameof(utteranceId)); ConversationId=ContextualProvisionalAdmissionSource.Token(conversationId,nameof(conversationId)); SpeakerId=ContextualProvisionalAdmissionSource.Token(speakerId,nameof(speakerId)); RecipientId=recipientId; AudienceIds=new ReadOnlyCollection<string>((audienceIds??throw new ArgumentNullException(nameof(audienceIds))).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToList()); ExactTextHash=exactTextHash; DisplayedTick=displayedTick; AdmittedTick=admittedTick; CheckpointGeneration=checkpointGeneration; CheckpointFingerprint=checkpointFingerprint; SaveId=saveId; WorldId=worldId; StoreSetId=storeSetId; Outcome=ContextualAdmissionOutcome.ObservedSuccess; Fingerprint=fingerprint; if(admittedTick<displayedTick)throw new ArgumentException("Admission precedes display"); }
        public string EventId{get;} public string EventHash{get;} public string UtteranceId{get;} public string ConversationId{get;} public string SpeakerId{get;} public string? RecipientId{get;} public IReadOnlyList<string> AudienceIds{get;} public string ExactTextHash{get;} public long DisplayedTick{get;} public long AdmittedTick{get;} public long CheckpointGeneration{get;} public string CheckpointFingerprint{get;} public string SaveId{get;} public string WorldId{get;} public string StoreSetId{get;} public ContextualAdmissionOutcome Outcome{get;} public string Fingerprint{get;}
    }

    public sealed class ContextualAdmissionRequest
    {
        public ContextualAdmissionRequest(string sessionId, ContextualAppraisalRequest v43Request, GroundedCompoundAppraisalProposal proposal, V43ProposalAttestation proposalAttestation, ContextualAdmittedDialogueEventReceipt dialogueReceipt, string displayReceiptFingerprint, string displayUtteranceId, string displayConversationId, string displaySpeakerId, string displayTextHash, long displayTick, long displayCheckpointGeneration, string canonicalAffectFingerprint, long canonicalAffectVersion, string? relationshipFingerprint, long? relationshipVersion)
        { SessionId=ContextualProvisionalAdmissionSource.Token(sessionId,nameof(sessionId)); V43Request=v43Request??throw new ArgumentNullException(nameof(v43Request)); Proposal=proposal??throw new ArgumentNullException(nameof(proposal)); ProposalAttestation=proposalAttestation??throw new ArgumentNullException(nameof(proposalAttestation)); DialogueReceipt=dialogueReceipt??throw new ArgumentNullException(nameof(dialogueReceipt)); DisplayReceiptFingerprint=displayReceiptFingerprint; DisplayUtteranceId=displayUtteranceId; DisplayConversationId=displayConversationId; DisplaySpeakerId=displaySpeakerId; DisplayTextHash=displayTextHash; DisplayTick=displayTick; DisplayCheckpointGeneration=displayCheckpointGeneration; CanonicalAffectFingerprint=canonicalAffectFingerprint; CanonicalAffectVersion=canonicalAffectVersion; RelationshipFingerprint=relationshipFingerprint; RelationshipVersion=relationshipVersion; if(proposal.OwnerId!=v43Request.CurrentSeed.OwnerId)throw new ArgumentException("Owner mismatch"); if(displayUtteranceId!=dialogueReceipt.UtteranceId||displayConversationId!=dialogueReceipt.ConversationId||displaySpeakerId!=dialogueReceipt.SpeakerId||displayTextHash!=dialogueReceipt.ExactTextHash)throw new ArgumentException("Display/dialogue mismatch"); }
        public string SessionId{get;} public ContextualAppraisalRequest V43Request{get;} public GroundedCompoundAppraisalProposal Proposal{get;} public V43ProposalAttestation ProposalAttestation{get;} public ContextualAdmittedDialogueEventReceipt DialogueReceipt{get;} public string DisplayReceiptFingerprint{get;} public string DisplayUtteranceId{get;} public string DisplayConversationId{get;} public string DisplaySpeakerId{get;} public string DisplayTextHash{get;} public long DisplayTick{get;} public long DisplayCheckpointGeneration{get;} public string CanonicalAffectFingerprint{get;} public long CanonicalAffectVersion{get;} public string? RelationshipFingerprint{get;} public long? RelationshipVersion{get;}
        public string Fingerprint => ContextualProvisionalAdmissionSource.Hash(string.Join("|",SessionId,Proposal.Fingerprint,ProposalAttestation.Fingerprint,DialogueReceipt.Fingerprint,DisplayReceiptFingerprint,CanonicalAffectFingerprint,CanonicalAffectVersion,RelationshipFingerprint??"",RelationshipVersion?.ToString()??""));
    }

    public sealed class MappedOverlayDimension
    {
        internal MappedOverlayDimension(string dimension,int sourceBps,int confidenceAdjustedBps,int overlayBps,int capBps){Dimension=ContextualProvisionalAdmissionSource.Token(dimension,nameof(dimension));SourceBps=sourceBps;ConfidenceAdjustedBps=confidenceAdjustedBps;OverlayBps=overlayBps;CapBps=capBps;if(Math.Abs(overlayBps)>capBps)throw new ArgumentOutOfRangeException(nameof(overlayBps));}
        public string Dimension{get;} public int SourceBps{get;} public int ConfidenceAdjustedBps{get;} public int OverlayBps{get;} public int CapBps{get;}
    }

    public sealed class ContextualProvisionalAdmissionAttempt
    {
        internal ContextualProvisionalAdmissionAttempt(string attemptId,ContextualAdmissionRequest request,IEnumerable<MappedOverlayDimension> affect,IEnumerable<MappedOverlayDimension> relationship,IEnumerable<string> biases,ContextualPresentationMode presentation,IEnumerable<string> reasons,string fingerprint)
        {AttemptId=attemptId;RequestFingerprint=request.Fingerprint;ProposalFingerprint=request.Proposal.Fingerprint;OwnerId=request.Proposal.OwnerId;LineageId=request.Proposal.LineageId;CounterpartId=request.Proposal.CounterpartId;UtteranceId=request.DialogueReceipt.UtteranceId;ConversationId=request.DialogueReceipt.ConversationId;SpeakerId=request.DialogueReceipt.SpeakerId;CurrentEventId=request.Proposal.CurrentEventId;CurrentEventHash=request.Proposal.CurrentEventHash;DisplayedTick=request.DisplayTick;CheckpointGeneration=request.DialogueReceipt.CheckpointGeneration;CheckpointFingerprint=request.DialogueReceipt.CheckpointFingerprint;AffectOverlay=new ReadOnlyCollection<MappedOverlayDimension>(affect.ToList());RelationshipOverlay=new ReadOnlyCollection<MappedOverlayDimension>(relationship.ToList());Biases=new ReadOnlyCollection<string>(biases.Distinct().OrderBy(x=>x,StringComparer.Ordinal).Take(4).ToList());PresentationMode=presentation;ConfidenceBps=request.Proposal.ConfidenceBps;IntensityBps=request.Proposal.IntensityBps;ReasonCodes=new ReadOnlyCollection<string>(reasons.Distinct().OrderBy(x=>x,StringComparer.Ordinal).Take(24).ToList());Outcome=ContextualAdmissionOutcome.AttemptOnly;Fingerprint=fingerprint;}
        public string AttemptId{get;} public string RequestFingerprint{get;} public string ProposalFingerprint{get;} public string OwnerId{get;} public string LineageId{get;} public string? CounterpartId{get;} public string UtteranceId{get;} public string ConversationId{get;} public string SpeakerId{get;} public string CurrentEventId{get;} public string CurrentEventHash{get;} public long DisplayedTick{get;} public long CheckpointGeneration{get;} public string CheckpointFingerprint{get;} public IReadOnlyList<MappedOverlayDimension>AffectOverlay{get;} public IReadOnlyList<MappedOverlayDimension>RelationshipOverlay{get;} public IReadOnlyList<string>Biases{get;} public ContextualPresentationMode PresentationMode{get;} public int ConfidenceBps{get;} public int IntensityBps{get;} public IReadOnlyList<string>ReasonCodes{get;} public ContextualAdmissionOutcome Outcome{get;} public string Fingerprint{get;}
    }

    public sealed class ObservedProvisionalAdmissionReceipt
    { internal ObservedProvisionalAdmissionReceipt(string receiptId,string attemptId,string reactionId,string ownerId,string utteranceId,long admittedTick,long generation,string checkpoint,string fingerprint){ReceiptId=receiptId;AttemptId=attemptId;ReactionId=reactionId;OwnerId=ownerId;UtteranceId=utteranceId;AdmittedTick=admittedTick;CheckpointGeneration=generation;CheckpointFingerprint=checkpoint;Outcome=ContextualAdmissionOutcome.ObservedSuccess;Fingerprint=fingerprint;} public string ReceiptId{get;} public string AttemptId{get;} public string ReactionId{get;} public string OwnerId{get;} public string UtteranceId{get;} public long AdmittedTick{get;} public long CheckpointGeneration{get;} public string CheckpointFingerprint{get;} public ContextualAdmissionOutcome Outcome{get;} public string Fingerprint{get;} }

    public sealed class ContextualProvisionalReaction
    { internal ContextualProvisionalReaction(string reactionId,ContextualAdmissionRequest request,ContextualProvisionalAdmissionAttempt attempt,string receiptFingerprint,long admittedTick,ContextualReactionState state,string fingerprint){ReactionId=reactionId;AttemptId=attempt.AttemptId;AdmissionReceiptFingerprint=receiptFingerprint;RequestFingerprint=request.Fingerprint;ProposalFingerprint=request.Proposal.Fingerprint;OwnerId=attempt.OwnerId;LineageId=attempt.LineageId;CounterpartId=attempt.CounterpartId;SessionId=request.SessionId;UtteranceId=attempt.UtteranceId;ConversationId=attempt.ConversationId;SpeakerId=attempt.SpeakerId;CurrentEventId=attempt.CurrentEventId;CurrentEventHash=attempt.CurrentEventHash;CreatedTick=attempt.DisplayedTick;AdmittedTick=admittedTick;ExpiresTick=attempt.DisplayedTick+ContextualProvisionalAdmissionSource.LifetimeTicks;CheckpointGeneration=attempt.CheckpointGeneration;CheckpointFingerprint=attempt.CheckpointFingerprint;AffectOverlay=attempt.AffectOverlay;RelationshipOverlay=attempt.RelationshipOverlay;Biases=attempt.Biases;PresentationMode=attempt.PresentationMode;ConfidenceBps=attempt.ConfidenceBps;IntensityBps=attempt.IntensityBps;ReasonCodes=attempt.ReasonCodes;State=state;Fingerprint=fingerprint;} public string ReactionId{get;} public string AttemptId{get;} public string AdmissionReceiptFingerprint{get;} public string RequestFingerprint{get;} public string ProposalFingerprint{get;} public string OwnerId{get;} public string LineageId{get;} public string? CounterpartId{get;} public string SessionId{get;} public string UtteranceId{get;} public string ConversationId{get;} public string SpeakerId{get;} public string CurrentEventId{get;} public string CurrentEventHash{get;} public long CreatedTick{get;} public long AdmittedTick{get;} public long ExpiresTick{get;} public long CheckpointGeneration{get;} public string CheckpointFingerprint{get;} public IReadOnlyList<MappedOverlayDimension>AffectOverlay{get;} public IReadOnlyList<MappedOverlayDimension>RelationshipOverlay{get;} public IReadOnlyList<string>Biases{get;} public ContextualPresentationMode PresentationMode{get;} public int ConfidenceBps{get;} public int IntensityBps{get;} public IReadOnlyList<string>ReasonCodes{get;} public ContextualReactionState State{get;internal set;} public string Fingerprint{get;internal set;} }
}
