// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Colossal;
using Colossal.IO.AssetDatabase;
using Colossal.UI.Binding;
using Game.SceneFlow;
using Game.UI.Menu;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using Unity.Entities;

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>
    /// 菜单的"上次存档名"与存储位置：<c>MenuUISystem</c> 的两个私有字段，快速存档按它们定档名、存进哪个库。
    ///
    /// <para><b>转换后改指新档</b>（v4.12.0 整合计划 1.8）：读档、继续结束后原版把两者设成源档的，
    /// 不改的话转换后按 F5 会写回源档。</para>
    ///
    /// <para><b>汇合点拦下后恢复</b>（2.7）：原版读档、继续在 <c>GameManager.Load</c> 返回后不看结果，
    /// 被拦下也照样把两者设成被拦的档。菜单层失效、只剩汇合点拦截时，玩家留在城里按 F5 就会写进被拦的档。</para>
    /// </summary>
    internal static class LastSaveTarget
    {
        private const string Tag = nameof(LastSaveTarget);

        // === Fields ===

        #region Fields

        /// <summary>菜单的"上次存档名"：快速存档用它当档名。</summary>
        private static readonly FieldInfo s_NameField = AccessTools.Field(typeof(MenuUISystem), "m_LastSaveNameBinding");

        /// <summary>菜单记下的存储位置：快速存档存进这个库。</summary>
        private static readonly FieldInfo s_CloudTargetField = AccessTools.Field(typeof(MenuUISystem), "m_LastSelectedCloudTarget");

        #endregion

        // === Public API ===

        #region Public API

        /// <summary>
        /// 把两者改指 <paramref name="saveName"/>，做法照原版存档面板（<c>MenuUISystem.SaveGame</c>）。取不到字段时记 Error。
        /// </summary>
        public static void Redirect(MenuUISystem menu, string saveName, ILocalAssetDatabase db)
        {
            if (menu == null) return;

            if (!(s_NameField?.GetValue(menu) is ValueBinding<string> binding))
            {
                ModLog.Error(Tag, "取不到 MenuUISystem.m_LastSaveNameBinding（游戏更新后改名？），快速存档仍会用源档名");
                return;
            }

            binding.Update(saveName);

            if (s_CloudTargetField != null)
                s_CloudTargetField.SetValue(menu, db?.dataSource?.remoteStorageSourceName);
            else
                ModLog.Error(Tag, "取不到 MenuUISystem.m_LastSelectedCloudTarget（游戏更新后改名？），快速存档仍存进源档所在的库");

            ModLog.Info(Tag, $"上次存档名改指 '{saveName}'，快速存档不会写回源档");
        }

        /// <summary>
        /// 汇合点拦下读档时调用：记下两者现值，在原版存读档队列里排一个任务恢复。
        /// 原版读档、继续的任务结束前会把两者设成被拦的档，恢复任务排在它之后执行。
        /// 不设同名任务上限（cap）：此时正处在读档任务里，传 1 会被静默丢掉。
        /// </summary>
        public static void RestoreAfterBlockedLoad(string blockedName)
        {
            MenuUISystem menu = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<MenuUISystem>();
            if (menu == null) return;

            if (!(s_NameField?.GetValue(menu) is ValueBinding<string> binding))
            {
                ModLog.Error(Tag, "取不到 MenuUISystem.m_LastSaveNameBinding（游戏更新后改名？），拦下读档后按 F5 会写进被拦的档");
                return;
            }

            string name = binding.value;
            string cloudTarget = null;
            if (s_CloudTargetField != null)
                cloudTarget = s_CloudTargetField.GetValue(menu) as string;
            else
                ModLog.Error(Tag, "取不到 MenuUISystem.m_LastSelectedCloudTarget（游戏更新后改名？），拦下读档后按 F5 会存进被拦档所在的库");

            TaskManager.instance.EnqueueTask(SaveHelpers.kSaveLoadTaskName, () =>
            {
                Restore(menu, binding, name, cloudTarget, blockedName);
                return Task.CompletedTask;
            });
        }

        #endregion

        // === Helpers ===

        #region Helpers

        /// <summary>只在原版确实改过时写回并记日志；命令行等不更新这两项的入口什么也不做。</summary>
        private static void Restore(MenuUISystem menu, ValueBinding<string> binding, string name, string cloudTarget,
            string blockedName)
        {
            try
            {
                bool changed = false;
                if (binding.value != name)
                {
                    binding.Update(name);
                    changed = true;
                }

                if (s_CloudTargetField != null && (s_CloudTargetField.GetValue(menu) as string) != cloudTarget)
                {
                    s_CloudTargetField.SetValue(menu, cloudTarget);
                    changed = true;
                }

                if (changed)
                    ModLog.Info(Tag, $"汇合点拦下读档 '{blockedName}' 后，上次存档名恢复为 '{name ?? "（无）"}'，按 F5 不会写进被拦的档");
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, $"汇合点拦下读档 '{blockedName}' 后恢复上次存档名失败");
            }
        }

        #endregion
    }
}
