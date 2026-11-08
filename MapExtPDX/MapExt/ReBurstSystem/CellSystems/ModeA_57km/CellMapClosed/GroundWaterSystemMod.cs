// Game.Simulation.GroundWaterSystem : CellMapSystem<GroundWater>, IJobSerializable

using Colossal.Collections;
using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using Game.Simulation;
using HarmonyLib;
using MapExtPDX.CellShared;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.ModeA
{
    // =========================================================================================
    // [配置区域]
    // =========================================================================================
    // 1. 基类泛型
    using BaseCellMapSystem = CellMapSystem<GroundWater>;
    // 2. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<GroundWater>;
    // 3. 原版系统类型 (用于禁用和定位)
    using TargetSystem = GroundWaterSystem;
    // 4. T struct
    using TargetType = GroundWater;
using MapExtPDX.MapExt.Core;
using MapExtPDX.SaveLoadSystem;
    // =========================================================================================

    /// <summary>
    /// 地下水系 地下水流动和污染扩散，并定期补充地下水资源
    /// </summary>
    // 降级保护（重构二 0.4）：合并版存档里本类的类型名是 MapExtPDX.CellMap.GroundWaterSystemMod，退回本版本时靠此别名读回，勿删
    [Colossal.Serialization.Entities.FormerlySerializedAs("MapExtPDX.CellMap.GroundWaterSystemMod")]
    public partial class GroundWaterSystemMod : CellMapReplacementSystem<TargetType, TargetSystem>
    {
        private const string Tag = nameof(GroundWaterSystemMod);

        #region 常量和静态字配置
        // 纹理尺寸(vanilla=256)
        // orgTextureSize 讀的是原版 static readonly 欄位；本 Mod 自己的組件不受
        // PatchSet3CellMapFields 的 ldsfld 替換（它只掃 Game 程序集），所以這裡拿到的是真正的 256，
        // 可用來核對 OnGameLoaded 從原版系統取到的來源尺寸是否如預期。
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.GroundWaterSystemkTextureSize; // mod尺寸

        // 基类参数：日志 Tag、本模式的贴图边长与地图边长（XCellMapSystemRe 按模式命名空间解析）
        protected override string LogTag => Tag;
        protected override int ModTextureSize => kTextureSize;
        protected override int MapSize => XCellMapSystemRe.kMapSize;

        // 尺寸不符时最近邻重采样、不丢弃，保留玩家的抽用与污染状态。丢弃后 OnGameLoaded 只能从原版原稿升采样，
        // 而 MapExt 编辑器做的地图原稿为空，地下水会整张清空（ModeC 标定计划 §2.4）。重采样后场非空，OnGameLoaded 照常沿用
        protected override SizeMismatchPolicy MismatchPolicy => SizeMismatchPolicy.NearestResample;

        // === 系統更新週期 ===
        // [MOD FIX] 原版 GroundWaterSystem.GetUpdateInterval 硬編碼 return 128，
        // 即每 128 模擬幀一次 = 每天 262144 / 128 = 2048 次。
        // 官方 WaterPipeParametersPrefab 的 tooltip 亦寫明 "per tick (2048 ticks per day)"。
        // 早期版本把 interval 值（128）誤當成每日次數，套用其他 CellMap 系統的
        // 262144 / kUpdatesPerDay 模板後得到 interval = 2048 → 每天僅 128 次，
        // 導致 m_GroundwaterPurification / m_GroundwaterReplenish 的施加頻率只有原版 1/16：
        // 地下水污染幾乎不消退（注入端 GroundWaterPollutionSystem 仍是每天 2048 次），
        // 抽乾的井恢復需約兩天遊戲時間。此處修正為原版語意。
        public static readonly int kUpdatesPerDay = 2048;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;
        public override int GetUpdateOffset(SystemUpdatePhase phase) => 64;

        // 最大阈
        public const int kMaxGroundWater = 10000;
        public const int kMinGroundWaterThreshold = 500;
        #endregion

        #region 查询和系统引
        private EntityQuery m_ParameterQuery;
        #endregion

        #region System Loop
        protected override void OnCreate()
        {
            // 1–2. 禁用原版系统、登记 Instance、创建自定义大小纹理由基类完成
            base.OnCreate();

            // 3. 获取其他依赖和查
            this.m_ParameterQuery = GetEntityQuery(ComponentType.ReadOnly<WaterPipeParameterData>());
        }

        protected override void OnUpdate()
        {
            // 暂存要跨三趟并行 Job、跨线程共用，Job 内的 Temp 用不了，所以用 TempJob，随 Job 链在末尾释放。
            // 超过 TempJob 块大小（2 MB）的分配实际走默认堆，与 Persistent 同价；选 TempJob 只为自动释放（v4.12.0 整合计划 §1.4）。
            // 不清零（4.3）：趟2 对每格无条件写入暂存（GroundWaterFlowGatherJob），趟3 读到的都是趟2 写过的值。
            // 原版是散射写法，每条边同时累加到两端，暂存才必须先清零
            NativeArray<int2> scratchMap = new NativeArray<int2>(m_Map.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            // 趟1 的结果：每格的污染再分配增量（趟2 写暂存时把它并进去）
            NativeArray<int> pollutionDelta = new NativeArray<int>(m_Map.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            // === 趟1：污染在邻格之间再分配（ModeC 标定计划 4.4）。每格汇总自己四条边的结果，只读主图，
            // 整数加法与顺序无关，与原写法逐位相同，可以并行
            GroundWaterPollutionGatherJob pollutionJob = new()
            {
                m_GroundWaterMap = m_Map,
                m_PollutionDelta = pollutionDelta
            };

            // === 趟2：水量与污染随水位差流动（4.4）。水量只取决于主图，与原写法逐位相同；
            // 随水带走的污染比例改用本趟开始时的值（原写法用扫描到这条边时的滚动值），
            // 每条边从两端算出的量一致，守恒精确，统计上等价
            GroundWaterFlowGatherJob flowJob = new()
            {
                m_GroundWaterMap = m_Map,
                m_PollutionDelta = pollutionDelta,
                m_TempMap = scratchMap
            };

            // === [MOD OPT] 趟3 套用：每格只讀寫自己的 [k]、無隨機 → 拆成 IJobParallelFor（位元級等價）。
            // batch = kTextureSize：每個 batch 一整行，與 AirPollutionSystemMod 趟1 的拆法一致。
            GroundWaterApplyJob applyJob = new()
            {
                m_GroundWaterMap = m_Map,
                m_TempMap = scratchMap,
                m_Parameters = m_ParameterQuery.GetSingleton<WaterPipeParameterData>()
            };

            JobHandle inputDeps = JobHandle.CombineDependencies(m_WriteDependencies, m_ReadDependencies, Dependency);

            JobHandle pollutionHandle = pollutionJob.Schedule(m_Map.Length, kTextureSize, inputDeps);
            JobHandle flowHandle = flowJob.Schedule(m_Map.Length, kTextureSize, pollutionHandle);
            Dependency = applyJob.Schedule(m_Map.Length, kTextureSize, flowHandle);

            AddWriter(Dependency);

            Dependency = JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies, Dependency);

            // [关键] 注册 TempJob 的自动释
            // 这告Unity：当 Dependency (即这Job) 完成后，自动调用 scratchMap.Dispose()
            // 无需手动管理生命周期，也不会阻塞主线程
            // 必須掛在最後一個 Job（趟3）上——掛在前面的 Job 上會在趟3 讀取期間被釋放。
            scratchMap.Dispose(Dependency);
            pollutionDelta.Dispose(Dependency);
        }

        // === 為什麼沒有 SetDefaults 覆寫（勿再補回） ===
        // v4.8.0 開發期間曾有一版覆寫，在 purpose == NewGame 時以原版的 Perlin 公式生成
        // 程序化含水層。2026-08-28 遊戲內實測後移除。當時寫了兩個理由，第 1 條不成立：
        //
        //   1. （2026-11-02 更正，v4.12.0 整合计划 §1.7）原先说"它从未生效：SetDefaults 收到的
        //      purpose 是 LoadMap/LoadGame"，这不对。GameManager.Load 建的 Context 就是 NewGame，
        //      反序列化器沿用传入的 purpose，并用同一个 Context 给地图里缺席的系统调 SetDefaults
        //      （EntityDeserializer.cs:621-625）；SerializerSystem 只在什么都读不到时才改写 purpose
        //      （SerializerSystem.cs:130-135）。原版地图里没有本模式的区段，开新城时本类的
        //      SetDefaults 收到的就是 NewGame，那一版覆写会生成 Perlin 含水层。2026-11-04 验收 V10 实证：
        //      同一条路径下，NaturalResourceSystemMod 的 SetDefaults 按 Perlin 把自然资源铺满了整张图。
        //      移除的理由只剩第 2 条，结论不变。
        //
        //   2. 原版同樣不生成。原版 GroundWaterSystem.SetDefaults（GroundWaterSystem.cs:266）
        //      的守衛比這裡更嚴——多一個 context.version < Version.timoSerializationFlow，
        //      那是為了只補償「還沒有 GroundWater 序列化區段」的舊年代地圖檔。較寬的條件都不
        //      成立，更嚴的必然也不成立。原版的地下水一律來自地圖作者畫在地圖檔裡的資料，
        //      而原版 GroundWaterSystem 連 OnGameLoaded 覆寫都沒有，沒有任何兜底生成路徑。
        //
        // 所以本 Mod 也不生成：作者沒畫就是沒有，與原版語意一致。作者畫過的含水層由
        // OnGameLoaded 以原版原稿升採樣還原——那才是 v4.8.0 真正要修的缺陷（65536 ≠ 1048576
        // 導致原稿在 CellMapReplacementSystem.Deserialize 中被丟棄）。
        //
        // 副作用是正面的：Perlin 場覆蓋率約 22.7%，會讓零格早退命中率從近 100% 掉到 60–72%，
        // GroundWaterTickJob 單次成本約從 12ms 升到 20–25ms（每 128 幀一次）。不生成即省下這筆。
        //
        // 基類 CellMapSystem<T>.SetDefaults 會把場清零，這正是需要的行為，故不覆寫。

        /// <summary>
        /// 載入後把地圖作者實際繪製的含水層補進本模式的擴展貼圖。
        ///
        /// <para><b>資料來源是原版系統的 m_Map</b>：原版 <c>GroundWaterSystem</c> 仍留在 world 裡、
        /// 仍實作 <c>IJobSerializable</c>，所以它照常參與序列化——地圖編輯器的地下水筆刷
        /// （<c>ApplyBrushesSystem.cs:246</c> 的 <c>TerraformingTarget.GroundWater</c>）畫下的
        /// <c>orgTextureSize²</c> 資料會完整讀進它的 <c>m_Map</c>。它被 <c>Enabled = false</c>，
        /// 且 GetMap／GetData／AddReader／AddWriter 四個入口全被本類的 <see cref="Patches"/>
        /// 重定向到本實例，所以那份資料在遊戲中沒有任何寫入者，等於唯讀的作者原稿；
        /// 存檔時它也照樣被寫出，連 v4.8.0 之前的 MapExt 存檔裡都留著這份原稿。</para>
        ///
        /// <para><b>不能用 <c>originalSystem.GetMap()</c> 取</b>：那個呼叫會被
        /// <see cref="Patches.GetMapPrefix"/> 攔下並回傳本實例的 buffer。必須反射讀
        /// <c>CellMapSystem&lt;GroundWater&gt;.m_Map</c>（protected 且宣告在共同基類，跨型別直接存取不到），
        /// 作法與 <c>VanillaSaveConversionSystem.ExecuteGroundWaterInit</c> 相同。</para>
        ///
        /// <para><b>為何是 <c>OnGameLoaded</c> 而非 <c>IPostDeserialize</c></b>：後者不會被反序列化器
        /// 自動派發，得另外註冊 <c>PostDeserialize&lt;T&gt;</c> 包裝系統
        /// （<c>Game/Serialization/PostDeserialize.cs</c>）；<c>SystemReplacer</c> 只為
        /// NaturalResource 與 SoilWater 註冊過，GroundWater 沒有，所以 v4.8.0 之前掛在
        /// <c>PostDeserialize(Context)</c> 的兜底補生成從未執行過。而且那個時點在 Deserialize 階段
        /// <b>內</b>，原版系統的反序列化 job 未必收斂；<c>OnGameLoaded</c> 由
        /// <c>GameSystemBase.OnCreate</c> 掛上 <c>LoadGameSystem.onOnSaveGameLoaded</c>，
        /// 而 <c>LoadGameSystem.OnUpdate</c> 是跑完整個 Deserialize 階段才 Invoke，
        /// 此時讀原版 m_Map 才安全。</para>
        ///
        /// <para><b>三條路徑的處置</b>：本模式解析度的場只要非空就一律沿用、不分 purpose——
        /// 既有存檔裡那是玩家已經在抽用、已累積污染的即時狀態；MapExt 編輯器製作的地圖檔裡
        /// 那是作者直接畫在 <c>kTextureSize²</c> 上的原稿（筆刷經 GetData 重定向寫進本實例，
        /// 原版 256² 區段從未被寫入）。場為空而原版原稿非空才升採樣還原（原版地圖開新城、
        /// 編輯器載入原版地圖、v4.8.0 之前存下的空場存檔都走這條）；兩者皆空時
        /// <b>不生成任何替代場</b>，只記一行提示（理由見本方法上方的
        /// 「為什麼沒有 SetDefaults 覆寫」註釋段）。三條路徑之前另有一道會話白名單，
        /// 把退出主菜單那輪 <c>Purpose.Cleanup</c> 擋在外面。</para>
        /// </summary>
        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);

            // 原版存檔轉換路徑由 VanillaSaveConversionSystem 全權處理：它會清空本場、再把原版
            // orgTextureSize² 資料 1:1 嵌入中心，與這裡的等比拉伸取向互斥（該路徑的地形也是
            // 「原始細節降採樣後嵌入中心」而非拉伸），直接讓路。原版地图开新城转换同样让路。
            // 兩者的 OnGameLoaded 誰先跑取決於系統建立順序，但正確性不依賴它：转换请求在整次转换读档期间
            // （加载中、转换中、保存中）都算数；即使不算，下面「場非空就不動」的判據也會讓嵌入結果原樣保留。
            // 地形尺寸不符而被否决的请求，在反序列化阶段已转为"已取消"，不算转换读档，这里照常补场。
            if (VanillaConversionState.IsConversionLoad) return;

            // 只在真正的遊戲／地圖編輯器會話介入。退出到主菜單時引擎會跑一輪 Purpose.Cleanup 的
            // 反序列化（`ConflictMonitoringSystem.OnGamePreload` 就是靠這個 purpose 重置 PatchSet
            // 會話狀態），本方法會被一併呼叫，然後對一張正在拆掉的全零場報「本地圖沒有含水層」
            // ——純噪音，實測每次退出主菜單重現一次。白名單與 PatchSet4AirwaySystemPatch 一致。
            Purpose purpose = serializationContext.purpose;
            if (purpose != Purpose.NewGame && purpose != Purpose.LoadGame &&
                purpose != Purpose.NewMap && purpose != Purpose.LoadMap)
            {
                return;
            }

            if (!m_Map.IsCreated) return;
            // 下面既讀 m_Map 也可能整張改寫：讀寫兩側的掛起 job 都要收斂。
            // 只等 m_WriteDependencies 擋得住寫入者，擋不住仍在讀本場的 job。
            JobHandle.CombineDependencies(m_WriteDependencies, m_ReadDependencies).Complete();

            int nonZero = CountAquiferCells(m_Map);

            // 原稿無條件先讀一次（唯讀，只多一次反射與一趟 orgTextureSize² 計數）：它是下面兩條
            // 分支唯一的資料來源，而「修復 v4.8.0 之前存下的空場」那條至今從未在日誌裡出現過。
            // 城市存檔的 GroundWater 由本 Mod 寫成 kTextureSize²，原版系統反序列化時長度與自身的
            // orgTextureSize² 不符會保持全零 → 原稿可能根本活不到城市存檔裡，那樣「空場修復」
            // 就是一條走不到的路（對外 ChangeLog 有承諾，見 CellMap_GroundWaterZeroField §5.1）。
            // 健康場早退分支一併記下原稿格數即可判定：讀到約源圖格數 = 前提成立；0 格 = 入口不可達。
            bool hasVanilla = TryGetVanillaAquifer(out NativeArray<TargetType> vanillaMap, out int srcSize);
            int vanillaCells = hasVanilla ? CountAquiferCells(vanillaMap) : 0;

            // 本模式解析度的場非空 → 不分 purpose 一律沿用。兩種來源都不能動：
            //   · 既有存檔：玩家已在抽用、已累積污染的即時狀態；
            //   · MapExt 編輯器製作的地圖檔：作者用地下水筆刷直接畫在 kTextureSize² 上
            //     （GetData 被重定向到本實例，原版 256² 區段從未被寫入）。
            // 開新城若不認這條，前者會被誤報成「本地圖沒有含水層」；而地圖源自原版、
            // 在編輯器升採樣後又修改過的場，還會被原版 256² 原稿重新升採樣覆蓋回去。
            if (nonZero != 0)
            {
                // 這條分支原本完全靜默，於是「沒有日誌」同時可能代表「場是健康的」與
                // 「OnGameLoaded 根本沒走到」，無從分辨。而含水層占比又是零格早退命中率
                // 與地下水各趟 Job 成本的唯一決定因素，所以無條件記一行。
                string source = purpose == Purpose.LoadGame ? "存档" : "地图文件";
                ModLog.Info(Tag,
                    $"地下水场沿用{source}内容，不介入：{kTextureSize}² = {m_Map.Length} 格，" +
                    $"含水层 {nonZero} 格（{100f * nonZero / m_Map.Length:F2}%）；" +
                    $"原版原稿 {(hasVanilla ? $"{srcSize}²：含水层 {vanillaCells} 格" : "不可读取")}");
                return;
            }

            if (vanillaCells == 0)
            {
                // 與原版一致：地圖作者沒畫地下水就是沒有，不生成任何替代場。
                // 這不是缺陷，所以用 Info 而非 Warn——它在無地下水的地圖上每次載入都會出現。
                ModLog.Info(Tag,
                    $"本地图没有含水层（{m_Map.Length} 格 m_Max 全为 0，原版系统亦无可用的作者原稿），" +
                    $"与原版行为一致。地下水泵在此不会出水；需要地下水请用地图编辑器或地下水画笔绘制。");
                return;
            }

            UpsampleAquifer(vanillaMap, srcSize);

            // 前後占比一併輸出：等比拉伸不改變含水層占比，兩者接近即證明座標映射沒有錯位
            // （差異只該來自邊界插值與 short 取整）。這是唯一不必進遊戲就能核對升採樣正確性的訊號。
            int filled = CountAquiferCells(m_Map);
            // 只有載入既有存檔時走到這裡才是「修復空場」：v4.8.0 之後存下的場非空會在上面早退。
            // 開新城與編輯器載入地圖走到這裡是正常的首次還原，不是修復。
            ModLog.Ok(Tag,
                $"地下水场已从原版 {srcSize}² 原稿升采样至 {kTextureSize}²（双线性等比拉伸，与地形同比例）" +
                (purpose == Purpose.LoadGame ? "，修复 v4.8.0 之前存下的空场" : "") +
                $"：含水层占比 {100f * vanillaCells / vanillaMap.Length:F2}% → {100f * filled / m_Map.Length:F2}%" +
                $"（{vanillaCells} → {filled} 格）");
        }

        /// <summary>
        /// 統計整張圖有多少格具備含水層容量（<c>m_Max != 0</c>）。
        /// <para><c>m_Max</c> 是唯一「只有反序列化與本次升採樣會寫、沒有任何執行期重生成器」的
        /// 靜態容量欄位（<c>GroundWaterPollutionSystem</c> 只寫 m_Polluted，<c>ConsumeGroundWater</c>
        /// 只減 m_Amount；唯一的例外是地圖編輯器的地下水畫筆 <c>ApplyBrushesSystem</c>），
        /// 所以它全為 0 等價於「這張圖從來沒有含水層」，而不是「水被抽乾了」。</para>
        /// </summary>
        private static int CountAquiferCells(NativeArray<TargetType> map)
        {
            int count = 0;
            for (int i = 0; i < map.Length; i++)
            {
                if (map[i].m_Max != 0) count++;
            }
            return count;
        }

        /// <summary>
        /// 反射取原版 <c>GroundWaterSystem</c> 的 <c>m_Map</c>（作者原稿），並先完成它的掛起依賴。
        /// <para>任何一步失敗都回 <c>false</c>，呼叫方據此視為「沒有原稿」（不生成任何替代場），
        /// 絕不讓異常逸出——本方法在 <c>OnGameLoaded</c> 內執行，逸出的異常會被
        /// <c>GameSystemBase.GameLoaded</c> 捕獲並順手 <c>Enabled = false</c>，
        /// 代價是整個地下水模擬被停用。</para>
        /// <para>三條分支都會呼叫它（含只用來記錄原稿格數的健康場早退），所以訊息措辭保持
        /// 與「是否真的要升採樣」無關。</para>
        /// </summary>
        private bool TryGetVanillaAquifer(out NativeArray<TargetType> map, out int size)
        {
            map = default;
            size = 0;
            try
            {
                var originalSystem = World.GetExistingSystemManaged<TargetSystem>();
                if (originalSystem == null) return false;

                // m_Map 與兩個依賴欄位都宣告在共同基類 CellMapSystem<GroundWater> 上
                var baseType = typeof(BaseCellMapSystem);
                var writeDeps = AccessTools.Field(baseType, "m_WriteDependencies");
                var readDeps = AccessTools.Field(baseType, "m_ReadDependencies");
                var mapField = AccessTools.Field(baseType, "m_Map");
                if (writeDeps == null || readDeps == null || mapField == null) return false;

                ((JobHandle)writeDeps.GetValue(originalSystem)).Complete();
                ((JobHandle)readDeps.GetValue(originalSystem)).Complete();

                map = (NativeArray<TargetType>)mapField.GetValue(originalSystem);
                if (!map.IsCreated || map.Length == 0) return false;

                size = (int)math.round(math.sqrt(map.Length));
                if (size <= 0 || size * size != map.Length)
                {
                    ModLog.Warn(Tag,
                        $"原版 GroundWater m_Map 长度 {map.Length} 不是完全平方数，无从对应坐标，视为无原稿");
                    return false;
                }
                if (size != orgTextureSize)
                {
                    // PatchSet3CellMapFields 掛在原版系統 OnCreate 之後（CreateSystems 先於
                    // InitializeModManager），照理打不中那句 CreateTextures(kTextureSize)。
                    // 真的變了不影響正確性（升採樣會退化為等比縮放），但值得留痕。
                    ModLog.Warn(Tag,
                        $"原版 GroundWater 纹理为 {size}² 而非预期的 {orgTextureSize}²，仍按 {size}² 换算");
                }
                return true;
            }
            catch (System.Exception ex)
            {
                ModLog.Warn(Tag,
                    $"取原版 GroundWater m_Map 失败，视为无原稿: {ex.Message}");
                return false;
            }
        }

        /// <summary>把原版 <paramref name="srcSize"/>² 的含水層原稿等比拉伸到本模式的 <c>kTextureSize²</c>。</summary>
        private void UpsampleAquifer(NativeArray<TargetType> src, int srcSize)
        {
            new UpsampleAquiferJob
            {
                m_Src = src,
                m_SrcSize = srcSize,
                m_DstSize = kTextureSize,
                m_Dst = m_Map
            }.Schedule(m_Map.Length, 1024).Complete();
        }
        #endregion

        #region 汇总写法（ModeC 标定计划 4.4）

        /// <summary>
        /// 邻格成对计算：与原版 <c>GroundWaterTickJob</c> 的 HandlePollution／HandleFlow 同一套公式，
        /// 改成纯函数，供按格汇总的两个 Job 从一对的两端分别调用。两端的输入相同，算出的量也相同，增减正好抵消。
        /// </summary>
        private static class GroundWaterPairs
        {
            /// <summary>三个量全为 0。一对格子都为空时，再分配与流动的量恒为 0，可以跳过（零格早退，逐位等价）。</summary>
            public static bool IsEmpty(TargetType g)
            {
                return g.m_Amount == 0 && g.m_Polluted == 0 && g.m_Max == 0;
            }

            /// <summary>HandlePollution 的 num3：(a, b) 这一对里 a 增加、b 减少的污染量。两格全为 0 时恒为 0。</summary>
            public static int PollutionTransfer(TargetType a, TargetType b)
            {
                if (IsEmpty(a) && IsEmpty(b)) return 0;
                int num = a.m_Polluted + b.m_Polluted;
                int num2 = a.m_Amount + b.m_Amount;
                return math.clamp((((num2 > 0) ? (a.m_Amount * num / num2) : 0) - a.m_Polluted) / 4,
                    -(b.m_Amount - b.m_Polluted) / 4, (a.m_Amount - a.m_Polluted) / 4);
            }

            /// <summary>
            /// HandleFlow：(a, b) 这一对里 a 增加的（水量, 污染）。水量 num4 只取决于主图；
            /// 带走的污染按流出一方的污染比例计，比例用本趟开始时的值（水量增量取 0，污染增量取趟1 的结果）。
            /// </summary>
            public static int2 Flow(TargetType a, int pollutionDeltaA, TargetType b, int pollutionDeltaB)
            {
                if (IsEmpty(a) && IsEmpty(b)) return int2.zero;
                int num3 = a.m_Amount - a.m_Max;
                int num4 = math.clamp((b.m_Amount - b.m_Max - num3) / 4, -a.m_Amount / 4, b.m_Amount / 4);
                int num5 = 0;
                if (num4 > 0)
                {
                    num5 = (int)((float)num4 * PollutedRatio(b, pollutionDeltaB));
                }
                else if (num4 < 0)
                {
                    num5 = (int)((float)num4 * PollutedRatio(a, pollutionDeltaA));
                }
                return new int2(num4, num5);
            }

            /// <summary>HandleFlow 的 num／num2：(m_Polluted + tmp.y) / (m_Amount + tmp.x)，这里 tmp.x 取 0。</summary>
            private static float PollutedRatio(TargetType g, int pollutionDelta)
            {
                return (g.m_Amount != 0) ? (1f * (float)(g.m_Polluted + pollutionDelta) / (float)g.m_Amount) : 0f;
            }
        }

        /// <summary>
        /// 趟1：每格汇总自己四条边的污染再分配。本格是一对里的前一格（右邻、下邻）时增加 num3，
        /// 是后一格（左邻、上邻）时减少 num3；只读主图，整数加法与顺序无关，与原写法逐位相同。
        /// </summary>
        [BurstCompile]
        private struct GroundWaterPollutionGatherJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<TargetType> m_GroundWaterMap;

            [WriteOnly] public NativeArray<int> m_PollutionDelta;

            public void Execute(int index)
            {
                int x = index % kTextureSize;
                int y = index / kTextureSize;
                TargetType self = m_GroundWaterMap[index];
                int delta = 0;
                if (x < kTextureSize - 1) delta += GroundWaterPairs.PollutionTransfer(self, m_GroundWaterMap[index + 1]);
                if (y < kTextureSize - 1) delta += GroundWaterPairs.PollutionTransfer(self, m_GroundWaterMap[index + kTextureSize]);
                if (x > 0) delta -= GroundWaterPairs.PollutionTransfer(m_GroundWaterMap[index - 1], self);
                if (y > 0) delta -= GroundWaterPairs.PollutionTransfer(m_GroundWaterMap[index - kTextureSize], self);
                m_PollutionDelta[index] = delta;
            }
        }

        /// <summary>
        /// 趟2：每格汇总自己四条边的水量与污染流动，连同趟1 的污染增量写进暂存（每格都写）。
        /// 水量与原写法逐位相同；污染比例用本趟开始时的值，与原写法的滚动值不同，统计上等价。
        /// </summary>
        [BurstCompile]
        private struct GroundWaterFlowGatherJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<TargetType> m_GroundWaterMap;

            [ReadOnly] public NativeArray<int> m_PollutionDelta;

            [WriteOnly] public NativeArray<int2> m_TempMap;

            public void Execute(int index)
            {
                int x = index % kTextureSize;
                int y = index / kTextureSize;
                TargetType self = m_GroundWaterMap[index];
                int selfDelta = m_PollutionDelta[index];
                int2 delta = new int2(0, selfDelta);
                if (x < kTextureSize - 1)
                {
                    int other = index + 1;
                    delta += GroundWaterPairs.Flow(self, selfDelta, m_GroundWaterMap[other], m_PollutionDelta[other]);
                }
                if (y < kTextureSize - 1)
                {
                    int other = index + kTextureSize;
                    delta += GroundWaterPairs.Flow(self, selfDelta, m_GroundWaterMap[other], m_PollutionDelta[other]);
                }
                if (x > 0)
                {
                    int other = index - 1;
                    delta -= GroundWaterPairs.Flow(m_GroundWaterMap[other], m_PollutionDelta[other], self, selfDelta);
                }
                if (y > 0)
                {
                    int other = index - kTextureSize;
                    delta -= GroundWaterPairs.Flow(m_GroundWaterMap[other], m_PollutionDelta[other], self, selfDelta);
                }
                m_TempMap[index] = delta;
            }
        }

        #endregion

        #region GroundWaterApplyJob
        /// <summary>
        /// 趟3：把前兩趟累積在 <c>m_TempMap</c> 的增量套用回主圖，並施加每 tick 的補充與淨化。
        ///
        /// <para><b>為何可以平行</b>：每個 index 只讀寫 <c>m_GroundWaterMap[k]</c> 與唯讀的
        /// <c>m_TempMap[k]</c>，格與格之間無耦合、無隨機數 → 逐位等價，
        /// 且每個 index 只寫自己那格，不需要 <c>[NativeDisableParallelForRestriction]</c>。</para>
        ///
        /// <para><b>為何同時加早退</b>：改造前這一趟是三趟中唯一沒有早退的，實測在 0.10%／0.34%／
        /// 10.62% 三種含水層覆蓋率下固定 4.86–4.91ms（浮動 &lt; 1%），是最大的一趟、也是每張地圖的
        /// 固定底價。原本判「早退的分支會破壞 SIMD，與平行化須擇一」，但實測即使在 10.62% 覆蓋率下
        /// 仍有 89% 的格子命中早退，該顧慮不成立，故兩者合併。</para>
        /// </summary>
        [BurstCompile]
        private struct GroundWaterApplyJob : IJobParallelFor
        {
            public NativeArray<TargetType> m_GroundWaterMap;
            [ReadOnly] public NativeArray<int2> m_TempMap;
            public WaterPipeParameterData m_Parameters;

            public void Execute(int k)
            {
                TargetType value = this.m_GroundWaterMap[k];
                int2 delta = this.m_TempMap[k];

                // === [MOD OPT] 零格早退（位元級等價）===
                // 五個量全為 0 時：
                //   m_Amount   = min(0 + 0 + ceil(replenish × 0), 0) = 0（不變）
                //   m_Polluted = clamp(0 + 0 - purification, 0, 0) = 0（不變；purification 正負皆然）
                // → 寫回的是同一個值，跳過即位元級等價。
                // 必須連 delta 一起判斷：趟1／趟2 的早退條件是「兩格全零才跳」，
                // 所以緊鄰含水層的零格仍可能收到增量，只看 map 值會漏掉它們。
                if (value.m_Amount == 0 && value.m_Polluted == 0 && value.m_Max == 0 &&
                    delta.x == 0 && delta.y == 0) return;

                value.m_Amount = (short)math.min(value.m_Amount + delta.x + math.ceil(this.m_Parameters.m_GroundwaterReplenish * (float)value.m_Max), value.m_Max); // 注意：Mathf改为Burst优化的math
                value.m_Polluted = (short)math.clamp(value.m_Polluted + delta.y - this.m_Parameters.m_GroundwaterPurification, 0, value.m_Amount);
                this.m_GroundWaterMap[k] = value;
            }
        }
        #endregion

        #region UpsampleAquiferJob
        /// <summary>
        /// 把原版尺寸的含水層原稿雙線性升採樣到本模式的擴展貼圖。
        ///
        /// <para><b>為何是等比拉伸而非置中嵌入</b>：一般載入路徑下地形是被拉伸的
        /// （<c>FinalizeTerrainData_Prefix</c> 對 <c>inMapSize</c> 乘上 CoreValue），
        /// 含水層必須跟著同樣拉伸才會與地形對齊。<c>VanillaSaveConversionSystem</c> 用的是 1:1
        /// 置中嵌入，那是因為該路徑的地形也是「原始細節降採樣後嵌入中心」——兩者各自與自己的
        /// 地形處理方式一致，不可互換，所以 <see cref="OnGameLoaded"/> 在該路徑下直接讓路。</para>
        ///
        /// <para>三個欄位各自獨立插值。<c>lerp</c> 是凸組合、<c>round</c> 單調，且三者共用同一組權重，
        /// 故原本的 <c>m_Polluted ≤ m_Amount ≤ m_Max</c> 不變量在插值後仍然成立
        /// （原版 <c>GroundWaterTickJob</c> 的 <c>Assert</c> 全靠這個不變量）。</para>
        ///
        /// <para>index 直接對應目標格，每個 index 只寫 <c>m_Dst[index]</c>，
        /// 因此不需要 <c>[NativeDisableParallelForRestriction]</c>。</para>
        /// </summary>
        [BurstCompile]
        private struct UpsampleAquiferJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<TargetType> m_Src;
            [ReadOnly] public int m_SrcSize;
            [ReadOnly] public int m_DstSize;
            [WriteOnly] public NativeArray<TargetType> m_Dst;

            public void Execute(int index)
            {
                int dx = index % m_DstSize;
                int dy = index / m_DstSize;

                // 目標格中心映射回源座標（-0.5 對齊格心，避免半格偏移）
                float su = (dx + 0.5f) * m_SrcSize / m_DstSize - 0.5f;
                float sv = (dy + 0.5f) * m_SrcSize / m_DstSize - 0.5f;
                int sx0 = (int)math.floor(su);
                int sy0 = (int)math.floor(sv);
                float fx = su - sx0;
                float fy = sv - sy0;

                // clamp to edge：越界的取樣點退化為複製最外圈，權重仍在 [0,1]
                int x0 = math.clamp(sx0, 0, m_SrcSize - 1);
                int x1 = math.clamp(sx0 + 1, 0, m_SrcSize - 1);
                int y0 = math.clamp(sy0, 0, m_SrcSize - 1) * m_SrcSize;
                int y1 = math.clamp(sy0 + 1, 0, m_SrcSize - 1) * m_SrcSize;

                TargetType c00 = m_Src[y0 + x0];
                TargetType c10 = m_Src[y0 + x1];
                TargetType c01 = m_Src[y1 + x0];
                TargetType c11 = m_Src[y1 + x1];

                m_Dst[index] = new TargetType
                {
                    m_Amount = (short)math.round(Bilinear(
                        c00.m_Amount, c10.m_Amount, c01.m_Amount, c11.m_Amount, fx, fy)),
                    m_Polluted = (short)math.round(Bilinear(
                        c00.m_Polluted, c10.m_Polluted, c01.m_Polluted, c11.m_Polluted, fx, fy)),
                    m_Max = (short)math.round(Bilinear(
                        c00.m_Max, c10.m_Max, c01.m_Max, c11.m_Max, fx, fy))
                };
            }

            private static float Bilinear(float v00, float v10, float v01, float v11, float fx, float fy)
                => math.lerp(math.lerp(v00, v10, fx), math.lerp(v01, v11, fx), fy);
        }
        #endregion

        #region HarmonyPatch
        // ==============================================================================
        // Harmony 补丁：非泛型薄壳，判断与转发在基类的 Redirect
        // （Harmony 2.2.2 按元数据令牌还原补丁方法，补丁方法不能声明在泛型类里）
        // ==============================================================================
        [HarmonyPatch]
        public static class Patches
        {
            // Patch: GetMap
            [HarmonyPatch(typeof(BaseCellMapSystem), "GetMap")]
            [HarmonyPrefix]
            public static bool GetMapPrefix(BaseCellMapSystem __instance,
                                      ref NativeArray<TargetType> __result,
                                      bool readOnly,
                                      ref JobHandle dependencies)
                => Redirect.GetMap(__instance, ref __result, readOnly, ref dependencies);

            // Patch: GetData
            [HarmonyPatch(typeof(BaseCellMapSystem), "GetData")]
            [HarmonyPrefix]
            public static bool GetDataPrefix(BaseCellMapSystem __instance,
                                      ref TargetCellMapData __result,
                                      bool readOnly,
                                      ref JobHandle dependencies)
                => Redirect.GetData(__instance, ref __result, readOnly, ref dependencies);

            // Patch: AddReader
            [HarmonyPatch(typeof(BaseCellMapSystem), "AddReader")]
            [HarmonyPrefix]
            public static bool AddReaderPrefix(BaseCellMapSystem __instance, JobHandle jobHandle)
                => Redirect.AddReader(__instance, jobHandle);

            // Patch: AddWriter
            [HarmonyPatch(typeof(BaseCellMapSystem), "AddWriter")]
            [HarmonyPrefix]
            public static bool AddWriterPrefix(BaseCellMapSystem __instance, JobHandle jobHandle)
                => Redirect.AddWriter(__instance, jobHandle);
        }
        #endregion

    } // mod class

} // mod namespace



