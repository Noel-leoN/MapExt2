# 🚀 Map Extended Mod (28/57/114/vanilla) — Vanilla Map Extension!

## 🔥 v4.0.2 — Expand Your Existing Vanilla City!

> **🎉 NEW: You can now extend your existing 14km vanilla city to 28km or 57km maps!** No need to start from scratch — simply load your vanilla save, and MapExt will automatically convert it, expanding the playable area to regions previously outside the map limits. Your terrain, city layout, and buildings are perfectly preserved. This is a one-click, non-destructive process.

### 🛠️ How to Extend Your City

1. In **Main Menu**, open MapExt Option UI and select target mode (28km or 57km).
2. Enable **"Vanilla Map Extension"** in MapSize tab → Save Conversion group.
3. **Load** any vanilla (14km) save. A confirmation dialog lists all conversion steps.
4. Click **"Extend and Load"**. The mod will: unlock 529 tiles, clear vehicles/residents, remove outside connections, synthesize extended heightmap, preserve original resources/groundwater, reset water, and auto-save as `{Name}_MapExt{Mode}`.
5. **MUST restart game** after conversion to prevent water glitches or crashes.
6. After restart, complete:
   * **Rebuild Outside Connections** at new map edges: Roads, Railways, Shipping Lanes, Airline Routes, Electricity, Water Supply
   * **Place new Water Sources**: Original sources cleared. You MUST use the **Water Features** mod to place river/sea sources (no other mods currently have this feature), then wait for natural fill, or use Water Tools (M button) to speed up.
   * **Adjust Sea Level**: Note that water levels might not be perfectly accurate after conversion, please manually adjust them using the built-in Water Tools (M button) or the Water Features mod.

⚠️ **Highly experimental.** Original save is never modified.

### 🌟 Other Highlights

* **💧 Water Tools:** New HUD panel section with sea level control (0.1m precision slider and numeric input), Apply Sea Level (GPU reset), Reset Water (re-simulate from sources), and simulation speed control (0x-128x exponential stepping).
* **⚠️ Compatibility:** Built-in Conflict Monitoring System attempts to auto-detect and disable conflicting economy subsystems at runtime. However, for optimal performance and simulation stability, it is highly recommended to avoid using other economy or pathfinding mods simultaneously. Check system status in the EconomyEX tab.
* **🔗 [EconomyEX](https://mods.paradoxplaza.com/mods/137149/Windows):** Standalone economy subset for vanilla-size maps. Auto-disables when both mods are installed.

## ⚠️ IMPORTANT: READ BEFORE USE

* **🛡️ Use at your own risk.** This mod makes extensive modifications to the game engine.
* **⚠️ Mod Compatibility:** To ensure optimal performance and avoid unintended game behavior, using this alongside other economy or pathfinding modification mods is strongly discouraged.
* **💾 Backup your saves:** ALWAYS use "Save As". **Never** overwrite a MapExt save if the mod fails to load or is uninstalled.
* **📏 Match Map Sizes:** Select the correct map size in Option UI. A failsafe prevents loading mismatched saves (does NOT apply to the Map Editor).
* **💻 Hardware:** 10GB+ VRAM recommended (mod uses 1-2GB extra). If loading crashes, reduce custom assets.

## 💎 Introduction

* **📏 Map Size Modes:**
  * **57km (Default):** 4x4 vanilla (DEM-14m). **28km:** 2x2 (DEM-7m). **114km:** 8x8 (DEM-28m, not recommended). **14km:** Vanilla 1x1 (DEM-3.5m).
* **🏔️ Terrain Limitation:** Terrain resolution decreases with map size. Coastlines may appear jagged. Use trees to cover rough patches.
* **🧱 Map Tiles:** Fixed at **529** unlockable tiles.

## 💡 Economy Patches, In-Game UI and Performance Tools

* **🎛️ In-Game UI Dashboard (v3.0+):** "M" button in HUD opens a panel with City Stats (5 accordion sections, 13+ metrics), Rent Control (11 sliders), and Pathfinding sliders. Zero overhead when closed. Auto EN/CN switching.
* **📊 Economy Overhaul (Beta):** Deep rewrites of RCI demand, job/home search, rent, consumer behavior, and AI pathfinding for mega-cities. Each subsystem toggleable. Built-in conflict monitoring.
* **📈 Pathfinding Distance Control:** Configurable max distance per travel purpose (shopping, leisure, jobs, home, school). Vanilla hardcodes 17000 — raise for big maps, lower for small maps.
* **🐕 NoDogs 2.0:** Three modes — Disable OnStreet, Prevent New Generation, Purge All. Live pet count display.
* **🚗 No Through-Traffic:** Disables through-traffic spawning to reduce pathfinding load.
* **🏗️ Editor Collision Override:** Skip collision checks in Map Editor (Off / Trees Only / All Objects).
* **🏔️ Terrain-Water Optimization:** GPU buffer pre-allocation, building cull throttling, terrain cascade throttling, configurable water sim quality — all adjustable in-game.
* **🌍 Disable World Backdrop:** Prevents background heightmap loading, reducing GPU/VRAM overhead.
* **🔄 Vanilla Map Extension (v4.0.2):** One-click expansion of vanilla saves to extended maps.
* **💧 Water Tools (v4.0.2):** Sea level control, water reset, and simulation speed (0x-128x) in the HUD panel.

## 🛠️ Usage

### 🗺️ Making a 1:1 Map

Import a heightmap/worldmap in the Map Editor:

* **28km:** Heightmap 28,672m / Worldmap 114,688m
* **57km:** Heightmap 57,344m / Worldmap 229,376m
* **114km:** Heightmap 114,688m / Worldmap 458,752m
* **Format:** 4096x4096 16-bit grayscale (PNG/TIFF). Resolutions up to 14336x14336 auto-downsampled.
* **⚠️** Mismatched dimensions will stretch terrain.
* **🚀 Tip:** Do NOT import Worldmap on 57km/114km maps — it adds 1-2 GB VRAM with no gameplay benefit.

### 📂 Useful Directory Paths

* **Heightmaps:** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Heightmaps`
* **Overlays:** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Overlays`
* **Logs:** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs`
* **Local Mods:** `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods`

### 🔗 Recommended Companion Mods

* **Skyve** — Playset management and save backup.
* **Image Overlay** — Overlay real-world maps for 1:1 city recreation.
* **Free Range Camera** — Unlock camera distance.
* **Water Features** — Better water source control for custom maps.
* **529 Tiles** — Unlock all map tiles.
* **Anarchy** — Remove placement restrictions.

## 🔍 Known Issues

* **🚦 Pathfinding Bottleneck:** At 200k-500k population, vanilla pathfinding jams cause CPU bottlenecks. This mod's optimizations help significantly but cannot guarantee stability at extreme scales.
* **🗺️ Map Edge Glitches:** Floating-point precision limits may cause terrain height artifacts at map edges. Build core zones closer to center.
* **🔄 Legacy Upgrades:** From older unreleased versions on non-57km sizes: enable "Debug devalidation", confirm size, load, Save As, then disable Debug.

## 💡 Tips

* **💧 Quick Water:** Use built-in Water Tools (M button → Water Tools): set sea level, click "Apply" to fill oceans, then set speed to 128x for rapid river/lake filling. Or use **Water Features** mod.
* **🧹 Clear Mod Cache:** After a crash, open **Skyve** and "Clear Mod Cache" to prevent save corruption on next load.

## 🏆 Credits

* [CS Modding Discord](https://discord.gg/s6BcrFKepF) | [Cities 2 Modding Discord](https://discord.gg/ABrJqdZJNE)
* [CS2 Modding Instructions](https://github.com/rcav8tr/CS2-Modding-Instructions) by rcav8tr
* [BepInEx](https://github.com/BepInEx/BepInEx) | [Harmony](https://github.com/pardeike/Harmony)
* 🤝 Thanks to testers: Rebeccat, HideoKuze2501, Nulos, Jack the Stripper, Bbublegum/Blax, Sulley and many others!

---

## 🔥 v4.0.2 更新说明 — 扩展你的现有原版城市！

> **🎉 重磅新功能：现在你可以将现有的 14km 原版城市扩展至 28km 或 57km 的庞大区域！** 无需从零开始，只需加载原版存档，MapExt 会自动将其转换，将原版无法游玩的边界区域全部扩展为可玩区域。你的原有地形、城市布局和建筑均会完美保留。

### 🛠️ 如何扩展你的现有城市

1. **主菜单**中打开 MapExt 选项，选择目标模式（28km 或 57km）。
2. 在 MapSize 标签页 → 存档转换组中启用**「原版地图扩展」**。
3. 加载任意原版（14km）存档，弹出确认对话框。
4. 点击**「扩展并加载」**。自动执行：解锁 529 分块、清除车辆/居民、拆除外部连接、合成扩展高度图、完美保留原版资源/地下水、重置水体、另存为 `{存档名}_MapExt{模式}`。
5. **必须重启游戏**，否则水体异常或崩溃。
6. 重启后完成：
   * **重建对外连接**：道路、铁路、航道、航线、电力、供水
   * **放置水源**：原有水源已清除。你必须使用 **Water Features** 模组来放置河流/海洋水源（目前没有其他模组具备此功能），放置后等待填充，或用水体工具（M按钮）加速。
   * **调整海平面**：提示：转换后水位可能不够准确，建议使用内建的“水体工具”（M键）或配合 Water Features 模组手动调整海平面。

⚠️ **高度实验性功能。** 原始存档不会被修改。

### 🌟 其它亮点更新

* **💧 水体工具面板：** HUD 面板新增水体工具：海平面调节（0.1m 精度）、应用海平面（GPU 重置）、重置水体（从水源重新模拟）、模拟速度控制（0x-128x）。
* **⚠️ 兼容性：** 内置冲突监控系统会尝试自动检测并休眠冲突的经济子系统。但为保证最佳性能与模拟稳定性，强烈建议尽量避免同时使用其他修改经济或寻路机制的模组。
* **🔗 [EconomyEX](https://mods.paradoxplaza.com/mods/137149/Windows)：** 经济模块独立子集，适用于原版地图。两者同时安装时 EconomyEX 自动休眠。

## ⚠️ 必读注意事项

* **🛡️ 风险自负**：本模组深度修改游戏引擎，可能存在未知问题。
* **⚠️ 模组兼容性**：强烈建议避免与其他修改经济或寻路机制的模组同时使用，以确保最佳性能并防止潜在的模拟异常。
* **💾 勤备份存档**：务必"另存为"新档。**绝不**在模组失效时覆盖 MapExt 存档。
* **📏 匹配地图尺寸**：选项面板中必须设置与存档匹配的尺寸。已内置防错机制（不适用于编辑器）。
* **💻 硬件**：推荐显存 10GB+（额外占用 1-2GB）。崩溃时请精简资产。

## 💎 介绍

* **📏 地图尺寸：** 57km（默认，4x4，DEM-14m）| 28km（2x2，DEM-7m）| 114km（8x8，DEM-28m，不推荐）| 14km（原版 1x1，DEM-3.5m）。
* **🏔️ 地形精度：** 尺寸越大，地形越粗糙。建议种树遮挡。
* **🧱 地图瓦片：** 固定 529 块。

## 💡 经济补丁、游戏内 UI 与性能工具

* **🎛️ 游戏内 UI（v3.0+）：** HUD "M"按钮展开面板，含城市统计（5 区块 13+ 指标）、租金调控（11 滑块）、寻路参数。关闭时零开销，自动中英文切换。
* **📊 经济修复 (Beta)：** 深度重构 RCI 需求、求职匹配、找房、租金、消费行为与 AI 寻路，专为百万人口优化。各子系统独立开关。内置冲突监控。
* **📈 寻路距离控制：** 按出行目的配置最大寻路距离。大地图适当提高，小地图降低以减少 CPU 开销。
* **🐕 NoDogs 2.0：** 禁止外出 / 阻止新生成 / 清除全部。含实时宠物计数。
* **🚗 过境交通控制：** 禁止过境交通，降低寻路负载。
* **🏗️ 编辑器碰撞跳过：** 三档模式（关闭/仅树木/所有物体）。
* **🏔️ 地形-水体优化：** GPU 缓冲预分配、建筑裁剪降频、地形级联降频、水模拟质量——游戏内实时调节。
* **🌍 禁用背景世界地图：** 阻止 Backdrop 加载，降低 GPU/显存开销。
* **🔄 原版地图扩展（v4.0.2）：** 一键将原版存档扩展至大地图。
* **💧 水体工具（v4.0.2）：** 海平面调节、水体重置、模拟速度控制（0x-128x）。

## 🛠️ 用法

### 🗺️ 制作 1:1 地图

在地图编辑器中导入对应尺寸的高程图/世界贴图：

* **28km**：高程图 28672m / 世界贴图 114688m
* **57km**：高程图 57344m / 世界贴图 229376m
* **114km**：高程图 114688m / 世界贴图 458752m
* **格式**：4096x4096 16位灰度图（PNG/TIFF），最高支持 14336x14336 自动降采样。
* **⚠️** 尺寸不匹配会导致拉伸。
* **🚀 提示**：57km/114km 地图不要导入 Worldmap——仅视觉装饰，额外消耗 1-2GB 显存。

### 📂 常用路径

* **高程图**：`%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Heightmaps`
* **覆盖层**：`%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Overlays`
* **日志**：`%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs`
* **本地模组**：`%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods`

### 🔗 推荐搭配模组

* **Skyve** — 播放集管理与存档备份。
* **Image Overlay** — 叠加真实地图，1:1 复刻城市。
* **Free Range Camera** — 解除相机限制。
* **Water Features** — 大地图水源管理。
* **529 Tiles** — 解锁全图瓦片。
* **Anarchy** — 无碰撞建造。

## 🔍 已知问题

* **🚦 原版寻路瓶颈：** 人口 20-50 万后寻路拥堵导致 CPU 瓶颈。本模组优化可大幅缓解但无法完全保证稳定性。
* **🗺️ 地图边缘异常：** 浮点精度限制可能导致边缘地形伪影。核心区域建议偏中间。
* **🔄 旧版升级：** 非 57km 旧测试版升级请启用 Debug devalidation，确认尺寸后另存为新档。

## 💡 技巧

* **💧 快速注水：** 用内置水体工具（M → 水体工具）设置海平面后点击"应用"一键填海，模拟速度拉到 128x 加速注水。也可配合 **Water Features** mod。
* **🧹 清理缓存：** 崩溃后用 **Skyve** 执行"清理模组缓存"防止坏档。
