using Colossal;
using System.Collections.Generic;

namespace SimpleRadio.Settings
{
    public class LocaleEN : IDictionarySource
    {
        private readonly SimpleRadioSettings m_Setting;

        public LocaleEN(SimpleRadioSettings setting)
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
                { m_Setting.GetOptionTabLocaleID(SimpleRadioSettings.kTabInfo), "Info" },
                { m_Setting.GetOptionTabLocaleID(SimpleRadioSettings.kTabFormat), "Format" },

                // === Group: Status ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupStatus), "Status" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.StationInfo)), "Loaded Stations" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.StationInfo)),
                    "Number of custom radio stations detected in the data folder." },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.SongInfo)), "Loaded Songs" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.SongInfo)),
                    "Total number of audio files loaded across all stations." },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.DataPath)), "Data Folder" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.DataPath)),
                    "Path to the SimpleRadio data folder. Create subfolders here with audio files (.ogg, .mp3, .wav) to add custom stations." },

                // === Group: Actions ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupActions), "Actions" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.OpenDataFolder)), "Open Data Folder" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.OpenDataFolder)),
                    "Open the SimpleRadio data folder in Windows Explorer.\n\n" +
                    "To add a custom station:\n" +
                    "1. Create a subfolder (folder name = station name)\n" +
                    "2. Place audio files inside (.ogg, .mp3, .wav)\n" +
                    "3. Optionally add an icon.svg\n" +
                    "4. Click 'Refresh Stations' or restart the game" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.RefreshStations)), "♫ Refresh Stations" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.RefreshStations)),
                    "Re-scan the data folder and reload all custom stations without restarting the game.\n\n" +
                    "Use this after adding or removing audio files from the data folder." },

                // === 播放設定 ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupPlayback), "Playback" },
                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.RestoreLastStation)), "Restore Last Used Station" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.RestoreLastStation)),
                    "Resume the last station used across cities. When disabled, each city uses the station in its save. " +
                    "Applies the next time you enter a city." },

                // === Group: Formats ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupFormats), "Audio Formats" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.EnableMP3)), "Enable MP3 Support" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.EnableMP3)),
                    "Allow loading .mp3 audio files.\n\n" +
                    "MP3 is widely supported and works reliably. Click 'Refresh Stations' to apply." },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.EnableWAV)), "Enable WAV Support" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.EnableWAV)),
                    "Allow loading .wav audio files.\n\n" +
                    "WARNING: WAV files are uncompressed and typically 10x larger than OGG/MP3 " +
                    "(~30-50 MB per song). This may significantly increase disk usage " +
                    "and cause brief stuttering on HDD when switching tracks.\n\n" +
                    "Recommended: Convert WAV to OGG or MP3 for better performance.\n\n" +
                    "Click 'Refresh Stations' to apply." },

                // === Group: Compatibility ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupCompat), "Compatibility" },

                { SimpleRadioSettings.kLocaleExtendedRadioDetected, "Detected - compatible" },
                { SimpleRadioSettings.kLocaleExtendedRadioMissing, "Not detected" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.ExtendedRadioStatus)), "ExtendedRadio" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.ExtendedRadioStatus)),
                    "Shows whether the ExtendedRadio mod is installed.\n\n" +
                    "SimpleRadio works on its own either way — you can use both mods together without conflicts.\n\n" +
                    "If another radio mod runs into an error while loading, SimpleRadio keeps the game's radio working." },
            };
        }

        public void Unload() { }
    }
}
