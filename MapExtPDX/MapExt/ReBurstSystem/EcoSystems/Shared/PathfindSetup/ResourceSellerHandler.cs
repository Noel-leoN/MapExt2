// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

// 原版來源：Game.Simulation.ResourcePathfindSetup.SetupResourceSellerJob + SetupResourceSeller
//   （_KnowledgeBase/1_GameSource/Game/Simulation/ResourcePathfindSetup.cs:25-241 / 721-774）
//
// 本檔以原版 SetupResourceSellerJob 為基礎複製，改動三處（見 P1 計畫 §3）：
//   1. Early Exit（砍 T）：湊滿 kMaxCandidatesToFind 個「已完成 FindTargets 的合格候選」即 break。
//   2. 隨機起點 startOffset：避免先建的企業恆被優先評估，攤平地理與計數偏差（計數策略方案 3）。
//   3. cap 可配置：kMaxCandidatesToFind 由 ModSettings.ResourceSellerCandidateCap 於排程時注入。
// 其餘篩選 / 評分 / 分支完整性（flag~flag9、ServiceAvailable 與庫存兩套評分、CargoStation 隊列上限、
// RequireTransport 運力上限、防超賣 num2 校驗）逐行保留，零行為偏移。

using Colossal.Entities;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Routes;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
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
	/// P1：ResourceSeller 尋路 Setup 的 Early Exit handler。
	/// 掛在 <see cref="PathfindSetupDispatcher"/> 上，接管 <see cref="SetupTargetType.ResourceSeller"/>。
	/// 排程語義與原版 <c>ResourcePathfindSetup.SetupResourceSeller</c> 等價，僅在內層候選遍歷加 Early Exit。
	/// </summary>
	public sealed class ResourceSellerHandler : IFindTargetsHandler
	{
		#region Enabled

		// 綁 ResourceBuyer EcoSystem 開關（與原版 ResourceBuyerSystem 替換同組，語義一致）。
		public bool Enabled => Mod.Instance != null
			&& Mod.Instance.Settings != null
			&& Mod.Instance.Settings.isEnableEconomyFix
			&& Mod.Instance.Settings.EnableResourceBuyerEcoSystem;

		#endregion

		#region Cached state（延遲初始化 + World 重建檢測）

		private bool _initialized;
		private World _cachedWorld;

		// Dependency getter（PathfindSetupSystem.Dependency 為 protected，經反射存取，與 C1 同法）
		private static Func<SystemBase, JobHandle> _getDependencyAccessor;

		private ResourceSystem _resourceSystem;
		private EntityQuery _resourceSellerQuery;

		// --- Type handles ---
		private EntityTypeHandle _entityType;
		private BufferTypeHandle<OwnedVehicle> _ownedVehicleType;
		private BufferTypeHandle<StorageTransferRequest> _storageTransferRequestType;
		private BufferTypeHandle<TripNeeded> _tripNeededType;
		private BufferTypeHandle<InstalledUpgrade> _installedUpgradeType;

		// --- Component / Buffer lookups ---
		private ComponentLookup<IndustrialProcessData> _industrialProcessDatas;
		private ComponentLookup<ServiceAvailable> _serviceAvailables;
		private ComponentLookup<ServiceCompanyData> _serviceCompanies;
		private ComponentLookup<ResourceData> _resourceDatas;
		private ComponentLookup<StorageCompanyData> _storageCompanyDatas;
		private ComponentLookup<PropertyRenter> _propertyRenters;
		private ComponentLookup<Game.Objects.OutsideConnection> _outsideConnections;
		private ComponentLookup<Game.Buildings.CargoTransportStation> _cargoTransportStations;
		private ComponentLookup<TransportCompanyData> _transportCompanyDatas;
		private ComponentLookup<PrefabRef> _prefabs;
		private ComponentLookup<Building> _buildings;
		private ComponentLookup<Game.Vehicles.DeliveryTruck> _deliveryTrucks;
		private BufferLookup<Game.Economy.Resources> _resources;
		private BufferLookup<TradeCost> _tradeCosts;
		private BufferLookup<GuestVehicle> _guestVehicleBufs;
		private BufferLookup<LayoutElement> _layoutElementBufs;

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

			// 與原版 m_ResourceSellerQuery 嚴格一致（ResourcePathfindSetup.cs:650-672）
			_resourceSellerQuery = system.GetSetupQuery(new EntityQueryDesc
			{
				All = new[]
				{
					ComponentType.ReadOnly<PrefabRef>(),
					ComponentType.ReadOnly<Game.Economy.Resources>()
				},
				Any = new[]
				{
					ComponentType.ReadOnly<Game.Companies.StorageCompany>(),
					ComponentType.ReadOnly<Game.Buildings.CargoTransportStation>(),
					ComponentType.ReadOnly<ResourceSeller>()
				},
				None = new[]
				{
					ComponentType.ReadOnly<ShipStop>(),
					ComponentType.ReadOnly<AirplaneStop>(),
					ComponentType.ReadOnly<TrainStop>(),
					ComponentType.ReadOnly<Deleted>(),
					ComponentType.ReadOnly<Destroyed>(),
					ComponentType.ReadOnly<Temp>()
				}
			});

			_entityType = system.GetEntityTypeHandle();
			_ownedVehicleType = system.GetBufferTypeHandle<OwnedVehicle>(true);
			_storageTransferRequestType = system.GetBufferTypeHandle<StorageTransferRequest>(true);
			_tripNeededType = system.GetBufferTypeHandle<TripNeeded>(true);
			_installedUpgradeType = system.GetBufferTypeHandle<InstalledUpgrade>(true);

			_industrialProcessDatas = system.GetComponentLookup<IndustrialProcessData>(true);
			_serviceAvailables = system.GetComponentLookup<ServiceAvailable>(true);
			_serviceCompanies = system.GetComponentLookup<ServiceCompanyData>(true);
			_resourceDatas = system.GetComponentLookup<ResourceData>(true);
			_storageCompanyDatas = system.GetComponentLookup<StorageCompanyData>(true);
			_propertyRenters = system.GetComponentLookup<PropertyRenter>(true);
			_outsideConnections = system.GetComponentLookup<Game.Objects.OutsideConnection>(true);
			_cargoTransportStations = system.GetComponentLookup<Game.Buildings.CargoTransportStation>(true);
			_transportCompanyDatas = system.GetComponentLookup<TransportCompanyData>(true);
			_prefabs = system.GetComponentLookup<PrefabRef>(true);
			_buildings = system.GetComponentLookup<Building>(true);
			_deliveryTrucks = system.GetComponentLookup<Game.Vehicles.DeliveryTruck>(true);
			_resources = system.GetBufferLookup<Game.Economy.Resources>(true);
			_tradeCosts = system.GetBufferLookup<TradeCost>(true);
			_guestVehicleBufs = system.GetBufferLookup<GuestVehicle>(true);
			_layoutElementBufs = system.GetBufferLookup<LayoutElement>(true);

			ModLog.Ok("ResourceSellerHandler", $"已初始化（World={currentWorld.Name}）");
		}

		#endregion

		#region Schedule

		public JobHandle Schedule(PathfindSetupSystem system, ref PathfindSetupSystem.SetupData setupData)
		{
			EnsureInitialized(system);

			// --- 更新全部 handles / lookups（對齊原版 SetupResourceSeller 的 .Update(system) 清單）---
			_entityType.Update(system);
			_resources.Update(system);
			_industrialProcessDatas.Update(system);
			_cargoTransportStations.Update(system);
			_storageCompanyDatas.Update(system);
			_propertyRenters.Update(system);
			_tradeCosts.Update(system);
			_serviceAvailables.Update(system);
			_serviceCompanies.Update(system);
			_resourceDatas.Update(system);
			_outsideConnections.Update(system);
			_storageTransferRequestType.Update(system);
			_tripNeededType.Update(system);
			_prefabs.Update(system);
			_buildings.Update(system);
			_deliveryTrucks.Update(system);
			_guestVehicleBufs.Update(system);
			_layoutElementBufs.Update(system);
			_ownedVehicleType.Update(system);
			_transportCompanyDatas.Update(system);
			_installedUpgradeType.Update(system);

			// --- 讀取可配置 cap（managed 端讀 settings，再注入 Burst job）---
			int cap = Mod.Instance.Settings.ResourceSellerCandidateCap;
			if (cap < 1) cap = 1; // 防呆：0 會導致湊不滿而退化成全城遍歷

			JobHandle inputDeps = _getDependencyAccessor(system);

			var job = new CustomSetupResourceSellerJob
			{
				m_EntityType = _entityType,
				m_OwnedVehicles = _ownedVehicleType,
				m_StorageTransferRequestType = _storageTransferRequestType,
				m_TripNeededType = _tripNeededType,
				m_Resources = _resources,
				m_IndustrialProcessDatas = _industrialProcessDatas,
				m_CargoTransportStations = _cargoTransportStations,
				m_StorageCompanyDatas = _storageCompanyDatas,
				m_TransportCompanyDatas = _transportCompanyDatas,
				m_PropertyRenters = _propertyRenters,
				m_TradeCosts = _tradeCosts,
				m_ServiceAvailables = _serviceAvailables,
				m_ServiceCompanies = _serviceCompanies,
				m_ResourceDatas = _resourceDatas,
				m_ResourcePrefabs = _resourceSystem.GetPrefabs(),
				m_OutsideConnections = _outsideConnections,
				m_Prefabs = _prefabs,
				m_Buildings = _buildings,
				m_DeliveryTrucks = _deliveryTrucks,
				m_GuestVehicleBufs = _guestVehicleBufs,
				m_LayoutElementBufs = _layoutElementBufs,
				m_InstalledUpgradeType = _installedUpgradeType,
				m_RandomSeed = RandomSeed.Next(),
				m_MaxCandidatesToFind = cap,
				m_SetupData = setupData
			};

			JobHandle jobHandle = JobChunkExtensions.ScheduleParallel(job, _resourceSellerQuery, inputDeps);
			_resourceSystem.AddPrefabsReader(jobHandle);
			return jobHandle;
		}

		#endregion
	}

	/// <summary>
	/// 原版 <c>SetupResourceSellerJob</c> 的 Early Exit 版本。欄位佈局與原版嚴格一致（僅追加
	/// <see cref="m_MaxCandidatesToFind"/>），內層雙迴圈加隨機起點 + 湊滿 cap 即 break。
	/// </summary>
	[BurstCompile]
	public struct CustomSetupResourceSellerJob : IJobChunk
	{
		[ReadOnly] public EntityTypeHandle m_EntityType;
		[ReadOnly] public BufferTypeHandle<OwnedVehicle> m_OwnedVehicles;
		[ReadOnly] public ComponentLookup<IndustrialProcessData> m_IndustrialProcessDatas;
		[ReadOnly] public ComponentLookup<ServiceAvailable> m_ServiceAvailables;
		[ReadOnly] public ComponentLookup<ServiceCompanyData> m_ServiceCompanies;
		[ReadOnly] public ComponentLookup<ResourceData> m_ResourceDatas;
		[ReadOnly] public ResourcePrefabs m_ResourcePrefabs;
		[ReadOnly] public ComponentLookup<StorageCompanyData> m_StorageCompanyDatas;
		[ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenters;
		[ReadOnly] public BufferLookup<Game.Economy.Resources> m_Resources;
		[ReadOnly] public BufferLookup<TradeCost> m_TradeCosts;
		[ReadOnly] public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;
		[ReadOnly] public ComponentLookup<Game.Buildings.CargoTransportStation> m_CargoTransportStations;
		[ReadOnly] public BufferTypeHandle<StorageTransferRequest> m_StorageTransferRequestType;
		[ReadOnly] public ComponentLookup<TransportCompanyData> m_TransportCompanyDatas;
		[ReadOnly] public BufferTypeHandle<TripNeeded> m_TripNeededType;
		[ReadOnly] public ComponentLookup<PrefabRef> m_Prefabs;
		[ReadOnly] public ComponentLookup<Building> m_Buildings;
		[ReadOnly] public ComponentLookup<Game.Vehicles.DeliveryTruck> m_DeliveryTrucks;
		[ReadOnly] public BufferLookup<GuestVehicle> m_GuestVehicleBufs;
		[ReadOnly] public BufferLookup<LayoutElement> m_LayoutElementBufs;
		[ReadOnly] public BufferTypeHandle<InstalledUpgrade> m_InstalledUpgradeType;
		[ReadOnly] public RandomSeed m_RandomSeed;

		// [MOD OPT] 可配置候選上限（ModSettings.ResourceSellerCandidateCap）
		[ReadOnly] public int m_MaxCandidatesToFind;

		public PathfindSetupSystem.SetupData m_SetupData;

		// --- 原版 ResourcePathfindSetup 硬編碼常量（inline，值取自 ResourcePathfindSetup.cs:567-577）---
		private const float kOutsideConnectionAmountBasedPenalty = 0.03f;
		private const float kCargoStationAmountBasedPenalty = 0.0001f;
		private const float kCargoStationPerRequestPenalty = 0.0001f;
		private const int kCargoStationMaxTripNeededQueue = 10;

		public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
		{
			NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
			BufferAccessor<StorageTransferRequest> bufferAccessor = chunk.GetBufferAccessor(ref m_StorageTransferRequestType);
			BufferAccessor<InstalledUpgrade> bufferAccessor2 = chunk.GetBufferAccessor(ref m_InstalledUpgradeType);
			BufferAccessor<TripNeeded> bufferAccessor3 = chunk.GetBufferAccessor(ref m_TripNeededType);
			BufferAccessor<OwnedVehicle> bufferAccessor4 = chunk.GetBufferAccessor(ref m_OwnedVehicles);
			Unity.Mathematics.Random random = m_RandomSeed.GetRandom(unfilteredChunkIndex);

			for (int i = 0; i < m_SetupData.Length; i++)
			{
				m_SetupData.GetItem(i, out var entity, out var targetSeeker);
				Resource resource = targetSeeker.m_SetupQueueTarget.m_Resource;
				int value = targetSeeker.m_SetupQueueTarget.m_Value;
				if ((targetSeeker.m_SetupQueueTarget.m_Flags & SetupTargetFlags.RequireTransport) != SetupTargetFlags.None && bufferAccessor4.Length == 0)
				{
					continue;
				}

				// === [MOD OPT] Early Exit：局部候選計數 + 隨機起點 ===
				int candidatesFound = 0;
				int startOffset = (nativeArray.Length > 0) ? random.NextInt(nativeArray.Length) : 0;

				for (int jj = 0; jj < nativeArray.Length; jj++)
				{
					int j = (startOffset + jj) % nativeArray.Length;
					Entity entity2 = nativeArray[j];
					Entity prefab = m_Prefabs[entity2].m_Prefab;
					int num = ((bufferAccessor.Length > 0) ? bufferAccessor[j].Length : 0);
					if (entity2.Equals(entity))
					{
						continue;
					}
					bool flag = m_OutsideConnections.HasComponent(entity2);
					bool flag2 = m_CargoTransportStations.HasComponent(entity2);
					bool flag3 = m_StorageCompanyDatas.HasComponent(prefab) && !flag2 && !flag;
					bool flag4 = m_ServiceAvailables.HasComponent(entity2);
					bool flag5 = m_IndustrialProcessDatas.HasComponent(prefab) && !flag4 && !flag3;
					bool flag6 = EconomyUtils.IsOfficeResource(resource);
					bool flag7 = EconomyUtils.GetWeight(resource, m_ResourcePrefabs, ref m_ResourceDatas) == 0f;
					bool flag8 = (targetSeeker.m_SetupQueueTarget.m_Flags & SetupTargetFlags.BuildingUpkeep) != 0;
					if ((m_Buildings.HasComponent(entity2) && BuildingUtils.CheckOption(m_Buildings[entity2], BuildingOption.Inactive)) || ((flag4 || flag5) && (!m_PropertyRenters.HasComponent(entity2) || m_PropertyRenters[entity2].m_Property == Entity.Null)))
					{
						continue;
					}
					bool flag9 = false;
					if (flag6 && flag5 && (m_IndustrialProcessDatas[prefab].m_Output.m_Resource & resource) != Resource.NoResource)
					{
						flag9 = true;
					}
					else if ((targetSeeker.m_SetupQueueTarget.m_Flags & SetupTargetFlags.Commercial) != 0 && flag4 && (m_IndustrialProcessDatas[prefab].m_Output.m_Resource & resource) != Resource.NoResource)
					{
						flag9 = true;
					}
					else if ((targetSeeker.m_SetupQueueTarget.m_Flags & SetupTargetFlags.Industrial) != 0 && flag5 && (m_IndustrialProcessDatas[prefab].m_Output.m_Resource & resource) != Resource.NoResource)
					{
						flag9 = true;
					}
					else if ((targetSeeker.m_SetupQueueTarget.m_Flags & SetupTargetFlags.Import) != SetupTargetFlags.None && (flag || flag2 || flag3))
					{
						StorageCompanyData data = m_StorageCompanyDatas[prefab];
						if (bufferAccessor2.Length != 0)
						{
							UpgradeUtils.CombineStats(ref data, bufferAccessor2[j], ref targetSeeker.m_PrefabRef, ref m_StorageCompanyDatas);
						}
						if ((data.m_StoredResources & resource) != Resource.NoResource || (flag && flag7))
						{
							flag9 = true;
						}
					}
					if (!flag9 || (!flag && flag8 && bufferAccessor3.Length > 0 && bufferAccessor3[j].Length > 0))
					{
						continue;
					}
					int num2;
					if (flag && flag7)
					{
						num2 = value;
					}
					else
					{
						int allBuyingResourcesTrucks = VehicleUtils.GetAllBuyingResourcesTrucks(entity2, resource, ref m_DeliveryTrucks, ref m_GuestVehicleBufs, ref m_LayoutElementBufs);
						num2 = EconomyUtils.GetResources(resource, m_Resources[entity2]) - allBuyingResourcesTrucks;
						if (num2 <= 0 || (!flag && num2 < value / 2))
						{
							continue;
						}
					}
					float num3 = 0f;
					if (m_ServiceAvailables.HasComponent(entity2))
					{
						ServiceAvailable serviceAvailable = m_ServiceAvailables[entity2];
						int num4 = math.max(1, m_ServiceCompanies[prefab].m_MaxService);
						float num5 = (float)serviceAvailable.m_ServiceAvailable / (float)num4;
						float servicePriceMultiplier = EconomyUtils.GetServicePriceMultiplier(serviceAvailable.m_ServiceAvailable, num4);
						float marketPrice = EconomyUtils.GetMarketPrice(resource, m_ResourcePrefabs, ref m_ResourceDatas);
						float num6 = 0f;
						if (m_TradeCosts.HasBuffer(entity2))
						{
							num6 = EconomyUtils.GetTradeCost(resource, m_TradeCosts[entity2]).m_BuyCost;
						}
						float num7 = marketPrice * 0.5f;
						float num8 = (marketPrice * (servicePriceMultiplier - 1f) + num6 - num7) * (float)value;
						float num9 = 100f * math.saturate(1f - 2f * num5);
						num3 = (num8 + num9) * targetSeeker.m_PathfindParameters.m_Weights.money;
					}
					else
					{
						float num10 = math.min(1f, (float)num2 * 1f / (float)value);
						num3 += 100f * (1f - num10);
						if (flag2)
						{
							if ((targetSeeker.m_SetupQueueTarget.m_Flags & SetupTargetFlags.RequireTransport) != SetupTargetFlags.None)
							{
								if (!m_TransportCompanyDatas.HasComponent(prefab))
								{
									continue;
								}
								TransportCompanyData transportCompanyData = m_TransportCompanyDatas[prefab];
								if (bufferAccessor4[j].Length >= transportCompanyData.m_MaxTransports)
								{
									continue;
								}
							}
							if (bufferAccessor3.Length > 0 && bufferAccessor3[j].Length >= kCargoStationMaxTripNeededQueue)
							{
								continue;
							}
							num3 += kCargoStationAmountBasedPenalty * (float)value;
							num3 += kCargoStationPerRequestPenalty * (float)num;
						}
						if (flag)
						{
							num3 += kOutsideConnectionAmountBasedPenalty * (float)value;
							if (flag8)
							{
								num3 += (float)random.NextInt(300);
							}
						}
						if (m_TradeCosts.HasBuffer(entity2))
						{
							DynamicBuffer<TradeCost> costs = m_TradeCosts[entity2];
							num3 += EconomyUtils.GetTradeCost(resource, costs).m_BuyCost * (float)value * 0.01f;
						}
					}
					targetSeeker.FindTargets(entity2, num3);

					// === [MOD OPT] Early Exit：完成一次 FindTargets 的合格候選計入，湊滿即 break ===
					candidatesFound++;
					if (candidatesFound >= m_MaxCandidatesToFind)
					{
						break;
					}
				}
			}
		}
	}
}
