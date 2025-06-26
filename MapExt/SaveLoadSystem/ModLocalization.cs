// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.


using Colossal.Localization;
using Colossal.Logging;
using System.Collections.Generic;

namespace MapExtPDX.SaveLoadSystem
{
    public static class ModLocalization
    {
        // 它的唯一职责：在Mod加载时，向游戏注册我们的后备(fallback)翻译
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
                { "LOAD_VALIDATION.ConfirmContinue", "Continue" }
            };

            localizationManager.AddSource("en-US", new MemorySource(fallback));
            Mod.Info("Custom localization source for MapExt2 has been added.");
        }
    }
}
