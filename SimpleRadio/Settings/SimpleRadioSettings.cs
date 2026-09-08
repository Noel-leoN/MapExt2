using System;
using System.Diagnostics;
using System.IO;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.SceneFlow;
using Game.Settings;
using SimpleRadio.Core;

namespace SimpleRadio.Settings
{
    /// <summary>
    /// SimpleRadio 设置面板。
    /// 提供只读信息展示、打开数据目录、热刷新电台、格式开关功能。
    /// </summary>
    /// <remarks>
    /// 類名刻意不叫 <c>ModSettings</c>：<see cref="ModSetting.ApplyAndSave"/> 傳的是
    /// <c>GetType().Name</c>，而 AssetDatabase 以**短類名**比對並取首個命中即 break，
    /// 因此同工作區多個 Mod 共用 <c>ModSettings</c> 這個名字時，只有其中一個能成為寫入目標，
    /// 其餘的按下開關時寫進的是別人的 .coc。
    /// <c>[FileLocation]</c> 與 <c>LoadSettings</c> 的區塊名皆未變動，玩家既有 .coc 照樣載入。
    /// </remarks>
    [FileLocation("ModsSettings/" + Mod.ModName + "/" + Mod.ModName)]
    [SettingsUITabOrder(kTabInfo, kTabFormat)]
    [SettingsUIGroupOrder(kGroupStatus, kGroupActions, kGroupPlayback, kGroupFormats, kGroupCompat)]
    [SettingsUIShowGroupName(kGroupStatus, kGroupActions, kGroupPlayback, kGroupFormats, kGroupCompat)]
    public class SimpleRadioSettings : ModSetting
    {
        // === Section/Group 常量 ===
        public const string kTabInfo = "Info";
        public const string kTabFormat = "Format";
        public const string kGroupStatus = "Status";
        public const string kGroupActions = "Actions";
        public const string kGroupPlayback = "Playback";
        public const string kGroupFormats = "Formats";
        public const string kGroupCompat = "Compatibility";

        // === 相容性狀態的 locale key ===
        // 唯讀 string 屬性由引擎渲染「執行期值」而非 locale key，
        // 所以這兩句得自己查字典，否則三語介面都會露出英文硬字串。
        internal const string kLocaleExtendedRadioDetected = "SimpleRadio.EXTENDEDRADIO_DETECTED";
        internal const string kLocaleExtendedRadioMissing = "SimpleRadio.EXTENDEDRADIO_NOT_DETECTED";

        // === 内部状态 ===
        private int _stationCount;
        private int _songCount;
        private bool _hasLoaded;

        public SimpleRadioSettings(IMod mod) : base(mod)
        {
            // Setting.SetDefaults 是 abstract，而原版從不對 Mod 的設定呼叫它 —— 必須自己叫。
            // 目前預設值恰與屬性初始化器一致所以看不出差別，但任何只在 SetDefaults 裡
            // 賦值、不帶初始化器的欄位都會讓 LoadSettings 的 defaults diff 恆為空而靜默失效。
            SetDefaults();
        }

        // ================================================================
        // Info Tab
        // ================================================================

        // === 只读信息展示 ===

        [SettingsUISection(kTabInfo, kGroupStatus)]
        public string StationInfo => _hasLoaded ? $"{_stationCount}" : "—";

        [SettingsUISection(kTabInfo, kGroupStatus)]
        public string SongInfo => _hasLoaded ? $"{_songCount}" : "—";

        [SettingsUISection(kTabInfo, kGroupStatus)]
        public string DataPath => StationLoader.GetDataPath();

        // === 操作按钮 ===

        /// <summary>
        /// 打开数据目录按钮。
        /// </summary>
        [SettingsUISection(kTabInfo, kGroupActions)]
        [SettingsUIButton]
        public bool OpenDataFolder
        {
            // ReSharper disable once ValueParameterNotUsed
            set
            {
                try
                {
                    string path = StationLoader.GetDataPath();
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                    // 使用 ProcessStartInfo 确保路径正确传递（包含空格时也能正常工作）
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception e)
                {
                    Mod.Logger.Error(e, "无法打开数据目录");
                }
            }
        }

        /// <summary>
        /// 刷新电台按钮：重新扫描数据目录并加载新音乐，无需重启游戏。
        /// </summary>
        [SettingsUISection(kTabInfo, kGroupActions)]
        [SettingsUIButton]
        [SettingsUIDisableByCondition(typeof(SimpleRadioSettings), nameof(IsRadioNotReady))]
        public bool RefreshStations
        {
            // ReSharper disable once ValueParameterNotUsed
            set
            {
                if (StationLoader.ReloadRadio())
                {
                    Mod.Logger.Info($"电台刷新完成: {_stationCount} 个电台, {_songCount} 首歌曲");
                }
            }
        }

        /// <summary>
        /// 城市尚未就緒或正在載入時，停用刷新按鈕。
        /// </summary>
        public bool IsRadioNotReady => !StationSelection.CanRefresh;

        [SettingsUISection(kTabInfo, kGroupPlayback)]
        public bool RestoreLastStation { get; set; } = true;

        [SettingsUIHidden]
        public string LastStation { get; set; } = string.Empty;

        // ================================================================
        // Format Tab
        // ================================================================

        // === 格式开关 ===

        /// <summary>
        /// MP3 格式支持开关。
        /// </summary>
        [SettingsUISection(kTabFormat, kGroupFormats)]
        public bool EnableMP3 { get; set; } = true;

        /// <summary>
        /// WAV 格式支持开关。
        /// </summary>
        [SettingsUISection(kTabFormat, kGroupFormats)]
        public bool EnableWAV { get; set; } = true;

        // === 兼容性信息 ===

        /// <summary>
        /// 显示 ExtendedRadio 兼容状态。
        /// </summary>
        [SettingsUISection(kTabFormat, kGroupCompat)]
        public string ExtendedRadioStatus =>
            AudioFormatHelper.IsExtendedRadioLoaded
                ? Localize(kLocaleExtendedRadioDetected, "Detected - compatible")
                : Localize(kLocaleExtendedRadioMissing, "Not detected");

        /// <summary>
        /// 查活躍語言字典；查不到就回退英文字面值，不要把 key 露給玩家。
        /// </summary>
        private static string Localize(string key, string fallback)
        {
            try
            {
                var dict = GameManager.instance?.localizationManager?.activeDictionary;
                if (dict != null && dict.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            catch
            {
                // 設定頁不該因為查字典失敗而壞掉
            }

            return fallback;
        }

        // ================================================================
        // 内部方法
        // ================================================================

        /// <summary>
        /// 由 StationLoader 在加载完成后调用，更新统计信息。
        /// </summary>
        internal void UpdateLoadInfo(int stations, int songs)
        {
            _stationCount = stations;
            _songCount = songs;
            _hasLoaded = true;
        }

        public override void SetDefaults()
        {
            _stationCount = 0;
            _songCount = 0;
            _hasLoaded = false;
            EnableMP3 = true;
            EnableWAV = true;
            RestoreLastStation = true;
            LastStation = string.Empty;
        }
    }
}
