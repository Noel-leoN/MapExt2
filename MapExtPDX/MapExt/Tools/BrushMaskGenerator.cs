// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using Colossal.Mathematics;
using Unity.Collections;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.Tools
{
    /// <summary>
    /// 道路阻挡几何运算工具。
    /// 提供基于奇偶穿越计数的射线-线段测试 + 距离缓冲区测试。
    /// v3: 增加距离缓冲区，改善低分辨率 heightmap 下的阻挡效果。
    /// </summary>
    public static class RoadBlockMath
    {
        // === Helpers ===

        #region Helpers

        /// <summary>
        /// 综合阻挡判定：
        /// 1. 奇偶穿越测试 — 像素是否在道路对侧
        /// 2. 距离缓冲测试 — 像素是否在道路中心线 margin 范围内
        /// 两者取 OR：对侧一定阻挡，缓冲区内也阻挡（保护道路表面）。
        /// </summary>
        public static bool IsBlockedByRoad(
            float2 from, float2 to, NativeList<Line2> roadSegments, float margin)
        {
            int crossingCount = 0;
            float minDistSq = float.MaxValue;
            float marginSq = margin * margin;

            for (int i = 0; i < roadSegments.Length; i++)
            {
                var seg = roadSegments[i];

                // 穿越计数
                if (SegmentsIntersect(from, to, seg.a, seg.b))
                    crossingCount++;

                // 距离缓冲（仅当 margin > 0 时检测）
                if (margin > 0f && minDistSq > marginSq)
                {
                    float distSq = PointToSegmentDistSq(to, seg.a, seg.b);
                    minDistSq = math.min(minDistSq, distSq);
                }
            }

            // 奇数次穿越 = 对侧 → 阻挡
            if ((crossingCount & 1) == 1) return true;

            // 距离缓冲区内 → 阻挡（保护道路表面不被地形修改）
            if (margin > 0f && minDistSq <= marginSq) return true;

            return false;
        }

        /// <summary>
        /// 计算点 p 到线段 (a→b) 的最短距离的平方。
        /// 避免 sqrt 提高性能。
        /// </summary>
        public static float PointToSegmentDistSq(float2 p, float2 a, float2 b)
        {
            float2 ab = b - a;
            float2 ap = p - a;
            float abLenSq = math.lengthsq(ab);

            if (abLenSq < 1e-10f) return math.lengthsq(ap); // 退化线段

            float t = math.saturate(math.dot(ap, ab) / abLenSq);
            float2 closest = a + t * ab;
            return math.lengthsq(p - closest);
        }

        /// <summary>
        /// 经典 2D 线段相交检测（基于叉积符号判断）。
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
