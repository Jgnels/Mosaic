using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;

namespace Dagmay.Core.Dialogue
{
    public sealed class DialogueAdmissionBinding : IEquatable<DialogueAdmissionBinding>
    {
        public DialogueAdmissionBinding(
            Guid storeId,
            string saveId,
            string worldId,
            long checkpointGeneration)
        {
            if (storeId == Guid.Empty) throw new ArgumentException("Store ID cannot be empty.", nameof(storeId));
            if (string.IsNullOrWhiteSpace(saveId) || saveId.Length > 256)
                throw new ArgumentException("Save ID is required and cannot exceed 256 characters.", nameof(saveId));
            if (string.IsNullOrWhiteSpace(worldId) || worldId.Length > 256)
                throw new ArgumentException("World ID is required and cannot exceed 256 characters.", nameof(worldId));
            if (checkpointGeneration < 0)
                throw new ArgumentOutOfRangeException(nameof(checkpointGeneration));

            StoreId = storeId;
            SaveId = saveId;
            WorldId = worldId;
            CheckpointGeneration = checkpointGeneration;
        }

        public Guid StoreId { get; }
        public string SaveId { get; }
        public string WorldId { get; }
        public long CheckpointGeneration { get; }

        public bool HasSameStoreSaveAndWorld(DialogueAdmissionBinding other)
        {
            if (other is null) return false;
            return StoreId == other.StoreId
                && string.Equals(SaveId, other.SaveId, StringComparison.Ordinal)
                && string.Equals(WorldId, other.WorldId, StringComparison.Ordinal);
        }

        public bool Equals(DialogueAdmissionBinding? other) =>
            other is not null
            && HasSameStoreSaveAndWorld(other)
            && CheckpointGeneration == other.CheckpointGeneration;

        public override bool Equals(object? obj) => Equals(obj as DialogueAdmissionBinding);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StoreId.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(SaveId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(WorldId);
                hash = (hash * 397) ^ CheckpointGeneration.GetHashCode();
                return hash;
            }
        }
    }

    public enum DialogueOutboxEntryState
    {
        Pending = 0,
        Completed = 1
    }

    public sealed class DialogueAdmissionOutboxEntry
    {
        public DialogueAdmissionOutboxEntry(
            Guid entryId,
            DialogueEventAdmissionPlan plan,
            long expectedCheckpointGeneration,
            string canonicalPayloadHash,
            DialogueOutboxEntryState state,
            long? completedAtGeneration)
        {
            if (entryId == Guid.Empty) throw new ArgumentException("Outbox entry ID cannot be empty.", nameof(entryId));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            if (entryId != plan.FactualEvent.Id.Value)
                throw new ArgumentException("Outbox entry identity must derive exactly from EventId.", nameof(entryId));
            if (expectedCheckpointGeneration < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedCheckpointGeneration));
            if (string.IsNullOrWhiteSpace(canonicalPayloadHash) ||
                canonicalPayloadHash.Length != 64 ||
                canonicalPayloadHash.Any(value => !Uri.IsHexDigit(value)))
                throw new ArgumentException("Canonical payload hash must be lowercase SHA-256 hex.", nameof(canonicalPayloadHash));
            if (!Enum.IsDefined(typeof(DialogueOutboxEntryState), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            if (state == DialogueOutboxEntryState.Pending && completedAtGeneration.HasValue)
                throw new ArgumentException("A pending outbox entry cannot have a completion generation.", nameof(completedAtGeneration));
            if (state == DialogueOutboxEntryState.Completed &&
                (!completedAtGeneration.HasValue || completedAtGeneration.Value < expectedCheckpointGeneration))
                throw new ArgumentException("A completed entry requires a valid completion generation.", nameof(completedAtGeneration));

            EntryId = entryId;
            ExpectedCheckpointGeneration = expectedCheckpointGeneration;
            CanonicalPayloadHash = canonicalPayloadHash.ToLowerInvariant();
            State = state;
            CompletedAtGeneration = completedAtGeneration;
        }

        public Guid EntryId { get; }
        public EventId EventId => Plan.FactualEvent.Id;
        public DialogueEventAdmissionPlan Plan { get; }
        public long ExpectedCheckpointGeneration { get; }
        public string CanonicalPayloadHash { get; }
        public DialogueOutboxEntryState State { get; }
        public long? CompletedAtGeneration { get; }

        public DialogueAdmissionOutboxEntry Complete(long generation)
        {
            if (generation < ExpectedCheckpointGeneration)
                throw new ArgumentOutOfRangeException(nameof(generation));
            if (State == DialogueOutboxEntryState.Completed)
            {
                if (CompletedAtGeneration != generation)
                    throw new InvalidOperationException("Completed outbox entry cannot be completed at a different generation.");
                return this;
            }

            return new DialogueAdmissionOutboxEntry(
                EntryId,
                Plan,
                ExpectedCheckpointGeneration,
                CanonicalPayloadHash,
                DialogueOutboxEntryState.Completed,
                generation);
        }
    }

    public sealed class DialogueAdmissionOutboxSnapshot
    {
        public const int MaximumEntries = 1024;

        public DialogueAdmissionOutboxSnapshot(
            DialogueAdmissionBinding binding,
            DateTimeOffset savedAtUtc,
            IEnumerable<DialogueAdmissionOutboxEntry> entries)
        {
            Binding = binding ?? throw new ArgumentNullException(nameof(binding));
            SavedAtUtc = savedAtUtc;
            var values = new List<DialogueAdmissionOutboxEntry>(
                entries ?? throw new ArgumentNullException(nameof(entries)));
            if (values.Count > MaximumEntries)
                throw new ArgumentException("Dialogue outbox exceeds its entry limit.", nameof(entries));
            if (values.Any(value => value is null))
                throw new ArgumentException("Dialogue outbox cannot contain null entries.", nameof(entries));
            if (values.GroupBy(value => value.EntryId).Any(group => group.Count() != 1))
                throw new ArgumentException("Dialogue outbox entry IDs must be unique.", nameof(entries));
            Entries = new ReadOnlyCollection<DialogueAdmissionOutboxEntry>(
                values.OrderBy(value => value.EntryId.ToString("N"), StringComparer.Ordinal).ToList());
        }

        public DialogueAdmissionBinding Binding { get; }
        public DateTimeOffset SavedAtUtc { get; }
        public IReadOnlyList<DialogueAdmissionOutboxEntry> Entries { get; }

        public DialogueAdmissionOutboxSnapshot AddPending(
            DialogueEventAdmissionPlan plan,
            DialogueAdmissionOutboxCodec codec,
            DateTimeOffset savedAtUtc)
        {
            if (plan is null) throw new ArgumentNullException(nameof(plan));
            if (codec is null) throw new ArgumentNullException(nameof(codec));
            var hash = codec.ComputePlanHash(plan);
            var existing = Entries.FirstOrDefault(value => value.EventId == plan.FactualEvent.Id);
            if (existing is not null)
            {
                if (!FixedTimeEquals(existing.CanonicalPayloadHash, hash))
                    throw new InvalidDataException("Outbox contains a conflicting duplicate EventId.");
                return this;
            }

            var values = Entries.ToList();
            values.Add(new DialogueAdmissionOutboxEntry(
                plan.FactualEvent.Id.Value,
                plan,
                Binding.CheckpointGeneration,
                hash,
                DialogueOutboxEntryState.Pending,
                null));
            return new DialogueAdmissionOutboxSnapshot(Binding, savedAtUtc, values);
        }

        public DialogueAdmissionOutboxSnapshot MarkCompleted(
            EventId eventId,
            long generation,
            DateTimeOffset savedAtUtc)
        {
            var found = false;
            var values = Entries.Select(value =>
            {
                if (value.EventId != eventId) return value;
                found = true;
                return value.Complete(generation);
            }).ToList();
            if (!found) throw new InvalidOperationException("Cannot complete an outbox entry that does not exist.");
            return new DialogueAdmissionOutboxSnapshot(Binding, savedAtUtc, values);
        }

        public DialogueAdmissionOutboxSnapshot AdvanceSuccessfulCheckpoint(
            DialogueAdmissionBinding nextBinding,
            DateTimeOffset savedAtUtc)
        {
            if (nextBinding is null) throw new ArgumentNullException(nameof(nextBinding));
            if (!Binding.HasSameStoreSaveAndWorld(nextBinding))
                throw new InvalidOperationException("Cannot advance an outbox across a store, save, or world binding.");
            if (nextBinding.CheckpointGeneration < Binding.CheckpointGeneration)
                throw new InvalidOperationException("Cannot roll an outbox checkpoint backward.");

            var retained = Entries.Where(value =>
                value.State == DialogueOutboxEntryState.Pending ||
                !value.CompletedAtGeneration.HasValue ||
                value.CompletedAtGeneration.Value >= nextBinding.CheckpointGeneration).ToList();
            return new DialogueAdmissionOutboxSnapshot(nextBinding, savedAtUtc, retained);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left.Length != right.Length) return false;
            var difference = 0;
            for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }
    }

    public enum DialogueOutboxStoreLoadStatus
    {
        LoadedPrimary = 0,
        RecoveredFromBackup = 1,
        NotFound = 2,
        Unrecoverable = 3,
        BindingMismatch = 4,
        OutboxAheadOfSave = 5
    }

    public sealed class DialogueOutboxStoreLoadResult
    {
        public DialogueOutboxStoreLoadResult(
            DialogueOutboxStoreLoadStatus status,
            DialogueAdmissionOutboxSnapshot? snapshot,
            string diagnostic)
        {
            Status = status;
            Snapshot = snapshot;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public DialogueOutboxStoreLoadStatus Status { get; }
        public DialogueAdmissionOutboxSnapshot? Snapshot { get; }
        public string Diagnostic { get; }
    }

    public enum DialogueOutboxWriteStage
    {
        TemporaryFlushed = 0,
        BeforeAtomicReplace = 1
    }

    public sealed class AtomicDialogueAdmissionOutboxStore
    {
        private readonly DialogueAdmissionOutboxCodec _codec;
        private readonly Action<DialogueOutboxWriteStage>? _writeObserver;

        public AtomicDialogueAdmissionOutboxStore(
            DialogueAdmissionOutboxCodec? codec = null,
            Action<DialogueOutboxWriteStage>? writeObserver = null)
        {
            _codec = codec ?? new DialogueAdmissionOutboxCodec();
            _writeObserver = writeObserver;
        }

        public void Save(string path, DialogueAdmissionOutboxSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Outbox path is required.", nameof(path));
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Outbox directory is invalid.");
            Directory.CreateDirectory(directory);
            var temporaryPath = fullPath + ".tmp";
            var backupPath = fullPath + ".bak";
            var bytes = _codec.Encode(snapshot);
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }

            _writeObserver?.Invoke(DialogueOutboxWriteStage.TemporaryFlushed);
            try
            {
                _writeObserver?.Invoke(DialogueOutboxWriteStage.BeforeAtomicReplace);
                if (File.Exists(fullPath)) File.Replace(temporaryPath, fullPath, backupPath, true);
                else File.Move(temporaryPath, fullPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        public DialogueOutboxStoreLoadResult Load(
            string path,
            DialogueAdmissionBinding expectedBinding)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Outbox path is required.", nameof(path));
            if (expectedBinding is null) throw new ArgumentNullException(nameof(expectedBinding));
            var fullPath = Path.GetFullPath(path);
            var backupPath = fullPath + ".bak";
            Exception? primaryFailure = null;
            if (File.Exists(fullPath))
            {
                try
                {
                    return Validate(
                        DialogueOutboxStoreLoadStatus.LoadedPrimary,
                        _codec.Decode(File.ReadAllBytes(fullPath)),
                        expectedBinding,
                        "Primary dialogue outbox loaded and verified.");
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    primaryFailure = exception;
                }
            }

            if (File.Exists(backupPath))
            {
                try
                {
                    return Validate(
                        DialogueOutboxStoreLoadStatus.RecoveredFromBackup,
                        _codec.Decode(File.ReadAllBytes(backupPath)),
                        expectedBinding,
                        "Primary dialogue outbox was invalid; verified backup loaded.");
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    return new DialogueOutboxStoreLoadResult(
                        DialogueOutboxStoreLoadStatus.Unrecoverable,
                        null,
                        $"Primary and backup dialogue outboxes are invalid. Primary: {primaryFailure?.Message ?? "not found"} Backup: {exception.Message}");
                }
            }

            if (primaryFailure is not null)
            {
                return new DialogueOutboxStoreLoadResult(
                    DialogueOutboxStoreLoadStatus.Unrecoverable,
                    null,
                    "Primary dialogue outbox is invalid and no backup exists: " + primaryFailure.Message);
            }

            return new DialogueOutboxStoreLoadResult(
                DialogueOutboxStoreLoadStatus.NotFound,
                null,
                "No dialogue outbox exists yet.");
        }

        private static DialogueOutboxStoreLoadResult Validate(
            DialogueOutboxStoreLoadStatus loadedStatus,
            DialogueAdmissionOutboxSnapshot snapshot,
            DialogueAdmissionBinding expected,
            string diagnostic)
        {
            if (!snapshot.Binding.HasSameStoreSaveAndWorld(expected))
            {
                return new DialogueOutboxStoreLoadResult(
                    DialogueOutboxStoreLoadStatus.BindingMismatch,
                    null,
                    "Dialogue outbox store/save/world binding does not match the loaded save.");
            }
            if (snapshot.Binding.CheckpointGeneration > expected.CheckpointGeneration)
            {
                return new DialogueOutboxStoreLoadResult(
                    DialogueOutboxStoreLoadStatus.OutboxAheadOfSave,
                    null,
                    "Dialogue outbox checkpoint is ahead of the loaded save.");
            }

            return new DialogueOutboxStoreLoadResult(loadedStatus, snapshot, diagnostic);
        }

        private static bool IsRecoverable(Exception exception) =>
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is InvalidDataException
            || exception is FormatException
            || exception is ArgumentException;
    }

    public enum DialogueAdmissionRecoveryStep
    {
        BeforeOutboxWrite = 0,
        AfterOutboxWrite = 1,
        AfterLedgerWrite = 2,
        AfterJournalWrite = 3,
        AfterDestinationsBeforeCompletion = 4,
        AfterCompletionBeforeCompaction = 5
    }

    public interface IDialogueAdmissionRecoveryObserver
    {
        void Reached(DialogueAdmissionRecoveryStep step, EventId eventId);
    }

    public enum DialogueAdmissionRecoveryStatus
    {
        Completed = 0,
        NoWork = 1,
        ReadOnlyStorage = 2,
        InvalidJournal = 3,
        InvalidOutbox = 4,
        BindingMismatch = 5,
        OutboxAheadOfSave = 6,
        StaleGeneration = 7,
        ConflictingDestination = 8
    }

    public sealed class DialogueAdmissionRecoveryResult
    {
        public DialogueAdmissionRecoveryResult(
            DialogueAdmissionRecoveryStatus status,
            int completedCount,
            string diagnostic)
        {
            Status = status;
            CompletedCount = completedCount;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public DialogueAdmissionRecoveryStatus Status { get; }
        public int CompletedCount { get; }
        public string Diagnostic { get; }
    }

    public sealed class DialogueAdmissionRecoveryCoordinator
    {
        private readonly object _gate = new object();
        private readonly AtomicDialogueAdmissionOutboxStore _store;
        private readonly DialogueAdmissionOutboxCodec _codec;
        private readonly DurableExperienceJournal _journal;
        private readonly IDialogueAdmissionRecoveryObserver? _observer;

        public DialogueAdmissionRecoveryCoordinator(
            AtomicDialogueAdmissionOutboxStore? store = null,
            DialogueAdmissionOutboxCodec? codec = null,
            DurableExperienceJournal? journal = null,
            IDialogueAdmissionRecoveryObserver? observer = null)
        {
            _codec = codec ?? new DialogueAdmissionOutboxCodec();
            _store = store ?? new AtomicDialogueAdmissionOutboxStore(_codec);
            _journal = journal ?? new DurableExperienceJournal();
            _observer = observer;
        }

        public DialogueAdmissionRecoveryResult EnqueueAndRecover(
            string outboxPath,
            string journalPath,
            DialogueAdmissionBinding binding,
            DialogueEventAdmissionPlan plan,
            IEventLedger ledger,
            bool storageWritable,
            DateTimeOffset nowUtc)
        {
            if (plan is null) throw new ArgumentNullException(nameof(plan));
            lock (_gate)
            {
                if (!storageWritable)
                    return Result(DialogueAdmissionRecoveryStatus.ReadOnlyStorage, "Dialogue storage is read-only.");

                _observer?.Reached(DialogueAdmissionRecoveryStep.BeforeOutboxWrite, plan.FactualEvent.Id);
                var loaded = _store.Load(outboxPath, binding);
                var loadFailure = MapLoadFailure(loaded);
                if (loadFailure is not null) return loadFailure;
                var snapshot = loaded.Snapshot ??
                    new DialogueAdmissionOutboxSnapshot(binding, nowUtc, Array.Empty<DialogueAdmissionOutboxEntry>());
                if (snapshot.Binding.CheckpointGeneration < binding.CheckpointGeneration)
                    snapshot = snapshot.AdvanceSuccessfulCheckpoint(binding, nowUtc);
                snapshot = snapshot.AddPending(plan, _codec, nowUtc);
                _store.Save(outboxPath, snapshot);
                _observer?.Reached(DialogueAdmissionRecoveryStep.AfterOutboxWrite, plan.FactualEvent.Id);
                return RecoverLocked(
                    outboxPath,
                    journalPath,
                    binding,
                    ledger,
                    storageWritable,
                    nowUtc);
            }
        }

        public DialogueAdmissionRecoveryResult Recover(
            string outboxPath,
            string journalPath,
            DialogueAdmissionBinding binding,
            IEventLedger ledger,
            bool storageWritable,
            DateTimeOffset nowUtc)
        {
            lock (_gate)
            {
                return RecoverLocked(
                    outboxPath,
                    journalPath,
                    binding,
                    ledger,
                    storageWritable,
                    nowUtc);
            }
        }

        private DialogueAdmissionRecoveryResult RecoverLocked(
            string outboxPath,
            string journalPath,
            DialogueAdmissionBinding binding,
            IEventLedger ledger,
            bool storageWritable,
            DateTimeOffset nowUtc)
        {
            if (binding is null) throw new ArgumentNullException(nameof(binding));
            if (ledger is null) throw new ArgumentNullException(nameof(ledger));
            if (!storageWritable)
                return Result(DialogueAdmissionRecoveryStatus.ReadOnlyStorage, "Dialogue storage is read-only.");

            var loaded = _store.Load(outboxPath, binding);
            var loadFailure = MapLoadFailure(loaded);
            if (loadFailure is not null) return loadFailure;
            if (loaded.Status == DialogueOutboxStoreLoadStatus.NotFound || loaded.Snapshot is null)
                return Result(DialogueAdmissionRecoveryStatus.NoWork, "No dialogue admissions are pending.");

            var snapshot = loaded.Snapshot;
            if (snapshot.Binding.CheckpointGeneration < binding.CheckpointGeneration)
            {
                snapshot = snapshot.AdvanceSuccessfulCheckpoint(binding, nowUtc);
                _store.Save(outboxPath, snapshot);
            }
            var journal = _journal.Load(journalPath);
            if (journal.Status == ExperienceJournalLoadStatus.Invalid)
                return Result(DialogueAdmissionRecoveryStatus.InvalidJournal, journal.Diagnostic);

            var completed = 0;
            foreach (var entry in snapshot.Entries.Where(value => value.State == DialogueOutboxEntryState.Pending))
            {
                if (entry.ExpectedCheckpointGeneration != binding.CheckpointGeneration)
                    return Result(DialogueAdmissionRecoveryStatus.StaleGeneration, "Pending dialogue admission belongs to a different checkpoint generation.");
                if (!DestinationMatches(ledger.Snapshot().Where(value => value.Value.Id == entry.EventId).Select(value => value.Value), entry))
                    return Result(DialogueAdmissionRecoveryStatus.ConflictingDestination, "Event ledger contains a conflicting dialogue EventId.");
                if (!DestinationMatches(journal.Records.Where(value => value.FactualEvent.Id == entry.EventId).Select(value => value.FactualEvent), entry))
                    return Result(DialogueAdmissionRecoveryStatus.ConflictingDestination, "Experience journal contains a conflicting or duplicate dialogue EventId.");

                if (!ledger.Contains(entry.EventId))
                {
                    var append = ledger.Append(entry.Plan.FactualEvent);
                    if (append.Status != EventAppendStatus.Appended)
                        return Result(DialogueAdmissionRecoveryStatus.ConflictingDestination, "Event ledger rejected the missing dialogue event.");
                    _observer?.Reached(DialogueAdmissionRecoveryStep.AfterLedgerWrite, entry.EventId);
                }
                if (!DestinationMatches(ledger.Snapshot().Where(value => value.Value.Id == entry.EventId).Select(value => value.Value), entry, requireOne: true))
                    return Result(DialogueAdmissionRecoveryStatus.ConflictingDestination, "Event ledger post-write verification failed.");

                var matchingJournal = journal.Records.Where(value => value.FactualEvent.Id == entry.EventId).ToList();
                if (matchingJournal.Count == 0)
                {
                    _journal.Append(journalPath, entry.Plan.JournalRecord, journal.Records.Count, journal.LastHash);
                    _observer?.Reached(DialogueAdmissionRecoveryStep.AfterJournalWrite, entry.EventId);
                    journal = _journal.Load(journalPath);
                    if (journal.Status != ExperienceJournalLoadStatus.Loaded)
                        return Result(DialogueAdmissionRecoveryStatus.InvalidJournal, "Experience journal post-write verification failed.");
                }
                if (!DestinationMatches(journal.Records.Where(value => value.FactualEvent.Id == entry.EventId).Select(value => value.FactualEvent), entry, requireOne: true))
                    return Result(DialogueAdmissionRecoveryStatus.ConflictingDestination, "Experience journal post-write verification failed.");

                _observer?.Reached(DialogueAdmissionRecoveryStep.AfterDestinationsBeforeCompletion, entry.EventId);
                snapshot = snapshot.MarkCompleted(entry.EventId, binding.CheckpointGeneration, nowUtc);
                _store.Save(outboxPath, snapshot);
                _observer?.Reached(DialogueAdmissionRecoveryStep.AfterCompletionBeforeCompaction, entry.EventId);
                completed++;
            }
            return new DialogueAdmissionRecoveryResult(
                completed == 0 ? DialogueAdmissionRecoveryStatus.NoWork : DialogueAdmissionRecoveryStatus.Completed,
                completed,
                completed == 0 ? "No pending dialogue admissions required recovery." : "Dialogue admissions recovered and verified.");
        }

        private bool DestinationMatches(
            IEnumerable<EnvironmentEvent> values,
            DialogueAdmissionOutboxEntry entry,
            bool requireOne = false)
        {
            var matches = values.ToList();
            if (matches.Count == 0) return !requireOne;
            if (matches.Count != 1) return false;
            return string.Equals(
                _codec.ComputeEventHash(matches[0]),
                entry.CanonicalPayloadHash,
                StringComparison.Ordinal);
        }

        private static DialogueAdmissionRecoveryResult? MapLoadFailure(
            DialogueOutboxStoreLoadResult loaded)
        {
            switch (loaded.Status)
            {
                case DialogueOutboxStoreLoadStatus.Unrecoverable:
                    return Result(DialogueAdmissionRecoveryStatus.InvalidOutbox, loaded.Diagnostic);
                case DialogueOutboxStoreLoadStatus.BindingMismatch:
                    return Result(DialogueAdmissionRecoveryStatus.BindingMismatch, loaded.Diagnostic);
                case DialogueOutboxStoreLoadStatus.OutboxAheadOfSave:
                    return Result(DialogueAdmissionRecoveryStatus.OutboxAheadOfSave, loaded.Diagnostic);
                default:
                    return null;
            }
        }

        private static DialogueAdmissionRecoveryResult Result(
            DialogueAdmissionRecoveryStatus status,
            string diagnostic) =>
            new DialogueAdmissionRecoveryResult(status, 0, diagnostic);
    }
}
