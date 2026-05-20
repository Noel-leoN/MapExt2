## v4.0.2 - Vanilla Map Extension and Water Tools

* **[Map Extension]:** Added support for extending vanilla (14km) saves to 28km or 57km mode. The extension pipeline synthesizes the extended terrain heightmap, preserves original natural resources and groundwater by embedding them into the expanded map, clears vehicle and resident entities, removes outside connections, resets water simulation state, unlocks all 529 map tiles, and auto-saves to a new file. Game restart is required after extension.
* **[UI - Water Tools]:** Added a Water Tools section to the in-game HUD panel with sea level adjustment (slider with 0.1m precision and numeric input), Apply Sea Level (GPU reset water surface to target height), Reset Water (clear water and re-simulate from sources), and water simulation speed control (0x-128x exponential stepping).
* **[Settings - Map Extension]:** Added an "Enable Vanilla Map Extension" toggle in the MapSize tab. Mutually exclusive with "Disable World Backdrop".
* **[UI - Water Tools Fixes]:** Fixed sea level detection to query type 2 (Sea Water Source) water source entities when properties return zero for backward compatibility. Removed high-frequency logger calls during slider drag events to prevent UI freezes.
* **[Localization]:** Added bilingual (EN and zh-HANS) dialog strings for vanilla map extension confirmation, completion checklist, and error prompts.
* **[Localization]:** Added Traditional Chinese (zh-HANT) localization support for in-game options settings, error validation dialogs, and the dashboard overlay.

---

### 主要改动

* **[地图扩展]：** 新增将原版（14km）存档扩展至 28km 或 57km 模式的功能。扩展流程包括：合成并扩展地形高度图、保留并居中原版自然资源与地下水、清除车辆与居民实体、拆除全部外部连接、重置水体模拟状态、解锁全部 529 格地图分块，并自动保存为新文件。扩展完成后必须重启游戏。
* **[UI - 水体工具]：** 在游戏内 HUD 面板新增水体工具模块，包含海平面调节（0.1m 精度滑块与数值输入）、应用海平面（GPU 重置水面至目标高度）、重置水体（清除水面并从水源重新模拟）、以及水模拟速度控制（0x-128x 指数级步进）。
* **[设置 - 地图扩展]：** 在 MapSize 标签页新增"启用原版地图扩展"开关，与"禁用背景世界地图"选项互斥。
* **[UI - 水体工具修复]：** 修复了海平面检测逻辑，当系统属性为零时支持通过查询海水源实体高度进行计算。移除了滑块拖拽时的高频日志输出以防止 UI 卡死。
* **[本地化]：** 为原版地图扩展确认、完成与错误提示对话框新增中英双语文本。
* **[本地化]：** 为所有游戏内选项设置项、错误验证对话框以及仪表盘界面新增繁体中文（zh-HANT）本地化支持。
