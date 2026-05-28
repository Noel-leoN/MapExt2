using Colossal;
using System.Collections.Generic;

namespace SimpleRadio.Settings
{
    public class LocaleHANS : IDictionarySource
    {
        private readonly ModSettings m_Setting;

        public LocaleHANS(ModSettings setting)
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
                { m_Setting.GetSettingsLocaleID(), "SimpleRadio 简易电台" },

                // === Tab ===
                { m_Setting.GetOptionTabLocaleID(ModSettings.kTabInfo), "信息" },
                { m_Setting.GetOptionTabLocaleID(ModSettings.kTabFormat), "格式" },

                // === Group: Status ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupStatus), "状态" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.StationInfo)), "已加载电台数" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.StationInfo)),
                    "在数据目录中检测到的自定义电台数量。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.SongInfo)), "已加载歌曲数" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.SongInfo)),
                    "所有电台中加载的音频文件总数。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DataPath)), "数据目录" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DataPath)),
                    "SimpleRadio 数据目录路径。在此处创建子文件夹并放入音频文件（.ogg、.mp3、.wav）即可添加自定义电台。" },

                // === Group: Actions ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupActions), "操作" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.OpenDataFolder)), "打开数据目录" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.OpenDataFolder)),
                    "在 Windows 资源管理器中打开 SimpleRadio 数据目录。\n\n" +
                    "添加自定义电台的步骤：\n" +
                    "1. 创建一个子文件夹（文件夹名即为电台名）\n" +
                    "2. 将音频文件放入文件夹（.ogg、.mp3、.wav）\n" +
                    "3. 可选：添加 icon.svg 作为电台图标\n" +
                    "4. 点击「刷新电台」或重启游戏即可生效" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RefreshStations)), "♫ 刷新电台" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshStations)),
                    "重新扫描数据目录并加载所有自定义电台，无需重启游戏。\n\n" +
                    "在数据目录中添加或删除音频文件后，点击此按钮即可刷新。" },

                // === Group: Formats ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupFormats), "音频格式" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableMP3)), "启用 MP3 支持" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableMP3)),
                    "允许加载 .mp3 音频文件。\n\n" +
                    "MP3 格式广泛支持，运行稳定可靠。需要重启游戏生效。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableWAV)), "启用 WAV 支持" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableWAV)),
                    "允许加载 .wav 音频文件。\n\n" +
                    "注意：WAV 是无压缩格式，文件体积通常是 OGG/MP3 的 10 倍（每首约 30-50 MB）。" +
                    "可能显著增加磁盘占用，且在机械硬盘上切歌时可能出现短暂卡顿。\n\n" +
                    "建议：将 WAV 转换为 OGG 或 MP3 以获得更好的性能。\n\n" +
                    "需要重启游戏生效。" },

                // === Group: Compatibility ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupCompat), "兼容性" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ExtendedRadioStatus)), "ExtendedRadio" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ExtendedRadioStatus)),
                    "显示 ExtendedRadio 的检测状态。\n\n" +
                    "当 ExtendedRadio 已启用时，它已提供自己的多格式音频支持（包括 MP3 和 WAV）。" +
                    "SimpleRadio 会自动禁用自身的格式补丁以避免冲突。" +
                    "上方的格式开关仍控制 SimpleRadio 扫描哪些文件，但音频解码将由 ExtendedRadio 处理。" },
            };
        }

        public void Unload() { }
    }
}
