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
    /// - <b>Priority.First</b>：搶在其他電台 Mod（如 ExtendedRadio）的 Postfix 之前完成注入。
    ///   SimpleRadio 與原版共用同一個 live 字典（m_Networks/m_RadioChannels），
    ///   原版 LoadRadio 每次 Clear() 內容但保留字典物件，in-place 注入即可，不需依賴他人排序。
    /// - <b>Finalizer</b>：中和整條 Postfix 鏈的例外。第三方電台 Mod（如 ExtendedRadio）
    ///   的 Postfix 若拋出未捕捉例外，會裸奔穿過 Radio.Reload 上傳到 AudioManager.OnGameLoaded
    ///   （全程無 try/catch），導致遊戲判定「disabling system」→ 整個音訊系統停用，
    ///   連原版與 SimpleRadio 電台一起陪葬。Finalizer 吞掉該例外並記錄，讓音訊系統存活。
    /// </summary>
    [HarmonyPatch(typeof(Radio), "LoadRadio")]
    public static class RadioLoadPatch
    {
        [HarmonyPriority(Priority.First)]
        [HarmonyPostfix]
        public static void Postfix(Radio __instance)
        {
            try
            {
                StationLoader.InjectCustomStations(__instance);
            }
            catch (Exception e)
            {
                Mod.Logger.Error(e, "[SimpleRadio] Failed to inject custom stations");
            }
        }

        /// <summary>
        /// Finalizer：Harmony 會把「原方法 + 所有 Postfix」包在同一個 try 內執行，
        /// 任一環節（含第三方 Mod 的 Postfix）拋出的例外都會進到此處。
        /// 回傳 null 即吞掉例外，阻止它上傳到 AudioManager.OnGameLoaded 而停用整個音訊系統。
        /// </summary>
        public static Exception Finalizer(Exception __exception)
        {
            if (__exception != null)
            {
                Mod.Logger.Warn(
                    $"[SimpleRadio] 已中和第三方電台 Mod 於 Radio.LoadRadio 的未捕捉例外，" +
                    $"避免音訊系統被停用。原始例外：{__exception.GetType().Name}: {__exception.Message}");
            }

            // 回傳 null → 例外被吞掉，不再向上傳播
            return null;
        }
    }
}
