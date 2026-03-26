using System;
using HarmonyLib;
using Game;
using Game.Simulation;
using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Serialization.Entities;
using Game.Areas;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Game.Tools;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace EconomyEX.Systems
{
	public partial class ServiceCoverageSystemMod : GameSystemBase
	{
		#region Constants

		public const uint COVERAGE_UPDATE_INTERVAL = 256u;

		#endregion

		#region Fields

		private SimulationSystem m_SimulationSystem;
		private PathfindQueueSystem m_PathfindQueueSystem;
		private AirwaySystem m_AirwaySystem;
		private EntityQuery m_EdgeQuery;
		private EntityQuery m_BuildingQuery;
		private PathfindTargetSeekerData m_TargetSeekerData;
		private NativeQueue<QueueItem> m_PendingCoverages;
		private CoverageService m_LastCoverageService;

		#endregion

		#region Lifecycle

		protected override void OnCreate()
		{
			base.OnCreate();
			m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
			m_PathfindQueueSystem = base.World.GetOrCreateSystemManaged<PathfindQueueSystem>();
			m_AirwaySystem = base.World.GetOrCreateSystemManaged<AirwaySystem>();
			m_EdgeQuery = GetEntityQuery(ComponentType.ReadOnly<Game.Net.Edge>(),
				ComponentType.ReadWrite<Game.Net.ServiceCoverage>(), ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Temp>());
			m_BuildingQuery = GetEntityQuery(ComponentType.ReadOnly<CoverageServiceType>(),
				ComponentType.ReadOnly<Game.Pathfind.CoverageElement>(), ComponentType.ReadOnly<PrefabRef>(),
				ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
			m_TargetSeekerData = new PathfindTargetSeekerData(this);
			m_PendingCoverages = new NativeQueue<QueueItem>(Allocator.Persistent);
			m_LastCoverageService = CoverageService.Count;
		}

		protected override void OnDestroy()
		{
			m_PendingCoverages.Dispose();
			base.OnDestroy();
		}

		protected override void OnGamePreload(Purpose purpose, GameMode mode)
		{
			base.OnGamePreload(purpose, mode);
			m_PendingCoverages.Clear();
			m_LastCoverageService = CoverageService.Count;
		}

		protected override void OnUpdate()
		{
			CoverageService frameService = GetFrameService(m_SimulationSystem.frameIndex);
			CoverageService frameService2 = GetFrameService(m_SimulationSystem.frameIndex + 1);
			if (frameService == frameService2)
			{
				if (EnqueuePendingCoverages(out var outputDeps))
				{
					base.Dependency = outputDeps;
				}

				return;
			}

			NativeArray<ArchetypeChunk> buildingChunks = m_BuildingQuery.ToArchetypeChunkArray(Allocator.TempJob);
			EntityTypeHandle entityTypeHandle = SystemAPI.GetEntityTypeHandle();
			BufferTypeHandle<Game.Net.ServiceCoverage> bufferTypeHandle =
				SystemAPI.GetBufferTypeHandle<Game.Net.ServiceCoverage>(isReadOnly: false);
			SharedComponentTypeHandle<CoverageServiceType> sharedComponentTypeHandle =
				SystemAPI.GetSharedComponentTypeHandle<CoverageServiceType>();
			uint queueFrame = m_SimulationSystem.frameIndex + 192;
			uint resultFrame = m_SimulationSystem.frameIndex + 256;
			for (int i = 0; i < buildingChunks.Length; i++)
			{
				ArchetypeChunk archetypeChunk = buildingChunks[i];
				if (archetypeChunk.GetSharedComponent(sharedComponentTypeHandle).m_Service == frameService2)
				{
					NativeArray<Entity> nativeArray = archetypeChunk.GetNativeArray(entityTypeHandle);
					for (int j = 0; j < nativeArray.Length; j++)
					{
						m_PendingCoverages.Enqueue(new QueueItem
						{
							m_Entity = nativeArray[j],
							m_QueueFrame = queueFrame,
							m_ResultFrame = resultFrame
						});
					}
				}
			}

			EnqueuePendingCoverages(out var outputDeps2);
			if (m_LastCoverageService != CoverageService.Count)
			{
				NativeList<BuildingData> nativeList = new NativeList<BuildingData>(Allocator.TempJob);
				NativeList<CoverageElement> elements = new NativeList<CoverageElement>(Allocator.TempJob);
				PrepareCoverageJob jobData = new PrepareCoverageJob
				{
					m_Service = frameService,
					m_BuildingChunks = buildingChunks,
					m_CoverageServiceType = sharedComponentTypeHandle,
					m_EntityType = entityTypeHandle,
					m_CoverageElementType =
						SystemAPI.GetBufferTypeHandle<Game.Pathfind.CoverageElement>(isReadOnly: true),
					m_BuildingData = nativeList,
					m_Elements = elements
				};
				ClearCoverageJob jobData2 = new ClearCoverageJob
				{
					m_CoverageIndex = (int)frameService,
					m_ServiceCoverageType = bufferTypeHandle
				};
				ProcessCoverageJob jobData3 = new ProcessCoverageJob
				{
					m_CoverageIndex = (int)frameService,
					m_BuildingData = nativeList,
					m_Elements = elements,
					m_OwnerData = SystemAPI.GetComponentLookup<Owner>(isReadOnly: true),
					m_CurveData = SystemAPI.GetComponentLookup<Curve>(isReadOnly: true),
					m_DensityData = SystemAPI.GetComponentLookup<Density>(isReadOnly: true),
					m_DestroyedData = SystemAPI.GetComponentLookup<Destroyed>(isReadOnly: true),
					m_TempData = SystemAPI.GetComponentLookup<Temp>(isReadOnly: true),
					m_BuildingComponentData = SystemAPI.GetComponentLookup<Building>(isReadOnly: true),
					m_ModifiedServiceCoverageData =
						SystemAPI.GetComponentLookup<ModifiedServiceCoverage>(isReadOnly: true),
					m_BorderDistrictData = SystemAPI.GetComponentLookup<BorderDistrict>(isReadOnly: true),
					m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
					m_PrefabCoverageData = SystemAPI.GetComponentLookup<CoverageData>(isReadOnly: true),
					m_CoverageElements = SystemAPI.GetBufferLookup<Game.Pathfind.CoverageElement>(isReadOnly: true),
					m_ServiceDistricts = SystemAPI.GetBufferLookup<ServiceDistrict>(isReadOnly: true),
					m_Efficiencies = SystemAPI.GetBufferLookup<Efficiency>(isReadOnly: true),
					m_CoverageData = SystemAPI.GetBufferLookup<Game.Net.ServiceCoverage>(isReadOnly: false)
				};
				ApplyCoverageJob jobData4 = new ApplyCoverageJob
				{
					m_BuildingData = nativeList,
					m_Elements = elements
				};
				JobHandle jobHandle = IJobExtensions.Schedule(jobData, base.Dependency);
				JobHandle dependsOn = IJobParallelForDeferExtensions.Schedule(
					dependsOn: JobHandle.CombineDependencies(jobHandle,
						JobChunkExtensions.ScheduleParallel(jobData2, m_EdgeQuery, base.Dependency)), jobData: jobData3,
					list: nativeList, innerloopBatchCount: 1);
				JobHandle jobHandle2 = IJobExtensions.Schedule(jobData4, dependsOn);
				buildingChunks.Dispose(jobHandle);
				nativeList.Dispose(jobHandle2);
				elements.Dispose(jobHandle2);
				outputDeps2 = JobHandle.CombineDependencies(outputDeps2, jobHandle2);
			}
			else
			{
				buildingChunks.Dispose();
			}

			m_LastCoverageService = frameService2;
			base.Dependency = outputDeps2;
		}

		private bool EnqueuePendingCoverages(out JobHandle outputDeps)
		{
			outputDeps = default(JobHandle);
			if (m_PendingCoverages.IsEmpty())
			{
				return false;
			}

			int count = m_PendingCoverages.Count;
			int batchSize = 192;
			int batchCount = (count + batchSize - 1) / batchSize;
			m_TargetSeekerData.Update(this, m_AirwaySystem.GetAirwayData());
			PathfindParameters pathfindParameters = new PathfindParameters
			{
				m_MaxSpeed = 111.111115f,
				m_WalkSpeed = 5.555556f,
				m_Weights = new PathfindWeights(1f, 1f, 1f, 1f),
				m_PathfindFlags = (PathfindFlags.Stable | PathfindFlags.IgnoreFlow),
				m_IgnoredRules = (RuleFlags.HasBlockage | RuleFlags.ForbidCombustionEngines |
				                  RuleFlags.ForbidTransitTraffic | RuleFlags.ForbidHeavyTraffic |
				                  RuleFlags.ForbidPrivateTraffic | RuleFlags.ForbidSlowTraffic |
				                  RuleFlags.AvoidBicycles)
			};
			SetupQueueTarget setupQueueTarget = default(SetupQueueTarget);
			SetupCoverageSearchJob jobData = new SetupCoverageSearchJob
			{
				m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
				m_PrefabCoverageData = SystemAPI.GetComponentLookup<CoverageData>(isReadOnly: true)
			};
			for (int i = 0; i < count; i++)
			{
				QueueItem queueItem = m_PendingCoverages.Peek();
				if (--batchCount < 0 && queueItem.m_QueueFrame > m_SimulationSystem.frameIndex)
				{
					break;
				}

				m_PendingCoverages.Dequeue();
				if (base.EntityManager.TryGetSharedComponent<CoverageServiceType>(queueItem.m_Entity,
					    out var component))
				{
					SetupPathfindMethods(component.m_Service, ref pathfindParameters, ref setupQueueTarget);
					CoverageAction action = new CoverageAction(Allocator.Persistent);
					jobData.m_Entity = queueItem.m_Entity;
					jobData.m_TargetSeeker = new PathfindTargetSeeker<PathfindTargetBuffer>(m_TargetSeekerData,
						pathfindParameters, setupQueueTarget, action.data.m_Sources.AsParallelWriter(),
						RandomSeed.Next(), isStartTarget: true);
					jobData.m_Action = action;
					JobHandle jobHandle = IJobExtensions.Schedule(jobData, base.Dependency);
					outputDeps = JobHandle.CombineDependencies(outputDeps, jobHandle);
					m_PathfindQueueSystem.Enqueue(action, queueItem.m_Entity, jobHandle, queueItem.m_ResultFrame, this);
				}
			}

			return true;
		}

		public static void SetupPathfindMethods(CoverageService service, ref PathfindParameters pathfindParameters,
			ref SetupQueueTarget setupQueueTarget)
		{
			switch (service)
			{
				case CoverageService.PostService:
				case CoverageService.Education:
				case CoverageService.EmergencyShelter:
				case CoverageService.Welfare:
					// [MOD EXT] 放开纯步行网格限制，允许通过道路扩散计算覆盖范围
					// 原版强制 Pedestrian-only 在大地图上会导致 A* 沿人行道极度密集地扩散，严重消耗算力
					pathfindParameters.m_Methods = PathMethod.Road | PathMethod.Pedestrian;
					setupQueueTarget.m_Methods = PathMethod.Road | PathMethod.Pedestrian;
					setupQueueTarget.m_RoadTypes = RoadTypes.Car;
					break;
				case CoverageService.Park:
					// [MOD EXT] 公园覆盖：同样放开路网限制，且移除所有活动点 ActivityMask
					// 原版从每一把长椅、每一个健身器材发起 A* 扩散，在大型公园中产生数十个搜索源点
					// 改为统一从公园建筑基础节点发射，与学校/医院等一致的扩散模式
					pathfindParameters.m_Methods = PathMethod.Road | PathMethod.Pedestrian;
					setupQueueTarget.m_Methods = PathMethod.Road | PathMethod.Pedestrian;
					setupQueueTarget.m_RoadTypes = RoadTypes.Car;
					break;
				default:
					pathfindParameters.m_Methods = PathMethod.Road;
					setupQueueTarget.m_Methods = PathMethod.Road;
					setupQueueTarget.m_RoadTypes = RoadTypes.Car;
					break;
			}
		}

		private static CoverageService GetFrameService(uint frame)
		{
			return (CoverageService)(frame % 256 * 8 / 256);
		}

		#endregion

		#region Jobs

		[BurstCompile]
		public struct ClearCoverageJob : IJobChunk
		{
			[ReadOnly] public int m_CoverageIndex;
			public BufferTypeHandle<Game.Net.ServiceCoverage> m_ServiceCoverageType;

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
				in v128 chunkEnabledMask)
			{
				BufferAccessor<Game.Net.ServiceCoverage> bufferAccessor =
					chunk.GetBufferAccessor(ref m_ServiceCoverageType);
				for (int i = 0; i < bufferAccessor.Length; i++)
				{
					DynamicBuffer<Game.Net.ServiceCoverage> dynamicBuffer = bufferAccessor[i];
					dynamicBuffer[m_CoverageIndex] = default(Game.Net.ServiceCoverage);
				}
			}
		}

		[BurstCompile]
		public struct PrepareCoverageJob : IJob
		{
			[ReadOnly] public CoverageService m_Service;
			[ReadOnly] public NativeArray<ArchetypeChunk> m_BuildingChunks;

			[ReadOnly] public SharedComponentTypeHandle<CoverageServiceType> m_CoverageServiceType;
			[ReadOnly] public EntityTypeHandle m_EntityType;

			[ReadOnly] public BufferTypeHandle<Game.Pathfind.CoverageElement> m_CoverageElementType;
			public NativeList<BuildingData> m_BuildingData;

			public NativeList<CoverageElement> m_Elements;

			public void Execute()
			{
				BuildingData value = default(BuildingData);
				for (int i = 0; i < m_BuildingChunks.Length; i++)
				{
					ArchetypeChunk archetypeChunk = m_BuildingChunks[i];
					if (archetypeChunk.GetSharedComponent(m_CoverageServiceType).m_Service != m_Service)
					{
						continue;
					}

					NativeArray<Entity> nativeArray = archetypeChunk.GetNativeArray(m_EntityType);
					BufferAccessor<Game.Pathfind.CoverageElement> bufferAccessor =
						archetypeChunk.GetBufferAccessor(ref m_CoverageElementType);
					for (int j = 0; j < archetypeChunk.Count; j++)
					{
						DynamicBuffer<Game.Pathfind.CoverageElement> dynamicBuffer = bufferAccessor[j];
						if (dynamicBuffer.Length != 0)
						{
							value.m_Entity = nativeArray[j];
							value.m_ElementCount = dynamicBuffer.Length;
							m_BuildingData.Add(in value);
							value.m_ElementIndex += dynamicBuffer.Length;
						}
					}
				}

				m_Elements.ResizeUninitialized(value.m_ElementIndex);
			}
		}

		[BurstCompile]
		public struct ProcessCoverageJob : IJobParallelForDefer
		{
			[ReadOnly] public int m_CoverageIndex;
			[NativeDisableParallelForRestriction] public NativeList<BuildingData> m_BuildingData;

			[NativeDisableParallelForRestriction] public NativeList<CoverageElement> m_Elements;
			[ReadOnly] public ComponentLookup<Owner> m_OwnerData;

			[ReadOnly] public ComponentLookup<Curve> m_CurveData;
			[ReadOnly] public ComponentLookup<Density> m_DensityData;

			[ReadOnly] public ComponentLookup<Destroyed> m_DestroyedData;
			[ReadOnly] public ComponentLookup<Temp> m_TempData;

			[ReadOnly] public ComponentLookup<Building> m_BuildingComponentData;
			[ReadOnly] public ComponentLookup<ModifiedServiceCoverage> m_ModifiedServiceCoverageData;

			[ReadOnly] public ComponentLookup<BorderDistrict> m_BorderDistrictData;
			[ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefData;

			[ReadOnly] public ComponentLookup<CoverageData> m_PrefabCoverageData;
			[ReadOnly] public BufferLookup<Game.Pathfind.CoverageElement> m_CoverageElements;

			[ReadOnly] public BufferLookup<ServiceDistrict> m_ServiceDistricts;
			[ReadOnly] public BufferLookup<Efficiency> m_Efficiencies;

			[NativeDisableParallelForRestriction] public BufferLookup<Game.Net.ServiceCoverage> m_CoverageData;

			public unsafe void Execute(int index)
			{
				ref BuildingData reference = ref m_BuildingData.ElementAt(index);
				PrefabRef prefabRef = m_PrefabRefData[reference.m_Entity];
				DynamicBuffer<Game.Pathfind.CoverageElement> dynamicBuffer = m_CoverageElements[reference.m_Entity];
				m_PrefabCoverageData.TryGetComponent(prefabRef.m_Prefab, out var componentData);
				Entity entity = reference.m_Entity;
				if (m_TempData.TryGetComponent(reference.m_Entity, out var componentData2))
				{
					entity = componentData2.m_Original;
				}

				if (m_ModifiedServiceCoverageData.TryGetComponent(entity, out var componentData3))
				{
					componentData3.ReplaceData(ref componentData);
				}

				Owner componentData4;
				while (m_OwnerData.TryGetComponent(entity, out componentData4))
				{
					entity = componentData4.m_Owner;
				}

				m_ServiceDistricts.TryGetBuffer(entity, out var bufferData);
				float efficiency = BuildingUtils.GetEfficiency(entity, ref m_Efficiencies);
				if (entity != reference.m_Entity &&
				    ((m_BuildingComponentData.TryGetComponent(reference.m_Entity, out var componentData5) &&
				      BuildingUtils.CheckOption(componentData5, BuildingOption.Inactive)) ||
				     m_DestroyedData.HasComponent(reference.m_Entity)))
				{
					efficiency = 0f;
				}

				NativeHashSet<Entity> nativeHashSet = default(NativeHashSet<Entity>);
				if (bufferData.IsCreated && bufferData.Length != 0)
				{
					nativeHashSet = new NativeHashSet<Entity>(bufferData.Length, Allocator.Temp);
					for (int i = 0; i < bufferData.Length; i++)
					{
						nativeHashSet.Add(bufferData[i].m_District);
					}
				}

				int elementWriteIndex = reference.m_ElementIndex;
				bool2 isInDistrict = default(bool2);
				for (int j = 0; j < dynamicBuffer.Length; j++)
				{
					Game.Pathfind.CoverageElement coverageElement = dynamicBuffer[j];
					if (!m_CoverageData.TryGetBuffer(coverageElement.m_Edge, out var bufferData2))
					{
						continue;
					}

					float densityFactor = 1f;
					if (nativeHashSet.IsCreated &&
					    m_BorderDistrictData.TryGetComponent(coverageElement.m_Edge, out var componentData6))
					{
						if (componentData6.m_Right == componentData6.m_Left)
						{
							if (!nativeHashSet.Contains(componentData6.m_Left))
							{
								continue;
							}
						}
						else
						{
							isInDistrict.x = nativeHashSet.Contains(componentData6.m_Left);
							isInDistrict.y = nativeHashSet.Contains(componentData6.m_Right);
							if (!math.any(isInDistrict))
							{
								continue;
							}

							densityFactor = math.select(0.5f, 1f, math.all(isInDistrict));
						}
					}

					float maxDensity = 0.01f;
					if (m_DensityData.TryGetComponent(coverageElement.m_Edge, out var componentData7))
					{
						maxDensity = math.max(maxDensity, componentData7.m_Density);
					}

					CoverageElement value = default(CoverageElement);
					value.m_CoveragePtr = UnsafeUtility.AddressOf(ref bufferData2.ElementAt(m_CoverageIndex));
					value.m_Coverage = math.max(0f, 1f - coverageElement.m_Cost * coverageElement.m_Cost) *
					                   componentData.m_Magnitude * efficiency;
					value.m_AverageCoverage = math.csum(value.m_Coverage) * 0.5f;
					value.m_DensityFactor = densityFactor;
					value.m_LengthFactor = m_CurveData[coverageElement.m_Edge].m_Length * math.sqrt(maxDensity);
					m_Elements[elementWriteIndex++] = value;
				}

				if (elementWriteIndex > reference.m_ElementIndex + 1)
				{
					m_Elements.AsArray()
						.GetSubArray(reference.m_ElementIndex, elementWriteIndex - reference.m_ElementIndex).Sort();
				}

				reference.m_Total = componentData.m_Capacity;
				reference.m_Remaining = componentData.m_Capacity;
				reference.m_ElementCount = elementWriteIndex - reference.m_ElementIndex;
				if (nativeHashSet.IsCreated)
				{
					nativeHashSet.Dispose();
				}
			}
		}

		[BurstCompile]
		public struct ApplyCoverageJob : IJob
		{
			public NativeList<BuildingData> m_BuildingData;
			public NativeList<CoverageElement> m_Elements;

			public unsafe void Execute()
			{
				for (int i = 0; i < m_BuildingData.Length; i++)
				{
					BuildingData buildingData = m_BuildingData[i];
					if (buildingData.m_ElementCount == 0 || buildingData.m_Remaining <= 0f)
					{
						m_BuildingData.RemoveAtSwapBack(i--);
					}
				}

				m_BuildingData.Sort(new BuildingDataComparer
				{
					m_Elements = m_Elements
				});
				int buildingIndex = 0;
				while (buildingIndex < m_BuildingData.Length)
				{
					BuildingData value = m_BuildingData[buildingIndex];
					CoverageElement coverageElement = m_Elements[value.m_ElementIndex++];
					ref Game.Net.ServiceCoverage reference =
						ref UnsafeUtility.AsRef<Game.Net.ServiceCoverage>(coverageElement.m_CoveragePtr);
					if (math.any(coverageElement.m_Coverage > reference.m_Coverage))
					{
						float coverageFactor = 0.99f * (1f - value.m_Remaining / value.m_Total);
						coverageFactor *= coverageFactor;
						coverageFactor *= coverageFactor;
						coverageFactor *= coverageFactor;
						coverageFactor = 1f - coverageFactor;
						float2 float5 = coverageElement.m_Coverage * coverageFactor;
						float2 valueToClamp = float5 - reference.m_Coverage;
						valueToClamp = math.clamp(valueToClamp, 0f, float5 * coverageElement.m_DensityFactor);
						reference.m_Coverage += valueToClamp;
						valueToClamp = math.saturate(valueToClamp / coverageElement.m_Coverage);
						value.m_Remaining -= math.lerp(valueToClamp.x, valueToClamp.y, 0.5f) *
						                     coverageElement.m_LengthFactor * coverageElement.m_DensityFactor;
					}

					if (--value.m_ElementCount == 0 || value.m_Remaining <= 0f)
					{
						buildingIndex++;
						continue;
					}

					coverageElement = m_Elements[value.m_ElementIndex];
					m_BuildingData[buildingIndex] = value;
					for (int j = buildingIndex + 1; j < m_BuildingData.Length; j++)
					{
						BuildingData value2 = m_BuildingData[j];
						CoverageElement other = m_Elements[value2.m_ElementIndex];
						if (coverageElement.CompareTo(other) <= 0)
						{
							break;
						}

						m_BuildingData[j] = value;
						m_BuildingData[j - 1] = value2;
					}
				}
			}
		}

		[BurstCompile]
		public struct SetupCoverageSearchJob : IJob
		{
			[ReadOnly] public Entity m_Entity;
			[ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefData;

			[ReadOnly] public ComponentLookup<CoverageData> m_PrefabCoverageData;
			public CoverageAction m_Action;

			public PathfindTargetSeeker<PathfindTargetBuffer> m_TargetSeeker;

			public void Execute()
			{
				PrefabRef prefabRef = m_PrefabRefData[m_Entity];
				CoverageData coverageData = default(CoverageData);
				if (m_PrefabCoverageData.HasComponent(prefabRef.m_Prefab))
				{
					coverageData = m_PrefabCoverageData[prefabRef.m_Prefab];
				}

				m_TargetSeeker.FindTargets(m_Entity, 0f);
				m_Action.data.m_Parameters = new CoverageParameters
				{
					m_Methods = m_TargetSeeker.m_PathfindParameters.m_Methods,
					m_Range = coverageData.m_Range
				};
			}
		}

		#endregion

		#region Helpers

		public struct CoverageElement : IComparable<CoverageElement>
		{
			[NativeDisableUnsafePtrRestriction] public unsafe void* m_CoveragePtr;
			public float2 m_Coverage;

			public float m_AverageCoverage;
			public float m_DensityFactor;

			public float m_LengthFactor;

			public int CompareTo(CoverageElement other)
			{
				return math.select(0, math.select(-1, 1, m_AverageCoverage < other.m_AverageCoverage),
					m_AverageCoverage != other.m_AverageCoverage);
			}
		}

		private struct QueueItem
		{
			public Entity m_Entity;
			public uint m_QueueFrame;

			public uint m_ResultFrame;
		}

		public struct BuildingData
		{
			public Entity m_Entity;
			public int m_ElementIndex;

			public int m_ElementCount;
			public float m_Total;

			public float m_Remaining;
		}

		private struct BuildingDataComparer : IComparer<BuildingData>
		{
			public NativeList<CoverageElement> m_Elements;

			public int Compare(BuildingData x, BuildingData y)
			{
				return m_Elements[x.m_ElementIndex].CompareTo(m_Elements[y.m_ElementIndex]);
			}
		}

		#endregion
	}

	// =========================================================
	// Harmony Prefix: 拦截所有对原版 ServiceCoverageSystem.SetupPathfindMethods 的外部调用
	// 被 Game.Tools 中的多个系统引用，必须通过 Harmony 修补以确保一致性
	// =========================================================
	[HarmonyPatch(typeof(Game.Simulation.ServiceCoverageSystem),
		nameof(Game.Simulation.ServiceCoverageSystem.SetupPathfindMethods))]
	public static class ServiceCoverageSystem_SetupPathfindMethods_Patch
	{
		/// <summary>
		/// Prefix: 完全替换原版 SetupPathfindMethods，返回 false 跳过原方法
		/// </summary>
		[HarmonyPrefix]
		public static bool Prefix(CoverageService service, ref PathfindParameters pathfindParameters,
			ref SetupQueueTarget setupQueueTarget)
		{
			switch (service)
			{
				case CoverageService.PostService:
				case CoverageService.Education:
				case CoverageService.EmergencyShelter:
				case CoverageService.Welfare:
					// 放开纯步行网格限制，允许通过道路扩散
					pathfindParameters.m_Methods = PathMethod.Road | PathMethod.Pedestrian;
					setupQueueTarget.m_Methods = PathMethod.Road | PathMethod.Pedestrian;
					setupQueueTarget.m_RoadTypes = RoadTypes.Car;
					break;
				case CoverageService.Park:
					// 公园覆盖：放开路网限制，不再从每个活动设施发起 A* 扩散
					pathfindParameters.m_Methods = PathMethod.Road | PathMethod.Pedestrian;
					setupQueueTarget.m_Methods = PathMethod.Road | PathMethod.Pedestrian;
					setupQueueTarget.m_RoadTypes = RoadTypes.Car;
					break;
				default:
					pathfindParameters.m_Methods = PathMethod.Road;
					setupQueueTarget.m_Methods = PathMethod.Road;
					setupQueueTarget.m_RoadTypes = RoadTypes.Car;
					break;
			}

			return false; // 跳过原方法
		}
	}
}
