using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Colossal.IO.AssetDatabase;
using Game.Audio.Radio;
using HarmonyLib;
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
    /// <para><b>去重的原因</b></para>
    /// <c>AssetDatabase.global</c> 是「已註冊資料庫的集合」，而
    /// <c>RegisterBuiltinDatabases()</c> 註冊了 <c>user</c>，所以 global 的標籤搜尋
    /// <b>看得到</b>本 Mod 註冊在 <c>AssetDatabase.user</c> 的資產。
    /// 於是 <c>RuntimeProgram.BuildRuntimeSegments</c> 會把同一批歌加兩次
    /// —— 一次來自 <c>Segment.clips</c>，一次來自 <c>Segment.tags</c> 的搜尋結果 ——
    /// 使 <c>RuntimeSegment.clips</c> 變成 2N。原版 <c>GetSegmentAudioClip</c> 本來會
    /// 覆寫掉這份 2N，但本 Prefix 攔掉了那一步，若照抄就會讓每首歌一輪播兩次。
    /// 故此處以 <c>Distinct()</c> 去重（<c>AssetData</c> 覆寫了 <c>Equals</c>／
    /// <c>GetHashCode</c>，按 <c>id</c> 比對）。
    ///
    /// 已知殘留：若玩家刪掉音檔後熱刷新，被刪檔案的 AudioAsset 仍留在 user 資料庫且
    /// 帶著 tag，會被算成另一個 distinct 項進入清單；載入時 <c>LoadAsync</c> 失敗，
    /// 原版 <c>ValidateQueue</c> 會把它移出佇列，屬 session 內自癒，不另行處理。
    ///
    /// <para><b>只在新一轮开始时洗牌</b></para>
    /// 原版不只在一轮播完时调用这里：切回本台（<c>currentChannel</c> setter），以及每个
    /// 游戏日的最后一个 tick（时刻取整成 86400，00:00–24:00 节目失效一帧，下一帧重置），
    /// 都会经 <c>SetupOrSkipSegment</c> 再调用一次。而片段游标 <c>m_CurrentClipId</c>
    /// 只在一轮播完时由 <c>GoToNextClip</c> 归零，这些时候仍停在本轮中途。
    /// 若照样重洗，本轮剩余部分会换成新排列，听过的歌提前回来、没听过的被挤掉，
    /// 所以游标不为 0 时保持原顺序。
    /// </summary>
    [HarmonyPatch(typeof(Radio), "GetPlaylistClips")]
    public static class PlaylistClipsPatch
    {
        // .NET Framework 的 new Random() 以系統 tick 為種子（約 15ms 精度），
        // 同一 tick 內建立的多個實例會產生相同序列。切台雖不頻繁，
        // 但沒有理由每次都新建 —— 本 Prefix 只在主執行緒被 Radio.Update 呼叫，
        // 靜態實例無執行緒安全問題。
        private static readonly Random s_rng = new Random();

        // 片段游标：下一首要排进队列的位置，只在一轮播完时由原版 GoToNextClip 归零
        private static readonly FieldInfo s_nextClipField = AccessTools.Field(typeof(RuntimeSegment), "m_CurrentClipId");

        // 反射失败只警告一次
        private static bool s_nextClipWarned;

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

                if (segment.clips != null && segment.clips.Count > 0)
                {
                    // --- 本轮进行中：保持原顺序（见类注释） ---
                    if (!IsRoundStart(segment)) return false;

                    // --- 新一轮：去重后随机重排 ---
                    // 首次调用时 segment.clips 是 BuildRuntimeSegments 加成的两份（见类注释），
                    // 必须先 Distinct 再洗牌，否则每首歌一轮会播两次。
                    // 注：segment.clips 赋值依赖 RuntimeSegment.clips 可接受 AudioAsset[]；
                    //     若游戏版本更新类型签名，外层 try/catch 会回退到原版逻辑
                    AudioAsset previousLast = segment.clips[segment.clips.Count - 1];
                    var list = segment.clips.Distinct().ToList();
                    segment.clipsCap = list.Count;
                    var shuffled = list.OrderBy(_ => s_rng.Next()).ToArray();

                    // 上一轮的最后一首刚排进队列，新一轮若从它开始就会连播两遍
                    if (shuffled.Length > 1 && shuffled[0] == previousLast)
                    {
                        int swap = 1 + s_rng.Next(shuffled.Length - 1);
                        (shuffled[0], shuffled[swap]) = (shuffled[swap], shuffled[0]);
                    }

                    segment.clips = shuffled;
                }

                return false; // 跳过原版 GetPlaylistClips
            }
            catch (Exception e)
            {
                Mod.Logger.Error(e, "PlaylistClipsPatch.Prefix 失败，回退到原版");
                return true;
            }
        }

        /// <summary>
        /// 游标为 0 表示本轮还没有任何一首排进队列，此时重洗不会打乱进行中的一轮。
        /// 读不到字段（游戏更新改名）时退回旧行为：每次都重洗。
        /// </summary>
        private static bool IsRoundStart(RuntimeSegment segment)
        {
            if (s_nextClipField == null)
            {
                if (!s_nextClipWarned)
                {
                    s_nextClipWarned = true;
                    Mod.Logger.Warn("找不到 RuntimeSegment.m_CurrentClipId，改为每次都重洗，歌曲重复会变多。");
                }
                return true;
            }

            return (int)s_nextClipField.GetValue(segment) == 0;
        }
    }
}
