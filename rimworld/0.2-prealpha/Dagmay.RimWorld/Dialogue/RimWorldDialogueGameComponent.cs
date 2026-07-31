using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using Dagmay.RimWorld.Bootstrap;
using Dagmay.RimWorld.Persistence;
using UnityEngine;
using Verse;

namespace Dagmay.RimWorld.Dialogue
{
    /// <summary>
    /// Observer-only composition root for the fake-provider dialogue path.
    /// All live RimWorld references remain inside this main-thread adapter.
    /// </summary>
    public sealed class RimWorldDialogueGameComponent : GameComponent
    {
        private readonly Game _game;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly OfflineRimWorldDialoguePipeline _pipeline =
            new OfflineRimWorldDialoguePipeline();
        private readonly RimWorldSpeechBubblePresenter _presenter =
            new RimWorldSpeechBubblePresenter();
        private readonly Dictionary<UtteranceId, OfflineRimWorldPreparedDialogue> _pending =
            new Dictionary<UtteranceId, OfflineRimWorldPreparedDialogue>();
        private readonly Dictionary<UtteranceId, RimWorldSocialDialogueTrigger> _pendingReplies =
            new Dictionary<UtteranceId, RimWorldSocialDialogueTrigger>();
        private DagmayIdentityGameComponent? _identity;
        private bool _disposed;
        private int _presentationThreadFailed;

        public RimWorldDialogueGameComponent(Game game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _presenter.PresentationCompleted += OnPresentationCompleted;
            _presenter.PresentationAbandoned += OnPresentationAbandoned;
        }

        public override void StartedNewGame() => EnsureAttached();

        public override void LoadedGame() => EnsureAttached();

        public override void GameComponentTick()
        {
            if (_disposed) return;
            if (!ReferenceEquals(Verse.Current.Game, _game))
            {
                DisposeAdapter();
                return;
            }
            EnsureAttached();
            if (!TryEnterPresentationLifecycle()) return;
            _presenter.Update(_clock.ElapsedMilliseconds);
        }

        public override void GameComponentOnGUI()
        {
            if (_disposed || !ReferenceEquals(Verse.Current.Game, _game)) return;
            if (Event.current is null || Event.current.type != EventType.Repaint) return;
            if (!TryEnterPresentationLifecycle()) return;
            var tick = Find.TickManager?.TicksGame ?? 0;
            _presenter.Draw(tick, DateTimeOffset.UtcNow, _clock.ElapsedMilliseconds);
        }

        private bool TryEnterPresentationLifecycle()
        {
            if (_disposed || Volatile.Read(ref _presentationThreadFailed) != 0) return false;
            try
            {
                _presenter.BindFromTrustedGameComponentLifecycle(
                    Thread.CurrentThread.ManagedThreadId);
                return true;
            }
            catch (InvalidOperationException exception)
            {
                FailPresentationThreadOnce(exception);
                return false;
            }
        }

        private void FailPresentationThreadOnce(InvalidOperationException exception)
        {
            if (Interlocked.Exchange(ref _presentationThreadFailed, 1) != 0) return;
            _pending.Clear();
            _pendingReplies.Clear();
            _presenter.PresentationCompleted -= OnPresentationCompleted;
            _presenter.PresentationAbandoned -= OnPresentationAbandoned;
            _presenter.Dispose();
            Log.Error(
                $"[Dagmay] {DagmayBuildInfo.Version} dialogue presentation disabled for this game "
                + $"after a runtime-thread validation failure: {exception.Message}");
        }

        private void EnsureAttached()
        {
            if (_disposed) return;
            var current = DagmayIdentityGameComponent.Current;
            if (ReferenceEquals(current, _identity)) return;
            if (_identity is not null)
                _identity.SocialDialogueTriggerCaptured -= OnSocialDialogueTriggerCaptured;
            _identity = current;
            if (_identity is not null)
                _identity.SocialDialogueTriggerCaptured += OnSocialDialogueTriggerCaptured;
        }

        private void OnSocialDialogueTriggerCaptured(RimWorldSocialDialogueTrigger trigger)
        {
            if (_disposed ||
                Volatile.Read(ref _presentationThreadFailed) != 0 ||
                !ReferenceEquals(Verse.Current.Game, _game))
            {
                return;
            }
            if (!_presenter.IsRuntimeThreadBound) return;
            try
            {
                _presenter.ValidateRuntimeThread(Thread.CurrentThread.ManagedThreadId);
            }
            catch (InvalidOperationException exception)
            {
                FailPresentationThreadOnce(exception);
                return;
            }
            try
            {
                var tick = Find.TickManager?.TicksGame ?? trigger.ObservedAtTick;
                var result = _pipeline.PrepareAsync(
                        trigger,
                        tick,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                if (!result.IsPrepared || result.Prepared is null)
                {
                    Log.Message(
                        $"[Dagmay] {DagmayBuildInfo.Version} fake dialogue preparation skipped; "
                        + $"status={result.Status}; diagnostic={result.Diagnostic}");
                    return;
                }

                var prepared = result.Prepared;
                BindIfPresent(trigger.Speaker);
                BindIfPresent(trigger.Recipient);
                var enqueue = _presenter.Enqueue(
                    prepared.PresentationRow,
                    prepared.Utterance,
                    DialogueDisclosure.WitnessesOnly,
                    prepared.AudienceIds,
                    _clock.ElapsedMilliseconds);
                if (IsAccepted(enqueue.Status))
                {
                    _pending[prepared.PresentationRow.UtteranceId] = prepared;
                    _pendingReplies[prepared.PresentationRow.UtteranceId] = trigger;
                    RemoveEvicted(enqueue.EvictedUtteranceId);
                }
            }
            catch (Exception exception)
            {
                Log.Warning(
                    $"[Dagmay] {DagmayBuildInfo.Version} fake dialogue path failed safely: "
                    + exception.Message);
            }
        }

        private void BindIfPresent(RimWorldDialogueIdentitySnapshot identity)
        {
            var pawn = Find.Maps
                .Where(map => map is not null)
                .SelectMany(map => map.mapPawns.AllPawns)
                .FirstOrDefault(candidate =>
                    candidate is not null &&
                    string.Equals(candidate.ThingID, identity.ExternalId, StringComparison.Ordinal));
            if (pawn is not null) _presenter.Bind(identity.IndividualId, pawn);
        }

        private void OnPresentationCompleted(RimWorldDialoguePresentationResult result)
        {
            if (_disposed) return;
            try
            {
                if (!_pending.TryGetValue(result.Row.UtteranceId, out var prepared)) return;
                _pending.Remove(result.Row.UtteranceId);
                _pendingReplies.TryGetValue(
                    result.Row.UtteranceId,
                    out var replyTrigger);
                _pendingReplies.Remove(result.Row.UtteranceId);

                if (result.Receipt is null)
                {
                    Log.Message(
                        $"[Dagmay] {DagmayBuildInfo.Version} dialogue presentation produced no receipt; "
                        + $"UtteranceId={result.Row.UtteranceId}; no admission or reply queued.");
                    return;
                }

                var identity = _identity;
                var diagnostic = "The Mosaic identity component is unavailable.";
                if (identity is null ||
                    !identity.TryQueueDialogueAdmission(
                        prepared.Request,
                        result.Receipt,
                        out diagnostic))
                {
                    Log.Warning(
                        $"[Dagmay] {DagmayBuildInfo.Version} dialogue admission failed closed; "
                        + $"UtteranceId={result.Row.UtteranceId}; diagnostic={diagnostic}");
                    return;
                }

                Log.Message(
                    $"[Dagmay] {DagmayBuildInfo.Version} queued verified dialogue admission; "
                    + $"UtteranceId={result.Row.UtteranceId}; channel={result.Receipt.Channel}.");

                if (replyTrigger is not null)
                    TryQueueReceiptGatedReply(replyTrigger, result.Receipt);
            }
            catch (Exception exception)
            {
                Log.Warning(
                    $"[Dagmay] {DagmayBuildInfo.Version} dialogue receipt handling failed safely: "
                    + exception.Message);
            }
        }

        private void TryQueueReceiptGatedReply(
            RimWorldSocialDialogueTrigger trigger,
            DisplayedUtteranceReceipt openingReceipt)
        {
            var tick = Find.TickManager?.TicksGame ?? openingReceipt.DisplayedAtTick;
            var result = _pipeline.PrepareReplyAsync(
                    trigger,
                    openingReceipt,
                    tick,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            if (!result.IsPrepared || result.Prepared is null)
            {
                Log.Message(
                    $"[Dagmay] {DagmayBuildInfo.Version} bounded reply preparation skipped; "
                    + $"status={result.Status}; diagnostic={result.Diagnostic}");
                return;
            }

            var prepared = result.Prepared;
            BindIfPresent(trigger.Recipient);
            BindIfPresent(trigger.Speaker);
            var enqueue = _presenter.Enqueue(
                prepared.PresentationRow,
                prepared.Utterance,
                DialogueDisclosure.WitnessesOnly,
                prepared.AudienceIds,
                _clock.ElapsedMilliseconds);
            if (!IsAccepted(enqueue.Status))
            {
                Log.Message(
                    $"[Dagmay] {DagmayBuildInfo.Version} bounded reply presentation skipped; "
                    + $"status={enqueue.Status}; ConversationId={prepared.Request.ConversationId}.");
                return;
            }

            _pending[prepared.PresentationRow.UtteranceId] = prepared;
            RemoveEvicted(enqueue.EvictedUtteranceId);
            Log.Message(
                $"[Dagmay] {DagmayBuildInfo.Version} queued receipt-gated bounded reply; "
                + $"ConversationId={prepared.Request.ConversationId}; "
                + $"UtteranceId={prepared.PresentationRow.UtteranceId}; "
                + $"evidence={prepared.Request.SourceEventIds.Count}.");
        }

        private static bool IsAccepted(DialoguePresentationEnqueueStatus status) =>
            status == DialoguePresentationEnqueueStatus.Activated ||
            status == DialoguePresentationEnqueueStatus.Queued ||
            status == DialoguePresentationEnqueueStatus.QueuedAfterEviction;

        private void RemoveEvicted(UtteranceId? utteranceId)
        {
            if (!utteranceId.HasValue) return;
            _pending.Remove(utteranceId.Value);
            _pendingReplies.Remove(utteranceId.Value);
        }

        private void OnPresentationAbandoned(DialoguePresentationRow row)
        {
            if (_disposed) return;
            _pending.Remove(row.UtteranceId);
            _pendingReplies.Remove(row.UtteranceId);
        }

        private void DisposeAdapter()
        {
            if (_disposed) return;
            _disposed = true;
            if (_identity is not null)
                _identity.SocialDialogueTriggerCaptured -= OnSocialDialogueTriggerCaptured;
            _identity = null;
            _presenter.PresentationCompleted -= OnPresentationCompleted;
            _presenter.PresentationAbandoned -= OnPresentationAbandoned;
            _presenter.Dispose();
            _pending.Clear();
            _pendingReplies.Clear();
        }
    }
}
