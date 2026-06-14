using HarmonyLib;
using Game.Simulation;
using Unity.Mathematics;

namespace EconomyEX.Helpers
{
    public static class MapSizeDetector
    {
        public static bool HasCheckedMapSize { get; private set; } = false;
        private const float VanillaMapSize = 14336f;

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
                    Mod.Instance?.DeactivateEconomyFix();
                }
            }
        }
    }
}
