using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Persistence;
using Dagmay.RimWorld.Dialogue;

namespace Dagmay.Tests
{
    internal static class OfflineRimWorldDialoguePathContractTests
    {
        public static void CaptureRequiresTwoStableBindingsAndSupportedSocialFact()
        {
            var fixture = TriggerFixture();
            TestAssert.True(
                RimWorldSocialDialogueCapture.TryCreate(
                    fixture.Source,
                    100,
                    fixture.Utc,
                    RimWorldSocialDialogueCapture.OpinionChanged,
                    fixture.Payload,
                    fixture.Speaker,
                    null) is null,
                "An absent recipient identity binding must suppress dialogue capture.");
            TestAssert.True(
                RimWorldSocialDialogueCapture.TryCreate(
                    fixture.Source,
                    100,
                    fixture.Utc,
                    "rimworld.health.condition_added",
                    fixture.Payload,
                    fixture.Speaker,
                    fixture.Recipient) is null,
                "A non-social observation must not enter the first dialogue path.");
            var captured = RimWorldSocialDialogueCapture.TryCreate(
                fixture.Source,
                100,
                fixture.Utc,
                RimWorldSocialDialogueCapture.OpinionChanged,
                fixture.Payload,
                fixture.Speaker,
                fixture.Recipient);
            TestAssert.True(captured is not null, "A supported social fact with two bindings should capture.");
            TestAssert.Equal(fixture.Source, captured!.SourceEventId,
                "The immutable capture must preserve the exact supporting EventId.");
        }

        public static void FakeOnlyPipelineIsDeterministicStrictAndDuplicateSafe()
        {
            var trigger = TriggerFixture().Create();
            var first = new OfflineRimWorldDialoguePipeline()
                .PrepareAsync(trigger, 101, CancellationToken.None).GetAwaiter().GetResult();
            var reproduced = new OfflineRimWorldDialoguePipeline()
                .PrepareAsync(trigger, 101, CancellationToken.None).GetAwaiter().GetResult();
            TestAssert.True(first.IsPrepared && reproduced.IsPrepared,
                "The deterministic fake-provider path should prepare a valid utterance.");
            TestAssert.Equal(first.Prepared!.PresentationRow.UtteranceId,
                reproduced.Prepared!.PresentationRow.UtteranceId,
                "The same source EventId must derive the same UtteranceId.");
            TestAssert.Equal(first.Prepared.PresentationRow.Text,
                reproduced.Prepared.PresentationRow.Text,
                "The fake-provider utterance must reproduce exactly.");
            TestAssert.Equal(trigger.SourceEventId,
                first.Prepared.Request.SourceEventIds.Single(),
                "Strict preparation must cite only the captured source EventId.");

            var duplicate = new OfflineRimWorldDialoguePipeline();
            TestAssert.True(
                duplicate.PrepareAsync(trigger, 101, CancellationToken.None).GetAwaiter().GetResult().IsPrepared,
                "The first preparation should succeed.");
            TestAssert.Equal(
                OfflineRimWorldDialoguePreparationStatus.Duplicate,
                duplicate.PrepareAsync(trigger, 101, CancellationToken.None).GetAwaiter().GetResult().Status,
                "The same deterministic trigger cannot prepare twice in one pipeline.");
        }

        public static void TimeoutCancellationAndDespawnFailClosed()
        {
            var trigger = TriggerFixture().Create();
            var pipeline = new OfflineRimWorldDialoguePipeline();
            TestAssert.Equal(
                OfflineRimWorldDialoguePreparationStatus.Expired,
                pipeline.PrepareAsync(
                    trigger,
                    trigger.ObservedAtTick + OfflineRimWorldDialoguePipeline.RequestLifetimeTicks,
                    CancellationToken.None).GetAwaiter().GetResult().Status,
                "A trigger at its expiry tick must fail before provider dispatch.");

            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                TestAssert.Equal(
                    OfflineRimWorldDialoguePreparationStatus.ProviderFailure,
                    new OfflineRimWorldDialoguePipeline().PrepareAsync(
                        trigger,
                        trigger.ObservedAtTick + 1,
                        canceled.Token).GetAwaiter().GetResult().Status,
                    "Cancellation must normalize to a fail-closed fake-provider result.");
            }

            TestAssert.Equal(
                RimWorldDialoguePresentationAvailability.PawnDespawned,
                RimWorldDialoguePresentationPolicy.Evaluate(
                    false, 1, 1, true, false, true, true),
                "A prepared utterance cannot display after its pawn despawns.");
        }

        public static void DisplayFailureCreatesNoReceiptOrOutbox()
        {
            var prepared = Prepare();
            var attempt = new DialoguePresentationAttempt(false, false);
            var receipt = attempt.CreateReceipt(
                prepared.PresentationRow,
                prepared.Utterance,
                102,
                TriggerFixture().Utc,
                DialogueDisclosure.WitnessesOnly,
                prepared.AudienceIds);
            TestAssert.True(receipt is null,
                "Failure of both actual channels must create no factual receipt.");

            var directory = NewDirectory();
            try
            {
                var outbox = Path.Combine(directory, "dialogue.outbox");
                TestAssert.False(File.Exists(outbox),
                    "Without a receipt the admission bridge must never create an outbox.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        public static void ActualPresentationQueuesOutboxAndAdmitsExactlyOnce()
        {
            var prepared = Prepare();
            var receipt = new DialoguePresentationAttempt(true, true).CreateReceipt(
                prepared.PresentationRow,
                prepared.Utterance,
                102,
                TriggerFixture().Utc,
                DialogueDisclosure.WitnessesOnly,
                prepared.AudienceIds)!;
            var admission = new DialogueEventAdmissionService("rimworld").Prepare(
                prepared.Request,
                receipt);
            TestAssert.True(admission.IsPrepared, "An actual bubble receipt should prepare factual admission.");

            var directory = NewDirectory();
            try
            {
                var outbox = Path.Combine(directory, "dialogue.outbox");
                var journal = Path.Combine(directory, "experience.journal");
                var ledger = new InMemoryEventLedger();
                var binding = Binding(1);
                var coordinator = new DialogueAdmissionRecoveryCoordinator();
                var queued = coordinator.EnqueuePending(
                    outbox,
                    binding,
                    admission.Plan!,
                    true,
                    TriggerFixture().Utc);
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.Queued, queued.Status,
                    "Presentation must first persist only a checkpoint-bound outbox entry.");
                TestAssert.Equal(0, ledger.Snapshot().Count,
                    "Outbox enqueue before the RimWorld save checkpoint must not mutate the ledger.");
                TestAssert.False(File.Exists(journal),
                    "Outbox enqueue before the RimWorld save checkpoint must not create a journal.");
                var restartedInspection = new DialogueAdmissionRecoveryCoordinator()
                    .InspectPending(outbox, binding);
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.Queued, restartedInspection.Status,
                    "A restart must rediscover pending dialogue work without materializing it early.");

                var first = coordinator.Recover(
                    outbox,
                    journal,
                    binding,
                    ledger,
                    true,
                    TriggerFixture().Utc);
                var replay = coordinator.EnqueueAndRecover(
                    outbox,
                    journal,
                    binding,
                    admission.Plan!,
                    ledger,
                    true,
                    TriggerFixture().Utc.AddSeconds(1));

                TestAssert.Equal(DialogueAdmissionRecoveryStatus.Completed, first.Status,
                    "The actual display should complete checkpoint-bound recovery.");
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.NoWork, replay.Status,
                    "Replaying the same displayed utterance must be idempotent.");
                TestAssert.Equal(1, ledger.Snapshot().Count,
                    "The factual ledger must contain the utterance exactly once.");
                var loaded = new DurableExperienceJournal().Load(journal);
                TestAssert.Equal(1, loaded.Records.Count,
                    "The factual journal must contain the utterance exactly once.");
                TestAssert.True(loaded.Records[0].Perception is null &&
                                loaded.Records[0].Memory is null,
                    "The offline RimWorld path admits no automatic subjective cognition.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        public static void StaleCheckpointCannotRecoverForwardOutbox()
        {
            var prepared = Prepare();
            var receipt = new DialoguePresentationAttempt(false, true).CreateReceipt(
                prepared.PresentationRow,
                prepared.Utterance,
                102,
                TriggerFixture().Utc,
                DialogueDisclosure.WitnessesOnly,
                prepared.AudienceIds)!;
            var plan = new DialogueEventAdmissionService("rimworld")
                .Prepare(prepared.Request, receipt).Plan!;
            var directory = NewDirectory();
            try
            {
                var outbox = Path.Combine(directory, "dialogue.outbox");
                var journal = Path.Combine(directory, "experience.journal");
                var ledger = new InMemoryEventLedger();
                var coordinator = new DialogueAdmissionRecoveryCoordinator();
                coordinator.EnqueueAndRecover(
                    outbox, journal, Binding(2), plan, ledger, true, TriggerFixture().Utc);
                var stale = coordinator.Recover(
                    outbox, journal, Binding(1), ledger, true, TriggerFixture().Utc);
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.OutboxAheadOfSave, stale.Status,
                    "A loaded save behind the outbox checkpoint must fail closed.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static OfflineRimWorldPreparedDialogue Prepare()
        {
            var result = new OfflineRimWorldDialoguePipeline()
                .PrepareAsync(TriggerFixture().Create(), 101, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            TestAssert.True(result.IsPrepared, "The offline fixture should prepare.");
            return result.Prepared!;
        }

        private static DialogueAdmissionBinding Binding(long generation) =>
            new DialogueAdmissionBinding(
                Guid.Parse("60000000-0000-0000-0000-000000000001"),
                "offline-save-lineage",
                "offline-world",
                generation);

        private static string NewDirectory()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "mosaic-rimworld-dialogue-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static TriggerData TriggerFixture()
        {
            var speaker = new RimWorldDialogueIdentitySnapshot(
                "Thing_Speaker",
                IndividualId.Parse("10000000000000000000000000000001"),
                new LineageId(Guid.Parse("20000000-0000-0000-0000-000000000001")),
                4,
                "Speaker",
                AffectVector.Neutral);
            var recipient = new RimWorldDialogueIdentitySnapshot(
                "Thing_Recipient",
                IndividualId.Parse("10000000000000000000000000000002"),
                new LineageId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
                5,
                "Recipient",
                AffectVector.Neutral);
            return new TriggerData(
                EventId.Parse("30000000000000000000000000000001"),
                new DateTimeOffset(2026, 7, 26, 10, 0, 0, TimeSpan.Zero),
                speaker,
                recipient,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target_external_id"] = "Thing_Recipient",
                    ["opinion_before"] = "0",
                    ["opinion_after"] = "-20",
                    ["opinion_delta"] = "-20"
                });
        }

        private sealed class TriggerData
        {
            public TriggerData(
                EventId source,
                DateTimeOffset utc,
                RimWorldDialogueIdentitySnapshot speaker,
                RimWorldDialogueIdentitySnapshot recipient,
                IDictionary<string, string> payload)
            {
                Source = source;
                Utc = utc;
                Speaker = speaker;
                Recipient = recipient;
                Payload = payload;
            }

            public EventId Source { get; }
            public DateTimeOffset Utc { get; }
            public RimWorldDialogueIdentitySnapshot Speaker { get; }
            public RimWorldDialogueIdentitySnapshot Recipient { get; }
            public IDictionary<string, string> Payload { get; }

            public RimWorldSocialDialogueTrigger Create() =>
                RimWorldSocialDialogueCapture.TryCreate(
                    Source,
                    100,
                    Utc,
                    RimWorldSocialDialogueCapture.OpinionChanged,
                    Payload,
                    Speaker,
                    Recipient)!;
        }
    }
}
