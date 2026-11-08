// Game.Simulation.AirPollutionSystem : CellMapSystem<AirPollution>, IJobSerializable

// 需修改内容
// BC调用GetCellCenter/GetPollution/GetWind/kTextureSize
// GetCellCenter/GetPollution引用kMapSize/kTextureSize
// OnCreate() base.CreateTextures(AirPollutionSystem.kTextureSize);

// CellMapSystem<T>解决方案
// 1. ECS替换模式优点：可全面修改系统行为，灵活控制；缺点：维护稍复杂)
// a. 直接在Job中引用自定义静态常量和静态方
// b. 继承CellMapSystem<T>创建Mod系统类，重写OnCreate/OnUpdate等方
// c. 禁用原版系统，使用Harmony补丁重定向方法调
// d. 存档兼容性处理，可实现map贴图尺寸任意变更
// 2. 泛型化：方便批量修改，使用别名定义泛型类型，配合Harmony补丁重定向方法调
// 3. 规范化：修正为ECS现代规范代码格式，局部变量改为易读名
// 注意：仅限于CellMapSystem<T>类，其他类型(非经济类)仍采用Job通用替换处理,避免过度复杂可能需要Harmony重定向所有外系统调用方法)
// 注意2：TelecomCoverage采用Job内硬编码/且实用性不大，暂不修改
// 注意3：WindSystem相关系统需大量调整其他参数，暂不修改

// 批量修改方式
// 1.配置区域->命名空间->类定
// 2.静态字段配原版系统引用依赖
// 3.OnCreate/OnUpdate(注意尽量使用TempJob分配临时缓冲区避免大型贴图内存溢
// 4.Job
// 5.存档读写、GetData修正与重定向判断由基类 CellMapReplacementSystem 提供（重构三），Patches 只留非泛型薄壳
// 其他直接套用

using Colossal.Entities;
using Colossal.Mathematics;
using Game;
using Game.Common;
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
    using BaseCellMapSystem = CellMapSystem<AirPollution>;
    // 2. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<AirPollution>;
    // 3. 原版系统类型 (用于禁用和定位)
    using TargetSystem = AirPollutionSystem;
    // 4. T struct
    using TargetType = AirPollution;
    // =========================================================================================

    /// <summary>
    /// 空气污染计算、扩散、衰减系
    /// 负责模拟空气中污染物的传播和消散过程
    /// 通过风力影响污染的移动，并考虑自然衰减和扩散
    /// </summary>
    // 降级保护（重构二 0.4）：合并版存档里本类的类型名是 MapExtPDX.CellMap.AirPollutionSystemMod，退回本版本时靠此别名读回，勿删
    [Colossal.Serialization.Entities.FormerlySerializedAs("MapExtPDX.CellMap.AirPollutionSystemMod")]
    public partial class AirPollutionSystemMod : CellMapReplacementSystem<TargetType, TargetSystem>
    {
        private const string Tag = nameof(AirPollutionSystemMod);

        #region 字段/配置
        // 纹理尺寸(vanilla=256)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.AirPollutionSystemkTextureSize; // mod尺寸

        // 基类参数：日志 Tag、本模式的贴图边长与地图边长（XCellMapSystemRe 按模式命名空间解析）
        protected override string LogTag => Tag;
        protected override int ModTextureSize => kTextureSize;
        protected override int MapSize => XCellMapSystemRe.kMapSize;

        // 系统更新周期：每次数128
        public static readonly int kUpdatesPerDay = 128;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;

        // 扩散系数位移
        private static readonly int kSpreadShift = 3;
        #endregion

        #region 查询和系统引
        // 系统依赖
        private WindSystem m_WindSystem;
        // 查询
        private EntityQuery m_PollutionParameterQuery;
        #endregion 

        #region System Loop
        protected override void OnCreate()
        {
            // 1–2. 禁用原版系统、登记 Instance、创建自定义大小纹理由基类完成
            base.OnCreate();

            // 3. 获取其他依赖和查
            m_WindSystem = World.GetOrCreateSystemManaged<WindSystem>();
            m_PollutionParameterQuery = GetEntityQuery(ComponentType.ReadOnly<PollutionParameterData>());
            // 强制依赖更新
            RequireForUpdate(m_PollutionParameterQuery);
        }

        protected override void OnUpdate()
        {
            // 获取参数
            var pParams = m_PollutionParameterQuery.GetSingleton<PollutionParameterData>();
            // 获取风力数据 (只读)
            var windMap = m_WindSystem.GetMap(true, out JobHandle windDeps);

            // 暂存要跨多个并行 Job、跨线程共用，Job 内的 Temp 用不了，所以用 TempJob，随 Job 链在末尾释放。
            // 超过 TempJob 块大小（2 MB）的分配实际走默认堆，与 Persistent 同价；选 TempJob 只为自动释放（v4.12.0 整合计划 §1.4）。
            // 分配时清零（ClearMemory）：活跃区外的暂存格保持 0，扩散读到的就是正确的 0
            NativeArray<TargetType> scratchMap = new(m_Map.Length, Allocator.TempJob);

            // 活跃区（ModeC 标定计划 4.3）：每行非零格的跨度，及据此扩张出的平流、扩散范围
            NativeArray<int2> rowSpans = new(kTextureSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<int4> bounds = new(2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            // 合并依赖
            JobHandle combinedDeps = JobUtils.CombineDependencies(windDeps, m_WriteDependencies, m_ReadDependencies, Dependency);

            // 活跃区必须在本次更新开始时按当前污染图求：两次更新之间，建筑、道路与车辆会往任意位置写入
            AirPollutionRowSpanJob spanJob = new()
            {
                m_PollutionMap = m_Map,
                m_RowSpans = rowSpans
            };
            AirPollutionBoundsJob boundsJob = new()
            {
                m_RowSpans = rowSpans,
                m_WindMap = windMap,
                m_WindAdvectionSpeed = pParams.m_WindAdvectionSpeed,
                m_Bounds = bounds
            };

            // === 趟1 平流(Advection)：只算平流范围内的格。每格只读唯读输入、只写自己的暂存格、无随机数，
            // 范围外的格采样到的全是 0，结果与全图计算逐位相同
            AirPollutionAdvectRowJob advectJob = new()
            {
                m_PollutionMap = m_Map,
                m_WindMap = windMap,
                m_PollutionParameters = pParams,
                m_Bounds = bounds,
                m_TempMap = scratchMap
            };

            // === 趟2 扩散(Diffusion)＋衰减(Decay)：只算扩散范围内的格，按行并行，每行一条随机流。
            // 随机舍入序列与原版不同，统计上等价（ModeC 标定计划 4.2）
            AirPollutionDiffuseRowJob diffuseJob = new()
            {
                m_PollutionMap = m_Map,
                m_TempMap = scratchMap,
                m_PollutionParameters = pParams,
                m_Bounds = bounds,
                m_Random = RandomSeed.Next()
            };

            JobHandle spanHandle = spanJob.Schedule(kTextureSize, 16, combinedDeps);
            JobHandle boundsHandle = boundsJob.Schedule(spanHandle);
            JobHandle advectHandle = advectJob.Schedule(kTextureSize, 4, boundsHandle);
            Dependency = diffuseJob.Schedule(kTextureSize, 4, advectHandle);

            // [关键] TempJob 的自动释放必须挂在最后一个 Job 上
            // （挂在 advectHandle 上会在趟2 读取期间被释放）
            scratchMap.Dispose(Dependency);
            rowSpans.Dispose(Dependency);
            bounds.Dispose(Dependency);

            // windMap 被活跃区与趟1 读取，挂在 advectHandle 上可让 WindSystem 更早解除等待
            m_WindSystem.AddReader(advectHandle);
            AddWriter(Dependency);

            // 与原版 AirPollutionSystem 收尾一致
            // （旧实作漏了这一步，并多写一次 m_WriteDependencies —— AddWriter 内部即为覆盖赋值）
            Dependency = JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies, Dependency);
        }

        #endregion

        #region 活跃区与按行 Job（ModeC 标定计划 4.2／4.3）

        /// <summary>每行非零格的 x 跨度；空行写 (int.MaxValue, int.MinValue)。</summary>
        [BurstCompile]
        public struct AirPollutionRowSpanJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<TargetType> m_PollutionMap;

            [WriteOnly] public NativeArray<int2> m_RowSpans;

            public void Execute(int y)
            {
                int rowStart = y * kTextureSize;
                int minX = int.MaxValue;
                int maxX = int.MinValue;
                for (int x = 0; x < kTextureSize; x++)
                {
                    if (m_PollutionMap[rowStart + x].m_Pollution != 0)
                    {
                        minX = x;
                        break;
                    }
                }

                if (minX != int.MaxValue)
                {
                    for (int x = kTextureSize - 1; x >= minX; x--)
                    {
                        if (m_PollutionMap[rowStart + x].m_Pollution != 0)
                        {
                            maxX = x;
                            break;
                        }
                    }
                }

                m_RowSpans[y] = new int2(minX, maxX);
            }
        }

        /// <summary>
        /// 由各行跨度求非零外接矩形，再扩张出两个范围：[0] 平流范围、[1] 扩散范围，格式 (minX, minY, maxX, maxY)。
        /// <para>平流的采样源离格中心不超过"平流系数 × 最大风速"，双线性再多读一格；扩散再读四邻，多扩一格。
        /// 范围外的格读到的全是 0，算出来也是 0，与全图计算相同。全图为 0 时两个范围都是空的（min &gt; max）。</para>
        /// </summary>
        [BurstCompile]
        public struct AirPollutionBoundsJob : IJob
        {
            [ReadOnly] public NativeArray<int2> m_RowSpans;

            [ReadOnly] public NativeArray<Wind> m_WindMap;

            public float m_WindAdvectionSpeed;

            public NativeArray<int4> m_Bounds;

            public void Execute()
            {
                int4 box = new int4(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
                for (int y = 0; y < kTextureSize; y++)
                {
                    int2 span = m_RowSpans[y];
                    if (span.x > span.y) continue; // 空行
                    box.x = math.min(box.x, span.x);
                    box.z = math.max(box.z, span.y);
                    box.y = math.min(box.y, y);
                    box.w = math.max(box.w, y);
                }

                if (box.x > box.z)
                {
                    m_Bounds[0] = new int4(1, 1, 0, 0);
                    m_Bounds[1] = new int4(1, 1, 0, 0);
                    return;
                }

                // 风场是双线性插值，插值结果的风速不超过各格风速的最大值
                float maxWind = 0f;
                for (int i = 0; i < m_WindMap.Length; i++)
                {
                    maxWind = math.max(maxWind, math.length(m_WindMap[i].m_Wind));
                }

                float cellSize = (float)XCellMapSystemRe.kMapSize / kTextureSize;
                int advectMargin = (int)math.ceil(m_WindAdvectionSpeed * maxWind / cellSize) + 1;
                int4 advect = Expand(box, advectMargin);
                m_Bounds[0] = advect;
                m_Bounds[1] = Expand(advect, 1);
            }

            private static int4 Expand(int4 box, int margin)
            {
                return new int4(math.max(0, box.x - margin), math.max(0, box.y - margin),
                    math.min(kTextureSize - 1, box.z + margin), math.min(kTextureSize - 1, box.w + margin));
            }
        }

        /// <summary>趟1 平流，按行执行，只算平流范围 m_Bounds[0] 内的格；每格的算法与原版 <c>AirPollutionMoveJob</c> 的平流部分相同。</summary>
        [BurstCompile]
        public struct AirPollutionAdvectRowJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<TargetType> m_PollutionMap;

            [ReadOnly] public NativeArray<Wind> m_WindMap;

            public PollutionParameterData m_PollutionParameters;

            [ReadOnly] public NativeArray<int4> m_Bounds;

            /// <summary>每行只写本行的格；范围外保持分配时的 0</summary>
            [NativeDisableParallelForRestriction] public NativeArray<TargetType> m_TempMap;

            public void Execute(int y)
            {
                int4 box = m_Bounds[0];
                if (y < box.y || y > box.w) return;

                int rowStart = y * kTextureSize;
                for (int x = box.x; x <= box.z; x++)
                {
                    int index = rowStart + x;
                    float3 currentCellPos = XCellMapSystemRe.AirPollutionSystemGetCellCenter(index);
                    Wind windInfo = XCellMapSystemRe.WindSystemGetWind(currentCellPos, this.m_WindMap);
                    float3 sourcePos = currentCellPos - this.m_PollutionParameters.m_WindAdvectionSpeed * new float3(windInfo.m_Wind.x, 0f, windInfo.m_Wind.y);
                    short advectedValue = XCellMapSystemRe.AirPollutionSystemGetPollution(sourcePos, this.m_PollutionMap).m_Pollution;
                    m_TempMap[index] = new TargetType
                    {
                        m_Pollution = advectedValue
                    };
                }
            }
        }

        /// <summary>
        /// 趟2 扩散＋衰减，按行并行，只算扩散范围 m_Bounds[1] 内的格；每格的算法与原版 <c>AirPollutionMoveJob</c> 的扩散、衰减部分相同。
        /// 每行一条随机流（<c>RandomSeed.GetRandom(行号)</c>），行与行之间与顺序无关；随机舍入序列与原版不同，统计上等价。
        /// </summary>
        [BurstCompile]
        public struct AirPollutionDiffuseRowJob : IJobParallelFor
        {
            /// <summary>每行只写本行的格；范围外原本就是 0，算出来也是 0，不必写</summary>
            [NativeDisableParallelForRestriction] public NativeArray<TargetType> m_PollutionMap;

            [ReadOnly] public NativeArray<TargetType> m_TempMap;

            public PollutionParameterData m_PollutionParameters;

            [ReadOnly] public NativeArray<int4> m_Bounds;

            public RandomSeed m_Random;

            public void Execute(int y)
            {
                int4 box = m_Bounds[1];
                if (y < box.y || y > box.w) return;

                Unity.Mathematics.Random rng = this.m_Random.GetRandom(y);
                float baseDecayAmount = (float)this.m_PollutionParameters.m_AirFade / (float)kUpdatesPerDay;

                for (int x = box.x; x <= box.z; x++)
                {
                    int currentIndex = y * kTextureSize + x;
                    int accumulatedPollution = m_TempMap[currentIndex].m_Pollution;
                    accumulatedPollution += ((x > 0) ? (m_TempMap[currentIndex - 1].m_Pollution >> kSpreadShift) : 0);
                    accumulatedPollution += ((x < kTextureSize - 1) ? (m_TempMap[currentIndex + 1].m_Pollution >> kSpreadShift) : 0);
                    accumulatedPollution += ((y > 0) ? (m_TempMap[currentIndex - kTextureSize].m_Pollution >> kSpreadShift) : 0);
                    accumulatedPollution += ((y < kTextureSize - 1) ? (m_TempMap[currentIndex + kTextureSize].m_Pollution >> kSpreadShift) : 0);
                    int selfOutflow = m_TempMap[currentIndex].m_Pollution >> (kSpreadShift - 2);
                    int decayValue = MathUtils.RoundToIntRandom(ref rng, baseDecayAmount);
                    accumulatedPollution -= (selfOutflow + decayValue);
                    accumulatedPollution = math.clamp(accumulatedPollution, 0, 32767);
                    this.m_PollutionMap[currentIndex] = new TargetType
                    {
                        m_Pollution = (short)accumulatedPollution
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



