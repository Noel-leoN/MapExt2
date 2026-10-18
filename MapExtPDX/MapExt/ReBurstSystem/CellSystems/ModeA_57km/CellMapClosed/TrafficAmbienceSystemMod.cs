// Game.Simulation.TrafficAmbienceSystem : CellMapSystem<Game.Simulation.TrafficAmbienceCell>, Colossal.Serialization.Entities.IJobSerializable

using Game;
using Game.Simulation;
using HarmonyLib;
using MapExtPDX.CellShared;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace MapExtPDX.ModeA
{
    // =========================================================================================
    // [配置区域]
    // =========================================================================================
    // 1. 基类泛型
    using BaseCellMapSystem = CellMapSystem<TrafficAmbienceCell>;
    // 2. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<TrafficAmbienceCell>;
    // 3. 原版系统类型 (用于禁用和定位)
    using TargetSystem = TrafficAmbienceSystem;
    // 4. T struct
    using TargetType = TrafficAmbienceCell;
    // =========================================================================================

    /// <summary>
    /// 交通氛围系(Traffic Ambience System)
    /// <para>
    /// 负责管理全图的交通噪氛围数据。定期更新每个网格单元的交通强度供UI显示
    /// </para>
    /// </summary>
    // 降级保护（重构二 0.4）：合并版存档里本类的类型名是 MapExtPDX.CellMap.TrafficAmbienceSystemMod，退回本版本时靠此别名读回，勿删
    [Colossal.Serialization.Entities.FormerlySerializedAs("MapExtPDX.CellMap.TrafficAmbienceSystemMod")]
    public partial class TrafficAmbienceSystemMod : CellMapReplacementSystem<TargetType, TargetSystem>
    {
        private const string Tag = nameof(TrafficAmbienceSystemMod);

        #region 字段/配置
        // 纹理尺寸(vanilla=256)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.TrafficAmbienceSystemkTextureSize; // mod尺寸

        // 基类参数：日志 Tag、本模式的贴图边长与地图边长（XCellMapSystemRe 按模式命名空间解析）
        protected override string LogTag => Tag;
        protected override int ModTextureSize => kTextureSize;
        protected override int MapSize => XCellMapSystemRe.kMapSize;

        // 系统更新周期：每次数1024
        public static readonly int kUpdatesPerDay = 1024;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;
        #endregion

        #region Lifecycle Methods
        // OnCreate／OnDestroy 由基类负责：禁用原版、创建贴图、登记 Instance

        protected override void OnUpdate()
        {

            TrafficAmbienceUpdateJob jobData = new TrafficAmbienceUpdateJob
            {
                m_TrafficMap = m_Map
            };

            // 调度并行 Job
            JobHandle combinedDependency = JobHandle.CombineDependencies(m_WriteDependencies, m_ReadDependencies, Dependency);

            // 调度参数：总长度为 grid size * grid size，batch size 设为 grid size (即一行一个batch)
            Dependency = IJobParallelForExtensions.Schedule(
                jobData,
                kTextureSize * kTextureSize,
                kTextureSize,
                combinedDependency
            );

            // 注册写入依赖，确保后续系统知道此 Job 正在写入数据
            AddWriter(Dependency);

            // 再次合并依赖，保证本帧后续逻辑的安全
            Dependency = JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies, Dependency);
        }
        #endregion

        #region TrafficAmbienceUpdateJob

        /// <summary>
        /// 负责更新交通氛围数据的并行 Job
        /// <para>核心逻辑是将累积的流量(Accumulator) 应用到当前流量(Traffic) 中，并重置累积器/para>
        /// </summary>
        [BurstCompile]
        private struct TrafficAmbienceUpdateJob : IJobParallelFor
        {
            // 交通氛围地图数
            public NativeArray<TargetType> m_TrafficMap;

            public void Execute(int index)
            {
                TargetType currentCell = m_TrafficMap[index];

                // 将累积的交通量转移到当前交通量字段，并清空累积
                // 意味着上一帧积累的数据在这一帧生
                m_TrafficMap[index] = new TargetType
                {
                    m_Traffic = currentCell.m_Accumulator,
                    m_Accumulator = 0f
                };
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



