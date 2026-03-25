using Colossal;
using System.Collections.Generic;

namespace EconomyEX.Settings
{
    public class LocaleHANS : IDictionarySource
    {
        private readonly ModSettings m_Setting;

        public LocaleHANS(ModSettings setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "EconomyEX 经济扩展" },
                
                { m_Setting.GetOptionTabLocaleID(ModSettings.kSectionStatus), "运行状态" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionStatus), "运行状态" },
                
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.StatusInfo)), "• 模块状态" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.StatusInfo)), "当前经济模块的工作状态。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ConflictWarning)), "• 冲突警告" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ConflictWarning)), "检测到的可能导致错误的冲突信息。" },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kSectionGeneral), "通用设置" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionGeneral), "通用设置" },
                
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableEconomyFix)), "• 启用经济修复与性能优化 (总开关)" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableEconomyFix)), "【此补丁目前处于测试阶段 (Beta)】\n优化并修复以下系统，以适配大城市经济和性能问题。\n\n⚠️ 【重要】：更改此项设置后，【必须重启游戏】，否则不会生效并且会引发不可预知的 Bug！" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableDemandEcoSystem)), "  ├─ RCI需求调节系统组" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableDemandEcoSystem)), "优化居住、商业、工业需求计算模型，使之更平滑合理。\n\n⚠️ 修改后需重启游戏生效。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)), "  ├─ 找工作系统组" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)), "优化市民找工作行为与匹配算法，提升就业匹配效率。\n\n⚠️ 与 Realistic JobSearch 等 Mod 不兼容！\n⚠️ 修改后需重启游戏生效。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)), "  ├─ 找房与租金系统组" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)), "优化家庭找房寻路计算；包含真实地价和租金计算（Land Value）重构，使之更加合理。\n\n⚠️ 修改后需重启游戏生效。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)), "  ├─ 消费采购与服务覆盖寻路系统组" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)), "优化市民购物与企业采购的资源匹配，并优化服务覆盖寻路，大幅降低超远路程规划产生的性能开销。\n\n⚠️ 与 Realistic PathFinding 等寻路 Mod 不兼容！\n⚠️ 修改后需重启游戏生效。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)), "  └─ 居民AI寻路优化补丁" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)), "修复市民寻路AI等待时间的逻辑缺陷，缓解寻路内存溢出的问题。\n\n⚠️ 与 Realistic PathFinding 等寻路 Mod 不兼容！\n⚠️ 修改后需重启游戏生效。" },

                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionPathfinding), "▍寻路优化参数(可于游戏中实时调节)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShoppingMaxCost)), "购物最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShoppingMaxCost)),
                    "控制市民为了买东西（如杂货、餐饮）愿意承受的最大出行成本。数值越低，市民在找不到商店时放弃得越快，能大幅降低CPU负担。\n" +
                    "★ 建议值：8000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.CompanyShoppingMaxCost)), "公司货运最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.CompanyShoppingMaxCost)),
                    "控制公司（工厂/商店）寻找材料并呼叫货车送货的最大搜索范围。公司补货通常不局限于本地，较高数值可允许公司在全图范围内寻找资源。\n" +
                    "★ 建议值：200000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LeisureMaxCost)), "休闲观光最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LeisureMaxCost)),
                    "控制市民为了游览公园、地标或观光愿意承受的最大出行成本。数值越低，全图无目的闲逛引发的寻路计算越少。\n" +
                    "★ 建议值：8000 ~ 12000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindJobMaxCost)), "找工作最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindJobMaxCost)),
                    "控制市民为了寻求工作岗位，最多愿意跨越多大规模的地图。该行为频率极低，建议直接拉满（对性能影响不明显）。\n" +
                    "★ 建议值：200000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindHomeMaxCost)), "找房搬家最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindHomeMaxCost)),
                    "控制市民搬家找房时的最大搜索范围上限。该行为频率较低，建议直接拉满。\n" +
                    "★ 建议值：200000"
                },
            };
        }

        public void Unload() { }
    }
}
