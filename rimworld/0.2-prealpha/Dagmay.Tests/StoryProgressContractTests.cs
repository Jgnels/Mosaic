using Dagmay.Core.Story;

namespace Dagmay.Tests
{
    internal static class StoryProgressContractTests
    {
        public static void LongRunningProjectRecordsMilestonesInsteadOfEveryTick()
        {
            var policy = new ThresholdStoryMilestonePolicy(0.25);

            TestAssert.False(
                policy.ShouldRecord(0.10, 0.11, StoryEventPhase.Progressed),
                "Tiny progress changes must not flood autobiographical storage.");

            TestAssert.True(
                policy.ShouldRecord(0.24, 0.26, StoryEventPhase.Progressed),
                "Crossing a configured progress milestone must be recordable.");
        }

        public static void TerminalStoryOutcomeAlwaysRecords()
        {
            var policy = new ThresholdStoryMilestonePolicy(0.25);

            TestAssert.True(
                policy.ShouldRecord(0.51, 0.51, StoryEventPhase.Failed),
                "Failure must be recorded even if numeric progress did not change.");

            TestAssert.True(
                policy.ShouldRecord(0.99, 1.0, StoryEventPhase.Completed),
                "Completion must always be recordable.");
        }

        public static void StoryProgressCanAttributeAlliedFactionContribution()
        {
            var contribution = new StoryContribution(
                StoryContributionKind.Faction,
                "Faction_AlliedSettlement",
                125.0,
                sourceModId: "Mlie.RoadsOfTheRim");

            var snapshot = new StoryEventProgressSnapshot(
                "roads:home-to-ally:001",
                5000,
                0.75,
                new[] { contribution });

            TestAssert.Equal(
                StoryContributionKind.Faction,
                snapshot.Contributions[0].Kind,
                "Story progress must distinguish allied faction contribution from pawn authorship.");

            TestAssert.Equal(
                "Mlie.RoadsOfTheRim",
                snapshot.Contributions[0].SourceModId!,
                "External contribution provenance must remain explicit.");
        }
    }
}
