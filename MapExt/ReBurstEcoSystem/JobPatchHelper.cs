// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// Recommended for non-commercial use. For commercial purposes, please consider contacting the author.
// When using this part of the code, please clearly credit [Project Name] and the author.

using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;

namespace MapExtPDX.MapExt.ReBurstEcoSystem
{
    public class JobPatchHelper
    {
        // --- 新增：用于传递解析结果的简单数据类 ---
        private class ResolvedTargetContext
        {
            public JobPatchTarget Target;
            public MethodInfo Method;
            public Type OriginalType;
            public Type ReplacementType;
            public bool IsValid;
        }

        // 日志归一化
        private static readonly string ModName = Mod.ModName;
        private static readonly string patchTypeName = nameof(JobPatchHelper);
        public static void Info(string message) => Mod.Info($" {ModName}.{patchTypeName}:{message}");
        public static void Warn(string message) => Mod.Warn($" {ModName}.{patchTypeName}:{message}");
        public static void Error(string message) => Mod.Error($" {ModName}.{patchTypeName}:{message}");
        public static void Error(Exception e, string message) => Mod.Error(e, $" {Mod.ModName}.{patchTypeName}:{message}");

        /// <summary>
        /// Applies all defined job patches using the provided Harmony instance.
        /// </summary>
        /// <param name="harmonyInstance">The Harmony instance to use for patching.</param>
        public static void ApplyAllPatches(Harmony harmonyInstance)
        {

            if (harmonyInstance == null) { Mod.Error($"[JobPatcher]错误！ Harmony实例未找到！"); return; }

            var targets = JobPatchTarget.GetPatchTargets();
#if DEBUG
            Info($"Processing {targets.Count} raw job patch target definitions...");
#endif
            // --- Stage 1: Resolve Types and Group by Method ---
            // 1. 解析与分组
            var methodGroups = targets
                .Select(t => ResolveTarget(t))
                .Where(x => x.IsValid)
                .GroupBy(x => x.Method);

            // --- Stage 2: Register Context and Apply Patch Once Per Method ---
            foreach (var group in methodGroups)
            {
                var method = group.Key;
                try
                {
                    foreach (var item in group)
                    {
                        GenericJobReplacePatch.AddReplacementToContext(method, item.OriginalType, item.ReplacementType);
                    }

                    harmonyInstance.Patch(method, transpiler: new HarmonyMethod(typeof(GenericJobReplacePatch), nameof(GenericJobReplacePatch.Transpiler)));
#if DEBUG
                    Info($"[JobPatcher] 已对方法应用Patch: {method.DeclaringType.Name}.{method.Name}"); 
#endif
                }
                catch (Exception ex)
                {
                    Error($"[JobPatcher] Patch失败 {method.Name}: {ex}");
                }
            }

            Info(" 完成！Finished processing and applying patches.");
        }

        // 解析单个目标
        private static ResolvedTargetContext ResolveTarget(JobPatchTarget t)
        {
            Type targetType = ResolveTypeRobust(t.TargetTypeName);
            MethodInfo method = null;
            Type oldJob = null;
            Type newJob = null;

            if (targetType != null)
            {
                // 处理重载
                if (t.MethodParamTypes != null && t.MethodParamTypes.Length > 0)
                {
                    var paramTypes = t.MethodParamTypes.Select(ResolveTypeRobust).ToArray();
                    // 只有当所有参数类型都解析成功时才尝试获取方法
                    if (paramTypes.All(pt => pt != null))
                    {
                        method = AccessTools.Method(targetType, t.TargetMethodName, paramTypes);
                    }
                }
                else
                {
                    method = AccessTools.Method(targetType, t.TargetMethodName);
                }

                if (method == null) method = AccessTools.DeclaredMethod(targetType, t.TargetMethodName);
            }

            if (method != null) oldJob = ResolveTypeRobust(t.OriginalJobFullName);
            if (oldJob != null) newJob = ResolveTypeRobust(t.ReplacementJobFullName);

            bool valid = method != null && oldJob != null && newJob != null;

            if (!valid) 
                Warn($"[JobPatcher] 解析失败: {t.TargetTypeName}.{t.TargetMethodName}");

            // 返回强类型对象
            return new ResolvedTargetContext
            {
                Target = t,
                Method = method,
                OriginalType = oldJob,
                ReplacementType = newJob,
                IsValid = valid
            };
        }

        // --- 增强的类型查找器 (解决嵌套类/私有类) ---
        private static Type ResolveTypeRobust(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            // 1. 尝试直接查找
            var t = AccessTools.TypeByName(typeName);
            if (t != null) return t;

            // 2. 尝试自动修复嵌套类符号 (将最后的 . 换成 +)
            // 针对: Namespace.Class.Inner -> Namespace.Class+Inner
            if (typeName.Contains(".") && !typeName.Contains("+"))
            {
                int lastDot = typeName.LastIndexOf('.');
                string nestedStyle = typeName.Substring(0, lastDot) + "+" + typeName.Substring(lastDot + 1);
                t = AccessTools.TypeByName(nestedStyle);
                if (t != null) return t;
            }

            return null; // 仍未找到
        }

    }
}
