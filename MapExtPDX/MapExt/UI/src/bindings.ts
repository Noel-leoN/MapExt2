// MapExt2 UI Bindings — Phase 1 + Phase 2
// 与 C# MapExtUISystem.cs 的 43 个 Binding 一一对应

import { bindValue, trigger } from "cs2/api";

const G = "mapext"; // 与 C# kGroup 一致

// === 面板状态 ===
export const panelOpen$    = bindValue<boolean>(G, "PanelOpen", false);
export const configOpen$   = bindValue<boolean>(G, "ConfigOpen", false);
export const dashboardOpen$ = bindValue<boolean>(G, "DashboardOpen", false);

export const setPanelOpen  = (v: boolean) => trigger(G, "SetPanelOpen", v);
export const setConfigOpen = (v: boolean) => trigger(G, "SetConfigOpen", v);
export const setDashboardOpen = (v: boolean) => trigger(G, "SetDashboardOpen", v);

// === 只读信息 ===
export const mapSizeInfo$    = bindValue<string>(G, "MapSizeInfo", "N/A");
export const systemStatus$   = bindValue<string>(G, "SystemStatus", "N/A");

// === 租金核心参数 (Phase 1) ===
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

// === 寻路核心参数 (Phase 1) ===
export const shopMaxCost$     = bindValue<number>(G, "ShopMaxCost", 8000);
export const leisureMaxCost$  = bindValue<number>(G, "LeisureMaxCost", 12000);

export const setShopMaxCost    = (v: number) => trigger(G, "SetShopMaxCost", v);
export const setLeisureMaxCost = (v: number) => trigger(G, "SetLeisureMaxCost", v);

// === 扩展租金公式参数 (Phase 2: 6 value + 6 trigger) ===
export const lvFactorRes$    = bindValue<number>(G, "LvFactorRes", 100);
export const lvFactorCom$    = bindValue<number>(G, "LvFactorCom", 100);
export const lvFactorInd$    = bindValue<number>(G, "LvFactorInd", 100);
export const levelFactorRes$ = bindValue<number>(G, "LevelFactorRes", 100);
export const levelFactorCom$ = bindValue<number>(G, "LevelFactorCom", 100);
export const levelFactorInd$ = bindValue<number>(G, "LevelFactorInd", 100);

export const setLvFactorRes    = (v: number) => trigger(G, "SetLvFactorRes", v);
export const setLvFactorCom    = (v: number) => trigger(G, "SetLvFactorCom", v);
export const setLvFactorInd    = (v: number) => trigger(G, "SetLvFactorInd", v);
export const setLevelFactorRes = (v: number) => trigger(G, "SetLevelFactorRes", v);
export const setLevelFactorCom = (v: number) => trigger(G, "SetLevelFactorCom", v);
export const setLevelFactorInd = (v: number) => trigger(G, "SetLevelFactorInd", v);

// === Dashboard 只读指标 (Phase 2: 8 GetterValueBinding) ===
export const totalHouseholds$     = bindValue<number>(G, "TotalHouseholds", 0);
export const rentedHouseholds$    = bindValue<number>(G, "RentedHouseholds", 0);
export const homelessCount$       = bindValue<number>(G, "HomelessCount", 0);
export const movingAwayCount$     = bindValue<number>(G, "MovingAwayCount", 0);
export const seekerHousedCount$   = bindValue<number>(G, "SeekerHousedCount", 0);
export const seekerHomelessCount$ = bindValue<number>(G, "SeekerHomelessCount", 0);
export const highRentCount$       = bindValue<number>(G, "HighRentBuildingCount", 0);
export const petCount$            = bindValue<number>(G, "PetCount", 0);

// === UI 外观参数 ===
export const uiFontSize$    = bindValue<number>(G, "UIFontSize", 13);
export const setUIFontSize  = (v: number) => trigger(G, "SetUIFontSize", v);

// === 面板宽度持久化 ===
export const uiMenuWidth$     = bindValue<number>(G, "UIMenuWidth", 220);
export const uiDetailWidth$   = bindValue<number>(G, "UIDetailWidth", 260);
export const setUIMenuWidth   = (v: number) => trigger(G, "SetUIMenuWidth", v);
export const setUIDetailWidth = (v: number) => trigger(G, "SetUIDetailWidth", v);

// === 重置 ===
export const resetRentControl = () => trigger(G, "ResetRentControl");
export const resetPathfinding = () => trigger(G, "ResetPathfinding");
