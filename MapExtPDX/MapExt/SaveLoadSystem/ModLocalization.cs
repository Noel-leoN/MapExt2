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
                { "MAPEXT_WORLDMAP.CancelImport", "Cancel" }
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
            };
            localizationManager.AddSource("zh-HANS", new MemorySource(zhHans));

            MapExtPDX.MapExt.Core.ModLog.Ok(Tag, "存档验证与WorldMap警告本地化文本已注册 (en-US, zh-HANS)");
        }
    }
}
