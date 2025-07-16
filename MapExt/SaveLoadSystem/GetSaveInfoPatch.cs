// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// MetaDataExtenderPatch.cs
using Game.Assets;      // 包含 SaveInfo
using Game.UI.Menu; // 包含 MenuUISystem
using HarmonyLib;
using System.Collections.Generic;

namespace MapExtPDX.SaveLoadSystem
{
    [HarmonyPatch(typeof(MenuUISystem), nameof(MenuUISystem.GetSaveInfo))]
    public static class MetaDataExtenderPatch
    {
        // CV在SaveInfo中的前缀
        public const string CoreValueKeyPrefix = "MapEXT_CoreValue=";

        /// <summary>
        /// 在 GetSaveInfo 方法执行后运行。
        /// 获取创建好的 SaveInfo 对象，并在其 modsEnabled 数组中添加CV。
        /// </summary>
        [HarmonyPostfix]
        public static void AddCoreValueToSaveInfo(ref SaveInfo __result)
        {
            if (__result == null) return;

            // 获取当前激活模式的 CoreValue
            int currentCoreValue = PatchManager.CurrentCoreValue;
            string coreValueEntry = CoreValueKeyPrefix + currentCoreValue;

            // 获取现有的 mods 数组
            List<string> mods = new List<string>(__result.modsEnabled ?? new string[0]);

            // 移除任何旧的 CoreValue 条目，以防万一（例如，在同一次会话中连续保存）
            mods.RemoveAll(mod => mod.StartsWith(CoreValueKeyPrefix));

            // 添加新的 CoreValue 条目
            mods.Add(coreValueEntry);

            // 将修改后的列表转换回数组并更新到 SaveInfo 对象中
            __result.modsEnabled = mods.ToArray();

            Mod.Info($"[SaveLoadSystem] Injected CoreValue ({coreValueEntry}) into SaveInfo's modsEnabled list.");
        }
    }
}
