using System;
using Dagmay.Core.Scheduling;
using Dagmay.RimWorld.Bootstrap;
using Verse;

namespace Dagmay.RimWorld.Settings
{
    public sealed class DagmayModSettings : ModSettings
    {
        public const int CurrentSchemaVersion = 1;
        private const int DefaultSessionRequests = 4;
        private const int DefaultHourlyRequests = 12;
        private const int DefaultDailyTokens = 40_000;
        private const int DefaultHeartbeatMinutes = 1;

        private int _schemaVersion = CurrentSchemaVersion;
        private bool _providerDispatchPaused = true;
        private bool _autoEnrollNewColonists;
        private bool _restrictedObserverEnabled;
        private int _maximumRequestsPerSession = DefaultSessionRequests;
        private int _maximumRequestsPerHour = DefaultHourlyRequests;
        private int _maximumEstimatedTokensPerDay = DefaultDailyTokens;
        private int _heartbeatMinutes = DefaultHeartbeatMinutes;

        public bool ProviderDispatchPaused => _providerDispatchPaused;
        public bool AutoEnrollNewColonists => _autoEnrollNewColonists;
        public bool RestrictedObserverEnabled => _restrictedObserverEnabled;
        public int MaximumRequestsPerSession => _maximumRequestsPerSession;
        public int MaximumRequestsPerHour => _maximumRequestsPerHour;
        public int MaximumEstimatedTokensPerDay => _maximumEstimatedTokensPerDay;
        public int HeartbeatMinutes => _heartbeatMinutes;

        public ReflectionBudgetPolicy CreateBudgetPolicy()
        {
            ValidateOrRestoreDefaults();
            return new ReflectionBudgetPolicy(
                maximumQueueSize: 500,
                maximumConcurrentRequests: 1,
                maximumRequestsPerHour: _maximumRequestsPerHour,
                maximumTokensPerDay: _maximumEstimatedTokensPerDay,
                schedulerHeartbeat: TimeSpan.FromMinutes(_heartbeatMinutes),
                maximumRequestsPerSession: _maximumRequestsPerSession);
        }

        public void SetProviderDispatchPaused(bool value) => _providerDispatchPaused = value;
        public void SetAutoEnrollNewColonists(bool value) => _autoEnrollNewColonists = value;
        public void SetRestrictedObserverEnabled(bool value) => _restrictedObserverEnabled = value;

        public void AdjustMaximumRequestsPerSession(int delta)
        {
            _maximumRequestsPerSession = Clamp(_maximumRequestsPerSession + delta, 1, 100);
        }

        public void AdjustMaximumRequestsPerHour(int delta)
        {
            _maximumRequestsPerHour = Clamp(_maximumRequestsPerHour + delta, 1, 1000);
        }

        public void AdjustMaximumEstimatedTokensPerDay(int delta)
        {
            _maximumEstimatedTokensPerDay = Clamp(
                _maximumEstimatedTokensPerDay + delta,
                1_200,
                10_000_000);
        }

        public void AdjustHeartbeatMinutes(int delta)
        {
            _heartbeatMinutes = Clamp(_heartbeatMinutes + delta, 1, 60);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref _schemaVersion, "dagmaySettingsSchema", CurrentSchemaVersion);
            Scribe_Values.Look(ref _providerDispatchPaused, "providerDispatchPaused", true);
            Scribe_Values.Look(ref _autoEnrollNewColonists, "autoEnrollNewColonists", false);
            Scribe_Values.Look(ref _restrictedObserverEnabled, "restrictedObserverEnabled", false);
            Scribe_Values.Look(ref _maximumRequestsPerSession, "maximumRequestsPerSession", DefaultSessionRequests);
            Scribe_Values.Look(ref _maximumRequestsPerHour, "maximumRequestsPerHour", DefaultHourlyRequests);
            Scribe_Values.Look(ref _maximumEstimatedTokensPerDay, "maximumEstimatedTokensPerDay", DefaultDailyTokens);
            Scribe_Values.Look(ref _heartbeatMinutes, "heartbeatMinutes", DefaultHeartbeatMinutes);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ValidateOrRestoreDefaults();
            }
        }

        public bool ValidateOrRestoreDefaults()
        {
            var valid = _schemaVersion == CurrentSchemaVersion
                && _maximumRequestsPerSession >= 1 && _maximumRequestsPerSession <= 100
                && _maximumRequestsPerHour >= 1 && _maximumRequestsPerHour <= 1000
                && _maximumEstimatedTokensPerDay >= 1_200 && _maximumEstimatedTokensPerDay <= 10_000_000
                && _heartbeatMinutes >= 1 && _heartbeatMinutes <= 60;
            if (valid) return true;

            _schemaVersion = CurrentSchemaVersion;
            _providerDispatchPaused = true;
            _autoEnrollNewColonists = false;
            _restrictedObserverEnabled = false;
            _maximumRequestsPerSession = DefaultSessionRequests;
            _maximumRequestsPerHour = DefaultHourlyRequests;
            _maximumEstimatedTokensPerDay = DefaultDailyTokens;
            _heartbeatMinutes = DefaultHeartbeatMinutes;
            Log.Error($"[Dagmay] Invalid or unsupported {DagmayBuildInfo.Version} settings were rejected. Provider dispatch is paused and conservative defaults were restored.");
            return false;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
