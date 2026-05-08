## v3.0.0 - In-Game UI Dashboard

* **[UI - In-Game Panel]:** Added a new in-game floating button and Master-Detail panel for real-time parameter tuning without leaving gameplay. The panel includes three sections: City Stats Dashboard, Rent Control, and Pathfinding.
* **[UI - City Stats]:** Added a real-time City Stats dashboard showing household counts, homeless, moving away, property seekers, high-rent buildings, and pets. The data collection system runs on-demand (only when the dashboard is open) with zero overhead when closed.
* **[UI - Rent Control]:** Exposed 11 rent formula parameters (rent multipliers, land value factors, building level factors, environment effect, service bonus) as interactive sliders directly in the game HUD.
* **[UI - Pathfinding]:** Exposed shopping and leisure pathfinding max cost as interactive sliders in the game HUD.
* **[UI - Localization]:** The in-game panel supports automatic English and Chinese switching based on the game's active locale setting.
* **[Core - ModLog]:** Fixed an intermittent NullReferenceException during early initialization by adding a SafeLog wrapper that falls back to Unity's native logger.

---

### 主要改动

* **[UI - 游戏内面板]：** 新增游戏内浮动按钮与主从面板，可在不退出游戏的情况下实时调参。面板包含三个模块：城市统计、租金调控、寻路参数。
* **[UI - 城市统计]：** 新增实时城市统计仪表盘，显示家庭总数、已租住、无家可归、搬离中、找房中、高租金建筑数与宠物数量。数据收集系统按需运行（仅在面板展开时启用），关闭时零开销。
* **[UI - 租金调控]：** 将 11 项租金公式参数（租金乘数、地价贡献系数、等级贡献系数、环境系数、服务加成）以交互式滑块形式暴露在游戏 HUD 中。
* **[UI - 寻路参数]：** 将购物与休闲寻路最大成本以交互式滑块形式暴露在游戏 HUD 中。
* **[UI - 本地化]：** 游戏内面板支持根据游戏语言设置自动切换中英文界面。
* **[核心 - ModLog]：** 修复初始化早期阶段偶发的 NullReferenceException，通过 SafeLog 包装器回退到 Unity 原生日志。
