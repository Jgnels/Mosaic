using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Identity;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;
using Dagmay.Core.Reflection;
using Dagmay.Core.Scheduling;

namespace Dagmay.Tests
{
    internal static class DeterminismLifecycleClosureContractTests
    {
        public static void CombinedObserverReadsPreserveCanonicalFingerprint()
        {
            var fixture = CreateCanonicalFixture();
            var before = Fingerprint(fixture);

            for (var iteration = 0; iteration < 8; iteration++)
            {
                var prior = iteration % 2 == 0
                    ? fixture.RelationshipEvidence
                    : fixture.RelationshipEvidence.Reverse().ToArray();
                var plan = new GroundedRelationshipDialogueComposer().Compose(
                    "Walter",
                    fixture.CurrentEvidence,
                    prior);
                TestAssert.True(plan.EvidenceIds.Count >= 1,
                    "A deterministic relationship read must retain exact evidence.");

                var turns = iteration % 2 == 0
                    ? fixture.PriorTurns
                    : fixture.PriorTurns.Reverse().ToArray();
                var selected = new CrossEncounterConversationSelector()
                    .SelectLatestCompletedExchange(
                        turns,
                        fixture.First.Id,
                        fixture.Second.Id,
                        1000);
                TestAssert.True(selected is not null,
                    "The completed prior exchange must remain readable.");

                var viewer = new ConversationHistoryViewerBuilder().Build(
                    fixture.First.Id,
                    fixture.Participants,
                    turns,
                    100);
                TestAssert.Equal(2, viewer.Rows.Count,
                    "The participant-scoped viewer must expose both witnessed turns.");
            }

            var after = Fingerprint(fixture);
            TestAssert.Equal(before, after,
                "Dialogue selection, continuity selection, and history viewing must preserve canonical state.");
        }

        public static void GroundedDialogueEqualScoreTieBreakUsesStableEventId()
        {
            var current = Opinion(
                "b1000000000000000000000000000009",
                500,
                0);
            var laterLexical = Opinion(
                "b1000000000000000000000000000002",
                300,
                25);
            var earlierLexical = Opinion(
                "b1000000000000000000000000000001",
                300,
                25);

            var forward = new GroundedRelationshipDialogueComposer().Compose(
                "Walter",
                current,
                new[] { laterLexical, earlierLexical },
                maximumPriorEvidence: 1);
            var reverse = new GroundedRelationshipDialogueComposer().Compose(
                "Walter",
                current,
                new[] { earlierLexical, laterLexical },
                maximumPriorEvidence: 1);

            TestAssert.Equal(
                earlierLexical.EventId,
                forward.SelectedPriorEvidence[0].EventId,
                "Equal-score dialogue evidence must use stable EventId ordering.");
            TestAssert.True(
                forward.EvidenceIds.SequenceEqual(reverse.EvidenceIds),
                "Caller enumeration order cannot alter equal-score evidence selection.");
            TestAssert.Equal(forward.Text, reverse.Text,
                "Caller enumeration order cannot alter deterministic dialogue text.");
        }

        public static void CompletedConversationTieBreakIsStableAcrossInputOrder()
        {
            var first = IndividualId.Parse("b2000000000000000000000000000001");
            var second = IndividualId.Parse("b2000000000000000000000000000002");
            var lower = Conversation(
                "b3000000000000000000000000000001",
                first,
                second,
                100,
                101);
            var higher = Conversation(
                "b3000000000000000000000000000002",
                first,
                second,
                100,
                101);
            var all = higher.Concat(lower).ToArray();

            var selector = new CrossEncounterConversationSelector();
            var forward = selector.SelectLatestCompletedExchange(all, first, second, 1000);
            var reverse = selector.SelectLatestCompletedExchange(all.Reverse(), first, second, 1000);

            TestAssert.True(forward is not null && reverse is not null,
                "Both deterministic selections must find a completed exchange.");
            TestAssert.Equal(
                new ConversationId(Guid.ParseExact("b3000000000000000000000000000001", "N")),
                forward!.ConversationId,
                "Equal-time completed exchanges must use stable ConversationId ordering.");
            TestAssert.Equal(forward.ConversationId, reverse!.ConversationId,
                "Input enumeration order cannot alter completed-exchange selection.");
        }

        public static void ConversationViewerRepeatedReadsAreStableAndDuplicateSafe()
        {
            var first = IndividualId.Parse("b4000000000000000000000000000001");
            var second = IndividualId.Parse("b4000000000000000000000000000002");
            var participants = new[]
            {
                new ConversationHistoryParticipant(first, "Candice"),
                new ConversationHistoryParticipant(second, "Walter")
            };
            var turns = Conversation(
                "b5000000000000000000000000000001",
                first,
                second,
                100,
                101);
            var duplicatedAndReversed = turns
                .Concat(turns)
                .Reverse()
                .ToArray();

            var builder = new ConversationHistoryViewerBuilder();
            var firstRead = builder.Build(first, participants, duplicatedAndReversed, 100);
            var secondRead = builder.Build(first, participants.Reverse(), turns, 100);

            TestAssert.Equal(2, firstRead.Rows.Count,
                "Repeated canonical dialogue EventIds cannot appear twice in the viewer.");
            TestAssert.True(
                firstRead.Rows.Select(value => value.EventId)
                    .SequenceEqual(secondRead.Rows.Select(value => value.EventId)),
                "Participant and turn input order cannot alter viewer ordering.");
            TestAssert.Equal("I noticed the change between us.", turns[0].Text,
                "Read-only viewer construction must not rewrite the first source turn.");
            TestAssert.Equal("I heard you, and my view is my own.", turns[1].Text,
                "Read-only viewer construction must not rewrite the second source turn.");
        }

        public static void IdentityLifecycleAndBindingReconstructionPreserveContinuity()
        {
            var initial = CreateIndividual("Mira");
            var archived = initial.TransitionLifecycle(
                Dagmay.Core.Lifecycle.LifecycleState.Archived,
                Dagmay.Core.Lifecycle.LifecycleTransitionKind.InWorldDeath,
                initial.Version);
            var revived = archived.TransitionLifecycle(
                Dagmay.Core.Lifecycle.LifecycleState.Active,
                Dagmay.Core.Lifecycle.LifecycleTransitionKind.InWorldRevival,
                archived.Version);
            var renamed = revived.Rename("Mira Vale", revived.Version);

            var bound = new EnvironmentBinding(
                initial.Id,
                EnvironmentBindingState.Bound,
                "Thing_Mira",
                100);
            var reconstructed = new EnvironmentBinding(
                renamed.Id,
                EnvironmentBindingState.Bound,
                "Thing_Mira_Recreated",
                1000);

            TestAssert.Equal(initial.Id, archived.Id,
                "Archival cannot replace IndividualId.");
            TestAssert.Equal(initial.Id, revived.Id,
                "In-world revival cannot replace IndividualId.");
            TestAssert.Equal(initial.Id, renamed.Id,
                "Rename cannot replace IndividualId.");
            TestAssert.Equal(initial.LineageId, renamed.LineageId,
                "Lifecycle and rename operations cannot replace LineageId.");
            TestAssert.True(bound is not null && reconstructed is not null,
                "Transient environment bindings must reconstruct around the durable individual without replacing it.");
        }

        public static void CanonicalFingerprintChangesWhenFixtureActuallyMutates()
        {
            var fixture = CreateCanonicalFixture();
            var before = Fingerprint(fixture);
            fixture.Ledger.Append(new EnvironmentEvent(
                EventId.Parse("b6000000000000000000000000000009"),
                "determinism:mutation:9",
                "rimworld.test",
                "rimworld",
                fixture.Now.AddMinutes(9),
                fixture.Now.AddMinutes(9),
                900,
                "offline mutation sensitivity fixture",
                new Dictionary<string, string> { ["result"] = "changed" },
                new[] { fixture.First.Id }));
            var after = Fingerprint(fixture);

            TestAssert.False(string.Equals(before, after, StringComparison.Ordinal),
                "The purity fingerprint must be sensitive to a real canonical mutation.");
        }

        private static string Fingerprint(CanonicalFixture fixture) =>
            CanonicalStateFingerprint.Compute(
                fixture.Archive,
                fixture.Binding,
                fixture.Ledger.Snapshot(),
                fixture.MemoryIndex.Snapshot(),
                fixture.Queue.Snapshot());

        private static CanonicalFixture CreateCanonicalFixture()
        {
            var now = new DateTimeOffset(2026, 7, 27, 1, 0, 0, TimeSpan.Zero);
            var first = CreateIndividual("Candice");
            var second = CreateIndividual("Walter");
            var source = new EnvironmentEvent(
                EventId.Parse("b6000000000000000000000000000001"),
                "determinism:event:1",
                "rimworld.social.opinion_changed",
                "rimworld",
                now,
                now,
                500,
                "offline determinism fixture",
                new Dictionary<string, string>
                {
                    ["target_external_id"] = "Thing_Walter",
                    ["target_name"] = "Walter",
                    ["opinion_before"] = "0",
                    ["opinion_after"] = "20",
                    ["opinion_delta"] = "20"
                },
                new[] { first.Id, second.Id });
            var perceptionId = new PerceptionId(Guid.ParseExact("b7000000000000000000000000000001", "N"));
            var memory = new SubjectiveMemory(
                new MemoryId(Guid.ParseExact("b8000000000000000000000000000001", "N")),
                first.Id,
                new[] { perceptionId },
                now,
                now.AddSeconds(1),
                "Walter and I had a meaningful interaction.",
                "The event changed the relationship.",
                AffectVector.Neutral,
                0.7,
                0.4,
                0.9,
                0.8,
                MemoryTier.Recent,
                PrivacyClassification.RelationshipSensitive,
                new[] { second.Id });
            var memoryIndex = new MemoryIndex();
            memoryIndex.Add(memory);
            var ledger = new InMemoryEventLedger();
            ledger.Append(source);
            var queue = new PersistentReflectionQueue(8);
            queue.EnqueueOrMerge(new ReflectionTask(
                new ReflectionTaskId(Guid.ParseExact("b9000000000000000000000000000001", "N")),
                first.Id,
                ModelTaskKind.InterpretMeaningfulEvent,
                ReflectionPriority.MeaningfulEvent,
                now,
                "determinism:reflection",
                new[] { source.Id },
                256));
            var archive = new IdentityArchiveSnapshot(
                Guid.Parse("ba000000-0000-0000-0000-000000000001"),
                3,
                now,
                new[]
                {
                    new PersistedIdentityRecord("Thing_Candice", first),
                    new PersistedIdentityRecord("Thing_Walter", second)
                });
            var binding = new EnvironmentBinding(
                first.Id,
                EnvironmentBindingState.Bound,
                "Thing_Candice",
                500);
            var relationshipEvidence = new[]
            {
                Opinion("bb000000000000000000000000000001", 100, 25),
                Opinion("bb000000000000000000000000000002", 200, -20)
            };
            var current = Opinion("bb000000000000000000000000000003", 500, -25);
            var priorTurns = Conversation(
                "bc000000000000000000000000000001",
                first.Id,
                second.Id,
                300,
                301);
            var participants = new[]
            {
                new ConversationHistoryParticipant(first.Id, first.DisplayName),
                new ConversationHistoryParticipant(second.Id, second.DisplayName)
            };
            return new CanonicalFixture(
                now,
                first,
                second,
                archive,
                binding,
                ledger,
                memoryIndex,
                queue,
                current,
                relationshipEvidence,
                priorTurns,
                participants);
        }

        private static IndividualState CreateIndividual(string name) =>
            IndividualState.Create(
                name,
                new IdentitySeed(
                    "rimworld",
                    "0.2-prealpha",
                    new[]
                    {
                        new SeedFact(
                            SeedFactCategory.Trait,
                            "kind",
                            "Kind",
                            "RimWorld trait",
                            1.0)
                    }));

        private static GroundedRelationshipEvidence Opinion(
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

        private static IReadOnlyList<PriorConversationTurn> Conversation(
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
                    EventIdFromConversation(conversationId, 1),
                    conversation,
                    first,
                    second,
                    "I noticed the change between us.",
                    firstTick,
                    new DateTimeOffset(2026, 7, 27, 2, 0, 0, TimeSpan.Zero),
                    DialoguePresentationChannel.Bubble,
                    audience),
                new PriorConversationTurn(
                    EventIdFromConversation(conversationId, 2),
                    conversation,
                    second,
                    first,
                    "I heard you, and my view is my own.",
                    secondTick,
                    new DateTimeOffset(2026, 7, 27, 2, 0, 1, TimeSpan.Zero),
                    DialoguePresentationChannel.Bubble,
                    audience)
            };
        }

        private static EventId EventIdFromConversation(string conversationId, int turn)
        {
            var bytes = Guid.ParseExact(conversationId, "N").ToByteArray();
            bytes[15] ^= (byte)(0x20 + turn);
            return new EventId(new Guid(bytes));
        }

        private sealed class CanonicalFixture
        {
            public CanonicalFixture(
                DateTimeOffset now,
                IndividualState first,
                IndividualState second,
                IdentityArchiveSnapshot archive,
                EnvironmentBinding binding,
                InMemoryEventLedger ledger,
                MemoryIndex memoryIndex,
                PersistentReflectionQueue queue,
                GroundedRelationshipEvidence currentEvidence,
                IReadOnlyList<GroundedRelationshipEvidence> relationshipEvidence,
                IReadOnlyList<PriorConversationTurn> priorTurns,
                IReadOnlyList<ConversationHistoryParticipant> participants)
            {
                Now = now;
                First = first;
                Second = second;
                Archive = archive;
                Binding = binding;
                Ledger = ledger;
                MemoryIndex = memoryIndex;
                Queue = queue;
                CurrentEvidence = currentEvidence;
                RelationshipEvidence = relationshipEvidence;
                PriorTurns = priorTurns;
                Participants = participants;
            }

            public DateTimeOffset Now { get; }
            public IndividualState First { get; }
            public IndividualState Second { get; }
            public IdentityArchiveSnapshot Archive { get; }
            public EnvironmentBinding Binding { get; }
            public InMemoryEventLedger Ledger { get; }
            public MemoryIndex MemoryIndex { get; }
            public PersistentReflectionQueue Queue { get; }
            public GroundedRelationshipEvidence CurrentEvidence { get; }
            public IReadOnlyList<GroundedRelationshipEvidence> RelationshipEvidence { get; }
            public IReadOnlyList<PriorConversationTurn> PriorTurns { get; }
            public IReadOnlyList<ConversationHistoryParticipant> Participants { get; }
        }
    }
}
