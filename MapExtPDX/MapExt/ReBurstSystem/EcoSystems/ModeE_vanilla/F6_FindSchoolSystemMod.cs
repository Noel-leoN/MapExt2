using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Pathfind;
using Game.Prefabs;
using Game.Triggers;
using Game.Vehicles;
using Game.Areas;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Game;
using Game.Simulation;

namespace MapExtPDX.ModeE
{
	public partial class FindSchoolSystemMod : GameSystemBase
	{
		#region Constants

		public override int GetUpdateInterval(SystemUpdatePhase phase) => 16;

		#endregion

		#region Fields

		public bool debugFastFindSchool;
		private PathfindSetupSystem m_PathfindSetupSystem;
		private SimulationSystem m_SimulationSystem;
		private EndFrameBarrier m_EndFrameBarrier;
		private EntityQuery m_SchoolSeekerQuery;
		private EntityQuery m_ResultsQuery;
		private TriggerSystem m_TriggerSystem;

		#endregion

		#region Lifecycle

		protected override void OnCreate()
		{
			base.OnCreate();
			m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
			m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
			m_TriggerSystem = World.GetOrCreateSystemManaged<TriggerSystem>();
			m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
			m_SchoolSeekerQuery = GetEntityQuery(ComponentType.ReadWrite<SchoolSeeker>(), ComponentType.ReadOnly<Owner>(), ComponentType.Exclude<PathInformation>(), ComponentType.Exclude<Deleted>());
			m_ResultsQuery = GetEntityQuery(ComponentType.ReadWrite<SchoolSeeker>(), ComponentType.ReadOnly<Owner>(), ComponentType.ReadOnly<PathInformation>(), ComponentType.Exclude<Deleted>());
			RequireAnyForUpdate(m_SchoolSeekerQuery, m_ResultsQuery);
			RequireForUpdate<EconomyParameterData>();
			RequireForUpdate<TimeData>();
		}

		protected override void OnUpdate()
		{
			if (!m_SchoolSeekerQuery.IsEmptyIgnoreFilter)
			{
				FindSchoolJob jobData = new FindSchoolJob
				{
					m_EntityType = SystemAPI.GetEntityTypeHandle(),
					m_SchoolSeekerType = SystemAPI.GetComponentTypeHandle<SchoolSeeker>(isReadOnly: true),
					m_OwnerType = SystemAPI.GetComponentTypeHandle<Owner>(isReadOnly: true),
					m_HouseholdMembers = SystemAPI.GetComponentLookup<HouseholdMember>(isReadOnly: true),
					m_PropertyRenters = SystemAPI.GetComponentLookup<PropertyRenter>(isReadOnly: true),
					m_CurrentDistrictData = SystemAPI.GetComponentLookup<CurrentDistrict>(isReadOnly: true),
					m_Citizens = SystemAPI.GetComponentLookup<Citizen>(isReadOnly: true),
					m_Households = SystemAPI.GetComponentLookup<Household>(isReadOnly: true),
					m_HouseholdCitizens = SystemAPI.GetBufferLookup<HouseholdCitizen>(isReadOnly: true),
					m_OwnedVehicles = SystemAPI.GetBufferLookup<OwnedVehicle>(isReadOnly: true),
					m_PathfindQueue = m_PathfindSetupSystem.GetQueue(this, 64).AsParallelWriter(),
					m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter()
				};
				Dependency = jobData.ScheduleParallel(m_SchoolSeekerQuery, Dependency);
				m_PathfindSetupSystem.AddQueueWriter(Dependency);
				m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
			}
			if (!m_ResultsQuery.IsEmptyIgnoreFilter)
			{
				StartStudyingJob jobData2 = new StartStudyingJob
				{
					m_Chunks = m_ResultsQuery.ToArchetypeChunkListAsync(World.UpdateAllocator.ToAllocator, out JobHandle outJobHandle),
					m_EntityType = SystemAPI.GetEntityTypeHandle(),
					m_SchoolSeekerType = SystemAPI.GetComponentTypeHandle<SchoolSeeker>(isReadOnly: true),
					m_OwnerType = SystemAPI.GetComponentTypeHandle<Owner>(isReadOnly: true),
					m_PathInfoType = SystemAPI.GetComponentTypeHandle<PathInformation>(isReadOnly: true),
					m_Citizens = SystemAPI.GetComponentLookup<Citizen>(),
					m_Prefabs = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
					m_SchoolData = SystemAPI.GetComponentLookup<SchoolData>(isReadOnly: true),
					m_StudentBuffers = SystemAPI.GetBufferLookup<Game.Buildings.Student>(),
					m_InstalledUpgrades = SystemAPI.GetBufferLookup<InstalledUpgrade>(isReadOnly: true),
					m_Deleteds = SystemAPI.GetComponentLookup<Deleted>(isReadOnly: true),
					m_Workers = SystemAPI.GetComponentLookup<Worker>(isReadOnly: true),
					m_Employees = SystemAPI.GetBufferLookup<Employee>(),
					m_TriggerBuffer = m_TriggerSystem.CreateActionBuffer(),
					m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer(),
					m_SimulationFrame = m_SimulationSystem.frameIndex
				};
				Dependency = jobData2.Schedule(JobHandle.CombineDependencies(outJobHandle, Dependency));
				m_TriggerSystem.AddActionBufferWriter(Dependency);
				m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
			}
		}

		#endregion

		#region Jobs

		[BurstCompile]
		private struct FindSchoolJob : IJobChunk
		{
			[ReadOnly] public EntityTypeHandle m_EntityType;
			[ReadOnly] public ComponentTypeHandle<Owner> m_OwnerType;
			[ReadOnly] public ComponentTypeHandle<SchoolSeeker> m_SchoolSeekerType;
			[ReadOnly] public ComponentLookup<HouseholdMember> m_HouseholdMembers;
			[ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenters;
			[ReadOnly] public ComponentLookup<CurrentDistrict> m_CurrentDistrictData;
			[ReadOnly] public ComponentLookup<Citizen> m_Citizens;
			[ReadOnly] public ComponentLookup<Household> m_Households;
			[ReadOnly] public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;
			[ReadOnly] public BufferLookup<OwnedVehicle> m_OwnedVehicles;
			public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				NativeArray<Entity> entities = chunk.GetNativeArray(m_EntityType);
				NativeArray<Owner> owners = chunk.GetNativeArray(ref m_OwnerType);
				NativeArray<SchoolSeeker> seekers = chunk.GetNativeArray(ref m_SchoolSeekerType);
				for (int i = 0; i < entities.Length; i++)
				{
					Entity owner = owners[i].m_Owner;
					if (!m_Citizens.HasComponent(owner))
					{
						m_CommandBuffer.AddComponent(unfilteredChunkIndex, entities[i], default(Deleted));
						continue;
					}
					Citizen citizen = m_Citizens[owner];
					Entity household = m_HouseholdMembers[owner].m_Household;
					if (m_PropertyRenters.HasComponent(household))
					{
						Entity seekerEntity = entities[i];
						Entity property = m_PropertyRenters[household].m_Property;
						int level = seekers[i].m_Level;
						Entity district = Entity.Null;
						if (m_CurrentDistrictData.HasComponent(property))
						{
							district = m_CurrentDistrictData[property].m_District;
						}
						m_CommandBuffer.AddComponent(unfilteredChunkIndex, seekerEntity, new PathInformation
						{
							m_State = PathFlags.Pending
						});
						Household householdData = m_Households[household];
						DynamicBuffer<HouseholdCitizen> householdCitizens = m_HouseholdCitizens[household];
						PathfindParameters parameters = new PathfindParameters
						{
							m_MaxSpeed = 111.111115f,
							m_WalkSpeed = 1.6666667f,
							m_Weights = CitizenUtils.GetPathfindWeights(citizen, householdData, householdCitizens.Length),
							m_Methods = PathMethod.Pedestrian | PathMethod.PublicTransportDay,
							m_MaxCost = CitizenBehaviorSystem.kMaxPathfindCost,
							m_PathfindFlags = PathfindFlags.Simplified | PathfindFlags.IgnorePath
						};
						SetupQueueTarget origin = new SetupQueueTarget
						{
							m_Type = SetupTargetType.CurrentLocation,
							m_Methods = PathMethod.Pedestrian
						};
						SetupQueueTarget destination = new SetupQueueTarget
						{
							m_Type = SetupTargetType.SchoolSeekerTo,
							m_Methods = PathMethod.Pedestrian,
							m_Value = level,
							m_Entity = district
						};
						if (citizen.GetAge() != CitizenAge.Child)
						{
							PathUtils.UpdateOwnedVehicleMethods(household, ref m_OwnedVehicles, ref parameters, ref origin, ref destination);
						}
						SetupQueueItem value = new SetupQueueItem(seekerEntity, parameters, origin, destination);
						m_PathfindQueue.Enqueue(value);
					}
				}
			}
		}

		[BurstCompile]
		private struct StartStudyingJob : IJob
		{
			[ReadOnly] public NativeList<ArchetypeChunk> m_Chunks;
			[ReadOnly] public EntityTypeHandle m_EntityType;
			[ReadOnly] public ComponentTypeHandle<Owner> m_OwnerType;
			[ReadOnly] public ComponentTypeHandle<SchoolSeeker> m_SchoolSeekerType;
			[ReadOnly] public ComponentTypeHandle<PathInformation> m_PathInfoType;
			[NativeDisableParallelForRestriction] public ComponentLookup<Citizen> m_Citizens;
			public BufferLookup<Game.Buildings.Student> m_StudentBuffers;
			[ReadOnly] public ComponentLookup<Deleted> m_Deleteds;
			[ReadOnly] public ComponentLookup<PrefabRef> m_Prefabs;
			[ReadOnly] public ComponentLookup<SchoolData> m_SchoolData;
			[ReadOnly] public ComponentLookup<Worker> m_Workers;
			[ReadOnly] public BufferLookup<InstalledUpgrade> m_InstalledUpgrades;
			public BufferLookup<Employee> m_Employees;
			public NativeQueue<TriggerAction> m_TriggerBuffer;
			public EntityCommandBuffer m_CommandBuffer;
			public uint m_SimulationFrame;

			public void Execute()
			{
				for (int i = 0; i < m_Chunks.Length; i++)
				{
					ArchetypeChunk chunk = m_Chunks[i];
					NativeArray<Owner> owners = chunk.GetNativeArray(ref m_OwnerType);
					NativeArray<PathInformation> pathInfos = chunk.GetNativeArray(ref m_PathInfoType);
					NativeArray<Entity> entities = chunk.GetNativeArray(m_EntityType);
					NativeArray<SchoolSeeker> schoolSeekers = chunk.GetNativeArray(ref m_SchoolSeekerType);
					for (int j = 0; j < entities.Length; j++)
					{
						if ((pathInfos[j].m_State & PathFlags.Pending) != 0)
						{
							continue;
						}
						Entity seekerEntity = entities[j];
						Entity owner = owners[j].m_Owner;
						bool started = false;
						if (m_Citizens.HasComponent(owner) && !m_Deleteds.HasComponent(owner))
						{
							Entity schoolEntity = pathInfos[j].m_Destination;
							if (m_Prefabs.HasComponent(schoolEntity) && m_StudentBuffers.HasBuffer(schoolEntity))
							{
								DynamicBuffer<Game.Buildings.Student> students = m_StudentBuffers[schoolEntity];
								Entity schoolPrefab = m_Prefabs[schoolEntity].m_Prefab;
								if (m_SchoolData.HasComponent(schoolPrefab))
								{
									SchoolData schoolData = m_SchoolData[schoolPrefab];
									if (m_InstalledUpgrades.HasBuffer(schoolEntity))
									{
										UpgradeUtils.CombineStats(ref schoolData, m_InstalledUpgrades[schoolEntity], ref m_Prefabs, ref m_SchoolData);
									}
									if (students.Length < schoolData.m_StudentCapacity)
									{
										students.Add(new Game.Buildings.Student { m_Student = owner });
										m_CommandBuffer.AddComponent(owner, new Game.Citizens.Student
										{
											m_School = schoolEntity,
											m_LastCommuteTime = pathInfos[j].m_Duration,
											m_Level = (byte)schoolSeekers[j].m_Level
										});
										if (m_Workers.HasComponent(owner))
										{
											Entity workplace = m_Workers[owner].m_Workplace;
											if (m_Employees.HasBuffer(workplace))
											{
												DynamicBuffer<Employee> employees = m_Employees[workplace];
												for (int k = 0; k < employees.Length; k++)
												{
													if (employees[k].m_Worker == owner)
													{
														employees.RemoveAtSwapBack(k);
														break;
													}
												}
											}
											m_CommandBuffer.RemoveComponent<Worker>(owner);
										}
										m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.CitizenStartedSchool, Entity.Null, owner, schoolEntity));
										Citizen citizenValue = m_Citizens[owner];
										citizenValue.SetFailedEducationCount(0);
										m_Citizens[owner] = citizenValue;
										started = true;
										m_CommandBuffer.RemoveComponent<SchoolSeekerCooldown>(owner);
									}
								}
							}
							if (!started)
							{
								m_CommandBuffer.AddComponent(owner, new SchoolSeekerCooldown { m_SimulationFrame = m_SimulationFrame });
							}
						}
						m_CommandBuffer.RemoveComponent<HasSchoolSeeker>(owner);
						m_CommandBuffer.AddComponent(seekerEntity, default(Deleted));
					}
				}
			}
		}

		#endregion
	}
}
