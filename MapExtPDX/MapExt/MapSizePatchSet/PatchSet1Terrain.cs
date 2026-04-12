// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// ReSharper disable UnusedMember.Local

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    using Game.Simulation;
    using HarmonyLib;
    using MapExtPDX.MapExt.Core;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using Unity.Entities;
    using Unity.Mathematics;

    // Target TerrainSystem class
    // 修补FinalizeTerrainData/GetTerrainBounds/GetHeightData等三个方法
    [HarmonyPatch(typeof(TerrainSystem))]
    public static class TerrainSystemPatches
    {
        private const string Tag = "TerrainPatch";

        // FinalizeTerrainData (改变引入默认值，仅修改此处即可，不需要同时修补其他方法)
        // 该方法调用仅在加载存档后执行一次，使用Prefix简化维护 
        // Target the FinalizeTerrainData method
        [HarmonyPatch("FinalizeTerrainData")]
        [HarmonyPrefix]
        public static void FinalizeTerrainData_Prefix(ref float2 inMapCorner, ref float2 inMapSize,
            ref float2 inWorldCorner, ref float2 inWorldSize)
        {
            int patches = 0;

            int scalefactor = PatchManager.CurrentCoreValue;
            float baseSize = PatchManager.OriginalMapSize;

            if (math.abs(inMapSize.x - baseSize) < 1f)
            {
                inMapSize *= scalefactor;
                inWorldSize *= scalefactor;
                inMapCorner = -0.5f * inMapSize;
                inWorldCorner = -0.5f * inWorldSize;

                patches++;
            }

            if (patches != 0)
            {
                ModLog.Patch(Tag,
                    $"FinalizeTerrainData Prefix applied {patches} patch(es). (Expected value: {inMapSize} , {inMapCorner} , {inWorldSize} , {inWorldCorner})");
            }
        } // FinalizeTerrainData method


        // Target the GetTerrainBounds method
        // 高频调用
        [HarmonyPatch(nameof(TerrainSystem.GetTerrainBounds))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GetTerrainBounds_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            int scalefactor = PatchManager.CurrentCoreValue;
            float baseSize = PatchManager.OriginalMapSize;
            float newSize = scalefactor * baseSize;

            int patches = 0;

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                // Look for the instruction loading the specific float constant 14336f
                if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == baseSize)
                {
                    // Replace the operand (the constant value) with our new dimension
                    codes[i].operand = newSize;
                    patches++;
                }
            }

            if (patches == 0)
            {
                ModLog.Warn(Tag,
                    $"GetTerrainBounds_Transpiler did not find any instructions to patch! (Expected value: {newSize})");
            }
            else
            {
#if DEBUG
                ModLog.Debug(Tag, $"GetTerrainBounds_Transpiler applied {patches} patch(es).(Expected value: {newSize})");
#endif
            }

            return codes;
        } // GetTerrainBounds method


        // Target the GetHeightData method
        // 极高频调用
        [HarmonyPatch(nameof(TerrainSystem.GetHeightData))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GetHeightData_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int scalefactor = PatchManager.CurrentCoreValue;
            float baseSize = PatchManager.OriginalMapSize;
            float newSize = scalefactor * baseSize;

            // log.Info("Applying Transpiler to TerrainSystem.GetHeightData");

            int patches = 0;

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                // Look for the instruction loading the specific float constant 14336f
                if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == baseSize)
                {
#if DEBUG
                    ModLog.Debug(Tag, $"Patching instruction {i} in GetHeightData: Replacing {baseSize} with {newSize}");
#endif
                    // Replace the operand (the constant value) with our new dimension
                    codes[i].operand = newSize;
                    patches++;
                    // This method uses the value twice (for x and z in the float3 size).
                    // The loop will continue and find the second instance too.
                }
            }

            if (patches == 0)
            {
                ModLog.Warn(Tag,
                    $"GetHeightData_Transpiler did not find any instructions to patch! (Expected value: {newSize})");
            }
            else
            {
#if DEBUG
                ModLog.Debug(Tag, $"GetHeightData_Transpiler applied {patches} patch(es).(Expected value: {newSize})");
#endif
            }


            return codes;
        } // GetHeightData method


        // === 优化 1.1: StructuredBuffer 首帧扩容 ===
        // 原版 OnCreate 中初始化的 ManagedStructuredBuffers 容量偏小
        // (AreaTriangle/AreaEdge 仅 1000)，大地图下动态扩容会引发运行时卡顿
        // 在首帧 OnUpdate 时通过反射预分配更大的容量

        private static bool s_BufferExpanded = false;

        [HarmonyPatch("OnUpdate")]
        [HarmonyPostfix]
        public static void OnUpdate_BufferExpansion_Postfix(TerrainSystem __instance)
        {
            if (s_BufferExpanded) return;
            s_BufferExpanded = true;

            int cv = PatchManager.CurrentCoreValue;
            if (cv <= 1) return; // Vanilla 模式无需扩容

            // 读取 ModSettings
            if (Mod.Instance?.Settings?.TerrainBufferPrealloc != true) return;

            try
            {
                // 仅扩容容量不足的 Buffer
                // Building/Lane 原版 10000，按 CV 倍率扩容
                // Triangle/Edge 原版 1000，按 CV 倍率扩容
                // ClipMap 原版 10000，按 CV 倍率扩容
                var bufferTargets = new (string fieldName, int newCapacity)[]
                {
                    ("m_BuildingInstanceData", 10000 * cv),
                    ("m_LaneInstanceData",     10000 * cv),
                    ("m_LaneRaisedInstanceData", 10000 * cv),
                    ("m_TriangleInstanceData",  1000 * cv),
                    ("m_EdgeInstanceData",      1000 * cv),
                    ("m_ClipMapBuffer",         10000 * cv),
                };

                int expanded = 0;
                foreach (var (fieldName, newCapacity) in bufferTargets)
                {
                    var field = AccessTools.Field(typeof(TerrainSystem), fieldName);
                    if (field == null)
                    {
                        ModLog.Warn(Tag, $"未找到字段: {fieldName}");
                        continue;
                    }

                    var oldBuffer = field.GetValue(__instance);
                    if (oldBuffer == null) continue;

                    // 获取当前容量 (ManagedStructuredBuffers 没有公开 Capacity，检查内部状态)
                    // 直接 Dispose 旧的，创建新的
                    var bufferType = field.FieldType;
                    var disposeMethod = bufferType.GetMethod("Dispose");
                    disposeMethod?.Invoke(oldBuffer, null);

                    // 构造新 buffer: new ManagedStructuredBuffers<T>(newCapacity)
                    var newBuffer = Activator.CreateInstance(bufferType, new object[] { newCapacity });
                    field.SetValue(__instance, newBuffer);
                    expanded++;
                }

                if (expanded > 0)
                {
                    ModLog.Ok(Tag, $"StructuredBuffer 预扩容完成: {expanded} 个 Buffer, CV={cv}");
                }
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, $"StructuredBuffer 扩容失败: {ex.Message}");
            }
        }


        // === 优化 1.2: AsyncGPUReadback 失败降级策略 ===
        // 原版 UpdateGPUReadback 在 GPU 回读连续失败 10 次后，会回退到全图回读
        // 大地图下全图回读 (4096×4096 R16) 开销巨大
        // 修改策略：在 m_FailCount 即将达到阈值(10)时拦截，重置计数并放弃本次回读
        // 使用 Prefix 而非 Transpiler，避免修改复杂 IL 分支结构导致 InvalidProgramException

        private static FieldInfo s_FailCountField;
        private static FieldInfo s_HeightMapChangedField;
        private static bool s_ReadbackFieldsResolved = false;

        [HarmonyPatch("UpdateGPUReadback")]
        [HarmonyPrefix]
        public static void UpdateGPUReadback_FailSafe_Prefix(TerrainSystem __instance)
        {
            // Vanilla 模式无需降级
            if (PatchManager.CurrentCoreValue <= 1) return;

            // 首次调用时解析字段引用并缓存
            if (!s_ReadbackFieldsResolved)
            {
                s_ReadbackFieldsResolved = true;
                s_FailCountField = AccessTools.Field(typeof(TerrainSystem), "m_FailCount");
                s_HeightMapChangedField = AccessTools.Field(typeof(TerrainSystem), "m_HeightMapChanged");

                if (s_FailCountField == null || s_HeightMapChangedField == null)
                {
                    ModLog.Warn(Tag, "UpdateGPUReadback 降级: 无法解析 m_FailCount 或 m_HeightMapChanged 字段");
                }
            }

            if (s_FailCountField == null) return;

            // 在 m_FailCount 达到 10 之前拦截（原版在 ++m_FailCount >= 10 时触发全图回读）
            int failCount = (int)s_FailCountField.GetValue(__instance);
            if (failCount >= 9)
            {
                // 重置失败计数，防止进入全图回读分支
                s_FailCountField.SetValue(__instance, 0);
                // 放弃本次回读，等待下次正常 TriggerAsyncChange 自然恢复
                s_HeightMapChangedField?.SetValue(__instance, false);
#if DEBUG
                ModLog.Warn(Tag, $"AsyncGPUReadback 连续失败 {failCount + 1} 次，已跳过全图回读");
#endif
            }
        }

    }


    // === 优化 2.1: 远距级联降频更新（修复版） ===
    // 原版 RenderCascades 每帧渲染所有级联层
    // 对远距级联层 (baseLod+2 及以上) 每 N 帧渲染一次，降低 GPU 开销
    //
    // 修复策略（解决地形错位问题）:
    // 错位根因: UpdateCascades 每帧更新 m_CascadeRanges（世界坐标映射）
    //           但节流帧跳过渲染 → shader 用新范围采样旧纹理 → 偏移
    // 修复: 在节流帧中，将远 LOD 的 m_CascadeRanges 恢复为上次渲染时的值
    //       同步修正 shader globals，确保 range 与纹理内容始终匹配
    [HarmonyPatch(typeof(TerrainSystem), nameof(TerrainSystem.RenderCascades))]
    internal static class TerrainSystem_RenderCascades_Patch
    {
        private const string Tag = "TerrainCascade";
        private const int FarCascadeUpdateInterval = 4;

        // --- 每帧状态 ---
        private static int s_FrameCounter = 0;
        private static bool s_IsThrottledFrame = false;

        // --- 上次成功渲染时的级联范围快照 ---
        private static float4[] s_LastRenderedRanges = new float4[4];
        private static bool s_HasSavedRanges = false;

        // --- 缓存的反射字段（一次性解析） ---
        private static FieldInfo s_CascadeRangesField;
        private static FieldInfo s_ShaderCascadeRangesField;
        private static int s_ShaderPropertyID = -1;
        private static bool s_FieldsResolved = false;

        /// <summary>
        /// Prefix: 在节流帧恢复上次渲染时的远 LOD 范围，避免 range/texture 不同步
        /// </summary>
        [HarmonyPrefix]
        public static void Prefix(TerrainSystem __instance)
        {
            // 非大地图模式或用户关闭时直接返回
            if (PatchManager.CurrentCoreValue <= 1) return;
            if (Mod.Instance?.Settings?.TerrainCascadeThrottle != true) return;

            s_FrameCounter++;
            s_IsThrottledFrame = (s_FrameCounter % FarCascadeUpdateInterval != 0);

            // 更新帧: 让原版逻辑完整运行
            if (!s_IsThrottledFrame) return;
            // 首次运行无快照可恢复
            if (!s_HasSavedRanges) return;

            // --- 一次性解析反射字段 ---
            if (!s_FieldsResolved)
            {
                s_FieldsResolved = true;
                s_CascadeRangesField = AccessTools.Field(typeof(TerrainSystem), "m_CascadeRanges");
                s_ShaderCascadeRangesField = AccessTools.Field(typeof(TerrainSystem), "m_ShaderCascadeRanges");
                s_ShaderPropertyID = UnityEngine.Shader.PropertyToID("colossal_TerrainCascadeRanges");

                if (s_CascadeRangesField == null || s_ShaderCascadeRangesField == null)
                {
                    ModLog.Warn(Tag, "无法解析 m_CascadeRanges 或 m_ShaderCascadeRanges 字段");
                }
            }

            if (s_CascadeRangesField == null) return;

            int baseLod = TerrainSystem.baseLod;
            var ranges = (float4[])s_CascadeRangesField.GetValue(__instance);
            var shaderRanges = (UnityEngine.Vector4[])s_ShaderCascadeRangesField.GetValue(__instance);
            var sliceUpdated = __instance.heightMapSliceUpdated;
            if (ranges == null || shaderRanges == null || sliceUpdated == null) return;

            bool needsShaderUpdate = false;

            for (int lod = baseLod + 2; lod < 4; lod++)
            {
                if (lod < 0 || lod >= 4) continue;

                // 恢复上次成功渲染时的范围
                ranges[lod] = s_LastRenderedRanges[lod];

                // 重算 shader 范围 (与 UpdateCascades 末尾逻辑一致)
                float4 r = s_LastRenderedRanges[lod];
                float2 invSize = 1f / math.max(0.001f, r.zw - r.xy);
                float2 negOff = r.xy * invSize;
                var shaderVec = new UnityEngine.Vector4(negOff.x, negOff.y, invSize.x, invSize.y);
                shaderRanges[lod] = shaderVec;

                // 清除更新标记 → RenderCascade 不会渲染此 LOD
                sliceUpdated[lod] = false;
                needsShaderUpdate = true;
            }

            // 重新设置 shader 全局变量
            if (needsShaderUpdate)
            {
                UnityEngine.Shader.SetGlobalVectorArray(s_ShaderPropertyID, shaderRanges);
            }
        }

        /// <summary>
        /// Postfix: 在更新帧（完整渲染后）保存当前范围快照
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix(TerrainSystem __instance)
        {
            // 仅在更新帧保存（节流帧的范围已被恢复，不能覆盖快照）
            if (s_IsThrottledFrame) return;
            if (PatchManager.CurrentCoreValue <= 1) return;
            if (Mod.Instance?.Settings?.TerrainCascadeThrottle != true) return;
            if (s_CascadeRangesField == null) return;

            var ranges = (float4[])s_CascadeRangesField.GetValue(__instance);
            if (ranges == null) return;

            for (int lod = 0; lod < 4; lod++)
            {
                s_LastRenderedRanges[lod] = ranges[lod];
            }
            s_HasSavedRanges = true;
        }
    }


    // === 优化 3.A: CullForCascades 建筑裁剪降频 ===
    // 当建筑实体未变化且无地形修改时，跳过 CullBuildingLotsJob 的全量裁剪
    // 复用上一帧缓存的 m_BuildingCullList，减少大地图下平移相机的 CPU 开销
    //
    // 原理: CullBuildingLotsJob 遍历全部建筑 Entity Chunk (大地图下数量 ×4)
    //       裁剪区域 = m_CascadeRanges[baseLod] (整个可玩区域)，不受相机位置影响
    //       当仅相机移动触发 heightMapRenderRequired 时，列表内容不变
    //       下游 CullBuildingsCascadeJob 按 per-LOD 区域做二次过滤，自适应新位置

    /// <summary>
    /// 辅助 Patch: 在 UpdateCascades 执行前捕获 m_UpdateArea 和 isLoaded 状态
    /// 这些状态在 UpdateCascades 内部会被消费/清零，必须在方法入口时读取
    /// </summary>
    [HarmonyPatch(typeof(TerrainSystem), "UpdateCascades")]
    internal static class TerrainSystem_UpdateCascades_TrackState
    {
        private const string Tag = "TerrainCullOpt";

        // 状态传递给 CullForCascades Prefix
        internal static bool s_NeedFullBuildingCull = true;

        // --- 缓存字段 ---
        private static FieldInfo s_UpdateAreaField;
        private static FieldInfo s_BuildingsChangedField;
        private static bool s_FieldsResolved = false;

        internal static FieldInfo BuildingsChangedField => s_BuildingsChangedField;

        [HarmonyPrefix]
        public static void Prefix(TerrainSystem __instance, bool isLoaded)
        {
            // 非大地图或用户关闭时默认需要完整裁剪
            s_NeedFullBuildingCull = true;

            if (PatchManager.CurrentCoreValue <= 1) return;
            if (Mod.Instance?.Settings?.TerrainCullThrottle != true) return;

            // --- 一次性解析反射字段 ---
            if (!s_FieldsResolved)
            {
                s_FieldsResolved = true;
                s_UpdateAreaField = AccessTools.Field(typeof(TerrainSystem), "m_UpdateArea");
                s_BuildingsChangedField = AccessTools.Field(typeof(TerrainSystem), "m_BuildingsChanged");

                if (s_UpdateAreaField == null || s_BuildingsChangedField == null)
                {
                    ModLog.Warn(Tag, "无法解析 m_UpdateArea 或 m_BuildingsChanged 字段");
                }
            }

            if (s_UpdateAreaField == null) return;

            // 检查是否有地形修改 (brush 操作等)
            bool hasUpdateArea = false;
            var updateAreaObj = s_UpdateAreaField.GetValue(__instance);
            if (updateAreaObj is float4 updateArea)
            {
                hasUpdateArea = math.lengthsq(updateArea) > 0f;
            }

            // 仅当：非加载帧 且 无地形修改 时，才可能跳过建筑裁剪
            s_NeedFullBuildingCull = isLoaded || hasUpdateArea;
        }
    }


    /// <summary>
    /// 主 Patch: 在 CullForCascades 入口拦截 heightMapRenderRequired 参数
    /// 当确认建筑实体未变化时，将其设 false → 跳过 CullBuildingLotsJob
    /// 注意: heightMapRenderRequired 是 CullForCascades 的值参数，修改不影响调用方
    ///       UpdateCascades 的局部变量（控制 CullCascade）不受影响
    /// </summary>
    [HarmonyPatch(typeof(TerrainSystem), "CullForCascades")]
    internal static class TerrainSystem_CullForCascades_Throttle
    {
        [HarmonyPrefix]
        public static void Prefix(TerrainSystem __instance, ref bool heightMapRenderRequired)
        {
            // 功能未启用或非大地图
            if (PatchManager.CurrentCoreValue <= 1) return;
            if (Mod.Instance?.Settings?.TerrainCullThrottle != true) return;

            // 有地形修改或加载帧 → 必须完整裁剪
            if (TerrainSystem_UpdateCascades_TrackState.s_NeedFullBuildingCull) return;

            // 检查建筑实体是否真的变化了
            var buildingsField = TerrainSystem_UpdateCascades_TrackState.BuildingsChangedField;
            if (buildingsField == null) return;

            var queryObj = buildingsField.GetValue(__instance);
            if (queryObj is EntityQuery entityQuery)
            {
                // 有建筑增删/修改 → 不跳过
                if (!entityQuery.IsEmptyIgnoreFilter) return;
            }

            // 无任何实体/地形变化，仅相机移动触发 → 跳过 CullBuildingLotsJob
            heightMapRenderRequired = false;
        }
    }
}
