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
    using UnityEngine;

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
                ModLog.Debug(Tag,
                    $"GetTerrainBounds_Transpiler applied {patches} patch(es).(Expected value: {newSize})");
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
                    ModLog.Debug(Tag,
                        $"Patching instruction {i} in GetHeightData: Replacing {baseSize} with {newSize}");
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

        /// <summary>
        /// [BUGFIX] 退出主菜单时重置会话状态。
        /// TerrainSystem 在 Cleanup 期间会释放 ManagedStructuredBuffers，
        /// 二次加载时必须重新执行扩容。
        /// </summary>
        internal static void ResetSessionState()
        {
            s_BufferExpanded = false;
            ModLog.Info(Tag, "TerrainPatch session state reset (s_BufferExpanded=false)");
        }

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
                    ("m_LaneInstanceData", 10000 * cv),
                    ("m_LaneRaisedInstanceData", 10000 * cv),
                    ("m_TriangleInstanceData", 1000 * cv),
                    ("m_EdgeInstanceData", 1000 * cv),
                    ("m_ClipMapBuffer", 10000 * cv),
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
        /// [BUGFIX] 退出主菜单时重置级联范围快照，避免二次加载使用旧数据。
        /// </summary>
        internal static void ResetSessionState()
        {
            s_HasSavedRanges = false;
            s_FrameCounter = 0;
            ModLog.Info(Tag, "CascadePatch session state reset");
        }

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

            if (s_CascadeRangesField == null || s_ShaderCascadeRangesField == null) return;

            int baseLod = TerrainSystem.baseLod;
            var ranges = (float4[])s_CascadeRangesField.GetValue(__instance);
            var shaderRanges = (UnityEngine.Vector4[])s_ShaderCascadeRangesField.GetValue(__instance);
            var sliceUpdated = __instance.heightMapSliceUpdated;
            if (ranges == null || shaderRanges == null || sliceUpdated == null) return;

            bool needsShaderUpdate = false;

            for (int lod = baseLod + 2; lod < 4; lod++)
            {
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


    // === 優化 2.2: Backdrop 高度圖降採樣事件化 ===
    //
    // 原版 TerrainSystem.OnUpdate（ModificationEnd 階段）無條件呼叫 DownSampleHeightMap()，
    // 該方法在 HasBackdrop（baseLod != 0）時對 cascade 做一次 compute dispatch，
    // 規模 (m_DownscaledHeightmap.width / 8)²（4096 圖 → 32×32 groups），
    // 把 cascade 降採樣 4 倍寫入 m_DownscaledHeightmap。
    //
    // 但 cascade 的內容只會在 RenderCascades() 真正繪製時改變，而 RenderCascades 僅在
    // heightMapRenderRequired 為 true 時才被 TerrainRenderSystem 呼叫（PreCulling 階段）。
    // 相機靜止、地形無變更時 cascade 逐幀不變，降採樣輸出必然與上一幀完全相同 → 純浪費。
    //
    // 門控設計（三個要點）：
    //
    // 1. 跨幀 latch，不讀 heightMapSliceUpdated。
    //    TerrainSystem 在 ModificationEnd、TerrainRenderSystem 在 PreCulling
    //    （SystemOrder.cs:303/641），故第 N 幀的降採樣讀到的是第 N-1 幀 RenderCascades
    //    的產物；而同一個 OnUpdate 內 UpdateCascades 已先把 heightMapSliceUpdated 改寫成
    //    「本幀稍後才要繪製」的狀態，直接讀它會錯一幀。
    //    另注意 heightMapSliceUpdatedLast 不可用作上一幀快照——原版 :4207 是
    //    `heightMapSliceUpdatedLast = heightMapSliceUpdated` 的 bool[] 引用賦值，兩者同物件。
    //    因此改由 RenderCascades 的 Postfix 置位 latch，降採樣消費並清除。
    //
    // 2. 不假設 shader 只讀哪個 slice。
    //    AdjustTerrain 是 Resources.Load<ComputeShader>（bundle 內，無 .compute 源碼），
    //    「只讀 slice 0」無法驗證。故 latch 條件是「本幀有繪製任何 cascade」，
    //    即使 kernel 實際讀 baseLod slice 也不會漏更新，代價是門控偏保守。
    //
    // 3. 只門控 OnUpdate 的呼叫點。
    //    另三個呼叫點（OnHeightsChanged、InitializeBackdrop、WaterSimulation.PostDeserialize）
    //    都是事件驅動的必要更新，一律放行。OnUpdate 內的順序是
    //    DownSampleHeightMap() → UpdateGPUReadback()（後者可能經 OnHeightsChanged 再次呼叫），
    //    故用「進入 OnUpdate 後的第一次呼叫」精確識別待門控的那一次。
    //
    // 載入強制放行：FinalizeTerrainData 在 :3208 呼叫 InitializeBackdrop（其內 :3269 降採樣一次），
    // 但 world map 要到 :3219 才 CopyTexture 進 slice 0——那次一次性降採樣拿不到 slice 0 內容，
    // 背景地形其實是靠每幀那次才正確初始化。因此載入後強制放行一段幀數。
    //
    // 生效範圍：僅對有 world backdrop 的存檔有意義（無 backdrop 時原方法本就是空操作）。
    [HarmonyPatch(typeof(TerrainSystem), nameof(TerrainSystem.DownSampleHeightMap))]
    internal static class TerrainSystem_DownSampleHeightMap_Gate
    {
        private const string Tag = "TerrainDownsample";

        /// <summary>載入後強制放行的幀數（涵蓋 slice 0 複製與初始收斂）。</summary>
        private const int kForcePassFrames = 16;

        /// <summary>cascade 自上次降採樣後是否被重繪過。初始為 true，確保首次必定執行。</summary>
        private static bool s_CascadeDirty = true;

        /// <summary>本幀是否已進入 OnUpdate 且尚未消費那次待門控的降採樣呼叫。</summary>
        private static bool s_PendingOnUpdateCall = false;

        /// <summary>載入後剩餘的強制放行幀數。</summary>
        private static int s_ForcePassFrames = kForcePassFrames;

        // --- 統計（僅 DEBUG 日誌用） ---
        private static int s_SkippedCount = 0;
        private static int s_ExecutedCount = 0;

        /// <summary>
        /// [BUGFIX] 退出主菜单时重置会话状态。
        /// 二次加载会重建 cascade 与 m_DownscaledHeightmap，dirty 记录必须作废并重新强制放行。
        /// </summary>
        internal static void ResetSessionState()
        {
            s_CascadeDirty = true;
            s_PendingOnUpdateCall = false;
            s_ForcePassFrames = kForcePassFrames;
            s_SkippedCount = 0;
            s_ExecutedCount = 0;
            ModLog.Info(Tag, "DownsampleGate session state reset");
        }

        /// <summary>由 RenderCascades 的 Postfix 呼叫：cascade 已被重繪，下次降採樣必須執行。</summary>
        internal static void MarkCascadeDirty() => s_CascadeDirty = true;

        /// <summary>由 FinalizeTerrainData 的 Postfix 呼叫：cascade 與降採樣圖剛重建。</summary>
        internal static void MarkTerrainReinitialized()
        {
            s_CascadeDirty = true;
            s_ForcePassFrames = kForcePassFrames;
        }

        /// <summary>由 OnUpdate 的 Prefix 呼叫：標記接下來的第一次降採樣屬於例行呼叫。</summary>
        internal static void MarkOnUpdateEntry() => s_PendingOnUpdateCall = true;

        /// <summary>
        /// 由 OnUpdate 的 Postfix 呼叫：清除未被消費的標記。
        /// 原版 OnUpdate 在 m_Heightmap == null 時整段跳過（TerrainSystem.cs:3481），
        /// 此時降採樣不會被呼叫，殘留標記會讓下一次事件驅動的呼叫被誤判為例行呼叫。
        /// </summary>
        internal static void ClearOnUpdateEntry() => s_PendingOnUpdateCall = false;

        [HarmonyPrefix]
        public static bool Prefix()
        {
            // 只門控 OnUpdate 的例行呼叫；事件驅動的呼叫一律放行
            bool isRoutineCall = s_PendingOnUpdateCall;
            s_PendingOnUpdateCall = false;

            if (!isRoutineCall)
            {
                s_CascadeDirty = false; // 事件路徑已刷新降採樣圖
                return true;
            }

            if (Mod.Instance?.Settings?.TerrainDownsampleThrottle != true)
                return true;

            // 載入後強制放行：slice 0 的 world map 複製發生在 InitializeBackdrop 之後
            if (s_ForcePassFrames > 0)
            {
                s_ForcePassFrames--;
                s_CascadeDirty = false;
                return true;
            }

            if (!s_CascadeDirty)
            {
                s_SkippedCount++;
                return false; // cascade 未變更 → 跳過 dispatch
            }

            s_CascadeDirty = false;
            s_ExecutedCount++;

#if DEBUG
            if ((s_ExecutedCount & 0xFF) == 0)
            {
                ModLog.Debug(Tag,
                    $"Backdrop 降採樣門控: 已跳過 {s_SkippedCount} 次 / 已執行 {s_ExecutedCount} 次");
            }
#endif
            return true;
        }
    }


    /// <summary>
    /// 輔助 Patch：標記進入 TerrainSystem.OnUpdate，讓門控能識別那次例行降採樣呼叫。
    /// 獨立成類以便與既有 <see cref="TerrainSystemPatches"/> 的 OnUpdate Postfix 分開註冊。
    /// </summary>
    [HarmonyPatch(typeof(TerrainSystem), "OnUpdate")]
    internal static class TerrainSystem_OnUpdate_DownsampleMarker
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            TerrainSystem_DownSampleHeightMap_Gate.MarkOnUpdateEntry();
        }

        /// <summary>
        /// 清除未被消費的標記。
        /// 原版 OnUpdate 在 <c>m_Heightmap == null</c> 時整段跳過（TerrainSystem.cs:3481），
        /// 此時降採樣不會被呼叫，標記若殘留會讓下一次事件驅動的呼叫被誤判為例行呼叫而遭門控。
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix()
        {
            TerrainSystem_DownSampleHeightMap_Gate.ClearOnUpdateEntry();
        }
    }


    /// <summary>
    /// 輔助 Patch：RenderCascades 執行後置位 dirty latch。
    /// 原版僅在 heightMapRenderRequired 為 true 時呼叫本方法
    /// （<c>TerrainRenderSystem.OnUpdate</c>），故 Postfix 抵達即代表 cascade 已被重繪。
    /// 與既有的 <see cref="TerrainSystem_RenderCascades_Patch"/>（降頻）並存，兩者互不干涉。
    /// </summary>
    [HarmonyPatch(typeof(TerrainSystem), nameof(TerrainSystem.RenderCascades))]
    internal static class TerrainSystem_RenderCascades_DirtyLatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            TerrainSystem_DownSampleHeightMap_Gate.MarkCascadeDirty();
        }
    }


    /// <summary>
    /// 輔助 Patch：地形資料重建（載入／匯入世界地圖）後強制放行降採樣。
    /// FinalizeTerrainData 會重建 cascade 與 m_DownscaledHeightmap，
    /// 且 slice 0 的 world map 複製發生在 InitializeBackdrop 的一次性降採樣之後。
    /// </summary>
    [HarmonyPatch(typeof(TerrainSystem), "FinalizeTerrainData")]
    internal static class TerrainSystem_FinalizeTerrainData_DownsampleReset
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            TerrainSystem_DownSampleHeightMap_Gate.MarkTerrainReinitialized();
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
    ///
    /// [FIX] 區域包含檢查：
    /// 原假設「裁剪區域恆為整個可玩區域」只在載入帧/CascadeReset 帧成立；
    /// 純相機移動帧的 area 是移動後的近距級聯區域（UpdateCascades L4180-4182），
    /// 而筆刷修改帧的裁剪區僅覆蓋筆刷範圍，會把 m_BuildingCullList 縮小成局部列表。
    /// 因此只有當本帧 area 完全包含於「上次實際執行裁剪的區域」內時才可安全跳過，
    /// 否則快取列表可能缺少新區域的建築，導致級聯紋理烘焙時丟失建築地基整平。
    /// </summary>
    [HarmonyPatch(typeof(TerrainSystem), "CullForCascades")]
    internal static class TerrainSystem_CullForCascades_Throttle
    {
        private const string Tag = "TerrainCullOpt";
        private const float kAreaEpsilon = 0.5f;

        // --- 上次實際執行 CullBuildingLotsJob 時的裁剪區域 ---
        private static float4 s_LastCulledArea;
        private static bool s_HasCulledArea = false;

        /// <summary>
        /// [BUGFIX] 退出主菜单时重置会话状态，二次加载不得复用旧裁剪区域。
        /// </summary>
        internal static void ResetSessionState()
        {
            s_HasCulledArea = false;
            ModLog.Info(Tag, "CullThrottle session state reset");
        }

        [HarmonyPrefix]
        public static void Prefix(TerrainSystem __instance, float4 area, ref bool heightMapRenderRequired)
        {
            // 本帧原本就不做建筑裁剪（仅道路/区域更新触发）
            if (!heightMapRenderRequired) return;

            // 功能未启用或非大地图 → 原版行为，但仍记录裁剪区域供之后启用时判断
            if (PatchManager.CurrentCoreValue <= 1 ||
                Mod.Instance?.Settings?.TerrainCullThrottle != true)
            {
                RecordCull(area);
                return;
            }

            // 有地形修改或加载帧 → 必须完整裁剪
            if (TerrainSystem_UpdateCascades_TrackState.s_NeedFullBuildingCull)
            {
                RecordCull(area);
                return;
            }

            // 检查建筑实体是否真的变化了
            var buildingsField = TerrainSystem_UpdateCascades_TrackState.BuildingsChangedField;
            if (buildingsField == null) return;

            var queryObj = buildingsField.GetValue(__instance);
            if (queryObj is EntityQuery entityQuery)
            {
                // 有建筑增删/修改 → 不跳过
                if (!entityQuery.IsEmptyIgnoreFilter)
                {
                    RecordCull(area);
                    return;
                }
            }

            // 仅相机移动触发：只有当本帧区域被上次实际裁剪区域完全覆盖时才可跳过
            if (s_HasCulledArea &&
                area.x >= s_LastCulledArea.x - kAreaEpsilon &&
                area.y >= s_LastCulledArea.y - kAreaEpsilon &&
                area.z <= s_LastCulledArea.z + kAreaEpsilon &&
                area.w <= s_LastCulledArea.w + kAreaEpsilon)
            {
                // 快取列表已覆盖新区域 → 跳过 CullBuildingLotsJob
                heightMapRenderRequired = false;
                return;
            }

            // 快取不覆盖（如筆刷修改後首次平移）→ 放行完整裁剪并更新记录
            RecordCull(area);
        }

        /// <summary>記錄本帧實際執行裁剪的區域（僅在 CullBuildingLotsJob 將被調度時呼叫）。</summary>
        private static void RecordCull(float4 area)
        {
            s_LastCulledArea = area;
            s_HasCulledArea = true;
        }
    }


    // === 优化 4: Backdrop 禁用 (方案 A: InitializeTerrainData 源头拦截) ===
    // 在 InitializeTerrainData 入口处将 worldMap 参数设为 null
    // 这样 SetWorldHeightmap(null) → DestroyWorldMap() → worldHeightmap = null
    // 然后 FinalizeTerrainData 中 worldHeightmap == null → baseLod = 0 → 无 Backdrop 路径
    // 性能收益: 每帧节省 ~0.5-2ms GPU + ~37MB VRAM + CPU 阻塞消除
    // 详见: docs/TerrainSystem/TerrainSystem_Analysis_Part2.md §9
    [HarmonyPatch(typeof(TerrainSystem), "InitializeTerrainData")]
    internal static class TerrainSystem_InitializeTerrainData_DisableBackdrop
    {
        private const string Tag = "TerrainBackdrop";

        [HarmonyPrefix]
        public static void Prefix(
            ref Texture2D worldMap,
            ref float2 inMapSize, ref float2 inWorldSize,
            ref float2 inMapCorner, ref float2 inWorldCorner)
        {
            // 仅在用户启用 Backdrop 禁用时生效
            if (Mod.Instance?.Settings?.DisableWorldBackdrop != true) return;

            if (worldMap != null)
            {
                // 销毁反序列化创建的 worldMap 纹理，防止 GPU 内存泄漏
                UnityEngine.Object.Destroy(worldMap);
                worldMap = null;

                // 同步 WorldSize = MapSize，确保 FinalizeTerrainData 中 baseLod = 0
                inWorldSize = inMapSize;
                inWorldCorner = inMapCorner;

                ModLog.Patch(Tag,
                    $"Backdrop disabled: worldMap nullified in InitializeTerrainData. " +
                    $"MapSize={inMapSize}, WorldSize forced to MapSize");
            }
        }
    }
}
