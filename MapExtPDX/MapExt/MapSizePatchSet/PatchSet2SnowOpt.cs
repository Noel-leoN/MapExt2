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
    /// 副作用（UI 已誠實揭示）：凍結期間降雪不積累、融雪不消退，雪面保持現狀。
    ///
    /// 僅 Game 模式生效：編輯器做圖需要觀察雪積累效果。
    /// </summary>
    [HarmonyPatch(typeof(SnowSystem), "OnUpdate")]
    internal static class SnowSystemOptRuntimePatch
    {
        private const string Tag = "SnowOpt";

        private static bool s_overridden = false;
        private static int s_originalSpeed = 1;

        /// <summary>退出主菜單時重置會話狀態（SnowSimSpeed 不序列化，覆寫記錄跨會話必須作廢）。</summary>
        internal static void ResetSessionState()
        {
            s_overridden = false;
            ModLog.Info(Tag, "SnowOpt session state reset");
        }

        [HarmonyPrefix]
        static bool Prefix(SnowSystem __instance)
        {
            bool freeze = ResolutionManager.SnowSimFrozen
                && Game.SceneFlow.GameManager.instance != null
                && Game.SceneFlow.GameManager.instance.gameMode.IsGame();

            if (freeze)
            {
                if (!s_overridden)
                {
                    s_overridden = true;
                    // 記錄原速（防禦：原值若已為 0 則按預設 1 恢復，避免永久凍結）
                    s_originalSpeed = __instance.SnowSimSpeed > 0 ? __instance.SnowSimSpeed : 1;
                    ModLog.Patch(Tag, $"雪模拟已冻结 (SnowSimSpeed {s_originalSpeed} → 0)");
                }
                __instance.SnowSimSpeed = 0;
            }
            else if (s_overridden)
            {
                s_overridden = false;
                __instance.SnowSimSpeed = s_originalSpeed;
                ModLog.Patch(Tag, $"雪模拟已恢复 (SnowSimSpeed → {s_originalSpeed})");
            }

            // 始終執行原版 OnUpdate（speed=0 時循環自然跳過，backdrop 維護與紋理綁定照常）
            return true;
        }
    }
}
