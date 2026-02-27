// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// Game.Simulation.HouseholdBehaviorSystem替换
// v1.4.2较大变化

using Colossal.Mathematics;
using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Prefabs;
using Game.Prefabs.Modes;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace MapExtPDX.ModeA
{
    // =========================================================================================
    // 1. Mod 自定义系统类型 (当前类)
    using ModSystem = HouseholdBehaviorSystemMod;
    // 2. 原版系统类型 (用于禁用和定位)
    using TargetSystem = HouseholdBehaviorSystem;

    // =========================================================================================

    /// <summary>
    /// v2.2.0修复：
    /// 1. HouseholdBehaviorSystem 引发的找房过度问题
    /// 2. 修改家庭外出购物过频逻辑(除特殊商品如车辆外，其他资源实际无差别消耗)
    /// 3. 修改车辆购买条件合理性
    /// 4. 修正一些性能低下的算法
    /// 5. 增加"虚拟网购"功能(暂去除)
    /// </summary>
    public partial class HouseholdBehaviorSystemMod : GameSystemBase
    {
        #region 静态常量 - 游戏规则参数

        // === 配置中心(自定义) ===
        /// <summary>每天的更新次数基数</summary>
        // 原版值256
        public static readonly int kUpdatesPerDay = 128;

        // 计算更新间隔帧数。262144 等于一天(月)。
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / (kUpdatesPerDay * 16);

        // 消耗补偿系数 (tick频率降低，单次消耗需按比例增加)
        public static readonly int kUpdateScale = 256 / kUpdatesPerDay;

        // === 游戏性参数 ===
        public static readonly int KMaxShoppingPossibility = 80; // 最大购物概率%
        public static readonly int kMaxHouseholdNeedAmount = 2000; // 单次最大购买量
        public static readonly int kCarAmount = 50; // 购车资源单位
        public static readonly int kCarBuyingMinimumMoney = 10000; // 购车最低存款
        public static readonly int KMinimumShoppingAmount = 50; // 资源剩余多少时触发购物检查
        public static readonly int kMinimumShoppingMoney = 1000; // 最低可支配资金 // 注意也被ResourceBuyerSystem引用，谨慎修改

        // 每个市民的资源需求倍率
        // 默认似乎为1f, 提高以适配消耗倍率
        private static readonly float kResourceDemandPerCitizenMultiplier = 3.5f;

        public static readonly float
            kTrafficReduction = 0.0004f; // EconomyPrefab预制：交通拥堵对购物欲望的负面影响系数 // 同时被其他多个系统引用，行为无关，可单独设置

        #endregion

        #region 系统依赖和查询字段

        private EntityQuery m_HouseholdGroup;
        private EntityQuery m_EconomyParameterGroup;
        private EntityQuery m_GameModeSettingQuery;

        private SimulationSystem m_SimulationSystem;
        private EndFrameBarrier m_EndFrameBarrier;
        private ResourceSystem m_ResourceSystem;
        private TaxSystem m_TaxSystem;
        private CitySystem m_CitySystem;

        #endregion

        #region System Loop

        protected override void OnCreate()
        {
            base.OnCreate();

            // 1.禁用原版系统并获取原版系统引用
            // 使用 GetExistingSystemManaged 避免意外创建未初始化的系统
            var originalSystem = World.GetExistingSystemManaged<TargetSystem>();
            if (originalSystem != null)
            {
                originalSystem.Enabled = false;
                //#if DEBUG
                Mod.Info($"[{typeof(ModSystem).Name}] 禁用原系统: {typeof(TargetSystem).Name}");
                //#endif
            }
            else
            {
                // 仅在调试时提示，原版系统可能已被其他Mod移除或尚未加载
#if DEBUG
                Mod.Error($"[{typeof(ModSystem).Name}] 无法找到可禁用的原系统(尚未加载或可能被其他Mod移除): {typeof(TargetSystem).Name}");
#endif
            }

            // 获取依赖系统
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_ResourceSystem = World.GetOrCreateSystemManaged<ResourceSystem>();
            m_TaxSystem = World.GetOrCreateSystemManaged<TaxSystem>();
            m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();

            // 创建经济参数查询(改为QueryBuilder方式,下同)
            m_EconomyParameterGroup = SystemAPI.QueryBuilder()
                .WithAll<EconomyParameterData>()
                .Build();

            // 创建家庭查询 - 包含所有正常家庭（排除游客、搬走的、已删除的、临时的）
            m_HouseholdGroup = SystemAPI.QueryBuilder()
                .WithAllRW<Household, HouseholdNeed>()
                .WithAll<HouseholdCitizen, Resources, UpdateFrame>()
                .WithNone<TouristHousehold, MovingAway, Deleted, Temp>()
                .Build();

            // 创建游戏模式设置查询(单查询保留原版方式)
            m_GameModeSettingQuery = SystemAPI.QueryBuilder()
                .WithAll<ModeSettingData>()
                .Build();

            // 设置系统运行所需的查询条件
            RequireForUpdate(m_HouseholdGroup);
            RequireForUpdate(m_EconomyParameterGroup);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            // 计算当前更新帧索引（用于分批更新家庭，避免单帧压力过大）
            uint updateFrameWithInterval = SimulationUtils.GetUpdateFrameWithInterval(m_SimulationSystem.frameIndex,
                (uint)GetUpdateInterval(SystemUpdatePhase.GameSimulation), 16); // 将家庭分为16批次更新

            // 创建家庭更新任务
            var householdTickJob = new HouseholdTickJob
            {
                // 获取类型句柄
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_HouseholdType = SystemAPI.GetComponentTypeHandle<Household>(false),
                m_HouseholdNeedType = SystemAPI.GetComponentTypeHandle<HouseholdNeed>(false),
                m_ResourceType = SystemAPI.GetBufferTypeHandle<Resources>(false),
                m_HouseholdCitizenType = SystemAPI.GetBufferTypeHandle<HouseholdCitizen>(true),
                m_TouristHouseholdType = SystemAPI.GetComponentTypeHandle<TouristHousehold>(false),
                m_CommuterHouseholdType = SystemAPI.GetComponentTypeHandle<CommuterHousehold>(true),
                m_UpdateFrameType = SystemAPI.GetSharedComponentTypeHandle<UpdateFrame>(),
                m_LodgingSeekerType = SystemAPI.GetComponentTypeHandle<LodgingSeeker>(true),

                // 获取组件查找表
                m_Workers = SystemAPI.GetComponentLookup<Worker>(true),
                m_OwnedVehicles = SystemAPI.GetBufferLookup<OwnedVehicle>(true),
                m_RenterBufs = SystemAPI.GetBufferLookup<Renter>(true),
                m_HomelessHouseholds = SystemAPI.GetComponentLookup<HomelessHousehold>(true),
                m_PropertySeekers = SystemAPI.GetComponentLookup<PropertySeeker>(true),
                m_PropertyRenters = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                m_ResourceDatas = SystemAPI.GetComponentLookup<ResourceData>(true),
                m_LodgingProviders = SystemAPI.GetComponentLookup<LodgingProvider>(true),
                m_CitizenDatas = SystemAPI.GetComponentLookup<Citizen>(true),
                m_Populations = SystemAPI.GetComponentLookup<Population>(true),
                m_PrefabRefs = SystemAPI.GetComponentLookup<PrefabRef>(true),
                m_HealthProblems = SystemAPI.GetComponentLookup<HealthProblem>(true),
                m_ConsumptionDatas = SystemAPI.GetComponentLookup<ConsumptionData>(true),

                // 获取经济参数单例数据
                m_EconomyParameters = SystemAPI.GetSingleton<EconomyParameterData>(),

                // 获取资源系统的预制体数据
                m_ResourcePrefabs = m_ResourceSystem.GetPrefabs(),

                // 获取税收系统的税率数据
                m_TaxRates = m_TaxSystem.GetTaxRates(),

                // 生成新的随机数种子
                m_RandomSeed = RandomSeed.Next(),

                // 设置资源需求倍数
                m_ResourceDemandPerCitizenMultiplier = kResourceDemandPerCitizenMultiplier,

                // 创建命令缓冲区用于延迟执行实体操作
                m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),

                // 设置当前更新帧索引
                m_UpdateFrameIndex = updateFrameWithInterval,

                // v1.4.2新增: 设置当前模拟帧索引
                m_FrameIndex = m_SimulationSystem.frameIndex,

                // 设置城市实体
                m_City = m_CitySystem.City
            };

            // 调度任务并行执行
            Dependency = JobChunkExtensions.ScheduleParallel(householdTickJob, m_HouseholdGroup, Dependency);

            // 将依赖关系添加到帧结束屏障，确保命令缓冲区在正确时机执行
            m_EndFrameBarrier.AddJobHandleForProducer(Dependency);

            // 通知资源系统有读取操作，用于依赖管理
            m_ResourceSystem.AddPrefabsReader(Dependency);

            // 通知税收系统有读取操作，用于依赖管理
            m_TaxSystem.AddReader(Dependency);
        }

        #endregion


        [BurstCompile]
        private struct HouseholdTickJob : IJobChunk
        {
            // 获取实体的Handle
            [ReadOnly] public EntityTypeHandle m_EntityType;

            // 家庭组件，包含金钱、资源存量等
            public ComponentTypeHandle<Household> m_HouseholdType;

            // 家庭需求组件，记录当前需要购买的资源
            public ComponentTypeHandle<HouseholdNeed> m_HouseholdNeedType;

            // 家庭成员列表缓冲区
            [ReadOnly] public BufferTypeHandle<HouseholdCitizen> m_HouseholdCitizenType;

            // 资源缓冲区(资源种类和数量)
            public BufferTypeHandle<Resources> m_ResourceType;

            // 用于分帧更新的组件
            [ReadOnly] public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

            // 游客家庭标记
            public ComponentTypeHandle<TouristHousehold> m_TouristHouseholdType;

            // 通勤家庭标记（住在城外）
            [ReadOnly] public ComponentTypeHandle<CommuterHousehold> m_CommuterHouseholdType;

            // 寻找住宿者标记（游客找旅馆）
            [ReadOnly] public ComponentTypeHandle<LodgingSeeker> m_LodgingSeekerType;

            // 无家可归状态查找表
            [ReadOnly] public ComponentLookup<HomelessHousehold> m_HomelessHouseholds;

            // 拥有的车辆查找表
            [ReadOnly] public BufferLookup<OwnedVehicle> m_OwnedVehicles;

            // 租赁者缓冲区（建筑侧）
            [ReadOnly] public BufferLookup<Renter> m_RenterBufs;

            // 房产寻找者查找表
            [ReadOnly] public ComponentLookup<PropertySeeker> m_PropertySeekers;

            // 房产租赁者（家庭侧，指向居住的建筑）
            [ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenters;

            // 工人组件查找表
            [ReadOnly] public ComponentLookup<Worker> m_Workers;

            // 资源数据配置
            [ReadOnly] public ComponentLookup<ResourceData> m_ResourceDatas;

            // 住宿提供者（旅馆/避难所）
            [ReadOnly] public ComponentLookup<LodgingProvider> m_LodgingProviders;

            // 城市人口统计
            [ReadOnly] public ComponentLookup<Population> m_Populations;

            // 市民个体数据（年龄、快乐度等）
            [ReadOnly] public ComponentLookup<Citizen> m_CitizenDatas;

            // 消费数据配置
            [ReadOnly] public ComponentLookup<ConsumptionData> m_ConsumptionDatas;

            // 预制体引用
            [ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefs;

            // 健康问题查找表
            [ReadOnly] public ComponentLookup<HealthProblem> m_HealthProblems;

            // 资源预制体映射
            [ReadOnly] public ResourcePrefabs m_ResourcePrefabs;

            // 当前税率数组
            [ReadOnly] public NativeArray<int> m_TaxRates;

            // 随机数种子
            public RandomSeed m_RandomSeed;

            // 每个市民的资源需求乘数
            public float m_ResourceDemandPerCitizenMultiplier;

            // 经济参数
            public EconomyParameterData m_EconomyParameters;

            // 并行实体命令缓冲区（用于添加/删除组件）
            public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            // 当前需要更新的帧索引（用于分帧）
            public uint m_UpdateFrameIndex;

            // 当前游戏全局帧索引
            public uint m_FrameIndex;

            // 城市实体（用于获取人口等全局数据）
            public Entity m_City;

            private struct HouseholdCache
            {
                public int TotalWealth; // 总财富
                public int CarCount; // 车辆数
                public int4 AgeCounts; // x:Child, y:Teen, z:Adult, w:Elderly
                public float AvgHappiness; // 平均幸福度
                public int LastDayIncome; // 昨日收入
                public int FamilySize; // 家庭人数
            }

            // 统一计算家庭成员年龄结构/幸福度/收入
            // 整合Job内联方法和EconomyUtils.GetHouseholdIncome
            private HouseholdCache PrecalculateHouseholdData(
                Entity householdEntity,
                Household household,
                DynamicBuffer<HouseholdCitizen> citizens,
                DynamicBuffer<Game.Economy.Resources> resources
            )
            {
                // --- 基础数据 ---
                HouseholdCache basecache = new()
                {
                    FamilySize = citizens.Length,
                    TotalWealth = EconomyUtils.GetHouseholdTotalWealth(household, resources)
                };

                // --- 车辆统计 ---
                if (m_OwnedVehicles.HasBuffer(householdEntity))
                    basecache.CarCount = m_OwnedVehicles[householdEntity].Length;
                else
                    basecache.CarCount = 0;

                // --- 成员遍历统计 (整合收入、年龄、幸福度) ---
                int householdIncome = 0;
                int totalHappiness = 0;
                basecache.AgeCounts = int4.zero;

                for (int i = 0; i < citizens.Length; i++)
                {
                    Entity citizenEntity = citizens[i].m_Citizen;

                    // 跳过不存在或去世成员
                    if (!m_CitizenDatas.HasComponent(citizenEntity)) continue;
                    if (CitizenUtils.IsDead(citizenEntity, ref m_HealthProblems)) continue;

                    Citizen citizenData = m_CitizenDatas[citizenEntity];
                    CitizenAge age = citizenData.GetAge();

                    // 收入计算 + 年龄统计
                    switch (age)
                    {
                        case CitizenAge.Child:
                            basecache.AgeCounts.x++;
                            householdIncome += m_EconomyParameters.m_FamilyAllowance;
                            break;
                        case CitizenAge.Teen:
                            basecache.AgeCounts.y++;
                            householdIncome += m_EconomyParameters.m_FamilyAllowance;
                            break;
                        case CitizenAge.Elderly:
                            basecache.AgeCounts.w++;
                            householdIncome += m_EconomyParameters.m_Pension;
                            break;
                        case CitizenAge.Adult:
                            basecache.AgeCounts.z++;

                            if (m_Workers.HasComponent(citizenEntity))
                            {
                                int workerLevel = m_Workers[citizenEntity].m_Level;
                                int wage = m_EconomyParameters.GetWage(workerLevel);
                                householdIncome += wage;
                                int taxableIncome = wage - m_EconomyParameters.m_ResidentialMinimumEarnings;
                                if (taxableIncome > 0)
                                {
                                    int taxRate = TaxSystem.GetResidentialTaxRate(workerLevel, m_TaxRates);
                                    householdIncome -= (int)math.round(taxableIncome * (taxRate / 100f));
                                }
                            }
                            else
                            {
                                if (citizenData.m_UnemploymentCounter <
                                    m_EconomyParameters.m_UnemploymentAllowanceMaxDays * PayWageSystem.kUpdatesPerDay)
                                {
                                    householdIncome += m_EconomyParameters.m_UnemploymentBenefit;
                                }
                            }

                            break;
                    }

                    // 幸福度统计
                    totalHappiness += citizenData.Happiness;
                }

                basecache.LastDayIncome = householdIncome;
                basecache.AvgHappiness = basecache.FamilySize > 0 ? totalHappiness / basecache.FamilySize : 0;
                return basecache;
            }

            // 极速版的权重计算 (不再遍历 Member，仅使用 int4 乘法)
            // 移除多余的 IsLeisure 传入,调用前已做判断
            private int GetWeightOptimized(int spendableMoney, ResourceData data, HouseholdCache cache)
            {
                // 预判：如果权重因子都是0，直接返回
                if (data.m_ChildWeight == 0 && data.m_TeenWeight == 0 && data.m_AdultWeight == 0 &&
                    data.m_ElderlyWeight == 0 && data.m_CarConsumption == 0)
                    return 0;

                // 基础消耗
                float baseConsumption = data.m_BaseConsumption;
                // 车辆消耗
                baseConsumption += (float)(cache.CarCount * data.m_CarConsumption);

                // 财富修正
                float wealthMod = data.m_WealthModifier;

                // 年龄权重 (点积运算代替循环)
                // 假设 AgeCounts 是 int4(Child, Teen, Adult, Elderly)
                // 我们需要把 ResourceData 里的权重也取出来
                float ageWeight =
                    data.m_ChildWeight * cache.AgeCounts.x +
                    data.m_TeenWeight * cache.AgeCounts.y +
                    data.m_AdultWeight * cache.AgeCounts.z +
                    data.m_ElderlyWeight * cache.AgeCounts.w;

                // 最终公式
                float wealthFactor = math.smoothstep(wealthMod, 1f, math.max(0.01f, (spendableMoney + 5000f) / 10000f));

                return (int)math.round(100f * ageWeight * baseConsumption * wealthFactor);
            }

            // 辅助方法，用于检查家庭是否需要买车。
            private bool NeedsCar(int spendableMoney, int4 ageCounts, int cars, ref Random random)
            {
                // 【优化】 车辆购买条件
                // 当家庭可支配资金低于最低购车门槛，或者车辆数已经达到或超过家庭成人数量，不考虑买车。
                // 
                if (spendableMoney <= kCarBuyingMinimumMoney || cars >= ageCounts.z)
                {
                    return false;
                }

                // 否则使用原有的递减概率
                return random.NextFloat() < (0f - math.log((float)cars + 0.1f)) / 10f + 0.1;
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                // 检查当前Chunk是否属于本帧需要更新的，如果不是则跳过，这是分帧更新的标准做法。
                if (chunk.GetSharedComponent(m_UpdateFrameType).m_Index != m_UpdateFrameIndex) return;

                // 获取Chunk中的原生数组和访问器
                NativeArray<Entity> householdEntities = chunk.GetNativeArray(m_EntityType);
                NativeArray<Household> households = chunk.GetNativeArray(ref m_HouseholdType);
                NativeArray<HouseholdNeed> householdNeeds = chunk.GetNativeArray(ref m_HouseholdNeedType);
                BufferAccessor<HouseholdCitizen> householdCitizenBuffers =
                    chunk.GetBufferAccessor(ref m_HouseholdCitizenType);
                BufferAccessor<Resources> resourceBuffers = chunk.GetBufferAccessor(ref m_ResourceType);
                NativeArray<TouristHousehold> touristHouseholds = chunk.GetNativeArray(ref m_TouristHouseholdType);

                // 初始化随机数种子，确保结果可复现
                Random random = m_RandomSeed.GetRandom(unfilteredChunkIndex);

                // 缓存一些 Handle 检查，减少循环内开销
                bool chunkHasHomeless = chunk.Has<HomelessHousehold>();
                bool chunkHasTourist = chunk.Has(ref m_TouristHouseholdType);
                bool chunkHasCommuter = chunk.Has(ref m_CommuterHouseholdType);
                bool chunkHasLodgingSeeker = chunk.Has(ref m_LodgingSeekerType);

                // 获取当前城市总人口
                int cityPopulation = m_Populations[m_City].m_Population;

                // --- A. 预计算阶段 (Pre-calculation) ---
                // 预计算避免放入主循环内重复计算

                //// 1. 基础购物概率 (随人口指数衰减)
                ///
                // 计算基于人口的概率因子 (人口越多，单次判定通过率越低)
                float popFactor = math.max(1f, math.sqrt(kTrafficReduction * (float)cityPopulation));
                int baseshopChance = (int)math.round(200f / popFactor);

                // 低人口时遵循原版，百万人口后单次通过率限制在 1% - 5% 之间
                baseshopChance = math.clamp(baseshopChance, 1, KMaxShoppingPossibility);

                // =========================================================
                // --- B. 主循环逻辑 ---
                // =========================================================

                // 遍历Chunk中的所有实体(家庭)
                for (int i = 0; i < chunk.Count; i++)
                {
                    Entity householdEntity = householdEntities[i];
                    Household household = households[i];
                    DynamicBuffer<HouseholdCitizen> citizens = householdCitizenBuffers[i];
                    DynamicBuffer<Resources> resources = resourceBuffers[i];

                    // =========================================================
                    // --- 1. 每日重置购物统计数据 ---
                    // =========================================================
                    // 检查是否过了一天（262144ticks为游戏内一天，具体取决于模拟速度配置）
                    if (m_FrameIndex - household.m_LastDayFrameIndex > 262144)
                    {
                        // 归档上一天的购物总值
                        household.m_ShoppedValueLastDay = household.m_ShoppedValuePerDay;
                        household.m_ShoppedValuePerDay = 0u;
                        household.m_MoneySpendOnBuildingLevelingLastDay = 0;
                        household.m_LastDayFrameIndex = m_FrameIndex;
                    }

                    // =========================================================
                    // --- 2. 空家庭清理 ---
                    // =========================================================
                    if (citizens.Length == 0)
                    {
                        // 如果家庭中没有市民，则标记为删除，并跳过当前实体的所有后续处理。
                        m_CommandBuffer.AddComponent(unfilteredChunkIndex, householdEntity, default(Deleted));
                        continue;
                    }

                    // =========================================================
                    // --- 3. 预计算家庭数据 ---
                    // =========================================================
                    // 【优化】预计算家庭数据：人口结构年龄/幸福度/日收入/总财富/可支配资金/车辆数等
                    // 家庭成员次循环，将 GetAgeWeight 及多处内部循环提取一次计算，避免反复遍历

                    HouseholdCache basecache =
                        PrecalculateHouseholdData(householdEntity, household, citizens, resources);

                    // 更新昨日收入
                    household.m_SalaryLastDay = basecache.LastDayIncome;

                    // =========================================================
                    // --- 4. 幸福度搬离 ---
                    // =========================================================

                    bool isUnhappyAndConsideringMoving = false;
                    // 计算平均快乐度
                    float averageHappiness = basecache.AvgHappiness; // 原版为int，改为float避免整数除法误差

                    // 原版复杂公式用于判断家庭是否因“不开心”而考虑迁出。“如果平均幸福度低于 30，根据不开心程度，有 0% 到 14% 的概率触发搬家意愿。” 
                    // 【优化】--------------------------------------------------------
                    // 修改幸福度迁出公式，简化运算，大规模城市人口下没人关心Sqrt带来的微小曲线差异
                    // 简化版：
                    // 在幸福度为0时约有14%概率搬家，幸福度越高概率指数级下降，超过30后概率基本为0

                    if (averageHappiness < 30)
                    {
                        // 线性近似概率，避免 exp 计算
                        // 0 happiness -> ~14% chance. 30 happiness -> 0 chance.
                        // 140 * e^(-0.11 * happiness) 拟合度非常高
                        float threshold = 140f * math.exp(-0.11f * averageHappiness);
                        isUnhappyAndConsideringMoving = random.NextInt(1000) < threshold;
                    }
                    // ----------------------------------------------------------------

                    // =========================================================
                    // --- 5. 迁出逻辑判定 ---
                    // =========================================================

                    bool hasNoAdults = (basecache.AgeCounts.z + basecache.AgeCounts.w) == 0;
                    bool isBankrupt = (basecache.TotalWealth + basecache.LastDayIncome < -1000);

                    // 决定迁出原因：没有成年人 -> 不开心 -> 破产 -> 不迁出
                    MoveAwayReason moveAwayReason =
                        hasNoAdults
                            ? MoveAwayReason.NoAdults
                            : (isUnhappyAndConsideringMoving
                                ? MoveAwayReason.NotHappy
                                : (isBankrupt ? MoveAwayReason.NoMoney : MoveAwayReason.None));

                    // 如果决定迁出，则执行迁出命令(添加迁出组件)并跳过后续处理。
                    if (moveAwayReason != MoveAwayReason.None)
                    {
                        CitizenUtils.HouseholdMoveAway(m_CommandBuffer, unfilteredChunkIndex, householdEntity,
                            moveAwayReason);
                        continue;
                    }

                    // =========================================================
                    // --- 6. 需求与购物逻辑 ---
                    // =========================================================
                    // 如果家庭不是无家可归状态，则更新其需求。
                    if (!chunkHasHomeless)
                    {
                        bool hasHome = true;
                        // 没有无家可归组件，但也没有租房组件或者租房组件指向空实体(原版修复失去房产或其他原因导致无房的家庭却没有纳入无家可归的bug)
                        if (!m_PropertyRenters.HasComponent(householdEntity) ||
                            m_PropertyRenters[householdEntity].m_Property == Entity.Null)
                        {
                            // 如果已经标记为"搬入" (MovedIn)，则说明失去了房子，变为无家可归，则添加标记为无家可归状态
                            if ((household.m_Flags & HouseholdFlags.MovedIn) != HouseholdFlags.None)
                            {
                                m_CommandBuffer.AddComponent<HomelessHousehold>(unfilteredChunkIndex, householdEntity);
                                hasHome = false;
                            }
                        }

                        if (hasHome)
                        {
                            // 家庭有房(没有无家可归组件且租房组件有效)，则正常更新需求
                            // PropertyRenter propertyRenter = m_PropertyRenters[householdEntity];
                            // 更新家庭的需求（消费资源或产生购物需求）

                            // 【优化2-StepB】 ---------------------------------------------
                            // 调用 UpdateHouseholdNeed 时，传入预计算好的参数
                            UpdateHouseholdNeed(
                                //chunk
                                i,
                                unfilteredChunkIndex,
                                ref household,
                                ref householdNeeds,
                                ref random,
                                basecache,
                                //cityPopulation,
                                touristHouseholds,
                                householdEntity,
                                resources,
                                chunkHasTourist,
                                chunkHasLodgingSeeker,
                                baseshopChance);
                        }
                    }
                    else // 具有无家可归组件
                    {
                        // 无家可归的家庭每天只会少量消耗金钱，模拟基本生存开销
                        EconomyUtils.AddResources(Resource.Money, -1, resources);
                    }

                    // =========================================================
                    // --- 8. 找房逻辑 ---
                    // =========================================================
                    // 对于非游客、非通勤且当前不在找房的家庭(无家可归和已有住房家庭)
                    bool isSeeker = m_PropertySeekers.IsComponentEnabled(householdEntity);
                    if (!chunkHasTourist && !chunkHasCommuter && !isSeeker)
                    {
                        // 检查当前家庭所在的建筑
                        Entity householdHomeBuilding = BuildingUtils.GetHouseholdHomeBuilding(householdEntity,
                            ref m_PropertyRenters, ref m_HomelessHouseholds);

                        // --- 修改代码 ---
                        // 【优化】仅当家庭确实没有房屋时才强制启用 PropertySeeker，保证最基本、最必要的找房逻辑。
                        // m_RenterBufs额外健壮性检查，防止房屋没有租户列表。
                        if (householdHomeBuilding == Entity.Null || !m_RenterBufs.HasBuffer(householdHomeBuilding))
                        {
                            m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex,
                                householdEntities[i], value: true);
                        }
                        // 【优化】整个 'else' 分支被移除，彻底杜绝所有基于随机数的“改善性”找房请求。
                        // RentAdjustSystem 已经存在同类逻辑
                        // “寻求改善”的逻辑现在完全交由 RentAdjustSystem 来更智能地处理。
                    }

                    // 将修改后的 household 实体数据写回 Household 数组
                    households[i] = household;
                }
            }

            /// <summary>
            /// 更新家庭的资源需求、消费和购物行为的子系统
            /// </summary>
            private void UpdateHouseholdNeed(
                //ArchetypeChunk chunk,
                int index,
                int unfilteredChunkIndex,
                ref Household household,
                ref NativeArray<HouseholdNeed> householdNeeds,
                ref Unity.Mathematics.Random random,
                HouseholdCache cache,
                //int population,
                NativeArray<TouristHousehold> touristHouseholds,
                Entity entity,
                DynamicBuffer<Game.Economy.Resources> resources,
                bool isTourist,
                bool hasLodgingSeeker,
                int baseShopChance)
            {
                // 创建对当前家庭实体需求的引用
                HouseholdNeed currentNeed = householdNeeds[index];

                // =========================================================
                // 阶段 A: 消耗现有资源
                // =========================================================
                // 如果家庭有内部资源储备 (m_Resources > 0)，则直接消耗这些储备。
                if (household.m_Resources > 0)
                {
                    // 计算基础消耗量：(基础系数 + 财富修正) * 人均消耗 * 人数
                    // ！如需调整家庭消费量，可修改此处的计算公式。
                    float consumptionAmount = GetConsumptionMultiplier(
                        m_EconomyParameters.m_ResourceConsumptionMultiplier,
                        cache.TotalWealth) * m_EconomyParameters.m_ResourceConsumptionPerCitizen * cache.FamilySize;
                    // 【关键修复】补偿时间步长差异：频率低了，每次要多消耗
                    consumptionAmount *= kUpdateScale;

                    // 原版设定：
                    //  m_ResourceConsumptionMultiplier = new float2(0.3f, 10f); 全城最穷家庭0.3倍，最富10倍；
                    // m_ResourceConsumptionPerCitizen = 3.5f; 全城基础人均日消耗3.5单位资源

                    // 游客家庭特殊逻辑。如果是游客家庭，消费量要乘以一个倍数(游客消费更高)
                    //if (chunk.Has(ref this.m_TouristHouseholdType))
                    if (isTourist)
                    {
                        consumptionAmount *= m_EconomyParameters.m_TouristConsumptionMultiplier;

                        // 游客找旅馆逻辑
                        // 如果游客当前没有住处
                        if (!hasLodgingSeeker)
                        {
                            TouristHousehold touristData = touristHouseholds[index];

                            // 如果还没找到旅馆，或旅馆已失效，则添加 LodgingSeeker 组件开始寻找
                            // 如果没有旅馆
                            if (touristData.m_Hotel.Equals(Entity.Null))
                            {
                                m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, default(LodgingSeeker));
                            }
                            // 旅馆已失效
                            else if (!m_LodgingProviders.HasComponent(touristData.m_Hotel))
                            {
                                // 旅馆倒闭或消失，重置并重新寻找
                                touristData.m_Hotel = Entity.Null;
                                touristHouseholds[index] = touristData;
                                m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, default(LodgingSeeker));
                            }
                        }
                    }

                    // 家庭消耗逻辑：随机取整消耗量
                    int consumptionInt = MathUtils.RoundToIntRandom(ref random, consumptionAmount);

                    // 设置每日消耗统计 (限制最大值32767)
                    household.m_ConsumptionPerDay = (short)math.min(32767, kUpdatesPerDay * consumptionInt);

                    // 扣除当前tick周期消耗资源
                    household.m_Resources = math.max(household.m_Resources - consumptionInt, 0);

                    // 优化：如果还有大量资源(>最小购物量)，直接返回，不要在此帧尝试购物。
                    // 这将"消耗"和"决定购物"分离到了不同的帧，极大地平滑了逻辑峰值。
                    if (household.m_Resources > KMinimumShoppingAmount) return;
                    // 如果小于最小购买量或者为0时，执行后续购物逻辑。
                }
                // 钳制防止负数干扰逻辑
                else
                {
                    household.m_Resources = 0;
                    household.m_ConsumptionPerDay = 0;
                }

                // =========================================================
                // 阶段 B: 资源耗尽或低库存，产生购物需求逻辑
                // =========================================================
                // --- 核心优化：概率剪枝 (Throttling) ---
                int finalShopChance = baseShopChance;
                // 1. 检查是否已经有未完成的订单
                // 已经有需求了（可能正在寻路，或者正在排队买），等待它完成。
                if (currentNeed.m_Resource != Resource.NoResource) return;

                // 2. 购物欲望冷却.如果今天已经买过了，欲望大幅降低
                if (household.m_ShoppedValuePerDay != 0) finalShopChance /= 20;

                // 3.**安全阀 (Safety Valve)**：如果家里彻底没粮了，必须提高概率防止"饿死"
                if (household.m_Resources <= KMinimumShoppingAmount)
                {
                    finalShopChance = math.max(finalShopChance, 25); // 至少 25% 概率，保证几天内必买
                }

                // 4. 第一道概率门槛 (RNG快速剪枝)
                if (random.NextInt(100) > finalShopChance) return;

                // 5. 资金检查:当可支配资金过低时，不产生购物需求
                // 注意：kMinimumShoppingMoney = 1000
                // 此时才计算可支配资金 (Lazy Evaluation)
                PropertyRenter renter = m_PropertyRenters.HasComponent(entity) ? m_PropertyRenters[entity] : default;
                int spendableMoney = EconomyUtils.GetHouseholdSpendableMoney(household, resources, ref m_RenterBufs,
                    ref m_ConsumptionDatas, ref m_PrefabRefs, renter);

                // 没钱，清空需求
                if (spendableMoney < kMinimumShoppingMoney)
                {
                    currentNeed.m_Amount = 0;
                    currentNeed.m_Resource = Resource.NoResource;
                    householdNeeds[index] = currentNeed;
                    return;
                }

                // =========================================================
                // 阶段 C: 购买资源选择
                // =========================================================
                // --- 资源权重计算 (利用预计算数据) ---
                int currentTotalWeight = 0; // 当前累计权重
                ResourceIterator iterator = ResourceIterator.GetIterator();

                // 第一次遍历：求和 (纯数学计算，极快)
                while (iterator.Next())
                {
                    ResourceData resData = m_ResourceDatas[m_ResourcePrefabs[iterator.resource]];
                    if (!resData.m_IsLeisure) // 仅计算非娱乐资源
                        currentTotalWeight += GetWeightOptimized(spendableMoney, resData, cache);
                }

                if (currentTotalWeight <= 0) return;

                // 随机选择
                int randomWeight = random.NextInt(currentTotalWeight);
                iterator = ResourceIterator.GetIterator();

                // 第二次遍历：选择
                while (iterator.Next())
                {
                    ResourceData resData = m_ResourceDatas[m_ResourcePrefabs[iterator.resource]];
                    if (resData.m_IsLeisure) continue; // 跳过非娱乐资源，与前面保持一致

                    int weight = GetWeightOptimized(spendableMoney, resData, cache);
                    randomWeight -= weight;

                    if (weight > 0 && randomWeight < 0)
                    {
                        // 选中了 iterator.resource

                        // 特殊检查：购车逻辑
                        if (iterator.resource == Resource.Vehicles)
                        {
                            // 此时才计算是否需要买车
                            bool needsCar = NeedsCar(spendableMoney, cache.FamilySize, cache.CarCount, ref random);
                            if (needsCar)
                            {
                                currentNeed.m_Resource = Resource.Vehicles;
                                currentNeed.m_Amount = kCarAmount;
                                householdNeeds[index] = currentNeed;
                            }

                            break;
                        }

                        // 普通资源购买
                        else
                        {
                            float marketPrice = EconomyUtils.GetMarketPrice(resData);

                            // 计算购买量：尽可能买满，实现"低频大额"
                            int affordAmount = math.clamp((int)(spendableMoney / marketPrice), 0,
                                kMaxHouseholdNeedAmount);
                            int finalAmount = (int)(affordAmount * m_ResourceDemandPerCitizenMultiplier);

                            // 最小购买量限制：防止只买1个单位
                            if (finalAmount > 5) // 至少能买5个才加入需求清单
                            {
                                currentNeed.m_Resource = iterator.resource;
                                currentNeed.m_Amount = math.max(0, finalAmount);
                                householdNeeds[index] = currentNeed;
                            }

                            break; // 选中后退出
                        }
                    }
                }
            }

            // 原版方法
            public static float GetConsumptionMultiplier(float2 parameter, int householdWealth)
            {
                return parameter.x + parameter.y *
                    math.smoothstep(0f, 1f, (float)(math.max(0, householdWealth) + 1000) / 6000f);
            }
        } // job
    } // class
} // namespace










