// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

// VanillaSaveConversionSystem.cs
// 原版存档转换后处理系统：在目标模式下加载原版存档或用原版地图开新城后，执行一次性数据修复。

using Colossal.IO.AssetDatabase;
using Colossal.Mathematics;
using Game;
using Game.Areas;
using Game.Common;
using Game.Objects;
using Game.PSI;
using Game.SceneFlow;
using Game.Simulation;
using Game.Tools;
using Game.UI;
using Game.UI.Localization;
using Game.UI.Menu;
using HarmonyLib;
using MapExtPDX.CellShared;
using MapExtPDX.MapExt.Core;
using MapExtPDX.SaveLoadSystem;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>
    /// 原版存档转换、原版地图开新城转换与 28→57 存档扩展的后处理系统，按转换请求（<see cref="VanillaConversionState"/>）分阶段执行：
    /// <c>OnGameLoaded</c> 做转换（MapTile、清理、地形、自然资源、地下水、水体；开新城跳过两步清理；
    /// 28→57 先核对路由读入的 5 张贴图，自然资源与地下水不再从原版实例嵌入）；
    /// <c>OnGameLoadingComplete</c> 把自动存档排进原版存读档队列；存档确认写入后才弹完成提示。
    /// <para>必需步骤出错一律抛出、走失败路径（v4.12.0 整合计划 Q19），只有水体预模拟是加速项，出错只记日志。</para>
    ///
    /// <para><b>为什么不用 OnUpdate</b>：本系统注册在 <c>LoadSimulation</c>，这个 phase 只在加载期间由
    /// <c>SimulationSystem</c> 补帧时更新，加载结束后 <c>OnUpdate</c> 不再跑（v4.12.0 整合计划 §1.5）。</para>
    /// </summary>
    public partial class VanillaSaveConversionSystem : GameSystemBase
    {
        private const string Tag = nameof(VanillaSaveConversionSystem);

        #region Constants and Fields

        // === 原版地形常量 ===
        private const int kVanillaHeightmapWidth = 4096;
        private const int kVanillaMapSize = 14336;
        private const int kVanillaWorldSize = 57344; // kDefaultMapSize × 4

        /// <summary>28→57 的外围混合宽度（像素）：目标地形 14 m/px，约 448 m（28→57 计划 §3.3）。</summary>
        private const int kExpansionBlendWidth = 32;

        // === 自动存档 ===
        /// <summary>存档预览图尺寸，与原版存档面板相同。</summary>
        private const int kPreviewWidth = 680;
        private const int kPreviewHeight = 383;

        /// <summary>请求结束后再压几帧速度：确保排在 SimulationSystem 加载结束后按设定定速的那一次之后。</summary>
        private const int kPauseSettleFrames = 3;

        #endregion

        protected override void OnCreate()
        {
            base.OnCreate();
            Enabled = true;
        }

        protected override void OnUpdate()
        {
            // 本系统仅在 OnGameLoaded 回调中执行，OnUpdate 不做任何事
        }

        protected override void OnGameLoaded(Colossal.Serialization.Entities.Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);

            // 反序列化已经结束：撤掉 28→57 的类型路由，下一次存读档重建出不带路由的映射表（v4.12.0 整合计划 §1.8）。
            // 被否决的请求也要撤，所以放在判断请求之前
            SaveExpansionTypeMapPatch.EndLoad("读档结束");

            // 只认闸门放行过的请求，且一个请求只执行一次：放行时推进到"加载中"，这里推进到"转换中"（P0-2、P0-3）。
            // 地形实际尺寸不是源尺寸的请求，已在反序列化阶段转为"已取消"，不执行任何步骤、不压暂停
            ConversionRequest request = VanillaConversionState.Current;
            if (request == null || request.Phase != ConversionPhase.Loading)
                return;

            request.Phase = ConversionPhase.Converting;
            bool newGame = request.IsNewGame;
            bool expansion = request.IsSaveExpansion;
            ModLog.Ok(Tag, newGame ? "=== 开始原版地图开新城转换 ===" : expansion ? "=== 开始 28→57 存档扩展 ===" : "=== 开始原版存档转换 ===");
            ModLog.Info(Tag, $"转换请求 {request}，purpose={serializationContext.purpose}");

            // ⓪ 转换与自动存档期间保持暂停，失败后也停在暂停（v4.12.0 整合计划 Q6）
            StartPauseHold(request);

            try
            {
                // 步骤名用英文标识：失败时原样写进三语提示与日志

                if (expansion)
                {
                    // ⓪ 28→57：ModeB 的 5 张贴图须已路由并中心嵌入，放在第一步，早于任何清理（整合计划 §1.8）
                    RunStep(request, "Receive", ExecuteExpansionReceiveCheck);
                }

                // ⓪a MapTile 边界重建：存档 529 格全部解锁；开新城只解锁中央 9 格（D2）
                RunStep(request, "MapTiles", () => ExecuteMapTileRebuild(unlockAll: !newGame));

                if (newGame)
                {
                    // 新城没有车辆与居民；外部连接保留在原处（D1）：MapExt 没有重新生成外部连接的路径，
                    // 玩家在游戏内也建不了，删掉就没有移民与进出口
                    ModLog.Info(Tag, "开新城：跳过车辆与居民清理、外部连接清理（D1）");
                }
                else
                {
                    // ⓪b 先清除所有车辆和居民（消除所有活跃导航引用）
                    // 必须在 OC 删除之前执行，否则车辆导航旧路径会触发 NRE
                    RunStep(request, "VehicleCleanup", ExecuteVehicleCleanup);

                    // ⓪c OutsideConnection 节点清理（标记为 Deleted）
                    // 包括飞机 OC（无法在游戏内手动删除）
                    // 车辆已全部清除，级联删除安全
                    RunStep(request, "OutsideConnections", ExecuteOutsideConnectionCleanup);
                }

                // ① Heightmap 合成 (含平面底图回退)
                RunStep(request, "Heightmap", () => ExecuteHeightmapSynthesis(request));

                if (expansion)
                {
                    // ②③ 28→57：自然资源与地下水已经路由读入、中心嵌入（⓪ 已核对），再从原版实例嵌入会把它们覆盖掉
                    // （整合计划 §1.8）；改为清空原版地下水实例，防新档重启后被拉伸回填（28→57 计划 §3.4）
                    RunStep(request, "VanillaGroundWater", ExecuteVanillaGroundWaterClear);
                }
                else
                {
                    // ② NaturalResource 中心嵌入
                    RunStep(request, "NaturalResources", ExecuteNaturalResourceRegen);

                    // ③ GroundWater 中心嵌入
                    RunStep(request, "GroundWater", ExecuteGroundWaterInit);
                }

                // ③b 水源升级 + 海平面重置
                RunStep(request, "WaterSources", () => ExecuteWaterSimReset(request));

                // ③c 预模拟水体填充（河流/湖泊注水加速）
                RunStep(request, "WaterPreSimulation", ExecuteWaterPreSimulation);

                // ④ 自动存档等加载结束后在 OnGameLoadingComplete 里排队
                request.Phase = ConversionPhase.Saving;
                request.CurrentStep = "AutoSave";
                ModLog.Ok(Tag, expansion
                    ? "=== 28→57 存档扩展完成，加载结束后自动存档 ==="
                    : $"=== 原版{(newGame ? "地图开新城" : "存档")}转换完成，加载结束后自动存档 ===");
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, $"转换在「{request.CurrentStep}」一步失败");
                Fail(request, request.CurrentStep, ex.Message);
            }
        }

        /// <summary>
        /// 加载画面结束后：转换成功的把自动存档排进原版存读档队列（排在当前加载之后，与玩家的存读档串行）；
        /// 转换失败的弹出失败提示，被否决的弹出否决说明。读档中途界面还没就绪，都放在这里。
        ///
        /// <para>三条路最后都把菜单的"上次存档名"改指新档名（v4.12.0 整合计划 1.8）：读档任务在加载结束后
        /// 才把它设成源档名，所以改动也要排进同一个队列，排在读档任务之后。被否决的城市虽然没动，
        /// 按 F5 也只存成新档，不写回源档（§7 第 2 条）；被否决的开新城没有源档，不改。</para>
        /// </summary>
        protected override void OnGameLoadingComplete(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (mode != GameMode.Game) return;

            ConversionRequest request = VanillaConversionState.Current;
            if (request == null) return;

            if (request.Phase == ConversionPhase.Saving && !request.SaveQueued)
            {
                request.SaveQueued = true;
                // 第三个参数是同名任务上限（cap），不是优先级：原版菜单传 1，同名任务在跑就丢弃新任务。
                // 这里正处在读档任务里，传 1 会被静默丢掉；不设上限才会排在读档任务之后执行
                Colossal.TaskManager.instance.EnqueueTask(SaveHelpers.kSaveLoadTaskName, () => SaveConvertedAsync(request));
            }
            else if (request.Phase == ConversionPhase.Failed && !request.FailureReported)
            {
                request.FailureReported = true;
                ShowFailureDialog(request);
                Colossal.TaskManager.instance.EnqueueTask(SaveHelpers.kSaveLoadTaskName, () => RedirectToNewSaveName(request));
            }
            else if (request.Phase == ConversionPhase.Cancelled && !request.VetoReported)
            {
                request.VetoReported = true;
                ShowVetoDialog(request);
                // 开新城没有源档要护，城市也没动：照原版开新城，F5 按城市名存
                if (!request.IsNewGame)
                    Colossal.TaskManager.instance.EnqueueTask(SaveHelpers.kSaveLoadTaskName, () => RedirectToNewSaveName(request));
            }
        }

        private static void RunStep(ConversionRequest request, string step, Action action)
        {
            request.CurrentStep = step;
#if DEBUG
            // 故障注入（DEBUG 自检）：FaultInject.txt 写 SaveGate/Convert/<步骤名>，让该步骤失败，验证失败提示与暂停
            if (MapExtHealth.IsFaultInjected("SaveGate/Convert/" + step))
                throw new InvalidOperationException("Fault injected (DEBUG self-test)");
#endif
            action();
        }

        private static void Fail(ConversionRequest request, string step, string detail)
        {
            request.FailedStep = step;
            request.FailureDetail = detail;
            request.Phase = ConversionPhase.Failed;
            ModLog.Error(Tag, $"转换失败（步骤：{step}）：{detail}。源存档未被修改，游戏保持暂停");
        }

        /// <summary>
        /// 转换与自动存档期间把模拟速度压在 0，不改玩家的"加载后暂停"设定。
        /// 加载期间 <c>selectedSpeed</c> 的写入会被忽略，加载结束后 <c>SimulationSystem</c> 才按设定定一次速度，
        /// 所以逐帧压，直到请求结束后再压 <see cref="kPauseSettleFrames"/> 帧。
        /// 原先改 <c>pausedAfterLoading</c>、又在 <c>OnGameLoaded</c> 里就改回去，那时 SimulationSystem 还没读，暂停实际不生效。
        /// </summary>
        private void StartPauseHold(ConversionRequest request)
        {
            SimulationSystem simulation = World.GetExistingSystemManaged<SimulationSystem>();
            if (simulation == null)
            {
                ModLog.Warn(Tag, "SimulationSystem 不存在，转换期间无法保持暂停");
                return;
            }

            int settledFrames = 0;
            Colossal.Core.MainThreadDispatcher.RegisterUpdater(() =>
            {
                GameManager gm = GameManager.instance;
                // 请求已清除（回主菜单、新的读档）或已离开游戏：停止
                if (gm == null || VanillaConversionState.Current != request || gm.gameMode != GameMode.Game) return true;
                if (gm.isGameLoading) return false;

                simulation.selectedSpeed = 0f;
                bool finished = request.Phase != ConversionPhase.Converting && request.Phase != ConversionPhase.Saving;
                return finished && ++settledFrames >= kPauseSettleFrames;
            });
        }

        #region ⓪ 28→57 接收核对

        /// <summary>
        /// 28→57 的接收核对（v4.12.0 整合计划 3.1、§1.8）：ModeB 的 5 张贴图须已经类型路由读进 ModeA 系统并中心嵌入。
        /// 读的是基类读档 Job 写下的结果，与各系统 OnGameLoaded 的先后无关；没收到（存档缺区段、路由没生效）
        /// 或被拒（源边长不对）都抛出，转换在任何清理之前失败。异常消息用英文，原样写进三语的失败提示。
        /// </summary>
        private void ExecuteExpansionReceiveCheck()
        {
            var problems = new List<string>();
            foreach (Type target in SaveExpansionTypeMapPatch.TargetTypes)
            {
                ExpansionEmbedResult result = World.GetExistingSystemManaged(target) is ISaveExpansionTarget receiver
                    ? receiver.ExpansionEmbed
                    : ExpansionEmbedResult.None;
                if (result != ExpansionEmbedResult.Done) problems.Add($"{target.Name} {result}");
            }

            if (problems.Count > 0)
                throw new InvalidOperationException("ModeB data was not received: " + string.Join(", ", problems));

            ModLog.Ok(Tag, $"28→57 接收核对通过：{SaveExpansionTypeMapPatch.TargetTypes.Count} 张贴图都已中心嵌入");
        }

        #endregion

        #region ① Heightmap 合成

        /// <summary>
        /// Heightmap 合成：worldHeightmap 作为底图 + 源 detail heightmap 降采样覆盖中心区域。
        /// <list type="bullet">
        /// <item>原版来源的两条分支照旧：目标 ModeA 嵌 1024²，目标 ModeB 裁背景中央并嵌 2048²，混合写在嵌入区内侧；</item>
        /// <item>28→57 按源／目标比值嵌入，背景按实际世界范围裁出新地图范围，校准与混合只改外围
        /// （<see cref="SynthesizeExpansion"/>，v4.12.0 整合计划 3.5）。</item>
        /// </list>
        /// <para>取不到地形、detail 形状不对或回读失败都抛出、走失败路径（整合计划 Q19）：原先直接 return，
        /// 转换照样报成功，存下的城市地形没有合成，或者只剩背景。异常消息用英文，原样写进三语的失败提示。</para>
        /// </summary>
        private void ExecuteHeightmapSynthesis(ConversionRequest request)
        {
            ModLog.Info(Tag, "① 开始 Heightmap 合成...");

            var terrainSystem = World.GetExistingSystemManaged<TerrainSystem>()
                ?? throw new InvalidOperationException("TerrainSystem not found");

            int cv = PatchManager.CurrentCoreValue;
            int hmWidth = kVanillaHeightmapWidth; // 4096

            // --- 获取 worldHeightmap 和 m_Heightmap ---
            var worldHM = terrainSystem.worldHeightmap;
            var detailHM = AccessTools.Field(typeof(TerrainSystem), "m_Heightmap")?.GetValue(terrainSystem) as RenderTexture
                ?? throw new InvalidOperationException("TerrainSystem.m_Heightmap is not available");
            if (detailHM.width != hmWidth || detailHM.height != hmWidth)
                throw new InvalidOperationException(
                    $"Unsupported heightmap size {detailHM.width}x{detailHM.height}, expected {hmWidth}x{hmWidth}");

            ModLog.Info(Tag, $"worldHeightmap: {(worldHM != null ? $"{worldHM.width}x{worldHM.height}" : "FLAT")}, " +
                $"m_Heightmap: {detailHM.width}x{detailHM.height}");

            NativeArray<ushort> worldPixels = default;
            NativeArray<ushort> detailPixels = default;
            NativeArray<ushort> synthesized = default;
            Texture2D synthTex = null;
            try
            {
                // --- GPU 回读 detail 高度图 ---
                detailPixels = ReadbackToR16(detailHM, "m_Heightmap");

                // --- 创建合成纹理 ---
                synthesized = new NativeArray<ushort>(hmWidth * hmWidth, Allocator.Persistent);

                // --- 原版来源：底图 = worldHeightmap（全量复制），缺失时生成平面底图 ---
                bool flatFallback = worldHM == null;
                if (!request.IsSaveExpansion)
                {
                    if (flatFallback)
                    {
                        ModLog.Info(Tag, "worldHeightmap 为 null，生成默认平面底图");
                        ushort seaLevelR16 = FlatBaseR16(terrainSystem);
                        worldPixels = new NativeArray<ushort>(hmWidth * hmWidth, Allocator.Persistent);
                        for (int i = 0; i < worldPixels.Length; i++)
                            worldPixels[i] = seaLevelR16;
                    }
                    else
                    {
                        worldPixels = ReadbackToR16(worldHM, "worldHeightmap");
                    }

                    NativeArray<ushort>.Copy(worldPixels, synthesized);
                }

                if (request.IsSaveExpansion) // 28→57
                {
                    request.FlatPeriphery = !SynthesizeExpansion(terrainSystem, worldHM, detailPixels, synthesized, hmWidth,
                        request.SourceTerrainMapSize, cv * (double)kVanillaMapSize);
                }
                else if (cv == 4) // ModeA: 57km
                {
                    // 原版 heightmap 覆盖中心 1/4 区域
                    // UV: [0.375, 0.625] → pixel [1536, 2560] → 1024px
                    int embedSize = hmWidth / 4; // 1024
                    int embedStart = (hmWidth - embedSize) / 2; // 1536

                    ModLog.Info(Tag, $"ModeA 合成: 降采样 {hmWidth}² → {embedSize}², 嵌入 [{embedStart}:{embedStart + embedSize}]");

                    // 降采样 detail (4096²) → 1024² 并覆盖到中心
                    BoxFilterDownsample(detailPixels, hmWidth, synthesized, hmWidth, embedStart, embedSize);

                    // --- 实测边界高度偏差并校准 world 底图 ---
                    int offsetR16 = MeasureBoundaryOffset(synthesized, hmWidth, embedStart, embedSize);
                    if (math.abs(offsetR16) > 10) // 超过 ~0.6m 才校准
                    {
                        float2 hso = terrainSystem.heightScaleOffset;
                        float offsetMeters = offsetR16 / 65535f * hso.x;
                        ModLog.Info(Tag, $"实测边界偏差: {offsetR16} R16 ≈ {offsetMeters:F1}m, 校准 world 底图");
                        ApplyOffsetOutsideEmbed(synthesized, hmWidth, embedStart, embedSize, offsetR16);
                        // 同步更新 worldPixels 供 edge blend 使用
                        for (int i = 0; i < worldPixels.Length; i++)
                            worldPixels[i] = (ushort)math.clamp(worldPixels[i] + offsetR16, 0, 65535);
                    }

                    // 边缘混合：消除 detail ↔ world 拼接边界的残余局部差异
                    int blendWidth = 32; // ~450m (32px × 14m/px)
                    ApplyEdgeBlend(synthesized, hmWidth, embedStart, embedSize, worldPixels, blendWidth);
                    ModLog.Info(Tag, $"ModeA 边缘混合: blendWidth={blendWidth}px (~{blendWidth * 14}m)");
                }
                else if (cv == 2) // ModeB: 28km
                {
                    // === ModeB 合成策略 ===
                    // worldMap (4096px = 57344m) 中心 50% ([1024:3072] = 28672m)
                    // 上采样到 4096px，作为底图覆盖 28672m。
                    // detail heightmap (4096px = 14336m) 降采样到 2048px，嵌入中心 [1024:3072]。
                    //
                    // 关键：边缘混合必须使用上采样后的 world 数据作为参考，
                    // 而不是原始 worldPixels（57344m 坐标系），
                    // 否则 worldPixels[idx] 和 synthesized[idx] 指向不同的世界位置。

                    int cropStart = hmWidth / 4; // 1024
                    int cropSize = hmWidth / 2; // 2048

                    // 先将 worldHeightmap 中心裁剪区域上采样到全图作为底图
                    if (!flatFallback)
                        BilinearUpsampleRegion(worldPixels, hmWidth, cropStart, cropSize, synthesized, hmWidth);

                    // 保存上采样后的 world 底图数据，用于后续边缘混合参考
                    // （此时 synthesized 和 upsampledWorld 共享 28672m 坐标系）
                    var upsampledWorld = new NativeArray<ushort>(hmWidth * hmWidth, Allocator.Persistent);
                    NativeArray<ushort>.Copy(synthesized, upsampledWorld);

                    // detail heightmap (4096²) 降采样到 2048²，嵌入中心 [1024:3072]
                    int embedSize = hmWidth / 2; // 2048
                    int embedStart = hmWidth / 4; // 1024

                    ModLog.Info(Tag, $"ModeB 合成: 降采样 {hmWidth}² → {embedSize}², " +
                        $"嵌入 [{embedStart}:{embedStart + embedSize}]");

                    BoxFilterDownsample(detailPixels, hmWidth, synthesized, hmWidth, embedStart, embedSize);

                    // --- 实测边界高度偏差并校准 world 底图 ---
                    int offsetR16B = MeasureBoundaryOffset(synthesized, hmWidth, embedStart, embedSize);
                    if (math.abs(offsetR16B) > 10)
                    {
                        float2 hsoB = terrainSystem.heightScaleOffset;
                        float offsetMetersB = offsetR16B / 65535f * hsoB.x;
                        ModLog.Info(Tag, $"实测边界偏差: {offsetR16B} R16 ≈ {offsetMetersB:F1}m, 校准 world 底图");
                        ApplyOffsetOutsideEmbed(synthesized, hmWidth, embedStart, embedSize, offsetR16B);
                        // 同步更新 upsampledWorld 供 edge blend 使用
                        for (int i = 0; i < upsampledWorld.Length; i++)
                            upsampledWorld[i] = (ushort)math.clamp(upsampledWorld[i] + offsetR16B, 0, 65535);
                    }

                    // 边缘混合：使用 upsampledWorld（28672m 坐标系）作为参考
                    int blendWidthB = 48; // ~336m (48px × 7m/px at 28672/4096)
                    ApplyEdgeBlend(synthesized, hmWidth, embedStart, embedSize, upsampledWorld, blendWidthB);
                    ModLog.Info(Tag, $"ModeB 边缘混合: blendWidth={blendWidthB}px (~{blendWidthB * 7}m)");

                    upsampledWorld.Dispose();
                }

                // --- 上传合成纹理到 TerrainSystem ---
                synthTex = new Texture2D(hmWidth, hmWidth, GraphicsFormat.R16_UNorm,
                    TextureCreationFlags.DontInitializePixels | TextureCreationFlags.DontUploadUponCreate);
                synthTex.name = "VanillaConvert_SynthesizedHeightmap";
                synthTex.SetPixelData(synthesized, 0);
                synthTex.Apply(false, false);

                // --- 销毁旧 worldHeightmap → cascade 退化为单层 (baseLod=0) ---
                // 转换后可玩区已覆盖原始 worldHeightmap 的全部范围，不再需要独立的世界背景层。
                // 如果不销毁，ReplaceHeightmap 内 FinalizeTerrainData 会检测到 worldHeightmap != null，
                // 设置 baseLod=1 保持双层 cascade，导致在原版 14km 边界处出现层间切换落差。
                var destroyWorldMap = AccessTools.Method(typeof(TerrainSystem), "DestroyWorldMap");
                if (destroyWorldMap != null)
                {
                    destroyWorldMap.Invoke(terrainSystem, null);
                    ModLog.Info(Tag, "已销毁旧 worldHeightmap → cascade 退化为单层模式");
                }
                else
                {
                    // 降级：直接设置属性为 null
                    terrainSystem.worldHeightmap = null;
                    ModLog.Warn(Tag, "DestroyWorldMap 未找到，直接清空 worldHeightmap");
                }

                // 使用 ReplaceHeightmap 替换地形并触发 TerrainWillChange
                // 此时 worldHeightmap == null → baseLod=0 → 无 cascade 边界
                terrainSystem.ReplaceHeightmap(synthTex);
                ModLog.Ok(Tag, "Heightmap 合成完成，已调用 ReplaceHeightmap (baseLod=0, 无 cascade)");
            }
            finally
            {
                // 失败路径也要释放：原先合成纹理与数组只在成功路径清理
                if (synthTex != null) UnityEngine.Object.Destroy(synthTex);
                if (synthesized.IsCreated) synthesized.Dispose();
                if (worldPixels.IsCreated) worldPixels.Dispose();
                if (detailPixels.IsCreated) detailPixels.Dispose();
            }
        }

        /// <summary>
        /// 28→57 的地形合成（28→57 计划 §3.3）：
        /// <list type="number">
        /// <item>底图：背景按 TerrainSystem 记录的实际世界范围裁出新地图范围，双线性上采样；
        /// 没有背景、或背景盖不住新地图时用平底（14→28 转出的城市没有背景）；</item>
        /// <item>源 detail（覆盖源可玩区）按源／目标比值降采样，放在中央，世界位置不变；</item>
        /// <item>边界校准只平移外围，边缘混合写在外围：原 28 km 城市的地形不改写。</item>
        /// </list>
        /// </summary>
        /// <returns>外围是否取自背景地形；false 为平底，完成提示据此写一句说明。</returns>
        private bool SynthesizeExpansion(TerrainSystem terrainSystem, Texture worldHM, NativeArray<ushort> detailPixels,
            NativeArray<ushort> synthesized, int hmWidth, double sourceMap, double targetMap)
        {
            // 否决闸已保证地形是源尺寸，这里再核对一次：比值不对会把城市放错位置
            float playable = terrainSystem.playableArea.x;
            if (math.abs(playable - sourceMap) > 1f)
                throw new InvalidOperationException($"Terrain playable area {playable} does not match the source map size {sourceMap}");

            int embedSize = SaveExpansionGeometry.EmbedSize(hmWidth, sourceMap, targetMap);
            int embedStart = SaveExpansionGeometry.EmbedStart(hmWidth, embedSize);

            // --- 底图 ---
            float2 cropStart = default, cropSize = default;
            bool backdrop = worldHM != null && worldHM.width == worldHM.height &&
                TryGetBackdropWindow(terrainSystem, worldHM.width, targetMap, out cropStart, out cropSize);
            if (backdrop)
            {
                NativeArray<ushort> worldPixels = ReadbackToR16(worldHM, "worldHeightmap");
                try
                {
                    BilinearUpsampleRegion(worldPixels, worldHM.width, cropStart, cropSize, synthesized, hmWidth);
                }
                finally
                {
                    worldPixels.Dispose();
                }

                ModLog.Info(Tag, $"28→57 底图：背景 {worldHM.width}²（世界 {terrainSystem.worldSize.x} m）裁取 " +
                    $"[{cropStart.x:F1}:{cropStart.x + cropSize.x:F1}) 上采样为 {hmWidth}²");
            }
            else
            {
                ushort flat = FlatBaseR16(terrainSystem);
                for (int i = 0; i < synthesized.Length; i++)
                    synthesized[i] = flat;

                ModLog.Info(Tag, worldHM == null
                    ? "28→57 底图：存档没有背景地形，外围用平底"
                    : $"28→57 底图：背景 {worldHM.width}x{worldHM.height}（世界 {terrainSystem.worldSize.x} m）盖不住新地图，外围用平底");
            }

            // --- 源 detail 降采样，放在中央 ---
            ModLog.Info(Tag, $"28→57 合成: 源 {sourceMap} m → 目标 {targetMap} m，降采样 {hmWidth}² → {embedSize}², " +
                $"嵌入 [{embedStart}:{embedStart + embedSize}]");
            BoxFilterDownsample(detailPixels, hmWidth, synthesized, hmWidth, embedStart, embedSize);

            // --- 边界校准：只平移外围 ---
            int offsetR16 = MeasureBoundaryOffset(synthesized, hmWidth, embedStart, embedSize);
            if (math.abs(offsetR16) > 10) // 超过 ~0.6m 才校准
            {
                float offsetMeters = offsetR16 / 65535f * terrainSystem.heightScaleOffset.x;
                ModLog.Info(Tag, $"实测边界偏差: {offsetR16} R16 ≈ {offsetMeters:F1}m, 校准外围底图");
                ApplyOffsetOutsideEmbed(synthesized, hmWidth, embedStart, embedSize, offsetR16);
            }

            // --- 外围混合：从原边界的 detail 值过渡到底图，嵌入区不动 ---
            ApplyOuterEdgeBlend(synthesized, hmWidth, embedStart, embedSize, kExpansionBlendWidth);
            ModLog.Info(Tag, $"28→57 外围混合: blendWidth={kExpansionBlendWidth}px (~{kExpansionBlendWidth * targetMap / hmWidth:F0}m)，原区域不改写");

            return backdrop;
        }

        /// <summary>
        /// 新地图范围在背景贴图上的采样窗口（像素，可为小数）。按 TerrainSystem 记录的实际世界范围换算，
        /// 不假设背景是可玩区的 4 倍（28→57 计划 §3.3）。窗口超出背景时返回 false：背景盖不住新地图，按没有背景处理。
        /// </summary>
        private static bool TryGetBackdropWindow(TerrainSystem terrainSystem, int backdropWidth, double targetMap,
            out float2 start, out float2 size)
        {
            float2 worldSize = terrainSystem.worldSize;
            float2 worldCorner = terrainSystem.worldOffset; // 世界范围的左下角，与 FinalizeTerrainData 的 inWorldCorner 相同
            double half = 0.5 * targetMap;

            double x0 = SaveExpansionGeometry.WorldToPixel(-half, worldCorner.x, worldSize.x, backdropWidth);
            double x1 = SaveExpansionGeometry.WorldToPixel(half, worldCorner.x, worldSize.x, backdropWidth);
            double y0 = SaveExpansionGeometry.WorldToPixel(-half, worldCorner.y, worldSize.y, backdropWidth);
            double y1 = SaveExpansionGeometry.WorldToPixel(half, worldCorner.y, worldSize.y, backdropWidth);
            start = new float2((float)x0, (float)y0);
            size = new float2((float)(x1 - x0), (float)(y1 - y0));

            const double tolerance = 0.5; // 半个像素
            return worldSize.x > 0f && worldSize.y > 0f &&
                   x0 >= -tolerance && y0 >= -tolerance &&
                   x1 <= backdropWidth + tolerance && y1 <= backdropWidth + tolerance;
        }

        /// <summary>平底的 R16 值：WaterSystem 的实际海平面（世界坐标 m）按地形的高度缩放换算。</summary>
        private ushort FlatBaseR16(TerrainSystem terrainSystem)
        {
            // 从 WaterSystem 获取实际海平面高度（世界坐标 m）
            var waterSystem = World.GetExistingSystemManaged<WaterSystem>();
            float seaLevelWorld = waterSystem?.SeaLevel ?? 511.7f; // kDefaultSeaLevel

            // 从 TerrainSystem 获取 heightScaleOffset
            float2 hso = terrainSystem.heightScaleOffset; // .x=scale, .y=offset

            // 世界坐标 → R16 像素值
            ushort seaLevelR16 = (ushort)math.clamp(
                (seaLevelWorld - hso.y) / math.max(hso.x, 1f) * 65535f, 0, 65535);

            ModLog.Info(Tag, $"海平面: {seaLevelWorld}m → R16={seaLevelR16} (scale={hso.x}, offset={hso.y})");
            return seaLevelR16;
        }

        // === GPU 回读工具方法 ===

        /// <summary>
        /// 从 GPU 回读 R16 高度数据（Texture2D 与 RenderTexture 通用）。
        /// 失败抛出（整合计划 Q19）：原先记一行错误后返回空数组，调用方跳过嵌入，存下的城市地形没有合成；
        /// 回读请求出错时数组内容也不可信，原先没检查。
        /// </summary>
        private static NativeArray<ushort> ReadbackToR16(Texture tex, string name)
        {
            var output = new NativeArray<ushort>(tex.width * tex.height, Allocator.Persistent);
            try
            {
                AsyncGPUReadbackRequest readback = AsyncGPUReadback.RequestIntoNativeArray(ref output, tex);
                readback.WaitForCompletion();
                if (readback.hasError)
                    throw new InvalidOperationException($"GPU readback of {name} failed");
            }
            catch
            {
                output.Dispose();
                throw;
            }

            ModLog.Info(Tag, $"GPU 回读 {name}: {tex.width}x{tex.height} 完成");
            return output;
        }

        // === 采样算法 ===

        /// <summary>
        /// Box Filter 降采样: 将 source (srcSize²) 降采样到 embedSize²，
        /// 写入 dest (destSize²) 的 [embedStart, embedStart+embedSize] 区域。
        /// </summary>
        private static void BoxFilterDownsample(
            NativeArray<ushort> source, int srcSize,
            NativeArray<ushort> dest, int destSize,
            int embedStart, int embedSize)
        {
            // 采样比例: srcSize / embedSize 个源像素 → 1 个目标像素
            float ratio = (float)srcSize / embedSize;

            for (int dy = 0; dy < embedSize; dy++)
            {
                for (int dx = 0; dx < embedSize; dx++)
                {
                    // 源区域范围
                    int sx0 = (int)(dx * ratio);
                    int sy0 = (int)(dy * ratio);
                    int sx1 = math.min((int)((dx + 1) * ratio), srcSize);
                    int sy1 = math.min((int)((dy + 1) * ratio), srcSize);

                    // Box filter: 计算源区域平均值
                    long sum = 0;
                    int count = 0;
                    for (int sy = sy0; sy < sy1; sy++)
                    {
                        for (int sx = sx0; sx < sx1; sx++)
                        {
                            sum += source[sy * srcSize + sx];
                            count++;
                        }
                    }

                    ushort avgValue = (ushort)(count > 0 ? sum / count : 0);
                    int destIdx = (embedStart + dy) * destSize + (embedStart + dx);
                    dest[destIdx] = avgValue;
                }
            }
        }

        /// <summary>
        /// 双线性上采样: 将 source (srcSize²) 的 [cropStart, cropStart+cropSize] 区域
        /// 上采样到 dest (destSize²) 全图。
        /// </summary>
        private static void BilinearUpsampleRegion(
            NativeArray<ushort> source, int srcSize,
            int cropStart, int cropSize,
            NativeArray<ushort> dest, int destSize)
        {
            // 整数窗口在 float 下都能精确表示，换算与原先逐位相同（14→28 分支的结果不变）
            BilinearUpsampleRegion(source, srcSize, new float2(cropStart), new float2(cropSize), dest, destSize);
        }

        /// <summary>
        /// 双线性上采样：将 source (srcSize²) 上 [cropStart, cropStart+cropSize) 的窗口上采样到 dest (destSize²) 全图。
        /// 窗口以源像素为单位，可为小数，两轴分开给（28→57 按背景的实际世界范围裁剪）。
        /// </summary>
        private static void BilinearUpsampleRegion(
            NativeArray<ushort> source, int srcSize,
            float2 cropStart, float2 cropSize,
            NativeArray<ushort> dest, int destSize)
        {
            float2 ratio = cropSize / destSize;

            for (int dy = 0; dy < destSize; dy++)
            {
                for (int dx = 0; dx < destSize; dx++)
                {
                    float srcX = cropStart.x + dx * ratio.x;
                    float srcY = cropStart.y + dy * ratio.y;

                    int x0 = math.clamp((int)srcX, 0, srcSize - 1);
                    int y0 = math.clamp((int)srcY, 0, srcSize - 1);
                    int x1 = math.min(x0 + 1, srcSize - 1);
                    int y1 = math.min(y0 + 1, srcSize - 1);

                    float fx = srcX - x0;
                    float fy = srcY - y0;

                    float v00 = source[y0 * srcSize + x0];
                    float v10 = source[y0 * srcSize + x1];
                    float v01 = source[y1 * srcSize + x0];
                    float v11 = source[y1 * srcSize + x1];

                    float result = math.lerp(
                        math.lerp(v00, v10, fx),
                        math.lerp(v01, v11, fx),
                        fy);

                    dest[dy * destSize + dx] = (ushort)math.clamp(result, 0, 65535);
                }
            }
        }

        /// <summary>
        /// 边缘混合：对 embed 区域边缘 blendWidth 像素范围内的值，
        /// 在降采样 detail 值和原始 world 底图值之间做 smoothstep 渐进过渡。
        /// 消除因两张纹理数据不一致导致的硬边界落差。
        /// </summary>
        private static void ApplyEdgeBlend(
            NativeArray<ushort> synthesized, int size,
            int embedStart, int embedSize,
            NativeArray<ushort> worldPixels, int blendWidth)
        {
            int embedEnd = embedStart + embedSize; // exclusive

            for (int y = embedStart; y < embedEnd; y++)
            {
                for (int x = embedStart; x < embedEnd; x++)
                {
                    // 计算到 embed 矩形四条边的最小距离
                    int distX = math.min(x - embedStart, embedEnd - 1 - x);
                    int distY = math.min(y - embedStart, embedEnd - 1 - y);
                    int distFromEdge = math.min(distX, distY);

                    if (distFromEdge >= blendWidth)
                        continue; // 远离边缘，保持纯 detail 值

                    int idx = y * size + x;
                    float t = (float)distFromEdge / blendWidth;

                    // smoothstep: 3t² - 2t³ (比线性插值更自然)
                    t = t * t * (3f - 2f * t);

                    // t=0 (边缘): 使用 world 值 → 与相邻的 world 像素无缝衔接
                    // t=1 (blendWidth 内侧): 使用 detail 值 → 保持降采样精度
                    float worldVal = worldPixels[idx];
                    float detailVal = synthesized[idx];
                    synthesized[idx] = (ushort)math.clamp(math.lerp(worldVal, detailVal, t), 0, 65535);
                }
            }
        }

        /// <summary>
        /// 外围混合（28→57）：嵌入区外 <paramref name="blendWidth"/> 像素内，从最近的嵌入区边缘像素（detail）
        /// 平滑过渡到底图；嵌入区内一个像素都不写。与 <see cref="ApplyEdgeBlend"/> 方向相反：
        /// 那个写在嵌入区内侧，会改动原城市的地形（28→57 计划 §3.3"旧区域内不执行改写"）。
        /// </summary>
        private static void ApplyOuterEdgeBlend(
            NativeArray<ushort> synthesized, int size,
            int embedStart, int embedSize, int blendWidth)
        {
            int embedEnd = embedStart + embedSize; // exclusive
            int from = math.max(0, embedStart - blendWidth);
            int to = math.min(size, embedEnd + blendWidth);

            for (int y = from; y < to; y++)
            {
                int edgeY = math.clamp(y, embedStart, embedEnd - 1);
                for (int x = from; x < to; x++)
                {
                    int edgeX = math.clamp(x, embedStart, embedEnd - 1);

                    // 到嵌入区的切比雪夫距离：0 在嵌入区内，不动；达到 blendWidth 保持底图
                    int dist = math.max(math.abs(x - edgeX), math.abs(y - edgeY));
                    if (dist == 0 || dist >= blendWidth)
                        continue;

                    // smoothstep：t=0 贴着边界取 detail 值，t=1 取底图值。只读嵌入区内与本像素，原地写安全
                    float t = (float)dist / blendWidth;
                    t = t * t * (3f - 2f * t);

                    int idx = y * size + x;
                    float edgeVal = synthesized[edgeY * size + edgeX];
                    float baseVal = synthesized[idx];
                    synthesized[idx] = (ushort)math.clamp(math.lerp(edgeVal, baseVal, t), 0, 65535);
                }
            }
        }

        /// <summary>
        /// 实测 embed 边界处 inside (降采样 detail) 与 outside (world 底图) 的平均 R16 差值。
        /// 沿四条边各采样 1px inside + 1px outside，取所有样本的均值差。
        /// </summary>
        private static int MeasureBoundaryOffset(
            NativeArray<ushort> synthesized, int size, int embedStart, int embedSize)
        {
            int embedEnd = embedStart + embedSize;
            long sumInside = 0, sumOutside = 0;
            int count = 0;

            // 采样间隔：每 4px 采一个点（减少计算量）
            int step = math.max(1, embedSize / 256);

            for (int i = embedStart; i < embedEnd; i += step)
            {
                // 左边界 (x=embedStart inside, x=embedStart-1 outside)
                if (embedStart > 0)
                {
                    sumInside += synthesized[i * size + embedStart];
                    sumOutside += synthesized[i * size + (embedStart - 1)];
                    count++;
                }
                // 右边界 (x=embedEnd-1 inside, x=embedEnd outside)
                if (embedEnd < size)
                {
                    sumInside += synthesized[i * size + (embedEnd - 1)];
                    sumOutside += synthesized[i * size + embedEnd];
                    count++;
                }
                // 上边界 (y=embedStart inside, y=embedStart-1 outside)
                if (embedStart > 0)
                {
                    sumInside += synthesized[embedStart * size + i];
                    sumOutside += synthesized[(embedStart - 1) * size + i];
                    count++;
                }
                // 下边界 (y=embedEnd-1 inside, y=embedEnd outside)
                if (embedEnd < size)
                {
                    sumInside += synthesized[(embedEnd - 1) * size + i];
                    sumOutside += synthesized[embedEnd * size + i];
                    count++;
                }
            }

            if (count == 0) return 0;
            return (int)((sumInside - sumOutside) / count);
        }

        /// <summary>
        /// 对 embed 区域外的所有像素施加 R16 偏移量（抬升或降低 world 底图）。
        /// </summary>
        private static void ApplyOffsetOutsideEmbed(
            NativeArray<ushort> data, int size, int embedStart, int embedSize, int offsetR16)
        {
            int embedEnd = embedStart + embedSize;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (x >= embedStart && x < embedEnd && y >= embedStart && y < embedEnd)
                        continue; // 跳过 embed 区域
                    int idx = y * size + x;
                    data[idx] = (ushort)math.clamp(data[idx] + offsetR16, 0, 65535);
                }
            }
        }

        #endregion

        #region ② NaturalResource 中心嵌入

        // === 原版 NaturalResource 纹理尺寸 ===
        private const int kVanillaNRTextureSize = 256;

        /// <summary>
        /// NaturalResource 资源修复：将反序列化拉伸的数据降采样回原版尺寸，
        /// 嵌入扩展地图中心区域，外围保持清空（由用户自行创建资源）。
        ///
        /// 策略：
        /// ① 从反序列化后被拉伸的 m_Map 中降采样回原版尺寸 (256²)，恢复原始资源数据。
        /// ② 清空整个 m_Map。
        /// ③ 将原版数据 1:1 嵌入到扩展地图中心。
        ///
        /// <para>取不到系统或字段、或处理出错都抛出、走失败路径（整合计划 Q19）：原先跳过或只记日志，
        /// 转换照样报成功，存下的是被拉伸的资源图。</para>
        /// </summary>
        private void ExecuteNaturalResourceRegen()
        {
            ModLog.Info(Tag, "② 开始 NaturalResource 中心嵌入...");

            // === 读取源: 原版 NaturalResourceSystem (存档数据在这里) ===
            var vanillaSystem = World.GetExistingSystemManaged<NaturalResourceSystem>()
                ?? throw new InvalidOperationException("NaturalResourceSystem not found");
            CompleteDependencies(vanillaSystem);

            var vanillaMapField = AccessTools.Field(typeof(CellMapSystem<NaturalResourceCell>), "m_Map")
                ?? throw new MissingFieldException("CellMapSystem<NaturalResourceCell>.m_Map not found");
            var vanillaMap = (NativeArray<NaturalResourceCell>)vanillaMapField.GetValue(vanillaSystem);
            int vanillaSize = (int)math.sqrt(vanillaMap.Length); // 应为 256

#if DEBUG
            ModLog.Info(Tag, $"原版 NaturalResource m_Map: Length={vanillaMap.Length}, Size={vanillaSize}");
#endif

            // 统计原版数据中的非零 cell（兼作全零守卫）
            int nzFert = 0, nzOre = 0, nzOil = 0;
            for (int i = 0; i < vanillaMap.Length; i++)
            {
                var dc = vanillaMap[i];
                if (dc.m_Fertility.m_Base > 0) nzFert++;
                if (dc.m_Ore.m_Base > 0) nzOre++;
                if (dc.m_Oil.m_Base > 0) nzOil++;
            }
#if DEBUG
            ModLog.Info(Tag, $"原版数据统计: 非零 Fertility={nzFert}, Ore={nzOre}, Oil={nzOil} (共{vanillaMap.Length}cells)");
#endif

            // 全零也要照常清零，不能跳过：原版地图开新城时，本模式的 SetDefaults 收到 NewGame，
            // 已经按 Perlin 生成了整张资源（v4.12.0 整合计划 §1.7），跳过会把它留下
            if (nzFert == 0 && nzOre == 0 && nzOil == 0)
                ModLog.Warn(Tag, "原版数据全为零（地图没有资源，或存档未正确反序列化），Mod 地图照常清零");

            // === 写入目标: Mod NaturalResourceSystemMod (大地图 CellMap) ===
            var modSystem = GetNaturalResourceSystemMod()
                ?? throw new InvalidOperationException($"NaturalResourceSystemMod not found (CV={PatchManager.CurrentCoreValue})");
            CompleteDependencies(modSystem);

            var modMapField = AccessTools.Field(modSystem.GetType().BaseType, "m_Map")
                ?? throw new MissingFieldException("NaturalResourceSystemMod m_Map not found");
            var modMap = (NativeArray<NaturalResourceCell>)modMapField.GetValue(modSystem);
            int modSize = (int)math.sqrt(modMap.Length); // ModeA=1024, ModeB=512

#if DEBUG
            ModLog.Info(Tag, $"Mod NaturalResource m_Map: Length={modMap.Length}, Size={modSize}");
#endif

            // --- 清空 mod m_Map ---
            for (int i = 0; i < modMap.Length; i++)
                modMap[i] = default;

            // --- 将原版数据 1:1 嵌入 mod 地图中心 ---
            // 原版: 256 cells × 56m/cell = 14336m
            // ModeA 1024²: embedStart = (1024-256)/2 = 384
            // ModeB 512²:  embedStart = (512-256)/2 = 128
            int embedStart = (modSize - vanillaSize) / 2;
            int embedEnd = embedStart + vanillaSize;

            for (int vy = 0; vy < vanillaSize; vy++)
            {
                for (int vx = 0; vx < vanillaSize; vx++)
                {
                    int srcIdx = vy * vanillaSize + vx;
                    int dstIdx = (embedStart + vy) * modSize + (embedStart + vx);
                    modMap[dstIdx] = vanillaMap[srcIdx];
                }
            }

            ModLog.Ok(Tag, $"NaturalResource 修复完成: 原版 {vanillaSize}² 嵌入 Mod {modSize}² 中心 [{embedStart}:{embedEnd}], 外围清空");
        }

        #endregion

        #region ③ GroundWater 中心嵌入

        // === 原版 GroundWater 纹理尺寸 ===
        private const int kVanillaGWTextureSize = 256;

        /// <summary>
        /// GroundWater 修复：将反序列化拉伸的数据降采样回原版尺寸，
        /// 嵌入扩展地图中心区域，外围保持清空。策略与 NaturalResource 相同，出错同样抛出（整合计划 Q19）。
        /// </summary>
        private void ExecuteGroundWaterInit()
        {
            ModLog.Info(Tag, "③ 开始 GroundWater 中心嵌入...");

            // === 读取源: 原版 GroundWaterSystem (存档数据在这里) ===
            var vanillaSystem = World.GetExistingSystemManaged<GroundWaterSystem>()
                ?? throw new InvalidOperationException("GroundWaterSystem not found");
            CompleteDependencies(vanillaSystem);

            var vanillaMap = GetVanillaGroundWaterMap(vanillaSystem);
            int vanillaSize = (int)math.sqrt(vanillaMap.Length); // 应为 256

#if DEBUG
            ModLog.Info(Tag, $"原版 GroundWater m_Map: Length={vanillaMap.Length}, Size={vanillaSize}");
            int nzAmount = 0;
            for (int i = 0; i < vanillaMap.Length; i++)
                if (vanillaMap[i].m_Amount > 0) nzAmount++;
            ModLog.Info(Tag, $"原版数据统计: 非零 Amount={nzAmount} (共{vanillaMap.Length}cells)");
#endif

            // === 写入目标: Mod GroundWaterSystemMod ===
            var modSystem = GetGroundWaterSystemMod()
                ?? throw new InvalidOperationException($"GroundWaterSystemMod not found (CV={PatchManager.CurrentCoreValue})");
            CompleteDependencies(modSystem);

            var modMapField = AccessTools.Field(modSystem.GetType().BaseType, "m_Map")
                ?? throw new MissingFieldException("GroundWaterSystemMod m_Map not found");
            var modMap = (NativeArray<GroundWater>)modMapField.GetValue(modSystem);
            int modSize = (int)math.sqrt(modMap.Length);

#if DEBUG
            ModLog.Info(Tag, $"Mod GroundWater m_Map: Length={modMap.Length}, Size={modSize}");
#endif

            // --- 清空 mod m_Map ---
            for (int i = 0; i < modMap.Length; i++)
                modMap[i] = default;

            // --- 将原版数据 1:1 嵌入中心 ---
            int embedStart = (modSize - vanillaSize) / 2;
            int embedEnd = embedStart + vanillaSize;

            for (int vy = 0; vy < vanillaSize; vy++)
            {
                for (int vx = 0; vx < vanillaSize; vx++)
                {
                    int srcIdx = vy * vanillaSize + vx;
                    int dstIdx = (embedStart + vy) * modSize + (embedStart + vx);
                    modMap[dstIdx] = vanillaMap[srcIdx];
                }
            }

            ModLog.Ok(Tag, $"GroundWater 修复完成: 原版 {vanillaSize}² 嵌入 Mod {modSize}² 中心 [{embedStart}:{embedEnd}], 外围清空");
        }

        /// <summary>
        /// 28→57：清空原版 <c>GroundWaterSystem</c> 实例的 <c>m_Map</c>（28→57 计划 §3.4 防回填）。
        /// <para>ModeB 存档的原版区段里还留着最早那张 14 km 地图的原稿。不清的话，新档重启后若本模式的地下水场全为零
        /// （合法的无含水层地图），<c>GroundWaterSystemMod.OnGameLoaded</c> 的空场修复会把原稿等比拉伸，回填整张图。
        /// 原版系统已停用、不模拟，清空后到自动存档前不会再被写入。</para>
        /// </summary>
        private void ExecuteVanillaGroundWaterClear()
        {
            ModLog.Info(Tag, "③ 开始清空原版地下水实例...");

            var vanillaSystem = World.GetExistingSystemManaged<GroundWaterSystem>()
                ?? throw new InvalidOperationException("GroundWaterSystem not found");
            CompleteDependencies(vanillaSystem);

            var vanillaMap = GetVanillaGroundWaterMap(vanillaSystem);
            int aquifer = 0;
            for (int i = 0; i < vanillaMap.Length; i++)
            {
                // 含水层与 GroundWaterSystemMod.CountAquiferCells 同口径：m_Max 非零
                if (vanillaMap[i].m_Max != 0) aquifer++;
                vanillaMap[i] = default;
            }

            ModLog.Ok(Tag, $"已清空原版地下水实例：{(int)math.sqrt(vanillaMap.Length)}²，原有含水层 {aquifer} 格；新档重启后不会被拉伸回填");
        }

        /// <summary>原版 <c>GroundWaterSystem</c> 的 <c>m_Map</c>（反射取 <c>CellMapSystem&lt;GroundWater&gt;</c> 的字段）。</summary>
        private static NativeArray<GroundWater> GetVanillaGroundWaterMap(GroundWaterSystem vanillaSystem)
        {
            var field = AccessTools.Field(typeof(CellMapSystem<GroundWater>), "m_Map")
                ?? throw new MissingFieldException("CellMapSystem<GroundWater>.m_Map not found");
            return (NativeArray<GroundWater>)field.GetValue(vanillaSystem);
        }

        /// <summary>
        /// 完成目标系统的所有挂起 Job 依赖，确保安全读写 m_Map。
        /// 取不到依赖字段就保证不了这一点，抛出、走失败路径（整合计划 Q19）；原先只记 Warn，照样读写。
        /// </summary>
        private static void CompleteDependencies(GameSystemBase system)
        {
            // CellMapSystem<T> 的依赖字段在基类中，AccessTools.Field 会沿继承链往上找
            var baseType = system.GetType().BaseType;
            var writeDeps = AccessTools.Field(baseType, "m_WriteDependencies")
                ?? throw new MissingFieldException($"{system.GetType().Name}: m_WriteDependencies not found");
            var readDeps = AccessTools.Field(baseType, "m_ReadDependencies")
                ?? throw new MissingFieldException($"{system.GetType().Name}: m_ReadDependencies not found");

            ((JobHandle)writeDeps.GetValue(system)).Complete();
            ((JobHandle)readDeps.GetValue(system)).Complete();
        }

        #endregion

        #region ④ 自动保存

        /// <summary>
        /// 转换结果的自动存档，在原版 <c>"SaveLoadGame"</c> 任务队列里执行（与 <c>QuickSave</c> 相同的做法）。
        /// <list type="bullet">
        /// <item>新档名去重：<c>原名_MapExtXX</c>，重名时加 " (2)"、" (3)"……，不覆盖任何已有存档；</item>
        /// <item>存储位置与原版存档一致：玩家最近选的本地或云端库；</item>
        /// <item>预览照 <c>QuickSave</c> 截一张，存完再销毁；</item>
        /// <item>成败以 <c>onGameSaveLoad</c> 结束事件的 <c>success</c> 为准，并核对目标资产确实存在
        /// （<c>GameManager.Save</c> 无论成败都返回 true）；</item>
        /// <item>不论成败，"上次存档名"与存储位置都改指新档：读档任务刚把它们设成源档的，不改的话 F5 会写回源档。</item>
        /// </list>
        /// </summary>
        private async Task SaveConvertedAsync(ConversionRequest request)
        {
            GameManager gm = GameManager.instance;
            MenuUISystem menuUI = null;
            ILocalAssetDatabase db = null;
            string saveName = null;
            bool? saveSuccess = null;
            RenderTexture preview = null;

            void OnSaveEvent(string name, string previewUri, bool start, bool success)
            {
                if (!start && name == saveName) saveSuccess = success;
            }

            try
            {
                if (VanillaConversionState.Current != request)
                {
                    ModLog.Warn(Tag, "转换请求已被清除，取消自动存档");
                    return;
                }

                menuUI = World.GetExistingSystemManaged<MenuUISystem>();
                if (menuUI == null) throw new InvalidOperationException("MenuUISystem 不存在，无法生成存档信息");

                db = ResolveTargetDatabase();
                saveName = MakeUniqueSaveName(db, request);
                ModLog.Info(Tag, $"④ 自动存档：'{saveName}'（{db.name}）");

                // 请求处于"保存中"，元数据补丁会给这份存档打上转换标签
                var saveInfo = menuUI.GetSaveInfo(false);

                preview = ScreenCaptureHelper.CreateRenderTarget("SaveGamePanel", kPreviewWidth, kPreviewHeight);
                if (Camera.main != null)
                    ScreenCaptureHelper.CaptureScreenshot(Camera.main, preview, new MenuHelpers.SaveGamePreviewSettings());

#if DEBUG
                // 故障注入（DEBUG 自检）：FaultInject.txt 写 SaveGate/AutoSave，模拟自动存档失败（验收 C3）
                if (MapExtHealth.IsFaultInjected("SaveGate/AutoSave"))
                    throw new InvalidOperationException("Fault injected (DEBUG self-test)");
#endif
                gm.onGameSaveLoad += OnSaveEvent;
                await gm.Save(saveName, saveInfo, db, preview);

                bool exists = SaveExists(db, saveName);
                if (saveSuccess == true && exists)
                {
                    request.Phase = ConversionPhase.Completed;
                    ModLog.Ok(Tag, $"自动存档完成：'{saveName}'");
                    ShowCompletionDialog(request, saveName);
                }
                else
                {
                    Fail(request, "AutoSave", $"存档结果 {(saveSuccess.HasValue ? saveSuccess.Value.ToString() : "未收到结束事件")}，目标资产存在 {exists}");
                    ShowSaveFailedDialog(saveName);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, $"自动存档 '{saveName}' 失败");
                Fail(request, "AutoSave", ex.Message);
                ShowSaveFailedDialog(saveName ?? request.DisplayName);
            }
            finally
            {
                if (gm != null) gm.onGameSaveLoad -= OnSaveEvent;
                if (preview != null)
                {
                    preview.Release();
                    UnityEngine.Object.Destroy(preview);
                }

                // 自动存档失败时也改：玩家按 F5 只会把城市存成这个新档名
                if (saveName != null) LastSaveTarget.Redirect(menuUI, saveName, db);
            }
        }

        /// <summary>与原版存档一致：按玩家最近选的存储位置（本地或云端）取目标库，取不到时退回本地库。</summary>
        private static ILocalAssetDatabase ResolveTargetDatabase()
        {
            try
            {
                ILocalAssetDatabase db = MenuHelpers.GetSanitizedCloudTarget(
                    Game.Settings.SharedSettings.instance.userState.lastCloudTarget).db;
                if (db != null) return db;
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"取存储位置失败：{ex.Message}");
            }

            ModLog.Info(Tag, "使用本地存档库 AssetDatabase.user");
            return Colossal.IO.AssetDatabase.AssetDatabase.user;
        }

        /// <summary>"原名_MapExt模式"，已有同名存档时依次加 " (2)"、" (3)"……</summary>
        private static string MakeUniqueSaveName(ILocalAssetDatabase db, ConversionRequest request)
        {
            string modeSuffix = PatchManager.GetModeNameForCoreValue(request.TargetCoreValue).Replace(" ", "");
            string original = string.IsNullOrEmpty(request.DisplayName) ? "VanillaSave" : request.DisplayName;
            string baseName = $"{original}_MapExt{modeSuffix}";

            string name = baseName;
            for (int i = 2; SaveExists(db, name) && i < 1000; i++) name = $"{baseName} ({i})";
            return name;
        }

        private static bool SaveExists(ILocalAssetDatabase db, string name) =>
            db.Exists<PackageAsset>(SaveHelpers.GetAssetDataPath<Game.Assets.SaveGameMetadata>(db, name), out _);

        /// <summary>
        /// 转换失败或被否决、没有自动存档时，把"上次存档名"改指预定的新档名。
        /// 玩家按 F5，转换了一半或被否决的城市只会存成新档，不会写回源档。
        /// </summary>
        private Task RedirectToNewSaveName(ConversionRequest request)
        {
            try
            {
                ILocalAssetDatabase db = ResolveTargetDatabase();
                LastSaveTarget.Redirect(World.GetExistingSystemManaged<MenuUISystem>(), MakeUniqueSaveName(db, request), db);
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, $"转换{(request.Phase == ConversionPhase.Cancelled ? "被否决" : "失败")}后改指上次存档名失败，快速存档仍会用源档名");
            }

            return Task.CompletedTask;
        }

        #endregion

        #region Helpers

        /// <summary>获取当前模式的 NaturalResourceSystemMod 实例</summary>
        private GameSystemBase GetNaturalResourceSystemMod()
        {
            int cv = PatchManager.CurrentCoreValue;
            switch (cv)
            {
                case 4: return World.GetExistingSystemManaged<ModeA.NaturalResourceSystemMod>();
                case 2: return World.GetExistingSystemManaged<ModeB.NaturalResourceSystemMod>();
                default:
                    ModLog.Warn(Tag, $"不支持的 CV={cv} 的 NaturalResourceSystemMod");
                    return null;
            }
        }

        /// <summary>获取当前模式的 GroundWaterSystemMod 实例</summary>
        private GameSystemBase GetGroundWaterSystemMod()
        {
            int cv = PatchManager.CurrentCoreValue;
            switch (cv)
            {
                case 4: return World.GetExistingSystemManaged<ModeA.GroundWaterSystemMod>();
                case 2: return World.GetExistingSystemManaged<ModeB.GroundWaterSystemMod>();
                default:
                    ModLog.Warn(Tag, $"不支持的 CV={cv} 的 GroundWaterSystemMod");
                    return null;
            }
        }

        // === ⓪ MapTile 边界重建 ===

        /// <summary>
        /// 通过反射调用 MapTileSystem.LegacyGenerateMapTiles(false) 重建 MapTile 实体：它销毁旧分块，
        /// 生成 529 格（分块尺寸由 ReBurst 的 GenerateMapTilesJob 按 CV 放大），清空并重写 m_StartTiles，
        /// 全部加 Native 后只给中央 3×3 去掉。
        ///
        /// <para><paramref name="unlockAll"/> 为 true（存档转换）时再移除全部 Native，529 格全部解锁；
        /// 开新城保留原版的购地节奏，只解锁中央 9 格（D2），地图作者自定的起始格不保留。</para>
        ///
        /// <para>失败直接抛出、走失败路径：吞掉的话会静默产出只有中央 14 km 可买的城市（v4.12.0 整合计划 §1.7）。
        /// 异常消息用英文，会原样写进三语的失败提示。</para>
        /// </summary>
        private void ExecuteMapTileRebuild(bool unlockAll)
        {
            ModLog.Info(Tag, $"⓪ 开始 MapTile 边界重建（{(unlockAll ? "全部解锁" : "只解锁中央 9 格")}）...");

            var mapTileSystem = World.GetExistingSystemManaged<Game.Areas.MapTileSystem>()
                ?? throw new InvalidOperationException("MapTileSystem not found");

            // 通过反射调用 private LegacyGenerateMapTiles(false)
            var method = AccessTools.Method(typeof(Game.Areas.MapTileSystem), "LegacyGenerateMapTiles")
                ?? throw new MissingMethodException("MapTileSystem.LegacyGenerateMapTiles not found (renamed by a game update?)");

            try
            {
                method.Invoke(mapTileSystem, new object[] { false });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                // 反射调用把原异常包了一层，失败提示要的是原异常
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }

            ModLog.Info(Tag, "LegacyGenerateMapTiles(false) 已执行，529 tiles 已重建");

            var lockedQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Game.Areas.MapTile>(),
                ComponentType.ReadOnly<Native>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Deleted>());

            if (unlockAll)
            {
                // 移除所有 MapTile 上的 Native 组件 → 全部解锁
                int nativeCount = lockedQuery.CalculateEntityCount();
                if (nativeCount > 0)
                {
                    EntityManager.RemoveComponent<Native>(lockedQuery);
                    ModLog.Info(Tag, $"已移除 {nativeCount} 个 MapTile 的 Native 组件（全部解锁）");
                }

                ModLog.Ok(Tag, "MapTile 边界重建完成 (529 tiles, 全部解锁)");
                return;
            }

            // 开新城：核对解锁格数，验收 V5 以这一行为准
            var allQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Game.Areas.MapTile>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Deleted>());
            int total = allQuery.CalculateEntityCount();
            int unlocked = total - lockedQuery.CalculateEntityCount();
            ModLog.Ok(Tag, $"MapTile 边界重建完成：{total} 格，解锁 {unlocked} 格（D2 应为中央 9 格）");
        }

        // === ⓪b OutsideConnection 节点清理 ===

        /// <summary>
        /// 给所有 OutsideConnection 实体添加 Deleted 组件，
        /// 触发游戏清理系统级联删除关联的 Edge/Lane/SubLane 等网络实体。
        /// 包括电力和水管类 OC（它们同样位于旧边界，需在新边界重建）。
        /// <para>出错抛出、走失败路径（整合计划 Q19）：原先只记日志，转换照样报成功，旧边界上留着外部连接。</para>
        /// </summary>
        private void ExecuteOutsideConnectionCleanup()
        {
            ModLog.Info(Tag, "⓪c 开始 OutsideConnection 节点清理（全类型）...");

            var ocQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Game.Objects.OutsideConnection>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            int count = ocQuery.CalculateEntityCount();
            if (count > 0)
            {
                // 完成所有挂起的 Job，防止结构变更后访问已变更 archetype 的实体
                EntityManager.CompleteAllTrackedJobs();
                EntityManager.AddComponent<Deleted>(ocQuery);
                ModLog.Ok(Tag, $"已标记 {count} 个 OutsideConnection 实体为删除（含交通/电力/水管）");
            }
            else
            {
                ModLog.Info(Tag, "未发现 OutsideConnection 实体");
            }
        }

        // === ③b 水模拟重置 ===

        /// <summary>
        /// 安全版水源升级 + 海平面重置（复现 UpgradeToNewWaterSystem 逻辑）。
        /// 关键改进：
        /// ① 保留非海源水源（河流 type 0、湖泊 type 1），仅删除海源（type 2/3）。
        /// ② 对保留水源执行 Legacy→新系统的 m_Height 语义转换。
        /// ③ 清除陈旧 GPU 流速/传播纹理，防止水面流向异常。
        /// ④ 使用 AddComponent&lt;Deleted&gt; 替代 DestroyEntity，避免并行 Job NRE。
        /// <para>处理结果记到 <see cref="ConversionRequest.WaterSources"/>，完成提示按它写水体一段。</para>
        /// <para>出错抛出、走失败路径（整合计划 Q19）：水面贴图在扩展后覆盖范围错拉，水源处理与 GPU 重置是必需步骤
        /// （28→57 计划 §3.5），原先只记日志，转换照样报成功。</para>
        /// </summary>
        private void ExecuteWaterSimReset(ConversionRequest request)
        {
            ModLog.Info(Tag, "③b 开始水模拟安全升级...");

            var waterSystem = World.GetExistingSystemManaged<WaterSystem>()
                ?? throw new InvalidOperationException("WaterSystem not found");

            float currentSeaLevel = waterSystem.SeaLevel;
            bool isLegacy = waterSystem.UseLegacyWaterSources;
            ModLog.Info(Tag, $"当前海平面: {currentSeaLevel}m, Legacy水源: {isLegacy}");

            // --- 共用：暂停水模拟、完成 Job ---
            waterSystem.WaterSimSpeed = 0;
            EntityManager.CompleteAllTrackedJobs();

            if (!isLegacy)
            {
                // === 非 Legacy 路径（MapExt vanilla 模式存档转换）===
                // 地形已完全替换，需要完整的 GPU 状态重置
                ClearWaterGpuState(waterSystem);

                // 原值写回 SeaLevel：setter 经 UpdateSeaLevel 置 m_seaLevelChanged，下一次 Simulate 重算海域传播
                // （移除海水、清传播、重新传播、按海平面补回）。Legacy 路径设海平面、MapExtUI 调海平面走的都是这一步；
                // 不写的话海域传播已被上面清空，海水只能从地图边缘按模拟速度慢慢流进来
                // （2026-11-06 第 3 轮验收：开新城与 28→57 的新档都要手动调海平面才注满，原版档转换正常）
                waterSystem.SeaLevel = currentSeaLevel;
                waterSystem.ResetToSealevel();

                int cv2 = PatchManager.CurrentCoreValue;
                int frames2 = cv2 >= 4 ? 16 : cv2 >= 2 ? 8 : 5;
                var newMapField2 = AccessTools.Field(typeof(WaterSystem), "m_NewMap");
                if (newMapField2 != null)
                    newMapField2.SetValue(waterSystem, frames2);

                // TerrainWillChange() 内部会设置 WaterSimSpeed=0，
                // 必须在之后重新设置 WaterSimSpeed=1
                waterSystem.TerrainWillChange();
                waterSystem.WaterSimSpeed = 1;

                request.WaterSources = WaterSourceOutcome.Kept;
                ModLog.Ok(Tag, $"已是新版水源，执行完整 GPU 重置 + 重设海平面 {currentSeaLevel}m + ResetToSealevel + m_NewMap={frames2}");
                return;
            }

            // === Legacy 路径：安全版 UpgradeToNewWaterSystem ===

            // --- ③ 获取新地形高度数据（Heightmap 合成已完成）---
            var terrainSystem = World.GetExistingSystemManaged<TerrainSystem>();
            TerrainHeightData terrainData = default;
            bool terrainReady = false;

            if (terrainSystem != null)
            {
                terrainSystem.AddCPUHeightReader(default);
                terrainData = terrainSystem.GetHeightData(waitForPending: true);
                terrainReady = terrainData.isCreated;
            }

            if (!terrainReady)
                ModLog.Warn(Tag, "TerrainHeightData 未就绪，保留水源将使用降级处理（全删）");

            // --- ④ 遍历水源：按类型分类处理 ---
            var sourceQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Game.Simulation.WaterSourceData>(),
                ComponentType.ReadOnly<Game.Objects.Transform>(),
                ComponentType.Exclude<Deleted>());

            int totalSources = sourceQuery.CalculateEntityCount();
            // 与原版 UpgradeToNewWaterSystem 一致，初始值为 MaxValue
            // 仅在实际发现 Type 2/3 海源时才更新
            float minSeaLevel = float.MaxValue;
            bool hasSeaSource = false;
            int deletedCount = 0, upgradedCount = 0;

            if (totalSources > 0)
            {
                var getNextIdMethod = AccessTools.Method(typeof(WaterSystem), "GetNextSourceId");
                var entities = sourceQuery.ToEntityArray(Allocator.TempJob);

                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    var sourceData = EntityManager.GetComponentData<WaterSourceData>(entity);

                    // --- Type 3: 旧版海源 → 删除（已被新 SeaLevel 系统替代）---
                    if (sourceData.m_ConstantDepth == 3)
                    {
                        minSeaLevel = math.min(minSeaLevel, sourceData.m_Height);
                        hasSeaSource = true;
                        EntityManager.AddComponent<Deleted>(entity);
                        deletedCount++;
                        continue;
                    }

                    // --- Type 2: 海水源 → 记录 m_Height（绝对海平面高度）---
                    // 原版 UpgradeToNewWaterSystem 中，Type 2 判断后**不用 continue**，
                    // 继续走下面的通用坐标升级流程（XZ clamp + WaterUtils.SampleHeight + 相对水深转换）。
                    // 我们这里也保持一致，仅记录 minSeaLevel 后 fall-through。
                    if (sourceData.m_ConstantDepth == 2)
                    {
                        minSeaLevel = math.min(minSeaLevel, sourceData.m_Height);
                        hasSeaSource = true;
                        // 不 continue —— 走后续的通用坐标升级流程
                    }

                    // --- 通用坐标升级（Type 0/1/2 共用）---
                    if (!terrainReady)
                    {
                        // 降级：无地形数据时只能删除
                        EntityManager.AddComponent<Deleted>(entity);
                        deletedCount++;
                        continue;
                    }

                    var transform = EntityManager.GetComponentData<Game.Objects.Transform>(entity);

                    // 将 XZ 钳制到新地形边界内
                    Bounds3 bounds = TerrainUtils.GetBounds(ref terrainData);
                    if (!MathUtils.Intersect(bounds.xz, transform.m_Position.xz))
                    {
                        transform.m_Position.xz = MathUtils.Clamp(transform.m_Position.xz, bounds.xz);
                    }

                    // 采样新地形高度
                    float newTerrainH = TerrainUtils.SampleHeight(ref terrainData, transform.m_Position);

                    if (sourceData.m_ConstantDepth == 1)
                    {
                        // Type 1（恒定深度）: Legacy m_Height 是绝对水面高度 → 转为相对水深
                        float absoluteWaterLevel = sourceData.m_Height;
                        float relativeDepth = absoluteWaterLevel - newTerrainH;
                        // 钳制：至少 1m 水深，避免负值（地形可能因降采样略有变化）
                        sourceData.m_Height = math.max(1f, relativeDepth);
                    }
                    else if (sourceData.m_ConstantDepth == 2)
                    {
                        // Type 2（海水源）: Legacy m_Height 是绝对海平面高度 → 转为相对水深
                        // 原版: componentData.m_Height = num2 - num3 (WaterSampleHeight - TerrainHeight)
                        // 我们无法调用 WaterUtils.SampleHeight（需要 GPU 数据），
                        // 直接用 m_Height（绝对海平面） - 地形高度作为相对水深
                        float seaRelativeDepth = sourceData.m_Height - newTerrainH;
                        sourceData.m_Height = math.max(0f, seaRelativeDepth);
                    }
                    // else: Type 0（流水源）: m_Height 是流量系数，不需要转换

                    // 更新 Transform.y
                    // 原版: Transform.y = WaterUtils.SampleHeight（GPU 水面采样）
                    // Type 2 海水源: 水面 ≈ 海平面高度，使用 minSeaLevel
                    // Type 0/1: 无 GPU 数据，近似用地形高度
                    transform.m_Position.y = (sourceData.m_ConstantDepth == 2)
                        ? minSeaLevel
                        : newTerrainH;

                    // 分配新源 ID
                    if (getNextIdMethod != null)
                    {
                        sourceData.m_Id = (int)getNextIdMethod.Invoke(waterSystem, null);
                    }

                    EntityManager.SetComponentData(entity, transform);
                    EntityManager.SetComponentData(entity, sourceData);
                    upgradedCount++;
                }

                entities.Dispose();
            }

            ModLog.Info(Tag, $"水源处理: 删除 {deletedCount} 个旧版海源(Type 3), 保留并升级 {upgradedCount} 个水源(含 Type 2 海水源)");
            // 地形数据未就绪时上面只能全删；本来就没有水源的不算删除
            request.WaterSources = terrainReady || totalSources == 0 ? WaterSourceOutcome.Upgraded : WaterSourceOutcome.Removed;

            // --- ⑤ 设置 Legacy 旗标 ---
            // 使用反射直接设置字段，绕过 setter 中的 UpgradeToNewWaterSystem() 调用
            var legacyField = AccessTools.Field(typeof(WaterSystem), "m_UseLegacyWaterSources");
            if (legacyField != null)
            {
                legacyField.SetValue(waterSystem, false);
                ModLog.Info(Tag, "已设置 m_UseLegacyWaterSources = false");
            }

            // --- ⑥ 设置 m_SimulateBackdrop = false ---
            // 存档中的残留值可能为 true，需显式重置以匹配新水源系统初始状态
            var backdropField = AccessTools.Field(typeof(WaterSystem), "m_SimulateBackdrop");
            if (backdropField != null)
            {
                backdropField.SetValue(waterSystem, false);
            }

            // --- ⑦ 清除陈旧 GPU 流速/传播纹理 ---
            ClearWaterGpuState(waterSystem);

            // --- ⑧ 设置海平面 ---
            // 仅当实际存在海源（Type 2/3）时才设置 minSeaLevel，
            // 否则保持当前值（避免 float.MaxValue 覆盖）
            float finalSeaLevel = hasSeaSource ? minSeaLevel : currentSeaLevel;
            waterSystem.SeaLevel = finalSeaLevel;
            ModLog.Info(Tag, $"已设置海平面: {waterSystem.SeaLevel}m" +
                (hasSeaSource ? $" (从 Type 2/3 海源检测)" : " (无海源，使用默认值)"));

            // --- ⑨ 重置水渲染（填充水面纹理到海平面高度）---
            waterSystem.ResetToSealevel();

            // --- ⑩ 设置 m_NewMap（触发水模拟重新初始化）---
            // 原版 UpgradeToNewWaterSystem 使用 3，但扩展地图的 CellSize 更大
            // (ModeA: 28 vs 原版 7)，相同帧数内水传播的物理距离成比例缩短。
            // 需要按地图缩放比例增加初始化帧数，确保海水/水源传播能覆盖整个区域。
            // 参考: 原版 OnBackdropActiveChanged 在类似场景中使用 m_NewMap=16。
            int cv = PatchManager.CurrentCoreValue;
            int newMapFrames = cv >= 4 ? 16 : cv >= 2 ? 8 : 5;  // ModeA=16, ModeB=8, 其他=5
            var newMapField = AccessTools.Field(typeof(WaterSystem), "m_NewMap");
            if (newMapField != null)
            {
                newMapField.SetValue(waterSystem, newMapFrames);
            }

            // --- ⑪ 同步 shader global ---
            Shader.SetGlobalVector("colossal_WaterParams",
                new UnityEngine.Vector4(waterSystem.SeaLevel, 0f, 0f, 0f));

            // --- ⑫ 通知地形变更 ---
            // TerrainWillChange() 内部会设置 WaterSimSpeed=0，必须在之后重新设置
            waterSystem.TerrainWillChange();

            // --- ⑬ 恢复水模拟速度 ---
            // 必须在 TerrainWillChange() 之后，否则会被覆盖为 0
            waterSystem.WaterSimSpeed = 1;

            ModLog.Ok(Tag, $"水源安全升级完成: 删除 {deletedCount} 海源, 保留 {upgradedCount} 河流/湖泊, " +
                $"海平面 {waterSystem.SeaLevel}m, m_NewMap={newMapFrames}");
        }

        // === ③c 水体预模拟填充 ===

        /// <summary>
        /// 在保存前手动 dispatch 多轮 GPU 水体模拟帧，
        /// 让 Type 0/1 水源（河流/湖泊）预填充水体。
        /// 保存的存档已包含填好的水面，重载后无需等待缓慢注水。
        ///
        /// 关键解锁条件（不处理任何一个，Simulate() 都会空转）：
        /// ① m_terrainReady 必须 ≤ 0（反序列化后为 int.MaxValue）
        /// ② WaterSimSpeed 必须 > 0（TerrainWillChange 会设为 0）
        /// ③ SimulationSystem.selectedSpeed 必须 > 0（pausedAfterLoading 会设为 0）
        /// ④ TimeStepOverride = 1 最大化每步传播距离
        /// ⑤ WaterSimSpeed = MaxSpeed 最大化每帧内部迭代次数
        /// </summary>
        private void ExecuteWaterPreSimulation()
        {
            ModLog.Info(Tag, "③c 开始水体预模拟填充...");

            try
            {
                var waterSystem = World.GetExistingSystemManaged<WaterSystem>();
                if (waterSystem == null)
                {
                    ModLog.Warn(Tag, "WaterSystem 不存在，跳过预模拟");
                    return;
                }

                // 获取 WaterSystem 内部的 CommandBuffer（与 PostDeserialize 相同的模式）
                var cmdBufField = AccessTools.Field(typeof(WaterSystem), "m_CommandBuffer");
                var cmdBuf = cmdBufField?.GetValue(waterSystem) as CommandBuffer;
                if (cmdBuf == null)
                {
                    ModLog.Warn(Tag, "无法获取 WaterSystem.m_CommandBuffer，跳过预模拟");
                    return;
                }

                // --- 解锁条件 ① m_terrainReady → 0 ---
                // Simulate() 入口: m_terrainReady--; if (m_terrainReady > 0) return;
                // 反序列化后 m_terrainReady = int.MaxValue，不设为 0 的话一帧都不会跑
                var terrainReadyField = AccessTools.Field(typeof(WaterSystem), "m_terrainReady");
                if (terrainReadyField != null)
                {
                    terrainReadyField.SetValue(waterSystem, 0);
                    ModLog.Info(Tag, "已设置 m_terrainReady=0");
                }

                // --- 解锁条件 ② WaterSimSpeed = MaxSpeed ---
                // Simulate 内部 for (i=0; i<WaterSimSpeed; i++) 循环控制每帧迭代次数
                int maxSpeed = waterSystem.MaxSpeed;
                waterSystem.WaterSimSpeed = maxSpeed;

                // --- 解锁条件 ③ selectedSpeed > 0 ---
                // Simulate 内部循环有 if (selectedSpeed == 0f) break;
                // 因为我们设置了 pausedAfterLoading=true，需要临时解除
                var simSystem = World.GetExistingSystemManaged<SimulationSystem>();
                float origSpeed = simSystem?.selectedSpeed ?? 1f;
                if (simSystem != null)
                    simSystem.selectedSpeed = 1f;

                // --- 解锁条件 ④ TimeStepOverride = 1 ---
                // GetTimeStep() 中: if (TimeStepOverride > 0) return TimeStepOverride;
                // 默认 timestep 很小（~0.03），设为 1 可最大化每步水传播距离
                float origTimeStep = waterSystem.TimeStepOverride;
                waterSystem.TimeStepOverride = 1f;

                // --- 解锁条件 ⑤ m_terrainChangeCounter = 0 ---
                // Simulate 在 terrainChangeCounter > 0 时走特殊恢复分支，不执行正常注水
                var terrainChangeField = AccessTools.Field(typeof(WaterSystem), "m_terrainChangeCounter");
                if (terrainChangeField != null)
                    terrainChangeField.SetValue(waterSystem, 0);

                // 先跑一次 OnUpdate 填充 SourceCache（GPU SourceStep 依赖它知道从哪注水）
                waterSystem.Update();

                // 按地图模式确定预模拟帧数
                // 每帧内部跑 MaxSpeed 次迭代 × TimeStep=1 → 等效传播距离大幅增加
                int cv = PatchManager.CurrentCoreValue;
                int preSimFrames = cv >= 4 ? 300 : cv >= 2 ? 200 : 100;

                ModLog.Info(Tag, $"预模拟 {preSimFrames} 帧 × {maxSpeed} 迭代/帧, TimeStep=1...");

                for (int i = 0; i < preSimFrames; i++)
                {
                    cmdBuf.Clear();
                    waterSystem.OnSimulateGPU(cmdBuf);
                    Graphics.ExecuteCommandBuffer(cmdBuf);

                    // 每 50 帧刷新一次 SourceCache（确保水源持续注水）
                    if (i % 50 == 49)
                        waterSystem.Update();
                }
                cmdBuf.Clear();

                // --- 恢复所有覆盖值 ---
                waterSystem.TimeStepOverride = origTimeStep;
                waterSystem.WaterSimSpeed = 1;
                if (simSystem != null)
                    simSystem.selectedSpeed = origSpeed;

                ModLog.Ok(Tag, $"水体预模拟完成: {preSimFrames} × {maxSpeed} = {preSimFrames * maxSpeed} 有效迭代");
            }
            catch (Exception ex)
            {
                // 预模拟失败不影响转换流程（水体会在重启后自然填充）
                ModLog.Warn(Tag, $"水体预模拟失败 (非致命，水体将在重启后自然填充): {ex.Message}");
            }
        }


        /// <summary>
        /// 清除陈旧的 GPU 水体状态：流速纹理、海水传播纹理、backdrop 流速纹理、
        /// 活跃格子标记，以及水源缓存。
        /// 必须在地形完全替换后、ResetToSealevel 前调用。
        /// </summary>
        private static void ClearWaterGpuState(WaterSystem waterSystem)
        {
            var traverse = Traverse.Create(waterSystem);

            try
            {
                // --- 清除 GPU 渲染目标 ---
                var cmdBufField = AccessTools.Field(typeof(WaterSystem), "m_CommandBuffer");
                var cmdBuf = cmdBufField?.GetValue(waterSystem) as CommandBuffer;

                if (cmdBuf != null)
                {
                    // 清除海水传播纹理
                    var waterBuf = traverse.Field("m_Water").GetValue<WaterSystem.QuadWaterBuffer>();

                    if (waterBuf.seaPropagationTexture != null)
                    {
                        cmdBuf.SetRenderTarget(waterBuf.seaPropagationTexture);
                        cmdBuf.ClearRenderTarget(false, true, UnityEngine.Color.black);
                    }

                    // 清除流速纹理 (FlowDownScaled[0])
                    var flowTex = waterSystem.FlowDownScaled(0);
                    if (flowTex != null)
                    {
                        cmdBuf.SetRenderTarget(flowTex);
                        cmdBuf.ClearRenderTarget(false, true, UnityEngine.Color.black);
                    }

                    // 清除 backdrop 流速纹理
                    if (waterBuf.downdScaledBackdropFlowTextures?[0] != null)
                    {
                        cmdBuf.SetRenderTarget(waterBuf.downdScaledBackdropFlowTextures[0]);
                        cmdBuf.ClearRenderTarget(false, true, UnityEngine.Color.black);
                    }

                    // 重置活跃格子标记
                    var waterSim = traverse.Field("m_waterSim").GetValue();
                    if (waterSim != null)
                    {
                        try
                        {
                            Traverse.Create(waterSim).Method("ResetActive", new[] { typeof(CommandBuffer) })
                                .GetValue(cmdBuf);
                        }
                        catch (Exception ex)
                        {
                            ModLog.Warn(Tag, $"ResetActive 调用失败 (非致命): {ex.Message}");
                        }
                    }

                    Graphics.ExecuteCommandBuffer(cmdBuf);
                    cmdBuf.Clear();

                    ModLog.Info(Tag, "已清除陈旧 GPU 流速/传播/活跃格子纹理");
                }
                else
                {
                    ModLog.Warn(Tag, "无法获取 m_CommandBuffer，跳过 GPU 纹理清除");
                }

                // --- 清除水源缓存 ---
                // 防止 OnUpdate 中的 SourceJob 引用已删除实体的残留数据
                ClearSourceCaches(traverse);

                // --- 重新绑定着色器纹理 ---
                try
                {
                    traverse.Method("BindTextures").GetValue();
                }
                catch (Exception ex)
                {
                    ModLog.Warn(Tag, $"BindTextures 调用失败 (非致命): {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"GPU 状态清除部分失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除 WaterSystem 的 SourceCache（NativeList），
        /// 防止残留的水源缓存引用已删除实体。
        /// </summary>
        private static void ClearSourceCaches(Traverse waterTraverse)
        {
            try
            {
                var cache1 = waterTraverse.Field("m_SourceCache1").GetValue();
                var cache2 = waterTraverse.Field("m_SourceCache2").GetValue();
                if (cache1 != null) Traverse.Create(cache1).Method("Clear").GetValue();
                if (cache2 != null) Traverse.Create(cache2).Method("Clear").GetValue();
                ModLog.Info(Tag, "已清除水源缓存 (m_SourceCache1/2)");
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"清除水源缓存失败 (非致命): {ex.Message}");
            }
        }

        // === ③c 车辆/居民清理 ===

        /// <summary>
        /// 标记所有车辆和居民为 Deleted，防止旧路径引用失效的实体导致 NRE。
        /// 使用游戏官方 DebugSystem.RemoveResidentsAndVehicles() 相同的方式：
        /// AddComponent&lt;Deleted&gt; 仅添加标记，不立即销毁，由后续帧安全回收。
        /// Vehicle 组件覆盖所有载具：汽车、卡车、公交、火车、地铁、轮船、飞机。
        /// </summary>
        private void ExecuteVehicleCleanup()
        {
            ModLog.Info(Tag, "③c 开始清除车辆和居民...");

            // 出错抛出、走失败路径（整合计划 Q19）：原先只记日志，接着删外部连接时车辆导航还引用着旧路径，会 NRE

            // 完成所有挂起的 Job（TrainNavigation/CarNavigation 等）
            // 否则已调度的 Job 会访问被 Deleted 实体的 archetype → NRE
            EntityManager.CompleteAllTrackedJobs();

            // 标记所有居民为 Deleted
            var residentQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Game.Creatures.Resident>());
            int residentCount = residentQuery.CalculateEntityCount();
            EntityManager.AddComponent<Game.Common.Deleted>(residentQuery);
            ModLog.Info(Tag, $"已标记 {residentCount} 个居民实体为 Deleted");

            // 标记所有车辆为 Deleted（汽车/火车/轮船/飞机/地铁等全部类型）
            var vehicleQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Game.Vehicles.Vehicle>());
            int vehicleCount = vehicleQuery.CalculateEntityCount();
            EntityManager.AddComponent<Game.Common.Deleted>(vehicleQuery);
            ModLog.Info(Tag, $"已标记 {vehicleCount} 个车辆实体为 Deleted");

            ModLog.Ok(Tag, $"车辆/居民清理完成: {vehicleCount} 车辆 + {residentCount} 居民");
        }

        /// <summary>存档确认写入后显示转换完成对话框，点击确认后退出游戏</summary>
        private static void ShowCompletionDialog(ConversionRequest request, string saveName)
        {
            try
            {
                string targetMode = PatchManager.GetModeNameForCoreValue(request.TargetCoreValue);

                var locParams = new Dictionary<string, ILocElement>
                {
                    { "TARGET_MODE", LocalizedString.Value(targetMode) },
                    { "SAVE_NAME", LocalizedString.Value(saveName) }
                };

                // 存档转换的水体一段按水源的实际处理结果写（原先一律写"原有水源已被清除"）
                if (!request.IsNewGame)
                    locParams.Add("WATER_NOTE", LocalizedString.Value(ResolveText(WaterNoteKey(request.WaterSources))));

                // 28→57 另写一句外围地形的来源：没有背景地形时外围是平地（28→57 计划 §3.3）
                if (request.IsSaveExpansion)
                    locParams.Add("TERRAIN_NOTE", LocalizedString.Value(ResolveText(request.FlatPeriphery
                        ? "VANILLA_CONVERT.ExpandTerrainFlat"
                        : "VANILLA_CONVERT.ExpandTerrainBackdrop")));

                // 开新城另用一套：分块只解锁中央 9 格，外部连接保留，没有要重建的东西；
                // 28→57 也另用一套：数据来自 28 km 存档，不是原版
                string messageKey = request.IsNewGame ? "VANILLA_CONVERT.NewGameCompleteMessage"
                    : request.IsSaveExpansion ? "VANILLA_CONVERT.ExpandCompleteMessage"
                    : "VANILLA_CONVERT.CompleteMessage";
                var dialog = new ConfirmationDialog(
                    LocalizedString.Id("VANILLA_CONVERT.Complete"),
                    new LocalizedString(messageKey, null, locParams),
                    LocalizedString.Id("VANILLA_CONVERT.QuitConfirm"),
                    LocalizedString.Id("VANILLA_CONVERT.QuitCancel"));

                // 使用 MainThreadDispatcher 延迟显示，确保在 RoadBuilder 等 mod
                // 的 OnGameLoadingComplete 对话框之后显示（避免被覆盖）
                Colossal.Core.MainThreadDispatcher.RegisterUpdater(() =>
                {
                    GameManager.instance.userInterface.appBindings.ShowConfirmationDialog(dialog, (int result) =>
                    {
                        if (result == 0)
                        {
                            ModLog.Info(Tag, "用户确认退出游戏");
                            GameManager.QuitGame();
                        }
                        else
                        {
                            ModLog.Info(Tag, "用户选择留在游戏中");
                        }
                    });
                    return true;
                });

                // === 通知條（與對話框並存）===
                // 對話框延後一帧只擋得住「同帧其它 Mod 也彈對話框」；擋不住跨帧的 UI 重建
                // （實測 RoadBuilder 會沖掉錯配彈窗，本對話框走同一條路徑、風險相同）。
                // 這一則尤其不能丟——它是玩家唯一得知「必須重啟才能玩轉換後的城市」的途徑，
                // 沖掉的話玩家會在水體未穩的狀態下繼續玩。
                NotificationSystem.Push(
                    identifier: "mapext.convert_complete",
                    title: LocalizedString.Id("VANILLA_CONVERT.Complete"),
                    text: new LocalizedString("VANILLA_CONVERT.CompleteNotify", null, locParams),
                    progressState: Colossal.PSI.Common.ProgressState.Complete,
                    progress: 100
                );
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"显示完成对话框失败: {ex.Message}");
            }
        }

        private static string WaterNoteKey(WaterSourceOutcome outcome)
        {
            switch (outcome)
            {
                case WaterSourceOutcome.Upgraded: return "VANILLA_CONVERT.WaterUpgraded";
                case WaterSourceOutcome.Removed: return "VANILLA_CONVERT.WaterRemoved";
                default: return "VANILLA_CONVERT.WaterKept";
            }
        }

        /// <summary>
        /// 按当前语言取一条本 Mod 文本，作为另一条文本的参数值；取不到时返回键名，便于发现漏写。
        /// 不传嵌套的 <c>LocalizedString.Id</c> 参数：界面端是否解析嵌套参数，本地没有前端源码可核对。
        /// </summary>
        private static string ResolveText(string key)
        {
            var dictionary = GameManager.instance?.localizationManager?.activeDictionary;
            return dictionary != null && dictionary.TryGetValue(key, out string text) ? text : key;
        }

        /// <summary>转换失败：写明失败步骤与原因。源存档没动，游戏保持暂停。</summary>
        private static void ShowFailureDialog(ConversionRequest request)
        {
            var locParams = new Dictionary<string, ILocElement>
            {
                { "STEP", LocalizedString.Value(request.FailedStep ?? "?") },
                { "ERROR", LocalizedString.Value(request.FailureDetail ?? "") }
            };
            ShowErrorMessage(new LocalizedString("VANILLA_CONVERT.Failed", null, locParams));
        }

        /// <summary>转换已完成但自动存档失败：源存档没动，游戏保持暂停，请玩家手动另存。</summary>
        private static void ShowSaveFailedDialog(string saveName)
        {
            var locParams = new Dictionary<string, ILocElement> { { "SAVE_NAME", LocalizedString.Value(saveName ?? "?") } };
            ShowErrorMessage(new LocalizedString("VANILLA_CONVERT.SaveFailed", null, locParams));
        }

        /// <summary>转换被否决：地形实际尺寸不是源尺寸，什么都没改，城市按原样载入，可以照常玩。</summary>
        private static void ShowVetoDialog(ConversionRequest request)
        {
            var locParams = new Dictionary<string, ILocElement>
            {
                { "NAME", LocalizedString.Value(request.DisplayName ?? "?") },
                { "TERRAIN_SIZE", LocalizedString.Value(MapSizeMismatchState.DescribeMapSize(request.VetoedTerrainMapSize)) },
                { "SOURCE_SIZE", LocalizedString.Value(MapSizeMismatchState.DescribeMapSize(request.SourceTerrainMapSize)) }
            };
            ShowMessage(LocalizedString.Id("VANILLA_CONVERT.VetoedTitle"),
                new LocalizedString("VANILLA_CONVERT.Vetoed", null, locParams));
        }

        private static void ShowErrorMessage(LocalizedString message) =>
            ShowMessage(LocalizedString.Id("LOAD_VALIDATION.TitleError"), message);

        /// <summary>延后一帧弹出对话框，排在其他 Mod 的加载完成对话框之后。</summary>
        private static void ShowMessage(LocalizedString title, LocalizedString message)
        {
            Colossal.Core.MainThreadDispatcher.RegisterUpdater(() =>
            {
                try
                {
                    var dialog = new MessageDialog(
                        title,
                        message,
                        LocalizedString.Id("LOAD_VALIDATION.ConfirmOK"));
                    GameManager.instance.userInterface.appBindings.ShowMessageDialog(dialog, null);
                }
                catch (Exception ex)
                {
                    ModLog.Warn(Tag, $"显示错误对话框失败: {ex.Message}");
                }

                return true;
            });
        }

        #endregion
    }
}
