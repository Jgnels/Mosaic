using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;
using Dagmay.Core.Scheduling;
using Dagmay.RimWorld.Diagnostics;

namespace Dagmay.Tests
{
    internal static class Experiment0ATelemetryContractTests
    {
        public static void Experiment0ATelemetryPreservesCanonicalStateFingerprint()
        {
            var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
            var individual = IndividualState.Create(
                "Telemetry fixture",
                new IdentitySeed(
                    "rimworld",
                    "experiment-0a",
                    new[]
                    {
                        new SeedFact(
                            SeedFactCategory.Trait,
                            "role",
                            "Colonist",
                            "fixture",
                            1.0)
                    }));
            var archive = new IdentityArchiveSnapshot(
                Guid.Parse("4be36f0b-a428-4f17-b3f6-59d0cd5471a1"),
                1,
                now,
                new[] { new PersistedIdentityRecord("Thing_Telemetry", individual) });
            var binding = new EnvironmentBinding(
                individual.Id,
                EnvironmentBindingState.Bound,
                "Thing_Telemetry",
                10);
            var ledger = new InMemoryEventLedger();
            var memories = new MemoryIndex();
            var reflections = new PersistentReflectionQueue(4);
            var before = CanonicalStateFingerprint.Compute(
                archive,
                binding,
                ledger.Snapshot(),
                memories.Snapshot(),
                reflections.Snapshot());

            var telemetry = new Experiment0ATelemetry();
            telemetry.RecordObservation(Pair(
                Experiment0ASource.Thought,
                "thought:purity",
                100,
                "individual-a",
                "individual-b"));
            telemetry.RecordDialoguePreparation(false, "Duplicate");
            telemetry.RecordPresentation("Bubble");
            telemetry.RecordSourceFailure(Experiment0ASource.Tale);
            _ = telemetry.Snapshot().ToLogLine("test", 100);

            var after = CanonicalStateFingerprint.Compute(
                archive,
                binding,
                ledger.Snapshot(),
                memories.Snapshot(),
                reflections.Snapshot());
            TestAssert.Equal(
                before,
                after,
                "Experiment 0A counters and summaries must not mutate canonical Mosaic state.");
        }

        public static void Experiment0ACountersAreBoundedAndDeterministic()
        {
            var first = new Experiment0ATelemetry();
            var second = new Experiment0ATelemetry();
            for (var index = 0; index < 5000; index++)
            {
                var observation = Pair(
                    Experiment0ASource.PlayLog,
                    "playlog:" + index,
                    index,
                    "owner-" + index,
                    "other-" + index);
                first.RecordObservation(observation);
                second.RecordObservation(observation);
            }

            var left = first.Snapshot();
            var right = second.Snapshot();
            TestAssert.Equal(
                left.ToLogLine("test", 5000),
                right.ToLogLine("test", 5000),
                "Equivalent telemetry input must produce one deterministic summary.");
            TestAssert.Equal(
                Experiment0ATelemetry.MaximumRecentObservations,
                left.RecentObservationCount,
                "The raw tick/participant correlation ring must remain bounded.");
            TestAssert.True(
                left.DedupeKeyCount <= Experiment0ATelemetry.MaximumDedupeKeysPerSource,
                "Stable observation deduplication must remain fixed-memory per source.");
            TestAssert.True(
                left.TrackedPairCount <= Experiment0ATelemetry.MaximumTrackedPairs,
                "Pair history tracking must remain bounded.");
        }

        public static void Experiment0ADuplicateStableObservationsDoNotInflatePlayLogOrTales()
        {
            var telemetry = new Experiment0ATelemetry();
            var playLog = Pair(
                Experiment0ASource.PlayLog,
                "playlog:42",
                100,
                "individual-a",
                "individual-b");
            var tale = new Experiment0AObservation(
                Experiment0ASource.Tale,
                "tale:Tale_7",
                101,
                101,
                new[] { "individual-a", "individual-b" });

            TestAssert.True(telemetry.RecordObservation(playLog), "First PlayLog observation must count.");
            TestAssert.False(telemetry.RecordObservation(playLog), "Duplicate PlayLog polling must not count.");
            TestAssert.True(telemetry.RecordObservation(tale), "First Tale observation must count.");
            TestAssert.False(telemetry.RecordObservation(tale), "Duplicate Tale polling must not count.");

            var snapshot = telemetry.Snapshot();
            TestAssert.Equal(1L, snapshot.ObservedPlayLogPairEvents,
                "One stable PlayLog ID must count once.");
            TestAssert.Equal(1L, snapshot.ObservedTaleEnrolledEvents,
                "One stable Tale ID must count once.");
        }

        public static void Experiment0APairIsolationDepthAndOverlapRemainSourceSpecific()
        {
            var telemetry = new Experiment0ATelemetry();
            telemetry.RecordObservation(Pair(
                Experiment0ASource.Thought,
                "thought:ab",
                100,
                "individual-a",
                "individual-b"));
            telemetry.RecordObservation(Pair(
                Experiment0ASource.PlayLog,
                "playlog:ac",
                150,
                "individual-a",
                "individual-c"));
            telemetry.RecordObservation(Pair(
                Experiment0ASource.PlayLog,
                "playlog:ab-episode",
                200,
                "individual-a",
                "individual-b"));
            telemetry.RecordObservation(Pair(
                Experiment0ASource.PlayLog,
                "playlog:ab-repeat",
                1000,
                "individual-a",
                "individual-b"));
            telemetry.RecordObservation(new Experiment0AObservation(
                Experiment0ASource.CurrentTrigger,
                "trigger:ab",
                2000,
                2000,
                new[] { "individual-a", "individual-b" },
                4));

            var snapshot = telemetry.Snapshot();
            TestAssert.Equal(1L, snapshot.UniquePairsBySource["thought"],
                "A/B Thought evidence must own only its pair.");
            TestAssert.Equal(2L, snapshot.UniquePairsBySource["playlog"],
                "A/B and A/C PlayLog evidence must remain isolated.");
            TestAssert.Equal(1L, snapshot.RepeatedPairEventsBySource["playlog"],
                "Only the later A/B PlayLog event is a repeated PlayLog pair.");
            TestAssert.Equal(1L, snapshot.OverlapCounts600["playlog|thought"],
                "Only same-pair cross-source evidence inside 600 ticks overlaps.");
            TestAssert.Equal(1L, snapshot.PriorSamePairHistoryDepthCurrentTrigger["4_7"],
                "Current-trigger depth must use the supplied usable canonical history.");
            TestAssert.Equal(2L, snapshot.PriorObservedPairEpisodeDepthSemanticSource["0"],
                "The first independent A/B and A/C semantic opportunities start at depth zero.");
            var log = snapshot.ToLogLine("test", 2000);
            TestAssert.True(log.Contains("prior_observed_pair_episode_depth_semantic_source="),
                "The semantic-source depth field must identify itself as a within-run observed-pair episode proxy.");
            TestAssert.False(log.Contains("prior_same_pair_history_depth_semantic_source="),
                "The semantic-source proxy must not be labeled as durable same-pair history.");
        }

        public static void Experiment0AMalformedReflectionFailsClosedAndTelemetryAddsNoCognitionAuthority()
        {
            TestAssert.True(
                Experiment0AReflectedMemberReader.TryReadInt64(
                    new ReflectedFixture(),
                    out var value,
                    "age"),
                "A valid reflected integer member must be readable.");
            TestAssert.Equal(7L, value, "The reflected integer must remain exact.");
            TestAssert.False(
                Experiment0AReflectedMemberReader.TryReadInt64(
                    new MissingFixture(),
                    out _,
                    "age"),
                "An unavailable member must fail closed.");
            TestAssert.False(
                Experiment0AReflectedMemberReader.TryReadInt64(
                    new ThrowingFixture(),
                    out _,
                    "Age"),
                "A throwing reflected member must fail closed.");

            var declaredTypes = typeof(Experiment0ATelemetry)
                .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .Concat(typeof(Experiment0ATelemetry)
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
                        .Concat(new[] { method.ReturnType })))
                .Select(type => type.FullName ?? type.Name)
                .ToArray();
            TestAssert.False(
                declaredTypes.Any(name =>
                    name.IndexOf(".Development.", StringComparison.Ordinal) >= 0 ||
                    name.IndexOf(".Appraisal.", StringComparison.Ordinal) >= 0 ||
                    name.IndexOf("ContextualDevelopmental", StringComparison.Ordinal) >= 0 ||
                    name.IndexOf("GroundedCompoundAppraisal", StringComparison.Ordinal) >= 0 ||
                    name.IndexOf("ContextualProvisional", StringComparison.Ordinal) >= 0),
                "Experiment 0A telemetry must not make v41-v44 cognition types reachable.");
        }

        private static Experiment0AObservation Pair(
            Experiment0ASource source,
            string key,
            long tick,
            string first,
            string second) =>
            new Experiment0AObservation(
                source,
                key,
                tick,
                tick,
                new[] { first, second });

        private sealed class ReflectedFixture
        {
            internal int age = 7;
        }

        private sealed class MissingFixture
        {
        }

        private sealed class ThrowingFixture
        {
            public int Age => throw new InvalidOperationException("malformed fixture");
        }
    }
}
