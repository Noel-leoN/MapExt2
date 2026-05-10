## v3.0.1 - Dashboard Expansion and World Backdrop Control

* **[UI - Dashboard]:** Reorganized the City Stats dashboard into 5 collapsible accordion sections (City Stats, Residential Market, Commercial Market, Population Activity, Misc). Added 13 new metrics: residential vacancy by density (Low/Med/High), commercial company data (active shops, seeking property), population activity (shopping, leisure, commuting, returning home), and commuter households.
* **[UI - Panel Layout]:** Added panel height persistence and bottom-edge drag resizing. Added 5 toggles in OptionUI to configure which dashboard sections are expanded by default. Removed the font size slider. Adjusted default panel widths.
* **[Performance - DisableWorldBackdrop]:** Added a new toggle in the Performance tab to prevent the background world heightmap (Backdrop) from loading on existing saves. Eliminates per-frame GPU overhead, CPU stalls, and reduces VRAM usage.
* **[Editor - WorldMap Import Warning]:** Added a confirmation dialog in the Map Editor that warns about performance impact before importing a WorldMap image.
* **[Localization]:** Added Chinese Simplified (zh-HANS) translations for save validation and WorldMap warning dialogs.

---

### 主要改动

* **[UI - 仪表盘]：** 将城市统计仪表盘重构为 5 个可折叠区块（城市统计、住宅市场、商业市场、人口活动、其他）。新增 13 项指标：按密度分类的住宅空置率（低/中/高密度）、商业公司数据（有店铺商家、等待入驻）、人口活动状态（购物中、休闲中、上班途中、回家途中）以及外来通勤者家庭数。
* **[UI - 面板布局]：** 新增面板高度持久化与底部拖拽调整功能。在选项面板中新增 5 个开关，可配置仪表盘各区块的默认展开状态。移除字体大小滑块，调整默认面板宽度。
* **[性能 - 禁用背景世界地图]：** 在性能标签页新增开关，可阻止已有存档加载背景世界地图（Backdrop），消除每帧 GPU 开销与 CPU 阻塞，降低显存占用。
* **[编辑器 - WorldMap 导入警告]：** 在地图编辑器中导入 WorldMap 时新增性能影响确认对话框。
* **[本地化]：** 为存档验证与 WorldMap 导入警告对话框新增中文简体 (zh-HANS) 翻译。
