## v4.8.0 - Heightmap Export, Load Validation, Save Reliability, Groundwater and Pollution Fixes

* **[Feature]:** Added a heightmap export button under the Debug tab that saves the current terrain as a 16-bit PNG the Map Editor can import directly.
* **[Map Size]:** Loading a map created in a different MapSize mode now shows a warning dialog instead of silently sampling terrain heights at the wrong scale.
* **[Change]:** Removed the experimental Water Async Compute option, which is incompatible with the water simulation and could leave water levels incorrect after terrain edits.
* **[Change]:** Redrew the in-game HUD button icon as a bolder two-line MAP/EXT mark.
* **[Fix]:** Reworked how the vehicle rescue system cleans up stranded vehicles, addressing an autosave failure reported on a large save with the system enabled.
* **[Fix]:** The vehicle rescue system now handles stranded vehicles in batches instead of processing over a thousand of them in a single frame when loading an older save.
* **[Fix]:** Loading a second save within the same session could make the vehicle rescue system act on unrelated vehicles.
* **[Fix]:** Noise pollution spread was not Burst-compiled and ran on the interpreted code path.
* **[Fix]:** Groundwater was missing entirely in the expanded map size modes, so groundwater pumps and wells had nothing to draw from. The aquifer painted by the map author is now restored at the expanded resolution on load, and saves that were left with an empty aquifer are repaired the same way. Saves that already have a working aquifer are left untouched.
* **[Fix]:** Groundwater updated at one sixteenth of the intended rate, so contamination did not decrease and depleted wells refilled slowly.
* **[Fix]:** In the vanilla map size mode, the reworked residential, commercial and industrial demand calculations were never applied.
* **[Fix]:** Turning on either the vanilla save conversion or the world backdrop option silently cleared the other while the settings screen still showed both as enabled; the two options are now mutually exclusive in the UI.
* **[Performance]:** Air pollution advection and the groundwater replenishment step now run in parallel, and the groundwater and ground pollution passes skip empty cells.
* **[Performance]:** The city statistics panel no longer counts buildings on the main thread.
* **[Performance]:** The vehicle rescue system now scans off the main thread, so looking for stranded vehicles no longer causes a brief stall.

---

### 主要变动

* **[功能]：** 在开发者选项新增高度图导出按钮，将当前地形输出为地图编辑器可直接导入的 16-bit PNG。
* **[地图尺寸]：** 加载以其他地图尺寸模式制作的地图时会显示警告对话框，不再静默地以错误比例采样地形高度。
* **[调整]：** 移除实验性的“水体 Async Compute”选项，该选项与水模拟不兼容，可能在地形编辑后留下错误的水位。
* **[调整]：** 将游戏内 HUD 按钮图标重绘为加粗双行 MAP/EXT。
* **[修复]：** 重写车辆救援系统清理滞留车辆的方式，处理某大型存档在启用该系统时自动保存失败的问题。
* **[修复]：** 车辆救援系统改为分批处理滞留车辆，加载旧存档时不再于单一帧内处理上千辆。
* **[修复]：** 同一游戏会话中加载第二个存档时，车辆救援系统可能对无关车辆执行操作。
* **[修复]：** 噪音污染扩散未经 Burst 编译，运行于解释路径。
* **[修复]：** 扩展地图尺寸模式下地下水完全不存在，导致地下水泵与水井无水可抽。现在加载时会按扩展后的分辨率还原地图作者绘制的含水层，先前保存下空含水层的存档也以同一方式修复；含水层已正常的存档不受影响。
* **[修复]：** 地下水的更新频率仅为预期的十六分之一，导致污染不会下降、抽干的水井回补缓慢。
* **[修复]：** 原版地图尺寸模式下，改良版的住宅／商业／工业需求计算从未生效。
* **[修复]：** 开启原版存档转换或背景地图选项时会静默清除另一项，而设置界面上两者仍显示为开启；两项现改为界面互斥。
* **[性能]：** 空气污染的平流与地下水的补充净化计算改为并行运算，地下水与地面污染的处理会跳过空白网格。
* **[性能]：** 城市统计面板不再于主线程逐一计算建筑。
* **[性能]：** 车辆救援系统的扫描改于主线程外进行，寻找滞留车辆时不再造成短暂卡顿。
