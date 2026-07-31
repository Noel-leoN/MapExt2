// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.


namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// 地形/水纹理分辨率配置中心。
    /// 在 PatchManager.Initialize() 中从 ModSettings 读取用户设置后初始化，
    /// 供所有 PatchSet 引用当前生效的分辨率值。
    /// </summary>
    public static class ResolutionManager
    {
        private const string Tag = "Resolution";

        #region Constants & Fields

        /// <summary>
        /// 水系统用的地形降采样分辨率 (固定 4096)。
        /// 基于实测发现: 地形/水 = 2:1 比例(即原版 4096:2048)时水渲染最稳定。
        /// 当地形分辨率 > 此值时，需要降采样。
        /// </summary>
        public const int WaterTerrainResolution = 4096;

        /// <summary>原版地形分辨率</summary>
        public const int VanillaTerrainResolution = 4096;

        /// <summary>原版水纹理分辨率</summary>
        public const int VanillaWaterTextureSize = 2048;

        /// <summary>原版水 CellSize</summary>
        public const float VanillaWaterCellSize = 7f;

        #endregion

        #region Properties

        /// <summary>
        /// 地形 heightmap 分辨率 (新建地图时使用)。
        /// 4096(原版) 或 8192(高清)。
        /// </summary>
        public static int TerrainResolution { get; private set; } = VanillaTerrainResolution;

        /// <summary>
        /// 水纹理分辨率 (m_TexSize 目标值)。
        /// 2048(原版) / 1024 / 512 / 256。
        /// </summary>
        public static int WaterTextureSize { get; private set; } = VanillaWaterTextureSize;

        public static WaterSimQualitySetting WaterSimQuality { get; set; } = WaterSimQualitySetting.Vanilla_EveryFrame;

        public static WaterTextureFormatSetting WaterTextureFormat { get; private set; } = WaterTextureFormatSetting.High_RGBA32F;

        /// <summary>
        /// 是否讓水系統走 Async Compute 佇列（實驗性）。
        /// 開啟後 UpdateSystem.OnBeginFrame 會將水的 CommandBuffer 標記為 AsyncCompute 並
        /// 以 ExecuteCommandBufferAsync 提交，讓水模擬與圖形管線在 GPU 上並行。
        /// 主要改善 GPU-bound 場景的幀時間（非 CPU）；收益與風險高度依賴顯示卡與驅動，故預設關閉。
        /// </summary>
        public static bool WaterAsyncCompute { get; set; } = false;

        /// <summary>
        /// 遊戲暫停時凍結水模擬。
        /// 原版暫停時仍每渲染幀發出整條水模擬 compute dispatch（timestep=0，物理不推進，純浪費）。
        /// 凍結近零風險（水面流動動畫由 shader time 驅動，不受模擬更新影響），故預設開啟。
        /// </summary>
        public static bool WaterPauseFreeze { get; set; } = true;

        /// <summary>
        /// 雪模擬凍結模式（凍結 = SnowSimSpeed 設 0）。
        /// 雪深模擬每 4 個模擬幀對 1024² 紋理全幅 dispatch 且 C# 側無任何溫度門檻
        /// （加雪／融雪判斷全在 GPU kernel 內），夏季雪深全 0 時仍照付 dispatch 與帶寬。
        ///
        /// 靜態初值必須是 <see cref="SnowSimFreezeSetting.Off"/>（原版不干預）：
        /// <c>PatchManager.Initialize</c> 只在 <c>Mod.Instance?.Settings != null</c> 時才呼叫
        /// <see cref="Initialize"/> 覆蓋此值，settings 不可用時本欄位即為最終生效值——
        /// 若預設為 Auto，會在使用者未同意的情況下凍結雪模擬，與 ModSettings 的預設 Off 相矛盾。
        /// </summary>
        public static SnowSimFreezeSetting SnowSimFreeze { get; set; } = SnowSimFreezeSetting.Off;

        /// <summary>
        /// 是否需要为水系统降采样地形级联纹理。
        /// 当地形分辨率 > WaterTerrainResolution(4096) 时为 true。
        /// </summary>
        public static bool NeedsDownsampleForWater => TerrainResolution > WaterTerrainResolution;

        /// <summary>
        /// 是否修改了水纹理分辨率（与原版不同）。
        /// </summary>
        public static bool IsWaterResolutionModified => WaterTextureSize != VanillaWaterTextureSize;

        /// <summary>
        /// 是否修改了水纹理格式精度（与原版 32-bit 不同）。
        /// </summary>
        public static bool IsWaterTextureFormatModified => WaterTextureFormat != WaterTextureFormatSetting.High_RGBA32F;

        #endregion

        #region Methods

        /// <summary>
        /// 从 ModSettings 的枚举值初始化分辨率参数。
        /// 必须在 PatchManager.Initialize() 中、任何 PatchSet 应用之前调用。
        /// </summary>
        public static void Initialize(TerrainResolutionSetting terrain, WaterResolutionSetting water,
            WaterSimQualitySetting simQuality, WaterTextureFormatSetting textureFormat, bool asyncCompute = false,
            bool pauseFreeze = true, SnowSimFreezeSetting snowFreeze = SnowSimFreezeSetting.Off)
        {
            // 8192 暂时禁用 (水模拟不兼容)，即使旧存档持久化了该值也强制降级
            TerrainResolution = terrain switch
            {
                // TerrainResolutionSetting.High_8192 => 8192, // 待水模拟修复后恢复
                _ => VanillaTerrainResolution
            };

            // 水纹理分辨率: 计算着色器内部存在纹理尺寸硬编码依赖，无法通过 Harmony 补丁修改
            // 降低水分辨率导致水体偏移/放大，需要自定义计算着色器才能实现 (Phase 3)
            WaterTextureSize = water switch
            {
                // WaterResolutionSetting.Medium_1024 => 1024,
                // WaterResolutionSetting.Low_512 => 512,
                // WaterResolutionSetting.Ultra_256 => 256,  // 禁用: ActiveTiles 网格 1×1，裁剪退化
                _ => VanillaWaterTextureSize
            };

            WaterSimQuality = SanitizeWaterSimQuality(simQuality);
            WaterTextureFormat = textureFormat;
            WaterAsyncCompute = asyncCompute;
            WaterPauseFreeze = pauseFreeze;
            SnowSimFreeze = snowFreeze;

            ModLog.Ok(Tag, $"Initialized: Terrain={TerrainResolution}, Water={WaterTextureSize}, " +
                           $"Format={WaterTextureFormat}, SimQuality={WaterSimQuality}, Async={WaterAsyncCompute}, " +
                           $"PauseFreeze={WaterPauseFreeze}, SnowFreeze={SnowSimFreeze}");
        }

        /// <summary>
        /// 方案 E（Adaptive_EventDriven）硬掛起：列舉值保留以免設定檔序號漂移，
        /// 但運行時一律降級為 Vanilla，避免跨幀 speed=0 與 Postfix / PauseFreeze 衝突。
        /// </summary>
        public static WaterSimQualitySetting SanitizeWaterSimQuality(WaterSimQualitySetting quality)
        {
            if (quality == WaterSimQualitySetting.Adaptive_EventDriven)
            {
                ModLog.Warn(Tag,
                    "WaterSimQuality=Adaptive_EventDriven 已掛起，降級為 Vanilla_EveryFrame " +
                    "（與 Postfix speed 契約衝突，見 PatchSet2WaterAdaptive）");
                return WaterSimQualitySetting.Vanilla_EveryFrame;
            }
            return quality;
        }

        public static void UpdateWaterSimQuality(WaterSimQualitySetting quality)
        {
            WaterSimQuality = SanitizeWaterSimQuality(quality);
            ModLog.Ok(Tag, $"WaterSimQuality updated in real-time to {WaterSimQuality}");
        }

        public static void UpdateWaterAsyncCompute(bool asyncCompute)
        {
            WaterAsyncCompute = asyncCompute;
            ModLog.Ok(Tag, $"WaterAsyncCompute updated in real-time to {asyncCompute}");
        }

        public static void UpdateWaterPauseFreeze(bool pauseFreeze)
        {
            WaterPauseFreeze = pauseFreeze;
            ModLog.Ok(Tag, $"WaterPauseFreeze updated in real-time to {pauseFreeze}");
        }

        public static void UpdateSnowSimFreeze(SnowSimFreezeSetting snowFreeze)
        {
            SnowSimFreeze = snowFreeze;
            ModLog.Ok(Tag, $"SnowSimFreeze updated in real-time to {snowFreeze}");
        }

        /// <summary>
        /// 计算水 CellSize: 基于用户配置的 WaterTextureSize。
        /// 保证 kMapSize = kCellSize × m_TexSize 恒等式成立。
        /// 例: 57344 = 28 × 2048 (原版) → 57344 = 112 × 512 (降级)
        /// </summary>
        public static float GetWaterCellSize(int scaledMapSize)
        {
            int actualTexSize = WaterTextureSize;
            float cellSize = (float)scaledMapSize / actualTexSize;
#if DEBUG
            ModLog.Debug(Tag,
                $"GetWaterCellSize: mapSize={scaledMapSize}, texSize={actualTexSize}, cellSize={cellSize}");
#endif
            return cellSize;
        }

        /// <summary>
        /// VRAM 估算 (MB)，用于 ModSettings UI 显示。
        /// 级联纹理: TerrainRes² × 2bytes × 4slices
        /// 水纹理: WaterTex² × 16bytes (float4) × ~6buffers
        /// </summary>
        public static string GetVRAMEstimate()
        {
            // 级联纹理 (Tex2DArray, 4 slices, R16)
            long cascadeBytes = (long)TerrainResolution * TerrainResolution * 2 * 4;
            // 水纹理 (约6个 RenderTexture, float4/ARGBFloat)
            long waterBytes = (long)WaterTextureSize * WaterTextureSize * 16 * 6;
            // ObjectsLayer 纹理
            long objectsLayerBytes = (long)TerrainResolution * TerrainResolution * 4;
            // 降采样副本 (如果需要)
            long adapterBytes = NeedsDownsampleForWater
                ? (long)WaterTerrainResolution * WaterTerrainResolution * 2 * 4 +
                  WaterTerrainResolution * WaterTerrainResolution * 4
                : 0;

            long totalBytes = cascadeBytes + waterBytes + objectsLayerBytes + adapterBytes;
            double totalMB = totalBytes / (1024.0 * 1024.0);

            return $"~{totalMB:F0} MB";
        }

        #endregion
    }
}
