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
        private static Task<ScenarioExecution> GroundedDevelopmentalContextMaterializationAsync(int cycles)
        {
            if (cycles < 1 || cycles > 50_000)
                throw new HarnessAssertionException("The v42 materialization scenario requires 1 through 50,000 cycles.");
            var assertions = new HarnessAssert();
            var metrics = new Dictionary<string, long>(StringComparer.Ordinal);
            var stores = new DurableCanonicalStoreSet(V41Save, V41World, V41Store);
            stores.SeedAffect(CanonicalAffectState.Create(V41Owner, ProvisionalAffectDelta.Zero, 3));
            var ledger = new InMemoryEventLedger();
            var checkpoint = V41Checkpoint();
            var outbox = new DurableAppraisalOutbox();
            var proposalRegistry = new DurableProposalRegistry();
            var coordinator = new CheckpointAlignedDurableAppraisalCoordinator(
                stores, outbox, proposalRegistry);
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
                var evidenceId = "ev-v42-" + suffix;
                var knowledge = new ProvisionalKnowledgeEvidence(
                    evidenceId, V41Owner, "source-" + evidenceId,
                    ProvisionalPrivacy.OwnerPrivate, true, index, ProvisionalFactuality.VerifiedFact);
                var cue = new ProvisionalSpeechCue(
                    ProvisionalCueType.Gratitude, 0.75m, true, 0,
                    ProvisionalFactuality.SpeechAct, new[] { evidenceId });
                var current = stores.Affect(V41Owner);
                var input = new ProvisionalDialogueAppraisalInput(
                    V41Owner, observed, new[] { cue }, new[] { knowledge },
                    current.Fingerprint, current.Version, null, null, new ProvisionalTraitProfile());
                var appraisal = v39.Activate(v39.Prepare(input).AppraisalId, observed.DisplayedTick);
                var eventId = new EventId(new Guid((index + 1).ToString("x32")));
                var packet = v39.ProposePromotion(
                    appraisal.AppraisalId,
                    eventId.ToString(),
                    V41Generation,
                    new ProvisionalAffectDelta(valence: 0.000001m),
                    ProvisionalRelationshipDelta.Zero,
                    "admission-v42-" + suffix);
                var source = V41Event(index, eventId);
                ledger.Append(source);
                var envelope = proposalRegistry.Register(v39, packet);
                maximumProposalRegistry = Math.Max(maximumProposalRegistry, proposalRegistry.ActiveCount);
                var eventReceipt = V41EventReceipt(
                    packet.SourceReceiptId,
                    packet.AdmittedDialogueEventId,
                    codec.ComputeEventHash(source),
                    observed.DisplayedTick);
                var request = new DurableAppraisalAdmissionRequest(
                    envelope, identity, eventReceipt, checkpoint);
                var prepared = coordinator.Prepare(request);
                maximumOutbox = Math.Max(maximumOutbox, outbox.Count);
                var completion = coordinator.Apply(prepared.EntryId, checkpoint);
                v39.ObserveApplicationReceipt(completion.ApplicationReceipt);
                outbox.AdvanceCheckpoint(V41Generation + 1);

                var memoryEvent = "memory-event-v42-" + index.ToString("D8");
                memories.Add(ContextualMemoryEvidence.Create(
                    "memory-v42-" + index.ToString("D8"),
                    V41Owner,
                    V41Lineage,
                    V41Counterpart,
                    memoryEvent,
                    new[] { memoryEvent },
                    100_000L + index,
                    index % 2 == 0 ? "social" : "relationship",
                    new[] { "relationship", "social" },
                    index % 101,
                    V41Generation,
                    checkpoint.CheckpointHash));
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
            var queryCount = Math.Max(1, cycles / 5);
            var canonicalBeforeQueries = stores.StateDigest();
            var forwardChain = MaterializationChain(
                forwardIndex, forwardMaterializer, queryCount, "forward");
            var reverseChain = MaterializationChain(
                reverseIndex, reverseMaterializer, queryCount, "reverse");

            assertions.Equal(forwardIndex.StateFingerprint, reverseIndex.StateFingerprint,
                "Forward and reverse admission preserve the exact accepted-v41 retrieval state.");
            assertions.Equal(forwardRegistry.StateFingerprint, reverseRegistry.StateFingerprint,
                "Forward and reverse admission preserve the exact v42 projection state.");
            assertions.Equal(forwardChain, reverseChain,
                "Forward and reverse admission preserve the exact v42 packet-chain digest.");
            assertions.Equal(canonicalBeforeQueries, stores.StateDigest(),
                "All v41 retrieval and v42 materialization queries are observer-pure.");
            assertions.Equal(cycles, stores.DevelopmentalRecordCount,
                "Every developmental record entered through genuine v39-to-v40 canonical success.");
            assertions.Equal(cycles, ledger.Snapshot().Count,
                "Every developmental record retains its exact canonical source event.");
            assertions.Equal(cycles * 2, forwardRegistry.ProjectionCount,
                "The v42 registry retains one compact projection per record and memory.");
            assertions.Equal(0, forwardRegistry.FullCanonicalObjectsRetained,
                "The v42 registry retains zero full canonical objects.");
            assertions.True(
                outbox.Count == 0 && proposalRegistry.ActiveCount == 0 &&
                maximumOutbox == 1 && maximumProposalRegistry == 1,
                "The genuine v40 admission path remains bounded and fully reconciled.");

            metrics["developmentalRecords"] = cycles;
            metrics["canonicalEvents"] = cycles;
            metrics["canonicalMemories"] = memories.Count;
            metrics["queriesPerDirection"] = queryCount;
            metrics["totalQueries"] = queryCount * 2L;
            metrics["projectionCount"] = forwardRegistry.ProjectionCount;
            metrics["fullCanonicalObjectsRetained"] = forwardRegistry.FullCanonicalObjectsRetained;
            metrics["maximumResults"] = GroundedDevelopmentalContextSource.MaximumItems;
            metrics["maximumOutbox"] = maximumOutbox;
            metrics["maximumProposalRegistry"] = maximumProposalRegistry;
            return Task.FromResult(new ScenarioExecution(
                assertions.Assertions,
                metrics,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["authority"] = GroundedDevelopmentalContextSource.Authority,
                    ["retrievalStateDigest"] = forwardIndex.StateFingerprint,
                    ["materializerStateDigest"] = forwardRegistry.StateFingerprint,
                    ["packetChainDigest"] = forwardChain,
                    ["canonicalStoreDigest"] = canonicalBeforeQueries
                }));

            string MaterializationChain(
                ContextualDevelopmentalRetrievalIndex indexValue,
                GroundedDevelopmentalContextMaterializer materializer,
                int count,
                string direction)
            {
                var chain = V41Hash("v42-packet-chain");
                for (var queryIndex = 0; queryIndex < count; queryIndex++)
                {
                    var currentTick = 200_000L + queryIndex;
                    var query = new DevelopmentalContextQuery(
                        V41Save,
                        V41World,
                        V41Store,
                        V41Owner,
                        V41Lineage,
                        V41Counterpart,
                        currentTick,
                        V41Generation,
                        new[] { checkpoint.CheckpointHash },
                        DevelopmentalContextPurpose.Appraisal,
                        new[] { "relationship", "social" });
                    var bundle = indexValue.Retrieve(query);
                    var materializationRequest = new GroundedDevelopmentalContextRequest(
                        query,
                        bundle,
                        new[] { "current-v42-" + queryIndex.ToString("D8") },
                        currentTick);
                    var packet = materializer.Materialize(materializationRequest);
                    assertions.True(packet.VerifyFingerprint(),
                        direction + " packet publicly self-verifies.");
                    assertions.True(packet.Items.Count <= GroundedDevelopmentalContextSource.MaximumItems,
                        direction + " packet remains within the four-item output bound.");
                    assertions.Equal(GroundedDevelopmentalContextSource.Authority, packet.Authority,
                        direction + " packet remains read-only and non-authoritative.");
                    assertions.Equal(0L, packet.Metrics["full_canonical_objects_retained"],
                        direction + " materialization retains zero full canonical objects.");
                    chain = V41Hash(chain + "|" + packet.Fingerprint);
                }
                return chain;
            }
        }
    }
}
