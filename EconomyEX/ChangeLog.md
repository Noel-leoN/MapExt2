## v4.8.0 - Vehicle Rescue Reliability, Large Map Warning and Second Load Fixes

* **[Map Size]:** Loading a large map save without MapExt installed now shows a warning dialog instead of only writing a line to the log.
* **[Fix]:** Reworked how the vehicle rescue system cleans up stranded vehicles, addressing an autosave failure reported on a large save with the system enabled.
* **[Fix]:** The vehicle rescue system now handles stranded vehicles in batches instead of processing over a thousand of them in a single frame when loading an older save.
* **[Fix]:** Loading a second save within the same session could make the vehicle rescue system act on unrelated vehicles.
* **[Fix]:** After loading a second save in the same session, the vanilla economy systems ran alongside the replaced ones, and the pet spawning and through-traffic settings reverted to vanilla behaviour.
* **[Fix]:** The settings page stayed on "IDLE: Waiting for map load..." when the first map loaded in a session was a large map.
* **[Performance]:** The vehicle rescue system now scans off the main thread, so looking for stranded vehicles no longer causes a brief stall.

---

### 主要變動

* **[地圖尺寸]：** 在未安裝 MapExt 的情況下載入大地圖存檔時會顯示警告對話框，不再只在日誌寫入一行。
* **[修復]：** 重寫車輛救援系統清理滯留車輛的方式，處理某大型存檔在啟用該系統時自動存檔失敗的問題。
* **[修復]：** 車輛救援系統改為分批處理滯留車輛，載入舊存檔時不再於單一幀內處理上千輛。
* **[修復]：** 同一次遊戲中載入第二個存檔時，車輛救援系統可能對無關車輛執行操作。
* **[修復]：** 同一次遊戲中載入第二個存檔後，原版經濟系統會與替換版同時運行，且寵物生成與過境交通兩項設定回退成原版行為。
* **[修復]：** 同一次遊戲中首個載入的地圖為大地圖時，設定頁狀態永遠停在「IDLE: Waiting for map load...」。
* **[效能]：** 車輛救援系統的掃描改於主執行緒外進行，尋找滯留車輛時不再造成短暫停頓。
