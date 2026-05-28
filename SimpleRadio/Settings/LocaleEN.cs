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
                { m_Setting.GetOptionTabLocaleID(ModSettings.kTabFormat), "Format" },

                // === Group: Status ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupStatus), "Status" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.StationInfo)), "Loaded Stations" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.StationInfo)),
                    "Number of custom radio stations detected in the data folder." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.SongInfo)), "Loaded Songs" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.SongInfo)),
                    "Total number of audio files loaded across all stations." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DataPath)), "Data Folder" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DataPath)),
                    "Path to the SimpleRadio data folder. Create subfolders here with audio files (.ogg, .mp3, .wav) to add custom stations." },

                // === Group: Actions ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupActions), "Actions" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.OpenDataFolder)), "Open Data Folder" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.OpenDataFolder)),
                    "Open the SimpleRadio data folder in Windows Explorer.\n\n" +
                    "To add a custom station:\n" +
                    "1. Create a subfolder (folder name = station name)\n" +
                    "2. Place audio files inside (.ogg, .mp3, .wav)\n" +
                    "3. Optionally add an icon.svg\n" +
                    "4. Click 'Refresh Stations' or restart the game" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RefreshStations)), "♫ Refresh Stations" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshStations)),
                    "Re-scan the data folder and reload all custom stations without restarting the game.\n\n" +
                    "Use this after adding or removing audio files from the data folder." },

                // === Group: Formats ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupFormats), "Audio Formats" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableMP3)), "Enable MP3 Support" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableMP3)),
                    "Allow loading .mp3 audio files.\n\n" +
                    "MP3 is widely supported and works reliably. Requires game restart to take effect." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableWAV)), "Enable WAV Support" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableWAV)),
                    "Allow loading .wav audio files.\n\n" +
                    "WARNING: WAV files are uncompressed and typically 10x larger than OGG/MP3 " +
                    "(~30-50 MB per song). This may significantly increase disk usage " +
                    "and cause brief stuttering on HDD when switching tracks.\n\n" +
                    "Recommended: Convert WAV to OGG or MP3 for better performance.\n\n" +
                    "Requires game restart to take effect." },

                // === Group: Compatibility ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupCompat), "Compatibility" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ExtendedRadioStatus)), "ExtendedRadio" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ExtendedRadioStatus)),
                    "Shows whether ExtendedRadio is detected.\n\n" +
                    "When ExtendedRadio is active, it already provides its own multi-format audio support " +
                    "(including MP3 and WAV). SimpleRadio will automatically disable its own format patches " +
                    "to avoid conflicts. The format toggles above will still control which files SimpleRadio scans, " +
                    "but audio decoding will be handled by ExtendedRadio." },
            };
        }

        public void Unload() { }
    }
}
