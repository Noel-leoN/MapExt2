using System;
using System.Runtime.CompilerServices;
using Colossal.Collections;
using Game.Buildings;
using Game.Common;
using Game.Notifications;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using Game.Simulation;
using static MapExtPDX.MapExt.ReBurstSystemModeD.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeD
{
    [BurstCompile]
    public struct PumpTickJob : IJobChunk
    {
        [ReadOnly]
        public EntityTypeHandle m_EntityType;

        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabType;

        [ReadOnly]
        public ComponentTypeHandle<Game.Objects.Transform> m_TransformType;

        [ReadOnly]
        public BufferTypeHandle<Game.Objects.SubObject> m_SubObjectType;

        [ReadOnly]
        public BufferTypeHandle<InstalledUpgrade> m_InstalledUpgradeType;

        [ReadOnly]
        public ComponentTypeHandle<WaterPipeBuildingConnection> m_BuildingConnectionType;

        [ReadOnly]
        public BufferTypeHandle<IconElement> m_IconElementType;

        public ComponentTypeHandle<Game.Buildings.WaterPumpingStation> m_WaterPumpingStationType;

        public ComponentTypeHandle<Game.Buildings.SewageOutlet> m_SewageOutletType;

        public BufferTypeHandle<Efficiency> m_EfficiencyType;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_Prefabs;

        [ReadOnly]
        public ComponentLookup<WaterPumpingStationData> m_PumpDatas;

        [ReadOnly]
        public ComponentLookup<Game.Objects.Transform> m_Transforms;

        public ComponentLookup<Game.Simulation.WaterSourceData> m_WaterSources;

        public ComponentLookup<WaterPipeEdge> m_FlowEdges;

        [ReadOnly]
        public WaterSurfaceData m_WaterSurfaceData;

        public NativeArray<GroundWater> m_GroundWaterMap;

        public IconCommandBuffer m_IconCommandBuffer;

        public WaterPipeParameterData m_Parameters;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
            NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref this.m_PrefabType);
            NativeArray<Game.Objects.Transform> nativeArray3 = chunk.GetNativeArray(ref this.m_TransformType);
            BufferAccessor<Game.Objects.SubObject> bufferAccessor = chunk.GetBufferAccessor(ref this.m_SubObjectType);
            BufferAccessor<InstalledUpgrade> bufferAccessor2 = chunk.GetBufferAccessor(ref this.m_InstalledUpgradeType);
            NativeArray<WaterPipeBuildingConnection> nativeArray4 = chunk.GetNativeArray(ref this.m_BuildingConnectionType);
            BufferAccessor<IconElement> bufferAccessor3 = chunk.GetBufferAccessor(ref this.m_IconElementType);
            NativeArray<Game.Buildings.WaterPumpingStation> nativeArray5 = chunk.GetNativeArray(ref this.m_WaterPumpingStationType);
            NativeArray<Game.Buildings.SewageOutlet> nativeArray6 = chunk.GetNativeArray(ref this.m_SewageOutletType);
            BufferAccessor<Efficiency> bufferAccessor4 = chunk.GetBufferAccessor(ref this.m_EfficiencyType);
            Span<float> factors = stackalloc float[30];
            for (int i = 0; i < chunk.Count; i++)
            {
                Entity entity = nativeArray[i];
                Entity prefab = nativeArray2[i].m_Prefab;
                WaterPipeBuildingConnection waterPipeBuildingConnection = nativeArray4[i];
                DynamicBuffer<IconElement> iconElements = ((bufferAccessor3.Length != 0) ? bufferAccessor3[i] : default(DynamicBuffer<IconElement>));
                ref Game.Buildings.WaterPumpingStation reference = ref nativeArray5.ElementAt(i);
                WaterPumpingStationData data = this.m_PumpDatas[prefab];
                if (bufferAccessor2.Length != 0)
                {
                    UpgradeUtils.CombineStats(ref data, bufferAccessor2[i], ref this.m_Prefabs, ref this.m_PumpDatas);
                }
                if (waterPipeBuildingConnection.m_ProducerEdge == Entity.Null)
                {
                    UnityEngine.Debug.LogError("WaterPumpingStation is missing producer edge!");
                    continue;
                }
                if (bufferAccessor4.Length != 0)
                {
                    BuildingUtils.GetEfficiencyFactors(bufferAccessor4[i], factors);
                    factors[19] = 1f;
                }
                else
                {
                    factors.Fill(1f);
                }
                float efficiency = BuildingUtils.GetEfficiency(factors);
                WaterPipeEdge value = this.m_FlowEdges[waterPipeBuildingConnection.m_ProducerEdge];
                reference.m_LastProduction = value.m_FreshFlow;
                float num = reference.m_LastProduction;
                reference.m_Pollution = 0f;
                reference.m_Capacity = 0;
                int num2 = 0;
                if (nativeArray6.Length != 0)
                {
                    ref Game.Buildings.SewageOutlet reference2 = ref nativeArray6.ElementAt(i);
                    num2 = reference2.m_LastPurified;
                    reference2.m_UsedPurified = math.min(reference.m_LastProduction, reference2.m_LastPurified);
                    num -= (float)reference2.m_UsedPurified;
                }
                float num3 = 0f;
                float num4 = 0f;
                bool flag = false;
                bool flag2 = false;
                if (data.m_Types != AllowedWaterTypes.None)
                {
                    if ((data.m_Types & AllowedWaterTypes.Groundwater) != AllowedWaterTypes.None)
                    {
                        GroundWater groundWater = GroundWaterSystemGetGroundWater(nativeArray3[i].m_Position, this.m_GroundWaterMap);
                        float num5 = (float)groundWater.m_Polluted / math.max(1f, groundWater.m_Amount);
                        float num6 = (float)groundWater.m_Amount / this.m_Parameters.m_GroundwaterPumpEffectiveAmount;
                        float num7 = math.clamp(num6 * (float)data.m_Capacity, 0f, (float)data.m_Capacity - num3);
                        num3 += num7;
                        num4 += num5 * num7;
                        flag = num6 < 0.75f && (float)groundWater.m_Amount < 0.75f * (float)groundWater.m_Max;
                        int num8 = (int)math.ceil(num * this.m_Parameters.m_GroundwaterUsageMultiplier);
                        int num9 = math.min(num8, groundWater.m_Amount);
                        GroundWaterSystemConsumeGroundWater(nativeArray3[i].m_Position, this.m_GroundWaterMap, num9);
                        num = Mathf.FloorToInt((float)(num8 - num9) / this.m_Parameters.m_GroundwaterUsageMultiplier);
                    }
                    if ((data.m_Types & AllowedWaterTypes.SurfaceWater) != AllowedWaterTypes.None && bufferAccessor.Length != 0)
                    {
                        DynamicBuffer<Game.Objects.SubObject> dynamicBuffer = bufferAccessor[i];
                        for (int j = 0; j < dynamicBuffer.Length; j++)
                        {
                            Entity subObject = dynamicBuffer[j].m_SubObject;
                            if (this.m_WaterSources.TryGetComponent(subObject, out var componentData) && this.m_Transforms.TryGetComponent(subObject, out var componentData2))
                            {
                                float surfaceWaterAvailability = WaterPumpingStationAISystem.GetSurfaceWaterAvailability(componentData2.m_Position, data.m_Types, this.m_WaterSurfaceData, this.m_Parameters.m_SurfaceWaterPumpEffectiveDepth);
                                float num10 = WaterUtils.SamplePolluted(ref this.m_WaterSurfaceData, componentData2.m_Position);
                                float num11 = math.clamp(surfaceWaterAvailability * (float)data.m_Capacity, 0f, (float)data.m_Capacity - num3);
                                num3 += num11;
                                num4 += num11 * num10;
                                flag2 = surfaceWaterAvailability < 0.75f;
                                componentData.m_Amount = (0f - this.m_Parameters.m_SurfaceWaterUsageMultiplier) * num;
                                componentData.m_Polluted = 0f;
                                this.m_WaterSources[subObject] = componentData;
                                num = 0f;
                            }
                        }
                    }
                }
                else
                {
                    num3 = data.m_Capacity;
                    num4 = 0f;
                    num = 0f;
                }
                reference.m_Capacity = (int)math.round(efficiency * num3 + (float)num2);
                reference.m_Pollution = ((reference.m_Capacity > 0) ? ((1f - data.m_Purification) * num4 / (float)reference.m_Capacity) : 0f);
                value.m_FreshCapacity = reference.m_Capacity;
                value.m_FreshPollution = ((reference.m_Capacity > 0) ? reference.m_Pollution : 0f);
                this.m_FlowEdges[waterPipeBuildingConnection.m_ProducerEdge] = value;
                if (bufferAccessor4.Length != 0)
                {
                    if (data.m_Capacity > 0)
                    {
                        float num12 = (num3 + (float)num2) / (float)(data.m_Capacity + num2);
                        factors[19] = num12;
                    }
                    BuildingUtils.SetEfficiencyFactors(bufferAccessor4[i], factors);
                }
                bool flag3 = num3 < 0.1f * (float)data.m_Capacity;
                this.UpdateNotification(entity, this.m_Parameters.m_NotEnoughGroundwaterNotification, flag && flag3, iconElements);
                this.UpdateNotification(entity, this.m_Parameters.m_NotEnoughSurfaceWaterNotification, flag2 && flag3, iconElements);
                this.UpdateNotification(entity, this.m_Parameters.m_DirtyWaterPumpNotification, reference.m_Pollution > this.m_Parameters.m_MaxToleratedPollution, iconElements);
                bool flag4 = (value.m_Flags & WaterPipeEdgeFlags.WaterShortage) != 0;
                this.UpdateNotification(entity, this.m_Parameters.m_NotEnoughWaterCapacityNotification, reference.m_Capacity > 0 && flag4, iconElements);
            }
        }

        private void UpdateNotification(Entity entity, Entity notificationPrefab, bool enabled, DynamicBuffer<IconElement> iconElements)
        {
            bool flag = this.HasNotification(iconElements, notificationPrefab);
            if (enabled != flag)
            {
                if (enabled)
                {
                    this.m_IconCommandBuffer.Add(entity, notificationPrefab);
                }
                else
                {
                    this.m_IconCommandBuffer.Remove(entity, notificationPrefab);
                }
            }
        }

        private bool HasNotification(DynamicBuffer<IconElement> iconElements, Entity notificationPrefab)
        {
            if (iconElements.IsCreated)
            {
                foreach (IconElement item in iconElements)
                {
                    if (this.m_Prefabs[item.m_Icon].m_Prefab == notificationPrefab)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }
}
