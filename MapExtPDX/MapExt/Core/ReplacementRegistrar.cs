// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Collections.Generic;
using Game;
using Game.Pathfind;
using Game.Serialization;
using HarmonyLib;
using MapExtPDX.EcoShared;
using MapExtPDX.MapExt.ReBurstSystem.Core;
using Unity.Entities;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// 声明式替换注册表：同一份声明驱动系统注册（<see cref="Apply"/>）、读档时重新禁用原版（<see cref="ReDisable"/>）
    /// 与诊断（<see cref="Diagnose"/>）。
    ///
    /// <para><b>为什么</b>：原先注册、禁用原版、重新禁用和诊断各写一份清单，靠人工保持一致。
    /// CellOnly 路径就因为注册处漏了禁用原版，原版找房／租金系统与替换系统同时运行（审查报告 P0-1）。
    /// 现在每条声明自带它取代的原版系统，四处共用同一份清单。</para>
    ///
    /// <para><b>顺序即更新顺序</b>：<see cref="Apply"/> 严格按声明顺序执行，每条声明内部也按链式调用的顺序执行各步骤。
    /// 引擎 <c>UpdateAt</c> 以递增序号登记，同一 phase 内注册的先后就是更新的先后，调换声明会静默改变更新次序。</para>
    ///
    /// <para>只登记本会话实际启用的声明：启用条件写在各 Declare 函数里，与原先的 if 结构一一对应。</para>
    /// </summary>
    public sealed class ReplacementRegistrar
    {
        private const string Tag = nameof(ReplacementRegistrar);

        // === Constants ===

        #region Constants

        /// <summary>
        /// CellMap 替换（含 CellOnly 路径）：随地图尺寸决定，不受经济开关约束。
        /// 本组声明失败一律按关键处理，让本会话进入降级状态（见 <see cref="MapExtHealth"/>）。
        /// </summary>
        public const string GroupCellMap = "CellMap";

        /// <summary>只受经济总开关约束、不属于任何分组的声明（如 NetUpkeep 禁用）。</summary>
        public const string GroupEconomy = "Economy";

        #endregion

        // === Fields ===

        #region Fields

        private readonly List<Declaration> m_Declarations = new List<Declaration>();
        private readonly List<string> m_Failures = new List<string>();
        private readonly HashSet<Type> m_ReDisabledSinceLastDiag = new HashSet<Type>();
        private bool m_DispatcherBootstrapped;

        /// <summary>已登记的声明数。</summary>
        public int Count => m_Declarations.Count;

        /// <summary>本会话登记的全部替换声明（供运行期冲突归因与状态轮询读取）。</summary>
        public IReadOnlyList<Declaration> Declarations => m_Declarations;

        /// <summary>Apply 中执行失败的声明（"分组/名称: 原因"），供诊断与注册表导出显示；CellMap 组的失败另记入健康状态。</summary>
        public IReadOnlyList<string> Failures => m_Failures;

        #endregion

        // === Declaration API ===

        #region Declaration API

        /// <summary>声明替换系统 <typeparamref name="TMod"/>，并列出它取代、须禁用的原版系统。</summary>
        public Declaration<TMod> Replace<TMod>(string group, params Type[] vanilla) where TMod : ComponentSystemBase
        {
            var declaration = new Declaration<TMod>(group, vanilla);
            m_Declarations.Add(declaration);
            return declaration;
        }

        /// <summary>只禁用原版系统、不注册替换（功能已由其他原版系统接手的残骸系统）。</summary>
        public void DisableOnly(string group, params Type[] vanilla)
        {
            m_Declarations.Add(new Declaration(group, ShortName(vanilla[0]), vanilla, null));
        }

        /// <summary>
        /// 需求 Job 置换：委托给 <see cref="JobPatchHelper"/>。原版需求系统保持启用，作为 Transpiler 宿主。
        /// 挂载不完整时 JobPatchHelper 汇总抛出，记为本声明失败（Demand 组，不降级）。
        /// </summary>
        public void DemandJobs(IEnumerable<JobPatchTarget> targets)
        {
            var declaration = new Declaration(AppliedConfig.GroupDemand, "DemandJobs", Array.Empty<Type>(), null);
            declaration.AddStep((u, h) => JobPatchHelper.Apply(h, targets));
            m_Declarations.Add(declaration);
        }

        /// <summary>
        /// 在 <c>PathfindSetupSystem.FindTargets</c> 的统一 dispatcher 上登记一个 handler。
        /// 第一个 handler 执行时自动 Clear 并挂载 dispatcher：新增 handler 不必再同步维护引导条件（mod-traps §2.3）。
        /// </summary>
        public void FindTargetsHandler(string group, SetupTargetType type, IFindTargetsHandler handler)
        {
            var declaration = new Declaration(group, $"{type}Handler", Array.Empty<Type>(), null);
            declaration.AddStep((u, h) =>
            {
                if (!m_DispatcherBootstrapped)
                {
                    PathfindSetupDispatcher.Clear();
                    h.CreateClassProcessor(typeof(PathfindSetupDispatcher)).Patch();
                    m_DispatcherBootstrapped = true;
                }

                PathfindSetupDispatcher.Register(type, handler);
            });
            m_Declarations.Add(declaration);
        }

        #endregion

        // === Execution ===

        #region Execution

        /// <summary>
        /// 按声明顺序执行：先禁用该声明取代的原版系统，再按顺序执行它的注册步骤。
        /// 单条声明失败只记录并继续，结果见 <see cref="Failures"/>；CellMap 组的失败让本会话进入降级状态。
        /// </summary>
        public void Apply(UpdateSystem updateSystem, Harmony harmony)
        {
            World world = updateSystem.World;
            int applied = 0;
            foreach (Declaration declaration in m_Declarations)
            {
                string component = $"{declaration.Group}/{declaration.Name}";
                try
                {
                    foreach (Type vanilla in declaration.Vanilla)
                        world.GetOrCreateSystemManaged(vanilla).Enabled = false;
                    foreach (var step in declaration.Steps)
                        step(updateSystem, harmony);
#if DEBUG
                    // 故障注入：步骤照常执行完再抛出，只验证降级信号
                    if (MapExtHealth.IsFaultInjected(component))
                        throw new InvalidOperationException("Fault injected (DEBUG self-test)");
#endif
                    applied++;
                }
                catch (Exception ex)
                {
                    m_Failures.Add($"{component}: {ex.Message}");
                    ModLog.Error(Tag, ex, $"替换声明执行失败：{component}");

                    // 原版系统在步骤之前已禁用：步骤失败时替换系统可能没注册或缺补丁，
                    // 对应的 CellMap 数据既没有原版也没有替换系统在正确维护
                    if (declaration.Group == GroupCellMap)
                        MapExtHealth.MarkDegraded(component, ex.Message);
                }
            }

            if (m_Failures.Count == 0)
                ModLog.Ok(Tag, $"已按顺序执行 {applied} 条替换声明");
            else
                ModLog.Error(Tag, $"替换声明 {m_Failures.Count} 条失败、{applied} 条成功：{string.Join("; ", m_Failures)}");
        }

        /// <summary>
        /// 防御性重新禁用：已被启用的原版系统再禁用一次，返回本次重新禁用的数量。
        /// 同时将实际发生重禁用的原版系统暂存至待报集合，防止 <c>OnGamePreload</c>/<c>OnGameLoadingComplete</c> 先修复后诊断掩盖外部重开记录。
        /// </summary>
        public int ReDisable(World world)
        {
            int count = 0;
            foreach (Declaration declaration in m_Declarations)
            {
                foreach (Type vanilla in declaration.Vanilla)
                {
                    var system = world.GetExistingSystemManaged(vanilla);
                    if (system == null || !system.Enabled) continue;

                    system.Enabled = false;
                    m_ReDisabledSinceLastDiag.Add(vanilla);
                    ModLog.Info(Tag, $"  重新禁用: {ShortName(vanilla)}");
                    count++;
                }
            }

            return count;
        }

        /// <summary>取出并清空自上次诊断以来在 <see cref="ReDisable"/> 阶段曾被外部重开的原版系统列表。</summary>
        public List<Type> ConsumeReDisabledVanillaSystems()
        {
            var list = new List<Type>(m_ReDisabledSinceLastDiag);
            m_ReDisabledSinceLastDiag.Clear();
            return list;
        }

        /// <summary>
        /// 逐条检查：取代的原版系统已禁用，替换系统仍启用（引擎会在读档回调抛异常时静默停用系统）。
        /// 找不到的系统视为通过，与 ConflictMonitoringSystem 原有的检查口径一致。
        /// </summary>
        public void Diagnose(World world, List<string> conflicts, ref int okCount, ref int totalChecked)
        {
            foreach (Declaration declaration in m_Declarations)
            {
                foreach (Type vanilla in declaration.Vanilla)
                {
                    totalChecked++;
                    var system = world.GetExistingSystemManaged(vanilla);
                    if (system != null && system.Enabled)
                        conflicts.Add($"{ShortName(vanilla)} [{declaration.Group}]");
                    else
                        okCount++;
                }

                if (declaration.ModType == null) continue;

                totalChecked++;
                var mod = world.GetExistingSystemManaged(declaration.ModType);
                if (mod != null && !mod.Enabled)
                    conflicts.Add($"{ShortName(declaration.ModType)} [{declaration.Group}]");
                else
                    okCount++;
            }
        }

        #endregion

        // === Helpers ===

        #region Helpers

        /// <summary>嵌套类型带上外层类名，如 ResidentAISystem.Actions。</summary>
        private static string ShortName(Type type) =>
            type.DeclaringType == null ? type.Name : $"{type.DeclaringType.Name}.{type.Name}";

        #endregion

        // === Declarations ===

        #region Declarations

        /// <summary>一条声明：分组、取代的原版系统、替换系统类型，以及按调用顺序执行的注册步骤。</summary>
        public class Declaration
        {
            internal readonly List<Action<UpdateSystem, Harmony>> Steps = new List<Action<UpdateSystem, Harmony>>();

            public string Group { get; }
            public string Name { get; }
            public IReadOnlyList<Type> Vanilla { get; }

            /// <summary>替换系统类型；只禁用或只挂补丁的声明为 null。</summary>
            public Type ModType { get; }

            internal Declaration(string group, string name, Type[] vanilla, Type modType)
            {
                Group = group;
                Name = name;
                Vanilla = vanilla ?? Array.Empty<Type>();
                ModType = modType;
            }

            internal void AddStep(Action<UpdateSystem, Harmony> step) => Steps.Add(step);
        }

        /// <summary>替换系统 <typeparamref name="TMod"/> 的声明，链式追加注册步骤。</summary>
        public sealed class Declaration<TMod> : Declaration where TMod : ComponentSystemBase
        {
            internal Declaration(string group, Type[] vanilla)
                : base(group, typeof(TMod).Name, vanilla, typeof(TMod))
            {
            }

            /// <summary><c>UpdateAt&lt;TMod&gt;(phase)</c>。</summary>
            public Declaration<TMod> At(SystemUpdatePhase phase)
            {
                AddStep((u, h) => u.UpdateAt<TMod>(phase));
                return this;
            }

            /// <summary><c>UpdateAfter&lt;TActions, TMod&gt;(phase)</c>：Actions 子系统紧跟替换系统更新。</summary>
            public Declaration<TMod> WithActions<TActions>(SystemUpdatePhase phase) where TActions : ComponentSystemBase
            {
                AddStep((u, h) => u.UpdateAfter<TActions, TMod>(phase));
                return this;
            }

            /// <summary>挂载伴随的 Harmony 补丁类（<c>CreateClassProcessor(...).Patch()</c>）。</summary>
            public Declaration<TMod> WithPatches(Type patchClass)
            {
                AddStep((u, h) => h.CreateClassProcessor(patchClass).Patch());
                return this;
            }
        }

        #endregion
    }

    /// <summary>需要额外泛型约束的声明步骤。</summary>
    public static class ReplacementDeclarationExtensions
    {
        /// <summary>
        /// <c>UpdateAfter&lt;PostDeserialize&lt;TMod&gt;&gt;(Deserialize)</c>：读档完成后由引擎调用 <c>TMod.PostDeserialize</c>。
        /// 约束放在扩展方法上，<typeparamref name="TMod"/> 不实现 <see cref="IPostDeserialize"/> 时编译期即报错。
        /// </summary>
        public static ReplacementRegistrar.Declaration<TMod> WithPostDeserialize<TMod>(
            this ReplacementRegistrar.Declaration<TMod> declaration)
            where TMod : ComponentSystemBase, IPostDeserialize
        {
            declaration.AddStep((u, h) => u.UpdateAfter<PostDeserialize<TMod>>(SystemUpdatePhase.Deserialize));
            return declaration;
        }
    }
}
