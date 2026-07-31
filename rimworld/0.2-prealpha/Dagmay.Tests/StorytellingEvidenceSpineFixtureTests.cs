using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;
using Dagmay.Core.Reflection;
using Dagmay.Core.Relationships;
using Dagmay.Providers.Fake;

namespace Dagmay.Tests
{
    internal static class StorytellingEvidenceSpineFixtureTests
    {
        public static void SocialKindsFlowToGroundedDialogueAndAppraisalInputs()
        {
            var fixture = Fixture();
            var records = new[]
            {
                Record(fixture.A, fixture.B, "rimworld.social.kindness", 10, 0.65, PerceptionChannel.Experienced),
                Record(fixture.A, fixture.B, "rimworld.social.insult", 20, -0.50, PerceptionChannel.Witnessed),
                Record(fixture.A, fixture.B, "rimworld.social.betrayal", 30, -0.90, PerceptionChannel.Experienced),
                Record(fixture.A, fixture.B, "rimworld.social.rescue", 40, 1.00, PerceptionChannel.Experienced),
                Record(fixture.A, fixture.B, "rimworld.social.rumor", 50, -0.70, PerceptionChannel.Told)
            };
            var ledger = new InMemoryEventLedger();
            foreach (var record in records)
            {
                TestAssert.Equal(EventAppendStatus.Appended, ledger.Append(record.FactualEvent).Status,
                    "Each fixture fact must enter canonical evidence exactly once.");
            }

            var projector = new RelationshipEvidenceProjector();
            var evidence = projector.FromJournal(fixture.A, fixture.B, records);
            TestAssert.Equal(5, evidence.Count,
                "Kindness, insult, betrayal, rescue, and rumor must retain exact episodic provenance.");
            TestAssert.True(evidence.Any(value => !value.IsHearsay) &&
                            evidence.Any(value => value.IsHearsay),
                "The spine must distinguish direct/witnessed evidence from hearsay.");
            var projection = projector.Build(
                fixture.A, fixture.B, "Thing_B", "B", evidence, includePrivate: false);
            var contextItem = projector.CreateDialogueContext(projection, fixture.B);
            var context = new DialogueContextAssembler().Build(
                fixture.A,
                fixture.B,
                60,
                new[] { contextItem },
                new DialogueContextBudget(4, 2048));
            TestAssert.Equal(1, context.Items.Count,
                "The owner-to-recipient relationship packet should pass its privacy boundary.");
            TestAssert.True(
                context.Items[0].EvidenceIds.SequenceEqual(projection.Relationship.EvidenceEventIds),
                "Dialogue retrieval must retain the projection's exact EventIds.");

            var appraisals = projector.CreateAppraisalInputs(projection);
            TestAssert.True(
                appraisals.Select(value => value.EvidenceId).SequenceEqual(
                    projection.Evidence.Where(value => !value.Superseded).Select(value => value.EventId)),
                "Appraisal inputs must cite the same unsuperseded EventIds.");
            var request = new ModelRequest(
                new RequestId(Guid.Parse("70000000-0000-0000-0000-000000000001")),
                fixture.A,
                new LineageId(Guid.Parse("71000000-0000-0000-0000-000000000001")),
                0,
                ModelTaskKind.GenerateDialogueUtterance,
                "story-spine-fixture-v1",
                "Use only supplied evidence.",
                context.Items[0].Text,
                "{\"type\":\"object\"}",
                context.Items[0].EvidenceIds,
                AffectVector.Neutral,
                fixture.Utc.AddMinutes(1),
                256);
            var fake = new DeterministicFakeProvider(
                FakeProviderBehavior.Success,
                "{\"fixture\":\"grounded\"}");
            var response = fake.GenerateStructuredAsync(request, CancellationToken.None)
                .GetAwaiter().GetResult();
            TestAssert.Equal(ModelResultStatus.Success, response.Status,
                "The bounded exact-evidence packet must be accepted by the deterministic fake provider.");
            TestAssert.True(request.EvidenceEventIds.SequenceEqual(context.Items[0].EvidenceIds),
                "The fake-provider request must carry no evidence outside the retrieval packet.");
        }

        public static void DirectedHistoriesRemainAsymmetricAndMixed()
        {
            var fixture = Fixture();
            var records = new[]
            {
                Record(fixture.A, fixture.B, "rimworld.social.kindness", 10, 0.8, PerceptionChannel.Experienced),
                Record(fixture.A, fixture.B, "rimworld.social.insult", 20, -0.2, PerceptionChannel.Witnessed),
                Record(fixture.B, fixture.A, "rimworld.social.betrayal", 30, -0.9, PerceptionChannel.Experienced)
            };
            var projector = new RelationshipEvidenceProjector();
            var aTowardB = projector.Build(
                fixture.A,
                fixture.B,
                "Thing_B",
                "B",
                projector.FromJournal(fixture.A, fixture.B, records),
                false);
            var bTowardA = projector.Build(
                fixture.B,
                fixture.A,
                "Thing_A",
                "A",
                projector.FromJournal(fixture.B, fixture.A, records),
                false);
            TestAssert.True(aTowardB.Relationship.Dimensions.Trust > 0,
                "A's mixed but mostly positive evidence should remain positive toward B.");
            TestAssert.True(bTowardA.Relationship.Dimensions.Trust < 0,
                "B's betrayal evidence should remain negative toward A.");
            TestAssert.False(
                aTowardB.Relationship.EvidenceEventIds.Intersect(
                    bTowardA.Relationship.EvidenceEventIds).Any(),
                "Opposite directed relationships must not merge their evidence.");
        }

        public static void SupersessionAndPrivacyBoundRetrievalWithoutErasure()
        {
            var fixture = Fixture();
            var oldBetrayal = Record(
                fixture.A,
                fixture.B,
                "rimworld.social.betrayal",
                10,
                -1,
                PerceptionChannel.Told,
                superseded: true);
            var correction = Record(
                fixture.A,
                fixture.B,
                "rimworld.social.correction",
                20,
                0.4,
                PerceptionChannel.Witnessed);
            var privateRescue = Record(
                fixture.A,
                fixture.B,
                "rimworld.social.rescue",
                30,
                1,
                PerceptionChannel.Experienced,
                privacy: PrivacyClassification.Private);
            var projector = new RelationshipEvidenceProjector();
            var evidence = projector.FromJournal(
                fixture.A,
                fixture.B,
                new[] { oldBetrayal, correction, privateRescue });
            var shareable = projector.Build(
                fixture.A, fixture.B, "Thing_B", "B", evidence, includePrivate: false);
            TestAssert.True(shareable.Relationship.Dimensions.Trust > 0,
                "A superseded betrayal must not override its later positive correction.");
            TestAssert.True(shareable.Relationship.EvidenceEventIds.Contains(oldBetrayal.FactualEvent.Id),
                "Superseded evidence must remain preserved in the bounded evidence record.");
            TestAssert.False(shareable.Relationship.EvidenceEventIds.Contains(privateRescue.FactualEvent.Id),
                "Private evidence must not influence a packet disclosed to the counterpart.");

            var item = projector.CreateDialogueContext(shareable, fixture.B);
            var outsider = new DialogueContextAssembler().Build(
                fixture.C,
                fixture.B,
                40,
                new[] { item },
                new DialogueContextBudget(4, 2048));
            TestAssert.Equal(0, outsider.Items.Count,
                "An unwitnessed outsider must not receive another individual's relationship packet.");
        }

        public static void JournalReloadRebuildIsDeterministic()
        {
            var fixture = Fixture();
            var records = new[]
            {
                Record(fixture.A, fixture.B, "rimworld.social.rescue", 30, 0.9, PerceptionChannel.Experienced),
                Record(fixture.A, fixture.B, "rimworld.social.rumor", 20, -0.4, PerceptionChannel.Told),
                Record(fixture.A, fixture.B, "rimworld.social.kindness", 10, 0.5, PerceptionChannel.Witnessed)
            };
            TestAssert.Throws<InvalidDataException>(
                () => new RelationshipEvidenceProjector().FromJournal(
                    fixture.A,
                    fixture.B,
                    records.Concat(new[] { records[0] })),
                "A duplicated factual EventId must fail the relationship rebuild closed.");
            var directory = Path.Combine(
                Path.GetTempPath(),
                "mosaic-story-spine-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "story.journal");
                var journal = new DurableExperienceJournal();
                long position = 0;
                var hash = string.Empty;
                foreach (var record in records)
                {
                    var appended = journal.Append(path, record, position, hash);
                    position = appended.Position;
                    hash = appended.EntryHash;
                }
                var loaded = journal.Load(path);
                TestAssert.Equal(ExperienceJournalLoadStatus.Loaded, loaded.Status,
                    "The storytelling fixture journal must reload with verified integrity.");

                var projector = new RelationshipEvidenceProjector();
                var first = projector.Build(
                    fixture.A,
                    fixture.B,
                    "Thing_B",
                    "B",
                    projector.FromJournal(fixture.A, fixture.B, loaded.Records),
                    false);
                var reordered = projector.Build(
                    fixture.A,
                    fixture.B,
                    "Thing_B",
                    "B",
                    projector.FromJournal(fixture.A, fixture.B, loaded.Records.Reverse()),
                    false);
                TestAssert.Equal(Fingerprint(first), Fingerprint(reordered),
                    "Save/reload and rebuild order must produce an identical bounded projection.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static ExperienceJournalRecord Record(
            IndividualId owner,
            IndividualId other,
            string kind,
            long tick,
            double valence,
            PerceptionChannel channel,
            bool superseded = false,
            PrivacyClassification privacy = PrivacyClassification.RelationshipSensitive)
        {
            var utc = new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero).AddTicks(tick);
            var eventId = EventId.New();
            var factual = new EnvironmentEvent(
                eventId,
                "story-spine:" + eventId,
                kind,
                "rimworld",
                utc,
                utc,
                tick,
                "Mosaic offline fixture",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [RelationshipEvidenceProjector.ValenceKey] =
                        valence.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    [RelationshipEvidenceProjector.SupersededKey] =
                        superseded ? "true" : "false"
                },
                new[] { owner, other });
            var perception = new PerceivedEvent(
                PerceptionId.New(),
                eventId,
                owner,
                channel,
                channel == PerceptionChannel.Told ? 0.55 : 0.9,
                new Dictionary<string, string> { ["kind"] = kind },
                Array.Empty<string>(),
                tick);
            var memory = new SubjectiveMemory(
                MemoryId.New(),
                owner,
                new[] { perception.Id },
                utc,
                utc.AddSeconds(1),
                "A bounded episodic account.",
                "A relationship-relevant event was encoded.",
                AffectVector.Neutral,
                0.7,
                Math.Abs(valence),
                perception.Confidence,
                0.8,
                MemoryTier.Recent,
                privacy,
                new[] { other });
            return new ExperienceJournalRecord(factual, perception, memory);
        }

        private static string Fingerprint(RelationshipEvidenceProjectionResult value) =>
            string.Join(
                "|",
                value.Relationship.OwnerId,
                value.Relationship.Other.IndividualId,
                value.Relationship.Dimensions.Trust.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                value.Relationship.Dimensions.Affection.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                value.Relationship.Dimensions.Resentment.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                string.Join(",", value.Relationship.EvidenceEventIds));

        private static (IndividualId A, IndividualId B, IndividualId C, DateTimeOffset Utc) Fixture() =>
            (
                IndividualId.Parse("80000000000000000000000000000001"),
                IndividualId.Parse("80000000000000000000000000000002"),
                IndividualId.Parse("80000000000000000000000000000003"),
                new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));
    }
}
