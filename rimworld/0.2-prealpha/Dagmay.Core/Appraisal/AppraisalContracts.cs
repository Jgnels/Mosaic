using System;
using System.Collections.Generic;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Appraisal
{
    public sealed class AppraisalInput
    {
        public AppraisalInput(
            EventId evidenceId,
            IndividualId perspectiveOwnerId,
            string actorKey,
            string? targetKey,
            string eventKind,
            double desirability,
            double praiseworthiness,
            double liking,
            double goalRelevance,
            double goalLikelihood)
        {
            EvidenceId = evidenceId;
            PerspectiveOwnerId = perspectiveOwnerId;
            ActorKey = ContractGuard.Text(actorKey, nameof(actorKey), 256);
            TargetKey = string.IsNullOrWhiteSpace(targetKey) ? null : ContractGuard.Text(targetKey, nameof(targetKey), 256);
            EventKind = ContractGuard.Text(eventKind, nameof(eventKind), 128);
            Desirability = SignedUnit(desirability, nameof(desirability));
            Praiseworthiness = SignedUnit(praiseworthiness, nameof(praiseworthiness));
            Liking = SignedUnit(liking, nameof(liking));
            GoalRelevance = ContractGuard.UnitInterval(goalRelevance, nameof(goalRelevance));
            GoalLikelihood = ContractGuard.UnitInterval(goalLikelihood, nameof(goalLikelihood));
        }

        public EventId EvidenceId { get; }
        public IndividualId PerspectiveOwnerId { get; }
        public string ActorKey { get; }
        public string? TargetKey { get; }
        public string EventKind { get; }
        public double Desirability { get; }
        public double Praiseworthiness { get; }
        public double Liking { get; }
        public double GoalRelevance { get; }
        public double GoalLikelihood { get; }

        private static double SignedUnit(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < -1d || value > 1d)
                throw new ArgumentOutOfRangeException(parameterName, "Value must be between -1 and 1.");
            return value;
        }
    }

    public sealed class AffectCandidate
    {
        public AffectCandidate(string kind, double intensity, string explanation)
        {
            Kind = ContractGuard.Text(kind, nameof(kind), 64);
            Intensity = ContractGuard.UnitInterval(intensity, nameof(intensity));
            Explanation = ContractGuard.Text(explanation, nameof(explanation), 512);
        }

        public string Kind { get; }
        public double Intensity { get; }
        public string Explanation { get; }
    }

    public sealed class AppraisalResult
    {
        public AppraisalResult(IEnumerable<AffectCandidate> candidates, double moodDelta)
        {
            Candidates = ContractGuard.List(candidates, nameof(candidates));
            if (double.IsNaN(moodDelta) || double.IsInfinity(moodDelta) || moodDelta < -1d || moodDelta > 1d)
                throw new ArgumentOutOfRangeException(nameof(moodDelta));
            MoodDelta = moodDelta;
        }

        public IReadOnlyList<AffectCandidate> Candidates { get; }
        public double MoodDelta { get; }
    }

    public interface IAppraisalEngine
    {
        AppraisalResult Appraise(AppraisalInput input);
    }
}
