using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Memory;

namespace Dagmay.IntegrationHarness
{
    internal static partial class IntegrationScenarios
    {
        private static Task<ScenarioExecution> LongHistoryRetrievalBenchmarkAsync()
        {
            var assertions = new HarnessAssert();
            var metrics = new Dictionary<string, long>(StringComparer.Ordinal);
            var details = new Dictionary<string, string>(StringComparer.Ordinal);
            var cases = BuildLongHistoryBenchmarkCases();
            var composer = new GroundedRelationshipDialogueComposer();
            var csv = new StringBuilder();
            var digest = new StringBuilder();
            csv.AppendLine("case,candidates,mosaic_exact,most_recent_exact,generative_style_exact,selected_ids");

            var mosaicExact = 0;
            var mostRecentExact = 0;
            var generativeExact = 0;
            var totalCandidates = 0;
            const int permutationsPerCase = 64;

            foreach (var benchmarkCase in cases)
            {
                totalCandidates += benchmarkCase.Candidates.Count;
                var canonical = composer.Compose(
                    "Counterpart",
                    benchmarkCase.Current,
                    benchmarkCase.Candidates,
                    benchmarkCase.MaximumPriorEvidence);
                var actual = canonical.SelectedPriorEvidence
                    .Select(value => value.EventId)
                    .ToArray();
                var recent = BenchmarkMostRecentBaseline(
                    benchmarkCase.Current,
                    benchmarkCase.Candidates,
                    benchmarkCase.MaximumPriorEvidence);
                var generative = BenchmarkGenerativeStyleBaseline(
                    benchmarkCase.Current,
                    benchmarkCase.Candidates,
                    benchmarkCase.MaximumPriorEvidence);

                var mosaicPass = actual.SequenceEqual(benchmarkCase.ExpectedEvidenceIds);
                var recentPass = recent.SequenceEqual(benchmarkCase.ExpectedEvidenceIds);
                var generativePass = generative.SequenceEqual(benchmarkCase.ExpectedEvidenceIds);
                if (mosaicPass) mosaicExact++;
                if (recentPass) mostRecentExact++;
                if (generativePass) generativeExact++;

                assertions.True(
                    mosaicPass,
                    benchmarkCase.Name + " selects the expected provenance-bearing evidence.");
                assertions.True(
                    actual.All(value => benchmarkCase.Candidates.Any(candidate =>
                        candidate.EventId == value && candidate.CanEnterDialogue)),
                    benchmarkCase.Name + " emits no private, observer-only, or non-experienced evidence.");

                for (var permutation = 0; permutation < permutationsPerCase; permutation++)
                {
                    var reordered = RotateBenchmarkValues(
                        benchmarkCase.Candidates,
                        permutation % benchmarkCase.Candidates.Count);
                    if ((permutation & 1) == 1)
                        reordered = reordered.Reverse().ToArray();

                    var replay = composer.Compose(
                        "Counterpart",
                        benchmarkCase.Current,
                        reordered,
                        benchmarkCase.MaximumPriorEvidence);
                    assertions.True(
                        actual.SequenceEqual(replay.SelectedPriorEvidence.Select(value => value.EventId)),
                        benchmarkCase.Name + " remains deterministic for permutation " + permutation + ".");
                }

                var selectedText = string.Join(
                    ";",
                    actual.Select(value => value.ToString()));
                csv.Append(BenchmarkEscapeCsv(benchmarkCase.Name));
                csv.Append(',');
                csv.Append(benchmarkCase.Candidates.Count.ToString(CultureInfo.InvariantCulture));
                csv.Append(',');
                csv.Append(mosaicPass ? "1" : "0");
                csv.Append(',');
                csv.Append(recentPass ? "1" : "0");
                csv.Append(',');
                csv.Append(generativePass ? "1" : "0");
                csv.Append(',');
                csv.Append(BenchmarkEscapeCsv(selectedText));
                csv.AppendLine();

                digest.Append(benchmarkCase.Name);
                digest.Append('|');
                digest.Append(selectedText);
                digest.Append('|');
                digest.Append(mosaicPass ? '1' : '0');
                digest.Append(recentPass ? '1' : '0');
                digest.Append(generativePass ? '1' : '0');
                digest.AppendLine();
            }

            assertions.Equal(cases.Count, mosaicExact,
                "Mosaic must exactly match every fixed benchmark oracle.");
            assertions.Equal(500, totalCandidates,
                "The benchmark corpus must contain exactly 500 prior evidence candidates.");

            metrics["cases"] = cases.Count;
            metrics["corpusCandidates"] = totalCandidates;
            metrics["permutationsPerCase"] = permutationsPerCase;
            metrics["mosaicExact"] = mosaicExact;
            metrics["mostRecentExact"] = mostRecentExact;
            metrics["generativeStyleExact"] = generativeExact;
            metrics["privacyLeaks"] = 0;
            details["benchmarkDigest"] = LongHistoryBenchmarkSha256(digest.ToString());
            details["caseCsv"] = csv.ToString();
            details["oracleVersion"] = "mosaic-0.2G-fixed-corpus-v1";
            return Task.FromResult(new ScenarioExecution(assertions.Assertions, metrics, details));
        }

        private static IReadOnlyList<LongHistoryBenchmarkCase> BuildLongHistoryBenchmarkCases()
        {
            var cases = new List<LongHistoryBenchmarkCase>();
            var cursor = 100000;

            var current1 = BenchmarkOpinion(cursor++, 1000, 25);
            var target1 = BenchmarkDirect(cursor++, 100, "none", "Rival");
            cases.Add(BuildBenchmarkCase(
                "buried-negative-direct",
                current1,
                target1,
                1,
                BenchmarkFillNeutral(cursor, 49, 500)));
            cursor += 49;

            var current2 = BenchmarkOpinion(cursor++, 1000, -25);
            var target2 = BenchmarkDirect(cursor++, 100, "none", "Friend");
            cases.Add(BuildBenchmarkCase(
                "buried-positive-direct",
                current2,
                target2,
                1,
                BenchmarkFillNeutral(cursor, 49, 500)));
            cursor += 49;

            var current3 = BenchmarkOpinion(cursor++, 1000, 25);
            var negative3 = BenchmarkDirect(cursor++, 100, "none", "Enemy");
            var positive3 = BenchmarkDirect(cursor++, 110, "none", "Friend");
            cases.Add(BuildBenchmarkCase(
                "mixed-valence-direct-history",
                current3,
                new[] { negative3, positive3 },
                new[] { negative3.EventId, positive3.EventId },
                2,
                BenchmarkFillNeutral(cursor, 48, 500)));
            cursor += 48;

            var current4 = BenchmarkOpinion(cursor++, 1000, 25);
            var private4 = BenchmarkDirect(
                cursor++, 900, "none", "Enemy",
                PerceptionChannel.Experienced,
                PrivacyClassification.Private);
            var expected4 = BenchmarkOpinion(cursor++, 800, -30);
            cases.Add(BuildBenchmarkCase(
                "privacy-filter",
                current4,
                new[] { private4, expected4 },
                new[] { expected4.EventId },
                1,
                BenchmarkFillNeutral(cursor, 48, 500)));
            cursor += 48;

            var current5 = BenchmarkOpinion(cursor++, 1000, -25);
            var told5 = BenchmarkDirect(
                cursor++, 900, "none", "Friend",
                PerceptionChannel.Told,
                PrivacyClassification.Shareable);
            var expected5 = BenchmarkOpinion(cursor++, 800, 30);
            cases.Add(BuildBenchmarkCase(
                "non-experienced-filter",
                current5,
                new[] { told5, expected5 },
                new[] { expected5.EventId },
                1,
                BenchmarkFillNeutral(cursor, 48, 500)));
            cursor += 48;

            var current6 = BenchmarkOpinion(cursor++, 1000, -25);
            var lower6 = BenchmarkOpinion(cursor++, 700, 30);
            var higher6 = BenchmarkOpinion(cursor++, 700, 30);
            cases.Add(BuildBenchmarkCase(
                "equal-score-eventid-tie",
                current6,
                new[] { higher6, lower6 },
                new[] { lower6.EventId },
                1,
                BenchmarkFillNeutral(cursor, 48, 500)));
            cursor += 48;

            var current7 = BenchmarkOpinion(cursor++, 1000, -25);
            var future7 = BenchmarkDirect(cursor++, 1200, "none", "Friend");
            var expected7 = BenchmarkOpinion(cursor++, 900, 30);
            cases.Add(BuildBenchmarkCase(
                "future-event-filter",
                current7,
                new[] { future7, expected7 },
                new[] { expected7.EventId },
                1,
                BenchmarkFillNeutral(cursor, 48, 500)));
            cursor += 48;

            var current8 = BenchmarkOpinion(cursor++, 1000, -25);
            var duplicate8 = BenchmarkOpinion(cursor++, 800, 30);
            var duplicateCandidates8 = new List<GroundedRelationshipEvidence>
            {
                duplicate8,
                duplicate8
            };
            duplicateCandidates8.AddRange(BenchmarkFillNeutral(cursor, 48, 500));
            cases.Add(new LongHistoryBenchmarkCase(
                "duplicate-event-deduplication",
                current8,
                duplicateCandidates8,
                new[] { duplicate8.EventId },
                1));
            cursor += 48;

            var current9 = BenchmarkOpinion(cursor++, 1000, 0);
            var positive9 = BenchmarkDirect(cursor++, 100, "none", "Friend");
            var negative9 = BenchmarkDirect(cursor++, 110, "none", "Enemy");
            cases.Add(BuildBenchmarkCase(
                "neutral-current-mixed-history",
                current9,
                new[] { positive9, negative9 },
                new[] { positive9.EventId, negative9.EventId },
                2,
                BenchmarkFillNeutral(cursor, 48, 500)));
            cursor += 48;

            var current10 = BenchmarkOpinion(cursor++, 1000, -25);
            var direct10 = BenchmarkDirect(cursor++, 100, "none", "Friend");
            var recent10 = BenchmarkOpinion(cursor++, 999, 100);
            cases.Add(BuildBenchmarkCase(
                "direct-relation-over-newer-opinion",
                current10,
                new[] { recent10, direct10 },
                new[] { direct10.EventId },
                1,
                BenchmarkFillNeutral(cursor, 48, 500)));

            return cases;
        }

        private static LongHistoryBenchmarkCase BuildBenchmarkCase(
            string name,
            GroundedRelationshipEvidence current,
            GroundedRelationshipEvidence target,
            int maximum,
            IEnumerable<GroundedRelationshipEvidence> filler) =>
            BuildBenchmarkCase(
                name,
                current,
                new[] { target },
                new[] { target.EventId },
                maximum,
                filler);

        private static LongHistoryBenchmarkCase BuildBenchmarkCase(
            string name,
            GroundedRelationshipEvidence current,
            IEnumerable<GroundedRelationshipEvidence> special,
            IEnumerable<EventId> expected,
            int maximum,
            IEnumerable<GroundedRelationshipEvidence> filler)
        {
            var candidates = special.Concat(filler).ToArray();
            if (candidates.Length != 50)
                throw new InvalidOperationException(name + " must contain exactly 50 candidates.");
            return new LongHistoryBenchmarkCase(
                name,
                current,
                candidates,
                expected.ToArray(),
                maximum);
        }

        private static IReadOnlyList<GroundedRelationshipEvidence> BenchmarkFillNeutral(
            int firstId,
            int count,
            long firstTick)
        {
            var values = new List<GroundedRelationshipEvidence>(count);
            for (var index = 0; index < count; index++)
                values.Add(BenchmarkOpinion(firstId + index, firstTick + index, 0));
            return values;
        }

        private static GroundedRelationshipEvidence BenchmarkOpinion(
            int id,
            long tick,
            int delta,
            PerceptionChannel channel = PerceptionChannel.Experienced,
            PrivacyClassification privacy = PrivacyClassification.Shareable) =>
            new GroundedRelationshipEvidence(
                BenchmarkEventId(id),
                "rimworld.social.opinion_changed",
                tick,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target_external_id"] = "Thing_Counterpart",
                    ["target_name"] = "Counterpart",
                    ["opinion_before"] = "0",
                    ["opinion_after"] = delta.ToString(CultureInfo.InvariantCulture),
                    ["opinion_delta"] = delta.ToString(CultureInfo.InvariantCulture)
                },
                channel,
                privacy,
                1.0);

        private static GroundedRelationshipEvidence BenchmarkDirect(
            int id,
            long tick,
            string before,
            string after,
            PerceptionChannel channel = PerceptionChannel.Experienced,
            PrivacyClassification privacy = PrivacyClassification.Shareable) =>
            new GroundedRelationshipEvidence(
                BenchmarkEventId(id),
                "rimworld.relationship.direct_changed",
                tick,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target_external_id"] = "Thing_Counterpart",
                    ["target_name"] = "Counterpart",
                    ["relations_before"] = before,
                    ["relations_after"] = after
                },
                channel,
                privacy,
                1.0);

        private static EventId BenchmarkEventId(int value) =>
            EventId.Parse(value.ToString("x32", CultureInfo.InvariantCulture));

        private static EventId[] BenchmarkMostRecentBaseline(
            GroundedRelationshipEvidence current,
            IEnumerable<GroundedRelationshipEvidence> candidates,
            int maximum) =>
            BenchmarkEligible(current, candidates)
                .OrderByDescending(value => value.OccurredAtTick)
                .ThenBy(value => value.EventId.ToString(), StringComparer.Ordinal)
                .Take(maximum)
                .Select(value => value.EventId)
                .ToArray();

        private static EventId[] BenchmarkGenerativeStyleBaseline(
            GroundedRelationshipEvidence current,
            IEnumerable<GroundedRelationshipEvidence> candidates,
            int maximum)
        {
            var currentSign = BenchmarkSign(current.Valence);
            return BenchmarkEligible(current, candidates)
                .OrderByDescending(value =>
                    BenchmarkRecency(value, current) +
                    BenchmarkImportance(value) +
                    BenchmarkRelevance(value, currentSign))
                .ThenByDescending(value => value.OccurredAtTick)
                .ThenBy(value => value.EventId.ToString(), StringComparer.Ordinal)
                .Take(maximum)
                .Select(value => value.EventId)
                .ToArray();
        }

        private static IEnumerable<GroundedRelationshipEvidence> BenchmarkEligible(
            GroundedRelationshipEvidence current,
            IEnumerable<GroundedRelationshipEvidence> candidates) =>
            candidates
                .Where(value => value.EventId != current.EventId)
                .Where(value => value.OccurredAtTick <= current.OccurredAtTick)
                .Where(value => value.CanEnterDialogue)
                .Where(value => GroundedRelationshipDialogueComposer.IsSupportedKind(value.EventKind))
                .GroupBy(value => value.EventId)
                .Select(value => value.First());

        private static double BenchmarkRecency(
            GroundedRelationshipEvidence value,
            GroundedRelationshipEvidence current) =>
            current.OccurredAtTick == 0
                ? 0
                : Math.Max(0, Math.Min(1, value.OccurredAtTick / (double)current.OccurredAtTick));

        private static double BenchmarkImportance(GroundedRelationshipEvidence value) =>
            string.Equals(
                value.EventKind,
                "rimworld.relationship.direct_changed",
                StringComparison.Ordinal)
                ? 1.0
                : Math.Abs(value.Valence);

        private static double BenchmarkRelevance(GroundedRelationshipEvidence value, int currentSign)
        {
            var candidateSign = BenchmarkSign(value.Valence);
            if (currentSign == 0) return Math.Abs(value.Valence);
            if (candidateSign == -currentSign) return 1.0;
            if (candidateSign == currentSign) return 0.5;
            return 0.1;
        }

        private static int BenchmarkSign(double value)
        {
            if (value > 0.05) return 1;
            if (value < -0.05) return -1;
            return 0;
        }

        private static T[] RotateBenchmarkValues<T>(
            IReadOnlyList<T> values,
            int offset)
        {
            var result = new T[values.Count];
            for (var index = 0; index < values.Count; index++)
                result[index] = values[(index + offset) % values.Count];
            return result;
        }

        private static string BenchmarkEscapeCsv(string value)
        {
            var normalized = value ?? string.Empty;
            if (normalized.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
                return normalized;
            return "\"" + normalized.Replace("\"", "\"\"") + "\"";
        }

        private static string LongHistoryBenchmarkSha256(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var valueByte in bytes)
                    builder.Append(valueByte.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private sealed class LongHistoryBenchmarkCase
        {
            public LongHistoryBenchmarkCase(
                string name,
                GroundedRelationshipEvidence current,
                IReadOnlyList<GroundedRelationshipEvidence> candidates,
                IReadOnlyList<EventId> expectedEvidenceIds,
                int maximumPriorEvidence)
            {
                Name = name;
                Current = current;
                Candidates = candidates;
                ExpectedEvidenceIds = expectedEvidenceIds;
                MaximumPriorEvidence = maximumPriorEvidence;
            }

            public string Name { get; }
            public GroundedRelationshipEvidence Current { get; }
            public IReadOnlyList<GroundedRelationshipEvidence> Candidates { get; }
            public IReadOnlyList<EventId> ExpectedEvidenceIds { get; }
            public int MaximumPriorEvidence { get; }
        }
    }
}
