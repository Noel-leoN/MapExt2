// Game.Simulation.ZoneAmbienceSystem : CellMapSystem<ZoneAmbienceCell>, IJobSerializable

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
    using BaseCellMapSystem = CellMapSystem<ZoneAmbienceCell>;
    // 2. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<ZoneAmbienceCell>;
    // 3. 原版系统类型 (用于禁用和定位)
    using TargetSystem = ZoneAmbienceSystem;
    // 4. T struct
    using TargetType = ZoneAmbienceCell;
    // =========================================================================================

    /// <summary>
    /// 区域氛围系统 (Zone Ambience System)
    /// <para>
    /// 负责管理整个地图的“氛围值”网格（如噪音、污染、地价等）
    /// 主要功能包括定期更新网格数据（将累积值应用到当前值），以及提供查询特定位置氛围值的工具方法
    /// </para>
    /// </summary>
    // 降级保护（重构二 0.4）：合并版存档里本类的类型名是 MapExtPDX.CellMap.ZoneAmbienceSystemMod，退回本版本时靠此别名读回，勿删
    [Colossal.Serialization.Entities.FormerlySerializedAs("MapExtPDX.CellMap.ZoneAmbienceSystemMod")]
    public partial class ZoneAmbienceSystemMod : CellMapReplacementSystem<TargetType, TargetSystem>
    {
        private const string Tag = nameof(ZoneAmbienceSystemMod);

        #region 字段/配置
        // 纹理尺寸(vanilla=256)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.ZoneAmbienceSystemkTextureSize; // mod尺寸

        // 基类参数：日志 Tag、本模式的贴图边长与地图边长（XCellMapSystemRe 按模式命名空间解析）
        protected override string LogTag => Tag;
        protected override int ModTextureSize => kTextureSize;
        protected override int MapSize => XCellMapSystemRe.kMapSize;

        // 系统更新周期：每64
        public static readonly int kUpdatesPerDay = 128;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;
        #endregion

        #region System Loop
        // OnCreate／OnDestroy 由基类负责：禁用原版、创建贴图、登记 Instance

        protected override void OnUpdate()
        {
            // 调度更新 Job
            var updateJob = new ZoneAmbienceUpdateJob
            {
                m_ZoneMap = m_Map
            };

            // 依赖链管理：合并读写依赖，确保线程安
            JobHandle dependency = JobHandle.CombineDependencies(m_WriteDependencies, m_ReadDependencies, Dependency);

            // 调度并行 Job，处理所有单元格
            Dependency = IJobParallelForExtensions.Schedule(
                updateJob,
                kTextureSize * kTextureSize, // 数组长度
                kTextureSize,                // Batch Size (通常一行作为一个batch)
                dependency
            );

            // 注册写入依赖，通知基类数据已被修改
            AddWriter(Dependency);

            // 再次合并依赖，确保后续系统等待本系统完成
            Dependency = JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies, Dependency);
        }

        #endregion

        #region Jobs

        /// <summary>
        /// 氛围更新 Job
        /// <para>并行处理每一个网格单元。将累积Accumulator)的值转移到当前Value)中，并重置累积器/para>
        /// </summary>
        [BurstCompile]
        private struct ZoneAmbienceUpdateJob : IJobParallelFor
        {
            public NativeArray<TargetType> m_ZoneMap;

            public void Execute(int index)
            {
                TargetType currentCell = m_ZoneMap[index];

                // 将累积值应用为当前有效值，并清空累积器以便下一帧重新计
                m_ZoneMap[index] = new TargetType
                {
                    m_Value = currentCell.m_Accumulator,
                    m_Accumulator = default(ZoneAmbiences)
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


