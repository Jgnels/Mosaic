using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Dialogue
{
    public sealed class DialogueContextBudget
    {
        public DialogueContextBudget(int maximumItems, int maximumCharacters)
        {
            if (maximumItems < 1 || maximumItems > 64)
                throw new ArgumentOutOfRangeException(nameof(maximumItems));
            if (maximumCharacters < 256 || maximumCharacters > 32000)
                throw new ArgumentOutOfRangeException(nameof(maximumCharacters));

            MaximumItems = maximumItems;
            MaximumCharacters = maximumCharacters;
        }

        public int MaximumItems { get; }
        public int MaximumCharacters { get; }
    }

    /// <summary>
    /// Applies ownership/privacy rules before deterministic ranking and budgeting.
    /// Observer-only context never enters a provider prompt.
    /// </summary>
    public sealed class DialogueContextAssembler
    {
        public DialogueContextPacket Build(
            IndividualId speakerId,
            IndividualId? recipientId,
            long currentTick,
            IEnumerable<DialogueContextItem> candidates,
            DialogueContextBudget budget)
        {
            if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));
            if (candidates is null) throw new ArgumentNullException(nameof(candidates));
            if (budget is null) throw new ArgumentNullException(nameof(budget));

            var accepted = new List<DialogueContextItem>();
            var usedCharacters = 0;
            var omitted = 0;

            var ordered = candidates
                .Select(item => item ?? throw new ArgumentException(
                    "Candidate context cannot contain null items.",
                    nameof(candidates)))
                .Where(item => IsDisclosable(item, speakerId, recipientId))
                .OrderByDescending(item => item.Relevance)
                .ThenBy(item => AudienceRank(item.Audience))
                .ThenBy(item => item.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.EvidenceIds[0].ToString(), StringComparer.Ordinal)
                .ThenBy(item => item.Text, StringComparer.Ordinal)
                .ToArray();

            foreach (var item in ordered)
            {
                var cost = item.Kind.Length + item.Text.Length + 2;
                if (accepted.Count >= budget.MaximumItems || usedCharacters + cost > budget.MaximumCharacters)
                {
                    omitted++;
                    continue;
                }

                accepted.Add(item);
                usedCharacters += cost;
            }

            return new DialogueContextPacket(
                speakerId,
                recipientId,
                currentTick,
                accepted,
                omitted,
                usedCharacters);
        }

        private static bool IsDisclosable(
            DialogueContextItem item,
            IndividualId speakerId,
            IndividualId? recipientId)
        {
            switch (item.Audience)
            {
                case DialogueContextAudience.Public:
                    return true;

                case DialogueContextAudience.PrivateSelf:
                    return item.OwnerId.HasValue && item.OwnerId.Value == speakerId;

                case DialogueContextAudience.RelationshipSensitive:
                    return item.OwnerId.HasValue &&
                           item.OwnerId.Value == speakerId &&
                           recipientId.HasValue &&
                           item.AllowedRecipientIds.Contains(recipientId.Value);

                case DialogueContextAudience.ObserverOnly:
                    return false;

                default:
                    return false;
            }
        }

        private static int AudienceRank(DialogueContextAudience audience)
        {
            switch (audience)
            {
                case DialogueContextAudience.Public:
                    return 0;
                case DialogueContextAudience.RelationshipSensitive:
                    return 1;
                case DialogueContextAudience.PrivateSelf:
                    return 2;
                default:
                    return 3;
            }
        }
    }
}
