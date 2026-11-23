
// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// LoadGameValidatorPatch.cs
using System;
using Game.Assets;
using Game.UI.Menu;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using static Game.UI.Menu.MenuUISystem;

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>
    /// 存档入口闸门第 1 层：主菜单读档（<c>MenuUISystem.SafeLoadGame</c>）。
    /// 判定交给 <see cref="SaveEntryGate"/>。在原方法写入城市配置覆盖项、更新"上次存档名"之前判定，拦下不留副作用。
    /// 类名与补丁方法名保持不变，注册表导出里这一行与以往相同。
    /// </summary>
    [HarmonyPatch(typeof(MenuUISystem), "SafeLoadGame")]
    public static class LoadGameValidatorPatch
    {
        private const string Tag = nameof(LoadGameValidatorPatch);

        /// <returns>true 放行原方法；false 已拦下（弹框或等玩家确认后重新触发）。</returns>
        [HarmonyPrefix]
        public static bool BeforeLoadGame(MenuUISystem __instance, LoadGameArgs args, bool dismiss)
        {
            try
            {
                // 取不到存档（反射失败或 ID 不在列表里）就放行，交给汇合点判定
                SaveInfo save = SaveEntryGate.FindMenuSave(__instance, args.saveId);
                if (save == null) return true;

                GateDecision decision = SaveEntryGate.EvaluateSave(SaveEntry.MenuLoad, save);
                return SaveEntryGate.HandleAtMenu(SaveEntry.MenuLoad, decision, save.id, save,
                    () => AccessTools.Method(typeof(MenuUISystem), "SafeLoadGame").Invoke(__instance, new object[] { args, dismiss }));
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, "读档校验异常，交给汇合点判定");
                return true;
            }
        }
    }
}
