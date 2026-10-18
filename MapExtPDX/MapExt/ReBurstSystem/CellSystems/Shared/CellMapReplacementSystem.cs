// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System.Runtime.CompilerServices;
using Colossal.Serialization.Entities;
using Game.Simulation;
using MapExtPDX.MapExt.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.CellShared
{
    /// <summary>存档里的贴图尺寸与本模式不同时的读档策略。</summary>
    public enum SizeMismatchPolicy : byte
    {
        /// <summary>丢弃存档数据，贴图保持 SetDefaults 之后的默认值。</summary>
        Discard = 0,

        /// <summary>最近邻重采样到本模式的贴图边长（自然资源）。</summary>
        NearestResample = 1,
    }

    /// <summary>
    /// CellMap 替换系统的共用基类（重构三）：禁用原版、创建贴图、登记 <see cref="Instance"/>、
    /// 存档读写适配、GetData 修正，以及重定向前缀的判断与转发。
    ///
    /// <para><b>手写共用源，不经生成器</b>：改动直接作用于 ModeA–D 与 ModeE 的 D2。
    /// 各系统特有的内容（模拟 Job、OnUpdate、特有的生命周期）留在派生类，只改 ModeA。</para>
    ///
    /// <para><b>派生类不得用 new 隐藏 Serialize 或 GetData</b>：重定向持有的 <see cref="Instance"/> 是本类类型，
    /// 引擎经 <see cref="IJobSerializable"/> 调用的 Serialize 也由本类显式实现，派生类 new 出的版本不会被调用，
    /// 编译与运行都不报错。需要差异时在本类加 protected virtual 钩子，或给 <see cref="SizeMismatchPolicy"/> 加值。</para>
    ///
    /// <para><b>补丁方法不放在本类</b>：Harmony 2.2.2 按元数据令牌还原补丁方法，声明在泛型类里的会被还原成开放泛型。
    /// 派生类各留一个非泛型的 Patches 薄壳，特性与 WithPatches 登记不变，方法体转调 <see cref="Redirect"/>。</para>
    /// </summary>
    /// <typeparam name="TCell">格子结构，与原版系统相同。</typeparam>
    /// <typeparam name="TVanilla">被取代的原版系统。</typeparam>
    public abstract partial class CellMapReplacementSystem<TCell, TVanilla> : CellMapSystem<TCell>, IJobSerializable
        where TCell : struct, ISerializable
        where TVanilla : CellMapSystem<TCell>
    {
        // === Fields ===

        #region Fields

        /// <summary>
        /// 当前启用的替换系统实例，供重定向前缀使用。每个封闭类型一份；
        /// 一个会话只启用一个模式，各模式的同名系统共用这一份。
        /// </summary>
        public static CellMapReplacementSystem<TCell, TVanilla> Instance { get; private set; }

        /// <summary>日志 Tag：派生类返回自己的 Tag，日志前缀仍是派生类名。</summary>
        protected abstract string LogTag { get; }

        /// <summary>本模式的贴图边长（XCellMapSystemRe.&lt;X&gt;SystemkTextureSize）。</summary>
        protected abstract int ModTextureSize { get; }

        /// <summary>本模式的地图边长（XCellMapSystemRe.kMapSize），GetData 按它计算格距。</summary>
        protected abstract int MapSize { get; }

        /// <summary>尺寸不符时的读档策略，默认丢弃。</summary>
        protected virtual SizeMismatchPolicy MismatchPolicy => SizeMismatchPolicy.Discard;

        #endregion

        // === Lifecycle ===

        #region Lifecycle

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;

            // 1. 禁用原版系统并获取原版系统引用
            // 使用 GetExistingSystemManaged 避免意外创建未初始化的系统
            var originalSystem = World.GetExistingSystemManaged<TVanilla>();
            if (originalSystem != null)
            {
                originalSystem.Enabled = false;
                ModLog.Swap(LogTag, $"禁用原系统: {typeof(TVanilla).Name}");
            }
            else
            {
                // 仅在调试时提示，原版系统可能已被其他Mod移除或尚未加载
#if DEBUG
                ModLog.Error(LogTag, $"无法找到可禁用的原系统(尚未加载或可能被其他Mod移除): {typeof(TVanilla).Name}");
#endif
            }

            // 2. 创建自定义大小纹理
            CreateTextures(ModTextureSize);
        }

        protected override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        #endregion

        // === 序列化自适应 ===

        #region 序列化自适应

        // 原版 Serialize 是非虚方法：显式实现接口，引擎经 IJobSerializable 一定调到这里。
        // 下方 Serialize 的约束写错会编译失败，不会静默退回原版 CellMapSystem<T>.Serialize
        JobHandle IJobSerializable.Serialize<TWriter>(EntityWriterData writerData, JobHandle inputDeps) =>
            Serialize<TWriter>(writerData, inputDeps);

        // 重写 Serialize 以处理大数据 (使用 TempJob)
        public new JobHandle Serialize<TWriter>(EntityWriterData writerData, JobHandle inputDeps)
            where TWriter : struct, IWriter
        {
            // 获取 Stride (数据步长)
            int stride = 0;
            if ((object)default(TCell) is IStrideSerializable strideSerializable)
            {
                stride = strideSerializable.GetStride(writerData.GetWriter<TWriter>().context);
            }

            // 调度自定义的序列化 Job
            JobHandle jobHandle = new SerializeJobMod<TWriter>
            {
                m_Stride = stride,
                m_Map = m_Map,
                m_WriterData = writerData
            }.Schedule(JobHandle.CombineDependencies(inputDeps, m_WriteDependencies));

            m_ReadDependencies = JobHandle.CombineDependencies(m_ReadDependencies, jobHandle);
            return jobHandle;
        }

        // 重写 Deserialize：尺寸不符时按 MismatchPolicy 处理（无需迁移旧存档，Job 会重新计算）
        public override JobHandle Deserialize<TReader>(EntityReaderData readerData, JobHandle inputDeps)
        {
            int stride = 0;
            if ((object)default(TCell) is IStrideSerializable strideSerializable)
            {
                stride = strideSerializable.GetStride(readerData.GetReader<TReader>().context);
            }

            DeserializeJobResetMismatch<TReader> jobData = new()
            {
                m_Stride = stride,
                m_TextureSize = m_TextureSize.x,
                m_Policy = MismatchPolicy,
                m_Map = m_Map,
                m_ReaderData = readerData
            };
            m_WriteDependencies = jobData.Schedule(JobHandle.CombineDependencies(inputDeps, m_ReadDependencies, m_WriteDependencies));
            return m_WriteDependencies;
        }

        [BurstCompile]
        private struct SerializeJobMod<TWriter> : IJob where TWriter : struct, IWriter
        {
            [ReadOnly] public int m_Stride;
            [ReadOnly] public NativeArray<TCell> m_Map;
            public EntityWriterData m_WriterData;

            public void Execute()
            {
                TWriter writer = m_WriterData.GetWriter<TWriter>();
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
            public int m_TextureSize;            // 本模式的贴图边长，取自系统的 m_TextureSize.x
            public SizeMismatchPolicy m_Policy;  // Discard 丢弃；NearestResample 重采样（自然资源）
            public NativeArray<TCell> m_Map;
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
                            // 尺寸不匹配：读取到临时数组（必须读取以推进流位置）
                            var dummy = new NativeArray<TCell>(storedCount, Allocator.TempJob);
                            reader.Read(dummy);
                            if (m_Policy == SizeMismatchPolicy.NearestResample)
                            {
                                ResampleNearest(dummy, (int)math.sqrt(storedCount));
                            }

                            dummy.Dispose();
                            // Discard：m_Map 保持 0
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
                            else if (m_Policy == SizeMismatchPolicy.NearestResample)
                            {
                                // 尺寸不匹配：解压到临时数组并插值
                                var dummy = new NativeArray<TCell>(actualCount, Allocator.TempJob);
                                NativeReference<int> pos = new NativeReference<int>(0, Allocator.Temp);
                                m_ReaderData.GetReader<TReader>(compressedBuffer, pos).Read(dummy);
                                pos.Dispose();

                                ResampleNearest(dummy, (int)math.sqrt(actualCount));
                                dummy.Dispose();
                            }
                            // Discard：只读取字节流，不解压，m_Map 保持 0
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
                    if (m_Map.Length == m_TextureSize * m_TextureSize) // 运气好匹配了
                        reader.Read(m_Map);
                }
            }

            // 采用最临近插值法 (Nearest Neighbor Interpolation) 将旧数据映射到新尺寸
            private void ResampleNearest(NativeArray<TCell> source, int oldSize)
            {
                float scale = (float)oldSize / m_TextureSize;
                for (int y = 0; y < m_TextureSize; y++)
                {
                    for (int x = 0; x < m_TextureSize; x++)
                    {
                        int oldX = math.clamp((int)(x * scale), 0, oldSize - 1);
                        int oldY = math.clamp((int)(y * scale), 0, oldSize - 1);
                        m_Map[y * m_TextureSize + x] = source[oldY * oldSize + oldX];
                    }
                }
            }
        }

        #endregion

        // === GetData 修正 ===

        #region GetData修正

        // 重写/重定向的 GetData：格距按本模式的地图边长计算（原版固定按 14336）
        public new CellMapData<TCell> GetData(bool readOnly, out JobHandle dependencies)
        {
            // 获取依赖
            dependencies = (readOnly
                ? m_WriteDependencies
                : JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies));

            float2 mapSize = new float2(MapSize, MapSize);

            return new CellMapData<TCell>
            {
                m_Buffer = m_Map,
                m_CellSize = mapSize / (float2)m_TextureSize,
                m_TextureSize = m_TextureSize
            };
        }

        #endregion

        // === HarmonyPatch ===

        #region HarmonyPatch

        /// <summary>
        /// 重定向前缀的判断与转发：只拦截对应原版系统的实例，把调用转给 <see cref="Instance"/>。
        /// 返回 false 表示已接管、跳过原方法。补丁方法本身在派生类的非泛型 Patches 里（见类注释）。
        /// </summary>
        internal static class Redirect
        {
            // 辅助判断：只拦截对应的原版系统实例
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool ShouldRedirect(object instance)
            {
                return Instance != null && instance.GetType() == typeof(TVanilla);
            }

            public static bool GetMap(CellMapSystem<TCell> instance, ref NativeArray<TCell> result,
                bool readOnly, ref JobHandle dependencies)
            {
                if (ShouldRedirect(instance))
                {
                    result = Instance.GetMap(readOnly, out var deps);
                    dependencies = deps;
                    return false;
                }

                return true;
            }

            public static bool GetData(CellMapSystem<TCell> instance, ref CellMapData<TCell> result,
                bool readOnly, ref JobHandle dependencies)
            {
                if (ShouldRedirect(instance))
                {
                    result = Instance.GetData(readOnly, out var deps);
                    dependencies = deps;
                    return false;
                }

                return true;
            }

            public static bool AddReader(CellMapSystem<TCell> instance, JobHandle jobHandle)
            {
                if (ShouldRedirect(instance))
                {
                    Instance.AddReader(jobHandle);
                    return false;
                }

                return true;
            }

            public static bool AddWriter(CellMapSystem<TCell> instance, JobHandle jobHandle)
            {
                if (ShouldRedirect(instance))
                {
                    Instance.AddWriter(jobHandle);
                    return false;
                }

                return true;
            }
        }

        #endregion
    }
}
