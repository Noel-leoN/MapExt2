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
    /// 从 ECS 世界中查询指定 Bounds 内的道路中心线几何，
    /// 将贝塞尔曲线采样为 2D 线段集合供奇偶穿越判定使用。
    /// v2: 使用道路中心线（左右边缘取平均），消除双边穿越混淆。
    /// </summary>
    public static class RoadSegmentQuery
    {
        // === Constants and Fields ===

        /// <summary>
        /// 贝塞尔曲线采样精度（每条道路半段采样的线段数）。
        /// v2: 8 → 16，提升弯道精度。
        /// </summary>
        private const int kBezierSamples = 16;

        // === Helpers ===

        #region Helpers

        /// <summary>
        /// 查询 brushBounds 范围内的所有道路中心线线段（2D XZ 平面）。
        /// 使用道路左右边缘的平均值计算中心线，每条道路段产生一条连续折线。
        /// </summary>
        /// <param name="em">Entity Manager</param>
        /// <param name="brushBounds">笔刷范围（世界坐标 XZ 平面）</param>
        /// <param name="margin">AABB 预筛选扩展余量（米），默认 2m</param>
        /// <param name="allocator">NativeList 分配器，默认 TempJob（跨方法安全）</param>
        /// <returns>道路中心线线段列表，调用方负责 Dispose</returns>
        public static NativeList<Line2> GetRoadSegmentsInBounds(
            EntityManager em, Bounds2 brushBounds, float margin = 2f,
            Allocator allocator = Allocator.TempJob)
        {
            var result = new NativeList<Line2>(64, allocator);

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

                // --- 对道路前后半段分别采样中心线 ---
                SampleCenterLine(
                    geometry.m_Start.m_Left, geometry.m_Start.m_Right,
                    ref result);
                SampleCenterLine(
                    geometry.m_End.m_Left, geometry.m_End.m_Right,
                    ref result);
            }

            entities.Dispose();
            return result;
        }

        /// <summary>
        /// 将道路半段的中心线（左右边缘 Bezier 取平均）在 XZ 平面上
        /// 采样为 kBezierSamples 条线段。
        /// 中心线 = (left(t) + right(t)) / 2，消除双边穿越混淆。
        /// </summary>
        private static void SampleCenterLine(
            Bezier4x3 leftCurve, Bezier4x3 rightCurve,
            ref NativeList<Line2> segments)
        {
            // 起点: 两条曲线的控制点 a 取平均
            float2 prev = (leftCurve.a.xz + rightCurve.a.xz) * 0.5f;

            for (int i = 1; i <= kBezierSamples; i++)
            {
                float t = (float)i / kBezierSamples;
                float3 leftPt = MathUtils.Position(leftCurve, t);
                float3 rightPt = MathUtils.Position(rightCurve, t);
                float2 center = (leftPt.xz + rightPt.xz) * 0.5f;

                // 跳过退化线段（两点重合）
                float2 delta = center - prev;
                if (math.lengthsq(delta) < 1e-6f)
                {
                    prev = center;
                    continue;
                }

                segments.Add(new Line2(prev, center));
                prev = center;
            }
        }

        #endregion
    }
}
