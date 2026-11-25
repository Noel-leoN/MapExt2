import { ModRegistrar } from "cs2/modding";
import { preloadImages } from "cs2/utils";
import { MapExtButton } from "components/MapExtButton";
import mapIcon from "./assets/map-icon.svg";

try {
    // 预取 coui://ui-mods/ 图标，避免首次显示时逐 UI Mod 目录异步请求造成 1+ 帧闪烁
    preloadImages([mapIcon]);
} catch (err) {
    console.warn("[MapExt2] Failed to preload UI images:", err);
}

const register: ModRegistrar = (moduleRegistry) => {
    try {
        // 注入游戏左上角 HUD 区域（包裹 try/catch 防止异常阻断后续其他 Mod 的 registrar）
        moduleRegistry.append("GameTopLeft", MapExtButton);
    } catch (err) {
        console.error("[MapExt2] Failed to register UI module:", err);
    }
};

export default register;
