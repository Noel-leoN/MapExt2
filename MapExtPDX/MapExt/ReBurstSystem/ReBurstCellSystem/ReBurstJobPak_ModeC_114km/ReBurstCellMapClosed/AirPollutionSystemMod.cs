﻿// Game.Simulation.AirPollutionSystem : CellMapSystem<AirPollution>, IJobSerializable

// 需修改内容
// BC调用GetCellCenter/GetPollution/GetWind/kTextureSize
// GetCellCenter/GetPollution引用kMapSize/kTextureSize
// OnCreate() base.CreateTextures(AirPollutionSystem.kTextureSize);
// OnUpdate无引

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
// 5.序列化泛型ReSample方法的逻辑结构
// 其他直接套用

using System.Reflection;
using System.Runtime.CompilerServices;
using Colossal.Entities;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Prefabs;
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
    using BaseCellMapSystem = CellMapSystem<AirPollution>;
    // 2. Mod 自定义系统类(当前
    using ModSystem = AirPollutionSystemMod;
    // 3. 数据包泛(用于 GetData)
    using TargetCellMapData = CellMapData<AirPollution>;
    // 4. 原版系统类型 (用于禁用和定
    using TargetSystem = AirPollutionSystem;
    // 5. T struct
    using TargetType = AirPollution;
using MapExtPDX.MapExt.Core;
    // =========================================================================================

    /// <summary>
    /// 空气污染计算、扩散、衰减系
    /// 负责模拟空气中污染物的传播和消散过程
    /// 通过风力影响污染的移动，并考虑自然衰减和扩散
    /// </summary>
    public partial class AirPollutionSystemMod : BaseCellMapSystem, IJobSerializable
    {
        #region 字段/配置
        // 单例引用，便于Harmony补丁调用
        public static ModSystem Instance { get; private set; }

        // 纹理尺寸(vanilla=256)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.AirPollutionSystemkTextureSize; // mod尺寸
        public int2 TextureSize => new(kTextureSize, kTextureSize);

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
            base.OnCreate();
            Instance = this;

            // 1.禁用原版系统并获取原版系统引
            // 使用 GetExistingSystemManaged 避免意外创建未初始化的系
            var originalSystem = World.GetExistingSystemManaged<TargetSystem>();
            if (originalSystem != null)
            {
                originalSystem.Enabled = false;
                //#if DEBUG
                Mod.Info($"[{typeof(ModSystem).Name}] 禁用原系�? {typeof(TargetSystem).Name}");
                //#endif
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
            //#if DEBUG
// [ENCODING_FIX]             Mod.Info($"[{typeof(ModSystem).Name}] 创建自定义纹 {typeof(TargetSystem).Name} kTextureSize 原值{TargetSystem.kTextureSize} 变更目标值{this.m_TextureSize.x}");
            //#endif

            // 3. 获取其他依赖和查
            m_WindSystem = World.GetOrCreateSystemManaged<WindSystem>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_PollutionParameterQuery = GetEntityQuery(ComponentType.ReadOnly<PollutionParameterData>());
            // 强制依赖更新
            RequireForUpdate(m_PollutionParameterQuery);
        }

        protected override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            JobHandle dependencies;
            // 获取参数
            var pParams = m_PollutionParameterQuery.GetSingleton<PollutionParameterData>();
            // 获取风力数据 (通常是只
            var windMap = m_WindSystem.GetMap(true, out dependencies);

            // [核心方案] 使用 Allocator.TempJob 分配临时缓冲
            // TempJob 允许大内存分8MB+)，且Persistent 快
            // 必须Job 完成Dispose
            NativeArray<TargetType> scratchMap = new(m_Map.Length, Allocator.TempJob);

            AirPollutionMoveJob jobData = new()
            {
                m_PollutionMap = m_Map,
                m_TempMap = scratchMap,      // 临时读写 (Advection 结果)
                m_WindMap = windMap,
                m_PollutionParameters = pParams,
                m_Random = RandomSeed.Next(),
                m_Frame = m_SimulationSystem.frameIndex
            };

            // 合并依赖
            JobHandle combinedDeps = JobUtils.CombineDependencies(dependencies, m_WriteDependencies, m_ReadDependencies, Dependency);

            // 调度 Job
            Dependency = jobData.Schedule(combinedDeps);

            // [关键] 注册 TempJob 的自动释
            // 这告Unity：当 Dependency (即这Job) 完成后，自动调用 scratchMap.Dispose()
            // 无需手动管理生命周期，也不会阻塞主线程
            scratchMap.Dispose(Dependency);

            // 注册系统读写依赖
            m_WindSystem.AddReader(Dependency);
            AddWriter(Dependency);

            // 更新自身的句
            m_WriteDependencies = Dependency;
        }

        #endregion

        #region AirPollutionMoveJob
        [BurstCompile]
        public struct AirPollutionMoveJob : IJob
        {
            /// <summary>
            /// 读写：空气污染数据图
            /// 最终计算结果写回该字段
            /// </summary>
            public NativeArray<TargetType> m_PollutionMap;

            /// <summary>
            /// 只读：风力数据图
            /// 用于计算污染移动的方向
            /// </summary>
            [ReadOnly] public NativeArray<Wind> m_WindMap;

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

            public NativeArray<TargetType> m_TempMap;

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
                // Job直接引入或系统常ECS替换模式)
                // int kSpreadShift = 3;

                // 获取纹理（网格）的边长大
                // Job直接引入或系统常ECS替换模式)
                // int textureSize = XCellMapSystemRe.AirPollutionSystemkTextureSize;

                // 创建临时数组，用于存储第一步“平流”后的结果
                // 必须使用临时数组，因为不能在读取 m_PollutionMap 的同时写入它，否则会导致数据污染
                // NativeArray<AirPollution> tempAdvectedMap = new NativeArray<AirPollution>(this.m_PollutionMap.Length, Allocator.Temp);

                // 初始化随机数生成
                Unity.Mathematics.Random rng = this.m_Random.GetRandom((int)this.m_Frame);

                // --------------------------------------------------------
                // 2. 第一阶段：平(Advection) - 模拟风吹动污
                // --------------------------------------------------------
                for (int i = 0; i < this.m_PollutionMap.Length; i++)
                {
                    // 获取当前格子的世界坐标中心点
                    float3 currentCellPos = XCellMapSystemRe.AirPollutionSystemGetCellCenter(i);

                    // 获取该位置的风速和风向
                    Wind windInfo = XCellMapSystemRe.WindSystemGetWind(currentCellPos, this.m_WindMap);

                    // 计算“源头坐标”：
                    // 逆着风向回溯，找到上一帧污染是从哪里吹过来的
                    // 公式：当前位- (风速系* 风向)
                    float3 sourcePos = currentCellPos - this.m_PollutionParameters.m_WindAdvectionSpeed * new float3(windInfo.m_Wind.x, 0f, windInfo.m_Wind.y);

                    // 在原始污染图中采样“源头坐标”的污染
                    short advectedValue = XCellMapSystemRe.AirPollutionSystemGetPollution(sourcePos, this.m_PollutionMap).m_Pollution;

                    // 将移动后的污染值存入临时数
                    m_TempMap[i] = new TargetType
                    {
                        m_Pollution = advectedValue
                    };
                }

                // --------------------------------------------------------
                // 3. 第二阶段：扩(Diffusion) 衰减 (Decay)
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

        #region 序列化自适应
        // ==============================================================================
        // 序列化修(泛型
        // ==============================================================================
        // 重写 Serialize 以处理大数据 (使用 TempJob)
        public new JobHandle Serialize<TWriter>(EntityWriterData writerData, JobHandle inputDeps) where TWriter : struct, IWriter
        {
#if DEBUG
            Mod.Info($"[{typeof(ModSystem).Name}] Serialize called...");
#endif
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
#if DEBUG
// [ENCODING_FIX]             Mod.Info($"[{typeof(ModSystem).Name}] Serialize 已保存m_Map texturesize = {math.sqrt(m_Map.Length)} 目标值{kTextureSize} 原始值{TargetSystem.kTextureSize}");
#endif
            return jobHandle;
        }

        // 重写 Deserialize (无需迁移旧存档，Job会重新计
        public override JobHandle Deserialize<TReader>(EntityReaderData readerData, JobHandle inputDeps)
        {
#if DEBUG
            Mod.Info($"[{typeof(ModSystem).Name}] Deserialize called...");
#endif

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
#if DEBUG
// [ENCODING_FIX]             Mod.Info($"[{typeof(ModSystem).Name}] Deserialize 已读取m_Map texturesize = {math.sqrt(m_Map.Length)} 目标值{kTextureSize} 原始值{TargetSystem.kTextureSize}");
#endif
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
        internal struct DeserializeJobResetMismatch<TReader> : IJob where TReader : struct, IReader
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

            // =================================================================
            // [新增] 拦截原版反序列化，防止尺寸不匹配报错
            // =================================================================

            // 注意：Deserialize 是泛型方法，Harmony 必须使用特殊方式 Patch
            // 目标方法：CellMapSystem<GroundPollution>.Deserialize<TReader>(EntityReaderData, JobHandle)
            [HarmonyPatch]
            public static class DeserializePatch
            {
                // 动态获取目标方(泛型 Patch 需TargetMethod)
                static MethodBase TargetMethod()
                {
                    // 获取基类 CellMapSystem<T> Deserialize 方法
                    var type = typeof(BaseCellMapSystem);
                    // 注意：因为是泛型方法，Harmony 可能需要你指定具体的泛型实例，或Patch 所有实
                    // 但在这里，存档加载时通常具体化为 BinaryReader
                    // 简便方法：Patch 包含该方法的类，并使用名字匹配，Harmony 处理泛型
                    return AccessTools.Method(type, "Deserialize");
                }

                // 前缀拦截
                [HarmonyPrefix]
                public static bool Prefix(object __instance, object readerData, JobHandle inputDeps, ref JobHandle __result)
                {
                    // 1. 只拦截原版系统实(例如Game.Simulation.GroundPollutionSystem)
                    // Mod 自己的实例调Deserialize 不应被拦截（虽然 Mod 调用的是 override，也不受Patch 影响
                    if (__instance.GetType() == typeof(TargetSystem))
                    {
                        // 2. 强制转换参数类型
                        // 由于 Harmony 对于泛型方法的参数传递比较复杂，我们这里手动构Job
                        // 但由TReader Patch 签名中无法直接写出，我们需要利用泛型方法的技
                        // 或者，更简单的：直接在此处手动调度 Job，跳过原版逻辑

                        // 这里readerData 实际上是 EntityReaderData，但Harmony 参数中如果是 object 需要强
                        var rData = (EntityReaderData)readerData;

                        // 3. 调用我们写好Helper 方法来处理泛型调
                        // 我们需要知TReader 具体是什么。通常Colossal.Serialization.Entities.BinaryReader
                        // 但为了稳健，我们使用反射调用泛型 Helper

                        // 获取当前TReader 类型
                        // 这是一个黑科技：通过 StackFrame 或__originalMethod 获取 TReader 类型有点
                        // 但我们可以观察到，EntityReaderData 内部其实封装Reader

                        // *** 简化策***
                        // 既然此时很难直接构建泛型 Job (因为不知TReader)
                        // 我们最直接的方案是：直接调Mod 实例Deserialize 方法
                        // 因为 Mod 实例Deserialize 方法已经是我们重写过的“安全版”了

                        if (Instance != null)
                        {
                            // 调用 ModSystem.Instance.Deserialize<TReader>(rData, inputDeps)
                            // 利用 MakeGenericMethod 动态调
                            var method = typeof(ModSystem).GetMethod("Deserialize", BindingFlags.Instance | BindingFlags.Public);
                            var genericMethod = method.MakeGenericMethod(MethodBase.GetCurrentMethod().GetGenericArguments());

                            // 反射调用：这会返JobHandle
                            __result = (JobHandle)genericMethod.Invoke(Instance, new object[] { rData, inputDeps });

                            // 返回 false 阻止原版方法执行
                            return false;
                        }
                    }

                    return true;
                }
            }
        }

        #endregion

    } // mod class

} // mod namespace


