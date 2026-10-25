using System;
using Game.Audio.Radio;
using HarmonyLib;
using SimpleRadio.Core;
using static Game.Audio.Radio.Radio;

namespace SimpleRadio.Patches
{
    /// <summary>
    /// 拦截 Radio.GetPlaylistClips 运行时回调。
    ///
    /// <para><b>為何需要這個 Prefix</b></para>
    /// <c>SetupOrSkipSegment</c> 每次切換頻道／節目變更時，透過 <c>m_OnDemandClips</c>
    /// delegate 呼叫 <c>GetPlaylistClips</c> → <c>GetSegmentAudioClip</c>，後者會在
    /// <c>AssetDatabase.global</c> 按 tags 重新搜一次，再依 <c>clipsCap</c> 建陣列。
    /// 那條路對本 Mod 有兩個問題：
    /// 一是每次切台都掃全庫 AudioAsset（原版電台歌曲數量可觀），
    /// 二是搜尋結果若少於 <c>clipsCap</c> 會直接
    /// <c>list[list2[num]]</c> 越界（<c>Radio.cs</c> 的 <c>GetSegmentAudioClip</c>）。
    /// 直接用注入時已建好的 clips 兩者都避開。
    ///
    /// <para><b>不用原版建的歌单</b></para>
    /// <c>AssetDatabase.global</c> 包含 <c>RegisterBuiltinDatabases()</c> 注册的 <c>user</c>，
    /// 标签搜索看得到本 Mod 注册在 <c>AssetDatabase.user</c> 的资产，所以
    /// <c>RuntimeProgram.BuildRuntimeSegments</c> 会把同一批歌加两次（<c>Segment.clips</c> 一次、
    /// <c>Segment.tags</c> 的搜索结果一次），<c>RuntimeSegment.clips</c> 变成 2N。
    /// 搜索结果还会带进已删除文件的旧资产：删掉音频再刷新，旧资产仍带着标签留在 user 数据库。
    /// 所以每轮都改用加载时登记的歌单，只含加载时实际存在的文件。
    ///
    /// <para><b>何时排序</b></para>
    /// 原版不只在一轮播完时调用这里：切回本台（<c>currentChannel</c> setter），以及每个
    /// 游戏日的最后一个 tick（时刻取整成 86400，00:00–24:00 节目失效一帧，下一帧重置），
    /// 都会经 <c>SetupOrSkipSegment</c> 再调用一次。只有新一轮才重排，其余时候保持本轮顺序，
    /// 否则听过的歌会提前回来、没听过的被挤掉。判定方法和排序规则见 <see cref="PlaylistOrder"/>。
    /// </summary>
    [HarmonyPatch(typeof(Radio), "GetPlaylistClips")]
    public static class PlaylistClipsPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(RuntimeSegment segment)
        {
            try
            {
                if (!IsOurSegment(segment)) return true;

                if (segment.clips != null && segment.clips.Count > 0)
                {
                    PlaylistOrder.OnSetupSegment(segment);
                }

                return false; // 跳过原版 GetPlaylistClips
            }
            catch (Exception e)
            {
                // 含游戏更新改了 RuntimeSegment 成员签名的情况
                Mod.Logger.Error(e, "PlaylistClipsPatch.Prefix 失败，回退到原版");
                return true;
            }
        }

        // 仅拦截 SimpleRadio 的 segment（通过 tags 中包含 NetworkKey 识别）
        private static bool IsOurSegment(RuntimeSegment segment)
        {
            if (segment.tags == null) return false;

            foreach (var tag in segment.tags)
            {
                if (tag != null && tag.Contains(StationLoader.NetworkKey)) return true;
            }

            return false;
        }
    }
}
