using System;
using Dagmay.Core.Actions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Presentation;

namespace Dagmay.Tests
{
    internal static class ExecutionAndPresentationContractTests
    {
        public static void ExternalModExecutionIsNotMosaicAuthorship()
        {
            var attribution = new ActionAttribution(
                ActionOriginKind.ExternalModExecution,
                "PickUpAndHaul.HaulToInventory",
                sourceModId: "Mehni.PickUpAndHaul");

            TestAssert.False(attribution.WasAuthoredByMosaic,
                "A job supplied or transformed by another mod must not be represented as a Mosaic-authored personal goal.");
            TestAssert.Equal("Mehni.PickUpAndHaul", attribution.SourceModId!,
                "External execution provenance must remain explicit.");
        }

        public static void MosaicCommitmentRequiresDecisionProvenance()
        {
            TestAssert.Throws<ArgumentException>(
                () => new ActionAttribution(
                    ActionOriginKind.MosaicCommitment,
                    "social.repair_relationship"),
                "Mosaic-authored action attribution must require the originating Mosaic decision/goal ID.");

            var attribution = new ActionAttribution(
                ActionOriginKind.MosaicCommitment,
                "social.repair_relationship",
                mosaicDecisionId: "goal:repair:lynx:michael");

            TestAssert.True(attribution.WasAuthoredByMosaic,
                "Only explicitly attributed Mosaic commitments should count as Mosaic-authored actions.");
        }

        public static void PresentationContextRequiresEvidenceGrounding()
        {
            var evidence = EventId.New();
            var item = new CharacterContextItem(
                "relationship_reason",
                "Michael abandoned Lynx during the raid.",
                new[] { evidence },
                0.92);

            var packet = new CharacterContextPacket(
                IndividualId.New(),
                1234,
                new[] { item });

            TestAssert.Equal(evidence, packet.Items[0].EvidenceIds[0],
                "Presentation context must retain supporting evidence IDs.");
            TestAssert.Equal(1234L, packet.BuiltAtTick,
                "Presentation context may report the observation tick without mutating canonical state.");

            TestAssert.Throws<ArgumentException>(
                () => new CharacterContextItem(
                    "relationship_reason",
                    "Unsupported claim.",
                    Array.Empty<EventId>(),
                    0.5),
                "Factual presentation context must not contain unsupported claims.");
        }
    }
}
