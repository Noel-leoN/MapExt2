// Game.Simulation.TerrainAttractivenessSystem : CellMapSystem<TerrainAttractiveness>, IJobSerializable

using System.Runtime.CompilerServices;
using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using Game.Simulation;
using HarmonyLib;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.ReBurstSystemModeB
{
    // =========================================================================================
    // [閰嶇疆鍖哄煙]
    // =========================================================================================
    // 1. 鍩虹被娉涘瀷
    using BaseCellMapSystem = CellMapSystem<TerrainAttractiveness>;
    // 2. Mod 鑷畾涔夌郴缁熺被鍨?(褰撳墠绫?
    using ModSystem = TerrainAttractivenessSystemMod;
    // 3. 鏁版嵁鍖呮硾鍨?(鐢ㄤ簬 GetData)
    using TargetCellMapData = CellMapData<TerrainAttractiveness>;
    // 4. 鍘熺増绯荤粺绫诲瀷 (鐢ㄤ簬绂佺敤鍜屽畾浣?
    using TargetSystem = TerrainAttractivenessSystem;
    // 5. T struct
    using TargetType = TerrainAttractiveness;
using MapExtPDX.MapExt.Core;
    // =========================================================================================

    /// <summary>
    /// 鍦板舰鍚稿紩鍔涜绠楃郴缁?(Terrain Attractiveness System)
    /// <para>
    /// 璇ョ郴缁熻礋璐ｆ牴鎹湴褰㈤珮搴︺€佹按鍩熸繁搴﹀拰妫灄鐜锛圸one Ambience锛夎绠楀叏鍥剧殑鍚稿紩鍔涙暟鍊笺€?
    /// 鍚稿紩鍔涘奖鍝嶅湴浠枫€佸競姘戞弧鎰忓害浠ュ強寤虹瓚鐢熸垚鐨勯€傚疁搴︺€?
    /// 绯荤粺缁ф壙鑷?CellMapSystem锛屼互缃戞牸褰㈠紡瀛樺偍鏁版嵁銆?
    /// </para>
    /// </summary>
    public partial class TerrainAttractivenessSystemMod : BaseCellMapSystem, IJobSerializable
    {
        #region 瀛楁/閰嶇疆
        // 鍗曚緥寮曠敤锛屼究浜嶩armony琛ヤ竵璋冪敤
        public static ModSystem Instance { get; private set; }

        // 绾圭悊灏哄(vanilla=256)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 鍘熺増灏哄
        public static readonly int kTextureSize = XCellMapSystemRe.TerrainAttractivenessSystemkTextureSize; // mod灏哄
        public int2 TextureSize => new int2(kTextureSize, kTextureSize);

        // 绯荤粺鏇存柊鍛ㄦ湡锛氭瘡鏃?鏈?16娆?
        public static readonly int kUpdatesPerDay = 16;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;

        /// <summary>
        /// 鐢ㄤ簬瀛樺偍涓棿璁＄畻缁撴灉锛堟按娣便€佸湴褰㈤珮搴︺€佹．鏋楃幆澧冨€硷級鐨勫師鐢熸暟缁勩€?
        /// </summary>
        private NativeArray<float3> m_AttractFactorData;
        #endregion

        #region 鏌ヨ鍜岀郴缁熷紩鐢?
        private TerrainSystem m_TerrainSystem;
        private WaterSystem m_WaterSystem;
        private ZoneAmbienceSystemMod m_ZoneAmbienceSystem;
        #endregion

        #region System Loop

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;

            // 1.绂佺敤鍘熺増绯荤粺骞惰幏鍙栧師鐗堢郴缁熷紩鐢?
            // 浣跨敤 GetExistingSystemManaged 閬垮厤鎰忓鍒涘缓鏈垵濮嬪寲鐨勭郴缁?
            var originalSystem = World.GetExistingSystemManaged<TargetSystem>();
            if (originalSystem != null)
            {
                originalSystem.Enabled = false;
                // #if DEBUG
                Mod.Info($"[{typeof(ModSystem).Name}] 绂佺敤鍘熺郴缁? {typeof(TargetSystem).Name}");
                // #endif
            }
            else
            {
                // 浠呭湪璋冭瘯鏃舵彁绀猴紝鍘熺増绯荤粺鍙兘宸茶鍏朵粬Mod绉婚櫎鎴栧皻鏈姞杞?
#if DEBUG
                Mod.Error($"[{typeof(ModSystem).Name}] 鏃犳硶鎵惧埌鍙鐢ㄧ殑鍘熺郴缁?灏氭湭鍔犺浇鎴栧彲鑳借鍏朵粬Mod绉婚櫎): {typeof(TargetSystem).Name}");
#endif
            }

            // 2. 鍒涘缓鑷畾涔夊ぇ灏忕汗鐞?
            CreateTextures(kTextureSize);
            // #if DEBUG
// [ENCODING_FIX]             Mod.Info($"[{typeof(ModSystem).Name}] 鍒涘缓鑷畾涔夌汗鐞? {typeof(TargetSystem).Name} kTextureSize 浠?鍘熷€納TargetSystem.kTextureSize} 鍙樻洿涓?鐩爣鍊納this.m_TextureSize.x}");
            // #endif

            // 3. 鑾峰彇鍏朵粬渚濊禆鍜屾煡璇?
            // 鑾峰彇渚濊禆鐨勬墭绠＄郴缁?(Managed Systems)
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_WaterSystem = World.GetOrCreateSystemManaged<WaterSystem>();
            m_ZoneAmbienceSystem = World.GetOrCreateSystemManaged<ZoneAmbienceSystemMod>();

            // 鍒濆鍖栨寔涔呭寲鏁版嵁缂撳瓨
            m_AttractFactorData = new NativeArray<float3>(m_Map.Length, Allocator.Persistent);

            // 娉ㄥ唽闇€瑕佹洿鏂扮殑缁勪欢鏁版嵁
            RequireForUpdate<AttractivenessParameterData>();
        }

        protected override void OnDestroy()
        {
            if (m_AttractFactorData.IsCreated)
            {
                m_AttractFactorData.Dispose();
            }
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            // 1. 鑾峰彇澶栭儴绯荤粺鏁版嵁鍙婂叾渚濊禆鍙ユ焺
            TerrainHeightData heightData = m_TerrainSystem.GetHeightData();
            WaterSurfaceData<SurfaceWater> waterData = m_WaterSystem.GetSurfaceData(out JobHandle waterDeps);
            CellMapData<ZoneAmbienceCell> ambienceData = m_ZoneAmbienceSystem.GetData(readOnly: true, out JobHandle ambienceDeps);

            // 鑾峰彇鍏ㄥ眬閰嶇疆鍙傛暟
            AttractivenessParameterData parameters = SystemAPI.GetSingleton<AttractivenessParameterData>();

            // 2. 璋冨害鍑嗗宸ヤ綔 Job锛氶噰闆嗗湴褰€佹按鍩熷拰鐜鏁版嵁
            // 蹇呴』绛夊緟涔嬪墠鐨勫啓鍏ュ畬鎴?(base.Dependency) 浠ュ強澶栭儴鏁版嵁鐨勪緷璧?
            TerrainAttractivenessPrepareJob prepareJob = new TerrainAttractivenessPrepareJob
            {
                m_TerrainData = heightData,
                m_WaterData = waterData,
                m_ZoneAmbienceData = ambienceData,
                m_AttractFactorData = m_AttractFactorData
            };

            JobHandle prepareHandle = prepareJob.ScheduleBatch(
                m_Map.Length,
                4,
                JobHandle.CombineDependencies(Dependency, waterDeps, ambienceDeps)
            );

            // 娉ㄥ唽璇诲彇鍣紝纭繚澶栭儴绯荤粺鐭ラ亾鎴戜滑鍦ㄨ鍙栨暟鎹?
            m_TerrainSystem.AddCPUHeightReader(prepareHandle);
            m_ZoneAmbienceSystem.AddReader(prepareHandle);
            m_WaterSystem.AddSurfaceReader(prepareHandle);

            // 3. 璋冨害涓昏绠?Job锛氭牴鎹噰闆嗙殑鏁版嵁璁＄畻鍚稿紩鍔?
            // 闇€瑕?base.m_ReadDependencies 鍜?base.m_WriteDependencies 鏉ュ鐞?Map 鐨勮鍐欓攣
            TerrainAttractivenessJob mainJob = new TerrainAttractivenessJob
            {
                m_AttractFactorData = m_AttractFactorData,
                m_Scale = heightData.scale.x * kTextureSize, // 璁＄畻缂╂斁姣斾緥
                m_AttractivenessMap = m_Map,
                m_AttractivenessParameters = parameters
            };

            // 涓?Job 渚濊禆浜?Prepare Job 浠ュ強 Map 鐨勮鍐欎緷璧?
            JobHandle mainHandle = mainJob.ScheduleBatch(
                m_Map.Length,
                4,
                JobHandle.CombineDependencies(m_WriteDependencies, m_ReadDependencies, prepareHandle)
            );

            // 4. 瀹屾垚渚濊禆閾捐缃?
            // 灏嗘 Job 娉ㄥ唽涓?Map 鐨勫啓鍏ヨ€?
            AddWriter(mainHandle);
            // 鏇存柊绯荤粺鐨勪富渚濊禆鍙ユ焺
            Dependency = JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies, mainHandle);
        }

        #endregion

        #region Jobs

        /// <summary>
        /// 鍑嗗闃舵 Job锛氫粠涓嶅悓绯荤粺涓噰鏍锋暟鎹苟鏁村悎鍒颁腑闂存暟缁勪腑銆?
        /// float3 鏍煎紡: x = 姘存繁, y = 鍦板舰楂樺害, z = 妫灄鐜鍊?
        /// </summary>
        [BurstCompile]
        private struct TerrainAttractivenessPrepareJob : IJobParallelForBatch
        {
            [ReadOnly] public TerrainHeightData m_TerrainData;
            [ReadOnly] public WaterSurfaceData<SurfaceWater> m_WaterData;
            [ReadOnly] public CellMapData<ZoneAmbienceCell> m_ZoneAmbienceData;

            [NativeDisableParallelForRestriction] // Batch Job 鍐欏叆闈為噸鍙犵储寮曪紝閫氬父瀹夊叏锛屼絾鏄剧ず澹版槑浠ラ槻涓囦竴
            public NativeArray<float3> m_AttractFactorData;

            public void Execute(int startIndex, int count)
            {
                for (int i = startIndex; i < startIndex + count; i++)
                {
                    float3 cellCenter = XCellMapSystemRe.GetCellCenter(i, kTextureSize);

                    float waterDepth = WaterUtils.SampleDepth(ref m_WaterData, cellCenter);
                    float terrainHeight = TerrainUtils.SampleHeight(ref m_TerrainData, cellCenter);
                    float forestAmbience = XCellMapSystemRe.ZoneAmbienceSystemGetZoneAmbience(GroupAmbienceType.Forest, cellCenter, m_ZoneAmbienceData.m_Buffer, 1f);

                    m_AttractFactorData[i] = new float3(waterDepth, terrainHeight, forestAmbience);
                }
            }
        }

        /// <summary>
        /// 涓昏绠?Job锛氳绠楁渶缁堢殑鍚稿紩鍔?Bonus銆?
        /// 鍖呮嫭妫灄鍔犳垚鍜屾捣宀哥嚎鍔犳垚锛岄€氳繃鍗风Н锛堝懆鍥村儚绱犳壂鎻忥級璁＄畻銆?
        /// </summary>
        [BurstCompile]
        private struct TerrainAttractivenessJob : IJobParallelForBatch
        {
            [ReadOnly] public NativeArray<float3> m_AttractFactorData;
            [ReadOnly] public float m_Scale;

            [NativeDisableParallelForRestriction]
            public NativeArray<TargetType> m_AttractivenessMap;

            public AttractivenessParameterData m_AttractivenessParameters;

            public void Execute(int startIndex, int count)
            {
                for (int i = startIndex; i < startIndex + count; i++)
                {
                    float3 currentCellCenter = XCellMapSystemRe.GetCellCenter(i, kTextureSize);
                    float2 calculatedBonus = float2.zero; // x: Forest, y: Shore

                    // 璁＄畻鎼滅储鍗婂緞 (鍩轰簬鍙傛暟璺濈鍜岀缉鏀?
                    int searchRadius = (int)math.ceil(math.max(m_AttractivenessParameters.m_ForestDistance, m_AttractivenessParameters.m_ShoreDistance) / m_Scale);

                    // 鍗风Н寰幆锛氭鏌ュ懆鍥寸殑鏍煎瓙
                    for (int yOffset = -searchRadius; yOffset <= searchRadius; yOffset++)
                    {
                        for (int xOffset = -searchRadius; xOffset <= searchRadius; xOffset++)
                        {
                            // 璁＄畻閭诲眳鍧愭爣骞禖lamp闃叉瓒婄晫
                            int neighborX = math.min(kTextureSize - 1, math.max(0, i % kTextureSize + xOffset));
                            int neighborY = math.min(kTextureSize - 1, math.max(0, i / kTextureSize + yOffset));
                            int neighborIndex = neighborX + neighborY * kTextureSize;

                            float3 neighborData = m_AttractFactorData[neighborIndex]; // x:Water, y:Height, z:Forest
                            float distance = math.distance(XCellMapSystemRe.GetCellCenter(neighborIndex, kTextureSize), currentCellCenter);

                            // 璁＄畻妫灄鍚稿紩鍔?(neighborData.z 鏄．鏋楃幆澧冨€?
                            // 璺濈瓒婅繎锛屾．鏋楀€艰秺楂橈紝鍚稿紩鍔涜秺澶?
                            float forestFalloff = math.saturate(1f - distance / m_AttractivenessParameters.m_ForestDistance);
                            calculatedBonus.x = math.max(calculatedBonus.x, forestFalloff * neighborData.z);

                            // 璁＄畻娴峰哺鍚稿紩鍔?(neighborData.x 鏄按娣?
                            // 濡傛灉姘存繁 > 2f锛岃涓烘湁鏁堟按婧愶紝璁＄畻璺濈琛板噺
                            float shoreFalloff = math.saturate(1f - distance / m_AttractivenessParameters.m_ShoreDistance);
                            float isWater = (neighborData.x > 2f) ? 1f : 0f;
                            calculatedBonus.y = math.max(calculatedBonus.y, shoreFalloff * isWater);
                        }
                    }

                    // 鍐欏叆缁撴灉
                    m_AttractivenessMap[i] = new TargetType
                    {
                        m_ForestBonus = calculatedBonus.x,
                        m_ShoreBonus = calculatedBonus.y
                    };
                }
            }
        }

        #endregion

        #region 搴忓垪鍖栬嚜閫傚簲
        // ==============================================================================
        // 搴忓垪鍖栦慨澶?(娉涘瀷鍖?
        // ==============================================================================
        // 閲嶅啓 Serialize 浠ュ鐞嗗ぇ鏁版嵁 (浣跨敤 TempJob)
        public new JobHandle Serialize<TWriter>(EntityWriterData writerData, JobHandle inputDeps) where TWriter : struct, IWriter
        {
            // 鑾峰彇 Stride (鏁版嵁姝ラ暱)
            int stride = 0;
            if ((object)default(TargetType) is IStrideSerializable strideSerializable)
            {
                stride = strideSerializable.GetStride(writerData.GetWriter<TWriter>().context);
            }

            // 璋冨害鑷畾涔夌殑搴忓垪鍖?Job
            JobHandle jobHandle = new SerializeJobMod<TWriter>
            {
                m_Stride = stride,
                m_Map = this.m_Map, // 浣跨敤鍩虹被鐨?Map
                m_WriterData = writerData
            }.Schedule(JobHandle.CombineDependencies(inputDeps, m_WriteDependencies));

            m_ReadDependencies = JobHandle.CombineDependencies(m_ReadDependencies, jobHandle);
            return jobHandle;
        }

        // 閲嶅啓 Deserialize (鏃犻渶杩佺Щ鏃у瓨妗ｏ紝Job浼氶噸鏂拌绠?
        public override JobHandle Deserialize<TReader>(EntityReaderData readerData, JobHandle inputDeps)
        {
            int stride = 0;
            if ((object)default(TargetType) is IStrideSerializable strideSerializable)
            {
                stride = strideSerializable.GetStride(readerData.GetReader<TReader>().context);
            }

            // 绠€鍖栫増 Job锛氬鏋滀笉鍖归厤鐩存帴涓㈠純
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
                    // 蹇呴』鐢?TempJob 闃叉 1024+ 灏哄瀵艰嚧鍐呭瓨婧㈠嚭
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

                // 榛樿涓?0 
                // m_Map 宸茬粡鍦?SetDefaults 涓娓呴浂浜嗭紝濡傛灉杩欓噷涓嶅啓鍏ワ紝灏辨槸閲嶇疆鐘舵€?

                if (reader.context.version > Game.Version.cellMapLengths)
                {
                    reader.Read(out int storedCount);

                    // 1. 鍒ゆ柇鏄惁鍖归厤
                    bool sizeMatches = (math.abs(storedCount) == m_Map.Length);

                    // 2. 濡傛灉鏄師濮嬫暟鎹?(Length > 0)
                    if (storedCount > 0)
                    {
                        if (sizeMatches)
                        {
                            reader.Read(m_Map);
                        }
                        else
                        {
                            // 灏哄涓嶅尮閰嶏細璇诲彇鍒颁复鏃舵暟缁勫苟涓㈠純 (蹇呴』璇诲彇浠ユ帹杩涙祦浣嶇疆)
                            var dummy = new NativeArray<TargetType>(storedCount, Allocator.TempJob);
                            reader.Read(dummy);
                            dummy.Dispose();
                            // m_Map 淇濇寔涓?0
                        }
                    }
                    // 3. 濡傛灉鏄帇缂╂暟鎹?(Length < 0)
                    else if (storedCount < 0)
                    {
                        int actualCount = -storedCount;
                        reader.Read(out int byteLength);

                        // 蹇呴』璇诲嚭鏉ヤ互娓呯┖娴?
                        NativeArray<byte> compressedBuffer = new NativeArray<byte>(byteLength, Allocator.TempJob);
                        try
                        {
                            reader.Read(compressedBuffer, m_Stride);

                            if (actualCount == m_Map.Length)
                            {
                                // 灏哄鍖归厤锛氭甯歌В鍘嬪埌 Map
                                NativeReference<int> pos = new NativeReference<int>(0, Allocator.Temp);
                                m_ReaderData.GetReader<TReader>(compressedBuffer, pos).Read(m_Map);
                                pos.Dispose();
                            }
                            // 鍚﹀垯锛氬彧璇诲彇瀛楄妭娴侊紝涓嶈В鍘嬶紝m_Map 淇濇寔涓?0
                        }
                        finally
                        {
                            compressedBuffer.Dispose();
                        }
                    }
                }
                else
                {
                    // 鏃х増鏈暟鎹紝閫氬父鏃犳硶鍖归厤灏哄锛岀洿鎺ュ拷鐣ユ垨灏濊瘯璇诲彇
                    if (m_Map.Length == kTextureSize * kTextureSize) // 杩愭皵濂藉尮閰嶄簡
                        reader.Read(m_Map);
                }
            }
        }

        #endregion

        #region GetData淇
        // 閲嶅啓/閲嶅畾鍚戠殑 GetData
        public new TargetCellMapData GetData(bool readOnly, out JobHandle dependencies)
        {
            // 鑾峰彇渚濊禆
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
        // Harmony 琛ヤ竵 (鍏ㄨ嚜鍔ㄩ€傞厤)
        // ==============================================================================
        [HarmonyPatch]
        public static class Patches
        {
            // 杈呭姪鍒ゆ柇锛氬彧鎷︽埅瀵瑰簲鐨勫師鐗堢郴缁熷疄渚?
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


