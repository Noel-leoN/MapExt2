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

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.SystemStatusReport)), "• System Status" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.SystemStatusReport)), "Real-time status of economy system replacement pairs." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RefreshStatus)), "↻ Refresh Status" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshStatus)), "Click to manually refresh conflict detection and system status display." },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kSectionGeneral), "General" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionGeneral), "General" },
                
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableEconomyFix)), "• Enable Economy Fix and Performance Boost (Master)" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableEconomyFix)), "[Beta] Enable to replace vanilla logic with optimized systems for large cities.\n\n⚠️ Game restart is required after changing this setting!" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableDemandEcoSystem)), "  ├─ RCI Demand Systems" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableDemandEcoSystem)), "Optimize Residential/Commercial/Industrial demand calculation models.\n\n⚠️ Restart required." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)), "  ├─ Job Search Systems" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)), "Optimize citizen job search behavior and matching algorithms.\n\n⚠️ Incompatible with Realistic JobSearch mods!\n⚠️ Restart required." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)), "  ├─ Housing and Rent Systems" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)), "Optimize household property search pathfinding; includes Land Value and Rent recalculation.\n\n⚠️ Restart required." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)), "  ├─ Resource Buyer and Service Coverage" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)), "Optimize citizen shopping and company resource procurement; reduces performance cost of long-distance pathfinding.\n\n⚠️ Incompatible with Realistic PathFinding mods!\n⚠️ Restart required.\n\n⚠️ Default: OFF to avoid conflicts with popular pathfinding mods." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)), "  └─ Resident AI Optimization" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)), "Fix resident AI pathfinding wait time logic defects.\n\n⚠️ Incompatible with Realistic PathFinding mods!\n⚠️ Restart required.\n\n⚠️ Default: OFF to avoid conflicts with popular pathfinding mods." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetEcoSystemToggles)), "Reset" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ResetEcoSystemToggles)), "Reset all economy system toggles to their default values." },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetEcoSystemToggles)), "Are you sure you want to reset all economy system toggles to defaults?" },

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
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EmergencyMaxCost)), "Max Hospital/Crime Pathfind Cost" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EmergencyMaxCost)),
                    "Controls the maximum search range for citizens seeking hospitals (when sick/injured) or committing crimes. A lower value restricts these activities to nearby areas, encouraging locally planned services.\n" +
                    "★ Tip: Build hospitals and police stations within this cost radius of residential areas. If your facilities are very close, you can further reduce this value.\n" +
                    "★ Recommended: 4000 ~ 8000 (Default: 6000)"
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

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolElementaryMaxCost)), "Max Elementary School Pathfind Cost" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolElementaryMaxCost)),
                    "Controls the maximum search range when Elementary students look for a school. Lowering this value forces them to enroll in nearby local schools only.\n" +
                    "★ Recommended: 10000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolHighSchoolMaxCost)), "Max High School Pathfind Cost" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolHighSchoolMaxCost)),
                    "Controls the maximum search range when High School students look for a school.\n" +
                    "★ Recommended: 17000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolCollegeMaxCost)), "Max College Pathfind Cost" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolCollegeMaxCost)),
                    "Controls the maximum search distance when connecting to a College.\n" +
                    "★ Recommended: 50000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolUniversityMaxCost)), "Max University Pathfind Cost" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolUniversityMaxCost)),
                    "Controls the maximum search range for Universities. If there is only one University center on the map, it is recommended to max this out to cover everyone.\n" +
                    "★ Recommended: 100000 ~ 200000"
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetPathfinding)), "Reset" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ResetPathfinding)), "Reset all pathfinding cost limits to their default values." },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetPathfinding)), "Are you sure you want to reset all pathfinding parameters to defaults?" },

                // --- Behavior Section ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionBehavior), "Economy Behavior and Throughput (Adjustable In-Game)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.JobSeekerCap)), "Job Search: Seeker Throughput" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.JobSeekerCap)), "Maximum job seeker entities created per system update. Increase for larger populations." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.PathfindRequestCap)), "Job Search: Pathfind Throughput" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.PathfindRequestCap)), "Maximum pathfinding requests processed per update. Typically 2~4x the Seeker Throughput." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShoppingTrafficReduction)), "Shopping Traffic Reduction Factor (x0.0001)" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShoppingTrafficReduction)), "Controls how much city population suppresses per-household shopping probability. Higher values = faster decay. Default: 4 (= 0.0004)." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HouseholdResourceDemandMultiplier)), "Household Resource Demand Multiplier" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HouseholdResourceDemandMultiplier)), "Multiplier for resource purchase amount per shopping trip. Default: 3.5." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HomeSeekerCap)), "Home Search: Move Throughput" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HomeSeekerCap)), "Maximum households with existing homes processed per frame for relocation evaluation. Default: 128." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HomelessSeekerCap)), "Home Search: Homeless Throughput" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HomelessSeekerCap)), "Maximum homeless households processed per frame for housing placement. Default: 1280." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetEcoBehavior)), "Reset" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ResetEcoBehavior)), "Reset all economy behavior and throughput parameters to their default values." },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetEcoBehavior)), "Are you sure you want to reset all economy behavior parameters to defaults?" },

                // ============================================================
                // Tab: Perf. Tools
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kSectionPerfTool), "Perf. Tools" },

                // --- Group: NoDogs ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kNoDogsGroup), "NoDogs Control" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoDogsOnStreet)), "NoDogs: Disable OnStreet" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoDogsOnStreet)),
                    "Prevents pets from appearing on streets (disables spawning, rendering and pathfinding). Logical pet entities still exist in memory."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoDogsGeneration)),
                    "NoDogs: Prevent New Generation"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoDogsGeneration)),
                    "Blocks new household pet generation by zeroing spawn probability. Existing pets remain, but no new ones will be created for newly moved-in households."
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoDogsPurge)), "⚠ NoDogs: Purge All Existing" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoDogsPurge)),
                    "⚠ WARNING: Removes ALL existing pet entities from the save for maximum performance gain. After purging, existing households will NOT re-acquire pets. Only newly moved-in households will bring dogs (if generation is not blocked)."
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ApplyNoDogs)), "► Apply NoDogs Settings" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ApplyNoDogs)),
                    "Click to apply the above NoDogs checkbox selections. Changes will NOT take effect until this button is pressed."
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ApplyNoDogs)),
                    "Apply NoDogs settings now? If 'Purge All Existing' is checked, all pets will be permanently removed from your save!"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DislayPetCount)), "Logical Pets Count" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DislayPetCount)),
                    "Count of logical pet entities currently existing on the map."
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RefreshPetCount)), "Refresh Pet Count" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshPetCount)),
                    "Click to recalculate the count of active pet entities. This is just for statistics and does not affect the game state."
                },

                // --- Group: No Through-Traffic ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kNoTrafficGroup), "Traffic Control" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoThroughTraffic)), "No Through-Traffic" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoThroughTraffic)),
                    "Disable Through-Traffic to reduce pathfinding calculation and traffic congestion. It'll take effect after the game has been running for a while."
                },
            };
        }

        public void Unload() { }
    }
}
