using Colossal;
using System.Collections.Generic;

namespace SimpleRadio.Settings
{
    public class LocaleHANS : IDictionarySource
    {
        private readonly SimpleRadioSettings m_Setting;

        public LocaleHANS(SimpleRadioSettings setting)
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
                { m_Setting.GetOptionTabLocaleID(SimpleRadioSettings.kTabInfo), "信息" },
                { m_Setting.GetOptionTabLocaleID(SimpleRadioSettings.kTabFormat), "格式" },

                // === Group: Status ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupStatus), "状态" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.StationInfo)), "已加载电台数" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.StationInfo)),
                    "从数据目录成功加载的自定义电台数量。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.SongInfo)), "已加载歌曲数" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.SongInfo)),
                    "所有电台中加载的音频文件总数。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.DataPath)), "数据目录" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.DataPath)),
                    "SimpleRadio 数据目录路径。这里的每个子文件夹是一个电台，音频文件（.ogg、.mp3、.wav）须直接放在电台文件夹里，其中的子文件夹不会被扫描。" },

                // === Group: Actions ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupActions), "操作" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.OpenDataFolder)), "打开数据目录" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.OpenDataFolder)),
                    "在 Windows 资源管理器中打开 SimpleRadio 数据目录。\n\n" +
                    "添加自定义电台的步骤：\n" +
                    "1. 创建一个子文件夹（文件夹名即为电台名）\n" +
                    "2. 将音频文件（.ogg、.mp3、.wav）直接放入该文件夹，其中的子文件夹不会被扫描\n" +
                    "3. 可选：添加 icon.svg 作为电台图标，或添加 .m3u8 播放列表指定顺序模式的播放顺序\n" +
                    "4. 点击「刷新电台」或重启游戏即可生效" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.RefreshStations)), "♫ 刷新电台" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.RefreshStations)),
                    "重新扫描数据目录并加载所有自定义电台，无需重启游戏。\n\n" +
                    "添加、删除音频文件或修改播放列表后，点击此按钮即可刷新。" },

                // === 播放設定 ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupPlayback), "播放" },
                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.RestoreLastStation)), "恢复上次使用的电台" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.RestoreLastStation)),
                    "延续上次使用的电台，跨城市共用。关闭后，各城市使用存档记录的电台。" +
                    "下次进入城市时生效。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.PlaylistTip)),
                    "提示：顺序模式下，在电台文件夹放一个 .m3u8 播放列表即可自定义播放顺序，改完点击「刷新电台」。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.PlaybackOrder)), "播放顺序" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.PlaybackOrder)),
                    "自定义电台的歌曲播放顺序。\n\n" +
                    "随机：每轮每首歌各播一次，顺序随机；新一轮开头不会是上一轮最后播的几首。\n\n" +
                    "顺序：按文件名顺序播放（与资源管理器的排序一致），播完从头循环。" +
                    "电台文件夹里有 .m3u 或 .m3u8 播放列表（UTF-8 编码）时，先按列表顺序，列表没列到的文件按文件名排在后面。" +
                    "改了播放列表后点击「刷新电台」生效。\n\n" +
                    "切换后，正在播放和已排进队列的歌会先播完，之后按新顺序继续。" },
                { m_Setting.GetEnumValueLocaleID(PlaybackOrder.Shuffle), "随机" },
                { m_Setting.GetEnumValueLocaleID(PlaybackOrder.Sequential), "顺序" },

                // === Group: Formats ===
                { m_Setting.GetOptionGroupLocaleID(SimpleRadioSettings.kGroupFormats), "音频格式" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.EnableMP3)), "启用 MP3 支持" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.EnableMP3)),
                    "允许加载 .mp3 音频文件。\n\n" +
                    "MP3 格式广泛支持，运行稳定可靠。点击「刷新电台」即可生效。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleRadioSettings.EnableWAV)), "启用 WAV 支持" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleRadioSettings.EnableWAV)),
                    "允许加载 .wav 音频文件。\n\n" +
                    "注意：WAV 是无压缩格式，文件体积通常是 OGG/MP3 的 10 倍（每首约 30-50 MB）。" +
                    "可能显著增加磁盘占用，且在机械硬盘上切歌时可能出现短暂卡顿。\n\n" +
                    "建议：将 WAV 转换为 OGG 或 MP3 以获得更好的性能。\n\n" +
                    "点击「刷新电台」即可生效。" },
            };
        }

        public void Unload() { }
    }
}
