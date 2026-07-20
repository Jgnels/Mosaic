using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Memory;

namespace Dagmay.Core.Persistence
{
    public sealed class InMemoryEventLedger : IEventLedger
    {
        private readonly object _gate = new object();
        private readonly List<EventLedgerEntry> _entries = new List<EventLedgerEntry>();
        private readonly HashSet<EventId> _ids = new HashSet<EventId>();
        private readonly Dictionary<string, long> _deduplicationPositions =
            new Dictionary<string, long>(StringComparer.Ordinal);

        public EventAppendResult Append(EnvironmentEvent value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            lock (_gate)
            {
                if (_ids.Contains(value.Id))
                {
                    return new EventAppendResult(EventAppendStatus.DuplicateEventId, FindPosition(value.Id));
                }

                if (_deduplicationPositions.TryGetValue(value.DeduplicationKey, out var existingPosition))
                {
                    return new EventAppendResult(EventAppendStatus.DuplicateDeduplicationKey, existingPosition);
                }

                var position = _entries.Count + 1L;
                _entries.Add(new EventLedgerEntry(position, value));
                _ids.Add(value.Id);
                _deduplicationPositions.Add(value.DeduplicationKey, position);
                return new EventAppendResult(EventAppendStatus.Appended, position);
            }
        }

        public bool Contains(EventId id)
        {
            lock (_gate)
            {
                return _ids.Contains(id);
            }
        }

        public IReadOnlyList<EventLedgerEntry> Snapshot()
        {
            lock (_gate)
            {
                return new ReadOnlyCollection<EventLedgerEntry>(new List<EventLedgerEntry>(_entries));
            }
        }

        private long FindPosition(EventId id)
        {
            for (var index = 0; index < _entries.Count; index++)
            {
                if (_entries[index].Value.Id == id) return _entries[index].Position;
            }

            return 0;
        }
    }
}

