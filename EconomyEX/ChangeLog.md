## v4.2.0 - Enhanced Mod Conflict Auto-Detection

* **[Conflict Detection]:** Conflicting economy system groups are now automatically disabled at startup when known incompatible mods are detected.
* **[Conflict Detection]:** Added main menu notification when economy system groups are auto-disabled due to conflicts.
* **[Conflict Detection]:** When MapExtPDX is also loaded, EconomyEX defers all conflict monitoring to MapExtPDX to avoid duplicate checks.
* **[Performance]:** Changed system conflict monitoring to passive diagnostic mode with zero runtime overhead.

---

### 主要改动

* **[冲突检测]：** 启动时检测到已知冲突 Mod 后，自动禁用对应的经济系统组，无需手动干预。
* **[冲突检测]：** 新增主菜单通知，提示因冲突被自动禁用的系统组。
* **[冲突检测]：** 当 MapExtPDX 同时加载时，EconomyEX 将冲突监控委托给 MapExtPDX，避免重复检测。
* **[性能]：** 冲突监控改为被动诊断模式，零运行时开销。
