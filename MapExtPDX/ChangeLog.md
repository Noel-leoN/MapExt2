## v2.2.3 - Major Update

### [New Features]

**CellMap Resolution Improvement**

* CellMap resolution raised to vanilla levels, improving simulation accuracy and UI display for: Air, Ground, Noise pollution, Land Value, Attractiveness, Traffic view, Livability, Population density, etc.

**Economic System Logic and Performance Optimization (Experimental)**
Logic and performance tuned for mega cities (population in the millions):

* **Demand System (Residential/Commercial/Industrial/Office):** Fixed vanilla bugs and improved algorithms for large-population cities.
* **Land Value System:** Fixed issue where environmental factors only affected UI display but not economic simulation. *(Note: Land values now behave more realistically, so legitimate high rent warnings may appear more frequently.)*
* **Housing Search System:** Fixed vanilla bugs, reduced vacancy rates, optimized performance, and adapted logic for large-population cities.
* **Citizen Behavior System:** Fixed vanilla bugs, optimized performance, improved logic for large cities.
* **Job Search System:** Optimized performance and improved algorithms for large-population cities.
* **Rent System:** Fixed vanilla bugs, improved logic for large populations, and adjusted rent-to-income/savings ratio (previously hardcoded at 100% in vanilla).

### [Other Fixes]

* Improved overall code stability.
* Code optimizations: Switched to Burst-friendly math libraries, cleaned up leftover Debug logic in BurstJobs, and improved various internal algorithms.

---

### 主要改动 (Chinese Patch Notes)

#### 【新增功能】

**1. CellMap 分辨率提升至原版水平**

* 改善了空气污染、地下水污染、噪音污染、地价、吸引力、交通视图、宜居度、人口质量密度等各大底层系统的模拟计算与 UI 显示精度。

**2. 经济系统逻辑与性能优化（实验性测试）**
专门调整底层运算，使其更适用于百万人口级别的大型城市：

* **各区域需求（住宅/商业/工业/办公）：** 修复原版 Bug，改善算法逻辑以适配海量人口城市。
* **地价系统：** 修复了环境因素仅供 UI 面板显示而不参与到经济模拟计算里的底层缺陷。*注意：真实地价回归正常水平，您在使用中可能会看到高租金警告，这是符合机制的。*
* **居民找房系统：** 修复了原版的运算缺陷，有效抑制大规模空置率；大幅优化算法提高性能；改善逻辑以适配高人口城市环境。
* **居民行为系统：** 修复原版行为逻辑 Bug；极大地优化算法，降低高人口带来的 CPU 卡顿。
* **找工作系统：** 优化性能分配并重构了相关求职调度逻辑。
* **租金系统：** 修复了原版的扣款与判定 Bug；大幅改善了适应大型城市的支付逻辑；重新调整了租金占家庭收入和储蓄的比例（因原版此项数值始终锁死为荒谬的 100%）。

#### 【其他修复】

* 改善了核心处理代码的整体稳定性。
* 内部性能调优：全面更换可由底层 Burst 编译器执行的数学库，清理了多个 BurstJobs 中的废弃 Debug，并优化了若干并发算法。
