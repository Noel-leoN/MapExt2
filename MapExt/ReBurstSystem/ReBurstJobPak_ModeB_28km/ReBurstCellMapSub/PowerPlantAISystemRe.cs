using System;
using System.Runtime.CompilerServices;
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using static MapExtPDX.MapExt.ReBurstSystemModeB.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeB
{
    [BurstCompile]
    public struct PowerPlantTickJob : IJobChunk
    {
        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabType;

        [ReadOnly]
        public ComponentTypeHandle<Game.Buildings.GarbageFacility> m_GarbageFacilityType;

        [ReadOnly]
        public BufferTypeHandle<InstalledUpgrade> m_InstalledUpgradeType;

        [ReadOnly]
        public ComponentTypeHandle<ElectricityBuildingConnection> m_BuildingConnectionType;

        [ReadOnly]
        public ComponentTypeHandle<Game.Buildings.ResourceConsumer> m_ResourceConsumerType;

        [ReadOnly]
        public ComponentTypeHandle<Game.Buildings.WaterPowered> m_WaterPoweredType;

        [ReadOnly]
        public ComponentTypeHandle<Game.Objects.Transform> m_TransformType;

        [ReadOnly]
        public BufferTypeHandle<Game.Net.SubNet> m_SubNetType;

        public ComponentTypeHandle<ElectricityProducer> m_ElectricityProducerType;

        public BufferTypeHandle<Efficiency> m_EfficiencyType;

        public ComponentTypeHandle<ServiceUsage> m_ServiceUsageType;

        public ComponentTypeHandle<PointOfInterest> m_PointOfInterestType;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_Prefabs;

        [ReadOnly]
        public ComponentLookup<PowerPlantData> m_PowerPlantDatas;

        [ReadOnly]
        public ComponentLookup<GarbagePoweredData> m_GarbagePoweredData;

        [ReadOnly]
        public ComponentLookup<WindPoweredData> m_WindPoweredData;

        [ReadOnly]
        public ComponentLookup<WaterPoweredData> m_WaterPoweredData;

        [ReadOnly]
        public ComponentLookup<SolarPoweredData> m_SolarPoweredData;

        [ReadOnly]
        public ComponentLookup<GroundWaterPoweredData> m_GroundWaterPoweredData;

        [ReadOnly]
        public ComponentLookup<PlaceableNetData> m_PlaceableNetData;

        [ReadOnly]
        public ComponentLookup<NetCompositionData> m_NetCompositionData;

        [ReadOnly]
        public ComponentLookup<Game.Buildings.ResourceConsumer> m_ResourceConsumers;

        [ReadOnly]
        public ComponentLookup<Curve> m_Curves;

        [ReadOnly]
        public ComponentLookup<Composition> m_Compositions;

        [NativeDisableContainerSafetyRestriction]
        public ComponentLookup<ServiceUsage> m_ServiceUsages;

        [NativeDisableParallelForRestriction]
        public ComponentLookup<ElectricityFlowEdge> m_FlowEdges;

        [ReadOnly]
        public NativeArray<Wind> m_WindMap;

        [ReadOnly]
        public TerrainHeightData m_TerrainHeightData;

        [ReadOnly]
        public WaterSurfaceData m_WaterSurfaceData;

        [ReadOnly]
        public NativeArray<GroundWater> m_GroundWaterMap;

        public float m_SunLight;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<PrefabRef> nativeArray = chunk.GetNativeArray(ref this.m_PrefabType);
            NativeArray<Game.Buildings.GarbageFacility> nativeArray2 = chunk.GetNativeArray(ref this.m_GarbageFacilityType);
            BufferAccessor<InstalledUpgrade> bufferAccessor = chunk.GetBufferAccessor(ref this.m_InstalledUpgradeType);
            NativeArray<ElectricityBuildingConnection> nativeArray3 = chunk.GetNativeArray(ref this.m_BuildingConnectionType);
            NativeArray<ElectricityProducer> nativeArray4 = chunk.GetNativeArray(ref this.m_ElectricityProducerType);
            NativeArray<Game.Buildings.WaterPowered> nativeArray5 = chunk.GetNativeArray(ref this.m_WaterPoweredType);
            NativeArray<Game.Objects.Transform> nativeArray6 = chunk.GetNativeArray(ref this.m_TransformType);
            BufferAccessor<Game.Net.SubNet> bufferAccessor2 = chunk.GetBufferAccessor(ref this.m_SubNetType);
            NativeArray<Game.Buildings.ResourceConsumer> nativeArray7 = chunk.GetNativeArray(ref this.m_ResourceConsumerType);
            BufferAccessor<Efficiency> bufferAccessor3 = chunk.GetBufferAccessor(ref this.m_EfficiencyType);
            NativeArray<ServiceUsage> nativeArray8 = chunk.GetNativeArray(ref this.m_ServiceUsageType);
            NativeArray<PointOfInterest> nativeArray9 = chunk.GetNativeArray(ref this.m_PointOfInterestType);
            Span<float> factors = stackalloc float[30];
            for (int i = 0; i < chunk.Count; i++)
            {
                Entity prefab = nativeArray[i].m_Prefab;
                ref ElectricityProducer reference = ref nativeArray4.ElementAt(i);
                ElectricityBuildingConnection electricityBuildingConnection = nativeArray3[i];
                byte b = ((nativeArray7.Length != 0) ? nativeArray7[i].m_ResourceAvailability : byte.MaxValue);
                Game.Objects.Transform transform = nativeArray6[i];
                if (bufferAccessor3.Length != 0)
                {
                    BuildingUtils.GetEfficiencyFactors(bufferAccessor3[i], factors);
                    factors[17] = 1f;
                    factors[18] = 1f;
                    factors[19] = 1f;
                    factors[20] = 1f;
                }
                else
                {
                    factors.Fill(1f);
                }
                float efficiency = BuildingUtils.GetEfficiency(factors);
                if (electricityBuildingConnection.m_ProducerEdge == Entity.Null)
                {
                    UnityEngine.Debug.LogError("PowerPlant is missing producer edge!");
                    continue;
                }
                ElectricityFlowEdge value = this.m_FlowEdges[electricityBuildingConnection.m_ProducerEdge];
                reference.m_LastProduction = value.m_Flow;
                float num = ((reference.m_Capacity > 0) ? ((float)reference.m_LastProduction / (float)reference.m_Capacity) : 0f);
                if (nativeArray8.Length != 0)
                {
                    nativeArray8[i] = new ServiceUsage
                    {
                        m_Usage = ((b > 0) ? num : 0f)
                    };
                }
                if (bufferAccessor.Length != 0)
                {
                    foreach (InstalledUpgrade item in bufferAccessor[i])
                    {
                        if (!BuildingUtils.CheckOption(item, BuildingOption.Inactive) && this.m_PowerPlantDatas.HasComponent(item) && this.m_ServiceUsages.HasComponent(item))
                        {
                            Game.Buildings.ResourceConsumer componentData;
                            byte b2 = (this.m_ResourceConsumers.TryGetComponent(item.m_Upgrade, out componentData) ? componentData.m_ResourceAvailability : b);
                            this.m_ServiceUsages[item] = new ServiceUsage
                            {
                                m_Usage = ((b2 > 0) ? num : 0f)
                            };
                        }
                    }
                }
                float2 zero = Unity.Mathematics.float2.zero;
                if (this.m_PowerPlantDatas.TryGetComponent(prefab, out var componentData2))
                {
                    zero += PowerPlantTickJob.GetPowerPlantProduction(componentData2, b, efficiency);
                }
                if (bufferAccessor.Length != 0)
                {
                    foreach (InstalledUpgrade item2 in bufferAccessor[i])
                    {
                        if (!BuildingUtils.CheckOption(item2, BuildingOption.Inactive) && this.m_PowerPlantDatas.TryGetComponent(this.m_Prefabs[item2.m_Upgrade], out componentData2))
                        {
                            Game.Buildings.ResourceConsumer componentData3;
                            byte resourceAvailability = (this.m_ResourceConsumers.TryGetComponent(item2.m_Upgrade, out componentData3) ? componentData3.m_ResourceAvailability : b);
                            zero += PowerPlantTickJob.GetPowerPlantProduction(componentData2, resourceAvailability, efficiency);
                        }
                    }
                }
                this.m_GarbagePoweredData.TryGetComponent(prefab, out var componentData4);
                this.m_WindPoweredData.TryGetComponent(prefab, out var componentData5);
                this.m_WaterPoweredData.TryGetComponent(prefab, out var componentData6);
                this.m_SolarPoweredData.TryGetComponent(prefab, out var componentData7);
                this.m_GroundWaterPoweredData.TryGetComponent(prefab, out var componentData8);
                if (bufferAccessor.Length != 0)
                {
                    UpgradeUtils.CombineStats(ref componentData4, bufferAccessor[i], ref this.m_Prefabs, ref this.m_GarbagePoweredData);
                    UpgradeUtils.CombineStats(ref componentData5, bufferAccessor[i], ref this.m_Prefabs, ref this.m_WindPoweredData);
                    UpgradeUtils.CombineStats(ref componentData6, bufferAccessor[i], ref this.m_Prefabs, ref this.m_WaterPoweredData);
                    UpgradeUtils.CombineStats(ref componentData7, bufferAccessor[i], ref this.m_Prefabs, ref this.m_SolarPoweredData);
                    UpgradeUtils.CombineStats(ref componentData8, bufferAccessor[i], ref this.m_Prefabs, ref this.m_GroundWaterPoweredData);
                }
                float2 @float = Unity.Mathematics.float2.zero;
                if (componentData4.m_Capacity > 0 && nativeArray2.Length != 0)
                {
                    @float = PowerPlantTickJob.GetGarbageProduction(componentData4, nativeArray2[i]);
                }
                float2 float2 = float2.zero;
                if (componentData5.m_Production > 0)
                {
                    Wind wind = WindSystemGetWind(nativeArray6[i].m_Position, this.m_WindMap);
                    float2 = GetWindProduction(componentData5, wind, efficiency);
                    if (float2.x > 0f && nativeArray9.Length != 0 && math.any(wind.m_Wind))
                    {
                        ref PointOfInterest reference2 = ref nativeArray9.ElementAt(i);
                        reference2.m_Position = transform.m_Position;
                        reference2.m_Position.xz -= wind.m_Wind;
                        reference2.m_IsValid = true;
                    }
                }
                float2 zero2 = float2.zero;
                if (nativeArray5.Length != 0 && bufferAccessor2.Length != 0 && componentData6.m_ProductionFactor > 0f)
                {
                    zero2 += this.GetWaterProduction(componentData6, nativeArray5[i], bufferAccessor2[i], efficiency);
                }
                if (componentData8.m_Production > 0 && componentData8.m_MaximumGroundWater > 0)
                {
                    zero2 += GetGroundWaterProduction(componentData8, nativeArray6[i].m_Position, efficiency, this.m_GroundWaterMap);
                }
                float2 float3 = float2.zero;
                if (componentData7.m_Production > 0)
                {
                    float3 = this.GetSolarProduction(componentData7, efficiency);
                }
                float2 float4 = math.round(zero + @float + float2 + zero2 + float3);
                value.m_Capacity = (reference.m_Capacity = (int)float4.x);
                this.m_FlowEdges[electricityBuildingConnection.m_ProducerEdge] = value;
                if (bufferAccessor3.Length != 0)
                {
                    if (float4.y > 0f)
                    {
                        float targetEfficiency = float4.x / float4.y;
                        float4 weights = new float4(zero.y - zero.x, float2.y - float2.x, zero2.y - zero2.x, float3.y - float3.x);
                        float4 float5 = BuildingUtils.ApproximateEfficiencyFactors(targetEfficiency, weights);
                        factors[17] = float5.x;
                        factors[18] = float5.y;
                        factors[19] = float5.z;
                        factors[20] = float5.w;
                    }
                    BuildingUtils.SetEfficiencyFactors(bufferAccessor3[i], factors);
                }
            }
        }

        private static float2 GetPowerPlantProduction(PowerPlantData powerPlantData, byte resourceAvailability, float efficiency)
        {
            float num = efficiency * (float)powerPlantData.m_ElectricityProduction;
            return new float2((resourceAvailability > 0) ? num : 0f, num);
        }

        private static float GetGarbageProduction(GarbagePoweredData garbageData, Game.Buildings.GarbageFacility garbageFacility)
        {
            return math.clamp((float)garbageFacility.m_ProcessingRate / garbageData.m_ProductionPerUnit, 0f, garbageData.m_Capacity);
        }

        private float2 GetWaterProduction(WaterPoweredData waterData, Game.Buildings.WaterPowered waterPowered, DynamicBuffer<Game.Net.SubNet> subNets, float efficiency)
        {
            float num = 0f;
            for (int i = 0; i < subNets.Length; i++)
            {
                Entity subNet = subNets[i].m_SubNet;
                PrefabRef prefabRef = this.m_Prefabs[subNet];
                if (this.m_Curves.TryGetComponent(subNet, out var componentData) && this.m_Compositions.TryGetComponent(subNet, out var componentData2) && this.m_PlaceableNetData.TryGetComponent(prefabRef.m_Prefab, out var componentData3) && this.m_NetCompositionData.TryGetComponent(componentData2.m_Edge, out var componentData4) && (componentData3.m_PlacementFlags & (Game.Net.PlacementFlags.FlowLeft | Game.Net.PlacementFlags.FlowRight)) != Game.Net.PlacementFlags.None && (componentData4.m_Flags.m_General & (CompositionFlags.General.Spillway | CompositionFlags.General.Front | CompositionFlags.General.Back)) == 0)
                {
                    num += this.GetWaterProduction(waterData, componentData, componentData3, componentData4, this.m_TerrainHeightData, this.m_WaterSurfaceData);
                }
            }
            float num2 = efficiency * GetWaterCapacity(waterPowered, waterData);
            return new float2(math.clamp(efficiency * num, 0f, num2), num2);
        }

        private float GetWaterProduction(WaterPoweredData waterData, Curve curve, PlaceableNetData placeableData, NetCompositionData compositionData, TerrainHeightData terrainHeightData, WaterSurfaceData waterSurfaceData)
        {
            int num = math.max(1, (int)math.round(curve.m_Length * waterSurfaceData.scale.x));
            bool test = (placeableData.m_PlacementFlags & Game.Net.PlacementFlags.FlowLeft) != 0;
            float num2 = 0f;
            for (int i = 0; i < num; i++)
            {
                float t = ((float)i + 0.5f) / (float)num;
                float3 @float = MathUtils.Position(curve.m_Bezier, t);
                float3 float2 = MathUtils.Tangent(curve.m_Bezier, t);
                float2 float3 = math.normalizesafe(math.select(MathUtils.Right(float2.xz), MathUtils.Left(float2.xz), test));
                float3 worldPosition = @float;
                float3 worldPosition2 = @float;
                worldPosition.xz -= float3 * (compositionData.m_Width * 0.5f);
                worldPosition2.xz += float3 * (compositionData.m_Width * 0.5f);
                float waterDepth;
                float num3 = WaterUtils.SampleHeight(ref waterSurfaceData, ref terrainHeightData, worldPosition, out waterDepth);
                float waterDepth2;
                float num4 = WaterUtils.SampleHeight(ref waterSurfaceData, ref terrainHeightData, worldPosition2, out waterDepth2);
                float2 x = WaterUtils.SampleVelocity(ref waterSurfaceData, worldPosition);
                float2 x2 = WaterUtils.SampleVelocity(ref waterSurfaceData, worldPosition2);
                if (num3 > worldPosition.y)
                {
                    waterDepth = math.max(0f, waterDepth - (num3 - worldPosition.y));
                    num3 = worldPosition.y;
                }
                num2 += (math.dot(x, float3) * waterDepth + math.dot(x2, float3) * waterDepth2) * 0.5f * math.max(0f, num3 - num4);
            }
            return num2 * waterData.m_ProductionFactor * curve.m_Length / (float)num;
        }

        private float2 GetSolarProduction(SolarPoweredData solarData, float efficiency)
        {
            float num = efficiency * (float)solarData.m_Production;
            return new float2(math.clamp(num * this.m_SunLight, 0f, num), num);
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }


        private static float2 GetWindProduction(WindPoweredData windData, Wind wind, float efficiency)
        {
            float num = efficiency * (float)windData.m_Production;
            float x = math.lengthsq(wind.m_Wind) / (windData.m_MaximumWind * windData.m_MaximumWind);
            return new float2(num * math.saturate(math.pow(x, 1.5f)), num);
        }

        private static float GetWaterCapacity(Game.Buildings.WaterPowered waterPowered, WaterPoweredData waterData)
        {
            return math.min(waterPowered.m_Length * waterPowered.m_Height, 1000000f) * waterData.m_CapacityFactor;
        }

        private static float2 GetGroundWaterProduction(GroundWaterPoweredData groundWaterData, float3 position, float efficiency, NativeArray<GroundWater> groundWaterMap)
        {
            float num = (float)GroundWaterSystemGetGroundWater(position, groundWaterMap).m_Amount / (float)groundWaterData.m_MaximumGroundWater;
            float num2 = efficiency * (float)groundWaterData.m_Production;
            return new float2(math.clamp(num2 * num, 0f, num2), num2);
        }
    }
}
