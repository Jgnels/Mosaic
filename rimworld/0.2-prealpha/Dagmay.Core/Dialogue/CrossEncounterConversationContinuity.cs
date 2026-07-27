using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Dialogue
{
    /// <summary>
    /// Plain factual projection of one utterance that was actually displayed
    /// and admitted. It describes that speech occurred; it does not make the
    /// utterance's semantic claims independently true.
    /// </summary>
    public sealed class PriorConversationTurn
    {
        public PriorConversationTurn(
            EventId eventId,
            ConversationId conversationId,
            IndividualId speakerId,
            IndividualId recipientId,
            string text,
            long displayedAtTick,
            DateTimeOffset displayedAtUtc,
            DialoguePresentationChannel channel,
            IEnumerable<IndividualId> audienceIds)
        {
            if (eventId.Value == Guid.Empty)
                throw new ArgumentException("Prior dialogue EventId cannot be empty.", nameof(eventId));
            if (conversationId.Value == Guid.Empty)
                throw new ArgumentException("Prior ConversationId cannot be empty.", nameof(conversationId));
            if (speakerId.Value == Guid.Empty || recipientId.Value == Guid.Empty || speakerId == recipientId)
                throw new ArgumentException("Prior dialogue requires two distinct participants.");
            if (string.IsNullOrWhiteSpace(text) || text.Length > 4000)
                throw new ArgumentException("Prior displayed text must be bounded.", nameof(text));
            if (displayedAtTick < 0) throw new ArgumentOutOfRangeException(nameof(displayedAtTick));
            if (!Enum.IsDefined(typeof(DialoguePresentationChannel), channel))
                throw new ArgumentOutOfRangeException(nameof(channel));
            var audience = (audienceIds ?? throw new ArgumentNullException(nameof(audienceIds))).ToArray();
            if (audience.Length == 0 || audience.Length > 32 ||
                audience.Any(value => value.Value == Guid.Empty) ||
                audience.Distinct().Count() != audience.Length ||
                !audience.Contains(speakerId) || !audience.Contains(recipientId))
            {
                throw new ArgumentException(
                    "Prior displayed dialogue requires a valid audience containing both participants.",
                    nameof(audienceIds));
            }

            EventId = eventId;
            ConversationId = conversationId;
            SpeakerId = speakerId;
            RecipientId = recipientId;
            Text = text;
            DisplayedAtTick = displayedAtTick;
            DisplayedAtUtc = displayedAtUtc;
            Channel = channel;
            AudienceIds = new ReadOnlyCollection<IndividualId>(audience.ToList());
        }

        public EventId EventId { get; }
        public ConversationId ConversationId { get; }
        public IndividualId SpeakerId { get; }
        public IndividualId RecipientId { get; }
        public string Text { get; }
        public long DisplayedAtTick { get; }
        public DateTimeOffset DisplayedAtUtc { get; }
        public DialoguePresentationChannel Channel { get; }
        public IReadOnlyList<IndividualId> AudienceIds { get; }
    }

    public sealed class PriorConversationContext
    {
        public PriorConversationContext(
            IndividualId firstParticipantId,
            IndividualId secondParticipantId,
            PriorConversationTurn firstTurn,
            PriorConversationTurn secondTurn)
        {
            if (firstParticipantId.Value == Guid.Empty ||
                secondParticipantId.Value == Guid.Empty ||
                firstParticipantId == secondParticipantId)
            {
                throw new ArgumentException("A prior conversation requires two distinct participants.");
            }
            FirstTurn = firstTurn ?? throw new ArgumentNullException(nameof(firstTurn));
            SecondTurn = secondTurn ?? throw new ArgumentNullException(nameof(secondTurn));
            if (FirstTurn.ConversationId != SecondTurn.ConversationId ||
                FirstTurn.EventId == SecondTurn.EventId ||
                FirstTurn.DisplayedAtTick > SecondTurn.DisplayedAtTick ||
                (FirstTurn.DisplayedAtTick == SecondTurn.DisplayedAtTick &&
                 FirstTurn.DisplayedAtUtc > SecondTurn.DisplayedAtUtc))
            {
                throw new ArgumentException("Prior turns must be distinct, chronological, and from one conversation.");
            }
            if (!MatchesPair(FirstTurn, firstParticipantId, secondParticipantId) ||
                !MatchesPair(SecondTurn, firstParticipantId, secondParticipantId) ||
                FirstTurn.SpeakerId == SecondTurn.SpeakerId ||
                FirstTurn.RecipientId != SecondTurn.SpeakerId ||
                SecondTurn.RecipientId != FirstTurn.SpeakerId)
            {
                throw new ArgumentException("Prior turns must form one reciprocal two-person exchange.");
            }

            FirstParticipantId = firstParticipantId;
            SecondParticipantId = secondParticipantId;
        }

        public IndividualId FirstParticipantId { get; }
        public IndividualId SecondParticipantId { get; }
        public ConversationId ConversationId => FirstTurn.ConversationId;
        public PriorConversationTurn FirstTurn { get; }
        public PriorConversationTurn SecondTurn { get; }
        public IReadOnlyList<EventId> EventIds =>
            new[] { FirstTurn.EventId, SecondTurn.EventId };

        public bool ContainsParticipants(IndividualId first, IndividualId second) =>
            (FirstParticipantId == first && SecondParticipantId == second) ||
            (FirstParticipantId == second && SecondParticipantId == first);

        private static bool MatchesPair(
            PriorConversationTurn turn,
            IndividualId first,
            IndividualId second) =>
            (turn.SpeakerId == first && turn.RecipientId == second) ||
            (turn.SpeakerId == second && turn.RecipientId == first);
    }

    /// <summary>
    /// Selects only a completed reciprocal two-turn exchange. One-sided speech,
    /// unrelated participants, unwitnessed turns, malformed duplication, and
    /// future turns fail closed.
    /// </summary>
    public sealed class CrossEncounterConversationSelector
    {
        public PriorConversationContext? SelectLatestCompletedExchange(
            IEnumerable<PriorConversationTurn> candidates,
            IndividualId firstParticipantId,
            IndividualId secondParticipantId,
            long throughTick)
        {
            if (candidates is null) throw new ArgumentNullException(nameof(candidates));
            if (firstParticipantId.Value == Guid.Empty ||
                secondParticipantId.Value == Guid.Empty ||
                firstParticipantId == secondParticipantId)
            {
                throw new ArgumentException("Selection requires two distinct participants.");
            }
            if (throughTick < 0) throw new ArgumentOutOfRangeException(nameof(throughTick));

            var completed = new List<PriorConversationContext>();
            foreach (var group in candidates
                .Select(value => value ?? throw new ArgumentException(
                    "Prior conversation candidates cannot contain null values.",
                    nameof(candidates)))
                .Where(value => value.DisplayedAtTick <= throughTick)
                .Where(value => value.AudienceIds.Contains(firstParticipantId) &&
                                value.AudienceIds.Contains(secondParticipantId))
                .Where(value => MatchesPair(value, firstParticipantId, secondParticipantId))
                .GroupBy(value => value.ConversationId))
            {
                var turns = group
                    .GroupBy(value => value.EventId)
                    .Select(value => value.First())
                    .OrderBy(value => value.DisplayedAtTick)
                    .ThenBy(value => value.DisplayedAtUtc)
                    .ThenBy(value => value.EventId.ToString(), StringComparer.Ordinal)
                    .ToArray();
                if (turns.Length != 2 || turns[0].SpeakerId == turns[1].SpeakerId ||
                    turns[0].RecipientId != turns[1].SpeakerId ||
                    turns[1].RecipientId != turns[0].SpeakerId)
                {
                    continue;
                }

                completed.Add(new PriorConversationContext(
                    firstParticipantId,
                    secondParticipantId,
                    turns[0],
                    turns[1]));
            }

            return completed
                .OrderByDescending(value => value.SecondTurn.DisplayedAtTick)
                .ThenByDescending(value => value.SecondTurn.DisplayedAtUtc)
                .ThenBy(value => value.ConversationId.ToString(), StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static bool MatchesPair(
            PriorConversationTurn turn,
            IndividualId first,
            IndividualId second) =>
            (turn.SpeakerId == first && turn.RecipientId == second) ||
            (turn.SpeakerId == second && turn.RecipientId == first);
    }

    public sealed class GroundedCrossEncounterOpeningPlan
    {
        public GroundedCrossEncounterOpeningPlan(
            string text,
            IEnumerable<EventId> evidenceIds,
            PriorConversationContext? priorConversation)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 600)
                throw new ArgumentException("Cross-encounter opening text must be bounded.", nameof(text));
            Text = text;
            EvidenceIds = new ReadOnlyCollection<EventId>(
                (evidenceIds ?? throw new ArgumentNullException(nameof(evidenceIds))).ToList());
            if (EvidenceIds.Count == 0 || EvidenceIds.Count > 5 ||
                EvidenceIds.Any(value => value.Value == Guid.Empty) ||
                EvidenceIds.Distinct().Count() != EvidenceIds.Count)
            {
                throw new ArgumentException(
                    "A cross-encounter opening requires one to five unique evidence IDs.",
                    nameof(evidenceIds));
            }
            PriorConversation = priorConversation;
        }

        public string Text { get; }
        public IReadOnlyList<EventId> EvidenceIds { get; }
        public PriorConversationContext? PriorConversation { get; }
    }

    /// <summary>
    /// Adds a bounded reminder of one verified prior exchange to an already
    /// grounded opening. Quoted prior speech remains attributed speech, not an
    /// assertion that its semantic content is independently true.
    /// </summary>
    public sealed class GroundedCrossEncounterDialogueComposer
    {
        public const int MaximumTextLength = 600;
        public const int MaximumQuotedCharactersPerTurn = 96;

        public GroundedCrossEncounterOpeningPlan Compose(
            IndividualId currentSpeakerId,
            IndividualId recipientId,
            GroundedRelationshipDialoguePlan currentPlan,
            PriorConversationContext? priorConversation)
        {
            if (currentSpeakerId.Value == Guid.Empty ||
                recipientId.Value == Guid.Empty ||
                currentSpeakerId == recipientId)
            {
                throw new ArgumentException("Cross-encounter composition requires two distinct participants.");
            }
            if (currentPlan is null) throw new ArgumentNullException(nameof(currentPlan));
            if (priorConversation is null)
            {
                return new GroundedCrossEncounterOpeningPlan(
                    currentPlan.Text,
                    currentPlan.EvidenceIds,
                    null);
            }
            if (!priorConversation.ContainsParticipants(currentSpeakerId, recipientId))
            {
                throw new ArgumentException(
                    "Prior conversation participants do not match the current pair.",
                    nameof(priorConversation));
            }

            var continuity = " When we last spoke, "
                + Describe(priorConversation.FirstTurn, currentSpeakerId)
                + ", and then "
                + Describe(priorConversation.SecondTurn, currentSpeakerId)
                + ".";
            var evidence = currentPlan.EvidenceIds
                .Concat(priorConversation.EventIds)
                .Distinct()
                .ToArray();
            return new GroundedCrossEncounterOpeningPlan(
                Bound(currentPlan.Text + continuity),
                evidence,
                priorConversation);
        }

        private static string Describe(
            PriorConversationTurn turn,
            IndividualId currentSpeakerId) =>
            (turn.SpeakerId == currentSpeakerId ? "I said \"" : "you said \"")
            + Quote(turn.Text)
            + "\"";

        private static string Quote(string value)
        {
            var normalized = string.Join(
                " ",
                value.Replace('"', '\'')
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (normalized.Length <= MaximumQuotedCharactersPerTurn) return normalized;
            return normalized.Substring(0, MaximumQuotedCharactersPerTurn - 3).TrimEnd() + "...";
        }

        private static string Bound(string value)
        {
            if (value.Length <= MaximumTextLength) return value;
            return value.Substring(0, MaximumTextLength - 4).TrimEnd() + "...";
        }
    }
}
