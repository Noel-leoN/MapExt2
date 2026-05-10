// DashboardSection — 城市统计数据展示（嵌入左侧面板版）
// 5 个 Accordion 区块，默认展开状态由 ModSettings 中的 DashboardDefault* 控制
// 住宅市场采用垂直密度行："Low  0 / 1,383 (0.0%)"

import React, { useState } from "react";
import { useValue } from "cs2/api";
import {
    totalHouseholds$, rentedHouseholds$,
    homelessCount$, movingAwayCount$,
    seekerHousedCount$, seekerHomelessCount$,
    highRentCount$, petCount$,
    freeResLow$, freeResMed$, freeResHigh$,
    totalResLow$, totalResMed$, totalResHigh$,
    totalCommercial$, commercialPropertyless$,
    shoppingCount$, leisureCount$,
    goingToWorkCount$, goingHomeCount$,
    commuterCount$,
    // 默认展开配置
    dashDefaultCityStats$, dashDefaultResidential$,
    dashDefaultCommercial$, dashDefaultActivity$, dashDefaultMisc$,
} from "../bindings";
import styles from "../mapext.module.scss";
import { useTranslation } from "../locales";

/** 可折叠区块 */
const Accordion: React.FC<{
    title: string;
    defaultOpen: boolean;
    children: React.ReactNode;
}> = ({ title, defaultOpen, children }) => {
    const [open, setOpen] = useState(defaultOpen);
    return (
        <div className={styles.accordionBlock}>
            <button
                className={`${styles.accordionHeader} ${open ? styles.accordionOpen : ""}`}
                onClick={() => setOpen(!open)}
            >
                <span className={styles.accordionArrow}>{open ? "\u25BC" : "\u25B6"}</span>
                <span>{title}</span>
            </button>
            {open && <div className={styles.accordionBody}>{children}</div>}
        </div>
    );
};

export const DashboardSection: React.FC = () => {
    const t = useTranslation();

    // 默认展开配置（从 ModSettings 读取）
    const defCityStats   = useValue(dashDefaultCityStats$);
    const defResidential = useValue(dashDefaultResidential$);
    const defCommercial  = useValue(dashDefaultCommercial$);
    const defActivity    = useValue(dashDefaultActivity$);
    const defMisc        = useValue(dashDefaultMisc$);

    // 基础指标
    const totalHH = useValue(totalHouseholds$);
    const rentedHH = useValue(rentedHouseholds$);
    const homeless = useValue(homelessCount$);
    const movingAway = useValue(movingAwayCount$);
    const seekerHoused = useValue(seekerHousedCount$);
    const seekerHomeless = useValue(seekerHomelessCount$);
    const highRent = useValue(highRentCount$);
    const pets = useValue(petCount$);

    // 住宅空置率
    const freeL = useValue(freeResLow$);
    const freeM = useValue(freeResMed$);
    const freeH = useValue(freeResHigh$);
    const totalL = useValue(totalResLow$);
    const totalM = useValue(totalResMed$);
    const totalH = useValue(totalResHigh$);

    // 商业
    const totalCom = useValue(totalCommercial$);
    const comPropertyless = useValue(commercialPropertyless$);

    // 人口活动
    const shopping = useValue(shoppingCount$);
    const leisure = useValue(leisureCount$);
    const goWork = useValue(goingToWorkCount$);
    const goHome = useValue(goingHomeCount$);
    const commuters = useValue(commuterCount$);

    // 计算辅助
    const housingPct = totalHH > 0 ? ((rentedHH / totalHH) * 100).toFixed(1) : "0.0";
    const vacPct = (free: number, total: number) =>
        total > 0 ? ((free / total) * 100).toFixed(1) : "0.0";
    const fmt = (n: number) => n.toLocaleString();

    return (
        <>
            {/* === 住房概览 === */}
            <Accordion title={t("dashboardTitle")} defaultOpen={defCityStats}>
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("totalHouseholds")}</span>
                    <span className={styles.statValue}>{fmt(totalHH)}</span>
                </div>
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("rentedHouseholds")}</span>
                    <span className={styles.statValue}>
                        {fmt(rentedHH)}
                        <span className={styles.statPercent}> ({housingPct}%)</span>
                    </span>
                </div>
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("homelessCount")}</span>
                    <span className={homeless > 500 ? styles.statWarning : styles.statValue}>
                        {fmt(homeless)}
                    </span>
                </div>
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("movingAwayCount")}</span>
                    <span className={movingAway > 200 ? styles.statWarning : styles.statValue}>
                        {fmt(movingAway)}
                    </span>
                </div>
                <div className={styles.sectionDivider} />
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("seekerHoused")}</span>
                    <span className={styles.statValue}>{fmt(seekerHoused)}</span>
                </div>
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("seekerHomeless")}</span>
                    <span className={seekerHomeless > 300 ? styles.statWarning : styles.statValue}>
                        {fmt(seekerHomeless)}
                    </span>
                </div>
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("highRentBuildings")}</span>
                    <span className={highRent > 200 ? styles.statWarning : styles.statValue}>
                        {fmt(highRent)}
                    </span>
                </div>
            </Accordion>

            {/* === 住宅市场 === */}
            <Accordion title={t("residentialTitle")} defaultOpen={defResidential}>
                <div className={styles.densityRow}>
                    <span className={styles.densityLabel}>{t("resDensityLow")}</span>
                    <span className={styles.densityData}>
                        {fmt(freeL)} / {fmt(totalL)}
                    </span>
                    <span className={styles.densityRate}>({vacPct(freeL, totalL)}%)</span>
                </div>
                <div className={styles.densityRow}>
                    <span className={styles.densityLabel}>{t("resDensityMed")}</span>
                    <span className={styles.densityData}>
                        {fmt(freeM)} / {fmt(totalM)}
                    </span>
                    <span className={styles.densityRate}>({vacPct(freeM, totalM)}%)</span>
                </div>
                <div className={styles.densityRow}>
                    <span className={styles.densityLabel}>{t("resDensityHigh")}</span>
                    <span className={styles.densityData}>
                        {fmt(freeH)} / {fmt(totalH)}
                    </span>
                    <span className={styles.densityRate}>({vacPct(freeH, totalH)}%)</span>
                </div>
            </Accordion>

            {/* === 商业市场 === */}
            <Accordion title={t("commercialTitle")} defaultOpen={defCommercial}>
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("totalCommercial")}</span>
                    <span className={styles.statValue}>{fmt(totalCom)}</span>
                </div>
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("commercialPropertyless")}</span>
                    <span className={comPropertyless > 100 ? styles.statWarning : styles.statValue}>
                        {fmt(comPropertyless)}
                    </span>
                </div>
            </Accordion>

            {/* === 人口活动 === */}
            <Accordion title={t("activityTitle")} defaultOpen={defActivity}>
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("shoppingCount")}</span>
                    <span className={styles.statValue}>{fmt(shopping)}</span>
                </div>
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("leisureCount")}</span>
                    <span className={styles.statValue}>{fmt(leisure)}</span>
                </div>
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("goingToWork")}</span>
                    <span className={styles.statValue}>{fmt(goWork)}</span>
                </div>
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("goingHome")}</span>
                    <span className={styles.statValue}>{fmt(goHome)}</span>
                </div>
            </Accordion>

            {/* === 其他 === */}
            <Accordion title={t("miscTitle")} defaultOpen={defMisc}>
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("commuterCount")}</span>
                    <span className={styles.statValue}>{fmt(commuters)}</span>
                </div>
                <div className={styles.statRow}>
                    <span className={styles.statLabel}>{t("petCount")}</span>
                    <span className={styles.statValue}>{fmt(pets)}</span>
                </div>
            </Accordion>
        </>
    );
};
