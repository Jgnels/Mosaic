using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Contracts;
using Dagmay.Core.Diagnostics;

namespace Dagmay.Tests
{
    internal static class ReadOnlyDecisionTraceContractTests
    {
        private const string CanonicalHash =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        public static void TraceIsDeterministicAcrossCandidateEnumerationOrder()
        {
            var builder = new ReadOnlyDecisionTraceBuilder();
            var individual = IndividualId.Parse("d1000000000000000000000000000001");
            var candidates = Candidates();

            var forward = builder.Build(
                individual,
                ReadOnlyDecisionOperation.GroundedDialogueSelection,
                500,
                "grounded-relationship-dialogue",
                "0.2F",
                candidates,
                "selected=2",
                "significance, weighted valence, recency, EventId ordinal",
                CanonicalHash);
            var reverse = builder.Build(
                individual,
                ReadOnlyDecisionOperation.GroundedDialogueSelection,
                500,
                "grounded-relationship-dialogue",
                "0.2F",
                candidates.Reverse(),
                "selected=2",
                "significance, weighted valence, recency, EventId ordinal",
                CanonicalHash);

            TestAssert.Equal(forward.TraceId, reverse.TraceId,
                "Equivalent candidate sets must produce one deterministic trace ID.");
            TestAssert.True(
                forward.Candidates.Select(value => value.EvidenceId)
                    .SequenceEqual(reverse.Candidates.Select(value => value.EvidenceId)),
                "Equivalent candidate sets must produce one canonical candidate order.");
        }

        public static void TraceCarriesAcceptedAndRejectedEvidenceWithReasons()
        {
            var trace = Build(Candidates());

            TestAssert.Equal(2, trace.AcceptedEvidenceIds.Count,
                "Accepted evidence remains explicit and bounded.");
            TestAssert.Equal(2, trace.RejectedEvidenceIds.Count,
                "Rejected evidence remains explicit and bounded.");
            TestAssert.True(
                trace.Candidates.Any(value =>
                    value.Disposition == DecisionCandidateDisposition.Rejected &&
                    value.ReasonCode == "privacy-filtered"),
                "A rejected candidate retains its explicit privacy reason.");
            TestAssert.True(trace.PreservesCanonicalState,
                "A read-only trace exposes its observer-purity invariant.");
        }

        public static void TraceRejectsCanonicalMutation()
        {
            var candidates = Candidates();
            TestAssert.Throws<InvalidOperationException>(
                () => new ReadOnlyDecisionTraceBuilder().BuildWithFingerprints(
                    IndividualId.Parse("d1000000000000000000000000000001"),
                    ReadOnlyDecisionOperation.Retrieval,
                    10,
                    "fixture",
                    "0.2F",
                    candidates,
                    "result",
                    "EventId ordinal",
                    new string('c', 64),
                    new string('d', 64)),
                "A read-only trace must reject unequal before and after fingerprints.");
        }

        public static void TraceRejectsDuplicateEvidenceAndRankGaps()
        {
            var duplicate = EventId.Parse("d2000000000000000000000000000001");
            TestAssert.Throws<ArgumentException>(
                () => Build(new[]
                {
                    new DecisionTraceCandidate(
                        duplicate, "opinion", 10, 1,
                        DecisionCandidateDisposition.Accepted, "selected", 1),
                    new DecisionTraceCandidate(
                        duplicate, "opinion", 9, 0.5,
                        DecisionCandidateDisposition.Rejected, "not-selected", 0)
                }),
                "One canonical trace cannot contain the same evidence ID twice.");

            TestAssert.Throws<ArgumentException>(
                () => Build(new[]
                {
                    new DecisionTraceCandidate(
                        EventId.Parse("d2000000000000000000000000000002"),
                        "opinion", 10, 1,
                        DecisionCandidateDisposition.Accepted, "selected", 2)
                }),
                "Accepted ranks must begin at one and remain contiguous.");
        }

        public static void TraceIdChangesWhenDecisionChanges()
        {
            var first = Build(Candidates());
            var changed = Candidates().ToArray();
            changed[3] = new DecisionTraceCandidate(
                changed[3].EvidenceId,
                changed[3].CandidateKind,
                changed[3].OccurredAtTick,
                changed[3].Score + 0.01,
                changed[3].Disposition,
                changed[3].ReasonCode,
                changed[3].Rank);
            var second = Build(changed);

            TestAssert.False(
                string.Equals(first.TraceId, second.TraceId, StringComparison.Ordinal),
                "A material decision-trace change must alter the deterministic trace ID.");
        }

        public static void TraceCopiesSourceCollectionsAndEnforcesBounds()
        {
            var source = Candidates().ToList();
            var trace = Build(source);
            source.Clear();

            TestAssert.Equal(4, trace.Candidates.Count,
                "Trace construction must copy source collections.");

            var excessive = Enumerable.Range(1, ReadOnlyDecisionTraceBuilder.MaximumCandidates + 1)
                .Select(index => new DecisionTraceCandidate(
                    EventId.Parse(index.ToString("x32")),
                    "candidate",
                    index,
                    index,
                    DecisionCandidateDisposition.Rejected,
                    "not-selected",
                    0))
                .ToArray();
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => Build(excessive),
                "Decision traces must reject unbounded candidate collections.");
        }

        private static ReadOnlyDecisionTrace Build(
            IEnumerable<DecisionTraceCandidate> candidates) =>
            new ReadOnlyDecisionTraceBuilder().Build(
                IndividualId.Parse("d1000000000000000000000000000001"),
                ReadOnlyDecisionOperation.GroundedDialogueSelection,
                500,
                "grounded-relationship-dialogue",
                "0.2F",
                candidates,
                "selected=2",
                "significance, weighted valence, recency, EventId ordinal",
                CanonicalHash);

        private static IReadOnlyList<DecisionTraceCandidate> Candidates() =>
            new[]
            {
                new DecisionTraceCandidate(
                    EventId.Parse("d2000000000000000000000000000001"),
                    "rimworld.relationship.direct_changed",
                    100,
                    2.9,
                    DecisionCandidateDisposition.Accepted,
                    "selected-opposite-valence",
                    1),
                new DecisionTraceCandidate(
                    EventId.Parse("d2000000000000000000000000000002"),
                    "rimworld.social.opinion_changed",
                    300,
                    1.8,
                    DecisionCandidateDisposition.Accepted,
                    "selected-same-valence",
                    2),
                new DecisionTraceCandidate(
                    EventId.Parse("d2000000000000000000000000000003"),
                    "rimworld.relationship.direct_changed",
                    400,
                    3.0,
                    DecisionCandidateDisposition.Rejected,
                    "privacy-filtered",
                    0),
                new DecisionTraceCandidate(
                    EventId.Parse("d2000000000000000000000000000004"),
                    "rimworld.social.opinion_changed",
                    450,
                    1.2,
                    DecisionCandidateDisposition.Rejected,
                    "not-selected",
                    0)
            };
    }
}
