using System;
using Game.Audio.Radio;
using HarmonyLib;
using SimpleRadio.Core;

namespace SimpleRadio.Patches
{
    /// <summary>
    /// 在頻道選擇完成後記錄記憶，包含暫停或關閉播放時的切台。
    /// </summary>
    [HarmonyPatch(typeof(Radio), nameof(Radio.currentChannel), MethodType.Setter)]
    internal static class RadioChannelPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Radio __instance)
        {
            try
            {
                StationSelection.OnChannelChanged(__instance);
            }
            catch (Exception e)
            {
                Mod.Logger.Warn(e, "記錄電台選擇失敗。");
            }
        }
    }
}
