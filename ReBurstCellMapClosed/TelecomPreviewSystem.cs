using System.Runtime.CompilerServices;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Scripting;
using Game.Tools;
using Game;

namespace MapExt2.Systems
{

	
	/// <summary>
	/// ����Ϊ�滻ϵͳ��
	/// nocell in bcjob;����ϵͳ����TelecomCoverageSystem(bc);
	/// ref by Heatmap/OverlayInfo;no method ref;
	/// 
	/// </summary>
	public partial class TelecomPreviewSystem : CellMapSystem<TelecomCoverage>
	{

		[Preserve]
		protected override void OnUpdate()
		{
			if (!this.m_ModifiedQuery.IsEmptyIgnoreFilter || this.m_ForceUpdate)
			{
				this.m_ForceUpdate = false;
				JobHandle outJobHandle;
				NativeList<ArchetypeChunk> densityChunks = this.m_DensityQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out outJobHandle);
				JobHandle outJobHandle2;
				NativeList<ArchetypeChunk> facilityChunks = this.m_FacilityQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out outJobHandle2);
				this.__TypeHandle.__Game_City_CityModifier_RO_BufferLookup.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Prefabs_TelecomFacilityData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Buildings_Efficiency_RO_BufferLookup.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Buildings_TelecomFacility_RO_ComponentLookup.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Companies_Employee_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Buildings_InstalledUpgrade_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Tools_Temp_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Buildings_Efficiency_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Buildings_TelecomFacility_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
				this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
				TelecomCoverageSystem.TelecomCoverageJob jobData = default(TelecomCoverageSystem.TelecomCoverageJob);
				jobData.m_DensityChunks = densityChunks;
				jobData.m_FacilityChunks = facilityChunks;
				jobData.m_TerrainHeightData = this.m_TerrainSystem.GetHeightData();
				jobData.m_City = this.m_CitySystem.City;
				jobData.m_Preview = true;
				jobData.m_TelecomCoverage = base.GetMap(readOnly: false, out var dependencies);
				jobData.m_TelecomStatus = this.m_Status;
				jobData.m_TransformType = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle;
				jobData.m_PropertyRenterType = this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentTypeHandle;
				jobData.m_TelecomFacilityType = this.__TypeHandle.__Game_Buildings_TelecomFacility_RO_ComponentTypeHandle;
				jobData.m_BuildingEfficiencyType = this.__TypeHandle.__Game_Buildings_Efficiency_RO_BufferTypeHandle;
				jobData.m_PrefabRefType = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;
				jobData.m_TempType = this.__TypeHandle.__Game_Tools_Temp_RO_ComponentTypeHandle;
				jobData.m_InstalledUpgradeType = this.__TypeHandle.__Game_Buildings_InstalledUpgrade_RO_BufferTypeHandle;
				jobData.m_HouseholdCitizenType = this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferTypeHandle;
				jobData.m_EmployeeType = this.__TypeHandle.__Game_Companies_Employee_RO_BufferTypeHandle;
				jobData.m_TransformData = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup;
				jobData.m_TelecomFacilityData = this.__TypeHandle.__Game_Buildings_TelecomFacility_RO_ComponentLookup;
				jobData.m_BuildingEfficiencyData = this.__TypeHandle.__Game_Buildings_Efficiency_RO_BufferLookup;
				jobData.m_PrefabRefData = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup;
				jobData.m_ObjectGeometryData = this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup;
				jobData.m_PrefabTelecomFacilityData = this.__TypeHandle.__Game_Prefabs_TelecomFacilityData_RO_ComponentLookup;
				jobData.m_CityModifiers = this.__TypeHandle.__Game_City_CityModifier_RO_BufferLookup;
				JobHandle jobHandle = IJobExtensions.Schedule(jobData, JobHandle.CombineDependencies(job1: JobHandle.CombineDependencies(outJobHandle, outJobHandle2, dependencies), job0: base.Dependency));
				densityChunks.Dispose(jobHandle);
				facilityChunks.Dispose(jobHandle);
				this.m_TerrainSystem.AddCPUHeightReader(jobHandle);
				base.AddWriter(jobHandle);
				base.Dependency = jobHandle;
			}
		}


	}
}
