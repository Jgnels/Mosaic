using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;

namespace Dagmay.Tests
{
    internal static class ConversationHistoryViewerContractTests
    {
        public static void ViewerContainsOnlyWitnessedParticipantSpeech()
        {
            var a = IndividualId.Parse("a1000000000000000000000000000001");
            var b = IndividualId.Parse("a1000000000000000000000000000002");
            var c = IndividualId.Parse("a1000000000000000000000000000003");
            var participants = Participants(a, b, c);
            var turns = new[]
            {
                Turn("a2000000000000000000000000000001", a, b, 100, new[] { a, b }),
                Turn("a2000000000000000000000000000002", b, a, 101, new[] { a, b }),
                Turn("a2000000000000000000000000000003", b, c, 200, new[] { b, c })
            };

            var snapshot = new ConversationHistoryViewerBuilder().Build(
                a,
                participants,
                turns,
                100);

            TestAssert.Equal(2, snapshot.Rows.Count,
                "A participant-scoped viewer must exclude another pair's conversation.");
            TestAssert.True(snapshot.Rows.All(row =>
                row.SpeakerId == a || row.RecipientId == a),
                "Every visible row must directly involve the selected participant.");
            TestAssert.Equal(1, snapshot.PrivacyFilteredCount,
                "The unrelated witnessed pair should be counted as privacy filtered.");
        }

        public static void ViewerOrderingAndTrimmingAreDeterministic()
        {
            var a = IndividualId.Parse("a1000000000000000000000000000011");
            var b = IndividualId.Parse("a1000000000000000000000000000012");
            var turns = new[]
            {
                Turn("a2000000000000000000000000000011", a, b, 100, new[] { a, b }),
                Turn("a2000000000000000000000000000012", b, a, 300, new[] { a, b }),
                Turn("a2000000000000000000000000000013", a, b, 200, new[] { a, b })
            };

            var first = new ConversationHistoryViewerBuilder().Build(
                a,
                Participants(a, b),
                turns,
                2);
            var second = new ConversationHistoryViewerBuilder().Build(
                a,
                Participants(a, b),
                turns.Reverse(),
                2);

            TestAssert.Equal(2, first.Rows.Count,
                "The explicit viewer row bound must be enforced.");
            TestAssert.Equal(1, first.TrimmedCount,
                "One older row should be reported as trimmed.");
            TestAssert.Equal(
                first.Rows[0].EventId,
                second.Rows[0].EventId,
                "Input rebuild order must not alter the newest visible row.");
            TestAssert.Equal(
                first.Rows[1].EventId,
                second.Rows[1].EventId,
                "Input rebuild order must not alter the second visible row.");
        }

        public static void ViewerDeduplicatesCanonicalDialogueEvents()
        {
            var a = IndividualId.Parse("a1000000000000000000000000000021");
            var b = IndividualId.Parse("a1000000000000000000000000000022");
            var turn = Turn(
                "a2000000000000000000000000000021",
                a,
                b,
                100,
                new[] { a, b });

            var snapshot = new ConversationHistoryViewerBuilder().Build(
                a,
                Participants(a, b),
                new[] { turn, turn },
                100);

            TestAssert.Equal(1, snapshot.Rows.Count,
                "A repeated rebuild input cannot display the same canonical dialogue event twice.");
        }

        public static void ViewerUsesCurrentParticipantLabelsWithoutChangingHistory()
        {
            var a = IndividualId.Parse("a1000000000000000000000000000031");
            var b = IndividualId.Parse("a1000000000000000000000000000032");
            var turn = Turn(
                "a2000000000000000000000000000031",
                a,
                b,
                100,
                new[] { a, b });

            var before = new ConversationHistoryViewerBuilder().Build(
                a,
                new[]
                {
                    new ConversationHistoryParticipant(a, "Old Name"),
                    new ConversationHistoryParticipant(b, "Other")
                },
                new[] { turn });
            var after = new ConversationHistoryViewerBuilder().Build(
                a,
                new[]
                {
                    new ConversationHistoryParticipant(a, "New Name"),
                    new ConversationHistoryParticipant(b, "Other")
                },
                new[] { turn });

            TestAssert.Equal("Old Name", before.Rows[0].SpeakerLabel,
                "The first read model should use the label supplied at read time.");
            TestAssert.Equal("New Name", after.Rows[0].SpeakerLabel,
                "A rename should update the read-only label without rewriting the factual event.");
            TestAssert.Equal(before.Rows[0].EventId, after.Rows[0].EventId,
                "A presentation rename cannot alter canonical dialogue identity.");
        }

        public static void ViewerRejectsUnknownSelectedIndividual()
        {
            var a = IndividualId.Parse("a1000000000000000000000000000041");
            var b = IndividualId.Parse("a1000000000000000000000000000042");
            var unknown = IndividualId.Parse("a1000000000000000000000000000043");

            TestAssert.Throws<ArgumentException>(
                () => new ConversationHistoryViewerBuilder().Build(
                    unknown,
                    Participants(a, b),
                    new[]
                    {
                        Turn(
                            "a2000000000000000000000000000041",
                            a,
                            b,
                            100,
                            new[] { a, b })
                    }),
                "The viewer must not fabricate a history for an unenrolled individual.");
        }

        public static void BuildingViewerDoesNotMutateSourceTurn()
        {
            var a = IndividualId.Parse("a1000000000000000000000000000051");
            var b = IndividualId.Parse("a1000000000000000000000000000052");
            var turn = Turn(
                "a2000000000000000000000000000051",
                a,
                b,
                100,
                new[] { a, b });
            var originalText = turn.Text;
            var originalAudience = turn.AudienceIds.ToArray();

            _ = new ConversationHistoryViewerBuilder().Build(
                a,
                Participants(a, b),
                new[] { turn });

            TestAssert.Equal(originalText, turn.Text,
                "Read-model construction must not alter canonical displayed text.");
            TestAssert.True(originalAudience.SequenceEqual(turn.AudienceIds),
                "Read-model construction must not alter the recorded audience.");
        }

        private static IReadOnlyList<ConversationHistoryParticipant> Participants(
            params IndividualId[] ids) =>
            ids.Select((id, index) =>
                new ConversationHistoryParticipant(id, "Colonist " + (index + 1)))
                .ToArray();

        private static PriorConversationTurn Turn(
            string eventId,
            IndividualId speaker,
            IndividualId recipient,
            long tick,
            IEnumerable<IndividualId> audience)
        {
            var eventGuid = Guid.ParseExact(eventId, "N");
            var conversationBytes = eventGuid.ToByteArray();
            conversationBytes[0] ^= 0x5a;
            return new PriorConversationTurn(
                new EventId(eventGuid),
                new ConversationId(new Guid(conversationBytes)),
                speaker,
                recipient,
                "A verified displayed line.",
                tick,
                new DateTimeOffset(2026, 7, 26, 18, 0, 0, TimeSpan.Zero)
                    .AddSeconds(tick),
                DialoguePresentationChannel.Bubble,
                audience);
        }
    }
}
