using System.Runtime.CompilerServices;
using Colossal.Collections;
using Game.Buildings;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.UI.Localization;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using Game.UI.Tooltip;

namespace MapExt2.Systems
{
	[CompilerGenerated]
	public class TempWaterPumpingTooltipSystem : TooltipSystemBase
	{
		private struct TempResult
		{
			public AllowedWaterTypes m_Types;

			public int m_Production;

			public int m_MaxCapacity;
		}

		[BurstCompile]
		private struct TempJob : IJobChunk
		{
			[ReadOnly]
			public ComponentTypeHandle<PrefabRef> m_PrefabType;

			[ReadOnly]
			public ComponentTypeHandle<Temp> m_TempType;

			[ReadOnly]
			public ComponentTypeHandle<Game.Objects.Transform> m_TransformType;

			[ReadOnly]
			public BufferTypeHandle<Game.Objects.SubObject> m_SubObjectType;

			[ReadOnly]
			public BufferTypeHandle<InstalledUpgrade> m_InstalledUpgradeType;

			[ReadOnly]
			public ComponentLookup<PrefabRef> m_Prefabs;

			[ReadOnly]
			public ComponentLookup<WaterPumpingStationData> m_PumpDatas;

			[ReadOnly]
			public ComponentLookup<Game.Objects.Transform> m_Transforms;

			[ReadOnly]
			public ComponentLookup<Game.Simulation.WaterSourceData> m_WaterSources;

			[ReadOnly]
			public NativeArray<GroundWater> m_GroundWaterMap;

			[ReadOnly]
			public WaterSurfaceData m_WaterSurfaceData;

			[ReadOnly]
			public TerrainHeightData m_TerrainHeightData;

			public NativeReference<TempResult> m_Result;

			public WaterPipeParameterData m_Parameters;

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				ref TempResult reference = ref this.m_Result.ValueAsRef();
				NativeArray<PrefabRef> nativeArray = chunk.GetNativeArray(ref this.m_PrefabType);
				NativeArray<Temp> nativeArray2 = chunk.GetNativeArray(ref this.m_TempType);
				NativeArray<Game.Objects.Transform> nativeArray3 = chunk.GetNativeArray(ref this.m_TransformType);
				BufferAccessor<Game.Objects.SubObject> bufferAccessor = chunk.GetBufferAccessor(ref this.m_SubObjectType);
				BufferAccessor<InstalledUpgrade> bufferAccessor2 = chunk.GetBufferAccessor(ref this.m_InstalledUpgradeType);
				for (int i = 0; i < chunk.Count; i++)
				{
					if ((nativeArray2[i].m_Flags & (TempFlags.Create | TempFlags.Modify | TempFlags.Upgrade)) == 0)
					{
						continue;
					}
					this.m_PumpDatas.TryGetComponent(nativeArray[i].m_Prefab, out var componentData);
					if (bufferAccessor2.Length != 0)
					{
						UpgradeUtils.CombineStats(ref componentData, bufferAccessor2[i], ref this.m_Prefabs, ref this.m_PumpDatas);
					}
					int num = 0;
					if (componentData.m_Types != 0)
					{
						if ((componentData.m_Types & AllowedWaterTypes.Groundwater) != 0)
						{
							int num2 = Mathf.RoundToInt(math.clamp((float)GroundWaterSystem.GetGroundWater(nativeArray3[i].m_Position, this.m_GroundWaterMap).m_Max / this.m_Parameters.m_GroundwaterPumpEffectiveAmount, 0f, 1f) * (float)componentData.m_Capacity);
							num += num2;
						}
						if ((componentData.m_Types & AllowedWaterTypes.SurfaceWater) != 0 && bufferAccessor.Length != 0)
						{
							DynamicBuffer<Game.Objects.SubObject> dynamicBuffer = bufferAccessor[i];
							for (int j = 0; j < dynamicBuffer.Length; j++)
							{
								Entity subObject = dynamicBuffer[j].m_SubObject;
								if (this.m_WaterSources.HasComponent(subObject) && this.m_Transforms.TryGetComponent(subObject, out var componentData2))
								{
									float surfaceWaterAvailability = WaterPumpingStationAISystem.GetSurfaceWaterAvailability(componentData2.m_Position, componentData.m_Types, this.m_WaterSurfaceData, this.m_Parameters.m_SurfaceWaterPumpEffectiveDepth);
									num += Mathf.RoundToInt(surfaceWaterAvailability * (float)componentData.m_Capacity);
								}
							}
						}
					}
					else
					{
						num = componentData.m_Capacity;
					}
					reference.m_Types |= componentData.m_Types;
					reference.m_Production += math.min(num, componentData.m_Capacity);
					reference.m_MaxCapacity += componentData.m_Capacity;
				}
			}

			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}
		}

		[BurstCompile]
		private struct GroundWaterPumpJob : IJobChunk
		{
			[ReadOnly]
			public ComponentTypeHandle<PrefabRef> m_PrefabType;

			[ReadOnly]
			public ComponentTypeHandle<Temp> m_TempType;

			[ReadOnly]
			public ComponentTypeHandle<Game.Objects.Transform> m_TransformType;

			[ReadOnly]
			public BufferTypeHandle<InstalledUpgrade> m_InstalledUpgradeType;

			[ReadOnly]
			public ComponentLookup<PrefabRef> m_Prefabs;

			[ReadOnly]
			public ComponentLookup<WaterPumpingStationData> m_PumpDatas;

			[ReadOnly]
			public NativeArray<GroundWater> m_GroundWaterMap;

			public NativeParallelHashMap<int2, int> m_PumpCapacityMap;

			public NativeList<int2> m_TempGroundWaterPumpCells;

			public WaterPipeParameterData m_Parameters;

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				NativeArray<PrefabRef> nativeArray = chunk.GetNativeArray(ref this.m_PrefabType);
				NativeArray<Temp> nativeArray2 = chunk.GetNativeArray(ref this.m_TempType);
				NativeArray<Game.Objects.Transform> nativeArray3 = chunk.GetNativeArray(ref this.m_TransformType);
				BufferAccessor<InstalledUpgrade> bufferAccessor = chunk.GetBufferAccessor(ref this.m_InstalledUpgradeType);
				bool flag = nativeArray2.Length != 0;
				for (int i = 0; i < chunk.Count; i++)
				{
					if (flag && (nativeArray2[i].m_Flags & (TempFlags.Create | TempFlags.Modify | TempFlags.Upgrade)) == 0)
					{
						continue;
					}
					this.m_PumpDatas.TryGetComponent(nativeArray[i].m_Prefab, out var componentData);
					if (bufferAccessor.Length != 0)
					{
						UpgradeUtils.CombineStats(ref componentData, bufferAccessor[i], ref this.m_Prefabs, ref this.m_PumpDatas);
					}
					if ((componentData.m_Types & AllowedWaterTypes.Groundwater) != 0 && GroundWaterSystem.TryGetCell(nativeArray3[i].m_Position, out var cell))
					{
						int num = Mathf.CeilToInt(math.clamp((float)GroundWaterSystem.GetGroundWater(nativeArray3[i].m_Position, this.m_GroundWaterMap).m_Max / this.m_Parameters.m_GroundwaterPumpEffectiveAmount, 0f, 1f) * (float)componentData.m_Capacity);
						if (!this.m_PumpCapacityMap.ContainsKey(cell))
						{
							this.m_PumpCapacityMap.Add(cell, num);
						}
						else
						{
							this.m_PumpCapacityMap[cell] += num;
						}
						if (flag)
						{
							this.m_TempGroundWaterPumpCells.Add(in cell);
						}
					}
				}
			}

			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}
		}

		public struct GroundWaterReservoirResult
		{
			public int m_PumpCapacity;

			public int m_Volume;
		}

		[BurstCompile]
		public struct GroundWaterReservoirJob : IJob
		{
			[ReadOnly]
			public NativeArray<GroundWater> m_GroundWaterMap;

			[ReadOnly]
			public NativeParallelHashMap<int2, int> m_PumpCapacityMap;

			[ReadOnly]
			public NativeList<int2> m_TempGroundWaterPumpCells;

			public NativeQueue<int2> m_Queue;

			public NativeReference<GroundWaterReservoirResult> m_Result;

			public void Execute()
			{
				NativeParallelHashSet<int2> processedCells = new NativeParallelHashSet<int2>(128, Allocator.Temp);
				ref GroundWaterReservoirResult reference = ref this.m_Result.ValueAsRef();
				foreach (int2 tempGroundWaterPumpCell in this.m_TempGroundWaterPumpCells)
				{
					this.EnqueueIfUnprocessed(tempGroundWaterPumpCell, processedCells);
				}
				int2 item;
				while (this.m_Queue.TryDequeue(out item))
				{
					int index = item.x + item.y * GroundWaterSystem.kTextureSize;
					GroundWater groundWater = this.m_GroundWaterMap[index];
					if (this.m_PumpCapacityMap.TryGetValue(item, out var item2))
					{
						reference.m_PumpCapacity += item2;
					}
					if (groundWater.m_Max > 500)
					{
						reference.m_Volume += groundWater.m_Max;
						this.EnqueueIfUnprocessed(new int2(item.x - 1, item.y), processedCells);
						this.EnqueueIfUnprocessed(new int2(item.x + 1, item.y), processedCells);
						this.EnqueueIfUnprocessed(new int2(item.x, item.y - 1), processedCells);
						this.EnqueueIfUnprocessed(new int2(item.x, item.y + 2), processedCells);
					}
					else if (reference.m_Volume > 0)
					{
						reference.m_Volume += groundWater.m_Max;
					}
				}
			}

			private void EnqueueIfUnprocessed(int2 cell, NativeParallelHashSet<int2> processedCells)
			{
				if (GroundWaterSystem.IsValidCell(cell) && processedCells.Add(cell))
				{
					this.m_Queue.Enqueue(cell);
				}
			}
		}

		private struct TypeHandle
		{
			[ReadOnly]
			public ComponentTypeHandle<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;

			[ReadOnly]
			public ComponentTypeHandle<Temp> __Game_Tools_Temp_RO_ComponentTypeHandle;

			[ReadOnly]
			public ComponentTypeHandle<Game.Objects.Transform> __Game_Objects_Transform_RO_ComponentTypeHandle;

			[ReadOnly]
			public BufferTypeHandle<Game.Objects.SubObject> __Game_Objects_SubObject_RO_BufferTypeHandle;

			[ReadOnly]
			public BufferTypeHandle<InstalledUpgrade> __Game_Buildings_InstalledUpgrade_RO_BufferTypeHandle;

			[ReadOnly]
			public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<WaterPumpingStationData> __Game_Prefabs_WaterPumpingStationData_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<Game.Objects.Transform> __Game_Objects_Transform_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<Game.Simulation.WaterSourceData> __Game_Simulation_WaterSourceData_RO_ComponentLookup;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PrefabRef>(isReadOnly: true);
				this.__Game_Tools_Temp_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Temp>(isReadOnly: true);
				this.__Game_Objects_Transform_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Game.Objects.Transform>(isReadOnly: true);
				this.__Game_Objects_SubObject_RO_BufferTypeHandle = state.GetBufferTypeHandle<Game.Objects.SubObject>(isReadOnly: true);
				this.__Game_Buildings_InstalledUpgrade_RO_BufferTypeHandle = state.GetBufferTypeHandle<InstalledUpgrade>(isReadOnly: true);
				this.__Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(isReadOnly: true);
				this.__Game_Prefabs_WaterPumpingStationData_RO_ComponentLookup = state.GetComponentLookup<WaterPumpingStationData>(isReadOnly: true);
				this.__Game_Objects_Transform_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.Transform>(isReadOnly: true);
				this.__Game_Simulation_WaterSourceData_RO_ComponentLookup = state.GetComponentLookup<Game.Simulation.WaterSourceData>(isReadOnly: true);
			}
		}

		private GroundWaterSystem m_GroundWaterSystem;

		private WaterSystem m_WaterSystem;

		private TerrainSystem m_TerrainSystem;

		private EntityQuery m_ErrorQuery;

		private EntityQuery m_TempQuery;

		private EntityQuery m_PumpQuery;

		private EntityQuery m_ParameterQuery;

		private ProgressTooltip m_Capacity;

		private IntTooltip m_ReservoirUsage;

		private StringTooltip m_OverRefreshCapacityWarning;

		private StringTooltip m_AvailabilityWarning;

		private LocalizedString m_GroundWarning;

		private LocalizedString m_SurfaceWarning;

		private NativeReference<TempResult> m_TempResult;

		private NativeReference<GroundWaterReservoirResult> m_ReservoirResult;

		private TypeHandle __TypeHandle;

		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_GroundWaterSystem = base.World.GetOrCreateSystemManaged<GroundWaterSystem>();
			this.m_WaterSystem = base.World.GetOrCreateSystemManaged<WaterSystem>();
			this.m_TerrainSystem = base.World.GetOrCreateSystemManaged<TerrainSystem>();
			this.m_ErrorQuery = base.GetEntityQuery(ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Error>());
			this.m_TempQuery = base.GetEntityQuery(ComponentType.ReadOnly<Game.Buildings.WaterPumpingStation>(), ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<Game.Objects.Transform>(), ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadOnly<Temp>(), ComponentType.Exclude<Hidden>(), ComponentType.Exclude<Error>(), ComponentType.Exclude<Deleted>());
			this.m_PumpQuery = base.GetEntityQuery(ComponentType.ReadOnly<Game.Buildings.WaterPumpingStation>(), ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<Game.Objects.Transform>(), ComponentType.ReadOnly<PrefabRef>(), ComponentType.Exclude<Hidden>(), ComponentType.Exclude<Deleted>());
			this.m_ParameterQuery = base.GetEntityQuery(ComponentType.ReadOnly<WaterPipeParameterData>());
			this.m_Capacity = new ProgressTooltip
			{
				path = "groundWaterCapacity",
				icon = "Media/Game/Icons/Water.svg",
				label = LocalizedString.Id("Tools.WATER_OUTPUT_LABEL"),
				unit = "volume",
				omitMax = true
			};
			this.m_ReservoirUsage = new IntTooltip
			{
				path = "groundWaterReservoirUsage",
				label = LocalizedString.Id("Tools.GROUND_WATER_RESERVOIR_USAGE"),
				unit = "percentage"
			};
			this.m_OverRefreshCapacityWarning = new StringTooltip
			{
				path = "groundWaterOverRefreshCapacityWarning",
				value = LocalizedString.Id("Tools.WARNING[OverRefreshCapacity]"),
				color = TooltipColor.Warning
			};
			this.m_AvailabilityWarning = new StringTooltip
			{
				path = "waterAvailabilityWarning",
				color = TooltipColor.Warning
			};
			this.m_GroundWarning = LocalizedString.Id("Tools.WARNING[NotEnoughGroundWater]");
			this.m_SurfaceWarning = LocalizedString.Id("Tools.WARNING[NotEnoughFreshWater]");
			this.m_TempResult = new NativeReference<TempResult>(Allocator.Persistent);
			this.m_ReservoirResult = new NativeReference<GroundWaterReservoirResult>(Allocator.Persistent);
		}

		[Preserve]
		protected override void OnDestroy()
		{
			this.m_TempResult.Dispose();
			this.m_ReservoirResult.Dispose();
			base.OnDestroy();
		}

		[Preserve]
		protected override void OnUpdate()
		{
			if (!this.m_ErrorQuery.IsEmptyIgnoreFilter || this.m_TempQuery.IsEmptyIgnoreFilter)
			{
				this.m_TempResult.Value = default(TempResult);
				this.m_ReservoirResult.Value = default(GroundWaterReservoirResult);
				return;
			}
			this.ProcessResults();
			this.m_TempResult.Value = default(TempResult);
			this.m_ReservoirResult.Value = default(GroundWaterReservoirResult);
			JobHandle dependencies;
			NativeArray<GroundWater> map = this.m_GroundWaterSystem.GetMap(readOnly: true, out dependencies);
			WaterPipeParameterData singleton = this.m_ParameterQuery.GetSingleton<WaterPipeParameterData>();
			this.__TypeHandle.__Game_Simulation_WaterSourceData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_WaterPumpingStationData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Buildings_InstalledUpgrade_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Objects_SubObject_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Tools_Temp_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			TempJob jobData = default(TempJob);
			jobData.m_PrefabType = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;
			jobData.m_TempType = this.__TypeHandle.__Game_Tools_Temp_RO_ComponentTypeHandle;
			jobData.m_TransformType = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle;
			jobData.m_SubObjectType = this.__TypeHandle.__Game_Objects_SubObject_RO_BufferTypeHandle;
			jobData.m_InstalledUpgradeType = this.__TypeHandle.__Game_Buildings_InstalledUpgrade_RO_BufferTypeHandle;
			jobData.m_Prefabs = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup;
			jobData.m_PumpDatas = this.__TypeHandle.__Game_Prefabs_WaterPumpingStationData_RO_ComponentLookup;
			jobData.m_Transforms = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup;
			jobData.m_WaterSources = this.__TypeHandle.__Game_Simulation_WaterSourceData_RO_ComponentLookup;
			jobData.m_GroundWaterMap = map;
			jobData.m_WaterSurfaceData = this.m_WaterSystem.GetSurfaceData(out var deps);
			jobData.m_TerrainHeightData = this.m_TerrainSystem.GetHeightData();
			jobData.m_Result = this.m_TempResult;
			jobData.m_Parameters = singleton;
			JobHandle jobHandle = JobChunkExtensions.Schedule(jobData, this.m_TempQuery, JobHandle.CombineDependencies(base.Dependency, dependencies, deps));
			this.m_WaterSystem.AddSurfaceReader(jobHandle);
			this.m_TerrainSystem.AddCPUHeightReader(jobHandle);
			NativeParallelHashMap<int2, int> pumpCapacityMap = new NativeParallelHashMap<int2, int>(8, Allocator.TempJob);
			NativeList<int2> tempGroundWaterPumpCells = new NativeList<int2>(Allocator.TempJob);
			this.__TypeHandle.__Game_Prefabs_WaterPumpingStationData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Buildings_InstalledUpgrade_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Tools_Temp_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			GroundWaterPumpJob jobData2 = default(GroundWaterPumpJob);
			jobData2.m_PrefabType = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;
			jobData2.m_TempType = this.__TypeHandle.__Game_Tools_Temp_RO_ComponentTypeHandle;
			jobData2.m_TransformType = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle;
			jobData2.m_InstalledUpgradeType = this.__TypeHandle.__Game_Buildings_InstalledUpgrade_RO_BufferTypeHandle;
			jobData2.m_Prefabs = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup;
			jobData2.m_PumpDatas = this.__TypeHandle.__Game_Prefabs_WaterPumpingStationData_RO_ComponentLookup;
			jobData2.m_GroundWaterMap = map;
			jobData2.m_PumpCapacityMap = pumpCapacityMap;
			jobData2.m_TempGroundWaterPumpCells = tempGroundWaterPumpCells;
			jobData2.m_Parameters = singleton;
			JobHandle dependsOn = JobChunkExtensions.Schedule(jobData2, this.m_PumpQuery, JobHandle.CombineDependencies(base.Dependency, dependencies));
			GroundWaterReservoirJob groundWaterReservoirJob = default(GroundWaterReservoirJob);
			groundWaterReservoirJob.m_GroundWaterMap = map;
			groundWaterReservoirJob.m_PumpCapacityMap = pumpCapacityMap;
			groundWaterReservoirJob.m_TempGroundWaterPumpCells = tempGroundWaterPumpCells;
			groundWaterReservoirJob.m_Queue = new NativeQueue<int2>(Allocator.TempJob);
			groundWaterReservoirJob.m_Result = this.m_ReservoirResult;
			GroundWaterReservoirJob jobData3 = groundWaterReservoirJob;
			JobHandle jobHandle2 = IJobExtensions.Schedule(jobData3, dependsOn);
			jobData3.m_Queue.Dispose(jobHandle2);
			pumpCapacityMap.Dispose(jobHandle2);
			tempGroundWaterPumpCells.Dispose(jobHandle2);
			base.Dependency = JobHandle.CombineDependencies(jobHandle, jobHandle2);
			this.m_GroundWaterSystem.AddReader(base.Dependency);
		}

		private void ProcessResults()
		{
			TempResult value = this.m_TempResult.Value;
			GroundWaterReservoirResult value2 = this.m_ReservoirResult.Value;
			if (value.m_MaxCapacity <= 0)
			{
				return;
			}
			if ((value.m_Types & AllowedWaterTypes.Groundwater) != 0)
			{
				this.ProcessProduction(value);
				if (value2.m_Volume > 0)
				{
					this.ProcessReservoir(value2);
				}
				this.ProcessAvailabilityWarning(value, this.m_GroundWarning);
			}
			else if ((value.m_Types & AllowedWaterTypes.SurfaceWater) != 0)
			{
				this.ProcessProduction(value);
				this.ProcessAvailabilityWarning(value, this.m_SurfaceWarning);
			}
			else
			{
				this.ProcessProduction(value);
			}
		}

		private void ProcessReservoir(GroundWaterReservoirResult reservoir)
		{
			WaterPipeParameterData singleton = this.m_ParameterQuery.GetSingleton<WaterPipeParameterData>();
			float num = singleton.m_GroundwaterReplenish / singleton.m_GroundwaterUsageMultiplier * (float)reservoir.m_Volume;
			float num2 = ((num > 0f && reservoir.m_PumpCapacity > 0) ? math.clamp(100f * (float)reservoir.m_PumpCapacity / num, 1f, 999f) : 0f);
			this.m_ReservoirUsage.value = Mathf.RoundToInt(num2);
			this.m_ReservoirUsage.color = ((num2 > 100f) ? TooltipColor.Warning : TooltipColor.Info);
			base.AddMouseTooltip(this.m_ReservoirUsage);
			if (num2 > 100f)
			{
				base.AddMouseTooltip(this.m_OverRefreshCapacityWarning);
			}
		}

		private void ProcessProduction(TempResult temp)
		{
			if (temp.m_Production > 0)
			{
				this.m_Capacity.value = temp.m_Production;
				this.m_Capacity.max = temp.m_MaxCapacity;
				ProgressTooltip.SetCapacityColor(this.m_Capacity);
				base.AddMouseTooltip(this.m_Capacity);
			}
		}

		private void ProcessAvailabilityWarning(TempResult temp, LocalizedString warningText)
		{
			if (temp.m_Production > 0 && (float)temp.m_Production < (float)temp.m_MaxCapacity * 0.75f)
			{
				this.m_AvailabilityWarning.value = warningText;
				base.AddMouseTooltip(this.m_AvailabilityWarning);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void __AssignQueries(ref SystemState state)
		{
		}

		protected override void OnCreateForCompiler()
		{
			base.OnCreateForCompiler();
			this.__AssignQueries(ref base.CheckedStateRef);
			this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
		}

		[Preserve]
		public TempWaterPumpingTooltipSystem()
		{
		}
	}
}
