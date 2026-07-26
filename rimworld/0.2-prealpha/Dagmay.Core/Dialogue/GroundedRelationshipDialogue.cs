using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Dagmay.Core.Contracts;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;

namespace Dagmay.Core.Dialogue
{
    /// <summary>
    /// Plain, provenance-bearing relationship evidence suitable for bounded dialogue context.
    /// This is a rebuildable projection of canonical experience records, never independent truth.
    /// </summary>
    public sealed class GroundedRelationshipEvidence
    {
        public GroundedRelationshipEvidence(
            EventId eventId,
            string eventKind,
            long occurredAtTick,
            IEnumerable<KeyValuePair<string, string>> factualPayload,
            PerceptionChannel channel,
            PrivacyClassification privacy,
            double confidence)
        {
            if (eventId.Value == Guid.Empty)
                throw new ArgumentException("Relationship dialogue evidence requires an EventId.", nameof(eventId));
            if (string.IsNullOrWhiteSpace(eventKind) || eventKind.Length > 128)
                throw new ArgumentException("Relationship dialogue evidence requires an event kind.", nameof(eventKind));
            if (occurredAtTick < 0) throw new ArgumentOutOfRangeException(nameof(occurredAtTick));
            if (factualPayload is null) throw new ArgumentNullException(nameof(factualPayload));
            if (!Enum.IsDefined(typeof(PerceptionChannel), channel))
                throw new ArgumentOutOfRangeException(nameof(channel));
            if (!Enum.IsDefined(typeof(PrivacyClassification), privacy))
                throw new ArgumentOutOfRangeException(nameof(privacy));
            if (double.IsNaN(confidence) || double.IsInfinity(confidence) ||
                confidence < 0 || confidence > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(confidence));
            }

            var payload = factualPayload
                .Select(pair => new KeyValuePair<string, string>(
                    ValidateText(pair.Key, nameof(factualPayload), 128),
                    ValidateText(pair.Value, nameof(factualPayload), 512)))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            if (payload.Count == 0 || payload.Count > 16)
                throw new ArgumentOutOfRangeException(nameof(factualPayload));

            EventId = eventId;
            EventKind = eventKind;
            OccurredAtTick = occurredAtTick;
            FactualPayload = new ReadOnlyDictionary<string, string>(payload);
            Channel = channel;
            Privacy = privacy;
            Confidence = confidence;
            Valence = GroundedRelationshipDialogueComposer.InferValence(eventKind, FactualPayload);
        }

        public EventId EventId { get; }
        public string EventKind { get; }
        public long OccurredAtTick { get; }
        public IReadOnlyDictionary<string, string> FactualPayload { get; }
        public PerceptionChannel Channel { get; }
        public PrivacyClassification Privacy { get; }
        public double Confidence { get; }
        public double Valence { get; }

        public bool CanEnterDialogue =>
            Channel == PerceptionChannel.Experienced &&
            (Privacy == PrivacyClassification.Shareable ||
             Privacy == PrivacyClassification.RelationshipSensitive);

        public static GroundedRelationshipEvidence? TryCreate(
            ExperienceJournalRecord record,
            IndividualId ownerId,
            IndividualId otherId)
        {
            if (record is null) throw new ArgumentNullException(nameof(record));
            if (ownerId.Value == Guid.Empty || otherId.Value == Guid.Empty || ownerId == otherId)
                throw new ArgumentException("Relationship projection requires two distinct individuals.");

            var perception = record.Perception;
            var memory = record.Memory;
            var factual = record.FactualEvent;
            if (perception is null || memory is null) return null;
            if (perception.PerceiverId != ownerId ||
                memory.OwnerId != ownerId ||
                perception.SourceEventId != factual.Id ||
                !memory.SourcePerceptionIds.Contains(perception.Id) ||
                !factual.Subjects.Contains(ownerId) ||
                !factual.Subjects.Contains(otherId) ||
                !memory.PeopleInvolved.Contains(otherId) ||
                !GroundedRelationshipDialogueComposer.IsSupportedKind(factual.Kind))
            {
                return null;
            }

            return new GroundedRelationshipEvidence(
                factual.Id,
                factual.Kind,
                factual.GameTick ?? 0,
                factual.FactualPayload,
                perception.Channel,
                memory.Privacy,
                Math.Min(perception.Confidence, memory.Confidence));
        }

        private static string ValidateText(string value, string parameter, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
                throw new ArgumentException("Evidence keys and values must be bounded.", parameter);
            return value;
        }
    }

    public sealed class GroundedRelationshipDialoguePlan
    {
        public GroundedRelationshipDialoguePlan(
            string text,
            IEnumerable<EventId> evidenceIds,
            IEnumerable<GroundedRelationshipEvidence> selectedPriorEvidence)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 600)
                throw new ArgumentException("Grounded dialogue text must be bounded.", nameof(text));
            Text = text;
            EvidenceIds = new ReadOnlyCollection<EventId>(
                (evidenceIds ?? throw new ArgumentNullException(nameof(evidenceIds))).ToList());
            SelectedPriorEvidence = new ReadOnlyCollection<GroundedRelationshipEvidence>(
                (selectedPriorEvidence ?? throw new ArgumentNullException(nameof(selectedPriorEvidence))).ToList());
            if (EvidenceIds.Count == 0 || EvidenceIds.Count > 3 ||
                EvidenceIds.Any(id => id.Value == Guid.Empty) ||
                EvidenceIds.Distinct().Count() != EvidenceIds.Count)
            {
                throw new ArgumentException("A grounded dialogue plan requires one to three unique evidence IDs.");
            }
        }

        public string Text { get; }
        public IReadOnlyList<EventId> EvidenceIds { get; }
        public IReadOnlyList<GroundedRelationshipEvidence> SelectedPriorEvidence { get; }
    }

    /// <summary>
    /// Deterministically selects same-counterpart history and renders bounded first-person dialogue.
    /// It cannot call a model, mutate identity, alter relationships, or issue a RimWorld action.
    /// </summary>
    public sealed class GroundedRelationshipDialogueComposer
    {
        public const int DefaultMaximumPriorEvidence = 2;
        public const int MaximumTextLength = 600;

        private static readonly string[] PositiveRelationTokens =
        {
            "bond", "friend", "lover", "fiance", "spouse", "parent", "child",
            "sibling", "grandparent", "grandchild", "family", "kin"
        };

        private static readonly string[] NegativeRelationTokens =
        {
            "rival", "enemy", "hostile", "exlover", "exspouse"
        };

        public GroundedRelationshipDialoguePlan Compose(
            string recipientDisplayName,
            GroundedRelationshipEvidence current,
            IEnumerable<GroundedRelationshipEvidence> priorCandidates,
            int maximumPriorEvidence = DefaultMaximumPriorEvidence)
        {
            if (string.IsNullOrWhiteSpace(recipientDisplayName) || recipientDisplayName.Length > 128)
                throw new ArgumentException("A bounded recipient display name is required.", nameof(recipientDisplayName));
            if (current is null) throw new ArgumentNullException(nameof(current));
            if (priorCandidates is null) throw new ArgumentNullException(nameof(priorCandidates));
            if (maximumPriorEvidence < 0 || maximumPriorEvidence > DefaultMaximumPriorEvidence)
                throw new ArgumentOutOfRangeException(nameof(maximumPriorEvidence));
            if (!IsSupportedKind(current.EventKind) || !current.CanEnterDialogue)
                throw new InvalidOperationException("Current relationship evidence is not eligible for dialogue.");

            var eligible = priorCandidates
                .Select(value => value ?? throw new ArgumentException(
                    "Prior relationship evidence cannot contain null values.",
                    nameof(priorCandidates)))
                .Where(value => value.EventId != current.EventId)
                .Where(value => value.OccurredAtTick <= current.OccurredAtTick)
                .Where(value => value.CanEnterDialogue)
                .Where(value => IsSupportedKind(value.EventKind))
                .GroupBy(value => value.EventId)
                .Select(group => group.First())
                .ToArray();

            var selected = SelectPriorEvidence(current, eligible, maximumPriorEvidence);
            var ids = new[] { current.EventId }
                .Concat(selected.Select(value => value.EventId))
                .ToArray();
            var text = Render(recipientDisplayName, current, selected);
            return new GroundedRelationshipDialoguePlan(text, ids, selected);
        }

        public static bool IsSupportedKind(string eventKind) =>
            string.Equals(eventKind, "rimworld.social.opinion_changed", StringComparison.Ordinal) ||
            string.Equals(eventKind, "rimworld.relationship.direct_changed", StringComparison.Ordinal);

        public static double InferValence(
            string eventKind,
            IReadOnlyDictionary<string, string> factualPayload)
        {
            if (factualPayload is null) throw new ArgumentNullException(nameof(factualPayload));
            if (string.Equals(eventKind, "rimworld.social.opinion_changed", StringComparison.Ordinal))
            {
                if (!TryInteger(factualPayload, "opinion_delta", out var delta)) return 0;
                return ClampSigned(delta / 100d);
            }

            if (!string.Equals(eventKind, "rimworld.relationship.direct_changed", StringComparison.Ordinal))
                return 0;

            var before = Relations(factualPayload, "relations_before");
            var after = Relations(factualPayload, "relations_after");
            var added = after.Except(before, StringComparer.OrdinalIgnoreCase).ToArray();
            var removed = before.Except(after, StringComparer.OrdinalIgnoreCase).ToArray();
            var score = 0d;
            foreach (var relation in added)
            {
                if (Matches(relation, PositiveRelationTokens)) score += 1;
                if (Matches(relation, NegativeRelationTokens)) score -= 1;
            }
            foreach (var relation in removed)
            {
                if (Matches(relation, PositiveRelationTokens)) score -= 1;
                if (Matches(relation, NegativeRelationTokens)) score += 0.75;
            }
            return ClampSigned(score);
        }

        public static string DescribeEvidence(GroundedRelationshipEvidence evidence)
        {
            if (evidence is null) throw new ArgumentNullException(nameof(evidence));
            if (string.Equals(evidence.EventKind, "rimworld.social.opinion_changed", StringComparison.Ordinal))
            {
                var before = Value(evidence.FactualPayload, "opinion_before", "unknown");
                var after = Value(evidence.FactualPayload, "opinion_after", "unknown");
                var delta = Value(evidence.FactualPayload, "opinion_delta", "unknown");
                return $"Opinion changed from {before} to {after} (delta {delta}).";
            }

            var relationsBefore = Value(evidence.FactualPayload, "relations_before", "none");
            var relationsAfter = Value(evidence.FactualPayload, "relations_after", "none");
            return $"Direct relationship changed from {relationsBefore} to {relationsAfter}.";
        }

        private static IReadOnlyList<GroundedRelationshipEvidence> SelectPriorEvidence(
            GroundedRelationshipEvidence current,
            IReadOnlyList<GroundedRelationshipEvidence> eligible,
            int maximum)
        {
            if (maximum == 0 || eligible.Count == 0)
                return Array.Empty<GroundedRelationshipEvidence>();

            var selected = new List<GroundedRelationshipEvidence>(maximum);
            var currentSign = Sign(current.Valence);
            if (currentSign != 0)
            {
                AddBest(selected, eligible.Where(value => Sign(value.Valence) == -currentSign), maximum);
                AddBest(selected, eligible.Where(value => Sign(value.Valence) == currentSign), maximum);
            }
            else
            {
                AddBest(selected, eligible.Where(value => Sign(value.Valence) > 0), maximum);
                AddBest(selected, eligible.Where(value => Sign(value.Valence) < 0), maximum);
            }

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
            if (best is not null && selected.All(value => value.EventId != best.EventId))
                selected.Add(best);
        }

        private static IOrderedEnumerable<GroundedRelationshipEvidence> Rank(
            IEnumerable<GroundedRelationshipEvidence> values) =>
            values
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
            string recipientDisplayName,
            GroundedRelationshipEvidence current,
            IReadOnlyList<GroundedRelationshipEvidence> selected)
        {
            var builder = new StringBuilder();
            builder.Append(recipientDisplayName.Trim());
            builder.Append(", ");
            builder.Append(CurrentClause(current));

            if (selected.Count > 0)
            {
                var memories = selected.Select(PriorClause).ToArray();
                var all = new[] { current }.Concat(selected).ToArray();
                var mixed = all.Any(value => Sign(value.Valence) > 0) &&
                            all.Any(value => Sign(value.Valence) < 0);
                if (mixed)
                {
                    builder.Append(" I remember that ");
                    builder.Append(JoinClauses(memories));
                    builder.Append(". That leaves me conflicted.");
                }
                else if (Sign(current.Valence) > 0)
                {
                    builder.Append(" It fits with what I remember: ");
                    builder.Append(JoinClauses(memories));
                    builder.Append('.');
                }
                else if (Sign(current.Valence) < 0)
                {
                    builder.Append(" It adds to what I remember: ");
                    builder.Append(JoinClauses(memories));
                    builder.Append('.');
                }
                else
                {
                    builder.Append(" I remember that ");
                    builder.Append(JoinClauses(memories));
                    builder.Append('.');
                }
            }

            return Bound(builder.ToString());
        }

        private static string CurrentClause(GroundedRelationshipEvidence evidence)
        {
            if (string.Equals(evidence.EventKind, "rimworld.social.opinion_changed", StringComparison.Ordinal))
            {
                if (!TryInteger(evidence.FactualPayload, "opinion_delta", out var delta) || delta == 0)
                    return "my opinion of you changed.";
                return delta > 0
                    ? "my opinion of you has improved."
                    : "my opinion of you has worsened.";
            }

            return DirectRelationshipClause(evidence.FactualPayload, current: true) + ".";
        }

        private static string PriorClause(GroundedRelationshipEvidence evidence)
        {
            if (string.Equals(evidence.EventKind, "rimworld.social.opinion_changed", StringComparison.Ordinal))
            {
                if (!TryInteger(evidence.FactualPayload, "opinion_delta", out var delta) || delta == 0)
                    return "my opinion of you changed";
                return delta > 0
                    ? "my opinion of you improved"
                    : "my opinion of you worsened";
            }

            return DirectRelationshipClause(evidence.FactualPayload, current: false);
        }

        private static string DirectRelationshipClause(
            IReadOnlyDictionary<string, string> payload,
            bool current)
        {
            var before = Relations(payload, "relations_before");
            var after = Relations(payload, "relations_after");
            var added = after.Except(before, StringComparer.OrdinalIgnoreCase).ToArray();
            var removed = before.Except(after, StringComparer.OrdinalIgnoreCase).ToArray();

            if (added.Length == 1 && removed.Length == 0)
                return current
                    ? "we are now " + Humanize(added[0])
                    : "we became " + Humanize(added[0]);
            if (removed.Length == 1 && added.Length == 0)
                return current
                    ? "we are no longer " + Humanize(removed[0])
                    : "we stopped being " + Humanize(removed[0]);

            return current
                ? "our relationship changed from " + DisplayRelations(before) + " to " + DisplayRelations(after)
                : "our relationship changed from " + DisplayRelations(before) + " to " + DisplayRelations(after);
        }

        private static string JoinClauses(IReadOnlyList<string> clauses)
        {
            if (clauses.Count == 1) return clauses[0];
            return clauses[0] + ", and " + clauses[1];
        }

        private static string Bound(string value)
        {
            if (value.Length <= MaximumTextLength) return value;
            var bounded = value.Substring(0, MaximumTextLength - 4).TrimEnd();
            return bounded + "...";
        }

        private static int Sign(double value)
        {
            if (value > 0.05) return 1;
            if (value < -0.05) return -1;
            return 0;
        }

        private static string[] Relations(
            IReadOnlyDictionary<string, string> payload,
            string key)
        {
            var raw = Value(payload, key, "none");
            if (string.Equals(raw, "none", StringComparison.OrdinalIgnoreCase))
                return Array.Empty<string>();
            return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string DisplayRelations(IReadOnlyList<string> relations) =>
            relations.Count == 0
                ? "no direct relation"
                : string.Join(" and ", relations.Select(Humanize));

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

        private static bool Matches(string value, IEnumerable<string> tokens)
        {
            var normalized = value.Replace("_", string.Empty).Replace(" ", string.Empty);
            return tokens.Any(token =>
                normalized.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
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
            payload.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;

        private static double ClampSigned(double value) =>
            Math.Max(-1, Math.Min(1, value));
    }
}
