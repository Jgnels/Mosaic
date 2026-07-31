using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Identity;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;

namespace Dagmay.Core.Relationships
{
    public sealed class DirectedRelationshipEvidence
    {
        public DirectedRelationshipEvidence(
            IndividualId ownerId,
            IndividualId otherId,
            EventId eventId,
            PerceptionId perceptionId,
            MemoryId memoryId,
            string eventKind,
            long occurredAtTick,
            long stateVersionAtPerception,
            PerceptionChannel channel,
            PrivacyClassification privacy,
            double valence,
            double confidence,
            bool superseded)
        {
            if (ownerId.Value == Guid.Empty || otherId.Value == Guid.Empty || ownerId == otherId)
                throw new ArgumentException("Directed evidence requires two distinct nonempty individuals.");
            if (eventId.Value == Guid.Empty || perceptionId.Value == Guid.Empty || memoryId.Value == Guid.Empty)
                throw new ArgumentException("Directed evidence requires exact nonempty provenance IDs.");
            if (string.IsNullOrWhiteSpace(eventKind) || eventKind.Length > 128)
                throw new ArgumentException("Event kind is required.", nameof(eventKind));
            if (occurredAtTick < 0) throw new ArgumentOutOfRangeException(nameof(occurredAtTick));
            if (stateVersionAtPerception < 0)
                throw new ArgumentOutOfRangeException(nameof(stateVersionAtPerception));
            if (!Enum.IsDefined(typeof(PerceptionChannel), channel))
                throw new ArgumentOutOfRangeException(nameof(channel));
            if (!Enum.IsDefined(typeof(PrivacyClassification), privacy))
                throw new ArgumentOutOfRangeException(nameof(privacy));
            if (double.IsNaN(valence) || double.IsInfinity(valence) || valence < -1 || valence > 1)
                throw new ArgumentOutOfRangeException(nameof(valence));
            if (double.IsNaN(confidence) || double.IsInfinity(confidence) || confidence < 0 || confidence > 1)
                throw new ArgumentOutOfRangeException(nameof(confidence));

            OwnerId = ownerId;
            OtherId = otherId;
            EventId = eventId;
            PerceptionId = perceptionId;
            MemoryId = memoryId;
            EventKind = eventKind;
            OccurredAtTick = occurredAtTick;
            StateVersionAtPerception = stateVersionAtPerception;
            Channel = channel;
            Privacy = privacy;
            Valence = valence;
            Confidence = confidence;
            Superseded = superseded;
        }

        public IndividualId OwnerId { get; }
        public IndividualId OtherId { get; }
        public EventId EventId { get; }
        public PerceptionId PerceptionId { get; }
        public MemoryId MemoryId { get; }
        public string EventKind { get; }
        public long OccurredAtTick { get; }
        public long StateVersionAtPerception { get; }
        public PerceptionChannel Channel { get; }
        public PrivacyClassification Privacy { get; }
        public double Valence { get; }
        public double Confidence { get; }
        public bool Superseded { get; }
        public bool IsHearsay => Channel == PerceptionChannel.Told ||
                                 Channel == PerceptionChannel.Inferred;
    }

    public sealed class RelationshipEvidenceProjectionResult
    {
        public RelationshipEvidenceProjectionResult(
            RelationshipRecord relationship,
            IReadOnlyList<DirectedRelationshipEvidence> evidence)
        {
            Relationship = relationship ?? throw new ArgumentNullException(nameof(relationship));
            Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        }

        public RelationshipRecord Relationship { get; }
        public IReadOnlyList<DirectedRelationshipEvidence> Evidence { get; }
    }

    public sealed class RelationshipEvidenceProjector
    {
        public const string ValenceKey = "relationship_valence";
        public const string SupersededKey = "relationship_evidence_superseded";
        public const int DefaultMaximumEvidence = 16;

        public IReadOnlyList<DirectedRelationshipEvidence> FromJournal(
            IndividualId ownerId,
            IndividualId otherId,
            IEnumerable<ExperienceJournalRecord> records)
        {
            if (records is null) throw new ArgumentNullException(nameof(records));
            var result = new List<DirectedRelationshipEvidence>();
            foreach (var record in records)
            {
                if (record is null || record.Perception is null || record.Memory is null) continue;
                var perception = record.Perception;
                var memory = record.Memory;
                var factual = record.FactualEvent;
                if (perception.PerceiverId != ownerId ||
                    memory.OwnerId != ownerId ||
                    perception.SourceEventId != factual.Id ||
                    !memory.SourcePerceptionIds.Contains(perception.Id) ||
                    !memory.PeopleInvolved.Contains(otherId) ||
                    !factual.FactualPayload.TryGetValue(ValenceKey, out var valenceText) ||
                    !double.TryParse(
                        valenceText,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var valence))
                {
                    continue;
                }
                var superseded = factual.FactualPayload.TryGetValue(
                    SupersededKey,
                    out var supersededText) &&
                    string.Equals(supersededText, "true", StringComparison.Ordinal);
                if (result.Any(value => value.EventId == factual.Id))
                    throw new InvalidDataException(
                        "Relationship evidence contains a duplicate EventId.");
                result.Add(new DirectedRelationshipEvidence(
                    ownerId,
                    otherId,
                    factual.Id,
                    perception.Id,
                    memory.Id,
                    factual.Kind,
                    factual.GameTick ?? 0,
                    perception.StateVersionAtPerception,
                    perception.Channel,
                    memory.Privacy,
                    valence,
                    Math.Min(perception.Confidence, memory.Confidence),
                    superseded));
            }

            return new ReadOnlyCollection<DirectedRelationshipEvidence>(
                result
                    .OrderByDescending(value => value.OccurredAtTick)
                    .ThenBy(value => value.EventId.ToString(), StringComparer.Ordinal)
                    .ToList());
        }

        public RelationshipEvidenceProjectionResult Build(
            IndividualId ownerId,
            IndividualId otherId,
            string otherExternalId,
            string otherDisplayName,
            IEnumerable<DirectedRelationshipEvidence> evidence,
            bool includePrivate,
            int maximumEvidence = DefaultMaximumEvidence)
        {
            if (maximumEvidence < 1 || maximumEvidence > 64)
                throw new ArgumentOutOfRangeException(nameof(maximumEvidence));
            var selected = (evidence ?? throw new ArgumentNullException(nameof(evidence)))
                .Where(value => value is not null &&
                                value.OwnerId == ownerId &&
                                value.OtherId == otherId &&
                                value.Privacy != PrivacyClassification.ObserverOnly &&
                                (includePrivate || value.Privacy != PrivacyClassification.Private))
                .OrderByDescending(value => value.OccurredAtTick)
                .ThenBy(value => value.EventId.ToString(), StringComparer.Ordinal)
                .Take(maximumEvidence)
                .ToList();
            if (selected.Count == 0)
                throw new InvalidOperationException("No directed relationship evidence is available.");

            var current = selected.Where(value => !value.Superseded).ToList();
            var weighted = current
                .Select(value => new
                {
                    Evidence = value,
                    Weight = value.Confidence * ChannelWeight(value.Channel)
                })
                .Where(value => value.Weight > 0)
                .ToList();
            var totalWeight = weighted.Sum(value => value.Weight);
            var score = totalWeight <= 0
                ? 0
                : weighted.Sum(value => value.Evidence.Valence * value.Weight) / totalWeight;
            score = ClampSigned(score);
            var confidence = current.Count == 0
                ? 0
                : Math.Min(1, totalWeight / current.Count);
            var dimensions = new RelationshipDimensions(
                score,
                ClampSigned(score * 0.8),
                Math.Max(0, -score) * 0.3,
                Math.Max(0, -score),
                Math.Min(1, selected.Count / 8d));
            var record = new RelationshipRecord(
                ownerId,
                new RelationshipTarget(
                    new EnvironmentEntityReference("rimworld", otherExternalId, otherDisplayName),
                    otherId),
                dimensions,
                confidence,
                selected.Select(value => value.EventId),
                selected.Max(value => value.StateVersionAtPerception));
            return new RelationshipEvidenceProjectionResult(
                record,
                new ReadOnlyCollection<DirectedRelationshipEvidence>(selected));
        }

        public DialogueContextItem CreateDialogueContext(
            RelationshipEvidenceProjectionResult projection,
            IndividualId recipientId)
        {
            if (projection is null) throw new ArgumentNullException(nameof(projection));
            var relationship = projection.Relationship;
            if (!relationship.Other.IndividualId.HasValue ||
                relationship.Other.IndividualId.Value != recipientId)
                throw new InvalidOperationException("Relationship context recipient mismatch.");
            if (projection.Evidence.Any(value =>
                value.Privacy == PrivacyClassification.Private ||
                value.Privacy == PrivacyClassification.ObserverOnly))
                throw new InvalidOperationException(
                    "Private or observer-only relationship evidence cannot enter dialogue context.");
            var text = string.Format(
                CultureInfo.InvariantCulture,
                "Directed relationship evidence: trust={0:0.000}; affection={1:0.000}; resentment={2:0.000}; confidence={3:0.000}.",
                relationship.Dimensions.Trust,
                relationship.Dimensions.Affection,
                relationship.Dimensions.Resentment,
                relationship.Confidence);
            return new DialogueContextItem(
                "directed-relationship-evidence",
                text,
                relationship.EvidenceEventIds,
                DialogueContextAudience.RelationshipSensitive,
                Math.Min(1, 0.5 + (relationship.Confidence / 2)),
                relationship.OwnerId,
                new[] { recipientId });
        }

        public IReadOnlyList<AppraisalInput> CreateAppraisalInputs(
            RelationshipEvidenceProjectionResult projection)
        {
            if (projection is null) throw new ArgumentNullException(nameof(projection));
            return projection.Evidence
                .Where(value => !value.Superseded)
                .Select(value => new AppraisalInput(
                    value.EventId,
                    value.OwnerId,
                    projection.Relationship.Other.Entity.ExternalId,
                    value.OtherId.ToString(),
                    value.EventKind,
                    value.Valence,
                    value.Valence,
                    value.Valence,
                    Math.Min(1, Math.Abs(value.Valence)),
                    value.Confidence))
                .ToArray();
        }

        private static double ChannelWeight(PerceptionChannel channel)
        {
            switch (channel)
            {
                case PerceptionChannel.Experienced:
                    return 1;
                case PerceptionChannel.Witnessed:
                    return 0.9;
                case PerceptionChannel.Told:
                    return 0.45;
                case PerceptionChannel.Inferred:
                    return 0.35;
                default:
                    return 0;
            }
        }

        private static double ClampSigned(double value) =>
            Math.Max(-1, Math.Min(1, value));
    }
}
