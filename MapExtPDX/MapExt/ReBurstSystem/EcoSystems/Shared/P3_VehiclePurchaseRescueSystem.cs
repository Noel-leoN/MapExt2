// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.

using Game;
using Game.Buildings;
using Game.Common;
using Game.Objects;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using MapExtPDX.MapExt.Core;
using Unity.Collections;
using Unity.Entities;

namespace MapExtPDX.EcoShared
{
    #region Component

    /// <summary>
    /// 标记已被购车救援系统处理过的车辆，防止重复传送。
    /// m_RetryCount 追踪重试次数，超过上限后删除僵尸车辆。
    /// </summary>
    public struct VehicleRescued : IComponentData
    {
        public int m_RetryCount;
    }

    #endregion

    /// <summary>
    /// 修复原版市民在商铺购车后因停车位不足导致车辆丢失的Bug。
    /// 
    /// 原版流程：ResourceBuyerSystem 在商铺创建车辆(stopped=true) → InitializeSystem.FindParkingSpace()
    /// 在商铺附近查找车位 → 若失败，ParkedCar.m_Lane 保持 Entity.Null，无任何系统兜底。
    /// 
    /// 修复策略：检测 m_Lane==Null 的新购私家车，传送到车主住宅附近，
    /// 利用原版 FixParkingLocation 的 m_ResetLocation 机制让原版系统以住宅为中心重新查找车位。
    /// 低频运行（每64帧一次），配合重试上限防止无限循环。
    /// </summary>
    public partial class P3_VehiclePurchaseRescueSystem : GameSystemBase
    {
        #region Constants and Fields

        private const string Tag = "P3_VehiclePurchaseRescue";

        /// <summary>
        /// 低频更新间隔：每 64 模拟帧执行一次。
        /// 买车是低频事件，无需每帧扫描。
        /// </summary>
        private const int kUpdateInterval = 64;

        /// <summary>
        /// 最大重试次数。超过后删除僵尸车辆。
        /// 每次重试间隔约 64 帧，8 次 ≈ 512 帧 ≈ 游戏内约 0.5 小时，足够等待车位空出。
        /// </summary>
        private const int kMaxRetries = 8;

        // === 阶段1：检测首次停放失败的新购车辆 ===
        private EntityQuery m_RescueQuery;

        // === 阶段2：对已救援但仍失败的车辆重试 ===
        private EntityQuery m_RetryQuery;

        // === ComponentLookup 用于高效访问 ===
        private ComponentLookup<ParkedCar> m_ParkedCarLookup;
        private ComponentLookup<PersonalCar> m_PersonalCarLookup;
        private ComponentLookup<Owner> m_OwnerLookup;
        private ComponentLookup<PropertyRenter> m_PropertyRenterLookup;
        private ComponentLookup<Game.Objects.Transform> m_TransformLookup;

        // === 低频更新 ===
        private SimulationSystem m_SimulationSystem;

        #endregion

        #region System Loop

        /// <summary>
        /// 低频更新：每 kUpdateInterval 帧执行一次。
        /// </summary>
        public override int GetUpdateInterval(SystemUpdatePhase phase) => kUpdateInterval;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();

            // --- 阶段1 Query：首次检测未停放的新购私家车 ---
            // ParkedCar + PersonalCar + Owner + Unspawned
            // 排除：Created（未初始化）、Deleted、Temp、FixParkingLocation（正在被原版处理）、VehicleRescued（已救援过）
            m_RescueQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<ParkedCar>(),
                    ComponentType.ReadOnly<PersonalCar>(),
                    ComponentType.ReadOnly<Owner>(),
                    ComponentType.ReadOnly<Unspawned>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Created>(),
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<FixParkingLocation>(),
                    ComponentType.ReadOnly<VehicleRescued>()
                }
            });

            // --- 阶段2 Query：对已救援但仍失败的车辆重试 ---
            // 有 VehicleRescued 标记 + 仍然 Unspawned + FixParkingLocation 已被原版系统处理移除
            m_RetryQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<ParkedCar>(),
                    ComponentType.ReadOnly<PersonalCar>(),
                    ComponentType.ReadOnly<Owner>(),
                    ComponentType.ReadOnly<VehicleRescued>(),
                    ComponentType.ReadOnly<Unspawned>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<FixParkingLocation>()
                }
            });

            // 仅在有匹配实体时才激活本系统，避免空帧开销
            RequireAnyForUpdate(m_RescueQuery, m_RetryQuery);

            // 初始化 ComponentLookup
            m_ParkedCarLookup = GetComponentLookup<ParkedCar>(true);
            m_PersonalCarLookup = GetComponentLookup<PersonalCar>(true);
            m_OwnerLookup = GetComponentLookup<Owner>(true);
            m_PropertyRenterLookup = GetComponentLookup<PropertyRenter>(true);
            m_TransformLookup = GetComponentLookup<Game.Objects.Transform>(true);

            ModLog.Info(Tag, "购车救援系统已创建");
        }

        protected override void OnUpdate()
        {
            // 主开关检查：未启用时不执行任何逻辑
            if (Mod.Instance?.Settings?.EnableVehicleRescue != true)
                return;

            // 更新 ComponentLookup
            m_ParkedCarLookup.Update(this);
            m_PersonalCarLookup.Update(this);
            m_OwnerLookup.Update(this);
            m_PropertyRenterLookup.Update(this);
            m_TransformLookup.Update(this);

            // === 阶段 1：首次救援 ===
            ProcessRescue();

            // === 阶段 2：对已救援但仍失败的车辆重试 ===
            ProcessRetry();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 阶段1：检测 InitializeSystem.FindParkingSpace() 失败的新购车辆，传送到住宅附近并交由原版系统重试。
        /// </summary>
        private void ProcessRescue()
        {
            if (m_RescueQuery.IsEmptyIgnoreFilter)
                return;

            using var entities = m_RescueQuery.ToEntityArray(Allocator.Temp);
            int rescuedCount = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                Entity vehicle = entities[i];

                // 关键过滤：只处理 m_Lane 为 Null 的车辆（停放失败）
                ParkedCar parkedCar = m_ParkedCarLookup[vehicle];
                if (parkedCar.m_Lane != Entity.Null) continue;

                // 排除虚拟交通车辆
                PersonalCar pc = m_PersonalCarLookup[vehicle];
                if ((pc.m_State & PersonalCarFlags.DummyTraffic) != 0) continue;

                // 获取车主住宅
                Entity homeProperty = GetHomeProperty(vehicle);
                if (homeProperty == Entity.Null) continue;

                // 获取住宅 Transform
                if (!m_TransformLookup.TryGetComponent(homeProperty, out var homeTf)) continue;

                // 1. 添加 VehicleRescued 标记（防止重复传送），初始重试计数为 0
                EntityManager.AddComponentData(vehicle, new VehicleRescued { m_RetryCount = 0 });

                // 2. 将车辆 Transform 传送到住宅位置
                EntityManager.SetComponentData(vehicle, homeTf);

                // 3. 添加 FixParkingLocation，m_ResetLocation = homeProperty
                //    原版 FixParkingLocationSystem 会以住宅 Transform 为搜索中心，100m 范围内查找车位
                EntityManager.AddComponentData(vehicle,
                    new FixParkingLocation(Entity.Null, homeProperty));

                // 4. 添加 Updated 标记，原版 FixParkingLocationSystem.m_FixQuery 要求
                //    All={Updated} + Any={FixParkingLocation} 才能匹配到该实体
                EntityManager.AddComponent<Updated>(vehicle);

                rescuedCount++;
            }

            if (rescuedCount > 0 && Mod.Instance?.Settings?.EnableRescueDebugLog == true)
            {
                ModLog.Ok(Tag, $"购车救援：已将 {rescuedCount} 辆停放失败的新购车辆传送到住宅附近重新停放");
            }
        }

        /// <summary>
        /// 阶段2：对已救援但住宅附近仍无车位的车辆，重新挂 FixParkingLocation 持续重试。
        /// 超过最大重试次数后删除僵尸车辆。
        /// </summary>
        private void ProcessRetry()
        {
            if (m_RetryQuery.IsEmptyIgnoreFilter)
                return;

            using var entities = m_RetryQuery.ToEntityArray(Allocator.Temp);
            int retryCount = 0;
            int successCount = 0;
            int removedCount = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                Entity vehicle = entities[i];

                ParkedCar parkedCar = m_ParkedCarLookup[vehicle];

                // 已成功停放 → 移除标记，完成救援
                if (parkedCar.m_Lane != Entity.Null)
                {
                    EntityManager.RemoveComponent<VehicleRescued>(vehicle);
                    successCount++;
                    continue;
                }

                // 读取当前重试计数
                VehicleRescued rescued = EntityManager.GetComponentData<VehicleRescued>(vehicle);

                // 超过最大重试次数 → 删除僵尸车辆
                if (rescued.m_RetryCount >= kMaxRetries)
                {
                    // 清理 Household 的 OwnedVehicle Buffer 中对该车辆的引用
                    CleanupOwnership(vehicle);
                    // 标记删除
                    EntityManager.AddComponent<Deleted>(vehicle);
                    removedCount++;
                    continue;
                }

                // 仍然失败 → 递增重试计数，重新挂 FixParkingLocation 让原版系统下一周期再试
                Entity homeProperty = GetHomeProperty(vehicle);
                if (homeProperty != Entity.Null)
                {
                    rescued.m_RetryCount++;
                    EntityManager.SetComponentData(vehicle, rescued);
                    EntityManager.AddComponentData(vehicle,
                        new FixParkingLocation(Entity.Null, homeProperty));
                    EntityManager.AddComponent<Updated>(vehicle);
                    retryCount++;
                }
                else
                {
                    // 无住宅（家庭已搬走或解散）→ 直接删除
                    CleanupOwnership(vehicle);
                    EntityManager.AddComponent<Deleted>(vehicle);
                    removedCount++;
                }
            }

            if (successCount > 0 && Mod.Instance?.Settings?.EnableRescueDebugLog == true)
            {
                ModLog.Ok(Tag, $"购车救援完成：{successCount} 辆车辆已成功停放在住宅附近");
            }

            if (retryCount > 0 && Mod.Instance?.Settings?.EnableRescueDebugLog == true)
            {
                ModLog.Info(Tag, $"购车救援重试：{retryCount} 辆车辆仍在等待住宅附近车位空出");
            }

            if (removedCount > 0 && Mod.Instance?.Settings?.EnableRescueDebugLog == true)
            {
                ModLog.Warn(Tag, $"购车救援放弃：{removedCount} 辆车辆超过最大重试次数({kMaxRetries})已删除");
            }
        }

        /// <summary>
        /// 获取车辆的车主住宅 Property 实体。
        /// 链路：Vehicle → Owner.m_Owner (Household) → PropertyRenter.m_Property (Home Building)
        /// </summary>
        private Entity GetHomeProperty(Entity vehicle)
        {
            if (!m_OwnerLookup.TryGetComponent(vehicle, out var owner))
                return Entity.Null;

            if (!m_PropertyRenterLookup.TryGetComponent(owner.m_Owner, out var renter))
                return Entity.Null;

            return renter.m_Property;
        }

        /// <summary>
        /// 清理车辆与 Household 的归属关系。
        /// 从 Household 的 OwnedVehicle Buffer 中移除该车辆的引用，防止悬挂引用。
        /// </summary>
        private void CleanupOwnership(Entity vehicle)
        {
            if (!m_OwnerLookup.TryGetComponent(vehicle, out var owner))
                return;

            Entity household = owner.m_Owner;
            if (!EntityManager.HasBuffer<OwnedVehicle>(household))
                return;

            var buffer = EntityManager.GetBuffer<OwnedVehicle>(household);
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].m_Vehicle == vehicle)
                {
                    buffer.RemoveAt(i);
                    break;
                }
            }
        }

        #endregion
    }
}
