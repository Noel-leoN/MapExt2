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

                // === Vanilla Map Extension Dialog ===
                { "VANILLA_CONVERT.Title", "Extend Vanilla Map" },
                { "VANILLA_CONVERT.Message", "This save was created without MapExt (14km vanilla).\n\nExtend to current mode ({TARGET_MODE})?\n\n⚠ HIGHLY EXPERIMENTAL - the following will happen:\n• Playable area will be expanded to new limits\n• All 529 map tiles will be unlocked\n• Terrain heightmap will be synthesized and expanded\n• Original natural resources and ground water will be preserved\n• All vehicle and resident entities will be cleared\n• All outside connections will be removed (traffic/electricity/water)\n• Water sources will be upgraded and sea level reset\n\nThe original save file will NOT be modified.\nA new save will be created (Format: SaveName_MapExtMode).\nYou MUST restart the game after conversion to prevent water glitches or crashes." },
                { "VANILLA_CONVERT.Confirm", "Extend and Load" },
                { "VANILLA_CONVERT.Cancel", "Cancel" },
                { "VANILLA_CONVERT.Complete", "Extension Complete" },
                { "VANILLA_CONVERT.CompleteMessage", "Vanilla map has been extended to {TARGET_MODE}.\nNew save: {SAVE_NAME}\n\n✅ Terrain heightmap synthesized\n✅ Original natural resources and ground water preserved\n✅ All vehicle and resident entities cleared\n✅ All outside connections removed\n✅ Water sources upgraded and sea level reset\n⚠ All 529 map tiles unlocked\n\n⚠ RESTART REQUIRED\nYou MUST quit to desktop and reload the new save.\nFailure to restart will cause water simulation glitches or crashes!\n\n📋 AFTER RESTART - TODO LIST:\n\n1. Rebuild Outside Connections at new map edges:\n   • Roads (highway connections)\n   • Railways (train lines)\n   • Shipping Lanes (cargo and passenger ships)\n   • Airline Routes (airport flight paths)\n   • Electricity (power line connections)\n   • Water Supply (water pipe connections)\n\n2. Place new Water Sources and Adjust Sea Level:\n   • Original water sources have been cleared\n   • You MUST use the Water Features mod to place river/sea sources (no other mods have this feature)\n   • ⚠ Note: Water levels might not be perfectly accurate. Use Water Tools (M button) or Water Features mod to adjust and fill naturally." },
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

                // === 原版地图扩展对话框 ===
                { "VANILLA_CONVERT.Title", "扩展原版地图" },
                { "VANILLA_CONVERT.Message", "此存档未使用 MapExt Mod（原版 14km）。\n\n是否扩展至当前模式（{TARGET_MODE}）？\n\n⚠ 高度实验性 - 转换过程将执行以下操作：\n• 扩展可游玩区域至新边界\n• 解锁全部 529 格地图分块\n• 合成并扩展地形高度图\n• 完美保留原版自然资源与地下水\n• 清除所有车辆与居民实体\n• 拆除全部外部连接（交通/电力/水管）\n• 升级水源并重置海平面\n\n原始存档不会被修改。\n转换后将自动保存为新存档 (格式: 存档名_MapExt模式)。\n必须重启游戏！否则将导致水体表现异常或游戏崩溃。" },
                { "VANILLA_CONVERT.Confirm", "扩展并加载" },
                { "VANILLA_CONVERT.Cancel", "取消" },
                { "VANILLA_CONVERT.Complete", "扩展完成" },
                { "VANILLA_CONVERT.CompleteMessage", "原版地图已成功扩展至 {TARGET_MODE}。\n新存档：{SAVE_NAME}\n\n✅ 地形高度图已合成\n✅ 原版自然资源与地下水已完美保留\n✅ 所有车辆与居民实体已清除\n✅ 全部外部连接已拆除\n✅ 水源已升级并重置海平面\n⚠ 全部 529 格地图分块已解锁\n\n⚠ 必须重启游戏\n请立即退出到桌面并重新加载新存档。\n不重启直接游玩会导致水体异常或游戏崩溃！\n\n📋 重启后待办事项：\n\n1. 在新的地图边界重建对外连接：\n   • 道路（高速公路连接）\n   • 铁路（火车线路）\n   • 航道（货运与客运轮船航线）\n   • 航线（机场飞行航线）\n   • 电力（输电线路连接）\n   • 供水（供水管道连接）\n\n2. 重新设置水源与海平面：\n   • 原有水源已被清除，必须使用 Water Features 模组在所需位置重新放置河流/海洋水源（目前无其他模组具备此功能）\n   • ⚠ 提示：转换后水位可能不够准确。建议使用内建水体工具（M按钮）或 Water Features 模组手动调整海平面并加速注水。" },
                { "VANILLA_CONVERT.QuitConfirm", "退出游戏" },
                { "VANILLA_CONVERT.QuitCancel", "留在游戏" },
            };
            localizationManager.AddSource("zh-HANS", new MemorySource(zhHans));

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

                // === 世界地图导入性能警告 ===
                { "MAPEXT_WORLDMAP.WarningTitle", "⚠️ 性能警告" },
                { "MAPEXT_WORLDMAP.WarningMessage", "匯入世界地圖(WorldMap)會引入額外的渲染開銷：\n\n• 增加一層級聯渲染層（baseLod 提升）\n• 每幀執行 DownSampleHeightMap 降採樣\n• MinMaxMap 尺寸增大（1024 vs 512）\n• GPU 顯示記憶體佔用增加\n\n在大地圖模式下可能導致幀率下降。是否繼續？" },
                { "MAPEXT_WORLDMAP.ConfirmImport", "仍然匯入" },
                { "MAPEXT_WORLDMAP.CancelImport", "取消" },

                // === 原版地图扩展对话框 ===
                { "VANILLA_CONVERT.Title", "擴展原版地圖" },
                { "VANILLA_CONVERT.Message", "此存檔未使用 MapExt Mod（原版 14km）。\n\n是否擴展至當前模式（{TARGET_MODE}）？\n\n⚠ 高度實驗性 - 轉換過程將執行以下操作：\n• 擴展可遊玩區域至新邊界\n• 解鎖全部 529 格地圖分塊\n• 合成並擴展地形高度圖\n• 完美保留原版自然資源與地下水\n• 清除所有車輛與居民實體\n• 拆除全部外部連接（交通/電力/水管）\n• 升級水源并重置海平面\n\n原始存檔不會被修改。\n轉換後將自動儲存為新存檔 (格式: 存檔名_MapExt模式)。\n必須重啟遊戲！否則將導致水體表現異常或遊戲崩潰。" },
                { "VANILLA_CONVERT.Confirm", "擴展並載入" },
                { "VANILLA_CONVERT.Cancel", "取消" },
                { "VANILLA_CONVERT.Complete", "擴展完成" },
                { "VANILLA_CONVERT.CompleteMessage", "原版地圖已成功擴展至 {TARGET_MODE}。\n新存檔：{SAVE_NAME}\n\n✅ 地形高度圖已合成\n✅ 原版自然資源與地下水已完美保留\n✅ 所有車輛與居民實體已清除\n✅ 全部外部連接已拆除\n✅ 水源已升級並重置海平面\n⚠ 全部 529 格地圖分塊已解鎖\n\n⚠ 必須重啟遊戲\n請立即退出到桌面並重新載入新存檔。\n不重啟直接遊玩會導致水體異常或遊戲崩潰！\n\n📋 重啟後待辦事項：\n\n1. 在新的地圖邊界重建對外連接：\n   • 道路（高速公路連接）\n   • 鐵路（火車線路）\n   • 航道（貨運與客運輪船航線）\n   • 航線（機場飛行航線）\n   • 電力（輸電線路連接）\n   • 供水（供水管道連接）\n\n2. 重新設置水源與海平面：\n   • 原有水源已被清除，必須使用 Water Features 模組在所需位置重新放置河流/海洋水源（目前無其他模組具備此功能）\n   • ⚠ 提示：轉換後水位可能不夠準確。建議使用內建水體工具（M按鈕）或 Water Features 模組手動調整海平面並加速注水。" },
                { "VANILLA_CONVERT.QuitConfirm", "退出遊戲" },
                { "VANILLA_CONVERT.QuitCancel", "留在遊戲" }
            };
            localizationManager.AddSource("zh-HANT", new MemorySource(zhHant));

            MapExtPDX.MapExt.Core.ModLog.Ok(Tag, "存档验证与WorldMap警告本地化文本已注册 (en-US, zh-HANS, zh-HANT)");
        }
    }
}
