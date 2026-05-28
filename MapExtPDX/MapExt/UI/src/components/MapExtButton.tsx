// MapExtButton — 游戏内浮动按钮 + Dashboard-in-Left + Detail-in-Right 布局
// 左侧面板：标题栏 + 状态栏 + Dashboard 统计（accordion）+ 底部菜单按钮
// 右侧面板：仅 Rent Control / Pathfinding 展开时显示
// 面板宽度和高度均持久化到 ModSettings（支持 OptionsUI + 拖拽）

import { useValue } from "cs2/api";
import React, { useState, useCallback, useEffect } from "react";
import { FloatingButton, Tooltip } from "cs2/ui";
import {
    panelOpen$,
    setPanelOpen,
    mapSizeInfo$, systemStatus$,
    setDashboardOpen,
    uiMenuWidth$, uiDetailWidth$, uiPanelHeight$,
    setUIMenuWidth, setUIDetailWidth, setUIPanelHeight,
} from "../bindings";
import { DashboardSection } from "../sections/DashboardSection";
import { RentControlSection } from "../sections/RentControlSection";
import { PathfindingSection } from "../sections/PathfindingSection";
import { WaterToolsSection } from "../sections/WaterToolsSection";
import { ResizeHandle } from "./ResizeHandle";
import styles from "../mapext.module.scss";
import mapIcon from "../assets/map-icon-M.svg";
import { useTranslation } from "../locales";

type DetailId = "rent" | "pathfinding" | "waterTools" | null;

// 面板尺寸限制
const MENU_MIN = 180, MENU_MAX = 360;
const DETAIL_MIN = 200, DETAIL_MAX = 450;
const HEIGHT_MIN = 300, HEIGHT_MAX = 1000;

export const MapExtButton: React.FC = () => {
    const isOpen = useValue(panelOpen$);
    const mapSize = useValue(mapSizeInfo$);
    const status = useValue(systemStatus$);
    const savedMenuWidth = useValue(uiMenuWidth$);
    const savedDetailWidth = useValue(uiDetailWidth$);
    const savedPanelHeight = useValue(uiPanelHeight$);
    const t = useTranslation();
    const [activeDetail, setActiveDetail] = useState<DetailId>(null);
    const [menuWidth, setMenuWidth] = useState(savedMenuWidth);
    const [detailWidth, setDetailWidth] = useState(savedDetailWidth);
    const [panelHeight, setPanelHeight] = useState(savedPanelHeight);

    // 从 C# binding 同步初始值（Options UI 修改时也会触发）
    useEffect(() => { setMenuWidth(savedMenuWidth); }, [savedMenuWidth]);
    useEffect(() => { setDetailWidth(savedDetailWidth); }, [savedDetailWidth]);
    useEffect(() => { setPanelHeight(savedPanelHeight); }, [savedPanelHeight]);

    // 面板打开时始终启用 Dashboard
    const handleToggle = () => {
        const opening = !isOpen;
        setPanelOpen(opening);
        if (opening) {
            setDashboardOpen(true);
        } else {
            setActiveDetail(null);
            setDashboardOpen(false);
        }
    };

    const toggleDetail = useCallback((id: DetailId) => {
        setActiveDetail(prev => prev === id ? null : id);
    }, []);

    // 拖拽调整左侧菜单宽度
    const onMenuResize = useCallback((delta: number) => {
        setMenuWidth(prev => Math.max(MENU_MIN, Math.min(MENU_MAX, prev + delta)));
    }, []);
    const onMenuResizeEnd = useCallback(() => {
        setMenuWidth(prev => { setUIMenuWidth(prev); return prev; });
    }, []);

    // 拖拽调整右侧详情宽度
    const onDetailResize = useCallback((delta: number) => {
        setDetailWidth(prev => Math.max(DETAIL_MIN, Math.min(DETAIL_MAX, prev + delta)));
    }, []);
    const onDetailResizeEnd = useCallback(() => {
        setDetailWidth(prev => { setUIDetailWidth(prev); return prev; });
    }, []);

    // 拖拽调整左侧面板高度（持久化）
    const onHeightResize = useCallback((delta: number) => {
        setPanelHeight(prev => Math.max(HEIGHT_MIN, Math.min(HEIGHT_MAX, prev + delta)));
    }, []);
    const onHeightResizeEnd = useCallback(() => {
        setPanelHeight(prev => { setUIPanelHeight(prev); return prev; });
    }, []);

    const detailClass = (id: DetailId) =>
        `${styles.menuItem} ${activeDetail === id ? styles.menuItemActive : ""}`;

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
                    {/* === 左侧面板：Dashboard 统计 === */}
                    <div className={styles.menuPanel} style={{ width: `${menuWidth}rem`, maxHeight: `${panelHeight}rem` }}>
                        <div className={styles.panelHeader}>
                            <span className={styles.panelTitle}>{t("panelTitle")}</span>
                            <button
                                className={styles.closeButton}
                                onClick={() => { setPanelOpen(false); setActiveDetail(null); setDashboardOpen(false); }}
                            >
                                X
                            </button>
                        </div>

                        <div className={styles.statusBar}>
                            <div className={styles.statusItem}>
                                <Tooltip tooltip={t("mapSizeTip")}>
                                    <span className={styles.statusLabel}>{t("mapSizeLabel")}</span>
                                </Tooltip>
                                <span className={styles.statusValue}>{mapSize}</span>
                            </div>
                            <div className={styles.statusItem}>
                                <Tooltip tooltip={t("statusTip")}>
                                    <span className={styles.statusLabel}>STATUS</span>
                                </Tooltip>
                                <span className={styles.statusValue}>{status}</span>
                            </div>
                        </div>

                        {/* Dashboard 统计内容直接嵌入左侧面板 */}
                        <div className={styles.dashboardScroll}>
                            <DashboardSection />
                        </div>

                        {/* 底部菜单：Rent Control / Pathfinding → 展开右侧 */}
                        <div className={styles.menuFooter}>
                            <Tooltip tooltip={t("rentControlMenuTip")}>
                                <button className={detailClass("rent")} onClick={() => toggleDetail("rent")}>
                                    <span>{activeDetail === "rent" ? ">" : "+"}</span>
                                    <span>{t("rentControlTitle")}</span>
                                </button>
                            </Tooltip>
                            <Tooltip tooltip={t("pathfindingMenuTip")}>
                                <button className={detailClass("pathfinding")} onClick={() => toggleDetail("pathfinding")}>
                                    <span>{activeDetail === "pathfinding" ? ">" : "+"}</span>
                                    <span>{t("pathfindingTitle")}</span>
                                </button>
                            </Tooltip>
                            <Tooltip tooltip={t("waterToolsMenuTip")}>
                                <button className={detailClass("waterTools")} onClick={() => toggleDetail("waterTools")}>
                                    <span>{activeDetail === "waterTools" ? ">" : "+"}</span>
                                    <span>{t("waterToolsTitle")}</span>
                                </button>
                            </Tooltip>
                        </div>

                        {/* 左侧面板底部高度拖拽柄 */}
                        <ResizeHandle direction="vertical" onResize={onHeightResize} onResizeEnd={onHeightResizeEnd} />
                    </div>

                    {/* 左侧面板右边缘拖拽柄 */}
                    <ResizeHandle direction="horizontal" onResize={onMenuResize} onResizeEnd={onMenuResizeEnd} />

                    {/* === 右侧详情面板（仅 Rent/Pathfinding） === */}
                    {activeDetail && (
                        <>
                            <div className={styles.detailPanel} style={{ width: `${detailWidth}rem` }}>
                                {activeDetail === "rent" && <RentControlSection />}
                                {activeDetail === "pathfinding" && <PathfindingSection />}
                                {activeDetail === "waterTools" && <WaterToolsSection />}
                            </div>
                            <ResizeHandle direction="horizontal" onResize={onDetailResize} onResizeEnd={onDetailResizeEnd} />
                        </>
                    )}
                </div>
            )}
        </>
    );
};
