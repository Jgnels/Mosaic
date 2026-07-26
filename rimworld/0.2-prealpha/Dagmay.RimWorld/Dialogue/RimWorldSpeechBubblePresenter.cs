using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using UnityEngine;
using Verse;

namespace Dagmay.RimWorld.Dialogue
{
    public sealed class RimWorldDialoguePresentationResult
    {
        public RimWorldDialoguePresentationResult(
            DialoguePresentationRow row,
            DialoguePresentationAttempt attempt,
            DisplayedUtteranceReceipt? receipt)
        {
            Row = row ?? throw new ArgumentNullException(nameof(row));
            Attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
            Receipt = receipt;
        }

        public DialoguePresentationRow Row { get; }
        public DialoguePresentationAttempt Attempt { get; }
        public DisplayedUtteranceReceipt? Receipt { get; }
    }

    /// <summary>
    /// Main-thread-only, presentation-only RimWorld adapter. It owns no provider,
    /// persistence, canonical mutation, pawn-job, or random-number authority.
    /// </summary>
    public sealed class RimWorldSpeechBubblePresenter : IDisposable
    {
        private sealed class PendingEvidence
        {
            public PendingEvidence(
                ValidatedUtterance utterance,
                DialogueDisclosure disclosure,
                IReadOnlyList<IndividualId> audienceIds)
            {
                Utterance = utterance;
                Disclosure = disclosure;
                AudienceIds = audienceIds;
            }

            public ValidatedUtterance Utterance { get; }
            public DialogueDisclosure Disclosure { get; }
            public IReadOnlyList<IndividualId> AudienceIds { get; }
        }

        private readonly int _mainThreadId;
        private readonly DialogueSpeechBubbleController _controller;
        private readonly Dictionary<IndividualId, Pawn> _pawnBindings =
            new Dictionary<IndividualId, Pawn>();
        private readonly Dictionary<UtteranceId, PendingEvidence> _pending =
            new Dictionary<UtteranceId, PendingEvidence>();
        private readonly HashSet<UtteranceId> _presented = new HashSet<UtteranceId>();
        private bool _disposed;

        public RimWorldSpeechBubblePresenter()
            : this(new DialogueSpeechBubbleController(), Thread.CurrentThread.ManagedThreadId)
        {
        }

        internal RimWorldSpeechBubblePresenter(
            DialogueSpeechBubbleController controller,
            int mainThreadId)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            if (mainThreadId < 1) throw new ArgumentOutOfRangeException(nameof(mainThreadId));
            _mainThreadId = mainThreadId;
            _controller.BubbleDismissed += OnBubbleDismissed;
            _controller.Activate();
        }

        public event Action<RimWorldDialoguePresentationResult>? PresentationCompleted;
        public event Action<DialoguePresentationRow>? PresentationAbandoned;

        public void Bind(IndividualId individualId, Pawn pawn)
        {
            EnsureMainThread();
            if (_disposed) return;
            if (individualId.Value == Guid.Empty)
                throw new ArgumentException("IndividualId cannot be empty.", nameof(individualId));
            _pawnBindings[individualId] = pawn ?? throw new ArgumentNullException(nameof(pawn));
        }

        public bool Unbind(IndividualId individualId)
        {
            EnsureMainThread();
            return !_disposed && _pawnBindings.Remove(individualId);
        }

        public DialoguePresentationEnqueueResult Enqueue(
            DialoguePresentationRow row,
            ValidatedUtterance utterance,
            DialogueDisclosure disclosure,
            IEnumerable<IndividualId> audienceIds,
            long nowMilliseconds)
        {
            EnsureMainThread();
            if (row is null) throw new ArgumentNullException(nameof(row));
            if (utterance is null) throw new ArgumentNullException(nameof(utterance));
            var audience = (audienceIds ?? throw new ArgumentNullException(nameof(audienceIds))).ToList();
            if (_disposed)
                return new DialoguePresentationEnqueueResult(DialoguePresentationEnqueueStatus.Disposed);

            DialoguePresentationEvidence.Validate(row, utterance, disclosure, audience);

            var result = _controller.Enqueue(row, nowMilliseconds);
            if (result.Status == DialoguePresentationEnqueueStatus.Activated ||
                result.Status == DialoguePresentationEnqueueStatus.Queued ||
                result.Status == DialoguePresentationEnqueueStatus.QueuedAfterEviction)
            {
                _pending[row.UtteranceId] = new PendingEvidence(utterance, disclosure, audience);
                if (result.EvictedUtteranceId.HasValue)
                    _pending.Remove(result.EvictedUtteranceId.Value);
            }
            return result;
        }

        public void Update(long nowMilliseconds)
        {
            EnsureMainThread();
            if (_disposed) return;
            _controller.Tick(nowMilliseconds);
        }

        public void Draw(long displayedAtTick, DateTimeOffset displayedAtUtc, long nowMilliseconds)
        {
            EnsureMainThread();
            if (_disposed) return;
            _controller.Tick(nowMilliseconds);
            foreach (var bubble in _controller.ActiveSnapshot())
            {
                DrawOne(bubble, displayedAtTick, displayedAtUtc, nowMilliseconds);
            }
        }

        public void Deactivate()
        {
            EnsureMainThread();
            if (_disposed) return;
            _controller.Deactivate();
            _pending.Clear();
            _presented.Clear();
        }

        public void Activate()
        {
            EnsureMainThread();
            if (_disposed) return;
            _controller.Activate();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _controller.BubbleDismissed -= OnBubbleDismissed;
            _controller.Dispose();
            _pawnBindings.Clear();
            _pending.Clear();
            _presented.Clear();
            PresentationCompleted = null;
            PresentationAbandoned = null;
        }

        private void DrawOne(
            ActiveDialogueBubble bubble,
            long displayedAtTick,
            DateTimeOffset displayedAtUtc,
            long nowMilliseconds)
        {
            _pawnBindings.TryGetValue(bubble.Row.SpeakerId, out var pawn);
            var availability = RimWorldDialoguePresentationPolicy.Evaluate(
                _disposed,
                _mainThreadId,
                Thread.CurrentThread.ManagedThreadId,
                pawn is not null,
                pawn?.Spawned == true,
                Find.CurrentMap is not null,
                pawn is not null && pawn.Map == Find.CurrentMap);

            var bubbleDisplayed = false;
            if (availability == RimWorldDialoguePresentationAvailability.Ready)
            {
                try
                {
                    DrawBubble(pawn!, bubble);
                    bubbleDisplayed = true;
                }
                catch
                {
                    bubbleDisplayed = false;
                }
            }

            if (_presented.Contains(bubble.Row.UtteranceId))
            {
                if (!bubbleDisplayed)
                {
                    _controller.Dismiss(
                        bubble.Row.SpeakerId,
                        bubble.Row.UtteranceId,
                        nowMilliseconds);
                }
                return;
            }

            var playLogMirrored = TryMirrorPlayLog(bubble.Row, pawn);
            var attempt = new DialoguePresentationAttempt(bubbleDisplayed, playLogMirrored);
            DisplayedUtteranceReceipt? receipt = null;
            if (_pending.TryGetValue(bubble.Row.UtteranceId, out var evidence))
            {
                receipt = attempt.CreateReceipt(
                    bubble.Row,
                    evidence.Utterance,
                    displayedAtTick,
                    displayedAtUtc,
                    evidence.Disclosure,
                    evidence.AudienceIds);
            }

            _pending.Remove(bubble.Row.UtteranceId);
            if (bubbleDisplayed)
            {
                _presented.Add(bubble.Row.UtteranceId);
            }
            else
            {
                _controller.Dismiss(
                    bubble.Row.SpeakerId,
                    bubble.Row.UtteranceId,
                    nowMilliseconds);
            }
            PresentationCompleted?.Invoke(
                new RimWorldDialoguePresentationResult(bubble.Row, attempt, receipt));
        }

        private static void DrawBubble(Pawn pawn, ActiveDialogueBubble bubble)
        {
            var anchor = UI.MapToUIPosition(pawn.DrawPos);
            const float width = 320f;
            var priorFont = Text.Font;
            var priorAnchor = Text.Anchor;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                var body = string.IsNullOrWhiteSpace(bubble.Row.DisplayLabel)
                    ? bubble.WrappedText
                    : bubble.Row.DisplayLabel + ":\n" + bubble.WrappedText;
                var height = Math.Min(160f, Math.Max(44f, Text.CalcHeight(body, width - 20f) + 16f));
                var coreRect = new DialogueScreenRect(
                    anchor.x - (width / 2f),
                    anchor.y - height - 34f,
                    width,
                    height).Clamp(Screen.width, Screen.height);
                var rect = new Rect(coreRect.X, coreRect.Y, coreRect.Width, coreRect.Height);
                Widgets.DrawBoxSolidWithOutline(
                    rect,
                    new Color(0.08f, 0.08f, 0.08f, 0.92f),
                    new Color(0.72f, 0.72f, 0.72f, 0.95f),
                    1);
                Widgets.Label(rect.ContractedBy(8f), body);
            }
            finally
            {
                Text.Font = priorFont;
                Text.Anchor = priorAnchor;
            }
        }

        private static bool TryMirrorPlayLog(DialoguePresentationRow row, Pawn? pawn)
        {
            try
            {
                var log = Find.PlayLog;
                if (log is null) return false;
                var text = string.IsNullOrWhiteSpace(row.DisplayLabel)
                    ? row.Text
                    : row.DisplayLabel + ": " + row.Text;
                log.Add(new MosaicDialogueLogEntry(text, pawn));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void OnBubbleDismissed(DialoguePresentationRow row)
        {
            if (_disposed) return;
            var wasPending = _pending.Remove(row.UtteranceId);
            _presented.Remove(row.UtteranceId);
            if (wasPending) PresentationAbandoned?.Invoke(row);
        }

        private void EnsureMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                throw new InvalidOperationException("RimWorld dialogue presentation is main-thread-only.");
        }
    }
}
