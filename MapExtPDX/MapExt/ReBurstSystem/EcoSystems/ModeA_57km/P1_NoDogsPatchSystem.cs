using Unity.Entities;
using Unity.Collections;
using Game;
using Game.Prefabs;
using Game.Citizens;
using Game.Common;
using Game.Tools;

namespace MapExtPDX.ModeA
{
    public partial class P1_NoDogsPatchSystem : GameSystemBase
    {
        private EntityQuery m_HouseholdDataQuery;
        private EntityQuery m_HouseholdPetQuery;
        private bool m_HasInitializedPrefabs = false;
        
        protected override void OnCreate()
        {
            base.OnCreate();
            
            // 查询所有携带有 HouseholdData 的预制体 Entity
            m_HouseholdDataQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadWrite<HouseholdData>(), ComponentType.ReadOnly<PrefabData>() }
            });
            
            // 查询所有活着的家庭宠物逻辑对象
            m_HouseholdPetQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<HouseholdPet>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
        }

        protected override void OnUpdate()
        {
            // 在游戏加载后第一次进入 Update 并发现 Prefab 已就绪时，执行一次初始数据覆写
            if (!m_HasInitializedPrefabs && !m_HouseholdDataQuery.IsEmptyIgnoreFilter)
            {
                var settings = Mod.Instance?.CurrentSettings;
                bool preventGen = settings?.NoDogsGeneration ?? false;
                ApplyPrefabChanges(preventGen);
                m_HasInitializedPrefabs = true;
                
                // 禁用本系统的循环更新，采用纯事件（UI操作）驱动模式，避免性能浪费
                Enabled = false;
            }
        }

        /// <summary>
        /// 提供给外部或UI展示当前存档仍在活动的逻辑宠物总数
        /// </summary>
        public int CountPets()
        {
            if (!m_HouseholdPetQuery.IsEmptyIgnoreFilter)
            {
                return m_HouseholdPetQuery.CalculateEntityCount();
            }
            return 0;
        }

        /// <summary>
        /// 被外部 ModSettings UI 勾选更改时调用
        /// </summary>
        public void ApplySettings(bool preventGeneration, bool purgeExisting)
        {
            ApplyPrefabChanges(preventGeneration);

            if (purgeExisting)
            {
                PurgeAllPets();
            }
        }

        private void ApplyPrefabChanges(bool preventGeneration)
        {
            int targetFirst = preventGeneration ? 0 : 20;
            int targetNext = preventGeneration ? 0 : 10;

            if (m_HouseholdDataQuery.IsEmptyIgnoreFilter)
            {
                Mod.Info("P1_NoDogsPatchSystem: HouseholdData prefabs not loaded yet or query is empty.");
                return;
            }

            int count = 0;
            using (var entities = m_HouseholdDataQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var entity in entities)
                {
                    if (EntityManager.HasComponent<HouseholdData>(entity))
                    {
                        var data = EntityManager.GetComponentData<HouseholdData>(entity);
                        if (data.m_FirstPetProbability != targetFirst || data.m_NextPetProbability != targetNext)
                        {
                            data.m_FirstPetProbability = targetFirst;
                            data.m_NextPetProbability = targetNext;
                            EntityManager.SetComponentData(entity, data);
                            count++;
                        }
                    }
                }
            }
            Mod.Info($"P1_NoDogsPatchSystem: Updated {count} HouseholdData prefabs. FirstPet={targetFirst}%, NextPet={targetNext}%");
        }

        private void PurgeAllPets()
        {
            if (m_HouseholdPetQuery.IsEmptyIgnoreFilter)
            {
                Mod.Info("P1_NoDogsPatchSystem: No logical pets found to purge.");
                return;
            }

            int count = m_HouseholdPetQuery.CalculateEntityCount();
            
            // 安全地为所有逻辑宠物添加 Deleted 组件，依赖底层机制完成物理与逻辑上的清理
            EntityManager.AddComponent<Deleted>(m_HouseholdPetQuery);
            
            // 同步清理所有家庭的 HouseholdAnimal 动态缓冲，防止 UI 与其它模块读到死 Entity 报错
            var householdWithAnimalsQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadWrite<HouseholdAnimal>() }
            });
            
            if (!householdWithAnimalsQuery.IsEmptyIgnoreFilter)
            {
                using var entities = householdWithAnimalsQuery.ToEntityArray(Allocator.Temp);
                foreach (var entity in entities)
                {
                    var buffer = EntityManager.GetBuffer<HouseholdAnimal>(entity);
                    buffer.Clear();
                }
            }

            Mod.Info($"P1_NoDogsPatchSystem: Successfully queued {count} logical pets for deletion tracking (!Purge!).");
        }
    }
}
