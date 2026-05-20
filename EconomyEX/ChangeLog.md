## v2.6.1 - Stability Fix

* **[Core - ModLog]:** Fixed an intermittent NullReferenceException during early initialization by adding a SafeLog wrapper that falls back to Unity's native logger.
* **[Localization]:** Added Traditional Chinese (zh-HANT) localization support for all in-game OptionUI settings.

---

### 主要改动

* **[核心 - ModLog]：** 修复初始化早期阶段偶发的 NullReferenceException，通过 SafeLog 包装器回退到 Unity 原生日志。
* **[本地化]：** 为所有游戏内选项设置项新增繁体中文（zh-HANT）本地化支持。
