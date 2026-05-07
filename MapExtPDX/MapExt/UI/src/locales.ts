// locales.ts - MapExt 中英文本字典
// 使用游戏引擎内置的 activeLocale binding 检测语言

import { bindValue, useValue } from "cs2/api";

// 游戏内置 binding：返回 "en-US", "zh-HANS" 等
const activeLocale$ = bindValue<string>("app", "activeLocale");

export const locales = {
    en: {
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
        pathfindingTitle: "Pathfinding"
    },
    zh: {
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
        pathfindingTitle: "寻路参数"
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
