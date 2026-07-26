using System;
using System.Collections.Generic;
using System.Linq;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Dialogue
{
    public enum DialogueEnqueueDisposition
    {
        Accepted = 0,
        Coalesced = 1,
        Duplicate = 2,
        RejectedExpired = 3,
        RejectedCapacity = 4
    }

    public sealed class DialogueEnqueueResult
    {
        public DialogueEnqueueResult(
            DialogueEnqueueDisposition disposition,
            DialogueRequestId? evictedRequestId = null)
        {
            Disposition = disposition;
            EvictedRequestId = evictedRequestId;
        }

        public DialogueEnqueueDisposition Disposition { get; }
        public DialogueRequestId? EvictedRequestId { get; }
    }

    /// <summary>
    /// Provider-independent bounded scheduler. It never merges work across individuals,
    /// recipients, expected speakers, trigger kinds, or conversations.
    /// </summary>
    public sealed class DialogueScheduler
    {
        private readonly int _capacity;
        private readonly List<DialogueRequest> _pending = new List<DialogueRequest>();
        private readonly HashSet<DialogueRequestId> _knownRequestIds = new HashSet<DialogueRequestId>();
        private readonly Dictionary<IndividualId, long> _lastServedSequence =
            new Dictionary<IndividualId, long>();
        private long _serveSequence;

        public DialogueScheduler(int capacity)
        {
            if (capacity < 1 || capacity > 1024)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public int Count => _pending.Count;
        public int Capacity => _capacity;

        public IReadOnlyList<DialogueRequest> Snapshot()
        {
            return _pending
                .OrderByDescending(request => request.Priority)
                .ThenBy(request => request.CreatedAtTick)
                .ThenBy(request => request.Id.ToString(), StringComparer.Ordinal)
                .ToArray();
        }

        public DialogueEnqueueResult Enqueue(DialogueRequest request, long currentTick)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));

            RemoveExpired(currentTick);

            if (request.IsExpired(currentTick))
                return new DialogueEnqueueResult(DialogueEnqueueDisposition.RejectedExpired);

            if (_knownRequestIds.Contains(request.Id))
                return new DialogueEnqueueResult(DialogueEnqueueDisposition.Duplicate);

            var existingIndex = FindCoalescingIndex(request);
            if (existingIndex >= 0)
            {
                _pending[existingIndex] = _pending[existingIndex].MergeEvidence(request);
                _knownRequestIds.Add(request.Id);
                return new DialogueEnqueueResult(DialogueEnqueueDisposition.Coalesced);
            }

            if (_pending.Count < _capacity)
            {
                _pending.Add(request);
                _knownRequestIds.Add(request.Id);
                return new DialogueEnqueueResult(DialogueEnqueueDisposition.Accepted);
            }

            var evictionIndex = FindEvictionCandidateIndex();
            var evictionCandidate = _pending[evictionIndex];
            if (request.Priority <= evictionCandidate.Priority)
                return new DialogueEnqueueResult(DialogueEnqueueDisposition.RejectedCapacity);

            _pending.RemoveAt(evictionIndex);
            _pending.Add(request);
            _knownRequestIds.Add(request.Id);
            return new DialogueEnqueueResult(
                DialogueEnqueueDisposition.Accepted,
                evictionCandidate.Id);
        }

        public DialogueRequest? TryDequeue(long currentTick)
        {
            if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));

            RemoveExpired(currentTick);
            if (_pending.Count == 0) return null;

            var selected = _pending
                .OrderByDescending(request => request.Priority)
                .ThenBy(request => LastServed(request.InitiatorId))
                .ThenBy(request => request.CreatedAtTick)
                .ThenBy(request => request.Id.ToString(), StringComparer.Ordinal)
                .First();

            _pending.Remove(selected);
            _serveSequence++;
            _lastServedSequence[selected.InitiatorId] = _serveSequence;
            return selected;
        }

        public int RemoveForIndividual(IndividualId individualId)
        {
            if (individualId.Value == Guid.Empty)
                throw new ArgumentException("Individual ID cannot be empty.", nameof(individualId));

            return _pending.RemoveAll(request =>
                request.InitiatorId == individualId ||
                request.ExpectedSpeakerId == individualId ||
                (request.RecipientId.HasValue && request.RecipientId.Value == individualId));
        }

        public int RemoveExpired(long currentTick)
        {
            if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));
            return _pending.RemoveAll(request => request.IsExpired(currentTick));
        }

        private int FindCoalescingIndex(DialogueRequest incoming)
        {
            for (var index = 0; index < _pending.Count; index++)
            {
                var current = _pending[index];
                if (current.InitiatorId == incoming.InitiatorId &&
                    current.ExpectedSpeakerId == incoming.ExpectedSpeakerId &&
                    current.RecipientId == incoming.RecipientId &&
                    current.ConversationId == incoming.ConversationId &&
                    current.TriggerKind == incoming.TriggerKind &&
                    string.Equals(current.CoalescingKey, incoming.CoalescingKey, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindEvictionCandidateIndex()
        {
            var candidate = _pending
                .Select((request, index) => new { Request = request, Index = index })
                .OrderBy(item => item.Request.Priority)
                .ThenByDescending(item => item.Request.CreatedAtTick)
                .ThenByDescending(item => item.Request.Id.ToString(), StringComparer.Ordinal)
                .First();

            return candidate.Index;
        }

        private long LastServed(IndividualId individualId)
        {
            return _lastServedSequence.TryGetValue(individualId, out var sequence)
                ? sequence
                : long.MinValue;
        }
    }
}
