## v1.0.6 - Playback Order and Playlists

* **[Playback]:** Fixed shuffle repeating songs before a round ended: switching back to a station or the start of a new in-game day no longer reshuffles the songs that have not played yet.
* **[Playback]:** Added a Playback Order option: Shuffle (default) or Sequential, which plays songs in file name order and loops.
* **[Playlists]:** In Sequential mode, an .m3u or .m3u8 playlist in a station folder sets the play order; files it does not list follow by file name.
* **[Playback]:** Switching to another station and back now plays the song that was queued instead of skipping it.
* **[Playback]:** A new shuffle round no longer opens with the songs that just played.

---

### 主要变动

* **[播放]：** 修复随机播放一轮还没播完就重复的问题：切回电台或游戏内新的一天开始时，不再把还没播的歌重新洗牌。
* **[播放]：** 新增“播放顺序”选项：随机（默认）或顺序，顺序模式按文件名播放并循环。
* **[播放列表]：** 顺序模式下，可在电台文件夹放 .m3u 或 .m3u8 播放列表指定播放顺序，列表没列到的文件按文件名排在后面。
* **[播放]：** 切到别的电台再切回时，接着播原本排在队列里的那首，不再跳过。
* **[播放]：** 随机播放的新一轮开头，不再是上一轮刚播过的几首。
