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
using HarmonyLib;
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
        public static readonly string HarmonyIdGlobal = $"{ModName}.global";
        public static readonly string HarmonyIdModes = $"{ModName}.modes";       
        // 用于全局并行补丁定义
        private Harmony _globalPatcher;
        // 用于MapSize模式选择补丁集定义
        private Harmony _modePatcher;

        // --- unpatch标志位，用于ReBurst安全卸载 ---
        public static bool IsUnloading { get; private set; } = false;

        // Mod加载入口;首次进入游戏主菜单加载执行一次；
        public void OnLoad(UpdateSystem updateSystem)
        {
            // Log;
            Info($"{nameof(OnLoad)}, version:{Mod.ModVersion}");
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                Info($"{asset.name} v{asset.version} mod asset at {asset.path}");

            // 1.初始化双Harmony实例
            _globalPatcher = new Harmony(HarmonyIdGlobal);
            Info("HarmonyGlobal instance created");
            _modePatcher = new Harmony(HarmonyIdModes);
            Info("HarmonyModes instance created");

            // 将当前实例赋值给静态属性(use for Setting)
            Instance = this;
            m_Setting = new ModSettings(this);

            // 2.Initialize settings
            m_Setting = new ModSettings(this);
            m_Setting.RegisterInOptionsUI();
            // 2.1 读取settings本地化语言库
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));
            GameManager.instance.localizationManager.AddSource("zh-HANS", new LocaleHANS(m_Setting));
            // 2.2 读取已保存设置
            // Load saved settings
            Colossal.IO.AssetDatabase.AssetDatabase.global.LoadSettings(nameof(ModName), m_Setting, new ModSettings(this));
            Info("Settings initialized");

            // 3. 初始化MapSize PatchManager，并应用启动时默认的补丁模式
            // m_Setting.PatchModeChoice 从配置文件中加载上次保存的模式
            // 从设置加载初始模式
            PatchModeSetting initialMode = m_Setting.PatchModeChoice;
            Info($"Initializing PatchManager with mode from settings: {m_Setting.PatchModeChoice}");
            PatchManager.Initialize(_modePatcher, initialMode);
            Info("PatchManager初始化完成应用！(所有Transpiler补丁完成执行；所有Pre/Postfix将在方法调用时执行");

            // 4. 执行全局并行补丁
            Info("全局并行补丁正在逐条执行...");
            // 4.1 加载SaveLoadSystem的2个class补丁
            if (m_Setting.DisableLoadGameValidation == false)
            {
                _globalPatcher.CreateClassProcessor(typeof(MetaDataExtenderPatch)).Patch();
                Info($"全局并行补丁{nameof(MetaDataExtenderPatch)}已应用.");
                _globalPatcher.CreateClassProcessor(typeof(LoadGameValidatorPatch)).Patch();
                Info($"全局并行补丁{nameof(LoadGameValidatorPatch)}已应用.");
            }
            // 其他并行的选项补丁，也在这里添加
            // _globalPatcher.CreateClassProcessor(typeof(ParallelOptionPatch)).Patch();
            // 加载SaveLoadSystem的弹窗本地化语言库
            ModLocalization.Initialize(GameManager.instance.localizationManager);

            // 其他并行的选项补丁
            // 手动ECS调用Apply方式以应用已保存的设置值
            // 4.2.1 加载NoDogs
            m_Setting.UpdateNoDogsSystemStates();
            // 4.2.2 加载NoTroughTraffic(From CS2LiteBooster)
            m_Setting.UpdateNoThroughTrafficSystemStates();
            // 4.2.3 加载NoRandomTraffic(From CS2LiteBooster)
            // m_Setting.UpdateNoRandomTrafficSystemStates();
            
            // 4.3 加载特色工具
            // 加载LandValueRemake
            // m_Setting.UpdateLandValueRemakeSystemStates();

            // 执行诊断系统
            // updateSystem.UpdateAt<SaveGameDiagnosticSystem>(SystemUpdatePhase.LateUpdate);

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

        public void ApplyPatchChangesFromSettings(PatchModeSetting modeToApply)
        {
            PatchManager.SetPatchMode(modeToApply);
            Logger.Info($"Mod.ApplyPatchChangesFromSettings: 已应用MapSize Mode: {modeToApply}");
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
