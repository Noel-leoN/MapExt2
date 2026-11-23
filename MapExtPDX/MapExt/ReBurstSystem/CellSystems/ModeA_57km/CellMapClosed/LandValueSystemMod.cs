// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit MapExt2 and the author.

// Game.Simulation.LandValueSystem
// 重写Job逻辑需更改引入字段，ECS替换+HarmonyPatch公开字段/方法

using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using HarmonyLib;
using MapExtPDX.CellShared;
using MapExtPDX.MapExt.Core;
using Unity.Burst;
using Unity.Burst.Intrinsics;
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
    using BaseCellMapSystem = CellMapSystem<LandValueCell>;
    // 2. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<LandValueCell>;
    // 3. 原版系统类型 (用于禁用和定位)
    using TargetSystem = LandValueSystem;
    // 4. T struct
    using TargetType = LandValueCell;

    // =========================================================================================

    // ===================== LandValueSystem 重构逻辑 =====================
    // 1. 原版LandValueSystem产生2个地价：Edge地价(组件)和CellMap热力图。前者作用于道路边区块建筑经济模拟计算，后者仅用于UI显示不参与任何经济模拟
    // 2. Edge地价计算医疗/教育/警务/商业/公交/电车地铁等服务覆盖加成。CellMap热力图加权采样Edge地价生成，叠加景观吸引力/电信覆盖/地形吸引力，扣除空气/土壤/噪音污染。经济模租金计算)使用的Edge地价与UI显示的热力图地价不一致
    // 3. 重构目标：Edge地价直接计算最终值，包含所有加成和扣除因素。CellMap热力图加权采样Edge地价生成，确保UI显示与经济模拟一致
    // 注意：早期版本根据道路边建筑属性计算地价方式对性能影响大，参照中日韩等地基准地价体地价公告现路线价，不再区分建筑用途对地价的影响。建筑地价影响由RentSystem体现

    // 降级保护：合并版存档里本类的类型名是 MapExtPDX.CellMap.LandValueSystemMod，退回本版本时靠此别名读回，勿删
    [Colossal.Serialization.Entities.FormerlySerializedAs("MapExtPDX.CellMap.LandValueSystemMod")]
    public partial class LandValueSystemMod : CellMapReplacementSystem<TargetType, TargetSystem>
    {
        private const string Tag = nameof(LandValueSystemMod);

        #region LandValue特定逻辑配置
        // --- 蔓延与辐射（热力图生成） ---
        // 地价从道路向周围辐射的物理距离（米）
        // 原版逻辑依赖网格大小（14336/128=112m），这里改为固定物理距离：1. 减少断层；2. 可配置辐射范围
        // 建议 150m - 300m (一个街区约 80-120m)
        public const float kSpreadDistance = 150.0f;

        // 数值平滑系数 (Lerp)。越小变化越慢（越平滑），越大变化越快（越灵敏）
        public const float kLerpSpeed = 0.6f;

        #endregion

        #region 字段/配置
        // 纹理尺寸(vanilla=128)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.LandValueSystemkTextureSize; // mod尺寸

        // 基类参数：日志 Tag、本模式的贴图边长与地图边长（XCellMapSystemRe 按模式命名空间解析）
        protected override string LogTag => Tag;
        protected override int ModTextureSize => kTextureSize;
        protected override int MapSize => XCellMapSystemRe.kMapSize;

        // 系统更新周期：每日更新次数 32 (vanilla)
        public static readonly int kUpdatesPerDay = 32;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;

        // 读档冷启动标志：OnGameLoadingComplete 置位后手动 Update 一次，OnUpdate 取用时清除
        private bool m_ColdStart;
        #endregion

        #region 查询和系统引用
        // 查询
        private EntityQuery m_EdgeGroup;
        // private EntityQuery m_NodeGroup; // vanilla 未使用
        private EntityQuery m_AttractivenessParameterQuery;
        private EntityQuery m_LandValueParameterQuery;
        // 依赖的子系统引用
        private GroundPollutionSystem m_GroundPollutionSystem;
        private AirPollutionSystem m_AirPollutionSystem;
        private NoisePollutionSystem m_NoisePollutionSystem;
        private AvailabilityInfoToGridSystem m_AvailabilityInfoToGridSystem;
        private SearchSystem m_NetSearchSystem;
        private TerrainAttractivenessSystem m_TerrainAttractivenessSystem;
        private TerrainSystem m_TerrainSystem;
        private WaterSystem m_WaterSystem;
        private TelecomCoverageSystem m_TelecomCoverageSystem;
        #endregion

        #region System Loop
        protected override void OnCreate()
        {
            // 1–2. 禁用原版系统、登记 Instance、创建自定义大小纹理由基类完成
            base.OnCreate();

            // 3. 获取其他依赖和查询
            m_NetSearchSystem = World.GetOrCreateSystemManaged<SearchSystem>();
            m_GroundPollutionSystem = World.GetOrCreateSystemManaged<GroundPollutionSystem>();
            m_AirPollutionSystem = World.GetOrCreateSystemManaged<AirPollutionSystem>();
            m_NoisePollutionSystem = World.GetOrCreateSystemManaged<NoisePollutionSystem>();
            m_TerrainAttractivenessSystem = World.GetOrCreateSystemManaged<TerrainAttractivenessSystem>();
            m_AvailabilityInfoToGridSystem = World.GetOrCreateSystemManaged<AvailabilityInfoToGridSystem>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_WaterSystem = World.GetOrCreateSystemManaged<WaterSystem>();
            m_TelecomCoverageSystem = World.GetOrCreateSystemManaged<TelecomCoverageSystem>();

            // 构建参数查询
            m_AttractivenessParameterQuery = SystemAPI.QueryBuilder()
                .WithAll<AttractivenessParameterData>()
                .Build();

            m_LandValueParameterQuery = SystemAPI.QueryBuilder()
                .WithAll<LandValueParameterData>()
                .Build();

            // 查询所有具有地价的道路边缘
            m_EdgeGroup = SystemAPI.QueryBuilder()
                .WithAll<Edge, LandValue, Curve>()
                .WithNone<Deleted, Temp>()
                .Build();

            // 确保有需要更新的道路边缘时才运行System
            RequireAnyForUpdate(m_EdgeGroup);
        }

        protected override void OnUpdate()
        {
            // 获取参数
            if (!m_LandValueParameterQuery.HasSingleton<LandValueParameterData>()) return;
            LandValueParameterData lvParams = m_LandValueParameterQuery.GetSingleton<LandValueParameterData>();
            AttractivenessParameterData attrParams = m_AttractivenessParameterQuery.GetSingleton<AttractivenessParameterData>();

            // 读档冷启动（见 OnGameLoadingComplete）：取用即清除，这一次只画热力图
            bool coldStart = m_ColdStart;
            m_ColdStart = false;

            // --- 资源准备 ---
            // 获取所有依赖数据的句柄
            NativeArray<TerrainAttractiveness> attrMap = m_TerrainAttractivenessSystem.GetMap(true, out JobHandle attrDep);
            NativeArray<GroundPollution> gPolMap = m_GroundPollutionSystem.GetMap(true, out JobHandle gPolDep);
            NativeArray<AirPollution> aPolMap = m_AirPollutionSystem.GetMap(true, out JobHandle aPolDep);
            NativeArray<NoisePollution> nPolMap = m_NoisePollutionSystem.GetMap(true, out JobHandle nPolDep);
            NativeArray<AvailabilityInfoCell> availMap = m_AvailabilityInfoToGridSystem.GetMap(true, out JobHandle availDep);
            CellMapData<TelecomCoverage> telecomMap = m_TelecomCoverageSystem.GetData(true, out JobHandle teleDep);
            WaterSurfaceData<SurfaceWater> waterData = m_WaterSystem.GetSurfaceData(out JobHandle waterDep);

            // 合并环境读取依赖
            JobHandle envDeps = JobUtils.CombineDependencies(attrDep, gPolDep, aPolDep, nPolDep, availDep, teleDep, waterDep);

            // --- Phase 1: Edge Calculation (逻辑核心) ---
            // 这一步直接计算 Edge 的最终地价，确保模拟数据的绝对精度，不再依赖 Map 分辨率
            var edgeJob = new EdgeLandValueJob
            {
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_CurveType = SystemAPI.GetComponentTypeHandle<Curve>(true),

                // Write 权限：直接写入地价
                m_LandValues = SystemAPI.GetComponentTypeHandle<LandValue>(false),

                // 服务数据
                m_ServiceCoverageType = SystemAPI.GetBufferTypeHandle<Game.Net.ServiceCoverage>(true),
                m_AvailabilityType = SystemAPI.GetBufferTypeHandle<ResourceAvailability>(true),

                // 环境数据采样器复用
                m_Sampler =
                {
                    // 环境 Maps
                    m_AttractiveMap = attrMap,
                    m_GroundPollutionMap = gPolMap,
                    m_AirPollutionMap = aPolMap,
                    m_NoisePollutionMap = nPolMap,
                    m_AvailabilityInfoMap = availMap,
                    m_TelecomCoverageMap = telecomMap,
                    m_WaterSurfaceData = waterData,
                    m_TerrainHeightData = m_TerrainSystem.GetHeightData(),

                    // 参数
                    m_LvParams = lvParams,
                    m_AttrParams = attrParams,

                    // 辅助尺寸 (用于采样其他系统的Map)
                    m_MapSize = XCellMapSystemRe.kMapSize,
                    m_AttractionMapSize = XCellMapSystemRe.LandValueSystemkTextureSize,
                },

                // 传入配置（可改为 ModSettings 输入）
                m_LerpSpeed = kLerpSpeed,
                // 环境衰减系数 (从 ModSettings 百分比转小数)
                m_EnvDampeningFactor = (Mod.Instance?.Settings?.LandValueEnvironmentEffect ?? 40) / 100f,
                // 服务加成上限乘数 (从 ModSettings 百分比转小数)
                m_ServiceBonusCapMultiplier = (Mod.Instance?.Settings?.ServiceBonusCapMultiplier ?? 100) / 100f,
            };

            // 调度 Edge Job
            // 需要等待环境数据准备好，并且等待上一帧的系统写入完成
            Dependency = JobHandle.CombineDependencies(Dependency, envDeps, m_WriteDependencies);
            // 冷启动不推进 Edge 地价：它随存档读回，是租金与找房的现值，多走一步 0.6 插值就改变了经济
            if (!coldStart)
            {
                Dependency = edgeJob.ScheduleParallel(m_EdgeGroup, Dependency);
            }

            // --- Phase 2: Map Rasterization (UI 可视化) ---
            // 这一步将计算好的 Edge 地价“画”到 Map 上。完全为UI 服务

            NativeQuadTree<Entity, QuadTreeBoundsXZ> netSearchTree = m_NetSearchSystem.GetNetSearchTree(true, out JobHandle searchDep);

            // 全量更新，不分片
            var gridJob = new LandValueToGridJob
            {
                m_LandValueMap = m_Map, // Write Map
                m_NetSearchTree = netSearchTree,

                // Read Component（读取 Phase 1 刚算好的地价）
                m_LandValueData = SystemAPI.GetComponentLookup<LandValue>(true),
                // m_EdgeGeometryData = SystemAPI.GetComponentLookup<EdgeGeometry>(true), // 改为Curve优化性能
                // 引入 Curve / LV 参数
                m_CurveData = SystemAPI.GetComponentLookup<Curve>(true),

                // 环境数据采样器复用
                m_Sampler =
                {
                    // 环境 Maps（用于无道路区域映射）
                    m_AttractiveMap = attrMap,
                    m_GroundPollutionMap = gPolMap,
                    m_AirPollutionMap = aPolMap,
                    m_NoisePollutionMap = nPolMap,
                    m_AvailabilityInfoMap = availMap,
                    m_TelecomCoverageMap = telecomMap,
                    m_WaterSurfaceData = waterData,
                    m_TerrainHeightData = m_TerrainSystem.GetHeightData(),

                    // 参数
                    m_LvParams = lvParams,
                    m_AttrParams = attrParams,

                    // 辅助尺寸 (用于采样其他系统的Map)
                    m_MapSize = XCellMapSystemRe.kMapSize,
                    m_AttractionMapSize = XCellMapSystemRe.LandValueSystemkTextureSize,
                },

                // m_CellSize = XCellMapSystemRe.kMapSize / kTextureSize, // 计算格大小已移除,改为物理距离
                m_TextureSize = kTextureSize,

                // 传入配置（可改为 ModSettings 输入）
                m_SpreadDistance = kSpreadDistance,

                // 读档冷启动：直接写目标值
                m_DirectWrite = coldStart
            };

            // 必须等待 Phase 1 完成，因为 Phase 2 要读 Phase 1 的结果。
            // 写图前还要等这张图的读者（热力图、悬停提示、调试视图与存档），原版同样读写依赖都等
            JobHandle gridDeps = JobHandle.CombineDependencies(Dependency, searchDep, m_ReadDependencies);

            // 调度 Map Job
            Dependency = gridJob.Schedule(kTextureSize * kTextureSize, 64, gridDeps);

            // --- 资源管理 ---
            AddWriter(Dependency);

            // 注册所有读取依赖
            m_NetSearchSystem.AddNetSearchTreeReader(Dependency);
            m_WaterSystem.AddSurfaceReader(Dependency);
            m_TerrainAttractivenessSystem.AddReader(Dependency);
            m_GroundPollutionSystem.AddReader(Dependency);
            m_AirPollutionSystem.AddReader(Dependency);
            m_NoisePollutionSystem.AddReader(Dependency);
            m_AvailabilityInfoToGridSystem.AddReader(Dependency);
            m_TelecomCoverageSystem.AddReader(Dependency);
            m_TerrainSystem.AddCPUHeightReader(Dependency);

            // 合并依赖
            Dependency = JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies, Dependency);

        }

        /// <summary>
        /// 读档冷启动：存档里没有本系统的图时，加载完成后立即整图栅格化一次，热力图不必等首次常规更新。
        /// 原版的图随存档读回，没有这个问题；本系统的图读不回来时（存档缺本系统区段、尺寸不符被丢弃），
        /// 首图最多要等游戏内 45 分钟，且按 0.4 插值只有四成强度。
        ///
        /// <para><b>为何不是 OnGameLoaded</b>：那时道路搜索树刚在 PreDeserialize 被清空，
        /// 要等同一帧稍后的 Modification5 才重建（Game.Net.SearchSystem），这时栅格化每格都算作附近没有道路。
        /// 本回调在加载画面结束后才触发，搜索树早已重建；加载期间模拟速度为 0，常规更新插不进来，
        /// 图仍是存档读回的原样。开了"读档后暂停"也照样触发。</para>
        ///
        /// <para><b>判据是整图全零</b>：常规更新过一次，每格就都大于零（水面写基准值，陆地向不低于基准值的目标插值），
        /// 所以全零说明本存档从没画过这张图。只补读档：新开城时原版同样从空图起步，
        /// 原版地价系统只在 GameSimulation 运行，地图文件里的图从没算过。</para>
        /// </summary>
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (mode != GameMode.Game || purpose != Purpose.LoadGame) return;
            if (!m_Map.IsCreated) return;

            // 只读检查，收敛写入者（读档 job）即可
            m_WriteDependencies.Complete();
            for (int i = 0; i < m_Map.Length; i++)
            {
                // 存档里有本系统的图：沿用，与原版读回自己的图一致
                if (m_Map[i].m_LandValue != 0f) return;
            }

            // 手动 Update 走 ECS 的组件依赖管理（原版 NaturalResourceSystem.PostDeserialize 同样手动 Update）。
            // 本回调抛出的异常不会停用系统，但会弹游戏的错误对话框，所以就地记录，退回等首次常规更新
            m_ColdStart = true;
            bool rasterized;
            try
            {
                Update();
                // OnUpdate 取用时会清除标志；仍未清除说明它没跑到取用处
                rasterized = !m_ColdStart;
            }
            catch (System.Exception ex)
            {
                ModLog.Error(Tag, ex, "读档冷启动失败，地价热力图改等首次常规更新");
                return;
            }
            finally
            {
                // 不能留给常规更新：那一次要照常推进 Edge 地价
                m_ColdStart = false;
            }

            if (rasterized)
            {
                ModLog.Ok(Tag, $"读档时地价图为空，已立即整图栅格化一次（{kTextureSize}²，直接写目标值）");
            }
            else
            {
                // 没有带地价的道路（RequireAnyForUpdate 未满足），或地价参数尚未就绪
                ModLog.Info(Tag, "读档时地价图为空，但当前没有可栅格化的道路或地价参数，等首次常规更新");
            }
        }
        #endregion

        #region Job Structs

        // 封装环境数据采样逻辑，供两个Job复用
        public struct EnvironmentSampler
        {
            [ReadOnly] public NativeArray<TerrainAttractiveness> m_AttractiveMap;
            [ReadOnly] public NativeArray<GroundPollution> m_GroundPollutionMap;
            [ReadOnly] public NativeArray<AirPollution> m_AirPollutionMap;
            [ReadOnly] public NativeArray<NoisePollution> m_NoisePollutionMap;
            [ReadOnly] public NativeArray<AvailabilityInfoCell> m_AvailabilityInfoMap;
            [ReadOnly] public CellMapData<TelecomCoverage> m_TelecomCoverageMap;
            [ReadOnly] public WaterSurfaceData<SurfaceWater> m_WaterSurfaceData;
            [ReadOnly] public TerrainHeightData m_TerrainHeightData;

            // 参数数据
            [ReadOnly] public LandValueParameterData m_LvParams;
            [ReadOnly] public AttractivenessParameterData m_AttrParams;

            public int m_MapSize;
            public int m_AttractionMapSize;

            // 核心逻辑：计算某坐标点的“环境固有价值”（不含服务设施加成）
            public float CalculateBaseEnvironmentValue(float3 pos)
            {
                // 1. 采样基础数据
                float gPol = XCellMapSystemRe.GroundPollutionSystemGetPollution(pos, m_GroundPollutionMap).m_Pollution;
                float aPol = XCellMapSystemRe.AirPollutionSystemGetPollution(pos, m_AirPollutionMap).m_Pollution;
                float nPol = XCellMapSystemRe.NoisePollutionSystemGetPollution(pos, m_NoisePollutionMap).m_Pollution;
                float waterPol = WaterUtils.SamplePolluted(ref m_WaterSurfaceData, pos);

                float availX = XCellMapSystemRe.AvailabilityInfoToGridSystemGetAvailabilityInfo(pos, m_AvailabilityInfoMap).m_AvailabilityInfo.x;
                float telecom = TelecomCoverage.SampleNetworkQuality(m_TelecomCoverageMap, pos);

                // 2. 计算加成
                float mapBonus = 0f;

                // 吸引力（无污染时才计入）
                if (waterPol <= 0.01f && gPol <= 0.01f)
                {
                    float attrRaw = SampleAttr(pos);
                    mapBonus += math.min(math.max(attrRaw - 5f, 0f) * m_LvParams.m_AttractivenessBonusMultiplier, m_LvParams.m_CommonFactorMaxBonus);
                }

                // 可用性与电信
                mapBonus += math.min((availX - 5f) * m_LvParams.m_AttractivenessBonusMultiplier, m_LvParams.m_CommonFactorMaxBonus);
                mapBonus += math.min(telecom * m_LvParams.m_TelecomCoverageBonusMultiplier, m_LvParams.m_CommonFactorMaxBonus);

                // 3. 计算惩罚
                float penalty = gPol * m_LvParams.m_GroundPollutionPenaltyMultiplier +
                                aPol * m_LvParams.m_AirPollutionPenaltyMultiplier +
                                nPol * m_LvParams.m_NoisePollutionPenaltyMultiplier;

                return mapBonus - penalty;
            }

            private float SampleAttr(float3 p)
            {
                int2 cell = XCellMapSystemRe.GetCell(p, m_MapSize, m_AttractionMapSize);
                int index = cell.y * m_AttractionMapSize + cell.x;
                if (index < 0 || index >= m_AttractiveMap.Length) return 0f;

                return TerrainAttractivenessSystem.EvaluateAttractiveness(
                     TerrainUtils.SampleHeight(ref m_TerrainHeightData, p),
                     m_AttractiveMap[index], m_AttrParams);
            }
        }

        // Phase 1: Edge Calculation(逻辑核心)
        [BurstCompile]
        private struct EdgeLandValueJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle m_EntityType;
            [ReadOnly] public ComponentTypeHandle<Curve> m_CurveType;
            [ReadOnly] public BufferTypeHandle<Game.Net.ServiceCoverage> m_ServiceCoverageType;
            [ReadOnly] public BufferTypeHandle<ResourceAvailability> m_AvailabilityType;

            public ComponentTypeHandle<LandValue> m_LandValues; // Write           

            // 引入复用的采样器
            [ReadOnly] public EnvironmentSampler m_Sampler;

            // ModSetting配置变量
            public float m_LerpSpeed; // 平滑系数
            public float m_EnvDampeningFactor; // 环境因子衰减系数 (0~1)
            public float m_ServiceBonusCapMultiplier; // 服务加成上限乘数 (0~2)

            // 遍历所有具有地价组件的道路边缘
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Curve> curves = chunk.GetNativeArray(ref m_CurveType);
                NativeArray<LandValue> landValues = chunk.GetNativeArray(ref m_LandValues);

                BufferAccessor<Game.Net.ServiceCoverage> serviceCoverages = chunk.GetBufferAccessor(ref m_ServiceCoverageType);
                BufferAccessor<ResourceAvailability> availabilities = chunk.GetBufferAccessor(ref m_AvailabilityType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    // === A. 环境加成 (Map) ===
                    // [Map 点状分布]
                    // --- A1. 多点采样平均 ---
                    // 采样 0.2, 0.5, 0.8 三个位置，避免长道路判定失误
                    Bezier4x3 bezier = curves[i].m_Bezier;

                    float bonus0 = m_Sampler.CalculateBaseEnvironmentValue(MathUtils.Position(bezier, 0.2f));
                    float bonus1 = m_Sampler.CalculateBaseEnvironmentValue(MathUtils.Position(bezier, 0.5f));
                    float bonus2 = m_Sampler.CalculateBaseEnvironmentValue(MathUtils.Position(bezier, 0.8f));

                    float finalMapNetValue = (bonus0 + bonus1 + bonus2) * 0.333333f;

                    // === B. 服务加成 (Buffer) ===
                    // [Buffer 全路段共享]
                    // --- B1. 服务加成：医疗/警务/教育 ---
                    float serviceBonus = 0f;
                    if (serviceCoverages.Length > 0)
                    {
                        var services = serviceCoverages[i];
                        if (services.Length > 5)
                        {
                            serviceBonus += math.lerp(services[0].m_Coverage.x, services[0].m_Coverage.y, 0.5f) * m_Sampler.m_LvParams.m_HealthCoverageBonusMultiplier;
                            serviceBonus += math.lerp(services[2].m_Coverage.x, services[2].m_Coverage.y, 0.5f) * m_Sampler.m_LvParams.m_PoliceCoverageBonusMultiplier;
                            serviceBonus += math.lerp(services[5].m_Coverage.x, services[5].m_Coverage.y, 0.5f) * m_Sampler.m_LvParams.m_EducationCoverageBonusMultiplier;
                        }
                    }

                    // --- B2. 资源加成：商业/公交/电车地铁 ---
                    if (availabilities.Length > 0)
                    {
                        var res = availabilities[i];
                        if (res.Length > 32)
                        {
                            serviceBonus += math.lerp(res[1].m_Availability.x, res[1].m_Availability.y, 0.5f) * m_Sampler.m_LvParams.m_CommercialServiceBonusMultiplier;
                            serviceBonus += math.lerp(res[31].m_Availability.x, res[31].m_Availability.y, 0.5f) * m_Sampler.m_LvParams.m_BusBonusMultiplier;
                            serviceBonus += math.lerp(res[32].m_Availability.x, res[32].m_Availability.y, 0.5f) * m_Sampler.m_LvParams.m_TramSubwayBonusMultiplier;
                        }
                    }
                    // Cap 服务加成
                    float finalServiceBonus = math.min(serviceBonus, m_Sampler.m_LvParams.m_CommonFactorMaxBonus * 2.5f * m_ServiceBonusCapMultiplier);

                    // === C. 聚合计算 ===
                    float baseline = m_Sampler.m_LvParams.m_LandValueBaseline;
                    // 环境衰减：控制环境因子对 Edge 地价的传递比例 (ModSettings Slider)
                    float dampedEnvValue = finalMapNetValue * m_EnvDampeningFactor;
                    float targetValue = math.max(baseline, baseline + dampedEnvValue + finalServiceBonus);

                    // === D. 写入组件 ===
                    LandValue currentLv = landValues[i];
                    currentLv.m_LandValue = math.lerp(currentLv.m_LandValue, targetValue, m_LerpSpeed); // 保持时间平滑
                    landValues[i] = currentLv;
                }
            }

        } // job end;

        // Phase 2: Map Rasterization（可视化）
        [BurstCompile]
        private struct LandValueToGridJob : IJobParallelFor
        {
            // 写入
            public NativeArray<TargetType> m_LandValueMap;

            // 数据
            [ReadOnly] public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;
            [ReadOnly] public ComponentLookup<LandValue> m_LandValueData;
            // [ReadOnly] public ComponentLookup<EdgeGeometry> m_EdgeGeometryData; // 改用Curve
            // 引入 Curve 组件获取道路形状
            [ReadOnly] public ComponentLookup<Curve> m_CurveData;

            // 引入复用的采样器
            [ReadOnly] public EnvironmentSampler m_Sampler;

            public int m_TextureSize;

            // ModSetting配置变量
            public float m_SpreadDistance;

            // 读档冷启动：直接写目标值，不做 0.4 插值（见 OnGameLoadingComplete）
            public bool m_DirectWrite;

            public void Execute(int index)
            {
                // 获取当前像素点的世界坐标中心
                float3 cellCenter = XCellMapSystemRe.GetCellCenter(index, m_TextureSize);

                // 水域剔除 (性能优化)
                if (WaterUtils.SampleDepth(ref m_Sampler.m_WaterSurfaceData, cellCenter) > 1f)
                {
                    m_LandValueMap[index] = new TargetType { m_LandValue = m_Sampler.m_LvParams.m_LandValueBaseline };
                    return;
                }

                // =========================================================
                // A. 计算“自然背景地价”（荒野）
                // 即使没有道路，污染和风景也应该显示出来
                // =========================================================
                float envNetValue = m_Sampler.CalculateBaseEnvironmentValue(cellCenter);
                float baseline = m_Sampler.m_LvParams.m_LandValueBaseline;
                float backgroundValue = math.max(baseline, baseline + envNetValue);

                // =========================================================
                // B. 投射地价热力图
                // =========================================================

                // 道路搜索与加权平均
                // 1. 设置搜索范围
                float radius = m_SpreadDistance;
                float radiusSq = math.max(1f, radius * radius);

                // 2. 初始化迭代器
                NetIterator iterator = new NetIterator
                {
                    m_CellCenter = cellCenter,
                    m_RadiusSq = radiusSq,

                    m_InvRadiusSq = 1.0f / radiusSq,
                    m_Baseline = baseline,

                    m_Bounds = new Bounds3(cellCenter - radius, cellCenter + radius),  // 构造查询包围盒 (XZ平面)

                    m_LandValueData = m_LandValueData,
                    m_CurveData = m_CurveData,

                    m_TotalWeight = 0f,
                    m_WeightedSum = 0f
                };

                // 3. 执行搜索
                m_NetSearchTree.Iterate(ref iterator);

                // 4. 计算最终目标值
                float targetValue;

                // 判断是否有道路覆盖
                if (iterator.m_TotalWeight > 0.001f)
                {
                    // 道路的平均地价(Average Road Value)
                    // 注意：在 EdgeJob 里已经把环境值加进去了，所以这里直接用道路值即可
                    // 此处如果有建筑，对应地价（含服务+环境）：
                    float roadValue = iterator.m_WeightedSum / iterator.m_TotalWeight;

                    // [优化] 处理边缘过渡问题 (Blending)
                    // 纯粹的IDW在权重低时可能会导致数值跳变，这里做一个简单的混合
                    // 如果只有一点点权重（很远），应该让它逐渐趋向backgroundValue
                    // 既然 EdgeJob 里的环境计算和上面的 A步骤环境计算使用的是同一套参数，
                    // 理论上 RoadValue 在去除服务加成后，应该非常接近 BackgroundValue
                    // 因此，直接使用 RoadValue 也是合理的，或者取二者最大值

                    float blendFactor = math.saturate(iterator.m_TotalWeight);

                    // 混合道路价与荒地价
                    targetValue = math.lerp(backgroundValue, roadValue, blendFactor);
                }
                else
                {
                    // B. 无道路：计算“荒地”的潜在价值（Baseline + 环境 - 污染）
                    // 这给了玩家规划建议，但不会算入服务加成（因为没有路就没有服务）

                    // 附近无道路，显示自然环境值（荒野模式）
                    targetValue = backgroundValue;
                }

                // 5. 平滑写入
                TargetType currentCell = m_LandValueMap[index];
                if (m_DirectWrite)
                {
                    // 读档冷启动：图从全零起步，按 0.4 插值约 4 次更新（游戏内 3 小时）才到 87%，这一次直接写目标值
                    currentCell.m_LandValue = targetValue;
                }
                // 仅当差异较大时 Lerp，节省性能，参数可调（0.4f 是原版数值）
                else if (math.abs(currentCell.m_LandValue - targetValue) >= 0.1f)
                {
                    currentCell.m_LandValue = math.lerp(currentCell.m_LandValue, targetValue, 0.4f);
                }
                m_LandValueMap[index] = currentCell;
            }

            // ===================== 优化 Iterator =====================

            private struct NetIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
            {
                // 输出累加器
                public float m_TotalWeight;
                public float m_WeightedSum;
                                            // 输入参数
                public float3 m_CellCenter; // 像素中心
                                            // public float m_Radius;      // 搜索半径
                public Bounds3 m_Bounds;
                public float m_RadiusSq;
                public float m_InvRadiusSq;
                public float m_Baseline;

                // 数据
                public ComponentLookup<LandValue> m_LandValueData;
                // public ComponentLookup<EdgeGeometry> m_EdgeGeometryData; // 改为Curve提高性能
                public ComponentLookup<Curve> m_CurveData;

                public bool Intersect(QuadTreeBoundsXZ bounds) => MathUtils.Intersect(bounds.m_Bounds, this.m_Bounds);

                public void Iterate(QuadTreeBoundsXZ bounds, Entity entity)
                {
                    // Level 1. 基础包围盒检查
                    if (!MathUtils.Intersect(bounds.m_Bounds, this.m_Bounds)) return;

                    // Level 2. 检查地价组件及阈值
                    if (!m_LandValueData.TryGetComponent(entity, out var lv)) return;
                    if (lv.m_LandValue <= m_Baseline + 0.1f) return;

                    if (!m_CurveData.TryGetComponent(entity, out var curve)) return;

                    // Level 3: AABB Distance Approximation（快速剔除）
                    // 计算点到 Bounds 的最小距离平方。如果连 AABB 最近点都太远，就不需要算 Bezier 了
                    // clamp center 限制到 bounds 内部，如果 center 在内部，距离为 0
                    float3 closestAABB = math.clamp(m_CellCenter, bounds.m_Bounds.min, bounds.m_Bounds.max);
                    // 这里只比较 XZ 平面，忽略 Y 轴差异（地价通常主要看水平距离）
                    float distSqAABB = math.distancesq(m_CellCenter.xz, closestAABB.xz);

                    if (distSqAABB > m_RadiusSq) return;

                    // Level 4: Precise Bezier Distance (昂贵操作)
                    // 计算点到贝塞尔曲线的最近距离
                    float distSqReal = MathUtils.DistanceSquared(curve.m_Bezier, m_CellCenter, out _);

                    if (distSqReal < m_RadiusSq)
                    {
                        // Kernel Function: Quadratic Kernel (No Sqrt)
                        // w = (1 - (d/r)^2)^2
                        float x = distSqReal * m_InvRadiusSq; // x = (d/r)^2
                        float t = 1.0f - x;
                        float weight = t * t;

                        m_WeightedSum += lv.m_LandValue * weight;
                        m_TotalWeight += weight;
                    }

                } // Iterate Method

            } // NetIterate Struct

        } // LandValueToGridJob

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



