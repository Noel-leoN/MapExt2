// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Linq;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using HarmonyLib;


namespace MapExtPDX // 方便切换更稳定高性能的BepInEx版本
{
    public class Mod : IMod
    {
        public const string ModName = "MapExt57km"; // 保持与BepInEx版本一致
        public const string ModNameZH = "16倍大型地图57km";

        public static Mod Instance { get; private set; }

        public static ExecutableAsset ModAsset { get; private set; }

        public static readonly string HarmonyId = ModName;

        private static Harmony harmonyInstance;

        // 日志初始化
        // 日志归结到Logs\ModName.log，不要放在Player.log
        public static ILog Logger = LogManager.GetLogger($"{ModName}").SetShowsErrorsInUI(false);
        public static void Log(string text) => Logger.Info(text);
        public static void Warn(string text) => Logger.Warn(text);
        public static void Error(string text) => Logger.Error(text);

        // Mod加载入口;
        public void OnLoad(UpdateSystem updateSystem)
        {
            Instance = this;

            // Log;
            Logger.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                Logger.Info($"{asset.name} v{asset.version} mod asset at {asset.path}");
            ModAsset = asset;
                        
            // Enable Harmony Patches            
            Harmony harmonyInstance = new Harmony(HarmonyId);
            harmonyInstance.PatchAll(typeof(Mod).Assembly);
            System.Reflection.MethodBase[] patchedMethods = harmonyInstance.GetPatchedMethods().ToArray();
            Logger.Info($"[{HarmonyId}] " + patchedMethods.Length);
            foreach (var patchedMethod in patchedMethods)
            {
                Logger.Info($"[{HarmonyId}]  {patchedMethod.Module.Name}:{patchedMethod.DeclaringType.Name}.{patchedMethod.Name}");
            }

            /// BurstJob替换器(HarmonyIL方式)
            try
            {
                // Delegate Patch Application to the JobPatcher class
                JobPatchHelper.ApplyAllPatches(harmonyInstance);

                Logger.Info($"[{HarmonyId}] Mod Initialization complete.");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{HarmonyId}] FATAL Error during Mod Initialization: {ex}");
                // Optional: Attempt to unpatch if initialization failed critically
                // harmonyInstance?.UnpatchAll(HarmonyId);
            }

        }

        public void OnDispose()
        {
            Logger.Info(nameof(OnDispose));
            Instance = null;

            // un-Harmony;
            Logger.Info($"[{HarmonyId}] Unloading Mod...");
            try
            {
                // --- Clean up contexts BEFORE unpatching ---
                GenericJobReplacePatch.CleanUpAllContexts();

                harmonyInstance?.UnpatchAll(HarmonyId); // Unpatch only *this* mod's patches
                Logger.Info($"[{HarmonyId}] Mod Unload complete.");
            }
            catch (Exception ex)
            {
                Logger.Error($"[{HarmonyId}] Error during Mod Unload: {ex}");
            }
            harmonyInstance = null; // Clear instance
        }

    }





}
