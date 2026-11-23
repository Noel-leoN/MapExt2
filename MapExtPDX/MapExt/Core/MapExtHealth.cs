// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit MapExt2 and the author.

using System;
using System.Collections.Generic;
using System.Linq;
using Game.PSI;
using Game.UI.Localization;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// 本会话的健康状态：关键组件没能完整挂上时进入"降级"。
    ///
    /// <para><b>为什么要有</b>：补丁集与替换声明都是逐项 try/catch 后继续执行，失败若只写日志容易被忽略。
    /// 对地图尺寸 Mod 来说，"地形补上了、水没补上"比整体失效更危险：玩家照常读档、存档，
    /// 扩展数据就按两种尺寸混着写进存档。降级把这类失败变成可见事件：推送主菜单通知，
    /// 系统状态栏与注册表导出也会显示。</para>
    ///
    /// <para><b>判定口径</b>：失败＝补丁集的应用委托或替换声明的步骤有异常逸出。
    /// 逐项尽力执行的辅助方法（<c>JobPatchHelper</c>、<c>CellMapSystemPatchManager</c> 等）做完能做的部分后汇总抛出，
    /// 所以"部分成功"也算失败。算不算关键由调用方决定：<see cref="PatchManager"/> 按补丁集分级，
    /// <see cref="ReplacementRegistrar"/> 把 CellMap 组的声明一律算作关键。</para>
    ///
    /// <para><see cref="IsDegraded"/> 同时涵盖 OnLoad 中断（<see cref="Mod.InitializationFailed"/>），
    /// 存档加载闸门只需读这一个标志。</para>
    /// </summary>
    public static class MapExtHealth
    {
        private const string Tag = nameof(MapExtHealth);

        // === Constants ===

        #region Constants

        private const string kNotificationId = "mapext.degraded";

        /// <summary>通知与状态栏最多列出的组件数，其余折叠成 "and N more"。</summary>
        private const int kMaxListedComponents = 3;

        #endregion

        // === Fields ===

        #region Fields

        private static readonly List<string> s_Components = new List<string>();
        private static readonly List<string> s_Reasons = new List<string>();

        /// <summary>本会话是否处于降级状态：有关键组件失败，或 OnLoad 中断。</summary>
        public static bool IsDegraded => s_Reasons.Count > 0 || Mod.InitializationFailed;

        /// <summary>关键失败明细（"组件: 详情"），按发生先后排列；不含 OnLoad 中断。</summary>
        public static IReadOnlyList<string> Reasons => s_Reasons;

        /// <summary>状态栏用的简短标签：健康时为 null，降级时如 "DEGRADED (TerrainSystemPatch, WaterSystemInitFix)"。</summary>
        public static string StatusLabel => IsDegraded ? $"DEGRADED ({ListComponents(includeInitFailure: true)})" : null;

        /// <summary>失败组件的简短清单（最多列 3 个，含 OnLoad 中断），供存档闸门的降级提示使用。</summary>
        public static string ComponentSummary => ListComponents(includeInitFailure: true);

        #endregion

        // === Public API ===

        #region Public API

        /// <summary>记录一项关键失败：写错误日志，并推送（或更新）同一条降级通知。</summary>
        /// <param name="component">失败的组件：补丁集键名，或替换声明的"分组/名称"。会显示在通知里。</param>
        /// <param name="detail">失败详情，只进日志与诊断。</param>
        public static void MarkDegraded(string component, string detail)
        {
            s_Components.Add(component);
            s_Reasons.Add($"{component}: {detail}");
            ModLog.Error(Tag, $"关键组件失败，本会话进入降级状态：{component} — {detail}");
            PushNotification();
        }

        #endregion

        // === Helpers ===

        #region Helpers

        /// <summary>
        /// 推送降级通知；同一 identifier 再次推送会更新原通知，不会叠加。
        /// <para>文案<b>硬编码英文</b>并用 <c>LocalizedString.Value</c>，与 OnLoad 中断通知一致：
        /// 失败可能发生在本地化注册之前或本身就与之相关，不依赖查表。</para>
        /// </summary>
        private static void PushNotification()
        {
            try
            {
                NotificationSystem.Push(
                    identifier: kNotificationId,
                    title: LocalizedString.Value("MapExt: Core patches failed"),
                    text: LocalizedString.Value(
                        $"[MapExt] {s_Components.Count} critical component(s) failed to load: " +
                        $"{ListComponents(includeInitFailure: false)}. " +
                        "Map extension is NOT fully active this session - do NOT load or save an extended-size city. " +
                        "Restart the game; if it repeats, report Logs/MapExtPDX.log."),
                    progressState: Colossal.PSI.Common.ProgressState.Failed,
                    progress: 100
                );
            }
            catch (Exception ex)
            {
                // 通知推不出去（UI 尚未就绪等）时，至少日志里已有上面那条错误
                ModLog.Warn(Tag, $"降级通知推送失败: {ex.Message}");
            }
        }

        private static string ListComponents(bool includeInitFailure)
        {
            var items = new List<string>(s_Components);
            if (includeInitFailure && Mod.InitializationFailed) items.Add("OnLoad interrupted");

            if (items.Count <= kMaxListedComponents) return string.Join(", ", items);
            return $"{string.Join(", ", items.Take(kMaxListedComponents))} and {items.Count - kMaxListedComponents} more";
        }

        #endregion

#if DEBUG
        // === Fault Injection（仅 DEBUG 编译）===

        #region Fault Injection

        private static HashSet<string> s_FaultInjected;

        /// <summary>
        /// 开发者自检：{UserData}/ModsData/MapExt2/FaultInject.txt 每行写一个组件名
        /// （补丁集键名，或替换声明的"分组/名称"，如 CellMap/AirPollutionSystemMod；# 开头为注释）。
        /// 列出的组件照常挂上后再抛异常，游戏照常运行，只用来验证降级通知与诊断显示。文件只读一次。
        /// </summary>
        public static bool IsFaultInjected(string component)
        {
            if (s_FaultInjected == null) s_FaultInjected = LoadFaultInjection();
            return s_FaultInjected.Contains(component);
        }

        private static HashSet<string> LoadFaultInjection()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                string path = System.IO.Path.Combine(
                    Colossal.PSI.Environment.EnvPath.kUserDataPath, "ModsData", "MapExt2", "FaultInject.txt");
                if (!System.IO.File.Exists(path)) return names;

                foreach (string line in System.IO.File.ReadAllLines(path))
                {
                    string name = line.Trim();
                    if (name.Length > 0 && !name.StartsWith("#", StringComparison.Ordinal)) names.Add(name);
                }

                ModLog.Warn(Tag, $"故障注入已启用（DEBUG 自检）：{string.Join(", ", names)}");
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"读取故障注入文件失败：{ex.Message}");
            }

            return names;
        }

        #endregion
#endif
    }
}
