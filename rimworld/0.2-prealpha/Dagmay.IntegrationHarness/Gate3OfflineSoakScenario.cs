using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;
using Dagmay.Core.Memory;
using Dagmay.Core.Observer;
using Dagmay.Core.Persistence;
using Dagmay.Core.Presentation;
using Dagmay.Core.Scheduling;
using Dagmay.Core.Views;

namespace Dagmay.IntegrationHarness
{
    internal static partial class IntegrationScenarios
    {
        private static Task<ScenarioExecution> Gate3OfflineSoakAsync(int cycleCount, int seed)
        {
            var assertions = new HarnessAssert();
            var metrics = new Dictionary<string, long>(StringComparer.Ordinal);
            var details = new Dictionary<string, string>(StringComparer.Ordinal);
            var directory = CreateTempDirectory("gate3-offline-soak");
            try
            {
                const int individualCount = 4;
                const int boundedEventSlots = 128;
                var storeId = DeterministicGuid(seed, "store", 0);
                var identities = new Dictionary<string, IndividualState>(StringComparer.Ordinal);
                var bindings = new Dictionary<string, EnvironmentBinding>(StringComparer.Ordinal);
                for (var index = 0; index < individualCount; index++)
                {
                    var externalId = "Thing_Human" + index.ToString("D3", CultureInfo.InvariantCulture);
                    var individual = IndividualState.Restore(
                        IndividualId.Parse(DeterministicGuid(seed, "individual", index).ToString()),
                        LineageId.Parse(DeterministicGuid(seed, "lineage", index).ToString()),
                        0,
                        "Soak " + index.ToString(CultureInfo.InvariantCulture),
                        Dagmay.Core.Lifecycle.LifecycleState.Active,
                        new IdentitySeed(
                            "rimworld",
                            "0.2-prealpha",
                            new[] { new SeedFact(SeedFactCategory.Trait, "kind", "Kind", "offline soak fixture", 1.0) }),
                        ContinuityProfile.InitialDefault,
                        AffectVector.Neutral);
                    identities.Add(externalId, individual);
                    bindings.Add(
                        externalId,
                        new EnvironmentBinding(individual.Id, EnvironmentBindingState.Bound, externalId, 0));
                }

                var archive = new AtomicIdentityArchive();
                var archivePath = Path.Combine(directory, "identities.dagmay");
                var generation = 0L;
                var savedAt = BaseTime;
                archive.Save(archivePath, Snapshot(storeId, generation, savedAt, identities));
                var ledger = new InMemoryEventLedger();
                var random = new Random(seed);
                var observationReads = 0L;
                var identityLookups = 0L;
                var eventAdmissionAttempts = 0L;
                var persistenceWrites = 1L;
                var reloads = 0L;
                var observerReads = 0L;
                var bindingLosses = 0L;
                var bindingRecoveries = 0L;
                var injectedFailures = 0L;
                var peakManagedBytes = GC.GetTotalMemory(false);

                for (var cycle = 1; cycle <= cycleCount; cycle++)
                {
                    var selectedIndex = random.Next(individualCount);
                    var externalId = "Thing_Human" + selectedIndex.ToString("D3", CultureInfo.InvariantCulture);
                    var individual = identities[externalId];
                    identityLookups++;

                    var binding = bindings[externalId];
                    observationReads++;
                    if (cycle % 37 == 0)
                    {
                        binding = binding.Transition(
                            EnvironmentBindingState.UnresolvedEnvironmentBinding,
                            null,
                            cycle);
                        bindings[externalId] = binding;
                        bindingLosses++;
                    }
                    else if (binding.IsTemporarilyUnresolved)
                    {
                        binding = binding.Transition(
                            EnvironmentBindingState.Bound,
                            externalId,
                            cycle);
                        bindings[externalId] = binding;
                        bindingRecoveries++;
                    }

                    var eventSlot = cycle % boundedEventSlots;
                    var eventId = EventId.Parse(DeterministicGuid(seed, "event", eventSlot).ToString());
                    var environmentEvent = new EnvironmentEvent(
                        eventId,
                        "gate3-soak:event:" + eventSlot.ToString(CultureInfo.InvariantCulture),
                        "rimworld.offline_observation",
                        "rimworld",
                        BaseTime.AddTicks(eventSlot),
                        BaseTime.AddTicks(eventSlot),
                        eventSlot,
                        "deterministic offline soak",
                        new Dictionary<string, string> { ["external_id"] = externalId },
                        new[] { individual.Id });
                    ledger.Append(environmentEvent);
                    eventAdmissionAttempts++;

                    var canonicalBeforeRead = new IdentityArchiveCodec().Encode(
                        Snapshot(storeId, generation, savedAt, identities));
                    _ = new OrdinaryMindSnapshot(
                        externalId,
                        individual.DisplayName,
                        "Active",
                        individual.Lifecycle.ToString(),
                        OrdinaryDisclosurePolicy.DescribeCurrentState(individual.Affect),
                        new[] { "Trait: Kind" },
                        Array.Empty<OrdinaryMindMemory>());
                    _ = new CharacterContextPacket(
                        individual.Id,
                        cycle,
                        new[]
                        {
                            new CharacterContextItem(
                                "observation",
                                "A bounded offline observation.",
                                new[] { eventId },
                                0.5)
                        });
                    _ = new ObserverSystemSnapshot(
                        BaseTime.AddTicks(cycle),
                        "offline",
                        "none",
                        string.Empty,
                        "No provider is used by the Gate 3 offline soak.",
                        true,
                        true,
                        true,
                        true,
                        0,
                        0,
                        ReflectionBudgetPolicy.ConservativePersonalDefault,
                        new ReflectionUsageSummary(0, 0, 0, 0, 0, 0, 0, null, string.Empty, string.Empty),
                        Array.Empty<ObserverIndividual>());
                    observerReads += 3;
                    var canonicalAfterRead = new IdentityArchiveCodec().Encode(
                        Snapshot(storeId, generation, savedAt, identities));
                    if (!canonicalBeforeRead.SequenceEqual(canonicalAfterRead))
                    {
                        throw new HarnessAssertionException(
                            "Observer or presentation construction changed the canonical identity snapshot.");
                    }

                    if (cycle % 10 == 0)
                    {
                        generation++;
                        savedAt = BaseTime.AddTicks(cycle);
                        archive.Save(archivePath, Snapshot(storeId, generation, savedAt, identities));
                        persistenceWrites++;
                        var loaded = archive.Load(archivePath, storeId, generation);
                        if (loaded.Status != ArchiveLoadStatus.LoadedPrimary || loaded.Snapshot is null)
                        {
                            throw new HarnessAssertionException(
                                "Checkpointed identity archive did not reload exactly during soak cycle "
                                + cycle.ToString(CultureInfo.InvariantCulture) + ".");
                        }

                        identities = loaded.Snapshot.Records.ToDictionary(
                            value => value.ExternalEntityId,
                            value => value.State,
                            StringComparer.Ordinal);
                        reloads++;
                    }

                    if (cycle % 113 == 0)
                    {
                        var failurePath = Path.Combine(directory, "injected-failure.dagmay");
                        var goodBytes = File.ReadAllBytes(archivePath);
                        File.WriteAllBytes(failurePath, goodBytes.Take(Math.Max(1, goodBytes.Length / 3)).ToArray());
                        var failed = archive.Load(failurePath, storeId, generation);
                        if (failed.Status != ArchiveLoadStatus.Unrecoverable || failed.Snapshot is not null)
                        {
                            throw new HarnessAssertionException("Injected truncated archive did not fail closed.");
                        }
                        injectedFailures++;
                    }

                    if (cycle % 64 == 0)
                    {
                        peakManagedBytes = Math.Max(peakManagedBytes, GC.GetTotalMemory(false));
                    }
                }

                var finalLoad = archive.Load(archivePath, storeId, generation);
                assertions.Equal(
                    ArchiveLoadStatus.LoadedPrimary,
                    finalLoad.Status,
                    "Final identity checkpoint reloads with exact store and generation.");
                assertions.Equal(
                    individualCount,
                    finalLoad.Snapshot!.Records.Count,
                    "Soak preserves every enrolled individual.");
                assertions.True(
                    finalLoad.Snapshot.Records.All(record =>
                        bindings[record.ExternalEntityId].IndividualId == record.State.Id),
                    "Binding loss and recovery never replaces an individual.");
                assertions.True(
                    ledger.Snapshot().Count <= boundedEventSlots,
                    "Experience admission remains bounded instead of creating per-cycle durable spam.");
                assertions.Equal(
                    cycleCount,
                    checked((int)eventAdmissionAttempts),
                    "Every soak cycle attempts deterministic experience admission.");

                metrics["seed"] = seed;
                metrics["cycles"] = cycleCount;
                metrics["individuals"] = individualCount;
                metrics["observationReads"] = observationReads;
                metrics["identityLookups"] = identityLookups;
                metrics["eventAdmissionAttempts"] = eventAdmissionAttempts;
                metrics["uniqueEvents"] = ledger.Snapshot().Count;
                metrics["persistenceWrites"] = persistenceWrites;
                metrics["reloads"] = reloads;
                metrics["observerReads"] = observerReads;
                metrics["bindingLosses"] = bindingLosses;
                metrics["bindingRecoveries"] = bindingRecoveries;
                metrics["injectedFailures"] = injectedFailures;
                metrics["sampledPeakManagedBytes"] = peakManagedBytes;
                details["finalStateSha256"] = FinalStateHash(
                    finalLoad.Snapshot,
                    bindings,
                    ledger.Snapshot());
                details["providerMode"] = "offline";
                details["saveFilesTouched"] = "none";

                return Task.FromResult(new ScenarioExecution(assertions.Assertions, metrics, details));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static IdentityArchiveSnapshot Snapshot(
            Guid storeId,
            long generation,
            DateTimeOffset savedAt,
            IReadOnlyDictionary<string, IndividualState> identities)
        {
            return new IdentityArchiveSnapshot(
                storeId,
                generation,
                savedAt,
                identities
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value => new PersistedIdentityRecord(value.Key, value.Value)));
        }

        private static Guid DeterministicGuid(int seed, string kind, int index)
        {
            using (var algorithm = SHA256.Create())
            {
                var input = Encoding.UTF8.GetBytes(
                    seed.ToString(CultureInfo.InvariantCulture)
                    + ":" + kind + ":" + index.ToString(CultureInfo.InvariantCulture));
                var hash = algorithm.ComputeHash(input);
                var bytes = new byte[16];
                Array.Copy(hash, bytes, bytes.Length);
                return new Guid(bytes);
            }
        }

        private static string FinalStateHash(
            IdentityArchiveSnapshot snapshot,
            IReadOnlyDictionary<string, EnvironmentBinding> bindings,
            IReadOnlyList<Dagmay.Core.Abstractions.EventLedgerEntry> events)
        {
            var builder = new StringBuilder();
            builder.Append(Convert.ToBase64String(new IdentityArchiveCodec().Encode(snapshot)));
            foreach (var pair in bindings.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                builder.Append('|').Append(pair.Key)
                    .Append('|').Append(pair.Value.IndividualId)
                    .Append('|').Append(pair.Value.State)
                    .Append('|').Append(pair.Value.EnvironmentObjectKey ?? string.Empty)
                    .Append('|').Append(pair.Value.ObservedAtTick.ToString(CultureInfo.InvariantCulture));
            }
            foreach (var entry in events.OrderBy(value => value.Position))
            {
                builder.Append('|').Append(entry.Position.ToString(CultureInfo.InvariantCulture))
                    .Append('|').Append(entry.Value.Id)
                    .Append('|').Append(entry.Value.DeduplicationKey);
            }

            using (var algorithm = SHA256.Create())
            {
                return string.Concat(
                    algorithm.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()))
                        .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }
    }
}
