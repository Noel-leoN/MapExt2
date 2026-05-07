// RentControlSection — 租金参数调控区域 (Gameface 兼容版)

import React, { useState } from "react";
import { SliderControl } from "../components/SliderControl";
import {
    rentMultRes$, rentMultCom$, rentMultInd$,
    landValueEnv$, serviceBonus$,
    setRentMultRes, setRentMultCom, setRentMultInd,
    setLandValueEnv, setServiceBonus,
    resetRentControl,
} from "../bindings";
import styles from "../mapext.module.scss";
import { useTranslation } from "../locales";

export const RentControlSection: React.FC = () => {
    const [isOpen, setIsOpen] = useState(false);
    const t = useTranslation();

    return (
        <>
            <button className={styles.configToggle} onClick={() => setIsOpen(!isOpen)}>
                <span>{isOpen ? "[-]" : "[+]"} {t("rentControlTitle")}</span>
            </button>

            {isOpen && (
                <div className={styles.section}>
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

                    <button
                        className={styles.resetButton}
                        onClick={resetRentControl}
                    >
                        {t("reset")}
                    </button>
                </div>
            )}
        </>
    );
};
