// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using System;
using MapExtPDX.MapExt.Core;
using UnityEngine;

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>转换请求的来源。</summary>
    public enum ConversionKind : byte
    {
        /// <summary>没有 MapExt 标记的原版存档。</summary>
        VanillaSave = 0,

        /// <summary>MapExt 在 None 模式（CV=1）下保存的存档。</summary>
        CoreValue1Save = 1,

        /// <summary>
        /// 用原版地图开新城（v4.12.0 整合计划 2.4）：重建分块后只解锁中央 9 格（D2），
        /// 跳过车辆与居民清理。外部连接三种转换都保留（D1，存档转换自 Q27 起也保留）。
        /// </summary>
        VanillaMapNewGame = 2,

        /// <summary>
        /// MapExt ModeB（CV=2）存档扩展到 ModeA（v4.12.0 整合计划 Phase 3，28→57）：ModeB 的 5 张贴图经类型路由
        /// 读进 ModeA 系统并中心嵌入（<see cref="SaveExpansionTypeMapPatch"/>），不再从原版实例嵌入。
        /// </summary>
        CoreValue2Save = 3,
    }

    /// <summary>转换中水源的处理结果：存档转换的完成提示按它写水体一段（v4.12.0 整合计划 2.5）。</summary>
    public enum WaterSourceOutcome : byte
    {
        /// <summary>新版水源：一个不删，只把水体重置到海平面。</summary>
        Kept = 0,

        /// <summary>旧版水源：河流、湖泊与海水水源换算到新水源系统，旧式恒定海源删除，改由海平面代替。</summary>
        Upgraded = 1,

        /// <summary>旧版水源而地形高度数据未就绪：无法换算，全部删除。</summary>
        Removed = 2,
    }

    /// <summary>转换请求的阶段，按声明顺序推进。</summary>
    public enum ConversionPhase : byte
    {
        /// <summary>玩家已在对话框里确认，等闸门放行绑定的存档。</summary>
        Requested = 0,

        /// <summary>闸门已放行，存档正在加载。</summary>
        Loading = 1,

        /// <summary><c>VanillaSaveConversionSystem.OnGameLoaded</c> 正在转换。</summary>
        Converting = 2,

        /// <summary>转换完成，等待或正在自动存档。</summary>
        Saving = 3,

        /// <summary>新档已确认写入。</summary>
        Completed = 4,

        /// <summary>转换或存档失败，见 <see cref="ConversionRequest.FailedStep"/>。</summary>
        Failed = 5,

        /// <summary>
        /// 被否决：地形实际尺寸不是源尺寸，不执行任何步骤、不压暂停，城市按原样载入（v4.12.0 整合计划 2.1）。
        /// 不走"失败"：失败提示要玩家别继续玩，被否决的城市可以照常玩。
        /// </summary>
        Cancelled = 6,
    }

    /// <summary>
    /// 一次转换请求：玩家确认时生成，绑定源存档的资产 ID，按 <see cref="ConversionPhase"/> 推进。
    /// 取代原先的全局 <c>PendingConversion</c> 标志：标志不认存档，残留时会让下一次任意读档跳过校验，
    /// 新档也会被打上转换标签（审查报告 P0-3）；转换系统的"已执行"标志又只置真、不复位，同一会话的第二次转换被跳过（P0-2）。
    /// </summary>
    public sealed class ConversionRequest
    {
        internal ConversionRequest(ConversionKind kind, string sourceAssetId, int sourceCoreValue, int targetCoreValue,
            string displayName)
        {
            Id = Guid.NewGuid();
            Kind = kind;
            SourceAssetId = sourceAssetId;
            SourceCoreValue = sourceCoreValue;
            TargetCoreValue = targetCoreValue;
            DisplayName = displayName;
            Phase = ConversionPhase.Requested;
            RequestedAt = Time.realtimeSinceStartup;
        }

        public Guid Id { get; }

        public ConversionKind Kind { get; }

        /// <summary>源资产的 ID：存档为 <c>SaveInfo.id</c>，开新城为 <c>MapInfo.id</c>。闸门只对这个资产放行。</summary>
        public string SourceAssetId { get; }

        /// <summary>
        /// 源资产的模式值；无标记的原版档为 <see cref="VanillaConversionState.UntaggedVanillaCoreValue"/>，不折算成 1；
        /// 开新城为按面积推断的 1；28→57 为 2。
        /// </summary>
        public int SourceCoreValue { get; }

        /// <summary>源资产应有的地形尺寸（<c>playableArea.x</c>）：原版档与 CV=1 档都是 14336，CV=2 档是 28672。否决闸按它比对。</summary>
        public float SourceTerrainMapSize => Math.Max(SourceCoreValue, 1) * PatchManager.OriginalMapSize;

        public int TargetCoreValue { get; }

        /// <summary>源存档的显示名，开新城为城市名（为空时用地图名），用于新档命名与提示。</summary>
        public string DisplayName { get; }

        /// <summary>开新城转换：没有源存档，转换步骤与提示都不同。</summary>
        public bool IsNewGame => Kind == ConversionKind.VanillaMapNewGame;

        /// <summary>28→57 存档扩展：贴图数据来自 ModeB 区段，地形按 1/2 的比值合成。</summary>
        public bool IsSaveExpansion => Kind == ConversionKind.CoreValue2Save;

        public ConversionPhase Phase { get; internal set; }

        /// <summary>转换中正在执行的步骤，出错时记为 <see cref="FailedStep"/>。</summary>
        public string CurrentStep { get; internal set; }

        public string FailedStep { get; internal set; }

        /// <summary>失败详情（异常消息或存档结果），写进失败提示。</summary>
        public string FailureDetail { get; internal set; }

        /// <summary>水源步骤的处理结果，写进完成提示。</summary>
        public WaterSourceOutcome WaterSources { get; internal set; }

        /// <summary>28→57 地形合成时外围是否为平底（存档没有背景地形，或背景盖不住新地图），写进完成提示。</summary>
        public bool FlatPeriphery { get; internal set; }

        /// <summary>28→57 平底外围是否开了海道（原边界上有低于海平面的格子，整合计划 Q24），写进完成提示。</summary>
        public bool SeaChannels { get; internal set; }

        /// <summary>自动存档是否已排进任务队列，防止重复排队。</summary>
        internal bool SaveQueued { get; set; }

        /// <summary>转换失败的提示是否已弹过：失败发生在读档中途，提示等加载结束后再弹。</summary>
        internal bool FailureReported { get; set; }

        /// <summary>被否决时记录的地形实际尺寸，写进否决说明。</summary>
        internal float VetoedTerrainMapSize { get; set; }

        /// <summary>否决说明是否已弹过：否决发生在反序列化阶段，说明等加载结束后再弹。</summary>
        internal bool VetoReported { get; set; }

        internal float RequestedAt { get; }

        public override string ToString() =>
            $"{Id.ToString("N").Substring(0, 8)} {Kind} CV {SourceCoreValue}→{TargetCoreValue} '{DisplayName}' [{Phase}]";
    }

    /// <summary>
    /// 当前转换请求的持有者。由 <see cref="SaveEntryGate"/> 在确认对话框里发起、在放行时推进，
    /// 由 <see cref="VanillaSaveConversionSystem"/> 执行转换与自动存档。
    ///
    /// <para><b>清除时机</b>：闸门判定一次与本请求无关的加载、请求过期、回主菜单（Cleanup）、Mod 卸载。
    /// "保存中"的请求只由存档任务自己收尾，闸门不清。</para>
    /// </summary>
    public static class VanillaConversionState
    {
        private const string Tag = nameof(VanillaConversionState);

        // === Constants ===

        #region Constants

        /// <summary>无 MapExt 标记的原版存档的模式值哨兵。</summary>
        public const int UntaggedVanillaCoreValue = -1;

        /// <summary>"已请求"的有效期：确认后重新触发的加载会在几帧内到达闸门，过期的视为作废。</summary>
        private const float kRequestTimeoutSeconds = 60f;

        #endregion

        // === Fields ===

        #region Fields

        /// <summary>当前请求；没有时为 null。只在主线程读写。</summary>
        public static ConversionRequest Current { get; private set; }

        /// <summary>本次加载就是转换那一次：加载中、转换中或保存中。地下水的读档补场据此让路。</summary>
        public static bool IsConversionLoad =>
            Current != null && Current.Phase >= ConversionPhase.Loading && Current.Phase <= ConversionPhase.Saving;

        /// <summary>正在为转换结果存档。只有这时 <c>GetSaveInfo</c> 才打转换标签。</summary>
        public static bool IsSaving => Current != null && Current.Phase == ConversionPhase.Saving;

        /// <summary>
        /// 当前请求已被闸门放行过（加载中及之后的任何阶段）。在 <c>OnGameLoadingComplete</c> 里，它属于刚结束的这次加载：
        /// 自动存档排在加载结束之后才完成，已完成、失败、被否决的请求要到下一次加载开始才清除。
        /// </summary>
        public static bool HasRequestForCurrentLoad => Current != null && Current.Phase != ConversionPhase.Requested;

        #endregion

        // === Public API ===

        #region Public API

        /// <summary>玩家确认转换时调用：生成绑定源存档的新请求，取代残留的旧请求。</summary>
        public static ConversionRequest Begin(ConversionKind kind, string sourceAssetId, int sourceCoreValue,
            int targetCoreValue, string displayName)
        {
            if (Current != null) Clear("被新请求取代");
            Current = new ConversionRequest(kind, sourceAssetId, sourceCoreValue, targetCoreValue, displayName);
            ModLog.Info(Tag, $"新建转换请求：{Current}");
            return Current;
        }

        /// <summary>当前请求是否"已请求"、绑定这个资产且未过期。</summary>
        public static bool IsRequestedFor(string assetId)
        {
            ConversionRequest r = Current;
            return r != null && r.Phase == ConversionPhase.Requested && r.SourceAssetId == assetId &&
                   Time.realtimeSinceStartup - r.RequestedAt <= kRequestTimeoutSeconds;
        }

        /// <summary>闸门放行绑定的资产时调用：已请求 → 加载中。一个请求只消费一次。</summary>
        public static void MarkLoading(string assetId)
        {
            if (!IsRequestedFor(assetId)) return;
            Current.Phase = ConversionPhase.Loading;
            ModLog.Info(Tag, $"转换请求进入加载：{Current}");

            // 28→57：让这次读档重建序列化映射表，类型路由才会生效（整合计划 §1.8）
            if (Current.IsSaveExpansion) SaveExpansionTypeMapPatch.BeginLoad();
        }

        /// <summary>
        /// 闸门判定一次新的加载时调用：清除与这次加载无关的请求，包括已过期、已消费过、已结束的。
        /// "保存中"的请求不在这里清，由存档任务收尾。
        /// </summary>
        public static void DropStale(string assetId)
        {
            ConversionRequest r = Current;
            if (r == null || r.Phase == ConversionPhase.Saving || IsRequestedFor(assetId)) return;
            Clear("闸门判定的加载与本请求无关，或请求已过期、已结束");
        }

        /// <summary>
        /// 记录地形实际尺寸时调用（反序列化阶段，早于各系统的 <c>OnGameLoaded</c>）：加载中的请求要求地形是源尺寸，
        /// 不符就转为"已取消"，不执行任何步骤。地形已是扩展尺寸的地图若照样转换，会被降采样塞进中央（开新城计划 §5.1）。
        /// 这时地下水还没判断让路，被否决的加载照常补场。地形是正方形，只比 x。
        /// </summary>
        public static void VetoIfTerrainMismatch(float terrainMapSize)
        {
            ConversionRequest r = Current;
            if (r == null || r.Phase != ConversionPhase.Loading) return;

            float expected = r.SourceTerrainMapSize;
            if (Math.Abs(terrainMapSize - expected) < 1f) return;

            r.VetoedTerrainMapSize = terrainMapSize;
            r.Phase = ConversionPhase.Cancelled;
            ModLog.Warn(Tag, $"地形实际尺寸 {terrainMapSize} 不是转换要求的 {expected}，否决转换，按原样载入：{r}");
        }

        /// <summary>新一次加载开始时调用：清除已经完成、失败或被否决的请求。</summary>
        public static void ClearFinished(string reason)
        {
            ConversionRequest r = Current;
            if (r != null && (r.Phase == ConversionPhase.Completed || r.Phase == ConversionPhase.Failed ||
                              r.Phase == ConversionPhase.Cancelled))
                Clear(reason);
        }

        public static void Clear(string reason)
        {
            if (Current == null) return;
            ModLog.Info(Tag, $"清除转换请求（{reason}）：{Current}");
            Current = null;

            // 读档中途被清除（回主菜单、Mod 卸载）也要撤掉 28→57 的路由
            SaveExpansionTypeMapPatch.EndLoad($"转换请求清除：{reason}");
        }

        #endregion
    }
}
