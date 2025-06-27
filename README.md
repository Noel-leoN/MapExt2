
# Cities Skylines 2 Map Extended Mod (28/57/114/229km) All-in-one Edition

## Caution!!! Make sure to check out the full description below before using this mod!
* There's no doubt it will affect game saves. BACKUP YOUR VANILLA GAMESAVE or SAVES from earlier versions of MapExt before use this mod! Whether you're using this mod or not, it's highly recommended to get into the habit of saving as a new file every time you play the game. If you're worried about disk space, you can delete old saves once you're sure you don't need them.
* If the mod is uninstalled or does not load successfully, please DO NOT LOAD a save of >=28km mapsize and then OVERWRITE it, which can corrupt the gamesave. As mentioned above, "Save As" every time.  If you really don't want to use this mod anymore(even though setting it to vanilla won't change anything), make sure to delete it in the Playset instead of just disabling it!
* Please set the correct mapsize in the Option UI and MATCH the Maps you want to make, or the Saves you want to play, or use a map or save of the specific size made by someone else. It's a good idea for anyone sharing maps or saved games with special sizes to tag the actual mapsize and scale.
* From v2.0 All-in-one version, now you can switch to the vanilla mapsize(14km)/28km/57km/114km/229km in the MainMenu's Option UI without having to uninstall the mod. Also, the loadgame verification function has been added to avoid loading the gamesave with wrong mapsize, which can prevent to corrupt gamesave.
* However, it cannot to validate maps loaded in the map editor, you have to choose the right settings in Option UI. Setting the wrong mapsize won't cause the map to break, but it will stretch if you want to make a 1:1 map with a size of >=28km.
* 
* It is very laborious to build a big city. Due to the large number of patched objects involved in the map size factor in the game, it is not entirely clear that there are hidden issues, Please use at your own risk.
* Recommended to use a graphics card with more than 10G of video memory. This mod will take an extra 1-2GB of VRAM. If you experience crashes while loading maps, it's likely due to insufficient VRAM from loading too many assets. It's recommended to create a new Playset the first time you use this mod and to use as few asset mods as possible. If everything works fine, you can gradually add more asset mods.
* If you are using a previous version that is NOT released on PDX mod platform and is a non-57km map size (e.g. 28km or 229km), you need to manually check the Debug devalidation option in OptionUI, and then be sure to select the correct mapsize mode. Once you've successfully loaded and checked that the save file is all good, you can save it as a new file and then just Turn Off the option in Debug.
## Introduction
* MapSize list:
* 57km(default): 4x4 the size of the vanilla map,DEM-14m.
* 28km: 2x2 DEM-7m
* 114km: 8x8 DEM-28m (not recommand due to low terrain resolution, as well as tearing at the edges of the map, and calculation issues of simulation systems)
* 229km: 16x16 DEM-56m (not recommand, same reason but even worse)
* 14km: vanilla 1x1 DEM-3.5m
* As the map size is enlarged, the terrain will be less detailed, and waterfront edges and mountains may look relatively rough. Due to some pretty tricky technical reasons, and also considering the performance impact (higher resolutions will affect performance noticeably), this mod won't be able to improve the terrain resolution for now. If needed, you can try some methods to avoid it, like planting trees or using other objects for cover. If you really need a higher terrain resolution, you can wait for LargerMap mod by algernon.
* The maptiles now stay at 529.
## Usage
### Make 1:1 Map:
* In the Editor, Import the correct size heightmap/worldmap terrain image.
* 28km playable: height 28672m / world 114688m
* 57km playable: height 57344m / world 229376m
* 114km playable: height 114688m / world 458752m
* 229km playable: height 229376m / world 917504m
* Supported heightmap/worldmap terrain image format: 4096x4096 16bit grayscale terrain image (PNG or TIFF) 
* If the import size is not the same as above, the map will work but stretch.
* Maps over 57km are recommended not to import the "fake" Worldmap to save performance. (The only use of the worldmap is for visuals; it really can't serve any other purpose.)
### Here's some folders you may need :
- heightmap/worldmap : "%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Heightmaps"
- overlays map : "%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Overlays"
- log (for reporting issues) : "%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs"
- local mod (only for manual installation, don't touch this if you subscribed this mod）"%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods"
### Some must-have mods to use together(Thanks to the authors of these mods!): 
- Skyve: to manager Playset, backup savegame, report issue
- Image Overlay: overlay an image on the game map, a must-have for building a real city
- Free Range Camera：a must-have too
- Water Feature: better control of water sources, important for bigger maps
- 529 Tiles： essential for managing maptiles

## Issues
* Due to the floating-point precision issues in the vanilla game simulation system (probably because of performance considerations), there might be some weird height display glitches at the edges of the map. Try to set up the residential/commercial/industrial areas close to the center of the map.
* May not be compatible with some special mods.
* Repeatedly replicate the overlay infomation of the playable area to the scope of the world map, its a vanilla bug, hasn't been fixed yet, so please ignore it for now, or don't use too much zoom out.
* If you found issues please report in Discord or Github, thank you.

## Tips
* For those who don’t want to wait too long for water sources to generate, it is recommended to use Water Feature Mod from yenyang and sliding the flowness feature to the max (it’s experimental, but it works great in practice).
* More tips can be found in the Discord channel listed below.

## Credits
- [Discord](https://discord.gg/s6BcrFKepF): Cities: Skylines Modding (mainly discussion location)
- [rcav8tr](https://github.com/rcav8tr/CS2-Modding-Instructions):Cities Skylines 2 Modding Instructions
- [Captain-Of-Coit](https://github.com/Captain-Of-Coit/cities-skylines-2-mod-template): A Cities: Skylines 2 BepInEx mod template.
- [BepInEx](https://github.com/BepInEx/BepInEx): Unity / XNA game patcher and plugin framework.
- [Harmony](https://github.com/pardeike/Harmony): A library for patching, replacing and decorating .NET and Mono methods during runtime.
- [CSLBBS](https://www.cslbbs.net): A chinese Cities: Skylines 2 community.
- [Discord](https://discord.gg/ABrJqdZJNE): Cities 2 Modding
- Thanks Rebeccat, HideoKuze2501, Nulos, Jack the Stripper,Bbublegum/Blax (in no particular order) and other good people who are not mentioned above for the test!

## 大地图mod全功能整合版(14km/28km/57km/114km/229km切换)

## 警告！！！在使用这个模组之前，请务必看完下面的完整描述！
* 毫无疑问，它会影响游戏存档。务必请备份你的原版游戏存档或早期版本的MapExt存档后，再使用这个模组！
* 现在Mod支持随时从主菜单切换回原版地图大小，而无需卸载模组。如果你不想再使用这个模组，请确保在Playset中删除它，而不是仅仅禁用！(其他的Mod最好也这样做，因为游戏的Mod管理器存在一些问题，某些情况下点击禁用了仍会部分加载，必须使用删除按钮，这也是许多不明崩溃的原因之一)
* 如果模组未成功卸载或加载，请不要读取超过28公里地图大小的存档,并且千万不要保存后退出。
* 请在选项界面中设置正确的地图大小，并匹配你想制作的地图或想玩的存档。
* 如果你想要一个28公里及以上大小的 1:1 地图，你需要在选项界面中选择正确的地图大小，然后在游戏编辑器中制作自己的地图，或者使用别人制作的特定大小的地图。否则，地图会拉伸或破损。
* 
* 该模组具有简单的 loadgame 读取检查功能，以防止以错误的设置加载不同地图大小的游戏存档。但是，无法验证识别地图编辑器中加载的地图，必须在 Option UI 中选择正确的设置。
* 建设一个大城市非常费力。由于游戏中涉及的地图大小的代码涉及大量修补对象，目前尚不完全清楚是否存在隐藏问题，如使用请风险自担。
* 建议使用显存超过 10G 的显卡。该mod可能需要额外的 1-2GB VRAM。如果您在加载地图时遇到崩溃，很可能是由于加载过多资产模组导致 VRAM 不足。建议在第一次使用此Mod时创建一个新的 Playset，并尽可能少地使用资产模组。如果一切正常，再逐渐添加更多的资产模组。

## 介绍
* MapSize 地图尺寸列表：
* 57 公里（默认）：原版地图大小的 4x4，DEM-14m。
* 28 公里：2x2 DEM-7m
* 114 公里：8x8 DEM-28m（由于地形分辨率低，以及地图边缘撕裂以及模拟系统的计算问题，因此不建议使用）
* 229 公里：16x16 DEM-56m（不推荐，同样的原因且更糟）
* 14 公里：原版 1x1 DEM-3.5m
* 随着地图大小的放大，地形的细节会变少，滨水区边缘和山脉可能看起来相对粗糙。由于一些非常棘手的技术原因，这个mod暂时无法提高地形分辨率。如果您需要更高的地形分辨率，您可以等待 algernon 的 LargerMap。
* 地图图块现在保持在 529 个。

## 用法
### 制作 1：1 地图：
* 在 Editor 中，导入正确大小的地形高度贴图heightmap/世界贴图worldmap。
* 28 公里可玩区域：高位图 28672 米 / 世界 114688 米
* 57 公里可玩区域：高位图 57344 米 / 世界 229376 米
* 114公里可玩区域：高位图 114688m / 世界 458752m
* 229公里可玩区域：高位图 229376米 / 世界917504米
* 支持的可玩区域高度图/世界地图的地形贴图格式：4096x4096 16 位灰度贴图（PNG 或 TIFF）
* 如果导入大小与上述大小不同，则地图将拉伸。
* 建议超过 57 公里的地图不要导入“假”世界地图以节省性能。（世界地图的唯一用途是视觉效果;它真的不能用于任何其他目的。）

### 可能需要的一些文件夹：
- heightmap/worldmap : "%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Heightmaps"
- 覆盖地图(叠加一个现实地图层，需要Overlay模组) : "%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Overlays"
- 日志 (for reporting issues) : "%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs"
- 本地模组 (仅用于经验丰富用户，如果您是订阅的模组请不要触碰这个) : "%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods"

### 已知问题：
* 由于原版游戏模拟系统中的浮点精度问题（可能是出于性能考虑），地图边缘可能会出现一些奇怪的高度显示故障。
* 可能与某些特殊模组不兼容。
* 将可玩区域的叠加信息反复复制到世界地图范围内，这是一个原版bug，目前没有修复，请暂时忽略，或者不要使用太大的缩放。

### 技巧： 
* 可以使用水源工具Mod的选项-实验功能-流动性开到最大，蒸发量最小，生成水源会相当快。
* 附加设置整合了一些作者之前发布的性能mod小工具，比如不溜狗、去除过境交通等。更多功能持续增加中。

欢迎留言反馈问题
