using System;
using Dagmay.Core.Affect;
using Dagmay.Core.Contracts;
using Dagmay.Core.Lifecycle;

namespace Dagmay.Core.Identity
{
    public sealed class IndividualState
    {
        private IndividualState(
            IndividualId id,
            LineageId lineageId,
            long version,
            string displayName,
            LifecycleState lifecycle,
            IdentitySeed seed,
            ContinuityProfile continuity,
            AffectVector affect)
        {
            Id = id;
            LineageId = lineageId;
            Version = version;
            DisplayName = RequireName(displayName);
            Lifecycle = lifecycle;
            Seed = seed ?? throw new ArgumentNullException(nameof(seed));
            Continuity = continuity ?? throw new ArgumentNullException(nameof(continuity));
            Affect = affect ?? throw new ArgumentNullException(nameof(affect));
        }

        public IndividualId Id { get; }
        public LineageId LineageId { get; }
        public long Version { get; }
        public string DisplayName { get; }
        public LifecycleState Lifecycle { get; }
        public IdentitySeed Seed { get; }
        public ContinuityProfile Continuity { get; }
        public AffectVector Affect { get; }

        public static IndividualState Create(string displayName, IdentitySeed seed, ContinuityProfile? continuity = null)
        {
            return new IndividualState(
                IndividualId.New(),
                LineageId.New(),
                0,
                displayName,
                LifecycleState.Active,
                seed,
                continuity ?? ContinuityProfile.InitialDefault,
                AffectVector.Neutral);
        }

        public static IndividualState Restore(
            IndividualId id,
            LineageId lineageId,
            long version,
            string displayName,
            LifecycleState lifecycle,
            IdentitySeed seed,
            ContinuityProfile continuity,
            AffectVector affect)
        {
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (!Enum.IsDefined(typeof(LifecycleState), lifecycle))
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycle));
            }

            return new IndividualState(
                id,
                lineageId,
                version,
                displayName,
                lifecycle,
                seed,
                continuity,
                affect);
        }

        public IndividualState Rename(string displayName, long expectedVersion)
        {
            RequireVersion(expectedVersion);
            return new IndividualState(Id, LineageId, Version + 1, displayName, Lifecycle, Seed, Continuity, Affect);
        }

        public IndividualState WithAffect(AffectVector affect, long expectedVersion)
        {
            RequireVersion(expectedVersion);
            if (Lifecycle == LifecycleState.Dead || Lifecycle == LifecycleState.Archived)
            {
                throw new InvalidOperationException("Ordinary affect updates cannot be applied after death.");
            }

            return new IndividualState(Id, LineageId, Version + 1, DisplayName, Lifecycle, Seed, Continuity, affect);
        }

        public IndividualState TransitionLifecycle(
            LifecycleState target,
            LifecycleTransitionKind kind,
            long expectedVersion)
        {
            RequireVersion(expectedVersion);
            if (!LifecycleRules.CanTransition(Lifecycle, target, kind))
            {
                throw new InvalidOperationException($"Lifecycle transition {Lifecycle} -> {target} is not allowed for {kind}.");
            }

            return new IndividualState(Id, LineageId, Version + 1, DisplayName, target, Seed, Continuity, Affect);
        }

        private void RequireVersion(long expectedVersion)
        {
            if (expectedVersion != Version)
            {
                throw new InvalidOperationException($"Stale state: expected version {expectedVersion}, current version {Version}.");
            }
        }

        private static string RequireName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A display name is required.", nameof(value));
            if (value.Length > 256) throw new ArgumentOutOfRangeException(nameof(value));
            return value.Trim();
        }
    }
}
