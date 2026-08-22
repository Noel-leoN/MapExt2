// Game.Simulation.GroundWaterSystem : CellMapSystem<GroundWater>, IJobSerializable

using System.Runtime.CompilerServices;
using Colossal.Collections;
using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using Game.Simulation;
using HarmonyLib;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.ModeB
{
    // =========================================================================================
    // [配置区域]
    // =========================================================================================
    // 1. 基类泛型
    using BaseCellMapSystem = CellMapSystem<GroundWater>;
    // 2. Mod 自定义系统类(当前
    using ModSystem = GroundWaterSystemMod;
    // 3. 数据包泛(用于 GetData)
    using TargetCellMapData = CellMapData<GroundWater>;
    // 4. 原版系统类型 (用于禁用和定
    using TargetSystem = GroundWaterSystem;
    // 5. T struct
    using TargetType = GroundWater;
using MapExtPDX.MapExt.Core;
using MapExtPDX.SaveLoadSystem;
    // =========================================================================================

    /// <summary>
    /// 地下水系 地下水流动和污染扩散，并定期补充地下水资源
    /// </summary>
    public partial class GroundWaterSystemMod : BaseCellMapSystem, IJobSerializable
    {
        public static ModSystem Instance { get; private set; }

        #region 常量和静态字配置
        // 纹理尺寸(vanilla=256)
        // orgTextureSize 讀的是原版 static readonly 欄位；本 Mod 自己的組件不受
        // PatchSet3CellMapFields 的 ldsfld 替換（它只掃 Game 程序集），所以這裡拿到的是真正的 256，
        // 可用來核對 OnGameLoaded 從原版系統取到的來源尺寸是否如預期。
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.GroundWaterSystemkTextureSize; // mod尺寸
        public int2 TextureSize => new int2(kTextureSize, kTextureSize);

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
            base.OnCreate();
            Instance = this;

            // 1.禁用原版系统并获取原版系统引
            // 使用 GetExistingSystemManaged 避免意外创建未初始化的系
            var originalSystem = World.GetExistingSystemManaged<TargetSystem>();
            if (originalSystem != null)
            {
                originalSystem.Enabled = false;
                // #if DEBUG
                Mod.Info($"[{typeof(ModSystem).Name}] 禁用原系�? {typeof(TargetSystem).Name}");
                // #endif
            }
            else
            {
                // 仅在调试时提示，原版系统可能已被其他Mod移除或尚未加
#if DEBUG
                Mod.Error($"[{typeof(ModSystem).Name}] 无法找到可禁用的原系�?尚未加载或可能被其他Mod移除): {typeof(TargetSystem).Name}");
#endif
            }

            // 2. 创建自定义大小纹
            CreateTextures(kTextureSize);
            // #if DEBUG
// [ENCODING_FIX]             Mod.Info($"[{typeof(ModSystem).Name}] 创建自定义纹 {typeof(TargetSystem).Name} kTextureSize 原值{TargetSystem.kTextureSize} 变更目标值{this.m_TextureSize.x}");
            // #endif

            // 3. 获取其他依赖和查
            this.m_ParameterQuery = GetEntityQuery(ComponentType.ReadOnly<WaterPipeParameterData>());
        }

        protected override void OnUpdate()
        {
            // [核心方案] 使用 Allocator.TempJob 分配临时缓冲
            // TempJob 允许大内存分8MB+)，且Persistent 快
            // 必须Job 完成Dispose
            NativeArray<int2> scratchMap = new NativeArray<int2>(m_Map.Length, Allocator.TempJob);

            GroundWaterTickJob groundWaterTickJob = default(GroundWaterTickJob);
            groundWaterTickJob.m_GroundWaterMap = m_Map;
            groundWaterTickJob.m_Parameters = m_ParameterQuery.GetSingleton<WaterPipeParameterData>();
            // 增加临时缓冲区引
            groundWaterTickJob.m_TempMap = scratchMap;

            GroundWaterTickJob jobData = groundWaterTickJob;

            Dependency = IJobExtensions.Schedule(jobData, JobHandle.CombineDependencies(m_WriteDependencies, m_ReadDependencies, Dependency));

            AddWriter(Dependency);

            Dependency = JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies, Dependency);

            // [关键] 注册 TempJob 的自动释
            // 这告Unity：当 Dependency (即这Job) 完成后，自动调用 scratchMap.Dispose()
            // 无需手动管理生命周期，也不会阻塞主线程
            scratchMap.Dispose(Dependency);
        }

        /// <summary>
        /// 新遊戲的地下水初始場生成（<b>基線</b>；地圖作者實際畫過的含水層由
        /// <see cref="OnGameLoaded"/> 以原版資料升採樣覆蓋）。
        ///
        /// <para><b>本覆寫是補回移植時遺漏的原版行為。</b>原版
        /// <c>GroundWaterSystem.SetDefaults</c>（<c>GroundWaterSystem.cs:264-283</c>）在
        /// <c>purpose == NewGame</c> 時以 Perlin 噪聲生成 <c>m_Amount = m_Max</c>。
        /// 而 <c>m_Max</c> 是全庫唯一「只有 SetDefaults 會寫、沒有任何重生成器」的靜態容量欄位
        /// （<c>GroundWaterPollutionSystem</c> 只寫 m_Polluted，<c>ConsumeGroundWater</c> 只減 m_Amount）。
        /// 早期移植沒帶上這個覆寫，於是擴展模式下整張圖恆為 0——趟3 的
        /// <c>m_Amount = min(..., m_Max)</c> 永遠算出 0，地下水泵抽不到水。</para>
        ///
        /// <para><b>為何本覆寫必定被呼叫、而 <see cref="Deserialize"/> 反而不會</b>：序列化流裡記的是系統的
        /// <c>AssemblyQualifiedName</c>（<c>SystemSerializer.SerializeType</c>），而
        /// <c>MapExtPDX.ModeB.GroundWaterSystemMod</c> 這個型別名不可能出現在任何原版地圖檔或存檔裡。
        /// 流中找不到的系統會被收進 <c>m_SystemDefaults</c> 改走 SetDefaults
        /// （<c>EntityDeserializer.cs:434-442</c> 收集 → <c>:627-631</c> 呼叫），
        /// 所以開新城一定走到這裡。原版的版本守衛
        /// （<c>context.version &lt; Version.timoSerializationFlow</c>）是為了只在「流裡沒有
        /// GroundWater 區段」的舊地圖檔上生成，對本 Mod 沒有意義，故刻意不帶。
        /// 與 <c>NaturalResourceSystemMod.SetDefaults</c> 的處理一致。</para>
        ///
        /// <para><b>為何無條件生成而不先探測原版資料</b>：這樣「新遊戲場必定非空」就不依賴
        /// <see cref="OnGameLoaded"/> 是否成功執行——後者若拋異常會被
        /// <c>GameSystemBase.GameLoaded</c> 吞掉並順手 <c>Enabled = false</c> 停用本系統。
        /// 代價是有作者資料的地圖會多算一次 Perlin 場後被覆蓋，以載入時間換取兜底確定性。</para>
        /// </summary>
        public override JobHandle SetDefaults(Context context)
        {
            JobHandle result = base.SetDefaults(context); // 先清零
            if (context.purpose == Purpose.NewGame)
            {
                result.Complete();
                GenerateProceduralGroundWater();
            }
            return result;
        }

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
        /// <para><b>三條路徑的處置</b>：開新城一律以作者原稿覆蓋 <see cref="SetDefaults"/> 的程序化基線；
        /// 載入既有存檔只在整張圖沒有任何容量（<c>m_Max</c> 全 0，即 v4.8.0 之前存下的空場）時才修復，
        /// 場只要非空就絕不動它——那是玩家已經在抽用、已累積污染的即時狀態；
        /// 而新城市既無原稿又無基線時（purpose 被事後改寫的邊角情形）補生成一次程序化場。</para>
        /// </summary>
        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);

            // 原版存檔轉換路徑由 VanillaSaveConversionSystem 全權處理：它會清空本場、再把原版
            // orgTextureSize² 資料 1:1 嵌入中心，與這裡的等比拉伸取向互斥（該路徑的地形也是
            // 「原始細節降採樣後嵌入中心」而非拉伸），直接讓路。
            // 兩者的 OnGameLoaded 誰先跑取決於系統建立順序，但正確性不依賴它：即使旗標已被 Reset、
            // 本方法後跑，下面「場非空就不動」的判據也會讓嵌入結果原樣保留。
            if (VanillaConversionState.PendingConversion) return;

            if (!m_Map.IsCreated) return;
            m_WriteDependencies.Complete();

            bool isNewGame = serializationContext.purpose == Purpose.NewGame;
            int nonZero = CountAquiferCells(m_Map);

            // 既有存檔且場是健康的 → 玩家的即時狀態，不介入
            if (!isNewGame && nonZero != 0) return;

            bool hasVanilla = TryGetVanillaAquifer(out NativeArray<TargetType> vanillaMap, out int srcSize);
            int vanillaCells = hasVanilla ? CountAquiferCells(vanillaMap) : 0;

            if (vanillaCells == 0)
            {
                if (nonZero != 0)
                {
                    ModLog.Info(nameof(GroundWaterSystemMod),
                        "原版系統無含水層原稿（地圖作者未繪製地下水），沿用 SetDefaults 生成的程序化基線場");
                }
                else if (isNewGame)
                {
                    // 新城市卻連基線都沒有：SetDefaults 當時收到的 purpose 未必是 NewGame，
                    // 因為 SerializerSystem 是在反序列化「讀不到任何資料」之後才把 LoadGame 改寫成
                    // NewGame（SerializerSystem.cs:130-142），而 SetDefaults 早在那之前就跑完了。
                    // 這裡補生成一次，保證新城市不會無水可抽。
                    ModLog.Warn(nameof(GroundWaterSystemMod),
                        "新城市的地下水場為空且原版無原稿（存檔／地圖檔沒有任何可反序列化的資料），補生成程序化場");
                    GenerateProceduralGroundWater();
                }
                else
                {
                    ModLog.Warn(nameof(GroundWaterSystemMod),
                        $"地下水容量場為空（{m_Map.Length} 格 m_Max 全為 0），且原版系統沒有可還原的含水層原稿" +
                        $"——地圖作者未繪製地下水，或原稿在本次載入中缺失。地下水泵在本存檔中不會出水。");
                }
                return;
            }

            UpsampleAquifer(vanillaMap, srcSize);

            // 前後占比一併輸出：等比拉伸不改變含水層占比，兩者接近即證明座標映射沒有錯位
            // （差異只該來自邊界插值與 short 取整）。這是唯一不必進遊戲就能核對升採樣正確性的訊號。
            int filled = CountAquiferCells(m_Map);
            ModLog.Ok(nameof(GroundWaterSystemMod),
                $"地下水場已從原版 {srcSize}² 原稿升採樣至 {kTextureSize}²（雙線性等比拉伸，與地形同比例）" +
                (isNewGame ? "，取代程序化基線" : "，修復 v4.8.0 之前存下的空場") +
                $"：含水層占比 {100f * vanillaCells / vanillaMap.Length:F2}% → {100f * filled / m_Map.Length:F2}%" +
                $"（{vanillaCells} → {filled} 格）");
        }

        /// <summary>
        /// 依原版公式在本模式解析度上生成地下水場。
        /// <para>座標先歸一化為 UV 再乘固定頻率 32（與原版 <c>GroundWaterSystem.cs:270-272</c> 相同），
        /// 故噪聲場形狀與原版一致，只是在放大的貼圖上取樣更密——含水層區域按地圖比例放大、占比不變。
        /// 與 <c>NaturalResourceSystemMod.GenerateProceduralResources</c> 的作法相同。</para>
        /// <para>沿用 <c>UnityEngine.Mathf</c> 而非 <c>Unity.Mathematics</c>：
        /// <c>PerlinNoise</c> 無對應實作，且 <c>RoundToInt</c> 的中點取整規則須與原版逐位一致。
        /// 全限定呼叫以免引入 <c>using UnityEngine</c> 與既有型別衝突。</para>
        /// </summary>
        private void GenerateProceduralGroundWater()
        {
            int nonZero = 0;
            for (int i = 0; i < m_Map.Length; i++)
            {
                float u = (float)(i % kTextureSize) / (float)kTextureSize;
                float v = (float)(i / kTextureSize) / (float)kTextureSize;

                short amount = (short)UnityEngine.Mathf.RoundToInt(10000f * math.saturate(
                    (UnityEngine.Mathf.PerlinNoise(32f * u, 32f * v) - 0.6f) / 0.4f));

                if (amount != 0) nonZero++;
                m_Map[i] = new TargetType { m_Amount = amount, m_Max = amount };
            }

            // 含水層占比是判斷本修補是否生效的唯一可觀測訊號（全 0 即代表沒生成）
            ModLog.Ok(nameof(GroundWaterSystemMod),
                $"地下水初始場已生成：{kTextureSize}² = {m_Map.Length} 格，" +
                $"含水層 {nonZero} 格（{100f * nonZero / m_Map.Length:F2}%）");
        }

        /// <summary>
        /// 統計整張圖有多少格具備含水層容量（<c>m_Max != 0</c>）。
        /// <para><c>m_Max</c> 是唯一「只有 SetDefaults／本次升採樣會寫、沒有任何執行期重生成器」的
        /// 靜態容量欄位，所以它全為 0 等價於「這張圖從來沒有含水層」，而不是「水被抽乾了」。</para>
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
        /// <para>任何一步失敗都回 <c>false</c> 讓呼叫方降級到程序化基線，絕不讓異常逸出——
        /// 本方法在 <c>OnGameLoaded</c> 內執行，逸出的異常會被 <c>GameSystemBase.GameLoaded</c>
        /// 捕獲並順手 <c>Enabled = false</c>，代價是整個地下水模擬被停用。</para>
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
                    ModLog.Warn(nameof(GroundWaterSystemMod),
                        $"原版 GroundWater m_Map 長度 {map.Length} 不是完全平方數，無從對應座標，跳過升採樣");
                    return false;
                }
                if (size != orgTextureSize)
                {
                    // PatchSet3CellMapFields 掛在原版系統 OnCreate 之後（CreateSystems 先於
                    // InitializeModManager），照理打不中那句 CreateTextures(kTextureSize)。
                    // 真的變了不影響正確性（升採樣會退化為等比縮放），但值得留痕。
                    ModLog.Warn(nameof(GroundWaterSystemMod),
                        $"原版 GroundWater 貼圖為 {size}² 而非預期的 {orgTextureSize}²，仍按 {size}² 升採樣");
                }
                return true;
            }
            catch (System.Exception ex)
            {
                ModLog.Warn(nameof(GroundWaterSystemMod),
                    $"取原版 GroundWater m_Map 失敗，跳過升採樣: {ex.Message}");
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

        #region GroundWaterTickJob
        [BurstCompile]
        private struct GroundWaterTickJob : IJob
        {
            public NativeArray<TargetType> m_GroundWaterMap;
            public WaterPipeParameterData m_Parameters;

            public NativeArray<int2> m_TempMap;

            private void HandlePollution(int index, int otherIndex, NativeArray<int2> tmp)
            {
                TargetType groundWater = this.m_GroundWaterMap[index];
                TargetType groundWater2 = this.m_GroundWaterMap[otherIndex];

                // === [MOD OPT] 零格早退（位元級等價）===
                // 兩格的 m_Amount / m_Polluted / m_Max 全為 0 時：
                //   num = num2 = 0 → 內層三元運算取 0 → (0 - 0) / 4 = 0
                //   clamp 的上下界 -(0-0)/4 與 (0-0)/4 同時塌成 0 → num3 恆為 0
                // 故本次呼叫對 tmp 沒有任何增量，可安全跳過（與 tmp 當前值無關）。
                // 逐項 == 0 比較而非位元 OR：short 是有符號型別，OR 會觸發 CS0675 符號擴展錯誤；
                // 語意上任何非 0（含理論上不該出現的負值）都不早退，退回原邏輯，行為安全。
                if (groundWater.m_Amount == 0 && groundWater.m_Polluted == 0 && groundWater.m_Max == 0 &&
                    groundWater2.m_Amount == 0 && groundWater2.m_Polluted == 0 && groundWater2.m_Max == 0) return;

                ref int2 reference = ref tmp.ElementAt(index);
                ref int2 reference2 = ref tmp.ElementAt(otherIndex);
                int num = groundWater.m_Polluted + groundWater2.m_Polluted;
                int num2 = groundWater.m_Amount + groundWater2.m_Amount;
                int num3 = math.clamp((((num2 > 0) ? (groundWater.m_Amount * num / num2) : 0) - groundWater.m_Polluted) / 4, -(groundWater2.m_Amount - groundWater2.m_Polluted) / 4, (groundWater.m_Amount - groundWater.m_Polluted) / 4);
                reference.y += num3;
                reference2.y -= num3;
                Assert.IsTrue(0 <= groundWater.m_Polluted + reference.y);
                Assert.IsTrue(groundWater.m_Polluted + reference.y <= groundWater.m_Amount);
                Assert.IsTrue(0 <= groundWater2.m_Polluted + reference2.y);
                Assert.IsTrue(groundWater2.m_Polluted + reference2.y <= groundWater2.m_Amount);
            }

            private void HandleFlow(int index, int otherIndex, NativeArray<int2> tmp)
            {
                TargetType groundWater = this.m_GroundWaterMap[index];
                TargetType groundWater2 = this.m_GroundWaterMap[otherIndex];

                // === [MOD OPT] 零格早退（位元級等價）===
                // 兩格全為 0 時：num3 = 0 - 0 = 0；
                //   num4 = clamp((0 - 0 - 0) / 4, -0/4, 0/4) = 0 → num5 = 0
                // → reference / reference2 完全不變。
                // 關鍵：num4 的 clamp 上下界用的是 map 值（m_Amount）而非 amount + tmp.x，
                // 故該推導與 tmp 的當前值無關。
                if (groundWater.m_Amount == 0 && groundWater.m_Polluted == 0 && groundWater.m_Max == 0 &&
                    groundWater2.m_Amount == 0 && groundWater2.m_Polluted == 0 && groundWater2.m_Max == 0) return;

                ref int2 reference = ref tmp.ElementAt(index);
                ref int2 reference2 = ref tmp.ElementAt(otherIndex);
                Assert.IsTrue(groundWater2.m_Polluted + reference2.y <= groundWater2.m_Amount + reference2.x);
                Assert.IsTrue(groundWater.m_Polluted + reference.y <= groundWater.m_Amount + reference.x);
                float num = ((groundWater.m_Amount + reference.x != 0) ? (1f * (float)(groundWater.m_Polluted + reference.y) / (float)(groundWater.m_Amount + reference.x)) : 0f);
                float num2 = ((groundWater2.m_Amount + reference2.x != 0) ? (1f * (float)(groundWater2.m_Polluted + reference2.y) / (float)(groundWater2.m_Amount + reference2.x)) : 0f);
                int num3 = groundWater.m_Amount - groundWater.m_Max;
                int num4 = math.clamp((groundWater2.m_Amount - groundWater2.m_Max - num3) / 4, -groundWater.m_Amount / 4, groundWater2.m_Amount / 4);
                reference.x += num4;
                reference2.x -= num4;
                int num5 = 0;
                if (num4 > 0)
                {
                    num5 = (int)((float)num4 * num2);
                }
                else if (num4 < 0)
                {
                    num5 = (int)((float)num4 * num);
                }
                reference.y += num5;
                reference2.y -= num5;
                Assert.IsTrue(0 <= groundWater.m_Amount + reference.x);
                Assert.IsTrue(groundWater.m_Amount + reference.x <= groundWater.m_Max);
                Assert.IsTrue(0 <= groundWater2.m_Amount + reference2.x);
                Assert.IsTrue(groundWater2.m_Amount + reference2.x <= groundWater2.m_Max);
                Assert.IsTrue(0 <= groundWater.m_Polluted + reference.y);
                Assert.IsTrue(groundWater.m_Polluted + reference.y <= groundWater.m_Amount + reference.x);
                Assert.IsTrue(0 <= groundWater2.m_Polluted + reference2.y);
                Assert.IsTrue(groundWater2.m_Polluted + reference2.y <= groundWater2.m_Amount + reference2.x);
            }

            public void Execute()
            {
                // NativeArray<int2> tmp = new NativeArray<int2>(this.m_GroundWaterMap.Length, Allocator.TempJob); // 传入的临时缓冲区，全部替换为m_TempMap
                for (int i = 0; i < this.m_GroundWaterMap.Length; i++)
                {
                    int num = i % kTextureSize;
                    int num2 = i / kTextureSize;
                    if (num < kTextureSize - 1)
                    {
                        this.HandlePollution(i, i + 1, m_TempMap);
                    }
                    if (num2 < kTextureSize - 1)
                    {
                        this.HandlePollution(i, i + kTextureSize, m_TempMap);
                    }
                }
                for (int j = 0; j < this.m_GroundWaterMap.Length; j++)
                {
                    int num3 = j % kTextureSize;
                    int num4 = j / kTextureSize;
                    if (num3 < kTextureSize - 1)
                    {
                        this.HandleFlow(j, j + 1, m_TempMap);
                    }
                    if (num4 < kTextureSize - 1)
                    {
                        this.HandleFlow(j, j + kTextureSize, m_TempMap);
                    }
                }
                for (int k = 0; k < this.m_GroundWaterMap.Length; k++)
                {
                    TargetType value = this.m_GroundWaterMap[k];
                    value.m_Amount = (short)math.min(value.m_Amount + m_TempMap[k].x + math.ceil(this.m_Parameters.m_GroundwaterReplenish * (float)value.m_Max), value.m_Max); // 注意：Mathf改为Burst优化的math
                    value.m_Polluted = (short)math.clamp(value.m_Polluted + m_TempMap[k].y - this.m_Parameters.m_GroundwaterPurification, 0, value.m_Amount);
                    this.m_GroundWaterMap[k] = value;
                }
                //tmp.Dispose();
                // m_TempMap 由调用方分配Allocator.TempJob
                // 无需手动释放（由 Dispose(Dependency) 管理
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
        /// （<c>GroundWaterTickJob</c> 的 <c>Assert</c> 全靠這個不變量）。</para>
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

        #region 序列化自适应
        // ==============================================================================
        // 序列化修(泛型
        // ==============================================================================
        // 重写 Serialize 以处理大数据 (使用 TempJob)
        public new JobHandle Serialize<TWriter>(EntityWriterData writerData, JobHandle inputDeps) where TWriter : struct, IWriter
        {
            // 获取 Stride (数据步长)
            int stride = 0;
            if ((object)default(TargetType) is IStrideSerializable strideSerializable)
            {
                stride = strideSerializable.GetStride(writerData.GetWriter<TWriter>().context);
            }

            // 调度自定义的序列Job
            JobHandle jobHandle = new SerializeJobMod<TWriter>
            {
                m_Stride = stride,
                m_Map = this.m_Map,
                m_WriterData = writerData
            }.Schedule(JobHandle.CombineDependencies(inputDeps, m_WriteDependencies));

            m_ReadDependencies = JobHandle.CombineDependencies(m_ReadDependencies, jobHandle);
            return jobHandle;
        }

        // 重写 Deserialize (无需迁移旧存档，Job会重新计
        public override JobHandle Deserialize<TReader>(EntityReaderData readerData, JobHandle inputDeps)
        {
            int stride = 0;
            if ((object)default(TargetType) is IStrideSerializable strideSerializable)
            {
                stride = strideSerializable.GetStride(readerData.GetReader<TReader>().context);
            }

            // 簡化版 Job：尺寸不匹配就讀完丟棄（該分支不可達，理由見 job 內註釋）
            DeserializeJobResetMismatch<TReader> jobData = new()
            {
                m_Stride = stride,
                m_Map = this.m_Map,
                m_ReaderData = readerData
            };
            m_WriteDependencies = jobData.Schedule(JobHandle.CombineDependencies(inputDeps, m_ReadDependencies, m_WriteDependencies));
            return m_WriteDependencies;
        }

        [BurstCompile]
        private struct SerializeJobMod<TWriter> : IJob where TWriter : struct, IWriter
        {
            [ReadOnly] public int m_Stride;
            [ReadOnly] public NativeArray<TargetType> m_Map;
            public EntityWriterData m_WriterData;

            public void Execute()
            {
                TWriter writer = this.m_WriterData.GetWriter<TWriter>();
                if (m_Stride != 0 && m_Map.Length != 0)
                {
                    // 必须TempJob 防止 1024+ 尺寸导致内存溢出
                    NativeList<byte> buffer = new NativeList<byte>(m_Map.Length * 2, Allocator.TempJob);
                    try
                    {
                        m_WriterData.GetWriter<TWriter>(buffer).Write(m_Map);
                        writer.Write(-m_Map.Length);
                        writer.Write(buffer.Length);
                        writer.Write(buffer.AsArray(), m_Stride);
                    }
                    finally { buffer.Dispose(); }
                }
                else
                {
                    writer.Write(m_Map.Length);
                    writer.Write(m_Map);
                }
            }
        }

        /// <summary>
        /// 尺寸相符就讀入，不符就讀完丟棄（<b>必須讀完</b>，否則
        /// <c>JobSystemSerializer.DeserializeEndJob</c> 的長度校驗會拋
        /// "Data size mismatch when deserializing system"）。
        ///
        /// <para><b>「不符」這條分支在實際流程中到不了</b>：序列化流裡帶本型別名的存檔必定是同模式
        /// MapExt 自己存的，而 <c>kTextureSize</c> 是 per-Mode 的 <c>const</c>
        /// （<c>XCellMapSystemRe</c>），尺寸必然相符；跨模式載入時型別名根本不同
        /// （各模式的 GroundWaterSystemMod 分屬不同 namespace，是不同型別），
        /// 本系統會被收進 <c>m_SystemDefaults</c> 改走
        /// <see cref="SetDefaults"/> 而根本不進這裡。所以這裡刻意<b>不</b>做重採樣——
        /// 地圖作者的原稿在原版 <c>GroundWaterSystem</c> 的 m_Map 裡，由
        /// <see cref="OnGameLoaded"/> 取用。</para>
        /// </summary>
        [BurstCompile]
        private struct DeserializeJobResetMismatch<TReader> : IJob where TReader : struct, IReader
        {
            [ReadOnly] public int m_Stride;
            public NativeArray<TargetType> m_Map;
            public EntityReaderData m_ReaderData;

            public void Execute()
            {
                TReader reader = m_ReaderData.GetReader<TReader>();
                if (!(reader.context.version > Version.stormWater)) return;

                // 默认0
                // m_Map 已经SetDefaults 中被清零了，如果这里不写入，就是重置状

                if (reader.context.version > Version.cellMapLengths)
                {
                    reader.Read(out int storedCount);

                    // 1. 判断是否匹配
                    bool sizeMatches = (math.abs(storedCount) == m_Map.Length);

                    // 2. 如果是原始数(Length > 0)
                    if (storedCount > 0)
                    {
                        if (sizeMatches)
                        {
                            reader.Read(m_Map);
                        }
                        else
                        {
                            // 尺寸不匹配：读取到临时数组并丢弃 (必须读取以推进流位置)
                            var dummy = new NativeArray<TargetType>(storedCount, Allocator.TempJob);
                            reader.Read(dummy);
                            dummy.Dispose();
                            // m_Map 保持0
                        }
                    }
                    // 3. 如果是压缩数(Length < 0)
                    else if (storedCount < 0)
                    {
                        int actualCount = -storedCount;
                        reader.Read(out int byteLength);

                        // 必须读出来以清空
                        NativeArray<byte> compressedBuffer = new NativeArray<byte>(byteLength, Allocator.TempJob);
                        try
                        {
                            reader.Read(compressedBuffer, m_Stride);

                            if (actualCount == m_Map.Length)
                            {
                                // 尺寸匹配：正常解压到 Map
                                NativeReference<int> pos = new NativeReference<int>(0, Allocator.Temp);
                                m_ReaderData.GetReader<TReader>(compressedBuffer, pos).Read(m_Map);
                                pos.Dispose();
                            }
                            // 否则：compressedBuffer 已读完（流位置已推进），不解压，m_Map 保持0
                        }
                        finally
                        {
                            compressedBuffer.Dispose();
                        }
                    }
                }
                else
                {
                    // 旧版本数据(version <= cellMapLengths)：流里没有长度前缀，只能整片读。
                    // 注意这个条件恒真——CreateTextures(kTextureSize) 保证 m_Map.Length 必等于
                    // kTextureSize²，所以原版那句「运气好匹配了」在本 Mod 里不成立：真走到这里会
                    // 按本模式尺寸读取，而流里只有原版 orgTextureSize² 的份量，必然读越界。
                    // 但该版本的存档不可能带有本 Mod 的型别名（那时 MapExt 还不存在），
                    // 与上面的尺寸不匹配分支一样到不了；保留原版形状以便日后比对。
                    if (m_Map.Length == kTextureSize * kTextureSize)
                        reader.Read(m_Map);
                }
            }

        }

        #endregion

        #region GetData修正
        // 重写/重定向的 GetData
        public new TargetCellMapData GetData(bool readOnly, out JobHandle dependencies)
        {
            // 获取依赖
            dependencies = (readOnly ? m_WriteDependencies : JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies));

            float2 mapSize = new float2(XCellMapSystemRe.kMapSize, XCellMapSystemRe.kMapSize);

            return new TargetCellMapData
            {
                m_Buffer = m_Map,
                m_CellSize = mapSize / (float2)m_TextureSize,
                m_TextureSize = m_TextureSize
            };
        }
        #endregion

        #region HarmonyPatch
        // ==============================================================================
        // Harmony 补丁 (全自动适配)
        // ==============================================================================
        [HarmonyPatch]
        public static class Patches
        {
            // 辅助判断：只拦截对应的原版系统实
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool ShouldRedirect(object instance)
            {
                return Instance != null && instance.GetType() == typeof(TargetSystem);
            }

            // Patch: GetMap
            [HarmonyPatch(typeof(BaseCellMapSystem), "GetMap")]
            [HarmonyPrefix]
            public static bool GetMapPrefix(BaseCellMapSystem __instance,
                                      ref NativeArray<TargetType> __result,
                                      bool readOnly,
                                      ref JobHandle dependencies)
            {
                if (ShouldRedirect(__instance))
                {
                    __result = Instance.GetMap(readOnly, out var deps);
                    dependencies = deps;
                    return false;
                }
                return true;
            }

            // Patch: GetData
            [HarmonyPatch(typeof(BaseCellMapSystem), "GetData")]
            [HarmonyPrefix]
            public static bool GetDataPrefix(BaseCellMapSystem __instance,
                                      ref TargetCellMapData __result,
                                      bool readOnly,
                                      ref JobHandle dependencies)
            {
                if (ShouldRedirect(__instance))
                {
                    __result = Instance.GetData(readOnly, out var deps);
                    dependencies = deps;
                    return false;
                }
                return true;
            }

            // Patch: AddReader
            [HarmonyPatch(typeof(BaseCellMapSystem), "AddReader")]
            [HarmonyPrefix]
            public static bool AddReaderPrefix(BaseCellMapSystem __instance, JobHandle jobHandle)
            {
                if (ShouldRedirect(__instance))
                {
                    Instance.AddReader(jobHandle);
                    return false;
                }
                return true;
            }

            // Patch: AddWriter
            [HarmonyPatch(typeof(BaseCellMapSystem), "AddWriter")]
            [HarmonyPrefix]
            public static bool AddWriterPrefix(BaseCellMapSystem __instance, JobHandle jobHandle)
            {
                if (ShouldRedirect(__instance))
                {
                    Instance.AddWriter(jobHandle);
                    return false;
                }
                return true;
            }
        }
        #endregion

    } // mod class

} // mod namespace



