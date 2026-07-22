using System;
using System.Collections.Generic;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Memory
{
    public enum MemoryTier
    {
        Recent,
        SignificantLongTerm,
        CoreAutobiographical
    }

    public enum PrivacyClassification
    {
        Shareable,
        RelationshipSensitive,
        Private,
        ObserverOnly
    }

    public sealed class SubjectiveMemory
    {
        public SubjectiveMemory(
            MemoryId id,
            IndividualId ownerId,
            IEnumerable<PerceptionId> sourcePerceptionIds,
            DateTimeOffset occurredAtUtc,
            DateTimeOffset encodedAtUtc,
            string conciseDiaryEntry,
            string appraisal,
            AffectVector affectAtEncoding,
            double importance,
            double emotionalWeight,
            double confidence,
            double accessibility,
            MemoryTier tier,
            PrivacyClassification privacy,
            IEnumerable<IndividualId> peopleInvolved)
        {
            Id = id;
            SchemaVersion = SchemaVersions.SubjectiveMemory;
            OwnerId = ownerId;
            SourcePerceptionIds = ContractGuard.List(sourcePerceptionIds, nameof(sourcePerceptionIds));
            OccurredAtUtc = occurredAtUtc;
            EncodedAtUtc = encodedAtUtc;
            ConciseDiaryEntry = ContractGuard.Text(conciseDiaryEntry, nameof(conciseDiaryEntry), 2000);
            Appraisal = ContractGuard.Text(appraisal, nameof(appraisal), 2000);
            AffectAtEncoding = affectAtEncoding ?? throw new ArgumentNullException(nameof(affectAtEncoding));
            Importance = ContractGuard.UnitInterval(importance, nameof(importance));
            EmotionalWeight = ContractGuard.UnitInterval(emotionalWeight, nameof(emotionalWeight));
            Confidence = ContractGuard.UnitInterval(confidence, nameof(confidence));
            Accessibility = ContractGuard.UnitInterval(accessibility, nameof(accessibility));
            Tier = tier;
            Privacy = privacy;
            PeopleInvolved = ContractGuard.List(peopleInvolved, nameof(peopleInvolved));
        }

        public MemoryId Id { get; }
        public int SchemaVersion { get; }
        public IndividualId OwnerId { get; }
        public IReadOnlyList<PerceptionId> SourcePerceptionIds { get; }
        public DateTimeOffset OccurredAtUtc { get; }
        public DateTimeOffset EncodedAtUtc { get; }
        public string ConciseDiaryEntry { get; }
        public string Appraisal { get; }
        public AffectVector AffectAtEncoding { get; }
        public double Importance { get; }
        public double EmotionalWeight { get; }
        public double Confidence { get; }
        public double Accessibility { get; }
        public MemoryTier Tier { get; }
        public PrivacyClassification Privacy { get; }
        public IReadOnlyList<IndividualId> PeopleInvolved { get; }
    }
}

