# 🏢 EconomyEX — Metropolis Economy Fix and Performance Boost

## For**: Cities: Skylines 2 · **Map Size**: Standard / Vanilla maps (14 km)

## ⚠️ Compatibility and Conflicts (Important!)

- **Do NOT install this mod if you already use MapExt (MapExtPDX)**. MapExt already includes this exact economy module built-in. If you subscribe to both, EconomyEX will auto-disable itself to prevent conflicts.
- **Large Maps (28 km+)**: Auto-disables; only activates on vanilla maps.
- **Realistic PathFinding**: ⚠️ Resource Buyer and Resident AI modules may conflict — disable them individually in EconomyEX settings.
- **Realistic JobSearch**: ⚠️ Job Search module may conflict — disable it individually in EconomyEX settings.
- **Asset / Visual Mods**: ✅ Highly compatible (ECS system replacement).

---

## What Does It Do?

When your city grows past **500K–1M** population, the vanilla game suffers from **zero demand, mass housing abandonment, and crippling simulation lag**. EconomyEX re-engineers several core simulation systems to fix these issues without changing core gameplay.

## ✨ Features

### 📈 Demand Fix
- RCI demand now uses **percentages** instead of absolute numbers — no more demand deadlocks in large cities.
- Commercial demand scales linearly with consumers; industrial labor metrics normalized across all city sizes.

### ⚡ Performance (Anti-Lag)
- **Job Search Throttling**: Dynamically reduces pathfinding frequency based on population size.
- **Shopping Throttling**: Slightly reduces shopping trip frequency at high population (simulates bulk buying).
- **Burst Protection**: Caps simultaneous homeless pathfinding requests to prevent frame spikes.

### 🏘️ Rent and Land Value
- **Savings-Based Rent**: Households use income + savings to pay rent — stops unfair mass evictions.
- **Unified Land Value**: Merges the economic and UI heat-map land value into one optimized system.

### 🕹️ Resident AI
- Fixes pathfinding wait-time logic defects, reducing memory overflow risk.

### 🎚️ Adjustable Pathfinding Costs (In-Game)
Five sliders let you tune max pathfinding costs for shopping, freight, leisure, job search, and home search — **no restart required**.

## 📦 Installation
Subscribe on Paradox Mods. No additional dependencies.

---

# 🏢 EconomyEX — 大都市经济修复与性能优化

## **适用版本**：Cities: Skylines 2 · **适用地图**：标准/原版地图（14 km）

## ⚠️ 兼容性与模组冲突（必看！）

- **如果您已安装 MapExt (MapExtPDX)，请完全不需要安装此模组**。MapExt 已经内置了完全相同的经济系统模块。如果同时订阅了两者，EconomyEX 会自动检测并休眠以避免冲突。
- **大地图（28 km+）**：自动检测到大地图后不会激活，此模组仅在原版尺寸地图上生效。
- **Realistic PathFinding** 模组：⚠️ "消费采购"与"居民AI"等有关寻路的模块可能与其冲突，可在本模组设置面板中将这几项单独关闭。
- **Realistic JobSearch** 模组：⚠️ "找工作系统"模块可能与其冲突，可在本模组设置面板中将此项单独关闭。
- **资产 / 视觉 Mod**：✅ 高度兼容，采用 ECS 系统严格替换底层逻辑，不涉及资产改动。

---

## 它能做什么？

当你的城市人口突破 **50 万甚至百万**，原版游戏会出现 **需求归零、房屋大面积废弃、模拟严重卡顿** 等致命问题。EconomyEX 针对以上症状，在不改变游戏核心玩法的前提下，重构了多个底层机制。

## ✨ 功能一览

### 📈 需求修正
- 将住宅/商业/工业需求计算从 **绝对数值** 改为 **百分比**，根治大城市"需求条永远为零"的死锁。
- 商业需求随消费者规模线性增长；工业劳动力指标标准化，让各个人口阶段的系统表现保持一致。

### ⚡ 性能优化（抗卡顿）
- **求职降频**：根据人口规模动态降低求职寻路频率，大幅减轻 CPU 负担。
- **购物降频**：人口越多，购物出行适度降频（模拟大宗采购），缓解交通与海量寻路压力。
- **爆发保护**：严格限制同一时刻允许的最大无房家庭寻路请求数量，防止引发瞬时掉帧与卡顿。

### 🏘️ 租金和地价
- **储蓄抗租**：家庭以"收入 + 储蓄"综合判定是否交得起租金，终结"富人集体驱逐"的崩坏循环。
- **地价统一**：将经济地价与 UI 热力图合并为一套系统，所见即所得，同时消除游戏原版的冗余计算浪费。

### 🕹️ 居民 AI
- 修复居民寻路等待时间的逻辑缺陷，降低在大城市规模下发生内存溢出的风险。

### 🎚️ 可调参数
在设置面板中可 **实时调节** 以下寻路成本上限（修改后立即生效，无需重启）：

| 参数 | 说明 | 建议值 |
|------|------|--------|
| 购物寻路成本 | 市民购物最大出行成本 | 8 000 |
| 公司货运成本 | 工厂/商店补货搜索范围 | 200 000 |
| 休闲寻路成本 | 公园、地标观光出行成本 | 8 000 – 12 000 |
| 找工作成本 | 求职搜索范围 | 200 000 |
| 找房成本 | 搬家找房搜索范围 | 200 000 |

## 📦 安装
在 Paradox Mods 找到本模组并开启即可。无需任何前置依赖。
