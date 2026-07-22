using System;
using System.Collections.Generic;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;
using Dagmay.Core.Interaction;
using Dagmay.Core.Lifecycle;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;
using Dagmay.Core.Reflection;
using Dagmay.Core.Relationships;
using Dagmay.Core.Scheduling;
using Dagmay.Core.Validation;

namespace Dagmay.Tests
{
    internal static class CoreContractTests
    {
        public static void StrongIdsRoundTripAndRejectEmpty()
        {
            var id = IndividualId.New();
            TestAssert.Equal(id, IndividualId.Parse(id.ToString()), "An individual ID must round-trip.");
            TestAssert.Throws<ArgumentException>(
                () => new IndividualId(Guid.Empty),
                "An empty individual ID must be rejected.");
        }

        public static void AffectIsBoundedAndComposable()
        {
            var target = new AffectVector(1, 1, 1, -1, 0.5, -0.5, 0.25);
            var blended = AffectVector.Neutral.BlendToward(target, 0.25);
            TestAssert.Equal(0.25, blended.Valence, "Affect blending must be deterministic.");
            TestAssert.Equal(-0.25, blended.Agency, "Negative affect dimensions must blend correctly.");
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new AffectVector(1.01, 0, 0, 0, 0, 0, 0),
                "Out-of-range affect must be rejected.");
        }

        public static void ContinuityUsesAcceptedWeightedCombination()
        {
            var profile = ContinuityProfile.InitialDefault;
            var total = profile.GroundedSeed
                + profile.AutobiographicalHistory
                + profile.Relationships
                + profile.ValuesAndCommitments
                + profile.ExpressionAndHabits;

            TestAssert.Approximately(1.0, total, 0.000001, "Continuity weights must total one.");
            TestAssert.True(profile.GroundedSeed < 1.0, "No single continuity category may define the individual alone.");
        }

        public static void RenamePreservesIdentityAndLineage()
        {
            var state = CreateIndividual("Mira");
            var renamed = state.Rename("Mira Vale", state.Version);

            TestAssert.Equal(state.Id, renamed.Id, "Rename must preserve individual ID.");
            TestAssert.Equal(state.LineageId, renamed.LineageId, "Rename must preserve lineage.");
            TestAssert.Equal(1L, renamed.Version, "Rename must advance the state version once.");
        }

        public static void InWorldRevivalContinuesTheSameLineage()
        {
            var state = CreateIndividual("Jo");
            var archived = state.TransitionLifecycle(
                LifecycleState.Archived,
                LifecycleTransitionKind.InWorldDeath,
                state.Version);

            TestAssert.Throws<InvalidOperationException>(
                () => archived.WithAffect(AffectVector.Neutral, archived.Version),
                "Archived individuals must not receive ordinary reflection mutations.");

            var revived = archived.TransitionLifecycle(
                LifecycleState.Active,
                LifecycleTransitionKind.InWorldRevival,
                archived.Version);

            TestAssert.Equal(state.Id, revived.Id, "In-world revival must preserve individual ID.");
            TestAssert.Equal(state.LineageId, revived.LineageId, "In-world revival must preserve lineage.");
        }

        public static void LedgerDeduplicatesEvents()
        {
            var ledger = new InMemoryEventLedger();
            var first = CreateEvent("injury:world-1:pawn-4:tick-100");
            var sameKey = CreateEvent("injury:world-1:pawn-4:tick-100");

            TestAssert.Equal(EventAppendStatus.Appended, ledger.Append(first).Status, "First event must append.");
            TestAssert.Equal(
                EventAppendStatus.DuplicateDeduplicationKey,
                ledger.Append(sameKey).Status,
                "A repeated environment callback must not create a second event.");
            TestAssert.Equal(1, ledger.Snapshot().Count, "The ledger must contain only one canonical event.");
        }

        public static void PerspectivesCanDisagreeWithoutChangingFacts()
        {
            var source = CreateEvent("care:world-1:tick-200");
            var jo = IndividualId.New();
            var mira = IndividualId.New();

            var joPerspective = new PerceivedEvent(
                PerceptionId.New(),
                source.Id,
                jo,
                PerceptionChannel.Experienced,
                0.95,
                new Dictionary<string, string> { ["meaning"] = "Mira stayed to help me" },
                Array.Empty<string>(),
                4);

            var miraPerspective = new PerceivedEvent(
                PerceptionId.New(),
                source.Id,
                mira,
                PerceptionChannel.Experienced,
                0.95,
                new Dictionary<string, string> { ["meaning"] = "I treated Jo because I was available" },
                Array.Empty<string>(),
                7);

            TestAssert.Equal(source.Id, joPerspective.SourceEventId, "Jo's perspective must retain fact provenance.");
            TestAssert.Equal(source.Id, miraPerspective.SourceEventId, "Mira's perspective must retain fact provenance.");
            TestAssert.False(
                joPerspective.AvailableDetails["meaning"] == miraPerspective.AvailableDetails["meaning"],
                "Subjective perspectives must be allowed to differ.");
        }

        public static void ValidatedMutationIsAtomicAndReplaySafe()
        {
            var state = CreateIndividual("Niko");
            var store = new InMemoryIdentityStore();
            var ledger = new InMemoryEventLedger();
            var source = CreateEvent("rescue:world-1:tick-300", state.Id);
            store.TryAdd(state);
            ledger.Append(source);

            var proposal = new ProposedAffectMutation(
                RequestId.New(),
                state.Id,
                state.Version,
                new AffectVector(0.2, 0.1, -0.1, 0.2, 0.15, 0.1, 0.05),
                new[] { source.Id },
                0.8,
                "The rescue modestly increased safety and affiliation.");

            var service = new AffectMutationService(store, ledger);
            var committed = service.TryCommit(proposal);
            TestAssert.Equal(MutationCommitStatus.Committed, committed.Status, "A grounded bounded mutation must commit.");
            TestAssert.True(committed.State is not null, "A successful commit must return the new state.");
            TestAssert.Equal(1L, committed.State!.Version, "A commit must advance the state once.");

            var replay = service.TryCommit(proposal);
            TestAssert.Equal(MutationCommitStatus.DuplicateResponse, replay.Status, "A response must not commit twice.");
        }

        public static void ExcessiveOrUngroundedMutationDoesNotChangeState()
        {
            var state = CreateIndividual("Ari");
            var store = new InMemoryIdentityStore();
            var ledger = new InMemoryEventLedger();
            store.TryAdd(state);

            var proposal = new ProposedAffectMutation(
                RequestId.New(),
                state.Id,
                state.Version,
                new AffectVector(0.9, 0.9, 0.9, 0.9, 0.9, 0.9, 0.9),
                new[] { EventId.New() },
                1.0,
                "Unsupported wholesale change.");

            var service = new AffectMutationService(store, ledger);
            var rejected = service.TryCommit(proposal);
            TestAssert.Equal(MutationCommitStatus.MissingEvidence, rejected.Status, "Unknown evidence must reject the proposal.");

            TestAssert.True(store.TryGet(state.Id, out var unchanged), "The identity must remain available.");
            TestAssert.Equal(0L, unchanged!.Version, "Rejected output must make zero canonical mutations.");
        }

        public static void QueueIsBoundedAndProtectsCriticalWork()
        {
            var now = DateTimeOffset.UtcNow;
            var person = IndividualId.New();
            var queue = new ReflectionQueue(2);
            var first = CreateTask(person, ReflectionPriority.Background, now, "background:1");
            var second = CreateTask(person, ReflectionPriority.Background, now.AddSeconds(1), "background:2");
            var critical = CreateTask(person, ReflectionPriority.CriticalLifecycle, now.AddSeconds(2), "death:1");

            queue.Enqueue(first);
            queue.Enqueue(second);
            var result = queue.Enqueue(critical);

            TestAssert.Equal(
                QueueEnqueueStatus.EnqueuedAfterEvictingLowerPriority,
                result.Status,
                "Critical work must displace lower-priority work at capacity.");
            TestAssert.Equal(2, queue.Count, "Queue capacity must remain bounded.");

            TestAssert.True(queue.TryDequeue(now.AddMinutes(1), out var dequeued), "A queued task must be available.");
            TestAssert.Equal(ReflectionPriority.CriticalLifecycle, dequeued!.Priority, "Critical work must be selected first.");
        }

        public static void SelectiveEnrollmentCannotCreateHalfIndividuals()
        {
            var entity = new EnvironmentEntityReference("rimworld", "pawn:world-1:42", "Mira");
            var enrolled = new EnrollmentRecord(entity, EnrollmentLevel.EnrolledIndividual, IndividualId.New());
            var known = new EnrollmentRecord(entity, EnrollmentLevel.KnownPerson, null);

            TestAssert.True(enrolled.IndividualId.HasValue, "Full enrollment must carry an individual ID.");
            TestAssert.False(known.IndividualId.HasValue, "A known person must not masquerade as a full individual.");
            TestAssert.Throws<ArgumentException>(
                () => new EnrollmentRecord(entity, EnrollmentLevel.EnrolledIndividual, null),
                "A half-created enrolled individual must be rejected.");
        }

        public static void InteractionOriginPreventsOperatorImpersonation()
        {
            var person = IndividualId.New();
            var entity = new EnvironmentEntityReference("rimworld", "pawn:world-1:77", "Perspective pawn");
            var external = InteractionOrigin.ExternalOperator("RimWorld player");
            var embodied = InteractionOrigin.Embodied(entity, person, "Perspective pawn");
            var embodiedButNotEnrolled = InteractionOrigin.Embodied(entity, null, "Perspective pawn");

            TestAssert.False(external.EmbodiedIndividualId.HasValue, "The ordinary external operator must not become a character silently.");
            TestAssert.Equal(person, embodied.EmbodiedIndividualId!.Value, "An embodied interaction must name its actual character.");
            TestAssert.False(
                embodiedButNotEnrolled.EmbodiedIndividualId.HasValue,
                "An embodied player character does not have to be a full Dagmay individual.");
            TestAssert.Throws<ArgumentException>(
                () => new InteractionOrigin(InteractionOriginKind.ExternalOperator, entity, person, "invalid"),
                "An external operator must not impersonate an individual.");
        }

        public static void RelationshipsAreAsymmetricByConstruction()
        {
            var mira = IndividualId.New();
            var jo = IndividualId.New();
            var source = EventId.New();
            var miraTarget = new RelationshipTarget(
                new EnvironmentEntityReference("rimworld", "pawn:world-1:mira", "Mira"),
                mira);
            var joTarget = new RelationshipTarget(
                new EnvironmentEntityReference("rimworld", "pawn:world-1:jo", "Jo"),
                jo);
            var miraTowardJo = new RelationshipRecord(
                mira,
                joTarget,
                new RelationshipDimensions(0.7, 0.4, 0.1, 0, 0.8),
                0.9,
                new[] { source },
                12);
            var joTowardMira = new RelationshipRecord(
                jo,
                miraTarget,
                new RelationshipDimensions(0.2, 0.1, 0.3, 0.2, 0.6),
                0.7,
                new[] { source },
                9);

            TestAssert.False(
                miraTowardJo.Dimensions.Trust == joTowardMira.Dimensions.Trust,
                "Reciprocal relationships must be allowed to differ.");
            TestAssert.Equal(mira, miraTowardJo.OwnerId, "Relationship state belongs to its observer.");
        }

        private static IndividualState CreateIndividual(string name)
        {
            var seed = new IdentitySeed(
                "rimworld",
                "0.1A",
                new[] { new SeedFact(SeedFactCategory.Trait, "kind", "Kind", "RimWorld trait", 1.0) });
            return IndividualState.Create(name, seed);
        }

        private static EnvironmentEvent CreateEvent(string deduplicationKey, params IndividualId[] subjects)
        {
            var now = DateTimeOffset.UtcNow;
            return new EnvironmentEvent(
                EventId.New(),
                deduplicationKey,
                "test.event",
                "rimworld",
                now,
                now,
                100,
                "Dagmay.Tests",
                new Dictionary<string, string> { ["fixture"] = "true" },
                subjects);
        }

        private static ReflectionTask CreateTask(
            IndividualId person,
            ReflectionPriority priority,
            DateTimeOffset created,
            string coalescingKey)
        {
            return new ReflectionTask(
                ReflectionTaskId.New(),
                person,
                ModelTaskKind.BackgroundReflection,
                priority,
                created,
                coalescingKey,
                Array.Empty<EventId>(),
                500);
        }
    }
}
