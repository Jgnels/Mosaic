using System.Collections.Generic;
using Dagmay.Core.Contracts;
using Dagmay.Core.Memory;

namespace Dagmay.Core.Abstractions
{
    public enum EventAppendStatus
    {
        Appended,
        DuplicateEventId,
        DuplicateDeduplicationKey
    }

    public sealed class EventAppendResult
    {
        public EventAppendResult(EventAppendStatus status, long position)
        {
            Status = status;
            Position = position;
        }

        public EventAppendStatus Status { get; }
        public long Position { get; }
    }

    public sealed class EventLedgerEntry
    {
        public EventLedgerEntry(long position, EnvironmentEvent value)
        {
            Position = position;
            Value = value;
        }

        public long Position { get; }
        public EnvironmentEvent Value { get; }
    }

    public interface IEventLedger
    {
        EventAppendResult Append(EnvironmentEvent value);
        bool Contains(EventId id);
        IReadOnlyList<EventLedgerEntry> Snapshot();
    }
}

