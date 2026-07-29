using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Contracts;
using Dagmay.Core.Decisions;
using Dagmay.Core.Presentation;

namespace Dagmay.Tests
{
    internal static class Mosaic03BoundedReactionLifecycleContractTests
    {
        private static readonly IndividualId Owner =
            IndividualId.Parse("c3000000000000000000000000000001");
        private static readonly IndividualId Counterpart =
            IndividualId.Parse("c3000000000000000000000000000002");
        private const string Session = "v38-session";
        private const string Store = "v38-store";
        private const long Generation = 7;

        public static void SourceRejectsDefaultForeignAndUngroundedIdentity()
        {
            var decision = GroundedDecision();
            TestAssert.Throws<ArgumentException>(
                () => BoundedReactionSource.FromGroundedDecision(
                    decision,
                    Session,
                    "conversation",
                    "utterance",
                    Counterpart.ToString(),
                    null,
                    new[] { Counterpart.ToString() },
                    Hash("text"),
                    Generation,
                    100),
                "A foreign speaker cannot claim the grounded owner decision.");
            TestAssert.Throws<ArgumentException>(
                () => BoundedReactionSource.FromGroundedDecision(
                    decision,
                    Session,
                    "conversation",
                    "utterance",
                    Owner.ToString(),
                    null,
                    Array.Empty<string>(),
                    Hash("text"),
                    Generation,
                    100),
                "An unowned audience must fail closed.");
        }

        public static void TicketIdentityIsDeterministicAcrossEquivalentInputOrder()
        {
            var first = NewLifecycle();
            var second = NewLifecycle();
            var forward = NewSource("identity", 100, new[] { "witness-b", Owner.ToString(), "witness-a" });
            var reverse = NewSource("identity", 100, new[] { "witness-a", "witness-b", Owner.ToString() });
            TestAssert.Equal(
                first.Prepare(forward, 500).TicketId,
                second.Prepare(reverse, 500).TicketId,
                "Equivalent source ordering must produce the same ticket ID.");
        }

        public static void TicketCarriesHashOnlyAndExcludesRawDialogue()
        {
            const string transient = "A private transient dialogue line.";
            var source = BoundedReactionSource.FromGroundedDecision(
                GroundedDecision(),
                Session,
                "conversation",
                "hash-only",
                Owner.ToString(),
                null,
                new[] { Owner.ToString() },
                Hash(transient),
                Generation,
                100);
            var ticket = NewLifecycle().Prepare(source, 500);
            TestAssert.Equal(Hash(transient), ticket.Source.ExactTextHash, "The exact transient text hash must survive.");
            TestAssert.False(
                ticket.GetType().GetProperties().Any(property =>
                    string.Equals(property.Name, "Text", StringComparison.Ordinal) ||
                    string.Equals(property.Name, "Dialogue", StringComparison.Ordinal)),
                "The ticket surface cannot expose raw dialogue.");
            TestAssert.False(ticket.Source.Fingerprint.Contains(transient), "The source fingerprint cannot contain raw dialogue.");
        }

        public static void TicketAudienceIsOwnerScopedSortedAndUnique()
        {
            var source = NewSource(
                "audience",
                100,
                new[] { "witness-b", Owner.ToString(), "witness-a", "witness-b" });
            TestAssert.Equal(3, source.IntendedAudienceIds.Count, "Audience IDs must be unique.");
            TestAssert.Equal(Owner.ToString(), source.IntendedAudienceIds[0], "Audience IDs must use ordinal ordering.");
            TestAssert.True(
                source.IntendedAudienceIds.Contains(Owner.ToString()),
                "The private perspective owner must remain in scope.");
        }

        public static void QueueIsGloballyBoundedToEightWithDeterministicEviction()
        {
            var first = NewLifecycle();
            var second = NewLifecycle();
            for (var index = 0; index < 9; index++)
            {
                first.Prepare(NewSource("queue-" + index, 100 + index), 1000);
            }
            for (var index = 8; index >= 0; index--)
            {
                second.Prepare(NewSource("queue-" + index, 100 + index), 1000);
            }
            TestAssert.Equal(BoundedReactionLifecycle.MaximumLiveTickets, first.LiveCount, "Queue must remain globally bounded.");
            TestAssert.Equal(BoundedReactionLifecycle.MaximumLiveTickets, second.LiveCount, "Reverse admission must remain bounded.");
            TestAssert.Equal(
                string.Join(",", first.LiveTickets.Select(value => value.TicketId)),
                string.Join(",", second.LiveTickets.Select(value => value.TicketId)),
                "Deterministic eviction must converge across equivalent admission order.");
        }

        public static void PrivateHistoryProjectionRequiresRecordedPerspectiveOwnership()
        {
            var lifecycle = NewLifecycle();
            var ticket = lifecycle.Prepare(NewSource("private", 100), 500);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
            var receipt = TrustedReceipt(ticket, attempt, "private-success", 120, new[] { Owner.ToString() });
            var projection = lifecycle.ObserveReceipt(receipt, 120);
            TestAssert.True(projection is not null, "Exact observed success must create one projection.");
            TestAssert.Equal(Owner.ToString(), projection!.PerspectiveOwnerId, "Projection must remain owner-scoped.");
            TestAssert.Equal(1, projection.ActualWitnessIds.Count, "Intended witnesses cannot become actual witnesses.");
        }

        public static void ReleaseAttemptIsExplicitlyNotSuccess()
        {
            var lifecycle = NewLifecycle();
            var ticket = lifecycle.Prepare(NewSource("attempt-only", 100), 500);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
            TestAssert.Equal(ObservedDisplayOutcome.ATTEMPT_ONLY, attempt.Outcome, "Attempt state must remain explicit.");
            TestAssert.False(attempt.DeclaresSuccess, "A release attempt can never declare success.");
            TestAssert.Equal(0, lifecycle.SuccessfulProjections.Count, "Beginning an attempt cannot project history.");
        }

        public static void AttemptIdentifiersAreDeterministicAndOrdinal()
        {
            var first = NewLifecycle();
            var firstTicket = first.Prepare(NewSource("attempt-id", 100), 500);
            var firstAttempt = first.BeginAttempt(firstTicket.TicketId, 110);
            var attemptOnly = NonSuccessReceipt(
                firstTicket,
                firstAttempt,
                "attempt-id-one",
                111,
                ObservedDisplayOutcome.ATTEMPT_ONLY);
            first.ObserveReceipt(attemptOnly, 111);
            var schedule = first.ScheduleRetry(firstTicket.TicketId, 112);
            var secondAttempt = first.BeginAttempt(firstTicket.TicketId, schedule.NotBeforeTick);

            var replay = NewLifecycle();
            var replayTicket = replay.Prepare(NewSource("attempt-id", 100), 500);
            var replayAttempt = replay.BeginAttempt(replayTicket.TicketId, 110);
            TestAssert.Equal(firstAttempt.AttemptId, replayAttempt.AttemptId, "Equivalent first attempts must be deterministic.");
            TestAssert.Equal(2, secondAttempt.AttemptOrdinal, "Retry attempt ordinal must advance exactly once.");
            TestAssert.False(
                string.Equals(firstAttempt.AttemptId, secondAttempt.AttemptId, StringComparison.Ordinal),
                "Different attempt ordinals must have different IDs.");
        }

        public static void ObservedReceiptRequiresExactAttemptTicketAndUtterance()
        {
            var lifecycle = NewLifecycle();
            var ticket = lifecycle.Prepare(NewSource("exact-binding", 100), 500);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
            var wrong = TrustedReceipt(
                ticket,
                attempt,
                "wrong-utterance",
                120,
                new[] { Owner.ToString() },
                utteranceId: "foreign-utterance");
            TestAssert.Throws<ArgumentException>(
                () => lifecycle.ObserveReceipt(wrong, 120),
                "A receipt for another utterance must fail closed.");
        }

        public static void ObservedReceiptRequiresTrustedSourceContractAndGateDigest()
        {
            var lifecycle = NewLifecycle();
            var ticket = lifecycle.Prepare(NewSource("trusted-source", 100), 500);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
            var wrongContract = TrustedReceipt(
                ticket,
                attempt,
                "wrong-contract",
                120,
                new[] { Owner.ToString() },
                sourceContract: "Foreign.Contract.v1");
            TestAssert.Throws<ArgumentException>(
                () => lifecycle.ObserveReceipt(wrongContract, 120),
                "A foreign source contract must fail closed.");
            var wrongDigest = TrustedReceipt(
                ticket,
                attempt,
                "wrong-digest",
                120,
                new[] { Owner.ToString() },
                sourceGateDigest: new string('a', 64));
            TestAssert.Throws<ArgumentException>(
                () => lifecycle.ObserveReceipt(wrongDigest, 120),
                "A foreign gate digest must fail closed.");
        }

        public static void ObservedReceiptFingerprintCoversEverySemanticField()
        {
            var lifecycle = NewLifecycle();
            var ticket = lifecycle.Prepare(NewSource("fingerprint", 100), 500);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
            var first = TrustedReceipt(ticket, attempt, "fingerprint-a", 120, new[] { Owner.ToString() });
            var changed = TrustedReceipt(ticket, attempt, "fingerprint-a", 121, new[] { Owner.ToString() });
            TestAssert.False(
                string.Equals(first.ReceiptFingerprint, changed.ReceiptFingerprint, StringComparison.Ordinal),
                "Changing a semantic field must change the receipt fingerprint.");
            TestAssert.Equal(64, first.ReceiptFingerprint.Length, "Receipt fingerprint must be SHA-256.");
        }

        public static void ObservedReceiptRequiresActualSortedAudienceAndSpeakerWitness()
        {
            var lifecycle = NewLifecycle();
            var ticket = lifecycle.Prepare(
                NewSource("actual-audience", 100, new[] { Owner.ToString(), "witness-a", "witness-b" }),
                500);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
            var receipt = TrustedReceipt(
                ticket,
                attempt,
                "actual-audience",
                120,
                new[] { "witness-b", Owner.ToString(), "witness-a" });
            TestAssert.Equal(Owner.ToString(), receipt.AudienceIds[0], "Actual audience must be canonically ordered.");
            TestAssert.Throws<TargetInvocationException>(
                () => TrustedReceipt(
                    ticket,
                    attempt,
                    "missing-speaker",
                    120,
                    new[] { "witness-a" }),
                "A trusted receipt without the speaker witness must fail closed.");
        }

        public static void AttemptOnlyAndFailedOutcomesNeverCompletePresentation()
        {
            foreach (var outcome in new[] { ObservedDisplayOutcome.ATTEMPT_ONLY, ObservedDisplayOutcome.FAILED })
            {
                var lifecycle = NewLifecycle();
                var ticket = lifecycle.Prepare(NewSource("non-success-" + outcome, 100), 500);
                var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
                var receipt = NonSuccessReceipt(ticket, attempt, "non-success-" + outcome, 120, outcome);
                TestAssert.True(
                    lifecycle.ObserveReceipt(receipt, 120) is null,
                    "Non-success outcomes cannot create a projection.");
                TestAssert.Equal(0, lifecycle.SuccessfulProjections.Count, "Non-success cannot complete presentation.");
            }
        }

        public static void ExactSuccessfulDuplicateIsIdempotent()
        {
            var lifecycle = NewLifecycle();
            var ticket = lifecycle.Prepare(NewSource("duplicate", 100), 500);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
            var receipt = TrustedReceipt(ticket, attempt, "duplicate", 120, new[] { Owner.ToString() });
            var first = lifecycle.ObserveReceipt(receipt, 120);
            var second = lifecycle.ObserveReceipt(receipt, 121);
            TestAssert.Equal(first!.ProjectionFingerprint, second!.ProjectionFingerprint, "Exact duplicate success must be idempotent.");
            TestAssert.Equal(1, lifecycle.SuccessfulProjections.Count, "Exact replay cannot project twice.");
        }

        public static void ConflictingDuplicateReceiptFailsClosed()
        {
            var lifecycle = NewLifecycle();
            var ticket = lifecycle.Prepare(NewSource("conflicting", 100), 500);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
            var first = TrustedReceipt(ticket, attempt, "conflicting-id", 120, new[] { Owner.ToString() });
            lifecycle.ObserveReceipt(first, 120);
            var conflict = TrustedReceipt(ticket, attempt, "conflicting-id", 121, new[] { Owner.ToString() });
            TestAssert.Throws<ArgumentException>(
                () => lifecycle.ObserveReceipt(conflict, 121),
                "Same receipt ID with changed semantics must fail closed.");
        }

        public static void ForeignOwnerSessionConversationOrCheckpointFailsClosed()
        {
            var lifecycle = NewLifecycle();
            TestAssert.Throws<ArgumentException>(
                () => lifecycle.Prepare(
                    BoundedReactionSource.FromGroundedDecision(
                        GroundedDecision(),
                        "foreign-session",
                        "conversation",
                        "foreign",
                        Owner.ToString(),
                        null,
                        new[] { Owner.ToString() },
                        Hash("foreign"),
                        Generation,
                        100),
                    500),
                "A foreign session source must fail closed.");
            var ticket = lifecycle.Prepare(NewSource("foreign-receipt", 100), 500);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
            var foreignConversation = TrustedReceipt(
                ticket,
                attempt,
                "foreign-conversation",
                120,
                new[] { Owner.ToString() },
                conversationId: "foreign");
            TestAssert.Throws<ArgumentException>(
                () => lifecycle.ObserveReceipt(foreignConversation, 120),
                "A foreign conversation must fail closed.");
            var foreignCheckpoint = TrustedReceipt(
                ticket,
                attempt,
                "foreign-checkpoint",
                120,
                new[] { Owner.ToString() },
                checkpointGeneration: Generation + 1);
            TestAssert.Throws<ArgumentException>(
                () => lifecycle.ObserveReceipt(foreignCheckpoint, 120),
                "A foreign checkpoint must fail closed.");
        }

        public static void StaleFutureAndOutOfOrderReceiptsFailClosed()
        {
            var lifecycle = NewLifecycle();
            var ticket = lifecycle.Prepare(NewSource("chronology", 100), 500);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 120);
            var stale = TrustedReceipt(ticket, attempt, "stale", 119, new[] { Owner.ToString() });
            TestAssert.Throws<ArgumentException>(
                () => lifecycle.ObserveReceipt(stale, 130),
                "Receipt before the release request must fail closed.");
            var future = TrustedReceipt(ticket, attempt, "future", 140, new[] { Owner.ToString() });
            TestAssert.Throws<ArgumentException>(
                () => lifecycle.ObserveReceipt(future, 130),
                "Receipt beyond the caller tick must fail closed.");
            var foreignAttempt = TrustedReceipt(
                ticket,
                attempt,
                "out-of-order",
                130,
                new[] { Owner.ToString() },
                releaseAttemptId: new string('e', 64));
            TestAssert.Throws<ArgumentException>(
                () => lifecycle.ObserveReceipt(foreignAttempt, 130),
                "Receipt for a non-active attempt must fail closed.");
        }

        public static void RetryScheduleIsCallerTickDrivenBoundedAndDeterministic()
        {
            var first = RetryLifecycle("retry");
            var second = RetryLifecycle("retry");
            var firstSchedule = first.Lifecycle.ScheduleRetry(first.Ticket.TicketId, 125);
            var secondSchedule = second.Lifecycle.ScheduleRetry(second.Ticket.TicketId, 125);
            TestAssert.Equal(firstSchedule.NotBeforeTick, secondSchedule.NotBeforeTick, "Retry scheduling must be deterministic.");
            TestAssert.Equal(135L, firstSchedule.NotBeforeTick, "First retry delay must derive only from caller tick.");
            TestAssert.Throws<InvalidOperationException>(
                () => first.Lifecycle.BeginAttempt(first.Ticket.TicketId, 134),
                "A retry before the bounded delay must fail closed.");
        }

        public static void ExpiredTicketCannotBeRevivedByLateReceipt()
        {
            var lifecycle = NewLifecycle();
            var ticket = lifecycle.Prepare(NewSource("expiry", 100), 115);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
            TestAssert.Equal(1, lifecycle.Expire(116), "Caller-driven expiry must terminalize the ticket.");
            var receipt = TrustedReceipt(ticket, attempt, "late", 115, new[] { Owner.ToString() });
            TestAssert.Throws<ArgumentException>(
                () => lifecycle.ObserveReceipt(receipt, 116),
                "A late receipt cannot revive an expired ticket.");
            TestAssert.Equal(0, lifecycle.SuccessfulProjections.Count, "Expiry cannot become success.");
        }

        public static void OrphanRecoveryCannotManufactureSuccess()
        {
            var lifecycle = NewLifecycle();
            var ticket = lifecycle.Prepare(NewSource("orphan", 100), 500);
            lifecycle.BeginAttempt(ticket.TicketId, 110);
            lifecycle.RecoverOrphan(ticket.TicketId, 120);
            TestAssert.Equal(0, lifecycle.LiveCount, "Orphan recovery must remove the unproven in-flight record.");
            TestAssert.Equal(0, lifecycle.SuccessfulProjections.Count, "Orphan recovery cannot manufacture success.");
        }

        public static void SnapshotRoundTripPreservesRecoverableInflightState()
        {
            var lifecycle = NewLifecycle();
            var ticket = lifecycle.Prepare(NewSource("snapshot", 100), 500);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
            var snapshot = lifecycle.Checkpoint();
            var decoded = BoundedReactionLifecycleSnapshot.Decode(snapshot.Serialized);
            var restored = BoundedReactionLifecycle.Restore(decoded, Store, Owner.ToString(), Session, Generation);
            TestAssert.Equal(snapshot.Serialized, restored.Checkpoint().Serialized, "Snapshot round trip must preserve exact bytes.");
            var receipt = TrustedReceipt(ticket, attempt, "snapshot-success", 120, new[] { Owner.ToString() });
            TestAssert.True(restored.ObserveReceipt(receipt, 120) is not null, "Recovered in-flight state must accept its exact receipt.");
        }

        public static void CorruptTruncatedAndUnsupportedSnapshotsFailClosed()
        {
            var lifecycle = NewLifecycle();
            lifecycle.Prepare(NewSource("corruption", 100), 500);
            var serialized = lifecycle.Checkpoint().Serialized;
            TestAssert.Throws<ArgumentException>(
                () => BoundedReactionLifecycleSnapshot.Decode(serialized.Substring(0, serialized.Length - 5)),
                "Truncated snapshot must fail closed.");
            var corrupted = serialized.Replace("MOSAIC-BRL", "MOSAIC-BRX");
            TestAssert.Throws<ArgumentException>(
                () => BoundedReactionLifecycleSnapshot.Decode(corrupted),
                "Corrupted snapshot must fail integrity.");
            var unsupported = serialized.Replace("MOSAIC-BRL|1|", "MOSAIC-BRL|2|");
            TestAssert.Throws<ArgumentException>(
                () => BoundedReactionLifecycleSnapshot.Decode(unsupported),
                "Unsupported snapshot version must fail closed.");
        }

        public static void CheckpointRollbackCannotApplyFutureLifecycleState()
        {
            var lifecycle = NewLifecycle();
            lifecycle.Prepare(NewSource("rollback", 100), 500);
            var snapshot = lifecycle.Checkpoint();
            TestAssert.Throws<ArgumentException>(
                () => BoundedReactionLifecycle.Restore(snapshot, Store, Owner.ToString(), Session, Generation - 1),
                "An older save checkpoint cannot apply future lifecycle state.");
        }

        public static void SaveReloadAtEveryLifecycleBoundaryReplaysIdentically()
        {
            var prepared = NewLifecycle();
            var ticket = prepared.Prepare(NewSource("all-boundaries", 100), 500);
            prepared = Reload(prepared);
            var attempt = prepared.BeginAttempt(ticket.TicketId, 110);
            prepared = Reload(prepared);
            var attemptOnly = NonSuccessReceipt(ticket, attempt, "all-boundaries-attempt", 120, ObservedDisplayOutcome.ATTEMPT_ONLY);
            prepared.ObserveReceipt(attemptOnly, 120);
            prepared = Reload(prepared);
            var schedule = prepared.ScheduleRetry(ticket.TicketId, 125);
            prepared = Reload(prepared);
            var secondAttempt = prepared.BeginAttempt(ticket.TicketId, schedule.NotBeforeTick);
            prepared = Reload(prepared);
            var success = TrustedReceipt(ticket, secondAttempt, "all-boundaries-success", 150, new[] { Owner.ToString() });
            var projection = prepared.ObserveReceipt(success, 150);
            var completed = Reload(prepared);
            TestAssert.Equal(1, completed.SuccessfulProjections.Count, "Save/reload at every boundary must converge on one success.");
            TestAssert.Equal(
                projection!.ProjectionFingerprint,
                completed.SuccessfulProjections[0].ProjectionFingerprint,
                "Final projection must replay identically.");
        }

        public static void FiftyThousandAdmissionsRemainBoundedAndDeterministic()
        {
            var first = NewLifecycle();
            var second = NewLifecycle();
            for (var index = 0; index < 50_000; index++)
            {
                var source = NewSource("stress-" + index.ToString("D5", CultureInfo.InvariantCulture), 100 + index);
                first.Prepare(source, 100_100);
                second.Prepare(source, 100_100);
                if (first.LiveCount > BoundedReactionLifecycle.MaximumLiveTickets)
                    throw new InvalidOperationException("Live queue exceeded its hard bound.");
            }
            TestAssert.Equal(8, first.LiveCount, "Stress queue must remain bounded to eight.");
            TestAssert.True(
                first.TombstoneCount <= BoundedReactionLifecycle.MaximumTerminalRecords,
                "Terminal compaction must remain bounded.");
            TestAssert.Equal(
                first.Checkpoint().SnapshotHash,
                second.Checkpoint().SnapshotHash,
                "Fifty thousand deterministic admissions must reproduce exact state.");
        }

        public static void AuthorityFirewallExcludesProviderCanonicalUiAndPawnControl()
        {
            var lifecycle = NewLifecycle();
            TestAssert.False(lifecycle.ProviderCallAuthority, "Lifecycle cannot call a provider.");
            TestAssert.False(lifecycle.CanonicalMutationAuthority, "Lifecycle cannot mutate canonical cognition.");
            TestAssert.False(lifecycle.UiRenderingAuthority, "Lifecycle cannot render UI.");
            TestAssert.False(lifecycle.PawnActionAuthority, "Lifecycle cannot control pawns.");
            var forbiddenTypeFragments = new[] { "Verse.", "UnityEngine.", "Harmony", "HttpClient", "Provider" };
            var referenced = typeof(BoundedReactionLifecycle).Assembly.GetReferencedAssemblies()
                .Select(value => value.Name ?? string.Empty)
                .ToArray();
            TestAssert.False(
                referenced.Any(name => forbiddenTypeFragments.Any(fragment =>
                    name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)),
                "Core lifecycle assembly cannot gain environment or provider authority.");
            var successFactory = typeof(ObservedDisplayReceipt).GetMethod(
                "CreateTrusted",
                BindingFlags.Static | BindingFlags.NonPublic);
            TestAssert.True(successFactory is not null && !successFactory.IsPublic, "Observed success factory must remain internal.");
        }

        private static BoundedReactionLifecycle NewLifecycle() =>
            new BoundedReactionLifecycle(Store, Owner.ToString(), Session, Generation);

        private static BoundedReactionSource NewSource(
            string suffix,
            long createdTick,
            IEnumerable<string>? audience = null) =>
            BoundedReactionSource.FromGroundedDecision(
                GroundedDecision(),
                Session,
                "conversation-" + suffix,
                "utterance-" + suffix,
                Owner.ToString(),
                null,
                audience ?? new[] { Owner.ToString() },
                Hash("transient-" + suffix),
                Generation,
                createdTick);

        private static GroundedSocialAppraisalProposal GroundedDecision()
        {
            var eventId = EventId.Parse("c4000000000000000000000000000001");
            const long tick = 90;
            var payload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target_external_id"] = "Thing_Target",
                ["opinion_before"] = "0",
                ["opinion_after"] = "20",
                ["opinion_delta"] = "20",
                ["pawn_external_id"] = "Thing_Owner"
            };
            var key = GroundedSocialProjectionHash.ComputePr10DeduplicationKey(
                eventId,
                GroundedSocialProjectionHash.OpinionChanged,
                tick,
                Owner,
                Counterpart,
                payload);
            var envelope = new RimWorldEventEvidenceEnvelope(
                eventId,
                GroundedSocialProjectionHash.OpinionChanged,
                tick,
                Owner,
                Counterpart,
                new[] { Counterpart, Owner },
                RimWorldEvidenceOutcomeState.Observed,
                RimWorldEvidencePrivacyDomain.RelationshipPrivate,
                GroundedSocialProjectionHash.SourceAdapter,
                key);
            var evidence = new GroundedSocialSemanticEvidence(
                envelope,
                1,
                new string('a', 64),
                payload);
            return GroundedSocialAppraisalPolicy.Appraise(
                GroundedSocialObservationBundler.Bundle(new[] { evidence })[0]);
        }

        private static ObservedDisplayReceipt TrustedReceipt(
            PresentationTicket ticket,
            ReleaseAttempt attempt,
            string receiptSeed,
            long displayedTick,
            IEnumerable<string> audience,
            string? utteranceId = null,
            string? conversationId = null,
            long? checkpointGeneration = null,
            string? sourceContract = null,
            string? sourceGateDigest = null,
            string? releaseAttemptId = null)
        {
            var method = typeof(ObservedDisplayReceipt).GetMethod(
                "CreateTrusted",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Trusted receipt factory is missing.");
            return (ObservedDisplayReceipt)(method.Invoke(
                null,
                new object?[]
                {
                    ticket.Source.SessionId,
                    releaseAttemptId ?? attempt.AttemptId,
                    Hash(receiptSeed),
                    utteranceId ?? ticket.Source.UtteranceId,
                    conversationId ?? ticket.Source.ConversationId,
                    ticket.Source.SpeakerId,
                    ticket.Source.RecipientId,
                    audience,
                    ticket.Source.ExactTextHash,
                    displayedTick,
                    checkpointGeneration ?? ticket.Source.CheckpointGeneration,
                    ObservedDisplayOutcome.OBSERVED_SUCCESS,
                    sourceContract ?? ObservedDisplayReceipt.SourceContractValue,
                    sourceGateDigest ?? ObservedDisplayReceipt.SourceGateDigestValue
                }) ?? throw new InvalidOperationException("Trusted receipt factory returned null."));
        }

        private static ObservedDisplayReceipt NonSuccessReceipt(
            PresentationTicket ticket,
            ReleaseAttempt attempt,
            string receiptSeed,
            long displayedTick,
            ObservedDisplayOutcome outcome) =>
            ObservedDisplayReceipt.CreateNonSuccess(
                ticket.Source.SessionId,
                attempt.AttemptId,
                Hash(receiptSeed),
                ticket.Source.UtteranceId,
                ticket.Source.ConversationId,
                ticket.Source.SpeakerId,
                ticket.Source.RecipientId,
                new[] { ticket.Source.SpeakerId },
                ticket.Source.ExactTextHash,
                displayedTick,
                ticket.Source.CheckpointGeneration,
                outcome,
                ObservedDisplayReceipt.SourceContractValue,
                ObservedDisplayReceipt.SourceGateDigestValue);

        private static (BoundedReactionLifecycle Lifecycle, PresentationTicket Ticket) RetryLifecycle(string suffix)
        {
            var lifecycle = NewLifecycle();
            var ticket = lifecycle.Prepare(NewSource(suffix, 100), 500);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
            lifecycle.ObserveReceipt(
                NonSuccessReceipt(ticket, attempt, suffix + "-attempt-only", 120, ObservedDisplayOutcome.ATTEMPT_ONLY),
                120);
            return (lifecycle, ticket);
        }

        private static BoundedReactionLifecycle Reload(BoundedReactionLifecycle lifecycle)
        {
            var decoded = BoundedReactionLifecycleSnapshot.Decode(lifecycle.Checkpoint().Serialized);
            return BoundedReactionLifecycle.Restore(decoded, Store, Owner.ToString(), Session, Generation);
        }

        private static string Hash(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
                return string.Concat(bytes.Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }
    }
}
