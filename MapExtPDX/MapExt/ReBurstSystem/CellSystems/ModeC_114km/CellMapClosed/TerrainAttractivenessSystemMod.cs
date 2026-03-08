﻿// Game.Simulation.TerrainAttractivenessSystem : CellMapSystem<TerrainAttractiveness>, IJobSerializable

using System.Runtime.CompilerServices;
using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using Game.Simulation;
using HarmonyLib;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.ModeC
{
    // =========================================================================================
    // [配置区域]
    // =========================================================================================
    // 1. 基类泛型
    using BaseCellMapSystem = CellMapSystem<TerrainAttractiveness>;
    // 2. Mod 自定义系统类(当前
    using ModSystem = TerrainAttractivenessSystemMod;
    // 3. 数据包泛(用于 GetData)
    using TargetCellMapData = CellMapData<TerrainAttractiveness>;
    // 4. 原版系统类型 (用于禁用和定
    using TargetSystem = TerrainAttractivenessSystem;
    // 5. T struct
    using TargetType = TerrainAttractiveness;
using MapExtPDX.MapExt.Core;
    // =========================================================================================

    /// <summary>
    /// 地形吸引力计算系(Terrain Attractiveness System)
    /// <para>
    /// 该系统负责根据地形高度、水域深度和森林环境（Zone Ambience）计算全图的吸引力数值
    /// 吸引力影响地价、市民满意度以及建筑生成的适宜度
    /// 系统继承CellMapSystem，以网格形式存储数据
    /// </para>
    /// </summary>
    public partial class TerrainAttractivenessSystemMod : BaseCellMapSystem, IJobSerializable
    {
        #region 字段/配置
        // 单例引用，便于Harmony补丁调用
        public static ModSystem Instance { get; private set; }

        // 纹理尺寸(vanilla=256)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.TerrainAttractivenessSystemkTextureSize; // mod尺寸
        public int2 TextureSize => new int2(kTextureSize, kTextureSize);

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
            base.OnCreate();
            Instance = this;

            // 1.禁用原版系统并获取原版系统引
            // 使用 GetExistingSystemManaged 避免意外创建未初始化的系
            var originalSystem = World.GetExistingSystemManaged<TargetSystem>();
            if (originalSystem != null)
            {
                originalSystem.Enabled = false;
                // #if DEBUG
                Mod.Info($"[{typeof(ModSystem).Name}] 禁用原系�? {typeof(TargetSystem).Name}");
                // #endif
            }
            else
            {
                // 仅在调试时提示，原版系统可能已被其他Mod移除或尚未加
#if DEBUG
                Mod.Error($"[{typeof(ModSystem).Name}] 无法找到可禁用的原系�?尚未加载或可能被其他Mod移除): {typeof(TargetSystem).Name}");
#endif
            }

            // 2. 创建自定义大小纹
            CreateTextures(kTextureSize);
            // #if DEBUG
// [ENCODING_FIX]             Mod.Info($"[{typeof(ModSystem).Name}] 创建自定义纹 {typeof(TargetSystem).Name} kTextureSize 原值{TargetSystem.kTextureSize} 变更目标值{this.m_TextureSize.x}");
            // #endif

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
            if (Instance == this) Instance = null;
            base.OnDestroy();
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

        #region 序列化自适应
        // ==============================================================================
        // 序列化修(泛型
        // ==============================================================================
        // 重写 Serialize 以处理大数据 (使用 TempJob)
        public new JobHandle Serialize<TWriter>(EntityWriterData writerData, JobHandle inputDeps) where TWriter : struct, IWriter
        {
            // 获取 Stride (数据步长)
            int stride = 0;
            if ((object)default(TargetType) is IStrideSerializable strideSerializable)
            {
                stride = strideSerializable.GetStride(writerData.GetWriter<TWriter>().context);
            }

            // 调度自定义的序列Job
            JobHandle jobHandle = new SerializeJobMod<TWriter>
            {
                m_Stride = stride,
                m_Map = this.m_Map,
                m_WriterData = writerData
            }.Schedule(JobHandle.CombineDependencies(inputDeps, m_WriteDependencies));

            m_ReadDependencies = JobHandle.CombineDependencies(m_ReadDependencies, jobHandle);
            return jobHandle;
        }

        // 重写 Deserialize (无需迁移旧存档，Job会重新计
        public override JobHandle Deserialize<TReader>(EntityReaderData readerData, JobHandle inputDeps)
        {
            int stride = 0;
            if ((object)default(TargetType) is IStrideSerializable strideSerializable)
            {
                stride = strideSerializable.GetStride(readerData.GetReader<TReader>().context);
            }

            // 简化版 Job：如果不匹配直接丢弃
            DeserializeJobResetMismatch<TReader> jobData = new()
            {
                m_Stride = stride,
                m_Map = this.m_Map,
                m_ReaderData = readerData
            };
            m_WriteDependencies = jobData.Schedule(JobHandle.CombineDependencies(inputDeps, m_ReadDependencies, m_WriteDependencies));
            return m_WriteDependencies;
        }

        [BurstCompile]
        private struct SerializeJobMod<TWriter> : IJob where TWriter : struct, IWriter
        {
            [ReadOnly] public int m_Stride;
            [ReadOnly] public NativeArray<TargetType> m_Map;
            public EntityWriterData m_WriterData;

            public void Execute()
            {
                TWriter writer = this.m_WriterData.GetWriter<TWriter>();
                if (m_Stride != 0 && m_Map.Length != 0)
                {
                    // 必须TempJob 防止 1024+ 尺寸导致内存溢出
                    NativeList<byte> buffer = new NativeList<byte>(m_Map.Length * 2, Allocator.TempJob);
                    try
                    {
                        m_WriterData.GetWriter<TWriter>(buffer).Write(m_Map);
                        writer.Write(-m_Map.Length);
                        writer.Write(buffer.Length);
                        writer.Write(buffer.AsArray(), m_Stride);
                    }
                    finally { buffer.Dispose(); }
                }
                else
                {
                    writer.Write(m_Map.Length);
                    writer.Write(m_Map);
                }
            }
        }

        [BurstCompile]
        private struct DeserializeJobResetMismatch<TReader> : IJob where TReader : struct, IReader
        {
            [ReadOnly] public int m_Stride;
            public NativeArray<TargetType> m_Map;
            public EntityReaderData m_ReaderData;

            public void Execute()
            {
                TReader reader = m_ReaderData.GetReader<TReader>();
                if (!(reader.context.version > Game.Version.stormWater)) return;

                // 默认0
                // m_Map 已经SetDefaults 中被清零了，如果这里不写入，就是重置状

                if (reader.context.version > Game.Version.cellMapLengths)
                {
                    reader.Read(out int storedCount);

                    // 1. 判断是否匹配
                    bool sizeMatches = (math.abs(storedCount) == m_Map.Length);

                    // 2. 如果是原始数(Length > 0)
                    if (storedCount > 0)
                    {
                        if (sizeMatches)
                        {
                            reader.Read(m_Map);
                        }
                        else
                        {
                            // 尺寸不匹配：读取到临时数组并丢弃 (必须读取以推进流位置)
                            var dummy = new NativeArray<TargetType>(storedCount, Allocator.TempJob);
                            reader.Read(dummy);
                            dummy.Dispose();
                            // m_Map 保持0
                        }
                    }
                    // 3. 如果是压缩数(Length < 0)
                    else if (storedCount < 0)
                    {
                        int actualCount = -storedCount;
                        reader.Read(out int byteLength);

                        // 必须读出来以清空
                        NativeArray<byte> compressedBuffer = new NativeArray<byte>(byteLength, Allocator.TempJob);
                        try
                        {
                            reader.Read(compressedBuffer, m_Stride);

                            if (actualCount == m_Map.Length)
                            {
                                // 尺寸匹配：正常解压到 Map
                                NativeReference<int> pos = new NativeReference<int>(0, Allocator.Temp);
                                m_ReaderData.GetReader<TReader>(compressedBuffer, pos).Read(m_Map);
                                pos.Dispose();
                            }
                            // 否则：只读取字节流，不解压，m_Map 保持0
                        }
                        finally
                        {
                            compressedBuffer.Dispose();
                        }
                    }
                }
                else
                {
                    // 旧版本数据，通常无法匹配尺寸，直接忽略或尝试读取
                    if (m_Map.Length == kTextureSize * kTextureSize) // 运气好匹配了
                        reader.Read(m_Map);
                }
            }
        }

        #endregion

        #region GetData修正
        // 重写/重定向的 GetData
        public new TargetCellMapData GetData(bool readOnly, out JobHandle dependencies)
        {
            // 获取依赖
            dependencies = (readOnly ? m_WriteDependencies : JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies));

            float2 mapSize = new float2(XCellMapSystemRe.kMapSize, XCellMapSystemRe.kMapSize);

            return new TargetCellMapData
            {
                m_Buffer = m_Map,
                m_CellSize = mapSize / (float2)m_TextureSize,
                m_TextureSize = m_TextureSize
            };
        }
        #endregion

        #region HarmonyPatch
        // ==============================================================================
        // Harmony 补丁 (全自动适配)
        // ==============================================================================
        [HarmonyPatch]
        public static class Patches
        {
            // 辅助判断：只拦截对应的原版系统实
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool ShouldRedirect(object instance)
            {
                return Instance != null && instance.GetType() == typeof(TargetSystem);
            }

            // Patch: GetMap
            [HarmonyPatch(typeof(BaseCellMapSystem), "GetMap")]
            [HarmonyPrefix]
            public static bool GetMapPrefix(BaseCellMapSystem __instance,
                                      ref NativeArray<TargetType> __result,
                                      bool readOnly,
                                      ref JobHandle dependencies)
            {
                if (ShouldRedirect(__instance))
                {
                    __result = Instance.GetMap(readOnly, out var deps);
                    dependencies = deps;
                    return false;
                }
                return true;
            }

            // Patch: GetData
            [HarmonyPatch(typeof(BaseCellMapSystem), "GetData")]
            [HarmonyPrefix]
            public static bool GetDataPrefix(BaseCellMapSystem __instance,
                                      ref TargetCellMapData __result,
                                      bool readOnly,
                                      ref JobHandle dependencies)
            {
                if (ShouldRedirect(__instance))
                {
                    __result = Instance.GetData(readOnly, out var deps);
                    dependencies = deps;
                    return false;
                }
                return true;
            }

            // Patch: AddReader
            [HarmonyPatch(typeof(BaseCellMapSystem), "AddReader")]
            [HarmonyPrefix]
            public static bool AddReaderPrefix(BaseCellMapSystem __instance, JobHandle jobHandle)
            {
                if (ShouldRedirect(__instance))
                {
                    Instance.AddReader(jobHandle);
                    return false;
                }
                return true;
            }

            // Patch: AddWriter
            [HarmonyPatch(typeof(BaseCellMapSystem), "AddWriter")]
            [HarmonyPrefix]
            public static bool AddWriterPrefix(BaseCellMapSystem __instance, JobHandle jobHandle)
            {
                if (ShouldRedirect(__instance))
                {
                    Instance.AddWriter(jobHandle);
                    return false;
                }
                return true;
            }
        }
        #endregion

    } // mod class

} // mod namespace



