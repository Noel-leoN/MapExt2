// Game.Simulation.SoilWaterSystem : CellMapSystem<SoilWater>, IJobSerializable, IPostDeserialize

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Colossal.Entities;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Events;
using Game.Prefabs;
using Game.Serialization;
using Game.Simulation;
using HarmonyLib;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    // =========================================================================================
    // [配置区域]
    // =========================================================================================
    // 1. T struct
    using TargetType = SoilWater;
    // 2. 基类泛型
    using BaseCellMapSystem = CellMapSystem<SoilWater>;
    // 3. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<SoilWater>;
    // 4. 原版系统类型 (用于禁用和定位)
    using TargetSystem = SoilWaterSystem;
    // 5. Mod 自定义系统类型 (当前类)
    using ModSystem = SoilWaterSystemMod;
    // =========================================================================================

    /// <summary>
    /// 土壤水份系统
    /// 负责模拟全图的土壤含水量，包括降雨吸收、地形流动扩散以及洪水事件的触发。
    /// 继承自 CellMapSystem 用于处理网格数据。
    /// 该系统是否有存在的必要？？？
    /// </summary>
    public partial class SoilWaterSystemMod : BaseCellMapSystem, IJobSerializable, IPostDeserialize
	{
        #region 字段/配置
        // 单例引用，便于Harmony补丁调用
        public static ModSystem Instance { get; private set; }
       
        // 纹理尺寸(vanilla=128)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = CellMapSystemRe.SoilWaterSystemkTextureSize; // mod尺寸
        public int2 TextureSize => new int2(kTextureSize, kTextureSize);

        // 系统更新周期：每日(月)次数1024
        public static readonly int kUpdatesPerDay = 1024;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;

        // 将地图分为8份，每tick更新一份
        public static readonly int kLoadDistribution = CellMapSystemRe.SoilWaterSystemkLoadDistribution; //随texturesize同步变更

        private Texture2D m_SoilWaterTexture;
        public Texture soilTexture => this.m_SoilWaterTexture;
        #endregion

        #region 查询和系统引用
        // 系统依赖
        private SimulationSystem m_SimulationSystem;
        private TerrainSystem m_TerrainSystem;
        private WaterSystem m_WaterSystem;
        private ClimateSystem m_ClimateSystem;
        private EndFrameBarrier m_EndFrameBarrier;        
        // 查询
        private EntityQuery m_SoilWaterParameterQuery;
        private EntityQuery m_FloodQuery;
        private EntityQuery m_FloodPrefabQuery;
        private EntityQuery m_FloodCounterQuery;
        #endregion

        #region System Loop
        protected override void OnCreate()
		{
            base.OnCreate();
            Instance = this;

            // 1.禁用原版系统并获取原版系统引用
            // 使用 GetExistingSystemManaged 避免意外创建未初始化的系统
            var originalSystem = World.GetExistingSystemManaged<TargetSystem>();
            if (originalSystem != null)
            {
                originalSystem.Enabled = false;
                // #if DEBUG
                Mod.Info($"[{typeof(ModSystem).Name}] 禁用原系统: {typeof(TargetSystem).Name}");
                // #endif
            }
            else
            {
                // 仅在调试时提示，原版系统可能已被其他Mod移除或尚未加载
#if DEBUG
                Mod.Error($"[{typeof(ModSystem).Name}] 无法找到可禁用的原系统(尚未加载或可能被其他Mod移除): {typeof(TargetSystem).Name}");
#endif
            }

            // 2. 创建自定义大小纹理
            CreateTextures(kTextureSize);
            // #if DEBUG
            Mod.Info($"[{typeof(ModSystem).Name}] 创建自定义纹理: {typeof(TargetSystem).Name} kTextureSize 从 原值{TargetSystem.kTextureSize} 变更为 目标值{this.m_TextureSize.x}");
            // #endif

            // 3. 获取其他依赖和查询
            // 获取子系统依赖
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_WaterSystem = World.GetOrCreateSystemManaged<WaterSystem>();
            m_ClimateSystem = World.GetOrCreateSystemManaged<ClimateSystem>();
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();

            // 构建查询
            m_SoilWaterParameterQuery = GetEntityQuery(ComponentType.ReadOnly<SoilWaterParameterData>());
            m_FloodQuery = GetEntityQuery(ComponentType.ReadOnly<Flood>());
            m_FloodPrefabQuery = GetEntityQuery(ComponentType.ReadOnly<FloodData>());
            m_FloodCounterQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<FloodCounterData>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build(this);

            // 确保洪水计数器存在
            CreateFloodCounter();

            // 初始化纹理和Map数据
            CreateTextures(kTextureSize);
            m_SoilWaterTexture = new Texture2D(kTextureSize, kTextureSize, TextureFormat.RFloat, false, true)
            {
                name = "SoilWaterTexture",
                hideFlags = HideFlags.HideAndDontSave
            };

            // 初始化默认数据
            NativeArray<float> rawTextureData = m_SoilWaterTexture.GetRawTextureData<float>();
            for (int i = 0; i < m_Map.Length; i++)
            {
                m_Map[i] = new TargetType { m_Amount = 1024, m_Max = 8192 };
                rawTextureData[i] = 0f;
            }
            m_SoilWaterTexture.Apply();

            RequireForUpdate(m_SoilWaterParameterQuery);
		}

        private void CreateFloodCounter()
        {
            EntityManager.CreateEntity(EntityManager.CreateArchetype(ComponentType.ReadWrite<FloodCounterData>()));
        }

        public void PostDeserialize(Context context)
        {
            EntityQuery entityQuery = EntityManager.CreateEntityQuery(ComponentType.ReadWrite<FloodCounterData>());
            try
            {
                if (entityQuery.CalculateEntityCount() == 0)
                {
                    this.CreateFloodCounter();
                }
            }
            finally
            {
                entityQuery.Dispose();
            }
        }

        protected override void OnDestroy()
        {
            if (m_SoilWaterTexture != null) UnityEngine.Object.Destroy(m_SoilWaterTexture);
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        protected override void OnUpdate()
		{
            TerrainHeightData heightData = m_TerrainSystem.GetHeightData();
            if (!heightData.isCreated) return;

            m_SoilWaterTexture.Apply(); // 应用上一帧的纹理修改

            // 获取天气和水面数据
            float precipitation = m_ClimateSystem.precipitation.value;
            WaterSurfaceData<SurfaceWater> waterSurfaceData = m_WaterSystem.GetSurfaceData(out JobHandle waterDeps);

            // 计算负载均衡索引和Shader更新频率
            // 计算每一帧只更新 1/kLoadDistribution 的网格
            int totalSteps = 262144; // 每日(月)总模拟步数
            int stepsPerFrame = totalSteps / kUpdatesPerDay;
            int shaderUpdatesPerSoilUpdate = stepsPerFrame / kLoadDistribution / m_WaterSystem.SimulationCycleSteps;
            int loadDistributionIndex = (int)((long)m_SimulationSystem.frameIndex / stepsPerFrame % kLoadDistribution);

            // 新增:TempJob临时缓冲区允许大内存分配
            NativeArray<int> scratchMap = new NativeArray<int>(m_Map.Length, Allocator.TempJob);

            // 调度核心模拟 Job
            var job = new SoilWaterTickJob
            {
                m_TempMap = scratchMap,

                // 数据源
                m_SoilWaterMap = m_Map,
                m_TerrainHeightData = heightData,
                m_WaterSurfaceData = waterSurfaceData,
                m_SoilWaterTextureData = m_SoilWaterTexture.GetRawTextureData<float>(),

                // 配置参数
                m_SoilWaterParameters = m_SoilWaterParameterQuery.GetSingleton<SoilWaterParameterData>(),
                m_FloodCounterEntity = m_FloodCounterQuery.GetSingletonEntity(),
                m_Weather = precipitation,
                m_ShaderUpdatesPerSoilUpdate = shaderUpdatesPerSoilUpdate,
                m_LoadDistributionIndex = loadDistributionIndex,

                // ECS 数据访问
                m_Changes = SystemAPI.GetComponentLookup<WaterLevelChange>(),
                m_FloodCounterDatas = SystemAPI.GetComponentLookup<FloodCounterData>(),
                m_Events = SystemAPI.GetComponentLookup<EventData>(true),

                // 异步获取实体列表
                m_FloodEntities = m_FloodQuery.ToEntityListAsync(World.UpdateAllocator.ToAllocator, out JobHandle floodHandle),
                m_FloodPrefabEntities = m_FloodPrefabQuery.ToEntityListAsync(World.UpdateAllocator.ToAllocator, out JobHandle prefabHandle),

                m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer()
            };

            // 依赖管理
            JobHandle combinedDeps = JobUtils.CombineDependencies(
                m_WriteDependencies,
                m_ReadDependencies,
                floodHandle,
                prefabHandle,
                waterDeps,
                Dependency
            );

            // 调度 Job
            Dependency = job.Schedule(combinedDeps);

            // 释放临时数据TempJob
            scratchMap.Dispose(Dependency);

            // 注册后续依赖
            AddWriter(Dependency); // 标记 m_Map 被写入
            m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
            m_TerrainSystem.AddCPUHeightReader(Dependency);
            m_WaterSystem.AddSurfaceReader(Dependency);

            // 确保后续读写正确
            Dependency = JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies, Dependency);
        }
        #endregion

        #region SoilWaterTickJob
        [BurstCompile]
        private struct SoilWaterTickJob : IJob
        {
            // --- 核心地图数据 ---
            public NativeArray<TargetType> m_SoilWaterMap;
            [ReadOnly] public TerrainHeightData m_TerrainHeightData;
            [ReadOnly] public WaterSurfaceData<SurfaceWater> m_WaterSurfaceData;
            public NativeArray<float> m_SoilWaterTextureData; // 写入纹理原始数据

            // --- 配置参数 ---
            public SoilWaterParameterData m_SoilWaterParameters;

            // --- 组件数据 ---
            public ComponentLookup<WaterLevelChange> m_Changes;
            public ComponentLookup<FloodCounterData> m_FloodCounterDatas;
            [ReadOnly] public ComponentLookup<EventData> m_Events;

            // --- 实体列表 ---
            [ReadOnly] public NativeList<Entity> m_FloodEntities;       // 当前正在进行的洪水事件
            [ReadOnly] public NativeList<Entity> m_FloodPrefabEntities; // 洪水事件的预制体

            // --- 命令缓冲 ---
            public EntityCommandBuffer m_CommandBuffer;
            public Entity m_FloodCounterEntity; // 全局计数器实体

            // --- 运行时参数 ---
            public float m_Weather; // 当前降雨量
            public int m_ShaderUpdatesPerSoilUpdate;
            public int m_LoadDistributionIndex;

            // 新增：临时Map(传入TempJob)
            public NativeArray<int> m_TempMap;

            /// <summary>
            /// 处理两个相邻单元格之间的水分扩散
            /// </summary>
            private void HandleInterface(int index, int otherIndex, NativeArray<int> diffBuffer, ref SoilWaterParameterData parameters)
            {
                TargetType current = m_SoilWaterMap[index];
                TargetType neighbor = m_SoilWaterMap[otherIndex];

                int currentDiff = diffBuffer[index];
                int neighborDiff = diffBuffer[otherIndex];

                // 计算高度差和饱和度差
                float heightDelta = neighbor.m_Surface - current.m_Surface;
                float saturationDelta = (float)neighbor.m_Amount / neighbor.m_Max - (float)current.m_Amount / current.m_Max;

                // 计算流动量：重力(高度差) + 渗透(饱和度差)
                // 0.25f 是经验系数
                float flowAmount = parameters.m_HeightEffect * heightDelta / (CellMapSystemRe.kMapSize / kTextureSize) + 0.25f * saturationDelta;

                // 限制最大扩散速度
                flowAmount = (flowAmount >= 0f)
                    ? math.min(parameters.m_MaxDiffusion, flowAmount)
                    : math.max(-parameters.m_MaxDiffusion, flowAmount);

                // 计算实际转移的水量（基于源头的当前水量）
                int transferAmount = (int)math.round(flowAmount * ((flowAmount > 0f) ? neighbor.m_Amount : current.m_Amount));

                // 更新临时缓冲
                currentDiff += transferAmount;
                neighborDiff -= transferAmount;

                diffBuffer[index] = currentDiff;
                diffBuffer[otherIndex] = neighborDiff;
            }

            private void StartFlood()
            {
                if (m_FloodPrefabEntities.Length > 0)
                {
                    Entity prefab = m_FloodPrefabEntities[0];
                    EntityArchetype archetype = m_Events[prefab].m_Archetype;
                    Entity eventEntity = m_CommandBuffer.CreateEntity(archetype);

                    // 实例化洪水事件
                    m_CommandBuffer.SetComponent(eventEntity, new PrefabRef { m_Prefab = prefab });
                    m_CommandBuffer.SetComponent(eventEntity, new WaterLevelChange
                    {
                        m_DangerHeight = 0f,
                        m_Direction = new float2(0f, 0f),
                        m_Intensity = 0f,
                        m_MaxIntensity = 0f
                    });
                }
            }

            private void StopFlood()
            {
                // 销毁当前的洪水事件实体
                if (m_FloodEntities.Length > 0)
                {
                    m_CommandBuffer.AddComponent<Deleted>(m_FloodEntities[0]);
                }
            }

            public void Execute()
            {
                // 临时缓冲区，用于存储扩散计算中的增量，避免读写冲突
                // NativeArray<int> diffusionBuffer = new NativeArray<int>(m_SoilWaterMap.Length, Allocator.TempJob);

                // 1. 计算水分扩散 (Diffusion Pass)
                // 遍历每个网格，处理它与右侧和下方网格的交互
                for (int i = 0; i < m_SoilWaterMap.Length; i++)
                {
                    int x = i % kTextureSize;
                    int y = i / kTextureSize;

                    // 向右扩散
                    if (x < kTextureSize - 1)
                    {
                        HandleInterface(i, i + 1, m_TempMap, ref m_SoilWaterParameters);
                    }
                    // 向下扩散
                    if (y < kTextureSize - 1)
                    {
                        HandleInterface(i, i + kTextureSize, m_TempMap, ref m_SoilWaterParameters);
                    }
                }

                // 2. 更新洪水计数器 (Flood Counter Logic)
                // 降雨强度因子：(2 * (降雨 - 0.5))^2，只有当降雨 > 0.5 时才显著增加
                float rainIntensity = math.max(0f, math.pow(2f * math.max(0f, m_Weather - 0.5f), 2f));

                FloodCounterData floodData = m_FloodCounterDatas[m_FloodCounterEntity];

                // 更新计数器：衰减 (0.98) + 降雨增量 - 自然消退 (0.1)
                floodData.m_FloodCounter = math.max(0f, 0.98f * floodData.m_FloodCounter + 2f * rainIntensity - 0.1f);

                // 3. 触发或更新洪水事件
                if (floodData.m_FloodCounter > 20f && m_FloodEntities.Length == 0)
                {
                    StartFlood();
                }
                else if (m_FloodEntities.Length > 0)
                {
                    if (floodData.m_FloodCounter == 0f)
                    {
                        StopFlood();
                    }
                    else
                    {
                        // 更新洪水强度
                        WaterLevelChange levelChange = m_Changes[m_FloodEntities[0]];
                        levelChange.m_Intensity = math.max(0f, (floodData.m_FloodCounter - 20f) / 80f);
                        m_Changes[m_FloodEntities[0]] = levelChange;
                    }
                }
                m_FloodCounterDatas[m_FloodCounterEntity] = floodData;

                // 4. 更新单元格状态 (Cell Update Pass)
                // 只更新当前帧负责的区域 (Load Distribution)
                int startRow = m_LoadDistributionIndex * kTextureSize / kLoadDistribution;
                int endRow = startRow + kTextureSize / kLoadDistribution;
                int startIdx = startRow * kTextureSize;
                int endIdx = endRow * kTextureSize;

                // 计算水面采样比率
                int2 waterSampleRatio = m_WaterSurfaceData.resolution.xz / kTextureSize;
                float inverseMaxDepth = 1f / (2f * m_SoilWaterParameters.m_MaximumWaterDepth);

                for (int j = startIdx; j < endIdx; j++)
                {
                    TargetType cellData = m_SoilWaterMap[j];

                    // 步骤 A: 应用扩散和降雨
                    // m_RainMultiplier 控制降雨对土壤水分的影响
                    cellData.m_Amount = (short)math.max(0, cellData.m_Amount + m_TempMap[j] + math.round(m_SoilWaterParameters.m_RainMultiplier * rainIntensity));

                    // 步骤 B: 更新地形高度
                    float3 worldPos = CellMapSystemRe.GetCellCenter(j,kTextureSize);
                    cellData.m_Surface = TerrainUtils.SampleHeight(ref m_TerrainHeightData, worldPos);

                    // 步骤 C: 计算地表水交互 (River/Ocean absorption)
                    // 计算该单元格能容纳的潜在水量 (剩余容量的一小部分)
                    short potentialAbsorption = (short)math.round(math.max(0f, 0.1f * (0.5f * cellData.m_Max - cellData.m_Amount)));
                    float waterAbsorptionVolume = (float)potentialAbsorption * m_SoilWaterParameters.m_WaterPerUnit / cellData.m_Max;

                    int validWaterSamples = 0;
                    int totalSamples = 0;
                    float accumulatedDepth = 0f;
                    float accumulatedAbsorption = 0f;

                    // 映射到 WaterSurfaceData 的坐标
                    int waterMapBaseIndex = (j % kTextureSize * waterSampleRatio.x) + (j / kTextureSize * m_WaterSurfaceData.resolution.x * waterSampleRatio.y);

                    // 采样周围的水体深度
                    for (int k = 0; k < waterSampleRatio.x; k += 4)
                    {
                        for (int l = 0; l < waterSampleRatio.y; l += 4)
                        {
                            int waterIndex = waterMapBaseIndex + k + l * m_WaterSurfaceData.resolution.z;
                            float depth = m_WaterSurfaceData.depths[waterIndex].m_Depth;

                            if (depth > 0.01f)
                            {
                                validWaterSamples++;
                                accumulatedDepth += math.min(m_SoilWaterParameters.m_MaximumWaterDepth, depth);
                                accumulatedAbsorption += math.min(waterAbsorptionVolume, depth);
                            }
                            totalSamples++;
                        }
                    }

                    // 修正吸收量
                    potentialAbsorption = (short)Math.Min(potentialAbsorption, math.round(cellData.m_Max * 10f * accumulatedAbsorption));
                    // 更新用于显示的变量
                    float visualWaterValue = (float)potentialAbsorption * m_SoilWaterParameters.m_WaterPerUnit / cellData.m_Max;

                    // 步骤 D: 计算溢出 (Overflow)
                    // 如果被水体覆盖，降低最大容量阈值，导致溢出
                    float capacityThreshold = (1f - inverseMaxDepth * accumulatedDepth / totalSamples) * cellData.m_Max;
                    short overflowAmount = (short)math.round(math.max(0f, m_SoilWaterParameters.m_OverflowRate * (cellData.m_Amount - capacityThreshold)));

                    float saturationRatio = 0f;
                    if (overflowAmount > 0)
                    {
                        saturationRatio = (float)cellData.m_Amount / cellData.m_Max;
                        visualWaterValue = 0f; // 溢出时不显示吸收效果
                    }
                    if (validWaterSamples == 0)
                    {
                        visualWaterValue = 0f;
                    }

                    // 步骤 E: 应用数值变化
                    cellData.m_Amount += potentialAbsorption; // 吸收河水
                    cellData.m_Amount -= overflowAmount;      // 溢出流失

                    // 自然恢复逻辑 (趋向于 1/8 容量?)
                    short equilibriumAdjustment = (short)math.round(math.sign(cellData.m_Max / 8 - cellData.m_Amount));
                    cellData.m_Amount += equilibriumAdjustment;

                    // 步骤 F: 更新纹理数据
                    // 这里的逻辑是将值编码进 float 纹理，供 Shader 使用
                    m_SoilWaterTextureData[j] = (0f - visualWaterValue) / m_ShaderUpdatesPerSoilUpdate + saturationRatio;

                    // 写回 Map
                    m_SoilWaterMap[j] = cellData;
                }

                // diffusionBuffer.Dispose();
            }
        }
        #endregion

        #region 序列化自适应
        // ==============================================================================
        // 序列化修复 (泛型化)
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

            // 调度自定义的序列化 Job
            JobHandle jobHandle = new SerializeJobMod<TWriter>
            {
                m_Stride = stride,
                m_Map = this.m_Map, // 使用基类的 Map
                m_WriterData = writerData
            }.Schedule(JobHandle.CombineDependencies(inputDeps, m_WriteDependencies));

            m_ReadDependencies = JobHandle.CombineDependencies(m_ReadDependencies, jobHandle);
            return jobHandle;
        }

        // 重写 Deserialize (无需迁移旧存档，Job会重新计算)
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
                    // 必须用 TempJob 防止 1024+ 尺寸导致内存溢出
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

                // 默认为 0 
                // m_Map 已经在 SetDefaults 中被清零了，如果这里不写入，就是重置状态

                if (reader.context.version > Game.Version.cellMapLengths)
                {
                    reader.Read(out int storedCount);

                    // 1. 判断是否匹配
                    bool sizeMatches = (math.abs(storedCount) == m_Map.Length);

                    // 2. 如果是原始数据 (Length > 0)
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
                            // m_Map 保持为 0
                        }
                    }
                    // 3. 如果是压缩数据 (Length < 0)
                    else if (storedCount < 0)
                    {
                        int actualCount = -storedCount;
                        reader.Read(out int byteLength);

                        // 必须读出来以清空流
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
                            // 否则：只读取字节流，不解压，m_Map 保持为 0
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

            float2 mapSize = new float2(CellMapSystemRe.kMapSize, CellMapSystemRe.kMapSize);

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
            // 辅助判断：只拦截对应的原版系统实例
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

