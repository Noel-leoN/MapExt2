// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Collections.Generic;
using Game.SceneFlow;
using Game.UI;
using Game.UI.Localization;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// 设定页「应用」按钮的提示对话框。
    ///
    /// <para>地图尺寸模式只在启动时应用一次：模式补丁、CellMap 替换系统与 Burst Job 置换都在 OnLoad 时按模式决定，
    /// 运行中切换会让它们分属不同模式。按钮因此只保存设定，再由这里提示完全重启：
    /// 选择的模式与本会话生效的模式不同时，提供「退出游戏／稍后」；相同时只提示无需重启。</para>
    /// </summary>
    public static class ModeRestartPrompt
    {
        private const string Tag = nameof(ModeRestartPrompt);

        // === Public API ===

        #region Public API

        /// <summary>按已保存的模式与本会话生效的模式弹出对应对话框。</summary>
        public static void Show(PatchModeSetting savedMode)
        {
            try
            {
                PatchModeSetting appliedMode = PatchManager.CurrentMode;
                var locParams = new Dictionary<string, ILocElement>
                {
                    { "NEW_MODE", LocalizedString.Value(DisplayName(savedMode)) },
                    { "CURRENT_MODE", LocalizedString.Value(DisplayName(appliedMode)) },
                };

                // 延后一帧显示，沿用 VanillaSaveConversionSystem 的做法，避开设定页处理按钮的同一帧
                Colossal.Core.MainThreadDispatcher.RegisterUpdater(() =>
                {
                    var bindings = GameManager.instance.userInterface.appBindings;
                    if (savedMode == appliedMode)
                    {
                        bindings.ShowMessageDialog(new MessageDialog(
                            LocalizedString.Id("MODE_APPLY.Title"),
                            new LocalizedString("MODE_APPLY.Unchanged", null, locParams),
                            LocalizedString.Id("MODE_APPLY.OK")), null);
                        return true;
                    }

                    bindings.ShowConfirmationDialog(new ConfirmationDialog(
                        LocalizedString.Id("MODE_APPLY.Title"),
                        new LocalizedString("MODE_APPLY.Message", null, locParams),
                        LocalizedString.Id("MODE_APPLY.Quit"),
                        LocalizedString.Id("MODE_APPLY.Later")), result =>
                    {
                        if (result == 0)
                        {
                            ModLog.Info(Tag, $"用户选择退出游戏，下次启动按 {savedMode} 运行");
                            GameManager.QuitGame();
                        }
                        else
                        {
                            ModLog.Info(Tag, $"用户选择稍后重启，本会话仍为 {appliedMode}");
                        }
                    });
                    return true;
                });
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"显示模式提示对话框失败: {ex.Message}");
            }
        }

        #endregion

        // === Helpers ===

        #region Helpers

        /// <summary>对话框中显示的模式名，如 "ModeA 57km"。</summary>
        private static string DisplayName(PatchModeSetting mode) =>
            PatchManager.GetModeNameForCoreValue(PatchManager.GetCoreValueForMode(mode));

        #endregion
    }
}
