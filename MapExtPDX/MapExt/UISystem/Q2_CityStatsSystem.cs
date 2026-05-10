// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Simulation;
using Game.Tools;
using MapExtPDX.MapExt.Core;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace MapExtPDX.UI
{
    /// <summary>
    /// 📊 [MOD] 城市统计数据收集系统（Phase 4）
    /// 按需运行：仅当 Dashboard 面板展开时由 MapExtUISystem 启用。
    /// 每 256 帧（约 4.3 秒）执行一次 ECS 查询，收集人口健康度指标。
    /// Phase 4 新增：住宅空置率、商业活动、人口活动（购物/休闲/通勤）。
    /// 所有新增数据均从游戏原生缓存系统零成本读取，无需自建 Burst Job。
    /// 面板关闭时 Enabled = false，零开销。
    /// </summary>
    public partial class Q2_CityStatsSystem : GameSystemBase
    {
        #region Constants

        private const string Tag = "CityStats";

        #endregion

        #region Fields

        private EntityQuery m_AllHouseholdQuery;
        private EntityQuery m_RenterQuery;
        private EntityQuery m_HomelessQuery;
        private EntityQuery m_MovingAwayQuery;
        private EntityQuery m_PropertySeekerHousedQuery;
        private EntityQuery m_PropertySeekerHomelessQuery;
        private EntityQuery m_HighRentBuildingQuery;
        private EntityQuery m_PetQuery;
        private EntityQuery m_CommuterQuery;

        // --- Phase 4: 游戏原生统计系统引用 ---
        private CountResidentialPropertySystem m_CountResPropertySystem;
        private CountCompanyDataSystem m_CountCompanySystem;
        private ResidentPurposeCounterSystem m_PurposeCounterSystem;

        /// <summary>反射获取的 ResidentPurposeCounterSystem.m_Results（Persistent NativeArray）</summary>
        private NativeArray<int> m_PurposeResults;
        private bool m_PurposeResultsValid;

        #endregion

        #region Public Properties — 供 MapExtUISystem GetterValueBinding 读取

        /// <summary>总家庭数（排除游客和通勤者）</summary>
        public int TotalHouseholds { get; private set; }

        /// <summary>已租住家庭数</summary>
        public int RentedHouseholds { get; private set; }

        /// <summary>无家可归家庭数</summary>
        public int HomelessCount { get; private set; }

        /// <summary>正在搬离城市的家庭数</summary>
        public int MovingAwayCount { get; private set; }

        /// <summary>正在找房的已有住房家庭（改善型搬迁或被驱逐）</summary>
        public int SeekerHousedCount { get; private set; }

        /// <summary>正在找房的无家可归家庭</summary>
        public int SeekerHomelessCount { get; private set; }

        /// <summary>带有高租金警告标志的建筑数量</summary>
        public int HighRentBuildingCount { get; private set; }

        /// <summary>宠物实体数量</summary>
        public int PetCount { get; private set; }

        // --- Phase 4: 住宅空置率（从 CountResidentialPropertySystem 缓存读取） ---

        /// <summary>低密度空置住宅数</summary>
        public int FreeResLow { get; private set; }
        /// <summary>中密度空置住宅数</summary>
        public int FreeResMed { get; private set; }
        /// <summary>高密度空置住宅数</summary>
        public int FreeResHigh { get; private set; }
        /// <summary>低密度总住宅数</summary>
        public int TotalResLow { get; private set; }
        /// <summary>中密度总住宅数</summary>
        public int TotalResMed { get; private set; }
        /// <summary>高密度总住宅数</summary>
        public int TotalResHigh { get; private set; }

        // --- Phase 4: 商业活动（从 CountCompanyDataSystem 缓存读取） ---

        /// <summary>有物业的商业公司总数</summary>
        public int TotalCommercial { get; private set; }
        /// <summary>无物业（等待入驻）的商业公司数</summary>
        public int CommercialPropertyless { get; private set; }

        // --- Phase 4: 人口活动（从 ResidentPurposeCounterSystem 缓存读取） ---

        /// <summary>正在前往购物的市民数</summary>
        public int ShoppingCount { get; private set; }
        /// <summary>正在休闲的市民数</summary>
        public int LeisureCount { get; private set; }
        /// <summary>正在上班途中的市民数</summary>
        public int GoingToWorkCount { get; private set; }
        /// <summary>正在回家途中的市民数</summary>
        public int GoingHomeCount { get; private set; }

        // --- Phase 4: 通勤者 ---

        /// <summary>外来通勤者家庭数</summary>
        public int CommuterCount { get; private set; }

        #endregion

        #region Lifecycle

        protected override void OnCreate()
        {
            base.OnCreate();

            // === 所有有效家庭（排除游客、通勤者） ===
            m_AllHouseholdQuery = GetEntityQuery(
                ComponentType.ReadOnly<Household>(),
                ComponentType.ReadOnly<HouseholdCitizen>(),
                ComponentType.Exclude<TouristHousehold>(),
                ComponentType.Exclude<CommuterHousehold>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // === 有租约的家庭 ===
            m_RenterQuery = GetEntityQuery(
                ComponentType.ReadOnly<Household>(),
                ComponentType.ReadOnly<PropertyRenter>(),
                ComponentType.ReadOnly<HouseholdCitizen>(),
                ComponentType.Exclude<TouristHousehold>(),
                ComponentType.Exclude<CommuterHousehold>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // === 无家可归的家庭 ===
            m_HomelessQuery = GetEntityQuery(
                ComponentType.ReadOnly<HomelessHousehold>(),
                ComponentType.ReadOnly<Household>(),
                ComponentType.Exclude<MovingAway>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // === 正在搬离的家庭 ===
            m_MovingAwayQuery = GetEntityQuery(
                ComponentType.ReadOnly<MovingAway>(),
                ComponentType.ReadOnly<Household>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // === 找房中（有房） ===
            m_PropertySeekerHousedQuery = GetEntityQuery(
                ComponentType.ReadOnly<PropertySeeker>(),
                ComponentType.ReadOnly<Household>(),
                ComponentType.ReadOnly<PropertyRenter>(),
                ComponentType.Exclude<HomelessHousehold>(),
                ComponentType.Exclude<MovingAway>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // === 找房中（流浪） ===
            m_PropertySeekerHomelessQuery = GetEntityQuery(
                ComponentType.ReadOnly<PropertySeeker>(),
                ComponentType.ReadOnly<HomelessHousehold>(),
                ComponentType.Exclude<MovingAway>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // === 高租金建筑候选（需 Chunk 遍历检查 BuildingFlags） ===
            m_HighRentBuildingQuery = GetEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<Renter>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // === 宠物实体 ===
            m_PetQuery = GetEntityQuery(
                ComponentType.ReadOnly<HouseholdPet>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // === Phase 4: 通勤者查询 ===
            m_CommuterQuery = GetEntityQuery(
                ComponentType.ReadOnly<CommuterHousehold>(),
                ComponentType.ReadOnly<HouseholdCitizen>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // === Phase 4: 获取游戏原生统计系统引用 ===
            m_CountResPropertySystem = World.GetOrCreateSystemManaged<CountResidentialPropertySystem>();
            m_CountCompanySystem = World.GetOrCreateSystemManaged<CountCompanyDataSystem>();
            m_PurposeCounterSystem = World.GetOrCreateSystemManaged<ResidentPurposeCounterSystem>();

            // --- 反射获取 ResidentPurposeCounterSystem 的私有 m_Results 字段 ---
            try
            {
                var field = typeof(ResidentPurposeCounterSystem)
                    .GetField("m_Results", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    m_PurposeResults = (NativeArray<int>)field.GetValue(m_PurposeCounterSystem);
                    m_PurposeResultsValid = m_PurposeResults.IsCreated && m_PurposeResults.Length >= 12;
                    ModLog.Ok(Tag, $"ResidentPurposeCounterSystem.m_Results 反射成功 (Length={m_PurposeResults.Length})");
                }
                else
                {
                    ModLog.Warn(Tag, "ResidentPurposeCounterSystem.m_Results 字段未找到");
                }
            }
            catch (System.Exception ex)
            {
                ModLog.Error(Tag, $"反射 ResidentPurposeCounterSystem 失败: {ex.Message}");
            }

            // 默认关闭，由 MapExtUISystem 在 Dashboard 展开时启用
            Enabled = false;

            ModLog.Ok(Tag, "城市统计系统已创建 (Phase 4: 按需启用, UpdateInterval=256)");
        }

        /// <summary>
        /// 限制更新频率：每 256 帧执行一次（约 4.3 秒 @60fps）。
        /// 仅在 Enabled=true 时生效。
        /// </summary>
        public override int GetUpdateInterval(SystemUpdatePhase phase)
            => 256;

        protected override void OnUpdate()
        {
            // === 快速计数（O(1) archetype 统计） ===
            TotalHouseholds = m_AllHouseholdQuery.CalculateEntityCount();
            RentedHouseholds = m_RenterQuery.CalculateEntityCount();
            HomelessCount = m_HomelessQuery.CalculateEntityCount();
            MovingAwayCount = m_MovingAwayQuery.CalculateEntityCount();
            SeekerHousedCount = m_PropertySeekerHousedQuery.CalculateEntityCount();
            SeekerHomelessCount = m_PropertySeekerHomelessQuery.CalculateEntityCount();
            PetCount = m_PetQuery.CalculateEntityCount();

            // === 高租金建筑需要遍历 Chunk 检查 BuildingFlags ===
            HighRentBuildingCount = CountHighRentBuildings();

            // === Phase 4: 通勤者（O(1) archetype 计数） ===
            CommuterCount = m_CommuterQuery.CalculateEntityCount();

            // === Phase 4: 住宅空置率（从缓存读取，零成本） ===
            ReadResidentialVacancy();

            // === Phase 4: 商业活动（从缓存读取，零成本） ===
            ReadCommercialData();

            // === Phase 4: 人口活动（从缓存读取，零成本） ===
            ReadPurposeCounterData();
        }

        /// <summary>
        /// 联动启用/禁用 ResidentPurposeCounterSystem。
        /// 由 MapExtUISystem 的 Dashboard 折叠回调间接调用（通过设置 Enabled）。
        /// </summary>
        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            if (m_PurposeCounterSystem != null)
                m_PurposeCounterSystem.Enabled = true;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            if (m_PurposeCounterSystem != null)
                m_PurposeCounterSystem.Enabled = false;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 统计带有 HighRentWarning 标志的建筑数量。
        /// HighRentWarning 是 BuildingFlags 的 flag 位，无法直接用 EntityQuery 过滤。
        /// </summary>
        private int CountHighRentBuildings()
        {
            int count = 0;
            var chunks = m_HighRentBuildingQuery.ToArchetypeChunkArray(Allocator.TempJob);
            var buildingHandle = SystemAPI.GetComponentTypeHandle<Building>(isReadOnly: true);

            for (int c = 0; c < chunks.Length; c++)
            {
                var buildings = chunks[c].GetNativeArray(ref buildingHandle);
                for (int i = 0; i < buildings.Length; i++)
                {
                    if ((buildings[i].m_Flags & BuildingFlags.HighRentWarning) != 0)
                    {
                        count++;
                    }
                }
            }

            chunks.Dispose();
            return count;
        }

        /// <summary>
        /// Phase 4: 从 CountResidentialPropertySystem 读取住宅空置缓存。
        /// int3.x = Low, .y = Medium, .z = High
        /// </summary>
        private void ReadResidentialVacancy()
        {
            if (m_CountResPropertySystem == null) return;
            var data = m_CountResPropertySystem.GetResidentialPropertyData();
            FreeResLow = data.m_FreeProperties.x;
            FreeResMed = data.m_FreeProperties.y;
            FreeResHigh = data.m_FreeProperties.z;
            TotalResLow = data.m_TotalProperties.x;
            TotalResMed = data.m_TotalProperties.y;
            TotalResHigh = data.m_TotalProperties.z;
        }

        /// <summary>
        /// Phase 4: 从 CountCompanyDataSystem 读取商业公司缓存。
        /// 汇总所有资源类型的 ServiceCompanies 和 ServicePropertyless。
        /// </summary>
        private void ReadCommercialData()
        {
            if (m_CountCompanySystem == null) return;
            var comData = m_CountCompanySystem.GetCommercialCompanyDatas(out JobHandle deps);
            deps.Complete();

            int totalSvc = 0;
            int totalPropertyless = 0;
            for (int i = 0; i < comData.m_ServiceCompanies.Length; i++)
            {
                totalSvc += comData.m_ServiceCompanies[i];
                totalPropertyless += comData.m_ServicePropertyless[i];
            }

            TotalCommercial = totalSvc;
            CommercialPropertyless = totalPropertyless;
        }

        /// <summary>
        /// Phase 4: 从 ResidentPurposeCounterSystem 的 m_Results 读取人口活动缓存。
        /// 索引对应 CountPurpose 枚举：0=GoingHome, 2=GoingToWork, 3=Leisure, 5=Shopping
        /// </summary>
        private void ReadPurposeCounterData()
        {
            if (!m_PurposeResultsValid) return;
            GoingHomeCount = m_PurposeResults[0];
            GoingToWorkCount = m_PurposeResults[2];
            LeisureCount = m_PurposeResults[3];
            ShoppingCount = m_PurposeResults[5];
        }

        #endregion
    }
}
