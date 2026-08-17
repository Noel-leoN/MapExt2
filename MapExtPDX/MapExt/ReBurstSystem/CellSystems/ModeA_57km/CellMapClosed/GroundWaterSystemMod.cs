// Game.Simulation.GroundWaterSystem : CellMapSystem<GroundWater>, IJobSerializable

using System.Runtime.CompilerServices;
using Colossal.Collections;
using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using Game.Simulation;
using HarmonyLib;
using Unity.Assertions;
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
    using BaseCellMapSystem = CellMapSystem<GroundWater>;
    // 2. Mod 自定义系统类(当前
    using ModSystem = GroundWaterSystemMod;
    // 3. 数据包泛(用于 GetData)
    using TargetCellMapData = CellMapData<GroundWater>;
    // 4. 原版系统类型 (用于禁用和定
    using TargetSystem = GroundWaterSystem;
    // 5. T struct
    using TargetType = GroundWater;
using MapExtPDX.MapExt.Core;
    // =========================================================================================

    /// <summary>
    /// 地下水系 地下水流动和污染扩散，并定期补充地下水资源
    /// </summary>
    public partial class GroundWaterSystemMod : BaseCellMapSystem, IJobSerializable, Game.Serialization.IPostDeserialize
    {
        public static ModSystem Instance { get; private set; }

        #region 常量和静态字配置
        // 纹理尺寸(vanilla=256)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.GroundWaterSystemkTextureSize; // mod尺寸
        public int2 TextureSize => new int2(kTextureSize, kTextureSize);

        // === 系統更新週期 ===
        // [MOD FIX] 原版 GroundWaterSystem.GetUpdateInterval 硬編碼 return 128，
        // 即每 128 模擬幀一次 = 每天 262144 / 128 = 2048 次。
        // 官方 WaterPipeParametersPrefab 的 tooltip 亦寫明 "per tick (2048 ticks per day)"。
        // 早期版本把 interval 值（128）誤當成每日次數，套用其他 CellMap 系統的
        // 262144 / kUpdatesPerDay 模板後得到 interval = 2048 → 每天僅 128 次，
        // 導致 m_GroundwaterPurification / m_GroundwaterReplenish 的施加頻率只有原版 1/16：
        // 地下水污染幾乎不消退（注入端 GroundWaterPollutionSystem 仍是每天 2048 次），
        // 抽乾的井恢復需約兩天遊戲時間。此處修正為原版語意。
        public static readonly int kUpdatesPerDay = 2048;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;
        public override int GetUpdateOffset(SystemUpdatePhase phase) => 64;

        // 最大阈
        public const int kMaxGroundWater = 10000;
        public const int kMinGroundWaterThreshold = 500;
        #endregion

        #region 查询和系统引
        private EntityQuery m_ParameterQuery;
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
                // #if DEBUG
                Mod.Info($"[{typeof(ModSystem).Name}] 禁用原系�? {typeof(TargetSystem).Name}");
                // #endif
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
            // #if DEBUG
// [ENCODING_FIX]             Mod.Info($"[{typeof(ModSystem).Name}] 创建自定义纹 {typeof(TargetSystem).Name} kTextureSize 原值{TargetSystem.kTextureSize} 变更目标值{this.m_TextureSize.x}");
            // #endif

            // 3. 获取其他依赖和查
            this.m_ParameterQuery = GetEntityQuery(ComponentType.ReadOnly<WaterPipeParameterData>());
        }

        protected override void OnUpdate()
        {
            // [核心方案] 使用 Allocator.TempJob 分配临时缓冲
            // TempJob 允许大内存分8MB+)，且Persistent 快
            // 必须Job 完成Dispose
            NativeArray<int2> scratchMap = new NativeArray<int2>(m_Map.Length, Allocator.TempJob);

            GroundWaterTickJob groundWaterTickJob = default(GroundWaterTickJob);
            groundWaterTickJob.m_GroundWaterMap = m_Map;
            groundWaterTickJob.m_Parameters = m_ParameterQuery.GetSingleton<WaterPipeParameterData>();
            // 增加临时缓冲区引
            groundWaterTickJob.m_TempMap = scratchMap;

            GroundWaterTickJob jobData = groundWaterTickJob;

            Dependency = IJobExtensions.Schedule(jobData, JobHandle.CombineDependencies(m_WriteDependencies, m_ReadDependencies, Dependency));

            AddWriter(Dependency);

            Dependency = JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies, Dependency);

            // [关键] 注册 TempJob 的自动释
            // 这告Unity：当 Dependency (即这Job) 完成后，自动调用 scratchMap.Dispose()
            // 无需手动管理生命周期，也不会阻塞主线程
            scratchMap.Dispose(Dependency);
        }

        /// <summary>
        /// 新遊戲的地下水初始場生成。
        ///
        /// <para><b>本覆寫是補回移植時遺漏的原版行為。</b>原版
        /// <c>GroundWaterSystem.SetDefaults</c>（<c>GroundWaterSystem.cs:264-283</c>）在
        /// <c>purpose == NewGame</c> 時以 Perlin 噪聲生成 <c>m_Amount = m_Max</c>。
        /// 而 <c>m_Max</c> 是全庫唯一「只有 SetDefaults 會寫、沒有任何重生成器」的靜態容量欄位
        /// （<c>GroundWaterPollutionSystem</c> 只寫 m_Polluted，<c>ConsumeGroundWater</c> 只減 m_Amount）。
        /// 早期移植沒帶上這個覆寫，於是擴展模式下整張圖恆為 0——趟3 的
        /// <c>m_Amount = min(..., m_Max)</c> 永遠算出 0，地下水泵抽不到水。</para>
        ///
        /// <para><b>刻意不帶原版的版本守衛</b>：原版條件是
        /// <c>context.version &lt; Version.timoSerializationFlow</c>，那是為了只在舊地圖檔上生成
        /// （老檔的序列化流裡沒有 GroundWater 區段，才會走到 SetDefaults；
        /// 見 <c>EntityDeserializer.cs:434-442</c> 的 m_SystemDefaults 收集條件）。
        /// 本 Mod 的貼圖尺寸與原版不同，任何地圖檔的資料都會被
        /// <see cref="DeserializeJobResetMismatch{TReader}"/> 丟棄，故每個新遊戲都必須自行生成。
        /// 與 <c>NaturalResourceSystemMod.SetDefaults</c> 的處理一致。</para>
        /// </summary>
        public override JobHandle SetDefaults(Context context)
        {
            JobHandle result = base.SetDefaults(context); // 先清零
            if (context.purpose == Purpose.NewGame)
            {
                result.Complete();
                GenerateProceduralGroundWater();
            }
            return result;
        }

        /// <summary>
        /// 反序列化後的兜底補生成。
        ///
        /// <para>地圖／存檔裡的 GroundWater 是原版 <c>orgTextureSize²</c> 格，與本模式的
        /// <c>kTextureSize²</c> 不匹配，<see cref="DeserializeJobResetMismatch{TReader}"/>
        /// 會讀完丟棄、<c>m_Map</c> 保持全 0。此時系統**存在**於序列化流中，
        /// <see cref="SetDefaults"/> 不會被呼叫（<c>EntityDeserializer.cs:627-631</c> 只對
        /// m_SystemDefaults 呼叫），故必須在此補。</para>
        ///
        /// <para>判據是「整張圖沒有任何容量」——<c>m_Max</c> 全為 0。該狀態在遊戲中不可能自然產生
        /// （m_Max 無其他寫入者），只可能是被丟棄或舊版 Mod 存下的空場，
        /// 因此補生成也順帶修復既有 v4.8.0 及更早的存檔。健康的場在第一個非零格就早退。</para>
        /// </summary>
        public void PostDeserialize(Context context)
        {
            m_WriteDependencies.Complete();

            for (int i = 0; i < m_Map.Length; i++)
            {
                if (m_Map[i].m_Max != 0) return; // 場是健康的，什麼都不做
            }

            ModLog.Warn(nameof(GroundWaterSystemMod),
                $"地下水容量場為空（{m_Map.Length} 格 m_Max 全為 0，多半是地圖檔的 " +
                $"{orgTextureSize}² 資料與本模式 {kTextureSize}² 不匹配而被丟棄），已重新生成");
            GenerateProceduralGroundWater();
        }

        /// <summary>
        /// 依原版公式在本模式解析度上生成地下水場。
        /// <para>座標先歸一化為 UV 再乘固定頻率 32（與原版 <c>GroundWaterSystem.cs:270-272</c> 相同），
        /// 故噪聲場形狀與原版一致，只是在放大的貼圖上取樣更密——含水層區域按地圖比例放大、占比不變。
        /// 與 <c>NaturalResourceSystemMod.GenerateProceduralResources</c> 的作法相同。</para>
        /// <para>沿用 <c>UnityEngine.Mathf</c> 而非 <c>Unity.Mathematics</c>：
        /// <c>PerlinNoise</c> 無對應實作，且 <c>RoundToInt</c> 的中點取整規則須與原版逐位一致。
        /// 全限定呼叫以免引入 <c>using UnityEngine</c> 與既有型別衝突。</para>
        /// </summary>
        private void GenerateProceduralGroundWater()
        {
            int nonZero = 0;
            for (int i = 0; i < m_Map.Length; i++)
            {
                float u = (float)(i % kTextureSize) / (float)kTextureSize;
                float v = (float)(i / kTextureSize) / (float)kTextureSize;

                short amount = (short)UnityEngine.Mathf.RoundToInt(10000f * math.saturate(
                    (UnityEngine.Mathf.PerlinNoise(32f * u, 32f * v) - 0.6f) / 0.4f));

                if (amount != 0) nonZero++;
                m_Map[i] = new TargetType { m_Amount = amount, m_Max = amount };
            }

            // 含水層占比是判斷本修補是否生效的唯一可觀測訊號（全 0 即代表沒生成）
            ModLog.Ok(nameof(GroundWaterSystemMod),
                $"地下水初始場已生成：{kTextureSize}² = {m_Map.Length} 格，" +
                $"含水層 {nonZero} 格（{100f * nonZero / m_Map.Length:F2}%）");
        }
        #endregion

        #region GroundWaterTickJob
        [BurstCompile]
        private struct GroundWaterTickJob : IJob
        {
            public NativeArray<TargetType> m_GroundWaterMap;
            public WaterPipeParameterData m_Parameters;

            public NativeArray<int2> m_TempMap;

            private void HandlePollution(int index, int otherIndex, NativeArray<int2> tmp)
            {
                TargetType groundWater = this.m_GroundWaterMap[index];
                TargetType groundWater2 = this.m_GroundWaterMap[otherIndex];

                // === [MOD OPT] 零格早退（位元級等價）===
                // 兩格的 m_Amount / m_Polluted / m_Max 全為 0 時：
                //   num = num2 = 0 → 內層三元運算取 0 → (0 - 0) / 4 = 0
                //   clamp 的上下界 -(0-0)/4 與 (0-0)/4 同時塌成 0 → num3 恆為 0
                // 故本次呼叫對 tmp 沒有任何增量，可安全跳過（與 tmp 當前值無關）。
                // 逐項 == 0 比較而非位元 OR：short 是有符號型別，OR 會觸發 CS0675 符號擴展錯誤；
                // 語意上任何非 0（含理論上不該出現的負值）都不早退，退回原邏輯，行為安全。
                if (groundWater.m_Amount == 0 && groundWater.m_Polluted == 0 && groundWater.m_Max == 0 &&
                    groundWater2.m_Amount == 0 && groundWater2.m_Polluted == 0 && groundWater2.m_Max == 0) return;

                ref int2 reference = ref tmp.ElementAt(index);
                ref int2 reference2 = ref tmp.ElementAt(otherIndex);
                int num = groundWater.m_Polluted + groundWater2.m_Polluted;
                int num2 = groundWater.m_Amount + groundWater2.m_Amount;
                int num3 = math.clamp((((num2 > 0) ? (groundWater.m_Amount * num / num2) : 0) - groundWater.m_Polluted) / 4, -(groundWater2.m_Amount - groundWater2.m_Polluted) / 4, (groundWater.m_Amount - groundWater.m_Polluted) / 4);
                reference.y += num3;
                reference2.y -= num3;
                Assert.IsTrue(0 <= groundWater.m_Polluted + reference.y);
                Assert.IsTrue(groundWater.m_Polluted + reference.y <= groundWater.m_Amount);
                Assert.IsTrue(0 <= groundWater2.m_Polluted + reference2.y);
                Assert.IsTrue(groundWater2.m_Polluted + reference2.y <= groundWater2.m_Amount);
            }

            private void HandleFlow(int index, int otherIndex, NativeArray<int2> tmp)
            {
                TargetType groundWater = this.m_GroundWaterMap[index];
                TargetType groundWater2 = this.m_GroundWaterMap[otherIndex];

                // === [MOD OPT] 零格早退（位元級等價）===
                // 兩格全為 0 時：num3 = 0 - 0 = 0；
                //   num4 = clamp((0 - 0 - 0) / 4, -0/4, 0/4) = 0 → num5 = 0
                // → reference / reference2 完全不變。
                // 關鍵：num4 的 clamp 上下界用的是 map 值（m_Amount）而非 amount + tmp.x，
                // 故該推導與 tmp 的當前值無關。
                if (groundWater.m_Amount == 0 && groundWater.m_Polluted == 0 && groundWater.m_Max == 0 &&
                    groundWater2.m_Amount == 0 && groundWater2.m_Polluted == 0 && groundWater2.m_Max == 0) return;

                ref int2 reference = ref tmp.ElementAt(index);
                ref int2 reference2 = ref tmp.ElementAt(otherIndex);
                Assert.IsTrue(groundWater2.m_Polluted + reference2.y <= groundWater2.m_Amount + reference2.x);
                Assert.IsTrue(groundWater.m_Polluted + reference.y <= groundWater.m_Amount + reference.x);
                float num = ((groundWater.m_Amount + reference.x != 0) ? (1f * (float)(groundWater.m_Polluted + reference.y) / (float)(groundWater.m_Amount + reference.x)) : 0f);
                float num2 = ((groundWater2.m_Amount + reference2.x != 0) ? (1f * (float)(groundWater2.m_Polluted + reference2.y) / (float)(groundWater2.m_Amount + reference2.x)) : 0f);
                int num3 = groundWater.m_Amount - groundWater.m_Max;
                int num4 = math.clamp((groundWater2.m_Amount - groundWater2.m_Max - num3) / 4, -groundWater.m_Amount / 4, groundWater2.m_Amount / 4);
                reference.x += num4;
                reference2.x -= num4;
                int num5 = 0;
                if (num4 > 0)
                {
                    num5 = (int)((float)num4 * num2);
                }
                else if (num4 < 0)
                {
                    num5 = (int)((float)num4 * num);
                }
                reference.y += num5;
                reference2.y -= num5;
                Assert.IsTrue(0 <= groundWater.m_Amount + reference.x);
                Assert.IsTrue(groundWater.m_Amount + reference.x <= groundWater.m_Max);
                Assert.IsTrue(0 <= groundWater2.m_Amount + reference2.x);
                Assert.IsTrue(groundWater2.m_Amount + reference2.x <= groundWater2.m_Max);
                Assert.IsTrue(0 <= groundWater.m_Polluted + reference.y);
                Assert.IsTrue(groundWater.m_Polluted + reference.y <= groundWater.m_Amount + reference.x);
                Assert.IsTrue(0 <= groundWater2.m_Polluted + reference2.y);
                Assert.IsTrue(groundWater2.m_Polluted + reference2.y <= groundWater2.m_Amount + reference2.x);
            }

            public void Execute()
            {
                // NativeArray<int2> tmp = new NativeArray<int2>(this.m_GroundWaterMap.Length, Allocator.TempJob); // 传入的临时缓冲区，全部替换为m_TempMap
                for (int i = 0; i < this.m_GroundWaterMap.Length; i++)
                {
                    int num = i % kTextureSize;
                    int num2 = i / kTextureSize;
                    if (num < kTextureSize - 1)
                    {
                        this.HandlePollution(i, i + 1, m_TempMap);
                    }
                    if (num2 < kTextureSize - 1)
                    {
                        this.HandlePollution(i, i + kTextureSize, m_TempMap);
                    }
                }
                for (int j = 0; j < this.m_GroundWaterMap.Length; j++)
                {
                    int num3 = j % kTextureSize;
                    int num4 = j / kTextureSize;
                    if (num3 < kTextureSize - 1)
                    {
                        this.HandleFlow(j, j + 1, m_TempMap);
                    }
                    if (num4 < kTextureSize - 1)
                    {
                        this.HandleFlow(j, j + kTextureSize, m_TempMap);
                    }
                }
                for (int k = 0; k < this.m_GroundWaterMap.Length; k++)
                {
                    TargetType value = this.m_GroundWaterMap[k];
                    value.m_Amount = (short)math.min(value.m_Amount + m_TempMap[k].x + math.ceil(this.m_Parameters.m_GroundwaterReplenish * (float)value.m_Max), value.m_Max); // 注意：Mathf改为Burst优化的math
                    value.m_Polluted = (short)math.clamp(value.m_Polluted + m_TempMap[k].y - this.m_Parameters.m_GroundwaterPurification, 0, value.m_Amount);
                    this.m_GroundWaterMap[k] = value;
                }
                //tmp.Dispose();
                // m_TempMap 由调用方分配Allocator.TempJob
                // 无需手动释放（由 Dispose(Dependency) 管理
            }
        }
        #endregion

        #region 序列化自适应
        // ==============================================================================
        // 序列化修(泛型
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

            // 调度自定义的序列Job
            JobHandle jobHandle = new SerializeJobMod<TWriter>
            {
                m_Stride = stride,
                m_Map = this.m_Map,
                m_WriterData = writerData
            }.Schedule(JobHandle.CombineDependencies(inputDeps, m_WriteDependencies));

            m_ReadDependencies = JobHandle.CombineDependencies(m_ReadDependencies, jobHandle);
            return jobHandle;
        }

        // 重写 Deserialize (无需迁移旧存档，Job会重新计
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
        private struct DeserializeJobResetMismatch<TReader> : IJob where TReader : struct, IReader
        {
            [ReadOnly] public int m_Stride;
            public NativeArray<TargetType> m_Map;
            public EntityReaderData m_ReaderData;

            public void Execute()
            {
                TReader reader = m_ReaderData.GetReader<TReader>();
                if (!(reader.context.version > Version.stormWater)) return;

                // 默认0
                // m_Map 已经SetDefaults 中被清零了，如果这里不写入，就是重置状

                if (reader.context.version > Version.cellMapLengths)
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
        }
        #endregion

    } // mod class

} // mod namespace



