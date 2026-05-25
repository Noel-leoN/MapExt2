using System;
using System.IO;
using Colossal.PSI.Environment;
using Colossal.UI;

namespace SimpleRadio.Core
{
    /// <summary>
    /// COUI 图标资源管理器。
    /// 注册两个 COUI host：
    ///   - simpleradio     → Mods/SimpleRadio/（部署目录，含预设图标库）
    ///   - simpleradio-data → ModsData/SimpleRadio/（数据目录，含用户自定义 icon.svg）
    ///
    /// 图标优先级：
    ///   1. 电台目录下的 icon.svg（用户自定义）→ coui://simpleradio-data/电台名/icon.svg
    ///   2. Resources/StationIcons/station_XX.svg（hash 分配）→ coui://simpleradio/...
    ///   3. Resources/DefaultIcon.svg（兜底）
    /// </summary>
    public static class IconManager
    {
        // === COUI Host 配置 ===
        /// <summary>mod 部署目录的 COUI key（预设图标）</summary>
        public const string kResourceKey = "simpleradio";
        /// <summary>ModsData 目录的 COUI key（用户自定义图标）</summary>
        public const string kDataKey = "simpleradio-data";

        /// <summary>COUI 基础路径前缀</summary>
        public static readonly string COUIBasePath = $"coui://{kResourceKey}";

        /// <summary>网络级图标（选择进入 SimpleRadio 时显示）</summary>
        public static readonly string NetworkIcon = $"{COUIBasePath}/Resources/DefaultIcon.svg";

        // === 电台图标库 ===
        private const string StationIconDir = "Resources/StationIcons";
        private const string StationIconPrefix = "station_";

        private static string _modDir;
        private static bool _registered;
        private static bool _dataHostRegistered;

        /// <summary>已发现的电台图标 COUI 路径列表</summary>
        private static string[] _stationIcons = Array.Empty<string>();

        /// <summary>
        /// 注册 COUI host 并扫描电台图标库。
        /// </summary>
        public static void Register()
        {
            if (_registered) return;

            try
            {
                // 1. 注册 mod 部署目录（预设图标）
                _modDir = Path.Combine(EnvPath.kUserDataPath, "Mods", Mod.ModName)
                    .Replace('\\', '/');

                if (!Directory.Exists(_modDir))
                {
                    Mod.Logger.Warn($"Mod 部署目录不存在: {_modDir}");
                    return;
                }

                UIManager.defaultUISystem.AddHostLocation(kResourceKey, _modDir, false);
                _registered = true;
                Mod.Logger.Info($"COUI host 已注册: {kResourceKey} -> {_modDir}");

                // 2. 注册 ModsData 目录（用户自定义 icon.svg）
                string dataDir = StationLoader.GetDataPath().Replace('\\', '/');
                if (Directory.Exists(dataDir))
                {
                    UIManager.defaultUISystem.AddHostLocation(kDataKey, dataDir, false);
                    _dataHostRegistered = true;
                }

                // 3. 扫描预设图标库
                ScanStationIcons();
            }
            catch (Exception e)
            {
                Mod.Logger.Warn(e, "COUI host 注册失败，图标将不可用");
            }
        }

        /// <summary>
        /// 获取电台图标的 COUI 路径。
        /// </summary>
        /// <param name="stationName">电台名称（= 目录名，用于 hash 分配和 COUI 路径构建）</param>
        /// <param name="stationDir">电台目录的物理路径（用于检测自定义 icon.svg）</param>
        public static string GetStationIcon(string stationName, string stationDir)
        {
            // 1. 用户自定义图标（电台目录下的 icon.svg）
            if (_dataHostRegistered)
            {
                string customIcon = Path.Combine(stationDir, "icon.svg");
                if (File.Exists(customIcon))
                {
                    // stationDir = ModsData/SimpleRadio/电台名/
                    // COUI host simpleradio-data 映射到 ModsData/SimpleRadio/
                    // → coui://simpleradio-data/电台名/icon.svg
                    return $"coui://{kDataKey}/{stationName}/icon.svg";
                }
            }

            // 2. 预设图标库（确定性 hash 分配）
            if (_stationIcons.Length > 0)
            {
                int index = Math.Abs(stationName.GetHashCode()) % _stationIcons.Length;
                return _stationIcons[index];
            }

            // 3. 兜底
            return NetworkIcon;
        }

        /// <summary>
        /// 扫描 Resources/StationIcons/ 目录下的 station_XX.svg 文件。
        /// </summary>
        private static void ScanStationIcons()
        {
            string iconDir = Path.Combine(_modDir, StationIconDir).Replace('\\', '/');

            if (!Directory.Exists(iconDir))
            {
                Mod.Logger.Info($"电台图标目录不存在: {StationIconDir}，所有电台将使用默认图标。");
                return;
            }

            string[] files = Directory.GetFiles(iconDir, $"{StationIconPrefix}*.svg");
            Array.Sort(files);

            _stationIcons = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);
                _stationIcons[i] = $"{COUIBasePath}/{StationIconDir}/{fileName}";
            }

            Mod.Logger.Info($"已加载 {_stationIcons.Length} 个电台图标。");
        }

        /// <summary>
        /// 取消注册（游戏退出时调用，进程终止后资源自动释放）。
        /// </summary>
        public static void Unregister()
        {
            _registered = false;
            _dataHostRegistered = false;
            _stationIcons = Array.Empty<string>();
        }
    }
}
