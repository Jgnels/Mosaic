using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Dialogue
{
    public sealed class GroundedConversationReplyPlan
    {
        public GroundedConversationReplyPlan(
            string text,
            IEnumerable<EventId> evidenceIds,
            IEnumerable<GroundedRelationshipEvidence> selectedRecipientEvidence)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > GroundedConversationReplyComposer.MaximumTextLength)
                throw new ArgumentException("Grounded reply text must be bounded.", nameof(text));
            Text = text;
            EvidenceIds = new ReadOnlyCollection<EventId>(
                (evidenceIds ?? throw new ArgumentNullException(nameof(evidenceIds))).ToList());
            SelectedRecipientEvidence = new ReadOnlyCollection<GroundedRelationshipEvidence>(
                (selectedRecipientEvidence ?? throw new ArgumentNullException(nameof(selectedRecipientEvidence))).ToList());
            if (EvidenceIds.Count == 0 ||
                EvidenceIds.Count > 3 ||
                EvidenceIds.Any(value => value.Value == Guid.Empty) ||
                EvidenceIds.Distinct().Count() != EvidenceIds.Count)
            {
                throw new ArgumentException(
                    "A grounded reply requires one to three unique evidence IDs.",
                    nameof(evidenceIds));
            }
        }

        public string Text { get; }
        public IReadOnlyList<EventId> EvidenceIds { get; }
        public IReadOnlyList<GroundedRelationshipEvidence> SelectedRecipientEvidence { get; }
    }

    /// <summary>
    /// Renders one bounded deterministic reply after an actually displayed
    /// grounded opening. The current social event remains mandatory evidence,
    /// while optional history belongs to the replying individual.
    /// </summary>
    public sealed class GroundedConversationReplyComposer
    {
        public const int DefaultMaximumPriorEvidence = 2;
        public const int MaximumTextLength = 600;

        public GroundedConversationReplyPlan Compose(
            string originalSpeakerDisplayName,
            GroundedRelationshipEvidence currentSpeakerEvidence,
            IEnumerable<GroundedRelationshipEvidence> recipientPriorCandidates,
            int maximumPriorEvidence = DefaultMaximumPriorEvidence)
        {
            if (string.IsNullOrWhiteSpace(originalSpeakerDisplayName) ||
                originalSpeakerDisplayName.Length > 128)
            {
                throw new ArgumentException(
                    "A bounded original-speaker display name is required.",
                    nameof(originalSpeakerDisplayName));
            }
            if (currentSpeakerEvidence is null)
                throw new ArgumentNullException(nameof(currentSpeakerEvidence));
            if (recipientPriorCandidates is null)
                throw new ArgumentNullException(nameof(recipientPriorCandidates));
            if (maximumPriorEvidence < 0 ||
                maximumPriorEvidence > DefaultMaximumPriorEvidence)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumPriorEvidence));
            }
            if (!currentSpeakerEvidence.CanEnterDialogue ||
                !GroundedRelationshipDialogueComposer.IsSupportedKind(
                    currentSpeakerEvidence.EventKind))
            {
                throw new InvalidOperationException(
                    "The opening social evidence is not eligible for a grounded reply.");
            }

            var eligible = recipientPriorCandidates
                .Select(value => value ?? throw new ArgumentException(
                    "Recipient relationship evidence cannot contain null values.",
                    nameof(recipientPriorCandidates)))
                .Where(value => value.EventId != currentSpeakerEvidence.EventId)
                .Where(value => value.OccurredAtTick <= currentSpeakerEvidence.OccurredAtTick)
                .Where(value => value.CanEnterDialogue)
                .Where(value => GroundedRelationshipDialogueComposer.IsSupportedKind(
                    value.EventKind))
                .GroupBy(value => value.EventId)
                .Select(group => group.First())
                .ToArray();

            var selected = Select(eligible, maximumPriorEvidence);
            var evidenceIds = new[] { currentSpeakerEvidence.EventId }
                .Concat(selected.Select(value => value.EventId))
                .ToArray();
            var text = Render(
                originalSpeakerDisplayName.Trim(),
                currentSpeakerEvidence,
                selected);
            return new GroundedConversationReplyPlan(text, evidenceIds, selected);
        }

        private static IReadOnlyList<GroundedRelationshipEvidence> Select(
            IReadOnlyList<GroundedRelationshipEvidence> eligible,
            int maximum)
        {
            if (maximum == 0 || eligible.Count == 0)
                return Array.Empty<GroundedRelationshipEvidence>();

            var selected = new List<GroundedRelationshipEvidence>(maximum);
            AddBest(selected, eligible.Where(value => Sign(value.Valence) > 0), maximum);
            AddBest(selected, eligible.Where(value => Sign(value.Valence) < 0), maximum);

            foreach (var candidate in Rank(eligible))
            {
                if (selected.Count >= maximum) break;
                if (selected.All(value => value.EventId != candidate.EventId))
                    selected.Add(candidate);
            }

            return new ReadOnlyCollection<GroundedRelationshipEvidence>(selected);
        }

        private static void AddBest(
            ICollection<GroundedRelationshipEvidence> selected,
            IEnumerable<GroundedRelationshipEvidence> candidates,
            int maximum)
        {
            if (selected.Count >= maximum) return;
            var best = Rank(candidates).FirstOrDefault();
            if (best is not null &&
                selected.All(value => value.EventId != best.EventId))
            {
                selected.Add(best);
            }
        }

        private static IOrderedEnumerable<GroundedRelationshipEvidence> Rank(
            IEnumerable<GroundedRelationshipEvidence> candidates) =>
            candidates
                .OrderByDescending(Significance)
                .ThenByDescending(value => Math.Abs(value.Valence) * value.Confidence)
                .ThenByDescending(value => value.OccurredAtTick)
                .ThenBy(value => value.EventId.ToString(), StringComparer.Ordinal);

        private static int Significance(GroundedRelationshipEvidence evidence) =>
            string.Equals(
                evidence.EventKind,
                "rimworld.relationship.direct_changed",
                StringComparison.Ordinal)
                ? 2
                : 1;

        private static string Render(
            string originalSpeakerDisplayName,
            GroundedRelationshipEvidence currentSpeakerEvidence,
            IReadOnlyList<GroundedRelationshipEvidence> selected)
        {
            var builder = new StringBuilder();
            builder.Append(originalSpeakerDisplayName);
            builder.Append(", ");
            builder.Append(Acknowledge(currentSpeakerEvidence));

            if (selected.Count == 0)
            {
                builder.Append(
                    " I do not have enough of my own history with you to say more yet.");
                return Bound(builder.ToString());
            }

            builder.Append(" From my side, I remember that ");
            builder.Append(JoinClauses(selected.Select(PriorClause).ToArray()));
            builder.Append('.');

            var hasPositive = selected.Any(value => Sign(value.Valence) > 0);
            var hasNegative = selected.Any(value => Sign(value.Valence) < 0);
            if (hasPositive && hasNegative)
            {
                builder.Append(" My own view is mixed.");
            }
            else if (hasPositive)
            {
                builder.Append(" My own view has been warmer.");
            }
            else if (hasNegative)
            {
                builder.Append(" My own view has been more guarded.");
            }
            else
            {
                builder.Append(" My own view is still uncertain.");
            }

            return Bound(builder.ToString());
        }

        private static string Acknowledge(
            GroundedRelationshipEvidence currentSpeakerEvidence)
        {
            if (string.Equals(
                    currentSpeakerEvidence.EventKind,
                    "rimworld.social.opinion_changed",
                    StringComparison.Ordinal))
            {
                if (!TryInteger(
                        currentSpeakerEvidence.FactualPayload,
                        "opinion_delta",
                        out var delta) ||
                    delta == 0)
                {
                    return "I hear that your opinion of me changed.";
                }

                return delta > 0
                    ? "I hear that your opinion of me has improved."
                    : "I hear that your opinion of me has worsened.";
            }

            return "I hear that our direct relationship changed.";
        }

        private static string PriorClause(GroundedRelationshipEvidence evidence)
        {
            if (string.Equals(
                    evidence.EventKind,
                    "rimworld.social.opinion_changed",
                    StringComparison.Ordinal))
            {
                if (!TryInteger(
                        evidence.FactualPayload,
                        "opinion_delta",
                        out var delta) ||
                    delta == 0)
                {
                    return "my opinion of you changed";
                }

                return delta > 0
                    ? "my opinion of you improved"
                    : "my opinion of you worsened";
            }

            var before = Value(
                evidence.FactualPayload,
                "relations_before",
                "none");
            var after = Value(
                evidence.FactualPayload,
                "relations_after",
                "none");
            return "our direct relationship changed from "
                + HumanizeRelations(before)
                + " to "
                + HumanizeRelations(after);
        }

        private static string JoinClauses(IReadOnlyList<string> clauses)
        {
            if (clauses.Count == 1) return clauses[0];
            return clauses[0] + ", and " + clauses[1];
        }

        private static string HumanizeRelations(string value)
        {
            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
                return "no direct relation";
            return string.Join(
                " and ",
                value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Humanize)
                    .Where(item => item.Length > 0));
        }

        private static string Humanize(string value)
        {
            var normalized = value.Replace('_', ' ').Trim();
            var builder = new StringBuilder();
            for (var index = 0; index < normalized.Length; index++)
            {
                var character = normalized[index];
                if (index > 0 &&
                    char.IsUpper(character) &&
                    char.IsLower(normalized[index - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(character);
            }

            return builder.ToString().ToLowerInvariant();
        }

        private static int Sign(double value)
        {
            if (value > 0.05) return 1;
            if (value < -0.05) return -1;
            return 0;
        }

        private static bool TryInteger(
            IReadOnlyDictionary<string, string> payload,
            string key,
            out int value)
        {
            value = 0;
            return payload.TryGetValue(key, out var text) &&
                   int.TryParse(
                       text,
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out value);
        }

        private static string Value(
            IReadOnlyDictionary<string, string> payload,
            string key,
            string fallback) =>
            payload.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;

        private static string Bound(string value)
        {
            if (value.Length <= MaximumTextLength) return value;
            return value.Substring(0, MaximumTextLength - 4).TrimEnd() + "...";
        }
    }
}
