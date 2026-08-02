using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dagmay.Core.Observer;
using Dagmay.Core.Views;
using Dagmay.RimWorld.Bootstrap;
using Dagmay.RimWorld.Persistence;
using Dagmay.RimWorld.Settings;
using UnityEngine;
using Verse;

namespace Dagmay.RimWorld.Observer
{
    internal sealed class DagmayObserverSettingsUi
    {
        private const float LineHeight = 26f;
        private Vector2 _scrollPosition = Vector2.zero;
        private string _selectedExternalId = string.Empty;
        private string _selectedMindExternalId = string.Empty;
        private string _lastAction = string.Empty;
        private DagmayIdentityGameComponent? _snapshotOwner;
        private ObserverSystemSnapshot? _cachedSnapshot;
        private DateTimeOffset _nextSnapshotRefreshUtc = DateTimeOffset.MinValue;
        private bool _observerUnlockPending;
        private bool _experienceRecoveryConfirmPending;
        private bool _exactExperienceRestoreConfirmPending;

        public void Draw(Rect inRect, DagmayModSettings settings)
        {
            var component = DagmayIdentityGameComponent.Current;
            ObserverSystemSnapshot? snapshot = null;
            if (component is not null)
            {
                if (!ReferenceEquals(component, _snapshotOwner)
                    || DateTimeOffset.UtcNow >= _nextSnapshotRefreshUtc)
                {
                    try
                    {
                        _cachedSnapshot = component.CreateObserverSnapshot();
                        _snapshotOwner = component;
                        _nextSnapshotRefreshUtc = DateTimeOffset.UtcNow.AddSeconds(1);
                    }
                    catch (Exception exception)
                    {
                        _cachedSnapshot = null;
                        _nextSnapshotRefreshUtc = DateTimeOffset.UtcNow.AddSeconds(1);
                        _lastAction = "Observer projection failed safely: " + exception.Message;
                    }
                }

                snapshot = _cachedSnapshot;
            }
            else
            {
                _snapshotOwner = null;
                _cachedSnapshot = null;
            }

            var viewWidth = Math.Max(300f, inRect.width - 18f);
            var selectedForHeight = snapshot?.Individuals.FirstOrDefault(value =>
                string.Equals(value.ExternalId, _selectedExternalId, StringComparison.Ordinal)
                && value.HasIdentity);
            var viewHeight = 1100f + ((snapshot?.Individuals.Count ?? 0) * 36f);
            if (component?.ExperienceRecoveryAvailable == true) viewHeight += 330f;
            if (component is not null && _selectedMindExternalId.Length > 0)
            {
                var mindForHeight = component.CreateOrdinaryMindSnapshot(_selectedMindExternalId);
                if (mindForHeight is not null)
                {
                    viewHeight += 280f
                        + (mindForHeight.IdentityFacts.Count * 34f)
                        + (mindForHeight.DisclosedMemories.Count * 82f);
                }
            }

            if (settings.RestrictedObserverEnabled && selectedForHeight is not null)
            {
                viewHeight += 350f
                    + (selectedForHeight.SeedFacts.Count * 46f)
                    + (selectedForHeight.Memories.Count * 200f)
                    + (selectedForHeight.Reflections.Count * 154f);
            }

            var viewRect = new Rect(0f, 0f, viewWidth, Math.Max(inRect.height, viewHeight));
            Widgets.BeginScrollView(inRect, ref _scrollPosition, viewRect);
            var y = 0f;

            Label(
                ref y,
                viewWidth,
                $"Dagmay {DagmayBuildInfo.Version} — Restricted Observer and Runtime Controls",
                34f);
            Label(
                ref y,
                viewWidth,
                "This is an out-of-world debugging surface. It may reveal private memories and reflections that ordinary in-world interaction must not expose. It never displays hidden model chain-of-thought.",
                58f);

            if (settings.RestrictedObserverEnabled)
            {
                var restricted = true;
                Widgets.CheckboxLabeled(
                    new Rect(0f, y, viewWidth, LineHeight),
                    "RESTRICTED OBSERVER ENABLED — uncheck to lock private Dagmay state",
                    ref restricted);
                y += LineHeight + 8f;
                if (!restricted)
                {
                    settings.SetRestrictedObserverEnabled(false);
                    _observerUnlockPending = false;
                    DagmayMod.SaveSettings();
                    _lastAction = "Restricted Observer Mode locked; private details are hidden.";
                }
            }
            else if (!_observerUnlockPending)
            {
                if (Widgets.ButtonText(
                    new Rect(0f, y, Math.Min(430f, viewWidth), LineHeight + 4f),
                    "Unlock Restricted Observer Mode…"))
                {
                    _observerUnlockPending = true;
                    _lastAction = "Confirmation required before private state is revealed.";
                }

                y += LineHeight + 12f;
            }
            else
            {
                Label(
                    ref y,
                    viewWidth,
                    "Confirmation: this out-of-world debugging view can reveal private memories and internal reflections that the individual has not disclosed. It is not ordinary in-world knowledge.",
                    66f);
                var confirmationWidth = Math.Min(300f, (viewWidth - 8f) / 2f);
                if (Widgets.ButtonText(new Rect(0f, y, confirmationWidth, LineHeight + 4f), "Confirm private inspection"))
                {
                    settings.SetRestrictedObserverEnabled(true);
                    _observerUnlockPending = false;
                    DagmayMod.SaveSettings();
                    _lastAction = "Restricted Observer Mode enabled after explicit confirmation.";
                }

                if (Widgets.ButtonText(
                    new Rect(confirmationWidth + 8f, y, confirmationWidth, LineHeight + 4f),
                    "Cancel"))
                {
                    _observerUnlockPending = false;
                    _lastAction = "Restricted Observer Mode remains locked.";
                }

                y += LineHeight + 12f;
            }

            Section(ref y, viewWidth, "Provider safety and budgets");
            Label(
                ref y,
                viewWidth,
                "If Google mode is configured, unpausing authorizes Dagmay to send bounded fictional colony context to that external provider. Keep this paused for offline inspection; the API key is never shown or stored here.",
                62f);
            var paused = settings.ProviderDispatchPaused;
            Widgets.CheckboxLabeled(
                new Rect(0f, y, viewWidth, LineHeight),
                "Pause provider dispatch (event recording and durable queue continue)",
                ref paused);
            y += LineHeight + 4f;
            if (paused != settings.ProviderDispatchPaused)
            {
                settings.SetProviderDispatchPaused(paused);
                InvalidateSnapshot();
                DagmayMod.SaveSettings();
                _lastAction = paused
                    ? "Provider dispatch paused immediately."
                    : "Provider dispatch enabled within the configured budgets.";
            }

            var autoEnroll = settings.AutoEnrollNewColonists;
            Widgets.CheckboxLabeled(
                new Rect(0f, y, viewWidth, LineHeight),
                "Automatically enroll future colonists (off is safer for selective testing)",
                ref autoEnroll);
            y += LineHeight + 8f;
            if (autoEnroll != settings.AutoEnrollNewColonists)
            {
                settings.SetAutoEnrollNewColonists(autoEnroll);
                DagmayMod.SaveSettings();
                _lastAction = autoEnroll
                    ? "Future living colonists will be enrolled automatically."
                    : "Future colonists require explicit enrollment.";
            }

            NumberControl(
                ref y,
                viewWidth,
                "Maximum requests per game session",
                settings.MaximumRequestsPerSession,
                () =>
                {
                    settings.AdjustMaximumRequestsPerSession(-1);
                    InvalidateSnapshot();
                },
                () =>
                {
                    settings.AdjustMaximumRequestsPerSession(1);
                    InvalidateSnapshot();
                });
            NumberControl(
                ref y,
                viewWidth,
                "Maximum requests per rolling hour",
                settings.MaximumRequestsPerHour,
                () =>
                {
                    settings.AdjustMaximumRequestsPerHour(-1);
                    InvalidateSnapshot();
                },
                () =>
                {
                    settings.AdjustMaximumRequestsPerHour(1);
                    InvalidateSnapshot();
                });
            NumberControl(
                ref y,
                viewWidth,
                "Maximum estimated tokens per UTC day",
                settings.MaximumEstimatedTokensPerDay,
                () =>
                {
                    settings.AdjustMaximumEstimatedTokensPerDay(-5_000);
                    InvalidateSnapshot();
                },
                () =>
                {
                    settings.AdjustMaximumEstimatedTokensPerDay(5_000);
                    InvalidateSnapshot();
                });
            NumberControl(
                ref y,
                viewWidth,
                "Background reflection heartbeat (real minutes)",
                settings.HeartbeatMinutes,
                () =>
                {
                    settings.AdjustHeartbeatMinutes(-1);
                    InvalidateSnapshot();
                },
                () =>
                {
                    settings.AdjustHeartbeatMinutes(1);
                    InvalidateSnapshot();
                });
            Label(
                ref y,
                viewWidth,
                "The session limit is a hard transport stop until RimWorld restarts or you raise the limit. Pending work remains queued. Increasing these limits may consume additional provider quota.",
                54f);

            if (snapshot is null)
            {
                Section(ref y, viewWidth, "Current game");
                Label(ref y, viewWidth, "Load a colony to inspect system health, enrollment, memories, and reflections.", 42f);
                Finish(inRect, ref y);
                return;
            }

            if (component is null)
            {
                Section(ref y, viewWidth, "Current game");
                Label(ref y, viewWidth, "The loaded game has no active Dagmay component to inspect.", 42f);
                Finish(inRect, ref y);
                return;
            }

            Section(ref y, viewWidth, "System health");
            Label(
                ref y,
                viewWidth,
                "Mode: " + snapshot.ReflectionMode
                + " | Provider: " + snapshot.Provider
                + " | Model: " + (snapshot.Model.Length == 0 ? "none" : snapshot.Model)
                + " | Dispatch: " + (snapshot.ProviderDispatchPaused ? "PAUSED" : "enabled"),
                42f);
            Label(
                ref y,
                viewWidth,
                "Storage — Identity: " + Health(snapshot.IdentityStorageHealthy)
                + " | Experience: " + Health(snapshot.ExperienceStorageHealthy)
                + " | Reflection: " + Health(snapshot.ReflectionStorageHealthy)
                + " | Queue: " + snapshot.PendingReflectionCount.ToString(CultureInfo.InvariantCulture)
                + " | Audit: " + snapshot.AuditRecordCount.ToString(CultureInfo.InvariantCulture),
                42f);
            Label(
                ref y,
                viewWidth,
                "Usage — Session: " + snapshot.Usage.AttemptsThisSession + "/" + snapshot.Budget.MaximumRequestsPerSession
                + " | Last hour: " + snapshot.Usage.AttemptsLastHour + "/" + snapshot.Budget.MaximumRequestsPerHour
                + " | Today attempts: " + snapshot.Usage.AttemptsToday
                + " | Reported tokens: " + snapshot.Usage.ReportedTokensToday
                + " | Estimated tokens: " + snapshot.Usage.EstimatedTokensToday + "/" + snapshot.Budget.MaximumTokensPerDay,
                54f);
            Label(
                ref y,
                viewWidth,
                "Today — Successful requests: " + snapshot.Usage.SuccessfulRequestsToday
                + " | Provider failures: " + snapshot.Usage.FailedAttemptsToday
                + " | Last outcome: " + (snapshot.Usage.LastOutcome.Length == 0 ? "none" : snapshot.Usage.LastOutcome)
                + (snapshot.Usage.LastErrorCode.Length == 0 ? string.Empty : " (" + snapshot.Usage.LastErrorCode + ")"),
                42f);
            Label(ref y, viewWidth, snapshot.ProviderDiagnostic, 48f);

            if (component.ExperienceRecoveryAvailable)
            {
                Section(ref y, viewWidth, "Continuity recovery required");
                Label(ref y, viewWidth, component.ExperienceRecoveryDiagnostic, 86f);
                if (!settings.RestrictedObserverEnabled)
                {
                    Label(ref y, viewWidth,
                        "Experience writes remain paused. Unlock Restricted Observer Mode to access explicit administrative recovery controls.", 54f);
                }
                else if (_exactExperienceRestoreConfirmPending)
                {
                    Label(ref y, viewWidth,
                        "Confirmation: make the loaded RimWorld save checkpoint authoritative. Verified post-checkpoint history will not be loaded into the rolled-back world; it will be preserved byte-for-byte as a non-canonical recovery artifact.", 78f);
                    var width = Math.Min(330f, (viewWidth - 8f) / 2f);
                    if (Widgets.ButtonText(new Rect(0f, y, width, LineHeight + 4f), "Confirm exact-save restore"))
                    {
                        _lastAction = component.RestoreExactRimWorldExperienceCheckpoint(out var result)
                            ? result
                            : "Recovery was not applied: " + result;
                        _exactExperienceRestoreConfirmPending = false;
                        InvalidateSnapshot();
                    }
                    if (Widgets.ButtonText(new Rect(width + 8f, y, width, LineHeight + 4f), "Cancel"))
                    {
                        _exactExperienceRestoreConfirmPending = false;
                        _lastAction = "Experience recovery cancelled; read-only safety mode remains active.";
                    }
                    y += LineHeight + 12f;
                }
                else if (_experienceRecoveryConfirmPending)
                {
                    Label(ref y, viewWidth,
                        "Confirmation: adopt the verified append-only external Mosaic experience history. This imports memories of events that may have occurred after the loaded RimWorld save point. Save under a new name immediately afterward.", 78f);
                    var width = Math.Min(330f, (viewWidth - 8f) / 2f);
                    if (Widgets.ButtonText(new Rect(0f, y, width, LineHeight + 4f), "Confirm future-head adoption"))
                    {
                        _lastAction = component.AdoptVerifiedExternalExperienceHead(out var result)
                            ? result
                            : "Recovery was not applied: " + result;
                        _experienceRecoveryConfirmPending = false;
                        InvalidateSnapshot();
                    }
                    if (Widgets.ButtonText(new Rect(width + 8f, y, width, LineHeight + 4f), "Cancel"))
                    {
                        _experienceRecoveryConfirmPending = false;
                        _lastAction = "Experience recovery cancelled; read-only safety mode remains active.";
                    }
                    y += LineHeight + 12f;
                }
                else
                {
                    if (Widgets.ButtonText(new Rect(0f, y, Math.Min(620f, viewWidth), LineHeight + 4f),
                        "Restore exact loaded-save checkpoint (recommended)…"))
                    {
                        _exactExperienceRestoreConfirmPending = true;
                        _lastAction = "Confirmation required: post-checkpoint history will remain preserved but non-canonical.";
                    }
                    y += LineHeight + 8f;
                    if (Widgets.ButtonText(new Rect(0f, y, Math.Min(620f, viewWidth), LineHeight + 4f),
                        "Adopt verified future experience head…"))
                    {
                        _experienceRecoveryConfirmPending = true;
                        _lastAction = "Confirmation required: this imports newer Mosaic history into the loaded older world state.";
                    }
                    y += LineHeight + 10f;
                }
            }
            else
            {
                _experienceRecoveryConfirmPending = false;
                _exactExperienceRestoreConfirmPending = false;
            }
            Section(ref y, viewWidth, "Colonist enrollment");
            Label(
                ref y,
                viewWidth,
                "Pausing stops ordinary observation and future reflection for that individual while preserving identity, lineage, memories, and lifecycle history. Death archival remains continuity-critical.",
                52f);

            foreach (var individual in snapshot.Individuals)
            {
                var row = new Rect(0f, y, viewWidth, 32f);
                var actionWidth = 92f;
                var mindWidth = 82f;
                var inspectWidth = 82f;
                var textWidth = viewWidth - actionWidth - mindWidth - inspectWidth - 24f;
                Widgets.Label(
                    new Rect(row.x, row.y, textWidth, row.height),
                    individual.DisplayName + " — " + individual.Enrollment);

                if (individual.Enrollment == ObserverEnrollmentState.NotEnrolled)
                {
                    if (Widgets.ButtonText(new Rect(textWidth + 8f, row.y, actionWidth, row.height), "Enroll"))
                    {
                        ApplyEnrollment(component, individual.ExternalId, true);
                    }
                }
                else if (individual.Enrollment == ObserverEnrollmentState.Active)
                {
                    if (Widgets.ButtonText(new Rect(textWidth + 8f, row.y, actionWidth, row.height), "Pause"))
                    {
                        ApplyEnrollment(component, individual.ExternalId, false);
                    }
                }
                else if (individual.Enrollment == ObserverEnrollmentState.Paused)
                {
                    if (Widgets.ButtonText(new Rect(textWidth + 8f, row.y, actionWidth, row.height), "Resume"))
                    {
                        ApplyEnrollment(component, individual.ExternalId, true);
                    }
                }

                if (individual.HasIdentity)
                {
                    if (Widgets.ButtonText(
                        new Rect(textWidth + actionWidth + 8f, row.y, mindWidth, row.height),
                        "Mind"))
                    {
                        _selectedMindExternalId = individual.ExternalId;
                    }
                }

                if (individual.HasIdentity && settings.RestrictedObserverEnabled)
                {
                    if (Widgets.ButtonText(
                        new Rect(textWidth + actionWidth + mindWidth + 16f, row.y, inspectWidth, row.height),
                        "Inspect"))
                    {
                        _selectedExternalId = individual.ExternalId;
                    }
                }

                y += 36f;
            }

            if (_lastAction.Length > 0)
            {
                Label(ref y, viewWidth, "Last action: " + _lastAction, 48f);
            }

            Section(ref y, viewWidth, "Ordinary Mind view");
            Label(
                ref y,
                viewWidth,
                "This projection is intentionally disclosure-filtered. It shows coarse current state, grounded identity facts, and only memories explicitly classified as shareable. It never exposes raw affect values, lineage IDs, provider diagnostics, private memories, or hidden reasoning.",
                72f);
            OrdinaryMindSnapshot? selectedMind = null;
            if (_selectedMindExternalId.Length > 0)
            {
                selectedMind = component.CreateOrdinaryMindSnapshot(_selectedMindExternalId);
            }

            if (selectedMind is null)
            {
                Label(ref y, viewWidth, "Choose Mind beside an enrolled individual.", 38f);
            }
            else
            {
                DrawOrdinaryMind(ref y, viewWidth, selectedMind);
            }

            Section(ref y, viewWidth, "Private individual inspection");
            if (!settings.RestrictedObserverEnabled)
            {
                Label(
                    ref y,
                    viewWidth,
                    "Restricted Observer Mode is locked. Enable it at the top of this page to inspect private memory and reflection records.",
                    52f);
                Finish(inRect, ref y);
                return;
            }

            var selected = snapshot.Individuals.FirstOrDefault(value =>
                string.Equals(value.ExternalId, _selectedExternalId, StringComparison.Ordinal)
                && value.HasIdentity);
            if (selected is null)
            {
                Label(ref y, viewWidth, "Choose Inspect beside an enrolled individual.", 38f);
                Finish(inRect, ref y);
                return;
            }

            DrawIndividual(ref y, viewWidth, selected);
            Finish(inRect, ref y);
        }

        private void ApplyEnrollment(DagmayIdentityGameComponent component, string externalId, bool enabled)
        {
            component.SetEnrollment(externalId, enabled, out _lastAction);
            InvalidateSnapshot();
            if (enabled) _selectedExternalId = externalId;
        }

        private void InvalidateSnapshot()
        {
            _nextSnapshotRefreshUtc = DateTimeOffset.MinValue;
        }

        private static void DrawOrdinaryMind(ref float y, float width, OrdinaryMindSnapshot mind)
        {
            Label(ref y, width, mind.DisplayName + " — " + mind.ServiceStatus, 34f);
            Label(ref y, width, "Current state: " + mind.CurrentState, 52f);

            Section(ref y, width, "Grounded self-description");
            if (mind.IdentityFacts.Count == 0)
            {
                Label(ref y, width, "No grounded identity facts are available yet.", 30f);
            }
            foreach (var fact in mind.IdentityFacts)
            {
                Label(ref y, width, fact, 34f);
            }

            Section(ref y, width, "Shareable memories");
            if (mind.DisclosedMemories.Count == 0)
            {
                Label(
                    ref y,
                    width,
                    "No memories are currently classified as shareable. Private and Observer-only memories remain hidden by design.",
                    48f);
            }
            foreach (var memory in mind.DisclosedMemories)
            {
                Label(
                    ref y,
                    width,
                    memory.OccurredAtUtc.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)
                    + " | " + memory.Tier
                    + " | confidence " + memory.Confidence.ToString("0.00", CultureInfo.InvariantCulture)
                    + "\n" + memory.DiaryEntry,
                    82f);
            }
        }

        private static void DrawIndividual(ref float y, float width, ObserverIndividual individual)
        {
            Label(ref y, width, individual.DisplayName + " — " + individual.Enrollment, 34f);
            Label(
                ref y,
                width,
                "Individual ID: " + individual.IndividualId
                + " | Lineage: " + individual.LineageId
                + " | Version: " + individual.Version
                + " | Lifecycle: " + individual.Lifecycle,
                50f);
            var affect = individual.Affect;
            Label(
                ref y,
                width,
                "Affect — Valence " + Format(affect.Valence)
                + " | Arousal " + Format(affect.Arousal)
                + " | Threat " + Format(affect.Threat)
                + " | Agency " + Format(affect.Agency)
                + " | Attachment " + Format(affect.Attachment)
                + " | Certainty " + Format(affect.Certainty)
                + " | Standing " + Format(affect.SocialStanding),
                58f);

            Section(ref y, width, "Grounded identity seed");
            if (individual.SeedFacts.Count == 0) Label(ref y, width, "No seed facts are available.", 30f);
            foreach (var fact in individual.SeedFacts)
            {
                Label(
                    ref y,
                    width,
                    fact.Category + " — " + fact.Key + ": " + fact.Value
                    + " (confidence " + fact.Confidence.ToString("0.00", CultureInfo.InvariantCulture) + ")",
                    46f);
            }

            Section(ref y, width, "Recent and significant private memories");
            if (individual.Memories.Count == 0) Label(ref y, width, "No subjective memories are available yet.", 30f);
            foreach (var memory in individual.Memories)
            {
                Label(
                    ref y,
                    width,
                    memory.OccurredAtUtc.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)
                    + " | encoded " + memory.EncodedAtUtc.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)
                    + " | " + memory.Tier + " | " + memory.Privacy
                    + " | importance " + memory.Importance.ToString("0.00", CultureInfo.InvariantCulture)
                    + " | emotional " + memory.EmotionalWeight.ToString("0.00", CultureInfo.InvariantCulture)
                    + " | confidence " + memory.Confidence.ToString("0.00", CultureInfo.InvariantCulture)
                    + " | access " + memory.Accessibility.ToString("0.00", CultureInfo.InvariantCulture)
                    + "\nMemory: " + memory.MemoryId
                    + " | source event(s): " + JoinIds(memory.SourceEventIds)
                    + " | perception(s): " + JoinIds(memory.SourcePerceptionIds)
                    + "\nAffect at encoding — V " + Format(memory.AffectAtEncoding.Valence)
                    + " | A " + Format(memory.AffectAtEncoding.Arousal)
                    + " | T " + Format(memory.AffectAtEncoding.Threat)
                    + " | Agency " + Format(memory.AffectAtEncoding.Agency)
                    + " | Attach " + Format(memory.AffectAtEncoding.Attachment)
                    + " | Cert " + Format(memory.AffectAtEncoding.Certainty)
                    + " | Standing " + Format(memory.AffectAtEncoding.SocialStanding)
                    + "\nPeople involved: " + JoinIds(memory.PeopleInvolved)
                    + "\nDiary: " + memory.DiaryEntry
                    + "\nAppraisal: " + memory.Appraisal,
                    200f);
            }

            Section(ref y, width, "Validated autobiographical reflections");
            if (individual.Reflections.Count == 0) Label(ref y, width, "No retained validated reflection payload is available yet.", 30f);
            foreach (var reflection in individual.Reflections)
            {
                Label(
                    ref y,
                    width,
                    reflection.OccurredAtUtc.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)
                    + " | " + reflection.Provider + "/" + reflection.Model
                    + " | confidence " + reflection.Confidence.ToString("0.00", CultureInfo.InvariantCulture)
                    + " | evidence " + reflection.EvidenceCount
                    + "\nInterpretation: " + reflection.Interpretation
                    + "\nAutobiographical: " + reflection.AutobiographicalReflection
                    + "\nDecision summary: " + reflection.DecisionSummary,
                    154f);
            }
        }

        private static void NumberControl(
            ref float y,
            float width,
            string label,
            int value,
            Action decrease,
            Action increase)
        {
            var buttonWidth = 42f;
            var valueWidth = 100f;
            Widgets.Label(new Rect(0f, y, width - (buttonWidth * 2f) - valueWidth - 12f, LineHeight), label);
            Widgets.Label(
                new Rect(width - (buttonWidth * 2f) - valueWidth - 8f, y, valueWidth, LineHeight),
                value.ToString("N0", CultureInfo.CurrentCulture));
            if (Widgets.ButtonText(new Rect(width - (buttonWidth * 2f) - 4f, y, buttonWidth, LineHeight), "−"))
            {
                decrease();
                DagmayMod.SaveSettings();
            }

            if (Widgets.ButtonText(new Rect(width - buttonWidth, y, buttonWidth, LineHeight), "+"))
            {
                increase();
                DagmayMod.SaveSettings();
            }

            y += LineHeight + 4f;
        }

        private static void Section(ref float y, float width, string text)
        {
            y += 10f;
            Label(ref y, width, text, 32f);
        }

        private static void Label(ref float y, float width, string text, float height)
        {
            Widgets.Label(new Rect(0f, y, width, height), text);
            y += height;
        }

        private static string Health(bool healthy) => healthy ? "healthy" : "READ-ONLY";
        private static string Format(double value) => value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);

        private static string JoinIds(IReadOnlyList<string> values)
        {
            if (values.Count == 0) return "none";
            var visible = values.Take(3).ToList();
            var result = string.Join(", ", visible);
            return values.Count <= visible.Count
                ? result
                : result + " (+" + (values.Count - visible.Count).ToString(CultureInfo.InvariantCulture) + " more)";
        }

        private static void Finish(Rect inRect, ref float y)
        {
            y = Math.Max(y, inRect.height);
            Widgets.EndScrollView();
        }
    }
}
