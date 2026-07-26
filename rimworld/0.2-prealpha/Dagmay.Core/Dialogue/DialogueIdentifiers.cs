using System;

namespace Dagmay.Core.Dialogue
{
    /// <summary>
    /// Identifies one scheduling/admission request. It is not a provider request ID
    /// and must remain stable if the request is persisted or retried.
    /// </summary>
    public readonly struct DialogueRequestId : IEquatable<DialogueRequestId>
    {
        public DialogueRequestId(Guid value)
        {
            if (value == Guid.Empty) throw new ArgumentException("Dialogue request ID cannot be empty.", nameof(value));
            Value = value;
        }

        public Guid Value { get; }
        public static DialogueRequestId New() => new DialogueRequestId(Guid.NewGuid());
        public static DialogueRequestId Parse(string value) => new DialogueRequestId(Guid.Parse(value));
        public bool Equals(DialogueRequestId other) => Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is DialogueRequestId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(DialogueRequestId left, DialogueRequestId right) => left.Equals(right);
        public static bool operator !=(DialogueRequestId left, DialogueRequestId right) => !left.Equals(right);
    }

    /// <summary>
    /// Identifies a bounded conversation. A multi-turn conversation creates a new
    /// DialogueRequestId for each expected turn while retaining this ID.
    /// </summary>
    public readonly struct ConversationId : IEquatable<ConversationId>
    {
        public ConversationId(Guid value)
        {
            if (value == Guid.Empty) throw new ArgumentException("Conversation ID cannot be empty.", nameof(value));
            Value = value;
        }

        public Guid Value { get; }
        public static ConversationId New() => new ConversationId(Guid.NewGuid());
        public static ConversationId Parse(string value) => new ConversationId(Guid.Parse(value));
        public bool Equals(ConversationId other) => Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is ConversationId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(ConversationId left, ConversationId right) => left.Equals(right);
        public static bool operator !=(ConversationId left, ConversationId right) => !left.Equals(right);
    }

    /// <summary>
    /// Identifies one proposed or admitted spoken line.
    /// </summary>
    public readonly struct UtteranceId : IEquatable<UtteranceId>
    {
        public UtteranceId(Guid value)
        {
            if (value == Guid.Empty) throw new ArgumentException("Utterance ID cannot be empty.", nameof(value));
            Value = value;
        }

        public Guid Value { get; }
        public static UtteranceId New() => new UtteranceId(Guid.NewGuid());
        public static UtteranceId Parse(string value) => new UtteranceId(Guid.Parse(value));
        public bool Equals(UtteranceId other) => Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is UtteranceId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString("N");
        public static bool operator ==(UtteranceId left, UtteranceId right) => left.Equals(right);
        public static bool operator !=(UtteranceId left, UtteranceId right) => !left.Equals(right);
    }
}
