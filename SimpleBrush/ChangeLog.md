## v1.0.1 - Infinite Resource Reliability and Settings Fixes

* **[Fix]:** The infinite resource options refilled resources far less often than the game consumes them, so extraction buildings could still drop to zero efficiency and stop producing between refills.
* **[Fix]:** The resource restore buttons no longer report success when pressed from the main menu, where no city is loaded.
* **[Fix]:** Mod options could fail to be written to disk when another installed mod used the same internal settings name.
* **[Change]:** Hardened the resource brush unlock so a failure part-way through cannot leave only some brushes unlocked, and so it keeps retrying while the game is still loading its data.

---

### 主要變動

* **[修復]：** 無限資源選項的補充頻率遠低於遊戲消耗資源的頻率，導致抽取類建築在兩次補充之間仍可能效率歸零並停止生產。
* **[修復]：** 在主菜單按下資源恢復按鈕時不再回報成功——該處並未載入任何城市。
* **[修復]：** 當其他已安裝的 Mod 使用相同的內部設定名稱時，本 Mod 的選項可能無法寫入磁碟。
* **[調整]：** 強化資源筆刷解鎖流程，中途失敗不會只留下部分筆刷被解鎖，且在遊戲資料尚未載入完成時會持續重試。
