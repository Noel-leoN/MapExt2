// Game.Simulation.TerrainAttractivenessSystem : CellMapSystem<TerrainAttractiveness>, IJobSerializable

using Game;
using Game.Prefabs;
using Game.Simulation;
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
    using BaseCellMapSystem = CellMapSystem<TerrainAttractiveness>;
    // 2. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<TerrainAttractiveness>;
    // 3. 原版系统类型 (用于禁用和定位)
    using TargetSystem = TerrainAttractivenessSystem;
    // 4. T struct
    using TargetType = TerrainAttractiveness;
    // =========================================================================================

    /// <summary>
    /// 地形吸引力计算系(Terrain Attractiveness System)
    /// <para>
    /// 该系统负责根据地形高度、水域深度和森林环境（Zone Ambience）计算全图的吸引力数值
    /// 吸引力影响地价、市民满意度以及建筑生成的适宜度
    /// 系统继承CellMapSystem，以网格形式存储数据
    /// </para>
    /// </summary>
    // 降级保护（重构二 0.4）：合并版存档里本类的类型名是 MapExtPDX.CellMap.TerrainAttractivenessSystemMod，退回本版本时靠此别名读回，勿删
    [Colossal.Serialization.Entities.FormerlySerializedAs("MapExtPDX.CellMap.TerrainAttractivenessSystemMod")]
    public partial class TerrainAttractivenessSystemMod : CellMapReplacementSystem<TargetType, TargetSystem>
    {
        private const string Tag = nameof(TerrainAttractivenessSystemMod);

        #region 字段/配置
        // 纹理尺寸(vanilla=256)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.TerrainAttractivenessSystemkTextureSize; // mod尺寸

        // 基类参数：日志 Tag、本模式的贴图边长与地图边长（XCellMapSystemRe 按模式命名空间解析）
        protected override string LogTag => Tag;
        protected override int ModTextureSize => kTextureSize;
        protected override int MapSize => XCellMapSystemRe.kMapSize;

        // 系统更新周期：每16
        public static readonly int kUpdatesPerDay = 16;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;

        /// <summary>
        /// 用于存储中间计算结果（水深、地形高度、森林环境值）的原生数组
        /// </summary>
        private NativeArray<float3> m_AttractFactorData;
        #endregion

        #region 查询和系统引
        private TerrainSystem m_TerrainSystem;
        private WaterSystem m_WaterSystem;
        private ZoneAmbienceSystemMod m_ZoneAmbienceSystem;
        #endregion

        #region System Loop

        protected override void OnCreate()
        {
            // 1–2. 禁用原版系统、登记 Instance、创建自定义大小纹理由基类完成
            base.OnCreate();

            // 3. 获取其他依赖和查
            // 获取依赖的托管系(Managed Systems)
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_WaterSystem = World.GetOrCreateSystemManaged<WaterSystem>();
            m_ZoneAmbienceSystem = World.GetOrCreateSystemManaged<ZoneAmbienceSystemMod>();

            // 初始化持久化数据缓存
            m_AttractFactorData = new NativeArray<float3>(m_Map.Length, Allocator.Persistent);

            // 注册需要更新的组件数据
            RequireForUpdate<AttractivenessParameterData>();
        }

        protected override void OnDestroy()
        {
            if (m_AttractFactorData.IsCreated)
            {
                m_AttractFactorData.Dispose();
            }
            base.OnDestroy(); // 基类清空 Instance 并释放 m_Map
        }

        protected override void OnUpdate()
        {
            // 1. 获取外部系统数据及其依赖句柄
            TerrainHeightData heightData = m_TerrainSystem.GetHeightData();
            WaterSurfaceData<SurfaceWater> waterData = m_WaterSystem.GetSurfaceData(out JobHandle waterDeps);
            CellMapData<ZoneAmbienceCell> ambienceData = m_ZoneAmbienceSystem.GetData(readOnly: true, out JobHandle ambienceDeps);

            // 获取全局配置参数
            AttractivenessParameterData parameters = SystemAPI.GetSingleton<AttractivenessParameterData>();

            // 2. 调度准备工作 Job：采集地形、水域和环境数据
            // 必须等待之前的写入完(base.Dependency) 以及外部数据的依
            TerrainAttractivenessPrepareJob prepareJob = new TerrainAttractivenessPrepareJob
            {
                m_TerrainData = heightData,
                m_WaterData = waterData,
                m_ZoneAmbienceData = ambienceData,
                m_AttractFactorData = m_AttractFactorData
            };

            JobHandle prepareHandle = prepareJob.ScheduleBatch(
                m_Map.Length,
                4,
                JobHandle.CombineDependencies(Dependency, waterDeps, ambienceDeps)
            );

            // 注册读取器，确保外部系统知道我们在读取数
            m_TerrainSystem.AddCPUHeightReader(prepareHandle);
            m_ZoneAmbienceSystem.AddReader(prepareHandle);
            m_WaterSystem.AddSurfaceReader(prepareHandle);

            // 3. 调度主计Job：根据采集的数据计算吸引
            // 需base.m_ReadDependencies base.m_WriteDependencies 来处Map 的读写锁
            TerrainAttractivenessJob mainJob = new TerrainAttractivenessJob
            {
                m_AttractFactorData = m_AttractFactorData,
                m_Scale = heightData.scale.x * kTextureSize, // 计算缩放比例
                m_AttractivenessMap = m_Map,
                m_AttractivenessParameters = parameters
            };

            // Job 依赖Prepare Job 以及 Map 的读写依
            JobHandle mainHandle = mainJob.ScheduleBatch(
                m_Map.Length,
                4,
                JobHandle.CombineDependencies(m_WriteDependencies, m_ReadDependencies, prepareHandle)
            );

            // 4. 完成依赖链设
            // 将此 Job 注册Map 的写入
            AddWriter(mainHandle);
            // 更新系统的主依赖句柄
            Dependency = JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies, mainHandle);
        }

        #endregion

        #region Jobs

        /// <summary>
        /// 准备阶段 Job：从不同系统中采样数据并整合到中间数组中
        /// float3 格式: x = 水深, y = 地形高度, z = 森林环境
        /// </summary>
        [BurstCompile]
        private struct TerrainAttractivenessPrepareJob : IJobParallelForBatch
        {
            [ReadOnly] public TerrainHeightData m_TerrainData;
            [ReadOnly] public WaterSurfaceData<SurfaceWater> m_WaterData;
            [ReadOnly] public CellMapData<ZoneAmbienceCell> m_ZoneAmbienceData;

            [NativeDisableParallelForRestriction] // Batch Job 写入非重叠索引，通常安全，但显示声明以防万一
            public NativeArray<float3> m_AttractFactorData;

            public void Execute(int startIndex, int count)
            {
                for (int i = startIndex; i < startIndex + count; i++)
                {
                    float3 cellCenter = XCellMapSystemRe.GetCellCenter(i, kTextureSize);

                    float waterDepth = WaterUtils.SampleDepth(ref m_WaterData, cellCenter);
                    float terrainHeight = TerrainUtils.SampleHeight(ref m_TerrainData, cellCenter);
                    float forestAmbience = XCellMapSystemRe.ZoneAmbienceSystemGetZoneAmbience(GroupAmbienceType.Forest, cellCenter, m_ZoneAmbienceData.m_Buffer, 1f);

                    m_AttractFactorData[i] = new float3(waterDepth, terrainHeight, forestAmbience);
                }
            }
        }

        /// <summary>
        /// 主计Job：计算最终的吸引Bonus
        /// 包括森林加成和海岸线加成，通过卷积（周围像素扫描）计算
        /// </summary>
        [BurstCompile]
        private struct TerrainAttractivenessJob : IJobParallelForBatch
        {
            [ReadOnly] public NativeArray<float3> m_AttractFactorData;
            [ReadOnly] public float m_Scale;

            [NativeDisableParallelForRestriction]
            public NativeArray<TargetType> m_AttractivenessMap;

            public AttractivenessParameterData m_AttractivenessParameters;

            public void Execute(int startIndex, int count)
            {
                for (int i = startIndex; i < startIndex + count; i++)
                {
                    float3 currentCellCenter = XCellMapSystemRe.GetCellCenter(i, kTextureSize);
                    float2 calculatedBonus = float2.zero; // x: Forest, y: Shore

                    // 计算搜索半径 (基于参数距离和缩
                    int searchRadius = (int)math.ceil(math.max(m_AttractivenessParameters.m_ForestDistance, m_AttractivenessParameters.m_ShoreDistance) / m_Scale);

                    // 卷积循环：检查周围的格子
                    for (int yOffset = -searchRadius; yOffset <= searchRadius; yOffset++)
                    {
                        for (int xOffset = -searchRadius; xOffset <= searchRadius; xOffset++)
                        {
                            // 计算邻居坐标并Clamp防止越界
                            int neighborX = math.min(kTextureSize - 1, math.max(0, i % kTextureSize + xOffset));
                            int neighborY = math.min(kTextureSize - 1, math.max(0, i / kTextureSize + yOffset));
                            int neighborIndex = neighborX + neighborY * kTextureSize;

                            float3 neighborData = m_AttractFactorData[neighborIndex]; // x:Water, y:Height, z:Forest
                            float distance = math.distance(XCellMapSystemRe.GetCellCenter(neighborIndex, kTextureSize), currentCellCenter);

                            // 计算森林吸引(neighborData.z 是森林环境
                            // 距离越近，森林值越高，吸引力越
                            float forestFalloff = math.saturate(1f - distance / m_AttractivenessParameters.m_ForestDistance);
                            calculatedBonus.x = math.max(calculatedBonus.x, forestFalloff * neighborData.z);

                            // 计算海岸吸引(neighborData.x 是水
                            // 如果水深 > 2f，视为有效水源，计算距离衰减
                            float shoreFalloff = math.saturate(1f - distance / m_AttractivenessParameters.m_ShoreDistance);
                            float isWater = (neighborData.x > 2f) ? 1f : 0f;
                            calculatedBonus.y = math.max(calculatedBonus.y, shoreFalloff * isWater);
                        }
                    }

                    // 写入结果
                    m_AttractivenessMap[i] = new TargetType
                    {
                        m_ForestBonus = calculatedBonus.x,
                        m_ShoreBonus = calculatedBonus.y
                    };
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



