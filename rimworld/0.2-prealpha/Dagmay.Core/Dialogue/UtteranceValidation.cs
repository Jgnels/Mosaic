using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Dialogue
{
    public sealed class UtteranceProposal
    {
        public UtteranceProposal(
            UtteranceId id,
            DialogueRequestId requestId,
            ConversationId conversationId,
            IndividualId speakerId,
            IndividualId? recipientId,
            string text,
            IEnumerable<EventId> evidenceIds,
            long generatedAtTick,
            string? actionDirective = null)
        {
            if (generatedAtTick < 0) throw new ArgumentOutOfRangeException(nameof(generatedAtTick));
            if (id.Value == Guid.Empty) throw new ArgumentException("Utterance ID cannot be empty.", nameof(id));
            if (requestId.Value == Guid.Empty) throw new ArgumentException("Dialogue request ID cannot be empty.", nameof(requestId));
            if (conversationId.Value == Guid.Empty) throw new ArgumentException("Conversation ID cannot be empty.", nameof(conversationId));
            if (speakerId.Value == Guid.Empty) throw new ArgumentException("Speaker ID cannot be empty.", nameof(speakerId));
            if (recipientId.HasValue && recipientId.Value.Value == Guid.Empty)
                throw new ArgumentException("Recipient ID cannot be empty.", nameof(recipientId));

            Id = id;
            RequestId = requestId;
            ConversationId = conversationId;
            SpeakerId = speakerId;
            RecipientId = recipientId;
            Text = ContractGuard.Text(text, nameof(text), 4000);
            EvidenceIds = ContractGuard.List(evidenceIds, nameof(evidenceIds));
            GeneratedAtTick = generatedAtTick;
            ActionDirective = ContractGuard.OptionalText(actionDirective, nameof(actionDirective), 1000);

            if (EvidenceIds.Count == 0 || EvidenceIds.Count > 32)
                throw new ArgumentOutOfRangeException(nameof(evidenceIds));
            if (EvidenceIds.Any(evidenceId => evidenceId.Value == Guid.Empty))
                throw new ArgumentException("Utterance evidence IDs cannot be empty.", nameof(evidenceIds));
            if (EvidenceIds.Distinct().Count() != EvidenceIds.Count)
                throw new ArgumentException("Utterance evidence IDs must be unique.", nameof(evidenceIds));
        }

        public UtteranceId Id { get; }
        public DialogueRequestId RequestId { get; }
        public ConversationId ConversationId { get; }
        public IndividualId SpeakerId { get; }
        public IndividualId? RecipientId { get; }
        public string Text { get; }
        public IReadOnlyList<EventId> EvidenceIds { get; }
        public long GeneratedAtTick { get; }
        public string? ActionDirective { get; }
    }

    public enum UtteranceValidationDisposition
    {
        Accepted = 0,
        DialogueDisabled = 1,
        DuplicateUtterance = 2,
        RequestMismatch = 3,
        ConversationMismatch = 4,
        RequestExpired = 5,
        ForeignSpeaker = 6,
        RecipientMismatch = 7,
        UngroundedEvidence = 8,
        TextTooLong = 9,
        UnsafeControlCharacters = 10,
        ActionDirectiveRejected = 11
    }

    public sealed class ValidatedUtterance
    {
        internal ValidatedUtterance(UtteranceProposal proposal)
        {
            Proposal = proposal ?? throw new ArgumentNullException(nameof(proposal));
        }

        public UtteranceProposal Proposal { get; }
    }

    public sealed class UtteranceValidationResult
    {
        private UtteranceValidationResult(
            UtteranceValidationDisposition disposition,
            ValidatedUtterance? utterance)
        {
            Disposition = disposition;
            Utterance = utterance;
        }

        public UtteranceValidationDisposition Disposition { get; }
        public ValidatedUtterance? Utterance { get; }
        public bool IsAccepted => Disposition == UtteranceValidationDisposition.Accepted;

        internal static UtteranceValidationResult Accepted(UtteranceProposal proposal)
        {
            return new UtteranceValidationResult(
                UtteranceValidationDisposition.Accepted,
                new ValidatedUtterance(proposal));
        }

        internal static UtteranceValidationResult Rejected(UtteranceValidationDisposition disposition)
        {
            if (disposition == UtteranceValidationDisposition.Accepted)
                throw new ArgumentException("Accepted requires an utterance.", nameof(disposition));
            return new UtteranceValidationResult(disposition, null);
        }
    }

    public sealed class UtteranceValidationPolicy
    {
        public UtteranceValidationPolicy(bool enabled, int maximumTextLength)
        {
            if (maximumTextLength < 1 || maximumTextLength > 4000)
                throw new ArgumentOutOfRangeException(nameof(maximumTextLength));
            Enabled = enabled;
            MaximumTextLength = maximumTextLength;
        }

        public bool Enabled { get; }
        public int MaximumTextLength { get; }
    }

    public sealed class UtteranceValidator
    {
        public UtteranceValidationResult Validate(
            DialogueRequest request,
            UtteranceProposal proposal,
            long currentTick,
            ISet<UtteranceId> admittedUtteranceIds,
            UtteranceValidationPolicy policy)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (proposal is null) throw new ArgumentNullException(nameof(proposal));
            if (admittedUtteranceIds is null) throw new ArgumentNullException(nameof(admittedUtteranceIds));
            if (policy is null) throw new ArgumentNullException(nameof(policy));
            if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));

            if (!policy.Enabled)
                return UtteranceValidationResult.Rejected(UtteranceValidationDisposition.DialogueDisabled);
            if (admittedUtteranceIds.Contains(proposal.Id))
                return UtteranceValidationResult.Rejected(UtteranceValidationDisposition.DuplicateUtterance);
            if (proposal.RequestId != request.Id)
                return UtteranceValidationResult.Rejected(UtteranceValidationDisposition.RequestMismatch);
            if (proposal.ConversationId != request.ConversationId)
                return UtteranceValidationResult.Rejected(UtteranceValidationDisposition.ConversationMismatch);
            if (request.IsExpired(currentTick))
                return UtteranceValidationResult.Rejected(UtteranceValidationDisposition.RequestExpired);
            if (proposal.SpeakerId != request.ExpectedSpeakerId)
                return UtteranceValidationResult.Rejected(UtteranceValidationDisposition.ForeignSpeaker);
            if (proposal.RecipientId != request.RecipientId)
                return UtteranceValidationResult.Rejected(UtteranceValidationDisposition.RecipientMismatch);
            if (proposal.Text.Length > policy.MaximumTextLength)
                return UtteranceValidationResult.Rejected(UtteranceValidationDisposition.TextTooLong);
            if (proposal.Text.Any(character => char.IsControl(character) && character != '\n' && character != '\r' && character != '\t'))
                return UtteranceValidationResult.Rejected(UtteranceValidationDisposition.UnsafeControlCharacters);
            if (!string.IsNullOrWhiteSpace(proposal.ActionDirective))
                return UtteranceValidationResult.Rejected(UtteranceValidationDisposition.ActionDirectiveRejected);

            var grounded = new HashSet<EventId>(request.SourceEventIds);
            if (proposal.EvidenceIds.Any(evidenceId => !grounded.Contains(evidenceId)))
                return UtteranceValidationResult.Rejected(UtteranceValidationDisposition.UngroundedEvidence);

            return UtteranceValidationResult.Accepted(proposal);
        }
    }
}
