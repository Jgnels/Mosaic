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
