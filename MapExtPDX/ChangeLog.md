## v4.12.0 - Load Safeguards, Map Extension Conversion and 114km Calibration

* **[Note]:** This release also includes the changes from v4.10.0 and v4.11.0, which were never published separately.
* **[Load Check]:** The map size check now runs before loading on every way into a city: Load, Continue (main menu and launcher), quick load, starting a new city and the `startGame` launch option. Maps made for another mode are stopped before a new city starts; vanilla maps get a stretch warning.
* **[Map Extension]:** New: start a new city on a vanilla 14km map in 57km or 28km, and extend 28km saves to 57km. The conversion option is renamed "Map Extension Conversion (EXPERIMENTAL)".
* **[Map Extension]:** Vanilla save conversion now keeps outside connections at the old border instead of removing them.
* **[Map Extension]:** Fixed a second conversion failing in the same session, failed conversions being reported as successful, F5 saving over the original save, and the game not staying paused after a conversion.
* **[114km Maps]:** Pollution and other simulation grids now use vanilla cell sizes. Older 114km saves are converted on load; do not open 114km saves made with this version in older versions.
* **[Performance]:** Faster saving and loading of simulation grids, and shorter periodic spikes from the terrain attractiveness, air pollution and groundwater updates.
* **[Housing]:** With the economy fix or the Household Property group turned off, home search now weighs pollution and noise at the right location, rent changes no longer send households searching in batches, and the vanilla household systems no longer run alongside MapExt's replacements.
* **[Land Value]:** Fixed land value updates overlapping with readers of the same map; the heat map is now drawn right after loading when a save's land value map cannot be restored.
* **[Map Size Mode]:** The Apply button saves the selected mode and offers to quit; the new mode takes effect after a full restart.
* **[Settings]:** Feature groups turned off because of a conflicting mod stay off for the current session only, without changing your saved settings; the system status shows pending restarts, and a degraded state if a critical patch fails.
* **[Developer Tools]:** Added export buttons for the system registration table, CellMap checksums and CellMap statistics.
* **[Saves]:** Existing saves require no migration, except 114km saves as noted above.

---

### 主要变动

* **[提醒]：** 本次发版同时包含未单独发布的 v4.10.0 与 v4.11.0 的改动。
* **[读档校验]：** 读档、主菜单与启动器的“继续”、快速读档、开新城以及启动参数 `startGame`，都会在载入前检查地图尺寸模式。为其他模式制作的地图在开城前拦下，原版地图会给出拉伸提示。
* **[地图扩展]：** 新功能：57km 与 28km 下可用原版 14km 地图开新城，28km 存档可扩展到 57km。转换选项改名为“地图扩展转换（实验性）”。
* **[地图扩展]：** 原版存档转换改为把外部连接保留在原边界，不再删除。
* **[地图扩展]：** 修复同一次游戏中第二次转换失败、转换失败仍报成功、转换后按 F5 覆盖原存档，以及转换后没有保持暂停的问题。
* **[114km 地图]：** 污染等模拟网格改用原版格距。旧的 114km 存档读入时自动换算；用本版保存的 114km 存档请勿再用旧版打开。
* **[性能]：** 模拟网格的存读档更快，地形吸引力、空气污染与地下水更新造成的周期性卡顿更短。
* **[住房]：** 经济修复总开关或住房分组关闭时，找房评分按正确位置计算污染与噪音，租金变动不再让家庭成批找房，原版找房与租金调整系统也不再与 MapExt 的替换系统同时运行。
* **[地价]：** 修复地价图更新与读取重叠的问题；存档里的地价图读不回时，读档后立即绘出热力图。
* **[地图尺寸模式]：** 应用按钮保存所选模式并提供退出游戏的选项，新模式在完全重启后生效。
* **[设置]：** 因冲突 Mod 自动停用的功能分组只在本次游戏中停用，不改写已保存的设置；系统状态会注明待重启的设置，关键补丁失败时标为降级。
* **[开发者工具]：** 新增导出系统注册表、CellMap 校验和与 CellMap 统计的按钮。
* **[存档]：** 现有存档无需迁移，114km 存档见上文。
