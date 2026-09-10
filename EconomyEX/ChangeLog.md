## v4.8.0 - Vehicle Rescue Reliability, Large Map Warning and Second Load Fixes

* **[Map Size]:** Loading a large map save without MapExt installed now shows a warning dialog instead of only writing a line to the log.
* **[Fix]:** Reworked how the vehicle rescue system cleans up stranded vehicles, addressing an autosave failure reported on a large save with the system enabled.
* **[Fix]:** The vehicle rescue system now handles stranded vehicles in batches instead of processing over a thousand of them in a single frame when loading an older save.
* **[Fix]:** Loading a second save within the same session could make the vehicle rescue system act on unrelated vehicles.
* **[Fix]:** After loading a second save in the same session, the vanilla economy systems ran alongside the replaced ones, and the pet spawning and through-traffic settings reverted to vanilla behaviour.
* **[Fix]:** The settings page stayed on "IDLE: Waiting for map load..." when the first map loaded in a session was a large map.
* **[Performance]:** The vehicle rescue system now scans off the main thread, so looking for stranded vehicles no longer causes a brief stall.

---

### 主要变动

* **[地图尺寸]：** 在未安装 MapExt 的情况下加载大地图存档时会显示警告对话框，不再只在日志写入一行。
* **[修复]：** 重写车辆救援系统清理滞留车辆的方式，处理某大型存档在启用该系统时自动保存失败的问题。
* **[修复]：** 车辆救援系统改为分批处理滞留车辆，加载旧存档时不再于单一帧内处理上千辆。
* **[修复]：** 同一游戏会话中加载第二个存档时，车辆救援系统可能对无关车辆执行操作。
* **[修复]：** 同一游戏会话中加载第二个存档后，原版经济系统会与替换版同时运行，且宠物生成与过境交通两项设置回退成原版行为。
* **[修复]：** 同一游戏会话中首个加载的地图为大地图时，设置页状态永远停在“IDLE: Waiting for map load...”。
* **[性能]：** 车辆救援系统的扫描改于主线程外进行，寻找滞留车辆时不再造成短暂卡顿。
