using System;
using System.Collections.Generic;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Beliefs
{
    public enum BeliefStatus
    {
        Active,
        Questioned,
        Superseded,
        Rejected
    }

    public enum EvidenceKind
    {
        EnvironmentEvent,
        PerceivedEvent,
        SubjectiveMemory,
        Testimony,
        Inference
    }

    public sealed class EvidenceReference
    {
        public EvidenceReference(EvidenceKind kind, string referenceId, bool supports, double weight)
        {
            Kind = kind;
            ReferenceId = ContractGuard.Text(referenceId, nameof(referenceId), 128);
            Supports = supports;
            Weight = ContractGuard.UnitInterval(weight, nameof(weight));
        }

        public EvidenceKind Kind { get; }
        public string ReferenceId { get; }
        public bool Supports { get; }
        public double Weight { get; }
    }

    public sealed class BeliefRecord
    {
        public BeliefRecord(
            BeliefId id,
            IndividualId ownerId,
            string proposition,
            double confidence,
            BeliefStatus status,
            IEnumerable<EvidenceReference> evidence,
            long createdAtStateVersion,
            long lastReviewedStateVersion)
        {
            if (createdAtStateVersion < 0) throw new ArgumentOutOfRangeException(nameof(createdAtStateVersion));
            if (lastReviewedStateVersion < createdAtStateVersion) throw new ArgumentOutOfRangeException(nameof(lastReviewedStateVersion));

            Id = id;
            SchemaVersion = SchemaVersions.Belief;
            OwnerId = ownerId;
            Proposition = ContractGuard.Text(proposition, nameof(proposition), 2000);
            Confidence = ContractGuard.UnitInterval(confidence, nameof(confidence));
            Status = status;
            Evidence = ContractGuard.List(evidence, nameof(evidence));
            CreatedAtStateVersion = createdAtStateVersion;
            LastReviewedStateVersion = lastReviewedStateVersion;
        }

        public BeliefId Id { get; }
        public int SchemaVersion { get; }
        public IndividualId OwnerId { get; }
        public string Proposition { get; }
        public double Confidence { get; }
        public BeliefStatus Status { get; }
        public IReadOnlyList<EvidenceReference> Evidence { get; }
        public long CreatedAtStateVersion { get; }
        public long LastReviewedStateVersion { get; }
    }
}

