// Game.Simulation.PopulationToGridSystem : CellMapSystem<PopulationCell>, IJobSerializable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Colossal.Entities;
using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Objects;
using Game.Simulation; 
using Game.Tools;
using HarmonyLib;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    // =========================================================================================
    // [配置区域]
    // =========================================================================================
    // 1. 基类泛型
    using BaseCellMapSystem = CellMapSystem<PopulationCell>;
    // 2. Mod 自定义系统类型 (当前类)
    using ModSystem = PopulationToGridSystemMod;
    // 3. 数据包泛型 (用于 GetData)
    using TargetCellMapData = CellMapData<PopulationCell>;
    // 4. 原版系统类型 (用于禁用和定位)
    using TargetSystem = PopulationToGridSystem;
    // 5. T struct
    using TargetType = PopulationCell;
    // =========================================================================================

    /// <summary>
    /// 人口映射系统
    /// 将住宅内的居民数量映射到 PopulationCell 网格中，用于生成人口密度数据。
    /// </summary>
    public partial class PopulationToGridSystemMod : BaseCellMapSystem, IJobSerializable
    {
        #region 字段/配置
        // 单例引用，便于Harmony补丁调用
        public static ModSystem Instance { get; private set; }
        
        // 纹理尺寸(vanilla=256)
        public static readonly int orgTextureSize = TargetSystem.kTextureSize; // 原版尺寸
        public static readonly int kTextureSize = CellMapSystemRe.PopulationToGridSystemkTextureSize; // mod尺寸
        public int2 TextureSize => new int2(kTextureSize, kTextureSize);

        // 系统更新周期：每日(月)32次
        public static readonly int kUpdatesPerDay = 32;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / kUpdatesPerDay;
        #endregion

        #region 查询和系统引用
        // 用于筛选住宅资产的查询
        private EntityQuery m_ResidentialPropertyQuery;
        #endregion

        #region System Loop
        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;

            // 1.禁用原版系统并获取原版系统引用
            // 使用 GetExistingSystemManaged 避免意外创建未初始化的系统
            var originalSystem = World.GetExistingSystemManaged<TargetSystem>();
            if (originalSystem != null)
            {
                originalSystem.Enabled = false;
                // #if DEBUG
                Mod.Info($"[{typeof(ModSystem).Name}] 禁用原系统: {typeof(TargetSystem).Name}");
                // #endif
            }
            else
            {
                // 仅在调试时提示，原版系统可能已被其他Mod移除或尚未加载
#if DEBUG
                Mod.Error($"[{typeof(ModSystem).Name}] 无法找到可禁用的原系统(尚未加载或可能被其他Mod移除): {typeof(TargetSystem).Name}");
#endif
            }

            // 2. 创建自定义大小纹理
            CreateTextures(kTextureSize);
            // #if DEBUG
            Mod.Info($"[{typeof(ModSystem).Name}] 创建自定义纹理: {typeof(TargetSystem).Name} kTextureSize 从 原值{TargetSystem.kTextureSize} 变更为 目标值{this.m_TextureSize.x}");
            // #endif

            // 3. 获取其他依赖和查询
            // 构建查询：筛选所有具有住宅属性、租赁者信息和Transform组件的实体
            // 排除已销毁、已删除或临时的实体
            m_ResidentialPropertyQuery = SystemAPI.QueryBuilder()
                .WithAll<ResidentialProperty, Renter, Transform>()
                .WithNone<Destroyed, Deleted, Temp>()
                .Build();
        }

        protected override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            // 获取组件和缓冲区的Lookup数据 (只读权限)
            var renterLookup = SystemAPI.GetBufferLookup<Renter>(isReadOnly: true);
            var citizenLookup = SystemAPI.GetBufferLookup<HouseholdCitizen>(isReadOnly: true);
            var transformLookup = SystemAPI.GetComponentLookup<Transform>(isReadOnly: true);

            // 将符合条件的实体转换为 NativeList，分配器使用 World 的 UpdateAllocator
            // ToEntityListAsync 返回一个 JobHandle，需要将其与当前的依赖合并
            NativeList<Entity> entities = m_ResidentialPropertyQuery.ToEntityListAsync(World.UpdateAllocator.ToAllocator, out var queryHandle);

            // 实例化 Job
            var job = new PopulationToGridJob
            {
                m_Entities = entities,
                m_PopulationMap = m_Map, // 使用基类的 Map (NativeArray)
                m_Renters = renterLookup,
                m_HouseholdCitizens = citizenLookup,
                m_Transforms = transformLookup
            };

            // 组合依赖项：查询结果 + 当前系统的读写依赖
            var combinedDeps = JobUtils.CombineDependencies(queryHandle, m_WriteDependencies, m_ReadDependencies, Dependency);

            // 调度 Job
            Dependency = job.Schedule(combinedDeps);

            // 通知基类正在写入 Map，以便后续系统能正确处理依赖
            AddWriter(Dependency);
        }
        #endregion

        #region PopulationToGridJob
        [BurstCompile]
        private struct PopulationToGridJob : IJob
        {
            [ReadOnly] public NativeList<Entity> m_Entities;
            [ReadOnly] public BufferLookup<Renter> m_Renters;
            [ReadOnly] public ComponentLookup<Transform> m_Transforms;
            [ReadOnly] public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;
            // 输出的目标网格数据
            public NativeArray<TargetType> m_PopulationMap;

            public void Execute()
            {
                // 1. 清空人口网格数据
                int totalCells = kTextureSize * kTextureSize;
                for (int i = 0; i < totalCells; i++) m_PopulationMap[i] = default;

                // 2. 遍历所有住宅实体
                for (int i = 0; i < m_Entities.Length; i++)
                {
                    Entity buildingEntity = m_Entities[i];
                    int totalCitizensInBuilding = 0;

                    // 获取该建筑内的所有租户 (Renter Buffer)
                    if (m_Renters.TryGetBuffer(buildingEntity, out var rentersBuffer))
                    {
                        for (int r = 0; r < rentersBuffer.Length; r++)
                        {
                            Entity renterEntity = rentersBuffer[r].m_Renter;

                            // 检查租户是否有家庭成员 (HouseholdCitizen Buffer)
                            if (m_HouseholdCitizens.TryGetBuffer(renterEntity, out var citizensBuffer))
                            {
                                totalCitizensInBuilding += citizensBuffer.Length;
                            }
                        }
                    }

                    // 获取建筑位置并映射到网格坐标
                    float3 position = m_Transforms[buildingEntity].m_Position;
                    int2 cellCoords = CellMapSystemRe.GetCell(position, CellMapSystemRe.kMapSize, kTextureSize);

                    // 边界检查：确保坐标在纹理范围内
                    if (cellCoords.x >= 0 && cellCoords.y >= 0 && cellCoords.x < kTextureSize && cellCoords.y < kTextureSize)
                    {
                        int gridIndex = cellCoords.x + cellCoords.y * kTextureSize;

                        // 累加人口数据
                        TargetType currentCell = m_PopulationMap[gridIndex];
                        currentCell.m_Population += totalCitizensInBuilding;
                        m_PopulationMap[gridIndex] = currentCell;
                    }
                }
            }
        }
        #endregion

        #region 序列化自适应
        // ==============================================================================
        // 序列化修复 (泛型化)
        // ==============================================================================
        // 重写 Serialize 以处理大数据 (使用 TempJob)
        public new JobHandle Serialize<TWriter>(EntityWriterData writerData, JobHandle inputDeps) where TWriter : struct, IWriter
        {
            // 获取 Stride (数据步长)
            int stride = 0;
            if ((object)default(TargetType) is IStrideSerializable strideSerializable)
            {
                stride = strideSerializable.GetStride(writerData.GetWriter<TWriter>().context);
            }

            // 调度自定义的序列化 Job
            JobHandle jobHandle = new SerializeJobMod<TWriter>
            {
                m_Stride = stride,
                m_Map = this.m_Map, // 使用基类的 Map
                m_WriterData = writerData
            }.Schedule(JobHandle.CombineDependencies(inputDeps, m_WriteDependencies));

            m_ReadDependencies = JobHandle.CombineDependencies(m_ReadDependencies, jobHandle);
            return jobHandle;
        }

        // 重写 Deserialize (无需迁移旧存档，Job会重新计算)
        public override JobHandle Deserialize<TReader>(EntityReaderData readerData, JobHandle inputDeps)
        {
            int stride = 0;
            if ((object)default(TargetType) is IStrideSerializable strideSerializable)
            {
                stride = strideSerializable.GetStride(readerData.GetReader<TReader>().context);
            }

            // 简化版 Job：如果不匹配直接丢弃
            DeserializeJobResetMismatch<TReader> jobData = new()
            {
                m_Stride = stride,
                m_Map = this.m_Map,
                m_ReaderData = readerData
            };
            m_WriteDependencies = jobData.Schedule(JobHandle.CombineDependencies(inputDeps, m_ReadDependencies, m_WriteDependencies));
            return m_WriteDependencies;
        }

        [BurstCompile]
        private struct SerializeJobMod<TWriter> : IJob where TWriter : struct, IWriter
        {
            [ReadOnly] public int m_Stride;
            [ReadOnly] public NativeArray<TargetType> m_Map;
            public EntityWriterData m_WriterData;

            public void Execute()
            {
                TWriter writer = this.m_WriterData.GetWriter<TWriter>();
                if (m_Stride != 0 && m_Map.Length != 0)
                {
                    // 必须用 TempJob 防止 1024+ 尺寸导致内存溢出
                    NativeList<byte> buffer = new NativeList<byte>(m_Map.Length * 2, Allocator.TempJob);
                    try
                    {
                        m_WriterData.GetWriter<TWriter>(buffer).Write(m_Map);
                        writer.Write(-m_Map.Length);
                        writer.Write(buffer.Length);
                        writer.Write(buffer.AsArray(), m_Stride);
                    }
                    finally { buffer.Dispose(); }
                }
                else
                {
                    writer.Write(m_Map.Length);
                    writer.Write(m_Map);
                }
            }
        }

        [BurstCompile]
        private struct DeserializeJobResetMismatch<TReader> : IJob where TReader : struct, IReader
        {
            [ReadOnly] public int m_Stride;
            public NativeArray<TargetType> m_Map;
            public EntityReaderData m_ReaderData;

            public void Execute()
            {
                TReader reader = m_ReaderData.GetReader<TReader>();
                if (!(reader.context.version > Game.Version.stormWater)) return;

                // 默认为 0 
                // m_Map 已经在 SetDefaults 中被清零了，如果这里不写入，就是重置状态

                if (reader.context.version > Game.Version.cellMapLengths)
                {
                    reader.Read(out int storedCount);

                    // 1. 判断是否匹配
                    bool sizeMatches = (math.abs(storedCount) == m_Map.Length);

                    // 2. 如果是原始数据 (Length > 0)
                    if (storedCount > 0)
                    {
                        if (sizeMatches)
                        {
                            reader.Read(m_Map);
                        }
                        else
                        {
                            // 尺寸不匹配：读取到临时数组并丢弃 (必须读取以推进流位置)
                            var dummy = new NativeArray<TargetType>(storedCount, Allocator.TempJob);
                            reader.Read(dummy);
                            dummy.Dispose();
                            // m_Map 保持为 0
                        }
                    }
                    // 3. 如果是压缩数据 (Length < 0)
                    else if (storedCount < 0)
                    {
                        int actualCount = -storedCount;
                        reader.Read(out int byteLength);

                        // 必须读出来以清空流
                        NativeArray<byte> compressedBuffer = new NativeArray<byte>(byteLength, Allocator.TempJob);
                        try
                        {
                            reader.Read(compressedBuffer, m_Stride);

                            if (actualCount == m_Map.Length)
                            {
                                // 尺寸匹配：正常解压到 Map
                                NativeReference<int> pos = new NativeReference<int>(0, Allocator.Temp);
                                m_ReaderData.GetReader<TReader>(compressedBuffer, pos).Read(m_Map);
                                pos.Dispose();
                            }
                            // 否则：只读取字节流，不解压，m_Map 保持为 0
                        }
                        finally
                        {
                            compressedBuffer.Dispose();
                        }
                    }
                }
                else
                {
                    // 旧版本数据，通常无法匹配尺寸，直接忽略或尝试读取
                    if (m_Map.Length == kTextureSize * kTextureSize) // 运气好匹配了
                        reader.Read(m_Map);
                }
            }
        }

        #endregion

        #region GetData修正
        // 重写/重定向的 GetData
        public new TargetCellMapData GetData(bool readOnly, out JobHandle dependencies)
        {
            // 获取依赖
            dependencies = (readOnly ? m_WriteDependencies : JobHandle.CombineDependencies(m_ReadDependencies, m_WriteDependencies));

            float2 mapSize = new float2(CellMapSystemRe.kMapSize, CellMapSystemRe.kMapSize);

            return new TargetCellMapData
            {
                m_Buffer = m_Map,
                m_CellSize = mapSize / (float2)m_TextureSize,
                m_TextureSize = m_TextureSize
            };
        }
        #endregion

        #region HarmonyPatch
        // ==============================================================================
        // Harmony 补丁 (全自动适配)
        // ==============================================================================
        [HarmonyPatch]
        public static class Patches
        {
            // 辅助判断：只拦截对应的原版系统实例
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool ShouldRedirect(object instance)
            {
                return Instance != null && instance.GetType() == typeof(TargetSystem);
            }

            // Patch: GetMap
            [HarmonyPatch(typeof(BaseCellMapSystem), "GetMap")]
            [HarmonyPrefix]
            public static bool GetMapPrefix(BaseCellMapSystem __instance,
                                      ref NativeArray<TargetType> __result,
                                      bool readOnly,
                                      ref JobHandle dependencies)
            {
                if (ShouldRedirect(__instance))
                {
                    __result = Instance.GetMap(readOnly, out var deps);
                    dependencies = deps;
                    return false;
                }
                return true;
            }

            // Patch: GetData
            [HarmonyPatch(typeof(BaseCellMapSystem), "GetData")]
            [HarmonyPrefix]
            public static bool GetDataPrefix(BaseCellMapSystem __instance,
                                      ref TargetCellMapData __result,
                                      bool readOnly,
                                      ref JobHandle dependencies)
            {
                if (ShouldRedirect(__instance))
                {
                    __result = Instance.GetData(readOnly, out var deps);
                    dependencies = deps;
                    return false;
                }
                return true;
            }

            // Patch: AddReader
            [HarmonyPatch(typeof(BaseCellMapSystem), "AddReader")]
            [HarmonyPrefix]
            public static bool AddReaderPrefix(BaseCellMapSystem __instance, JobHandle jobHandle)
            {
                if (ShouldRedirect(__instance))
                {
                    Instance.AddReader(jobHandle);
                    return false;
                }
                return true;
            }

            // Patch: AddWriter
            [HarmonyPatch(typeof(BaseCellMapSystem), "AddWriter")]
            [HarmonyPrefix]
            public static bool AddWriterPrefix(BaseCellMapSystem __instance, JobHandle jobHandle)
            {
                if (ShouldRedirect(__instance))
                {
                    Instance.AddWriter(jobHandle);
                    return false;
                }
                return true;
            }
        }
        #endregion

    } // mod class

} // mod namespace

