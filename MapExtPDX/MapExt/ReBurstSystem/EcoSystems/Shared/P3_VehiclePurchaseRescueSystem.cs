// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.

using Game;
using Game.Buildings;
using Game.Common;
using Game.Objects;

using Game.Tools;
using Game.Vehicles;
using MapExtPDX.MapExt.Core;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;

namespace MapExtPDX.EcoShared
{
    /// <summary>
    /// 修复原版市民在商铺购车后因停车位不足导致车辆丢失的Bug。
    /// 
    /// 原版流程：ResourceBuyerSystem 在商铺创建车辆(stopped=true) → InitializeSystem.FindParkingSpace()
    /// 在商铺附近查找车位 → 若失败，ParkedCar.m_Lane 保持 Entity.Null，无任何系统兜底。
    /// 
    /// 修复策略：检测 m_Lane==Null 的新购私家车，传送到车主住宅附近，
    /// 利用原版 FixParkingLocation 的 m_ResetLocation 机制让原版系统以住宅为中心重新查找车位。
    /// 低频运行（每64帧一次），配合重试上限防止无限循环。
    /// 
    /// 【设计】使用纯内存 Dictionary 追踪已救援车辆，不注入任何自定义 ECS 组件到实体上，
    /// 确保存档零污染，禁用 Mod 后不留痕迹。
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

        // === 候选车辆 Query：检测所有可能需要救援的新购车辆 ===
        private EntityQuery m_CandidateQuery;

        // === 纯内存追踪：已救援车辆 → 重试计数 ===
        // 不注入任何自定义 Component，存档零污染
        private readonly Dictionary<Entity, int> m_RescuedVehicles = new();

        // === 文件日志路径（延迟初始化） ===
        private string m_LogFilePath;

        // === ComponentLookup 用于高效访问 ===
        private ComponentLookup<ParkedCar> m_ParkedCarLookup;
        private ComponentLookup<PersonalCar> m_PersonalCarLookup;
        private ComponentLookup<Owner> m_OwnerLookup;
        private ComponentLookup<PropertyRenter> m_PropertyRenterLookup;
        private ComponentLookup<Game.Objects.Transform> m_TransformLookup;

        #endregion

        #region System Loop

        /// <summary>
        /// 低频更新：每 kUpdateInterval 帧执行一次。
        /// </summary>
        public override int GetUpdateInterval(SystemUpdatePhase phase) => kUpdateInterval;

        protected override void OnCreate()
        {
            base.OnCreate();

            // --- 候选 Query：所有可能需要救援的新购私家车 ---
            // ParkedCar + PersonalCar + Owner + Unspawned
            // 排除：Created（未初始化）、Deleted、Temp、FixParkingLocation（正在被原版处理）
            // 注意：不再排除 VehicleRescued（已移除该自定义组件），改用 Dictionary 过滤
            m_CandidateQuery = GetEntityQuery(new EntityQueryDesc
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
                    ComponentType.ReadOnly<FixParkingLocation>()
                }
            });

            // 仅在有匹配实体时才激活本系统，避免空帧开销
            RequireForUpdate(m_CandidateQuery);

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

            // 统一 Query 结果
            using var entities = m_CandidateQuery.ToEntityArray(Allocator.Temp);

            // 使用 EntityCommandBuffer 延迟执行结构性修改，防止 ComponentLookup 在循环中失效
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            {
                // === 阶段 1：首次救援新车辆 ===
                ProcessRescue(entities, ecb);

                // === 阶段 2：对已救援但仍失败的车辆重试 ===
                ProcessRetry(entities, ecb);

                // === 清理：移除已不在 Query 中的陈旧记录 ===
                CleanupStaleEntries(entities);

                // 统一执行所有记录的修改
                ecb.Playback(EntityManager);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 阶段1：检测 InitializeSystem.FindParkingSpace() 失败的新购车辆，传送到住宅附近并交由原版系统重试。
        /// </summary>
        private void ProcessRescue(NativeArray<Entity> entities, EntityCommandBuffer ecb)
        {
            int rescuedCount = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                Entity vehicle = entities[i];

                // 跳过已在追踪中的车辆（由阶段2处理）
                if (m_RescuedVehicles.ContainsKey(vehicle)) continue;

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

                // 1. 记录到内存追踪表（初始重试计数为 0）
                m_RescuedVehicles[vehicle] = 0;

                // 2. 将车辆 Transform 传送到住宅位置
                ecb.SetComponent(vehicle, homeTf);

                // 3. 添加 FixParkingLocation，m_ResetLocation = homeProperty
                //    原版 FixParkingLocationSystem 会以住宅 Transform 为搜索中心，100m 范围内查找车位
                ecb.AddComponent(vehicle, new FixParkingLocation(Entity.Null, homeProperty));

                // 4. 添加 Updated 标记，原版 FixParkingLocationSystem.m_FixQuery 要求
                //    All={Updated} + Any={FixParkingLocation} 才能匹配到该实体
                ecb.AddComponent<Updated>(vehicle);

                rescuedCount++;
            }

            if (rescuedCount > 0)
                DebugLog($"购车救援：已将 {rescuedCount} 辆停放失败的新购车辆传送到住宅附近重新停放");
        }

        /// <summary>
        /// 阶段2：对已救援但住宅附近仍无车位的车辆，重新挂 FixParkingLocation 持续重试。
        /// 超过最大重试次数后删除僵尸车辆。
        /// </summary>
        private void ProcessRetry(NativeArray<Entity> entities, EntityCommandBuffer ecb)
        {
            // 拷贝所有的追踪 Key 以免在循环中修改字典导致 Enumerator 失效
            using var keys = new NativeList<Entity>(m_RescuedVehicles.Count, Allocator.Temp);
            foreach (var key in m_RescuedVehicles.Keys)
            {
                keys.Add(key);
            }

            // 收集需要从追踪表中移除的 Entity（避免遍历中修改字典）
            using var toRemove = new NativeList<Entity>(Allocator.Temp);
            int retryCount = 0;
            int successCount = 0;
            int removedCount = 0;

            for (int i = 0; i < keys.Length; i++)
            {
                Entity vehicle = keys[i];
                if (!m_RescuedVehicles.TryGetValue(vehicle, out int currentRetry))
                    continue;

                // 实体已被销毁或不再匹配 Query → 清理
                if (!EntityManager.Exists(vehicle) || !m_ParkedCarLookup.HasComponent(vehicle))
                {
                    toRemove.Add(vehicle);
                    continue;
                }

                // FixParkingLocation 仍挂着 → 原版系统还没处理完，跳过
                if (EntityManager.HasComponent<FixParkingLocation>(vehicle))
                    continue;

                ParkedCar parkedCar = m_ParkedCarLookup[vehicle];

                // 已成功停放 → 从追踪表移除，完成救援
                if (parkedCar.m_Lane != Entity.Null)
                {
                    toRemove.Add(vehicle);
                    successCount++;
                    continue;
                }

                // 超过最大重试次数 → 删除僵尸车辆
                if (currentRetry >= kMaxRetries)
                {
                    CleanupOwnership(vehicle);
                    ecb.AddComponent<Deleted>(vehicle);
                    toRemove.Add(vehicle);
                    removedCount++;
                    continue;
                }

                // 仍然失败 → 递增重试计数，重新挂 FixParkingLocation 让原版系统下一周期再试
                Entity homeProperty = GetHomeProperty(vehicle);
                if (homeProperty != Entity.Null)
                {
                    m_RescuedVehicles[vehicle] = currentRetry + 1;
                    ecb.AddComponent(vehicle, new FixParkingLocation(Entity.Null, homeProperty));
                    ecb.AddComponent<Updated>(vehicle);
                    retryCount++;
                }
                else
                {
                    // 无住宅（家庭已搬走或解散）→ 直接删除
                    CleanupOwnership(vehicle);
                    ecb.AddComponent<Deleted>(vehicle);
                    toRemove.Add(vehicle);
                    removedCount++;
                }
            }

            // 批量清理追踪表
            for (int i = 0; i < toRemove.Length; i++)
            {
                m_RescuedVehicles.Remove(toRemove[i]);
            }

            if (successCount > 0)
                DebugLog($"购车救援完成：{successCount} 辆车辆已成功停放在住宅附近");
            if (retryCount > 0)
                DebugLog($"购车救援重试：{retryCount} 辆车辆仍在等待住宅附近车位空出");
            if (removedCount > 0)
                DebugLog($"购车救援放弃：{removedCount} 辆车辆超过最大重试次数({kMaxRetries})已删除");
        }

        /// <summary>
        /// 清理陈旧的追踪记录：移除已不存在于 ECS 世界中的实体。
        /// 防止 Dictionary 内存泄漏。
        /// </summary>
        private void CleanupStaleEntries(NativeArray<Entity> entities)
        {
            if (m_RescuedVehicles.Count == 0)
                return;

            // 构建当前 Query 匹配的实体集合
            var activeSet = new HashSet<Entity>(entities.Length);
            for (int i = 0; i < entities.Length; i++)
            {
                activeSet.Add(entities[i]);
            }

            // 移除不在 Query 中且实体已不存在的记录
            using var toRemove = new NativeList<Entity>(Allocator.Temp);
            foreach (var kvp in m_RescuedVehicles)
            {
                if (!activeSet.Contains(kvp.Key) && !EntityManager.Exists(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toRemove.Length; i++)
            {
                m_RescuedVehicles.Remove(toRemove[i]);
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

        #region Debug File Log

        /// <summary>
        /// 文件日志：绕过 Unity/Colossal Logger，直接写入 ModsData 目录下的独立日志文件。
        /// 仅在 EnableRescueDebugLog 开启时写入，低频调用无性能影响。
        /// </summary>
        private void DebugLog(string message)
        {
            if (Mod.Instance?.Settings?.EnableRescueDebugLog != true)
                return;

            try
            {
                if (m_LogFilePath == null)
                {
                    var dir = Path.Combine(
                        UnityEngine.Application.persistentDataPath,
                        "ModsData", "MapExt2");
                    Directory.CreateDirectory(dir);
                    m_LogFilePath = Path.Combine(dir, "rescue_debug.log");
                }

                File.AppendAllText(m_LogFilePath,
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { /* 静默：文件 I/O 失败不应影响游戏 */ }
        }

        #endregion
    }
}
