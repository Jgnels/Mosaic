using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Development;
using Dagmay.Core.Presentation;

namespace Dagmay.IntegrationHarness
{
    internal static partial class IntegrationScenarios
    {
        private static Task<ScenarioExecution> ContextualProvisionalReactionAsync()
        {
            var assertions = new HarnessAssert();
            var checkpoint = V41Checkpoint();
            var canonicalStores = new DurableCanonicalStoreSet(V41Save, V41World, V41Store);
            var affectBefore = CanonicalAffectState.Create(V41Owner, ProvisionalAffectDelta.Zero, 3);
            var relationshipBefore = CanonicalRelationshipState.Create(
                V41Owner,
                V41Counterpart,
                new CanonicalRelationshipVector(trust: 0.25m, affection: 0.10m),
                5);
            canonicalStores.SeedAffect(affectBefore);
            canonicalStores.SeedRelationship(relationshipBefore);

            var proposalRequest = V44ProposalRequest(checkpoint.CheckpointHash);
            var proposal = new GroundedCompoundAppraisalSynthesizer().Synthesize(proposalRequest);
            assertions.True(proposal.VerifyFingerprint(),
                "The exact v43 grounded compound proposal publicly self-verifies before v44 admission.");
            assertions.Equal(GroundedCompoundAppraisalSource.Authority, proposal.Authority,
                "The v43 proposal remains inert evidence without mutation authority.");

            var proposalTrust = new V43ProposalTrustRegistry(V41Save, V41World, V41Store);
            var dialogueTrust = new DialogueAdmissionTrustRegistry(V41Save, V41World, V41Store);
            var pathClaims = new ReactionPathClaimRegistry();
            var attestation = proposalTrust.Attest(proposalRequest, proposal);
            var display = V44ObservedDisplay();
            assertions.Equal(ObservedDisplayOutcome.OBSERVED_SUCCESS, display.Outcome,
                "A genuine trusted display receipt supplies observed success rather than an attempt claim.");
            assertions.True(display.AudienceIds.Contains(V41Owner, StringComparer.Ordinal),
                "The perspective owner is an actual witnessed display participant.");
            var dialogueReceipt = dialogueTrust.Create(
                proposal.CurrentEventId,
                proposal.CurrentEventHash,
                display,
                V44DisplayedTick + 1,
                checkpoint.CheckpointHash);
            var request = new ContextualAdmissionRequest(
                V44Session,
                proposal,
                attestation,
                display,
                dialogueReceipt,
                affectBefore.Fingerprint,
                affectBefore.Version,
                relationshipBefore.Fingerprint,
                relationshipBefore.Version);
            var store = new ContextualProvisionalReactionStore(
                V44Session,
                V41Save,
                V41World,
                V41Store,
                V41Generation,
                checkpoint.CheckpointHash,
                proposalTrust,
                dialogueTrust,
                pathClaims);

            var attempt = store.PrepareAttempt(request);
            assertions.Equal(ContextualAdmissionOutcome.AttemptOnly, attempt.Outcome,
                "Preparing the exact trusted chain produces ATTEMPT_ONLY and does not claim admission success.");
            assertions.Equal(0, store.ActiveCount(),
                "ATTEMPT_ONLY creates no live provisional reaction.");
            assertions.True(attempt.AffectOverlay.Count > 0 && attempt.RelationshipOverlay.Count > 0,
                "The attempt carries deterministic bounded affect and relationship overlays from the v43 proposal.");
            assertions.Equal(proposal.Fingerprint, attempt.ProposalFingerprint,
                "The attempt preserves the exact trusted v43 proposal fingerprint.");
            assertions.True(proposalTrust.Count == 1 && dialogueTrust.Count == 1 && pathClaims.Count == 0,
                "Preparing preserves one-use trust evidence and claims no reaction path before observed admission.");

            var admitted = store.Admit(request, attempt, V44DisplayedTick + 2);
            var prepared = admitted.Item1;
            var admissionReceipt = admitted.Item2;
            store.VerifyReceipt(admissionReceipt);
            assertions.Equal(ContextualAdmissionOutcome.ObservedSuccess, admissionReceipt.Outcome,
                "Store-observed admission emits OBSERVED_SUCCESS only after the exact request and attempt are verified.");
            assertions.Equal(ContextualReactionState.Prepared, prepared.State,
                "Observed admission creates one PREPARED session-local reaction.");
            assertions.True(
                prepared.AttemptFingerprint == attempt.Fingerprint &&
                prepared.AdmissionReceiptFingerprint == admissionReceipt.Fingerprint &&
                admissionReceipt.AttemptId == attempt.AttemptId,
                "The prepared reaction and receipt bind the exact full attempt and each other.");
            assertions.True(proposalTrust.Count == 0 && dialogueTrust.Count == 0 && pathClaims.Count == 1,
                "Successful admission consumes both one-use trust records and holds exactly one reaction-path claim.");

            assertions.Equal(ContextualProvisionalAdmissionSource.Authority, prepared.Authority,
                "The prepared reaction explicitly carries no durable, canonical, provider, planner, UI, or pawn authority.");
            assertions.Equal(ContextualProvisionalAdmissionSource.ReceiptAuthority, admissionReceipt.Authority,
                "The admission receipt is evidence only and carries no canonical or pawn authority.");
            var forbiddenAuthorityMethods = typeof(ContextualProvisionalReactionStore)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => new[] { "Persist", "Restore", "Snapshot", "Canonical", "Pawn", "Job", "Planner" }
                    .Any(fragment => method.Name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(method => method.Name)
                .ToArray();
            assertions.Equal(0, forbiddenAuthorityMethods.Length,
                "The live v44 store exposes no persistence, canonical, pawn, job, or planner authority surface.");
            assertions.Equal(affectBefore.Fingerprint, canonicalStores.Affect(V41Owner).Fingerprint,
                "PREPARED leaves canonical affect unchanged.");
            assertions.Equal(relationshipBefore.Fingerprint,
                canonicalStores.Relationship(V41Owner, V41Counterpart)?.Fingerprint,
                "PREPARED leaves canonical relationship state unchanged.");

            var active = store.Activate(prepared.ReactionId, V44DisplayedTick + 2);
            assertions.Equal(ContextualReactionState.Active, active.State,
                "The admitted PREPARED reaction advances to ACTIVE inside its display-anchored lifetime.");
            assertions.Equal(1, store.ActiveCount(V41Owner),
                "ACTIVE retains exactly one bounded owner-local overlay.");
            assertions.Equal(active.ReactionId, store.Activate(active.ReactionId, V44DisplayedTick + 3).ReactionId,
                "ACTIVE activation replay is idempotent.");

            var expired = store.Expire(active.ExpiresTick);
            assertions.Equal(1, expired,
                "Expiry terminalizes the active reaction at the exact display-anchored lifetime boundary.");
            assertions.True(store.ActiveCount() == 0 && pathClaims.Count == 0,
                "Expiry removes the live overlay and releases its exact reaction-path claim.");
            var diagnostic = store.DiagnosticProjection();
            assertions.True(
                diagnostic.TerminalCounts.TryGetValue("EXPIRED", out var expiredCount) && expiredCount == 1,
                "Bounded diagnostics retain one hashed terminal expiry count without the full reaction.");
            assertions.True(diagnostic.SeenCount == 1 && diagnostic.SeenEventCount == 1,
                "Expiry preserves bounded utterance and canonical-event replay protection.");

            var receiptWasDiscarded = false;
            try
            {
                store.VerifyReceipt(admissionReceipt);
            }
            catch (InvalidOperationException)
            {
                receiptWasDiscarded = true;
            }
            assertions.True(receiptWasDiscarded,
                "Expiry discards the trusted live receipt object rather than retaining a reusable authority token.");
            assertions.Equal(affectBefore.Fingerprint, canonicalStores.Affect(V41Owner).Fingerprint,
                "The complete PREPARED-to-ACTIVE-to-expiry lifecycle leaves canonical affect unchanged.");
            assertions.Equal(relationshipBefore.Fingerprint,
                canonicalStores.Relationship(V41Owner, V41Counterpart)?.Fingerprint,
                "The complete lifecycle leaves canonical relationship state unchanged.");

            return Task.FromResult(new ScenarioExecution(
                assertions.Assertions,
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    ["trustedV43AttestationsAfterAdmission"] = proposalTrust.Count,
                    ["trustedDialogueReceiptsAfterAdmission"] = dialogueTrust.Count,
                    ["activeAfterExpiry"] = store.ActiveCount(),
                    ["reactionPathClaimsAfterExpiry"] = pathClaims.Count,
                    ["seenUtterances"] = diagnostic.SeenCount,
                    ["seenCanonicalEvents"] = diagnostic.SeenEventCount,
                    ["expired"] = expiredCount
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["proposalFingerprint"] = proposal.Fingerprint,
                    ["attemptFingerprint"] = attempt.Fingerprint,
                    ["admissionReceiptFingerprint"] = admissionReceipt.Fingerprint,
                    ["terminalStateDigest"] = diagnostic.StateDigest,
                    ["authority"] = ContextualProvisionalAdmissionSource.Authority
                }));
        }
    }
}
