// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.


using Colossal.Localization;
using System.Collections.Generic;

namespace MapExtPDX.SaveLoadSystem
{
    public static class ModLocalization
    {
        private const string Tag = "SaveLoad";

        // Mod 加载时注册 fallback 本地化
        public static void Initialize(LocalizationManager localizationManager)
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

                // === WorldMap Import Warning ===
                { "MAPEXT_WORLDMAP.WarningTitle", "⚠️ Performance Warning" },
                { "MAPEXT_WORLDMAP.WarningMessage", "Importing a World Map introduces additional rendering overhead:\n\n• Extra cascade rendering layer (baseLod increased)\n• Per-frame DownSampleHeightMap for backdrop\n• Larger MinMaxMap (1024 vs 512)\n• Increased GPU memory usage\n\nThis may reduce FPS on large maps. Continue?" },
                { "MAPEXT_WORLDMAP.ConfirmImport", "Import Anyway" },
                { "MAPEXT_WORLDMAP.CancelImport", "Cancel" },

                // === Vanilla Save Conversion Dialog ===
                { "VANILLA_CONVERT.Title", "Convert Vanilla Save" },
                { "VANILLA_CONVERT.Message", "This save was created without MapExt (14km vanilla).\n\nConvert to current mode ({TARGET_MODE})?\n\n⚠ HIGHLY EXPERIMENTAL - the following will happen:\n• All 529 map tiles will be unlocked\n• Terrain heightmap will be synthesized\n• Natural resources and ground water will be regenerated\n• All vehicle and resident entities will be cleared\n• All outside connections will be removed (traffic/electricity/water)\n• Water sources will be upgraded and sea level reset\n\nThe original save file will NOT be modified.\nA new save will be created (Format: SaveName_MapExtMode).\nYou MUST restart the game after conversion to prevent water glitches or crashes." },
                { "VANILLA_CONVERT.Confirm", "Convert and Load" },
                { "VANILLA_CONVERT.Cancel", "Cancel" },
                { "VANILLA_CONVERT.Complete", "Conversion Complete" },
                { "VANILLA_CONVERT.CompleteMessage", "Vanilla save has been converted to {TARGET_MODE}.\nNew save: {SAVE_NAME}\n\n✅ Terrain heightmap synthesized\n✅ Natural resources and ground water regenerated\n✅ All vehicle and resident entities cleared\n✅ All outside connections removed\n✅ Water sources upgraded and sea level reset\n⚠ All 529 map tiles unlocked\n\n⚠ RESTART REQUIRED\nYou MUST quit to desktop and reload the new save.\nFailure to restart will cause water simulation glitches or crashes!\n\n📋 AFTER RESTART - TODO LIST:\n\n1. Rebuild Outside Connections at new map edges:\n   • Roads (highway connections)\n   • Railways (train lines)\n   • Shipping Lanes (cargo and passenger ships)\n   • Airline Routes (airport flight paths)\n   • Electricity (power line connections)\n   • Water Supply (water pipe connections)\n\n2. Place new Water Sources:\n   • Original water sources have been cleared\n   • Place river/sea water sources at desired locations\n   • Wait for water simulation to fill naturally\n   • Use Water Tools (M button) to speed up filling" },
                { "VANILLA_CONVERT.QuitConfirm", "Quit Game" },
                { "VANILLA_CONVERT.QuitCancel", "Stay" }
            };

            localizationManager.AddSource("en-US", new MemorySource(fallback));

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

                // === 世界地图导入性能警告 ===
                { "MAPEXT_WORLDMAP.WarningTitle", "⚠️ 性能警告" },
                { "MAPEXT_WORLDMAP.WarningMessage", "导入世界地图(WorldMap)会引入额外的渲染开销：\n\n• 增加一层级联渲染层（baseLod 提升）\n• 每帧执行 DownSampleHeightMap 降采样\n• MinMaxMap 尺寸增大（1024 vs 512）\n• GPU 显存占用增加\n\n在大地图模式下可能导致帧率下降。是否继续？" },
                { "MAPEXT_WORLDMAP.ConfirmImport", "仍然导入" },
                { "MAPEXT_WORLDMAP.CancelImport", "取消" },

                // === 原版存档转换对话框 ===
                { "VANILLA_CONVERT.Title", "转换原版存档" },
                { "VANILLA_CONVERT.Message", "此存档未使用 MapExt Mod（原版 14km）。\n\n是否转换为当前模式（{TARGET_MODE}）？\n\n⚠ 高度实验性 - 转换过程将执行以下操作：\n• 解锁全部 529 格地图分块\n• 合成地形高度图\n• 重新生成自然资源与地下水\n• 清除所有车辆与居民实体\n• 拆除全部外部连接（交通/电力/水管）\n• 升级水源并重置海平面\n\n原始存档不会被修改。\n转换后将自动保存为新存档 (格式: 存档名_MapExt模式)。\n必须重启游戏！否则将导致水体表现异常或游戏崩溃。" },
                { "VANILLA_CONVERT.Confirm", "转换并加载" },
                { "VANILLA_CONVERT.Cancel", "取消" },
                { "VANILLA_CONVERT.Complete", "转换完成" },
                { "VANILLA_CONVERT.CompleteMessage", "原版存档已转换为 {TARGET_MODE}。\n新存档：{SAVE_NAME}\n\n✅ 地形高度图已合成\n✅ 自然资源与地下水已重新生成\n✅ 所有车辆与居民实体已清除\n✅ 全部外部连接已拆除\n✅ 水源已升级并重置海平面\n⚠ 全部 529 格地图分块已解锁\n\n⚠ 必须重启游戏\n请立即退出到桌面并重新加载新存档。\n不重启直接游玩会导致水体异常或游戏崩溃！\n\n📋 重启后待办事项：\n\n1. 在新的地图边界重建对外连接：\n   • 道路（高速公路连接）\n   • 铁路（火车线路）\n   • 航道（货运与客运轮船航线）\n   • 航线（机场飞行航线）\n   • 电力（输电线路连接）\n   • 供水（供水管道连接）\n\n2. 重新设置水源：\n   • 原有水源已被清除\n   • 在所需位置放置河流/海洋水源\n   • 等待水体模拟自然填充\n   • 可使用水体工具（M按钮）加速注水" },
                { "VANILLA_CONVERT.QuitConfirm", "退出游戏" },
                { "VANILLA_CONVERT.QuitCancel", "留在游戏" },
            };
            localizationManager.AddSource("zh-HANS", new MemorySource(zhHans));

            MapExtPDX.MapExt.Core.ModLog.Ok(Tag, "存档验证与WorldMap警告本地化文本已注册 (en-US, zh-HANS)");
        }
    }
}
