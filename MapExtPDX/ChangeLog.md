## v4.4.0 - New 43km Map Size Mode

* **[New]:** Added a new map size mode - ModeD: 43km (3x3 vanilla). It sits between 28km and 57km, offering a larger playable area than 28km while keeping terrain detail (DEM 10.5m) sharper than 57km.
* **[Note]:** Remember to fully restart the game after switching map size modes before loading a save.
* **[Note]:** The vanilla 14km save conversion feature still supports 28km and 57km only; ModeD is not part of that conversion path.
* **[Economy]:** Reworked residential, commercial, and industrial demand to scale with city size instead of fixed thresholds.
* **[Economy]:** Residential demand now uses a realistic natural unemployment rate and ratio-based vacancy handling, so population inflow reflects job availability and housing demand is not over-suppressed in large cities.
* **[Economy]:** Commercial building demand tolerance now scales with population on large maps.
* **[Economy]:** Raised the industrial cheap-labor effect to support export-oriented industry, and corrected a vanilla office demand calculation that could keep office demand at maximum.
* **[Performance]:** Optimized the industrial demand labor calculation.

---

### 主要改动

* **[新增]：** 新增地图尺寸模式 ModeD：43km（3x3 原版尺寸）。介于 28km 与 57km 之间，可玩范围大于 28km，同时地形精度（DEM 10.5m）比 57km 更细腻。
* **[提醒]：** 切换地图尺寸模式后，加载存档前请务必完全重启游戏。
* **[提醒]：** 原版 14km 存档转换功能仍仅支持 28km 与 57km，ModeD 不纳入该转换路径。
* **[經濟]：** 重構住宅、商業、工業需求，改以城市規模動態縮放取代原本的固定門檻判定。
* **[經濟]：** 住宅需求改用貼近現實的自然失業率與比率化空置判定，人口流入更能反映就業狀況，大城市的住宅需求亦不再被過度壓制。
* **[經濟]：** 大地圖的商業建築需求容忍度改為隨人口動態調整。
* **[經濟]：** 放寬工業的廉價勞動力效應以支援出口導向型工業，並修正一處會使辦公需求持續觸頂的原版計算問題。
* **[效能]：** 最佳化工業需求的勞動力計算。

---

## v4.3.0 - Game 1.6.0f Compatibility

* **[Compatibility]:** Updated the mod for Cities: Skylines II 1.6.0f.
* **[Fix]:** Citizens without a car and elementary school students are no longer incorrectly dismissed from their job or school when destinations are far away on large maps.
* **[Fix]:** Households that lost their property are now correctly marked as homeless.
* **[Economy]:** Building rent calculation updated to match the 1.6.0f garbage fee changes.
* **[Settings]:** The Find Job pathfinding slider now uses an absolute cost value, consistent with the other pathfinding sliders.
* **[Warning]:** Due to major vanilla economy adjustments, some mod economy features may have balance issues. Please report any problems you encounter.

---

### 主要改动

* **[兼容性]：** 适配《城市：天际线 2》1.6.0f 版本。
* **[修复]：** 大地图上无车市民与小学生不再因目的地过远而被异常解雇或退学。
* **[修复]：** 修复失去房产的家庭未被正确标记为无家可归的问题。
* **[经济]：** 建筑租金计算适配 1.6.0f 的垃圾费机制调整。
* **[设置]：** 找工作寻路滑块改为绝对成本数值，与其他寻路滑块单位统一。
* **[提醒]：** 鉴于原版经济系统进行了较大调整，Mod 经济修改部分可能存在数值失衡，如遇问题请及时反馈。
