# Map Extended Mod (28/43/57/vanilla)

* **Extended Map Sizes:** Provides 28km and 57km map size modes, plus 43km as an experimental option. Tile count stays at the vanilla 529 — tiles scale up with the map instead of multiplying in number.
* **Vanilla Save Expansion:** Converts existing 14km vanilla saves to 28km or 57km maps, preserving buildings, terrain, and city layout (43km conversion is not supported).
* **Economy and Pathfinding Adjustments:** Reworks RCI demand, job and home search, and consumption mechanics to reduce pathfinding bottlenecks and CPU load at large populations. Details can be fine-tuned in the in-game UI.
* **Economic Data Dashboard:** In-game HUD dashboard provides a real-time overview of key economic metrics and population health.
* **In-Game Tools:** Includes sea level and water simulation speed control, through-traffic and pet management, editor collision override, and heightmap export.
* **Conflict Monitoring and Safeguards:** Runtime detection of incompatible mods, plus a load-time warning when a save's map size does not match the selected mode, avoiding terrain sampled at the wrong scale. Applies to both gameplay and Map Editor.

---

## 🗺️ Vanilla Map Extension

Converts existing 14km vanilla saves to 28km or 57km maps without starting over. Terrain, city layout, and placed buildings are preserved. The original save file is never modified.

### 🛠️ Steps to Extend an Existing Vanilla Save

1. In the **Main Menu**, open Options -> MapExt, navigate to the MapSize tab, and select the target mode (28km or 57km).
2. Enable **"Vanilla Map Extension"** in the MapSize tab.
3. **Load** an existing vanilla (14km) save file.
4. Click **"Extend and Load"** in the confirmation dialog. The mod unlocks 529 tiles, clears active vehicles and residents, removes outside connections, synthesizes extended terrain, preserves resources and groundwater, resets water simulation, and saves as `{SaveName}_MapExt{Mode}`.
5. **MUST restart the game** after conversion to allow the water simulation and map bounds to reinitialize cleanly.
6. After restart, complete:
   * **Rebuild Outside Connections** at the new map borders: Roads, Railways, Shipping Lanes, Airline Routes, Electricity, Water Supply.
   * **Place Water Sources and Adjust Sea Level**: Original water sources are cleared during conversion. Use the **Water Features** mod to place river/sea sources, and adjust sea level using the built-in Water Tools (MAP/EXT button on HUD) or Water Features.

> **⚠️ Important Notice**: This conversion is **strictly for existing vanilla saves**. Starting a new game on a vanilla map shows a warning but is not converted — automatic conversion at city creation is not supported yet. To start a new city on an extended map, select the desired MapSize mode in the main menu and load a custom map made for that size.

### 🔗 Standalone Version
* **[EconomyEX](https://mods.paradoxplaza.com/mods/137149/Windows):** Standalone economy and performance subset for vanilla-size (14km) maps. Automatically disables itself when MapExt is also installed.

---

## ⚠️ Important: Read Before Use

* **Use at Your Own Risk:** This mod makes deep modifications to core game simulation systems.
* **Mod Compatibility:** Strongly recommended to avoid using alongside other mods that overhaul economy or pathfinding mechanics to ensure simulation stability. MapExt includes a runtime conflict detection system that displays warnings and disables conflicting subsystems to mitigate crashes; however, it has an architectural conflict with **RealisticPathFinding** that cannot be automatically resolved — **do not install both simultaneously**.
* **Backup Saves Frequently:** Always use "Save As" to keep backups. Never overwrite a MapExt save if the mod fails to load or is uninstalled.
* **Match Map Sizes:** Always configure the map size matching your save in Options. Built-in failsafes apply to both normal gameplay and Map Editor modes.
* **Hardware Requirements:** 10GB+ VRAM recommended (the mod uses 1-2GB extra VRAM). If you experience crashes during loading, consider reducing custom assets.

---

## 💎 Features and Tools

### 📏 Map Size Modes
* **57km (Default):** 4x4 vanilla size (DEM-14m)
* **43km (Experimental):** 3x3 (DEM-10.5m)
* **28km:** 2x2 (DEM-7m)
* **14km:** Vanilla 1x1 (DEM-3.5m)
* **Map Tiles:** Fixed at the vanilla 529-tile grid — each tile scales up with the map size rather than the tile count multiplying.
* **Terrain Precision:** Terrain sampling resolution decreases as map size increases. Coastlines on larger sizes exhibit noticeable jagged edges.

### 💡 Economy and Simulation
* **Demand Algorithm Rework:** Converts vanilla RCI demand to percentage-based scaling, normalizes industrial labor metrics, and stabilizes household rent affordability evaluations.
* **Pathfinding Distance Control:** Configurable maximum pathfinding cost per travel purpose (shopping, company procurement, leisure, job search, home search, emergency, and four school levels), plus candidate and seeker caps for home and leisure searches, to reduce CPU load.
* **NoDogs 2.0:** Three pet control modes (Disable OnStreet, Prevent New Generation, Purge All) with live pet statistics.
* **No Through-Traffic:** Disables through-traffic vehicle spawning to reduce transit routing pressure.
* **Ghost Vehicle Cleanup:** The base game leaves a car bought with no parking space nearby stranded without a parking lane, turning it into a permanent "ghost". Vehicle Purchase Rescue (Debug tab, default off) re-parks these near the owner's home, deletes orphans whose household is gone, and works through the backlog in existing saves in batches. Use the "Scan Ghost Vehicles" button to check your save first.

### 🛠️ In-Game Tools and Performance
* **HUD Dashboard:** Click the **MAP/EXT** button on the in-game HUD to open the dashboard, featuring city statistics, rent control, and pathfinding sliders. Zero overhead when closed.
* **Heightmap Export:** Export the current city terrain from the Debug tab as a 16-bit grayscale PNG, mapped 1:1 for direct import into the Map Editor. Five orientations (Native, flip vertical, flip horizontal, rotate 180, or all four at once) plus optional raw output.
* **Water Tools:** Sea level control (0.1m precision), sea level apply, water simulation reset, and simulation speed control (0x-128x).
* **Editor Collision Override:** Bypass collision validation checks when placing objects in the Map Editor (Off / Trees Only / All Objects).
* **Disable World Backdrop:** Disables background terrain heightmap loading to reduce GPU and VRAM overhead.
* **Terrain-Water Optimization:** GPU buffer pre-allocation, building culling adjustments, and terrain cascade throttling.

---

## 🛠️ Usage

### 🗺️ Making a 1:1 Map

Import heightmaps and worldmaps corresponding to your chosen mode in the Map Editor:

* **28km:** Heightmap 28,672m / Worldmap 114,688m
* **43km:** Heightmap 43,008m / Worldmap 172,032m
* **57km:** Heightmap 57,344m / Worldmap 229,376m

* **Format:** 4096x4096 16-bit grayscale (PNG/TIFF).
* **Tip:** Importing a Worldmap is not recommended — it is purely visual and adds 1-2 GB VRAM overhead.

### 📂 Useful Directory Paths

* **Heightmaps:** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Heightmaps`
* **Overlays:** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Overlays`
* **Logs:** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs`
* **Local Mods:** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods`

### 🔗 Recommended Companion Mods

* **Skyve** — Playset management and save backup.
* **Image Overlay** — Overlay real-world maps for 1:1 city recreation.
* **Free Range Camera** — Unlock camera distance.
* **Water Features** — Water source placement for large maps.
* **529 Tiles** — Unlock all 529 tiles (the base game only permits 441) plus extra tile features.
* **Anarchy** — Remove placement restrictions.

---

## 🔍 Known Issues and Tips

* **Pathfinding Overhead at Large Populations:** At populations above 200k-500k, pathfinding volume increases significantly. Mod optimizations help mitigate lag, but system limits still apply.
* **Map Edge Floating-Point Precision:** Near extreme map edges, floating-point precision limits may cause terrain artifacts and simulation inaccuracies. It is recommended to keep core urban zones toward the center.
* **Fast Water Filling:** Use built-in Water Tools (HUD MAP/EXT button): set sea level, click "Apply", then increase simulation speed to 128x for rapid water filling. For water source placement and advanced features, pair with the **Water Features** mod.
* **Clear Mod Cache:** After a crash, open **Skyve** and click "Clear Mod Cache" to prevent potential save issues on next load.

---

## 🏆 Credits

* [CS Modding Discord](https://discord.gg/s6BcrFKepF) | [Cities 2 Modding Discord](https://discord.gg/ABrJqdZJNE)
* [CS2 Modding Instructions](https://github.com/rcav8tr/CS2-Modding-Instructions) by rcav8tr
* [BepInEx](https://github.com/BepInEx/BepInEx) | [Harmony](https://github.com/pardeike/Harmony)
* Special thanks to the following community members for their invaluable feedback, testing, and suggestions: Rebeccat, HideoKuze2501, Nulos, Jack the Stripper, Bbublegum/Blax, Sulley, krzychu124, and everyone else who helped!

---

# 地图尺寸扩展模组 (28/43/57/原版)

* **地图尺寸扩展**：提供 28km 与 57km 地图尺寸模式，另有 43km 作为实验性选项。瓦片数量维持原版 529 块——瓦片随地图等比放大，而非按数量增殖。
* **原版存档扩展**：支持将 14km 原版存档扩展至 28km 或 57km 地图，保留既有建筑、地形与路网（不支持 43km 扩展）。
* **经济与寻路调整**：重写 RCI 需求、求职找房与消费逻辑，缓解大人口规模下的寻路积压与 CPU 开销。可在游戏内 UI 调整细节。
* **城市经济数据快查**：游戏内 HUD 仪表盘提供关键经济指标与人口健康状态的实时速查。
* **游戏内工具**：提供海平面调节、水体模拟加速、过境交通与宠物控制、编辑器碰撞跳过，以及地形高度图导出。
* **冲突监控与防错**：内置运行时冲突检测；当存档尺寸与所选模式不符时于加载前警告，避免地形按错误比例采样。常规游戏与地图编辑器均生效。

---

## 🗺️ 原版地图扩展

支持将现有的 14km 原版城市存档扩展至 28km 或 57km 地图，无需从零建城。原有地形、城市布局与已放置建筑均会保留。原始存档不会被修改。

### 🛠️ 扩展原版城市游戏存档的操作步骤

1. 在**主菜单**的 Options 中打开 MapExt 选项，并在 MapSize 界面中选择目标模式（28km 或 57km）。
2. 在 MapSize 标签页中开启**“原版地图扩展”**。
3. **加载**已有原版（14km）存档。
4. 在确认对话框中点击**“扩展并加载”**。模组将自动解锁 529 瓦片、清除活跃车辆与居民、拆除旧外部连接、合成扩展高程、保留自然资源与地下水、重置水体，并另存为 `{存档名}_MapExt{模式}`。
5. **必须完全重启游戏**，以便水体物理与模拟边界重新初始化。
6. 重启后完成必要重建：
   * **重建外部连接**：在新的地图边界连接道路、铁路、航道、航线、电力与供水。
   * **放置水源与调节海平面**：原版水源已被清除。需使用 **Water Features** 模组放置河流/海洋水源，并使用内置水体工具（HUD 上的 MAP/EXT 按钮）或 Water Features 调整海平面。

> **⚠️ 注意事项与限制**：本功能**仅支持转换既有的原版存档**。目前不支持直接新建原版地图进行游玩或转换（开新城若选择原版地图虽有警告提示，但底层尚未支持自动转换）；若要建立新城市，请在主菜单切换至对应大地图模式后，直接载入该尺寸的地图。

### 🔗 独立经济子集
* **[EconomyEX](https://mods.paradoxplaza.com/mods/137149/Windows)：** 适用于原版地图（14km）的独立经济与性能子集。两者同时安装时 EconomyEX 会自动休眠。

---

## ⚠️ 必读注意事项

* **风险自负**：本模组深度修改游戏底层模拟系统。
* **模组兼容性**：强烈建议避免与其他修改经济或寻路机制的模组同时使用，以确保模拟稳定。本模组内置运行时冲突检测系统，可在检测到冲突时显示警告并自动休眠冲突子系统以防崩溃；但与 **RealisticPathFinding（真实寻路）** 存在底层机制与架构级冲突，无法自动调和，**不能同时安装**。
* **勤备份存档**：务必使用“另存为”保存副本。切勿在模组未生效或卸载后覆盖 MapExt 存档。
* **匹配地图尺寸**：选项面板中必须设置与存档匹配的尺寸。内置防错机制在常规游戏模式与地图编辑器中均生效。
* **硬件要求**：建议显存 10GB 以上（模组额外占用 1-2GB 显存）。若加载时遇到显存不足，请精简自定义资产。

---

## 💎 功能特性与工具

### 📏 地图尺寸模式
* **57km（默认）**：4x4 原版尺寸（DEM-14m）
* **43km（实验性）**：3x3（DEM-10.5m）
* **28km**：2x2（DEM-7m）
* **14km**：原版 1x1（DEM-3.5m）
* **地图瓦片**：固定为原版 529 块可解锁瓦片——每块瓦片随地图尺寸等比放大，而非瓦片数量增殖。
* **地形精度**：地图尺寸越大，地形采样精度相对降低。大尺寸边缘海岸线存在较多锯齿。

### 💡 经济与模拟
* **需求算法调整**：将原版 RCI 需求改为百分比制，标准化工业劳动力指标，并调整家庭租金承受力评估机制。
* **寻路距离控制**：按出行目的（购物、企业采购、休闲、求职、找房、急救及四级就学）分别配置最大寻路成本上限，并可限制找房与休闲的候选数量，降低 CPU 寻路负载。
* **NoDogs 2.0**：三档宠物控制模式（禁止外出、阻止新生成、清除全部），提供实时宠物统计。
* **过境交通控制**：禁止过境交通车辆生成，降低道路寻路计算量。
* **幽灵车清理**：原版在购车时若附近无停车位，车辆不会被分配车道，从此成为永久“幽灵车”。购车救援（调试面板，默认关闭）将其移至车主住宅附近重新停放，家庭已消失的孤儿车则直接删除；存档中的存量幽灵车分批处理。可先用“扫描幽灵车”按钮查看存档内数量。

### 🛠️ 游戏内工具与性能
* **HUD 仪表盘**：点击游戏内 HUD 的 **MAP/EXT** 按钮可展开面板，包含城市统计、租金调控与寻路参数。面板关闭时零开销。
* **高度图导出**：在调试面板中可将当前城市地形导出为 16-bit 灰度 PNG 高度图，地图像素与高度 1:1 映射，可直接导入游戏地图编辑器。提供五种方位（Native、垂直翻转、水平翻转、旋转 180、一次输出全部四份），并可另存原始 RAW。
* **水体工具**：海平面调节（0.1m 精度）、应用海平面、水体重置与模拟速度控制（0x-128x）。
* **编辑器碰撞跳过**：在地图编辑器中跳过物体放置碰撞检查（关闭 / 仅树木 / 所有物体）。
* **禁用背景世界地图**：阻止背景地形（Backdrop）加载，降低 GPU 与显存开销。
* **地形-水体优化**：GPU 缓冲预分配、建筑裁剪降频与地形级联降频。

---

## 🛠️ 用法

### 🗺️ 制作 1:1 地图

在地图编辑器中导入对应尺寸的高程图与世界贴图：

* **28km**：高程图 28,672m / 世界贴图 114,688m
* **43km**：高程图 43,008m / 世界贴图 172,032m
* **57km**：高程图 57,344m / 世界贴图 229,376m

* **格式**：4096x4096 16-bit 灰度图（PNG/TIFF）。
* **提示**：不建议导入 Worldmap，其仅为视觉装饰，会额外占用 1-2GB 显存。

### 📂 常用路径

* **高程图**：`%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Heightmaps`
* **覆盖层**：`%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Overlays`
* **日志**：`%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs`
* **本地模组**：`%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods`

### 🔗 推荐搭配模组

* **Skyve** — 播放集管理与存档备份。
* **Image Overlay** — 叠加真实地图参考。
* **Free Range Camera** — 解除相机视角限制。
* **Water Features** — 大地图水源放置。
* **529 Tiles** — 解锁全部 529 块瓦片（原版仅允许 441 块）并提供额外瓦片功能。
* **Anarchy** — 无碰撞建造。

---

## 🔍 已知问题与技巧

* **大人口寻路开销**：当人口达到 20 万至 50 万以上时，寻路计算量显著增加。模组的优化可缓解卡顿，但依然受硬件性能约束。
* **地图边缘浮点精度**：在地图极大边缘区域，浮点精度可能引起地形伪影和模拟不准确，建议核心城区尽量偏向中心建设。
* **快速水体填充**：在水体工具（HUD 上的 MAP/EXT 按钮）中设置海平面后点击“应用”，再将模拟速度调至 128x，可快速注入水体。若需水源生成和更多功能，请配合 **Water Features** 模组。
* **清理模组缓存**：若遇异常崩溃，可在 Skyve 中执行“清理模组缓存”。

---

## 🏆 致谢

* [CS Modding Discord](https://discord.gg/s6BcrFKepF) | [Cities 2 Modding Discord](https://discord.gg/ABrJqdZJNE)
* [CS2 Modding Instructions](https://github.com/rcav8tr/CS2-Modding-Instructions) by rcav8tr
* [BepInEx](https://github.com/BepInEx/BepInEx) | [Harmony](https://github.com/pardeike/Harmony)
* 感谢社区成员提供的测试、建议与反馈：Rebeccat, HideoKuze2501, Nulos, Jack the Stripper, Bbublegum/Blax, Sulley, krzychu124 以及所有提供帮助的玩家。

