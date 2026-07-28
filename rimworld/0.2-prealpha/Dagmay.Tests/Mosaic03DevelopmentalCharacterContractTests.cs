using System;
using System.Linq;
using Dagmay.Core.Contracts;
using Dagmay.Core.Development;

namespace Dagmay.Tests
{
    internal static class Mosaic03DevelopmentalCharacterContractTests
    {
        private const string HashA =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string HashB =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        public static void BaselineTraitsAndDispositionsRemainSeparateAndBounded()
        {
            var trait = new BaselineTraitValue(BaselineTraitDimension.Empathy, 0.8);
            var disposition = new LearnedDispositionValue(
                LearnedDispositionDimension.Mercy,
                -0.2);

            TestAssert.Equal(BaselineTraitDimension.Empathy, trait.Dimension, "Baseline trait dimension must remain explicit.");
            TestAssert.Equal(LearnedDispositionDimension.Mercy, disposition.Dimension, "Learned disposition dimension must remain explicit.");
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new LearnedDispositionValue(
                    LearnedDispositionDimension.Courage,
                    1.01),
                "Development dimensions must reject values outside the signed unit interval.");
        }

        public static void ExposureSeparatesAdaptationSensitizationAndRecovery()
        {
            var exposure = new ExposureDomainState(
                "violence",
                0.7,
                0.4,
                0.2,
                0.3,
                0.5);

            TestAssert.Equal(0.4, exposure.Adaptation, "Adaptation must remain separate.");
            TestAssert.Equal(0.2, exposure.Sensitization, "Sensitization must remain separate.");
            TestAssert.Equal(0.3, exposure.UnresolvedLoad, "Unresolved load must remain separate.");
            TestAssert.Equal(0.5, exposure.Integration, "Integration must remain separate.");
        }

        public static void GroundedWantRequiresOwnerEvidenceAndHasNoActionAuthority()
        {
            var owner = IndividualId.New();
            var evidence = EventId.New();
            var want = new GroundedWantCandidate(
                "KEEP_IMPORTANT_PROMISE",
                owner,
                "commitment",
                "pawn:partner",
                100,
                1000,
                0.8,
                new[] { evidence },
                GroundedWantStatus.Active);

            TestAssert.Equal(owner, want.OwnerId, "Want ownership must remain explicit.");
            TestAssert.Equal(evidence, want.SourceEvidenceIds[0], "Want must retain source evidence.");
            TestAssert.False(want.DirectActionAuthority, "Want cannot issue a pawn action.");

            TestAssert.Throws<ArgumentException>(
                () => new GroundedWantCandidate(
                    "UNGROUNDED",
                    owner,
                    "test",
                    null,
                    100,
                    1000,
                    0,
                    Array.Empty<EventId>(),
                    GroundedWantStatus.Active),
                "Ungrounded wants must be rejected.");
        }


public static void GroundedWantOptionalTargetNormalizesWhitespaceAndPreservesText()
{
    var owner = IndividualId.New();
    var evidence = EventId.New();
    var untargeted = new GroundedWantCandidate(
        "UNTARGETED",
        owner,
        "recovery",
        "   ",
        10,
        100,
        0.2,
        new[] { evidence },
        GroundedWantStatus.Active);
    var targeted = new GroundedWantCandidate(
        "TARGETED",
        owner,
        "relationship",
        "pawn:partner",
        10,
        100,
        0.4,
        new[] { evidence },
        GroundedWantStatus.Active);

    TestAssert.True(
        untargeted.TargetKey is null,
        "Whitespace optional want targets must normalize to null.");
    TestAssert.Equal(
        "pawn:partner",
        targeted.TargetKey!,
        "Nonblank optional want targets must survive validation.");
}

        public static void KnowledgeClaimPreservesWitnessChainPrivacyAndSubjectivity()
        {
            var witness = IndividualId.New();
            var listener = IndividualId.New();
            var source = EventId.New();
            var claim = new PerspectiveKnowledgeClaim(
                HashA,
                listener,
                "pawn:mira",
                "rescued",
                "pawn:jo",
                source,
                witness,
                new[] { witness, listener },
                0.72,
                KnowledgeChannel.Report,
                KnowledgePrivacyDomain.RelationshipPrivate,
                KnowledgeClaimStatus.Current,
                500,
                distortionTags: new[] { "UNVERIFIED" });

            TestAssert.Equal(source, claim.SourceEventId, "Claim must retain original event provenance.");
            TestAssert.Equal(witness, claim.OriginalWitnessId, "Claim must retain original witness.");
            TestAssert.Equal(listener, claim.SpeakerChain.Last(), "Speaker chain must end at observer.");
            TestAssert.False(claim.IsCanonicalWorldFact, "An observer claim is not canonical world fact.");

            TestAssert.Throws<ArgumentException>(
                () => new PerspectiveKnowledgeClaim(
                    HashB,
                    listener,
                    "pawn:mira",
                    "rescued",
                    "pawn:jo",
                    source,
                    witness,
                    new[] { witness, listener, witness },
                    0.5,
                    KnowledgeChannel.Rumor,
                    KnowledgePrivacyDomain.Public,
                    KnowledgeClaimStatus.Current,
                    600),
                "Speaker-chain loops must be rejected.");
        }


public static void KnowledgeClaimOptionalSupersessionNormalizesWhitespaceAndPreservesHash()
{
    var witness = IndividualId.New();
    var listener = IndividualId.New();
    var source = EventId.New();
    var unsuperseded = new PerspectiveKnowledgeClaim(
        HashA,
        listener,
        "pawn:mira",
        "rescued",
        "pawn:jo",
        source,
        witness,
        new[] { witness, listener },
        0.8,
        KnowledgeChannel.Report,
        KnowledgePrivacyDomain.RelationshipPrivate,
        KnowledgeClaimStatus.Current,
        700,
        supersedesClaimId: "   ");
    var superseding = new PerspectiveKnowledgeClaim(
        HashB,
        listener,
        "pawn:mira",
        "rescued",
        "pawn:jo",
        source,
        witness,
        new[] { witness, listener },
        0.9,
        KnowledgeChannel.Correction,
        KnowledgePrivacyDomain.RelationshipPrivate,
        KnowledgeClaimStatus.Current,
        800,
        supersedesClaimId: HashA);

    TestAssert.True(
        unsuperseded.SupersedesClaimId is null,
        "Whitespace optional supersession IDs must normalize to null.");
    TestAssert.Equal(
        HashA,
        superseding.SupersedesClaimId!,
        "A supplied supersession hash must survive validation.");
}

        public static void MilestoneIsProjectionNotTraitMutation()
        {
            var owner = IndividualId.New();
            var support = EventId.New();
            var counter = EventId.New();
            var milestone = new DevelopmentMilestoneProjection(
                HashA,
                owner,
                "MERCIFUL",
                "Merciful",
                DevelopmentMilestoneStatus.Active,
                0.75,
                new[] { support },
                new[] { counter },
                LearnedDispositionDimension.Mercy,
                0.5);

            TestAssert.Equal(DevelopmentMilestoneStatus.Active, milestone.Status, "Milestone status must remain explicit.");
            TestAssert.False(milestone.DirectTraitMutation, "Milestone cannot rewrite baseline traits.");
            TestAssert.False(milestone.DirectActionAuthority, "Milestone cannot issue pawn actions.");
        }

        public static void BetrayalPressureIsACompleteDistributionWithoutAuthority()
        {
            var result = new BetrayalPressureResult(
                0.45,
                0.25,
                0.20,
                0.10,
                new[] { EventId.New() },
                "Loyalty and current relationships outweighed grievance.");

            TestAssert.Equal(0.10, result.Betray, "Betrayal probability must remain inspectable.");
            TestAssert.False(result.DirectActionAuthority, "Betrayal pressure cannot execute betrayal.");

            TestAssert.Throws<ArgumentException>(
                () => new BetrayalPressureResult(
                    0.5,
                    0.5,
                    0.5,
                    0.5,
                    new[] { EventId.New() },
                    "Invalid distribution."),
                "Betrayal probabilities must sum to one.");
        }

        public static void EvidenceRevisionReferenceIsConstantSizeAndVersioned()
        {
            var reference = new EvidenceRevisionReference(
                "development:pawn:mira",
                100,
                25000,
                HashA,
                EventId.New());

            TestAssert.Equal(100L, reference.LedgerRevision, "Evidence reference must retain ledger revision.");
            TestAssert.Equal(25000L, reference.EvidenceCount, "Evidence reference must retain count without copying every ID.");
            TestAssert.Equal(HashA, reference.RollingDigest, "Evidence reference must retain rolling digest.");
        }
    }
}
