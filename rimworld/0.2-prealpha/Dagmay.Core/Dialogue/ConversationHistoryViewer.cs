using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Dialogue
{
    public sealed class ConversationHistoryParticipant
    {
        public ConversationHistoryParticipant(
            IndividualId individualId,
            string displayLabel)
        {
            if (individualId.Value == Guid.Empty)
                throw new ArgumentException("Participant ID cannot be empty.", nameof(individualId));
            if (string.IsNullOrWhiteSpace(displayLabel) || displayLabel.Length > 256)
                throw new ArgumentException("Participant label must be bounded.", nameof(displayLabel));

            IndividualId = individualId;
            DisplayLabel = displayLabel;
        }

        public IndividualId IndividualId { get; }
        public string DisplayLabel { get; }
    }

    public sealed class ConversationHistoryViewerRow
    {
        public ConversationHistoryViewerRow(
            EventId eventId,
            ConversationId conversationId,
            IndividualId speakerId,
            string speakerLabel,
            IndividualId recipientId,
            string recipientLabel,
            string text,
            long displayedAtTick,
            DateTimeOffset displayedAtUtc,
            DialoguePresentationChannel channel,
            int supportingEvidenceCount)
        {
            if (eventId.Value == Guid.Empty)
                throw new ArgumentException("Dialogue event ID cannot be empty.", nameof(eventId));
            if (conversationId.Value == Guid.Empty)
                throw new ArgumentException("Conversation ID cannot be empty.", nameof(conversationId));
            if (speakerId.Value == Guid.Empty || recipientId.Value == Guid.Empty || speakerId == recipientId)
                throw new ArgumentException("History rows require two distinct participants.");
            if (string.IsNullOrWhiteSpace(speakerLabel) || speakerLabel.Length > 256)
                throw new ArgumentException("Speaker label must be bounded.", nameof(speakerLabel));
            if (string.IsNullOrWhiteSpace(recipientLabel) || recipientLabel.Length > 256)
                throw new ArgumentException("Recipient label must be bounded.", nameof(recipientLabel));
            if (string.IsNullOrWhiteSpace(text) || text.Length > 4000)
                throw new ArgumentException("Displayed text must be bounded.", nameof(text));
            if (displayedAtTick < 0) throw new ArgumentOutOfRangeException(nameof(displayedAtTick));
            if (!Enum.IsDefined(typeof(DialoguePresentationChannel), channel))
                throw new ArgumentOutOfRangeException(nameof(channel));
            if (supportingEvidenceCount < 1 || supportingEvidenceCount > 32)
                throw new ArgumentOutOfRangeException(nameof(supportingEvidenceCount));

            EventId = eventId;
            ConversationId = conversationId;
            SpeakerId = speakerId;
            SpeakerLabel = speakerLabel;
            RecipientId = recipientId;
            RecipientLabel = recipientLabel;
            Text = text;
            DisplayedAtTick = displayedAtTick;
            DisplayedAtUtc = displayedAtUtc;
            Channel = channel;
            SupportingEvidenceCount = supportingEvidenceCount;
        }

        public EventId EventId { get; }
        public ConversationId ConversationId { get; }
        public IndividualId SpeakerId { get; }
        public string SpeakerLabel { get; }
        public IndividualId RecipientId { get; }
        public string RecipientLabel { get; }
        public string Text { get; }
        public long DisplayedAtTick { get; }
        public DateTimeOffset DisplayedAtUtc { get; }
        public DialoguePresentationChannel Channel { get; }
        public int SupportingEvidenceCount { get; }
    }

    public sealed class ConversationHistoryViewerSnapshot
    {
        public ConversationHistoryViewerSnapshot(
            IndividualId viewerId,
            string viewerLabel,
            IEnumerable<ConversationHistoryViewerRow> rows,
            int privacyFilteredCount,
            int malformedCount,
            int trimmedCount)
        {
            if (viewerId.Value == Guid.Empty)
                throw new ArgumentException("Viewer ID cannot be empty.", nameof(viewerId));
            if (string.IsNullOrWhiteSpace(viewerLabel) || viewerLabel.Length > 256)
                throw new ArgumentException("Viewer label must be bounded.", nameof(viewerLabel));
            if (privacyFilteredCount < 0 || malformedCount < 0 || trimmedCount < 0)
                throw new ArgumentOutOfRangeException("Diagnostic counts cannot be negative.");

            ViewerId = viewerId;
            ViewerLabel = viewerLabel;
            Rows = new ReadOnlyCollection<ConversationHistoryViewerRow>(
                (rows ?? throw new ArgumentNullException(nameof(rows))).ToList());
            PrivacyFilteredCount = privacyFilteredCount;
            MalformedCount = malformedCount;
            TrimmedCount = trimmedCount;
        }

        public IndividualId ViewerId { get; }
        public string ViewerLabel { get; }
        public IReadOnlyList<ConversationHistoryViewerRow> Rows { get; }
        public int PrivacyFilteredCount { get; }
        public int MalformedCount { get; }
        public int TrimmedCount { get; }
    }

    /// <summary>
    /// Builds an immutable participant-scoped viewer model from factual records
    /// of actually displayed speech. It never owns or mutates canonical history.
    /// </summary>
    public sealed class ConversationHistoryViewerBuilder
    {
        public const int MaximumRows = 200;

        public ConversationHistoryViewerSnapshot Build(
            IndividualId viewerId,
            IEnumerable<ConversationHistoryParticipant> participants,
            IEnumerable<PriorConversationTurn> admittedTurns,
            int maximumRows = 100)
        {
            if (viewerId.Value == Guid.Empty)
                throw new ArgumentException("Viewer ID cannot be empty.", nameof(viewerId));
            if (participants is null) throw new ArgumentNullException(nameof(participants));
            if (admittedTurns is null) throw new ArgumentNullException(nameof(admittedTurns));
            if (maximumRows < 1 || maximumRows > MaximumRows)
                throw new ArgumentOutOfRangeException(nameof(maximumRows));

            var participantMap = participants
                .Select(value => value ?? throw new ArgumentException(
                    "Participant list cannot contain null values.",
                    nameof(participants)))
                .GroupBy(value => value.IndividualId)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().DisplayLabel);
            if (!participantMap.TryGetValue(viewerId, out var viewerLabel))
                throw new ArgumentException("Viewer must be an enrolled participant.", nameof(viewerId));

            var accepted = new List<ConversationHistoryViewerRow>();
            var privacyFiltered = 0;
            var malformed = 0;

            foreach (var turn in admittedTurns)
            {
                if (turn is null)
                {
                    malformed++;
                    continue;
                }

                if (!turn.AudienceIds.Contains(viewerId))
                {
                    privacyFiltered++;
                    continue;
                }

                if (turn.SpeakerId != viewerId && turn.RecipientId != viewerId)
                {
                    privacyFiltered++;
                    continue;
                }

                if (!participantMap.TryGetValue(turn.SpeakerId, out var speakerLabel) ||
                    !participantMap.TryGetValue(turn.RecipientId, out var recipientLabel))
                {
                    malformed++;
                    continue;
                }

                accepted.Add(new ConversationHistoryViewerRow(
                    turn.EventId,
                    turn.ConversationId,
                    turn.SpeakerId,
                    speakerLabel,
                    turn.RecipientId,
                    recipientLabel,
                    turn.Text,
                    turn.DisplayedAtTick,
                    turn.DisplayedAtUtc,
                    turn.Channel,
                    1));
            }

            var ordered = accepted
                .GroupBy(value => value.EventId)
                .Select(group => group.First())
                .OrderByDescending(value => value.DisplayedAtTick)
                .ThenByDescending(value => value.DisplayedAtUtc)
                .ThenBy(value => value.EventId.ToString(), StringComparer.Ordinal)
                .ToArray();
            var trimmed = Math.Max(0, ordered.Length - maximumRows);
            var selected = ordered.Take(maximumRows).ToArray();

            return new ConversationHistoryViewerSnapshot(
                viewerId,
                viewerLabel,
                selected,
                privacyFiltered,
                malformed,
                trimmed);
        }
    }
}
