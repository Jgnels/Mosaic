using System;
using System.Collections.Generic;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Beliefs
{
    public enum EvidenceDomain
    {
        Personal = 0,
        Group = 1,
        Faction = 2,
        World = 3
    }

    public sealed class TemporalFact
    {
        public TemporalFact(
            FactId id,
            IndividualId perspectiveOwnerId,
            string subjectKey,
            string predicate,
            string value,
            long validFromTick,
            long learnedAtTick,
            double confidence,
            EvidenceDomain domain,
            IEnumerable<EventId> sourceEvidenceIds)
        {
            if (validFromTick < 0) throw new ArgumentOutOfRangeException(nameof(validFromTick));
            if (learnedAtTick < 0) throw new ArgumentOutOfRangeException(nameof(learnedAtTick));

            Id = id;
            PerspectiveOwnerId = perspectiveOwnerId;
            SubjectKey = ContractGuard.Text(subjectKey, nameof(subjectKey), 256);
            Predicate = ContractGuard.Text(predicate, nameof(predicate), 128);
            Value = ContractGuard.Text(value, nameof(value), 2048);
            ValidFromTick = validFromTick;
            LearnedAtTick = learnedAtTick;
            Confidence = ContractGuard.UnitInterval(confidence, nameof(confidence));
            Domain = domain;

            var sources = ContractGuard.List(sourceEvidenceIds, nameof(sourceEvidenceIds));
            if (sources.Count == 0) throw new ArgumentException("At least one source evidence ID is required.", nameof(sourceEvidenceIds));
            SourceEvidenceIds = sources;
        }

        public FactId Id { get; }
        public IndividualId PerspectiveOwnerId { get; }
        public string SubjectKey { get; }
        public string Predicate { get; }
        public string Value { get; }
        public long ValidFromTick { get; }
        public long LearnedAtTick { get; }
        public double Confidence { get; }
        public EvidenceDomain Domain { get; }
        public IReadOnlyList<EventId> SourceEvidenceIds { get; }
        public long? ValidUntilTick { get; private set; }
        public FactId? SupersededByFactId { get; private set; }

        public bool IsCurrent => !ValidUntilTick.HasValue && !SupersededByFactId.HasValue;

        public void Supersede(long validUntilTick, FactId replacementFactId)
        {
            if (!IsCurrent) throw new InvalidOperationException("Temporal fact is already closed or superseded.");
            if (validUntilTick < ValidFromTick) throw new ArgumentOutOfRangeException(nameof(validUntilTick));

            ValidUntilTick = validUntilTick;
            SupersededByFactId = replacementFactId;
        }
    }
}
