## v4.8.0 - Vehicle Rescue Reliability

* **[Fix]:** Reworked how the vehicle rescue system cleans up stranded vehicles, addressing an autosave failure reported on a large save with the system enabled.
* **[Fix]:** The vehicle rescue system now handles stranded vehicles in batches instead of processing over a thousand of them in a single frame when loading an older save.
* **[Fix]:** Loading a second save within the same session could make the vehicle rescue system act on unrelated vehicles.

---

### 主要變動

* **[修復]：** 重寫車輛救援系統清理滯留車輛的方式，處理某大型存檔在啟用該系統時自動存檔失敗的問題。
* **[修復]：** 車輛救援系統改為分批處理滯留車輛，載入舊存檔時不再於單一幀內處理上千輛。
* **[修復]：** 同一次遊戲中載入第二個存檔時，車輛救援系統可能對無關車輛執行操作。
