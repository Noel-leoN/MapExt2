## v4.7.0 - Economy Rework, Pathfinding and Simulation Performance

* **[Note]:** This release also includes the changes from v4.5.0 and v4.6.0, which were never published separately.
* **[Economy]:** Reworked residential, commercial, and industrial demand calculation to align with the vanilla 1.6.0f economy.
* **[Economy]:** Household relocation and eviction decisions are now handled by the household behavior system using a probability-based model, matching the vanilla 1.6.0f division of labor and reducing citywide relocation congestion.
* **[Performance]:** Business purchasing and leisure target searches now stop after collecting enough qualified candidates instead of scanning every business or venue in the city.
* **[Performance]:** Added an option to freeze the water simulation while the game is paused, enabled by default.
* **[Performance]:** Added a snow simulation freeze option with automatic and always-on modes, disabled by default.
* **[Performance]:** Added an option to skip the per-frame backdrop terrain downsample when the terrain has not changed, disabled by default and only relevant to maps that have a background world map.
* **[Performance]:** Optimized sea level and map size info retrieval to reduce UI rendering overhead.
* **[Feature]:** Added adjustable candidate caps for business purchasing, leisure, and home searches under Economy Behavior settings.
* **[Map Size]:** Stabilized and hardened the 43km map size mode (ModeD).
* **[Fix]:** Water flow blur stayed disabled after switching the water simulation quality back to Vanilla.
* **[Fix]:** The water surface could resume simulating before it finished adapting to terrain edits, which could leave ripples or incorrect water levels after using the terrain brush.
* **[Fix]:** Citizens moving away had their unemployment timer frozen, which made the happiness penalty for unemployment too light.
* **[Fix]:** Companies selling virtual goods or services were treated as out of stock, causing buyers to retry pathfinding repeatedly.
* **[Fix]:** Improved the vehicle rescue system and added a debug scan tool to manage stranded ghost vehicles.

---

### 主要變動

* **[提醒]：** 本次發版同時包含未單獨發布的 v4.5.0 與 v4.6.0 變更。
* **[經濟]：** 重構住宅、商業、工業的需求計算，使其與原版 1.6.0f 的經濟機制對齊。
* **[經濟]：** 家庭的換房與驅離判定改由家庭行為系統以機率模型處理，與原版 1.6.0f 的系統分工一致，並降低全城搬遷造成的壅塞。
* **[效能]：** 企業採購與休閒目標搜尋在收集到足夠的合格候選後即停止，不再掃描全城所有企業或休閒設施。
* **[效能]：** 新增「暫停時凍結水模擬」選項，預設開啟。
* **[效能]：** 新增「凍結雪模擬」選項，提供自動與始終凍結兩種模式，預設關閉。
* **[效能]：** 新增「背景地形降採樣事件化」選項，地形未變更時跳過每幀降採樣，預設關閉，僅對有背景世界地圖的存檔有效。
* **[效能]：** 最佳化海平面與地圖尺寸資訊的讀取，以降低介面渲染開銷。
* **[功能]：** 於經濟行為設定新增企業採購、休閒與找房的候選數量上限滑桿。
* **[地圖尺寸]：** 加固並穩定 43km 地圖尺寸模式（ModeD）。
* **[修復]：** 水模擬品質切回 Vanilla 後，水流模糊未還原而持續處於關閉狀態。
* **[修復]：** 水面可能在尚未完成適應地形變更前就恢復模擬，導致使用地形筆刷後出現波紋或水位異常。
* **[修復]：** 搬離中的市民失業計時被凍結，導致失業帶來的幸福度懲罰偏輕。
* **[修復]：** 販售虛擬商品或服務的企業被誤判為缺貨，導致採購方反覆重試尋路。
* **[修復]：** 改善車輛救援系統，並於設定選單加入除錯掃描工具以管理滯留的幽靈車。
