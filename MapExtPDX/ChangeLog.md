## [4.2.1] - 2026-06-15

### 🐛 修复 / Fixed

- 修复了在地图编辑器中调整地形时，水面模拟速度异常跳变（忽快忽慢）的问题，使水流表现更稳定自然。
  Fixed abnormal water simulation speed jitter when editing terrain in the map editor, making water flow more stable and natural.
- 修复了在原版地图尺寸下，退出到主菜单后再次载入存档时，地价与部分经济相关数据可能出现异常的问题。
  Fixed a potential land value and economy data anomaly when reloading a save on vanilla-size maps after returning to the main menu.

### ⚡ 性能 / Performance

- 为大尺寸地图的水面模拟提供运行时优化选项，降低水面计算对帧率的影响。
  Added a runtime optimization option for water simulation on large maps to reduce its impact on frame rate.

### 🗑️ 移除 / Removed

- 暂时移除了此前不稳定的水流速度手动调节功能，避免引发异常表现。
  Temporarily removed the previously unstable manual water flow speed control to prevent abnormal behavior.

---

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
