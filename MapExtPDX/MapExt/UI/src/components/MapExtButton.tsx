// MapExtButton — 游戏内浮动按钮 + 展开面板 (Phase 1 + Phase 2)
// Coherent Gameface 兼容版：不使用 Emoji/Unicode 特殊字符

import { useValue } from "cs2/api";
import React from "react";
import { FloatingButton } from "cs2/ui";
import {
    panelOpen$,
    setPanelOpen,
    mapSizeInfo$, systemStatus$,
} from "../bindings";
import { DashboardSection } from "../sections/DashboardSection";
import { RentControlSection } from "../sections/RentControlSection";
import { PathfindingSection } from "../sections/PathfindingSection";
import styles from "../mapext.module.scss";
import mapIcon from "../assets/map-icon-M.svg";
import { useTranslation } from "../locales";

export const MapExtButton: React.FC = () => {
    const isOpen = useValue(panelOpen$);
    const mapSize = useValue(mapSizeInfo$);
    const status = useValue(systemStatus$);
    const t = useTranslation();

    const handleToggle = () => {
        setPanelOpen(!isOpen);
    };

    return (
        <>
            <FloatingButton
                onClick={handleToggle}
                selected={isOpen}
                src={mapIcon}
                tinted={true}
            />

            {isOpen && (
                <div className={styles.panel}>
                    {/* === 面板标题栏 === */}
                    <div className={styles.panelHeader}>
                        <span className={styles.panelTitle}>{t("panelTitle")}</span>
                        <button
                            className={styles.closeButton}
                            onClick={() => setPanelOpen(false)}
                        >
                            X
                        </button>
                    </div>

                    {/* === 状态信息区（始终可见） === */}
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

                    {/* === 折叠配置区 === */}
                    <div className={styles.configArea}>
                        <DashboardSection />
                        <RentControlSection />
                        <PathfindingSection />
                    </div>
                </div>
            )}
        </>
    );
};
