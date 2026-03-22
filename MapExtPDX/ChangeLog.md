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
