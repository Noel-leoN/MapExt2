using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Companies;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
// using static MapExtPDX.MapExt.ReBurstSystemModeC.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeC
{
    // v1.3.6f变更

    /// <summary>
    /// TelecomCoverageJob被TelecomCoverageSystem/TelecomPreviewSystem两个系统调用
    /// </summary>
    /// 
    [BurstCompile]
    public struct TelecomCoverageJob : IJob
    {
        // 重定向私有结构体
        private struct CellDensityData
        {
            public ushort m_Density;
        }

        // 重定向私有结构体
        private struct CellFacilityData
        {
            public float m_SignalStrength;

            public float m_AccumulatedSignalStrength;

            public float m_NetworkCapacity;
        }

        [ReadOnly]
        public NativeList<ArchetypeChunk> m_DensityChunks;

        [ReadOnly]
        public NativeList<ArchetypeChunk> m_FacilityChunks;

        [ReadOnly]
        public TerrainHeightData m_TerrainHeightData;

        [ReadOnly]
        public Entity m_City;

        [ReadOnly]
        public bool m_Preview;

        public NativeArray<TelecomCoverage> m_TelecomCoverage;

        public NativeArray<TelecomStatus> m_TelecomStatus;

        [ReadOnly]
        public ComponentTypeHandle<Transform> m_TransformType;

        [ReadOnly]
        public ComponentTypeHandle<PropertyRenter> m_PropertyRenterType;

        [ReadOnly]
        public ComponentTypeHandle<Game.Buildings.TelecomFacility> m_TelecomFacilityType;

        [ReadOnly]
        public BufferTypeHandle<Efficiency> m_BuildingEfficiencyType;

        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabRefType;

        [ReadOnly]
        public ComponentTypeHandle<Temp> m_TempType;

        [ReadOnly]
        public BufferTypeHandle<InstalledUpgrade> m_InstalledUpgradeType;

        [ReadOnly]
        public BufferTypeHandle<HouseholdCitizen> m_HouseholdCitizenType;

        [ReadOnly]
        public BufferTypeHandle<Employee> m_EmployeeType;

        [ReadOnly]
        public ComponentLookup<Transform> m_TransformData;

        [ReadOnly]
        public ComponentLookup<Game.Buildings.TelecomFacility> m_TelecomFacilityData;

        [ReadOnly]
        public BufferLookup<Efficiency> m_BuildingEfficiencyData;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_PrefabRefData;

        [ReadOnly]
        public ComponentLookup<ObjectGeometryData> m_ObjectGeometryData;

        [ReadOnly]
        public ComponentLookup<TelecomFacilityData> m_PrefabTelecomFacilityData;

        [ReadOnly]
        public BufferLookup<CityModifier> m_CityModifiers;

        public void Execute()
        {
            NativeArray<CellDensityData> densityData = new NativeArray<CellDensityData>(16384, Allocator.Temp);
            NativeArray<CellFacilityData> facilityData = new NativeArray<CellFacilityData>(16384, Allocator.Temp);
            NativeArray<float> obstructSlopes = new NativeArray<float>(16384, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeList<float> signalStrengths = new NativeList<float>(16384, Allocator.Temp);
            DynamicBuffer<CityModifier> cityModifiers = this.m_CityModifiers[this.m_City];
            for (int i = 0; i < this.m_DensityChunks.Length; i++)
            {
                this.AddDensity(densityData, this.m_DensityChunks[i]);
            }
            for (int j = 0; j < this.m_FacilityChunks.Length; j++)
            {
                this.CalculateSignalStrength(facilityData, obstructSlopes, signalStrengths, this.m_FacilityChunks[j], cityModifiers);
            }
            int arrayIndex = 0;
            TelecomStatus status = default(TelecomStatus);
            for (int k = 0; k < this.m_FacilityChunks.Length; k++)
            {
                this.AddNetworkCapacity(densityData, facilityData, signalStrengths, this.m_FacilityChunks[k], ref arrayIndex, ref status, cityModifiers);
            }
            if (this.m_TelecomCoverage.Length != 0)
            {
                this.CalculateTelecomCoverage(facilityData);
            }
            if (this.m_TelecomStatus.Length != 0)
            {
                status.m_Quality = this.CalculateTelecomQuality(densityData, facilityData);
                this.m_TelecomStatus[0] = status;
            }
            densityData.Dispose();
            facilityData.Dispose();
            obstructSlopes.Dispose();
            signalStrengths.Dispose();
        }

        private void CalculateTelecomCoverage(NativeArray<CellFacilityData> facilityData)
        {
            int num = 0;
            TelecomCoverage value = default(TelecomCoverage);
            for (int i = 0; i < 128; i++)
            {
                for (int j = 0; j < 128; j++)
                {
                    int index = num + j;
                    CellFacilityData cellFacilityData = facilityData[index];
                    value.m_SignalStrength = (byte)math.clamp((int)(cellFacilityData.m_SignalStrength * 255f), 0, 255);
                    value.m_NetworkLoad = (byte)math.clamp((int)(127.5f / math.max(0.0001f, cellFacilityData.m_NetworkCapacity)), 0, 255);
                    this.m_TelecomCoverage[index] = value;
                }
                num += 128;
            }
        }

        private float CalculateTelecomQuality(NativeArray<CellDensityData> densityData, NativeArray<CellFacilityData> facilityData)
        {
            float2 @float = 0f;
            int num = 0;
            for (int i = 0; i < 128; i++)
            {
                for (int j = 0; j < 128; j++)
                {
                    int index = num + j;
                    CellDensityData cellDensityData = densityData[index];
                    CellFacilityData cellFacilityData = facilityData[index];
                    float num2 = cellFacilityData.m_SignalStrength * 2f;
                    float num3 = 1f / math.max(0.0001f, cellFacilityData.m_NetworkCapacity);
                    float num4 = math.min(1f, num2 / (1f + num3));
                    float num5 = (int)cellDensityData.m_Density;
                    @float += new float2(num4 * num5, num5);
                }
                num += 128;
            }
            if (@float.y != 0f)
            {
                @float.x /= @float.y;
            }
            return @float.x;
        }

        private void AddNetworkCapacity(NativeArray<CellDensityData> densityData, NativeArray<CellFacilityData> facilityData, NativeList<float> signalStrengths, ArchetypeChunk chunk, ref int arrayIndex, ref TelecomStatus status, DynamicBuffer<CityModifier> cityModifiers)
        {
            NativeArray<Transform> nativeArray = chunk.GetNativeArray(ref this.m_TransformType);
            NativeArray<Game.Buildings.TelecomFacility> nativeArray2 = chunk.GetNativeArray(ref this.m_TelecomFacilityType);
            BufferAccessor<Efficiency> bufferAccessor = chunk.GetBufferAccessor(ref this.m_BuildingEfficiencyType);
            NativeArray<PrefabRef> nativeArray3 = chunk.GetNativeArray(ref this.m_PrefabRefType);
            NativeArray<Temp> nativeArray4 = chunk.GetNativeArray(ref this.m_TempType);
            BufferAccessor<InstalledUpgrade> bufferAccessor2 = chunk.GetBufferAccessor(ref this.m_InstalledUpgradeType);
            for (int i = 0; i < nativeArray.Length; i++)
            {
                Transform transform = nativeArray[i];
                PrefabRef prefabRef = nativeArray3[i];
                this.m_PrefabTelecomFacilityData.TryGetComponent(prefabRef.m_Prefab, out var componentData);
                if (bufferAccessor2.Length != 0)
                {
                    UpgradeUtils.CombineStats(ref componentData, bufferAccessor2[i], ref this.m_PrefabRefData, ref this.m_PrefabTelecomFacilityData);
                }
                float efficiencyFactor = this.GetEfficiencyFactor(nativeArray2, nativeArray4, bufferAccessor, i);
                CityUtils.ApplyModifier(ref componentData.m_NetworkCapacity, cityModifiers, CityModifierType.TelecomCapacity);
                componentData.m_Range *= math.sqrt(efficiencyFactor);
                componentData.m_NetworkCapacity *= efficiencyFactor;
                if (!(componentData.m_Range < 1f) && !(componentData.m_NetworkCapacity < 1f))
                {
                    //
                    int2 @int = math.max(CellMapSystem<TelecomCoverage>.GetCell(transform.m_Position - componentData.m_Range, CellMapSystemRe.kMapSize, 128), 0); //

                    int2 int2 = math.min(CellMapSystem<TelecomCoverage>.GetCell(transform.m_Position + componentData.m_Range, CellMapSystemRe.kMapSize, 128) + 1, 128); //

                    int2 int3 = int2 - @int;
                    if (!math.any(int3 <= 0))
                    {
                        NativeArray<float> subArray = signalStrengths.AsArray().GetSubArray(arrayIndex, int3.x * int3.y);
                        arrayIndex += int3.x * int3.y;
                        float num = this.CalculateNetworkUsers(densityData, facilityData, subArray, @int, int2);
                        float capacity = componentData.m_NetworkCapacity / math.max(1f, num);
                        this.AddNetworkCapacity(facilityData, subArray, @int, int2, capacity);
                        status.m_Capacity += componentData.m_NetworkCapacity;
                        status.m_Load += num;
                    }
                }
            }
        }

        private void AddNetworkCapacity(NativeArray<CellFacilityData> facilityData, NativeArray<float> signalStrengthArray, int2 min, int2 max, float capacity)
        {
            int2 @int = max - min;
            int num = 128 * min.y;
            int num2 = -min.x;
            for (int i = min.y; i < max.y; i++)
            {
                for (int j = min.x; j < max.x; j++)
                {
                    float num3 = signalStrengthArray[num2 + j];
                    int index = num + j;
                    CellFacilityData value = facilityData[index];
                    value.m_NetworkCapacity = math.select(value.m_NetworkCapacity, value.m_NetworkCapacity + capacity * (num3 / value.m_AccumulatedSignalStrength), value.m_AccumulatedSignalStrength > 0.0001f);
                    facilityData[index] = value;
                }
                num += 128;
                num2 += @int.x;
            }
        }

        private float CalculateNetworkUsers(NativeArray<CellDensityData> densityData, NativeArray<CellFacilityData> facilityData, NativeArray<float> signalStrengthArray, int2 min, int2 max)
        {
            float num = 0f;
            int2 @int = max - min;
            int num2 = 128 * min.y;
            int num3 = -min.x;
            for (int i = min.y; i < max.y; i++)
            {
                for (int j = min.x; j < max.x; j++)
                {
                    float num4 = signalStrengthArray[num3 + j];
                    int index = num2 + j;
                    CellDensityData cellDensityData = densityData[index];
                    CellFacilityData cellFacilityData = facilityData[index];
                    num += math.select(0f, (float)(int)cellDensityData.m_Density * (num4 / cellFacilityData.m_AccumulatedSignalStrength), cellFacilityData.m_AccumulatedSignalStrength > 0.0001f);
                }
                num2 += 128;
                num3 += @int.x;
            }
            return num;
        }

        private void CalculateSignalStrength(NativeArray<CellFacilityData> facilityData, NativeArray<float> obstructSlopes, NativeList<float> signalStrengths, ArchetypeChunk chunk, DynamicBuffer<CityModifier> cityModifiers)
        {
            NativeArray<Transform> nativeArray = chunk.GetNativeArray(ref this.m_TransformType);
            NativeArray<Game.Buildings.TelecomFacility> nativeArray2 = chunk.GetNativeArray(ref this.m_TelecomFacilityType);
            BufferAccessor<Efficiency> bufferAccessor = chunk.GetBufferAccessor(ref this.m_BuildingEfficiencyType);
            NativeArray<PrefabRef> nativeArray3 = chunk.GetNativeArray(ref this.m_PrefabRefType);
            NativeArray<Temp> nativeArray4 = chunk.GetNativeArray(ref this.m_TempType);
            BufferAccessor<InstalledUpgrade> bufferAccessor2 = chunk.GetBufferAccessor(ref this.m_InstalledUpgradeType);
            for (int i = 0; i < nativeArray.Length; i++)
            {
                Transform transform = nativeArray[i];
                PrefabRef prefabRef = nativeArray3[i];
                ObjectGeometryData objectGeometryData = this.m_ObjectGeometryData[prefabRef.m_Prefab];
                this.m_PrefabTelecomFacilityData.TryGetComponent(prefabRef.m_Prefab, out var componentData);
                if (bufferAccessor2.Length != 0)
                {
                    UpgradeUtils.CombineStats(ref componentData, bufferAccessor2[i], ref this.m_PrefabRefData, ref this.m_PrefabTelecomFacilityData);
                }
                float efficiencyFactor = this.GetEfficiencyFactor(nativeArray2, nativeArray4, bufferAccessor, i);
                CityUtils.ApplyModifier(ref componentData.m_NetworkCapacity, cityModifiers, CityModifierType.TelecomCapacity);
                componentData.m_Range *= math.sqrt(efficiencyFactor);
                componentData.m_NetworkCapacity *= efficiencyFactor;
                if (componentData.m_Range < 1f || componentData.m_NetworkCapacity < 1f)
                {
                    continue;
                }
                float3 position = transform.m_Position;
                position.y += objectGeometryData.m_Size.y;
                int2 @int = math.max(CellMapSystem<TelecomCoverage>.GetCell(position - componentData.m_Range, CellMapSystemRe.kMapSize, 128), 0); //
                int2 int2 = math.min(CellMapSystem<TelecomCoverage>.GetCell(position + componentData.m_Range, CellMapSystemRe.kMapSize, 128) + 1, 128); //
                int2 int3 = int2 - @int;
                if (math.any(int3 <= 0))
                {
                    continue;
                }
                int length = signalStrengths.Length;
                signalStrengths.Resize(length + int3.x * int3.y, NativeArrayOptions.UninitializedMemory);
                NativeArray<float> subArray = signalStrengths.AsArray().GetSubArray(length, int3.x * int3.y);
                if (componentData.m_PenetrateTerrain)
                {
                    this.CalculateSignalStrength(subArray, @int, int2, componentData.m_Range, position);
                }
                else
                {
                    this.ResetObstructAngles(obstructSlopes, @int, int2);
                    int2 int4 = math.clamp(CellMapSystem<TelecomCoverage>.GetCell(position, CellMapSystemRe.kMapSize, 128), 0, 127); //
                    this.CalculateCellSignalStrength(obstructSlopes, subArray, int4, @int, int2, componentData.m_Range, position);
                    int2 int5 = int4;
                    int2 int6 = int4 + 1;
                    while (math.any((int5 > @int) | (int6 < int2)))
                    {
                        if (int5.y > @int.y)
                        {
                            int5.y--;
                            for (int j = int4.x; j < int6.x; j++)
                            {
                                this.CalculateCellSignalStrength(obstructSlopes, subArray, new int2(j, int5.y), @int, int2, componentData.m_Range, position);
                            }
                            for (int num = int4.x - 1; num >= int5.x; num--)
                            {
                                this.CalculateCellSignalStrength(obstructSlopes, subArray, new int2(num, int5.y), @int, int2, componentData.m_Range, position);
                            }
                        }
                        if (int6.y < int2.y)
                        {
                            for (int k = int4.x; k < int6.x; k++)
                            {
                                this.CalculateCellSignalStrength(obstructSlopes, subArray, new int2(k, int6.y), @int, int2, componentData.m_Range, position);
                            }
                            for (int num2 = int4.x - 1; num2 >= int5.x; num2--)
                            {
                                this.CalculateCellSignalStrength(obstructSlopes, subArray, new int2(num2, int6.y), @int, int2, componentData.m_Range, position);
                            }
                            int6.y++;
                        }
                        if (int5.x > @int.x)
                        {
                            int5.x--;
                            for (int l = int4.y; l < int6.y; l++)
                            {
                                this.CalculateCellSignalStrength(obstructSlopes, subArray, new int2(int5.x, l), @int, int2, componentData.m_Range, position);
                            }
                            for (int num3 = int4.y - 1; num3 >= int5.y; num3--)
                            {
                                this.CalculateCellSignalStrength(obstructSlopes, subArray, new int2(int5.x, num3), @int, int2, componentData.m_Range, position);
                            }
                        }
                        if (int6.x < int2.x)
                        {
                            for (int m = int4.y; m < int6.y; m++)
                            {
                                this.CalculateCellSignalStrength(obstructSlopes, subArray, new int2(int6.x, m), @int, int2, componentData.m_Range, position);
                            }
                            for (int num4 = int4.y - 1; num4 >= int5.y; num4--)
                            {
                                this.CalculateCellSignalStrength(obstructSlopes, subArray, new int2(int6.x, num4), @int, int2, componentData.m_Range, position);
                            }
                            int6.x++;
                        }
                    }
                }
                this.AddSignalStrengths(facilityData, subArray, @int, int2);
            }
        }

        private float GetEfficiencyFactor(NativeArray<Game.Buildings.TelecomFacility> telecomFacilities, NativeArray<Temp> temps, BufferAccessor<Efficiency> efficiencyAccessor, int i)
        {
            float result = 1f;
            if (temps.Length != 0)
            {
                Temp temp = temps[i];
                if (this.m_BuildingEfficiencyData.TryGetBuffer(temp.m_Original, out var bufferData))
                {
                    Game.Buildings.TelecomFacility telecomFacility = this.m_TelecomFacilityData[temp.m_Original];
                    if (!this.m_Preview || (telecomFacility.m_Flags & TelecomFacilityFlags.HasCoverage) != 0)
                    {
                        result = BuildingUtils.GetEfficiency(bufferData);
                    }
                }
            }
            else if (efficiencyAccessor.Length != 0)
            {
                Game.Buildings.TelecomFacility telecomFacility2 = telecomFacilities[i];
                if (!this.m_Preview || (telecomFacility2.m_Flags & TelecomFacilityFlags.HasCoverage) != 0)
                {
                    result = BuildingUtils.GetEfficiency(efficiencyAccessor[i]);
                }
            }
            return result;
        }

        private void AddSignalStrengths(NativeArray<CellFacilityData> facilityData, NativeArray<float> signalStrengthArray, int2 min, int2 max)
        {
            int2 @int = max - min;
            int num = 128 * min.y;
            int num2 = -min.x;
            for (int i = min.y; i < max.y; i++)
            {
                for (int j = min.x; j < max.x; j++)
                {
                    float num3 = signalStrengthArray[num2 + j];
                    int index = num + j;
                    CellFacilityData value = facilityData[index];
                    value.m_SignalStrength = 1f - (1f - value.m_SignalStrength) * (1f - num3);
                    value.m_AccumulatedSignalStrength += num3;
                    facilityData[index] = value;
                }
                num += 128;
                num2 += @int.x;
            }
        }

        private void CalculateSignalStrength(NativeArray<float> signalStrengthArray, int2 min, int2 max, float range, float3 position)
        {
            int2 @int = max - min;
            int num = -min.x;
            for (int i = min.y; i < max.y; i++)
            {
                for (int j = min.x; j < max.x; j++)
                {
                    float3 cellCenter = CellMapSystemRe.GetCellCenter(new int2(j, i), 128); //
                    float distance = math.length((position - cellCenter).xz);
                    signalStrengthArray[num + j] = math.max(0f, this.CalculateSignalStrength(distance, range));
                }
                num += @int.x;
            }
        }

        private void ResetObstructAngles(NativeArray<float> obstructAngles, int2 min, int2 max)
        {
            int2 @int = max - min;
            int num = @int.x * @int.y;
            for (int i = 0; i < num; i++)
            {
                obstructAngles[i] = float.MaxValue;
            }
        }

        private float CalculateSignalStrength(float distance, float range)
        {
            float num = distance / range;
            num *= num;
            return 1f - num;
        }

        private void CalculateCellSignalStrength(NativeArray<float> obstructSlopes, NativeArray<float> signalStrengthArray, int2 cell, int2 min, int2 max, float range, float3 position)
        {
            int2 @int = cell - min;
            int2 int2 = max - min;
            int index = @int.x + int2.x * @int.y;
            float3 cellCenter = CellMapSystemRe.GetCellCenter(cell, 128); //
            float3 @float = position - cellCenter;
            float num = math.length(@float.xz);
            float num2 = this.CalculateSignalStrength(num, range);
            if (num2 <= 0f)
            {
                signalStrengthArray[index] = 0f;
                return;
            }
            cellCenter.y = TerrainUtils.SampleHeight(ref this.m_TerrainHeightData, cellCenter);
            @float.y = position.y - cellCenter.y;
            float num3 = @float.y / math.max(1f, num);
            float num4 = (float)CellMapSystemRe.kMapSize / 128f; //
            float2 float2 = math.abs(@float.xz);
            int2 int3 = math.clamp(@int + math.select((int2)math.sign(@float.xz), 0, math.all(float2 < num4)), 0, int2 - 1);
            int2 int4;
            float t;
            if (float2.x >= float2.y)
            {
                int4 = int3.x + int2.x * new int2(@int.y, int3.y);
                t = float2.y / math.max(1f, float2.x);
            }
            else
            {
                int4 = new int2(@int.x, int3.x) + int2.x * int3.y;
                t = float2.x / math.max(1f, float2.y);
            }
            float2 float3 = new float2(obstructSlopes[int4.x], obstructSlopes[int4.y]);
            float2 float4 = math.saturate((float3 - num3) * 20f + 1f);
            obstructSlopes[index] = math.min(math.lerp(float3.x, float3.y, t), num3);
            signalStrengthArray[index] = num2 * math.lerp(float4.x, float4.y, t);
        }

        private void AddDensity(NativeArray<CellDensityData> densityData, ArchetypeChunk chunk)
        {
            NativeArray<PropertyRenter> nativeArray = chunk.GetNativeArray(ref this.m_PropertyRenterType);
            if (nativeArray.Length != 0)
            {
                BufferAccessor<HouseholdCitizen> bufferAccessor = chunk.GetBufferAccessor(ref this.m_HouseholdCitizenType);
                BufferAccessor<Employee> bufferAccessor2 = chunk.GetBufferAccessor(ref this.m_EmployeeType);
                for (int i = 0; i < bufferAccessor.Length; i++)
                {
                    PropertyRenter propertyRenter = nativeArray[i];
                    DynamicBuffer<HouseholdCitizen> dynamicBuffer = bufferAccessor[i];
                    if (dynamicBuffer.Length != 0 && this.m_TransformData.HasComponent(propertyRenter.m_Property))
                    {
                        Transform transform = this.m_TransformData[propertyRenter.m_Property];
                        this.AddDensity(densityData, dynamicBuffer.Length, transform.m_Position);
                    }
                }
                for (int j = 0; j < bufferAccessor2.Length; j++)
                {
                    PropertyRenter propertyRenter2 = nativeArray[j];
                    DynamicBuffer<Employee> dynamicBuffer2 = bufferAccessor2[j];
                    if (dynamicBuffer2.Length != 0 && this.m_TransformData.HasComponent(propertyRenter2.m_Property))
                    {
                        Transform transform2 = this.m_TransformData[propertyRenter2.m_Property];
                        this.AddDensity(densityData, dynamicBuffer2.Length, transform2.m_Position);
                    }
                }
                return;
            }
            NativeArray<Transform> nativeArray2 = chunk.GetNativeArray(ref this.m_TransformType);
            if (nativeArray2.Length == 0)
            {
                return;
            }
            BufferAccessor<HouseholdCitizen> bufferAccessor3 = chunk.GetBufferAccessor(ref this.m_HouseholdCitizenType);
            BufferAccessor<Employee> bufferAccessor4 = chunk.GetBufferAccessor(ref this.m_EmployeeType);
            for (int k = 0; k < bufferAccessor3.Length; k++)
            {
                Transform transform3 = nativeArray2[k];
                DynamicBuffer<HouseholdCitizen> dynamicBuffer3 = bufferAccessor3[k];
                if (dynamicBuffer3.Length != 0)
                {
                    this.AddDensity(densityData, dynamicBuffer3.Length, transform3.m_Position);
                }
            }
            for (int l = 0; l < bufferAccessor4.Length; l++)
            {
                Transform transform4 = nativeArray2[l];
                DynamicBuffer<Employee> dynamicBuffer4 = bufferAccessor4[l];
                if (dynamicBuffer4.Length != 0)
                {
                    this.AddDensity(densityData, dynamicBuffer4.Length, transform4.m_Position);
                }
            }
        }

        private void AddDensity(NativeArray<CellDensityData> densityData, int density, float3 position)
        {
            int2 @int = math.clamp(CellMapSystem<TelecomCoverage>.GetCell(position, CellMapSystemRe.kMapSize, 128), 0, 127); //
            int index = @int.x + 128 * @int.y;
            CellDensityData value = densityData[index];
            value.m_Density = (ushort)math.min(65535, value.m_Density + density);
            densityData[index] = value;
        }
    }

}

