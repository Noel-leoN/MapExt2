// locales.ts - MapExt 中英文本字典 (Phase 1 ~ Phase 5)
// 使用游戏引擎内置的 activeLocale binding 检测语言

import { bindValue, useValue } from "cs2/api";

// 游戏内置 binding：返回 "en-US", "zh-HANS" 等
const activeLocale$ = bindValue<string>("app", "activeLocale");

export const locales = {
    en: {
        // === Phase 1 ===
        rentRes: "Multiplier for Residential rent calculation. 100% = vanilla equivalent. Lower values reduce rent across all residential buildings.",
        rentCom: "Multiplier for Commercial rent calculation. 100% = vanilla equivalent. Lower values help commercial businesses survive in high-land-value areas.",
        rentInd: "Multiplier for Industrial rent calculation. 100% = vanilla equivalent. Lower values help factories survive.",
        envFactor: "Controls how much environmental factors (attractiveness, pollution) affect road land values. (Vanilla = 40%)",
        svcBonus: "Scales the upper limit of service coverage bonuses (healthcare, police, etc.) to land values.",
        shopMax: "Max travel cost citizens are willing to endure for shopping.\nRecommended: 14km/28km: 8000 | 57km/114km: 8000~12000",
        leisureMax: "Max travel cost citizens are willing to endure for leisure.\nRecommended: 14km/28km: 8000~12000 | 57km/114km: 12000~20000",
        reset: "Reset Defaults",
        panelTitle: "MapExt Dashboard",
        mapSizeLabel: "MapSize",
        rentControlTitle: "Rent Control",
        pathfindingTitle: "Pathfinding",

        // === Phase 2 — Dashboard ===
        dashboardTitle: "City Stats",
        totalHouseholds: "Households",
        rentedHouseholds: "Housed",
        homelessCount: "Homeless",
        movingAwayCount: "Moving Away",
        seekerHoused: "Seekers (Housed)",
        seekerHomeless: "Seekers (Homeless)",
        highRentBuildings: "High Rent Bldgs",
        petCount: "Pets",

        // === Phase 2 — 扩展租金参数 ===
        lvFactorRes: "Land value contribution to residential rent. 100% = full contribution. Lower values decouple rent from land price.",
        lvFactorCom: "Land value contribution to commercial rent. 100% = full contribution.",
        lvFactorInd: "Land value contribution to industrial rent. 100% = full contribution.",
        levelFactorRes: "Building level contribution to residential rent. Higher-level buildings charge more rent at 100%.",
        levelFactorCom: "Building level contribution to commercial rent. 100% = vanilla scaling.",
        levelFactorInd: "Building level contribution to industrial rent. 100% = vanilla scaling.",

        // === Phase 4 — Dashboard 扩展 ===
        residentialTitle: "Residential Market",
        commercialTitle: "Commercial Market",
        activityTitle: "Population Activity",
        resDensityLow: "Low",
        resDensityMed: "Medium",
        resDensityHigh: "High",
        resVacant: "Vacant",
        resTotal: "Total",
        resVacancyRate: "Vacancy",
        totalCommercial: "Active Shops",
        commercialPropertyless: "Seeking Property",
        shoppingCount: "Shopping",
        leisureCount: "Leisure",
        goingToWork: "Commuting",
        goingHome: "Returning Home",
        commuterCount: "Commuters",
        miscTitle: "Misc",

        // === Phase 5 — 水体工具 ===
        waterToolsTitle: "Water Tools",
        seaLevelLabel: "Sea Level",
        seaLevelTip: "Current sea level height. Adjust and click Apply to reset water surface to this height.",
        seaLevelPrecise: "Height",
        applySeaLevel: "Apply Sea Level",
        resetWater: "Reset Water",
        waterSimSpeedLabel: "Water Sim Speed",
        waterSimPaused: "Paused",
        waterSimSpeedHint: "Higher values speed up water generation but increase GPU load. Use 1x for normal play.",
        lockSeaLevel: "Lock Sea Level",
        lockSeaLevelTip: "Lock sea level to prevent water simulation from changing it. The locked value updates when you set a new sea level.",

        // === Tooltips ===
        totalHouseholdsTip: "Total number of household entities in the city (both housed and homeless).",
        rentedHouseholdsTip: "Percentage of households that have successfully rented or owned a home. High rate indicates stable housing.",
        homelessCountTip: "Number of households without a home. They may cluster in parks. If high, try reducing residential rents or building lower density housing.",
        movingAwayCountTip: "Number of households currently leaving the city due to high living costs or lack of jobs.",
        seekerHousedTip: "Housed households currently searching for a better or cheaper home to move into.",
        seekerHomelessTip: "Homeless households actively searching for any vacant residential properties in the city.",
        highRentBuildingsTip: "Number of buildings currently displaying the 'High Rent' warning icon. Players can reduce Residential Rent Multiplier to help.",
        resDensityLowTip: "Vacant units / Total units (Vacancy Rate) for Low Density Residential zoning.",
        resDensityMedTip: "Vacant units / Total units (Vacancy Rate) for Medium Density Residential zoning.",
        resDensityHighTip: "Vacant units / Total units (Vacancy Rate) for High Density Residential zoning.",
        totalCommercialTip: "Total number of commercial companies that currently occupy a building.",
        commercialPropertylessTip: "Commercial companies that have spawned but cannot find a vacant building. Build more commercial zones.",
        shoppingCountTip: "Citizens currently traveling to shop or actively shopping.",
        leisureCountTip: "Citizens currently seeking or enjoying leisure activities (parks, landmarks, etc.).",
        goingToWorkTip: "Citizens currently traveling to their workplace.",
        goingHomeTip: "Citizens currently returning back to their residential homes.",
        commuterCountTip: "Workers commuting into your city from outside connections (highway, train, etc.).",
        petCountTip: "Total number of household pets in the city.",
        applySeaLevelTip: "Forces all water nodes to instantly adjust to the specified Sea Level.",
        resetWaterTip: "Resets the water simulation completely to clear up glitched waves or flooding.",
        mapSizeTip: "Calculated playable map size based on the current active tiles.",
        statusTip: "Indicates whether MapExt systems are running normally.",
        rentControlMenuTip: "Open Rent Control details to adjust residential, commercial and industrial rent multipliers.",
        pathfindingMenuTip: "Open Pathfinding details to adjust citizens' max travel cost limits for shopping and leisure.",
        waterToolsMenuTip: "Open Water Tools details to manage sea levels, lock sea height, or speed up simulation.",

        // === Phase 1.1 — Pathfinding Extended ===
        emergencyLabel: "Emergency",
        findJobLabel: "Find Job",
        findHomeLabel: "Find Home",
        findSchoolLabel: "School",
        emergencyMax: "Max pathfind cost for emergency service dispatch.\nDefault: 6000",
        findJobMax: "Max pathfind cost for citizens seeking employment.\nHigher values allow longer commutes. Default: 200000",
        findHomeMax: "Max pathfind cost for homeless households searching for housing.\nDefault: 200000",
        findSchoolMax: "Max pathfind cost for children finding an elementary school.\nDefault: 10000",
        pathfindingOptionHint: "More pathfinding options (Company Freight, High School, College, University) are available in Options > EconomyEX > Pathfinding.",
    },
    zh: {
        // === Phase 1 ===
        rentRes: "住宅租金乘数（影响所有住宅租金计算）。100% 为原版倍率。降低可缓解高地价导致的高租金问题。",
        rentCom: "商业租金乘数。100% 为原版倍率。适当降低可帮助商铺在繁华地段生存。",
        rentInd: "工业租金乘数。100% 为原版倍率。",
        envFactor: "环境地价系数：控制地形吸引力、污染等环境因素对道路地价的影响占比。（推荐/原版 = 40%）",
        svcBonus: "服务加成上限乘数：调整医疗、警察等城市服务对地价的加成上限倍率。",
        shopMax: "市民购物寻路最大成本上限。值越高允许走得越远。\n推荐值：14km/28km 选 8000 | 57km/114km 选 8000~12000",
        leisureMax: "市民休闲观光寻路最大成本上限。值越高允许走得越远。\n推荐值：14km/28km 选 8000~12000 | 57km/114km 选 12000~20000",
        reset: "恢复默认值",
        panelTitle: "MapExt 控制面板",
        mapSizeLabel: "地图尺寸",
        rentControlTitle: "租金调控",
        pathfindingTitle: "寻路参数",

        // === Phase 2 — Dashboard ===
        dashboardTitle: "城市统计",
        totalHouseholds: "总家庭",
        rentedHouseholds: "已租住",
        homelessCount: "无家可归",
        movingAwayCount: "正在搬离",
        seekerHoused: "找房（有房）",
        seekerHomeless: "找房（流浪）",
        highRentBuildings: "高租金建筑",
        petCount: "宠物",

        // === Phase 2 — 扩展租金参数 ===
        lvFactorRes: "住宅地价贡献系数：控制地价在住宅租金公式中的占比。100% = 完全贡献，降低可使租金与地价脱钩。",
        lvFactorCom: "商业地价贡献系数：控制地价在商业租金公式中的占比。100% = 完全贡献。",
        lvFactorInd: "工业地价贡献系数：控制地价在工业租金公式中的占比. 100% = 完全贡献。",
        levelFactorRes: "住宅等级贡献系数：建筑等级越高，租金在 100% 时越贵。降低可削弱升级对租金的影响。",
        levelFactorCom: "商业等级贡献系数：100% = 原版等级缩放。",
        levelFactorInd: "工业等级贡献系数：100% = 原版等级缩放。",

        // === Phase 4 — Dashboard 扩展 ===
        residentialTitle: "住宅市场",
        commercialTitle: "商业市场",
        activityTitle: "人口 activity",
        resDensityLow: "低密度",
        resDensityMed: "中密度",
        resDensityHigh: "高密度",
        resVacant: "空置",
        resTotal: "总数",
        resVacancyRate: "空置率",
        totalCommercial: "有店铺商家",
        commercialPropertyless: "等待入驻",
        shoppingCount: "购物中",
        leisureCount: "休闲中",
        goingToWork: "上班途中",
        goingHome: "回家途中",
        commuterCount: "外来通勤",
        miscTitle: "其他",

        // === Phase 5 — 水体工具 ===
        waterToolsTitle: "水体工具",
        seaLevelLabel: "海平面",
        seaLevelTip: "当前海平面高度。调整后点击“应用海平面”将水面重置到此高度。",
        seaLevelPrecise: "精确高度",
        applySeaLevel: "应用海平面",
        resetWater: "重置水面",
        waterSimSpeedLabel: "水模拟速度",
        waterSimPaused: "已暂停",
        waterSimSpeedHint: "提高速度可加快水体生成，但会增加 GPU 负载。正常游玩请保持 1x。",
        lockSeaLevel: "锁定海平面",
        lockSeaLevelTip: "锁定海平面高度，防止水体模拟改变它。设置新海平面时锁定值会自动更新。",

        // === Tooltips ===
        totalHouseholdsTip: "城市中的总家庭数（包括已租住和无家可归的家庭）。",
        rentedHouseholdsTip: "已成功租住或拥有住房的家庭比例。高比例意味着大部分市民居有其屋。",
        homelessCountTip: "目前处于流浪状态的家庭数（可能聚集在公园）。过高时建议降低住宅租金或建设低密度住宅。",
        movingAwayCountTip: "当前因生活成本过高或找不到工作而正在搬离城市的家庭数。",
        seekerHousedTip: "已有住房但因为租金过高或想要换房而正在重新寻找住宅的家庭数。",
        seekerHomelessTip: "无家可归并正积极在城市中寻找空置住房的家庭数。",
        highRentBuildingsTip: "当前显示“租金过高”警告图标的建筑数量。可通过调低“住宅租金乘数”来改善。",
        resDensityLowTip: "低密度住宅的空置单元数 / 总单元数（以及空置率）。",
        resDensityMedTip: "中密度住宅的空置单元数 / 总单元数（以及空置率）。",
        resDensityHighTip: "高密度住宅的空置单元数 / 总单元数（以及空置率）。",
        totalCommercialTip: "当前已成功入驻建筑并处于营业状态的商业公司总数。",
        commercialPropertylessTip: "已成立但未能找到空置店铺的商业公司。建议规划更多商业区。",
        shoppingCountTip: "当前正在前往购物或正在商铺内采购的市民数量。",
        leisureCountTip: "当前正在寻找或正在享受休闲娱乐（公园、地标等）的市民数量。",
        goingToWorkTip: "当前正在前往工作岗位的上班族数量。",
        goingHomeTip: "当前正在返回住宅家中的市民数量。",
        commuterCountTip: "从外界交通（高速、火车等）通勤进入本市工作的外来务工人员数量。",
        petCountTip: "城市中所有家庭拥有的宠物总数。",
        applySeaLevelTip: "强制所有水体节点立即调整并对齐到指定的“海平面”高度。",
        resetWaterTip: "完全重置水体模拟，用以清除异常波浪、局部积水或突发洪水。",
        mapSizeTip: "基于当前已解锁区域计算出的实际可玩地图尺寸。",
        statusTip: "显示 MapExt 扩展系统当前的运行状态是否正常。",
        rentControlMenuTip: "打开租金控制详情以调整住宅、商业和工业 of 租金系数。",
        pathfindingMenuTip: "打开寻路参数详情以调整市民在购物和休闲时的最大出行意愿成本限制。",
        waterToolsMenuTip: "打开水体工具详情以管理海平面、锁定水深或加速水体流速模拟。",

        // === Phase 1.1 — 寻路扩展 ===
        emergencyLabel: "急救",
        findJobLabel: "找工作",
        findHomeLabel: "找房",
        findSchoolLabel: "找学校",
        emergencyMax: "急救服务调度的最大寻路成本。\n默认值：6000",
        findJobMax: "市民找工作的最大寻路成本。值越高允许更远的通勤距离。\n默认值：200000",
        findHomeMax: "流浪家庭找房的最大寻路成本。\n默认值：200000",
        findSchoolMax: "孩子找小学的最大寻路成本。\n默认值：10000",
        pathfindingOptionHint: "更多寻路参数（公司货运、高中、大学、研究所）请前往 设置 > EconomyEX > Pathfinding 调节。",
    },
    zhHant: {
        // === Phase 1 ===
        rentRes: "住宅租金乘數（影響所有住宅租金計算）。100% 為原版倍率。降低可緩解高地價導致的高租金問題。",
        rentCom: "商業租金乘數。100% 為原版倍率。適當降低可幫助商鋪在繁華地段生存。",
        rentInd: "工業租金乘數。100% 為原版倍率。",
        envFactor: "環境地價係數：控制地形吸引力、污染等環境因素對道路地價的影響占比。（推薦/原版 = 40%）",
        svcBonus: "服務加成上限乘數：調整醫療、警察等城市服務對地價的加成上限倍率。",
        shopMax: "市民購物尋路最大成本上限。值越高允許走得越遠。\n推薦值：14km/28km 選 8000 | 57km/114km 選 8000~12000",
        leisureMax: "市民休閒觀光尋路最大成本上限。值越高允許走得越遠。\n推薦值：14km/28km 選 8000~12000 | 57km/114km 選 12000~20000",
        reset: "恢復預設值",
        panelTitle: "MapExt 控制面板",
        mapSizeLabel: "地圖尺寸",
        rentControlTitle: "租金調控",
        pathfindingTitle: "路徑參數",

        // === Phase 2 — Dashboard ===
        dashboardTitle: "城市統計",
        totalHouseholds: "總家庭",
        rentedHouseholds: "已租住",
        homelessCount: "無家可歸",
        movingAwayCount: "正在搬離",
        seekerHoused: "找房（有房）",
        seekerHomeless: "找房（流浪）",
        highRentBuildings: "高租金建築",
        petCount: "寵物",

        // === Phase 2 — 扩展租金参数 ===
        lvFactorRes: "住宅地價貢獻係數：控制地價在住宅租金公式中的占比。100% = 完全貢獻，降低可使租金與地價脫鉤。",
        lvFactorCom: "商業地價貢獻係數：控制地價在商業租金公式中的占比。100% = 完全貢獻。",
        lvFactorInd: "工業地價貢獻係數：控制地價在工業租金公式中的占比。100% = 完全貢獻。",
        levelFactorRes: "住宅等級貢獻係數：建築等級越高，租金在 100% 時越貴。降低可削弱升級對租金的影響。",
        levelFactorCom: "商業等級貢獻係數：100% = 原版等級縮放。",
        levelFactorInd: "工業等級貢獻係數：100% = 原版等級縮放。",

        // === Phase 4 — Dashboard 扩展 ===
        residentialTitle: "住宅市場",
        commercialTitle: "商業市場",
        activityTitle: "人口活動",
        resDensityLow: "低密度",
        resDensityMed: "中密度",
        resDensityHigh: "高密度",
        resVacant: "空置",
        resTotal: "總數",
        resVacancyRate: "空置率",
        totalCommercial: "有店鋪商家",
        commercialPropertyless: "等待入駐",
        shoppingCount: "購物中",
        leisureCount: "休閒中",
        goingToWork: "上班途中",
        goingHome: "回家途中",
        commuterCount: "外來通勤",
        miscTitle: "其他",

        // === Phase 5 — 水体工具 ===
        waterToolsTitle: "水體工具",
        seaLevelLabel: "海平面",
        seaLevelTip: "當前海平面高度。調整後點擊“套用海平面”將水面重置到此高度。",
        seaLevelPrecise: "精確高度",
        applySeaLevel: "套用海平面",
        resetWater: "重置水面",
        waterSimSpeedLabel: "水模擬速度",
        waterSimPaused: "已暫停",
        waterSimSpeedHint: "提高速度可加快水體生成，但會增加 GPU 負載。正常遊玩請保持 1x。",
        lockSeaLevel: "鎖定海平面",
        lockSeaLevelTip: "鎖定海平面高度，防止水體模擬改變它。設置新海平面時鎖定值會自動更新。",

        // === Tooltips ===
        totalHouseholdsTip: "城市中的家庭總數（包括已租屋與無家可歸的家庭）。",
        rentedHouseholdsTip: "已成功租屋或擁有住房的家庭比例。高比例代表大部分市民居有其屋。",
        homelessCountTip: "目前處於流浪狀態的家庭數（可能聚集在公園）。過高時建議調低住宅租金或建設低密度住宅。",
        movingAwayCountTip: "當前因生活成本過高或找不到工作而正在搬离城市的家庭數。",
        seekerHousedTip: "已有住房但因為租金過高或想換房而重新尋找住宅的家庭數。",
        seekerHomelessTip: "無家可歸並正積極在城市中尋找空置住房的家庭數。",
        highRentBuildingsTip: "當前顯示“租金過高”警示圖示的建築數量。玩家可調低“住宅租金乘數”以改善此狀況。",
        resDensityLowTip: "低密度住宅的空置單元數 / 總單元數（以及空置率）。",
        resDensityMedTip: "中密度住宅的空置單元數 / 總單元數（以及空置率）。",
        resDensityHighTip: "高密度住宅的空置單元數 / 總單元數（以及空置率）。",
        totalCommercialTip: "當前已成功入駐建築並處於營業狀態的商業公司總數。",
        commercialPropertylessTip: "已成立但未能找到合適店面的商業公司。建議規劃更多商業區。",
        shoppingCountTip: "當前正在前往購物或正在商店內採購的市民數量。",
        leisureCountTip: "當前正在尋找或正在享受休閒娛樂（公園、地標等）的市民數量。",
        goingToWorkTip: "當前正處於通勤前往工作單位途中的上班族數量。",
        goingHomeTip: "當前正在返回住宅家中的市民数量。",
        commuterCountTip: "從聯外交通（高速公路、火車等）通勤進入本市工作的外來勞工數量。",
        petCountTip: "城市中所有家庭擁有的寵物總數。",
        applySeaLevelTip: "強制所有水體節點立即調整並對齊到指定的“海平面”高度。",
        resetWaterTip: "完全重置水體模擬，用以清除異常波浪、局部積水或突发洪水。",
        mapSizeTip: "基於當前已解鎖區域計算出的實際可玩地圖尺寸。",
        statusTip: "顯示 MapExt 擴充系統當前的運作狀態是否正常。",
        rentControlMenuTip: "打開租金控制詳情以調整住宅、商業和工業的租金乘數。",
        pathfindingMenuTip: "打開路徑參數詳情以調整市民在購物和休閒時的最大出行成本意愿限制。",
        waterToolsMenuTip: "打開水體工具詳情以管理海平面、鎖定水深或加速水體流速模擬。",

        // === Phase 1.1 — 尋路擴展 ===
        emergencyLabel: "急救",
        findJobLabel: "找工作",
        findHomeLabel: "找房",
        findSchoolLabel: "找學校",
        emergencyMax: "急救服務調度的最大尋路成本。\n預設值：6000",
        findJobMax: "市民找工作的最大尋路成本。值越高允許更遠的通勤距離。\n預設值：200000",
        findHomeMax: "流浪家庭找房的最大尋路成本。\n預設值：200000",
        findSchoolMax: "孩子找小學的最大尋路成本。\n預設值：10000",
        pathfindingOptionHint: "更多尋路參數（公司貨運、高中、大學、研究所）請前往 設定 > EconomyEX > Pathfinding 調節。",
    }
};

export type LocaleKey = keyof typeof locales.en;

/**
 * React Hook：返回当前语言对应的翻译函数。
 * 直接读取游戏引擎的 `app.activeLocale` binding，
 * 无需依赖 navigator.language 等浏览器 API。
 */
export function useTranslation() {
    const locale = useValue(activeLocale$);
    const lower = locale ? locale.toLowerCase() : "";
    let dict = locales.en;

    if (lower.startsWith("zh-hant") || lower.startsWith("zh-tw") || lower.startsWith("zh-hk")) {
        dict = locales.zhHant;
    } else if (lower.startsWith("zh")) {
        dict = locales.zh;
    }

    return (key: LocaleKey): string => dict[key];
}

/**
 * React Hook：返回当前语言是否为中文。
 * 供需要直接判断语言的组件使用（如分段选择器标签）。
 */
export function useIsZh(): boolean {
    const locale = useValue(activeLocale$);
    return !!(locale && locale.toLowerCase().startsWith("zh"));
}
