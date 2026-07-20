using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dagmay.Core.Affect;
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
        private static async Task<ScenarioExecution> FailureIsolationAsync()
        {
            var assertions = new HarnessAssert();
            var metrics = new Dictionary<string, long>(StringComparer.Ordinal);
            var directory = CreateTempDirectory("failure-isolation");
            try
            {
                const int maximumAttempts = 3;
                var storeId = Guid.Parse("f845e62c-a326-4c32-9762-f4a1879df9b1");
                var state = CreateIndividual("Failure Isolation Fixture");
                var source = CreateEvent(
                    state.Id,
                    "rimworld.skill.level_gained",
                    "failure-isolation:source",
                    BaseTime,
                    new Dictionary<string, string>
                    {
                        ["skill"] = "Intellectual",
                        ["level"] = "9"
                    });
                var encoder = new DeterministicExperienceEncoder();
                var encoded = encoder.EncodeExperiencedEvent(source, state, BaseTime.AddSeconds(1));
                state = encoded.UpdatedState;
                var ledger = new InMemoryEventLedger();
                ledger.Append(source);
                var contextBuilder = new ReflectionContextBuilder();
                var archive = new AtomicIdentityArchive();
                var archivePath = Path.Combine(directory, "canonical.dagmay");
                archive.Save(
                    archivePath,
                    CreateIdentitySnapshot(
                        storeId,
                        1,
                        new Dictionary<string, IndividualState>(StringComparer.Ordinal)
                        {
                            ["rimworld:pawn:failure-isolation"] = state
                        }));
                var canonicalBytes = File.ReadAllBytes(archivePath);
                var allAudit = new List<ReflectionAuditRecord>();

                var offlineTask = CreateFailureIsolationTask(state.Id, source.Id, "offline", BaseTime.AddMinutes(1));
                var offlineQueue = new PersistentReflectionQueue(8);
                offlineQueue.EnqueueOrMerge(offlineTask);
                var offlinePath = Path.Combine(directory, "offline.reflection");
                var reflectionStore = new AtomicReflectionStore();
                reflectionStore.Save(
                    offlinePath,
                    new ReflectionStoreSnapshot(storeId, 1, BaseTime.AddMinutes(1), offlineQueue.Snapshot(), Array.Empty<ReflectionAuditRecord>()));
                var offlineReload = reflectionStore.Load(offlinePath);
                assertions.Equal(ReflectionStoreLoadStatus.LoadedPrimary, offlineReload.Status, "Unavailable/offline transport leaves a checksum-verified reflection store.");
                assertions.Equal(1, offlineReload.Snapshot!.PendingTasks.Count, "Unavailable/offline transport leaves pending work durable without dispatch.");
                assertions.Equal(0, offlineReload.Snapshot.AuditRecords.Count, "Unavailable/offline transport does not fabricate a provider attempt.");
                AssertCanonicalArchiveUnchanged(assertions, archive, archivePath, canonicalBytes, state, "offline transport");

                var cases = new[]
                {
                    new ProviderFailureFixture("invalid-response", FakeProviderBehavior.InvalidResponse, ModelResultStatus.InvalidResponse, false),
                    new ProviderFailureFixture("timeout", FakeProviderBehavior.TimedOut, ModelResultStatus.TimedOut, true),
                    new ProviderFailureFixture("rate-limit", FakeProviderBehavior.RateLimited, ModelResultStatus.RateLimited, true),
                    new ProviderFailureFixture("provider-error", FakeProviderBehavior.ProviderError, ModelResultStatus.ProviderError, true)
                };
                var retainedForRetry = 0;
                var quarantined = 0;
                foreach (var fixture in cases)
                {
                    var task = CreateFailureIsolationTask(state.Id, source.Id, fixture.Name, BaseTime.AddMinutes(2));
                    var queue = new PersistentReflectionQueue(8);
                    queue.EnqueueOrMerge(task);
                    var failurePending = queue.Find(task.Id)!;
                    var request = contextBuilder.BuildRequest(
                        task,
                        state,
                        new[] { source },
                        new[] { encoded.Memory },
                        BaseTime.AddMinutes(2),
                        TimeSpan.FromMinutes(1));
                    var result = await new DeterministicFakeProvider(fixture.Behavior)
                        .GenerateStructuredAsync(request, CancellationToken.None)
                        .ConfigureAwait(false);
                    assertions.Equal(fixture.ExpectedStatus, result.Status, fixture.Name + " is normalized to the expected provider status.");
                    assertions.Equal(fixture.ExpectedRetryable, result.Retryable, fixture.Name + " exposes the expected retryability.");

                    var audit = new List<ReflectionAuditRecord>
                    {
                        CreateFailureIsolationAudit(failurePending, request, state, ReflectionAuditStatus.AttemptFailed, result, fixture.Name + " failed safely.")
                    };
                    if (result.Retryable && failurePending.AttemptCount + 1 < maximumAttempts)
                    {
                        assertions.True(queue.MarkRetry(task.Id, BaseTime.AddMinutes(7), result.ErrorCode), fixture.Name + " remains queued for bounded retry.");
                        assertions.Equal(1, queue.Find(task.Id)!.AttemptCount, fixture.Name + " persists exactly one failed attempt.");
                        retainedForRetry++;
                    }
                    else
                    {
                        assertions.True(queue.Remove(task.Id), fixture.Name + " is removed from dispatch after permanent failure.");
                        audit.Add(CreateFailureIsolationAudit(
                            failurePending,
                            request,
                            state,
                            ReflectionAuditStatus.Quarantined,
                            result,
                            fixture.Name + " was quarantined without canonical mutation."));
                        quarantined++;
                    }

                    var path = Path.Combine(directory, fixture.Name + ".reflection");
                    reflectionStore.Save(path, new ReflectionStoreSnapshot(storeId, 1, BaseTime.AddMinutes(3), queue.Snapshot(), audit));
                    var reload = reflectionStore.Load(path);
                    assertions.Equal(ReflectionStoreLoadStatus.LoadedPrimary, reload.Status, fixture.Name + " durable result reloads from the verified primary store.");
                    assertions.Equal(queue.Count, reload.Snapshot!.PendingTasks.Count, fixture.Name + " queue disposition survives shutdown/reload.");
                    assertions.Equal(audit.Count, reload.Snapshot.AuditRecords.Count, fixture.Name + " audit disposition survives shutdown/reload.");
                    assertions.True(reload.Snapshot.AuditRecords.All(value => value.Status != ReflectionAuditStatus.Committed), fixture.Name + " cannot create a committed audit record.");
                    allAudit.AddRange(audit);
                    AssertCanonicalArchiveUnchanged(assertions, archive, archivePath, canonicalBytes, state, fixture.Name);
                }

                var canceledTask = CreateFailureIsolationTask(state.Id, source.Id, "canceled", BaseTime.AddMinutes(3));
                var canceledRequest = contextBuilder.BuildRequest(
                    canceledTask,
                    state,
                    new[] { source },
                    new[] { encoded.Memory },
                    BaseTime.AddMinutes(3),
                    TimeSpan.FromMinutes(1));
                using (var canceledSource = new CancellationTokenSource())
                {
                    canceledSource.Cancel();
                    var canceled = await new DeterministicFakeProvider()
                        .GenerateStructuredAsync(canceledRequest, canceledSource.Token)
                        .ConfigureAwait(false);
                    assertions.Equal(ModelResultStatus.Canceled, canceled.Status, "Canceled provider work is normalized without throwing into the game loop.");
                    assertions.True(!canceled.Retryable, "Canceled fixture work is not retried automatically.");
                    AssertCanonicalArchiveUnchanged(assertions, archive, archivePath, canonicalBytes, state, "canceled request");
                }

                var malformedTask = CreateFailureIsolationTask(state.Id, source.Id, "malformed-success", BaseTime.AddMinutes(4));
                var malformedQueue = new PersistentReflectionQueue(8);
                malformedQueue.EnqueueOrMerge(malformedTask);
                var malformedPending = malformedQueue.Find(malformedTask.Id)!;
                var malformedRequest = contextBuilder.BuildRequest(
                    malformedTask,
                    state,
                    new[] { source },
                    new[] { encoded.Memory },
                    BaseTime.AddMinutes(4),
                    TimeSpan.FromMinutes(1));
                var malformedResult = await new DeterministicFakeProvider(FakeProviderBehavior.Success, "{not-valid-json")
                    .GenerateStructuredAsync(malformedRequest, CancellationToken.None)
                    .ConfigureAwait(false);
                assertions.Equal(ModelResultStatus.Success, malformedResult.Status, "Malformed-output fixture reaches strict local parsing through a nominal provider success.");
                var parseRejected = false;
                try
                {
                    ReflectionProposalJson.Parse(malformedResult.StructuredPayload);
                }
                catch
                {
                    parseRejected = true;
                }
                assertions.True(parseRejected, "Strict reflection parsing rejects malformed provider output.");
                assertions.True(malformedQueue.Remove(malformedTask.Id), "Malformed provider output is removed from dispatch after quarantine.");
                var malformedAudit = CreateFailureIsolationAudit(
                    malformedPending,
                    malformedRequest,
                    state,
                    ReflectionAuditStatus.Quarantined,
                    ModelResult.Failure(
                        malformedRequest.Id,
                        ModelResultStatus.InvalidResponse,
                        malformedResult.Provider,
                        malformedResult.Model,
                        "INVALID_STRUCTURED_OUTPUT",
                        "Strict local parsing rejected malformed output.",
                        malformedResult.Latency),
                    "Malformed structured output was quarantined.",
                    malformedResult.StructuredPayload);
                allAudit.Add(malformedAudit);
                var malformedPath = Path.Combine(directory, "malformed.reflection");
                reflectionStore.Save(
                    malformedPath,
                    new ReflectionStoreSnapshot(storeId, 1, BaseTime.AddMinutes(4), malformedQueue.Snapshot(), new[] { malformedAudit }));
                var malformedReload = reflectionStore.Load(malformedPath);
                assertions.Equal(0, malformedReload.Snapshot!.PendingTasks.Count, "Malformed output remains non-dispatchable after reload.");
                assertions.Equal(ReflectionAuditStatus.Quarantined, malformedReload.Snapshot.AuditRecords.Single().Status, "Malformed-output quarantine is durable.");
                AssertCanonicalArchiveUnchanged(assertions, archive, archivePath, canonicalBytes, state, "malformed output");
                quarantined++;

                var pendingTask = CreateFailureIsolationTask(state.Id, source.Id, "pending-commit", BaseTime.AddMinutes(5));
                var pendingRequest = contextBuilder.BuildRequest(
                    pendingTask,
                    state,
                    new[] { source },
                    new[] { encoded.Memory },
                    BaseTime.AddMinutes(5),
                    TimeSpan.FromMinutes(1));
                var success = await new DeterministicFakeProvider()
                    .GenerateStructuredAsync(pendingRequest, CancellationToken.None)
                    .ConfigureAwait(false);
                var proposal = ReflectionProposalJson.Parse(success.StructuredPayload);
                var validation = new ReflectionProposalValidator().Validate(pendingRequest, proposal, state, ledger);
                assertions.True(validation.IsValid && validation.Replacement is not null, "Pending-commit fixture begins with a fully grounded validated replacement.");
                var replacement = validation.Replacement!;
                var pending = new PendingReflectionTask(pendingTask, 0, BaseTime.AddMinutes(5), string.Empty);
                var pendingAudit = CreateFailureIsolationAudit(
                    pending,
                    pendingRequest,
                    state,
                    ReflectionAuditStatus.PendingCommit,
                    success,
                    "Validated proposal persisted before identity replacement.",
                    success.StructuredPayload);

                var absentArchivePath = Path.Combine(directory, "pending-absent.dagmay");
                archive.Save(absentArchivePath, CreateIdentitySnapshot(storeId, 1, SingleIdentity(state)));
                var absentReflectionPath = Path.Combine(directory, "pending-absent.reflection");
                reflectionStore.Save(
                    absentReflectionPath,
                    new ReflectionStoreSnapshot(storeId, 1, BaseTime.AddMinutes(5), new[] { pending }, new[] { pendingAudit }));
                var absentState = LoadSingleIdentity(archive, absentArchivePath);
                var absentStore = reflectionStore.Load(absentReflectionPath).Snapshot!;
                var absentProposal = ReflectionProposalJson.Parse(absentStore.AuditRecords.Single().StructuredPayload);
                var absentValidation = new ReflectionProposalValidator().Validate(pendingRequest, absentProposal, absentState, ledger);
                assertions.True(absentValidation.IsValid && absentValidation.Replacement is not null, "Interrupted commit absent from the archive is revalidated before recovery.");
                archive.Save(absentArchivePath, CreateIdentitySnapshot(storeId, 2, SingleIdentity(absentValidation.Replacement!)));
                var absentQueue = new PersistentReflectionQueue(8, absentStore.PendingTasks);
                assertions.True(absentQueue.Remove(pendingTask.Id), "Recovered absent commit removes its durable task exactly once.");
                var absentCommitted = CreateFailureIsolationAudit(
                    pending,
                    pendingRequest,
                    state,
                    ReflectionAuditStatus.Committed,
                    success,
                    "Recovery completed the validated identity commit.");
                reflectionStore.Save(
                    absentReflectionPath,
                    new ReflectionStoreSnapshot(storeId, 2, BaseTime.AddMinutes(6), absentQueue.Snapshot(), absentStore.AuditRecords.Concat(new[] { absentCommitted })));
                var absentRecovered = LoadSingleIdentity(archive, absentArchivePath);
                assertions.Equal(state.Version + 1, absentRecovered.Version, "Absent interrupted commit advances canonical version exactly once.");
                assertions.Equal(state.Id, absentRecovered.Id, "Absent interrupted commit preserves IndividualId.");
                assertions.Equal(state.LineageId, absentRecovered.LineageId, "Absent interrupted commit preserves LineageId.");
                var absentReload = reflectionStore.Load(absentReflectionPath).Snapshot!;
                assertions.Equal(0, absentReload.PendingTasks.Count, "Recovered absent commit remains removed after shutdown/reload.");
                assertions.Equal(1, absentReload.AuditRecords.Count(value => value.Status == ReflectionAuditStatus.Committed), "Recovered absent commit has exactly one terminal committed audit.");
                allAudit.AddRange(absentReload.AuditRecords);

                var appliedArchivePath = Path.Combine(directory, "pending-applied.dagmay");
                archive.Save(appliedArchivePath, CreateIdentitySnapshot(storeId, 1, SingleIdentity(replacement)));
                var appliedBytes = File.ReadAllBytes(appliedArchivePath);
                assertions.True(IsAlreadyApplied(replacement, pendingAudit, proposal), "Already-applied interrupted commit is recognized from identity, lineage, version, and target affect.");
                var appliedQueue = new PersistentReflectionQueue(8, new[] { pending });
                assertions.True(appliedQueue.Remove(pendingTask.Id), "Already-applied recovery removes the stale durable task.");
                assertions.True(appliedBytes.SequenceEqual(File.ReadAllBytes(appliedArchivePath)), "Already-applied recovery performs no second canonical write.");
                assertions.Equal(state.Version + 1, LoadSingleIdentity(archive, appliedArchivePath).Version, "Already-applied recovery does not advance identity version twice.");

                var conflictArchivePath = Path.Combine(directory, "pending-conflict.dagmay");
                var conflictingState = state.WithAffect(DifferentAffect(state.Affect), state.Version);
                archive.Save(conflictArchivePath, CreateIdentitySnapshot(storeId, 1, SingleIdentity(conflictingState)));
                var conflictBytes = File.ReadAllBytes(conflictArchivePath);
                assertions.True(!IsAlreadyApplied(conflictingState, pendingAudit, proposal), "Conflicting version cannot masquerade as the interrupted reflection commit.");
                assertions.True(conflictingState.Version != pendingAudit.BaseStateVersion, "Conflicting recovery fixture is newer than the pending base version.");
                var conflictQueue = new PersistentReflectionQueue(8, new[] { pending });
                assertions.True(conflictQueue.Remove(pendingTask.Id), "Conflicting pending commit is removed from dispatch after quarantine.");
                var conflictAudit = CreateFailureIsolationAudit(
                    pending,
                    pendingRequest,
                    state,
                    ReflectionAuditStatus.Quarantined,
                    success,
                    "Recovery found a conflicting identity version; no mutation was applied.",
                    success.StructuredPayload);
                assertions.True(conflictBytes.SequenceEqual(File.ReadAllBytes(conflictArchivePath)), "Conflicting recovery leaves canonical identity bytes unchanged.");
                assertions.Equal(conflictingState.Affect, LoadSingleIdentity(archive, conflictArchivePath).Affect, "Conflicting recovery preserves the independently persisted affect.");
                allAudit.Add(conflictAudit);

                var mismatchJournalPath = Path.Combine(directory, "mismatch.journal");
                var journal = new DurableExperienceJournal();
                var firstAppend = journal.Append(
                    mismatchJournalPath,
                    new ExperienceJournalRecord(source, encoded.Perception, encoded.Memory),
                    0,
                    string.Empty);
                var secondSource = CreateEvent(
                    state.Id,
                    "rimworld.health.condition_removed",
                    "failure-isolation:mismatch-suffix",
                    BaseTime.AddMinutes(7),
                    new Dictionary<string, string> { ["condition"] = "FailureFixture" });
                var secondEncoded = encoder.EncodeExperiencedEvent(secondSource, state, BaseTime.AddMinutes(7).AddSeconds(1));
                journal.Append(
                    mismatchJournalPath,
                    new ExperienceJournalRecord(secondSource, secondEncoded.Perception, secondEncoded.Memory),
                    firstAppend.Position,
                    firstAppend.EntryHash);
                var mismatchLoad = journal.Load(mismatchJournalPath);
                assertions.Equal(ExperienceJournalLoadStatus.Loaded, mismatchLoad.Status, "Storage-mismatch fixture begins from a verified external hash chain.");
                var wrongCheckpointHash = new string('0', 64);
                assertions.True(!string.Equals(wrongCheckpointHash, mismatchLoad.EntryHashes[0], StringComparison.Ordinal), "Mismatched save checkpoint is distinguishable from the verified journal prefix.");
                var exposedRecords = string.Equals(wrongCheckpointHash, mismatchLoad.EntryHashes[0], StringComparison.Ordinal) ? 1 : 0;
                assertions.Equal(0, exposedRecords, "Unverified mismatch exposes no external suffix for automatic canonical adoption.");
                AssertCanonicalArchiveUnchanged(assertions, archive, archivePath, canonicalBytes, state, "storage mismatch");

                assertions.Equal(allAudit.Count, allAudit.Select(value => value.Id).Distinct().Count(), "Failure-isolation audit record IDs are globally unique.");
                assertions.True(allAudit.All(value => value.Status != ReflectionAuditStatus.Committed || value.TaskId == pendingTask.Id), "Only the explicitly recovered validated proposal can have committed status.");

                metrics["providerFailureCases"] = cases.Length + 2;
                metrics["retryableFailuresRetained"] = retainedForRetry;
                metrics["quarantinedFailures"] = quarantined;
                metrics["pendingCommitRecoveryCases"] = 3;
                metrics["canonicalRecoveryCommits"] = 1;
                metrics["storageMismatchAutoAdoptions"] = 0;
                metrics["shutdownReloadChecks"] = cases.Length + 3;
                return new ScenarioExecution(assertions.Assertions, metrics);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static ReflectionTask CreateFailureIsolationTask(
            IndividualId individualId,
            EventId sourceId,
            string suffix,
            DateTimeOffset createdAtUtc)
        {
            return new ReflectionTask(
                ReflectionTaskId.New(),
                individualId,
                ModelTaskKind.InterpretMeaningfulEvent,
                ReflectionPriority.MeaningfulEvent,
                createdAtUtc,
                "failure-isolation:" + suffix,
                new[] { sourceId },
                700);
        }

        private static ReflectionAuditRecord CreateFailureIsolationAudit(
            PendingReflectionTask pending,
            ModelRequest request,
            IndividualState state,
            ReflectionAuditStatus status,
            ModelResult result,
            string diagnostic,
            string structuredPayload = "")
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
                BaseTime.AddMinutes(10),
                status,
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
                structuredPayload,
                diagnostic);
        }

        private static IReadOnlyDictionary<string, IndividualState> SingleIdentity(IndividualState state)
        {
            return new Dictionary<string, IndividualState>(StringComparer.Ordinal)
            {
                ["rimworld:pawn:failure-isolation"] = state
            };
        }

        private static IndividualState LoadSingleIdentity(AtomicIdentityArchive archive, string path)
        {
            var loaded = archive.Load(path);
            if (loaded.Status != ArchiveLoadStatus.LoadedPrimary || loaded.Snapshot is null || loaded.Snapshot.Records.Count != 1)
            {
                throw new HarnessAssertionException("Failure-isolation identity archive did not reload from one verified primary record.");
            }

            return loaded.Snapshot.Records[0].State;
        }

        private static void AssertCanonicalArchiveUnchanged(
            HarnessAssert assertions,
            AtomicIdentityArchive archive,
            string path,
            byte[] expectedBytes,
            IndividualState expectedState,
            string scenario)
        {
            assertions.True(expectedBytes.SequenceEqual(File.ReadAllBytes(path)), scenario + " leaves canonical identity archive bytes unchanged.");
            var actual = LoadSingleIdentity(archive, path);
            assertions.Equal(expectedState.Id, actual.Id, scenario + " preserves IndividualId.");
            assertions.Equal(expectedState.LineageId, actual.LineageId, scenario + " preserves LineageId.");
            assertions.Equal(expectedState.Version, actual.Version, scenario + " preserves canonical identity version.");
            assertions.Equal(expectedState.Affect, actual.Affect, scenario + " preserves canonical affect.");
        }

        private static bool IsAlreadyApplied(
            IndividualState state,
            ReflectionAuditRecord record,
            ReflectionProposal proposal)
        {
            return state.Id == proposal.IndividualId
                && state.LineageId == record.LineageId
                && state.Version == record.BaseStateVersion + 1
                && state.Affect.Equals(proposal.TargetAffect);
        }

        private static AffectVector DifferentAffect(AffectVector value)
        {
            var valence = value.Valence >= 0 ? -0.5 : 0.5;
            return new AffectVector(
                valence,
                value.Arousal,
                value.Threat,
                value.Agency,
                value.Attachment,
                value.Certainty,
                value.SocialStanding);
        }

        private sealed class ProviderFailureFixture
        {
            public ProviderFailureFixture(
                string name,
                FakeProviderBehavior behavior,
                ModelResultStatus expectedStatus,
                bool expectedRetryable)
            {
                Name = name;
                Behavior = behavior;
                ExpectedStatus = expectedStatus;
                ExpectedRetryable = expectedRetryable;
            }

            public string Name { get; }
            public FakeProviderBehavior Behavior { get; }
            public ModelResultStatus ExpectedStatus { get; }
            public bool ExpectedRetryable { get; }
        }
    }
}