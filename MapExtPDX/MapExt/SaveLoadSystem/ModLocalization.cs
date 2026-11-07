// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.


using Colossal;
using Colossal.Localization;
using System.Collections.Generic;

namespace MapExtPDX.SaveLoadSystem
{
    public static class ModLocalization
    {
        private const string Tag = nameof(ModLocalization);

        /// <summary>
        /// 產出三語的對話框文本來源，<b>不自行註冊</b>。
        ///
        /// <para>註冊統一由 <c>Mod.RegisterLocalization</c> 執行：它把這裡的對話框文本與
        /// 設定 UI 文本（<c>LocaleEN</c> 等）併成一個 <c>CompositeLocaleSource</c>，
        /// 讓每個語言只呼叫一次 <c>AddSource</c>。原本這裡各自呼叫一次、
        /// 設定那邊再各自呼叫一次，共 6 次，其中 2 次落在 en-US——
        /// 而 en-US 每次註冊都會觸發一趟無鎖的全字典 <c>MergeFrom</c>，
        /// 那是 2026-09-05 那次 <c>Mod.OnLoad</c> 中斷的競爭窗口。
        /// 完整論證見 <see cref="MapExtPDX.MapExt.Core.CompositeLocaleSource"/>。</para>
        /// </summary>
        public static void GetSources(
            out IDictionarySource en, out IDictionarySource hans, out IDictionarySource hant)
        {
            var fallback = new Dictionary<string, string>
            {
                // Dialog Titles
                { "LOAD_VALIDATION.TitleError", "Load Error" },
                { "LOAD_VALIDATION.TitleWarning", "Warning" },
                { "LOAD_VALIDATION.TitleLegacy", "Legacy Save Detected" },

                // Dialog Messages
                { "LOAD_VALIDATION.ModNotUsed", "This save file was not created with MapExt mod. Loading is not permitted. Please set the '{NONE_MODE}' Mode in the MapExt Option of the MainMenu." },
                { "LOAD_VALIDATION.Mismatch", "Mode Mismatch! This save requires '{SAVED_MODE}', but your mod is set to '{CURRENT_MODE}'. Please change MapExt's MapSize Mode in the options menu to match the save file." },
                // { "LOAD_VALIDATION.LegacySave", "This is a legacy save file without mode data. It will be loaded using the '{LEGACY_MODE}' mode settings. Do you want to continue?" },

                 { "LOAD_VALIDATION.LegacyMismatch", "This is a legacy save file. If it's 57km map, please switch the mod to '{LEGACY_MODE}' in the options menu. If it's other mapsize, Please Read MapExt's new Description!" },
                
                // Button Texts
                { "LOAD_VALIDATION.ConfirmOK", "OK" },
                { "LOAD_VALIDATION.ConfirmContinue", "Continue" },

                // === Map Size Mismatch Warning ===
                { "MAPEXT_MAPSIZE.MismatchTitle", "⚠️ Map Size Mode Mismatch" },
                { "MAPEXT_MAPSIZE.MismatchMessage", "This map was created in '{AUTHORED_MODE}', but MapExt is currently set to '{CURRENT_MODE}'.\n\nTerrain heights are being sampled at the wrong scale, so elevation, water and buildable areas will all be incorrect.\n\nTo fix this, quit to the main menu, set MapExt's MapSize Mode to '{AUTHORED_MODE}', then load the map again.\n\nNote: maps created in the editor carry no map-size metadata, so this can only be detected after loading has begun." },
                // 通知條版本（短文案）：對話框可能被其它 Mod 的 UI 重建沖掉，這條留在通知區
                { "MAPEXT_MAPSIZE.MismatchNotify", "This map was made for '{AUTHORED_MODE}' but MapExt is set to '{CURRENT_MODE}'. Terrain height is sampled at the wrong scale. Quit to the main menu, switch MapExt's MapSize Mode to '{AUTHORED_MODE}', then reload." },

                // === 原版地形被拉伸：载入后提示（v4.12.0 整合计划 2.2），游戏开新城与编辑器读图各一套 ===
                { "MAPEXT_MAPSIZE.StretchTitle", "⚠️ Vanilla Map Stretched" },
                { "MAPEXT_MAPSIZE.StretchMessage", "This new city was started on a map with vanilla 14 km terrain, but MapExt is set to '{CURRENT_MODE}'. The terrain has been stretched to the current map size: slopes are flattened, while water sources, outside connections and map tiles stayed where they were, so only the central 14 km can ever be bought. Natural resources were also generated randomly instead of using the ones painted on the map.\n\nIt is best not to keep building this city. Return to the main menu and start a new city on a map made for '{CURRENT_MODE}' instead." },
                { "MAPEXT_MAPSIZE.StretchNotify", "This city's vanilla 14 km terrain is stretched to '{CURRENT_MODE}': slopes are flattened and only the central 14 km can be bought." },
                { "MAPEXT_MAPSIZE.StretchEditorMessage", "This map has vanilla 14 km terrain, but MapExt is set to '{CURRENT_MODE}', so the editor shows it stretched to the current map size. Its map tiles were not rebuilt and still cover only the central 14 km.\n\nSaved like this, it becomes a map with '{CURRENT_MODE}' terrain whose buyable area is only the central 14 km. To edit the map at its original size, switch MapExt's MapSize Mode to '{NONE_MODE}' and restart the game." },
                { "MAPEXT_MAPSIZE.StretchEditorNotify", "This vanilla 14 km map is shown stretched to '{CURRENT_MODE}', and its map tiles still cover only the central 14 km. To edit it at its original size, switch MapExt to '{NONE_MODE}'." },

                // === WorldMap Import Warning ===
                { "MAPEXT_WORLDMAP.WarningTitle", "⚠️ Performance Warning" },
                { "MAPEXT_WORLDMAP.WarningMessage", "Importing a World Map introduces additional rendering overhead:\n\n• Extra cascade rendering layer (baseLod increased)\n• Per-frame DownSampleHeightMap for backdrop\n• Larger MinMaxMap (1024 vs 512)\n• Increased GPU memory usage\n\nThis may reduce FPS on large maps. Continue?" },
                { "MAPEXT_WORLDMAP.ConfirmImport", "Import Anyway" },
                { "MAPEXT_WORLDMAP.CancelImport", "Cancel" },

                // === Vanilla Map Extension Dialog ===
                { "VANILLA_CONVERT.Title", "Extend Vanilla Map" },
                { "VANILLA_CONVERT.Message", "This save was created without MapExt (14km vanilla).\n\nExtend to current mode ({TARGET_MODE})?\n\n⚠ HIGHLY EXPERIMENTAL - the following will happen:\n• Playable area will be expanded to new limits\n• All 529 map tiles will be unlocked\n• Terrain heightmap will be synthesized and expanded\n• Original natural resources and ground water will be preserved\n• All vehicle and resident entities will be cleared\n• Outside connections stay at the old border, so trade and immigration continue; to have traffic enter from the new map edges, add new ones there with developer mode or a mod\n• Water sources will be upgraded and sea level reset\n\nThe original save file will NOT be modified.\nA new save will be created (Format: SaveName_MapExtMode).\nYou MUST restart the game after conversion to prevent water glitches or crashes." },
                { "VANILLA_CONVERT.Confirm", "Extend and Load" },
                { "VANILLA_CONVERT.Cancel", "Cancel" },
                { "VANILLA_CONVERT.Complete", "Extension Complete" },
                { "VANILLA_CONVERT.CompleteMessage", "Vanilla map has been extended to {TARGET_MODE}.\nNew save: {SAVE_NAME}\n\n• Terrain heightmap synthesized\n• Original natural resources and ground water preserved\n• All vehicle and resident entities cleared\n• Outside connections kept at the old border\n• Water reset to the sea level\n⚠ All 529 map tiles unlocked\n\n⚠ RESTART REQUIRED\nYou MUST quit to desktop and reload the new save.\nFailure to restart will cause water simulation glitches or crashes!\n\n★ AFTER RESTART - TODO LIST:\n\n1. (Optional) Add outside connections at the new map edges:\n   The existing ones stay at the old border, so trade and immigration continue. To have roads, railways, shipping lanes and airline routes enter from the new map edges, add new ones there with developer mode (launch option --developerMode, then press Home in game to open the object placement panel) or a mod that can place outside connections in game.\n   Do not remove all of the old ones: with no road, rail, ship or air outside connection left, the vanilla game crashes when companies export goods.\n\n2. Check the water and sea level:\n{WATER_NOTE}" },
                // 水体一段：按水源的实际处理结果三选一，C# 端取文本后填进 {WATER_NOTE}
                { "VANILLA_CONVERT.WaterKept", "   • The existing water sources were kept; the water was reset to the sea level\n   • ⚠ Water levels might not be perfectly accurate after the conversion. Use the built-in Water Tools (M button) to adjust the sea level and speed up filling." },
                { "VANILLA_CONVERT.WaterUpgraded", "   • River, lake and sea-water sources were kept and converted to the new water system; the old constant sea sources were replaced by the sea level\n   • ⚠ Water levels might not be perfectly accurate after the conversion. Use the built-in Water Tools (M button) to adjust the sea level and speed up filling." },
                { "VANILLA_CONVERT.WaterRemoved", "   • The old water sources could not be converted and were all removed; place new river and sea sources where needed\n   • ⚠ Use the built-in Water Tools (M button) to adjust the sea level and speed up filling." },
                // 通知條版本（短文案）：完成對話框可能被其它 Mod 的 UI 重建沖掉，這條留在通知區
                { "VANILLA_CONVERT.CompleteNotify", "Conversion finished - saved as '{SAVE_NAME}'. You must restart the game before playing it." },
                // 原版地图开新城（v4.12.0 整合计划 2.4）：分块只解锁中央 9 格（D2），外部连接保留（D1）
                { "VANILLA_CONVERT.NewGameMessage", "The selected map is a vanilla 14 km map. Extend it to the current mode ({TARGET_MODE}) before starting the new city?\n\n⚠ HIGHLY EXPERIMENTAL - the following will happen:\n• The terrain heightmap is synthesized: the original 14 km stays in the center, the background terrain fills the rest\n• Map tiles are rebuilt for the new size; only the central 9 tiles start unlocked, the rest can be bought as usual\n• Starting tiles set by the map author are not kept\n• The map's natural resources and ground water are kept in the center; the new area starts without them\n• Outside connections stay at the old 14 km border\n• Water is reset to the sea level\n\nThe map itself will NOT be modified.\nThe new city will be saved automatically (Format: CityName_MapExtMode).\nYou MUST restart the game after conversion to prevent water glitches or crashes." },
                { "VANILLA_CONVERT.NewGameConfirm", "Extend and Start" },
                { "VANILLA_CONVERT.NewGameCompleteMessage", "The vanilla map has been extended to {TARGET_MODE}.\nNew save: {SAVE_NAME}\n\n• Terrain heightmap synthesized\n• Map tiles rebuilt; the central 9 tiles are unlocked\n• The map's natural resources and ground water kept in the center\n• Water reset to the sea level\n⚠ Outside connections are still at the old 14 km border\n\n⚠ RESTART REQUIRED\nYou MUST quit to desktop and load the new save.\nFailure to restart will cause water simulation glitches or crashes!" },
                { "VANILLA_CONVERT.ExpandSaveTitle", "Extend 28 km Save" },
                { "VANILLA_CONVERT.ExpandSaveMessage", "This save was made in MapExt {SOURCE_MODE}.\n\nExtend it to the current mode ({TARGET_MODE})? The 28 km city stays in the center of the new map, at the same world position.\n\n⚠ HIGHLY EXPERIMENTAL - the following will happen:\n• Playable area will be expanded to new limits, and all 529 map tiles will be unlocked\n• Terrain heightmap is synthesized: the 28 km terrain is downsampled into the center (about 14 m per pixel instead of 7 m, so slopes and shorelines may change slightly); the background terrain fills the rest, or flat land if the save has none\n• Natural resources, ground water, ground and air pollution and soil water are kept in the center; the new area starts without them\n• Derived data such as land value and noise are recalculated by the simulation\n• All vehicle and resident entities will be cleared\n• Outside connections stay at the old border, so trade and immigration continue; to have traffic enter from the new map edges, add new ones there with developer mode or a mod\n• Water sources will be upgraded and sea level reset\n\nThe original save file will NOT be modified.\nA new save will be created (Format: SaveName_MapExtMode).\nYou MUST restart the game after conversion to prevent water glitches or crashes." },
                { "VANILLA_CONVERT.ExpandCompleteMessage", "The 28 km save has been extended to {TARGET_MODE}.\nNew save: {SAVE_NAME}\n\n• Terrain heightmap synthesized; the 28 km city is in the center of the new map, at the same world position\n{TERRAIN_NOTE}\n• Natural resources, ground water, ground and air pollution and soil water kept in the center\n• All vehicle and resident entities cleared\n• Outside connections kept at the old border\n• Water reset to the sea level\n⚠ All 529 map tiles unlocked\n\n⚠ RESTART REQUIRED\nYou MUST quit to desktop and reload the new save.\nFailure to restart will cause water simulation glitches or crashes!\n\n★ AFTER RESTART - TODO LIST:\n\n1. (Optional) Add outside connections at the new map edges:\n   The existing ones stay at the old border, so trade and immigration continue. To have roads, railways, shipping lanes and airline routes enter from the new map edges, add new ones there with developer mode (launch option --developerMode, then press Home in game to open the object placement panel) or a mod that can place outside connections in game.\n   Do not remove all of the old ones: with no road, rail, ship or air outside connection left, the vanilla game crashes when companies export goods.\n\n2. Check the water and sea level:\n{WATER_NOTE}" },
                { "VANILLA_CONVERT.ExpandTerrainBackdrop", "• The terrain of the new area comes from the map's background terrain" },
                { "VANILLA_CONVERT.ExpandTerrainFlat", "⚠ The save had no background terrain covering the new map (for example, a city converted from 14 km), so the new area is flat land" },
                { "VANILLA_CONVERT.ExpandTerrainFlatChannels", "⚠ The save had no background terrain covering the new map (for example, a city converted from 14 km), so the new area is flat land; the sea at the old border was extended in straight channels to the new map edge, keeping it connected to the open sea" },
                { "VANILLA_CONVERT.QuitConfirm", "Quit Game" },
                { "VANILLA_CONVERT.QuitCancel", "Stay" },
                { "VANILLA_CONVERT.EnableFirst", "This vanilla save must be converted before it can be played in the current mode. Enable '⚠ Map Extension Conversion' under MapSize → Save Conversion (Experimental), then load it again." },
                { "VANILLA_CONVERT.Failed", "The conversion failed at step '{STEP}':\n{ERROR}\n\nThe original save or map was not modified. The game is paused; do not keep playing this city, just return to the main menu." },
                { "VANILLA_CONVERT.SaveFailed", "The conversion finished, but saving '{SAVE_NAME}' failed.\n\nThe original save or map was not modified. The game is paused; save it under a new name yourself before quitting." },
                // 否决：地形实际尺寸不是源尺寸（反序列化阶段判定，加载结束后说明）
                { "VANILLA_CONVERT.VetoedTitle", "Conversion Cancelled" },
                { "VANILLA_CONVERT.Vetoed", "'{NAME}' already has {TERRAIN_SIZE} terrain, but this conversion expects {SOURCE_SIZE}.\n\nThe conversion was cancelled and nothing was changed; the city has been loaded as it is." },

                // === 存档入口闸门 ===
                { "SAVE_GATE.DegradedTitle", "MapExt Is Not Fully Loaded" },
                { "SAVE_GATE.DegradedMessage", "Some critical MapExt components failed to load this session: {COMPONENTS}.\n\nLoading now may write data of the wrong map size into this city's save. Go back and restart the game; if this keeps happening, wait for a MapExt update." },
                { "SAVE_GATE.DegradedLoadAnyway", "Load Anyway (Not Recommended)" },
                { "SAVE_GATE.DegradedBack", "Go Back" },
                { "SAVE_GATE.DegradedBlocked", "Some critical MapExt components failed to load this session ({COMPONENTS}), so this load was stopped. Load the city from the main menu, or restart the game." },
                { "SAVE_GATE.ConvertFromMenu", "This save must be converted before it can be played in the current mode. Return to the main menu and load it from the save list to convert it." },

                // === 开新城：按地图面积推断制作模式（v4.12.0 整合计划 2.3）===
                { "SAVE_GATE.StretchTitle", "Vanilla Map Will Be Stretched" },
                { "SAVE_GATE.StretchMessage", "'{MAP_NAME}' is a vanilla 14 km map, but MapExt is set to '{CURRENT_MODE}'. A new city on it gets its terrain stretched to the current map size: slopes flatten out, while water sources, outside connections and map tiles stay where they were, so only the central 14 km can ever be bought. Natural resources are also generated randomly instead of using the ones painted on the map.\n\n'{CURRENT_MODE}' cannot convert vanilla maps. Use a map made for this mode instead." },
                { "SAVE_GATE.StretchMessageConvertOff", "'{MAP_NAME}' is a vanilla 14 km map, but MapExt is set to '{CURRENT_MODE}'. A new city on it gets its terrain stretched to the current map size: slopes flatten out, while water sources, outside connections and map tiles stay where they were, so only the central 14 km can ever be bought. Natural resources are also generated randomly instead of using the ones painted on the map.\n\nTo extend the map properly instead, enable '⚠ Map Extension Conversion' under MapSize → Save Conversion (Experimental), then start the new city again." },
                { "SAVE_GATE.StretchContinue", "Continue Anyway" },
                { "SAVE_GATE.StretchBack", "Go Back" },
                { "SAVE_GATE.StretchBlocked", "'{MAP_NAME}' is a vanilla 14 km map, and in '{CURRENT_MODE}' its terrain would be stretched, so this new city was not started. Start it from the main menu, where you can choose what to do." },
                { "SAVE_GATE.MapMismatch", "'{MAP_NAME}' was made for '{MAP_MODE}', but MapExt is set to '{CURRENT_MODE}', so its terrain would be sampled at the wrong scale.\n\nSwitch MapExt's MapSize Mode to '{MAP_MODE}' and restart the game, then start the new city again." },
                { "SAVE_GATE.DegradedVanillaMap", "Some critical MapExt components failed to load this session ({COMPONENTS}), so the vanilla map '{MAP_NAME}' can be neither extended nor loaded correctly in '{CURRENT_MODE}'. The new city was not started; restart the game and try again." },

                // === Map size mode apply (restart required) ===
                { "MODE_APPLY.Title", "Map Size Mode Saved" },
                { "MODE_APPLY.Message", "MapExt will run in '{NEW_MODE}' after a full restart of the game.\n\nUntil then this session keeps running in '{CURRENT_MODE}', and saves made for '{NEW_MODE}' cannot be loaded." },
                { "MODE_APPLY.Unchanged", "MapExt is already running in '{CURRENT_MODE}'. No restart is needed." },
                { "MODE_APPLY.Quit", "Quit Game" },
                { "MODE_APPLY.Later", "Later" },
                { "MODE_APPLY.OK", "OK" }
            };

            en = new MemorySource(fallback);

            // 中文简体本地化
            var zhHans = new Dictionary<string, string>
            {
                // === 存档验证对话框 ===
                { "LOAD_VALIDATION.TitleError", "加载错误" },
                { "LOAD_VALIDATION.TitleWarning", "警告" },
                { "LOAD_VALIDATION.TitleLegacy", "检测到旧版存档" },
                { "LOAD_VALIDATION.ModNotUsed", "此存档未使用 MapExt Mod 创建。不允许加载。请在主菜单 MapExt 选项中设置 '{NONE_MODE}' 模式。" },
                { "LOAD_VALIDATION.Mismatch", "模式不匹配！此存档需要 '{SAVED_MODE}'，但您的 Mod 当前设置为 '{CURRENT_MODE}'。请在选项菜单中更改 MapExt 的地图尺寸模式以匹配存档。" },
                { "LOAD_VALIDATION.LegacyMismatch", "这是旧版存档。如果是 57km 地图，请在选项菜单中将 Mod 切换为 '{LEGACY_MODE}'。其他地图尺寸请阅读 MapExt 的最新说明！" },
                { "LOAD_VALIDATION.ConfirmOK", "确定" },
                { "LOAD_VALIDATION.ConfirmContinue", "继续" },

                // === 地图尺寸模式不匹配警告 ===
                { "MAPEXT_MAPSIZE.MismatchTitle", "⚠️ 地图尺寸模式不匹配" },
                { "MAPEXT_MAPSIZE.MismatchMessage", "此地图是在「{AUTHORED_MODE}」下制作的，但 MapExt 当前设置为「{CURRENT_MODE}」。\n\n地形高度正在以错误的比例采样，海拔、水体与可建造区域都会出错。\n\n解决方法：退出到主菜单，将 MapExt 的地图尺寸模式设为「{AUTHORED_MODE}」，然后重新加载此地图。\n\n说明：编辑器导出的地图不含地图尺寸元数据，因此只能在加载开始后才检测得到。" },
                // 通知条版本（短文案）
                { "MAPEXT_MAPSIZE.MismatchNotify", "此地图为「{AUTHORED_MODE}」制作，但 MapExt 当前为「{CURRENT_MODE}」，地形高度采样比例错误。请退出到主菜单，将地图尺寸模式改为「{AUTHORED_MODE}」后重新加载。" },

                { "MAPEXT_MAPSIZE.StretchTitle", "⚠️ 原版地图已被拉伸" },
                { "MAPEXT_MAPSIZE.StretchMessage", "这座新城用的地图是原版 14 km 地形，但 MapExt 当前为「{CURRENT_MODE}」，地形已按当前尺寸拉伸：坡度变缓，水源、外部连接与地图分块却停在原处，只有中央 14 km 能够购买。自然资源也改为随机生成，没有用地图里画好的。\n\n建议不要继续经营这座城市。请返回主菜单，改用为「{CURRENT_MODE}」制作的地图开新城。" },
                { "MAPEXT_MAPSIZE.StretchNotify", "这座城市的原版 14 km 地形已按「{CURRENT_MODE}」拉伸：坡度变缓，只有中央 14 km 能够购买。" },
                { "MAPEXT_MAPSIZE.StretchEditorMessage", "这张地图是原版 14 km 地形，但 MapExt 当前为「{CURRENT_MODE}」，编辑器里显示的是按当前尺寸拉伸后的地形；地图分块没有重建，仍只覆盖中央 14 km。\n\n照这样存回去，会得到一张地形为「{CURRENT_MODE}」、可购买范围却只有中央 14 km 的地图。如果只想按原版尺寸编辑，请把 MapExt 的地图尺寸模式切到「{NONE_MODE}」并重启游戏。" },
                { "MAPEXT_MAPSIZE.StretchEditorNotify", "这张原版 14 km 地图按「{CURRENT_MODE}」拉伸显示，地图分块仍只覆盖中央 14 km。要按原版尺寸编辑，请把 MapExt 切到「{NONE_MODE}」。" },

                // === 世界地图导入性能警告 ===
                { "MAPEXT_WORLDMAP.WarningTitle", "⚠️ 性能警告" },
                { "MAPEXT_WORLDMAP.WarningMessage", "导入世界地图(WorldMap)会引入额外的渲染开销：\n\n• 增加一层级联渲染层（baseLod 提升）\n• 每帧执行 DownSampleHeightMap 降采样\n• MinMaxMap 尺寸增大（1024 vs 512）\n• GPU 显存占用增加\n\n在大地图模式下可能导致帧率下降。是否继续？" },
                { "MAPEXT_WORLDMAP.ConfirmImport", "仍然导入" },
                { "MAPEXT_WORLDMAP.CancelImport", "取消" },

                // === 原版地图扩展对话框 ===
                { "VANILLA_CONVERT.Title", "扩展原版地图" },
                { "VANILLA_CONVERT.Message", "此存档未使用 MapExt Mod（原版 14km）。\n\n是否扩展至当前模式（{TARGET_MODE}）？\n\n⚠ 高度实验性 - 转换过程将执行以下操作：\n• 扩展可游玩区域至新边界\n• 解锁全部 529 格地图分块\n• 合成并扩展地形高度图\n• 完美保留原版自然资源与地下水\n• 清除所有车辆与居民实体\n• 外部连接保留在原边界，城市照常贸易与迁入；想让车流从新地图边缘进出，可用开发者模式或 Mod 在新边界补建\n• 升级水源并重置海平面\n\n原始存档不会被修改。\n转换后将自动保存为新存档 (格式: 存档名_MapExt模式)。\n必须重启游戏！否则将导致水体表现异常或游戏崩溃。" },
                { "VANILLA_CONVERT.Confirm", "扩展并加载" },
                { "VANILLA_CONVERT.Cancel", "取消" },
                { "VANILLA_CONVERT.Complete", "扩展完成" },
                { "VANILLA_CONVERT.CompleteMessage", "原版地图已成功扩展至 {TARGET_MODE}。\n新存档：{SAVE_NAME}\n\n• 地形高度图已合成\n• 原版自然资源与地下水已完美保留\n• 所有车辆与居民实体已清除\n• 外部连接保留在原边界\n• 水体已重置到海平面\n⚠ 全部 529 格地图分块已解锁\n\n⚠ 必须重启游戏\n请立即退出到桌面并重新加载新存档。\n不重启直接游玩会导致水体异常或游戏崩溃！\n\n★ 重启后待办事项：\n\n1. （可选）在新的地图边界补建外部连接：\n   外部连接仍在原边界，城市照常贸易与迁入。想让道路、铁路、航道、航线从新地图边缘进出，可用开发者模式（启动参数 --developerMode，游戏内按 Home 键打开物件摆放面板）或可在游戏内放置外部连接的 Mod 在新边界补建。\n   旧的不要全部删掉：一个交通类外部连接都不剩时，原版游戏会在企业出口商品时闪退。\n\n2. 检查水体与海平面：\n{WATER_NOTE}" },
                { "VANILLA_CONVERT.WaterKept", "   • 原有水源全部保留，水体已重置到海平面\n   • ⚠ 提示：转换后水位可能不够准确。建议使用内建水体工具（M按钮）手动调整海平面并加速注水。" },
                { "VANILLA_CONVERT.WaterUpgraded", "   • 河流、湖泊与海水水源已保留并换算到新水体系统，旧式恒定海源改由海平面代替\n   • ⚠ 提示：转换后水位可能不够准确。建议使用内建水体工具（M按钮）手动调整海平面并加速注水。" },
                { "VANILLA_CONVERT.WaterRemoved", "   • 旧版水源无法换算，已全部删除，请在所需位置重新放置河流与海洋水源\n   • ⚠ 提示：建议使用内建水体工具（M按钮）手动调整海平面并加速注水。" },
                // 通知条版本（短文案）
                { "VANILLA_CONVERT.CompleteNotify", "转换完成，已存为「{SAVE_NAME}」。开始游玩前必须重启游戏。" },
                { "VANILLA_CONVERT.NewGameMessage", "所选地图是原版 14 km 地图。是否先扩展至当前模式（{TARGET_MODE}）再开新城？\n\n⚠ 高度实验性 - 转换过程将执行以下操作：\n• 合成地形高度图：原版 14 km 留在中央，外围由背景地形补齐\n• 按新尺寸重建地图分块：开局只解锁中央 9 格，其余照常购买\n• 地图作者设定的起始分块不保留\n• 地图的自然资源与地下水保留在中央，新增区域开局没有\n• 外部连接留在原 14 km 边界\n• 水体重置到海平面\n\n地图本身不会被修改。\n新城将自动保存为新存档（格式：城市名_MapExt模式）。\n转换后必须重启游戏！否则将导致水体表现异常或游戏崩溃。" },
                { "VANILLA_CONVERT.NewGameConfirm", "扩展并开始" },
                { "VANILLA_CONVERT.NewGameCompleteMessage", "原版地图已扩展至 {TARGET_MODE}。\n新存档：{SAVE_NAME}\n\n• 已合成地形高度图\n• 已重建地图分块，中央 9 格已解锁\n• 地图的自然资源与地下水保留在中央\n• 水体已重置到海平面\n⚠ 外部连接仍在原 14 km 边界\n\n⚠ 必须重启游戏\n请退出到桌面，再读取新存档。\n不重启会导致水体模拟异常或游戏崩溃！" },
                { "VANILLA_CONVERT.ExpandSaveTitle", "扩展 28 km 存档" },
                { "VANILLA_CONVERT.ExpandSaveMessage", "此存档是在 MapExt {SOURCE_MODE} 下保存的。\n\n是否扩展至当前模式（{TARGET_MODE}）？原 28 km 城市留在新地图中央，世界位置不变。\n\n⚠ 高度实验性 - 转换过程将执行以下操作：\n• 扩展可游玩区域至新边界，解锁全部 529 格地图分块\n• 合成地形高度图：原 28 km 地形降采样后放在中央（精度由约 7 m 降为约 14 m，坡地与岸线可能略有变化），外围由背景地形补齐，没有背景时为平地\n• 自然资源、地下水、地面与空气污染、土壤含水量保留在中央，新增区域开局没有\n• 地价、噪音等派生数据由模拟重新计算\n• 清除所有车辆与居民实体\n• 外部连接保留在原边界，城市照常贸易与迁入；想让车流从新地图边缘进出，可用开发者模式或 Mod 在新边界补建\n• 升级水源并重置海平面\n\n原始存档不会被修改。\n转换后将自动保存为新存档 (格式: 存档名_MapExt模式)。\n必须重启游戏！否则将导致水体表现异常或游戏崩溃。" },
                { "VANILLA_CONVERT.ExpandCompleteMessage", "28 km 存档已扩展至 {TARGET_MODE}。\n新存档：{SAVE_NAME}\n\n• 地形高度图已合成，原 28 km 城市在新地图中央，世界位置不变\n{TERRAIN_NOTE}\n• 自然资源、地下水、地面与空气污染、土壤含水量已保留在中央\n• 所有车辆与居民实体已清除\n• 外部连接保留在原边界\n• 水体已重置到海平面\n⚠ 全部 529 格地图分块已解锁\n\n⚠ 必须重启游戏\n请立即退出到桌面并重新加载新存档。\n不重启直接游玩会导致水体异常或游戏崩溃！\n\n★ 重启后待办事项：\n\n1. （可选）在新的地图边界补建外部连接：\n   外部连接仍在原边界，城市照常贸易与迁入。想让道路、铁路、航道、航线从新地图边缘进出，可用开发者模式（启动参数 --developerMode，游戏内按 Home 键打开物件摆放面板）或可在游戏内放置外部连接的 Mod 在新边界补建。\n   旧的不要全部删掉：一个交通类外部连接都不剩时，原版游戏会在企业出口商品时闪退。\n\n2. 检查水体与海平面：\n{WATER_NOTE}" },
                { "VANILLA_CONVERT.ExpandTerrainBackdrop", "• 新增区域的地形取自原地图的背景地形" },
                { "VANILLA_CONVERT.ExpandTerrainFlat", "⚠ 存档里没有能覆盖新地图的背景地形（例如由 14 km 转换来的城市），新增区域为平地" },
                { "VANILLA_CONVERT.ExpandTerrainFlatChannels", "⚠ 存档里没有能覆盖新地图的背景地形（例如由 14 km 转换来的城市），新增区域为平地；原边界上的海已笔直延伸到新地图边缘，与外海相通" },
                { "VANILLA_CONVERT.QuitConfirm", "退出游戏" },
                { "VANILLA_CONVERT.QuitCancel", "留在游戏" },
                { "VANILLA_CONVERT.EnableFirst", "这个原版存档要先转换，才能在当前模式下游玩。请在「地图尺寸」→「存档转换 (实验性)」中启用「⚠ 地图扩展转换」，再重新读取。" },
                { "VANILLA_CONVERT.Failed", "转换在「{STEP}」一步失败：\n{ERROR}\n\n原存档或地图没有被修改。游戏已暂停，请不要继续游玩这座城市，返回主菜单即可。" },
                { "VANILLA_CONVERT.SaveFailed", "转换已完成，但自动保存「{SAVE_NAME}」失败。\n\n原存档或地图没有被修改。游戏已暂停，请在退出前手动另存为新档。" },
                { "VANILLA_CONVERT.VetoedTitle", "转换已取消" },
                { "VANILLA_CONVERT.Vetoed", "「{NAME}」的地形实际是 {TERRAIN_SIZE}，而这次转换要求 {SOURCE_SIZE}。\n\n转换已取消，没有做任何改动，城市已按原样载入。" },

                // === 存档入口闸门 ===
                { "SAVE_GATE.DegradedTitle", "MapExt 未完整加载" },
                { "SAVE_GATE.DegradedMessage", "本次游戏有 MapExt 关键组件加载失败：{COMPONENTS}。\n\n现在加载，可能把错误尺寸的数据写进这座城市的存档。建议返回并重启游戏；如果反复出现，请等待 MapExt 更新。" },
                { "SAVE_GATE.DegradedLoadAnyway", "仍要加载（不推荐）" },
                { "SAVE_GATE.DegradedBack", "返回" },
                { "SAVE_GATE.DegradedBlocked", "本次游戏有 MapExt 关键组件加载失败（{COMPONENTS}），已阻止这次加载。请从主菜单读取存档，或重启游戏。" },
                { "SAVE_GATE.ConvertFromMenu", "这个存档要先转换，才能在当前模式下游玩。请回到主菜单，从存档列表读取它进行转换。" },

                { "SAVE_GATE.StretchTitle", "原版地图会被拉伸" },
                { "SAVE_GATE.StretchMessage", "「{MAP_NAME}」是原版 14 km 地图，但 MapExt 当前为「{CURRENT_MODE}」。用它开新城，地形会被拉伸到当前尺寸：坡度变缓，水源、外部连接与地图分块却停在原处，只有中央 14 km 能够购买。自然资源也会改为随机生成，不用地图里画好的。\n\n「{CURRENT_MODE}」不支持转换原版地图，请改用为这个模式制作的地图。" },
                { "SAVE_GATE.StretchMessageConvertOff", "「{MAP_NAME}」是原版 14 km 地图，但 MapExt 当前为「{CURRENT_MODE}」。用它开新城，地形会被拉伸到当前尺寸：坡度变缓，水源、外部连接与地图分块却停在原处，只有中央 14 km 能够购买。自然资源也会改为随机生成，不用地图里画好的。\n\n要正确扩展这张地图，请在「地图尺寸」→「存档转换 (实验性)」中启用「⚠ 地图扩展转换」，再重新开新城。" },
                { "SAVE_GATE.StretchContinue", "仍要继续" },
                { "SAVE_GATE.StretchBack", "返回" },
                { "SAVE_GATE.StretchBlocked", "「{MAP_NAME}」是原版 14 km 地图，在「{CURRENT_MODE}」下地形会被拉伸，已阻止这次开新城。请从主菜单开新城，在那里选择处理方式。" },
                { "SAVE_GATE.MapMismatch", "「{MAP_NAME}」是为「{MAP_MODE}」制作的，但 MapExt 当前为「{CURRENT_MODE}」，地形会按错误的比例采样。\n\n请把 MapExt 的地图尺寸模式改为「{MAP_MODE}」并重启游戏，再开新城。" },
                { "SAVE_GATE.DegradedVanillaMap", "本次游戏有 MapExt 关键组件加载失败（{COMPONENTS}），原版地图「{MAP_NAME}」在「{CURRENT_MODE}」下既无法扩展、也无法正确载入，已阻止开新城。请重启游戏后再试。" },

                // === 应用地图尺寸模式（需重启） ===
                { "MODE_APPLY.Title", "地图尺寸模式已保存" },
                { "MODE_APPLY.Message", "完全重启游戏后，MapExt 将以「{NEW_MODE}」运行。\n\n在此之前，本次游戏仍按「{CURRENT_MODE}」运行，为「{NEW_MODE}」制作的存档也无法加载。" },
                { "MODE_APPLY.Unchanged", "MapExt 当前已在「{CURRENT_MODE}」下运行，无需重启。" },
                { "MODE_APPLY.Quit", "退出游戏" },
                { "MODE_APPLY.Later", "稍后" },
                { "MODE_APPLY.OK", "确定" },
            };
            hans = new MemorySource(zhHans);

            // 中文繁体本地化
            var zhHant = new Dictionary<string, string>
            {
                // === 存档验证对话框 ===
                { "LOAD_VALIDATION.TitleError", "載入錯誤" },
                { "LOAD_VALIDATION.TitleWarning", "警告" },
                { "LOAD_VALIDATION.TitleLegacy", "偵測到舊版存檔" },
                { "LOAD_VALIDATION.ModNotUsed", "此存檔未使用 MapExt Mod 建立。不允許載入。請在主選單 MapExt 選項中設置 '{NONE_MODE}' 模式。" },
                { "LOAD_VALIDATION.Mismatch", "模式不符！此存檔需要 '{SAVED_MODE}'，但您的 Mod 當前設定為 '{CURRENT_MODE}'。請在選項選單中變更 MapExt 的地圖尺寸模式以符合存檔。" },
                { "LOAD_VALIDATION.LegacyMismatch", "這是舊版存檔。如果是 57km 地圖，請在選項選單中將 Mod 切換為 '{LEGACY_MODE}'。其他地圖尺寸請閱讀 MapExt 的最新說明！" },
                { "LOAD_VALIDATION.ConfirmOK", "確定" },
                { "LOAD_VALIDATION.ConfirmContinue", "繼續" },

                // === 地圖尺寸模式不符警告 ===
                { "MAPEXT_MAPSIZE.MismatchTitle", "⚠️ 地圖尺寸模式不符" },
                { "MAPEXT_MAPSIZE.MismatchMessage", "此地圖是在「{AUTHORED_MODE}」下製作的，但 MapExt 當前設定為「{CURRENT_MODE}」。\n\n地形高度正以錯誤的比例取樣，海拔、水體與可建造區域都會出錯。\n\n解決方法：退出至主選單，將 MapExt 的地圖尺寸模式設為「{AUTHORED_MODE}」，然後重新載入此地圖。\n\n說明：編輯器匯出的地圖不含地圖尺寸元資料，因此只能在載入開始後才偵測得到。" },
                // 通知條版本（短文案）
                { "MAPEXT_MAPSIZE.MismatchNotify", "此地圖為「{AUTHORED_MODE}」製作，但 MapExt 當前為「{CURRENT_MODE}」，地形高度取樣比例錯誤。請退出至主選單，將地圖尺寸模式改為「{AUTHORED_MODE}」後重新載入。" },

                { "MAPEXT_MAPSIZE.StretchTitle", "⚠️ 原版地圖已被拉伸" },
                { "MAPEXT_MAPSIZE.StretchMessage", "這座新城用的地圖是原版 14 km 地形，但 MapExt 當前為「{CURRENT_MODE}」，地形已按當前尺寸拉伸：坡度變緩，水源、外部連接與地圖分塊卻停在原處，只有中央 14 km 能夠購買。自然資源也改為隨機生成，沒有用地圖裡畫好的。\n\n建議不要繼續經營這座城市。請返回主選單，改用為「{CURRENT_MODE}」製作的地圖開新城。" },
                { "MAPEXT_MAPSIZE.StretchNotify", "這座城市的原版 14 km 地形已按「{CURRENT_MODE}」拉伸：坡度變緩，只有中央 14 km 能夠購買。" },
                { "MAPEXT_MAPSIZE.StretchEditorMessage", "這張地圖是原版 14 km 地形，但 MapExt 當前為「{CURRENT_MODE}」，編輯器裡顯示的是按當前尺寸拉伸後的地形；地圖分塊沒有重建，仍只覆蓋中央 14 km。\n\n照這樣存回去，會得到一張地形為「{CURRENT_MODE}」、可購買範圍卻只有中央 14 km 的地圖。如果只想按原版尺寸編輯，請把 MapExt 的地圖尺寸模式切到「{NONE_MODE}」並重啟遊戲。" },
                { "MAPEXT_MAPSIZE.StretchEditorNotify", "這張原版 14 km 地圖按「{CURRENT_MODE}」拉伸顯示，地圖分塊仍只覆蓋中央 14 km。要按原版尺寸編輯，請把 MapExt 切到「{NONE_MODE}」。" },

                // === 世界地图导入性能警告 ===
                { "MAPEXT_WORLDMAP.WarningTitle", "⚠️ 性能警告" },
                { "MAPEXT_WORLDMAP.WarningMessage", "匯入世界地圖(WorldMap)會引入額外的渲染開銷：\n\n• 增加一層級聯渲染層（baseLod 提升）\n• 每幀執行 DownSampleHeightMap 降採樣\n• MinMaxMap 尺寸增大（1024 vs 512）\n• GPU 顯示記憶體佔用增加\n\n在大地圖模式下可能導致幀率下降。是否繼續？" },
                { "MAPEXT_WORLDMAP.ConfirmImport", "仍然匯入" },
                { "MAPEXT_WORLDMAP.CancelImport", "取消" },

                // === 原版地图扩展对话框 ===
                { "VANILLA_CONVERT.Title", "擴展原版地圖" },
                { "VANILLA_CONVERT.Message", "此存檔未使用 MapExt Mod（原版 14km）。\n\n是否擴展至當前模式（{TARGET_MODE}）？\n\n⚠ 高度實驗性 - 轉換過程將執行以下操作：\n• 擴展可遊玩區域至新邊界\n• 解鎖全部 529 格地圖分塊\n• 合成並擴展地形高度圖\n• 完美保留原版自然資源與地下水\n• 清除所有車輛與居民實體\n• 外部連接保留在原邊界，城市照常貿易與遷入；想讓車流從新地圖邊緣進出，可用開發者模式或 Mod 在新邊界補建\n• 升級水源并重置海平面\n\n原始存檔不會被修改。\n轉換後將自動儲存為新存檔 (格式: 存檔名_MapExt模式)。\n必須重啟遊戲！否則將導致水體表現異常或遊戲崩潰。" },
                { "VANILLA_CONVERT.Confirm", "擴展並載入" },
                { "VANILLA_CONVERT.Cancel", "取消" },
                { "VANILLA_CONVERT.Complete", "擴展完成" },
                { "VANILLA_CONVERT.CompleteMessage", "原版地圖已成功擴展至 {TARGET_MODE}。\n新存檔：{SAVE_NAME}\n\n• 地形高度圖已合成\n• 原版自然資源與地下水已完美保留\n• 所有車輛與居民實體已清除\n• 外部連接保留在原邊界\n• 水體已重置到海平面\n⚠ 全部 529 格地圖分塊已解鎖\n\n⚠ 必須重啟遊戲\n請立即退出到桌面並重新載入新存檔。\n不重啟直接遊玩會導致水體異常或遊戲崩潰！\n\n★ 重啟後待辦事項：\n\n1. （可選）在新的地圖邊界補建外部連接：\n   外部連接仍在原邊界，城市照常貿易與遷入。想讓道路、鐵路、航道、航線從新地圖邊緣進出，可用開發者模式（啟動參數 --developerMode，遊戲內按 Home 鍵開啟物件擺放面板）或可在遊戲內放置外部連接的 Mod 在新邊界補建。\n   舊的不要全部刪掉：一個交通類外部連接都不剩時，原版遊戲會在企業出口商品時閃退。\n\n2. 檢查水體與海平面：\n{WATER_NOTE}" },
                { "VANILLA_CONVERT.WaterKept", "   • 原有水源全部保留，水體已重置到海平面\n   • ⚠ 提示：轉換後水位可能不夠準確。建議使用內建水體工具（M按鈕）手動調整海平面並加速注水。" },
                { "VANILLA_CONVERT.WaterUpgraded", "   • 河流、湖泊與海水水源已保留並換算到新水體系統，舊式恆定海源改由海平面代替\n   • ⚠ 提示：轉換後水位可能不夠準確。建議使用內建水體工具（M按鈕）手動調整海平面並加速注水。" },
                { "VANILLA_CONVERT.WaterRemoved", "   • 舊版水源無法換算，已全部刪除，請在所需位置重新放置河流與海洋水源\n   • ⚠ 提示：建議使用內建水體工具（M按鈕）手動調整海平面並加速注水。" },
                // 通知條版本（短文案）
                { "VANILLA_CONVERT.CompleteNotify", "轉換完成，已存為「{SAVE_NAME}」。開始遊玩前必須重啟遊戲。" },
                { "VANILLA_CONVERT.NewGameMessage", "所選地圖是原版 14 km 地圖。是否先擴展至當前模式（{TARGET_MODE}）再開新城？\n\n⚠ 高度實驗性 - 轉換過程將執行以下操作：\n• 合成地形高度圖：原版 14 km 留在中央，外圍由背景地形補齊\n• 按新尺寸重建地圖分塊：開局只解鎖中央 9 格，其餘照常購買\n• 地圖作者設定的起始分塊不保留\n• 地圖的自然資源與地下水保留在中央，新增區域開局沒有\n• 外部連接留在原 14 km 邊界\n• 水體重置到海平面\n\n地圖本身不會被修改。\n新城將自動儲存為新存檔（格式：城市名_MapExt模式）。\n轉換後必須重啟遊戲！否則將導致水體表現異常或遊戲崩潰。" },
                { "VANILLA_CONVERT.NewGameConfirm", "擴展並開始" },
                { "VANILLA_CONVERT.NewGameCompleteMessage", "原版地圖已擴展至 {TARGET_MODE}。\n新存檔：{SAVE_NAME}\n\n• 已合成地形高度圖\n• 已重建地圖分塊，中央 9 格已解鎖\n• 地圖的自然資源與地下水保留在中央\n• 水體已重置到海平面\n⚠ 外部連接仍在原 14 km 邊界\n\n⚠ 必須重啟遊戲\n請退出到桌面，再讀取新存檔。\n不重啟會導致水體模擬異常或遊戲崩潰！" },
                { "VANILLA_CONVERT.ExpandSaveTitle", "擴展 28 km 存檔" },
                { "VANILLA_CONVERT.ExpandSaveMessage", "此存檔是在 MapExt {SOURCE_MODE} 下儲存的。\n\n是否擴展至當前模式（{TARGET_MODE}）？原 28 km 城市留在新地圖中央，世界位置不變。\n\n⚠ 高度實驗性 - 轉換過程將執行以下操作：\n• 擴展可遊玩區域至新邊界，解鎖全部 529 格地圖分塊\n• 合成地形高度圖：原 28 km 地形降採樣後放在中央（精度由約 7 m 降為約 14 m，坡地與岸線可能略有變化），外圍由背景地形補齊，沒有背景時為平地\n• 自然資源、地下水、地面與空氣污染、土壤含水量保留在中央，新增區域開局沒有\n• 地價、噪音等衍生資料由模擬重新計算\n• 清除所有車輛與居民實體\n• 外部連接保留在原邊界，城市照常貿易與遷入；想讓車流從新地圖邊緣進出，可用開發者模式或 Mod 在新邊界補建\n• 升級水源並重置海平面\n\n原始存檔不會被修改。\n轉換後將自動儲存為新存檔 (格式: 存檔名_MapExt模式)。\n必須重啟遊戲！否則將導致水體表現異常或遊戲崩潰。" },
                { "VANILLA_CONVERT.ExpandCompleteMessage", "28 km 存檔已擴展至 {TARGET_MODE}。\n新存檔：{SAVE_NAME}\n\n• 地形高度圖已合成，原 28 km 城市在新地圖中央，世界位置不變\n{TERRAIN_NOTE}\n• 自然資源、地下水、地面與空氣污染、土壤含水量已保留在中央\n• 所有車輛與居民實體已清除\n• 外部連接保留在原邊界\n• 水體已重置到海平面\n⚠ 全部 529 格地圖分塊已解鎖\n\n⚠ 必須重啟遊戲\n請立即退出到桌面並重新載入新存檔。\n不重啟直接遊玩會導致水體異常或遊戲崩潰！\n\n★ 重啟後待辦事項：\n\n1. （可選）在新的地圖邊界補建外部連接：\n   外部連接仍在原邊界，城市照常貿易與遷入。想讓道路、鐵路、航道、航線從新地圖邊緣進出，可用開發者模式（啟動參數 --developerMode，遊戲內按 Home 鍵開啟物件擺放面板）或可在遊戲內放置外部連接的 Mod 在新邊界補建。\n   舊的不要全部刪掉：一個交通類外部連接都不剩時，原版遊戲會在企業出口商品時閃退。\n\n2. 檢查水體與海平面：\n{WATER_NOTE}" },
                { "VANILLA_CONVERT.ExpandTerrainBackdrop", "• 新增區域的地形取自原地圖的背景地形" },
                { "VANILLA_CONVERT.ExpandTerrainFlat", "⚠ 存檔裡沒有能覆蓋新地圖的背景地形（例如由 14 km 轉換來的城市），新增區域為平地" },
                { "VANILLA_CONVERT.ExpandTerrainFlatChannels", "⚠ 存檔裡沒有能覆蓋新地圖的背景地形（例如由 14 km 轉換來的城市），新增區域為平地；原邊界上的海已筆直延伸到新地圖邊緣，與外海相通" },
                { "VANILLA_CONVERT.QuitConfirm", "退出遊戲" },
                { "VANILLA_CONVERT.QuitCancel", "留在遊戲" },
                { "VANILLA_CONVERT.EnableFirst", "這個原版存檔要先轉換，才能在當前模式下遊玩。請在「地圖尺寸」→「存檔轉換 (實驗性)」中啟用「⚠ 地圖擴展轉換」，再重新讀取。" },
                { "VANILLA_CONVERT.Failed", "轉換在「{STEP}」一步失敗：\n{ERROR}\n\n原存檔或地圖沒有被修改。遊戲已暫停，請不要繼續遊玩這座城市，返回主選單即可。" },
                { "VANILLA_CONVERT.SaveFailed", "轉換已完成，但自動儲存「{SAVE_NAME}」失敗。\n\n原存檔或地圖沒有被修改。遊戲已暫停，請在退出前手動另存為新檔。" },
                { "VANILLA_CONVERT.VetoedTitle", "轉換已取消" },
                { "VANILLA_CONVERT.Vetoed", "「{NAME}」的地形實際是 {TERRAIN_SIZE}，而這次轉換要求 {SOURCE_SIZE}。\n\n轉換已取消，沒有做任何改動，城市已按原樣載入。" },

                // === 存档入口闸门 ===
                { "SAVE_GATE.DegradedTitle", "MapExt 未完整載入" },
                { "SAVE_GATE.DegradedMessage", "本次遊戲有 MapExt 關鍵元件載入失敗：{COMPONENTS}。\n\n現在載入，可能把錯誤尺寸的資料寫進這座城市的存檔。建議返回並重啟遊戲；如果反覆出現，請等待 MapExt 更新。" },
                { "SAVE_GATE.DegradedLoadAnyway", "仍要載入（不建議）" },
                { "SAVE_GATE.DegradedBack", "返回" },
                { "SAVE_GATE.DegradedBlocked", "本次遊戲有 MapExt 關鍵元件載入失敗（{COMPONENTS}），已阻止這次載入。請從主選單讀取存檔，或重啟遊戲。" },
                { "SAVE_GATE.ConvertFromMenu", "這個存檔要先轉換，才能在當前模式下遊玩。請回到主選單，從存檔列表讀取它進行轉換。" },

                { "SAVE_GATE.StretchTitle", "原版地圖會被拉伸" },
                { "SAVE_GATE.StretchMessage", "「{MAP_NAME}」是原版 14 km 地圖，但 MapExt 當前為「{CURRENT_MODE}」。用它開新城，地形會被拉伸到當前尺寸：坡度變緩，水源、外部連接與地圖分塊卻停在原處，只有中央 14 km 能夠購買。自然資源也會改為隨機生成，不用地圖裡畫好的。\n\n「{CURRENT_MODE}」不支援轉換原版地圖，請改用為這個模式製作的地圖。" },
                { "SAVE_GATE.StretchMessageConvertOff", "「{MAP_NAME}」是原版 14 km 地圖，但 MapExt 當前為「{CURRENT_MODE}」。用它開新城，地形會被拉伸到當前尺寸：坡度變緩，水源、外部連接與地圖分塊卻停在原處，只有中央 14 km 能夠購買。自然資源也會改為隨機生成，不用地圖裡畫好的。\n\n要正確擴展這張地圖，請在「地圖尺寸」→「存檔轉換 (實驗性)」中啟用「⚠ 地圖擴展轉換」，再重新開新城。" },
                { "SAVE_GATE.StretchContinue", "仍要繼續" },
                { "SAVE_GATE.StretchBack", "返回" },
                { "SAVE_GATE.StretchBlocked", "「{MAP_NAME}」是原版 14 km 地圖，在「{CURRENT_MODE}」下地形會被拉伸，已阻止這次開新城。請從主選單開新城，在那裡選擇處理方式。" },
                { "SAVE_GATE.MapMismatch", "「{MAP_NAME}」是為「{MAP_MODE}」製作的，但 MapExt 當前為「{CURRENT_MODE}」，地形會按錯誤的比例取樣。\n\n請把 MapExt 的地圖尺寸模式改為「{MAP_MODE}」並重啟遊戲，再開新城。" },
                { "SAVE_GATE.DegradedVanillaMap", "本次遊戲有 MapExt 關鍵元件載入失敗（{COMPONENTS}），原版地圖「{MAP_NAME}」在「{CURRENT_MODE}」下既無法擴展、也無法正確載入，已阻止開新城。請重啟遊戲後再試。" },

                // === 套用地圖尺寸模式（需重啟） ===
                { "MODE_APPLY.Title", "地圖尺寸模式已儲存" },
                { "MODE_APPLY.Message", "完全重啟遊戲後，MapExt 將以「{NEW_MODE}」運行。\n\n在此之前，本次遊戲仍按「{CURRENT_MODE}」運行，為「{NEW_MODE}」製作的存檔也無法載入。" },
                { "MODE_APPLY.Unchanged", "MapExt 目前已在「{CURRENT_MODE}」下運行，無需重啟。" },
                { "MODE_APPLY.Quit", "退出遊戲" },
                { "MODE_APPLY.Later", "稍後" },
                { "MODE_APPLY.OK", "確定" }
            };
            hant = new MemorySource(zhHant);
        }
    }
}
