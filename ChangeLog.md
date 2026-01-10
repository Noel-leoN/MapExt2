### 2.2.0
## Patch Notes – Major Update
### [New Features]
### CellMap Resolution Improvement
#### CellMap resolution raised to vanilla levels, improving simulation accuracy and UI display for:
#### Air/Ground/Noise pollution, LandValue, Attractiveness, Traffic view, Livability, Population density, etc.

### Economic System Logic and Performance Optimization (Experimental)  
#### Logic and performance tuned for mega cities (population in the millions):

#### Demand System (Residential/Commercial/Industrial/Office): Fixed vanilla bugs and improved algorithms for large-population cities.

#### Land Value System: Fixed issue where environmental factors only affected UI display but not economic simulation. Note: land values now behave more realistically, which may trigger high rent warnings.

#### Housing Search System: Fixed vanilla bugs, reduced vacancy rates, optimized performance, and adapted logic for large-population cities.

#### Citizen Behavior System: Fixed vanilla bugs, optimized performance, improved logic for large-population cities.

#### Job Search System: Optimized performance and improved logic for large-population cities.

#### Rent System: Fixed vanilla bugs, improved logic for large-population cities, and adjusted rent-to-income/savings ratio (vanilla was fixed at 100%).

### [Other Fixes]
#### Improved overall code stability.
#### Code optimization: things like switching math libraries to be Burst-friendly, cleaning up leftover Debug stuff in BurstJobs, improving algorithms, and so on.

#### 主要改动：
#### 【新增功能】
#### 1. CellMap分辨率提升到原版水平，包括空气/地下/噪音污染/地价资源/吸引力/交通/宜居度/人口密度等模拟计算与UI显示的精度等。。
#### 2. 经济系统逻辑和性能优化(测试)，使之适用于百万级大型城市。
#### a. 住宅/商业/工业/办公需求：修复原版bug，改善算法逻辑使之适用于大型人口城市。
#### b. 地价系统：修复环境因素地价仅供UI显示而不参与经济模拟计算的问题。注意：地价将处于较为正常水平，可能会出现高租金警告。
#### c. 居民找房系统：修复原版一些bug，抑制空置率；优化算法提高性能；改善逻辑以适配大型人口城市。
#### d. 居民行为系统：修复原版一些bug；优化算法提高性能；改善逻辑以适配大型人口城市。
#### e. 找工作系统：优化算法提高性能；改善逻辑以适配大型人口城市。
#### f. 租金系统：修复原版bug；改善逻辑以适配大型人口城市；修改租金占家庭收入和储蓄比例(原版为100%)。
#### 【其他修复】
####  提高代码稳定性。