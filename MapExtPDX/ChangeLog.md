## v4.0.1 - Vanilla Map Extension and Water Tools

* **[Map Extension - Major Feature]:** Added support for extending vanilla (14km) saves to 28km or 57km mode. The conversion pipeline synthesizes the extended terrain heightmap, perfectly preserves original natural resources and groundwater by embedding them into the expanded map (removed outdated Perlin generation), clears all vehicle and resident entities, removes all outside connections, resets water simulation state, unlocks all 529 map tiles, and auto-saves to a new file. Game restart is required after conversion. Players must then rebuild outside connections and place new water sources at the new map edges. Note: Water levels might not be perfectly accurate after conversion, please use the built-in Water Tools or the Water Features mod to adjust.
* **[UI - Water Tools]:** Added a Water Tools section to the in-game HUD panel with sea level adjustment (slider with 0.1m precision and numeric input), Apply Sea Level (GPU reset water surface to target height), Reset Water (clear all water and re-simulate from sources), and water simulation speed control (0x-128x exponential stepping).
* **[Settings - Save Conversion]:** Added an "Enable Vanilla Map Extension" toggle in the MapSize tab. Mutually exclusive with "Disable World Backdrop".
* **[Localization]:** Added bilingual (EN and zh-HANS) dialog strings for vanilla map extension confirmation, completion with detailed TODO checklist, and error prompts.

---

### 主要改动

* **[地图扩展 - 重要功能]：** 新增原版（14km）存档扩展至 28km 或 57km 模式的功能。转换流程包括：合成并扩展地形高度图、完美保留并居中原版自然资源与地下水（移除了过时的 Perlin 噪声生成）、清除所有车辆与居民实体、拆除全部外部连接、重置水体模拟状态、解锁全部 529 格地图分块，并自动保存为新文件。转换完成后必须重启游戏。重启后需重建对外连接并重新放置水源。提示：转换后水位可能不够准确，建议使用内建水体工具或 Water Features 模组手动调整。
* **[UI - 水体工具]：** 在游戏内 HUD 面板新增水体工具模块，包含海平面调节（0.1m 精度滑块与数值输入）、应用海平面（GPU 重置水面至目标高度）、重置水体（清除水面并从水源重新模拟）、以及水模拟速度控制（0x-128x 指数级步进）。
* **[设置 - 存档转换]：** 在 MapSize 标签页新增"启用原版地图扩展"开关，与"禁用背景世界地图"选项互斥。
* **[本地化]：** 为原版地图扩展确认、完成（含详细待办清单）与错误提示对话框新增中英双语文本。
