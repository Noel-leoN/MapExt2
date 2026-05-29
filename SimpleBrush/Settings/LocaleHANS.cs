using Colossal;
using System.Collections.Generic;

namespace SimpleBrush.Settings
{
    /// <summary>
    /// SimpleBrush 简体中文本地化字典。
    /// </summary>
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
                { m_Setting.GetSettingsLocaleID(), "SimpleBrush 极简资源笔刷" },

                // === Group: 无限资源 ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupInfinite), "无限资源" },

                // === InfiniteFertility ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.InfiniteFertility)), "无限肥沃土地" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.InfiniteFertility)),
                    "开启后，农业用地的肥沃度将永远不会因工业开采而耗尽。" },

                // === InfiniteOre ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.InfiniteOre)), "无限矿石" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.InfiniteOre)),
                    "开启后，矿石储量将永远不会因采矿作业而耗尽。" },

                // === InfiniteOil ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.InfiniteOil)), "无限石油" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.InfiniteOil)),
                    "开启后，石油储量将永远不会因抽取作业而耗尽。" },

                // === InfiniteFish ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.InfiniteFish)), "无限鱼类" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.InfiniteFish)),
                    "开启后，渔业资源将永远不会因捕捞作业而耗尽。" },

                // === Group: 恢复耗尽资源 ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupRestore), "恢复耗尽资源" },

                // === RestoreFertility ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreFertility)), "重置已耗尽的肥沃土地" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreFertility)),
                    "立即将地图上所有已被消耗的肥沃土地资源重置为满值。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreFertility)),
                    "确定要恢复吗？这将重置全图被消耗的肥沃土地资源。" },

                // === RestoreOre ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreOre)), "重置已耗尽的矿石" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreOre)),
                    "立即重置已被工业开采的矿石消耗值，完全补满现有矿区储备。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreOre)),
                    "确定要恢复吗？这将重置全图被消耗的矿石资源。" },

                // === RestoreOil ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreOil)), "重置已耗尽的石油" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreOil)),
                    "立即重置已被工业抽取的石油消耗值，完全补满现有油田储备。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreOil)),
                    "确定要恢复吗？这将重置全图被消耗的石油资源。" },

                // === RestoreFish ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreFish)), "重置已捕捞的鱼类" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreFish)),
                    "立即重置所有水域的鱼类消耗值，完全补满渔业资源储备。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreFish)),
                    "确定要恢复吗？这将重置全图被消耗的鱼类资源。" },

                // === RestoreAll ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreAll)), ">> 一键恢复全部自然资源" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreAll)),
                    "一键补满地图上所有的肥沃土地、矿石、石油和鱼类资源消耗。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreAll)),
                    "确定要全部恢复吗？这会立即补满地图上所有已损耗的自然资源储备。" },
            };
        }

        public void Unload() { }
    }
}
