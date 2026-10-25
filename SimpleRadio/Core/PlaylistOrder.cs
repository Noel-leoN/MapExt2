using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Colossal.IO.AssetDatabase;
using Game.Audio.Radio;
using HarmonyLib;
using SimpleRadio.Settings;
using static Game.Audio.Radio.Radio;

namespace SimpleRadio.Core
{
    /// <summary>
    /// 自定义电台每一轮的播放顺序。
    ///
    /// <para><b>一轮</b></para>
    /// 每个电台只有一个 Playlist 片段，一轮就是把歌单从头到尾排进队列一遍：新一轮开始时
    /// 按当前模式排好整轮，之后原版沿片段游标 <c>m_CurrentClipId</c> 逐首排队。
    /// 原版每次准备片段都会回调 <c>GetPlaylistClips</c>，切回本台、每日节目重置时也会，
    /// 所以要分清"新一轮"和"本轮进行中"。原版的 <c>isSetUp</c> 由 <c>Setup()</c> 置位、
    /// <c>Reset()</c> 清除，而原版自己从不调用 <c>Setup()</c>；这里排好一轮后调用它，
    /// 一轮最后一首排进队列时 <c>GoToNextClip</c> 调用 <c>Reset()</c>，下一次回调就是新一轮。
    /// 不能用"游标为 0"判断：上一轮最后一首排进队列后游标就归零，新一轮的第一首
    /// 要等上一首播完才排队，这段时间里的回调会把排好的新一轮再洗一遍。
    ///
    /// <para><b>播放顺序</b></para>
    /// 随机：新一轮的前 k 位不放上一轮最后 k 首，k = ⌊(N−1)/2⌋，N ≥ 2 时至少为 1，
    /// 跨轮重复之间至少隔 k 首。k 取 ⌊N/2⌋ 时偶数首会退化：4 首、k = 2 时，前两位只能放
    /// 上一轮前两位那两首，前后两半永远不混。
    /// 顺序：加载时排好的固定顺序，见 <see cref="StationSequence"/>。
    ///
    /// <para><b>切换即时生效</b></para>
    /// 设置页改了模式就重排当前电台：已排进队列的那首照常播完，之后按新模式继续。
    /// 其他电台在下次切回时重排。游标读写不了就留到下一轮生效。
    ///
    /// <para><b>切回时补播</b></para>
    /// 原版切台时 <c>ClearQueue</c> 会丢掉旧电台已排队、还没开播的那首，游标却已越过它。
    /// 切走前记下它，切回后若它正是最近排进队列的那首，就让它先播。排着的若是紧急广播
    /// 或"上一首"的回放，位置对不上，就不补；被切断的当前曲不补，与原版一致。
    /// </summary>
    internal static class PlaylistOrder
    {
        /// <summary>
        /// 单个电台片段的排序状态，随 RuntimeSegment 回收，刷新电台时整批换新。
        /// </summary>
        private sealed class State
        {
            // 加载时登记的歌单，也是顺序模式的播放顺序
            internal AudioAsset[] Songs;

            // 当前这一轮按哪种模式排的
            internal PlaybackOrder Applied;

            // 是否排过；首次回调时 clips 是原版建的两份，没有"上一轮"
            internal bool Arranged;

            // 上一轮最后 k 首，最后一个是上一轮最后排进队列的
            internal AudioAsset[] Tail = Array.Empty<AudioAsset>();

            // 上次切走时被原版丢掉的排队曲
            internal AudioAsset Dropped;
        }

        private static readonly ConditionalWeakTable<RuntimeSegment, State> s_states = new ConditionalWeakTable<RuntimeSegment, State>();

        // 片段游标：下一首要排进队列的位置
        private static readonly FieldInfo s_cursorField = AccessTools.Field(typeof(RuntimeSegment), "m_CurrentClipId");

        // 片段计数：达到 clipsCap 时原版提前结束片段；写游标时清零，免得提前触发
        private static readonly FieldInfo s_capCountField = AccessTools.Field(typeof(RuntimeSegment), "m_CapCount");

        // 只在主线程由 Radio 和设置页调用，共用一个实例即可；
        // .NET Framework 的 new Random() 以系统 tick 为种子，同一 tick 内新建的实例序列相同
        private static readonly Random s_rng = new Random();

        // 反射失败或状态缺失只警告一次
        private static bool s_cursorWarned;
        private static bool s_capCountWarned;
        private static bool s_stateWarned;

        private static PlaybackOrder CurrentMode => Mod.Instance?.Settings?.PlaybackOrder ?? PlaybackOrder.Shuffle;

        // === 登记与回调 ===

        /// <summary>
        /// 登记电台的歌单，由 StationLoader 在加载和刷新时调用。
        /// </summary>
        internal static void Register(RuntimeRadioChannel channel, AudioAsset[] songs)
        {
            if (channel?.schedule == null) return;

            foreach (RuntimeProgram program in channel.schedule)
            {
                foreach (RuntimeSegment segment in program.segments)
                {
                    s_states.Remove(segment);
                    s_states.Add(segment, new State { Songs = songs });
                }
            }
        }

        /// <summary>
        /// 原版准备片段时的回调（<c>GetPlaylistClips</c> 的 Prefix）：新一轮才排序，
        /// 本轮进行中只在模式变了时重排。
        /// </summary>
        internal static void OnSetupSegment(RuntimeSegment segment)
        {
            State state = GetState(segment);
            PlaybackOrder mode = CurrentMode;
            if (!segment.isSetUp)
            {
                StartRound(segment, state, mode);
            }
            else if (state.Applied != mode)
            {
                Rearrange(segment, state, mode);
            }
        }

        /// <summary>
        /// 设置页改了播放顺序：立即重排当前电台，其他电台在下次切回时重排。
        /// </summary>
        internal static void OnModeChanged()
        {
            try
            {
                Radio radio = StationLoader.RadioInstance;
                if (radio == null || !TryGetSegment(radio.currentChannel, out RuntimeSegment segment, out State state)) return;

                // 还没排好的一轮会在下次回调时按新模式排
                PlaybackOrder mode = CurrentMode;
                if (segment.isSetUp && state.Applied != mode)
                {
                    Rearrange(segment, state, mode);
                }
            }
            catch (Exception e)
            {
                Mod.Logger.Error(e, "切换播放顺序失败，将在下一轮生效");
            }
        }

        /// <summary>
        /// 切台前（<c>currentChannel</c> setter 的 Prefix）：记下旧电台队列里排着的那首。
        /// </summary>
        internal static void OnLeavingChannel(Radio radio, RuntimeRadioChannel next)
        {
            RuntimeRadioChannel current = radio.currentChannel;
            if (current == null || current == next) return;

            if (TryGetSegment(current, out _, out State state))
            {
                state.Dropped = radio.pendingClip;
            }
        }

        /// <summary>
        /// 切台后（Postfix）：上次切走时丢掉的那首若正是最近排进队列的，就让它先播。
        /// </summary>
        internal static void OnEnteredChannel(Radio radio)
        {
            if (!TryGetSegment(radio.currentChannel, out RuntimeSegment segment, out State state)) return;

            AudioAsset dropped = state.Dropped;
            state.Dropped = null;
            if (dropped == null || !segment.isSetUp || !TryReadCursor(segment, out int cursor)) return;

            IReadOnlyList<AudioAsset> clips = segment.clips;
            if (LastQueued(clips, cursor, state) != dropped) return;

            if (cursor > 0)
            {
                TryWriteCursor(segment, cursor - 1);
                return;
            }

            // 游标为 0：丢掉的是上一轮最后一首，新一轮已经排好
            int index = IndexOf(clips, dropped);
            if (index < 0) return;

            if (state.Applied == PlaybackOrder.Sequential)
            {
                // 先播它，播完回到开头（它通常就在最后一位）
                TryWriteCursor(segment, index);
            }
            else if (index > 0)
            {
                AudioAsset[] round = clips.ToArray();
                (round[0], round[index]) = (round[index], round[0]);
                segment.clips = round;
            }
        }

        // === 排序 ===

        /// <summary>
        /// 新一轮：按当前模式排好整轮，并标记本轮已排好。
        /// </summary>
        private static void StartRound(RuntimeSegment segment, State state, PlaybackOrder mode)
        {
            AudioAsset[] round = (AudioAsset[])state.Songs.Clone();
            int k = AvoidCount(round.Length);
            state.Tail = state.Arranged ? TakeLast(segment.clips, segment.clips.Count, k) : Array.Empty<AudioAsset>();
            if (mode == PlaybackOrder.Shuffle)
            {
                ShuffleFrom(round, 0, state.Tail, k);
            }

            state.Arranged = true;
            state.Applied = mode;
            segment.clipsCap = round.Length;
            segment.clips = round;
            segment.Setup();
        }

        /// <summary>
        /// 本轮进行中换了模式：已排进队列的保持不动，之后的按新模式重排。
        /// </summary>
        private static void Rearrange(RuntimeSegment segment, State state, PlaybackOrder mode)
        {
            if (!TryReadCursor(segment, out int cursor)) return;

            AudioAsset[] clips = segment.clips.ToArray();
            if (mode == PlaybackOrder.Sequential)
            {
                // 接在最近排进队列的那首后面；它不在歌单里（如紧急广播）就从头开始
                AudioAsset[] sequence = (AudioAsset[])state.Songs.Clone();
                int next = Array.IndexOf(sequence, LastQueued(clips, cursor, state)) + 1;
                if (next >= sequence.Length) next = 0;
                if (!TryWriteCursor(segment, next)) return;

                // 绕回开头相当于新一轮开始：上一轮末尾改成刚排进队列的那几首
                if (next == 0 && cursor > 0)
                {
                    state.Tail = TakeLast(clips, cursor, AvoidCount(sequence.Length));
                }

                segment.clipsCap = sequence.Length;
                segment.clips = sequence;
            }
            else
            {
                ShuffleFrom(clips, cursor, state.Tail, AvoidCount(clips.Length));
                segment.clips = clips;
            }

            state.Applied = mode;
        }

        /// <summary>
        /// 打乱 <paramref name="clips"/> 从 <paramref name="start"/> 起的部分；前 <paramref name="k"/> 位里
        /// 还没定下的位置只放 <paramref name="recent"/> 以外的歌。前段从这些歌里抽、其余混在一起
        /// 打乱放后段，在满足约束的排列里是均匀随机的。
        /// </summary>
        private static void ShuffleFrom(AudioAsset[] clips, int start, AudioAsset[] recent, int k)
        {
            int front = k - start;
            if (front <= 0 || recent.Length == 0)
            {
                Shuffle(clips, start, clips.Length - start);
                return;
            }

            var fresh = new List<AudioAsset>(clips.Length - start);
            var stale = new List<AudioAsset>(recent.Length);
            for (int i = start; i < clips.Length; i++)
            {
                (Array.IndexOf(recent, clips[i]) >= 0 ? stale : fresh).Add(clips[i]);
            }

            Shuffle(fresh, 0, fresh.Count);
            front = Math.Min(front, fresh.Count);
            var back = new List<AudioAsset>(fresh.Count - front + stale.Count);
            for (int i = front; i < fresh.Count; i++)
            {
                back.Add(fresh[i]);
            }
            back.AddRange(stale);
            Shuffle(back, 0, back.Count);

            int pos = start;
            for (int i = 0; i < front; i++)
            {
                clips[pos++] = fresh[i];
            }
            foreach (AudioAsset clip in back)
            {
                clips[pos++] = clip;
            }
        }

        // Fisher–Yates
        private static void Shuffle(IList<AudioAsset> list, int start, int count)
        {
            for (int i = count - 1; i > 0; i--)
            {
                int j = s_rng.Next(i + 1);
                (list[start + i], list[start + j]) = (list[start + j], list[start + i]);
            }
        }

        // 新一轮前几位要避开上一轮末尾几首
        private static int AvoidCount(int count) => count < 2 ? 0 : Math.Max(1, (count - 1) / 2);

        // === 辅助 ===

        private static State GetState(RuntimeSegment segment)
        {
            if (s_states.TryGetValue(segment, out State state)) return state;

            // 电台都经 StationLoader 登记，正常走不到这里；退回只按文件名排序
            if (!s_stateWarned)
            {
                s_stateWarned = true;
                Mod.Logger.Warn("有电台片段没有登记歌单，改用片段自带的歌曲，播放列表不生效。");
            }
            state = new State { Songs = StationSequence.SortByFileName(segment.clips.Distinct()) };
            s_states.Add(segment, state);
            return state;
        }

        // 本 Mod 的电台只有一个节目、一个 Playlist 片段
        private static bool TryGetSegment(RuntimeRadioChannel channel, out RuntimeSegment segment, out State state)
        {
            segment = null;
            state = null;
            if (channel?.schedule == null || channel.network != StationLoader.NetworkKey) return false;

            foreach (RuntimeProgram program in channel.schedule)
            {
                foreach (RuntimeSegment candidate in program.segments)
                {
                    if (s_states.TryGetValue(candidate, out state))
                    {
                        segment = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        // 最近排进队列的那首：本轮游标的前一位；本轮还没排过就是上一轮最后一首
        private static AudioAsset LastQueued(IReadOnlyList<AudioAsset> clips, int cursor, State state)
        {
            if (cursor > 0) return cursor <= clips.Count ? clips[cursor - 1] : null;
            return state.Tail.Length > 0 ? state.Tail[state.Tail.Length - 1] : null;
        }

        // clips[0..end) 的最后 count 首
        private static AudioAsset[] TakeLast(IReadOnlyList<AudioAsset> clips, int end, int count)
        {
            count = Math.Min(count, end);
            var tail = new AudioAsset[count];
            for (int i = 0; i < count; i++)
            {
                tail[i] = clips[end - count + i];
            }
            return tail;
        }

        private static int IndexOf(IReadOnlyList<AudioAsset> clips, AudioAsset clip)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] == clip) return i;
            }
            return -1;
        }

        private static bool TryReadCursor(RuntimeSegment segment, out int cursor)
        {
            cursor = 0;
            if (s_cursorField == null)
            {
                WarnCursorMissing();
                return false;
            }

            cursor = (int)s_cursorField.GetValue(segment);
            return true;
        }

        private static bool TryWriteCursor(RuntimeSegment segment, int cursor)
        {
            if (s_cursorField == null)
            {
                WarnCursorMissing();
                return false;
            }

            s_cursorField.SetValue(segment, cursor);
            if (s_capCountField != null)
            {
                s_capCountField.SetValue(segment, 0);
            }
            else if (!s_capCountWarned)
            {
                // 计数没清零时片段会提前结束一次，原版下一帧重置节目后从游标处接着播
                s_capCountWarned = true;
                Mod.Logger.Warn("找不到 RuntimeSegment.m_CapCount，调整播放位置后片段可能提前结束一次，不影响播放顺序。");
            }
            return true;
        }

        private static void WarnCursorMissing()
        {
            if (s_cursorWarned) return;
            s_cursorWarned = true;
            Mod.Logger.Warn("找不到 RuntimeSegment.m_CurrentClipId，切换播放顺序改为下一轮生效，切回电台时也不再补播被跳过的歌。");
        }
    }
}
