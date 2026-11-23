// Game.Simulation.AvailabilityInfoToGridSystem : CellMapSystem<AvailabilityInfoCell>, IJobSerializable

// BC调用GetCellCenter/kTextureSize
// GetCellCenter/GetAvailabilityInfo/引用kMapSize/kTextureSize
// OnCreate() base.CreateTextures(AvailabilityInfoToGridSystem.kTextureSize);
// OnUpdate引用m_CellSize=CellMapSystem.kMapSize/kTextureSize
// OnUpdate Schedule引用kTextureSize

using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Game;
using Game.Common;
using Game.Net;
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
    using BaseCellMapSystem = CellMapSystem<AvailabilityInfoCell>;
    // 2. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<AvailabilityInfoCell>;
    // 3. 原版系统类型 (用于禁用和定位)
    using TargetSystem = AvailabilityInfoToGridSystem;
    // 4. T struct
    using TargetType = AvailabilityInfoCell;
    // =========================================================================================

    // 降级保护：合并版存档里本类的类型名是 MapExtPDX.CellMap.AvailabilityInfoToGridSystemMod，退回本版本时靠此别名读回，勿删
    /// <summary>
    /// 网格Heatmap，将网络（道管线）上的资源可用性数据（如吸引力、服务、就业机会）映射到一个二维网格Grid
    /// </summary>
    [Colossal.Serialization.Entities.FormerlySerializedAs("MapExtPDX.CellMap.AvailabilityInfoToGridSystemMod")]
    public partial class AvailabilityInfoToGridSystemMod : CellMapReplacementSystem<TargetType, TargetSystem>
    {
        private const string Tag = nameof(AvailabilityInfoToGridSystemMod);

        #region 静态字段配
        // 网格纹理尺寸(原版128x128)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.AvailabilityInfoToGridSystemkTextureSize;

        // 基类参数：日志 Tag、本模式的贴图边长与地图边长（XCellMapSystemRe 按模式命名空间解析）
        protected override string LogTag => Tag;
        protected override int ModTextureSize => kTextureSize;
        protected override int MapSize => XCellMapSystemRe.kMapSize;

        // 系统更新周期：每32
        public static readonly int kUpdatesPerDay = 32;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;
        #endregion

        // 系统依赖
        private SearchSystem m_NetSearchSystem;

        #region System Loop

        protected override void OnCreate()
        {
            // 1–2. 禁用原版系统、登记 Instance、创建自定义大小纹理由基类完成
            base.OnCreate();

            // 3. 获取其他依赖和查
            m_NetSearchSystem = World.GetOrCreateSystemManaged<SearchSystem>();
        }

        protected override void OnUpdate()
        {
            // 获取搜索树依
            NativeQuadTree<Entity, QuadTreeBoundsXZ> netSearchTree = m_NetSearchSystem.GetNetSearchTree(readOnly: true, out JobHandle searchTreeDep);

            // 准备 Job 数据
            AvailabilityInfoToGridJob job = new AvailabilityInfoToGridJob
            {
                m_AvailabilityInfoMap = m_Map,
                m_NetSearchTree = netSearchTree,
                m_AvailabilityData = SystemAPI.GetBufferLookup<ResourceAvailability>(isReadOnly: true),
                m_EdgeGeometryData = SystemAPI.GetComponentLookup<EdgeGeometry>(isReadOnly: true),
                m_CellSize = XCellMapSystemRe.kMapSize / kTextureSize
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

            // 更新系统依赖状
            Dependency = JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies, Dependency);
        }

        #endregion

        #region AvailabilityInfoToGridJob
        [BurstCompile]
        public struct AvailabilityInfoToGridJob : IJobParallelFor
        {
            // 输出：网格数据数
            public NativeArray<TargetType> m_AvailabilityInfoMap;

            [ReadOnly]
            public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;

            [ReadOnly]
            public BufferLookup<ResourceAvailability> m_AvailabilityData;

            [ReadOnly]
            public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;

            public float m_CellSize;

            // index 代表网格中的单元格索
            public void Execute(int index)
            {
                // 直接修补输入值；重定义字段为局部变量；
                int cellSize = XCellMapSystemRe.kMapSize / XCellMapSystemRe.AvailabilityInfoToGridSystemkTextureSize;
                // m_CellSize = (float)CellMapSystem<AvailabilityInfoCell>.kMapSize / (float)AvailabilityInfoToGridSystem.kTextureSize;

                // 重定义kTextureSize字段为局部变
                int textureSize = XCellMapSystemRe.AvailabilityInfoToGridSystemkTextureSize;

                // 计算当前单元格的世界坐标中心
                // mod重定
                float3 cellCenter = XCellMapSystemRe.GetCellCenter(index, textureSize);

                // 初始化迭代器
                AvailabilityInfoToGridNetIterator netIterator = default(AvailabilityInfoToGridNetIterator);
                netIterator.m_TotalWeight = default(TargetType);
                netIterator.m_Result = default(TargetType);

                // 设置搜索包围盒：Cell 中心向外扩展 1.5 CellSize
                // Y轴范围设置得非常(10000f) 以忽略高度差
                float3 searchExtents = new float3(1.5f * cellSize, 10000f, 1.5f * cellSize);
                netIterator.m_Bounds = new Bounds3(cellCenter - searchExtents, cellCenter + searchExtents);

                netIterator.m_CellSize = cellSize;
                netIterator.m_EdgeGeometryData = this.m_EdgeGeometryData;
                netIterator.m_Availabilities = this.m_AvailabilityData;

                // 执行四叉树范围查
                AvailabilityInfoToGridNetIterator iterator = netIterator;
                this.m_NetSearchTree.Iterate(ref iterator);

                // 读取当前单元格的旧值（或者直接覆盖，取决于上下文，这里是读取结构体以写入其字段）
                TargetType cellValue = this.m_AvailabilityInfoMap[index];

                // 计算加权平均值： 总和 / 总权
                // 使用 math.select 防止除以(如果总权重为0，则结果)
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
            // [累加器] 用于记录所有采样点的总权重（分母），用于最后求平均
            public TargetType m_TotalWeight;

            // [累加器] 用于记录所有采样点的加权数值总和（分子）
            public TargetType m_Result;

            // 网格单元的大小，用于计算搜索范围和距离衰
            public float m_CellSize;

            // 当前查询的包围盒（Grid Cell 的搜索范围）
            public Bounds3 m_Bounds;

            // ECS 数据查找表：资源可用性数据（Buffer
            public BufferLookup<ResourceAvailability> m_Availabilities;

            // ECS 数据查找表：边的几何形状数据（Component
            public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;

            // 接口方法：检查四叉树节点的包围盒是否与查询包围盒相交
            public bool Intersect(QuadTreeBoundsXZ bounds)
            {
                return MathUtils.Intersect(bounds.m_Bounds, this.m_Bounds);
            }

            // 辅助方法：将采样点的数据按权重累加到结果
            // 参数说明
            // - attractivenessRange ~ workplacesRange: 资源数值的范围（x=最小 y=最大值）
            // - globalProgress (t): 当前采样点在整条边上的进(0.0 ~ 1.0)
            // - samplePos: 采样点的世界坐标
            // - weight: 基于距离的权
            private void AddData(float2 attractivenessRange, float2 uneducatedRange, float2 educatedRange, float2 servicesRange, float2 workplacesRange, float2 globalProgress, float3 samplePos, float weight)
            {
                // 根据进度 t.y 在数据的 Min/Max 之间插值，得到当前点的数据
                float currentAttractiveness = math.lerp(attractivenessRange.x, attractivenessRange.y, globalProgress.y);
                // 消费者数量是 未受教育 + 受教的总和的一半（可能是为了平衡数值）
                float currentConsumers = 0.5f * math.lerp(uneducatedRange.x + educatedRange.x, uneducatedRange.y + educatedRange.y, globalProgress.y);
                float currentServices = math.lerp(servicesRange.x, servicesRange.y, globalProgress.y);
                float currentWorkplaces = math.lerp(workplacesRange.x, workplacesRange.y, globalProgress.y);

                // 累加加权后的数
                this.m_Result.AddAttractiveness(weight * currentAttractiveness);
                this.m_TotalWeight.AddAttractiveness(weight);

                this.m_Result.AddConsumers(weight * currentConsumers);
                this.m_TotalWeight.AddConsumers(weight);

                this.m_Result.AddServices(weight * currentServices);
                this.m_TotalWeight.AddServices(weight);

                this.m_Result.AddWorkplaces(weight * currentWorkplaces);
                this.m_TotalWeight.AddWorkplaces(weight);
            }

            // 核心逻辑：当四叉树找到一Entity 时调用此方法
            public void Iterate(QuadTreeBoundsXZ bounds, Entity entity)
            {
                // 1. 再次精确检查包围盒相交
                // 2. 检Entity 是否拥有必要的数据组
                if (MathUtils.Intersect(bounds.m_Bounds, this.m_Bounds) &&
                    this.m_Availabilities.HasBuffer(entity) &&
                    this.m_EdgeGeometryData.HasComponent(entity))
                {
                    // 获取资源数据 Buffer
                    DynamicBuffer<ResourceAvailability> availabilitiesBuffer = this.m_Availabilities[entity];

                    // Buffer 的特定索引读取不同类型的数据范围 (Min, Max)
                    // 注意：这里的索引 (18, 2, 3...) 是硬编码的，对应具体的游戏逻辑定义
                    float2 attractivenessData = availabilitiesBuffer[18].m_Availability;
                    float2 uneducatedData = availabilitiesBuffer[2].m_Availability;
                    float2 educatedData = availabilitiesBuffer[3].m_Availability;
                    float2 servicesData = availabilitiesBuffer[1].m_Availability;
                    float2 workplacesData = availabilitiesBuffer[0].m_Availability;

                    // 获取几何形状
                    EdgeGeometry edgeGeometry = this.m_EdgeGeometryData[entity];

                    // 根据边的长度动态计算采样步数（采样密度
                    // 边的形状通常分为 Start 部分End 部分（例如贝塞尔曲线的两段控制）
                    int startSegmentSteps = (int)math.ceil(edgeGeometry.m_Start.middleLength * 0.05f);
                    int endSegmentSteps = (int)math.ceil(edgeGeometry.m_End.middleLength * 0.05f);
                    float totalSteps = startSegmentSteps + endSegmentSteps;

                    // 计算当前查询中心点（网格单元中心
                    float3 queryCenter = 0.5f * (this.m_Bounds.min + this.m_Bounds.max);

                    // --- 循环处理 Start 线段部分 ---
                    for (int i = 1; i <= startSegmentSteps; i++)
                    {
                        // progress.x = 当前线段内的局部进(0~1)
                        // progress.y = 整条边的全局进度 (0~1)，用Lerp 数据
                        float2 progress = i / new float2(startSegmentSteps, totalSteps);

                        // 计算曲线上的采样点位(Lerp 左侧几何和右侧几何的中间位置)
                        float3 samplePos = math.lerp(
                            MathUtils.Position(edgeGeometry.m_Start.m_Left, progress.x),
                            MathUtils.Position(edgeGeometry.m_Start.m_Right, progress.x),
                            0.5f);

                        // 计算权重：距离越近权重越高。超1.5CellSize 则权重为0
                        float distToSample = math.distance(queryCenter.xz, samplePos.xz);
                        float distWeight = math.max(0f, 1f - distToSample / (1.5f * this.m_CellSize));

                        this.AddData(attractivenessData, uneducatedData, educatedData, servicesData, workplacesData, progress, samplePos, distWeight);
                    }

                    // --- 循环处理 End 线段部分 ---
                    for (int j = 1; j <= endSegmentSteps; j++)
                    {
                        // 注意这里的全局进度计算：需要加startSegmentSteps (num)
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

} // namespace end;


