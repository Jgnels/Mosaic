using System;

namespace Dagmay.Core.Contracts
{
    public readonly struct IndividualId : IEquatable<IndividualId>
    {
        public IndividualId(Guid value)
        {
            if (value == Guid.Empty) throw new ArgumentException("Individual ID cannot be empty.", nameof(value));
            Value = value;
        }

        public Guid Value { get; }
        public static IndividualId New() => new IndividualId(Guid.NewGuid());
        public static IndividualId Parse(string value) => new IndividualId(Guid.Parse(value));
        public bool Equals(IndividualId other) => Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is IndividualId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(IndividualId left, IndividualId right) => left.Equals(right);
        public static bool operator !=(IndividualId left, IndividualId right) => !left.Equals(right);
    }

    public readonly struct LineageId : IEquatable<LineageId>
    {
        public LineageId(Guid value)
        {
            if (value == Guid.Empty) throw new ArgumentException("Lineage ID cannot be empty.", nameof(value));
            Value = value;
        }

        public Guid Value { get; }
        public static LineageId New() => new LineageId(Guid.NewGuid());
        public static LineageId Parse(string value) => new LineageId(Guid.Parse(value));
        public bool Equals(LineageId other) => Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is LineageId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(LineageId left, LineageId right) => left.Equals(right);
        public static bool operator !=(LineageId left, LineageId right) => !left.Equals(right);
    }

    public readonly struct EventId : IEquatable<EventId>
    {
        public EventId(Guid value)
        {
            if (value == Guid.Empty) throw new ArgumentException("Event ID cannot be empty.", nameof(value));
            Value = value;
        }

        public Guid Value { get; }
        public static EventId New() => new EventId(Guid.NewGuid());
        public static EventId Parse(string value) => new EventId(Guid.Parse(value));
        public bool Equals(EventId other) => Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is EventId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(EventId left, EventId right) => left.Equals(right);
        public static bool operator !=(EventId left, EventId right) => !left.Equals(right);
    }

    public readonly struct PerceptionId : IEquatable<PerceptionId>
    {
        public PerceptionId(Guid value)
        {
            if (value == Guid.Empty) throw new ArgumentException("Perception ID cannot be empty.", nameof(value));
            Value = value;
        }

        public Guid Value { get; }
        public static PerceptionId New() => new PerceptionId(Guid.NewGuid());
        public static PerceptionId Parse(string value) => new PerceptionId(Guid.Parse(value));
        public bool Equals(PerceptionId other) => Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is PerceptionId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(PerceptionId left, PerceptionId right) => left.Equals(right);
        public static bool operator !=(PerceptionId left, PerceptionId right) => !left.Equals(right);
    }

    public readonly struct MemoryId : IEquatable<MemoryId>
    {
        public MemoryId(Guid value)
        {
            if (value == Guid.Empty) throw new ArgumentException("Memory ID cannot be empty.", nameof(value));
            Value = value;
        }

        public Guid Value { get; }
        public static MemoryId New() => new MemoryId(Guid.NewGuid());
        public static MemoryId Parse(string value) => new MemoryId(Guid.Parse(value));
        public bool Equals(MemoryId other) => Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is MemoryId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(MemoryId left, MemoryId right) => left.Equals(right);
        public static bool operator !=(MemoryId left, MemoryId right) => !left.Equals(right);
    }

    public readonly struct BeliefId : IEquatable<BeliefId>
    {
        public BeliefId(Guid value)
        {
            if (value == Guid.Empty) throw new ArgumentException("Belief ID cannot be empty.", nameof(value));
            Value = value;
        }

        public Guid Value { get; }
        public static BeliefId New() => new BeliefId(Guid.NewGuid());
        public bool Equals(BeliefId other) => Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is BeliefId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(BeliefId left, BeliefId right) => left.Equals(right);
        public static bool operator !=(BeliefId left, BeliefId right) => !left.Equals(right);
    }

    public readonly struct RequestId : IEquatable<RequestId>
    {
        public RequestId(Guid value)
        {
            if (value == Guid.Empty) throw new ArgumentException("Request ID cannot be empty.", nameof(value));
            Value = value;
        }

        public Guid Value { get; }
        public static RequestId New() => new RequestId(Guid.NewGuid());
        public static RequestId Parse(string value) => new RequestId(Guid.Parse(value));
        public bool Equals(RequestId other) => Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is RequestId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(RequestId left, RequestId right) => left.Equals(right);
        public static bool operator !=(RequestId left, RequestId right) => !left.Equals(right);
    }

    public readonly struct ReflectionTaskId : IEquatable<ReflectionTaskId>
    {
        public ReflectionTaskId(Guid value)
        {
            if (value == Guid.Empty) throw new ArgumentException("Reflection task ID cannot be empty.", nameof(value));
            Value = value;
        }

        public Guid Value { get; }
        public static ReflectionTaskId New() => new ReflectionTaskId(Guid.NewGuid());
        public static ReflectionTaskId Parse(string value) => new ReflectionTaskId(Guid.Parse(value));
        public bool Equals(ReflectionTaskId other) => Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is ReflectionTaskId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(ReflectionTaskId left, ReflectionTaskId right) => left.Equals(right);
        public static bool operator !=(ReflectionTaskId left, ReflectionTaskId right) => !left.Equals(right);
    }

    public readonly struct ReflectionRecordId : IEquatable<ReflectionRecordId>
    {
        public ReflectionRecordId(Guid value)
        {
            if (value == Guid.Empty) throw new ArgumentException("Reflection record ID cannot be empty.", nameof(value));
            Value = value;
        }

        public Guid Value { get; }
        public static ReflectionRecordId New() => new ReflectionRecordId(Guid.NewGuid());
        public static ReflectionRecordId Parse(string value) => new ReflectionRecordId(Guid.Parse(value));
        public bool Equals(ReflectionRecordId other) => Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is ReflectionRecordId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(ReflectionRecordId left, ReflectionRecordId right) => left.Equals(right);
        public static bool operator !=(ReflectionRecordId left, ReflectionRecordId right) => !left.Equals(right);
    }
}
