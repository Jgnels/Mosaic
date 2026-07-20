using Dagmay.RimWorld.Observer;
using Dagmay.RimWorld.Settings;
using UnityEngine;
using Verse;

namespace Dagmay.RimWorld.Bootstrap
{
    public sealed class DagmayMod : Mod
    {
        private readonly DagmayObserverSettingsUi _settingsUi = new DagmayObserverSettingsUi();

        public DagmayMod(ModContentPack content) : base(content)
        {
            Instance = this;
            CurrentSettings = GetSettings<DagmayModSettings>();
            CurrentSettings.ValidateOrRestoreDefaults();
            Log.Message($"[Dagmay] Version {DagmayBuildInfo.Version} loaded. Ordinary Mind view and Restricted Observer interface active; no pawn-control code is present.");
        }

        public static DagmayMod? Instance { get; private set; }
        public static DagmayModSettings CurrentSettings { get; private set; } = new DagmayModSettings();

        public override string SettingsCategory() => "Dagmay";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            _settingsUi.Draw(inRect, CurrentSettings);
        }

        public static void SaveSettings()
        {
            Instance?.WriteSettings();
        }
    }
}
