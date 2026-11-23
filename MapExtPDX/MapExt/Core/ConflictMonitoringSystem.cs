// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Serialization.Entities;
using Game;
using Game.Areas;
using Game.Net;
using Game.PSI;
using Game.Rendering;
using Game.SceneFlow;
using Game.Simulation;
using Game.UI;
using Game.UI.Localization;
using MapExtPDX.MapExt.MapSizePatchSet;
using MapExtPDX.SaveLoadSystem;
using Unity.Entities;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// 冲突诊断与运行期低频状态监控系统：
    /// <list type="bullet">
    /// <item>每次进入城市（<see cref="OnGameLoadingComplete"/>）或点击 RefreshStatus 时执行完整三层诊断（系统 <c>Enabled</c> 状态与未知双跑、本会话 Harmony 补丁存活／重叠、<c>CellMapSystem&lt;T&gt;.kMapSize == 14336</c> 回读）；</item>
    /// <item>在 <see cref="OnUpdate"/> 中每 240 帧执行一次轻量缓存指针轮询（仅覆盖本会话被禁用的原版系统、扩展模式 18 个模拟核心宿主、Demand 宿主与 MapExt 替换系统；不含工具／UI／音频宿主）；</item>
    /// <item>原版系统被外部周期性重开时将其设回禁用并按需查询内存 IL 归因缓存（不宣称“已修复/已解决”）；模拟宿主或替换系统掉为 <c>false</c> 时<b>只报告不自动重开</b>，避免违背系统自身内部停用逻辑。</item>
    /// </list>
    /// <para>冲突结论日志的开头被 MapExTools 的 <c>MapExtLogSummary</c> 逐字匹配，改这些措辞时同步那边的前缀与单测。</para>
    /// </summary>
    public partial class ConflictMonitoringSystem : GameSystemBase
    {
        private const string Tag = nameof(ConflictMonitoringSystem);

        #region Constants

        /// <summary>地图尺寸模式不符的通知条。</summary>
        private const string kMismatchNotificationId = "mapext.mapsize_mismatch";

        /// <summary>原版地形被拉伸的通知条。</summary>
        private const string kStretchNotificationId = "mapext.vanilla_stretch";

        /// <summary>MainLoop 低频状态轮询帧间隔（120–300 帧区间取 240 帧，约 2–4 秒一次）。</summary>
        private const int kPollFrameInterval = 240;

        /// <summary>轮询发现原版系统被重新启用、层 1 又没查到调用方时的说明。</summary>
        private const string kNoMutatorReEnabled = "未在已加载模组方法体中检出直接 set_Enabled 调用（可能经反射或间接调用重新启用）";

        /// <summary>轮询发现应保持启用的系统被停用、层 1 又没查到调用方时的说明。</summary>
        private const string kNoMutatorDisabled = "未在已加载模组方法体中检出直接 set_Enabled 调用（可能由系统内部逻辑自停或反射调用触发）";

        #endregion

        #region Fields

        private struct CachedSystemRef
        {
            public ComponentSystemBase System;
            public Type SystemType;
            public string Group;
            public bool IsCriticalCellMap;
        }

        /// <summary>是否已执行过首次诊断</summary>
        private bool _initialCheckDone = false;

        /// <summary>本游戏会话是否已完成单次未知替换双跑核查（进程内已加载程序集不变，无需每次进城重复核查）</summary>
        private bool _sessionDualRunChecked = false;

        /// <summary>是否已显示过 CellMap 弹窗（避免重复弹窗）</summary>
        private bool _cellMapDialogShown = false;

        private int _pollFrameCounter = 0;
        private bool _pollCacheReady = false;
        private readonly List<CachedSystemRef> _monitoredVanillaDisabled = new List<CachedSystemRef>();
        private readonly List<CachedSystemRef> _monitoredExpectedEnabled = new List<CachedSystemRef>();
        private readonly HashSet<Type> _reportedRuntimeAnomalies = new HashSet<Type>();

        #endregion

        protected override void OnCreate()
        {
            base.OnCreate();
            ModLog.Ok(Tag, "ConflictMonitoringSystem 已创建（读档三层诊断 + 低频状态轮询模式）");
        }

        /// <summary>
        /// 场景切换守卫：在返回主菜单（Cleanup）时重置所有 PatchSet 的静态会话状态。
        /// 根因：返回主菜单时原版引擎会释放/重建资源（Terrain Buffers、CellMap 数据等），
        /// 但 Harmony patch 中的 static 标志不会自动重置，导致二次加载跳过关键初始化。
        /// </summary>
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            // 这两条通知说的是上一次加载的地图；原版加载时不清通知条，不撤下会留到另一座城市
            PopNotification(kMismatchNotificationId);
            PopNotification(kStretchNotificationId);

            if (purpose == Purpose.Cleanup)
            {
                ModLog.Info(Tag, "检测到场景清理（Cleanup），重置所有 PatchSet 会话状态...");
                try
                {
                    // PatchSet1: Terrain StructuredBuffer 扩容标志
                    TerrainSystemPatches.ResetSessionState();
                    // PatchSet1: 级联渲染降频快照
                    TerrainSystem_RenderCascades_Patch.ResetSessionState();
                    // PatchSet1: 背景地形降採樣門控（dirty latch + 載入強制放行計數）
                    TerrainSystem_DownSampleHeightMap_Gate.ResetSessionState();
                    // PatchSet2: Water 地形欺骗资源引用
                    WaterTerrainSwap.ResetSessionState();
                    // PatchSet2: 水模拟质量（背景水覆写记录 + StableSpeed + 暂停冻结状态）
                    WaterSystemOptRuntimePatch.ResetSessionState();
                    // PatchSet2: 雪模拟冻结（SnowSimSpeed 覆写记录）
                    SnowSystemOptRuntimePatch.ResetSessionState();
                    // PatchSet2: 事件驅動自適應休眠（狀態機 + 信號快照 + EntityQuery 快取）
                    WaterAdaptiveSleep.ResetSessionState();
                    // 诊断标志：允许二次加载重新诊断
                    _cellMapDialogShown = false;
                    _pollCacheReady = false;
                    _monitoredVanillaDisabled.Clear();
                    _monitoredExpectedEnabled.Clear();
                    _reportedRuntimeAnomalies.Clear();
                    // 存档闸门：回主菜单即作废残留的转换请求
                    VanillaConversionState.Clear("返回主菜单");
                    ModLog.Ok(Tag, "所有 PatchSet 会话状态已重置");
                }
                catch (Exception ex)
                {
                    ModLog.Error(Tag, $"会话状态重置失败: {ex.Message}");
                }
            }
            else
            {
                // 每次載入開始前清除上一次的地圖尺寸錯配判定，
                // 避免「遊戲中直接載入另一張地圖」時沿用舊記錄而誤報。
                // （Cleanup 分支已透過 TerrainSystemPatches.ResetSessionState 一併清除）
                // 同时清掉地形尺寸记录：本次加载第一次调用 FinalizeTerrainData 才记得下来
                MapSizeMismatchState.Reset();

                // 存档闸门：已完成、失败或被否决的转换请求到下一次加载就作废；本次正是转换读档时请求处于"加载中"，不受影响
                VanillaConversionState.ClearFinished("新一次加载开始");

                // 加载开始前执行防御性检查，确保被本 Mod 替换的原版系统保持禁用状态
                // （防范外部 Mod 将其重新启用，消除加载期间原版与替换系统双跑的窗口期）。
                // 要重新禁用的正是 OnLoad 时各条替换声明取代的那些，与实时设定无关
                SystemReplacer.ReDisableVanillaSystems(this.World);
            }
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (mode == GameMode.Game)
            {
                // 每次加载都运行诊断（二次加载后系统状态可能异常，必须每次验证）
                string loadLabel = _initialCheckDone ? "二次加载" : "首次加载";
                _initialCheckDone = true;
                ModLog.Ok(Tag, $"游戏加载完成（{loadLabel}），执行系统状态诊断");

                // 诊断前再次执行防御性检查，确保被本 Mod 替换的原版系统未被外部 Mod 重新启用
                SystemReplacer.ReDisableVanillaSystems(this.World);

                // 地圖尺寸模式錯配提示（由 FinalizeTerrainData_Prefix 於載入中途記錄）
                ShowMapSizeMismatchDialogIfNeeded(purpose, mode);

                RunDiagnostics();
            }
            else if (mode == GameMode.Editor)
            {
                // 地圖作者在編輯器載入別的模式製作的地圖，地形取樣同樣錯位，而且比遊戲內更常發生；
                // 原版系統重禁用與狀態診斷屬遊戲會話專用，編輯器只做錯配提示。
                ShowMapSizeMismatchDialogIfNeeded(purpose, mode);
            }
        }

        /// <summary>
        /// MainLoop 低频状态轮询：仅在游戏会话内每 <see cref="kPollFrameInterval"/> 帧遍历一次已缓存的系统指针列表。
        /// </summary>
        protected override void OnUpdate()
        {
            if (!_pollCacheReady) return;
            GameManager gm = GameManager.instance;
            if (gm == null || gm.gameMode != GameMode.Game) return;

            if (++_pollFrameCounter < kPollFrameInterval) return;
            _pollFrameCounter = 0;

            try
            {
                PollMonitoredSystems();
            }
            catch (Exception ex)
            {
                _pollCacheReady = false;
                ModLog.Warn(Tag, $"运行期状态轮询异常，本会话已暂停轮询: {ex.Message}");
            }
        }

        /// <summary>
        /// 若本次載入偵測到地圖尺寸模式錯配，顯示提示對話框。
        ///
        /// <para><b>為何在此處而非偵測點</b>：偵測發生於
        /// <c>TerrainSystem.FinalizeTerrainData</c> 的 Prefix（反序列化中途），
        /// 該時機 UI 尚未就緒無法彈窗。此處是載入完成後最早的安全時機。</para>
        ///
        /// <para>沿用 <c>VanillaSaveConversionSystem</c> 的 <c>MainThreadDispatcher</c>
        /// 延迟模式，确保排在其他 Mod 的加载完成对话框之后，避免被覆盖。</para>
        ///
        /// <para><b>原版地形被拉伸</b>另走 <see cref="ShowVanillaStretchIfNeeded"/>。</para>
        /// </summary>
        private void ShowMapSizeMismatchDialogIfNeeded(Purpose purpose, GameMode mode)
        {
            if (MapSizeMismatchState.DialogShown) return;

            if (MapSizeMismatchState.VanillaStretched)
            {
                ShowVanillaStretchIfNeeded(purpose, mode);
                return;
            }

            if (!MapSizeMismatchState.HasMismatch)
                return;

            // 28→57 读的是 28 km 存档：模式不符正是转换要处理的；被否决的由转换系统弹否决说明
            if (VanillaConversionState.HasRequestForCurrentLoad)
            {
                ModLog.Info(Tag, $"地图尺寸模式不符由转换请求处理，不提示：{VanillaConversionState.Current}");
                return;
            }

            // 尊重使用者關閉驗證的設定，與 LoadGameValidatorPatch 的行為一致
            if (Mod.Instance?.CurrentSettings?.DisableLoadGameValidation == true)
            {
                ModLog.Info(Tag, "已停用加载验证，略过地图尺寸错配提示（日志中仍有警告记录）");
                return;
            }

            MapSizeMismatchState.DialogShown = true;

            try
            {
                string authoredMode = PatchManager.GetModeNameForCoreValue(MapSizeMismatchState.AuthoredCoreValue);
                string currentMode = PatchManager.GetModeNameForCoreValue(MapSizeMismatchState.CurrentCoreValue);

                var locParams = new Dictionary<string, ILocElement>
                {
                    { "AUTHORED_MODE", LocalizedString.Value(authoredMode) },
                    { "CURRENT_MODE", LocalizedString.Value(currentMode) }
                };

                var dialog = new MessageDialog(
                    LocalizedString.Id("MAPEXT_MAPSIZE.MismatchTitle"),
                    new LocalizedString("MAPEXT_MAPSIZE.MismatchMessage", null, locParams),
                    LocalizedString.Id("LOAD_VALIDATION.ConfirmOK"));

                Colossal.Core.MainThreadDispatcher.RegisterUpdater(() =>
                {
                    GameManager.instance.userInterface.appBindings.ShowMessageDialog(dialog, null);
                    return true;
                });

                // === 通知条（与弹窗并存，非备援）===
                // 延迟一帧只能避免同帧其他 Mod 弹窗覆盖，挡不住跨帧 UI 刷新冲掉模态弹窗；
                // 通知条由 PSI 层管理、独立于场景 UI，不会被重建冲掉，且玩家事后仍可见，两者互补。
                NotificationSystem.Push(
                    identifier: kMismatchNotificationId,
                    title: LocalizedString.Id("MAPEXT_MAPSIZE.MismatchTitle"),
                    text: new LocalizedString("MAPEXT_MAPSIZE.MismatchNotify", null, locParams),
                    progressState: Colossal.PSI.Common.ProgressState.Warning,
                    progress: 100
                );

                ModLog.Warn(Tag,
                    $"已提示地图尺寸错配：地图为 {authoredMode}，当前为 {currentMode}");
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"显示地图尺寸错配对话框失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 原版 14 km 的地形在扩展模式下按当前尺寸拉伸后的载入后提示。
        /// <list type="bullet">
        /// <item>事实警告，不受"禁止读档校验"控制；</item>
        /// <item>只提示游戏的开新城与编辑器的读图（<c>LoadMap</c>）：编辑器新建地图本来就以默认 14336 创建再缩放；
        /// 读档拉伸只在关闭校验后读原版存档时出现，只记 Warn；</item>
        /// <item>本次加载有转换请求时不提示，否决的说明由转换系统弹；</item>
        /// <item>菜单层已提示过、玩家选了"仍要继续"的只推通知条，其余对话框与通知条并存（理由同模式不符）。</item>
        /// </list>
        /// 到这里的开新城，是菜单层判不了的：面积不可信、资产没有元数据，或闸门异常放行。
        /// </summary>
        private static void ShowVanillaStretchIfNeeded(Purpose purpose, GameMode mode)
        {
            string currentMode = PatchManager.GetModeNameForCoreValue(PatchManager.CurrentCoreValue);

            if (VanillaConversionState.HasRequestForCurrentLoad)
            {
                ModLog.Info(Tag, $"原版尺寸的地形本次由转换请求处理，不提示拉伸：{VanillaConversionState.Current}");
                return;
            }

            bool editor = mode == GameMode.Editor;
            if (editor ? purpose != Purpose.LoadMap : purpose != Purpose.NewGame)
            {
                if (editor)
                    ModLog.Info(Tag, $"编辑器 {purpose}：默认地形按 {currentMode} 创建，不提示拉伸");
                else
                    ModLog.Warn(Tag, $"读档（{purpose}）的地形是原版 14 km，已按 {currentMode} 拉伸；读档的拉伸只记日志，不提示");
                return;
            }

            MapSizeMismatchState.DialogShown = true;

            try
            {
                var locParams = new Dictionary<string, ILocElement>
                {
                    { "CURRENT_MODE", LocalizedString.Value(currentMode) },
                    { "NONE_MODE", LocalizedString.Value(PatchManager.GetModeNameForCoreValue(SaveEntryGate.NoneModeCoreValue)) }
                };

                // 菜单层的标记只在游戏开新城时有；编辑器读图一律弹框
                bool prompted = !editor && SaveEntryGate.StretchPromptedForCurrentLoad;
                if (!prompted)
                {
                    var dialog = new MessageDialog(
                        LocalizedString.Id("MAPEXT_MAPSIZE.StretchTitle"),
                        new LocalizedString(editor ? "MAPEXT_MAPSIZE.StretchEditorMessage" : "MAPEXT_MAPSIZE.StretchMessage", null, locParams),
                        LocalizedString.Id("LOAD_VALIDATION.ConfirmOK"));

                    Colossal.Core.MainThreadDispatcher.RegisterUpdater(() =>
                    {
                        GameManager.instance.userInterface.appBindings.ShowMessageDialog(dialog, null);
                        return true;
                    });
                }

                NotificationSystem.Push(
                    identifier: kStretchNotificationId,
                    title: LocalizedString.Id("MAPEXT_MAPSIZE.StretchTitle"),
                    text: new LocalizedString(editor ? "MAPEXT_MAPSIZE.StretchEditorNotify" : "MAPEXT_MAPSIZE.StretchNotify", null, locParams),
                    progressState: Colossal.PSI.Common.ProgressState.Warning,
                    progress: 100
                );

                ModLog.Warn(Tag, $"原版尺寸的地形已按 {currentMode} 拉伸（{mode} {purpose}），" +
                                 (prompted ? "菜单层已提示过，只推通知条" : "已弹提示并推通知条"));
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"显示拉伸提示失败: {ex.Message}");
            }
        }

        private static void PopNotification(string identifier)
        {
            try
            {
                if (NotificationSystem.Exist(identifier)) NotificationSystem.Pop(identifier);
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"撤下通知条 {identifier} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 供 UI 按钮（RefreshStatus）调用。
        /// 执行完整的系统状态诊断并更新 UI。
        /// </summary>
        public void ForceCheck()
        {
            if (GameManager.instance.gameMode != GameMode.Game) return;
            RunDiagnostics();
        }

        #region Diagnostics

        private void RunDiagnostics()
        {
            var world = this.World;
            if (world == null || !world.IsCreated) return;

            // 检查范围按本会话快照决定（OnLoad 时实际替换了哪些）；结果仍写入设定对象供 UI 显示
            var settings = Mod.Instance?.CurrentSettings;
            var config = AppliedConfig.Current;
            if (settings == null || config == null) return;

            bool isExpanded = PatchManager.CurrentMode != PatchModeSetting.None;
            var conflicts = new List<string>();
            var criticalIssues = new List<CriticalIssueItem>();
            var failedSystemTypes = new HashSet<Type>();
            var criticalFailedTypes = new HashSet<Type>();
            var attributedCulprits = new HashSet<string>(StringComparer.Ordinal);
            int okCount = 0;
            int totalChecked = 0;

            ReplacementRegistrar registrar = SystemReplacer.Registrar;

            // === 0. 消费 OnGamePreload / OnGameLoadingComplete 阶段实际发生重禁用的原版系统记录 ===
            // 记录只说明加载时发现它处于启用状态；重开也可能发生在上一局游戏中（如运行期周期性重开），不一定发生在加载期间
            if (registrar != null)
            {
                List<Type> reDisabled = registrar.ConsumeReDisabledVanillaSystems();
                foreach (Type vanilla in reDisabled)
                {
                    failedSystemTypes.Add(vanilla);
                    string shortName = ShortTypeName(vanilla);
                    bool isCellMap = IsCellMapCriticalVanilla(registrar, vanilla, isExpanded);
                    if (isCellMap)
                    {
                        criticalFailedTypes.Add(vanilla);
                        criticalIssues.Add(new CriticalIssueItem
                        {
                            Kind = CriticalIssueKind.ReEnabledOnLoad,
                            TargetName = shortName,
                            LogDetail = $"{shortName} [加载时处于启用状态，已重新禁用]",
                        });
                    }
                    else
                    {
                        conflicts.Add($"{shortName} [Enabled at load]");
                        ModLog.Warn(Tag, $"加载时发现被替换的原版系统 {shortName} 处于启用状态，已重新禁用（可能在上一局游戏中或加载过程中被外部重新启用）");
                    }
                }
            }

            // === 1. 替换声明验证：取代的原版系统已禁用、替换系统仍启用 ===
            if (registrar != null)
            {
                int beforeCount = conflicts.Count;
                registrar.Diagnose(world, conflicts, ref okCount, ref totalChecked);
                if (conflicts.Count > beforeCount)
                {
                    // 收集替换声明诊断异常对应的具体原版类型以供层 1 归因，并将 CellMap 核心替换项提升至严重列表
                    CollectRegistrarFailedTypes(world, registrar, isExpanded, failedSystemTypes, criticalFailedTypes, conflicts, criticalIssues);
                }
            }

            // === 2. Demand Job 宿主（Transpiler 宿主，须保持启用）===
            if (config.EconomyFix && config.Demand)
            {
                CheckModEnabled<ResidentialDemandSystem>(world, "Demand", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<IndustrialDemandSystem>(world, "Demand", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<CommercialDemandSystem>(world, "Demand", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
            }

            // === 3. CellMap 模拟核心宿主与关键补丁宿主状态验证（宿主停用属局部功能偏差，记入普通冲突与日志，不弹 CRITICAL 退出框） ===
            if (isExpanded)
            {
                int beforeHostConflicts = conflicts.Count;

                CheckModEnabled<TelecomCoverageSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<WindSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<WindSimulationSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<AttractionSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<GroundWaterPollutionSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<ObjectPolluteSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<ZoneSpawnSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<CitizenHappinessSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<PollutionTriggerSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<PowerPlantAISystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<WaterPumpingStationAISystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<FloodCheckSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<WaterDangerSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<WaterLevelChangeSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<NetColorSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<MapTileSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<SpawnableAmbienceSystem>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<CarNavigationSystem.Actions>(world, "CellSystem", conflicts, failedSystemTypes, ref okCount, ref totalChecked);

                CheckModEnabled<TerrainSystem>(world, "MapSizePatch", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<WaterSystem>(world, "MapSizePatch", conflicts, failedSystemTypes, ref okCount, ref totalChecked);
                CheckModEnabled<AirwaySystem>(world, "MapSizePatch", conflicts, failedSystemTypes, ref okCount, ref totalChecked);

                if (conflicts.Count > beforeHostConflicts)
                {
                    var disabledHosts = new List<string>();
                    for (int i = beforeHostConflicts; i < conflicts.Count; i++)
                        disabledHosts.Add(conflicts[i]);
                    ModLog.Warn(Tag, $"检测到模拟宿主系统处于停用状态（MapExt 对该宿主的补丁不执行）: {string.Join(", ", disabledHosts)}");
                }
            }

            // === 4. 层 1：仅在检出状态异常或本会话首次入城核查双跑时查询归因索引 ===
            bool runDualCheckThisTime = !_sessionDualRunChecked;
            if (failedSystemTypes.Count > 0 || runDualCheckThisTime)
            {
                _sessionDualRunChecked = true;
                var layer1QueryTargets = new HashSet<Type>(failedSystemTypes);
                var sessionReplacedVanilla = new HashSet<Type>();

                if (runDualCheckThisTime && registrar != null)
                {
                    foreach (ReplacementRegistrar.Declaration decl in registrar.Declarations)
                    {
                        foreach (Type v in decl.Vanilla)
                        {
                            layer1QueryTargets.Add(v);
                            sessionReplacedVanilla.Add(v);
                        }
                    }
                }

                if (layer1QueryTargets.Count > 0)
                {
                    Dictionary<Type, List<string>> mutators = ModAttributionHelper.FindEnabledMutators(
                        layer1QueryTargets,
                        world,
                        out Dictionary<Type, List<string>> dualRunMap);

                    // 4a. 为已检出状态异常的系统附加精确方法级归因（仅严重项来源记入弹窗归因源列表）
                    foreach (Type failedSys in failedSystemTypes)
                    {
                        if (mutators.TryGetValue(failedSys, out List<string> srcs))
                        {
                            if (criticalFailedTypes.Contains(failedSys))
                            {
                                foreach (string s in srcs)
                                    attributedCulprits.Add(s);
                            }

                            ModLog.Warn(Tag, $"归因定位 | {ShortTypeName(failedSys)} 状态变更关联调用方: {string.Join("; ", srcs)}");
                        }
                    }

                    // 4b. 静态双跑嫌疑（仅凭静态 IL + 活跃模拟系统只记普通冲突与日志，绝不弹 CRITICAL 退出框）
                    if (runDualCheckThisTime)
                    {
                        foreach (KeyValuePair<Type, List<string>> kv in dualRunMap)
                        {
                            if (!sessionReplacedVanilla.Contains(kv.Key)) continue;
                            string srcSummary = string.Join("; ", kv.Value);
                            string msg = $"{ShortTypeName(kv.Key)} [Overlap: {srcSummary}]";
                            if (!conflicts.Contains(msg))
                                conflicts.Add(msg);

                            ModLog.Warn(Tag, $"检测到对 {ShortTypeName(kv.Key)} 调用 set_Enabled 且含活跃模拟系统的模组: {srcSummary}");
                        }
                    }
                }
            }

            // === 5. 层 2：本会话 Harmony 补丁快照存活与外部重叠补丁核查 ===
            var strippedIssues = new List<CriticalIssueItem>();
            var overlapWarnings = new List<string>();
            ModAttributionHelper.InspectSessionPatches(strippedIssues, overlapWarnings, attributedCulprits);
            foreach (CriticalIssueItem sp in strippedIssues)
            {
                criticalIssues.Add(sp);
                ModLog.Error(Tag, $"检测到关键补丁缺失: {sp.LogDetail}");
            }

            foreach (string ow in overlapWarnings)
            {
                ModLog.Warn(Tag, $"检测到目标方法上的外部重叠补丁: {ow}");
            }

            // === 6. 层 3：全模式回读核验 CellMapSystem<T>.kMapSize == 14336 ===
            ModAttributionHelper.CheckCellMapStaticFields(criticalIssues, attributedCulprits);

            // === 7. 构建本会话运行期低频轮询的系统引用缓存 ===
            RebuildPollCache(world, config, registrar);

            // === 8. 更新 UI 状态与告警弹窗 ===
            int totalConflicts = conflicts.Count + criticalIssues.Count;
            if (totalConflicts > 0)
            {
                string statusMsg = $"[!] {okCount}/{totalChecked} OK, {totalConflicts} conflict(s)";
                if (criticalIssues.Count > 0)
                    statusMsg += $" ({criticalIssues.Count} CRITICAL CellMap)";

                settings._systemStatusReport = statusMsg;
                ModLog.Warn(Tag, statusMsg);

                // 仅在存在真实 CellMap 尺寸错配或核心补丁被移除时弹三语严重对话框
                if (criticalIssues.Count > 0 && !_cellMapDialogShown)
                {
                    _cellMapDialogShown = true;
                    ShowCellMapConflictDialog(criticalIssues, attributedCulprits);
                }
            }
            else if (totalChecked > 0)
            {
                settings._systemStatusReport = $"{okCount}/{totalChecked} OK";
                ModLog.Ok(Tag, $"诊断完成: {okCount}/{totalChecked} OK");
            }
            else
            {
                settings._systemStatusReport = "No systems checked (all groups off)";
            }
        }

        private void RebuildPollCache(World world, AppliedConfig config, ReplacementRegistrar registrar)
        {
            _monitoredVanillaDisabled.Clear();
            _monitoredExpectedEnabled.Clear();
            _pollFrameCounter = 0;

            if (registrar != null)
            {
                foreach (ReplacementRegistrar.Declaration decl in registrar.Declarations)
                {
                    bool isCritical = decl.Group == ReplacementRegistrar.GroupCellMap;
                    foreach (Type vanilla in decl.Vanilla)
                    {
                        ComponentSystemBase vSys = world.GetExistingSystemManaged(vanilla);
                        if (vSys != null)
                        {
                            _monitoredVanillaDisabled.Add(new CachedSystemRef
                            {
                                System = vSys,
                                SystemType = vanilla,
                                Group = decl.Group,
                                IsCriticalCellMap = isCritical,
                            });
                        }
                    }

                    if (decl.ModType != null)
                    {
                        ComponentSystemBase mSys = world.GetExistingSystemManaged(decl.ModType);
                        // 仅监控初始确认为 Enabled == true 的替换系统（如 SoilWaterSystemMod 未挂更新阶段时按实况处理）
                        if (mSys != null && mSys.Enabled)
                        {
                            _monitoredExpectedEnabled.Add(new CachedSystemRef
                            {
                                System = mSys,
                                SystemType = decl.ModType,
                                Group = decl.Group,
                                IsCriticalCellMap = isCritical,
                            });
                        }
                    }
                }
            }

            if (config.EconomyFix && config.Demand)
            {
                AddExpectedEnabledIfPresent<ResidentialDemandSystem>(world, "Demand", false);
                AddExpectedEnabledIfPresent<IndustrialDemandSystem>(world, "Demand", false);
                AddExpectedEnabledIfPresent<CommercialDemandSystem>(world, "Demand", false);
            }

            // 严格仅纳入 18 个模拟核心宿主，不含 9 个常态禁用的工具／UI／音频宿主与 TerrainSystem/WaterSystem/AirwaySystem
            if (PatchManager.CurrentMode != PatchModeSetting.None)
            {
                AddExpectedEnabledIfPresent<TelecomCoverageSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<WindSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<WindSimulationSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<AttractionSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<GroundWaterPollutionSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<ObjectPolluteSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<ZoneSpawnSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<CitizenHappinessSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<PollutionTriggerSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<PowerPlantAISystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<WaterPumpingStationAISystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<FloodCheckSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<WaterDangerSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<WaterLevelChangeSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<NetColorSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<MapTileSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<SpawnableAmbienceSystem>(world, "CellSystem", true);
                AddExpectedEnabledIfPresent<CarNavigationSystem.Actions>(world, "CellSystem", true);
            }

            _pollCacheReady = true;
        }

        private void AddExpectedEnabledIfPresent<T>(World world, string group, bool isCriticalCellMap)
            where T : GameSystemBase
        {
            T sys = world.GetExistingSystemManaged<T>();
            if (sys != null && sys.Enabled)
            {
                _monitoredExpectedEnabled.Add(new CachedSystemRef
                {
                    System = sys,
                    SystemType = typeof(T),
                    Group = group,
                    IsCriticalCellMap = isCriticalCellMap,
                });
            }
        }

        private void PollMonitoredSystems()
        {
            World world = this.World;
            if (world == null || !world.IsCreated) return;

            // 1. 检查本应保持禁用的原版系统是否被其他 Mod 在运行期周期性重新启用
            for (int i = 0; i < _monitoredVanillaDisabled.Count; i++)
            {
                CachedSystemRef item = _monitoredVanillaDisabled[i];
                if (item.System == null || !item.System.Enabled) continue;

                // 再次置为 false，但因两次轮询之间无法阻止周期性双跑，仅每系统每会话记录一次发现告警，绝不表述为“已修复/已解决”
                item.System.Enabled = false;
                if (_reportedRuntimeAnomalies.Add(item.SystemType))
                {
                    string src = LookupSingleSystemMutator(item.SystemType, world, kNoMutatorReEnabled);
                    ModLog.Warn(Tag,
                        $"运行期检测到已被替换的原版系统 {ShortTypeName(item.SystemType)} [{item.Group}] 被外部重新启用" +
                        $"（存在周期性双跑风险，关联调用方: {src}）");

                    var settings = Mod.Instance?.CurrentSettings;
                    if (settings != null)
                        settings._systemStatusReport = $"[!] Runtime re-enable detected: {ShortTypeName(item.SystemType)}";
                }
            }

            // 2. 检查 18 个模拟核心宿主与活跃替换系统是否掉为 Enabled == false：严格只报告、绝不自动重开！
            for (int i = 0; i < _monitoredExpectedEnabled.Count; i++)
            {
                CachedSystemRef item = _monitoredExpectedEnabled[i];
                if (item.System == null || item.System.Enabled) continue;

                if (_reportedRuntimeAnomalies.Add(item.SystemType))
                {
                    string src = LookupSingleSystemMutator(item.SystemType, world, kNoMutatorDisabled);
                    ModLog.Warn(Tag,
                        $"运行期检测到系统 {ShortTypeName(item.SystemType)} [{item.Group}] 变为已停用状态" +
                        $"（为防异常循环不自动重开，关联调用方或异常排查: {src}）");

                    var settings = Mod.Instance?.CurrentSettings;
                    if (settings != null)
                        settings._systemStatusReport = $"[!] Runtime disabled: {ShortTypeName(item.SystemType)}";
                }
            }
        }

        /// <summary>查询单个系统的 <c>set_Enabled</c> 调用方；没查到时返回 <paramref name="notFoundHint"/>（按重新启用或被停用分别说明）。</summary>
        private static string LookupSingleSystemMutator(Type sysType, World world, string notFoundHint)
        {
            Dictionary<Type, List<string>> map = ModAttributionHelper.FindEnabledMutators(
                new[] { sysType },
                world,
                out _);
            return map.TryGetValue(sysType, out List<string> list) && list.Count > 0
                ? string.Join("; ", list)
                : notFoundHint;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 检查宿主系统是否仍然启用（期望 Enabled == true）。
        /// </summary>
        private static void CheckModEnabled<T>(
            World world,
            string group,
            List<string> conflicts,
            HashSet<Type> failedTypes,
            ref int okCount,
            ref int totalChecked) where T : GameSystemBase
        {
            totalChecked++;
            var system = world.GetExistingSystemManaged<T>();
            if (system == null) { okCount++; return; }

            if (!system.Enabled)
            {
                failedTypes.Add(typeof(T));
                conflicts.Add($"{ShortTypeName(typeof(T))} [{group}]");
            }
            else
            {
                okCount++;
            }
        }

        /// <summary>
        /// 被替换的原版系统是否计入 CRITICAL 严重项：已在 <see cref="ConflictTargets"/> 登记的系统按角色判定
        /// （只有 12 个 CellMapSystem&lt;T&gt; 计入，C1/D1 CellOnly 不计入）；未登记的才按所属 CellMap 替换声明兜底。
        /// </summary>
        private static bool IsCellMapCriticalVanilla(ReplacementRegistrar registrar, Type vanilla, bool isExpanded)
        {
            if (vanilla == null) return false;

            // 找房组关闭时 C1/D1 的 *_CellOnly 声明也登记在 CellMap 分组，已登记的系统不能再按分组判断
            if (vanilla.FullName != null && ConflictTargets.FindProtectedSystem(vanilla.FullName) != null)
                return ConflictTargets.IsCriticalCellMapVanilla(vanilla.FullName, isExpanded);

            foreach (ReplacementRegistrar.Declaration d in registrar.Declarations)
            {
                if (d.Group != ReplacementRegistrar.GroupCellMap) continue;
                if (d.Vanilla.Contains(vanilla)) return true;
            }

            return false;
        }

        private static void CollectRegistrarFailedTypes(
            World world,
            ReplacementRegistrar registrar,
            bool isExpanded,
            HashSet<Type> failedTypes,
            HashSet<Type> criticalFailedTypes,
            List<string> generalConflicts,
            List<CriticalIssueItem> criticalIssues)
        {
            foreach (ReplacementRegistrar.Declaration d in registrar.Declarations)
            {
                foreach (Type v in d.Vanilla)
                {
                    ComponentSystemBase sys = world.GetExistingSystemManaged(v);
                    if (sys != null && sys.Enabled)
                    {
                        failedTypes.Add(v);
                        if (IsCellMapCriticalVanilla(registrar, v, isExpanded))
                        {
                            criticalFailedTypes.Add(v);
                            string shortName = ShortTypeName(v);
                            string rawEntry = $"{shortName} [{d.Group}]";
                            generalConflicts.Remove(rawEntry);
                            criticalIssues.Add(new CriticalIssueItem
                            {
                                Kind = CriticalIssueKind.VanillaStillActive,
                                TargetName = shortName,
                                LogDetail = $"{shortName} [原版系统仍处于启用状态]",
                            });
                        }
                    }
                }

                if (d.ModType != null)
                {
                    ComponentSystemBase modSys = world.GetExistingSystemManaged(d.ModType);
                    if (modSys != null && !modSys.Enabled)
                    {
                        failedTypes.Add(d.ModType);
                        bool anyVanillaCritical = false;
                        foreach (Type v in d.Vanilla)
                        {
                            if (IsCellMapCriticalVanilla(registrar, v, isExpanded))
                            {
                                anyVanillaCritical = true;
                                break;
                            }
                        }

                        if (anyVanillaCritical)
                        {
                            criticalFailedTypes.Add(d.ModType);
                            string shortName = ShortTypeName(d.ModType);
                            string rawEntry = $"{shortName} [{d.Group}]";
                            generalConflicts.Remove(rawEntry);
                            criticalIssues.Add(new CriticalIssueItem
                            {
                                Kind = CriticalIssueKind.ReplacementDisabled,
                                TargetName = shortName,
                                LogDetail = $"{shortName} [MapExt 替换系统被停用]",
                            });
                        }
                    }
                }
            }
        }

        private static string ShortTypeName(Type type) =>
            type.DeclaringType == null ? type.Name : $"{type.DeclaringType.Name}.{type.Name}";

        #endregion

        #region CellMap Conflict Dialog

        /// <summary>显示三语本地化的 CellMap 严重冲突弹窗（优先展示精确命中的冲突源 Mod，极端零命中时降级调用实时过滤后的已加载代码 Mod 列表）。</summary>
        private static void ShowCellMapConflictDialog(List<CriticalIssueItem> criticalIssues, HashSet<string> attributedCulprits)
        {
            try
            {
                var issueLines = new List<string>(criticalIssues.Count);
                var logIssueItems = new List<string>(criticalIssues.Count);
                foreach (CriticalIssueItem item in criticalIssues)
                {
                    logIssueItems.Add(item.LogDetail ?? item.TargetName);
                    issueLines.Add("  • " + FormatLocalizedCriticalIssue(item));
                }

                string affectedBlock = string.Join("\n", issueLines);
                bool hasExactAttribution = attributedCulprits != null && attributedCulprits.Count > 0;

                string sourcesBlock;
                if (hasExactAttribution)
                {
                    string bulletMods = "  • " + string.Join("\n  • ", attributedCulprits);
                    sourcesBlock = ResolveLocTemplate(
                        "MAPEXT_CONFLICT.CellMapSourcesIdentified",
                        "Associated mod(s):\n{MOD_LIST}")
                        .Replace("{MOD_LIST}", bulletMods);
                }
                else
                {
                    sourcesBlock = ResolveLocTemplate(
                        "MAPEXT_CONFLICT.CellMapSourcesFallback",
                        "Loaded code mods (fallback list):\n  {MOD_LIST}")
                        .Replace("{MOD_LIST}", ModConflictDetector.GetLoadedModNames());
                }

                var locParams = new Dictionary<string, ILocElement>
                {
                    { "ISSUES", LocalizedString.Value(affectedBlock) },
                    { "SOURCES", LocalizedString.Value(sourcesBlock) },
                };

                var dialog = new ConfirmationDialog(
                    LocalizedString.Id("MAPEXT_CONFLICT.CellMapTitle"),
                    new LocalizedString("MAPEXT_CONFLICT.CellMapMessage", null, locParams),
                    LocalizedString.Id("MAPEXT_CONFLICT.CellMapQuit"),
                    LocalizedString.Id("MAPEXT_CONFLICT.CellMapContinue"));

                Colossal.Core.MainThreadDispatcher.RegisterUpdater(() =>
                {
                    GameManager.instance.userInterface.appBindings.ShowConfirmationDialog(dialog, (int result) =>
                    {
                        if (result == 0)
                        {
                            ModLog.Error(Tag, "CellMap 冲突：用户选择退出游戏");
                            GameManager.QuitGame();
                        }
                        else
                        {
                            ModLog.Warn(Tag, "CellMap 冲突：用户选择继续运行");
                        }
                    });
                    return true;
                });

                string logSrc = hasExactAttribution ? string.Join("; ", attributedCulprits) : "无直接命中";
                ModLog.Error(Tag, $"CRITICAL CellMap 冲突! 受影响: {string.Join(", ", logIssueItems)} | 归因源: {logSrc}");
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"显示 CellMap 冲突弹窗失败: {ex.Message}");
            }
        }

        private static string FormatLocalizedCriticalIssue(CriticalIssueItem item)
        {
            switch (item.Kind)
            {
                case CriticalIssueKind.ReEnabledOnLoad:
                    return ResolveLocTemplate("MAPEXT_CONFLICT.IssueReEnabledOnLoad", "{TARGET} (replaced vanilla system was found enabled during loading and has been disabled again)")
                        .Replace("{TARGET}", item.TargetName ?? "?");
                case CriticalIssueKind.VanillaStillActive:
                    return ResolveLocTemplate("MAPEXT_CONFLICT.IssueVanillaActive", "{TARGET} (replaced vanilla system is still active)")
                        .Replace("{TARGET}", item.TargetName ?? "?");
                case CriticalIssueKind.ReplacementDisabled:
                    return ResolveLocTemplate("MAPEXT_CONFLICT.IssueModDisabled", "{TARGET} (MapExt replacement system was disabled)")
                        .Replace("{TARGET}", item.TargetName ?? "?");
                case CriticalIssueKind.HarmonyPatchesRemoved:
                    return ResolveLocTemplate("MAPEXT_CONFLICT.IssuePatchRemoved", "MapExt Harmony patches were removed ({COUNT} target method(s), e.g. {SAMPLE})")
                        .Replace("{COUNT}", item.Count.ToString())
                        .Replace("{SAMPLE}", item.Sample ?? string.Empty);
                case CriticalIssueKind.MapSizeStaticFieldTampered:
                    return ResolveLocTemplate("MAPEXT_CONFLICT.IssueMapSizeTampered", "CellMapSystem<T>.kMapSize static field was overwritten ({COUNT} field(s), e.g. {SAMPLE})")
                        .Replace("{COUNT}", item.Count.ToString())
                        .Replace("{SAMPLE}", item.Sample ?? string.Empty);
                default:
                    return item.TargetName ?? "?";
            }
        }

        private static string ResolveLocTemplate(string key, string fallbackEn)
        {
            try
            {
                var dict = GameManager.instance?.localizationManager?.activeDictionary;
                if (dict != null && dict.TryGetValue(key, out string val) && !string.IsNullOrEmpty(val))
                    return val;
            }
            catch
            {
                // 降级使用英文模板
            }

            return fallbackEn;
        }

        #endregion
    }
}
