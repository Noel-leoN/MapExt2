// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.

using Colossal.Serialization.Entities; // Purpose（OnGamePreload 簽名）
using Game;
using Game.Buildings;
using Game.Common;
using Game.Objects;

using Game.Tools;
using Game.Vehicles;
using MapExtPDX.MapExt.Core;
using System;
using System.IO;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace MapExtPDX.EcoShared
{
    /// <summary>
    /// 修复原版市民在商铺购车后因停车位不足导致车辆丢失的Bug。
    ///
    /// 原版流程：ResourceBuyerSystem 在商铺创建车辆(stopped=true) → InitializeSystem.FindParkingSpace()
    /// 在商铺附近查找车位 → 若失败，ParkedCar.m_Lane 保持 Entity.Null，无任何系统兜底。
    ///
    /// 修复策略：检测 m_Lane==Null 的新购私家车，传送到车主住宅附近，
    /// 利用原版 FixParkingLocation 的 m_ResetLocation 机制让原版系统以住宅为中心重新查找车位。
    /// 低频运行（每64帧一次），配合重试上限防止无限循环。
    ///
    /// 【時序】本系統掛在 <c>GameSimulation</c>，與原版所有 <c>FixParkingLocation</c> 生產者一致
    /// （<c>PersonalCarAISystem</c> 等，皆為 GameSimulation）。一個 Unity 幀的實際順序是
    /// <c>MainLoop</c>（含 Modification1–5）→ <c>PrepareCleanUpSystem</c> 快照 →
    /// <c>Cleanup</c> 移除旗標 → <c>LateUpdate</c>（GameSimulation ×N），
    /// 因此本系統在 GameSimulation 掛上的 <c>Updated</c> 比當幀快照更新、不會被當幀清掉，
    /// 必然完整存活到下一幀 Modification5 的 <c>FixParkingLocationSystem</c>，且只被消化一次。
    /// 反之若改掛 Modification 系列，<c>ModificationSystem</c> 走的是不帶 updateIndex 的
    /// <c>UpdateSystem.Update(phase)</c> 多載，會**完全忽略** <see cref="GetUpdateInterval"/>，
    /// 使 64 幀節流退化成每渲染幀一次、<see cref="kMaxRetries"/> 預算從約 512 模擬幀縮成約 8 幀。
    ///
    /// 【设计】追蹤表只存在記憶體中（<see cref="m_RescuedVehicles"/>），不注入任何自定义 ECS 组件到实体上，
    /// 确保存档零污染，禁用 Mod 后不留痕迹。
    ///
    /// 【2026-08-11 加固】三項變更，起因為某 78 萬人口舊存檔在啟用本系統時
    /// 於 <c>Serialize</c> 階段拋 <c>NullReferenceException</c>（`EntityManager.HighestEntityIndex`），
    /// 關閉本系統後存檔恢復正常：
    /// ① 移除全部 <c>EntityManager</c> 同步直寫（原 <c>CleanupOwnership</c> 直改
    ///    <c>OwnedVehicle</c> buffer、<c>ClearCarKeeper</c> 直寫 <c>CarKeeper</c>）——
    ///    這兩件事原版 <c>Game.Vehicles.ReferencesSystem</c> 的 Deleted 分支本就會做，屬重複勞動；
    /// ② 首次救援加單幀配額 <see cref="kMaxRescuePerRun"/>，避免載入時一次動上千輛；
    /// ③ <c>OnGamePreload</c> 清空追蹤表，避免跨存檔的 Entity 索引重用誤傷。
    ///
    /// 【2026-08-19 Job 化】原本整個掃描與指令建構都在主執行緒，且以
    /// <c>ecb.Playback(EntityManager)</c> 當場回放——後者會在模擬迴圈中間製造
    /// 結構性變更同步點（原版同位置一律交給 barrier）。本次改為：
    /// ① 追蹤表 <c>Dictionary&lt;Entity,int&gt;</c> → <c>NativeHashMap&lt;Entity,int&gt;</c>（Persistent）；
    /// ② 重試與首次救援各自成為 Burst job（<see cref="RetryJob"/>／<see cref="RescueScanJob"/>），
    ///    以單執行緒 <c>Schedule</c> 串接，保留「重試必須先於首次救援」的順序與配額累積語意；
    /// ③ 指令改由 <c>EndFrameBarrier</c> 回放——它註冊在 MainLoop 的 <c>UpdateBefore</c>，
    ///    回放點在下一幀 MainLoop 開頭，仍早於 Modification5，可見性窗口與原本完全相同；
    /// ④ 統計改寫入 <see cref="m_Stats"/>，只在除錯日誌開啟時才 Complete 讀取，
    ///    因此關閉日誌（預設）時本系統不再產生任何主執行緒同步點；
    /// ⑤ 移除原 <c>CleanupStaleEntries</c>——它的條件（不在 Query 且實體已不存在）
    ///    是 <c>ProcessRetry</c> 存在性檢查的真子集，在同一次 OnUpdate 中必然已被清掉，屬死碼。
    /// </summary>
    public partial class P3_VehiclePurchaseRescueSystem : GameSystemBase
    {
        #region Constants and Fields

        private const string Tag = "P3_VehiclePurchaseRescue";

        /// <summary>
        /// 低频更新间隔：每 64 模拟帧执行一次。
        /// 买车是低频事件，无需每帧扫描。
        /// </summary>
        private const int kUpdateInterval = 64;

        /// <summary>
        /// 最大重试次数。超过后删除僵尸车辆。
        /// 每次重试间隔约 64 帧，8 次 ≈ 512 帧 ≈ 游戏内约 0.5 小时，足够等待车位空出。
        /// </summary>
        private const int kMaxRetries = 8;

        /// <summary>
        /// 單次執行的「首次救援」上限。
        ///
        /// 舊存檔可能累積上千輛存量幽靈車（實測某 78 萬人口存檔載入後一次性命中 1334 輛），
        /// 若不設限則會在單幀內對它們全部下 SetComponent + AddComponent×2，
        /// 令原版 FixParkingLocationSystem 在同一幀面對上千個搜尋請求。
        /// 分批攤平：每 64 模擬幀處理 64 輛，1334 輛約需 21 輪（≈1344 模擬幀）完成，
        /// 對玩家無感，但把單幀結構性變更量壓回個位數量級。
        /// 重試路徑不受此限——它本就受 <see cref="kMaxRetries"/> 與追蹤表規模自然約束。
        /// </summary>
        private const int kMaxRescuePerRun = 64;

        // --- m_Stats 槽位索引（job 只能寫 NativeArray，故以固定槽位代替多個計數器）---
        private const int kStatSuccess = 0;   // 重試成功停放
        private const int kStatRetry = 1;     // 本輪重新掛 FixParkingLocation
        private const int kStatAbandon = 2;   // 超過重試上限／無家而刪除
        private const int kStatRescue = 3;    // 首次救援
        private const int kStatOrphan = 4;    // 孤兒車刪除
        private const int kStatDeferred = 5;  // 被配額攔下的積壓量
        private const int kStatCount = 6;

        // === 候选车辆 Query：检测所有可能需要救援的新购车辆 ===
        private EntityQuery m_CandidateQuery;

        /// <summary>
        /// 指令回放交給原版 <c>EndFrameBarrier</c>：它註冊為 MainLoop 的 <c>UpdateBefore</c>，
        /// 回放點在下一幀 MainLoop 開頭，早於 <c>ModificationSystem</c>，
        /// 因此 Modification5 的 FixParkingLocationSystem 照樣能在同一幀看到指令結果。
        /// 這也是原版所有 <c>FixParkingLocation</c> 生產者採用的 barrier。
        /// </summary>
        private EndFrameBarrier m_EndFrameBarrier;

        // === 純記憶體追踪：已救援车辆 → 重试计数 ===
        // 不注入任何自定义 Component，存档零污染；改用 Native 容器以便在 Burst job 內讀寫。
        private NativeHashMap<Entity, int> m_RescuedVehicles;

        /// <summary>本輪統計槽位，僅在除錯日誌開啟時才 Complete 讀取。</summary>
        private NativeArray<int> m_Stats;

        /// <summary>首次救援被上限攔下的累計輛數，僅用於節流日誌。</summary>
        private int m_DeferredRescueCount;

        // === 文件日志路径（延迟初始化） ===
        private string m_LogFilePath;

        // === Job 用 TypeHandle 與 ComponentLookup ===
        private EntityTypeHandle m_EntityType;
        private ComponentTypeHandle<ParkedCar> m_ParkedCarType;
        private ComponentTypeHandle<PersonalCar> m_PersonalCarType;
        private ComponentTypeHandle<Owner> m_OwnerType;

        private ComponentLookup<ParkedCar> m_ParkedCarLookup;
        private ComponentLookup<PersonalCar> m_PersonalCarLookup;
        private ComponentLookup<Owner> m_OwnerLookup;
        private ComponentLookup<PropertyRenter> m_PropertyRenterLookup;
        private ComponentLookup<Game.Objects.Transform> m_TransformLookup;
        private ComponentLookup<FixParkingLocation> m_FixParkingLookup;

        #endregion

        #region System Loop

        /// <summary>
        /// 低频更新：每 kUpdateInterval 帧执行一次。
        /// 注意：此節流只在帶 updateIndex 的 phase（GameSimulation／EditorSimulation／LoadSimulation）生效。
        /// </summary>
        public override int GetUpdateInterval(SystemUpdatePhase phase) => kUpdateInterval;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();

            // --- 候选 Query：所有可能需要救援的新购私家车 ---
            // ParkedCar + PersonalCar + Owner + Unspawned
            // 排除：Created（未初始化）、Deleted、Temp、FixParkingLocation（正在被原版处理）
            // 注意：不再排除 VehicleRescued（已移除该自定义组件），改用追蹤表过滤
            m_CandidateQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<ParkedCar>(),
                    ComponentType.ReadOnly<PersonalCar>(),
                    ComponentType.ReadOnly<Owner>(),
                    ComponentType.ReadOnly<Unspawned>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Created>(),
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<FixParkingLocation>()
                }
            });

            // 仅在有匹配实体时才激活本系统，避免空帧开销
            RequireForUpdate(m_CandidateQuery);

            // 追蹤表與統計槽位跨幀存活，須用 Persistent 並在 OnDestroy 釋放
            m_RescuedVehicles = new NativeHashMap<Entity, int>(256, Allocator.Persistent);
            m_Stats = new NativeArray<int>(kStatCount, Allocator.Persistent);

            m_EntityType = GetEntityTypeHandle();
            m_ParkedCarType = GetComponentTypeHandle<ParkedCar>(true);
            m_PersonalCarType = GetComponentTypeHandle<PersonalCar>(true);
            m_OwnerType = GetComponentTypeHandle<Owner>(true);

            m_ParkedCarLookup = GetComponentLookup<ParkedCar>(true);
            m_PersonalCarLookup = GetComponentLookup<PersonalCar>(true);
            m_OwnerLookup = GetComponentLookup<Owner>(true);
            m_PropertyRenterLookup = GetComponentLookup<PropertyRenter>(true);
            m_TransformLookup = GetComponentLookup<Game.Objects.Transform>(true);
            m_FixParkingLookup = GetComponentLookup<FixParkingLocation>(true);

            ModLog.Info(Tag, "购车救援系统已创建");
        }

        protected override void OnDestroy()
        {
            CompleteDependency();

            if (m_RescuedVehicles.IsCreated)
                m_RescuedVehicles.Dispose();
            if (m_Stats.IsCreated)
                m_Stats.Dispose();

            base.OnDestroy();
        }

        /// <summary>
        /// 存檔切換時清空追蹤表：Entity 索引會在新 World 中重用，
        /// 殘留的鍵會讓 RetryJob 對新存檔的無關實體下指令。
        /// </summary>
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            // 追蹤表由 job 讀寫，主執行緒清空前必須等待
            CompleteDependency();
            m_RescuedVehicles.Clear();
            m_DeferredRescueCount = 0;
        }

        protected override void OnUpdate()
        {
            // 主开关检查：未启用时不执行任何逻辑
            if (Mod.Instance?.Settings?.EnableVehicleRescue != true)
                return;

            bool debugLog = Mod.Instance?.Settings?.EnableRescueDebugLog == true;

            m_EntityType.Update(this);
            m_ParkedCarType.Update(this);
            m_PersonalCarType.Update(this);
            m_OwnerType.Update(this);

            m_ParkedCarLookup.Update(this);
            m_OwnerLookup.Update(this);
            m_PropertyRenterLookup.Update(this);
            m_TransformLookup.Update(this);
            m_FixParkingLookup.Update(this);

            // === 兩個階段各自一個 command buffer，回放順序 = 建立順序 ===
            // 全部指令走 barrier，本方法內不再出現任何 EntityManager 直寫或就地 Playback：
            // 前者會觸發隱式 job 同步點並令持有中的 lookup 失效，
            // 後者會在模擬迴圈中間製造結構性變更同步點（原版同位置一律交給 barrier）。
            var retryBuffer = m_EndFrameBarrier.CreateCommandBuffer();
            var rescueBuffer = m_EndFrameBarrier.CreateCommandBuffer();

            // === 阶段 1：对已救援但仍失败的车辆重试 ===
            // 注意：必須先於 RescueScanJob 執行——若順序顛倒，剛入追蹤表的新車會在同輪
            // 被立即重複處理（barrier 尚未回放，FixParkingLocation 檢查不到），
            // 導致重試計數虛增與重複指令。兩個 job 以單執行緒串接即可保證此序。
            JobHandle retryHandle = new RetryJob
            {
                m_RescuedVehicles = m_RescuedVehicles,
                m_ParkedCarData = m_ParkedCarLookup,
                m_OwnerData = m_OwnerLookup,
                m_PropertyRenterData = m_PropertyRenterLookup,
                m_FixParkingData = m_FixParkingLookup,
                m_CommandBuffer = retryBuffer,
                m_Stats = m_Stats
            }.Schedule(Dependency);

            // === 阶段 2：首次救援新车辆（受 kMaxRescuePerRun 分批）===
            // 以單執行緒 Schedule 而非 ScheduleParallel：配額累計與追蹤表寫入都需要序列語意，
            // 且 chunk 迭代順序固定，結果具決定性。
            JobHandle rescueHandle = new RescueScanJob
            {
                m_EntityType = m_EntityType,
                m_ParkedCarType = m_ParkedCarType,
                m_PersonalCarType = m_PersonalCarType,
                m_OwnerType = m_OwnerType,
                m_PropertyRenterData = m_PropertyRenterLookup,
                m_TransformData = m_TransformLookup,
                m_RescuedVehicles = m_RescuedVehicles,
                m_CommandBuffer = rescueBuffer,
                m_Stats = m_Stats
            }.Schedule(m_CandidateQuery, retryHandle);

            m_EndFrameBarrier.AddJobHandleForProducer(rescueHandle);
            Dependency = rescueHandle;

            // 只有開啟除錯日誌時才需要讀統計——關閉時（預設）本系統零同步點
            if (debugLog)
            {
                rescueHandle.Complete();
                EmitDebugLog();
            }
        }

        #endregion

        #region Jobs

        /// <summary>
        /// 阶段 1：对已救援但住宅附近仍无车位的车辆，重新挂 FixParkingLocation 持续重试；
        /// 超过最大重试次数或车主已无住宅则删除僵尸车辆。
        ///
        /// 兼負全部 <see cref="m_Stats"/> 歸零之責（本 job 必為每輪第一個執行者）。
        /// </summary>
        [BurstCompile]
        private struct RetryJob : IJob
        {
            public NativeHashMap<Entity, int> m_RescuedVehicles;

            [ReadOnly] public ComponentLookup<ParkedCar> m_ParkedCarData;
            [ReadOnly] public ComponentLookup<Owner> m_OwnerData;
            [ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenterData;
            [ReadOnly] public ComponentLookup<FixParkingLocation> m_FixParkingData;

            public EntityCommandBuffer m_CommandBuffer;
            public NativeArray<int> m_Stats;

            public void Execute()
            {
                for (int s = 0; s < m_Stats.Length; s++)
                    m_Stats[s] = 0;

                if (m_RescuedVehicles.IsEmpty)
                    return;

                // 先取出 Key 快照，避免遍歷中改動追蹤表
                var keys = m_RescuedVehicles.GetKeyArray(Allocator.Temp);

                for (int i = 0; i < keys.Length; i++)
                {
                    Entity vehicle = keys[i];
                    if (!m_RescuedVehicles.TryGetValue(vehicle, out int currentRetry))
                        continue;

                    // 实体已被销毁或不再是停放车 → 清理
                    // 存在性一律走 lookup，不用 EntityManager（避免隱式同步點）
                    if (!m_ParkedCarData.HasComponent(vehicle))
                    {
                        m_RescuedVehicles.Remove(vehicle);
                        continue;
                    }

                    // FixParkingLocation 仍挂着 → 原版系统还没处理完，跳过
                    if (m_FixParkingData.HasComponent(vehicle))
                        continue;

                    // 已成功停放 → 从追踪表移除，完成救援
                    if (m_ParkedCarData[vehicle].m_Lane != Entity.Null)
                    {
                        m_RescuedVehicles.Remove(vehicle);
                        m_Stats[kStatSuccess]++;
                        continue;
                    }

                    // 超过最大重试次数 → 删除僵尸车辆
                    // 只下 Deleted；CarKeeper 與 OwnedVehicle 由原版 Game.Vehicles.ReferencesSystem
                    // 的 Deleted 分支自動清理（詳見 RescueScanJob 中的孤兒車註釋）
                    if (currentRetry >= kMaxRetries)
                    {
                        m_CommandBuffer.AddComponent<Deleted>(vehicle);
                        m_RescuedVehicles.Remove(vehicle);
                        m_Stats[kStatAbandon]++;
                        continue;
                    }

                    // 仍然失败 → 递增重试计数，重新挂 FixParkingLocation 让原版系统下一周期再试
                    Entity homeProperty = GetHomeProperty(vehicle);
                    if (homeProperty != Entity.Null)
                    {
                        m_RescuedVehicles[vehicle] = currentRetry + 1;
                        m_CommandBuffer.AddComponent(vehicle, new FixParkingLocation(Entity.Null, homeProperty));
                        m_CommandBuffer.AddComponent<Updated>(vehicle);
                        m_Stats[kStatRetry]++;
                    }
                    else
                    {
                        // 无住宅（家庭已搬走或解散）→ 直接删除（清理同上，交由原版）
                        m_CommandBuffer.AddComponent<Deleted>(vehicle);
                        m_RescuedVehicles.Remove(vehicle);
                        m_Stats[kStatAbandon]++;
                    }
                }

                keys.Dispose();
            }

            /// <summary>
            /// 链路：Vehicle → Owner.m_Owner (Household) → PropertyRenter.m_Property (Home Building)
            /// </summary>
            private Entity GetHomeProperty(Entity vehicle)
            {
                if (!m_OwnerData.TryGetComponent(vehicle, out var owner))
                    return Entity.Null;

                if (!m_PropertyRenterData.TryGetComponent(owner.m_Owner, out var renter))
                    return Entity.Null;

                return renter.m_Property;
            }
        }

        /// <summary>
        /// 阶段 2：检测 InitializeSystem.FindParkingSpace() 失败的车辆（新购车与存量幽灵车），
        /// 有家者传送到住宅附近交由原版系统重试；无家的孤兒車直接安全刪除。
        ///
        /// 單次處理量受 <see cref="kMaxRescuePerRun"/> 限制，避免舊存檔載入時
        /// 一次性對上千輛存量幽靈車下指令（實測曾達 1334 輛／幀）。
        /// 未處理的會在後續每 64 模擬幀繼續消化，順序由 chunk 迭代決定，無飢餓問題
        /// （已處理者進追蹤表後即被 <c>ContainsKey</c> 略過）。
        /// </summary>
        [BurstCompile]
        private struct RescueScanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle m_EntityType;
            [ReadOnly] public ComponentTypeHandle<ParkedCar> m_ParkedCarType;
            [ReadOnly] public ComponentTypeHandle<PersonalCar> m_PersonalCarType;
            [ReadOnly] public ComponentTypeHandle<Owner> m_OwnerType;

            [ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenterData;
            [ReadOnly] public ComponentLookup<Game.Objects.Transform> m_TransformData;

            public NativeHashMap<Entity, int> m_RescuedVehicles;
            public EntityCommandBuffer m_CommandBuffer;
            public NativeArray<int> m_Stats;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(m_EntityType);
                var parkedCars = chunk.GetNativeArray(ref m_ParkedCarType);
                var personalCars = chunk.GetNativeArray(ref m_PersonalCarType);
                var owners = chunk.GetNativeArray(ref m_OwnerType);

                for (int i = 0; i < entities.Length; i++)
                {
                    // 关键过滤：只处理 m_Lane 为 Null 的车辆（停放失败）
                    // 置於最前：健康車輛佔絕大多數，先擋掉可省下後續的雜湊查詢
                    if (parkedCars[i].m_Lane != Entity.Null)
                        continue;

                    Entity vehicle = entities[i];

                    // 跳过已在追踪中的车辆（由 RetryJob 处理）
                    if (m_RescuedVehicles.ContainsKey(vehicle))
                        continue;

                    // 排除虚拟交通车辆
                    if ((personalCars[i].m_State & PersonalCarFlags.DummyTraffic) != 0)
                        continue;

                    // --- 分批閘門：本輪配額用盡後只統計、不下指令 ---
                    // 置於過濾之後，確保配額只計「真正要動手的車」，不被大量健康車輛耗盡。
                    if (m_Stats[kStatRescue] + m_Stats[kStatOrphan] >= kMaxRescuePerRun)
                    {
                        m_Stats[kStatDeferred]++;
                        continue;
                    }

                    // 获取车主住宅
                    Entity homeProperty = Entity.Null;
                    if (m_PropertyRenterData.TryGetComponent(owners[i].m_Owner, out var renter))
                        homeProperty = renter.m_Property;

                    if (homeProperty == Entity.Null)
                    {
                        // --- 孤兒車清理（存量 bug 車輛，多見於舊存檔）---
                        // 家庭已搬走/解散/無房（含遊民家庭），無法以住宅為中心重試，
                        // 車輛本身已是不可用的幽靈狀態，直接安全刪除是淨收益。
                        //
                        // 只下 Deleted，不自行清 CarKeeper / OwnedVehicle：
                        // 原版 Game.Vehicles.ReferencesSystem（Modification5）的 Deleted 分支會在
                        // 實體真正銷毀前自動清理兩者——`m_CarKeepers[keeper].m_Car == entity` 時歸零
                        // （ReferencesSystem.cs:532-536），並 `CollectionUtils.RemoveValue` 移除
                        // OwnedVehicle 項（:481-484）。其 query 為 All{Vehicle} + Any{Created,Deleted}，
                        // 而私家車 archetype 經 VehiclePrefab 必帶 Vehicle，故必被覆蓋。
                        // 實際銷毀更晚：MainLoop 的 PrepareCleanUpSystem 收集 → Cleanup 的
                        // CleanUpSystem.DestroyEntity，兩者都在 ReferencesSystem 之後。
                        m_CommandBuffer.AddComponent<Deleted>(vehicle);
                        m_Stats[kStatOrphan]++;
                        continue;
                    }

                    // 获取住宅 Transform
                    //
                    // 取不到時（罕見：住宅正在建造或銷毀中）不再直接 continue——
                    // 那樣該車既不消耗配額也不進追蹤表，會每輪被重新評估且永不收斂。
                    // 改為照樣掛 FixParkingLocation 並記入追蹤表：原版
                    // FixParkingLocationSystem 會以車輛現位置為中心搜尋，
                    // 後續由 RetryJob 以 kMaxRetries 收尾。這與重試路徑語意一致
                    // ——它本來也只重掛 FixParkingLocation，不做傳送。
                    bool canTeleport = m_TransformData.TryGetComponent(homeProperty, out var homeTf);

                    // 1. 记录到追踪表（初始重试计数为 0；上方 ContainsKey 已保證此键不存在）
                    m_RescuedVehicles.TryAdd(vehicle, 0);

                    // 2. 将车辆 Transform 传送到住宅位置
                    if (canTeleport)
                        m_CommandBuffer.SetComponent(vehicle, homeTf);

                    // 3. 添加 FixParkingLocation，m_ResetLocation = homeProperty
                    //    原版 FixParkingLocationSystem 会以住宅 Transform 为搜索中心，100m 范围内查找车位
                    m_CommandBuffer.AddComponent(vehicle, new FixParkingLocation(Entity.Null, homeProperty));

                    // 4. 添加 Updated 标记，原版 FixParkingLocationSystem.m_FixQuery 要求
                    //    All={Updated} + Any={FixParkingLocation} 才能匹配到该实体
                    m_CommandBuffer.AddComponent<Updated>(vehicle);

                    m_Stats[kStatRescue]++;
                }
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 掃描當前存檔中的幽靈車輛並生成統計報告（供 Settings 按鈕呼叫）。
        /// 純唯讀操作，不做任何修改；實際清理由本系統低頻自動完成。
        /// </summary>
        public string ScanGhostVehicles()
        {
            // UI 按鈕觸發，主執行緒讀追蹤表與 lookup 前必須等待本系統的 job
            CompleteDependency();

            m_ParkedCarLookup.Update(this);
            m_PersonalCarLookup.Update(this);
            m_OwnerLookup.Update(this);
            m_PropertyRenterLookup.Update(this);

            using var entities = m_CandidateQuery.ToEntityArray(Allocator.Temp);

            int ghostCount = 0;      // 停放失敗的幽靈車總數
            int rescuableCount = 0;  // 有住宅可救援
            int orphanCount = 0;     // 無家孤兒車（將被刪除）
            int dummyCount = 0;      // 虛擬交通（不處理）

            for (int i = 0; i < entities.Length; i++)
            {
                Entity vehicle = entities[i];

                if (m_ParkedCarLookup[vehicle].m_Lane != Entity.Null) continue;

                if ((m_PersonalCarLookup[vehicle].m_State & PersonalCarFlags.DummyTraffic) != 0)
                {
                    dummyCount++;
                    continue;
                }

                ghostCount++;
                if (GetHomeProperty(vehicle) != Entity.Null)
                    rescuableCount++;
                else
                    orphanCount++;
            }

            bool enabled = Mod.Instance?.Settings?.EnableVehicleRescue == true;
            return $"Ghost vehicles: {ghostCount} (rescuable: {rescuableCount}, orphan: {orphanCount}, dummy skipped: {dummyCount}, tracking: {m_RescuedVehicles.Count})"
                + (enabled ? " | Rescue: ON, cleanup in progress." : " | Rescue: OFF, enable it to start cleanup.");
        }

        /// <summary>
        /// 获取车辆的车主住宅 Property 实体（主執行緒版本，供 <see cref="ScanGhostVehicles"/> 使用）。
        /// 链路：Vehicle → Owner.m_Owner (Household) → PropertyRenter.m_Property (Home Building)
        /// </summary>
        private Entity GetHomeProperty(Entity vehicle)
        {
            if (!m_OwnerLookup.TryGetComponent(vehicle, out var owner))
                return Entity.Null;

            if (!m_PropertyRenterLookup.TryGetComponent(owner.m_Owner, out var renter))
                return Entity.Null;

            return renter.m_Property;
        }

        #endregion

        #region Debug File Log

        /// <summary>
        /// 讀取本輪統計並輸出到檔案日誌。僅在 EnableRescueDebugLog 開啟時被呼叫
        /// （呼叫端已 Complete 過 job，此處可安全讀 <see cref="m_Stats"/>）。
        /// </summary>
        private void EmitDebugLog()
        {
            int success = m_Stats[kStatSuccess];
            int retry = m_Stats[kStatRetry];
            int abandon = m_Stats[kStatAbandon];
            int rescued = m_Stats[kStatRescue];
            int orphan = m_Stats[kStatOrphan];
            int deferred = m_Stats[kStatDeferred];

            if (success > 0)
                DebugLog($"购车救援完成：{success} 辆车辆已成功停放在住宅附近");
            if (retry > 0)
                DebugLog($"购车救援重试：{retry} 辆车辆仍在等待住宅附近车位空出");
            if (abandon > 0)
                DebugLog($"购车救援放弃：{abandon} 辆车辆超过最大重试次数({kMaxRetries})或已无住宅，已删除");
            if (rescued > 0)
                DebugLog($"购车救援：已将 {rescued} 辆停放失败的新购车辆传送到住宅附近重新停放");
            if (orphan > 0)
                DebugLog($"孤兒車清理：已刪除 {orphan} 輛無家可歸的存量幽靈車輛");

            // 積壓量僅在數量變化時記一次，避免每輪重複刷同一行
            if (deferred != m_DeferredRescueCount)
            {
                m_DeferredRescueCount = deferred;
                if (deferred > 0)
                    DebugLog($"购车救援分批：本轮配额 {kMaxRescuePerRun} 已用尽，尚有 {deferred} 辆待后续处理");
            }
        }

        /// <summary>
        /// 文件日志：绕过 Unity/Colossal Logger，直接写入 ModsData 目录下的独立日志文件。
        /// 仅在 EnableRescueDebugLog 开启时写入，低频调用无性能影响。
        /// </summary>
        private void DebugLog(string message)
        {
            try
            {
                if (m_LogFilePath == null)
                {
                    var dir = Path.Combine(
                        UnityEngine.Application.persistentDataPath,
                        "ModsData", "MapExt2");
                    Directory.CreateDirectory(dir);
                    m_LogFilePath = Path.Combine(dir, "rescue_debug.log");
                }

                File.AppendAllText(m_LogFilePath,
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { /* 静默：文件 I/O 失败不应影响游戏 */ }
        }

        #endregion
    }
}
