// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using Game;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using EconomyEX;
using EconomyEX.Helpers;

namespace EconomyEX.Systems
{
    /// <summary>
    /// P2: Editor Collision Override System
    /// 绕过 GenericJobReplacer 的校验限制，作为独立的 System 在 ValidationSystem 之后运行，
    /// 专门移除由于碰撞检测而标记到待放置对象（Temp）上的 Error 和 Warning。
    /// 可以由 ModSettings 中的三档开关控制。
    /// </summary>
    [UpdateAfter(typeof(ValidationSystem))]
    public partial class P2_EditorCollisionOverrideSystem : GameSystemBase
    {
        private const string Tag = "P2_CollisionSys";
        private ToolSystem m_ToolSystem;
        private EntityQuery m_ErrorQuery;
        private ModificationEndBarrier m_ModificationBarrier;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_ModificationBarrier = World.GetOrCreateSystemManaged<ModificationEndBarrier>();

            // 只查询工具正在放置的物体 (Temp)，且包含 Error 或 Warning
            m_ErrorQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<PrefabRef>() },
                Any = new[] { ComponentType.ReadOnly<Error>(), ComponentType.ReadOnly<Warning>() },
                None = new[] { ComponentType.ReadOnly<Deleted>() } // 忽略已删除
            });

            RequireForUpdate(m_ErrorQuery);
            ModLog.Ok(Tag, "System Created.");
        }

        protected override void OnUpdate()
        {
            if (!m_ToolSystem.actionMode.IsEditor())
                return;

            var skipMode = Mod.Instance?.Settings?.EditorCollisionSkip ?? EditorCollisionSkipMode.Off;
            if (skipMode == EditorCollisionSkipMode.Off)
                return;

            var job = new OverrideCollisionJob
            {
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_PrefabRefType = SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
                m_TreeDataLookup = SystemAPI.GetComponentLookup<TreeData>(true),
                m_SkipMode = skipMode,
                m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer().AsParallelWriter()
            };

            Dependency = job.ScheduleParallel(m_ErrorQuery, Dependency);
            m_ModificationBarrier.AddJobHandleForProducer(Dependency);
        }

        [BurstCompile]
        private struct OverrideCollisionJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle m_EntityType;
            [ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabRefType;
            [ReadOnly] public ComponentLookup<TreeData> m_TreeDataLookup;
            public EditorCollisionSkipMode m_SkipMode;
            public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(m_EntityType);
                var prefabRefs = chunk.GetNativeArray(ref m_PrefabRefType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    bool shouldRemove = false;
                    
                    if (m_SkipMode == EditorCollisionSkipMode.AllObjects)
                    {
                        shouldRemove = true;
                    }
                    else if (m_SkipMode == EditorCollisionSkipMode.TreesOnly)
                    {
                        if (m_TreeDataLookup.HasComponent(prefabRefs[i].m_Prefab))
                        {
                            shouldRemove = true;
                        }
                    }

                    if (shouldRemove)
                    {
                        m_CommandBuffer.RemoveComponent<Error>(unfilteredChunkIndex, entities[i]);
                        m_CommandBuffer.RemoveComponent<Warning>(unfilteredChunkIndex, entities[i]);
                    }
                }
            }
        }
    }
}
