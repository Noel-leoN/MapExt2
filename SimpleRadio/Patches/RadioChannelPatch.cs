using System;
using Game.Audio.Radio;
using HarmonyLib;
using SimpleRadio.Core;
using static Game.Audio.Radio.Radio;

namespace SimpleRadio.Patches
{
    /// <summary>
    /// 切台补丁。
    /// Prefix 记下旧电台队列里排着的那首，原版随后会丢掉它；
    /// Postfix 在切回时让那首先播（见 <see cref="PlaylistOrder"/>），
    /// 并在频道选择完成后记录记忆，包含暂停或关闭播放时的切台。
    /// </summary>
    [HarmonyPatch(typeof(Radio), nameof(Radio.currentChannel), MethodType.Setter)]
    internal static class RadioChannelPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Radio __instance, RuntimeRadioChannel value)
        {
            try
            {
                PlaylistOrder.OnLeavingChannel(__instance, value);
            }
            catch (Exception e)
            {
                Mod.Logger.Warn(e, "记录切走时的排队曲失败。");
            }
        }

        [HarmonyPostfix]
        private static void Postfix(Radio __instance)
        {
            try
            {
                PlaylistOrder.OnEnteredChannel(__instance);
            }
            catch (Exception e)
            {
                Mod.Logger.Warn(e, "补播切走时被跳过的歌失败。");
            }

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
