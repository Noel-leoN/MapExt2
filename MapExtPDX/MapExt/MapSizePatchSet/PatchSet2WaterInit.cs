namespace MapExtPDX.MapExt.MapSizePatchSet
{
    using System;
    using Game.Simulation;
    using HarmonyLib;
    using MapExtPDX.MapExt.Core;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine.Experimental.Rendering;

    /// <summary>
    /// InitTextures()在WaterSystem.OnCreate初始化，Harmony框架延迟加载因而直接修补该方法无效，采取方案：
    /// 1.Transpiler修补InitTextures()；
    /// 2.强制清除初始化生成字段；
    /// 3.重新运行一次InitTextures()；
    /// 4.[Layer 3] 如果水分辨率需要降级，在 InitTextures 后再次 Dispose + 以新尺寸重建。
    /// 此class修补WaterSystem.OnCreate初始化后的重置逻辑(针对InitTextures)，专门负责处理 WaterSystem 的安全重置、内存清理和重新初始化
    /// InitTextures 修补已归一至WaterSystemMethodPatches
    /// BepInEx版本无需对InitTextures()修复(Preloader已处理)
    /// </summary>
    public static class WaterSystemReinitializer
    {
        private const string Tag = "WaterInit";

        // ========================================================================
        // 核心重置逻辑
        // ========================================================================
        public static void Execute()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var waterSystem = world.GetExistingSystemManaged<WaterSystem>();

            if (waterSystem == null)
            {
                ModLog.Error(Tag, "WaterSystem managed instance not found! Initialization aborted.");
                return;
            }

            ModLog.Patch(Tag, "WaterSystem instance found. Starting safe re-initialization...");

            // 获取 Traverse 对象以便访问私有成员
            var traverse = Traverse.Create(waterSystem);

            // 1. 清理 m_Water (RenderTextures)
            DisposeQuadWaterBuffer(traverse);

            // 2. 清理 Helpers (Native Arrays & ComputeBuffers)
#if DEBUG
            ModLog.Debug(Tag, "Processing 'm_waterSimActiveTilesHelper'...");
#endif
            DisposeHelper(traverse.Field("m_waterSimActiveTilesHelper").GetValue());

#if DEBUG
            ModLog.Debug(Tag, "Processing 'm_waterBackdropSimActiveTilesHelper'...");
#endif
            DisposeHelper(traverse.Field("m_waterBackdropSimActiveTilesHelper").GetValue());

            // 3. 调用 InitTextures 重建系统 (此时 Transpiler 已生效，kMapSize/kCellSize 被正确替换)
            try
            {
#if DEBUG
                ModLog.Debug(Tag, "Invoking WaterSystem.InitTextures() to apply new settings...");
#endif
                traverse.Method("InitTextures").GetValue();
                ModLog.Ok(Tag, "WaterSystem re-initialized with Transpiler-patched values.");

                // === Layer 3: 水分辨率降级后处理 ===
                if (ResolutionManager.IsWaterResolutionModified)
                {
                    ApplyWaterResolutionDowngrade(traverse, waterSystem);
                }
                else
                {
                    // 原版分辨率，仅验证
                    VerifyTexSize(traverse, ResolutionManager.VanillaWaterTextureSize);
                }
            }
            catch (Exception e)
            {
                ModLog.Error(Tag, $"CRITICAL: Failed to invoke InitTextures. Exception: {e}");
            }

            // Layer 2 (级联纹理降采样) 现在由 FinalizeTerrainData Postfix 自动处理,
            // 不再需要手动初始化。
        }

        // ========================================================================
        // Layer 3: 水分辨率降级
        // ========================================================================

        /// <summary>
        /// 在 InitTextures() 创建标准 2048 纹理后，以用户配置的分辨率重建所有水体组件。
        /// 步骤: Dispose 旧 2048 资源 → 设置 m_TexSize → 重建 m_Water/Helpers/Readers。
        /// </summary>
        private static void ApplyWaterResolutionDowngrade(Traverse traverse, WaterSystem waterSystem)
        {
            int targetTexSize = ResolutionManager.WaterTextureSize;
            ModLog.Patch(Tag, $"Layer 3: Applying water resolution downgrade 2048 → {targetTexSize}");

            try
            {
                // --- 3a. Dispose InitTextures 刚创建的 2048 版 m_Water ---
                DisposeQuadWaterBuffer(traverse);

                // --- 3b. 设置 m_TexSize 为用户配置的分辨率 ---
                traverse.Field("m_TexSize").SetValue(new int2(targetTexSize, targetTexSize));

                // --- 3c. 以新尺寸重建 QuadWaterBuffer ---
                var newWater = default(WaterSystem.QuadWaterBuffer);
                newWater.Init(new int2(targetTexSize, targetTexSize));
                traverse.Field("m_Water").SetValue(newWater);
                ModLog.Ok(Tag, $"Rebuilt m_Water at {targetTexSize}x{targetTexSize}");

                // --- 3d. Dispose + 重建 ActiveWaterTilesHelper (internal class) ---
                RebuildActiveWaterTilesHelpers(traverse, targetTexSize);

                // --- 3e. Dispose + 重建 SurfaceDataReader / HeightDataReader (internal class) ---
                RebuildDataReaders(traverse, waterSystem, targetTexSize);

                // --- 3f. 验证 ---
                VerifyTexSize(traverse, targetTexSize);

                ModLog.Ok(Tag, $"Layer 3 complete: water resolution = {targetTexSize}x{targetTexSize}, " +
                    $"cellSize = {ResolutionManager.GetWaterCellSize(PatchManager.CurrentMapSize)}");
            }
            catch (Exception e)
            {
                ModLog.Error(Tag, $"Layer 3 failed: {e}");
                ModLog.Warn(Tag, "Attempting fallback: re-invoke InitTextures at vanilla 2048...");

                // 回退: 重新用原版参数重建
                try
                {
                    traverse.Method("InitTextures").GetValue();
                    ModLog.Warn(Tag, "Fallback succeeded. Water at 2048 (vanilla).");
                }
                catch (Exception fallbackEx)
                {
                    ModLog.Error(Tag, $"Fallback also failed: {fallbackEx.Message}");
                }
            }
        }

        /// <summary>
        /// 反射重建两个 ActiveWaterTilesHelper 实例 (internal class)。
        /// 构造器签名: ActiveWaterTilesHelper(int2 gridSize, int textureSize, JobHandle activeReaders)
        /// </summary>
        private static void RebuildActiveWaterTilesHelpers(Traverse traverse, int targetTexSize)
        {
            // Dispose 旧的
            DisposeHelper(traverse.Field("m_waterSimActiveTilesHelper").GetValue());
            DisposeHelper(traverse.Field("m_waterBackdropSimActiveTilesHelper").GetValue());

            // 获取 ActiveWaterTilesHelper 类型 (internal)
            var helperType = AccessTools.TypeByName("Game.Simulation.ActiveWaterTilesHelper");
            if (helperType == null)
            {
                ModLog.Error(Tag, "Could not find type 'Game.Simulation.ActiveWaterTilesHelper'!");
                return;
            }

            // 获取 m_ActiveReaders (JobHandle)
            var activeReaders = traverse.Field("m_ActiveReaders").GetValue<JobHandle>();

            // 计算 gridSize: TexSize / GridSize。GridSize = 32 * (1 << GridSizeMultiplier)
            // GridSizeMultiplier 默认为 3，所以 GridSize = 32 * 8 = 256
            int gridSizeValue = 32 * (1 << traverse.Property("GridSizeMultiplier").GetValue<int>());
            int2 gridCount = new int2(targetTexSize / gridSizeValue);

            // 处理 gridCount 为 0 的情况 (当 targetTexSize < gridSizeValue)
            if (gridCount.x <= 0 || gridCount.y <= 0)
            {
                ModLog.Warn(Tag, $"GridCount={gridCount} invalid (texSize={targetTexSize}, gridSize={gridSizeValue}). " +
                    "Falling back to gridCount=(1,1).");
                gridCount = new int2(1, 1);
            }

            // 查找构造器: (int2, int, JobHandle)
            var helperCtor = helperType.GetConstructor(new[] { typeof(int2), typeof(int), typeof(JobHandle) });
            if (helperCtor == null)
            {
                ModLog.Error(Tag, "Could not find ActiveWaterTilesHelper constructor (int2, int, JobHandle)!");
                return;
            }

            // 构造 sim helper (水纹理全尺寸)
            var simHelper = helperCtor.Invoke(new object[] { gridCount, targetTexSize, activeReaders });
            traverse.Field("m_waterSimActiveTilesHelper").SetValue(simHelper);
            ModLog.Ok(Tag, $"Rebuilt m_waterSimActiveTilesHelper: grid={gridCount}, texSize={targetTexSize}");

            // 构造 backdrop helper (半分辨率)
            int backdropTexSize = targetTexSize / 2;
            int2 backdropGridCount = new int2(backdropTexSize / gridSizeValue);
            if (backdropGridCount.x <= 0 || backdropGridCount.y <= 0)
            {
                backdropGridCount = new int2(1, 1);
            }
            var backdropHelper = helperCtor.Invoke(new object[] { backdropGridCount, backdropTexSize, activeReaders });
            traverse.Field("m_waterBackdropSimActiveTilesHelper").SetValue(backdropHelper);
            ModLog.Ok(Tag, $"Rebuilt m_waterBackdropSimActiveTilesHelper: grid={backdropGridCount}, texSize={backdropTexSize}");
        }

        /// <summary>
        /// 反射重建 SurfaceDataReader / HeightDataReader (internal class)。
        /// InitTextures 创建的 reader 引用旧的 2048 纹理，需要用新纹理重建。
        /// 构造器签名: BaseDataReader(RenderTexture source, int mapSize, GraphicsFormat format)
        /// </summary>
        private static void RebuildDataReaders(Traverse traverse, WaterSystem waterSystem, int targetTexSize)
        {
            int mapSize = PatchManager.CurrentMapSize;

            // === m_depthsReader (SurfaceDataReader) ===
            var oldDepths = traverse.Field("m_depthsReader").GetValue();
            if (oldDepths != null) Traverse.Create(oldDepths).Method("Dispose").GetValue();

            var surfaceReaderType = AccessTools.TypeByName("Game.Simulation.SurfaceDataReader");
            if (surfaceReaderType != null)
            {
                // 需要新的 WaterTexture (已重建为 targetTexSize)
                var newDepths = Activator.CreateInstance(surfaceReaderType,
                    waterSystem.WaterTexture, mapSize, GraphicsFormat.R32G32B32A32_SFloat);
                traverse.Field("m_depthsReader").SetValue(newDepths);
                ModLog.Ok(Tag, $"Rebuilt m_depthsReader: tex={targetTexSize}, mapSize={mapSize}");
            }
            else
            {
                ModLog.Error(Tag, "Could not find type 'Game.Simulation.SurfaceDataReader'!");
            }

            // === m_velocitiesReader (SurfaceDataReader) ===
            var oldVelocities = traverse.Field("m_velocitiesReader").GetValue();
            if (oldVelocities != null) Traverse.Create(oldVelocities).Method("Dispose").GetValue();

            if (surfaceReaderType != null)
            {
                // FlowDownScaled(0) 的尺寸 = targetTexSize / 2
                var newVelocities = Activator.CreateInstance(surfaceReaderType,
                    waterSystem.FlowDownScaled(0), mapSize, GraphicsFormat.R32G32B32A32_SFloat);
                traverse.Field("m_velocitiesReader").SetValue(newVelocities);
                ModLog.Ok(Tag, $"Rebuilt m_velocitiesReader: mapSize={mapSize}");
            }

            // === m_maxHeightReader (HeightDataReader) ===
            var oldMaxHeight = traverse.Field("m_maxHeightReader").GetValue();
            if (oldMaxHeight != null) Traverse.Create(oldMaxHeight).Method("Dispose").GetValue();

            var heightReaderType = AccessTools.TypeByName("Game.Simulation.HeightDataReader");
            if (heightReaderType != null)
            {
                // MaxHeightDownscaled 的尺寸 = targetTexSize / 2
                var newMaxHeight = Activator.CreateInstance(heightReaderType,
                    waterSystem.MaxHeightDownscaled, mapSize, GraphicsFormat.R16_SFloat);
                traverse.Field("m_maxHeightReader").SetValue(newMaxHeight);
                ModLog.Ok(Tag, $"Rebuilt m_maxHeightReader: mapSize={mapSize}");
            }
            else
            {
                ModLog.Error(Tag, "Could not find type 'Game.Simulation.HeightDataReader'!");
            }

            // === m_depthsBackdropReader (SurfaceDataReader, 空构造器) ===
            var oldBackdrop = traverse.Field("m_depthsBackdropReader").GetValue();
            if (oldBackdrop != null) Traverse.Create(oldBackdrop).Method("Dispose").GetValue();

            if (surfaceReaderType != null)
            {
                // backdrop reader 使用无参构造器 (与 InitTextures 原版逻辑一致)
                var newBackdrop = Activator.CreateInstance(surfaceReaderType);
                traverse.Field("m_depthsBackdropReader").SetValue(newBackdrop);
#if DEBUG
                ModLog.Debug(Tag, "Rebuilt m_depthsBackdropReader (empty)");
#endif
            }
        }

        /// <summary>
        /// 验证 m_TexSize 是否与预期值一致。
        /// </summary>
        private static void VerifyTexSize(Traverse traverse, int expectedTexSize)
        {
            try
            {
                var texSizeField = traverse.Field("m_TexSize");
                if (texSizeField.FieldExists())
                {
                    var texSize = texSizeField.GetValue<int2>();
                    ModLog.Info(Tag, $"Post-reinit verification: m_TexSize={texSize}, expected={expectedTexSize}");
                    if (texSize.x != expectedTexSize)
                    {
                        ModLog.Warn(Tag, $"Water texture size mismatch! Got {texSize.x}x{texSize.y}, " +
                             $"expected {expectedTexSize}x{expectedTexSize}.");
                    }
                }
            }
            catch (Exception verifyEx)
            {
                ModLog.Warn(Tag, $"Post-reinit verification failed (non-fatal): {verifyEx.Message}");
            }
        }

        // ========================================================================
        // 资源清理工具方法
        // ========================================================================

        private static void DisposeQuadWaterBuffer(Traverse systemTraverse)
        {
            try
            {
                var field = systemTraverse.Field("m_Water");
                if (!field.FieldExists())
                {
                    ModLog.Warn(Tag, "Field 'm_Water' not found.");
                    return;
                }

                var bufferInstance = field.GetValue();
                if (bufferInstance != null)
                {
                    Traverse.Create(bufferInstance).Method("Dispose").GetValue();
#if DEBUG
                    ModLog.Debug(Tag, "Disposed old 'm_Water' (RenderTextures).");
#endif
                }
            }
            catch (Exception e)
            {
                ModLog.Warn(Tag, $"Failed to dispose m_Water. {e.Message}");
            }
        }

        private static void DisposeHelper(object helperInstance)
        {
            if (helperInstance == null)
            {
                ModLog.Warn(Tag, "Helper instance is null, skipping.");
                return;
            }

            if (helperInstance is IDisposable disposable)
            {
#if DEBUG
                ModLog.Debug(Tag, $"Helper ({helperInstance.GetType().Name}) implements IDisposable. Disposing...");
#endif
                disposable.Dispose();
                return;
            }

            // 手动清理内部字段
#if DEBUG
            ModLog.Debug(Tag, $"Helper ({helperInstance.GetType().Name}) requires manual reflection dispose.");
#endif
            var t = Traverse.Create(helperInstance);
            DisposeNativeField(t, "m_ActiveCPU", "NativeArray");
            DisposeNativeField(t, "m_Active", "ComputeBuffer");
            DisposeNativeField(t, "m_CurrentActiveTilesIndices", "ComputeBuffer");
        }

        private static void DisposeNativeField(Traverse helperTraverse, string fieldName, string typeDesc)
        {
            try
            {
                var field = helperTraverse.Field(fieldName);
                if (!field.FieldExists()) return;

                var val = field.GetValue();
                if (val == null) return;

                if (val is IDisposable disposable)
                {
                    disposable.Dispose();
#if DEBUG
                    ModLog.Debug(Tag, $"Disposed {fieldName} ({typeDesc}) via IDisposable.");
#endif
                }
                else
                {
                    var method = val.GetType().GetMethod("Dispose", Type.EmptyTypes);
                    if (method != null)
                    {
                        method.Invoke(val, null);
#if DEBUG
                        ModLog.Debug(Tag, $"Disposed {fieldName} ({typeDesc}) via Reflection.");
#endif
                    }
                    else
                    {
                        ModLog.Warn(Tag, $"Could not find Dispose() on {fieldName}.");
                    }
                }
            }
            catch (Exception e)
            {
                ModLog.Error(Tag, $"Failed to dispose {fieldName}: {e.Message}");
            }
        }

    }

}
