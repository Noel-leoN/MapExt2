// MapExtButton — 游戏内浮动按钮 + Master-Detail 双面板 (Phase 2)
// 左侧菜单 + 右侧详情面板布局

import { useValue } from "cs2/api";
import React, { useState, useCallback } from "react";
import { FloatingButton } from "cs2/ui";
import {
    panelOpen$,
    setPanelOpen,
    mapSizeInfo$, systemStatus$,
    setDashboardOpen,
} from "../bindings";
import { DashboardSection } from "../sections/DashboardSection";
import { RentControlSection } from "../sections/RentControlSection";
import { PathfindingSection } from "../sections/PathfindingSection";
import styles from "../mapext.module.scss";
import mapIcon from "../assets/map-icon-M.svg";
import { useTranslation } from "../locales";

type SectionId = "dashboard" | "rent" | "pathfinding" | null;

export const MapExtButton: React.FC = () => {
    const isOpen = useValue(panelOpen$);
    const mapSize = useValue(mapSizeInfo$);
    const status = useValue(systemStatus$);
    const t = useTranslation();
    const [activeSection, setActiveSection] = useState<SectionId>(null);

    const handleToggle = () => {
        setPanelOpen(!isOpen);
        if (isOpen) {
            // 关闭面板时同时关闭 Dashboard
            setActiveSection(null);
            setDashboardOpen(false);
        }
    };

    const toggleSection = useCallback((id: SectionId) => {
        const next = activeSection === id ? null : id;
        setActiveSection(next);
        // 联动 Dashboard Q2 系统启停
        setDashboardOpen(next === "dashboard");
    }, [activeSection]);

    // 选中态样式
    const menuClass = (id: SectionId) =>
        `${styles.menuItem} ${activeSection === id ? styles.menuItemActive : ""}`;

    return (
        <>
            <FloatingButton
                onClick={handleToggle}
                selected={isOpen}
                src={mapIcon}
                tinted={true}
            />

            {isOpen && (
                <div className={styles.panelContainer}>
                    {/* === 左侧菜单面板 === */}
                    <div className={styles.menuPanel}>
                        {/* 标题栏 */}
                        <div className={styles.panelHeader}>
                            <span className={styles.panelTitle}>{t("panelTitle")}</span>
                            <button
                                className={styles.closeButton}
                                onClick={() => { setPanelOpen(false); setActiveSection(null); setDashboardOpen(false); }}
                            >
                                X
                            </button>
                        </div>

                        {/* 状态信息区（始终可见） */}
                        <div className={styles.statusBar}>
                            <div className={styles.statusItem}>
                                <span className={styles.statusLabel}>{t("mapSizeLabel")}</span>
                                <span className={styles.statusValue}>{mapSize}</span>
                            </div>
                            <div className={styles.statusItem}>
                                <span className={styles.statusLabel}>STATUS</span>
                                <span className={styles.statusValue}>{status}</span>
                            </div>
                        </div>

                        {/* 菜单项 */}
                        <div className={styles.menuList}>
                            <button className={menuClass("dashboard")} onClick={() => toggleSection("dashboard")}>
                                <span>{activeSection === "dashboard" ? ">" : "+"}</span>
                                <span>{t("dashboardTitle")}</span>
                            </button>
                            <button className={menuClass("rent")} onClick={() => toggleSection("rent")}>
                                <span>{activeSection === "rent" ? ">" : "+"}</span>
                                <span>{t("rentControlTitle")}</span>
                            </button>
                            <button className={menuClass("pathfinding")} onClick={() => toggleSection("pathfinding")}>
                                <span>{activeSection === "pathfinding" ? ">" : "+"}</span>
                                <span>{t("pathfindingTitle")}</span>
                            </button>
                        </div>
                    </div>

                    {/* === 右侧详情面板（仅在选中时出现） === */}
                    {activeSection && (
                        <div className={styles.detailPanel}>
                            {activeSection === "dashboard" && <DashboardSection />}
                            {activeSection === "rent" && <RentControlSection />}
                            {activeSection === "pathfinding" && <PathfindingSection />}
                        </div>
                    )}
                </div>
            )}
        </>
    );
};
