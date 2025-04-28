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

/*
		public struct SpawnLocation
		{
			public Entity m_Entity;

			public Entity m_Building;

			public int4 m_LotArea;

			public float m_Priority;

			public ZoneType m_ZoneType;

			public Game.Zones.AreaType m_AreaType;

			public LotFlags m_LotFlags;
		}
*/

namespace MapExtPDX
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

        public NativeQueue<Game.Simulation.ZoneSpawnSystem.SpawnLocation>.ParallelWriter m_Residential;

        public NativeQueue<Game.Simulation.ZoneSpawnSystem.SpawnLocation>.ParallelWriter m_Commercial;

        public NativeQueue<Game.Simulation.ZoneSpawnSystem.SpawnLocation>.ParallelWriter m_Industrial;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
            Game.Simulation.ZoneSpawnSystem.SpawnLocation bestLocation = default(Game.Simulation.ZoneSpawnSystem.SpawnLocation);
            Game.Simulation.ZoneSpawnSystem.SpawnLocation bestLocation2 = default(Game.Simulation.ZoneSpawnSystem.SpawnLocation);
            Game.Simulation.ZoneSpawnSystem.SpawnLocation bestLocation3 = default(Game.Simulation.ZoneSpawnSystem.SpawnLocation);
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
            BufferAccessor<VacantLot> bufferAccessor = chunk.GetBufferAccessor(ref this.m_VacantLotType);
            if (bufferAccessor.Length != 0)
            {
                NativeArray<Owner> nativeArray2 = chunk.GetNativeArray(ref this.m_OwnerType);
                NativeArray<CurvePosition> nativeArray3 = chunk.GetNativeArray(ref this.m_CurvePositionType);
                NativeArray<Block> nativeArray4 = chunk.GetNativeArray(ref this.m_BlockType);
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
                        if (!this.m_ZonePropertiesDatas.HasComponent(this.m_ZonePrefabs[lot.m_Type]))
                        {
                            continue;
                        }
                        ZoneData zoneData = this.m_ZoneData[this.m_ZonePrefabs[lot.m_Type]];
                        ZonePropertiesData zonePropertiesData = this.m_ZonePropertiesDatas[this.m_ZonePrefabs[lot.m_Type]];
                        DynamicBuffer<ProcessEstimate> estimates = this.m_ProcessEstimates[this.m_ZonePrefabs[lot.m_Type]];
                        switch (zoneData.m_AreaType)
                        {
                            case Game.Zones.AreaType.Residential:
                                if (this.m_SpawnResidential != 0)
                                {
                                    float curvePos2 = this.CalculateCurvePos(curvePosition, lot, block);
                                    this.TryAddLot(ref bestLocation, ref random, owner.m_Owner, curvePos2, entity, lot.m_Area, lot.m_Flags, lot.m_Height, zoneData, zonePropertiesData, estimates, this.m_Processes);
                                }
                                break;
                            case Game.Zones.AreaType.Commercial:
                                if (this.m_SpawnCommercial != 0)
                                {
                                    float curvePos3 = this.CalculateCurvePos(curvePosition, lot, block);
                                    this.TryAddLot(ref bestLocation2, ref random, owner.m_Owner, curvePos3, entity, lot.m_Area, lot.m_Flags, lot.m_Height, zoneData, zonePropertiesData, estimates, this.m_Processes);
                                }
                                break;
                            case Game.Zones.AreaType.Industrial:
                                if (this.m_SpawnIndustrial != 0 || this.m_SpawnStorage != 0)
                                {
                                    float curvePos = this.CalculateCurvePos(curvePosition, lot, block);
                                    this.TryAddLot(ref bestLocation3, ref random, owner.m_Owner, curvePos, entity, lot.m_Area, lot.m_Flags, lot.m_Height, zoneData, zonePropertiesData, estimates, this.m_Processes, this.m_SpawnIndustrial != 0, this.m_SpawnStorage != 0);
                                }
                                break;
                        }
                    }
                }
            }
            if (bestLocation.m_Priority != 0f)
            {
                this.m_Residential.Enqueue(bestLocation);
            }
            if (bestLocation2.m_Priority != 0f)
            {
                this.m_Commercial.Enqueue(bestLocation2);
            }
            if (bestLocation3.m_Priority != 0f)
            {
                this.m_Industrial.Enqueue(bestLocation3);
            }
        }

        private float CalculateCurvePos(CurvePosition curvePosition, VacantLot lot, Block block)
        {
            float s = math.saturate((float)(lot.m_Area.x + lot.m_Area.y) * 0.5f / (float)block.m_Size.x);
            return math.lerp(curvePosition.m_CurvePosition.x, curvePosition.m_CurvePosition.y, s);
        }

        private void TryAddLot(ref Game.Simulation.ZoneSpawnSystem.SpawnLocation bestLocation, ref Random random, Entity road, float curvePos, Entity entity, int4 area, LotFlags flags, int height, ZoneData zoneData, ZonePropertiesData zonePropertiesData, DynamicBuffer<ProcessEstimate> estimates, NativeList<IndustrialProcessData> processes, bool normal = true, bool storage = false)
        {
            if (!this.m_Availabilities.HasBuffer(road))
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
            Game.Simulation.ZoneSpawnSystem.SpawnLocation location = default(Game.Simulation.ZoneSpawnSystem.SpawnLocation);
            location.m_Entity = entity;
            location.m_LotArea = area;
            location.m_ZoneType = zoneData.m_ZoneType;
            location.m_AreaType = zoneData.m_AreaType;
            location.m_LotFlags = flags;
            bool office = zoneData.m_AreaType == Game.Zones.AreaType.Industrial && estimates.Length == 0;
            DynamicBuffer<ResourceAvailability> availabilities = this.m_Availabilities[road];
            if (this.m_BlockData.HasComponent(location.m_Entity))
            {
                float3 position = ZoneUtils.GetPosition(this.m_BlockData[location.m_Entity], location.m_LotArea.xz, location.m_LotArea.yw);
                bool extractor = false;
                float pollution = GroundPollutionSystem.GetPollution(position, this.m_PollutionMap).m_Pollution;
                float landValue = this.m_LandValues[road].m_LandValue;
                float maxHeight = (float)height - position.y;
                if (this.SelectBuilding(ref location, ref random, availabilities, zoneData, zonePropertiesData, curvePos, pollution, landValue, maxHeight, estimates, processes, normal, storage, extractor, office) && location.m_Priority > bestLocation.m_Priority)
                {
                    bestLocation = location;
                }
            }
        }

        private bool SelectBuilding(ref Game.Simulation.ZoneSpawnSystem.SpawnLocation location, ref Random random, DynamicBuffer<ResourceAvailability> availabilities, ZoneData zoneData, ZonePropertiesData zonePropertiesData, float curvePos, float pollution, float landValue, float maxHeight, DynamicBuffer<ProcessEstimate> estimates, NativeList<IndustrialProcessData> processes, bool normal = true, bool storage = false, bool extractor = false, bool office = false)
        {
            int2 @int = location.m_LotArea.yw - location.m_LotArea.xz;
            BuildingData buildingData = default(BuildingData);
            bool2 @bool = new bool2((location.m_LotFlags & LotFlags.CornerLeft) != 0, (location.m_LotFlags & LotFlags.CornerRight) != 0);
            bool flag = (zoneData.m_ZoneFlags & ZoneFlags.SupportNarrow) == 0;
            for (int i = 0; i < this.m_BuildingChunks.Length; i++)
            {
                ArchetypeChunk archetypeChunk = this.m_BuildingChunks[i];
                if (!archetypeChunk.GetSharedComponent(this.m_BuildingSpawnGroupType).m_ZoneType.Equals(location.m_ZoneType))
                {
                    continue;
                }
                bool flag2 = archetypeChunk.Has(ref this.m_WarehouseType);
                if ((flag2 && !storage) || (!flag2 && !normal))
                {
                    continue;
                }
                NativeArray<Entity> nativeArray = archetypeChunk.GetNativeArray(this.m_EntityType);
                NativeArray<BuildingData> nativeArray2 = archetypeChunk.GetNativeArray(ref this.m_BuildingDataType);
                NativeArray<SpawnableBuildingData> nativeArray3 = archetypeChunk.GetNativeArray(ref this.m_SpawnableBuildingType);
                NativeArray<BuildingPropertyData> nativeArray4 = archetypeChunk.GetNativeArray(ref this.m_BuildingPropertyType);
                NativeArray<ObjectGeometryData> nativeArray5 = archetypeChunk.GetNativeArray(ref this.m_ObjectGeometryType);
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
                    int num = this.EvaluateDemandAndAvailability(buildingPropertyData, zoneData.m_AreaType, zoneDensity, flag2);
                    if (!(num >= this.m_MinDemand || extractor))
                    {
                        continue;
                    }
                    int2 int2 = math.select(@int - lotSize, 0, lotSize == @int - 1);
                    float num2 = (float)(lotSize.x * lotSize.y) * random.NextFloat(1f, 1.05f);
                    num2 += (float)(int2.x * lotSize.y) * random.NextFloat(0.95f, 1f);
                    num2 += (float)(@int.x * int2.y) * random.NextFloat(0.55f, 0.6f);
                    num2 /= (float)(@int.x * @int.y);
                    num2 *= (float)(num + 1);
                    num2 *= math.csum(math.select(0.01f, 0.5f, @bool == bool2));
                    if (!extractor)
                    {
                        float num3 = landValue;
                        float num4;
                        if (location.m_AreaType == Game.Zones.AreaType.Residential)
                        {
                            num4 = ((buildingPropertyData.m_ResidentialProperties == 1) ? 2f : ((float)buildingPropertyData.CountProperties()));
                            lotSize.x = math.select(lotSize.x, @int.x, lotSize.x == @int.x - 1 && flag);
                            num3 *= (float)(lotSize.x * @int.y);
                        }
                        else
                        {
                            num4 = buildingPropertyData.m_SpaceMultiplier;
                        }
                        float score = ZoneEvaluationUtils.GetScore(location.m_AreaType, office, availabilities, curvePos, ref this.m_Preferences, flag2, flag2 ? this.m_StorageDemands : this.m_IndustrialDemands, buildingPropertyData, pollution, num3 / num4, estimates, processes, this.m_ResourcePrefabs, ref this.m_ResourceDatas);
                        score = math.select(score, math.max(0f, score) + 1f, this.m_MinDemand == 0);
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

        private int EvaluateDemandAndAvailability(BuildingPropertyData buildingPropertyData, Game.Zones.AreaType areaType, ZoneDensity zoneDensity, bool storage = false)
        {
            switch (areaType)
            {
                case Game.Zones.AreaType.Residential:
                    return zoneDensity switch
                    {
                        ZoneDensity.Low => this.m_ResidentialDemands.x,
                        ZoneDensity.Medium => this.m_ResidentialDemands.y,
                        _ => this.m_ResidentialDemands.z,
                    };
                case Game.Zones.AreaType.Commercial:
                    {
                        int num2 = 0;
                        ResourceIterator iterator2 = ResourceIterator.GetIterator();
                        while (iterator2.Next())
                        {
                            if ((buildingPropertyData.m_AllowedSold & iterator2.resource) != Resource.NoResource)
                            {
                                num2 += this.m_CommercialBuildingDemands[EconomyUtils.GetResourceIndex(iterator2.resource)];
                            }
                        }
                        return num2;
                    }
                case Game.Zones.AreaType.Industrial:
                    {
                        int num = 0;
                        ResourceIterator iterator = ResourceIterator.GetIterator();
                        while (iterator.Next())
                        {
                            if (storage)
                            {
                                if ((buildingPropertyData.m_AllowedStored & iterator.resource) != Resource.NoResource)
                                {
                                    num += this.m_StorageDemands[EconomyUtils.GetResourceIndex(iterator.resource)];
                                }
                            }
                            else if ((buildingPropertyData.m_AllowedManufactured & iterator.resource) != Resource.NoResource)
                            {
                                num += this.m_IndustrialDemands[EconomyUtils.GetResourceIndex(iterator.resource)];
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
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }

}

/*
		[Preserve]
		protected override void OnUpdate()
		{
			RandomSeed.Next().GetRandom(0);
			bool flag = this.debugFastSpawn || (this.m_ResidentialDemandSystem.buildingDemand.x + this.m_ResidentialDemandSystem.buildingDemand.y + this.m_ResidentialDemandSystem.buildingDemand.z) / 3 > 0;
			bool flag2 = this.debugFastSpawn || this.m_CommercialDemandSystem.buildingDemand > 0;
			bool flag3 = this.debugFastSpawn || (this.m_IndustrialDemandSystem.industrialBuildingDemand + this.m_IndustrialDemandSystem.officeBuildingDemand) / 2 > 0;
			bool flag4 = this.debugFastSpawn || this.m_IndustrialDemandSystem.storageBuildingDemand > 0;
			NativeQueue<SpawnLocation> residential = new NativeQueue<SpawnLocation>(Allocator.TempJob);
			NativeQueue<SpawnLocation> commercial = new NativeQueue<SpawnLocation>(Allocator.TempJob);
			NativeQueue<SpawnLocation> industrial = new NativeQueue<SpawnLocation>(Allocator.TempJob);
			this.__TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Zones_ProcessEstimate_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Zones_Block_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_LandValue_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_ResourceAvailability_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_ZonePropertiesData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_ZoneData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_WarehouseData_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_BuildingSpawnGroupData_SharedComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_BuildingPropertyData_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Zones_VacantLot_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Zones_CurvePosition_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Common_Owner_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Zones_Block_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Unity_Entities_Entity_TypeHandle.Update(ref base.CheckedStateRef);
			EvaluateSpawnAreas evaluateSpawnAreas = default(EvaluateSpawnAreas);
			evaluateSpawnAreas.m_BuildingChunks = this.m_BuildingQuery.ToArchetypeChunkListAsync(base.World.UpdateAllocator.ToAllocator, out var outJobHandle);
			evaluateSpawnAreas.m_ZonePrefabs = this.m_ZoneSystem.GetPrefabs();
			evaluateSpawnAreas.m_Preferences = this.__query_1944910157_0.GetSingleton<ZonePreferenceData>();
			evaluateSpawnAreas.m_SpawnResidential = (flag ? 1 : 0);
			evaluateSpawnAreas.m_SpawnCommercial = (flag2 ? 1 : 0);
			evaluateSpawnAreas.m_SpawnIndustrial = (flag3 ? 1 : 0);
			evaluateSpawnAreas.m_SpawnStorage = (flag4 ? 1 : 0);
			evaluateSpawnAreas.m_MinDemand = ((!this.debugFastSpawn) ? 1 : 0);
			evaluateSpawnAreas.m_ResidentialDemands = this.m_ResidentialDemandSystem.buildingDemand;
			evaluateSpawnAreas.m_CommercialBuildingDemands = this.m_CommercialDemandSystem.GetBuildingDemands(out var deps);
			evaluateSpawnAreas.m_IndustrialDemands = this.m_IndustrialDemandSystem.GetBuildingDemands(out var deps2);
			evaluateSpawnAreas.m_StorageDemands = this.m_IndustrialDemandSystem.GetStorageBuildingDemands(out var deps3);
			evaluateSpawnAreas.m_RandomSeed = RandomSeed.Next();
			evaluateSpawnAreas.m_EntityType = this.__TypeHandle.__Unity_Entities_Entity_TypeHandle;
			evaluateSpawnAreas.m_BlockType = this.__TypeHandle.__Game_Zones_Block_RO_ComponentTypeHandle;
			evaluateSpawnAreas.m_OwnerType = this.__TypeHandle.__Game_Common_Owner_RO_ComponentTypeHandle;
			evaluateSpawnAreas.m_CurvePositionType = this.__TypeHandle.__Game_Zones_CurvePosition_RO_ComponentTypeHandle;
			evaluateSpawnAreas.m_VacantLotType = this.__TypeHandle.__Game_Zones_VacantLot_RO_BufferTypeHandle;
			evaluateSpawnAreas.m_BuildingDataType = this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentTypeHandle;
			evaluateSpawnAreas.m_SpawnableBuildingType = this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentTypeHandle;
			evaluateSpawnAreas.m_BuildingPropertyType = this.__TypeHandle.__Game_Prefabs_BuildingPropertyData_RO_ComponentTypeHandle;
			evaluateSpawnAreas.m_ObjectGeometryType = this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentTypeHandle;
			evaluateSpawnAreas.m_BuildingSpawnGroupType = this.__TypeHandle.__Game_Prefabs_BuildingSpawnGroupData_SharedComponentTypeHandle;
			evaluateSpawnAreas.m_WarehouseType = this.__TypeHandle.__Game_Prefabs_WarehouseData_RO_ComponentTypeHandle;
			evaluateSpawnAreas.m_ZoneData = this.__TypeHandle.__Game_Prefabs_ZoneData_RO_ComponentLookup;
			evaluateSpawnAreas.m_ZonePropertiesDatas = this.__TypeHandle.__Game_Prefabs_ZonePropertiesData_RO_ComponentLookup;
			evaluateSpawnAreas.m_Availabilities = this.__TypeHandle.__Game_Net_ResourceAvailability_RO_BufferLookup;
			evaluateSpawnAreas.m_LandValues = this.__TypeHandle.__Game_Net_LandValue_RO_ComponentLookup;
			evaluateSpawnAreas.m_BlockData = this.__TypeHandle.__Game_Zones_Block_RO_ComponentLookup;
			evaluateSpawnAreas.m_Processes = this.m_ProcessQuery.ToComponentDataListAsync<IndustrialProcessData>(base.World.UpdateAllocator.ToAllocator, out var outJobHandle2);
			evaluateSpawnAreas.m_ProcessEstimates = this.__TypeHandle.__Game_Zones_ProcessEstimate_RO_BufferLookup;
			evaluateSpawnAreas.m_ResourceDatas = this.__TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup;
			evaluateSpawnAreas.m_ResourcePrefabs = this.m_ResourceSystem.GetPrefabs();
			evaluateSpawnAreas.m_PollutionMap = this.m_PollutionSystem.GetMap(readOnly: true, out var dependencies);
			evaluateSpawnAreas.m_Residential = residential.AsParallelWriter();
			evaluateSpawnAreas.m_Commercial = commercial.AsParallelWriter();
			evaluateSpawnAreas.m_Industrial = industrial.AsParallelWriter();
			EvaluateSpawnAreas jobData = evaluateSpawnAreas;
			this.__TypeHandle.__Game_Prefabs_PlaceholderObjectElement_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_SubNet_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_SubAreaNode_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_SubArea_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Zones_Cell_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_NetGeometryData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_AreaGeometryData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_SpawnableObjectData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_PlaceableObjectData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Zones_ValidArea_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Zones_Block_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			SpawnBuildingJob spawnBuildingJob = default(SpawnBuildingJob);
			spawnBuildingJob.m_BlockData = this.__TypeHandle.__Game_Zones_Block_RO_ComponentLookup;
			spawnBuildingJob.m_ValidAreaData = this.__TypeHandle.__Game_Zones_ValidArea_RO_ComponentLookup;
			spawnBuildingJob.m_TransformData = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup;
			spawnBuildingJob.m_PrefabRefData = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup;
			spawnBuildingJob.m_PrefabBuildingData = this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup;
			spawnBuildingJob.m_PrefabPlaceableObjectData = this.__TypeHandle.__Game_Prefabs_PlaceableObjectData_RO_ComponentLookup;
			spawnBuildingJob.m_PrefabSpawnableObjectData = this.__TypeHandle.__Game_Prefabs_SpawnableObjectData_RO_ComponentLookup;
			spawnBuildingJob.m_PrefabObjectGeometryData = this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup;
			spawnBuildingJob.m_PrefabAreaGeometryData = this.__TypeHandle.__Game_Prefabs_AreaGeometryData_RO_ComponentLookup;
			spawnBuildingJob.m_PrefabNetGeometryData = this.__TypeHandle.__Game_Prefabs_NetGeometryData_RO_ComponentLookup;
			spawnBuildingJob.m_Cells = this.__TypeHandle.__Game_Zones_Cell_RO_BufferLookup;
			spawnBuildingJob.m_PrefabSubAreas = this.__TypeHandle.__Game_Prefabs_SubArea_RO_BufferLookup;
			spawnBuildingJob.m_PrefabSubAreaNodes = this.__TypeHandle.__Game_Prefabs_SubAreaNode_RO_BufferLookup;
			spawnBuildingJob.m_PrefabSubNets = this.__TypeHandle.__Game_Prefabs_SubNet_RO_BufferLookup;
			spawnBuildingJob.m_PrefabPlaceholderElements = this.__TypeHandle.__Game_Prefabs_PlaceholderObjectElement_RO_BufferLookup;
			spawnBuildingJob.m_DefinitionArchetype = this.m_DefinitionArchetype;
			spawnBuildingJob.m_RandomSeed = RandomSeed.Next();
			spawnBuildingJob.m_LefthandTraffic = this.m_CityConfigurationSystem.leftHandTraffic;
			spawnBuildingJob.m_TerrainHeightData = this.m_TerrainSystem.GetHeightData();
			spawnBuildingJob.m_ZoneSearchTree = this.m_SearchSystem.GetSearchTree(readOnly: true, out var dependencies2);
			spawnBuildingJob.m_BuildingConfigurationData = this.m_BuildingConfigurationQuery.GetSingleton<BuildingConfigurationData>();
			spawnBuildingJob.m_Residential = residential;
			spawnBuildingJob.m_Commercial = commercial;
			spawnBuildingJob.m_Industrial = industrial;
			spawnBuildingJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
			SpawnBuildingJob jobData2 = spawnBuildingJob;
			JobHandle jobHandle = JobChunkExtensions.ScheduleParallel(jobData, this.m_LotQuery, JobUtils.CombineDependencies(outJobHandle, deps, deps2, deps3, dependencies, base.Dependency, outJobHandle2));
			JobHandle jobHandle2 = IJobParallelForExtensions.Schedule(jobData2, 3, 1, JobHandle.CombineDependencies(jobHandle, dependencies2));
			this.m_ResourceSystem.AddPrefabsReader(jobHandle);
			this.m_PollutionSystem.AddReader(jobHandle);
			this.m_CommercialDemandSystem.AddReader(jobHandle);
			this.m_IndustrialDemandSystem.AddReader(jobHandle);
			residential.Dispose(jobHandle2);
			commercial.Dispose(jobHandle2);
			industrial.Dispose(jobHandle2);
			this.m_ZoneSystem.AddPrefabsReader(jobHandle);
			this.m_TerrainSystem.AddCPUHeightReader(jobHandle2);
			this.m_EndFrameBarrier.AddJobHandleForProducer(jobHandle2);
			this.m_SearchSystem.AddSearchTreeReader(jobHandle2);
			base.Dependency = jobHandle2;
		}
*/