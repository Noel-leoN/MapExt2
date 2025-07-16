// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// Recommended for non-commercial use. For commercial purposes, please consider contacting the author.
// When using this part of the code, please clearly credit [Project Name] and the author.

using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;

namespace MapExtPDX.MapExt.ReBurstSystem
{
    public class JobPatchHelper
    {

        /// <summary>
        /// Applies all defined job patches using the provided Harmony instance.
        /// </summary>
        /// <param name="harmonyInstance">The Harmony instance to use for patching.</param>
        public static void ApplyAllPatches(Harmony harmonyInstance)
        {

            if (harmonyInstance == null) { Mod.Error($"[JobPatcher]错误！ Harmony实例未找到！"); return; }

            var targets = JobPatchTarget.GetPatchTargets();
#if DEBUG
            Mod.Info($"[JobPatcher] Processing {targets.Count} raw job patch target definitions...");
#endif
            // --- Stage 1: Resolve Types and Group by Method ---
            var resolvedAndGrouped = targets
                .Select(t =>
                { // Attempt to resolve types and method for each target
                    Type targetType = null;
                    MethodInfo targetMethod = null;
                    Type originalJobType = null;
                    Type replacementJobType = null;
                    bool isValid = false;
                    try
                    {
                        targetType = AccessTools.TypeByName(t.TargetTypeName);
                        Mod.Info($"[JobPatcher] 发现目标类型 {targetType} ");

                        if (targetType != null)
                        {
                            // Add robust method finding if needed (handle overloads etc.)
                            targetMethod = AccessTools.Method(targetType, t.TargetMethodName);
                            if (targetMethod == null) { targetMethod = AccessTools.DeclaredMethod(targetType, t.TargetMethodName); }

                            Mod.Info($"[JobPatcher] 发现目标方法 {targetMethod} ");

                        }
                        if (targetMethod != null)
                        {
                            originalJobType = AccessTools.TypeByName(t.OriginalJobFullName);
                            Mod.Info($"[JobPatcher] 发现目标Job {originalJobType} ");
                        }
                        if (originalJobType != null)
                        {
                            // Try global first, then local assembly
                            replacementJobType = AccessTools.TypeByName(t.ReplacementJobFullName) ??
                                                 typeof(JobPatchHelper).Assembly.GetType(t.ReplacementJobFullName);
                            Mod.Info($"[JobPatcher] 获取替换Job {replacementJobType} ");
                        }
                        isValid = targetMethod != null && originalJobType != null && replacementJobType != null;
                        
                        
                       
                        if (!isValid)
                        {
                            Mod.Error($"[JobPatcher]错误！ Failed to resolve components for target: {t.TargetTypeName}.{t.TargetMethodName} -> {t.OriginalJobFullName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Mod.Error($"[JobPatcher]错误！ Exception resolving target {t.TargetTypeName}.{t.TargetMethodName}: {ex.Message}");
                        isValid = false;
                    }
                    // Return an anonymous object containing resolved info (or nulls) and the original target
                    return new { Target = t, Method = targetMethod, OriginalType = originalJobType, ReplacementType = replacementJobType, IsValid = isValid };
                })
                .Where(x => x.IsValid) // Filter out targets where resolution failed
                .GroupBy(x => x.Method); // Group the valid, resolved targets by the MethodInfo object

#if DEBUG
            Mod.Info($"[JobPatcher] Found {resolvedAndGrouped.Count()} unique target method(s) to patch.");
#endif

            // --- Stage 2: Register Context and Apply Patch Once Per Method ---
            foreach (var methodGroup in resolvedAndGrouped)
            {
                MethodInfo targetMethod = methodGroup.Key; // The unique method being patched

                Mod.Info($"[JobPatcher] --> Processing method: {targetMethod.DeclaringType.FullName}.{targetMethod.Name}");

                // Register *all* replacements intended for this specific method
                foreach (var item in methodGroup)
                {
                    GenericJobReplacePatch.AddReplacementToContext(targetMethod, item.OriginalType, item.ReplacementType);
                }

                // Apply the Harmony patch *ONCE* for this method using the generic transpiler
                try
                {
                    harmonyInstance.Patch(targetMethod,
                        transpiler: new HarmonyMethod(typeof(GenericJobReplacePatch), nameof(GenericJobReplacePatch.Transpiler))
                    );
#if DEBUG
                    Mod.Info($"    Success: Queued single patch application for this method.");
#endif
                }
                catch (Exception ex)
                {
                    Mod.Error($"    错误！Fatal Error applying Harmony patch to method {targetMethod.DeclaringType.FullName}.{targetMethod.Name}: {ex}");
                    // Consider cleanup or alternative actions if patching fails here
                }
            }

            Mod.Info("[JobPatcher] Finished processing and applying patches.");
        }

    }
}
