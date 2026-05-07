// PathfindingSection — 寻路参数调控区域 (含 Reset)

import React, { useState } from "react";
import { SliderControl } from "../components/SliderControl";
import {
    shopMaxCost$, leisureMaxCost$,
    setShopMaxCost, setLeisureMaxCost,
    resetPathfinding,
} from "../bindings";
import styles from "../mapext.module.scss";
import { useTranslation } from "../locales";

export const PathfindingSection: React.FC = () => {
    const [isOpen, setIsOpen] = useState(false);
    const t = useTranslation();

    return (
        <>
            <button className={styles.configToggle} onClick={() => setIsOpen(!isOpen)}>
                <span>{isOpen ? "[-]" : "[+]"} {t("pathfindingTitle")}</span>
            </button>

            {isOpen && (
                <div className={styles.section}>
                    <SliderControl
                        label="Shopping"
                        binding={shopMaxCost$}
                        commit={setShopMaxCost}
                        min={1000} max={200000} step={1000}
                        unit=""
                        tooltip={t("shopMax")}
                    />
                    <SliderControl
                        label="Leisure"
                        binding={leisureMaxCost$}
                        commit={setLeisureMaxCost}
                        min={1000} max={200000} step={1000}
                        unit=""
                        tooltip={t("leisureMax")}
                    />

                    <button
                        className={styles.resetButton}
                        onClick={resetPathfinding}
                    >
                        {t("reset")}
                    </button>
                </div>
            )}
        </>
    );
};
