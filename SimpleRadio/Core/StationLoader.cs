using System;
using System.Collections.Generic;
using System.IO;
using Colossal.IO.AssetDatabase;
using Colossal.PSI.Environment;
using Game.Audio.Radio;
using HarmonyLib;
using static Game.Audio.Radio.Radio;

namespace SimpleRadio.Core
{
    /// <summary>
    /// 核心电台加载器：扫描 ModsData/SimpleRadio/ 目录，构建并注入自定义电台。
    /// 
    /// 目录结构:
    ///   ModsData/SimpleRadio/
    ///   ├── 我的摇滚电台/
    ///   │   ├── song1.ogg
    ///   │   ├── song2.ogg
    ///   │   └── icon.svg          ← 可选电台图标
    ///   └── Chill Vibes/
    ///       ├── lofi_1.ogg
    ///       └── lofi_2.ogg
    /// </summary>
    public static class StationLoader
    {
        // === Constants ===
        private const string DataFolder = "SimpleRadio";
        internal const string NetworkKey = "SimpleRadio_CustomNetwork";

        // === 统计数据（供设置面板展示） ===
        public static int LoadedStations { get; private set; }
        public static int LoadedSongs { get; private set; }

        /// <summary>
        /// 保存 Radio 实例引用，供热刷新使用。
        /// </summary>
        public static Radio RadioInstance { get; private set; }

        /// <summary>
        /// 获取数据目录的完整路径（使用系统反斜杠，适配 explorer.exe）。
        /// </summary>
        public static string GetDataPath()
        {
            // EnvPath.kUserDataPath 来自 Application.persistentDataPath，使用正斜杠
            // 转换为系统原生路径分隔符确保 explorer.exe 正确解析
            string rawPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", DataFolder);
            return rawPath.Replace('/', Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// 触发电台热刷新：调用 Radio.Reload() 重新加载所有电台。
        /// 我们的 Postfix 会在 LoadRadio 完成后自动再次执行。
        /// </summary>
        public static bool ReloadRadio()
        {
            if (RadioInstance == null)
            {
                Mod.Logger.Warn("Radio 实例尚未初始化（需先进入游戏地图），无法刷新。");
                return false;
            }

            try
            {
                Mod.Logger.Info("正在热刷新电台...");
                RadioInstance.Reload(true);
                return true;
            }
            catch (Exception e)
            {
                Mod.Logger.Error(e, "热刷新电台失败");
                return false;
            }
        }

        /// <summary>
        /// 在 Radio.LoadRadio Postfix 中调用，将自定义电台注入游戏。
        /// </summary>
        public static void InjectCustomStations(Radio radio)
        {
            // 保存引用供热刷新使用
            RadioInstance = radio;

            // === 1. 定位数据目录 ===
            string basePath = GetDataPath();
            if (!Directory.Exists(basePath))
            {
                try
                {
                    Directory.CreateDirectory(basePath);
                    Mod.Logger.Info($"数据目录已创建: {basePath}");
                    Mod.Logger.Info("请将 .ogg 文件放入子文件夹中，然后点击\"刷新电台\"或重启游戏。");
                }
                catch (Exception e)
                {
                    Mod.Logger.Error(e, $"无法创建数据目录: {basePath}");
                }
                return;
            }

            // === 2. 获取 Radio 私有字典（一次性 Traverse） ===
            var traverse = Traverse.Create(radio);
            var networks = traverse.Field<Dictionary<string, RadioNetwork>>("m_Networks").Value;
            var channels = traverse.Field<Dictionary<string, RuntimeRadioChannel>>("m_RadioChannels").Value;

            if (networks == null || channels == null)
            {
                Mod.Logger.Error("无法访问 Radio 内部字典，跳过加载。");
                return;
            }

            // === 3. 注册自定义网络 ===
            if (!networks.ContainsKey(NetworkKey))
            {
                networks[NetworkKey] = new RadioNetwork
                {
                    name = NetworkKey,
                    nameId = NetworkKey,
                    description = "SimpleRadio custom music stations",
                    icon = IconManager.NetworkIcon,
                    allowAds = false,
                    uiPriority = networks.Count
                };
            }

            // === 4. 扫描子目录并构建电台 ===
            LoadedStations = 0;
            LoadedSongs = 0;

            string[] stationDirs;
            try
            {
                stationDirs = Directory.GetDirectories(basePath);
            }
            catch (Exception e)
            {
                Mod.Logger.Error(e, $"无法读取数据目录: {basePath}");
                return;
            }

            foreach (var stationDir in stationDirs)
            {
                try
                {
                    LoadStation(stationDir, networks, channels);
                }
                catch (Exception e)
                {
                    Mod.Logger.Error(e, $"加载电台失败: {Path.GetFileName(stationDir)}");
                }
            }

            // === 5. 清除缓存让 UI 刷新 ===
            traverse.Field("m_CachedRadioChannelDescriptors").SetValue(null);

            Mod.Logger.Info($"[SimpleRadio] 加载完成: {LoadedStations} 个电台, {LoadedSongs} 首歌曲。");

            // 更新设置面板
            if (Mod.Instance?.Settings != null)
            {
                Mod.Instance.Settings.UpdateLoadInfo(LoadedStations, LoadedSongs);
            }

            // === 6. 订阅电台切换事件（保存当前选择） ===
            SubscribeChannelChange(radio);

            // === 7. 恢复上次选择的电台 ===
            RestoreLastStation(radio, channels);
        }

        /// <summary>
        /// 订阅 Radio.ClipChanged 事件，在用户切换到 SimpleRadio 电台时保存选择。
        /// 使用 Delegate.Combine 模式（与游戏内部一致）。
        /// </summary>
        private static void SubscribeChannelChange(Radio radio)
        {
            // Delegate.Remove(null, x) 返回 null，Delegate.Combine(null, x) 返回 x，两者均 null-safe
            radio.ClipChanged = (OnClipChanged)Delegate.Remove(radio.ClipChanged, new OnClipChanged(OnClipChanged));
            radio.ClipChanged = (OnClipChanged)Delegate.Combine(radio.ClipChanged, new OnClipChanged(OnClipChanged));
        }

        /// <summary>
        /// ClipChanged 回调：当播放的频道属于 SimpleRadio 网络时，保存频道名到设置。
        /// </summary>
        private static void OnClipChanged(Radio radio, AudioAsset asset)
        {
            try
            {
                var channel = radio.currentChannel;
                if (channel == null) return;

                var settings = Mod.Instance?.Settings;
                if (settings == null) return;

                if (channel.network == NetworkKey)
                {
                    // 当前频道属于 SimpleRadio → 保存
                    if (settings.LastStation != channel.name)
                    {
                        settings.LastStation = channel.name;
                    }
                }
                else if (!string.IsNullOrEmpty(settings.LastStation))
                {
                    // 用户已切换到其他电台 → 清空记忆（下次启动不强制恢复）
                    settings.LastStation = "";
                }
            }
            catch { /* 保存失败不影响播放 */ }
        }

        /// <summary>
        /// 恢复上次保存的电台选择。
        /// 
        /// 边缘情况处理:
        /// - 上次电台目录已删除/改名 → channels 中不存在 → 跳过
        /// - 设置为空/null → 跳过
        /// - Radio 已在播放 → 切换到保存的电台
        /// </summary>
        private static void RestoreLastStation(Radio radio, Dictionary<string, RuntimeRadioChannel> channels)
        {
            var settings = Mod.Instance?.Settings;
            if (settings == null || string.IsNullOrEmpty(settings.LastStation)) return;

            if (!channels.TryGetValue(settings.LastStation, out var channel))
            {
                Mod.Logger.Info($"上次电台 '{settings.LastStation}' 不存在（可能已删除），跳过恢复。");
                settings.LastStation = "";
                return;
            }

            radio.currentChannel = channel;
            Mod.Logger.Info($"已恢复上次电台: {settings.LastStation}");
        }

        /// <summary>
        /// 加载单个电台目录。
        /// </summary>
        private static void LoadStation(
            string stationDir,
            Dictionary<string, RadioNetwork> networks,
            Dictionary<string, RuntimeRadioChannel> channels)
        {
            string stationName = Path.GetFileName(stationDir);

            // channel.name = 字典 key（RadioUISystem 用 name 做字典查找，两者必须一致）
            string channelKey = stationName;

            if (channels.ContainsKey(channelKey))
            {
                Mod.Logger.Warn($"电台键名冲突，跳过: {channelKey}");
                return;
            }

            // --- 扫描 .ogg 文件 ---
            string[] oggFiles = Directory.GetFiles(stationDir, "*.ogg");
            if (oggFiles.Length == 0)
            {
                Mod.Logger.Warn($"电台 '{stationName}' 没有 .ogg 文件，跳过。");
                return;
            }

            // 使用 List 收集，避免 AddToArray 的 O(N²) 问题
            var clips = new List<AudioAsset>(oggFiles.Length);

            foreach (var oggFile in oggFiles)
            {
                var asset = AudioAssetHelper.LoadAndRegister(oggFile, stationName, NetworkKey);
                if (asset != null)
                {
                    clips.Add(asset);
                }
            }

            if (clips.Count == 0)
            {
                Mod.Logger.Warn($"电台 '{stationName}' 没有成功加载的音频，跳过。");
                return;
            }

            // --- 构建 Segment ---
            // tags 用于 PlaylistClipsPatch 识别 SimpleRadio segment（包含 NetworkKey）
            var clipsArray = clips.ToArray();
            var segment = new Segment
            {
                type = SegmentType.Playlist,
                clips = clipsArray,
                tags = new[] { $"radio channel:{stationName}", $"radio station:{NetworkKey}" },
                clipsCap = clipsArray.Length
            };

            // --- 构建 Program（全天候 24 小时循环） ---
            var program = new Program
            {
                name = $"{stationName} Program",
                description = stationName,
                icon = null,
                startTime = "00:00",
                endTime = "00:00",
                loopProgram = true,
                pairIntroOutro = false,
                segments = new[] { segment }
            };

            // --- 构建 RadioChannel ---
            var channel = new RadioChannel
            {
                name = stationName,
                nameId = stationName,
                description = $"Custom station: {stationName}",
                icon = IconManager.GetStationIcon(stationName, stationDir),
                network = NetworkKey,
                uiPriority = channels.Count,
                programs = new[] { program }
            };

            // --- 注册到游戏 ---
            channels[channelKey] = channel.CreateRuntime(stationDir);

            LoadedStations++;
            LoadedSongs += clips.Count;

            Mod.Logger.Info($"  ✓ 电台 '{stationName}': {clips.Count} 首歌曲");
        }
    }
}
