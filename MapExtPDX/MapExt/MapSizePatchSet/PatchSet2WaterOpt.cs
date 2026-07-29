// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    using Game; // GameModeExtensions.IsGame()
    using Game.Simulation;
    using HarmonyLib;
    using MapExtPDX.MapExt.Core;
    using UnityEngine.Rendering;

    /// <summary>
    /// 水模拟运行时性能优化补丁。
    /// 拦截 WaterSystem.OnSimulateGPU，通过 WaterSimSpeed 控制模拟频率。
    /// 
    /// 同时包含 Editor 水速横跳修复：
    /// TerrainWillChange() 在 Editor 中被每帧调用（MapExt 地形补丁导致），
    /// 每次设 WaterSimSpeed=0。Simulate() 在 counter 归零时恢复 speed=1。
    /// 此 0/1 交替导致原版 Editor Water 面板（DebugSystem）显示横跳。
    /// 
    /// 修复策略：在 Postfix 中检测 speed 是否被 TerrainWillChange 瞬态置 0，
    /// 若 Simulate 已将其恢复为 1（说明 terrain change 已处理完毕），
    /// 则保持该恢复值。这个方案不依赖对 TerrainWillChange 的 hook
    /// （该方法可能被 JIT 内联导致 Harmony detour 无效）。
    /// </summary>
    [HarmonyPatch(typeof(WaterSystem), "OnSimulateGPU")]
    internal static class WaterSystemOptRuntimePatch
    {
        private const string Tag = "WaterOpt";

        private static int s_frameCounter = 0;

        // --- 背景水（Backdrop）模擬開關 ---
        // m_SimulateBackdrop 由存檔反序列化（部分地圖原生無背景水），
        // 覆寫前記住原值，切回 Vanilla/Paused 時還原。
        // 直接寫私有欄位而非 simulateBackdrop 屬性：
        // 屬性 setter 會觸發 OnBackdropActiveChanged（重建紋理 + 16 帧重模擬 + m_NewMap=16），
        // 運行時切換不可承受；直接寫欄位僅跳過背景水的模擬與渲染分支。
        private static System.Reflection.FieldInfo s_simulateBackdropField;
        private static bool s_backdropFieldResolved = false;
        private static bool s_backdropOverridden = false;
        private static bool s_originalBackdrop = false;

        /// <summary>
        /// 记录用户设定的 speed 值（> 1 时才更新）。
        /// TerrainWillChange 只设 0，Simulate() 只设 0/1，
        /// 因此 speed > 1 只可能来自用户。
        /// </summary>
        internal static int StableSpeed = 1;

        // --- 暫停凍結（Pause Freeze）---
        // 遊戲暫停（selectedSpeed==0）時，原版 Simulate() 仍每渲染幀完整執行一步模擬
        // （selectedSpeed 的 break 位於循環步驟之後、GetTimeStep() 僅回傳 timestep=0），
        // 所有 compute dispatch 照發但物理不推進——純浪費。
        // 凍結 = 在 Prefix 尾部設 WaterSimSpeed=0，讓 Simulate() 跳過模擬循環；
        // 解凍後 speed 由既有 Postfix StableSpeed 機制自然恢復（與 Minimal 檔位同構）。
        private static Game.Simulation.SimulationSystem s_simulationSystem;
        private static System.Reflection.FieldInfo s_terrainCounterField;
        private static bool s_terrainCounterResolved = false;
        private static bool s_pauseFrozen = false;      // 僅供狀態變化日誌
        private static int s_pauseGraceFrames = 0;      // 地形變更後的收斂寬限（渲染幀）

        /// <summary>地形變更 counter 歸零後，暫停凍結讓行的收斂寬限幀數。</summary>
        private const int kPauseFreezeGraceFrames = 30;

        /// <summary>
        /// [BUGFIX] 退出主菜单时重置会话状态。
        /// m_SimulateBackdrop 会在下次加载时重新反序列化，覆写记录必须作废。
        /// </summary>
        internal static void ResetSessionState()
        {
            s_backdropOverridden = false;
            StableSpeed = 1;
            s_frameCounter = 0;
            s_lastAppliedAsync = null; // 下次加载重新套用并记录 IsAsync
            s_simulationSystem = null; // World 可能重建，下次凍結檢查時重新解析
            s_pauseFrozen = false;
            s_pauseGraceFrames = 0;
            ModLog.Info(Tag, "WaterOpt session state reset");
        }

        [HarmonyPrefix]
        static bool Prefix(WaterSystem __instance, CommandBuffer cmd)
        {
            // === 捕获用户意图值（在 Simulate 覆盖前） ===
            // Postfix 每帧将 speed 恢复为 StableSpeed，
            // 所以正常帧间 preSpeed 只可能是 StableSpeed 值或 0（TerrainWillChange）。
            // 若 preSpeed 不等于 StableSpeed 且 > 0，则必定是用户通过 Editor 面板设置的新值。
            int preSpeed = __instance.WaterSimSpeed;
            if (preSpeed > 0 && preSpeed != StableSpeed)
            {
                StableSpeed = preSpeed;
            }

            // === Async Compute（與畫質正交，須在 Vanilla 早退之前設定）===
            // UpdateSystem.OnBeginFrame 在呼叫本方法「前」讀 IsAsync 決定 CommandBuffer 的
            // AsyncCompute flag，「後」再讀一次決定是否 ExecuteCommandBufferAsync。
            // 穩態下每幀在此設值 → 兩次讀取一致；僅使用者切換當幀有一次無害的瞬態不匹配。
            ApplyAsyncCompute(__instance);

            var quality = ResolutionManager.WaterSimQuality;

            // Vanilla 模式：仅确保背景水已还原，其余零开销直通原版
            if (quality == WaterSimQualitySetting.Vanilla_EveryFrame)
            {
                // [BUGFIX] Minimal/Paused 檔位會設 BlurFlowMap=false，切回 Vanilla 必須還原，
                // 否則 Flow Blur 跨存檔永久關閉直到重啟遊戲（原版預設 true 且全遊戲無其他寫入點）
                __instance.BlurFlowMap = true;
                RestoreBackdrop(__instance);
                ApplyPauseFreeze(__instance);
                return true;
            }

            switch (quality)
            {
                case WaterSimQualitySetting.Paused_NoFlow:
                    // 完全暂停模拟，但 Simulate() 仍然执行维护逻辑；背景水还原为存档原值
                    __instance.WaterSimSpeed = 0;
                    __instance.BlurFlowMap = false;
                    RestoreBackdrop(__instance);
                    break;

                case WaterSimQualitySetting.Minimal_Every4Frames:
                    // 每 4 帧执行一次完整 GPU 模拟，其余帧仅维护状态；关闭背景水与 Flow Blur
                    // [BUGFIX] 地形變更 counter 倒數期間不寫 speed：
                    // 原版恢復流程靠 speed=0 凍結模擬循環，第 4 幀寫 1 會讓恢復與模擬並行
                    //（與 Postfix 的讓行同一根因）。counter 歸零後原版自行設 speed=1，節奏自然恢復。
                    if (GetTerrainChangeCounter(__instance) > 0)
                    {
                        break;
                    }
                    s_frameCounter++;
                    if (s_frameCounter % 4 != 0)
                    {
                        __instance.WaterSimSpeed = 0;
                    }
                    else
                    {
                        __instance.WaterSimSpeed = 1;
                        if (s_frameCounter >= 10000) s_frameCounter = 0;
                    }
                    __instance.BlurFlowMap = false;
                    DisableBackdrop(__instance);
                    break;

                case WaterSimQualitySetting.Reduced_NoBackdrop:
                    // 每帧模拟，仅关闭背景水模拟/渲染（Flow Blur 保持原版）
                    __instance.BlurFlowMap = true;
                    DisableBackdrop(__instance);
                    break;
            }

            // 暫停凍結最後套用（優先於各檔位的 speed 寫入）
            ApplyPauseFreeze(__instance);

            // 始终执行原版 OnSimulateGPU，不跳帧
            return true;
        }

        // --- Pause Freeze Helpers ---

        /// <summary>
        /// 遊戲暫停時凍結水模擬（WaterSimSpeed=0）。
        /// 僅 Game 模式生效——編輯器常態 selectedSpeed=0，凍結會讓做圖者看不到水流。
        /// 地形變更（放置建築等）的 counter 倒數期間與其後 30 幀寬限期讓行，
        /// 由原版恢復流程完成水面對新地形的收斂後再凍結。
        /// </summary>
        private static void ApplyPauseFreeze(WaterSystem instance)
        {
            bool freeze = false;

            if (ResolutionManager.WaterPauseFreeze
                && Game.SceneFlow.GameManager.instance != null
                && Game.SceneFlow.GameManager.instance.gameMode.IsGame())
            {
                if (GetTerrainChangeCounter(instance) > 0)
                {
                    // 地形變更倒數中：讓行，並在歸零後保留收斂寬限
                    s_pauseGraceFrames = kPauseFreezeGraceFrames;
                }
                else if (s_pauseGraceFrames > 0)
                {
                    s_pauseGraceFrames--;
                }
                else
                {
                    s_simulationSystem ??= instance.World.GetExistingSystemManaged<SimulationSystem>();
                    if (s_simulationSystem != null && s_simulationSystem.selectedSpeed == 0f)
                    {
                        freeze = true;
                    }
                }
            }

            if (freeze)
            {
                instance.WaterSimSpeed = 0;
            }

            if (s_pauseFrozen != freeze)
            {
                s_pauseFrozen = freeze;
                ModLog.Patch(Tag, freeze
                    ? "游戏暂停，水模拟已冻结 (PauseFreeze)"
                    : "水模拟已恢复 (PauseFreeze 解除)");
            }
        }

        private static int GetTerrainChangeCounter(WaterSystem instance)
        {
            if (!s_terrainCounterResolved)
            {
                s_terrainCounterResolved = true;
                s_terrainCounterField = AccessTools.Field(typeof(WaterSystem), "m_terrainChangeCounter");
                if (s_terrainCounterField == null)
                    ModLog.Warn(Tag, "无法解析 m_terrainChangeCounter，暂停冻结将不对地形变更让行");
            }
            return s_terrainCounterField != null ? (int)s_terrainCounterField.GetValue(instance) : 0;
        }

        // --- Async Compute Helper ---

        /// <summary>上次套用的 IsAsync 值，僅用於避免每幀重複 log。</summary>
        private static bool? s_lastAppliedAsync = null;

        /// <summary>
        /// 依 ResolutionManager.WaterAsyncCompute 設定 WaterSystem.IsAsync。
        /// 每幀呼叫，僅在值變化時記錄日誌。
        /// </summary>
        private static void ApplyAsyncCompute(WaterSystem instance)
        {
            bool desired = ResolutionManager.WaterAsyncCompute;
            if (instance.IsAsync != desired)
            {
                instance.IsAsync = desired;
            }
            if (s_lastAppliedAsync != desired)
            {
                s_lastAppliedAsync = desired;
                ModLog.Patch(Tag, $"WaterSystem.IsAsync = {desired} (Async Compute {(desired ? "启用" : "关闭")})");
            }
        }

        // --- Backdrop Helpers ---

        private static System.Reflection.FieldInfo ResolveBackdropField()
        {
            if (!s_backdropFieldResolved)
            {
                s_backdropFieldResolved = true;
                s_simulateBackdropField = AccessTools.Field(typeof(WaterSystem), "m_SimulateBackdrop");
                if (s_simulateBackdropField == null)
                    ModLog.Warn(Tag, "无法解析 m_SimulateBackdrop 字段，背景水开关不可用");
            }
            return s_simulateBackdropField;
        }

        /// <summary>關閉背景水模擬與渲染，並同步 colossal_WaterParams shader 全域值（y 分量 = backdrop 開關）。</summary>
        private static void DisableBackdrop(WaterSystem instance)
        {
            var field = ResolveBackdropField();
            if (field == null) return;

            bool current = (bool)field.GetValue(instance);
            if (!current) return; // 地圖原生無背景水或已關閉

            if (!s_backdropOverridden)
            {
                s_backdropOverridden = true;
                s_originalBackdrop = true;
                ModLog.Patch(Tag, "背景水模拟已关闭 (WaterSimQuality)");
            }
            field.SetValue(instance, false);
            UnityEngine.Shader.SetGlobalVector("colossal_WaterParams",
                new UnityEngine.Vector4(instance.SeaLevel, 0f, 0f, 0f));
        }

        /// <summary>還原背景水為存檔原值（僅當本補丁曾覆寫時）。</summary>
        private static void RestoreBackdrop(WaterSystem instance)
        {
            if (!s_backdropOverridden) return;
            s_backdropOverridden = false;

            var field = ResolveBackdropField();
            if (field == null) return;

            field.SetValue(instance, s_originalBackdrop);
            UnityEngine.Shader.SetGlobalVector("colossal_WaterParams",
                new UnityEngine.Vector4(instance.SeaLevel, s_originalBackdrop ? 1f : 0f, 0f, 0f));
            ModLog.Patch(Tag, $"背景水模拟已还原为存档原值: {s_originalBackdrop}");
        }

        /// <summary>
        /// Postfix: 修复 Editor 水速横跳。
        /// 
        /// 机制：TerrainWillChange() 可能被 JIT 内联到 TerrainSystem.UpdateCascades 中，
        /// 导致 Harmony 无法 hook 它。因此在 OnSimulateGPU 完成后修复 speed 值。
        /// 
        /// Simulate() 内部流程（每帧）：
        /// 1. counter > 0: counter--, 当 counter==0 时设 speed=1（terrain 恢复完成）
        /// 2. speed > 0: 执行水模拟
        /// 3. OnSimulateGPU 返回后：TerrainWillChange 可能设 speed=0, counter=1
        /// 
        /// Postfix 在步骤 2 之后、步骤 3 之前执行。
        /// 此时 speed 应为 Simulate() 的最终意图值。
        /// 如果 speed=0 且非用户手动暂停（Paused_NoFlow），则恢复为 StableSpeed。
        /// </summary>
        [HarmonyPostfix]
        static void Postfix(WaterSystem __instance)
        {
            int postSpeed = __instance.WaterSimSpeed;

            // [BUGFIX] 地形變更 counter 倒數期間（放置建築 counter=1、筆刷 counter=15）讓行：
            // 原版 Simulate() 在 counter>0 時每幀跑恢復流程（RestoreHeightFromHeightmap 等），
            // 靠 speed=0 讓模擬循環跳過、僅在 counter 歸零時才恢復 speed=1（L1424-1426）。
            // 若此處把 speed 抬回，下一幀起恢復流程與模擬循環並行——
            // 模擬會在尚未恢復完成的水面高度上推進，筆刷後可能產生波紋/水位異常。
            // counter 歸零後原版自行設 speed=1，Editor 橫跳修復（下方分支）不受影響。
            if (postSpeed == 0 && GetTerrainChangeCounter(__instance) > 0)
            {
                return;
            }

            if (postSpeed == 0 && ResolutionManager.WaterSimQuality != WaterSimQualitySetting.Paused_NoFlow)
            {
                // speed=0 来自 TerrainWillChange 的瞬态重置 → 恢复用户值
                __instance.WaterSimSpeed = StableSpeed;
            }
            else if (postSpeed == 1 && StableSpeed > 1)
            {
                // Simulate() L1430 硬编码 speed=1（counter→0），
                // 但用户通过 Editor 面板设了更高值 → 恢复用户值
                __instance.WaterSimSpeed = StableSpeed;
            }
            // postSpeed > 1 → 用户刚设的新值，不干预
            // postSpeed == 1 && StableSpeed == 1 → 默认状态，不干预
        }
    }

    /// <summary>
    /// TerrainWillChange 防重复补丁（可能因 JIT 内联而不生效，
    /// 但保留作为第一道防线。主要修复逻辑在 OnSimulateGPU Postfix 中）。
    /// </summary>
    [HarmonyPatch(typeof(WaterSystem), nameof(WaterSystem.TerrainWillChange))]
    internal static class WaterSystem_TerrainWillChange_Patch
    {
        private static System.Reflection.FieldInfo s_counterField;
        private static bool s_fieldResolved = false;

        [HarmonyPrefix]
        static bool Prefix(WaterSystem __instance)
        {
            if (!s_fieldResolved)
            {
                s_counterField = AccessTools.Field(typeof(WaterSystem), "m_terrainChangeCounter");
                s_fieldResolved = true;
            }

            if (s_counterField == null)
                return true;

            // 只设 counter，不碰 speed
            s_counterField.SetValue(__instance, 1);
            return false;
        }
    }
}
