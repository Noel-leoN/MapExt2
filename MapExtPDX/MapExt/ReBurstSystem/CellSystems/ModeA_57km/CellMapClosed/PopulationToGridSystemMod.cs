// Game.Simulation.PopulationToGridSystem : CellMapSystem<PopulationCell>, IJobSerializable

using Colossal.Entities;
using Game;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Objects;
using Game.Simulation;
using Game.Tools;
using HarmonyLib;
using MapExtPDX.CellShared;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.ModeA
{
    // =========================================================================================
    // [配置区域]
    // =========================================================================================
    // 1. 基类泛型
    using BaseCellMapSystem = CellMapSystem<PopulationCell>;
    // 2. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<PopulationCell>;
    // 3. 原版系统类型 (用于禁用和定位)
    using TargetSystem = PopulationToGridSystem;
    // 4. T struct
    using TargetType = PopulationCell;
    // =========================================================================================

    // 降级保护（重构二 0.4）：合并版存档里本类的类型名是 MapExtPDX.CellMap.PopulationToGridSystemMod，退回本版本时靠此别名读回，勿删
    /// <summary>
    /// 人口映射系统
    /// 将住宅内的居民数量映射到 PopulationCell 网格中，用于生成人口密度数据
    /// </summary>
    [Colossal.Serialization.Entities.FormerlySerializedAs("MapExtPDX.CellMap.PopulationToGridSystemMod")]
    public partial class PopulationToGridSystemMod : CellMapReplacementSystem<TargetType, TargetSystem>
    {
        private const string Tag = nameof(PopulationToGridSystemMod);

        #region 字段/配置
        // 纹理尺寸(vanilla=64)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.PopulationToGridSystemkTextureSize; // mod尺寸

        // 基类参数：日志 Tag、本模式的贴图边长与地图边长（XCellMapSystemRe 按模式命名空间解析）
        protected override string LogTag => Tag;
        protected override int ModTextureSize => kTextureSize;
        protected override int MapSize => XCellMapSystemRe.kMapSize;

        // 系统更新周期：每32
        public static readonly int kUpdatesPerDay = 32;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;
        #endregion

        #region 查询和系统引
        // 用于筛选住宅资产的查询
        private EntityQuery m_ResidentialPropertyQuery;
        #endregion

        #region System Loop
        protected override void OnCreate()
        {
            // 1–2. 禁用原版系统、登记 Instance、创建自定义大小纹理由基类完成
            base.OnCreate();

            // 3. 获取其他依赖和查
            // 构建查询：筛选所有具有住宅属性、租赁者信息和Transform组件的实
            // 排除已销毁、已删除或临时的实体
            m_ResidentialPropertyQuery = SystemAPI.QueryBuilder()
                .WithAll<ResidentialProperty, Renter, Transform>()
                .WithNone<Destroyed, Deleted, Temp>()
                .Build();
        }

        protected override void OnUpdate()
        {
            // 获取组件和缓冲区的Lookup数据 (只读权限)
            var renterLookup = SystemAPI.GetBufferLookup<Renter>(isReadOnly: true);
            var citizenLookup = SystemAPI.GetBufferLookup<HouseholdCitizen>(isReadOnly: true);
            var transformLookup = SystemAPI.GetComponentLookup<Transform>(isReadOnly: true);

            // 将符合条件的实体转换NativeList，分配器使用 World UpdateAllocator
            // ToEntityListAsync 返回一JobHandle，需要将其与当前的依赖合
            NativeList<Entity> entities = m_ResidentialPropertyQuery.ToEntityListAsync(World.UpdateAllocator.ToAllocator, out var queryHandle);

            // 实例Job
            var job = new PopulationToGridJob
            {
                m_Entities = entities,
                m_PopulationMap = m_Map,
                m_Renters = renterLookup,
                m_HouseholdCitizens = citizenLookup,
                m_Transforms = transformLookup
            };

            // 组合依赖项：查询结果 + 当前系统的读写依
            var combinedDeps = JobUtils.CombineDependencies(queryHandle, m_WriteDependencies, m_ReadDependencies, Dependency);

            // 调度 Job
            Dependency = job.Schedule(combinedDeps);

            // 通知基类正在写入 Map，以便后续系统能正确处理依赖
            AddWriter(Dependency);
        }
        #endregion

        #region PopulationToGridJob
        [BurstCompile]
        private struct PopulationToGridJob : IJob
        {
            [ReadOnly] public NativeList<Entity> m_Entities;
            [ReadOnly] public BufferLookup<Renter> m_Renters;
            [ReadOnly] public ComponentLookup<Transform> m_Transforms;
            [ReadOnly] public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;
            // 输出的目标网格数
            public NativeArray<TargetType> m_PopulationMap;

            public void Execute()
            {
                // 1. 清空人口网格数据
                int totalCells = kTextureSize * kTextureSize;
                for (int i = 0; i < totalCells; i++) m_PopulationMap[i] = default;

                // 2. 遍历所有住宅实
                for (int i = 0; i < m_Entities.Length; i++)
                {
                    Entity buildingEntity = m_Entities[i];
                    int totalCitizensInBuilding = 0;

                    // 获取该建筑内的所有租(Renter Buffer)
                    if (m_Renters.TryGetBuffer(buildingEntity, out var rentersBuffer))
                    {
                        for (int r = 0; r < rentersBuffer.Length; r++)
                        {
                            Entity renterEntity = rentersBuffer[r].m_Renter;

                            // 检查租户是否有家庭成员 (HouseholdCitizen Buffer)
                            if (m_HouseholdCitizens.TryGetBuffer(renterEntity, out var citizensBuffer))
                            {
                                totalCitizensInBuilding += citizensBuffer.Length;
                            }
                        }
                    }

                    // 获取建筑位置并映射到网格坐标
                    float3 position = m_Transforms[buildingEntity].m_Position;
                    int2 cellCoords = XCellMapSystemRe.GetCell(position, XCellMapSystemRe.kMapSize, kTextureSize);

                    // 边界检查：确保坐标在纹理范围内
                    if (cellCoords.x >= 0 && cellCoords.y >= 0 && cellCoords.x < kTextureSize && cellCoords.y < kTextureSize)
                    {
                        int gridIndex = cellCoords.x + cellCoords.y * kTextureSize;

                        // 累加人口数据
                        TargetType currentCell = m_PopulationMap[gridIndex];
                        currentCell.m_Population += totalCitizensInBuilding;
                        m_PopulationMap[gridIndex] = currentCell;
                    }
                }
            }
        }
        #endregion

        #region HarmonyPatch
        // ==============================================================================
        // Harmony 补丁：非泛型薄壳，判断与转发在基类的 Redirect
        // （Harmony 2.2.2 按元数据令牌还原补丁方法，补丁方法不能声明在泛型类里）
        // ==============================================================================
        [HarmonyPatch]
        public static class Patches
        {
            // Patch: GetMap
            [HarmonyPatch(typeof(BaseCellMapSystem), "GetMap")]
            [HarmonyPrefix]
            public static bool GetMapPrefix(BaseCellMapSystem __instance,
                                      ref NativeArray<TargetType> __result,
                                      bool readOnly,
                                      ref JobHandle dependencies)
                => Redirect.GetMap(__instance, ref __result, readOnly, ref dependencies);

            // Patch: GetData
            [HarmonyPatch(typeof(BaseCellMapSystem), "GetData")]
            [HarmonyPrefix]
            public static bool GetDataPrefix(BaseCellMapSystem __instance,
                                      ref TargetCellMapData __result,
                                      bool readOnly,
                                      ref JobHandle dependencies)
                => Redirect.GetData(__instance, ref __result, readOnly, ref dependencies);

            // Patch: AddReader
            [HarmonyPatch(typeof(BaseCellMapSystem), "AddReader")]
            [HarmonyPrefix]
            public static bool AddReaderPrefix(BaseCellMapSystem __instance, JobHandle jobHandle)
                => Redirect.AddReader(__instance, jobHandle);

            // Patch: AddWriter
            [HarmonyPatch(typeof(BaseCellMapSystem), "AddWriter")]
            [HarmonyPrefix]
            public static bool AddWriterPrefix(BaseCellMapSystem __instance, JobHandle jobHandle)
                => Redirect.AddWriter(__instance, jobHandle);
        }
        #endregion

    } // mod class

} // mod namespace



