using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dagmay.Core.Abstractions;
using Dagmay.Core.Contracts;
using Dagmay.Core.Identity;
using Dagmay.Core.Lifecycle;
using Dagmay.Core.Memory;
using Dagmay.Core.Observer;
using Dagmay.Core.Persistence;
using Dagmay.Core.Reflection;
using Dagmay.Core.Scheduling;
using Dagmay.Core.Views;
using Dagmay.Core.Dialogue;
using Dagmay.RimWorld.Identity;
using Dagmay.RimWorld.Diagnostics;
using Dagmay.RimWorld.Bootstrap;
using Dagmay.RimWorld.Dialogue;
using Dagmay.RimWorld.Perception;
using Dagmay.RimWorld.Reflection;
using Verse;

namespace Dagmay.RimWorld.Persistence
{
    public sealed class DagmayIdentityGameComponent : GameComponent
    {
        private const int ScanIntervalTicks = 600;
        private const int MaximumReflectionAttempts = 3;
        private const int MaximumAuditRecords = 500;
        private const int ReflectionEstimatedTokens = 1200;
        private static readonly TimeSpan ReflectionRequestTimeout = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan MinimumDispatchSpacing = TimeSpan.FromSeconds(10);

        private readonly Dictionary<string, IndividualState> _identities =
            new Dictionary<string, IndividualState>(StringComparer.Ordinal);
        private readonly AtomicIdentityArchive _archive = new AtomicIdentityArchive();
        private readonly DurableExperienceJournal _experienceJournal = new DurableExperienceJournal();
        private readonly AtomicReflectionStore _reflectionStore = new AtomicReflectionStore();
        private readonly DialogueAdmissionRecoveryCoordinator _dialogueAdmissionCoordinator =
            new DialogueAdmissionRecoveryCoordinator();
        private readonly InMemoryEventLedger _eventLedger = new InMemoryEventLedger();
        private readonly MemoryIndex _memoryIndex = new MemoryIndex();
        private readonly List<ExperienceJournalRecord> _experienceRecords =
            new List<ExperienceJournalRecord>();
        private readonly DialogueHistoryProjection _dialogueHistoryProjection =
            new DialogueHistoryProjection();
        private readonly CrossEncounterConversationSelector _conversationContinuitySelector =
            new CrossEncounterConversationSelector();
        private readonly Dictionary<PerceptionId, EventId> _perceptionSources =
            new Dictionary<PerceptionId, EventId>();
        private readonly DeterministicExperienceEncoder _experienceEncoder = new DeterministicExperienceEncoder();
        private readonly ReflectionContextBuilder _reflectionContextBuilder = new ReflectionContextBuilder();
        private readonly ReflectionProposalValidator _reflectionValidator = new ReflectionProposalValidator();
        private readonly PostLoadReflectionCheckpointGate _reflectionCheckpointGate =
            new PostLoadReflectionCheckpointGate();
        private ReflectionBudgetPolicy _runtimeBudgetPolicy = ReflectionBudgetPolicy.ConservativePersonalDefault;
        private ReflectionBudgetGate _reflectionBudget =
            new ReflectionBudgetGate(ReflectionBudgetPolicy.ConservativePersonalDefault);
        private readonly Dictionary<string, PawnObservationSnapshot> _observations =
            new Dictionary<string, PawnObservationSnapshot>(StringComparer.Ordinal);
        private readonly ReadOnlySocialEventEnvelopeCapture _readOnlyEventEnvelopeCapture =
            new ReadOnlySocialEventEnvelopeCapture();
        private readonly ConcurrentQueue<CompletedReflectionCall> _completedReflectionCalls =
            new ConcurrentQueue<CompletedReflectionCall>();
        private readonly HashSet<ReflectionTaskId> _invalidatedInFlightTasks =
            new HashSet<ReflectionTaskId>();

        private PersistentReflectionQueue _reflectionQueue = new PersistentReflectionQueue(500);
        private List<ReflectionAuditRecord> _reflectionAudit = new List<ReflectionAuditRecord>();
        private int _reflectionAdmissionDeferredSession;
        private RimWorldReflectionProviderSelection? _providerSelection;
        private string _storeId = string.Empty;
        private long _generation;
        private long _reflectionGeneration;
        private long _dialogueCheckpointGeneration;
        private List<string> _manifestExternalIds = new List<string>();
        private List<string> _manifestIndividualIds = new List<string>();
        private List<string> _pausedExternalIds = new List<string>();
        private long _experiencePosition;
        private string _experienceLastHash = string.Empty;
        private ExperienceJournalLoadResult? _pendingExperienceRecovery;
        private string _experienceRecoveryDiagnostic = string.Empty;
        private bool _initialized;
        private bool _writesEnabled = true;
        private bool _experienceWritesEnabled = true;
        private bool _reflectionWritesEnabled = true;
        private bool _reflectionDirty;
        private bool _dialogueCheckpointDirty;
        private bool _dialogueCheckpointManifestInvalid;
        private bool _reflectionStoreWasNew;
        private bool _requestInFlight;
        private ReflectionTaskId? _inFlightTaskId;
        private DateTimeOffset _nextReflectionHeartbeatUtc = DateTimeOffset.MinValue;
        private DateTimeOffset _nextDispatchAtUtc = DateTimeOffset.MinValue;
        private int _backgroundCursor;
        private int _sessionAttemptCount;
        private IndividualId? _lastDispatchedIndividualId;
        private string _settingsFingerprint = string.Empty;
        private bool _sessionBudgetNoticeLogged;
        private bool _providerPauseNoticeLogged;
        private bool _storageSafetyPauseNoticeLogged;
        private bool _postLoadCheckpointPauseNoticeLogged;

        public DagmayIdentityGameComponent(Game game)
        {
            Current = this;
        }

        public static DagmayIdentityGameComponent? Current { get; private set; }

        public event Action<RimWorldSocialDialogueTrigger>? SocialDialogueTriggerCaptured;
        public event Action<ReadOnlyRimWorldEventProjection>? ReadOnlyEventEnvelopeCaptured;

        public RimWorldConversationHistoryData BuildConversationHistoryData(
            IndividualId? selectedIndividualId,
            int maximumRows)
        {
            if (maximumRows < 1 ||
                maximumRows > ConversationHistoryViewerBuilder.MaximumRows)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRows));
            }

            var participants = _identities
                .Select(pair => new RimWorldConversationParticipantSnapshot(
                    pair.Value.Id,
                    pair.Value.DisplayName,
                    pair.Key))
                .OrderBy(value => value.DisplayLabel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.IndividualId.ToString(), StringComparer.Ordinal)
                .ToArray();
            if (!selectedIndividualId.HasValue)
            {
                return new RimWorldConversationHistoryData(
                    participants,
                    null,
                    participants.Length == 0
                        ? "No enrolled colonists are available."
                        : "Select a colonist to review verified displayed dialogue.");
            }

            var selected = participants.FirstOrDefault(value =>
                value.IndividualId == selectedIndividualId.Value);
            if (selected is null)
            {
                return new RimWorldConversationHistoryData(
                    participants,
                    null,
                    "The selected colonist is no longer enrolled in this game.");
            }

            var ledgerEntries = _eventLedger.Snapshot();
            var conversationIds = new HashSet<ConversationId>();
            foreach (var ledgerEntry in ledgerEntries)
            {
                var factualEvent = ledgerEntry.Value;
                if (!string.Equals(
                        factualEvent.Kind,
                        DialogueEventAdmissionService.EventKind,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        factualEvent.Source,
                        DialogueEventAdmissionService.DefaultSource,
                        StringComparison.Ordinal) ||
                    !factualEvent.Subjects.Contains(selected.IndividualId) ||
                    !factualEvent.FactualPayload.TryGetValue(
                        DialogueEventAdmissionService.ConversationIdKey,
                        out var conversationText) ||
                    !Guid.TryParseExact(conversationText, "N", out var conversationGuid) ||
                    conversationGuid == Guid.Empty)
                {
                    continue;
                }

                conversationIds.Add(new ConversationId(conversationGuid));
            }

            var currentTick = Find.TickManager?.TicksGame ?? long.MaxValue;
            var turns = new List<PriorConversationTurn>();
            var malformed = 0;
            var privacyFiltered = 0;
            foreach (var conversationId in conversationIds)
            {
                var packet = _dialogueHistoryProjection.Build(
                    ledgerEntries,
                    new DialogueHistoryQuery(
                        conversationId,
                        currentTick,
                        100,
                        DialogueHistoryView.Participant,
                        selected.IndividualId));
                malformed += packet.MalformedEventCount;
                privacyFiltered += packet.PrivacyFilteredCount;
                foreach (var entry in packet.Entries)
                {
                    if (!entry.RecipientId.HasValue) continue;
                    turns.Add(new PriorConversationTurn(
                        entry.EventId,
                        entry.ConversationId,
                        entry.SpeakerId,
                        entry.RecipientId.Value,
                        entry.Text,
                        entry.DisplayedAtTick,
                        entry.DisplayedAtUtc,
                        entry.Channel,
                        entry.AudienceIds));
                }
            }

            ConversationHistoryViewerSnapshot snapshot;
            try
            {
                snapshot = new ConversationHistoryViewerBuilder().Build(
                    selected.IndividualId,
                    participants.Select(value =>
                        new ConversationHistoryParticipant(
                            value.IndividualId,
                            value.DisplayLabel)),
                    turns,
                    maximumRows);
            }
            catch (Exception exception)
            {
                return new RimWorldConversationHistoryData(
                    participants,
                    null,
                    "Conversation history failed closed: " + exception.Message);
            }

            var combined = new ConversationHistoryViewerSnapshot(
                snapshot.ViewerId,
                snapshot.ViewerLabel,
                snapshot.Rows,
                checked(snapshot.PrivacyFilteredCount + privacyFiltered),
                checked(snapshot.MalformedCount + malformed),
                snapshot.TrimmedCount);
            return new RimWorldConversationHistoryData(
                participants,
                combined,
                "Read-only view of verified displayed dialogue; "
                + $"rows={combined.Rows.Count}; privacyFiltered={combined.PrivacyFilteredCount}; "
                + $"malformed={combined.MalformedCount}; trimmed={combined.TrimmedCount}.");
        }

        public override void StartedNewGame()
        {
            InitializeNewStore();
            InitializeReflectionRuntime(loadExisting: false);
            if (SynchronizeColonists()) PersistIfAllowed("new game enrollment");
            SeedInitialReflectionTasks();
            PersistReflectionIfDirty("new reflection store");
            LogStatus("new game");
            WriteSocialPathCertificationReport(postLoadAudit: false);
        }

        public override void LoadedGame()
        {
            _reflectionCheckpointGate.BeginLoadedSession();
            InitializeFromSave();
            ResumePendingDialogueAdmissions();
            InitializeReflectionRuntime(loadExisting: true);
            if (SynchronizeColonists()) PersistIfAllowed("post-load synchronization");
            SeedInitialReflectionTasks();
            if (_reflectionDirty)
            {
                Log.Message(
                    $"[Dagmay] {DagmayBuildInfo.Version} deferred post-load reflection synchronization "
                    + "until the next RimWorld save checkpoint.");
            }
            LogStatus("loaded game");
            WriteSocialPathCertificationReport(postLoadAudit: true);
        }

        public override void GameComponentTick()
        {
            if (!_initialized) return;
            RefreshRuntimeSettings();
            DrainCompletedReflectionCalls();
            if (Find.TickManager is null || Find.TickManager.TicksGame % ScanIntervalTicks != 0) return;

            if (SynchronizeColonists()) PersistIfAllowed("colonist identity change");
            var nowUtc = DateTimeOffset.UtcNow;
            if (nowUtc >= _nextReflectionHeartbeatUtc)
            {
                _nextReflectionHeartbeatUtc = nowUtc.Add(_runtimeBudgetPolicy.SchedulerHeartbeat);
                QueueBackgroundReflectionIfIdle(nowUtc);
            }

            PersistReflectionIfDirty("scheduler checkpoint");
            TryDispatchReflection(nowUtc);
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && _initialized)
            {
                DrainCompletedReflectionCalls();
                var identityStateChanged = SynchronizeColonists();
                RecoverPendingCommits();
                CheckpointDialogueAdmissions();
                if (IdentitySaveCheckpointPolicy.RequiresArchiveWrite(identityStateChanged))
                {
                    PersistIfAllowed("RimWorld save checkpoint");
                }
                var reflectionCheckpointPersisted = PersistReflectionIfDirty("RimWorld save checkpoint");
                if (reflectionCheckpointPersisted && _reflectionWritesEnabled && !_reflectionDirty)
                {
                    _reflectionCheckpointGate.CompleteRimWorldSaveCheckpoint();
                    _postLoadCheckpointPauseNoticeLogged = false;
                }
                if (_writesEnabled) BuildManifest();
                WriteSocialPathCertificationReport(postLoadAudit: false);
            }

            Scribe_Values.Look(ref _storeId, "dagmayStoreId", string.Empty);
            Scribe_Values.Look(ref _generation, "dagmayGeneration", 0L);
            Scribe_Values.Look(ref _reflectionGeneration, "dagmayReflectionGeneration", 0L);
            Scribe_Values.Look(ref _dialogueCheckpointGeneration, "mosaicDialogueCheckpointGeneration", 0L);
            Scribe_Values.Look(ref _experiencePosition, "dagmayExperiencePosition", 0L);
            Scribe_Values.Look(ref _experienceLastHash, "dagmayExperienceLastHash", string.Empty);
            Scribe_Collections.Look(ref _manifestExternalIds, "dagmayExternalIds", LookMode.Value);
            Scribe_Collections.Look(ref _manifestIndividualIds, "dagmayIndividualIds", LookMode.Value);
            Scribe_Collections.Look(ref _pausedExternalIds, "dagmayPausedExternalIds", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _manifestExternalIds = _manifestExternalIds ?? new List<string>();
                _manifestIndividualIds = _manifestIndividualIds ?? new List<string>();
                _pausedExternalIds = (_pausedExternalIds ?? new List<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .Take(500)
                    .ToList();
                _experienceLastHash = _experienceLastHash ?? string.Empty;
                if (_dialogueCheckpointGeneration < 0)
                {
                    _dialogueCheckpointGeneration = 0;
                    _dialogueCheckpointManifestInvalid = true;
                }
            }
        }

        private void InitializeNewStore()
        {
            if (_initialized) return;
            _storeId = Guid.NewGuid().ToString("N");
            _generation = 0;
            _reflectionGeneration = 0;
            _dialogueCheckpointGeneration = 0;
            _experiencePosition = 0;
            _experienceLastHash = string.Empty;
            _pendingExperienceRecovery = null;
            _experienceRecoveryDiagnostic = string.Empty;
            _writesEnabled = true;
            _experienceWritesEnabled = true;
            _reflectionWritesEnabled = true;
            _reflectionStoreWasNew = true;
            _dialogueCheckpointDirty = false;
            _dialogueCheckpointManifestInvalid = false;
            _pausedExternalIds = new List<string>();
            _sessionAttemptCount = 0;
            _lastDispatchedIndividualId = null;
            _invalidatedInFlightTasks.Clear();
            _storageSafetyPauseNoticeLogged = false;
            _initialized = true;
        }

        private void InitializeFromSave()
        {
            if (_initialized) return;
            if (_dialogueCheckpointManifestInvalid)
            {
                DisableWrites("The save contains an invalid Mosaic dialogue checkpoint generation.");
                _initialized = true;
                return;
            }
            var decision = SaveManifestSafetyPolicy.Evaluate(
                _storeId,
                _generation,
                _reflectionGeneration,
                _experiencePosition,
                _experienceLastHash,
                _manifestExternalIds,
                _manifestIndividualIds,
                _pausedExternalIds);
            if (decision.Disposition == SaveManifestDisposition.InitializeNewStore)
            {
                InitializeNewStore();
                Log.Message($"[Dagmay] No existing identity manifest was present; created a new {DagmayBuildInfo.Version} identity store for this save.");
                return;
            }
            if (decision.Disposition == SaveManifestDisposition.FailClosed || !decision.StoreId.HasValue)
            {
                DisableWrites(decision.Diagnostic + " No sidecar was opened and no replacement identity will be created.");
                _initialized = true;
                return;
            }

            var parsedStoreId = decision.StoreId.Value;
            var result = _archive.Load(GetArchivePath(), parsedStoreId, _generation);
            if (result.Status == ArchiveLoadStatus.NotFound)
            {
                if (_generation != 0 || _manifestExternalIds.Count > 0)
                {
                    DisableWrites("The RimWorld save references Dagmay identities, but the external identity archive is missing.");
                }
            }
            else if (result.Status == ArchiveLoadStatus.Unrecoverable || result.Snapshot is null)
            {
                DisableWrites(result.Diagnostic);
            }
            else
            {
                LoadVerifiedSnapshot(parsedStoreId, result);
            }

            _initialized = true;
            LoadExperienceJournal();
        }

        private void InitializeReflectionRuntime(bool loadExisting)
        {
            RefreshRuntimeSettings();
            if (loadExisting)
            {
                LoadReflectionStore();
                CompactRoutineReflectionBacklog();
            }
            else
            {
                _reflectionQueue = new PersistentReflectionQueue(_runtimeBudgetPolicy.MaximumQueueSize);
                _reflectionAudit = new List<ReflectionAuditRecord>();
                _reflectionDirty = true;
                _reflectionStoreWasNew = true;
            }

            _providerSelection?.Dispose();
            _providerSelection = RimWorldReflectionProviderSelection.FromEnvironment();
            Log.Message(
                $"[Dagmay] {DagmayBuildInfo.Version} reflection mode={_providerSelection.Mode}; "
                + _providerSelection.Diagnostic);

            _sessionAttemptCount = 0;
            _lastDispatchedIndividualId = null;
            _invalidatedInFlightTasks.Clear();
            _sessionBudgetNoticeLogged = false;
            _providerPauseNoticeLogged = false;
            _storageSafetyPauseNoticeLogged = false;
            _postLoadCheckpointPauseNoticeLogged = false;
            _nextReflectionHeartbeatUtc = DateTimeOffset.UtcNow.Add(_runtimeBudgetPolicy.SchedulerHeartbeat);
            _nextDispatchAtUtc = DateTimeOffset.UtcNow;
        }

        private void LoadExperienceJournal()
        {
            _pendingExperienceRecovery = null;
            _experienceRecoveryDiagnostic = string.Empty;

            var result = _experienceJournal.Load(GetExperienceJournalPath());
            if (result.Status == ExperienceJournalLoadStatus.NotFound)
            {
                if (_experiencePosition != 0 || _experienceLastHash.Length != 0)
                {
                    DisableExperienceWrites("The RimWorld save references experience history, but its journal is missing.");
                }

                return;
            }

            if (result.Status == ExperienceJournalLoadStatus.Invalid)
            {
                DisableExperienceWrites(result.Diagnostic);
                return;
            }

            if (result.Records.Count == _experiencePosition
                && string.Equals(result.LastHash, _experienceLastHash, StringComparison.Ordinal))
            {
                LoadExperienceRecords(result.Records);
                return;
            }

            var checkpointMatchesVerifiedPrefix = CheckpointMatchesVerifiedPrefix(result);
            if (checkpointMatchesVerifiedPrefix && result.Records.Count > _experiencePosition)
            {
                LoadExperienceRecords(result.Records.Take(checked((int)_experiencePosition)));
                _pendingExperienceRecovery = result;
                var aheadBy = result.Records.Count - _experiencePosition;
                _experienceRecoveryDiagnostic =
                    $"The verified external experience journal is ahead of the loaded RimWorld save checkpoint by {aheadBy} record(s) "
                    + $"(save={_experiencePosition}, external={result.Records.Count}). "
                    + "Dagmay loaded only the checkpointed prefix and paused experience writes. "
                    + "Restricted Observer can explicitly adopt the verified external head if preserving the newer Dagmay history is intended.";
                DisableExperienceWrites(_experienceRecoveryDiagnostic);
                return;
            }

            var actualPosition = result.Records.Count;
            DisableExperienceWrites(
                "The experience journal head does not match the RimWorld save checkpoint and no verified forward-only recovery was found. "
                + $"Save checkpoint position={_experiencePosition}; external journal position={actualPosition}. "
                + "Dagmay will not guess which history is authoritative.");
        }

        private bool CheckpointMatchesVerifiedPrefix(ExperienceJournalLoadResult result)
        {
            if (_experiencePosition < 0 || _experiencePosition > result.Records.Count) return false;
            if (_experiencePosition == 0) return _experienceLastHash.Length == 0;
            if (_experiencePosition > int.MaxValue || result.EntryHashes.Count < _experiencePosition) return false;
            return string.Equals(
                result.EntryHashes[checked((int)_experiencePosition - 1)],
                _experienceLastHash,
                StringComparison.Ordinal);
        }

        private void LoadExperienceRecords(IEnumerable<ExperienceJournalRecord> records)
        {
            foreach (var record in records)
            {
                _eventLedger.Append(record.FactualEvent);
                if (record.Perception is not null)
                {
                    _perceptionSources[record.Perception.Id] = record.Perception.SourceEventId;
                }

                if (record.Memory is not null)
                {
                    _memoryIndex.Add(record.Memory);
                }

                _experienceRecords.Add(record);
            }
        }

        public bool ExperienceRecoveryAvailable => _pendingExperienceRecovery is not null;

        public string ExperienceRecoveryDiagnostic => _experienceRecoveryDiagnostic;

        public bool AdoptVerifiedExternalExperienceHead(out string diagnostic)
        {
            diagnostic = string.Empty;
            var pending = _pendingExperienceRecovery;
            if (!_initialized || pending is null)
            {
                diagnostic = "No verified forward-only experience recovery is available for the current game.";
                return false;
            }

            if (!CheckpointMatchesVerifiedPrefix(pending) || pending.Records.Count <= _experiencePosition)
            {
                diagnostic = "The pending recovery no longer matches the loaded save checkpoint. No state was changed.";
                return false;
            }

            try
            {
                var startingPosition = _experiencePosition;
                LoadExperienceRecords(pending.Records.Skip(checked((int)startingPosition)));
                _experiencePosition = pending.Records.Count;
                _experienceLastHash = pending.LastHash;
                _experienceWritesEnabled = true;
                _storageSafetyPauseNoticeLogged = false;
                _pendingExperienceRecovery = null;
                _experienceRecoveryDiagnostic = string.Empty;

                var adopted = _experiencePosition - startingPosition;
                Log.Warning(
                    $"[Dagmay] {DagmayBuildInfo.Version} ADMINISTRATIVE RECOVERY adopted the verified external experience head; "
                    + $"checkpoint={startingPosition}; adoptedRecords={adopted}; newPosition={_experiencePosition}. "
                    + "This preserves newer Dagmay experience history across a RimWorld save rollback. Save the game now to checkpoint the adopted head.");
                diagnostic =
                    $"Adopted {adopted} verified experience record(s) from the external Dagmay journal. "
                    + "The identities were not replaced. Save the game now under a new name to checkpoint this recovery.";
                return true;
            }
            catch (Exception exception)
            {
                DisableExperienceWrites("Experience recovery failed safely: " + exception.Message);
                diagnostic = "Recovery failed safely; Dagmay remains read-only for experience storage. " + exception.Message;
                return false;
            }
        }

        private void LoadReflectionStore()
        {
            if (!Guid.TryParse(_storeId, out var expectedStoreId) || expectedStoreId == Guid.Empty)
            {
                _reflectionQueue = new PersistentReflectionQueue(_runtimeBudgetPolicy.MaximumQueueSize);
                _reflectionAudit = new List<ReflectionAuditRecord>();
                _reflectionStoreWasNew = false;
                DisableReflectionWrites("The reflection sidecar was not opened because the RimWorld save store ID is invalid.");
                return;
            }

            var result = _reflectionStore.Load(GetReflectionStorePath(), expectedStoreId, _reflectionGeneration);
            if (result.Status == ReflectionStoreLoadStatus.NotFound)
            {
                _reflectionQueue = new PersistentReflectionQueue(_runtimeBudgetPolicy.MaximumQueueSize);
                _reflectionAudit = new List<ReflectionAuditRecord>();
                _reflectionStoreWasNew = true;
                _reflectionDirty = true;
                if (_reflectionGeneration != 0)
                {
                    DisableReflectionWrites("The RimWorld save references reflection history, but its sidecar is missing.");
                }

                return;
            }

            if (result.Status == ReflectionStoreLoadStatus.Unrecoverable || result.Snapshot is null)
            {
                DisableReflectionWrites(result.Diagnostic);
                return;
            }

            _reflectionQueue = new PersistentReflectionQueue(
                _runtimeBudgetPolicy.MaximumQueueSize,
                result.Snapshot.PendingTasks);
            _reflectionAudit = new List<ReflectionAuditRecord>(result.Snapshot.AuditRecords);
            _reflectionGeneration = result.Snapshot.Generation;
            _reflectionStoreWasNew = false;
            if (result.Status == ReflectionStoreLoadStatus.RecoveredFromBackup)
            {
                DisableReflectionWrites(result.Diagnostic + $" No automatic overwrite will occur in Version {DagmayBuildInfo.Version}.");
            }
        }

        private void LoadVerifiedSnapshot(Guid expectedStoreId, ArchiveLoadResult result)
        {
            var snapshot = result.Snapshot!;
            if (snapshot.StoreId != expectedStoreId)
            {
                DisableWrites("The external archive store ID does not match the RimWorld save manifest.");
                return;
            }
            if (snapshot.Generation != _generation)
            {
                DisableWrites("The external archive generation does not match the RimWorld save checkpoint.");
                return;
            }

            if (!ManifestMatchesSnapshot(snapshot))
            {
                DisableWrites("The external identity archive does not match the identity mapping stored in the RimWorld save.");
                return;
            }

            foreach (var record in snapshot.Records) _identities.Add(record.ExternalEntityId, record.State);

            if (result.Status == ArchiveLoadStatus.RecoveredFromBackup)
            {
                DisableWrites(result.Diagnostic + $" No automatic overwrite will occur in Version {DagmayBuildInfo.Version}.");
            }
        }

        private bool ManifestMatchesSnapshot(IdentityArchiveSnapshot snapshot)
        {
            if (_manifestExternalIds.Count != _manifestIndividualIds.Count) return false;
            if (_manifestExternalIds.Count != snapshot.Records.Count) return false;
            var records = snapshot.Records.ToDictionary(record => record.ExternalEntityId, StringComparer.Ordinal);
            for (var index = 0; index < _manifestExternalIds.Count; index++)
            {
                if (!records.TryGetValue(_manifestExternalIds[index], out var record)) return false;
                if (!string.Equals(record.State.Id.ToString(), _manifestIndividualIds[index], StringComparison.Ordinal)) return false;
            }

            return true;
        }

        private bool SynchronizeColonists()
        {
            if (!_writesEnabled) return false;
            var changed = false;
            foreach (var map in Find.Maps.ToList())
            {
                foreach (var pawn in map.mapPawns.AllPawns.ToList())
                {
                    if ((pawn.IsColonist && !pawn.Dead) || _identities.ContainsKey(pawn.ThingID))
                    {
                        changed |= SynchronizePawn(pawn, forceEnrollment: false);
                    }
                }
            }

            return changed;
        }

        private IEnumerable<Pawn> SocialPeersFor(Pawn pawn)
        {
            foreach (var map in Find.Maps.ToList())
            {
                foreach (var candidate in map.mapPawns.AllPawns.ToList())
                {
                    if (candidate is null
                        || candidate.Dead
                        || ReferenceEquals(candidate, pawn)
                        || string.IsNullOrWhiteSpace(candidate.ThingID)
                        || !_identities.ContainsKey(candidate.ThingID))
                    {
                        continue;
                    }

                    yield return candidate;
                }
            }
        }

        private bool SynchronizePawn(Pawn pawn, bool forceEnrollment)
        {
            if (!_writesEnabled) return false;
            if (pawn is null || string.IsNullOrWhiteSpace(pawn.ThingID)) return false;
            var externalId = pawn.ThingID;
            var displayName = PawnDisplayName(pawn);
            var observation = PawnObservationCapture.Capture(pawn, SocialPeersFor(pawn));

            if (!_identities.TryGetValue(externalId, out var state))
            {
                if (pawn.Dead || !pawn.IsColonist) return false;
                if (!forceEnrollment && !DagmayMod.CurrentSettings.AutoEnrollNewColonists) return false;
                state = IndividualState.Create(displayName, PawnSeedExtractor.Capture(pawn));
                _identities.Add(externalId, state);
                _observations[externalId] = observation;
                Log.Message($"[Dagmay] {DagmayBuildInfo.Version} enrolled {displayName}; IndividualId={state.Id}; LineageId={state.LineageId}.");
                return true;
            }

            var changed = false;
            if (!pawn.Dead && !string.Equals(state.DisplayName, displayName, StringComparison.Ordinal))
            {
                state = state.Rename(displayName, state.Version);
                changed = true;
                Log.Message($"[Dagmay] {DagmayBuildInfo.Version} observed rename; IndividualId={state.Id}; displayName={displayName}.");
            }

            if (pawn.Dead && state.Lifecycle != LifecycleState.Archived && state.Lifecycle != LifecycleState.Dead)
            {
                state = state.TransitionLifecycle(
                    LifecycleState.Archived,
                    LifecycleTransitionKind.InWorldDeath,
                    state.Version);
                changed = true;
                RecordFactualOnly(externalId, state, "rimworld.lifecycle.death", new Dictionary<string, string>
                {
                    ["name"] = state.DisplayName,
                    ["lifecycle"] = state.Lifecycle.ToString()
                });
                Log.Message($"[Dagmay] {DagmayBuildInfo.Version} archived deceased individual {state.DisplayName}; IndividualId={state.Id}.");
            }

            if (!pawn.Dead)
            {
                if (!IsProcessingEnabled(externalId))
                {
                    _observations[externalId] = observation;
                    if (changed) _identities[externalId] = state;
                    return changed;
                }

                if (_observations.TryGetValue(externalId, out var previous))
                {
                    foreach (var change in PawnChangeDetector.Detect(previous, observation))
                    {
                        changed |= RecordExperiencedChange(externalId, ref state, change);
                    }
                }

                _observations[externalId] = observation;
            }

            if (changed) _identities[externalId] = state;
            return changed;
        }

        private bool RecordExperiencedChange(
            string externalId,
            ref IndividualState state,
            ObservedPawnChange change)
        {
            if (!_experienceWritesEnabled || state.Lifecycle != LifecycleState.Active) return false;
            try
            {
                var additionalSubjects = new List<IndividualId>();
                IndividualState? dialogueRecipient = null;
                var dialogueRecipientExternalId = string.Empty;
                if (change.FactualPayload.TryGetValue("target_external_id", out var targetExternalId)
                    && _identities.TryGetValue(targetExternalId, out var targetState)
                    && targetState.Id != state.Id)
                {
                    additionalSubjects.Add(targetState.Id);
                    dialogueRecipient = targetState;
                    dialogueRecipientExternalId = targetExternalId;
                }

                var factualEvent = CreateEvent(externalId, state, change.Kind, change.FactualPayload, additionalSubjects);
                var encoded = _experienceEncoder.EncodeExperiencedEvent(factualEvent, state, DateTimeOffset.UtcNow);
                var record = new ExperienceJournalRecord(factualEvent, encoded.Perception, encoded.Memory);
                var priorRelationshipEvidence = dialogueRecipient is null
                    ? Array.Empty<GroundedRelationshipEvidence>()
                    : BuildGroundedRelationshipHistory(state.Id, dialogueRecipient.Id);
                var recipientPriorRelationshipEvidence = dialogueRecipient is null
                    ? Array.Empty<GroundedRelationshipEvidence>()
                    : BuildGroundedRelationshipHistory(dialogueRecipient.Id, state.Id);
                var priorConversationContext = dialogueRecipient is null
                    ? null
                    : BuildPriorConversationContext(
                        state.Id,
                        dialogueRecipient.Id,
                        factualEvent.GameTick ?? 0);
                var appended = _experienceJournal.Append(
                    GetExperienceJournalPath(),
                    record,
                    _experiencePosition,
                    _experienceLastHash);

                var eventAppend = _eventLedger.Append(factualEvent);
                _perceptionSources[encoded.Perception.Id] = encoded.Perception.SourceEventId;
                _memoryIndex.Add(encoded.Memory);
                _experienceRecords.Add(record);
                _experiencePosition = appended.Position;
                _experienceLastHash = appended.EntryHash;
                state = encoded.UpdatedState;
                if (factualEvent.GameTick.HasValue)
                {
                    var projection = _readOnlyEventEnvelopeCapture.TryCapture(
                        experienceJournalAdmitted: true,
                        eventLedgerAdmitted: eventAppend.Status == EventAppendStatus.Appended,
                        factualEvent.Id,
                        factualEvent.GameTick.Value,
                        factualEvent.Kind,
                        factualEvent.FactualPayload.ToDictionary(
                            pair => pair.Key,
                            pair => pair.Value,
                            StringComparer.Ordinal),
                        state.Id,
                        dialogueRecipient?.Id);
                    if (projection is not null) NotifyReadOnlyEventEnvelope(projection);
                }
                QueueEventReflection(state, factualEvent);
                if (IsSocialCertificationKind(change.Kind))
                {
                    WriteSocialPathCertificationReport(postLoadAudit: false);
                }
                Log.Message(
                    $"[Dagmay] {DagmayBuildInfo.Version} recorded bounded experience; name={state.DisplayName}; "
                    + $"kind={change.Kind}; EventId={factualEvent.Id}; MemoryId={encoded.Memory.Id}.");
                if (dialogueRecipient is not null)
                {
                    var trigger = RimWorldSocialDialogueCapture.TryCreate(
                        factualEvent.Id,
                        factualEvent.GameTick ?? Find.TickManager?.TicksGame ?? 0,
                        factualEvent.ObservedAtUtc,
                        change.Kind,
                        change.FactualPayload,
                        CreateDialogueIdentitySnapshot(externalId, state),
                        CreateDialogueIdentitySnapshot(
                            dialogueRecipientExternalId,
                            dialogueRecipient),
                        priorRelationshipEvidence,
                        recipientPriorRelationshipEvidence,
                        priorConversationContext);
                    if (trigger is not null) NotifySocialDialogueTrigger(trigger);
                }
                return true;
            }
            catch (Exception exception)
            {
                DisableExperienceWrites("Experience recording failed: " + exception.Message);
                return false;
            }
        }

        private IReadOnlyList<GroundedRelationshipEvidence> BuildGroundedRelationshipHistory(
            IndividualId ownerId,
            IndividualId otherId)
        {
            var result = new List<GroundedRelationshipEvidence>();
            foreach (var record in _experienceRecords)
            {
                var evidence = GroundedRelationshipEvidence.TryCreate(record, ownerId, otherId);
                if (evidence is not null) result.Add(evidence);
            }

            return result
                .OrderByDescending(value => value.OccurredAtTick)
                .ThenBy(value => value.EventId.ToString(), StringComparer.Ordinal)
                .Take(16)
                .ToArray();
        }

        private PriorConversationContext? BuildPriorConversationContext(
            IndividualId firstParticipantId,
            IndividualId secondParticipantId,
            long throughTick)
        {
            var ledgerEntries = _eventLedger.Snapshot();
            var conversationIds = new HashSet<ConversationId>();
            foreach (var ledgerEntry in ledgerEntries)
            {
                var factualEvent = ledgerEntry.Value;
                if (!string.Equals(
                        factualEvent.Kind,
                        DialogueEventAdmissionService.EventKind,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        factualEvent.Source,
                        DialogueEventAdmissionService.DefaultSource,
                        StringComparison.Ordinal) ||
                    !factualEvent.Subjects.Contains(firstParticipantId) ||
                    !factualEvent.Subjects.Contains(secondParticipantId) ||
                    !factualEvent.FactualPayload.TryGetValue(
                        DialogueEventAdmissionService.ConversationIdKey,
                        out var conversationText) ||
                    !Guid.TryParseExact(conversationText, "N", out var conversationGuid) ||
                    conversationGuid == Guid.Empty)
                {
                    continue;
                }

                conversationIds.Add(new ConversationId(conversationGuid));
            }

            var turns = new List<PriorConversationTurn>();
            foreach (var conversationId in conversationIds)
            {
                var packet = _dialogueHistoryProjection.Build(
                    ledgerEntries,
                    new DialogueHistoryQuery(
                        conversationId,
                        throughTick,
                        4,
                        DialogueHistoryView.Participant,
                        firstParticipantId));
                foreach (var entry in packet.Entries)
                {
                    if (!entry.RecipientId.HasValue ||
                        !entry.AudienceIds.Contains(firstParticipantId) ||
                        !entry.AudienceIds.Contains(secondParticipantId))
                    {
                        continue;
                    }

                    turns.Add(new PriorConversationTurn(
                        entry.EventId,
                        entry.ConversationId,
                        entry.SpeakerId,
                        entry.RecipientId.Value,
                        entry.Text,
                        entry.DisplayedAtTick,
                        entry.DisplayedAtUtc,
                        entry.Channel,
                        entry.AudienceIds));
                }
            }

            return _conversationContinuitySelector.SelectLatestCompletedExchange(
                turns,
                firstParticipantId,
                secondParticipantId,
                throughTick);
        }

        private void RecordFactualOnly(
            string externalId,
            IndividualState state,
            string kind,
            IDictionary<string, string> payload)
        {
            if (!_experienceWritesEnabled) return;
            try
            {
                var factualEvent = CreateEvent(externalId, state, kind, payload);
                var appended = _experienceJournal.Append(
                    GetExperienceJournalPath(),
                    new ExperienceJournalRecord(factualEvent, null, null),
                    _experiencePosition,
                    _experienceLastHash);
                _eventLedger.Append(factualEvent);
                _experiencePosition = appended.Position;
                _experienceLastHash = appended.EntryHash;
            }
            catch (Exception exception)
            {
                DisableExperienceWrites("Factual lifecycle recording failed: " + exception.Message);
            }
        }

        private void QueueEventReflection(IndividualState state, EnvironmentEvent factualEvent)
        {
            if (!ReflectionStorageSafetyPolicy.AllowsQueueMutation(_experienceWritesEnabled, _reflectionWritesEnabled)
                || state.Lifecycle != LifecycleState.Active
                || !IsProcessingEnabled(state.Id))
            {
                return;
            }

            var plan = ReflectionEventTaskPolicy.Plan(state.Id, factualEvent);
            var sourceEventIds = _eventLedger.Snapshot()
                .Where(entry => entry.Value.Subjects.Contains(state.Id))
                .Where(entry =>
                {
                    var candidatePlan = ReflectionEventTaskPolicy.Plan(state.Id, entry.Value);
                    return string.Equals(candidatePlan.CoalescingKey, plan.CoalescingKey, StringComparison.Ordinal);
                })
                .OrderBy(entry => entry.Position)
                .Select(entry => entry.Value.Id)
                .Distinct()
                .Take(100)
                .ToList();

            if (sourceEventIds.Count < plan.MinimumEvidenceCount)
            {
                _reflectionAdmissionDeferredSession++;
                Log.Message(
                    $"[Dagmay] {DagmayBuildInfo.Version} retained experience locally; reflection admission deferred; "
                    + $"name={state.DisplayName}; kind={factualEvent.Kind}; evidence={sourceEventIds.Count}/{plan.MinimumEvidenceCount}; "
                    + $"queue={_reflectionQueue.Count}.");
                return;
            }

            var result = _reflectionQueue.EnqueueOrMerge(new ReflectionTask(
                ReflectionTaskId.New(),
                state.Id,
                plan.TaskKind,
                plan.Priority,
                factualEvent.ObservedAtUtc,
                plan.CoalescingKey,
                sourceEventIds,
                ReflectionEstimatedTokens));
            _reflectionDirty = true;

            if (result == PersistentQueueEnqueueStatus.Merged)
            {
                Log.Message(
                    $"[Dagmay] {DagmayBuildInfo.Version} coalesced reflection work; name={state.DisplayName}; "
                    + $"kind={factualEvent.Kind}; evidence={sourceEventIds.Count}; queue={_reflectionQueue.Count}.");
            }
            else
            {
                Log.Message(
                    $"[Dagmay] {DagmayBuildInfo.Version} admitted reflection work; name={state.DisplayName}; "
                    + $"kind={factualEvent.Kind}; evidence={sourceEventIds.Count}; priority={plan.Priority}; queue={_reflectionQueue.Count}.");
            }
        }

        private void SeedInitialReflectionTasks()
        {
            if (!ReflectionStorageSafetyPolicy.AllowsQueueMutation(_experienceWritesEnabled, _reflectionWritesEnabled)
                || !_reflectionStoreWasNew
                || _reflectionQueue.Count != 0
                || _reflectionAudit.Count != 0)
            {
                return;
            }

            var entries = _eventLedger.Snapshot();
            var nowUtc = DateTimeOffset.UtcNow;
            var count = 0;
            foreach (var state in _identities.Values.Where(value =>
                value.Lifecycle == LifecycleState.Active && IsProcessingEnabled(value.Id)))
            {
                var evidence = entries
                    .Where(entry => entry.Value.Subjects.Contains(state.Id))
                    .OrderByDescending(entry => entry.Position)
                    .Take(5)
                    .OrderBy(entry => entry.Position)
                    .Select(entry => entry.Value.Id)
                    .ToList();
                if (evidence.Count == 0) continue;
                _reflectionQueue.EnqueueOrMerge(new ReflectionTask(
                    ReflectionTaskId.New(),
                    state.Id,
                    ModelTaskKind.InterpretMeaningfulEvent,
                    ReflectionPriority.MeaningfulEvent,
                    nowUtc,
                    "upgrade-0.1d:" + state.Id,
                    evidence,
                    ReflectionEstimatedTokens));
                count++;
            }

            if (count > 0)
            {
                _reflectionDirty = true;
                Log.Message($"[Dagmay] {DagmayBuildInfo.Version} queued {count} initial reflection task(s) from verified 0.1C history.");
            }
        }

        private void QueueBackgroundReflectionIfIdle(DateTimeOffset nowUtc)
        {
            if (!ReflectionStorageSafetyPolicy.AllowsQueueMutation(_experienceWritesEnabled, _reflectionWritesEnabled)
                || _reflectionQueue.Count != 0)
            {
                return;
            }
            var active = _identities.Values
                .Where(value => value.Lifecycle == LifecycleState.Active && IsProcessingEnabled(value.Id))
                .OrderBy(value => value.Id.ToString(), StringComparer.Ordinal)
                .ToList();
            if (active.Count == 0) return;

            var entries = _eventLedger.Snapshot();
            for (var offset = 0; offset < active.Count; offset++)
            {
                var index = (_backgroundCursor + offset) % active.Count;
                var state = active[index];
                var evidence = entries
                    .Where(entry => entry.Value.Subjects.Contains(state.Id))
                    .OrderByDescending(entry => entry.Position)
                    .Take(5)
                    .OrderBy(entry => entry.Position)
                    .Select(entry => entry.Value.Id)
                    .ToList();
                if (evidence.Count == 0) continue;

                _reflectionQueue.EnqueueOrMerge(new ReflectionTask(
                    ReflectionTaskId.New(),
                    state.Id,
                    ModelTaskKind.BackgroundReflection,
                    ReflectionPriority.Background,
                    nowUtc,
                    "background:" + state.Id + ":" + nowUtc.ToUniversalTime().ToString("yyyyMMddHHmm"),
                    evidence,
                    ReflectionEstimatedTokens));
                _backgroundCursor = (index + 1) % active.Count;
                _reflectionDirty = true;
                return;
            }
        }

        private void TryDispatchReflection(DateTimeOffset nowUtc)
        {
            if (!ReflectionStorageSafetyPolicy.AllowsProcessing(
                    _writesEnabled,
                    _experienceWritesEnabled,
                    _reflectionWritesEnabled))
            {
                LogStorageSafetyPauseOnce();
                return;
            }

            if (!_reflectionCheckpointGate.AllowsSidecarPersistence(rimWorldSaveInProgress: false))
            {
                if (!_postLoadCheckpointPauseNoticeLogged && _reflectionQueue.Count > 0)
                {
                    Log.Message(
                        $"[Dagmay] {DagmayBuildInfo.Version} reflection processing is waiting for the first "
                        + "RimWorld save checkpoint after load; queued work remains in memory and no provider call will start.");
                    _postLoadCheckpointPauseNoticeLogged = true;
                }

                return;
            }

            if (_requestInFlight
                || nowUtc < _nextDispatchAtUtc
                || _providerSelection?.Provider is null)
            {
                return;
            }

            if (DagmayMod.CurrentSettings.ProviderDispatchPaused)
            {
                if (!_providerPauseNoticeLogged && _reflectionQueue.Count > 0)
                {
                    Log.Message($"[Dagmay] {DagmayBuildInfo.Version} provider dispatch is paused in settings; queued work remains durable.");
                    _providerPauseNoticeLogged = true;
                }

                return;
            }

            _providerPauseNoticeLogged = false;

            if (!_reflectionQueue.TrySelect(nowUtc, _lastDispatchedIndividualId, out var pending) || pending is null) return;
            if (!TryFindIdentity(pending.Task.IndividualId, out _, out var state)
                || state.Lifecycle != LifecycleState.Active
                || !IsProcessingEnabled(pending.Task.IndividualId))
            {
                QuarantineUndispatchableTask(
                    pending,
                    "IDENTITY_UNAVAILABLE_OR_PAUSED",
                    "The target identity is absent, inactive, or paused from Dagmay processing.");
                return;
            }

            var decision = _reflectionBudget.Evaluate(
                nowUtc,
                pending,
                _reflectionAudit,
                _sessionAttemptCount);
            if (!decision.IsAllowed)
            {
                if (decision.BlockReason == ReflectionDispatchBlockReason.SessionRequestLimit)
                {
                    if (!_sessionBudgetNoticeLogged)
                    {
                        Log.Message(
                            $"[Dagmay] {DagmayBuildInfo.Version} session request limit reached ({_sessionAttemptCount}/{_runtimeBudgetPolicy.MaximumRequestsPerSession}); "
                            + $"queued work remains durable; QueueRemaining={_reflectionQueue.Count}; {QueueCompositionSummary()}; "
                            + "no additional model call will be made this session.");
                        _sessionBudgetNoticeLogged = true;
                    }

                    return;
                }

                _reflectionQueue.Defer(
                    pending.Task.Id,
                    decision.RetryAtUtc ?? nowUtc.AddMinutes(5),
                    decision.BlockReason.ToString());
                _reflectionDirty = true;
                PersistReflectionIfDirty("reflection budget deferral");
                return;
            }

            ModelRequest request;
            try
            {
                request = _reflectionContextBuilder.BuildRequest(
                    pending.Task,
                    state,
                    ResolveSourceEvents(pending.Task.SourceEventIds),
                    RelevantMemories(state.Id),
                    nowUtc,
                    ReflectionRequestTimeout);
            }
            catch (Exception exception)
            {
                QuarantineUndispatchableTask(
                    pending,
                    "CONTEXT_BUILD_FAILED",
                    "The reflection context could not be built: " + exception.Message);
                return;
            }

            var attemptNumber = pending.AttemptCount + 1;
            AddAudit(CreateAttemptStarted(pending, request, attemptNumber));
            if (!PersistReflectionIfDirty("pre-dispatch audit checkpoint")) return;

            var provider = _providerSelection.Provider;
            _sessionAttemptCount++;
            _lastDispatchedIndividualId = pending.Task.IndividualId;
            _requestInFlight = true;
            _inFlightTaskId = pending.Task.Id;
            _nextDispatchAtUtc = nowUtc.Add(MinimumDispatchSpacing);
            Log.Message(
                $"[Dagmay] {DagmayBuildInfo.Version} dispatched reflection; provider={_providerSelection.ProviderId}; "
                + $"model={_providerSelection.ModelId}; RequestId={request.Id}; "
                + $"IndividualId={request.IndividualId}; attempt={attemptNumber}; "
                + $"session={_sessionAttemptCount}/{_runtimeBudgetPolicy.MaximumRequestsPerSession}.");
            _ = Task.Run(() => RunProviderCallAsync(provider, pending, request, attemptNumber));
        }

        private async Task RunProviderCallAsync(
            IModelProvider provider,
            PendingReflectionTask pending,
            ModelRequest request,
            int attemptNumber)
        {
            ModelResult result;
            try
            {
                result = await provider.GenerateStructuredAsync(request, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                result = ModelResult.Failure(
                    request.Id,
                    ModelResultStatus.ProviderError,
                    provider.ProviderId,
                    string.Empty,
                    "UNEXPECTED_PROVIDER_EXCEPTION",
                    "The provider raised an unexpected " + exception.GetType().Name + ".",
                    TimeSpan.Zero,
                    retryable: true);
            }

            _completedReflectionCalls.Enqueue(new CompletedReflectionCall(
                pending,
                request,
                attemptNumber,
                result));
        }

        private void DrainCompletedReflectionCalls()
        {
            while (_completedReflectionCalls.TryDequeue(out var completed))
            {
                _requestInFlight = false;
                HandleProviderResult(completed);
                _inFlightTaskId = null;
            }
        }

        private void HandleProviderResult(CompletedReflectionCall completed)
        {
            var currentTask = _reflectionQueue.Find(completed.Pending.Task.Id);
            if (currentTask is null)
            {
                Log.Error($"[Dagmay] {DagmayBuildInfo.Version} discarded a provider result because its durable task is no longer present.");
                return;
            }

            if (!ReflectionStorageSafetyPolicy.AllowsProcessing(
                    _writesEnabled,
                    _experienceWritesEnabled,
                    _reflectionWritesEnabled))
            {
                LogStorageSafetyPauseOnce();
                return;
            }

            var result = completed.Result;
            if (result.RequestId != completed.Request.Id)
            {
                QuarantineCompletedTask(completed, "REQUEST_ID_MISMATCH", "The provider returned the wrong request ID.");
                return;
            }

            if (_invalidatedInFlightTasks.Remove(completed.Pending.Task.Id))
            {
                QuarantineCompletedTask(
                    completed,
                    "ENROLLMENT_PAUSED_AFTER_DISPATCH",
                    "The individual was paused after dispatch; this response remains invalid even if processing was resumed before it completed.");
                return;
            }

            if (!IsProcessingEnabled(completed.Request.IndividualId))
            {
                QuarantineCompletedTask(
                    completed,
                    "ENROLLMENT_PAUSED",
                    "The individual was paused after dispatch; the response cannot mutate identity state.");
                return;
            }

            if (result.Status != ModelResultStatus.Success)
            {
                AddAudit(CreateResultAudit(
                    completed,
                    ReflectionAuditStatus.AttemptFailed,
                    result.ErrorMessage,
                    includePayload: false));
                if (result.Retryable && completed.AttemptNumber < MaximumReflectionAttempts)
                {
                    var delay = ReflectionBudgetGate.RetryDelay(completed.AttemptNumber, result.RetryAfter);
                    _reflectionQueue.MarkRetry(
                        completed.Pending.Task.Id,
                        DateTimeOffset.UtcNow.Add(delay),
                        result.ErrorCode);
                }
                else
                {
                    _reflectionQueue.Remove(completed.Pending.Task.Id);
                    AddAudit(CreateResultAudit(
                        completed,
                        ReflectionAuditStatus.Quarantined,
                        "The failed response was quarantined and cannot mutate identity state.",
                        includePayload: false));
                }

                _reflectionDirty = true;
                PersistReflectionIfDirty("provider failure handling");
                Log.Error(
                    $"[Dagmay] {DagmayBuildInfo.Version} reflection attempt failed safely; status={result.Status}; "
                    + $"code={result.ErrorCode}; retryable={result.Retryable}.");
                return;
            }

            ReflectionProposal proposal;
            try
            {
                proposal = ReflectionProposalJson.Parse(result.StructuredPayload);
            }
            catch (Exception exception)
            {
                QuarantineCompletedTask(
                    completed,
                    "INVALID_STRUCTURED_OUTPUT",
                    "Strict local validation rejected the provider payload: " + exception.Message);
                return;
            }

            if (!TryFindIdentity(completed.Request.IndividualId, out var externalId, out var current))
            {
                QuarantineCompletedTask(completed, "IDENTITY_UNAVAILABLE", "The target identity no longer exists.");
                return;
            }

            var validation = _reflectionValidator.Validate(completed.Request, proposal, current, _eventLedger);
            if (!validation.IsValid || validation.Replacement is null)
            {
                QuarantineCompletedTask(
                    completed,
                    "VALIDATION_" + validation.Status.ToString().ToUpperInvariant(),
                    validation.Diagnostic);
                return;
            }

            AddAudit(CreateResultAudit(
                completed,
                ReflectionAuditStatus.PendingCommit,
                validation.Diagnostic,
                includePayload: true));
            if (!PersistReflectionIfDirty("pre-commit reflection checkpoint")) return;
            if (!TryPersistIdentityReplacement(
                    externalId,
                    current,
                    validation.Replacement,
                    "validated reflection commit"))
            {
                return;
            }

            _reflectionQueue.Remove(completed.Pending.Task.Id);
            AddAudit(CreateResultAudit(
                completed,
                ReflectionAuditStatus.Committed,
                "The validated reflection was atomically committed to the identity archive.",
                includePayload: false));
            PersistReflectionIfDirty("reflection commit completion");
            Log.Message(
                $"[Dagmay] {DagmayBuildInfo.Version} committed validated reflection; IndividualId={current.Id}; "
                + $"RequestId={completed.Request.Id}; newVersion={validation.Replacement.Version}.");
        }

        private void QuarantineCompletedTask(CompletedReflectionCall completed, string code, string diagnostic)
        {
            _reflectionQueue.Remove(completed.Pending.Task.Id);
            var result = completed.Result.Status == ModelResultStatus.Success
                ? ModelResult.Failure(
                    completed.Request.Id,
                    ModelResultStatus.InvalidResponse,
                    completed.Result.Provider,
                    completed.Result.Model,
                    code,
                    SafeAuditText(diagnostic, 2000),
                    completed.Result.Latency,
                    providerOperationId: completed.Result.ProviderOperationId)
                : completed.Result;
            AddAudit(CreateResultAudit(
                new CompletedReflectionCall(
                    completed.Pending,
                    completed.Request,
                    completed.AttemptNumber,
                    result),
                ReflectionAuditStatus.Quarantined,
                diagnostic,
                includePayload: completed.Result.Status == ModelResultStatus.Success,
                payloadOverride: completed.Result.StructuredPayload));
            _reflectionDirty = true;
            PersistReflectionIfDirty("quarantined reflection response");
            Log.Error($"[Dagmay] {DagmayBuildInfo.Version} quarantined reflection response; code={code}. No identity mutation occurred.");
        }

        private void QuarantineUndispatchableTask(
            PendingReflectionTask pending,
            string code,
            string diagnostic)
        {
            _reflectionQueue.Remove(pending.Task.Id);
            if (TryFindIdentity(pending.Task.IndividualId, out _, out var state))
            {
                AddAudit(new ReflectionAuditRecord(
                    ReflectionRecordId.New(),
                    pending.Task.Id,
                    RequestId.New(),
                    pending.Task.IndividualId,
                    state.LineageId,
                    state.Version,
                    pending.Task.TaskKind,
                    ReflectionContextBuilder.PromptVersion,
                    DateTimeOffset.UtcNow,
                    ReflectionAuditStatus.Quarantined,
                    Math.Max(1, pending.AttemptCount + 1),
                    _providerSelection?.ProviderId ?? "none",
                    _providerSelection?.ModelId ?? string.Empty,
                    string.Empty,
                    ModelResultStatus.InvalidResponse,
                    code,
                    diagnostic,
                    false,
                    pending.Task.EstimatedTokens,
                    0,
                    0,
                    0,
                    string.Empty,
                    string.Empty,
                    SafeAuditText(diagnostic, 2000)));
            }

            _reflectionDirty = true;
            PersistReflectionIfDirty("undispatchable task quarantine");
            Log.Error($"[Dagmay] {DagmayBuildInfo.Version} quarantined reflection task; code={code}. No model call was made.");
        }

        private ReflectionAuditRecord CreateAttemptStarted(
            PendingReflectionTask pending,
            ModelRequest request,
            int attemptNumber)
        {
            return new ReflectionAuditRecord(
                ReflectionRecordId.New(),
                pending.Task.Id,
                request.Id,
                request.IndividualId,
                request.LineageId,
                request.BaseStateVersion,
                request.TaskKind,
                request.PromptVersion,
                DateTimeOffset.UtcNow,
                ReflectionAuditStatus.AttemptStarted,
                attemptNumber,
                _providerSelection?.ProviderId ?? "none",
                _providerSelection?.ModelId ?? string.Empty,
                string.Empty,
                ModelResultStatus.NotRun,
                string.Empty,
                string.Empty,
                false,
                pending.Task.EstimatedTokens,
                0,
                0,
                0,
                SafeAuditText(request.Context, 100_000),
                string.Empty,
                "The durable attempt record was saved before transport began.");
        }

        private static ReflectionAuditRecord CreateResultAudit(
            CompletedReflectionCall completed,
            ReflectionAuditStatus status,
            string diagnostic,
            bool includePayload,
            string? payloadOverride = null)
        {
            var result = completed.Result;
            return new ReflectionAuditRecord(
                ReflectionRecordId.New(),
                completed.Pending.Task.Id,
                completed.Request.Id,
                completed.Request.IndividualId,
                completed.Request.LineageId,
                completed.Request.BaseStateVersion,
                completed.Request.TaskKind,
                completed.Request.PromptVersion,
                DateTimeOffset.UtcNow,
                status,
                completed.AttemptNumber,
                SafeAuditText(result.Provider, 128),
                SafeAuditText(result.Model, 128),
                SafeAuditText(result.ProviderOperationId, 256),
                result.Status,
                SafeAuditText(result.ErrorCode, 128),
                SafeAuditText(result.ErrorMessage, 2000),
                result.Retryable,
                completed.Pending.Task.EstimatedTokens,
                result.PromptTokens,
                result.OutputTokens,
                result.TotalTokens,
                string.Empty,
                includePayload
                    ? SafeAuditText(payloadOverride ?? result.StructuredPayload, 100_000)
                    : string.Empty,
                SafeAuditText(diagnostic, 2000));
        }

        private void AddAudit(ReflectionAuditRecord record)
        {
            while (_reflectionAudit.Count >= MaximumAuditRecords)
            {
                var removable = _reflectionAudit.FindIndex(value =>
                    value.Status != ReflectionAuditStatus.PendingCommit
                    || _reflectionQueue.Find(value.TaskId) is null);
                if (removable < 0)
                {
                    throw new InvalidOperationException("The reflection audit is full of unresolved commits.");
                }

                _reflectionAudit.RemoveAt(removable);
            }

            _reflectionAudit.Add(record);
            _reflectionDirty = true;
        }

        private void RecoverPendingCommits()
        {
            var recoveryDisposition = ReflectionStorageSafetyPolicy.ClassifyPendingCommitRecovery(
                _writesEnabled,
                _experienceWritesEnabled,
                _reflectionWritesEnabled,
                _reflectionAudit.Count);

            if (recoveryDisposition == PendingCommitRecoveryDisposition.StorageUnavailable)
            {
                LogStorageSafetyPauseOnce();
                return;
            }

            if (recoveryDisposition == PendingCommitRecoveryDisposition.NothingToRecover)
            {
                return;
            }
            var pendingRecords = new List<ReflectionAuditRecord>();
            for (var index = 0; index < _reflectionAudit.Count; index++)
            {
                var candidate = _reflectionAudit[index];
                if (candidate.Status != ReflectionAuditStatus.PendingCommit) continue;
                var terminalLater = _reflectionAudit.Skip(index + 1).Any(value =>
                    value.TaskId == candidate.TaskId
                    && (value.Status == ReflectionAuditStatus.Committed
                        || value.Status == ReflectionAuditStatus.Quarantined));
                if (!terminalLater) pendingRecords.Add(candidate);
            }

            foreach (var record in pendingRecords)
            {
                var pending = _reflectionQueue.Find(record.TaskId);
                if (pending is null
                    || !TryFindIdentity(record.IndividualId, out var externalId, out var state))
                {
                    AddAudit(CopyRecoveryAudit(
                        record,
                        ReflectionAuditStatus.Quarantined,
                        "Recovery could not find the durable task or identity."));
                    if (pending is not null) _reflectionQueue.Remove(pending.Task.Id);
                    continue;
                }

                ReflectionProposal proposal;
                try
                {
                    proposal = ReflectionProposalJson.Parse(record.StructuredPayload);
                }
                catch (Exception exception)
                {
                    _reflectionQueue.Remove(pending.Task.Id);
                    AddAudit(CopyRecoveryAudit(
                        record,
                        ReflectionAuditStatus.Quarantined,
                        "Recovery rejected the stored proposal: " + exception.Message));
                    continue;
                }

                if (state.Id == proposal.IndividualId
                    && state.LineageId == record.LineageId
                    && state.Version == record.BaseStateVersion + 1
                    && AffectMatches(state, proposal))
                {
                    _reflectionQueue.Remove(pending.Task.Id);
                    AddAudit(CopyRecoveryAudit(
                        record,
                        ReflectionAuditStatus.Committed,
                        "Recovery verified that the identity archive already contains this commit."));
                    continue;
                }

                if (state.Version != record.BaseStateVersion)
                {
                    _reflectionQueue.Remove(pending.Task.Id);
                    AddAudit(CopyRecoveryAudit(
                        record,
                        ReflectionAuditStatus.Quarantined,
                        "Recovery found a conflicting identity version; no mutation was applied."));
                    continue;
                }

                var request = new ModelRequest(
                    record.RequestId,
                    record.IndividualId,
                    record.LineageId,
                    record.BaseStateVersion,
                    pending.Task.TaskKind,
                    ReflectionContextBuilder.PromptVersion,
                    ReflectionContextBuilder.SystemInstruction,
                    string.IsNullOrWhiteSpace(record.RequestContext)
                        ? "Recovery of a previously validated durable proposal."
                        : record.RequestContext,
                    ReflectionProposalJson.ProviderCompatibleSchema,
                    pending.Task.SourceEventIds,
                    state.Affect,
                    DateTimeOffset.UtcNow.AddMinutes(1),
                    768);
                var validation = _reflectionValidator.Validate(request, proposal, state, _eventLedger);
                if (!validation.IsValid || validation.Replacement is null)
                {
                    _reflectionQueue.Remove(pending.Task.Id);
                    AddAudit(CopyRecoveryAudit(
                        record,
                        ReflectionAuditStatus.Quarantined,
                        "Recovery validation failed: " + validation.Diagnostic));
                    continue;
                }

                if (!TryPersistIdentityReplacement(
                        externalId,
                        state,
                        validation.Replacement,
                        "pending reflection recovery"))
                {
                    break;
                }

                _reflectionQueue.Remove(pending.Task.Id);
                AddAudit(CopyRecoveryAudit(
                    record,
                    ReflectionAuditStatus.Committed,
                    "Recovery completed the validated identity commit."));
            }

            PersistReflectionIfDirty("pending commit recovery");
        }

        private static ReflectionAuditRecord CopyRecoveryAudit(
            ReflectionAuditRecord source,
            ReflectionAuditStatus status,
            string diagnostic)
        {
            return new ReflectionAuditRecord(
                ReflectionRecordId.New(),
                source.TaskId,
                source.RequestId,
                source.IndividualId,
                source.LineageId,
                source.BaseStateVersion,
                source.TaskKind,
                source.PromptVersion,
                DateTimeOffset.UtcNow,
                status,
                source.AttemptNumber,
                source.Provider,
                source.Model,
                source.ProviderOperationId,
                source.ResultStatus,
                source.ErrorCode,
                source.ErrorMessage,
                source.Retryable,
                source.EstimatedTokens,
                source.PromptTokens,
                source.OutputTokens,
                source.TotalTokens,
                string.Empty,
                string.Empty,
                diagnostic);
        }

        private static bool AffectMatches(IndividualState state, ReflectionProposal proposal)
        {
            const double tolerance = 0.000000001;
            return Math.Abs(state.Affect.Valence - proposal.TargetAffect.Valence) <= tolerance
                && Math.Abs(state.Affect.Arousal - proposal.TargetAffect.Arousal) <= tolerance
                && Math.Abs(state.Affect.Threat - proposal.TargetAffect.Threat) <= tolerance
                && Math.Abs(state.Affect.Agency - proposal.TargetAffect.Agency) <= tolerance
                && Math.Abs(state.Affect.Attachment - proposal.TargetAffect.Attachment) <= tolerance
                && Math.Abs(state.Affect.Certainty - proposal.TargetAffect.Certainty) <= tolerance
                && Math.Abs(state.Affect.SocialStanding - proposal.TargetAffect.SocialStanding) <= tolerance;
        }

        private static string SafeAuditText(string value, int maximumLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var builder = new StringBuilder(Math.Min(value.Length, maximumLength));
            for (var index = 0; index < value.Length && builder.Length < maximumLength; index++)
            {
                var character = value[index];
                if (character == '\t'
                    || character == '\n'
                    || character == '\r'
                    || (character >= 0x20 && character <= 0xD7FF)
                    || (character >= 0xE000 && character <= 0xFFFD))
                {
                    builder.Append(character);
                    continue;
                }

                if (char.IsHighSurrogate(character)
                    && index + 1 < value.Length
                    && char.IsLowSurrogate(value[index + 1])
                    && builder.Length + 2 <= maximumLength)
                {
                    builder.Append(character);
                    builder.Append(value[++index]);
                    continue;
                }

                builder.Append('\uFFFD');
            }

            return builder.ToString();
        }

        private IReadOnlyList<EnvironmentEvent> ResolveSourceEvents(IEnumerable<EventId> ids)
        {
            var requested = new HashSet<EventId>(ids);
            return _eventLedger.Snapshot()
                .Where(entry => requested.Contains(entry.Value.Id))
                .Select(entry => entry.Value)
                .ToList();
        }

        private IReadOnlyList<SubjectiveMemory> RelevantMemories(IndividualId individualId)
        {
            var result = new List<SubjectiveMemory>();
            var seen = new HashSet<MemoryId>();
            foreach (var memory in _memoryIndex.Recent(individualId, 10)
                .Concat(_memoryIndex.MostSignificant(individualId, 10)))
            {
                if (seen.Add(memory.Id)) result.Add(memory);
            }

            return result;
        }

        private bool TryFindIdentity(
            IndividualId individualId,
            out string externalId,
            out IndividualState state)
        {
            foreach (var pair in _identities)
            {
                if (pair.Value.Id != individualId) continue;
                externalId = pair.Key;
                state = pair.Value;
                return true;
            }

            externalId = string.Empty;
            state = null!;
            return false;
        }

        public bool TryQueueDialogueAdmission(
            DialogueRequest request,
            DisplayedUtteranceReceipt receipt,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_initialized || !_writesEnabled || !_experienceWritesEnabled)
            {
                diagnostic = "Mosaic dialogue storage is not initialized and writable.";
                return false;
            }

            var admission = new DialogueEventAdmissionService("rimworld").Prepare(request, receipt);
            if (!admission.IsPrepared || admission.Plan is null)
            {
                diagnostic = admission.Diagnostic;
                return false;
            }

            try
            {
                var result = _dialogueAdmissionCoordinator.EnqueuePending(
                    GetDialogueOutboxPath(),
                    CreateDialogueAdmissionBinding(_dialogueCheckpointGeneration),
                    admission.Plan,
                    storageWritable: true,
                    DateTimeOffset.UtcNow);
                if (result.Status != DialogueAdmissionRecoveryStatus.Queued &&
                    result.Status != DialogueAdmissionRecoveryStatus.NoWork)
                {
                    DisableExperienceWrites(
                        "Dialogue admission recovery failed closed: " + result.Diagnostic);
                    diagnostic = result.Diagnostic;
                    return false;
                }

                _dialogueCheckpointDirty = true;
                diagnostic = result.Diagnostic;
                return true;
            }
            catch (Exception exception)
            {
                DisableExperienceWrites(
                    "Dialogue admission failed after preserving its outbox evidence: " + exception.Message);
                diagnostic = exception.Message;
                return false;
            }
        }

        private void CheckpointDialogueAdmissions()
        {
            if (!_dialogueCheckpointDirty || !_writesEnabled || !_experienceWritesEnabled) return;
            var nextGeneration = checked(_dialogueCheckpointGeneration + 1);
            try
            {
                var recovery = _dialogueAdmissionCoordinator.Recover(
                    GetDialogueOutboxPath(),
                    GetExperienceJournalPath(),
                    CreateDialogueAdmissionBinding(_dialogueCheckpointGeneration),
                    _eventLedger,
                    storageWritable: true,
                    DateTimeOffset.UtcNow);
                if (recovery.Status != DialogueAdmissionRecoveryStatus.Completed ||
                    recovery.CompletedCount < 1)
                {
                    DisableExperienceWrites(
                        "Dialogue checkpoint recovery found no verified pending admission: "
                        + recovery.Diagnostic);
                    return;
                }
                if (!RefreshDialogueJournalHead())
                {
                    DisableExperienceWrites(
                        "Dialogue checkpoint recovery did not produce a verified journal head.");
                    return;
                }

                var advancement = _dialogueAdmissionCoordinator.Recover(
                    GetDialogueOutboxPath(),
                    GetExperienceJournalPath(),
                    CreateDialogueAdmissionBinding(nextGeneration),
                    _eventLedger,
                    storageWritable: true,
                    DateTimeOffset.UtcNow);
                if (advancement.Status != DialogueAdmissionRecoveryStatus.Completed &&
                    advancement.Status != DialogueAdmissionRecoveryStatus.NoWork)
                {
                    DisableExperienceWrites(
                        "Dialogue checkpoint advancement failed closed: " + advancement.Diagnostic);
                    return;
                }

                _dialogueCheckpointGeneration = nextGeneration;
                _dialogueCheckpointDirty = false;
            }
            catch (Exception exception)
            {
                DisableExperienceWrites(
                    "Dialogue checkpoint advancement failed safely: " + exception.Message);
            }
        }

        private void ResumePendingDialogueAdmissions()
        {
            if (!_initialized || !_writesEnabled || !_experienceWritesEnabled) return;
            try
            {
                var inspection = _dialogueAdmissionCoordinator.InspectPending(
                    GetDialogueOutboxPath(),
                    CreateDialogueAdmissionBinding(_dialogueCheckpointGeneration));
                if (inspection.Status == DialogueAdmissionRecoveryStatus.Queued)
                {
                    _dialogueCheckpointDirty = true;
                    Log.Message(
                        $"[Dagmay] {DagmayBuildInfo.Version} retained pending dialogue "
                        + "admission evidence for the next save checkpoint.");
                }
                else if (inspection.Status != DialogueAdmissionRecoveryStatus.NoWork)
                {
                    DisableExperienceWrites(
                        "Dialogue outbox inspection failed closed: " + inspection.Diagnostic);
                }
            }
            catch (Exception exception)
            {
                DisableExperienceWrites(
                    "Dialogue outbox inspection failed safely: " + exception.Message);
            }
        }

        private bool RefreshDialogueJournalHead(EventId? requiredEventId = null)
        {
            var journal = _experienceJournal.Load(GetExperienceJournalPath());
            if (journal.Status == ExperienceJournalLoadStatus.Invalid) return false;
            if (requiredEventId.HasValue &&
                journal.Records.Count(record => record.FactualEvent.Id == requiredEventId.Value) != 1)
            {
                return false;
            }

            _experiencePosition = journal.Records.Count;
            _experienceLastHash = journal.LastHash;
            return true;
        }

        private DialogueAdmissionBinding CreateDialogueAdmissionBinding(long generation)
        {
            if (!Guid.TryParseExact(_storeId, "N", out var storeId))
                throw new InvalidOperationException("The Mosaic StoreId is invalid.");
            var worldId = Verse.Current.Game?.World?.GetUniqueLoadID();
            if (string.IsNullOrWhiteSpace(worldId))
                throw new InvalidOperationException("The RimWorld world identity is unavailable.");
            return new DialogueAdmissionBinding(
                storeId,
                _storeId,
                worldId!,
                generation);
        }

        private static RimWorldDialogueIdentitySnapshot CreateDialogueIdentitySnapshot(
            string externalId,
            IndividualState state) =>
            new RimWorldDialogueIdentitySnapshot(
                externalId,
                state.Id,
                state.LineageId,
                state.Version,
                state.DisplayName,
                state.Affect);

        private void NotifySocialDialogueTrigger(RimWorldSocialDialogueTrigger trigger)
        {
            var handlers = SocialDialogueTriggerCaptured;
            if (handlers is null) return;
            foreach (Action<RimWorldSocialDialogueTrigger> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(trigger);
                }
                catch (Exception exception)
                {
                    Log.Warning(
                        $"[Dagmay] {DagmayBuildInfo.Version} dialogue trigger subscriber failed safely: "
                        + exception.Message);
                }
            }
        }

        private void NotifyReadOnlyEventEnvelope(ReadOnlyRimWorldEventProjection projection)
        {
            var envelope = projection.Envelope;
            Log.Message(
                $"[Dagmay] 0.3 shadow event envelope; kind={envelope.EventKind}; "
                + $"EventId={envelope.EventId}; actor={envelope.ActorId!.Value}; "
                + $"target={envelope.TargetId!.Value}; outcome={envelope.OutcomeState}; "
                + $"privacy={envelope.PrivacyDomain}; witnesses={projection.WitnessIds.Count}; "
                + $"actionAuthority={envelope.DirectActionAuthority.ToString().ToLowerInvariant()}");

            var handlers = ReadOnlyEventEnvelopeCaptured;
            if (handlers is null) return;
            foreach (Action<ReadOnlyRimWorldEventProjection> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(projection);
                }
                catch (Exception)
                {
                    Log.Warning("[Dagmay] 0.3 shadow event envelope subscriber failed safely.");
                }
            }
        }

        public bool SetEnrollment(string externalId, bool enabled, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!_initialized)
            {
                diagnostic = "Dagmay has not initialized for the current game.";
                return false;
            }

            if (!_writesEnabled)
            {
                diagnostic = "Mosaic identity storage is read-only; enrollment changes are blocked to preserve continuity.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(externalId))
            {
                diagnostic = "The selected colonist has no stable RimWorld identifier.";
                return false;
            }

            externalId = externalId.Trim();
            var pawn = FindPawn(externalId);
            if (enabled)
            {
                if (_identities.TryGetValue(externalId, out var existing))
                {
                    if (existing.Lifecycle != LifecycleState.Active || pawn is null || pawn.Dead || !pawn.IsColonist)
                    {
                        diagnostic = "Only a living colonist can resume active Dagmay processing.";
                        return false;
                    }

                    if (!_pausedExternalIds.Remove(externalId))
                    {
                        diagnostic = existing.DisplayName + " is already enrolled and active.";
                        return true;
                    }

                    _observations[externalId] = PawnObservationCapture.Capture(pawn, SocialPeersFor(pawn));
                    Log.Message($"[Dagmay] {DagmayBuildInfo.Version} resumed processing for {existing.DisplayName}; IndividualId={existing.Id}.");
                    diagnostic = existing.DisplayName + " resumed with the same individual and lineage. Save the game to persist this enrollment setting.";
                    return true;
                }

                if (pawn is null || pawn.Dead || !pawn.IsColonist)
                {
                    diagnostic = "Only a living current colonist can be enrolled.";
                    return false;
                }

                _pausedExternalIds.Remove(externalId);
                if (!SynchronizePawn(pawn, forceEnrollment: true))
                {
                    diagnostic = "Dagmay could not create the selected identity.";
                    return false;
                }

                PersistIfAllowed("explicit Observer enrollment");
                diagnostic = PawnDisplayName(pawn) + " was enrolled. Save the game now to checkpoint the new mapping.";
                return true;
            }

            if (!_identities.TryGetValue(externalId, out var state))
            {
                diagnostic = "This colonist does not yet have a Dagmay individual to pause.";
                return false;
            }

            if (state.Lifecycle != LifecycleState.Active)
            {
                diagnostic = "Archived individuals cannot resume ordinary processing from this control.";
                return false;
            }

            if (_pausedExternalIds.Contains(externalId))
            {
                diagnostic = state.DisplayName + " is already paused; identity and history remain preserved.";
                return true;
            }

            _pausedExternalIds.Add(externalId);
            var removed = _reflectionQueue.RemoveForIndividual(state.Id, _inFlightTaskId);
            if (_inFlightTaskId.HasValue
                && _reflectionQueue.Find(_inFlightTaskId.Value)?.Task.IndividualId == state.Id)
            {
                _invalidatedInFlightTasks.Add(_inFlightTaskId.Value);
            }
            if (removed > 0)
            {
                _reflectionDirty = true;
                PersistReflectionIfDirty("enrollment pause queue cleanup");
            }

            if (pawn is not null && !pawn.Dead)
            {
                _observations[externalId] = PawnObservationCapture.Capture(pawn, SocialPeersFor(pawn));
            }

            Log.Message(
                $"[Dagmay] {DagmayBuildInfo.Version} paused processing for {state.DisplayName}; IndividualId={state.Id}; "
                + $"removedQueuedTasks={removed}. Identity and history were preserved.");
            diagnostic = state.DisplayName + " is paused. Identity and history remain preserved; save the game to persist this setting.";
            return true;
        }

        public OrdinaryMindSnapshot? CreateOrdinaryMindSnapshot(string externalId)
        {
            if (string.IsNullOrWhiteSpace(externalId)) throw new ArgumentException("An external ID is required.", nameof(externalId));
            if (!_identities.TryGetValue(externalId, out var state)) return null;

            var serviceStatus = state.Lifecycle == LifecycleState.Active
                ? (IsProcessingEnabled(externalId) ? "Active" : "Paused")
                : "Archived";
            var identityFacts = state.Seed.Facts
                .Take(12)
                .Select(value => value.Category + ": " + value.Value)
                .ToList();
            var disclosedMemories = _memoryIndex.Recent(state.Id, 20)
                .Concat(_memoryIndex.MostSignificant(state.Id, 20))
                .GroupBy(value => value.Id)
                .Select(group => group.First())
                .Where(OrdinaryDisclosurePolicy.CanShowMemory)
                .OrderByDescending(value => value.EncodedAtUtc)
                .Take(5)
                .Select(value => new OrdinaryMindMemory(
                    value.OccurredAtUtc,
                    value.ConciseDiaryEntry,
                    value.Tier.ToString(),
                    value.Confidence))
                .ToList();

            return new OrdinaryMindSnapshot(
                externalId,
                state.DisplayName,
                serviceStatus,
                state.Lifecycle.ToString(),
                OrdinaryDisclosurePolicy.DescribeCurrentState(state.Affect),
                identityFacts,
                disclosedMemories);
        }

        public ObserverSystemSnapshot CreateObserverSnapshot()
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var pawns = new Dictionary<string, Pawn>(StringComparer.Ordinal);
            foreach (var map in Find.Maps.ToList())
            {
                foreach (var pawn in map.mapPawns.AllPawns.ToList())
                {
                    if (pawn is null || string.IsNullOrWhiteSpace(pawn.ThingID)) continue;
                    if (pawn.IsColonist || _identities.ContainsKey(pawn.ThingID)) pawns[pawn.ThingID] = pawn;
                }
            }

            var externalIds = new HashSet<string>(_identities.Keys, StringComparer.Ordinal);
            foreach (var externalId in pawns.Keys) externalIds.Add(externalId);
            var individuals = externalIds
                .Select(externalId => CreateObserverIndividual(
                    externalId,
                    pawns.TryGetValue(externalId, out var pawn) ? pawn : null))
                .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.ExternalId, StringComparer.Ordinal)
                .ToList();

            return new ObserverSystemSnapshot(
                nowUtc,
                _providerSelection?.Mode ?? "offline",
                _providerSelection?.ProviderId ?? "none",
                _providerSelection?.ModelId ?? string.Empty,
                _providerSelection?.Diagnostic ?? "Reflection runtime is not initialized.",
                DagmayMod.CurrentSettings.ProviderDispatchPaused,
                _writesEnabled,
                _experienceWritesEnabled,
                _reflectionWritesEnabled,
                _reflectionQueue.Count,
                _reflectionAudit.Count,
                _runtimeBudgetPolicy,
                ReflectionUsageSummary.Calculate(nowUtc, _reflectionAudit, _sessionAttemptCount),
                individuals);
        }

        private ObserverIndividual CreateObserverIndividual(string externalId, Pawn? pawn)
        {
            if (!_identities.TryGetValue(externalId, out var state))
            {
                return new ObserverIndividual(
                    externalId,
                    pawn is null ? externalId : PawnDisplayName(pawn),
                    ObserverEnrollmentState.NotEnrolled,
                    string.Empty,
                    string.Empty,
                    0,
                    string.Empty,
                    Dagmay.Core.Affect.AffectVector.Neutral,
                    Array.Empty<ObserverSeedFact>(),
                    Array.Empty<ObserverMemory>(),
                    Array.Empty<ObserverReflection>());
            }

            var enrollment = state.Lifecycle == LifecycleState.Active
                ? (IsProcessingEnabled(externalId) ? ObserverEnrollmentState.Active : ObserverEnrollmentState.Paused)
                : ObserverEnrollmentState.Archived;
            var memories = _memoryIndex.Recent(state.Id, 10)
                .Concat(_memoryIndex.MostSignificant(state.Id, 10))
                .GroupBy(value => value.Id)
                .Select(group => group.First())
                .OrderByDescending(value => value.EncodedAtUtc)
                .Take(10)
                .Select(value => new ObserverMemory(
                    value.Id.ToString(),
                    value.SourcePerceptionIds.Select(source => source.ToString()),
                    value.SourcePerceptionIds
                        .Where(source => _perceptionSources.ContainsKey(source))
                        .Select(source => _perceptionSources[source].ToString()),
                    value.OccurredAtUtc,
                    value.EncodedAtUtc,
                    value.ConciseDiaryEntry,
                    value.Appraisal,
                    value.AffectAtEncoding,
                    value.Tier.ToString(),
                    value.Privacy.ToString(),
                    value.Importance,
                    value.EmotionalWeight,
                    value.Confidence,
                    value.Accessibility,
                    value.PeopleInvolved.Select(person => person.ToString())))
                .ToList();
            var reflections = new List<ObserverReflection>();
            var committedRequests = new HashSet<RequestId>(_reflectionAudit
                .Where(value => value.Status == ReflectionAuditStatus.Committed)
                .Select(value => value.RequestId));
            foreach (var audit in _reflectionAudit
                .Where(value => value.IndividualId == state.Id
                    && value.Status == ReflectionAuditStatus.PendingCommit
                    && committedRequests.Contains(value.RequestId)
                    && !string.IsNullOrWhiteSpace(value.StructuredPayload))
                .OrderByDescending(value => value.OccurredAtUtc))
            {
                if (reflections.Count >= 5) break;
                try
                {
                    var proposal = ReflectionProposalJson.Parse(audit.StructuredPayload);
                    reflections.Add(new ObserverReflection(
                        proposal.RequestId.ToString(),
                        audit.OccurredAtUtc,
                        audit.Provider,
                        audit.Model,
                        proposal.Confidence,
                        proposal.Interpretation,
                        proposal.AutobiographicalReflection,
                        proposal.DecisionSummary,
                        proposal.EvidenceEventIds.Count));
                }
                catch (Exception)
                {
                    // Observer rendering never promotes or repairs malformed diagnostic payloads.
                }
            }

            return new ObserverIndividual(
                externalId,
                state.DisplayName,
                enrollment,
                state.Id.ToString(),
                state.LineageId.ToString(),
                state.Version,
                state.Lifecycle.ToString(),
                state.Affect,
                state.Seed.Facts.Take(20).Select(value => new ObserverSeedFact(
                    value.Category.ToString(),
                    value.Key,
                    value.Value,
                    value.Confidence)),
                memories,
                reflections);
        }

        private bool IsProcessingEnabled(string externalId)
        {
            return !_pausedExternalIds.Contains(externalId);
        }

        private bool IsProcessingEnabled(IndividualId individualId)
        {
            return TryFindIdentity(individualId, out var externalId, out _)
                && IsProcessingEnabled(externalId);
        }

        private static Pawn? FindPawn(string externalId)
        {
            foreach (var map in Find.Maps.ToList())
            {
                foreach (var pawn in map.mapPawns.AllPawns.ToList())
                {
                    if (pawn is not null && string.Equals(pawn.ThingID, externalId, StringComparison.Ordinal)) return pawn;
                }
            }

            return null;
        }

        private void RefreshRuntimeSettings()
        {
            var settings = DagmayMod.CurrentSettings;
            var fingerprint = settings.MaximumRequestsPerSession + ":"
                + settings.MaximumRequestsPerHour + ":"
                + settings.MaximumEstimatedTokensPerDay + ":"
                + settings.HeartbeatMinutes;
            if (string.Equals(fingerprint, _settingsFingerprint, StringComparison.Ordinal)) return;

            _runtimeBudgetPolicy = settings.CreateBudgetPolicy();
            _reflectionBudget = new ReflectionBudgetGate(_runtimeBudgetPolicy);
            _settingsFingerprint = fingerprint;
            _sessionBudgetNoticeLogged = false;
            if (_nextReflectionHeartbeatUtc != DateTimeOffset.MinValue)
            {
                _nextReflectionHeartbeatUtc = DateTimeOffset.UtcNow.Add(_runtimeBudgetPolicy.SchedulerHeartbeat);
            }
        }

        private static EnvironmentEvent CreateEvent(
            string externalId,
            IndividualState state,
            string kind,
            IDictionary<string, string> payload,
            IEnumerable<IndividualId>? additionalSubjects = null)
        {
            var now = DateTimeOffset.UtcNow;
            var gameTick = Find.TickManager?.TicksGame;
            var facts = new Dictionary<string, string>(payload, StringComparer.Ordinal)
            {
                ["pawn_external_id"] = externalId,
                ["display_name_at_event"] = state.DisplayName
            };
            var detail = facts.ContainsKey("condition") ? facts["condition"]
                : facts.ContainsKey("need") ? facts["need"]
                : facts.ContainsKey("skill") ? facts["skill"]
                : facts.ContainsKey("target_external_id") ? facts["target_external_id"]
                : "event";
            var deduplicationKey = kind + ":" + externalId + ":" + (gameTick?.ToString() ?? "no-tick") + ":" + detail;
            var subjects = new List<IndividualId> { state.Id };
            if (additionalSubjects is not null)
            {
                foreach (var subject in additionalSubjects)
                {
                    if (!subjects.Contains(subject)) subjects.Add(subject);
                }
            }

            return new EnvironmentEvent(
                EventId.New(),
                deduplicationKey,
                kind,
                "rimworld",
                now,
                now,
                gameTick,
                $"Dagmay.RimWorld {DagmayBuildInfo.Version} polling observer",
                facts,
                subjects);
        }

        private void PersistIfAllowed(string reason)
        {
            if (!_writesEnabled || !_initialized) return;
            try
            {
                var snapshot = CreateIdentitySnapshot(null, null);
                _archive.Save(GetArchivePath(), snapshot);
                _generation = snapshot.Generation;
            }
            catch (Exception exception)
            {
                DisableWrites($"Identity persistence failed during {reason}: {exception.Message}");
            }
        }

        private bool TryPersistIdentityReplacement(
            string externalId,
            IndividualState expectedCurrent,
            IndividualState replacement,
            string reason)
        {
            if (!_writesEnabled || !_initialized) return false;
            if (!_identities.TryGetValue(externalId, out var current)
                || current.Id != expectedCurrent.Id
                || current.Version != expectedCurrent.Version)
            {
                Log.Error($"[Dagmay] {DagmayBuildInfo.Version} refused an identity replacement because its expected version is no longer current.");
                return false;
            }

            try
            {
                var snapshot = CreateIdentitySnapshot(externalId, replacement);
                _archive.Save(GetArchivePath(), snapshot);
                _generation = snapshot.Generation;
                _identities[externalId] = replacement;
                return true;
            }
            catch (Exception exception)
            {
                DisableWrites($"Identity persistence failed during {reason}: {exception.Message}");
                return false;
            }
        }

        private IdentityArchiveSnapshot CreateIdentitySnapshot(
            string? replacementExternalId,
            IndividualState? replacement)
        {
            var records = new List<PersistedIdentityRecord>(_identities.Count);
            foreach (var pair in _identities)
            {
                var state = replacement is not null
                    && string.Equals(pair.Key, replacementExternalId, StringComparison.Ordinal)
                    ? replacement
                    : pair.Value;
                records.Add(new PersistedIdentityRecord(pair.Key, state));
            }

            return new IdentityArchiveSnapshot(
                Guid.Parse(_storeId),
                _generation + 1,
                DateTimeOffset.UtcNow,
                records);
        }

        private bool PersistReflectionIfDirty(string reason)
        {
            if (!_reflectionDirty) return true;
            if (!_reflectionWritesEnabled || !_initialized) return false;
            if (!_reflectionCheckpointGate.AllowsSidecarPersistence(Scribe.mode == LoadSaveMode.Saving))
            {
                return false;
            }
            try
            {
                var snapshot = new ReflectionStoreSnapshot(
                    Guid.Parse(_storeId),
                    _reflectionGeneration + 1,
                    DateTimeOffset.UtcNow,
                    _reflectionQueue.Snapshot(),
                    _reflectionAudit);
                _reflectionStore.Save(GetReflectionStorePath(), snapshot);
                _reflectionGeneration = snapshot.Generation;
                _reflectionDirty = false;
                _reflectionStoreWasNew = false;
                return true;
            }
            catch (Exception exception)
            {
                DisableReflectionWrites($"Reflection persistence failed during {reason}: {exception.Message}");
                return false;
            }
        }

        private void BuildManifest()
        {
            _manifestExternalIds.Clear();
            _manifestIndividualIds.Clear();
            foreach (var pair in _identities)
            {
                _manifestExternalIds.Add(pair.Key);
                _manifestIndividualIds.Add(pair.Value.Id.ToString());
            }
        }

        private static bool IsSocialCertificationKind(string kind)
        {
            return string.Equals(
                    kind,
                    "rimworld.social.opinion_changed",
                    StringComparison.Ordinal)
                || string.Equals(
                    kind,
                    "rimworld.relationship.direct_changed",
                    StringComparison.Ordinal);
        }

        private void WriteSocialPathCertificationReport(bool postLoadAudit)
        {
            try
            {
                var summary = SocialPathCertificationReport.Build(
                    _eventLedger.Snapshot(),
                    _memoryIndex.Snapshot(),
                    _perceptionSources,
                    postLoadAudit,
                    _writesEnabled,
                    _experienceWritesEnabled,
                    _reflectionWritesEnabled);
                SocialPathCertificationReport.Write(
                    GetSocialCertificationPath(),
                    _storeId,
                    DagmayBuildInfo.Version,
                    summary,
                    _experiencePosition,
                    _reflectionQueue.Count);

                if (summary.Passed)
                {
                    Log.Message(
                        $"[Dagmay] {DagmayBuildInfo.Version} SOCIAL PATH CERTIFICATION PASS; "
                        + $"events={summary.SocialEvents}; memories={summary.SocialMemories}; "
                        + $"reflectionEligible={summary.ReflectionEligibleEvents}; postLoad={summary.PostLoadAudit}.");
                }
                else if (summary.SocialEvents > 0)
                {
                    Log.Message(
                        $"[Dagmay] {DagmayBuildInfo.Version} social path certification partial; "
                        + $"events={summary.SocialEvents}; memories={summary.SocialMemories}; "
                        + $"linked={summary.LinkedSocialEvents}; privacy={summary.RelationshipSensitiveMemories}; "
                        + $"counterpartProvenance={summary.CounterpartProvenanceMemories}; "
                        + $"reflectionEligible={summary.ReflectionEligibleEvents}; postLoad={summary.PostLoadAudit}.");
                }
            }
            catch (Exception exception)
            {
                Log.Warning(
                    $"[Dagmay] {DagmayBuildInfo.Version} social certification diagnostics failed safely: "
                    + exception.Message);
            }
        }

        private string GetSocialCertificationPath()
        {
            var directory = Path.Combine(GenFilePaths.ConfigFolderPath, "Dagmay", "Diagnostics");
            return Path.Combine(directory, SaveManifestSafetyPolicy.SafeStoreFileStem(_storeId) + "-social-certification.txt");
        }

        private string GetArchivePath()
        {
            var directory = Path.Combine(GenFilePaths.ConfigFolderPath, "Dagmay", "Identities");
            return Path.Combine(directory, SaveManifestSafetyPolicy.SafeStoreFileStem(_storeId) + ".dagmay");
        }

        private string GetExperienceJournalPath()
        {
            var directory = Path.Combine(GenFilePaths.ConfigFolderPath, "Dagmay", "Experiences");
            return Path.Combine(directory, SaveManifestSafetyPolicy.SafeStoreFileStem(_storeId) + ".journal");
        }

        private string GetReflectionStorePath()
        {
            var directory = Path.Combine(GenFilePaths.ConfigFolderPath, "Dagmay", "Reflections");
            return Path.Combine(directory, SaveManifestSafetyPolicy.SafeStoreFileStem(_storeId) + ".reflection");
        }

        private string GetDialogueOutboxPath()
        {
            var directory = Path.Combine(GenFilePaths.ConfigFolderPath, "Dagmay", "Dialogue");
            return Path.Combine(
                directory,
                SaveManifestSafetyPolicy.SafeStoreFileStem(_storeId) + ".dialogue-outbox");
        }

        private static string PawnDisplayName(Pawn pawn)
        {
            var value = pawn.Name?.ToStringShort;
            if (value is null || string.IsNullOrWhiteSpace(value)) value = pawn.LabelShort;
            return value is null || string.IsNullOrWhiteSpace(value)
                ? "Unnamed pawn"
                : value;
        }

        private void DisableWrites(string diagnostic)
        {
            _writesEnabled = false;
            _experienceWritesEnabled = false;
            _reflectionWritesEnabled = false;
            Log.Error($"[Dagmay] {DagmayBuildInfo.Version} identity storage entered read-only safety mode. " + diagnostic);
        }

        private void DisableExperienceWrites(string diagnostic)
        {
            _experienceWritesEnabled = false;
            Log.Error($"[Dagmay] {DagmayBuildInfo.Version} experience storage entered read-only safety mode. " + diagnostic);
        }

        private void DisableReflectionWrites(string diagnostic)
        {
            _reflectionWritesEnabled = false;
            Log.Error($"[Dagmay] {DagmayBuildInfo.Version} reflection storage entered read-only safety mode. " + diagnostic);
        }

        private void LogStorageSafetyPauseOnce()
        {
            if (_storageSafetyPauseNoticeLogged || _reflectionQueue.Count == 0) return;

            Log.Warning(
                $"[Dagmay] {DagmayBuildInfo.Version} reflection processing is paused because canonical storage is read-only; "
                + "queued work remains durable and no provider result will be applied until explicit recovery restores storage health.");
            _storageSafetyPauseNoticeLogged = true;
        }

        private void CompactRoutineReflectionBacklog()
        {
            if (!ReflectionStorageSafetyPolicy.AllowsQueueMutation(_experienceWritesEnabled, _reflectionWritesEnabled)
                || _reflectionQueue.Count < 1)
            {
                return;
            }

            var events = _eventLedger.Snapshot()
                .ToDictionary(entry => entry.Value.Id, entry => entry.Value);
            var original = _reflectionQueue.Snapshot();
            var preserved = new List<PendingReflectionTask>();
            var grouped = new Dictionary<string, List<PendingReflectionTask>>(StringComparer.Ordinal);
            var suppressed = 0;

            foreach (var pending in original)
            {
                if (pending.AttemptCount != 0)
                {
                    preserved.Add(pending);
                    continue;
                }

                var sourceEvents = pending.Task.SourceEventIds
                    .Where(events.ContainsKey)
                    .Select(id => events[id])
                    .ToList();
                if (sourceEvents.Count != pending.Task.SourceEventIds.Count || sourceEvents.Count == 0)
                {
                    preserved.Add(pending);
                    continue;
                }

                var plans = sourceEvents
                    .Select(value => ReflectionEventTaskPolicy.Plan(pending.Task.IndividualId, value))
                    .ToList();
                if (plans.Any(value => !value.Compactable)
                    || plans.Select(value => value.CoalescingKey).Distinct(StringComparer.Ordinal).Count() != 1)
                {
                    preserved.Add(pending);
                    continue;
                }

                var key = plans[0].CoalescingKey;
                if (!grouped.TryGetValue(key, out var bucket))
                {
                    bucket = new List<PendingReflectionTask>();
                    grouped.Add(key, bucket);
                }
                bucket.Add(pending);
            }

            foreach (var pair in grouped)
            {
                var bucket = pair.Value;
                var first = bucket
                    .OrderBy(value => value.Task.CreatedAtUtc)
                    .First();
                var sourceIds = bucket
                    .SelectMany(value => value.Task.SourceEventIds)
                    .Distinct()
                    .Take(100)
                    .ToList();
                var sourcePlans = sourceIds
                    .Where(events.ContainsKey)
                    .Select(id => ReflectionEventTaskPolicy.Plan(first.Task.IndividualId, events[id]))
                    .ToList();
                var minimumEvidenceCount = sourcePlans.Count == 0
                    ? 1
                    : sourcePlans.Max(value => value.MinimumEvidenceCount);

                if (sourceIds.Count < minimumEvidenceCount)
                {
                    suppressed += bucket.Count;
                    continue;
                }

                var priority = sourcePlans.Count == 0
                    ? first.Task.Priority
                    : (ReflectionPriority)sourcePlans.Max(value => (int)value.Priority);
                preserved.Add(new PendingReflectionTask(
                    new ReflectionTask(
                        first.Task.Id,
                        first.Task.IndividualId,
                        ModelTaskKind.InterpretMeaningfulEvent,
                        priority,
                        bucket.Min(value => value.Task.CreatedAtUtc),
                        pair.Key,
                        sourceIds,
                        bucket.Max(value => value.Task.EstimatedTokens)),
                    0,
                    bucket.Min(value => value.NextAttemptAtUtc),
                    string.Empty));
            }

            if (grouped.Count == 0 && suppressed == 0) return;

            _reflectionQueue = new PersistentReflectionQueue(
                _runtimeBudgetPolicy.MaximumQueueSize,
                preserved);
            _reflectionDirty = true;
            Log.Message(
                $"[Dagmay] {DagmayBuildInfo.Version} applied salience admission to unattempted routine reflection backlog; "
                + $"before={original.Count}; after={preserved.Count}; suppressedTasks={suppressed}; "
                + $"retainedEvidence={preserved.Sum(value => value.Task.SourceEventIds.Count)}.");
        }

        private string QueueCompositionSummary()
        {
            var tasks = _reflectionQueue.Snapshot();
            var critical = tasks.Count(value => value.Task.Priority == ReflectionPriority.CriticalLifecycle);
            var meaningful = tasks.Count(value => value.Task.Priority == ReflectionPriority.MeaningfulEvent);
            var consolidation = tasks.Count(value => value.Task.Priority == ReflectionPriority.Consolidation);
            var background = tasks.Count(value => value.Task.Priority == ReflectionPriority.Background);
            var individuals = tasks.Select(value => value.Task.IndividualId).Distinct().Count();
            return $"QueueByPriority=CriticalLifecycle:{critical},MeaningfulEvent:{meaningful},Consolidation:{consolidation},Background:{background}; "
                + $"QueueIndividuals={individuals}; AdmissionDeferredSession={_reflectionAdmissionDeferredSession}";
        }

        private void LogStatus(string context)
        {
            Log.Message(
                $"[Dagmay] {DagmayBuildInfo.Version} observer service ready after {context}. "
                + $"Individuals={_identities.Count}; Generation={_generation}; "
                + $"Events={_experiencePosition}; Memories={_memoryIndex.Count}; "
                + $"ReflectionMode={_providerSelection?.Mode ?? "offline"}; "
                + $"ReflectionQueue={_reflectionQueue.Count}; {QueueCompositionSummary()}; ReflectionAudit={_reflectionAudit.Count}; "
                + $"IdentityStorage={(_writesEnabled ? "healthy" : "READ-ONLY")}; "
                + $"ExperienceStorage={(_experienceWritesEnabled ? "healthy" : "READ-ONLY")}; "
                + $"ReflectionStorage={(_reflectionWritesEnabled ? "healthy" : "READ-ONLY")}; "
                + $"IdentityPath={GetArchivePath()}; ExperiencePath={GetExperienceJournalPath()}; "
                + $"ReflectionPath={GetReflectionStorePath()}");

            foreach (var pair in _identities)
            {
                var state = pair.Value;
                Log.Message(
                    $"[Dagmay] {DagmayBuildInfo.Version} identity inventory: pawn={pair.Key}; name={state.DisplayName}; "
                    + $"IndividualId={state.Id}; LineageId={state.LineageId}; "
                    + $"Version={state.Version}; Lifecycle={state.Lifecycle}.");
            }
        }

        private sealed class CompletedReflectionCall
        {
            public CompletedReflectionCall(
                PendingReflectionTask pending,
                ModelRequest request,
                int attemptNumber,
                ModelResult result)
            {
                Pending = pending;
                Request = request;
                AttemptNumber = attemptNumber;
                Result = result;
            }

            public PendingReflectionTask Pending { get; }
            public ModelRequest Request { get; }
            public int AttemptNumber { get; }
            public ModelResult Result { get; }
        }
    }
}
