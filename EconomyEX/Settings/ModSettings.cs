using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using EconomyEX.Helpers;

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

        public bool IsEconomyFixDisabled => !EnableEconomyFix;

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        public bool EnableDemandEcoSystem { get; set; } = true;

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        public bool EnableJobSearchEcoSystem { get; set; } = true;

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        public bool EnableHouseholdPropertyEcoSystem { get; set; } = true;

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        public bool EnableResourceBuyerEcoSystem { get; set; } = true;

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        public bool EnableResidentAIEcoSystem { get; set; } = true;

        public const string kSectionPathfinding = "Pathfinding";

        // ==========================================
        // 寻路优化参数（与 MapExtPDX 属性名一致，便于移植）
        // ==========================================
        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float ShoppingMaxCost { get; set; } = 8000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float CompanyShoppingMaxCost { get; set; } = 200000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float LeisureMaxCost { get; set; } = 12000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 17000f, step = 500f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float EmergencyMaxCost { get; set; } = 6000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 17000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindJobMaxCost { get; set; } = 200000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 17000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindHomeMaxCost { get; set; } = 200000f;

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
            else if (Helpers.MapSizeDetector.HasCheckedMapSize && !Mod.IsVanillaMap)
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
            EnableDemandEcoSystem = true;
            EnableJobSearchEcoSystem = true;
            EnableHouseholdPropertyEcoSystem = true;
            EnableResourceBuyerEcoSystem = true;
            EnableResidentAIEcoSystem = true;
            ShoppingMaxCost = 8000f;
            CompanyShoppingMaxCost = 200000f;
            LeisureMaxCost = 12000f;
            EmergencyMaxCost = 6000f;
            FindJobMaxCost = 200000f;
            FindHomeMaxCost = 200000f;
        }
    }
}
