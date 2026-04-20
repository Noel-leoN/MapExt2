// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Economy;
using Game.Simulation;
using Game.Tools;
using MapExtPDX.MapExt.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace MapExtPDX.EcoShared
{
    /// <summary>
    /// 🔍 [MOD] 人口诊断系统
    /// 轻量级托管系统，不参与每帧更新。
    /// 仅在用户点击 OptionUI 刷新按钮时执行 ECS 查询，
    /// 统计关键人口健康度指标并格式化为诊断报告。
    /// </summary>
    public partial class PopulationDiagnosticSystem : GameSystemBase
    {
        #region Constants

        private const string Tag = "PopDiag";

        #endregion

        #region Fields

        private EntityQuery m_MovingAwayQuery;
        private EntityQuery m_HomelessQuery;
        private EntityQuery m_PropertySeekerHousedQuery;
        private EntityQuery m_PropertySeekerHomelessQuery;
        private EntityQuery m_HighRentBuildingQuery;
        private EntityQuery m_AllHouseholdQuery;
        private EntityQuery m_RenterQuery;

        #endregion

        #region Lifecycle

        protected override void OnCreate()
        {
            base.OnCreate();

            // 正在离开城市的家庭
            m_MovingAwayQuery = GetEntityQuery(
                ComponentType.ReadOnly<MovingAway>(),
                ComponentType.ReadOnly<Household>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // 无家可归的家庭
            m_HomelessQuery = GetEntityQuery(
                ComponentType.ReadOnly<HomelessHousehold>(),
                ComponentType.ReadOnly<Household>(),
                ComponentType.Exclude<MovingAway>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // 正在找房的已有住房家庭（被驱逐或改善型搬迁）
            m_PropertySeekerHousedQuery = GetEntityQuery(
                ComponentType.ReadOnly<PropertySeeker>(),
                ComponentType.ReadOnly<Household>(),
                ComponentType.ReadOnly<PropertyRenter>(),
                ComponentType.Exclude<HomelessHousehold>(),
                ComponentType.Exclude<MovingAway>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // 正在找房的无家可归家庭
            m_PropertySeekerHomelessQuery = GetEntityQuery(
                ComponentType.ReadOnly<PropertySeeker>(),
                ComponentType.ReadOnly<HomelessHousehold>(),
                ComponentType.Exclude<MovingAway>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // 高租金警告建筑
            m_HighRentBuildingQuery = GetEntityQuery(
                ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<Renter>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // 所有有效家庭（用于总数参考）
            m_AllHouseholdQuery = GetEntityQuery(
                ComponentType.ReadOnly<Household>(),
                ComponentType.ReadOnly<HouseholdCitizen>(),
                ComponentType.Exclude<TouristHousehold>(),
                ComponentType.Exclude<CommuterHousehold>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // 有租约的家庭（用于计算租房比例）
            m_RenterQuery = GetEntityQuery(
                ComponentType.ReadOnly<Household>(),
                ComponentType.ReadOnly<PropertyRenter>(),
                ComponentType.ReadOnly<HouseholdCitizen>(),
                ComponentType.Exclude<TouristHousehold>(),
                ComponentType.Exclude<CommuterHousehold>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            // 此系统不需要自动更新
            Enabled = false;

            ModLog.Ok(Tag, "人口诊断系统已创建 (按需查询模式)");
        }

        protected override void OnUpdate()
        {
            // 此系统不参与常规更新循环
        }

        #endregion

        #region Public API

        /// <summary>
        /// 执行一次诊断查询并返回格式化的报告字符串。
        /// 由 ModSettings 的刷新按钮触发调用。
        /// </summary>
        public string RunDiagnostics()
        {
            // === 基础计数 ===
            int movingAwayCount = m_MovingAwayQuery.CalculateEntityCount();
            int homelessCount = m_HomelessQuery.CalculateEntityCount();
            int seekerHousedCount = m_PropertySeekerHousedQuery.CalculateEntityCount();
            int seekerHomelessCount = m_PropertySeekerHomelessQuery.CalculateEntityCount();
            int totalHouseholds = m_AllHouseholdQuery.CalculateEntityCount();
            int rentedHouseholds = m_RenterQuery.CalculateEntityCount();

            // === 高租金建筑计数 ===
            int highRentCount = CountHighRentBuildings();

            // === MovingAway 细分统计 ===
            AnalyzeMovingAway(out int maWithHome, out int maWithoutHome,
                out int maCitizenTotal, out int maWithOC,
                out int maSingleMember, out int maMultiMember);

            // === 计算比率 ===
            float movingAwayRatio = totalHouseholds > 0
                ? (float)movingAwayCount / totalHouseholds * 100f
                : 0f;
            float housingRate = totalHouseholds > 0
                ? (float)rentedHouseholds / totalHouseholds * 100f
                : 0f;

            // === 格式化报告 ===
            string report =
                $"Households: {totalHouseholds} | Housed: {rentedHouseholds} ({housingRate:F1}%)\n" +
                $"MovingAway: {movingAwayCount} ({movingAwayRatio:F1}%) | Homeless: {homelessCount}\n" +
                $"Seekers: {seekerHousedCount} (housed) + {seekerHomelessCount} (homeless)\n" +
                $"HighRent Buildings: {highRentCount}\n" +
                $"--- MovingAway Detail ---\n" +
                $"  StillHoused: {maWithHome} | Evicted: {maWithoutHome}\n" +
                $"  Citizens: {maCitizenTotal} | ReachedOC: {maWithOC}\n" +
                $"  Single: {maSingleMember} | Family: {maMultiMember}";

            ModLog.Scan(Tag,
                $"诊断完成 | 家庭: {totalHouseholds}, 有房: {rentedHouseholds}, " +
                $"搬离: {movingAwayCount}, 流浪: {homelessCount}, " +
                $"找房(有房): {seekerHousedCount}, 找房(流浪): {seekerHomelessCount}, " +
                $"高租金: {highRentCount}");

            ModLog.Scan(Tag,
                $"搬离细分 | 仍有房: {maWithHome}, 已失房: {maWithoutHome}, " +
                $"涉及市民: {maCitizenTotal}, 已到OC: {maWithOC}, " +
                $"单人户: {maSingleMember}, 多人户: {maMultiMember}");

            return report;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 统计带有 HighRentWarning 标志的建筑数量。
        /// 需要遍历 Chunk 检查 BuildingFlags，因为 HighRentWarning 是 flag 而非独立 component。
        /// </summary>
        private int CountHighRentBuildings()
        {
            int count = 0;
            var chunks = m_HighRentBuildingQuery.ToArchetypeChunkArray(Allocator.Temp);
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
        /// 对 MovingAway 家庭进行细分分析：
        /// - 是否仍有住房 (PropertyRenter)
        /// - 涉及市民总数及年龄分布
        /// - 是否已到达 OutsideConnection (CitizenFlags.MovingAwayReachOC)
        /// - 单人户 vs 多人户
        /// </summary>
        private void AnalyzeMovingAway(
            out int withHome, out int withoutHome,
            out int citizenTotal, out int reachedOC,
            out int singleMember, out int multiMember)
        {
            withHome = 0;
            withoutHome = 0;
            citizenTotal = 0;
            reachedOC = 0;
            singleMember = 0;
            multiMember = 0;

            var entities = m_MovingAwayQuery.ToEntityArray(Allocator.Temp);
            var propertyRenterLookup = SystemAPI.GetComponentLookup<PropertyRenter>(isReadOnly: true);
            var householdCitizenLookup = SystemAPI.GetBufferLookup<HouseholdCitizen>(isReadOnly: true);
            var citizenLookup = SystemAPI.GetComponentLookup<Citizen>(isReadOnly: true);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];

                // --- 住房状态 ---
                if (propertyRenterLookup.HasComponent(entity))
                    withHome++;
                else
                    withoutHome++;

                // --- 成员分析 ---
                if (householdCitizenLookup.HasBuffer(entity))
                {
                    var members = householdCitizenLookup[entity];
                    int memberCount = members.Length;
                    citizenTotal += memberCount;

                    if (memberCount <= 1)
                        singleMember++;
                    else
                        multiMember++;

                    // 检查是否有成员已到达 OutsideConnection
                    for (int m = 0; m < members.Length; m++)
                    {
                        if (citizenLookup.TryGetComponent(members[m].m_Citizen, out var citizen))
                        {
                            if ((citizen.m_State & CitizenFlags.MovingAwayReachOC) != 0)
                            {
                                reachedOC++;
                                break; // 只要有一个成员到达OC即计数一次
                            }
                        }
                    }
                }
                else
                {
                    // 无成员 buffer = 空家庭（异常状态）
                    singleMember++;
                }
            }

            entities.Dispose();
        }

        #endregion
    }
}
