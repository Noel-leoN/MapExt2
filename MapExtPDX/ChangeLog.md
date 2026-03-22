## v2.2.7 - Economy Overhaul Expansion

### [New Features]

* **Consumer & Service Pathing:** Added optimized resource-buying and service-coverage systems, reducing CPU overhead from extreme-distance pathfinding on large maps.
* **Resident AI Pathing:** Fixed citizen pathfinding wait-time logic flaws and mitigated memory overflow on large maps.
* **Configurable Pathfind Costs:** New sliders in settings to tune max pathfind cost for Shopping, Company Delivery, Leisure, Job Search, and Home Search — lets you balance realism vs. performance.
* **Per-System Toggle:** Each economy sub-system can now be individually enabled/disabled from settings (restart required).

### [Fixes & Improvements]

* **Industrial Demand:** Updated office resource demand weight (×2 → ×3) to match official IceFlake patch.
* **Job Search Trigger:** Lowered minimum vacancy threshold (50 → 5) and added a 20% floor on unemployed search probability.
* **Job Matching:** Fixed a `#if DEBUG` compilation bug causing logic misalignment in release builds; added cleanup for ghost commuters.
* **Home Search:** Fixed mixed-use buildings miscounting company tenants as residential; added random traversal offset for fairer property evaluation.

---

### 主要改动 (Chinese Patch Notes)

#### 【新增功能】

* **消费采购与服务覆盖系统：** 新增优化后的资源采购与服务覆盖寻路系统，大幅降低大地图下超远寻路产生的 CPU 开销。
* **居民AI寻路补丁：** 修复市民寻路AI等待时间逻辑缺陷，缓解大地图内存溢出问题。
* **寻路成本可调：** 设置面板新增5个滑块，可分别调节购物/公司货运/休闲/找工作/找房的最大寻路成本，兼顾性能与真实感。
* **子系统独立开关：** 各经济补丁子系统均可在设置中单独启禁用（需重启游戏生效）。

#### 【修复与改进】

* **工业需求：** 办公资源需求权重由2倍提升至3倍，跟进官方补丁。
* **找工作触发：** 最低空缺阈值从50降至5，失业者搜索概率增设20%保底。
* **求职匹配：** 修复 `#if DEBUG` 编译期逻辑错位 Bug；新增幽灵通勤者清理。
* **找房系统：** 修复混合建筑（商住）公司租户被误算为住宅满员的问题；遍历候选房产时引入随机偏移以提高公平性。

#### 【兼容性提示】

* ⚠️ 「消费/服务覆盖」与「居民AI」补丁与 **Realistic PathFinding** 等寻路 Mod 不兼容，可关闭对应开关避免冲突。
* ⚠️ 「找工作」补丁与 **Realistic JobSearch** 等 Mod 不兼容。

---

## v2.2.5 - Economy & Demand Balance Update

### [New Features and Improvements]

* **Demand Balance Adjustments:** Overhauled RCI demand sensitivity. Highly penalizes immigration during extreme unemployment (enforcing a 4.5% NAIRU baseline) and unlocks strong industrial stimulus during labor surplus to encourage export-oriented economies. Alleviated the low-density residential "instant max demand" issue.

---

### 主要改动 (Chinese Patch Notes)

#### 【经济与供需平衡重构】

* **住宅惩罚**：纠正了原版20%容忍失业率的谬误，强制实施4.5%自然失业率红线，重罚高失业对人口涌入的吸引力。
* **低密度刚需缓解**：下调了低密度别墅由于容量极小导致的极易触发“零空置率最高需求补偿”的敏感度。
* **工业激活**：彻底放宽了大量失业人口（廉价劳动力红利）对工业建厂的刺激上限，激活后期的“出口导向型加工厂”硬核物流玩法。

---

## v2.2.4 - Maintenance and Optimization Update

### [New Features and Improvements]

* **Housing Search System Optimization:** Fixed logic flaws in homeless property search to reduce pathfinding loops and improve efficiency.
* **Economic System Patch Isolation:** Decoupled economic modules between different map modes (ModeA/B/C/E) for better stability and easier maintenance.
* **Simulation Refinement:** Adjusted internal rent and behavior weights for more stable economic growth in mega-cities.

### [Other Fixes]

* Fixed minor bugs in Burst job execution and improved code robustness.

---

### 主要改动 (Chinese Patch Notes)

#### 【新增与改进】

**1. 找房系统优化**

* 优化了流浪家庭寻找住所的判定逻辑，修复了寻找休息点时的潜在死循环问题，降低了 CPU 寻路开销。

**2. 经济系统补丁隔离**

* 实现了各尺寸地图模式下经济补丁的逻辑解耦，避免了跨模式的逻辑干扰。

**3. 模拟逻辑微调**

* 针对百万人口级城市微调了交租与市民行为权重，提升了整体经济模拟的平顺度。

#### 【其他修复】

* 修复了若干 Burst 任务中的微小逻辑错误，提升系统稳定性。

---

## v2.2.3 - Major Update

### [New Features]

* **CellMap Resolution Improvement**

* CellMap resolution raised to vanilla levels, improving simulation accuracy and UI display for: Air, Ground, Noise pollution, Land Value, Attractiveness, Traffic view, Livability, Population density, etc.

**Economic System Logic and Performance Optimization (Experimental)**
Logic and performance tuned for mega cities (population in the millions):

* **Demand System (Residential/Commercial/Industrial/Office):** Fixed vanilla bugs and improved algorithms for large-population cities.
* **Land Value System:** Fixed issue where environmental factors only affected UI display but not economic simulation. 💡 Note: Land values now behave more realistically, so legitimate high rent warnings may appear more frequently.
* **Housing Search System:** Fixed vanilla bugs, reduced vacancy rates, optimized performance, and adapted logic for large-population cities.
* **Citizen Behavior System:** Fixed vanilla bugs, optimized performance, improved logic for large cities.
* **Job Search System:** Optimized performance and improved algorithms for large-population cities.
* **Rent System:** Fixed vanilla bugs, improved logic for large populations, and adjusted rent-to-income/savings ratio (previously hardcoded at 100% in vanilla).

### [Other Fixes]

* Improved overall code stability.
* Code optimizations.

---

### 主要改动 (Chinese Patch Notes)

#### 【新增功能】

**1. CellMap 分辨率提升至原版水平**

* 改善了空气污染、地下水污染、噪音污染、地价、吸引力、交通视图、宜居度、人口质量密度等各大底层系统的模拟计算与 UI 显示精度。

**2. 经济系统逻辑与性能优化（实验性测试）**
专门调整底层运算，使其更适用于百万人口级别的大型城市：

* **各区域需求（住宅/商业/工业/办公）：** 修复原版 Bug，改善算法逻辑以适配海量人口城市。
* **地价系统：** 修复了环境因素仅供 UI 面板显示而不参与到经济模拟计算里的底层缺陷。💡 注意：真实地价回归正常水平，您在使用中可能会看到高租金警告，这是符合机制的。
* **居民找房系统：** 修复了原版的运算缺陷，有效抑制大规模空置率；大幅优化算法提高性能；改善逻辑以适配高人口城市环境。
* **居民行为系统：** 修复原版行为逻辑 Bug；极大地优化算法，降低高人口带来的 CPU 卡顿。
* **找工作系统：** 优化性能分配并重构了相关求职调度逻辑。
* **租金系统：** 修复了原版的扣款与判定 Bug；大幅改善了适应大型城市的支付逻辑；重新调整了租金占家庭收入和储蓄的比例（因原版此项数值始终锁死为荒谬的 100%）。

#### 【其他修复】

* 改善了核心处理代码的整体稳定性。
* 内部性能调优。
