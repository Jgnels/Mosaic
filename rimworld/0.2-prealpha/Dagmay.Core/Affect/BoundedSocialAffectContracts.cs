using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Dagmay.Core.Appraisal;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Affect
{
    public sealed class FixedAffectVector
    {
        public const int Scale = 1_000_000;
        private static readonly IReadOnlyDictionary<string, int> DecayPerTick =
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [nameof(Valence)] = 80,
                [nameof(Arousal)] = 170,
                [nameof(ThreatSafety)] = 100,
                [nameof(AgencyControl)] = 45,
                [nameof(AttachmentAffiliation)] = 18,
                [nameof(CertaintyConfusion)] = 40,
                [nameof(SocialStanding)] = 22
            });

        public FixedAffectVector(
            int valence = 0,
            int arousal = 0,
            int threatSafety = 0,
            int agencyControl = 0,
            int attachmentAffiliation = 0,
            int certaintyConfusion = 0,
            int socialStanding = 0)
        {
            Valence = Bound(valence, nameof(valence));
            Arousal = Bound(arousal, nameof(arousal));
            ThreatSafety = Bound(threatSafety, nameof(threatSafety));
            AgencyControl = Bound(agencyControl, nameof(agencyControl));
            AttachmentAffiliation = Bound(attachmentAffiliation, nameof(attachmentAffiliation));
            CertaintyConfusion = Bound(certaintyConfusion, nameof(certaintyConfusion));
            SocialStanding = Bound(socialStanding, nameof(socialStanding));
        }

        public int Valence { get; }
        public int Arousal { get; }
        public int ThreatSafety { get; }
        public int AgencyControl { get; }
        public int AttachmentAffiliation { get; }
        public int CertaintyConfusion { get; }
        public int SocialStanding { get; }

        public FixedAffectVector Add(FixedAffectVector other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            return new FixedAffectVector(
                SaturatingAdd(Valence, other.Valence),
                SaturatingAdd(Arousal, other.Arousal),
                SaturatingAdd(ThreatSafety, other.ThreatSafety),
                SaturatingAdd(AgencyControl, other.AgencyControl),
                SaturatingAdd(AttachmentAffiliation, other.AttachmentAffiliation),
                SaturatingAdd(CertaintyConfusion, other.CertaintyConfusion),
                SaturatingAdd(SocialStanding, other.SocialStanding));
        }

        public FixedAffectVector Decay(long elapsedTicks)
        {
            if (elapsedTicks < 0) throw new ArgumentOutOfRangeException(nameof(elapsedTicks));
            return new FixedAffectVector(
                DecayOne(Valence, elapsedTicks, DecayPerTick[nameof(Valence)]),
                DecayOne(Arousal, elapsedTicks, DecayPerTick[nameof(Arousal)]),
                DecayOne(ThreatSafety, elapsedTicks, DecayPerTick[nameof(ThreatSafety)]),
                DecayOne(AgencyControl, elapsedTicks, DecayPerTick[nameof(AgencyControl)]),
                DecayOne(AttachmentAffiliation, elapsedTicks, DecayPerTick[nameof(AttachmentAffiliation)]),
                DecayOne(CertaintyConfusion, elapsedTicks, DecayPerTick[nameof(CertaintyConfusion)]),
                DecayOne(SocialStanding, elapsedTicks, DecayPerTick[nameof(SocialStanding)]));
        }

        public string Fingerprint() => GroundedSocialProjectionHash.HashFields(new[]
        {
            GroundedSocialSemanticEvidence.Pair("valence", Valence.ToString(CultureInfo.InvariantCulture)),
            GroundedSocialSemanticEvidence.Pair("arousal", Arousal.ToString(CultureInfo.InvariantCulture)),
            GroundedSocialSemanticEvidence.Pair("threat_safety", ThreatSafety.ToString(CultureInfo.InvariantCulture)),
            GroundedSocialSemanticEvidence.Pair("agency_control", AgencyControl.ToString(CultureInfo.InvariantCulture)),
            GroundedSocialSemanticEvidence.Pair("attachment_affiliation", AttachmentAffiliation.ToString(CultureInfo.InvariantCulture)),
            GroundedSocialSemanticEvidence.Pair("certainty_confusion", CertaintyConfusion.ToString(CultureInfo.InvariantCulture)),
            GroundedSocialSemanticEvidence.Pair("social_standing", SocialStanding.ToString(CultureInfo.InvariantCulture))
        });

        public static FixedAffectVector FromFamilies(
            MosaicEmotionFamily primary,
            MosaicEmotionFamily? secondary,
            int intensity)
        {
            if (intensity < 1 || intensity > 5) throw new ArgumentOutOfRangeException(nameof(intensity));
            var primaryVector = Family(primary);
            var secondaryVector = secondary.HasValue ? Family(secondary.Value) : new FixedAffectVector();
            return new FixedAffectVector(
                Combine(primaryVector.Valence, secondaryVector.Valence, intensity),
                Combine(primaryVector.Arousal, secondaryVector.Arousal, intensity),
                Combine(primaryVector.ThreatSafety, secondaryVector.ThreatSafety, intensity),
                Combine(primaryVector.AgencyControl, secondaryVector.AgencyControl, intensity),
                Combine(primaryVector.AttachmentAffiliation, secondaryVector.AttachmentAffiliation, intensity),
                Combine(primaryVector.CertaintyConfusion, secondaryVector.CertaintyConfusion, intensity),
                Combine(primaryVector.SocialStanding, secondaryVector.SocialStanding, intensity));
        }

        private static FixedAffectVector Family(MosaicEmotionFamily family)
        {
            switch (family)
            {
                case MosaicEmotionFamily.PositiveWellbeing: return new FixedAffectVector(700000, 180000, -180000, 120000, 120000, 100000, 120000);
                case MosaicEmotionFamily.NegativeWellbeing: return new FixedAffectVector(-750000, 280000, 360000, -220000, -120000, -140000, -120000);
                case MosaicEmotionFamily.Gratitude: return new FixedAffectVector(650000, 220000, -320000, 80000, 680000, 120000, 80000);
                case MosaicEmotionFamily.Admiration: return new FixedAffectVector(520000, 180000, -100000, 80000, 360000, 100000, 260000);
                case MosaicEmotionFamily.Reproach: return new FixedAffectVector(-420000, 300000, 260000, 100000, -260000, 80000, 120000);
                case MosaicEmotionFamily.Anger: return new FixedAffectVector(-650000, 780000, 520000, 180000, -300000, 80000, 160000);
                case MosaicEmotionFamily.Betrayal: return new FixedAffectVector(-900000, 700000, 680000, -420000, -820000, -120000, -260000);
                case MosaicEmotionFamily.Heartbreak: return new FixedAffectVector(-920000, 520000, 420000, -500000, -900000, -180000, -220000);
                case MosaicEmotionFamily.Love: return new FixedAffectVector(900000, 340000, -500000, 120000, 950000, 180000, 100000);
                case MosaicEmotionFamily.Trust: return new FixedAffectVector(620000, 100000, -650000, 180000, 720000, 300000, 80000);
                case MosaicEmotionFamily.Resentment: return new FixedAffectVector(-620000, 480000, 500000, 120000, -560000, 100000, 80000);
                case MosaicEmotionFamily.Affection: return new FixedAffectVector(650000, 150000, -280000, 80000, 780000, 150000, 100000);
                case MosaicEmotionFamily.Suspicion: return new FixedAffectVector(-180000, 300000, 420000, -120000, -260000, -480000, 20000);
                case MosaicEmotionFamily.Uncertainty: return new FixedAffectVector(-40000, 180000, 120000, -180000, 0, -700000, 0);
                case MosaicEmotionFamily.Ambiguity: return new FixedAffectVector(0, 100000, 40000, -80000, 0, -500000, 0);
                case MosaicEmotionFamily.Pride: return new FixedAffectVector(650000, 220000, -120000, 520000, 40000, 180000, 480000);
                default: throw new ArgumentOutOfRangeException(nameof(family));
            }
        }

        private static int Combine(int primary, int secondary, int intensity)
        {
            var weighted = (long)primary * 1_000_000L + (long)secondary * 350_000L;
            return Bound(RoundHalfAway(weighted * intensity, 5_000_000L), nameof(intensity));
        }

        private static int RoundHalfAway(long numerator, long denominator)
        {
            var sign = numerator < 0 ? -1 : 1;
            var magnitude = Math.Abs(numerator);
            var quotient = magnitude / denominator;
            var remainder = magnitude % denominator;
            if (remainder * 2 >= denominator) quotient++;
            return checked((int)(sign * quotient));
        }

        private static int DecayOne(int current, long elapsedTicks, int rate)
        {
            if (current == 0 || elapsedTicks == 0) return current;
            var ticksToScale = (Scale + (long)rate - 1L) / rate;
            var reduction = elapsedTicks >= ticksToScale
                ? Scale
                : checked((int)(elapsedTicks * rate));
            if (current > 0) return Math.Max(0, current - reduction);
            return Math.Min(0, current + reduction);
        }

        private static int SaturatingAdd(int first, int second)
        {
            var sum = (long)first + second;
            return sum > Scale ? Scale : sum < -Scale ? -Scale : (int)sum;
        }

        private static int Bound(int value, string parameterName)
        {
            if (value < -Scale || value > Scale) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    public sealed class BoundedAffectState
    {
        public BoundedAffectState(
            IndividualId individualId,
            LineageId lineageId,
            FixedAffectVector vector,
            long lastTick,
            long lastAdmissionPosition,
            long appliedCount,
            string rollingEvidenceDigest,
            long version)
        {
            if (individualId.Value == Guid.Empty) throw new ArgumentException("IndividualId cannot be empty.", nameof(individualId));
            if (lineageId.Value == Guid.Empty) throw new ArgumentException("LineageId cannot be empty.", nameof(lineageId));
            if (lastTick < 0 || lastAdmissionPosition < 0 || appliedCount < 0 || version < 0)
                throw new ArgumentOutOfRangeException("Affect counters cannot be negative.");
            if (version != appliedCount) throw new ArgumentException("Affect version must equal applied count.", nameof(version));
            IndividualId = individualId;
            LineageId = lineageId;
            Vector = vector ?? throw new ArgumentNullException(nameof(vector));
            LastTick = lastTick;
            LastAdmissionPosition = lastAdmissionPosition;
            AppliedCount = appliedCount;
            RollingEvidenceDigest = GroundedSocialProjectionHash.LowerHex(
                rollingEvidenceDigest,
                nameof(rollingEvidenceDigest));
            Version = version;
        }

        public IndividualId IndividualId { get; }
        public LineageId LineageId { get; }
        public FixedAffectVector Vector { get; }
        public long LastTick { get; }
        public long LastAdmissionPosition { get; }
        public long AppliedCount { get; }
        public string RollingEvidenceDigest { get; }
        public long Version { get; }

        public static BoundedAffectState Initial(IndividualId individualId, LineageId lineageId) =>
            new BoundedAffectState(
                individualId,
                lineageId,
                new FixedAffectVector(),
                0,
                0,
                0,
                new string('0', 64),
                0);

        public string Fingerprint() => GroundedSocialProjectionHash.HashFields(new[]
        {
            GroundedSocialSemanticEvidence.Pair("individual", IndividualId.ToString()),
            GroundedSocialSemanticEvidence.Pair("lineage", LineageId.ToString()),
            GroundedSocialSemanticEvidence.Pair("vector", Vector.Fingerprint()),
            GroundedSocialSemanticEvidence.Pair("last_tick", LastTick.ToString(CultureInfo.InvariantCulture)),
            GroundedSocialSemanticEvidence.Pair("last_position", LastAdmissionPosition.ToString(CultureInfo.InvariantCulture)),
            GroundedSocialSemanticEvidence.Pair("applied_count", AppliedCount.ToString(CultureInfo.InvariantCulture)),
            GroundedSocialSemanticEvidence.Pair("rolling_digest", RollingEvidenceDigest),
            GroundedSocialSemanticEvidence.Pair("version", Version.ToString(CultureInfo.InvariantCulture))
        });
    }

    public sealed class AffectTransitionProposal
    {
        internal AffectTransitionProposal(
            string proposalId,
            IndividualId individualId,
            LineageId lineageId,
            long expectedStateVersion,
            string expectedStateFingerprint,
            long admissionPosition,
            long tick,
            string bundleId,
            IEnumerable<EventId> evidenceIds,
            string appraisalId,
            FixedAffectVector delta,
            string nextRollingEvidenceDigest)
        {
            ProposalId = GroundedSocialProjectionHash.LowerHex(proposalId, nameof(proposalId));
            if (individualId.Value == Guid.Empty || lineageId.Value == Guid.Empty)
                throw new ArgumentException("Proposal identity and lineage are required.");
            if (expectedStateVersion < 0 || admissionPosition < 1 || tick < 0)
                throw new ArgumentOutOfRangeException("Proposal counters are invalid.");
            var normalizedEvidence = (evidenceIds ?? throw new ArgumentNullException(nameof(evidenceIds)))
                .OrderBy(value => value.ToString(), StringComparer.Ordinal)
                .ToArray();
            if (normalizedEvidence.Length < 1 || normalizedEvidence.Length > 2 ||
                normalizedEvidence.Any(value => value.Value == Guid.Empty) ||
                normalizedEvidence.Distinct().Count() != normalizedEvidence.Length)
                throw new ArgumentException("Proposal requires one or two unique evidence IDs.", nameof(evidenceIds));
            IndividualId = individualId;
            LineageId = lineageId;
            ExpectedStateVersion = expectedStateVersion;
            ExpectedStateFingerprint = GroundedSocialProjectionHash.LowerHex(expectedStateFingerprint, nameof(expectedStateFingerprint));
            AdmissionPosition = admissionPosition;
            Tick = tick;
            BundleId = GroundedSocialProjectionHash.LowerHex(bundleId, nameof(bundleId));
            EvidenceIds = new ReadOnlyCollection<EventId>(normalizedEvidence);
            AppraisalId = GroundedSocialProjectionHash.LowerHex(appraisalId, nameof(appraisalId));
            Delta = delta ?? throw new ArgumentNullException(nameof(delta));
            NextRollingEvidenceDigest = GroundedSocialProjectionHash.LowerHex(nextRollingEvidenceDigest, nameof(nextRollingEvidenceDigest));
        }

        public string ProposalId { get; }
        public IndividualId IndividualId { get; }
        public LineageId LineageId { get; }
        public long ExpectedStateVersion { get; }
        public string ExpectedStateFingerprint { get; }
        public long AdmissionPosition { get; }
        public long Tick { get; }
        public string BundleId { get; }
        public IReadOnlyList<EventId> EvidenceIds { get; }
        public string AppraisalId { get; }
        public FixedAffectVector Delta { get; }
        public string NextRollingEvidenceDigest { get; }
        public bool DirectCharacterMutation => false;
        public bool DirectActionAuthority => false;

        internal string ComputeIntegrityId() => GroundedSocialAffectReducer.ComputeProposalId(
            IndividualId,
            LineageId,
            ExpectedStateVersion,
            ExpectedStateFingerprint,
            AdmissionPosition,
            Tick,
            BundleId,
            EvidenceIds,
            AppraisalId,
            Delta,
            NextRollingEvidenceDigest);
    }

    public static class GroundedSocialAffectReducer
    {
        public static AffectTransitionProposal Propose(
            BoundedAffectState state,
            GroundedSocialAppraisalProposal appraisal)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (appraisal is null) throw new ArgumentNullException(nameof(appraisal));
            var bundle = appraisal.Bundle;
            if (bundle.PerspectiveOwnerId != state.IndividualId)
                throw new ArgumentException("Foreign appraisal owner.", nameof(appraisal));
            if (bundle.CommitAdmissionPosition <= state.LastAdmissionPosition)
                throw new ArgumentException("Affect admission position must advance.", nameof(appraisal));
            if (bundle.Tick < state.LastTick)
                throw new ArgumentException("Affect transitions cannot travel backward in time.", nameof(appraisal));
            var nextDigest = ComputeNextDigest(
                state,
                bundle.CommitAdmissionPosition,
                bundle.BundleId,
                bundle.SourceEvidenceIds,
                appraisal.AppraisalId);
            var proposalId = ComputeProposalId(
                state.IndividualId,
                state.LineageId,
                state.Version,
                state.Fingerprint(),
                bundle.CommitAdmissionPosition,
                bundle.Tick,
                bundle.BundleId,
                bundle.SourceEvidenceIds,
                appraisal.AppraisalId,
                appraisal.AffectDelta,
                nextDigest);
            return new AffectTransitionProposal(
                proposalId,
                state.IndividualId,
                state.LineageId,
                state.Version,
                state.Fingerprint(),
                bundle.CommitAdmissionPosition,
                bundle.Tick,
                bundle.BundleId,
                bundle.SourceEvidenceIds,
                appraisal.AppraisalId,
                appraisal.AffectDelta,
                nextDigest);
        }

        public static BoundedAffectState Commit(
            BoundedAffectState state,
            AffectTransitionProposal proposal)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (proposal is null) throw new ArgumentNullException(nameof(proposal));
            if (!string.Equals(proposal.ProposalId, proposal.ComputeIntegrityId(), StringComparison.Ordinal))
                throw new ArgumentException("Affect proposal integrity mismatch.", nameof(proposal));
            if (proposal.IndividualId != state.IndividualId || proposal.LineageId != state.LineageId)
                throw new ArgumentException("Foreign identity or lineage.", nameof(proposal));
            if (proposal.ExpectedStateVersion != state.Version ||
                !string.Equals(proposal.ExpectedStateFingerprint, state.Fingerprint(), StringComparison.Ordinal))
                throw new ArgumentException("Stale affect proposal.", nameof(proposal));
            if (proposal.AdmissionPosition <= state.LastAdmissionPosition || proposal.Tick < state.LastTick)
                throw new ArgumentException("Stale or backward affect proposal.", nameof(proposal));
            var expectedDigest = ComputeNextDigest(
                state,
                proposal.AdmissionPosition,
                proposal.BundleId,
                proposal.EvidenceIds,
                proposal.AppraisalId);
            if (!string.Equals(expectedDigest, proposal.NextRollingEvidenceDigest, StringComparison.Ordinal))
                throw new ArgumentException("Affect proposal rolling digest mismatch.", nameof(proposal));
            var decayed = state.Vector.Decay(proposal.Tick - state.LastTick);
            return new BoundedAffectState(
                state.IndividualId,
                state.LineageId,
                decayed.Add(proposal.Delta),
                proposal.Tick,
                proposal.AdmissionPosition,
                checked(state.AppliedCount + 1),
                proposal.NextRollingEvidenceDigest,
                checked(state.Version + 1));
        }

        internal static string ComputeProposalId(
            IndividualId individualId,
            LineageId lineageId,
            long expectedStateVersion,
            string expectedStateFingerprint,
            long admissionPosition,
            long tick,
            string bundleId,
            IEnumerable<EventId> evidenceIds,
            string appraisalId,
            FixedAffectVector delta,
            string nextRollingEvidenceDigest)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                GroundedSocialSemanticEvidence.Pair("individual", individualId.ToString()),
                GroundedSocialSemanticEvidence.Pair("lineage", lineageId.ToString()),
                GroundedSocialSemanticEvidence.Pair("expected_version", expectedStateVersion.ToString(CultureInfo.InvariantCulture)),
                GroundedSocialSemanticEvidence.Pair("expected_fingerprint", expectedStateFingerprint),
                GroundedSocialSemanticEvidence.Pair("position", admissionPosition.ToString(CultureInfo.InvariantCulture)),
                GroundedSocialSemanticEvidence.Pair("tick", tick.ToString(CultureInfo.InvariantCulture)),
                GroundedSocialSemanticEvidence.Pair("bundle", bundleId),
                GroundedSocialSemanticEvidence.Pair("appraisal", appraisalId),
                GroundedSocialSemanticEvidence.Pair("delta", delta.Fingerprint()),
                GroundedSocialSemanticEvidence.Pair("next_digest", nextRollingEvidenceDigest)
            };
            foreach (var evidence in evidenceIds.OrderBy(value => value.ToString(), StringComparer.Ordinal))
                fields.Add(GroundedSocialSemanticEvidence.Pair("evidence", evidence.ToString()));
            return GroundedSocialProjectionHash.HashFields(fields);
        }

        private static string ComputeNextDigest(
            BoundedAffectState state,
            long admissionPosition,
            string bundleId,
            IEnumerable<EventId> evidenceIds,
            string appraisalId)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                GroundedSocialSemanticEvidence.Pair("previous", state.RollingEvidenceDigest),
                GroundedSocialSemanticEvidence.Pair("position", admissionPosition.ToString(CultureInfo.InvariantCulture)),
                GroundedSocialSemanticEvidence.Pair("bundle", bundleId),
                GroundedSocialSemanticEvidence.Pair("appraisal", appraisalId)
            };
            foreach (var evidence in evidenceIds.OrderBy(value => value.ToString(), StringComparer.Ordinal))
                fields.Add(GroundedSocialSemanticEvidence.Pair("evidence", evidence.ToString()));
            return GroundedSocialProjectionHash.HashFields(fields);
        }
    }

    public sealed class BoundedAffectCheckpoint
    {
        private BoundedAffectCheckpoint(IReadOnlyList<BoundedAffectState> states, string checkpointHash)
        {
            States = states;
            CheckpointHash = checkpointHash;
        }

        public IReadOnlyList<BoundedAffectState> States { get; }
        public string CheckpointHash { get; }

        public static BoundedAffectCheckpoint Create(IEnumerable<BoundedAffectState> states)
        {
            var values = (states ?? throw new ArgumentNullException(nameof(states))).ToArray();
            if (values.Any(value => value is null))
                throw new ArgumentException("Checkpoint states must be nonnull.", nameof(states));
            var normalized = values
                .OrderBy(value => value.IndividualId.ToString(), StringComparer.Ordinal)
                .ToArray();
            if (normalized.Select(value => value.IndividualId).Distinct().Count() != normalized.Length)
                throw new ArgumentException("Checkpoint state identities must be unique.", nameof(states));
            var hash = ComputeHash(normalized);
            return new BoundedAffectCheckpoint(
                new ReadOnlyCollection<BoundedAffectState>(normalized),
                hash);
        }

        public static BoundedAffectCheckpoint Restore(
            IEnumerable<BoundedAffectState> states,
            string expectedCheckpointHash)
        {
            var checkpoint = Create(states);
            var expected = GroundedSocialProjectionHash.LowerHex(
                expectedCheckpointHash,
                nameof(expectedCheckpointHash));
            if (!string.Equals(checkpoint.CheckpointHash, expected, StringComparison.Ordinal))
                throw new ArgumentException("Affect checkpoint hash mismatch.", nameof(expectedCheckpointHash));
            return checkpoint;
        }

        private static string ComputeHash(IEnumerable<BoundedAffectState> states)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                GroundedSocialSemanticEvidence.Pair("schema", "mosaic.bounded-affect-checkpoint.v1")
            };
            foreach (var state in states)
            {
                fields.Add(GroundedSocialSemanticEvidence.Pair("owner", state.IndividualId.ToString()));
                fields.Add(GroundedSocialSemanticEvidence.Pair("state", state.Fingerprint()));
            }
            return GroundedSocialProjectionHash.HashFields(fields);
        }
    }
}
