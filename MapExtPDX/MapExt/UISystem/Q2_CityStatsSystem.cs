// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using Colossal.Collections;
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
using Unity.Burst;
using Unity.Burst.Intrinsics;
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

        /// <summary>
        /// <see cref="ReadCommercialData"/> 連續因 job 未完成而跳過的上限，達到即退回一次同步等待。
        /// 4 輪約 17 秒，只在相位關係出乎預期時才會走到。
        /// </summary>
        private const int kMaxCommercialSkips = 4;

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

        /// <summary>
        /// 高租金建築計數的累加器（Persistent）。
        /// 常態路徑由 <see cref="CountHighRentJob"/> 平行寫入，OnUpdate 開頭讀上一輪結果，
        /// 對齊原版 Count* 系統（如 CountResidentialPropertySystem）的「讀上一輪 → 清 → 排本輪」模式。
        /// </summary>
        private NativeAccumulator<HighRentData> m_HighRentAccumulator;

        /// <summary>
        /// OnStartRunning 剛同步算過首屏值時置位：下一次 OnUpdate 不要用剛清空的累加器覆寫它。
        /// 沒有這個標記的話首屏值會在第一次 OnUpdate 立刻被 0 抹掉。
        /// </summary>
        private bool m_SkipNextHighRentRead;

        /// <summary>
        /// 最近一次 <see cref="CountHighRentJob"/> 的 handle。
        ///
        /// <para><b>為何不用 <c>Dependency</c> 代替</b>：<c>SystemBase.Dependency</c> 的 getter 是
        /// 由 dependency manager 依本系統的讀寫型別重算的，而該 job 對 <c>Building</c> 只是
        /// <b>reader</b>（reader 之間互不等待），因此無法從契約層保證 getter 回傳的 handle
        /// 一定包含剛排下去的這個 job。系統 disable 後 ECS 也不再追蹤 <c>Dependency</c>，
        /// 而 job 仍在寫 Persistent 累加器 —— 顯式持有才能精確等它，且等待範圍從
        /// 「本系統全部型別依賴」收窄到單一 job，比 <c>Dependency.Complete()</c> 更便宜。</para>
        /// </summary>
        private JobHandle m_HighRentHandle;

        /// <summary><see cref="ReadCommercialData"/> 連續跳過的輪數，見 <see cref="kMaxCommercialSkips"/>。</summary>
        private int m_CommercialSkips;

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

            // === 高租金建築計數累加器（S1：把主執行緒掃描搬進 Job） ===
            m_HighRentAccumulator = new NativeAccumulator<HighRentData>(Allocator.Persistent);

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

        /// <summary>
        /// 固定相位，避開 <c>CountCompanyDataSystem</c>（interval 16、offset 1，同在 GameSimulation）。
        /// <para><c>UpdateSystem</c> 的判定是 <c>updateIndex &amp; (interval-1) == offset</c>；
        /// 256 是 16 的倍數，所以兩系統是否同幀由 offset 一次決定、之後永不改變。
        /// 若交給自動分配而低 4 位恰為 1，本系統每輪都會在對方剛排完 job 的同一幀執行，
        /// <see cref="ReadCommercialData"/> 的 <c>IsCompleted</c> 門檻永遠不成立，
        /// 商業兩項數值就靜默凍結在初值。取 8：低 4 位不為 1，且落在對方兩次執行的中點，
        /// 對方 7 幀前排下的 job 此時早已完成。</para>
        /// </summary>
        public override int GetUpdateOffset(SystemUpdatePhase phase)
            => 8;

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

            // === 高租金建築：讀上一輪 Job 結果 → 清 → 排本輪（S1） ===
            // HighRentWarning 是 BuildingFlags 的位元而非獨立 component，無法用 EntityQuery 過濾，
            // 只能逐 entity 檢查；但沒有理由佔用主執行緒。結果延遲一輪（256 幀），
            // 首屏由 OnStartRunning 的同步計算補上。
            // 讀結果與 Clear() 之前先等上一輪的 job：它 256 幀前就排下去，此刻早該結束，
            // Complete() 只清 safety handle；但不能依賴 Dependency 含有它（理由見 m_HighRentHandle）。
            m_HighRentHandle.Complete();
            if (m_SkipNextHighRentRead)
            {
                // 本輪的累加器是 OnStartRunning 剛清空的，讀它只會得到 0；保留首屏同步值。
                m_SkipNextHighRentRead = false;
            }
            else
            {
                HighRentBuildingCount = m_HighRentAccumulator.GetResult().m_Count;
            }

            m_HighRentAccumulator.Clear();
            m_HighRentHandle = new CountHighRentJob
            {
                m_BuildingType = SystemAPI.GetComponentTypeHandle<Building>(isReadOnly: true),
                m_Result = m_HighRentAccumulator.AsParallelWriter(),
            }.ScheduleParallel(m_HighRentBuildingQuery, Dependency);
            Dependency = m_HighRentHandle;

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

            // 面板剛展開時同步算一次，讓首屏就有正確值（累加器是 Persistent，
            // 不清的話會顯示上次關閉時的 stale 值）。這是使用者主動觸發的單次操作，
            // 與 Q1_PopulationDiagnosticSystem 的診斷按鈕同性質，可接受主執行緒成本。
            //
            // Clear() 之前先確保上一輪的 job 已結束：正常路徑 OnStopRunning 已等過，
            // 但系統若在同一幀被 disable→enable，這裡是最後一道防線（已完成時為零成本）。
            m_HighRentHandle.Complete();
            m_HighRentAccumulator.Clear();
            HighRentBuildingCount = CountHighRentBuildingsSync();
            m_SkipNextHighRentRead = true;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            if (m_PurposeCounterSystem != null)
                m_PurposeCounterSystem.Enabled = false;

            // 系統停止後 ECS 不再追蹤本系統的 Dependency，但已排程的 CountHighRentJob
            // 仍會寫入 Persistent 累加器；若不等它結束，下次 OnStartRunning 的 Clear()
            // 會與它競態。這是面板收起時的一次性同步，成本可忽略。
            // 等的是自己持有的 handle 而非 Dependency —— 理由見 m_HighRentHandle 的註釋。
            m_HighRentHandle.Complete();
        }

        protected override void OnDestroy()
        {
            // Dispose 之前必須等 job 結束。World.Dispose() 雖有 CompleteAllTrackedJobs()，
            // 但單獨銷毀本系統（非整個 World）不走那條路徑，故自己等一次。
            m_HighRentHandle.Complete();
            if (m_HighRentAccumulator.IsCreated)
                m_HighRentAccumulator.Dispose();
            base.OnDestroy();
        }

        #endregion

        #region Jobs

        /// <summary>高租金建築計數的累加載體（對齊原版 Count* 系統的 NativeAccumulator 慣例）。</summary>
        private struct HighRentData : IAccumulable<HighRentData>
        {
            public int m_Count;

            public void Accumulate(HighRentData other) => m_Count += other.m_Count;
        }

        /// <summary>
        /// 統計帶 HighRentWarning 標誌的建築數（S1）。
        /// 該標誌是 BuildingFlags 的位元而非獨立 component，無法用 EntityQuery 過濾，
        /// 只能逐 entity 檢查——但可以平行化，不必佔用主執行緒。
        /// </summary>
        [BurstCompile]
        private struct CountHighRentJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<Building> m_BuildingType;

            public NativeAccumulator<HighRentData>.ParallelWriter m_Result;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                                bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Building> buildings = chunk.GetNativeArray(ref m_BuildingType);
                int count = 0;

                for (int i = 0; i < buildings.Length; i++)
                {
                    if ((buildings[i].m_Flags & BuildingFlags.HighRentWarning) != 0)
                    {
                        count++;
                    }
                }

                // 每 chunk 只彙總一次，避免 per-entity 的 accumulator 開銷
                if (count != 0)
                {
                    m_Result.Accumulate(new HighRentData { m_Count = count });
                }
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 统计带有 HighRentWarning 标志的建筑数量（同步版）。
        /// HighRentWarning 是 BuildingFlags 的 flag 位，无法直接用 EntityQuery 过滤。
        /// <para>
        /// <b>僅供 <see cref="OnStartRunning"/> 的首屏使用</b>——常態路徑走
        /// <see cref="CountHighRentJob"/>。此處的 <c>ToArchetypeChunkArray</c> 是同步版，
        /// 會 complete 該 query 的全部 job 依賴，不可放進 OnUpdate。
        /// </para>
        /// </summary>
        private int CountHighRentBuildingsSync()
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

            // S2：不阻塞主執行緒。deps 是 CountCompanyDataSystem 的
            // CountCompanyDataJob → 單執行緒 SumJob 整條鏈，未完成就跳過本輪、保留上一輪的值
            // （256 幀後會再試）。GetUpdateOffset 已把本系統固定在對方排 job 之後 7 幀，
            // 常態下這裡總是已完成；連續跳過上限是防止任何未預期的相位關係讓數值永久凍結的保險，
            // 觸發時退回一次同步等待（即改造前的行為），不會靜默失效。
            if (!deps.IsCompleted && ++m_CommercialSkips < kMaxCommercialSkips) return;
            m_CommercialSkips = 0;

            // 已完成時 Complete() 僅清理 safety handle，無實際等待；Unity 要求讀 NativeArray 前必須呼叫。
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
