using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dagmay.Core.Appraisal;

namespace Dagmay.Core.Presentation
{
    public enum PresentationTicketStatus
    {
        Prepared = 0,
        InFlight = 1,
        RetryPending = 2,
        Completed = 3,
        Expired = 4,
        Evicted = 5,
        Discarded = 6,
        Orphaned = 7
    }

    public enum ObservedDisplayOutcome
    {
        OBSERVED_SUCCESS = 0,
        ATTEMPT_ONLY = 1,
        FAILED = 2
    }

    internal static class BoundedReactionCanonical
    {
        public static string Text(string value, string parameterName, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
                throw new ArgumentException("A bounded nonblank value is required.", parameterName);
            if (value.IndexOf('\0') >= 0)
                throw new ArgumentException("NUL is not permitted in portable contract text.", parameterName);
            return value;
        }

        public static string? OptionalText(string? value, string parameterName, int maximum)
        {
            if (value is null) return null;
            return Text(value, parameterName, maximum);
        }

        public static string LowerHex(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
                throw new ArgumentException("A lowercase SHA-256 value is required.", parameterName);
            foreach (var character in value)
            {
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                    throw new ArgumentException("A lowercase SHA-256 value is required.", parameterName);
            }
            return value;
        }

        public static IReadOnlyList<string> StableIds(
            IEnumerable<string> values,
            string parameterName,
            int minimum,
            int maximum)
        {
            if (values is null) throw new ArgumentNullException(parameterName);
            var normalized = values
                .Select(value => Text(value, parameterName, 256))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (normalized.Length < minimum || normalized.Length > maximum)
                throw new ArgumentOutOfRangeException(parameterName);
            return new ReadOnlyCollection<string>(normalized);
        }

        public static string HashFields(IEnumerable<KeyValuePair<string, string>> fields)
        {
            if (fields is null) throw new ArgumentNullException(nameof(fields));
            var builder = new StringBuilder();
            foreach (var field in fields)
            {
                var key = Text(field.Key, nameof(fields), 256);
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
                var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                var result = new StringBuilder(bytes.Length * 2);
                foreach (var item in bytes)
                    result.Append(item.ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        public static KeyValuePair<string, string> Pair(string name, string value) =>
            new KeyValuePair<string, string>(name, value);

        public static string Base64(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? throw new ArgumentNullException(nameof(value))));

        public static string FromBase64(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch (FormatException exception)
            {
                throw new ArgumentException("Snapshot contains invalid base64.", nameof(value), exception);
            }
        }

        public static string JoinBase64(IEnumerable<string> values) =>
            string.Join(",", values.Select(Base64));

        public static IReadOnlyList<string> SplitBase64(string value)
        {
            if (value.Length == 0) return new ReadOnlyCollection<string>(Array.Empty<string>());
            return new ReadOnlyCollection<string>(value.Split(',').Select(FromBase64).ToArray());
        }

        public static long NonnegativeLong(string value, string parameterName)
        {
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result < 0)
                throw new ArgumentException("A nonnegative invariant integer is required.", parameterName);
            return result;
        }

        public static int NonnegativeInt(string value, string parameterName)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result < 0)
                throw new ArgumentException("A nonnegative invariant integer is required.", parameterName);
            return result;
        }
    }

    public sealed class BoundedReactionSource
    {
        public const string Contract = "Mosaic.Core.BoundedReactionLifecycle.v1";

        private BoundedReactionSource(
            string sessionId,
            string conversationId,
            string utteranceId,
            string perspectiveOwnerId,
            string speakerId,
            string? recipientId,
            IEnumerable<string> intendedAudienceIds,
            string exactTextHash,
            long checkpointGeneration,
            long createdTick,
            string groundedDecisionId,
            string groundedDecisionFingerprint,
            IEnumerable<string> sourceEventIds,
            bool relationshipPrivate)
        {
            SessionId = BoundedReactionCanonical.Text(sessionId, nameof(sessionId), 256);
            ConversationId = BoundedReactionCanonical.Text(conversationId, nameof(conversationId), 256);
            UtteranceId = BoundedReactionCanonical.Text(utteranceId, nameof(utteranceId), 256);
            PerspectiveOwnerId = BoundedReactionCanonical.Text(perspectiveOwnerId, nameof(perspectiveOwnerId), 256);
            SpeakerId = BoundedReactionCanonical.Text(speakerId, nameof(speakerId), 256);
            RecipientId = BoundedReactionCanonical.OptionalText(recipientId, nameof(recipientId), 256);
            IntendedAudienceIds = BoundedReactionCanonical.StableIds(
                intendedAudienceIds,
                nameof(intendedAudienceIds),
                1,
                32);
            ExactTextHash = BoundedReactionCanonical.LowerHex(exactTextHash, nameof(exactTextHash));
            if (checkpointGeneration < 0) throw new ArgumentOutOfRangeException(nameof(checkpointGeneration));
            if (createdTick < 0) throw new ArgumentOutOfRangeException(nameof(createdTick));
            GroundedDecisionId = BoundedReactionCanonical.LowerHex(groundedDecisionId, nameof(groundedDecisionId));
            GroundedDecisionFingerprint = BoundedReactionCanonical.LowerHex(
                groundedDecisionFingerprint,
                nameof(groundedDecisionFingerprint));
            SourceEventIds = BoundedReactionCanonical.StableIds(sourceEventIds, nameof(sourceEventIds), 1, 16);
            RelationshipPrivate = relationshipPrivate;
            CheckpointGeneration = checkpointGeneration;
            CreatedTick = createdTick;

            if (!string.Equals(PerspectiveOwnerId, SpeakerId, StringComparison.Ordinal))
                throw new ArgumentException("The private perspective owner must be the reaction speaker.");
            if (!RelationshipPrivate)
                throw new ArgumentException("v38 presentation sources must remain relationship-private.");
            if (!IntendedAudienceIds.Contains(SpeakerId, StringComparer.Ordinal))
                throw new ArgumentException("The intended audience must include the speaker.", nameof(intendedAudienceIds));
            if (RecipientId is not null &&
                !IntendedAudienceIds.Contains(RecipientId, StringComparer.Ordinal))
                throw new ArgumentException("A recipient must be included in the intended audience.", nameof(recipientId));

            Fingerprint = ComputeFingerprint();
        }

        public static BoundedReactionSource FromGroundedDecision(
            GroundedSocialAppraisalProposal decision,
            string sessionId,
            string conversationId,
            string utteranceId,
            string speakerId,
            string? recipientId,
            IEnumerable<string> intendedAudienceIds,
            string exactTextHash,
            long checkpointGeneration,
            long createdTick)
        {
            if (decision is null) throw new ArgumentNullException(nameof(decision));
            if (createdTick < decision.Bundle.Tick)
                throw new ArgumentException("A presentation source cannot predate its grounded decision.", nameof(createdTick));
            var owner = decision.PerspectiveOwnerId.ToString();
            if (!string.Equals(owner, speakerId, StringComparison.Ordinal))
                throw new ArgumentException("The grounded decision owner must be the reaction speaker.", nameof(speakerId));
            var fingerprintFields = new List<KeyValuePair<string, string>>
            {
                BoundedReactionCanonical.Pair("appraisal_id", decision.AppraisalId),
                BoundedReactionCanonical.Pair("bundle_id", decision.Bundle.BundleId),
                BoundedReactionCanonical.Pair("owner_id", owner),
                BoundedReactionCanonical.Pair("counterpart_id", decision.CounterpartId.ToString()),
                BoundedReactionCanonical.Pair("primary_family", decision.PrimaryFamily.ToString()),
                BoundedReactionCanonical.Pair("secondary_family", decision.SecondaryFamily?.ToString() ?? "null"),
                BoundedReactionCanonical.Pair("intensity", decision.Intensity.ToString(CultureInfo.InvariantCulture))
            };
            foreach (var evidenceId in decision.SourceEvidenceIds.OrderBy(value => value.ToString(), StringComparer.Ordinal))
                fingerprintFields.Add(BoundedReactionCanonical.Pair("source_event_id", evidenceId.ToString()));
            foreach (var rule in decision.StableRuleIds)
                fingerprintFields.Add(BoundedReactionCanonical.Pair("stable_rule_id", rule));
            var decisionFingerprint = BoundedReactionCanonical.HashFields(fingerprintFields);
            return new BoundedReactionSource(
                sessionId,
                conversationId,
                utteranceId,
                owner,
                speakerId,
                recipientId,
                intendedAudienceIds,
                exactTextHash,
                checkpointGeneration,
                createdTick,
                decision.AppraisalId,
                decisionFingerprint,
                decision.SourceEvidenceIds.Select(value => value.ToString()),
                relationshipPrivate: true);
        }

        internal static BoundedReactionSource Restore(
            string sessionId,
            string conversationId,
            string utteranceId,
            string perspectiveOwnerId,
            string speakerId,
            string? recipientId,
            IEnumerable<string> intendedAudienceIds,
            string exactTextHash,
            long checkpointGeneration,
            long createdTick,
            string groundedDecisionId,
            string groundedDecisionFingerprint,
            IEnumerable<string> sourceEventIds,
            bool relationshipPrivate) =>
            new BoundedReactionSource(
                sessionId,
                conversationId,
                utteranceId,
                perspectiveOwnerId,
                speakerId,
                recipientId,
                intendedAudienceIds,
                exactTextHash,
                checkpointGeneration,
                createdTick,
                groundedDecisionId,
                groundedDecisionFingerprint,
                sourceEventIds,
                relationshipPrivate);

        public string SessionId { get; }
        public string ConversationId { get; }
        public string UtteranceId { get; }
        public string PerspectiveOwnerId { get; }
        public string SpeakerId { get; }
        public string? RecipientId { get; }
        public IReadOnlyList<string> IntendedAudienceIds { get; }
        public string ExactTextHash { get; }
        public long CheckpointGeneration { get; }
        public long CreatedTick { get; }
        public string GroundedDecisionId { get; }
        public string GroundedDecisionFingerprint { get; }
        public IReadOnlyList<string> SourceEventIds { get; }
        public bool RelationshipPrivate { get; }
        public string Fingerprint { get; }
        public bool DirectCharacterMutation => false;
        public bool DirectActionAuthority => false;

        internal string Export()
        {
            return string.Join("|", new[]
            {
                BoundedReactionCanonical.Base64(SessionId),
                BoundedReactionCanonical.Base64(ConversationId),
                BoundedReactionCanonical.Base64(UtteranceId),
                BoundedReactionCanonical.Base64(PerspectiveOwnerId),
                BoundedReactionCanonical.Base64(SpeakerId),
                RecipientId is null ? "-" : BoundedReactionCanonical.Base64(RecipientId),
                BoundedReactionCanonical.JoinBase64(IntendedAudienceIds),
                ExactTextHash,
                CheckpointGeneration.ToString(CultureInfo.InvariantCulture),
                CreatedTick.ToString(CultureInfo.InvariantCulture),
                GroundedDecisionId,
                GroundedDecisionFingerprint,
                BoundedReactionCanonical.JoinBase64(SourceEventIds),
                RelationshipPrivate ? "1" : "0"
            });
        }

        internal static BoundedReactionSource Import(string encoded)
        {
            var fields = encoded.Split('|');
            if (fields.Length != 14) throw new ArgumentException("Invalid source snapshot field count.", nameof(encoded));
            return Restore(
                BoundedReactionCanonical.FromBase64(fields[0]),
                BoundedReactionCanonical.FromBase64(fields[1]),
                BoundedReactionCanonical.FromBase64(fields[2]),
                BoundedReactionCanonical.FromBase64(fields[3]),
                BoundedReactionCanonical.FromBase64(fields[4]),
                fields[5] == "-" ? null : BoundedReactionCanonical.FromBase64(fields[5]),
                BoundedReactionCanonical.SplitBase64(fields[6]),
                fields[7],
                BoundedReactionCanonical.NonnegativeLong(fields[8], nameof(encoded)),
                BoundedReactionCanonical.NonnegativeLong(fields[9], nameof(encoded)),
                fields[10],
                fields[11],
                BoundedReactionCanonical.SplitBase64(fields[12]),
                fields[13] == "1");
        }

        private string ComputeFingerprint()
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                BoundedReactionCanonical.Pair("source_contract", Contract),
                BoundedReactionCanonical.Pair("session_id", SessionId),
                BoundedReactionCanonical.Pair("conversation_id", ConversationId),
                BoundedReactionCanonical.Pair("utterance_id", UtteranceId),
                BoundedReactionCanonical.Pair("perspective_owner_id", PerspectiveOwnerId),
                BoundedReactionCanonical.Pair("speaker_id", SpeakerId),
                BoundedReactionCanonical.Pair("recipient_id", RecipientId ?? "null"),
                BoundedReactionCanonical.Pair("exact_text_hash", ExactTextHash),
                BoundedReactionCanonical.Pair("checkpoint_generation", CheckpointGeneration.ToString(CultureInfo.InvariantCulture)),
                BoundedReactionCanonical.Pair("created_tick", CreatedTick.ToString(CultureInfo.InvariantCulture)),
                BoundedReactionCanonical.Pair("grounded_decision_id", GroundedDecisionId),
                BoundedReactionCanonical.Pair("grounded_decision_fingerprint", GroundedDecisionFingerprint),
                BoundedReactionCanonical.Pair("relationship_private", RelationshipPrivate ? "true" : "false")
            };
            foreach (var audienceId in IntendedAudienceIds)
                fields.Add(BoundedReactionCanonical.Pair("intended_audience_id", audienceId));
            foreach (var sourceEventId in SourceEventIds)
                fields.Add(BoundedReactionCanonical.Pair("source_event_id", sourceEventId));
            return BoundedReactionCanonical.HashFields(fields);
        }
    }

    public sealed class PresentationTicket
    {
        internal PresentationTicket(
            BoundedReactionSource source,
            long expiryTick,
            int retryCount = 0,
            PresentationTicketStatus status = PresentationTicketStatus.Prepared,
            long nextAttemptNotBeforeTick = 0,
            string? ticketId = null)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            if (expiryTick < source.CreatedTick) throw new ArgumentOutOfRangeException(nameof(expiryTick));
            if (retryCount < 0 || retryCount > BoundedReactionLifecycle.MaximumAttempts)
                throw new ArgumentOutOfRangeException(nameof(retryCount));
            if (!Enum.IsDefined(typeof(PresentationTicketStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (nextAttemptNotBeforeTick < 0) throw new ArgumentOutOfRangeException(nameof(nextAttemptNotBeforeTick));
            CreatedTick = source.CreatedTick;
            ExpiryTick = expiryTick;
            RetryCount = retryCount;
            Status = status;
            NextAttemptNotBeforeTick = nextAttemptNotBeforeTick;
            var computed = BoundedReactionCanonical.HashFields(new[]
            {
                BoundedReactionCanonical.Pair("kind", "presentation_ticket"),
                BoundedReactionCanonical.Pair("source_fingerprint", source.Fingerprint),
                BoundedReactionCanonical.Pair("expiry_tick", expiryTick.ToString(CultureInfo.InvariantCulture))
            });
            TicketId = ticketId is null
                ? computed
                : BoundedReactionCanonical.LowerHex(ticketId, nameof(ticketId));
            if (!string.Equals(TicketId, computed, StringComparison.Ordinal))
                throw new ArgumentException("Ticket ID does not match its canonical source.", nameof(ticketId));
        }

        public string TicketId { get; }
        public BoundedReactionSource Source { get; }
        public long CreatedTick { get; }
        public long ExpiryTick { get; }
        public int RetryCount { get; }
        public PresentationTicketStatus Status { get; }
        public long NextAttemptNotBeforeTick { get; }
        public bool DirectActionAuthority => false;

        internal PresentationTicket With(
            PresentationTicketStatus status,
            int? retryCount = null,
            long? nextAttemptNotBeforeTick = null) =>
            new PresentationTicket(
                Source,
                ExpiryTick,
                retryCount ?? RetryCount,
                status,
                nextAttemptNotBeforeTick ?? NextAttemptNotBeforeTick,
                TicketId);
    }

    public sealed class ReleaseAttempt
    {
        internal ReleaseAttempt(
            string ticketId,
            int attemptOrdinal,
            long requestedTick,
            long checkpointGeneration,
            string? attemptId = null)
        {
            TicketId = BoundedReactionCanonical.LowerHex(ticketId, nameof(ticketId));
            if (attemptOrdinal < 1 || attemptOrdinal > BoundedReactionLifecycle.MaximumAttempts)
                throw new ArgumentOutOfRangeException(nameof(attemptOrdinal));
            if (requestedTick < 0) throw new ArgumentOutOfRangeException(nameof(requestedTick));
            if (checkpointGeneration < 0) throw new ArgumentOutOfRangeException(nameof(checkpointGeneration));
            AttemptOrdinal = attemptOrdinal;
            RequestedTick = requestedTick;
            CheckpointGeneration = checkpointGeneration;
            var computed = BoundedReactionCanonical.HashFields(new[]
            {
                BoundedReactionCanonical.Pair("kind", "release_attempt"),
                BoundedReactionCanonical.Pair("ticket_id", TicketId),
                BoundedReactionCanonical.Pair("attempt_ordinal", AttemptOrdinal.ToString(CultureInfo.InvariantCulture)),
                BoundedReactionCanonical.Pair("requested_tick", RequestedTick.ToString(CultureInfo.InvariantCulture)),
                BoundedReactionCanonical.Pair("checkpoint_generation", CheckpointGeneration.ToString(CultureInfo.InvariantCulture))
            });
            AttemptId = attemptId is null
                ? computed
                : BoundedReactionCanonical.LowerHex(attemptId, nameof(attemptId));
            if (!string.Equals(AttemptId, computed, StringComparison.Ordinal))
                throw new ArgumentException("Attempt ID does not match its canonical fields.", nameof(attemptId));
        }

        public string AttemptId { get; }
        public string TicketId { get; }
        public int AttemptOrdinal { get; }
        public long RequestedTick { get; }
        public long CheckpointGeneration { get; }
        public ObservedDisplayOutcome Outcome => ObservedDisplayOutcome.ATTEMPT_ONLY;
        public bool DeclaresSuccess => false;
        public bool DirectActionAuthority => false;
    }

    public sealed class ObservedDisplayReceipt
    {
        public const string SourceContractValue = BoundedReactionSource.Contract;
        public const string SourceGateDigestValue = "f44b00eee5a2fe8d7609bbd80caa79948caf388760f2e33765ec64ad50c3e0d8";

        private ObservedDisplayReceipt(
            string sessionId,
            string releaseAttemptId,
            string receiptId,
            string utteranceId,
            string conversationId,
            string speakerId,
            string? recipientId,
            IEnumerable<string> audienceIds,
            string exactTextHash,
            long displayedTick,
            long checkpointGeneration,
            ObservedDisplayOutcome outcome,
            string sourceContract,
            string sourceGateDigest)
        {
            SessionId = BoundedReactionCanonical.Text(sessionId, nameof(sessionId), 256);
            ReleaseAttemptId = BoundedReactionCanonical.LowerHex(releaseAttemptId, nameof(releaseAttemptId));
            ReceiptId = BoundedReactionCanonical.LowerHex(receiptId, nameof(receiptId));
            UtteranceId = BoundedReactionCanonical.Text(utteranceId, nameof(utteranceId), 256);
            ConversationId = BoundedReactionCanonical.Text(conversationId, nameof(conversationId), 256);
            SpeakerId = BoundedReactionCanonical.Text(speakerId, nameof(speakerId), 256);
            RecipientId = BoundedReactionCanonical.OptionalText(recipientId, nameof(recipientId), 256);
            AudienceIds = BoundedReactionCanonical.StableIds(audienceIds, nameof(audienceIds), 1, 32);
            ExactTextHash = BoundedReactionCanonical.LowerHex(exactTextHash, nameof(exactTextHash));
            if (displayedTick < 0) throw new ArgumentOutOfRangeException(nameof(displayedTick));
            if (checkpointGeneration < 0) throw new ArgumentOutOfRangeException(nameof(checkpointGeneration));
            if (!Enum.IsDefined(typeof(ObservedDisplayOutcome), outcome))
                throw new ArgumentOutOfRangeException(nameof(outcome));
            SourceContract = BoundedReactionCanonical.Text(sourceContract, nameof(sourceContract), 128);
            SourceGateDigest = BoundedReactionCanonical.LowerHex(sourceGateDigest, nameof(sourceGateDigest));
            if (!AudienceIds.Contains(SpeakerId, StringComparer.Ordinal))
                throw new ArgumentException("The actual audience must include the speaker.", nameof(audienceIds));
            DisplayedTick = displayedTick;
            CheckpointGeneration = checkpointGeneration;
            Outcome = outcome;
            ReceiptFingerprint = ComputeFingerprint();
        }

        public static ObservedDisplayReceipt CreateNonSuccess(
            string sessionId,
            string releaseAttemptId,
            string receiptId,
            string utteranceId,
            string conversationId,
            string speakerId,
            string? recipientId,
            IEnumerable<string> audienceIds,
            string exactTextHash,
            long displayedTick,
            long checkpointGeneration,
            ObservedDisplayOutcome outcome,
            string sourceContract,
            string sourceGateDigest)
        {
            if (outcome == ObservedDisplayOutcome.OBSERVED_SUCCESS)
                throw new ArgumentException("Public Core callers cannot manufacture observed success.", nameof(outcome));
            return new ObservedDisplayReceipt(
                sessionId,
                releaseAttemptId,
                receiptId,
                utteranceId,
                conversationId,
                speakerId,
                recipientId,
                audienceIds,
                exactTextHash,
                displayedTick,
                checkpointGeneration,
                outcome,
                sourceContract,
                sourceGateDigest);
        }

        internal static ObservedDisplayReceipt CreateTrusted(
            string sessionId,
            string releaseAttemptId,
            string receiptId,
            string utteranceId,
            string conversationId,
            string speakerId,
            string? recipientId,
            IEnumerable<string> audienceIds,
            string exactTextHash,
            long displayedTick,
            long checkpointGeneration,
            ObservedDisplayOutcome outcome,
            string sourceContract,
            string sourceGateDigest) =>
            new ObservedDisplayReceipt(
                sessionId,
                releaseAttemptId,
                receiptId,
                utteranceId,
                conversationId,
                speakerId,
                recipientId,
                audienceIds,
                exactTextHash,
                displayedTick,
                checkpointGeneration,
                outcome,
                sourceContract,
                sourceGateDigest);

        public string SessionId { get; }
        public string ReleaseAttemptId { get; }
        public string ReceiptId { get; }
        public string UtteranceId { get; }
        public string ConversationId { get; }
        public string SpeakerId { get; }
        public string? RecipientId { get; }
        public IReadOnlyList<string> AudienceIds { get; }
        public string ExactTextHash { get; }
        public long DisplayedTick { get; }
        public long CheckpointGeneration { get; }
        public ObservedDisplayOutcome Outcome { get; }
        public string SourceContract { get; }
        public string SourceGateDigest { get; }
        public string ReceiptFingerprint { get; }
        public bool DirectCharacterMutation => false;
        public bool DirectActionAuthority => false;

        private string ComputeFingerprint()
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                BoundedReactionCanonical.Pair("session_id", SessionId),
                BoundedReactionCanonical.Pair("release_attempt_id", ReleaseAttemptId),
                BoundedReactionCanonical.Pair("receipt_id", ReceiptId),
                BoundedReactionCanonical.Pair("utterance_id", UtteranceId),
                BoundedReactionCanonical.Pair("conversation_id", ConversationId),
                BoundedReactionCanonical.Pair("speaker_id", SpeakerId),
                BoundedReactionCanonical.Pair("recipient_id", RecipientId ?? "null")
            };
            foreach (var audienceId in AudienceIds)
                fields.Add(BoundedReactionCanonical.Pair("audience_id", audienceId));
            fields.Add(BoundedReactionCanonical.Pair("exact_text_hash", ExactTextHash));
            fields.Add(BoundedReactionCanonical.Pair("displayed_tick", DisplayedTick.ToString(CultureInfo.InvariantCulture)));
            fields.Add(BoundedReactionCanonical.Pair("checkpoint_generation", CheckpointGeneration.ToString(CultureInfo.InvariantCulture)));
            fields.Add(BoundedReactionCanonical.Pair("outcome", Outcome.ToString()));
            fields.Add(BoundedReactionCanonical.Pair("source_contract", SourceContract));
            fields.Add(BoundedReactionCanonical.Pair("source_gate_digest", SourceGateDigest));
            return BoundedReactionCanonical.HashFields(fields);
        }
    }

    public sealed class SuccessfulHistoryProjection
    {
        internal SuccessfulHistoryProjection(
            string ticketId,
            string receiptId,
            string receiptFingerprint,
            string perspectiveOwnerId,
            IEnumerable<string> actualWitnessIds,
            long displayedTick)
        {
            TicketId = BoundedReactionCanonical.LowerHex(ticketId, nameof(ticketId));
            ReceiptId = BoundedReactionCanonical.LowerHex(receiptId, nameof(receiptId));
            ReceiptFingerprint = BoundedReactionCanonical.LowerHex(receiptFingerprint, nameof(receiptFingerprint));
            PerspectiveOwnerId = BoundedReactionCanonical.Text(perspectiveOwnerId, nameof(perspectiveOwnerId), 256);
            ActualWitnessIds = BoundedReactionCanonical.StableIds(actualWitnessIds, nameof(actualWitnessIds), 1, 32);
            if (!ActualWitnessIds.Contains(PerspectiveOwnerId, StringComparer.Ordinal))
                throw new ArgumentException("The private owner must be an actual witness.", nameof(actualWitnessIds));
            if (displayedTick < 0) throw new ArgumentOutOfRangeException(nameof(displayedTick));
            DisplayedTick = displayedTick;
            ProjectionFingerprint = BoundedReactionCanonical.HashFields(
                new[]
                {
                    BoundedReactionCanonical.Pair("ticket_id", TicketId),
                    BoundedReactionCanonical.Pair("receipt_id", ReceiptId),
                    BoundedReactionCanonical.Pair("receipt_fingerprint", ReceiptFingerprint),
                    BoundedReactionCanonical.Pair("owner_id", PerspectiveOwnerId),
                    BoundedReactionCanonical.Pair("actual_witness_ids", string.Join(",", ActualWitnessIds)),
                    BoundedReactionCanonical.Pair("displayed_tick", DisplayedTick.ToString(CultureInfo.InvariantCulture))
                });
        }

        public string TicketId { get; }
        public string ReceiptId { get; }
        public string ReceiptFingerprint { get; }
        public string PerspectiveOwnerId { get; }
        public IReadOnlyList<string> ActualWitnessIds { get; }
        public long DisplayedTick { get; }
        public string ProjectionFingerprint { get; }
        public bool ContainsRawDialogue => false;
        public bool DirectCharacterMutation => false;
    }

    public sealed class RetrySchedule
    {
        internal RetrySchedule(string ticketId, int nextAttemptOrdinal, long notBeforeTick)
        {
            TicketId = BoundedReactionCanonical.LowerHex(ticketId, nameof(ticketId));
            if (nextAttemptOrdinal < 1 || nextAttemptOrdinal > BoundedReactionLifecycle.MaximumAttempts)
                throw new ArgumentOutOfRangeException(nameof(nextAttemptOrdinal));
            if (notBeforeTick < 0) throw new ArgumentOutOfRangeException(nameof(notBeforeTick));
            NextAttemptOrdinal = nextAttemptOrdinal;
            NotBeforeTick = notBeforeTick;
        }

        public string TicketId { get; }
        public int NextAttemptOrdinal { get; }
        public long NotBeforeTick { get; }
    }

    internal sealed class LifecycleTombstone
    {
        public LifecycleTombstone(
            string ticketId,
            PresentationTicketStatus status,
            long terminalTick,
            string terminalDigest)
        {
            TicketId = BoundedReactionCanonical.LowerHex(ticketId, nameof(ticketId));
            if (status != PresentationTicketStatus.Completed &&
                status != PresentationTicketStatus.Expired &&
                status != PresentationTicketStatus.Evicted &&
                status != PresentationTicketStatus.Discarded &&
                status != PresentationTicketStatus.Orphaned)
                throw new ArgumentException("A tombstone requires a terminal status.", nameof(status));
            if (terminalTick < 0) throw new ArgumentOutOfRangeException(nameof(terminalTick));
            Status = status;
            TerminalTick = terminalTick;
            TerminalDigest = BoundedReactionCanonical.LowerHex(terminalDigest, nameof(terminalDigest));
        }

        public string TicketId { get; }
        public PresentationTicketStatus Status { get; }
        public long TerminalTick { get; }
        public string TerminalDigest { get; }
    }

    public sealed class BoundedReactionLifecycle
    {
        public const int MaximumLiveTickets = 8;
        public const int MaximumAttempts = 3;
        public const int MaximumTerminalRecords = 32;

        private readonly SortedDictionary<string, PresentationTicket> _live =
            new SortedDictionary<string, PresentationTicket>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, ReleaseAttempt> _activeAttempts =
            new SortedDictionary<string, ReleaseAttempt>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, LifecycleTombstone> _tombstones =
            new SortedDictionary<string, LifecycleTombstone>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, SuccessfulHistoryProjection> _projections =
            new SortedDictionary<string, SuccessfulHistoryProjection>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, string> _receiptFingerprints =
            new SortedDictionary<string, string>(StringComparer.Ordinal);

        public BoundedReactionLifecycle(
            string storeId,
            string perspectiveOwnerId,
            string sessionId,
            long checkpointGeneration)
        {
            StoreId = BoundedReactionCanonical.Text(storeId, nameof(storeId), 256);
            PerspectiveOwnerId = BoundedReactionCanonical.Text(
                perspectiveOwnerId,
                nameof(perspectiveOwnerId),
                256);
            SessionId = BoundedReactionCanonical.Text(sessionId, nameof(sessionId), 256);
            if (checkpointGeneration < 0) throw new ArgumentOutOfRangeException(nameof(checkpointGeneration));
            CheckpointGeneration = checkpointGeneration;
        }

        public string StoreId { get; }
        public string PerspectiveOwnerId { get; }
        public string SessionId { get; }
        public long CheckpointGeneration { get; }
        public int LiveCount => _live.Count;
        public int TombstoneCount => _tombstones.Count;
        public IReadOnlyList<PresentationTicket> LiveTickets =>
            new ReadOnlyCollection<PresentationTicket>(_live.Values.ToArray());
        public IReadOnlyList<SuccessfulHistoryProjection> SuccessfulProjections =>
            new ReadOnlyCollection<SuccessfulHistoryProjection>(_projections.Values.ToArray());
        public bool ProviderCallAuthority => false;
        public bool CanonicalMutationAuthority => false;
        public bool UiRenderingAuthority => false;
        public bool PawnActionAuthority => false;

        public PresentationTicket Prepare(BoundedReactionSource source, long expiryTick)
        {
            ValidateSourceBoundary(source);
            var candidate = new PresentationTicket(source, expiryTick);
            if (_tombstones.ContainsKey(candidate.TicketId))
                throw new ArgumentException("A terminal ticket cannot be revived.", nameof(source));
            if (_live.TryGetValue(candidate.TicketId, out var existing))
                return existing;
            _live.Add(candidate.TicketId, candidate);
            if (_live.Count > MaximumLiveTickets)
            {
                var eviction = _live.Values
                    .OrderBy(value => value.CreatedTick)
                    .ThenBy(value => value.TicketId, StringComparer.Ordinal)
                    .First();
                Terminalize(eviction, PresentationTicketStatus.Evicted, eviction.CreatedTick, "bounded-queue-eviction");
                if (string.Equals(eviction.TicketId, candidate.TicketId, StringComparison.Ordinal))
                    return candidate.With(PresentationTicketStatus.Evicted);
            }
            return candidate;
        }

        public ReleaseAttempt BeginAttempt(string ticketId, long requestedTick)
        {
            var ticket = RequireLive(ticketId);
            if (requestedTick < ticket.CreatedTick || requestedTick > ticket.ExpiryTick)
            {
                if (requestedTick > ticket.ExpiryTick)
                    Terminalize(ticket, PresentationTicketStatus.Expired, requestedTick, "expired-before-attempt");
                throw new ArgumentOutOfRangeException(nameof(requestedTick));
            }
            if (ticket.Status == PresentationTicketStatus.InFlight)
                throw new InvalidOperationException("A ticket already has an active attempt.");
            if (ticket.Status == PresentationTicketStatus.RetryPending &&
                requestedTick < ticket.NextAttemptNotBeforeTick)
                throw new InvalidOperationException("The deterministic retry delay has not elapsed.");
            var ordinal = ticket.RetryCount + 1;
            if (ordinal > MaximumAttempts)
                throw new InvalidOperationException("The bounded attempt limit has been reached.");
            var attempt = new ReleaseAttempt(ticket.TicketId, ordinal, requestedTick, CheckpointGeneration);
            _activeAttempts[ticket.TicketId] = attempt;
            _live[ticket.TicketId] = ticket.With(PresentationTicketStatus.InFlight);
            return attempt;
        }

        public SuccessfulHistoryProjection? ObserveReceipt(
            ObservedDisplayReceipt receipt,
            long callerTick)
        {
            if (receipt is null) throw new ArgumentNullException(nameof(receipt));
            if (callerTick < 0) throw new ArgumentOutOfRangeException(nameof(callerTick));
            if (_receiptFingerprints.TryGetValue(receipt.ReceiptId, out var existingFingerprint))
            {
                if (!string.Equals(existingFingerprint, receipt.ReceiptFingerprint, StringComparison.Ordinal))
                    throw new ArgumentException("A conflicting duplicate receipt failed closed.", nameof(receipt));
                return _projections.TryGetValue(receipt.ReceiptId, out var duplicateProjection)
                    ? duplicateProjection
                    : null;
            }
            var attempt = _activeAttempts.Values.SingleOrDefault(
                value => string.Equals(value.AttemptId, receipt.ReleaseAttemptId, StringComparison.Ordinal));
            if (attempt is null)
                throw new ArgumentException("Receipt does not bind the active release attempt.", nameof(receipt));
            var ticket = RequireLive(attempt.TicketId);
            ValidateReceipt(ticket, attempt, receipt, callerTick);
            _receiptFingerprints.Add(receipt.ReceiptId, receipt.ReceiptFingerprint);

            if (receipt.Outcome != ObservedDisplayOutcome.OBSERVED_SUCCESS)
            {
                _activeAttempts.Remove(ticket.TicketId);
                _live[ticket.TicketId] = ticket.With(
                    PresentationTicketStatus.RetryPending,
                    retryCount: attempt.AttemptOrdinal);
                TrimReceiptFingerprints();
                return null;
            }

            var projection = new SuccessfulHistoryProjection(
                ticket.TicketId,
                receipt.ReceiptId,
                receipt.ReceiptFingerprint,
                PerspectiveOwnerId,
                receipt.AudienceIds,
                receipt.DisplayedTick);
            _projections.Add(receipt.ReceiptId, projection);
            Terminalize(ticket, PresentationTicketStatus.Completed, receipt.DisplayedTick, receipt.ReceiptFingerprint);
            TrimReceiptFingerprints();
            return projection;
        }

        public RetrySchedule ScheduleRetry(string ticketId, long callerTick)
        {
            var ticket = RequireLive(ticketId);
            if (callerTick < ticket.CreatedTick || callerTick > ticket.ExpiryTick)
                throw new ArgumentOutOfRangeException(nameof(callerTick));
            if (ticket.Status != PresentationTicketStatus.RetryPending)
                throw new InvalidOperationException("Only a failed or attempt-only receipt can schedule a retry.");
            if (ticket.RetryCount >= MaximumAttempts)
                throw new InvalidOperationException("The bounded attempt limit has been reached.");
            var delay = 10L << Math.Min(ticket.RetryCount - 1, 3);
            var notBefore = callerTick > long.MaxValue - delay ? long.MaxValue : callerTick + delay;
            if (notBefore > ticket.ExpiryTick)
                throw new InvalidOperationException("The next deterministic retry falls after expiry.");
            _live[ticket.TicketId] = ticket.With(
                PresentationTicketStatus.RetryPending,
                nextAttemptNotBeforeTick: notBefore);
            return new RetrySchedule(ticket.TicketId, ticket.RetryCount + 1, notBefore);
        }

        public int Expire(long callerTick)
        {
            if (callerTick < 0) throw new ArgumentOutOfRangeException(nameof(callerTick));
            var expired = _live.Values
                .Where(value => value.ExpiryTick < callerTick)
                .OrderBy(value => value.ExpiryTick)
                .ThenBy(value => value.TicketId, StringComparer.Ordinal)
                .ToArray();
            foreach (var ticket in expired)
                Terminalize(ticket, PresentationTicketStatus.Expired, callerTick, "caller-driven-expiry");
            return expired.Length;
        }

        public void Discard(string ticketId, long callerTick)
        {
            if (callerTick < 0) throw new ArgumentOutOfRangeException(nameof(callerTick));
            Terminalize(RequireLive(ticketId), PresentationTicketStatus.Discarded, callerTick, "explicit-discard");
        }

        public void RecoverOrphan(string ticketId, long callerTick)
        {
            if (callerTick < 0) throw new ArgumentOutOfRangeException(nameof(callerTick));
            var ticket = RequireLive(ticketId);
            if (ticket.Status != PresentationTicketStatus.InFlight)
                throw new InvalidOperationException("Only an unproven in-flight ticket can become an orphan.");
            Terminalize(ticket, PresentationTicketStatus.Orphaned, callerTick, "unproven-orphan");
        }

        public BoundedReactionLifecycleSnapshot Checkpoint() =>
            BoundedReactionLifecycleSnapshot.Create(
                StoreId,
                PerspectiveOwnerId,
                SessionId,
                CheckpointGeneration,
                _live.Values,
                _activeAttempts.Values,
                _tombstones.Values,
                _projections.Values,
                _receiptFingerprints);

        public static BoundedReactionLifecycle Restore(
            BoundedReactionLifecycleSnapshot snapshot,
            string expectedStoreId,
            string expectedPerspectiveOwnerId,
            string expectedSessionId,
            long saveCheckpointGeneration)
        {
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            snapshot.Validate();
            if (!string.Equals(snapshot.StoreId, expectedStoreId, StringComparison.Ordinal) ||
                !string.Equals(snapshot.PerspectiveOwnerId, expectedPerspectiveOwnerId, StringComparison.Ordinal) ||
                !string.Equals(snapshot.SessionId, expectedSessionId, StringComparison.Ordinal))
                throw new ArgumentException("Snapshot identity/session binding failed.", nameof(snapshot));
            if (snapshot.CheckpointGeneration != saveCheckpointGeneration)
                throw new ArgumentException("Snapshot checkpoint does not match the save generation.", nameof(snapshot));
            var lifecycle = new BoundedReactionLifecycle(
                snapshot.StoreId,
                snapshot.PerspectiveOwnerId,
                snapshot.SessionId,
                snapshot.CheckpointGeneration);
            foreach (var ticket in snapshot.LiveTickets)
            {
                lifecycle.ValidateSourceBoundary(ticket.Source);
                lifecycle._live.Add(ticket.TicketId, ticket);
            }
            foreach (var attempt in snapshot.ActiveAttempts)
            {
                if (!lifecycle._live.ContainsKey(attempt.TicketId))
                    throw new ArgumentException("Snapshot attempt has no recoverable ticket.", nameof(snapshot));
                lifecycle._activeAttempts.Add(attempt.TicketId, attempt);
            }
            foreach (var tombstone in snapshot.Tombstones)
                lifecycle._tombstones.Add(tombstone.TicketId, tombstone);
            foreach (var projection in snapshot.Projections)
                lifecycle._projections.Add(projection.ReceiptId, projection);
            foreach (var pair in snapshot.ReceiptFingerprints)
                lifecycle._receiptFingerprints.Add(pair.Key, pair.Value);
            return lifecycle;
        }

        private void ValidateSourceBoundary(BoundedReactionSource source)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (!string.Equals(source.PerspectiveOwnerId, PerspectiveOwnerId, StringComparison.Ordinal) ||
                !string.Equals(source.SessionId, SessionId, StringComparison.Ordinal) ||
                source.CheckpointGeneration != CheckpointGeneration)
                throw new ArgumentException("Foreign owner, session, or checkpoint source failed closed.", nameof(source));
        }

        private void ValidateReceipt(
            PresentationTicket ticket,
            ReleaseAttempt attempt,
            ObservedDisplayReceipt receipt,
            long callerTick)
        {
            var source = ticket.Source;
            if (!string.Equals(receipt.SessionId, SessionId, StringComparison.Ordinal) ||
                !string.Equals(receipt.ReleaseAttemptId, attempt.AttemptId, StringComparison.Ordinal) ||
                !string.Equals(receipt.UtteranceId, source.UtteranceId, StringComparison.Ordinal) ||
                !string.Equals(receipt.ConversationId, source.ConversationId, StringComparison.Ordinal) ||
                !string.Equals(receipt.SpeakerId, source.SpeakerId, StringComparison.Ordinal) ||
                !string.Equals(receipt.RecipientId, source.RecipientId, StringComparison.Ordinal) ||
                !string.Equals(receipt.ExactTextHash, source.ExactTextHash, StringComparison.Ordinal))
                throw new ArgumentException("Receipt identity does not match the source and attempt.", nameof(receipt));
            if (!string.Equals(receipt.SourceContract, ObservedDisplayReceipt.SourceContractValue, StringComparison.Ordinal) ||
                !string.Equals(receipt.SourceGateDigest, ObservedDisplayReceipt.SourceGateDigestValue, StringComparison.Ordinal))
                throw new ArgumentException("Receipt source contract or gate digest is untrusted.", nameof(receipt));
            if (receipt.CheckpointGeneration != CheckpointGeneration ||
                attempt.CheckpointGeneration != CheckpointGeneration)
                throw new ArgumentException("Receipt checkpoint is stale or foreign.", nameof(receipt));
            if (receipt.DisplayedTick < attempt.RequestedTick ||
                receipt.DisplayedTick > callerTick ||
                receipt.DisplayedTick > ticket.ExpiryTick)
                throw new ArgumentException("Receipt chronology is stale, future, reversed, or expired.", nameof(receipt));
            if (receipt.AudienceIds.Any(
                value => !source.IntendedAudienceIds.Contains(value, StringComparer.Ordinal)))
                throw new ArgumentException("Actual witnesses must be drawn from the intended private audience.", nameof(receipt));
            if (!receipt.AudienceIds.Contains(PerspectiveOwnerId, StringComparer.Ordinal))
                throw new ArgumentException("The owner was not an actual witness.", nameof(receipt));
        }

        private PresentationTicket RequireLive(string ticketId)
        {
            var normalized = BoundedReactionCanonical.LowerHex(ticketId, nameof(ticketId));
            if (!_live.TryGetValue(normalized, out var ticket))
                throw new KeyNotFoundException("No live presentation ticket exists.");
            return ticket;
        }

        private void Terminalize(
            PresentationTicket ticket,
            PresentationTicketStatus status,
            long terminalTick,
            string reason)
        {
            _live.Remove(ticket.TicketId);
            _activeAttempts.Remove(ticket.TicketId);
            var digest = reason.Length == 64 && reason.All(
                value => (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f'))
                ? reason
                : BoundedReactionCanonical.HashFields(new[]
                {
                    BoundedReactionCanonical.Pair("ticket_id", ticket.TicketId),
                    BoundedReactionCanonical.Pair("status", status.ToString()),
                    BoundedReactionCanonical.Pair("terminal_tick", terminalTick.ToString(CultureInfo.InvariantCulture)),
                    BoundedReactionCanonical.Pair("reason", reason)
                });
            _tombstones[ticket.TicketId] = new LifecycleTombstone(ticket.TicketId, status, terminalTick, digest);
            while (_tombstones.Count > MaximumTerminalRecords)
            {
                var oldest = _tombstones.Values
                    .OrderBy(value => value.TerminalTick)
                    .ThenBy(value => value.TicketId, StringComparer.Ordinal)
                    .First();
                _tombstones.Remove(oldest.TicketId);
                var projection = _projections.Values.FirstOrDefault(
                    value => string.Equals(value.TicketId, oldest.TicketId, StringComparison.Ordinal));
                if (projection is not null)
                {
                    _projections.Remove(projection.ReceiptId);
                    _receiptFingerprints.Remove(projection.ReceiptId);
                }
            }
        }

        private void TrimReceiptFingerprints()
        {
            while (_receiptFingerprints.Count > MaximumTerminalRecords * 2)
            {
                var removable = _receiptFingerprints.Keys
                    .FirstOrDefault(value => !_projections.ContainsKey(value));
                if (removable is null) break;
                _receiptFingerprints.Remove(removable);
            }
        }
    }

    public sealed class BoundedReactionLifecycleSnapshot
    {
        public const int SchemaVersion = 1;

        private BoundedReactionLifecycleSnapshot(
            string storeId,
            string perspectiveOwnerId,
            string sessionId,
            long checkpointGeneration,
            IEnumerable<PresentationTicket> liveTickets,
            IEnumerable<ReleaseAttempt> activeAttempts,
            IEnumerable<LifecycleTombstone> tombstones,
            IEnumerable<SuccessfulHistoryProjection> projections,
            IEnumerable<KeyValuePair<string, string>> receiptFingerprints)
        {
            StoreId = BoundedReactionCanonical.Text(storeId, nameof(storeId), 256);
            PerspectiveOwnerId = BoundedReactionCanonical.Text(
                perspectiveOwnerId,
                nameof(perspectiveOwnerId),
                256);
            SessionId = BoundedReactionCanonical.Text(sessionId, nameof(sessionId), 256);
            if (checkpointGeneration < 0) throw new ArgumentOutOfRangeException(nameof(checkpointGeneration));
            CheckpointGeneration = checkpointGeneration;
            LiveTickets = new ReadOnlyCollection<PresentationTicket>(
                liveTickets.OrderBy(value => value.TicketId, StringComparer.Ordinal).ToArray());
            ActiveAttempts = new ReadOnlyCollection<ReleaseAttempt>(
                activeAttempts.OrderBy(value => value.AttemptId, StringComparer.Ordinal).ToArray());
            Tombstones = new ReadOnlyCollection<LifecycleTombstone>(
                tombstones.OrderBy(value => value.TicketId, StringComparer.Ordinal).ToArray());
            Projections = new ReadOnlyCollection<SuccessfulHistoryProjection>(
                projections.OrderBy(value => value.ReceiptId, StringComparer.Ordinal).ToArray());
            ReceiptFingerprints = new ReadOnlyDictionary<string, string>(
                new SortedDictionary<string, string>(
                    receiptFingerprints.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal),
                    StringComparer.Ordinal));
            if (LiveTickets.Count > BoundedReactionLifecycle.MaximumLiveTickets ||
                Tombstones.Count > BoundedReactionLifecycle.MaximumTerminalRecords ||
                Projections.Count > BoundedReactionLifecycle.MaximumTerminalRecords)
                throw new ArgumentOutOfRangeException(nameof(liveTickets));
            Encoding = EncodeWithoutHash();
            SnapshotHash = BoundedReactionCanonical.HashFields(new[]
            {
                BoundedReactionCanonical.Pair("schema", SchemaVersion.ToString(CultureInfo.InvariantCulture)),
                BoundedReactionCanonical.Pair("encoding", Encoding)
            });
            Serialized = Encoding + "\nH|" + SnapshotHash;
        }

        internal static BoundedReactionLifecycleSnapshot Create(
            string storeId,
            string perspectiveOwnerId,
            string sessionId,
            long checkpointGeneration,
            IEnumerable<PresentationTicket> liveTickets,
            IEnumerable<ReleaseAttempt> activeAttempts,
            IEnumerable<LifecycleTombstone> tombstones,
            IEnumerable<SuccessfulHistoryProjection> projections,
            IEnumerable<KeyValuePair<string, string>> receiptFingerprints) =>
            new BoundedReactionLifecycleSnapshot(
                storeId,
                perspectiveOwnerId,
                sessionId,
                checkpointGeneration,
                liveTickets,
                activeAttempts,
                tombstones,
                projections,
                receiptFingerprints);

        public int Version => SchemaVersion;
        public string StoreId { get; }
        public string PerspectiveOwnerId { get; }
        public string SessionId { get; }
        public long CheckpointGeneration { get; }
        public IReadOnlyList<PresentationTicket> LiveTickets { get; }
        public IReadOnlyList<ReleaseAttempt> ActiveAttempts { get; }
        internal IReadOnlyList<LifecycleTombstone> Tombstones { get; }
        public IReadOnlyList<SuccessfulHistoryProjection> Projections { get; }
        internal IReadOnlyDictionary<string, string> ReceiptFingerprints { get; }
        public string Encoding { get; }
        public string SnapshotHash { get; }
        public string Serialized { get; }

        public static BoundedReactionLifecycleSnapshot Decode(string serialized)
        {
            if (string.IsNullOrWhiteSpace(serialized))
                throw new ArgumentException("Snapshot bytes are empty.", nameof(serialized));
            var normalized = serialized.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = normalized.Split('\n');
            if (lines.Length < 2 || lines.Any(value => value.Length == 0))
                throw new ArgumentException("Snapshot is truncated.", nameof(serialized));
            var hashFields = lines[lines.Length - 1].Split('|');
            if (hashFields.Length != 2 || hashFields[0] != "H")
                throw new ArgumentException("Snapshot hash trailer is missing.", nameof(serialized));
            var expectedHash = BoundedReactionCanonical.LowerHex(hashFields[1], nameof(serialized));
            var encoding = string.Join("\n", lines.Take(lines.Length - 1));
            var actualHash = BoundedReactionCanonical.HashFields(new[]
            {
                BoundedReactionCanonical.Pair("schema", SchemaVersion.ToString(CultureInfo.InvariantCulture)),
                BoundedReactionCanonical.Pair("encoding", encoding)
            });
            if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
                throw new ArgumentException("Snapshot integrity check failed.", nameof(serialized));

            var header = lines[0].Split('|');
            if (header.Length != 6 || header[0] != "MOSAIC-BRL" || header[1] != "1")
                throw new ArgumentException("Snapshot schema is unsupported.", nameof(serialized));
            var storeId = BoundedReactionCanonical.FromBase64(header[2]);
            var ownerId = BoundedReactionCanonical.FromBase64(header[3]);
            var sessionId = BoundedReactionCanonical.FromBase64(header[4]);
            var generation = BoundedReactionCanonical.NonnegativeLong(header[5], nameof(serialized));
            var tickets = new List<PresentationTicket>();
            var attempts = new List<ReleaseAttempt>();
            var tombstones = new List<LifecycleTombstone>();
            var projections = new List<SuccessfulHistoryProjection>();
            var receipts = new List<KeyValuePair<string, string>>();

            foreach (var line in lines.Skip(1).Take(lines.Length - 2))
            {
                var fields = line.Split('|');
                switch (fields[0])
                {
                    case "T":
                        if (fields.Length != 8) throw new ArgumentException("Invalid ticket record.", nameof(serialized));
                        var source = BoundedReactionSource.Import(BoundedReactionCanonical.FromBase64(fields[7]));
                        if (!Enum.TryParse(fields[5], false, out PresentationTicketStatus ticketStatus))
                            throw new ArgumentException("Invalid ticket status.", nameof(serialized));
                        tickets.Add(new PresentationTicket(
                            source,
                            BoundedReactionCanonical.NonnegativeLong(fields[3], nameof(serialized)),
                            BoundedReactionCanonical.NonnegativeInt(fields[4], nameof(serialized)),
                            ticketStatus,
                            BoundedReactionCanonical.NonnegativeLong(fields[6], nameof(serialized)),
                            fields[1]));
                        if (tickets[tickets.Count - 1].CreatedTick !=
                            BoundedReactionCanonical.NonnegativeLong(fields[2], nameof(serialized)))
                            throw new ArgumentException("Ticket creation tick disagrees with its source.", nameof(serialized));
                        break;
                    case "A":
                        if (fields.Length != 6) throw new ArgumentException("Invalid attempt record.", nameof(serialized));
                        attempts.Add(new ReleaseAttempt(
                            fields[2],
                            BoundedReactionCanonical.NonnegativeInt(fields[3], nameof(serialized)),
                            BoundedReactionCanonical.NonnegativeLong(fields[4], nameof(serialized)),
                            BoundedReactionCanonical.NonnegativeLong(fields[5], nameof(serialized)),
                            fields[1]));
                        break;
                    case "X":
                        if (fields.Length != 5 ||
                            !Enum.TryParse(fields[2], false, out PresentationTicketStatus terminalStatus))
                            throw new ArgumentException("Invalid tombstone record.", nameof(serialized));
                        tombstones.Add(new LifecycleTombstone(
                            fields[1],
                            terminalStatus,
                            BoundedReactionCanonical.NonnegativeLong(fields[3], nameof(serialized)),
                            fields[4]));
                        break;
                    case "P":
                        if (fields.Length != 7) throw new ArgumentException("Invalid projection record.", nameof(serialized));
                        projections.Add(new SuccessfulHistoryProjection(
                            fields[1],
                            fields[2],
                            fields[3],
                            BoundedReactionCanonical.FromBase64(fields[4]),
                            BoundedReactionCanonical.SplitBase64(fields[5]),
                            BoundedReactionCanonical.NonnegativeLong(fields[6], nameof(serialized))));
                        break;
                    case "R":
                        if (fields.Length != 3) throw new ArgumentException("Invalid receipt digest record.", nameof(serialized));
                        receipts.Add(new KeyValuePair<string, string>(
                            BoundedReactionCanonical.LowerHex(fields[1], nameof(serialized)),
                            BoundedReactionCanonical.LowerHex(fields[2], nameof(serialized))));
                        break;
                    default:
                        throw new ArgumentException("Snapshot contains an unknown record.", nameof(serialized));
                }
            }
            var snapshot = Create(storeId, ownerId, sessionId, generation, tickets, attempts, tombstones, projections, receipts);
            if (!string.Equals(snapshot.Serialized, normalized, StringComparison.Ordinal))
                throw new ArgumentException("Snapshot is not in canonical order.", nameof(serialized));
            return snapshot;
        }

        internal void Validate()
        {
            var decoded = Decode(Serialized);
            if (!string.Equals(decoded.SnapshotHash, SnapshotHash, StringComparison.Ordinal))
                throw new ArgumentException("Snapshot validation failed.");
        }

        private string EncodeWithoutHash()
        {
            var lines = new List<string>
            {
                string.Join("|", new[]
                {
                    "MOSAIC-BRL",
                    SchemaVersion.ToString(CultureInfo.InvariantCulture),
                    BoundedReactionCanonical.Base64(StoreId),
                    BoundedReactionCanonical.Base64(PerspectiveOwnerId),
                    BoundedReactionCanonical.Base64(SessionId),
                    CheckpointGeneration.ToString(CultureInfo.InvariantCulture)
                })
            };
            lines.AddRange(LiveTickets.Select(ticket => string.Join("|", new[]
            {
                "T",
                ticket.TicketId,
                ticket.CreatedTick.ToString(CultureInfo.InvariantCulture),
                ticket.ExpiryTick.ToString(CultureInfo.InvariantCulture),
                ticket.RetryCount.ToString(CultureInfo.InvariantCulture),
                ticket.Status.ToString(),
                ticket.NextAttemptNotBeforeTick.ToString(CultureInfo.InvariantCulture),
                BoundedReactionCanonical.Base64(ticket.Source.Export())
            })));
            lines.AddRange(ActiveAttempts.Select(attempt => string.Join("|", new[]
            {
                "A",
                attempt.AttemptId,
                attempt.TicketId,
                attempt.AttemptOrdinal.ToString(CultureInfo.InvariantCulture),
                attempt.RequestedTick.ToString(CultureInfo.InvariantCulture),
                attempt.CheckpointGeneration.ToString(CultureInfo.InvariantCulture)
            })));
            lines.AddRange(Tombstones.Select(tombstone => string.Join("|", new[]
            {
                "X",
                tombstone.TicketId,
                tombstone.Status.ToString(),
                tombstone.TerminalTick.ToString(CultureInfo.InvariantCulture),
                tombstone.TerminalDigest
            })));
            lines.AddRange(Projections.Select(projection => string.Join("|", new[]
            {
                "P",
                projection.TicketId,
                projection.ReceiptId,
                projection.ReceiptFingerprint,
                BoundedReactionCanonical.Base64(projection.PerspectiveOwnerId),
                BoundedReactionCanonical.JoinBase64(projection.ActualWitnessIds),
                projection.DisplayedTick.ToString(CultureInfo.InvariantCulture)
            })));
            lines.AddRange(ReceiptFingerprints.Select(pair => string.Join("|", new[]
            {
                "R",
                pair.Key,
                pair.Value
            })));
            return string.Join("\n", lines);
        }
    }
}
