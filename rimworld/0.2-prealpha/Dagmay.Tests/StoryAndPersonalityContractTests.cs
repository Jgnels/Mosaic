using Dagmay.Core.Beliefs;
using Dagmay.Core.Contracts;
using Dagmay.Core.Personality;
using Dagmay.Core.Story;

namespace Dagmay.Tests
{
    internal static class StoryAndPersonalityContractTests
    {
        public static void WorldStoryEventPreservesLifecycleAndPortableContext()
        {
            var pawn = IndividualId.New();
            var started = new StoryEventDescriptor("rimcities:quest:assassinate:42","rimcities.quest.assassinate",
                EvidenceDomain.World,StoryEventPhase.Started,StorySignificance.Major,1000,new[]{pawn},
                "Cabbage.RimCities","Faction_Empire","CityTile_431");
            var completed = new StoryEventDescriptor(started.StoryEventId,started.EventKind,started.Domain,
                StoryEventPhase.Completed,started.Significance,2000,started.ParticipantIds,started.SourceModId,
                started.FactionKey,started.LocationKey);
            TestAssert.False(started.IsTerminal,"Started world event must remain open.");
            TestAssert.True(completed.IsTerminal,"Completed world event must be terminal.");
            TestAssert.Equal(started.StoryEventId,completed.StoryEventId,"Lifecycle updates must retain story-event identity.");
            TestAssert.Equal("CityTile_431",completed.LocationKey!,"Portable location context must survive.");
        }

        public static void ExternalTraitInfluenceIsNotMosaicLearnedPersonality()
        {
            var influence = new BehaviorInfluence(BehaviorInfluenceKind.ExternalModTrait,"VTE_Coward",0.8,
                "VanillaExpanded.VanillaTraitsExpanded","External trait may force fleeing.");
            TestAssert.False(influence.IsMosaicLearned,"External trait behavior must not masquerade as learned personality.");
            TestAssert.Equal("VanillaExpanded.VanillaTraitsExpanded",influence.SourceModId!,"External trait provenance must remain explicit.");
        }

        public static void LearnedPreferenceIsDistinctFromGameTrait()
        {
            var learned = new BehaviorInfluence(BehaviorInfluenceKind.MosaicLearnedPreference,"social.forgiveness",-0.4,
                explanation:"Repeated betrayals reduced willingness to forgive.");
            TestAssert.True(learned.IsMosaicLearned,"History-derived preferences must be distinguishable from static traits.");
        }
    }
}
