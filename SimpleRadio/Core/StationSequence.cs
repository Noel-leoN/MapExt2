using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Colossal.IO.AssetDatabase;

namespace SimpleRadio.Core
{
    /// <summary>
    /// 顺序模式的播放顺序。电台文件夹顶层有播放列表（.m3u／.m3u8）就先按列表，
    /// 列表没列到的文件按文件名排在后面；没有播放列表就全按文件名。
    /// 文件名按资源管理器的方式排序，数字按数值比较（2 排在 10 前面）。
    /// </summary>
    internal static class StationSequence
    {
        private static readonly string[] s_playlistExtensions = { ".m3u8", ".m3u" };

        private static readonly IComparer<string> s_fileNameComparer = Comparer<string>.Create(CompareFileNames);

        // 资源管理器同款排序，找不到时只警告一次
        private static bool s_logicalUnavailable;

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string x, string y);

        /// <summary>
        /// 排出电台的播放顺序。<paramref name="songs"/> 的键是文件名。
        /// </summary>
        internal static AudioAsset[] Build(string stationDir, string stationName, IReadOnlyList<KeyValuePair<string, AudioAsset>> songs)
        {
            var order = new List<AudioAsset>(songs.Count);
            var added = new HashSet<AudioAsset>();

            List<string> entries = ReadPlaylist(stationDir, stationName, out string playlistName);
            if (entries != null)
            {
                // 同一文件夹内文件名不重复（Windows 不区分大小写）
                var byName = new Dictionary<string, AudioAsset>(StringComparer.OrdinalIgnoreCase);
                foreach (var song in songs)
                {
                    byName[song.Key] = song.Value;
                }
                foreach (string entry in entries)
                {
                    if (byName.TryGetValue(entry, out AudioAsset asset) && added.Add(asset))
                    {
                        order.Add(asset);
                    }
                }

                int listed = order.Count;
                int unlisted = songs.Select(s => s.Value).Distinct().Count() - listed;
                if (listed == 0)
                {
                    Mod.Logger.Warn($"电台 '{stationName}' 的播放列表 {playlistName} 没有一条对得上本电台的文件，改按文件名排序。");
                }
                else
                {
                    Mod.Logger.Info(unlisted > 0
                        ? $"  电台 '{stationName}' 按播放列表 {playlistName} 排序：列出 {listed} 首，另有 {unlisted} 首未列出，按文件名排在后面"
                        : $"  电台 '{stationName}' 按播放列表 {playlistName} 排序：{listed} 首");
                }
            }

            foreach (var song in songs.OrderBy(s => s.Key, s_fileNameComparer))
            {
                if (added.Add(song.Value))
                {
                    order.Add(song.Value);
                }
            }

            return order.ToArray();
        }

        /// <summary>
        /// 只按文件名排序（片段没有登记歌单时的退路）。
        /// </summary>
        internal static AudioAsset[] SortByFileName(IEnumerable<AudioAsset> clips)
        {
            return clips.OrderBy(FileNameOf, s_fileNameComparer).ToArray();
        }

        // 取不到路径时排在最前，不让退路本身抛错
        private static string FileNameOf(AudioAsset clip)
        {
            try
            {
                return Path.GetFileName(clip.path ?? string.Empty);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 读出播放列表里的文件名，按出现顺序；没有播放列表或读不了时返回 null。
        /// </summary>
        private static List<string> ReadPlaylist(string stationDir, string stationName, out string playlistName)
        {
            playlistName = null;
            try
            {
                string[] playlists = Directory.GetFiles(stationDir)
                    .Where(f => s_playlistExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .OrderBy(Path.GetFileName, s_fileNameComparer)
                    .ToArray();
                if (playlists.Length == 0) return null;

                playlistName = Path.GetFileName(playlists[0]);
                if (playlists.Length > 1)
                {
                    Mod.Logger.Warn($"电台 '{stationName}' 有 {playlists.Length} 个播放列表，只用 {playlistName}。");
                }

                // 有 BOM 按 BOM；没有就按 UTF-8 严格解码，遇到非法字节抛出，不拿乱码去匹配
                string text;
                using (var reader = new StreamReader(playlists[0], new UTF8Encoding(false, true), true))
                {
                    text = reader.ReadToEnd();
                }

                var entries = new List<string>();
                foreach (string raw in text.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;

                    string name = EntryFileName(line);
                    if (!string.IsNullOrEmpty(name))
                    {
                        entries.Add(name);
                    }
                }
                return entries;
            }
            catch (DecoderFallbackException)
            {
                Mod.Logger.Warn($"电台 '{stationName}' 的播放列表 {playlistName} 不是 UTF-8 编码，已忽略；请另存为 UTF-8（.m3u8 默认即是）。");
                return null;
            }
            catch (Exception e)
            {
                Mod.Logger.Warn(e, $"读取电台 '{stationName}' 的播放列表失败，已忽略。");
                return null;
            }
        }

        // 条目只取文件名；file:/// 写法先转成本地路径，%xx 转义随之还原
        private static string EntryFileName(string entry)
        {
            try
            {
                if (entry.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                    && Uri.TryCreate(entry, UriKind.Absolute, out Uri uri) && uri.IsFile)
                {
                    entry = uri.LocalPath;
                }
                return Path.GetFileName(entry);
            }
            catch (ArgumentException)
            {
                // 含路径非法字符，不是文件条目
                return null;
            }
        }

        private static int CompareFileNames(string x, string y)
        {
            if (!s_logicalUnavailable)
            {
                try
                {
                    return StrCmpLogicalW(x, y);
                }
                catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
                {
                    s_logicalUnavailable = true;
                    Mod.Logger.Warn("系统不支持 StrCmpLogicalW，文件名改按字符顺序排列。");
                }
            }
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
    }
}
