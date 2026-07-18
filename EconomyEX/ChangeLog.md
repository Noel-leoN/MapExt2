## v4.5.0 - Economy Rework and Performance

* **[Economy]:** Reworked residential, commercial, and industrial demand calculation to align with the vanilla 1.6.0f economy.
* **[Economy]:** Household relocation and eviction decisions are now handled by the household behavior system using a probability-based model, matching the vanilla 1.6.0f division of labor and reducing citywide relocation congestion.
* **[Change]:** Removed the Beta downstream AI systems (PersonalCarAI, TaxiAI, Leisure, FindSchool) from EconomyEX to keep the mod focused on core economy fixes.
* **[Fix]:** Improved the vehicle rescue system and added a debug scan tool to manage stranded ghost vehicles.
* **[Performance]:** Optimized sea level and map size info retrieval to reduce UI rendering overhead.

---

### 主要變動

* **[經濟]：** 重構住宅、商業、工業的需求計算，使其與原版 1.6.0f 的經濟機制對齊。
* **[經濟]：** 家庭的換房與驅離判定改由家庭行為系統以機率模型處理，與原版 1.6.0f 的系統分工一致，並降低全城搬遷造成的壅塞。
* **[調整]：** 自 EconomyEX 移除 Beta 下游 AI 系統（私家車 AI、計程車 AI、休閒、找學校），讓本 Mod 聚焦於核心經濟修復。
* **[修復]：** 改善車輛救援系統，並於設定選單加入除錯掃描工具以管理滯留的幽靈車。
* **[效能]：** 最佳化海平面與地圖尺寸資訊的讀取，以降低介面渲染開銷。
