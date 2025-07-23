// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Colossal.Serialization.Entities;
using Game.Net;
using Game.Serialization;
using Game.Simulation;
using HarmonyLib;
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
    [HarmonyPatch(typeof(AirwaySystem))]
    public static class AirwaySystem_OnUpdate_Patch
    {
        // A "session lock" to ensure logic runs only once per game load/new game.
        private static bool s_HasRunThisSession = false;

        // --- Reflection Fields - Meticulously defined to match the source code ---

        // Fields for AirwaySystem
        private static readonly FieldInfo m_AirwayDataField = AccessTools.Field(typeof(AirwaySystem), "m_AirwayData");
        private static readonly FieldInfo m_LoadGameSystemField = AccessTools.Field(typeof(AirwaySystem), "m_LoadGameSystem");
        private static readonly FieldInfo m_TerrainSystemField = AccessTools.Field(typeof(AirwaySystem), "m_TerrainSystem");
        private static readonly FieldInfo m_WaterSystemField = AccessTools.Field(typeof(AirwaySystem), "m_WaterSystem");

        // Properties for AirwayHelpers.AirwayData
        private static readonly PropertyInfo m_HelicopterMapProperty = AccessTools.Property(typeof(AirwayHelpers.AirwayData), "helicopterMap");
        private static readonly PropertyInfo m_AirplaneMapProperty = AccessTools.Property(typeof(AirwayHelpers.AirwayData), "airplaneMap");

        // Fields for AirwayHelpers.AirwayMap
        private static readonly FieldInfo m_GridSizeField = AccessTools.Field(typeof(AirwayHelpers.AirwayMap), "m_GridSize");
        private static readonly FieldInfo m_CellSizeField = AccessTools.Field(typeof(AirwayHelpers.AirwayMap), "m_CellSize");
        private static readonly FieldInfo m_PathHeightField = AccessTools.Field(typeof(AirwayHelpers.AirwayMap), "m_PathHeight");
        private static readonly FieldInfo m_EntitiesField = AccessTools.Field(typeof(AirwayHelpers.AirwayMap), "m_Entities");

        private static readonly PropertyInfo DependencyProperty = AccessTools.Property(typeof(SystemBase), "Dependency");


        // This runs BEFORE the original OnUpdate. Its only job is to manage the session lock.
        [HarmonyPatch("OnUpdate")]
        [HarmonyPrefix]
        public static void Prefix(AirwaySystem __instance)
        {
            try
            {
                var loadGameSystem = (LoadGameSystem)m_LoadGameSystemField.GetValue(__instance);

                // When exiting to main menu, the game runs a "Cleanup" cycle. use this to reset session lock.
                if (loadGameSystem.context.purpose == Purpose.Cleanup)
                {
                    if (s_HasRunThisSession)
                    {
                        Mod.Info("[Airway Patch] Game session is ending (Cleanup). Resetting session lock.");
                        s_HasRunThisSession = false;
                    }
                }
            }
            catch (Exception e)
            {
                Mod.Error(e, "Error in AirwaySystem OnUpdate Prefix!");
            }
        }

        // This runs AFTER the original OnUpdate. 主要逻辑.
        [HarmonyPatch("OnUpdate")]
        [HarmonyPostfix]
        public static void Postfix(AirwaySystem __instance)
        {
            // --- Guard Clause: If already run this session, do nothing ---
            if (s_HasRunThisSession)
            {
                return;
            }

            try
            {
                Mod.Info("=================================================");
                Mod.Info("[Airway Patch Postfix] Starting airway check...");

                // --- Get necessary systems and data using reflection ---
                var loadGameSystem = (LoadGameSystem)m_LoadGameSystemField.GetValue(__instance);
                var terrainSystem = (TerrainSystem)m_TerrainSystemField.GetValue(__instance);
                var waterSystem = (WaterSystem)m_WaterSystemField.GetValue(__instance);

                var purpose = loadGameSystem.context.purpose;
                Mod.Info($"[Airway Patch Postfix] Current game purpose: {purpose}");

                // only care about modifying airways in a real game or map editor session.
                if (purpose != Purpose.NewGame && purpose != Purpose.LoadGame && purpose != Purpose.NewMap && purpose != Purpose.LoadMap)
                {
                    Mod.Info($"[Airway Patch Postfix] Purpose is not relevant. Skipping.");
                    return;
                }

                // Get the AirwayData struct from the system instance
                object airwayDataObject = m_AirwayDataField.GetValue(__instance);

                // Get the helicopterMap struct (as a boxed object) to check its size
                object heliMapObject = m_HelicopterMapProperty.GetValue(airwayDataObject);
                float currentCellSize = (float)m_CellSizeField.GetValue(heliMapObject);

                // --- Define custom target size ---
                float mapSize = 14336f * PatchManager.CurrentCoreValue;
                float targetHeliCellSize = mapSize / 29f;

                Mod.Info($"[Airway Patch Postfix] Current CellSize: {currentCellSize}, Target CellSize: {targetHeliCellSize}");

                // --- The Core Logic: Check if modification is needed ---
                if (Mathf.Approximately(currentCellSize, targetHeliCellSize))
                {
                    Mod.Info("[Airway Patch Postfix] Sizes already match. No action needed. Engaging session lock.");
                    s_HasRunThisSession = true;
                    return;
                }

                Mod.Info("[Airway Patch Postfix] Size mismatch detected. Rebuilding airway data in memory and scheduling update job...");

                // --- Rebuild AirwayData in memory with new sizes but OLD entities ---
                // not creating or destroying anything, just creating new C# structs.

                // 1. Get all data from the OLD helicopter map
                object oldHeliMapObject = m_HelicopterMapProperty.GetValue(airwayDataObject);
                int2 oldHeliGridSize = (int2)m_GridSizeField.GetValue(oldHeliMapObject);
                float oldHeliPathHeight = (float)m_PathHeightField.GetValue(oldHeliMapObject);
                var oldHeliEntities = (NativeArray<Entity>)m_EntitiesField.GetValue(oldHeliMapObject);

                // 2. Get all data from the OLD airplane map
                object oldAirplaneMapObject = m_AirplaneMapProperty.GetValue(airwayDataObject);
                int2 oldAirplaneGridSize = (int2)m_GridSizeField.GetValue(oldAirplaneMapObject);
                float oldAirplanePathHeight = (float)m_PathHeightField.GetValue(oldAirplaneMapObject);
                var oldAirplaneEntities = (NativeArray<Entity>)m_EntitiesField.GetValue(oldAirplaneMapObject);
                float targetAirplaneCellSize = targetHeliCellSize * 2f;

                // 3. Create NEW map structs with the new sizes
                // pass Allocator.None because NOT allocating new arrays. The constructor will just store our references.
                // CORRECTION: The constructor *always* allocates. So it must dispose the old and create new with proper allocator.
                // re-read AirwayMap constructor. It takes an allocator. So it must provide one.
                var newHeliMap = new AirwayHelpers.AirwayMap(oldHeliGridSize, targetHeliCellSize, oldHeliPathHeight, Allocator.Persistent);
                newHeliMap.entities.CopyFrom(oldHeliEntities); // Copy entity references

                var newAirplaneMap = new AirwayHelpers.AirwayMap(oldAirplaneGridSize, targetAirplaneCellSize, oldAirplanePathHeight, Allocator.Persistent);
                newAirplaneMap.entities.CopyFrom(oldAirplaneEntities); // Copy entity references

                // 4. Create a new top-level data struct
                var newAirwayData = new AirwayHelpers.AirwayData(newHeliMap, newAirplaneMap);

                // 5. Replace the system's data struct with new one using reflection
                m_AirwayDataField.SetValue(__instance, newAirwayData);

                Mod.Info("[Airway Patch Postfix] AirwayData in memory replaced successfully.");

                // --- Schedule the job to update the curve positions of existing entities ---
                var updateCurvesJob = new UpdateAirwayCurvesJob
                {
                    m_HelicopterMap = newHeliMap,
                    m_AirplaneMap = newAirplaneMap,
                    m_TerrainHeightData = terrainSystem.GetHeightData(true),
                    m_WaterSurfaceData = waterSystem.GetSurfaceData(out var waterDep),
                    m_CurveData = __instance.GetComponentLookup<Curve>(false) // isReadOnly = false
                };

                // 1. GET the current dependency using reflection.
                JobHandle currentDependency = (JobHandle)DependencyProperty.GetValue(__instance);

                // 2. Combine it with the dependencies for our job.
                JobHandle combinedDeps = JobHandle.CombineDependencies(currentDependency, waterDep);

                // 3. Schedule our job.
                JobHandle updateJobHandle = updateCurvesJob.Schedule(combinedDeps);

                // These calls are the same
                terrainSystem.AddCPUHeightReader(updateJobHandle);
                waterSystem.AddSurfaceReader(updateJobHandle);

                // 4. SET the system's dependency to new job's handle using reflection.
                DependencyProperty.SetValue(__instance, updateJobHandle);

                Mod.Info("[Airway Patch Postfix] Update job scheduled. Engaging session lock.");
                s_HasRunThisSession = true;
            }
            catch (Exception e)
            {
                Mod.Error(e, "A critical error occurred in AirwaySystem OnUpdate Postfix!");
            }
            finally
            {
                Mod.Info("=================================================\n");
            }
        }

        // A lightweight job that ONLY updates the Curve component of existing entities.
        [BurstCompile]
        private struct UpdateAirwayCurvesJob : IJob
        {
            [ReadOnly]
            public AirwayHelpers.AirwayMap m_HelicopterMap;
            [ReadOnly]
            public AirwayHelpers.AirwayMap m_AirplaneMap;
            [ReadOnly]
            public TerrainHeightData m_TerrainHeightData;
            [ReadOnly]
            public WaterSurfaceData m_WaterSurfaceData;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<Curve> m_CurveData;

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
                nodePosition.y += WaterUtils.SampleHeight(ref m_WaterSurfaceData, ref m_TerrainHeightData, nodePosition);
                nodePosition2.y += WaterUtils.SampleHeight(ref m_WaterSurfaceData, ref m_TerrainHeightData, nodePosition2);

                Curve value2 = default(Curve);
                value2.m_Bezier = NetUtils.StraightCurve(nodePosition, nodePosition2);
                value2.m_Length = math.distance(value2.m_Bezier.a, value2.m_Bezier.d);

                m_CurveData[entity] = value2;
            }
        }
    }
}
