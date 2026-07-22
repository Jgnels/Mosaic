using System;
using System.Collections.Generic;
using Dagmay.Core.Contracts;
using Dagmay.Core.Reflection;

namespace Dagmay.Core.Scheduling
{
    public enum ReflectionPriority
    {
        Background = 10,
        Consolidation = 25,
        MeaningfulEvent = 60,
        CriticalLifecycle = 100
    }

    public sealed class ReflectionBudgetPolicy
    {
        public ReflectionBudgetPolicy(
            int maximumQueueSize,
            int maximumConcurrentRequests,
            int maximumRequestsPerHour,
            int maximumTokensPerDay,
            TimeSpan schedulerHeartbeat,
            int maximumRequestsPerSession = 4)
        {
            if (maximumQueueSize <= 0) throw new ArgumentOutOfRangeException(nameof(maximumQueueSize));
            if (maximumConcurrentRequests <= 0) throw new ArgumentOutOfRangeException(nameof(maximumConcurrentRequests));
            if (maximumRequestsPerHour <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRequestsPerHour));
            if (maximumTokensPerDay <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTokensPerDay));
            if (schedulerHeartbeat <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(schedulerHeartbeat));
            if (maximumRequestsPerSession <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRequestsPerSession));

            MaximumQueueSize = maximumQueueSize;
            MaximumConcurrentRequests = maximumConcurrentRequests;
            MaximumRequestsPerHour = maximumRequestsPerHour;
            MaximumTokensPerDay = maximumTokensPerDay;
            SchedulerHeartbeat = schedulerHeartbeat;
            MaximumRequestsPerSession = maximumRequestsPerSession;
        }

        public int MaximumQueueSize { get; }
        public int MaximumConcurrentRequests { get; }
        public int MaximumRequestsPerHour { get; }
        public int MaximumTokensPerDay { get; }
        public TimeSpan SchedulerHeartbeat { get; }
        public int MaximumRequestsPerSession { get; }

        public static ReflectionBudgetPolicy ConservativePersonalDefault { get; } = new ReflectionBudgetPolicy(
            maximumQueueSize: 500,
            maximumConcurrentRequests: 1,
            maximumRequestsPerHour: 12,
            maximumTokensPerDay: 40_000,
            schedulerHeartbeat: TimeSpan.FromMinutes(1),
            maximumRequestsPerSession: 4);
    }

    public sealed class ReflectionTask
    {
        public ReflectionTask(
            ReflectionTaskId id,
            IndividualId individualId,
            ModelTaskKind taskKind,
            ReflectionPriority priority,
            DateTimeOffset createdAtUtc,
            string coalescingKey,
            IEnumerable<EventId> sourceEventIds,
            int estimatedTokens)
        {
            if (estimatedTokens <= 0) throw new ArgumentOutOfRangeException(nameof(estimatedTokens));

            Id = id;
            IndividualId = individualId;
            TaskKind = taskKind;
            Priority = priority;
            CreatedAtUtc = createdAtUtc;
            CoalescingKey = ContractGuard.Text(coalescingKey, nameof(coalescingKey), 256);
            SourceEventIds = ContractGuard.List(sourceEventIds, nameof(sourceEventIds));
            EstimatedTokens = estimatedTokens;
        }

        public ReflectionTaskId Id { get; }
        public IndividualId IndividualId { get; }
        public ModelTaskKind TaskKind { get; }
        public ReflectionPriority Priority { get; }
        public DateTimeOffset CreatedAtUtc { get; }
        public string CoalescingKey { get; }
        public IReadOnlyList<EventId> SourceEventIds { get; }
        public int EstimatedTokens { get; }
    }

    public enum QueueEnqueueStatus
    {
        Enqueued,
        Duplicate,
        RejectedAtCapacity,
        EnqueuedAfterEvictingLowerPriority
    }

    public sealed class QueueEnqueueResult
    {
        public QueueEnqueueResult(QueueEnqueueStatus status, ReflectionTaskId? evictedTaskId)
        {
            Status = status;
            EvictedTaskId = evictedTaskId;
        }

        public QueueEnqueueStatus Status { get; }
        public ReflectionTaskId? EvictedTaskId { get; }
    }

    public sealed class ReflectionQueue
    {
        private readonly object _gate = new object();
        private readonly int _capacity;
        private readonly List<ReflectionTask> _tasks = new List<ReflectionTask>();
        private readonly HashSet<ReflectionTaskId> _ids = new HashSet<ReflectionTaskId>();
        private readonly HashSet<string> _coalescingKeys = new HashSet<string>(StringComparer.Ordinal);

        public ReflectionQueue(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public int Count
        {
            get
            {
                lock (_gate) return _tasks.Count;
            }
        }

        public QueueEnqueueResult Enqueue(ReflectionTask task)
        {
            if (task is null) throw new ArgumentNullException(nameof(task));

            lock (_gate)
            {
                if (_ids.Contains(task.Id) || _coalescingKeys.Contains(task.CoalescingKey))
                {
                    return new QueueEnqueueResult(QueueEnqueueStatus.Duplicate, null);
                }

                if (_tasks.Count < _capacity)
                {
                    Add(task);
                    return new QueueEnqueueResult(QueueEnqueueStatus.Enqueued, null);
                }

                var lowestIndex = FindLowestPriorityIndex();
                var lowest = _tasks[lowestIndex];
                if ((int)task.Priority <= (int)lowest.Priority)
                {
                    return new QueueEnqueueResult(QueueEnqueueStatus.RejectedAtCapacity, null);
                }

                RemoveAt(lowestIndex);
                Add(task);
                return new QueueEnqueueResult(QueueEnqueueStatus.EnqueuedAfterEvictingLowerPriority, lowest.Id);
            }
        }

        public bool TryDequeue(DateTimeOffset nowUtc, out ReflectionTask? task)
        {
            lock (_gate)
            {
                if (_tasks.Count == 0)
                {
                    task = null;
                    return false;
                }

                var selectedIndex = 0;
                var selectedScore = EffectivePriority(_tasks[0], nowUtc);
                for (var index = 1; index < _tasks.Count; index++)
                {
                    var score = EffectivePriority(_tasks[index], nowUtc);
                    if (score > selectedScore
                        || (score == selectedScore && _tasks[index].CreatedAtUtc < _tasks[selectedIndex].CreatedAtUtc))
                    {
                        selectedIndex = index;
                        selectedScore = score;
                    }
                }

                task = _tasks[selectedIndex];
                RemoveAt(selectedIndex);
                return true;
            }
        }

        private static int EffectivePriority(ReflectionTask task, DateTimeOffset nowUtc)
        {
            var age = nowUtc > task.CreatedAtUtc ? nowUtc - task.CreatedAtUtc : TimeSpan.Zero;
            var agingBonus = Math.Min(30, (int)Math.Floor(age.TotalMinutes / 5));
            return (int)task.Priority + agingBonus;
        }

        private int FindLowestPriorityIndex()
        {
            var selected = 0;
            for (var index = 1; index < _tasks.Count; index++)
            {
                if ((int)_tasks[index].Priority < (int)_tasks[selected].Priority
                    || (_tasks[index].Priority == _tasks[selected].Priority
                        && _tasks[index].CreatedAtUtc > _tasks[selected].CreatedAtUtc))
                {
                    selected = index;
                }
            }

            return selected;
        }

        private void Add(ReflectionTask task)
        {
            _tasks.Add(task);
            _ids.Add(task.Id);
            _coalescingKeys.Add(task.CoalescingKey);
        }

        private void RemoveAt(int index)
        {
            var task = _tasks[index];
            _tasks.RemoveAt(index);
            _ids.Remove(task.Id);
            _coalescingKeys.Remove(task.CoalescingKey);
        }
    }
}
