// ResizeHandle — 面板拖拽调整宽度组件
// 复用 SliderControl 的 document 级拖拽模式（Coherent Gameface 兼容）

import React, { useCallback, useRef } from "react";
import styles from "../mapext.module.scss";

interface ResizeHandleProps {
    /** 拖拽方向 */
    direction: "horizontal" | "vertical";
    /** 拖拽时回调，参数为鼠标移动的 delta 像素值 */
    onResize: (delta: number) => void;
    /** 拖拽结束回调，用于持久化最终宽度 */
    onResizeEnd?: () => void;
}

export const ResizeHandle: React.FC<ResizeHandleProps> = ({ direction, onResize, onResizeEnd }) => {
    const startRef = useRef(0);

    const handleMouseDown = useCallback((e: React.MouseEvent) => {
        e.preventDefault();
        e.stopPropagation();
        startRef.current = direction === "horizontal" ? e.clientX : e.clientY;

        const onMouseMove = (ev: MouseEvent) => {
            const current = direction === "horizontal" ? ev.clientX : ev.clientY;
            const delta = current - startRef.current;
            if (delta !== 0) {
                onResize(delta);
                startRef.current = current;
            }
        };

        const onMouseUp = () => {
            document.removeEventListener("mousemove", onMouseMove);
            document.removeEventListener("mouseup", onMouseUp);
            onResizeEnd?.();
        };

        document.addEventListener("mousemove", onMouseMove);
        document.addEventListener("mouseup", onMouseUp);
    }, [direction, onResize, onResizeEnd]);

    return (
        <div
            className={direction === "horizontal" ? styles.resizeHandleH : styles.resizeHandleV}
            onMouseDown={handleMouseDown}
        />
    );
};
