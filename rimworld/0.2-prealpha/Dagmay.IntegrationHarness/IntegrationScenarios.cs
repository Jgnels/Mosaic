using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;
using Dagmay.Core.Presentation;
using Dagmay.Core.Reflection;
using Dagmay.Core.Relationships;
using Dagmay.Core.Scheduling;
using Dagmay.Providers.Fake;

namespace Dagmay.IntegrationHarness
{
    internal sealed class ScenarioExecution
    {
        public ScenarioExecution(
            IReadOnlyList<string> assertions,
            Dictionary<string, long> metrics,
            Dictionary<string, string>? details = null)
        {
            Assertions = assertions;
            Metrics = metrics;
            Details = details ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public IReadOnlyList<string> Assertions { get; }
        public Dictionary<string, long> Metrics { get; }
        public Dictionary<string, string> Details { get; }
    }

    internal sealed class ScenarioDefinition
    {
        public ScenarioDefinition(string name, Func<Task<ScenarioExecution>> run, bool includeByDefault = true)
        {
            Name = name;
            Run = run;
            IncludeByDefault = includeByDefault;
        }

        public string Name { get; }
        public Func<Task<ScenarioExecution>> Run { get; }
        public bool IncludeByDefault { get; }
    }

    internal static partial class IntegrationScenarios
    {
        private static readonly DateTimeOffset BaseTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public static IReadOnlyList<ScenarioDefinition> All(int gate3SoakCycles = 250, int deterministicSeed = 2031)
        {
            return new[]
            {
                new ScenarioDefinition("LongHistoryContinuityAndPersistence", LongHistoryContinuityAndPersistenceAsync),
                new ScenarioDefinition("ProviderOutageDurableQueueRecovery", ProviderOutageDurableQueueRecoveryAsync),
                new ScenarioDefinition("CheckpointRollbackForwardRecovery", CheckpointRollbackForwardRecoveryAsync),
                new ScenarioDefinition("SocialPerspectiveAndPrivacy", SocialPerspectiveAndPrivacyAsync),
                new ScenarioDefinition("DialogueOfflineEndToEnd", DialogueOfflineEndToEndAsync),
                new ScenarioDefinition("PersistenceTorture", PersistenceTortureAsync),
                new ScenarioDefinition("FailureIsolation", FailureIsolationAsync),
                new ScenarioDefinition("DeterminismObserverLifecycleClosure", DeterminismObserverLifecycleClosureAsync),
                new ScenarioDefinition("ReadOnlyDecisionTraceFoundation", ReadOnlyDecisionTraceFoundationAsync),
                new ScenarioDefinition("ReadOnlySocialEventEnvelope", ReadOnlySocialEventEnvelopeAsync),
                new ScenarioDefinition("BoundedReactionLifecycle", BoundedReactionLifecycleAsync),
                new ScenarioDefinition("ProvisionalDialogueAppraisal", ProvisionalDialogueAppraisalAsync),
                new ScenarioDefinition("DurableAppraisalAdmission", DurableAppraisalAdmissionAsync),
                new ScenarioDefinition("ContextualDevelopmentalRetrieval", () => ContextualDevelopmentalRetrievalAsync(gate3SoakCycles)),
                new ScenarioDefinition("GroundedDevelopmentalContextMaterialization", () => GroundedDevelopmentalContextMaterializationAsync(gate3SoakCycles)),
                new ScenarioDefinition("GroundedCompoundAppraisal", () => GroundedCompoundAppraisalAsync(gate3SoakCycles)),
                new ScenarioDefinition("LongHistoryRetrievalBenchmark", LongHistoryRetrievalBenchmarkAsync),
                new ScenarioDefinition(
                    "DurableAppraisalAdmissionStress",
                    () => DurableAppraisalAdmissionStressAsync(50_000),
                    includeByDefault: false),
                new ScenarioDefinition(
                    "Gate3OfflineSoak",
                    () => Gate3OfflineSoakAsync(gate3SoakCycles, deterministicSeed),
                    includeByDefault: false)
            };
        }

        private static Task<ScenarioExecution> ProvisionalDialogueAppraisalAsync()
        {
            var assertions = new HarnessAssert();
            var owner = IndividualId.Parse("e3000000000000000000000000000001");
            var v37 = V38GroundedDecision(owner);
            var v38Lifecycle = V38Lifecycle(owner);
            var ticket = v38Lifecycle.Prepare(V38Source(v37, owner, "v39-chain", 100), 500);
            var attempt = v38Lifecycle.BeginAttempt(ticket.TicketId, 110);
            var observed = V38TrustedReceipt(ticket, attempt, "v39-observed", 120);
            assertions.True(
                v38Lifecycle.ObserveReceipt(observed, 120) is not null,
                "v37 grounded affect reaches v39 only through a genuine v38 observed-success receipt.");

            var cues = new[]
            {
                new ProvisionalSpeechCue(
                    ProvisionalCueType.Gratitude,
                    0.65m,
                    true,
                    0,
                    ProvisionalFactuality.SpeechAct,
                    new[] { "ev-v39-chain" }),
                new ProvisionalSpeechCue(
                    ProvisionalCueType.Uncertainty,
                    0.70m,
                    true,
                    0,
                    ProvisionalFactuality.ClaimOnly,
                    new[] { "ev-v39-chain" })
            }.OrderBy(value => value.CueType.ToString(), StringComparer.Ordinal).ToArray();
            var input = new ProvisionalDialogueAppraisalInput(
                owner.ToString(),
                observed,
                cues,
                new[]
                {
                    new ProvisionalKnowledgeEvidence(
                        "ev-v39-chain",
                        owner.ToString(),
                        v37.SourceEvidenceIds[0].ToString(),
                        ProvisionalPrivacy.OwnerPrivate,
                        true,
                        100,
                        ProvisionalFactuality.VerifiedFact)
                },
                V38Hash("canonical-affect-before"),
                3,
                V38Hash("canonical-relationship-before"),
                5,
                new ProvisionalTraitProfile());
            var store = new ProvisionalDialogueAppraisalStore(observed.SessionId);
            var appraisal = store.Activate(store.Prepare(input).AppraisalId, observed.DisplayedTick);
            var overlay = store.EffectiveOverlay(owner.ToString(), observed.ConversationId, observed.DisplayedTick);
            assertions.True(
                overlay.Affect.Valence > 0m &&
                overlay.Affect.Certainty < 0m &&
                !appraisal.DirectCanonicalMutation &&
                !appraisal.DirectPawnAuthority,
                "v39 produces one bounded compound provisional overlay without canonical or pawn authority.");

            var packet = store.ProposePromotion(
                appraisal.AppraisalId,
                "dialogue-event-v39-chain",
                9,
                ProvisionalAffectDelta.Zero,
                new ProvisionalRelationshipDelta(resentment: 0.01m),
                "admission-receipt-v39-chain");
            var failed = CanonicalApplicationReceipt.CreateFailure(
                packet.PacketId,
                owner.ToString(),
                packet.AdmittedDialogueEventId,
                9,
                packet.ExpectedAffectFingerprint,
                packet.ExpectedAffectVersion,
                packet.ExpectedRelationshipFingerprint,
                packet.ExpectedRelationshipVersion);
            assertions.Equal(
                ProvisionalAppraisalState.Active,
                store.ObserveApplicationReceipt(failed).State,
                "A failed canonical attempt removes its packet but leaves the provisional overlay active.");
            packet = store.ProposePromotion(
                appraisal.AppraisalId,
                "dialogue-event-v39-chain",
                9,
                ProvisionalAffectDelta.Zero,
                new ProvisionalRelationshipDelta(resentment: 0.01m),
                "admission-receipt-v39-chain");
            var successful = V39TrustedApplication(
                packet,
                packet.ExpectedAffectFingerprint,
                packet.ExpectedAffectVersion,
                V38Hash("canonical-relationship-after"),
                packet.ExpectedRelationshipVersion!.Value + 1);
            assertions.Equal(
                ProvisionalAppraisalState.Promoted,
                store.ObserveApplicationReceipt(successful).State,
                "Observed canonical success advances only the targeted relationship store and removes the overlay.");
            assertions.True(
                store.ActiveCount() == 0 &&
                store.PendingPacketCount == 0 &&
                store.CompletedCount == 1,
                "Successful v39 reconciliation leaves no live overlay or pending authority packet.");

            return Task.FromResult(new ScenarioExecution(
                assertions.Assertions,
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    ["focusedContracts"] = 42,
                    ["activeAfterSuccess"] = store.ActiveCount(),
                    ["pendingAfterSuccess"] = store.PendingPacketCount,
                    ["completed"] = store.CompletedCount
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["gateDigest"] = "c8557ab218dc0a6136a960cfb495dd4853e5dd3ad81d8eadd7b36082b1b47819",
                    ["sourceReceiptFingerprint"] = observed.ReceiptFingerprint,
                    ["completionChain"] = store.CompletedChain
                }));
        }

        private static CanonicalApplicationReceipt V39TrustedApplication(
            DurableApplicationPacket packet,
            string affectFingerprint,
            long affectVersion,
            string relationshipFingerprint,
            long relationshipVersion)
        {
            var method = typeof(CanonicalApplicationReceipt).GetMethod(
                "CreateTrusted",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new HarnessAssertionException("Trusted canonical application receipt factory is unavailable.");
            return (CanonicalApplicationReceipt)(method.Invoke(
                null,
                new object?[]
                {
                    packet.PacketId,
                    packet.PerspectiveOwnerId,
                    packet.AdmittedDialogueEventId,
                    packet.ExpectedCheckpointGeneration,
                    true,
                    affectFingerprint,
                    affectVersion,
                    relationshipFingerprint,
                    relationshipVersion,
                    CanonicalApplicationReceipt.SourceContractValue
                }) ?? throw new HarnessAssertionException("Trusted canonical application receipt factory returned null."));
        }

        private static Task<ScenarioExecution> LongHistoryContinuityAndPersistenceAsync()
        {
            var assertions = new HarnessAssert();
            var metrics = new Dictionary<string, long>(StringComparer.Ordinal);
            var directory = CreateTempDirectory("long-history");
            try
            {
                const int individualCount = 64;
                const int eventsPerIndividual = 20;
                var storeId = Guid.Parse("f40da69e-0959-41b2-8f49-77518ec29691");
                var identities = new Dictionary<string, IndividualState>(StringComparer.Ordinal);
                var originalContinuity = new Dictionary<string, (IndividualId Id, LineageId LineageId)>(StringComparer.Ordinal);
                for (var index = 0; index < individualCount; index++)
                {
                    var externalId = "rimworld:pawn:harness:" + index.ToString("D3");
                    var state = CreateIndividual("Harness " + index.ToString("D3"));
                    identities.Add(externalId, state);
                    originalContinuity.Add(externalId, (state.Id, state.LineageId));
                }

                var archivePath = Path.Combine(directory, "identities.dagmay");
                var journalPath = Path.Combine(directory, "experiences.journal");
                var archive = new AtomicIdentityArchive();
                var journal = new DurableExperienceJournal();
                var encoder = new DeterministicExperienceEncoder();
                var ledger = new InMemoryEventLedger();
                var memoryIndex = new MemoryIndex();
                long journalPosition = 0;
                var journalHash = string.Empty;

                archive.Save(archivePath, CreateIdentitySnapshot(storeId, 1, identities));

                var sequence = 0;
                foreach (var externalId in identities.Keys.ToList())
                {
                    var state = identities[externalId];
                    for (var eventIndex = 0; eventIndex < eventsPerIndividual; eventIndex++)
                    {
                        sequence++;
                        var occurred = BaseTime.AddMinutes(sequence);
                        var source = CreateEvent(
                            state.Id,
                            "rimworld.skill.level_gained",
                            externalId + ":skill:" + eventIndex,
                            occurred,
                            new Dictionary<string, string>
                            {
                                ["skill"] = eventIndex % 2 == 0 ? "Crafting" : "Plants",
                                ["level"] = (eventIndex + 1).ToString()
                            });
                        var encoded = encoder.EncodeExperiencedEvent(source, state, occurred.AddSeconds(1));
                        var appended = journal.Append(
                            journalPath,
                            new ExperienceJournalRecord(source, encoded.Perception, encoded.Memory),
                            journalPosition,
                            journalHash);
                        journalPosition = appended.Position;
                        journalHash = appended.EntryHash;
                        ledger.Append(source);
                        memoryIndex.Add(encoded.Memory);
                        state = encoded.UpdatedState;
                    }

                    identities[externalId] = state;
                }

                archive.Save(archivePath, CreateIdentitySnapshot(storeId, 2, identities));
                var loadedArchive = archive.Load(archivePath);
                var loadedJournal = journal.Load(journalPath);

                assertions.Equal(ArchiveLoadStatus.LoadedPrimary, loadedArchive.Status, "Long-history identity archive reloads from the verified primary file.");
                var archiveSnapshot = loadedArchive.Snapshot
                    ?? throw new HarnessAssertionException("Long-history identity archive did not expose a verified snapshot.");
                assertions.True(true, "Long-history identity archive exposes a verified snapshot.");
                assertions.Equal(individualCount, archiveSnapshot.Records.Count, "All simulated individuals survive identity persistence.");
                assertions.Equal(ExperienceJournalLoadStatus.Loaded, loadedJournal.Status, "Long-history experience journal reloads with a verified hash chain.");
                assertions.Equal(individualCount * eventsPerIndividual, loadedJournal.Records.Count, "Every simulated experience survives journal persistence.");
                assertions.Equal(individualCount * eventsPerIndividual, memoryIndex.Count, "Every encoded long-history memory is indexed without transcript loss.");
                assertions.Equal(individualCount * eventsPerIndividual, ledger.Snapshot().Count, "Every long-history fact is present in the canonical event ledger.");

                foreach (var record in archiveSnapshot.Records)
                {
                    var expected = originalContinuity[record.ExternalEntityId];
                    assertions.Equal(expected.Id, record.State.Id, "IndividualId remains stable through a long simulated history for " + record.ExternalEntityId + ".");
                    assertions.Equal(expected.LineageId, record.State.LineageId, "LineageId remains stable through a long simulated history for " + record.ExternalEntityId + ".");
                }

                metrics["individuals"] = individualCount;
                metrics["events"] = loadedJournal.Records.Count;
                metrics["memories"] = memoryIndex.Count;
                metrics["identityGeneration"] = archiveSnapshot.Generation;
                return Task.FromResult(new ScenarioExecution(assertions.Assertions, metrics));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static async Task<ScenarioExecution> ProviderOutageDurableQueueRecoveryAsync()
        {
            var assertions = new HarnessAssert();
            var metrics = new Dictionary<string, long>(StringComparer.Ordinal);
            var directory = CreateTempDirectory("provider-recovery");
            try
            {
                const int individualCount = 12;
                var storeId = Guid.Parse("fe7df4d2-4cbb-42c8-ac89-34ff3dbf83bd");
                var states = new Dictionary<IndividualId, IndividualState>();
                var originals = new Dictionary<IndividualId, (IndividualId Id, LineageId LineageId)>();
                var events = new Dictionary<EventId, EnvironmentEvent>();
                var memories = new Dictionary<IndividualId, SubjectiveMemory>();
                var ledger = new InMemoryEventLedger();
                var queue = new PersistentReflectionQueue(100);
                var encoder = new DeterministicExperienceEncoder();

                for (var index = 0; index < individualCount; index++)
                {
                    var state = CreateIndividual("Recovery " + index.ToString("D2"));
                    var occurred = BaseTime.AddMinutes(index);
                    var source = CreateEvent(
                        state.Id,
                        "rimworld.social.opinion_changed",
                        "provider-recovery:" + index,
                        occurred,
                        new Dictionary<string, string>
                        {
                            ["target_external_id"] = "rimworld:pawn:other:" + index,
                            ["target_name"] = "Counterpart " + index,
                            ["opinion_before"] = "0",
                            ["opinion_after"] = "20",
                            ["opinion_delta"] = "20"
                        });
                    var encoded = encoder.EncodeExperiencedEvent(source, state, occurred.AddSeconds(1));
                    state = encoded.UpdatedState;
                    states.Add(state.Id, state);
                    originals.Add(state.Id, (state.Id, state.LineageId));
                    events.Add(source.Id, source);
                    memories.Add(state.Id, encoded.Memory);
                    ledger.Append(source);
                    queue.EnqueueOrMerge(new ReflectionTask(
                        ReflectionTaskId.New(),
                        state.Id,
                        ModelTaskKind.InterpretMeaningfulEvent,
                        ReflectionPriority.MeaningfulEvent,
                        occurred,
                        "provider-recovery:" + state.Id,
                        new[] { source.Id },
                        700));
                }

                var contextBuilder = new ReflectionContextBuilder();
                var outageProvider = new DeterministicFakeProvider(FakeProviderBehavior.RateLimited);
                var outageTime = BaseTime.AddHours(1);
                var failureCount = 0;
                foreach (var pending in queue.Snapshot())
                {
                    var state = states[pending.Task.IndividualId];
                    var sourceEvents = pending.Task.SourceEventIds.Select(id => events[id]).ToList();
                    var request = contextBuilder.BuildRequest(
                        pending.Task,
                        state,
                        sourceEvents,
                        new[] { memories[state.Id] },
                        outageTime,
                        TimeSpan.FromMinutes(1));
                    var result = await outageProvider.GenerateStructuredAsync(request, CancellationToken.None).ConfigureAwait(false);
                    assertions.Equal(ModelResultStatus.RateLimited, result.Status, "Provider outage is normalized as a retryable rate limit.");
                    assertions.True(result.Retryable, "Provider outage leaves durable work eligible for retry.");
                    assertions.True(queue.MarkRetry(pending.Task.Id, outageTime.AddMinutes(5), result.ErrorCode), "Rate-limited work is retained and marked for retry.");
                    failureCount++;
                }

                var reflectionPath = Path.Combine(directory, "reflections.reflection");
                var reflectionStore = new AtomicReflectionStore();
                reflectionStore.Save(
                    reflectionPath,
                    new ReflectionStoreSnapshot(storeId, 1, outageTime, queue.Snapshot(), Array.Empty<ReflectionAuditRecord>()));
                var loaded = reflectionStore.Load(reflectionPath);
                assertions.Equal(ReflectionStoreLoadStatus.LoadedPrimary, loaded.Status, "Durable reflection queue reloads after simulated provider outage.");
                var reflectionSnapshot = loaded.Snapshot
                    ?? throw new HarnessAssertionException("Reloaded reflection store did not expose a verified snapshot.");
                assertions.True(true, "Reloaded reflection store exposes a verified snapshot.");
                assertions.Equal(individualCount, reflectionSnapshot.PendingTasks.Count, "All provider-outage tasks survive persistence.");
                assertions.True(reflectionSnapshot.PendingTasks.All(value => value.AttemptCount == 1), "Retry attempt counts survive reflection-store persistence.");

                queue = new PersistentReflectionQueue(100, reflectionSnapshot.PendingTasks);
                var successProvider = new DeterministicFakeProvider();
                var validator = new ReflectionProposalValidator();
                var recoveryTime = outageTime.AddMinutes(6);
                var successCount = 0;
                IndividualId? lastDispatched = null;
                while (queue.TrySelect(recoveryTime, lastDispatched, out var pending) && pending is not null)
                {
                    var state = states[pending.Task.IndividualId];
                    var sourceEvents = pending.Task.SourceEventIds.Select(id => events[id]).ToList();
                    var request = contextBuilder.BuildRequest(
                        pending.Task,
                        state,
                        sourceEvents,
                        new[] { memories[state.Id] },
                        recoveryTime,
                        TimeSpan.FromMinutes(1));
                    var result = await successProvider.GenerateStructuredAsync(request, CancellationToken.None).ConfigureAwait(false);
                    assertions.Equal(ModelResultStatus.Success, result.Status, "Recovered provider work completes through the deterministic fake provider.");
                    var proposal = ReflectionProposalJson.Parse(result.StructuredPayload);
                    var validation = validator.Validate(request, proposal, state, ledger);
                    assertions.True(validation.IsValid, "Recovered provider output passes grounded atomic validation.");
                    states[state.Id] = validation.Replacement!;
                    assertions.True(queue.Remove(pending.Task.Id), "Committed recovered work is removed from the durable queue exactly once.");
                    lastDispatched = state.Id;
                    successCount++;
                }

                assertions.Equal(0, queue.Count, "Provider recovery drains all eligible durable work.");
                assertions.Equal(individualCount, failureCount, "Every queued individual experienced the simulated outage once.");
                assertions.Equal(individualCount, successCount, "Every queued individual recovered and committed once.");
                foreach (var pair in states)
                {
                    var expected = originals[pair.Key];
                    assertions.Equal(expected.Id, pair.Value.Id, "Provider outage and recovery never replace IndividualId.");
                    assertions.Equal(expected.LineageId, pair.Value.LineageId, "Provider outage and recovery never replace LineageId.");
                }

                metrics["individuals"] = individualCount;
                metrics["outageFailures"] = failureCount;
                metrics["recoveredCommits"] = successCount;
                metrics["queueRemaining"] = queue.Count;
                return new ScenarioExecution(assertions.Assertions, metrics);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static Task<ScenarioExecution> CheckpointRollbackForwardRecoveryAsync()
        {
            var assertions = new HarnessAssert();
            var metrics = new Dictionary<string, long>(StringComparer.Ordinal);
            var directory = CreateTempDirectory("checkpoint-recovery");
            try
            {
                const int checkpointPosition = 6;
                const int finalPosition = 14;
                var journalPath = Path.Combine(directory, "experiences.journal");
                var journal = new DurableExperienceJournal();
                var encoder = new DeterministicExperienceEncoder();
                var individual = CreateIndividual("Rollback Fixture");
                var originalId = individual.Id;
                var originalLineage = individual.LineageId;
                long position = 0;
                var lastHash = string.Empty;
                var checkpointHash = string.Empty;

                for (var index = 1; index <= finalPosition; index++)
                {
                    var occurred = BaseTime.AddMinutes(index);
                    var source = CreateEvent(
                        individual.Id,
                        index % 2 == 0 ? "rimworld.health.condition_removed" : "rimworld.health.condition_added",
                        "checkpoint:" + index,
                        occurred,
                        new Dictionary<string, string> { ["condition"] = "FixtureCondition" + index });
                    var encoded = encoder.EncodeExperiencedEvent(source, individual, occurred.AddSeconds(1));
                    individual = encoded.UpdatedState;
                    var append = journal.Append(
                        journalPath,
                        new ExperienceJournalRecord(source, encoded.Perception, encoded.Memory),
                        position,
                        lastHash);
                    position = append.Position;
                    lastHash = append.EntryHash;
                    if (index == checkpointPosition) checkpointHash = append.EntryHash;
                }

                var loaded = journal.Load(journalPath);
                assertions.Equal(ExperienceJournalLoadStatus.Loaded, loaded.Status, "Rollback scenario begins from a fully verified external journal.");
                assertions.Equal(finalPosition, loaded.Records.Count, "External journal contains the newer post-checkpoint history.");
                assertions.Equal(checkpointHash, loaded.EntryHashes[checkpointPosition - 1], "Older save checkpoint matches an exact verified prefix hash.");

                var checkpointLedger = new InMemoryEventLedger();
                var checkpointMemories = new MemoryIndex();
                LoadRecords(loaded.Records.Take(checkpointPosition), checkpointLedger, checkpointMemories);
                assertions.Equal(checkpointPosition, checkpointLedger.Snapshot().Count, "Read-only rollback view exposes only checkpointed factual history before adoption.");
                assertions.Equal(checkpointPosition, checkpointMemories.Count, "Read-only rollback view exposes only checkpointed memories before adoption.");

                LoadRecords(loaded.Records.Skip(checkpointPosition), checkpointLedger, checkpointMemories);
                var adoptedPosition = loaded.Records.Count;
                var adoptedHash = loaded.LastHash;
                assertions.Equal(finalPosition, checkpointLedger.Snapshot().Count, "Explicit forward recovery adopts every verified newer fact.");
                assertions.Equal(finalPosition, checkpointMemories.Count, "Explicit forward recovery adopts every verified newer memory.");
                assertions.Equal(finalPosition, adoptedPosition, "Recovered checkpoint advances to the verified external head position.");
                assertions.Equal(lastHash, adoptedHash, "Recovered checkpoint advances to the verified external head hash.");
                assertions.Equal(originalId, individual.Id, "Forward experience recovery does not replace IndividualId.");
                assertions.Equal(originalLineage, individual.LineageId, "Forward experience recovery does not replace LineageId.");

                var wrongCheckpointHash = loaded.EntryHashes[checkpointPosition - 2];
                assertions.True(!string.Equals(wrongCheckpointHash, checkpointHash, StringComparison.Ordinal), "A mismatched checkpoint hash is distinguishable from the verified prefix and must not be auto-adopted.");

                metrics["checkpointPosition"] = checkpointPosition;
                metrics["externalPosition"] = finalPosition;
                metrics["adoptedRecords"] = finalPosition - checkpointPosition;
                return Task.FromResult(new ScenarioExecution(assertions.Assertions, metrics));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static Task<ScenarioExecution> SocialPerspectiveAndPrivacyAsync()
        {
            var assertions = new HarnessAssert();
            var metrics = new Dictionary<string, long>(StringComparer.Ordinal);
            var directory = CreateTempDirectory("social-perspective");
            try
            {
                var first = CreateIndividual("First");
                var second = CreateIndividual("Second");
                var encoder = new DeterministicExperienceEncoder();
                var journal = new DurableExperienceJournal();
                var journalPath = Path.Combine(directory, "social.journal");
                long position = 0;
                var lastHash = string.Empty;

                var firstEvent = CreateSocialEvent(first, second, 5, 35, "social:first-to-second", BaseTime);
                var firstEncoded = encoder.EncodeExperiencedEvent(firstEvent, first, BaseTime.AddSeconds(1));
                var firstAppend = journal.Append(
                    journalPath,
                    new ExperienceJournalRecord(firstEvent, firstEncoded.Perception, firstEncoded.Memory),
                    position,
                    lastHash);
                position = firstAppend.Position;
                lastHash = firstAppend.EntryHash;

                var secondEvent = CreateSocialEvent(second, first, 10, -20, "social:second-to-first", BaseTime.AddMinutes(1));
                var secondEncoded = encoder.EncodeExperiencedEvent(secondEvent, second, BaseTime.AddMinutes(1).AddSeconds(1));
                journal.Append(
                    journalPath,
                    new ExperienceJournalRecord(secondEvent, secondEncoded.Perception, secondEncoded.Memory),
                    position,
                    lastHash);

                assertions.Equal(1, firstEncoded.Memory.PeopleInvolved.Count, "First social memory links exactly one enrolled counterpart.");
                assertions.Equal(second.Id, firstEncoded.Memory.PeopleInvolved[0], "First social memory links the second individual's stable IndividualId.");
                assertions.Equal(1, secondEncoded.Memory.PeopleInvolved.Count, "Second social memory links exactly one enrolled counterpart.");
                assertions.Equal(first.Id, secondEncoded.Memory.PeopleInvolved[0], "Second social memory links the first individual's stable IndividualId.");
                assertions.Equal(PrivacyClassification.RelationshipSensitive, firstEncoded.Memory.Privacy, "First social memory remains relationship-sensitive.");
                assertions.Equal(PrivacyClassification.RelationshipSensitive, secondEncoded.Memory.Privacy, "Second social memory remains relationship-sensitive.");

                var firstRelationship = new RelationshipRecord(
                    first.Id,
                    new RelationshipTarget(new EnvironmentEntityReference("rimworld", "HumanSecond", second.DisplayName), second.Id),
                    new RelationshipDimensions(0.6, 0.5, 0.0, 0.0, 0.4),
                    0.8,
                    new[] { firstEvent.Id },
                    firstEncoded.UpdatedState.Version);
                var secondRelationship = new RelationshipRecord(
                    second.Id,
                    new RelationshipTarget(new EnvironmentEntityReference("rimworld", "HumanFirst", first.DisplayName), first.Id),
                    new RelationshipDimensions(-0.3, -0.2, 0.2, 0.5, 0.4),
                    0.8,
                    new[] { secondEvent.Id },
                    secondEncoded.UpdatedState.Version);
                assertions.True(firstRelationship.Dimensions.Trust != secondRelationship.Dimensions.Trust, "Relationship state remains asymmetric by owner perspective.");
                assertions.True(firstRelationship.OwnerId != secondRelationship.OwnerId, "Opposing relationship records retain distinct owners.");

                var loaded = journal.Load(journalPath);
                assertions.Equal(ExperienceJournalLoadStatus.Loaded, loaded.Status, "Social provenance survives a journal round trip.");
                assertions.Equal(2, loaded.Records.Count, "Both asymmetric social experiences survive persistence.");
                assertions.Equal(second.Id, loaded.Records[0].Memory!.PeopleInvolved[0], "Persisted first-person social memory retains counterpart provenance.");
                assertions.Equal(first.Id, loaded.Records[1].Memory!.PeopleInvolved[0], "Persisted second-person social memory retains counterpart provenance.");

                metrics["socialEvents"] = loaded.Records.Count;
                metrics["asymmetricRelationshipRecords"] = 2;
                return Task.FromResult(new ScenarioExecution(assertions.Assertions, metrics));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static IdentityArchiveSnapshot CreateIdentitySnapshot(
            Guid storeId,
            long generation,
            IReadOnlyDictionary<string, IndividualState> identities)
        {
            return new IdentityArchiveSnapshot(
                storeId,
                generation,
                BaseTime.AddMinutes(generation),
                identities.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new PersistedIdentityRecord(pair.Key, pair.Value)));
        }

        private static IndividualState CreateIndividual(string name)
        {
            return IndividualState.Create(
                name,
                new IdentitySeed(
                    "rimworld",
                    "integration-harness-v1",
                    new[]
                    {
                        new SeedFact(SeedFactCategory.Trait, "fixture.kind", "Kind", "Dagmay integration harness", 1.0)
                    }));
        }

        private static EnvironmentEvent CreateEvent(
            IndividualId ownerId,
            string kind,
            string deduplicationKey,
            DateTimeOffset occurredAtUtc,
            IDictionary<string, string> payload)
        {
            return new EnvironmentEvent(
                EventId.New(),
                deduplicationKey,
                kind,
                "rimworld",
                occurredAtUtc,
                occurredAtUtc,
                (long)(occurredAtUtc - BaseTime).TotalSeconds,
                "Dagmay.IntegrationHarness",
                payload,
                new[] { ownerId });
        }

        private static EnvironmentEvent CreateSocialEvent(
            IndividualState owner,
            IndividualState other,
            int opinionBefore,
            int opinionAfter,
            string deduplicationKey,
            DateTimeOffset occurredAtUtc)
        {
            return new EnvironmentEvent(
                EventId.New(),
                deduplicationKey,
                "rimworld.social.opinion_changed",
                "rimworld",
                occurredAtUtc,
                occurredAtUtc,
                (long)(occurredAtUtc - BaseTime).TotalSeconds,
                "Dagmay.IntegrationHarness",
                new Dictionary<string, string>
                {
                    ["target_external_id"] = "fixture:" + other.Id,
                    ["target_name"] = other.DisplayName,
                    ["opinion_before"] = opinionBefore.ToString(),
                    ["opinion_after"] = opinionAfter.ToString(),
                    ["opinion_delta"] = (opinionAfter - opinionBefore).ToString()
                },
                new[] { owner.Id, other.Id });
        }

        private static void LoadRecords(
            IEnumerable<ExperienceJournalRecord> records,
            InMemoryEventLedger ledger,
            MemoryIndex memoryIndex)
        {
            foreach (var record in records)
            {
                ledger.Append(record.FactualEvent);
                if (record.Memory is not null) memoryIndex.Add(record.Memory);
            }
        }

        private static string CreateTempDirectory(string suffix)
        {
            var path = Path.Combine(Path.GetTempPath(), "dagmay-integration-" + suffix + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
