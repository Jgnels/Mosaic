using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Dagmay.Core.Contracts;

namespace Dagmay.Core.Dialogue
{
    public sealed class DialoguePresentationRow
    {
        public DialoguePresentationRow(
            EventId eventId,
            UtteranceId utteranceId,
            ConversationId conversationId,
            IndividualId speakerId,
            IndividualId? recipientId,
            string displayLabel,
            string text,
            DialoguePriority priority)
        {
            if (eventId.Value == Guid.Empty) throw new ArgumentException("EventId cannot be empty.", nameof(eventId));
            if (utteranceId.Value == Guid.Empty) throw new ArgumentException("UtteranceId cannot be empty.", nameof(utteranceId));
            if (conversationId.Value == Guid.Empty) throw new ArgumentException("ConversationId cannot be empty.", nameof(conversationId));
            if (speakerId.Value == Guid.Empty) throw new ArgumentException("SpeakerId cannot be empty.", nameof(speakerId));
            if (recipientId.HasValue && recipientId.Value.Value == Guid.Empty)
                throw new ArgumentException("RecipientId cannot be empty.", nameof(recipientId));
            if (displayLabel is null || displayLabel.Length > 256)
                throw new ArgumentException("Display label cannot exceed 256 characters.", nameof(displayLabel));
            if (text is null || text.Length > 4000)
                throw new ArgumentException("Presentation text cannot exceed 4000 characters.", nameof(text));
            if (!Enum.IsDefined(typeof(DialoguePriority), priority))
                throw new ArgumentOutOfRangeException(nameof(priority));

            EventId = eventId;
            UtteranceId = utteranceId;
            ConversationId = conversationId;
            SpeakerId = speakerId;
            RecipientId = recipientId;
            DisplayLabel = displayLabel;
            Text = text;
            Priority = priority;
        }

        public EventId EventId { get; }
        public UtteranceId UtteranceId { get; }
        public ConversationId ConversationId { get; }
        public IndividualId SpeakerId { get; }
        public IndividualId? RecipientId { get; }
        public string DisplayLabel { get; }
        public string Text { get; }
        public DialoguePriority Priority { get; }
    }

    public sealed class ActiveDialogueBubble
    {
        public ActiveDialogueBubble(
            DialoguePresentationRow row,
            string wrappedText,
            long activatedAtMilliseconds,
            long expiresAtMilliseconds)
        {
            Row = row ?? throw new ArgumentNullException(nameof(row));
            WrappedText = wrappedText ?? throw new ArgumentNullException(nameof(wrappedText));
            if (activatedAtMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(activatedAtMilliseconds));
            if (expiresAtMilliseconds <= activatedAtMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(expiresAtMilliseconds));
            ActivatedAtMilliseconds = activatedAtMilliseconds;
            ExpiresAtMilliseconds = expiresAtMilliseconds;
        }

        public DialoguePresentationRow Row { get; }
        public string WrappedText { get; }
        public long ActivatedAtMilliseconds { get; }
        public long ExpiresAtMilliseconds { get; }
    }

    public enum DialoguePresentationEnqueueStatus
    {
        Activated = 0,
        Queued = 1,
        QueuedAfterEviction = 2,
        RejectedDuplicate = 3,
        RejectedEmpty = 4,
        RejectedCapacity = 5,
        Inactive = 6,
        Disposed = 7
    }

    public sealed class DialoguePresentationEnqueueResult
    {
        public DialoguePresentationEnqueueResult(
            DialoguePresentationEnqueueStatus status,
            UtteranceId? evictedUtteranceId = null)
        {
            Status = status;
            EvictedUtteranceId = evictedUtteranceId;
        }

        public DialoguePresentationEnqueueStatus Status { get; }
        public UtteranceId? EvictedUtteranceId { get; }
    }

    public sealed class DialogueSpeechBubbleController : IDisposable
    {
        public const int DefaultPerSpeakerQueueCapacity = 3;
        public const int DefaultGlobalQueueCapacity = 32;

        private sealed class QueuedRow
        {
            public QueuedRow(DialoguePresentationRow row, long sequence)
            {
                Row = row;
                Sequence = sequence;
            }

            public DialoguePresentationRow Row { get; }
            public long Sequence { get; }
        }

        private readonly object _gate = new object();
        private readonly int _perSpeakerCapacity;
        private readonly int _globalCapacity;
        private readonly Dictionary<IndividualId, ActiveDialogueBubble> _active =
            new Dictionary<IndividualId, ActiveDialogueBubble>();
        private readonly Dictionary<IndividualId, List<QueuedRow>> _queued =
            new Dictionary<IndividualId, List<QueuedRow>>();
        private readonly HashSet<UtteranceId> _seen = new HashSet<UtteranceId>();
        private long _sequence;
        private bool _activeLifecycle;
        private bool _disposed;

        public DialogueSpeechBubbleController(
            int perSpeakerCapacity = DefaultPerSpeakerQueueCapacity,
            int globalCapacity = DefaultGlobalQueueCapacity)
        {
            if (perSpeakerCapacity < 1) throw new ArgumentOutOfRangeException(nameof(perSpeakerCapacity));
            if (globalCapacity < perSpeakerCapacity) throw new ArgumentOutOfRangeException(nameof(globalCapacity));
            _perSpeakerCapacity = perSpeakerCapacity;
            _globalCapacity = globalCapacity;
        }

        public event Action<ActiveDialogueBubble>? BubbleActivated;
        public event Action<DialoguePresentationRow>? BubbleDismissed;

        public bool IsActive
        {
            get { lock (_gate) return _activeLifecycle && !_disposed; }
        }

        public void Activate()
        {
            lock (_gate)
            {
                if (_disposed || _activeLifecycle) return;
                _activeLifecycle = true;
            }
        }

        public void Deactivate()
        {
            List<DialoguePresentationRow> dismissed;
            lock (_gate)
            {
                if (_disposed || !_activeLifecycle) return;
                _activeLifecycle = false;
                dismissed = _active.Values.Select(value => value.Row)
                    .Concat(_queued.Values.SelectMany(value => value).Select(value => value.Row))
                    .ToList();
                _active.Clear();
                _queued.Clear();
            }
            foreach (var row in dismissed) BubbleDismissed?.Invoke(row);
        }

        public DialoguePresentationEnqueueResult Enqueue(
            DialoguePresentationRow row,
            long nowMilliseconds)
        {
            if (row is null) throw new ArgumentNullException(nameof(row));
            if (nowMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(nowMilliseconds));
            ActiveDialogueBubble? activated = null;
            DialoguePresentationEnqueueResult result;
            lock (_gate)
            {
                if (_disposed)
                    return new DialoguePresentationEnqueueResult(DialoguePresentationEnqueueStatus.Disposed);
                if (!_activeLifecycle)
                    return new DialoguePresentationEnqueueResult(DialoguePresentationEnqueueStatus.Inactive);
                if (string.IsNullOrWhiteSpace(row.Text))
                    return new DialoguePresentationEnqueueResult(DialoguePresentationEnqueueStatus.RejectedEmpty);
                if (!_seen.Add(row.UtteranceId))
                    return new DialoguePresentationEnqueueResult(DialoguePresentationEnqueueStatus.RejectedDuplicate);

                if (!_active.ContainsKey(row.SpeakerId))
                {
                    activated = ActivateRow(row, nowMilliseconds);
                    _active.Add(row.SpeakerId, activated);
                    result = new DialoguePresentationEnqueueResult(DialoguePresentationEnqueueStatus.Activated);
                }
                else
                {
                    result = Queue(row);
                    if (result.Status == DialoguePresentationEnqueueStatus.RejectedCapacity)
                        _seen.Remove(row.UtteranceId);
                }
            }
            if (activated is not null) BubbleActivated?.Invoke(activated);
            return result;
        }

        public void Tick(long nowMilliseconds)
        {
            if (nowMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(nowMilliseconds));
            var dismissed = new List<DialoguePresentationRow>();
            var activated = new List<ActiveDialogueBubble>();
            lock (_gate)
            {
                if (_disposed || !_activeLifecycle) return;
                var expired = _active
                    .Where(pair => pair.Value.ExpiresAtMilliseconds <= nowMilliseconds)
                    .Select(pair => pair.Key)
                    .OrderBy(value => value.ToString(), StringComparer.Ordinal)
                    .ToList();
                foreach (var speaker in expired)
                {
                    dismissed.Add(_active[speaker].Row);
                    _active.Remove(speaker);
                    ActivateNext(speaker, nowMilliseconds, activated);
                }
            }
            foreach (var row in dismissed) BubbleDismissed?.Invoke(row);
            foreach (var bubble in activated) BubbleActivated?.Invoke(bubble);
        }

        public bool Dismiss(IndividualId speakerId, UtteranceId utteranceId, long nowMilliseconds)
        {
            if (nowMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(nowMilliseconds));
            DialoguePresentationRow? dismissed = null;
            var activated = new List<ActiveDialogueBubble>();
            lock (_gate)
            {
                if (_disposed || !_activeLifecycle) return false;
                if (!_active.TryGetValue(speakerId, out var active) ||
                    active.Row.UtteranceId != utteranceId)
                    return false;
                dismissed = active.Row;
                _active.Remove(speakerId);
                ActivateNext(speakerId, nowMilliseconds, activated);
            }
            BubbleDismissed?.Invoke(dismissed);
            foreach (var bubble in activated) BubbleActivated?.Invoke(bubble);
            return true;
        }

        public IReadOnlyList<ActiveDialogueBubble> ActiveSnapshot()
        {
            lock (_gate)
            {
                return new ReadOnlyCollection<ActiveDialogueBubble>(
                    _active.Values
                        .OrderBy(value => value.Row.SpeakerId.ToString(), StringComparer.Ordinal)
                        .ToList());
            }
        }

        public int QueuedCount
        {
            get { lock (_gate) return _queued.Values.Sum(value => value.Count); }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _activeLifecycle = false;
                _active.Clear();
                _queued.Clear();
                BubbleActivated = null;
                BubbleDismissed = null;
            }
        }

        private DialoguePresentationEnqueueResult Queue(DialoguePresentationRow row)
        {
            if (!_queued.TryGetValue(row.SpeakerId, out var speakerQueue))
            {
                speakerQueue = new List<QueuedRow>();
                _queued.Add(row.SpeakerId, speakerQueue);
            }

            QueuedRow? eviction = null;
            if (speakerQueue.Count >= _perSpeakerCapacity)
            {
                eviction = Worst(speakerQueue);
                if (!Outranks(row, eviction.Row))
                    return new DialoguePresentationEnqueueResult(DialoguePresentationEnqueueStatus.RejectedCapacity);
            }
            else if (QueuedCountUnsafe() >= _globalCapacity)
            {
                var globalWorst = Worst(_queued.Values.SelectMany(value => value).ToList());
                if (!Outranks(row, globalWorst.Row))
                    return new DialoguePresentationEnqueueResult(DialoguePresentationEnqueueStatus.RejectedCapacity);
                eviction = globalWorst;
            }

            if (eviction is not null)
            {
                _queued[eviction.Row.SpeakerId].Remove(eviction);
                _seen.Remove(eviction.Row.UtteranceId);
            }
            speakerQueue.Add(new QueuedRow(row, ++_sequence));
            return new DialoguePresentationEnqueueResult(
                eviction is null
                    ? DialoguePresentationEnqueueStatus.Queued
                    : DialoguePresentationEnqueueStatus.QueuedAfterEviction,
                eviction?.Row.UtteranceId);
        }

        private void ActivateNext(
            IndividualId speaker,
            long nowMilliseconds,
            ICollection<ActiveDialogueBubble> activated)
        {
            if (!_queued.TryGetValue(speaker, out var values) || values.Count == 0)
            {
                _queued.Remove(speaker);
                return;
            }
            var next = values
                .OrderByDescending(value => (int)value.Row.Priority)
                .ThenBy(value => value.Sequence)
                .ThenBy(value => value.Row.UtteranceId.ToString(), StringComparer.Ordinal)
                .First();
            values.Remove(next);
            if (values.Count == 0) _queued.Remove(speaker);
            var bubble = ActivateRow(next.Row, nowMilliseconds);
            _active.Add(speaker, bubble);
            activated.Add(bubble);
        }

        private static ActiveDialogueBubble ActivateRow(
            DialoguePresentationRow row,
            long nowMilliseconds)
        {
            var wrapped = DialogueBubbleText.Wrap(row.Text);
            var duration = DialogueBubbleTiming.DurationMilliseconds(row.Text);
            return new ActiveDialogueBubble(row, wrapped, nowMilliseconds, checked(nowMilliseconds + duration));
        }

        private static QueuedRow Worst(IReadOnlyList<QueuedRow> values) =>
            values
                .OrderBy(value => (int)value.Row.Priority)
                .ThenByDescending(value => value.Sequence)
                .ThenByDescending(value => value.Row.UtteranceId.ToString(), StringComparer.Ordinal)
                .First();

        private static bool Outranks(DialoguePresentationRow candidate, DialoguePresentationRow existing) =>
            (int)candidate.Priority > (int)existing.Priority;

        private int QueuedCountUnsafe() => _queued.Values.Sum(value => value.Count);
    }

    public static class DialogueBubbleTiming
    {
        public const int BaseMilliseconds = 3500;
        public const int MillisecondsPerDisplayedCharacter = 35;
        public const int MaximumMilliseconds = 9000;

        public static int DurationMilliseconds(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));
            var count = new StringInfo(text).LengthInTextElements;
            var value = BaseMilliseconds + checked(count * MillisecondsPerDisplayedCharacter);
            return Math.Max(BaseMilliseconds, Math.Min(MaximumMilliseconds, value));
        }
    }

    public static class DialogueBubbleText
    {
        public const int DefaultElementsPerLine = 40;
        public const int DefaultMaximumLines = 4;

        public static string Wrap(
            string text,
            int elementsPerLine = DefaultElementsPerLine,
            int maximumLines = DefaultMaximumLines)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));
            if (elementsPerLine < 1) throw new ArgumentOutOfRangeException(nameof(elementsPerLine));
            if (maximumLines < 1) throw new ArgumentOutOfRangeException(nameof(maximumLines));
            if (text.Length == 0) return string.Empty;

            var elements = new List<string>();
            var enumerator = StringInfo.GetTextElementEnumerator(text);
            while (enumerator.MoveNext())
            {
                var value = (string)enumerator.Current;
                elements.Add(value == "\r" || value == "\n" || value == "\t" ? " " : value);
            }
            var capacity = checked(elementsPerLine * maximumLines);
            var truncated = elements.Count > capacity;
            if (truncated)
            {
                elements = elements.Take(Math.Max(0, capacity - 1)).ToList();
                elements.Add("…");
            }
            var builder = new StringBuilder();
            for (var index = 0; index < elements.Count; index++)
            {
                if (index > 0 && index % elementsPerLine == 0) builder.Append('\n');
                builder.Append(elements[index]);
            }
            return builder.ToString();
        }
    }

    public readonly struct DialogueScreenRect
    {
        public DialogueScreenRect(float x, float y, float width, float height)
        {
            if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }

        public DialogueScreenRect Clamp(float screenWidth, float screenHeight, float margin = 4f)
        {
            if (screenWidth < 0 || screenHeight < 0) throw new ArgumentOutOfRangeException();
            if (margin < 0) throw new ArgumentOutOfRangeException(nameof(margin));
            var availableWidth = Math.Max(0f, screenWidth - (margin * 2f));
            var availableHeight = Math.Max(0f, screenHeight - (margin * 2f));
            var clampedWidth = Math.Min(Width, availableWidth);
            var clampedHeight = Math.Min(Height, availableHeight);
            var maxX = Math.Max(margin, screenWidth - clampedWidth - margin);
            var maxY = Math.Max(margin, screenHeight - clampedHeight - margin);
            return new DialogueScreenRect(
                Math.Max(margin, Math.Min(maxX, X)),
                Math.Max(margin, Math.Min(maxY, Y)),
                clampedWidth,
                clampedHeight);
        }
    }

    public sealed class DialoguePresentationAttempt
    {
        public DialoguePresentationAttempt(bool bubbleDisplayed, bool playLogMirrored)
        {
            BubbleDisplayed = bubbleDisplayed;
            PlayLogMirrored = playLogMirrored;
        }

        public bool BubbleDisplayed { get; }
        public bool PlayLogMirrored { get; }
        public bool AnyDisplayed => BubbleDisplayed || PlayLogMirrored;
        public DialoguePresentationChannel? ActualChannel =>
            BubbleDisplayed
                ? DialoguePresentationChannel.Bubble
                : PlayLogMirrored
                    ? DialoguePresentationChannel.PlayLog
                    : (DialoguePresentationChannel?)null;

        public DisplayedUtteranceReceipt? CreateReceipt(
            DialoguePresentationRow row,
            ValidatedUtterance utterance,
            long displayedAtTick,
            DateTimeOffset displayedAtUtc,
            DialogueDisclosure disclosure,
            IEnumerable<IndividualId> audienceIds)
        {
            if (row is null) throw new ArgumentNullException(nameof(row));
            if (utterance is null) throw new ArgumentNullException(nameof(utterance));
            if (!AnyDisplayed) return null;
            DialoguePresentationEvidence.Validate(
                row,
                utterance,
                disclosure,
                audienceIds);
            return new DisplayedUtteranceReceipt(
                utterance,
                displayedAtTick,
                displayedAtUtc,
                disclosure,
                ActualChannel!.Value,
                audienceIds);
        }
    }

    public static class DialoguePresentationEvidence
    {
        public static void Validate(
            DialoguePresentationRow row,
            ValidatedUtterance utterance,
            DialogueDisclosure disclosure,
            IEnumerable<IndividualId> audienceIds)
        {
            if (row is null) throw new ArgumentNullException(nameof(row));
            if (utterance is null) throw new ArgumentNullException(nameof(utterance));
            if (audienceIds is null) throw new ArgumentNullException(nameof(audienceIds));
            if (!Enum.IsDefined(typeof(DialogueDisclosure), disclosure))
                throw new ArgumentOutOfRangeException(nameof(disclosure));
            var proposal = utterance.Proposal;
            if (proposal.Id != row.UtteranceId ||
                proposal.ConversationId != row.ConversationId ||
                proposal.SpeakerId != row.SpeakerId ||
                proposal.RecipientId != row.RecipientId)
                throw new InvalidOperationException("Presentation row does not match the validated utterance.");
            var audience = audienceIds.ToList();
            if (audience.Count == 0 || audience.Count > 32)
                throw new ArgumentOutOfRangeException(nameof(audienceIds));
            if (audience.Any(id => id.Value == Guid.Empty) ||
                audience.Distinct().Count() != audience.Count ||
                !audience.Contains(row.SpeakerId) ||
                (row.RecipientId.HasValue && !audience.Contains(row.RecipientId.Value)))
                throw new ArgumentException(
                    "Presentation audience must be unique and include the speaker and recipient.",
                    nameof(audienceIds));
        }
    }
}
