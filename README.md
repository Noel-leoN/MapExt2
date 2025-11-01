
# Cities Skylines 2 Map Extended Mod (28/57/114/vanilla) All-in-one Edition (Beta)

## Caution!!! Make sure to check out the full description below before using this mod!
* It is very laborious to build a big city. This mod makes extensive modifications to the vanilla game's code, it is not entirely clear what potential issues there might be, and it may conflict with some mods.Please use at your own risk.
* BACKUP YOUR GAME SAVES before use this mod! Whether you're using this mod or not, it's highly recommended to get into the habit of saving as a new file every time you play the game, then delete old saves once you're sure don't need them.
* If the mod is uninstalled or does not load successfully, please DO NOT LOAD savegame made by MapExt and then OVERWRITE it. As mentioned above, "Save As" every time.
* In the Option UI, Set the correct mapsize mode(28km/57km/114km/vanilla 14km, side length of map square)  and MATCH the Maps and Saves. You must use a map or save of the specific size made by youself or someone else. It's a good idea for anyone sharing maps or saved games with special sizes to tag the actual mapsize and scale.
* The loadgame verification function has been added to avoid loading the gamesave with wrong mapsize. However, it CANNOT validate maps loaded in the map Editor.
* Recommended to use graphics card with more than 10G of video memory. This mod will take an extra 1-2GB of VRAM. If you experience crashes while loading maps, it's likely due to insufficient VRAM from loading too many assets. It's better to create a new Playset the first time you use this mod and to use as few asset mods as possible. If everything works fine, you can gradually add more asset mods.

## Introduction
* MapSize mode list:
* 57km(default): 4x4 the size of the vanilla map,DEM-14m.
* 28km: 2x2 DEM-7m
* 114km: 8x8 DEM-28m (not recommand due to low terrain resolution, as well as tearing at the edges of the map, and calculation issues of simulation systems)
* 14km: vanilla 1x1 DEM-3.5m
* As the map size is enlarged, the terrain will be less detailed, and waterfront edges and mountains may look relatively rough. Due to some pretty tricky technical reasons, and also considering the performance impact, this mod won't be able to improve the terrain resolution for now. If needed, you can try some methods to avoid it, like planting trees or using other objects for cover. If you really need a higher terrain resolution, you can wait for LargerMap mod by algernon.
* The maptiles now stay at 529.
* Integrated some performance tool mods i've made before, just like NoDogs/CS2LiteBoost, which can enable ingame with OptionUI, and then unsubscribe them.
## Usage
### Make 1:1 Map:
In the Editor, Import the correct size heightmap/worldmap terrain image：
* 28km playable: height 28672m / world 114688m
* 57km playable: height 57344m / world 229376m
* 114km playable: height 114688m / world 458752m
* 
* Supported heightmap/worldmap terrain image format: 4096x4096 16bit grayscale terrain image (PNG or TIFF) 
* (It's possible to import terrain heightmaps with a resolution <b>lower than 14336x14336</b>, which will be automatically upsampled or downsampled to 4096x4096.)
* If the import size is not the same as above, the map will work but stretch.
* Maps over 57km are recommended not to import the "fake" Worldmap to save performance. (The only use of the worldmap is for visuals; it really can't serve any other purpose.)
### Some useful folders(paste to the Explorer address bar):
- heightmap/worldmap : "%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Heightmaps"
- overlays map : "%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Overlays"
- log (for reporting issues) : "%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs"
- local mod (only for manual installation, don't touch this if you subscribed this mod）"%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods"
### Some must-have mods to use together(Thanks to the authors!): 
- Skyve: manager Playset, backup savegame, report issue
- Image Overlay: overlay an image on the game map, a must-have for building a real city
- Free Range Camera：a must-have too
- Water Feature: better control of water sources, important for bigger maps
- 529 Tiles： essential for managing maptiles
- Anarchy: a must-have too.
- Demand Master Control: Vanilla games might have economic or population simulation issues or sudden performance drops when the population goes over 200k-500k. Tweaking some options with this mod could help.

## Issues
* It’s been noticed that in vanilla game, when dealing with populations over 200k–500k (depending on city layout), pathfinding queue jams can cause sudden CPU slowdowns and weird economic simulation issues—and these problems become even more obvious when using this mod. I'm trying to fix it. Some possible ways to mitigate it include: avoid building huge areas all at once, don’t boost citizen happiness too quickly, don’t rapidly expand service coverage, and don’t build too many parks (actually, it’s best to skip parks entirely). Just like in real life, steady city growth is healthy, while going all-out too fast just leads to a crash.
* Due to the floating-point precision in the vanilla game simulation system, there might be some weird height display glitches at the edges of the map. Try to set up the residential/commercial/industrial areas close to the center of the map.
* May not be compatible with some special mods.
* If you are using a previous version that is NOT released on PDX mod platform and is a non-57km map size (e.g. 28km or 114km), you need to manually check the Debug devalidation option in OptionUI, and then be sure to select the correct mapsize mode. Once you've successfully loaded and checked that the save file is all good, you can save it as a new file and then just Turn Off the option in Debug.
* If you found issues please report in Discord or Github, thank you.

## Tips
* For those who don’t want to wait too long for water sources to generate, it is recommended to use Water Feature Mod from yenyang and sliding the flowness feature to the max (it’s experimental, but it works great in practice). Or set sealevel in DevUI.
* More tips can be found in the Discord channel listed below.

## Credits
- [Discord](https://discord.gg/s6BcrFKepF): Cities: Skylines Modding (mainly discussion location)
- [rcav8tr](https://github.com/rcav8tr/CS2-Modding-Instructions):Cities Skylines 2 Modding Instructions
- [Captain-Of-Coit](https://github.com/Captain-Of-Coit/cities-skylines-2-mod-template): A Cities: Skylines 2 BepInEx mod template.
- [BepInEx](https://github.com/BepInEx/BepInEx): Unity / XNA game patcher and plugin framework.
- [Harmony](https://github.com/pardeike/Harmony): A library for patching, replacing and decorating .NET and Mono methods during runtime.
- [Discord](https://discord.gg/ABrJqdZJNE): Cities 2 Modding
- Thanks Rebeccat, HideoKuze2501, Nulos, Jack the Stripper,Bbublegum/Blax (in no particular order) and other good people who are not mentioned above for the test!

## 注意！！！在使用此模组之前，请务必查看下面的完整说明！
* 建造大城市非常费力。此模组对原版游戏代码进行了广泛修改，潜在问题尚不完全清楚，可能会与某些模组冲突。请自行承担风险。
* 在使用此模组之前，请备份您的游戏存档！无论是否使用此模组，都强烈建议养成每次玩游戏时另存为新文件的习惯，然后在确定不再需要旧存档后再删除。
* 如果模组被卸载或未能成功加载，请不要加载由 MapExt 制作的存档并覆盖它。如上所述，请每次使用“另存为”。
* 在选项界面中，设置正确的地图尺寸模式（28公里/57公里/114公里/原版14公里，地图方格边长），并匹配地图和存档。您必须使用您自己或其他人制作的特定尺寸的地图或存档。建议任何共享特殊尺寸地图或存档的人标注实际地图尺寸和比例。
* 已添加存档加载验证功能，以避免加载错误地图尺寸的游戏存档。不过，它无法验证地图编辑器中加载的地图。
* 推荐使用显存超过10G的显卡。此模组将占用额外1-2GB显存。如果在加载地图时出现崩溃，很可能是由于加载过多资源导致显存不足。第一次使用此模组时最好创建新的游戏设置，并尽量少使用资源模组。如果一切正常，您可以逐步添加更多资源模组。

## 介绍
* 地图大小模式列表：
* 57公里（默认）：4x4 原版地图大小，DEM-14米。
* 28公里：2x2 DEM-7米
* 114公里：8x8 DEM-28米（不推荐使用，因为地形分辨率低，地图边缘可能出现撕裂，且模拟系统计算会有问题）
* 14公里：原版 1x1 DEM-3.5米
* 随着地图尺寸的增大，地形细节将会减少，水岸边缘和山脉可能显得相对粗糙。由于一些相当复杂的技术原因，同时也考虑到性能影响，这个模组暂时无法提升地形分辨率。如有需要，你可以尝试通过种植树木或使用其他物体作为遮挡来规避。如果你真的需要更高的地形分辨率，可以等待 algernon 的 LargerMap 模组。
* 地图瓦片数量现在保持在529。

## 用法
### 制作 1：1 地图：
* 在 Editor 中，导入正确大小的地形高度贴图heightmap/世界贴图worldmap。
* 28 公里可玩区域：高位图 28672 米 / 世界 114688 米
* 57 公里可玩区域：高位图 57344 米 / 世界 229376 米
* 114公里可玩区域：高位图 114688m / 世界 458752m
* 
* 支持的可玩区域高度图/世界地图的地形贴图格式：4096x4096 16 位灰度贴图（PNG 或 TIFF）
* (支持直接导入不高于14336x14336分辨率的地形高位图，mod会自动缩放至4096x4096)
* 如果导入大小与上述大小不同，则地图将拉伸。
* 建议超过 57 公里的地图不要导入“假”世界地图以节省性能。（世界地图的唯一用途是视觉效果;它真的不能用于任何其他目的。）

### 可能需要的一些文件夹：
- heightmap/worldmap : "%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Heightmaps"
- 覆盖地图(叠加一个现实地图层，需要Overlay模组) : "%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Overlays"
- 日志 (for reporting issues) : "%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs"
- 本地模组模式 (仅用于经验丰富用户，如果您是订阅模组请不要触碰这个) : "%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods"

### 已知问题：
* 由于原版游戏模拟系统中的浮点精度问题（可能是出于性能考虑），地图边缘可能会出现一些奇怪的高度显示故障。
* 可能与某些特殊模组不兼容。
* 在原版游戏中，当城市人口超过20万至50万（取决于城市布局）时，寻路队列堵塞可能会导致性能迅速下降以及经济模拟出现异常问题。使用此模组时，这些问题会更加明显，正在尝试修复。一些可能的缓解方法包括：避免一次性建造大面积区域，不要过快提升市民幸福度，不要迅速扩展服务覆盖范围，不要建造过多公园（实际上，最好完全不建公园）。就像现实生活一样，稳定的城市增长才能健康，过快改善将导致各种异常。

### 技巧： 
* 可以使用水源工具Mod的选项-实验功能-流动性开到最大，蒸发量最小，生成水源会相当快。或者在游戏中的开发者UI设置sealevel秒填(但仍需设置水源，否则会逐渐干涸)
* 附加设置整合了一些作者之前发布的性能mod小工具，比如不溜狗、去除过境交通等。更多功能持续增加中。
* 游戏在发生崩溃后很可能造成mod缓存未正确清理故障(不仅是本mod，所有mod都可能发生)，此时加载存档可能会坏档。如果进游戏出现错误提示，请尝试skyve清除缓存。

欢迎留言反馈问题

