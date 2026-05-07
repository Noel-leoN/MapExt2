import { ModRegistrar } from "cs2/modding";
import { MapExtButton } from "components/MapExtButton";

const register: ModRegistrar = (moduleRegistry) => {
    // 注入游戏左上角 HUD 区域
    moduleRegistry.append('GameTopLeft', MapExtButton);
}

export default register;