#define UNITY_ASSERTIONS
using Colossal.Collections;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static MapExtPDX.MapExt.ReBurstSystemModeC.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeC
{
    [BurstCompile]
    public struct LandValueMapUpdateJob : IJobParallelFor
    {
        public NativeArray<LandValueCell> m_LandValueMap;

        [ReadOnly]
        public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;

        [ReadOnly]
        public NativeArray<TerrainAttractiveness> m_AttractiveMap;

        [ReadOnly]
        public NativeArray<GroundPollution> m_GroundPollutionMap;

        [ReadOnly]
        public NativeArray<AirPollution> m_AirPollutionMap;

        [ReadOnly]
        public NativeArray<NoisePollution> m_NoisePollutionMap;

        [ReadOnly]
        public NativeArray<AvailabilityInfoCell> m_AvailabilityInfoMap;

        [ReadOnly]
        public CellMapData<TelecomCoverage> m_TelecomCoverageMap;

        [ReadOnly]
        public WaterSurfaceData m_WaterSurfaceData;

        [ReadOnly]
        public TerrainHeightData m_TerrainHeightData;

        [ReadOnly]
        public ComponentLookup<LandValue> m_LandValueData;

        [ReadOnly]
        public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;

        [ReadOnly]
        public AttractivenessParameterData m_AttractivenessParameterData;

        [ReadOnly]
        public LandValueParameterData m_LandValueParameterData;

        public float m_CellSize;

        public void Execute(int index)
        {
            // OnUpdate中引入参数
            // m_CellSize = CellMapSystem<LandValueCell>.kMapSize / LandValueSystem.kTextureSize
            // 直接修改job内部逻辑，避免transpiler双重修补(使用直接数，不要用倍数，以免可能泛型双重修补）
            m_CellSize = 114688 / LandValueSystem.kTextureSize;

            int kTextureSize = 128;
            float3 cellCenter = GetCellCenter(index, kTextureSize);
            if (WaterUtils.SampleDepth(ref m_WaterSurfaceData, cellCenter) > 1f)
            {
                m_LandValueMap[index] = new LandValueCell
                {
                    m_LandValue = m_LandValueParameterData.m_LandValueBaseline
                };
                return;
            }
            NetIterator netIterator = default;
            netIterator.m_TotalCount = 0;
            netIterator.m_TotalLandValueBonus = 0f;
            netIterator.m_Bounds = new Bounds3(cellCenter - new float3(1.5f * m_CellSize, 10000f, 1.5f * m_CellSize), cellCenter + new float3(1.5f * m_CellSize, 10000f, 1.5f * m_CellSize));
            netIterator.m_EdgeGeometryData = m_EdgeGeometryData;
            netIterator.m_LandValueData = m_LandValueData;
            NetIterator iterator = netIterator;
            m_NetSearchTree.Iterate(ref iterator);
            float num = GroundPollutionSystemGetPollution(cellCenter, m_GroundPollutionMap).m_Pollution;
            float num2 = AirPollutionSystemGetPollution(cellCenter, m_AirPollutionMap).m_Pollution;
            float num3 = NoisePollutionSystemGetPollution(cellCenter, m_NoisePollutionMap).m_Pollution;
            float x = AvailabilityInfoToGridSystemGetAvailabilityInfo(cellCenter, m_AvailabilityInfoMap).m_AvailabilityInfo.x;
            float num4 = TelecomCoverage.SampleNetworkQuality(m_TelecomCoverageMap, cellCenter);
            LandValueCell value = m_LandValueMap[index];
            float num5 = iterator.m_TotalCount > 0f ? iterator.m_TotalLandValueBonus / iterator.m_TotalCount : 0f;
            float num6 = math.min((x - 5f) * m_LandValueParameterData.m_AttractivenessBonusMultiplier, m_LandValueParameterData.m_CommonFactorMaxBonus);
            float num7 = math.min(num4 * m_LandValueParameterData.m_TelecomCoverageBonusMultiplier, m_LandValueParameterData.m_CommonFactorMaxBonus);
            num5 += num6 + num7;
            float num8 = WaterUtils.SamplePolluted(ref m_WaterSurfaceData, cellCenter);
            float num9 = 0f;
            if (num8 <= 0f && num <= 0f)
            {
                num9 = TerrainAttractivenessSystem.EvaluateAttractiveness(TerrainUtils.SampleHeight(ref m_TerrainHeightData, cellCenter), m_AttractiveMap[index], m_AttractivenessParameterData);
                num5 += math.min(math.max(num9 - 5f, 0f) * m_LandValueParameterData.m_AttractivenessBonusMultiplier, m_LandValueParameterData.m_CommonFactorMaxBonus);
            }
            float num10 = num * m_LandValueParameterData.m_GroundPollutionPenaltyMultiplier + num2 * m_LandValueParameterData.m_AirPollutionPenaltyMultiplier + num3 * m_LandValueParameterData.m_NoisePollutionPenaltyMultiplier;
            float num11 = math.max(m_LandValueParameterData.m_LandValueBaseline, m_LandValueParameterData.m_LandValueBaseline + num5 - num10);
            if (math.abs(value.m_LandValue - num11) >= 0.1f)
            {
                value.m_LandValue = math.lerp(value.m_LandValue, num11, 0.4f);
            }
            m_LandValueMap[index] = value;
        }
        private struct NetIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            public int m_TotalCount;

            public float m_TotalLandValueBonus;

            public Bounds3 m_Bounds;

            public ComponentLookup<LandValue> m_LandValueData;

            public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;

            public bool Intersect(QuadTreeBoundsXZ bounds)
            {
                return MathUtils.Intersect(bounds.m_Bounds, m_Bounds);
            }

            public void Iterate(QuadTreeBoundsXZ bounds, Entity entity)
            {
                if (MathUtils.Intersect(bounds.m_Bounds, m_Bounds) && m_LandValueData.HasComponent(entity) && m_EdgeGeometryData.HasComponent(entity))
                {
                    LandValue landValue = m_LandValueData[entity];
                    if (landValue.m_LandValue > 0f)
                    {
                        m_TotalLandValueBonus += landValue.m_LandValue;
                        m_TotalCount++;
                    }
                }
            }
        }



    }

}

/*
		[BurstCompile]
		public struct EdgeUpdateJob : IJobChunk
		{
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			[ReadOnly]
			public ComponentTypeHandle<Edge> m_EdgeType;

			[ReadOnly]
			public BufferTypeHandle<Game.Net.ServiceCoverage> m_ServiceCoverageType;

			[ReadOnly]
			public BufferTypeHandle<ResourceAvailability> m_AvailabilityType;

			[NativeDisableParallelForRestriction]
			public ComponentLookup<LandValue> m_LandValues;

			[ReadOnly]
			public LandValueParameterData m_LandValueParameterData;

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<Edge> nativeArray2 = chunk.GetNativeArray(ref this.m_EdgeType);
				BufferAccessor<Game.Net.ServiceCoverage> bufferAccessor = chunk.GetBufferAccessor(ref this.m_ServiceCoverageType);
				BufferAccessor<ResourceAvailability> bufferAccessor2 = chunk.GetBufferAccessor(ref this.m_AvailabilityType);
				for (int i = 0; i < nativeArray2.Length; i++)
				{
					Entity entity = nativeArray[i];
					float num = 0f;
					float num2 = 0f;
					float num3 = 0f;
					if (bufferAccessor.Length > 0)
					{
						DynamicBuffer<Game.Net.ServiceCoverage> dynamicBuffer = bufferAccessor[i];
						Game.Net.ServiceCoverage serviceCoverage = dynamicBuffer[0];
						num = math.lerp(serviceCoverage.m_Coverage.x, serviceCoverage.m_Coverage.y, 0.5f) * this.m_LandValueParameterData.m_HealthCoverageBonusMultiplier;
						Game.Net.ServiceCoverage serviceCoverage2 = dynamicBuffer[5];
						num2 = math.lerp(serviceCoverage2.m_Coverage.x, serviceCoverage2.m_Coverage.y, 0.5f) * this.m_LandValueParameterData.m_EducationCoverageBonusMultiplier;
						Game.Net.ServiceCoverage serviceCoverage3 = dynamicBuffer[2];
						num3 = math.lerp(serviceCoverage3.m_Coverage.x, serviceCoverage3.m_Coverage.y, 0.5f) * this.m_LandValueParameterData.m_PoliceCoverageBonusMultiplier;
					}
					float num4 = 0f;
					float num5 = 0f;
					float num6 = 0f;
					if (bufferAccessor2.Length > 0)
					{
						DynamicBuffer<ResourceAvailability> dynamicBuffer2 = bufferAccessor2[i];
						ResourceAvailability resourceAvailability = dynamicBuffer2[1];
						num4 = math.lerp(resourceAvailability.m_Availability.x, resourceAvailability.m_Availability.y, 0.5f) * this.m_LandValueParameterData.m_CommercialServiceBonusMultiplier;
						ResourceAvailability resourceAvailability2 = dynamicBuffer2[31];
						num5 = math.lerp(resourceAvailability2.m_Availability.x, resourceAvailability2.m_Availability.y, 0.5f) * this.m_LandValueParameterData.m_BusBonusMultiplier;
						ResourceAvailability resourceAvailability3 = dynamicBuffer2[32];
						num6 = math.lerp(resourceAvailability3.m_Availability.x, resourceAvailability3.m_Availability.y, 0.5f) * this.m_LandValueParameterData.m_TramSubwayBonusMultiplier;
					}
					LandValue value = this.m_LandValues[entity];
					float num7 = math.max(num + num2 + num3 + num4 + num5 + num6, 0f);
					if (math.abs(value.m_LandValue - num7) >= 0.1f)
					{
						float x = math.lerp(value.m_LandValue, num7, 0.6f);
						value.m_LandValue = math.max(x, 0f);
						this.m_LandValues[entity] = value;
					}
				}
			}

			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}
		}
*/

/*
    [Preserve]
    protected override void OnUpdate()
    {
        if (!this.m_EdgeGroup.IsEmptyIgnoreFilter)
        {
            this.__TypeHandle.__Game_Net_LandValue_RW_ComponentLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Net_ResourceAvailability_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Net_ServiceCoverage_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Net_Edge_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Unity_Entities_Entity_TypeHandle.Update(ref base.CheckedStateRef);
            EdgeUpdateJob edgeUpdateJob = default(EdgeUpdateJob);
            edgeUpdateJob.m_EntityType = this.__TypeHandle.__Unity_Entities_Entity_TypeHandle;
            edgeUpdateJob.m_EdgeType = this.__TypeHandle.__Game_Net_Edge_RO_ComponentTypeHandle;
            edgeUpdateJob.m_ServiceCoverageType = this.__TypeHandle.__Game_Net_ServiceCoverage_RO_BufferTypeHandle;
            edgeUpdateJob.m_AvailabilityType = this.__TypeHandle.__Game_Net_ResourceAvailability_RO_BufferTypeHandle;
            edgeUpdateJob.m_LandValues = this.__TypeHandle.__Game_Net_LandValue_RW_ComponentLookup;
            edgeUpdateJob.m_LandValueParameterData = this.m_LandValueParameterQuery.GetSingleton<LandValueParameterData>();
            EdgeUpdateJob jobData = edgeUpdateJob;
            base.Dependency = JobChunkExtensions.ScheduleParallel(jobData, this.m_EdgeGroup, base.Dependency);
        }
        this.__TypeHandle.__Game_Net_EdgeGeometry_RO_ComponentLookup.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Net_LandValue_RO_ComponentLookup.Update(ref base.CheckedStateRef);
        LandValueMapUpdateJob landValueMapUpdateJob = default(LandValueMapUpdateJob);
        landValueMapUpdateJob.m_NetSearchTree = this.m_NetSearchSystem.GetNetSearchTree(readOnly: true, out var dependencies);
        landValueMapUpdateJob.m_AttractiveMap = this.m_TerrainAttractivenessSystem.GetMap(readOnly: true, out var dependencies2);
        landValueMapUpdateJob.m_GroundPollutionMap = this.m_GroundPollutionSystem.GetMap(readOnly: true, out var dependencies3);
        landValueMapUpdateJob.m_AirPollutionMap = this.m_AirPollutionSystem.GetMap(readOnly: true, out var dependencies4);
        landValueMapUpdateJob.m_NoisePollutionMap = this.m_NoisePollutionSystem.GetMap(readOnly: true, out var dependencies5);
        landValueMapUpdateJob.m_AvailabilityInfoMap = this.m_AvailabilityInfoToGridSystem.GetMap(readOnly: true, out var dependencies6);
        landValueMapUpdateJob.m_TelecomCoverageMap = this.m_TelecomCoverageSystem.GetData(readOnly: true, out var dependencies7);
        landValueMapUpdateJob.m_LandValueMap = base.m_Map;
        landValueMapUpdateJob.m_LandValueData = this.__TypeHandle.__Game_Net_LandValue_RO_ComponentLookup;
        landValueMapUpdateJob.m_TerrainHeightData = this.m_TerrainSystem.GetHeightData();
        landValueMapUpdateJob.m_WaterSurfaceData = this.m_WaterSystem.GetSurfaceData(out var deps);
        landValueMapUpdateJob.m_EdgeGeometryData = this.__TypeHandle.__Game_Net_EdgeGeometry_RO_ComponentLookup;
        landValueMapUpdateJob.m_AttractivenessParameterData = this.m_AttractivenessParameterQuery.GetSingleton<AttractivenessParameterData>();
        landValueMapUpdateJob.m_LandValueParameterData = this.m_LandValueParameterQuery.GetSingleton<LandValueParameterData>();
        landValueMapUpdateJob.m_CellSize = (float)CellMapSystem<LandValueCell>.kMapSize / (float)LandValueSystem.kTextureSize;
        LandValueMapUpdateJob jobData2 = landValueMapUpdateJob;
        base.Dependency = IJobParallelForExtensions.Schedule(jobData2, LandValueSystem.kTextureSize * LandValueSystem.kTextureSize, LandValueSystem.kTextureSize, JobHandle.CombineDependencies(dependencies, dependencies2, JobHandle.CombineDependencies(base.m_WriteDependencies, base.m_ReadDependencies, JobHandle.CombineDependencies(base.Dependency, deps, JobHandle.CombineDependencies(dependencies3, dependencies5, JobHandle.CombineDependencies(dependencies6, dependencies4, dependencies7))))));
        base.AddWriter(base.Dependency);
        this.m_NetSearchSystem.AddNetSearchTreeReader(base.Dependency);
        this.m_WaterSystem.AddSurfaceReader(base.Dependency);
        this.m_TerrainAttractivenessSystem.AddReader(base.Dependency);
        this.m_GroundPollutionSystem.AddReader(base.Dependency);
        this.m_AirPollutionSystem.AddReader(base.Dependency);
        this.m_NoisePollutionSystem.AddReader(base.Dependency);
        this.m_AvailabilityInfoToGridSystem.AddReader(base.Dependency);
        this.m_TelecomCoverageSystem.AddReader(base.Dependency);
        this.m_TerrainSystem.AddCPUHeightReader(base.Dependency);
        base.Dependency = JobHandle.CombineDependencies(base.m_ReadDependencies, base.m_WriteDependencies, base.Dependency);
    }
*/