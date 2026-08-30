## v4.8.0 - Heightmap Export, Load Validation, Save Reliability, Groundwater and Pollution Fixes

* **[Feature]:** Added a heightmap export button under the Debug tab that saves the current terrain as a 16-bit PNG the Map Editor can import directly.
* **[Map Size]:** Loading a map created in a different MapSize mode now shows a warning dialog instead of silently sampling terrain heights at the wrong scale.
* **[Change]:** Removed the experimental Water Async Compute option, which is incompatible with the water simulation and could leave water levels incorrect after terrain edits.
* **[Change]:** Redrew the in-game HUD button icon as a bolder two-line MAP/EXT mark.
* **[Fix]:** Reworked how the vehicle rescue system cleans up stranded vehicles, addressing an autosave failure reported on a large save with the system enabled.
* **[Fix]:** The vehicle rescue system now handles stranded vehicles in batches instead of processing over a thousand of them in a single frame when loading an older save.
* **[Fix]:** Loading a second save within the same session could make the vehicle rescue system act on unrelated vehicles.
* **[Fix]:** Noise pollution spread was not Burst-compiled and ran on the interpreted code path.
* **[Fix]:** Groundwater was missing entirely in the expanded map size modes, so groundwater pumps and wells had nothing to draw from. The aquifer painted by the map author is now restored at the expanded resolution on load, and saves that were left with an empty aquifer are repaired the same way. Saves that already have a working aquifer are left untouched.
* **[Fix]:** Groundwater updated at one sixteenth of the intended rate, so contamination did not decrease and depleted wells refilled slowly.
* **[Fix]:** In the vanilla map size mode, the reworked residential, commercial and industrial demand calculations were never applied.
* **[Fix]:** Turning on either the vanilla save conversion or the world backdrop option silently cleared the other while the settings screen still showed both as enabled; the two options are now mutually exclusive in the UI.
* **[Performance]:** Air pollution advection and the groundwater replenishment step now run in parallel, and the groundwater and ground pollution passes skip empty cells.
* **[Performance]:** The city statistics panel no longer counts buildings on the main thread.
* **[Performance]:** The vehicle rescue system now scans off the main thread, so looking for stranded vehicles no longer causes a brief stall.

---

### 主要變動

* **[功能]：** 於開發者選項新增高度圖匯出按鈕，將當前地形輸出為地圖編輯器可直接匯入的 16-bit PNG。
* **[地圖尺寸]：** 載入以其他地圖尺寸模式製作的地圖時會顯示警告對話框，不再靜默地以錯誤比例取樣地形高度。
* **[調整]：** 移除實驗性的「水體 Async Compute」選項，該選項與水模擬不相容，可能在地形編輯後留下錯誤的水位。
* **[調整]：** 將遊戲內 HUD 按鈕圖標重繪為加粗雙行 MAP/EXT。
* **[修復]：** 重寫車輛救援系統清理滯留車輛的方式，處理某大型存檔在啟用該系統時自動存檔失敗的問題。
* **[修復]：** 車輛救援系統改為分批處理滯留車輛，載入舊存檔時不再於單一幀內處理上千輛。
* **[修復]：** 同一次遊戲中載入第二個存檔時，車輛救援系統可能對無關車輛執行操作。
* **[修復]：** 噪音污染擴散未經 Burst 編譯，執行於解譯路徑。
* **[修復]：** 擴展地圖尺寸模式下地下水完全不存在，導致地下水泵與水井無水可抽。現在載入時會依擴展後的解析度還原地圖作者繪製的含水層，先前存下空含水層的存檔亦以同一方式修復；含水層已正常的存檔不受影響。
* **[修復]：** 地下水的更新頻率僅為預期的十六分之一，導致污染不會下降、抽乾的水井回補緩慢。
* **[修復]：** 原版地圖尺寸模式下，改良版的住宅／商業／工業需求計算從未生效。
* **[修復]：** 開啟原版存檔轉換或背景地圖選項時會靜默清除另一項，而設定畫面上兩者仍顯示為開啟；兩項現改為介面互斥。
* **[效能]：** 空氣污染的風向平流與地下水的補充淨化計算改為並行運算，地下水與地面污染的處理會跳過空白格。
* **[效能]：** 城市統計面板不再於主執行緒逐一計算建築。
* **[效能]：** 車輛救援系統的掃描改於主執行緒外進行，尋找滯留車輛時不再造成短暫停頓。
