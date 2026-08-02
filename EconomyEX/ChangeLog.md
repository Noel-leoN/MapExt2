## v4.7.0 - Economy Rework and Scope Change

* **[Note]:** This release also includes the changes from v4.5.0 and v4.6.0, which were never published separately.
* **[Economy]:** Reworked residential, commercial, and industrial demand calculation to align with the vanilla 1.6.0f economy.
* **[Economy]:** Household relocation and eviction decisions are now handled by the household behavior system using a probability-based model, matching the vanilla 1.6.0f division of labor and reducing citywide relocation congestion.
* **[Change]:** Removed the Beta downstream AI systems (PersonalCarAI, TaxiAI, Leisure, FindSchool) from EconomyEX to keep the mod focused on core economy fixes.
* **[Feature]:** Added an adjustable candidate cap for household home searches under Economy Behavior settings.
* **[Fix]:** Citizens moving away had their unemployment timer frozen, which made the happiness penalty for unemployment too light.
* **[Fix]:** Companies selling virtual goods or services were treated as out of stock, causing buyers to retry pathfinding repeatedly.
* **[Fix]:** Improved the vehicle rescue system and added a debug scan tool to manage stranded ghost vehicles.

---

### 主要變動

* **[提醒]：** 本次發版同時包含未單獨發布的 v4.5.0 與 v4.6.0 變更。
* **[經濟]：** 重構住宅、商業、工業的需求計算，使其與原版 1.6.0f 的經濟機制對齊。
* **[經濟]：** 家庭的換房與驅離判定改由家庭行為系統以機率模型處理，與原版 1.6.0f 的系統分工一致，並降低全城搬遷造成的壅塞。
* **[調整]：** 自 EconomyEX 移除 Beta 下游 AI 系統（私家車 AI、計程車 AI、休閒、找學校），讓本 Mod 聚焦於核心經濟修復。
* **[功能]：** 於經濟行為設定新增家庭找房的候選數量上限滑桿。
* **[修復]：** 搬離中的市民失業計時被凍結，導致失業帶來的幸福度懲罰偏輕。
* **[修復]：** 販售虛擬商品或服務的企業被誤判為缺貨，導致採購方反覆重試尋路。
* **[修復]：** 改善車輛救援系統，並於設定選單加入除錯掃描工具以管理滯留的幽靈車。
