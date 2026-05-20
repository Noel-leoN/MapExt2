// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

// VanillaSaveConversionSystem.cs
// 原版存档转换后处理系统：在目标模式下加载原版存档后，执行一次性数据修复。

using Colossal.IO.AssetDatabase;
using Colossal.Mathematics;
using Game;
using Game.Areas;
using Game.Common;
using Game.Objects;
using Game.SceneFlow;
using Game.Simulation;
using Game.Tools;
using Game.UI;
using Game.UI.Localization;
using Game.UI.Menu;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using MapExtPDX.SaveLoadSystem;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>
    /// 原版存档转换后处理系统。
    /// 在 OnGameLoaded 时检测 PendingConversion 标志，执行：
    /// ① Heightmap 合成
    /// ② NaturalResource 中心嵌入
    /// ③ GroundWater 中心嵌入
    /// ④ 自动保存新档
    /// </summary>
    public partial class VanillaSaveConversionSystem : GameSystemBase
    {
        private const string Tag = "VanillaConvert";

        #region Constants and Fields

        // === 原版地形常量 ===
        private const int kVanillaHeightmapWidth = 4096;
        private const int kVanillaMapSize = 14336;
        private const int kVanillaWorldSize = 57344; // kDefaultMapSize × 4



        private bool m_ConversionExecuted = false;
        private bool m_OriginalPausedAfterLoading = false;

        #endregion

        protected override void OnCreate()
        {
            base.OnCreate();
            Enabled = true;
        }

        protected override void OnUpdate()
        {
            // 本系统仅在 OnGameLoaded 回调中执行，OnUpdate 不做任何事
        }

        protected override void OnGameLoaded(Colossal.Serialization.Entities.Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);

            if (!VanillaConversionState.PendingConversion)
                return;

            if (m_ConversionExecuted)
            {
                ModLog.Warn(Tag, "转换已执行过，跳过");
                VanillaConversionState.Reset();
                return;
            }

            ModLog.Ok(Tag, "=== 开始原版存档转换 ===");
            ModLog.Info(Tag, $"目标 CV={VanillaConversionState.TargetCoreValue}, " +
                $"原始存档名='{VanillaConversionState.OriginalSaveName}'");

            try
            {
                // ⓪ 利用游戏原生设置强制暂停
                // SimulationSystem.selectedSpeed setter 在 m_IsLoading 期间会被忽略，
                // 加载完成时 SimulationSystem.OnUpdate 读取 pausedAfterLoading 决定速度。
                var gameplay = Game.Settings.SharedSettings.instance?.gameplay;
                m_OriginalPausedAfterLoading = gameplay?.pausedAfterLoading ?? false;
                if (gameplay != null)
                {
                    gameplay.pausedAfterLoading = true;
                    ModLog.Info(Tag, $"已设置 pausedAfterLoading=true（原值: {m_OriginalPausedAfterLoading}）");
                }

                // ⓪a MapTile 边界重建 (529 tiles 全部解锁)
                ExecuteMapTileRebuild();

                // ⓪b 先清除所有车辆和居民（消除所有活跃导航引用）
                // 必须在 OC 删除之前执行，否则车辆导航旧路径会触发 NRE
                ExecuteVehicleCleanup();

                // ⓪c OutsideConnection 节点清理（标记为 Deleted）
                // 包括飞机 OC（无法在游戏内手动删除）
                // 车辆已全部清除，级联删除安全
                ExecuteOutsideConnectionCleanup();

                // ① Heightmap 合成 (含平面底图回退)
                ExecuteHeightmapSynthesis();

                // ② NaturalResource 中心嵌入
                ExecuteNaturalResourceRegen();

                // ③ GroundWater 中心嵌入
                ExecuteGroundWaterInit();

                // ③b 水源升级 + 海平面重置
                ExecuteWaterSimReset();

                // ③c 预模拟水体填充（河流/湖泊注水加速）
                ExecuteWaterPreSimulation();

                // ④ 自动保存新档
                ExecuteAutoSave();

                m_ConversionExecuted = true;

                ModLog.Ok(Tag, "=== 原版存档转换完成 ===");

                // ⑥ 显示完成提示
                ShowCompletionDialog();
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, $"转换过程中发生错误: {ex}");
                ShowErrorDialog(ex.Message);
            }
            finally
            {
                VanillaConversionState.Reset();

                // 恢复原始 pausedAfterLoading 设置（不永久修改用户首选项）
                var gp = Game.Settings.SharedSettings.instance?.gameplay;
                if (gp != null)
                {
                    gp.pausedAfterLoading = m_OriginalPausedAfterLoading;
                    ModLog.Info(Tag, $"已恢复 pausedAfterLoading={m_OriginalPausedAfterLoading}");
                }
            }
        }

        #region ① Heightmap 合成

        /// <summary>
        /// Heightmap 合成：worldHeightmap 作为底图 + 原版 detail heightmap 降采样覆盖中心区域。
        /// </summary>
        private void ExecuteHeightmapSynthesis()
        {
            ModLog.Info(Tag, "① 开始 Heightmap 合成...");

            var terrainSystem = World.GetExistingSystemManaged<TerrainSystem>();
            if (terrainSystem == null)
            {
                ModLog.Error(Tag, "TerrainSystem 不存在，跳过 Heightmap 合成");
                return;
            }

            int cv = PatchManager.CurrentCoreValue;

            // --- 获取 worldHeightmap 和 m_Heightmap ---
            var worldHM = terrainSystem.worldHeightmap;
            var heightmapField = AccessTools.Field(typeof(TerrainSystem), "m_Heightmap");
            var detailHM = heightmapField?.GetValue(terrainSystem) as RenderTexture;

            // --- worldHeightmap 缺失时生成平面底图 ---
            NativeArray<ushort> worldPixels;
            bool flatFallback = false;

            if (worldHM == null)
            {
                ModLog.Info(Tag, "worldHeightmap 为 null，生成默认平面底图");
                flatFallback = true;

                // 从 WaterSystem 获取实际海平面高度（世界坐标 m）
                var waterSystem = World.GetExistingSystemManaged<WaterSystem>();
                float seaLevelWorld = waterSystem?.SeaLevel ?? 511.7f; // kDefaultSeaLevel

                // 从 TerrainSystem 获取 heightScaleOffset
                float2 hso = terrainSystem.heightScaleOffset; // .x=scale, .y=offset

                // 世界坐标 → R16 像素值
                ushort seaLevelR16 = (ushort)math.clamp(
                    (seaLevelWorld - hso.y) / math.max(hso.x, 1f) * 65535f, 0, 65535);

                ModLog.Info(Tag, $"海平面: {seaLevelWorld}m → R16={seaLevelR16} (scale={hso.x}, offset={hso.y})");

                int flatSize = kVanillaHeightmapWidth; // 4096
                worldPixels = new NativeArray<ushort>(flatSize * flatSize, Allocator.Persistent);
                for (int i = 0; i < worldPixels.Length; i++)
                    worldPixels[i] = seaLevelR16;
            }
            else
            {
                worldPixels = ReadbackTextureToR16(worldHM, "worldHeightmap");
            }

            int hmWidth = kVanillaHeightmapWidth; // 4096

            if (detailHM == null)
            {
                ModLog.Warn(Tag, "m_Heightmap 为 null，跳过 detail 嵌入");
            }

            ModLog.Info(Tag, $"worldHeightmap: {(worldHM != null ? $"{worldHM.width}x{worldHM.height}" : "FLAT")}, " +
                $"m_Heightmap: {(detailHM != null ? $"{detailHM.width}x{detailHM.height}" : "null")}");

            // --- GPU 回读 detail 高度图 ---
            NativeArray<ushort> detailPixels = default;
            if (detailHM != null)
                detailPixels = ReadbackRenderTextureToR16(detailHM, "m_Heightmap");

            if (!worldPixels.IsCreated)
            {
                ModLog.Error(Tag, "世界底图数据无效，跳过 Heightmap 合成");
                if (detailPixels.IsCreated) detailPixels.Dispose();
                return;
            }

            try
            {
                // --- 创建合成纹理 ---
                // 底图 = worldHeightmap（全量复制）
                NativeArray<ushort> synthesized = new NativeArray<ushort>(hmWidth * hmWidth, Allocator.Persistent);
                NativeArray<ushort>.Copy(worldPixels, synthesized);

                if (cv == 4 && detailPixels.IsCreated) // ModeA: 57km
                {
                    // 原版 heightmap 覆盖中心 1/4 区域
                    // UV: [0.375, 0.625] → pixel [1536, 2560] → 1024px
                    int embedSize = hmWidth / 4; // 1024
                    int embedStart = (hmWidth - embedSize) / 2; // 1536

                    ModLog.Info(Tag, $"ModeA 合成: 降采样 {hmWidth}² → {embedSize}², 嵌入 [{embedStart}:{embedStart + embedSize}]");

                    // 降采样 detail (4096²) → 1024² 并覆盖到中心
                    BoxFilterDownsample(detailPixels, hmWidth, synthesized, hmWidth, embedStart, embedSize);

                    // --- 实测边界高度偏差并校准 world 底图 ---
                    int offsetR16 = MeasureBoundaryOffset(synthesized, hmWidth, embedStart, embedSize);
                    if (math.abs(offsetR16) > 10) // 超过 ~0.6m 才校准
                    {
                        float2 hso = terrainSystem.heightScaleOffset;
                        float offsetMeters = offsetR16 / 65535f * hso.x;
                        ModLog.Info(Tag, $"实测边界偏差: {offsetR16} R16 ≈ {offsetMeters:F1}m, 校准 world 底图");
                        ApplyOffsetOutsideEmbed(synthesized, hmWidth, embedStart, embedSize, offsetR16);
                        // 同步更新 worldPixels 供 edge blend 使用
                        for (int i = 0; i < worldPixels.Length; i++)
                            worldPixels[i] = (ushort)math.clamp(worldPixels[i] + offsetR16, 0, 65535);
                    }

                    // 边缘混合：消除 detail ↔ world 拼接边界的残余局部差异
                    int blendWidth = 32; // ~450m (32px × 14m/px)
                    ApplyEdgeBlend(synthesized, hmWidth, embedStart, embedSize, worldPixels, blendWidth);
                    ModLog.Info(Tag, $"ModeA 边缘混合: blendWidth={blendWidth}px (~{blendWidth * 14}m)");
                }
                else if (cv == 2 && detailPixels.IsCreated) // ModeB: 28km
                {
                    // === ModeB 合成策略 ===
                    // worldMap (4096px = 57344m) 中心 50% ([1024:3072] = 28672m)
                    // 上采样到 4096px，作为底图覆盖 28672m。
                    // detail heightmap (4096px = 14336m) 降采样到 2048px，嵌入中心 [1024:3072]。
                    //
                    // 关键：边缘混合必须使用上采样后的 world 数据作为参考，
                    // 而不是原始 worldPixels（57344m 坐标系），
                    // 否则 worldPixels[idx] 和 synthesized[idx] 指向不同的世界位置。

                    int cropStart = hmWidth / 4; // 1024
                    int cropSize = hmWidth / 2; // 2048

                    // 先将 worldHeightmap 中心裁剪区域上采样到全图作为底图
                    if (!flatFallback)
                        BilinearUpsampleRegion(worldPixels, hmWidth, cropStart, cropSize, synthesized, hmWidth);

                    // 保存上采样后的 world 底图数据，用于后续边缘混合参考
                    // （此时 synthesized 和 upsampledWorld 共享 28672m 坐标系）
                    var upsampledWorld = new NativeArray<ushort>(hmWidth * hmWidth, Allocator.Persistent);
                    NativeArray<ushort>.Copy(synthesized, upsampledWorld);

                    // detail heightmap (4096²) 降采样到 2048²，嵌入中心 [1024:3072]
                    int embedSize = hmWidth / 2; // 2048
                    int embedStart = hmWidth / 4; // 1024

                    ModLog.Info(Tag, $"ModeB 合成: 降采样 {hmWidth}² → {embedSize}², " +
                        $"嵌入 [{embedStart}:{embedStart + embedSize}]");

                    BoxFilterDownsample(detailPixels, hmWidth, synthesized, hmWidth, embedStart, embedSize);

                    // --- 实测边界高度偏差并校准 world 底图 ---
                    int offsetR16B = MeasureBoundaryOffset(synthesized, hmWidth, embedStart, embedSize);
                    if (math.abs(offsetR16B) > 10)
                    {
                        float2 hsoB = terrainSystem.heightScaleOffset;
                        float offsetMetersB = offsetR16B / 65535f * hsoB.x;
                        ModLog.Info(Tag, $"实测边界偏差: {offsetR16B} R16 ≈ {offsetMetersB:F1}m, 校准 world 底图");
                        ApplyOffsetOutsideEmbed(synthesized, hmWidth, embedStart, embedSize, offsetR16B);
                        // 同步更新 upsampledWorld 供 edge blend 使用
                        for (int i = 0; i < upsampledWorld.Length; i++)
                            upsampledWorld[i] = (ushort)math.clamp(upsampledWorld[i] + offsetR16B, 0, 65535);
                    }

                    // 边缘混合：使用 upsampledWorld（28672m 坐标系）作为参考
                    int blendWidthB = 48; // ~336m (48px × 7m/px at 28672/4096)
                    ApplyEdgeBlend(synthesized, hmWidth, embedStart, embedSize, upsampledWorld, blendWidthB);
                    ModLog.Info(Tag, $"ModeB 边缘混合: blendWidth={blendWidthB}px (~{blendWidthB * 7}m)");

                    upsampledWorld.Dispose();
                }

                // --- 上传合成纹理到 TerrainSystem ---
                Texture2D synthTex = new Texture2D(hmWidth, hmWidth, GraphicsFormat.R16_UNorm,
                    TextureCreationFlags.DontInitializePixels | TextureCreationFlags.DontUploadUponCreate);
                synthTex.name = "VanillaConvert_SynthesizedHeightmap";
                synthTex.SetPixelData(synthesized, 0);
                synthTex.Apply(false, false);

                // --- 销毁旧 worldHeightmap → cascade 退化为单层 (baseLod=0) ---
                // 转换后可玩区已覆盖原始 worldHeightmap 的全部范围，不再需要独立的世界背景层。
                // 如果不销毁，ReplaceHeightmap 内 FinalizeTerrainData 会检测到 worldHeightmap != null，
                // 设置 baseLod=1 保持双层 cascade，导致在原版 14km 边界处出现层间切换落差。
                var destroyWorldMap = AccessTools.Method(typeof(TerrainSystem), "DestroyWorldMap");
                if (destroyWorldMap != null)
                {
                    destroyWorldMap.Invoke(terrainSystem, null);
                    ModLog.Info(Tag, "已销毁旧 worldHeightmap → cascade 退化为单层模式");
                }
                else
                {
                    // 降级：直接设置属性为 null
                    terrainSystem.worldHeightmap = null;
                    ModLog.Warn(Tag, "DestroyWorldMap 未找到，直接清空 worldHeightmap");
                }

                // 使用 ReplaceHeightmap 替换地形并触发 TerrainWillChange
                // 此时 worldHeightmap == null → baseLod=0 → 无 cascade 边界
                terrainSystem.ReplaceHeightmap(synthTex);
                ModLog.Ok(Tag, "Heightmap 合成完成，已调用 ReplaceHeightmap (baseLod=0, 无 cascade)");

                // 清理
                UnityEngine.Object.Destroy(synthTex);
                synthesized.Dispose();
            }
            finally
            {
                if (worldPixels.IsCreated) worldPixels.Dispose();
                if (detailPixels.IsCreated) detailPixels.Dispose();
            }
        }

        // === GPU 回读工具方法 ===

        /// <summary>从 Texture (Texture2D) 回读 R16 数据</summary>
        private NativeArray<ushort> ReadbackTextureToR16(Texture tex, string name)
        {
            try
            {
                var output = new NativeArray<ushort>(tex.width * tex.height, Allocator.Persistent);
                AsyncGPUReadback.RequestIntoNativeArray(ref output, tex).WaitForCompletion();
                ModLog.Info(Tag, $"GPU 回读 {name}: {tex.width}x{tex.height} 完成");
                return output;
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, $"GPU 回读 {name} 失败: {ex.Message}");
                return default;
            }
        }

        /// <summary>从 RenderTexture 回读 R16 数据</summary>
        private NativeArray<ushort> ReadbackRenderTextureToR16(RenderTexture rt, string name)
        {
            try
            {
                var output = new NativeArray<ushort>(rt.width * rt.height, Allocator.Persistent);
                AsyncGPUReadback.RequestIntoNativeArray(ref output, rt).WaitForCompletion();
                ModLog.Info(Tag, $"GPU 回读 {name}: {rt.width}x{rt.height} 完成");
                return output;
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, $"GPU 回读 {name} 失败: {ex.Message}");
                return default;
            }
        }

        // === 采样算法 ===

        /// <summary>
        /// Box Filter 降采样: 将 source (srcSize²) 降采样到 embedSize²，
        /// 写入 dest (destSize²) 的 [embedStart, embedStart+embedSize] 区域。
        /// </summary>
        private static void BoxFilterDownsample(
            NativeArray<ushort> source, int srcSize,
            NativeArray<ushort> dest, int destSize,
            int embedStart, int embedSize)
        {
            // 采样比例: srcSize / embedSize 个源像素 → 1 个目标像素
            float ratio = (float)srcSize / embedSize;

            for (int dy = 0; dy < embedSize; dy++)
            {
                for (int dx = 0; dx < embedSize; dx++)
                {
                    // 源区域范围
                    int sx0 = (int)(dx * ratio);
                    int sy0 = (int)(dy * ratio);
                    int sx1 = math.min((int)((dx + 1) * ratio), srcSize);
                    int sy1 = math.min((int)((dy + 1) * ratio), srcSize);

                    // Box filter: 计算源区域平均值
                    long sum = 0;
                    int count = 0;
                    for (int sy = sy0; sy < sy1; sy++)
                    {
                        for (int sx = sx0; sx < sx1; sx++)
                        {
                            sum += source[sy * srcSize + sx];
                            count++;
                        }
                    }

                    ushort avgValue = (ushort)(count > 0 ? sum / count : 0);
                    int destIdx = (embedStart + dy) * destSize + (embedStart + dx);
                    dest[destIdx] = avgValue;
                }
            }
        }

        /// <summary>
        /// 双线性上采样: 将 source (srcSize²) 的 [cropStart, cropStart+cropSize] 区域
        /// 上采样到 dest (destSize²) 全图。
        /// </summary>
        private static void BilinearUpsampleRegion(
            NativeArray<ushort> source, int srcSize,
            int cropStart, int cropSize,
            NativeArray<ushort> dest, int destSize)
        {
            float ratio = (float)cropSize / destSize;

            for (int dy = 0; dy < destSize; dy++)
            {
                for (int dx = 0; dx < destSize; dx++)
                {
                    float srcX = cropStart + dx * ratio;
                    float srcY = cropStart + dy * ratio;

                    int x0 = math.clamp((int)srcX, 0, srcSize - 1);
                    int y0 = math.clamp((int)srcY, 0, srcSize - 1);
                    int x1 = math.min(x0 + 1, srcSize - 1);
                    int y1 = math.min(y0 + 1, srcSize - 1);

                    float fx = srcX - x0;
                    float fy = srcY - y0;

                    float v00 = source[y0 * srcSize + x0];
                    float v10 = source[y0 * srcSize + x1];
                    float v01 = source[y1 * srcSize + x0];
                    float v11 = source[y1 * srcSize + x1];

                    float result = math.lerp(
                        math.lerp(v00, v10, fx),
                        math.lerp(v01, v11, fx),
                        fy);

                    dest[dy * destSize + dx] = (ushort)math.clamp(result, 0, 65535);
                }
            }
        }

        /// <summary>
        /// 边缘混合：对 embed 区域边缘 blendWidth 像素范围内的值，
        /// 在降采样 detail 值和原始 world 底图值之间做 smoothstep 渐进过渡。
        /// 消除因两张纹理数据不一致导致的硬边界落差。
        /// </summary>
        private static void ApplyEdgeBlend(
            NativeArray<ushort> synthesized, int size,
            int embedStart, int embedSize,
            NativeArray<ushort> worldPixels, int blendWidth)
        {
            int embedEnd = embedStart + embedSize; // exclusive

            for (int y = embedStart; y < embedEnd; y++)
            {
                for (int x = embedStart; x < embedEnd; x++)
                {
                    // 计算到 embed 矩形四条边的最小距离
                    int distX = math.min(x - embedStart, embedEnd - 1 - x);
                    int distY = math.min(y - embedStart, embedEnd - 1 - y);
                    int distFromEdge = math.min(distX, distY);

                    if (distFromEdge >= blendWidth)
                        continue; // 远离边缘，保持纯 detail 值

                    int idx = y * size + x;
                    float t = (float)distFromEdge / blendWidth;

                    // smoothstep: 3t² - 2t³ (比线性插值更自然)
                    t = t * t * (3f - 2f * t);

                    // t=0 (边缘): 使用 world 值 → 与相邻的 world 像素无缝衔接
                    // t=1 (blendWidth 内侧): 使用 detail 值 → 保持降采样精度
                    float worldVal = worldPixels[idx];
                    float detailVal = synthesized[idx];
                    synthesized[idx] = (ushort)math.clamp(math.lerp(worldVal, detailVal, t), 0, 65535);
                }
            }
        }

        /// <summary>
        /// 实测 embed 边界处 inside (降采样 detail) 与 outside (world 底图) 的平均 R16 差值。
        /// 沿四条边各采样 1px inside + 1px outside，取所有样本的均值差。
        /// </summary>
        private static int MeasureBoundaryOffset(
            NativeArray<ushort> synthesized, int size, int embedStart, int embedSize)
        {
            int embedEnd = embedStart + embedSize;
            long sumInside = 0, sumOutside = 0;
            int count = 0;

            // 采样间隔：每 4px 采一个点（减少计算量）
            int step = math.max(1, embedSize / 256);

            for (int i = embedStart; i < embedEnd; i += step)
            {
                // 左边界 (x=embedStart inside, x=embedStart-1 outside)
                if (embedStart > 0)
                {
                    sumInside += synthesized[i * size + embedStart];
                    sumOutside += synthesized[i * size + (embedStart - 1)];
                    count++;
                }
                // 右边界 (x=embedEnd-1 inside, x=embedEnd outside)
                if (embedEnd < size)
                {
                    sumInside += synthesized[i * size + (embedEnd - 1)];
                    sumOutside += synthesized[i * size + embedEnd];
                    count++;
                }
                // 上边界 (y=embedStart inside, y=embedStart-1 outside)
                if (embedStart > 0)
                {
                    sumInside += synthesized[embedStart * size + i];
                    sumOutside += synthesized[(embedStart - 1) * size + i];
                    count++;
                }
                // 下边界 (y=embedEnd-1 inside, y=embedEnd outside)
                if (embedEnd < size)
                {
                    sumInside += synthesized[(embedEnd - 1) * size + i];
                    sumOutside += synthesized[embedEnd * size + i];
                    count++;
                }
            }

            if (count == 0) return 0;
            return (int)((sumInside - sumOutside) / count);
        }

        /// <summary>
        /// 对 embed 区域外的所有像素施加 R16 偏移量（抬升或降低 world 底图）。
        /// </summary>
        private static void ApplyOffsetOutsideEmbed(
            NativeArray<ushort> data, int size, int embedStart, int embedSize, int offsetR16)
        {
            int embedEnd = embedStart + embedSize;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (x >= embedStart && x < embedEnd && y >= embedStart && y < embedEnd)
                        continue; // 跳过 embed 区域
                    int idx = y * size + x;
                    data[idx] = (ushort)math.clamp(data[idx] + offsetR16, 0, 65535);
                }
            }
        }

        #endregion

        #region ② NaturalResource 中心嵌入

        // === 原版 NaturalResource 纹理尺寸 ===
        private const int kVanillaNRTextureSize = 256;

        /// <summary>
        /// NaturalResource 资源修复：将反序列化拉伸的数据降采样回原版尺寸，
        /// 嵌入扩展地图中心区域，外围保持清空（由用户自行创建资源）。
        ///
        /// 策略：
        /// ① 从反序列化后被拉伸的 m_Map 中降采样回原版尺寸 (256²)，恢复原始资源数据。
        /// ② 清空整个 m_Map。
        /// ③ 将原版数据 1:1 嵌入到扩展地图中心。
        /// </summary>
        private void ExecuteNaturalResourceRegen()
        {
            ModLog.Info(Tag, "② 开始 NaturalResource 中心嵌入...");

            try
            {
                // === 读取源: 原版 NaturalResourceSystem (存档数据在这里) ===
                var vanillaSystem = World.GetExistingSystemManaged<NaturalResourceSystem>();
                if (vanillaSystem == null)
                {
                    ModLog.Warn(Tag, "无法获取原版 NaturalResourceSystem 实例，跳过");
                    return;
                }
                CompleteDependencies(vanillaSystem);

                var vanillaMapField = AccessTools.Field(typeof(CellMapSystem<NaturalResourceCell>), "m_Map");
                if (vanillaMapField == null)
                {
                    ModLog.Warn(Tag, "无法获取原版 NaturalResource m_Map 字段，跳过");
                    return;
                }
                var vanillaMap = (NativeArray<NaturalResourceCell>)vanillaMapField.GetValue(vanillaSystem);
                int vanillaSize = (int)math.sqrt(vanillaMap.Length); // 应为 256

#if DEBUG
                ModLog.Info(Tag, $"原版 NaturalResource m_Map: Length={vanillaMap.Length}, Size={vanillaSize}");
#endif

                // 统计原版数据中的非零 cell（兼作全零守卫）
                int nzFert = 0, nzOre = 0, nzOil = 0;
                for (int i = 0; i < vanillaMap.Length; i++)
                {
                    var dc = vanillaMap[i];
                    if (dc.m_Fertility.m_Base > 0) nzFert++;
                    if (dc.m_Ore.m_Base > 0) nzOre++;
                    if (dc.m_Oil.m_Base > 0) nzOil++;
                }
#if DEBUG
                ModLog.Info(Tag, $"原版数据统计: 非零 Fertility={nzFert}, Ore={nzOre}, Oil={nzOil} (共{vanillaMap.Length}cells)");
#endif

                if (nzFert == 0 && nzOre == 0 && nzOil == 0)
                {
                    ModLog.Warn(Tag, "原版数据全为零，可能存档未正确反序列化，跳过");
                    return;
                }

                // === 写入目标: Mod NaturalResourceSystemMod (大地图 CellMap) ===
                var modSystem = GetNaturalResourceSystemMod();
                if (modSystem == null)
                {
                    ModLog.Warn(Tag, "无法获取 NaturalResourceSystemMod 实例，跳过");
                    return;
                }
                CompleteDependencies(modSystem);

                var modMapField = AccessTools.Field(modSystem.GetType().BaseType, "m_Map");
                if (modMapField == null)
                {
                    ModLog.Warn(Tag, "无法获取 Mod NaturalResource m_Map 字段，跳过");
                    return;
                }
                var modMap = (NativeArray<NaturalResourceCell>)modMapField.GetValue(modSystem);
                int modSize = (int)math.sqrt(modMap.Length); // ModeA=1024, ModeB=512

#if DEBUG
                ModLog.Info(Tag, $"Mod NaturalResource m_Map: Length={modMap.Length}, Size={modSize}");
#endif

                // --- 清空 mod m_Map ---
                for (int i = 0; i < modMap.Length; i++)
                    modMap[i] = default;

                // --- 将原版数据 1:1 嵌入 mod 地图中心 ---
                // 原版: 256 cells × 56m/cell = 14336m
                // ModeA 1024²: embedStart = (1024-256)/2 = 384
                // ModeB 512²:  embedStart = (512-256)/2 = 128
                int embedStart = (modSize - vanillaSize) / 2;
                int embedEnd = embedStart + vanillaSize;

                for (int vy = 0; vy < vanillaSize; vy++)
                {
                    for (int vx = 0; vx < vanillaSize; vx++)
                    {
                        int srcIdx = vy * vanillaSize + vx;
                        int dstIdx = (embedStart + vy) * modSize + (embedStart + vx);
                        modMap[dstIdx] = vanillaMap[srcIdx];
                    }
                }

                ModLog.Ok(Tag, $"NaturalResource 修复完成: 原版 {vanillaSize}² 嵌入 Mod {modSize}² 中心 [{embedStart}:{embedEnd}], 外围清空");
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, $"NaturalResource 处理失败: {ex}");
            }
        }

        #endregion

        #region ③ GroundWater 中心嵌入

        // === 原版 GroundWater 纹理尺寸 ===
        private const int kVanillaGWTextureSize = 256;

        /// <summary>
        /// GroundWater 修复：将反序列化拉伸的数据降采样回原版尺寸，
        /// 嵌入扩展地图中心区域，外围保持清空。策略与 NaturalResource 相同。
        /// </summary>
        private void ExecuteGroundWaterInit()
        {
            ModLog.Info(Tag, "③ 开始 GroundWater 中心嵌入...");

            try
            {
                // === 读取源: 原版 GroundWaterSystem (存档数据在这里) ===
                var vanillaSystem = World.GetExistingSystemManaged<GroundWaterSystem>();
                if (vanillaSystem == null)
                {
                    ModLog.Warn(Tag, "无法获取原版 GroundWaterSystem 实例，跳过");
                    return;
                }
                CompleteDependencies(vanillaSystem);

                var vanillaMapField = AccessTools.Field(typeof(CellMapSystem<GroundWater>), "m_Map");
                if (vanillaMapField == null)
                {
                    ModLog.Warn(Tag, "无法获取原版 GroundWater m_Map 字段，跳过");
                    return;
                }
                var vanillaMap = (NativeArray<GroundWater>)vanillaMapField.GetValue(vanillaSystem);
                int vanillaSize = (int)math.sqrt(vanillaMap.Length); // 应为 256

#if DEBUG
                ModLog.Info(Tag, $"原版 GroundWater m_Map: Length={vanillaMap.Length}, Size={vanillaSize}");
                int nzAmount = 0;
                for (int i = 0; i < vanillaMap.Length; i++)
                    if (vanillaMap[i].m_Amount > 0) nzAmount++;
                ModLog.Info(Tag, $"原版数据统计: 非零 Amount={nzAmount} (共{vanillaMap.Length}cells)");
#endif

                // === 写入目标: Mod GroundWaterSystemMod ===
                var modSystem = GetGroundWaterSystemMod();
                if (modSystem == null)
                {
                    ModLog.Warn(Tag, "无法获取 GroundWaterSystemMod 实例，跳过");
                    return;
                }
                CompleteDependencies(modSystem);

                var modMapField = AccessTools.Field(modSystem.GetType().BaseType, "m_Map");
                if (modMapField == null)
                {
                    ModLog.Warn(Tag, "无法获取 Mod GroundWater m_Map 字段，跳过");
                    return;
                }
                var modMap = (NativeArray<GroundWater>)modMapField.GetValue(modSystem);
                int modSize = (int)math.sqrt(modMap.Length);

#if DEBUG
                ModLog.Info(Tag, $"Mod GroundWater m_Map: Length={modMap.Length}, Size={modSize}");
#endif

                // --- 清空 mod m_Map ---
                for (int i = 0; i < modMap.Length; i++)
                    modMap[i] = default;

                // --- 将原版数据 1:1 嵌入中心 ---
                int embedStart = (modSize - vanillaSize) / 2;
                int embedEnd = embedStart + vanillaSize;

                for (int vy = 0; vy < vanillaSize; vy++)
                {
                    for (int vx = 0; vx < vanillaSize; vx++)
                    {
                        int srcIdx = vy * vanillaSize + vx;
                        int dstIdx = (embedStart + vy) * modSize + (embedStart + vx);
                        modMap[dstIdx] = vanillaMap[srcIdx];
                    }
                }

                ModLog.Ok(Tag, $"GroundWater 修复完成: 原版 {vanillaSize}² 嵌入 Mod {modSize}² 中心 [{embedStart}:{embedEnd}], 外围清空");
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, $"GroundWater 处理失败: {ex}");
            }
        }



        /// <summary>
        /// 完成目标系统的所有挂起 Job 依赖，确保安全读写 m_Map。
        /// </summary>
        private static void CompleteDependencies(GameSystemBase system)
        {
            try
            {
                // CellMapSystem<T> 的依赖字段在基类中
                var baseType = system.GetType().BaseType;
                var writeDeps = AccessTools.Field(baseType, "m_WriteDependencies");
                var readDeps = AccessTools.Field(baseType, "m_ReadDependencies");

                if (writeDeps != null)
                    ((JobHandle)writeDeps.GetValue(system)).Complete();
                if (readDeps != null)
                    ((JobHandle)readDeps.GetValue(system)).Complete();
            }
            catch (Exception ex)
            {
                ModLog.Warn("VanillaConvert", $"CompleteDependencies 失败: {ex.Message}");
            }
        }

        #endregion

        #region ④ 自动保存

        /// <summary>
        /// 自动保存转换后的存档。
        /// 直接调用 GameManager.Save 确保以新文件名保存，绕过 SafeSaveGame 的异步覆盖确认。
        /// </summary>
        private void ExecuteAutoSave()
        {
            ModLog.Info(Tag, "④ 开始自动保存...");

            try
            {
                string modeKm = PatchManager.GetModeNameForCoreValue(VanillaConversionState.TargetCoreValue);
                // 移除空格，简化文件名
                string modeSuffix = modeKm.Replace(" ", "");
                string originalName = VanillaConversionState.OriginalSaveName;
                if (string.IsNullOrEmpty(originalName)) originalName = "VanillaSave";

                string newSaveName = $"{originalName}_MapExt{modeSuffix}";

                ModLog.Info(Tag, $"保存新档: '{newSaveName}'");

                // 获取 MenuUISystem 用于 GetSaveInfo
                var menuUI = World.GetExistingSystemManaged<MenuUISystem>();
                if (menuUI == null)
                {
                    ModLog.Warn(Tag, "MenuUISystem 不存在，无法获取 SaveInfo");
                    return;
                }

                // 获取存档元数据
                var saveInfo = menuUI.GetSaveInfo(false);

                // 获取目标数据库
                var getDbMethod = AccessTools.Method(typeof(MenuHelpers), "GetSanitizedCloudTarget");
                var lastTarget = AccessTools.Field(typeof(Game.Settings.SharedSettings), "instance")
                    ?.GetValue(null);
                var userState = lastTarget?.GetType().GetProperty("userState")?.GetValue(lastTarget);
                var lastCloudTarget = userState?.GetType().GetProperty("lastCloudTarget")?.GetValue(userState);

                ILocalAssetDatabase targetDb = null;
                if (getDbMethod != null && lastCloudTarget != null)
                {
                    var cloudTarget = getDbMethod.Invoke(null, new[] { lastCloudTarget });
                    targetDb = (ILocalAssetDatabase)cloudTarget?.GetType().GetProperty("db")?.GetValue(cloudTarget);
                }

                if (targetDb == null)
                {
                    // 降级：使用 AssetDatabase.user（本地存档库）
                    targetDb = Colossal.IO.AssetDatabase.AssetDatabase.user;
                    ModLog.Info(Tag, "使用 AssetDatabase.user 作为目标数据库");
                }

                // 直接调用 GameManager.Save（跳过覆盖确认对话框）
                // savePreview 不能为 null，创建 1x1 哑纹理
                var dummyPreview = new Texture2D(1, 1);
                GameManager.instance.Save(newSaveName, saveInfo, targetDb, dummyPreview);
                UnityEngine.Object.Destroy(dummyPreview);
                ModLog.Ok(Tag, $"已触发保存: '{newSaveName}'");

                // --- 防误覆盖: 更新 MenuUISystem.m_LastSaveNameBinding ---
                // 游戏的 QuickSave 和 UI 保存面板默认文件名优先取 m_LastSaveNameBinding.value,
                // 如果不更新，其值仍为原版存档名，玩家后续保存时有覆盖原存档的风险。
                try
                {
                    var lastSaveField = AccessTools.Field(typeof(MenuUISystem), "m_LastSaveNameBinding");
                    if (lastSaveField != null)
                    {
                        var binding = lastSaveField.GetValue(menuUI);
                        // ValueBinding<string>.Update(string)
                        var updateMethod = binding?.GetType().GetMethod("Update",
                            new[] { typeof(string) });
                        updateMethod?.Invoke(binding, new object[] { newSaveName });
                        ModLog.Info(Tag, $"已更新 m_LastSaveNameBinding → '{newSaveName}'");
                    }
                }
                catch (Exception ex2)
                {
                    ModLog.Warn(Tag, $"更新 m_LastSaveNameBinding 失败（不影响存档）: {ex2.Message}");
                }
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, $"自动保存失败: {ex}");
            }
        }

        #endregion

        #region Helpers

        /// <summary>获取当前模式的 NaturalResourceSystemMod 实例</summary>
        private GameSystemBase GetNaturalResourceSystemMod()
        {
            int cv = PatchManager.CurrentCoreValue;
            switch (cv)
            {
                case 4: return World.GetExistingSystemManaged<ModeA.NaturalResourceSystemMod>();
                case 2: return World.GetExistingSystemManaged<ModeB.NaturalResourceSystemMod>();
                default:
                    ModLog.Warn(Tag, $"不支持的 CV={cv} 的 NaturalResourceSystemMod");
                    return null;
            }
        }

        /// <summary>获取当前模式的 GroundWaterSystemMod 实例</summary>
        private GameSystemBase GetGroundWaterSystemMod()
        {
            int cv = PatchManager.CurrentCoreValue;
            switch (cv)
            {
                case 4: return World.GetExistingSystemManaged<ModeA.GroundWaterSystemMod>();
                case 2: return World.GetExistingSystemManaged<ModeB.GroundWaterSystemMod>();
                default:
                    ModLog.Warn(Tag, $"不支持的 CV={cv} 的 GroundWaterSystemMod");
                    return null;
            }
        }

        // === ⓪ MapTile 边界重建 ===

        /// <summary>
        /// 通过反射调用 MapTileSystem.LegacyGenerateMapTiles(false) 重建 MapTile 实体，
        /// 然后移除所有 Native 组件以全部解锁 529 格。
        /// </summary>
        private void ExecuteMapTileRebuild()
        {
            ModLog.Info(Tag, "⓪ 开始 MapTile 边界重建...");

            try
            {
                var mapTileSystem = World.GetExistingSystemManaged<Game.Areas.MapTileSystem>();
                if (mapTileSystem == null)
                {
                    ModLog.Warn(Tag, "MapTileSystem 不存在，跳过 MapTile 重建");
                    return;
                }

                // 通过反射调用 private LegacyGenerateMapTiles(false)
                var method = AccessTools.Method(typeof(Game.Areas.MapTileSystem), "LegacyGenerateMapTiles");
                if (method == null)
                {
                    ModLog.Warn(Tag, "无法找到 LegacyGenerateMapTiles 方法");
                    return;
                }

                method.Invoke(mapTileSystem, new object[] { false });
                ModLog.Info(Tag, "LegacyGenerateMapTiles(false) 已执行，529 tiles 已重建");

                // 移除所有 MapTile 上的 Native 组件 → 全部解锁
                var nativeQuery = EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<Game.Areas.MapTile>(),
                    ComponentType.ReadOnly<Native>(),
                    ComponentType.Exclude<Temp>(),
                    ComponentType.Exclude<Deleted>());

                int nativeCount = nativeQuery.CalculateEntityCount();
                if (nativeCount > 0)
                {
                    EntityManager.RemoveComponent<Native>(nativeQuery);
                    ModLog.Info(Tag, $"已移除 {nativeCount} 个 MapTile 的 Native 组件（全部解锁）");
                }

                ModLog.Ok(Tag, "MapTile 边界重建完成 (529 tiles, 全部解锁)");
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, $"MapTile 重建失败: {ex}");
            }
        }

        // === ⓪b OutsideConnection 节点清理 ===

        /// <summary>
        /// 给所有 OutsideConnection 实体添加 Deleted 组件，
        /// 触发游戏清理系统级联删除关联的 Edge/Lane/SubLane 等网络实体。
        /// 包括电力和水管类 OC（它们同样位于旧 14km 边界，需在新边界重建）。
        /// </summary>
        private void ExecuteOutsideConnectionCleanup()
        {
            ModLog.Info(Tag, "⓪c 开始 OutsideConnection 节点清理（全类型）...");

            try
            {
                var ocQuery = EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<Game.Objects.OutsideConnection>(),
                    ComponentType.Exclude<Deleted>(),
                    ComponentType.Exclude<Temp>());

                int count = ocQuery.CalculateEntityCount();
                if (count > 0)
                {
                    // 完成所有挂起的 Job，防止结构变更后访问已变更 archetype 的实体
                    EntityManager.CompleteAllTrackedJobs();
                    EntityManager.AddComponent<Deleted>(ocQuery);
                    ModLog.Ok(Tag, $"已标记 {count} 个 OutsideConnection 实体为删除（含交通/电力/水管）");
                }
                else
                {
                    ModLog.Info(Tag, "未发现 OutsideConnection 实体");
                }
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, $"OutsideConnection 清理失败: {ex}");
            }
        }

        // === ③b 水模拟重置 ===

        /// <summary>
        /// 安全版水源升级 + 海平面重置（复现 UpgradeToNewWaterSystem 逻辑）。
        /// 关键改进：
        /// ① 保留非海源水源（河流 type 0、湖泊 type 1），仅删除海源（type 2/3）。
        /// ② 对保留水源执行 Legacy→新系统的 m_Height 语义转换。
        /// ③ 清除陈旧 GPU 流速/传播纹理，防止水面流向异常。
        /// ④ 使用 AddComponent&lt;Deleted&gt; 替代 DestroyEntity，避免并行 Job NRE。
        /// </summary>
        private void ExecuteWaterSimReset()
        {
            ModLog.Info(Tag, "③b 开始水模拟安全升级...");

            try
            {
                var waterSystem = World.GetExistingSystemManaged<WaterSystem>();
                if (waterSystem == null)
                {
                    ModLog.Warn(Tag, "WaterSystem 不存在");
                    return;
                }

                float currentSeaLevel = waterSystem.SeaLevel;
                bool isLegacy = waterSystem.UseLegacyWaterSources;
                ModLog.Info(Tag, $"当前海平面: {currentSeaLevel}m, Legacy水源: {isLegacy}");

                // --- 共用：暂停水模拟、完成 Job ---
                waterSystem.WaterSimSpeed = 0;
                EntityManager.CompleteAllTrackedJobs();

                if (!isLegacy)
                {
                    // === 非 Legacy 路径（MapExt vanilla 模式存档转换）===
                    // 地形已完全替换，需要完整的 GPU 状态重置
                    ClearWaterGpuState(waterSystem);
                    waterSystem.ResetToSealevel();

                    int cv2 = PatchManager.CurrentCoreValue;
                    int frames2 = cv2 >= 4 ? 16 : cv2 >= 2 ? 8 : 5;
                    var newMapField2 = AccessTools.Field(typeof(WaterSystem), "m_NewMap");
                    if (newMapField2 != null)
                        newMapField2.SetValue(waterSystem, frames2);

                    // TerrainWillChange() 内部会设置 WaterSimSpeed=0，
                    // 必须在之后重新设置 WaterSimSpeed=1
                    waterSystem.TerrainWillChange();
                    waterSystem.WaterSimSpeed = 1;

                    ModLog.Ok(Tag, $"已是新版水源，执行完整 GPU 重置 + ResetToSealevel + m_NewMap={frames2}");
                    return;
                }

                // === Legacy 路径：安全版 UpgradeToNewWaterSystem ===

                // --- ③ 获取新地形高度数据（Heightmap 合成已完成）---
                var terrainSystem = World.GetExistingSystemManaged<TerrainSystem>();
                TerrainHeightData terrainData = default;
                bool terrainReady = false;

                if (terrainSystem != null)
                {
                    terrainSystem.AddCPUHeightReader(default);
                    terrainData = terrainSystem.GetHeightData(waitForPending: true);
                    terrainReady = terrainData.isCreated;
                }

                if (!terrainReady)
                    ModLog.Warn(Tag, "TerrainHeightData 未就绪，保留水源将使用降级处理（全删）");

                // --- ④ 遍历水源：按类型分类处理 ---
                var sourceQuery = EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<Game.Simulation.WaterSourceData>(),
                    ComponentType.ReadOnly<Game.Objects.Transform>(),
                    ComponentType.Exclude<Deleted>());

                int totalSources = sourceQuery.CalculateEntityCount();
                // 与原版 UpgradeToNewWaterSystem 一致，初始值为 MaxValue
                // 仅在实际发现 Type 2/3 海源时才更新
                float minSeaLevel = float.MaxValue;
                bool hasSeaSource = false;
                int deletedCount = 0, upgradedCount = 0;

                if (totalSources > 0)
                {
                    var getNextIdMethod = AccessTools.Method(typeof(WaterSystem), "GetNextSourceId");
                    var entities = sourceQuery.ToEntityArray(Allocator.TempJob);

                    for (int i = 0; i < entities.Length; i++)
                    {
                        var entity = entities[i];
                        var sourceData = EntityManager.GetComponentData<WaterSourceData>(entity);

                        // --- Type 3: 旧版海源 → 删除（已被新 SeaLevel 系统替代）---
                        if (sourceData.m_ConstantDepth == 3)
                        {
                            minSeaLevel = math.min(minSeaLevel, sourceData.m_Height);
                            hasSeaSource = true;
                            EntityManager.AddComponent<Deleted>(entity);
                            deletedCount++;
                            continue;
                        }

                        // --- Type 2: 海水源 → 记录 m_Height（绝对海平面高度）---
                        // 原版 UpgradeToNewWaterSystem 中，Type 2 判断后**不用 continue**，
                        // 继续走下面的通用坐标升级流程（XZ clamp + WaterUtils.SampleHeight + 相对水深转换）。
                        // 我们这里也保持一致，仅记录 minSeaLevel 后 fall-through。
                        if (sourceData.m_ConstantDepth == 2)
                        {
                            minSeaLevel = math.min(minSeaLevel, sourceData.m_Height);
                            hasSeaSource = true;
                            // 不 continue —— 走后续的通用坐标升级流程
                        }

                        // --- 通用坐标升级（Type 0/1/2 共用）---
                        if (!terrainReady)
                        {
                            // 降级：无地形数据时只能删除
                            EntityManager.AddComponent<Deleted>(entity);
                            deletedCount++;
                            continue;
                        }

                        var transform = EntityManager.GetComponentData<Game.Objects.Transform>(entity);

                        // 将 XZ 钳制到新地形边界内
                        Bounds3 bounds = TerrainUtils.GetBounds(ref terrainData);
                        if (!MathUtils.Intersect(bounds.xz, transform.m_Position.xz))
                        {
                            transform.m_Position.xz = MathUtils.Clamp(transform.m_Position.xz, bounds.xz);
                        }

                        // 采样新地形高度
                        float newTerrainH = TerrainUtils.SampleHeight(ref terrainData, transform.m_Position);

                        if (sourceData.m_ConstantDepth == 1)
                        {
                            // Type 1（恒定深度）: Legacy m_Height 是绝对水面高度 → 转为相对水深
                            float absoluteWaterLevel = sourceData.m_Height;
                            float relativeDepth = absoluteWaterLevel - newTerrainH;
                            // 钳制：至少 1m 水深，避免负值（地形可能因降采样略有变化）
                            sourceData.m_Height = math.max(1f, relativeDepth);
                        }
                        else if (sourceData.m_ConstantDepth == 2)
                        {
                            // Type 2（海水源）: Legacy m_Height 是绝对海平面高度 → 转为相对水深
                            // 原版: componentData.m_Height = num2 - num3 (WaterSampleHeight - TerrainHeight)
                            // 我们无法调用 WaterUtils.SampleHeight（需要 GPU 数据），
                            // 直接用 m_Height（绝对海平面） - 地形高度作为相对水深
                            float seaRelativeDepth = sourceData.m_Height - newTerrainH;
                            sourceData.m_Height = math.max(0f, seaRelativeDepth);
                        }
                        // else: Type 0（流水源）: m_Height 是流量系数，不需要转换

                        // 更新 Transform.y
                        // 原版: Transform.y = WaterUtils.SampleHeight（GPU 水面采样）
                        // Type 2 海水源: 水面 ≈ 海平面高度，使用 minSeaLevel
                        // Type 0/1: 无 GPU 数据，近似用地形高度
                        transform.m_Position.y = (sourceData.m_ConstantDepth == 2)
                            ? minSeaLevel
                            : newTerrainH;

                        // 分配新源 ID
                        if (getNextIdMethod != null)
                        {
                            sourceData.m_Id = (int)getNextIdMethod.Invoke(waterSystem, null);
                        }

                        EntityManager.SetComponentData(entity, transform);
                        EntityManager.SetComponentData(entity, sourceData);
                        upgradedCount++;
                    }

                    entities.Dispose();
                }

                ModLog.Info(Tag, $"水源处理: 删除 {deletedCount} 个旧版海源(Type 3), 保留并升级 {upgradedCount} 个水源(含 Type 2 海水源)");

                // --- ⑤ 设置 Legacy 旗标 ---
                // 使用反射直接设置字段，绕过 setter 中的 UpgradeToNewWaterSystem() 调用
                var legacyField = AccessTools.Field(typeof(WaterSystem), "m_UseLegacyWaterSources");
                if (legacyField != null)
                {
                    legacyField.SetValue(waterSystem, false);
                    ModLog.Info(Tag, "已设置 m_UseLegacyWaterSources = false");
                }

                // --- ⑥ 设置 m_SimulateBackdrop = false ---
                // 存档中的残留值可能为 true，需显式重置以匹配新水源系统初始状态
                var backdropField = AccessTools.Field(typeof(WaterSystem), "m_SimulateBackdrop");
                if (backdropField != null)
                {
                    backdropField.SetValue(waterSystem, false);
                }

                // --- ⑦ 清除陈旧 GPU 流速/传播纹理 ---
                ClearWaterGpuState(waterSystem);

                // --- ⑧ 设置海平面 ---
                // 仅当实际存在海源（Type 2/3）时才设置 minSeaLevel，
                // 否则保持当前值（避免 float.MaxValue 覆盖）
                float finalSeaLevel = hasSeaSource ? minSeaLevel : currentSeaLevel;
                waterSystem.SeaLevel = finalSeaLevel;
                ModLog.Info(Tag, $"已设置海平面: {waterSystem.SeaLevel}m" +
                    (hasSeaSource ? $" (从 Type 2/3 海源检测)" : " (无海源，使用默认值)"));

                // --- ⑨ 重置水渲染（填充水面纹理到海平面高度）---
                waterSystem.ResetToSealevel();

                // --- ⑩ 设置 m_NewMap（触发水模拟重新初始化）---
                // 原版 UpgradeToNewWaterSystem 使用 3，但扩展地图的 CellSize 更大
                // (ModeA: 28 vs 原版 7)，相同帧数内水传播的物理距离成比例缩短。
                // 需要按地图缩放比例增加初始化帧数，确保海水/水源传播能覆盖整个区域。
                // 参考: 原版 OnBackdropActiveChanged 在类似场景中使用 m_NewMap=16。
                int cv = PatchManager.CurrentCoreValue;
                int newMapFrames = cv >= 4 ? 16 : cv >= 2 ? 8 : 5;  // ModeA=16, ModeB=8, 其他=5
                var newMapField = AccessTools.Field(typeof(WaterSystem), "m_NewMap");
                if (newMapField != null)
                {
                    newMapField.SetValue(waterSystem, newMapFrames);
                }

                // --- ⑪ 同步 shader global ---
                Shader.SetGlobalVector("colossal_WaterParams",
                    new UnityEngine.Vector4(waterSystem.SeaLevel, 0f, 0f, 0f));

                // --- ⑫ 通知地形变更 ---
                // TerrainWillChange() 内部会设置 WaterSimSpeed=0，必须在之后重新设置
                waterSystem.TerrainWillChange();

                // --- ⑬ 恢复水模拟速度 ---
                // 必须在 TerrainWillChange() 之后，否则会被覆盖为 0
                waterSystem.WaterSimSpeed = 1;

                ModLog.Ok(Tag, $"水源安全升级完成: 删除 {deletedCount} 海源, 保留 {upgradedCount} 河流/湖泊, " +
                    $"海平面 {waterSystem.SeaLevel}m, m_NewMap={newMapFrames}");
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, $"水模拟升级失败: {ex}");
            }
        }

        // === ③c 水体预模拟填充 ===

        /// <summary>
        /// 在保存前手动 dispatch 多轮 GPU 水体模拟帧，
        /// 让 Type 0/1 水源（河流/湖泊）预填充水体。
        /// 保存的存档已包含填好的水面，重载后无需等待缓慢注水。
        ///
        /// 关键解锁条件（不处理任何一个，Simulate() 都会空转）：
        /// ① m_terrainReady 必须 ≤ 0（反序列化后为 int.MaxValue）
        /// ② WaterSimSpeed 必须 > 0（TerrainWillChange 会设为 0）
        /// ③ SimulationSystem.selectedSpeed 必须 > 0（pausedAfterLoading 会设为 0）
        /// ④ TimeStepOverride = 1 最大化每步传播距离
        /// ⑤ WaterSimSpeed = MaxSpeed 最大化每帧内部迭代次数
        /// </summary>
        private void ExecuteWaterPreSimulation()
        {
            ModLog.Info(Tag, "③c 开始水体预模拟填充...");

            try
            {
                var waterSystem = World.GetExistingSystemManaged<WaterSystem>();
                if (waterSystem == null)
                {
                    ModLog.Warn(Tag, "WaterSystem 不存在，跳过预模拟");
                    return;
                }

                // 获取 WaterSystem 内部的 CommandBuffer（与 PostDeserialize 相同的模式）
                var cmdBufField = AccessTools.Field(typeof(WaterSystem), "m_CommandBuffer");
                var cmdBuf = cmdBufField?.GetValue(waterSystem) as CommandBuffer;
                if (cmdBuf == null)
                {
                    ModLog.Warn(Tag, "无法获取 WaterSystem.m_CommandBuffer，跳过预模拟");
                    return;
                }

                // --- 解锁条件 ① m_terrainReady → 0 ---
                // Simulate() 入口: m_terrainReady--; if (m_terrainReady > 0) return;
                // 反序列化后 m_terrainReady = int.MaxValue，不设为 0 的话一帧都不会跑
                var terrainReadyField = AccessTools.Field(typeof(WaterSystem), "m_terrainReady");
                if (terrainReadyField != null)
                {
                    terrainReadyField.SetValue(waterSystem, 0);
                    ModLog.Info(Tag, "已设置 m_terrainReady=0");
                }

                // --- 解锁条件 ② WaterSimSpeed = MaxSpeed ---
                // Simulate 内部 for (i=0; i<WaterSimSpeed; i++) 循环控制每帧迭代次数
                int maxSpeed = waterSystem.MaxSpeed;
                waterSystem.WaterSimSpeed = maxSpeed;

                // --- 解锁条件 ③ selectedSpeed > 0 ---
                // Simulate 内部循环有 if (selectedSpeed == 0f) break;
                // 因为我们设置了 pausedAfterLoading=true，需要临时解除
                var simSystem = World.GetExistingSystemManaged<SimulationSystem>();
                float origSpeed = simSystem?.selectedSpeed ?? 1f;
                if (simSystem != null)
                    simSystem.selectedSpeed = 1f;

                // --- 解锁条件 ④ TimeStepOverride = 1 ---
                // GetTimeStep() 中: if (TimeStepOverride > 0) return TimeStepOverride;
                // 默认 timestep 很小（~0.03），设为 1 可最大化每步水传播距离
                float origTimeStep = waterSystem.TimeStepOverride;
                waterSystem.TimeStepOverride = 1f;

                // --- 解锁条件 ⑤ m_terrainChangeCounter = 0 ---
                // Simulate 在 terrainChangeCounter > 0 时走特殊恢复分支，不执行正常注水
                var terrainChangeField = AccessTools.Field(typeof(WaterSystem), "m_terrainChangeCounter");
                if (terrainChangeField != null)
                    terrainChangeField.SetValue(waterSystem, 0);

                // 先跑一次 OnUpdate 填充 SourceCache（GPU SourceStep 依赖它知道从哪注水）
                waterSystem.Update();

                // 按地图模式确定预模拟帧数
                // 每帧内部跑 MaxSpeed 次迭代 × TimeStep=1 → 等效传播距离大幅增加
                int cv = PatchManager.CurrentCoreValue;
                int preSimFrames = cv >= 4 ? 300 : cv >= 2 ? 200 : 100;

                ModLog.Info(Tag, $"预模拟 {preSimFrames} 帧 × {maxSpeed} 迭代/帧, TimeStep=1...");

                for (int i = 0; i < preSimFrames; i++)
                {
                    cmdBuf.Clear();
                    waterSystem.OnSimulateGPU(cmdBuf);
                    Graphics.ExecuteCommandBuffer(cmdBuf);

                    // 每 50 帧刷新一次 SourceCache（确保水源持续注水）
                    if (i % 50 == 49)
                        waterSystem.Update();
                }
                cmdBuf.Clear();

                // --- 恢复所有覆盖值 ---
                waterSystem.TimeStepOverride = origTimeStep;
                waterSystem.WaterSimSpeed = 1;
                if (simSystem != null)
                    simSystem.selectedSpeed = origSpeed;

                ModLog.Ok(Tag, $"水体预模拟完成: {preSimFrames} × {maxSpeed} = {preSimFrames * maxSpeed} 有效迭代");
            }
            catch (Exception ex)
            {
                // 预模拟失败不影响转换流程（水体会在重启后自然填充）
                ModLog.Warn(Tag, $"水体预模拟失败 (非致命，水体将在重启后自然填充): {ex.Message}");
            }
        }


        /// <summary>
        /// 清除陈旧的 GPU 水体状态：流速纹理、海水传播纹理、backdrop 流速纹理、
        /// 活跃格子标记，以及水源缓存。
        /// 必须在地形完全替换后、ResetToSealevel 前调用。
        /// </summary>
        private static void ClearWaterGpuState(WaterSystem waterSystem)
        {
            var traverse = Traverse.Create(waterSystem);

            try
            {
                // --- 清除 GPU 渲染目标 ---
                var cmdBufField = AccessTools.Field(typeof(WaterSystem), "m_CommandBuffer");
                var cmdBuf = cmdBufField?.GetValue(waterSystem) as CommandBuffer;

                if (cmdBuf != null)
                {
                    // 清除海水传播纹理
                    var waterBuf = traverse.Field("m_Water").GetValue<WaterSystem.QuadWaterBuffer>();

                    if (waterBuf.seaPropagationTexture != null)
                    {
                        cmdBuf.SetRenderTarget(waterBuf.seaPropagationTexture);
                        cmdBuf.ClearRenderTarget(false, true, UnityEngine.Color.black);
                    }

                    // 清除流速纹理 (FlowDownScaled[0])
                    var flowTex = waterSystem.FlowDownScaled(0);
                    if (flowTex != null)
                    {
                        cmdBuf.SetRenderTarget(flowTex);
                        cmdBuf.ClearRenderTarget(false, true, UnityEngine.Color.black);
                    }

                    // 清除 backdrop 流速纹理
                    if (waterBuf.downdScaledBackdropFlowTextures?[0] != null)
                    {
                        cmdBuf.SetRenderTarget(waterBuf.downdScaledBackdropFlowTextures[0]);
                        cmdBuf.ClearRenderTarget(false, true, UnityEngine.Color.black);
                    }

                    // 重置活跃格子标记
                    var waterSim = traverse.Field("m_waterSim").GetValue();
                    if (waterSim != null)
                    {
                        try
                        {
                            Traverse.Create(waterSim).Method("ResetActive", new[] { typeof(CommandBuffer) })
                                .GetValue(cmdBuf);
                        }
                        catch (Exception ex)
                        {
                            ModLog.Warn("VanillaConvert", $"ResetActive 调用失败 (非致命): {ex.Message}");
                        }
                    }

                    Graphics.ExecuteCommandBuffer(cmdBuf);
                    cmdBuf.Clear();

                    ModLog.Info("VanillaConvert", "已清除陈旧 GPU 流速/传播/活跃格子纹理");
                }
                else
                {
                    ModLog.Warn("VanillaConvert", "无法获取 m_CommandBuffer，跳过 GPU 纹理清除");
                }

                // --- 清除水源缓存 ---
                // 防止 OnUpdate 中的 SourceJob 引用已删除实体的残留数据
                ClearSourceCaches(traverse);

                // --- 重新绑定着色器纹理 ---
                try
                {
                    traverse.Method("BindTextures").GetValue();
                }
                catch (Exception ex)
                {
                    ModLog.Warn("VanillaConvert", $"BindTextures 调用失败 (非致命): {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("VanillaConvert", $"GPU 状态清除部分失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除 WaterSystem 的 SourceCache（NativeList），
        /// 防止残留的水源缓存引用已删除实体。
        /// </summary>
        private static void ClearSourceCaches(Traverse waterTraverse)
        {
            try
            {
                var cache1 = waterTraverse.Field("m_SourceCache1").GetValue();
                var cache2 = waterTraverse.Field("m_SourceCache2").GetValue();
                if (cache1 != null) Traverse.Create(cache1).Method("Clear").GetValue();
                if (cache2 != null) Traverse.Create(cache2).Method("Clear").GetValue();
                ModLog.Info("VanillaConvert", "已清除水源缓存 (m_SourceCache1/2)");
            }
            catch (Exception ex)
            {
                ModLog.Warn("VanillaConvert", $"清除水源缓存失败 (非致命): {ex.Message}");
            }
        }

        // === ③c 车辆/居民清理 ===

        /// <summary>
        /// 标记所有车辆和居民为 Deleted，防止旧路径引用失效的实体导致 NRE。
        /// 使用游戏官方 DebugSystem.RemoveResidentsAndVehicles() 相同的方式：
        /// AddComponent&lt;Deleted&gt; 仅添加标记，不立即销毁，由后续帧安全回收。
        /// Vehicle 组件覆盖所有载具：汽车、卡车、公交、火车、地铁、轮船、飞机。
        /// </summary>
        private void ExecuteVehicleCleanup()
        {
            ModLog.Info(Tag, "③c 开始清除车辆和居民...");

            try
            {
                // 完成所有挂起的 Job（TrainNavigation/CarNavigation 等）
                // 否则已调度的 Job 会访问被 Deleted 实体的 archetype → NRE
                EntityManager.CompleteAllTrackedJobs();

                // 标记所有居民为 Deleted
                var residentQuery = EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<Game.Creatures.Resident>());
                int residentCount = residentQuery.CalculateEntityCount();
                EntityManager.AddComponent<Game.Common.Deleted>(residentQuery);
                ModLog.Info(Tag, $"已标记 {residentCount} 个居民实体为 Deleted");

                // 标记所有车辆为 Deleted（汽车/火车/轮船/飞机/地铁等全部类型）
                var vehicleQuery = EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<Game.Vehicles.Vehicle>());
                int vehicleCount = vehicleQuery.CalculateEntityCount();
                EntityManager.AddComponent<Game.Common.Deleted>(vehicleQuery);
                ModLog.Info(Tag, $"已标记 {vehicleCount} 个车辆实体为 Deleted");

                ModLog.Ok(Tag, $"车辆/居民清理完成: {vehicleCount} 车辆 + {residentCount} 居民");
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, $"车辆清理失败: {ex}");
            }
        }

        /// <summary>显示转换完成对话框，点击确认后退出游戏</summary>
        private void ShowCompletionDialog()
        {
            try
            {
                string targetMode = PatchManager.GetModeNameForCoreValue(VanillaConversionState.TargetCoreValue);
                string modeSuffix = targetMode.Replace(" ", "");
                string saveName = $"{VanillaConversionState.OriginalSaveName}_MapExt{modeSuffix}";

                var locParams = new Dictionary<string, ILocElement>
                {
                    { "TARGET_MODE", LocalizedString.Value(targetMode) },
                    { "SAVE_NAME", LocalizedString.Value(saveName) }
                };

                var dialog = new ConfirmationDialog(
                    LocalizedString.Id("VANILLA_CONVERT.Complete"),
                    new LocalizedString("VANILLA_CONVERT.CompleteMessage", null, locParams),
                    LocalizedString.Id("VANILLA_CONVERT.QuitConfirm"),
                    LocalizedString.Id("VANILLA_CONVERT.QuitCancel"));

                // 使用 MainThreadDispatcher 延迟显示，确保在 RoadBuilder 等 mod
                // 的 OnGameLoadingComplete 对话框之后显示（避免被覆盖）
                Colossal.Core.MainThreadDispatcher.RegisterUpdater(() =>
                {
                    GameManager.instance.userInterface.appBindings.ShowConfirmationDialog(dialog, (int result) =>
                    {
                        if (result == 0)
                        {
                            ModLog.Info(Tag, "用户确认退出游戏");
                            GameManager.QuitGame();
                        }
                        else
                        {
                            ModLog.Info(Tag, "用户选择留在游戏中");
                        }
                    });
                    return true;
                });
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"显示完成对话框失败: {ex.Message}");
            }
        }

        /// <summary>显示错误对话框</summary>
        private void ShowErrorDialog(string errorMessage)
        {
            try
            {
                var dialog = new MessageDialog(
                    LocalizedString.Id("LOAD_VALIDATION.TitleError"),
                    LocalizedString.Value($"Vanilla save conversion failed:\n{errorMessage}"),
                    LocalizedString.Id("LOAD_VALIDATION.ConfirmOK"));

                GameManager.instance.userInterface.appBindings.ShowMessageDialog(dialog, null);
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"显示错误对话框失败: {ex.Message}");
            }
        }

        #endregion
    }
}
