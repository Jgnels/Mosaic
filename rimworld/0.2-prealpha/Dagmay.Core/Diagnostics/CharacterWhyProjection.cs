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
    /// Pure observer-facing explanation. It contains durable provenance only;
    /// transient UI/window identity is intentionally excluded.
    /// </summary>
    public sealed class CharacterWhyProjection
    {
        internal CharacterWhyProjection(
            string projectionId,
            IndividualId perspectiveOwnerId,
            string subjectId,
            IEnumerable<EventId> evidenceIds,
            IEnumerable<string> factIds,
            IEnumerable<string> ruleIds,
            string explanation,
            string canonicalFingerprint)
        {
            ProjectionId = projectionId;
            PerspectiveOwnerId = perspectiveOwnerId;
            SubjectId = subjectId;
            EvidenceIds = new ReadOnlyCollection<EventId>(evidenceIds.ToList());
            FactIds = new ReadOnlyCollection<string>(factIds.ToList());
            RuleIds = new ReadOnlyCollection<string>(ruleIds.ToList());
            Explanation = explanation;
            CanonicalFingerprint = canonicalFingerprint;
        }

        public string ProjectionId { get; }
        public IndividualId PerspectiveOwnerId { get; }
        public string SubjectId { get; }
        public IReadOnlyList<EventId> EvidenceIds { get; }
        public IReadOnlyList<string> FactIds { get; }
        public IReadOnlyList<string> RuleIds { get; }
        public string Explanation { get; }
        public string CanonicalFingerprint { get; }
    }

    public sealed class CharacterWhyProjectionBuilder
    {
        public const int MaximumEvidenceIds = 64;
        public const int MaximumFactIds = 64;
        public const int MaximumRuleIds = 64;

        public CharacterWhyProjection Build(
            IndividualId perspectiveOwnerId,
            string subjectId,
            IEnumerable<EventId> evidenceIds,
            IEnumerable<string> factIds,
            IEnumerable<string> ruleIds,
            string explanation,
            string canonicalFingerprint)
        {
            if (perspectiveOwnerId.Value == Guid.Empty)
                throw new ArgumentException("Why projection requires a perspective owner.", nameof(perspectiveOwnerId));
            var normalizedSubject = ContractGuard.Text(subjectId, nameof(subjectId), 256);
            var normalizedExplanation = ContractGuard.Text(explanation, nameof(explanation), 1024);
            var normalizedFingerprint = LowerHex(canonicalFingerprint, nameof(canonicalFingerprint));

            var evidence = (evidenceIds ?? throw new ArgumentNullException(nameof(evidenceIds)))
                .OrderBy(value => value.ToString(), StringComparer.Ordinal)
                .ToArray();
            if (evidence.Length < 1 || evidence.Length > MaximumEvidenceIds)
                throw new ArgumentOutOfRangeException(nameof(evidenceIds));
            if (evidence.Any(value => value.Value == Guid.Empty))
                throw new ArgumentException("Why projection evidence cannot contain an empty ID.", nameof(evidenceIds));
            if (evidence.Distinct().Count() != evidence.Length)
                throw new ArgumentException("Why projection evidence IDs must be unique.", nameof(evidenceIds));

            var facts = NormalizeTextIds(factIds, nameof(factIds), MaximumFactIds);
            var rules = NormalizeTextIds(ruleIds, nameof(ruleIds), MaximumRuleIds);

            var canonical = Encode(
                perspectiveOwnerId,
                normalizedSubject,
                evidence,
                facts,
                rules,
                normalizedExplanation,
                normalizedFingerprint);
            return new CharacterWhyProjection(
                Sha256(canonical),
                perspectiveOwnerId,
                normalizedSubject,
                evidence,
                facts,
                rules,
                normalizedExplanation,
                normalizedFingerprint);
        }

        private static string[] NormalizeTextIds(
            IEnumerable<string> values,
            string parameterName,
            int maximum)
        {
            var normalized = (values ?? throw new ArgumentNullException(parameterName))
                .Select(value => ContractGuard.Text(value, parameterName, 128))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (normalized.Length > maximum)
                throw new ArgumentOutOfRangeException(parameterName);
            if (normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Length)
                throw new ArgumentException("Why projection IDs must be unique.", parameterName);
            return normalized;
        }

        private static string Encode(
            IndividualId owner,
            string subject,
            IReadOnlyList<EventId> evidence,
            IReadOnlyList<string> facts,
            IReadOnlyList<string> rules,
            string explanation,
            string fingerprint)
        {
            var builder = new StringBuilder();
            Append(builder, owner.ToString());
            Append(builder, subject);
            Append(builder, explanation);
            Append(builder, fingerprint);
            foreach (var value in evidence) Append(builder, value.ToString());
            foreach (var value in facts) Append(builder, value);
            foreach (var value in rules) Append(builder, value);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string value)
        {
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
            builder.Append('|');
        }

        private static string LowerHex(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
                throw new ArgumentException("Value must be a lowercase SHA-256 string.", parameterName);
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    throw new ArgumentException("Value must be a lowercase SHA-256 string.", parameterName);
                }
            }
            return value;
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
