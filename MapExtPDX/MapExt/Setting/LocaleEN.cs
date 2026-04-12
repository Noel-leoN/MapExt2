// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// --- LOCALE ---

using Colossal;
using System.Collections.Generic;

namespace MapExtPDX
{
    public class LocaleEN : IDictionarySource
    {
        private readonly ModSettings m_Setting;

        public LocaleEN(ModSettings setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            var entries = new Dictionary<string, string>
            {
                // ============================================================
                // Mod Title
                // ============================================================
                { m_Setting.GetSettingsLocaleID(), "#MapExt" },

                // ============================================================
                // Tab 1: MapSize
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kMapSizeModeTab), "MapSize" },

                // --- Group: Main MapSize Mode ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kMainModeGroup), "Main MapSize Mode" },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.PatchModeChoice)), "► Select MapSize Mode"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.PatchModeChoice)),
                    "⚠️ Changes require clicking the 'Apply Changes' button to take effect!\n\nMode List:\n - ModeA: 57km (4x4)  DEM:14m\n - ModeB: 28km (2x2)  DEM:7m\n - ModeC: 114km (8x8)  DEM:28m\n - Vanilla: 14km Vanilla(1x1) DEM:3.5m\n\nNotice:\n1. Larger MapSizes suffer from lower DEM resolution, resulting in rougher graphics for mountains, ramps, and waterfronts. If you have high requirements for terrain smoothness, it is recommended to build a flatter map or use landscaping tools to mask areas.\n2. The larger the MapSize, the greater the floating-point calculation deviation of the economic simulation data at the edges (causing false visuals). This is an unfixable engine limitation, especially noticeable in 114km mode. It is recommended to build active zones (Res/Com/Ind) in the center of the map.\n\n! [CRITICAL]: You MUST RESTART THE GAME after changing the map size mode before loading a save. Failure to do so will cause logic conflicts and may corrupt your save data!"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ModSettingCoreValue)),
                    "• Current Applied MapSize"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ModSettingCoreValue)),
                    "The currently selected and successfully applied map size. This size refers to the edge length of the map. Unit is in meters.\n⚠️ Warning: Although MapExt has loadgame validation to prevent loading wrong size game saves, Please BACKUP ALL of your GameSaves (Strongly recommand SKYVE) before Loading them with this mod!!! Otherwise, there is a risk that the save may be corrupted due to game crashes or other special reasons."
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ApplyPatchChanges)), "► Apply Changes" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ApplyPatchChanges)),
                    "Applies the selected patch mode.\n\n⚠️ [CRITICAL]: This Mod does not support hot-switching. You MUST RESTART THE GAME after applying changes and before loading any save. Loading a save directly will cause simulation corruption!"
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ApplyPatchChanges)),
                    "Applying MapSize Mode, please be patient until completion.\n\n⚠️ Please completely RESTART THE GAME after completion. Do not load any save immediately!"
                },

                // --- Group: Terrain-Water Optimization (Beta) ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kTerrainWaterOptGroup), "Terrain-Water Performance Optimization (Beta)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.TerrainBufferPrealloc)), "Terrain Buffer Pre-allocation" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.TerrainBufferPrealloc)),
                    "Pre-allocates larger GPU StructuredBuffers on the first frame based on map scale multiplier, " +
                    "preventing runtime buffer reallocation stutter when many buildings/roads are visible.\n\n" +
                    "★ Recommended: ON for all large maps. No visual side effects."
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.TerrainCascadeThrottle)), "⚠ Terrain Cascade Throttle (Experimental)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.TerrainCascadeThrottle)),
                    "Reduces GPU load by updating distant terrain cascade layers every 4 frames instead of every frame.\n\n" +
                    "⚠ WARNING: May cause visible terrain offset/misalignment when moving the camera, " +
                    "because cascade viewport ranges update every frame but rendering is throttled.\n\n" +
                    "★ Recommended: OFF unless you experience severe GPU bottleneck on very large maps."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.WaterSimQuality)),
                    "► Water Simulation Quality"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.WaterSimQuality)),
                    "Controls the CPU/GPU update frequency of the water system to improve frame rates on large maps.\n\n" +
                    " - Vanilla (Every Frame): Simulates every frame, highest quality but maximum performance cost.\n" +
                    " - Reduced (No Backdrop): Simulates every frame, but disables flow calculation in the distant backdrop.\n" +
                    " - Minimal (Every 4 Frames): Skips simulation for 3 out of 4 frames and disables visual blur. Massively reduces GPU requests, with only slightly noticeable water stuttering.\n" +
                    " - Paused (No Flow): Completely freezes water flow calculations (water will remain static).\n\n" +
                    "★ Tip: Applies instantly, no restart required."
                },

                // (hidden items - kept for serialization)
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.TerrainResolution)), "Terrain Resolution" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.TerrainResolution)),
                    "Terrain heightmap resolution for new maps. 8192 provides sharper terrain editing and rendering (especially noticeable with terrain brushes). " +
                    "Existing saves will keep their original resolution.\n" +
                    "⚠️ Restart required after change."
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.WaterResolution)), "Water Simulation Resolution" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.WaterResolution)),
                    "Water simulation texture size. Lower values greatly reduce GPU/VRAM usage with minimal visual impact. " +
                    "512 or 256 recommended for large maps.\n" +
                    "⚠️ Changing this will reset water surfaces when loading old saves (rivers/lakes refill from sources).\n" +
                    "⚠️ Restart required after change."
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.VRAMEstimate)), "Estimated Terrain/Water VRAM" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.VRAMEstimate)),
                    "Approximate GPU memory usage for terrain cascade and water simulation textures at the selected resolutions."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.WaterTextureFormat)),
                    "Water Texture Precision (VRAM Opt)"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.WaterTextureFormat)),
                    "Forces the 32-bit float simulation textures to 16-bit, saving up to 43% of VRAM and theoretically halving the bandwidth overhead.\n\n" +
                    " - High (32-bit HDR): Lossless precision, consumes ~180MB VRAM.\n" +
                    " - Low (16-bit): Lossy precision, consumes ~105MB VRAM. Minor rippling artifacts might appear when depth exceeds 100 meters due to floating point truncation.\n\n" +
                    "⚠️ Restart Required."
                },

                // --- Group: Economy Overhaul ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kEcoGroup), "Economy Overhaul" },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.isEnableEconomyFix)),
                    "• Economy Logic and Perf. Optimization (Beta Master Switch)"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.isEnableEconomyFix)),
                    "(This patch is currently in the testing phase (Beta))\nFixes and optimizes the following systems to adapt to cities with populations in the millions:\n - Residential/Commercial/Industrial demand systems\n - Household home-search system\n - Household behavior system (consumer behavior adjustment)\n - Citizen job-search system\n - Rent calculation system\n - Resource procurement and service coverage pathfinding systems\n - Resident AI pathfinding optimization patch\n\n⚠️ [CRITICAL]: Changing this option requires a GAME RESTART to take effect safely. Otherwise, severe logical bugs will occur!"
                },

                // --- Group: Caution ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kNoteGroup), "Caution!" },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ModeChangeWarningMessage)),
                    "⚠️ Please completely RESTART THE GAME after applying ANY of the above settings!"
                },

                // ============================================================
                // Tab 2: EconomyEX
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kMiscTab), "EconomyEX" },

                // --- Group: Economy System Toggles ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kEcoSystemEnableGroup), "Economy System Toggles" },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableDemandEcoSystem)),
                    "├─ RCI Demand Systems"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableDemandEcoSystem)),
                    "Optimizes Residential, Commercial, and Industrial demand calculation models for a smoother experience.\n\n⚠️ Restart Required."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)),
                    "├─ Job Search Systems"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)),
                    "Optimizes citizen job-search behavior and matching algorithms to improve efficiency.\n\n⚠️ Incompatible with Realistic JobSearch and similar mods!\n⚠️ Restart Required."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)),
                    "├─ Household, Property and Rent Systems"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)),
                    "Optimizes household home-searching pathfinding; includes a realistic Land Value remake to make location values more reasonable; and heavily refactors the expensive rent adjustment mechanism.\n\n⚠️ Restart Required."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)),
                    "├─ Consumer and Service Pathing Systems"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)),
                    "Optimizes resource matching for citizen shopping and company restocking, greatly reducing performance overhead from extreme-distance pathfinding.\n\n⚠️ Incompatible with Realistic PathFinding and similar mods!\n⚠️ Restart Required."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)),
                    "└─ Resident AI Pathing System"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)),
                    "Fixes citizen pathfinding AI wait time logic flaws and mitigates critical memory overflows caused by large map path calculations.\n\n⚠️ Incompatible with Realistic PathFinding and similar mods!\n⚠️ Restart Required."
                },

                // --- Group: Pathfinding Cost Limits ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kPathfindingGroup), "Pathfinding Cost Limits (Adjustable In-Game)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShoppingMaxCost)), "Max Shopping Pathfind Cost" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShoppingMaxCost)),
                    "Controls the maximum travel cost citizens are willing to endure for shopping (groceries, dining). A lower value makes citizens give up faster when shops are far, significantly reducing CPU load on large maps.\n" +
                    "★ Recommended values:\n" +
                    " - 14km / 28km: 8000\n" +
                    " - 57km / 114km: 8000 ~ 12000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.CompanyShoppingMaxCost)),
                    "Company Max Delivery Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.CompanyShoppingMaxCost)),
                    "Controls the maximum search distance when companies (factories/stores) attempt to restock materials via cargo delivery. A higher value (up to 200k) allows companies to search across the entire map, preventing extreme material shortages on large maps.\n" +
                    "★ Recommended values:\n" +
                    " - All Map Sizes: 200000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LeisureMaxCost)),
                    "Max Leisure/Sightseeing Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LeisureMaxCost)),
                    "Controls the maximum travel cost citizens are willing to endure to visit parks, landmarks, or sightseeing. A lower value prevents excessive pathfinding for aimless wandering.\n" +
                    "★ Recommended values:\n" +
                    " - 14km / 28km: 8000 ~ 12000\n" +
                    " - 57km / 114km: 12000 ~ 20000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EmergencyMaxCost)),
                    "Max Hospital/Crime Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EmergencyMaxCost)),
                    "Controls the maximum search range for citizens seeking hospitals (when sick/injured) or committing crimes. A lower value restricts these activities to nearby areas, encouraging locally planned services.\n" +
                    "★ Tip: Build hospitals and police stations within this cost radius of residential areas. If your facilities are very close, you can further reduce this value.\n" +
                    "★ Recommended values:\n" +
                    " - All Map Sizes: 4000 ~ 8000 (Default: 6000)"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindJobMaxCost)), "Max Find Job Pathfind Cost" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindJobMaxCost)),
                    "Controls how far citizens are willing to search across the map for a job. A higher value (up to 200k) helps isolated towns on large maps finding workers. This occurs very rarely, so it's recommended to max it out (minimal performance impact).\n" +
                    "★ Recommended:\n" +
                    " - All Map Sizes: 200000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindHomeMaxCost)), "Max Find Home Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindHomeMaxCost)),
                    "Controls the maximum search distance when citizens look for a new home. Increasing this allows citizens to relocate across the entire large map, preventing remote towns from being empty. This occurs rarely, recommended to max it out.\n" +
                    "★ Recommended:\n" +
                    " - All Map Sizes: 200000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolElementaryMaxCost)),
                    "Max Elementary School Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolElementaryMaxCost)),
                    "Controls the maximum search range when Elementary students look for a school. Lowering this value forces them to enroll in nearby local schools only.\n" +
                    "★ Recommended:\n" +
                    " - All Map Sizes: 10000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolHighSchoolMaxCost)),
                    "Max High School Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolHighSchoolMaxCost)),
                    "Controls the maximum search range when High School students look for a school.\n" +
                    "★ Recommended:\n" +
                    " - All Map Sizes: 17000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolCollegeMaxCost)),
                    "Max College Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolCollegeMaxCost)),
                    "Controls the maximum search distance when connecting to a College.\n" +
                    "★ Recommended:\n" +
                    " - All Map Sizes: 50000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolUniversityMaxCost)),
                    "Max University Pathfind Cost"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolUniversityMaxCost)),
                    "Controls the maximum search range for Universities. If there is only one University center on a massive map, it is recommended to max this out to cover everyone.\n" +
                    "★ Recommended:\n" +
                    " - All Map Sizes: 100000 ~ 200000"
                },

                // --- Group: Economy Behavior and Throughput ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kEcoBehaviorGroup), "Economy Behavior and Throughput (Adjustable In-Game)" },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.JobSeekerCap)), "Job Search: Seeker Throughput"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.JobSeekerCap)),
                    "Maximum job seeker entities created per system update. Increase for larger populations.\n" +
                    "Higher values speed up employment matching but increase CPU load. Can be adjusted in-game.\n" +
                    "★ Recommended:\n" +
                    " - Under 500k pop: 200 ~ 500\n" +
                    " - 2M pop: 500 ~ 1000\n" +
                    " - Over 5M pop: 1000 ~ 3000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.PathfindRequestCap)),
                    "Job Search: Pathfind Throughput"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.PathfindRequestCap)),
                    "Maximum pathfinding requests processed per update. Typically 2~4x the Seeker Throughput.\n" +
                    "Higher values speed up job matching but increase pathfinding CPU load. Can be adjusted in-game.\n" +
                    "★ Recommended:\n" +
                    " - Under 500k pop: 1000 ~ 2000\n" +
                    " - 2M pop: 2000 ~ 4000\n" +
                    " - Over 5M pop: 4000 ~ 8000"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShoppingTrafficReduction)),
                    "Shopping Traffic Reduction Factor"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShoppingTrafficReduction)),
                    "Controls how much city population suppresses per-household shopping probability. " +
                    "Formula: shopChance = 200 / sqrt(factor × population).\n" +
                    "Higher values mean faster probability decay as population grows, reducing commercial trade volume.\n\n" +
                    "★ Effect at different population scales (at default 0.0004):\n" +
                    " - 10k pop: shopChance ≈ 100% → almost every household shops\n" +
                    " - 100k pop: shopChance ≈ 32% → one-third of households shop\n" +
                    " - 1M pop: shopChance ≈ 10% → one-tenth of households shop\n" +
                    " - 5M pop: shopChance ≈ 4% → very few households shop\n\n" +
                    "★ Recommended by population scale:\n" +
                    " - Under 100k (small city): 0.0004 (default, same as vanilla)\n" +
                    " - 100k~500k (medium): 0.0003 ~ 0.0004\n" +
                    " - 500k~2M (large): 0.0002 ~ 0.0003\n" +
                    " - Over 2M (mega city): 0.0001 ~ 0.0002\n\n" +
                    "Can be adjusted in-game. Lower values encourage consumption and boost commercial revenue."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HouseholdResourceDemandMultiplier)),
                    "Household Resource Demand Multiplier"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HouseholdResourceDemandMultiplier)),
                    "Multiplier for resource purchase amount per shopping trip. Since the mod reduces shopping frequency " +
                    "(tick rate is 1/2 of vanilla), single-trip amounts must be increased to maintain economic equilibrium.\n\n" +
                    "★ Multiplier effects:\n" +
                    " - 1.0: Same per-trip amount as vanilla (but lower frequency = insufficient total consumption)\n" +
                    " - 3.5: Compensates to ~70-88% of vanilla total consumption (default)\n" +
                    " - 5.0: Nearly full compensation of vanilla consumption levels\n" +
                    " - 8.0: Over-compensation, for extreme maps + very low shopping probability\n\n" +
                    "★ Recommended by map/population scale:\n" +
                    " - 14km Vanilla: 1.0 ~ 2.0\n" +
                    " - 28km (ModeB): 2.0 ~ 3.5\n" +
                    " - 57km (ModeA): 3.5 ~ 5.0 (default: 3.5)\n" +
                    " - 114km (ModeC): 5.0 ~ 8.0\n\n" +
                    "★ Indicators: If commercial zones show vacant/bankrupt buildings, increase this value. " +
                    "If goods are instantly depleted (industrial products sold out), decrease it.\n" +
                    "Can be adjusted in-game."
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HomeSeekerCap)), "Home Search: Move Throughput" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HomeSeekerCap)),
                    "Maximum households with existing homes processed per frame for relocation evaluation. " +
                    "Controls how fast the system processes move requests (updated every 16 frames).\n" +
                    "Higher values speed up relocation matching but increase single-frame CPU cost (FindPropertyJob is single-threaded).\n\n" +
                    "★ Recommended:\n" +
                    " - Under 500k pop: 64 ~ 128 (default)\n" +
                    " - 2M pop: 128 ~ 256\n" +
                    " - Over 5M pop: 256 ~ 512\n\n" +
                    "★ Indicators: If many households refuse to relocate despite better housing available, increase this value. " +
                    "If the game stutters, decrease it. Can be adjusted in-game."
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HomelessSeekerCap)),
                    "Home Search: Homeless Throughput"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HomelessSeekerCap)),
                    "Maximum homeless households processed per frame for housing placement. " +
                    "Homeless families are prioritized over relocation requests.\n\n" +
                    "★ Recommended:\n" +
                    " - Under 500k pop: 640 ~ 1280 (default)\n" +
                    " - 2M pop: 1280 ~ 2560\n" +
                    " - Over 5M pop: 2560 ~ 5120\n\n" +
                    "★ Indicators: If large numbers of homeless remain despite vacant housing, increase this value. " +
                    "If mass homeless influx causes frame drops, decrease it. Can be adjusted in-game."
                },

                // ============================================================
                // Tab 3: Perf. Tools
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kPerformanceToolTab), "Perf. Tools" },

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

                // ============================================================
                // Tab 4: Debug
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kDebugTab), "Debug" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kDebugGroup), "Debug" },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DisableLoadGameValidation)),
                    "× Disable LoadGame Validation"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DisableLoadGameValidation)),
                    "⚠️ WARNING! Enabled by default (unchecked). LoadGame validation prevents loading saves with incorrect map size modes, which corrupts saves!\n" +
                    "Checking this will disable the validation. Only use this for special cases, such as unrecognised saves from older MapExt versions. Please ensure you've selected the correct 'MapSize Mode' before loading, or you might corrupt your save!\n" +
                    "Always backup your saves before using this feature."
                },
            };
            return entries;
        }

        public void Unload()
        {
        }
    }
}