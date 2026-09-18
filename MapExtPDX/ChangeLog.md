## v4.9.0 - Game 1.6.2f Compatibility and Economy Simulation Adaptation

* **[Compatibility]:** Updated for game version 1.6.2f. Note that the official patch introduced adjustments to the underlying economy simulation, which may lead to demographic or economic fluctuations in existing saves; please report any unexpected behaviour.
* **[Economy]:** Synchronized the new disposable income model, calculating household shopping and leisure preferences based on income after rent.
* **[Economy]:** Synchronized the revised wealth wellbeing calculation, reflecting household income and rent burdens in citizen welfare.
* **[Economy]:** The minimum free property threshold for residential searches is now read from economy configuration parameters.
* **[Economy]:** Resource buyers no longer import goods flagged with outside connections when external connections are active.
* **[Simulation]:** Aligned water velocity texture format with official 1.6.2f specifications for expanded map rendering stability.
* **[Simulation]:** Resolved concurrency restrictions and sampling height alignment in wind simulation jobs.
* **[Tools]:** Aligned area tool structure definition with the latest game patch.

---

### 主要变动

* **[兼容性]：** 适配游戏 1.6.2f 版本。由于官方在本次补丁中对底层经济模拟机制进行了较多调整，既有存档在适应新机制时可能出现人口流动或经济指标波动，如遇未知异常欢迎反馈。
* **[经济系统]：** 同步官方基于可支配收入的消费与休闲决策模型，家庭购物偏好改为由扣除租金后的净收入驱动。
* **[经济系统]：** 同步官方重构后的财富福利计算算法，市民幸福度动态反映家庭收入与租金负担水平。
* **[经济系统]：** 家庭找房的最低空置房产阈值改为读取经济参数配置。
* **[经济系统]：** 外部连接启用时，资源采购系统不再购买带有外连标记的货物。
* **[模拟机制]：** 对齐官方 1.6.2f 水流贴图数据格式，保证大地图水体采样稳定。
* **[模拟机制]：** 修复风场模拟的并发调度限制与采样高度对齐问题。
* **[工具]：** 对齐区域绘制工具（Area Tool）与官方最新补丁的结构体定义。
