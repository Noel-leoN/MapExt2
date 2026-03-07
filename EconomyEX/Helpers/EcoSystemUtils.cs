// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using Unity.Mathematics;
using Unity.Collections;
using Game.Simulation;
using Game.Net;
using Game.Areas;

namespace EconomyEX.Helpers
{
    public static class EcoSystemUtils
    {
        // Standard Vanilla Map Size
        public const int kMapSize = 14336;

        public static GroundPollution GetPollution(float3 position, NativeArray<GroundPollution> map, int mapSize = kMapSize)
        {
            int2 cell = GetCell(position, mapSize, CellMapSystem<GroundPollution>.kTextureSize);
            int index = cell.y * CellMapSystem<GroundPollution>.kTextureSize + cell.x;
            if (index >= 0 && index < map.Length)
            {
                return map[index];
            }
            return default;
        }

        public static AirPollution GetPollution(float3 position, NativeArray<AirPollution> map, int mapSize = kMapSize)
        {
            int2 cell = GetCell(position, mapSize, CellMapSystem<AirPollution>.kTextureSize);
            int index = cell.y * CellMapSystem<AirPollution>.kTextureSize + cell.x;
            if (index >= 0 && index < map.Length)
            {
                return map[index];
            }
            return default;
        }

        public static NoisePollution GetPollution(float3 position, NativeArray<NoisePollution> map, int mapSize = kMapSize)
        {
            int2 cell = GetCell(position, mapSize, CellMapSystem<NoisePollution>.kTextureSize);
            int index = cell.y * CellMapSystem<NoisePollution>.kTextureSize + cell.x;
            if (index >= 0 && index < map.Length)
            {
                return map[index];
            }
            return default;
        }

        public static AvailabilityInfoCell GetAvailability(float3 position, NativeArray<AvailabilityInfoCell> map, int mapSize = kMapSize)
        {
            int2 cell = GetCell(position, mapSize, AvailabilityInfoToGridSystem.kTextureSize);
            int index = cell.y * AvailabilityInfoToGridSystem.kTextureSize + cell.x;
            if (index >= 0 && index < map.Length)
            {
                return map[index];
            }
            return default;
        }

        public static TerrainAttractiveness GetAttractiveness(float3 position, NativeArray<TerrainAttractiveness> map, int mapSize = kMapSize)
        {
             int2 cell = GetCell(position, mapSize, TerrainAttractivenessSystem.kTextureSize);
            int index = cell.y * TerrainAttractivenessSystem.kTextureSize + cell.x;
            if (index >= 0 && index < map.Length)
            {
                return map[index];
            }
            return default;
        }

        public static int2 GetCell(float3 position, int mapSize, int textureSize)
        {
            float cellSize = (float)mapSize / textureSize;
            int x = (int)((position.x + mapSize / 2f) / cellSize);
            int y = (int)((position.z + mapSize / 2f) / cellSize);
            return new int2(math.clamp(x, 0, textureSize - 1), math.clamp(y, 0, textureSize - 1));
        }

        public static float3 GetCellCenter(int index, int textureSize, int mapSize = kMapSize)
        {
            int x = index % textureSize;
            int y = index / textureSize;
            float cellSize = (float)mapSize / textureSize;
            
            float posX = (x + 0.5f) * cellSize - mapSize / 2f;
            float posZ = (y + 0.5f) * cellSize - mapSize / 2f;
            
            return new float3(posX, 0, posZ);
        }
    }
}
