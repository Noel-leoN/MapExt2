// [AUTO-GENERATED] 由 XCellMapSystemRe.Generate.ps1 从 XCellMapSystemRe.cs 自动生成，请勿手动编辑
// Mode: ReBurstSystemModeC, kMapSize: 114688
// kTextureSize 倍率由 CellMapTextureSizeMultiplier 在编译时自动计算

using Colossal.Entities;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Prefabs;
using Game.Simulation;
using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MapExtPDX.ModeC
{
    /// <summary>
    /// 重定义CellMapSystem<T>.kMapSize/ClosedGenericType.kTextureSize
    /// 基类和派生封闭类型以及外部系统直接或间接调用的静态方法库
    /// 集中管理自定义BurstJob直接重定向引用本库，以确保CV值正确编译
    /// </summary>
    public static class XCellMapSystemRe
    {
        #region kMapSize / kTextureSize 常量定义

        // 变更MapSizeModeA/B/C请修改该值
        // 等同于CV值；用于CellMapSystem/WaterSystem; 其他系统无BurstJob调用；
        // public static readonly int MapSizeMultiplier = 4;
        public const int kMapSize = 114688; //MapSizeMultiplier * 14336;

        // WaterSystem CellSize/WaveSpeed
        public const float kCellSize = kMapSize / 14336 * 7f; //28f; //MapSizeMultiplier * 7f;
        public const float WaveSpeed = kMapSize / 14336 * 7f / 30f; //kCellSize / 30f;

        // Water-related System
        public static int TsunamiEndDelay => (int)math.round(kMapSize / WaveSpeed);

        /// <summary>
        /// v2.2.0版本 将CellMapSystem模拟计算的贴图分辨率提升到原版水平
        /// </summary>
        // 核心调整倍率；57344->4, 28672->2,114688->1(为避免超出Unity贴图限制使用原值),14336->1;
        public const int CellMapTextureSizeMultiplier = kMapSize >= 114688 ? 1 : (kMapSize / 14336);

        // CellMapSystem<T>封闭类型TextureSize字段赋值
        public const int AirPollutionSystemkTextureSize = CellMapTextureSizeMultiplier * 256;
        public const int AvailabilityInfoToGridSystemkTextureSize = CellMapTextureSizeMultiplier * 128;
        public const int GroundPollutionSystemkTextureSize = CellMapTextureSizeMultiplier * 256;
        public const int GroundWaterSystemkTextureSize = CellMapTextureSizeMultiplier * 256;
        public const int LandValueSystemkTextureSize = CellMapTextureSizeMultiplier * 128;
        public const int NaturalResourceSystemkTextureSize = CellMapTextureSizeMultiplier * 256;
        public const int NoisePollutionSystemkTextureSize = CellMapTextureSizeMultiplier * 256;
        public const int PopulationToGridSystemkTextureSize = CellMapTextureSizeMultiplier * 64;

        public const int SoilWaterSystemkTextureSize = CellMapTextureSizeMultiplier * 128;
        public const int SoilWaterSystemkLoadDistribution = CellMapTextureSizeMultiplier * 8; // 需同步修改

        public const int TerrainAttractivenessSystemkTextureSize = CellMapTextureSizeMultiplier * 128;
        public const int TrafficAmbienceSystemkTextureSize = CellMapTextureSizeMultiplier * 64;
        public const int ZoneAmbienceSystemkTextureSize = CellMapTextureSizeMultiplier * 64;

        // TelecomPreviewSystem/TelecomCoverageSystem 硬编码 128
        public const int TelecomCoverageSystemkTextureSize = /*CellMapTextureSizeMultiplier * */ 128;

        // 风场贴图变化需同步修改kChangeFactor,并修改序列化/反序列化硬编码
        // 暂不修改
        public const int WindSystemkTextureSize = /*CellMapTextureSizeMultiplier * */ 64;
        // public static readonly int3 WindSimulationSystemkResolution = new(WindSystemkTextureSize, WindSystemkTextureSize, 16); // z代表风场垂直分层

        #endregion

        #region 基类静态方法重定向

        public static int2 GetCell(float3 position, int mapSize, int textureSize)
        {
            return (int2)math.floor((0.5f + position.xz / mapSize) * textureSize);
        }

        public static float2 GetCellCoords(float3 position, int mapSize, int textureSize)
        {
            return (0.5f + position.xz / mapSize) * textureSize;
        }

        public static float3 GetCellCenter(int index, int textureSize)
        {
            int num = index % textureSize;
            int num2 = index / textureSize;
            int num3 = kMapSize / textureSize;
            return new float3(-0.5f * kMapSize + (num + 0.5f) * num3, 0f, -0.5f * kMapSize + (num2 + 0.5f) * num3);
        }

        public static float3 GetCellCenter(int2 cell, int textureSize)
        {
            int num = unchecked(kMapSize / textureSize);
            return new float3(-0.5f * kMapSize + (cell.x + 0.5f) * num, 0f, -0.5f * kMapSize + (cell.y + 0.5f) * num);
        }

        #endregion

        #region 派生封闭类型的静态方法重定向

        #region AirPollutionSystem

        public static float3 AirPollutionSystemGetCellCenter(int index) =>
            GetCellCenter(index, AirPollutionSystemkTextureSize);

        public static AirPollution AirPollutionSystemGetPollution(float3 position,
            NativeArray<AirPollution> pollutionMap)
        {
            AirPollution result = default;
            float num = kMapSize / (float)AirPollutionSystemkTextureSize;
            int2 cell = GetCell(position - new float3(num / 2f, 0f, num / 2f), kMapSize,
                AirPollutionSystemkTextureSize);
            float2 float5 = GetCellCoords(position, kMapSize, AirPollutionSystemkTextureSize) - new float2(0.5f, 0.5f);
            unchecked
            {
                cell = math.clamp(cell, 0, AirPollutionSystemkTextureSize - 2);
                short pollution = pollutionMap[cell.x + AirPollutionSystemkTextureSize * cell.y].m_Pollution;
                short pollution2 = pollutionMap[cell.x + 1 + AirPollutionSystemkTextureSize * cell.y].m_Pollution;
                short pollution3 = pollutionMap[cell.x + AirPollutionSystemkTextureSize * (cell.y + 1)].m_Pollution;
                short pollution4 = pollutionMap[cell.x + 1 + AirPollutionSystemkTextureSize * (cell.y + 1)].m_Pollution;
                result.m_Pollution = (short)math.round(math.lerp(math.lerp(pollution, pollution2, float5.x - cell.x),
                    math.lerp(pollution3, pollution4, float5.x - cell.x), float5.y - cell.y));
                return result;
            }
        }

        #endregion

        #region AvailabilityInfoToGridSystem

        public static float3 AvailabilityInfoToGridSystemGetCellCenter(int index) =>
            GetCellCenter(index, AvailabilityInfoToGridSystemkTextureSize);

        public static AvailabilityInfoCell AvailabilityInfoToGridSystemGetAvailabilityInfo(float3 position,
            NativeArray<AvailabilityInfoCell> AvailabilityInfoMap)
        {
            AvailabilityInfoCell result = default;
            int2 cell = GetCell(position, kMapSize, AvailabilityInfoToGridSystemkTextureSize);
            float2 cellCoords = GetCellCoords(position, kMapSize, AvailabilityInfoToGridSystemkTextureSize);
            if (cell.x < 0 || cell.x >= AvailabilityInfoToGridSystemkTextureSize || cell.y < 0 ||
                cell.y >= AvailabilityInfoToGridSystemkTextureSize)
            {
                return default;
            }

            unchecked
            {
                float4 availabilityInfo =
                    AvailabilityInfoMap[cell.x + AvailabilityInfoToGridSystemkTextureSize * cell.y].m_AvailabilityInfo;
                float4 y = cell.x < AvailabilityInfoToGridSystemkTextureSize - 1
                    ? AvailabilityInfoMap[cell.x + 1 + AvailabilityInfoToGridSystemkTextureSize * cell.y]
                        .m_AvailabilityInfo
                    : (float4)0;
                float4 x = cell.y < AvailabilityInfoToGridSystemkTextureSize - 1
                    ? AvailabilityInfoMap[cell.x + AvailabilityInfoToGridSystemkTextureSize * (cell.y + 1)]
                        .m_AvailabilityInfo
                    : (float4)0;
                float4 y2 =
                    cell.x < AvailabilityInfoToGridSystemkTextureSize - 1 &&
                    cell.y < AvailabilityInfoToGridSystemkTextureSize - 1
                        ? AvailabilityInfoMap[cell.x + 1 + AvailabilityInfoToGridSystemkTextureSize * (cell.y + 1)]
                            .m_AvailabilityInfo
                        : (float4)0;
                result.m_AvailabilityInfo = math.lerp(math.lerp(availabilityInfo, y, cellCoords.x - cell.x),
                    math.lerp(x, y2, cellCoords.x - cell.x), cellCoords.y - cell.y);
                return result;
            }
        }

        #endregion

        #region GroundPollutionSystem

        public static GroundPollution GroundPollutionSystemGetPollution(float3 position,
            NativeArray<GroundPollution> pollutionMap)
        {
            GroundPollution result = default;
            int2 cell = GetCell(position, kMapSize, GroundPollutionSystemkTextureSize);
            float2 cellCoords = GetCellCoords(position, kMapSize, GroundPollutionSystemkTextureSize);
            if (cell.x < 0 || cell.x >= GroundPollutionSystemkTextureSize || cell.y < 0 ||
                cell.y >= GroundPollutionSystemkTextureSize)
            {
                return result;
            }

            unchecked
            {
                GroundPollution groundPollution = pollutionMap[cell.x + GroundPollutionSystemkTextureSize * cell.y];
                GroundPollution groundPollution2 = cell.x < GroundPollutionSystemkTextureSize - 1
                    ? pollutionMap[cell.x + 1 + GroundPollutionSystemkTextureSize * cell.y]
                    : default;
                GroundPollution groundPollution3 = cell.y < GroundPollutionSystemkTextureSize - 1
                    ? pollutionMap[cell.x + GroundPollutionSystemkTextureSize * (cell.y + 1)]
                    : default;
                GroundPollution groundPollution4 =
                    cell.x < GroundPollutionSystemkTextureSize - 1 && cell.y < GroundPollutionSystemkTextureSize - 1
                        ? pollutionMap[cell.x + 1 + GroundPollutionSystemkTextureSize * (cell.y + 1)]
                        : default;
                result.m_Pollution = (short)math.round(math.lerp(
                    math.lerp(groundPollution.m_Pollution, groundPollution2.m_Pollution, cellCoords.x - cell.x),
                    math.lerp(groundPollution3.m_Pollution, groundPollution4.m_Pollution, cellCoords.x - cell.x),
                    cellCoords.y - cell.y));
                return result;
            }
        }

        #endregion

        #region GroundWaterSystem

        public static float3 GroundWaterSystemGetCellCenter(int index) =>
            GetCellCenter(index, GroundWaterSystemkTextureSize);

        public static GroundWater GroundWaterSystemGetGroundWater(float3 position,
            NativeArray<GroundWater> groundWaterMap)
        {
            float2 @float = GetCellCoords(position, kMapSize, GroundWaterSystemkTextureSize) - new float2(0.5f, 0.5f);
            int2 cell = new int2((int)math.floor(@float.x), (int)math.floor(@float.y));
            unchecked
            {
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
                result.m_Amount = (short)math.round(GroundWaterSystemBilinear(groundWater.m_Amount,
                    groundWater2.m_Amount,
                    groundWater3.m_Amount, groundWater4.m_Amount, sx, sy));
                result.m_Polluted = (short)math.round(GroundWaterSystemBilinear(groundWater.m_Polluted,
                    groundWater2.m_Polluted, groundWater3.m_Polluted, groundWater4.m_Polluted, sx, sy));
                result.m_Max = (short)math.round(GroundWaterSystemBilinear(groundWater.m_Max, groundWater2.m_Max,
                    groundWater3.m_Max, groundWater4.m_Max, sx, sy));
                return result;
            }
        }

        private static GroundWater GroundWaterSystemGetGroundWater(NativeArray<GroundWater> groundWaterMap, int2 cell)
        {
            if (!GroundWaterSystemIsValidCell(cell))
            {
                return default(GroundWater);
            }

            return groundWaterMap[cell.x + GroundWaterSystemkTextureSize * cell.y];
        }

        private static void GroundWaterSystemSetGroundWater(NativeArray<GroundWater> groundWaterMap, int2 cell,
            GroundWater gw)
        {
            if (GroundWaterSystemIsValidCell(cell))
            {
                groundWaterMap[cell.x + GroundWaterSystemkTextureSize * cell.y] = gw;
            }
        }

        public static bool GroundWaterSystemIsValidCell(int2 cell)
        {
            if (cell.x >= 0 && cell.y >= 0 && cell.x < GroundWaterSystemkTextureSize)
            {
                return cell.y < GroundWaterSystemkTextureSize;
            }

            return false;
        }

        public static bool GroundWaterSystemTryGetCell(float3 position, out int2 cell)
        {
            cell = GetCell(position, kMapSize, GroundWaterSystemkTextureSize);
            return GroundWaterSystemIsValidCell(cell);
        }

        private static float GroundWaterSystemBilinear(short v00, short v10, short v01, short v11, float sx, float sy)
        {
            return math.lerp(math.lerp(v00, v10, sx), math.lerp(v01, v11, sx), sy);
        }

        public static void GroundWaterSystemConsumeGroundWater(float3 position, NativeArray<GroundWater> groundWaterMap,
            int amount)
        {
            Assert.IsTrue(amount >= 0);
            float2 @float = GetCellCoords(position, kMapSize, GroundWaterSystemkTextureSize) - new float2(0.5f, 0.5f);
            int2 cell = new int2((int)math.floor(@float.x), (int)math.floor(@float.y));
            float totalAvailable;
            float totalConsumed;
            unchecked
            {
                int2 cell2 = new int2(cell.x + 1, cell.y);
                int2 cell3 = new int2(cell.x, cell.y + 1);
                int2 cell4 = new int2(cell.x + 1, cell.y + 1);
                GroundWater gw2 = GroundWaterSystemGetGroundWater(groundWaterMap, cell);
                GroundWater gw3 = GroundWaterSystemGetGroundWater(groundWaterMap, cell2);
                GroundWater gw4 = GroundWaterSystemGetGroundWater(groundWaterMap, cell3);
                GroundWater gw5 = GroundWaterSystemGetGroundWater(groundWaterMap, cell4);
                float sx = @float.x - cell.x;
                float sy = @float.y - cell.y;
                float num = math.ceil(GroundWaterSystemBilinear(gw2.m_Amount, 0, 0, 0, sx, sy));
                float num2 = math.ceil(GroundWaterSystemBilinear(0, gw3.m_Amount, 0, 0, sx, sy));
                float num3 = math.ceil(GroundWaterSystemBilinear(0, 0, gw4.m_Amount, 0, sx, sy));
                float num4 = math.ceil(GroundWaterSystemBilinear(0, 0, 0, gw5.m_Amount, sx, sy));
                totalAvailable = num + num2 + num3 + num4;
                totalConsumed = math.min(amount, totalAvailable);
                if (totalAvailable < amount)
                {
                    Debug.LogWarning(
                        $"Trying to consume more groundwater than available! amount: {amount}, available: {totalAvailable}");
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
            }

            void ConsumeFraction(ref GroundWater gw, float cellAvailable)
            {
                if (!(totalAvailable < 0.5f))
                {
                    float num5 = cellAvailable / totalAvailable;
                    totalAvailable -= cellAvailable;
                    float num6 = math.max(y: math.max(0f, totalConsumed - totalAvailable),
                        x: math.round(num5 * totalConsumed));
                    Assert.IsTrue(num6 <= (float)gw.m_Amount);
                    gw.Consume((int)num6);
                    totalConsumed -= num6;
                }
            }
        }

        #endregion

        #region LandValueSystem

        public static int LandValueSystemGetCellIndex(float3 pos)
        {
            unchecked
            {
                int num = kMapSize / LandValueSystemkTextureSize;
                return Mathf.FloorToInt((kMapSize / 2 + pos.x) / num) +
                       Mathf.FloorToInt((kMapSize / 2 + pos.z) / num) * LandValueSystemkTextureSize;
            }
        }

        #endregion

        #region NoisePollutionSystem

        public static NoisePollution NoisePollutionSystemGetPollution(float3 position,
            NativeArray<NoisePollution> pollutionMap)
        {
            NoisePollution result = default;
            float num = kMapSize / (float)NoisePollutionSystemkTextureSize;
            int2 cell = GetCell(position - new float3(num / 2f, 0f, num / 2f), kMapSize,
                NoisePollutionSystemkTextureSize);
            float2 @float = GetCellCoords(position, kMapSize, NoisePollutionSystemkTextureSize) -
                            new float2(0.5f, 0.5f);
            unchecked
            {
                cell = math.clamp(cell, 0, NoisePollutionSystemkTextureSize - 2);
                short pollution = pollutionMap[cell.x + NoisePollutionSystemkTextureSize * cell.y].m_Pollution;
                short pollution2 = pollutionMap[cell.x + 1 + NoisePollutionSystemkTextureSize * cell.y].m_Pollution;
                short pollution3 = pollutionMap[cell.x + NoisePollutionSystemkTextureSize * (cell.y + 1)].m_Pollution;
                short pollution4 = pollutionMap[cell.x + 1 + NoisePollutionSystemkTextureSize * (cell.y + 1)]
                    .m_Pollution;
                result.m_Pollution = (short)math.round(math.lerp(math.lerp(pollution, pollution2, @float.x - cell.x),
                    math.lerp(pollution3, pollution4, @float.x - cell.x), @float.y - cell.y));
                return result;
            }
        }

        #endregion

        #region SoilWaterSystem

        public static float3 SoilWaterSystemGetCellCenter(int index)
        {
            return GetCellCenter(index, SoilWaterSystemkTextureSize);
        }

        #endregion

        #region TerrainAttractivenessSystem

        public static float TerrainAttractivenessSystemEvaluateAttractiveness(float3 position,
            CellMapData<TerrainAttractiveness> data, TerrainHeightData heightData,
            AttractivenessParameterData parameters, NativeArray<int> factors)
        {
            float num = TerrainUtils.SampleHeight(ref heightData, position);
            TerrainAttractiveness attractiveness =
                TerrainAttractivenessSystemGetAttractiveness(position, data.m_Buffer);
            float num2 = parameters.m_ForestEffect * attractiveness.m_ForestBonus;
            AttractionSystem.SetFactor(factors, AttractionSystem.AttractivenessFactor.Forest, num2);
            float num3 = parameters.m_ShoreEffect * attractiveness.m_ShoreBonus;
            AttractionSystem.SetFactor(factors, AttractionSystem.AttractivenessFactor.Beach, num3);
            float num4 = math.min(parameters.m_HeightBonus.z,
                math.max(0f, num - parameters.m_HeightBonus.x) * parameters.m_HeightBonus.y);
            AttractionSystem.SetFactor(factors, AttractionSystem.AttractivenessFactor.Height, num4);
            return num2 + num3 + num4;
        }

        public static TerrainAttractiveness TerrainAttractivenessSystemGetAttractiveness(float3 position,
            NativeArray<TerrainAttractiveness> attractivenessMap)
        {
            TerrainAttractiveness result = default;
            int2 cell = GetCell(position, kMapSize, TerrainAttractivenessSystemkTextureSize);
            float2 cellCoords = GetCellCoords(position, kMapSize, TerrainAttractivenessSystemkTextureSize);
            if (cell.x < 0 || cell.x >= TerrainAttractivenessSystemkTextureSize || cell.y < 0 ||
                cell.y >= TerrainAttractivenessSystemkTextureSize)
            {
                return result;
            }

            unchecked
            {
                TerrainAttractiveness terrainAttractiveness =
                    attractivenessMap[cell.x + TerrainAttractivenessSystemkTextureSize * cell.y];
                TerrainAttractiveness terrainAttractiveness2 = cell.x < TerrainAttractivenessSystemkTextureSize - 1
                    ? attractivenessMap[cell.x + 1 + TerrainAttractivenessSystemkTextureSize * cell.y]
                    : default;
                TerrainAttractiveness terrainAttractiveness3 = cell.y < TerrainAttractivenessSystemkTextureSize - 1
                    ? attractivenessMap[cell.x + TerrainAttractivenessSystemkTextureSize * (cell.y + 1)]
                    : default;
                TerrainAttractiveness terrainAttractiveness4 =
                    cell.x < TerrainAttractivenessSystemkTextureSize - 1 &&
                    cell.y < TerrainAttractivenessSystemkTextureSize - 1
                        ? attractivenessMap[cell.x + 1 + TerrainAttractivenessSystemkTextureSize * (cell.y + 1)]
                        : default;
                result.m_ForestBonus = (short)math.round(math.lerp(
                    math.lerp(terrainAttractiveness.m_ForestBonus, terrainAttractiveness2.m_ForestBonus,
                        cellCoords.x - cell.x),
                    math.lerp(terrainAttractiveness3.m_ForestBonus, terrainAttractiveness4.m_ForestBonus,
                        cellCoords.x - cell.x), cellCoords.y - cell.y));
                result.m_ShoreBonus = (short)math.round(math.lerp(
                    math.lerp(terrainAttractiveness.m_ShoreBonus, terrainAttractiveness2.m_ShoreBonus,
                        cellCoords.x - cell.x),
                    math.lerp(terrainAttractiveness3.m_ShoreBonus, terrainAttractiveness4.m_ShoreBonus,
                        cellCoords.x - cell.x), cellCoords.y - cell.y));
                return result;
            }
        }

        #endregion

        #region TrafficAmbienceSystem

        public static TrafficAmbienceCell TrafficAmbienceSystemGetTrafficAmbience2(float3 position,
            NativeArray<TrafficAmbienceCell> trafficAmbienceMap, float maxPerCell)
        {
            TrafficAmbienceCell result = default;
            int2 cell = GetCell(position, kMapSize, TrafficAmbienceSystemkTextureSize);
            float num = 0f;
            float num2 = 0f;
            unchecked
            {
                for (int i = cell.x - 2; i <= cell.x + 2; i++)
                {
                    for (int j = cell.y - 2; j <= cell.y + 2; j++)
                    {
                        if (i >= 0 && i < TrafficAmbienceSystemkTextureSize && j >= 0 &&
                            j < TrafficAmbienceSystemkTextureSize)
                        {
                            int index = i + TrafficAmbienceSystemkTextureSize * j;
                            float num3 = math.max(1f,
                                math.distancesq(GetCellCenter(index, TrafficAmbienceSystemkTextureSize), position));
                            num += math.min(maxPerCell, trafficAmbienceMap[index].m_Traffic) / num3;
                            num2 += 1f / num3;
                        }
                    }
                }

                result.m_Traffic = num / num2;
                return result;
            }
        }

        #endregion

        #region WindSystem,WindSimulationSystem

        public static Wind WindSystemGetWind(float3 position, NativeArray<Wind> windMap)
        {
            int2 cell = GetCell(position, kMapSize, WindSystemkTextureSize);
            cell = math.clamp(cell, 0, WindSystemkTextureSize - 1);
            float2 cellCoords = GetCellCoords(position, kMapSize, WindSystemkTextureSize);
            int num = math.min(WindSystemkTextureSize - 1, cell.x + 1);
            int num2 = math.min(WindSystemkTextureSize - 1, cell.y + 1);
            Wind result = default;
            result.m_Wind = math.lerp(
                math.lerp(windMap[cell.x + WindSystemkTextureSize * cell.y].m_Wind,
                    windMap[num + WindSystemkTextureSize * cell.y].m_Wind, cellCoords.x - cell.x),
                math.lerp(windMap[cell.x + WindSystemkTextureSize * num2].m_Wind,
                    windMap[num + WindSystemkTextureSize * num2].m_Wind, cellCoords.x - cell.x), cellCoords.y - cell.y);
            return result;
        }

        public static WindSimulationSystem.WindCell WindSimulationSystemGetCell(int3 position,
            NativeArray<WindSimulationSystem.WindCell> cells)
        {
            int3 WindSimulationSystemkResolution = new(WindSystemkTextureSize, WindSystemkTextureSize, 16);
            int num = unchecked(position.x + position.y * WindSimulationSystemkResolution.x +
                                position.z * WindSimulationSystemkResolution.x * WindSimulationSystemkResolution.y);
            if (num < 0 || num >= cells.Length)
            {
                return default;
            }

            return cells[num];
        }

        public static float3 WindSimulationSystemGetCenterVelocity(int3 cell,
            NativeArray<WindSimulationSystem.WindCell> cells)
        {
            float3 velocities = WindSimulationSystemGetCell(cell, cells).m_Velocities;
            float3 @float = ((cell.x > 0)
                ? WindSimulationSystemGetCell(cell + new int3(-1, 0, 0), cells).m_Velocities
                : velocities);
            float3 float2 = ((cell.y > 0)
                ? WindSimulationSystemGetCell(cell + new int3(0, -1, 0), cells).m_Velocities
                : velocities);
            float3 float3 = ((cell.z > 0)
                ? WindSimulationSystemGetCell(cell + new int3(0, 0, -1), cells).m_Velocities
                : velocities);
            return 0.5f * new float3(velocities.x + @float.x, velocities.y + float2.y, velocities.z + float3.z);
        }

        public static float3 WindSimulationSystemGetCellCenter(int index)
        {
            unchecked
            {
                int3 WindSimulationSystemkResolution = new(WindSystemkTextureSize, WindSystemkTextureSize, 16);
                int3 @int = new(index % WindSimulationSystemkResolution.x,
                    index / WindSimulationSystemkResolution.x % WindSimulationSystemkResolution.y,
                    index / (WindSimulationSystemkResolution.x * WindSimulationSystemkResolution.y));
                float3 result = kMapSize * new float3((@int.x + 0.5f) / WindSimulationSystemkResolution.x, 0f,
                    (@int.y + 0.5f) / WindSimulationSystemkResolution.y) - kMapSize / 2;
                result.y = 100f + 1024f * (@int.z + 0.5f) / WindSimulationSystemkResolution.z;
                return result;
            }
        }

        #endregion

        #region ZoneAmbienceSystem

        public static float ZoneAmbienceSystemGetZoneAmbience(GroupAmbienceType type, float3 position,
            NativeArray<ZoneAmbienceCell> zoneAmbienceMap, float maxPerCell)
        {
            int2 cell = GetCell(position, kMapSize, ZoneAmbienceSystemkTextureSize);
            float num = 0f;
            float num2 = 0f;
            unchecked
            {
                for (int i = cell.x - 2; i <= cell.x + 2; i++)
                {
                    for (int j = cell.y - 2; j <= cell.y + 2; j++)
                    {
                        if (i >= 0 && i < ZoneAmbienceSystemkTextureSize && j >= 0 &&
                            j < ZoneAmbienceSystemkTextureSize)
                        {
                            int index = i + ZoneAmbienceSystemkTextureSize * j;
                            float num3 = math.max(1f,
                                math.distancesq(GetCellCenter(index, ZoneAmbienceSystemkTextureSize), position) / 10f);
                            num += math.min(maxPerCell, zoneAmbienceMap[index].m_Value.GetAmbience(type)) / num3;
                            num2 += 1f / num3;
                        }
                    }
                }

                return num / num2;
            }
        }

        public static float ZoneAmbienceSystemGetZoneAmbienceNear(GroupAmbienceType type, float3 position,
            NativeArray<ZoneAmbienceCell> zoneAmbienceMap, float nearWeight, float maxPerCell)
        {
            int2 cell = GetCell(position, kMapSize, ZoneAmbienceSystemkTextureSize);
            float num = 0f;
            float num2 = 0f;
            unchecked
            {
                for (int i = cell.x - 2; i <= cell.x + 2; i++)
                {
                    for (int j = cell.y - 2; j <= cell.y + 2; j++)
                    {
                        if (i >= 0 && i < ZoneAmbienceSystemkTextureSize && j >= 0 &&
                            j < ZoneAmbienceSystemkTextureSize)
                        {
                            int index = i + ZoneAmbienceSystemkTextureSize * j;
                            float num3 = math.max(1f,
                                math.pow(
                                    math.distance(GetCellCenter(index, ZoneAmbienceSystemkTextureSize), position) / 10f,
                                    1f + nearWeight));
                            num += math.min(maxPerCell, zoneAmbienceMap[index].m_Value.GetAmbience(type)) / num3;
                            num2 += 1f / num3;
                        }
                    }
                }

                return num / num2;
            }
        }

        #endregion

        #endregion

        // ======================================
        // 其他外部系统调用上述方法字段的静态方法重定向
        // ======================================

        #region CitizenHappinessSystem 静态方法重定向

        // 重写3个静态方法
        public static int2 GetGroundPollutionBonuses(Entity building,
            ref ComponentLookup<Game.Objects.Transform> transforms, NativeArray<GroundPollution> pollutionMap,
            DynamicBuffer<CityModifier> cityModifiers, in CitizenHappinessParameterData data)
        {
            int2 result = default;
            unchecked
            {
                if (transforms.HasComponent(building))
                {
                    short y = (short)(GroundPollutionSystemGetPollution(transforms[building].m_Position, pollutionMap)
                        .m_Pollution / data.m_PollutionBonusDivisor);
                    float value = 1f;
                    CityUtils.ApplyModifier(ref value, cityModifiers, CityModifierType.PollutionHealthAffect);
                    result.x = (int)((float)(-math.min(data.m_MaxAirAndGroundPollutionBonus, y)) * value);
                }

                return result;
            }
        }

        public static int2 GetAirPollutionBonuses(Entity building,
            ref ComponentLookup<Game.Objects.Transform> transforms, NativeArray<AirPollution> airPollutionMap,
            DynamicBuffer<CityModifier> cityModifiers, in CitizenHappinessParameterData data)
        {
            int2 result = default;
            unchecked
            {
                if (transforms.HasComponent(building))
                {
                    short y = (short)(AirPollutionSystemGetPollution(transforms[building].m_Position, airPollutionMap)
                        .m_Pollution / data.m_PollutionBonusDivisor);
                    float value = 1f;
                    CityUtils.ApplyModifier(ref value, cityModifiers, CityModifierType.PollutionHealthAffect);
                    result.x = (int)((float)(-math.min(data.m_MaxAirAndGroundPollutionBonus, y)) * value);
                }

                return result;
            }
        }

        public static int2 GetNoiseBonuses(Entity building, ref ComponentLookup<Game.Objects.Transform> transforms,
            NativeArray<NoisePollution> noiseMap, in CitizenHappinessParameterData data)
        {
            int2 result = default;
            unchecked
            {
                if (transforms.HasComponent(building))
                {
                    short y = (short)(NoisePollutionSystemGetPollution(transforms[building].m_Position, noiseMap)
                        .m_Pollution / data.m_PollutionBonusDivisor);
                    result.y = -math.min(data.m_MaxNoisePollutionBonus, y);
                }

                return result;
            }
        }

        #endregion

        #region Game.UI.InGame.BuildingHappiness 重定向静态方法

        public static void GetResidentialBuildingHappinessFactors(Entity city, NativeArray<int> taxRates,
            Entity property, NativeArray<int2> factors, ref ComponentLookup<PrefabRef> prefabs,
            ref ComponentLookup<SpawnableBuildingData> spawnableBuildings,
            ref ComponentLookup<BuildingPropertyData> buildingPropertyDatas,
            ref BufferLookup<CityModifier> cityModifiers, ref ComponentLookup<Building> buildings,
            ref ComponentLookup<ElectricityConsumer> electricityConsumers,
            ref ComponentLookup<WaterConsumer> waterConsumers,
            ref BufferLookup<Game.Net.ServiceCoverage> serviceCoverages, ref ComponentLookup<Locked> locked,
            ref ComponentLookup<Game.Objects.Transform> transforms,
            ref ComponentLookup<GarbageProducer> garbageProducers, ref ComponentLookup<CrimeProducer> crimeProducers,
            ref ComponentLookup<MailProducer> mailProducers, ref BufferLookup<Renter> renters,
            ref ComponentLookup<Citizen> citizenDatas, ref BufferLookup<HouseholdCitizen> householdCitizens,
            ref ComponentLookup<BuildingData> buildingDatas, ref LocalEffectSystem.ReadData localEffectData,
            CitizenHappinessParameterData citizenHappinessParameters, GarbageParameterData garbageParameters,
            HealthcareParameterData healthcareParameters, ParkParameterData parkParameters,
            EducationParameterData educationParameters, TelecomParameterData telecomParameters,
            DynamicBuffer<HappinessFactorParameterData> happinessFactorParameters,
            NativeArray<GroundPollution> pollutionMap, NativeArray<NoisePollution> noisePollutionMap,
            NativeArray<AirPollution> airPollutionMap, CellMapData<TelecomCoverage> telecomCoverage,
            float relativeElectricityFee, float relativeWaterFee)
        {
            if (!prefabs.HasComponent(property))
            {
                return;
            }

            Entity prefab = prefabs[property].m_Prefab;
            if (!spawnableBuildings.HasComponent(prefab) || !buildingDatas.HasComponent(prefab))
            {
                return;
            }

            BuildingPropertyData buildingPropertyData = buildingPropertyDatas[prefab];
            DynamicBuffer<CityModifier> cityModifiers2 = cityModifiers[city];
            BuildingData buildingData = buildingDatas[prefab];
            unchecked
            {
                float num = buildingData.m_LotSize.x * buildingData.m_LotSize.y;
                Entity entity = Entity.Null;
                float curvePosition = 0f;
                int level = spawnableBuildings[prefab].m_Level;
                if (buildings.HasComponent(property))
                {
                    Building building = buildings[property];
                    entity = building.m_RoadEdge;
                    curvePosition = building.m_CurvePosition;
                }

                if (buildingPropertyData.m_ResidentialProperties <= 0)
                {
                    return;
                }

                num /= (float)buildingPropertyData.m_ResidentialProperties;
                float num2 = 1f;
                int currentHappiness = 50;
                int leisureCounter = 128;
                float num3 = 0.3f;
                float num4 = 0.25f;
                float num5 = 0.25f;
                float num6 = 0.15f;
                float num7 = 0.05f;
                float num8 = 2f;
                if (renters.HasBuffer(property))
                {
                    num3 = 0f;
                    num4 = 0f;
                    num5 = 0f;
                    num6 = 0f;
                    num7 = 0f;
                    int2 @int = default(int2);
                    int2 int2 = default(int2);
                    int num9 = 0;
                    int num10 = 0;
                    DynamicBuffer<Renter> dynamicBuffer = renters[property];
                    for (int i = 0; i < dynamicBuffer.Length; i++)
                    {
                        Entity renter = dynamicBuffer[i].m_Renter;
                        if (!householdCitizens.HasBuffer(renter))
                        {
                            continue;
                        }

                        num10++;
                        DynamicBuffer<HouseholdCitizen> dynamicBuffer2 = householdCitizens[renter];
                        for (int j = 0; j < dynamicBuffer2.Length; j++)
                        {
                            Entity citizen = dynamicBuffer2[j].m_Citizen;
                            if (citizenDatas.HasComponent(citizen))
                            {
                                Citizen citizen2 = citizenDatas[citizen];
                                int2.x += citizen2.Happiness;
                                int2.y++;
                                num9 += citizen2.m_LeisureCounter;
                                switch (citizen2.GetEducationLevel())
                                {
                                    case 0:
                                        num3 += 1f;
                                        break;
                                    case 1:
                                        num4 += 1f;
                                        break;
                                    case 2:
                                        num5 += 1f;
                                        break;
                                    case 3:
                                        num6 += 1f;
                                        break;
                                    case 4:
                                        num7 += 1f;
                                        break;
                                }

                                if (citizen2.GetAge() == CitizenAge.Child)
                                {
                                    @int.x++;
                                }
                            }
                        }

                        @int.y++;
                    }

                    if (@int.y > 0)
                    {
                        num2 = (float)@int.x / (float)@int.y;
                    }

                    if (int2.y > 0)
                    {
                        currentHappiness = Mathf.RoundToInt((float)int2.x / (float)int2.y);
                        leisureCounter = Mathf.RoundToInt((float)num9 / (float)int2.y);
                        num3 /= (float)int2.y;
                        num4 /= (float)int2.y;
                        num5 /= (float)int2.y;
                        num6 /= (float)int2.y;
                        num7 /= (float)int2.y;
                        num8 = (float)int2.y / (float)num10;
                    }
                }

                Entity healthcareServicePrefab = healthcareParameters.m_HealthcareServicePrefab;
                Entity parkServicePrefab = parkParameters.m_ParkServicePrefab;
                Entity educationServicePrefab = educationParameters.m_EducationServicePrefab;
                Entity telecomServicePrefab = telecomParameters.m_TelecomServicePrefab;
                if (!locked.HasEnabledComponent(happinessFactorParameters[4].m_LockedEntity))
                {
                    int2 electricitySupplyBonuses = CitizenHappinessSystem.GetElectricitySupplyBonuses(property,
                        ref electricityConsumers, in citizenHappinessParameters);
                    int2 value = factors[3];
                    value.x++;
                    value.y += (electricitySupplyBonuses.x + electricitySupplyBonuses.y) / 2 -
                               happinessFactorParameters[4].m_BaseLevel;
                    factors[3] = value;
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[23].m_LockedEntity))
                {
                    int2 electricityFeeBonuses = CitizenHappinessSystem.GetElectricityFeeBonuses(property,
                        ref electricityConsumers, relativeElectricityFee, in citizenHappinessParameters);
                    int2 value2 = factors[26];
                    value2.x++;
                    value2.y += (electricityFeeBonuses.x + electricityFeeBonuses.y) / 2 -
                                happinessFactorParameters[23].m_BaseLevel;
                    factors[26] = value2;
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[8].m_LockedEntity))
                {
                    int2 waterSupplyBonuses =
                        CitizenHappinessSystem.GetWaterSupplyBonuses(property, ref waterConsumers,
                            in citizenHappinessParameters);
                    int2 value3 = factors[7];
                    value3.x++;
                    value3.y += (waterSupplyBonuses.x + waterSupplyBonuses.y) / 2 -
                                happinessFactorParameters[8].m_BaseLevel;
                    factors[7] = value3;
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[24].m_LockedEntity))
                {
                    int2 waterFeeBonuses = CitizenHappinessSystem.GetWaterFeeBonuses(property, ref waterConsumers,
                        relativeWaterFee, in citizenHappinessParameters);
                    int2 value4 = factors[27];
                    value4.x++;
                    value4.y += (waterFeeBonuses.x + waterFeeBonuses.y) / 2 - happinessFactorParameters[24].m_BaseLevel;
                    factors[27] = value4;
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[9].m_LockedEntity))
                {
                    int2 waterPollutionBonuses = CitizenHappinessSystem.GetWaterPollutionBonuses(property,
                        ref waterConsumers, cityModifiers2, in citizenHappinessParameters);
                    int2 value5 = factors[8];
                    value5.x++;
                    value5.y += (waterPollutionBonuses.x + waterPollutionBonuses.y) / 2 -
                                happinessFactorParameters[9].m_BaseLevel;
                    factors[8] = value5;
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[10].m_LockedEntity))
                {
                    int2 sewageBonuses =
                        CitizenHappinessSystem.GetSewageBonuses(property, ref waterConsumers,
                            in citizenHappinessParameters);
                    int2 value6 = factors[9];
                    value6.x++;
                    value6.y += (sewageBonuses.x + sewageBonuses.y) / 2 - happinessFactorParameters[10].m_BaseLevel;
                    factors[9] = value6;
                }

                if (serviceCoverages.HasBuffer(entity))
                {
                    DynamicBuffer<Game.Net.ServiceCoverage> serviceCoverage = serviceCoverages[entity];
                    if (!locked.HasEnabledComponent(happinessFactorParameters[5].m_LockedEntity))
                    {
                        int2 healthcareBonuses = CitizenHappinessSystem.GetHealthcareBonuses(curvePosition,
                            serviceCoverage,
                            ref locked, healthcareServicePrefab, in citizenHappinessParameters);
                        int2 value7 = factors[4];
                        value7.x++;
                        value7.y += (healthcareBonuses.x + healthcareBonuses.y) / 2 -
                                    happinessFactorParameters[5].m_BaseLevel;
                        factors[4] = value7;
                    }

                    if (!locked.HasEnabledComponent(happinessFactorParameters[12].m_LockedEntity))
                    {
                        int2 entertainmentBonuses = CitizenHappinessSystem.GetEntertainmentBonuses(curvePosition,
                            serviceCoverage, cityModifiers2, ref locked, parkServicePrefab,
                            in citizenHappinessParameters);
                        int2 value8 = factors[11];
                        value8.x++;
                        value8.y += (entertainmentBonuses.x + entertainmentBonuses.y) / 2 -
                                    happinessFactorParameters[12].m_BaseLevel;
                        factors[11] = value8;
                    }

                    if (!locked.HasEnabledComponent(happinessFactorParameters[13].m_LockedEntity))
                    {
                        int2 educationBonuses = CitizenHappinessSystem.GetEducationBonuses(curvePosition,
                            serviceCoverage,
                            ref locked, educationServicePrefab, in citizenHappinessParameters, 1);
                        int2 value9 = factors[12];
                        value9.x++;
                        value9.y += Mathf.RoundToInt(num2 * (float)(educationBonuses.x + educationBonuses.y) / 2f) -
                                    happinessFactorParameters[13].m_BaseLevel;
                        factors[12] = value9;
                    }

                    if (!locked.HasEnabledComponent(happinessFactorParameters[15].m_LockedEntity))
                    {
                        int2 wellfareBonuses = CitizenHappinessSystem.GetWellfareBonuses(curvePosition, serviceCoverage,
                            in citizenHappinessParameters, currentHappiness);
                        int2 value10 = factors[14];
                        value10.x++;
                        value10.y += (wellfareBonuses.x + wellfareBonuses.y) / 2 -
                                     happinessFactorParameters[15].m_BaseLevel;
                        factors[14] = value10;
                    }
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[6].m_LockedEntity))
                {
                    int2 groundPollutionBonuses = GetGroundPollutionBonuses(property, ref transforms, pollutionMap,
                        cityModifiers2, in citizenHappinessParameters);
                    int2 value11 = factors[5];
                    value11.x++;
                    value11.y += (groundPollutionBonuses.x + groundPollutionBonuses.y) / 2 -
                                 happinessFactorParameters[6].m_BaseLevel;
                    factors[5] = value11;
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[2].m_LockedEntity))
                {
                    int2 airPollutionBonuses = GetAirPollutionBonuses(property, ref transforms, airPollutionMap,
                        cityModifiers2, in citizenHappinessParameters);
                    int2 value12 = factors[2];
                    value12.x++;
                    value12.y += (airPollutionBonuses.x + airPollutionBonuses.y) / 2 -
                                 happinessFactorParameters[2].m_BaseLevel;
                    factors[2] = value12;
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[7].m_LockedEntity))
                {
                    int2 noiseBonuses = GetNoiseBonuses(property, ref transforms, noisePollutionMap,
                        in citizenHappinessParameters);
                    int2 value13 = factors[6];
                    value13.x++;
                    value13.y += (noiseBonuses.x + noiseBonuses.y) / 2 - happinessFactorParameters[7].m_BaseLevel;
                    factors[6] = value13;
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[11].m_LockedEntity))
                {
                    int2 garbageBonuses = CitizenHappinessSystem.GetGarbageBonuses(property, ref garbageProducers,
                        ref locked, happinessFactorParameters[11].m_LockedEntity, in garbageParameters);
                    int2 value14 = factors[10];
                    value14.x++;
                    value14.y += (garbageBonuses.x + garbageBonuses.y) / 2 - happinessFactorParameters[11].m_BaseLevel;
                    factors[10] = value14;
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[1].m_LockedEntity))
                {
                    int2 crimeBonuses = CitizenHappinessSystem.GetCrimeBonuses(default(CrimeVictim), property,
                        ref crimeProducers, ref locked, happinessFactorParameters[1].m_LockedEntity,
                        in citizenHappinessParameters);
                    int2 value15 = factors[1];
                    value15.x++;
                    value15.y += (crimeBonuses.x + crimeBonuses.y) / 2 - happinessFactorParameters[1].m_BaseLevel;
                    factors[1] = value15;
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[14].m_LockedEntity))
                {
                    int2 mailBonuses = CitizenHappinessSystem.GetMailBonuses(property, ref mailProducers, ref locked,
                        telecomServicePrefab, in citizenHappinessParameters);
                    int2 value16 = factors[13];
                    value16.x++;
                    value16.y += (mailBonuses.x + mailBonuses.y) / 2 - happinessFactorParameters[14].m_BaseLevel;
                    factors[13] = value16;
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[0].m_LockedEntity))
                {
                    int2 telecomBonuses = CitizenHappinessSystem.GetTelecomBonuses(property, ref transforms,
                        telecomCoverage, ref locked, telecomServicePrefab, in citizenHappinessParameters);
                    int2 value17 = factors[0];
                    value17.x++;
                    value17.y += (telecomBonuses.x + telecomBonuses.y) / 2 - happinessFactorParameters[0].m_BaseLevel;
                    factors[0] = value17;
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[16].m_LockedEntity))
                {
                    int2 leisureBonuses = CitizenHappinessSystem.GetLeisureBonuses(leisureCounter, isTourist: false);
                    int2 value18 = factors[15];
                    value18.x++;
                    value18.y += (leisureBonuses.x + leisureBonuses.y) / 2 - happinessFactorParameters[16].m_BaseLevel;
                    factors[15] = value18;
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[17].m_LockedEntity))
                {
                    float2 @float =
                        new float2(num3, num3) *
                        CitizenHappinessSystem.GetTaxBonuses(0, taxRates, cityModifiers2,
                            in citizenHappinessParameters) +
                        new float2(num4, num4) *
                        CitizenHappinessSystem.GetTaxBonuses(1, taxRates, cityModifiers2,
                            in citizenHappinessParameters) +
                        new float2(num5, num5) *
                        CitizenHappinessSystem.GetTaxBonuses(2, taxRates, cityModifiers2,
                            in citizenHappinessParameters) +
                        new float2(num6, num6) *
                        CitizenHappinessSystem.GetTaxBonuses(3, taxRates, cityModifiers2,
                            in citizenHappinessParameters) +
                        new float2(num7, num7) *
                        CitizenHappinessSystem.GetTaxBonuses(4, taxRates, cityModifiers2,
                            in citizenHappinessParameters);
                    int2 value19 = factors[16];
                    value19.x++;
                    value19.y += Mathf.RoundToInt(@float.x + @float.y) / 2 - happinessFactorParameters[17].m_BaseLevel;
                    factors[16] = value19;
                }

                if (!locked.HasEnabledComponent(happinessFactorParameters[3].m_LockedEntity))
                {
                    float2 float2 =
                        CitizenHappinessSystem.GetApartmentWellbeing(
                            buildingPropertyData.m_SpaceMultiplier * num / num8,
                            level);
                    int2 value20 = factors[21];
                    value20.x++;
                    value20.y += Mathf.RoundToInt(float2.x + float2.y) / 2 - happinessFactorParameters[3].m_BaseLevel;
                    factors[21] = value20;
                }

                float wellbeing = 50f;
                float health = 50f;
                float2 float3 = CitizenHappinessSystem.GetLocalEffectBonuses(ref wellbeing, ref health,
                    ref localEffectData,
                    ref transforms, property);
                int2 value21 = factors[28];
                value21.x++;
                value21.y += Mathf.RoundToInt(float3.x + float3.y) / 2;
                factors[28] = value21;
            }
        }

        #endregion

        // 僅用於HouseholdFindPopertySystemMod
        //#region Game.Buildings.PropertyUtils 重定向静态方法

        //public struct GenericApartmentQuality
        //{
        //    public float apartmentSize;
        //    public float2 educationBonus;
        //    public float welfareBonus;
        //    public float score;
        //    public int level;
        //}

        //public static float GetPropertyScore

        //#endregion
    }
}


