using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Contracts;
using Dagmay.Core.Diagnostics;
using Dagmay.Core.Relationships;

namespace Dagmay.Tests
{
    internal static class Mosaic03CorePromotionContractTests
    {
        private const string HashA =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string HashB =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        public static void CompoundAppraisalSupportsLayeredOwnerCalibratedMeaning()
        {
            var evidence = EventId.New();
            var owner = IndividualId.New();
            var result = new CompoundAppraisalResult(
                evidence,
                owner,
                new[]
                {
                    new RankedAffectMeaning(
                        MosaicEmotionFamily.Resentment,
                        1.0,
                        1,
                        "owner.abandonment.resentment",
                        "The grievance is the dominant durable interpretation."),
                    new RankedAffectMeaning(
                        MosaicEmotionFamily.Betrayal,
                        0.9,
                        2,
                        "owner.abandonment.betrayal",
                        "A trusted duty was violated.")
                },
                5,
                "Betrayed and surprised.",
                "Trust falls while grievance and perceived debt persist.",
                new[] { "M03A-05-PRIMARY", "M03A-05-SECONDARY" });

            TestAssert.Equal(evidence, result.EvidenceId, "Compound appraisal must retain evidence provenance.");
            TestAssert.Equal(owner, result.PerspectiveOwnerId, "Compound appraisal must retain perspective ownership.");
            TestAssert.Equal(MosaicEmotionFamily.Resentment, result.Primary.Family, "Owner-calibrated primary meaning must remain first.");
            TestAssert.Equal(MosaicEmotionFamily.Betrayal, result.Secondary!.Family, "Owner-calibrated secondary meaning must remain explicit.");
            TestAssert.Equal(5, result.StoryIntensity, "Story intensity must retain its bounded owner scale.");
        }

        public static void CompoundAppraisalRejectsDuplicateFamiliesAndRankGaps()
        {
            TestAssert.Throws<ArgumentException>(
                () => new CompoundAppraisalResult(
                    EventId.New(),
                    IndividualId.New(),
                    new[]
                    {
                        new RankedAffectMeaning(MosaicEmotionFamily.Gratitude, 0.5, 1, "r1", "first"),
                        new RankedAffectMeaning(MosaicEmotionFamily.Gratitude, 0.4, 2, "r2", "duplicate")
                    },
                    3,
                    null,
                    null,
                    new[] { "RULE-1" }),
                "One compound appraisal cannot repeat the same family.");

            TestAssert.Throws<ArgumentException>(
                () => new CompoundAppraisalResult(
                    EventId.New(),
                    IndividualId.New(),
                    new[]
                    {
                        new RankedAffectMeaning(MosaicEmotionFamily.Gratitude, 0.5, 2, "r1", "gap")
                    },
                    3,
                    null,
                    null,
                    new[] { "RULE-1" }),
                "Compound appraisal ranks must be contiguous.");
        }

        public static void DirectedSocialMeaningIsAsymmetricAndBounded()
        {
            var a = IndividualId.New();
            var b = IndividualId.New();
            var ab = new DirectedSocialKey(a, b);
            var ba = new DirectedSocialKey(b, a);

            TestAssert.False(ab.Equals(ba), "A-to-B social meaning must remain distinct from B-to-A.");
            var saturated = new SocialMeaningVector(
                0.9, 0.9, 0.9, 0.9, 0.9, 0.9, 0.9)
                .AddBounded(new SocialMeaningVector(
                    0.8, 0.8, 0.8, 0.8, 0.8, 0.8, 0.8));
            TestAssert.Equal(1.0, saturated.Trust, "Social meaning addition must remain bounded.");
            TestAssert.Equal(1.0, saturated.Resentment, "Every social dimension must remain bounded.");
        }

        public static void SocialMeaningProposalCarriesVersionFingerprintAndRules()
        {
            var proposal = new SocialMeaningProposal(
                HashA,
                new DirectedSocialKey(IndividualId.New(), IndividualId.New()),
                EventId.New(),
                600,
                4,
                HashB,
                new SocialMeaningVector(-0.2, -0.1, 0.1, 0.3, -0.1, 0.2, 0.05),
                new[] { "OWNER-ORACLE:M03A-05", "M03-SOCIAL-v1" },
                "Abandonment reduced trust and increased resentment.");

            TestAssert.Equal(4L, proposal.ExpectedVersion, "Proposal must be tied to an expected state version.");
            TestAssert.Equal(HashB, proposal.SourceFingerprint, "Proposal must be tied to a canonical source fingerprint.");
            TestAssert.Equal(2, proposal.StableRuleIds.Count, "Proposal must retain stable rule provenance.");

            TestAssert.Throws<ArgumentException>(
                () => new SocialMeaningProposal(
                    HashA.ToUpperInvariant(),
                    proposal.Key,
                    EventId.New(),
                    1,
                    0,
                    HashB,
                    SocialMeaningVector.Neutral,
                    new[] { "RULE" },
                    "invalid hash"),
                "Proposal hashes must use deterministic lowercase encoding.");
        }

        public static void CharacterWhyProjectionIsDeterministicAcrossInputOrder()
        {
            var owner = IndividualId.New();
            var first = EventId.New();
            var second = EventId.New();
            var builder = new CharacterWhyProjectionBuilder();

            var forward = builder.Build(
                owner,
                "relationship:A:B",
                new[] { first, second },
                new[] { "FACT-2", "FACT-1" },
                new[] { "RULE-2", "RULE-1" },
                "Trust decreased after repeated grounded evidence.",
                HashA);
            var reverse = builder.Build(
                owner,
                "relationship:A:B",
                new[] { second, first },
                new[] { "FACT-1", "FACT-2" },
                new[] { "RULE-1", "RULE-2" },
                "Trust decreased after repeated grounded evidence.",
                HashA);

            TestAssert.Equal(forward.ProjectionId, reverse.ProjectionId, "Why projection ID must not depend on caller enumeration order.");
            TestAssert.True(
                forward.EvidenceIds.SequenceEqual(reverse.EvidenceIds),
                "Why projection evidence ordering must be deterministic.");
            TestAssert.True(
                forward.FactIds.SequenceEqual(new[] { "FACT-1", "FACT-2" }),
                "Why projection fact IDs must use stable ordinal ordering.");
        }

        public static void CharacterWhyProjectionCopiesCollectionsAndRejectsDuplicates()
        {
            var evidence = new List<EventId> { EventId.New() };
            var facts = new List<string> { "FACT-1" };
            var rules = new List<string> { "RULE-1" };
            var projection = new CharacterWhyProjectionBuilder().Build(
                IndividualId.New(),
                "relationship:A:B",
                evidence,
                facts,
                rules,
                "Grounded explanation.",
                HashA);

            evidence.Clear();
            facts.Clear();
            rules.Clear();

            TestAssert.Equal(1, projection.EvidenceIds.Count, "Why projection must copy evidence collections.");
            TestAssert.Equal(1, projection.FactIds.Count, "Why projection must copy fact collections.");
            TestAssert.Equal(1, projection.RuleIds.Count, "Why projection must copy rule collections.");

            var duplicate = EventId.New();
            TestAssert.Throws<ArgumentException>(
                () => new CharacterWhyProjectionBuilder().Build(
                    IndividualId.New(),
                    "relationship:A:B",
                    new[] { duplicate, duplicate },
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    "Duplicate evidence.",
                    HashA),
                "Why projection must reject duplicate evidence IDs.");
        }
    }
}
