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
        public static ILog Logger = LogManager.GetLogger(ModName).SetShowsErrorsInUI(false);

        // === Singleton ===
        public static Mod Instance { get; private set; }

        // === Settings ===
        public ModSettings Settings { get; private set; }

        #region IMod 接口

        public void OnLoad(UpdateSystem updateSystem)
        {
            Instance = this;
            Logger.Info($"Loading {ModName} v{ModAssemblyInfo.Version}...");

            // 1. 初始化设置面板与本地化
            Settings = new ModSettings(this);
            Settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));
            GameManager.instance.localizationManager.AddSource("zh-HANS", new LocaleHANS(Settings));
            GameManager.instance.localizationManager.AddSource("zh-HANT", new LocaleHANT(Settings));
            AssetDatabase.global.LoadSettings(ModName, Settings, new ModSettings(this));

            // 2. 注册资源画笔解锁系统（PrefabUpdate 阶段执行一次后自动禁用）
            updateSystem.UpdateAt<TerraformingUnlocker>(SystemUpdatePhase.PrefabUpdate);

            // 3. 注册无限资源守护系统（GameSimulation 阶段，与原版资源系统同步）
            updateSystem.UpdateAt<ResourceGuardSystem>(SystemUpdatePhase.GameSimulation);

            Logger.Info($"{ModName} loaded successfully.");
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
