// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>
    /// 原版存档转换的全局状态标志。
    /// 由 LoadGameValidatorPatch 设置，由 VanillaSaveConversionSystem 消费。
    /// </summary>
    public static class VanillaConversionState
    {
        /// <summary>是否有待处理的原版转换</summary>
        public static bool PendingConversion { get; set; } = false;

        /// <summary>转换目标模式的 CoreValue</summary>
        public static int TargetCoreValue { get; set; } = 0;

        /// <summary>原始存档显示名（用于新档命名）</summary>
        public static string OriginalSaveName { get; set; } = string.Empty;

        /// <summary>重置所有状态</summary>
        public static void Reset()
        {
            PendingConversion = false;
            TargetCoreValue = 0;
            OriginalSaveName = string.Empty;
        }
    }
}
