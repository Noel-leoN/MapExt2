// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Game.SceneFlow;
using Game.Simulation;
using Game.UI;
using Game.UI.Editor;
using Game.UI.Localization;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    /// <summary>
    /// 地图编辑器导入地形高位图自适应分辨率
    /// 仅在创建地图时一次调用，无需Transpiler提高性能
    /// </summary>
    public static class TerrainToR16Patch
    {
        private static void Info(string message) => Mod.Info($" {nameof(Mod.ModName)}.{nameof(TerrainToR16Patch)}:{message}");
        private static void Warn(string message) => Mod.Warn($" {nameof(Mod.ModName)}.{nameof(TerrainToR16Patch)}:{message}");
        private static void Error(string message) => Mod.Error($" {nameof(Mod.ModName)}.{nameof(TerrainToR16Patch)}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {nameof(Mod.ModName)}.{nameof(TerrainToR16Patch)}:{message}");

        private const int TARGET_WIDTH = 4096; // vanilla = 4096;
        private const int TARGET_HEIGHT = 4096; // vanilla = 4096;

        [HarmonyPatch(typeof(TerrainSystem), "IsValidHeightmapFormat")]
        [HarmonyPrefix]
        public static bool IsValidHeightmapFormat(ref bool __result, TerrainSystem __instance, Texture2D tex)
        {
            // max texture resolution;
            // 地形分辨率高于原版地图大小将造成渲染系统错误(raycast)，尚不知何原因
            __result = tex.width <= 14336;
            // __result = true;
            return false;
        }

        [HarmonyPatch(typeof(TerrainPanelSystem), "DisplayHeightmapError")]
        [HarmonyPrefix]
        public static bool DisplayHeightmapError(TerrainPanelSystem __instance)
        {
            AppBindings appBindings = GameManager.instance.userInterface.appBindings;
            LocalizedString? localizedString = LocalizedString.Id("Editor.INCORRECT_HEIGHTMAP_TITLE");
            Dictionary<string, ILocElement> dictionary = new Dictionary<string, ILocElement>();
            int kDefaultHeightmapWidth = 14336; // vanilla仅支持导入分辨率不高于14336x14336贴图，原因未知
            dictionary.Add("WIDTH", LocalizedString.Value(kDefaultHeightmapWidth.ToString()));
            kDefaultHeightmapWidth = 14336;
            dictionary.Add("HEIGHT", LocalizedString.Value(kDefaultHeightmapWidth.ToString()));
            appBindings.ShowMessageDialog(new MessageDialog(localizedString, new LocalizedString("Editor.INCORRECT_HEIGHTMAP_MESSAGE", null, dictionary), LocalizedString.Id("Common.ERROR_DIALOG_CONTINUE")), null);
            return false;
        }

        [HarmonyPatch(typeof(TerrainSystem), "CreateDefaultHeightmap")]
        [HarmonyPrefix]
        public static void CreateDefaultHeightmap(ref int width, ref int height)
        {
            width = TARGET_WIDTH;
            height = TARGET_HEIGHT;
            // __result = true;
            // return false;
        }

        // --- Helper: Resample Texture2D -> New R16 Texture2D ---
        private static Texture2D ResampleTexture2DToR16(Texture2D source)
        {
            if (source == null) return null;
            Info($"Resampling Texture2D {source.width}x{source.height} to {TARGET_WIDTH}x{TARGET_HEIGHT} R16_UNorm");

            // 1. Create intermediate RT (Force R16 format here)
            RenderTexture tempRT = RenderTexture.GetTemporary(TARGET_WIDTH, TARGET_HEIGHT, 0, RenderTextureFormat.R16); // Explicitly R16
            tempRT.filterMode = FilterMode.Bilinear;
            tempRT.wrapMode = TextureWrapMode.Clamp;

            // 2. Blit source to RT (resamples)
            // 简单采用双线性Blit过滤
            // 可考虑更复杂算法！
            Graphics.Blit(source, tempRT);

            // 3. Create target Texture2D (must be R16_UNorm)
            Texture2D result = new Texture2D(TARGET_WIDTH, TARGET_HEIGHT, GraphicsFormat.R16_UNorm, TextureCreationFlags.None);
            result.filterMode = FilterMode.Bilinear;
            result.wrapMode = TextureWrapMode.Clamp;
            result.name = $"{source.name}_Resampled_4k_R16";

            // 4. Read back from RT to Texture2D
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = tempRT;
            result.ReadPixels(new Rect(0, 0, TARGET_WIDTH, TARGET_HEIGHT), 0, 0);
            result.Apply(false, false); // No mipmaps, keep readable for now
            RenderTexture.active = previousActive;

            // 5. Release RT
            RenderTexture.ReleaseTemporary(tempRT);

            Info($"Resampling complete. New Texture2D: {result.width}x{result.height}, Format: {result.graphicsFormat}");
            return result; // Caller must handle destruction later if needed
        }

        private static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                Info($"Destroying object: {obj.name}");
                UnityEngine.Object.Destroy(obj);
            }
        }

        // --- Harmony Patch ---

        [HarmonyPatch(typeof(TerrainSystem), "ToR16")]
        [HarmonyPostfix]
        public static void ToR16_Postfix(Texture2D textureRGBA64, ref Texture2D __result) // Use ref to modify the return value
        {
            // If the original method returned null, do nothing
            if (__result == null)
            {
                Warn("ToR16_Postfix: Original result is null, skipping.");
                return;
            }

            // Check if resampling is needed
            if (__result.width != TARGET_WIDTH || __result.height != TARGET_HEIGHT)
            {
                Info($"ToR16_Postfix: Result texture ({__result.width}x{__result.height}, Format: {__result.graphicsFormat}) needs resampling to {TARGET_WIDTH}x{TARGET_HEIGHT}.");

                Texture2D originalResult = __result; // Keep a reference to the original result

                try
                {
                    // Perform resampling (this creates a *new* texture)
                    Texture2D resampledTexture = ResampleTexture2DToR16(originalResult);

                    if (resampledTexture != null)
                    {
                        // Update the return value to the new, resampled texture
                        __result = resampledTexture;
                        Info($"ToR16_Postfix: Successfully replaced result with resampled texture ({__result.width}x{__result.height}).");

                        // IMPORTANT: Destroy the texture returned by the original ToR16 method
                        // UNLESS it was the same object as the input 'tex' AND no format conversion actually happened.
                        // However, it's safer to assume ToR16 might create a copy even for format conversion,
                        // and ResampleTexture2DToR16 *always* creates a new texture.
                        // Therefore, destroying originalResult should usually be correct.
                        // Need to avoid destroying the *input* 'tex' if ToR16 just returned it unchanged.
                        if (originalResult != textureRGBA64) // Only destroy if ToR16 actually created a new texture object
                        {
                            Warn($"ToR16_Postfix: Destroying original result texture '{originalResult.name}' (different from input).");
                            DestroyObject(originalResult);
                        }
                        else
                        {
                            Warn($"ToR16_Postfix: Original result was same as input ('{textureRGBA64.name}'), not destroying original result.");
                            // In this case, the caller (ReplaceHeightmap) is responsible for destroying 'tex' if needed.
                        }
                    }
                    else
                    {
                        Error("ToR16_Postfix: Resampling failed. Original result is kept.");
                        // Keep the original __result
                    }
                }
                catch (Exception ex)
                {
                    Error($"ToR16_Postfix: Exception during resampling: {ex}");
                    // Keep the original __result on error
                }
            }
            else
            {
                Info($"ToR16_Postfix: Result texture ({__result.width}x{__result.height}) is already target size. No action needed.");
            }
        }
    }
}
