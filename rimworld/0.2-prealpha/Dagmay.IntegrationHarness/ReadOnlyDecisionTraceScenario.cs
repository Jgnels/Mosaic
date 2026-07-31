using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dagmay.Core.Contracts;
using Dagmay.Core.Diagnostics;

namespace Dagmay.IntegrationHarness
{
    internal static partial class IntegrationScenarios
    {
        private static Task<ScenarioExecution> ReadOnlyDecisionTraceFoundationAsync()
        {
            var assertions = new HarnessAssert();
            var metrics = new Dictionary<string, long>(StringComparer.Ordinal);
            var details = new Dictionary<string, string>(StringComparer.Ordinal);
            var individual = IndividualId.Parse("e1000000000000000000000000000001");
            var fingerprint =
                "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
            var candidates = new[]
            {
                new DecisionTraceCandidate(
                    EventId.Parse("e2000000000000000000000000000001"),
                    "rimworld.relationship.direct_changed",
                    100,
                    2.9,
                    DecisionCandidateDisposition.Accepted,
                    "selected-opposite-valence",
                    1),
                new DecisionTraceCandidate(
                    EventId.Parse("e2000000000000000000000000000002"),
                    "rimworld.social.opinion_changed",
                    300,
                    1.8,
                    DecisionCandidateDisposition.Accepted,
                    "selected-same-valence",
                    2),
                new DecisionTraceCandidate(
                    EventId.Parse("e2000000000000000000000000000003"),
                    "rimworld.relationship.direct_changed",
                    400,
                    3.0,
                    DecisionCandidateDisposition.Rejected,
                    "privacy-filtered",
                    0),
                new DecisionTraceCandidate(
                    EventId.Parse("e2000000000000000000000000000004"),
                    "rimworld.social.opinion_changed",
                    450,
                    1.2,
                    DecisionCandidateDisposition.Rejected,
                    "not-selected",
                    0),
                new DecisionTraceCandidate(
                    EventId.Parse("e2000000000000000000000000000005"),
                    "rimworld.social.opinion_changed",
                    500,
                    1.1,
                    DecisionCandidateDisposition.Rejected,
                    "future-event",
                    0),
                new DecisionTraceCandidate(
                    EventId.Parse("e2000000000000000000000000000006"),
                    "rimworld.social.opinion_changed",
                    250,
                    1.0,
                    DecisionCandidateDisposition.Rejected,
                    "non-experienced",
                    0)
            };

            var builder = new ReadOnlyDecisionTraceBuilder();
            string? expectedTraceId = null;
            const int cycles = 128;
            for (var cycle = 0; cycle < cycles; cycle++)
            {
                var reordered = RotateTraceCandidates(candidates, cycle % candidates.Length);
                if ((cycle & 1) == 1)
                    reordered = reordered.Reverse().ToArray();

                var trace = builder.Build(
                    individual,
                    ReadOnlyDecisionOperation.GroundedDialogueSelection,
                    500,
                    "grounded-relationship-dialogue",
                    "0.2F",
                    reordered,
                    "selected=2",
                    "significance, weighted valence, recency, EventId ordinal",
                    fingerprint);

                if (expectedTraceId is null)
                    expectedTraceId = trace.TraceId;

                assertions.Equal(
                    expectedTraceId,
                    trace.TraceId,
                    "Equivalent trace input permutation " + cycle + " reproduces one trace ID.");
            }

            assertions.True(!string.IsNullOrWhiteSpace(expectedTraceId),
                "The trace integration scenario emits a deterministic digest.");
            metrics["cycles"] = cycles;
            metrics["candidates"] = candidates.Length;
            metrics["accepted"] = candidates.Count(value =>
                value.Disposition == DecisionCandidateDisposition.Accepted);
            metrics["rejected"] = candidates.Count(value =>
                value.Disposition == DecisionCandidateDisposition.Rejected);
            details["decisionTraceDigest"] = expectedTraceId!;
            return Task.FromResult(new ScenarioExecution(assertions.Assertions, metrics, details));
        }

        private static T[] RotateTraceCandidates<T>(IReadOnlyList<T> values, int offset)
        {
            var result = new T[values.Count];
            for (var index = 0; index < values.Count; index++)
                result[index] = values[(index + offset) % values.Count];
            return result;
        }
    }
}
