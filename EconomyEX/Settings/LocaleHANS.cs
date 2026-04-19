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

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.SystemStatusReport)), "• 系统状态" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.SystemStatusReport)), "经济系统替换对的实时状态报告。" },

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
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)), "优化市民购物与企业采购的资源匹配，并优化服务覆盖寻路，大幅降低超远路程规划产生的性能开销。\n\n⚠️ 与 Realistic PathFinding 等寻路 Mod 不兼容！\n⚠️ 修改后需重启游戏生效。\n\n⚠️ 默认关闭，以避免与流行寻路Mod冲突。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)), "  └─ 居民AI寻路优化补丁" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)), "修复市民寻路AI等待时间的逻辑缺陷，缓解寻路内存溢出的问题。\n\n⚠️ 与 Realistic PathFinding 等寻路 Mod 不兼容！\n⚠️ 修改后需重启游戏生效。\n\n⚠️ 默认关闭，以避免与流行寻路Mod冲突。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetEcoSystemToggles)), "重置" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ResetEcoSystemToggles)), "将所有经济子系统开关恢复为默认值。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetEcoSystemToggles)), "确认要将所有经济子系统开关重置为默认值吗？" },

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
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EmergencyMaxCost)), "医院/犯罪最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EmergencyMaxCost)),
                    "控制市民生病就医或犯罪时的最大搜索范围。较低的值将这些行为限制在就近区域，鼓励本地化的公共服务规划。\n" +
                    "★ 提示：建议在此成本范围内合理布局医院与警局。若相关设施非常密集，可进一步降低此值。\n" +
                    "★ 建议值：4000 ~ 8000（默认：6000）"
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

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetPathfinding)), "重置" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ResetPathfinding)), "将所有寻路成本上限参数恢复为默认值。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetPathfinding)), "确认要将所有寻路参数重置为默认值吗？" },

                // --- 经济行为参数 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionBehavior), "经济行为与吞吐量 (可于游戏中实时调节)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.JobSeekerCap)), "找工作系统：求职吞吐量" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.JobSeekerCap)), "每次系统更新最多创建的求职者数量。城市人口越大可适当提高。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.PathfindRequestCap)), "找工作系统：寻路吞吐量" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.PathfindRequestCap)), "每次寻路更新最多处理的求职寻路请求数量。通常为求职吞吐量的 2~4 倍。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShoppingTrafficReduction)), "购物概率人口压制系数 (x0.0001)" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShoppingTrafficReduction)), "控制城市人口对家庭购物概率的衰减影响。数值越大，高人口时购物概率越低。默认：4（=0.0004）。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HouseholdResourceDemandMultiplier)), "家庭购物需求倍率" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HouseholdResourceDemandMultiplier)), "每次家庭购物时的资源购买量倍率。默认3.5。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HomeSeekerCap)), "找房系统：搬家吞吐量" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HomeSeekerCap)), "每帧最多处理的已有住房但想搬家的家庭数量。默认128。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HomelessSeekerCap)), "找房系统：流浪安置吞吐量" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HomelessSeekerCap)), "每帧最多处理的无家可归家庭找房数量。默认1280。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetEcoBehavior)), "重置" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ResetEcoBehavior)), "将所有经济行为与吞吐量参数恢复为默认值。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetEcoBehavior)), "确认要将所有经济行为参数重置为默认值吗？" },

                // ============================================================
                // Tab: 性能工具
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kSectionPerfTool), "性能工具" },

                // --- Group: NoDogs ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kNoDogsGroup), "宠物控制 (NoDogs)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoDogsOnStreet)), "NoDogs: 禁止外出" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoDogsOnStreet)),
                    "禁止宠物外出上街（关闭生成、渲染与寻路）。逻辑宠物实体仍存在于内存中。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoDogsGeneration)), "NoDogs: 阻止新生成" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoDogsGeneration)),
                    "将新家庭的宠物生成概率归零，阻止新移民携带宠物。已有宠物保留不变。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoDogsPurge)), "⚠ NoDogs: 清除所有存量" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoDogsPurge)),
                    "⚠ 警告：移除存档中所有已有宠物实体，最大化性能提升。清除后已有家庭不会再获得宠物，只有新搬入的家庭才会自带（若未阻止生成）。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ApplyNoDogs)), "► 应用 NoDogs 设置" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ApplyNoDogs)),
                    "点击后上方勾选才会生效。未点击此按钮前，勾选不会对游戏产生任何影响。"
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ApplyNoDogs)),
                    "确认应用 NoDogs 设置？若勾选了「清除所有存量」，所有宠物将被永久移除！"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DislayPetCount)), "当前逻辑宠物数" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DislayPetCount)), "地图上当前的逻辑宠物实体数量统计。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RefreshPetCount)), "刷新宠物统计" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshPetCount)),
                    "点击以重新计算地图上的活动宠物实体数量。这只是一个统计，对游戏状态无任何影响。"
                },

                // --- Group: 过境交通控制 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kNoTrafficGroup), "过境交通控制" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoThroughTraffic)), "禁止过境交通" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoThroughTraffic)),
                    "禁止所有过境交通工具出现，降低寻路计算量和交通拥堵. (可能需要运行一段时间生效)"
                },
            };
        }

        public void Unload() { }
    }
}
