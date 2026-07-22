using System;
using System.Collections.Generic;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Reflection
{
    public sealed class ProposedAffectMutation
    {
        public ProposedAffectMutation(
            RequestId requestId,
            IndividualId individualId,
            long baseStateVersion,
            AffectVector targetAffect,
            IEnumerable<EventId> evidenceEventIds,
            double confidence,
            string decisionSummary)
        {
            if (baseStateVersion < 0) throw new ArgumentOutOfRangeException(nameof(baseStateVersion));

            SchemaVersion = SchemaVersions.MutationProposal;
            RequestId = requestId;
            IndividualId = individualId;
            BaseStateVersion = baseStateVersion;
            TargetAffect = targetAffect ?? throw new ArgumentNullException(nameof(targetAffect));
            EvidenceEventIds = ContractGuard.List(evidenceEventIds, nameof(evidenceEventIds));
            Confidence = ContractGuard.UnitInterval(confidence, nameof(confidence));
            DecisionSummary = ContractGuard.Text(decisionSummary, nameof(decisionSummary), 2000);
        }

        public int SchemaVersion { get; }
        public RequestId RequestId { get; }
        public IndividualId IndividualId { get; }
        public long BaseStateVersion { get; }
        public AffectVector TargetAffect { get; }
        public IReadOnlyList<EventId> EvidenceEventIds { get; }
        public double Confidence { get; }
        public string DecisionSummary { get; }
    }
}

