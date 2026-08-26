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

        // === 熱刷新狀態 ===
        // 熱刷新與讀檔的「恢復目標」不同：
        //   讀檔／新遊戲 → 原版存檔記錄的頻道（m_LastSaveRadioChannel）
        //   熱刷新       → 玩家刷新前正在聽的那個頻道
        // 原版 m_LastSaveRadioChannel 在整個 session 內不會被 Reload 清掉，
        // 若不分流，熱刷新會把電台跳回「當初讀檔時」那個，屬於回歸。
        private static bool s_isHotReload;
        private static string s_hotReloadChannel;

        /// <summary>
        /// 获取数据目录的完整路径（使用系统反斜杠，适配 explorer.exe）。
        /// </summary>
        public static string GetDataPath()
        {
            // EnvPath.kUserDataPath 来自 Application.persistentDataPath，使用正斜杠
            // 转换为系统原生路径分隔符并使用 Path.GetFullPath 确保 explorer.exe 在任何环境下都能正确解析
            string rawPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", DataFolder);
            return Path.GetFullPath(rawPath);
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

                // 先記下當下正在播的頻道，注入完成後再切回去。
                // 用 try/finally 確保 Reload 內部拋例外時旗標不會卡在 true，
                // 否則之後正常讀檔就不會恢復電台了。
                s_hotReloadChannel = RadioInstance.currentChannel?.name;
                s_isHotReload = true;
                try
                {
                    RadioInstance.Reload(true);
                }
                finally
                {
                    s_isHotReload = false;
                    s_hotReloadChannel = null;
                }
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
                    Mod.Logger.Info("请将音频文件（.ogg/.mp3/.wav）放入子文件夹中，然后点击\"刷新电台\"或重启游戏。");
                    // 目錄剛建好 → 補註冊 COUI data host，否則本場遊戲都用不到自訂 icon.svg
                    IconManager.EnsureDataHost();
                }
                catch (Exception e)
                {
                    Mod.Logger.Error(e, $"无法创建数据目录: {basePath}");
                }
                return;
            }

            // 目錄存在，但 Mod.OnLoad 執行時可能還不存在（首次安裝）→ 補一次
            IconManager.EnsureDataHost();

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

            // === 6. 恢复电台选择 ===
            RestoreChannel(radio, traverse, channels);
        }

        /// <summary>
        /// 恢復電台選擇。
        ///
        /// 為何必須由本 Mod 補這一步：原版 <c>LoadRadio</c> 在自己的方法體尾端就呼叫
        /// <c>Enable()</c>，而 <c>Enable()</c> 用存檔記錄的頻道名查 <c>m_RadioChannels</c>；
        /// 那一刻自訂電台還沒注入（本 Postfix 尚未執行），所以查不到就退回
        /// <c>radioChannelDescriptors[0]</c>。注入完成後在此重試即可命中。
        ///
        /// 恢復目標分兩種來源，見 <see cref="s_isHotReload"/> 的說明。
        /// </summary>
        private static void RestoreChannel(
            Radio radio,
            Traverse traverse,
            Dictionary<string, RuntimeRadioChannel> channels)
        {
            string target;

            if (s_isHotReload)
            {
                target = s_hotReloadChannel;
            }
            else
            {
                // 原版把「存檔時正在播的頻道名」寫進城市存檔（AudioManager.Serialize），
                // 且不區分來源 —— 自訂電台同樣被記錄，所以這就是最準的恢復依據，
                // 而且它綁存檔而非全域設定，多存檔之間不會互相汙染。
                target = null;
                try
                {
                    target = traverse.Field<string>("m_LastSaveRadioChannel").Value;
                }
                catch (Exception e)
                {
                    // 欄位改名／型別變動時 Traverse 會拋，降級為「不恢復」而非讓注入失敗
                    Mod.Logger.Warn(e, "无法读取原版存档电台名，跳过恢复。");
                }
            }

            if (string.IsNullOrEmpty(target)) return;

            // 原版 Enable() 已經選中同一個頻道時不必重設：
            // currentChannel 的 setter 會 FinishCurrentClip + ClearQueue，白做一次會打斷播放。
            if (radio.currentChannel != null && radio.currentChannel.name == target) return;

            if (!channels.TryGetValue(target, out var channel))
            {
                Mod.Logger.Info($"电台 '{target}' 不存在（可能已删除或改名），保持当前选择。");
                return;
            }

            radio.currentChannel = channel;
            Mod.Logger.Info($"已恢复电台: {target}");
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

            // --- 扫描所有启用格式的音频文件 ---
            var enabledExts = AudioFormatHelper.GetEnabledExtensions();
            var audioFiles = new List<string>();
            foreach (var ext in enabledExts)
            {
                audioFiles.AddRange(Directory.GetFiles(stationDir, "*" + ext));
            }

            if (audioFiles.Count == 0)
            {
                Mod.Logger.Warn($"电台 '{stationName}' 没有支持的音频文件，跳过。");
                return;
            }

            // 使用 List 收集，避免 AddToArray 的 O(N²) 问题
            var clips = new List<AudioAsset>(audioFiles.Count);

            foreach (var audioFile in audioFiles)
            {
                var asset = AudioAssetHelper.LoadAndRegister(audioFile, stationName, NetworkKey);
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
