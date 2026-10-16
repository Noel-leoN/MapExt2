// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Colossal.PSI.Environment;
using Game;
using HarmonyLib;
using Unity.Entities;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// 系统注册表导出工具（开发者用）。
    ///
    /// <para><b>用途</b>：重构一（声明式替换注册表）的行为等价验收。
    /// 同一配置下重构前后各导出一次，逐行比对。</para>
    ///
    /// <para><b>内容</b>：
    /// ① 更新次序：读 <c>UpdateSystem.m_Updates</c>，即引擎把 UpdateAt 与 UpdateBefore／UpdateAfter 锚定合并后的实际执行顺序，
    /// 列出 MapExt 系统在各 phase 内的相对次序，并附上它之前最近的原版系统作为锚点。
    /// 不输出绝对序号：它随其他 Mod 的加载先后变化。
    /// ② 启用状态：MapExt 系统的 Enabled，以及处于禁用状态的原版系统。
    /// ③ 补丁清单：owner 为 MapExt 两个 Harmony 实例的全部补丁，按目标方法分行，行内保持 Harmony 的登记顺序。</para>
    ///
    /// <para><b>时机</b>：进入城市后导出。原版系统的重新禁用发生在读档过程中，主菜单下的启用状态不完整。</para>
    ///
    /// <para><b>输出</b>：写入 MapExtPDX.log，并另存到 {UserData}/ModsData/MapExt2/RegistrationDump。</para>
    /// </summary>
    public static class RegistrationDump
    {
        private const string Tag = nameof(RegistrationDump);

        // === Constants ===

        #region Constants

        /// <summary>另存目录名，位于 {UserData}/ModsData/MapExt2 下。</summary>
        private const string kDumpFolder = "RegistrationDump";

        #endregion

        // === Fields ===

        #region Fields

        private static readonly Assembly s_ModAssembly = typeof(RegistrationDump).Assembly;
        private static readonly Assembly s_GameAssembly = typeof(UpdateSystem).Assembly;

        /// <summary>泛型元数标记，如 PostDeserialize`1 中的 `1。</summary>
        private static readonly Regex s_GenericArity = new Regex(@"`\d+", RegexOptions.Compiled);

        #endregion

        // === Public API ===

        #region Public API

        /// <summary>
        /// 把更新次序、启用状态与补丁清单写入 MapExtPDX.log。
        /// </summary>
        /// <returns>供设定页显示的单行结果摘要。</returns>
        public static string Export()
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    ModLog.Warn(Tag, "World 尚未就绪，请先加载存档。");
                    return "World not ready — load a save first.";
                }

                var updateSystem = world.GetExistingSystemManaged<UpdateSystem>();
                if (updateSystem == null)
                {
                    ModLog.Warn(Tag, "找不到 UpdateSystem。");
                    return "UpdateSystem not found.";
                }

                List<string> order = CollectUpdateOrder(updateSystem);
                List<string> states = CollectEnabledStates(world);
                List<string> patches = CollectPatches();

                ModSettings s = Mod.Instance?.Settings;
                string settings = s == null
                    ? "—"
                    : $"EcoFix={s.isEnableEconomyFix}, Demand={s.EnableDemandEcoSystem}, Job={s.EnableJobSearchEcoSystem}, " +
                      $"Household={s.EnableHouseholdPropertyEcoSystem}, ResBuyer={s.EnableResourceBuyerEcoSystem}, " +
                      $"ResidentAI={s.EnableResidentAIEcoSystem}, DownAI={s.EnableDownstreamAIEcoSystem}";

                var lines = new List<string>
                {
                    $"模式: {PatchManager.CurrentMode} (CV={PatchManager.CurrentCoreValue})",
                    $"设定（实时值）: {settings}",
                    $"生效配置（快照）: {AppliedConfig.Current?.ToString() ?? "—"}",
                    $"更新次序: {order.Count}",
                    $"启用状态: {states.Count}",
                    $"补丁: {patches.Count}",
                };
                lines.AddRange(order);
                lines.AddRange(states);
                lines.AddRange(patches);

                ModLog.Report(Tag, "系统注册表", rb =>
                {
                    foreach (string line in lines) rb.Add(line);
                });

                string file = WriteDumpFile(lines);
                return file == null
                    ? $"Exported {lines.Count} lines — see MapExtPDX.log (file copy failed)"
                    : $"Exported {lines.Count} lines — ModsData/MapExt2/{kDumpFolder}/{file}";
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, "系统注册表导出失败。");
                return $"Export failed: {ex.Message}";
            }
        }

        #endregion

        // === Helpers ===

        #region Helpers

        /// <summary>
        /// 按 m_Updates 的顺序逐 phase 列出 MapExt 系统：
        /// "次序 | phase | 相对序号 | 系统类型 | 前接 最近的原版系统"。
        /// </summary>
        private static List<string> CollectUpdateOrder(UpdateSystem updateSystem)
        {
            var lines = new List<string>();

            Type dataType = AccessTools.Inner(typeof(UpdateSystem), "SystemData");
            FieldInfo updatesField = AccessTools.Field(typeof(UpdateSystem), "m_Updates");
            FieldInfo phaseField = dataType == null ? null : AccessTools.Field(dataType, "m_Phase");
            FieldInfo systemField = dataType == null ? null : AccessTools.Field(dataType, "m_System");
            if (updatesField == null || phaseField == null || systemField == null)
            {
                lines.Add("次序 | 字段缺失（m_Updates／SystemData.m_Phase／SystemData.m_System）");
                return lines;
            }

            // m_Updates 由 Refresh 按 phase 分段生成，同一 phase 的条目连续
            var counts = new Dictionary<SystemUpdatePhase, int>();
            SystemUpdatePhase currentPhase = SystemUpdatePhase.Invalid;
            string anchor = "—";
            foreach (object entry in (IEnumerable)updatesField.GetValue(updateSystem))
            {
                var phase = (SystemUpdatePhase)phaseField.GetValue(entry);
                var system = (ComponentSystemBase)systemField.GetValue(entry);
                if (phase != currentPhase)
                {
                    currentPhase = phase;
                    anchor = "—";
                }

                Type type = system.GetType();
                if (IsMapExt(type))
                {
                    counts.TryGetValue(phase, out int n);
                    counts[phase] = ++n;
                    lines.Add($"次序 | {phase} | {n:D2} | {FormatType(type)} | 前接 {anchor}");
                }
                else if (type.Assembly == s_GameAssembly)
                {
                    anchor = FormatType(type);
                }
            }

            return lines;
        }

        /// <summary>
        /// 列出全部 MapExt 系统的 Enabled，以及处于禁用状态的原版系统；两组各自按类型名排序。
        /// </summary>
        private static List<string> CollectEnabledStates(World world)
        {
            var mapExt = new List<string>();
            var vanillaDisabled = new List<string>();
            foreach (ComponentSystemBase system in world.Systems)
            {
                Type type = system.GetType();
                if (IsMapExt(type))
                    mapExt.Add($"MapExt 系统 | {FormatType(type)} | Enabled={system.Enabled}");
                else if (!system.Enabled && type.Assembly == s_GameAssembly)
                    vanillaDisabled.Add($"原版已禁用 | {FormatType(type)}");
            }

            mapExt.Sort(StringComparer.Ordinal);
            vanillaDisabled.Sort(StringComparer.Ordinal);
            mapExt.AddRange(vanillaDisabled);
            return mapExt;
        }

        /// <summary>
        /// 列出 MapExt 两个 Harmony 实例挂上的全部补丁："补丁 | 目标方法 | 类型 | owner:补丁方法, …"。
        /// </summary>
        private static List<string> CollectPatches()
        {
            var lines = new List<string>();
            foreach (MethodBase original in Harmony.GetAllPatchedMethods())
            {
                HarmonyLib.Patches info = Harmony.GetPatchInfo(original);
                if (info == null) continue;

                string target = FormatMethod(original);
                AddPatchLine(lines, target, "Prefix", info.Prefixes);
                AddPatchLine(lines, target, "Postfix", info.Postfixes);
                AddPatchLine(lines, target, "Transpiler", info.Transpilers);
                AddPatchLine(lines, target, "Finalizer", info.Finalizers);
            }

            lines.Sort(StringComparer.Ordinal);
            return lines;
        }

        /// <summary>同一目标同一类型的 MapExt 补丁合成一行，保持 Harmony 的登记顺序。</summary>
        private static void AddPatchLine(List<string> lines, string target, string kind, IEnumerable<HarmonyLib.Patch> patches)
        {
            List<string> own = patches
                .Where(p => p.owner == Mod.HarmonyIdGlobal || p.owner == Mod.HarmonyIdModes)
                .Select(p => $"{(p.owner == Mod.HarmonyIdGlobal ? "global" : "modes")}:" +
                             $"{FormatType(p.PatchMethod.DeclaringType)}.{p.PatchMethod.Name}")
                .ToList();
            if (own.Count > 0)
                lines.Add($"补丁 | {target} | {kind} | {string.Join(", ", own)}");
        }

        /// <summary>
        /// 另存一份到 {UserData}/ModsData/MapExt2/RegistrationDump。日志在每次启动游戏时被覆盖，
        /// 轮流切换配置、重启采集基线时靠这份文件保留每次的结果。失败只记警告，日志中的报告不受影响。
        /// </summary>
        /// <returns>写出的文件名；失败时返回 null。</returns>
        private static string WriteDumpFile(List<string> lines)
        {
            try
            {
                string dir = Path.Combine(EnvPath.kUserDataPath, "ModsData", "MapExt2", kDumpFolder);
                Directory.CreateDirectory(dir);
                string name = $"registry-{DateTime.Now:yyyyMMdd-HHmmss}-{PatchManager.CurrentMode}.txt";
                File.WriteAllLines(Path.Combine(dir, name), lines, new UTF8Encoding(false));
                ModLog.Ok(Tag, $"已另存：{Path.Combine(dir, name)}");
                return name;
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"另存文件失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>类型本身或其泛型参数来自 MapExt 程序集，如 PostDeserialize&lt;ModeA.SoilWaterSystemMod&gt;。</summary>
        private static bool IsMapExt(Type type)
        {
            if (type.Assembly == s_ModAssembly) return true;
            return type.IsGenericType && type.GetGenericArguments().Any(IsMapExt);
        }

        /// <summary>带命名空间的类型名；泛型写成 Name&lt;Arg&gt;，嵌套类型保留 +。</summary>
        private static string FormatType(Type type)
        {
            if (type == null) return "?";
            if (!type.IsGenericType) return type.FullName ?? type.Name;

            string name = s_GenericArity.Replace(type.GetGenericTypeDefinition().FullName ?? type.Name, "");
            return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(FormatType))}>";
        }

        /// <summary>声明类型.方法名(参数类型短名)，用参数区分重载。</summary>
        private static string FormatMethod(MethodBase method)
        {
            string parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
            return $"{FormatType(method.DeclaringType)}.{method.Name}({parameters})";
        }

        #endregion
    }
}
