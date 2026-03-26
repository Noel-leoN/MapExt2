### v1.1.0

#### New

- **Modular Toggles**: Each economy sub-system (Demand, Job Search, Property/Rent, Resource Buyer, Resident AI) can now be toggled independently in the settings.
- **Pathfinding Cost Sliders**: Added 5 in-game adjustable pathfinding parameters (Shopping, Freight, Leisure, Find Job, Find Home).
- **MapExt Conflict Detection**: Automatically detects MapExt (MapExtPDX) and goes dormant to avoid duplicate patching.
- **Map Size Detection**: Automatically identifies vanilla maps (14 km) and only activates on standard maps.
- **Conflict Monitoring System**: Continuously monitors system replacements at runtime and displays warnings in the settings panel.
- **New Systems**: Added ResourceBuyer and ServiceCoverage, plus ResidentAI pathfinding optimization.
- **Localization**: Settings panel is fully localized in English and Chinese.

#### Changed

- Fully refactored Demand, Job Search, and Property/Rent systems to better scale with metropolis sizes.
- Added [Beta] tag to the master switch description to indicate testing phase.

### v1.0.0

- Initial Release — Basic economy fix framework.

### v1.1.0

#### 新增

- **模块化开关**：各经济修复子系统（需求、找工作、找房与租金、消费采购、居民AI）均可在设置中独立启用/禁用。
- **寻路成本滑块**：新增 5 项可在游戏内实时调节的寻路上限参数（购物、公司货运、休闲、找工作、找房）。
- **MapExt 冲突检测**：启动时自动检测 MapExt (MapExtPDX)，若存在则自动休眠，避免重复修补。
- **地图尺寸检测**：自动识别原版地图尺寸（14 km），仅在标准地图上激活。
- **冲突监控系统**：运行时持续监控系统替换状态，并在设置面板实时显示冲突警告。
- **新增系统**：加入了消费采购与服务覆盖寻路优化（ResourceBuyer and ServiceCoverage），以及居民AI寻路优化（ResidentAI）。
- **本地化**：完整的中英文设置面板本地化。

#### 调整

- 需求系统、找工作系统、找房与租金系统全面重构，以适配超级大城市的规模。
- Beta 标志已添加至总开关描述，提示当前功能处于测试阶段。

### v1.0.0

- 首发版本 — 基础经济修复框架发布。
