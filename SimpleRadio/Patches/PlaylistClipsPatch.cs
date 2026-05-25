using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.IO.AssetDatabase;
using Game.Audio.Radio;
using HarmonyLib;
using static Game.Audio.Radio.Radio;

namespace SimpleRadio.Patches
{
    /// <summary>
    /// 拦截 Radio.GetPlaylistClips 运行时回调。
    ///
    /// 原因：SetupOrSkipSegment 每次切换频道时通过 m_OnDemandClips delegate 调用
    /// GetPlaylistClips → GetSegmentAudioClip，后者在 AssetDatabase.global 中按 tags 搜索。
    /// 我们的 AudioAsset 注册在 AssetDatabase.user 中，global 搜索找不到
    /// → clipsCap > 搜索结果数 → IndexOutOfRangeException。
    ///
    /// 对策：检测到 SimpleRadio segment 时跳过原版搜索，直接使用已构建好的 clips。
    /// </summary>
    [HarmonyPatch(typeof(Radio), "GetPlaylistClips")]
    public static class PlaylistClipsPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Radio __instance, RuntimeSegment segment)
        {
            try
            {
                // 仅拦截 SimpleRadio 的 segment（通过 tags 中包含 NetworkKey 识别）
                if (segment.tags == null) return true;

                bool isOurSegment = false;
                foreach (var tag in segment.tags)
                {
                    if (tag != null && tag.Contains(Core.StationLoader.NetworkKey))
                    {
                        isOurSegment = true;
                        break;
                    }
                }

                if (!isOurSegment) return true;

                // --- 使用已有 clips 并随机重排 ---
                // 注：segment.clips 赋值依赖 RuntimeSegment.clips 可接受 AudioAsset[] 的隐式转换
                //     若游戏版本更新类型签名，外层 try/catch 会回退到原版逻辑
                if (segment.clips != null && segment.clips.Count > 0)
                {
                    var list = new List<AudioAsset>(segment.clips);
                    var rnd = new Random();
                    segment.clipsCap = list.Count;
                    segment.clips = list.OrderBy(_ => rnd.Next()).ToArray();
                }

                return false; // 跳过原版 GetPlaylistClips
            }
            catch (Exception e)
            {
                Mod.Logger.Error(e, "PlaylistClipsPatch.Prefix 失败，回退到原版");
                return true;
            }
        }
    }
}
