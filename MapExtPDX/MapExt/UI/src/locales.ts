// locales.ts - MapExt 中英文本字典 (Phase 1 ~ Phase 4)
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
        lvFactorInd: "工业地价贡献系数：控制地价在工业租金公式中的占比。100% = 完全贡献。",
        levelFactorRes: "住宅等级贡献系数：建筑等级越高，租金在 100% 时越贵。降低可削弱升级对租金的影响。",
        levelFactorCom: "商业等级贡献系数：100% = 原版等级缩放。",
        levelFactorInd: "工业等级贡献系数：100% = 原版等级缩放。",

        // === Phase 4 — Dashboard 扩展 ===
        residentialTitle: "住宅市场",
        commercialTitle: "商业市场",
        activityTitle: "人口活动",
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
    // locale 格式为 "zh-HANS", "en-US", "de-DE" 等
    const isZh = locale && locale.toLowerCase().startsWith("zh");
    const dict = isZh ? locales.zh : locales.en;

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
