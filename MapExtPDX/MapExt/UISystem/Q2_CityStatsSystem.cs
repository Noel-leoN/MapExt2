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
using Unity.Collections;
using Unity.Entities;

namespace MapExtPDX.UI
{
    /// <summary>
    /// 📊 [MOD] 城市统计数据收集系统（Phase 2）
    /// 按需运行：仅当 Dashboard 面板展开时由 MapExtUISystem 启用。
    /// 每 256 帧（约 4.3 秒）执行一次 ECS 查询，收集人口健康度指标。
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

            // 默认关闭，由 MapExtUISystem 在 Dashboard 展开时启用
            Enabled = false;

            ModLog.Ok(Tag, "城市统计系统已创建 (按需启用, UpdateInterval=256)");
        }

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

        #endregion
    }
}
