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
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            var entries = new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "1MapExt" }, // Main mod title
                { m_Setting.GetOptionTabLocaleID(ModSettings.kMapSizeModeTab), "MapSize Mode" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kMainModeGroup), "Main MapSize Mode" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.PatchModeChoice)), "Select MapSize Mode" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.PatchModeChoice)), "Changes require clicking 'Apply Changes'."  + "\r\n" + "\r\n" + "Mode List:" + "\r\n" + "ModeA: 57km  (4x4)  DEM:14m" + "\r\n" + "ModeB: 28km  (2x2)  DEM:7m" + "\r\n" + "ModeC: 114km  (8x8)  DEM:28m" + "\r\n" + "ModeD: 229km  (16x16)  DEM:56m" + "\r\n" + "ModeNone: 14km  vanilla(1x1)  DEM:3.5m" + "\r\n" + "Notice:" + "\r\n" + "1. Larger MapSizes have worse DEM resolution, resulting in rough graphics for mountains, ramps, and waterfronts. If you have high requirements for terrain smoothness, it is recommended to build a flatter map or use some masking methods.\r\n2. The larger the MapSize, the greater the calculation deviation of the economic simulation data at the edges. And it usually creates some false visuals. When using the 114km/229km mode, it is recommended to build the active buildings in the center of the map as much as possible."},

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ApplyPatchChanges)), "Apply Changes" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ApplyPatchChanges)), "Applies the selected patch mode. This will take some time to re-patch MapSize." },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ApplyPatchChanges)), "Applying MapSize Mode, Please be patient until completion." },

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
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kInfoGroup), "MapSize Info" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.kInfoGroup)), "MapSize Info" },

                 //{ m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LoadedSaveCoreValue)), "Loaded Save's MapSize" },
                //{ m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LoadedSaveCoreValue)), "Loaded Save's Map Size" },

                 { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ModSettingCoreValue)), "Current Applied MapSize" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ModSettingCoreValue)), "Warning: Although MapExt has loadgame validation to prevent loading wrong size game saves, Please BACKUP ALL of your GameSaves (Strongly recommand SKYVE) before Loading them with this mod!!! Otherwise, there is a risk that the save may be corrupted due to game crashes or other special reasons"},

                //{ m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.WarningInfo)), "Warning: Please BACKUP ALL of your GameSaves (Strongly recommand SKYVE) before Loading them with this mod!!! Otherwise, there is a risk that the save may be corrupted due to game crashes or other special reasons" },

                // { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.IsModSettingCoreValueMatch)), "Loaded Save MapSize Match with ModSetting" },
                //{ m_Setting.GetOptionDescLocaleID(nameof(ModSettings.IsModSettingCoreValueMatch)), "Loaded Save MapSize Match with ModSetting" },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kPerformanceToolTab), "Perf. Tools" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kPerformanceToolGroup), "Performance Tools" },
                  { m_Setting.GetOptionLabelLocaleID(ModSettings.kPerformanceToolTab), "Some Performance Tools to slightly reduce CPU/GPU pressure. (It’ll be a while before this starts working.)" },
                   { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoThroughTraffic)), "No Through-Traffic" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoThroughTraffic)), "Disable Through-Traffic to reduce pathfinding calculation and traffic congestion. It'll take effect after the game has been running for a while." },
                 { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoDogs)), "No Dogs" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoDogs)), "It'll take effect after the game has been running for a while, so just wait for the dogs to come home or go on a trip to another city." },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kMiscTab), "Misc Tools" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kMiscTab), "Misc Tools" },
                { m_Setting.GetOptionLabelLocaleID(ModSettings.kMiscTab), "Misc Feature Tools" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LandValueRemake)), "LandValue Remake(Currently unavailable)" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LandValueRemake)), "Remake the landvalue system to bring back the deeper economic systems from earlier versions! (with bug fixed and enhanced)." },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kDebugTab), "Debug" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kDebugGroup), "Debug" },
                { m_Setting.GetOptionLabelLocaleID(ModSettings.kDebugTab), "Debug" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DisableLoadGameValidation)), "Disable LoadGame Validation" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DisableLoadGameValidation)), "Warn! Disable LoadGame Validation function. Usually don't click! Use only if your legacy MapExt savegame is not recognized. Legacy savegame need to be in the correct mode selected in the MapSize option, otherwise the savegame may be corrupted." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ApplyAirwayRegenerate)), "Apply Airway Regenerate" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kAirwayGroup), "Airway Regenerator" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ApplyAirwayRegenerate)), "After adding the new airplane path boundary point, just click this to make it active in the game." },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ApplyAirwayRegenerate)), "All airway will be rebuilt soon." },


            };
            return entries;
        }
        public void Unload() { }


    }
}