// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    using Game; // GameModeExtensions.IsGame()
    using Game.Simulation;
    using HarmonyLib;
    using MapExtPDX.MapExt.Core;

    /// <summary>
    /// 雪模擬運行時凍結補丁。
    ///
    /// 背景：SnowSystem.OnUpdate（GameSimulation interval 4）中，AddSnow 對 1024² 雪深紋理
    /// 做全幅 compute dispatch（64×64 groups），C# 側無任何溫度/降雪門檻——加雪與融雪的
    /// 判斷全在 GPU kernel 內，夏季雪深全 0 時 dispatch 與紋理帶寬照付
    /// （kernel 同時讀 terrain cascade 與 WaterTexture）。SnowTransfer 有門檻
    /// （precipitation&lt;0.1 或海拔修正溫度&gt;0 時 early return），不在本補丁關注範圍。
    ///
    /// 凍結策略：設 SnowSimSpeed=0 跳過 AddSnow/SnowTransfer 循環，
    /// 保留 UpdateSnowBackdropTexture 與全域紋理綁定（3 個小 dispatch，成本可忽略）——
    /// 與水模擬「勿整段跳過 OnUpdate」的降頻哲學一致。
    ///
    /// 三檔語義（<see cref="SnowSimFreezeSetting"/>）：
    /// - Off：完全不干預，原版行為。
    /// - Auto：僅在「明確無雪」時凍結——氣溫高於融點加安全邊際、且非降水中。
    ///   入冬（氣溫接近融點）或開始降水即自動解凍，故無可見副作用，為預設檔。
    /// - Always：無條件凍結，最省；副作用為降雪不積累、融雪不消退（UI 已揭示）。
    ///
    /// 僅 Game 模式生效：編輯器做圖需要觀察雪積累效果。
    /// </summary>
    [HarmonyPatch(typeof(SnowSystem), "OnUpdate")]
    internal static class SnowSystemOptRuntimePatch
    {
        private const string Tag = "SnowOpt";

        /// <summary>
        /// Auto 檔的解凍安全邊際（°C）。
        /// 氣溫須高於 freezingTemperature + 此值才允許凍結，
        /// 讓入冬過程提前解凍、雪 kernel 有時間跟上，避免初雪延遲落地。
        /// </summary>
        private const float kAutoThawMargin = 3f;

        private static bool s_overridden = false;

        /// <summary>
        /// 使用者/原版設定的雪模擬速度（凍結期間的恢復目標）。
        /// SnowSystem.OnCreate 設 1；Dev Debug「Weather &amp; climate」面板可改。
        /// 見 <see cref="CaptureStableSpeed"/> 的偵測規則。
        /// </summary>
        internal static int StableSpeed = 1;

        /// <summary>本補丁上次寫入的值，用於區分「外部改寫」與「自己的覆寫」。</summary>
        private static int s_lastWritten = -1;

        private static ClimateSystem s_climateSystem;

        /// <summary>退出主菜單時重置會話狀態（SnowSimSpeed 不序列化，覆寫記錄跨會話必須作廢）。</summary>
        internal static void ResetSessionState()
        {
            s_overridden = false;
            StableSpeed = 1;
            s_lastWritten = -1;
            s_climateSystem = null; // World 重建，快取的系統引用失效
            ModLog.Info(Tag, "SnowOpt session state reset");
        }

        [HarmonyPrefix]
        static bool Prefix(SnowSystem __instance)
        {
            // === 捕獲使用者意圖值（在本補丁覆寫前）===
            CaptureStableSpeed(__instance);

            bool freeze = ShouldFreeze(__instance);

            if (freeze)
            {
                if (!s_overridden)
                {
                    s_overridden = true;
                    ModLog.Patch(Tag, $"雪模拟已冻结 (SnowSimSpeed {StableSpeed} → 0, 模式={ResolutionManager.SnowSimFreeze})");
                }
                __instance.SnowSimSpeed = 0;
                s_lastWritten = 0;
            }
            else if (s_overridden)
            {
                s_overridden = false;
                __instance.SnowSimSpeed = StableSpeed;
                s_lastWritten = StableSpeed;
                ModLog.Patch(Tag, $"雪模拟已恢复 (SnowSimSpeed → {StableSpeed})");
            }

            // 始終執行原版 OnUpdate（speed=0 時循環自然跳過，backdrop 維護與紋理綁定照常）
            return true;
        }

        /// <summary>
        /// 偵測並記錄外部（Dev Debug 面板等）寫入的雪模擬速度。
        ///
        /// 規則：當前值 &gt; 0 且不等於本補丁上次寫入的值 → 必定來自外部，記為新的恢復目標。
        /// 凍結期間玩家拖動面板（值被寫成非 0）同樣會在此被捕獲，
        /// 雖然本幀又被壓回 0（面板顯示 0），但解凍後會恢復到該新值，不再被吞掉。
        /// </summary>
        private static void CaptureStableSpeed(SnowSystem instance)
        {
            int current = instance.SnowSimSpeed;
            if (current > 0 && current != s_lastWritten && current != StableSpeed)
            {
                StableSpeed = current;
                ModLog.Info(Tag, $"检测到外部设置的雪模拟速度: {current}");
            }
        }

        /// <summary>依設定檔與氣候狀態判斷本幀是否應凍結。</summary>
        private static bool ShouldFreeze(SnowSystem instance)
        {
            var mode = ResolutionManager.SnowSimFreeze;
            if (mode == SnowSimFreezeSetting.Off)
                return false;

            // 僅 Game 模式：編輯器做圖需觀察雪積累
            var gm = Game.SceneFlow.GameManager.instance;
            if (gm == null || !gm.gameMode.IsGame())
                return false;

            if (mode == SnowSimFreezeSetting.Always)
                return true;

            // === Auto：僅在明確無雪季節凍結 ===
            return IsDefinitelySnowFree(instance);
        }

        /// <summary>
        /// 判斷當前是否「明確無雪」——氣溫高於融點加安全邊際，且未在降水。
        ///
        /// 保守設計：雪深資料只存在 GPU RenderTexture（無 CPU 副本，回讀成本不划算），
        /// 故不以「雪深為 0」為條件，而以氣候條件近似。誤判方向偏安全：
        /// 寧可少凍結（維持原版行為）也不錯凍結（雪該化卻不化）。
        /// 高山積雪在夏季本就靠 kernel 內的海拔溫度修正維持，凍結期間保持現狀亦合理。
        /// </summary>
        private static bool IsDefinitelySnowFree(SnowSystem instance)
        {
            var climate = ResolveClimateSystem(instance);
            if (climate == null)
                return false; // 拿不到氣候資料 → 不凍結（保守）

            // 降水中一律不凍結：可能正在下雪，也可能是雨後需要融雪收斂
            if (climate.isPrecipitating)
                return false;

            float temperature = climate.temperature;
            float freezing = climate.freezingTemperature;

            // 氣溫須明確高於融點（含安全邊際）才視為無雪季節
            return temperature > freezing + kAutoThawMargin;
        }

        private static ClimateSystem ResolveClimateSystem(SnowSystem instance)
        {
            if (s_climateSystem != null)
                return s_climateSystem;

            // SnowSystem 自身持有 m_ClimateSystem 引用，直接取用避免額外 World 查詢
            var world = instance.World;
            if (world == null)
                return null;

            s_climateSystem = world.GetExistingSystemManaged<ClimateSystem>();
            if (s_climateSystem == null)
                ModLog.Warn(Tag, "无法获取 ClimateSystem，Auto 档雪冻结不可用");

            return s_climateSystem;
        }
    }
}
