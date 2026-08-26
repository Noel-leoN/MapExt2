using System;
using System.IO;
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
        private static string _dataDir;

        /// <summary>Register 只跑一次的守衛（與「哪個 host 註冊成功」分開記）</summary>
        private static bool _initialized;
        /// <summary>部署目錄 host 是否註冊成功</summary>
        private static bool _resourceHostRegistered;
        /// <summary>ModsData 目錄 host 是否註冊成功</summary>
        private static bool _dataHostRegistered;

        /// <summary>已发现的电台图标 COUI 路径列表</summary>
        private static string[] _stationIcons = Array.Empty<string>();

        /// <summary>随机数生成器（用于电台图标随机分配）</summary>
        private static readonly Random _rng = new Random();

        /// <summary>
        /// 注册 COUI host 并扫描电台图标库。
        /// </summary>
        /// <param name="modDir">Mod 部署目录（由 Mod.OnLoad 通过 TryGetExecutableAsset 解析）</param>
        public static void Register(string modDir)
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                // 1. 注册 mod 部署目录（预设图标）
                if (string.IsNullOrEmpty(modDir) || !Directory.Exists(modDir))
                {
                    Mod.Logger.Warn($"Mod 部署目录无效或不存在: {modDir ?? "(null)"}，预设图标将不可用。");
                }
                else
                {
                    _modDir = modDir.Replace('\\', '/');
                    UIManager.defaultUISystem.AddHostLocation(kResourceKey, _modDir, false);
                    _resourceHostRegistered = true;
                    Mod.Logger.Info($"COUI host 已注册: {kResourceKey} -> {_modDir}");
                }

                // 2. 注册 ModsData 目录（用户自定义 icon.svg）
                //    首次安裝時該目錄還不存在，此處會註冊不到；
                //    目錄由 StationLoader 建立後會再呼叫 EnsureDataHost() 補上。
                EnsureDataHost();

                // 3. 扫描预设图标库
                if (_resourceHostRegistered) ScanStationIcons();
            }
            catch (Exception e)
            {
                Mod.Logger.Warn(e, "COUI host 注册失败，图标将不可用");
            }
        }

        /// <summary>
        /// 補註冊 ModsData 目錄的 COUI host。
        ///
        /// 首次安裝時 <see cref="Register"/> 執行於 <c>Mod.OnLoad</c>，那一刻
        /// <c>ModsData/SimpleRadio/</c> 尚未建立，host 註冊不上；而該目錄是
        /// <c>StationLoader.InjectCustomStations</c> 才建的。若不補這一步，
        /// 玩家整場遊戲都用不到自訂 <c>icon.svg</c>，必須重開遊戲。
        ///
        /// <c>AddHostLocation</c> 內部對相同 path 會直接忽略，重複呼叫安全。
        /// </summary>
        internal static void EnsureDataHost()
        {
            if (_dataHostRegistered) return;

            try
            {
                string dataDir = StationLoader.GetDataPath().Replace('\\', '/');
                if (!Directory.Exists(dataDir)) return;

                UIManager.defaultUISystem.AddHostLocation(kDataKey, dataDir, false);
                _dataDir = dataDir;
                _dataHostRegistered = true;
                Mod.Logger.Info($"COUI host 已注册: {kDataKey} -> {dataDir}");
            }
            catch (Exception e)
            {
                Mod.Logger.Warn(e, "ModsData COUI host 注册失败，自定义图标将不可用");
            }
        }

        /// <summary>
        /// 获取电台图标的 COUI 路径。
        /// </summary>
        /// <param name="stationName">电台名称（= 目录名，用于 COUI 路径构建）</param>
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

            // 2. 预设图标库（随机分配，每次加载可能不同，增加趣味性）
            if (_stationIcons.Length > 0)
            {
                int index = _rng.Next(_stationIcons.Length);
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
        /// 解除 COUI host 註冊（<c>Mod.OnDispose</c> 呼叫）。
        ///
        /// host location 是註冊在自己 world 之外的狀態，必須主動撤銷。
        /// <b>只能用雙參數版 <c>RemoveHostLocation(host, path)</c></b>：
        /// 單參數版會 Remove 整個 host key，連帶砍掉其他 Mod 註冊在同一 host
        /// 名下的所有路徑。
        /// </summary>
        public static void Unregister()
        {
            try
            {
                if (_resourceHostRegistered && !string.IsNullOrEmpty(_modDir))
                {
                    UIManager.defaultUISystem.RemoveHostLocation(kResourceKey, _modDir);
                }

                if (_dataHostRegistered && !string.IsNullOrEmpty(_dataDir))
                {
                    UIManager.defaultUISystem.RemoveHostLocation(kDataKey, _dataDir);
                }
            }
            catch (Exception e)
            {
                Mod.Logger.Warn(e, "COUI host 解除注册失败");
            }

            _initialized = false;
            _resourceHostRegistered = false;
            _dataHostRegistered = false;
            _modDir = null;
            _dataDir = null;
            _stationIcons = Array.Empty<string>();
        }
    }
}
