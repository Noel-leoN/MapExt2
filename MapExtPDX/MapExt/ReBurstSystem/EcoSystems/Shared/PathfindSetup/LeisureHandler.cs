// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

// 原版來源：Game.Simulation.CitizenPathfindSetup.SetupLeisureTargetJob + SetupLeisureTarget
//   （_KnowledgeBase/1_GameSource/Game/Simulation/CitizenPathfindSetup.cs:101-187 / 933-961）
//
// 本檔以原版 SetupLeisureTargetJob 為基礎複製，改動兩處（見 P2 計畫）：
//   1. Early Exit（砍 T）：湊滿 m_MaxCandidatesToFind 個「已完成 FindTargets 的合格候選」即 break。
//   2. 隨機起點 startOffset：避免先建的休閒設施恆被優先評估，攤平地理偏差。
// 其餘篩選 / 評分邏輯（LeisureType 比對、ServiceAvailable 門檻、Commercial/Meals 的庫存 cost 計算）逐行保留。
//
// 與 P1 差異：per-entity 極輕（無 GetAllBuyingResourcesTrucks 類重活），收益全來自砍 T；
// 多數候選 cost=0，A* 端以實際路徑距離排序，Early Exit 幾乎無品質損失。
// 原版 dead field m_LeisureSystemUpdateInterval（body 不使用）已省略——全新排程無欄位對齊約束。

using Colossal.Entities;
using Game.Agents;
using Game.Buildings;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.EcoShared
{
	/// <summary>
	/// P2：Leisure 尋路 Setup 的 Early Exit handler。
	/// 掛在 <see cref="PathfindSetupDispatcher"/> 上，接管 <see cref="SetupTargetType.Leisure"/>。
	/// 排程語義與原版 <c>CitizenPathfindSetup.SetupLeisureTarget</c> 等價，僅在內層候選遍歷加 Early Exit。
	/// </summary>
	public sealed class LeisureHandler : IFindTargetsHandler
	{
		#region Enabled

		// 綁 DownstreamAI EcoSystem 開關（F5 LeisureSystem 屬該組，語義一致）。
		public bool Enabled => Mod.Instance != null
			&& Mod.Instance.Settings != null
			&& Mod.Instance.Settings.isEnableEconomyFix
			&& Mod.Instance.Settings.EnableDownstreamAIEcoSystem;

		#endregion

		#region Cached state（延遲初始化 + World 重建檢測）

		private bool _initialized;
		private World _cachedWorld;

		// Dependency getter（PathfindSetupSystem.Dependency 為 protected，經反射存取，與 C1/P1 同法）
		private static Func<SystemBase, JobHandle> _getDependencyAccessor;

		private ResourceSystem _resourceSystem;
		private EntityQuery _leisureProviderQuery;

		// --- Type handles ---
		private EntityTypeHandle _entityType;
		private ComponentTypeHandle<ServiceAvailable> _serviceAvailableType;
		private ComponentTypeHandle<PrefabRef> _prefabType;

		// --- Component / Buffer lookups ---
		private ComponentLookup<IndustrialProcessData> _industrialProcessDatas;
		private ComponentLookup<LeisureProviderData> _leisureProviderDatas;
		private ComponentLookup<ResourceData> _resourceDatas;
		private ComponentLookup<ServiceCompanyData> _serviceDatas;
		private ComponentLookup<Building> _buildingDatas;
		private BufferLookup<Game.Economy.Resources> _resources;

		private void EnsureInitialized(PathfindSetupSystem system)
		{
			if (_getDependencyAccessor == null)
			{
				var dependencyGetter = AccessTools.PropertyGetter(typeof(SystemBase), "Dependency");
				_getDependencyAccessor = (Func<SystemBase, JobHandle>)Delegate.CreateDelegate(
					typeof(Func<SystemBase, JobHandle>), dependencyGetter);
			}

			var currentWorld = system.World;
			if (_initialized && _cachedWorld == currentWorld && _cachedWorld.IsCreated)
				return;
			_initialized = true;
			_cachedWorld = currentWorld;

			_resourceSystem = currentWorld.GetOrCreateSystemManaged<ResourceSystem>();

			// 與原版 m_LeisureProviderQuery 嚴格一致（CitizenPathfindSetup.cs:807）
			_leisureProviderQuery = system.GetSetupQuery(
				ComponentType.ReadOnly<Game.Buildings.LeisureProvider>(),
				ComponentType.Exclude<Temp>(),
				ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Destroyed>());

			_entityType = system.GetEntityTypeHandle();
			_serviceAvailableType = system.GetComponentTypeHandle<ServiceAvailable>(true);
			_prefabType = system.GetComponentTypeHandle<PrefabRef>(true);

			_industrialProcessDatas = system.GetComponentLookup<IndustrialProcessData>(true);
			_leisureProviderDatas = system.GetComponentLookup<LeisureProviderData>(true);
			_resourceDatas = system.GetComponentLookup<ResourceData>(true);
			_serviceDatas = system.GetComponentLookup<ServiceCompanyData>(true);
			_buildingDatas = system.GetComponentLookup<Building>(true);
			_resources = system.GetBufferLookup<Game.Economy.Resources>(true);

			ModLog.Ok("LeisureHandler", $"已初始化（World={currentWorld.Name}）");
		}

		#endregion

		#region Schedule

		public JobHandle Schedule(PathfindSetupSystem system, ref PathfindSetupSystem.SetupData setupData)
		{
			EnsureInitialized(system);

			// --- 更新全部 handles / lookups（對齊原版 SetupLeisureTarget 的 .Update(system) 清單）---
			_entityType.Update(system);
			_serviceAvailableType.Update(system);
			_prefabType.Update(system);
			_leisureProviderDatas.Update(system);
			_resources.Update(system);
			_industrialProcessDatas.Update(system);
			_resourceDatas.Update(system);
			_serviceDatas.Update(system);
			_buildingDatas.Update(system);

			// --- 讀取可配置 cap（managed 端讀 settings，再注入 Burst job）---
			int cap = Mod.Instance.Settings.LeisureCandidateCap;
			if (cap < 1) cap = 1; // 防呆：0 會導致湊不滿而退化成全城遍歷

			JobHandle inputDeps = _getDependencyAccessor(system);

			var job = new CustomSetupLeisureTargetJob
			{
				m_EntityType = _entityType,
				m_ServiceAvailableType = _serviceAvailableType,
				m_PrefabType = _prefabType,
				m_LeisureProviderDatas = _leisureProviderDatas,
				m_Resources = _resources,
				m_IndustrialProcessDatas = _industrialProcessDatas,
				m_ResourceDatas = _resourceDatas,
				m_ServiceDatas = _serviceDatas,
				m_BuildingDatas = _buildingDatas,
				m_ResourcePrefabs = _resourceSystem.GetPrefabs(),
				m_RandomSeed = Game.Common.RandomSeed.Next(),
				m_MaxCandidatesToFind = cap,
				m_SetupData = setupData
			};

			JobHandle jobHandle = JobChunkExtensions.ScheduleParallel(job, _leisureProviderQuery, inputDeps);
			_resourceSystem.AddPrefabsReader(jobHandle);
			return jobHandle;
		}

		#endregion
	}

	/// <summary>
	/// 原版 <c>SetupLeisureTargetJob</c> 的 Early Exit 版本。內層候選遍歷加隨機起點 + 湊滿 cap 即 break。
	/// </summary>
	[BurstCompile]
	public struct CustomSetupLeisureTargetJob : IJobChunk
	{
		[ReadOnly] public EntityTypeHandle m_EntityType;
		[ReadOnly] public ComponentTypeHandle<ServiceAvailable> m_ServiceAvailableType;
		[ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabType;
		[ReadOnly] public ComponentLookup<IndustrialProcessData> m_IndustrialProcessDatas;
		[ReadOnly] public ComponentLookup<LeisureProviderData> m_LeisureProviderDatas;
		[ReadOnly] public ComponentLookup<ResourceData> m_ResourceDatas;
		[ReadOnly] public ComponentLookup<ServiceCompanyData> m_ServiceDatas;
		[ReadOnly] public ComponentLookup<Building> m_BuildingDatas;
		[ReadOnly] public BufferLookup<Game.Economy.Resources> m_Resources;
		[ReadOnly] public ResourcePrefabs m_ResourcePrefabs;
		[ReadOnly] public Game.Common.RandomSeed m_RandomSeed;

		// [MOD OPT] 可配置候選上限（ModSettings.LeisureCandidateCap）
		[ReadOnly] public int m_MaxCandidatesToFind;

		public PathfindSetupSystem.SetupData m_SetupData;

		public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
		{
			NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
			NativeArray<ServiceAvailable> nativeArray2 = chunk.GetNativeArray(ref m_ServiceAvailableType);
			NativeArray<PrefabRef> nativeArray3 = chunk.GetNativeArray(ref m_PrefabType);
			Unity.Mathematics.Random random = m_RandomSeed.GetRandom(unfilteredChunkIndex);

			for (int i = 0; i < m_SetupData.Length; i++)
			{
				m_SetupData.GetItem(i, out var _, out var targetSeeker);
				LeisureType value = (LeisureType)targetSeeker.m_SetupQueueTarget.m_Value;
				float value2 = targetSeeker.m_SetupQueueTarget.m_Value2;

				// === [MOD OPT] Early Exit：局部候選計數 + 隨機起點 ===
				int candidatesFound = 0;
				int startOffset = (nativeArray.Length > 0) ? random.NextInt(nativeArray.Length) : 0;

				for (int jj = 0; jj < nativeArray.Length; jj++)
				{
					int j = (startOffset + jj) % nativeArray.Length;
					Entity entity2 = nativeArray[j];
					if (m_BuildingDatas.HasComponent(entity2) && BuildingUtils.CheckOption(m_BuildingDatas[entity2], BuildingOption.Inactive))
					{
						continue;
					}
					Entity prefab = nativeArray3[j].m_Prefab;
					if (!m_LeisureProviderDatas.HasComponent(prefab))
					{
						continue;
					}
					LeisureProviderData leisureProviderData = m_LeisureProviderDatas[prefab];
					float cost = 0f;
					if (value != leisureProviderData.m_LeisureType)
					{
						continue;
					}
					if ((value == LeisureType.Commercial || value == LeisureType.Meals) && nativeArray2.Length > 0 && m_ServiceDatas.HasComponent(prefab))
					{
						int serviceAvailable = nativeArray2[j].m_ServiceAvailable;
						if ((float)serviceAvailable < value2)
						{
							continue;
						}
						if (m_IndustrialProcessDatas.HasComponent(prefab))
						{
							IndustrialProcessData industrialProcessData = m_IndustrialProcessDatas[prefab];
							if (industrialProcessData.m_Output.m_Resource != Resource.NoResource)
							{
								serviceAvailable = math.min(serviceAvailable, EconomyUtils.GetResources(industrialProcessData.m_Output.m_Resource, m_Resources[entity2]));
								cost = 1000f * (1f - math.saturate(1f * (float)serviceAvailable / (float)m_ServiceDatas[prefab].m_MaxService) * 2f);
							}
						}
					}
					// === [MOD OPT] Early Exit：僅「實際加入了 target」的候選才計數 ===
					// FindTargets 回傳實際加入的 target 數，可能為 0（該設施無可用路網接入點）。
					// 無條件 ++ 會讓不可達設施白佔 cap 額度。詳見 ResourceSellerHandler 同處註釋。
					if (targetSeeker.FindTargets(entity2, cost) > 0 && ++candidatesFound >= m_MaxCandidatesToFind)
					{
						break;
					}
				}
			}
		}
	}
}
