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
                
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableEconomyFix)), "• Enable Economy Fix & Performance Boost (Master)" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableEconomyFix)), "[Beta] Enable to replace vanilla logic with optimized systems for large cities.\n\n⚠️ Game restart is required after changing this setting!" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableDemandEcoSystem)), "  ├─ RCI Demand Systems" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableDemandEcoSystem)), "Optimize Residential/Commercial/Industrial demand calculation models.\n\n⚠️ Restart required." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)), "  ├─ Job Search Systems" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)), "Optimize citizen job search behavior and matching algorithms.\n\n⚠️ Incompatible with Realistic JobSearch mods!\n⚠️ Restart required." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)), "  ├─ Housing & Rent Systems" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)), "Optimize household property search pathfinding; includes Land Value and Rent recalculation.\n\n⚠️ Restart required." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)), "  ├─ Resource Buyer & Service Coverage" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)), "Optimize citizen shopping and company resource procurement; reduces performance cost of long-distance pathfinding.\n\n⚠️ Incompatible with Realistic PathFinding mods!\n⚠️ Restart required." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)), "  └─ Resident AI Optimization" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)), "Fix resident AI pathfinding wait time logic defects.\n\n⚠️ Incompatible with Realistic PathFinding mods!\n⚠️ Restart required." },

                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionPathfinding), "▍Pathfinding Optimization (Adjustable In-Game)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShoppingMaxCost)), "Max Shopping Pathfind Cost" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShoppingMaxCost)),
                    "Controls the maximum travel cost a citizen is willing to bear for shopping (groceries, dining). Lower values reduce CPU load.\n" +
                    "★ Recommended: 8000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.CompanyShoppingMaxCost)), "Max Company Freight Pathfind Cost" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.CompanyShoppingMaxCost)),
                    "Controls the maximum search range for companies (factories/stores) to find materials and dispatch freight. High values allow map-wide resource search.\n" +
                    "★ Recommended: 200000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LeisureMaxCost)), "Max Leisure Pathfind Cost" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LeisureMaxCost)),
                    "Controls the maximum travel cost for citizens visiting parks, landmarks, or sightseeing. Lower values reduce aimless wandering pathfinding.\n" +
                    "★ Recommended: 8000 ~ 12000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindJobMaxCost)), "Max Find Job Pathfind Cost" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindJobMaxCost)),
                    "Controls how far citizens are willing to travel to find a job. This action is infrequent, recommend setting to maximum.\n" +
                    "★ Recommended: 200000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindHomeMaxCost)), "Max Find Home Pathfind Cost" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindHomeMaxCost)),
                    "Controls the maximum search range when citizens look for a new home. This action is infrequent, recommend setting to maximum.\n" +
                    "★ Recommended: 200000"
                },
            };
        }

        public void Unload() { }
    }
}
