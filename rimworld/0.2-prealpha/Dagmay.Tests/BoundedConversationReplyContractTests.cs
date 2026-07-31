using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Memory;
using Dagmay.RimWorld.Dialogue;

namespace Dagmay.Tests
{
    internal static class BoundedConversationReplyContractTests
    {
        public static void ReplyUsesCurrentFactAndRecipientOwnedHistory()
        {
            var current = Opinion(
                "81000000000000000000000000000001",
                300,
                -30,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);
            var recipientPositive = Opinion(
                "81000000000000000000000000000002",
                100,
                25,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);

            var plan = new GroundedConversationReplyComposer().Compose(
                "Candice",
                current,
                new[] { recipientPositive });

            TestAssert.Equal(2, plan.EvidenceIds.Count,
                "The reply must cite the opening social fact and the selected recipient-owned history.");
            TestAssert.Equal(current.EventId, plan.EvidenceIds[0],
                "The current opening fact remains mandatory reply evidence.");
            TestAssert.True(plan.EvidenceIds.Contains(recipientPositive.EventId),
                "The selected reciprocal history must remain explicitly grounded.");
            TestAssert.True(
                plan.Text.IndexOf("your opinion of me has worsened", StringComparison.Ordinal) >= 0 &&
                plan.Text.IndexOf("my opinion of you improved", StringComparison.Ordinal) >= 0,
                "The reply must distinguish the opening speaker's view from the recipient's own history.");
        }

        public static void ReciprocalHistoryCanDisagreeAsymmetrically()
        {
            var currentNegative = Opinion(
                "81000000000000000000000000000003",
                300,
                -25,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);
            var recipientPositive = Opinion(
                "81000000000000000000000000000004",
                200,
                40,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);

            var plan = new GroundedConversationReplyComposer().Compose(
                "Candice",
                currentNegative,
                new[] { recipientPositive });

            TestAssert.True(
                plan.Text.IndexOf("worsened", StringComparison.Ordinal) >= 0 &&
                plan.Text.IndexOf("warmer", StringComparison.Ordinal) >= 0,
                "A bounded reply must preserve asymmetric perspectives rather than force agreement.");
        }

        public static void ReplyFiltersPrivateAndHearsayRecipientEvidence()
        {
            var current = Opinion(
                "81000000000000000000000000000005",
                300,
                20,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);
            var privateEvidence = Opinion(
                "81000000000000000000000000000006",
                100,
                -30,
                PrivacyClassification.Private,
                PerceptionChannel.Experienced);
            var hearsay = Opinion(
                "81000000000000000000000000000007",
                200,
                -30,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Told);

            var plan = new GroundedConversationReplyComposer().Compose(
                "Candice",
                current,
                new[] { privateEvidence, hearsay });

            TestAssert.Equal(1, plan.EvidenceIds.Count,
                "Rejected recipient context must not enter the reply evidence list.");
            TestAssert.Equal(0, plan.SelectedRecipientEvidence.Count,
                "Private and hearsay recipient evidence must fail closed.");
        }

        public static void PipelineReplyRequiresMatchingDisplayedOpening()
        {
            var fixture = CreateFixture();
            var opening = new OfflineRimWorldDialoguePipeline()
                .PrepareAsync(fixture.Trigger, 301, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            TestAssert.True(opening.IsPrepared,
                "The opening fixture must prepare before receipt-gated reply testing.");

            var receipt = Receipt(opening.Prepared!, 302, fixture.Utc);
            var mismatchedTrigger = CreateFixture(
                EventId.Parse("82000000000000000000000000000009")).Trigger;
            var result = new OfflineRimWorldDialoguePipeline()
                .PrepareReplyAsync(
                    mismatchedTrigger,
                    receipt,
                    303,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            TestAssert.Equal(
                OfflineRimWorldDialoguePreparationStatus.OpeningMismatch,
                result.Status,
                "A receipt from a different opening cannot release a reply.");
        }

        public static void PipelinePreparesOneOppositeReplyInSameConversation()
        {
            var fixture = CreateFixture();
            var pipeline = new OfflineRimWorldDialoguePipeline();
            var opening = pipeline
                .PrepareAsync(fixture.Trigger, 301, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            TestAssert.True(opening.IsPrepared,
                "The bounded exchange opening should prepare.");

            var receipt = Receipt(opening.Prepared!, 302, fixture.Utc);
            var reply = pipeline
                .PrepareReplyAsync(
                    fixture.Trigger,
                    receipt,
                    303,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            TestAssert.True(reply.IsPrepared,
                "A matching actual display receipt should release exactly one reply.");
            TestAssert.Equal(
                opening.Prepared!.Request.ConversationId,
                reply.Prepared!.Request.ConversationId,
                "Opening and reply must share one stable conversation ID.");
            TestAssert.Equal(
                DialogueTriggerKind.Reply,
                reply.Prepared.Request.TriggerKind,
                "The second turn must be explicitly classified as a reply.");
            TestAssert.Equal(
                fixture.Trigger.Recipient.IndividualId,
                reply.Prepared.Request.ExpectedSpeakerId,
                "The original recipient must become the reply speaker.");
            TestAssert.Equal(
                fixture.Trigger.Speaker.IndividualId,
                reply.Prepared.Request.RecipientId!.Value,
                "The original speaker must become the reply recipient.");
            TestAssert.True(
                reply.Prepared.Request.SourceEventIds.Contains(fixture.Trigger.SourceEventId),
                "The reply must remain grounded in the qualifying current social fact.");
        }

        public static void PipelineReplyIsDeterministicAndDuplicateSafe()
        {
            var fixture = CreateFixture();
            var firstPipeline = new OfflineRimWorldDialoguePipeline();
            var firstOpening = firstPipeline
                .PrepareAsync(fixture.Trigger, 301, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            var firstReceipt = Receipt(firstOpening.Prepared!, 302, fixture.Utc);
            var firstReply = firstPipeline
                .PrepareReplyAsync(
                    fixture.Trigger,
                    firstReceipt,
                    303,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            var reproducedPipeline = new OfflineRimWorldDialoguePipeline();
            var reproducedOpening = reproducedPipeline
                .PrepareAsync(fixture.Trigger, 301, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            var reproducedReceipt = Receipt(
                reproducedOpening.Prepared!,
                302,
                fixture.Utc);
            var reproducedReply = reproducedPipeline
                .PrepareReplyAsync(
                    fixture.Trigger,
                    reproducedReceipt,
                    303,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            TestAssert.Equal(
                firstReply.Prepared!.PresentationRow.UtteranceId,
                reproducedReply.Prepared!.PresentationRow.UtteranceId,
                "The same opening source must derive the same reply UtteranceId.");
            TestAssert.Equal(
                firstReply.Prepared.PresentationRow.Text,
                reproducedReply.Prepared.PresentationRow.Text,
                "The bounded fake reply must reproduce exactly.");
            TestAssert.Equal(
                OfflineRimWorldDialoguePreparationStatus.Duplicate,
                firstPipeline.PrepareReplyAsync(
                        fixture.Trigger,
                        firstReceipt,
                        304,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
                    .Status,
                "One opening receipt cannot prepare the same deterministic reply twice.");
        }

        private static DisplayedUtteranceReceipt Receipt(
            OfflineRimWorldPreparedDialogue prepared,
            long displayedAtTick,
            DateTimeOffset utc) =>
            new DisplayedUtteranceReceipt(
                prepared.Utterance,
                displayedAtTick,
                utc,
                DialogueDisclosure.WitnessesOnly,
                DialoguePresentationChannel.Bubble,
                prepared.AudienceIds);

        private static Fixture CreateFixture(EventId? sourceOverride = null)
        {
            var speaker = new RimWorldDialogueIdentitySnapshot(
                "Thing_Candice",
                IndividualId.Parse("82000000000000000000000000000001"),
                new LineageId(Guid.Parse("83000000-0000-0000-0000-000000000001")),
                4,
                "Candice",
                AffectVector.Neutral);
            var recipient = new RimWorldDialogueIdentitySnapshot(
                "Thing_Walter",
                IndividualId.Parse("82000000000000000000000000000002"),
                new LineageId(Guid.Parse("83000000-0000-0000-0000-000000000002")),
                5,
                "Walter",
                AffectVector.Neutral);
            var recipientHistory = Opinion(
                "84000000000000000000000000000001",
                200,
                30,
                PrivacyClassification.RelationshipSensitive,
                PerceptionChannel.Experienced);
            var source = sourceOverride ??
                EventId.Parse("84000000000000000000000000000002");
            var utc = new DateTimeOffset(
                2026,
                7,
                26,
                14,
                0,
                0,
                TimeSpan.Zero);
            var trigger = RimWorldSocialDialogueCapture.TryCreate(
                source,
                300,
                utc,
                RimWorldSocialDialogueCapture.OpinionChanged,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target_external_id"] = "Thing_Walter",
                    ["target_name"] = "Walter",
                    ["opinion_before"] = "10",
                    ["opinion_after"] = "-20",
                    ["opinion_delta"] = "-30"
                },
                speaker,
                recipient,
                Array.Empty<GroundedRelationshipEvidence>(),
                new[] { recipientHistory })!;
            return new Fixture(trigger, utc);
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
                    ["target_external_id"] = "Thing_Other",
                    ["target_name"] = "Other",
                    ["opinion_before"] = "0",
                    ["opinion_after"] = delta.ToString(CultureInfo.InvariantCulture),
                    ["opinion_delta"] = delta.ToString(CultureInfo.InvariantCulture)
                },
                channel,
                privacy,
                1.0);

        private sealed class Fixture
        {
            public Fixture(
                RimWorldSocialDialogueTrigger trigger,
                DateTimeOffset utc)
            {
                Trigger = trigger;
                Utc = utc;
            }

            public RimWorldSocialDialogueTrigger Trigger { get; }
            public DateTimeOffset Utc { get; }
        }
    }
}
