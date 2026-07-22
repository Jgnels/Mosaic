using System;
using System.Collections.Generic;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;

namespace Dagmay.Core.Relationships
{
    public sealed class RelationshipTarget
    {
        public RelationshipTarget(EnvironmentEntityReference entity, IndividualId? individualId)
        {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            IndividualId = individualId;
        }

        public EnvironmentEntityReference Entity { get; }
        public IndividualId? IndividualId { get; }
    }

    public sealed class RelationshipDimensions
    {
        public RelationshipDimensions(
            double trust,
            double affection,
            double fear,
            double resentment,
            double familiarity)
        {
            Trust = Signed(trust, nameof(trust));
            Affection = Signed(affection, nameof(affection));
            Fear = ContractGuard.UnitInterval(fear, nameof(fear));
            Resentment = ContractGuard.UnitInterval(resentment, nameof(resentment));
            Familiarity = ContractGuard.UnitInterval(familiarity, nameof(familiarity));
        }

        public double Trust { get; }
        public double Affection { get; }
        public double Fear { get; }
        public double Resentment { get; }
        public double Familiarity { get; }

        public static RelationshipDimensions Neutral { get; } = new RelationshipDimensions(0, 0, 0, 0, 0);

        private static double Signed(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < -1 || value > 1)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and between -1 and 1.");
            }

            return value;
        }
    }

    public sealed class RelationshipRecord
    {
        public RelationshipRecord(
            IndividualId ownerId,
            RelationshipTarget other,
            RelationshipDimensions dimensions,
            double confidence,
            IEnumerable<EventId> evidenceEventIds,
            long lastUpdatedStateVersion)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            if (other.IndividualId.HasValue && ownerId == other.IndividualId.Value)
            {
                throw new ArgumentException("A relationship must refer to another person.", nameof(other));
            }
            if (lastUpdatedStateVersion < 0) throw new ArgumentOutOfRangeException(nameof(lastUpdatedStateVersion));

            OwnerId = ownerId;
            Other = other;
            Dimensions = dimensions ?? throw new ArgumentNullException(nameof(dimensions));
            Confidence = ContractGuard.UnitInterval(confidence, nameof(confidence));
            EvidenceEventIds = ContractGuard.List(evidenceEventIds, nameof(evidenceEventIds));
            LastUpdatedStateVersion = lastUpdatedStateVersion;
        }

        public IndividualId OwnerId { get; }
        public RelationshipTarget Other { get; }
        public RelationshipDimensions Dimensions { get; }
        public double Confidence { get; }
        public IReadOnlyList<EventId> EvidenceEventIds { get; }
        public long LastUpdatedStateVersion { get; }
    }
}
