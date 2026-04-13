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
        public bool EnableResourceBuyerEcoSystem { get; set; } = false;

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        public bool EnableResidentAIEcoSystem { get; set; } = false;

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        public bool ResetEcoSystemToggles
        {
            set
            {
                EnableDemandEcoSystem = true;
                EnableJobSearchEcoSystem = true;
                EnableHouseholdPropertyEcoSystem = true;
                EnableResourceBuyerEcoSystem = false;
                EnableResidentAIEcoSystem = false;
            }
        }

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

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolElementaryMaxCost { get; set; } = 10000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolHighSchoolMaxCost { get; set; } = 17000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolCollegeMaxCost { get; set; } = 50000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolUniversityMaxCost { get; set; } = 100000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        public bool ResetPathfinding
        {
            set
            {
                ShoppingMaxCost = 8000f;
                CompanyShoppingMaxCost = 200000f;
                LeisureMaxCost = 12000f;
                EmergencyMaxCost = 6000f;
                FindJobMaxCost = 200000f;
                FindHomeMaxCost = 200000f;
                FindSchoolElementaryMaxCost = 10000f;
                FindSchoolHighSchoolMaxCost = 17000f;
                FindSchoolCollegeMaxCost = 50000f;
                FindSchoolUniversityMaxCost = 100000f;
            }
        }

        // ==========================================
        // 经济行为参数
        // ==========================================
        public const string kSectionBehavior = "Behavior";

        [SettingsUISection(kSectionGeneral, kSectionBehavior)]
        [SettingsUISlider(min = 200, max = 5000, step = 100, scalarMultiplier = 1, unit = Game.UI.Unit.kInteger)]
        public int JobSeekerCap { get; set; } = 1000;

        [SettingsUISection(kSectionGeneral, kSectionBehavior)]
        [SettingsUISlider(min = 500, max = 10000, step = 500, scalarMultiplier = 1, unit = Game.UI.Unit.kInteger)]
        public int PathfindRequestCap { get; set; } = 4000;

        [SettingsUISection(kSectionGeneral, kSectionBehavior)]
        [SettingsUISlider(min = 1, max = 20, step = 1, scalarMultiplier = 10000, unit = Game.UI.Unit.kInteger)]
        public float ShoppingTrafficReduction { get; set; } = 0.0004f;

        [SettingsUISection(kSectionGeneral, kSectionBehavior)]
        [SettingsUISlider(min = 1.0f, max = 8.0f, step = 0.5f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float HouseholdResourceDemandMultiplier { get; set; } = 3.5f;

        [SettingsUISection(kSectionGeneral, kSectionBehavior)]
        [SettingsUISlider(min = 32, max = 512, step = 16, scalarMultiplier = 1, unit = Game.UI.Unit.kInteger)]
        public int HomeSeekerCap { get; set; } = 128;

        [SettingsUISection(kSectionGeneral, kSectionBehavior)]
        [SettingsUISlider(min = 128, max = 5120, step = 128, scalarMultiplier = 1, unit = Game.UI.Unit.kInteger)]
        public int HomelessSeekerCap { get; set; } = 1280;

        [SettingsUISection(kSectionGeneral, kSectionBehavior)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        public bool ResetEcoBehavior
        {
            set
            {
                JobSeekerCap = 1000;
                PathfindRequestCap = 4000;
                ShoppingTrafficReduction = 0.0004f;
                HouseholdResourceDemandMultiplier = 3.5f;
                HomeSeekerCap = 128;
                HomelessSeekerCap = 1280;
            }
        }

        // ==========================================
        // NoDogs (backing fields, no UI in EconomyEX)
        // ==========================================
        [SettingsUIHidden]
        public bool NoDogsOnStreet { get; set; } = false;
        [SettingsUIHidden]
        public bool NoDogsGeneration { get; set; } = false;
        [SettingsUIHidden]
        public bool NoDogsPurge { get; set; } = false;


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

        /// <summary>系统状态报告概要，由 ConflictMonitoringSystem 更新</summary>
        [SettingsUISection(kSectionStatus, kSectionStatus)]
        public string SystemStatusReport { get; set; } = "Waiting...";

        public override void SetDefaults()
        {
            EnableEconomyFix = true;
            EnableDemandEcoSystem = true;
            EnableJobSearchEcoSystem = true;
            EnableHouseholdPropertyEcoSystem = true;
            EnableResourceBuyerEcoSystem = false;
            EnableResidentAIEcoSystem = false;
            ShoppingMaxCost = 8000f;
            CompanyShoppingMaxCost = 200000f;
            LeisureMaxCost = 12000f;
            EmergencyMaxCost = 6000f;
            FindJobMaxCost = 200000f;
            FindHomeMaxCost = 200000f;
            FindSchoolElementaryMaxCost = 10000f;
            FindSchoolHighSchoolMaxCost = 17000f;
            FindSchoolCollegeMaxCost = 50000f;
            FindSchoolUniversityMaxCost = 100000f;
            JobSeekerCap = 1000;
            PathfindRequestCap = 4000;
            ShoppingTrafficReduction = 0.0004f;
            HouseholdResourceDemandMultiplier = 3.5f;
            HomeSeekerCap = 128;
            HomelessSeekerCap = 1280;
        }
    }
}
