// Game.Simulation.NoisePollutionSystem : CellMapSystem<NoisePollution>, IJobSerializable

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
    using BaseCellMapSystem = CellMapSystem<NoisePollution>;
    // 2. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<NoisePollution>;
    // 3. 原版系统类型 (用于禁用和定位)
    using TargetSystem = NoisePollutionSystem;
    // 4. T struct
    using TargetType = NoisePollution;
    // =========================================================================================

    // 降级保护：合并版存档里本类的类型名是 MapExtPDX.CellMap.NoisePollutionSystemMod，退回本版本时靠此别名读回，勿删
    /// <summary>
    /// 噪音污染系统
    /// 负责计算地图上噪声的扩散平滑处理
    /// 噪音通常由建筑、交通等声源填入 m_PollutionTemp，本系统负责将其扩散到周围格点
    /// </summary>
    [Colossal.Serialization.Entities.FormerlySerializedAs("MapExtPDX.CellMap.NoisePollutionSystemMod")]
    public partial class NoisePollutionSystemMod : CellMapReplacementSystem<TargetType, TargetSystem>
    {
        private const string Tag = nameof(NoisePollutionSystemMod);

        #region 常量与配置
        // 纹理尺寸(vanilla=256)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = XCellMapSystemRe.NoisePollutionSystemkTextureSize; // mod尺寸

        // 基类参数：日志 Tag、本模式的贴图边长与地图边长（XCellMapSystemRe 按模式命名空间解析）
        protected override string LogTag => Tag;
        protected override int ModTextureSize => kTextureSize;
        protected override int MapSize => XCellMapSystemRe.kMapSize;

        // 系统更新周期：每日更新 128 次
        public static readonly int kUpdatesPerDay = 128;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;
        #endregion

        #region Lifecycle
        // OnCreate／OnDestroy 由基类负责：禁用原版、创建贴图、登记 Instance

        protected override void OnUpdate()
        {
            // 1. 获取地图数据的原生数组（读写权限）
            NativeArray<TargetType> pollutionMap = GetMap(readOnly: false, out JobHandle inputDeps);

            // 2. 调度扩散计算 Job (Swap Job)
            // 这一步计算加权平均值，将 Temp 中的累积值转化为实际 Pollution
            var swapJob = new NoisePollutionSwapJob
            {
                m_PollutionMap = pollutionMap
            };
            // 按照 Map 长度并行调度，依赖于 inputDeps
            JobHandle swapHandle = swapJob.Schedule(m_Map.Length, 64, inputDeps);

            // 3. 调度清理 Job (Clear Job)
            // 计算完成后，清空 Temp 值，为下一帧的累积做准备
            var clearJob = new NoisePollutionClearJob
            {
                m_PollutionMap = pollutionMap
            };
            JobHandle clearHandle = clearJob.Schedule(m_Map.Length, 64, swapHandle);

            // 4. 将最终的 JobHandle 注册回基类，确保后续读写安全
            AddWriter(clearHandle);
        }
        #endregion

        #region Jobs
        /// <summary>
        /// 扩散计算 Job：执行 3x3 高斯模糊加权
        /// </summary>
        [BurstCompile]
        private struct NoisePollutionSwapJob : IJobParallelFor
        {
            // 允许并行写入，因为写入的是 Struct 内部字段，且每个 Index 独立
            [NativeDisableParallelForRestriction] public NativeArray<TargetType> m_PollutionMap;

            public void Execute(int index)
            {
                TargetType cell = m_PollutionMap[index];

                // 计算二维坐标
                int x = index % kTextureSize;
                int y = index / kTextureSize;

                // 获取周围 8 个邻居的 Temp（边界检查：越界则视为 0）
                // 使用三元运算符替代 if 以利于 Burst 向量化优化

                // 左右邻居
                short left = (short)((x > 0) ? m_PollutionMap[index - 1].m_PollutionTemp : 0);
                short right = (short)((x < kTextureSize - 1) ? m_PollutionMap[index + 1].m_PollutionTemp : 0);

                // 上下邻居
                short bottom = (short)((y > 0) ? m_PollutionMap[index - kTextureSize].m_PollutionTemp : 0);
                short top = (short)((y < kTextureSize - 1) ? m_PollutionMap[index + kTextureSize].m_PollutionTemp : 0);

                // 对角线邻居
                short bottomLeft = (short)((x > 0 && y > 0) ? m_PollutionMap[index - 1 - kTextureSize].m_PollutionTemp : 0);
                short bottomRight = (short)((x < kTextureSize - 1 && y > 0) ? m_PollutionMap[index + 1 - kTextureSize].m_PollutionTemp : 0);
                short topLeft = (short)((x > 0 && y < kTextureSize - 1) ? m_PollutionMap[index - 1 + kTextureSize].m_PollutionTemp : 0);
                short topRight = (short)((x < kTextureSize - 1 && y < kTextureSize - 1) ? m_PollutionMap[index + 1 + kTextureSize].m_PollutionTemp : 0);

                // 计算加权平均
                // 权重分布：
                // 中心: 1/4 (0.25)
                // 十字: 1/8 (0.125) * 4 = 0.5
                // 对角: 1/16 (0.0625) * 4 = 0.25
                // 总权重 = 1.0
                int weightedSum = (cell.m_PollutionTemp / 4) +
                                  ((left + right + bottom + top) / 8) +
                                  ((bottomLeft + bottomRight + topLeft + topRight) / 16);

                cell.m_Pollution = (short)weightedSum;

                // 写回数组
                m_PollutionMap[index] = cell;
            }
        }

        /// <summary>
        /// 清理 Job：重置累积缓冲区
        /// </summary>
        [BurstCompile]
        private struct NoisePollutionClearJob : IJobParallelFor
        {
            public NativeArray<TargetType> m_PollutionMap;

            public void Execute(int index)
            {
                TargetType cell = m_PollutionMap[index];
                // === [MOD OPT] 條件寫回（位元級等價）===
                // 已經是 0 的格子，寫入不改變任何位元。大地圖上多數格為 0，
                // 跳過可省下大部分 store 與 dirty cache line。
                if (cell.m_PollutionTemp == 0) return;
                cell.m_PollutionTemp = 0;
                m_PollutionMap[index] = cell;
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


