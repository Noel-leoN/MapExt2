// WaterToolsSection — 水体工具面板（Phase 5 P1: 海平面 + 模拟速度）

import React, { useState, useCallback, useEffect } from "react";
import { useValue } from "cs2/api";
import {
    seaLevel$, setSeaLevel, applySeaLevel, resetWater,
    waterSimSpeed$, setWaterSimSpeed,
} from "../bindings";
import { SliderControl } from "../components/SliderControl";
import styles from "../mapext.module.scss";
import { useTranslation } from "../locales";

// 2 的幂次方速度档位序列（与 Editor 一致）
const SPEED_STEPS = [0, 1, 2, 4, 8, 16, 32, 64, 128];

export const WaterToolsSection: React.FC = () => {
    const t = useTranslation();
    const currentSeaLevel = useValue(seaLevel$);
    const currentSimSpeed = useValue(waterSimSpeed$);

    // 本地编辑态：仅在用户主动修改时使用，否则跟随引擎值
    const [localSeaLevel, setLocalSeaLevel] = useState(currentSeaLevel);
    const [isEditing, setIsEditing] = useState(false);

    // 引擎值变化时同步（非编辑态）
    useEffect(() => {
        if (!isEditing) setLocalSeaLevel(currentSeaLevel);
    }, [currentSeaLevel, isEditing]);

    // 海平面滑块变更（实时预览，不立即应用）
    const onSeaLevelChange = useCallback((v: number) => {
        setIsEditing(true);
        setLocalSeaLevel(v);
    }, []);

    // 提交海平面值到引擎
    const onSeaLevelCommit = useCallback((v: number) => {
        setSeaLevel(v);
        setIsEditing(false);
    }, []);

    // 精确输入框
    const onInputChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
        const v = parseFloat(e.target.value);
        if (!isNaN(v)) {
            setLocalSeaLevel(v);
            setIsEditing(true);
        }
    }, []);

    const onInputBlur = useCallback(() => {
        setSeaLevel(localSeaLevel);
        setIsEditing(false);
    }, [localSeaLevel]);

    const onInputKeyDown = useCallback((e: React.KeyboardEvent) => {
        if (e.key === "Enter") {
            setSeaLevel(localSeaLevel);
            setIsEditing(false);
        }
    }, [localSeaLevel]);

    // 模拟速度 ▼/▲
    const speedDown = useCallback(() => {
        const idx = SPEED_STEPS.indexOf(currentSimSpeed);
        if (idx > 0) setWaterSimSpeed(SPEED_STEPS[idx - 1]);
        else if (idx === -1) {
            // 当前值不在标准序列中，找到最近的较小值
            for (let i = SPEED_STEPS.length - 1; i >= 0; i--) {
                if (SPEED_STEPS[i] < currentSimSpeed) { setWaterSimSpeed(SPEED_STEPS[i]); break; }
            }
        }
    }, [currentSimSpeed]);

    const speedUp = useCallback(() => {
        const idx = SPEED_STEPS.indexOf(currentSimSpeed);
        if (idx >= 0 && idx < SPEED_STEPS.length - 1) setWaterSimSpeed(SPEED_STEPS[idx + 1]);
        else if (idx === -1) {
            // 当前值不在标准序列中，找到最近的较大值
            for (let i = 0; i < SPEED_STEPS.length; i++) {
                if (SPEED_STEPS[i] > currentSimSpeed) { setWaterSimSpeed(SPEED_STEPS[i]); break; }
            }
        }
    }, [currentSimSpeed]);

    return (
        <div className={styles.detailContent}>
            <div className={styles.detailTitle}>{t("waterToolsTitle")}</div>

            {/* === 海平面控制 === */}
            <div className={styles.waterSubTitle}>{t("seaLevelLabel")}</div>

            <SliderControl
                label={t("seaLevelLabel")}
                binding={seaLevel$}
                commit={onSeaLevelCommit}
                min={0} max={800} step={0.1}
                unit="m"
                tooltip={t("seaLevelTip")}
            />

            {/* 精确输入行 */}
            <div className={styles.waterInputRow}>
                <span className={styles.waterInputLabel}>{t("seaLevelPrecise")}</span>
                <input
                    className={styles.waterInput}
                    type="number"
                    step="0.1"
                    value={localSeaLevel.toFixed(1)}
                    onChange={onInputChange}
                    onBlur={onInputBlur}
                    onKeyDown={onInputKeyDown}
                />
                <span className={styles.waterInputUnit}>m</span>
            </div>

            {/* 操作按钮行 */}
            <div className={styles.waterButtonRow}>
                <button className={styles.waterApplyBtn} onClick={applySeaLevel}>
                    {t("applySeaLevel")}
                </button>
                <button className={styles.waterResetBtn} onClick={resetWater}>
                    {t("resetWater")}
                </button>
            </div>

            <div className={styles.sectionDivider} />

            {/* === 水模拟速度 === */}
            <div className={styles.waterSubTitle}>{t("waterSimSpeedLabel")}</div>
            <div className={styles.waterSpeedRow}>
                <button
                    className={styles.waterSpeedBtn}
                    onClick={speedDown}
                    disabled={currentSimSpeed <= 0}
                >▼</button>
                <span className={styles.waterSpeedValue}>
                    {currentSimSpeed === 0 ? t("waterSimPaused") : `${currentSimSpeed}x`}
                </span>
                <button
                    className={styles.waterSpeedBtn}
                    onClick={speedUp}
                    disabled={currentSimSpeed >= 128}
                >▲</button>
            </div>
            <div className={styles.waterSpeedHint}>{t("waterSimSpeedHint")}</div>
        </div>
    );
};
