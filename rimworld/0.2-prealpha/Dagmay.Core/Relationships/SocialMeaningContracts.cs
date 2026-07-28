using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Relationships
{
    /// <summary>
    /// Directed, perspective-owned social meaning. A-to-B and B-to-A are distinct.
    /// </summary>
    public sealed class DirectedSocialKey : IEquatable<DirectedSocialKey>
    {
        public DirectedSocialKey(IndividualId perspectiveOwnerId, IndividualId targetId)
        {
            if (perspectiveOwnerId.Value == Guid.Empty)
                throw new ArgumentException("Perspective owner cannot be empty.", nameof(perspectiveOwnerId));
            if (targetId.Value == Guid.Empty)
                throw new ArgumentException("Target cannot be empty.", nameof(targetId));
            if (perspectiveOwnerId.Equals(targetId))
                throw new ArgumentException("Directed social meaning cannot target self.", nameof(targetId));

            PerspectiveOwnerId = perspectiveOwnerId;
            TargetId = targetId;
        }

        public IndividualId PerspectiveOwnerId { get; }
        public IndividualId TargetId { get; }

        public bool Equals(DirectedSocialKey? other) =>
            other is not null
            && PerspectiveOwnerId.Equals(other.PerspectiveOwnerId)
            && TargetId.Equals(other.TargetId);

        public override bool Equals(object? obj) => Equals(obj as DirectedSocialKey);

        public override int GetHashCode()
        {
            unchecked
            {
                return (PerspectiveOwnerId.GetHashCode() * 397) ^ TargetId.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Bounded multidimensional social state inspired by social-importance models
    /// without collapsing relationships into one scalar.
    /// </summary>
    public sealed class SocialMeaningVector : IEquatable<SocialMeaningVector>
    {
        public SocialMeaningVector(
            double trust,
            double affection,
            double fear,
            double resentment,
            double respect,
            double obligation,
            double familiarity)
        {
            Trust = Validate(trust, nameof(trust));
            Affection = Validate(affection, nameof(affection));
            Fear = Validate(fear, nameof(fear));
            Resentment = Validate(resentment, nameof(resentment));
            Respect = Validate(respect, nameof(respect));
            Obligation = Validate(obligation, nameof(obligation));
            Familiarity = Validate(familiarity, nameof(familiarity));
        }

        public double Trust { get; }
        public double Affection { get; }
        public double Fear { get; }
        public double Resentment { get; }
        public double Respect { get; }
        public double Obligation { get; }
        public double Familiarity { get; }

        public static SocialMeaningVector Neutral { get; } =
            new SocialMeaningVector(0, 0, 0, 0, 0, 0, 0);

        public SocialMeaningVector AddBounded(SocialMeaningVector delta)
        {
            if (delta is null) throw new ArgumentNullException(nameof(delta));
            return new SocialMeaningVector(
                Add(Trust, delta.Trust),
                Add(Affection, delta.Affection),
                Add(Fear, delta.Fear),
                Add(Resentment, delta.Resentment),
                Add(Respect, delta.Respect),
                Add(Obligation, delta.Obligation),
                Add(Familiarity, delta.Familiarity));
        }

        public bool Equals(SocialMeaningVector? other) =>
            other is not null
            && Trust.Equals(other.Trust)
            && Affection.Equals(other.Affection)
            && Fear.Equals(other.Fear)
            && Resentment.Equals(other.Resentment)
            && Respect.Equals(other.Respect)
            && Obligation.Equals(other.Obligation)
            && Familiarity.Equals(other.Familiarity);

        public override bool Equals(object? obj) => Equals(obj as SocialMeaningVector);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + Trust.GetHashCode();
                hash = (hash * 31) + Affection.GetHashCode();
                hash = (hash * 31) + Fear.GetHashCode();
                hash = (hash * 31) + Resentment.GetHashCode();
                hash = (hash * 31) + Respect.GetHashCode();
                hash = (hash * 31) + Obligation.GetHashCode();
                hash = (hash * 31) + Familiarity.GetHashCode();
                return hash;
            }
        }

        private static double Validate(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < -1d || value > 1d)
                throw new ArgumentOutOfRangeException(parameterName, "Social meaning dimensions must be finite values from -1 to 1.");
            return value;
        }

        private static double Add(double left, double right) =>
            Math.Max(-1d, Math.Min(1d, left + right));
    }

    /// <summary>
    /// Immutable proposal only. A later Mosaic-owned validation service decides whether
    /// a proposal may become canonical state.
    /// </summary>
    public sealed class SocialMeaningProposal
    {
        public const int MaximumRuleIds = 32;

        public SocialMeaningProposal(
            string proposalId,
            DirectedSocialKey key,
            EventId evidenceId,
            long tick,
            long expectedVersion,
            string sourceFingerprint,
            SocialMeaningVector delta,
            IEnumerable<string> stableRuleIds,
            string explanation)
        {
            ProposalId = LowerHex(proposalId, nameof(proposalId));
            Key = key ?? throw new ArgumentNullException(nameof(key));
            if (evidenceId.Value == Guid.Empty)
                throw new ArgumentException("Social meaning proposal requires evidence.", nameof(evidenceId));
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            if (expectedVersion < 0) throw new ArgumentOutOfRangeException(nameof(expectedVersion));

            EvidenceId = evidenceId;
            Tick = tick;
            ExpectedVersion = expectedVersion;
            SourceFingerprint = LowerHex(sourceFingerprint, nameof(sourceFingerprint));
            Delta = delta ?? throw new ArgumentNullException(nameof(delta));

            var rules = (stableRuleIds ?? throw new ArgumentNullException(nameof(stableRuleIds)))
                .Select(value => ContractGuard.Text(value, nameof(stableRuleIds), 128))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (rules.Length < 1 || rules.Length > MaximumRuleIds)
                throw new ArgumentOutOfRangeException(nameof(stableRuleIds));
            if (rules.Distinct(StringComparer.Ordinal).Count() != rules.Length)
                throw new ArgumentException("Stable rule IDs must be unique.", nameof(stableRuleIds));

            StableRuleIds = new ReadOnlyCollection<string>(rules);
            Explanation = ContractGuard.Text(explanation, nameof(explanation), 512);
        }

        public string ProposalId { get; }
        public DirectedSocialKey Key { get; }
        public EventId EvidenceId { get; }
        public long Tick { get; }
        public long ExpectedVersion { get; }
        public string SourceFingerprint { get; }
        public SocialMeaningVector Delta { get; }
        public IReadOnlyList<string> StableRuleIds { get; }
        public string Explanation { get; }

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
    }
}
