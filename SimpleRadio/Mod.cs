using System;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using HarmonyLib;
using SimpleRadio.Core;
using SimpleRadio.Patches;
using SimpleRadio.Settings;

namespace SimpleRadio
{
    /// <summary>
    /// SimpleRadio Mod 入口。
    /// 极简自定义音乐播放器：将 OGG 文件放入 ModsData/SimpleRadio/电台名/ 即可播放。
    /// </summary>
    public class Mod : IMod
    {
        // === Constants ===
        public const string ModName = "SimpleRadio";
        public const string HarmonyId = "SimpleRadio.Patch";

        // === Logger ===
        public static ILog Logger = LogManager.GetLogger(ModName).SetShowsErrorsInUI(false);

        // === Singleton ===
        public static Mod Instance { get; private set; }

        // === Settings ===
        public ModSettings Settings { get; private set; }

        // === Harmony ===
        private Harmony _harmony;

        #region IMod 接口

        public void OnLoad(UpdateSystem updateSystem)
        {
            Instance = this;
            Logger.Info($"Loading {ModName} v{ModAssemblyInfo.Version}...");

            try
            {
                // 1. 初始化设置面板
                Settings = new ModSettings(this);
                Settings.RegisterInOptionsUI();
                GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));
                GameManager.instance.localizationManager.AddSource("zh-HANS", new LocaleHANS(Settings));
                AssetDatabase.global.LoadSettings(ModName, Settings, new ModSettings(this));

                // 2. 注册 COUI 图标资源
                IconManager.Register();

                // 3. 注册额外音频格式（MP3/WAV）并检测 ExtendedRadio
                AudioFormatHelper.RegisterExtensions();

                // 4. 注册 Harmony 补丁
                _harmony = new Harmony(HarmonyId);
                _harmony.CreateClassProcessor(typeof(RadioLoadPatch)).Patch();       // Postfix: 注入电台
                _harmony.CreateClassProcessor(typeof(PlaylistClipsPatch)).Patch();   // Prefix: 拦截运行时 clip 刷新

                // 仅在 ExtendedRadio 未加载时注册 LoadAsync 补丁
                // ExtendedRadio 已有全局 LoadAsync Prefix 处理所有格式
                if (!AudioFormatHelper.IsExtendedRadioLoaded)
                {
                    _harmony.CreateClassProcessor(typeof(AudioLoadPatch)).Patch();   // Prefix: 多格式解码器选择
                    Logger.Info("已注册 AudioLoadPatch（多格式解码支持）。");
                }

                Logger.Info($"{ModName} v{ModAssemblyInfo.Version} loaded successfully.");
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to load {ModName}");
            }
        }

        public void OnDispose()
        {
            Logger.Info($"Disposing {ModName}...");

            if (Settings != null)
            {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }

            IconManager.Unregister();

            if (_harmony != null)
            {
                _harmony.UnpatchAll(HarmonyId);
                _harmony = null;
            }
        }

        #endregion
    }
}
