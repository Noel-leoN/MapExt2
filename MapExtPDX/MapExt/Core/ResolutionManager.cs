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
        /// 水系統 Async Compute（<c>WaterSystem.IsAsync</c>）——<b>已硬掛起，恆為 false</b>。
        ///
        /// <para>原設計：<c>UpdateSystem.OnBeginFrame</c> 在 <c>IsAsync</c> 為真時給水的 CommandBuffer
        /// 打上 <c>CommandBufferExecutionFlags.AsyncCompute</c> 並以 <c>ExecuteCommandBufferAsync</c>
        /// 提交，讓水模擬與圖形管線在 GPU 上並行，改善 GPU-bound 場景的幀時間。</para>
        ///
        /// <para><b>掛起原因（兩個獨立缺陷，非驅動品質問題）</b>：</para>
        /// <list type="number">
        ///   <item><b>圖形指令混入 compute 佇列</b>：<c>WaterSimulation.DoTextureClear</c> 用的是
        ///   <c>SetRenderTarget</c> + <c>ClearRenderTarget</c>（圖形指令），async compute 佇列會拒收。
        ///   Unity 只印警告不中止，於是<b>該次清除靜默不發生</b>——
        ///   <c>ResetSeaPropgagtion</c> 沒清就跑 <c>EvaluateSeaPropagation</c>，
        ///   等於拿殘留的上一輪傳播資料當輸入。其兩個呼叫點正是地形變更倒數與海平面變更分支，
        ///   也就是地圖作者用筆刷、改海平面時走的路徑，後果是靜默的水位／海岸邊界錯誤。</item>
        ///
        ///   <item><b>跨佇列零同步（無 Mod 側正解）</b>：原版全庫沒有任何
        ///   <c>GraphicsFence</c> / <c>WaitOnAsyncGraphicsFence</c>，
        ///   <c>OnBeginFrame</c> 也不查 <c>SystemInfo.supportsAsyncCompute</c>。
        ///   而 <c>WaterTexture</c> 在圖形佇列側有三個同幀消費者
        ///   （<c>WaterRenderSystem</c> 材質取樣、<c>SnowSystem</c> compute 讀入、
        ///   <c>TerrainSystem</c> 走自己的 CommandBuffer 立即提交）。
        ///   async 佇列寫、圖形佇列讀且中間無 fence，是確定性的 race，與顯示卡無關。</item>
        /// </list>
        ///
        /// <para><b>恢復條件</b>：缺陷 ① 可修（原版 <c>InitShader</c> 已解析出全庫未使用的
        /// <c>ClearTexture</c> kernel 與 <c>_ClearTarget</c>/<c>_ClearColor</c>，
        /// 改走 <c>DispatchCompute</c> 即可）；但缺陷 ② 需同時對上述三個消費點插 fence，
        /// 其中 TerrainSystem 那條是 hot path 且用自有 CommandBuffer，只能上 transpiler。
        /// 兩者<b>都</b>解決前不得恢復。詳見 <c>docs/02_TerrainWater/Water_AsyncCompute_Analysis.md</c>。</para>
        /// </summary>
        public static bool WaterAsyncCompute => false;

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
            // WaterAsyncCompute 已硬掛起為唯讀 false：僅核查傳入值並在殘留 true 時記錄警告
            SanitizeWaterAsyncCompute(asyncCompute);
            WaterPauseFreeze = pauseFreeze;
            SnowSimFreeze = snowFreeze;

            ModLog.Ok(Tag, $"Initialized: Terrain={TerrainResolution}, Water={WaterTextureSize}, " +
                           $"Format={WaterTextureFormat}, SimQuality={WaterSimQuality}, Async=suspended, " +
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

        /// <summary>
        /// Async Compute 硬掛起的核查閘：一律回傳 false，殘留設定檔的 true 只記錄一次警告。
        /// 布林開關沒有「序號漂移」問題，但屬性與設定檔鍵保留，以免舊 .coc 反序列化時噴未知鍵。
        /// 掛起原因見 <see cref="WaterAsyncCompute"/>。
        /// </summary>
        public static bool SanitizeWaterAsyncCompute(bool requested)
        {
            if (requested)
            {
                ModLog.Warn(Tag,
                    "WaterAsyncCompute=true 已掛起，強制關閉。原因：DoTextureClear 的 " +
                    "SetRenderTarget/ClearRenderTarget 在 async compute 佇列會被拒收，" +
                    "海水傳播紋理清除靜默失效（地形筆刷／改海平面時會出現水位錯誤）；" +
                    "且水紋理跨佇列消費點無任何 GraphicsFence。與顯示卡無關，見 " +
                    "docs/20_systems/02_terrain_water/Water_AsyncCompute_Analysis.md");
            }
            return false;
        }

        public static void UpdateWaterSimQuality(WaterSimQualitySetting quality)
        {
            WaterSimQuality = SanitizeWaterSimQuality(quality);
            ModLog.Ok(Tag, $"WaterSimQuality updated in real-time to {WaterSimQuality}");
        }

        /// <summary>
        /// Async Compute 已掛起：保留此方法供 ModSettings setter 呼叫，
        /// 但只做核查與警告，不改變 <see cref="WaterAsyncCompute"/>（恆 false）。
        /// </summary>
        public static void UpdateWaterAsyncCompute(bool asyncCompute)
        {
            SanitizeWaterAsyncCompute(asyncCompute);
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
