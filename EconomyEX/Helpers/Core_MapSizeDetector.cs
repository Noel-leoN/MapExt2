using HarmonyLib;
using Game.Simulation;
using Unity.Mathematics;

namespace EconomyEX.Helpers
{
    public static class MapSizeDetector
    {
        public static bool HasCheckedMapSize { get; private set; } = false;
        private const float VanillaMapSize = 14336f;

        /// <summary>
        /// 本次載入偵測到大地圖存檔——它需要 MapExt 才能正確載入，而 EconomyEX 不提供擴容。
        /// 由 <see cref="ConflictMonitoringSystem"/> 在 OnGameLoadingComplete 消費並彈窗，
        /// 因為 Prefix 執行時仍在反序列化中途、UI 尚未就緒，無法直接提示。
        /// </summary>
        public static bool HasLargeMapWarning { get; private set; } = false;

        /// <summary>偵測到的地圖邊長（公尺），供提示文案使用。</summary>
        public static float DetectedMapSize { get; private set; } = 0f;

        /// <summary>已對本次載入彈過提示，避免重複打擾。</summary>
        public static bool WarningDialogShown { get; set; } = false;

        public static void Install(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(MapSizeDetectorPatch)).Patch();
        }

        /// <summary>
        /// [BUGFIX] 场景切换时重置所有静态状态标志。
        /// 由 Prefix 在每次 FinalizeTerrainData 时自动调用。
        /// </summary>
        internal static void ResetState()
        {
            HasCheckedMapSize = false;
            HasLargeMapWarning = false;
            DetectedMapSize = 0f;
            WarningDialogShown = false;
        }

        [HarmonyPatch(typeof(TerrainSystem), "FinalizeTerrainData")]
        public static class MapSizeDetectorPatch
        {
            [HarmonyPrefix]
            public static void Prefix(ref float2 inMapSize)
            {
                // [BUGFIX] 每次加载新地图时先重置状态，确保不残留上一次场景的过期标志
                ResetState();

                HasCheckedMapSize = true;
                Mod.Info($"Map Load Detected. Size: {inMapSize.x}x{inMapSize.y}");

                // Check if map is basically vanilla size (allow small float error)
                // 14336 is the standard size.
                if (inMapSize.x <= VanillaMapSize + 1.0f)
                {
                    Mod.Info("Vanilla Map Size verified.");
                    Mod.Instance?.ActivateEconomyFix();
                }
                else
                {
                    Mod.Warn($"Large Map Detected ({inMapSize.x}). EconomyEX will remain inactive.");

                    // 記錄供 OnGameLoadingComplete 彈窗：此存檔需要 MapExt 才能正確載入，
                    // 缺 MapExt 時地形取樣比例錯誤、存檔實質不可遊玩，而這一側原本只寫 log，
                    // 玩家看不到任何提示。inMapSize 只在此刻還是地圖檔的原始權威值。
                    HasLargeMapWarning = true;
                    DetectedMapSize = inMapSize.x;

                    Mod.Instance?.DeactivateEconomyFix();
                }
            }
        }
    }
}
