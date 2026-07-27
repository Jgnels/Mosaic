using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Diagnostics
{
    /// <summary>
    /// Read-only operations that may emit an observer-facing explanation record.
    /// These values do not grant mutation, provider, persistence-write, or pawn authority.
    /// </summary>
    public enum ReadOnlyDecisionOperation
    {
        Retrieval = 0,
        GroundedDialogueSelection = 1,
        ConversationContinuitySelection = 2,
        HistoryProjection = 3,
        AdmissionEvaluation = 4
    }

    public enum DecisionCandidateDisposition
    {
        Accepted = 0,
        Rejected = 1
    }

    /// <summary>
    /// One evidence candidate considered by a read-only decision.
    /// Rank is one-based for accepted candidates and zero for rejected candidates.
    /// </summary>
    public sealed class DecisionTraceCandidate
    {
        public DecisionTraceCandidate(
            EventId evidenceId,
            string candidateKind,
            long occurredAtTick,
            double score,
            DecisionCandidateDisposition disposition,
            string reasonCode,
            int rank)
        {
            if (evidenceId.Value == Guid.Empty)
                throw new ArgumentException("Decision-trace evidence ID cannot be empty.", nameof(evidenceId));
            if (string.IsNullOrWhiteSpace(candidateKind) || candidateKind.Length > 128)
                throw new ArgumentException("Decision-trace candidate kind must be bounded.", nameof(candidateKind));
            if (occurredAtTick < 0)
                throw new ArgumentOutOfRangeException(nameof(occurredAtTick));
            if (double.IsNaN(score) || double.IsInfinity(score))
                throw new ArgumentOutOfRangeException(nameof(score));
            if (!Enum.IsDefined(typeof(DecisionCandidateDisposition), disposition))
                throw new ArgumentOutOfRangeException(nameof(disposition));
            if (string.IsNullOrWhiteSpace(reasonCode) || reasonCode.Length > 128)
                throw new ArgumentException("Decision-trace reason code must be bounded.", nameof(reasonCode));
            if (disposition == DecisionCandidateDisposition.Accepted && (rank < 1 || rank > 256))
                throw new ArgumentOutOfRangeException(nameof(rank));
            if (disposition == DecisionCandidateDisposition.Rejected && rank != 0)
                throw new ArgumentOutOfRangeException(nameof(rank));

            EvidenceId = evidenceId;
            CandidateKind = candidateKind.Trim();
            OccurredAtTick = occurredAtTick;
            Score = score;
            Disposition = disposition;
            ReasonCode = reasonCode.Trim();
            Rank = rank;
        }

        public EventId EvidenceId { get; }
        public string CandidateKind { get; }
        public long OccurredAtTick { get; }
        public double Score { get; }
        public DecisionCandidateDisposition Disposition { get; }
        public string ReasonCode { get; }
        public int Rank { get; }
    }

    /// <summary>
    /// Immutable, deterministic explanation of a read-only decision.
    /// The equal before/after fingerprints are a constructor-enforced observer-purity boundary.
    /// </summary>
    public sealed class ReadOnlyDecisionTrace
    {
        internal ReadOnlyDecisionTrace(
            string traceId,
            IndividualId individualId,
            ReadOnlyDecisionOperation operation,
            long tick,
            string mechanism,
            string mechanismVersion,
            IEnumerable<DecisionTraceCandidate> candidates,
            string result,
            string tieBreakReason,
            string beforeCanonicalHash,
            string afterCanonicalHash)
        {
            if (!IsLowerHex(traceId, 64))
                throw new ArgumentException("Trace ID must be a lowercase SHA-256 value.", nameof(traceId));
            if (individualId.Value == Guid.Empty)
                throw new ArgumentException("Decision trace requires an individual.", nameof(individualId));
            if (!Enum.IsDefined(typeof(ReadOnlyDecisionOperation), operation))
                throw new ArgumentOutOfRangeException(nameof(operation));
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick));
            if (string.IsNullOrWhiteSpace(mechanism) || mechanism.Length > 128)
                throw new ArgumentException("Decision-trace mechanism must be bounded.", nameof(mechanism));
            if (string.IsNullOrWhiteSpace(mechanismVersion) || mechanismVersion.Length > 64)
                throw new ArgumentException("Decision-trace mechanism version must be bounded.", nameof(mechanismVersion));
            if (string.IsNullOrWhiteSpace(result) || result.Length > 512)
                throw new ArgumentException("Decision-trace result must be bounded.", nameof(result));
            if (string.IsNullOrWhiteSpace(tieBreakReason) || tieBreakReason.Length > 256)
                throw new ArgumentException("Decision-trace tie-break explanation must be bounded.", nameof(tieBreakReason));
            if (!IsLowerHex(beforeCanonicalHash, 64) || !IsLowerHex(afterCanonicalHash, 64))
                throw new ArgumentException("Canonical fingerprints must be lowercase SHA-256 values.");
            if (!string.Equals(beforeCanonicalHash, afterCanonicalHash, StringComparison.Ordinal))
                throw new InvalidOperationException("A read-only decision trace cannot describe canonical mutation.");

            var normalized = (candidates ?? throw new ArgumentNullException(nameof(candidates))).ToArray();
            if (normalized.Length < 1 || normalized.Length > 256)
                throw new ArgumentOutOfRangeException(nameof(candidates));
            if (normalized.Any(value => value is null))
                throw new ArgumentException("Decision-trace candidates cannot contain null values.", nameof(candidates));
            if (normalized.Select(value => value.EvidenceId).Distinct().Count() != normalized.Length)
                throw new ArgumentException("Decision-trace candidates require unique evidence IDs.", nameof(candidates));

            var acceptedRanks = normalized
                .Where(value => value.Disposition == DecisionCandidateDisposition.Accepted)
                .Select(value => value.Rank)
                .OrderBy(value => value)
                .ToArray();
            for (var index = 0; index < acceptedRanks.Length; index++)
            {
                if (acceptedRanks[index] != index + 1)
                    throw new ArgumentException("Accepted decision-trace ranks must be contiguous and one-based.", nameof(candidates));
            }

            TraceId = traceId;
            IndividualId = individualId;
            Operation = operation;
            Tick = tick;
            Mechanism = mechanism.Trim();
            MechanismVersion = mechanismVersion.Trim();
            Candidates = new ReadOnlyCollection<DecisionTraceCandidate>(normalized.ToList());
            Result = result.Trim();
            TieBreakReason = tieBreakReason.Trim();
            BeforeCanonicalHash = beforeCanonicalHash;
            AfterCanonicalHash = afterCanonicalHash;
            AcceptedEvidenceIds = new ReadOnlyCollection<EventId>(
                normalized
                    .Where(value => value.Disposition == DecisionCandidateDisposition.Accepted)
                    .OrderBy(value => value.Rank)
                    .Select(value => value.EvidenceId)
                    .ToList());
            RejectedEvidenceIds = new ReadOnlyCollection<EventId>(
                normalized
                    .Where(value => value.Disposition == DecisionCandidateDisposition.Rejected)
                    .Select(value => value.EvidenceId)
                    .ToList());
        }

        public string TraceId { get; }
        public IndividualId IndividualId { get; }
        public ReadOnlyDecisionOperation Operation { get; }
        public long Tick { get; }
        public string Mechanism { get; }
        public string MechanismVersion { get; }
        public IReadOnlyList<DecisionTraceCandidate> Candidates { get; }
        public IReadOnlyList<EventId> AcceptedEvidenceIds { get; }
        public IReadOnlyList<EventId> RejectedEvidenceIds { get; }
        public string Result { get; }
        public string TieBreakReason { get; }
        public string BeforeCanonicalHash { get; }
        public string AfterCanonicalHash { get; }
        public bool PreservesCanonicalState =>
            string.Equals(BeforeCanonicalHash, AfterCanonicalHash, StringComparison.Ordinal);

        private static bool IsLowerHex(string value, int length)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != length)
                return false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Produces a canonical trace independent of caller enumeration order.
    /// This builder owns no canonical state and performs no writes.
    /// </summary>
    public sealed class ReadOnlyDecisionTraceBuilder
    {
        public const int MaximumCandidates = 256;

        public ReadOnlyDecisionTrace Build(
            IndividualId individualId,
            ReadOnlyDecisionOperation operation,
            long tick,
            string mechanism,
            string mechanismVersion,
            IEnumerable<DecisionTraceCandidate> candidates,
            string result,
            string tieBreakReason,
            string canonicalFingerprint) =>
            BuildWithFingerprints(
                individualId,
                operation,
                tick,
                mechanism,
                mechanismVersion,
                candidates,
                result,
                tieBreakReason,
                canonicalFingerprint,
                canonicalFingerprint);

        public ReadOnlyDecisionTrace BuildWithFingerprints(
            IndividualId individualId,
            ReadOnlyDecisionOperation operation,
            long tick,
            string mechanism,
            string mechanismVersion,
            IEnumerable<DecisionTraceCandidate> candidates,
            string result,
            string tieBreakReason,
            string beforeCanonicalFingerprint,
            string afterCanonicalFingerprint)
        {
            if (candidates is null)
                throw new ArgumentNullException(nameof(candidates));

            var ordered = candidates
                .Select(value => value ?? throw new ArgumentException(
                    "Decision-trace candidates cannot contain null values.",
                    nameof(candidates)))
                .OrderBy(value => value.Disposition == DecisionCandidateDisposition.Accepted ? 0 : 1)
                .ThenBy(value => value.Disposition == DecisionCandidateDisposition.Accepted
                    ? value.Rank
                    : int.MaxValue)
                .ThenByDescending(value => value.Score)
                .ThenByDescending(value => value.OccurredAtTick)
                .ThenBy(value => value.EvidenceId.ToString(), StringComparer.Ordinal)
                .ToArray();

            if (ordered.Length < 1 || ordered.Length > MaximumCandidates)
                throw new ArgumentOutOfRangeException(nameof(candidates));

            var canonical = Encode(
                individualId,
                operation,
                tick,
                mechanism,
                mechanismVersion,
                ordered,
                result,
                tieBreakReason,
                beforeCanonicalFingerprint,
                afterCanonicalFingerprint);
            var traceId = Sha256(canonical);

            return new ReadOnlyDecisionTrace(
                traceId,
                individualId,
                operation,
                tick,
                mechanism,
                mechanismVersion,
                ordered,
                result,
                tieBreakReason,
                beforeCanonicalFingerprint,
                afterCanonicalFingerprint);
        }

        private static string Encode(
            IndividualId individualId,
            ReadOnlyDecisionOperation operation,
            long tick,
            string mechanism,
            string mechanismVersion,
            IReadOnlyList<DecisionTraceCandidate> candidates,
            string result,
            string tieBreakReason,
            string beforeCanonicalFingerprint,
            string afterCanonicalFingerprint)
        {
            var builder = new StringBuilder();
            Append(builder, individualId.ToString());
            Append(builder, ((int)operation).ToString(CultureInfo.InvariantCulture));
            Append(builder, tick.ToString(CultureInfo.InvariantCulture));
            Append(builder, mechanism);
            Append(builder, mechanismVersion);
            Append(builder, result);
            Append(builder, tieBreakReason);
            Append(builder, beforeCanonicalFingerprint);
            Append(builder, afterCanonicalFingerprint);
            Append(builder, candidates.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var candidate in candidates)
            {
                Append(builder, candidate.EvidenceId.ToString());
                Append(builder, candidate.CandidateKind);
                Append(builder, candidate.OccurredAtTick.ToString(CultureInfo.InvariantCulture));
                Append(builder, candidate.Score.ToString("R", CultureInfo.InvariantCulture));
                Append(builder, ((int)candidate.Disposition).ToString(CultureInfo.InvariantCulture));
                Append(builder, candidate.ReasonCode);
                Append(builder, candidate.Rank.ToString(CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string value)
        {
            var normalized = value ?? string.Empty;
            builder.Append(normalized.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(normalized);
            builder.Append('|');
        }

        private static string Sha256(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var valueByte in bytes)
                    builder.Append(valueByte.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }
    }
}
