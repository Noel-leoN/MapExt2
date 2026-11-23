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
    /// 方案 E — 水模拟事件驱动自适应休眠（<c>WaterSimQuality = Adaptive_EventDriven</c>）。
    ///
    /// <para>
    /// <b>[SUSPENDED / 硬挂起]</b> 本档位已停用：UI 不下拉、
    /// <see cref="ResolutionManager.SanitizeWaterSimQuality"/> 强制降级为 Vanilla，
    /// <c>PatchSet2WaterOpt</c> 不再调用 <see cref="Apply"/>，Postfix 的
    /// “意图 speed=0”例外已收回仅保留 <c>Paused_NoFlow</c>。
    /// 原因：跨帧持有 <c>WaterSimSpeed=0</c> 与 Editor 横跳修复 / PauseFreeze
    /// （依赖 Postfix 抬回 StableSpeed）结构性冲突；另 changed-filter 未设
    /// RequiredVersion 会使休眠路径失效。代码保留供日后以单一 speed 仲裁器重启。
    /// </para>
    ///
    /// <para>
    /// 动机：多数城市在多数时间水面已收敛（河流稳态、湖泊静止），但原版每渲染帧
    /// 照样跑完整条 GPU 模拟链。固定降频（<c>Minimal_Every4Frames</c>）是时间上的粗暴切分——
    /// 静止时仍付 1/4 成本，激烈变化时又只有 1/4 保真。本档位改为<b>内容自适应</b>：
    /// 收敛后完全休眠（近零成本），任何扰动瞬间全速唤醒。
    /// </para>
    ///
    /// <para>=== 唤醒判据（三层防线） ===</para>
    ///
    /// <para>
    /// 1. <b>被动侦测（覆盖最大风险面，零监听成本）</b>：
    /// 游戏原生已有睡眠协议：所有地形类扰动（地形刷、建筑放置/升级、道路整平、
    /// 编辑器 lot、heightmap 替换、载图）都经 <c>TerrainWillChange</c>（counter=1）或
    /// <c>TerrainWillChangeFromBrush</c>（counter=15），倒数归零时由 <c>Simulate()</c>
    /// <b>强制写 <c>WaterSimSpeed=1</c></b>（WaterSystem.cs:1426）。
    /// 因此本补丁不必监听任何地形事件——休眠期间侦测到 speed 被外力改为非 0
    /// 即视为唤醒信号。这一条就吃下地形类全部路径。
    /// </para>
    ///
    /// <para>
    /// 2. <b>主动监听（4 个信号，全 public/ECS，零反射）</b>：
    /// <c>WaterLevelChange</c> query（海啸/洪水事件）；
    /// <c>WaterSourceData</c> changed-filter（水源增删改；原版 SourceJob 只 RO 读，无自噪音）；
    /// <c>SeaLevel</c> float 缓存比对（<c>m_seaLevelChanged</c> 会在 Simulate 尾自清，轮询有窗口，直接比对 float 更稳）；
    /// <c>ClimateSystem.isPrecipitating</c>（降水开始）。
    /// </para>
    ///
    /// <para>
    /// 3. <b>心跳兜底（漏事件的最后保险）</b>：
    /// 每 <see cref="kHeartbeatInterval"/> 帧强制唤醒，跑满
    /// <see cref="kObserveFrames"/> 帧让 active-tiles readback 走完一轮，
    /// 连续 <see cref="kStableRoundsToSleep"/> 轮判定收敛才再次休眠。
    /// 这同时消化了融雪黑盒：<c>SnowSystem.AddSnow</c> 每帧把 WaterTexture 绑进雪 kernel
    /// 且<b>不受 WaterSimSpeed 闸控</b>，C# 侧无法确证读写方向；心跳保证即使雪在
    /// 休眠期间改了水面，也会被周期性重新模拟消化。
    /// </para>
    ///
    /// <para>=== 收敛判据 ===</para>
    ///
    /// <para>
    /// <c>GetActive()</c> 的活跃砖集合稳定 + <c>GetDepths()</c> 的 Σ|Δdepth| &lt; ε。
    /// <b>不用 velocity 归零</b>——河流稳态有恒定非零流速，那样永远不会睡。
    /// </para>
    ///
    /// <para>
    /// 注意 <c>GetActive()</c> 的实际长度是 <c>(m_TexSize / GridSize)²</c>
    /// （<c>GridSize = 256</c>，默认 <c>2048 / 256 = 8</c>，即 <c>8×8 = 64</c>）。
    /// 收敛比对采用累加 checksum，且仅在观察窗口内计算，不是每帧。
    /// </para>
    ///
    /// <para>=== 前置闸门 ===</para>
    ///
    /// <para>
    /// - <c>Loaded &amp;&amp; !IsNewMap</c>：载入中与新图初始收敛期不休眠。
    /// - 仅 Game 模式：编辑器做图需要即时看到水流。
    /// - <c>UseActiveCellsCulling</c> 为 false 时停用：该旗标被编辑器
    ///   <c>UpdateSeaLevel</c>/<c>ResetToSealevel</c> 设 false 后<b>无任何代码恢复</b>，
    ///   此时全砖强制活跃、<c>GetActive()</c> 失去鉴别力。
    /// - 地形变更 counter &gt; 0 期间让行（与 PatchSet2WaterOpt 的既有纪律一致）。
    /// </para>
    ///
    /// <para>=== 与其他 WaterSimSpeed 写入者的优先级 ===</para>
    ///
    /// <para>
    /// 地形恢复窗口（counter &gt; 0） &gt; Paused 档位 &gt; 暂停冻结 &gt; 本档位。
    /// 本档位与 <c>Minimal_Every4Frames</c> 语义互斥，由 enum 天然保证不会同时生效。
    /// </para>
    /// </summary>
    internal static class WaterAdaptiveSleep
    {
        private const string Tag = nameof(WaterAdaptiveSleep);

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

        /// <summary>深度取样步长：默认 2048²（4,194,304 格）全扫太贵，按步长隔格取样。</summary>
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
                WakeUp(instance, "地形变更中");
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
                    WakeUp(instance, $"speed 被外力改为 {current}");
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
        /// 活跃砖集合的位置敏感 checksum。
        /// 数组长度是 (m_TexSize / GridSize)²（GridSize = 256，默认 2048/256 → 8×8 = 64），
        /// 用单趟累加而非逐格 diff；乘上索引使“砖集合位移但总数不变”也能被侦测。
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
        /// 抽样深度总和。默认 2048²（4,194,304 格）全扫过贵，按 <see cref="kDepthSampleStride"/> 隔格取样；
        /// 仅在观察窗口结束时调用（非每帧）。
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
