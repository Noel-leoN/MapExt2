using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using EconomyEX.Detection;

namespace EconomyEX.Settings
{
    [FileLocation("ModsSettings/" + Mod.ModName + "/" + Mod.ModName)]
    public class ModSettings : ModSetting
    {
        public const string kSectionGeneral = "General";
        public const string kSectionStatus = "Status";

        public ModSettings(IMod mod) : base(mod)
        {
        }

        [SettingsUISection(kSectionStatus, kSectionStatus)]
        public string StatusInfo { get; private set; } = "Initializing...";

        [SettingsUISection(kSectionStatus, kSectionStatus)]
        public string ConflictWarning { get; set; } = "None"; // Updated by ConflictMonitoringSystem

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        public bool EnableEconomyFix { get; set; } = true;

        public void UpdateStatus()
        {
            if (Mod.IsMapExtPresent)
            {
                StatusInfo = "DISABLED: 'MapExt' mod detected. Please use MapExt's internal economy module.";
            }
            else if (Mod.IsActive)
            {
                StatusInfo = "ACTIVE: Economy Fixes Enabled (Vanilla Map Detected).";
            }
            else if (Detection.MapSizeDetector.HasCheckedMapSize && !Mod.IsVanillaMap)
            {
                 StatusInfo = "DISABLED: Large Map Detected. This mod is for Vanilla maps only.";
            }
            else
            {
                StatusInfo = "IDLE: Waiting for map load...";
            }
        }

        public override void SetDefaults()
        {
            EnableEconomyFix = true;
        }
    }
}
