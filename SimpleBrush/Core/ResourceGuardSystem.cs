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
    /// 在 NaturalResourceSystem 完成资源再生计算后运行，
    /// 将已开启"无限"模式的资源类型的 m_Used 重置为 0，实现资源永不枯竭。
    /// 当 MapExt2 激活时，底层 GetData 调用会被透明重定向至扩展大缓冲区，无需额外适配。
    /// </summary>
    [UpdateAfter(typeof(NaturalResourceSystem))]
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
            // 与原版 NaturalResourceSystem 同步（8192 帧），在其完成后立即修正
            return 8192;
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
