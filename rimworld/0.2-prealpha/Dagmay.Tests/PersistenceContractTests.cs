using System;
using System.IO;
using System.Text;
using Dagmay.Core.Identity;
using Dagmay.Core.Lifecycle;
using Dagmay.Core.Persistence;

namespace Dagmay.Tests
{
    internal static class PersistenceContractTests
    {
        public static void IdentityArchiveRoundTripPreservesContinuity()
        {
            var original = CreateIndividual("Mira")
                .Rename("Mira Vale", 0)
                .TransitionLifecycle(LifecycleState.Archived, LifecycleTransitionKind.InWorldDeath, 1);
            var snapshot = new IdentityArchiveSnapshot(
                Guid.NewGuid(),
                7,
                DateTimeOffset.UtcNow,
                new[] { new PersistedIdentityRecord("rimworld:pawn:42", original) });

            var codec = new IdentityArchiveCodec();
            var restored = codec.Decode(codec.Encode(snapshot));
            var record = restored.Records[0];

            TestAssert.Equal(snapshot.StoreId, restored.StoreId, "Store ID must round-trip.");
            TestAssert.Equal(7L, restored.Generation, "Archive generation must round-trip.");
            TestAssert.Equal(original.Id, record.State.Id, "Individual ID must survive persistence.");
            TestAssert.Equal(original.LineageId, record.State.LineageId, "Lineage must survive persistence.");
            TestAssert.Equal(original.Version, record.State.Version, "State version must survive persistence.");
            TestAssert.Equal("Mira Vale", record.State.DisplayName, "Current display name must survive persistence.");
            TestAssert.Equal(LifecycleState.Archived, record.State.Lifecycle, "Death archive state must survive persistence.");
            TestAssert.Equal("Kind", record.State.Seed.Facts[0].Value, "Grounded seed facts must survive persistence.");
        }

        public static void CorruptedArchiveCannotBecomeCanonicalState()
        {
            var codec = new IdentityArchiveCodec();
            var snapshot = CreateSnapshot(1, "Ari");
            var encoded = codec.Encode(snapshot);
            encoded[encoded.Length / 2] ^= 0x01;

            TestAssert.Throws<InvalidDataException>(
                () => codec.Decode(encoded),
                "A corrupted or checksum-mismatched archive must be rejected.");
        }

        public static void AtomicArchiveRecoversVerifiedBackup()
        {
            var directory = Path.Combine(Path.GetTempPath(), "dagmay-tests-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "identity-store.dagmay");
            try
            {
                var archive = new AtomicIdentityArchive();
                archive.Save(path, CreateSnapshot(1, "Jo"));
                archive.Save(path, CreateSnapshot(2, "Jo"));
                File.WriteAllText(path, "intentionally corrupted", Encoding.UTF8);

                var recovered = archive.Load(path);
                TestAssert.Equal(
                    ArchiveLoadStatus.RecoveredFromBackup,
                    recovered.Status,
                    "A corrupted primary archive must recover only from a verified backup.");
                TestAssert.True(recovered.Snapshot is not null, "Verified backup recovery must return a snapshot.");
                TestAssert.Equal(1L, recovered.Snapshot!.Generation, "Recovery must expose the backup generation explicitly.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        public static void ArchiveCheckpointExpectationRejectsIdentityAndGenerationMismatch()
        {
            var directory = Path.Combine(Path.GetTempPath(), "dagmay-tests-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "identity-store.dagmay");
            try
            {
                var archive = new AtomicIdentityArchive();
                var snapshot = CreateSnapshot(4, "Checkpoint");
                archive.Save(path, snapshot);

                var exact = archive.Load(path, snapshot.StoreId, snapshot.Generation);
                TestAssert.Equal(
                    ArchiveLoadStatus.LoadedPrimary,
                    exact.Status,
                    "A checksum-valid archive matching the save checkpoint must load.");

                var wrongStore = archive.Load(path, Guid.Parse("817824dc-6e50-45e6-9135-29ec21b70e23"), 4);
                TestAssert.Equal(
                    ArchiveLoadStatus.Unrecoverable,
                    wrongStore.Status,
                    "An archive for another store must fail closed.");
                TestAssert.True(wrongStore.Snapshot is null, "Store mismatch must not expose canonical state.");

                var rollback = archive.Load(path, snapshot.StoreId, 5);
                TestAssert.Equal(
                    ArchiveLoadStatus.Unrecoverable,
                    rollback.Status,
                    "An older archive generation must not replace the save checkpoint.");
                TestAssert.True(rollback.Snapshot is null, "Generation rollback must not expose canonical state.");

                var uncheckpointedForwardState = archive.Load(path, snapshot.StoreId, 3);
                TestAssert.Equal(
                    ArchiveLoadStatus.Unrecoverable,
                    uncheckpointedForwardState.Status,
                    "An archive ahead of the save checkpoint must require explicit recovery rather than silent adoption.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        public static void BackupRecoveryCannotRollBackPastSaveCheckpoint()
        {
            var directory = Path.Combine(Path.GetTempPath(), "dagmay-tests-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "identity-store.dagmay");
            try
            {
                var archive = new AtomicIdentityArchive();
                var older = CreateSnapshot(7, "Older");
                var current = CreateSnapshot(8, "Current");
                archive.Save(path, older);
                archive.Save(path, current);
                File.WriteAllText(path, "partially replaced primary", Encoding.UTF8);

                var result = archive.Load(path, current.StoreId, current.Generation);
                TestAssert.Equal(
                    ArchiveLoadStatus.Unrecoverable,
                    result.Status,
                    "A verified but stale backup must not be substituted for the save checkpoint.");
                TestAssert.True(result.Snapshot is null, "Rejected rollback recovery must not expose stale identity state.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        public static void InterruptedTemporaryWritePreservesLastKnownGoodArchive()
        {
            var directory = Path.Combine(Path.GetTempPath(), "dagmay-tests-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "identity-store.dagmay");
            try
            {
                var archive = new AtomicIdentityArchive();
                var checkpoint = CreateSnapshot(9, "Stable");
                archive.Save(path, checkpoint);
                File.WriteAllText(path + ".tmp", "interrupted write", Encoding.UTF8);

                var result = archive.Load(path, checkpoint.StoreId, checkpoint.Generation);
                TestAssert.Equal(
                    ArchiveLoadStatus.LoadedPrimary,
                    result.Status,
                    "An abandoned temporary file must not displace the last known good primary.");
                TestAssert.Equal(
                    "Stable",
                    result.Snapshot!.Records[0].State.DisplayName,
                    "Interrupted writes must preserve the checkpointed individual.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        public static void TruncatedAndUnsupportedArchivesFailClosed()
        {
            var codec = new IdentityArchiveCodec();
            var encoded = codec.Encode(CreateSnapshot(1, "Schema"));
            var truncated = new byte[encoded.Length / 2];
            Array.Copy(encoded, truncated, truncated.Length);
            TestAssert.Throws<InvalidDataException>(
                () => codec.Decode(truncated),
                "A truncated identity archive must fail closed.");

            var text = Encoding.UTF8.GetString(encoded);
            var unsupported = Encoding.UTF8.GetBytes(text.Replace("format=\"1\"", "format=\"2\""));
            TestAssert.Throws<InvalidDataException>(
                () => codec.Decode(unsupported),
                "An unsupported identity archive envelope version must fail closed.");
        }

        private static IdentityArchiveSnapshot CreateSnapshot(long generation, string name)
        {
            return new IdentityArchiveSnapshot(
                Guid.Parse("71f56903-c88d-4205-81e7-4b046757eb77"),
                generation,
                DateTimeOffset.UtcNow,
                new[] { new PersistedIdentityRecord("rimworld:pawn:fixture", CreateIndividual(name)) });
        }

        private static IndividualState CreateIndividual(string name)
        {
            var seed = new IdentitySeed(
                "rimworld",
                "0.1B",
                new[] { new SeedFact(SeedFactCategory.Trait, "kind", "Kind", "RimWorld trait", 1.0) });
            return IndividualState.Create(name, seed);
        }
    }
}
