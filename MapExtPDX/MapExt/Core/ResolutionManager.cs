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

        /// <summary>
        /// 是否需要为水系统降采样地形级联纹理。
        /// 当地形分辨率 > WaterTerrainResolution(4096) 时为 true。
        /// </summary>
        public static bool NeedsDownsampleForWater => TerrainResolution > WaterTerrainResolution;

        /// <summary>
        /// 是否修改了水纹理分辨率（与原版不同）。
        /// </summary>
        public static bool IsWaterResolutionModified => WaterTextureSize != VanillaWaterTextureSize;

        #endregion

        #region Methods

        /// <summary>
        /// 从 ModSettings 的枚举值初始化分辨率参数。
        /// 必须在 PatchManager.Initialize() 中、任何 PatchSet 应用之前调用。
        /// </summary>
        public static void Initialize(TerrainResolutionSetting terrain, WaterResolutionSetting water)
        {
            TerrainResolution = terrain switch
            {
                TerrainResolutionSetting.High_8192 => 8192,
                _ => VanillaTerrainResolution
            };

            WaterTextureSize = water switch
            {
                WaterResolutionSetting.Vanilla_2048 => 2048,
                WaterResolutionSetting.Medium_1024 => 1024,
                WaterResolutionSetting.Low_512 => 512,
                WaterResolutionSetting.Ultra_256 => 256,
                _ => VanillaWaterTextureSize
            };

            ModLog.Ok(Tag, $"Initialized: Terrain={TerrainResolution}, Water={WaterTextureSize}, " +
                           $"NeedsDownsample={NeedsDownsampleForWater}");
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
