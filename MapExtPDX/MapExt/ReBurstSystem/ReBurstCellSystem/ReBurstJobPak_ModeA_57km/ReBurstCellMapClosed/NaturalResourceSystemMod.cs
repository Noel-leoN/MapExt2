// Game.Simulation.NaturalResourceSystem : CellMapSystem<NaturalResourceCell>, IJobSerializable, IPostDeserialize

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Areas;
using Game.City;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Serialization;
using Game.Simulation;
using Game.Tools;
using HarmonyLib;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    using MapExtPDX.MapExt.Core;
    using static Game.Areas.AreaResourceSystem;
    // =========================================================================================
    // [配置区域]
    // =========================================================================================
    // 1. 基类泛型
    using BaseCellMapSystem = CellMapSystem<NaturalResourceCell>;
    // 2. Mod 自定义系统类型 (当前类)
    using ModSystem = NaturalResourceSystemMod;
    // 3. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<NaturalResourceCell>;
    // 4. 原版系统类型 (用于禁用和定位)
    using TargetSystem = NaturalResourceSystem;
    // 5. T struct
    using TargetType = NaturalResourceCell;

    // =========================================================================================

    /// <summary>
    /// 自然资源系统
    /// 负责管理地图上的肥力、矿产、石油和鱼类资源。
    /// 处理资源的再生、污染带来的损耗以及资源变化对区域（Area）的影响。
    /// </summary>
    public partial class NaturalResourceSystemMod : BaseCellMapSystem, IJobSerializable, IPostDeserialize
    {
        #region Constants & Configuration

        // 单例引用，便于Harmony补丁调用
        public static ModSystem Instance { get; private set; }

        // 纹理尺寸(vanilla=256)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.NaturalResourceSystemkTextureSize; // mod尺寸
        public int2 TextureSize => new int2(kTextureSize, kTextureSize);

        // 地图尺寸
        // public const int MapSize = XCellMapSystemRe.kMapSize;

        // 系统更新周期：每日(月)32次
        public const int UPDATES_PER_DAY = 32;

        // 在模拟阶段减少更新频率，其他阶段每帧更新
        public override int GetUpdateInterval(SystemUpdatePhase phase) =>
            (phase == SystemUpdatePhase.GameSimulation) ? 262144 / UPDATES_PER_DAY : 1;

        // 原版系统其他常量(vanilla未使用)
        public const int MAX_BASE_RESOURCES = 10000; // 最大基础资源量
        public const int FERTILITY_REGENERATION_RATE = 800; // 肥力再生速率
        public const int FISH_REGENERATION_RATE = 800; // 鱼类再生速率

        public const int EDITOR_ROWS_PER_TICK = 4; // 编辑器模式下每帧更新的行数

        // 原版系统硬编码改为可配置字段
        public const int kFertilityRegenerationRate = 25; // 注意上面为800，这里是25
        public const int kFishRegenerationRate = 25;

        #endregion

        #region Dependencies

        private ToolSystem m_ToolSystem;
        private SimulationSystem m_SimulationSystem;
        private GroundPollutionSystem m_GroundPollutionSystem;
        private NoisePollutionSystem m_NoisePollutionSystem;
        private TerrainSystem m_TerrainSystem;
        private WaterSystem m_WaterSystem;
        private GroundWaterSystem m_GroundWaterSystem;
        private Game.Areas.SearchSystem m_AreaSearchSystem;
        private Game.Objects.SearchSystem m_ObjectSearchSystem;
        private CitySystem m_CitySystem;

        #endregion

        #region State

        private bool m_UpdateAll;
        private EntityQuery m_PollutionParameterQuery;
        private EntityQuery m_AreaConfigurationQuery;

        #endregion

        #region Lifecycle Methods

        [Preserve]
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
            Mod.Info(
                $"[{typeof(ModSystem).Name}] 创建自定义纹理: {typeof(TargetSystem).Name} kTextureSize 从 原值{TargetSystem.kTextureSize} 变更为 目标值{this.m_TextureSize.x}");
            // #endif

            // 3. 获取其他依赖和查询
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_GroundPollutionSystem = World.GetOrCreateSystemManaged<GroundPollutionSystem>();
            m_NoisePollutionSystem = World.GetOrCreateSystemManaged<NoisePollutionSystem>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_WaterSystem = World.GetOrCreateSystemManaged<WaterSystem>();
            m_GroundWaterSystem = World.GetOrCreateSystemManaged<GroundWaterSystem>();
            m_AreaSearchSystem = World.GetOrCreateSystemManaged<Game.Areas.SearchSystem>();
            m_ObjectSearchSystem = World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();

            // 构建查询
            m_PollutionParameterQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PollutionParameterData>()
                .Build(this);

            m_AreaConfigurationQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<AreasConfigurationData>()
                .Build(this);
        }

        protected override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override JobHandle SetDefaults(Context context)
        {
            // 如果是新游戏，生成初始资源分布
            JobHandle result = base.SetDefaults(context);
            if (context.purpose == Purpose.NewGame)
            {
                result.Complete();
                GenerateProceduralResources();
            }

            return result;
        }

        public void PostDeserialize(Context context)
        {
            // 如果存档版本旧（没有鱼类资源），强制全量更新
            if (!context.format.Has(FormatTags.FishResource))
            {
                this.Update();
                m_UpdateAll = true;
            }
        }

        private void GenerateProceduralResources()
        {
            // 使用 Perlin Noise 生成初始资源 (仅在新游戏时调用)
            float3 noiseOffset = default(float3);
            for (int i = 0; i < m_Map.Length; i++)
            {
                float u = (float)(i % kTextureSize) / (float)kTextureSize;
                float v = (float)(i / kTextureSize) / (float)kTextureSize;

                float3 scale = new float3(6.1f, 13.9f, 10.7f);
                float3 posU = u * scale;
                float3 posV = v * scale;

                noiseOffset.x = Mathf.PerlinNoise(posU.x, posV.x);
                noiseOffset.y = Mathf.PerlinNoise(posU.y, posV.y);
                noiseOffset.z = Mathf.PerlinNoise(posU.z, posV.z);

                // 调整噪声分布
                noiseOffset = (noiseOffset - new float3(0.4f, 0.7f, 0.7f)) * new float3(5f, 10f, 10f);
                noiseOffset = 10000f * math.saturate(noiseOffset);

                TargetType cell = default(TargetType);
                cell.m_Fertility.m_Base = (ushort)noiseOffset.x;
                cell.m_Ore.m_Base = (ushort)noiseOffset.y;
                cell.m_Oil.m_Base = (ushort)noiseOffset.z;

                m_Map[i] = cell;
            }
        }

        #endregion

        #region Update Loop

        protected override void OnUpdate()
        {
            // 1. 准备依赖数据
            JobHandle waterDep;
            WaterSurfaceData<SurfaceWater> surfaceData = m_WaterSystem.GetSurfaceData(out waterDep);

            // 确保分辨率匹配
            int2 waterResFactor = surfaceData.resolution.xz / kTextureSize;

            // 2. 准备并行写入队列
            NativeQueue<Entity> updateQueue = new NativeQueue<Entity>(Allocator.TempJob);
            NativeList<Entity> updateList = new NativeList<Entity>(Allocator.TempJob);

            // 3. 配置资源再生 Job (核心逻辑)
            var pollutionParams = m_PollutionParameterQuery.GetSingleton<PollutionParameterData>();

            RegenerateNaturalResourcesJob regenerateJob = new RegenerateNaturalResourcesJob
            {
                // 参数配置
                m_FertilityRegenerationRate = kFertilityRegenerationRate,
                m_FishRegenerationRate = kFertilityRegenerationRate,
                m_PollutionRate = pollutionParams.m_FertilityGroundMultiplier / 32f,
                m_WaterCellFactor = 300f / (waterResFactor.x * waterResFactor.y),
                m_MapOffset = -0.5f * XCellMapSystemRe.kMapSize,
                m_CellSize = (float)XCellMapSystemRe.kMapSize / kTextureSize,
                m_RandomSeed = RandomSeed.Next(),
                m_WaterResolutionFactor = waterResFactor,
                // m_TextureWidth = m_CellData.m_TextureSize.x,

                // 数据源
                m_GroundPollutionData = m_GroundPollutionSystem.GetData(true, out var depGroundPollution),
                m_NoisePollutionData = m_NoisePollutionSystem.GetData(true, out var depNoisePollution),
                m_WaterSurfaceData = surfaceData,
                m_AreaTree = m_AreaSearchSystem.GetSearchTree(true, out var depAreaTree),
                m_CellData = this.GetData(false, out var depCellData),

                // Lookups (使用 SystemAPI 替代原来的 TypeHandle)
                m_MapFeatureElements = SystemAPI.GetBufferLookup<MapFeatureElement>(true),
                m_Nodes = SystemAPI.GetBufferLookup<Node>(true),
                m_Triangles = SystemAPI.GetBufferLookup<Triangle>(true),

                // 输出
                m_UpdateQueue = updateQueue.AsParallelWriter()
            };

            // 4. 确定更新范围 (编辑器模式下分帧更新，游戏模式下可能全量或分块)
            int loopCount = kTextureSize;
            if (m_ToolSystem.actionMode.IsEditor() && !m_UpdateAll)
            {
                loopCount = EDITOR_ROWS_PER_TICK; // 每次只更新 4 行
                regenerateJob.m_ZOffset = (int)((m_SimulationSystem.frameIndex * loopCount) & (kTextureSize - 1));
            }

            m_UpdateAll = false;

            // 5. 调度 Job 链
            JobHandle deps = JobUtils.CombineDependencies(depGroundPollution, depNoisePollution, depAreaTree,
                depCellData, waterDep);
            deps = JobHandle.CombineDependencies(deps, Dependency);

            // 运行资源再生 (并行)
            JobHandle regenerateHandle = regenerateJob.Schedule(loopCount, 1, deps);

            // 收集受影响的 Entity (去重)
            CollectUpdatedAreasJob collectJob = new CollectUpdatedAreasJob
            {
                m_UpdateBuffer = updateQueue,
                m_UpdateList = updateList
            };
            JobHandle collectHandle = collectJob.Schedule(regenerateHandle);

            // 更新具体的 Area 组件 (如林业、渔业区域的数据)
            // 注意：AreaResourceSystem.UpdateAreaResourcesJob 是外部定义的 Job，这里保留原逻辑结构
            var areaConfig = m_AreaConfigurationQuery.GetSingleton<AreasConfigurationData>();

            AreaResourceSystem.UpdateAreaResourcesJob updateAreaJob = new AreaResourceSystem.UpdateAreaResourcesJob
            {
                m_City = m_CitySystem.City,
                m_FullUpdate = false,
                m_UpdateList = updateList.AsDeferredJobArray(),
                m_ObjectTree = m_ObjectSearchSystem.GetStaticSearchTree(true, out var depObjTree),
                m_NaturalResourceData = regenerateJob.m_CellData, // 传递修改后的数据
                m_GroundWaterResourceData = m_GroundWaterSystem.GetData(true, out var depGroundWater),

                // Component/Buffer Lookups
                m_GeometryData = SystemAPI.GetComponentLookup<Geometry>(true),
                m_TreeData = SystemAPI.GetComponentLookup<Tree>(true),
                // v1.5.4f 新增装饰组件，虽然当前系统不直接修改它，但更新区域时可能需要读取
                m_DecorationData = SystemAPI.GetComponentLookup<Decoration>(true),
                m_PlantData = SystemAPI.GetComponentLookup<Plant>(true),
                m_TransformData = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true),
                m_DamagedData = SystemAPI.GetComponentLookup<Damaged>(true),
                m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(true),
                m_ExtractorAreaData = SystemAPI.GetComponentLookup<ExtractorAreaData>(true),
                m_PrefabTreeData = SystemAPI.GetComponentLookup<TreeData>(true),
                m_Nodes = SystemAPI.GetBufferLookup<Node>(true),
                m_Triangles = SystemAPI.GetBufferLookup<Triangle>(true),
                m_CityModifiers = SystemAPI.GetBufferLookup<CityModifier>(true),

                // RW Lookups
                m_ExtractorData = SystemAPI.GetComponentLookup<Extractor>(false),
                m_WoodResources = SystemAPI.GetBufferLookup<WoodResource>(false),
                m_MapFeatureElements = SystemAPI.GetBufferLookup<MapFeatureElement>(false),

                // 其他数据
                m_TerrainHeightData = m_TerrainSystem.GetHeightData(),
                m_WaterSurfaceData = surfaceData,
                m_BuildableLandMaxSlope = areaConfig.m_BuildableLandMaxSlope
            };

            JobHandle finalHandle = updateAreaJob.Schedule(updateList, 1,
                JobHandle.CombineDependencies(collectHandle, depObjTree, depGroundWater));

            // 6. 资源管理与依赖注册
            updateQueue.Dispose(collectHandle);
            updateList.Dispose(finalHandle);

            // 通知系统写入/读取
            AddWriter(regenerateHandle); // 修改了 CellData
            AddReader(finalHandle); // 后续 Job 读取了 CellData

            m_GroundPollutionSystem.AddReader(regenerateHandle);
            m_NoisePollutionSystem.AddReader(regenerateHandle);
            m_AreaSearchSystem.AddSearchTreeReader(regenerateHandle);

            m_TerrainSystem.AddCPUHeightReader(finalHandle);
            m_WaterSystem.AddSurfaceReader(finalHandle);
            m_ObjectSearchSystem.AddStaticSearchTreeReader(finalHandle);
            m_GroundWaterSystem.AddReader(finalHandle);

            this.Dependency = finalHandle;
        }

        #endregion

        #region Jobs

        /// <summary>
        /// 核心 Job：重新计算自然资源（肥力、鱼类），并标记受影响的区域。
        /// </summary>
        [BurstCompile]
        private struct RegenerateNaturalResourcesJob : IJobParallelFor
        {
            [ReadOnly] public int m_ZOffset;

            // [ReadOnly] public int m_TextureWidth;
            [ReadOnly] public int m_FertilityRegenerationRate;
            [ReadOnly] public int m_FishRegenerationRate;
            [ReadOnly] public float m_PollutionRate;
            [ReadOnly] public float m_WaterCellFactor;
            [ReadOnly] public float m_MapOffset;
            [ReadOnly] public float m_CellSize;
            [ReadOnly] public RandomSeed m_RandomSeed;
            [ReadOnly] public int2 m_WaterResolutionFactor;

            [ReadOnly] public CellMapData<GroundPollution> m_GroundPollutionData;
            [ReadOnly] public CellMapData<NoisePollution> m_NoisePollutionData;
            [ReadOnly] public WaterSurfaceData<SurfaceWater> m_WaterSurfaceData;
            [ReadOnly] public NativeQuadTree<AreaSearchItem, QuadTreeBoundsXZ> m_AreaTree;

            [ReadOnly] public BufferLookup<MapFeatureElement> m_MapFeatureElements;
            [ReadOnly] public BufferLookup<Node> m_Nodes;
            [ReadOnly] public BufferLookup<Triangle> m_Triangles;

            // 注意：NativeDisableParallelForRestriction 是必需的，因为按行并行，虽然逻辑上不重叠，但编译器无法推断
            [NativeDisableParallelForRestriction] public TargetCellMapData m_CellData;

            public NativeQueue<Entity>.ParallelWriter m_UpdateQueue;

            // index 代表当前的行号 (Z轴索引)
            public void Execute(int index)
            {
                int zIndex = index + m_ZOffset;
                int rowStartIndex = zIndex * m_CellData.m_TextureSize.x; //m_TextureWidth;

                // 水面数据的索引偏移计算
                int waterRowStartIndex = zIndex * m_WaterResolutionFactor.y * m_WaterSurfaceData.resolution.x;

                // 准备 QuadTree 迭代器
                AreaIterator areaIterator = new AreaIterator
                {
                    m_MapFeatureElements = m_MapFeatureElements,
                    m_Nodes = m_Nodes,
                    m_Triangles = m_Triangles,
                    m_UpdateBuffer = m_UpdateQueue
                };

                // 遍历这一行的所有列 (X轴)
                for (int x = 0; x < m_CellData.m_TextureSize.x; x++)
                {
                    int cellIndex = rowStartIndex + x;

                    // 读取当前单元格数据
                    TargetType cell = m_CellData.m_Buffer[cellIndex];
                    GroundPollution groundPollution = m_GroundPollutionData.m_Buffer[cellIndex];
                    NoisePollution noisePollution = m_NoisePollutionData.m_Buffer[cellIndex];

                    Unity.Mathematics.Random random = m_RandomSeed.GetRandom(1 + cellIndex);

                    // --- 1. 肥力计算 (Fertility) ---
                    // 肥力会随时间恢复，但会被地面污染减少
                    int pollutionDamage = MathUtils.RoundToIntRandom(ref random,
                        (float)groundPollution.m_Pollution * m_PollutionRate);
                    int recoveredFertility = cell.m_Fertility.m_Used - m_FertilityRegenerationRate + pollutionDamage;

                    // 限制范围：不能小于0，不能超过基础肥力值
                    cell.m_Fertility.m_Used =
                        (ushort)math.min(cell.m_Fertility.m_Base, math.max(0, recoveredFertility));

                    // --- 2. 鱼类资源计算 (Fish) ---
                    // 采样对应区域的水深和水污染
                    int currentWaterIndex = waterRowStartIndex;
                    float totalWaterDepth = 0f;
                    float totalPollutedDepth = 0f;

                    // 由于水面分辨率可能高于资源网格，需要进行子采样累加
                    for (int j = 0; j < m_WaterResolutionFactor.y; j++)
                    {
                        int rowWaterIdx = currentWaterIndex;
                        for (int k = 0; k < m_WaterResolutionFactor.x; k++)
                        {
                            SurfaceWater surfaceWater = m_WaterSurfaceData.depths[rowWaterIdx++];
                            // 只有深度大于2米才算有效水域
                            float effectiveDepth = math.max(0f, surfaceWater.m_Depth - 2f);
                            totalWaterDepth += effectiveDepth;
                            totalPollutedDepth += effectiveDepth * surfaceWater.m_Polluted;
                        }

                        currentWaterIndex += m_WaterSurfaceData.resolution.x;
                    }

                    // 归一化水深和污染
                    totalWaterDepth *= m_WaterCellFactor;
                    totalPollutedDepth *= m_WaterCellFactor;

                    // 噪音污染也会吓跑鱼类 (转换为等效污染值)
                    totalPollutedDepth += totalWaterDepth * (float)noisePollution.m_Pollution * 6.25E-05f;

                    // 计算理论最大鱼类产量 (基于水深) 和 污染造成的限制
                    int potentialFish = (int)math.min(10000f, totalWaterDepth);
                    int pollutionLimit = (int)math.clamp(totalPollutedDepth * 50f, 0f, 10000f);

                    // 阈值过滤：如果鱼太少，视为无鱼
                    int2 fishCalc = new int2(potentialFish, cell.m_Fish.m_Base);
                    fishCalc = math.select(fishCalc, new int2(0, 20), (fishCalc > 0) & (fishCalc < 20));

                    // --- 3. 触发 Area 更新 ---
                    // 如果鱼类基础值发生显著变化（水干了或者新水源），通知覆盖该区域的 Area (如渔场)
                    if (math.abs(fishCalc.x - fishCalc.y) >= 20)
                    {
                        cell.m_Fish.m_Base = (ushort)fishCalc.x;

                        // 设置搜索包围盒
                        areaIterator.m_Bounds.min = m_MapOffset + new float2(x, zIndex) * m_CellSize;
                        areaIterator.m_Bounds.max = areaIterator.m_Bounds.min + m_CellSize;

                        // 在 QuadTree 中查找受影响的 Entity
                        m_AreaTree.Iterate(ref areaIterator);
                    }

                    // --- 4. 鱼类当前可用量更新 ---
                    // 如果当前存量小于污染限制，快速恢复（但也受污染上限压制）
                    // 如果当前存量大于污染限制，随时间自然减少
                    if (cell.m_Fish.m_Used < pollutionLimit)
                    {
                        int recovery = MathUtils.RoundToIntRandom(ref random, totalPollutedDepth * 3.125f);
                        cell.m_Fish.m_Used = (ushort)math.min(pollutionLimit, cell.m_Fish.m_Used + recovery);
                    }
                    else
                    {
                        cell.m_Fish.m_Used =
                            (ushort)math.max(pollutionLimit, cell.m_Fish.m_Used - m_FishRegenerationRate);
                    }

                    // 写回数据
                    m_CellData.m_Buffer[cellIndex] = cell;

                    // 水索引X轴推进
                    waterRowStartIndex += m_WaterResolutionFactor.x;
                }
            }
        }

        /// <summary>
        /// 辅助 Job：整理并行队列中的 Entity，去重并排序。
        /// </summary>
        [BurstCompile]
        private struct CollectUpdatedAreasJob : IJob
        {
            public NativeQueue<Entity> m_UpdateBuffer;
            public NativeList<Entity> m_UpdateList;

            public void Execute()
            {
                int count = m_UpdateBuffer.Count;
                m_UpdateList.ResizeUninitialized(count);

                // 将队列转入列表
                for (int i = 0; i < count; i++)
                {
                    m_UpdateList[i] = m_UpdateBuffer.Dequeue();
                }

                // 排序以方便去重
                m_UpdateList.Sort(new EntityComparer());

                // 去重逻辑
                if (m_UpdateList.Length > 0)
                {
                    int uniqueCount = 1;
                    Entity current = m_UpdateList[0];
                    for (int i = 1; i < m_UpdateList.Length; i++)
                    {
                        if (m_UpdateList[i] != current)
                        {
                            current = m_UpdateList[i];
                            m_UpdateList[uniqueCount++] = current;
                        }
                    }

                    // 移除多余元素
                    m_UpdateList.Resize(uniqueCount, NativeArrayOptions.ClearMemory);
                }
            }

            private struct EntityComparer : IComparer<Entity>
            {
                public int Compare(Entity x, Entity y) => x.Index - y.Index;
            }
        }

        // QuadTree 迭代器辅助结构
        private struct AreaIterator : INativeQuadTreeIterator<AreaSearchItem, QuadTreeBoundsXZ>,
            IUnsafeQuadTreeIterator<AreaSearchItem, QuadTreeBoundsXZ>
        {
            public Bounds2 m_Bounds;
            public BufferLookup<MapFeatureElement> m_MapFeatureElements;
            public BufferLookup<Node> m_Nodes;
            public BufferLookup<Triangle> m_Triangles;
            public NativeQueue<Entity>.ParallelWriter m_UpdateBuffer;

            public bool Intersect(QuadTreeBoundsXZ bounds)
            {
                return MathUtils.Intersect(bounds.m_Bounds.xz, this.m_Bounds);
            }

            public void Iterate(QuadTreeBoundsXZ bounds, AreaSearchItem item)
            {
                if (MathUtils.Intersect(bounds.m_Bounds.xz, this.m_Bounds) &&
                    this.m_MapFeatureElements.HasBuffer(item.m_Area))
                {
                    // 进一步精确检测：检查三角形与资源单元格是否相交
                    Triangle2 triangle = AreaUtils.GetTriangle2(this.m_Nodes[item.m_Area],
                        this.m_Triangles[item.m_Area][item.m_Triangle]);
                    if (MathUtils.Intersect(this.m_Bounds, triangle))
                    {
                        this.m_UpdateBuffer.Enqueue(item.m_Area);
                    }
                }
            }
        }

        #endregion

        #region 序列化自适应

        // ==============================================================================
        // 序列化修复 (泛型化)
        // ==============================================================================
        // 重写 Serialize 以处理大数据 (使用 TempJob)
        public new JobHandle Serialize<TWriter>(EntityWriterData writerData, JobHandle inputDeps)
            where TWriter : struct, IWriter
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
            m_WriteDependencies =
                jobData.Schedule(JobHandle.CombineDependencies(inputDeps, m_ReadDependencies, m_WriteDependencies));
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
                    finally
                    {
                        buffer.Dispose();
                    }
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
            dependencies = (readOnly
                ? m_WriteDependencies
                : JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies));

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
