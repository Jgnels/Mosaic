using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;

namespace Dagmay.Tests
{
    internal static class ExperienceContractTests
    {
        public static void DeterministicEncodingSeparatesFactPerceptionAndMemory()
        {
            var individual = CreateIndividual("Mira");
            var factualEvent = CreateEvent(
                individual.Id,
                "rimworld.health.condition_added",
                new Dictionary<string, string> { ["condition"] = "Bruise" });

            var result = new DeterministicExperienceEncoder()
                .EncodeExperiencedEvent(factualEvent, individual, DateTimeOffset.UtcNow);

            TestAssert.Equal(factualEvent.Id, result.Perception.SourceEventId, "Perception must retain factual provenance.");
            TestAssert.Equal(result.Perception.Id, result.Memory.SourcePerceptionIds[0], "Memory must retain perception provenance.");
            TestAssert.Equal("Bruise", factualEvent.FactualPayload["condition"], "Subjective encoding must not rewrite the fact.");
            TestAssert.True(result.Memory.ConciseDiaryEntry.Contains("Bruise"), "Memory may interpret an available factual detail.");
            TestAssert.Equal(1L, result.UpdatedState.Version, "Deterministic affect update must advance state exactly once.");
        }

        public static void ExperienceJournalRoundTripPreservesProvenance()
        {
            var directory = Path.Combine(Path.GetTempPath(), "dagmay-experience-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "experience.journal");
            try
            {
                var individual = CreateIndividual("Jo");
                var source = CreateEvent(
                    individual.Id,
                    "rimworld.skill.level_gained",
                    new Dictionary<string, string> { ["skill"] = "Plants", ["level"] = "8" });
                var encoded = new DeterministicExperienceEncoder()
                    .EncodeExperiencedEvent(source, individual, DateTimeOffset.UtcNow);
                var record = new ExperienceJournalRecord(source, encoded.Perception, encoded.Memory);
                var journal = new DurableExperienceJournal();
                var appended = journal.Append(path, record, 0, string.Empty);
                var loaded = journal.Load(path);

                TestAssert.Equal(ExperienceJournalLoadStatus.Loaded, loaded.Status, "Written journal must load.");
                TestAssert.Equal(1, loaded.Records.Count, "One journal record must round-trip.");
                TestAssert.Equal(appended.EntryHash, loaded.LastHash, "Journal head hash must round-trip.");
                TestAssert.Equal(source.Id, loaded.Records[0].FactualEvent.Id, "Event ID must survive journal persistence.");
                TestAssert.Equal(encoded.Perception.Id, loaded.Records[0].Memory!.SourcePerceptionIds[0], "Memory provenance must survive journal persistence.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        public static void ExperienceJournalRejectsTampering()
        {
            var directory = Path.Combine(Path.GetTempPath(), "dagmay-experience-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "experience.journal");
            try
            {
                var individual = CreateIndividual("Ari");
                var source = CreateEvent(
                    individual.Id,
                    "rimworld.need.critical",
                    new Dictionary<string, string> { ["need"] = "Food", ["level"] = "0.1" });
                var encoded = new DeterministicExperienceEncoder()
                    .EncodeExperiencedEvent(source, individual, DateTimeOffset.UtcNow);
                var journal = new DurableExperienceJournal();
                journal.Append(path, new ExperienceJournalRecord(source, encoded.Perception, encoded.Memory), 0, string.Empty);

                var text = File.ReadAllText(path, Encoding.UTF8);
                var index = text.IndexOf('\t') + 4;
                var replacement = text[index] == 'A' ? 'B' : 'A';
                File.WriteAllText(path, text.Substring(0, index) + replacement + text.Substring(index + 1), Encoding.UTF8);

                var loaded = journal.Load(path);
                TestAssert.Equal(ExperienceJournalLoadStatus.Invalid, loaded.Status, "Changed experience history must be rejected.");
                TestAssert.Equal(0, loaded.Records.Count, "Invalid history must not expose a partial canonical record set.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }


        public static void OrdinaryDisclosureExcludesPrivateMemoriesAndDescribesStateCoarsely()
        {
            var individual = CreateIndividual("Vale");
            var encoder = new DeterministicExperienceEncoder();
            var privateExperience = encoder.EncodeExperiencedEvent(
                CreateEvent(individual.Id, "rimworld.health.condition_added", new Dictionary<string, string> { ["condition"] = "Bruise" }),
                individual,
                DateTimeOffset.UtcNow);
            var shareableExperience = encoder.EncodeExperiencedEvent(
                CreateEvent(individual.Id, "rimworld.skill.level_gained", new Dictionary<string, string> { ["skill"] = "Crafting", ["level"] = "5" }),
                privateExperience.UpdatedState,
                DateTimeOffset.UtcNow.AddSeconds(1));

            TestAssert.False(
                Dagmay.Core.Views.OrdinaryDisclosurePolicy.CanShowMemory(privateExperience.Memory),
                "Private health memories must not appear in the ordinary Mind view.");
            TestAssert.True(
                Dagmay.Core.Views.OrdinaryDisclosurePolicy.CanShowMemory(shareableExperience.Memory),
                "Explicitly shareable accessible memories may appear in the ordinary Mind view.");

            var summary = Dagmay.Core.Views.OrdinaryDisclosurePolicy.DescribeCurrentState(
                new Dagmay.Core.Affect.AffectVector(-0.5, 0.2, 0.6, -0.5, 0, -0.4, 0));
            TestAssert.True(summary.Contains("unsafe") || summary.Contains("pressure"), "Ordinary state should expose a coarse concern.");
            TestAssert.False(summary.Contains("0.6"), "Ordinary state must not expose raw diagnostic affect values.");
        }

        public static void SocialExperienceLinksOtherIndividualAndRemainsRelationshipSensitive()
        {
            var owner = CreateIndividual("Mira");
            var other = CreateIndividual("Jo");
            var now = DateTimeOffset.UtcNow;
            var source = new EnvironmentEvent(
                EventId.New(),
                "social-opinion:" + Guid.NewGuid().ToString("N"),
                "rimworld.social.opinion_changed",
                "rimworld",
                now,
                now,
                200,
                "Dagmay.Tests",
                new Dictionary<string, string>
                {
                    ["target_external_id"] = "Human2",
                    ["target_name"] = "Jo",
                    ["opinion_before"] = "5",
                    ["opinion_after"] = "30",
                    ["opinion_delta"] = "25"
                },
                new[] { owner.Id, other.Id });

            var result = new DeterministicExperienceEncoder()
                .EncodeExperiencedEvent(source, owner, now.AddSeconds(1));

            TestAssert.Equal(1, result.Memory.PeopleInvolved.Count, "Social memory must identify the other individual.");
            TestAssert.Equal(other.Id, result.Memory.PeopleInvolved[0], "Social memory must link the enrolled counterpart by IndividualId.");
            TestAssert.Equal(PrivacyClassification.RelationshipSensitive, result.Memory.Privacy, "Social opinion memories are relationship-sensitive by default.");
            TestAssert.True(result.Memory.ConciseDiaryEntry.Contains("Jo"), "Social memory may name the known counterpart.");
        }

        public static void MemoryIndexRetrievesRecentAndSignificantWithoutTranscriptSemantics()
        {
            var individual = CreateIndividual("Niko");
            var encoder = new DeterministicExperienceEncoder();
            var skill = encoder.EncodeExperiencedEvent(
                CreateEvent(individual.Id, "rimworld.skill.level_gained", new Dictionary<string, string> { ["skill"] = "Crafting", ["level"] = "4" }),
                individual,
                DateTimeOffset.UtcNow.AddMinutes(-1));
            var injury = encoder.EncodeExperiencedEvent(
                CreateEvent(individual.Id, "rimworld.health.condition_added", new Dictionary<string, string> { ["condition"] = "Burn" }),
                skill.UpdatedState,
                DateTimeOffset.UtcNow);
            var index = new MemoryIndex();
            index.Add(skill.Memory);
            index.Add(injury.Memory);

            TestAssert.Equal(injury.Memory.Id, index.Recent(individual.Id, 1)[0].Id, "Recent retrieval must use encoding time.");
            TestAssert.Equal(injury.Memory.Id, index.MostSignificant(individual.Id, 1)[0].Id, "Significance retrieval must use bounded importance.");
            TestAssert.Equal(2, index.Count, "Index stores selected memories, not a complete activity transcript.");
        }

        private static IndividualState CreateIndividual(string name)
        {
            return IndividualState.Create(
                name,
                new IdentitySeed(
                    "rimworld",
                    "0.1C",
                    new[] { new SeedFact(SeedFactCategory.Trait, "fixture", "Kind", "test", 1.0) }));
        }

        private static EnvironmentEvent CreateEvent(
            IndividualId individualId,
            string kind,
            IDictionary<string, string> payload)
        {
            var now = DateTimeOffset.UtcNow;
            return new EnvironmentEvent(
                EventId.New(),
                kind + ":" + Guid.NewGuid().ToString("N"),
                kind,
                "rimworld",
                now,
                now,
                100,
                "Dagmay.Tests",
                payload,
                new[] { individualId });
        }
    }
}
