// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// --- LOCALE ---

using Colossal;
using System.Collections.Generic;

namespace MapExtPDX
{
    public class LocaleHANS : IDictionarySource
    {
        private readonly ModSettings m_Setting;

        public LocaleHANS(ModSettings setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            var entries = new Dictionary<string, string>
            {
                // ============================================================
                // Mod 标题
                // ============================================================
                { m_Setting.GetSettingsLocaleID(), "#大地图" },

                // ============================================================
                // Tab 1: 地图尺寸
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kMapSizeModeTab), "地图尺寸" },

                // --- Group: 地图尺寸模式 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kMainModeGroup), "地图尺寸模式" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.PatchModeChoice)), "► 选择地图尺寸模式" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.PatchModeChoice)),
                    "⚠️ 改变模式后必须点击「应用设置」按钮生效！\n\n模式详情:\n - ModeA: 57km (4x4) DEM:14m\n - ModeB: 28km (2x2) DEM:7m\n - ModeC: 114km (8x8) DEM:28m\n - 纯净模式: 14km 原版(1x1) DEM:3.5m\n\n注意:\n1. 随着地图尺寸的增加，DEM地形分辨率会相应降低，导致部分山地、水岸与坡道显得粗糙。如果对地形平滑度要求较高，建议使用较为平坦的地图或使用模组工具进行修饰。\n2. 由于游戏底层的浮点精度限制，在地图边缘区域可能会出现模拟数据计算偏差（产生虚假的视觉效果），使用 114km 模式时尤为明显。建议将城市活动中心（住/商/工）尽量建设在地图中心区域。\n\n⚠️ 【重要警告】：在更改地图尺寸模式后，【必须重启游戏】才能安全加载存档，否则可能导致坏档！"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ModSettingCoreValue)),
                    "• 当前已应用地图尺寸"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ModSettingCoreValue)),
                    "当前已选择并成功应用的地图尺寸。该尺寸指地图边长。单位为米。\n⚠️ 注意: 虽然本mod具有存档验证以防错误加载不同尺寸地图存档，但仍然强烈建议在使用本Mod加载游戏存档前，请备份好您的所有游戏存档(推荐Skyve)，以防游戏崩溃或各种奇特问题坏档！大地图制作不易，且行且珍惜。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ApplyPatchChanges)), "► 应用设置" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ApplyPatchChanges)),
                    "点击以应用所选的地图尺寸模式。\n\n⚠️ 【重要】：本 Mod 核心逻辑不支持热切换。在应用新设置后，【必须重启游戏】才能安全读取存档，否则系统逻辑将会错乱并导致坏档风险！"
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ApplyPatchChanges)),
                    "正在应用地图尺寸模式，请耐心等待完成。\n\n⚠️ 完成后，请务必【重启游戏】以确保设置完全生效，切勿直接读取存档！"
                },

                // --- Group: 地形-水体优化 (Beta) ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kTerrainWaterOptGroup), "地形-水体性能优化 (Beta)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.TerrainBufferPrealloc)), "地形缓冲预分配" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.TerrainBufferPrealloc)),
                    "根据地图倍率在首帧预分配更大的 GPU StructuredBuffer，" +
                    "避免大量建筑/道路可见时运行时动态扩容卡顿。\n\n" +
                    "★ 建议：大地图全部开启，无视觉副作用。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.TerrainCascadeThrottle)), "⚠ 地形级联降频 (实验性)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.TerrainCascadeThrottle)),
                    "通过将远距地形级联层每4帧更新一次（而非每帧）来降低 GPU 负载。\n\n" +
                    "⚠ 警告：移动相机时可能出现地形偏移/错位，" +
                    "因为级联视口范围每帧更新但渲染被降频。\n\n" +
                    "★ 建议：除非在超大地图上遇到严重 GPU 瓶颈，否则保持关闭。"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.WaterSimQuality)),
                    "► 水体模拟质量"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.WaterSimQuality)),
                    "控制水系统 CPU 和 GPU 计算的更新频率，以提升大地图的帧率和游戏速度。\n\n" +
                    " - Vanilla: 原版高精度：每帧调度计算，效果最好，消耗最大。\n" +
                    " - Reduced: 降低消耗：每帧计算并传播水体，但关闭背景大地图边缘的水流计算。\n" +
                    " - Minimal: 极简性能：每四帧跳过计算并显式关闭水面模糊和后处理效果，将大幅度降低 GPU 请求频率，可能会有极不明显的水流卡顿。\n" +
                    " - Paused: 暂停流体：完全冻结水体流动计算（水面将静止但水位不会出现大变化）。\n\n" +
                    "★ 提示：该选项即时生效无须重启。"
                },

                // (隐藏项 - 保留用于序列化)
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.TerrainResolution)), "地形分辨率" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.TerrainResolution)),
                    "新建地图的地形高度图分辨率。8192 提供更精细的地形编辑和渲染（使用地形笔刷时尤为明显）。" +
                    "已有存档将保持其原始分辨率。\n" +
                    "⚠️ 修改后需重启游戏。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.WaterResolution)), "水体模拟分辨率" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.WaterResolution)),
                    "水体模拟纹理大小。较低的值可大幅降低GPU/显存占用，视觉影响极小。" +
                    "大地图建议使用 512 或 256。\n" +
                    "⚠️ 修改后加载旧存档时水面将重置（河流和湖泊会从水源重新注水）。\n" +
                    "⚠️ 修改后需重启游戏。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.VRAMEstimate)), "地形/水体预估显存占用" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.VRAMEstimate)),
                    "当前分辨率设置下地形级联纹理和水体模拟纹理的大致GPU显存占用量。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.WaterTextureFormat)), "水模拟贴图精度 (VRAM 优化)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.WaterTextureFormat)),
                    "将原本 32 浮点的模拟通道数据强制压缩到 16 浮点，省去高达 43% 的显存占用并将理论上的宽带开销减半，极大提升 GPU 模拟性能限制。\n\n" +
                    " - 原版 HDR (32-bit)：精度高无损，消耗约 180MB 显存。 \n" +
                    " - 性能模式 (16-bit)：精度有损，消耗约 105MB 显存。在水深大于 100 米时边缘可能会因截断出现计算波纹（通常很少见）。\n\n" +
                    "⚠️ 修改后需【重启游戏】或重新读取存档生效。"
                },

                // --- Group: 经济系统修复 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kEcoGroup), "经济系统修复" },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.isEnableEconomyFix)),
                    "• 经济系统修复与性能优化 (Beta 总开关)"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.isEnableEconomyFix)),
                    "【此补丁目前处于测试阶段 (Beta)】\n优化并修复以下系统，以适配百万人口规模的巨型城市：\n - 住宅/商业/工业需求系统\n - 家庭找房系统\n - 家庭行为系统 (消费行为修正)\n - 市民寻找工作系统\n - 租金计算系统\n - 资源采购与服务覆盖寻路系统\n - 居民AI寻路优化补丁\n\n⚠️ 【重要】：更改此项设置后，【必须重启游戏】，否则不会生效并且会引发不可预知的 Bug！"
                },

                // --- Group: 警告 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kNoteGroup), "警告" },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ModeChangeWarningMessage)),
                    "⚠️ 更改上述【任一】选项后，请务必【重启游戏】再读取存档！"
                },

                // ============================================================
                // Tab 2: EconomyEX
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kMiscTab), "EconomyEX" },

                // --- Group: 经济子系统开关 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kEcoSystemEnableGroup), "经济子系统开关" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableDemandEcoSystem)), "├─ RCI需求调节系统组" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableDemandEcoSystem)),
                    "优化居住、商业、工业需求计算模型，使之更平滑合理，并匹配百万人口规模的巨型城市。\n\n⚠️ 修改后需重启游戏生效。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)), "├─ 找工作系统组" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)),
                    "优化市民找工作行为与匹配算法，提升就业匹配效率。\n\n⚠️ 与 Realistic JobSearch 等 Mod 不兼容！\n⚠️ 修改后需重启游戏生效。"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)),
                    "├─ 找房与租金系统组"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)),
                    "优化家庭找房寻路计算；包含真实地价和租金计算（Land Value）重构，使之更加合理。\n\n⚠️ 修改后需重启游戏生效。"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)),
                    "├─ 消费采购与服务覆盖寻路系统组"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)),
                    "优化市民购物与企业采购的资源匹配，并优化服务覆盖寻路，大幅降低超远路程规划产生的性能开销。\n\n⚠️ 与 Realistic PathFinding 等寻路 Mod 不兼容！\n⚠️ 修改后需重启游戏生效。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)), "└─ 居民AI寻路优化补丁" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)),
                    "修复市民寻路AI等待时间的逻辑缺陷，缓解大地图底层寻路内存溢出的问题。\n\n⚠️ 与 Realistic PathFinding 等寻路 Mod 不兼容！\n⚠️ 修改后需重启游戏生效。"
                },

                // --- Group: 寻路成本上限 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kPathfindingGroup), "寻路成本上限 (可于游戏中实时调节)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShoppingMaxCost)), "购物最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShoppingMaxCost)),
                    "控制市民为了买东西（如杂货、餐饮）愿意承受的最大出行成本。数值越低，市民在找不到商店时放弃得越快，能大幅降低大地图下的CPU负担。\n" +
                    "★ 建议值：\n" +
                    " - 14km / 28km： 8000\n" +
                    " - 57km / 114km：8000 ~ 12000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.CompanyShoppingMaxCost)), "公司货运最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.CompanyShoppingMaxCost)),
                    "控制公司（工厂/商店）寻找材料并呼叫货车送货的最大搜索范围。由于公司补货通常不局限于本地，极高数值（最大20万）可允许公司在全图范围内寻找资源，防止在大地图中出现材料荒。\n" +
                    "★ 建议值：\n" +
                    " - 全地图通用：200000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LeisureMaxCost)), "休闲观光最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LeisureMaxCost)),
                    "控制市民为了游览公园、地标或观光愿意承受的最大出行成本。数值越低，全图无目的闲逛引发的寻路计算越少。\n" +
                    "★ 建议值：\n" +
                    " - 14km / 28km： 8000 ~ 12000\n" +
                    " - 57km / 114km：12000 ~ 20000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EmergencyMaxCost)), "医院/犯罪最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EmergencyMaxCost)),
                    "控制市民生病就医或犯罪时的最大搜索范围。较低的值将这些行为限制在就近区域，鼓励本地化的公共服务规划。\n" +
                    "★ 提示：建议在此成本范围内合理布局医院与警局。若相关设施非常密集，可进一步降低此值。\n" +
                    "★ 建议值：\n" +
                    " - 全地图通用：4000 ~ 8000（默认：6000）"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindJobMaxCost)), "找工作最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindJobMaxCost)),
                    "控制市民为了寻求工作岗位，最多愿意跨越多大规模的地图。数值越高（最大20万），大地图远郊孤岛小镇越容易招到工人。该行为频率极低，建议直接拉满（对性能影响不明显）。\n" +
                    "★ 建议值：\n" +
                    " - 全地图通用：200000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindHomeMaxCost)), "找房搬家最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindHomeMaxCost)),
                    "控制市民搬家找房时的最大搜索范围上限。提升此数值可让市民跨越整张特大地图寻找新住宅，避免偏远新城无人入住。该行为频率较低，建议直接拉满。\n" +
                    "★ 建议值：\n" +
                    " - 全地图通用：200000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolElementaryMaxCost)), "找小学最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolElementaryMaxCost)),
                    "控制小学生寻找学校愿意走的最远路线开销。较小的值能强迫小学生只能就近入学。\n" +
                    "★ 建议值：\n" +
                    " - 全地图通用：10000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolHighSchoolMaxCost)), "找高中最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolHighSchoolMaxCost)),
                    "控制中学生寻找高中能够跨越的最大路线开销。\n" +
                    "★ 建议值：\n" +
                    " - 全地图通用：17000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolCollegeMaxCost)), "找学院最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolCollegeMaxCost)),
                    "控制寻找学院(大专)级别的最大范围。\n" +
                    "★ 建议值：\n" +
                    " - 全地图通用：50000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolUniversityMaxCost)), "找大学最高寻路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolUniversityMaxCost)),
                    "控制寻找大学的最大范围。如果是全图唯一的大学城城邦，建议拉满以覆盖全图每个角落。\n" +
                    "★ 建议值：\n" +
                    " - 全地图通用：100000 ~ 200000"
                },

                // --- Group: 经济行为与吞吐量 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kEcoBehaviorGroup), "经济行为与吞吐量 (可于游戏中实时调节)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.JobSeekerCap)), "找工作系统：求职吞吐量" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.JobSeekerCap)),
                    "每次系统更新最多创建的求职者数量。城市人口越大可适当提高。\n" +
                    "较高的值加快就业匹配速度，但增加 CPU 负担。可于游戏中实时调节。\n" +
                    "★ 建议值：\n" +
                    " - 50万以下人口：200 ~ 500\n" +
                    " - 200万人口：500 ~ 1000\n" +
                    " - 500万以上人口：1000 ~ 3000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.PathfindRequestCap)), "找工作系统：寻路吞吐量" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.PathfindRequestCap)),
                    "每次寻路更新最多处理的求职寻路请求数量。通常为求职吞吐量的 2~4 倍。\n" +
                    "较高的值加快岗位匹配，但增加寻路系统的 CPU 负担。可于游戏中实时调节。\n" +
                    "★ 建议值：\n" +
                    " - 50万以下人口：1000 ~ 2000\n" +
                    " - 200万人口：2000 ~ 4000\n" +
                    " - 500万以上人口：4000 ~ 8000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShoppingTrafficReduction)), "购物概率人口压制系数" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShoppingTrafficReduction)),
                    "控制城市人口对家庭购物概率的衰减影响。公式：shopChance = 200 / sqrt(系数 × 人口)。" +
                    "数值越大，高人口时购物概率越低，商业区交易量越少。\n\n" +
                    "★ 不同人口规模的效果对照：（以默认值 0.0004 为例）\n" +
                    " - 1万人口：shopChance ≈ 100% → 几乎每户都购物\n" +
                    " - 10万人口：shopChance ≈ 32% → 三分之一家庭购物\n" +
                    " - 100万人口：shopChance ≈ 10% → 十分之一家庭购物\n" +
                    " - 500万人口：shopChance ≈ 4% → 极少数家庭购物\n\n" +
                    "★ 按人口规模调节建议：\n" +
                    " - 10万以下小城市：0.0004（默认，与原版一致）\n" +
                    " - 10万~50万中型城市：0.0003 ~ 0.0004\n" +
                    " - 50万~200万大型城市：0.0002 ~ 0.0003\n" +
                    " - 200万以上超大城市：0.0001 ~ 0.0002\n\n" +
                    "可于游戏中实时调节。降低此值可鼓励消费、提升商业区收入。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HouseholdResourceDemandMultiplier)), "家庭购物需求倍率" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HouseholdResourceDemandMultiplier)),
                    "每次家庭购物时的资源购买量倍率。由于大地图优化降低了购物频率（tick频率为原版1/2），" +
                    "需要增大单次购买量来补偿，保持经济系统总消费量平衡。\n\n" +
                    "★ 倍率与实际效果：\n" +
                    " - 1.0：与原版购买量一致（但频率降低，总消费量不足）\n" +
                    " - 3.5：补偿后约为原版总消费量的 70%~88%（默认值）\n" +
                    " - 5.0：接近完全补偿原版消费水平\n" +
                    " - 8.0：超额补偿，适合极大地图 + 极低购物概率\n\n" +
                    "★ 按地图/人口规模调节建议：\n" +
                    " - 14km原版地图：1.0 ~ 2.0\n" +
                    " - 28km (ModeB)：2.0 ~ 3.5\n" +
                    " - 57km (ModeA)：3.5 ~ 5.0（默认3.5）\n" +
                    " - 114km (ModeC)：5.0 ~ 8.0\n\n" +
                    "★ 观察指标：如果商业区大量空置/倒闭，请提高此值。" +
                    "如果商品供不应求（工业产品被秒光），请降低此值。\n" +
                    "可于游戏中实时调节。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HomeSeekerCap)), "找房系统：搬家吞吐量" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HomeSeekerCap)),
                    "每帧最多处理的【已有住房但想搬家】的家庭数量。此参数控制系统每次更新(16帧/次)中评估搬家请求的速率。" +
                    "数值越大，搬家匹配越快，但单帧CPU开销越高（FindPropertyJob 为单线程）。\n\n" +
                    "★ 调节建议：\n" +
                    " - 50万以下人口：64 ~ 128（默认）\n" +
                    " - 200万人口：128 ~ 256\n" +
                    " - 500万以上人口：256 ~ 512\n\n" +
                    "★ 如何判断：若城市中大量家庭不主动搬迁（明明有更好住房），说明吞吐量不足，请提高此值。" +
                    "若游戏出现顿卡，请降低此值。可于游戏中实时调节。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HomelessSeekerCap)), "找房系统：流浪安置吞吐量" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HomelessSeekerCap)),
                    "每帧最多处理的【无家可归】家庭找房数量。流浪家庭的找房优先级高于搬家家庭。" +
                    "此参数决定无家可归者被安置的速度。\n\n" +
                    "★ 调节建议：\n" +
                    " - 50万以下人口：640 ~ 1280（默认）\n" +
                    " - 200万人口：1280 ~ 2560\n" +
                    " - 500万以上人口：2560 ~ 5120\n\n" +
                    "★ 如何判断：若城市中大量流浪汉长期无法入住空置住宅，请提高此值。" +
                    "若大量流浪同时涌入导致帧率骤降，请降低此值。可于游戏中实时调节。"
                },

                // ============================================================
                // Tab 3: 性能工具
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kPerformanceToolTab), "性能工具" },

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
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshPetCount)), "点击以重新计算地图上的活动宠物实体数量。这只是一个统计，对游戏状态无任何影响。" },

                // --- Group: 过境交通控制 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kNoTrafficGroup), "过境交通控制" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoThroughTraffic)), "禁止过境交通" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoThroughTraffic)),
                    "禁止所有过境交通工具出现，降低寻路计算量和交通拥堵. (可能需要运行一段时间生效)"
                },

                // ============================================================
                // Tab 4: 开发者选项
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kDebugTab), "开发者选项" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kDebugGroup), "开发者选项" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DisableLoadGameValidation)), "× 禁止游戏读取存档验证" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DisableLoadGameValidation)),
                    "⚠️ 警告！默认(不勾选)为启用游戏读取存档验证，以防止错误设置地图尺寸模式而读取不同尺寸的存档造成坏档！\n该选项勾选后将取消验证，仅用于使用旧版MapExt mod特殊尺寸模式而无法正确识别的情况。使用旧版存档请务必确认'地图尺寸模式'是否设置正确，否则可能坏档！\n务必在使用该功能前备份您的存档"
                },
            };
            return entries;
        }

        public void Unload()
        {
        }
    }
}