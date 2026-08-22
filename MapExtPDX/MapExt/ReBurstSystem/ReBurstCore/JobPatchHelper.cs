// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MapExtPDX.MapExt.Core;

namespace MapExtPDX.MapExt.ReBurstSystem.Core
{
    public static class JobPatchHelper
    {
        private const string Tag = "ReBurst";

        private class ResolvedTargetContext
        {
            public JobPatchTarget Target;
            public MethodInfo Method;
            public Type OriginalType;
            public Type ReplacementType;
            public bool IsValid;

            /// <summary>解析失敗的原因（IsValid 為 true 時為 null）。</summary>
            public string SkipReason;
        }


        /// <summary>
        /// 核心入口：应用一组补丁目标
        /// </summary>
        public static void Apply(Harmony harmonyInstance, IEnumerable<JobPatchTarget> targets)
        {
            if (harmonyInstance == null)
            {
                ModLog.Error(Tag, "Harmony 实例为空，无法应用补丁");
                return;
            }

            if (targets == null || !targets.Any())
            {
                // 不可靜默返回：目標列表為空意味著整批 Job 替換完全不掛載，
                // 而下游沒有任何其他信號會反映這件事——ModeE 的 Eco 替換就曾因此長期無聲失效。
                ModLog.Warn(Tag, "未提供任何 Job 替換目標，本批次不掛載 Transpiler — 若非預期，請檢查目標產生條件");
                return;
            }

            var targetList = targets.ToList();
            ModLog.Debug(Tag, $"正在预处理 {targetList.Count} 个 Job 替换目标...");

            // 1. 解析与分组
            // 解析失敗的目標必須計數並上報：這些目標全靠字串／反射定位，
            // 遊戲改名時編譯不報錯，若在此靜默丟棄，該系統會無聲退回原版行為。
            var resolved = targetList.Select(ResolveTarget).ToList();
            var skipped = resolved.Where(x => !x.IsValid).ToList();
            var methodGroups = resolved
                .Where(x => x.IsValid)
                .GroupBy(x => x.Method);

            int successMethods = 0;
            int failMethods = 0;
            var succeededNames = new List<string>();

            // 結構驗證未通過而被拒絕註冊的 Job。與 skipped 不同：這些目標反射解析成功，
            // 是欄位結構對不上原版。此時 Transpiler 仍會掛載，同方法內其餘 Job 照常替換。
            var rejected = new List<string>();

            // 2. 按方法应用 Patch
            foreach (var group in methodGroups)
            {
                var method = group.Key;
                try
                {
                    foreach (var item in group)
                    {
                        if (!GenericJobReplacePatch.AddReplacementToContext(method, item.OriginalType,
                                item.ReplacementType))
                        {
                            rejected.Add($"{method.DeclaringType?.Name}.{method.Name} → {item.OriginalType.Name}");
                        }
                    }

                    harmonyInstance.Patch(method,
                        transpiler: new HarmonyMethod(typeof(GenericJobReplacePatch),
                            nameof(GenericJobReplacePatch.Transpiler)));
                    successMethods++;
                    succeededNames.Add($"{method.DeclaringType?.Name}.{method.Name}");
                    ModLog.Debug(Tag, $"已挂载 Patch: {method.DeclaringType?.Name}.{method.Name}");
                }
                catch (Exception ex)
                {
                    failMethods++;
                    ModLog.Error(Tag, $"挂载失败: {method.Name}. {ex.Message}");
                }
            }

            // Release 下输出汇总报告
            ModLog.Report(Tag, "Job Transpiler 挂载汇总", rb =>
            {
                rb.Stat("成功", successMethods);
                rb.Stat("失败", failMethods);
                rb.Stat("跳过", skipped.Count);
                rb.Stat("註冊被拒", rejected.Count);
#if DEBUG
                foreach (var name in succeededNames)
                    rb.Item(name);
                foreach (var s in skipped)
                    rb.Item($"[跳过] {s.Target.TargetTypeName}.{s.Target.TargetMethodName} → {s.SkipReason}");
#endif
            });

            // 註冊被拒必須在 Release 獨立報出：AddReplacementToContext 內那條 Error 混在
            // 逐條註冊日誌中間，且該方法的 Transpiler 依然掛載，會形成原版 Job 與替換版並存的部分替換。
            if (rejected.Count > 0)
            {
                ModLog.Error(Tag, $"有 {rejected.Count} 個 Job 因結構驗證未通過而未註冊，" +
                                  $"其所在方法已掛載 Transpiler，可能形成部分替換：{string.Join("、", rejected)}");
            }

            // 跳过项在 Release 也必须可见：ModLog.Debug 带 [Conditional("DEBUG")]，
            // 正式版看不到逐条明细，故在此补一条聚合 Warn 指向 /check-upgrade 的执行期验证。
            if (skipped.Count > 0)
            {
                var preview = string.Join("、", skipped
                    .Take(3)
                    .Select(s => $"{s.Target.TargetTypeName}.{s.Target.TargetMethodName}"));
                if (skipped.Count > 3) preview += $" 等 {skipped.Count} 项";
                ModLog.Warn(Tag, $"有 {skipped.Count} 个 Job 目标解析失败，已退回原版行为：{preview}" +
                                 "（多为游戏版本升级导致的类型／方法改名，参见 /check-upgrade）");
            }
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
            string skipReason = null;

            if (!valid)
            {
                // 构建详细错误信息，方便排查
                if (targetType == null) skipReason = $"[类型未找到 {t.TargetTypeName}]";
                else if (method == null) skipReason = $"[方法未找到 {t.TargetMethodName}]";
                else if (oldJob == null) skipReason = $"[原Job未找到 {t.OriginalJobFullName}]";
                else skipReason = $"[新Job未找到 {t.ReplacementJobFullName}]";
                ModLog.Debug(Tag, $"跳过无效目标: {t.TargetTypeName}.{t.TargetMethodName} -> 原因: {skipReason}");
            }

            return new ResolvedTargetContext
            {
                Target = t,
                Method = method,
                OriginalType = oldJob,
                ReplacementType = newJob,
                IsValid = valid,
                SkipReason = skipReason
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
