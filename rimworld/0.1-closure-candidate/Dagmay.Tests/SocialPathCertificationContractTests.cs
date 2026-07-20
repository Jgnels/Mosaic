using System;
using System.Collections.Generic;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;
using Dagmay.RimWorld.Diagnostics;

namespace Dagmay.Tests
{
    internal static class SocialPathCertificationContractTests
    {
        public static void CertificationRequiresExactCounterpartProvenanceAndOneOwnerMemory()
        {
            var owner = CreateIndividual("Owner");
            var counterpart = CreateIndividual("Counterpart");
            var unrelated = CreateIndividual("Unrelated");
            var value = CreateSocialEvent(
                owner.Id,
                counterpart.Id,
                "rimworld.relationship.direct_changed",
                "counterpart-external",
                100,
                "direct:owner-counterpart");
            var encoded = new DeterministicExperienceEncoder()
                .EncodeExperiencedEvent(value, owner, DateTimeOffset.UtcNow);
            var ledger = new InMemoryEventLedger();
            ledger.Append(value);
            var sources = new Dictionary<PerceptionId, EventId>
            {
                [encoded.Perception.Id] = value.Id
            };

            var wrongCounterpart = ReplaceMemoryPeople(encoded.Memory, new[] { unrelated.Id });
            var wrongSummary = SocialPathCertificationReport.Build(
                ledger.Snapshot(),
                new[] { wrongCounterpart },
                sources,
                postLoadAudit: true,
                identityStorageHealthy: true,
                experienceStorageHealthy: true,
                reflectionStorageHealthy: true);

            TestAssert.True(wrongSummary.StableCounterpartLinkComplete, "The factual event has one exact stable counterpart subject.");
            TestAssert.False(wrongSummary.CounterpartProvenanceComplete, "An unrelated non-owner IndividualId cannot satisfy counterpart provenance.");
            TestAssert.False(wrongSummary.Passed, "Mismatched counterpart provenance cannot certify the social path.");

            var duplicate = ReplaceMemoryId(encoded.Memory, MemoryId.New());
            var duplicateSummary = SocialPathCertificationReport.Build(
                ledger.Snapshot(),
                new[] { encoded.Memory, duplicate },
                sources,
                postLoadAudit: true,
                identityStorageHealthy: true,
                experienceStorageHealthy: true,
                reflectionStorageHealthy: true);

            TestAssert.False(duplicateSummary.EventToMemoryComplete, "Duplicate owner memories linked to one social event cannot certify one-to-one encoding.");
            TestAssert.False(duplicateSummary.Passed, "Duplicate social-memory links cannot produce an overall PASS.");
        }

        public static void CertificationRequiresOwnerEvidencePostLoadAndHealthyStores()
        {
            var owner = CreateIndividual("Owner");
            var counterpart = CreateIndividual("Counterpart");
            var otherOwner = CreateIndividual("Other owner");
            var direct = CreateSocialEvent(
                owner.Id,
                counterpart.Id,
                "rimworld.relationship.direct_changed",
                "counterpart-external",
                100,
                "direct:valid");
            var encoded = new DeterministicExperienceEncoder()
                .EncodeExperiencedEvent(direct, owner, DateTimeOffset.UtcNow);
            var directLedger = new InMemoryEventLedger();
            directLedger.Append(direct);
            var directSources = new Dictionary<PerceptionId, EventId>
            {
                [encoded.Perception.Id] = direct.Id
            };

            var healthy = SocialPathCertificationReport.Build(
                directLedger.Snapshot(),
                new[] { encoded.Memory },
                directSources,
                postLoadAudit: true,
                identityStorageHealthy: true,
                experienceStorageHealthy: true,
                reflectionStorageHealthy: true);
            TestAssert.True(healthy.Passed, "A valid post-load direct-relationship path with healthy stores certifies.");

            var readOnly = SocialPathCertificationReport.Build(
                directLedger.Snapshot(),
                new[] { encoded.Memory },
                directSources,
                postLoadAudit: true,
                identityStorageHealthy: true,
                experienceStorageHealthy: false,
                reflectionStorageHealthy: true);
            TestAssert.False(readOnly.StorageHealthy, "A read-only experience store fails the storage-health gate.");
            TestAssert.False(readOnly.Passed, "A read-only store cannot produce Gate.Overall=True.");

            var opinion = CreateSocialEvent(
                owner.Id,
                counterpart.Id,
                "rimworld.social.opinion_changed",
                "counterpart-external",
                500,
                "opinion:owner");
            var secondarySubjectOnly = CreateSocialEvent(
                otherOwner.Id,
                owner.Id,
                "rimworld.social.opinion_changed",
                "counterpart-external",
                500,
                "opinion:other-owner");
            var opinionLedger = new InMemoryEventLedger();
            opinionLedger.Append(opinion);
            opinionLedger.Append(secondarySubjectOnly);
            var opinionSummary = SocialPathCertificationReport.Build(
                opinionLedger.Snapshot(),
                Array.Empty<SubjectiveMemory>(),
                new Dictionary<PerceptionId, EventId>(),
                postLoadAudit: true,
                identityStorageHealthy: true,
                experienceStorageHealthy: true,
                reflectionStorageHealthy: true);

            TestAssert.Equal(0, opinionSummary.ReflectionEligibleEvents, "An event where the individual is only a secondary subject cannot satisfy that individual's repeated-opinion evidence threshold.");
        }

        private static IndividualState CreateIndividual(string name)
        {
            return IndividualState.Create(
                name,
                new IdentitySeed(
                    "rimworld",
                    "social-certification-test",
                    new[] { new SeedFact(SeedFactCategory.Trait, "fixture", "Kind", "test", 1.0) }));
        }

        private static EnvironmentEvent CreateSocialEvent(
            IndividualId ownerId,
            IndividualId counterpartId,
            string kind,
            string targetExternalId,
            long gameTick,
            string deduplicationKey)
        {
            var now = DateTimeOffset.UtcNow;
            return new EnvironmentEvent(
                EventId.New(),
                deduplicationKey,
                kind,
                "rimworld",
                now,
                now,
                gameTick,
                "Dagmay.Tests",
                new Dictionary<string, string>
                {
                    ["target_external_id"] = targetExternalId,
                    ["target_name"] = "Counterpart",
                    ["opinion_before"] = "0",
                    ["opinion_after"] = "20",
                    ["opinion_delta"] = "20"
                },
                new[] { ownerId, counterpartId });
        }

        private static SubjectiveMemory ReplaceMemoryPeople(
            SubjectiveMemory value,
            IEnumerable<IndividualId> people)
        {
            return new SubjectiveMemory(
                value.Id,
                value.OwnerId,
                value.SourcePerceptionIds,
                value.OccurredAtUtc,
                value.EncodedAtUtc,
                value.ConciseDiaryEntry,
                value.Appraisal,
                value.AffectAtEncoding,
                value.Importance,
                value.EmotionalWeight,
                value.Confidence,
                value.Accessibility,
                value.Tier,
                value.Privacy,
                people);
        }

        private static SubjectiveMemory ReplaceMemoryId(SubjectiveMemory value, MemoryId id)
        {
            return new SubjectiveMemory(
                id,
                value.OwnerId,
                value.SourcePerceptionIds,
                value.OccurredAtUtc,
                value.EncodedAtUtc,
                value.ConciseDiaryEntry,
                value.Appraisal,
                value.AffectAtEncoding,
                value.Importance,
                value.EmotionalWeight,
                value.Confidence,
                value.Accessibility,
                value.Tier,
                value.Privacy,
                value.PeopleInvolved);
        }
    }
}