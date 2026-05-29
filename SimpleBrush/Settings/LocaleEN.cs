using Colossal;
using System.Collections.Generic;

namespace SimpleBrush.Settings
{
    /// <summary>
    /// SimpleBrush 英文本地化字典。
    /// </summary>
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
                { m_Setting.GetSettingsLocaleID(), "SimpleBrush" },

                // === Group: Infinite Resources ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupInfinite), "Infinite Resources" },

                // === InfiniteFertility ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.InfiniteFertility)), "Infinite Fertility" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.InfiniteFertility)),
                    "When enabled, farmland fertility will never deplete from industrial usage." },

                // === InfiniteOre ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.InfiniteOre)), "Infinite Ore" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.InfiniteOre)),
                    "When enabled, ore deposits will never deplete from mining operations." },

                // === InfiniteOil ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.InfiniteOil)), "Infinite Oil" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.InfiniteOil)),
                    "When enabled, oil deposits will never deplete from extraction." },

                // === InfiniteFish ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.InfiniteFish)), "Infinite Fish" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.InfiniteFish)),
                    "When enabled, fishery resources will never deplete from harvesting." },

                // === Group: Restore Resources ===
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGroupRestore), "Restore Depleted Resources" },

                // === RestoreFertility ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreFertility)), "Restore Fertility" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreFertility)),
                    "Reset all used fertility to zero, fully restoring fertile land." },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreFertility)),
                    "Are you sure? This will reset all depleted fertility on the map." },

                // === RestoreOre ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreOre)), "Restore Ore" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreOre)),
                    "Reset all used ore deposits to zero, fully restoring ore reserves." },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreOre)),
                    "Are you sure? This will reset all depleted ore on the map." },

                // === RestoreOil ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreOil)), "Restore Oil" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreOil)),
                    "Reset all used oil deposits to zero, fully restoring oil reserves." },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreOil)),
                    "Are you sure? This will reset all depleted oil on the map." },

                // === RestoreFish ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreFish)), "Restore Fish" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreFish)),
                    "Reset all fish usage to zero, fully restoring fishery resources." },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreFish)),
                    "Are you sure? This will reset all depleted fish on the map." },

                // === RestoreAll ===
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RestoreAll)), ">> Restore All Resources" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RestoreAll)),
                    "Reset all depleted natural resources (fertility, ore, oil, fish) in one click." },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.RestoreAll)),
                    "Are you sure? This will reset ALL depleted natural resources on the entire map." },
            };
        }

        public void Unload() { }
    }
}
