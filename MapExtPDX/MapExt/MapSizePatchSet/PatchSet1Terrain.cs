// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit MapExt2 and the author.

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    using Game;
    using Game.SceneFlow;
    using Game.Simulation;
    using HarmonyLib;
    using MapExtPDX.MapExt.Core;
    using MapExtPDX.SaveLoadSystem;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    // Target TerrainSystem class
    // 修补 TerrainSystemPatches 下 5 个方法
    [HarmonyPatch(typeof(TerrainSystem))]
    public static class TerrainSystemPatches
    {
        private const string Tag = nameof(TerrainSystemPatches);

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

            // 权威记录：只记游戏与编辑器每次加载的第一次调用，那次的 inMapSize 来自读档的 playableArea
            // 或 SetDefaults 的默认值；之后转换步骤与编辑器导入调 ReplaceHeightmap 等，都以默认 14336 调用，不算。
            // 回主菜单的 Cleanup 也经 SetDefaults 以 14336 调用一次，不记，否则每次回主菜单都多一行"原版地形被拉伸"。
            // 转换的否决闸也在这里判：此时还在反序列化阶段，地下水等系统的 OnGameLoaded 都还没跑
            GameManager gm = GameManager.instance;
            if (gm != null && gm.gameMode.IsGameOrEditor() &&
                MapSizeMismatchState.TryRecordTerrainSize(inMapSize.x, scalefactor))
            {
                ModLog.Info(Tag, $"本次加载的地形尺寸 {inMapSize.x}（{MapSizeMismatchState.DescribeMapSize(inMapSize.x)}），当前 CV={scalefactor}" +
                                 (MapSizeMismatchState.VanillaStretched ? "：原版尺寸的地形按当前模式拉伸" : ""));
                VanillaConversionState.VetoIfTerrainMismatch(inMapSize.x);
            }

            if (math.abs(inMapSize.x - baseSize) < 1f)
            {
                inMapSize *= scalefactor;
                inWorldSize *= scalefactor;
                inMapCorner = -0.5f * inMapSize;
                inWorldCorner = -0.5f * inWorldSize;

                patches++;
            }
            else
            {
                // === 地圖尺寸錯配偵測 ===
                // 走到這裡代表 inMapSize 不是原版 14336，唯一來源是
                // TerrainSystem.Deserialize 讀回的 playableArea——也就是說這張地圖／存檔
                // 是在某個擴展模式下製作的，其 playableArea 已被寫成 CV × 14336。
                //
                // 此時縮放不會發生（守衛不成立），但 GetHeightData／GetTerrainBounds
                // 的 Transpiler 仍按「當前模式」的常量換算取樣比例，
                // 兩者對不上就是靜默的高度錯位。故在此記錄，載入完成後提示。
                DetectAuthoredSizeMismatch(inMapSize.x, baseSize, scalefactor);
            }

            if (patches != 0)
            {
                ModLog.Patch(Tag,
                    $"FinalizeTerrainData Prefix applied {patches} patch(es). (Expected value: {inMapSize} , {inMapCorner} , {inWorldSize} , {inWorldCorner})");
            }
        } // FinalizeTerrainData method

        /// <summary>
        /// 由 <c>inMapSize</c> 反推地圖製作時的 CoreValue，與當前模式比對並記錄錯配。
        ///
        /// <para><b>只記錄不攔截</b>：此處位於反序列化中途，UI 尚未就緒，
        /// 且中止載入會讓引擎停在半初始化狀態。實際提示延到
        /// <c>OnGameLoadingComplete</c>，見 <see cref="MapSizeMismatchState"/>。</para>
        /// </summary>
        /// <param name="actualMapSize">地圖檔中的 playableArea.x。</param>
        /// <param name="baseSize">原版基準尺寸 14336。</param>
        /// <param name="currentCV">當前模式的 CoreValue。</param>
        private static void DetectAuthoredSizeMismatch(float actualMapSize, float baseSize, int currentCV)
        {
            // 反推製作模式：擴展模式存的 playableArea 必為 CV × 14336 的整數倍
            int authoredCV = (int)math.round(actualMapSize / baseSize);

            // 非整數倍 → 不是 MapExt 產出的地圖（可能是其他 Mod 或損壞資料），
            // 無從判斷應有模式，僅記錄供追查，不誤報成模式錯配。
            if (authoredCV < 1 || math.abs(actualMapSize - authoredCV * baseSize) > 1f)
            {
                ModLog.Warn(Tag,
                    $"检测到非预期的地图尺寸 {actualMapSize}（非 {baseSize} 的整数倍），" +
                    $"无法推断制作模式；当前 CV={currentCV}。地形高度可能不正确。");
                return;
            }

            if (authoredCV == currentCV)
            {
                // 同模式的地圖／存檔：playableArea 已是正確的目標尺寸，
                // 不需再縮放（重複縮放反而會放大到 CV² 倍），屬正常路徑。
                return;
            }

            // 真正的錯配：製作模式與當前模式不同，且兩邊都是有效的 MapExt 模式
            MapSizeMismatchState.Record(authoredCV, currentCV);

            ModLog.Warn(Tag,
                $"地图尺寸模式不符！地图以 {PatchManager.GetModeNameForCoreValue(authoredCV)} " +
                $"(CV={authoredCV}, playableArea={actualMapSize}) 制作，" +
                $"但当前模式为 {PatchManager.GetModeNameForCoreValue(currentCV)} (CV={currentCV})。" +
                $"地形高度采样比例将错位，加载完成后会显示提示。");
        }


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


        // === StructuredBuffer 首帧扩容 ===
        // 原版 OnCreate 中初始化的 ManagedStructuredBuffers 容量偏小
        // (AreaTriangle/AreaEdge 仅 1000)，大地图下动态扩容会引发运行时卡顿
        // 在首帧 OnUpdate 时通过反射预分配更大的容量

        private static bool s_BufferExpanded = false;

        /// <summary>
        /// 返回主菜单时重置会话状态。
        /// TerrainSystem 在 Cleanup 期间会释放 ManagedStructuredBuffers，
        /// 二次加载时必须重新执行扩容。
        /// 同時清除地圖尺寸錯配記錄，避免上一次載入的判定殘留到下一張地圖。
        /// </summary>
        internal static void ResetSessionState()
        {
            s_BufferExpanded = false;
            MapSizeMismatchState.Reset();
            ModLog.Info(Tag, "TerrainPatch session state reset (s_BufferExpanded=false, MapSizeMismatch cleared)");
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


        // === AsyncGPUReadback 失败降级策略 ===
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


    // === 远距级联降频更新 ===
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
        private const string Tag = nameof(TerrainSystem_RenderCascades_Patch);
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
        /// 返回主菜单时重置级联范围快照，避免二次加载使用旧数据。
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


    // === Backdrop 高度图降采样事件化 ===
    //
    // 原版 TerrainSystem.OnUpdate（ModificationEnd 阶段）无条件调用 DownSampleHeightMap()，
    // 该方法在 HasBackdrop（baseLod != 0）时对 cascade 做一次 compute dispatch，
    // 规模 (m_DownscaledHeightmap.width / 8)²（4096 图 4 倍降采样为 1024 → 128×128 groups），
    // 把 cascade 降采样 4 倍写入 m_DownscaledHeightmap。
    //
    // 但 cascade 的内容只会在 RenderCascades() 真正绘制时改变，而 RenderCascades 仅在
    // heightMapRenderRequired 为 true 时才被 TerrainRenderSystem 调用（PreCulling 阶段）。
    // 相机静止、地形无变更时 cascade 逐帧不变，降采样输出必然与上一帧完全相同 → 纯浪费。
    //
    // 门控设计（三个要点）：
    //
    // 1. 跨帧 latch，不读 heightMapSliceUpdated。
    //    TerrainSystem 在 ModificationEnd、TerrainRenderSystem 在 PreCulling
    //    （SystemOrder.Initialize），故第 N 帧的降采样读到的是第 N-1 帧 RenderCascades
    //    的产物；而同一个 OnUpdate 内 UpdateCascades 已先把 heightMapSliceUpdated 改写成
    //    “本帧稍后才要绘制”的状态，直接读它会错一帧。
    //    另注意 heightMapSliceUpdatedLast 不可用作上一帧快照——原版 TerrainSystem.UpdateCascades 是
    //    heightMapSliceUpdatedLast = heightMapSliceUpdated 的 bool[] 引用赋值，两者同对象。
    //    因此改由 RenderCascades 的 Postfix 置位 latch，降采样消费并清除。
    //
    // 2. 不假设 shader 只读哪个 slice。
    //    AdjustTerrain 是 Resources.Load<ComputeShader>（bundle 内，无 .compute 源码），
    //    “只读 slice 0”无法验证。故 latch 条件是“本帧有绘制任何 cascade”，
    //    即使 kernel 实际读 baseLod slice 也不会漏更新，代价是门控偏保守。
    //
    // 3. 只门控 OnUpdate 的调用点。
    //    另三个调用点（OnHeightsChanged、InitializeBackdrop、WaterSimulation.PostDeserialize）
    //    都是事件驱动的必要更新，一律放行。OnUpdate 内的顺序是
    //    DownSampleHeightMap() → UpdateGPUReadback()（后者可能经 OnHeightsChanged 再次调用），
    //    故用“进入 OnUpdate 后的第一次调用”精确识别待门控的那一次。
    //
    // 载入强制放行：FinalizeTerrainData 先调用 InitializeBackdrop（内部执行一次 DownSampleHeightMap），
    // 之后才将 world map CopyTexture 进 slice 0——那次一次性降采样拿不到 slice 0 内容，
    // 背景地形实际靠后续每帧调用才正确初始化。因此载入后强制放行一段帧数。
    //
    // 生效範圍：僅對有 world backdrop 的存檔有意義（無 backdrop 時原方法本就是空操作）。
    [HarmonyPatch(typeof(TerrainSystem), nameof(TerrainSystem.DownSampleHeightMap))]
    internal static class TerrainSystem_DownSampleHeightMap_Gate
    {
        private const string Tag = nameof(TerrainSystem_DownSampleHeightMap_Gate);

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
        /// 返回主菜单时重置会话状态。
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
        /// 原版 TerrainSystem.OnUpdate 在 m_Heightmap == null 时整段跳过，
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
                    $"Backdrop 降采样门控: 已跳过 {s_SkippedCount} 次 / 已执行 {s_ExecutedCount} 次");
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
        /// 原版 <c>TerrainSystem.OnUpdate</c> 在 <c>m_Heightmap == null</c> 时整段跳过，
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


    // === Backdrop 禁用（InitializeTerrainData 源头拦截）===
    // 在 InitializeTerrainData 入口处将 worldMap 参数设为 null
    // 这样 SetWorldHeightmap(null) → DestroyWorldMap() → worldHeightmap = null
    // 然后 FinalizeTerrainData 中 worldHeightmap == null → baseLod = 0 → 无 Backdrop 路径
    // 性能收益: 每帧节省 ~0.5-2ms GPU + ~37MB VRAM + CPU 阻塞消除
    [HarmonyPatch(typeof(TerrainSystem), "InitializeTerrainData")]
    internal static class TerrainSystem_InitializeTerrainData_DisableBackdrop
    {
        private const string Tag = nameof(TerrainSystem_InitializeTerrainData_DisableBackdrop);

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
