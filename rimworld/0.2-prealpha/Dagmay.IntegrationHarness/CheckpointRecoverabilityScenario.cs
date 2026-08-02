using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dagmay.Core.Identity;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;
using Dagmay.Core.Scheduling;

namespace Dagmay.IntegrationHarness
{
    internal static partial class IntegrationScenarios
    {
        private static Task<ScenarioExecution> CheckpointRecoverabilityAsync()
        {
            var assertions = new HarnessAssert();
            var metrics = new Dictionary<string, long>(StringComparer.Ordinal);
            var directory = CreateTempDirectory("checkpoint-recoverability");
            try
            {
                var storeId = Guid.Parse("c5e0217b-f29f-4142-812b-60fbfa05fd8b");
                var identityPath = Path.Combine(directory, "owner.dagmay");
                var journalPath = Path.Combine(directory, "owner.journal");
                var reflectionPath = Path.Combine(directory, "owner.reflection");
                var archive = new AtomicIdentityArchive();
                var journal = new DurableExperienceJournal();
                var reflectionStore = new AtomicReflectionStore();
                var encoder = new DeterministicExperienceEncoder();
                var owner = CreateIndividual("Owner checkpoint");
                var identities = new Dictionary<string, IndividualState>(StringComparer.Ordinal)
                {
                    ["rimworld:pawn:owner"] = owner
                };
                long position = 0;
                var hash = string.Empty;

                for (var index = 1; index <= 8; index++)
                {
                    var source = CreateEvent(owner.Id, "rimworld.skill.level_gained",
                        "checkpoint:" + index, BaseTime.AddMinutes(index),
                        new Dictionary<string, string> { ["skill"] = "Plants", ["level"] = index.ToString() });
                    var encoded = encoder.EncodeExperiencedEvent(source, owner, BaseTime.AddMinutes(index));
                    var append = journal.Append(journalPath,
                        new ExperienceJournalRecord(source, encoded.Perception, encoded.Memory), position, hash);
                    position = append.Position;
                    hash = append.EntryHash;
                    owner = encoded.UpdatedState;
                    identities["rimworld:pawn:owner"] = owner;
                }

                var savePosition = position;
                var saveHash = hash;
                var saveIdentity = CreateIdentitySnapshot(storeId, 10, identities);
                archive.Save(identityPath, saveIdentity);
                archive.PreserveCheckpoint(identityPath, saveIdentity);
                var saveReflection = new ReflectionStoreSnapshot(storeId, 3, BaseTime,
                    Array.Empty<PendingReflectionTask>(), Array.Empty<ReflectionAuditRecord>());
                reflectionStore.Save(reflectionPath, saveReflection);
                reflectionStore.PreserveCheckpoint(reflectionPath, saveReflection);

                for (var index = 9; index <= 13; index++)
                {
                    var source = CreateEvent(owner.Id, "rimworld.skill.level_gained",
                        "future:" + index, BaseTime.AddMinutes(index),
                        new Dictionary<string, string> { ["skill"] = "Plants", ["level"] = index.ToString() });
                    var encoded = encoder.EncodeExperiencedEvent(source, owner, BaseTime.AddMinutes(index));
                    var append = journal.Append(journalPath,
                        new ExperienceJournalRecord(source, encoded.Perception, encoded.Memory), position, hash);
                    position = append.Position;
                    hash = append.EntryHash;
                    owner = encoded.UpdatedState;
                    identities["rimworld:pawn:owner"] = owner;
                    if (index <= 11 || index == 13)
                    {
                        var generation = index <= 11 ? index + 2L : 14L;
                        archive.Save(identityPath, CreateIdentitySnapshot(storeId, generation, identities));
                    }
                }
                for (var generation = 4L; generation <= 8L; generation++)
                    reflectionStore.Save(reflectionPath, new ReflectionStoreSnapshot(storeId, generation,
                        BaseTime.AddMinutes(generation), Array.Empty<PendingReflectionTask>(), Array.Empty<ReflectionAuditRecord>()));

                var recoveredIdentity = archive.Load(identityPath, storeId, 10);
                var recoveredReflection = reflectionStore.Load(reflectionPath, storeId, 3);
                var ahead = journal.Load(journalPath);
                assertions.Equal(ArchiveLoadStatus.LoadedCheckpoint, recoveredIdentity.Status,
                    "owner sequence 10 -> 13/14 resolves the exact immutable identity checkpoint.");
                assertions.Equal(ReflectionStoreLoadStatus.LoadedCheckpoint, recoveredReflection.Status,
                    "reflection resolves its exact save-bound checkpoint after later writes.");
                assertions.Equal(13, ahead.Records.Count,
                    "the verified external journal remains visibly ahead before an explicit recovery choice.");

                var rollback = journal.RestoreVerifiedPrefix(journalPath, savePosition, saveHash);
                var canonical = journal.Load(journalPath);
                var ledger = new InMemoryEventLedger();
                var memories = new MemoryIndex();
                LoadRecords(canonical.Records, ledger, memories);
                assertions.Equal(8, canonical.Records.Count,
                    "exact-world recovery canonicalizes only the loaded save prefix.");
                assertions.Equal(saveHash, canonical.LastHash,
                    "the recovered journal head exactly matches the RimWorld save hash.");
                assertions.True(File.Exists(rollback.PreservedFuturePath),
                    "the five-record future suffix remains preserved as non-canonical audit evidence.");
                assertions.Equal(8, ledger.Snapshot().Count,
                    "runtime reconstruction admits exactly eight checkpointed factual events.");
                assertions.Equal(8, memories.Count,
                    "runtime reconstruction admits exactly eight checkpointed subjective memories.");
                assertions.Equal(14L, archive.GetGenerationFloor(identityPath, storeId),
                    "new identity writes after rollback will not collide with generations 11-14.");
                assertions.Equal(8L, reflectionStore.GetGenerationFloor(reflectionPath, storeId),
                    "new reflection writes after rollback will not collide with later reflection generations.");

                metrics["save_identity_generation"] = 10;
                metrics["external_identity_generation"] = 14;
                metrics["save_experience_position"] = savePosition;
                metrics["external_experience_position"] = 13;
                metrics["recovered_experience_position"] = canonical.Records.Count;
                return Task.FromResult(new ScenarioExecution(assertions.Assertions, metrics,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["future_artifact"] = rollback.PreservedFuturePath
                    }));
            }
            finally { DeleteDirectory(directory); }
        }
    }
}
