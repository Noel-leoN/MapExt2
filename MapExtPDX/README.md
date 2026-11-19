# Map Extended Mod (28/43/57 km + vanilla) — Economy Rework, In-Game Tools and Performance

* **Larger Maps:** 57km (default), 28km, an experimental 43km mode and a 114km mode (not recommended), alongside the vanilla 14km size.
* **Economy and Performance:** Reworked demand, job and home search, and consumption to ease pathfinding load at large populations, adjustable in game.
* **In-Game Tools:** HUD dashboard with key economic metrics, sea level and water tools, through-traffic and pet controls, editor collision override and heightmap export.
* **Safeguards:** Every load checks the map size mode first, and conflicting mods are detected at startup.
* **Map Extension Conversion (Optional):** Move a vanilla 14km city or map, or a 28km city, onto a larger map.

> **🧰 MapExTools (optional companion app):** A separate Windows app — switch MapExt's map size mode and economy groups while the game is closed, set launch options, read the game and MapExt logs, and inspect saves and heightmaps. It is not included in this mod; download it from Releases via the GitHub icon on the right side of this page.

---

## 🚀 Getting Started

The normal way to play is to start a new city on a large map built for your chosen size — either one you make yourself or one another player published.

**Staying on 57km (the default):** nothing to configure. Just load a 57km map and start building.

**Switching to another size** (28km, 43km, 114km, or vanilla 14km):

1. In the **Main Menu**, open Options -> MapExt -> **MapSize** tab, and pick the size under **"► Select MapSize Mode"**.
2. Click **"► Apply Changes"**.
3. **Completely restart the game.** Hot-switching is not supported — loading a save before restarting will corrupt the simulation.
4. After restarting, load a map built for that size and start your city.

Maps for each size can be made yourself (see "Making a 1:1 Map" below) or subscribed from other players.

> **⚠️ Sizes must match**: if the map does not match the selected mode, terrain is sampled at the wrong scale. The mod checks before every load (Load, Continue, New Game, quick load) and stops a mismatched save or map — follow the prompt and switch to the correct mode.

---

## 🗺️ Map Extension Conversion (Optional)

An **optional extra** for moving to a larger map without starting over. You do not need it to start a new city on a map built for your mode — see "Getting Started" above.

Three sources can be converted to 28km or 57km (43km and 114km are not supported):

* **Vanilla 14km save** → 28km or 57km: all 529 tiles are unlocked.
* **New city on a vanilla 14km map** → 28km or 57km: only the central 9 tiles start unlocked and the rest can be bought as usual; starting tiles set by the map author are not kept.
* **28km save** → 57km: the city stays in the center of the new map at the same position; terrain is resampled to 14m per pixel.

The original save or map is never modified. The result is saved as `{Name}_MapExt{Mode}`.

### 🛠️ Steps

1. In the **Main Menu**, open Options -> MapExt -> MapSize tab, select the target mode (28km or 57km), click **"► Apply Changes"**, and **restart the game**.
2. Enable **"⚠ Map Extension Conversion (EXPERIMENTAL)"** in the MapSize tab.
3. **Load** the save, or start a new city on a vanilla map.
4. Click **"Extend and Load"** (**"Extend and Start"** for a new city). The mod synthesizes the extended terrain (the map's background terrain fills the new area; 28km saves without one get flat land with straight channels to the open sea), keeps natural resources and groundwater in the center (28km saves also keep pollution and soil water), clears vehicles and residents from saves, resets the water to the sea level, and saves the result.
5. **Restart the game** before playing the new save, so the water simulation and map bounds start cleanly.
6. After restarting:
   * **Outside Connections** stay at the old border, so trade and immigration continue. To have traffic enter from the new map edges, add new ones there with developer mode (launch option `--developerMode`, then press Home in game to open the object placement panel) or a mod that can place outside connections in game. Do not remove all of the old ones: with none left, the game crashes when companies export goods.
   * **Water Sources and Sea Level**: existing water sources are kept (old-format river, lake and sea sources are converted, and old constant sea sources are replaced by the sea level); the completion message says if any had to be removed. Adjust the sea level with the built-in Water Tools (MAP/EXT button on HUD) or **Water Features**, which can also place new river or sea sources.

> **Without conversion**: if the option is off or you choose "Continue Anyway", a new city on a vanilla map is stretched to the selected size — slopes become flatter and natural resources are randomly generated.

### 🔗 Other Mods by the Author
* **[SimpleRadio](https://mods.paradoxplaza.com/mods/145663/Windows):** Drop music files into a folder and they show up as an in-game radio station. No configuration needed.
* **[SimpleBrush](https://mods.paradoxplaza.com/mods/145932/Windows):** Unlocks the hidden natural resource brushes and restores depleted resources with one click.

> **Note on EconomyEX:** Updates to the standalone [EconomyEX](https://mods.paradoxplaza.com/mods/137149/Windows) mod are paused — all of its economy and performance features have been migrated into **MapExt** (including full support for vanilla 14km maps). You do not need to install EconomyEX separately.
> The author's Paradox Mods page also hosts sample maps and save files you can use as a reference.

---

## ⚠️ Important: Read Before Use

* **Use at Your Own Risk:** This mod makes deep modifications to core game simulation systems.
* **Mod Compatibility:** Mods that also rework the economy or pathfinding may change the same systems. MapExt detects known ones at startup and turns off its overlapping feature groups for that session.
* **Backup Saves Frequently:** Always use "Save As" to keep backups. Never overwrite a MapExt save if the mod fails to load or is uninstalled.
* **Hardware Requirements:** 10GB+ VRAM recommended (the mod uses 1-2GB extra VRAM). If you experience crashes during loading, consider reducing custom assets.

---

## 💎 Features and Tools

### 📏 Map Size Modes
* **57km (Default):** 4x4 vanilla size (DEM-14m)
* **43km (Experimental):** 3x3 (DEM-10.5m)
* **28km:** 2x2 (DEM-7m)
* **114km (Not Recommended):** 8x8 (DEM-28m)
* **14km:** Vanilla 1x1 (DEM-3.5m)
* **Map Tiles:** Fixed at the vanilla 529-tile grid — each tile scales up with the map size rather than the tile count multiplying.
* **Terrain Precision:** Terrain sampling resolution decreases as map size increases. Coastlines on larger sizes exhibit noticeable jagged edges.

### 💡 Economy and Simulation
* **Demand Algorithm Rework:** Converts vanilla RCI demand to percentage-based scaling, normalizes industrial labor metrics, and stabilizes household rent affordability evaluations.
* **Pathfinding Distance Control:** Configurable maximum pathfinding cost per travel purpose (shopping, company procurement, leisure, job search, home search, emergency, and four school levels), plus candidate and seeker caps for home and leisure searches, to reduce CPU load.
* **NoDogs 2.0:** Three pet control modes (Disable OnStreet, Prevent New Generation, Purge All) with live pet statistics.
* **No Through-Traffic:** Disables through-traffic vehicle spawning to reduce transit routing pressure.
* **Ghost Vehicle Cleanup:** The base game leaves a car bought with no parking space nearby stranded without a parking lane, turning it into a permanent "ghost". Vehicle Purchase Rescue (Debug tab, default off) re-parks these near the owner's home, deletes orphans whose household is gone, and works through the backlog in existing saves in batches. Use the "Scan Ghost Vehicles" button to check your save first.

> **📖 EconomyEX Migration Note:** Standalone [EconomyEX](https://mods.paradoxplaza.com/mods/137149/Windows) is currently paused and all of its economy and land value features have been migrated into **MapExt** (including the 14km vanilla mode). All tunable sliders live directly in MapExt's own settings panel — no need to install EconomyEX (its old page still lists per-slider ranges and defaults for reference).

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
* **114km:** Heightmap 114,688m / Worldmap 458,752m

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

# 地图尺寸扩展模组（28/43/57 km 及原版尺寸）— 经济改良、游戏内工具与性能优化

* **更大的地图**：57km（默认）、28km、实验性的 43km 与不推荐的 114km 模式，也可使用原版 14km 尺寸。
* **经济与性能**：改写需求、求职找房与消费逻辑，缓解大人口规模下的寻路负担，可在游戏内调整。
* **游戏内工具**：HUD 经济数据面板、海平面与水体工具、过境交通与宠物控制、编辑器碰撞跳过与高度图导出。
* **防错机制**：每次载入前先检查地图尺寸模式，启动时识别冲突模组。
* **地图扩展转换（可选）**：把原版 14km 的城市或地图、或 28km 的城市迁到更大的地图上。

> **🧰 MapExTools（可选配套工具）**：独立的 Windows 程序——游戏关闭时切换 MapExt 的地图尺寸模式与经济分组，设置游戏启动参数，查看游戏与 MapExt 日志，检视存档与高度图。它不在本模组包内，请点击本页面右侧的 GitHub 图标，在 Releases 中下载。

---

## 🚀 开始使用

本模组的主线用法是**在大地图上从零建城**：载入一张对应尺寸的地图即可开始。

**沿用 57km（默认模式）**：无需任何设置，直接载入 57km 地图开始建城。

**改用其他尺寸**（28km / 43km / 114km / 原版 14km）：

1. 在**主菜单**打开 选项 -> MapExt -> **MapSize** 标签页，用 **“► Select MapSize Mode”** 选择目标尺寸。
2. 点击 **“► Apply Changes”** 应用。
3. **完全重启游戏。** 本模组不支持热切换——未重启就载入存档会造成模拟逻辑冲突并可能损坏存档。
4. 重启后载入一张**与所选尺寸相符**的地图，即可开始建城。

各尺寸的地图可以自己制作（见下方“制作 1:1 地图”），也可以订阅其他玩家发布的对应尺寸地图。

> **⚠️ 尺寸必须匹配**：地图与所选模式的尺寸不一致时，地形会按错误比例采样。模组会在每次载入前检查（读档、继续、开新城、快速读档），并拦下尺寸不符的存档或地图，请依提示切换到正确的模式。

---

## 🗺️ 地图扩展转换（可选）

这是一项**可选的附加功能**，供不想从零开始、希望迁往大地图的玩家使用。在对应尺寸的地图上开新城不需要它——见上方“开始使用”。

可转换三种来源，目标为 28km 或 57km（不支持 43km 与 114km）：

* **14km 原版存档** → 28km 或 57km：解锁全部 529 块瓦片。
* **用 14km 原版地图开新城** → 28km 或 57km：只解锁中央 9 块，其余照常购买；地图作者设定的起始瓦片不保留。
* **28km 存档** → 57km：城市留在新地图中央、位置不变；地形按每像素 14m 重新采样。

原始存档或地图不会被修改，结果另存为 `{名称}_MapExt{模式}`。

### 🛠️ 操作步骤

1. 在**主菜单**的 Options 中打开 MapExt 选项，在 MapSize 界面中选择目标模式（28km 或 57km），点击 **“► Apply Changes”** 应用，然后**完全重启游戏**。
2. 在 MapSize 标签页开启 **“⚠ 地图扩展转换（实验性）”**。
3. **加载**存档，或用原版地图开新城。
4. 在确认对话框中点击 **“扩展并加载”**（开新城为 **“扩展并开始”**）。模组将合成扩展地形（新增区域取地图的背景地形；没有背景地形的 28km 存档为平地，并开出通往外海的直水道），把自然资源与地下水保留在中央（28km 存档另保留污染与土壤水），清除存档中的车辆与居民，把水体重置到海平面，并保存结果。
5. **再次完全重启游戏**后再游玩新存档，以便水体模拟与地图边界重新初始化。
6. 重启后：
   * **外部连接**保留在原边界，贸易与迁入照常。若想让车流从新地图边缘进出，可用开发者模式（启动参数 `--developerMode`，游戏内按 Home 键打开物件摆放面板）或能在游戏内放置外部连接的模组，在新边缘添加。请勿删光旧的外部连接：一个都不剩时，公司出口货物会导致游戏闪退。
   * **水源与海平面**：原有水源会保留（旧版的河流、湖泊与海水水源会换算，旧式恒定海源由海平面取代）；若有水源不得不删除，完成提示会说明。可用内置水体工具（HUD 上的 MAP/EXT 按钮）或 **Water Features** 调整海平面，后者也能放置新的河流或海洋水源。

> **不转换时**：选项关闭或选择“仍要继续”时，用原版地图开的新城会按所选尺寸拉伸——坡度变缓，自然资源随机生成。

### 🔗 作者的其他模组
* **[SimpleRadio](https://mods.paradoxplaza.com/mods/145663/Windows)：** 将音乐文件放入文件夹即可成为游戏内电台，无需任何配置。
* **[SimpleBrush](https://mods.paradoxplaza.com/mods/145932/Windows)：** 解锁游戏内置的自然资源笔刷，并可一键恢复已耗尽的资源。

> **关于 EconomyEX**：独立版 [EconomyEX](https://mods.paradoxplaza.com/mods/137149/Windows) 已暂停更新，其全部经济与性能优化功能均已迁移整合至 **MapExt**（含原版 14km 地图模式），无需单独安装。
> 作者的 Paradox Mods 主页另有示范地图与存档，可供参考。

---

## ⚠️ 必读注意事项

* **风险自负**：本模组深度修改游戏底层模拟系统。
* **模组兼容性**：同样改动经济或寻路的模组可能替换相同的系统。本模组启动时会识别已知的这类模组，并在本次游戏中停用重叠的功能分组。
* **勤备份存档**：务必使用“另存为”保存副本。切勿在模组未生效或卸载后覆盖 MapExt 存档。
* **硬件要求**：建议显存 10GB 以上（模组额外占用 1-2GB 显存）。若加载时遇到显存不足，请精简自定义资产。

---

## 💎 功能特性与工具

### 📏 地图尺寸模式
* **57km（默认）**：4x4 原版尺寸（DEM-14m）
* **43km（实验性）**：3x3（DEM-10.5m）
* **28km**：2x2（DEM-7m）
* **114km（不推荐）**：8x8（DEM-28m）
* **14km**：原版 1x1（DEM-3.5m）
* **地图瓦片**：固定为原版 529 块可解锁瓦片——每块瓦片随地图尺寸等比放大，而非瓦片数量增殖。
* **地形精度**：地图尺寸越大，地形采样精度相对降低。大尺寸边缘海岸线存在较多锯齿。

### 💡 经济与模拟
* **需求算法调整**：将原版 RCI 需求改为百分比制，标准化工业劳动力指标，并调整家庭租金承受力评估机制。
* **寻路距离控制**：按出行目的（购物、企业采购、休闲、求职、找房、急救及四级就学）分别配置最大寻路成本上限，并可限制找房与休闲的候选数量，降低 CPU 寻路负载。
* **NoDogs 2.0**：三档宠物控制模式（禁止外出、阻止新生成、清除全部），提供实时宠物统计。
* **过境交通控制**：禁止过境交通车辆生成，降低道路寻路计算量。
* **幽灵车清理**：原版在购车时若附近无停车位，车辆不会被分配车道，从此成为永久“幽灵车”。购车救援（调试面板，默认关闭）将其移至车主住宅附近重新停放，家庭已消失的孤儿车则直接删除；存档中的存量幽灵车分批处理。可先用“扫描幽灵车”按钮查看存档内数量。

> **📖 EconomyEX 功能迁移说明**：独立版 [EconomyEX](https://mods.paradoxplaza.com/mods/137149/Windows) 已暂停更新，其全部经济与地价改动均已迁移至 **MapExt** 统一维护（含原版 14km 模式）。所有可调滑块均在 MapExt 的设置面板中提供，无需再安装 EconomyEX（各滑块的范围与默认值说明仍可参阅旧页）。

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
* **114km**：高程图 114,688m / 世界贴图 458,752m

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

