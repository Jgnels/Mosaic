using System;
using System.Collections.Generic;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Story
{
    public enum StoryContributionKind
    {
        Pawn = 0,
        Colony = 1,
        Faction = 2,
        PlayerDirected = 3,
        ExternalSystem = 4
    }

    public sealed class StoryContribution
    {
        public StoryContribution(
            StoryContributionKind kind,
            string contributorKey,
            double amount,
            string? sourceModId = null)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0d)
                throw new ArgumentOutOfRangeException(nameof(amount));

            Kind = kind;
            ContributorKey = ContractGuard.Text(contributorKey, nameof(contributorKey), 256);
            Amount = amount;
            SourceModId = string.IsNullOrWhiteSpace(sourceModId)
                ? null
                : ContractGuard.Text(sourceModId, nameof(sourceModId), 256);
        }

        public StoryContributionKind Kind { get; }
        public string ContributorKey { get; }
        public double Amount { get; }
        public string? SourceModId { get; }
    }

    /// <summary>
    /// Immutable progress snapshot for a long-running story thread.
    /// Intended for milestone recording, not per-tick autobiographical storage.
    /// </summary>
    public sealed class StoryEventProgressSnapshot
    {
        public StoryEventProgressSnapshot(
            string storyEventId,
            long observedAtTick,
            double progress,
            IEnumerable<StoryContribution> contributions)
        {
            if (observedAtTick < 0) throw new ArgumentOutOfRangeException(nameof(observedAtTick));

            StoryEventId = ContractGuard.Text(storyEventId, nameof(storyEventId), 256);
            ObservedAtTick = observedAtTick;
            Progress = ContractGuard.UnitInterval(progress, nameof(progress));
            Contributions = ContractGuard.List(contributions, nameof(contributions));
        }

        public string StoryEventId { get; }
        public long ObservedAtTick { get; }
        public double Progress { get; }
        public IReadOnlyList<StoryContribution> Contributions { get; }
    }

    public interface IStoryMilestonePolicy
    {
        bool ShouldRecord(double previousProgress, double currentProgress, StoryEventPhase phase);
    }

    /// <summary>
    /// Deterministic milestone policy that prevents high-frequency world/project
    /// bookkeeping from flooding autobiographical memory.
    /// </summary>
    public sealed class ThresholdStoryMilestonePolicy : IStoryMilestonePolicy
    {
        private readonly double _step;

        public ThresholdStoryMilestonePolicy(double step = 0.25d)
        {
            if (double.IsNaN(step) || double.IsInfinity(step) || step <= 0d || step > 1d)
                throw new ArgumentOutOfRangeException(nameof(step));

            _step = step;
        }

        public bool ShouldRecord(double previousProgress, double currentProgress, StoryEventPhase phase)
        {
            previousProgress = ContractGuard.UnitInterval(previousProgress, nameof(previousProgress));
            currentProgress = ContractGuard.UnitInterval(currentProgress, nameof(currentProgress));

            if (phase == StoryEventPhase.Completed ||
                phase == StoryEventPhase.Failed ||
                phase == StoryEventPhase.Cancelled ||
                phase == StoryEventPhase.Expired)
                return true;

            if (currentProgress <= previousProgress)
                return false;

            var previousBucket = (int)Math.Floor(previousProgress / _step);
            var currentBucket = (int)Math.Floor(currentProgress / _step);

            return currentBucket > previousBucket || currentProgress >= 1d;
        }
    }
}
