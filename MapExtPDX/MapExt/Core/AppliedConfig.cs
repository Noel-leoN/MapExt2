// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit MapExt2 and the author.

using System.Collections.Generic;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// 本会话实际生效的配置快照：<see cref="Mod"/> 的 OnLoad 捕获一次，之后只读。
    ///
    /// <para><b>为什么要快照</b>：地图尺寸模式、经济总开关和六个经济分组决定了 OnLoad 时注册哪些替换系统、
    /// 禁用哪些原版系统，整局不能再改。注册、读档时的重新禁用、诊断与寻路 handler 都只读这份快照，
    /// 不读 <see cref="ModSettings"/> 的实时值；玩家中途改了这些设定，要重启后才生效，
    /// 状态栏据 <see cref="DescribePendingRestart"/> 提示"待重启"。</para>
    ///
    /// <para><b>冲突自动停用只记在快照里</b>（<see cref="AutoDisabledGroups"/>），不写回设定：
    /// 改写设定对象的话，玩家之后调任何选项触发保存，自动停用就会被永久写进 .coc。</para>
    /// </summary>
    public sealed class AppliedConfig
    {
        // === Constants ===

        #region Constants

        // 分组名，与 ModConflictDetector 自动停用时使用的名称一致
        public const string GroupDemand = "Demand";
        public const string GroupJobSearch = "JobSearch";
        public const string GroupHouseholdProperty = "HouseholdProperty";
        public const string GroupResourceBuyer = "ResourceBuyer";
        public const string GroupResidentAI = "ResidentAI";
        public const string GroupDownstreamAI = "DownstreamAI";

        #endregion

        // === Fields ===

        #region Fields

        private readonly HashSet<string> m_AutoDisabled;

        /// <summary>本会话的快照；OnLoad 捕获之前（或 OnLoad 中断时）为 null。</summary>
        public static AppliedConfig Current { get; private set; }

        public PatchModeSetting Mode { get; }
        public int CoreValue { get; }
        public bool EconomyFix { get; }

        // 各分组是否生效：设定开启，且没有因冲突被自动停用
        public bool Demand { get; }
        public bool JobSearch { get; }
        public bool HouseholdProperty { get; }
        public bool ResourceBuyer { get; }
        public bool ResidentAI { get; }
        public bool DownstreamAI { get; }

        /// <summary>因冲突 Mod 在本会话自动停用的分组（不写回设定）。</summary>
        public IReadOnlyCollection<string> AutoDisabledGroups => m_AutoDisabled;

        #endregion

        // === Lifecycle ===

        #region Lifecycle

        private AppliedConfig(ModSettings s, HashSet<string> autoDisabled)
        {
            m_AutoDisabled = autoDisabled;
            Mode = PatchManager.CurrentMode;
            CoreValue = PatchManager.CurrentCoreValue;
            EconomyFix = s.isEnableEconomyFix;
            Demand = Effective(s.EnableDemandEcoSystem, GroupDemand);
            JobSearch = Effective(s.EnableJobSearchEcoSystem, GroupJobSearch);
            HouseholdProperty = Effective(s.EnableHouseholdPropertyEcoSystem, GroupHouseholdProperty);
            ResourceBuyer = Effective(s.EnableResourceBuyerEcoSystem, GroupResourceBuyer);
            ResidentAI = Effective(s.EnableResidentAIEcoSystem, GroupResidentAI);
            DownstreamAI = Effective(s.EnableDownstreamAIEcoSystem, GroupDownstreamAI);
        }

        /// <summary>
        /// 按设定与本会话自动停用的分组生成快照，并设为 <see cref="Current"/>。
        /// 只应在 OnLoad 中、<see cref="PatchManager.Initialize"/> 之后调用一次。
        /// </summary>
        public static AppliedConfig Capture(ModSettings settings, IEnumerable<string> autoDisabledGroups)
        {
            var disabled = autoDisabledGroups == null
                ? new HashSet<string>()
                : new HashSet<string>(autoDisabledGroups);
            Current = new AppliedConfig(settings, disabled);
            return Current;
        }

        #endregion

        // === Public API ===

        #region Public API

        /// <summary>
        /// 实时设定与快照不一致时，返回需要重启才生效的差异摘要，如 "Mode ModeA→ModeB, ResidentAI off→on"；一致时返回 null。
        /// </summary>
        public string DescribePendingRestart(ModSettings live)
        {
            if (live == null) return null;

            var diffs = new List<string>();
            if (live.PatchModeChoice != Mode)
                diffs.Add($"Mode {Mode}→{live.PatchModeChoice}");
            if (live.isEnableEconomyFix != EconomyFix)
                diffs.Add($"EconomyFix {OnOff(EconomyFix)}→{OnOff(live.isEnableEconomyFix)}");

            AddGroupDiff(diffs, GroupDemand, Demand, live.EnableDemandEcoSystem);
            AddGroupDiff(diffs, GroupJobSearch, JobSearch, live.EnableJobSearchEcoSystem);
            AddGroupDiff(diffs, GroupHouseholdProperty, HouseholdProperty, live.EnableHouseholdPropertyEcoSystem);
            AddGroupDiff(diffs, GroupResourceBuyer, ResourceBuyer, live.EnableResourceBuyerEcoSystem);
            AddGroupDiff(diffs, GroupResidentAI, ResidentAI, live.EnableResidentAIEcoSystem);
            AddGroupDiff(diffs, GroupDownstreamAI, DownstreamAI, live.EnableDownstreamAIEcoSystem);

            return diffs.Count == 0 ? null : string.Join(", ", diffs);
        }

        /// <summary>单行描述，供日志与开发者导出使用。</summary>
        public override string ToString() =>
            $"Mode={Mode}, CV={CoreValue}, EcoFix={EconomyFix}, Demand={Demand}, Job={JobSearch}, " +
            $"Household={HouseholdProperty}, ResBuyer={ResourceBuyer}, ResidentAI={ResidentAI}, DownAI={DownstreamAI}, " +
            $"AutoDisabled=[{string.Join(", ", m_AutoDisabled)}]";

        #endregion

        // === Helpers ===

        #region Helpers

        private bool Effective(bool enabledInSettings, string group) =>
            enabledInSettings && !m_AutoDisabled.Contains(group);

        /// <summary>
        /// 比较一个分组。自动停用的分组在重启后仍会被停用（冲突 Mod 还在），
        /// 所以先按同样规则折算实时值，再与快照比较。
        /// </summary>
        private void AddGroupDiff(List<string> diffs, string group, bool applied, bool liveSetting)
        {
            bool live = Effective(liveSetting, group);
            if (live != applied)
                diffs.Add($"{group} {OnOff(applied)}→{OnOff(live)}");
        }

        private static string OnOff(bool value) => value ? "on" : "off";

        #endregion
    }
}
