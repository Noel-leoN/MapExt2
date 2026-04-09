// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Colossal.Serialization.Entities;
using Game.Net;
using Game.Serialization;
using Game.Simulation;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using System;
using System.Reflection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    [HarmonyPatch]
    [HarmonyPatch(typeof(AirwaySystem))]
    public static class AirwaySystem_OnUpdate_Patch
    {
        private const string Tag = "AirwayPatch";

        // 会话锁：确保每次加载/新建游戏仅执行一次
        private static bool s_HasRunThisSession = false;

        #region Reflection Fields

        // AirwaySystem 实例字段
        private static readonly FieldInfo m_AirwayDataField = AccessTools.Field(typeof(AirwaySystem), "m_AirwayData");
        private static readonly FieldInfo m_LoadGameSystemField = AccessTools.Field(typeof(AirwaySystem), "m_LoadGameSystem");
        private static readonly FieldInfo m_TerrainSystemField = AccessTools.Field(typeof(AirwaySystem), "m_TerrainSystem");
        private static readonly FieldInfo m_WaterSystemField = AccessTools.Field(typeof(AirwaySystem), "m_WaterSystem");

        // AirwayData 属性（helicopterMap/airplaneMap 均为 { get; private set; }）
        private static readonly PropertyInfo m_HelicopterMapProperty = AccessTools.Property(typeof(AirwayHelpers.AirwayData), "helicopterMap");
        private static readonly PropertyInfo m_AirplaneMapProperty = AccessTools.Property(typeof(AirwayHelpers.AirwayData), "airplaneMap");

        // AirwayMap 唯一需要修改的字段
        private static readonly FieldInfo m_CellSizeField = AccessTools.Field(typeof(AirwayHelpers.AirwayMap), "m_CellSize");

        // SystemBase.Dependency 属性
        private static readonly PropertyInfo DependencyProperty = AccessTools.Property(typeof(SystemBase), "Dependency");

        #endregion

        // === Prefix: 管理会话锁 ===
        [HarmonyPatch(typeof(AirwaySystem), "OnUpdate")]
        [HarmonyPrefix]
        public static void Prefix(AirwaySystem __instance)
        {
            try
            {
                var loadGameSystem = (LoadGameSystem)m_LoadGameSystemField.GetValue(__instance);

                // 退出主菜单时游戏运行 Cleanup 周期，重置会话锁
                if (loadGameSystem.context.purpose == Purpose.Cleanup && s_HasRunThisSession)
                {
                    ModLog.Info(Tag, "Game session ending (Cleanup). Resetting session lock.");
                    s_HasRunThisSession = false;
                }
            }
            catch (Exception e)
            {
                ModLog.Error(Tag, e, "Error in AirwaySystem OnUpdate Prefix!");
            }
        }

        // === Postfix: 检测并原地修正 CellSize，重算 Curve ===
        [HarmonyPatch(typeof(AirwaySystem), "OnUpdate")]
        [HarmonyPostfix]
        public static void Postfix(AirwaySystem __instance)
        {
            if (s_HasRunThisSession) return;

            try
            {
                var loadGameSystem = (LoadGameSystem)m_LoadGameSystemField.GetValue(__instance);
                var purpose = loadGameSystem.context.purpose;

                // 仅在实际游戏/地图编辑器会话中修改航路
                if (purpose != Purpose.NewGame && purpose != Purpose.LoadGame &&
                    purpose != Purpose.NewMap && purpose != Purpose.LoadMap)
                    return;

                // --- 读取当前 CellSize ---
                object airwayDataBox = m_AirwayDataField.GetValue(__instance);
                object heliMapBox = m_HelicopterMapProperty.GetValue(airwayDataBox);
                float currentCellSize = (float)m_CellSizeField.GetValue(heliMapBox);

                // --- 计算目标 CellSize ---
                float targetHeliCellSize = 14336f * PatchManager.CurrentCoreValue / 29f;

                if (Mathf.Approximately(currentCellSize, targetHeliCellSize))
                {
                    ModLog.Ok(Tag, $"CellSize already matches ({currentCellSize:F2}). Session lock engaged.");
                    s_HasRunThisSession = true;
                    return;
                }

                // === 原地修改 CellSize（零分配） ===
                // AirwayMap 是 struct，通过 boxed object 修改后写回
                float targetAirplaneCellSize = targetHeliCellSize * 2f;

                // 1. 修改直升机 map 的 CellSize
                m_CellSizeField.SetValue(heliMapBox, targetHeliCellSize);
                m_HelicopterMapProperty.SetValue(airwayDataBox, heliMapBox);

                // 2. 修改飞机 map 的 CellSize
                object airplaneMapBox = m_AirplaneMapProperty.GetValue(airwayDataBox);
                m_CellSizeField.SetValue(airplaneMapBox, targetAirplaneCellSize);
                m_AirplaneMapProperty.SetValue(airwayDataBox, airplaneMapBox);

                // 3. 将修改后的 AirwayData 写回系统
                m_AirwayDataField.SetValue(__instance, airwayDataBox);

                // === 调度 Job 重算所有航路的 Curve 位置 ===
                var terrainSystem = (TerrainSystem)m_TerrainSystemField.GetValue(__instance);
                var waterSystem = (WaterSystem)m_WaterSystemField.GetValue(__instance);

                // unbox 获取修改后的 map struct（NativeArray 引用共享同一块内存）
                var modifiedHeliMap = (AirwayHelpers.AirwayMap)heliMapBox;
                var modifiedAirplaneMap = (AirwayHelpers.AirwayMap)airplaneMapBox;

                var updateCurvesJob = new UpdateAirwayCurvesJob
                {
                    m_HelicopterMap = modifiedHeliMap,
                    m_AirplaneMap = modifiedAirplaneMap,
                    m_TerrainHeightData = terrainSystem.GetHeightData(true),
                    m_WaterSurfaceData = waterSystem.GetSurfaceData(out var waterDep),
                    m_CurveData = __instance.GetComponentLookup<Curve>(false)
                };

                // 组合依赖 → 调度 → 注册 reader → 设置系统 Dependency
                JobHandle currentDep = (JobHandle)DependencyProperty.GetValue(__instance);
                JobHandle jobHandle = updateCurvesJob.Schedule(JobHandle.CombineDependencies(currentDep, waterDep));

                terrainSystem.AddCPUHeightReader(jobHandle);
                waterSystem.AddSurfaceReader(jobHandle);
                DependencyProperty.SetValue(__instance, jobHandle);

                ModLog.Swap(Tag, $"CellSize patched in-place: Heli={targetHeliCellSize:F2}, Airplane={targetAirplaneCellSize:F2}. Curves job scheduled.");
                s_HasRunThisSession = true;
            }
            catch (Exception e)
            {
                ModLog.Error(Tag, e, "Critical error in AirwaySystem OnUpdate Postfix!");
            }
        }

        // A lightweight job that ONLY updates the Curve component of existing entities.
        [BurstCompile]
        private struct UpdateAirwayCurvesJob : IJob
        {
            [ReadOnly] public AirwayHelpers.AirwayMap m_HelicopterMap;
            [ReadOnly] public AirwayHelpers.AirwayMap m_AirplaneMap;
            [ReadOnly] public TerrainHeightData m_TerrainHeightData;
            [ReadOnly] public WaterSurfaceData<SurfaceWater> m_WaterSurfaceData;

            [NativeDisableParallelForRestriction] public ComponentLookup<Curve> m_CurveData;

            public void Execute()
            {
                // Update all helicopter lanes
                for (int i = 0; i < m_HelicopterMap.entities.Length; i++)
                {
                    UpdateLane(i, m_HelicopterMap);
                }

                // Update all airplane lanes
                for (int i = 0; i < m_AirplaneMap.entities.Length; i++)
                {
                    UpdateLane(i, m_AirplaneMap);
                }
            }

            private void UpdateLane(int entityIndex, AirwayHelpers.AirwayMap map)
            {
                Entity entity = map.entities[entityIndex];
                if (entity == Entity.Null || !m_CurveData.HasComponent(entity))
                {
                    return;
                }

                AirwayHelpers.LaneDirection direction;
                int2 cellIndex = map.GetCellIndex(entityIndex, out direction);

                int2 startNode, endNode;
                switch (direction)
                {
                    case AirwayHelpers.LaneDirection.HorizontalZ:
                        startNode = cellIndex;
                        endNode = new int2(cellIndex.x, cellIndex.y + 1);
                        break;
                    case AirwayHelpers.LaneDirection.HorizontalX:
                        startNode = cellIndex;
                        endNode = new int2(cellIndex.x + 1, cellIndex.y);
                        break;
                    case AirwayHelpers.LaneDirection.Diagonal:
                        startNode = cellIndex;
                        endNode = cellIndex + 1;
                        break;
                    case AirwayHelpers.LaneDirection.DiagonalCross:
                        startNode = new int2(cellIndex.x + 1, cellIndex.y);
                        endNode = new int2(cellIndex.x, cellIndex.y + 1);
                        break;
                    default:
                        return; // Should not happen
                }

                float3 nodePosition = map.GetNodePosition(startNode);
                float3 nodePosition2 = map.GetNodePosition(endNode);
                nodePosition.y +=
                    WaterUtils.SampleHeight(ref m_WaterSurfaceData, ref m_TerrainHeightData, nodePosition);
                nodePosition2.y +=
                    WaterUtils.SampleHeight(ref m_WaterSurfaceData, ref m_TerrainHeightData, nodePosition2);

                Curve value2 = default(Curve);
                value2.m_Bezier = NetUtils.StraightCurve(nodePosition, nodePosition2);
                value2.m_Length = math.distance(value2.m_Bezier.a, value2.m_Bezier.d);

                m_CurveData[entity] = value2;
            }
        }
    }
}
