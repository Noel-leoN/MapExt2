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