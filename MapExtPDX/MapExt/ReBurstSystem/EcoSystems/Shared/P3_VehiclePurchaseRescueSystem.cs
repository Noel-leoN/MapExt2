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
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;

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
    /// 【设计】使用纯内存 Dictionary 追踪已救援车辆，不注入任何自定义 ECS 组件到实体上，
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

        // === 候选车辆 Query：检测所有可能需要救援的新购车辆 ===
        private EntityQuery m_CandidateQuery;

        // === 纯内存追踪：已救援车辆 → 重试计数 ===
        // 不注入任何自定义 Component，存档零污染
        private readonly Dictionary<Entity, int> m_RescuedVehicles = new();

        /// <summary>首次救援被上限攔下的累計輛數，僅用於節流日誌。</summary>
        private int m_DeferredRescueCount;

        // === 文件日志路径（延迟初始化） ===
        private string m_LogFilePath;

        // === ComponentLookup 用于高效访问 ===
        // 全部走 lookup 而非 EntityManager：後者在 OnUpdate 內會觸發隱式 job 同步點，
        // 且對持有中的 lookup 有失效風險（見 OnUpdate 的設計說明）。
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
        /// </summary>
        public override int GetUpdateInterval(SystemUpdatePhase phase) => kUpdateInterval;

        protected override void OnCreate()
        {
            base.OnCreate();

            // --- 候选 Query：所有可能需要救援的新购私家车 ---
            // ParkedCar + PersonalCar + Owner + Unspawned
            // 排除：Created（未初始化）、Deleted、Temp、FixParkingLocation（正在被原版处理）
            // 注意：不再排除 VehicleRescued（已移除该自定义组件），改用 Dictionary 过滤
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

            // 初始化 ComponentLookup
            m_ParkedCarLookup = GetComponentLookup<ParkedCar>(true);
            m_PersonalCarLookup = GetComponentLookup<PersonalCar>(true);
            m_OwnerLookup = GetComponentLookup<Owner>(true);
            m_PropertyRenterLookup = GetComponentLookup<PropertyRenter>(true);
            m_TransformLookup = GetComponentLookup<Game.Objects.Transform>(true);
            m_FixParkingLookup = GetComponentLookup<FixParkingLocation>(true);

            ModLog.Info(Tag, "购车救援系统已创建");
        }

        /// <summary>
        /// 存檔切換時清空追蹤表：Entity 索引會在新 World 中重用，
        /// 殘留的鍵會讓 ProcessRetry 對新存檔的無關實體下指令。
        /// </summary>
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            m_RescuedVehicles.Clear();
            m_DeferredRescueCount = 0;
        }

        protected override void OnUpdate()
        {
            // 主开关检查：未启用时不执行任何逻辑
            if (Mod.Instance?.Settings?.EnableVehicleRescue != true)
                return;

            // 更新 ComponentLookup
            m_ParkedCarLookup.Update(this);
            m_PersonalCarLookup.Update(this);
            m_OwnerLookup.Update(this);
            m_PropertyRenterLookup.Update(this);
            m_TransformLookup.Update(this);
            m_FixParkingLookup.Update(this);

            // 统一 Query 结果
            using var entities = m_CandidateQuery.ToEntityArray(Allocator.Temp);

            // === 全部結構性與元件寫入一律走 ECB ===
            // 本方法內**不得**出現任何 EntityManager.SetComponentData / GetBuffer 之類的直寫：
            // ① 直寫會觸發隱式 job 同步點，且會令上方已 Update 的 ComponentLookup 面臨失效風險；
            // ② 直寫與 ECB 混用會造成「同一幀內部分生效、部分延後」的順序不確定；
            // ③ 曾實測某 78 萬人口舊存檔在載入後累積上千輛存量幽靈車，
            //    大量同步 buffer 改寫疑與序列化階段的 ECS 狀態損壞相關（未完全坐實，但直寫無必要）。
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            {
                // === 阶段 1：对已救援但仍失败的车辆重试 ===
                // 注意：必須先於 ProcessRescue 執行——若順序顛倒，剛入隊的新車會在同幀
                // 被立即重複處理（ECB 尚未 playback，FixParkingLocation 檢查不到），
                // 導致重試計數虛增與 ECB 重複指令。
                ProcessRetry(ecb);

                // === 阶段 2：首次救援新车辆（受 kMaxRescuePerRun 分批）===
                ProcessRescue(entities, ecb);

                // === 清理：移除已不在 Query 中的陈旧记录 ===
                CleanupStaleEntries(entities);

                // 统一执行所有记录的修改
                ecb.Playback(EntityManager);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 阶段：检测 InitializeSystem.FindParkingSpace() 失败的车辆（新购车与存量幽灵车），
        /// 有家者传送到住宅附近交由原版系统重试；无家的孤兒車直接安全刪除。
        ///
        /// 單次處理量受 <see cref="kMaxRescuePerRun"/> 限制，避免舊存檔載入時
        /// 一次性對上千輛存量幽靈車下指令（實測曾達 1334 輛／幀）。
        /// 未處理的會在後續每 64 模擬幀繼續消化，順序由 Query 決定，無飢餓問題
        /// （已處理者進追蹤表後即被 <c>ContainsKey</c> 略過）。
        /// </summary>
        private void ProcessRescue(NativeArray<Entity> entities, EntityCommandBuffer ecb)
        {
            int rescuedCount = 0;
            int orphanCount = 0;
            int deferred = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                Entity vehicle = entities[i];

                // 跳过已在追踪中的车辆（由阶段2处理）
                if (m_RescuedVehicles.ContainsKey(vehicle)) continue;

                // 关键过滤：只处理 m_Lane 为 Null 的车辆（停放失败）
                ParkedCar parkedCar = m_ParkedCarLookup[vehicle];
                if (parkedCar.m_Lane != Entity.Null) continue;

                // 排除虚拟交通车辆
                PersonalCar pc = m_PersonalCarLookup[vehicle];
                if ((pc.m_State & PersonalCarFlags.DummyTraffic) != 0) continue;

                // --- 分批閘門：本輪配額用盡後只統計、不下指令 ---
                // 置於過濾之後，確保配額只計「真正要動手的車」，不被大量健康車輛耗盡。
                if (rescuedCount + orphanCount >= kMaxRescuePerRun)
                {
                    deferred++;
                    continue;
                }

                // 获取车主住宅
                Entity homeProperty = GetHomeProperty(vehicle);
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
                    ecb.AddComponent<Deleted>(vehicle);
                    orphanCount++;
                    continue;
                }

                // 获取住宅 Transform
                //
                // 取不到時（罕見：住宅正在建造或銷毀中）不再直接 continue——
                // 那樣該車既不消耗配額也不進追蹤表，會每輪被重新評估且永不收斂。
                // 改為照樣掛 FixParkingLocation 並記入追蹤表：原版
                // FixParkingLocationSystem 會以車輛現位置為中心搜尋，
                // 後續由 ProcessRetry 以 kMaxRetries 收尾。這與 ProcessRetry 的重試路徑
                // 語意一致——它本來也只重掛 FixParkingLocation，不做傳送。
                bool canTeleport = m_TransformLookup.TryGetComponent(homeProperty, out var homeTf);

                // 1. 记录到内存追踪表（初始重试计数为 0）
                m_RescuedVehicles[vehicle] = 0;

                // 2. 将车辆 Transform 传送到住宅位置
                if (canTeleport)
                    ecb.SetComponent(vehicle, homeTf);

                // 3. 添加 FixParkingLocation，m_ResetLocation = homeProperty
                //    原版 FixParkingLocationSystem 会以住宅 Transform 为搜索中心，100m 范围内查找车位
                ecb.AddComponent(vehicle, new FixParkingLocation(Entity.Null, homeProperty));

                // 4. 添加 Updated 标记，原版 FixParkingLocationSystem.m_FixQuery 要求
                //    All={Updated} + Any={FixParkingLocation} 才能匹配到该实体
                ecb.AddComponent<Updated>(vehicle);

                rescuedCount++;
            }

            if (rescuedCount > 0)
                DebugLog($"购车救援：已将 {rescuedCount} 辆停放失败的新购车辆传送到住宅附近重新停放");
            if (orphanCount > 0)
                DebugLog($"孤兒車清理：已刪除 {orphanCount} 輛無家可歸的存量幽靈車輛");

            // 積壓量僅在數量變化時記一次，避免每輪重複刷同一行
            if (deferred != m_DeferredRescueCount)
            {
                m_DeferredRescueCount = deferred;
                if (deferred > 0)
                    DebugLog($"购车救援分批：本轮配额 {kMaxRescuePerRun} 已用尽，尚有 {deferred} 辆待后续处理");
            }
        }

        /// <summary>
        /// 阶段：对已救援但住宅附近仍无车位的车辆，重新挂 FixParkingLocation 持续重试。
        /// 超过最大重试次数后删除僵尸车辆。
        /// </summary>
        private void ProcessRetry(EntityCommandBuffer ecb)
        {
            // 拷贝所有的追踪 Key 以免在循环中修改字典导致 Enumerator 失效
            using var keys = new NativeList<Entity>(m_RescuedVehicles.Count, Allocator.Temp);
            foreach (var key in m_RescuedVehicles.Keys)
            {
                keys.Add(key);
            }

            // 收集需要从追踪表中移除的 Entity（避免遍历中修改字典）
            using var toRemove = new NativeList<Entity>(Allocator.Temp);
            int retryCount = 0;
            int successCount = 0;
            int removedCount = 0;

            for (int i = 0; i < keys.Length; i++)
            {
                Entity vehicle = keys[i];
                if (!m_RescuedVehicles.TryGetValue(vehicle, out int currentRetry))
                    continue;

                // 实体已被销毁或不再匹配 Query → 清理
                // 存在性一律走 lookup.EntityExists，不用 EntityManager.Exists（避免隱式同步點）
                if (!m_ParkedCarLookup.EntityExists(vehicle) || !m_ParkedCarLookup.HasComponent(vehicle))
                {
                    toRemove.Add(vehicle);
                    continue;
                }

                // FixParkingLocation 仍挂着 → 原版系统还没处理完，跳过
                if (m_FixParkingLookup.HasComponent(vehicle))
                    continue;

                ParkedCar parkedCar = m_ParkedCarLookup[vehicle];

                // 已成功停放 → 从追踪表移除，完成救援
                if (parkedCar.m_Lane != Entity.Null)
                {
                    toRemove.Add(vehicle);
                    successCount++;
                    continue;
                }

                // 超过最大重试次数 → 删除僵尸车辆
                // 只下 Deleted；CarKeeper 與 OwnedVehicle 由原版 Game.Vehicles.ReferencesSystem
                // 的 Deleted 分支自動清理（詳見 ProcessRescue 中的孤兒車註釋）
                if (currentRetry >= kMaxRetries)
                {
                    ecb.AddComponent<Deleted>(vehicle);
                    toRemove.Add(vehicle);
                    removedCount++;
                    continue;
                }

                // 仍然失败 → 递增重试计数，重新挂 FixParkingLocation 让原版系统下一周期再试
                Entity homeProperty = GetHomeProperty(vehicle);
                if (homeProperty != Entity.Null)
                {
                    m_RescuedVehicles[vehicle] = currentRetry + 1;
                    ecb.AddComponent(vehicle, new FixParkingLocation(Entity.Null, homeProperty));
                    ecb.AddComponent<Updated>(vehicle);
                    retryCount++;
                }
                else
                {
                    // 无住宅（家庭已搬走或解散）→ 直接删除（清理同上，交由原版）
                    ecb.AddComponent<Deleted>(vehicle);
                    toRemove.Add(vehicle);
                    removedCount++;
                }
            }

            // 批量清理追踪表
            for (int i = 0; i < toRemove.Length; i++)
            {
                m_RescuedVehicles.Remove(toRemove[i]);
            }

            if (successCount > 0)
                DebugLog($"购车救援完成：{successCount} 辆车辆已成功停放在住宅附近");
            if (retryCount > 0)
                DebugLog($"购车救援重试：{retryCount} 辆车辆仍在等待住宅附近车位空出");
            if (removedCount > 0)
                DebugLog($"购车救援放弃：{removedCount} 辆车辆超过最大重试次数({kMaxRetries})已删除");
        }

        /// <summary>
        /// 清理陈旧的追踪记录：移除已不存在于 ECS 世界中的实体。
        /// 防止 Dictionary 内存泄漏。
        /// </summary>
        private void CleanupStaleEntries(NativeArray<Entity> entities)
        {
            if (m_RescuedVehicles.Count == 0)
                return;

            // 构建当前 Query 匹配的实体集合
            var activeSet = new HashSet<Entity>(entities.Length);
            for (int i = 0; i < entities.Length; i++)
            {
                activeSet.Add(entities[i]);
            }

            // 移除不在 Query 中且实体已不存在的记录
            using var toRemove = new NativeList<Entity>(Allocator.Temp);
            foreach (var kvp in m_RescuedVehicles)
            {
                if (!activeSet.Contains(kvp.Key) && !m_ParkedCarLookup.EntityExists(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toRemove.Length; i++)
            {
                m_RescuedVehicles.Remove(toRemove[i]);
            }
        }

        /// <summary>
        /// 获取车辆的车主住宅 Property 实体。
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

        /// <summary>
        /// 掃描當前存檔中的幽靈車輛並生成統計報告（供 Settings 按鈕呼叫）。
        /// 純唯讀操作，不做任何修改；實際清理由本系統低頻自動完成。
        /// </summary>
        public string ScanGhostVehicles()
        {
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

        #endregion

        #region Debug File Log

        /// <summary>
        /// 文件日志：绕过 Unity/Colossal Logger，直接写入 ModsData 目录下的独立日志文件。
        /// 仅在 EnableRescueDebugLog 开启时写入，低频调用无性能影响。
        /// </summary>
        private void DebugLog(string message)
        {
            if (Mod.Instance?.Settings?.EnableRescueDebugLog != true)
                return;

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
