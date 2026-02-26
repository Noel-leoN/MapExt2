// Game.Simulation.AvailabilityInfoToGridSystem : CellMapSystem<AvailabilityInfoCell>, IJobSerializable

// BC调用GetCellCenter/kTextureSize
// GetCellCenter/GetAvailabilityInfo/引用kMapSize/kTextureSize
// OnCreate() base.CreateTextures(AvailabilityInfoToGridSystem.kTextureSize);
// OnUpdate引用m_CellSize=CellMapSystem.kMapSize/kTextureSize
// OnUpdate Schedule引用kTextureSize

using System.Runtime.CompilerServices;
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Simulation;
using HarmonyLib;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.ReBurstSystemModeC
{
    // =========================================================================================
    // [配置区域]
    // =========================================================================================
    // 1. 基类泛型
    using BaseCellMapSystem = CellMapSystem<AvailabilityInfoCell>;
    // 2. Mod 自定义系统类型 (当前类)
    using ModSystem = AvailabilityInfoToGridSystemMod;
    // 3. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<AvailabilityInfoCell>;
    // 4. 原版系统类型 (用于禁用和定位)
    using TargetSystem = AvailabilityInfoToGridSystem;
    // 5. T struct
    using TargetType = AvailabilityInfoCell;
using MapExtPDX.MapExt.Core;
    // =========================================================================================

    /// <summary>
    /// 网格Heatmap，将网络（道路/管线）上的资源可用性数据（如吸引力、服务、就业机会）映射到一个二维网格Grid
    /// </summary>
    public partial class AvailabilityInfoToGridSystemMod : BaseCellMapSystem, IJobSerializable
    {
        // 单例引用，便于Harmony补丁调用
        public static ModSystem Instance { get; private set; }

        #region 静态字段配置
        // 网格纹理尺寸(原版128x128)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = CellMapSystemRe.AvailabilityInfoToGridSystemkTextureSize;
        public int2 TextureSize => new int2(kTextureSize, kTextureSize);

        // 系统更新周期：每日(月)32次
        public static readonly int kUpdatesPerDay = 32;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;
        #endregion

        // 系统依赖
        private SearchSystem m_NetSearchSystem;

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
                //#if DEBUG
                Mod.Info($"[{typeof(ModSystem).Name}] 禁用原系统: {typeof(TargetSystem).Name}");
                //#endif
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
            //#if DEBUG
            Mod.Info($"[{typeof(ModSystem).Name}] 创建自定义纹理: {typeof(TargetSystem).Name} kTextureSize 从 原值{TargetSystem.kTextureSize} 变更为 目标值{this.m_TextureSize.x}");
            //#endif

            // 3. 获取其他依赖和查询
            m_NetSearchSystem = World.GetOrCreateSystemManaged<SearchSystem>();
        }

        protected override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            // 获取搜索树依赖
            NativeQuadTree<Entity, QuadTreeBoundsXZ> netSearchTree = m_NetSearchSystem.GetNetSearchTree(readOnly: true, out JobHandle searchTreeDep);

            // 准备 Job 数据
            AvailabilityInfoToGridJob job = new AvailabilityInfoToGridJob
            {
                m_AvailabilityInfoMap = m_Map,
                m_NetSearchTree = netSearchTree,
                m_AvailabilityData = SystemAPI.GetBufferLookup<ResourceAvailability>(isReadOnly: true),
                m_EdgeGeometryData = SystemAPI.GetComponentLookup<EdgeGeometry>(isReadOnly: true),
                m_CellSize = CellMapSystemRe.kMapSize / kTextureSize
            };

            // 组合依赖项：SearchSystem 读取依赖 + 自身读写依赖
            JobHandle inputDeps = JobUtils.CombineDependencies(
                searchTreeDep,
                m_WriteDependencies,
                m_ReadDependencies,
                Dependency
            );

            // 调度并行 Job
            Dependency = IJobParallelForExtensions.Schedule(
                job,
                kTextureSize * kTextureSize,
                kTextureSize,
                inputDeps
            );

            // 注册资源访问
            AddWriter(Dependency);
            m_NetSearchSystem.AddNetSearchTreeReader(Dependency);

            // 更新系统依赖状态
            Dependency = JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies, Dependency);
        }

        #endregion

        #region AvailabilityInfoToGridJob
        [BurstCompile]
        public struct AvailabilityInfoToGridJob : IJobParallelFor
        {
            // 输出：网格数据数组
            public NativeArray<TargetType> m_AvailabilityInfoMap;

            [ReadOnly]
            public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;

            [ReadOnly]
            public BufferLookup<ResourceAvailability> m_AvailabilityData;

            [ReadOnly]
            public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;

            public float m_CellSize;

            // index 代表网格中的单元格索引
            public void Execute(int index)
            {
                // 直接修补输入值；重定义字段为局部变量；
                int cellSize = CellMapSystemRe.kMapSize / CellMapSystemRe.AvailabilityInfoToGridSystemkTextureSize;
                // 57344f/128f; m_CellSize = (float)CellMapSystem<AvailabilityInfoCell>.kMapSize / (float)AvailabilityInfoToGridSystem.kTextureSize;

                // 重定义kTextureSize字段为局部变量
                int textureSize = CellMapSystemRe.AvailabilityInfoToGridSystemkTextureSize;

                // 计算当前单元格的世界坐标中心
                // mod重定向
                float3 cellCenter = CellMapSystemRe.GetCellCenter(index, textureSize);

                // 初始化迭代器
                AvailabilityInfoToGridNetIterator netIterator = default(AvailabilityInfoToGridNetIterator);
                netIterator.m_TotalWeight = default(TargetType);
                netIterator.m_Result = default(TargetType);

                // 设置搜索包围盒：以 Cell 中心向外扩展 1.5 倍 CellSize
                // Y轴范围设置得非常大 (10000f) 以忽略高度差异
                float3 searchExtents = new float3(1.5f * cellSize, 10000f, 1.5f * cellSize);
                netIterator.m_Bounds = new Bounds3(cellCenter - searchExtents, cellCenter + searchExtents);

                netIterator.m_CellSize = cellSize;
                netIterator.m_EdgeGeometryData = this.m_EdgeGeometryData;
                netIterator.m_Availabilities = this.m_AvailabilityData;

                // 执行四叉树范围查询
                AvailabilityInfoToGridNetIterator iterator = netIterator; // 复制一份以传递 ref
                this.m_NetSearchTree.Iterate(ref iterator);

                // 读取当前单元格的旧值（或者直接覆盖，取决于上下文，这里是读取结构体以写入其字段）
                TargetType cellValue = this.m_AvailabilityInfoMap[index];

                // 计算加权平均值： 总和 / 总权重
                // 使用 math.select 防止除以零 (如果总权重为0，则结果为0)
                cellValue.m_AvailabilityInfo = math.select(
                    iterator.m_Result.m_AvailabilityInfo / iterator.m_TotalWeight.m_AvailabilityInfo,
                    0f,
                    iterator.m_TotalWeight.m_AvailabilityInfo == 0f // 判断条件
                );

                // 写回结果数组
                this.m_AvailabilityInfoMap[index] = cellValue;
            } // Execute end;


        } // AvailabilityInfoToGridJob end;

        // 迭代器结构：负责在四叉树查询过程中处理每一个找到的 Entity
        private struct AvailabilityInfoToGridNetIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            // [累加器] 用于记录所有采样点的总权重（分母），用于最后求平均值
            public TargetType m_TotalWeight;

            // [累加器] 用于记录所有采样点的加权数值总和（分子）
            public TargetType m_Result;

            // 网格单元的大小，用于计算搜索范围和距离衰减
            public float m_CellSize;

            // 当前查询的包围盒（Grid Cell 的搜索范围）
            public Bounds3 m_Bounds;

            // ECS 数据查找表：资源可用性数据（Buffer）
            public BufferLookup<ResourceAvailability> m_Availabilities;

            // ECS 数据查找表：边的几何形状数据（Component）
            public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;

            // 接口方法：检查四叉树节点的包围盒是否与查询包围盒相交
            public bool Intersect(QuadTreeBoundsXZ bounds)
            {
                return MathUtils.Intersect(bounds.m_Bounds, this.m_Bounds);
            }

            // 辅助方法：将采样点的数据按权重累加到结果中
            // 参数说明：
            // - attractivenessRange ~ workplacesRange: 资源数值的范围（x=最小值, y=最大值）
            // - globalProgress (t): 当前采样点在整条边上的进度 (0.0 ~ 1.0)
            // - samplePos: 采样点的世界坐标
            // - weight: 基于距离的权重
            private void AddData(float2 attractivenessRange, float2 uneducatedRange, float2 educatedRange, float2 servicesRange, float2 workplacesRange, float2 globalProgress, float3 samplePos, float weight)
            {
                // 根据进度 t.y 在数据的 Min/Max 之间插值，得到当前点的数据值
                float currentAttractiveness = math.lerp(attractivenessRange.x, attractivenessRange.y, globalProgress.y);
                // 消费者数量是 未受教育 + 受教育 的总和的一半（可能是为了平衡数值）
                float currentConsumers = 0.5f * math.lerp(uneducatedRange.x + educatedRange.x, uneducatedRange.y + educatedRange.y, globalProgress.y);
                float currentServices = math.lerp(servicesRange.x, servicesRange.y, globalProgress.y);
                float currentWorkplaces = math.lerp(workplacesRange.x, workplacesRange.y, globalProgress.y);

                // 累加加权后的数值
                this.m_Result.AddAttractiveness(weight * currentAttractiveness);
                this.m_TotalWeight.AddAttractiveness(weight);

                this.m_Result.AddConsumers(weight * currentConsumers);
                this.m_TotalWeight.AddConsumers(weight);

                this.m_Result.AddServices(weight * currentServices);
                this.m_TotalWeight.AddServices(weight);

                this.m_Result.AddWorkplaces(weight * currentWorkplaces);
                this.m_TotalWeight.AddWorkplaces(weight);
            }

            // 核心逻辑：当四叉树找到一个 Entity 时调用此方法
            public void Iterate(QuadTreeBoundsXZ bounds, Entity entity)
            {
                // 1. 再次精确检查包围盒相交
                // 2. 检查 Entity 是否拥有必要的数据组件
                if (MathUtils.Intersect(bounds.m_Bounds, this.m_Bounds) &&
                    this.m_Availabilities.HasBuffer(entity) &&
                    this.m_EdgeGeometryData.HasComponent(entity))
                {
                    // 获取资源数据 Buffer
                    DynamicBuffer<ResourceAvailability> availabilitiesBuffer = this.m_Availabilities[entity];

                    // 从 Buffer 的特定索引读取不同类型的数据范围 (Min, Max)
                    // 注意：这里的索引 (18, 2, 3...) 是硬编码的，对应具体的游戏逻辑定义
                    float2 attractivenessData = availabilitiesBuffer[18].m_Availability;
                    float2 uneducatedData = availabilitiesBuffer[2].m_Availability;
                    float2 educatedData = availabilitiesBuffer[3].m_Availability;
                    float2 servicesData = availabilitiesBuffer[1].m_Availability;
                    float2 workplacesData = availabilitiesBuffer[0].m_Availability;

                    // 获取几何形状
                    EdgeGeometry edgeGeometry = this.m_EdgeGeometryData[entity];

                    // 根据边的长度动态计算采样步数（采样密度）
                    // 边的形状通常分为 Start 部分和 End 部分（例如贝塞尔曲线的两段控制）
                    int startSegmentSteps = (int)math.ceil(edgeGeometry.m_Start.middleLength * 0.05f);
                    int endSegmentSteps = (int)math.ceil(edgeGeometry.m_End.middleLength * 0.05f);
                    float totalSteps = startSegmentSteps + endSegmentSteps;

                    // 计算当前查询中心点（网格单元中心）
                    float3 queryCenter = 0.5f * (this.m_Bounds.min + this.m_Bounds.max);

                    // --- 循环处理 Start 线段部分 ---
                    for (int i = 1; i <= startSegmentSteps; i++)
                    {
                        // progress.x = 当前线段内的局部进度 (0~1)
                        // progress.y = 整条边的全局进度 (0~1)，用于 Lerp 数据值
                        float2 progress = i / new float2(startSegmentSteps, totalSteps);

                        // 计算曲线上的采样点位置 (Lerp 左侧几何和右侧几何的中间位置)
                        float3 samplePos = math.lerp(
                            MathUtils.Position(edgeGeometry.m_Start.m_Left, progress.x),
                            MathUtils.Position(edgeGeometry.m_Start.m_Right, progress.x),
                            0.5f);

                        // 计算权重：距离越近权重越高。超过 1.5倍 CellSize 则权重为0。
                        float distToSample = math.distance(queryCenter.xz, samplePos.xz);
                        float distWeight = math.max(0f, 1f - distToSample / (1.5f * this.m_CellSize));

                        this.AddData(attractivenessData, uneducatedData, educatedData, servicesData, workplacesData, progress, samplePos, distWeight);
                    }

                    // --- 循环处理 End 线段部分 ---
                    for (int j = 1; j <= endSegmentSteps; j++)
                    {
                        // 注意这里的全局进度计算：需要加上 startSegmentSteps (即 num)
                        float2 progress = new float2(j, startSegmentSteps + j) / new float2(endSegmentSteps, totalSteps);

                        float3 samplePos = math.lerp(
                            MathUtils.Position(edgeGeometry.m_End.m_Left, progress.x),
                            MathUtils.Position(edgeGeometry.m_End.m_Right, progress.x),
                            0.5f);

                        float distToSample = math.distance(queryCenter.xz, samplePos.xz);
                        float distWeight = math.max(0f, 1f - distToSample / (1.5f * this.m_CellSize));

                        this.AddData(attractivenessData, uneducatedData, educatedData, servicesData, workplacesData, progress, samplePos, distWeight);
                    }
                }
            } // Iterate end;
        } // NetIterator end; 
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
                if (!(reader.context.version > Version.stormWater)) return;

                // 默认为 0 
                // m_Map 已经在 SetDefaults 中被清零了，如果这里不写入，就是重置状态

                if (reader.context.version > Version.cellMapLengths)
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

} // namespace end;
