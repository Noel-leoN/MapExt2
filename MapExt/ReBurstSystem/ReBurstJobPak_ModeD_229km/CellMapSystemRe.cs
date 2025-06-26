using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game.Prefabs;
using Game.Simulation;
using Unity.Assertions;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace MapExtPDX.MapExt.ReBurstSystemModeD
{
    /// <summary>
    /// 重定义CellMapSystem<T>基类及封闭派生类静态方法库
    /// 自定义BurstJob直接重定向引用本库，以确保CV值获得修改
    /// (BurstCompile无法编译Harmony或反射执行的修补)
    /// </summary>
    public static class CellMapSystemRe
    {
        // 等同于CV值；用于CellMapSystem/WaterSystem; 其他系统无BurstJob调用；
        public static readonly int MapSizeMultiplier = 16;
        public static readonly int kMapSize = MapSizeMultiplier * 14336;

        // WaterSystem CellSize/WaveSpeed
        public static readonly float kCellSize = MapSizeMultiplier * 7f;
        public static readonly float WaveSpeed = kCellSize / 30f;

        // Water-related System
        public static int TsunamiEndDelay => Mathf.RoundToInt(kMapSize / WaveSpeed);

        public static float3 GetCellCenter(int index, int textureSize)
        {
            int num = index % textureSize;
            int num2 = index / textureSize;
            int num3 = kMapSize / textureSize;
            return new float3(-0.5f * kMapSize + (num + 0.5f) * num3, 0f, -0.5f * kMapSize + (num2 + 0.5f) * num3);
        }

        public static float3 GetCellCenter(int2 cell, int textureSize)
        {
            int num = kMapSize / textureSize;
            return new float3(-0.5f * kMapSize + (cell.x + 0.5f) * num, 0f, -0.5f * kMapSize + (cell.y + 0.5f) * num);
        }

        public static Wind WindSystemGetWind(float3 position, NativeArray<Wind> windMap)
        {
            int2 cell = CellMapSystem<Wind>.GetCell(position, CellMapSystemRe.kMapSize, WindSystem.kTextureSize);
            cell = math.clamp(cell, 0, WindSystem.kTextureSize - 1);
            float2 cellCoords = CellMapSystem<Wind>.GetCellCoords(position, kMapSize, WindSystem.kTextureSize);
            int num = math.min(WindSystem.kTextureSize - 1, cell.x + 1);
            int num2 = math.min(WindSystem.kTextureSize - 1, cell.y + 1);
            Wind result = default;
            result.m_Wind = math.lerp(math.lerp(windMap[cell.x + WindSystem.kTextureSize * cell.y].m_Wind, windMap[num + WindSystem.kTextureSize * cell.y].m_Wind, cellCoords.x - cell.x), math.lerp(windMap[cell.x + WindSystem.kTextureSize * num2].m_Wind, windMap[num + WindSystem.kTextureSize * num2].m_Wind, cellCoords.x - cell.x), cellCoords.y - cell.y);
            return result;
        }

        public static AirPollution AirPollutionSystemGetPollution(float3 position, NativeArray<AirPollution> pollutionMap)
        {
            AirPollution result = default;
            float num = kMapSize / (float)AirPollutionSystem.kTextureSize;
            int2 cell = CellMapSystem<AirPollution>.GetCell(position - new float3(num / 2f, 0f, num / 2f), kMapSize, AirPollutionSystem.kTextureSize);
            float2 @float = CellMapSystem<AirPollution>.GetCellCoords(position, kMapSize, AirPollutionSystem.kTextureSize) - new float2(0.5f, 0.5f);
            cell = math.clamp(cell, 0, AirPollutionSystem.kTextureSize - 2);
            short pollution = pollutionMap[cell.x + AirPollutionSystem.kTextureSize * cell.y].m_Pollution;
            short pollution2 = pollutionMap[cell.x + 1 + AirPollutionSystem.kTextureSize * cell.y].m_Pollution;
            short pollution3 = pollutionMap[cell.x + AirPollutionSystem.kTextureSize * (cell.y + 1)].m_Pollution;
            short pollution4 = pollutionMap[cell.x + 1 + AirPollutionSystem.kTextureSize * (cell.y + 1)].m_Pollution;
            result.m_Pollution = (short)math.round(math.lerp(math.lerp(pollution, pollution2, @float.x - cell.x), math.lerp(pollution3, pollution4, @float.x - cell.x), @float.y - cell.y));
            return result;
        }

        public static AvailabilityInfoCell AvailabilityInfoToGridSystemGetAvailabilityInfo(float3 position, NativeArray<AvailabilityInfoCell> AvailabilityInfoMap)
        {
            AvailabilityInfoCell result = default;
            int2 cell = CellMapSystem<AvailabilityInfoCell>.GetCell(position, kMapSize, AvailabilityInfoToGridSystem.kTextureSize);
            float2 cellCoords = CellMapSystem<AvailabilityInfoCell>.GetCellCoords(position, kMapSize, AvailabilityInfoToGridSystem.kTextureSize);
            if (cell.x < 0 || cell.x >= AvailabilityInfoToGridSystem.kTextureSize || cell.y < 0 || cell.y >= AvailabilityInfoToGridSystem.kTextureSize)
            {
                return default;
            }

            float4 availabilityInfo = AvailabilityInfoMap[cell.x + AvailabilityInfoToGridSystem.kTextureSize * cell.y].m_AvailabilityInfo;
            float4 y = cell.x < AvailabilityInfoToGridSystem.kTextureSize - 1 ? AvailabilityInfoMap[cell.x + 1 + AvailabilityInfoToGridSystem.kTextureSize * cell.y].m_AvailabilityInfo : (float4)0;
            float4 x = cell.y < AvailabilityInfoToGridSystem.kTextureSize - 1 ? AvailabilityInfoMap[cell.x + AvailabilityInfoToGridSystem.kTextureSize * (cell.y + 1)].m_AvailabilityInfo : (float4)0;
            float4 y2 = cell.x < AvailabilityInfoToGridSystem.kTextureSize - 1 && cell.y < AvailabilityInfoToGridSystem.kTextureSize - 1 ? AvailabilityInfoMap[cell.x + 1 + AvailabilityInfoToGridSystem.kTextureSize * (cell.y + 1)].m_AvailabilityInfo : (float4)0;
            result.m_AvailabilityInfo = math.lerp(math.lerp(availabilityInfo, y, cellCoords.x - cell.x), math.lerp(x, y2, cellCoords.x - cell.x), cellCoords.y - cell.y);
            return result;
        }

        public static GroundPollution GroundPollutionSystemGetPollution(float3 position, NativeArray<GroundPollution> pollutionMap)
        {
            GroundPollution result = default;
            int2 cell = CellMapSystem<GroundPollution>.GetCell(position, kMapSize, GroundPollutionSystem.kTextureSize);
            float2 cellCoords = CellMapSystem<GroundPollution>.GetCellCoords(position, kMapSize, GroundPollutionSystem.kTextureSize);
            if (cell.x < 0 || cell.x >= GroundPollutionSystem.kTextureSize || cell.y < 0 || cell.y >= GroundPollutionSystem.kTextureSize)
            {
                return result;
            }

            GroundPollution groundPollution = pollutionMap[cell.x + GroundPollutionSystem.kTextureSize * cell.y];
            GroundPollution groundPollution2 = cell.x < GroundPollutionSystem.kTextureSize - 1 ? pollutionMap[cell.x + 1 + GroundPollutionSystem.kTextureSize * cell.y] : default;
            GroundPollution groundPollution3 = cell.y < GroundPollutionSystem.kTextureSize - 1 ? pollutionMap[cell.x + GroundPollutionSystem.kTextureSize * (cell.y + 1)] : default;
            GroundPollution groundPollution4 = cell.x < GroundPollutionSystem.kTextureSize - 1 && cell.y < GroundPollutionSystem.kTextureSize - 1 ? pollutionMap[cell.x + 1 + GroundPollutionSystem.kTextureSize * (cell.y + 1)] : default;
            result.m_Pollution = (short)Mathf.RoundToInt(math.lerp(math.lerp(groundPollution.m_Pollution, groundPollution2.m_Pollution, cellCoords.x - cell.x), math.lerp(groundPollution3.m_Pollution, groundPollution4.m_Pollution, cellCoords.x - cell.x), cellCoords.y - cell.y));
            return result;
        }

        public static NoisePollution NoisePollutionSystemGetPollution(float3 position, NativeArray<NoisePollution> pollutionMap)
        {
            NoisePollution result = default;
            float num = kMapSize / (float)NoisePollutionSystem.kTextureSize;
            int2 cell = CellMapSystem<NoisePollution>.GetCell(position - new float3(num / 2f, 0f, num / 2f), kMapSize, NoisePollutionSystem.kTextureSize);
            float2 @float = CellMapSystem<NoisePollution>.GetCellCoords(position, kMapSize, NoisePollutionSystem.kTextureSize) - new float2(0.5f, 0.5f);
            cell = math.clamp(cell, 0, NoisePollutionSystem.kTextureSize - 2);
            short pollution = pollutionMap[cell.x + NoisePollutionSystem.kTextureSize * cell.y].m_Pollution;
            short pollution2 = pollutionMap[cell.x + 1 + NoisePollutionSystem.kTextureSize * cell.y].m_Pollution;
            short pollution3 = pollutionMap[cell.x + NoisePollutionSystem.kTextureSize * (cell.y + 1)].m_Pollution;
            short pollution4 = pollutionMap[cell.x + 1 + NoisePollutionSystem.kTextureSize * (cell.y + 1)].m_Pollution;
            result.m_Pollution = (short)Mathf.RoundToInt(math.lerp(math.lerp(pollution, pollution2, @float.x - cell.x), math.lerp(pollution3, pollution4, @float.x - cell.x), @float.y - cell.y));
            return result;
        }

        public static float ZoneAmbienceSystemGetZoneAmbience(GroupAmbienceType type, float3 position, NativeArray<ZoneAmbienceCell> zoneAmbienceMap, float maxPerCell)
        {
            int2 cell = CellMapSystem<ZoneAmbienceCell>.GetCell(position, CellMapSystemRe.kMapSize, ZoneAmbienceSystem.kTextureSize);
            float num = 0f;
            float num2 = 0f;
            for (int i = cell.x - 2; i <= cell.x + 2; i++)
            {
                for (int j = cell.y - 2; j <= cell.y + 2; j++)
                {
                    if (i >= 0 && i < ZoneAmbienceSystem.kTextureSize && j >= 0 && j < ZoneAmbienceSystem.kTextureSize)
                    {
                        int index = i + ZoneAmbienceSystem.kTextureSize * j;
                        float num3 = math.max(1f, math.distancesq(GetCellCenter(index, ZoneAmbienceSystem.kTextureSize), position) / 10f);
                        num += math.min(maxPerCell, zoneAmbienceMap[index].m_Value.GetAmbience(type)) / num3;
                        num2 += 1f / num3;
                    }
                }
            }

            return num / num2;
        }

        public static float3 WindSimulationSystemGetCellCenter(int index)
        {
            int3 @int = new int3(index % WindSimulationSystem.kResolution.x, index / WindSimulationSystem.kResolution.x % WindSimulationSystem.kResolution.y, index / (WindSimulationSystem.kResolution.x * WindSimulationSystem.kResolution.y));
            float3 result = CellMapSystemRe.kMapSize * new float3((@int.x + 0.5f) / WindSimulationSystem.kResolution.x, 0f, (@int.y + 0.5f) / WindSimulationSystem.kResolution.y) - CellMapSystemRe.kMapSize / 2;
            result.y = 100f + 1024f * (@int.z + 0.5f) / WindSimulationSystem.kResolution.z;
            return result;
        }

        public static float TerrainAttractivenessSystemEvaluateAttractiveness(float3 position, CellMapData<TerrainAttractiveness> data, TerrainHeightData heightData, AttractivenessParameterData parameters, NativeArray<int> factors)
        {
            float num = TerrainUtils.SampleHeight(ref heightData, position);
            TerrainAttractiveness attractiveness = TerrainAttractivenessSystemGetAttractiveness(position, data.m_Buffer);
            float num2 = parameters.m_ForestEffect * attractiveness.m_ForestBonus;
            AttractionSystem.SetFactor(factors, AttractionSystem.AttractivenessFactor.Forest, num2);
            float num3 = parameters.m_ShoreEffect * attractiveness.m_ShoreBonus;
            AttractionSystem.SetFactor(factors, AttractionSystem.AttractivenessFactor.Beach, num3);
            float num4 = math.min(parameters.m_HeightBonus.z, math.max(0f, num - parameters.m_HeightBonus.x) * parameters.m_HeightBonus.y);
            AttractionSystem.SetFactor(factors, AttractionSystem.AttractivenessFactor.Height, num4);
            return num2 + num3 + num4;
        }

        public static TerrainAttractiveness TerrainAttractivenessSystemGetAttractiveness(float3 position, NativeArray<TerrainAttractiveness> attractivenessMap)
        {
            TerrainAttractiveness result = default;
            int2 cell = CellMapSystem<TerrainAttractiveness>.GetCell(position, kMapSize, TerrainAttractivenessSystem.kTextureSize);
            float2 cellCoords = CellMapSystem<TerrainAttractiveness>.GetCellCoords(position, kMapSize, TerrainAttractivenessSystem.kTextureSize);
            if (cell.x < 0 || cell.x >= TerrainAttractivenessSystem.kTextureSize || cell.y < 0 || cell.y >= TerrainAttractivenessSystem.kTextureSize)
            {
                return result;
            }

            TerrainAttractiveness terrainAttractiveness = attractivenessMap[cell.x + TerrainAttractivenessSystem.kTextureSize * cell.y];
            TerrainAttractiveness terrainAttractiveness2 = cell.x < TerrainAttractivenessSystem.kTextureSize - 1 ? attractivenessMap[cell.x + 1 + TerrainAttractivenessSystem.kTextureSize * cell.y] : default;
            TerrainAttractiveness terrainAttractiveness3 = cell.y < TerrainAttractivenessSystem.kTextureSize - 1 ? attractivenessMap[cell.x + TerrainAttractivenessSystem.kTextureSize * (cell.y + 1)] : default;
            TerrainAttractiveness terrainAttractiveness4 = cell.x < TerrainAttractivenessSystem.kTextureSize - 1 && cell.y < TerrainAttractivenessSystem.kTextureSize - 1 ? attractivenessMap[cell.x + 1 + TerrainAttractivenessSystem.kTextureSize * (cell.y + 1)] : default;
            result.m_ForestBonus = (short)Mathf.RoundToInt(math.lerp(math.lerp(terrainAttractiveness.m_ForestBonus, terrainAttractiveness2.m_ForestBonus, cellCoords.x - cell.x), math.lerp(terrainAttractiveness3.m_ForestBonus, terrainAttractiveness4.m_ForestBonus, cellCoords.x - cell.x), cellCoords.y - cell.y));
            result.m_ShoreBonus = (short)Mathf.RoundToInt(math.lerp(math.lerp(terrainAttractiveness.m_ShoreBonus, terrainAttractiveness2.m_ShoreBonus, cellCoords.x - cell.x), math.lerp(terrainAttractiveness3.m_ShoreBonus, terrainAttractiveness4.m_ShoreBonus, cellCoords.x - cell.x), cellCoords.y - cell.y));
            return result;
        }

        public static TrafficAmbienceCell TrafficAmbienceSystemGetTrafficAmbience2(float3 position, NativeArray<TrafficAmbienceCell> trafficAmbienceMap, float maxPerCell)
        {
            TrafficAmbienceCell result = default;
            int2 cell = CellMapSystem<TrafficAmbienceCell>.GetCell(position, kMapSize, TrafficAmbienceSystem.kTextureSize);
            float num = 0f;
            float num2 = 0f;
            for (int i = cell.x - 2; i <= cell.x + 2; i++)
            {
                for (int j = cell.y - 2; j <= cell.y + 2; j++)
                {
                    if (i >= 0 && i < TrafficAmbienceSystem.kTextureSize && j >= 0 && j < TrafficAmbienceSystem.kTextureSize)
                    {
                        int index = i + TrafficAmbienceSystem.kTextureSize * j;
                        float num3 = math.max(1f, math.distancesq(GetCellCenter(index, TrafficAmbienceSystem.kTextureSize), position));
                        num += math.min(maxPerCell, trafficAmbienceMap[index].m_Traffic) / num3;
                        num2 += 1f / num3;
                    }
                }
            }

            result.m_Traffic = num / num2;
            return result;
        }

        public static float ZoneAmbienceSystemGetZoneAmbienceNear(GroupAmbienceType type, float3 position, NativeArray<ZoneAmbienceCell> zoneAmbienceMap, float nearWeight, float maxPerCell)
        {
            int2 cell = CellMapSystem<ZoneAmbienceCell>.GetCell(position, kMapSize, ZoneAmbienceSystem.kTextureSize);
            float num = 0f;
            float num2 = 0f;
            for (int i = cell.x - 2; i <= cell.x + 2; i++)
            {
                for (int j = cell.y - 2; j <= cell.y + 2; j++)
                {
                    if (i >= 0 && i < ZoneAmbienceSystem.kTextureSize && j >= 0 && j < ZoneAmbienceSystem.kTextureSize)
                    {
                        int index = i + ZoneAmbienceSystem.kTextureSize * j;
                        float num3 = math.max(1f, math.pow(math.distance(GetCellCenter(index, ZoneAmbienceSystem.kTextureSize), position) / 10f, 1f + nearWeight));
                        num += math.min(maxPerCell, zoneAmbienceMap[index].m_Value.GetAmbience(type)) / num3;
                        num2 += 1f / num3;
                    }
                }
            }

            return num / num2;
        }

        public static GroundWater GroundWaterSystemGetGroundWater(float3 position, NativeArray<GroundWater> groundWaterMap)
        {
            float2 @float = CellMapSystem<GroundWater>.GetCellCoords(position, CellMapSystemRe.kMapSize, GroundWaterSystem.kTextureSize) - new float2(0.5f, 0.5f);
            int2 cell = new int2(Mathf.FloorToInt(@float.x), Mathf.FloorToInt(@float.y));
            int2 cell2 = new int2(cell.x + 1, cell.y);
            int2 cell3 = new int2(cell.x, cell.y + 1);
            int2 cell4 = new int2(cell.x + 1, cell.y + 1);
            GroundWater groundWater = GroundWaterSystemGetGroundWater(groundWaterMap, cell);
            GroundWater groundWater2 = GroundWaterSystemGetGroundWater(groundWaterMap, cell2);
            GroundWater groundWater3 = GroundWaterSystemGetGroundWater(groundWaterMap, cell3);
            GroundWater groundWater4 = GroundWaterSystemGetGroundWater(groundWaterMap, cell4);
            float sx = @float.x - (float)cell.x;
            float sy = @float.y - (float)cell.y;
            GroundWater result = default(GroundWater);
            result.m_Amount = (short)math.round(GroundWaterSystemBilinear(groundWater.m_Amount, groundWater2.m_Amount, groundWater3.m_Amount, groundWater4.m_Amount, sx, sy));
            result.m_Polluted = (short)math.round(GroundWaterSystemBilinear(groundWater.m_Polluted, groundWater2.m_Polluted, groundWater3.m_Polluted, groundWater4.m_Polluted, sx, sy));
            result.m_Max = (short)math.round(GroundWaterSystemBilinear(groundWater.m_Max, groundWater2.m_Max, groundWater3.m_Max, groundWater4.m_Max, sx, sy));
            return result;
        }
        private static GroundWater GroundWaterSystemGetGroundWater(NativeArray<GroundWater> groundWaterMap, int2 cell)
        {
            if (!GroundWaterSystemIsValidCell(cell))
            {
                return default(GroundWater);
            }

            return groundWaterMap[cell.x + GroundWaterSystem.kTextureSize * cell.y];
        }

        private static void GroundWaterSystemSetGroundWater(NativeArray<GroundWater> groundWaterMap, int2 cell, GroundWater gw)
        {
            if (GroundWaterSystemIsValidCell(cell))
            {
                groundWaterMap[cell.x + GroundWaterSystem.kTextureSize * cell.y] = gw;
            }
        }

        public static bool GroundWaterSystemIsValidCell(int2 cell)
        {
            if (cell.x >= 0 && cell.y >= 0 && cell.x < GroundWaterSystem.kTextureSize)
            {
                return cell.y < GroundWaterSystem.kTextureSize;
            }

            return false;
        }

        public static bool GroundWaterSystemTryGetCell(float3 position, out int2 cell)
        {
            cell = CellMapSystem<GroundWater>.GetCell(position, CellMapSystemRe.kMapSize, GroundWaterSystem.kTextureSize);
            return GroundWaterSystemIsValidCell(cell);
        }

        private static float GroundWaterSystemBilinear(short v00, short v10, short v01, short v11, float sx, float sy)
        {
            return math.lerp(math.lerp(v00, v10, sx), math.lerp(v01, v11, sx), sy);
        }

        public static void GroundWaterSystemConsumeGroundWater(float3 position, NativeArray<GroundWater> groundWaterMap, int amount)
        {
            Assert.IsTrue(amount >= 0);
            float2 @float = CellMapSystem<GroundWater>.GetCellCoords(position, CellMapSystemRe.kMapSize, GroundWaterSystem.kTextureSize) - new float2(0.5f, 0.5f);
            int2 cell = new int2(Mathf.FloorToInt(@float.x), Mathf.FloorToInt(@float.y));
            int2 cell2 = new int2(cell.x + 1, cell.y);
            int2 cell3 = new int2(cell.x, cell.y + 1);
            int2 cell4 = new int2(cell.x + 1, cell.y + 1);
            GroundWater gw2 = GroundWaterSystemGetGroundWater(groundWaterMap, cell);
            GroundWater gw3 = GroundWaterSystemGetGroundWater(groundWaterMap, cell2);
            GroundWater gw4 = GroundWaterSystemGetGroundWater(groundWaterMap, cell3);
            GroundWater gw5 = GroundWaterSystemGetGroundWater(groundWaterMap, cell4);
            float sx = @float.x - (float)cell.x;
            float sy = @float.y - (float)cell.y;
            float num = math.ceil(GroundWaterSystemBilinear(gw2.m_Amount, 0, 0, 0, sx, sy));
            float num2 = math.ceil(GroundWaterSystemBilinear(0, gw3.m_Amount, 0, 0, sx, sy));
            float num3 = math.ceil(GroundWaterSystemBilinear(0, 0, gw4.m_Amount, 0, sx, sy));
            float num4 = math.ceil(GroundWaterSystemBilinear(0, 0, 0, gw5.m_Amount, sx, sy));
            float totalAvailable = num + num2 + num3 + num4;
            float totalConsumed = math.min(amount, totalAvailable);
            if (totalAvailable < (float)amount)
            {
                UnityEngine.Debug.LogWarning($"Trying to consume more groundwater than available! amount: {amount}, available: {totalAvailable}");
            }

            ConsumeFraction(ref gw2, num);
            ConsumeFraction(ref gw3, num2);
            ConsumeFraction(ref gw4, num3);
            ConsumeFraction(ref gw5, num4);
            Assert.IsTrue(Mathf.Approximately(totalAvailable, 0f));
            Assert.IsTrue(Mathf.Approximately(totalConsumed, 0f));
            GroundWaterSystemSetGroundWater(groundWaterMap, cell, gw2);
            GroundWaterSystemSetGroundWater(groundWaterMap, cell2, gw3);
            GroundWaterSystemSetGroundWater(groundWaterMap, cell3, gw4);
            GroundWaterSystemSetGroundWater(groundWaterMap, cell4, gw5);
            void ConsumeFraction(ref GroundWater gw, float cellAvailable)
            {
                if (!(totalAvailable < 0.5f))
                {
                    float num5 = cellAvailable / totalAvailable;
                    totalAvailable -= cellAvailable;
                    float num6 = math.max(y: math.max(0f, totalConsumed - totalAvailable), x: math.round(num5 * totalConsumed));
                    Assert.IsTrue(num6 <= (float)gw.m_Amount);
                    gw.Consume((int)num6);
                    totalConsumed -= num6;
                }
            }
        }


    }
}
