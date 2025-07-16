// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Serialization;
using Game.Simulation;
using HarmonyLib;
using System.Reflection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    [HarmonyPatch(typeof(AirwaySystem), "OnUpdate")]
    public static class AirwaySystem_OnUpdate_Patch
    {
        // --- THE SESSION LOCK ---
        public static bool s_HasRunThisSession = false;

        // --- REFLECTION CACHE ---
        // Caching reflection info is crucial for performance. 
        private static readonly FieldInfo m_AirwayDataField = AccessTools.Field(typeof(AirwaySystem), "m_AirwayData");
        private static readonly FieldInfo m_LoadGameSystemField = AccessTools.Field(typeof(AirwaySystem), "m_LoadGameSystem");
        private static readonly FieldInfo m_TerrainSystemField = AccessTools.Field(typeof(AirwaySystem), "m_TerrainSystem");
        private static readonly FieldInfo m_WaterSystemField = AccessTools.Field(typeof(AirwaySystem), "m_WaterSystem");
        private static readonly FieldInfo m_PrefabQueryField = AccessTools.Field(typeof(AirwaySystem), "m_PrefabQuery");
        private static readonly FieldInfo m_AirplaneConnectionQueryField = AccessTools.Field(typeof(AirwaySystem), "m_AirplaneConnectionQuery");
        private static readonly FieldInfo m_OldConnectionQueryField = AccessTools.Field(typeof(AirwaySystem), "m_OldConnectionQuery");
        private static readonly PropertyInfo DependencyProperty = AccessTools.Property(typeof(SystemBase), "Dependency");

        // --- AirwayMap Internal Fields ---
        // reflection to read the private m_CellSize for comparison.
        private static readonly FieldInfo am_CellSizeField = AccessTools.Field(typeof(AirwayHelpers.AirwayMap), "m_CellSize");

        /// <summary>
        /// This Harmony Prefix takes complete control of the AirwaySystem's OnUpdate on the first frame.
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(AirwaySystem __instance)
        {
            
            // --- Log Header ---
            Mod.Info("=====================================================");
            Mod.Info("[Airway Patch] First frame detected. Intercepting OnUpdate.");

            var loadGameSystem = (LoadGameSystem)m_LoadGameSystemField.GetValue(__instance);
            var purpose = loadGameSystem.context.purpose;
            // --- DIAGNOSTIC LOG ---
            Mod.Info($"--- AirwaySystem.OnUpdate called. Purpose: {purpose}, SessionLock: {s_HasRunThisSession} ---");

            // ===== THE CRITICAL, PURPOSE-BASED GUARD CLAUSE =====
            if (purpose == Purpose.Cleanup)
            {
                Mod.Info("[Airway Patch] Purpose is 'Cleanup'. Letting original method run. No action taken.");
                return true; // Let the original OnUpdate run for cleanup.
            }

            // check our session lock. If already run, do nothing.
            if (s_HasRunThisSession)
            {
                return true; // work is done for this session.
            }

            // --- Log Header ---
            Mod.Info("=====================================================");
            Mod.Info($"[Airway Patch] First '{purpose}' frame detected. Intercepting OnUpdate.");

            // Decide action based on purpose
            if (purpose == Purpose.LoadGame || purpose == Purpose.LoadMap)
            {
                Mod.Info($"[Airway Patch] Context: LOAD ({purpose}). Preparing to destroy old grid.");

                DestroyExistingAirwayEntities(__instance);
            }
            else // This will be NewGame or NewMap
            {
                Mod.Info($"[Airway Patch] Context: NEW ({purpose}). Disposing template data.");
                var oldData = (AirwayHelpers.AirwayData)m_AirwayDataField.GetValue(__instance);
                oldData.Dispose();
            }

            // Generate the new airways with custom logic
            GenerateCustomAirways(__instance);

            // Engage the session lock so this logic doesn't run again until exit to menu and reset.
            s_HasRunThisSession = true;
            Mod.Info("[Airway Patch] All operations complete. Session lock engaged.");
            Mod.Info("=====================================================");

            // VERY IMPORTANT: Return false to PREVENT the original OnUpdate from running.
            return false;
        }

        /// <summary>
        /// Finds and destroys all entities created by a previous AirwaySystem run.
        /// This is critical for loading a save file.
        /// </summary>
        private static void DestroyExistingAirwayEntities(AirwaySystem __instance)
        {
            Mod.Info("[Airway Patch] Destroying old airway entities...");
            var oldData = (AirwayHelpers.AirwayData)m_AirwayDataField.GetValue(__instance);
            int destroyedCount = 0;

            // Destroy Helicopter Entities
            if (!oldData.helicopterMap.entities.IsCreated)
            {
                Mod.Warn("[Airway Patch] Helicopter entities array was not created. Nothing to destroy.");
            }
            else
            {
                foreach (var entity in oldData.helicopterMap.entities)
                {
                    if (__instance.EntityManager.Exists(entity))
                    {
                        __instance.EntityManager.DestroyEntity(entity);
                        destroyedCount++;
                    }
                }
            }

            // Destroy Airplane Entities
            if (!oldData.airplaneMap.entities.IsCreated)
            {
                Mod.Warn("[Airway Patch] Airplane entities array was not created. Nothing to destroy.");
            }
            else
            {
                foreach (var entity in oldData.airplaneMap.entities)
                {
                    if (__instance.EntityManager.Exists(entity))
                    {
                        __instance.EntityManager.DestroyEntity(entity);
                        destroyedCount++;
                    }
                }
            }

            oldData.Dispose(); // Dispose the old native arrays and data structure
            Mod.Info($"[Airway Patch] Destroyed {destroyedCount} old airway entities and disposed old data.");
        }


        /// <summary>
        /// This is a meticulous 1-to-1 recreation of the original OnUpdate logic,
        /// but using custom map sizes.
        /// </summary>
        private static void GenerateCustomAirways(AirwaySystem __instance)
        {
            Mod.Info("[Airway Patch] Starting custom airway generation process...");

            // --- 1. Define Custom Sizes & Create New AirwayData ---
            float mapSize = PatchManager.CurrentCoreValue * 14336f;
            float helicopterCellSize = mapSize / 29f;
            float airplaneCellSize = helicopterCellSize * 2f;

            Mod.Info($"[Airway Patch] Using custom sizes: Heli Cell={helicopterCellSize}, Plane Cell={airplaneCellSize}");

            var helicopterMap = new AirwayHelpers.AirwayMap(new int2(28, 28), helicopterCellSize, 200f, Allocator.Persistent);
            var airplaneMap = new AirwayHelpers.AirwayMap(new int2(14, 14), airplaneCellSize, 1000f, Allocator.Persistent);
            var newAirwayData = new AirwayHelpers.AirwayData(helicopterMap, airplaneMap);

            // Overwrite the system's m_AirwayData with new, correctly-sized one.
            m_AirwayDataField.SetValue(__instance, newAirwayData);
            Mod.Info("[Airway Patch] New custom AirwayData created and set in system.");

            // --- 2. Replicate Original OnUpdate Logic ---
            var prefabQuery = (EntityQuery)m_PrefabQueryField.GetValue(__instance);

            using (var nativeArray = prefabQuery.ToEntityArray(Allocator.TempJob))
            {
                if (nativeArray.Length == 0)
                {
                    Mod.Warn("[Airway Patch] Prefab for 'Airway Lane' not found. Cannot generate airways.");
                    return;
                }

                Entity prefabEntity = nativeArray[0];
                var componentData = __instance.EntityManager.GetComponentData<NetLaneArchetypeData>(prefabEntity);

                Mod.Info("[Airway Patch] Checking for airplane connections to update...");
                var airplaneConnectionQuery = (EntityQuery)m_AirplaneConnectionQueryField.GetValue(__instance);

                if (!airplaneConnectionQuery.IsEmptyIgnoreFilter)
                {
                    int count = airplaneConnectionQuery.CalculateEntityCount();
                    __instance.EntityManager.AddComponent<Updated>(airplaneConnectionQuery);
                    Mod.Info($"[Airway Patch] Found {count} airplane connections. Added <Updated> component to notify other systems.");
                }
                else
                {
                    Mod.Info("[Airway Patch] No active airplane connections found to update.");
                }

                // Create the blank entities that the job will populate.
                __instance.EntityManager.CreateEntity(componentData.m_LaneArchetype, newAirwayData.helicopterMap.entities);
                __instance.EntityManager.CreateEntity(componentData.m_LaneArchetype, newAirwayData.airplaneMap.entities);
                Mod.Info($"[Airway Patch] Created {newAirwayData.helicopterMap.entities.Length} blank helicopter and {newAirwayData.airplaneMap.entities.Length} blank airplane entities.");

                // Get systems and data exactly like the original.
                var terrainSystem = (TerrainSystem)m_TerrainSystemField.GetValue(__instance);
                var waterSystem = (WaterSystem)m_WaterSystemField.GetValue(__instance);
                var heightData = terrainSystem.GetHeightData(true);
                var surfaceData = waterSystem.GetSurfaceData(out var deps);

                var prefabRefLookup = __instance.GetComponentLookup<PrefabRef>(false);
                var laneLookup = __instance.GetComponentLookup<Lane>(false);
                var curveLookup = __instance.GetComponentLookup<Curve>(false);
                var connectionLaneLookup = __instance.GetComponentLookup<Game.Net.ConnectionLane>(false);

                // --- 3. Setup and Schedule Jobs, mirroring the original ---
                var helicopterJob = new GenerateAirwayLanesJob
                {
                    m_AirwayMap = newAirwayData.helicopterMap,
                    m_Prefab = prefabEntity,
                    m_RoadType = RoadTypes.Helicopter,
                    m_TerrainHeightData = heightData,
                    m_WaterSurfaceData = surfaceData,
                    m_PrefabRefData = prefabRefLookup,
                    m_LaneData = laneLookup,
                    m_CurveData = curveLookup,
                    m_ConnectionLaneData = connectionLaneLookup
                };

                // Combine dependencies and schedule the first job.
                var currentDependency = (JobHandle)AccessTools.Property(typeof(SystemBase), "Dependency").GetValue(__instance);
                var helicopterJobHandle = helicopterJob.Schedule(newAirwayData.helicopterMap.entities.Length, 4, JobHandle.CombineDependencies(currentDependency, deps));


                var airplaneJob = new GenerateAirwayLanesJob
                {
                    m_AirwayMap = newAirwayData.airplaneMap,
                    m_Prefab = prefabEntity,
                    m_RoadType = RoadTypes.Airplane,
                    m_TerrainHeightData = heightData,
                    m_WaterSurfaceData = surfaceData,
                    m_PrefabRefData = prefabRefLookup,
                    m_LaneData = laneLookup,
                    m_CurveData = curveLookup,
                    m_ConnectionLaneData = connectionLaneLookup
                };

                // Chain the second job to the first one.
                var airplaneJobHandle = airplaneJob.Schedule(newAirwayData.airplaneMap.entities.Length, 4, helicopterJobHandle);

                Mod.Info("[Airway Patch] Generation jobs scheduled successfully.");

                // --- 4. Finalize Dependencies ---
                terrainSystem.AddCPUHeightReader(airplaneJobHandle);
                waterSystem.AddSurfaceReader(airplaneJobHandle);

                // Safely set the final dependency back to the system using reflection.
                AccessTools.Property(typeof(SystemBase), "Dependency").SetValue(__instance, airplaneJobHandle);
            } // nativeArray is automatically disposed here by the 'using' block.
        }

        public static void RequestManualRegeneration()
        {
            // 安全检查：确保玩家当前在游戏中，而不是在主菜单或加载界面。
            if (GameManager.instance.gameMode != GameMode.Game)
            {
                Mod.Info("[Airway Patch] Manual regeneration requested, but not in an active game session. Request ignored.");
                return;
            }

            // 如果已经在游戏中，我们就可以安全地重置会话锁。
            Mod.Info("[Airway Patch] Manual regeneration requested via UI. Resetting session lock.");
            s_HasRunThisSession = false;
        }


        [BurstCompile]
        private struct GenerateAirwayLanesJob : IJobParallelFor
        {
            [ReadOnly]
            public AirwayHelpers.AirwayMap m_AirwayMap;

            [ReadOnly]
            public Entity m_Prefab;

            [ReadOnly]
            public RoadTypes m_RoadType;

            [ReadOnly]
            public TerrainHeightData m_TerrainHeightData;

            [ReadOnly]
            public WaterSurfaceData m_WaterSurfaceData;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<PrefabRef> m_PrefabRefData;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<Lane> m_LaneData;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<Curve> m_CurveData;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<Game.Net.ConnectionLane> m_ConnectionLaneData;

            public void Execute(int entityIndex)
            {
                AirwayHelpers.LaneDirection direction;
                int2 cellIndex = this.m_AirwayMap.GetCellIndex(entityIndex, out direction);
                switch (direction)
                {
                    case AirwayHelpers.LaneDirection.HorizontalZ:
                        this.CreateLane(entityIndex, cellIndex, new int2(cellIndex.x, cellIndex.y + 1));
                        break;
                    case AirwayHelpers.LaneDirection.HorizontalX:
                        this.CreateLane(entityIndex, cellIndex, new int2(cellIndex.x + 1, cellIndex.y));
                        break;
                    case AirwayHelpers.LaneDirection.Diagonal:
                        this.CreateLane(entityIndex, cellIndex, cellIndex + 1);
                        break;
                    case AirwayHelpers.LaneDirection.DiagonalCross:
                        this.CreateLane(entityIndex, new int2(cellIndex.x + 1, cellIndex.y), new int2(cellIndex.x, cellIndex.y + 1));
                        break;
                }
            }

            private void CreateLane(int entityIndex, int2 startNode, int2 endNode)
            {
                Entity entity = this.m_AirwayMap.entities[entityIndex];
                Lane value = default(Lane);
                value.m_StartNode = this.m_AirwayMap.GetPathNode(startNode);
                value.m_MiddleNode = new PathNode(entity, 1);
                value.m_EndNode = this.m_AirwayMap.GetPathNode(endNode);
                float3 nodePosition = this.m_AirwayMap.GetNodePosition(startNode);
                float3 nodePosition2 = this.m_AirwayMap.GetNodePosition(endNode);
                nodePosition.y += WaterUtils.SampleHeight(ref this.m_WaterSurfaceData, ref this.m_TerrainHeightData, nodePosition);
                nodePosition2.y += WaterUtils.SampleHeight(ref this.m_WaterSurfaceData, ref this.m_TerrainHeightData, nodePosition2);
                Curve value2 = default(Curve);
                value2.m_Bezier = NetUtils.StraightCurve(nodePosition, nodePosition2);
                value2.m_Length = math.distance(value2.m_Bezier.a, value2.m_Bezier.d);
                Game.Net.ConnectionLane value3 = default(Game.Net.ConnectionLane);
                value3.m_AccessRestriction = Entity.Null;
                value3.m_Flags = ConnectionLaneFlags.AllowMiddle | ConnectionLaneFlags.Airway;
                value3.m_TrackTypes = TrackTypes.None;
                value3.m_RoadTypes = this.m_RoadType;
                this.m_PrefabRefData[entity] = new PrefabRef(this.m_Prefab);
                this.m_LaneData[entity] = value;
                this.m_CurveData[entity] = value2;
                this.m_ConnectionLaneData[entity] = value3;
            }
        }
    }
}
