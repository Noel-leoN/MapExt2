## v4.1.0 - Terrain Tools and Vehicle Rescue Improvements

* **[Simulation - Vehicle Rescue]:** Fixed a collection modification exception in the vehicle rescue retry logic, and redirected debug logs to a dedicated file.
* **[Editor - Terrain Tools]:** Added a distance buffer for road obstacle checks, resolved GPU texture copy compatibility issues, and rate-limited terrain system updates.

---

### 主要改动

* **[模拟 - 购车救援]：** 修复了车辆救援重试逻辑中字典遍历修改的异常，并将调试日志独立输出至专属文件。
* **[编辑器 - 地形工具]：** 为道路阻挡检测新增可配置距离缓冲区，解决部分硬件下的 GPU 拷贝兼容问题，并限制了地形系统的异步更新频率。
