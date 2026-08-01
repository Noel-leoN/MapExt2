## v4.7.0 - Water, Snow and Terrain Performance Options

* **[Performance]:** Added an option to freeze the water simulation while the game is paused, enabled by default.
* **[Performance]:** Added a snow simulation freeze option with automatic and always-on modes, disabled by default.
* **[Performance]:** Added an option to skip the per-frame backdrop terrain downsample when the terrain has not changed, disabled by default and only relevant to maps that have a background world map.
* **[Fix]:** Water flow blur stayed disabled after switching the water simulation quality back to Vanilla.
* **[Fix]:** The water surface could resume simulating before it finished adapting to terrain edits, which could leave ripples or incorrect water levels after using the terrain brush.

---

### 主要變動

* **[效能]：** 新增「暫停時凍結水模擬」選項，預設開啟。
* **[效能]：** 新增「凍結雪模擬」選項，提供自動與始終凍結兩種模式，預設關閉。
* **[效能]：** 新增「背景地形降採樣事件化」選項，地形未變更時跳過每幀降採樣，預設關閉，僅對有背景世界地圖的存檔有效。
* **[修復]：** 水模擬品質切回 Vanilla 後，水流模糊未還原而持續處於關閉狀態。
* **[修復]：** 水面可能在尚未完成適應地形變更前就恢復模擬，導致使用地形筆刷後出現波紋或水位異常。
