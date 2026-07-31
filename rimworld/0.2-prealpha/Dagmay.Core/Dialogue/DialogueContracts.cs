using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Dialogue
{
    public enum DialogueTriggerKind
    {
        Player = 0,
        Urgent = 1,
        Reply = 2,
        Social = 3,
        Event = 4,
        Monologue = 5
    }

    public enum DialoguePriority
    {
        Background = 0,
        Normal = 100,
        Important = 200,
        Urgent = 300,
        Player = 400
    }

    public enum DialogueContextAudience
    {
        Public = 0,
        RelationshipSensitive = 1,
        PrivateSelf = 2,
        ObserverOnly = 3
    }

    public enum DialoguePromptRole
    {
        System = 0,
        User = 1,
        Assistant = 2
    }

    public sealed class DialogueParticipantSnapshot
    {
        public DialogueParticipantSnapshot(
            IndividualId individualId,
            string displayName,
            string visibleRole,
            bool canSpeak,
            bool isPresent)
        {
            if (individualId.Value == Guid.Empty)
                throw new ArgumentException("Participant IndividualId cannot be empty.", nameof(individualId));

            IndividualId = individualId;
            DisplayName = ContractGuard.Text(displayName, nameof(displayName), 128);
            VisibleRole = ContractGuard.Text(visibleRole, nameof(visibleRole), 128);
            CanSpeak = canSpeak;
            IsPresent = isPresent;
        }

        public IndividualId IndividualId { get; }
        public string DisplayName { get; }
        public string VisibleRole { get; }
        public bool CanSpeak { get; }
        public bool IsPresent { get; }
    }

    /// <summary>
    /// Plain-data context captured on the environment adapter's main thread.
    /// No Pawn, Map, Thing, Def, Verse, Unity, or mod object may appear here.
    /// </summary>
    public sealed class DialogueSceneSnapshot
    {
        public DialogueSceneSnapshot(
            EventId triggerEventId,
            long observedAtTick,
            string environmentSummary,
            IEnumerable<DialogueParticipantSnapshot> participants)
        {
            if (observedAtTick < 0) throw new ArgumentOutOfRangeException(nameof(observedAtTick));
            if (triggerEventId.Value == Guid.Empty)
                throw new ArgumentException("Trigger EventId cannot be empty.", nameof(triggerEventId));

            TriggerEventId = triggerEventId;
            ObservedAtTick = observedAtTick;
            EnvironmentSummary = ContractGuard.Text(environmentSummary, nameof(environmentSummary), 2048);
            Participants = ContractGuard.List(participants, nameof(participants));

            if (Participants.Count == 0 || Participants.Count > 8)
                throw new ArgumentOutOfRangeException(nameof(participants), "A scene requires between 1 and 8 participants.");

            var seen = new HashSet<IndividualId>();
            for (var index = 0; index < Participants.Count; index++)
            {
                var participant = Participants[index]
                    ?? throw new ArgumentException("Scene participants cannot contain null values.", nameof(participants));

                if (!seen.Add(participant.IndividualId))
                    throw new ArgumentException("Scene participants must be unique by IndividualId.", nameof(participants));
            }
        }

        public EventId TriggerEventId { get; }
        public long ObservedAtTick { get; }
        public string EnvironmentSummary { get; }
        public IReadOnlyList<DialogueParticipantSnapshot> Participants { get; }

        public bool Contains(IndividualId individualId)
        {
            return Participants.Any(participant => participant.IndividualId == individualId);
        }
    }

    public sealed class DialogueContextItem
    {
        public DialogueContextItem(
            string kind,
            string text,
            IEnumerable<EventId> evidenceIds,
            DialogueContextAudience audience,
            double relevance,
            IndividualId? ownerId = null,
            IEnumerable<IndividualId>? allowedRecipientIds = null)
        {
            if (!Enum.IsDefined(typeof(DialogueContextAudience), audience))
                throw new ArgumentOutOfRangeException(nameof(audience));
            if (ownerId.HasValue && ownerId.Value.Value == Guid.Empty)
                throw new ArgumentException("Context owner ID cannot be empty.", nameof(ownerId));

            Kind = ContractGuard.Text(kind, nameof(kind), 128);
            Text = ContractGuard.Text(text, nameof(text), 2048);
            EvidenceIds = ContractGuard.List(evidenceIds, nameof(evidenceIds));
            Audience = audience;
            Relevance = ContractGuard.UnitInterval(relevance, nameof(relevance));
            OwnerId = ownerId;
            AllowedRecipientIds = ContractGuard.List(
                allowedRecipientIds ?? Array.Empty<IndividualId>(),
                nameof(allowedRecipientIds));

            if (EvidenceIds.Count == 0 || EvidenceIds.Count > 32)
                throw new ArgumentOutOfRangeException(
                    nameof(evidenceIds),
                    "Dialogue context requires between 1 and 32 supporting EventIds.");

            if (EvidenceIds.Any(evidenceId => evidenceId.Value == Guid.Empty))
                throw new ArgumentException("Dialogue context evidence IDs cannot be empty.", nameof(evidenceIds));

            if (EvidenceIds.Distinct().Count() != EvidenceIds.Count)
                throw new ArgumentException("Dialogue context evidence IDs must be unique.", nameof(evidenceIds));

            if (AllowedRecipientIds.Count > 16)
                throw new ArgumentOutOfRangeException(nameof(allowedRecipientIds));

            if (AllowedRecipientIds.Any(recipientId => recipientId.Value == Guid.Empty))
                throw new ArgumentException("Allowed recipient IDs cannot be empty.", nameof(allowedRecipientIds));

            if (AllowedRecipientIds.Distinct().Count() != AllowedRecipientIds.Count)
                throw new ArgumentException("Allowed recipient IDs must be unique.", nameof(allowedRecipientIds));

            if ((audience == DialogueContextAudience.PrivateSelf ||
                 audience == DialogueContextAudience.RelationshipSensitive) &&
                ownerId is null)
            {
                throw new ArgumentException("Non-public context requires an owner.", nameof(ownerId));
            }

            if (audience == DialogueContextAudience.RelationshipSensitive &&
                AllowedRecipientIds.Count == 0)
            {
                throw new ArgumentException(
                    "Relationship-sensitive context requires at least one allowed recipient.",
                    nameof(allowedRecipientIds));
            }
        }

        public string Kind { get; }
        public string Text { get; }
        public IReadOnlyList<EventId> EvidenceIds { get; }
        public DialogueContextAudience Audience { get; }
        public double Relevance { get; }
        public IndividualId? OwnerId { get; }
        public IReadOnlyList<IndividualId> AllowedRecipientIds { get; }
    }

    public sealed class DialogueRequest
    {
        public DialogueRequest(
            DialogueRequestId id,
            ConversationId conversationId,
            DialogueTriggerKind triggerKind,
            DialoguePriority priority,
            IndividualId initiatorId,
            IndividualId expectedSpeakerId,
            IndividualId? recipientId,
            DialogueSceneSnapshot scene,
            IEnumerable<EventId> sourceEventIds,
            long createdAtTick,
            long expiresAtTick,
            string coalescingKey)
        {
            if (createdAtTick < 0) throw new ArgumentOutOfRangeException(nameof(createdAtTick));
            if (expiresAtTick <= createdAtTick)
                throw new ArgumentOutOfRangeException(nameof(expiresAtTick), "Expiration must follow creation.");
            if (id.Value == Guid.Empty)
                throw new ArgumentException("Dialogue request ID cannot be empty.", nameof(id));
            if (conversationId.Value == Guid.Empty)
                throw new ArgumentException("Conversation ID cannot be empty.", nameof(conversationId));
            if (initiatorId.Value == Guid.Empty)
                throw new ArgumentException("Initiator ID cannot be empty.", nameof(initiatorId));
            if (expectedSpeakerId.Value == Guid.Empty)
                throw new ArgumentException("Expected speaker ID cannot be empty.", nameof(expectedSpeakerId));
            if (recipientId.HasValue && recipientId.Value.Value == Guid.Empty)
                throw new ArgumentException("Recipient ID cannot be empty.", nameof(recipientId));
            if (!Enum.IsDefined(typeof(DialogueTriggerKind), triggerKind))
                throw new ArgumentOutOfRangeException(nameof(triggerKind));
            if (!Enum.IsDefined(typeof(DialoguePriority), priority))
                throw new ArgumentOutOfRangeException(nameof(priority));

            Id = id;
            ConversationId = conversationId;
            TriggerKind = triggerKind;
            Priority = priority;
            InitiatorId = initiatorId;
            ExpectedSpeakerId = expectedSpeakerId;
            RecipientId = recipientId;
            Scene = scene ?? throw new ArgumentNullException(nameof(scene));
            SourceEventIds = ContractGuard.List(sourceEventIds, nameof(sourceEventIds));
            CreatedAtTick = createdAtTick;
            ExpiresAtTick = expiresAtTick;
            CoalescingKey = ContractGuard.Text(coalescingKey, nameof(coalescingKey), 256);

            if (SourceEventIds.Count == 0 || SourceEventIds.Count > 32)
                throw new ArgumentOutOfRangeException(
                    nameof(sourceEventIds),
                    "Dialogue requests require between 1 and 32 source EventIds.");

            if (SourceEventIds.Any(eventId => eventId.Value == Guid.Empty))
                throw new ArgumentException("Dialogue request source EventIds cannot be empty.", nameof(sourceEventIds));

            if (SourceEventIds.Distinct().Count() != SourceEventIds.Count)
                throw new ArgumentException("Dialogue request source EventIds must be unique.", nameof(sourceEventIds));

            if (!Scene.Contains(initiatorId))
                throw new ArgumentException("The initiator must be present in the scene.", nameof(initiatorId));

            if (!Scene.Contains(expectedSpeakerId))
                throw new ArgumentException("The expected speaker must be present in the scene.", nameof(expectedSpeakerId));

            if (recipientId.HasValue && !Scene.Contains(recipientId.Value))
                throw new ArgumentException("The recipient must be present in the scene.", nameof(recipientId));

            if (!SourceEventIds.Contains(Scene.TriggerEventId))
                throw new ArgumentException("The scene trigger EventId must ground the request.", nameof(sourceEventIds));
        }

        public DialogueRequestId Id { get; }
        public ConversationId ConversationId { get; }
        public DialogueTriggerKind TriggerKind { get; }
        public DialoguePriority Priority { get; }
        public IndividualId InitiatorId { get; }
        public IndividualId ExpectedSpeakerId { get; }
        public IndividualId? RecipientId { get; }
        public DialogueSceneSnapshot Scene { get; }
        public IReadOnlyList<EventId> SourceEventIds { get; }
        public long CreatedAtTick { get; }
        public long ExpiresAtTick { get; }
        public string CoalescingKey { get; }

        public bool IsExpired(long currentTick)
        {
            if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));
            return currentTick >= ExpiresAtTick;
        }

        internal DialogueRequest MergeEvidence(DialogueRequest newer)
        {
            if (newer is null) throw new ArgumentNullException(nameof(newer));
            if (InitiatorId != newer.InitiatorId ||
                ExpectedSpeakerId != newer.ExpectedSpeakerId ||
                RecipientId != newer.RecipientId ||
                TriggerKind != newer.TriggerKind ||
                !string.Equals(CoalescingKey, newer.CoalescingKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Only equivalent owner-scoped dialogue requests may coalesce.");
            }

            var mergedEvidence = SourceEventIds
                .Concat(newer.SourceEventIds)
                .Distinct()
                .OrderBy(eventId => eventId.ToString(), StringComparer.Ordinal)
                .Take(32)
                .ToArray();

            var winner = newer.Priority > Priority ? newer : this;
            return new DialogueRequest(
                Id,
                ConversationId,
                TriggerKind,
                winner.Priority,
                InitiatorId,
                ExpectedSpeakerId,
                RecipientId,
                winner.Scene,
                mergedEvidence,
                Math.Min(CreatedAtTick, newer.CreatedAtTick),
                Math.Max(ExpiresAtTick, newer.ExpiresAtTick),
                CoalescingKey);
        }
    }

    public sealed class DialogueContextPacket
    {
        public DialogueContextPacket(
            IndividualId speakerId,
            IndividualId? recipientId,
            long builtAtTick,
            IEnumerable<DialogueContextItem> items,
            int omittedItemCount,
            int characterCount)
        {
            if (builtAtTick < 0) throw new ArgumentOutOfRangeException(nameof(builtAtTick));
            if (omittedItemCount < 0) throw new ArgumentOutOfRangeException(nameof(omittedItemCount));
            if (characterCount < 0) throw new ArgumentOutOfRangeException(nameof(characterCount));
            if (speakerId.Value == Guid.Empty)
                throw new ArgumentException("Speaker ID cannot be empty.", nameof(speakerId));
            if (recipientId.HasValue && recipientId.Value.Value == Guid.Empty)
                throw new ArgumentException("Recipient ID cannot be empty.", nameof(recipientId));

            SpeakerId = speakerId;
            RecipientId = recipientId;
            BuiltAtTick = builtAtTick;
            Items = ContractGuard.List(items, nameof(items));
            OmittedItemCount = omittedItemCount;
            CharacterCount = characterCount;

            if (Items.Count > 64) throw new ArgumentOutOfRangeException(nameof(items));
            if (Items.Any(item => item is null))
                throw new ArgumentException("Dialogue context packets cannot contain null items.", nameof(items));
        }

        public IndividualId SpeakerId { get; }
        public IndividualId? RecipientId { get; }
        public long BuiltAtTick { get; }
        public IReadOnlyList<DialogueContextItem> Items { get; }
        public int OmittedItemCount { get; }
        public int CharacterCount { get; }
    }

    public sealed class DialoguePromptSegment
    {
        public DialoguePromptSegment(DialoguePromptRole role, string content, string purpose)
        {
            if (!Enum.IsDefined(typeof(DialoguePromptRole), role))
                throw new ArgumentOutOfRangeException(nameof(role));

            Role = role;
            Content = ContractGuard.Text(content, nameof(content), 16000);
            Purpose = ContractGuard.Text(purpose, nameof(purpose), 128);
        }

        public DialoguePromptRole Role { get; }
        public string Content { get; }
        public string Purpose { get; }
    }

    public sealed class DialoguePromptPlan
    {
        public DialoguePromptPlan(
            DialogueRequestId requestId,
            ConversationId conversationId,
            IEnumerable<DialoguePromptSegment> segments)
        {
            if (requestId.Value == Guid.Empty)
                throw new ArgumentException("Dialogue request ID cannot be empty.", nameof(requestId));
            if (conversationId.Value == Guid.Empty)
                throw new ArgumentException("Conversation ID cannot be empty.", nameof(conversationId));

            RequestId = requestId;
            ConversationId = conversationId;
            Segments = ContractGuard.List(segments, nameof(segments));

            if (Segments.Count == 0 || Segments.Count > 16)
                throw new ArgumentOutOfRangeException(nameof(segments));

            if (Segments.Any(segment => segment is null))
                throw new ArgumentException("Prompt plans cannot contain null segments.", nameof(segments));
        }

        public DialogueRequestId RequestId { get; }
        public ConversationId ConversationId { get; }
        public IReadOnlyList<DialoguePromptSegment> Segments { get; }
    }
}
