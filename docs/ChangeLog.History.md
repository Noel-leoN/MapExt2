### 2.2.0
## Patch Notes – Major Update
### [New Features]
### CellMap Resolution Upgrade  
#### Resolution raised to vanilla levels, improving simulation accuracy and UI display for:
#### air pollution, underground pollution, noise pollution, land value, attractiveness, traffic, livability, population density, etc.

### Economic System Logic and Performance Optimization (Experimental)  
#### Logic and performance tuned for mega cities (population in the millions):

#### Demand System (Residential/Commercial/Industrial/Office): Fixed vanilla bugs and improved algorithms for large-population cities.

#### Land Value System: Fixed issue where environmental factors only affected UI display but not economic simulation. Note: land values now behave more realistically, which may trigger high rent warnings.

#### Housing Search System: Fixed vanilla bugs, reduced vacancy rates, optimized performance, and adapted logic for large-population cities.

#### Citizen Behavior System: Fixed vanilla bugs, optimized performance, improved logic for large-population cities, and introduced “virtual consumption” (remote acquisition) to reduce pathfinding load.

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
#### d. 居民行为系统：修复原版一些bug；优化算法提高性能；改善逻辑以适配大型人口城市；增加“虚拟消费”(隔空取物)以减少寻路压力。
#### e. 找工作系统：优化算法提高性能；改善逻辑以适配大型人口城市。
#### f. 租金系统：修复原版bug；改善逻辑以适配大型人口城市；修改租金占家庭收入和储蓄比例(原版为100%)。
#### 【其他修复】
####  提高代码稳定性。一些原始游戏代码优化。

### 2.1.6
- Minor fix.

### 2.1.5
- Compatibility fixes with game version 1.5.2f1.

### 2.1.3
- Fix bug.

### 2.1.2
- Fixe a bug that caused a crash due to a missing piece. Sorry for the inconvenience!

### 2.1.1
  - Updated for 1.4.2f.  Major changes again, please report any issues, thanks.
  - Re-add fixing power plant, water pump that need groundwater.
  - Add fix a few missing minor systems.
  
  - 1.4.2f版本适配。由于大量改动，请及时报告问题，谢谢。
  - 重新添加各类需要地下水的电厂、水泵修复。
  - 补充修复个别遗漏的小系统。

### 2.1.0
  - Updated for 1.3.6f.  Major changes, please report any issues, thanks.
  - Completely remove the unstable 229km mode. 
  - Since the new patch changed the demand system, temporarily disabled the resident housing search fix and will bring it back once it's improved.
  - 1.3.6f版本适配。由于大量改动，请及时报告问题，谢谢。
  - 彻底移除不稳定的229km模式。
  - 由于新补丁改动了需求系统，暂时取消对居民寻房系统的修复，待完善后再推出。

### 2.0.6
  - fix the residential demand issue in 28km/114km mode in the last update.

### 2.0.5
  - Fix vanilla bug: The residential demand disappeared under certain conditions, the game UI showed that there're plenty of citizen moving in, but the actual number of population is decreasing, and the number of empty houses is increasing (as seen in InfoLoom), and the CPU usage is very high. It can more easily happen on the big map. This fix may take quite some time to run before it gradually mitigates.
  - Removed unstable 229km mode.

### 2.0.4
  - Fix network color infoview of residential suitability.

### 2.0.3
  - Fix bug with multiple airport direct connections.

### 2.0.2
  - Due to potential lag issues, temporarily removed the repairs for things like groundwater pumping stations and groundwater power stations.
  - Fixed bugs in the pollution calculation.

### 2.0.1
  - Airway-pathfinding have been repaired.
  - Added the feature that makes the outside airplane connections added in-game actually work.(testing,not sure if it works at all the conditions)
  - Groundwater pumping stations, Wind power stations, Geothermal power stations can now work in the right place, BUT Anarchy mod needs to be turned on.

### 2.0.0
  - Integrated vanilla-14km/28/57/114/229km switchable version.
  - Add LoadGame verification function to prevent the savegames of different mapsizes from being loaded incorrectly.
  - ~~Some minor systems such as Airway-pathfinding, groundwater pumping stations, wind power generation, etc., have been repaired.~~
  - Integrates some of my performance mod gadgets, such as NoDogs, LiteBoost (will be added continuously).
  - Added the OptionUI settings interface.

### 1.1.2
  - update support 1.3.3f game version
  - 更新适配1.3.3f版本

### 1.0.1.0
  - Added feature:  It is now possible to import terrain heightmaps with a resolution <b>lower than 12288x12288</b>, which will be automatically upsampled or downsampled to 4096x4096. This may result in slight improvements to the terrain aliasing. 
  - 增加特色功能：现在可以导入任何<b>低于12288x12288</b>分辨率的地形高位图(heightmap和worldmap),mod将自动上采样/下采样至4096x4096，可能将轻微改善地形边缘锯齿。

### 1.0.0.2
- code building fix
- 构建修改

### v1.0.0.0  
- PDX first version
- PDX初始版本