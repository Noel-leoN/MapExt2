using Colossal;
using System.Collections.Generic;

namespace SimpleBrush.Settings
{
    /// <summary>
    /// SimpleBrush 繁體中文本地化字典。
    /// </summary>
    public class LocaleHANT : IDictionarySource
    {
        private readonly ModSettings m_Setting;

        public LocaleHANT(ModSettings setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                // === Mod 名稱 ===
                { m_Setting.GetSettingsLocaleID(), "SimpleBrush 極簡資源筆刷" },

                // === Group: 無限資源 ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupInfinite), "無限資源" },

                // === InfiniteFertility ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.InfiniteFertility)), "無限肥沃土地" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.InfiniteFertility)),
                    "開啟後，農業用地的肥沃度將永遠不會因產業開發而耗盡。" },

                // === InfiniteOre ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.InfiniteOre)), "無限礦石" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.InfiniteOre)),
                    "開啟後，礦石蘊藏量將永遠不會因採礦作業而耗盡。" },

                // === InfiniteOil ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.InfiniteOil)), "無限石油" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.InfiniteOil)),
                    "開啟後，石油蘊藏量將永遠不會因開採作業而耗盡。" },

                // === InfiniteFish ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.InfiniteFish)), "無限魚類" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.InfiniteFish)),
                    "開啟後，漁業資源將永遠不會因捕撈作業而耗盡。" },

                // === Group: 恢復耗盡資源 ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupRestore), "恢復已耗盡的資源" },

                // === RestoreFertility ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreFertility)), "恢復已耗盡的肥沃土地" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreFertility)),
                    "立即將地圖中所有已被消耗的肥沃土地資源恢復至全滿。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreFertility)),
                    "確定要恢復嗎？這將恢復全地圖已消耗的肥沃土地資源。" },

                // === RestoreOre ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreOre)), "恢復已耗盡的礦石" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreOre)),
                    "立即清除已被工業開採的礦石消耗量，完全補滿現有礦區的蘊藏量。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreOre)),
                    "確定要恢復嗎？這將恢復全地圖已消耗的礦石資源。" },

                // === RestoreOil ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreOil)), "恢復已耗盡的石油" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreOil)),
                    "立即清除已被工業抽取的石油消耗量，完全補滿現有油田的蘊藏量。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreOil)),
                    "確定要恢復嗎？這將恢復全地圖已消耗的石油資源。" },

                // === RestoreFish ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreFish)), "恢復已捕撈的魚類" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreFish)),
                    "立即清除所有水域的魚類消耗量，完全補滿漁業資源的儲備量。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreFish)),
                    "確定要恢復嗎？這將恢復全地圖已消耗的魚類資源。" },

                // === RestoreAll ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreAll)), ">> 一鍵恢復全部自然資源" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreAll)),
                    "一鍵補滿地圖中所有的肥沃土地、礦石、石油與魚類資源的消耗量。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreAll)),
                    "確定要全部恢復嗎？這會立即補滿地圖中所有已損耗的自然資源與蘊藏量。" },
            };
        }

        public void Unload() { }
    }
}
