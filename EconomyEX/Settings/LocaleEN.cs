using Colossal;
using System.Collections.Generic;

namespace EconomyEX.Settings
{
    public class LocaleEN : IDictionarySource
    {
        private readonly ModSettings m_Setting;

        public LocaleEN(ModSettings setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "EconomyEX" },
                
                { m_Setting.GetOptionTabLocaleID(ModSettings.kSectionStatus), "Status" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionStatus), "Status" },
                
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.StatusInfo)), "• Module Status" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.StatusInfo)), "Current working status of the economy module." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ConflictWarning)), "• Conflict Warning" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ConflictWarning)), "Detected conflicts that might cause issues." },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kSectionGeneral), "General" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionGeneral), "General" },
                
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableEconomyFix)), "• Enable Economy Fix & Performance Boost" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableEconomyFix)), "Enable to replace vanilla logic with optimized systems for large cities.\n\n⚠️ Game restart is required after changing this setting!\n\n------------------------------------------------\nEconomyEX is an economy system fix and optimization mod. It re-engineers several core vanilla simulation systems to solve severe economic stagnation (e.g. 0 Demand) and debilitating PC performance issues (e.g. Agent Floods) that occur when your city grows into a massive Metropolis." },
            };
        }

        public void Unload() { }
    }
}
