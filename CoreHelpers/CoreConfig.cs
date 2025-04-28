// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.
// Recommended for non-commercial use. For commercial purposes, please consider contacting the author.

using Unity.Mathematics;

namespace MapExtPDX
{
    /// <summary>
    /// 全局定义地图扩展倍数
    /// 需额外手动变更Maptile ReBurst部分
    /// 视情变更NoisePollution ReBurst内部逻辑
    /// </summary>

    // 地图大小变更仅用修改Value值
    public static class MapSizeMultiplier
    {
        // 
        public const int Value = 4; // 2=28672；4=57344；8=114688；16=229376
        public const float OriginalMapSizeValue = 14336f; // TerrainSystem使用
        public const float NewMapSizeValue = OriginalMapSizeValue * Value; // TerrainSystem使用
        public const int OriginalMapSizeValueInt = 14336; // WaterSystem/CellMapSystem使用
        public const int NewMapSizeValueInt = OriginalMapSizeValueInt * Value; // WaterSystem/CellMapSystem使用

        /// <summary>
        /// Pre-calculate original default values for comparison in Prefix
        /// 弃用
        /// </summary>
        public static readonly float2 OriginalDefaultMapSize = new float2(OriginalMapSizeValue, OriginalMapSizeValue);
        public static readonly float2 OriginalDefaultWorldSize = OriginalDefaultMapSize * 4f;
    }
}
