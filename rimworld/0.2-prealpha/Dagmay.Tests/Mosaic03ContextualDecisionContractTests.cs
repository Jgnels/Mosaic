using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Contracts;
using Dagmay.Core.Decisions;

namespace Dagmay.Tests
{
    internal static class Mosaic03ContextualDecisionContractTests
    {
        private const string HashA =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string HashB =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        public static void ContextualReactionPreservesEvidenceAndHasNoAuthority()
        {
            var evidence = EventId.New();
            var owner = IndividualId.New();
            var decision = new ContextualReactionDecision(
                HashA,
                evidence,
                owner,
                "CONTEXTUAL_SEVERITY_HYBRID",
                ReactionPresentationChannel.Deferred,
                null,
                true,
                true,
                5,
                new[] { "REACTION:DEFER_FOR_SAFETY", "REACTION:SEVERITY_DEFAULT" },
                new[] { "danger deferred presentation", "history preserved" });

            TestAssert.Equal(evidence, decision.EvidenceId, "Reaction must retain exact evidence.");
            TestAssert.Equal(owner, decision.PerspectiveOwnerId, "Reaction must retain perspective ownership.");
            TestAssert.True(decision.CreateHistoryEntry, "Safety deferral must not erase history.");
            TestAssert.False(decision.ChangesAppraisalMeaning, "Presentation cannot rewrite appraisal meaning.");
            TestAssert.False(decision.DirectActionAuthority, "Presentation cannot issue pawn actions.");
        }

        public static void ContextualReactionRejectsInvalidDeferredAndTextChannels()
        {
            TestAssert.Throws<ArgumentException>(
                () => new ContextualReactionDecision(
                    HashA,
                    EventId.New(),
                    IndividualId.New(),
                    "POLICY",
                    ReactionPresentationChannel.Deferred,
                    null,
                    true,
                    false,
                    4,
                    new[] { "RULE" },
                    new[] { "why" }),
                "Deferred channel requires deferUntilSafe.");

            TestAssert.Throws<ArgumentException>(
                () => new ContextualReactionDecision(
                    HashA,
                    EventId.New(),
                    IndividualId.New(),
                    "POLICY",
                    ReactionPresentationChannel.SpeechFragment,
                    null,
                    true,
                    false,
                    4,
                    new[] { "RULE" },
                    new[] { "why" }),
                "Text-bearing reaction channels require a text seed.");
        }

        public static void RetrievalIsPerspectiveConsistentAuthorizedAndReadOnly()
        {
            var owner = IndividualId.New();
            var counterpart = IndividualId.New();
            var anchor = new NarrativeEvidenceReference(
                EventId.New(),
                owner,
                counterpart,
                NarrativeEvidenceCategory.DurableAnchor,
                100,
                0.7,
                1,
                0.8,
                "Became committed partners.",
                true,
                true);
            var lived = new NarrativeEvidenceReference(
                EventId.New(),
                owner,
                counterpart,
                NarrativeEvidenceCategory.LivedExperience,
                900,
                -0.9,
                1,
                1,
                "Was abandoned during danger.",
                true,
                true);
            var decision = new ContextualRetrievalDecision(
                HashA,
                ContextualRetrievalStrategy.DefaultHybrid,
                new[] { anchor, lived },
                new[] { "RETRIEVAL:DEFAULT_HYBRID" },
                new[] { "anchor plus lived experience" });

            TestAssert.Equal(2, decision.Selected.Count, "Hybrid retrieval must retain both evidence classes.");
            TestAssert.False(decision.DirectMemoryMutation, "Retrieval cannot alter canonical memory.");
            TestAssert.False(decision.DirectActionAuthority, "Retrieval cannot issue pawn actions.");
        }

        public static void RetrievalRejectsDuplicateForeignAndUnauthorizedEvidence()
        {
            var owner = IndividualId.New();
            var counterpart = IndividualId.New();
            var evidenceId = EventId.New();
            var evidence = new NarrativeEvidenceReference(
                evidenceId,
                owner,
                counterpart,
                NarrativeEvidenceCategory.LivedExperience,
                100,
                0,
                1,
                1,
                "Valid.",
                true,
                true);

            TestAssert.Throws<ArgumentException>(
                () => new ContextualRetrievalDecision(
                    HashA,
                    ContextualRetrievalStrategy.BestAvailable,
                    new[] { evidence, evidence },
                    new[] { "RULE" },
                    new[] { "why" }),
                "Retrieval must reject duplicate evidence IDs.");

            var unauthorized = new NarrativeEvidenceReference(
                EventId.New(),
                owner,
                counterpart,
                NarrativeEvidenceCategory.LivedExperience,
                200,
                0,
                1,
                1,
                "Private.",
                true,
                false);
            TestAssert.Throws<ArgumentException>(
                () => new ContextualRetrievalDecision(
                    HashA,
                    ContextualRetrievalStrategy.BestAvailable,
                    new[] { unauthorized },
                    new[] { "RULE" },
                    new[] { "why" }),
                "Retrieval must reject unauthorized evidence.");

            var foreign = new NarrativeEvidenceReference(
                EventId.New(),
                IndividualId.New(),
                counterpart,
                NarrativeEvidenceCategory.LivedExperience,
                300,
                0,
                1,
                1,
                "Foreign.",
                true,
                true);
            TestAssert.Throws<ArgumentException>(
                () => new ContextualRetrievalDecision(
                    HashA,
                    ContextualRetrievalStrategy.BestAvailable,
                    new[] { evidence, foreign },
                    new[] { "RULE" },
                    new[] { "why" }),
                "Retrieval must reject mixed perspective owners.");
        }

        public static void DevelopmentalHtnIsPlanOnlyAndWaitCannotClaimEffects()
        {
            var safety = new DevelopmentalPlanStep(
                "HTN-STABILIZE",
                "STABILIZE_SAFETY",
                "Safety first.",
                true,
                new[] { "safety_stabilized" });
            var wait = new DevelopmentalPlanStep(
                "HTN-WAIT",
                "WAIT_FOR_KNOWN_OPPORTUNITY",
                "No known opportunity.",
                false);
            var plan = new DevelopmentalPlanProposal(
                HashA,
                HashB,
                "ATTEMPT_RELATIONSHIP_REPAIR",
                new[] { safety, wait },
                new[] { "HTN:BOUNDED_PLAN_ONLY", "HTN:SAFETY_FIRST" });

            TestAssert.False(plan.CanonicalMutationClaimed, "Plan cannot claim canonical mutation.");
            TestAssert.False(plan.SuccessClaimed, "Plan cannot assert success.");
            TestAssert.False(plan.DirectActionAuthority, "Plan cannot issue a job.");
            TestAssert.False(wait.SuccessClaimed, "Wait cannot assert success.");
            TestAssert.False(wait.DirectActionAuthority, "Wait cannot issue a job.");

            TestAssert.Throws<ArgumentException>(
                () => new DevelopmentalPlanStep(
                    "BAD-WAIT",
                    "WAIT",
                    "Invalid.",
                    false,
                    new[] { "repair_attempted" }),
                "Non-executable steps cannot claim effects.");
        }

        public static void OwnerPolicyFeedbackCannotBecomeCharacterState()
        {
            var feedback = new OwnerPolicyFeedbackRecord(
                HashA,
                OwnerPolicyArea.Reaction,
                "SEVERITY_HYBRID",
                OwnerPolicyFeedbackVerdict.Prefer,
                100,
                "Owner prefers severity hybrid with explicit contextual modifiers.");
            var snapshot = new OwnerPolicyCalibrationSnapshot(
                5,
                new Dictionary<string, double>
                {
                    ["REACTION:SEVERITY_HYBRID"] = 0.2
                },
                new[] { HashA });

            TestAssert.False(feedback.IsCharacterEvidence, "Owner policy feedback is not pawn evidence.");
            TestAssert.False(snapshot.IsCharacterState, "Owner policy calibration is not character state.");
            TestAssert.Equal(0.2, snapshot.Scores["REACTION:SEVERITY_HYBRID"], "Calibration must retain bounded score.");

            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new OwnerPolicyCalibrationSnapshot(
                    1,
                    new Dictionary<string, double>
                    {
                        ["REACTION:TOO_LARGE"] = 0.21
                    },
                    Array.Empty<string>()),
                "Policy calibration scores must remain bounded.");
        }

        public static void EventEnvelopeDistinguishesAttemptFromObservedOutcome()
        {
            var actor = IndividualId.New();
            var target = IndividualId.New();
            var attempt = new RimWorldEventEvidenceEnvelope(
                EventId.New(),
                "rimworld.rescue.attempted",
                100,
                actor,
                target,
                new[] { target, actor },
                RimWorldEvidenceOutcomeState.Attempted,
                RimWorldEvidencePrivacyDomain.Local,
                "Mosaic.RimWorld.ReadOnlyAdapter",
                HashA);
            var completed = new RimWorldEventEvidenceEnvelope(
                EventId.New(),
                "rimworld.rescue.completed",
                200,
                actor,
                target,
                new[] { actor, target },
                RimWorldEvidenceOutcomeState.Completed,
                RimWorldEvidencePrivacyDomain.Local,
                "Mosaic.RimWorld.ReadOnlyAdapter",
                HashB);

            TestAssert.False(attempt.IsObservedOutcome, "Attempt must not be treated as observed outcome.");
            TestAssert.False(attempt.CanClaimSuccess, "Attempt cannot claim success.");
            TestAssert.True(completed.IsObservedOutcome, "Completion is an observed outcome.");
            TestAssert.True(completed.CanClaimSuccess, "Completed outcome may claim success.");
            TestAssert.True(
                attempt.ParticipantIds.SequenceEqual(completed.ParticipantIds),
                "Participant order must be canonical.");
            TestAssert.False(completed.DirectCharacterMutation, "Adapter evidence cannot mutate character state.");
            TestAssert.False(completed.DirectActionAuthority, "Adapter evidence cannot issue jobs.");
        }

        public static void EventEnvelopeRejectsDuplicateParticipants()
        {
            var actor = IndividualId.New();
            TestAssert.Throws<ArgumentException>(
                () => new RimWorldEventEvidenceEnvelope(
                    EventId.New(),
                    "rimworld.job.started",
                    10,
                    actor,
                    null,
                    new[] { actor, actor },
                    RimWorldEvidenceOutcomeState.Attempted,
                    RimWorldEvidencePrivacyDomain.Local,
                    "Adapter",
                    HashA),
                "Event participant IDs must be unique.");
        }
    }
}
