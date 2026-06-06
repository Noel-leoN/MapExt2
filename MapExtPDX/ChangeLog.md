## v4.1.3 - Conflict Detection for Realistic Series Mods

* **[Core - Conflict Detection]:** Added a startup mod fingerprint scanner that detects known conflicting mods (RWH, RealisticPathFinding, Time2Work, RealisticParking, UrbanInequality) at launch with zero runtime cost.
* **[Core - Conflict Detection]:** Added reverse detection in ConflictMonitoringSystem to check if MapExt replacement systems are disabled by external mods.
* **[Core - Conflict Detection]:** Added EconomyParameterData multi-writer conflict detection when two or more mods modify the same singleton.
* **[Settings - UI]:** Added a "Detected Conflict Mods" read-only field in the EconomyEX tab, visible in main menu and in-game, showing all detected third-party mods at startup.
* **[Core - Refactor]:** Moved ConflictMonitoringSystem from UISystem to Core directory for better architectural alignment.

---

### 主要改动

* **[核心 - 冲突检测]：** 新增启动时 Mod 指纹扫描器，在启动阶段一次性识别已知冲突 Mod（RWH、RealisticPathFinding、Time2Work、RealisticParking、UrbanInequality），零运行时开销。
* **[核心 - 冲突检测]：** 在冲突监控系统中新增反向检测，检查 MapExt 自身的替换系统是否被外部 Mod 禁用。
* **[核心 - 冲突检测]：** 新增 EconomyParameterData 多写冲突检测，当两个以上 Mod 同时修改同一 Singleton 时告警。
* **[设置 - UI]：** 在 EconomyEX 标签页新增"检测到的冲突Mod"只读字段，主菜单与游戏内均可见，显示启动时检测到的所有第三方冲突 Mod。
* **[核心 - 重构]：** 将 ConflictMonitoringSystem 从 UISystem 移至 Core 目录。
