// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    using Game; // GameModeExtensions.IsGame()
    using Game.Simulation;
    using MapExtPDX.MapExt.Core;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// 方案 E — 水模擬事件驅動自適應休眠（`WaterSimQuality = Adaptive_EventDriven`）。
    ///
    /// 動機：多數城市在多數時間水面已收斂（河流穩態、湖泊靜止），但原版每渲染幀
    /// 照樣跑完整條 GPU 模擬鏈。固定降頻（`Minimal_Every4Frames`）是時間上的粗暴切分——
    /// 靜止時仍付 1/4 成本，激烈變化時又只有 1/4 保真。本檔位改為**內容自適應**：
    /// 收斂後完全休眠（近零成本），任何擾動瞬間全速喚醒。
    ///
    /// === 喚醒判據（三層防線） ===
    ///
    /// 1. **被動偵測（覆蓋最大風險面，零監聽成本）**
    ///    遊戲原生已有睡眠協議：所有地形類擾動（地形刷、建築放置/升級、道路整平、
    ///    編輯器 lot、heightmap 替換、載圖）都經 `TerrainWillChange`（counter=1）或
    ///    `TerrainWillChangeFromBrush`（counter=15），倒數歸零時由 `Simulate()`
    ///    **強制寫 `WaterSimSpeed=1`**（WaterSystem.cs:1426）。
    ///    因此本補丁不必監聽任何地形事件——休眠期間偵測到 speed 被外力改為非 0
    ///    即視為喚醒信號。這一條就吃下地形類全部路徑。
    ///
    /// 2. **主動監聽（4 個信號，全 public/ECS，零反射）**
    ///    - `WaterLevelChange` query（海嘯/洪水事件）
    ///    - `WaterSourceData` changed-filter（水源增刪改；原版 SourceJob 只 RO 讀，無自噪音）
    ///    - `SeaLevel` float 快取比對（`m_seaLevelChanged` 會在 Simulate 尾自清，
    ///      輪詢有窗口，直接比對 float 更穩）
    ///    - `ClimateSystem.isPrecipitating`（降水開始）
    ///
    /// 3. **心跳兜底（漏事件的最後保險）**
    ///    每 <see cref="kHeartbeatInterval"/> 幀強制喚醒，跑滿
    ///    <see cref="kObserveFrames"/> 幀讓 active-tiles readback 走完一輪，
    ///    連續 <see cref="kStableRoundsToSleep"/> 輪判定收斂才再次休眠。
    ///    這同時消化了融雪黑盒：`SnowSystem.AddSnow` 每幀把 WaterTexture 綁進雪 kernel
    ///    且**不受 WaterSimSpeed 閘控**，C# 側無法確證讀寫方向；心跳保證即使雪在
    ///    休眠期間改了水面，也會被週期性重新模擬消化。
    ///
    /// === 收斂判據 ===
    ///
    /// `GetActive()` 的活躍磚集合穩定 + `GetDepths()` 的 Σ|Δdepth| &lt; ε。
    /// **不用 velocity 歸零**——河流穩態有恆定非零流速，那樣永遠不會睡。
    ///
    /// 注意 `GetActive()` 的實際長度是 `m_TexSize / GridSize` 的平方
    /// （2048/8 = 256² = 65536），非某些文檔所稱的 8×8=64。故收斂比對採
    /// 累加 checksum 而非逐格 diff，且僅在觀察窗口內計算，不是每幀。
    ///
    /// === 前置閘門 ===
    ///
    /// - `Loaded &amp;&amp; !IsNewMap`：載入中與新圖初始收斂期不休眠。
    /// - 僅 Game 模式：編輯器做圖需要即時看到水流。
    /// - `UseActiveCellsCulling` 為 false 時停用：該旗標被編輯器
    ///   `UpdateSeaLevel`/`ResetToSealevel` 設 false 後**無任何代碼恢復**，
    ///   此時全磚強制活躍、`GetActive()` 失去鑑別力。
    /// - 地形變更 counter &gt; 0 期間讓行（與 PatchSet2WaterOpt 的既有紀律一致）。
    ///
    /// === 與其他 WaterSimSpeed 寫入者的優先級 ===
    ///
    /// 地形恢復窗口（counter&gt;0） &gt; Paused 檔位 &gt; 暫停凍結 &gt; 本檔位。
    /// 本檔位與 `Minimal_Every4Frames` 語義互斥，由 enum 天然保證不會同時生效。
    /// </summary>
    internal static class WaterAdaptiveSleep
    {
        private const string Tag = "WaterAdaptive";

        #region Config

        /// <summary>心跳間隔（渲染幀）：休眠多久後強制喚醒重新評估。</summary>
        private const int kHeartbeatInterval = 512;

        /// <summary>
        /// 觀察窗口幀數：喚醒後至少全速跑這麼多幀才允許判斷收斂。
        /// 需覆蓋 active-tiles readback 一輪（原版每 8 sim 幀單位刷新）。
        /// </summary>
        private const int kObserveFrames = 36;

        /// <summary>連續多少輪觀察判定穩定才真正休眠。</summary>
        private const int kStableRoundsToSleep = 2;

        /// <summary>深度變化總量閾值：Σ|Δdepth| 低於此值視為收斂。</summary>
        private const float kDepthDeltaEpsilon = 0.05f;

        /// <summary>深度取樣步長：65536 格全掃太貴，隔格取樣。</summary>
        private const int kDepthSampleStride = 64;

        #endregion

        #region State

        private enum SleepState
        {
            /// <summary>全速模擬中，尚未累積足夠觀察幀。</summary>
            Awake,

            /// <summary>全速模擬中，正在累積連續穩定輪次。</summary>
            Observing,

            /// <summary>休眠中（WaterSimSpeed=0）。</summary>
            Sleeping,
        }

        private static SleepState s_state = SleepState.Awake;

        /// <summary>休眠前記錄的速度值（喚醒時恢復，勿硬編碼 1——Debug 面板可設 8-32）。</summary>
        private static int s_savedSpeed = 1;

        /// <summary>本補丁上次寫入 WaterSimSpeed 的值，用於區分外力改寫。</summary>
        private static int s_lastWritten = -1;

        private static int s_observeFrames = 0;
        private static int s_stableRounds = 0;
        private static int s_sleepFrames = 0;

        // --- 收斂比對快取 ---
        private static long s_lastActiveChecksum = long.MinValue;
        private static float s_lastDepthSum = float.NaN;

        // --- 信號快取 ---
        private static float s_lastSeaLevel = float.NaN;
        private static bool s_lastPrecipitating = false;

        // --- ECS query（一次性建立） ---
        private static EntityQuery s_waterLevelChangeQuery;
        private static EntityQuery s_waterSourceQuery;
        private static bool s_queriesBuilt = false;

        private static ClimateSystem s_climateSystem;

        /// <summary>統計：累計休眠幀數（DEBUG 日誌用）。</summary>
        private static int s_totalSleptFrames = 0;

        #endregion

        /// <summary>
        /// 退出主菜單時重置會話狀態。
        /// World 重建使 query 與系統引用失效；速度覆寫記錄跨會話必須作廢。
        /// </summary>
        internal static void ResetSessionState()
        {
            s_state = SleepState.Awake;
            s_savedSpeed = 1;
            s_lastWritten = -1;
            s_observeFrames = 0;
            s_stableRounds = 0;
            s_sleepFrames = 0;
            s_lastActiveChecksum = long.MinValue;
            s_lastDepthSum = float.NaN;
            s_lastSeaLevel = float.NaN;
            s_lastPrecipitating = false;
            s_queriesBuilt = false;
            s_waterLevelChangeQuery = default;
            s_waterSourceQuery = default;
            s_climateSystem = null;
            s_totalSleptFrames = 0;
            ModLog.Info(Tag, "WaterAdaptive session state reset");
        }

        #region System Loop

        /// <summary>
        /// 由 <see cref="WaterSystemOptRuntimePatch"/> 的 Prefix 在
        /// <c>Adaptive_EventDriven</c> 檔位下呼叫。
        /// </summary>
        /// <param name="instance">WaterSystem 實例。</param>
        /// <param name="terrainChangeCounter">
        /// 地形變更倒數（由呼叫方傳入，避免重複反射解析欄位）。
        /// </param>
        internal static void Apply(WaterSystem instance, int terrainChangeCounter)
        {
            // === 前置閘門 ===

            // 地形變更恢復窗口：讓行原版恢復流程，且視為擾動 → 回 Awake
            if (terrainChangeCounter > 0)
            {
                WakeUp(instance, "地形變更中");
                return;
            }

            // 載入中／新圖初始收斂期不休眠
            if (!instance.Loaded || instance.IsNewMap)
            {
                WakeUp(instance, "载入/新图收敛期");
                return;
            }

            // 編輯器停用（做圖需即時看到水流）
            var gm = Game.SceneFlow.GameManager.instance;
            if (gm == null || !gm.gameMode.IsGame())
            {
                WakeUp(instance, "非 Game 模式");
                return;
            }

            // active-tiles 剔除已失效 → GetActive() 無鑑別力，無法可靠判斂
            if (!instance.UseActiveCellsCulling)
            {
                WakeUp(instance, "UseActiveCellsCulling=false");
                return;
            }

            // === 捕獲外力改寫（被動偵測，第一層防線）===
            int current = instance.WaterSimSpeed;
            if (current > 0 && current != s_lastWritten)
            {
                // 休眠期間 speed 被改非 0 → 必定來自原版恢復流程或 Debug 面板
                s_savedSpeed = current;
                if (s_state == SleepState.Sleeping)
                {
                    WakeUp(instance, $"speed 被外力改為 {current}");
                    return;
                }
            }

            // === 主動監聽（第二層防線）===
            string signal = PollWakeSignals(instance);
            if (signal != null)
            {
                WakeUp(instance, signal);
                return;
            }

            // === 狀態機 ===
            switch (s_state)
            {
                case SleepState.Awake:
                    // 剛喚醒：先跑滿觀察窗口再開始評估收斂
                    s_observeFrames++;
                    if (s_observeFrames >= kObserveFrames)
                    {
                        s_state = SleepState.Observing;
                        s_observeFrames = 0;
                        // 建立首個基準快照（本輪不計入穩定輪次）
                        SnapshotConvergence(instance);
                    }
                    break;

                case SleepState.Observing:
                    s_observeFrames++;
                    if (s_observeFrames < kObserveFrames)
                        break;

                    s_observeFrames = 0;
                    if (IsConverged(instance))
                    {
                        s_stableRounds++;
                        if (s_stableRounds >= kStableRoundsToSleep)
                        {
                            GoToSleep(instance);
                        }
                    }
                    else
                    {
                        // 水面仍在變化 → 重置穩定計數，繼續觀察
                        s_stableRounds = 0;
                    }
                    break;

                case SleepState.Sleeping:
                    s_sleepFrames++;
                    s_totalSleptFrames++;

                    if (s_sleepFrames >= kHeartbeatInterval)
                    {
                        // 心跳：強制喚醒重新評估（第三層防線）
                        WakeUp(instance, "心跳兜底");
                        break;
                    }

                    // 維持休眠
                    instance.WaterSimSpeed = 0;
                    s_lastWritten = 0;
                    break;
            }
        }

        #endregion

        #region Helpers

        /// <summary>轉入全速模擬狀態。已在 Awake 且無需改寫 speed 時為零成本。</summary>
        private static void WakeUp(WaterSystem instance, string reason)
        {
            if (s_state == SleepState.Sleeping)
            {
                instance.WaterSimSpeed = s_savedSpeed;
                s_lastWritten = s_savedSpeed;
                ModLog.Patch(Tag,
                    $"水模拟已唤醒 (WaterSimSpeed → {s_savedSpeed}, 原因={reason}, 本次休眠 {s_sleepFrames} 帧)");
            }

            s_state = SleepState.Awake;
            s_observeFrames = 0;
            s_stableRounds = 0;
            s_sleepFrames = 0;
            s_lastActiveChecksum = long.MinValue;
            s_lastDepthSum = float.NaN;
        }

        /// <summary>轉入休眠：記錄當前速度後寫 0。</summary>
        private static void GoToSleep(WaterSystem instance)
        {
            int current = instance.WaterSimSpeed;
            if (current > 0)
            {
                s_savedSpeed = current;
            }

            s_state = SleepState.Sleeping;
            s_sleepFrames = 0;
            s_stableRounds = 0;

            instance.WaterSimSpeed = 0;
            s_lastWritten = 0;

            ModLog.Patch(Tag,
                $"水面已收敛，水模拟进入休眠 (WaterSimSpeed {s_savedSpeed} → 0, 累计休眠 {s_totalSleptFrames} 帧)");
        }

        /// <summary>
        /// 輪詢 4 個主動喚醒信號。回傳非 null 表示需喚醒（值為原因描述）。
        /// </summary>
        private static string PollWakeSignals(WaterSystem instance)
        {
            BuildQueries(instance);

            // --- 海嘯/洪水事件 ---
            if (s_queriesBuilt && !s_waterLevelChangeQuery.IsEmptyIgnoreFilter)
                return "WaterLevelChange 事件";

            // --- 水源增刪改 ---
            if (s_queriesBuilt && !s_waterSourceQuery.IsEmpty)
                return "水源变更";

            // --- 海平面變更 ---
            float seaLevel = instance.SeaLevel;
            if (!float.IsNaN(s_lastSeaLevel) && seaLevel != s_lastSeaLevel)
            {
                s_lastSeaLevel = seaLevel;
                return "海平面变更";
            }
            s_lastSeaLevel = seaLevel;

            // --- 降水開始 ---
            var climate = ResolveClimateSystem(instance);
            if (climate != null)
            {
                bool precipitating = climate.isPrecipitating;
                if (precipitating && !s_lastPrecipitating)
                {
                    s_lastPrecipitating = true;
                    return "开始降水";
                }
                s_lastPrecipitating = precipitating;
            }

            return null;
        }

        /// <summary>建立 ECS query（一次性）。水源 query 掛 changed-filter 以偵測修改。</summary>
        private static void BuildQueries(WaterSystem instance)
        {
            if (s_queriesBuilt) return;

            var world = instance.World;
            if (world == null) return;

            s_queriesBuilt = true;
            try
            {
                var em = world.EntityManager;

                s_waterLevelChangeQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<Game.Events.WaterLevelChange>());

                // changed-filter：僅當 WaterSourceData 被寫入時才非空
                s_waterSourceQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<WaterSourceData>());
                s_waterSourceQuery.SetChangedVersionFilter(
                    ComponentType.ReadOnly<WaterSourceData>());

                ModLog.Ok(Tag, "自适应休眠：唤醒信号 query 已建立");
            }
            catch (System.Exception ex)
            {
                ModLog.Warn(Tag, $"建立唤醒信号 query 失败，将仅依赖被动侦测与心跳兜底: {ex.Message}");
                s_waterLevelChangeQuery = default;
                s_waterSourceQuery = default;
            }
        }

        private static ClimateSystem ResolveClimateSystem(WaterSystem instance)
        {
            if (s_climateSystem != null) return s_climateSystem;

            var world = instance.World;
            if (world == null) return null;

            s_climateSystem = world.GetExistingSystemManaged<ClimateSystem>();
            return s_climateSystem;
        }

        /// <summary>建立收斂基準快照（不做判定）。</summary>
        private static void SnapshotConvergence(WaterSystem instance)
        {
            s_lastActiveChecksum = ComputeActiveChecksum(instance);
            s_lastDepthSum = ComputeDepthSum(instance);
        }

        /// <summary>
        /// 判斷水面是否已收斂：活躍磚集合不變 且 深度總量變化低於閾值。
        /// 同時更新快照供下一輪比對。
        /// </summary>
        private static bool IsConverged(WaterSystem instance)
        {
            long activeChecksum = ComputeActiveChecksum(instance);
            float depthSum = ComputeDepthSum(instance);

            bool converged;
            if (s_lastActiveChecksum == long.MinValue || float.IsNaN(s_lastDepthSum)
                || float.IsNaN(depthSum))
            {
                converged = false; // 無有效基準
            }
            else
            {
                converged = activeChecksum == s_lastActiveChecksum
                            && System.Math.Abs(depthSum - s_lastDepthSum) < kDepthDeltaEpsilon;
            }

            s_lastActiveChecksum = activeChecksum;
            s_lastDepthSum = depthSum;
            return converged;
        }

        /// <summary>
        /// 活躍磚集合的位置敏感 checksum。
        /// 陣列長度是 (m_TexSize / GridSize)²（預設 2048/8 → 256² = 65536），
        /// 故用單趟累加而非逐格 diff；乘上索引使「磚集合位移但總數不變」也能被偵測。
        /// </summary>
        private static long ComputeActiveChecksum(WaterSystem instance)
        {
            NativeArray<int> active;
            try
            {
                active = instance.GetActive();
            }
            catch (System.Exception)
            {
                return long.MinValue;
            }

            if (!active.IsCreated || active.Length == 0)
                return long.MinValue;

            long sum = 0;
            for (int i = 0; i < active.Length; i++)
            {
                if (active[i] > 0)
                {
                    sum += (long)i * 31 + 17;
                }
            }
            return sum;
        }

        /// <summary>
        /// 抽樣深度總和。65536+ 格全掃過貴，按 <see cref="kDepthSampleStride"/> 隔格取樣；
        /// 僅在觀察窗口結束時呼叫（非每幀）。
        /// </summary>
        private static float ComputeDepthSum(WaterSystem instance)
        {
            NativeArray<SurfaceWater> depths;
            Unity.Jobs.JobHandle deps;
            try
            {
                depths = instance.GetDepths(out deps);
            }
            catch (System.Exception)
            {
                return float.NaN;
            }

            if (!depths.IsCreated || depths.Length == 0)
                return float.NaN;

            // 讀 CPU 副本前確保寫入者完成
            deps.Complete();

            float sum = 0f;
            for (int i = 0; i < depths.Length; i += kDepthSampleStride)
            {
                sum += depths[i].m_Depth;
            }
            return sum;
        }

        #endregion
    }
}
