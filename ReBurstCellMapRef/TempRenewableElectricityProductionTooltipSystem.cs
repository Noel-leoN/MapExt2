using System.Runtime.CompilerServices;
using Colossal.Entities;
using Game.Buildings;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.UI.Localization;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Scripting;
using Game.UI.Tooltip;

namespace MapExt2.Systems
{
	[CompilerGenerated]
	public class TempRenewableElectricityProductionTooltipSystem : TooltipSystemBase
	{
		private struct TypeHandle
		{
			[ReadOnly]
			public ComponentLookup<WindPoweredData> __Game_Prefabs_WindPoweredData_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<GroundWaterPoweredData> __Game_Prefabs_GroundWaterPoweredData_RO_ComponentLookup;

			[ReadOnly]
			public ComponentTypeHandle<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;

			[ReadOnly]
			public ComponentTypeHandle<Transform> __Game_Objects_Transform_RO_ComponentTypeHandle;

			[ReadOnly]
			public ComponentTypeHandle<Temp> __Game_Tools_Temp_RO_ComponentTypeHandle;

			[ReadOnly]
			public ComponentTypeHandle<Game.Buildings.WaterPowered> __Game_Buildings_WaterPowered_RO_ComponentTypeHandle;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Game_Prefabs_WindPoweredData_RO_ComponentLookup = state.GetComponentLookup<WindPoweredData>(isReadOnly: true);
				this.__Game_Prefabs_GroundWaterPoweredData_RO_ComponentLookup = state.GetComponentLookup<GroundWaterPoweredData>(isReadOnly: true);
				this.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PrefabRef>(isReadOnly: true);
				this.__Game_Objects_Transform_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Transform>(isReadOnly: true);
				this.__Game_Tools_Temp_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Temp>(isReadOnly: true);
				this.__Game_Buildings_WaterPowered_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Game.Buildings.WaterPowered>(isReadOnly: true);
			}
		}

		private WindSystem m_WindSystem;

		private GroundWaterSystem m_GroundWaterSystem;

		private EntityQuery m_ErrorQuery;

		private EntityQuery m_TempQuery;

		private ProgressTooltip m_Production;

		private StringTooltip m_WindWarning;

		private StringTooltip m_GroundWaterAvailabilityWarning;

		private TypeHandle __TypeHandle;

		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_WindSystem = base.World.GetOrCreateSystemManaged<WindSystem>();
			this.m_GroundWaterSystem = base.World.GetOrCreateSystemManaged<GroundWaterSystem>();
			this.m_ErrorQuery = base.GetEntityQuery(ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Error>());
			this.m_TempQuery = base.GetEntityQuery(ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Transform>(), ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadOnly<RenewableElectricityProduction>(), ComponentType.Exclude<Deleted>());
			this.m_Production = new ProgressTooltip
			{
				path = "renewableElectricityProduction",
				icon = "Media/Game/Icons/Electricity.svg",
				label = LocalizedString.Id("Tools.ELECTRICITY_PRODUCTION_LABEL"),
				unit = "power",
				omitMax = true
			};
			this.m_WindWarning = new StringTooltip
			{
				path = "windWarning",
				value = LocalizedString.Id("Tools.WARNING[NotEnoughWind]"),
				color = TooltipColor.Warning
			};
			this.m_GroundWaterAvailabilityWarning = new StringTooltip
			{
				path = "groundWaterAvailabilityWarning",
				value = LocalizedString.Id("Tools.WARNING[NotEnoughGroundWater]"),
				color = TooltipColor.Warning
			};
			base.RequireForUpdate(this.m_TempQuery);
		}

		[Preserve]
		protected override void OnUpdate()
		{
			if (!this.m_ErrorQuery.IsEmptyIgnoreFilter)
			{
				return;
			}
			base.CompleteDependency();
			float num = 0f;
			float num2 = 0f;
			bool flag = false;
			bool flag2 = false;
			JobHandle dependencies;
			NativeArray<Wind> map = this.m_WindSystem.GetMap(readOnly: true, out dependencies);
			JobHandle dependencies2;
			NativeArray<GroundWater> map2 = this.m_GroundWaterSystem.GetMap(readOnly: true, out dependencies2);
			dependencies.Complete();
			dependencies2.Complete();
			this.__TypeHandle.__Game_Prefabs_WindPoweredData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			ComponentLookup<WindPoweredData> _Game_Prefabs_WindPoweredData_RO_ComponentLookup = this.__TypeHandle.__Game_Prefabs_WindPoweredData_RO_ComponentLookup;
			this.__TypeHandle.__Game_Prefabs_GroundWaterPoweredData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			ComponentLookup<GroundWaterPoweredData> _Game_Prefabs_GroundWaterPoweredData_RO_ComponentLookup = this.__TypeHandle.__Game_Prefabs_GroundWaterPoweredData_RO_ComponentLookup;
			NativeArray<ArchetypeChunk> nativeArray = this.m_TempQuery.ToArchetypeChunkArray(Allocator.TempJob);
			try
			{
				this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
				ComponentTypeHandle<PrefabRef> typeHandle = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;
				this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
				ComponentTypeHandle<Transform> typeHandle2 = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle;
				this.__TypeHandle.__Game_Tools_Temp_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
				ComponentTypeHandle<Temp> typeHandle3 = this.__TypeHandle.__Game_Tools_Temp_RO_ComponentTypeHandle;
				this.__TypeHandle.__Game_Buildings_WaterPowered_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
				ComponentTypeHandle<Game.Buildings.WaterPowered> typeHandle4 = this.__TypeHandle.__Game_Buildings_WaterPowered_RO_ComponentTypeHandle;
				foreach (ArchetypeChunk item in nativeArray)
				{
					NativeArray<PrefabRef> nativeArray2 = item.GetNativeArray(ref typeHandle);
					NativeArray<Transform> nativeArray3 = item.GetNativeArray(ref typeHandle2);
					NativeArray<Temp> nativeArray4 = item.GetNativeArray(ref typeHandle3);
					NativeArray<Game.Buildings.WaterPowered> nativeArray5 = item.GetNativeArray(ref typeHandle4);
					for (int i = 0; i < nativeArray2.Length; i++)
					{
						if ((nativeArray4[i].m_Flags & (TempFlags.Create | TempFlags.Modify | TempFlags.Upgrade)) == 0)
						{
							continue;
						}
						Entity prefab = nativeArray2[i].m_Prefab;
						if (_Game_Prefabs_WindPoweredData_RO_ComponentLookup.HasComponent(prefab))
						{
							Wind wind = WindSystem.GetWind(nativeArray3[i].m_Position, map);
							float2 windProduction = PowerPlantAISystem.GetWindProduction(_Game_Prefabs_WindPoweredData_RO_ComponentLookup[prefab], wind, 1f);
							num += windProduction.x;
							num2 += windProduction.y;
							flag |= windProduction.x < windProduction.y * 0.75f;
						}
						if (nativeArray5.Length != 0 && base.EntityManager.TryGetComponent<WaterPoweredData>(prefab, out var component))
						{
							Game.Buildings.WaterPowered waterPowered = nativeArray5[i];
							float waterCapacity = PowerPlantAISystem.GetWaterCapacity(waterPowered, component);
							float num3 = math.min(waterCapacity, waterPowered.m_Estimate * component.m_ProductionFactor);
							num += num3;
							num2 += waterCapacity;
						}
						if (_Game_Prefabs_GroundWaterPoweredData_RO_ComponentLookup.TryGetComponent(prefab, out var componentData) && componentData.m_MaximumGroundWater > 0)
						{
							float2 groundWaterProduction = PowerPlantAISystem.GetGroundWaterProduction(componentData, nativeArray3[i].m_Position, 1f, map2);
							num += groundWaterProduction.x;
							num2 += groundWaterProduction.y;
							if (groundWaterProduction.x < groundWaterProduction.y * 0.75f)
							{
								flag2 = true;
							}
						}
					}
				}
				if (num2 > 0f)
				{
					this.m_Production.value = num;
					this.m_Production.max = num2;
					ProgressTooltip.SetCapacityColor(this.m_Production);
					base.AddMouseTooltip(this.m_Production);
					if (flag)
					{
						base.AddMouseTooltip(this.m_WindWarning);
					}
					if (flag2)
					{
						base.AddMouseTooltip(this.m_GroundWaterAvailabilityWarning);
					}
				}
			}
			finally
			{
				nativeArray.Dispose();
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
		public TempRenewableElectricityProductionTooltipSystem()
		{
		}
	}
}
