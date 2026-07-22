using System;
using System.Collections.Generic;
using System.Linq;

namespace Dagmay.Core.Scheduling
{
    public enum ReflectionDispatchBlockReason
    {
        None,
        SessionRequestLimit,
        HourlyRequestLimit,
        DailyTokenLimit,
        CircuitOpen
    }

    public sealed class ReflectionDispatchDecision
    {
        public ReflectionDispatchDecision(
            ReflectionDispatchBlockReason blockReason,
            DateTimeOffset? retryAtUtc,
            string diagnostic)
        {
            BlockReason = blockReason;
            RetryAtUtc = retryAtUtc;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public ReflectionDispatchBlockReason BlockReason { get; }
        public DateTimeOffset? RetryAtUtc { get; }
        public string Diagnostic { get; }
        public bool IsAllowed => BlockReason == ReflectionDispatchBlockReason.None;
    }

    public sealed class ReflectionBudgetGate
    {
        private readonly ReflectionBudgetPolicy _policy;
        private readonly int _circuitFailureThreshold;
        private readonly TimeSpan _circuitCooldown;

        public ReflectionBudgetGate(
            ReflectionBudgetPolicy policy,
            int circuitFailureThreshold = 3,
            TimeSpan? circuitCooldown = null)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            if (circuitFailureThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(circuitFailureThreshold));
            _circuitFailureThreshold = circuitFailureThreshold;
            _circuitCooldown = circuitCooldown ?? TimeSpan.FromMinutes(5);
            if (_circuitCooldown <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(circuitCooldown));
        }

        public ReflectionDispatchDecision Evaluate(
            DateTimeOffset nowUtc,
            PendingReflectionTask task,
            IEnumerable<ReflectionAuditRecord> records,
            int sessionAttemptCount = 0)
        {
            if (task is null) throw new ArgumentNullException(nameof(task));
            if (records is null) throw new ArgumentNullException(nameof(records));
            if (sessionAttemptCount < 0) throw new ArgumentOutOfRangeException(nameof(sessionAttemptCount));
            var values = records.OrderBy(value => value.OccurredAtUtc).ToList();

            if (sessionAttemptCount >= _policy.MaximumRequestsPerSession)
            {
                return new ReflectionDispatchDecision(
                    ReflectionDispatchBlockReason.SessionRequestLimit,
                    null,
                    "The per-game-session request budget is exhausted; queued work remains eligible after restart or a settings change.");
            }

            var circuit = CircuitRetryAt(values, nowUtc);
            if (circuit.HasValue)
            {
                return new ReflectionDispatchDecision(
                    ReflectionDispatchBlockReason.CircuitOpen,
                    circuit,
                    "The provider circuit is cooling down after repeated transient failures.");
            }

            var hourStart = nowUtc.Subtract(TimeSpan.FromHours(1));
            var attempts = values.Count(value =>
                value.Status == ReflectionAuditStatus.AttemptStarted
                && value.OccurredAtUtc > hourStart
                && value.OccurredAtUtc <= nowUtc);
            if (attempts >= _policy.MaximumRequestsPerHour)
            {
                var first = values
                    .Where(value => value.Status == ReflectionAuditStatus.AttemptStarted && value.OccurredAtUtc > hourStart)
                    .OrderBy(value => value.OccurredAtUtc)
                    .First();
                return new ReflectionDispatchDecision(
                    ReflectionDispatchBlockReason.HourlyRequestLimit,
                    first.OccurredAtUtc.AddHours(1),
                    "The conservative hourly request budget is exhausted.");
            }

            var dayStart = new DateTimeOffset(nowUtc.UtcDateTime.Date, TimeSpan.Zero);
            var estimatedToday = values
                .Where(value => value.Status == ReflectionAuditStatus.AttemptStarted && value.OccurredAtUtc >= dayStart)
                .Sum(value => value.EstimatedTokens);
            if (estimatedToday + task.Task.EstimatedTokens > _policy.MaximumTokensPerDay)
            {
                return new ReflectionDispatchDecision(
                    ReflectionDispatchBlockReason.DailyTokenLimit,
                    dayStart.AddDays(1),
                    "The conservative daily estimated-token budget is exhausted.");
            }

            return new ReflectionDispatchDecision(ReflectionDispatchBlockReason.None, null, "Dispatch is within configured budgets.");
        }

        public static TimeSpan RetryDelay(int completedAttemptCount, TimeSpan? providerRetryAfter = null)
        {
            if (completedAttemptCount < 1) throw new ArgumentOutOfRangeException(nameof(completedAttemptCount));
            var exponent = Math.Min(6, completedAttemptCount - 1);
            var seconds = Math.Min(60, Math.Pow(2, exponent));
            var backoff = TimeSpan.FromSeconds(seconds);
            if (providerRetryAfter.HasValue && providerRetryAfter.Value > backoff)
            {
                backoff = providerRetryAfter.Value;
            }

            return backoff > TimeSpan.FromMinutes(10) ? TimeSpan.FromMinutes(10) : backoff;
        }

        private DateTimeOffset? CircuitRetryAt(IReadOnlyList<ReflectionAuditRecord> records, DateTimeOffset nowUtc)
        {
            var failures = 0;
            DateTimeOffset? latestFailure = null;
            for (var index = records.Count - 1; index >= 0; index--)
            {
                var value = records[index];
                if (value.Status == ReflectionAuditStatus.Committed) break;
                if (value.Status != ReflectionAuditStatus.AttemptFailed) continue;
                if (!value.Retryable) break;
                failures++;
                if (!latestFailure.HasValue) latestFailure = value.OccurredAtUtc;
                if (failures >= _circuitFailureThreshold) break;
            }

            if (failures < _circuitFailureThreshold || !latestFailure.HasValue) return null;
            var retryAt = latestFailure.Value.Add(_circuitCooldown);
            return retryAt > nowUtc ? retryAt : (DateTimeOffset?)null;
        }
    }
}
