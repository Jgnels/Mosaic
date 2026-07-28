using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Dagmay.Core.Affect;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Contracts;
using Dagmay.Core.Decisions;
using Dagmay.RimWorld.Perception;

namespace Dagmay.Tests
{
    internal static class Mosaic03GroundedSocialAffectContractTests
    {
        private const string BatchA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string BatchB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private static readonly IndividualId Actor = IndividualId.Parse("c2000000000000000000000000000002");
        private static readonly IndividualId Target = IndividualId.Parse("c2000000000000000000000000000001");
        private static readonly LineageId ActorLineage = LineageId.Parse("d2000000000000000000000000000002");

        public static void TypedOpinionEvidenceMatchesPr10AndStripsDisplaySemantics()
        {
            var payload = OpinionPayload();
            var evidence = Project(EventId.Parse("c1000000000000000000000000000001"), 1, BatchA, GroundedSocialProjectionHash.OpinionChanged, payload);
            TestAssert.Equal(20, evidence.OpinionDelta!.Value, "Typed evidence must retain the bounded opinion delta.");
            TestAssert.Equal(Actor, evidence.PerspectiveOwnerId, "Perspective owner must remain the actor.");
            TestAssert.Equal(Target, evidence.CounterpartId, "Counterpart must remain the stable target ID.");
            TestAssert.False(evidence.DirectCharacterMutation, "Typed evidence cannot mutate character state.");
            TestAssert.False(evidence.DirectActionAuthority, "Typed evidence cannot issue pawn actions.");
        }

        public static void DisplayRenameChangesSourceFingerprintButNotDedupOrSemanticIdentity()
        {
            var firstPayload = OpinionPayload();
            var secondPayload = OpinionPayload();
            secondPayload["target_name"] = "Renamed counterpart";
            secondPayload["display_name_at_event"] = "Renamed owner";
            var eventId = EventId.Parse("c1000000000000000000000000000002");
            var first = Project(eventId, 2, BatchA, GroundedSocialProjectionHash.OpinionChanged, firstPayload);
            var second = Project(eventId, 2, BatchA, GroundedSocialProjectionHash.OpinionChanged, secondPayload);
            TestAssert.Equal(first.DeduplicationKey, second.DeduplicationKey, "Display labels must not enter PR #10 deduplication.");
            TestAssert.Equal(first.SemanticId, second.SemanticId, "Display labels must not change semantic identity.");
            TestAssert.False(string.Equals(first.SourceEnvelopeFingerprint, second.SourceEnvelopeFingerprint, StringComparison.Ordinal), "Diagnostic source fingerprint may record the raw rename.");
        }

        public static void StaleDeduplicationKeyRejectsMaterialPayloadChange()
        {
            var original = OpinionPayload();
            var envelope = Envelope(EventId.Parse("c1000000000000000000000000000003"), 900, GroundedSocialProjectionHash.OpinionChanged, original);
            var changed = OpinionPayload();
            changed["opinion_after"] = "21";
            changed["opinion_delta"] = "21";
            TestAssert.Throws<ArgumentException>(
                () => new GroundedSocialSemanticEvidence(envelope, 3, BatchA, changed),
                "Payload/key disagreement must fail closed.");
        }

        public static void SourceContractAcceptsProvenanceAndLabelsButRejectsForeignSemanticKeys()
        {
            var valid = Project(EventId.Parse("c1000000000000000000000000000004"), 4, BatchA, GroundedSocialProjectionHash.OpinionChanged, OpinionPayload());
            TestAssert.Equal(20, valid.OpinionAfter!.Value, "PR #10 raw provenance fields must be accepted.");
            var foreign = OpinionPayload();
            foreign["player_visible_secret"] = "true";
            TestAssert.True(
                GroundedSocialSemanticProjectionAdapter.TryProject(
                    Projection(EventId.Parse("c1000000000000000000000000000005"), 900, GroundedSocialProjectionHash.OpinionChanged, foreign),
                    5,
                    BatchA,
                    foreign) is null,
                "Foreign semantic payload keys must fail closed.");
        }

        public static void DirectRelationUsesNoneSentinelAndCanonicalOrdering()
        {
            var evidence = Project(EventId.Parse("c1000000000000000000000000000006"), 6, BatchA, GroundedSocialProjectionHash.DirectRelationshipChanged, RelationshipPayload("none", "Friend"));
            TestAssert.Equal(0, evidence.RelationsBefore.Count, "The canonical none sentinel must become an empty typed set.");
            TestAssert.Equal("Friend", evidence.RelationsAfter[0], "The relation token must survive typed parsing.");
            var noncanonical = RelationshipPayload("none", "Spouse,Lover");
            TestAssert.True(
                GroundedSocialSemanticProjectionAdapter.TryProject(
                    Projection(EventId.Parse("c1000000000000000000000000000007"), 900, GroundedSocialProjectionHash.DirectRelationshipChanged, noncanonical),
                    7,
                    BatchA,
                    noncanonical) is null,
                "Noncanonical relation ordering must fail closed.");
        }

        public static void SocialWrapperEnforcesActorTargetParticipantsOutcomeAndPrivacy()
        {
            var payload = OpinionPayload();
            var eventId = EventId.Parse("c1000000000000000000000000000008");
            var dedup = GroundedSocialProjectionHash.ComputePr10DeduplicationKey(eventId, GroundedSocialProjectionHash.OpinionChanged, 900, Actor, Target, payload);
            var wrongParticipants = new RimWorldEventEvidenceEnvelope(
                eventId,
                GroundedSocialProjectionHash.OpinionChanged,
                900,
                Actor,
                Target,
                new[] { Actor },
                RimWorldEvidenceOutcomeState.Observed,
                RimWorldEvidencePrivacyDomain.RelationshipPrivate,
                GroundedSocialProjectionHash.SourceAdapter,
                dedup);
            TestAssert.Throws<ArgumentException>(
                () => new GroundedSocialSemanticEvidence(wrongParticipants, 8, BatchA, payload),
                "Strict social evidence must require actor and target exactly once.");
        }

        public static void BundlerCoalescesOneOpinionAndOneRelationExactlyOnce()
        {
            var pair = Pair(BatchA, 10, 11, 900, "Friend");
            var bundles = GroundedSocialObservationBundler.Bundle(pair.Reverse());
            TestAssert.Equal(1, bundles.Count, "One capture batch must become one compound bundle.");
            TestAssert.Equal(2, bundles[0].SourceEvidenceIds.Count, "Both exact source EventIds must survive coalescing.");
            TestAssert.Equal(11L, bundles[0].CommitAdmissionPosition, "Commit position must be the highest source position.");
            TestAssert.False(bundles[0].DirectActionAuthority, "Bundle cannot issue pawn actions.");
        }

        public static void SameTickDifferentBatchDoesNotCoalesce()
        {
            var first = Project(EventId.Parse("c1000000000000000000000000000012"), 12, BatchA, GroundedSocialProjectionHash.OpinionChanged, OpinionPayload());
            var second = Project(EventId.Parse("c1000000000000000000000000000013"), 13, BatchB, GroundedSocialProjectionHash.OpinionChanged, OpinionPayload());
            var bundles = GroundedSocialObservationBundler.Bundle(new[] { second, first });
            TestAssert.Equal(2, bundles.Count, "Same tick across separate capture batches must remain separate.");
        }

        public static void OneBatchCannotMixOwnerDirectionOrDuplicateKind()
        {
            var first = Project(EventId.Parse("c1000000000000000000000000000014"), 14, BatchA, GroundedSocialProjectionHash.OpinionChanged, OpinionPayload());
            var reversePayload = OpinionPayload();
            var reverseEnvelope = Envelope(EventId.Parse("c1000000000000000000000000000015"), 900, GroundedSocialProjectionHash.OpinionChanged, reversePayload, Target, Actor);
            var reverse = new GroundedSocialSemanticEvidence(reverseEnvelope, 15, BatchA, reversePayload);
            TestAssert.Throws<ArgumentException>(
                () => GroundedSocialObservationBundler.Bundle(new[] { first, reverse }),
                "One batch cannot mix directional owners.");

            var duplicateKind = Project(EventId.Parse("c1000000000000000000000000000016"), 16, BatchA, GroundedSocialProjectionHash.OpinionChanged, OpinionPayload());
            TestAssert.Throws<ArgumentException>(
                () => GroundedSocialObservationBundler.Bundle(new[] { first, duplicateKind }),
                "One bundle cannot contain two opinion changes.");
        }

        public static void AppraisalUsesOwnerCalibratedFriendMeaningWithoutAuthority()
        {
            var bundle = GroundedSocialObservationBundler.Bundle(Pair(BatchA, 20, 21, 900, "Friend"))[0];
            var appraisal = GroundedSocialAppraisalPolicy.Appraise(bundle);
            TestAssert.Equal(MosaicEmotionFamily.Affection, appraisal.PrimaryFamily, "Friend transition must follow the owner-calibrated affection meaning.");
            TestAssert.Equal(MosaicEmotionFamily.Trust, appraisal.SecondaryFamily!.Value, "Friend transition must retain trust as secondary meaning.");
            TestAssert.Equal(2, appraisal.SourceEvidenceIds.Count, "Appraisal must retain both exact evidence IDs.");
            TestAssert.False(appraisal.DirectCharacterMutation, "Appraisal is a proposal, not a direct mutation.");
            TestAssert.False(appraisal.DirectActionAuthority, "Appraisal cannot issue pawn actions.");
        }

        public static void ExPartnerStatusDoesNotInventResentment()
        {
            var bundle = GroundedSocialObservationBundler.Bundle(Pair(BatchA, 22, 23, 900, "ExLover", opinionDelta: -30))[0];
            var appraisal = GroundedSocialAppraisalPolicy.Appraise(bundle);
            TestAssert.Equal(MosaicEmotionFamily.Heartbreak, appraisal.PrimaryFamily, "Ex-partner status may ground loss, not automatic hostility.");
            TestAssert.False(appraisal.PrimaryFamily == MosaicEmotionFamily.Resentment, "Ex status alone must not invent resentment.");
        }

        public static void ContradictoryIntimateBondAndNegativeOpinionRemainAmbiguous()
        {
            var bundle = GroundedSocialObservationBundler.Bundle(Pair(BatchA, 24, 25, 900, "Lover", opinionDelta: -20))[0];
            var appraisal = GroundedSocialAppraisalPolicy.Appraise(bundle);
            TestAssert.Equal(MosaicEmotionFamily.Ambiguity, appraisal.PrimaryFamily, "Conflicting same-batch evidence must remain explicit ambiguity.");
        }

        public static void UnknownKinshipDoesNotInventEmotion()
        {
            var relation = Project(EventId.Parse("c1000000000000000000000000000026"), 26, BatchA, GroundedSocialProjectionHash.DirectRelationshipChanged, RelationshipPayload("none", "Sibling"));
            var appraisal = GroundedSocialAppraisalPolicy.Appraise(GroundedSocialObservationBundler.Bundle(new[] { relation })[0]);
            TestAssert.Equal(MosaicEmotionFamily.Ambiguity, appraisal.PrimaryFamily, "Unknown or kinship relation facts must not imply affection or hostility.");
        }

        public static void AffectReducerCommitsOnceAtCanonicalSourcePosition()
        {
            var appraisal = GroundedSocialAppraisalPolicy.Appraise(GroundedSocialObservationBundler.Bundle(Pair(BatchA, 30, 31, 900, "Friend"))[0]);
            var initial = BoundedAffectState.Initial(Actor, ActorLineage);
            var proposal = GroundedSocialAffectReducer.Propose(initial, appraisal);
            var committed = GroundedSocialAffectReducer.Commit(initial, proposal);
            TestAssert.Equal(31L, committed.LastAdmissionPosition, "Affect commit must use the highest bundled source position.");
            TestAssert.Equal(1L, committed.AppliedCount, "A compound pair must update affect exactly once.");
            TestAssert.Equal(1L, committed.Version, "Affect version must advance atomically.");
        }

        public static void AffectReducerAcceptsPositionGapsAndRejectsStaleReplay()
        {
            var firstAppraisal = GroundedSocialAppraisalPolicy.Appraise(GroundedSocialObservationBundler.Bundle(Pair(BatchA, 40, 41, 900, "Friend"))[0]);
            var initial = BoundedAffectState.Initial(Actor, ActorLineage);
            var first = GroundedSocialAffectReducer.Commit(initial, GroundedSocialAffectReducer.Propose(initial, firstAppraisal));
            var secondAppraisal = GroundedSocialAppraisalPolicy.Appraise(GroundedSocialObservationBundler.Bundle(Pair(BatchB, 60, 61, 1200, "ExLover", opinionDelta: -20))[0]);
            var second = GroundedSocialAffectReducer.Commit(first, GroundedSocialAffectReducer.Propose(first, secondAppraisal));
            TestAssert.Equal(61L, second.LastAdmissionPosition, "Unrelated ledger positions may create safe gaps.");
            TestAssert.Throws<ArgumentException>(
                () => GroundedSocialAffectReducer.Propose(second, firstAppraisal),
                "A stale source position must fail closed.");
        }

        public static void ForgedDeltaAndRollingDigestFailClosed()
        {
            var appraisal = GroundedSocialAppraisalPolicy.Appraise(GroundedSocialObservationBundler.Bundle(Pair(BatchA, 70, 71, 900, "Friend"))[0]);
            var initial = BoundedAffectState.Initial(Actor, ActorLineage);
            var valid = GroundedSocialAffectReducer.Propose(initial, appraisal);
            var forgedDelta = ForgeProposal(
                valid,
                new FixedAffectVector(valence: FixedAffectVector.Scale),
                valid.NextRollingEvidenceDigest);
            TestAssert.Throws<ArgumentException>(
                () => GroundedSocialAffectReducer.Commit(initial, forgedDelta),
                "Changing the delta without changing the integrity ID must fail closed.");

            var forgedDigest = ForgeProposal(
                valid,
                valid.Delta,
                new string('f', 64));
            TestAssert.Throws<ArgumentException>(
                () => GroundedSocialAffectReducer.Commit(initial, forgedDigest),
                "Changing the rolling digest without changing the integrity ID must fail closed.");
        }

        public static void ForeignLineageAndStaleStateFailClosed()
        {
            var appraisal = GroundedSocialAppraisalPolicy.Appraise(GroundedSocialObservationBundler.Bundle(Pair(BatchA, 80, 81, 900, "Friend"))[0]);
            var initial = BoundedAffectState.Initial(Actor, ActorLineage);
            var proposal = GroundedSocialAffectReducer.Propose(initial, appraisal);
            var committed = GroundedSocialAffectReducer.Commit(initial, proposal);
            TestAssert.Throws<ArgumentException>(
                () => GroundedSocialAffectReducer.Commit(committed, proposal),
                "A replayed stale proposal must fail closed.");
            var replacement = BoundedAffectState.Initial(Actor, LineageId.Parse("d2000000000000000000000000000099"));
            TestAssert.Throws<ArgumentException>(
                () => GroundedSocialAffectReducer.Commit(replacement, proposal),
                "A replacement lineage cannot inherit another lineage's proposal.");
        }

        public static void HugeDecaySaturatesWithoutOverflow()
        {
            var vector = new FixedAffectVector(
                1_000_000, -1_000_000, 1_000_000, -1_000_000, 1_000_000, -1_000_000, 1_000_000);
            var decayed = vector.Decay(long.MaxValue);
            TestAssert.Equal(0, decayed.Valence, "Huge elapsed tick decay must safely reach zero.");
            TestAssert.Equal(0, decayed.AttachmentAffiliation, "Huge elapsed tick decay must not overflow.");
        }

        public static void CheckpointIsDeterministicAndTamperEvident()
        {
            var first = BoundedAffectState.Initial(Actor, ActorLineage);
            var second = BoundedAffectState.Initial(Target, LineageId.Parse("d2000000000000000000000000000001"));
            var forward = BoundedAffectCheckpoint.Create(new[] { first, second });
            var reverse = BoundedAffectCheckpoint.Create(new[] { second, first });
            TestAssert.Equal(forward.CheckpointHash, reverse.CheckpointHash, "Checkpoint order must be canonical.");
            TestAssert.Throws<ArgumentException>(
                () => BoundedAffectCheckpoint.Restore(forward.States, new string('f', 64)),
                "Checkpoint hash disagreement must fail closed.");
            TestAssert.Throws<ArgumentException>(
                () => BoundedAffectCheckpoint.Create(new BoundedAffectState[] { first, null! }),
                "A null checkpoint entry must fail closed before canonical sorting.");
        }

        public static void VisibleReactionSurfaceExcludesRawPrivatePayload()
        {
            var appraisal = GroundedSocialAppraisalPolicy.Appraise(GroundedSocialObservationBundler.Bundle(Pair(BatchA, 90, 91, 900, "Friend"))[0]);
            var visible = appraisal.VisibleReactionSeed + " " + appraisal.LaterStoryConsequence;
            TestAssert.False(visible.IndexOf("Thing_Actor", StringComparison.Ordinal) >= 0, "Actor external ID cannot enter visible text.");
            TestAssert.False(visible.IndexOf("Thing_Target", StringComparison.Ordinal) >= 0, "Target external ID cannot enter visible text.");
            TestAssert.False(visible.IndexOf("Perspective owner", StringComparison.Ordinal) >= 0, "Display name cannot enter visible text.");
            TestAssert.False(visible.IndexOf("20", StringComparison.Ordinal) >= 0, "Raw private opinion value cannot enter visible text.");
        }


        private static AffectTransitionProposal ForgeProposal(
            AffectTransitionProposal source,
            FixedAffectVector delta,
            string nextRollingEvidenceDigest)
        {
            var constructor = typeof(AffectTransitionProposal)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single();
            return (AffectTransitionProposal)constructor.Invoke(new object[]
            {
                source.ProposalId,
                source.IndividualId,
                source.LineageId,
                source.ExpectedStateVersion,
                source.ExpectedStateFingerprint,
                source.AdmissionPosition,
                source.Tick,
                source.BundleId,
                source.EvidenceIds,
                source.AppraisalId,
                delta,
                nextRollingEvidenceDigest
            });
        }

        private static GroundedSocialSemanticEvidence[] Pair(
            string batch,
            long opinionPosition,
            long relationPosition,
            long tick,
            string relationAfter,
            int opinionDelta = 20)
        {
            var before = 0;
            var opinionPayload = OpinionPayload(before, opinionDelta);
            var opinion = Project(
                EventId.Parse("e1" + opinionPosition.ToString("x30")),
                opinionPosition,
                batch,
                GroundedSocialProjectionHash.OpinionChanged,
                opinionPayload,
                tick);
            var relation = Project(
                EventId.Parse("e2" + relationPosition.ToString("x30")),
                relationPosition,
                batch,
                GroundedSocialProjectionHash.DirectRelationshipChanged,
                RelationshipPayload("none", relationAfter),
                tick);
            return new[] { opinion, relation };
        }

        private static GroundedSocialSemanticEvidence Project(
            EventId eventId,
            long position,
            string batch,
            string kind,
            IDictionary<string, string> payload,
            long tick = 900)
        {
            var projection = Projection(eventId, tick, kind, payload);
            var semantic = GroundedSocialSemanticProjectionAdapter.TryProject(
                projection,
                position,
                batch,
                payload);
            TestAssert.True(semantic is not null, "Supported typed social evidence must project.");
            return semantic!;
        }

        private static ReadOnlyRimWorldEventProjection Projection(
            EventId eventId,
            long tick,
            string kind,
            IDictionary<string, string> payload)
        {
            var projection = ReadOnlySocialEventEnvelopeAdapter.TryProject(
                eventId,
                tick,
                kind,
                payload,
                Actor,
                Target);
            TestAssert.True(projection is not null, "PR #10 source projection must succeed.");
            return projection!;
        }

        private static RimWorldEventEvidenceEnvelope Envelope(
            EventId eventId,
            long tick,
            string kind,
            IDictionary<string, string> payload,
            IndividualId? actor = null,
            IndividualId? target = null)
        {
            var actualActor = actor ?? Actor;
            var actualTarget = target ?? Target;
            var dedup = GroundedSocialProjectionHash.ComputePr10DeduplicationKey(
                eventId,
                kind,
                tick,
                actualActor,
                actualTarget,
                payload);
            return new RimWorldEventEvidenceEnvelope(
                eventId,
                kind,
                tick,
                actualActor,
                actualTarget,
                new[] { actualActor, actualTarget },
                RimWorldEvidenceOutcomeState.Observed,
                RimWorldEvidencePrivacyDomain.RelationshipPrivate,
                GroundedSocialProjectionHash.SourceAdapter,
                dedup);
        }

        private static Dictionary<string, string> OpinionPayload(int before = 0, int delta = 20) =>
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target_external_id"] = "Thing_Target",
                ["target_name"] = "Counterpart",
                ["opinion_before"] = before.ToString(CultureInfo.InvariantCulture),
                ["opinion_after"] = (before + delta).ToString(CultureInfo.InvariantCulture),
                ["opinion_delta"] = delta.ToString(CultureInfo.InvariantCulture),
                ["pawn_external_id"] = "Thing_Actor",
                ["display_name_at_event"] = "Perspective owner"
            };

        private static Dictionary<string, string> RelationshipPayload(string before, string after) =>
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target_external_id"] = "Thing_Target",
                ["target_name"] = "Counterpart",
                ["relations_before"] = before,
                ["relations_after"] = after,
                ["pawn_external_id"] = "Thing_Actor",
                ["display_name_at_event"] = "Perspective owner"
            };
    }
}
