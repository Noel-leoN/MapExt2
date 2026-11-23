// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Reflection;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.PSI;
using Game.SceneFlow;
using Game.UI.Localization;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using MapExtPDX.SaveLoadSystem;
using Unity.Jobs.LowLevel.Unsafe;

namespace MapExtPDX
{
    public class Mod : IMod
    {
        private const string Tag = nameof(Mod);

        public const string ModName = "MapExtPDX";

        // public const string ModFileName = "MapExtPDX2";
        public const string ModNameZH = "大地图mod";
        public static string ModVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        // 定义公共静态实例
        // (IMod标准接口仅包含OnLoad/Dispose，如自定义公共方法，需创建静态实例)
        public static Mod Instance { get; private set; }

        // 定义设置UI
        private ModSettings m_Setting;
        public ModSettings CurrentSettings => m_Setting;

        /// <summary>
        /// Settings 别名，使 EcoSystems 代码与 EconomyEX 保持一致的访问路径。
        /// </summary>
        public ModSettings Settings => m_Setting;

        // 日志初始化
        // 日志归结到Logs\ModName.log，不要放在Player.log
        // 只供 ModLog 内部使用，业务代码一律经 ModLog 输出
        public static ILog Logger = LogManager.GetLogger($"{ModName}").SetShowsErrorsInUI(false);

        /// <summary>
        /// 双 Harmony 实例：按来源分别管理补丁，卸载时各按自己的 id 移除。
        /// 地图尺寸模式只在启动时应用一次，不支持运行中切换。
        /// </summary>
        public static readonly string HarmonyIdGlobal = $"{ModName}_global";

        public static readonly string HarmonyIdModes = $"{ModName}_modes";

        // 用于全局并行补丁定义
        private Harmony _globalPatcher;

        // 用于MapSize模式选择补丁集定义
        private Harmony _modePatcher;

        // --- unpatch标志位，用于ReBurst安全卸载 ---
        public static bool IsUnloading { get; private set; } = false;

        /// <summary>
        /// <see cref="OnLoad"/> 是否中斷過。中斷後本 Mod 處於<b>半初始化</b>狀態——
        /// 補丁可能只套用了一部分，不等於「乾淨地沒生效」。
        /// </summary>
        public static bool InitializationFailed { get; private set; }

        /// <summary>
        /// Mod 加载入口（游戏启动时、载入 Prefab 前由 <c>ModManager</c> 调用）。本方法只负责兜底，
        /// 实际初始化全在 <see cref="OnLoadCore"/>。
        ///
        /// <para><b>为什么需要自己兜底</b>：<c>ModManager.InitializeMods</c> 对逸出的异常
        /// 只写一行 <c>[Modding] [ERROR]</c> 就继续，<b>不弹窗</b>。若不拦截，全部补丁集、
        /// SystemReplacer 与 UI binding 都会静默跳过，玩家无从得知 Mod 未完整生效。
        /// 这里捕获后推一则主菜单通知，让失效变成可见事件。</para>
        /// </summary>
        public void OnLoad(UpdateSystem updateSystem)
        {
            try
            {
                OnLoadCore(updateSystem);
            }
            catch (Exception ex)
            {
                InitializationFailed = true;
                ModLog.Error(Tag, $"OnLoad 中断，本次游戏 MapExt 未完整生效: {ex}");
                PushInitFailureNotification(ex);
            }
        }

        private void OnLoadCore(UpdateSystem updateSystem)
        {
            // === 0. 加载模组执行asset ===
            ModLog.Info(Tag, $"OnLoad, version:{ModVersion}");
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                ModLog.Info(Tag, $"{asset.name} v{asset.version} mod asset at {asset.path}");

            // === 初始化双Harmony实例 ===
            _globalPatcher = new Harmony(HarmonyIdGlobal);
            ModLog.Patch(Tag, "HarmonyGlobal 实例已创建");
            _modePatcher = new Harmony(HarmonyIdModes);
            ModLog.Patch(Tag, "HarmonyModes 实例已创建");

            // === B. 获取设置setting ===
            // 将当前实例赋值给静态属性(use for Setting)
            Instance = this;

            // Initialize settings
            m_Setting = new ModSettings(this);
            m_Setting.RegisterInOptionsUI();
            // 讀取本地化語言庫：設定 UI 文本與對話框文本合併為「每語言一次註冊」，
            // 且每次註冊獨立 try-catch。理由見 RegisterLocalization。
            RegisterLocalization();
            // 读取已保存设置
            var defaultSettings = new ModSettings(this);
            Colossal.IO.AssetDatabase.AssetDatabase.global.LoadSettings(ModName, m_Setting, defaultSettings);
            ModLog.Ok(Tag, "Settings 已初始化");

            // 外部工具 MapExTools 按 StartupKeys 显示默认值、判定存档模式：与本 Mod 的实际定义逐项比对
            VerifyStartupKeys(defaultSettings);

            // 两个 SaveConvert 开关在 UI 上互锁，但外部配置备份/还原工具或手动编辑 .coc
            // 可能写入两者皆 true 的互斥组合，载入后归一化一次
            m_Setting.NormalizeSaveConvertExclusion();

            // Release 亦输出反序列化后的实际值，便于与 ModsSettings/MapExtPDX/MapExtPDX.coc 对照
            ModLog.Info(Tag,
                $"SaveConvert 加载值: EnableVanillaConversion={m_Setting.EnableVanillaConversion}, " +
                $"DisableWorldBackdrop={m_Setting.DisableWorldBackdrop}");

            // 此後 setter 的呼叫一律來自使用者操作而非反序列化，撞名探測才有意義
            ModSettings.s_settingsLoaded = true;

#if DEBUG
            // === 设置值验证日志（仅 DEBUG 编译有效） ===
            // 用于诊断 .coc 文件加载异常或框架缓存不一致问题
            ModLog.Debug(Tag, "=== Settings Dump (DEBUG) ===");
            ModLog.Debug(Tag, $"  PatchMode={m_Setting.PatchModeChoice}");
            ModLog.Debug(Tag, $"  TerrainRes={m_Setting.TerrainResolution}, WaterRes={m_Setting.WaterResolution}");
            ModLog.Debug(Tag,
                $"  WaterSimQuality={m_Setting.WaterSimQuality}, WaterTexFmt={m_Setting.WaterTextureFormat}");
            ModLog.Debug(Tag, $"  TerrainBufferPrealloc={m_Setting.TerrainBufferPrealloc}");
            ModLog.Debug(Tag, $"  TerrainCascadeThrottle={m_Setting.TerrainCascadeThrottle}");
            ModLog.Debug(Tag,
                $"  EnableVanillaConversion={m_Setting.EnableVanillaConversion}, DisableWorldBackdrop={m_Setting.DisableWorldBackdrop}");
            ModLog.Debug(Tag, $"  EconomyFix={m_Setting.isEnableEconomyFix}");
            ModLog.Debug(Tag,
                $"  EcoSystems: Demand={m_Setting.EnableDemandEcoSystem}, JobSearch={m_Setting.EnableJobSearchEcoSystem}");
            ModLog.Debug(Tag,
                $"  EcoSystems: HouseholdProp={m_Setting.EnableHouseholdPropertyEcoSystem}, ResBuyer={m_Setting.EnableResourceBuyerEcoSystem}");
            ModLog.Debug(Tag,
                $"  EcoSystems: ResidentAI={m_Setting.EnableResidentAIEcoSystem}, DownstreamAI={m_Setting.EnableDownstreamAIEcoSystem}");
            ModLog.Debug(Tag, $"  NoDogs: Street={m_Setting.NoDogsOnStreet}, Gen={m_Setting.NoDogsGeneration}");
            ModLog.Debug(Tag, $"  NoThroughTraffic={m_Setting.NoThroughTraffic}");
            ModLog.Debug(Tag, $"  DisableLoadGameValidation={m_Setting.DisableLoadGameValidation}");
            ModLog.Debug(Tag, "=== End Settings Dump ===");
#endif

            // === C. 初始化MapSize PatchManager ===
            // 应用启动时默认的补丁模式
            // m_Setting.PatchModeChoice 从配置文件中加载上次保存的模式
            // 从设置加载初始模式
            PatchModeSetting initialMode = m_Setting.PatchModeChoice;
            ModLog.Info(Tag, $"正在初始化 PatchManager 使用设置模式: {m_Setting.PatchModeChoice}");
            // 执行MapSize关联主要系统补丁
            PatchManager.Initialize(_modePatcher, initialMode);


            // === D. 存档验证系统 ===
            ModLog.Info(Tag, "全局并行方式补丁正在逐条执行...");
            // 4.1 存档元数据与各入口的闸门一律挂载：「禁止读档校验」只在判定时读，
            // 关着校验时存下的档照样带模式值标签。挂载失败按关键处理，本会话进入降级
            ApplySaveGatePatch(nameof(MetaDataExtenderPatch),
                () => _globalPatcher.CreateClassProcessor(typeof(MetaDataExtenderPatch)).Patch());
            ApplySaveGatePatch(nameof(LoadGameValidatorPatch),
                () => _globalPatcher.CreateClassProcessor(typeof(LoadGameValidatorPatch)).Patch());
            ApplySaveGatePatch(nameof(ContinueGameGatePatch),
                () => _globalPatcher.CreateClassProcessor(typeof(ContinueGameGatePatch)).Patch());
            ApplySaveGatePatch(nameof(NewGameGatePatch),
                () => _globalPatcher.CreateClassProcessor(typeof(NewGameGatePatch)).Patch());
            ApplySaveGatePatch(nameof(QuickLoadGatePatch),
                () => _globalPatcher.CreateClassProcessor(typeof(QuickLoadGatePatch)).Patch());
            ApplySaveGatePatch(nameof(GameManagerLoadGatePatch),
                () => _globalPatcher.CreateClassProcessor(typeof(GameManagerLoadGatePatch)).Patch());

            // 其他并行的选项补丁，也在这里添加
            // _globalPatcher.CreateClassProcessor(typeof(ParallelOptionPatch)).Patch();
            // 對話框本地化已隨設定文本在 RegisterLocalization 一併註冊（合併為每語言一次）

            // 4.2 注册原版存档转换系统
            updateSystem.UpdateAt<VanillaSaveConversionSystem>(SystemUpdatePhase.LoadSimulation);
            ModLog.Patch(Tag, $"{nameof(VanillaSaveConversionSystem)} 已注册到 LoadSimulation");

            // === E. 性能工具 ===
            // 其他并行的选项补丁
            // 手动ECS调用Apply方式以应用已保存的设置值
            // --- 加载NoDogs ---
            m_Setting.UpdateNoDogsSystemStates();

            // --- 加载NoThroughTraffic(From CS2LiteBooster，含 RandomTrafficDispatchSystem) ---
            m_Setting.UpdateNoThroughTrafficSystemStates();

            // === F. 加载特色工具 ===
            // 加载LandValueRemake
            // m_Setting.UpdateLandValueRemakeSystemStates();
            //Info($"LandValue Remake补丁(全局并行) {nameof(ModLocalization)}已应用.");

            // 执行诊断系统
            // updateSystem.UpdateAt<SaveGameDiagnosticSystem>(SystemUpdatePhase.LateUpdate);

            // === G. 冲突 Mod 指纹检测 ===
            ModConflictDetector.ScanLoadedMods();
            m_Setting._detectedConflictMods = ModConflictDetector.GetDetectedModsSummary();
            // 根据检测结果生成基于当前设置的冲突报告
            var conflictReport = ModConflictDetector.GetConflictReport(m_Setting);
            if (conflictReport != "None")
            {
                m_Setting._conflictWarning = $"[Startup] {conflictReport}";
                ModLog.Warn(Tag, $"启动冲突报告: {conflictReport}");
            }

            // 自动禁用与冲突 Mod 重叠的系统组（在 SystemReplacer.Apply 之前执行！）
            // 只记入本会话的快照，不改写设定
            var disabledGroups = ModConflictDetector.AutoDisableConflictGroups(m_Setting);
            if (disabledGroups.Count > 0)
            {
                m_Setting._conflictWarning = $"[Auto-Disabled] {string.Join(", ", disabledGroups)}";
                m_Setting._systemStatusReport = $"Auto-disabled {disabledGroups.Count} group(s) due to conflicts";
            }

            // === G2. 本会话生效配置快照 ===
            // 此后注册、重新禁用、诊断与寻路 handler 都只读快照；中途改设定要重启才生效
            AppliedConfig config = AppliedConfig.Capture(m_Setting, disabledGroups);
            ModLog.Info(Tag, $"本会话生效配置: {config}");

            // 本会话 Burst 是否开启：--burst-disable-compilation 参数或环境变量 UNITY_BURST_DISABLE_COMPILATION 会让 Job 改走 Mono，
            // Burst 库照样加载，从进程外看不出来。外部工具 MapExTools 按这一行判断，改措辞要同步工具的匹配规则
            if (JobsUtility.JobCompilerEnabled)
                ModLog.Info(Tag, "本会话 Burst: 已启用");
            else
                ModLog.Warn(Tag, "本会话 Burst: 已停用（--burst-disable-compilation 或 UNITY_BURST_DISABLE_COMPILATION），Job 改走 Mono，运行会明显变慢");

            // === H. ECS替换系统补丁 ===
            // CellMapSystem<T> 和 经济系统 的ECS替换补丁
            SystemReplacer.Apply(updateSystem, _globalPatcher, config);

            // 录制本会话实际已挂的 Dual Harmony 补丁快照并执行 ConflictTargets 单向子集自检
            ModAttributionHelper.CaptureAndVerifySessionPatches();

            // === I. 主菜单通知（冲突提醒） ===
            if (disabledGroups.Count > 0)
            {
                // pageId = AssemblyName.Namespace.TypeName (见 ModSetting 构造函数)
                const string pageId = "MapExt2.MapExtPDX.Mod";
                const string sectionId = "MapExt2.MapExtPDX.Mod.EconomyEX";

                // 分组在注册前就已停用，本会话即时生效、无需重启；设定本身未被改动
                NotificationSystem.Push(
                    identifier: "mapext.conflict",
                    title: LocalizedString.Value("MapExt: Conflicting groups disabled"),
                    text: LocalizedString.Value(
                        $"[MapExt] Disabled {disabledGroups.Count} group(s) for this session " +
                        $"due to mod conflicts: {string.Join(", ", disabledGroups)}. Your saved settings are unchanged."),
                    progressState: Colossal.PSI.Common.ProgressState.Failed,
                    progress: 100,
                    onClicked: () =>
                    {
                        var optionsUI = Unity.Entities.World.DefaultGameObjectInjectionWorld?
                            .GetExistingSystemManaged<Game.UI.Menu.OptionsUISystem>();
                        optionsUI?.OpenPage(pageId, sectionId, false);
                    }
                );
            }
        }

        /// <summary>
        /// 註冊三語本地化：每個語言只呼叫一次 <c>AddSource</c>，且各自獨立 try-catch。
        ///
        /// <para><b>为什么要合并</b>：将设置文本（<c>LocaleEN</c>／HANS／HANT）与对话框文本
        /// （<c>ModLocalization</c>）按语言合并为一次 <c>AddSource</c>，每语言仅注册一次，
        /// 降低非英文环境下 <c>en-US</c> 触发引擎无锁 fallback 字典合并（<c>MergeFrom</c>）的竞争窗口。</para>
        ///
        /// <para><b>為什麼要 try-catch</b>：本地化註冊失敗的實際影響只是「該語言的文字顯示成
        /// key」，不該讓整個 Mod 失效。完整論證見
        /// <see cref="MapExt.Core.CompositeLocaleSource"/>。</para>
        /// </summary>
        private void RegisterLocalization()
        {
            ModLocalization.GetSources(out var dialogEn, out var dialogHans, out var dialogHant);
            var lm = GameManager.instance.localizationManager;

            int ok = 0;
            if (AddSourceSafe(lm, "en-US", new CompositeLocaleSource(new LocaleEN(m_Setting), dialogEn))) ok++;
            if (AddSourceSafe(lm, "zh-HANS", new CompositeLocaleSource(new LocaleHANS(m_Setting), dialogHans))) ok++;
            if (AddSourceSafe(lm, "zh-HANT", new CompositeLocaleSource(new LocaleHANT(m_Setting), dialogHant))) ok++;

            if (ok == 3)
            {
                ModLog.Ok(Tag, "本地化已注册 (en-US, zh-HANS, zh-HANT；设置文本与对话框文本已合并为每语言一份)");
            }
            else
            {
                ModLog.Warn(Tag, $"本地化仅 {ok}/3 个语言注册成功；未成功者其文字会显示为 key，功能不受影响");
            }
        }

        /// <summary>
        /// 包一層 try-catch 的 <c>AddSource</c>，回傳是否成功。
        /// <para>引擎的 <c>LocalizationDictionary</c> 是無鎖的裸 <c>Dictionary</c>，
        /// <c>AddSource</c> 可能因別的執行流同時寫入 fallback 字典而拋
        /// <c>Collection was modified</c>。那是引擎缺陷、Mod 無從預防，
        /// 這裡只確保它不擴散成整個 OnLoad 中斷。</para>
        /// </summary>
        private static bool AddSourceSafe(
            Colossal.Localization.LocalizationManager lm, string localeId, Colossal.IDictionarySource source)
        {
            try
            {
                lm.AddSource(localeId, source);
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"注册 {localeId} 本地化失败（引擎本地化字典无锁，疑撞上并发写入）: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 初始化中斷時推一則主菜單通知，讓「Mod 沒生效」變成可見事件。
        /// <para>文案<b>硬編碼英文</b>並用 <c>LocalizedString.Value</c>（字面值、不查表）：
        /// 中斷點有可能正是本地化註冊本身，那時查表必然拿不到譯文。</para>
        /// </summary>
        private static void PushInitFailureNotification(Exception ex)
        {
            try
            {
                NotificationSystem.Push(
                    identifier: "mapext.init_failed",
                    title: LocalizedString.Value("MapExt: INITIALIZATION FAILED"),
                    text: LocalizedString.Value(
                        "MapExt did not finish loading, so map extension is NOT fully active this session. " +
                        "Do NOT load or save an extended-size city now - save corruption is possible. " +
                        $"Restart the game; if it repeats, report Logs/MapExtPDX.log ({ex.GetType().Name})."),
                    progressState: Colossal.PSI.Common.ProgressState.Failed,
                    progress: 100
                );
            }
            catch (Exception pushEx)
            {
                // 連通知都推不出去（UI 尚未就緒等），至少讓日誌留痕
                ModLog.Warn(Tag, $"初始化失败通知推送失败: {pushEx.Message}");
            }
        }

        /// <summary>
        /// 挂一个存档闸门补丁类。闸门缺一层，模式不符的存档就可能绕过校验读进来，所以失败按关键处理：
        /// 记错误并让本会话进入降级（见 <see cref="MapExtHealth"/>）。
        /// </summary>
        private static void ApplySaveGatePatch(string name, Action apply)
        {
            try
            {
                apply();
#if DEBUG
                // 故障注入：补丁照常挂上后再抛出，只验证降级信号
                if (MapExtHealth.IsFaultInjected($"SaveGate/{name}"))
                    throw new InvalidOperationException("Fault injected (DEBUG self-test)");
#endif
                ModLog.Patch(Tag, $"{name} 已应用");
            }
            catch (Exception ex)
            {
                MapExtHealth.MarkDegraded($"SaveGate/{name}", ex.Message);
            }
        }

        /// <summary>
        /// 把 <see cref="StartupKeys"/>（与外部工具 MapExTools 共用的定义）和本 Mod 的实际定义逐项比对：
        /// 全部受管键的默认值与枚举定义、各模式 CoreValue、设定块名、原版地图边长、原版存档判定值。
        /// 不一致只记 Warn，不影响加载——后果是工具显示的默认值或存档判定与 Mod 不符。
        /// </summary>
        /// <param name="defaults">传给 <c>LoadSettings</c> 的默认值对象（构造时已执行 <c>SetDefaults</c>）。</param>
        private static void VerifyStartupKeys(ModSettings defaults)
        {
            try
            {
                var mismatches = new System.Collections.Generic.List<string>();

                // --- 全部受管键：属性存在、类型相符、枚举序列与默认值一致 ---
                foreach (StartupKeyInfo key in StartupKeys.AllKeys)
                {
                    PropertyInfo prop = typeof(ModSettings).GetProperty(key.Name, BindingFlags.Instance | BindingFlags.Public);
                    if (prop == null)
                    {
                        mismatches.Add($"{key.Name}: ModSettings 没有该属性");
                        continue;
                    }

                    object actual = prop.GetValue(defaults);
                    bool same;
                    switch (key.Kind)
                    {
                        case StartupKeyKind.Mode:
                            same = prop.PropertyType == typeof(PatchModeSetting) && actual.ToString() == (string)key.DefaultValue;
                            break;
                        case StartupKeyKind.Enum:
                            same = prop.PropertyType.IsEnum && actual.ToString() == (string)key.DefaultValue;
                            if (prop.PropertyType.IsEnum && key.EnumNames != null)
                            {
                                string[] names = Enum.GetNames(prop.PropertyType);
                                if (string.Join(",", names) != string.Join(",", key.EnumNames))
                                {
                                    mismatches.Add($"{key.Name} 枚举名或顺序: StartupKeys=[{string.Join(",", key.EnumNames)}], 实际=[{string.Join(",", names)}]");
                                }
                            }
                            break;
                        case StartupKeyKind.Bool:
                            same = prop.PropertyType == typeof(bool) && Equals(actual, key.DefaultValue);
                            break;
                        case StartupKeyKind.Int:
                            same = prop.PropertyType == typeof(int) && Equals(actual, key.DefaultValue);
                            break;
                        case StartupKeyKind.Float:
                            same = prop.PropertyType == typeof(float)
                                   && actual is float af
                                   && key.DefaultValue is float kf
                                   && Math.Abs(af - kf) < 1e-6f;
                            break;
                        default:
                            same = false;
                            break;
                    }

                    if (!same)
                        mismatches.Add($"{key.Name}: StartupKeys={key.DefaultValue}, ModSettings={actual} ({prop.PropertyType.Name})");
                }

                // --- 模式枚举名、顺序与 CoreValue ---
                string[] enumNames = Enum.GetNames(typeof(PatchModeSetting));
                if (string.Join(",", enumNames) != string.Join(",", StartupKeys.ModeNames))
                    mismatches.Add($"PatchModeSetting 枚举名或顺序: StartupKeys=[{string.Join(",", StartupKeys.ModeNames)}], 实际=[{string.Join(",", enumNames)}]");

                foreach (StartupModeInfo mode in StartupKeys.Modes)
                {
                    if (!Enum.TryParse(mode.Name, false, out PatchModeSetting value))
                    {
                        mismatches.Add($"{mode.Name}: PatchModeSetting 没有该枚举名");
                        continue;
                    }

                    int actualCV = PatchManager.GetCoreValueForMode(value);
                    if (actualCV != mode.CoreValue)
                        mismatches.Add($"{mode.Name} CoreValue: StartupKeys={mode.CoreValue}, PatchManager={actualCV}");
                }

                // --- 其余常量（经 object 比较：两边都是编译期常量，直接写 != 会被判为不可达代码） ---
                AddIfDiffer(mismatches, "设定块名 (Mod.ModName)", StartupKeys.CocBlockName, ModName);
                AddIfDiffer(mismatches, "原版边长 (PatchManager.OriginalMapSize)",
                    StartupKeys.OriginalMapSize, PatchManager.OriginalMapSize);
                AddIfDiffer(mismatches, "原版存档判定值 (VanillaConversionState)",
                    StartupKeys.UntaggedVanillaCoreValue, VanillaConversionState.UntaggedVanillaCoreValue);

                if (mismatches.Count == 0)
                    ModLog.Ok(Tag, $"StartupKeys 自检通过（{StartupKeys.AllKeys.Length} 个键、{StartupKeys.Modes.Length} 个模式）");
                else
                    ModLog.Warn(Tag, $"StartupKeys 与 Mod 定义不一致 {mismatches.Count} 处，MapExTools 的默认值或存档判定会出错: " +
                                     string.Join("; ", mismatches));
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"StartupKeys 自检异常，已跳过: {ex.Message}");
            }
        }

        private static void AddIfDiffer(System.Collections.Generic.List<string> mismatches, string label, object shared, object actual)
        {
            if (!Equals(shared, actual))
                mismatches.Add($"{label}: StartupKeys={shared}, 实际={actual}");
        }

        public void OnDispose()
        {
            ModLog.Info(Tag, nameof(OnDispose));

            IsUnloading = true;
            VanillaConversionState.Clear("Mod 卸载");

            // 在Mod卸载时移除所有补丁
            // 必須傳入自己的 Harmony id：Harmony 的 UnpatchAll(null) 會對每個 patch 的
            // owner 檢查一律放行，且它遍歷的 GetAllPatchedMethods() 作用範圍是整個
            // appdomain——不傳 id 等於剝除進程內所有 Mod 的 patch，不只自己的。
            // 危害路径：ModManager 在单一 Mod 初始化失败时只 Dispose 该 Mod，
            // 此时此前已载入的 Mod 补丁会被静默清空。
            // （Harmony 2.2.2 無 UnpatchSelf，傳自身 id 是唯一手段。）
            _globalPatcher?.UnpatchAll(HarmonyIdGlobal);
            _modePatcher?.UnpatchAll(HarmonyIdModes);
            _globalPatcher = null;
            _modePatcher = null;
            ModLog.Ok(Tag, "所有 Harmony 补丁已移除");

            // 卸载Settings
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    } // class Mod
} // namespace MapExtPDX
