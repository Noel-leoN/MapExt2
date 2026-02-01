// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Reflection;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Serialization;
using Game.Simulation;
using HarmonyLib;
using MapExtPDX.MapExt.MapSizePatchSet;
using MapExtPDX.SaveLoadSystem;

namespace MapExtPDX
{
    public class Mod : IMod
    {
        public const string ModName = "MapExtPDX"; // 保持与BepInEx版本一致
        // public const string ModFileName = "MapExtPDX2";
        public const string ModNameZH = "大地图mod";
        public static string ModVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        // 定义公共静态实例
        // (IMod标准接口仅包含OnLoad/Dispose，如自定义公共方法，需创建静态实例)
        public static Mod Instance { get; private set; }

        // 定义设置UI
        private ModSettings m_Setting;
        public ModSettings CurrentSettings => m_Setting;

        // 日志初始化
        // 日志归结到Logs\ModName.log，不要放在Player.log
        public static ILog Logger = LogManager.GetLogger($"{ModName}").SetShowsErrorsInUI(false);
        public static void Info(string text) => Logger.Info(text);
        public static void Debug(string text) => Logger.Debug(text);
        public static void Warn(string text) => Logger.Warn(text);
        public static void Error(string text) => Logger.Error(text);
        public static void Error(Exception e, string text) => Logger.Error(e, text);

        // 定义Harmony
        public static readonly string HarmonyId = ModName; // 弃用

        /// <summary>
        /// 双Harmony实例模式
        /// </summary>
        //  双Harmony实例名称定义
        public static readonly string HarmonyIdGlobal = $"{ModName}_global";
        public static readonly string HarmonyIdModes = $"{ModName}_modes";
        // 用于全局并行补丁定义
        private Harmony _globalPatcher;
        // 用于MapSize模式选择补丁集定义
        private Harmony _modePatcher;

        // --- unpatch标志位，用于ReBurst安全卸载 ---
        public static bool IsUnloading { get; private set; } = false;

        // Mod加载入口;首次进入游戏主菜单加载执行一次；
        public void OnLoad(UpdateSystem updateSystem)
        {
            // === 0. 加载模组执行asset ===
            Info($"{nameof(OnLoad)}, version:{ModVersion}");
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                Info($"{asset.name} v{asset.version} mod asset at {asset.path}");

            // === A. 配置实例 ===
            // Harmony.DEBUG = true;
            // FileLog.Log("MapExt_harmony.log");
            // === 初始化双Harmony实例 ===
            _globalPatcher = new Harmony(HarmonyIdGlobal);
            Info("HarmonyGlobal instance created");
            _modePatcher = new Harmony(HarmonyIdModes);
            Info("HarmonyModes instance created");

            // === B. 获取设置setting ===
            // 将当前实例赋值给静态属性(use for Setting)
            Instance = this;

            // Initialize settings
            m_Setting = new ModSettings(this);
            m_Setting.RegisterInOptionsUI();
            // 读取settings本地化语言库
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));
            GameManager.instance.localizationManager.AddSource("zh-HANS", new LocaleHANS(m_Setting));
            // 读取已保存设置
            Colossal.IO.AssetDatabase.AssetDatabase.global.LoadSettings(ModName, m_Setting, new ModSettings(this));
            Info("Settings initialized");

            // === C. 初始化MapSize PatchManager ===
            // 应用启动时默认的补丁模式
            // m_Setting.PatchModeChoice 从配置文件中加载上次保存的模式
            // 从设置加载初始模式
            PatchModeSetting initialMode = m_Setting.PatchModeChoice;
            Info($"正在初始化 PatchManager 使用设置模式: {m_Setting.PatchModeChoice}");
            // 执行MapSize关联主要系统补丁
            PatchManager.Initialize(_modePatcher, initialMode);

            // v2.1.1新增
            // --- 1. 强制重置WaterSystem.InitTextures() ---
            // !后续移入PatchManager
            WaterSystemReinitializer.Execute();

            // v2.2.0改动
            // --- 2. 执行CellMapFields补丁(自动扫描方式) ---
            // !后续移入PatchManager
            CellMapSystemPatchManager.ApplyPatches(_globalPatcher);

            // 已移入PatchManager.Initialize
            // --- 3. 修复AirwaySystem ---
            // _globalPatcher.CreateClassProcessor(typeof(AirwaySystem_OnUpdate_Patch)).Patch();
            // Info($"AirwaySystem补丁(全局并行方式) {nameof(AirwaySystem_OnUpdate_Patch)}已应用.");

            // === D. 存档验证系统 === 
            Info("全局并行方式补丁正在逐条执行...");
            // 4.1 加载SaveLoadSystem的2个class补丁
            if (m_Setting.DisableLoadGameValidation == false)
            {
                _globalPatcher.CreateClassProcessor(typeof(MetaDataExtenderPatch)).Patch();
                Info($"存档验证补丁(全局并行) {nameof(MetaDataExtenderPatch)}已应用.");
                _globalPatcher.CreateClassProcessor(typeof(LoadGameValidatorPatch)).Patch();
                Info($"存档验证补丁(全局并行) {nameof(LoadGameValidatorPatch)}已应用.");
            }
            // 其他并行的选项补丁，也在这里添加
            // _globalPatcher.CreateClassProcessor(typeof(ParallelOptionPatch)).Patch();
            // 加载SaveLoadSystem的弹窗本地化语言库
            ModLocalization.Initialize(GameManager.instance.localizationManager);
            Info($"加载OptionUI本地化文本 {nameof(ModLocalization)}已应用.");

            // === E. 性能工具 ===
            // 其他并行的选项补丁
            // 手动ECS调用Apply方式以应用已保存的设置值
            // --- 加载NoDogs ---
            m_Setting.UpdateNoDogsSystemStates();
            
            // --- 加载NoTroughTraffic(From CS2LiteBooster) ---
            m_Setting.UpdateNoThroughTrafficSystemStates();

            // --- 加载NoRandomTraffic(From CS2LiteBooster) ---
            // m_Setting.UpdateNoRandomTrafficSystemStates();

            // === F. 加载特色工具 ===
            // 加载LandValueRemake
            // m_Setting.UpdateLandValueRemakeSystemStates();
            //Info($"LandValue Remake补丁(全局并行) {nameof(ModLocalization)}已应用.");

            // 执行诊断系统
            // updateSystem.UpdateAt<SaveGameDiagnosticSystem>(SystemUpdatePhase.LateUpdate);

            // === G. ECS替换系统补丁 ===
            // CellMapSystem<T> 和 经济系统 的ECS替换补丁
            ApplyECSReplacer(updateSystem);

        }

        public void ApplyECSReplacer(UpdateSystem updateSystem)
        {
            // Part 1:
            // --- CellMapSystem<T> ECS替换 ---
            // // Telecom/Wind 等系统暂不替换

            // Part 2:
            // --- 找房/购物/找工作/系统优化替换 ---
            // ECS替换HouseholdSpawnSystem
            // ECS替换HouseholdFindPropertySystem
            // ECS替换HouseholdBehaviorSystem
            // ECS替换FindJobSystem
            // ECS替换CitizenFindJobSystem

            // --- 住工商需求/租金系统优化替换 ---
            // Job通用替换修补ResidentialDemandSystemRe
            // Job通用替换修补CommerialDemandSystemRe
            // Job通用替换修补IndustrialDemandSystemRe
            // Job通用替换修补RentAdjustSystemRe

            // ModeA/B/C 共用禁用原系统逻辑
            if (PatchManager.CurrentCoreValue != 1)
            {
                // CellMapSystem<T> ECS替换
                // Telecom/Wind 等系统暂不替换
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.AirPollutionSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.AvailabilityInfoToGridSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.GroundPollutionSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.GroundWaterSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.LandValueSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.NaturalResourceSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.NoisePollutionSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.PopulationToGridSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.SoilWaterSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.TerrainAttractivenessSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.TrafficAmbienceSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.ZoneAmbienceSystem>().Enabled = false;

                // 经济系统 ECS替换
                if (m_Setting.isEnableEconomyFix == true)
                {
                    // ResidentialDemand/CommercialDemandSystem/IndustrialDemand/RentAdjust 采用Job通用替换修补，无需禁用原系统

                    // InfoLoom兼容
                    //updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.CommercialDemandSystem>().Enabled = false;
                    //MethodInfo targetMethod = AccessTools.Method(typeof(Game.Simulation.CommercialDemandSystem), "OnUpdate");
                    //_globalPatcher.Unpatch(targetMethod, HarmonyPatchType.Prefix, "Bruceyboy24804InfoLoomTwo");

                    // updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdSpawnSystem>().Enabled = false;
                    updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdFindPropertySystem>().Enabled = false;
                    updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdBehaviorSystem>().Enabled = false;
                    updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.CitizenFindJobSystem>().Enabled = false;
                    updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.FindJobSystem>().Enabled = false;
                }
            }

            // 57km ModeA
            if (PatchManager.CurrentCoreValue == 4)
            {
                // ==================================================
                // --- CellMapSystem<T> ECS替换 ---
                // ==================================================
                updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.AirPollutionSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeA.AirPollutionSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.AvailabilityInfoToGridSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeA.AvailabilityInfoToGridSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.GroundPollutionSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeA.GroundPollutionSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.GroundWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeA.GroundWaterSystemMod.Patches)).Patch();


                updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.NaturalResourceSystemMod>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.NaturalResourceSystemMod>(SystemUpdatePhase.EditorSimulation);
                updateSystem.UpdateAfter<PostDeserialize<MapExt.ReBurstSystemModeA.NaturalResourceSystemMod>>(SystemUpdatePhase.Deserialize);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeA.NaturalResourceSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.NoisePollutionSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeA.NoisePollutionSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.PopulationToGridSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeA.PopulationToGridSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.SoilWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                //updateSystem.UpdateAfter<MapExt.ReBurstSystemModeA.SoilWaterSystemMod>(SystemUpdatePhase.Deserialize);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeA.SoilWaterSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.TerrainAttractivenessSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeA.TerrainAttractivenessSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.TrafficAmbienceSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeA.TrafficAmbienceSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.ZoneAmbienceSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeA.ZoneAmbienceSystemMod.Patches)).Patch();

                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.LandValueSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeA.LandValueSystemMod.Patches)).Patch();
                // updateSystem.UpdateAt<LandValueConfigSyncSystem>(SystemUpdatePhase.GameSimulation);
                // ======================================================

                // ======================
                // --- 经济系统 ECS替换 ---
                // ======================

                if (m_Setting.isEnableEconomyFix == true)
                {
                    //_globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstEcoSystemModeA.CommercialDemandSystemMod.Patches)).Patch();
                    //updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeA.CommercialDemandSystemMod>(SystemUpdatePhase.GameSimulation);

                    // 暂不替换HouseholdSpawnSystem
                    // updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.HouseholdSpawnSystemMod>(SystemUpdatePhase.GameSimulation);
                    // Mod.Info($"已执行{nameof(HouseholdSpawnSystemMod)}.");

                    // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                    _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstEcoSystemModeA.PathfindSetupSystem_FindTargets_Patch)).Patch(); updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeA.HouseholdFindPropertySystemMod>(SystemUpdatePhase.GameSimulation);

                    updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeA.HouseholdBehaviorSystemMod>(SystemUpdatePhase.GameSimulation);

                    updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeA.CitizenFindJobSystemMod>(SystemUpdatePhase.GameSimulation);

                    updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeA.FindJobSystemMod>(SystemUpdatePhase.GameSimulation);

                    // Job通用替换修补ResidentialDemand/IndustrialDemand/RentAdjust
                    MapExt.ReBurstEcoSystem.JobPatchHelper.ApplyAllPatches(_globalPatcher);
                }

            }

            //// 28km ModeB
            if (PatchManager.CurrentCoreValue == 2)
            {
                // ==================================================
                // --- CellMapSystem<T> ECS替换 ---
                // ==================================================
                updateSystem.UpdateAt<MapExt.ReBurstSystemModeB.AirPollutionSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeB.AirPollutionSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeB.AvailabilityInfoToGridSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeB.AvailabilityInfoToGridSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeB.GroundPollutionSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeB.GroundPollutionSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeB.GroundWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeB.GroundWaterSystemMod.Patches)).Patch();


                updateSystem.UpdateAt<MapExt.ReBurstSystemModeB.NaturalResourceSystemMod>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateAt<MapExt.ReBurstSystemModeB.NaturalResourceSystemMod>(SystemUpdatePhase.EditorSimulation);
                updateSystem.UpdateAfter<PostDeserialize<MapExt.ReBurstSystemModeB.NaturalResourceSystemMod>>(SystemUpdatePhase.Deserialize);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeB.NaturalResourceSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeB.NoisePollutionSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeB.NoisePollutionSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeB.PopulationToGridSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeB.PopulationToGridSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeB.SoilWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                //updateSystem.UpdateAfter<MapExt.ReBurstSystemModeA.SoilWaterSystemMod>(SystemUpdatePhase.Deserialize);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeB.SoilWaterSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeB.TerrainAttractivenessSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeB.TerrainAttractivenessSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeB.TrafficAmbienceSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeB.TrafficAmbienceSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeB.ZoneAmbienceSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeB.ZoneAmbienceSystemMod.Patches)).Patch();

                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<MapExt.ReBurstSystemModeB.LandValueSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeB.LandValueSystemMod.Patches)).Patch();
                // updateSystem.UpdateAt<LandValueConfigSyncSystem>(SystemUpdatePhase.GameSimulation);
                // ======================================================

                // ======================
                // --- 经济系统 ECS替换 ---
                // ======================

                if (m_Setting.isEnableEconomyFix == true)
                {
                    //_globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstEcoSystemModeA.CommercialDemandSystemMod.Patches)).Patch();
                    //updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeA.CommercialDemandSystemMod>(SystemUpdatePhase.GameSimulation);

                    // 暂不替换HouseholdSpawnSystem
                    // updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.HouseholdSpawnSystemMod>(SystemUpdatePhase.GameSimulation);
                    // Mod.Info($"已执行{nameof(HouseholdSpawnSystemMod)}.");

                    // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                    _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstEcoSystemModeB.PathfindSetupSystem_FindTargets_Patch)).Patch(); updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeB.HouseholdFindPropertySystemMod>(SystemUpdatePhase.GameSimulation);

                    updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeB.HouseholdBehaviorSystemMod>(SystemUpdatePhase.GameSimulation);

                    updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeB.CitizenFindJobSystemMod>(SystemUpdatePhase.GameSimulation);

                    updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeB.FindJobSystemMod>(SystemUpdatePhase.GameSimulation);

                    // Job通用替换修补ResidentialDemand/IndustrialDemand/RentAdjust
                    MapExt.ReBurstEcoSystem.JobPatchHelper.ApplyAllPatches(_globalPatcher);
                }

            }

            // 114km ModeC
            if (PatchManager.CurrentCoreValue == 8)
            {
                // ==================================================
                // --- CellMapSystem<T> ECS替换 ---
                // ==================================================
                updateSystem.UpdateAt<MapExt.ReBurstSystemModeC.AirPollutionSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeC.AirPollutionSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeC.AvailabilityInfoToGridSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeC.AvailabilityInfoToGridSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeC.GroundPollutionSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeC.GroundPollutionSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeC.GroundWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeC.GroundWaterSystemMod.Patches)).Patch();


                updateSystem.UpdateAt<MapExt.ReBurstSystemModeC.NaturalResourceSystemMod>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateAt<MapExt.ReBurstSystemModeC.NaturalResourceSystemMod>(SystemUpdatePhase.EditorSimulation);
                updateSystem.UpdateAfter<PostDeserialize<MapExt.ReBurstSystemModeC.NaturalResourceSystemMod>>(SystemUpdatePhase.Deserialize);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeC.NaturalResourceSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeC.NoisePollutionSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeC.NoisePollutionSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeC.PopulationToGridSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeC.PopulationToGridSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeC.SoilWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                //updateSystem.UpdateAfter<MapExt.ReBurstSystemModeA.SoilWaterSystemMod>(SystemUpdatePhase.Deserialize);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeC.SoilWaterSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeC.TerrainAttractivenessSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeC.TerrainAttractivenessSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeC.TrafficAmbienceSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeC.TrafficAmbienceSystemMod.Patches)).Patch();

                updateSystem.UpdateAt<MapExt.ReBurstSystemModeC.ZoneAmbienceSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeC.ZoneAmbienceSystemMod.Patches)).Patch();

                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<MapExt.ReBurstSystemModeC.LandValueSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstSystemModeC.LandValueSystemMod.Patches)).Patch();
                // updateSystem.UpdateAt<LandValueConfigSyncSystem>(SystemUpdatePhase.GameSimulation);
                // ======================================================


                // ======================
                // --- 经济系统 ECS替换 ---
                // ======================

                if (m_Setting.isEnableEconomyFix == true)
                {
                    //_globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstEcoSystemModeA.CommercialDemandSystemMod.Patches)).Patch();
                    //updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeA.CommercialDemandSystemMod>(SystemUpdatePhase.GameSimulation);

                    // 暂不替换HouseholdSpawnSystem
                    // updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.HouseholdSpawnSystemMod>(SystemUpdatePhase.GameSimulation);
                    // Mod.Info($"已执行{nameof(HouseholdSpawnSystemMod)}.");

                    // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                    _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstEcoSystemModeC.PathfindSetupSystem_FindTargets_Patch)).Patch(); updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeC.HouseholdFindPropertySystemMod>(SystemUpdatePhase.GameSimulation);

                    updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeC.HouseholdBehaviorSystemMod>(SystemUpdatePhase.GameSimulation);

                    updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeC.CitizenFindJobSystemMod>(SystemUpdatePhase.GameSimulation);

                    updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeC.FindJobSystemMod>(SystemUpdatePhase.GameSimulation);

                    // Job通用替换修补ResidentialDemand/IndustrialDemand/RentAdjust
                    MapExt.ReBurstEcoSystem.JobPatchHelper.ApplyAllPatches(_globalPatcher);
                }
            }

            // vanilla ModeE (None)
            if (PatchManager.CurrentCoreValue == 1 && m_Setting.isEnableEconomyFix == true)
            {
                // === 原系统禁用 ===
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.LandValueSystem>().Enabled = false;

                // updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdSpawnSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdFindPropertySystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdBehaviorSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.CitizenFindJobSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.FindJobSystem>().Enabled = false;

                // === 自定义系统启用 ===
                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeE.LandValueSystemMod>(SystemUpdatePhase.GameSimulation);
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstEcoSystemModeE.LandValueSystemMod.Patches)).Patch();

                // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                _globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstEcoSystemModeE.PathfindSetupSystem_FindTargets_Patch)).Patch(); updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeE.HouseholdFindPropertySystemMod>(SystemUpdatePhase.GameSimulation);

                updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeE.HouseholdBehaviorSystemMod>(SystemUpdatePhase.GameSimulation);

                updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeE.CitizenFindJobSystemMod>(SystemUpdatePhase.GameSimulation);

                updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeE.FindJobSystemMod>(SystemUpdatePhase.GameSimulation);

                // Job通用替换修补ResidentialDemand/CommerialDemand/IndustrialDemand/RentAdjust
                MapExt.ReBurstEcoSystem.JobPatchHelper.ApplyAllPatches(_globalPatcher);
            }
        }

        // 被Settings中的Apply按钮调用
        public void OnPatchModeChanged(PatchModeSetting newModeFromSettings)
        {
            if (m_Setting == null) return;
            // 通知PatchManager使用设置中当前选定的新模式
            Info($"Mod.OnPatchModeChanged: MapSize Mode在设置UI中改变为: {newModeFromSettings}");
            // 关键方法，切换应用补丁模式集
            PatchManager.SetPatchMode(newModeFromSettings);
            //m_Setting?.RefreshModSettingInfo(); // Update status display
        }

        //public void OnEcoPatchChanged(bool isEnableEcoFix)
        //{
        //    if (m_Setting == null) return;
        //    // 通知PatchManager使用设置中当前选定的新模式
        //    Info($"EcoPatch Mode在设置UI中改变为: {isEnableEcoFix}");
        //    // 关键方法，切换应用补丁模式集
        //    EconomySystemPatchManagerApplyPatches(_globalPatcher);
        //    //m_Setting?.RefreshModSettingInfo(); // Update status display
        //}

        public void ApplyPatchChangesFromSettings(PatchModeSetting modeToApply)
        {
            PatchManager.SetPatchMode(modeToApply);
            Info($"Mod.ApplyPatchChangesFromSettings: 已应用MapSize Mode: {modeToApply}");
        }

        public void OnDispose()
        {
            Info(nameof(OnDispose));

            IsUnloading = true;

            // 在Mod卸载时移除所有补丁
            _globalPatcher?.UnpatchAll();
            _modePatcher?.UnpatchAll();
            _globalPatcher = null;
            _modePatcher = null;
            Info("All Harmony patches removed on dispose.");

            // 卸载Settings
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }        

        /// <summary>
        /// Gets the current game mode.
        /// This method can be called by ModSettings.IsVisibleInMainMenu.
        /// </summary>
        public GameMode GetCurrentGameMode()
        {
            var gameMode = GameManager.instance.gameMode;
            Info($"当前游戏模式为 {gameMode}");
            return gameMode;
        }

    } // class Mod
} // namespace MapExtPDX
