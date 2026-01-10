// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// Recommended for non-commercial use. For commercial purposes, please consider contacting the author.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace MapExtPDX.MapExt.ReBurstSystem
{
    public class JobPatchHelper
    {
        private class ResolvedTargetContext
        {
            public JobPatchTarget Target;
            public MethodInfo Method;
            public Type OriginalType;
            public Type ReplacementType;
            public bool IsValid;
        }

        // --- 日志封装 ---
        private static readonly string ModName = Mod.ModName;
        private static readonly string patchTypeName = nameof(JobPatchHelper);

        public static void Info(string message) => Mod.Info($"[{ModName}.{patchTypeName}] {message}");
        public static void Warn(string message) => Mod.Warn($"[{ModName}.{patchTypeName}] ⚠️ {message}");
        public static void Error(string message) => Mod.Error($"[{ModName}.{patchTypeName}] ❌ {message}");
        public static void Error(Exception e, string message) => Mod.Error(e, $"[{ModName}.{patchTypeName}] ❌ {message}");

        /// <summary>
        /// 应用所有 Job 补丁
        /// </summary>
        public static void ApplyAllPatches(Harmony harmonyInstance)
        {
            if (harmonyInstance == null)
            {
                Error("Harmony 实例为空，无法应用补丁！");
                return;
            }

            var targets = JobPatchTarget.GetPatchTargets();
#if DEBUG
        Info($"📥 正在预处理 {targets.Count} 个 Job 替换目标...");
#endif

            // 1. 解析与分组
            var methodGroups = targets
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

                    harmonyInstance.Patch(method, transpiler: new HarmonyMethod(typeof(GenericJobReplacePatch), nameof(GenericJobReplacePatch.Transpiler)));
                    successMethods++;
#if DEBUG
                Info($"✅ 已挂载 Patch: {method.DeclaringType?.Name}.{method.Name}");
#endif
                }
                catch (Exception ex)
                {
                    failMethods++;
                    Error(ex, $"挂载失败: {method.Name}");
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
                if (targetType == null) reason += $"[类型未找到: {t.TargetTypeName}] ";
                else if (method == null) reason += $"[方法未找到: {t.TargetMethodName}] ";
                else if (oldJob == null) reason += $"[原Job未找到: {t.OriginalJobFullName}] ";
                else if (newJob == null) reason += $"[新Job未找到: {t.ReplacementJobFullName}] ";

                Warn($"跳过无效目标: {t.TargetTypeName}.{t.TargetMethodName} -> 原因: {reason}");
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
