using System;
using System.Collections.Generic;
using System.IO;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;
using Dagmay.Core.Memory;
using Dagmay.Core.Lifecycle;
using Dagmay.Core.Persistence;
using Dagmay.Core.Reflection;
using Dagmay.Core.Scheduling;

namespace Dagmay.Tests
{
    internal static class CheckpointRecoverabilityContractTests
    {
        public static void IdentityCheckpointSurvivesOwnerSequenceAndOneHundredWrites()
        {
            var directory = TemporaryDirectory("identity-depth");
            var path = Path.Combine(directory, "owner.dagmay");
            var storeId = Guid.NewGuid();
            var archive = new AtomicIdentityArchive();
            try
            {
                var checkpoint = IdentitySnapshot(storeId, 10, "Checkpoint");
                var checkpointState = checkpoint.Records[0].State;
                archive.Save(path, checkpoint);
                archive.PreserveCheckpoint(path, checkpoint);
                var futureState = checkpointState.Rename("Renamed after save", checkpointState.Version);
                futureState = futureState.WithAffect(
                    new AffectVector(0.5, 0.4, 0.3, 0.2, 0.1, -0.1, -0.2), futureState.Version);
                futureState = futureState.TransitionLifecycle(
                    LifecycleState.Dead, LifecycleTransitionKind.InWorldDeath, futureState.Version);
                for (var generation = 11L; generation <= 110L; generation++)
                    archive.Save(path, IdentitySnapshot(storeId, generation, futureState));

                File.WriteAllText(archive.GetCheckpointPath(path, storeId, 10) + ".tmp", "interrupted");
                var exact = archive.Load(path, storeId, 10);
                TestAssert.Equal(ArchiveLoadStatus.LoadedCheckpoint, exact.Status,
                    "Generation 10 must remain recoverable after arbitrary later identity writes.");
                TestAssert.Equal("Checkpoint", exact.Snapshot!.Records[0].State.DisplayName,
                    "No later identity mutation may leak into the checkpoint snapshot.");
                TestAssert.Equal(1, exact.Snapshot.Records.Count,
                    "Enrollment after the save must not leak into the restored checkpoint.");
                TestAssert.Equal(checkpointState.Id, exact.Snapshot.Records[0].State.Id,
                    "Later rename, affect, and lifecycle writes must not fabricate or replace identity.");
                TestAssert.Equal(LifecycleState.Active, exact.Snapshot.Records[0].State.Lifecycle,
                    "Post-checkpoint lifecycle changes must not leak into the restored save.");
                TestAssert.Equal(0L, exact.Snapshot.Records[0].State.Version,
                    "Post-checkpoint experience-derived state changes must not leak into the restored save.");
                TestAssert.Equal(110L, archive.GetGenerationFloor(path, storeId),
                    "A recovered old save must allocate above every already-persisted generation.");
            }
            finally { DeleteDirectory(directory); }
        }

        public static void IdentityCheckpointIsImmutableIdempotentAndFailsClosedWhenCorrupt()
        {
            var directory = TemporaryDirectory("identity-immutable");
            var path = Path.Combine(directory, "owner.dagmay");
            var storeId = Guid.NewGuid();
            var archive = new AtomicIdentityArchive();
            try
            {
                var checkpoint = IdentitySnapshot(storeId, 10, "Exact");
                archive.PreserveCheckpoint(path, checkpoint);
                archive.PreserveCheckpoint(path, checkpoint);
                TestAssert.Throws<InvalidDataException>(
                    () => archive.PreserveCheckpoint(path, IdentitySnapshot(storeId, 10, "Conflict")),
                    "Conflicting bytes for the same store and generation must fail.");

                archive.Save(path, IdentitySnapshot(storeId, 13, "Future-13"));
                archive.Save(path, IdentitySnapshot(storeId, 14, "Future-14"));
                TestAssert.Equal(13L, archive.Load(path + ".bak").Snapshot!.Generation,
                    "The atomic backup must remain only the immediately previous generation, not the save checkpoint.");
                var checkpointPath = archive.GetCheckpointPath(path, storeId, 10);
                File.WriteAllBytes(checkpointPath, new byte[] { 1, 2, 3 });
                File.WriteAllText(checkpointPath + ".tmp", "interrupted");
                var rejected = archive.Load(path, storeId, 10);
                TestAssert.Equal(ArchiveLoadStatus.Unrecoverable, rejected.Status,
                    "A corrupt exact checkpoint must fail closed even when newer heads are valid.");
            }
            finally { DeleteDirectory(directory); }
        }

        public static void ReflectionCheckpointSurvivesOneHundredWritesAndRejectsConflict()
        {
            var directory = TemporaryDirectory("reflection-depth");
            var path = Path.Combine(directory, "owner.reflection");
            var storeId = Guid.NewGuid();
            var store = new AtomicReflectionStore();
            try
            {
                var checkpoint = ReflectionSnapshot(storeId, 3, DateTimeOffset.UnixEpoch);
                store.Save(path, checkpoint);
                store.PreserveCheckpoint(path, checkpoint);
                store.PreserveCheckpoint(path, checkpoint);
                TestAssert.Throws<InvalidDataException>(
                    () => store.PreserveCheckpoint(path, ReflectionSnapshot(storeId, 3, DateTimeOffset.UnixEpoch.AddSeconds(1))),
                    "Conflicting reflection bytes for one generation must fail.");
                for (var generation = 4L; generation <= 103L; generation++)
                    store.Save(path, ReflectionSnapshot(storeId, generation, DateTimeOffset.UnixEpoch.AddSeconds(generation)));

                var exact = store.Load(path, storeId, 3);
                TestAssert.Equal(ReflectionStoreLoadStatus.LoadedCheckpoint, exact.Status,
                    "Reflection checkpoint must survive arbitrary later writes.");
                TestAssert.Equal(103L, store.GetGenerationFloor(path, storeId),
                    "Reflection generations must remain monotonic after loading an old save.");

                File.WriteAllBytes(store.GetCheckpointPath(path, storeId, 3), new byte[] { 4, 5, 6 });
                TestAssert.Equal(ReflectionStoreLoadStatus.Unrecoverable, store.Load(path, storeId, 3).Status,
                    "A corrupt exact reflection checkpoint must fail closed despite newer heads.");
            }
            finally { DeleteDirectory(directory); }
        }

        public static void JournalRollbackRestoresExactPrefixAndPreservesFutureBytes()
        {
            var directory = TemporaryDirectory("journal-rollback");
            var path = Path.Combine(directory, "owner.journal");
            var journal = new DurableExperienceJournal();
            var position = 0L;
            var hash = string.Empty;
            var checkpointHash = string.Empty;
            try
            {
                for (var index = 1; index <= 13; index++)
                {
                    var appended = journal.Append(path, EventRecord("event-" + index), position, hash);
                    position = appended.Position;
                    hash = appended.EntryHash;
                    if (index == 8) checkpointHash = hash;
                }
                var futureBytes = File.ReadAllBytes(path);
                File.WriteAllText(path + ".rollback.tmp", "interrupted");
                var restored = journal.RestoreVerifiedPrefix(path, 8, checkpointHash);
                var canonical = journal.Load(path);

                TestAssert.Equal(ExperienceJournalRollbackStatus.RestoredExactPrefix, restored.Status,
                    "An ahead verified journal must establish an exact loaded-save branch.");
                TestAssert.Equal(8, canonical.Records.Count, "Canonical history must stop at the save checkpoint.");
                TestAssert.Equal(checkpointHash, canonical.LastHash, "Canonical head must equal the save hash.");
                TestAssert.True(File.Exists(restored.PreservedFuturePath), "Future history must remain as an immutable audit artifact.");
                TestAssert.True(BytesEqual(futureBytes, File.ReadAllBytes(restored.PreservedFuturePath)),
                    "The non-canonical future journal must be preserved byte-for-byte.");

                var branch = journal.Append(path, EventRecord("new-branch-9"), 8, checkpointHash);
                TestAssert.Equal(9L, branch.Position, "New history must append from the restored checkpoint, not future position 13.");
                var branched = journal.Load(path);
                TestAssert.Equal("new-branch-9", branched.Records[8].FactualEvent.Kind,
                    "No post-checkpoint event from the rolled-back world may leak into the new canonical branch.");
            }
            finally { DeleteDirectory(directory); }
        }

        public static void MultipleCompletedSavesRemainExactlyRecoverable()
        {
            var directory = TemporaryDirectory("multiple-saves");
            var path = Path.Combine(directory, "owner.dagmay");
            var storeId = Guid.NewGuid();
            var archive = new AtomicIdentityArchive();
            try
            {
                foreach (var generation in new[] { 5L, 10L, 15L })
                {
                    var snapshot = IdentitySnapshot(storeId, generation, "Save-" + generation);
                    archive.Save(path, snapshot);
                    archive.PreserveCheckpoint(path, snapshot);
                }
                var interruptedSave = IdentitySnapshot(storeId, 16, "Interrupted-save");
                archive.Save(path, interruptedSave);
                archive.PreserveCheckpoint(path, interruptedSave);

                foreach (var generation in new[] { 5L, 10L, 15L })
                {
                    var loaded = archive.Load(path, storeId, generation);
                    TestAssert.True(
                        loaded.Status == ArchiveLoadStatus.LoadedCheckpoint
                        || loaded.Status == ArchiveLoadStatus.RecoveredFromBackup,
                        "Every completed save generation must remain independently recoverable through the required primary/backup/checkpoint order.");
                    TestAssert.Equal("Save-" + generation, loaded.Snapshot!.Records[0].State.DisplayName,
                        "A failed or uncheckpointed later save must not alter an older completed save.");
                }
            }
            finally { DeleteDirectory(directory); }
        }

        private static IdentityArchiveSnapshot IdentitySnapshot(Guid storeId, long generation, string name)
        {
            var seed = new IdentitySeed("rimworld", "checkpoint-test",
                new[] { new SeedFact(SeedFactCategory.Trait, "kind", "Kind", "fixture", 1.0) });
            return new IdentityArchiveSnapshot(storeId, generation, DateTimeOffset.UnixEpoch.AddSeconds(generation),
                new[] { new PersistedIdentityRecord("rimworld:pawn:owner", IndividualState.Create(name, seed)) });
        }

        private static IdentityArchiveSnapshot IdentitySnapshot(Guid storeId, long generation, IndividualState state)
        {
            return new IdentityArchiveSnapshot(storeId, generation, DateTimeOffset.UnixEpoch.AddSeconds(generation),
                new[]
                {
                    new PersistedIdentityRecord("rimworld:pawn:owner", state),
                    new PersistedIdentityRecord("rimworld:pawn:enrolled-after-save",
                        IndividualState.Create("Enrolled after save", state.Seed))
                });
        }

        private static ReflectionStoreSnapshot ReflectionSnapshot(Guid storeId, long generation, DateTimeOffset savedAt)
        {
            return new ReflectionStoreSnapshot(storeId, generation, savedAt,
                Array.Empty<PendingReflectionTask>(), Array.Empty<ReflectionAuditRecord>());
        }

        private static ExperienceJournalRecord EventRecord(string kind)
        {
            var now = DateTimeOffset.UnixEpoch.AddSeconds(kind.Length);
            return new ExperienceJournalRecord(new EnvironmentEvent(
                EventId.New(), kind + ":" + Guid.NewGuid().ToString("N"), kind, "rimworld",
                now, now, 1, "Dagmay.Tests", new Dictionary<string, string>(), Array.Empty<IndividualId>()), null, null);
        }

        private static string TemporaryDirectory(string name)
        {
            return Path.Combine(Path.GetTempPath(), "dagmay-" + name + "-" + Guid.NewGuid().ToString("N"));
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
            return true;
        }
    }
}
