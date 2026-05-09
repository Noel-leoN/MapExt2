// MapExtButton — 游戏内浮动按钮 + Master-Detail 双面板 + 拖拽调宽 + 字体大小
// 左侧菜单 + 右侧详情面板布局，支持鼠标拖拽调整面板宽度（持久化到 ModSettings）

import { useValue } from "cs2/api";
import React, { useState, useCallback, useEffect } from "react";
import { FloatingButton } from "cs2/ui";
import {
    panelOpen$,
    setPanelOpen,
    mapSizeInfo$, systemStatus$,
    setDashboardOpen,
    uiFontSize$,
    uiMenuWidth$, uiDetailWidth$,
    setUIMenuWidth, setUIDetailWidth,
} from "../bindings";
import { DashboardSection } from "../sections/DashboardSection";
import { RentControlSection } from "../sections/RentControlSection";
import { PathfindingSection } from "../sections/PathfindingSection";
import { ResizeHandle } from "./ResizeHandle";
import styles from "../mapext.module.scss";
import mapIcon from "../assets/map-icon-M.svg";
import { useTranslation } from "../locales";

type SectionId = "dashboard" | "rent" | "pathfinding" | null;

// 面板宽度限制
const MENU_MIN = 160, MENU_MAX = 320;
const DETAIL_MIN = 200, DETAIL_MAX = 450;

export const MapExtButton: React.FC = () => {
    const isOpen = useValue(panelOpen$);
    const mapSize = useValue(mapSizeInfo$);
    const status = useValue(systemStatus$);
    const fontSize = useValue(uiFontSize$);
    const savedMenuWidth = useValue(uiMenuWidth$);
    const savedDetailWidth = useValue(uiDetailWidth$);
    const t = useTranslation();
    const [activeSection, setActiveSection] = useState<SectionId>(null);
    const [menuWidth, setMenuWidth] = useState(savedMenuWidth);
    const [detailWidth, setDetailWidth] = useState(savedDetailWidth);

    // 从 C# binding 同步初始宽度（Options UI 修改时也会触发）
    useEffect(() => { setMenuWidth(savedMenuWidth); }, [savedMenuWidth]);
    useEffect(() => { setDetailWidth(savedDetailWidth); }, [savedDetailWidth]);

    const handleToggle = () => {
        setPanelOpen(!isOpen);
        if (isOpen) {
            setActiveSection(null);
            setDashboardOpen(false);
        }
    };

    const toggleSection = useCallback((id: SectionId) => {
        const next = activeSection === id ? null : id;
        setActiveSection(next);
        setDashboardOpen(next === "dashboard");
    }, [activeSection]);

    // 拖拽调整左侧菜单宽度（右边缘）
    const onMenuResize = useCallback((delta: number) => {
        setMenuWidth(prev => Math.max(MENU_MIN, Math.min(MENU_MAX, prev + delta)));
    }, []);

    // 拖拽结束：持久化左侧宽度到 C#
    const onMenuResizeEnd = useCallback(() => {
        setMenuWidth(prev => { setUIMenuWidth(prev); return prev; });
    }, []);

    // 拖拽调整右侧详情宽度（右边缘）
    const onDetailResize = useCallback((delta: number) => {
        setDetailWidth(prev => Math.max(DETAIL_MIN, Math.min(DETAIL_MAX, prev + delta)));
    }, []);

    // 拖拽结束：持久化右侧宽度到 C#
    const onDetailResizeEnd = useCallback(() => {
        setDetailWidth(prev => { setUIDetailWidth(prev); return prev; });
    }, []);

    const menuClass = (id: SectionId) =>
        `${styles.menuItem} ${activeSection === id ? styles.menuItemActive : ""}`;

    // CSS 变量：字体大小
    const cssVars = { "--me-font-size": `${fontSize}rem` } as React.CSSProperties;

    return (
        <>
            <FloatingButton
                onClick={handleToggle}
                selected={isOpen}
                src={mapIcon}
                tinted={true}
            />

            {isOpen && (
                <div className={styles.panelContainer} style={cssVars}>
                    {/* === 左侧菜单面板 === */}
                    <div className={styles.menuPanel} style={{ width: `${menuWidth}rem` }}>
                        <div className={styles.panelHeader}>
                            <span className={styles.panelTitle}>{t("panelTitle")}</span>
                            <button
                                className={styles.closeButton}
                                onClick={() => { setPanelOpen(false); setActiveSection(null); setDashboardOpen(false); }}
                            >
                                X
                            </button>
                        </div>

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

                    {/* 左侧面板右边缘拖拽柄 */}
                    <ResizeHandle direction="horizontal" onResize={onMenuResize} onResizeEnd={onMenuResizeEnd} />

                    {/* === 右侧详情面板 === */}
                    {activeSection && (
                        <>
                            <div className={styles.detailPanel} style={{ width: `${detailWidth}rem` }}>
                                {activeSection === "dashboard" && <DashboardSection />}
                                {activeSection === "rent" && <RentControlSection />}
                                {activeSection === "pathfinding" && <PathfindingSection />}
                            </div>
                            {/* 右侧面板右边缘拖拽柄 */}
                            <ResizeHandle direction="horizontal" onResize={onDetailResize} onResizeEnd={onDetailResizeEnd} />
                        </>
                    )}
                </div>
            )}
        </>
    );
};
