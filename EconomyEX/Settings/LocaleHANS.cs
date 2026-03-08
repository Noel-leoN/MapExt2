using Colossal;
using System.Collections.Generic;

namespace EconomyEX.Settings
{
    public class LocaleHANS : IDictionarySource
    {
        private readonly ModSettings m_Setting;

        public LocaleHANS(ModSettings setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "EconomyEX 经济扩展" },
                
                { m_Setting.GetOptionTabLocaleID(ModSettings.kSectionStatus), "运行状态" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionStatus), "运行状态" },
                
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.StatusInfo)), "• 模块状态" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.StatusInfo)), "当前经济模块的工作状态。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ConflictWarning)), "• 冲突警告" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ConflictWarning)), "检测到的可能导致错误的冲突信息。" },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kSectionGeneral), "通用设置" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionGeneral), "通用设置" },
                
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableEconomyFix)), "• 启用经济修复与性能优化" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableEconomyFix)), "开启此项将替换原版的部分逻辑以修复大城市经济和性能问题。\n\n⚠️ 【重要】：更改此项设置后，【必须重启游戏】，否则不会生效并且会引发不可预知的 Bug！" },
            };
        }

        public void Unload() { }
    }
}
