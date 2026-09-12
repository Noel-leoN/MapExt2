using Game;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace SimpleBrush.Core
{
    /// <summary>
    /// 一次性解锁原版隐藏的自然资源放置工具。
    /// 在 PrefabUpdate 阶段运行，定位所有 TerraformingData Entity，
    /// 将 Ore/Oil/FertileLand/GroundWater 类型的画笔激活并归入 "Terraforming" 工具栏分组。
    /// </summary>
    public partial class TerraformingUnlocker : GameSystemBase
    {
        #region Constants

        /// <summary>等待 "Terraforming" UI 分組載入的最長帧數，逾時後記錄警告並放棄。</summary>
        private const int kMaxGroupWaitFrames = 1800;

        #endregion

        #region Fields

        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_TerraformingQuery;
        private bool m_Unlocked;
        private int m_GroupWaitFrames;

        #endregion

        #region System Loop

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_TerraformingQuery = GetEntityQuery(ComponentType.ReadOnly<TerraformingData>());
        }

        protected override void OnUpdate()
        {
            if (m_Unlocked) return;
            if (m_TerraformingQuery.IsEmptyIgnoreFilter) return;

            // === 按 PrefabID 精确查找 "Terraforming" UI 分类分组 ===
            // 找不到時直接返回、不設 m_Unlocked：PrefabUpdate 每帧無條件驅動，下一帧會重試。
            // 否則會做出「畫筆解鎖了、但 m_Group 仍指向舊分組或 Entity.Null」的半套結果且永不重試。
            UIAssetCategoryPrefab terraformingGroup = null;
            if (m_PrefabSystem.TryGetPrefab(
                    new PrefabID(nameof(UIAssetCategoryPrefab), "Terraforming"), out var groupPrefab))
            {
                // TryGetPrefab 的泛型多載以未檢查的 as 轉型，回 true 亦可能交回 null，故一律自行判空。
                terraformingGroup = groupPrefab as UIAssetCategoryPrefab;
            }

            if (terraformingGroup == null)
            {
                if (++m_GroupWaitFrames < kMaxGroupWaitFrames) return;

                m_Unlocked = true;
                Enabled = false;
                Mod.Logger.Warn("未找到原版 'Terraforming' UI 分组，资源画笔无法正确归类，已放弃解锁");
                return;
            }

            // === 遍历所有 TerraformingData Entity 并解锁资源类型 ===
            var entities = m_TerraformingQuery.ToEntityArray(Allocator.Temp);
            int unlockedCount = 0;

            try
            {
                foreach (var entity in entities)
                {
                    if (!m_PrefabSystem.TryGetPrefab(entity, out TerraformingPrefab prefab) || prefab == null) continue;

                    // 仅解锁资源类型画笔（跳过地形高度和材质工具，它们已在工具栏中可见）
                    if (prefab.m_Target == TerraformingTarget.Material ||
                        prefab.m_Target == TerraformingTarget.Height ||
                        prefab.m_Target == TerraformingTarget.None)
                    {
                        continue;
                    }

                    // --- 确保 UIObject 组件存在 ---
                    var ui = prefab.GetComponent<UIObject>();
                    if (ui == null)
                    {
                        ui = prefab.AddComponent<UIObject>();
                    }

                    // --- 始终强制确保激活状态和属性正确 ---
                    // active 与 m_IsDebugObject 属 authoring 层，其消费者只有 UIObject 的
                    // GetPrefabComponents 与 LateInitialize，两者在本系统运行前均已执行完毕；
                    // 因此这两行对当次 session 无效果，仅为日后 UpdatePrefab 重建时保留。
                    ui.active = true;
                    ui.m_IsDebugObject = false;
                    // m_Icon 为 authoring-only（无 *Data 对应），工具栏每次 bind 现读，必须写在这里。
                    ui.m_Icon = GetIconForTarget(prefab.m_Target);
                    ui.m_Priority = GetPriorityForTarget(prefab.m_Target);

                    // --- 从原先的分组中移除（如果存在旧分组） ---
                    if (ui.m_Group != null)
                    {
                        RemoveElementFromGroup(ui.m_Group, entity);
                    }

                    // --- 归入 Terraforming 分类分组 ---
                    ui.m_Group = terraformingGroup;
                    // 使用原版 UIGroupPrefab.AddElement(EntityManager, Entity)
                    terraformingGroup.AddElement(EntityManager, entity);

                    // --- 将 UIObjectData 同步写入 ECS Entity ---
                    // UIObjectData 只有 m_Group／m_Priority 两个字段，构造与「Has 则 Set、否则 Add」
                    // 的分支均无第二种写法；就地内联以免为这几行引入 ExtraLib 依赖
                    //（该库有同功能的扩展方法）。
                    var uiData = new UIObjectData
                    {
                        m_Group = m_PrefabSystem.GetEntity(ui.m_Group),
                        m_Priority = ui.m_Priority
                    };

                    if (EntityManager.HasComponent<UIObjectData>(entity))
                        EntityManager.SetComponentData(entity, uiData);
                    else
                        EntityManager.AddComponentData(entity, uiData);

                    unlockedCount++;
                }
            }
            finally
            {
                entities.Dispose();

                // 不論成功或中途拋錯都自我停用：OnUpdate 拋出的例外會每帧記一次 Critical
                // 並彈出模態錯誤框暫停模擬，重試的代價遠高於接受一次部分解鎖。
                m_Unlocked = true;
                Enabled = false;
            }

            Mod.Logger.Info($"SimpleBrush 成功解锁了 {unlockedCount} 个自然资源放置画笔");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 根据资源类型返回原版游戏内置的对应图标路径。
        /// 图标路径来源于反编译的 MapTilesUISystem / NaturalResourcesTooltipSystem。
        /// </summary>
        private static string GetIconForTarget(TerraformingTarget target)
        {
            switch (target)
            {
                case TerraformingTarget.FertileLand: return "Media/Game/Icons/Fertility.svg";
                case TerraformingTarget.Oil:         return "Media/Game/Icons/Oil.svg";
                case TerraformingTarget.Ore:         return "Media/Game/Icons/Coal.svg";
                case TerraformingTarget.GroundWater:  return "Media/Game/Icons/Water.svg";
                default:                             return "Media/Placeholder.svg";
            }
        }

        /// <summary>
        /// 根据资源类型返回工具栏中的显示顺序权重。
        /// </summary>
        private static int GetPriorityForTarget(TerraformingTarget target)
        {
            switch (target)
            {
                case TerraformingTarget.FertileLand: return 101;
                case TerraformingTarget.Ore:         return 102;
                case TerraformingTarget.Oil:         return 103;
                case TerraformingTarget.GroundWater:  return 104;
                default:                             return 100;
            }
        }

        /// <summary>
        /// 从 UIGroupPrefab 的 ECS Buffer 中移除指定 Entity。
        /// <para>原版 <see cref="UIGroupPrefab"/> 只提供 <c>AddElement</c>，且它同时写入
        /// <c>UIGroupElement</c> 与 <c>UnlockRequirement</c> 两个 buffer，因此撤销只能自行遍历
        /// 这两处逐一移除。ExtraLib 也补过同一个缺口（<c>RemoveElement</c> 扩展方法）。</para>
        /// </summary>
        private void RemoveElementFromGroup(UIGroupPrefab group, Entity entity)
        {
            if (!m_PrefabSystem.TryGetEntity(group, out var groupEntity)) return;

            // 从 UIGroupElement buffer 中移除
            if (EntityManager.HasBuffer<UIGroupElement>(groupEntity))
            {
                var buffer = EntityManager.GetBuffer<UIGroupElement>(groupEntity);
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].m_Prefab == entity)
                    {
                        buffer.RemoveAt(i);
                        break;
                    }
                }
            }

            // 从 UnlockRequirement buffer 中移除
            if (EntityManager.HasBuffer<UnlockRequirement>(groupEntity))
            {
                var unlockBuffer = EntityManager.GetBuffer<UnlockRequirement>(groupEntity);
                for (int i = 0; i < unlockBuffer.Length; i++)
                {
                    if (unlockBuffer[i].m_Prefab == entity)
                    {
                        unlockBuffer.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        #endregion
    }
}
