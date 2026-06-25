// SliderControl — Coherent Gameface 兼容版自定义滑块 (v3)
// 使用 mousedown + document mousemove/mouseup 实现拖拽
// 不使用 PointerEvent / setPointerCapture（Gameface 不支持）

import { useValue } from "cs2/api";
import { ValueBinding } from "cs2/api";
import React, { useState, useCallback, useRef, useEffect } from "react";
import { Tooltip } from "cs2/ui";
import styles from "../mapext.module.scss";

export interface SliderControlProps {
    label: string;
    binding: ValueBinding<number>;
    commit: (v: number) => void;
    min: number;
    max: number;
    step: number;
    unit?: string;
    tooltip?: string;
    /** 可選：將數值轉換為額外顯示文字（如距離 km），顯示在值后方 */
    displayTransform?: (value: number) => string;
}

/** 根据 step 精度格式化显示值，避免 IEEE 754 浮点溢出 */
function formatValue(value: number, step: number): string {
    if (step >= 1) return Math.round(value).toString();
    const decimals = Math.max(0, Math.ceil(-Math.log10(step)));
    return value.toFixed(decimals);
}

export const SliderControl: React.FC<SliderControlProps> = ({
    label, binding, commit, min, max, step, unit = "%", tooltip, displayTransform
}) => {
    const serverValue = useValue(binding);
    const [localValue, setLocalValue] = useState<number | null>(null);
    const trackRef = useRef<HTMLDivElement>(null);
    const draggingRef = useRef(false);
    const latestValueRef = useRef<number | null>(null);

    // 跟随服务器值（非拖拽时）
    useEffect(() => {
        if (!draggingRef.current) {
            setLocalValue(null);
        }
    }, [serverValue]);

    const displayValue = localValue !== null ? localValue : serverValue;
    const ratio = Math.max(0, Math.min(1, (displayValue - min) / (max - min)));

    // 鼠标位置 → 值
    const posToValue = useCallback((clientX: number): number => {
        const track = trackRef.current;
        if (!track) return min;
        const rect = track.getBoundingClientRect();
        const pct = Math.max(0, Math.min(1, (clientX - rect.left) / rect.width));
        const raw = min + pct * (max - min);
        return Math.round(raw / step) * step;
    }, [min, max, step]);

    // mousedown：开始拖拽，绑定 document 级事件
    const handleMouseDown = useCallback((e: React.MouseEvent) => {
        e.preventDefault();
        e.stopPropagation();
        draggingRef.current = true;

        const v = posToValue(e.clientX);
        setLocalValue(v);
        latestValueRef.current = v;

        const onMouseMove = (ev: MouseEvent) => {
            if (!draggingRef.current) return;
            const val = posToValue(ev.clientX);
            setLocalValue(val);
            latestValueRef.current = val;
        };

        const onMouseUp = () => {
            draggingRef.current = false;
            document.removeEventListener("mousemove", onMouseMove);
            document.removeEventListener("mouseup", onMouseUp);

            if (latestValueRef.current !== null) {
                commit(latestValueRef.current);
                setLocalValue(null);
                latestValueRef.current = null;
            }
        };

        document.addEventListener("mousemove", onMouseMove);
        document.addEventListener("mouseup", onMouseUp);
    }, [posToValue, commit]);

    return (
        <div className={styles.sliderRow}>
            {tooltip ? (
                <Tooltip tooltip={tooltip}>
                    <span className={styles.sliderLabel}>{label}</span>
                </Tooltip>
            ) : (
                <span className={styles.sliderLabel}>{label}</span>
            )}
            <div
                className={styles.sliderTrackWrap}
                onMouseDown={handleMouseDown}
            >
                <div ref={trackRef} className={styles.sliderTrack}>
                    <div
                        className={styles.sliderFill}
                        style={{ width: `${ratio * 100}%` }}
                    />
                    <div
                        className={styles.sliderThumb}
                        style={{ left: `${ratio * 100}%` }}
                    />
                </div>
            </div>
            <span className={styles.sliderValue}>
                {formatValue(displayValue, step)}{unit}
                {displayTransform && <span className={styles.sliderSubValue}> {displayTransform(displayValue)}</span>}
            </span>
        </div>
    );
};
