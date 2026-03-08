# 🏢 EconomyEX

## 💡 COMPATIBILITY NOTE / 兼容性提示 💡

- **EconomyEX** is fully compatible with **MapExt (MapExtPDX)**. EconomyEX was originally developed as the economy patch module *within* MapExt, and is already built into MapExt for large maps (28km+). If you subscribe to both, EconomyEX will automatically detect MapExt and gracefully disable its own patches to prevent conflicts. This standalone version is specifically aimed at players using standard/smaller maps who want the economy fixes.
- **EconomyEX** 与 **MapExt (MapExtPDX)** 完全兼容。本模组最初是作为 MapExt 内部的“经济修复模块”开发并提取出来的独立版本。大尺寸地图（28km+）的 MapExt 已经内置了完全相同的功能。如果您同时订阅了两者，EconomyEX 内置的探测器会自动识别到 MapExt，并安全地自我休眠以防止任何冲突。这个同名的独立版本，专门提供给游玩标准地图、专注体验经济修复的玩家。

**EconomyEX** is an economy system fix and optimization mod for *Cities: Skylines 2*. It re-engineers several core vanilla simulation systems to solve severe economic stagnation and debilitating PC performance issues that occur when your city grows into a massive Metropolis (500k+ to 1M+ population).

**If your large city suffers from 0 demand, massive unexplained housing abandonments, or extreme simulation slowdowns (lag/stuttering), this mod is for you.**

## ❓ Why This Mod?

The vanilla game is optimized for "Standard Cities" (up to ~150k population). As you build a massive metropolis, the mathematical formulas and agent behaviors in the vanilla game break down:

1. **📉 Demand Deadlocks (Absolute Values)**: The game uses fixed numbers to measure demand. In a 500k city, having just 500 empty homes (0.1% of your city) can artificially crash residential demand to zero.
2. **🐌 Simulation Lag ("Agent Floods")**: Citizens look for jobs, homes, and go shopping at fixed frequencies. In a massive city, this creates a flood of Pathfinding System requests every update tick, completely choking your CPU.
3. **🏚️ Unfair Evictions**: The rent system doesn't properly account for a family's lifetime savings, causing wealthy families to be evicted en masse during brief income dips.
4. **🕵️ "Fake" Land Value**: The visual Land Value map you see in the UI is calculated using a different system than what the economy *actually* uses, causing CPU waste and confusing gameplay.

## 🛠️ What EconomyEX Fixes

### 📈 Demand and Growth Scaling

- **Proportional Demand**: Demand calculations are now based on **Percentages** rather than absolute numbers. Your metropolis will require realistically proportional vacancy rates to stall demand.
- **Fixed Commercial and Industrial**: Commercial demand now scales linearly with your true consumer base. Industrial labor impacts are normalized so a 5% unemployment rate behaves correctly at any city size.

### ⚡ Extreme CPU Optimization (Anti-Lag)

- **Probabilistic Job Seeking**: Instead of thousands of citizens spamming the **Pathfinding System** every update tick, search frequency is dynamically throttled based on population size.
- **Smart Shopping**: As your population explodes, citizens slightly reduce shopping trip frequency (simulating bulk buying), preventing "Traffic Apocalypses" and saving CPU costs.
- **Burst Protection**: Restricts how many homeless families can search for houses simultaneously, preventing game freezes.

### 🏘️ Fair Rent and Unified Land Value

- **Lifesavings Rent Survival**: Families now use their **Income + Savings** to stay in their homes. Truly broke households are the only ones evicted.
- **Unified Land Value**: Merges the "Economic Land Value" and "UI Heat Map" into a single, optimized system. 100% accuracy, zero redundant calculations.

## ⚙️ Compatibility

- Automatically detects and yields to **MapExt (MapExtPDX)**.
- Strictly replaces logical systems via ECS; highly compatible with asset mods and visual tweaks.

---

# 🏢 EconomyEX (中文说明)

**EconomyEX** 是一款针对《都市：天际线 2》的经济系统修正与优化模组。它重构了多个核心原版模拟系统，专门解决当你的城市成长为“大都市”（50万至100万+人口）时出现的严重经济停滞和 PC 性能极度卡顿问题。

**如果你的超大型城市正饱受需求归零、莫名其妙的房屋大面积废弃、或模拟速度极度缓慢（卡顿/掉帧）的折磨，这个模组正是为你准备的。**

## ❓ 为什么需要这个模组？

原版游戏针对的是“标准规模城市”（约15万人口以内）。当你建造一座庞大的大都市时，原版游戏中的数学公式和市民行为就会开始崩溃：

1. **📉 需求死锁（绝对数值问题）**：游戏使用固定的绝对数值来衡量需求。在一个50万人口的城市中，仅仅500套空房子就可能在算法上将住宅需求死死压在零点。
2. **🐌 模拟卡顿（“代理洪流”）**：市民找工作、找房子和购物的频率是固定的。在巨型城市中，这会在每次系统更新周期引发海量的**寻路系统（Pathfinding System）**请求，彻底堵死你的 CPU。
3. **🏚️ 不合理的驱逐**：租金系统没有正确计算家庭的终生储蓄，导致富裕家庭在短暂的收入波动期间被成批驱逐，毁掉城市人口稳定性。
4. **🕵️ “虚假”的地价**：UI 界面看到的地价分布图与经济系统实际使用的地价是两套断层的系统。这不仅浪费了大量 CPU 算力，还经常误导玩家。

## 🛠️ EconomyEX 修复了什么？

### 📈 需求与增长缩放

- **按比例的动态需求**：需求计算现在基于**百分比**而非绝对数值。确保你的城市可以顺滑地增长到 100 万人以上而需求条不会卡死。
- **修复商业与工业**：商业需求现在严格随真实的消费者基数线性增长。工业劳动力影响被标准化，失业率的反馈在任何规模下都保持一致。

### ⚡ 极限 CPU 优化（抗卡顿）

- **概率求职系统**：根据城市人口规模动态降频求职搜索频率。你的市民依然能完美地找到工作，但 **寻路系统** 的 CPU 负载被成倍降低。
- **动态购物频率**：随着人口爆炸，市民会稍微降低购物出行频率（模拟大宗采购）。这防止了“交通末日”并节省了海量的 CPU 算力。
- **爆发保护**：严格限制同一时间段内允许多少无家可归的家庭疯狂请求寻路系统寻找房屋。

### 🏘️ 合理的租金与真实的地价

- **储蓄抗租金机制**：家庭现在会动用他们的“总收入+总储蓄”来对抗高昂的租金。彻底终结了“富人集体无家可归”的死亡螺旋。
- **地价系统大一统**：将“经济地价”和“UI热力图地价”合并为一个单一、深度优化的架构。保证你在屏幕上看到的热力图 100% 精确反映底层经济模拟。

## ⚙️ 兼容性说明

- 启动时自动检测 **MapExt (MapExtPDX)** 模组。EconomyEX 会安全地自动识别并禁用自身补丁以防止冲突。
- 采用 ECS 架构严格替换逻辑系统；与资产模组（Asset Mods）和视觉调整类模组高度兼容。
