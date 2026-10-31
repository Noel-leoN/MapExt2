// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using System;
using MapExtPDX.MapExt.Core;
using UnityEngine;

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>转换请求的来源。开新城转换（Phase 2）与 28→57（Phase 3）在这里加值。</summary>
    public enum ConversionKind : byte
    {
        /// <summary>没有 MapExt 标记的原版存档。</summary>
        VanillaSave = 0,

        /// <summary>MapExt 在 None 模式（CV=1）下保存的存档。</summary>
        CoreValue1Save = 1,
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

        /// <summary>源存档的 <c>SaveInfo.id</c>。闸门只对这个资产放行。</summary>
        public string SourceAssetId { get; }

        /// <summary>源存档的模式值；无标记的原版档为 <see cref="VanillaConversionState.UntaggedVanillaCoreValue"/>，不折算成 1。</summary>
        public int SourceCoreValue { get; }

        public int TargetCoreValue { get; }

        /// <summary>源存档的显示名，用于新档命名与提示。</summary>
        public string DisplayName { get; }

        public ConversionPhase Phase { get; internal set; }

        /// <summary>转换中正在执行的步骤，出错时记为 <see cref="FailedStep"/>。</summary>
        public string CurrentStep { get; internal set; }

        public string FailedStep { get; internal set; }

        /// <summary>失败详情（异常消息或存档结果），写进失败提示。</summary>
        public string FailureDetail { get; internal set; }

        /// <summary>自动存档是否已排进任务队列，防止重复排队。</summary>
        internal bool SaveQueued { get; set; }

        /// <summary>转换失败的提示是否已弹过：失败发生在读档中途，提示等加载结束后再弹。</summary>
        internal bool FailureReported { get; set; }

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

        /// <summary>新一次加载开始时调用：清除已经完成或失败的请求。</summary>
        public static void ClearFinished(string reason)
        {
            ConversionRequest r = Current;
            if (r != null && (r.Phase == ConversionPhase.Completed || r.Phase == ConversionPhase.Failed)) Clear(reason);
        }

        public static void Clear(string reason)
        {
            if (Current == null) return;
            ModLog.Info(Tag, $"清除转换请求（{reason}）：{Current}");
            Current = null;
        }

        #endregion
    }
}
