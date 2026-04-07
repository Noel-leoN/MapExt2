// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using HarmonyLib;
using MapExtPDX.MapExt.MapSizePatchSet;
using System;
using System.Collections.Generic;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// MapSize模式补丁集管理器
    /// </summary>
    public static class PatchManager
    {
        // --- 日志封装 ---
        private static readonly string ModName = Mod.ModName;
        private static readonly string patchTypeName = nameof(PatchManager);
        public static void Info(string message) => Mod.Info($"[{ModName}.{patchTypeName}] {message}");
        public static void Warn(string message) => Mod.Warn($"[{ModName}.{patchTypeName}] ⚠️ {message}");
        public static void Error(string message) => Mod.Error($"[{ModName}.{patchTypeName}] ❌ {message}");

        public static void Error(Exception e, string message) =>
            Mod.Error(e, $"[{ModName}.{patchTypeName}] ❌ {message}");

        // 定义PatchManager的Harmony实例引用字段
        private static Harmony _modePatcher;

        // 定义MapSize PatchModeSetting引用字段
        private static PatchModeSetting _currentMode;

        // 核心字段！(CV值)
        // 当前地图尺寸倍率核心值属性器；可读取/私有写入
        public static int CurrentCoreValue { get; private set; }

        // 地图尺寸原始值；用于某些补丁set调用计算
        public const int OriginalMapSize = 14336;

        // 当前缩放后的地图尺寸 = CV × OriginalMapSize
        // 供 BaseDataReader.GetReadbackBounds 等需要实际世界尺寸的场景使用
        public static int CurrentMapSize => CurrentCoreValue * OriginalMapSize;

        // CellMapSystem<T>当前kTextureSize倍率
        public static int CurrentCoreValueTex { get; private set; }

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
                { "TerrainToR16Patch", (h) => PatchHelpers.PatchAllMethodsInType(h, typeof(TerrainToR16Patch)) },

                // PatchSet2:WaterSystem
                { "WaterSystemPatch_Static", (h) => h.CreateClassProcessor(typeof(WaterSystemMethodPatches)).Patch() },
                {
                    "WaterSimulationPatch_Static",
                    (h) => h.CreateClassProcessor(typeof(WaterSimulationMethodPatches)).Patch()
                },
                {
                    "WaterSimulationLegacyPatch_Static",
                    (h) => h.CreateClassProcessor(typeof(WaterSimulationLegacyMethodPatches)).Patch()
                },
                {
                    "WaterLevelChangeSystemMethodPatches_Static",
                    (h) => h.CreateClassProcessor(typeof(WaterLevelChangeSystemMethodPatches)).Patch()
                },
                { "WaterSystem_BaseDataReader_Patch", (h) => WaterSystem_BaseDataReader_Patch.Apply(h) },
                // v2.1.1新增: WaterSystem.InitTextures()重置
                { "WaterSystemInitFix", (_) => WaterSystemReinitializer.Execute() },

                // v2.x.x新增: Layer 2 水模拟地形欺骗 (方法拦截 + 全局变量双通道)
                // 仅当地形分辨率 > 4096 时才注册 Harmony Patch，避免每帧 detour 开销
                { "WaterAdapterOnUpdatePatch", (h) => {
                    if (!ResolutionManager.NeedsDownsampleForWater)
                    {
                        ModLog.Info("PatchManager", "Terrain ≤ 4096, skipping water adapter patches");
                        return;
                    }
                    h.CreateClassProcessor(typeof(WaterSystem_OnSimulateGPU_Patch)).Patch();
                    h.CreateClassProcessor(typeof(TerrainSystem_GetCascadeTexture_Patch)).Patch();
                }},

                // v2.2.0改动
                // PatchSet3:CellMapSystem<T>托管代码部分
                { "CellMapSystemValuesPatch", (h) => CellMapSystemPatchManager.ApplyPatches(h) },

                // PatchSet4:AirWaySystem
                { "AirwaySystemPatch", (h) => h.CreateClassProcessor(typeof(AirwaySystem_OnUpdate_Patch)).Patch() },

                // PatchSetFinal:ReBurstJobSystems
                // 集中调用方式
                {
                    "ReBurstSystemsPatches",
                    (h) => ReBurstSystem.Core.JobPatchHelper.Apply(h,
                        ReBurstSystem.Core.JobPatchDefinitions.GetCellSystemTargets(CurrentMode))
                },
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
                        "WaterSimulationPatch_Static",
                        "WaterSimulationLegacyPatch_Static",
                        "WaterLevelChangeSystemMethodPatches_Static",
                        "WaterSystem_BaseDataReader_Patch",
                        "WaterSystemInitFix",
                        "WaterAdapterOnUpdatePatch",
                        "CellMapSystemValuesPatch",
                        "AirwaySystemPatch",
                        "ReBurstSystemsPatches",
                    };

                case PatchModeSetting.ModeB: // 模式28km
                    return new List<string>
                    {
                        "TerrainSystemPatch",
                        "TerrainToR16Patch",
                        "WaterSystemPatch_Static",
                        "WaterSimulationPatch_Static",
                        "WaterSimulationLegacyPatch_Static",
                        "WaterLevelChangeSystemMethodPatches_Static",
                        "WaterSystem_BaseDataReader_Patch",
                        "WaterSystemInitFix",
                        "WaterAdapterOnUpdatePatch",
                        "CellMapSystemValuesPatch",
                        "AirwaySystemPatch",
                        "ReBurstSystemsPatches",
                    };

                case PatchModeSetting.ModeC: // 模式114km
                    return new List<string>
                    {
                        "TerrainSystemPatch",
                        "TerrainToR16Patch",
                        "WaterSystemPatch_Static",
                        "WaterSimulationPatch_Static",
                        "WaterSimulationLegacyPatch_Static",
                        "WaterLevelChangeSystemMethodPatches_Static",
                        "WaterSystem_BaseDataReader_Patch",
                        "WaterSystemInitFix",
                        "WaterAdapterOnUpdatePatch",
                        "CellMapSystemValuesPatch",
                        "AirwaySystemPatch",
                        "ReBurstSystemsPatches",
                    };

                case PatchModeSetting.None: // 14km vanilla模式
                    return new List<string>
                    {
                        "TerrainToR16Patch",
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

            // === 初始化分辨率管理器 (必须在任何 PatchSet 应用之前) ===
            var settings = Mod.Instance?.Settings;
            if (settings != null)
            {
                ResolutionManager.Initialize(settings.TerrainResolution, settings.WaterResolution);
            }
            else
            {
                Warn("ModSettings not available, using default resolution values.");
            }

            Info(
                $"PatchManager Initialized. Effective initial mode from settings: {_currentMode}, CoreValue: {CurrentCoreValue}. " +
                $"TerrainRes={ResolutionManager.TerrainResolution}, WaterTex={ResolutionManager.WaterTextureSize}. Applying initial patches.");

            // 核心方法：执行模式加载
            ApplyPatchesForMode(_currentMode);

            Info("PatchManager初始化完成应用！(所有MapSize Modes Transpiler补丁完成执行；所有Pre/Postfix将在方法调用时执行.)");
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

        /// <summary>
        /// 核心CV设置！
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        // 获取模式对应的CV值；用于初始化、切换模式；
        internal static int GetCoreValueForMode(PatchModeSetting mode) // Changed to internal or could be public
        {
            switch (mode)
            {
                case PatchModeSetting.ModeA:
                default: return 4;
                case PatchModeSetting.ModeB: return 2;
                case PatchModeSetting.ModeC: return 8;
                // case PatchModeSetting.ModeD: return 16;
                case PatchModeSetting.None: return 1;
            }
        }

        // 核心方法：应用PatchSet's Recipe
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
                // case 16: return ("ModeD 229km");
                case 1: return ("Vanilla 14km");

                // 防止意外传入未知CV
                default:
                    return $"Unknown Mode (ID: {coreValue})";
            }
        }
    }

    /// <summary>
    /// Harmony 辅助工具：处理没有类级别 [HarmonyPatch] 属性、
    /// 但方法级别各自指定了不同目标类的 Patch 类型
    /// </summary>
    internal static class PatchHelpers
    {
        private static void Info(string message) => Mod.Info($" {Mod.ModName}.PatchHelpers:{message}");
        private static void Warn(string message) => Mod.Warn($" {Mod.ModName}.PatchHelpers:{message}");

        /// <summary>
        /// 扫描 patchType 中所有带有 [HarmonyPatch] 属性的方法，
        /// 逐一创建 PatchProcessor 并应用。
        /// 解决 CreateClassProcessor 要求类级别属性的限制。
        /// </summary>
        public static void PatchAllMethodsInType(HarmonyLib.Harmony harmony, System.Type patchType)
        {
            int patchedCount = 0;
            foreach (var method in patchType.GetMethods(
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.DeclaredOnly))
            {
                var patchAttrs = method.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false);
                if (patchAttrs.Length == 0) continue;

                try
                {
                    // 从属性中提取目标类和方法信息
                    System.Type targetType = null;
                    string targetMethodName = null;

                    foreach (HarmonyLib.HarmonyPatch attr in patchAttrs)
                    {
                        if (attr.info.declaringType != null) targetType = attr.info.declaringType;
                        if (attr.info.methodName != null) targetMethodName = attr.info.methodName;
                    }

                    if (targetType == null || targetMethodName == null)
                    {
                        Warn($"Skipping {method.Name}: missing target type or method name in [HarmonyPatch]");
                        continue;
                    }

                    // 查找目标方法
                    var targetMethod = HarmonyLib.AccessTools.Method(targetType, targetMethodName);
                    if (targetMethod == null)
                    {
                        Warn($"Target method {targetType.Name}.{targetMethodName} not found! Skipping {method.Name}.");
                        continue;
                    }

                    // 判断 patch 类型（Prefix/Postfix/Transpiler）
                    var processor = harmony.CreateProcessor(targetMethod);
                    if (method.GetCustomAttributes(typeof(HarmonyLib.HarmonyPrefix), false).Length > 0)
                        processor.AddPrefix(new HarmonyLib.HarmonyMethod(method));
                    else if (method.GetCustomAttributes(typeof(HarmonyLib.HarmonyPostfix), false).Length > 0)
                        processor.AddPostfix(new HarmonyLib.HarmonyMethod(method));
                    else if (method.GetCustomAttributes(typeof(HarmonyLib.HarmonyTranspiler), false).Length > 0)
                        processor.AddTranspiler(new HarmonyLib.HarmonyMethod(method));
                    else
                    {
                        Warn($"Skipping {method.Name}: no Prefix/Postfix/Transpiler attribute found.");
                        continue;
                    }

                    processor.Patch();
                    patchedCount++;
                    Info($"Patched {targetType.Name}.{targetMethodName} via {method.Name}");
                }
                catch (System.Exception ex)
                {
                    Warn($"Failed to patch via {method.Name}: {ex.Message}");
                }
            }

            Info($"PatchAllMethodsInType({patchType.Name}): Applied {patchedCount} patches.");
        }
    }
}
