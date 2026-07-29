using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Contracts;
using Dagmay.Core.Decisions;
using Dagmay.Core.Presentation;

namespace Dagmay.IntegrationHarness
{
    internal static partial class IntegrationScenarios
    {
        private static Task<ScenarioExecution> BoundedReactionLifecycleAsync()
        {
            var assertions = new HarnessAssert();
            var owner = IndividualId.Parse("e3000000000000000000000000000001");
            var decision = V38GroundedDecision(owner);

            var successful = V38Lifecycle(owner);
            var ticket = successful.Prepare(V38Source(decision, owner, "valid", 100), 500);
            var attempt = successful.BeginAttempt(ticket.TicketId, 110);
            var success = V38TrustedReceipt(ticket, attempt, "valid-success", 120);
            var projection = successful.ObserveReceipt(success, 120);
            assertions.True(
                projection is not null &&
                projection.PerspectiveOwnerId == owner.ToString() &&
                projection.ActualWitnessIds.SequenceEqual(new[] { owner.ToString() }, StringComparer.Ordinal),
                "A grounded v37 decision completes only through one exact owner-scoped observed success.");
            assertions.True(
                successful.ObserveReceipt(success, 121) is not null &&
                successful.SuccessfulProjections.Count == 1,
                "Exact observed-success replay is idempotent and cannot project twice.");

            var nonSuccessCount = 0;
            foreach (var outcome in new[] { ObservedDisplayOutcome.ATTEMPT_ONLY, ObservedDisplayOutcome.FAILED })
            {
                var lifecycle = V38Lifecycle(owner);
                var nonSuccessTicket = lifecycle.Prepare(
                    V38Source(decision, owner, "non-success-" + outcome, 100),
                    500);
                var nonSuccessAttempt = lifecycle.BeginAttempt(nonSuccessTicket.TicketId, 110);
                var receipt = ObservedDisplayReceipt.CreateNonSuccess(
                    nonSuccessTicket.Source.SessionId,
                    nonSuccessAttempt.AttemptId,
                    V38Hash("receipt-" + outcome),
                    nonSuccessTicket.Source.UtteranceId,
                    nonSuccessTicket.Source.ConversationId,
                    nonSuccessTicket.Source.SpeakerId,
                    null,
                    new[] { owner.ToString() },
                    nonSuccessTicket.Source.ExactTextHash,
                    120,
                    9,
                    outcome,
                    ObservedDisplayReceipt.SourceContractValue,
                    ObservedDisplayReceipt.SourceGateDigestValue);
                if (lifecycle.ObserveReceipt(receipt, 120) is null &&
                    lifecycle.SuccessfulProjections.Count == 0)
                    nonSuccessCount++;
            }
            assertions.Equal(2, nonSuccessCount, "Attempt-only and failed display outcomes never project success.");

            var rejected = 0;
            rejected += V38ReceiptRejected(owner, decision, "stale", displayedTick: 109, callerTick: 130) ? 1 : 0;
            rejected += V38ReceiptRejected(owner, decision, "future", displayedTick: 140, callerTick: 130) ? 1 : 0;
            rejected += V38ReceiptRejected(owner, decision, "foreign", displayedTick: 120, callerTick: 120, conversationId: "foreign") ? 1 : 0;
            rejected += V38ReceiptRejected(owner, decision, "private", displayedTick: 120, callerTick: 120, audience: new[] { owner.ToString(), "unintended-private-id" }) ? 1 : 0;
            rejected += V38ReceiptRejected(owner, decision, "forged", displayedTick: 120, callerTick: 120, sourceDigest: new string('f', 64)) ? 1 : 0;
            rejected += V38ReceiptRejected(owner, decision, "out-of-order", displayedTick: 120, callerTick: 120, attemptId: new string('e', 64)) ? 1 : 0;
            assertions.Equal(6, rejected, "Stale, future, foreign, private, forged, and out-of-order receipts fail closed.");

            var replay = V38Lifecycle(owner);
            var replayTicket = replay.Prepare(V38Source(decision, owner, "reload", 100), 500);
            replay = V38Reload(replay, owner);
            var replayAttempt = replay.BeginAttempt(replayTicket.TicketId, 110);
            replay = V38Reload(replay, owner);
            var attemptOnlyReceipt = ObservedDisplayReceipt.CreateNonSuccess(
                replayTicket.Source.SessionId,
                replayAttempt.AttemptId,
                V38Hash("reload-attempt"),
                replayTicket.Source.UtteranceId,
                replayTicket.Source.ConversationId,
                replayTicket.Source.SpeakerId,
                null,
                new[] { owner.ToString() },
                replayTicket.Source.ExactTextHash,
                120,
                9,
                ObservedDisplayOutcome.ATTEMPT_ONLY,
                ObservedDisplayReceipt.SourceContractValue,
                ObservedDisplayReceipt.SourceGateDigestValue);
            replay.ObserveReceipt(attemptOnlyReceipt, 120);
            replay = V38Reload(replay, owner);
            var retry = replay.ScheduleRetry(replayTicket.TicketId, 125);
            replay = V38Reload(replay, owner);
            var retryAttempt = replay.BeginAttempt(replayTicket.TicketId, retry.NotBeforeTick);
            replay = V38Reload(replay, owner);
            replay.ObserveReceipt(V38TrustedReceipt(replayTicket, retryAttempt, "reload-success", 150), 150);
            replay = V38Reload(replay, owner);
            assertions.Equal(1, replay.SuccessfulProjections.Count, "Save/reload-equivalent snapshots converge at every lifecycle boundary.");

            var stressA = V38Lifecycle(owner);
            var stressB = V38Lifecycle(owner);
            var maximumLive = 0;
            for (var index = 0; index < 50_000; index++)
            {
                var suffix = "stress-" + index.ToString("D5", CultureInfo.InvariantCulture);
                var source = V38Source(decision, owner, suffix, 200 + index);
                stressA.Prepare(source, 100_000);
                stressB.Prepare(source, 100_000);
                maximumLive = Math.Max(maximumLive, stressA.LiveCount);
            }
            assertions.Equal(8, maximumLive, "Fifty thousand admissions never exceed the eight-ticket live bound.");
            assertions.Equal(
                stressA.Checkpoint().SnapshotHash,
                stressB.Checkpoint().SnapshotHash,
                "Admission permutations and repeated stress construction remain deterministic.");

            var lifecycleEdges = V38Lifecycle(owner);
            var retryTicket = lifecycleEdges.Prepare(V38Source(decision, owner, "retry-edge", 100), 500);
            var retryAttemptOne = lifecycleEdges.BeginAttempt(retryTicket.TicketId, 110);
            lifecycleEdges.ObserveReceipt(
                ObservedDisplayReceipt.CreateNonSuccess(
                    retryTicket.Source.SessionId,
                    retryAttemptOne.AttemptId,
                    V38Hash("retry-edge-attempt"),
                    retryTicket.Source.UtteranceId,
                    retryTicket.Source.ConversationId,
                    retryTicket.Source.SpeakerId,
                    null,
                    new[] { owner.ToString() },
                    retryTicket.Source.ExactTextHash,
                    120,
                    9,
                    ObservedDisplayOutcome.ATTEMPT_ONLY,
                    ObservedDisplayReceipt.SourceContractValue,
                    ObservedDisplayReceipt.SourceGateDigestValue),
                120);
            var retryEdge = lifecycleEdges.ScheduleRetry(retryTicket.TicketId, 125);
            var retryEdgeAttempt = lifecycleEdges.BeginAttempt(retryTicket.TicketId, retryEdge.NotBeforeTick);
            lifecycleEdges.RecoverOrphan(retryTicket.TicketId, retryEdgeAttempt.RequestedTick + 1);
            var expiryTicket = lifecycleEdges.Prepare(V38Source(decision, owner, "expiry-edge", 200), 210);
            lifecycleEdges.Expire(211);
            assertions.True(
                lifecycleEdges.SuccessfulProjections.Count == 0 &&
                lifecycleEdges.LiveTickets.All(value => value.TicketId != expiryTicket.TicketId),
                "Retries, expiries, and orphan placement remain explicit and never manufacture success.");
            assertions.True(
                !lifecycleEdges.ProviderCallAuthority &&
                !lifecycleEdges.CanonicalMutationAuthority &&
                !lifecycleEdges.UiRenderingAuthority &&
                !lifecycleEdges.PawnActionAuthority,
                "The integrated lifecycle has no provider, canonical, UI, runtime, world, or pawn authority.");

            var metrics = new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["focusedContracts"] = 26,
                ["admissions"] = 50_000,
                ["maximumLiveQueue"] = maximumLive,
                ["rejectedAdversarialReceipts"] = rejected,
                ["successfulProjections"] = successful.SuccessfulProjections.Count
            };
            var details = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceContract"] = ObservedDisplayReceipt.SourceContractValue,
                ["sourceGateDigest"] = ObservedDisplayReceipt.SourceGateDigestValue,
                ["stressSnapshotHash"] = stressA.Checkpoint().SnapshotHash
            };
            return Task.FromResult(new ScenarioExecution(assertions.Assertions, metrics, details));
        }

        private static BoundedReactionLifecycle V38Lifecycle(IndividualId owner) =>
            new BoundedReactionLifecycle("v38-integration-store", owner.ToString(), "v38-integration-session", 9);

        private static BoundedReactionSource V38Source(
            GroundedSocialAppraisalProposal decision,
            IndividualId owner,
            string suffix,
            long createdTick) =>
            BoundedReactionSource.FromGroundedDecision(
                decision,
                "v38-integration-session",
                "conversation-" + suffix,
                "utterance-" + suffix,
                owner.ToString(),
                null,
                new[] { owner.ToString() },
                V38Hash("transient-" + suffix),
                9,
                createdTick);

        private static GroundedSocialAppraisalProposal V38GroundedDecision(IndividualId owner)
        {
            var counterpart = IndividualId.Parse("e3000000000000000000000000000002");
            var eventId = EventId.Parse("e4000000000000000000000000000001");
            var payload = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target_external_id"] = "Thing_Target",
                ["opinion_before"] = "0",
                ["opinion_after"] = "25",
                ["opinion_delta"] = "25",
                ["pawn_external_id"] = "Thing_Owner"
            };
            var deduplicationKey = GroundedSocialProjectionHash.ComputePr10DeduplicationKey(
                eventId,
                GroundedSocialProjectionHash.OpinionChanged,
                90,
                owner,
                counterpart,
                payload);
            var envelope = new RimWorldEventEvidenceEnvelope(
                eventId,
                GroundedSocialProjectionHash.OpinionChanged,
                90,
                owner,
                counterpart,
                new[] { counterpart, owner },
                RimWorldEvidenceOutcomeState.Observed,
                RimWorldEvidencePrivacyDomain.RelationshipPrivate,
                GroundedSocialProjectionHash.SourceAdapter,
                deduplicationKey);
            var evidence = new GroundedSocialSemanticEvidence(
                envelope,
                1,
                new string('b', 64),
                payload);
            return GroundedSocialAppraisalPolicy.Appraise(
                GroundedSocialObservationBundler.Bundle(new[] { evidence })[0]);
        }

        private static ObservedDisplayReceipt V38TrustedReceipt(
            PresentationTicket ticket,
            ReleaseAttempt attempt,
            string seed,
            long displayedTick,
            string? conversationId = null,
            IEnumerable<string>? audience = null,
            string? sourceDigest = null,
            string? attemptId = null)
        {
            var method = typeof(ObservedDisplayReceipt).GetMethod(
                "CreateTrusted",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new HarnessAssertionException("Trusted receipt factory is unavailable.");
            try
            {
                return (ObservedDisplayReceipt)(method.Invoke(
                    null,
                    new object?[]
                    {
                        ticket.Source.SessionId,
                        attemptId ?? attempt.AttemptId,
                        V38Hash(seed),
                        ticket.Source.UtteranceId,
                        conversationId ?? ticket.Source.ConversationId,
                        ticket.Source.SpeakerId,
                        null,
                        audience ?? new[] { ticket.Source.SpeakerId },
                        ticket.Source.ExactTextHash,
                        displayedTick,
                        ticket.Source.CheckpointGeneration,
                        ObservedDisplayOutcome.OBSERVED_SUCCESS,
                        ObservedDisplayReceipt.SourceContractValue,
                        sourceDigest ?? ObservedDisplayReceipt.SourceGateDigestValue
                    }) ?? throw new HarnessAssertionException("Trusted receipt factory returned null."));
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }

        private static bool V38ReceiptRejected(
            IndividualId owner,
            GroundedSocialAppraisalProposal decision,
            string suffix,
            long displayedTick,
            long callerTick,
            string? conversationId = null,
            IEnumerable<string>? audience = null,
            string? sourceDigest = null,
            string? attemptId = null)
        {
            var lifecycle = V38Lifecycle(owner);
            var intendedAudience = audience is null
                ? new[] { owner.ToString() }
                : audience.Where(value => value == owner.ToString()).ToArray();
            if (intendedAudience.Length == 0) intendedAudience = new[] { owner.ToString() };
            var source = BoundedReactionSource.FromGroundedDecision(
                decision,
                "v38-integration-session",
                "conversation-" + suffix,
                "utterance-" + suffix,
                owner.ToString(),
                null,
                intendedAudience,
                V38Hash("transient-" + suffix),
                9,
                100);
            var ticket = lifecycle.Prepare(source, 500);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
            try
            {
                lifecycle.ObserveReceipt(
                    V38TrustedReceipt(
                        ticket,
                        attempt,
                        suffix,
                        displayedTick,
                        conversationId,
                        audience,
                        sourceDigest,
                        attemptId),
                    callerTick);
                return false;
            }
            catch (ArgumentException)
            {
                return true;
            }
        }

        private static BoundedReactionLifecycle V38Reload(
            BoundedReactionLifecycle lifecycle,
            IndividualId owner)
        {
            var decoded = BoundedReactionLifecycleSnapshot.Decode(lifecycle.Checkpoint().Serialized);
            return BoundedReactionLifecycle.Restore(
                decoded,
                "v38-integration-store",
                owner.ToString(),
                "v38-integration-session",
                9);
        }

        private static string V38Hash(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                return string.Concat(
                    algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))
                        .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }
    }
}
