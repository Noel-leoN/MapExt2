using Game;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace SimpleBrush.Core
{
    /// <summary>
    /// 高性能自然资源守护系统。
    /// 将已开启"无限"模式的资源类型的 m_Used 重置为 0，实现资源永不枯竭。
    /// 当 MapExt2 激活时，底层 GetData 调用会被透明重定向至扩展大缓冲区，无需额外适配。
    /// </summary>
    /// <remarks>
    /// 刻意不掛 <c>[UpdateAfter]</c>：CS2 未建立 ComponentSystemGroup，Unity ECS 的排序屬性
    /// 完全不會被讀取（原版源碼亦無一處使用），幀內順序由 <c>Game.UpdateSystem</c> 依註冊順序決定。
    /// 本系統只需要「夾得夠頻繁」而不需要緊跟再生系統，理由見 <see cref="GetUpdateInterval"/>。
    /// </remarks>
    public partial class ResourceGuardSystem : GameSystemBase
    {
        #region Fields

        private NaturalResourceSystem m_NaturalResourceSystem;

        #endregion

        #region System Loop

        protected override void OnCreate()
        {
            base.OnCreate();
            m_NaturalResourceSystem = World.GetOrCreateSystemManaged<NaturalResourceSystem>();
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            // 對齊「真正扣減資源」的 AreaLotSimulationSystem（interval 512），而非再生系統的 8192。
            // 用 8192 時，兩次補償之間會累積 16 個扣減 tick；扣減端的上限是 65535 而不是 m_Base，
            // 足以讓 m_Used 超過 m_Base 使可用量變負 —— 抽取企業的濃度歸零、整個生產 pass 被跳過，
            // 建築效率顯示 0% 並空轉，直到下一次補償才恢復。
            // 成本可忽略：原版 GameModeNaturalResourcesAdjustSystem 就在 interval 2048 做同形狀的全圖 pass。
            // interval 僅在 GameSimulation／EditorSimulation／LoadSimulation 三個 phase 生效，
            // 其餘 phase 回 1 以對齊原版慣例（本系統目前只註冊於 GameSimulation）。
            return phase == SystemUpdatePhase.GameSimulation ? 512 : 1;
        }

        protected override void OnUpdate()
        {
            var settings = Mod.Instance?.Settings;
            if (settings == null) return;

            // === 检查是否有任何无限开关已启用 ===
            bool fertility = settings.InfiniteFertility;
            bool ore = settings.InfiniteOre;
            bool oil = settings.InfiniteOil;
            bool fish = settings.InfiniteFish;

            if (!fertility && !ore && !oil && !fish) return;

            // === 获取可写缓冲区 ===
            var data = m_NaturalResourceSystem.GetData(false, out var deps);

            // === 调度 Burst 异步并行 Job ===
            var job = new InfiniteGuardJob
            {
                m_CellData = data,
                m_Fertility = fertility,
                m_Ore = ore,
                m_Oil = oil,
                m_Fish = fish
            };

            var handle = job.Schedule(
                data.m_TextureSize.x * data.m_TextureSize.y,
                64,
                JobHandle.CombineDependencies(deps, Dependency));

            // === 归还写锁 ===
            m_NaturalResourceSystem.AddWriter(handle);
            Dependency = handle;
        }

        #endregion

        #region Jobs

        [BurstCompile]
        private struct InfiniteGuardJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction]
            public CellMapData<NaturalResourceCell> m_CellData;
            public bool m_Fertility;
            public bool m_Ore;
            public bool m_Oil;
            public bool m_Fish;

            public void Execute(int index)
            {
                var cell = m_CellData.m_Buffer[index];
                if (m_Fertility) cell.m_Fertility.m_Used = 0;
                if (m_Ore) cell.m_Ore.m_Used = 0;
                if (m_Oil) cell.m_Oil.m_Used = 0;
                if (m_Fish) cell.m_Fish.m_Used = 0;
                m_CellData.m_Buffer[index] = cell;
            }
        }

        #endregion
    }
}
