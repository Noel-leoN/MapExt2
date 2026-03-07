using HarmonyLib;
using Game.Simulation;
using Unity.Mathematics;

namespace EconomyEX.Detection
{
    public static class MapSizeDetector
    {
        public static bool HasCheckedMapSize { get; private set; } = false;
        private const float VanillaMapSize = 14336f;

        public static void Install(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(MapSizeDetectorPatch)).Patch();
        }

        [HarmonyPatch(typeof(TerrainSystem), "FinalizeTerrainData")]
        public static class MapSizeDetectorPatch
        {
            [HarmonyPrefix]
            public static void Prefix(ref float2 inMapSize)
            {
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
