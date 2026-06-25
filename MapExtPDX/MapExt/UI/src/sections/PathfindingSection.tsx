// PathfindingSection — 寻路参数调控（内容组件，无 toggle 按钮）

import React from "react";
import { SliderControl } from "../components/SliderControl";
import {
    shopMaxCost$, leisureMaxCost$,
    setShopMaxCost, setLeisureMaxCost,
    emergencyMaxCost$, findJobMultiplier$, findHomeMaxCost$, findSchoolElemMaxCost$,
    setEmergencyMaxCost, setFindJobMultiplier, setFindHomeMaxCost, setFindSchoolElemMaxCost,
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
                label={t("shoppingLabel")}
                binding={shopMaxCost$}
                commit={setShopMaxCost}
                min={1000} max={200000} step={1000}
                unit=""
                tooltip={t("shopMax")}
            />
            <SliderControl
                label={t("leisureLabel")}
                binding={leisureMaxCost$}
                commit={setLeisureMaxCost}
                min={1000} max={200000} step={1000}
                unit=""
                tooltip={t("leisureMax")}
            />

            <div className={styles.sectionDivider} />

            <SliderControl
                label={t("emergencyLabel")}
                binding={emergencyMaxCost$}
                commit={setEmergencyMaxCost}
                min={1000} max={17000} step={500}
                unit=""
                tooltip={t("emergencyMax")}
            />
            <SliderControl
                label={t("findJobLabel")}
                binding={findJobMultiplier$}
                commit={setFindJobMultiplier}
                min={1.0} max={20.0} step={0.5}
                unit="×"
                tooltip={t("findJobMax")}
                displayTransform={(v: number) => `~${(v * 14025 / 10000).toFixed(1)} km`}
            />
            <SliderControl
                label={t("findHomeLabel")}
                binding={findHomeMaxCost$}
                commit={setFindHomeMaxCost}
                min={17000} max={200000} step={1000}
                unit=""
                tooltip={t("findHomeMax")}
            />
            <SliderControl
                label={t("findSchoolLabel")}
                binding={findSchoolElemMaxCost$}
                commit={setFindSchoolElemMaxCost}
                min={1000} max={200000} step={1000}
                unit=""
                tooltip={t("findSchoolMax")}
            />

            <button
                className={styles.resetButton}
                onClick={resetPathfinding}
            >
                {t("reset")}
            </button>

            <div className={styles.optionHint}>
                {t("pathfindingOptionHint")}
            </div>
        </div>
    );
};
