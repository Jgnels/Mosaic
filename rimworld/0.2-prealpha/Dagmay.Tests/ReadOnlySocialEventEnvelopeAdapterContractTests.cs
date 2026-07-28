using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Contracts;
using Dagmay.Core.Decisions;
using Dagmay.Core.Identity;
using Dagmay.Core.Persistence;
using Dagmay.Core.Scheduling;
using Dagmay.RimWorld.Perception;

namespace Dagmay.Tests
{
    internal static class ReadOnlySocialEventEnvelopeAdapterContractTests
    {
        private static readonly EventId SourceEventId =
            EventId.Parse("c1000000000000000000000000000001");
        private static readonly IndividualId ActorId =
            IndividualId.Parse("c2000000000000000000000000000002");
        private static readonly IndividualId TargetId =
            IndividualId.Parse("c2000000000000000000000000000001");

        public static void OpinionChangeMappingSucceeds()
        {
            var projection = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload());

            TestAssert.Equal(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                projection.Envelope.EventKind,
                "The opinion-change fact must project into the exact supported event kind.");
        }

        public static void DirectRelationshipMappingSucceeds()
        {
            var projection = Project(
                ReadOnlySocialEventEnvelopeAdapter.DirectRelationshipChanged,
                RelationshipPayload());

            TestAssert.Equal(
                ReadOnlySocialEventEnvelopeAdapter.DirectRelationshipChanged,
                projection.Envelope.EventKind,
                "The direct-relationship fact must project into the exact supported event kind.");
        }

        public static void UnsupportedKindReturnsNoEnvelope()
        {
            var projection = ReadOnlySocialEventEnvelopeAdapter.TryProject(
                SourceEventId,
                900,
                "rimworld.skill.level_gained",
                new Dictionary<string, string> { ["skill"] = "Social" },
                ActorId,
                TargetId);

            TestAssert.True(projection is null, "Unsupported facts must produce no envelope.");
        }

        public static void ExactEventIdAndTickArePreserved()
        {
            var projection = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload(),
                tick: 123456);

            TestAssert.Equal(
                SourceEventId,
                projection.Envelope.EventId,
                "The admitted source EventId must be preserved exactly.");
            TestAssert.Equal(
                123456L,
                projection.Envelope.Tick,
                "The admitted game tick must be preserved exactly.");
        }

        public static void ActorAndTargetAreDistinctAndIncludedExactlyOnce()
        {
            var projection = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload());
            var envelope = projection.Envelope;

            TestAssert.True(
                envelope.ActorId.HasValue && envelope.TargetId.HasValue,
                "The social envelope must identify both actor and target.");
            TestAssert.False(
                envelope.ActorId!.Value == envelope.TargetId!.Value,
                "The observed perspective owner and counterpart must be distinct.");
            TestAssert.Equal(
                1,
                envelope.ParticipantIds.Count(value => value == ActorId),
                "The actor must occur exactly once in participants.");
            TestAssert.Equal(
                1,
                envelope.ParticipantIds.Count(value => value == TargetId),
                "The target must occur exactly once in participants.");
            TestAssert.Equal(
                2,
                envelope.ParticipantIds.Count,
                "Participants must contain exactly actor and target.");
        }

        public static void ParticipantOrderingIsDeterministic()
        {
            var forward = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload(),
                actorId: ActorId,
                targetId: TargetId);
            var reverse = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload(),
                actorId: TargetId,
                targetId: ActorId);

            TestAssert.True(
                forward.Envelope.ParticipantIds.SequenceEqual(reverse.Envelope.ParticipantIds),
                "Participant ordering must not depend on actor/target input order.");
            TestAssert.Equal(
                TargetId,
                forward.Envelope.ParticipantIds[0],
                "Participants must use canonical strong-ID ordering.");
            TestAssert.Equal(
                ActorId,
                forward.Envelope.ParticipantIds[1],
                "Participants must use canonical strong-ID ordering.");
        }

        public static void WitnessListIsEmpty()
        {
            var projection = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload());

            TestAssert.Equal(
                0,
                projection.WitnessIds.Count,
                "Polling social observation does not prove any witness.");
        }

        public static void OutcomeIsObserved()
        {
            var projection = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload());

            TestAssert.Equal(
                RimWorldEvidenceOutcomeState.Observed,
                projection.Envelope.OutcomeState,
                "The polling adapter may claim only an observed state.");
        }

        public static void PrivacyIsRelationshipPrivate()
        {
            var projection = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload());

            TestAssert.Equal(
                RimWorldEvidencePrivacyDomain.RelationshipPrivate,
                projection.Envelope.PrivacyDomain,
                "Social facts from this path must remain relationship-private.");
        }

        public static void CanClaimSuccessIsFalse()
        {
            var projection = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload());

            TestAssert.False(
                projection.Envelope.CanClaimSuccess,
                "An observed opinion or relationship change cannot claim success.");
        }

        public static void DirectActionAndCharacterMutationAuthorityRemainFalse()
        {
            var projection = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload());

            TestAssert.False(
                projection.Envelope.DirectActionAuthority,
                "The adapter must have no direct action authority.");
            TestAssert.False(
                projection.Envelope.DirectCharacterMutation,
                "The adapter must have no direct character-mutation authority.");
        }

        public static void PayloadEnumerationOrderDoesNotChangeDeduplicationKey()
        {
            var forward = OpinionPayload();
            var reverse = forward
                .Reverse()
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

            var first = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                forward);
            var second = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                reverse);

            TestAssert.Equal(
                first.Envelope.DeduplicationKey,
                second.Envelope.DeduplicationKey,
                "Dictionary enumeration order cannot affect the canonical deduplication key.");
        }

        public static void DisplayLabelChangesDoNotChangeDeduplicationKey()
        {
            var firstPayload = OpinionPayload();
            var secondPayload = OpinionPayload();
            secondPayload["target_name"] = "Renamed counterpart";
            secondPayload["display_name_at_event"] = "Renamed perspective owner";

            var first = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                firstPayload);
            var second = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                secondPayload);

            TestAssert.Equal(
                first.Envelope.DeduplicationKey,
                second.Envelope.DeduplicationKey,
                "Display-label changes cannot enter the deduplication key.");
        }

        public static void PayloadChangesDoChangeDeduplicationKey()
        {
            var firstPayload = OpinionPayload();
            var secondPayload = OpinionPayload();
            secondPayload["opinion_after"] = "30";
            secondPayload["opinion_delta"] = "30";

            var first = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                firstPayload);
            var second = Project(
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                secondPayload);

            TestAssert.False(
                string.Equals(
                    first.Envelope.DeduplicationKey,
                    second.Envelope.DeduplicationKey,
                    StringComparison.Ordinal),
                "A material factual payload change must change the deduplication key.");
        }

        public static void FailedOrDisabledAdmissionEmitsNoEnvelope()
        {
            var capture = new ReadOnlySocialEventEnvelopeCapture();
            var failedJournal = capture.TryCapture(
                false,
                true,
                SourceEventId,
                900,
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload(),
                ActorId,
                TargetId);
            var failedLedger = capture.TryCapture(
                true,
                false,
                SourceEventId,
                900,
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload(),
                ActorId,
                TargetId);

            TestAssert.True(
                failedJournal is null && failedLedger is null,
                "A failed or disabled journal/ledger admission path must emit no envelope.");
        }

        public static void IdenticalSourceEventCannotNotifyTwice()
        {
            var capture = new ReadOnlySocialEventEnvelopeCapture();
            var first = capture.TryCapture(
                true,
                true,
                SourceEventId,
                900,
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload(),
                ActorId,
                TargetId);
            var duplicate = capture.TryCapture(
                true,
                true,
                SourceEventId,
                900,
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload(),
                ActorId,
                TargetId);

            TestAssert.True(first is not null, "The first admitted source event must notify.");
            TestAssert.True(
                duplicate is null,
                "An identical source EventId cannot produce a second session notification.");
        }

        public static void ProjectionPreservesCanonicalFingerprint()
        {
            var now = new DateTimeOffset(2026, 7, 28, 1, 0, 0, TimeSpan.Zero);
            var actor = CreateIndividual("Perspective owner");
            var target = CreateIndividual("Counterpart");
            var archive = new IdentityArchiveSnapshot(
                Guid.Parse("c3000000-0000-0000-0000-000000000001"),
                1,
                now,
                new[]
                {
                    new PersistedIdentityRecord("Thing_Actor", actor),
                    new PersistedIdentityRecord("Thing_Target", target)
                });
            var binding = new EnvironmentBinding(
                actor.Id,
                EnvironmentBindingState.Bound,
                "Thing_Actor",
                900);
            var ledger = new InMemoryEventLedger();
            var memories = new Dagmay.Core.Memory.MemoryIndex();
            var reflections = new PersistentReflectionQueue(8);
            var before = CanonicalStateFingerprint.Compute(
                archive,
                binding,
                ledger.Snapshot(),
                memories.Snapshot(),
                reflections.Snapshot());

            var projection = ReadOnlySocialEventEnvelopeAdapter.TryProject(
                SourceEventId,
                900,
                ReadOnlySocialEventEnvelopeAdapter.OpinionChanged,
                OpinionPayload(),
                actor.Id,
                target.Id);

            var after = CanonicalStateFingerprint.Compute(
                archive,
                binding,
                ledger.Snapshot(),
                memories.Snapshot(),
                reflections.Snapshot());
            TestAssert.True(projection is not null, "The supported fixture must project.");
            TestAssert.Equal(
                before,
                after,
                "Building the read-only envelope must preserve the canonical fingerprint.");
        }

        private static ReadOnlyRimWorldEventProjection Project(
            string eventKind,
            IDictionary<string, string> payload,
            long tick = 900,
            IndividualId? actorId = null,
            IndividualId? targetId = null)
        {
            var projection = ReadOnlySocialEventEnvelopeAdapter.TryProject(
                SourceEventId,
                tick,
                eventKind,
                payload,
                actorId ?? ActorId,
                targetId ?? TargetId);
            TestAssert.True(projection is not null, "The supported social fixture must project.");
            return projection!;
        }

        private static Dictionary<string, string> OpinionPayload() =>
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target_external_id"] = "Thing_Target",
                ["target_name"] = "Counterpart",
                ["opinion_before"] = "0",
                ["opinion_after"] = "20",
                ["opinion_delta"] = "20",
                ["pawn_external_id"] = "Thing_Actor",
                ["display_name_at_event"] = "Perspective owner"
            };

        private static Dictionary<string, string> RelationshipPayload() =>
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target_external_id"] = "Thing_Target",
                ["target_name"] = "Counterpart",
                ["relations_before"] = "none",
                ["relations_after"] = "Friend",
                ["pawn_external_id"] = "Thing_Actor",
                ["display_name_at_event"] = "Perspective owner"
            };

        private static IndividualState CreateIndividual(string displayName) =>
            IndividualState.Create(
                displayName,
                new IdentitySeed(
                    "rimworld",
                    "0.3-shadow-event-adapter",
                    new[]
                    {
                        new SeedFact(
                            SeedFactCategory.Trait,
                            "kind",
                            "Kind",
                            "RimWorld trait",
                            1.0)
                    }));
    }
}
