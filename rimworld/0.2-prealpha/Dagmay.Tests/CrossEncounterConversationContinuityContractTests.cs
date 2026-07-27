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
    internal static class CrossEncounterConversationContinuityContractTests
    {
        public static void SelectorRequiresOneCompleteReciprocalExchange()
        {
            var a = IndividualId.Parse("91000000000000000000000000000001");
            var b = IndividualId.Parse("91000000000000000000000000000002");
            var c = IndividualId.Parse("91000000000000000000000000000003");
            var incomplete = Conversation(
                "92000000000000000000000000000001",
                a,
                b,
                100,
                includeReply: false);
            var unrelated = Conversation(
                "92000000000000000000000000000002",
                a,
                c,
                200,
                includeReply: true);
            var complete = Conversation(
                "92000000000000000000000000000003",
                a,
                b,
                300,
                includeReply: true);

            var selected = new CrossEncounterConversationSelector()
                .SelectLatestCompletedExchange(
                    incomplete.Concat(unrelated).Concat(complete),
                    a,
                    b,
                    1000);

            TestAssert.True(selected is not null,
                "One completed reciprocal exchange should be selected.");
            TestAssert.Equal(complete[0].ConversationId, selected!.ConversationId,
                "One-sided and unrelated conversations must not qualify.");
            TestAssert.Equal(2, selected.EventIds.Count,
                "A qualifying prior exchange contains exactly two admitted turns.");
        }

        public static void SelectorChoosesLatestDeterministicallyAndRejectsFutureTurns()
        {
            var a = IndividualId.Parse("91000000000000000000000000000011");
            var b = IndividualId.Parse("91000000000000000000000000000012");
            var older = Conversation(
                "92000000000000000000000000000011",
                a,
                b,
                100,
                includeReply: true);
            var future = Conversation(
                "92000000000000000000000000000012",
                a,
                b,
                1000,
                includeReply: true);

            var selected = new CrossEncounterConversationSelector()
                .SelectLatestCompletedExchange(
                    future.Reverse().Concat(older.Reverse()),
                    a,
                    b,
                    500);

            TestAssert.True(selected is not null,
                "The earlier completed exchange should remain available.");
            TestAssert.Equal(older[0].ConversationId, selected!.ConversationId,
                "Future dialogue cannot leak backward into current context.");
        }

        public static void ComposerPreservesCurrentGroundingWhenNoHistoryExists()
        {
            var current = CurrentPlan();
            var speaker = IndividualId.Parse("91000000000000000000000000000021");
            var recipient = IndividualId.Parse("91000000000000000000000000000022");
            var result = new GroundedCrossEncounterDialogueComposer().Compose(
                speaker,
                recipient,
                current,
                null);

            TestAssert.Equal(current.Text, result.Text,
                "Without a completed prior exchange, PR #5 behavior must remain byte-for-byte unchanged.");
            TestAssert.Equal(current.EvidenceIds.Count, result.EvidenceIds.Count,
                "No synthetic continuity evidence may be invented.");
        }

        public static void ComposerAttributesPriorSpeechWithoutPromotingItToTruth()
        {
            var speaker = IndividualId.Parse("91000000000000000000000000000031");
            var recipient = IndividualId.Parse("91000000000000000000000000000032");
            var turns = Conversation(
                "92000000000000000000000000000031",
                speaker,
                recipient,
                100,
                includeReply: true);
            var context = new PriorConversationContext(
                speaker,
                recipient,
                turns[0],
                turns[1]);
            var result = new GroundedCrossEncounterDialogueComposer().Compose(
                speaker,
                recipient,
                CurrentPlan(),
                context);

            TestAssert.True(
                result.Text.IndexOf("When we last spoke", StringComparison.Ordinal) >= 0 &&
                result.Text.IndexOf("I said", StringComparison.Ordinal) >= 0 &&
                result.Text.IndexOf("you said", StringComparison.Ordinal) >= 0,
                "Prior synthetic text must remain explicitly attributed as earlier speech.");
            TestAssert.True(result.EvidenceIds.Contains(turns[0].EventId) &&
                            result.EvidenceIds.Contains(turns[1].EventId),
                "Both actual prior display events must remain exact provenance.");
        }

        public static void ComposerLabelsPerspectiveFromEitherSide()
        {
            var a = IndividualId.Parse("91000000000000000000000000000041");
            var b = IndividualId.Parse("91000000000000000000000000000042");
            var turns = Conversation(
                "92000000000000000000000000000041",
                a,
                b,
                100,
                includeReply: true);
            var context = new PriorConversationContext(a, b, turns[0], turns[1]);
            var result = new GroundedCrossEncounterDialogueComposer().Compose(
                b,
                a,
                CurrentPlan(),
                context);

            var firstYou = result.Text.IndexOf("you said", StringComparison.Ordinal);
            var laterI = result.Text.IndexOf("I said", StringComparison.Ordinal);
            TestAssert.True(firstYou >= 0 && laterI > firstYou,
                "Perspective labels must invert when the prior recipient becomes the current speaker.");
        }

        public static void PipelineCarriesPriorConversationEventsIntoOpeningOnly()
        {
            var speaker = new RimWorldDialogueIdentitySnapshot(
                "Thing_A",
                IndividualId.Parse("91000000000000000000000000000051"),
                new LineageId(Guid.Parse("93000000-0000-0000-0000-000000000051")),
                4,
                "Candice",
                AffectVector.Neutral);
            var recipient = new RimWorldDialogueIdentitySnapshot(
                "Thing_B",
                IndividualId.Parse("91000000000000000000000000000052"),
                new LineageId(Guid.Parse("93000000-0000-0000-0000-000000000052")),
                5,
                "Walter",
                AffectVector.Neutral);
            var turns = Conversation(
                "92000000000000000000000000000051",
                speaker.IndividualId,
                recipient.IndividualId,
                100,
                includeReply: true);
            var context = new PriorConversationContext(
                speaker.IndividualId,
                recipient.IndividualId,
                turns[0],
                turns[1]);
            var source = EventId.Parse("94000000000000000000000000000051");
            var trigger = RimWorldSocialDialogueCapture.TryCreate(
                source,
                300,
                new DateTimeOffset(2026, 7, 26, 16, 0, 0, TimeSpan.Zero),
                RimWorldSocialDialogueCapture.OpinionChanged,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target_external_id"] = "Thing_B",
                    ["target_name"] = "Walter",
                    ["opinion_before"] = "10",
                    ["opinion_after"] = "-20",
                    ["opinion_delta"] = "-30"
                },
                speaker,
                recipient,
                Array.Empty<GroundedRelationshipEvidence>(),
                Array.Empty<GroundedRelationshipEvidence>(),
                context)!;

            var result = new OfflineRimWorldDialoguePipeline()
                .PrepareAsync(trigger, 301, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            TestAssert.True(result.IsPrepared,
                "The cross-encounter opening should pass strict preparation.");
            TestAssert.Equal(3, result.Prepared!.Request.SourceEventIds.Count,
                "The request should contain current social fact plus two prior display events.");
            TestAssert.True(result.Prepared.Request.SourceEventIds.Contains(source) &&
                            result.Prepared.Request.SourceEventIds.Contains(turns[0].EventId) &&
                            result.Prepared.Request.SourceEventIds.Contains(turns[1].EventId),
                "Cross-encounter continuity must remain exactly provenance-bearing.");
            TestAssert.True(
                result.Prepared.PresentationRow.Text.IndexOf(
                    "When we last spoke",
                    StringComparison.Ordinal) >= 0,
                "The prepared opening should visibly acknowledge the prior exchange.");
        }

        private static GroundedRelationshipDialoguePlan CurrentPlan()
        {
            var current = new GroundedRelationshipEvidence(
                EventId.Parse("94000000000000000000000000000001"),
                RimWorldSocialDialogueCapture.OpinionChanged,
                300,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target_external_id"] = "Thing_Other",
                    ["target_name"] = "Other",
                    ["opinion_before"] = "10",
                    ["opinion_after"] = "-20",
                    ["opinion_delta"] = "-30"
                },
                PerceptionChannel.Experienced,
                PrivacyClassification.RelationshipSensitive,
                1.0);
            return new GroundedRelationshipDialogueComposer().Compose(
                "Walter",
                current,
                Array.Empty<GroundedRelationshipEvidence>());
        }

        private static IReadOnlyList<PriorConversationTurn> Conversation(
            string conversationId,
            IndividualId first,
            IndividualId second,
            long firstTick,
            bool includeReply)
        {
            var id = new ConversationId(Guid.ParseExact(conversationId, "N"));
            var audience = new[] { first, second };
            var turns = new List<PriorConversationTurn>
            {
                new PriorConversationTurn(
                    new EventId(DeriveEventGuid(conversationId, 1)),
                    id,
                    first,
                    second,
                    "I noticed the change between us.",
                    firstTick,
                    new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero),
                    DialoguePresentationChannel.Bubble,
                    audience)
            };
            if (includeReply)
            {
                turns.Add(new PriorConversationTurn(
                    new EventId(DeriveEventGuid(conversationId, 2)),
                    id,
                    second,
                    first,
                    "I heard you, and my own view is different.",
                    firstTick + 1,
                    new DateTimeOffset(2026, 7, 26, 12, 0, 1, TimeSpan.Zero),
                    DialoguePresentationChannel.Bubble,
                    audience));
            }
            return turns;
        }

        private static Guid DeriveEventGuid(string conversationId, int turn)
        {
            var bytes = Guid.ParseExact(conversationId, "N").ToByteArray();
            bytes[15] = (byte)(bytes[15] ^ turn);
            return new Guid(bytes);
        }
    }
}
