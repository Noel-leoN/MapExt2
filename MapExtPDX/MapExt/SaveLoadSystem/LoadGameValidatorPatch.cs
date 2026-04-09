
// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// LoadGameValidatorPatch.cs
using Colossal.UI.Binding; // Contains ValueBinding
using Game.Assets;
using Game.SceneFlow;
using Game.UI;      // Contains SaveInfo and NotificationUISystem (or similar)
using Game.UI.Localization;
using Game.UI.Menu; // Contains MenuUISystem
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using System;
using System.Collections.Generic; // Contains GameManager
using System.Linq;
using static Game.UI.Menu.MenuUISystem;

namespace MapExtPDX.SaveLoadSystem
{
    // 定义一个枚举来表示验证结果，使代码更清晰
    public enum SaveValidationResult
    {
        Success,            // 加载成功 (包括旧存档在正确模式下加载)
        ModNotUsed,         // 存档未使用本Mod
        CoreValueMismatch,  // 新存档模式不匹配
        LegacyMismatch      // 旧存档模式不匹配
    }

    [HarmonyPatch(typeof(MenuUISystem), "SafeLoadGame")]
    public static class LoadGameValidatorPatch
    {
        private const string Tag = "SaveLoad";

        // 定义遗留存档应被视为的 CoreValue;通常为4；其他mapsize需手动修改存档metadata
        public const int LegacySaveCoreValue = 4;

        public const int NoneModeCoreValue = 1;

        /// <summary>
        /// 在 LoadGame 方法执行前运行。
        /// 这是我们的主要拦截点。
        /// </summary>
        /// <param name="__instance">MenuUISystem的实例</param>
        /// <param name="args">LoadGame的参数</param>
        /// <returns>返回 true 以继续执行原始方法，返回 false 以阻止执行。</returns>
        [HarmonyPrefix]
        public static bool BeforeLoadGame(MenuUISystem __instance, LoadGameArgs args, bool dismiss)
        {
            if (Mod.Instance == null || Mod.Instance.CurrentSettings.DisableLoadGameValidation)
            {
                return true; // 设置开启禁用验证，直接执行原版方法，不执行验证逻辑
            }

            ModLog.Scan(Tag, "--- 存档验证开始 ---");
            ModLog.Info(Tag, $"尝试加载存档 ID: {args.saveId}, CityName: {args.cityName}");

            // 1. 获取与UI中选定的存档相对应的SaveInfo对象
            var savesBinding = AccessTools.Field(typeof(MenuUISystem), "m_SavesBinding").GetValue(__instance) as ValueBinding<List<SaveInfo>>;
            SaveInfo saveInfo = savesBinding?.value.Find(s => s.id == args.saveId);
            if (savesBinding == null)
            {
                ModLog.Debug(Tag, "无法获取 m_SavesBinding，跳过验证");
                return false; // 无法找到SaveInfo，禁止加载
            }

            // 2. 获取当前 Mod 设置的 CoreValue
            int currentModCoreValue = PatchManager.CurrentCoreValue;
            // === LOGGING POINT 2: 调用 ValidateSave 前的已知状态 ===
            ModLog.Info(Tag, $"当前 Mod CoreValue: {currentModCoreValue}");

            // 3. 执行验证逻辑
            SaveValidationResult validationResult = ValidateSave(saveInfo, currentModCoreValue, out Dictionary<string, ILocElement> locParams);
            ModLog.Info(Tag, $"验证结果: {validationResult}");

            // 4. 根据验证结果采取行动
            switch (validationResult)
            {
                case SaveValidationResult.Success:
                    // 如果是旧存档且模式正确，ValidateSave会在这里返回Success，然后静默标记
                    if (locParams.ContainsKey("IS_LEGACY"))
                    {
                        var mods = new List<string>(saveInfo.modsEnabled ?? new string[0]);
                        mods.Add(MetaDataExtenderPatch.CoreValueKeyPrefix + LegacySaveCoreValue);
                        saveInfo.modsEnabled = mods.ToArray();
                        ModLog.Ok(Tag, $"旧版存档 '{saveInfo.displayName}' 匹配当前模式，已标记 CoreValue: {LegacySaveCoreValue}");
                    }
                    else
                    {
                        ModLog.Ok(Tag, $"存档 '{saveInfo.displayName}' 验证通过");
                    }
                    return true; // 验证成功，放行加载

                case SaveValidationResult.ModNotUsed:
                    ModLog.Debug(Tag, "存档未使用本Mod，显示对话框并阻止加载");
                    ShowInfoDialog(LocalizedString.Id("LOAD_VALIDATION.TitleError"), new LocalizedString("LOAD_VALIDATION.ModNotUsed", null, locParams));
                    return false;

                case SaveValidationResult.CoreValueMismatch:
                    ModLog.Debug(Tag, "CoreValue 不匹配，显示对话框并阻止加载");
                    ShowInfoDialog(LocalizedString.Id("LOAD_VALIDATION.TitleError"), new LocalizedString("LOAD_VALIDATION.Mismatch", null, locParams));
                    return false;

                case SaveValidationResult.LegacyMismatch:
                    ModLog.Debug(Tag, "旧版存档模式不匹配，显示对话框并阻止加载");
                    ShowInfoDialog(LocalizedString.Id("LOAD_VALIDATION.TitleError"), new LocalizedString("LOAD_VALIDATION.LegacyMismatch", null, locParams));
                    return false;
            }
            return true;
        }


        /// <summary>
        /// 核心验证逻辑，检查存档是否符合加载条件。
        /// </summary>
        private static SaveValidationResult ValidateSave(SaveInfo saveInfo, int currentModCoreValue, out Dictionary<string, ILocElement> locParams)
        {
            locParams = new Dictionary<string, ILocElement>();
            string modIdentifier = "MapExt";  // 判断存档中是否已启用MapExt2(PDX版本)
            var mods = saveInfo.modsEnabled;

            // 打印出存档中的所有 Mod，以便我们核对
            // Info($"Save's mod list contains: \n - {string.Join("\n - ", mods)}");

            // 1. 判断存档是否使用了MapExt，即是否为vanilla存档
            // === LOGGING POINT 3: 检查 Mod 列表 ===
            ModLog.Scan(Tag, $"在存档中搜索 mod 标识 '{modIdentifier}'");
            if (mods == null || !mods.Any(m => m.StartsWith(modIdentifier, StringComparison.OrdinalIgnoreCase)))
            {
                ModLog.Debug(Tag, $"存档 modsEnabled 为空或未找到 '{modIdentifier}'，判定为未使用本Mod");
                // 检测到存档未使用本Mod，现在检查当前模式是否为 "None"
                if (currentModCoreValue == NoneModeCoreValue)
                {
                    // 当前是 None 模式，允许加载原版存档
                    return SaveValidationResult.Success;
                }
                else
                {
                    // 当前不是 None 模式，不允许加载，并准备错误提示信息
                    string noneModeName = PatchManager.GetModeNameForCoreValue(NoneModeCoreValue);
                    locParams.Add("NONE_MODE", LocalizedString.Value(noneModeName));
                    return SaveValidationResult.ModNotUsed;
                }
            }


            ModLog.Info(Tag, "存档中已找到 Mod 标识，继续检查 CoreValue");

            // 2. 判断已使用MapExt存档
            string coreValueEntry = mods.FirstOrDefault(m => m.StartsWith(MetaDataExtenderPatch.CoreValueKeyPrefix));

            // 2.1 若没有CoreValue值，即旧版本存档
            if (string.IsNullOrEmpty(coreValueEntry))
            {
                ModLog.Info(Tag, "未找到 CoreValue 标签，视为旧版存档");
                ModLog.Info(Tag, $"比较 CurrentCoreValue ({currentModCoreValue}) 与 LegacyCoreValue ({LegacySaveCoreValue})");
                // 这是旧版存档，立即在这里做出最终判断
                if (currentModCoreValue == LegacySaveCoreValue)
                {
                    // 模式匹配，可以加载
                    ModLog.Ok(Tag, "旧版存档检查通过");
                    locParams.Add("IS_LEGACY", LocalizedString.Value("true")); // 添加一个标记给调用者
                    return SaveValidationResult.Success;
                }
                else
                {
                    ModLog.Debug(Tag, "旧版存档检查失败，模式不匹配");
                    // 模式不匹配，准备错误信息并返回失败
                    string legacyModeName = PatchManager.GetModeNameForCoreValue(LegacySaveCoreValue);
                    locParams.Add("LEGACY_MODE", LocalizedString.Value(legacyModeName));
                    return SaveValidationResult.LegacyMismatch;
                }
            }
            else
            {
                ModLog.Info(Tag, $"找到 CoreValue 标签: '{coreValueEntry}'，视为新版存档");

                // 这是新版存档
                if (int.TryParse(coreValueEntry.Substring(MetaDataExtenderPatch.CoreValueKeyPrefix.Length), out int savedCoreValue))
                {
                    ModLog.Info(Tag, $"解析出存档 CoreValue: {savedCoreValue}，与当前 ({currentModCoreValue}) 比较");
                    if (savedCoreValue == currentModCoreValue)
                    {
                        ModLog.Ok(Tag, "新版存档检查通过");
                        return SaveValidationResult.Success;
                    }
                    else
                    {
                        ModLog.Debug(Tag, "新版存档检查失败，CoreValue 不匹配");
                        // 模式不匹配，准备错误信息并返回失败
                        string savedModeName = PatchManager.GetModeNameForCoreValue(savedCoreValue);
                        string currentModeName = PatchManager.GetModeNameForCoreValue(currentModCoreValue);
                        locParams.Add("SAVED_MODE", LocalizedString.Value(savedModeName));
                        locParams.Add("CURRENT_MODE", LocalizedString.Value(currentModeName));
                        return SaveValidationResult.CoreValueMismatch;
                    }
                }
                else
                {
                    // 如果 CoreValue 标签存在但解析失败，也视为不匹配
                    ModLog.Debug(Tag, $"无法解析 CoreValue 标签 '{coreValueEntry}'，视为不匹配");
                    return SaveValidationResult.CoreValueMismatch;
                }
            }
        }

        /// <summary>
        /// 显示一个只有“OK”按钮的信息对话框
        /// </summary>
        private static void ShowInfoDialog(LocalizedString title, LocalizedString message)
        {
            var dialog = new MessageDialog(title, message, LocalizedString.Id("LOAD_VALIDATION.ConfirmOK"));
            GameManager.instance.userInterface.appBindings.ShowMessageDialog(dialog, null);
        }
    }
}