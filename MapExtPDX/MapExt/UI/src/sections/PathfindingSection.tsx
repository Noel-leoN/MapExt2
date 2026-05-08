// PathfindingSection — 寻路参数调控（内容组件，无 toggle 按钮）

import React from "react";
import { SliderControl } from "../components/SliderControl";
import {
    shopMaxCost$, leisureMaxCost$,
    setShopMaxCost, setLeisureMaxCost,
    resetPathfinding,
} from "../bindings";
import styles from "../mapext.module.scss";
import { useTranslation } from "../locales";

export const PathfindingSection: React.FC = () => {
    const t = useTranslation();

    return (
        <div className={styles.detailContent}>
            <div className={styles.detailTitle}>{t("pathfindingTitle")}</div>

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
    );
};
