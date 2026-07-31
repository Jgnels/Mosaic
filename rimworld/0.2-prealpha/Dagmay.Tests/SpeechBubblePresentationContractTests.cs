using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Identity;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;
using Dagmay.Core.Scheduling;
using Dagmay.RimWorld.Dialogue;

namespace Dagmay.Tests
{
    internal static class SpeechBubblePresentationContractTests
    {
        public static void QueueBoundsAndEvictionAreDeterministic()
        {
            var firstSpeaker = IndividualId.New();
            var secondSpeaker = IndividualId.New();
            var firstActive = Row(firstSpeaker, "first active", DialoguePriority.Normal);
            var secondActive = Row(secondSpeaker, "second active", DialoguePriority.Normal);
            var low = Row(firstSpeaker, "low", DialoguePriority.Background);
            var normal = Row(firstSpeaker, "normal", DialoguePriority.Normal);
            var urgent = Row(secondSpeaker, "urgent", DialoguePriority.Urgent);
            var controller = new DialogueSpeechBubbleController(2, 2);
            controller.Activate();

            TestAssert.Equal(DialoguePresentationEnqueueStatus.Activated, controller.Enqueue(firstActive, 0).Status,
                "The first speaker row should activate.");
            TestAssert.Equal(DialoguePresentationEnqueueStatus.Activated, controller.Enqueue(secondActive, 0).Status,
                "A different speaker must have an independent active slot.");
            controller.Enqueue(low, 0);
            controller.Enqueue(normal, 0);
            var result = controller.Enqueue(urgent, 0);

            TestAssert.Equal(DialoguePresentationEnqueueStatus.QueuedAfterEviction, result.Status,
                "An urgent row should evict the deterministic global worst.");
            TestAssert.Equal(low.UtteranceId, result.EvictedUtteranceId!.Value,
                "The oldest low-priority row is the deterministic eviction target.");
            TestAssert.Equal(2, controller.QueuedCount, "The global queued bound must remain exact.");
            controller.Dismiss(firstSpeaker, firstActive.UtteranceId, 1);
            controller.Dismiss(secondSpeaker, secondActive.UtteranceId, 1);
            var active = controller.ActiveSnapshot();
            TestAssert.True(active.Any(value => value.Row.UtteranceId == normal.UtteranceId),
                "The surviving first-speaker row should activate.");
            TestAssert.True(active.Any(value => value.Row.UtteranceId == urgent.UtteranceId),
                "The urgent second-speaker row should activate.");
        }

        public static void PerSpeakerBoundCannotBeBrokenByGlobalEviction()
        {
            var speaker = IndividualId.New();
            var other = IndividualId.New();
            var controller = new DialogueSpeechBubbleController(1, 2);
            controller.Activate();
            controller.Enqueue(Row(speaker, "active", DialoguePriority.Normal), 0);
            var replaced = Row(speaker, "replace me", DialoguePriority.Background);
            controller.Enqueue(replaced, 0);
            controller.Enqueue(Row(other, "other active", DialoguePriority.Normal), 0);
            controller.Enqueue(Row(other, "other queued", DialoguePriority.Background), 0);
            var winning = Row(speaker, "winner", DialoguePriority.Urgent);

            var result = controller.Enqueue(winning, 0);
            TestAssert.Equal(DialoguePresentationEnqueueStatus.QueuedAfterEviction, result.Status,
                "The stronger row should replace within its own full speaker queue.");
            TestAssert.Equal(replaced.UtteranceId, result.EvictedUtteranceId!.Value,
                "A full per-speaker queue must evict from that same speaker.");
            TestAssert.Equal(2, controller.QueuedCount, "Neither queue bound may grow during replacement.");
        }

        public static void DuplicateUtterancesCannotDisplayTwice()
        {
            var controller = new DialogueSpeechBubbleController();
            controller.Activate();
            var row = Row(IndividualId.New(), "once", DialoguePriority.Normal);
            TestAssert.Equal(DialoguePresentationEnqueueStatus.Activated, controller.Enqueue(row, 0).Status,
                "The first utterance should activate.");
            TestAssert.Equal(DialoguePresentationEnqueueStatus.RejectedDuplicate, controller.Enqueue(row, 1).Status,
                "The same UtteranceId must be rejected while active.");
            controller.Dismiss(row.SpeakerId, row.UtteranceId, 2);
            TestAssert.Equal(DialoguePresentationEnqueueStatus.RejectedDuplicate, controller.Enqueue(row, 3).Status,
                "A displayed/dismissed UtteranceId must remain deduplicated.");
        }

        public static void IdentityNotDisplayNameControlsIsolationAndRename()
        {
            var first = IndividualId.New();
            var second = IndividualId.New();
            var controller = new DialogueSpeechBubbleController();
            controller.Activate();
            var firstRow = Row(first, "first", DialoguePriority.Normal, "Alex");
            var collision = Row(second, "second", DialoguePriority.Normal, "Alex");
            var renamed = Row(first, "renamed", DialoguePriority.Normal, "Taylor");
            controller.Enqueue(firstRow, 0);
            controller.Enqueue(collision, 0);
            controller.Enqueue(renamed, 0);

            TestAssert.Equal(2, controller.ActiveSnapshot().Count,
                "Equal display names must not merge different IndividualIds.");
            controller.Dismiss(first, firstRow.UtteranceId, 1);
            var replacement = controller.ActiveSnapshot().Single(value => value.Row.SpeakerId == first);
            TestAssert.Equal("Taylor", replacement.Row.DisplayLabel,
                "A rename changes the non-authoritative label without changing the speaker identity.");
        }

        public static void UnicodeWrappingIsBoundedAndTextElementSafe()
        {
            var samples = new[]
            {
                "普通话对白没有空格也必须安全换行",
                "🙂🚀🙂🚀🙂🚀🙂🚀",
                "e\u0301e\u0301e\u0301e\u0301",
                "English العربية עברית mixed"
            };
            foreach (var sample in samples)
            {
                var wrapped = DialogueBubbleText.Wrap(sample, 3, 2);
                var lines = wrapped.Split('\n');
                TestAssert.True(lines.Length <= 2, "Wrapping must honor the maximum line count.");
                TestAssert.True(lines.All(line => new StringInfo(line).LengthInTextElements <= 3),
                    "Wrapping must count Unicode text elements rather than UTF-16 code units.");
                TestAssert.False(wrapped.Contains("\uFFFD"),
                    "Wrapping must not introduce a replacement character by splitting Unicode.");
            }

            var longText = new string('x', 1000);
            var bounded = DialogueBubbleText.Wrap(longText);
            TestAssert.True(new StringInfo(bounded.Replace("\n", string.Empty)).LengthInTextElements <= 160,
                "Default wrapping must cap displayed text to four forty-element lines.");
            TestAssert.True(bounded.EndsWith("\u2026", StringComparison.Ordinal),
                "Truncated display text must end with a single Unicode ellipsis.");
            TestAssert.Equal(DialoguePresentationEnqueueStatus.RejectedEmpty,
                ActivatedController().Enqueue(Row(IndividualId.New(), "   ", DialoguePriority.Normal), 0).Status,
                "Whitespace-only presentation must be rejected.");
        }

        public static void TimingAndScreenClampHonorBounds()
        {
            TestAssert.Equal(3500, DialogueBubbleTiming.DurationMilliseconds(string.Empty),
                "Empty timing should use the 3.5-second base.");
            TestAssert.Equal(3535, DialogueBubbleTiming.DurationMilliseconds("🙂"),
                "An emoji should count as one displayed character.");
            TestAssert.Equal(9000, DialogueBubbleTiming.DurationMilliseconds(new string('x', 1000)),
                "Long text timing must clamp to nine seconds.");

            var clamped = new DialogueScreenRect(-100, 900, 500, 300).Clamp(320, 200);
            TestAssert.Equal(4f, clamped.X, "The rectangle must clamp to the left margin.");
            TestAssert.Equal(4f, clamped.Y, "An oversized rectangle must clamp to the top margin.");
            TestAssert.Equal(312f, clamped.Width, "Oversized width must fit inside screen margins.");
            TestAssert.Equal(192f, clamped.Height, "Oversized height must fit inside screen margins.");
        }

        public static void LifecycleAndDisposalAreIdempotentAndSilent()
        {
            var controller = new DialogueSpeechBubbleController();
            var activated = 0;
            var dismissed = 0;
            controller.BubbleActivated += _ => activated++;
            controller.BubbleDismissed += _ => dismissed++;
            controller.Activate();
            controller.Activate();
            var row = Row(IndividualId.New(), "hello", DialoguePriority.Normal);
            controller.Enqueue(row, 0);
            controller.Deactivate();
            controller.Deactivate();
            TestAssert.Equal(1, activated, "Idempotent activation must not duplicate callbacks.");
            TestAssert.Equal(1, dismissed, "Idempotent deactivation must dismiss exactly once.");
            controller.Dispose();
            controller.Dispose();
            TestAssert.Equal(DialoguePresentationEnqueueStatus.Disposed, controller.Enqueue(
                Row(IndividualId.New(), "ignored", DialoguePriority.Normal), 1).Status,
                "A disposed controller must ignore later work.");
            TestAssert.Equal(1, activated, "Disposed subscriptions must never receive callbacks.");
        }

        public static void AvailabilityFailsClosedForThreadBindingAndMapLoss()
        {
            TestAssert.Equal(RimWorldDialoguePresentationAvailability.WrongThread,
                RimWorldDialoguePresentationPolicy.Evaluate(false, 1, 2, true, true, true, true),
                "Rendering on a non-main thread must fail closed.");
            TestAssert.Equal(RimWorldDialoguePresentationAvailability.MissingBinding,
                RimWorldDialoguePresentationPolicy.Evaluate(false, 1, 1, false, false, false, false),
                "An absent pawn binding must fail closed.");
            TestAssert.Equal(RimWorldDialoguePresentationAvailability.PawnDespawned,
                RimWorldDialoguePresentationPolicy.Evaluate(false, 1, 1, true, false, true, true),
                "A despawned pawn must not receive a bubble.");
            TestAssert.Equal(RimWorldDialoguePresentationAvailability.MapUnavailable,
                RimWorldDialoguePresentationPolicy.Evaluate(false, 1, 1, true, true, false, false),
                "Map loss must dismiss presentation safely.");
            TestAssert.Equal(RimWorldDialoguePresentationAvailability.PawnOnDifferentMap,
                RimWorldDialoguePresentationPolicy.Evaluate(false, 1, 1, true, true, true, false),
                "A pawn on a different map must not receive an off-map bubble.");
            TestAssert.Equal(RimWorldDialoguePresentationAvailability.Disposed,
                RimWorldDialoguePresentationPolicy.Evaluate(true, 1, 2, false, false, false, false),
                "Disposal must dominate all other availability states.");
        }

        public static void PresenterBindsAtTrustedRuntimeLifecycleNotConstruction()
        {
            RimWorldRuntimeThreadBinding? binding = null;
            var constructionThreadId = 0;
            var constructionThread = new Thread(() =>
            {
                constructionThreadId = Thread.CurrentThread.ManagedThreadId;
                binding = new RimWorldRuntimeThreadBinding();
            });
            constructionThread.Start();
            constructionThread.Join();

            TestAssert.True(binding is not null,
                "The presenter thread binding fixture must be constructed on loader thread A.");
            TestAssert.False(binding!.IsBound,
                "Construction on loader thread A must not establish presentation affinity.");
            TestAssert.Throws<InvalidOperationException>(
                () => binding.EnsureCurrentThread(constructionThreadId),
                "An ordinary call from the construction thread cannot bind the presenter accidentally.");

            var runtimeThreadId = Thread.CurrentThread.ManagedThreadId;
            TestAssert.True(runtimeThreadId != constructionThreadId,
                "The fixture must use a distinct trusted runtime thread B.");
            binding.BindFromTrustedGameComponentLifecycle(runtimeThreadId);
            binding.EnsureCurrentThread(runtimeThreadId);
            binding.EnsureCurrentThread(runtimeThreadId);
            TestAssert.Equal(runtimeThreadId, binding.EstablishedThreadId,
                "Later presenter calls on trusted runtime thread B must retain the same affinity.");

            Exception? thirdThreadFailure = null;
            var thirdThread = new Thread(() =>
            {
                try
                {
                    binding.EnsureCurrentThread(Thread.CurrentThread.ManagedThreadId);
                }
                catch (Exception exception)
                {
                    thirdThreadFailure = exception;
                }
            });
            thirdThread.Start();
            thirdThread.Join();
            TestAssert.True(thirdThreadFailure is InvalidOperationException,
                "Any third thread must fail the established main-thread boundary.");
        }

        public static void TickAndGuiShareOneEstablishedRuntimeThread()
        {
            var binding = new RimWorldRuntimeThreadBinding();
            var runtimeThreadId = Thread.CurrentThread.ManagedThreadId;

            binding.BindFromTrustedGameComponentLifecycle(runtimeThreadId);
            binding.EnsureCurrentThread(runtimeThreadId);
            binding.BindFromTrustedGameComponentLifecycle(runtimeThreadId);
            binding.EnsureCurrentThread(runtimeThreadId);

            TestAssert.Equal(runtimeThreadId, binding.EstablishedThreadId,
                "Trusted tick and GUI lifecycle entries must share one established runtime thread.");
            TestAssert.Throws<InvalidOperationException>(
                () => binding.BindFromTrustedGameComponentLifecycle(runtimeThreadId + 1000),
                "A GUI or tick callback on another thread must not replace established affinity.");
        }

        public static void ConcurrentFirstUseCannotBindTwoRuntimeThreads()
        {
            var binding = new RimWorldRuntimeThreadBinding();
            using var ready = new CountdownEvent(2);
            using var start = new ManualResetEventSlim(false);
            var successes = 0;
            var failures = 0;

            ThreadStart contender = () =>
            {
                ready.Signal();
                start.Wait();
                var threadId = Thread.CurrentThread.ManagedThreadId;
                try
                {
                    binding.BindFromTrustedGameComponentLifecycle(threadId);
                    binding.EnsureCurrentThread(threadId);
                    Interlocked.Increment(ref successes);
                }
                catch (InvalidOperationException)
                {
                    Interlocked.Increment(ref failures);
                }
            };
            var first = new Thread(contender);
            var second = new Thread(contender);
            first.Start();
            second.Start();
            ready.Wait();
            start.Set();
            first.Join();
            second.Join();

            TestAssert.Equal(1, successes,
                "Atomic first-use binding must admit exactly one runtime thread.");
            TestAssert.Equal(1, failures,
                "The concurrent losing thread must fail rather than replace affinity.");
            TestAssert.True(binding.EstablishedThreadId > 0,
                "Concurrent binding must leave one stable trusted runtime thread.");
        }

        public static void RuntimeThreadBindingDisposalIsIdempotentAndSilent()
        {
            var runtimeThreadId = Thread.CurrentThread.ManagedThreadId;
            var binding = new RimWorldRuntimeThreadBinding(runtimeThreadId);
            binding.EnsureCurrentThread(runtimeThreadId);
            binding.Dispose();
            binding.Dispose();

            binding.BindFromTrustedGameComponentLifecycle(runtimeThreadId + 1000);
            binding.EnsureCurrentThread(runtimeThreadId + 1000);
            TestAssert.True(binding.IsDisposed,
                "Disposed presentation affinity must remain disposed through later lifecycle callbacks.");
            TestAssert.Equal(runtimeThreadId, binding.EstablishedThreadId,
                "Disposal must not rewrite the formerly established runtime identity.");
        }

        public static void ReceiptsReportOnlyTheActualSuccessfulChannel()
        {
            var fixture = ValidatedFixture();
            var bubble = new DialoguePresentationAttempt(true, true).CreateReceipt(
                fixture.Row, fixture.Utterance, 11, fixture.Utc,
                DialogueDisclosure.WitnessesOnly, fixture.Audience);
            TestAssert.Equal(DialoguePresentationChannel.Bubble, bubble!.Channel,
                "A successful bubble remains the actual channel even when mirrored.");

            var fallback = new DialoguePresentationAttempt(false, true).CreateReceipt(
                fixture.Row, fixture.Utterance, 11, fixture.Utc,
                DialogueDisclosure.WitnessesOnly, fixture.Audience);
            TestAssert.Equal(DialoguePresentationChannel.PlayLog, fallback!.Channel,
                "Play log is the actual channel only when the bubble fails.");

            var failed = new DialoguePresentationAttempt(false, false).CreateReceipt(
                fixture.Row, fixture.Utterance, 11, fixture.Utc,
                DialogueDisclosure.WitnessesOnly, fixture.Audience);
            TestAssert.True(failed is null, "No successful presentation means no factual display receipt.");
        }

        public static void PresentationReadsPreserveCanonicalFingerprint()
        {
            var now = new DateTimeOffset(2026, 7, 26, 8, 0, 0, TimeSpan.Zero);
            var individual = IndividualState.Create("Presentation fixture", new IdentitySeed(
                "rimworld", "0.2-prealpha",
                new[] { new SeedFact(SeedFactCategory.Trait, "role", "Colonist", "fixture", 1.0) }));
            var archive = new IdentityArchiveSnapshot(
                Guid.Parse("39dd3cea-bcb2-467e-99bf-4360afe48dd9"),
                1,
                now,
                new[] { new PersistedIdentityRecord("Thing_Purity", individual) });
            var binding = new EnvironmentBinding(
                individual.Id, EnvironmentBindingState.Bound, "Thing_Purity", 1);
            var ledger = new InMemoryEventLedger();
            var memory = new MemoryIndex();
            var queue = new PersistentReflectionQueue(4);
            var before = CanonicalStateFingerprint.Compute(
                archive, binding, ledger.Snapshot(), memory.Snapshot(), queue.Snapshot());

            var controller = ActivatedController();
            var row = Row(individual.Id, "A presentation-only line.", DialoguePriority.Normal);
            controller.Enqueue(row, 0);
            _ = controller.ActiveSnapshot();
            controller.Tick(100);
            controller.Dismiss(individual.Id, row.UtteranceId, 101);
            _ = DialogueBubbleText.Wrap(row.Text);
            _ = new DialogueScreenRect(-1, -1, 100, 50).Clamp(1920, 1080);

            var after = CanonicalStateFingerprint.Compute(
                archive, binding, ledger.Snapshot(), memory.Snapshot(), queue.Snapshot());
            TestAssert.Equal(before, after,
                "Presentation queues, formatting, and reads must not mutate canonical identity or cognition.");
        }

        private static DialogueSpeechBubbleController ActivatedController()
        {
            var controller = new DialogueSpeechBubbleController();
            controller.Activate();
            return controller;
        }

        private static DialoguePresentationRow Row(
            IndividualId speaker,
            string text,
            DialoguePriority priority,
            string label = "Speaker",
            IndividualId? recipient = null,
            UtteranceId? utteranceId = null,
            ConversationId? conversationId = null)
        {
            return new DialoguePresentationRow(
                EventId.New(),
                utteranceId ?? UtteranceId.New(),
                conversationId ?? ConversationId.New(),
                speaker,
                recipient,
                label,
                text,
                priority);
        }

        private static (DialoguePresentationRow Row, ValidatedUtterance Utterance,
            IndividualId[] Audience, DateTimeOffset Utc) ValidatedFixture()
        {
            var speaker = IndividualId.New();
            var recipient = IndividualId.New();
            var source = EventId.New();
            var conversation = ConversationId.New();
            var request = new DialogueRequest(
                DialogueRequestId.New(),
                conversation,
                DialogueTriggerKind.Social,
                DialoguePriority.Normal,
                speaker,
                speaker,
                recipient,
                new DialogueSceneSnapshot(
                    source,
                    10,
                    "Offline presentation fixture.",
                    new[]
                    {
                        new DialogueParticipantSnapshot(speaker, "Speaker", "colonist", true, true),
                        new DialogueParticipantSnapshot(recipient, "Recipient", "colonist", true, true)
                    }),
                new[] { source },
                10,
                100,
                "presentation-fixture");
            var proposal = new UtteranceProposal(
                UtteranceId.New(),
                request.Id,
                conversation,
                speaker,
                recipient,
                "A grounded line.",
                new[] { source },
                10);
            var validation = new UtteranceValidator().Validate(
                request,
                proposal,
                10,
                new HashSet<UtteranceId>(),
                new UtteranceValidationPolicy(true, 4000));
            TestAssert.True(validation.IsAccepted, "The presentation fixture must validate.");
            return (
                new DialoguePresentationRow(
                    EventId.New(),
                    proposal.Id,
                    conversation,
                    speaker,
                    recipient,
                    "Speaker",
                    proposal.Text,
                    DialoguePriority.Normal),
                validation.Utterance!,
                new[] { speaker, recipient },
                new DateTimeOffset(2026, 7, 26, 8, 0, 0, TimeSpan.Zero));
        }
    }
}
