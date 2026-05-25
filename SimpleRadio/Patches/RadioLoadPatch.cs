using System;
using Game.Audio.Radio;
using HarmonyLib;
using SimpleRadio.Core;

namespace SimpleRadio.Patches
{
    /// <summary>
    /// 在 Radio.LoadRadio (private) 完成后注入自定义电台、恢复上次选择。
    /// </summary>
    [HarmonyPatch(typeof(Radio), "LoadRadio")]
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
