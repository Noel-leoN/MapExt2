using Game.Buildings;
using Game.Common;
using Game.Economy;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Zones;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static MapExtPDX.MapExt.ReBurstSystemModeA.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    [BurstCompile]
    public struct EvaluateSpawnAreas : IJobChunk
    {
        [ReadOnly]
        public NativeList<ArchetypeChunk> m_BuildingChunks;

        [ReadOnly]
        public ZonePrefabs m_ZonePrefabs;

        [ReadOnly]
        public ZonePreferenceData m_Preferences;

        [ReadOnly]
        public int m_SpawnResidential;

        [ReadOnly]
        public int m_SpawnCommercial;

        [ReadOnly]
        public int m_SpawnIndustrial;

        [ReadOnly]
        public int m_SpawnStorage;

        [ReadOnly]
        public int m_MinDemand;

        public int3 m_ResidentialDemands;

        [ReadOnly]
        public NativeArray<int> m_CommercialBuildingDemands;

        [ReadOnly]
        public NativeArray<int> m_IndustrialDemands;

        [ReadOnly]
        public NativeArray<int> m_StorageDemands;

        [ReadOnly]
        public RandomSeed m_RandomSeed;

        [ReadOnly]
        public EntityTypeHandle m_EntityType;

        [ReadOnly]
        public ComponentTypeHandle<Block> m_BlockType;

        [ReadOnly]
        public ComponentTypeHandle<Owner> m_OwnerType;

        [ReadOnly]
        public ComponentTypeHandle<CurvePosition> m_CurvePositionType;

        [ReadOnly]
        public BufferTypeHandle<VacantLot> m_VacantLotType;

        [ReadOnly]
        public ComponentTypeHandle<BuildingData> m_BuildingDataType;

        [ReadOnly]
        public ComponentTypeHandle<SpawnableBuildingData> m_SpawnableBuildingType;

        [ReadOnly]
        public ComponentTypeHandle<BuildingPropertyData> m_BuildingPropertyType;

        [ReadOnly]
        public ComponentTypeHandle<ObjectGeometryData> m_ObjectGeometryType;

        [ReadOnly]
        public SharedComponentTypeHandle<BuildingSpawnGroupData> m_BuildingSpawnGroupType;

        [ReadOnly]
        public ComponentTypeHandle<WarehouseData> m_WarehouseType;

        [ReadOnly]
        public ComponentLookup<ZoneData> m_ZoneData;

        [ReadOnly]
        public ComponentLookup<ZonePropertiesData> m_ZonePropertiesDatas;

        [ReadOnly]
        public BufferLookup<ResourceAvailability> m_Availabilities;

        [ReadOnly]
        public NativeList<IndustrialProcessData> m_Processes;

        [ReadOnly]
        public BufferLookup<ProcessEstimate> m_ProcessEstimates;

        [ReadOnly]
        public ComponentLookup<LandValue> m_LandValues;

        [ReadOnly]
        public ComponentLookup<Block> m_BlockData;

        [ReadOnly]
        public ComponentLookup<ResourceData> m_ResourceDatas;

        [ReadOnly]
        public ResourcePrefabs m_ResourcePrefabs;

        [ReadOnly]
        public NativeArray<GroundPollution> m_PollutionMap;

        public NativeQueue<ZoneSpawnSystem.SpawnLocation>.ParallelWriter m_Residential;

        public NativeQueue<ZoneSpawnSystem.SpawnLocation>.ParallelWriter m_Commercial;

        public NativeQueue<ZoneSpawnSystem.SpawnLocation>.ParallelWriter m_Industrial;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Random random = m_RandomSeed.GetRandom(unfilteredChunkIndex);
            ZoneSpawnSystem.SpawnLocation bestLocation = default;
            ZoneSpawnSystem.SpawnLocation bestLocation2 = default;
            ZoneSpawnSystem.SpawnLocation bestLocation3 = default;
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
            BufferAccessor<VacantLot> bufferAccessor = chunk.GetBufferAccessor(ref m_VacantLotType);
            if (bufferAccessor.Length != 0)
            {
                NativeArray<Owner> nativeArray2 = chunk.GetNativeArray(ref m_OwnerType);
                NativeArray<CurvePosition> nativeArray3 = chunk.GetNativeArray(ref m_CurvePositionType);
                NativeArray<Block> nativeArray4 = chunk.GetNativeArray(ref m_BlockType);
                for (int i = 0; i < nativeArray.Length; i++)
                {
                    Entity entity = nativeArray[i];
                    DynamicBuffer<VacantLot> dynamicBuffer = bufferAccessor[i];
                    Owner owner = nativeArray2[i];
                    CurvePosition curvePosition = nativeArray3[i];
                    Block block = nativeArray4[i];
                    for (int j = 0; j < dynamicBuffer.Length; j++)
                    {
                        VacantLot lot = dynamicBuffer[j];
                        if (!m_ZonePropertiesDatas.HasComponent(m_ZonePrefabs[lot.m_Type]))
                        {
                            continue;
                        }
                        ZoneData zoneData = m_ZoneData[m_ZonePrefabs[lot.m_Type]];
                        ZonePropertiesData zonePropertiesData = m_ZonePropertiesDatas[m_ZonePrefabs[lot.m_Type]];
                        DynamicBuffer<ProcessEstimate> estimates = m_ProcessEstimates[m_ZonePrefabs[lot.m_Type]];
                        switch (zoneData.m_AreaType)
                        {
                            case AreaType.Residential:
                                if (m_SpawnResidential != 0)
                                {
                                    float curvePos2 = CalculateCurvePos(curvePosition, lot, block);
                                    TryAddLot(ref bestLocation, ref random, owner.m_Owner, curvePos2, entity, lot.m_Area, lot.m_Flags, lot.m_Height, zoneData, zonePropertiesData, estimates, m_Processes);
                                }
                                break;
                            case AreaType.Commercial:
                                if (m_SpawnCommercial != 0)
                                {
                                    float curvePos3 = CalculateCurvePos(curvePosition, lot, block);
                                    TryAddLot(ref bestLocation2, ref random, owner.m_Owner, curvePos3, entity, lot.m_Area, lot.m_Flags, lot.m_Height, zoneData, zonePropertiesData, estimates, m_Processes);
                                }
                                break;
                            case AreaType.Industrial:
                                if (m_SpawnIndustrial != 0 || m_SpawnStorage != 0)
                                {
                                    float curvePos = CalculateCurvePos(curvePosition, lot, block);
                                    TryAddLot(ref bestLocation3, ref random, owner.m_Owner, curvePos, entity, lot.m_Area, lot.m_Flags, lot.m_Height, zoneData, zonePropertiesData, estimates, m_Processes, m_SpawnIndustrial != 0, m_SpawnStorage != 0);
                                }
                                break;
                        }
                    }
                }
            }
            if (bestLocation.m_Priority != 0f)
            {
                m_Residential.Enqueue(bestLocation);
            }
            if (bestLocation2.m_Priority != 0f)
            {
                m_Commercial.Enqueue(bestLocation2);
            }
            if (bestLocation3.m_Priority != 0f)
            {
                m_Industrial.Enqueue(bestLocation3);
            }
        }

        private float CalculateCurvePos(CurvePosition curvePosition, VacantLot lot, Block block)
        {
            float s = math.saturate((lot.m_Area.x + lot.m_Area.y) * 0.5f / block.m_Size.x);
            return math.lerp(curvePosition.m_CurvePosition.x, curvePosition.m_CurvePosition.y, s);
        }

        private void TryAddLot(ref ZoneSpawnSystem.SpawnLocation bestLocation, ref Random random, Entity road, float curvePos, Entity entity, int4 area, LotFlags flags, int height, ZoneData zoneData, ZonePropertiesData zonePropertiesData, DynamicBuffer<ProcessEstimate> estimates, NativeList<IndustrialProcessData> processes, bool normal = true, bool storage = false)
        {
            if (!m_Availabilities.HasBuffer(road))
            {
                return;
            }
            if ((zoneData.m_ZoneFlags & ZoneFlags.SupportLeftCorner) == 0)
            {
                flags &= ~LotFlags.CornerLeft;
            }
            if ((zoneData.m_ZoneFlags & ZoneFlags.SupportRightCorner) == 0)
            {
                flags &= ~LotFlags.CornerRight;
            }
            ZoneSpawnSystem.SpawnLocation location = default;
            location.m_Entity = entity;
            location.m_LotArea = area;
            location.m_ZoneType = zoneData.m_ZoneType;
            location.m_AreaType = zoneData.m_AreaType;
            location.m_LotFlags = flags;
            bool office = zoneData.m_AreaType == AreaType.Industrial && estimates.Length == 0;
            DynamicBuffer<ResourceAvailability> availabilities = m_Availabilities[road];
            if (m_BlockData.HasComponent(location.m_Entity))
            {
                float3 position = ZoneUtils.GetPosition(m_BlockData[location.m_Entity], location.m_LotArea.xz, location.m_LotArea.yw);
                bool extractor = false;
                float pollution = GroundPollutionSystemGetPollution(position, m_PollutionMap).m_Pollution;
                float landValue = m_LandValues[road].m_LandValue;
                float maxHeight = height - position.y;
                if (SelectBuilding(ref location, ref random, availabilities, zoneData, zonePropertiesData, curvePos, pollution, landValue, maxHeight, estimates, processes, normal, storage, extractor, office) && location.m_Priority > bestLocation.m_Priority)
                {
                    bestLocation = location;
                }
            }
        }

        private bool SelectBuilding(ref ZoneSpawnSystem.SpawnLocation location, ref Random random, DynamicBuffer<ResourceAvailability> availabilities, ZoneData zoneData, ZonePropertiesData zonePropertiesData, float curvePos, float pollution, float landValue, float maxHeight, DynamicBuffer<ProcessEstimate> estimates, NativeList<IndustrialProcessData> processes, bool normal = true, bool storage = false, bool extractor = false, bool office = false)
        {
            int2 @int = location.m_LotArea.yw - location.m_LotArea.xz;
            BuildingData buildingData = default;
            bool2 @bool = new bool2((location.m_LotFlags & LotFlags.CornerLeft) != 0, (location.m_LotFlags & LotFlags.CornerRight) != 0);
            bool flag = (zoneData.m_ZoneFlags & ZoneFlags.SupportNarrow) == 0;
            for (int i = 0; i < m_BuildingChunks.Length; i++)
            {
                ArchetypeChunk archetypeChunk = m_BuildingChunks[i];
                if (!archetypeChunk.GetSharedComponent(m_BuildingSpawnGroupType).m_ZoneType.Equals(location.m_ZoneType))
                {
                    continue;
                }
                bool flag2 = archetypeChunk.Has(ref m_WarehouseType);
                if (flag2 && !storage || !flag2 && !normal)
                {
                    continue;
                }
                NativeArray<Entity> nativeArray = archetypeChunk.GetNativeArray(m_EntityType);
                NativeArray<BuildingData> nativeArray2 = archetypeChunk.GetNativeArray(ref m_BuildingDataType);
                NativeArray<SpawnableBuildingData> nativeArray3 = archetypeChunk.GetNativeArray(ref m_SpawnableBuildingType);
                NativeArray<BuildingPropertyData> nativeArray4 = archetypeChunk.GetNativeArray(ref m_BuildingPropertyType);
                NativeArray<ObjectGeometryData> nativeArray5 = archetypeChunk.GetNativeArray(ref m_ObjectGeometryType);
                for (int j = 0; j < nativeArray3.Length; j++)
                {
                    if (nativeArray3[j].m_Level != 1)
                    {
                        continue;
                    }
                    BuildingData buildingData2 = nativeArray2[j];
                    int2 lotSize = buildingData2.m_LotSize;
                    bool2 bool2 = new bool2((buildingData2.m_Flags & Game.Prefabs.BuildingFlags.LeftAccess) != 0, (buildingData2.m_Flags & Game.Prefabs.BuildingFlags.RightAccess) != 0);
                    float y = nativeArray5[j].m_Size.y;
                    if (!math.all(lotSize <= @int) || !(y <= maxHeight))
                    {
                        continue;
                    }
                    BuildingPropertyData buildingPropertyData = nativeArray4[j];
                    ZoneDensity zoneDensity = PropertyUtils.GetZoneDensity(zoneData, zonePropertiesData);
                    int num = EvaluateDemandAndAvailability(buildingPropertyData, zoneData.m_AreaType, zoneDensity, flag2);
                    if (!(num >= m_MinDemand || extractor))
                    {
                        continue;
                    }
                    int2 int2 = math.select(@int - lotSize, 0, lotSize == @int - 1);
                    float num2 = lotSize.x * lotSize.y * random.NextFloat(1f, 1.05f);
                    num2 += int2.x * lotSize.y * random.NextFloat(0.95f, 1f);
                    num2 += @int.x * int2.y * random.NextFloat(0.55f, 0.6f);
                    num2 /= @int.x * @int.y;
                    num2 *= num + 1;
                    num2 *= math.csum(math.select(0.01f, 0.5f, @bool == bool2));
                    if (!extractor)
                    {
                        float num3 = landValue;
                        float num4;
                        if (location.m_AreaType == AreaType.Residential)
                        {
                            num4 = buildingPropertyData.m_ResidentialProperties == 1 ? 2f : buildingPropertyData.CountProperties();
                            lotSize.x = math.select(lotSize.x, @int.x, lotSize.x == @int.x - 1 && flag);
                            num3 *= lotSize.x * @int.y;
                        }
                        else
                        {
                            num4 = buildingPropertyData.m_SpaceMultiplier;
                        }
                        float score = ZoneEvaluationUtils.GetScore(location.m_AreaType, office, availabilities, curvePos, ref m_Preferences, flag2, flag2 ? m_StorageDemands : m_IndustrialDemands, buildingPropertyData, pollution, num3 / num4, estimates, processes, m_ResourcePrefabs, ref m_ResourceDatas);
                        score = math.select(score, math.max(0f, score) + 1f, m_MinDemand == 0);
                        num2 *= score;
                    }
                    if (num2 > location.m_Priority)
                    {
                        location.m_Building = nativeArray[j];
                        buildingData = buildingData2;
                        location.m_Priority = num2;
                    }
                }
            }
            if (location.m_Building != Entity.Null)
            {
                if ((buildingData.m_Flags & Game.Prefabs.BuildingFlags.LeftAccess) == 0 && ((buildingData.m_Flags & Game.Prefabs.BuildingFlags.RightAccess) != 0 || random.NextBool()))
                {
                    location.m_LotArea.x = location.m_LotArea.y - buildingData.m_LotSize.x;
                    location.m_LotArea.w = location.m_LotArea.z + buildingData.m_LotSize.y;
                }
                else
                {
                    location.m_LotArea.yw = location.m_LotArea.xz + buildingData.m_LotSize;
                }
                return true;
            }
            return false;
        }

        private int EvaluateDemandAndAvailability(BuildingPropertyData buildingPropertyData, AreaType areaType, ZoneDensity zoneDensity, bool storage = false)
        {
            switch (areaType)
            {
                case AreaType.Residential:
                    return zoneDensity switch
                    {
                        ZoneDensity.Low => m_ResidentialDemands.x,
                        ZoneDensity.Medium => m_ResidentialDemands.y,
                        _ => m_ResidentialDemands.z,
                    };
                case AreaType.Commercial:
                    {
                        int num2 = 0;
                        ResourceIterator iterator2 = ResourceIterator.GetIterator();
                        while (iterator2.Next())
                        {
                            if ((buildingPropertyData.m_AllowedSold & iterator2.resource) != Resource.NoResource)
                            {
                                num2 += m_CommercialBuildingDemands[EconomyUtils.GetResourceIndex(iterator2.resource)];
                            }
                        }
                        return num2;
                    }
                case AreaType.Industrial:
                    {
                        int num = 0;
                        ResourceIterator iterator = ResourceIterator.GetIterator();
                        while (iterator.Next())
                        {
                            if (storage)
                            {
                                if ((buildingPropertyData.m_AllowedStored & iterator.resource) != Resource.NoResource)
                                {
                                    num += m_StorageDemands[EconomyUtils.GetResourceIndex(iterator.resource)];
                                }
                            }
                            else if ((buildingPropertyData.m_AllowedManufactured & iterator.resource) != Resource.NoResource)
                            {
                                num += m_IndustrialDemands[EconomyUtils.GetResourceIndex(iterator.resource)];
                            }
                        }
                        return num;
                    }
                default:
                    return 0;
            }
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }

}