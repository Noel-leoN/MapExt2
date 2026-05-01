# 🚀 Cities Skylines 2 Map Extended Mod (28/57/114/vanilla) with Economy Fix and Performance Improvement

## 🚀 Update Notice

* **💎 Economy Fix and Performance Optimization (Experimental):** This version includes a suite of optimizations specifically tuned for mega-cities (millions of population). It refactors demand logic, land value, housing search, citizen behaviors, and rent systems.

* **⏳ Simulation Catch-up:** Loading an old save may result in extremely slow simulation initially as the new economic logic recalculates everything. Please allow the simulation to run for a while for performance to stabilize.

* **🌍 Resources Map Loss:** Due to architectural changes in the resource system, **ore, oil, and other natural resources may disappear from existing saves.** You will need to use the `ExtraLandScapingTool` mod to manually repaint them.

* **⚠️ Compatibility:** This mod fundamentally rewrites core economic logic. It features a **built-in Conflict Monitoring System** that uses a double-check mechanism to automatically detect and disable conflicting economy subsystems at runtime. This means mods like **Realistic Trip**, **Realistic PathFinding**, and similar economy/simulation mods can be installed alongside MapExt — the mod will automatically yield conflicting system groups to avoid crashes. However, if both mods modify the same underlying data, subtle simulation anomalies may still occur. You can check the current conflict status in the **EconomyEX** tab of the Option UI. Please report any anomalies.

* **🔗 Relationship with [EconomyEX](https://mods.paradoxplaza.com/mods/137149/Windows):** EconomyEX is a **standalone subset** of this mod's economy module, designed exclusively for vanilla-size (14km) maps. If you play on vanilla maps and don't need extended map sizes, you can use EconomyEX alone. **When both mods are installed, EconomyEX will automatically disable itself** to avoid conflicts. For detailed economy patch documentation, please refer to the [EconomyEX mod page](https://mods.paradoxplaza.com/mods/137149/Windows).

## ⚠️ IMPORTANT: READ BEFORE USE

* **🛡️ Use at your own risk:** This mod makes extensive modifications to the game engine. There may be unknown issues or conflicts with other mods.

* **💾 Backup your saves:** ALWAYS use "Save As" to create new save files. Do not directly overwrite existing saves. **Never** attempt to load and overwrite a save created with MapExt if the mod fails to load or is uninstalled.

* **📏 Match Map Sizes:** In the Option UI, you must select the precise map size (28km / 57km / 114km / Vanilla 14km) that matches your currently loaded map or save. ⚠️ Note: A failsafe prevents loading incorrect sizes for save games, but this does NOT apply to the Map Editor.

* **💻 Hardware Requirements:** A GPU with 10GB+ VRAM is recommended (this mod uses an additional 1-2GB of VRAM). If the game crashes while loading, you likely have too many custom assets. We strongly recommend creating a pristine, minimal Playset on your first use.

## 💎 Introduction

* **📏 Map Size Modes:**

  * **57km (Default):** 4x4 vanilla map dimensions (DEM-14m resolution).

  * **28km:** 2x2 dimensions (DEM-7m resolution).

  * **114km:** 8x8 dimensions (DEM-28m resolution). ❌ Not recommended due to extremely low terrain resolution, edge tearing, and simulation calculation errors.

  * **14km:** Vanilla 1x1 dimensions (DEM-3.5m resolution).

* **🏔️ Terrain Limitation:** As the map size expands, terrain resolution inevitably decreases. Coastlines and mountains may appear jagged and rough. Due to structural complexity and performance impacts, this mod currently cannot artificially improve terrain resolution. You can mitigate this visually by planting trees or using other objects to cover rough patches. For high-resolution massive maps, consider waiting for the upcoming `LargerMap` mod by algernon.

* **🧱 Map Tiles:** The number of unlockable map tiles remains fixed at **529**.

## 💡 Economy Patches and Performance Tools

This mod integrates a comprehensive set of economy patches and performance tools, all configurable via the in-game Option UI:

* **📊 Economy Overhaul (Beta):** Deep rewrites of RCI demand, job search, home search, rent calculation, consumer behavior, and resident AI pathfinding — optimized for mega-cities with millions of population. Each subsystem can be individually toggled on/off. A built-in **Conflict Monitoring System** with double-check mechanism automatically detects and disables conflicting subsystems if another mod re-enables the same vanilla systems.

* **📈 Pathfinding Max-Distance Control:** Configurable maximum pathfinding distance for each travel purpose — including shopping, leisure, job search, home search, school enrollment, and emergency services. On vanilla maps the engine hardcodes a 17000-cost cap, which causes vehicles and citizens to vanish mid-route on extended maps. Use the sliders in the **EconomyEX** tab to raise or lower these limits per purpose:
  * **Smaller maps (14km/28km):** Lower values (e.g. Shopping 8000, Leisure 8000–12000) keep citizens local and reduce CPU load.
  * **Bigger maps (57km/114km):** Raise values (e.g. Shopping 8000–12000, Leisure 12000–20000) so citizens can reach distant services.
  * **Cross-map activities (Find Job, Find Home, Company Delivery):** Recommended to max out (200000) to ensure isolated towns remain functional.

* **🐕 NoDogs 2.0:** An enhanced pet control suite with three operating modes — **Disable OnStreet** (prevents pets from appearing on streets), **Prevent New Generation** (blocks new pet spawning for incoming households), and **Purge All Existing** (removes all pet entities from the save for maximum performance gain). Includes a live pet count display. Each option must be explicitly applied via the Apply button.

* **🚗 No Through-Traffic:** Disables all through-traffic vehicle spawning, reducing pathfinding calculations and traffic congestion on extended maps.

* **🏗️ Editor Collision Override:** Bypass collision validation checks when placing objects in the Map Editor — supports three modes (Off / Trees Only / All Objects). Greatly speeds up tree planting on extended maps.

* **🏔️ Terrain-Water Optimization:** Includes GPU buffer pre-allocation, building cull throttling, terrain cascade throttling, and configurable water simulation quality levels — all adjustable in-game without restart.

## 🛠️ Usage

### 🗺️ Making a 1:1 Map

In the Map Editor, import a heightmap/worldmap image of the corresponding size:

* **28km Playable Area:** Heightmap 28,672m / Worldmap 114,688m

* **57km Playable Area:** Heightmap 57,344m / Worldmap 229,376m

* **114km Playable Area:** Heightmap 114,688m / Worldmap 458,752m

* **🖼️ Supported Image Formats:** 4096x4096 16-bit grayscale (PNG or TIFF).

* 💡 Note: You can import heightmaps with resolutions up to 14336x14336; the mod will automatically downsample/scale them to 4096x4096.

* **⚠️ Scaling Warning:** If the physical dimensions of your imported map do not mathematically match the target size, the terrain will stretch.

* **🚀 Performance Tip:** For 57km and 114km maps, do NOT import a "Worldmap" background image — it serves only as visual edge decoration with no gameplay benefit. On a 114km map, the worldmap texture alone can consume an extra **1–2 GB of VRAM** and noticeably reduce frame rates.

### 📂 Useful Directory Paths (Paste into File Explorer)

* **🗺️ Heightmaps and Worldmaps:** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Heightmaps`

* **🖼️ Overlay Maps:** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Overlays`

* **📝 Game Logs (for bug reporting):** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs`

* **🛠️ Local Mods Folder:** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods`

### 🔗 Recommended Companion Mods

* **🛰️ Skyve**: Essential for managing Playsets, backing up saves, and diagnosing issues.

* **📸 Image Overlay**: Allows overlaying a real-world map image in-game, perfect for recreating 1:1 real cities.

* **🎥 Free Range Camera**: Unlocks camera distance constraints.

* **💧 Water Features**: Provides better control over water sources — critical for extended custom maps.

* **🏢 529 Tiles**: Essential for purchasing and managing all map tiles on extended maps.

* **🏗️ Anarchy**: Removes placement restrictions.

* **📊 Demand Master Control**: Vanilla simulation often struggles (economic anomalies, pathfinding lag) when populations exceed 200k-500k. Tweaking demand directly can drastically help stability.

## 🔍 Known Issues

* **🚦 Vanilla Pathfinding Flaw:** In vanilla CS2, when populations exceed 200k–500k, pathfinding queue jams cause severe CPU bottlenecks and break the economic simulation. Extending the map makes these underlying issues more apparent. While this mod's built-in Economy and Performance optimizations are designed to significantly alleviate these issues, extreme scenarios may still challenge engine limits. Absolute stability at ultra-high populations cannot be guaranteed.

* **🏙️ City Planning Advice:** Stable growth is key. Avoid zoning massive residential blocks at once, don't rush to max out citizen happiness overnight, control your service coverage spread, and avoid spamming parks (or avoid parks entirely).

* **🗺️ Map Edge Glitches:** Due to engine floating-point precision limits, you may notice bizarre terrain height graphical glitches near the absolute edges of extended maps. It's best to build your core residential/commercial/industrial zones closer to the map center.

* **🔄 Legacy Version Upgrades:** If upgrading from an older unreleased version and playing on sizes other than 57km, manually check the "Debug devalidation" toggle in Option UI, confirm your map size, fully load your save, "Save As" a new file, and then disable the Debug toggle.

## 💡 Tips

* **💧 Quick Water Generation:** Since extended maps take extremely long to manually fill with water, we recommend Yenyang's **Water Features** mod. Enable experimental features, max out the "flowness" slider, and minimize evaporation for quick results. Alternatively, instantly fill the oceans by setting the Sea Level via the in-game Developer UI (though you still need physical water sources to maintain it over time).

* **🧹 Clear Mod Cache:** After the game crashes to desktop due to memory leaks or other issues, residual mod caches are very likely to be generated, which may cause save corruption on subsequent loads (this is not limited to this mod). If you see red error messages on the loading screen, immediately close the game completely, open **Skyve**, and perform a "Clear Mod Cache" operation.

* **📢 Bug Reporting and Support:** Please report issues or find more advanced tips in the Discord community linked below, or on GitHub.

## 🏆 Credits and Acknowledgements

* [Cities: Skylines Modding Discord](https://discord.gg/s6BcrFKepF) (Primary discussion hub)

* [Cities 2 Modding Discord](https://discord.gg/ABrJqdZJNE)

* [CS2 Modding Instructions](https://github.com/rcav8tr/CS2-Modding-Instructions) by rcav8tr

* [CS2 BepInEx Mod Template](https://github.com/Captain-Of-Coit/cities-skylines-2-mod-template) by Captain-Of-Coit

* [BepInEx](https://github.com/BepInEx/BepInEx)

* [Harmony](https://github.com/pardeike/Harmony)

* 🤝 Special thanks to our testers: Rebeccat, HideoKuze2501, Nulos, Jack the Stripper, Bbublegum/Blax, and many others!

---

## 🚀 v2.5.x 更新说明

* **💎 经济与性能深度优化（实验性）：** 模组内置了专为百万级人口大城市设计的经济与性能修复补丁。该功能从底层全面重构了各项区域需求、地价系统、居民找房逻辑、居民日常行为与交租计算等。

* **⚠️ 兼容性说明：** 本模组已内置 **冲突监控系统 (Conflict Monitoring System)**，采用"二次确认"机制，能够在运行时自动检测并禁用与其他 Mod 产生冲突的经济子系统组。因此，**Realistic Trip**、**Realistic PathFinding** 等同类系列 Mod 现在可以与 MapExt 共存使用——模组将自动让出冲突的系统组以避免崩溃。但若双方修改了相同的底层数据，仍可能出现细微的模拟异常。您可以在选项面板的 **EconomyEX** 标签页中查看当前冲突状态。如遇问题请反馈。

* **🔗 与 [EconomyEX](https://mods.paradoxplaza.com/mods/137149/Windows) 的关系：** EconomyEX 是本模组经济模块的 **独立子集**，仅适用于原版大小（14km）地图。如果您仅在原版地图上游玩且不需要扩展地图功能，可以单独使用 EconomyEX。**当两者同时安装时，EconomyEX 将自动休眠**以避免冲突。经济补丁的详细说明可参阅 [EconomyEX 模组页面](https://mods.paradoxplaza.com/mods/137149/Windows)。

## ⚠️ 必读注意事项

* **🛡️ 风险提示**：本模组对原版底层代码进行了大量深度修改，可能存在未知问题或与其他模组冲突，请自行承担风险。

* **💾 勤备份存档**：强烈建议养成每次**"另存为"新文档**的习惯，切勿直接覆盖旧存档。**绝对不要**在模组卸载或加载失败时，强行读取并覆盖 MapExt 存档, 否则会导致存档损坏。

* **📏 匹配地图尺寸**：在选项界面中，必须设置与当前游玩地图相匹配的绝对尺寸（28km、57km、114km 或 原版14km）。⚠️ 注：模组已内置防错机制，会阻止跨尺寸读取存档，但该机制**不适用于地图编辑器**。

* **💻 硬件建议**：推荐显存 10GB 以上（本模组将额外占用 1-2GB 显存）。若加载地图时崩溃，通常可能是因为加载了过多资产模组导致显存爆满。首次使用建议创建一个精简的 Playset 播放集，确认正常后再逐步添加资产。

## 💎 介绍

* **📏 地图尺寸模式列表：**

  * **57公里（默认）**：4x4 原版地图面积，DEM-14米分辨率。

  * **28公里**：2x2 原版地图面积，DEM-7米分辨率。

  * **114公里**：8x8 原版地图面积，DEM-28米分辨率。（极不推荐，地形分辨率过低，地图边缘会出现撕裂，且严重影响模拟计算）

  * **14公里**：原版 1x1 地图面积，DEM-3.5米分辨率。

* **🏔️ 地形精度限制：** 由于尺寸放大，地形细节不可避免地会降低，海岸线与山脉会显得相对粗糙。受限于复杂的技术原因与性能开销考量，本模组暂不提供地形分辨率无损提升方案。建议通过种树或其他造景手段进行视觉遮挡。如果追求高精度大地形，可以关注 algernon 正在开发的 `LargerMap` 模组。

* **🧱 地图瓦片：** 可解锁的地图瓦片总数量维持在 529 块。

## 💡 经济补丁与性能工具

本模组整合了一套完整的经济修复补丁与性能优化工具，均可在游戏内选项面板中配置：

* **📊 经济系统修复 (Beta)：** 从底层深度重构了 RCI 需求、求职匹配、找房搬家、租金计算、消费行为与居民AI寻路等核心逻辑——专为百万级人口巨型城市优化。各子系统支持独立开关。内置 **冲突监控系统**，采用"二次确认"机制，可自动检测并禁用与其他 Mod 冲突的子系统组。

* **📈 寻路最远距离控制：** 可针对每种出行目的分别配置最大寻路距离——涵盖购物、休闲、求职、找房、入学、急救服务等。原版引擎硬编码了 17000 的成本上限，在大地图中会导致市民和载具中途消失。在选项面板的 **EconomyEX** 标签页中拖动滑块即可调节：
  * **小地图（14km/28km）：** 使用较低值（如购物 8000、休闲 8000–12000），让市民就近活动，减少 CPU 开销。
  * **大地图（57km/114km）：** 适当提高（如购物 8000–12000、休闲 12000–20000），使市民能够到达远处的服务设施。
  * **跨图活动（求职、找房、公司货运）：** 建议拉满（200000），确保偏远城镇正常运转。

* **🐕 NoDogs 2.0：** 增强版宠物控制套件，三种操作模式 — **禁止外出**（阻止宠物上街，关闭生成、渲染与寻路）、**阻止新生成**（将新移民的宠物生成概率归零）、**清除所有存量**（移除存档中全部宠物实体，最大化性能提升）。含实时宠物数量统计。所有选项需点击"应用"按钮方可生效。

* **🚗 过境交通控制：** 禁止所有过境交通工具出现，降低大地图的寻路计算量与交通拥堵。

* **🏗️ 编辑器碰撞跳过：** 在地图编辑器放置物体时跳过碰撞验证——支持三档模式（关闭 / 仅树木 / 所有物体），极大提升大地图种树效率。

* **🏔️ 地形-水体性能优化：** 含 GPU 缓冲预分配、建筑裁剪降频、地形级联降频与可配置水模拟质量等级——均可在游戏中实时调节，无须重启。

## 🛠️ 用法

### 🗺️ 制作 1:1 比例地图

在地图编辑器（Editor）中，导入对应正确尺寸的高程图（Heightmap）与世界贴图（Worldmap）：

* **28 公里可玩区域**：高程图 28672 米 / 世界贴图 114688 米

* **57 公里可玩区域**：高程图 57344 米 / 世界贴图 229376 米

* **114公里可玩区域**：高程图 114688 米 / 世界贴图 458752 米

* **🖼️ 支持的图像格式**：4096x4096 16位灰度图（PNG 或 TIFF）。

* **⚠️ 拉伸警告**：若导入的现实尺寸与上述设定的比例不匹配，地形将自动拉伸。

* **🚀 性能提示**：对于 57km 及 114km 的地图，强烈建议不要导入只作背景板用的"假"世界贴图。世界贴图仅提供边缘视觉过渡，对游玩毫无物理作用。在 114km 地图上，仅世界贴图纹理就可额外消耗 **1–2 GB 显存**，并显著降低帧率。

### 📂 常用路径速查（可直接粘贴至文件资源管理器地址栏）

* **🗺️ 高程图与世界贴图**：`%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Heightmaps`

* **🖼️ 覆盖层地图 (配合 Image Overlay)**： `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Overlays`

* **📝 游戏日志 (用于报错排查)**： `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs`

* **🛠️ 本地模组文件夹 (仅限手动安装或高级调试，工坊模式请勿改动)**： `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods`

### 🔗 推荐搭配模组（感谢各位开源作者！）

* **🛰️ Skyve**: 必备。用于管理播放集（Playset）、备份检查存档、排查冲突。

* **📸 Image Overlay**: 必备。能够在地图上叠加真实世界地图，1:1 照抄现实城市的利器。

* **🎥 Free Range Camera**: 解除原版相机视野限制。

* **💧 Water Features**: 更好的水源掌控能力，大地图玩水必备。

* **🏢 529 Tiles**: 用于解锁大面积的全图瓦片地块。

* **🏗️ Anarchy**: 无碰撞/无政府建造环境。

* **📊 Demand Master Control**: 原版游戏在人口达到 20-50 万后极易出现经济崩盘与卡顿，用此模组人工干预需求有助于维持城市基础运转。

## 🔍 已知问题

* **🚦 原版寻路瓶颈与经济紊乱：** 在原版机制下，当人口达到 20-50 万（视路网规划而定），寻路队列拥堵会导致极度占用 CPU 并诱发经济模拟崩溃。本模组内置的经济与性能优化功能能够在极大程度上缓解这些致命问题，但不能保证稳定性。

* **🏙️ 城市发展节奏建议：** 切忌一次性划定超大面积区块；不要过度拔高市民幸福度；不要大幅增加服务设施覆盖；不要建太多公园（甚至最好不建公园）。现实中稳步发展才是城市健康之道，过快的福利提升容易催生大量寻路乱象和经济异常。

* **🗺️ 地图边缘异常：** 受限于原版框架的浮点精度限制，在大型地图最外侧边缘区域可能会出现奇怪的地形高度错误显示。建议将核心的工业/商业/住宅区选址在地图偏中间。

* **🔄 向上兼容说明：** 如果您从之前的未发布非 57km 版（如私人测试版 28km/114km）升级上来，请在选项面板勾选 "Debug devalidation"，选好尺寸，载入成功后另存为新档，再关闭 Debug 选项。

* 📬 欢迎加入 Discord 频道或前往 GitHub 提交报错反馈。

## 💡 技巧与辅助

* **💧 快速注水：** 大地图等待水流灌满极其漫长，强烈推荐使用 **Water Features** mod。在选项中允许实验性功能，将"流动性"拖拽拉满，蒸发量调最低，注水极快。或者使用原版开发者模式（DevUI）一键填充当前海平面（当然，您必须记得事先放置好稳定水源头，否则水依然会随时间干涸）。

* **🧹 清理缓存排雷：** 当游戏由于内存泄漏或其他机制崩溃退回桌面后，极易产生模组残留缓存导致后续读取直接坏档（这不限于本模组）。一旦发现载入界面出现报错红字，请立即大退游戏，打开 Skyve 专门执行一次"清理模组缓存"。
