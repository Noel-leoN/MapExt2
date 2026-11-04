// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Colossal;
using Colossal.Core;
using Colossal.UI.Binding;
using Game;
using Game.Assets;
using Game.City;
using Game.Prefabs.Modes;
using Game.SceneFlow;
using Game.UI;
using Game.UI.Localization;
using Game.UI.Menu;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using Unity.Entities;
using UnityEngine;

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>存档模式值的判定结果。</summary>
    public enum SaveValidationResult
    {
        /// <summary>可以加载，包括没有模式值标签的旧版存档在 57 km 模式下加载。</summary>
        Success,

        /// <summary>存档没有用过本 Mod（原版存档）。</summary>
        ModNotUsed,

        /// <summary>带模式值标签，但与当前模式不符。</summary>
        CoreValueMismatch,

        /// <summary>没有模式值标签的旧版存档，当前不是 57 km 模式。</summary>
        LegacyMismatch,
    }

    /// <summary>加载从哪个入口进来。</summary>
    internal enum SaveEntry
    {
        /// <summary>主菜单读档：<c>MenuUISystem.SafeLoadGame</c>。</summary>
        MenuLoad,

        /// <summary>主菜单与启动器的继续：<c>MenuUISystem.SafeContinueGame</c>。</summary>
        MenuContinue,

        /// <summary>主菜单开新城：<c>MenuUISystem.SafeNewGame</c>。</summary>
        MenuNewGame,

        /// <summary>游戏内快速读档：<c>MenuUISystem.SafeQuickLoad</c>。</summary>
        QuickLoad,

        /// <summary>汇合点 <c>GameManager.Load</c>：命令行、其他 Mod 的直接调用、菜单层失效。</summary>
        Convergence,
    }

    internal enum GateVerdict
    {
        Allow,
        Block,
        OfferConversion,
        ConfirmDegraded,

        /// <summary>原版地图在扩展模式下开新城会被拉伸：给"返回"与"仍要继续"（只在菜单层）。</summary>
        StretchWarning,
    }

    /// <summary>一次判定的结论。</summary>
    internal sealed class GateDecision
    {
        public GateVerdict Verdict { get; private set; }

        /// <summary>拦下时给玩家看的说明。</summary>
        public LocalizedString Message { get; private set; }

        public ConversionKind ConversionKind { get; private set; }

        public int SourceCoreValue { get; private set; }

        public int TargetCoreValue { get; private set; }

        /// <summary>没有模式值标签的旧版 MapExt 存档在 57 km 模式下放行：放行前给它补上标签。</summary>
        public bool IsLegacyMatch { get; private set; }

        public static GateDecision Allow(bool legacyMatch = false) =>
            new GateDecision { Verdict = GateVerdict.Allow, IsLegacyMatch = legacyMatch };

        public static GateDecision Block(LocalizedString message) =>
            new GateDecision { Verdict = GateVerdict.Block, Message = message };

        public static GateDecision Conversion(ConversionKind kind, int sourceCoreValue, int targetCoreValue) =>
            new GateDecision
            {
                Verdict = GateVerdict.OfferConversion,
                ConversionKind = kind,
                SourceCoreValue = sourceCoreValue,
                TargetCoreValue = targetCoreValue,
            };

        public static GateDecision Degraded() => new GateDecision { Verdict = GateVerdict.ConfirmDegraded };

        public static GateDecision Stretch(LocalizedString message) =>
            new GateDecision { Verdict = GateVerdict.StretchWarning, Message = message };
    }

    /// <summary>
    /// 存档入口闸门（v4.12.0 整合计划 §2.1、§2.2）：菜单各入口与 <c>GameManager.Load</c> 汇合点共用的判定与提示。
    ///
    /// <para><b>判定顺序</b>：转换请求 → 模式值（受"禁止读档校验"控制）→ 降级（不受该开关控制）。
    /// 模式不符的档直接说明原因；降级确认只对本来就会放行的加载弹出，转换在降级状态下一律不提供。
    /// 开新城的模式值按地图面积推断，原版地图在扩展模式下的拉伸警告不受任何开关控制（D3）。</para>
    ///
    /// <para><b>两层</b>：菜单层在原方法写入城市配置覆盖项、更新"上次存档名"之前判定，"仍要加载"与转换只在这一层提供，
    /// 确认后带着许可重新调用菜单方法。汇合点只拦不放，兜住命令行、其他 Mod 与菜单层失效的情况。
    /// 菜单层放行时按资产 ID 发一次性许可，汇合点见到许可直接放行，同一次加载不会判两次、弹两次框。</para>
    ///
    /// <para><b>启动阶段</b>：命令行 <c>startGame=</c> 读档被拒时引擎自己回主菜单；启动器"继续"不会，
    /// 拦下时要自己转入主菜单。这时界面还没就绪，对话框推迟到主菜单加载完成后再弹。</para>
    /// </summary>
    internal static class SaveEntryGate
    {
        private const string Tag = nameof(SaveEntryGate);

        // === Constants ===

        #region Constants

        /// <summary>没有模式值标签的旧版 MapExt 存档按 57 km（CV=4）处理。</summary>
        public const int LegacySaveCoreValue = 4;

        public const int NoneModeCoreValue = 1;

        /// <summary>存档 <c>modsEnabled</c> 里本 Mod 的标识前缀。</summary>
        private const string kModIdentifier = "MapExt";

        /// <summary>许可与"已确认降级""已确认拉伸"的有效期：重新触发的加载几帧内就会到达，过期的视为作废。</summary>
        private const float kPermitSeconds = 60f;

        /// <summary>
        /// 原版地图 529 个分块的总面积（14336²，约 2.0552E+08 m²）。<c>MapInfo.area</c> 是分块几何面积，
        /// 与它的比值等于 CV²（开新城计划 §3.2；地形起伏只带来约 0.006% 的漂移）。
        /// </summary>
        private const double kVanillaTileArea = (double)PatchManager.OriginalMapSize * PatchManager.OriginalMapSize;

        /// <summary>面积比值与 CV² 的相对容差：超过就判为面积不可信（开新城计划 §3.3）。</summary>
        private const double kAreaRatioTolerance = 0.05;

        #endregion

        // === Fields ===

        #region Fields

        private static readonly FieldInfo s_SavesBindingField = AccessTools.Field(typeof(MenuUISystem), "m_SavesBinding");
        private static readonly FieldInfo s_MapsBindingField = AccessTools.Field(typeof(MenuUISystem), "m_MapsBinding");

        private static string s_PermitAssetId;
        private static float s_PermitUntil;

        private static string s_DegradedAckAssetId;
        private static float s_DegradedAckUntil;

        private static string s_StretchAckAssetId;
        private static float s_StretchAckUntil;

        /// <summary>菜单层对这张地图提示过拉伸、玩家选了"仍要继续"：汇合点放行它时转为 <see cref="StretchPromptedForCurrentLoad"/>。</summary>
        private static string s_StretchPromptedMapId;

        #endregion

        // === Public API ===

        #region Public API

        /// <summary>还在启动阶段：主菜单尚未加载，<c>gameMode</c> 仍是初始值 <c>Other</c>。</summary>
        public static bool IsBooting => GameManager.instance != null && GameManager.instance.gameMode == GameMode.Other;

        /// <summary>判定一次读档（读档、继续、快速读档、汇合点）。</summary>
        public static GateDecision EvaluateSave(SaveEntry entry, SaveInfo save)
        {
            if (VanillaConversionState.IsRequestedFor(save.id))
            {
                ModLog.Info(Tag, $"{entry} '{save.displayName}'：转换请求绑定的存档，放行");
                return GateDecision.Allow();
            }

            VanillaConversionState.DropStale(save.id);

            int currentCV = PatchManager.CurrentCoreValue;
            bool legacyMatch = false;
            if (Mod.Instance?.CurrentSettings?.DisableLoadGameValidation != true)
            {
                SaveValidationResult result =
                    ValidateSave(save, currentCV, out Dictionary<string, ILocElement> locParams, out int savedCV, out legacyMatch);
                // 带上存档 ID：命令行 -startGame= 用的就是它
                ModLog.Info(Tag, $"{entry} '{save.displayName}'（ID {save.id}）：存档 {DescribeCoreValue(savedCV)}，当前 CV={currentCV}，结果 {result}");

                switch (result)
                {
                    case SaveValidationResult.ModNotUsed:
                        if (!CanConvertTo(currentCV))
                            return GateDecision.Block(new LocalizedString("LOAD_VALIDATION.ModNotUsed", null, locParams));
                        if (!IsConversionEnabled)
                            return GateDecision.Block(LocalizedString.Id("VANILLA_CONVERT.EnableFirst"));
                        return IsDegradedForExpandedLoad
                            ? GateDecision.Block(DegradedBlockedMessage())
                            : GateDecision.Conversion(ConversionKind.VanillaSave, VanillaConversionState.UntaggedVanillaCoreValue, currentCV);

                    case SaveValidationResult.CoreValueMismatch:
                        if (savedCV == NoneModeCoreValue && CanConvertTo(currentCV) && IsConversionEnabled)
                        {
                            return IsDegradedForExpandedLoad
                                ? GateDecision.Block(DegradedBlockedMessage())
                                : GateDecision.Conversion(ConversionKind.CoreValue1Save, NoneModeCoreValue, currentCV);
                        }

                        return GateDecision.Block(new LocalizedString("LOAD_VALIDATION.Mismatch", null, locParams));

                    case SaveValidationResult.LegacyMismatch:
                        return GateDecision.Block(new LocalizedString("LOAD_VALIDATION.LegacyMismatch", null, locParams));
                }
            }

            if (IsDegradedForExpandedLoad && !ConsumeDegradedAck(save.id))
            {
                ModLog.Warn(Tag, $"{entry} '{save.displayName}'：本会话降级（{MapExtHealth.ComponentSummary}），需要玩家确认");
                return GateDecision.Degraded();
            }

            return GateDecision.Allow(legacyMatch);
        }

        /// <summary>
        /// 判定一次开新城（v4.12.0 整合计划 §2.1 第 4 条）：转换请求 → 面积推断的制作模式 → 降级。
        /// <list type="bullet">
        /// <item>原版地图而当前为扩展模式：拉伸是事实警告，不受任何开关影响（D3）。降级时直接说明并拦下（Q13）；
        /// 汇合点只拦；菜单层在 ModeA／ModeB 且启用转换时给"转换／取消"，其余给"返回"与"仍要继续"；</item>
        /// <item>推断模式与当前模式不一致（包括 None 模式开扩展地图）：按模式不符拦下，受"禁止读档校验"控制（Q12）；</item>
        /// <item>面积不可信，或推断出的模式值不属于任何现行模式（Q15）：放行，载入后按地形实际尺寸判。</item>
        /// </list>
        /// 降级确认只对会放行的加载弹出。
        /// </summary>
        public static GateDecision EvaluateNewGame(SaveEntry entry, MapInfo map)
        {
            // 确认转换后重新触发的那一次：不认请求的话会再判成原版地图、再弹转换框（§1.7）
            if (VanillaConversionState.IsRequestedFor(map.id))
            {
                ModLog.Info(Tag, $"{entry} 地图 '{map.displayName}'：转换请求绑定的地图，放行");
                return GateDecision.Allow();
            }

            VanillaConversionState.DropStale(map.id);

            int currentCV = PatchManager.CurrentCoreValue;
            bool trusted = InferMapCoreValue(map.area, out double ratio, out int mapCV);
            ModLog.Info(Tag, $"{entry} 地图 '{map.displayName}'（ID {map.id}）：面积 {map.area:E4}，比值 {ratio:0.###}，" +
                             $"推断 CV={mapCV}（{(trusted ? "可信" : "不可信，放行，载入后按地形实际尺寸判")}），当前 CV={currentCV}");

            if (trusted && mapCV != currentCV)
            {
                if (mapCV == NoneModeCoreValue)
                {
                    // 原版地图、当前为扩展模式：地形会被拉伸
                    if (IsDegradedForExpandedLoad)
                    {
                        ModLog.Warn(Tag, $"{entry} 原版地图 '{map.displayName}'：本会话降级（{MapExtHealth.ComponentSummary}），拦下");
                        return GateDecision.Block(new LocalizedString("SAVE_GATE.DegradedVanillaMap", null, MapParams(map, currentCV)));
                    }

                    if (entry == SaveEntry.Convergence)
                    {
                        ModLog.Warn(Tag, $"{entry} 原版地图 '{map.displayName}'：会按 CV={currentCV} 拉伸，汇合点只拦不放");
                        return GateDecision.Block(new LocalizedString("SAVE_GATE.StretchBlocked", null, MapParams(map, currentCV)));
                    }

                    if (CanConvertTo(currentCV) && IsConversionEnabled)
                    {
                        ModLog.Info(Tag, $"{entry} 原版地图 '{map.displayName}'：可转换到 CV={currentCV}，弹转换确认");
                        return GateDecision.Conversion(ConversionKind.VanillaMapNewGame, NoneModeCoreValue, currentCV);
                    }

                    if (!ConsumeStretchAck(map.id))
                    {
                        ModLog.Warn(Tag, $"{entry} 原版地图 '{map.displayName}'：会按 CV={currentCV} 拉伸，弹拉伸警告");
                        string key = CanConvertTo(currentCV) ? "SAVE_GATE.StretchMessageConvertOff" : "SAVE_GATE.StretchMessage";
                        return GateDecision.Stretch(new LocalizedString(key, null, MapParams(map, currentCV)));
                    }

                    // 玩家刚对这张地图选了"仍要继续"：放行，记下菜单层已提示过（载入后只推通知条，Q11）
                    s_StretchPromptedMapId = map.id;
                    ModLog.Warn(Tag, $"{entry} 原版地图 '{map.displayName}'：玩家已确认仍要继续，按 CV={currentCV} 拉伸");
                }
                else if (Mod.Instance?.CurrentSettings?.DisableLoadGameValidation != true)
                {
                    ModLog.Info(Tag, $"{entry} 地图 '{map.displayName}'：为 CV={mapCV} 制作，与当前模式不符，拦下");
                    var locParams = MapParams(map, currentCV);
                    locParams.Add("MAP_MODE", LocalizedString.Value(PatchManager.GetModeNameForCoreValue(mapCV)));
                    return GateDecision.Block(new LocalizedString("SAVE_GATE.MapMismatch", null, locParams));
                }
                else
                {
                    ModLog.Info(Tag, $"{entry} 地图 '{map.displayName}'：模式不符，但已禁止读档校验，放行");
                }
            }

            if (IsDegradedForExpandedLoad && !ConsumeDegradedAck(map.id))
            {
                ModLog.Warn(Tag, $"{entry} 地图 '{map.displayName}'：本会话降级（{MapExtHealth.ComponentSummary}），需要玩家确认");
                return GateDecision.Degraded();
            }

            return GateDecision.Allow();
        }

        /// <summary>
        /// 菜单层按判定结论行事。返回 true 表示放行原方法（已发许可）；false 表示已拦下并弹框。
        /// 转换与"仍要加载"在确认后调用 <paramref name="retrigger"/>，重新走一遍菜单方法。
        /// 快速读档不提供转换，只说明。<paramref name="displayName"/> 用于新档命名，为空时取存档的显示名。
        /// </summary>
        public static bool HandleAtMenu(SaveEntry entry, GateDecision decision, string assetId, SaveInfo save, Action retrigger,
            string displayName = null)
        {
            switch (decision.Verdict)
            {
                case GateVerdict.Allow:
                    ApplyAllow(decision, save);
                    GrantPermit(assetId);
                    return true;

                case GateVerdict.Block:
                    ShowMessage(decision.Message);
                    return false;

                case GateVerdict.OfferConversion:
                    if (entry == SaveEntry.QuickLoad)
                    {
                        ShowMessage(LocalizedString.Id("SAVE_GATE.ConvertFromMenu"));
                        return false;
                    }

                    ShowConversionConfirm(assetId, displayName ?? save?.displayName ?? save?.cityName ?? "UnknownCity",
                        decision, retrigger);
                    return false;

                case GateVerdict.ConfirmDegraded:
                    ShowDegradedConfirm(assetId, retrigger);
                    return false;

                case GateVerdict.StretchWarning:
                    ShowStretchConfirm(assetId, decision.Message, retrigger);
                    return false;

                default:
                    return true;
            }
        }

        /// <summary>放行前的收尾：没有标签的旧版存档补上模式值标签。</summary>
        public static void ApplyAllow(GateDecision decision, SaveInfo save)
        {
            if (!decision.IsLegacyMatch || save == null) return;

            var mods = new List<string>(save.modsEnabled ?? Array.Empty<string>());
            mods.Add(MetaDataExtenderPatch.CoreValueKeyPrefix + LegacySaveCoreValue);
            save.modsEnabled = mods.ToArray();
            ModLog.Ok(Tag, $"旧版存档 '{save.displayName}' 匹配当前模式，已标记 CoreValue: {LegacySaveCoreValue}");
        }

        /// <summary>
        /// 汇合点拦下一次加载：复位菜单已写入的城市配置覆盖项，给出说明；启动器"继续"这类启动阶段的加载还要转入主菜单。
        /// 只拦不放，不提供"仍要加载"与转换。
        /// </summary>
        public static void BlockAtConvergence(GateDecision decision)
        {
            ResetMenuOverrides();

            LocalizedString message;
            switch (decision.Verdict)
            {
                case GateVerdict.OfferConversion:
                    message = LocalizedString.Id("SAVE_GATE.ConvertFromMenu");
                    break;
                case GateVerdict.ConfirmDegraded:
                    message = DegradedBlockedMessage();
                    break;
                default:
                    message = decision.Message;
                    break;
            }

            ShowMessage(message);

            // 命令行 startGame= 被拒时引擎自己回主菜单（GameManager.Initialize），其余启动阶段的加载要自己转
            if (IsBooting && !GameManager.instance.configuration.startGame.isValid) RequestMainMenu();
        }

        /// <summary>菜单层放行时发一次性许可，汇合点见到就直接放行。</summary>
        public static void GrantPermit(string assetId)
        {
            s_PermitAssetId = assetId;
            s_PermitUntil = Time.realtimeSinceStartup + kPermitSeconds;
        }

        /// <summary>汇合点消费许可：资产 ID 相符且未过期时返回 true，并作废这张许可。</summary>
        public static bool TryConsumePermit(string assetId)
        {
            bool valid = assetId != null && assetId == s_PermitAssetId && Time.realtimeSinceStartup <= s_PermitUntil;
            if (valid) s_PermitAssetId = null;
            return valid;
        }

        /// <summary>
        /// 本次游戏加载的拉伸已在菜单层提示过、玩家选了"仍要继续"：载入后只推通知条，不再弹框（Q11）。
        /// 汇合点每次判定游戏加载时重设，到 <c>OnGameLoadingComplete</c> 时指的就是这次加载。
        /// </summary>
        public static bool StretchPromptedForCurrentLoad { get; private set; }

        /// <summary>汇合点开始判定一次游戏模式的读档或开新城：清掉上一次加载的标记。</summary>
        public static void BeginGameLoad() => StretchPromptedForCurrentLoad = false;

        /// <summary>汇合点凭许可放行一张地图：菜单层对它提示过拉伸的，记到本次加载上。</summary>
        public static void NotePermittedMapLoad(string mapId)
        {
            StretchPromptedForCurrentLoad = mapId != null && mapId == s_StretchPromptedMapId;
            s_StretchPromptedMapId = null;
        }

        /// <summary>
        /// 启动阶段拦下加载后转入主菜单：排进原版存读档队列，等当前任务结束后再加载。
        /// 启动器"继续"只触发界面事件就返回，引擎之后不会自己加载主菜单。
        /// 不设同名任务上限（cap）：汇合点拦下时正处在"继续"的读档任务里，传 1 会被静默丢掉。
        /// </summary>
        public static void RequestMainMenu()
        {
            ModLog.Info(Tag, "启动阶段的加载已拦下，转入主菜单");
            TaskManager.instance.EnqueueTask(SaveHelpers.kSaveLoadTaskName, () => GameManager.instance.MainMenu());
        }

        /// <summary>
        /// 复位菜单在调用 <c>GameManager.Load</c> 之前写入的覆盖项。它们要到下一次读档才会被消耗，
        /// 拦下后不复位，被取消的开局选项、主题与游戏模式会套到下一次读档上。
        /// </summary>
        public static void ResetMenuOverrides()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            CityConfigurationSystem city = world.GetExistingSystemManaged<CityConfigurationSystem>();
            if (city != null)
            {
                city.overrideLoadedOptions = false;
                city.overrideThemeName = null;
            }

            GameModeSystem gameMode = world.GetExistingSystemManaged<GameModeSystem>();
            if (gameMode != null) gameMode.overrideMode = null;

            ModLog.Info(Tag, "已复位菜单写入的覆盖项（开局选项、主题、游戏模式）");
        }

        /// <summary>按 ID 在主菜单的存档列表里找存档。取不到列表时记 Error 并返回 null，交给汇合点判定（P0-6）。</summary>
        public static SaveInfo FindMenuSave(MenuUISystem menu, string saveId)
        {
            ValueBinding<List<SaveInfo>> binding = ReadMenuBinding<SaveInfo>(menu, s_SavesBindingField, "m_SavesBinding");
            if (binding == null) return null;

            SaveInfo save = binding.value?.Find(s => s.id == saveId);
            if (save == null) ModLog.Warn(Tag, $"存档列表里找不到 ID {saveId}，交给汇合点判定");
            return save;
        }

        /// <summary>按 ID 在主菜单的地图列表里找地图。取不到列表时记 Error 并返回 null，交给汇合点判定。</summary>
        public static MapInfo FindMenuMap(MenuUISystem menu, string mapId)
        {
            ValueBinding<List<MapInfo>> binding = ReadMenuBinding<MapInfo>(menu, s_MapsBindingField, "m_MapsBinding");
            if (binding == null) return null;

            MapInfo map = binding.value?.Find(m => m.id == mapId);
            if (map == null) ModLog.Warn(Tag, $"地图列表里找不到 ID {mapId}，交给汇合点判定");
            return map;
        }

        #endregion

        // === Helpers ===

        #region Helpers

        /// <summary>本会话降级且当前为扩展模式：这时加载会把错误尺寸的数据写进存档。</summary>
        private static bool IsDegradedForExpandedLoad =>
            MapExtHealth.IsDegraded && PatchManager.CurrentCoreValue > NoneModeCoreValue;

        private static bool IsConversionEnabled => Mod.Instance?.CurrentSettings?.EnableVanillaConversion == true;

        /// <summary>转换只支持 57 km（CV=4）与 28 km（CV=2）。</summary>
        private static bool CanConvertTo(int coreValue) => coreValue == 4 || coreValue == 2;

        /// <summary>
        /// 存档的模式值判定。<paramref name="savedCV"/>：原版存档为 <see cref="VanillaConversionState.UntaggedVanillaCoreValue"/>，
        /// 没有标签的旧版 MapExt 存档为 <see cref="LegacySaveCoreValue"/>，标签解析失败为 0。
        /// </summary>
        private static SaveValidationResult ValidateSave(SaveInfo save, int currentCV,
            out Dictionary<string, ILocElement> locParams, out int savedCV, out bool legacyMatch)
        {
            locParams = new Dictionary<string, ILocElement>();
            legacyMatch = false;
            string[] mods = save.modsEnabled;

            // 1. 存档里没有本 Mod：原版存档，只有 None 模式能直接读
            if (mods == null || !mods.Any(m => m.StartsWith(kModIdentifier, StringComparison.OrdinalIgnoreCase)))
            {
                savedCV = VanillaConversionState.UntaggedVanillaCoreValue;
                if (currentCV == NoneModeCoreValue) return SaveValidationResult.Success;

                locParams.Add("NONE_MODE", LocalizedString.Value(PatchManager.GetModeNameForCoreValue(NoneModeCoreValue)));
                return SaveValidationResult.ModNotUsed;
            }

            // 2. 用过本 Mod 但没有模式值标签：旧版存档，按 57 km 处理
            string entry = mods.FirstOrDefault(m => m.StartsWith(MetaDataExtenderPatch.CoreValueKeyPrefix));
            if (string.IsNullOrEmpty(entry))
            {
                savedCV = LegacySaveCoreValue;
                if (currentCV == LegacySaveCoreValue)
                {
                    legacyMatch = true;
                    return SaveValidationResult.Success;
                }

                locParams.Add("LEGACY_MODE", LocalizedString.Value(PatchManager.GetModeNameForCoreValue(LegacySaveCoreValue)));
                return SaveValidationResult.LegacyMismatch;
            }

            // 3. 带标签：与当前模式比较；解析失败也算不符
            if (!int.TryParse(entry.Substring(MetaDataExtenderPatch.CoreValueKeyPrefix.Length), out savedCV))
            {
                ModLog.Warn(Tag, $"无法解析模式值标签 '{entry}'，按不符处理");
                savedCV = 0;
            }

            if (savedCV == currentCV) return SaveValidationResult.Success;

            locParams.Add("SAVED_MODE", LocalizedString.Value(PatchManager.GetModeNameForCoreValue(savedCV)));
            locParams.Add("CURRENT_MODE", LocalizedString.Value(PatchManager.GetModeNameForCoreValue(currentCV)));
            return SaveValidationResult.CoreValueMismatch;
        }

        private static string DescribeCoreValue(int savedCV) =>
            savedCV == VanillaConversionState.UntaggedVanillaCoreValue ? "无 MapExt 标记（原版）" : $"CV={savedCV}";

        /// <summary>
        /// 按 <c>MapInfo.area</c> 推断地图的制作模式（开新城计划 §3.3 的守卫）。返回是否可信：
        /// 比值要接近某个整数的平方，且这个整数是现行模式之一（Q15：面积反映的是分块而不是地形，
        /// 比值 256 的图按 CV=16 会在所有现行模式下被拦，其中几张文件名写着 57km）。
        /// </summary>
        private static bool InferMapCoreValue(float area, out double ratio, out int mapCV)
        {
            ratio = area / kVanillaTileArea;
            mapCV = (int)Math.Round(Math.Sqrt(Math.Max(ratio, 0d)));
            double square = (double)mapCV * mapCV;
            return mapCV >= 1 && Math.Abs(ratio - square) <= kAreaRatioTolerance * square && PatchManager.IsKnownCoreValue(mapCV);
        }

        private static Dictionary<string, ILocElement> MapParams(MapInfo map, int currentCV) =>
            new Dictionary<string, ILocElement>
            {
                { "MAP_NAME", LocalizedString.Value(map.displayName ?? map.id ?? "?") },
                { "CURRENT_MODE", LocalizedString.Value(PatchManager.GetModeNameForCoreValue(currentCV)) },
                { "COMPONENTS", LocalizedString.Value(MapExtHealth.ComponentSummary) },
            };

        private static void AcknowledgeStretch(string assetId)
        {
            s_StretchAckAssetId = assetId;
            s_StretchAckUntil = Time.realtimeSinceStartup + kPermitSeconds;
        }

        /// <summary>玩家刚对这张地图选了"仍要继续"：放行一次，并作废这次确认。</summary>
        private static bool ConsumeStretchAck(string assetId)
        {
            bool valid = assetId != null && assetId == s_StretchAckAssetId && Time.realtimeSinceStartup <= s_StretchAckUntil;
            if (valid) s_StretchAckAssetId = null;
            return valid;
        }

        private static void AcknowledgeDegraded(string assetId)
        {
            s_DegradedAckAssetId = assetId;
            s_DegradedAckUntil = Time.realtimeSinceStartup + kPermitSeconds;
        }

        /// <summary>玩家刚对这个资产选了"仍要加载"：放行一次，并作废这次确认。</summary>
        private static bool ConsumeDegradedAck(string assetId)
        {
            bool valid = assetId != null && assetId == s_DegradedAckAssetId && Time.realtimeSinceStartup <= s_DegradedAckUntil;
            if (valid) s_DegradedAckAssetId = null;
            return valid;
        }

        private static ValueBinding<List<T>> ReadMenuBinding<T>(MenuUISystem menu, FieldInfo field, string name)
        {
#if DEBUG
            // 故障注入（DEBUG 自检）：假装取不到菜单列表，验证交给汇合点判定的路径（验收 L8）
            if (MapExtHealth.IsFaultInjected("SaveGate/" + name))
            {
                ModLog.Error(Tag, $"故障注入：假装取不到 MenuUISystem.{name}，交给汇合点判定");
                return null;
            }
#endif
            var binding = field?.GetValue(menu) as ValueBinding<List<T>>;
            if (binding == null) ModLog.Error(Tag, $"取不到 MenuUISystem.{name}（游戏更新后改名？），本次交给汇合点判定");
            return binding;
        }

        private static LocalizedString DegradedBlockedMessage() =>
            new LocalizedString("SAVE_GATE.DegradedBlocked", null, ComponentParams());

        private static Dictionary<string, ILocElement> ComponentParams() =>
            new Dictionary<string, ILocElement> { { "COMPONENTS", LocalizedString.Value(MapExtHealth.ComponentSummary) } };

        private static void ShowMessage(LocalizedString message)
        {
            var dialog = new MessageDialog(LocalizedString.Id("LOAD_VALIDATION.TitleError"), message,
                LocalizedString.Id("LOAD_VALIDATION.ConfirmOK"));
            ShowWhenUiReady(() => GameManager.instance.userInterface.appBindings.ShowMessageDialog(dialog, null));
        }

        /// <summary>
        /// 转换确认：确认后新建绑定这个资产（存档或地图）的请求，再重新触发菜单方法。
        /// 开新城另用一套文案：没有源存档，分块只解锁中央 9 格，外部连接保留。
        /// </summary>
        private static void ShowConversionConfirm(string assetId, string displayName, GateDecision decision, Action retrigger)
        {
            string targetMode = PatchManager.GetModeNameForCoreValue(decision.TargetCoreValue);
            var locParams = new Dictionary<string, ILocElement> { { "TARGET_MODE", LocalizedString.Value(targetMode) } };
            bool newGame = decision.ConversionKind == ConversionKind.VanillaMapNewGame;
            var dialog = new ConfirmationDialog(
                LocalizedString.Id("VANILLA_CONVERT.Title"),
                new LocalizedString(newGame ? "VANILLA_CONVERT.NewGameMessage" : "VANILLA_CONVERT.Message", null, locParams),
                LocalizedString.Id(newGame ? "VANILLA_CONVERT.NewGameConfirm" : "VANILLA_CONVERT.Confirm"),
                LocalizedString.Id("VANILLA_CONVERT.Cancel"));

            ShowWhenUiReady(() => GameManager.instance.userInterface.appBindings.ShowConfirmationDialog(dialog, result =>
            {
                // 只有确认按钮（0）才转换；关闭对话框等其他结果一律当作取消
                if (result != 0)
                {
                    ModLog.Info(Tag, $"玩家取消了转换 '{displayName}'");
                    return;
                }

                ModLog.Ok(Tag, $"玩家确认转换 '{displayName}' → {targetMode}");
                VanillaConversionState.Begin(decision.ConversionKind, assetId, decision.SourceCoreValue,
                    decision.TargetCoreValue, displayName);
                Retrigger(retrigger, "确认转换");
            }));
        }

        /// <summary>降级确认：危险的"仍要加载"放在确认按钮上，关闭对话框等其他结果一律当作返回。</summary>
        private static void ShowDegradedConfirm(string assetId, Action retrigger)
        {
            var dialog = new ConfirmationDialog(
                LocalizedString.Id("SAVE_GATE.DegradedTitle"),
                new LocalizedString("SAVE_GATE.DegradedMessage", null, ComponentParams()),
                LocalizedString.Id("SAVE_GATE.DegradedLoadAnyway"),
                LocalizedString.Id("SAVE_GATE.DegradedBack"));

            ShowWhenUiReady(() => GameManager.instance.userInterface.appBindings.ShowConfirmationDialog(dialog, result =>
            {
                if (result != 0)
                {
                    ModLog.Info(Tag, "降级状态下玩家选择返回");
                    return;
                }

                ModLog.Warn(Tag, $"降级状态下玩家选择仍要加载（{MapExtHealth.ComponentSummary}）");
                AcknowledgeDegraded(assetId);
                Retrigger(retrigger, "降级确认");
            }));
        }

        /// <summary>拉伸警告：危险的"仍要继续"放在确认按钮上，关闭对话框等其他结果一律当作返回。</summary>
        private static void ShowStretchConfirm(string mapId, LocalizedString message, Action retrigger)
        {
            var dialog = new ConfirmationDialog(
                LocalizedString.Id("SAVE_GATE.StretchTitle"),
                message,
                LocalizedString.Id("SAVE_GATE.StretchContinue"),
                LocalizedString.Id("SAVE_GATE.StretchBack"));

            ShowWhenUiReady(() => GameManager.instance.userInterface.appBindings.ShowConfirmationDialog(dialog, result =>
            {
                if (result != 0)
                {
                    ModLog.Info(Tag, "拉伸警告：玩家选择返回");
                    return;
                }

                ModLog.Warn(Tag, "拉伸警告：玩家选择仍要继续");
                AcknowledgeStretch(mapId);
                Retrigger(retrigger, "拉伸确认");
            }));
        }

        private static void Retrigger(Action retrigger, string reason)
        {
            try
            {
                retrigger();
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, $"{reason}后重新触发加载失败");
                VanillaConversionState.Clear("重新触发加载失败");
            }
        }

        /// <summary>
        /// 界面就绪就立刻弹；启动阶段（主菜单还没加载）等主菜单加载完成后再弹，
        /// 并延后一帧，排在其他 Mod 的加载完成对话框之后。
        /// </summary>
        private static void ShowWhenUiReady(Action show)
        {
            GameManager gm = GameManager.instance;
            if (gm == null) return;

            if (gm.gameMode != GameMode.Other)
            {
                Show(show);
                return;
            }

            GameManager.EventGamePreload handler = null;
            handler = (purpose, mode) =>
            {
                if (mode != GameMode.MainMenu) return;
                gm.onGameLoadingComplete -= handler;
                MainThreadDispatcher.RegisterUpdater(() =>
                {
                    Show(show);
                    return true;
                });
            };
            gm.onGameLoadingComplete += handler;
        }

        private static void Show(Action show)
        {
            try
            {
                show();
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"显示对话框失败：{ex.Message}");
            }
        }

        #endregion
    }
}
