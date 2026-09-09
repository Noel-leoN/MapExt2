using System;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using SimpleBrush.Core;
using SimpleBrush.Settings;

namespace SimpleBrush
{
    /// <summary>
    /// SimpleBrush Mod 入口。
    /// 解锁隐藏的自然资源画笔工具，并提供一键恢复耗尽资源功能。
    /// </summary>
    public class Mod : IMod
    {
        // === Constants ===
        public const string ModName = "SimpleBrush";

        // === Logger ===
        public static readonly ILog Logger = LogManager.GetLogger(ModName).SetShowsErrorsInUI(false);

        // === Singleton ===
        public static Mod Instance { get; private set; }

        // === Settings ===
        public SimpleBrushSettings Settings { get; private set; }

        #region IMod 接口

        /// <summary>
        /// <see cref="OnLoad"/> 是否中斷過。中斷後本 Mod 處於半初始化狀態。
        /// </summary>
        public static bool InitializationFailed { get; private set; }

        /// <summary>
        /// Mod 載入入口。本方法只負責兜底，實際初始化在 <see cref="OnLoadCore"/>。
        ///
        /// <para><c>ModManager.InitializeMods</c> 對逸出的異常只寫一行 log、不彈窗，
        /// 於是「整個 Mod 沒生效」會靜默發生。MapExtPDX 於 2026-09-05 實測到一次
        /// （<c>AddSource</c> 撞上引擎無鎖的本地化字典），本 Mod 走同一條引擎路徑，
        /// 故一併加固。此處只記日誌不推通知——本 Mod 失效的後果是畫筆工具沒解鎖，
        /// 使用者一眼可見，且不涉及存檔安全。</para>
        /// </summary>
        public void OnLoad(UpdateSystem updateSystem)
        {
            try
            {
                OnLoadCore(updateSystem);
            }
            catch (Exception ex)
            {
                InitializationFailed = true;
                Logger.Error(ex, $"{ModName} OnLoad 中斷，本次遊戲未生效");
            }
        }

        private void OnLoadCore(UpdateSystem updateSystem)
        {
            Instance = this;
            Logger.Info($"Loading {ModName} v{ModAssemblyInfo.Version}...");

            // 1. 初始化设置面板与本地化
            Settings = new SimpleBrushSettings(this);
            Settings.RegisterInOptionsUI();
            var lm = GameManager.instance.localizationManager;
            AddSourceSafe(lm, "en-US", new LocaleEN(Settings));
            AddSourceSafe(lm, "zh-HANS", new LocaleHANS(Settings));
            AddSourceSafe(lm, "zh-HANT", new LocaleHANT(Settings));
            AssetDatabase.global.LoadSettings(ModName, Settings, new SimpleBrushSettings(this));

            // 2. 注册资源画笔解锁系统（PrefabUpdate 阶段执行一次后自动禁用）
            updateSystem.UpdateAt<TerraformingUnlocker>(SystemUpdatePhase.PrefabUpdate);

            // 3. 注册无限资源守护系统（GameSimulation 阶段，与原版资源系统同步）
            updateSystem.UpdateAt<ResourceGuardSystem>(SystemUpdatePhase.GameSimulation);

            Logger.Info($"{ModName} loaded successfully.");
        }

        /// <summary>
        /// 包一層 try-catch 的 <c>AddSource</c>。
        /// <para>引擎的 <c>LocalizationDictionary</c> 是無鎖的裸 <c>Dictionary</c>：註冊
        /// fallback 語言（en-US）時會觸發一趟遍歷整個字典的 <c>MergeFrom</c>，
        /// 期間若有別的執行流寫入就拋 <c>Collection was modified</c>。本地化失敗的實際影響
        /// 只是「該語言文字顯示為 key」，不該讓整個 Mod 失效。</para>
        /// </summary>
        private static void AddSourceSafe(
            Colossal.Localization.LocalizationManager lm, string localeId, Colossal.IDictionarySource source)
        {
            try
            {
                lm.AddSource(localeId, source);
            }
            catch (Exception ex)
            {
                Logger.Warn($"註冊 {localeId} 本地化失敗（引擎本地化字典無鎖，疑撞上並發寫入），該語言文字將顯示為 key: {ex.Message}");
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

            Instance = null;
        }

        #endregion
    }
}
