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
        private SimulationSystem m_SimulationSystem;
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
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
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

            // [核心方案] 使用 Allocator.TempJob 分配临时缓冲区
            // TempJob 允许大内存分配(8MB+)，且比 Persistent 快
            // 必须在 Job 完成后 Dispose
            NativeArray<TargetType> scratchMap = new(m_Map.Length, Allocator.TempJob);

            // 合并依赖
            JobHandle combinedDeps = JobUtils.CombineDependencies(windDeps, m_WriteDependencies, m_ReadDependencies, Dependency);

            // === [MOD OPT] 趟1 平流(Advection)：每格只讀唯讀輸入、只寫自己的 m_TempMap[i]、
            // 無隨機數 → 執行順序不影響結果，改用 IJobParallelFor 平行化（位元級等價）。
            // batch = kTextureSize：每個 batch 一整行，1024 個 batch 足夠 work stealing。
            AirPollutionAdvectJob advectJob = new()
            {
                m_PollutionMap = m_Map,
                m_WindMap = windMap,
                m_PollutionParameters = pParams,
                m_TempMap = scratchMap
            };
            JobHandle advectHandle = advectJob.Schedule(m_Map.Length, kTextureSize, combinedDeps);

            // === 趟2 擴散(Diffusion)＋衰減(Decay)：逐格消耗同一個 Random 序列，
            // 平行化會改變數值，故保持單執行緒 IJob，隨機序列與原版逐位相同。
            AirPollutionDiffuseJob diffuseJob = new()
            {
                m_PollutionMap = m_Map,
                m_TempMap = scratchMap,
                m_PollutionParameters = pParams,
                m_Random = RandomSeed.Next(),
                m_Frame = m_SimulationSystem.frameIndex
            };
            Dependency = diffuseJob.Schedule(advectHandle);

            // [关键] TempJob 的自动释放必须挂在最后一个 Job 上
            // （挂在 advectHandle 上会在趟2 读取期间被释放）
            scratchMap.Dispose(Dependency);

            // windMap 只被趟1 读取，挂在 advectHandle 上可让 WindSystem 更早解除等待
            m_WindSystem.AddReader(advectHandle);
            AddWriter(Dependency);

            // 与原版 AirPollutionSystem 收尾一致
            // （旧实作漏了这一步，并多写一次 m_WriteDependencies —— AddWriter 内部即为覆盖赋值）
            Dependency = JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies, Dependency);
        }

        #endregion

        #region AirPollutionAdvectJob
        /// <summary>
        /// 趟1：平流(Advection) —— 模擬風把污染吹走。
        /// <para>
        /// 每格逆著風向回溯採樣「源頭座標」的污染值，寫入暫存圖。
        /// 每個 index 只讀唯讀輸入（污染圖、風場）、只寫自己的 m_TempMap[index]、不使用隨機數，
        /// 因此執行順序不影響結果 —— 與原本單執行緒版本位元級等價。
        /// </para>
        /// </summary>
        [BurstCompile]
        public struct AirPollutionAdvectJob : IJobParallelFor
        {
            /// <summary>只讀：污染圖。雙線性插值需要任意索引讀取，標 [ReadOnly] 即可解除 ParallelFor 的 index 限制</summary>
            [ReadOnly] public NativeArray<TargetType> m_PollutionMap;

            /// <summary>只讀：風力圖（64²，遠小於污染圖）</summary>
            [ReadOnly] public NativeArray<Wind> m_WindMap;

            /// <summary>污染相關全域參數（風力影響係數等）</summary>
            public PollutionParameterData m_PollutionParameters;

            /// <summary>只寫：平流結果。僅寫 [index]，符合 IJobParallelFor 的寫入限制</summary>
            [WriteOnly] public NativeArray<TargetType> m_TempMap;

            public void Execute(int index)
            {
                // 當前格子的世界座標中心點
                float3 currentCellPos = XCellMapSystemRe.AirPollutionSystemGetCellCenter(index);

                // 該位置的風速與風向
                Wind windInfo = XCellMapSystemRe.WindSystemGetWind(currentCellPos, this.m_WindMap);

                // 源頭座標：逆著風向回溯，找出污染是從哪裡吹過來的
                float3 sourcePos = currentCellPos - this.m_PollutionParameters.m_WindAdvectionSpeed * new float3(windInfo.m_Wind.x, 0f, windInfo.m_Wind.y);

                // 在原始污染圖中採樣源頭座標的污染值（雙線性插值）
                short advectedValue = XCellMapSystemRe.AirPollutionSystemGetPollution(sourcePos, this.m_PollutionMap).m_Pollution;

                m_TempMap[index] = new TargetType
                {
                    m_Pollution = advectedValue
                };
            }
        }
        #endregion

        #region AirPollutionDiffuseJob
        [BurstCompile]
        public struct AirPollutionDiffuseJob : IJob
        {
            /// <summary>
            /// 写入：空气污染数据图
            /// 最终计算结果写回该字段（本 Job 不读取它，只读 m_TempMap）
            /// </summary>
            public NativeArray<TargetType> m_PollutionMap;

            /// <summary>
            /// 污染相关的全局参数（如风力影响系数、衰减速度等）
            /// </summary>
            public PollutionParameterData m_PollutionParameters;

            /// <summary>
            /// 随机数生成器种子，用于产生不确定的衰减值
            /// </summary>
            public RandomSeed m_Random;

            /// <summary>
            /// 当前帧数，用于初始化随机状态
            /// </summary>
            public uint m_Frame;

            /// <summary>只讀：趟1(AirPollutionAdvectJob) 的平流結果</summary>
            [ReadOnly] public NativeArray<TargetType> m_TempMap;

            // ============================================================
            // 执行逻辑
            // ============================================================
            public void Execute()
            {
                // --------------------------------------------------------
                // 1. 准备工作
                // --------------------------------------------------------

                // 扩散系数位移量：3 表示除以 8 (2^3)
                // 用于快速计算邻居格子的污染有多少比例扩散到了当前格子

                // [MOD OPT] 平流(Advection)阶段已拆分为 AirPollutionAdvectJob (IJobParallelFor)，
                // 其结果存放于 m_TempMap；本 Job 只负责扩散与衰减，并保持单执行绪以维持
                // 与原版逐位相同的随机序列。

                // 初始化随机数生成
                Unity.Mathematics.Random rng = this.m_Random.GetRandom((int)this.m_Frame);

                // --------------------------------------------------------
                // 2. 扩散(Diffusion) 与衰减 (Decay)
                // --------------------------------------------------------

                // 计算每一帧的衰减基础值
                // AirFade 是总衰减量，除以每天更新次数得到单次更新的衰减量
                float baseDecayAmount = (float)this.m_PollutionParameters.m_AirFade / (float)kUpdatesPerDay;

                // 遍历整个网格（使用二维坐标逻辑以便处理邻居
                for (int y = 0; y < kTextureSize; y++)
                {
                    for (int x = 0; x < kTextureSize; x++)
                    {
                        // 计算当前一维数组索
                        int currentIndex = y * kTextureSize + x;

                        // 获取当前格子经过平流后的基础污染
                        int accumulatedPollution = m_TempMap[currentIndex].m_Pollution;

                        // --- 扩散逻辑 (从邻居吸取污 ---
                        // 位运算优化： >> kSpreadShift 等同除以 8
                        // 意为：每个邻居将1/8 的污染扩散给了当前格子

                        // 检查左邻居 (x > 0)
                        accumulatedPollution += ((x > 0) ? (m_TempMap[currentIndex - 1].m_Pollution >> kSpreadShift) : 0);

                        // 检查右邻居 (x < mapSize - 1)
                        accumulatedPollution += ((x < kTextureSize - 1) ? (m_TempMap[currentIndex + 1].m_Pollution >> kSpreadShift) : 0);

                        // 检查下邻居 (y > 0)
                        accumulatedPollution += ((y > 0) ? (m_TempMap[currentIndex - kTextureSize].m_Pollution >> kSpreadShift) : 0);

                        // 检查上邻居 (y < mapSize - 1)
                        accumulatedPollution += ((y < kTextureSize - 1) ? (m_TempMap[currentIndex + kTextureSize].m_Pollution >> kSpreadShift) : 0);

                        // --- 自我流失与衰---

                        // 1. 减去流失到邻居的部分
                        // (kSpreadShift - 2) = (3 - 2) = 1>> 1 等同于除2
                        // 逻辑：我们要接收来自 4 个邻居的 (1/8)，为了守恒，大约需要减去自己的 (4 * 1/8) = 1/2
                        // 所以这里减去了自身值的 1/2
                        int selfOutflow = m_TempMap[currentIndex].m_Pollution >> (kSpreadShift - 2);

                        // 2. 计算随机衰减值（模拟自然消散）：
                        int decayValue = MathUtils.RoundToIntRandom(ref rng, baseDecayAmount);

                        // 执行减法
                        accumulatedPollution -= (selfOutflow + decayValue);

                        // --- 结果写入 ---
                        // 限制范围short 的有效区[0, 32767] 防止溢出
                        accumulatedPollution = math.clamp(accumulatedPollution, 0, 32767);

                        // 将最终计算结果写回主污染
                        this.m_PollutionMap[currentIndex] = new TargetType
                        {
                            m_Pollution = (short)accumulatedPollution
                        };
                    }
                }

                // m_TempMap 由调用方分配（Allocator.TempJob），用于存储平流后的结果
                // 无需手动释放（由 Dispose(Dependency) 管理

            } // Execute
        } // BusrtJob Struct

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



