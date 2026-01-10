// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.

using System.Collections.Generic;

namespace MapExtPDX.MapExt.ReBurstEcoSystem
{
    public class JobPatchTarget
    {
        public string TargetAssemblyName { get; set; }
        public string TargetTypeName { get; set; }
        public string TargetMethodName { get; set; }        
        public string OriginalJobFullName { get; set; }
        public string ReplacementJobFullName { get; set; }
        public string[] MethodParamTypes { get; set; } // 新增：支持重载

        // 构造函数
        public JobPatchTarget(
            string targetType,
            string targetMethod,
            string[] methodParams, // 新增参数
            string originalJob,
            // string replacementAsm, // 暂未使用，用于跨mod替换
            string replacementJobBaseName,
            string replacementNamespacePattern,
            int currentCoreValueForMode,
            string modeIdentifier,
            string targetAsmHint = null)
        {
            TargetAssemblyName = targetAsmHint;
            TargetTypeName = targetType;
            TargetMethodName = targetMethod;
            MethodParamTypes = methodParams;
            // 自动替换 / 为 + 以适应嵌套类反射语法
            OriginalJobFullName = originalJob.Replace('/', '+'); // Harmony/Reflection uses '+' for nested types
            // ReplacementJobAssemblyName = replacementAsm;

            // 获取ReBurstJob所在命名空间名称；
            string finalReplacementNamespace = replacementNamespacePattern
                .Replace("{MODE_PLACEHOLDER}", modeIdentifier) // 若使用ModeA/B/C/D区分
                .Replace("{CORE_VALUE_PLACEHOLDER}", currentCoreValueForMode.ToString()); // 若使用核心设置值区分
            ReplacementJobFullName = $"{finalReplacementNamespace}.{replacementJobBaseName}".Replace('/', '+');// Ensure consistency
        }

        public static class JobPatchDefinitions
        {
            // --- 定义ReBurstJob基本名称集 ---
            // Job名称的通用部分
            private class BaseJobTargetInfo
            {
                public string TargetType { get; }
                public string TargetMethod { get; }
                public string OriginalJob { get; }
                public string ReplacementJobBaseName { get; }
                public string[] MethodParamTypes { get; } // 新增属性

                public BaseJobTargetInfo(
                    string targetType, 
                    string targetMethod, 
                    string originalJob, 
                    string replacementJobBaseName, 
                    string[] methodParams = null)
                {
                    TargetType = targetType;
                    TargetMethod = targetMethod;
                    OriginalJob = originalJob;
                    ReplacementJobBaseName = replacementJobBaseName;
                    MethodParamTypes = methodParams;
                }
            }

            // 集中定义(对象类完整名/对象方法名/原Job全名/替换Job的不含命名空间的类型名)
            // 注意某些Job被不同的系统调用，因此需要完整列出
            private static readonly List<BaseJobTargetInfo> AllBaseTargets = new List<BaseJobTargetInfo>
    {        
        // ======== 经济系统相关 ========
        #region v2.2.0经济修复
		 // v2.2.0经济修复
        new BaseJobTargetInfo(
            "Game.Simulation.RentAdjustSystem",
            "OnUpdate",
            "Game.Simulation.RentAdjustSystem/AdjustRentJob",
            "AdjustRentJob"
        ),
        // v2.2.0经济修复新增
        new BaseJobTargetInfo(
            "Game.Simulation.ResidentialDemandSystem",
            "OnUpdate",
            "Game.Simulation.ResidentialDemandSystem/UpdateResidentialDemandJob",
            "UpdateResidentialDemandJob"
        ),
        // v2.2.0经济修复新增
        new BaseJobTargetInfo(
            "Game.Simulation.IndustrialDemandSystem",
            "OnUpdate",
            "Game.Simulation.IndustrialDemandSystem/UpdateIndustrialDemandJob",
            "UpdateIndustrialDemandJob"
        ),
        new BaseJobTargetInfo(
            "Game.Simulation.CommercialDemandSystem",
            "OnUpdate",
            "Game.Simulation.CommercialDemandSystem/UpdateCommercialDemandJob",
            "UpdateCommercialDemandJob"
        ),
    

        // v2.2.0版经济修复
            /*
            new BaseJobTargetInfo(
                "Game.Simulation.HouseholdFindPropertySystem",
                "OnUpdate",
                "Game.Simulation.HouseholdFindPropertySystem/PreparePropertyJob",
                "PreparePropertyJob"),
            new BaseJobTargetInfo(
                "Game.Simulation.HouseholdFindPropertySystem",
                "OnUpdate",
                "Game.Simulation.HouseholdFindPropertySystem/FindPropertyJob",
                "FindPropertyJob"
            ), 
            */
            /*
           // v2.2.0版新增修复HouseholdBehaviorSystem相关Job
           new BaseJobTargetInfo(
               "Game.Simulation.HouseholdBehaviorSystem",
               "OnUpdate",
               "Game.Simulation.HouseholdBehaviorSystem/HouseholdTickJob",
               "HouseholdTickJob"
           ),*/

            // ======= 特殊装箱调用 =======
            // 无法通用替换，采用单独HarmonyPrefix修补
            //new BaseJobTargetInfo(
            //    "Game.Simulation.CitizenPathfindSetup",
            //    "SetupFindHome",
            //    "Game.Simulation.CitizenPathfindSetup/SetupFindHomeJob",
            //    "SetupFindHomeJob"
            //), 

            #endregion

            // ... add more base targets here ...
    };

            // Define namespace pattern
            // Option 1: Using Mode Letter (A, B, C)
            private const string ReplacementNamespacePatternMode = "MapExtPDX.MapExt.ReBurstEcoSystemMode{MODE_PLACEHOLDER}";
            // Option 2: Using CoreValue directly
            //private const string ReplacementNamespacePatternCV = "MyModName.Patches.CoreValue{CORE_VALUE_PLACEHOLDER}";


            public static List<JobPatchTarget> GetTargetsForMode(PatchModeSetting mode)
            {
                var concreteTargets = new List<JobPatchTarget>();
                int coreValue = PatchManager.CurrentCoreValue;
                string modeIdentifier = GetModeIdentifier(mode); // Helper to get "A", "B", or "CoreValue2" etc.

                // Choose your pattern
                string currentNamespacePattern = ReplacementNamespacePatternMode;
                // string currentNamespacePattern = ReplacementNamespacePatternCV;

                if (mode == PatchModeSetting.None) // No targets for "None"
                {
                    Mod.Info($"[JobPatchDefinitions] No BurstJob targets for 'None' mode.");
                    return concreteTargets;
                }

                Mod.Info($"[JobPatchDefinitions] Generating targets for Mode: {mode}, CoreValue: {coreValue}, Identifier: {modeIdentifier}");

                foreach (var baseInfo in AllBaseTargets)
                {
                    concreteTargets.Add(new JobPatchTarget(
                        baseInfo.TargetType,
                        baseInfo.TargetMethod,
                        baseInfo.MethodParamTypes,
                        baseInfo.OriginalJob,
                        baseInfo.ReplacementJobBaseName,
                        currentNamespacePattern, // Pass the chosen pattern
                        coreValue,
                        modeIdentifier, // Pass the string to fill the placeholder (e.g., "A" or "2")
                        targetAsmHint: null // Or determine this if needed
                    ));
                }

                Mod.Info($"[JobPatchDefinitions] Generated {concreteTargets.Count} concrete targets.");
                return concreteTargets;
            }

            // Helper to get a string identifier for the mode (could be in PatchManager too)
            private static string GetModeIdentifier(PatchModeSetting mode)
            {
                switch (mode)
                {
                    // If using Mode Letters in namespace:
                    case PatchModeSetting.ModeA: return "A";
                    case PatchModeSetting.ModeB: return "B";
                    case PatchModeSetting.ModeC: return "C";
                    case PatchModeSetting.None: return "E";
                    // case PatchModeSetting.ModeD: return "D";
                    // If using CoreValue string in namespace:
                    // case Setting.PatchModeSetting.ModeA: return PatchManager.GetCoreValueForMode(mode).ToString(); // "2"
                    // case Setting.PatchModeSetting.ModeB: return PatchManager.GetCoreValueForMode(mode).ToString(); // "4"
                    default: return "Unknown";
                }
            }
        }

        public static List<JobPatchTarget> GetPatchTargets()
        {
            // Get the current mode from PatchManager (assuming _currentMode is accessible or a getter exists)
            PatchModeSetting currentModeEnum = PatchManager.CurrentMode;
            return JobPatchDefinitions.GetTargetsForMode(currentModeEnum);
        }


    } // end of class
} // namespace
