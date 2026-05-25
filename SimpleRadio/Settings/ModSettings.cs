using System;
using System.Diagnostics;
using System.IO;
using Colossal.IO.AssetDatabase;
using Colossal.PSI.Environment;
using Game.Modding;
using Game.Settings;
using SimpleRadio.Core;

namespace SimpleRadio.Settings
{
    /// <summary>
    /// SimpleRadio 设置面板。
    /// 提供只读信息展示、打开数据目录、热刷新电台功能。
    /// </summary>
    [FileLocation("ModsSettings/" + Mod.ModName + "/" + Mod.ModName)]
    [SettingsUITabOrder(kTabInfo)]
    [SettingsUIGroupOrder(kGroupStatus, kGroupActions)]
    [SettingsUIShowGroupName(kGroupStatus, kGroupActions)]
    public class ModSettings : ModSetting
    {
        // === Section/Group 常量 ===
        public const string kTabInfo = "Info";
        public const string kGroupStatus = "Status";
        public const string kGroupActions = "Actions";

        // === 内部状态 ===
        private int _stationCount;
        private int _songCount;
        private bool _hasLoaded;

        public ModSettings(IMod mod) : base(mod)
        {
        }

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
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsRadioNotReady))]
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
        /// 禁用条件：Radio 实例未初始化时返回 true，禁用刷新按钮。
        /// </summary>
        public bool IsRadioNotReady => StationLoader.RadioInstance == null;

        // === 持久化设置（隐藏，不显示在 UI 中） ===

        /// <summary>
        /// 上次退出时选择的电台名称，由设置系统自动持久化。
        /// </summary>
        [SettingsUIHidden]
        public string LastStation { get; set; } = "";

        // === 内部方法 ===

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
            LastStation = "";
        }
    }
}
