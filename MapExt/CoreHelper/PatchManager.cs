// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Collections.Generic;
using HarmonyLib;
using MapExtPDX.MapExt.MapSizePatchSet;
using MapExtPDX.MapExt.ReBurstSystem;
using MapExtPDX.SaveLoadSystem;

namespace MapExtPDX 
{
    /// <summary>
    /// MapSize模式补丁集管理器
    /// </summary>
    public static class PatchManager
    {
        // 日志归一化
        private static void Info(string message) => Mod.Info($" {Mod.ModName}.{nameof(PatchManager)}:{message}");
        private static void Warn(string message) => Mod.Warn($" {Mod.ModName}.{nameof(PatchManager)}:{message}");
        private static void Error(string message) => Mod.Error($" {Mod.ModName}.{nameof(PatchManager)}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {Mod.ModName}.{nameof(PatchManager)}:{message}");

        // 定义PatchManager的Harmony实例引用字段
        private static Harmony _modePatcher;

        // 定义MapSize PatchModeSetting引用字段
        private static PatchModeSetting _currentMode;

        // 核心字段！(CV值)
        // 当前地图尺寸倍率核心值属性器；可读取/私有写入
        public static int CurrentCoreValue { get; private set; }

        // 地图尺寸原始值；用于某些补丁set调用计算
        public const int OriginalMapSize = 14336;

        // 调用当前模式状态
        public static PatchModeSetting CurrentMode => _currentMode;

        // 从当前已应用保存设置中读取MapSize CoreValue;用于UI Info显示.
        public static int? LoadedSaveCoreValue { get; set; } = null;

        /// <summary>
        /// 补丁集注册表系统
        /// </summary>
        // 补丁集注册表: string是名称, Action是应用补丁的委托
        private static readonly Dictionary<string, Action<Harmony>> s_AllPatchSets;

        // 静态构造函数，用于初始化注册表
        static PatchManager()
        {
            Info("Initializing MapSize Mode PatchSet Registry...");

            s_AllPatchSets = new Dictionary<string, Action<Harmony>>
            {
                // string Key: 补丁集名称
                // action Value: 应用该补丁集方式(传统注释调用，集中方式调用)

                // PatchSet1:TerrainSystem
                { "TerrainSystemPatch", (h) => h.CreateClassProcessor(typeof(TerrainSystemPatches)).Patch() },
                 { "TerrainToR16Patch", (h) => h.CreateClassProcessor(typeof(TerrainToR16Patch)).Patch() },

                // PatchSet2:WaterSystem
                { "WaterSystemPatch_Static", (h) => h.CreateClassProcessor(typeof(WaterSystemMethodPatches)).Patch() },
                  { "WaterSystemPatch_GetSurfaceData", (h) => h.CreateClassProcessor(typeof(WaterSystem_GetSurfaceData_Patch)).Patch() },


                // PatchSet3:CellMapSystem<T>托管代码部分
                { "CellMapSystemPatch_Field", (h) => h.CreateClassProcessor(typeof(CellMapSystem_KMapSize_Field_Patches)).Patch() },
                { "CellMapSystemPatch_Method", (h) => h.CreateClassProcessor(typeof(CellMapSystem_KMapSize_Method_Patches)).Patch() },

                // PatchSet4:AirWaySystem
                { "AirWaySystemPatch", (h) => h.CreateClassProcessor(typeof(AirwaySystem_OnCreate_Patch)).Patch() },

                // PatchSetFinal:ReBurstJobSystems
                // 集中调用方式
                { "ReBurstSystemsPatches", (h) => JobPatchHelper.ApplyAllPatches(h) },

                // 转为并行补丁系统
                // PatchSetGloble:SaveLoadSystem
                //{ "MetaDataExtenderPatch", (h) => h.CreateClassProcessor(typeof(MetaDataExtenderPatch)).Patch() },
                //{ "LoadGameValidatorPatch", (h) => h.CreateClassProcessor(typeof(LoadGameValidatorPatch)).Patch() },

            };
            Info($"Registry initialized with {s_AllPatchSets.Count} patch sets.");
        }

        // 定义每个模式的“配方”
        private static List<string> GetRecipeForMode(PatchModeSetting mode)
        {
            switch (mode)
            {
                case PatchModeSetting.ModeA: // 模式57km
                default:
                    return new List<string>
                    {
                        "TerrainSystemPatch",
                        "TerrainToR16Patch",
                        "WaterSystemPatch_Static",
                        "WaterSystemPatch_GetSurfaceData",
                        "CellMapSystemPatch_Field",
                        "CellMapSystemPatch_Method",
                        "ReBurstSystemsPatches",
                        "AirWaySystemPatch",
                        //"MetaDataExtenderPatch",
                        //"LoadGameValidatorPatch"
                    };

                case PatchModeSetting.ModeB: // 模式28km
                    return new List<string>
                    {
                        "TerrainSystemPatch",
                        "TerrainToR16Patch",
                        "WaterSystemPatch_Static",
                        "WaterSystemPatch_GetSurfaceData",
                        "CellMapSystemPatch_Field",
                        "CellMapSystemPatch_Method",
                        "ReBurstSystemsPatches",
                        "AirWaySystemPatch",
                        //"MetaDataExtenderPatch",
                        //"LoadGameValidatorPatch"
                    };

                case PatchModeSetting.ModeC: // 模式114km
                    return new List<string>
                    {
                        "TerrainSystemPatch",
                        "TerrainToR16Patch",
                        "WaterSystemPatch_Static",
                        "WaterSystemPatch_GetSurfaceData",
                        "CellMapSystemPatch_Field",
                        "CellMapSystemPatch_Method",
                        "ReBurstSystemsPatches",
                        "AirWaySystemPatch",
                        //"MetaDataExtenderPatch",
                        //"LoadGameValidatorPatch"
                    };

                case PatchModeSetting.ModeD: // 模式229km
                    return new List<string>
                    {
                        "TerrainSystemPatch",
                        "TerrainToR16Patch",
                        "WaterSystemPatch_Static",
                        "WaterSystemPatch_GetSurfaceData",
                        "CellMapSystemPatch_Field",
                        "CellMapSystemPatch_Method",
                        "ReBurstSystemsPatches",
                        "AirWaySystemPatch",
                        //"MetaDataExtenderPatch",
                        //"LoadGameValidatorPatch"
                    };
                
                case PatchModeSetting.None:  // 14km vanilla模式
                    return new List<string>{
                        //"MetaDataExtenderPatch",
                        //"LoadGameValidatorPatch"
                    };
            }
        }

        // Mod主入口调用执行
        public static void Initialize(Harmony harmony, PatchModeSetting initialModeFromSettings)
        {
            // 传入Mod主入口_modePatcher harmony实例
            _modePatcher = harmony;
            
            // 从已保存设置中读取模式并应用补丁集
            // 进入游戏后Mod初始化加载，不需要进行UnpatchAll or CleanUpAllContexts
            _currentMode = initialModeFromSettings;
            CurrentCoreValue = GetCoreValueForMode(_currentMode);
            Info($"PatchManager Initialized. Effective initial mode from settings: {_currentMode}, CoreValue: {CurrentCoreValue}. Applying initial patches.");

            // if (_currentMode != PatchModeSetting.None) 
            // {
                ApplyPatchesForMode(_currentMode); 
            // }
            // else
            // {
            //     Info("Initial mode is 'None', no patches applied by default.");
            // }
        }

        // 核心功能方法
        // 切换设置新的模式 - Mod已正确初始化，进入UI设置选择并点击Apply
        public static void SetPatchMode(PatchModeSetting newMode)
        {
            // 安全验证，是否已正确创建harmony实例
            if (_modePatcher == null)
            {
                Error("Harmony instance is null in SetPatchMode. Aborting.");
                return;
            }

            // 避免点击Apply后重复修补；LoadedSaveCoreValue字段当前方案中无效，备用；
            if (_currentMode == newMode && LoadedSaveCoreValue == null) 
            {
                Info($"Patch mode {newMode} is already active. No changes applied.");
                return;
            }

            Info($"Switching patch mode from {_currentMode} (CV: {CurrentCoreValue}) to {newMode}");

            // 0.5 提前额外清理ReBurstJob上下文
            GenericJobReplacePatch.CleanUpAllContexts();

            // 1. 移除所有旧补丁，确保干净的状态
            Info("Unpatching all previous MapSize patchsets...");
            _modePatcher.UnpatchAll(Mod.HarmonyIdModes);           

            // 2. 根据新模式设置CV
            _currentMode = newMode; // Set new mode first
            CurrentCoreValue = GetCoreValueForMode(_currentMode); // Then update CV based on it
            Info($"CV set to: {CurrentCoreValue} for mode {_currentMode}");

            // 3. 根据新模式应用所需的补丁集
            //if (_currentMode != PatchModeSetting.None)
            //{
                ApplyPatchesForMode(_currentMode);
            //}

            Info($"Successfully switched to patch mode: {_currentMode}");
            LoadedSaveCoreValue = null; // Reset after applying changes prompted by save load // 当前方案并未启用该字段
        }

        // 获取模式对应的CV值；用于初始化、切换模式；
        internal static int GetCoreValueForMode(PatchModeSetting mode) // Changed to internal or could be public
        {
            switch (mode)
            {
                case PatchModeSetting.ModeA: default: return 4;
                case PatchModeSetting.ModeB: return 2;
                case PatchModeSetting.ModeC: return 8;
                case PatchModeSetting.ModeD: return 16;
                case PatchModeSetting.None:  return 1; 
            }
        }

        // 应用PatchSet's Recipe
        private static void ApplyPatchesForMode(PatchModeSetting mode)
        {
            // 获取当前模式的“配方”
            var recipe = GetRecipeForMode(mode);
            Info($"Applying patchset recipe for {mode}: [{string.Join(", ", recipe)}]");

            foreach (var setName in recipe)
            {
                if (s_AllPatchSets.TryGetValue(setName, out var applyAction))
                {
                    try
                    {
                        // 执行注册表中对应的Action来应用补丁
                        applyAction(_modePatcher);
                        Info($"-> Successfully applied patchset: {setName}");
                    }
                    catch (Exception e)
                    {
                        Error(e, $"Failed to apply patchset '{setName}'.");
                    }
                }
                else
                {
                    Warn($"Patchset '{setName}' not found in registry.");
                }
            }
        }

        /// <summary>
        /// 用于存档验证系统弹窗警告显示；
        /// 根据 CoreValue 获取其对应的模式枚举名称。
        /// 这是 GetCoreValueForMode 方法的反向操作。
        /// </summary>
        /// <param name="coreValue">要查询的 CoreValue。</param>
        /// <returns>模式的名称 (例如 "ModeA")，如果找不到则返回一个描述性文本。</returns>
        public static string GetModeNameForCoreValue(int coreValue)
        {
            switch (coreValue)
            {
                case 4: return ("ModeA 57km");
                case 2: return ("ModeB 28km");
                case 8: return ("ModeC 114km");
                case 16: return ("ModeD 229km");
                case 1: return ("Vanilla 14km");

                // 防止意外传入未知CV
                default:
                    return $"Unknown Mode (ID: {coreValue})";
            }
        }

        // 暂不使用
        public static void UnpatchAll()
        {
            if (_modePatcher != null)
            {
                GenericJobReplacePatch.CleanUpAllContexts();
                _modePatcher.UnpatchAll(_modePatcher.Id);
                Info($"All patches with ID {_modePatcher.Id} have been removed by PatchManager.");
            }
            // 重置为vanilla;待验证
            _currentMode = PatchModeSetting.None; 
            CurrentCoreValue = GetCoreValueForMode(PatchModeSetting.None);
            Info($"PatchManager reset. CurrentMode: {_currentMode}, CurrentCoreValue: {CurrentCoreValue}");
        }
    }
}
