using Colossal.Mathematics;
using Game;
using Game.Common;
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
    using BaseCellMapSystem = CellMapSystem<GroundPollution>;
    // 2. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<GroundPollution>;
    // 3. 原版系统类型 (用于禁用和定位)
    using TargetSystem = GroundPollutionSystem;
    // 4. T struct
    using TargetType = GroundPollution;
    // =========================================================================================

    // 降级保护（重构二 0.4）：合并版存档里本类的类型名是 MapExtPDX.CellMap.GroundPollutionSystemMod，退回本版本时靠此别名读回，勿删
    /// <summary>
    /// 地表污染系统：用于计算和存储地面污染数据
    /// </summary>
    [Colossal.Serialization.Entities.FormerlySerializedAs("MapExtPDX.CellMap.GroundPollutionSystemMod")]
    public partial class GroundPollutionSystemMod : CellMapReplacementSystem<TargetType, TargetSystem>
    {
        private const string Tag = nameof(GroundPollutionSystemMod);

        #region 常量和静态字配置
        // 纹理尺寸(vanilla=256)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.GroundPollutionSystemkTextureSize; // mod尺寸

        // 基类参数：日志 Tag、本模式的贴图边长与地图边长（XCellMapSystemRe 按模式命名空间解析）
        protected override string LogTag => Tag;
        protected override int ModTextureSize => kTextureSize;
        protected override int MapSize => XCellMapSystemRe.kMapSize;

        // 系统更新周期：每128
        public static readonly int kUpdatesPerDay = 128;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;
        #endregion

        #region 查询和系统引
        // 系统依赖
        private SimulationSystem m_SimulationSystem;
        // 查询
        private EntityQuery m_PollutionParameterGroup;
        #endregion

        #region System Loop
        protected override void OnCreate()
        {
            // 1–2. 禁用原版系统、登记 Instance、创建自定义大小纹理由基类完成
            base.OnCreate();

            // 3. 获取其他依赖和查
            this.m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            this.m_PollutionParameterGroup = GetEntityQuery(ComponentType.ReadOnly<PollutionParameterData>());
            // 强制依赖更新
            RequireForUpdate(this.m_PollutionParameterGroup);
        }

        protected override void OnUpdate()
        {
            PollutionFadeJob pollutionFadeJob = default(PollutionFadeJob);
            pollutionFadeJob.m_PollutionMap = m_Map;
            pollutionFadeJob.m_PollutionParameters = this.m_PollutionParameterGroup.GetSingleton<PollutionParameterData>();
            pollutionFadeJob.m_Random = RandomSeed.Next();
            pollutionFadeJob.m_Frame = this.m_SimulationSystem.frameIndex;
            PollutionFadeJob jobData = pollutionFadeJob;
            Dependency = IJobExtensions.Schedule(jobData, JobHandle.CombineDependencies(m_WriteDependencies, m_ReadDependencies, Dependency));
            AddWriter(Dependency);
            Dependency = JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies, Dependency);
        }
        #endregion

        #region PollutionFadeJob
        [BurstCompile]
        private struct PollutionFadeJob : IJob
        {
            public NativeArray<TargetType> m_PollutionMap;

            public PollutionParameterData m_PollutionParameters;

            public RandomSeed m_Random;

            public uint m_Frame;

            public void Execute()
            {
                Unity.Mathematics.Random random = this.m_Random.GetRandom((int)this.m_Frame);
                for (int i = 0; i < this.m_PollutionMap.Length; i++)
                {
                    TargetType value = this.m_PollutionMap[i];
                    if (value.m_Pollution > 0)
                    {
                        value.m_Pollution = (short)math.max(0, this.m_PollutionMap[i].m_Pollution - MathUtils.RoundToIntRandom(ref random, (float)this.m_PollutionParameters.m_GroundFade / (float)kUpdatesPerDay));
                        // === [MOD OPT] 條件寫回（位元級等價）===
                        // 原本寫回在 if 外，m_Pollution == 0 的格子會把讀出來的同一個值寫回去。
                        // 大地图上绝大多数格为 0，等于每次 tick 无谓标脏并回写整张扩展贴图（按 kTextureSize² 计）。
                        // 只在真正變化時寫入；random 的消耗位置不變（本來就在 if 內），隨機序列不受影響。
                        this.m_PollutionMap[i] = value;
                    }
                }
            }
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




