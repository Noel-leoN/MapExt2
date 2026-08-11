## v4.8.0 - Heightmap Export, Load Validation and Save Reliability

* **[Feature]:** Added a heightmap export button under the Debug tab that saves the current terrain as a 16-bit PNG the Map Editor can import directly.
* **[Map Size]:** Loading a map created in a different MapSize mode now shows a warning dialog instead of silently sampling terrain heights at the wrong scale.
* **[Change]:** Removed the experimental Water Async Compute option, which is incompatible with the water simulation and could leave water levels incorrect after terrain edits.
* **[Fix]:** Reworked how the vehicle rescue system cleans up stranded vehicles, addressing an autosave failure reported on a large save with the system enabled.
* **[Fix]:** The vehicle rescue system now handles stranded vehicles in batches instead of processing over a thousand of them in a single frame when loading an older save.
* **[Fix]:** Loading a second save within the same session could make the vehicle rescue system act on unrelated vehicles.

---

### 主要變動

* **[功能]：** 於開發者選項新增高度圖匯出按鈕，將當前地形輸出為地圖編輯器可直接匯入的 16-bit PNG。
* **[地圖尺寸]：** 載入以其他地圖尺寸模式製作的地圖時會顯示警告對話框，不再靜默地以錯誤比例取樣地形高度。
* **[調整]：** 移除實驗性的「水體 Async Compute」選項，該選項與水模擬不相容，可能在地形編輯後留下錯誤的水位。
* **[修復]：** 重寫車輛救援系統清理滯留車輛的方式，處理某大型存檔在啟用該系統時自動存檔失敗的問題。
* **[修復]：** 車輛救援系統改為分批處理滯留車輛，載入舊存檔時不再於單一幀內處理上千輛。
* **[修復]：** 同一次遊戲中載入第二個存檔時，車輛救援系統可能對無關車輛執行操作。
