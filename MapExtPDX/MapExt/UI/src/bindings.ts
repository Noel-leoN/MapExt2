// MapExt2 UI Bindings — Phase 1
// 与 C# MapExtUISystem.cs 的 19 个 Binding 一一对应

import { bindValue, trigger } from "cs2/api";

const G = "mapext"; // 与 C# kGroup 一致

// === 面板状态 ===
export const panelOpen$    = bindValue<boolean>(G, "PanelOpen", false);
export const configOpen$   = bindValue<boolean>(G, "ConfigOpen", false);

export const setPanelOpen  = (v: boolean) => trigger(G, "SetPanelOpen", v);
export const setConfigOpen = (v: boolean) => trigger(G, "SetConfigOpen", v);

// === 只读信息 ===
export const mapSizeInfo$    = bindValue<string>(G, "MapSizeInfo", "N/A");
export const systemStatus$   = bindValue<string>(G, "SystemStatus", "N/A");

// === 租金核心参数 ===
export const rentMultRes$     = bindValue<number>(G, "RentMultRes", 100);
export const rentMultCom$     = bindValue<number>(G, "RentMultCom", 100);
export const rentMultInd$     = bindValue<number>(G, "RentMultInd", 100);
export const landValueEnv$    = bindValue<number>(G, "LandValueEnv", 40);
export const serviceBonus$    = bindValue<number>(G, "ServiceBonus", 100);

export const setRentMultRes   = (v: number) => trigger(G, "SetRentMultRes", v);
export const setRentMultCom   = (v: number) => trigger(G, "SetRentMultCom", v);
export const setRentMultInd   = (v: number) => trigger(G, "SetRentMultInd", v);
export const setLandValueEnv  = (v: number) => trigger(G, "SetLandValueEnv", v);
export const setServiceBonus  = (v: number) => trigger(G, "SetServiceBonus", v);

// === 寻路核心参数 ===
export const shopMaxCost$     = bindValue<number>(G, "ShopMaxCost", 8000);
export const leisureMaxCost$  = bindValue<number>(G, "LeisureMaxCost", 12000);

export const setShopMaxCost    = (v: number) => trigger(G, "SetShopMaxCost", v);
export const setLeisureMaxCost = (v: number) => trigger(G, "SetLeisureMaxCost", v);

// === 重置 ===
export const resetRentControl = () => trigger(G, "ResetRentControl");
export const resetPathfinding = () => trigger(G, "ResetPathfinding");
