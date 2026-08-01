using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Dagmay.Core.Contracts;
using Dagmay.Core.Dialogue;
using Dagmay.RimWorld.Bootstrap;
using Dagmay.RimWorld.Diagnostics;
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
        private const long TelemetryPollIntervalTicks = 600;
        private const long TelemetrySummaryIntervalTicks = 60000;

        private readonly Game _game;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Experiment0ATelemetry _experimentTelemetry =
            new Experiment0ATelemetry();
        private readonly RimWorldExperiment0ASemanticPoller _experimentPoller;
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
        private long _lastTelemetryPollTick = -1;
        private long _nextTelemetrySummaryTick = TelemetrySummaryIntervalTicks;

        public RimWorldDialogueGameComponent(Game game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _experimentPoller = new RimWorldExperiment0ASemanticPoller(_experimentTelemetry);
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
            var tick = Find.TickManager?.TicksGame ?? 0;
            PollExperiment0A(tick);
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
            if (_disposed || !ReferenceEquals(Verse.Current.Game, _game)) return;
            RecordCurrentTrigger(trigger);
            if (Volatile.Read(ref _presentationThreadFailed) != 0)
            {
                _experimentTelemetry.RecordDialoguePreparation(
                    false,
                    "presentation_thread_failed");
                return;
            }
            if (!_presenter.IsRuntimeThreadBound)
            {
                _experimentTelemetry.RecordDialoguePreparation(
                    false,
                    "presentation_thread_unbound");
                return;
            }
            var preparationRecorded = false;
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
                _experimentTelemetry.RecordDialoguePreparation(
                    result.IsPrepared,
                    result.Status.ToString());
                preparationRecorded = true;
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
                else
                {
                    _experimentTelemetry.RecordPresentation(string.Empty);
                }
            }
            catch (Exception exception)
            {
                if (!preparationRecorded)
                    _experimentTelemetry.RecordDialoguePreparation(false, "exception");
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
                _experimentTelemetry.RecordPresentation(
                    result.Receipt?.Channel.ToString() ?? string.Empty);
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
            _experimentTelemetry.RecordDialoguePreparation(
                result.IsPrepared,
                "reply_" + result.Status);
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
                _experimentTelemetry.RecordPresentation(string.Empty);
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
            _experimentTelemetry.RecordPresentation(string.Empty);
            _pending.Remove(row.UtteranceId);
            _pendingReplies.Remove(row.UtteranceId);
        }

        private void DisposeAdapter()
        {
            if (_disposed) return;
            LogExperiment0ASummary("final", Find.TickManager?.TicksGame ?? 0);
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

        private void RecordCurrentTrigger(RimWorldSocialDialogueTrigger trigger)
        {
            try
            {
                var priorDepth = trigger.PriorRelationshipEvidence
                    .Concat(trigger.RecipientPriorRelationshipEvidence)
                    .Select(value => value.EventId)
                    .Distinct()
                    .Count();
                _experimentTelemetry.RecordObservation(new Experiment0AObservation(
                    Experiment0ASource.CurrentTrigger,
                    "trigger:" + trigger.SourceEventId,
                    trigger.ObservedAtTick,
                    trigger.ObservedAtTick,
                    new[]
                    {
                        trigger.Speaker.IndividualId.ToString(),
                        trigger.Recipient.IndividualId.ToString()
                    },
                    priorDepth));
            }
            catch (Exception)
            {
                _experimentTelemetry.RecordSourceFailure(Experiment0ASource.CurrentTrigger);
            }
        }

        private void PollExperiment0A(long tick)
        {
            if (tick < 0 ||
                tick == _lastTelemetryPollTick ||
                tick % TelemetryPollIntervalTicks != 0)
            {
                return;
            }

            _lastTelemetryPollTick = tick;
            var identity = _identity;
            if (identity is not null)
            {
                try
                {
                    var enrolled = identity.CreateObserverSnapshot().Individuals
                        .Where(value => value.HasIdentity)
                        .ToDictionary(
                            value => value.ExternalId,
                            value => value.IndividualId,
                            StringComparer.Ordinal);
                    _experimentPoller.Poll(enrolled, tick);
                }
                catch (Exception)
                {
                    _experimentTelemetry.RecordSourceFailure(Experiment0ASource.Thought);
                    _experimentTelemetry.RecordSourceFailure(Experiment0ASource.PlayLog);
                    _experimentTelemetry.RecordSourceFailure(Experiment0ASource.Tale);
                }
            }

            if (tick >= _nextTelemetrySummaryTick)
            {
                LogExperiment0ASummary("periodic", tick);
                _nextTelemetrySummaryTick = tick > long.MaxValue - TelemetrySummaryIntervalTicks
                    ? long.MaxValue
                    : tick + TelemetrySummaryIntervalTicks;
            }
        }

        private void LogExperiment0ASummary(string phase, long tick)
        {
            try
            {
                Log.Message(
                    "[Mosaic] " + DagmayBuildInfo.Version + " "
                    + _experimentTelemetry.Snapshot().ToLogLine(phase, Math.Max(0, tick)));
            }
            catch (Exception exception)
            {
                Log.Warning(
                    "[Mosaic] " + DagmayBuildInfo.Version
                    + " Experiment 0A summary failed safely: " + exception.Message);
            }
        }
    }
}
