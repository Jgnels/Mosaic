using System;
using System.Collections.Generic;
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
        private static Task<ScenarioExecution> DeterminismObserverLifecycleClosureAsync()
        {
            var assertions = new HarnessAssert();
            var metrics = new Dictionary<string, long>(StringComparer.Ordinal);
            const int cycles = 128;
            var first = IndividualId.Parse("c1000000000000000000000000000001");
            var second = IndividualId.Parse("c1000000000000000000000000000002");
            var current = DeterminismOpinion(
                "c2000000000000000000000000000009",
                500,
                -25);
            var relationshipEvidence = new[]
            {
                DeterminismOpinion("c2000000000000000000000000000001", 100, 30),
                DeterminismOpinion("c2000000000000000000000000000002", 200, -20),
                DeterminismOpinion("c2000000000000000000000000000003", 250, 16),
                DeterminismOpinion("c2000000000000000000000000000004", 250, 16)
            };
            var conversationA = DeterminismConversation(
                "c3000000000000000000000000000001",
                first,
                second,
                300,
                301);
            var conversationB = DeterminismConversation(
                "c3000000000000000000000000000002",
                first,
                second,
                300,
                301);
            var allTurns = conversationB.Concat(conversationA).ToArray();
            var participants = new[]
            {
                new ConversationHistoryParticipant(first, "Candice"),
                new ConversationHistoryParticipant(second, "Walter")
            };

            string? baseline = null;
            for (var cycle = 0; cycle < cycles; cycle++)
            {
                var evidenceOrder = Rotate(
                    relationshipEvidence,
                    cycle % relationshipEvidence.Length,
                    reverse: (cycle & 1) != 0);
                var turnOrder = Rotate(
                    allTurns,
                    cycle % allTurns.Length,
                    reverse: (cycle & 2) != 0);
                var participantOrder = (cycle & 4) == 0
                    ? participants
                    : participants.Reverse().ToArray();

                var plan = new GroundedRelationshipDialogueComposer().Compose(
                    "Walter",
                    current,
                    evidenceOrder);
                var selected = new CrossEncounterConversationSelector()
                    .SelectLatestCompletedExchange(turnOrder, first, second, 1000)
                    ?? throw new HarnessAssertionException(
                        "Determinism scenario did not select a completed exchange.");
                var viewer = new ConversationHistoryViewerBuilder().Build(
                    first,
                    participantOrder,
                    turnOrder,
                    100);
                var signature = string.Join("|", new[]
                {
                    plan.Text,
                    string.Join(",", plan.EvidenceIds.Select(value => value.ToString())),
                    selected.ConversationId.ToString(),
                    string.Join(",", viewer.Rows.Select(value => value.EventId.ToString()))
                });

                if (baseline is null)
                {
                    baseline = signature;
                    assertions.True(true,
                        "The first deterministic replay establishes a canonical signature.");
                }
                else
                {
                    assertions.Equal(baseline, signature,
                        "Equivalent histories must produce one signature regardless of enumeration order.");
                }
            }

            var digest = Sha256Hex(baseline ?? string.Empty);
            assertions.Equal(64, digest.Length,
                "The deterministic closure signature must be represented by SHA-256 hex.");
            metrics["cycles"] = cycles;
            metrics["relationshipEvidence"] = relationshipEvidence.Length;
            metrics["conversationTurns"] = allTurns.Length;
            metrics["viewerRows"] = 4;
            var details = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["deterministicDigest"] = digest,
                ["stableConversationId"] = "c3000000000000000000000000000001"
            };
            return Task.FromResult(new ScenarioExecution(
                assertions.Assertions,
                metrics,
                details));
        }

        private static GroundedRelationshipEvidence DeterminismOpinion(
            string id,
            long tick,
            int delta) =>
            new GroundedRelationshipEvidence(
                EventId.Parse(id),
                "rimworld.social.opinion_changed",
                tick,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target_external_id"] = "Thing_Walter",
                    ["target_name"] = "Walter",
                    ["opinion_before"] = "0",
                    ["opinion_after"] = delta.ToString(),
                    ["opinion_delta"] = delta.ToString()
                },
                PerceptionChannel.Experienced,
                PrivacyClassification.RelationshipSensitive,
                1.0);

        private static IReadOnlyList<PriorConversationTurn> DeterminismConversation(
            string conversationId,
            IndividualId first,
            IndividualId second,
            long firstTick,
            long secondTick)
        {
            var conversation = new ConversationId(Guid.ParseExact(conversationId, "N"));
            var audience = new[] { first, second };
            return new[]
            {
                new PriorConversationTurn(
                    DeterminismEventId(conversationId, 1),
                    conversation,
                    first,
                    second,
                    "I noticed the change between us.",
                    firstTick,
                    BaseTime.AddTicks(firstTick),
                    DialoguePresentationChannel.Bubble,
                    audience),
                new PriorConversationTurn(
                    DeterminismEventId(conversationId, 2),
                    conversation,
                    second,
                    first,
                    "I heard you, and my view is my own.",
                    secondTick,
                    BaseTime.AddTicks(secondTick),
                    DialoguePresentationChannel.Bubble,
                    audience)
            };
        }

        private static EventId DeterminismEventId(string conversationId, int turn)
        {
            var bytes = Guid.ParseExact(conversationId, "N").ToByteArray();
            bytes[15] ^= (byte)(0x40 + turn);
            return new EventId(new Guid(bytes));
        }

        private static T[] Rotate<T>(IReadOnlyList<T> source, int offset, bool reverse)
        {
            var values = new T[source.Count];
            for (var index = 0; index < source.Count; index++)
                values[index] = source[(index + offset) % source.Count];
            if (reverse) Array.Reverse(values);
            return values;
        }

        private static string Sha256Hex(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                return string.Concat(
                    algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))
                        .Select(item => item.ToString("x2")));
            }
        }
    }
}
