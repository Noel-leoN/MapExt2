using Game.Simulation;
using Unity.Entities;

namespace SimpleBrush.Core
{
    /// <summary>
    /// 自然资源状态一键恢复工具。
    /// 直接通过 NaturalResourceSystem.GetData() 操作 CellMap 缓冲区，安全重置已耗尽字段。
    /// 当 MapExt2 激活时，其 Harmony 重定向对此调用完全透明生效。
    /// </summary>
    public static class ResourceCleaner
    {
        // === 资源类型枚举 ===
        public enum ResourceType { Fertility, Ore, Oil, Fish, All }

        /// <summary>
        /// 清零指定类型的 NaturalResourceCell.m_Used 字段。
        /// </summary>
        /// <param name="world">当前游戏 World 实例</param>
        /// <param name="type">要恢复的资源类型</param>
        public static void ClearUsed(World world, ResourceType type)
        {
            if (world == null)
            {
                Mod.Logger.Warn("World 实例为空，无法执行恢复");
                return;
            }

            var system = world.GetExistingSystemManaged<NaturalResourceSystem>();
            if (system == null)
            {
                Mod.Logger.Warn("未检测到 NaturalResourceSystem，无法执行恢复");
                return;
            }

            // GetData(readOnly: false) — 获取可写缓冲区
            // 如果 MapExt2 激活，底层调用会被自动透明路由至扩展缓冲区
            var data = system.GetData(false, out var deps);
            deps.Complete(); // Settings 回调在主线程执行，安全 Complete

            int count = data.m_Buffer.Length;

            // switch 提至循环外层，避免循环不变分支的冗余判断，提高可读性
            switch (type)
            {
                case ResourceType.Fertility:
                    for (int i = 0; i < count; i++)
                    {
                        var cell = data.m_Buffer[i];
                        cell.m_Fertility.m_Used = 0;
                        data.m_Buffer[i] = cell;
                    }
                    break;

                case ResourceType.Ore:
                    for (int i = 0; i < count; i++)
                    {
                        var cell = data.m_Buffer[i];
                        cell.m_Ore.m_Used = 0;
                        data.m_Buffer[i] = cell;
                    }
                    break;

                case ResourceType.Oil:
                    for (int i = 0; i < count; i++)
                    {
                        var cell = data.m_Buffer[i];
                        cell.m_Oil.m_Used = 0;
                        data.m_Buffer[i] = cell;
                    }
                    break;

                case ResourceType.Fish:
                    for (int i = 0; i < count; i++)
                    {
                        var cell = data.m_Buffer[i];
                        cell.m_Fish.m_Used = 0;
                        data.m_Buffer[i] = cell;
                    }
                    break;

                case ResourceType.All:
                    for (int i = 0; i < count; i++)
                    {
                        var cell = data.m_Buffer[i];
                        cell.m_Fertility.m_Used = 0;
                        cell.m_Ore.m_Used = 0;
                        cell.m_Oil.m_Used = 0;
                        cell.m_Fish.m_Used = 0;
                        data.m_Buffer[i] = cell;
                    }
                    break;
            }

            // 通知系统数据已修改
            // AddWriter 会直接覆盖 m_WriteDependencies（非 Combine）。
            // 此处传入 default(JobHandle) 是安全的：
            //   1. deps.Complete() 已完成了所有待处理的读写依赖
            //   2. 缓冲区修改是在主线程同步完成的，无异步 Job 需要追踪
            //   3. default(JobHandle) 代表"已完成"，正确反映当前状态
            system.AddWriter(default);
            Mod.Logger.Info($"[SimpleBrush] 已重置耗尽资源: {type} ({count} cells)");
        }
    }
}
