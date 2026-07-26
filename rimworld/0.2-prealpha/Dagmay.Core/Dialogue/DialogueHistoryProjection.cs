using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Memory;

namespace Dagmay.Core.Dialogue
{
    public enum DialogueHistoryView
    {
        Participant = 0,
        Observer = 1
    }

    public sealed class DialogueHistoryQuery
    {
        public DialogueHistoryQuery(
            ConversationId conversationId,
            long throughTick,
            int maximumEntries,
            DialogueHistoryView view,
            IndividualId? viewerId = null)
        {
            if (conversationId.Value == Guid.Empty)
                throw new ArgumentException("Conversation ID cannot be empty.", nameof(conversationId));
            if (throughTick < 0) throw new ArgumentOutOfRangeException(nameof(throughTick));
            if (maximumEntries < 1 || maximumEntries > 100)
                throw new ArgumentOutOfRangeException(nameof(maximumEntries));
            if (!Enum.IsDefined(typeof(DialogueHistoryView), view))
                throw new ArgumentOutOfRangeException(nameof(view));
            if (viewerId.HasValue && viewerId.Value.Value == Guid.Empty)
                throw new ArgumentException("Viewer ID cannot be empty.", nameof(viewerId));
            if (view == DialogueHistoryView.Participant && !viewerId.HasValue)
            {
                throw new ArgumentException("Participant history requires a viewer ID.", nameof(viewerId));
            }

            ConversationId = conversationId;
            ThroughTick = throughTick;
            MaximumEntries = maximumEntries;
            View = view;
            ViewerId = viewerId;
        }

        public ConversationId ConversationId { get; }
        public long ThroughTick { get; }
        public int MaximumEntries { get; }
        public DialogueHistoryView View { get; }
        public IndividualId? ViewerId { get; }
    }

    public sealed class DialogueHistoryEntry
    {
        internal DialogueHistoryEntry(
            EventId eventId,
            UtteranceId utteranceId,
            DialogueRequestId requestId,
            ConversationId conversationId,
            IndividualId speakerId,
            IndividualId? recipientId,
            string text,
            long displayedAtTick,
            DateTimeOffset displayedAtUtc,
            DialogueDisclosure disclosure,
            DialoguePresentationChannel channel,
            IEnumerable<EventId> supportingEventIds,
            IEnumerable<IndividualId> audienceIds)
        {
            EventId = eventId;
            UtteranceId = utteranceId;
            RequestId = requestId;
            ConversationId = conversationId;
            SpeakerId = speakerId;
            RecipientId = recipientId;
            Text = text;
            DisplayedAtTick = displayedAtTick;
            DisplayedAtUtc = displayedAtUtc;
            Disclosure = disclosure;
            Channel = channel;
            SupportingEventIds = ContractGuard.List(supportingEventIds, nameof(supportingEventIds));
            AudienceIds = ContractGuard.List(audienceIds, nameof(audienceIds));
        }

        public EventId EventId { get; }
        public UtteranceId UtteranceId { get; }
        public DialogueRequestId RequestId { get; }
        public ConversationId ConversationId { get; }
        public IndividualId SpeakerId { get; }
        public IndividualId? RecipientId { get; }
        public string Text { get; }
        public long DisplayedAtTick { get; }
        public DateTimeOffset DisplayedAtUtc { get; }
        public DialogueDisclosure Disclosure { get; }
        public DialoguePresentationChannel Channel { get; }
        public IReadOnlyList<EventId> SupportingEventIds { get; }
        public IReadOnlyList<IndividualId> AudienceIds { get; }
    }

    public sealed class DialogueHistoryPacket
    {
        internal DialogueHistoryPacket(
            DialogueHistoryQuery query,
            IEnumerable<DialogueHistoryEntry> entries,
            int malformedEventCount,
            int privacyFilteredCount,
            int trimmedCount)
        {
            Query = query ?? throw new ArgumentNullException(nameof(query));
            Entries = ContractGuard.List(entries, nameof(entries));
            MalformedEventCount = malformedEventCount;
            PrivacyFilteredCount = privacyFilteredCount;
            TrimmedCount = trimmedCount;
        }

        public DialogueHistoryQuery Query { get; }
        public IReadOnlyList<DialogueHistoryEntry> Entries { get; }
        public int MalformedEventCount { get; }
        public int PrivacyFilteredCount { get; }
        public int TrimmedCount { get; }
    }

    /// <summary>
    /// Produces a read-only conversation projection from canonical factual events.
    /// It never creates or owns a second conversation-memory database.
    /// </summary>
    public sealed class DialogueHistoryProjection
    {
        private readonly string _expectedSource;

        public DialogueHistoryProjection(string expectedSource = DialogueEventAdmissionService.DefaultSource)
        {
            _expectedSource = ContractGuard.Text(expectedSource, nameof(expectedSource), 512);
        }

        public DialogueHistoryPacket Build(
            IEnumerable<EventLedgerEntry> ledgerEntries,
            DialogueHistoryQuery query)
        {
            if (ledgerEntries is null) throw new ArgumentNullException(nameof(ledgerEntries));
            if (query is null) throw new ArgumentNullException(nameof(query));

            var accepted = new List<DialogueHistoryEntry>();
            var malformed = 0;
            var privacyFiltered = 0;

            foreach (var ledgerEntry in ledgerEntries)
            {
                if (ledgerEntry is null || ledgerEntry.Value is null)
                {
                    malformed++;
                    continue;
                }

                var factualEvent = ledgerEntry.Value;
                if (!string.Equals(
                        factualEvent.Kind,
                        DialogueEventAdmissionService.EventKind,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(factualEvent.Source, _expectedSource, StringComparison.Ordinal) ||
                    !TryParse(factualEvent, out var entry))
                {
                    malformed++;
                    continue;
                }

                if (entry.ConversationId != query.ConversationId ||
                    entry.DisplayedAtTick > query.ThroughTick)
                {
                    continue;
                }

                if (query.View == DialogueHistoryView.Participant &&
                    !entry.AudienceIds.Contains(query.ViewerId.GetValueOrDefault()))
                {
                    privacyFiltered++;
                    continue;
                }

                accepted.Add(entry);
            }

            var ordered = accepted
                .OrderBy(entry => entry.DisplayedAtTick)
                .ThenBy(entry => entry.DisplayedAtUtc)
                .ThenBy(entry => entry.EventId.ToString(), StringComparer.Ordinal)
                .ToArray();
            var trimmed = Math.Max(0, ordered.Length - query.MaximumEntries);
            var selected = ordered.Skip(trimmed).ToArray();

            return new DialogueHistoryPacket(
                query,
                selected,
                malformed,
                privacyFiltered,
                trimmed);
        }

        private static bool TryParse(
            EnvironmentEvent factualEvent,
            out DialogueHistoryEntry entry)
        {
            entry = null!;
            if (!factualEvent.GameTick.HasValue || factualEvent.GameTick.Value < 0)
                return false;
            if (factualEvent.Subjects.Count == 0 || factualEvent.Subjects.Count > 32)
                return false;
            if (factualEvent.Subjects.Any(id => id.Value == Guid.Empty) ||
                factualEvent.Subjects.Distinct().Count() != factualEvent.Subjects.Count)
            {
                return false;
            }

            if (!TryValue(factualEvent, DialogueEventAdmissionService.ConversationIdKey, out var conversationText) ||
                !TryValue(factualEvent, DialogueEventAdmissionService.RequestIdKey, out var requestText) ||
                !TryValue(factualEvent, DialogueEventAdmissionService.UtteranceIdKey, out var utteranceText) ||
                !TryValue(factualEvent, DialogueEventAdmissionService.SpeakerIdKey, out var speakerText) ||
                !TryValue(factualEvent, DialogueEventAdmissionService.TextKey, out var spokenText) ||
                !TryValue(factualEvent, DialogueEventAdmissionService.DisclosureKey, out var disclosureText) ||
                !TryValue(factualEvent, DialogueEventAdmissionService.PresentationChannelKey, out var channelText) ||
                !TryValue(factualEvent, DialogueEventAdmissionService.SupportingEventIdsKey, out var supportText))
            {
                return false;
            }

            if (!TryParseGuid(conversationText, out var conversationGuid) ||
                !TryParseGuid(requestText, out var requestGuid) ||
                !TryParseGuid(utteranceText, out var utteranceGuid) ||
                !TryParseGuid(speakerText, out var speakerGuid))
            {
                return false;
            }

            if (spokenText.Length > 4000)
                return false;
            if (!Enum.TryParse(disclosureText, false, out DialogueDisclosure disclosure) ||
                !Enum.IsDefined(typeof(DialogueDisclosure), disclosure))
            {
                return false;
            }
            if (!Enum.TryParse(channelText, false, out DialoguePresentationChannel channel) ||
                !Enum.IsDefined(typeof(DialoguePresentationChannel), channel))
            {
                return false;
            }

            IndividualId? recipientId = null;
            if (!factualEvent.FactualPayload.TryGetValue(
                    DialogueEventAdmissionService.RecipientIdKey,
                    out var recipientText))
            {
                return false;
            }
            if (!string.IsNullOrWhiteSpace(recipientText))
            {
                if (!TryParseGuid(recipientText, out var recipientGuid)) return false;
                recipientId = new IndividualId(recipientGuid);
            }

            var supportingEvents = new List<EventId>();
            foreach (var token in supportText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!TryParseGuid(token, out var eventGuid)) return false;
                supportingEvents.Add(new EventId(eventGuid));
            }
            if (supportingEvents.Count == 0 || supportingEvents.Count > 32 ||
                supportingEvents.Distinct().Count() != supportingEvents.Count)
            {
                return false;
            }

            var utteranceId = new UtteranceId(utteranceGuid);
            var expectedEventId = new EventId(utteranceGuid);
            if (factualEvent.Id != expectedEventId ||
                !string.Equals(
                    factualEvent.DeduplicationKey,
                    DialogueEventAdmissionService.DeduplicationKey(utteranceId),
                    StringComparison.Ordinal))
            {
                return false;
            }

            var speakerId = new IndividualId(speakerGuid);
            if (!factualEvent.Subjects.Contains(speakerId)) return false;
            if (recipientId.HasValue && !factualEvent.Subjects.Contains(recipientId.Value)) return false;

            entry = new DialogueHistoryEntry(
                factualEvent.Id,
                utteranceId,
                new DialogueRequestId(requestGuid),
                new ConversationId(conversationGuid),
                speakerId,
                recipientId,
                spokenText,
                factualEvent.GameTick.Value,
                factualEvent.OccurredAtUtc,
                disclosure,
                channel,
                supportingEvents,
                factualEvent.Subjects);
            return true;
        }

        private static bool TryValue(
            EnvironmentEvent factualEvent,
            string key,
            out string value)
        {
            if (factualEvent.FactualPayload.TryGetValue(key, out var found) &&
                !string.IsNullOrWhiteSpace(found))
            {
                value = found;
                return true;
            }

            value = string.Empty;
            return false;
        }

        private static bool TryParseGuid(string text, out Guid value)
        {
            return Guid.TryParseExact(text, "N", out value) && value != Guid.Empty;
        }
    }
}
