// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// --- LOCALE ---

using Colossal;
using System.Collections.Generic;

namespace MapExtPDX
{
    public class LocaleEN : IDictionarySource
    {
        private readonly ModSettings m_Setting;

        public LocaleEN(ModSettings setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            var entries = new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "#MapExt" }, // Main mod title
                { m_Setting.GetOptionTabLocaleID(ModSettings.kMapSizeModeTab), "MapSize Mode" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kMainModeGroup), "Main MapSize Mode" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.PatchModeChoice)), "► Select MapSize Mode" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.PatchModeChoice)),
                    "⚠️ Changes require clicking the 'Apply Changes' button to take effect!\n\nMode List:\n - ModeA: 57km (4x4)  DEM:14m\n - ModeB: 28km (2x2)  DEM:7m\n - ModeC: 114km (8x8)  DEM:28m\n - Vanilla: 14km Vanilla(1x1) DEM:3.5m\n\nNotice:\n1. Larger MapSizes suffer from lower DEM resolution, resulting in rougher graphics for mountains, ramps, and waterfronts. If you have high requirements for terrain smoothness, it is recommended to build a flatter map or use landscaping tools to mask areas.\n2. The larger the MapSize, the greater the floating-point calculation deviation of the economic simulation data at the edges (causing false visuals). This is an unfixable engine limitation, especially noticeable in 114km mode. It is recommended to build active zones (Res/Com/Ind) in the center of the map.\n\n! [CRITICAL]: You MUST RESTART THE GAME after changing the map size mode before loading a save. Failure to do so will cause logic conflicts and may corrupt your save data!"
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ApplyPatchChanges)), "► Apply Changes" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ApplyPatchChanges)),
                    "Applies the selected patch mode.\n\n⚠️ [CRITICAL]: This Mod does not support hot-switching. You MUST RESTART THE GAME after applying changes and before loading any save. Loading a save directly will cause simulation corruption!"
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ApplyPatchChanges)),
                    "Applying MapSize Mode, please be patient until completion.\n\n⚠️ Please completely RESTART THE GAME after completion. Do not load any save immediately!"
                },

                // Display names for enum values in the dropdown (if not using GetPatchModeDisplayName directly)
                // This uses the default enum value localization mechanism.

                //{ m_Setting.GetEnumValueLocaleID(PatchModeSetting.ModeA), "Mode 57km (4x4) DEM:14m" },
                //{ m_Setting.GetEnumValueLocaleID(PatchModeSetting.ModeB), "Mode 28km (2x2) DEM:7m" },
                //{ m_Setting.GetEnumValueLocaleID(PatchModeSetting.ModeC), "Mode 114km (8x8) DEM:28m" },
                //{ m_Setting.GetEnumValueLocaleID(PatchModeSetting.ModeD), "Mode 229km (16x16) DEM:56m" },
                //{ m_Setting.GetEnumValueLocaleID(PatchModeSetting.None), "None (No Patches)" },

                // Add back your original localization entries for other settings
                // { m_Setting.GetOptionGroupLocaleID(Setting.kButtonGroup), "Buttons" },
                // ...
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kInfoGroup), "▍MapSize Info" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.kInfoGroup)), "▍MapSize Info" },

                //{ m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LoadedSaveCoreValue)), "Loaded Save's MapSize" },
                //{ m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LoadedSaveCoreValue)), "Loaded Save's Map Size" },

                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ModSettingCoreValue)),
                    "• Current Applied MapSize"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ModSettingCoreValue)),
                    "⚠️ Warning: Although MapExt has loadgame validation to prevent loading wrong size game saves, Please BACKUP ALL of your GameSaves (Strongly recommand SKYVE) before Loading them with this mod!!! Otherwise, there is a risk that the save may be corrupted due to game crashes or other special reasons"
                },

                { m_Setting.GetOptionGroupLocaleID(ModSettings.kEcoGroup), "▍Economy Overhaul" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.kEcoGroup)), "• Logic & Perf. Optimization" },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.isEnableEconomyFix)),
                    "• Economy Logic & Perf. Optimization (Master Switch)"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.isEnableEconomyFix)),
                    "Fixes and optimizes the following systems to adapt to cities with populations in the millions:\n - Residential/Commercial/Industrial demand systems\n - Household home-search system\n - Household behavior system (consumer behavior adjustment)\n - Citizen job-search system\n - Rent calculation system\n\n⚠️ [CRITICAL]: Changing this option requires a GAME RESTART to take effect safely. Otherwise, severe logical bugs will occur!"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableDemandEcoSystem)),
                    "  ├─ Demand Systems (Restart Required)"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableDemandEcoSystem)),
                    "Takes over and optimizes vanilla Residential/Commercial/Industrial demand calculations. (Includes A1/A2/A3 systems)\n\n⚠️ Restart Required."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)),
                    "  ├─ Job Search Systems (Restart Required)"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)),
                    "Takes over and optimizes citizen job-finding behaviors. (Includes B1/B2 systems)\n\n⚠️ Restart Required."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)),
                    "  ├─ Household, Property & Rent Systems (Restart Required - CORE)"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)),
                    "Takes over Household Behavior, Home Searching, and Rent Adjustment systems. These three systems are deeply coupled. (Includes C1/C2/D1 systems)\n\n⚠️ Restart Required."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)),
                    "  ├─ Consumer & Service Pathing Systems (Restart Required)"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)),
                    "Takes over pathfinding and resource matching for citizens shopping and companies restocking. (Includes E1/E2/E3 systems)\n\n⚠️ Restart Required."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)),
                    "  └─ Resident AI Pathing System (Restart Required)"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)),
                    "Optimizes citizen pathfinding logic and prevents critical memory overflows on large maps. (Includes F1 system)\n\n⚠️ Restart Required."
                },

                { m_Setting.GetOptionGroupLocaleID(ModSettings.kNoteGroup), "▍Caution!" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.kNoteGroup)), "▍Caution!" },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ModeChangeWarningMessage)),
                    "⚠️ Please completely RESTART THE GAME after applying ANY of the above settings!"
                },

                //{ m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.WarningInfo)), "Warning: Please BACKUP ALL of your GameSaves (Strongly recommand SKYVE) before Loading them with this mod!!! Otherwise, there is a risk that the save may be corrupted due to game crashes or other special reasons" },

                // { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.IsModSettingCoreValueMatch)), "Loaded Save MapSize Match with ModSetting" },
                //{ m_Setting.GetOptionDescLocaleID(nameof(ModSettings.IsModSettingCoreValueMatch)), "Loaded Save MapSize Match with ModSetting" },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kPerformanceToolTab), "▍Perf. Tools" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kPerformanceToolGroup), "▍Performance Tools" },
                {
                    m_Setting.GetOptionLabelLocaleID(ModSettings.kPerformanceToolTab),
                    "※ Some Performance Tools to slightly reduce CPU/GPU pressure. (It'll be a while before this starts working.)"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoThroughTraffic)), "× No Through-Traffic" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoThroughTraffic)),
                    "Disable Through-Traffic to reduce pathfinding calculation and traffic congestion. It'll take effect after the game has been running for a while."
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoDogs)), "× No Dogs" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoDogs)),
                    "It'll take effect after the game has been running for a while, so just wait for the dogs to come home or go on a trip to another city."
                },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kMiscTab), "▍EconomyTweak" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kMiscTab), "▍EconomyTweak" },
                { m_Setting.GetOptionLabelLocaleID(ModSettings.kMiscTab), "• Economy Detail Tweak" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShoppingMaxCost)), "Max Shopping Pathfind Cost" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShoppingMaxCost)),
                    "Controls the maximum travel cost citizens are willing to endure for shopping (groceries, dining). A lower value makes citizens give up faster when shops are far, significantly reducing CPU load on large maps.\n" +
                    "★ Recommended values:\n" +
                    " - 14km / 28km: 8000\n" +
                    " - 57km / 114km: 8000 ~ 12000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.CompanyShoppingMaxCost)),
                    "Company Max Delivery Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.CompanyShoppingMaxCost)),
                    "Controls the maximum search distance when companies (factories/stores) attempt to restock materials via cargo delivery. A higher value (up to 200k) allows companies to search across the entire map, preventing extreme material shortages on large maps.\n" +
                    "★ Recommended values:\n" +
                    " - All Map Sizes: 200000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LeisureMaxCost)),
                    "Max Leisure/Sightseeing Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LeisureMaxCost)),
                    "Controls the maximum travel cost citizens are willing to endure to visit parks, landmarks, or sightseeing. A lower value prevents excessive pathfinding for aimless wandering.\n" +
                    "★ Recommended values:\n" +
                    " - 14km / 28km: 8000 ~ 12000\n" +
                    " - 57km / 114km: 12000 ~ 20000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindJobMaxCost)), "Max Find Job Pathfind Cost" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindJobMaxCost)),
                    "Controls how far citizens are willing to search across the map for a job. A higher value (up to 200k) helps isolated towns on large maps finding workers. This occurs very rarely, so it's recommended to max it out (minimal performance impact).\n" +
                    "★ Recommended:\n" +
                    " - All Map Sizes: 200000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindHomeMaxCost)), "Max Find Home Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindHomeMaxCost)),
                    "Controls the maximum search distance when citizens look for a new home. Increasing this allows citizens to relocate across the entire large map, preventing remote towns from being empty. This occurs rarely, recommended to max it out.\n" +
                    "★ Recommended:\n" +
                    " - All Map Sizes: 200000"
                },
            };
            return entries;
        }

        public void Unload()
        {
        }
    }
}