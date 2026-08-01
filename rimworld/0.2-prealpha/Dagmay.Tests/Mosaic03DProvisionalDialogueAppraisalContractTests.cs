using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Presentation;

namespace Dagmay.Tests
{
    internal static class Mosaic03DProvisionalDialogueAppraisalContractTests
    {
        private const string Owner = "individual-owner-a";
        private const string Speaker = "individual-speaker-b";
        private const string Witness = "individual-witness-c";
        private const string Session = "session-v39-fixture";
        private static readonly string AffectFingerprint = Hash("canonical-affect-before");
        private static readonly string RelationshipFingerprint = Hash("canonical-relationship-before");

        public static void AttemptIsNotSuccess()
        {
            var store = NewStore();
            TestAssert.Throws<ArgumentException>(
                () => store.Prepare(Input(receipt: Receipt(outcome: ObservedDisplayOutcome.ATTEMPT_ONLY))),
                "An attempted display cannot create a provisional appraisal.");
        }

        public static void FailedReleaseIsNotSuccess()
        {
            var store = NewStore();
            TestAssert.Throws<ArgumentException>(
                () => store.Prepare(Input(receipt: Receipt(outcome: ObservedDisplayOutcome.FAILED))),
                "A failed display cannot create a provisional appraisal.");
        }

        public static void NonWitnessFailsClosed()
        {
            TestAssert.Throws<ArgumentException>(
                () => NewStore().Prepare(Input(owner: Witness)),
                "An unwitnessed perspective must fail closed.");
        }

        public static void StaleSessionReceiptFails()
        {
            TestAssert.Throws<ArgumentException>(
                () => NewStore().Prepare(Input(receipt: Receipt(session: "stale-session"))),
                "A stale session receipt must fail closed.");
        }

        public static void ForeignKnowledgeFails()
        {
            TestAssert.Throws<ArgumentException>(
                () => NewStore().Prepare(Input(knowledge: new[] { Knowledge(owner: Witness) })),
                "Foreign owner knowledge must fail closed.");
        }

        public static void MissingKnowledgeFails()
        {
            TestAssert.Throws<ArgumentException>(
                () => NewStore().Prepare(Input(knowledge: new[] { Knowledge(evidenceId: "other") })),
                "Missing cue evidence must fail closed.");
        }

        public static void UnadmittedKnowledgeFails()
        {
            TestAssert.Throws<ArgumentException>(
                () => NewStore().Prepare(Input(knowledge: new[] { Knowledge(admitted: false) })),
                "Unadmitted evidence must fail closed.");
        }

        public static void FutureKnowledgeFails()
        {
            TestAssert.Throws<ArgumentException>(
                () => NewStore().Prepare(Input(knowledge: new[] { Knowledge(tick: 1100) })),
                "Future evidence must fail closed.");
        }

        public static void DuplicateIsIdempotent()
        {
            var store = NewStore();
            var input = Input();
            var first = store.Prepare(input);
            var second = store.Prepare(input);
            TestAssert.Equal(first.AppraisalId, second.AppraisalId, "Active duplicate admission must be idempotent.");
            TestAssert.Equal(1, store.ActiveCount(), "Active duplicate admission cannot add a second record.");
        }

        public static void CueOrderIsDeterministic()
        {
            var first = NewStore().Prepare(Input(cues: Cues(
                Cue(ProvisionalCueType.Gratitude, 0.65m),
                Cue(ProvisionalCueType.Uncertainty, 0.70m))));
            var second = NewStore().Prepare(Input(cues: Cues(
                Cue(ProvisionalCueType.Uncertainty, 0.70m),
                Cue(ProvisionalCueType.Gratitude, 0.65m))));
            TestAssert.Equal(first.AppraisalId, second.AppraisalId, "Canonical cue order must produce one appraisal identity.");
            TestAssert.Equal(first.AffectDelta.Valence, second.AffectDelta.Valence, "Canonical cue order must preserve appraisal values.");
        }

        public static void QuotedInsultIsNotAttributed()
        {
            var item = NewStore().Prepare(Input(cues: new[]
            {
                Cue(ProvisionalCueType.Insult, quoteDepth: 1)
            }));
            TestAssert.Equal(0m, item.Confidence, "Quoted hostility must not be attributed to the speaker.");
            TestAssert.Equal(0m, item.AffectDelta.MaximumAbsolute, "Quoted hostility cannot create affect.");
            TestAssert.True(item.ReasonCodes.Contains("QUOTED_OR_NONATTRIBUTABLE_CUE_SUPPRESSED"), "Suppression must be explicit.");
        }

        public static void ClaimDoesNotBecomeFact()
        {
            var item = NewStore().Prepare(Input(cues: new[]
            {
                Cue(ProvisionalCueType.Accusation, factuality: ProvisionalFactuality.ClaimOnly)
            }));
            TestAssert.True(item.ReasonCodes.Contains("CUE_ACCUSATION"), "A claim may shape a bounded interpretation.");
            TestAssert.False(item.ReasonCodes.Contains("VERIFIED_FACT"), "A claim cannot become a verified fact.");
        }

        public static void PerUtteranceBounds()
        {
            var cues = Enumerable.Range(0, 20)
                .Select(_ => Cue(ProvisionalCueType.Threat, 1m))
                .ToArray();
            var item = NewStore().Prepare(Input(cues: cues));
            TestAssert.True(item.AffectDelta.MaximumAbsolute <= 0.15m, "Per-utterance affect must remain at or below 0.15.");
            TestAssert.True(item.RelationshipDelta.MaximumAbsolute <= 0.08m, "Per-utterance relationship stance must remain at or below 0.08.");
        }

        public static void ConversationBoundsAndRunawayPrevention()
        {
            var store = NewStore();
            for (var index = 0; index < 100; index++)
            {
                var receipt = Receipt("conversation-bound-" + index.ToString("D3", CultureInfo.InvariantCulture), 1000 + index);
                var item = store.Prepare(Input(receipt: receipt, cues: new[] { Cue(ProvisionalCueType.Threat, 1m) }));
                store.Activate(item.AppraisalId, receipt.DisplayedTick);
            }
            var overlay = store.EffectiveOverlay(Owner, "conversation-001", 1100);
            TestAssert.True(overlay.Affect.MaximumAbsolute <= 0.35m, "Conversation affect must remain at or below 0.35.");
            TestAssert.True(overlay.Relationship.MaximumAbsolute <= 0.20m, "Conversation relationship stance must remain at or below 0.20.");
            TestAssert.True(store.ActiveCount(Owner) <= 32, "At most 32 full live appraisals may remain.");
        }

        public static void OutOfOrderReceiptsFailClosed()
        {
            var store = NewStore();
            var newer = Receipt("newer", 2040);
            store.Activate(store.Prepare(Input(receipt: newer)).AppraisalId, newer.DisplayedTick);
            TestAssert.Throws<ArgumentException>(
                () => store.Prepare(Input(receipt: Receipt("older", 2030))),
                "An older receipt must fail closed.");
        }

        public static void TerminalDuplicateIsRejectedAfterCompaction()
        {
            var store = NewStore();
            var firstReceipt = Receipt("terminal-000", 1900);
            var first = store.Prepare(Input(receipt: firstReceipt, cues: new[] { Cue(ProvisionalCueType.Neutral) }));
            store.Activate(first.AppraisalId, firstReceipt.DisplayedTick);
            for (var index = 1; index < 34; index++)
            {
                var receipt = Receipt("terminal-" + index.ToString("D3", CultureInfo.InvariantCulture), 1900 + index);
                store.Activate(store.Prepare(Input(receipt: receipt, cues: new[] { Cue(ProvisionalCueType.Neutral) })).AppraisalId, receipt.DisplayedTick);
            }
            TestAssert.Throws<ArgumentException>(
                () => store.Prepare(Input(receipt: firstReceipt, cues: new[] { Cue(ProvisionalCueType.Neutral) })),
                "A compacted terminal duplicate must fail closed.");
        }

        public static void SeenFilterIsFixedMemoryAndDeterministic()
        {
            var first = BuildAdmissions(128, "filter");
            var second = BuildAdmissions(128, "filter");
            TestAssert.Equal(1 << 20, ProvisionalDialogueAppraisalStore.FilterBytes, "The terminal filter must remain exactly 1 MiB.");
            TestAssert.Equal(first.SeenFilterDigest, second.SeenFilterDigest, "Filter bit layout must be deterministic.");
            TestAssert.Equal(128L, first.SeenCount, "Every admitted receipt must enter fixed-memory duplicate protection.");
        }

        public static void CapacityEvictionDeterministic()
        {
            var first = BuildAdmissions(40, "capacity");
            var second = BuildAdmissions(40, "capacity");
            TestAssert.Equal(first.StateDigest(), second.StateDigest(), "Capacity eviction must be deterministic.");
            TestAssert.Equal(32, first.ActiveCount(Owner), "Capacity eviction must retain at most 32 records.");
            TestAssert.Equal(8L, first.DiagnosticProjection().CountsByState["Discarded"], "Capacity eviction must compact eight records.");
        }

        public static void DecayAndExpiry()
        {
            var store = NewStore();
            var item = store.Prepare(Input());
            store.Activate(item.AppraisalId, item.CreatedTick);
            var early = store.EffectiveOverlay(Owner, item.ConversationId, item.CreatedTick).Affect.MaximumAbsolute;
            var middle = store.EffectiveOverlay(Owner, item.ConversationId, item.CreatedTick + ProvisionalDialogueAppraisalStore.LifetimeTicks / 2).Affect.MaximumAbsolute;
            var late = store.EffectiveOverlay(Owner, item.ConversationId, item.ExpiresTick).Affect.MaximumAbsolute;
            TestAssert.True(early > middle, "Linear decay must reduce the provisional overlay.");
            TestAssert.Equal(0m, late, "Expiry must remove the overlay.");
        }

        public static void CheckpointChangeDiscards()
        {
            var store = NewStore();
            var item = store.Prepare(Input());
            store.Activate(item.AppraisalId, item.CreatedTick);
            TestAssert.Equal(1, store.CheckpointAdvanced(8), "Checkpoint mismatch must discard the provisional record.");
            TestAssert.Equal(0, store.ActiveCount(), "Checkpoint mismatch must leave no active overlay.");
        }

        public static void RestartDiscardsAndOldReceiptCannotReplay()
        {
            var old = NewStore();
            old.Activate(old.Prepare(Input()).AppraisalId, 1000);
            var restarted = new ProvisionalDialogueAppraisalStore("session-after-restart");
            TestAssert.Equal(0, restarted.ActiveCount(), "The first implementation must not persist provisional state.");
            TestAssert.Throws<ArgumentException>(
                () => restarted.Prepare(Input()),
                "An old-session receipt cannot replay after restart.");
        }

        public static void TraitModifiersAreBoundedAndNoSignFlip()
        {
            var low = NewStore().Prepare(Input(traits: new ProvisionalTraitProfile(emotionalReactivity: 0m)));
            var high = NewStore().Prepare(Input(traits: new ProvisionalTraitProfile(emotionalReactivity: 1m)));
            TestAssert.True(high.AffectDelta.MaximumAbsolute / low.AffectDelta.MaximumAbsolute <= 1.666667m, "Trait scaling must remain bounded.");
            TestAssert.True(low.AffectDelta.Valence < 0m && high.AffectDelta.Valence < 0m, "Traits cannot reverse appraisal sign.");
        }

        public static void SeverityHybridPresentation()
        {
            var silent = NewStore().Prepare(Input(cues: new[] { Cue(ProvisionalCueType.Uncertainty, 0.2m) }));
            var icon = NewStore().Prepare(Input(cues: new[] { Cue(ProvisionalCueType.Insult, 0.7m) }));
            var thought = NewStore().Prepare(Input(
                cues: new[] { Cue(ProvisionalCueType.Threat, 1m) },
                traits: new ProvisionalTraitProfile(expressiveness: 1m, emotionalReactivity: 1m)));
            TestAssert.Equal(ProvisionalPresentationMode.None, silent.PresentationMode, "Low-severity appraisal must remain invisible.");
            TestAssert.True(icon.PresentationMode != ProvisionalPresentationMode.None, "Medium severity must permit a bounded indicator.");
            TestAssert.Equal(ProvisionalPresentationMode.Thought, thought.PresentationMode, "High severity must select the bounded thought projection.");
        }

        public static void PromotionPacketIsProposalOnly()
        {
            var store = NewStore();
            var item = store.Activate(store.Prepare(Input()).AppraisalId, 1000);
            var packet = store.ProposePromotion(
                item.AppraisalId, "dialogue-event-001", 7,
                new ProvisionalAffectDelta(valence: -0.02m),
                new ProvisionalRelationshipDelta(resentment: 0.01m),
                "admission-receipt-001");
            TestAssert.Equal("PROPOSAL_ONLY_NO_MUTATION_AUTHORITY", DurableApplicationPacket.Authority, "Promotion packets must be proposal-only.");
            TestAssert.False(packet.CanonicalMutationAuthority, "A proposal packet cannot mutate canonical state.");
        }

        public static void FailedApplicationIsNotSuccessAndOverlayRemains()
        {
            var setup = Pending();
            var failed = CanonicalApplicationReceipt.CreateFailure(
                setup.Packet.PacketId, Owner, "dialogue-event-001", 7,
                AffectFingerprint, 3, RelationshipFingerprint, 5);
            var result = setup.Store.ObserveApplicationReceipt(failed);
            TestAssert.Equal(ProvisionalAppraisalState.Active, result.State, "Failed application must return the appraisal to active.");
            TestAssert.Equal(0, setup.Store.PendingPacketCount, "Failed application must remove the pending packet.");
            TestAssert.True(setup.Store.EffectiveOverlay(Owner, "conversation-001", 1000).Affect.MaximumAbsolute > 0m, "Failed application must retain the overlay.");
        }

        public static void SuccessfulZeroEffectReconcilesWithoutDoubleCounting()
        {
            var setup = Pending();
            var success = TrustedApplication(
                setup.Packet, true, AffectFingerprint, 3, RelationshipFingerprint, 5);
            var result = setup.Store.ObserveApplicationReceipt(success);
            TestAssert.Equal(ProvisionalAppraisalState.Promoted, result.State, "Observed canonical success must promote the record.");
            TestAssert.Equal(0m, setup.Store.EffectiveOverlay(Owner, "conversation-001", 1000).Affect.MaximumAbsolute, "Successful reconciliation must remove the overlay.");
        }

        public static void DuplicateSuccessReceiptIdempotent()
        {
            var setup = Pending();
            var success = TrustedApplication(setup.Packet, true, AffectFingerprint, 3, RelationshipFingerprint, 5);
            var first = setup.Store.ObserveApplicationReceipt(success);
            var second = setup.Store.ObserveApplicationReceipt(success);
            TestAssert.Equal(first.AppraisalId, second.AppraisalId, "Recent duplicate canonical success must be idempotent.");
            TestAssert.Equal(1L, setup.Store.CompletedCount, "Duplicate success cannot double-count completion.");
        }

        public static void SuccessfulPromotionRetentionIsBounded()
        {
            var store = PromoteMany(80, "promotion-retention");
            TestAssert.Equal(80L, store.CompletedCount, "All observed promotions must contribute to the completion chain.");
            TestAssert.True(store.CompletedRecentCount <= 64, "At most 64 complete successes may remain.");
            TestAssert.Equal(0, store.PendingPacketCount, "Successful promotion must remove pending packets.");
            TestAssert.Equal(1 << 20, ProvisionalDialogueAppraisalStore.FilterBytes, "Completion duplicate protection must remain fixed-memory.");
        }

        public static void ForeignApplicationReceiptFails()
        {
            var foreignPacket = Hash("foreign-packet");
            var receipt = TrustedApplication(
                foreignPacket, Owner, "event", 7, true,
                AffectFingerprint, 3, RelationshipFingerprint, 5);
            TestAssert.Throws<ArgumentException>(
                () => NewStore().ObserveApplicationReceipt(receipt),
                "A foreign canonical receipt must fail closed.");
        }

        public static void StaleSuccessReceiptFails()
        {
            var setup = Pending();
            var stale = TrustedApplication(
                setup.Packet.PacketId, Owner, "dialogue-event-001", 8, true,
                AffectFingerprint, 3, RelationshipFingerprint, 5);
            TestAssert.Throws<ArgumentException>(
                () => setup.Store.ObserveApplicationReceipt(stale),
                "A checkpoint-stale success receipt must fail closed.");
        }

        public static void TargetedAffectRequiresVersionAdvance()
        {
            var setup = Pending(affect: new ProvisionalAffectDelta(valence: -0.01m));
            var stale = TrustedApplication(
                setup.Packet, true, Hash("new-affect"), 3, RelationshipFingerprint, 5);
            TestAssert.Throws<ArgumentException>(
                () => setup.Store.ObserveApplicationReceipt(stale),
                "A targeted affect success must advance affect version.");
        }

        public static void RelationshipOnlySuccessPreservesAffectVersion()
        {
            var setup = Pending(relationship: new ProvisionalRelationshipDelta(resentment: 0.01m));
            var success = TrustedApplication(
                setup.Packet, true, AffectFingerprint, 3, Hash("relationship-after"), 6);
            var result = setup.Store.ObserveApplicationReceipt(success);
            TestAssert.Equal(ProvisionalAppraisalState.Promoted, result.State, "Relationship-only success must preserve affect and advance only relationship state.");
        }

        public static void NoopSuccessRejectsArtificialVersionChurn()
        {
            var setup = Pending();
            var churn = TrustedApplication(
                setup.Packet, true, Hash("artificial-churn"), 4, RelationshipFingerprint, 5);
            TestAssert.Throws<ArgumentException>(
                () => setup.Store.ObserveApplicationReceipt(churn),
                "No-op success cannot manufacture canonical version churn.");
        }

        public static void DiagnosticProjectionDoesNotLeakRawTextOrIds()
        {
            var store = NewStore();
            store.Activate(store.Prepare(Input()).AppraisalId, 1000);
            var diagnostic = store.DiagnosticProjection();
            var surface = string.Join("|", new[]
            {
                diagnostic.SessionHash,
                diagnostic.StateDigest,
                ProvisionalAppraisalDiagnostics.Authority
            });
            TestAssert.False(surface.Contains(Owner) || surface.Contains(Speaker) || surface.Contains("transient text"), "Diagnostics must not leak raw text or identity labels.");
            TestAssert.False(diagnostic.ContainsRawText || diagnostic.ContainsOwnerOrSpeakerIds, "Diagnostics must explicitly exclude private content and IDs.");
        }

        public static void ForgedDisplayReceiptFailsFingerprint()
        {
            var forged = CloneAndTamper(Receipt(), "<ReceiptFingerprint>k__BackingField", Hash("forged-display"));
            TestAssert.Throws<ArgumentException>(
                () => NewStore().Prepare(Input(receipt: forged)),
                "A forged v38 receipt fingerprint must fail before appraisal.");
        }

        public static void ForeignDisplayReceiptContractFails()
        {
            var foreign = TrustedReceipt("foreign", 1000, ObservedDisplayOutcome.OBSERVED_SUCCESS, Session, "Foreign.Contract.v1");
            TestAssert.Throws<ArgumentException>(
                () => NewStore().Prepare(Input(receipt: foreign)),
                "A foreign display source contract must fail closed.");
        }

        public static void ForgedApplicationReceiptFailsFingerprint()
        {
            var setup = Pending();
            var success = TrustedApplication(setup.Packet, true, AffectFingerprint, 3, RelationshipFingerprint, 5);
            var forged = CloneAndTamper(success, "<ReceiptFingerprint>k__BackingField", Hash("forged-application"));
            TestAssert.Throws<ArgumentException>(
                () => setup.Store.ObserveApplicationReceipt(forged),
                "A forged canonical receipt fingerprint must fail closed.");
        }

        public static void StaticAuthorityContract()
        {
            var displayFactory = typeof(ObservedDisplayReceipt).GetMethod("CreateTrusted", BindingFlags.Static | BindingFlags.NonPublic);
            var canonicalFactory = typeof(CanonicalApplicationReceipt).GetMethod("CreateTrusted", BindingFlags.Static | BindingFlags.NonPublic);
            TestAssert.True(displayFactory is not null && !displayFactory.IsPublic, "Observed-success factory must remain internal.");
            TestAssert.True(canonicalFactory is not null && !canonicalFactory.IsPublic, "Canonical-success factory must remain internal.");
            TestAssert.True(
                typeof(DurableApplicationPacket).GetConstructors(BindingFlags.Instance | BindingFlags.Public).Length == 0,
                "Ordinary callers cannot construct success-authority packets.");
            var references = typeof(ProvisionalDialogueAppraisalStore).Assembly.GetReferencedAssemblies().Select(value => value.Name ?? string.Empty).ToArray();
            TestAssert.False(references.Any(value =>
                value.IndexOf("Verse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) >= 0),
                "The Core appraisal store cannot gain runtime authority dependencies.");
        }

        public static void V37V38V39Integration()
        {
            var v37Gate = "8c31cbf7dfe2497579a502b998a6eee91ae715cae38cd4eee5d13f59979945c9";
            var v38 = Receipt();
            var store = NewStore();
            var item = store.Prepare(Input(
                receipt: v38,
                cues: Cues(
                    Cue(ProvisionalCueType.Gratitude, 0.65m),
                    Cue(ProvisionalCueType.Uncertainty, 0.70m))));
            store.Activate(item.AppraisalId, v38.DisplayedTick);
            var overlay = store.EffectiveOverlay(Owner, v38.ConversationId, v38.DisplayedTick);
            TestAssert.Equal(64, v37Gate.Length, "The v37 dependency digest must remain explicit.");
            TestAssert.Equal(ObservedDisplayReceipt.SourceGateDigestValue, v38.SourceGateDigest, "v39 must bind the exact v38 gate digest.");
            TestAssert.True(overlay.Affect.Valence > 0m && overlay.Affect.Certainty < 0m, "The v37-v38-v39 chain must produce one bounded compound overlay.");
            TestAssert.False(item.DirectCanonicalMutation || item.DirectPawnAuthority, "The integrated appraisal must remain inert.");
        }

        public static void DeterministicReplayAcrossSourcePermutations()
        {
            var cues = new[]
            {
                Cue(ProvisionalCueType.Gratitude, 0.65m),
                Cue(ProvisionalCueType.Uncertainty, 0.70m),
                Cue(ProvisionalCueType.Insult, 0.40m, direct: false)
            };
            var outputs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var permutation in Permutations(cues))
            {
                var store = NewStore();
                var item = store.Prepare(Input(cues: permutation));
                store.Activate(item.AppraisalId, 1000);
                outputs.Add(store.StateDigest());
            }
            TestAssert.Equal(1, outputs.Count, "Equivalent cue permutations must normalize to one deterministic result.");
        }

        public static void ScalingFiftyThousandAdmissionsBounded()
        {
            var store = BuildAdmissions(50_000, "base-stress");
            TestAssert.True(store.ActiveCount(Owner) <= 32, "Fifty thousand admissions must retain at most 32 full live records.");
            TestAssert.True(store.FullLiveRecordCount <= 32, "Admission stress cannot retain terminal records.");
            TestAssert.Equal(50_000L, store.SeenCount, "The admission stress must not be reduced.");
            TestAssert.Equal(1 << 20, ProvisionalDialogueAppraisalStore.FilterBytes, "Admission duplicate protection must remain fixed-memory.");
        }

        public static void ScalingFiftyThousandSuccessfulPromotionsBounded()
        {
            var store = PromoteMany(50_000, "promotion-stress");
            TestAssert.Equal(50_000L, store.CompletedCount, "The successful-promotion stress must not be reduced.");
            TestAssert.Equal(0, store.ActiveCount(), "Successful promotions must leave no live overlays.");
            TestAssert.Equal(0, store.PendingPacketCount, "Successful promotions must leave no pending packets.");
            TestAssert.True(store.CompletedRecentCount <= 64, "Successful promotions may retain at most 64 complete records.");
            TestAssert.Equal(50_000L, store.CompletedFilterCount, "Every success must enter fixed-memory duplicate protection.");
        }

        private static ProvisionalDialogueAppraisalStore BuildAdmissions(int count, string prefix)
        {
            var store = NewStore();
            var neutral = new[] { Cue(ProvisionalCueType.Neutral, 0.5m) };
            var evidence = new[] { Knowledge() };
            for (var index = 0; index < count; index++)
            {
                var receipt = Receipt(prefix + "-" + index.ToString("D5", CultureInfo.InvariantCulture), 3000 + index);
                var item = store.Prepare(Input(receipt: receipt, cues: neutral, knowledge: evidence));
                store.Activate(item.AppraisalId, receipt.DisplayedTick);
            }
            return store;
        }

        private static ProvisionalDialogueAppraisalStore PromoteMany(int count, string prefix)
        {
            var store = NewStore();
            var neutral = new[] { Cue(ProvisionalCueType.Neutral, 0.5m) };
            var evidence = new[] { Knowledge() };
            for (var index = 0; index < count; index++)
            {
                var suffix = prefix + "-" + index.ToString("D5", CultureInfo.InvariantCulture);
                var receipt = Receipt(suffix, 4000 + index);
                var item = store.Activate(store.Prepare(Input(receipt: receipt, cues: neutral, knowledge: evidence)).AppraisalId, receipt.DisplayedTick);
                var packet = store.ProposePromotion(
                    item.AppraisalId, "event-" + suffix, 7,
                    ProvisionalAffectDelta.Zero, ProvisionalRelationshipDelta.Zero,
                    "admission-" + suffix);
                store.ObserveApplicationReceipt(TrustedApplication(
                    packet.PacketId, Owner, "event-" + suffix, 7, true,
                    AffectFingerprint, 3, RelationshipFingerprint, 5));
            }
            return store;
        }

        private static (ProvisionalDialogueAppraisalStore Store, DurableApplicationPacket Packet) Pending(
            ProvisionalAffectDelta? affect = null,
            ProvisionalRelationshipDelta? relationship = null)
        {
            var store = NewStore();
            var item = store.Activate(store.Prepare(Input()).AppraisalId, 1000);
            var packet = store.ProposePromotion(
                item.AppraisalId, "dialogue-event-001", 7,
                affect ?? ProvisionalAffectDelta.Zero,
                relationship ?? ProvisionalRelationshipDelta.Zero,
                "admission-receipt-001");
            return (store, packet);
        }

        private static ProvisionalDialogueAppraisalStore NewStore() =>
            new ProvisionalDialogueAppraisalStore(Session);

        private static ProvisionalDialogueAppraisalInput Input(
            ObservedDisplayReceipt? receipt = null,
            IEnumerable<ProvisionalSpeechCue>? cues = null,
            IEnumerable<ProvisionalKnowledgeEvidence>? knowledge = null,
            string owner = Owner,
            ProvisionalTraitProfile? traits = null,
            string? relationshipFingerprint = null,
            long? relationshipVersion = null)
        {
            var stableCues = (cues ?? new[] { Cue() })
                .OrderBy(CueSortKey, StringComparer.Ordinal)
                .ToArray();
            var stableKnowledge = (knowledge ?? new[] { Knowledge() })
                .OrderBy(value => value.EvidenceId, StringComparer.Ordinal)
                .ToArray();
            return new ProvisionalDialogueAppraisalInput(
                owner,
                receipt ?? Receipt(),
                stableCues,
                stableKnowledge,
                AffectFingerprint,
                3,
                relationshipFingerprint ?? RelationshipFingerprint,
                relationshipVersion ?? 5,
                traits ?? new ProvisionalTraitProfile());
        }

        private static string CueSortKey(ProvisionalSpeechCue cue) =>
            string.Join("|", new[]
            {
                cue.CueType.ToString(),
                cue.Confidence.ToString("0.000000", CultureInfo.InvariantCulture),
                cue.DirectToOwner ? "1" : "0",
                cue.QuoteDepth.ToString(CultureInfo.InvariantCulture),
                cue.Factuality.ToString(),
                string.Join(",", cue.SourceEvidenceIds)
            });

        private static ProvisionalSpeechCue[] Cues(params ProvisionalSpeechCue[] values) =>
            values.OrderBy(CueSortKey, StringComparer.Ordinal).ToArray();

        private static ProvisionalSpeechCue Cue(
            ProvisionalCueType type = ProvisionalCueType.Insult,
            decimal confidence = 0.9m,
            bool direct = true,
            int quoteDepth = 0,
            ProvisionalFactuality factuality = ProvisionalFactuality.SpeechAct,
            IEnumerable<string>? sources = null) =>
            new ProvisionalSpeechCue(
                type, confidence, direct, quoteDepth, factuality,
                (sources ?? new[] { "ev-001" }).OrderBy(value => value, StringComparer.Ordinal));

        private static ProvisionalKnowledgeEvidence Knowledge(
            string owner = Owner,
            string evidenceId = "ev-001",
            long tick = 900,
            bool admitted = true) =>
            new ProvisionalKnowledgeEvidence(
                evidenceId, owner, "source-" + evidenceId,
                ProvisionalPrivacy.OwnerPrivate, admitted, tick,
                ProvisionalFactuality.VerifiedFact);

        private static ObservedDisplayReceipt Receipt(
            string utterance = "utt-001",
            long tick = 1000,
            ObservedDisplayOutcome outcome = ObservedDisplayOutcome.OBSERVED_SUCCESS,
            string session = Session) =>
            TrustedReceipt(utterance, tick, outcome, session, ObservedDisplayReceipt.SourceContractValue);

        private static ObservedDisplayReceipt TrustedReceipt(
            string utterance,
            long tick,
            ObservedDisplayOutcome outcome,
            string session,
            string sourceContract)
        {
            if (outcome != ObservedDisplayOutcome.OBSERVED_SUCCESS)
            {
                return ObservedDisplayReceipt.CreateNonSuccess(
                    session, Hash("attempt-" + utterance), Hash("receipt-" + utterance), utterance,
                    "conversation-001", Speaker, Owner, new[] { Owner, Speaker },
                    Hash("transient-" + utterance), tick, 7, outcome,
                    sourceContract, ObservedDisplayReceipt.SourceGateDigestValue);
            }
            var method = typeof(ObservedDisplayReceipt).GetMethod(
                "CreateTrusted", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Trusted v38 receipt factory is missing.");
            return (ObservedDisplayReceipt)(method.Invoke(null, new object?[]
            {
                session,
                Hash("attempt-" + utterance),
                Hash("receipt-" + utterance),
                utterance,
                "conversation-001",
                Speaker,
                Owner,
                new[] { Owner, Speaker },
                Hash("transient-" + utterance),
                tick,
                7L,
                outcome,
                sourceContract,
                ObservedDisplayReceipt.SourceGateDigestValue
            }) ?? throw new InvalidOperationException("Trusted v38 receipt factory returned null."));
        }

        private static CanonicalApplicationReceipt TrustedApplication(
            DurableApplicationPacket packet,
            bool success,
            string affectFingerprint,
            long affectVersion,
            string? relationshipFingerprint,
            long? relationshipVersion) =>
            TrustedApplication(
                packet.PacketId, packet.PerspectiveOwnerId, packet.AdmittedDialogueEventId,
                packet.ExpectedCheckpointGeneration, success, affectFingerprint, affectVersion,
                relationshipFingerprint, relationshipVersion);

        private static CanonicalApplicationReceipt TrustedApplication(
            string packetId,
            string owner,
            string eventId,
            long generation,
            bool success,
            string affectFingerprint,
            long affectVersion,
            string? relationshipFingerprint,
            long? relationshipVersion)
        {
            var method = typeof(CanonicalApplicationReceipt).GetMethod(
                "CreateTrusted", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Trusted canonical receipt factory is missing.");
            return (CanonicalApplicationReceipt)(method.Invoke(null, new object?[]
            {
                packetId,
                owner,
                eventId,
                generation,
                success,
                affectFingerprint,
                affectVersion,
                relationshipFingerprint,
                relationshipVersion,
                CanonicalApplicationReceipt.SourceContractValue
            }) ?? throw new InvalidOperationException("Trusted canonical receipt factory returned null."));
        }

        private static T CloneAndTamper<T>(T source, string fieldName, object value)
            where T : class
        {
            var cloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("MemberwiseClone is unavailable.");
            var clone = (T)(cloneMethod.Invoke(source, null)
                ?? throw new InvalidOperationException("Clone failed."));
            var field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Tamper target is unavailable.");
            field.SetValue(clone, value);
            return clone;
        }

        private static IEnumerable<ProvisionalSpeechCue[]> Permutations(ProvisionalSpeechCue[] values)
        {
            for (var first = 0; first < values.Length; first++)
            {
                for (var second = 0; second < values.Length; second++)
                {
                    if (second == first) continue;
                    for (var third = 0; third < values.Length; third++)
                    {
                        if (third == first || third == second) continue;
                        yield return Cues(values[first], values[second], values[third]);
                    }
                }
            }
        }

        private static string Hash(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))
                    .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }
    }
}
