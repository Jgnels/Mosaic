using System;
using System.Collections.Generic;
using Dagmay.Core.Affect;
using Dagmay.Core.Actions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;
using Dagmay.Core.Memory;
using Dagmay.Core.Observer;
using Dagmay.Core.Persistence;
using Dagmay.Core.Presentation;
using Dagmay.Core.Reflection;
using Dagmay.Core.Scheduling;
using Dagmay.Core.Views;

namespace Dagmay.Tests
{
    internal static class ExecutionAndPresentationContractTests
    {
        public static void ExternalModExecutionIsNotMosaicAuthorship()
        {
            var attribution = new ActionAttribution(
                ActionOriginKind.ExternalModExecution,
                "PickUpAndHaul.HaulToInventory",
                sourceModId: "Mehni.PickUpAndHaul");

            TestAssert.False(attribution.WasAuthoredByMosaic,
                "A job supplied or transformed by another mod must not be represented as a Mosaic-authored personal goal.");
            TestAssert.Equal("Mehni.PickUpAndHaul", attribution.SourceModId!,
                "External execution provenance must remain explicit.");
        }

        public static void MosaicCommitmentRequiresDecisionProvenance()
        {
            TestAssert.Throws<ArgumentException>(
                () => new ActionAttribution(
                    ActionOriginKind.MosaicCommitment,
                    "social.repair_relationship"),
                "Mosaic-authored action attribution must require the originating Mosaic decision/goal ID.");

            var attribution = new ActionAttribution(
                ActionOriginKind.MosaicCommitment,
                "social.repair_relationship",
                mosaicDecisionId: "goal:repair:lynx:michael");

            TestAssert.True(attribution.WasAuthoredByMosaic,
                "Only explicitly attributed Mosaic commitments should count as Mosaic-authored actions.");
        }

        public static void PresentationContextRequiresEvidenceGrounding()
        {
            var evidence = EventId.New();
            var item = new CharacterContextItem(
                "relationship_reason",
                "Michael abandoned Lynx during the raid.",
                new[] { evidence },
                0.92);

            var packet = new CharacterContextPacket(
                IndividualId.New(),
                1234,
                new[] { item });

            TestAssert.Equal(evidence, packet.Items[0].EvidenceIds[0],
                "Presentation context must retain supporting evidence IDs.");
            TestAssert.Equal(1234L, packet.BuiltAtTick,
                "Presentation context may report the observation tick without mutating canonical state.");

            TestAssert.Throws<ArgumentException>(
                () => new CharacterContextItem(
                    "relationship_reason",
                    "Unsupported claim.",
                    Array.Empty<EventId>(),
                    0.5),
                "Factual presentation context must not contain unsupported claims.");
        }

        public static void PresentationContextCollectionsAreBounded()
        {
            var evidence = new List<EventId>();
            for (var index = 0; index < 101; index++) evidence.Add(EventId.New());

            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new CharacterContextItem(
                    "history",
                    "Too much evidence for one presentation item.",
                    evidence,
                    0.5),
                "A presentation item must not accept an unbounded evidence collection.");

            var item = new CharacterContextItem(
                "history",
                "A bounded presentation item.",
                new[] { EventId.New() },
                0.5);
            var items = new List<CharacterContextItem>();
            for (var index = 0; index < 101; index++) items.Add(item);

            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new CharacterContextPacket(IndividualId.New(), 1, items),
                "A presentation packet must not accept an unbounded item collection.");
            TestAssert.Throws<ArgumentException>(
                () => new CharacterContextPacket(
                    IndividualId.New(),
                    1,
                    new CharacterContextItem[] { item, null! }),
                "A presentation packet must reject null items at its read-only boundary.");
        }

        public static void ObserverAndPresentationReadsPreserveCanonicalFingerprint()
        {
            var now = new DateTimeOffset(2026, 7, 23, 6, 0, 0, TimeSpan.Zero);
            var individual = IndividualState.Create(
                "Read-only fixture",
                new IdentitySeed(
                    "rimworld",
                    "0.2-prealpha",
                    new[] { new SeedFact(SeedFactCategory.Trait, "kind", "Kind", "RimWorld trait", 1.0) }));
            var eventId = EventId.New();
            var perceptionId = PerceptionId.New();
            var memory = new SubjectiveMemory(
                MemoryId.New(),
                individual.Id,
                new[] { perceptionId },
                now,
                now.AddSeconds(1),
                "A grounded memory.",
                "The event mattered.",
                AffectVector.Neutral,
                0.7,
                0.4,
                0.9,
                0.8,
                MemoryTier.Recent,
                PrivacyClassification.Shareable,
                Array.Empty<IndividualId>());
            var memoryIndex = new MemoryIndex();
            memoryIndex.Add(memory);
            var ledger = new InMemoryEventLedger();
            ledger.Append(new EnvironmentEvent(
                eventId,
                "purity:event:1",
                "rimworld.test",
                "rimworld",
                now,
                now,
                42,
                "offline fixture",
                new Dictionary<string, string> { ["result"] = "observed" },
                new[] { individual.Id }));
            var queue = new PersistentReflectionQueue(4);
            queue.EnqueueOrMerge(new ReflectionTask(
                ReflectionTaskId.New(),
                individual.Id,
                ModelTaskKind.InterpretMeaningfulEvent,
                ReflectionPriority.MeaningfulEvent,
                now,
                "purity:reflection",
                new[] { eventId },
                256));
            var archive = new IdentityArchiveSnapshot(
                Guid.Parse("19d42de0-4bc1-4ec8-a3d7-030e72c68529"),
                3,
                now,
                new[] { new PersistedIdentityRecord("Thing_Human42", individual) });
            var binding = new EnvironmentBinding(
                individual.Id,
                EnvironmentBindingState.Bound,
                "Thing_Human42",
                42);

            var before = CanonicalStateFingerprint.Compute(
                archive,
                binding,
                ledger.Snapshot(),
                memoryIndex.Snapshot(),
                queue.Snapshot());

            for (var index = 0; index < 3; index++)
            {
                _ = memoryIndex.Recent(individual.Id, 10);
                _ = memoryIndex.MostSignificant(individual.Id, 10);
                _ = new OrdinaryMindSnapshot(
                    "Thing_Human42",
                    individual.DisplayName,
                    "Active",
                    individual.Lifecycle.ToString(),
                    OrdinaryDisclosurePolicy.DescribeCurrentState(individual.Affect),
                    new[] { "Trait: Kind" },
                    new[] { new OrdinaryMindMemory(now, memory.ConciseDiaryEntry, memory.Tier.ToString(), memory.Confidence) });
                _ = new CharacterContextPacket(
                    individual.Id,
                    42,
                    new[] { new CharacterContextItem("memory", memory.ConciseDiaryEntry, new[] { eventId }, 0.9) });
                _ = new ObserverSystemSnapshot(
                    now,
                    "offline",
                    "none",
                    string.Empty,
                    "Offline purity fixture.",
                    true,
                    true,
                    true,
                    true,
                    queue.Count,
                    0,
                    ReflectionBudgetPolicy.ConservativePersonalDefault,
                    new ReflectionUsageSummary(0, 0, 0, 0, 0, 0, 0, null, string.Empty, string.Empty),
                    Array.Empty<ObserverIndividual>());
            }

            TestAssert.Throws<ArgumentException>(
                () => new CharacterContextItem("memory", "Unsupported.", Array.Empty<EventId>(), 0.5),
                "Presentation exception paths must reject unsupported claims.");

            var after = CanonicalStateFingerprint.Compute(
                archive,
                binding,
                ledger.Snapshot(),
                memoryIndex.Snapshot(),
                queue.Snapshot());
            TestAssert.Equal(
                before,
                after,
                "Observer, ordinary-view, diagnostics, and presentation reads must preserve meaningful canonical state.");
        }
    }
}
