// RentControlSection — 租金参数调控（内容组件，无 toggle 按钮）

import React from "react";
import { SliderControl } from "../components/SliderControl";
import {
    rentMultRes$, rentMultCom$, rentMultInd$,
    landValueEnv$, serviceBonus$,
    setRentMultRes, setRentMultCom, setRentMultInd,
    setLandValueEnv, setServiceBonus,
    lvFactorRes$, lvFactorCom$, lvFactorInd$,
    levelFactorRes$, levelFactorCom$, levelFactorInd$,
    setLvFactorRes, setLvFactorCom, setLvFactorInd,
    setLevelFactorRes, setLevelFactorCom, setLevelFactorInd,
    resetRentControl,
} from "../bindings";
import styles from "../mapext.module.scss";
import { useTranslation } from "../locales";

export const RentControlSection: React.FC = () => {
    const t = useTranslation();

    return (
        <div className={styles.detailContent}>
            <div className={styles.detailTitle}>{t("rentControlTitle")}</div>

            {/* === 租金总乘数 === */}
            <SliderControl
                label="Res. Rent"
                binding={rentMultRes$}
                commit={setRentMultRes}
                min={0} max={200} step={5}
                tooltip={t("rentRes")}
            />
            <SliderControl
                label="Com. Rent"
                binding={rentMultCom$}
                commit={setRentMultCom}
                min={0} max={200} step={5}
                tooltip={t("rentCom")}
            />
            <SliderControl
                label="Ind. Rent"
                binding={rentMultInd$}
                commit={setRentMultInd}
                min={0} max={200} step={5}
                tooltip={t("rentInd")}
            />

            <div className={styles.sectionDivider} />

            {/* === 地价因子 === */}
            <SliderControl
                label="Env. Factor"
                binding={landValueEnv$}
                commit={setLandValueEnv}
                min={0} max={100} step={5}
                tooltip={t("envFactor")}
            />
            <SliderControl
                label="Svc. Bonus"
                binding={serviceBonus$}
                commit={setServiceBonus}
                min={0} max={200} step={10}
                tooltip={t("svcBonus")}
            />

            <div className={styles.sectionDivider} />

            {/* === 地价贡献系数 === */}
            <SliderControl
                label="LV Res."
                binding={lvFactorRes$}
                commit={setLvFactorRes}
                min={0} max={200} step={5}
                tooltip={t("lvFactorRes")}
            />
            <SliderControl
                label="LV Com."
                binding={lvFactorCom$}
                commit={setLvFactorCom}
                min={0} max={200} step={5}
                tooltip={t("lvFactorCom")}
            />
            <SliderControl
                label="LV Ind."
                binding={lvFactorInd$}
                commit={setLvFactorInd}
                min={0} max={200} step={5}
                tooltip={t("lvFactorInd")}
            />

            <div className={styles.sectionDivider} />

            {/* === 等级贡献系数 === */}
            <SliderControl
                label="Lvl Res."
                binding={levelFactorRes$}
                commit={setLevelFactorRes}
                min={0} max={200} step={5}
                tooltip={t("levelFactorRes")}
            />
            <SliderControl
                label="Lvl Com."
                binding={levelFactorCom$}
                commit={setLevelFactorCom}
                min={0} max={200} step={5}
                tooltip={t("levelFactorCom")}
            />
            <SliderControl
                label="Lvl Ind."
                binding={levelFactorInd$}
                commit={setLevelFactorInd}
                min={0} max={200} step={5}
                tooltip={t("levelFactorInd")}
            />

            <button
                className={styles.resetButton}
                onClick={resetRentControl}
            >
                {t("reset")}
            </button>
        </div>
    );
};
