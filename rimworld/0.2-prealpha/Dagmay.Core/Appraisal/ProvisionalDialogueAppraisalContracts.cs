using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dagmay.Core.Presentation;

namespace Dagmay.Core.Appraisal
{
    public enum ProvisionalPrivacy
    {
        Public = 0,
        ParticipantPrivate = 1,
        OwnerPrivate = 2
    }

    public enum ProvisionalFactuality
    {
        SpeechAct = 0,
        ClaimOnly = 1,
        VerifiedFact = 2
    }

    public enum ProvisionalCueType
    {
        Gratitude = 0,
        Praise = 1,
        Apology = 2,
        RepairAttempt = 3,
        Request = 4,
        Refusal = 5,
        Accusation = 6,
        Blame = 7,
        Threat = 8,
        Insult = 9,
        Disclosure = 10,
        Affection = 11,
        Fear = 12,
        Uncertainty = 13,
        Neutral = 14
    }

    public enum ProvisionalAppraisalState
    {
        Prepared = 0,
        Active = 1,
        Expired = 2,
        Discarded = 3,
        PromotionPending = 4,
        Promoted = 5
    }

    public enum ProvisionalPresentationMode
    {
        None = 0,
        Icon = 1,
        Thought = 2
    }

    internal static class ProvisionalAppraisalCanonical
    {
        public const decimal Quantum = 0.000001m;

        public static decimal Quantize(decimal value) =>
            decimal.Round(value, 6, MidpointRounding.ToEven);

        public static decimal Bound(decimal value, decimal low, decimal high) =>
            Quantize(Math.Min(high, Math.Max(low, value)));

        public static string Text(string value, string parameterName, int maximum = 256)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
                throw new ArgumentException("A bounded nonblank value is required.", parameterName);
            if (value.Any(character => character < 32))
                throw new ArgumentException("Control characters are not permitted.", parameterName);
            return value;
        }

        public static string? OptionalText(string? value, string parameterName, int maximum = 256) =>
            value is null ? null : Text(value, parameterName, maximum);

        public static string LowerHex(string value, string parameterName)
        {
            if (value is null || value.Length != 64 ||
                value.Any(character => !((character >= '0' && character <= '9') ||
                                         (character >= 'a' && character <= 'f'))))
                throw new ArgumentException("A lowercase SHA-256 value is required.", parameterName);
            return value;
        }

        public static IReadOnlyList<string> StableIds(
            IEnumerable<string> values,
            string parameterName,
            int minimum = 0,
            int maximum = 256)
        {
            if (values is null) throw new ArgumentNullException(parameterName);
            var source = values.Select(value => Text(value, parameterName)).ToArray();
            var stable = source.Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (stable.Length != source.Length || stable.Length < minimum || stable.Length > maximum)
                throw new ArgumentException("Identifiers must be bounded, unique, and canonically ordered.", parameterName);
            for (var index = 0; index < stable.Length; index++)
            {
                if (!string.Equals(stable[index], source[index], StringComparison.Ordinal))
                    throw new ArgumentException("Identifiers must be bounded, unique, and canonically ordered.", parameterName);
            }
            return new ReadOnlyCollection<string>(stable);
        }

        public static string DecimalText(decimal value) =>
            Quantize(value).ToString("0.000000", CultureInfo.InvariantCulture);

        public static string HashFields(IEnumerable<KeyValuePair<string, string>> fields)
        {
            if (fields is null) throw new ArgumentNullException(nameof(fields));
            var builder = new StringBuilder();
            foreach (var field in fields)
            {
                var key = Text(field.Key, nameof(fields));
                var value = field.Value ?? throw new ArgumentNullException(nameof(fields));
                builder.Append(key.Length.ToString(CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(key);
                builder.Append('=');
                builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(value);
                builder.Append('|');
            }
            using (var algorithm = SHA256.Create())
            {
                return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()))
                    .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        public static KeyValuePair<string, string> Pair(string key, string value) =>
            new KeyValuePair<string, string>(key, value);
    }

    public sealed class ProvisionalAffectDelta
    {
        public static readonly ProvisionalAffectDelta Zero = new ProvisionalAffectDelta();

        public ProvisionalAffectDelta(
            decimal valence = 0m,
            decimal arousal = 0m,
            decimal threat = 0m,
            decimal agency = 0m,
            decimal attachment = 0m,
            decimal certainty = 0m,
            decimal socialStanding = 0m)
        {
            Valence = ProvisionalAppraisalCanonical.Quantize(valence);
            Arousal = ProvisionalAppraisalCanonical.Quantize(arousal);
            Threat = ProvisionalAppraisalCanonical.Quantize(threat);
            Agency = ProvisionalAppraisalCanonical.Quantize(agency);
            Attachment = ProvisionalAppraisalCanonical.Quantize(attachment);
            Certainty = ProvisionalAppraisalCanonical.Quantize(certainty);
            SocialStanding = ProvisionalAppraisalCanonical.Quantize(socialStanding);
        }

        public decimal Valence { get; }
        public decimal Arousal { get; }
        public decimal Threat { get; }
        public decimal Agency { get; }
        public decimal Attachment { get; }
        public decimal Certainty { get; }
        public decimal SocialStanding { get; }
        public decimal MaximumAbsolute => new[] { Valence, Arousal, Threat, Agency, Attachment, Certainty, SocialStanding }.Max(Math.Abs);
        public bool IsZero => MaximumAbsolute == 0m;

        public ProvisionalAffectDelta Scale(decimal factor) =>
            new ProvisionalAffectDelta(
                Valence * factor,
                Arousal * factor,
                Threat * factor,
                Agency * factor,
                Attachment * factor,
                Certainty * factor,
                SocialStanding * factor);

        public ProvisionalAffectDelta Plus(ProvisionalAffectDelta other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            return new ProvisionalAffectDelta(
                Valence + other.Valence,
                Arousal + other.Arousal,
                Threat + other.Threat,
                Agency + other.Agency,
                Attachment + other.Attachment,
                Certainty + other.Certainty,
                SocialStanding + other.SocialStanding);
        }

        public ProvisionalAffectDelta Clamp(decimal bound) =>
            new ProvisionalAffectDelta(
                ProvisionalAppraisalCanonical.Bound(Valence, -bound, bound),
                ProvisionalAppraisalCanonical.Bound(Arousal, -bound, bound),
                ProvisionalAppraisalCanonical.Bound(Threat, -bound, bound),
                ProvisionalAppraisalCanonical.Bound(Agency, -bound, bound),
                ProvisionalAppraisalCanonical.Bound(Attachment, -bound, bound),
                ProvisionalAppraisalCanonical.Bound(Certainty, -bound, bound),
                ProvisionalAppraisalCanonical.Bound(SocialStanding, -bound, bound));

        internal IEnumerable<KeyValuePair<string, string>> Fields(string prefix)
        {
            yield return ProvisionalAppraisalCanonical.Pair(prefix + ".agency", ProvisionalAppraisalCanonical.DecimalText(Agency));
            yield return ProvisionalAppraisalCanonical.Pair(prefix + ".arousal", ProvisionalAppraisalCanonical.DecimalText(Arousal));
            yield return ProvisionalAppraisalCanonical.Pair(prefix + ".attachment", ProvisionalAppraisalCanonical.DecimalText(Attachment));
            yield return ProvisionalAppraisalCanonical.Pair(prefix + ".certainty", ProvisionalAppraisalCanonical.DecimalText(Certainty));
            yield return ProvisionalAppraisalCanonical.Pair(prefix + ".social_standing", ProvisionalAppraisalCanonical.DecimalText(SocialStanding));
            yield return ProvisionalAppraisalCanonical.Pair(prefix + ".threat", ProvisionalAppraisalCanonical.DecimalText(Threat));
            yield return ProvisionalAppraisalCanonical.Pair(prefix + ".valence", ProvisionalAppraisalCanonical.DecimalText(Valence));
        }
    }

    public sealed class ProvisionalRelationshipDelta
    {
        public static readonly ProvisionalRelationshipDelta Zero = new ProvisionalRelationshipDelta();

        public ProvisionalRelationshipDelta(
            decimal trust = 0m,
            decimal affection = 0m,
            decimal fear = 0m,
            decimal resentment = 0m)
        {
            if (fear < 0m || resentment < 0m)
                throw new ArgumentOutOfRangeException(nameof(fear), "Fear and resentment provisional deltas cannot be negative.");
            Trust = ProvisionalAppraisalCanonical.Quantize(trust);
            Affection = ProvisionalAppraisalCanonical.Quantize(affection);
            Fear = ProvisionalAppraisalCanonical.Quantize(fear);
            Resentment = ProvisionalAppraisalCanonical.Quantize(resentment);
        }

        public decimal Trust { get; }
        public decimal Affection { get; }
        public decimal Fear { get; }
        public decimal Resentment { get; }
        public decimal MaximumAbsolute => new[] { Trust, Affection, Fear, Resentment }.Max(Math.Abs);
        public bool IsZero => MaximumAbsolute == 0m;

        public ProvisionalRelationshipDelta Scale(decimal factor) =>
            new ProvisionalRelationshipDelta(Trust * factor, Affection * factor, Fear * factor, Resentment * factor);

        public ProvisionalRelationshipDelta Plus(ProvisionalRelationshipDelta other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            return new ProvisionalRelationshipDelta(
                Trust + other.Trust,
                Affection + other.Affection,
                Fear + other.Fear,
                Resentment + other.Resentment);
        }

        public ProvisionalRelationshipDelta Clamp(decimal signedBound, decimal nonnegativeBound) =>
            new ProvisionalRelationshipDelta(
                ProvisionalAppraisalCanonical.Bound(Trust, -signedBound, signedBound),
                ProvisionalAppraisalCanonical.Bound(Affection, -signedBound, signedBound),
                ProvisionalAppraisalCanonical.Bound(Fear, 0m, nonnegativeBound),
                ProvisionalAppraisalCanonical.Bound(Resentment, 0m, nonnegativeBound));

        internal IEnumerable<KeyValuePair<string, string>> Fields(string prefix)
        {
            yield return ProvisionalAppraisalCanonical.Pair(prefix + ".affection", ProvisionalAppraisalCanonical.DecimalText(Affection));
            yield return ProvisionalAppraisalCanonical.Pair(prefix + ".fear", ProvisionalAppraisalCanonical.DecimalText(Fear));
            yield return ProvisionalAppraisalCanonical.Pair(prefix + ".resentment", ProvisionalAppraisalCanonical.DecimalText(Resentment));
            yield return ProvisionalAppraisalCanonical.Pair(prefix + ".trust", ProvisionalAppraisalCanonical.DecimalText(Trust));
        }
    }

    public sealed class ProvisionalTraitProfile
    {
        public ProvisionalTraitProfile(
            decimal expressiveness = 0.5m,
            decimal threatVigilance = 0.5m,
            decimal attachmentSensitivity = 0.5m,
            decimal repairOrientation = 0.5m,
            decimal emotionalReactivity = 0.5m)
        {
            Expressiveness = BoundTrait(expressiveness, nameof(expressiveness));
            ThreatVigilance = BoundTrait(threatVigilance, nameof(threatVigilance));
            AttachmentSensitivity = BoundTrait(attachmentSensitivity, nameof(attachmentSensitivity));
            RepairOrientation = BoundTrait(repairOrientation, nameof(repairOrientation));
            EmotionalReactivity = BoundTrait(emotionalReactivity, nameof(emotionalReactivity));
        }

        public decimal Expressiveness { get; }
        public decimal ThreatVigilance { get; }
        public decimal AttachmentSensitivity { get; }
        public decimal RepairOrientation { get; }
        public decimal EmotionalReactivity { get; }
        public decimal ResponseFactor => ProvisionalAppraisalCanonical.Bound(0.75m + EmotionalReactivity * 0.50m, 0.75m, 1.25m);

        private static decimal BoundTrait(decimal value, string parameterName)
        {
            if (value < 0m || value > 1m) throw new ArgumentOutOfRangeException(parameterName);
            return ProvisionalAppraisalCanonical.Quantize(value);
        }
    }

    public sealed class ProvisionalKnowledgeEvidence
    {
        public ProvisionalKnowledgeEvidence(
            string evidenceId,
            string ownerId,
            string sourceEventId,
            ProvisionalPrivacy privacy,
            bool admitted,
            long observedTick,
            ProvisionalFactuality factuality)
        {
            EvidenceId = ProvisionalAppraisalCanonical.Text(evidenceId, nameof(evidenceId));
            OwnerId = ProvisionalAppraisalCanonical.Text(ownerId, nameof(ownerId));
            SourceEventId = ProvisionalAppraisalCanonical.Text(sourceEventId, nameof(sourceEventId));
            if (!Enum.IsDefined(typeof(ProvisionalPrivacy), privacy)) throw new ArgumentOutOfRangeException(nameof(privacy));
            if (!Enum.IsDefined(typeof(ProvisionalFactuality), factuality)) throw new ArgumentOutOfRangeException(nameof(factuality));
            if (observedTick < 0) throw new ArgumentOutOfRangeException(nameof(observedTick));
            Privacy = privacy;
            Admitted = admitted;
            ObservedTick = observedTick;
            Factuality = factuality;
        }

        public string EvidenceId { get; }
        public string OwnerId { get; }
        public string SourceEventId { get; }
        public ProvisionalPrivacy Privacy { get; }
        public bool Admitted { get; }
        public long ObservedTick { get; }
        public ProvisionalFactuality Factuality { get; }
    }

    public sealed class ProvisionalSpeechCue
    {
        public ProvisionalSpeechCue(
            ProvisionalCueType cueType,
            decimal confidence,
            bool directToOwner,
            int quoteDepth,
            ProvisionalFactuality factuality,
            IEnumerable<string> sourceEvidenceIds)
        {
            if (!Enum.IsDefined(typeof(ProvisionalCueType), cueType)) throw new ArgumentOutOfRangeException(nameof(cueType));
            if (!Enum.IsDefined(typeof(ProvisionalFactuality), factuality)) throw new ArgumentOutOfRangeException(nameof(factuality));
            if (confidence < 0m || confidence > 1m) throw new ArgumentOutOfRangeException(nameof(confidence));
            if (quoteDepth < 0 || quoteDepth > 4) throw new ArgumentOutOfRangeException(nameof(quoteDepth));
            CueType = cueType;
            Confidence = ProvisionalAppraisalCanonical.Quantize(confidence);
            DirectToOwner = directToOwner;
            QuoteDepth = quoteDepth;
            Factuality = factuality;
            SourceEvidenceIds = ProvisionalAppraisalCanonical.StableIds(sourceEvidenceIds, nameof(sourceEvidenceIds));
            CanonicalKey = string.Join("|", new[]
            {
                CueType.ToString(),
                ProvisionalAppraisalCanonical.DecimalText(Confidence),
                DirectToOwner ? "1" : "0",
                QuoteDepth.ToString(CultureInfo.InvariantCulture),
                Factuality.ToString(),
                string.Join(",", SourceEvidenceIds)
            });
        }

        public ProvisionalCueType CueType { get; }
        public decimal Confidence { get; }
        public bool DirectToOwner { get; }
        public int QuoteDepth { get; }
        public ProvisionalFactuality Factuality { get; }
        public IReadOnlyList<string> SourceEvidenceIds { get; }
        internal string CanonicalKey { get; }
    }

    public sealed class ProvisionalDialogueAppraisalInput
    {
        public ProvisionalDialogueAppraisalInput(
            string perspectiveOwnerId,
            ObservedDisplayReceipt receipt,
            IEnumerable<ProvisionalSpeechCue> cues,
            IEnumerable<ProvisionalKnowledgeEvidence> knowledge,
            string canonicalAffectFingerprint,
            long canonicalAffectVersion,
            string? relationshipFingerprint,
            long? relationshipVersion,
            ProvisionalTraitProfile traitProfile)
        {
            PerspectiveOwnerId = ProvisionalAppraisalCanonical.Text(perspectiveOwnerId, nameof(perspectiveOwnerId));
            Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
            var cueSource = (cues ?? throw new ArgumentNullException(nameof(cues))).ToArray();
            var canonicalCues = cueSource.OrderBy(value => value.CanonicalKey, StringComparer.Ordinal).ToArray();
            if (canonicalCues.Length == 0 || !canonicalCues.SequenceEqual(cueSource))
                throw new ArgumentException("Cues must be nonempty and canonically ordered.", nameof(cues));
            var knowledgeSource = (knowledge ?? throw new ArgumentNullException(nameof(knowledge))).ToArray();
            var canonicalKnowledge = knowledgeSource.OrderBy(value => value.EvidenceId, StringComparer.Ordinal).ToArray();
            if (!canonicalKnowledge.SequenceEqual(knowledgeSource) ||
                canonicalKnowledge.Select(value => value.EvidenceId).Distinct(StringComparer.Ordinal).Count() != canonicalKnowledge.Length)
                throw new ArgumentException("Knowledge must be unique and canonically ordered.", nameof(knowledge));
            Cues = new ReadOnlyCollection<ProvisionalSpeechCue>(canonicalCues);
            Knowledge = new ReadOnlyCollection<ProvisionalKnowledgeEvidence>(canonicalKnowledge);
            CanonicalAffectFingerprint = ProvisionalAppraisalCanonical.LowerHex(canonicalAffectFingerprint, nameof(canonicalAffectFingerprint));
            if (canonicalAffectVersion < 0) throw new ArgumentOutOfRangeException(nameof(canonicalAffectVersion));
            CanonicalAffectVersion = canonicalAffectVersion;
            RelationshipFingerprint = relationshipFingerprint is null
                ? null
                : ProvisionalAppraisalCanonical.LowerHex(relationshipFingerprint, nameof(relationshipFingerprint));
            if (relationshipVersion < 0) throw new ArgumentOutOfRangeException(nameof(relationshipVersion));
            if ((RelationshipFingerprint is null) != (relationshipVersion is null))
                throw new ArgumentException("Relationship fingerprint and version must both be present or absent.");
            RelationshipVersion = relationshipVersion;
            TraitProfile = traitProfile ?? throw new ArgumentNullException(nameof(traitProfile));
        }

        public string PerspectiveOwnerId { get; }
        public ObservedDisplayReceipt Receipt { get; }
        public IReadOnlyList<ProvisionalSpeechCue> Cues { get; }
        public IReadOnlyList<ProvisionalKnowledgeEvidence> Knowledge { get; }
        public string CanonicalAffectFingerprint { get; }
        public long CanonicalAffectVersion { get; }
        public string? RelationshipFingerprint { get; }
        public long? RelationshipVersion { get; }
        public ProvisionalTraitProfile TraitProfile { get; }
    }

    public sealed class ProvisionalDialogueAppraisal
    {
        internal ProvisionalDialogueAppraisal(
            string appraisalId,
            string perspectiveOwnerId,
            string sessionId,
            string receiptId,
            string utteranceId,
            string conversationId,
            string speakerId,
            long createdTick,
            long expiresTick,
            long checkpointGeneration,
            decimal confidence,
            ProvisionalAffectDelta affectDelta,
            ProvisionalRelationshipDelta relationshipDelta,
            IEnumerable<string> biases,
            ProvisionalPresentationMode presentationMode,
            IEnumerable<string> supportingEvidenceIds,
            IEnumerable<string> reasonCodes,
            string canonicalAffectFingerprint,
            long canonicalAffectVersion,
            string? relationshipFingerprint,
            long? relationshipVersion,
            ProvisionalAppraisalState state,
            string? promotionPacketId = null)
        {
            AppraisalId = ProvisionalAppraisalCanonical.LowerHex(appraisalId, nameof(appraisalId));
            PerspectiveOwnerId = ProvisionalAppraisalCanonical.Text(perspectiveOwnerId, nameof(perspectiveOwnerId));
            SessionId = ProvisionalAppraisalCanonical.Text(sessionId, nameof(sessionId));
            ReceiptId = ProvisionalAppraisalCanonical.LowerHex(receiptId, nameof(receiptId));
            UtteranceId = ProvisionalAppraisalCanonical.Text(utteranceId, nameof(utteranceId));
            ConversationId = ProvisionalAppraisalCanonical.Text(conversationId, nameof(conversationId));
            SpeakerId = ProvisionalAppraisalCanonical.Text(speakerId, nameof(speakerId));
            CreatedTick = createdTick;
            ExpiresTick = expiresTick;
            CheckpointGeneration = checkpointGeneration;
            Confidence = ProvisionalAppraisalCanonical.Quantize(confidence);
            AffectDelta = affectDelta ?? throw new ArgumentNullException(nameof(affectDelta));
            RelationshipDelta = relationshipDelta ?? throw new ArgumentNullException(nameof(relationshipDelta));
            Biases = ProvisionalAppraisalCanonical.StableIds(biases, nameof(biases));
            if (!Enum.IsDefined(typeof(ProvisionalPresentationMode), presentationMode)) throw new ArgumentOutOfRangeException(nameof(presentationMode));
            PresentationMode = presentationMode;
            SupportingEvidenceIds = ProvisionalAppraisalCanonical.StableIds(supportingEvidenceIds, nameof(supportingEvidenceIds));
            ReasonCodes = ProvisionalAppraisalCanonical.StableIds(reasonCodes, nameof(reasonCodes), 1);
            CanonicalAffectFingerprint = ProvisionalAppraisalCanonical.LowerHex(canonicalAffectFingerprint, nameof(canonicalAffectFingerprint));
            CanonicalAffectVersion = canonicalAffectVersion;
            RelationshipFingerprint = relationshipFingerprint;
            RelationshipVersion = relationshipVersion;
            if (!Enum.IsDefined(typeof(ProvisionalAppraisalState), state)) throw new ArgumentOutOfRangeException(nameof(state));
            State = state;
            PromotionPacketId = promotionPacketId;
        }

        public string AppraisalId { get; }
        public string PerspectiveOwnerId { get; }
        public string SessionId { get; }
        public string ReceiptId { get; }
        public string UtteranceId { get; }
        public string ConversationId { get; }
        public string SpeakerId { get; }
        public long CreatedTick { get; }
        public long ExpiresTick { get; }
        public long CheckpointGeneration { get; }
        public decimal Confidence { get; }
        public ProvisionalAffectDelta AffectDelta { get; }
        public ProvisionalRelationshipDelta RelationshipDelta { get; }
        public IReadOnlyList<string> Biases { get; }
        public ProvisionalPresentationMode PresentationMode { get; }
        public IReadOnlyList<string> SupportingEvidenceIds { get; }
        public IReadOnlyList<string> ReasonCodes { get; }
        public string CanonicalAffectFingerprint { get; }
        public long CanonicalAffectVersion { get; }
        public string? RelationshipFingerprint { get; }
        public long? RelationshipVersion { get; }
        public ProvisionalAppraisalState State { get; }
        public string? PromotionPacketId { get; }
        public bool DirectCanonicalMutation => false;
        public bool DirectPawnAuthority => false;

        internal ProvisionalDialogueAppraisal WithState(ProvisionalAppraisalState state, string? packetId = null) =>
            new ProvisionalDialogueAppraisal(
                AppraisalId, PerspectiveOwnerId, SessionId, ReceiptId, UtteranceId, ConversationId,
                SpeakerId, CreatedTick, ExpiresTick, CheckpointGeneration, Confidence, AffectDelta,
                RelationshipDelta, Biases, PresentationMode, SupportingEvidenceIds, ReasonCodes,
                CanonicalAffectFingerprint, CanonicalAffectVersion, RelationshipFingerprint,
                RelationshipVersion, state, packetId);
    }

    public sealed class DurableApplicationPacket
    {
        internal DurableApplicationPacket(
            string packetId,
            ProvisionalDialogueAppraisal appraisal,
            string admittedDialogueEventId,
            ProvisionalAffectDelta proposedAffectDelta,
            ProvisionalRelationshipDelta proposedRelationshipDelta,
            string sourceReceiptId)
        {
            PacketId = ProvisionalAppraisalCanonical.LowerHex(packetId, nameof(packetId));
            AppraisalId = appraisal.AppraisalId;
            PerspectiveOwnerId = appraisal.PerspectiveOwnerId;
            AdmittedDialogueEventId = ProvisionalAppraisalCanonical.Text(admittedDialogueEventId, nameof(admittedDialogueEventId));
            ExpectedCheckpointGeneration = appraisal.CheckpointGeneration;
            ExpectedAffectFingerprint = appraisal.CanonicalAffectFingerprint;
            ExpectedAffectVersion = appraisal.CanonicalAffectVersion;
            ExpectedRelationshipFingerprint = appraisal.RelationshipFingerprint;
            ExpectedRelationshipVersion = appraisal.RelationshipVersion;
            ProposedAffectDelta = proposedAffectDelta;
            ProposedRelationshipDelta = proposedRelationshipDelta;
            SourceReceiptId = ProvisionalAppraisalCanonical.Text(sourceReceiptId, nameof(sourceReceiptId));
        }

        public const string Authority = "PROPOSAL_ONLY_NO_MUTATION_AUTHORITY";
        public string PacketId { get; }
        public string AppraisalId { get; }
        public string PerspectiveOwnerId { get; }
        public string AdmittedDialogueEventId { get; }
        public long ExpectedCheckpointGeneration { get; }
        public string ExpectedAffectFingerprint { get; }
        public long ExpectedAffectVersion { get; }
        public string? ExpectedRelationshipFingerprint { get; }
        public long? ExpectedRelationshipVersion { get; }
        public ProvisionalAffectDelta ProposedAffectDelta { get; }
        public ProvisionalRelationshipDelta ProposedRelationshipDelta { get; }
        public string SourceReceiptId { get; }
        public bool CanonicalMutationAuthority => false;
    }

    public sealed class CanonicalApplicationReceipt
    {
        public const string SourceContractValue = "Mosaic.Core.CanonicalMutationReceipt.v1";

        private CanonicalApplicationReceipt(
            string packetId,
            string perspectiveOwnerId,
            string admittedDialogueEventId,
            long checkpointGeneration,
            bool success,
            string resultingAffectFingerprint,
            long resultingAffectVersion,
            string? resultingRelationshipFingerprint,
            long? resultingRelationshipVersion,
            string sourceContract)
        {
            PacketId = ProvisionalAppraisalCanonical.LowerHex(packetId, nameof(packetId));
            PerspectiveOwnerId = ProvisionalAppraisalCanonical.Text(perspectiveOwnerId, nameof(perspectiveOwnerId));
            AdmittedDialogueEventId = ProvisionalAppraisalCanonical.Text(admittedDialogueEventId, nameof(admittedDialogueEventId));
            if (checkpointGeneration < 0) throw new ArgumentOutOfRangeException(nameof(checkpointGeneration));
            CheckpointGeneration = checkpointGeneration;
            Success = success;
            ResultingAffectFingerprint = ProvisionalAppraisalCanonical.LowerHex(resultingAffectFingerprint, nameof(resultingAffectFingerprint));
            if (resultingAffectVersion < 0) throw new ArgumentOutOfRangeException(nameof(resultingAffectVersion));
            ResultingAffectVersion = resultingAffectVersion;
            ResultingRelationshipFingerprint = resultingRelationshipFingerprint is null
                ? null
                : ProvisionalAppraisalCanonical.LowerHex(resultingRelationshipFingerprint, nameof(resultingRelationshipFingerprint));
            if (resultingRelationshipVersion < 0) throw new ArgumentOutOfRangeException(nameof(resultingRelationshipVersion));
            if ((ResultingRelationshipFingerprint is null) != (resultingRelationshipVersion is null))
                throw new ArgumentException("Relationship result fingerprint and version must both be present or absent.");
            ResultingRelationshipVersion = resultingRelationshipVersion;
            SourceContract = ProvisionalAppraisalCanonical.Text(sourceContract, nameof(sourceContract), 128);
            ReceiptFingerprint = ComputeFingerprint();
        }

        public static CanonicalApplicationReceipt CreateFailure(
            string packetId,
            string perspectiveOwnerId,
            string admittedDialogueEventId,
            long checkpointGeneration,
            string resultingAffectFingerprint,
            long resultingAffectVersion,
            string? resultingRelationshipFingerprint,
            long? resultingRelationshipVersion) =>
            new CanonicalApplicationReceipt(
                packetId, perspectiveOwnerId, admittedDialogueEventId, checkpointGeneration, false,
                resultingAffectFingerprint, resultingAffectVersion, resultingRelationshipFingerprint,
                resultingRelationshipVersion, SourceContractValue);

        internal static CanonicalApplicationReceipt CreateTrusted(
            string packetId,
            string perspectiveOwnerId,
            string admittedDialogueEventId,
            long checkpointGeneration,
            bool success,
            string resultingAffectFingerprint,
            long resultingAffectVersion,
            string? resultingRelationshipFingerprint,
            long? resultingRelationshipVersion,
            string sourceContract) =>
            new CanonicalApplicationReceipt(
                packetId, perspectiveOwnerId, admittedDialogueEventId, checkpointGeneration, success,
                resultingAffectFingerprint, resultingAffectVersion, resultingRelationshipFingerprint,
                resultingRelationshipVersion, sourceContract);

        public string PacketId { get; }
        public string PerspectiveOwnerId { get; }
        public string AdmittedDialogueEventId { get; }
        public long CheckpointGeneration { get; }
        public bool Success { get; }
        public string ResultingAffectFingerprint { get; }
        public long ResultingAffectVersion { get; }
        public string? ResultingRelationshipFingerprint { get; }
        public long? ResultingRelationshipVersion { get; }
        public string SourceContract { get; }
        public string ReceiptFingerprint { get; }

        internal string ComputeFingerprint() =>
            ProvisionalAppraisalCanonical.HashFields(new[]
            {
                ProvisionalAppraisalCanonical.Pair("packet_id", PacketId),
                ProvisionalAppraisalCanonical.Pair("perspective_owner_id", PerspectiveOwnerId),
                ProvisionalAppraisalCanonical.Pair("admitted_dialogue_event_id", AdmittedDialogueEventId),
                ProvisionalAppraisalCanonical.Pair("checkpoint_generation", CheckpointGeneration.ToString(CultureInfo.InvariantCulture)),
                ProvisionalAppraisalCanonical.Pair("success", Success ? "true" : "false"),
                ProvisionalAppraisalCanonical.Pair("resulting_affect_fingerprint", ResultingAffectFingerprint),
                ProvisionalAppraisalCanonical.Pair("resulting_affect_version", ResultingAffectVersion.ToString(CultureInfo.InvariantCulture)),
                ProvisionalAppraisalCanonical.Pair("resulting_relationship_fingerprint", ResultingRelationshipFingerprint ?? "null"),
                ProvisionalAppraisalCanonical.Pair("resulting_relationship_version", ResultingRelationshipVersion?.ToString(CultureInfo.InvariantCulture) ?? "null"),
                ProvisionalAppraisalCanonical.Pair("source_contract", SourceContract)
            });
    }

    public sealed class ProvisionalOverlay
    {
        internal ProvisionalOverlay(
            ProvisionalAffectDelta affect,
            ProvisionalRelationshipDelta relationship,
            IReadOnlyList<string> biases)
        {
            Affect = affect;
            Relationship = relationship;
            Biases = biases;
        }

        public ProvisionalAffectDelta Affect { get; }
        public ProvisionalRelationshipDelta Relationship { get; }
        public IReadOnlyList<string> Biases { get; }
    }

    public sealed class ProvisionalAppraisalDiagnostics
    {
        internal ProvisionalAppraisalDiagnostics(
            string sessionHash,
            IReadOnlyDictionary<string, long> counts,
            int activeCount,
            int packetCount,
            long completedCount,
            int completedRecentCount,
            int filterBytes,
            string stateDigest)
        {
            SessionHash = sessionHash;
            CountsByState = counts;
            ActiveCount = activeCount;
            PacketCount = packetCount;
            CompletedCount = completedCount;
            CompletedRecentCount = completedRecentCount;
            CompletedFilterBytes = filterBytes;
            StateDigest = stateDigest;
        }

        public const string Authority = "READ_ONLY_PROVISIONAL_NO_CANONICAL_OR_PAWN_AUTHORITY";
        public string SessionHash { get; }
        public IReadOnlyDictionary<string, long> CountsByState { get; }
        public int ActiveCount { get; }
        public int PacketCount { get; }
        public long CompletedCount { get; }
        public int CompletedRecentCount { get; }
        public int CompletedFilterBytes { get; }
        public string StateDigest { get; }
        public bool ContainsRawText => false;
        public bool ContainsOwnerOrSpeakerIds => false;
    }
}
