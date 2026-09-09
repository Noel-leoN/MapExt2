using Colossal;
using System.Collections.Generic;

namespace SimpleBrush.Settings
{
    /// <summary>
    /// SimpleBrush 英文本地化字典。
    /// </summary>
    public class LocaleEN : IDictionarySource
    {
        private readonly SimpleBrushSettings m_Setting;

        public LocaleEN(SimpleBrushSettings setting)
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
                { m_Setting.GetSettingsLocaleID(), "SimpleBrush" },

                // === Group: Infinite Resources ===
                { m_Setting.GetOptionGroupLocaleID(SimpleBrushSettings.kGroupInfinite), "Infinite Resources" },

                // === InfiniteFertility ===
                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleBrushSettings.InfiniteFertility)), "Infinite Fertility" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleBrushSettings.InfiniteFertility)),
                    "When enabled, farmland fertility will never deplete from industrial usage." },

                // === InfiniteOre ===
                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleBrushSettings.InfiniteOre)), "Infinite Ore" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleBrushSettings.InfiniteOre)),
                    "When enabled, ore deposits will never deplete from mining operations." },

                // === InfiniteOil ===
                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleBrushSettings.InfiniteOil)), "Infinite Oil" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleBrushSettings.InfiniteOil)),
                    "When enabled, oil deposits will never deplete from extraction." },

                // === InfiniteFish ===
                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleBrushSettings.InfiniteFish)), "Infinite Fish" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleBrushSettings.InfiniteFish)),
                    "When enabled, fishery resources will never deplete from harvesting." },

                // === Group: Restore Resources ===
                { m_Setting.GetOptionGroupLocaleID(SimpleBrushSettings.kGroupRestore), "Restore Depleted Resources" },

                // === RestoreFertility ===
                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleBrushSettings.RestoreFertility)), "Restore Fertility" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleBrushSettings.RestoreFertility)),
                    "Reset all used fertility to zero, fully restoring fertile land." },
                { m_Setting.GetOptionWarningLocaleID(nameof(SimpleBrushSettings.RestoreFertility)),
                    "Are you sure? This will reset all depleted fertility on the map." },

                // === RestoreOre ===
                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleBrushSettings.RestoreOre)), "Restore Ore" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleBrushSettings.RestoreOre)),
                    "Reset all used ore deposits to zero, fully restoring ore reserves." },
                { m_Setting.GetOptionWarningLocaleID(nameof(SimpleBrushSettings.RestoreOre)),
                    "Are you sure? This will reset all depleted ore on the map." },

                // === RestoreOil ===
                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleBrushSettings.RestoreOil)), "Restore Oil" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleBrushSettings.RestoreOil)),
                    "Reset all used oil deposits to zero, fully restoring oil reserves." },
                { m_Setting.GetOptionWarningLocaleID(nameof(SimpleBrushSettings.RestoreOil)),
                    "Are you sure? This will reset all depleted oil on the map." },

                // === RestoreFish ===
                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleBrushSettings.RestoreFish)), "Restore Fish" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleBrushSettings.RestoreFish)),
                    "Reset all fish usage to zero, fully restoring fishery resources." },
                { m_Setting.GetOptionWarningLocaleID(nameof(SimpleBrushSettings.RestoreFish)),
                    "Are you sure? This will reset all depleted fish on the map." },

                // === RestoreAll ===
                { m_Setting.GetOptionLabelLocaleID(nameof(SimpleBrushSettings.RestoreAll)), ">> Restore All Resources" },
                { m_Setting.GetOptionDescLocaleID(nameof(SimpleBrushSettings.RestoreAll)),
                    "Reset all depleted natural resources (fertility, ore, oil, fish) in one click." },
                { m_Setting.GetOptionWarningLocaleID(nameof(SimpleBrushSettings.RestoreAll)),
                    "Are you sure? This will reset ALL depleted natural resources on the entire map." },
            };
        }

        public void Unload() { }
    }
}
