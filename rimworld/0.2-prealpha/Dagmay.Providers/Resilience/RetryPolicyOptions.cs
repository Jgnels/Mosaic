using System;
using Dagmay.Core.Reflection;

namespace Dagmay.Providers.Resilience
{
    public sealed class RetryPolicyOptions
    {
        public RetryPolicyOptions(int maximumAttempts, TimeSpan initialDelay, TimeSpan maximumDelay)
        {
            if (maximumAttempts < 1 || maximumAttempts > 5) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
            if (initialDelay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(initialDelay));
            if (maximumDelay < initialDelay) throw new ArgumentOutOfRangeException(nameof(maximumDelay));

            MaximumAttempts = maximumAttempts;
            InitialDelay = initialDelay;
            MaximumDelay = maximumDelay;
        }

        public int MaximumAttempts { get; }
        public TimeSpan InitialDelay { get; }
        public TimeSpan MaximumDelay { get; }

        public static RetryPolicyOptions ConservativeDefault { get; } = new RetryPolicyOptions(
            maximumAttempts: 3,
            initialDelay: TimeSpan.FromSeconds(1),
            maximumDelay: TimeSpan.FromSeconds(8));

        public bool IsTransient(ModelResultStatus status)
        {
            return status == ModelResultStatus.TimedOut
                || status == ModelResultStatus.RateLimited
                || status == ModelResultStatus.ProviderError;
        }
    }
}

