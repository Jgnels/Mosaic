using System;
using System.Collections.Generic;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Reflection
{
    public sealed class ReflectionProposal
    {
        public ReflectionProposal(
            RequestId requestId,
            IndividualId individualId,
            long baseStateVersion,
            IEnumerable<EventId> evidenceEventIds,
            double confidence,
            string interpretation,
            string autobiographicalReflection,
            string decisionSummary,
            AffectVector targetAffect)
        {
            if (baseStateVersion < 0) throw new ArgumentOutOfRangeException(nameof(baseStateVersion));

            SchemaVersion = SchemaVersions.ReflectionProposal;
            RequestId = requestId;
            IndividualId = individualId;
            BaseStateVersion = baseStateVersion;
            EvidenceEventIds = ContractGuard.List(evidenceEventIds, nameof(evidenceEventIds));
            if (EvidenceEventIds.Count == 0 || EvidenceEventIds.Count > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(evidenceEventIds));
            }

            Confidence = ContractGuard.UnitInterval(confidence, nameof(confidence));
            Interpretation = ContractGuard.Text(interpretation, nameof(interpretation), 2000);
            AutobiographicalReflection = ContractGuard.Text(
                autobiographicalReflection,
                nameof(autobiographicalReflection),
                2000);
            DecisionSummary = ContractGuard.Text(decisionSummary, nameof(decisionSummary), 2000);
            TargetAffect = targetAffect ?? throw new ArgumentNullException(nameof(targetAffect));
        }

        public int SchemaVersion { get; }
        public RequestId RequestId { get; }
        public IndividualId IndividualId { get; }
        public long BaseStateVersion { get; }
        public IReadOnlyList<EventId> EvidenceEventIds { get; }
        public double Confidence { get; }
        public string Interpretation { get; }
        public string AutobiographicalReflection { get; }
        public string DecisionSummary { get; }
        public AffectVector TargetAffect { get; }
    }
}
