// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Reflection;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.PSI;
using Game.SceneFlow;
using Game.UI.Localization;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using MapExtPDX.SaveLoadSystem;

namespace MapExtPDX
{
    public class Mod : IMod
    {
        private const string Tag = "Mod";

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

        /// <summary>
        /// Settings 别名，使 EcoSystems 代码与 EconomyEX 保持一致的访问路径。
        /// </summary>
        public ModSettings Settings => m_Setting;

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
            ModLog.Info(Tag, $"OnLoad, version:{ModVersion}");
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                ModLog.Info(Tag, $"{asset.name} v{asset.version} mod asset at {asset.path}");

            // === 初始化双Harmony实例 ===
            _globalPatcher = new Harmony(HarmonyIdGlobal);
            ModLog.Patch(Tag, "HarmonyGlobal 实例已创建");
            _modePatcher = new Harmony(HarmonyIdModes);
            ModLog.Patch(Tag, "HarmonyModes 实例已创建");

            // === B. 获取设置setting ===
            // 将当前实例赋值给静态属性(use for Setting)
            Instance = this;

            // Initialize settings
            m_Setting = new ModSettings(this);
            m_Setting.RegisterInOptionsUI();
            // 读取settings本地化语言库
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));
            GameManager.instance.localizationManager.AddSource("zh-HANS", new LocaleHANS(m_Setting));
            GameManager.instance.localizationManager.AddSource("zh-HANT", new LocaleHANT(m_Setting));
            // 读取已保存设置
            Colossal.IO.AssetDatabase.AssetDatabase.global.LoadSettings(ModName, m_Setting, new ModSettings(this));
            ModLog.Ok(Tag, "Settings 已初始化");

#if DEBUG
            // === 设置值验证日志（仅 DEBUG 编译有效） ===
            // 用于诊断 .coc 文件加载异常或框架缓存不一致问题
            ModLog.Debug(Tag, "=== Settings Dump (DEBUG) ===");
            ModLog.Debug(Tag, $"  PatchMode={m_Setting.PatchModeChoice}");
            ModLog.Debug(Tag, $"  TerrainRes={m_Setting.TerrainResolution}, WaterRes={m_Setting.WaterResolution}");
            ModLog.Debug(Tag,
                $"  WaterSimQuality={m_Setting.WaterSimQuality}, WaterTexFmt={m_Setting.WaterTextureFormat}");
            ModLog.Debug(Tag, $"  TerrainBufferPrealloc={m_Setting.TerrainBufferPrealloc}");
            ModLog.Debug(Tag,
                $"  TerrainCascadeThrottle={m_Setting.TerrainCascadeThrottle}, TerrainCullThrottle={m_Setting.TerrainCullThrottle}");
            ModLog.Debug(Tag,
                $"  EnableVanillaConversion={m_Setting.EnableVanillaConversion}, DisableWorldBackdrop={m_Setting.DisableWorldBackdrop}");
            ModLog.Debug(Tag, $"  EconomyFix={m_Setting.isEnableEconomyFix}");
            ModLog.Debug(Tag,
                $"  EcoSystems: Demand={m_Setting.EnableDemandEcoSystem}, JobSearch={m_Setting.EnableJobSearchEcoSystem}");
            ModLog.Debug(Tag,
                $"  EcoSystems: HouseholdProp={m_Setting.EnableHouseholdPropertyEcoSystem}, ResBuyer={m_Setting.EnableResourceBuyerEcoSystem}");
            ModLog.Debug(Tag,
                $"  EcoSystems: ResidentAI={m_Setting.EnableResidentAIEcoSystem}, DownstreamAI={m_Setting.EnableDownstreamAIEcoSystem}");
            ModLog.Debug(Tag, $"  NoDogs: Street={m_Setting.NoDogsOnStreet}, Gen={m_Setting.NoDogsGeneration}");
            ModLog.Debug(Tag, $"  NoThroughTraffic={m_Setting.NoThroughTraffic}");
            ModLog.Debug(Tag, $"  DisableLoadGameValidation={m_Setting.DisableLoadGameValidation}");
            ModLog.Debug(Tag, "=== End Settings Dump ===");
#endif

            // === C. 初始化MapSize PatchManager ===
            // 应用启动时默认的补丁模式
            // m_Setting.PatchModeChoice 从配置文件中加载上次保存的模式
            // 从设置加载初始模式
            PatchModeSetting initialMode = m_Setting.PatchModeChoice;
            ModLog.Info(Tag, $"正在初始化 PatchManager 使用设置模式: {m_Setting.PatchModeChoice}");
            // 执行MapSize关联主要系统补丁
            PatchManager.Initialize(_modePatcher, initialMode);


            // === D. 存档验证系统 === 
            ModLog.Info(Tag, "全局并行方式补丁正在逐条执行...");
            // 4.1 加载SaveLoadSystem的2个class补丁
            if (m_Setting.DisableLoadGameValidation == false)
            {
                _globalPatcher.CreateClassProcessor(typeof(MetaDataExtenderPatch)).Patch();
                ModLog.Patch(Tag, $"{nameof(MetaDataExtenderPatch)} 已应用");
                _globalPatcher.CreateClassProcessor(typeof(LoadGameValidatorPatch)).Patch();
                ModLog.Patch(Tag, $"{nameof(LoadGameValidatorPatch)} 已应用");
            }

            // 其他并行的选项补丁，也在这里添加
            // _globalPatcher.CreateClassProcessor(typeof(ParallelOptionPatch)).Patch();
            // 加载SaveLoadSystem的弹窗本地化语言库
            ModLocalization.Initialize(GameManager.instance.localizationManager);
            ModLog.Ok(Tag, $"{nameof(ModLocalization)} 本地化文本已加载");

            // 4.2 注册原版存档转换系统
            updateSystem.UpdateAt<VanillaSaveConversionSystem>(SystemUpdatePhase.LoadSimulation);
            ModLog.Patch(Tag, $"{nameof(VanillaSaveConversionSystem)} 已注册到 LoadSimulation");

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

            // === G. 冲突 Mod 指纹检测 ===
            ModConflictDetector.ScanLoadedMods();
            m_Setting._detectedConflictMods = ModConflictDetector.GetDetectedModsSummary();
            // 根据检测结果生成基于当前设置的冲突报告
            var conflictReport = ModConflictDetector.GetConflictReport(m_Setting);
            if (conflictReport != "None")
            {
                m_Setting._conflictWarning = $"[Startup] {conflictReport}";
                ModLog.Warn(Tag, $"启动冲突报告: {conflictReport}");
            }

            // 自动禁用与冲突 Mod 重叠的系统组（在 SystemReplacer.Apply 之前执行！）
            var disabledGroups = ModConflictDetector.AutoDisableConflictGroups(m_Setting);
            if (disabledGroups.Count > 0)
            {
                m_Setting._conflictWarning = $"[Auto-Disabled] {string.Join(", ", disabledGroups)}";
                m_Setting._systemStatusReport = $"Auto-disabled {disabledGroups.Count} group(s) due to conflicts";
            }

            // === H. ECS替换系统补丁 ===
            // CellMapSystem<T> 和 经济系统 的ECS替换补丁
            SystemReplacer.Apply(updateSystem, _globalPatcher, m_Setting);

            // === I. 主菜单通知（冲突提醒） ===
            if (disabledGroups.Count > 0)
            {
                // pageId = AssemblyName.Namespace.TypeName (见 ModSetting 构造函数)
                const string pageId = "MapExt2.MapExtPDX.Mod";
                const string sectionId = "MapExt2.MapExtPDX.Mod.EconomyEX";

                NotificationSystem.Push(
                    identifier: "mapext.conflict",
                    title: LocalizedString.Value("RESTART REQUIRED!!!"),
                    text: LocalizedString.Value(
                        $"[MapExt2] Disabled {disabledGroups.Count} group(s) " +
                        $"due to mod conflicts: {string.Join(", ", disabledGroups)}"),
                    progressState: Colossal.PSI.Common.ProgressState.Failed,
                    progress: 100,
                    onClicked: () =>
                    {
                        var optionsUI = Unity.Entities.World.DefaultGameObjectInjectionWorld?
                            .GetExistingSystemManaged<Game.UI.Menu.OptionsUISystem>();
                        optionsUI?.OpenPage(pageId, sectionId, false);
                    }
                );
            }

            // === J. RPF 硬冲突通知 ===
            // RPF 的 UpdateGroupSystem 跨阶段注册会导致 ECB 崩溃，两者不能共存
            if (ModConflictDetector.HasRealisticPathFinding)
            {
                NotificationSystem.Push(
                    identifier: "mapext.rpf_incompatible",
                    title: LocalizedString.Value("MapExt2: Incompatible Mod Detected"),
                    text: LocalizedString.Value(
                        "[MapExt2] RealisticPathFinding (RPF) is incompatible with MapExt2. " +
                        "RPF's cross-phase system registration causes EntityCommandBuffer crashes. " +
                        "Please disable one of them to avoid errors."),
                    progressState: Colossal.PSI.Common.ProgressState.Failed,
                    progress: 100
                );
                ModLog.Error(Tag, "RPF 与 MapExt2 存在不可调和的 ECS 管线冲突（UpdateGroupSystem 跨阶段注册），请禁用其中之一。");
            }
        }

        // 被Settings中的Apply按钮调用
        public void OnPatchModeChanged(PatchModeSetting newModeFromSettings)
        {
            if (m_Setting == null) return;
            // 通知PatchManager使用设置中当前选定的新模式
            ModLog.Swap(Tag, $"MapSize Mode 在设置UI中改变为: {newModeFromSettings}");
            // 关键方法，切换应用补丁模式集
            PatchManager.SetPatchMode(newModeFromSettings);
            //m_Setting?.RefreshModSettingInfo(); // Update status display
        }

        public void ApplyPatchChangesFromSettings(PatchModeSetting modeToApply)
        {
            PatchManager.SetPatchMode(modeToApply);
            ModLog.Ok(Tag, $"已应用 MapSize Mode: {modeToApply}");
        }

        public void OnDispose()
        {
            ModLog.Info(Tag, nameof(OnDispose));

            IsUnloading = true;

            // 在Mod卸载时移除所有补丁
            _globalPatcher?.UnpatchAll();
            _modePatcher?.UnpatchAll();
            _globalPatcher = null;
            _modePatcher = null;
            ModLog.Ok(Tag, "所有 Harmony 补丁已移除");

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
            ModLog.Info(Tag, $"当前游戏模式为 {gameMode}");
            return gameMode;
        }
    } // class Mod
} // namespace MapExtPDX
