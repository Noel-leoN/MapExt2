// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Game.UI.Menu;
using HarmonyLib;

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    // This system's only job is to listen for when the game exits to the main menu.
    [HarmonyPatch(typeof(MenuUISystem), "ExitToMainMenu")]
    public static class SessionManager
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            // When exit to menu, reset the lock so our patch can run on the next game session.
            if (AirwaySystem_OnUpdate_Patch.s_HasRunThisSession)
            {
                Mod.Info("[Airway Patch] Exited to Main Menu. Resetting session lock.");
                AirwaySystem_OnUpdate_Patch.s_HasRunThisSession = false;
            }
        }
       
    }
}
