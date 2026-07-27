using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;

namespace Dagmay.RimWorld.Dialogue
{
    public sealed class RimWorldConversationParticipantSnapshot
    {
        public RimWorldConversationParticipantSnapshot(
            IndividualId individualId,
            string displayLabel,
            string externalId)
        {
            if (individualId.Value == Guid.Empty)
                throw new ArgumentException("Individual ID cannot be empty.", nameof(individualId));
            if (string.IsNullOrWhiteSpace(displayLabel) || displayLabel.Length > 256)
                throw new ArgumentException("Display label must be bounded.", nameof(displayLabel));
            if (string.IsNullOrWhiteSpace(externalId) || externalId.Length > 256)
                throw new ArgumentException("External ID must be bounded.", nameof(externalId));

            IndividualId = individualId;
            DisplayLabel = displayLabel;
            ExternalId = externalId;
        }

        public IndividualId IndividualId { get; }
        public string DisplayLabel { get; }
        public string ExternalId { get; }
    }

    public sealed class RimWorldConversationHistoryData
    {
        public RimWorldConversationHistoryData(
            IEnumerable<RimWorldConversationParticipantSnapshot> participants,
            ConversationHistoryViewerSnapshot? selectedHistory,
            string diagnostic)
        {
            Participants = new ReadOnlyCollection<RimWorldConversationParticipantSnapshot>(
                (participants ?? throw new ArgumentNullException(nameof(participants)))
                .OrderBy(value => value.DisplayLabel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.IndividualId.ToString(), StringComparer.Ordinal)
                .ToList());
            SelectedHistory = selectedHistory;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public IReadOnlyList<RimWorldConversationParticipantSnapshot> Participants { get; }
        public ConversationHistoryViewerSnapshot? SelectedHistory { get; }
        public string Diagnostic { get; }
    }
}
