// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// Recommended for non-commercial use. For commercial purposes, please consider contacting the author.
// When using this part of the code, please clearly credit [Project Name] and the author.

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace MapExtPDX.MapExt.ReBurstSystem
{
    /// <summary>
    /// BurstJob替换通用工具
    /// </summary>
    public static class GenericJobReplacePatch
    {
        // 修补内容定义
        // The context holds a dictionary of replacements for a single method
        private class MethodPatchContext
        {
            // Maps: Original Job Type -> Replacement Job Type
            public Dictionary<Type, Type> Replacements { get; }

            public MethodPatchContext()
            {
                Replacements = new Dictionary<Type, Type>();
            }
        }

        // Dictionary: Method being patched -> Its consolidated replacement context
        private static Dictionary<MethodBase, MethodPatchContext> activePatchContexts =
            new Dictionary<MethodBase, MethodPatchContext>();

        // --- Track methods for which context was successfully used ---
        private static readonly HashSet<MethodBase> successfullyProcessedMethods =
            new HashSet<MethodBase>();

        // Registers a SINGLE replacement pair for a method.
        // This will be called multiple times for the same method if needed.
        public static void AddReplacementToContext(MethodBase method, Type originalJobType, Type replacementJobType)
        {
            if (method == null || originalJobType == null || replacementJobType == null) return;

            // Get existing context or create a new one for this method
            if (!activePatchContexts.TryGetValue(method, out MethodPatchContext context))
            {
                context = new MethodPatchContext();
                activePatchContexts[method] = context;
#if DEBUG
                Mod.Logger.Info($"[JobReplacer] Created new context for method: {method.DeclaringType?.FullName}.{method.Name}");
#endif
            }

            // Add or update the specific replacement pair within the method's context
            context.Replacements[originalJobType] = replacementJobType;

            Mod.Logger.Info($"[JobReplacer]    Registered replacement: {originalJobType.Name} -> {replacementJobType.Name}");

        }

        // The Transpiler method - Modified to use the new context structure
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il, MethodBase originalMethod)
        {
            if (Mod.IsUnloading)
            {
                // If the mod is unloading, Harmony might call this unexpectedly during cleanup.
                // Do not attempt to find context or log errors. Just return original instructions.
                Mod.Logger.Warn($"[Job Patcher] Transpiler invoked during unload for {originalMethod.DeclaringType?.FullName}.{originalMethod.Name}. Skipping context lookup."); // Optional debug log
                foreach (var instruction in instructions)
                {
                    yield return instruction;
                }
                yield break; // Stop processing immediately
            }

            // Retrieve the consolidated context for the current method
            if (!activePatchContexts.TryGetValue(originalMethod, out MethodPatchContext context) || context.Replacements.Count == 0)
            {
                Mod.Logger.Error($"[JobReplacer]错误！ Transpiler Error: No context or replacements found for method {originalMethod.DeclaringType?.FullName}.{originalMethod.Name}. Aborting transpilation.");
                // Return original instructions if context is missing or empty
                foreach (var instruction in instructions) { yield return instruction; }
                yield break; // Stop iteration
            }

            // We have context, log the replacements we'll be attempting for this method run
#if DEBUG
            Mod.Logger.Info($"[JobReplacer] Transpiling: {originalMethod.DeclaringType?.FullName}.{originalMethod.Name}");

            foreach (var kvp in context.Replacements)
            {

                Mod.Logger.Info($"[JobReplacer]    Will replace: '{kvp.Key.FullName}' with '{kvp.Value.FullName}'");

            }
#endif

            bool modifiedOverall = false;
            var instructionList = instructions.ToList();

            for (int i = 0; i < instructionList.Count; i++)
            {
                var instruction = instructionList[i];
                bool instructionPatched = false;

                // 1. Patch 'initobj' instructions
                if (instruction.opcode == OpCodes.Initobj && instruction.operand is Type operandType)
                {
                    // Check if the operand type is one of the original types we need to replace
                    if (context.Replacements.TryGetValue(operandType, out Type replacementType))
                    {
#if DEBUG
                        Mod.Logger.Info($"[JobReplacer]  - Replacing initobj target type '{operandType.FullName}' with '{replacementType.FullName}' at index {i}");
#endif
                        yield return new CodeInstruction(OpCodes.Initobj, replacementType);
                        modifiedOverall = true;
                        instructionPatched = true;
                    }
                }

                // 2. Patch Generic Method Calls
                else if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                         instruction.operand is MethodInfo calledMethodInfo &&
                         calledMethodInfo.IsGenericMethod &&
                         calledMethodInfo.Name.Contains("Schedule"))
                {
                    var genericArguments = calledMethodInfo.GetGenericArguments();
                    List<Type> newGenericArguments = new List<Type>(genericArguments.Length);
                    bool needsReplacement = false;

                    // Check *each* generic argument against *all* registered replacements for this method
                    foreach (var argType in genericArguments)
                    {
                        if (context.Replacements.TryGetValue(argType, out Type replacementType))
                        {
                            newGenericArguments.Add(replacementType);
                            needsReplacement = true;
#if DEBUG
                            Mod.Logger.Info($"[JobReplacer]    Found generic arg for replacement: '{argType.FullName}' -> '{replacementType.FullName}'");
#endif
                        }
                        else
                        {
                            newGenericArguments.Add(argType);
                        }
                    }

                    if (needsReplacement)
                    {
                        MethodInfo newConstructedMethod = null;
                        try
                        {
                            MethodInfo genericMethodDefinition = calledMethodInfo.GetGenericMethodDefinition();
                            newConstructedMethod = genericMethodDefinition.MakeGenericMethod(newGenericArguments.ToArray());
                        }
                        catch (Exception ex)
                        {
                            Mod.Logger.Error($"[JobReplacer]错误！  - Error reconstructing generic method call at index {i} for '{calledMethodInfo.FullDescription()}': {ex.Message}. Will use original instruction.");
                        }

                        if (newConstructedMethod != null)
                        {
#if DEBUG
                            Mod.Logger.Info($"[JobReplacer]  - Replacing generic call target '{calledMethodInfo.FullDescription()}' with '{newConstructedMethod.FullDescription()}' at index {i}");
#endif
                            yield return new CodeInstruction(instruction.opcode, newConstructedMethod);
                            modifiedOverall = true;
                            instructionPatched = true;
                        }
                        // else: Fall through to yield original instruction
                    }
                    // else: Fall through to yield original instruction
                }

                // 3. TODO: Patch Field Access (stfld / ldfld)
                /*
                else if ((instruction.opcode == OpCodes.Stfld || instruction.opcode == OpCodes.Ldfld) &&
                         instruction.operand is FieldInfo fieldInfo)
                {
                     // Check if the field's *declaring type* is one we need to replace
                     if(context.Replacements.TryGetValue(fieldInfo.DeclaringType, out Type replacementJobType))
                     {
                         // Find the corresponding field in the replacement struct
                         FieldInfo replacementField = AccessTools.Field(replacementJobType, fieldInfo.Name);
                         if (replacementField != null && replacementField.FieldType == fieldInfo.FieldType)
                         {
                             Console.WriteLine($"  - Replacing field access '{fieldInfo.Name}' in {fieldInfo.DeclaringType.Name} with field in {replacementJobType.Name} at index {i}");
                             yield return new CodeInstruction(instruction.opcode, replacementField);
                             modifiedOverall = true;
                             instructionPatched = true;
                         }
                         else { // Log warning if field not found or incompatible }
                     }
                }
                */

                // Yield original if not patched
                if (!instructionPatched)
                {
                    yield return instruction;
                }
            } // End for loop

            // Final log messages (remains the same)
            if (!modifiedOverall) { Mod.Logger.Warn($"[JobReplacer] Warning: No modifications were applied during transpilation for {originalMethod.DeclaringType?.FullName}.{originalMethod.Name}."); }
            else
            {
#if DEBUG
                Mod.Logger.Info($"[JobReplacer] Transpilation complete for {originalMethod.DeclaringType?.FullName}.{originalMethod.Name}.");
#endif
                // --- NEW: Mark method as successfully processed ---
                lock (successfullyProcessedMethods) // Use lock for thread safety if needed, though unlikely here
                {
                    successfullyProcessedMethods.Add(originalMethod);
                }
            }

            // Clean up context for this method AFTER transpilation is fully complete(弃用)
            // activePatchContexts.Remove(originalMethod);
            // Mod.Logger.Info($"[JobReplacer] Cleared context for method: {originalMethod.DeclaringType?.FullName}.{originalMethod.Name}");

        }

        // --- NEW: Method to clean up all contexts before unpatching ---
        public static void CleanUpAllContexts()
        {
#if DEBUG
            Mod.Logger.Info("[Job Patcher] Cleaning up patch contexts before unpatching...");
#endif
            lock (successfullyProcessedMethods) // Use lock if you added it above
            {
                int count = 0;
                // Create a copy of the keys to avoid modification issues while iterating
                List<MethodBase> methodsToClear = successfullyProcessedMethods.ToList();

                foreach (var method in methodsToClear)
                {
                    if (activePatchContexts.Remove(method))
                    {
                        // Console.WriteLine($"  - Cleared context for: {method.DeclaringType?.FullName}.{method.Name}");
                        count++;
                    }
                }
                successfullyProcessedMethods.Clear(); // Clear the tracking set
#if DEBUG
                Mod.Logger.Info($"[Job Patcher] Cleared {count} context(s).");
#endif
            }
        }
    }
}