## v4.2.0 - Enhanced Mod Conflict Auto-Detection

* **[Conflict Detection]:** Conflicting economy system groups are now automatically disabled at startup when known incompatible mods are detected.
* **[Conflict Detection]:** Added a critical CellMap conflict warning popup that shows affected systems and all loaded mods when a CellMap system is unexpectedly disabled.
* **[Conflict Detection]:** Added main menu notification when economy system groups are auto-disabled due to conflicts.
* **[Conflict Detection]:** Added an incompatibility warning for RealisticPathFinding (RPF), which cannot coexist with MapExt2.
* **[Performance]:** Changed system conflict monitoring to passive diagnostic mode with zero runtime overhead.
* **[Fix]:** Fixed SoilWaterSystem registration to match vanilla behavior.
* **[Fix]:** Fixed PersonalCarAISystem missing save-load phase registration.
* **[UI]:** Added system status tooltip in the in-game panel.

---

### 主要改动

* **[冲突检测]：** 启动时检测到已知冲突 Mod 后，自动禁用对应的经济系统组，无需手动干预。
* **[冲突检测]：** 新增 CellMap 严重冲突弹窗，显示受影响的系统和所有已加载的 Mod。
* **[冲突检测]：** 新增主菜单通知，提示因冲突被自动禁用的系统组。
* **[冲突检测]：** 新增 RealisticPathFinding (RPF) 不兼容警告。
* **[性能]：** 冲突监控改为被动诊断模式，零运行时开销。
* **[修复]：** 修正 SoilWaterSystem 注册方式以匹配原版行为。
* **[修复]：** 补全 PersonalCarAISystem 存档加载阶段的注册。
* **[UI]：** 在游戏内面板新增系统状态悬停提示。
