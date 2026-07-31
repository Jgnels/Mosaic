using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Memory;
using Dagmay.RimWorld.Dialogue;

namespace Dagmay.Tests
{
    internal static class GroundedRelationshipDialogueContractTests
    {
        public static void NoHistoryUsesOnlyCurrentEvidenceAndSpecificFact()
        {
            var current = Opinion(
                "71000000000000000000000000000001",
                300,
                -30,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);
            var plan = new GroundedRelationshipDialogueComposer().Compose(
                "Walter",
                current,
                Array.Empty<GroundedRelationshipEvidence>());

            TestAssert.Equal(1, plan.EvidenceIds.Count,
                "A first grounded line must cite only its current factual event.");
            TestAssert.Equal(current.EventId, plan.EvidenceIds[0],
                "The current event must remain the first grounding ID.");
            TestAssert.True(
                plan.Text.IndexOf("my opinion of you has worsened", StringComparison.Ordinal) >= 0,
                "The deterministic line must state the observed opinion direction.");
        }

        public static void MixedHistoryRetainsPositiveAndNegativeEvidence()
        {
            var current = Opinion(
                "71000000000000000000000000000002",
                300,
                -25,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);
            var positive = Opinion(
                "71000000000000000000000000000003",
                100,
                35,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);
            var negative = Opinion(
                "71000000000000000000000000000004",
                200,
                -20,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);

            var plan = new GroundedRelationshipDialogueComposer().Compose(
                "Walter",
                current,
                new[] { negative, positive });

            TestAssert.Equal(3, plan.EvidenceIds.Count,
                "Mixed relationship history should retain the current event plus two bounded prior events.");
            TestAssert.True(plan.EvidenceIds.Contains(positive.EventId) &&
                            plan.EvidenceIds.Contains(negative.EventId),
                "Both positive and negative same-counterpart evidence must survive mixed-history selection.");
            TestAssert.True(
                plan.Text.IndexOf("conflicted", StringComparison.Ordinal) >= 0,
                "A mixed deterministic line may describe conflict but must not invent motives.");
        }

        public static void DirectRelationshipEvidenceOutranksNewerOpinionNoise()
        {
            var current = Opinion(
                "71000000000000000000000000000005",
                500,
                -25,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);
            var olderDirect = Direct(
                "71000000000000000000000000000006",
                100,
                "none",
                "Lover",
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);
            var newerOpinion = Opinion(
                "71000000000000000000000000000007",
                450,
                -16,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);

            var plan = new GroundedRelationshipDialogueComposer().Compose(
                "Walter",
                current,
                new[] { newerOpinion, olderDirect },
                maximumPriorEvidence: 1);

            TestAssert.Equal(1, plan.SelectedPriorEvidence.Count,
                "The one-item history budget must remain exact.");
            TestAssert.Equal(olderDirect.EventId, plan.SelectedPriorEvidence[0].EventId,
                "A meaningful direct relationship event should outrank newer low-impact opinion noise.");
        }

        public static void PrivateAndHearsayHistoryFailClosed()
        {
            var current = Opinion(
                "71000000000000000000000000000008",
                300,
                20,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);
            var privateEvidence = Opinion(
                "71000000000000000000000000000009",
                100,
                -40,
                PrivacyClassification.Private,
                PerceptionChannel.Experienced);
            var hearsay = Opinion(
                "7100000000000000000000000000000a",
                200,
                -40,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Told);

            var plan = new GroundedRelationshipDialogueComposer().Compose(
                "Walter",
                current,
                new[] { privateEvidence, hearsay });

            TestAssert.Equal(0, plan.SelectedPriorEvidence.Count,
                "Private and hearsay evidence must not enter the first grounded relationship-dialogue path.");
            TestAssert.Equal(1, plan.EvidenceIds.Count,
                "Rejected prior context must not appear in the exact evidence list.");
        }

        public static void OfflinePipelineCarriesSelectedEvidenceIntoValidatedUtterance()
        {
            var speaker = new RimWorldDialogueIdentitySnapshot(
                "Thing_Speaker",
                IndividualId.Parse("72000000000000000000000000000001"),
                new LineageId(Guid.Parse("73000000-0000-0000-0000-000000000001")),
                4,
                "Candice",
                AffectVector.Neutral);
            var recipient = new RimWorldDialogueIdentitySnapshot(
                "Thing_Recipient",
                IndividualId.Parse("72000000000000000000000000000002"),
                new LineageId(Guid.Parse("73000000-0000-0000-0000-000000000002")),
                5,
                "Walter",
                AffectVector.Neutral);
            var positive = Opinion(
                "74000000000000000000000000000001",
                100,
                30,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);
            var negative = Opinion(
                "74000000000000000000000000000002",
                200,
                -20,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);
            var source = EventId.Parse("74000000000000000000000000000003");
            var payload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target_external_id"] = "Thing_Recipient",
                ["target_name"] = "Walter",
                ["opinion_before"] = "10",
                ["opinion_after"] = "-20",
                ["opinion_delta"] = "-30"
            };
            var trigger = RimWorldSocialDialogueCapture.TryCreate(
                source,
                300,
                new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero),
                RimWorldSocialDialogueCapture.OpinionChanged,
                payload,
                speaker,
                recipient,
                new[] { positive, negative })!;

            var result = new OfflineRimWorldDialoguePipeline()
                .PrepareAsync(trigger, 301, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            TestAssert.True(result.IsPrepared,
                "The grounded fake-only RimWorld path should prepare a valid strict utterance.");
            TestAssert.Equal(3, result.Prepared!.Request.SourceEventIds.Count,
                "The request must carry current, positive, and negative exact evidence IDs.");
            TestAssert.True(result.Prepared.Request.SourceEventIds.Contains(source) &&
                            result.Prepared.Request.SourceEventIds.Contains(positive.EventId) &&
                            result.Prepared.Request.SourceEventIds.Contains(negative.EventId),
                "The strict request must contain exactly the selected grounding provenance.");
            TestAssert.True(
                result.Prepared.PresentationRow.Text.IndexOf("conflicted", StringComparison.Ordinal) >= 0,
                "The presented deterministic text should reflect the selected mixed history.");
        }

        private static GroundedRelationshipEvidence Opinion(
            string id,
            long tick,
            int delta,
            PrivacyClassification privacy,
            PerceptionChannel channel) =>
            new GroundedRelationshipEvidence(
                EventId.Parse(id),
                RimWorldSocialDialogueCapture.OpinionChanged,
                tick,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target_external_id"] = "Thing_Recipient",
                    ["target_name"] = "Walter",
                    ["opinion_before"] = "0",
                    ["opinion_after"] = delta.ToString(),
                    ["opinion_delta"] = delta.ToString()
                },
                channel,
                privacy,
                1.0);

        private static GroundedRelationshipEvidence Direct(
            string id,
            long tick,
            string before,
            string after,
            PrivacyClassification privacy,
            PerceptionChannel channel) =>
            new GroundedRelationshipEvidence(
                EventId.Parse(id),
                RimWorldSocialDialogueCapture.DirectRelationshipChanged,
                tick,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target_external_id"] = "Thing_Recipient",
                    ["target_name"] = "Walter",
                    ["relations_before"] = before,
                    ["relations_after"] = after
                },
                channel,
                privacy,
                1.0);
    }
}
