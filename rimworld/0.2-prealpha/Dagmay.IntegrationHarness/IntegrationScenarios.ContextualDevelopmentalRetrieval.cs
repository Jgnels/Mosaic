using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Contracts;
using Dagmay.Core.Development;
using Dagmay.Core.Dialogue;
using Dagmay.Core.Memory;
using Dagmay.Core.Persistence;

namespace Dagmay.IntegrationHarness
{
    internal static partial class IntegrationScenarios
    {
        private const string V41Owner = "11111111111111111111111111111111";
        private const string V41Counterpart = "22222222222222222222222222222222";
        private const string V41Lineage = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string V41Save = "save-v41-integration";
        private const string V41World = "world-v41-integration";
        private const string V41Store = "stores-v41-integration";
        private const long V41Generation = 7;

        private static Task<ScenarioExecution> ContextualDevelopmentalRetrievalAsync(int cycles)
        {
            if (cycles < 1 || cycles > 50_000)
                throw new HarnessAssertionException("The contextual retrieval scenario requires 1 through 50,000 cycles.");
            var assertions = new HarnessAssert();
            var metrics = new Dictionary<string, long>(StringComparer.Ordinal);
            var stores = new DurableCanonicalStoreSet(V41Save, V41World, V41Store);
            stores.SeedAffect(CanonicalAffectState.Create(V41Owner, ProvisionalAffectDelta.Zero, 3));
            var ledger = new InMemoryEventLedger();
            var checkpoint = V41Checkpoint();
            var outbox = new DurableAppraisalOutbox();
            var registry = new DurableProposalRegistry();
            var coordinator = new CheckpointAlignedDurableAppraisalCoordinator(stores, outbox, registry);
            var v39 = new ProvisionalDialogueAppraisalStore("session-v40-stress");
            var identity = DurableIdentityBinding.Create(
                V41Owner, V41Lineage, V41Save, V41World, V41Store, 2);
            var codec = new DialogueAdmissionOutboxCodec();
            var memories = new List<ContextualMemoryEvidence>(cycles);
            var maximumOutbox = 0;
            var maximumRegistry = 0;

            for (var index = 0; index < cycles; index++)
            {
                var suffix = index.ToString("D5");
                var observed = V40Observed(V41Owner, V41Counterpart, suffix, V41Generation);
                var evidenceId = "ev-v41-" + suffix;
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
                    appraisal.AppraisalId, eventId.ToString(), V41Generation,
                    new ProvisionalAffectDelta(valence: 0.000001m),
                    ProvisionalRelationshipDelta.Zero,
                    "admission-v41-" + suffix);
                var source = V41Event(index, eventId);
                ledger.Append(source);
                var envelope = registry.Register(v39, packet);
                maximumRegistry = Math.Max(maximumRegistry, registry.ActiveCount);
                var eventReceipt = V41EventReceipt(
                    packet.SourceReceiptId, packet.AdmittedDialogueEventId,
                    codec.ComputeEventHash(source), observed.DisplayedTick);
                var request = new DurableAppraisalAdmissionRequest(
                    envelope, identity, eventReceipt, checkpoint);
                var prepared = coordinator.Prepare(request);
                maximumOutbox = Math.Max(maximumOutbox, outbox.Count);
                var completion = coordinator.Apply(prepared.EntryId, checkpoint);
                v39.ObserveApplicationReceipt(completion.ApplicationReceipt);
                outbox.AdvanceCheckpoint(V41Generation + 1);

                memories.Add(ContextualMemoryEvidence.Create(
                    "memory-" + index.ToString("D8"),
                    V41Owner,
                    V41Lineage,
                    V41Counterpart,
                    "memory-event-" + index.ToString("D8"),
                    new[] { "memory-event-" + index.ToString("D8") },
                    100_000L + index,
                    index % 2 == 0 ? "social" : "relationship",
                    new[] { "relationship", "social" },
                    index % 101,
                    V41Generation,
                    checkpoint.CheckpointHash));
            }

            var forward = new ContextualDevelopmentalRetrievalIndex(
                stores, ledger, new[] { checkpoint }, memories);
            var reverse = new ContextualDevelopmentalRetrievalIndex(
                stores, ledger, new[] { checkpoint }, memories.AsEnumerable().Reverse());
            var queryCount = Math.Max(1, cycles / 5);
            var canonicalBeforeQueries = stores.StateDigest();
            var forwardChain = QueryChain(forward, queryCount);
            var reverseChain = QueryChain(reverse, queryCount);

            assertions.Equal(forward.StateFingerprint, reverse.StateFingerprint,
                "Forward and reverse rebuilds preserve the exact compact-index fingerprint.");
            assertions.Equal(forwardChain, reverseChain,
                "Forward and reverse rebuilds preserve the exact normalized query-chain digest.");
            assertions.Equal(canonicalBeforeQueries, stores.StateDigest(),
                "All contextual queries are observer-pure over canonical state.");
            assertions.Equal(ContextualDevelopmentalRetrievalSource.MaximumCandidatesPerKey,
                forward.MaximumObservedCandidatesPerKey,
                "Per-key candidate storage remains capped at exactly 64.");
            assertions.Equal(cycles, stores.DevelopmentalRecordCount,
                "Every stress record entered through genuine v39-to-v40 canonical success.");
            assertions.Equal(cycles, ledger.Snapshot().Count,
                "Every stress record retained an exact canonical source event.");
            assertions.True(
                outbox.Count == 0 && registry.ActiveCount == 0 &&
                maximumOutbox == 1 && maximumRegistry == 1,
                "The genuine v40 admission path remains bounded and fully reconciled.");

            metrics["developmentalRecords"] = cycles;
            metrics["canonicalEvents"] = cycles;
            metrics["canonicalMemories"] = memories.Count;
            metrics["queriesPerDirection"] = queryCount;
            metrics["totalQueries"] = queryCount * 2L;
            metrics["maximumCandidatesPerKey"] = forward.MaximumObservedCandidatesPerKey;
            metrics["maximumResults"] = ContextualDevelopmentalRetrievalSource.MaximumResults;
            metrics["maximumOutbox"] = maximumOutbox;
            metrics["maximumRegistry"] = maximumRegistry;
            return Task.FromResult(new ScenarioExecution(assertions.Assertions, metrics, new Dictionary<string, string>
            {
                ["authority"] = ContextualDevelopmentalRetrievalSource.Authority,
                ["retrievalStateDigest"] = forward.StateFingerprint,
                ["queryChainDigest"] = forwardChain,
                ["canonicalStoreDigest"] = canonicalBeforeQueries
            }));

            string QueryChain(ContextualDevelopmentalRetrievalIndex indexValue, int count)
            {
                var chain = V41Hash("v41-query-chain");
                for (var queryIndex = 0; queryIndex < count; queryIndex++)
                {
                    var query = new DevelopmentalContextQuery(
                        V41Save,
                        V41World,
                        V41Store,
                        V41Owner,
                        V41Lineage,
                        V41Counterpart,
                        200_000L + queryIndex,
                        V41Generation,
                        new[] { checkpoint.CheckpointHash },
                        DevelopmentalContextPurpose.Appraisal,
                        new[] { "relationship", "social" });
                    var bundle = indexValue.Retrieve(query);
                    assertions.True(bundle.Selected.Count <= ContextualDevelopmentalRetrievalSource.MaximumResults,
                        "Contextual retrieval remains within the four-item output bound.");
                    assertions.Equal(ContextualDevelopmentalRetrievalSource.Authority, bundle.Authority,
                        "Contextual retrieval remains read-only and has no canonical or pawn authority.");
                    var roots = bundle.Selected.SelectMany(value => value.RootEventIds).ToList();
                    assertions.Equal(roots.Count, roots.Distinct(StringComparer.Ordinal).Count(),
                        "One root event never occupies multiple result roles.");
                    chain = V41Hash(chain + "|" + bundle.Fingerprint);
                }
                return chain;
            }
        }

        private static EnvironmentEvent V41Event(int index, EventId id)
        {
            return new EnvironmentEvent(
                id,
                "v41-integration:" + index.ToString("D8"),
                "mosaic.social.development",
                "offline",
                BaseTime.AddSeconds(index),
                BaseTime.AddSeconds(index).AddMilliseconds(1),
                10_000L + index,
                "Mosaic.IntegrationHarness",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["mosaic_admitted"] = "true",
                    ["mosaic_privacy"] = "OWNER_PRIVATE",
                    ["mosaic_lineage_id"] = V41Lineage,
                    ["mosaic_category"] = "mixed-social",
                    ["mosaic_context_tags"] = "relationship,social"
                },
                new[] { IndividualId.Parse(V41Owner), IndividualId.Parse(V41Counterpart) });
        }

        private static AdmittedDialogueEventReceipt V41EventReceipt(
            string receiptId,
            string eventId,
            string eventHash,
            long eventTick)
        {
            var method = typeof(AdmittedDialogueEventReceipt).GetMethod(
                "CreateTrusted", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new HarnessAssertionException("Trusted v40 event receipt factory is missing.");
            return (AdmittedDialogueEventReceipt)(method.Invoke(null, new object?[]
            {
                receiptId, eventId, eventHash, V41Owner, V41Counterpart,
                new[] { V41Owner, V41Counterpart }.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                eventTick, V41Generation, V41Save, V41World, V41Store,
                DurableAppraisalPrivacy.OwnerPrivate,
                DurableAppraisalAdmissionSource.EventReceiptContract
            }) ?? throw new HarnessAssertionException("Trusted v40 event receipt factory failed."));
        }

        private static DevelopmentalAppraisalRecord V41Record(
            int index,
            EnvironmentEvent source,
            string eventHash)
        {
            var suffix = index.ToString("D8");
            var constructor = typeof(DevelopmentalAppraisalRecord)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            var positive = index % 2 == 0;
            var affect = positive
                ? new ProvisionalAffectDelta(valence: 0.01m, agency: 0.005m)
                : new ProvisionalAffectDelta(valence: -0.01m, threat: 0.005m);
            var relationship = positive
                ? new ProvisionalRelationshipDelta(trust: 0.02m, affection: 0.01m)
                : new ProvisionalRelationshipDelta(trust: -0.02m, resentment: 0.01m);
            return (DevelopmentalAppraisalRecord)constructor.Invoke(new object?[]
            {
                V41Hash("record:" + suffix),
                V41Hash("packet:" + suffix),
                V41Hash("appraisal:" + suffix),
                V41Owner,
                V41Lineage,
                V41Counterpart,
                source.Id.ToString(),
                eventHash,
                V41Generation,
                affect,
                affect,
                relationship,
                relationship,
                V41Hash("affect-before:" + suffix),
                V41Hash("affect-after:" + suffix),
                V41Hash("relationship-before:" + suffix),
                V41Hash("relationship-after:" + suffix)
            });
        }

        private static void V41AddRecord(
            DurableCanonicalStoreSet stores,
            DevelopmentalAppraisalRecord record)
        {
            var field = typeof(DurableCanonicalStoreSet).GetField(
                "RecordsById", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Canonical v40 record store seam is missing.");
            var records = (IDictionary<string, DevelopmentalAppraisalRecord>)field.GetValue(stores)!;
            records.Add(record.RecordId, record);
        }

        private static CheckpointCommitReceipt V41Checkpoint()
        {
            var method = typeof(CheckpointCommitReceipt).GetMethod(
                "CreateTrusted", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Trusted v40 checkpoint factory is missing.");
            return (CheckpointCommitReceipt)(method.Invoke(null, new object[]
            {
                "receipt-v41-integration",
                V41Save,
                V41World,
                V41Store,
                V41Generation,
                V41Hash("checkpoint-v41-integration"),
                true,
                true,
                true,
                DurableAppraisalAdmissionSource.CheckpointReceiptContract
            }) ?? throw new InvalidOperationException("Trusted v40 checkpoint factory failed."));
        }

        private static string V41Hash(string value)
        {
            using (var algorithm = SHA256.Create())
                return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))
                    .Select(item => item.ToString("x2")));
        }
    }
}
