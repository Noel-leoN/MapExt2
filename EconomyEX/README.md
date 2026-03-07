# EconomyEX

Economy system fix mod for Cities: Skylines 2. Replaces several vanilla ECS simulation systems to address logic defects and performance bottlenecks that become increasingly severe as city population grows into the hundreds of thousands or millions.

## Why This Mod

The vanilla economy simulation systems were designed for moderate city sizes. As population scales beyond ~200k, several issues emerge:

- **Demand stalling**: The residential demand bar locks at 0 or max because the vanilla formula uses hard cutoffs based on free property thresholds that do not scale with city size.
- **Mass eviction spirals**: The rent adjustment system evaluates every household every frame, triggering excessive relocations from temporary income dips in large cities.
- **Job search flooding**: All unemployed citizens generate job search requests simultaneously without throttling, creating massive ECS entity spikes at high population.
- **Housing search starvation**: Homeless households compete equally with improvement-seeking households, causing the homeless to remain in shelters indefinitely.
- **Shopping traffic explosion**: Household shopping frequency is independent of population, leading to unrealistic traffic volumes in large cities.
- **Land value instability**: The vanilla land value system can produce frame-to-frame oscillations and uses memory allocations that overflow on large maps.

This mod replaces the affected systems with rewritten versions that solve these issues while maintaining gameplay balance.

## Modified Systems

### Residential Demand (ResidentialDemandSystem)

**Vanilla issue**: The demand formula uses `FreeResidentialRequirement` as a hard threshold — when free properties exceed this value, building demand drops to zero regardless of other factors. This causes the demand bar to lock at extremes in larger cities where property counts are far above/below the vanilla threshold.

**Mod changes**:

- Replaces the hard threshold with a smoothed vacancy model. Demand responds proportionally to how far the vacancy rate deviates from a target rate, rather than jumping between 0 and full.
- When vacancy exceeds a panic threshold, demand is gradually reduced using a smooth curve instead of being abruptly cut off.
- Low, medium, and high density demands are calculated independently as separate factors rather than being derived from a shared household demand value.
- The homeless effect is normalized relative to population instead of using a fixed count, so it remains meaningful at all city sizes.
- Student coverage is calculated as a ratio of available slots to population rather than using an absolute count.

**Performance**: Uses Burst-compiled IJob. Temporary data uses TempJob allocator to prevent memory overflow at large population counts.

---

### Commercial Demand (CommercialDemandSystem)

**Vanilla issue**: Commercial demand can become disconnected from actual consumer demand and employment in large cities.

**Mod changes**:

- Rewritten demand calculation that better reflects the relationship between available workforce, consumer population, and existing commercial capacity.

**Performance**: Burst-compiled IJob with TempJob allocator.

---

### Industrial Demand (IndustrialDemandSystem)

**Vanilla issue**: The workforce effect mapping does not scale correctly, causing industrial demand to stagnate at high population.

**Mod changes**:

- Ported from MapExt with corrected workforce effect mapping, so industrial zones respond more accurately to labor supply changes.
- Separate calculation paths for industrial, office, and storage zones.

**Performance**: Burst-compiled IJob with TempJob allocator.

---

### Rent Adjustment (RentAdjustSystem)

**Vanilla issue**: The system checks every household against rent affordability on every update cycle. In large cities, this means thousands of households simultaneously enter the property-seeking state due to temporary income fluctuations (e.g., a citizen switching jobs), causing cascading relocations.

**Mod changes**:

- Household affordability is now calculated as a weighted combination of income and savings, rather than using raw income plus total bank balance. This prevents wealthy households from being falsely flagged as unable to afford modest rent, and prevents poor households with temporary windfalls from ignoring unaffordable housing.
- High-rent relocation checks are staggered across frames — each household is only checked once per period rather than every frame. This prevents mass simultaneous relocation events.
- Low-rent upgrade seeking (households looking for better housing when they can easily afford their current rent) is also throttled with a randomized probability per period, preventing excessive property churn.
- Invalid renters (missing resource buffers) are cleaned up immediately instead of being silently skipped.
- Company/business rent logic is unchanged from vanilla.

**Performance**: Burst-compiled IJobChunk with parallel scheduling.

---

### Land Value (LandValueSystem)

**Vanilla issue**: The vanilla system inherits `CellMapSystem<LandValueCell>`, which uses `Allocator.Temp` for internal buffers. On large maps (>57km), these temporary allocations overflow. Additionally, land values can oscillate frame-to-frame because values are set directly rather than interpolated.

**Mod changes**:

- Fully replaced land value system (not just patched). Uses lerp interpolation for value updates, so land values change smoothly over time instead of jumping to new values each frame.
- Land value propagation along roads uses distance-decayed weighting within a fixed search radius, replacing the vanilla averaging approach. Properties near high-value roads benefit proportionally.
- Environmental factors (ground/air/noise pollution, terrain attractiveness, service coverage, telecom coverage) are sampled and applied uniformly per road edge.
- Uses Harmony Prefix to redirect vanilla `CellMapSystem<LandValueCell>` API calls (GetMap, GetData, AddReader, AddWriter), ensuring all other game systems and the UI heat map read the mod's land value data instead of the disabled vanilla data.

**Performance**: Edge calculation uses Burst-compiled IJobChunk with parallel scheduling. Grid rasterization uses IJobParallelFor. All allocations use TempJob to avoid overflow on large maps.

---

### Citizen Find Job (CitizenFindJobSystem)

**Vanilla issue**: The system uses a fixed cooldown (5000–10000 frames) between job searches and creates unlimited job seeker entities per frame. In cities with 500k+ population, this generates tens of thousands of job seeker entities in a single frame, creating massive ECS structural changes and pathfinding queue spikes.

**Mod changes**:

- Introduces a market saturation model: the cooldown between job searches adjusts dynamically based on the ratio of vacant positions to the healthy vacancy rate. When jobs are abundant, cooldowns are short and citizens find work quickly. When jobs are scarce, cooldowns increase significantly, preventing citizens from repeatedly searching for non-existent positions.
- Enforces a per-frame cap on new job seeker entities. Once the cap is reached, remaining citizens simply have their cooldown timestamp updated without creating seeker entities. This spreads the job search load across multiple frames.
- Employed citizens only attempt to switch jobs when higher-level positions are available in sufficient quantity, preventing pointless job-switching cycles where citizens swap between equivalent positions.
- Uses lock-free atomic counting to track seeker creation across parallel job threads.

**Performance**: Burst-compiled IJobChunk. Per-frame seeker cap with lock-free Interlocked.Increment prevents pathfinding system overload. Dynamic cooldowns reduce the total number of search operations in saturated job markets.

---

### Find Job (FindJobSystem)

**Vanilla issue**: The job matching and hiring process runs as a single monolithic pass, which at high population creates long dependency chains.

**Mod changes**:

- Splits the process into two distinct phases: `ProcessJobSeekers` (initiating pathfind searches for job seekers) and `ProcessJobResults` (processing completed pathfind results and performing the actual hiring).

**Performance**: Burst-compiled with parallel scheduling. Two-phase design allows better job dependency management.

---

### Household Find Property (HouseholdFindPropertySystem)

**Vanilla issue**: Homeless households and improvement-seeking households share the same processing queue. In large cities, the queue is dominated by improvement seekers, causing genuinely homeless households to wait indefinitely for shelter.

**Mod changes**:

- Homeless households and normal households use separate queries. Homeless households are scheduled first with a higher per-update processing limit (1024 vs 256 for normal households), ensuring they always get processing priority.
- Households with income that are temporarily homeless will avoid shelters and prioritize searching for regular housing, preventing the shelter-to-house cycling problem.
- A cooldown system prevents the same household from repeatedly entering the property search queue. Failed searches incur a longer cooldown than successful ones.
- Property scoring considers rent affordability relative to household income, and shelters receive a fixed score penalty to discourage housed families from "downgrading" to shelters.

**Performance**: Burst-compiled IJobChunk with parallel scheduling. Per-update processing limits prevent the system from consuming excessive frame time. Debug telemetry tracks processing counts, hit rates, and failure reasons.

---

### Household Behavior (HouseholdBehaviorSystem)

**Vanilla issue**: The system recalculates household economic data (income, wealth, age distribution, happiness) from scratch for every household every tick. At 500k+ population, this becomes a significant performance bottleneck. Additionally, shopping trip generation does not account for population size, causing unrealistic traffic volumes.

**Mod changes**:

- Introduces a HouseholdCache that pre-computes family size, total wealth, vehicle count, age distribution, income, and average happiness once per household per tick. All subsequent calculations reference this cache instead of re-querying component data.
- Shopping frequency scales inversely with city population — larger cities generate proportionally fewer shopping trips per household, significantly reducing traffic load without affecting the economic simulation.
- Resource consumption weights consider age structure (children, teens, adults, elderly) and wealth level, creating more varied consumption patterns across different household types.
- Move-away probability uses a non-linear exponential decay model based on happiness. At low happiness, the move-away probability is high but not absolute. At moderate happiness, the probability drops sharply. This prevents mass exodus events where all unhappy households leave simultaneously.

**Performance**: Burst-compiled IJobChunk. HouseholdCache eliminates redundant component lookups that dominate frame time at high population. Population-based traffic reduction decreases pathfinding load in large cities.

## Compatibility

- On startup, the mod detects MapExt (MapExtPDX). If MapExt is present, this mod does not load any patches because MapExt includes its own economy fixes.
- The mod automatically enables for standard maps and disables for large maps (>14km). Large map users should use MapExt instead, which handles large map memory requirements.
- A runtime conflict monitor continuously verifies that mod systems and vanilla systems have the correct enabled/disabled state.

## License

MIT License · Copyright (c) 2024 Noel2 (Noel-leoN)

---

# EconomyEX（中文说明）

Cities: Skylines 2 经济系统修正模组。替换原版多个 ECS 模拟系统，修复随城市人口增长到数十万乃至百万级别后逐渐暴露的逻辑缺陷和性能瓶颈。

## 为什么需要这个模组

原版经济模拟系统针对中等规模城市设计。当人口超过约 20 万后，以下问题会逐渐显现：

- **需求条卡死**：住宅需求条锁定在 0 或满格，因为原版公式使用不随城市规模缩放的硬性属性阈值。
- **大规模驱逐连锁**：租金系统每帧检查所有家庭，大城市中短暂的收入波动（如市民换工作）会同时触发大量家庭搬迁。
- **求职洪峰**：所有失业市民在没有节流的情况下同时发起求职请求，在高人口时产生巨量 ECS 实体。
- **无家可归者饥饿**：无家可归的家庭与改善住房的家庭共享同一处理队列，前者被后者淹没而长期滞留庇护所。
- **购物交通爆炸**：家庭购物频率与人口规模无关，大城市产生不切实际的交通量。
- **地价不稳定**：原版地价系统逐帧振荡，并且在大地图上内存分配会溢出。

本模组替换受影响的系统，解决上述问题并保持游戏平衡。

## 修改的系统

### 住宅需求 (ResidentialDemandSystem)

**原版问题**：需求公式使用 `FreeResidentialRequirement` 作为硬阈值——当空置房产超过此值时建筑需求直接归零。在大城市中房产数量远超原版阈值，导致需求条卡在极端值。

**模组修改**：

- 用平滑空置率模型替代硬阈值。需求根据空置率偏离目标值的程度成比例变化，而不是在 0 和满之间跳变。
- 空置率超过警戒阈值时，需求通过平滑曲线逐步降低，而非突然截断。
- 低、中、高密度各自独立计算需求因子，而非从共享的家庭需求值派生。
- 无家可归效应相对于人口进行归一化，在任何城市规模下都有意义。
- 学生覆盖率使用学位数/人口比例计算，而非使用绝对数量。

**性能**：使用 Burst 编译的 IJob。临时数据使用 TempJob 分配器，防止大人口时内存溢出。

---

### 商业需求 (CommercialDemandSystem)

**原版问题**：商业需求在大城市中与实际消费需求和就业脱节。

**模组修改**：

- 重写需求计算，更准确地反映劳动力、消费人口和现有商业容量之间的关系。

**性能**：Burst 编译的 IJob，TempJob 分配器。

---

### 工业需求 (IndustrialDemandSystem)

**原版问题**：劳动力效应映射未正确缩放，导致高人口时工业需求停滞。

**模组修改**：

- 从 MapExt 移植并修正了劳动力效应映射，工业区对劳动力供给变化响应更准确。
- 工业、办公、仓储各有独立计算路径。

**性能**：Burst 编译的 IJob，TempJob 分配器。

---

### 租金调整 (RentAdjustSystem)

**原版问题**：系统在每次更新周期检查所有家庭的租金承受力。在大城市中，数千家庭因临时收入波动（例如市民换工作）同时进入搬迁状态，引发连锁搬迁。

**模组修改**：

- 家庭承受力改为综合考虑收入和储蓄，而非简单使用原始收入加银行余额。防止富裕家庭被错误标记为"负担不起"，也防止暂时获得意外之财的贫困家庭忽视不可承受的房租。
- 高租金搬迁检查在帧之间交错进行——每个家庭每个周期只检查一次，不再每帧检查。防止大规模同时搬迁。
- 低租金升级寻房（家庭在轻松负担当前房租时寻找更好的住房）也通过随机概率节流，防止过度房产流转。
- 资源缓冲区缺失的无效租户会被立即清理，而不是被静默跳过。
- 企业/商业租金逻辑与原版一致。

**性能**：Burst 编译的 IJobChunk，并行调度。

---

### 地价 (LandValueSystem)

**原版问题**：原版系统继承 `CellMapSystem<LandValueCell>`，内部缓冲区使用 `Allocator.Temp`，在大地图（>57km）上溢出。此外地价逐帧直接赋值导致数值振荡。

**模组修改**：

- 完全替换地价系统。值更新使用 lerp 插值，使地价随时间平滑变化而非逐帧跳变。
- 地价沿道路网络传播时使用距离衰减权重，在固定搜索半径内计算，替代原版的平均值方式。临近高价道路的房产按比例受益。
- 环境因素（地面/空气/噪声污染、地形吸引力、服务覆盖、通讯覆盖）按道路边统一采样计算。
- 通过 Harmony Prefix 重定向原版 `CellMapSystem<LandValueCell>` 的 API 调用（GetMap、GetData、AddReader、AddWriter），确保所有其他游戏系统和 UI 热力图读取模组的地价数据。

**性能**：边计算使用 Burst 编译的 IJobChunk 并行调度。网格栅格化使用 IJobParallelFor。所有分配使用 TempJob 以避免大地图溢出。

---

### 市民找工作 (CitizenFindJobSystem)

**原版问题**：系统使用固定冷却时间（5000–10000 帧）且每帧可创建无限求职实体。50 万以上人口城市中，单帧内可产生数万个求职实体，造成大量 ECS 结构变更和寻路队列峰值。

**模组修改**：

- 引入就业市场饱和度模型：求职冷却时间根据空缺率与健康空缺率的比值动态调整。岗位充裕时冷却短、市民快速找到工作；岗位稀缺时冷却大幅增加，避免市民反复搜索不存在的职位。
- 每帧新建求职实体设有上限。达到上限后，剩余市民只更新冷却时间戳而不创建实体，将求职负载分散到多帧。
- 在职市民仅在有足够数量的更高级别空缺时才尝试跳槽，防止在同级职位间无意义轮换。
- 使用无锁原子计数追踪并行线程中的求职者创建数。

**性能**：Burst 编译的 IJobChunk。每帧上限配合无锁计数防止寻路系统过载。动态冷却减少饱和市场中的搜索操作总量。

---

### 就业匹配 (FindJobSystem)

**原版问题**：求职匹配和入职流程在单次遍历中完成，高人口时产生长依赖链。

**模组修改**：

- 将流程拆分为两个独立阶段：`ProcessJobSeekers`（为求职者发起寻路）和 `ProcessJobResults`（处理已完成的寻路结果并执行入职）。

**性能**：Burst 编译，并行调度。两阶段设计允许更好的 Job 依赖管理。

---

### 家庭找房 (HouseholdFindPropertySystem)

**原版问题**：无家可归家庭和改善住房家庭共享同一处理队列。大城市中改善型需求主导队列，真正无家可归的家庭被迫无限等待。

**模组修改**：

- 无家可归家庭和普通家庭使用独立查询。无家可归者优先调度，且每次更新处理上限更高（1024 vs 普通家庭 256），确保始终获得处理优先权。
- 有收入的临时无家可归家庭会跳过庇护所、优先搜索正常住宅，防止"庇护所-住宅"反复循环。
- 冷却系统防止同一家庭反复进入搜房队列。搜索失败的冷却时间长于成功的冷却时间。
- 房产评分考虑租金与家庭收入的可承受性，庇护所有固定评分惩罚，阻止有房家庭"降级"到庇护所。

**性能**：Burst 编译的 IJobChunk，并行调度。每次更新处理上限防止系统消耗过多帧时间。调试遥测追踪处理计数、命中率和失败原因。

---

### 家庭行为 (HouseholdBehaviorSystem)

**原版问题**：系统每 tick 从零开始重新计算每个家庭的经济数据（收入、财富、年龄分布、幸福度）。50 万以上人口时成为显著性能瓶颈。此外购物出行生成不考虑人口规模，导致交通量不合理。

**模组修改**：

- 引入 HouseholdCache，每个家庭每 tick 预计算家庭规模、总财富、车辆数、年龄分布、收入和平均幸福度。后续所有计算引用此缓存，不再重复查询组件数据。
- 购物频率与城市人口呈反比——大城市每户产生的购物出行比例更低，在不影响经济模拟的前提下大幅减少交通负载。
- 资源消费权重考虑年龄结构（儿童、青少年、成年人、老年人）和财富等级，不同类型家庭的消费行为更有差异化。
- 搬离概率使用基于幸福度的非线性指数衰减模型。低幸福度时搬离概率高但非绝对；中等幸福度时概率急剧下降。防止所有不满家庭同时离开的"大逃亡"事件。

**性能**：Burst 编译的 IJobChunk。HouseholdCache 消除在高人口时占据帧时间的重复组件查询。人口相关交通缩减降低大城市寻路负载。

## 兼容性

- 启动时检测 MapExt（MapExtPDX）。如检测到则不加载任何补丁，因为 MapExt 包含自己的经济修正。
- 根据地图尺寸自动启停：标准地图启用，大地图（>14km）不启用。大地图用户请使用 MapExt。
- 运行时冲突监控持续检查模组系统和原版系统的启用/禁用状态是否正确。

## 许可证

MIT License · Copyright (c) 2024 Noel2 (Noel-leoN)
