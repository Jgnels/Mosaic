using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dagmay.Core.Contracts;
using Dagmay.Core.Reflection;

namespace Dagmay.Core.Scheduling
{
    public sealed class PendingReflectionTask
    {
        public PendingReflectionTask(
            ReflectionTask task,
            int attemptCount,
            DateTimeOffset nextAttemptAtUtc,
            string lastFailureCode)
        {
            if (attemptCount < 0 || attemptCount > 100) throw new ArgumentOutOfRangeException(nameof(attemptCount));
            Task = task ?? throw new ArgumentNullException(nameof(task));
            AttemptCount = attemptCount;
            NextAttemptAtUtc = nextAttemptAtUtc;
            LastFailureCode = Optional(lastFailureCode, 128);
        }

        public ReflectionTask Task { get; }
        public int AttemptCount { get; }
        public DateTimeOffset NextAttemptAtUtc { get; }
        public string LastFailureCode { get; }

        public PendingReflectionTask WithRetry(DateTimeOffset nextAttemptAtUtc, string failureCode)
        {
            return new PendingReflectionTask(Task, AttemptCount + 1, nextAttemptAtUtc, failureCode);
        }

        public PendingReflectionTask WithTask(ReflectionTask replacement)
        {
            return new PendingReflectionTask(replacement, AttemptCount, NextAttemptAtUtc, LastFailureCode);
        }

        public PendingReflectionTask Defer(DateTimeOffset nextAttemptAtUtc, string reasonCode)
        {
            return new PendingReflectionTask(Task, AttemptCount, nextAttemptAtUtc, reasonCode);
        }

        private static string Optional(string value, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            if (value.Length > maximum) throw new ArgumentOutOfRangeException(nameof(value));
            return value.Trim();
        }
    }

    public enum ReflectionAuditStatus
    {
        AttemptStarted,
        AttemptFailed,
        PendingCommit,
        Committed,
        Quarantined
    }

    public sealed class ReflectionAuditRecord
    {
        public ReflectionAuditRecord(
            ReflectionRecordId id,
            ReflectionTaskId taskId,
            RequestId requestId,
            IndividualId individualId,
            LineageId lineageId,
            long baseStateVersion,
            ModelTaskKind taskKind,
            string promptVersion,
            DateTimeOffset occurredAtUtc,
            ReflectionAuditStatus status,
            int attemptNumber,
            string provider,
            string model,
            string providerOperationId,
            ModelResultStatus resultStatus,
            string errorCode,
            string errorMessage,
            bool retryable,
            int estimatedTokens,
            int promptTokens,
            int outputTokens,
            int totalTokens,
            string requestContext,
            string structuredPayload,
            string diagnostic)
        {
            if (baseStateVersion < 0) throw new ArgumentOutOfRangeException(nameof(baseStateVersion));
            if (attemptNumber < 1 || attemptNumber > 100) throw new ArgumentOutOfRangeException(nameof(attemptNumber));
            if (estimatedTokens < 0) throw new ArgumentOutOfRangeException(nameof(estimatedTokens));
            if (promptTokens < 0) throw new ArgumentOutOfRangeException(nameof(promptTokens));
            if (outputTokens < 0) throw new ArgumentOutOfRangeException(nameof(outputTokens));
            if (totalTokens < 0) throw new ArgumentOutOfRangeException(nameof(totalTokens));
            if (!Enum.IsDefined(typeof(ReflectionAuditStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (!Enum.IsDefined(typeof(ModelResultStatus), resultStatus)) throw new ArgumentOutOfRangeException(nameof(resultStatus));
            if (!Enum.IsDefined(typeof(ModelTaskKind), taskKind)) throw new ArgumentOutOfRangeException(nameof(taskKind));

            Id = id;
            SchemaVersion = SchemaVersions.ReflectionRecord;
            TaskId = taskId;
            RequestId = requestId;
            IndividualId = individualId;
            LineageId = lineageId;
            BaseStateVersion = baseStateVersion;
            TaskKind = taskKind;
            PromptVersion = Required(promptVersion, 128, nameof(promptVersion));
            OccurredAtUtc = occurredAtUtc;
            Status = status;
            AttemptNumber = attemptNumber;
            Provider = Optional(provider, 128);
            Model = Optional(model, 128);
            ProviderOperationId = Optional(providerOperationId, 256);
            ResultStatus = resultStatus;
            ErrorCode = Optional(errorCode, 128);
            ErrorMessage = Optional(errorMessage, 2000);
            Retryable = retryable;
            EstimatedTokens = estimatedTokens;
            PromptTokens = promptTokens;
            OutputTokens = outputTokens;
            TotalTokens = totalTokens;
            RequestContext = Optional(requestContext, 100_000);
            StructuredPayload = Optional(structuredPayload, 100_000);
            Diagnostic = Optional(diagnostic, 2000);
        }

        public ReflectionRecordId Id { get; }
        public int SchemaVersion { get; }
        public ReflectionTaskId TaskId { get; }
        public RequestId RequestId { get; }
        public IndividualId IndividualId { get; }
        public LineageId LineageId { get; }
        public long BaseStateVersion { get; }
        public ModelTaskKind TaskKind { get; }
        public string PromptVersion { get; }
        public DateTimeOffset OccurredAtUtc { get; }
        public ReflectionAuditStatus Status { get; }
        public int AttemptNumber { get; }
        public string Provider { get; }
        public string Model { get; }
        public string ProviderOperationId { get; }
        public ModelResultStatus ResultStatus { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }
        public bool Retryable { get; }
        public int EstimatedTokens { get; }
        public int PromptTokens { get; }
        public int OutputTokens { get; }
        public int TotalTokens { get; }
        public string RequestContext { get; }
        public string StructuredPayload { get; }
        public string Diagnostic { get; }

        private static string Optional(string value, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            if (value.Length > maximum) throw new ArgumentOutOfRangeException(nameof(value));
            return value.Trim();
        }

        private static string Required(string value, int maximum, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
            if (value.Length > maximum) throw new ArgumentOutOfRangeException(parameterName);
            return value.Trim();
        }
    }

    public sealed class ReflectionStoreSnapshot
    {
        public ReflectionStoreSnapshot(
            Guid storeId,
            long generation,
            DateTimeOffset savedAtUtc,
            IEnumerable<PendingReflectionTask> pendingTasks,
            IEnumerable<ReflectionAuditRecord> auditRecords)
        {
            if (storeId == Guid.Empty) throw new ArgumentException("Store ID cannot be empty.", nameof(storeId));
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            if (pendingTasks is null) throw new ArgumentNullException(nameof(pendingTasks));
            if (auditRecords is null) throw new ArgumentNullException(nameof(auditRecords));

            var tasks = new List<PendingReflectionTask>(pendingTasks);
            var records = new List<ReflectionAuditRecord>(auditRecords);
            if (tasks.Count > 500) throw new ArgumentOutOfRangeException(nameof(pendingTasks));
            if (records.Count > 500) throw new ArgumentOutOfRangeException(nameof(auditRecords));
            if (tasks.Select(value => value.Task.Id).Distinct().Count() != tasks.Count)
            {
                throw new ArgumentException("Reflection task IDs must be unique.", nameof(pendingTasks));
            }

            if (records.Select(value => value.Id).Distinct().Count() != records.Count)
            {
                throw new ArgumentException("Reflection audit record IDs must be unique.", nameof(auditRecords));
            }

            StoreId = storeId;
            Generation = generation;
            SavedAtUtc = savedAtUtc;
            PendingTasks = new ReadOnlyCollection<PendingReflectionTask>(tasks);
            AuditRecords = new ReadOnlyCollection<ReflectionAuditRecord>(records);
        }

        public Guid StoreId { get; }
        public long Generation { get; }
        public DateTimeOffset SavedAtUtc { get; }
        public IReadOnlyList<PendingReflectionTask> PendingTasks { get; }
        public IReadOnlyList<ReflectionAuditRecord> AuditRecords { get; }
    }

    public enum PersistentQueueEnqueueStatus
    {
        Enqueued,
        Merged,
        RejectedAtCapacity,
        EnqueuedAfterEvictingLowerPriority
    }

    public sealed class PersistentReflectionQueue
    {
        private readonly int _capacity;
        private readonly List<PendingReflectionTask> _tasks;

        public PersistentReflectionQueue(int capacity, IEnumerable<PendingReflectionTask>? initial = null)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _tasks = initial is null ? new List<PendingReflectionTask>() : new List<PendingReflectionTask>(initial);
            if (_tasks.Count > capacity) throw new ArgumentOutOfRangeException(nameof(initial));
        }

        public int Count => _tasks.Count;

        public PersistentQueueEnqueueStatus EnqueueOrMerge(ReflectionTask task)
        {
            if (task is null) throw new ArgumentNullException(nameof(task));
            for (var index = 0; index < _tasks.Count; index++)
            {
                if (_tasks[index].Task.IndividualId != task.IndividualId
                    || !string.Equals(_tasks[index].Task.CoalescingKey, task.CoalescingKey, StringComparison.Ordinal))
                {
                    continue;
                }
                var existing = _tasks[index];
                var newTaskWins = (int)task.Priority > (int)existing.Task.Priority;
                var eventIds = (newTaskWins
                        ? task.SourceEventIds.Concat(existing.Task.SourceEventIds)
                        : existing.Task.SourceEventIds.Concat(task.SourceEventIds))
                    .Distinct()
                    .Take(100)
                    .ToList();
                var replacementKind = newTaskWins
                    ? task.TaskKind
                    : existing.Task.TaskKind;
                var replacement = new ReflectionTask(
                    existing.Task.Id,
                    existing.Task.IndividualId,
                    replacementKind,
                    (ReflectionPriority)Math.Max((int)existing.Task.Priority, (int)task.Priority),
                    existing.Task.CreatedAtUtc <= task.CreatedAtUtc ? existing.Task.CreatedAtUtc : task.CreatedAtUtc,
                    existing.Task.CoalescingKey,
                    eventIds,
                    Math.Max(existing.Task.EstimatedTokens, task.EstimatedTokens));
                _tasks[index] = existing.WithTask(replacement);
                return PersistentQueueEnqueueStatus.Merged;
            }

            if (_tasks.Count < _capacity)
            {
                _tasks.Add(new PendingReflectionTask(task, 0, task.CreatedAtUtc, string.Empty));
                return PersistentQueueEnqueueStatus.Enqueued;
            }

            var lowest = FindLowestPriorityIndex();
            if ((int)task.Priority <= (int)_tasks[lowest].Task.Priority)
            {
                return PersistentQueueEnqueueStatus.RejectedAtCapacity;
            }

            _tasks.RemoveAt(lowest);
            _tasks.Add(new PendingReflectionTask(task, 0, task.CreatedAtUtc, string.Empty));
            return PersistentQueueEnqueueStatus.EnqueuedAfterEvictingLowerPriority;
        }

        public bool TrySelect(DateTimeOffset nowUtc, out PendingReflectionTask? task)
        {
            return TrySelect(nowUtc, null, out task);
        }

        public bool TrySelect(
            DateTimeOffset nowUtc,
            IndividualId? lastDispatchedIndividualId,
            out PendingReflectionTask? task)
        {
            var selected = -1;
            var score = int.MinValue;
            for (var index = 0; index < _tasks.Count; index++)
            {
                var candidate = _tasks[index];
                if (candidate.NextAttemptAtUtc > nowUtc) continue;
                var candidateScore = EffectivePriority(candidate.Task, nowUtc);
                var candidateBreaksRepeat = lastDispatchedIndividualId.HasValue
                    && candidate.Task.IndividualId != lastDispatchedIndividualId.Value;
                var selectedBreaksRepeat = selected >= 0
                    && lastDispatchedIndividualId.HasValue
                    && _tasks[selected].Task.IndividualId != lastDispatchedIndividualId.Value;
                if (selected < 0
                    || candidateScore > score
                    || (candidateScore == score && candidateBreaksRepeat && !selectedBreaksRepeat)
                    || (candidateScore == score
                        && candidateBreaksRepeat == selectedBreaksRepeat
                        && candidate.Task.CreatedAtUtc < _tasks[selected].Task.CreatedAtUtc))
                {
                    selected = index;
                    score = candidateScore;
                }
            }

            task = selected < 0 ? null : _tasks[selected];
            return task is not null;
        }

        public bool Remove(ReflectionTaskId id)
        {
            var index = IndexOf(id);
            if (index < 0) return false;
            _tasks.RemoveAt(index);
            return true;
        }

        public bool MarkRetry(ReflectionTaskId id, DateTimeOffset nextAttemptAtUtc, string failureCode)
        {
            var index = IndexOf(id);
            if (index < 0) return false;
            _tasks[index] = _tasks[index].WithRetry(nextAttemptAtUtc, failureCode);
            return true;
        }

        public bool Defer(ReflectionTaskId id, DateTimeOffset nextAttemptAtUtc, string reasonCode)
        {
            var index = IndexOf(id);
            if (index < 0) return false;
            _tasks[index] = _tasks[index].Defer(nextAttemptAtUtc, reasonCode);
            return true;
        }

        public PendingReflectionTask? Find(ReflectionTaskId id)
        {
            var index = IndexOf(id);
            return index < 0 ? null : _tasks[index];
        }

        public IReadOnlyList<PendingReflectionTask> Snapshot()
        {
            return new ReadOnlyCollection<PendingReflectionTask>(new List<PendingReflectionTask>(_tasks));
        }

        public int RemoveForIndividual(IndividualId individualId, ReflectionTaskId? preserveTaskId = null)
        {
            var removed = 0;
            for (var index = _tasks.Count - 1; index >= 0; index--)
            {
                var task = _tasks[index].Task;
                if (task.IndividualId != individualId) continue;
                if (preserveTaskId.HasValue && task.Id == preserveTaskId.Value) continue;
                _tasks.RemoveAt(index);
                removed++;
            }

            return removed;
        }

        private int IndexOf(ReflectionTaskId id)
        {
            for (var index = 0; index < _tasks.Count; index++)
            {
                if (_tasks[index].Task.Id == id) return index;
            }

            return -1;
        }

        private int FindLowestPriorityIndex()
        {
            var selected = 0;
            for (var index = 1; index < _tasks.Count; index++)
            {
                if ((int)_tasks[index].Task.Priority < (int)_tasks[selected].Task.Priority
                    || (_tasks[index].Task.Priority == _tasks[selected].Task.Priority
                        && _tasks[index].Task.CreatedAtUtc > _tasks[selected].Task.CreatedAtUtc))
                {
                    selected = index;
                }
            }

            return selected;
        }

        private static int EffectivePriority(ReflectionTask task, DateTimeOffset nowUtc)
        {
            var age = nowUtc > task.CreatedAtUtc ? nowUtc - task.CreatedAtUtc : TimeSpan.Zero;
            return (int)task.Priority + Math.Min(30, (int)Math.Floor(age.TotalMinutes / 5));
        }
    }
}
