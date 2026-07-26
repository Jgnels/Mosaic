using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Dialogue
{
    /// <summary>
    /// Converts a witnessed conversation projection into bounded context items.
    /// The source remains the canonical EnvironmentEvent; this class stores no history.
    /// </summary>
    public sealed class DialogueHistoryContextProjector
    {
        public IReadOnlyList<DialogueContextItem> Build(
            DialogueHistoryPacket history,
            IndividualId ownerId,
            IndividualId? currentRecipientId,
            int maximumEntries)
        {
            if (history is null) throw new ArgumentNullException(nameof(history));
            if (ownerId.Value == Guid.Empty) throw new ArgumentException("Owner ID cannot be empty.", nameof(ownerId));
            if (currentRecipientId.HasValue && currentRecipientId.Value.Value == Guid.Empty)
                throw new ArgumentException("Recipient ID cannot be empty.", nameof(currentRecipientId));
            if (maximumEntries < 1 || maximumEntries > 20)
                throw new ArgumentOutOfRangeException(nameof(maximumEntries));

            var eligible = history.Entries
                .Where(entry => entry.AudienceIds.Contains(ownerId))
                .Where(entry => !currentRecipientId.HasValue || entry.AudienceIds.Contains(currentRecipientId.Value))
                .OrderByDescending(entry => entry.DisplayedAtTick)
                .ThenByDescending(entry => entry.EventId.ToString(), StringComparer.Ordinal)
                .Take(maximumEntries)
                .OrderBy(entry => entry.DisplayedAtTick)
                .ThenBy(entry => entry.EventId.ToString(), StringComparer.Ordinal)
                .ToArray();

            var result = new List<DialogueContextItem>(eligible.Length);
            for (var index = 0; index < eligible.Length; index++)
            {
                var entry = eligible[index];
                var relevance = Math.Max(0.5, 1.0 - ((eligible.Length - 1 - index) * 0.03));
                var spokenText = entry.Text.Length <= 1500
                    ? entry.Text
                    : entry.Text.Substring(0, 1500) + " [truncated]";
                var text = "speaker_id=" + entry.SpeakerId.ToString()
                    + "; recipient_id=" + (entry.RecipientId?.ToString() ?? string.Empty)
                    + "; spoken_text=" + spokenText;

                if (currentRecipientId.HasValue)
                {
                    result.Add(new DialogueContextItem(
                        "recent-conversation-line",
                        text,
                        new[] { entry.EventId },
                        DialogueContextAudience.RelationshipSensitive,
                        relevance,
                        ownerId,
                        new[] { currentRecipientId.Value }));
                }
                else
                {
                    result.Add(new DialogueContextItem(
                        "recent-conversation-line",
                        text,
                        new[] { entry.EventId },
                        DialogueContextAudience.PrivateSelf,
                        relevance,
                        ownerId));
                }
            }

            return result;
        }
    }
}
