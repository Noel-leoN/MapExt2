# 🚀 Cities Skylines 2 Map Extended Mod (28/57/114/vanilla) with Economy Fix and Performance Improvement

## 🚀 Update Notice

* **💎 Economy Fix and Performance Optimization (Experimental):** This version includes a suite of optimizations specifically tuned for mega-cities (millions of population). It refactors demand logic, land value, housing search, citizen behaviors, and rent systems.

* **⏳ Simulation Catch-up:** Loading an old save may result in extremely slow simulation initially as the new economic logic recalculates everything. Please allow the simulation to run for a while for performance to stabilize.

* **🌍 Resources Map Loss:** Due to architectural changes in the resource system, **ore, oil, and other natural resources may disappear from existing saves.** You will need to use the `ExtraLandScapingTool` mod to manually repaint them.

* **⚠️ Compatibility:** This mod now fundamentally rewrites core economic logic. It is highly likely to conflict with other economy-rebalancing or simulation-altering mods.

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

* **⚙️ Integrated Features:** This mod includes simple performance tweaks I've previously authored (such as NoDogs and CS2LiteBoost). These can be toggled on inside the Option UI, meaning you can unsubscribe from their standalone versions.

## 🛠️ Usage

### 🗺️ Making a 1:1 Map

In the Map Editor, import a heightmap/worldmap image of the corresponding size:

* **28km Playable Area:** Heightmap 28,672m / Worldmap 114,688m

* **57km Playable Area:** Heightmap 57,344m / Worldmap 229,376m

* **114km Playable Area:** Heightmap 114,688m / Worldmap 458,752m

* **🖼️ Supported Image Formats:** 4096x4096 16-bit grayscale (PNG or TIFF).

* 💡 Note: You can import heightmaps with resolutions up to 14336x14336; the mod will automatically downsample/scale them to 4096x4096.

* **⚠️ Scaling Warning:** If the physical dimensions of your imported map do not mathematically match the target size, the terrain will stretch.

* **🚀 Performance Tip:** For maps larger than 57km, do NOT import a fake "Worldmap" in order to save performance. The worldmap is purely visual dressing and provides no gameplay benefit.

### 📂 Useful Directory Paths (Paste into File Explorer)

* **🗺️ Heightmaps and Worldmaps:** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Heightmaps`

* **🖼️ Overlay Maps:** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Overlays`

* **📝 Game Logs (for bug reporting):** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs`

* **🛠️ Local Mods Folder:** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods`

### 🔗 Recommended Companion Mods

* **🛰️ Skyve**: Essential for managing Playsets, backing up saves, and diagnosing issues.

* **📸 Image Overlay**: Allows overlaying a real-world map image in-game, perfect for recreating 1:1 real cities.

* **🎥 Free Range Camera**: Unlocks camera distance constraints.

* **💧 Water Features**: Provides better control over water sources—critical for larger custom maps.

* **🏢 529 Tiles**: Essential for purchasing and managing all map tiles on extended maps.

* **🏗️ Anarchy**: Removes placement restrictions.

* **📊 Demand Master Control**: Vanilla simulation often struggles (economic anomalies, pathfinding lag) when populations exceed 200k-500k. Tweaking demand directly can drastically help stability.

## 🔍 Known Issues

* **🚦 Vanilla Pathfinding Flaw:** In vanilla CS2, when populations exceed 200k–500k, pathfinding queue jams cause severe CPU bottlenecks and break the economic simulation. Extending the map makes these underlying issues more apparent. While this mod's built-in Economy and Performance optimizations are designed to significantly alleviate these issues, extreme scenarios may still challenge engine limits. Absolute stability at ultra-high populations cannot be guaranteed.

* **🏙️ City Planning Advice:** Stable growth is key. Avoid zoning massive residential blocks at once, don't rush to max out citizen happiness overnight, control your service coverage spread, and avoid spamming parks (or avoid parks entirely).

* **🗺️ Map Edge Glitches:** Due to engine floating-point precision limits, you may notice bizarre terrain height graphical glitches near the absolute edges of large maps. It's best to build your core residential/commercial/industrial zones closer to the map center.

* **🚫 Compatibility (CRITICAL):** Because this mod now fundamentally rewrites the core economic and simulation logic, it is highly likely to conflict with other economy-rebalancing or simulation-altering mods. Using multiple economy mods simultaneously will cause severe calculation conflicts and game-breaking bugs.

* **🔄 Legacy Version Upgrades:** If upgrading from an older unreleased version and playing on sizes other than 57km, manually check the "Debug devalidation" toggle in Option UI, confirm your map size, fully load your save, "Save As" a new file, and then disable the Debug toggle.

## 💡 Tips

* **💧 Quick Water Generation:** Since large maps take extremely long to manually fill with water, we recommend Yenyang's **Water Features** mod. Enable experimental features, max out the "flowness" slider, and minimize evaporation for quick results. Alternatively, instantly fill the oceans by setting the Sea Level via the in-game Developer UI (though you still need physical water sources to maintain it over time).

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

## 🚀 v2.2.3 更新说明

* **💎 经济与性能深度优化（实验性）：** 模组内置了专为百万级人口大城市设计的经济与性能修复补丁。该功能从底层全面重构了各项区域需求、地价系统、居民找房逻辑、居民日常行为与交租计算等。

* **⏳ 模拟“适应期”提示：** 载入旧存档（v2.2.3 之前）后，由于新的经济逻辑需要重新计算所有数据，初次运行可能会出现模拟速度极慢、瞬间卡顿等现象。请耐心等待一段时间，待模拟数据稳定后即可恢复正常。

* **🌍 资源地图丢失提示：** 受到底层机制变更影响，**旧存档中的矿产、石油等自然资源可能会丢失**。您需要使用 `ExtraLandScapingTool` 模组重新涂刷这些资源。

* **⚠️ 兼容性警告：** 由于本模组已在底层深度重构了原版经济计算逻辑，它极易与市面上其他各类“经济修复 / 经济调整 / 需求控制”类模组发生冲突。同时使用多款修缮经济的模组会导致模拟计算错乱甚至直接坏档毁图。

## ⚠️ 必读注意事项

* **🛡️ 风险提示**：本模组对原版底层代码进行了大量深度修改，可能存在未知问题或与其他模组冲突，请自行承担风险。

* **💾 勤备份存档**：强烈建议养成每次**“另存为”新文档**的习惯，切勿直接覆盖旧存档。**绝对不要**在模组卸载或加载失败时，强行读取并覆盖 MapExt 存档。

* **📏 匹配地图尺寸**：在选项界面中，必须设置与当前游玩地图相匹配的绝对尺寸（28km、57km、114km 或 原版14km）。⚠️ 注：模组已内置防错机制，会阻止跨尺寸读取存档，但该机制**不适用于地图编辑器**。

* **💻 硬件建议**：推荐显存 10GB 以上（本模组将额外占用 1-2GB 显存）。若加载地图时崩溃，通常是因为加载了过多资产模组导致显存爆满。首次使用建议创建一个精简的 Playset 播放集，确认正常后再逐步添加资产。

## 💎 介绍

* **📏 地图尺寸模式列表：**

  * **57公里（默认）**：4x4 原版地图面积，DEM-14米分辨率。

  * **28公里**：2x2 原版地图面积，DEM-7米分辨率。

  * **114公里**：8x8 原版地图面积，DEM-28米分辨率。（极不推荐，地形分辨率过低，地图边缘会出现撕裂，且严重影响模拟计算）

  * **14公里**：原版 1x1 地图面积，DEM-3.5米分辨率。

* **🏔️ 地形精度限制：** 由于尺寸放大，地形细节不可避免地会降低，海岸线与山脉会显得相对粗糙。受限于复杂的技术原因与性能开销考量，本模组暂不提供地形分辨率无损提升方案。建议通过种树或其他造景手段进行视觉遮挡。如果追求高精度大地形，可以关注 algernon 正在开发的 `LargerMap` 模组。

* **🧱 地图瓦片：** 可解锁的地图瓦片总数量维持在 529 块。

* **⚙️ 内置优化小贴士：** 模组内置整合了一些我以前制作的性能优化小工具（如禁止遛狗 NoDogs、路网寻路性能优化 CS2LiteBoost），可以直接在选项面板开启，此时您可以取消订阅那些独立的优化模组。

## 🛠️ 用法

### 🗺️ 制作 1:1 比例地图

在地图编辑器（Editor）中，导入对应正确尺寸的高位图（Heightmap）与世界贴图（Worldmap）：

* **28 公里可玩区域**：高程图 28672 米 / 世界贴图 114688 米

* **57 公里可玩区域**：高程图 57344 米 / 世界贴图 229376 米

* **114公里可玩区域**：高程图 114688 米 / 世界贴图 458752 米

* **🖼️ 支持的图像格式**：4096x4096 16位灰度图（PNG 或 TIFF）。

* 💡 注：可以直接导入不高于 14336x14336 分辨率的地形高程图，模组会自动将其智能缩放至 4096x4096

* **⚠️ 拉伸警告**：若导入的现实尺寸与上述设定的比例不匹配，地形将被错误拉伸。

* **🚀 性能提示**：对于超过 57公里的地图，为了节省性能开销，强烈建议不要导入只作背景板用的“假”世界贴图。（世界贴图仅提供边缘视觉过度，对游玩毫无物理作用）。

### 📂 常用路径速查（可直接粘贴至文件资源管理器地址栏）

* **🗺️ 高程图与世界贴图**：`%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Heightmaps`

* **🖼️ 覆盖层地图 (配合 Image Overlay)**： `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Overlays`

* **📝 游戏日志 (用于报错排查)**： `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs`

* **🛠️ 本地模组文件夹 (仅限手动安装或高级调试，工坊模式请勿更动)**： `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods`

### 🔗 推荐搭配模组（感谢各位开源作者！）

* **🛰️ Skyve**: 必备。用于管理播放集（Playset）、备份检查存档、排查冲突。

* **📸 Image Overlay**: 必备。能够在地图上叠加真实世界地图，1:1 照抄现实城市的利器。

* **🎥 Free Range Camera**: 解除原版相机视野限制。

* **💧 Water Features**: 更好的水源掌控能力，大地图玩水必备。

* **🏢 529 Tiles**: 必备。用于解锁大面积的全图瓦片地块。

* **🏗️ Anarchy**: 无碰撞/无政府建造环境。

* **📊 Demand Master Control**: 原版游戏在人口达到 20-50 万后极易出现经济崩盘与卡顿，用此模组人工干预需求有助于维持城市基础运转。

## 🔍 已知问题

* **🚦 原版寻路瓶颈与经济紊乱：** 在原版机制下，当人口达到 20-50 万（巡逻路网规划而定），寻路队列拥堵会导致极度占用 CPU 并诱发经济模拟崩溃。本模组内置的经济与性能优化功能能够在极大程度上缓解这些致命问题，但在不可理喻的极端人口规模下仍可能会触及引擎上限，因此无法作绝对的稳定性保证。

* **🏙️ 城市发展节奏建议：** 切忌一次性划定超大面积区块；不要过度拔高市民幸福度；不要大幅增加服务设施覆盖；不要建太多公园（甚至最好不建公园）。现实中稳步发展才是城市健康之道，过快的福利提升容易催生大量寻路乱象和经济异常。

* **🗺️ 地图边缘异常：** 受限于原版框架的浮点精度限制，在大型地图最外侧边缘区域可能会出现奇怪的地形高度错误显示。建议将核心的商业/住宅区选址在地图偏中间。

* **🚫 兼容性冲突（严重警告）：** 由于本模组已在底层深度重构了原版经济计算逻辑，它极易与市面上其他各类“经济修复 / 经济调整 / 需求控制”类模组发生冲突。同时使用多款修缮经济的模组会导致模拟计算错乱甚至直接坏档毁图。

* **🔄 向上兼容说明：** 如果您从之前的未发布非 57km 版（如私人测试版 28km/114km）升级上来，请在选项面板勾选 "Debug devalidation"，选好尺寸，载入成功后另存为新档，再关闭 Debug 选项。

* 📬 欢迎加入 Discord 频道或前往 GitHub 提交报错反馈。

## 💡 技巧与辅助

* **💧 快速注水：** 大地图等待水流灌满极其漫长，强烈推荐使用 **Water Features** mod。在选项中允许实验性功能，将“流动性”拖拽拉满，蒸发量调最低，注水极快。或者使用原版开发者模式（DevUI）一键填充当前海平面（当然，您必须记得事先放置好稳定水源头，否则水依然会随时间干涸）。

* **🧹 清理缓存排雷：** 当游戏由于内存泄漏或其他机制崩溃退回桌面后，极易产生模组残留缓存导致后续读取直接坏档（这不限于本模组）。一旦发现载入界面出现报错红字，请立即大退游戏，打开 Skyve 专门执行一次“清理模组缓存”。
