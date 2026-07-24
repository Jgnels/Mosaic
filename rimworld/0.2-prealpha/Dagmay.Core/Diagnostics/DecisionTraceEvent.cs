using System;
using System.Collections.Generic;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Diagnostics
{
    public enum DecisionTraceStage
    {
        EvidenceAdmission = 0,
        Retrieval = 1,
        Appraisal = 2,
        RelationshipUpdate = 3,
        GoalScoring = 4,
        Planning = 5,
        ExecutionGate = 6,
        Outcome = 7
    }

    public sealed class DecisionTraceEvent
    {
        public DecisionTraceEvent(
            long tick,
            IndividualId individualId,
            DecisionTraceStage stage,
            string decisionId,
            string summary,
            IEnumerable<EventId> evidenceIds,
            string? previousState = null,
            string? newState = null,
            double? score = null)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            if (score.HasValue && (double.IsNaN(score.Value) || double.IsInfinity(score.Value)))
                throw new ArgumentOutOfRangeException(nameof(score));

            Tick = tick;
            IndividualId = individualId;
            Stage = stage;
            DecisionId = ContractGuard.Text(decisionId, nameof(decisionId), 128);
            Summary = ContractGuard.Text(summary, nameof(summary), 1024);
            EvidenceIds = ContractGuard.List(evidenceIds, nameof(evidenceIds));
            PreviousState = previousState;
            NewState = newState;
            Score = score;
        }

        public long Tick { get; }
        public IndividualId IndividualId { get; }
        public DecisionTraceStage Stage { get; }
        public string DecisionId { get; }
        public string Summary { get; }
        public IReadOnlyList<EventId> EvidenceIds { get; }
        public string? PreviousState { get; }
        public string? NewState { get; }
        public double? Score { get; }
    }
}
