// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit MapExt2 and the author.

using System;
using System.Threading.Tasks;
using Colossal.Core;
using Colossal.IO.AssetDatabase;
using Colossal.PSI.Common;
using Game;
using Game.Assets;
using Game.PSI;
using Game.SceneFlow;
using Game.UI;
using Game.UI.Localization;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using UnityEngine;

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>
    /// 载入后补判命中（未经入口闸门的尺寸不符或扩展模式降级态读档）时的存档保护闸门：
    /// 拦截 <c>GameManager.Save</c> 的两个重载，在返回主菜单或加载其他存档前阻止一切存档写入
    /// （手动存档、快速存档 F5、自动存档及错误对话框中的“保存并继续／退出”）。
    ///
    /// <para><b>为何须覆盖双重载</b>：原版 <c>MenuUISystem.SaveGame</c>、<c>QuickSave</c>、
    /// <c>AutoSaveSystem.AutoSave</c> 与 <c>AppBindings</c> 均走 <c>Texture</c> 重载，
    /// 其内部先构造 <c>ScreenCaptureHelper.AsyncRequest</c>（立即分配 <c>Allocator.Persistent</c>
    /// 缓冲并发起 GPU 回读）再转发至 <c>AsyncRequest</c> 重载。在 <c>Texture</c> 重载提前返回可避免分配；
    /// 在 <c>AsyncRequest</c> 重载兜底外部 Mod 直接调用，并在跳过原方法前主动释放 <c>previewRequest</c>。</para>
    /// </summary>
    [HarmonyPatch]
    public static class GameManagerSaveGuardPatch
    {
        private const string Tag = nameof(GameManagerSaveGuardPatch);

        // === Constants ===

        #region Constants

        /// <summary>补判触发的持久通知条 ID（回主菜单或下一次加载时撤下）。</summary>
        public const string GuardNotificationId = "mapext.save_guard";

        #endregion

        // === Fields ===

        #region Fields

        private static bool s_SaveBlocked;
        private static LocalizedString s_NotifyTitle;
        private static LocalizedString s_NotifyText;
        private static LocalizedString s_BlockedDialogMessage;

        /// <summary>当前是否处于“回主菜单前拦截所有存档”锁定态。</summary>
        public static bool IsSaveBlocked => s_SaveBlocked;

        #endregion

        // === Public API ===

        #region Public API

        /// <summary>开启存档拦截锁并推送持久通知条。</summary>
        public static void Activate(
            LocalizedString notifyTitle,
            LocalizedString notifyText,
            LocalizedString blockedDialogMessage)
        {
            s_SaveBlocked = true;
            s_NotifyTitle = notifyTitle;
            s_NotifyText = notifyText;
            s_BlockedDialogMessage = blockedDialogMessage;
            PushGuardNotification();
        }

        /// <summary>解除存档拦截锁并撤下通知条（回主菜单、下一次加载开始或 Mod 卸载时调用）。</summary>
        public static void Clear()
        {
            s_SaveBlocked = false;
            s_NotifyTitle = default;
            s_NotifyText = default;
            s_BlockedDialogMessage = default;
            PopGuardNotification();
        }

        #endregion

        // === Harmony Patches ===

        #region Harmony Patches

        /// <summary>
        /// 拦截 <c>GameManager.Save(string, SaveInfo, ILocalAssetDatabase, Texture)</c>：
        /// 锁定态下在构造 <c>ScreenCaptureHelper.AsyncRequest</c> 之前提前返回，避免分配未消费的 GPU 回读请求。
        /// </summary>
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Save),
            new[] { typeof(string), typeof(SaveInfo), typeof(ILocalAssetDatabase), typeof(Texture) })]
        [HarmonyPrefix]
        public static bool BeforeSaveTexture(string saveName, SaveInfo meta, ref Task<bool> __result)
        {
            if (!s_SaveBlocked) return true;

            HandleBlockedSave(saveName, meta);
            __result = Task.FromResult(false);
            return false;
        }

        /// <summary>
        /// 拦截 <c>GameManager.Save(string, SaveInfo, ILocalAssetDatabase, ScreenCaptureHelper.AsyncRequest)</c>：
        /// 兜底绕过 <c>Texture</c> 重载的外部调用，并主动释放传入的 <paramref name="previewRequest"/> 防止原生缓冲泄漏。
        /// </summary>
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Save),
            new[] { typeof(string), typeof(SaveInfo), typeof(ILocalAssetDatabase), typeof(ScreenCaptureHelper.AsyncRequest) })]
        [HarmonyPrefix]
        public static bool BeforeSaveAsyncRequest(
            string saveName, SaveInfo meta, ScreenCaptureHelper.AsyncRequest previewRequest, ref Task<bool> __result)
        {
            if (!s_SaveBlocked) return true;

            if (previewRequest != null)
            {
                // 请求在构造时已分配 Allocator.Persistent 缓冲并发出 GPU 回读；跳过原方法时须主动释放
                _ = DisposePreviewRequestSafeAsync(previewRequest);
            }

            HandleBlockedSave(saveName, meta);
            __result = Task.FromResult(false);
            return false;
        }

        #endregion

        // === Helpers ===

        #region Helpers

        private static void HandleBlockedSave(string saveName, SaveInfo meta)
        {
            string display = string.IsNullOrEmpty(saveName) ? "?" : saveName;
            bool isAutoSave = meta?.autoSave == true;

            if (isAutoSave)
            {
                // 自动存档每周期触发一次：只记 Warn 并刷新同一条通知条，避免反复弹模态框打断操作
                ModLog.Warn(Tag, $"已拦截自动存档 '{display}'：本次读档触发载入后补判保护，返回主菜单前禁止保存");
                PushGuardNotification();
                return;
            }

            ModLog.Warn(Tag, $"已拦截存档 '{display}'：本次读档触发载入后补判保护，返回主菜单前禁止保存");
            PushGuardNotification();

            // 经 MainThreadDispatcher 延后一帧弹框，避免与原版 HandlesOverwrite 覆盖确认回调同帧撞车
            LocalizedString dialogMsg = s_BlockedDialogMessage;
            MainThreadDispatcher.RegisterUpdater(() =>
            {
                try
                {
                    if (Mod.IsUnloading || !s_SaveBlocked) return true;
                    GameManager gm = GameManager.instance;
                    if (gm == null || gm.gameMode != GameMode.Game) return true;

                    var dialog = new MessageDialog(
                        LocalizedString.Id("SAVE_GUARD.SaveBlockedTitle"),
                        dialogMsg,
                        LocalizedString.Id("LOAD_VALIDATION.ConfirmOK"));
                    gm.userInterface.appBindings.ShowMessageDialog(dialog, null);
                }
                catch (Exception ex)
                {
                    ModLog.Warn(Tag, $"显示存档拦截提示框失败：{ex.Message}");
                }

                return true;
            });
        }

        private static async Task DisposePreviewRequestSafeAsync(ScreenCaptureHelper.AsyncRequest previewRequest)
        {
            try
            {
                await previewRequest.Dispose();
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"释放被拦截存档的预览回读请求失败：{ex.Message}");
            }
        }

        private static void PushGuardNotification()
        {
            try
            {
                NotificationSystem.Push(
                    identifier: GuardNotificationId,
                    title: s_NotifyTitle,
                    text: s_NotifyText,
                    progressState: ProgressState.Warning,
                    progress: 100);
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"推送存档保护通知条失败：{ex.Message}");
            }
        }

        private static void PopGuardNotification()
        {
            try
            {
                if (NotificationSystem.Exist(GuardNotificationId))
                    NotificationSystem.Pop(GuardNotificationId);
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"撤下通知条 {GuardNotificationId} 失败：{ex.Message}");
            }
        }

        #endregion
    }
}
