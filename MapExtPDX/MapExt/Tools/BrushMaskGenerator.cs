// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using Colossal.Mathematics;
using Unity.Collections;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.Tools
{
    /// <summary>
    /// 道路阻挡几何运算工具。
    /// 提供射线-线段相交测试，用于判断像素是否被道路阻隔。
    /// </summary>
    public static class RoadBlockMath
    {
        // === Helpers ===

        #region Helpers

        /// <summary>
        /// 检测线段 (from → to) 是否与任何道路线段相交。
        /// </summary>
        public static bool RayIntersectsAnySegment(
            float2 from, float2 to, NativeList<Line2> roadSegments)
        {
            for (int i = 0; i < roadSegments.Length; i++)
            {
                if (SegmentsIntersect(from, to, roadSegments[i].a, roadSegments[i].b))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 经典 2D 线段相交检测（基于叉积符号判断）。
        /// 判断线段 (a1→a2) 与线段 (b1→b2) 是否相交。
        /// </summary>
        public static bool SegmentsIntersect(float2 a1, float2 a2, float2 b1, float2 b2)
        {
            float2 d1 = a2 - a1;
            float2 d2 = b2 - b1;
            float cross = d1.x * d2.y - d1.y * d2.x;

            // 平行线段不相交
            if (math.abs(cross) < 1e-8f) return false;

            float2 d3 = b1 - a1;
            float invCross = 1f / cross;
            float t = (d3.x * d2.y - d3.y * d2.x) * invCross;
            float u = (d3.x * d1.y - d3.y * d1.x) * invCross;

            return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
        }

        #endregion
    }
}
