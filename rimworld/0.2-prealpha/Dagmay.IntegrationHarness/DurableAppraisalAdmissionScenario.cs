using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Contracts;
using Dagmay.Core.Presentation;

namespace Dagmay.IntegrationHarness
{
    internal static partial class IntegrationScenarios
    {
        private static Task<ScenarioExecution> DurableAppraisalAdmissionAsync()
        {
            var assertions = new HarnessAssert();
            var owner = IndividualId.Parse("e4000000000000000000000000000001");
            var v37 = V38GroundedDecision(owner);
            var lifecycle = V38Lifecycle(owner);
            var ticket = lifecycle.Prepare(V38Source(v37, owner, "v40-chain", 100), 500);
            var attempt = lifecycle.BeginAttempt(ticket.TicketId, 110);
            var observed = V38TrustedReceipt(ticket, attempt, "v40-observed", 120);
            assertions.True(lifecycle.ObserveReceipt(observed, 120) is not null,
                "v37 evidence must pass through genuine v38 observed-success admission.");

            var canonicalAffect = CanonicalAffectState.Create(
                owner.ToString(), new ProvisionalAffectDelta(valence: 0.10m), 3);
            var canonicalRelationship = CanonicalRelationshipState.Create(
                owner.ToString(), observed.SpeakerId,
                new CanonicalRelationshipVector(trust: 0.20m, affection: 0.10m), 5);
            var v39 = new ProvisionalDialogueAppraisalStore(observed.SessionId);
            var knowledge = new ProvisionalKnowledgeEvidence(
                "ev-v40-chain", owner.ToString(), v37.SourceEvidenceIds[0].ToString(),
                ProvisionalPrivacy.OwnerPrivate, true, 100, ProvisionalFactuality.VerifiedFact);
            var cue = new ProvisionalSpeechCue(
                ProvisionalCueType.Gratitude, 0.70m, true, 0, ProvisionalFactuality.SpeechAct,
                new[] { knowledge.EvidenceId });
            var input = new ProvisionalDialogueAppraisalInput(
                owner.ToString(), observed, new[] { cue }, new[] { knowledge },
                canonicalAffect.Fingerprint, canonicalAffect.Version,
                canonicalRelationship.Fingerprint, canonicalRelationship.Version,
                new ProvisionalTraitProfile());
            var appraisal = v39.Activate(v39.Prepare(input).AppraisalId, observed.DisplayedTick);
            var packet = v39.ProposePromotion(
                appraisal.AppraisalId, "dialogue-event-v40-chain", 9,
                new ProvisionalAffectDelta(valence: 0.01m),
                new ProvisionalRelationshipDelta(trust: 0.01m),
                "admission-receipt-v40-chain");
            assertions.True(!packet.CanonicalMutationAuthority,
                "Corrected v39 remains proposal-only before v40.");

            var stores = new DurableCanonicalStoreSet("save-v40", "world-v40", "stores-v40");
            stores.SeedAffect(canonicalAffect);
            stores.SeedRelationship(canonicalRelationship);
            var outbox = new DurableAppraisalOutbox();
            var registry = new DurableProposalRegistry();
            var coordinator = new CheckpointAlignedDurableAppraisalCoordinator(stores, outbox, registry);
            var envelope = registry.Register(v39, packet);
            var eventReceipt = V40EventReceipt(
                packet.SourceReceiptId, packet.AdmittedDialogueEventId, owner.ToString(),
                observed.SpeakerId, observed.DisplayedTick, 9);
            var checkpoint = V40Checkpoint("checkpoint-v40-chain", 9);
            var request = new DurableAppraisalAdmissionRequest(
                envelope,
                DurableIdentityBinding.Create(
                    owner.ToString(), "lineage-v40-owner", stores.SaveId, stores.WorldId,
                    stores.StoreSetId, 2),
                eventReceipt,
                checkpoint);
            var prepared = coordinator.Prepare(request);
            assertions.True(!prepared.IsCanonicalSuccess && stores.DevelopmentalRecordCount == 0,
                "Prepare returns only an attempt and writes no canonical destination.");
            var evidence = coordinator.Apply(prepared.EntryId, checkpoint);
            assertions.True(
                evidence.IsCanonicalSuccess &&
                stores.DevelopmentalRecordCount == 1 &&
                stores.Affect(owner.ToString()).Version == 4 &&
                stores.Relationship(owner.ToString(), observed.SpeakerId)!.Version == 6,
                "v40 materializes the exact owner-private record and only targeted canonical transitions.");
            var promoted = v39.ObserveApplicationReceipt(evidence.ApplicationReceipt);
            assertions.Equal(ProvisionalAppraisalState.Promoted, promoted.State,
                "v39 reconciles only after v40 reread-verified canonical success.");

            return Task.FromResult(new ScenarioExecution(
                assertions.Assertions,
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    ["developmentalRecords"] = stores.DevelopmentalRecordCount,
                    ["affectVersion"] = stores.Affect(owner.ToString()).Version,
                    ["relationshipVersion"] = stores.Relationship(owner.ToString(), observed.SpeakerId)!.Version,
                    ["outboxEntries"] = outbox.Count,
                    ["registryEntries"] = registry.ActiveCount
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["gateDigest"] = DurableAppraisalAdmissionSource.GateDigest,
                    ["v39GateDigest"] = DurableAppraisalAdmissionSource.V39CorrectedGateDigest,
                    ["canonicalStoreDigest"] = stores.StateDigest(),
                    ["outboxDigest"] = outbox.StateDigest(),
                    ["completionFingerprint"] = evidence.CompletionFingerprint
                }));
        }

        private static Task<ScenarioExecution> DurableAppraisalAdmissionStressAsync(int admissions)
        {
            if (admissions != 50_000)
                throw new HarnessAssertionException("The durable-appraisal stress executable requires exactly 50,000 admissions.");
            var assertions = new HarnessAssert();
            const string owner = "individual-v40-stress-owner";
            const string speaker = "individual-v40-stress-speaker";
            const string save = "save-v40-stress";
            const string world = "world-v40-stress";
            const string storeSet = "stores-v40-stress";
            const long generation = 9;
            var stores = new DurableCanonicalStoreSet(save, world, storeSet);
            stores.SeedAffect(CanonicalAffectState.Create(owner, ProvisionalAffectDelta.Zero, 3));
            var outbox = new DurableAppraisalOutbox();
            var registry = new DurableProposalRegistry();
            var coordinator = new CheckpointAlignedDurableAppraisalCoordinator(stores, outbox, registry);
            var v39 = new ProvisionalDialogueAppraisalStore("session-v40-stress");
            var checkpoint = V40Checkpoint(
                "checkpoint-v40-stress", generation, save, world, storeSet);
            var identity = DurableIdentityBinding.Create(
                owner, "lineage-v40-stress", save, world, storeSet, 2);
            var maximumOutbox = 0;
            var maximumRegistry = 0;

            for (var index = 0; index < admissions; index++)
            {
                var suffix = index.ToString("D5", CultureInfo.InvariantCulture);
                var observed = V40Observed(owner, speaker, suffix, generation);
                var evidenceId = "ev-v40-stress-" + suffix;
                var knowledge = new ProvisionalKnowledgeEvidence(
                    evidenceId, owner, "source-" + evidenceId, ProvisionalPrivacy.OwnerPrivate,
                    true, index, ProvisionalFactuality.VerifiedFact);
                var cue = new ProvisionalSpeechCue(
                    ProvisionalCueType.Gratitude, 0.75m, true, 0,
                    ProvisionalFactuality.SpeechAct, new[] { evidenceId });
                var current = stores.Affect(owner);
                var input = new ProvisionalDialogueAppraisalInput(
                    owner, observed, new[] { cue }, new[] { knowledge },
                    current.Fingerprint, current.Version, null, null, new ProvisionalTraitProfile());
                var appraisal = v39.Activate(v39.Prepare(input).AppraisalId, observed.DisplayedTick);
                var packet = v39.ProposePromotion(
                    appraisal.AppraisalId, "dialogue-event-v40-stress-" + suffix, generation,
                    new ProvisionalAffectDelta(valence: 0.000001m),
                    ProvisionalRelationshipDelta.Zero,
                    "admission-v40-stress-" + suffix);
                var envelope = registry.Register(v39, packet);
                maximumRegistry = Math.Max(maximumRegistry, registry.ActiveCount);
                var eventReceipt = V40EventReceipt(
                    packet.SourceReceiptId, packet.AdmittedDialogueEventId, owner, speaker,
                    observed.DisplayedTick, generation, save, world, storeSet);
                var request = new DurableAppraisalAdmissionRequest(envelope, identity, eventReceipt, checkpoint);
                var prepared = coordinator.Prepare(request);
                maximumOutbox = Math.Max(maximumOutbox, outbox.Count);
                var completion = coordinator.Apply(prepared.EntryId, checkpoint);
                v39.ObserveApplicationReceipt(completion.ApplicationReceipt);
                outbox.AdvanceCheckpoint(generation + 1);
            }

            assertions.True(
                stores.DevelopmentalRecordCount == admissions &&
                stores.Affect(owner).Version == admissions + 3 &&
                outbox.Count == 0 &&
                registry.ActiveCount == 0 &&
                outbox.CompletedCompactedCount == admissions &&
                outbox.TerminalFilterCount == admissions &&
                maximumOutbox == 1 &&
                maximumRegistry == 1,
                "50,000 durable admissions must remain bounded, duplicate-free, and fully reconciled.");
            return Task.FromResult(new ScenarioExecution(
                assertions.Assertions,
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    ["processed"] = admissions,
                    ["developmentalRecords"] = stores.DevelopmentalRecordCount,
                    ["affectVersion"] = stores.Affect(owner).Version,
                    ["maxOutboxEntries"] = maximumOutbox,
                    ["maxRegistryEntries"] = maximumRegistry,
                    ["outboxEntries"] = outbox.Count,
                    ["registryEntries"] = registry.ActiveCount,
                    ["terminalFilterBytes"] = DurableAppraisalOutbox.TerminalFilterBytes,
                    ["terminalFilterCount"] = outbox.TerminalFilterCount
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["gateDigest"] = DurableAppraisalAdmissionSource.GateDigest,
                    ["canonicalStoreDigest"] = stores.StateDigest(),
                    ["outboxStateDigest"] = outbox.StateDigest(),
                    ["outboxTerminalChain"] = outbox.TerminalChain,
                    ["terminalFilterDigest"] = outbox.TerminalFilterDigest,
                    ["v39CompletionChain"] = v39.CompletedChain
                }));
        }

        private static ObservedDisplayReceipt V40Observed(
            string owner, string speaker, string suffix, long generation)
        {
            var method = typeof(ObservedDisplayReceipt).GetMethod(
                "CreateTrusted", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new HarnessAssertionException("Trusted v38 receipt factory unavailable.");
            return (ObservedDisplayReceipt)(method.Invoke(null, new object?[]
            {
                "session-v40-stress",
                V40Hash("attempt-" + suffix),
                V40Hash("receipt-" + suffix),
                "utterance-" + suffix,
                "conversation-" + suffix,
                speaker,
                owner,
                new[] { owner, speaker }.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                V40Hash("text-" + suffix),
                1000L + int.Parse(suffix, CultureInfo.InvariantCulture),
                generation,
                ObservedDisplayOutcome.OBSERVED_SUCCESS,
                ObservedDisplayReceipt.SourceContractValue,
                ObservedDisplayReceipt.SourceGateDigestValue
            }) ?? throw new HarnessAssertionException("Trusted v38 receipt factory returned null."));
        }

        private static AdmittedDialogueEventReceipt V40EventReceipt(
            string receiptId,
            string eventId,
            string owner,
            string speaker,
            long eventTick,
            long generation,
            string save = "save-v40",
            string world = "world-v40",
            string storeSet = "stores-v40")
        {
            var method = typeof(AdmittedDialogueEventReceipt).GetMethod(
                "CreateTrusted", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new HarnessAssertionException("Trusted v40 event receipt factory unavailable.");
            return (AdmittedDialogueEventReceipt)(method.Invoke(null, new object?[]
            {
                receiptId, eventId, V40Hash("event-" + eventId), owner, speaker,
                new[] { owner, speaker }.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                eventTick, generation, save, world, storeSet,
                DurableAppraisalPrivacy.OwnerPrivate,
                DurableAppraisalAdmissionSource.EventReceiptContract
            }) ?? throw new HarnessAssertionException("Trusted v40 event receipt factory returned null."));
        }

        private static CheckpointCommitReceipt V40Checkpoint(
            string receiptId,
            long generation,
            string save = "save-v40",
            string world = "world-v40",
            string storeSet = "stores-v40")
        {
            var method = typeof(CheckpointCommitReceipt).GetMethod(
                "CreateTrusted", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new HarnessAssertionException("Trusted v40 checkpoint receipt factory unavailable.");
            return (CheckpointCommitReceipt)(method.Invoke(null, new object?[]
            {
                receiptId, save, world, storeSet, generation, V40Hash("checkpoint-" + receiptId),
                true, true, true, DurableAppraisalAdmissionSource.CheckpointReceiptContract
            }) ?? throw new HarnessAssertionException("Trusted v40 checkpoint receipt factory returned null."));
        }

        private static string V40Hash(string value)
        {
            using (var algorithm = SHA256.Create())
                return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))
                    .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
