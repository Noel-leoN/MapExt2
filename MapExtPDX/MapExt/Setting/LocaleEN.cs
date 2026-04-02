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
                    "The currently selected and successfully applied map size. This size refers to the edge length of the map. Unit is in meters.\n⚠️ Warning: Although MapExt has loadgame validation to prevent loading wrong size game saves, Please BACKUP ALL of your GameSaves (Strongly recommand SKYVE) before Loading them with this mod!!! Otherwise, there is a risk that the save may be corrupted due to game crashes or other special reasons."
                },

                { m_Setting.GetOptionGroupLocaleID(ModSettings.kEcoGroup), "▍Economy Overhaul (Beta)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.kEcoGroup)), "• Logic & Perf. Optimization (Beta)" },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.isEnableEconomyFix)),
                    "• Economy Logic & Perf. Optimization (Beta Master Switch)"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.isEnableEconomyFix)),
                    "(This patch is currently in the testing phase (Beta))\nFixes and optimizes the following systems to adapt to cities with populations in the millions:\n - Residential/Commercial/Industrial demand systems\n - Household home-search system\n - Household behavior system (consumer behavior adjustment)\n - Citizen job-search system\n - Rent calculation system\n - Resource procurement & service coverage pathfinding systems\n - Resident AI pathfinding optimization patch\n\n⚠️ [CRITICAL]: Changing this option requires a GAME RESTART to take effect safely. Otherwise, severe logical bugs will occur!"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableDemandEcoSystem)),
                    "  ├─ RCI Demand Systems"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableDemandEcoSystem)),
                    "Optimizes Residential, Commercial, and Industrial demand calculation models for a smoother experience.\n\n⚠️ Restart Required."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)),
                    "  ├─ Job Search Systems"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)),
                    "Optimizes citizen job-search behavior and matching algorithms to improve efficiency.\n\n⚠️ Incompatible with Realistic JobSearch and similar mods!\n⚠️ Restart Required."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)),
                    "  ├─ Household, Property & Rent Systems"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)),
                    "Optimizes household home-searching pathfinding; includes a realistic Land Value remake to make location values more reasonable; and heavily refactors the expensive rent adjustment mechanism.\n\n⚠️ Restart Required."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)),
                    "  ├─ Consumer & Service Pathing Systems"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)),
                    "Optimizes resource matching for citizen shopping and company restocking, greatly reducing performance overhead from extreme-distance pathfinding.\n\n⚠️ Incompatible with Realistic PathFinding and similar mods!\n⚠️ Restart Required."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)),
                    "  └─ Resident AI Pathing System"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)),
                    "Fixes citizen pathfinding AI wait time logic flaws and mitigates critical memory overflows caused by large map path calculations.\n\n⚠️ Incompatible with Realistic PathFinding and similar mods!\n⚠️ Restart Required."
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
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoDogs)), "× NoDogs 2.0" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoDogs)),
                    "NoDogs 2.0 — A multi-level pet population control tool to boost simulation performance.\n\n" +
                    "1. Vanilla: Normal pet spawning (default game behavior).\n" +
                    "2. Disable OnStreet: Prevents pets from appearing on streets (disables rendering/pathfinding). Logical pet entities still exist in memory.\n" +
                    "3. Prevent Gen: Blocks new household pet generation. Existing pets remain but no new ones will be created.\n" +
                    "4. Purge All: Blocks generation AND removes all existing pet entities from the save. Maximum performance gain.\n\n" +
                    "⚠️ WARNING: After purging, existing households will NOT re-acquire pets. Only newly moved-in households will bring dogs if you switch back to Vanilla."
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DislayPetCount)), "• Logical Pets Count" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DislayPetCount)),
                    "Displays the current number of logical pet entities in the simulation. Click 'Refresh' below to update."
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RefreshPetCount)), "↻ Refresh Pet Count" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshPetCount)),
                    "Click to query and refresh the current logical pet entity count from the ECS world."
                },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kMiscTab), "▍EconomyTweak (Beta)" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kMiscGroup), "▍EconomyTweak (Beta)" },
                { m_Setting.GetOptionLabelLocaleID(ModSettings.kMiscTab), "• Economy Detail Tweak (Beta)" },
                {
                    m_Setting.GetOptionGroupLocaleID(ModSettings.kEconomyTweakGroup),
                    "▍Pathfinding Optimization(Can be Adjusted In Game)"
                },
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
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EmergencyMaxCost)),
                    "Max Hospital/Crime Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EmergencyMaxCost)),
                    "Controls the maximum search range for citizens seeking hospitals (when sick/injured) or committing crimes. A lower value restricts these activities to nearby areas, encouraging locally planned services.\n" +
                    "★ Tip: Build hospitals and police stations within this cost radius of residential areas. If your facilities are very close, you can further reduce this value.\n" +
                    "★ Recommended values:\n" +
                    " - All Map Sizes: 4000 ~ 8000 (Default: 6000)"
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
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolElementaryMaxCost)), "Max Elementary School Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolElementaryMaxCost)),
                    "Controls the maximum search range when Elementary students look for a school. Lowering this value forces them to enroll in nearby local schools only.\n" +
                    "★ Recommended:\n" +
                    " - All Map Sizes: 10000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolHighSchoolMaxCost)), "Max High School Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolHighSchoolMaxCost)),
                    "Controls the maximum search range when High School students look for a school.\n" +
                    "★ Recommended:\n" +
                    " - All Map Sizes: 17000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolCollegeMaxCost)), "Max College Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolCollegeMaxCost)),
                    "Controls the maximum search distance when connecting to a College.\n" +
                    "★ Recommended:\n" +
                    " - All Map Sizes: 50000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolUniversityMaxCost)), "Max University Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolUniversityMaxCost)),
                    "Controls the maximum search range for Universities. If there is only one University center on a massive map, it is recommended to max this out to cover everyone.\n" +
                    "★ Recommended:\n" +
                    " - All Map Sizes: 100000 ~ 200000"
                },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kDebugTab), "▍Developer Options" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kDebugGroup), "▍Developer Options" },
                { m_Setting.GetOptionLabelLocaleID(ModSettings.kDebugTab), "▍Developer Options" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DisableLoadGameValidation)), "× Disable LoadGame Validation" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DisableLoadGameValidation)),
                    "⚠️ WARNING! Enabled by default (unchecked). LoadGame validation prevents loading saves with incorrect map size modes, which corrupts saves!\n" +
                    "Checking this will disable the validation. Only use this for special cases, such as unrecognised saves from older MapExt versions. Please ensure you've selected the correct 'MapSize Mode' before loading, or you might corrupt your save!\n" +
                    "Always backup your saves before using this feature."
                },
            };
            return entries;
        }

        public void Unload()
        {
        }
    }
}