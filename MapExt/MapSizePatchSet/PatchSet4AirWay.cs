// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Game.Net;
using HarmonyLib;
using Unity.Collections;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    /// <summary>
    /// 修补AirwaySystem寻路错误
    /// </summary>
    [HarmonyPatch(typeof(AirwaySystem), "OnCreate")]
    public class AirwaySystem_OnCreate_Patch
    {

        public static void Postfix(AirwaySystem __instance)
        {
            // 1. 定义新参数 
            // 引入当前设置CV值；
            int scaleMultiplier = PatchManager.CurrentCoreValue;
            int newHelicopterGridWidth = 28 * scaleMultiplier;
            float newTerrainSize = 14336f * scaleMultiplier;
            float newHelicopterCellSize = newTerrainSize / (newHelicopterGridWidth + 1); // 57344f / 113f ≈ 507.46902f
            int newAirplaneGridWidth = newHelicopterGridWidth / 2;
            float newAirplaneCellSize = newHelicopterCellSize * 2; // ≈ 1014.93804f

            // Mod.log.Info("Patching AirwaySystem.OnCreate...");
            // Mod.log.Info($"New helicopter grid: {newHelicopterGridWidth}x{newHelicopterGridWidth}, cell size: {newHelicopterCellSize}");
            // Mod.log.Info($"New airplane grid: {newAirplaneGridWidth}x{newAirplaneGridWidth}, cell size: {newAirplaneCellSize}");

            // 2. 访问私有字段 m_AirwayData
            var airwayDataField = AccessTools.Field(typeof(AirwaySystem), "m_AirwayData");
            if (airwayDataField == null)
            {
                // 如果找不到字段，记录错误并退出，防止 Mod 崩溃
                // Mod.log.Error("Could not find m_AirwayData field in AirwaySystem.");
                return;
            }

            // 获取原始 OnCreate 创建的旧 AirwayData
            AirwayHelpers.AirwayData oldAirwayData = (AirwayHelpers.AirwayData)airwayDataField.GetValue(__instance);

            // 3. 销毁旧数据以防止内存泄漏
            // Dispose() 会释放旧的 NativeArray<Entity>
            oldAirwayData.Dispose();

            // 4. 创建新的 AirwayMap 和 AirwayData 实例
            AirwayHelpers.AirwayMap newHelicopterMap = new AirwayHelpers.AirwayMap(
                new int2(newHelicopterGridWidth, newHelicopterGridWidth),
                newHelicopterCellSize,
                200f, // 高度可以保持不变，或按需修改
                Allocator.Persistent);

            AirwayHelpers.AirwayMap newAirplaneMap = new AirwayHelpers.AirwayMap(
                new int2(newAirplaneGridWidth, newAirplaneGridWidth),
                newAirplaneCellSize,
                1000f, // 高度可以保持不变，或按需修改
                Allocator.Persistent);

            AirwayHelpers.AirwayData newAirwayData = new AirwayHelpers.AirwayData(newHelicopterMap, newAirplaneMap);

            // 5. 将新创建的数据设置回 AirwaySystem 实例的私有字段中
            airwayDataField.SetValue(__instance, newAirwayData);

            Mod.Info("AirwaySystem patched successfully with new map sizes.");
        }
    }
}
