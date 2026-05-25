using Colossal;
using System.Collections.Generic;

namespace SimpleRadio.Settings
{
    public class LocaleEN : IDictionarySource
    {
        private readonly ModSettings m_Setting;

        public LocaleEN(ModSettings setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                // === Mod 名称 ===
                { m_Setting.GetSettingsLocaleID(), "SimpleRadio" },

                // === Tab ===
                { m_Setting.GetOptionTabLocaleID(ModSettings.kTabInfo), "Info" },

                // === Group: Status ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupStatus), "Status" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.StationInfo)), "Loaded Stations" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.StationInfo)),
                    "Number of custom radio stations detected in the data folder." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.SongInfo)), "Loaded Songs" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.SongInfo)),
                    "Total number of OGG audio files loaded across all stations." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DataPath)), "Data Folder" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DataPath)),
                    "Path to the SimpleRadio data folder. Create subfolders here with .ogg files to add custom stations." },

                // === Group: Actions ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupActions), "Actions" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.OpenDataFolder)), "Open Data Folder" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.OpenDataFolder)),
                    "Open the SimpleRadio data folder in Windows Explorer.\n\n" +
                    "To add a custom station:\n" +
                    "1. Create a subfolder (folder name = station name)\n" +
                    "2. Place .ogg audio files inside\n" +
                    "3. Optionally add an icon.svg\n" +
                    "4. Click 'Refresh Stations' or restart the game" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RefreshStations)), "♫ Refresh Stations" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshStations)),
                    "Re-scan the data folder and reload all custom stations without restarting the game.\n\n" +
                    "Use this after adding or removing .ogg files from the data folder." },
            };
        }

        public void Unload() { }
    }
}
