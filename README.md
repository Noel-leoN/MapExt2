# Cities Skylines 2 Map Extended Mod Beta(57km version)

		## Introduction

		- 57km^2 MapSize (4x4 the size of the vanilla map)

		## Usage
		For 57km^2 version(more stable):
		- create map in game editor manually to import 57.344km heightmap (229.376km worldmap is optional) . (it's 1:1 scale, or any size you want but it scales)

		For 229km^2 version(under test):
		- create map in game editor manually to import 229.376km heightmap (optional 917.504km worldmap, but not recommand because of performance drop). (or any size you want but it scales)

		Supported terrain image format: 4096x4096 16bit grayscale terrain image (PNG or TIFF) .

		## Caution
		- Bugs with all vanilla maps. You HAVE TO USE a custom map.
		Due to the change in terrain height ratio, DO NOT use vanilla game saves to play, otherwise existing buildings will have visual errors.
		- If you have any BepInEx version of MapExt installed, BE SURE to delete all directories and files, including BepInEx/patcher/MapExt and local PDX mods/MapExt.PDX
		- There's no doubt it will affect game saves (generally, different mapsize/resolution savegame might not be compatible, but the same mapsize/resolution savegame may work).

		## Issues
		- As the map size is enlarged, the terrain will be less detailed, and waterfront edges and mountains may look relatively rough.
		- Due to the floating-point precision issues in the vanilla game simulation system (probably because of performance considerations), there might be some weird height display glitches at the edges of the map.The 229km version will show this more noticeably, while the 57km version is pretty much usable.
		- May not be compatible with some special mods.
		- Repeatedly replicate the overlayinfomation of the playable area to the scope of the world map, its a vanilla bug, hasn't been fixed yet, so please ignore it for now, or don't use too much zoom out.
		- a few simulation systems may not be working properly,such as water pumping/tempwater powerstation.
		- Water Feature Mod needs to override the "mapextend" constant specified inside it in order to work properly.(now The latest beta version is working fine. )
		- If you found issues please report in github, thank you.

		## Disclaimer
		- SAVE YOUR GAME before use this mod. Please use at your own risk.

		## Notice
		- This is a "pure" PDX mod solution.Most patches use Harmony Transpiler to get performance similar to the vanilla.
		- For experienced users, it's recommended to use BepInEx/PDX mixed version (released on GitHub) for much better stability,mod compatibility and performance.However, the installation is somewhat complicated.

		## Credits
		- [Captain-Of-Coit](https://github.com/Captain-Of-Coit/cities-skylines-2-mod-template): A Cities: Skylines 2 BepInEx mod template.
		- [BepInEx](https://github.com/BepInEx/BepInEx): Unity / XNA game patcher and plugin framework.
		- [Harmony](https://github.com/pardeike/Harmony): A library for patching, replacing and decorating .NET and Mono methods during runtime.
		- [CSLBBS](https://www.cslbbs.net): A chinese Cities: Skylines 2 community.
		- [Discord](https://discord.gg/ABrJqdZJNE): Cities 2 Modding (in testing channel https://discord.com/channels/1169011184557637825/1252265608607961120)
		- Thanks  Rebeccat, HideoKuze2501, Nulos, Jack the Stripper,Bbublegum/Blax (in no particular order) and other good people who are not mentioned above for the test!

		大地图mod
		介绍：自定义地图可达57kmx57km，原版4x4倍大小
		
		使用：
		1.请在地图编辑器中自制地图或使用别人做好的地图。需导入57.344km大小的地形高位图(1:1真实世界比例)，可选导入229.376km的世界外围地图(不可建造部分)。建议可只导入前者以提高性能。其他大小也可以导入，但会自动缩放。
		2.高位图仅支持4096x4096分辨率/16位灰度/PNG或TIFF格式。具体请自行查询相关教程。
		3.自制地图时尽量不要改动高度缩放(默认为4096)以免出现未知问题。
		
		注意：
		1.安装过BepInEx版本的务必彻底删除几个dll(目录可保留)。
		2.不要用原版地图及存档，虽然可能打开，但所有高度不正常。
		3.由于本mod策略为地图放大而不改变分辨率，因此地形精度会下降，水岸和山体可能相对粗糙，介意的请不要使用。
		4.信息视图比如污染、资源等会出现可建造范围外重复显示，这是原版游戏bug，暂未修复。
		5.个别模拟系统可能数值计算相对不正常，比如温差发电站、水泵之类。
		6.Water Feature Mod水源工具需要使用最新测试版。老版不支持生成水源。
		
		技巧：
		可以使用水源工具选项-实验功能-流动性开到最大，蒸发量减少，生成水源会相当快。
		
		提示：
		有一定经验用户建议使用BepInEx版本(在Github/CSLBBS发布)，以获得更好的稳定性、mod兼容性和性能。
		欢迎反馈测试
