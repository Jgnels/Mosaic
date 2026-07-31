using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;

namespace Dagmay.Tests
{
    internal static class DialogueAdmissionOutboxContractTests
    {
        public static void CrashBeforeOutboxWriteLeavesNoAdmission()
        {
            using (var fixture = new Fixture(DialogueAdmissionRecoveryStep.BeforeOutboxWrite))
            {
                TestAssert.Throws<IOException>(
                    () => fixture.Enqueue(),
                    "Injected crash before outbox write must escape.");
                TestAssert.False(File.Exists(fixture.OutboxPath), "No outbox may exist before its write.");
                fixture.AssertDestinationCounts(0, 0);
            }
        }

        public static void CrashAfterOutboxBeforeDestinationsLeavesRecoverablePending()
        {
            using (var fixture = new Fixture(DialogueAdmissionRecoveryStep.AfterOutboxWrite))
            {
                TestAssert.Throws<IOException>(() => fixture.Enqueue(), "Injected crash should interrupt after outbox persistence.");
                TestAssert.True(File.Exists(fixture.OutboxPath), "Pending outbox must be durable before destination writes.");
                fixture.AssertDestinationCounts(0, 0);
                TestAssert.Equal(
                    DialogueOutboxEntryState.Pending,
                    fixture.LoadSnapshot().Entries.Single().State,
                    "Persisted entry must remain pending.");
            }
        }

        public static void RecoveryAfterLedgerOnlyMaterializesJournalExactlyOnce()
        {
            using (var fixture = new Fixture(DialogueAdmissionRecoveryStep.AfterLedgerWrite))
            {
                TestAssert.Throws<IOException>(() => fixture.Enqueue(), "Injected crash should leave ledger-only state.");
                fixture.AssertDestinationCounts(1, 0);
                var result = fixture.Recover();
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.Completed, result.Status, "Recovery must finish ledger-only state.");
                fixture.AssertDestinationCounts(1, 1);
            }
        }

        public static void RecoveryAfterJournalOnlyMaterializesLedgerExactlyOnce()
        {
            using (var fixture = new Fixture())
            {
                fixture.SavePending();
                fixture.Journal.Append(fixture.JournalPath, fixture.Plan.JournalRecord, 0, string.Empty);
                fixture.AssertDestinationCounts(0, 1);
                var result = fixture.Recover();
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.Completed, result.Status, "Recovery must finish journal-only state.");
                fixture.AssertDestinationCounts(1, 1);
            }
        }

        public static void RecoveryAfterBothBeforeCompletionMarksTombstoneWithoutDuplicates()
        {
            using (var fixture = new Fixture(DialogueAdmissionRecoveryStep.AfterDestinationsBeforeCompletion))
            {
                TestAssert.Throws<IOException>(() => fixture.Enqueue(), "Injected crash should leave both destinations before completion.");
                fixture.AssertDestinationCounts(1, 1);
                var result = fixture.Recover();
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.Completed, result.Status, "Recovery must mark completed after verifying both sides.");
                fixture.AssertDestinationCounts(1, 1);
                TestAssert.Equal(DialogueOutboxEntryState.Completed, fixture.LoadSnapshot().Entries.Single().State,
                    "Completion must be retained as a tombstone.");
            }
        }

        public static void CompletedTombstoneSurvivesCurrentCheckpointAndCompactsLater()
        {
            using (var fixture = new Fixture(DialogueAdmissionRecoveryStep.AfterCompletionBeforeCompaction))
            {
                TestAssert.Throws<IOException>(
                    () => fixture.Enqueue(),
                    "Injected crash after completion must occur before any later-checkpoint compaction.");
                TestAssert.Equal(1, fixture.LoadSnapshot().Entries.Count, "Completion tombstone must remain at its checkpoint.");
                TestAssert.Equal(
                    DialogueOutboxEntryState.Completed,
                    fixture.LoadSnapshot().Entries.Single().State,
                    "Completion must be durable before the injected crash.");
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.NoWork, fixture.Recover().Status, "Same-checkpoint replay must be a no-op.");
                TestAssert.Equal(1, fixture.LoadSnapshot().Entries.Count, "Same checkpoint cannot compact its tombstone.");
                var next = fixture.BindingAt(fixture.Binding.CheckpointGeneration + 1);
                var advanced = fixture.Coordinator.Recover(
                    fixture.OutboxPath, fixture.JournalPath, next, fixture.Ledger, true, fixture.Now.AddMinutes(1));
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.NoWork, advanced.Status, "Later checkpoint needs no destination writes.");
                TestAssert.Equal(0, fixture.Store.Load(fixture.OutboxPath, next).Snapshot!.Entries.Count,
                    "A later successful checkpoint may compact the completed tombstone.");
            }
        }

        public static void RestartReplayCompletesPendingAdmission()
        {
            using (var fixture = new Fixture(DialogueAdmissionRecoveryStep.AfterOutboxWrite))
            {
                TestAssert.Throws<IOException>(() => fixture.Enqueue(), "Crash must leave a pending outbox.");
                var restarted = new DialogueAdmissionRecoveryCoordinator(store: fixture.Store);
                var result = restarted.Recover(
                    fixture.OutboxPath, fixture.JournalPath, fixture.Binding, fixture.Ledger, true, fixture.Now);
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.Completed, result.Status, "A new coordinator must replay pending work.");
                fixture.AssertDestinationCounts(1, 1);
            }
        }

        public static void IdenticalDuplicateAdmissionIsIdempotent()
        {
            using (var fixture = new Fixture())
            {
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.Completed, fixture.Enqueue().Status, "Initial admission must complete.");
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.NoWork, fixture.Enqueue().Status, "Identical duplicate must not write again.");
                fixture.AssertDestinationCounts(1, 1);
            }
        }

        public static void ConflictingDuplicateEventIdFailsClosed()
        {
            using (var fixture = new Fixture())
            {
                fixture.SavePending();
                var conflicting = Fixture.CreatePlan("conflicting text", new UtteranceId(fixture.Plan.FactualEvent.Id.Value));
                TestAssert.Throws<InvalidDataException>(
                    () => fixture.Coordinator.EnqueueAndRecover(
                        fixture.OutboxPath, fixture.JournalPath, fixture.Binding, conflicting,
                        fixture.Ledger, true, fixture.Now),
                    "The same EventId with different canonical payload must fail closed.");
                fixture.AssertDestinationCounts(0, 0);
            }
        }

        public static void StalePendingGenerationFailsClosed()
        {
            using (var fixture = new Fixture())
            {
                fixture.SavePending();
                var later = fixture.BindingAt(fixture.Binding.CheckpointGeneration + 1);
                var result = fixture.Coordinator.Recover(
                    fixture.OutboxPath, fixture.JournalPath, later, fixture.Ledger, true, fixture.Now);
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.StaleGeneration, result.Status,
                    "Pending work from an older checkpoint cannot mutate a later checkpoint.");
                fixture.AssertDestinationCounts(0, 0);
            }
        }

        public static void OutboxAheadOfLoadedSaveFailsClosed()
        {
            using (var fixture = new Fixture())
            {
                var ahead = fixture.BindingAt(5);
                fixture.Store.Save(fixture.OutboxPath, fixture.PendingSnapshot(ahead));
                var result = fixture.Coordinator.Recover(
                    fixture.OutboxPath, fixture.JournalPath, fixture.BindingAt(4), fixture.Ledger, true, fixture.Now);
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.OutboxAheadOfSave, result.Status,
                    "An outbox ahead of the loaded save must fail closed.");
            }
        }

        public static void ReadOnlyStoragePerformsNoWrites()
        {
            using (var fixture = new Fixture())
            {
                var result = fixture.Coordinator.EnqueueAndRecover(
                    fixture.OutboxPath, fixture.JournalPath, fixture.Binding, fixture.Plan,
                    fixture.Ledger, false, fixture.Now);
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.ReadOnlyStorage, result.Status,
                    "Read-only storage must reject admission.");
                TestAssert.False(File.Exists(fixture.OutboxPath), "Read-only admission cannot create an outbox.");
                fixture.AssertDestinationCounts(0, 0);
            }
        }

        public static void InvalidTruncatedJournalBlocksRecovery()
        {
            using (var fixture = new Fixture())
            {
                fixture.SavePending();
                File.WriteAllText(fixture.JournalPath, "truncated");
                var result = fixture.Recover();
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.InvalidJournal, result.Status,
                    "Invalid journal must block recovery.");
                fixture.AssertDestinationCounts(0, 0, journalMustBeValid: false);
            }
        }

        public static void InvalidPrimaryUsesVerifiedBackup()
        {
            using (var fixture = new Fixture())
            {
                var pending = fixture.PendingSnapshot(fixture.Binding);
                fixture.Store.Save(fixture.OutboxPath, pending);
                fixture.Store.Save(
                    fixture.OutboxPath,
                    pending.MarkCompleted(fixture.Plan.FactualEvent.Id, fixture.Binding.CheckpointGeneration, fixture.Now));
                File.WriteAllText(fixture.OutboxPath, "invalid primary");
                var loaded = fixture.Store.Load(fixture.OutboxPath, fixture.Binding);
                TestAssert.Equal(DialogueOutboxStoreLoadStatus.RecoveredFromBackup, loaded.Status,
                    "A valid backup must recover an invalid primary.");
                TestAssert.Equal(DialogueOutboxEntryState.Pending, loaded.Snapshot!.Entries.Single().State,
                    "Recovered backup must preserve its exact prior state.");
            }
        }

        public static void InvalidPrimaryAndBackupFailClosed()
        {
            using (var fixture = new Fixture())
            {
                var pending = fixture.PendingSnapshot(fixture.Binding);
                fixture.Store.Save(fixture.OutboxPath, pending);
                fixture.Store.Save(fixture.OutboxPath, pending.MarkCompleted(
                    fixture.Plan.FactualEvent.Id, fixture.Binding.CheckpointGeneration, fixture.Now));
                File.WriteAllText(fixture.OutboxPath, "invalid primary");
                File.WriteAllText(fixture.OutboxPath + ".bak", "invalid backup");
                var loaded = fixture.Store.Load(fixture.OutboxPath, fixture.Binding);
                TestAssert.Equal(DialogueOutboxStoreLoadStatus.Unrecoverable, loaded.Status,
                    "Invalid primary and backup must fail closed.");
            }
        }

        public static void UnchangedSaveAsGenerationRemainsIdempotent()
        {
            using (var fixture = new Fixture())
            {
                fixture.Enqueue();
                var sameCheckpoint = new DialogueAdmissionBinding(
                    fixture.Binding.StoreId,
                    fixture.Binding.SaveId,
                    fixture.Binding.WorldId,
                    fixture.Binding.CheckpointGeneration);
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.NoWork,
                    fixture.Coordinator.Recover(
                        fixture.OutboxPath, fixture.JournalPath, sameCheckpoint,
                        fixture.Ledger, true, fixture.Now).Status,
                    "Unchanged Save As with the same stable binding/generation must be idempotent.");
                fixture.AssertDestinationCounts(1, 1);
            }
        }

        public static void SaveRollbackRejectsForwardOutbox()
        {
            using (var fixture = new Fixture())
            {
                fixture.Store.Save(fixture.OutboxPath, fixture.PendingSnapshot(fixture.BindingAt(8)));
                var result = fixture.Coordinator.Recover(
                    fixture.OutboxPath, fixture.JournalPath, fixture.BindingAt(7),
                    fixture.Ledger, true, fixture.Now);
                TestAssert.Equal(DialogueAdmissionRecoveryStatus.OutboxAheadOfSave, result.Status,
                    "Rollback cannot consume forward outbox state.");
            }
        }

        public static void CrossStoreSaveOrWorldBindingFailsClosed()
        {
            using (var fixture = new Fixture())
            {
                fixture.SavePending();
                foreach (var mismatched in new[]
                {
                    new DialogueAdmissionBinding(Guid.NewGuid(), fixture.Binding.SaveId, fixture.Binding.WorldId, 4),
                    new DialogueAdmissionBinding(fixture.Binding.StoreId, "other-save", fixture.Binding.WorldId, 4),
                    new DialogueAdmissionBinding(fixture.Binding.StoreId, fixture.Binding.SaveId, "other-world", 4)
                })
                {
                    TestAssert.Equal(
                        DialogueOutboxStoreLoadStatus.BindingMismatch,
                        fixture.Store.Load(fixture.OutboxPath, mismatched).Status,
                        "Store, save, and world bindings must each fail closed on mismatch.");
                }
            }
        }

        public static void OutboxCodecRoundTripIsDeterministic()
        {
            using (var fixture = new Fixture())
            {
                var snapshot = fixture.PendingSnapshot(fixture.Binding);
                var first = fixture.Codec.Encode(snapshot);
                var restored = fixture.Codec.Decode(first);
                var second = fixture.Codec.Encode(restored);
                TestAssert.True(first.SequenceEqual(second), "Outbox codec must reproduce identical bytes.");
                TestAssert.Equal(fixture.Plan.FactualEvent.Id, restored.Entries.Single().EventId,
                    "EventId must round-trip.");
                TestAssert.Equal(fixture.Codec.ComputePlanHash(fixture.Plan),
                    restored.Entries.Single().CanonicalPayloadHash,
                    "Canonical payload hash must round-trip and revalidate.");
            }
        }

        public static void InterruptedTemporaryReplacementPreservesLastGoodOutbox()
        {
            using (var fixture = new Fixture())
            {
                var pending = fixture.PendingSnapshot(fixture.Binding);
                fixture.Store.Save(fixture.OutboxPath, pending);
                var interrupted = new AtomicDialogueAdmissionOutboxStore(
                    fixture.Codec,
                    stage =>
                    {
                        if (stage == DialogueOutboxWriteStage.BeforeAtomicReplace)
                            throw new IOException("Injected replacement interruption.");
                    });
                TestAssert.Throws<IOException>(
                    () => interrupted.Save(
                        fixture.OutboxPath,
                        pending.MarkCompleted(
                            fixture.Plan.FactualEvent.Id,
                            fixture.Binding.CheckpointGeneration,
                            fixture.Now)),
                    "Interrupted replacement must surface.");
                var loaded = fixture.Store.Load(fixture.OutboxPath, fixture.Binding);
                TestAssert.Equal(DialogueOutboxStoreLoadStatus.LoadedPrimary, loaded.Status,
                    "Last good primary must remain loadable.");
                TestAssert.Equal(DialogueOutboxEntryState.Pending, loaded.Snapshot!.Entries.Single().State,
                    "Interrupted replacement cannot partially publish the new state.");
            }
        }

        private sealed class Fixture : IDisposable
        {
            public Fixture(DialogueAdmissionRecoveryStep? failure = null)
            {
                DirectoryPath = Path.Combine(
                    Path.GetTempPath(),
                    "mosaic-dialogue-outbox-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(DirectoryPath);
                OutboxPath = Path.Combine(DirectoryPath, "dialogue.outbox");
                JournalPath = Path.Combine(DirectoryPath, "experience.journal");
                Binding = new DialogueAdmissionBinding(Guid.NewGuid(), "save-lineage", "world-1", 4);
                Now = new DateTimeOffset(2026, 7, 26, 18, 0, 0, TimeSpan.Zero);
                Plan = CreatePlan("recoverable dialogue");
                Codec = new DialogueAdmissionOutboxCodec();
                Store = new AtomicDialogueAdmissionOutboxStore(Codec);
                Journal = new DurableExperienceJournal();
                Ledger = new InMemoryEventLedger();
                Coordinator = new DialogueAdmissionRecoveryCoordinator(
                    Store, Codec, Journal,
                    failure.HasValue ? new ThrowingObserver(failure.Value) : null);
            }

            public string DirectoryPath { get; }
            public string OutboxPath { get; }
            public string JournalPath { get; }
            public DialogueAdmissionBinding Binding { get; }
            public DateTimeOffset Now { get; }
            public DialogueEventAdmissionPlan Plan { get; }
            public DialogueAdmissionOutboxCodec Codec { get; }
            public AtomicDialogueAdmissionOutboxStore Store { get; }
            public DurableExperienceJournal Journal { get; }
            public InMemoryEventLedger Ledger { get; }
            public DialogueAdmissionRecoveryCoordinator Coordinator { get; }

            public DialogueAdmissionRecoveryResult Enqueue() =>
                Coordinator.EnqueueAndRecover(
                    OutboxPath, JournalPath, Binding, Plan, Ledger, true, Now);

            public DialogueAdmissionRecoveryResult Recover() =>
                new DialogueAdmissionRecoveryCoordinator(Store, Codec, Journal).Recover(
                    OutboxPath, JournalPath, Binding, Ledger, true, Now);

            public DialogueAdmissionBinding BindingAt(long generation) =>
                new DialogueAdmissionBinding(
                    Binding.StoreId, Binding.SaveId, Binding.WorldId, generation);

            public DialogueAdmissionOutboxSnapshot PendingSnapshot(DialogueAdmissionBinding binding) =>
                new DialogueAdmissionOutboxSnapshot(
                    binding, Now, Array.Empty<DialogueAdmissionOutboxEntry>())
                    .AddPending(Plan, Codec, Now);

            public void SavePending() => Store.Save(OutboxPath, PendingSnapshot(Binding));

            public DialogueAdmissionOutboxSnapshot LoadSnapshot() =>
                Store.Load(OutboxPath, Binding).Snapshot!;

            public void AssertDestinationCounts(
                int ledgerCount,
                int journalCount,
                bool journalMustBeValid = true)
            {
                TestAssert.Equal(ledgerCount, Ledger.Snapshot().Count, "Ledger count is incorrect.");
                var loaded = Journal.Load(JournalPath);
                if (journalMustBeValid)
                {
                    TestAssert.True(
                        loaded.Status == ExperienceJournalLoadStatus.Loaded ||
                        loaded.Status == ExperienceJournalLoadStatus.NotFound,
                        "Journal must remain valid or absent.");
                    TestAssert.Equal(journalCount, loaded.Records.Count, "Journal count is incorrect.");
                }
            }

            public void Dispose()
            {
                if (Directory.Exists(DirectoryPath))
                    Directory.Delete(DirectoryPath, true);
            }

            public static DialogueEventAdmissionPlan CreatePlan(
                string text,
                UtteranceId? utteranceId = null)
            {
                var speaker = IndividualId.New();
                var recipient = IndividualId.New();
                var witness = IndividualId.New();
                var source = EventId.New();
                var conversation = ConversationId.New();
                var requestId = DialogueRequestId.New();
                var scene = new DialogueSceneSnapshot(
                    source,
                    10,
                    "A bounded test scene.",
                    new[]
                    {
                        new DialogueParticipantSnapshot(speaker, "Speaker", "colonist", true, true),
                        new DialogueParticipantSnapshot(recipient, "Recipient", "colonist", true, true),
                        new DialogueParticipantSnapshot(witness, "Witness", "colonist", true, true)
                    });
                var request = new DialogueRequest(
                    requestId,
                    conversation,
                    DialogueTriggerKind.Social,
                    DialoguePriority.Normal,
                    speaker,
                    speaker,
                    recipient,
                    scene,
                    new[] { source },
                    10,
                    100,
                    "outbox-contract-test");
                var proposal = new UtteranceProposal(
                    utteranceId ?? UtteranceId.New(),
                    requestId,
                    conversation,
                    speaker,
                    recipient,
                    text,
                    new[] { source },
                    12);
                var validation = new UtteranceValidator().Validate(
                    request,
                    proposal,
                    12,
                    new HashSet<UtteranceId>(),
                    new UtteranceValidationPolicy(true, 4000));
                TestAssert.True(validation.IsAccepted, "Outbox fixture utterance must validate.");
                var receipt = new DisplayedUtteranceReceipt(
                    validation.Utterance!,
                    13,
                    new DateTimeOffset(2026, 7, 26, 18, 0, 0, TimeSpan.Zero),
                    DialogueDisclosure.WitnessesOnly,
                    DialoguePresentationChannel.Overlay,
                    new[] { speaker, recipient, witness });
                var prepared = new DialogueEventAdmissionService("rimworld").Prepare(request, receipt);
                TestAssert.True(prepared.IsPrepared, "Outbox fixture admission must prepare.");
                return prepared.Plan!;
            }
        }

        private sealed class ThrowingObserver : IDialogueAdmissionRecoveryObserver
        {
            private readonly DialogueAdmissionRecoveryStep _failure;

            public ThrowingObserver(DialogueAdmissionRecoveryStep failure)
            {
                _failure = failure;
            }

            public void Reached(DialogueAdmissionRecoveryStep step, EventId eventId)
            {
                if (step == _failure)
                    throw new IOException("Injected crash at " + step + " for " + eventId + ".");
            }
        }
    }
}
