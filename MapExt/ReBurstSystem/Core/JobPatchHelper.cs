// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace MapExtPDX.MapExt.ReBurstSystem.Core
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

        // --- Log ---
        private static void Info(string message) => GenericJobReplacePatch.Info(message);
        private static void Warn(string message) => GenericJobReplacePatch.Warn(message);
        private static void Error(string message) => GenericJobReplacePatch.Error(message);


        /// <summary>
        /// 核心入口：应用一组补丁目标
        /// </summary>
        public static void Apply(Harmony harmonyInstance, IEnumerable<JobPatchTarget> targets)
        {
            if (harmonyInstance == null)
            {
                Error("Harmony 实例为空，无法应用补丁！");
                return;
            }

            if (targets == null || !targets.Any())
            {
                // Info("没有需要应用的补丁目标");
                return;
            }

            var targetList = targets.ToList();
#if DEBUG
            Info($"📥 正在预处理 {targetList.Count} 个 Job 替换目标...");
#endif

            // 1. 解析与分组
            var methodGroups = targetList
                .Select(t => ResolveTarget(t))
                .Where(x => x.IsValid)
                .GroupBy(x => x.Method);

            int successMethods = 0;
            int failMethods = 0;

            // 2. 按方法应用 Patch
            foreach (var group in methodGroups)
            {
                var method = group.Key;
                try
                {
                    foreach (var item in group)
                    {
                        GenericJobReplacePatch.AddReplacementToContext(method, item.OriginalType, item.ReplacementType);
                    }

                    harmonyInstance.Patch(method,
                        transpiler: new HarmonyMethod(typeof(GenericJobReplacePatch),
                            nameof(GenericJobReplacePatch.Transpiler)));
                    successMethods++;
#if DEBUG
                    Info($"✅ 已挂载 Patch: {method.DeclaringType?.Name}.{method.Name}");
#endif
                }
                catch (Exception ex)
                {
                    failMethods++;
                    Error($"挂载失败: {method.Name}. {ex.Message}");
                }
            }

            Info($"🎉 初始化完成！成功: {successMethods}, 失败: {failMethods}");
        }

        // 解析逻辑
        private static ResolvedTargetContext ResolveTarget(JobPatchTarget t)
        {
            Type targetType = ResolveTypeRobust(t.TargetTypeName);
            MethodInfo method = null;

            // 尝试解析方法
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
                // 构建详细错误信息，方便排查
                string reason = "";
                if (targetType == null) reason += $"[类型未找到 {t.TargetTypeName}] ";
                else if (method == null) reason += $"[方法未找到 {t.TargetMethodName}] ";
                else if (oldJob == null) reason += $"[原Job未找到 {t.OriginalJobFullName}] ";
                else if (newJob == null) reason += $"[新Job未找到 {t.ReplacementJobFullName}] ";
#if DEBUG
                Warn($"跳过无效目标: {t.TargetTypeName}.{t.TargetMethodName} -> 原因: {reason}");
#endif
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

            // 尝试自动修复嵌套类符号 (Namespace.Class.Inner -> Namespace.Class+Inner)
            if (typeName.Contains(".") && !typeName.Contains("+"))
            {
                int lastDot = typeName.LastIndexOf('.');
                string nestedStyle = typeName.Substring(0, lastDot) + "+" + typeName.Substring(lastDot + 1);
                t = AccessTools.TypeByName(nestedStyle);
                if (t != null) return t;
            }

            return null;
        }
    }
}
