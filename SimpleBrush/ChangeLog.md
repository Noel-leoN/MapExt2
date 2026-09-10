## v1.0.1 - Infinite Resource Reliability and Settings Fixes

* **[Fix]:** The infinite resource options refilled resources far less often than the game consumes them, so extraction buildings could still drop to zero efficiency and stop producing between refills.
* **[Fix]:** The resource restore buttons no longer report success when pressed from the main menu, where no city is loaded.
* **[Fix]:** Mod options could fail to be written to disk when another installed mod used the same internal settings name.
* **[Change]:** Hardened the resource brush unlock so a failure part-way through cannot leave only some brushes unlocked, and so it keeps retrying while the game is still loading its data.

---

### 主要变动

* **[修复]：** 无限资源选项的补充频率远低于游戏消耗资源的频率，导致抽取类建筑在两次补充之间仍可能效率归零并停止生产。
* **[修复]：** 在主菜单按下资源恢复按钮时不再提示成功——该处并未加载任何城市。
* **[修复]：** 当其他已安装的 Mod 使用相同的内部设置名称时，本 Mod 的选项可能无法写入磁盘。
* **[调整]：** 强化资源笔刷解锁流程，中途失败不会只留下部分笔刷被解锁，且在游戏数据尚未加载完成时会持续重试。
