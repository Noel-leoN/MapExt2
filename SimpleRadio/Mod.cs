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
                GameManager.instance.localizationManager.AddSource("zh-HANT", new LocaleHANT(Settings));
                AssetDatabase.global.LoadSettings(ModName, Settings, new ModSettings(this));

                // 2. 解析 Mod 部署目录（通过游戏官方 API，适配本地和 PDX 订阅环境）
                string modDir = null;
                if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                {
                    modDir = new System.IO.FileInfo(asset.path).Directory?.FullName;
                    Logger.Info($"Mod 部署目录: {modDir}");
                }
                else
                {
                    Logger.Warn("无法通过 TryGetExecutableAsset 获取部署路径，图标可能不可用。");
                }

                // 3. 注册 COUI 图标资源
                IconManager.Register(modDir);

                // 4. 註冊額外音訊格式（MP3/WAV），無條件執行（見 AudioFormatHelper 說明）
                AudioFormatHelper.RegisterExtensions();

                // 5. 註冊 Harmony 補丁
                _harmony = new Harmony(HarmonyId);
                _harmony.CreateClassProcessor(typeof(RadioLoadPatch)).Patch();       // Postfix + Finalizer: 注入電台並中和第三方崩潰
                _harmony.CreateClassProcessor(typeof(PlaylistClipsPatch)).Patch();   // Prefix: 攔截執行期 clip 刷新

                // 獨立模式：無條件註冊 AudioLoadPatch（多格式解碼器選擇）。
                // 其 Prefix 內建「僅攔截 SimpleRadio 自身資產」的守衛，對他人資產放行，
                // 與 ExtendedRadio 的全域 LoadAsync Prefix 可安全共存；不再依賴其存在與否。
                _harmony.CreateClassProcessor(typeof(AudioLoadPatch)).Patch();
                Logger.Info("已註冊 AudioLoadPatch（多格式解碼支援，獨立自主）。");

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
