using System;
using Game.Audio.Radio;
using HarmonyLib;
using SimpleRadio.Core;

namespace SimpleRadio.Patches
{
    /// <summary>
    /// 在 Radio.LoadRadio (private) 完成后注入自定义电台、恢复上次选择。
    /// HarmonyAfter 确保在 ExtendedRadio 的 Postfix 之后执行，
    /// 因为 ExtendedRadio 会用 SetValue 覆写 Radio 内部字典引用，
    /// 先执行的 Postfix 注入的电台会被覆盖丢失。
    /// </summary>
    [HarmonyPatch(typeof(Radio), "LoadRadio")]
    [HarmonyAfter("ExtendedRadio.ExtendedRadioMod")]
    public static class RadioLoadPatch
    {
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
    }
}
