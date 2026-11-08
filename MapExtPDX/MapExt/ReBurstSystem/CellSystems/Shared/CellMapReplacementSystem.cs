// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System.Runtime.CompilerServices;
using Colossal.Serialization.Entities;
using Game;
using Game.Simulation;
using MapExtPDX.MapExt.Core;
using MapExtPDX.MapExt.Tools;
using MapExtPDX.SaveLoadSystem;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.CellShared
{
    /// <summary>存档里的贴图尺寸与本模式不同时的读档策略。</summary>
    public enum SizeMismatchPolicy : byte
    {
        /// <summary>丢弃存档数据并把贴图清零，与基类 SetDefaults 相同。</summary>
        Discard = 0,

        /// <summary>最近邻重采样到本模式的贴图边长（自然资源、地下水）。</summary>
        NearestResample = 1,

        /// <summary>
        /// 28→57 存档扩展（v4.12.0 整合计划 3.4）：源贴图按原格距逐行写进中央，外围清零。
        /// 只由类型路由触发（<see cref="SaveExpansionTypeMapPatch"/>），不作为任何系统的默认策略。
        /// </summary>
        CenterEmbed = 2,
    }

    /// <summary>一次读档的中心嵌入结果，由读档 Job 写入。</summary>
    public enum ExpansionEmbedResult
    {
        /// <summary>没有中心嵌入：这次读档没有路由本系统，或存档里缺它的区段。</summary>
        None = 0,

        /// <summary>已按原格距写进中央，外围清零。</summary>
        Done = 1,

        /// <summary>源贴图边长不是预期值，或区段为空：贴图已清零。</summary>
        Rejected = 2,
    }

    /// <summary>
    /// 28→57 存档扩展的接收结果（v4.12.0 整合计划 3.4）：非泛型接口，转换系统按类型取各替换系统的结果做接收核对。
    /// </summary>
    public interface ISaveExpansionTarget
    {
        /// <summary>本次读档的中心嵌入结果。读取前先完成写入依赖；每次加载开始时复位为 <see cref="ExpansionEmbedResult.None"/>。</summary>
        ExpansionEmbedResult ExpansionEmbed { get; }
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
    public abstract partial class CellMapReplacementSystem<TCell, TVanilla> : CellMapSystem<TCell>, IJobSerializable,
        ISaveExpansionTarget
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

        /// <summary>m_StoredLength 的初值：本次读档没有经过 Deserialize（存档里没有本系统的区段，或是旧版格式）。</summary>
        private const int kNotRead = int.MinValue;

        /// <summary>
        /// 最近一次读档时存档里的贴图长度（压缩数据为负），由反序列化 Job 写入，<see cref="OnGameLoaded"/> 读出后复位。
        /// 用来在尺寸不符时记日志，读档后不必再猜存档是哪个版本存的。
        /// </summary>
        private NativeArray<int> m_StoredLength;

        /// <summary>
        /// 本次读档的中心嵌入结果（<see cref="ExpansionEmbedResult"/>），由读档 Job 写入。每次加载开始时在 <see cref="OnGamePreload"/> 复位；
        /// <see cref="OnGameLoaded"/> 不复位，转换系统的接收核对与各系统的 OnGameLoaded 谁先跑都读得到。
        /// </summary>
        private NativeArray<int> m_EmbedResult;

        /// <summary>本次读档中心嵌入预期的源贴图边长与写入偏移，调度读档 Job 时记下，供读档后记日志。</summary>
        private int m_EmbedSourceSize;

        private int m_EmbedOffset;

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

            m_StoredLength = new NativeArray<int>(1, Allocator.Persistent);
            m_StoredLength[0] = kNotRead;
            m_EmbedResult = new NativeArray<int>(1, Allocator.Persistent);
            m_EmbedResult[0] = (int)ExpansionEmbedResult.None;
        }

        protected override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (m_StoredLength.IsCreated || m_EmbedResult.IsCreated)
            {
                m_WriteDependencies.Complete();
                if (m_StoredLength.IsCreated) m_StoredLength.Dispose();
                if (m_EmbedResult.IsCreated) m_EmbedResult.Dispose();
            }
            base.OnDestroy();
        }

        /// <summary>每次加载开始时复位中心嵌入结果：存档里缺区段时引擎不调 Deserialize，不复位会留着上一次读档的结果。</summary>
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            if (!m_EmbedResult.IsCreated) return;

            m_WriteDependencies.Complete();
            m_EmbedResult[0] = (int)ExpansionEmbedResult.None;
        }

        /// <summary>
        /// 读档后，若存档里的贴图尺寸与本模式不符，记一行说明处理方式（重采样或清零）；28→57 中心嵌入另记一行，带校验和。
        /// 存档里没有本系统的区段时引擎改调 SetDefaults、不经 Deserialize，不记。
        /// </summary>
        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            if (!m_StoredLength.IsCreated) return;

            m_WriteDependencies.Complete();
            int stored = m_StoredLength[0];
            m_StoredLength[0] = kNotRead;

            // 结果码不在这里复位：转换系统的接收核对还要读
            var embed = m_EmbedResult.IsCreated ? (ExpansionEmbedResult)m_EmbedResult[0] : ExpansionEmbedResult.None;
            if (embed != ExpansionEmbedResult.None)
            {
                LogExpansionEmbed(embed, stored);
                return;
            }

            if (stored == kNotRead) return;

            int count = math.abs(stored);
            if (count == m_Map.Length) return;

            int side = (int)math.round(math.sqrt(count));
            string action = count == 0
                ? "存档里为空，已清零"
                : MismatchPolicy == SizeMismatchPolicy.NearestResample
                    ? "已最近邻重采样"
                    : "已丢弃并清零，由模拟重建";
            ModLog.Info(LogTag, $"存档里的贴图是 {side}²（{count} 格），本模式为 {ModTextureSize}²：{action}");
        }

        #endregion

        // === 序列化自适应 ===

        #region 序列化自适应

        // 原版 Serialize 是非虚方法：显式实现接口，引擎经 IJobSerializable 一定调到这里。
        // 下方 Serialize 的约束写错会编译失败，不会静默退回原版 CellMapSystem<T>.Serialize
        JobHandle IJobSerializable.Serialize<TWriter>(EntityWriterData writerData, JobHandle inputDeps) =>
            Serialize<TWriter>(writerData, inputDeps);

        // 存档：写法同原版 CellMapSystem.SerializeJob，缓冲按整图一次分配（见 SerializeJobMod）
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
            SerializeJobMod<TWriter> jobData = new()
            {
                m_Stride = stride,
                m_Map = m_Map,
                m_WriterData = writerData
            };
            JobHandle dependsOn = JobHandle.CombineDependencies(inputDeps, m_WriteDependencies);

            // 存读档计时开着时改为同步执行并计时（临时诊断，见 CellMapSerializeTimer）
            JobHandle jobHandle = CellMapSerializeTimer.Enabled
                ? CellMapSerializeTimer.RunSerialize(jobData, dependsOn, writerData, LogTag, m_TextureSize.x)
                : jobData.Schedule(dependsOn);

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

            // 28→57：本次读档把 ModeB 的区段路由给了本系统，按原格距嵌进中央。
            // 只看路由，不看请求阶段：类型在读任何数据之前就解析完了，地形否决改不了路由（v4.12.0 整合计划 Q18）
            bool embed = SaveExpansionTypeMapPatch.IsRoutedThisLoad(GetType());
            if (embed)
            {
                m_EmbedSourceSize = SaveExpansionTypeMapPatch.SourceTextureSize(ModTextureSize, MapSize);
                m_EmbedOffset = SaveExpansionGeometry.EmbedOffset(ModTextureSize, m_EmbedSourceSize);
            }

            DeserializeJobResetMismatch<TReader> jobData = new()
            {
                m_Stride = stride,
                m_TextureSize = m_TextureSize.x,
                m_Policy = embed ? SizeMismatchPolicy.CenterEmbed : MismatchPolicy,
                m_EmbedSourceSize = embed ? m_EmbedSourceSize : 0,
                m_EmbedOffset = embed ? m_EmbedOffset : 0,
                m_Map = m_Map,
                m_StoredLength = m_StoredLength,
                m_EmbedResult = m_EmbedResult,
                m_ReaderData = readerData
            };
            JobHandle dependsOn = JobHandle.CombineDependencies(inputDeps, m_ReadDependencies, m_WriteDependencies);

            // 存读档计时开着时改为同步执行并计时（临时诊断，见 CellMapSerializeTimer）
            m_WriteDependencies = CellMapSerializeTimer.Enabled
                ? CellMapSerializeTimer.RunDeserialize(jobData, dependsOn, readerData, LogTag, m_TextureSize.x)
                : jobData.Schedule(dependsOn);
            return m_WriteDependencies;
        }

        /// <summary>
        /// 存档 Job。泛型 Job 的封闭类型要到运行时才确定，Burst 不会自己编译它；
        /// 封闭类型由 <c>CellMapSerializeJobRegistration.cs</c> 的 <c>RegisterGenericJobType</c> 逐个登记（v4.12.0 整合计划 4.6），
        /// 所以是 internal：程序集级特性要能访问到它。新增 CellMap 替换系统时那里要补两行，否则静默退回 Mono，
        /// 唯一的信号是建置日志 Burst 段少了对应的 <c>Method:</c> 行。
        /// </summary>
        [BurstCompile]
        internal struct SerializeJobMod<TWriter> : IJob where TWriter : struct, IWriter
        {
            [ReadOnly] public int m_Stride;
            [ReadOnly] public NativeArray<TCell> m_Map;
            public EntityWriterData m_WriterData;

            public void Execute()
            {
                TWriter writer = m_WriterData.GetWriter<TWriter>();
                if (m_Stride != 0 && m_Map.Length != 0)
                {
                    // Job 内分配、Job 内用完，与原版一样用 Temp：超过块大小的请求改走默认堆，不会出错（v4.12.0 整合计划 §1.4）。
                    // 初始容量按整图一次分配到位（每格写出的字节数即 SizeOf<TCell>）。原版从 1000 字节起步、写满就翻倍，
                    // 扩展模式的图大 4–64 倍，要扩容十几次。不用 m_Stride 估算：原版 NaturalResourceCell.GetStride 漏算了鱼（12 对 16 字节）
                    NativeList<byte> buffer = new NativeList<byte>(m_Map.Length * UnsafeUtility.SizeOf<TCell>(), Allocator.Temp);
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

        /// <summary>
        /// 读档 Job。与 <see cref="SerializeJobMod{TWriter}"/> 一样靠 <c>RegisterGenericJobType</c> 登记封闭类型走 Burst，所以是 internal。
        /// 临时数组都在 Job 内分配、Job 内释放，用 Temp（v4.12.0 整合计划 4.6）。
        /// </summary>
        [BurstCompile]
        internal struct DeserializeJobResetMismatch<TReader> : IJob where TReader : struct, IReader
        {
            [ReadOnly] public int m_Stride;
            public int m_TextureSize;            // 本模式的贴图边长，取自系统的 m_TextureSize.x
            public SizeMismatchPolicy m_Policy;  // Discard 丢弃；NearestResample 重采样（自然资源、地下水）；CenterEmbed 28→57 中心嵌入
            public int m_EmbedSourceSize;        // CenterEmbed：预期的源贴图边长
            public int m_EmbedOffset;            // CenterEmbed：写入中央的起始下标（两轴相同）
            public NativeArray<TCell> m_Map;
            public NativeArray<int> m_StoredLength;  // [0] 存档里的贴图长度（压缩数据为负），供读档后记日志
            public NativeArray<int> m_EmbedResult;   // [0] CenterEmbed 的结果（ExpansionEmbedResult），供接收核对
            public EntityReaderData m_ReaderData;

            public void Execute()
            {
                m_StoredLength[0] = kNotRead;
                TReader reader = m_ReaderData.GetReader<TReader>();
                if (!(reader.context.version > Game.Version.stormWater)) return;

                // 存档里有本系统的区段时，引擎只调 Deserialize、不调 SetDefaults（EntityDeserializer 只给存档里缺席的系统调）。
                // 这时 m_Map 里是读档前的内容：开局是 OnCreate 的初值，同一会话里再读档就是上一个存档的数据。
                // 所以尺寸不符而丢弃时必须显式清零（ClearMap），不能指望它已经是 0

                if (reader.context.version > Game.Version.cellMapLengths)
                {
                    reader.Read(out int storedCount);
                    m_StoredLength[0] = storedCount;

                    // 1. 判断是否匹配
                    bool sizeMatches = (math.abs(storedCount) == m_Map.Length);

                    // 2. 如果是原始数据 (Length > 0)
                    if (storedCount > 0)
                    {
                        if (sizeMatches && m_Policy != SizeMismatchPolicy.CenterEmbed)
                        {
                            reader.Read(m_Map);
                        }
                        else
                        {
                            // 尺寸不匹配或中心嵌入：读取到临时数组（必须读取以推进流位置）。
                            // 下面的 Read 会整块写满，分配时不必清零（本 Job 的三个临时数组同此，原版每次都清零）
                            var dummy = new NativeArray<TCell>(storedCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                            reader.Read(dummy);
                            if (m_Policy == SizeMismatchPolicy.CenterEmbed)
                            {
                                EmbedCenter(dummy, storedCount);
                            }
                            else if (m_Policy == SizeMismatchPolicy.NearestResample)
                            {
                                ResampleNearest(dummy, (int)math.sqrt(storedCount));
                            }
                            else
                            {
                                ClearMap();
                            }

                            dummy.Dispose();
                        }
                    }
                    // 3. 如果是压缩数据 (Length < 0)
                    else if (storedCount < 0)
                    {
                        int actualCount = -storedCount;
                        reader.Read(out int byteLength);

                        // 必须读出来以清空流
                        NativeArray<byte> compressedBuffer = new NativeArray<byte>(byteLength, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                        try
                        {
                            reader.Read(compressedBuffer, m_Stride);

                            if (actualCount == m_Map.Length && m_Policy != SizeMismatchPolicy.CenterEmbed)
                            {
                                // 尺寸匹配：正常解压到 Map
                                NativeReference<int> pos = new NativeReference<int>(0, Allocator.Temp);
                                m_ReaderData.GetReader<TReader>(compressedBuffer, pos).Read(m_Map);
                                pos.Dispose();
                            }
                            else if (m_Policy == SizeMismatchPolicy.NearestResample || m_Policy == SizeMismatchPolicy.CenterEmbed)
                            {
                                // 尺寸不匹配或中心嵌入：解压到临时数组，再插值或写进中央
                                var dummy = new NativeArray<TCell>(actualCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                                NativeReference<int> pos = new NativeReference<int>(0, Allocator.Temp);
                                m_ReaderData.GetReader<TReader>(compressedBuffer, pos).Read(dummy);
                                pos.Dispose();

                                if (m_Policy == SizeMismatchPolicy.CenterEmbed)
                                    EmbedCenter(dummy, actualCount);
                                else
                                    ResampleNearest(dummy, (int)math.sqrt(actualCount));
                                dummy.Dispose();
                            }
                            else
                            {
                                // Discard：只读取字节流，不解压
                                ClearMap();
                            }
                        }
                        finally
                        {
                            compressedBuffer.Dispose();
                        }
                    }
                    // 4. 存档里的贴图长度为 0：同样当作尺寸不符，没有可重采样或嵌入的来源
                    else
                    {
                        ClearMap();
                        if (m_Policy == SizeMismatchPolicy.CenterEmbed)
                            m_EmbedResult[0] = (int)ExpansionEmbedResult.Rejected;
                    }
                }
                else
                {
                    // 旧版本数据，通常无法匹配尺寸，直接忽略或尝试读取
                    if (m_Map.Length == m_TextureSize * m_TextureSize) // 运气好匹配了
                        reader.Read(m_Map);
                }
            }

            // 丢弃时把贴图清零，与基类 CellMapSystem.SetDefaults 相同
            private void ClearMap()
            {
                for (int i = 0; i < m_Map.Length; i++)
                {
                    m_Map[i] = default;
                }
            }

            // 28→57 中心嵌入：先清零整张图（路由来的区段让引擎跳过了 SetDefaults，贴图里还是读档前的内容），
            // 再把源贴图逐行写进 [offset, offset + side)。格距不变，目标格 (x + offset) 与源格 x 的世界坐标相同。
            // 源边长不是预期值时只清零，记为被拒，由转换的接收核对报失败
            private void EmbedCenter(NativeArray<TCell> source, int count)
            {
                ClearMap();

                int side = (int)math.round(math.sqrt(count));
                if (side != m_EmbedSourceSize || side * side != count ||
                    m_EmbedOffset < 0 || m_EmbedOffset + side > m_TextureSize)
                {
                    m_EmbedResult[0] = (int)ExpansionEmbedResult.Rejected;
                    return;
                }

                for (int y = 0; y < side; y++)
                {
                    NativeArray<TCell>.Copy(source, y * side, m_Map, (y + m_EmbedOffset) * m_TextureSize + m_EmbedOffset, side);
                }

                m_EmbedResult[0] = (int)ExpansionEmbedResult.Done;
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

        // === 存档扩展（28→57）===

        #region 存档扩展

        /// <inheritdoc/>
        public ExpansionEmbedResult ExpansionEmbed
        {
            get
            {
                if (!m_EmbedResult.IsCreated) return ExpansionEmbedResult.None;
                m_WriteDependencies.Complete();
                return (ExpansionEmbedResult)m_EmbedResult[0];
            }
        }

        /// <summary>
        /// 读档后记中心嵌入的结果。中央校验和与 <see cref="CellMapChecksumExporter"/> 同法（xxHash3 取原始字节），
        /// 中央逐行拼起来就是源贴图的字节，可与源模式下同一存档的导出直接比；整图校验和与重启后读新档的导出比。
        /// </summary>
        private void LogExpansionEmbed(ExpansionEmbedResult embed, int stored)
        {
            int count = stored == kNotRead ? 0 : math.abs(stored);
            int side = (int)math.round(math.sqrt(count));
            if (embed != ExpansionEmbedResult.Done)
            {
                ModLog.Warn(LogTag, $"28→57 中心嵌入被拒：存档里的贴图是 {side}²（{count} 格），预期 {m_EmbedSourceSize}²，已清零");
                return;
            }

            int size = m_TextureSize.x;
            ModLog.Ok(LogTag,
                $"28→57 中心嵌入：存档 {side}² 写进本模式 {size}² 的 [{m_EmbedOffset}:{m_EmbedOffset + side})，" +
                $"中央 xxHash3 {HashCenter(m_EmbedOffset, side)}，外围非零格 {CountNonZeroOutside(m_EmbedOffset, side)}，" +
                $"整图 xxHash3 {FormatHash(m_Map)}");
        }

        /// <summary>把中央 side² 逐行拷出再算校验和，结果可与源贴图的整图校验和比。</summary>
        private string HashCenter(int offset, int side)
        {
            var center = new NativeArray<TCell>(side * side, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                int size = m_TextureSize.x;
                for (int y = 0; y < side; y++)
                {
                    NativeArray<TCell>.Copy(m_Map, (y + offset) * size + offset, center, y * side, side);
                }

                return FormatHash(center);
            }
            finally
            {
                center.Dispose();
            }
        }

        /// <summary>中央 side² 之外不全为零字节的格子数，中心嵌入后应为 0。</summary>
        private unsafe int CountNonZeroOutside(int offset, int side)
        {
            int size = m_TextureSize.x;
            int stride = UnsafeUtility.SizeOf<TCell>();
            byte* data = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(m_Map);
            int count = 0;
            for (int y = 0; y < size; y++)
            {
                bool rowInside = y >= offset && y < offset + side;
                for (int x = 0; x < size; x++)
                {
                    if (rowInside && x >= offset && x < offset + side) continue;

                    byte* cell = data + ((long)y * size + x) * stride;
                    for (int b = 0; b < stride; b++)
                    {
                        if (cell[b] != 0)
                        {
                            count++;
                            break;
                        }
                    }
                }
            }

            return count;
        }

        /// <summary>与 <see cref="CellMapChecksumExporter"/> 相同的写法：xxHash3 取原始字节，高位在前。</summary>
        private static unsafe string FormatHash(NativeArray<TCell> data)
        {
            uint2 hash = xxHash3.Hash64(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(data),
                (long)data.Length * UnsafeUtility.SizeOf<TCell>());
            return $"{hash.y:X8}{hash.x:X8}";
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
