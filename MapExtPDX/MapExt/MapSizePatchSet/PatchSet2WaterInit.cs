// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.


namespace MapExtPDX.MapExt.MapSizePatchSet
{
    using System;
    using Game.Simulation;
    using HarmonyLib; // 使用Traverse而未采用System反射
    using Unity.Entities;

    /// <summary>
    /// InitTextures()在WaterSystem.OnCreate初始化，Harmony框架延迟加载因而直接修补该方法无效，采取方案：
    /// 1.Transpiler修补InitTextures()；
    /// 2.强制清除初始化生成字段；
    /// 3.重新运行一次InitTextures()；
    /// 此class修补WaterSystem.OnCreate初始化后的重置逻辑(针对InitTextures)，专门负责处理 WaterSystem 的安全重置、内存清理和重新初始化
    /// InitTextures 修补已归一至WaterSystemMethodPatches
    /// BepInEx版本无需对InitTextures()修复(Preloader已处理)
    /// </summary>
    public static class WaterSystemReinitializer
    {
        // --- 日志封装 ---
        private static readonly string patchTypename = nameof(WaterSystemReinitializer);
        private static void Info(string message) => Mod.Info($" {(Mod.ModName)}.{patchTypename}:{message}");
        private static void Warn(string message) => Mod.Warn($" {(Mod.ModName)}.{patchTypename}:{message}");
        private static void Error(string message) => Mod.Error($" {(Mod.ModName)}.{patchTypename}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {(Mod.ModName)}.{patchTypename}:{message}");

        // ========================================================================
        // 核心重置逻辑
        // ========================================================================
        public static void Execute()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var waterSystem = world.GetExistingSystemManaged<WaterSystem>();

            if (waterSystem == null)
            {
                Info("Error: WaterSystem managed instance not found! Initialization aborted.");
                return;
            }

            Info("WaterSystem instance found. Starting safe re-initialization sequence...");

            // 获取 Traverse 对象以便访问私有成员
            var traverse = Traverse.Create(waterSystem);

            // 1. 清理 m_Water (RenderTextures)
            DisposeQuadWaterBuffer(traverse);

            // 2. 清理 Helpers (Native Arrays & ComputeBuffers)
#if DEBUG
            Info("Processing 'm_waterSimActiveTilesHelper'...");
#endif
            DisposeHelper(traverse.Field("m_waterSimActiveTilesHelper").GetValue());

#if DEBUG
            Info("Processing 'm_waterBackdropSimActiveTilesHelper'...");
#endif
            DisposeHelper(traverse.Field("m_waterBackdropSimActiveTilesHelper").GetValue());

            // 3. 调用 InitTextures 重建系统
            try
            {
#if DEBUG
                Info("Invoking WaterSystem.InitTextures() to apply new settings...");
#endif
                traverse.Method("InitTextures").GetValue();
                Info("SUCCESS: WaterSystem re-initialized. New MapSize should be effective.");

                // === 验证纹理尺寸与 ResolutionManager 一致 ===
                try
                {
                    var texSizeField = traverse.Field("m_TexSize");
                    if (texSizeField.FieldExists())
                    {
                        var texSize = texSizeField.GetValue<Unity.Mathematics.int2>();
                        Info($"Post-reinit verification: m_TexSize = {texSize}, expected = {Core.ResolutionManager.WaterTextureSize}");
                        if (texSize.x != Core.ResolutionManager.WaterTextureSize)
                        {
                            Warn($"Water texture size mismatch! Got {texSize.x}x{texSize.y}, " +
                                 $"expected {Core.ResolutionManager.WaterTextureSize}x{Core.ResolutionManager.WaterTextureSize}. " +
                                 "InitTextures Transpiler may not have been applied correctly.");
                        }
                    }
                }
                catch (Exception verifyEx)
                {
                    Warn($"Post-reinit verification failed (non-fatal): {verifyEx.Message}");
                }
            }
            catch (Exception e)
            {
                Error($"CRITICAL ERROR: Failed to invoke InitTextures. Exception: {e}");
            }
        }

        private static void DisposeQuadWaterBuffer(Traverse systemTraverse)
        {
            // ---------------------------------------------------------
            // 1. 清理 m_Water (QuadWaterBuffer)
            // ---------------------------------------------------------
            try
            {
                // 尝试获取 m_Water 字段
                var field = systemTraverse.Field("m_Water");
                if (!field.FieldExists())
                {
                    Warn("Warning: Field 'm_Water' not found.");
                    return;
                }

                // QuadWaterBuffer 是 struct，需要调用它的 Dispose
                // 能直接引用 QuadWaterBuffer 类型，可以用 GetValue<QuadWaterBuffer>().Dispose()
                // 这里为了通用性，使用反射调用 Dispose
                var bufferInstance = field.GetValue();
                if (bufferInstance != null)
                {
                    Traverse.Create(bufferInstance).Method("Dispose").GetValue();
#if DEBUG
                    Info("Disposed old 'm_Water' (RenderTextures).");
#endif
                }
            }
            catch (Exception e)
            {
                Error($"Warning: Failed to dispose m_Water. {e.Message}");
            }
        }

        private static void DisposeHelper(object helperInstance)
        {
            if (helperInstance == null)
            {
                Warn("  -> Helper instance is null, skipping.");
                return;
            }

            // 1. 检查 Helper 类本身是否实现了 IDisposable
            if (helperInstance is IDisposable disposable)
            {
#if DEBUG
                Info($"  -> Helper ({helperInstance.GetType().Name}) implements IDisposable. Disposing...");
#endif
                disposable.Dispose();
                return;
            }

            // 2. 如果没有，手动清理内部字段
#if DEBUG
            Info($"  -> Helper ({helperInstance.GetType().Name}) requires manual reflection dispose.");
#endif
            var t = Traverse.Create(helperInstance);

            // 使用修正后的方法
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

                // --- 修正部分开始 ---

                // 方案 A (推荐): 直接作为 IDisposable 调用
                // NativeArray 和 ComputeBuffer 都实现了 IDisposable 接口
                if (val is IDisposable disposable)
                {
                    disposable.Dispose();
#if DEBUG
                    Info($"    -> Disposed {fieldName} ({typeDesc}) via IDisposable.");
#endif
                }
                // 方案 B (备选): 如果方案 A 不起作用，使用反射精确查找无参方法
                else
                {
                    // GetMethod("Dispose", Type.EmptyTypes) 明确表示查找没有参数的方法
                    var method = val.GetType().GetMethod("Dispose", Type.EmptyTypes);
                    if (method != null)
                    {
                        method.Invoke(val, null);
#if DEBUG
                        Info($"    -> Disposed {fieldName} ({typeDesc}) via Reflection.");
#endif
                    }
                    else
                    {
                        Warn($"    -> Warning: Could not find Dispose() on {fieldName}.");
                    }
                }

                // --- 修正部分结束 ---
            }
            catch (Exception e)
            {
                Error($"    -> Failed to dispose {fieldName}: {e.Message}");
            }
        }

    }

}

