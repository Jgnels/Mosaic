using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;
using Dagmay.Core.Reflection;
using Dagmay.Core.Scheduling;
using Dagmay.Providers.Fake;

namespace Dagmay.IntegrationHarness
{
    internal static partial class IntegrationScenarios
    {
        private static async Task<ScenarioExecution> PersistenceTortureAsync()
        {
            var assertions = new HarnessAssert();
            var metrics = new Dictionary<string, long>(StringComparer.Ordinal);
            var directory = CreateTempDirectory("persistence-torture");
            try
            {
                const int cycleCount = 6;
                const int individualCount = 4;
                const int ordinaryEventsPerCycle = 2;
                var storeId = Guid.Parse("37620f75-aa4f-43f0-b8fb-baf469f3e055");
                var archivePath = Path.Combine(directory, "identities.dagmay");
                var journalPath = Path.Combine(directory, "experiences.journal");
                var reflectionPath = Path.Combine(directory, "reflections.reflection");
                var archive = new AtomicIdentityArchive();
                var journal = new DurableExperienceJournal();
                var reflectionStore = new AtomicReflectionStore();
                var encoder = new DeterministicExperienceEncoder();
                var contextBuilder = new ReflectionContextBuilder();
                var unavailableProvider = new DeterministicFakeProvider(FakeProviderBehavior.RateLimited);

                var identities = new Dictionary<string, IndividualState>(StringComparer.Ordinal);
                var originalContinuity = new Dictionary<string, (IndividualId Id, LineageId LineageId)>(StringComparer.Ordinal);
                for (var index = 0; index < individualCount; index++)
                {
                    var externalId = "rimworld:pawn:torture:" + index.ToString("D2");
                    var state = CreateIndividual("Torture " + index.ToString("D2"));
                    identities.Add(externalId, state);
                    originalContinuity.Add(externalId, (state.Id, state.LineageId));
                }

                archive.Save(archivePath, CreateIdentitySnapshot(storeId, 1, identities));
                var expectedExperienceCount = 0;
                long reflectionGeneration = 0;
                var forwardRecoveryCount = 0;

                for (var cycle = 1; cycle <= cycleCount; cycle++)
                {
                    var loadedArchive = archive.Load(archivePath);
                    assertions.Equal(ArchiveLoadStatus.LoadedPrimary, loadedArchive.Status, "Identity archive reloads from the verified primary at torture cycle " + cycle + ".");
                    var archiveSnapshot = loadedArchive.Snapshot
                        ?? throw new HarnessAssertionException("Torture identity archive did not expose a verified snapshot.");
                    identities = archiveSnapshot.Records.ToDictionary(
                        value => value.ExternalEntityId,
                        value => value.State,
                        StringComparer.Ordinal);
                    AssertIdentityContinuity(assertions, identities, originalContinuity, cycle);

                    var loadedJournal = journal.Load(journalPath);
                    if (expectedExperienceCount == 0)
                    {
                        assertions.Equal(ExperienceJournalLoadStatus.NotFound, loadedJournal.Status, "The first torture cycle begins without an experience journal.");
                    }
                    else
                    {
                        assertions.Equal(ExperienceJournalLoadStatus.Loaded, loadedJournal.Status, "Experience journal hash chain verifies at torture cycle " + cycle + ".");
                    }
                    assertions.Equal(expectedExperienceCount, loadedJournal.Records.Count, "No experience is lost across torture reload cycle " + cycle + ".");

                    var ledger = new InMemoryEventLedger();
                    var memoryIndex = new MemoryIndex();
                    LoadRecords(loadedJournal.Records, ledger, memoryIndex);
                    AssertUniqueExperienceRecords(assertions, loadedJournal.Records, cycle);
                    long journalPosition = loadedJournal.Records.Count;
                    var journalHash = loadedJournal.LastHash;

                    PersistentReflectionQueue queue;
                    List<ReflectionAuditRecord> audit;
                    var loadedReflection = reflectionStore.Load(reflectionPath);
                    if (cycle == 1)
                    {
                        assertions.Equal(ReflectionStoreLoadStatus.NotFound, loadedReflection.Status, "The first torture cycle begins without a reflection sidecar.");
                        queue = new PersistentReflectionQueue(100);
                        audit = new List<ReflectionAuditRecord>();
                    }
                    else
                    {
                        assertions.Equal(ReflectionStoreLoadStatus.LoadedPrimary, loadedReflection.Status, "Reflection sidecar reloads from the verified primary at torture cycle " + cycle + ".");
                        var reflectionSnapshot = loadedReflection.Snapshot
                            ?? throw new HarnessAssertionException("Torture reflection sidecar did not expose a verified snapshot.");
                        assertions.Equal(storeId, reflectionSnapshot.StoreId, "Reflection sidecar retains the exact torture StoreId.");
                        queue = new PersistentReflectionQueue(100, reflectionSnapshot.PendingTasks);
                        audit = new List<ReflectionAuditRecord>(reflectionSnapshot.AuditRecords);
                        reflectionGeneration = reflectionSnapshot.Generation;
                        assertions.True(queue.Count > 0, "A non-empty reflection queue survives save/reload while the provider is unavailable at cycle " + cycle + ".");
                        AssertUniqueReflectionRecords(assertions, queue.Snapshot(), audit, cycle);
                    }

                    var orderedExternalIds = identities.Keys.OrderBy(value => value, StringComparer.Ordinal).ToList();
                    var ownerExternalId = orderedExternalIds[(cycle - 1) % orderedExternalIds.Count];
                    var counterpartExternalId = orderedExternalIds[cycle % orderedExternalIds.Count];
                    var owner = identities[ownerExternalId];
                    var counterpart = identities[counterpartExternalId];
                    var occurred = BaseTime.AddDays(cycle);

                    var skillEvent = CreateEvent(
                        owner.Id,
                        "rimworld.skill.level_gained",
                        "torture:skill:" + cycle,
                        occurred,
                        new Dictionary<string, string>
                        {
                            ["skill"] = "Crafting",
                            ["level"] = (cycle + 3).ToString()
                        });
                    var skillEncoded = encoder.EncodeExperiencedEvent(skillEvent, owner, occurred.AddSeconds(1));
                    AppendTortureRecord(
                        journal,
                        journalPath,
                        skillEvent,
                        skillEncoded,
                        ledger,
                        memoryIndex,
                        ref journalPosition,
                        ref journalHash);
                    identities[ownerExternalId] = skillEncoded.UpdatedState;
                    owner = skillEncoded.UpdatedState;
                    queue.EnqueueOrMerge(CreateTortureReflectionTask(owner.Id, skillEvent, occurred));
                    expectedExperienceCount++;

                    var socialEvent = CreateSocialEvent(
                        owner,
                        counterpart,
                        cycle * 3,
                        cycle * 3 + 20,
                        "torture:social:" + cycle,
                        occurred.AddMinutes(1));
                    var socialEncoded = encoder.EncodeExperiencedEvent(socialEvent, owner, occurred.AddMinutes(1).AddSeconds(1));
                    AppendTortureRecord(
                        journal,
                        journalPath,
                        socialEvent,
                        socialEncoded,
                        ledger,
                        memoryIndex,
                        ref journalPosition,
                        ref journalHash);
                    identities[ownerExternalId] = socialEncoded.UpdatedState;
                    queue.EnqueueOrMerge(CreateTortureReflectionTask(owner.Id, socialEvent, occurred.AddMinutes(1)));
                    expectedExperienceCount++;

                    var dispatchAt = occurred.AddHours(1);
                    if (!queue.TrySelect(dispatchAt, out var selectedPending) || selectedPending is null)
                    {
                        throw new HarnessAssertionException("Queued reflection was not dispatchable during simulated provider outage at cycle " + cycle + ".");
                    }
                    assertions.True(true, "Queued reflection remains dispatchable during simulated provider outage at cycle " + cycle + ".");
                    var pending = selectedPending;
                    var state = identities.Values.Single(value => value.Id == pending.Task.IndividualId);
                    var eventById = ledger.Snapshot().ToDictionary(value => value.Value.Id, value => value.Value);
                    var sourceEvents = pending.Task.SourceEventIds.Select(value => eventById[value]).ToList();
                    var ownerMemories = memoryIndex.Snapshot().Where(value => value.OwnerId == state.Id).ToList();
                    var request = contextBuilder.BuildRequest(
                        pending.Task,
                        state,
                        sourceEvents,
                        ownerMemories,
                        dispatchAt,
                        TimeSpan.FromMinutes(1));
                    var result = await unavailableProvider.GenerateStructuredAsync(request, CancellationToken.None).ConfigureAwait(false);
                    assertions.Equal(ModelResultStatus.RateLimited, result.Status, "Provider remains unavailable while new experiences continue arriving at cycle " + cycle + ".");
                    assertions.True(result.Retryable, "Provider outage is retryable without deleting canonical experience at cycle " + cycle + ".");
                    audit.Add(CreateTortureFailureAudit(pending, request, state, result, dispatchAt));
                    assertions.True(queue.MarkRetry(pending.Task.Id, dispatchAt.AddMinutes(5), result.ErrorCode), "Failed provider work remains queued for a later retry at cycle " + cycle + ".");
                    assertions.True(queue.Count > 0, "Reflection queue is non-empty at save time during torture cycle " + cycle + ".");

                    archive.Save(archivePath, CreateIdentitySnapshot(storeId, cycle + 1L, identities));
                    reflectionGeneration++;
                    reflectionStore.Save(
                        reflectionPath,
                        new ReflectionStoreSnapshot(storeId, reflectionGeneration, dispatchAt, queue.Snapshot(), audit));

                    var savedReflection = reflectionStore.Load(reflectionPath);
                    assertions.Equal(ReflectionStoreLoadStatus.LoadedPrimary, savedReflection.Status, "Reflection checkpoint verifies immediately after torture save cycle " + cycle + ".");
                    var savedReflectionSnapshot = savedReflection.Snapshot
                        ?? throw new HarnessAssertionException("Saved torture reflection checkpoint was missing.");
                    assertions.Equal(queue.Count, savedReflectionSnapshot.PendingTasks.Count, "No queued reflection task is lost at torture save cycle " + cycle + ".");
                    assertions.Equal(audit.Count, savedReflectionSnapshot.AuditRecords.Count, "No reflection audit record is lost at torture save cycle " + cycle + ".");

                    if (cycle == 3)
                    {
                        var saveCheckpointPosition = journalPosition;
                        var saveCheckpointHash = journalHash;
                        var aheadOwnerExternalId = orderedExternalIds[0];
                        var aheadOwner = identities[aheadOwnerExternalId];
                        var aheadEvent = CreateEvent(
                            aheadOwner.Id,
                            "rimworld.health.condition_removed",
                            "torture:external-ahead",
                            occurred.AddHours(2),
                            new Dictionary<string, string> { ["condition"] = "TortureFixture" });
                        var aheadEncoded = encoder.EncodeExperiencedEvent(aheadEvent, aheadOwner, occurred.AddHours(2).AddSeconds(1));
                        var aheadAppend = journal.Append(
                            journalPath,
                            new ExperienceJournalRecord(aheadEvent, aheadEncoded.Perception, aheadEncoded.Memory),
                            journalPosition,
                            journalHash);
                        journalPosition = aheadAppend.Position;
                        journalHash = aheadAppend.EntryHash;
                        expectedExperienceCount++;

                        var verifiedAhead = journal.Load(journalPath);
                        assertions.Equal(ExperienceJournalLoadStatus.Loaded, verifiedAhead.Status, "The one-record-ahead external journal verifies as a complete hash chain.");
                        assertions.Equal(saveCheckpointPosition + 1L, verifiedAhead.Records.Count, "The external journal is exactly one record ahead of the simulated save checkpoint.");
                        assertions.Equal(saveCheckpointHash, verifiedAhead.EntryHashes[checked((int)saveCheckpointPosition - 1)], "The simulated save checkpoint is an exact hash-verified journal prefix.");

                        var recoveryLedger = new InMemoryEventLedger();
                        var recoveryMemories = new MemoryIndex();
                        LoadRecords(verifiedAhead.Records.Take(checked((int)saveCheckpointPosition)), recoveryLedger, recoveryMemories);
                        assertions.Equal(checked((int)saveCheckpointPosition), recoveryLedger.Snapshot().Count, "Forward recovery exposes only the checkpoint prefix before explicit adoption.");
                        assertions.Equal(checked((int)saveCheckpointPosition), recoveryMemories.Count, "Forward recovery exposes only checkpoint memories before explicit adoption.");
                        LoadRecords(verifiedAhead.Records.Skip(checked((int)saveCheckpointPosition)), recoveryLedger, recoveryMemories);
                        assertions.Equal(expectedExperienceCount, recoveryLedger.Snapshot().Count, "Explicit forward recovery adopts the one verified newer event exactly once.");
                        assertions.Equal(expectedExperienceCount, recoveryMemories.Count, "Explicit forward recovery adopts the one verified newer memory exactly once.");
                        forwardRecoveryCount++;
                    }
                }

                var finalArchive = archive.Load(archivePath);
                var finalArchiveSnapshot = finalArchive.Snapshot
                    ?? throw new HarnessAssertionException("Final torture identity archive was missing.");
                var finalIdentities = finalArchiveSnapshot.Records.ToDictionary(
                    value => value.ExternalEntityId,
                    value => value.State,
                    StringComparer.Ordinal);
                AssertIdentityContinuity(assertions, finalIdentities, originalContinuity, cycleCount + 1);

                var finalJournal = journal.Load(journalPath);
                assertions.Equal(ExperienceJournalLoadStatus.Loaded, finalJournal.Status, "Final torture experience journal verifies.");
                assertions.Equal(cycleCount * ordinaryEventsPerCycle + 1, finalJournal.Records.Count, "Final torture journal contains every expected ordinary and forward-recovered experience.");
                AssertUniqueExperienceRecords(assertions, finalJournal.Records, cycleCount + 1);

                var finalReflection = reflectionStore.Load(reflectionPath);
                assertions.Equal(ReflectionStoreLoadStatus.LoadedPrimary, finalReflection.Status, "Final torture reflection sidecar verifies.");
                var finalReflectionSnapshot = finalReflection.Snapshot
                    ?? throw new HarnessAssertionException("Final torture reflection sidecar was missing.");
                assertions.Equal(cycleCount * ordinaryEventsPerCycle, finalReflectionSnapshot.PendingTasks.Count, "Every queued reflection created during provider outage survives all torture cycles.");
                assertions.Equal(cycleCount, finalReflectionSnapshot.AuditRecords.Count, "Every simulated provider failure has exactly one durable audit record.");
                AssertUniqueReflectionRecords(assertions, finalReflectionSnapshot.PendingTasks, finalReflectionSnapshot.AuditRecords, cycleCount + 1);
                assertions.Equal(1, forwardRecoveryCount, "Forward-only external-journal recovery executes exactly once.");

                metrics["cycles"] = cycleCount;
                metrics["individuals"] = finalIdentities.Count;
                metrics["experiences"] = finalJournal.Records.Count;
                metrics["memories"] = finalJournal.Records.Count(value => value.Memory is not null);
                metrics["queuedReflections"] = finalReflectionSnapshot.PendingTasks.Count;
                metrics["providerFailures"] = finalReflectionSnapshot.AuditRecords.Count;
                metrics["forwardRecoveries"] = forwardRecoveryCount;
                return new ScenarioExecution(assertions.Assertions, metrics);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void AppendTortureRecord(
            DurableExperienceJournal journal,
            string journalPath,
            EnvironmentEvent value,
            DeterministicExperienceResult encoded,
            InMemoryEventLedger ledger,
            MemoryIndex memoryIndex,
            ref long position,
            ref string lastHash)
        {
            var appended = journal.Append(
                journalPath,
                new ExperienceJournalRecord(value, encoded.Perception, encoded.Memory),
                position,
                lastHash);
            position = appended.Position;
            lastHash = appended.EntryHash;
            var ledgerResult = ledger.Append(value);
            if (ledgerResult.Status != EventAppendStatus.Appended)
            {
                throw new HarnessAssertionException("A newly appended torture event was unexpectedly deduplicated.");
            }
            memoryIndex.Add(encoded.Memory);
        }

        private static ReflectionTask CreateTortureReflectionTask(
            IndividualId ownerId,
            EnvironmentEvent value,
            DateTimeOffset createdAtUtc)
        {
            return new ReflectionTask(
                ReflectionTaskId.New(),
                ownerId,
                ModelTaskKind.InterpretMeaningfulEvent,
                ReflectionPriority.MeaningfulEvent,
                createdAtUtc,
                "persistence-torture:" + value.Id,
                new[] { value.Id },
                700);
        }

        private static ReflectionAuditRecord CreateTortureFailureAudit(
            PendingReflectionTask pending,
            ModelRequest request,
            IndividualState state,
            ModelResult result,
            DateTimeOffset occurredAtUtc)
        {
            return new ReflectionAuditRecord(
                ReflectionRecordId.New(),
                pending.Task.Id,
                request.Id,
                state.Id,
                state.LineageId,
                request.BaseStateVersion,
                request.TaskKind,
                request.PromptVersion,
                occurredAtUtc,
                ReflectionAuditStatus.AttemptFailed,
                pending.AttemptCount + 1,
                result.Provider,
                result.Model,
                result.ProviderOperationId,
                result.Status,
                result.ErrorCode,
                result.ErrorMessage,
                result.Retryable,
                pending.Task.EstimatedTokens,
                result.PromptTokens,
                result.OutputTokens,
                result.TotalTokens,
                request.Context,
                result.StructuredPayload,
                "Persistence-torture provider outage fixture.");
        }

        private static void AssertIdentityContinuity(
            HarnessAssert assertions,
            IReadOnlyDictionary<string, IndividualState> identities,
            IReadOnlyDictionary<string, (IndividualId Id, LineageId LineageId)> originals,
            int cycle)
        {
            assertions.Equal(originals.Count, identities.Count, "No identity record is lost or duplicated at torture cycle " + cycle + ".");
            assertions.Equal(identities.Count, identities.Keys.Distinct(StringComparer.Ordinal).Count(), "External identity keys remain unique at torture cycle " + cycle + ".");
            assertions.Equal(identities.Count, identities.Values.Select(value => value.Id).Distinct().Count(), "IndividualIds remain unique at torture cycle " + cycle + ".");
            foreach (var pair in identities)
            {
                var expected = originals[pair.Key];
                assertions.Equal(expected.Id, pair.Value.Id, "IndividualId continuity survives torture cycle " + cycle + " for " + pair.Key + ".");
                assertions.Equal(expected.LineageId, pair.Value.LineageId, "LineageId continuity survives torture cycle " + cycle + " for " + pair.Key + ".");
            }
        }

        private static void AssertUniqueExperienceRecords(
            HarnessAssert assertions,
            IReadOnlyList<ExperienceJournalRecord> records,
            int cycle)
        {
            assertions.Equal(records.Count, records.Select(value => value.FactualEvent.Id).Distinct().Count(), "Experience EventIds remain unique at torture cycle " + cycle + ".");
            assertions.Equal(records.Count, records.Select(value => value.FactualEvent.DeduplicationKey).Distinct(StringComparer.Ordinal).Count(), "Experience deduplication keys remain unique at torture cycle " + cycle + ".");
            var memories = records.Where(value => value.Memory is not null).Select(value => value.Memory!).ToList();
            var perceptions = records.Where(value => value.Perception is not null).Select(value => value.Perception!).ToList();
            assertions.Equal(records.Count, memories.Count, "Every torture experience retains exactly one subjective memory at cycle " + cycle + ".");
            assertions.Equal(records.Count, perceptions.Count, "Every torture experience retains exactly one perception at cycle " + cycle + ".");
            assertions.Equal(memories.Count, memories.Select(value => value.Id).Distinct().Count(), "MemoryIds remain unique at torture cycle " + cycle + ".");
            assertions.Equal(perceptions.Count, perceptions.Select(value => value.Id).Distinct().Count(), "PerceptionIds remain unique at torture cycle " + cycle + ".");
        }

        private static void AssertUniqueReflectionRecords(
            HarnessAssert assertions,
            IReadOnlyList<PendingReflectionTask> pending,
            IReadOnlyList<ReflectionAuditRecord> audit,
            int cycle)
        {
            assertions.Equal(pending.Count, pending.Select(value => value.Task.Id).Distinct().Count(), "Pending reflection task IDs remain unique at torture cycle " + cycle + ".");
            var sourceIds = pending.SelectMany(value => value.Task.SourceEventIds).ToList();
            assertions.Equal(sourceIds.Count, sourceIds.Distinct().Count(), "Pending reflection source-event links remain nonduplicated at torture cycle " + cycle + ".");
            assertions.Equal(audit.Count, audit.Select(value => value.Id).Distinct().Count(), "Reflection audit record IDs remain unique at torture cycle " + cycle + ".");
            assertions.Equal(audit.Count, audit.Select(value => value.RequestId).Distinct().Count(), "Each torture provider attempt has one durable request record at cycle " + cycle + ".");
        }
    }
}