using System.Runtime.CompilerServices;
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Zones;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using Game.Rendering;
using Game.UI.Tooltip;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;

namespace MapExt2.Systems
{
    /// <summary>
	/// 暂不修补
    /// 仅引用GroundPollutionSystemRe,job过大，作用不大
    /// </summary>
    public class NetColorSystemRe
	{
        [HarmonyPatch]
        public static class JobReplacementPatch
        {
            /// <summary>
            /// 填写目标方法MethodA
            /// </summary>
            static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(NetColorSystem), "OnUpdate");
            }

            /// <summary>
            /// 填写目标系统/JobB,替换JobC
            /// 填写debug log前缀
            /// </summary>
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
            {
                return JobReplaceTranspilerHelper.ReplaceJobSchedule(
                    instructions,
                    oldJobType: AccessTools.Inner(typeof(NetColorSystem), "UpdateEdgeColorsJob"),
                    newJobType: typeof(UpdateEdgeColorsJob),
                    logPrefix: "[NetColorSystemJobReplacePatch] "
                );
            }
        }

        [BurstCompile]
		private struct UpdateEdgeColorsJob : IJobChunk
		{
			[ReadOnly]
			public NativeList<ArchetypeChunk> m_InfomodeChunks;

			[ReadOnly]
			public ComponentTypeHandle<InfomodeActive> m_InfomodeActiveType;

			[ReadOnly]
			public ComponentTypeHandle<InfoviewCoverageData> m_InfoviewCoverageType;

			[ReadOnly]
			public ComponentTypeHandle<InfoviewAvailabilityData> m_InfoviewAvailabilityType;

			[ReadOnly]
			public ComponentTypeHandle<InfoviewNetGeometryData> m_InfoviewNetGeometryType;

			[ReadOnly]
			public ComponentTypeHandle<InfoviewNetStatusData> m_InfoviewNetStatusType;

			[ReadOnly]
			public ComponentTypeHandle<TrainTrack> m_TrainTrackType;

			[ReadOnly]
			public ComponentTypeHandle<TramTrack> m_TramTrackType;

			[ReadOnly]
			public ComponentTypeHandle<Waterway> m_WaterwayType;

			[ReadOnly]
			public ComponentTypeHandle<SubwayTrack> m_SubwayTrackType;

			[ReadOnly]
			public ComponentTypeHandle<NetCondition> m_NetConditionType;

			[ReadOnly]
			public ComponentTypeHandle<Road> m_RoadType;

			[ReadOnly]
			public ComponentTypeHandle<Game.Net.Pollution> m_PollutionType;

			[ReadOnly]
			public ComponentTypeHandle<EdgeGeometry> m_EdgeGeometryType;

			[ReadOnly]
			public BufferTypeHandle<Game.Net.ServiceCoverage> m_ServiceCoverageType;

			[ReadOnly]
			public BufferTypeHandle<ResourceAvailability> m_ResourceAvailabilityType;

			[ReadOnly]
			public ComponentLookup<LandValue> m_LandValues;

			[ReadOnly]
			public ComponentLookup<Edge> m_Edges;

			[ReadOnly]
			public ComponentLookup<Node> m_Nodes;

			[ReadOnly]
			public ComponentLookup<Temp> m_Temps;

			[ReadOnly]
			public ComponentLookup<ResourceData> m_ResourceDatas;

			[ReadOnly]
			public ComponentLookup<ZonePropertiesData> m_ZonePropertiesDatas;

			[ReadOnly]
			public BufferLookup<Game.Net.ServiceCoverage> m_ServiceCoverageData;

			[ReadOnly]
			public BufferLookup<ResourceAvailability> m_ResourceAvailabilityData;

			[ReadOnly]
			public BufferLookup<ProcessEstimate> m_ProcessEstimates;

			[ReadOnly]
			public ComponentTypeHandle<Edge> m_EdgeType;

			[ReadOnly]
			public ComponentTypeHandle<Temp> m_TempType;

			public ComponentTypeHandle<EdgeColor> m_ColorType;

			[ReadOnly]
			public Entity m_ZonePrefab;

			[ReadOnly]
			public ResourcePrefabs m_ResourcePrefabs;

			[ReadOnly]
			public NativeArray<GroundPollution> m_PollutionMap;

			[ReadOnly]
			public NativeArray<int> m_IndustrialDemands;

			[ReadOnly]
			public NativeArray<int> m_StorageDemands;

			[ReadOnly]
			public NativeList<IndustrialProcessData> m_Processes;

			[ReadOnly]
			public ZonePreferenceData m_ZonePreferences;

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				NativeArray<EdgeColor> nativeArray = chunk.GetNativeArray(ref this.m_ColorType);
				InfoviewAvailabilityData availabilityData;
				InfomodeActive activeData2;
				InfoviewNetStatusData statusData;
				InfomodeActive activeData3;
				int index;
				if (chunk.Has(ref this.m_ServiceCoverageType) && this.GetServiceCoverageData(chunk, out var coverageData, out var activeData))
				{
					NativeArray<Temp> nativeArray2 = chunk.GetNativeArray(ref this.m_TempType);
					BufferAccessor<Game.Net.ServiceCoverage> bufferAccessor = chunk.GetBufferAccessor(ref this.m_ServiceCoverageType);
					EdgeColor value2 = default(EdgeColor);
					for (int i = 0; i < nativeArray.Length; i++)
					{
						DynamicBuffer<Game.Net.ServiceCoverage> dynamicBuffer = bufferAccessor[i];
						if (CollectionUtils.TryGet(nativeArray2, i, out var value) && this.m_ServiceCoverageData.TryGetBuffer(value.m_Original, out var bufferData))
						{
							dynamicBuffer = bufferData;
						}
						if (dynamicBuffer.Length == 0)
						{
							nativeArray[i] = default(EdgeColor);
							continue;
						}
						Game.Net.ServiceCoverage serviceCoverage = dynamicBuffer[(int)coverageData.m_Service];
						value2.m_Index = (byte)activeData.m_Index;
						value2.m_Value0 = (byte)math.clamp(Mathf.RoundToInt(InfoviewUtils.GetColor(coverageData, serviceCoverage.m_Coverage.x) * 255f), 0, 255);
						value2.m_Value1 = (byte)math.clamp(Mathf.RoundToInt(InfoviewUtils.GetColor(coverageData, serviceCoverage.m_Coverage.y) * 255f), 0, 255);
						nativeArray[i] = value2;
					}
				}
				else if (chunk.Has(ref this.m_ResourceAvailabilityType) && this.GetResourceAvailabilityData(chunk, out availabilityData, out activeData2))
				{
					ZonePreferenceData preferences = this.m_ZonePreferences;
					NativeArray<Edge> nativeArray3 = chunk.GetNativeArray(ref this.m_EdgeType);
					NativeArray<Temp> nativeArray4 = chunk.GetNativeArray(ref this.m_TempType);
					BufferAccessor<ResourceAvailability> bufferAccessor2 = chunk.GetBufferAccessor(ref this.m_ResourceAvailabilityType);
					EdgeColor value4 = default(EdgeColor);
					for (int j = 0; j < nativeArray.Length; j++)
					{
						Edge edge = nativeArray3[j];
						DynamicBuffer<ResourceAvailability> availabilityBuffer = bufferAccessor2[j];
						float num;
						float num2;
						if (CollectionUtils.TryGet(nativeArray4, j, out var value3))
						{
							if (!this.m_Edges.TryGetComponent(value3.m_Original, out var componentData))
							{
								num = ((!this.m_Temps.TryGetComponent(edge.m_Start, out var componentData2) || !this.m_LandValues.TryGetComponent(componentData2.m_Original, out var componentData3)) ? this.m_LandValues[edge.m_Start].m_LandValue : componentData3.m_LandValue);
								num2 = ((!this.m_Temps.TryGetComponent(edge.m_End, out var componentData4) || !this.m_LandValues.TryGetComponent(componentData4.m_Original, out var componentData5)) ? this.m_LandValues[edge.m_End].m_LandValue : componentData5.m_LandValue);
							}
							else
							{
								edge = componentData;
								num = this.m_LandValues[componentData.m_Start].m_LandValue;
								num2 = this.m_LandValues[componentData.m_End].m_LandValue;
								if (this.m_ResourceAvailabilityData.TryGetBuffer(value3.m_Original, out var bufferData2))
								{
									availabilityBuffer = bufferData2;
								}
							}
						}
						else
						{
							num = this.m_LandValues[edge.m_Start].m_LandValue;
							num2 = this.m_LandValues[edge.m_End].m_LandValue;
						}
						if (availabilityBuffer.Length == 0)
						{
							nativeArray[j] = default(EdgeColor);
							continue;
						}
						float3 position = this.m_Nodes[edge.m_Start].m_Position;
						float3 position2 = this.m_Nodes[edge.m_End].m_Position;
						GroundPollution pollution = GroundPollutionSystemRe.GetPollution(position, this.m_PollutionMap);
						GroundPollution pollution2 = GroundPollutionSystemRe.GetPollution(position2, this.m_PollutionMap);
						float pollution3 = pollution.m_Pollution;
						float pollution4 = pollution2.m_Pollution;
						this.m_ProcessEstimates.TryGetBuffer(this.m_ZonePrefab, out var bufferData3);
						if (this.m_ZonePropertiesDatas.TryGetComponent(this.m_ZonePrefab, out var componentData6))
						{
							float num3 = ((availabilityData.m_AreaType != AreaType.Residential) ? componentData6.m_SpaceMultiplier : (componentData6.m_ScaleResidentials ? componentData6.m_ResidentialProperties : (componentData6.m_ResidentialProperties / 8f)));
							num /= num3;
							num2 /= num3;
						}
						value4.m_Index = (byte)activeData2.m_Index;
						value4.m_Value0 = (byte)math.clamp(Mathf.RoundToInt(InfoviewUtils.GetColor(availabilityData, availabilityBuffer, 0f, ref preferences, this.m_IndustrialDemands, this.m_StorageDemands, pollution3, num, bufferData3, this.m_Processes, this.m_ResourcePrefabs, this.m_ResourceDatas) * 255f), 0, 255);
						value4.m_Value1 = (byte)math.clamp(Mathf.RoundToInt(InfoviewUtils.GetColor(availabilityData, availabilityBuffer, 1f, ref preferences, this.m_IndustrialDemands, this.m_StorageDemands, pollution4, num2, bufferData3, this.m_Processes, this.m_ResourcePrefabs, this.m_ResourceDatas) * 255f), 0, 255);
						nativeArray[j] = value4;
					}
				}
				else if (this.GetNetStatusType(chunk, out statusData, out activeData3))
				{
					this.GetNetStatusColors(nativeArray, chunk, statusData, activeData3);
				}
				else if (this.GetNetGeometryColor(chunk, out index))
				{
					for (int k = 0; k < nativeArray.Length; k++)
					{
						nativeArray[k] = new EdgeColor((byte)index, 0, 0);
					}
				}
				else
				{
					for (int l = 0; l < nativeArray.Length; l++)
					{
						nativeArray[l] = new EdgeColor(0, byte.MaxValue, byte.MaxValue);
					}
				}
			}

			private bool GetServiceCoverageData(ArchetypeChunk chunk, out InfoviewCoverageData coverageData, out InfomodeActive activeData)
			{
				coverageData = default(InfoviewCoverageData);
				activeData = default(InfomodeActive);
				int num = int.MaxValue;
				for (int i = 0; i < this.m_InfomodeChunks.Length; i++)
				{
					ArchetypeChunk archetypeChunk = this.m_InfomodeChunks[i];
					NativeArray<InfoviewCoverageData> nativeArray = archetypeChunk.GetNativeArray(ref this.m_InfoviewCoverageType);
					if (nativeArray.Length == 0)
					{
						continue;
					}
					NativeArray<InfomodeActive> nativeArray2 = archetypeChunk.GetNativeArray(ref this.m_InfomodeActiveType);
					for (int j = 0; j < nativeArray.Length; j++)
					{
						InfomodeActive infomodeActive = nativeArray2[j];
						int priority = infomodeActive.m_Priority;
						if (priority < num)
						{
							coverageData = nativeArray[j];
							coverageData.m_Service = CoverageService.Count;
							activeData = infomodeActive;
							num = priority;
						}
					}
				}
				return num != int.MaxValue;
			}

			private bool GetResourceAvailabilityData(ArchetypeChunk chunk, out InfoviewAvailabilityData availabilityData, out InfomodeActive activeData)
			{
				availabilityData = default(InfoviewAvailabilityData);
				activeData = default(InfomodeActive);
				int num = int.MaxValue;
				for (int i = 0; i < this.m_InfomodeChunks.Length; i++)
				{
					ArchetypeChunk archetypeChunk = this.m_InfomodeChunks[i];
					NativeArray<InfoviewAvailabilityData> nativeArray = archetypeChunk.GetNativeArray(ref this.m_InfoviewAvailabilityType);
					if (nativeArray.Length == 0)
					{
						continue;
					}
					NativeArray<InfomodeActive> nativeArray2 = archetypeChunk.GetNativeArray(ref this.m_InfomodeActiveType);
					for (int j = 0; j < nativeArray.Length; j++)
					{
						InfomodeActive infomodeActive = nativeArray2[j];
						int priority = infomodeActive.m_Priority;
						if (priority < num)
						{
							availabilityData = nativeArray[j];
							activeData = infomodeActive;
							num = priority;
						}
					}
				}
				return num != int.MaxValue;
			}

			private bool GetNetStatusType(ArchetypeChunk chunk, out InfoviewNetStatusData statusData, out InfomodeActive activeData)
			{
				statusData = default(InfoviewNetStatusData);
				activeData = default(InfomodeActive);
				int num = int.MaxValue;
				for (int i = 0; i < this.m_InfomodeChunks.Length; i++)
				{
					ArchetypeChunk archetypeChunk = this.m_InfomodeChunks[i];
					NativeArray<InfoviewNetStatusData> nativeArray = archetypeChunk.GetNativeArray(ref this.m_InfoviewNetStatusType);
					if (nativeArray.Length == 0)
					{
						continue;
					}
					NativeArray<InfomodeActive> nativeArray2 = archetypeChunk.GetNativeArray(ref this.m_InfomodeActiveType);
					for (int j = 0; j < nativeArray.Length; j++)
					{
						InfomodeActive infomodeActive = nativeArray2[j];
						int priority = infomodeActive.m_Priority;
						if (priority < num)
						{
							InfoviewNetStatusData infoviewNetStatusData = nativeArray[j];
							if (this.HasNetStatus(nativeArray[j], chunk))
							{
								statusData = infoviewNetStatusData;
								activeData = infomodeActive;
								num = priority;
							}
						}
					}
				}
				return num != int.MaxValue;
			}

			private bool HasNetStatus(InfoviewNetStatusData infoviewNetStatusData, ArchetypeChunk chunk)
			{
				return infoviewNetStatusData.m_Type switch
				{
					NetStatusType.Wear => chunk.Has(ref this.m_NetConditionType), 
					NetStatusType.TrafficFlow => chunk.Has(ref this.m_RoadType), 
					NetStatusType.NoisePollutionSource => chunk.Has(ref this.m_PollutionType), 
					NetStatusType.AirPollutionSource => chunk.Has(ref this.m_PollutionType), 
					NetStatusType.TrafficVolume => chunk.Has(ref this.m_RoadType), 
					_ => false, 
				};
			}

			private void GetNetStatusColors(NativeArray<EdgeColor> results, ArchetypeChunk chunk, InfoviewNetStatusData statusData, InfomodeActive activeData)
			{
				switch (statusData.m_Type)
				{
				case NetStatusType.Wear:
				{
					NativeArray<NetCondition> nativeArray2 = chunk.GetNativeArray(ref this.m_NetConditionType);
					EdgeColor value2 = default(EdgeColor);
					for (int j = 0; j < nativeArray2.Length; j++)
					{
						NetCondition netCondition = nativeArray2[j];
						value2.m_Index = (byte)activeData.m_Index;
						value2.m_Value0 = (byte)math.clamp(Mathf.RoundToInt(InfoviewUtils.GetColor(statusData, netCondition.m_Wear.x / 10f) * 255f), 0, 255);
						value2.m_Value1 = (byte)math.clamp(Mathf.RoundToInt(InfoviewUtils.GetColor(statusData, netCondition.m_Wear.y / 10f) * 255f), 0, 255);
						results[j] = value2;
					}
					break;
				}
				case NetStatusType.TrafficFlow:
				{
					NativeArray<Road> nativeArray5 = chunk.GetNativeArray(ref this.m_RoadType);
					EdgeColor value4 = default(EdgeColor);
					for (int l = 0; l < nativeArray5.Length; l++)
					{
						Road road2 = nativeArray5[l];
						float4 trafficFlowSpeed = NetUtils.GetTrafficFlowSpeed(road2.m_TrafficFlowDuration0, road2.m_TrafficFlowDistance0);
						float4 trafficFlowSpeed2 = NetUtils.GetTrafficFlowSpeed(road2.m_TrafficFlowDuration1, road2.m_TrafficFlowDistance1);
						value4.m_Index = (byte)activeData.m_Index;
						value4.m_Value0 = (byte)math.clamp(Mathf.RoundToInt(InfoviewUtils.GetColor(statusData, math.csum(trafficFlowSpeed) * 0.125f + math.cmin(trafficFlowSpeed) * 0.5f) * 255f), 0, 255);
						value4.m_Value1 = (byte)math.clamp(Mathf.RoundToInt(InfoviewUtils.GetColor(statusData, math.csum(trafficFlowSpeed2) * 0.125f + math.cmin(trafficFlowSpeed2) * 0.5f) * 255f), 0, 255);
						results[l] = value4;
					}
					break;
				}
				case NetStatusType.NoisePollutionSource:
				{
					NativeArray<Game.Net.Pollution> nativeArray6 = chunk.GetNativeArray(ref this.m_PollutionType);
					NativeArray<EdgeGeometry> nativeArray7 = chunk.GetNativeArray(ref this.m_EdgeGeometryType);
					EdgeColor value5 = default(EdgeColor);
					for (int m = 0; m < nativeArray6.Length; m++)
					{
						float status2 = nativeArray6[m].m_Accumulation.x / math.max(0.1f, nativeArray7[m].m_Start.middleLength + nativeArray7[m].m_End.middleLength);
						value5.m_Index = (byte)activeData.m_Index;
						value5.m_Value0 = (byte)math.clamp(Mathf.RoundToInt(InfoviewUtils.GetColor(statusData, status2) * 255f), 0, 255);
						value5.m_Value1 = value5.m_Value0;
						results[m] = value5;
					}
					break;
				}
				case NetStatusType.AirPollutionSource:
				{
					NativeArray<Game.Net.Pollution> nativeArray3 = chunk.GetNativeArray(ref this.m_PollutionType);
					NativeArray<EdgeGeometry> nativeArray4 = chunk.GetNativeArray(ref this.m_EdgeGeometryType);
					EdgeColor value3 = default(EdgeColor);
					for (int k = 0; k < nativeArray3.Length; k++)
					{
						float status = nativeArray3[k].m_Accumulation.y / math.max(0.1f, nativeArray4[k].m_Start.middleLength + nativeArray4[k].m_End.middleLength);
						value3.m_Index = (byte)activeData.m_Index;
						value3.m_Value0 = (byte)math.clamp(Mathf.RoundToInt(InfoviewUtils.GetColor(statusData, status) * 255f), 0, 255);
						value3.m_Value1 = value3.m_Value0;
						results[k] = value3;
					}
					break;
				}
				case NetStatusType.TrafficVolume:
				{
					NativeArray<Road> nativeArray = chunk.GetNativeArray(ref this.m_RoadType);
					EdgeColor value = default(EdgeColor);
					for (int i = 0; i < nativeArray.Length; i++)
					{
						Road road = nativeArray[i];
						float4 x = math.sqrt(road.m_TrafficFlowDistance0 * 5.33333349f);
						float4 x2 = math.sqrt(road.m_TrafficFlowDistance1 * 5.33333349f);
						value.m_Index = (byte)activeData.m_Index;
						value.m_Value0 = (byte)math.clamp(Mathf.RoundToInt(InfoviewUtils.GetColor(statusData, math.csum(x) * 0.25f) * 255f), 0, 255);
						value.m_Value1 = (byte)math.clamp(Mathf.RoundToInt(InfoviewUtils.GetColor(statusData, math.csum(x2) * 0.25f) * 255f), 0, 255);
						results[i] = value;
					}
					break;
				}
				}
			}

			private bool GetNetGeometryColor(ArchetypeChunk chunk, out int index)
			{
				index = 0;
				int num = int.MaxValue;
				for (int i = 0; i < this.m_InfomodeChunks.Length; i++)
				{
					ArchetypeChunk archetypeChunk = this.m_InfomodeChunks[i];
					NativeArray<InfoviewNetGeometryData> nativeArray = archetypeChunk.GetNativeArray(ref this.m_InfoviewNetGeometryType);
					if (nativeArray.Length == 0)
					{
						continue;
					}
					NativeArray<InfomodeActive> nativeArray2 = archetypeChunk.GetNativeArray(ref this.m_InfomodeActiveType);
					for (int j = 0; j < nativeArray.Length; j++)
					{
						InfomodeActive infomodeActive = nativeArray2[j];
						int priority = infomodeActive.m_Priority;
						if (priority < num && this.HasNetGeometryColor(nativeArray[j], chunk))
						{
							index = infomodeActive.m_Index;
							num = priority;
						}
					}
				}
				return num != int.MaxValue;
			}

			private bool HasNetGeometryColor(InfoviewNetGeometryData infoviewNetGeometryData, ArchetypeChunk chunk)
			{
				return infoviewNetGeometryData.m_Type switch
				{
					NetType.TrainTrack => chunk.Has(ref this.m_TrainTrackType), 
					NetType.TramTrack => chunk.Has(ref this.m_TramTrackType), 
					NetType.Waterway => chunk.Has(ref this.m_WaterwayType), 
					NetType.SubwayTrack => chunk.Has(ref this.m_SubwayTrackType), 
					NetType.Road => chunk.Has(ref this.m_RoadType), 
					_ => false, 
				};
			}

			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}
		}

    }
}
