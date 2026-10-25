using System;
using Game.Audio.Radio;
using HarmonyLib;
using SimpleRadio.Core;

namespace SimpleRadio.Patches
{
    /// <summary>
    /// 在 Radio.LoadRadio (private) 完成後注入自定義電台、恢復上次選擇。
    ///
    /// 設計要點（軟獨佔架構）：
    /// - <b>Priority.First</b>：在其他 Postfix 之前完成注入。
    ///   SimpleRadio 與原版共用同一個 live 字典（m_Networks/m_RadioChannels），
    ///   原版 LoadRadio 每次 Clear() 內容但保留字典物件，in-place 注入即可，不需依賴他人排序。
    /// - <b>Finalizer</b>：LoadRadio 本身或任一 Postfix 抛出的未捕获异常，会经 Radio.Reload
    ///   一路传到 AudioManager.OnGameLoaded（沿途没有 try/catch），游戏随即判定 disabling system，
    ///   停用整个音频系统，所有电台都无法播放。Finalizer 记录并吞掉该异常，让音频系统继续工作。
    /// </summary>
    [HarmonyPatch(typeof(Radio), "LoadRadio")]
    public static class RadioLoadPatch
    {
        [HarmonyPriority(Priority.First)]
        [HarmonyPrefix]
        private static void Prefix(Radio __instance, bool enable, out StationSelection.LoadState __state)
        {
            __state = StationSelection.BeginLoad(__instance, enable);
        }

        [HarmonyPriority(Priority.First)]
        [HarmonyPostfix]
        private static void Postfix(Radio __instance, StationSelection.LoadState __state)
        {
            try
            {
                bool succeeded = StationLoader.InjectCustomStations(__instance);
                if (__state != null) __state.InjectionSucceeded = succeeded;
            }
            catch (Exception e)
            {
                Mod.Logger.Error(e, "[SimpleRadio] Failed to inject custom stations");
            }
        }

        /// <summary>
        /// Finalizer：Harmony 會把「原方法 + 所有 Postfix」包在同一個 try 內執行，
        /// 任一环节（原方法或任何一个 Postfix）抛出的异常都会进到这里。
        /// 回傳 null 即吞掉例外，阻止它上傳到 AudioManager.OnGameLoaded 而停用整個音訊系統。
        /// </summary>
        [HarmonyPriority(Priority.Last)]
        private static Exception Finalizer(Exception __exception, StationSelection.LoadState __state)
        {
            // 完整 Postfix 鏈結束後才查目標，讓其他 Mod 的同步注入有機會完成。
            StationSelection.CompleteLoad(__state, __exception);

            if (__exception != null)
            {
                Mod.Logger.Warn(
                    $"[SimpleRadio] Radio.LoadRadio 抛出未捕获的异常，已拦截，以免音频系统被停用。" +
                    $"原始异常：{__exception.GetType().Name}: {__exception.Message}");
            }

            // 回傳 null → 例外被吞掉，不再向上傳播
            return null;
        }
    }
}
