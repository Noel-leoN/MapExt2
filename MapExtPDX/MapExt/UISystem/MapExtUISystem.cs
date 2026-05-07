// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using Colossal.UI.Binding;
using Game;
using Game.UI;
using MapExtPDX.MapExt.Core;

namespace MapExtPDX.UI
{
    /// <summary>
    /// 🎛️ [MOD] MapExt 游戏内 UI 中央控制器
    /// 继承 UISystemBase，通过 ValueBinding/TriggerBinding 实现前端双向实时调参。
    /// Phase 1 暴露 7 个高频参数（租金 5 + 寻路 2），共 19 个 Binding。
    /// 面板关闭时跳过所有更新（零开销）。
    /// </summary>
    public partial class MapExtUISystem : UISystemBase
    {
        #region Constants

        private const string Tag = "UISystem";
        private const string kGroup = "mapext";

        #endregion

        #region Fields — 面板状态

        private ValueBinding<bool> m_PanelOpen;
        private ValueBinding<bool> m_ConfigOpen;

        #endregion

        #region Fields — 租金核心参数 (5 value + 5 trigger = 10)

        private ValueBinding<int> m_RentMultRes;
        private ValueBinding<int> m_RentMultCom;
        private ValueBinding<int> m_RentMultInd;
        private ValueBinding<int> m_LandValueEnvEffect;
        private ValueBinding<int> m_ServiceBonusCap;

        #endregion

        #region Fields — 寻路核心参数 (2 value + 2 trigger = 4)

        private ValueBinding<float> m_ShoppingMaxCost;
        private ValueBinding<float> m_LeisureMaxCost;

        #endregion

        #region Lifecycle

        public override GameMode gameMode => GameMode.Game;

        protected override void OnCreate()
        {
            base.OnCreate();

            var s = Mod.Instance.Settings;

            // === 面板状态 Bindings (4) ===
            AddBinding(m_PanelOpen = new ValueBinding<bool>(kGroup, "PanelOpen", false));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetPanelOpen", v => m_PanelOpen.Update(v)));

            AddBinding(m_ConfigOpen = new ValueBinding<bool>(kGroup, "ConfigOpen", false));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetConfigOpen", v => m_ConfigOpen.Update(v)));

            // === 只读信息 (GetterValueBinding，自动脏检查) ===
            AddUpdateBinding(new GetterValueBinding<string>(
                kGroup, "MapSizeInfo",
                () => $"{PatchManager.CurrentCoreValue * 14336}m"));

            AddUpdateBinding(new GetterValueBinding<string>(
                kGroup, "SystemStatus",
                () => Mod.Instance?.Settings?.SystemStatusReport ?? "N/A"));

            // === 租金核心参数 Bindings (10) ===
            // --- 住宅租金乘数 ---
            AddBinding(m_RentMultRes = new ValueBinding<int>(kGroup, "RentMultRes", s.RentMultiplierResidential));
            AddBinding(new TriggerBinding<int>(kGroup, "SetRentMultRes", v =>
            {
                Mod.Instance.Settings.RentMultiplierResidential = v;
                m_RentMultRes.Update(v);
            }));

            // --- 商业租金乘数 ---
            AddBinding(m_RentMultCom = new ValueBinding<int>(kGroup, "RentMultCom", s.RentMultiplierCommercial));
            AddBinding(new TriggerBinding<int>(kGroup, "SetRentMultCom", v =>
            {
                Mod.Instance.Settings.RentMultiplierCommercial = v;
                m_RentMultCom.Update(v);
            }));

            // --- 工业租金乘数 ---
            AddBinding(m_RentMultInd = new ValueBinding<int>(kGroup, "RentMultInd", s.RentMultiplierIndustrial));
            AddBinding(new TriggerBinding<int>(kGroup, "SetRentMultInd", v =>
            {
                Mod.Instance.Settings.RentMultiplierIndustrial = v;
                m_RentMultInd.Update(v);
            }));

            // --- 环境地价系数 ---
            AddBinding(m_LandValueEnvEffect = new ValueBinding<int>(kGroup, "LandValueEnv", s.LandValueEnvironmentEffect));
            AddBinding(new TriggerBinding<int>(kGroup, "SetLandValueEnv", v =>
            {
                Mod.Instance.Settings.LandValueEnvironmentEffect = v;
                m_LandValueEnvEffect.Update(v);
            }));

            // --- 服务加成系数 ---
            AddBinding(m_ServiceBonusCap = new ValueBinding<int>(kGroup, "ServiceBonus", s.ServiceBonusCapMultiplier));
            AddBinding(new TriggerBinding<int>(kGroup, "SetServiceBonus", v =>
            {
                Mod.Instance.Settings.ServiceBonusCapMultiplier = v;
                m_ServiceBonusCap.Update(v);
            }));

            // === 寻路核心参数 Bindings (4) ===
            // --- 购物寻路上限 ---
            AddBinding(m_ShoppingMaxCost = new ValueBinding<float>(kGroup, "ShopMaxCost", s.ShoppingMaxCost));
            AddBinding(new TriggerBinding<float>(kGroup, "SetShopMaxCost", v =>
            {
                Mod.Instance.Settings.ShoppingMaxCost = v;
                m_ShoppingMaxCost.Update(v);
            }));

            // --- 休闲寻路上限 ---
            AddBinding(m_LeisureMaxCost = new ValueBinding<float>(kGroup, "LeisureMaxCost", s.LeisureMaxCost));
            AddBinding(new TriggerBinding<float>(kGroup, "SetLeisureMaxCost", v =>
            {
                Mod.Instance.Settings.LeisureMaxCost = v;
                m_LeisureMaxCost.Update(v);
            }));

            // === 重置按钮 Triggers (2) ===
            AddBinding(new TriggerBinding(kGroup, "ResetRentControl", () =>
            {
                var settings = Mod.Instance.Settings;
                settings.RentMultiplierResidential = 100;
                settings.RentMultiplierCommercial = 100;
                settings.RentMultiplierIndustrial = 100;
                settings.LandValueEnvironmentEffect = 40;
                settings.ServiceBonusCapMultiplier = 100;

                // 同步 ValueBindings
                m_RentMultRes.Update(100);
                m_RentMultCom.Update(100);
                m_RentMultInd.Update(100);
                m_LandValueEnvEffect.Update(40);
                m_ServiceBonusCap.Update(100);
            }));

            AddBinding(new TriggerBinding(kGroup, "ResetPathfinding", () =>
            {
                var settings = Mod.Instance.Settings;
                settings.ShoppingMaxCost = 8000f;
                settings.LeisureMaxCost = 12000f;

                m_ShoppingMaxCost.Update(8000f);
                m_LeisureMaxCost.Update(12000f);
            }));

            ModLog.Ok(Tag, "MapExtUISystem 已创建 (Phase 1: 21 Bindings)");
        }

        #endregion

        #region System Loop

        protected override void OnUpdate()
        {
            // GetterValueBinding 的自动更新由 base.OnUpdate() 处理
            base.OnUpdate();

            // 面板关闭时跳过所有脏检查（零开销）
            if (!m_PanelOpen.value) return;

            // === 反向同步：检测 Options UI 是否修改了值 ===
            var s = Mod.Instance.Settings;

            // --- 租金参数 ---
            if (m_RentMultRes.value != s.RentMultiplierResidential)
                m_RentMultRes.Update(s.RentMultiplierResidential);

            if (m_RentMultCom.value != s.RentMultiplierCommercial)
                m_RentMultCom.Update(s.RentMultiplierCommercial);

            if (m_RentMultInd.value != s.RentMultiplierIndustrial)
                m_RentMultInd.Update(s.RentMultiplierIndustrial);

            if (m_LandValueEnvEffect.value != s.LandValueEnvironmentEffect)
                m_LandValueEnvEffect.Update(s.LandValueEnvironmentEffect);

            if (m_ServiceBonusCap.value != s.ServiceBonusCapMultiplier)
                m_ServiceBonusCap.Update(s.ServiceBonusCapMultiplier);

            // --- 寻路参数 ---
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (m_ShoppingMaxCost.value != s.ShoppingMaxCost)
                m_ShoppingMaxCost.Update(s.ShoppingMaxCost);

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (m_LeisureMaxCost.value != s.LeisureMaxCost)
                m_LeisureMaxCost.Update(s.LeisureMaxCost);
        }

        #endregion
    }
}
