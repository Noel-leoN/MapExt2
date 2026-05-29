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
        #region Fields

        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_TerraformingQuery;
        private bool m_Unlocked;

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

            m_Unlocked = true;
            Enabled = false; // 执行一次后禁用自身

            // === 按 PrefabID 精确查找 "Terraforming" UI 分类分组 ===
            UIAssetCategoryPrefab terraformingGroup = null;
            if (m_PrefabSystem.TryGetPrefab(
                    new PrefabID(nameof(UIAssetCategoryPrefab), "Terraforming"), out var groupPrefab)
                && groupPrefab is UIAssetCategoryPrefab category)
            {
                terraformingGroup = category;
            }
            else
            {
                Mod.Logger.Warn("未找到原版 'Terraforming' UI 分组，资源画笔可能无法正确归类");
            }

            // === 遍历所有 TerraformingData Entity 并解锁资源类型 ===
            var entities = m_TerraformingQuery.ToEntityArray(Allocator.Temp);
            int unlockedCount = 0;

            foreach (var entity in entities)
            {
                if (!m_PrefabSystem.TryGetPrefab(entity, out TerraformingPrefab prefab)) continue;

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
                ui.active = true;
                ui.m_IsDebugObject = false;
                ui.m_Icon = GetIconForTarget(prefab.m_Target);
                ui.m_Priority = GetPriorityForTarget(prefab.m_Target);

                // --- 从原先的分组中移除（如果存在旧分组） ---
                if (ui.m_Group != null)
                {
                    RemoveElementFromGroup(ui.m_Group, entity);
                }

                // --- 归入 Terraforming 分类分组 ---
                if (terraformingGroup != null)
                {
                    ui.m_Group = terraformingGroup;
                    // 使用原版 UIGroupPrefab.AddElement(EntityManager, Entity)
                    terraformingGroup.AddElement(EntityManager, entity);
                }

                // --- 将 UIObjectData 同步写入 ECS Entity ---
                // 内联实现 ExtraLib 的 ToComponentData() + AddOrSetComponentData()
                var uiData = new UIObjectData
                {
                    m_Group = (ui.m_Group != null)
                        ? m_PrefabSystem.GetEntity(ui.m_Group)
                        : Entity.Null,
                    m_Priority = ui.m_Priority
                };

                if (EntityManager.HasComponent<UIObjectData>(entity))
                    EntityManager.SetComponentData(entity, uiData);
                else
                    EntityManager.AddComponentData(entity, uiData);

                unlockedCount++;
            }

            entities.Dispose();
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
        /// 等价于 ExtraLib 的 UIGroupPrefab.RemoveElement() 扩展方法。
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
