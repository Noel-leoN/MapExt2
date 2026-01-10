// Game.Simulation.NoisePollutionSystem : CellMapSystem<NoisePollution>, IJobSerializable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Colossal.Serialization.Entities;
using Game;
using Game.Simulation;
using HarmonyLib;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.ReBurstSystemModeC
{
    // =========================================================================================
    // [配置区域]
    // =========================================================================================
    // 1. 基类泛型
    using BaseCellMapSystem = CellMapSystem<NoisePollution>;
    // 2. Mod 自定义系统类型 (当前类)
    using ModSystem = NoisePollutionSystemMod;
    // 3. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<NoisePollution>;
    // 4. 原版系统类型 (用于禁用和定位)
    using TargetSystem = NoisePollutionSystem;
    // 5. T struct
    using TargetType = NoisePollution;
    // =========================================================================================

    public partial class NoisePollutionSystemMod : BaseCellMapSystem, IJobSerializable
    {
        /// <summary>
        /// 噪音污染系统
        /// 负责计算地图上噪声的扩散平滑处理。
        /// 噪音通常由建筑、交通等声源填入 m_PollutionTemp，本系统负责将其扩散到周围格点。
        /// </summary>
        #region 常量与配置
        // 单例引用，便于Harmony补丁调用
        public static ModSystem Instance { get; private set; }
        
        // 纹理尺寸(vanilla=256)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = CellMapSystemRe.NoisePollutionSystemkTextureSize; // mod尺寸
        public int2 TextureSize => new int2(kTextureSize, kTextureSize);

        // 系统更新周期：每日(月)128次
        public static readonly int kUpdatesPerDay = 128;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;
        #endregion

        #region Lifecycle
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

        }

        protected override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            // 1. 获取地图数据的原生数组（读写权限）
            NativeArray<TargetType> pollutionMap = GetMap(readOnly: false, out JobHandle inputDeps);

            // 2. 调度扩散计算 Job (Swap Job)
            // 这一步计算加权平均值，将 Temp 中的累积值转化为实际的 Pollution 值
            var swapJob = new NoisePollutionSwapJob
            {
                m_PollutionMap = pollutionMap
            };
            // 按照 Map 长度并行调度，依赖于 inputDeps
            JobHandle swapHandle = swapJob.Schedule(m_Map.Length, 64, inputDeps);

            // 3. 调度清理 Job (Clear Job)
            // 计算完成后，清空 Temp 值，为下一帧的累积做准备
            var clearJob = new NoisePollutionClearJob
            {
                m_PollutionMap = pollutionMap
            };
            JobHandle clearHandle = clearJob.Schedule(m_Map.Length, 64, swapHandle);

            // 4. 将最终的 JobHandle 注册回基类，确保后续读写安全
            AddWriter(clearHandle);
        }
        #endregion

        #region Jobs
        /// <summary>
        /// 扩散计算 Job：执行 3x3 高斯模糊加权
        /// </summary>
        private struct NoisePollutionSwapJob : IJobParallelFor
        {
            // 允许并行写入，因为写入的是 Struct 内部字段，且每个 Index 独立
            [NativeDisableParallelForRestriction] public NativeArray<TargetType> m_PollutionMap;

            public void Execute(int index)
            {
                TargetType cell = m_PollutionMap[index];

                // 计算二维坐标
                int x = index % kTextureSize;
                int y = index / kTextureSize;

                // 获取周围 8 个邻居的 Temp 值 (边界检查：越界则视为 0)
                // 使用三元运算符替代 if 以利于 Burst 向量化优化

                // 左右邻居
                short left = (short)((x > 0) ? m_PollutionMap[index - 1].m_PollutionTemp : 0);
                short right = (short)((x < kTextureSize - 1) ? m_PollutionMap[index + 1].m_PollutionTemp : 0);

                // 上下邻居
                short bottom = (short)((y > 0) ? m_PollutionMap[index - kTextureSize].m_PollutionTemp : 0);
                short top = (short)((y < kTextureSize - 1) ? m_PollutionMap[index + kTextureSize].m_PollutionTemp : 0);

                // 对角线邻居
                short bottomLeft = (short)((x > 0 && y > 0) ? m_PollutionMap[index - 1 - kTextureSize].m_PollutionTemp : 0);
                short bottomRight = (short)((x < kTextureSize - 1 && y > 0) ? m_PollutionMap[index + 1 - kTextureSize].m_PollutionTemp : 0);
                short topLeft = (short)((x > 0 && y < kTextureSize - 1) ? m_PollutionMap[index - 1 + kTextureSize].m_PollutionTemp : 0);
                short topRight = (short)((x < kTextureSize - 1 && y < kTextureSize - 1) ? m_PollutionMap[index + 1 + kTextureSize].m_PollutionTemp : 0);

                // 计算加权平均值
                // 权重分布：
                // 中心: 1/4 (0.25)
                // 十字: 1/8 (0.125) * 4 = 0.5
                // 对角: 1/16 (0.0625) * 4 = 0.25
                // 总权重 = 1.0
                int weightedSum = (cell.m_PollutionTemp / 4) +
                                  ((left + right + bottom + top) / 8) +
                                  ((bottomLeft + bottomRight + topLeft + topRight) / 16);

                cell.m_Pollution = (short)weightedSum;

                // 写回数组
                m_PollutionMap[index] = cell;
            }
        }

        /// <summary>
        /// 清理 Job：重置累积缓冲区
        /// </summary>
        [BurstCompile]
        private struct NoisePollutionClearJob : IJobParallelFor
        {
            public NativeArray<TargetType> m_PollutionMap;

            public void Execute(int index)
            {
                TargetType cell = m_PollutionMap[index];
                cell.m_PollutionTemp = 0; // 重置 Temp 值
                m_PollutionMap[index] = cell;
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