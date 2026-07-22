using System;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Beliefs;
using Dagmay.Core.Contracts;
using Dagmay.Core.Diagnostics;

namespace Dagmay.Tests
{
    internal static class StorytellingFoundationContractTests
    {
        public static void TemporalFactPreservesEvidenceAndSupersedesWithoutErasure()
        {
            var owner = IndividualId.New();
            var source = EventId.New();
            var first = new TemporalFact(
                FactId.New(),
                owner,
                "pawn:michael",
                "relationship.trust",
                "low",
                100,
                120,
                0.9,
                EvidenceDomain.Personal,
                new[] { source });

            var replacement = FactId.New();
            first.Supersede(200, replacement);

            TestAssert.False(first.IsCurrent, "Superseded facts must stop being current.");
            TestAssert.Equal(source, first.SourceEvidenceIds[0], "Supersession must preserve source evidence provenance.");
            TestAssert.Equal(replacement, first.SupersededByFactId!.Value, "Supersession must identify the replacement fact.");
            TestAssert.Equal(200L, first.ValidUntilTick!.Value, "Supersession must preserve the validity boundary.");
        }

        public static void AppraisalContractsAreBoundedAndSideEffectFreeByConstruction()
        {
            var input = new AppraisalInput(
                EventId.New(),
                IndividualId.New(),
                "pawn:michael",
                "pawn:lynx",
                "rimworld.social.insult",
                -0.8,
                -0.7,
                -0.2,
                0.8,
                0.4);

            var candidate = new AffectCandidate("anger", 0.6, "A harmful, blameworthy social event.");
            var result = new AppraisalResult(new[] { candidate }, -0.1);

            TestAssert.Equal("rimworld.social.insult", input.EventKind, "Appraisal input must preserve grounded event kind.");
            TestAssert.Equal("anger", result.Candidates[0].Kind, "Appraisal result must preserve bounded affect candidate.");
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new AppraisalResult(new[] { candidate }, 2.0),
                "Mood delta outside the bounded range must be rejected.");
        }

        public static void DecisionTraceCarriesEvidenceWithoutOwningCanonicalState()
        {
            var evidence = EventId.New();
            var trace = new DecisionTraceEvent(
                123,
                IndividualId.New(),
                DecisionTraceStage.RelationshipUpdate,
                "relationship:update:michael",
                "Trust decreased after direct negative social evidence.",
                new[] { evidence },
                "trust=-0.20",
                "trust=-0.35",
                -0.15);

            TestAssert.Equal(evidence, trace.EvidenceIds[0], "Decision traces must retain evidence provenance.");
            TestAssert.Equal(DecisionTraceStage.RelationshipUpdate, trace.Stage, "Decision trace stage must remain explicit.");
        }
    }
}
