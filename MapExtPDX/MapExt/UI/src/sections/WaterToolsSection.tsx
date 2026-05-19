// WaterToolsSection — 水体工具面板（Phase 5 P1: 海平面 + 模拟速度）
// v4.0.1: 修复浮点精度、分段滑块（0-4000m）、速度居中

import React, { useState, useCallback, useEffect } from "react";
import { useValue } from "cs2/api";
import {
    seaLevel$, setSeaLevel, applySeaLevel, resetWater,
    waterSimSpeed$, setWaterSimSpeed,
    seaLevelLocked$, setSeaLevelLocked,
} from "../bindings";
import { SliderControl } from "../components/SliderControl";
import styles from "../mapext.module.scss";
import { useTranslation, useIsZh } from "../locales";

// 2 的幂次方速度档位序列（与 Editor 一致）
const SPEED_STEPS = [0, 1, 2, 4, 8, 16, 32, 64, 128];

// === 分段滑块配置 ===
interface RangePreset {
    key: string;
    label: string;
    labelZh: string;
    min: number;
    max: number;
    step: number;
}

const RANGE_PRESETS: RangePreset[] = [
    { key: "0",  label: "0-500",     labelZh: "0-500",    min: 0,    max: 500,  step: 0.1 },
    { key: "1",  label: "500-1000",  labelZh: "500-1k",   min: 500,  max: 1000, step: 0.5 },
    { key: "2",  label: "1000-1500", labelZh: "1k-1.5k",  min: 1000, max: 1500, step: 0.5 },
    { key: "3",  label: "1500-2000", labelZh: "1.5k-2k",  min: 1500, max: 2000, step: 1.0 },
    { key: "4",  label: "2000-4000", labelZh: "2k-4k",    min: 2000, max: 4000, step: 2.0 },
];

/** 根据当前值自动选择最适合的段位 */
function detectRange(value: number): number {
    for (let i = RANGE_PRESETS.length - 1; i >= 0; i--) {
        if (value >= RANGE_PRESETS[i].min) return i;
    }
    return 0;
}

export const WaterToolsSection: React.FC = () => {
    const t = useTranslation();
    const isZh = useIsZh();
    const currentSeaLevel = useValue(seaLevel$);
    const currentSimSpeed = useValue(waterSimSpeed$);
    const isLocked = useValue(seaLevelLocked$);

    // 本地编辑态：仅在用户主动修改时使用，否则跟随引擎值
    const [localSeaLevel, setLocalSeaLevel] = useState(currentSeaLevel);
    const [isEditing, setIsEditing] = useState(false);
    // 分段选择器索引
    const [rangeIndex, setRangeIndex] = useState(() => detectRange(currentSeaLevel));

    // 引擎值变化时同步（非编辑态）
    useEffect(() => {
        if (!isEditing) {
            setLocalSeaLevel(currentSeaLevel);
            setRangeIndex(detectRange(currentSeaLevel));
        }
    }, [currentSeaLevel, isEditing]);

    const currentRange = RANGE_PRESETS[rangeIndex];

    // 切换段位
    const onRangeChange = useCallback((idx: number) => {
        setRangeIndex(idx);
        // 如果当前值不在新范围内，clamp 到最近的边界
        const range = RANGE_PRESETS[idx];
        setLocalSeaLevel(prev => {
            if (prev < range.min) return range.min;
            if (prev > range.max) return range.max;
            return prev;
        });
    }, []);

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

    // 精确输入框（不受分段范围限制，0-4000）
    const onInputChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
        const v = parseFloat(e.target.value);
        if (!isNaN(v)) {
            const clamped = Math.max(0, Math.min(4000, v));
            setLocalSeaLevel(clamped);
            setIsEditing(true);
            // 自动切换到对应段位
            setRangeIndex(detectRange(clamped));
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

            {/* 分段选择器 (8段，每段 500m) */}
            <div className={styles.waterRangeRow}>
                {RANGE_PRESETS.map((preset, idx) => (
                    <button
                        key={preset.key}
                        className={`${styles.waterRangeBtn} ${idx === rangeIndex ? styles.waterRangeBtnActive : ""}`}
                        onClick={() => onRangeChange(idx)}
                    >
                        {isZh ? preset.labelZh : preset.label}
                    </button>
                ))}
            </div>

            <SliderControl
                label={t("seaLevelLabel")}
                binding={seaLevel$}
                commit={onSeaLevelCommit}
                min={currentRange.min} max={currentRange.max} step={currentRange.step}
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
                    min="0"
                    max="4000"
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
                <button
                    className={`${styles.waterLockBtn} ${isLocked ? styles.waterLockBtnActive : ""}`}
                    onClick={() => setSeaLevelLocked(!isLocked)}
                    title={t("lockSeaLevelTip")}
                >
                    {isLocked ? "🔒" : "🔓"} {t("lockSeaLevel")}
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
