using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace EconomyEX.Helpers
{
    public static class JobPatchHelper
    {
        private class ResolvedTargetContext
        {
            public JobPatchTarget Target;
            public MethodInfo Method;
            public Type OriginalType;
            public Type ReplacementType;
            public bool IsValid;
        }

        private static void Info(string message) => Mod.Info(message);
        private static void Warn(string message) => Mod.Warn(message);
        private static void Error(string message) => Mod.Error(message);

        public static void Apply(Harmony harmonyInstance, IEnumerable<JobPatchTarget> targets)
        {
            if (harmonyInstance == null)
            {
                Error("Harmony instance is null!");
                return;
            }

            if (targets == null || !targets.Any())
            {
                return;
            }

            var targetList = targets.ToList();
            Info($"Preprocessing {targetList.Count} Job replacement targets...");

            var methodGroups = targetList
                .Select(t => ResolveTarget(t))
                .Where(x => x.IsValid)
                .GroupBy(x => x.Method);

            int successMethods = 0;
            int failMethods = 0;

            foreach (var group in methodGroups)
            {
                var method = group.Key;
                try
                {
                    // For each target in the group (usually just one job per method, but could be multiple)
                    foreach (var item in group)
                    {
                         GenericJobReplacePatch.AddReplacementToContext(method, item.OriginalType, item.ReplacementType);
                    }

                    harmonyInstance.Patch(method, transpiler: new HarmonyMethod(typeof(GenericJobReplacePatch), nameof(GenericJobReplacePatch.Transpiler)));
                    successMethods++;
                    Info($"Patched: {method.DeclaringType?.Name}.{method.Name}");
                }
                catch (Exception ex)
                {
                    failMethods++;
                    Error($"Failed to patch: {method.Name}. {ex.Message}");
                }
            }

            Info($"Job Patching Complete. Success: {successMethods}, Failed: {failMethods}");
        }

        private static ResolvedTargetContext ResolveTarget(JobPatchTarget t)
        {
            Type targetType = ResolveTypeRobust(t.TargetTypeName);
            MethodInfo method = null;

            if (targetType != null)
            {
                if (t.MethodParamTypes != null && t.MethodParamTypes.Length > 0)
                {
                    var paramTypes = t.MethodParamTypes.Select(ResolveTypeRobust).ToArray();
                    if (paramTypes.All(pt => pt != null))
                        method = AccessTools.Method(targetType, t.TargetMethodName, paramTypes);
                }
                else
                {
                    method = AccessTools.Method(targetType, t.TargetMethodName);
                }

                if (method == null) method = AccessTools.DeclaredMethod(targetType, t.TargetMethodName);
            }

            Type oldJob = (method != null) ? ResolveTypeRobust(t.OriginalJobFullName) : null;
            Type newJob = (oldJob != null) ? ResolveTypeRobust(t.ReplacementJobFullName) : null;

            bool valid = method != null && oldJob != null && newJob != null;

            if (!valid)
            {
                string reason = "";
                if (targetType == null) reason += $"[Type Not Found: {t.TargetTypeName}] ";
                else if (method == null) reason += $"[Method Not Found: {t.TargetMethodName}] ";
                else if (oldJob == null) reason += $"[Original Job Not Found: {t.OriginalJobFullName}] ";
                else if (newJob == null) reason += $"[Replacement Job Not Found: {t.ReplacementJobFullName}] ";
                Warn($"Skipping Invalid Target: {t.TargetTypeName}.{t.TargetMethodName} -> {reason}");
            }

            return new ResolvedTargetContext
            {
                Target = t,
                Method = method,
                OriginalType = oldJob,
                ReplacementType = newJob,
                IsValid = valid
            };
        }

        private static Type ResolveTypeRobust(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            var t = AccessTools.TypeByName(typeName);
            if (t != null) return t;

            if (typeName.Contains(".") && !typeName.Contains("+"))
            {
                int lastDot = typeName.LastIndexOf('.');
                string nestedStyle = typeName.Substring(0, lastDot) + "+" + typeName.Substring(lastDot + 1);
                t = AccessTools.TypeByName(nestedStyle);
                if (t != null) return t;
            }
            return null;
        }
        
        // Internal Transpiler Class
        private static class GenericJobReplacePatch
        {
             // Mapping: Method -> (OriginalJobType -> ReplacementJobType)
             private static Dictionary<MethodBase, Dictionary<Type, Type>> _replacements = new Dictionary<MethodBase, Dictionary<Type, Type>>();

             public static void AddReplacementToContext(MethodBase method, Type original, Type replacement)
             {
                 if (!_replacements.ContainsKey(method)) _replacements[method] = new Dictionary<Type, Type>();
                 _replacements[method][original] = replacement;
             }

             public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
             {
                 if (!_replacements.TryGetValue(original, out var mapping)) return instructions;

                 var list = instructions.ToList();
                 bool changed = false;

                 foreach (var instruction in list)
                 {
                     if (instruction.operand is Type operandType)
                     {
                         if (mapping.TryGetValue(operandType, out var replacementType))
                         {
                             instruction.operand = replacementType;
                             changed = true;
                         }
                     }
                     // Handle constrained calls / generics if necessary (usually handled by operand replacement in simple cases)
                 }
                 
                 return list.AsEnumerable();
             }
        }
    }
}
