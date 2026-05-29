// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.Tools
{
    /// <summary>
    /// 道路几何体查询工具。
    /// 从 ECS 世界中查询指定 Bounds 内的道路 EdgeGeometry，
    /// 将贝塞尔曲线采样为线段集合供遮罩计算使用。
    /// 支持道路宽度余量（margin），将线段向法线方向膨胀。
    /// </summary>
    public static class RoadSegmentQuery
    {
        // === Constants and Fields ===

        /// <summary>贝塞尔曲线采样精度（每条道路段采样的线段数）</summary>
        private const int kBezierSamples = 8;

        // === Helpers ===

        #region Helpers

        /// <summary>
        /// 查询 brushBounds 范围内的所有道路边缘线段（2D XZ 平面）。
        /// 道路线段会根据 margin 参数向外法线方向膨胀，扩大保护区域。
        /// </summary>
        /// <param name="em">Entity Manager</param>
        /// <param name="brushBounds">笔刷范围（世界坐标 XZ 平面）</param>
        /// <param name="margin">道路两侧向外扩展的余量（米），默认 2m</param>
        /// <returns>道路线段列表，调用方负责 Dispose</returns>
        public static NativeList<Line2> GetRoadSegmentsInBounds(
            EntityManager em, Bounds2 brushBounds, float margin = 2f,
            Allocator allocator = Allocator.TempJob)
        {
            var result = new NativeList<Line2>(32, allocator);

            // 查询所有含 EdgeGeometry 的非删除、非临时 Entity
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<EdgeGeometry>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var geometry = em.GetComponentData<EdgeGeometry>(entities[i]);

                // --- 快速 AABB 排除（含余量扩展）---
                Bounds2 edgeBounds = new Bounds2(
                    geometry.m_Bounds.min.xz - margin,
                    geometry.m_Bounds.max.xz + margin);

                if (!MathUtils.Intersect(edgeBounds, brushBounds))
                    continue;

                // --- 对道路段的左右边缘贝塞尔曲线各采样 ---
                // 采样时根据 margin 向外法线方向膨胀线段
                SampleBezierToSegments(geometry.m_Start.m_Left, margin, ref result);
                SampleBezierToSegments(geometry.m_Start.m_Right, margin, ref result);
                SampleBezierToSegments(geometry.m_End.m_Left, margin, ref result);
                SampleBezierToSegments(geometry.m_End.m_Right, margin, ref result);
            }

            entities.Dispose();
            return result;
        }

        /// <summary>
        /// 将 Bezier4x3 曲线在 XZ 平面上采样为 kBezierSamples 条线段。
        /// 如果 margin > 0，每条线段向法线方向双侧膨胀生成两条平行线段。
        /// </summary>
        private static void SampleBezierToSegments(
            Bezier4x3 curve, float margin, ref NativeList<Line2> segments)
        {
            float2 prev = curve.a.xz;

            for (int i = 1; i <= kBezierSamples; i++)
            {
                float t = (float)i / kBezierSamples;
                float3 point = MathUtils.Position(curve, t);
                float2 current = point.xz;

                // 跳过退化线段（两点重合）
                float2 delta = current - prev;
                float lenSq = math.lengthsq(delta);
                if (lenSq < 1e-6f)
                {
                    prev = current;
                    continue;
                }

                if (margin > 0f)
                {
                    // 向外法线方向膨胀线段（道路宽度余量）
                    float2 dir = delta * math.rsqrt(lenSq); // normalize
                    float2 normal = new float2(-dir.y, dir.x); // 左法线

                    // 生成两条平行线段：原始 + 左右偏移
                    segments.Add(new Line2(prev + normal * margin, current + normal * margin));
                    segments.Add(new Line2(prev - normal * margin, current - normal * margin));
                }
                else
                {
                    segments.Add(new Line2(prev, current));
                }

                prev = current;
            }
        }

        #endregion
    }
}
