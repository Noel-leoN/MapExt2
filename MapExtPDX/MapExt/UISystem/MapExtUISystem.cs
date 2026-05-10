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
    /// Phase 1: 7 个高频参数（租金 5 + 寻路 2），21 个 Binding。
    /// Phase 2: +Dashboard 统计 8 + 扩展租金 6 + 面板状态 2 = 新增 22 个，累计 43 个 Binding。
    /// Phase 3: +UI 外观 4，累计 47 个 Binding。
    /// Phase 4: +Dashboard 扩展 13（住宅空置 6 + 商业 2 + 人口活动 4 + 通勤 1），累计 60 个 Binding。
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
        private ValueBinding<bool> m_DashboardOpen;

        #endregion

        #region Fields — 租金核心参数 (Phase 1: 5 value + 5 trigger = 10)

        private ValueBinding<int> m_RentMultRes;
        private ValueBinding<int> m_RentMultCom;
        private ValueBinding<int> m_RentMultInd;
        private ValueBinding<int> m_LandValueEnvEffect;
        private ValueBinding<int> m_ServiceBonusCap;

        #endregion

        #region Fields — 寻路核心参数 (Phase 1: 2 value + 2 trigger = 4)

        private ValueBinding<float> m_ShoppingMaxCost;
        private ValueBinding<float> m_LeisureMaxCost;

        #endregion

        #region Fields — 扩展租金公式参数 (Phase 2: 6 value + 6 trigger = 12)

        private ValueBinding<int> m_LvFactorRes;
        private ValueBinding<int> m_LvFactorCom;
        private ValueBinding<int> m_LvFactorInd;
        private ValueBinding<int> m_LevelFactorRes;
        private ValueBinding<int> m_LevelFactorCom;
        private ValueBinding<int> m_LevelFactorInd;

        #endregion

        #region Fields — UI 外观参数

        private ValueBinding<int> m_UIMenuWidth;
        private ValueBinding<int> m_UIDetailWidth;
        private ValueBinding<int> m_UIPanelHeight;

        #endregion

        #region Fields — Q2 系统引用

        private Q2_CityStatsSystem m_Q2System;

        #endregion

        #region Lifecycle

        public override GameMode gameMode => GameMode.Game;

        protected override void OnCreate()
        {
            base.OnCreate();

            var s = Mod.Instance.Settings;

            // === 面板状态 Bindings (6) ===
            AddBinding(m_PanelOpen = new ValueBinding<bool>(kGroup, "PanelOpen", false));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetPanelOpen", v => m_PanelOpen.Update(v)));

            AddBinding(m_ConfigOpen = new ValueBinding<bool>(kGroup, "ConfigOpen", false));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetConfigOpen", v => m_ConfigOpen.Update(v)));

            // --- Dashboard 折叠状态（Phase 2） ---
            AddBinding(m_DashboardOpen = new ValueBinding<bool>(kGroup, "DashboardOpen", false));
            AddBinding(new TriggerBinding<bool>(kGroup, "SetDashboardOpen", v =>
            {
                m_DashboardOpen.Update(v);
                // 联动控制 Q2 系统的启停
                if (m_Q2System != null)
                    m_Q2System.Enabled = v;
            }));

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

            // === 扩展租金公式参数 Bindings (Phase 2: 12) ===
            // --- 地价贡献系数 ---
            AddBinding(m_LvFactorRes = new ValueBinding<int>(kGroup, "LvFactorRes", s.LandValueFactorResidential));
            AddBinding(new TriggerBinding<int>(kGroup, "SetLvFactorRes", v =>
            {
                Mod.Instance.Settings.LandValueFactorResidential = v;
                m_LvFactorRes.Update(v);
            }));

            AddBinding(m_LvFactorCom = new ValueBinding<int>(kGroup, "LvFactorCom", s.LandValueFactorCommercial));
            AddBinding(new TriggerBinding<int>(kGroup, "SetLvFactorCom", v =>
            {
                Mod.Instance.Settings.LandValueFactorCommercial = v;
                m_LvFactorCom.Update(v);
            }));

            AddBinding(m_LvFactorInd = new ValueBinding<int>(kGroup, "LvFactorInd", s.LandValueFactorIndustrial));
            AddBinding(new TriggerBinding<int>(kGroup, "SetLvFactorInd", v =>
            {
                Mod.Instance.Settings.LandValueFactorIndustrial = v;
                m_LvFactorInd.Update(v);
            }));

            // --- 等级贡献系数 ---
            AddBinding(m_LevelFactorRes = new ValueBinding<int>(kGroup, "LevelFactorRes", s.LevelFactorResidential));
            AddBinding(new TriggerBinding<int>(kGroup, "SetLevelFactorRes", v =>
            {
                Mod.Instance.Settings.LevelFactorResidential = v;
                m_LevelFactorRes.Update(v);
            }));

            AddBinding(m_LevelFactorCom = new ValueBinding<int>(kGroup, "LevelFactorCom", s.LevelFactorCommercial));
            AddBinding(new TriggerBinding<int>(kGroup, "SetLevelFactorCom", v =>
            {
                Mod.Instance.Settings.LevelFactorCommercial = v;
                m_LevelFactorCom.Update(v);
            }));

            AddBinding(m_LevelFactorInd = new ValueBinding<int>(kGroup, "LevelFactorInd", s.LevelFactorIndustrial));
            AddBinding(new TriggerBinding<int>(kGroup, "SetLevelFactorInd", v =>
            {
                Mod.Instance.Settings.LevelFactorIndustrial = v;
                m_LevelFactorInd.Update(v);
            }));

            // === UI 外观参数 (4: 2 value + 2 trigger) ===
            // --- 面板宽度持久化 ---
            AddBinding(m_UIMenuWidth = new ValueBinding<int>(kGroup, "UIMenuWidth", s.UIMenuPanelWidth));
            AddBinding(new TriggerBinding<int>(kGroup, "SetUIMenuWidth", v =>
            {
                Mod.Instance.Settings.UIMenuPanelWidth = v;
                m_UIMenuWidth.Update(v);
            }));

            AddBinding(m_UIDetailWidth = new ValueBinding<int>(kGroup, "UIDetailWidth", s.UIDetailPanelWidth));
            AddBinding(new TriggerBinding<int>(kGroup, "SetUIDetailWidth", v =>
            {
                Mod.Instance.Settings.UIDetailPanelWidth = v;
                m_UIDetailWidth.Update(v);
            }));

            AddBinding(m_UIPanelHeight = new ValueBinding<int>(kGroup, "UIPanelHeight", s.UIPanelHeight));
            AddBinding(new TriggerBinding<int>(kGroup, "SetUIPanelHeight", v =>
            {
                Mod.Instance.Settings.UIPanelHeight = v;
                m_UIPanelHeight.Update(v);
            }));

            // --- Dashboard 默认展开区块（只读 GetterValueBinding，从 Settings 读取） ---
            AddUpdateBinding(new GetterValueBinding<bool>(kGroup, "DashDefaultCityStats",
                () => Mod.Instance?.Settings?.DashboardDefaultCityStats ?? true));
            AddUpdateBinding(new GetterValueBinding<bool>(kGroup, "DashDefaultResidential",
                () => Mod.Instance?.Settings?.DashboardDefaultResidential ?? true));
            AddUpdateBinding(new GetterValueBinding<bool>(kGroup, "DashDefaultCommercial",
                () => Mod.Instance?.Settings?.DashboardDefaultCommercial ?? true));
            AddUpdateBinding(new GetterValueBinding<bool>(kGroup, "DashDefaultActivity",
                () => Mod.Instance?.Settings?.DashboardDefaultActivity ?? true));
            AddUpdateBinding(new GetterValueBinding<bool>(kGroup, "DashDefaultMisc",
                () => Mod.Instance?.Settings?.DashboardDefaultMisc ?? true));

            // === Dashboard 只读指标 (Phase 2: 8 GetterValueBinding) ===
            // 注意：Q2 系统在 SystemReplacer.Apply() 中注册，此时可能尚未创建
            // m_Q2System 通过 OnUpdate 懒加载获取，GetterValueBinding lambda 每次读取字段当前值

            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "TotalHouseholds",
                () => m_Q2System?.TotalHouseholds ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "RentedHouseholds",
                () => m_Q2System?.RentedHouseholds ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "HomelessCount",
                () => m_Q2System?.HomelessCount ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "MovingAwayCount",
                () => m_Q2System?.MovingAwayCount ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "SeekerHousedCount",
                () => m_Q2System?.SeekerHousedCount ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "SeekerHomelessCount",
                () => m_Q2System?.SeekerHomelessCount ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "HighRentBuildingCount",
                () => m_Q2System?.HighRentBuildingCount ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "PetCount",
                () => m_Q2System?.PetCount ?? 0));

            // === Dashboard Phase 4 扩展指标 (13 GetterValueBinding) ===
            // --- 住宅空置率（从 CountResidentialPropertySystem 缓存读取） ---
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "FreeResLow",
                () => m_Q2System?.FreeResLow ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "FreeResMed",
                () => m_Q2System?.FreeResMed ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "FreeResHigh",
                () => m_Q2System?.FreeResHigh ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "TotalResLow",
                () => m_Q2System?.TotalResLow ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "TotalResMed",
                () => m_Q2System?.TotalResMed ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "TotalResHigh",
                () => m_Q2System?.TotalResHigh ?? 0));

            // --- 商业活动（从 CountCompanyDataSystem 缓存读取） ---
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "TotalCommercial",
                () => m_Q2System?.TotalCommercial ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "CommercialPropertyless",
                () => m_Q2System?.CommercialPropertyless ?? 0));

            // --- 人口活动（从 ResidentPurposeCounterSystem 缓存读取） ---
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "ShoppingCount",
                () => m_Q2System?.ShoppingCount ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "LeisureCount",
                () => m_Q2System?.LeisureCount ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "GoingToWorkCount",
                () => m_Q2System?.GoingToWorkCount ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "GoingHomeCount",
                () => m_Q2System?.GoingHomeCount ?? 0));

            // --- 通勤者 ---
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "CommuterCount",
                () => m_Q2System?.CommuterCount ?? 0));

            // === 重置按钮 Triggers (2) ===
            AddBinding(new TriggerBinding(kGroup, "ResetRentControl", () =>
            {
                var settings = Mod.Instance.Settings;
                // Phase 1 参数
                settings.RentMultiplierResidential = 100;
                settings.RentMultiplierCommercial = 100;
                settings.RentMultiplierIndustrial = 100;
                settings.LandValueEnvironmentEffect = 40;
                settings.ServiceBonusCapMultiplier = 100;
                // Phase 2 扩展参数
                settings.LandValueFactorResidential = 100;
                settings.LandValueFactorCommercial = 100;
                settings.LandValueFactorIndustrial = 100;
                settings.LevelFactorResidential = 100;
                settings.LevelFactorCommercial = 100;
                settings.LevelFactorIndustrial = 100;

                // 同步 ValueBindings — Phase 1
                m_RentMultRes.Update(100);
                m_RentMultCom.Update(100);
                m_RentMultInd.Update(100);
                m_LandValueEnvEffect.Update(40);
                m_ServiceBonusCap.Update(100);
                // 同步 ValueBindings — Phase 2
                m_LvFactorRes.Update(100);
                m_LvFactorCom.Update(100);
                m_LvFactorInd.Update(100);
                m_LevelFactorRes.Update(100);
                m_LevelFactorCom.Update(100);
                m_LevelFactorInd.Update(100);
            }));

            AddBinding(new TriggerBinding(kGroup, "ResetPathfinding", () =>
            {
                var settings = Mod.Instance.Settings;
                settings.ShoppingMaxCost = 8000f;
                settings.LeisureMaxCost = 12000f;

                m_ShoppingMaxCost.Update(8000f);
                m_LeisureMaxCost.Update(12000f);
            }));

            ModLog.Ok(Tag, "MapExtUISystem 已创建 (Phase 4: 60 Bindings)");
        }

        #endregion

        #region System Loop

        protected override void OnUpdate()
        {
            // === Q2 系统懒加载（OnCreate 时 Q2 尚未注册） ===
            if (m_Q2System == null)
                m_Q2System = World.GetExistingSystemManaged<Q2_CityStatsSystem>();

            // GetterValueBinding 的自动更新由 base.OnUpdate() 处理
            base.OnUpdate();

            // === UI 外观参数脏检查（无论面板是否打开都必须同步） ===
            var s = Mod.Instance.Settings;

            if (m_UIMenuWidth.value != s.UIMenuPanelWidth)
                m_UIMenuWidth.Update(s.UIMenuPanelWidth);

            if (m_UIDetailWidth.value != s.UIDetailPanelWidth)
                m_UIDetailWidth.Update(s.UIDetailPanelWidth);

            if (m_UIPanelHeight.value != s.UIPanelHeight)
                m_UIPanelHeight.Update(s.UIPanelHeight);

            // 面板关闭时跳过调参脏检查（零开销）
            if (!m_PanelOpen.value) return;

            // === 反向同步：检测 Options UI 是否修改了值 ===

            // --- Phase 1 租金参数 ---
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

            // --- Phase 1 寻路参数 ---
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (m_ShoppingMaxCost.value != s.ShoppingMaxCost)
                m_ShoppingMaxCost.Update(s.ShoppingMaxCost);

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (m_LeisureMaxCost.value != s.LeisureMaxCost)
                m_LeisureMaxCost.Update(s.LeisureMaxCost);

            // --- Phase 2 扩展租金参数 ---
            if (m_LvFactorRes.value != s.LandValueFactorResidential)
                m_LvFactorRes.Update(s.LandValueFactorResidential);

            if (m_LvFactorCom.value != s.LandValueFactorCommercial)
                m_LvFactorCom.Update(s.LandValueFactorCommercial);

            if (m_LvFactorInd.value != s.LandValueFactorIndustrial)
                m_LvFactorInd.Update(s.LandValueFactorIndustrial);

            if (m_LevelFactorRes.value != s.LevelFactorResidential)
                m_LevelFactorRes.Update(s.LevelFactorResidential);

            if (m_LevelFactorCom.value != s.LevelFactorCommercial)
                m_LevelFactorCom.Update(s.LevelFactorCommercial);

            if (m_LevelFactorInd.value != s.LevelFactorIndustrial)
                m_LevelFactorInd.Update(s.LevelFactorIndustrial);
        }

        #endregion
    }
}
