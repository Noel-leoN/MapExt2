## v4.12.0 - Load Safeguards, Map Extension Conversion and 114km Calibration

* **[Recommended Companion App — MapExTools v1.1 (Optional)]:** New standalone open-source Windows utility to edit all 65 MapExt settings offline, switch modes and local playsets, pre-check mod conflicts, skip the Paradox Launcher, read logs and inspect saves/heightmaps. Not bundled in this mod — download from [GitHub Releases](https://github.com/Noel-leoN/MapExt2/releases) (or via the GitHub icon on this page).
* **[Note]:** This release also includes the changes from v4.10.0 and v4.11.0, which were never published separately.
* **[Load Check and Save Guard]:** The map size check runs before loading on Load, Continue, quick load, New Game and the `startGame` launch option (maps made for another mode are stopped before starting; vanilla maps show a stretch warning). If deferred mod loading lets a mismatched save (or an extended save in a degraded session) load before MapExt initializes, MapExt warns after loading, pauses the simulation and blocks all saving until you return to the main menu.
* **[Map Extension]:** New: start a new city on a vanilla 14km map in 57km or 28km, and extend 28km saves to 57km. The conversion option is renamed "Map Extension Conversion (EXPERIMENTAL)".
* **[Map Extension]:** Vanilla save conversion now keeps outside connections at the old border instead of removing them.
* **[Map Extension]:** Fixed a second conversion failing in the same session, failed conversions being reported as successful, F5 saving over the original save, and the game not staying paused after a conversion.
* **[114km Maps]:** Pollution and other simulation grids now use vanilla cell sizes. Older 114km saves are converted on load (do not open 114km saves made with this version in older versions); saves of other sizes require no migration.
* **[Performance]:** Faster saving and loading of simulation grids, and shorter periodic spikes from the terrain attractiveness, air pollution and groundwater updates.
* **[Traffic]:** "No Through-Traffic" now also disables the paired random traffic dispatch system, stopping idle background ECS polling on existing saves.
* **[Housing]:** Fixed issues when the economy fix or the Household Property group is turned off: home search now weighs pollution and noise at the right location, rent changes no longer send households searching in batches, and the vanilla household systems no longer run alongside MapExt's replacements.
* **[Land Value]:** Fixed land value updates overlapping with readers of the same map; the heat map is now drawn right after loading when a save's land value map cannot be restored.
* **[Map Size Mode]:** Hot-switching while the game is running is removed for stability; clicking Apply now saves the selected mode and adds a prompt to quit the game so the new mode takes effect after a full restart.
* **[Compatibility and Diagnostics]:** Added runtime conflict attribution and low-frequency status checks, with a dialog when another mod conflicts with a critical CellMap system. Feature groups turned off for a conflicting mod stay off for the current session only without changing saved settings; turning off the Economy Fix master switch no longer auto-disables groups or reports startup conflicts. System status shows pending restarts and degraded states.
* **[UI]:** Prefixed MapExt UI icon assets to prevent icon collisions with other mods.
* **[EconomyEX]:** Updates to standalone EconomyEX are paused; all of its economy and performance features are now maintained inside MapExt (including the vanilla 14km mode).

---

### 主要变动

* **[推荐配套工具 — MapExTools v1.1（可选）]：** 全新独立的开源 Windows 桌面配套工具，可在游戏关闭时一站式编辑全部 65 项 MapExt 设置、切换地图尺寸模式与本地播放集、执行字节码冲突预检、跳过启动器直启游戏、诊断日志并检视存档与高度图。不在本模组包内，请前往 [GitHub Releases](https://github.com/Noel-leoN/MapExt2/releases)（或点击本页右侧 GitHub 图标）下载。
* **[提醒]：** 本次发版同时包含未单独发布的 v4.10.0 与 v4.11.0 的改动。
* **[读档校验与防误存保护]：** 读档、主菜单与启动器的“继续”、快速读档、开新城及启动参数 `startGame` 均会在常规载入前校验地图尺寸模式（不符地图在开城前拦下，原版地图提示拉伸）。若因推迟加载模组导致尺寸不符的存档（或降级态下的扩展存档）绕过前门进入城市，MapExt 会在载入后弹窗提醒、自动暂停模拟，并在返回主菜单前拦截所有手动、快速与自动存档。
* **[地图扩展]：** 新功能：57km 与 28km 下可用原版 14km 地图开新城，28km 存档可扩展到 57km。转换选项改名为“地图扩展转换（实验性）”。
* **[地图扩展]：** 原版存档转换改为把外部连接保留在原边界，不再删除。
* **[地图扩展]：** 修复同一次游戏中第二次转换失败、转换失败仍报成功、转换后按 F5 覆盖原存档，以及转换后没有保持暂停的问题。
* **[114km 地图]：** 污染等模拟网格改用原版格距。旧的 114km 存档读入时自动换算（用本版保存的 114km 存档请勿再用旧版打开）；其他尺寸存档无需迁移。
* **[性能]：** 模拟网格的存读档更快，地形吸引力、空气污染与地下水更新造成的周期性卡顿更短。
* **[过境交通]：** “禁止过境交通”现已一并停用配套的随机交通调度系统，彻底关闭旧存档残留请求导致的后台 ECS 空转轮询。
* **[住房]：** 修复关闭经济修复总开关或住房分组时的旧有问题：找房评分现按正确位置计算污染与噪音，租金变动不再让家庭成批找房，原版找房与租金调整系统也不再与 MapExt 的替换系统同时运行。
* **[地价]：** 修复地价图更新与读取重叠的问题；存档里的地价图读不回时，读档后立即绘出热力图。
* **[地图尺寸模式]：** 出于稳定性考虑不再支持运行中热切换；点击“应用设置”后现改为保存模式并新增退出游戏提示，完全重启后生效。
* **[兼容性与诊断]：** 新增运行期冲突归因与低频轮询检测，检测到严重 CellMap 冲突时弹出提示框；因冲突模组自动停用的分组仅在本次会话生效、不改写已保存设定；关闭经济修复总开关时不再自动停用分组或误报启动冲突；系统状态会注明待重启项与降级状态。
* **[界面]：** 为 MapExt 界面图标资源加上独立前缀，避免与其他模组图标同名覆盖。
* **[EconomyEX]：** 独立版 EconomyEX 已暂停更新，其全部经济与性能优化功能均已收归 MapExt 维护（含原版 14km 模式）。
