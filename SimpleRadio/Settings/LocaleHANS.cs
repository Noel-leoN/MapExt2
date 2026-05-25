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

                // === Group: Status ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupStatus), "状态" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.StationInfo)), "已加载电台数" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.StationInfo)),
                    "在数据目录中检测到的自定义电台数量。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.SongInfo)), "已加载歌曲数" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.SongInfo)),
                    "所有电台中加载的 OGG 音频文件总数。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DataPath)), "数据目录" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DataPath)),
                    "SimpleRadio 数据目录路径。在此处创建子文件夹并放入 .ogg 文件即可添加自定义电台。" },

                // === Group: Actions ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupActions), "操作" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.OpenDataFolder)), "打开数据目录" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.OpenDataFolder)),
                    "在 Windows 资源管理器中打开 SimpleRadio 数据目录。\n\n" +
                    "添加自定义电台的步骤：\n" +
                    "1. 创建一个子文件夹（文件夹名即为电台名）\n" +
                    "2. 将 .ogg 音频文件放入文件夹\n" +
                    "3. 可选：添加 icon.svg 作为电台图标\n" +
                    "4. 点击「刷新电台」或重启游戏即可生效" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RefreshStations)), "♫ 刷新电台" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshStations)),
                    "重新扫描数据目录并加载所有自定义电台，无需重启游戏。\n\n" +
                    "在数据目录中添加或删除 .ogg 文件后，点击此按钮即可刷新。" },
            };
        }

        public void Unload() { }
    }
}
