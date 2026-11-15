import { ModRegistrar } from "cs2/modding";
import { MapExtButton } from "components/MapExtButton";

const register: ModRegistrar = (moduleRegistry) => {
    try {
        // 注入游戏左上角 HUD 区域（包裹 try/catch 防止异常阻断后续其他 Mod 的 registrar）
        moduleRegistry.append("GameTopLeft", MapExtButton);
    } catch (err) {
        console.error("[MapExt2] Failed to register UI module:", err);
    }
};

export default register;
