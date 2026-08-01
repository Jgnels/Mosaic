using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Contracts;
using Dagmay.Core.Development;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Persistence;

namespace Dagmay.IntegrationHarness
{
    internal static partial class IntegrationScenarios
    {
        private static Task<ScenarioExecution> GroundedCompoundAppraisalAsync(int cycles)
        {
            if (cycles < 1 || cycles > 10_000)
                throw new HarnessAssertionException("The v43 compound-appraisal scenario requires 1 through 10,000 source cycles.");
            var assertions = new HarnessAssert();
            var stores = new DurableCanonicalStoreSet(V41Save, V41World, V41Store);
            stores.SeedAffect(CanonicalAffectState.Create(V41Owner, ProvisionalAffectDelta.Zero, 3));
            var ledger = new InMemoryEventLedger();
            var checkpoint = V41Checkpoint();
            var outbox = new DurableAppraisalOutbox();
            var proposalRegistry = new DurableProposalRegistry();
            var coordinator = new CheckpointAlignedDurableAppraisalCoordinator(stores, outbox, proposalRegistry);
            var v39 = new ProvisionalDialogueAppraisalStore("session-v40-stress");
            var identity = DurableIdentityBinding.Create(
                V41Owner, V41Lineage, V41Save, V41World, V41Store, 2);
            var codec = new DialogueAdmissionOutboxCodec();
            var memories = new List<ContextualMemoryEvidence>(cycles);
            var maximumOutbox = 0;
            var maximumProposalRegistry = 0;

            for (var index = 0; index < cycles; index++)
            {
                var suffix = index.ToString("D5");
                var observed = V40Observed(V41Owner, V41Counterpart, suffix, V41Generation);
                var evidenceId = "ev-v43-" + suffix;
                var knowledge = new ProvisionalKnowledgeEvidence(
                    evidenceId, V41Owner, "source-" + evidenceId,
                    ProvisionalPrivacy.OwnerPrivate, true, index,
                    ProvisionalFactuality.VerifiedFact);
                var cue = new ProvisionalSpeechCue(
                    ProvisionalCueType.Gratitude, 0.75m, true, 0,
                    ProvisionalFactuality.SpeechAct, new[] { evidenceId });
                var current = stores.Affect(V41Owner);
                var input = new ProvisionalDialogueAppraisalInput(
                    V41Owner, observed, new[] { cue }, new[] { knowledge },
                    current.Fingerprint, current.Version, null, null,
                    new ProvisionalTraitProfile());
                var appraisal = v39.Activate(v39.Prepare(input).AppraisalId, observed.DisplayedTick);
                var eventId = new EventId(new Guid((index + 1).ToString("x32")));
                var promotion = v39.ProposePromotion(
                    appraisal.AppraisalId, eventId.ToString(), V41Generation,
                    new ProvisionalAffectDelta(valence: 0.000001m),
                    ProvisionalRelationshipDelta.Zero,
                    "admission-v43-" + suffix);
                var source = V41Event(index, eventId);
                ledger.Append(source);
                var envelope = proposalRegistry.Register(v39, promotion);
                maximumProposalRegistry = Math.Max(maximumProposalRegistry, proposalRegistry.ActiveCount);
                var eventReceipt = V41EventReceipt(
                    promotion.SourceReceiptId, promotion.AdmittedDialogueEventId,
                    codec.ComputeEventHash(source), observed.DisplayedTick);
                var prepared = coordinator.Prepare(new DurableAppraisalAdmissionRequest(
                    envelope, identity, eventReceipt, checkpoint));
                maximumOutbox = Math.Max(maximumOutbox, outbox.Count);
                var completion = coordinator.Apply(prepared.EntryId, checkpoint);
                v39.ObserveApplicationReceipt(completion.ApplicationReceipt);
                outbox.AdvanceCheckpoint(V41Generation + 1);

                var memoryEvent = "memory-event-v43-" + index.ToString("D8");
                memories.Add(ContextualMemoryEvidence.Create(
                    "memory-v43-" + index.ToString("D8"), V41Owner, V41Lineage,
                    V41Counterpart, memoryEvent, new[] { memoryEvent }, 100_000L + index,
                    index % 2 == 0 ? "social" : "relationship",
                    new[] { "relationship", "social" }, index % 101,
                    V41Generation, checkpoint.CheckpointHash));
            }

            var forwardIndex = new ContextualDevelopmentalRetrievalIndex(
                stores, ledger, new[] { checkpoint }, memories);
            var reverseIndex = new ContextualDevelopmentalRetrievalIndex(
                stores, ledger, new[] { checkpoint }, memories.AsEnumerable().Reverse());
            var forwardRegistry = new TrustedContextProjectionRegistry(
                stores, ledger, new[] { checkpoint }, memories);
            var reverseRegistry = new TrustedContextProjectionRegistry(
                stores, ledger, new[] { checkpoint }, memories.AsEnumerable().Reverse());
            var forwardMaterializer = new GroundedDevelopmentalContextMaterializer(forwardRegistry);
            var reverseMaterializer = new GroundedDevelopmentalContextMaterializer(reverseRegistry);
            var synthesizer = new GroundedCompoundAppraisalSynthesizer();
            var contextCount = Math.Max(1, cycles / 2);
            const int proposalsPerContext = 10;
            var canonicalBefore = stores.StateDigest();
            var forwardChain = ProposalChain(
                forwardIndex, forwardMaterializer, contextCount, proposalsPerContext, "forward");
            var reverseChain = ProposalChain(
                reverseIndex, reverseMaterializer, contextCount, proposalsPerContext, "reverse");

            assertions.Equal(forwardIndex.StateFingerprint, reverseIndex.StateFingerprint,
                "Opposite source admission orders preserve the accepted v41 retrieval state.");
            assertions.Equal(forwardRegistry.StateFingerprint, reverseRegistry.StateFingerprint,
                "Opposite source admission orders preserve the accepted v42 projection state.");
            assertions.Equal(forwardChain, reverseChain,
                "Opposite source admission orders preserve the v43 proposal chain.");
            assertions.Equal(canonicalBefore, stores.StateDigest(),
                "v41 retrieval, v42 materialization, and v43 synthesis are observer-pure.");
            assertions.True(outbox.Count == 0 && proposalRegistry.ActiveCount == 0 &&
                maximumOutbox == 1 && maximumProposalRegistry == 1,
                "The inherited durable-admission path remains bounded and fully reconciled.");

            return Task.FromResult(new ScenarioExecution(
                assertions.Assertions,
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    ["developmentalRecords"] = cycles,
                    ["canonicalMemories"] = memories.Count,
                    ["uniqueContexts"] = contextCount,
                    ["proposals"] = contextCount * proposalsPerContext,
                    ["maximumFamilies"] = GroundedCompoundAppraisalSource.MaximumFamilies,
                    ["maximumAffectDimensions"] = GroundedCompoundAppraisalSource.MaximumAffectDimensions,
                    ["maximumRelationshipDimensions"] = GroundedCompoundAppraisalSource.MaximumRelationshipDimensions,
                    ["maximumWhy"] = GroundedCompoundAppraisalSource.MaximumWhyContributions,
                    ["synthesizerRetainedState"] = 0,
                    ["maximumOutbox"] = maximumOutbox,
                    ["maximumProposalRegistry"] = maximumProposalRegistry
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["authority"] = GroundedCompoundAppraisalSource.Authority,
                    ["retrievalStateDigest"] = forwardIndex.StateFingerprint,
                    ["materializerStateDigest"] = forwardRegistry.StateFingerprint,
                    ["proposalChainDigest"] = forwardChain,
                    ["canonicalStoreDigest"] = canonicalBefore
                }));

            string ProposalChain(
                ContextualDevelopmentalRetrievalIndex indexValue,
                GroundedDevelopmentalContextMaterializer materializer,
                int contexts,
                int repetitions,
                string direction)
            {
                var chain = V41Hash("v43-proposal-chain");
                for (var queryIndex = 0; queryIndex < contexts; queryIndex++)
                {
                    var currentTick = 200_000L + queryIndex;
                    var currentEventId = "current-v43-" + queryIndex.ToString("D8");
                    var query = new DevelopmentalContextQuery(
                        V41Save, V41World, V41Store, V41Owner, V41Lineage,
                        V41Counterpart, currentTick, V41Generation,
                        new[] { checkpoint.CheckpointHash }, DevelopmentalContextPurpose.Appraisal,
                        new[] { "relationship", "social" });
                    var bundle = indexValue.Retrieve(query);
                    var materializationRequest = new GroundedDevelopmentalContextRequest(
                        query, bundle, new[] { currentEventId }, currentTick);
                    var packet = materializer.Materialize(materializationRequest);
                    var seed = GroundedCurrentEventAppraisalSeed.Create(
                        V41Owner, V41Lineage, V41Counterpart, currentEventId,
                        new[] { currentEventId }, currentTick, V41Hash("event:" + currentEventId),
                        V41Save, V41World, V41Store, V41Generation, checkpoint.CheckpointHash,
                        "OWNER_PRIVATE", true, true, true,
                        new[]
                        {
                            new KeyValuePair<GroundedAppraisalFamily, int>(
                                GroundedAppraisalFamily.Gratitude, 7000)
                        },
                        new[] { new KeyValuePair<string, int>("valence", 3000) },
                        new[] { new KeyValuePair<string, int>("trust", 1000) },
                        new[] { "RULE_DIRECT_EVENT" });
                    var request = new ContextualAppraisalRequest(seed, materializationRequest, packet);
                    string? repeated = null;
                    for (var repetition = 0; repetition < repetitions; repetition++)
                    {
                        var proposal = synthesizer.Synthesize(request);
                        assertions.True(proposal.VerifyFingerprint(),
                            direction + " proposal publicly self-verifies.");
                        assertions.True(proposal.Families.Count <= 3 &&
                            proposal.AffectDimensions.Count <= 6 &&
                            proposal.RelationshipConsiderations.Count <= 4 &&
                            proposal.Why.Count <= 16,
                            direction + " proposal remains within every output bound.");
                        assertions.Equal(GroundedCompoundAppraisalSource.Authority, proposal.Authority,
                            direction + " proposal remains inert and non-authoritative.");
                        if (repeated is null) repeated = proposal.Fingerprint;
                        else assertions.Equal(repeated, proposal.Fingerprint,
                            direction + " exact request replay remains deterministic.");
                        chain = V41Hash(chain + "|" + proposal.Fingerprint);
                    }
                }
                return chain;
            }
        }
    }
}
