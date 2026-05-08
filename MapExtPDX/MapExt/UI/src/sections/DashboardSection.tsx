// DashboardSection — 城市统计数据展示区域 (Phase 2)
// 纯只读面板，折叠时联动 C# Q2_CityStatsSystem 启停

import React from "react";
import { useValue } from "cs2/api";
import {
    dashboardOpen$, setDashboardOpen,
    totalHouseholds$, rentedHouseholds$,
    homelessCount$, movingAwayCount$,
    seekerHousedCount$, seekerHomelessCount$,
    highRentCount$, petCount$,
} from "../bindings";
import styles from "../mapext.module.scss";
import { useTranslation } from "../locales";

export const DashboardSection: React.FC = () => {
    const isOpen = useValue(dashboardOpen$);
    const t = useTranslation();

    const totalHH = useValue(totalHouseholds$);
    const rentedHH = useValue(rentedHouseholds$);
    const homeless = useValue(homelessCount$);
    const movingAway = useValue(movingAwayCount$);
    const seekerHoused = useValue(seekerHousedCount$);
    const seekerHomeless = useValue(seekerHomelessCount$);
    const highRent = useValue(highRentCount$);
    const pets = useValue(petCount$);

    // 已租住百分比
    const housingPct = totalHH > 0 ? ((rentedHH / totalHH) * 100).toFixed(1) : "0.0";

    const handleToggle = () => {
        setDashboardOpen(!isOpen);
    };

    // 格式化数字（千位分隔）
    const fmt = (n: number) => n.toLocaleString();

    return (
        <>
            <button className={styles.configToggle} onClick={handleToggle}>
                <span>{isOpen ? "[-]" : "[+]"} {t("dashboardTitle")}</span>
            </button>

            {isOpen && (
                <div className={styles.section}>
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
                    <div className={styles.statRow}>
                        <span className={styles.statLabel}>{t("petCount")}</span>
                        <span className={styles.statValue}>{fmt(pets)}</span>
                    </div>
                </div>
            )}
        </>
    );
};
