using Game.Common;
using Game.Events;
using Game.Prefabs;
using Game.Simulation;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static MapExtPDX.MapExt.ReBurstSystemModeA.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    [BurstCompile]
    public struct SoilWaterTickJob : IJob
    {
        public NativeArray<SoilWater> m_SoilWaterMap;

        [ReadOnly]
        public TerrainHeightData m_TerrainHeightData;

        [ReadOnly]
        public WaterSurfaceData m_WaterSurfaceData;

        public NativeArray<float> m_SoilWaterTextureData;

        public SoilWaterParameterData m_SoilWaterParameters;

        public ComponentLookup<WaterLevelChange> m_Changes;

        public ComponentLookup<FloodCounterData> m_FloodCounterDatas;

        [ReadOnly]
        public ComponentLookup<EventData> m_Events;

        [ReadOnly]
        public NativeList<Entity> m_FloodEntities;

        [ReadOnly]
        public NativeList<Entity> m_FloodPrefabEntities;

        public EntityCommandBuffer m_CommandBuffer;

        public Entity m_FloodCounterEntity;

        public float m_Weather;

        public int m_ShaderUpdatesPerSoilUpdate;

        public int m_LoadDistributionIndex;

        private void HandleInterface(int index, int otherIndex, NativeArray<int> tmp, ref SoilWaterParameterData soilWaterParameters)
        {
            SoilWater soilWater = m_SoilWaterMap[index];
            SoilWater soilWater2 = m_SoilWaterMap[otherIndex];
            int num = tmp[index];
            int num2 = tmp[otherIndex];
            float num3 = soilWater2.m_Surface - soilWater.m_Surface;
            float num4 = soilWater2.m_Amount / (float)soilWater2.m_Max - soilWater.m_Amount / (float)soilWater.m_Max;
            float num5 = soilWaterParameters.m_HeightEffect * num3 / (CellMapSystemRe.kMapSize / SoilWaterSystem.kTextureSize) + 0.25f * num4;
            num5 = !(num5 >= 0f) ? math.max(0f - soilWaterParameters.m_MaxDiffusion, num5) : math.min(soilWaterParameters.m_MaxDiffusion, num5);
            int num6 = Mathf.RoundToInt(num5 * (num5 > 0f ? soilWater2.m_Amount : soilWater.m_Amount));
            num += num6;
            num2 -= num6;
            tmp[index] = num;
            tmp[otherIndex] = num2;
        }

        private void StartFlood()
        {
            if (m_FloodPrefabEntities.Length > 0)
            {
                EntityArchetype archetype = m_Events[m_FloodPrefabEntities[0]].m_Archetype;
                Entity e = m_CommandBuffer.CreateEntity(archetype);
                m_CommandBuffer.SetComponent(e, new PrefabRef
                {
                    m_Prefab = m_FloodPrefabEntities[0]
                });
                m_CommandBuffer.SetComponent(e, new WaterLevelChange
                {
                    m_DangerHeight = 0f,
                    m_Direction = new float2(0f, 0f),
                    m_Intensity = 0f,
                    m_MaxIntensity = 0f
                });
            }
        }

        private void StopFlood()
        {
            m_CommandBuffer.AddComponent<Deleted>(m_FloodEntities[0]);
        }

        public void Execute()
        {
            NativeArray<int> tmp = new NativeArray<int>(m_SoilWaterMap.Length, Allocator.Temp);
            for (int i = 0; i < m_SoilWaterMap.Length; i++)
            {
                int num = i % SoilWaterSystem.kTextureSize;
                int num2 = i / SoilWaterSystem.kTextureSize;
                if (num < SoilWaterSystem.kTextureSize - 1)
                {
                    HandleInterface(i, i + 1, tmp, ref m_SoilWaterParameters);
                }
                if (num2 < SoilWaterSystem.kTextureSize - 1)
                {
                    HandleInterface(i, i + SoilWaterSystem.kTextureSize, tmp, ref m_SoilWaterParameters);
                }
            }
            float num3 = math.max(0f, math.pow(2f * math.max(0f, m_Weather - 0.5f), 2f));
            float num4 = 1f / (2f * m_SoilWaterParameters.m_MaximumWaterDepth);
            int2 @int = m_WaterSurfaceData.resolution.xz / SoilWaterSystem.kTextureSize;
            FloodCounterData value = m_FloodCounterDatas[m_FloodCounterEntity];
            value.m_FloodCounter = math.max(0f, 0.98f * value.m_FloodCounter + 2f * num3 - 0.1f);
            if (value.m_FloodCounter > 20f && m_FloodEntities.Length == 0)
            {
                StartFlood();
            }
            else if (m_FloodEntities.Length > 0)
            {
                if (value.m_FloodCounter == 0f)
                {
                    StopFlood();
                }
                else
                {
                    WaterLevelChange value2 = m_Changes[m_FloodEntities[0]];
                    value2.m_Intensity = math.max(0f, (value.m_FloodCounter - 20f) / 80f);
                    m_Changes[m_FloodEntities[0]] = value2;
                }
            }
            m_FloodCounterDatas[m_FloodCounterEntity] = value;
            int num5 = 0;
            int num6 = 0;
            int num7 = 0;
            int num8 = m_LoadDistributionIndex * SoilWaterSystem.kTextureSize / SoilWaterSystem.kLoadDistribution;
            int num9 = num8 + SoilWaterSystem.kTextureSize / SoilWaterSystem.kLoadDistribution;
            for (int j = num8 * SoilWaterSystem.kTextureSize; j < num9 * SoilWaterSystem.kTextureSize; j++)
            {
                SoilWater value3 = m_SoilWaterMap[j];
                value3.m_Amount = (short)math.max(0, value3.m_Amount + tmp[j] + Mathf.RoundToInt(m_SoilWaterParameters.m_RainMultiplier * num3));
                float num10 = value3.m_Surface = TerrainUtils.SampleHeight(ref m_TerrainHeightData, GetCellCenter(j, SoilWaterSystem.kTextureSize));
                short num11 = (short)Mathf.RoundToInt(math.max(0f, 0.1f * (0.5f * value3.m_Max - value3.m_Amount)));
                float x = num11 * m_SoilWaterParameters.m_WaterPerUnit / value3.m_Max;
                int num12 = 0;
                int num13 = 0;
                float num14 = 0f;
                float num15 = 0f;
                int num16 = j % SoilWaterSystem.kTextureSize * @int.x + j / SoilWaterSystem.kTextureSize * m_WaterSurfaceData.resolution.x * @int.y;
                for (int k = 0; k < @int.x; k += 4)
                {
                    for (int l = 0; l < @int.y; l += 4)
                    {
                        float depth = m_WaterSurfaceData.depths[num16 + k + l * m_WaterSurfaceData.resolution.z].m_Depth;
                        if (depth > 0.01f)
                        {
                            num12++;
                            num14 += math.min(m_SoilWaterParameters.m_MaximumWaterDepth, depth);
                            num15 += math.min(x, depth);
                        }
                        num13++;
                    }
                }
                num11 = (short)Math.Min(num11, Mathf.RoundToInt(value3.m_Max * 10f * num15));
                x = num11 * m_SoilWaterParameters.m_WaterPerUnit / value3.m_Max;
                float num17 = (1f - num4 * num14 / num13) * value3.m_Max;
                short num18 = (short)Mathf.RoundToInt(math.max(0f, m_SoilWaterParameters.m_OverflowRate * (value3.m_Amount - num17)));
                float num19 = 0f;
                if (num18 > 0f)
                {
                    num19 = value3.m_Amount / (float)value3.m_Max;
                    x = 0f;
                }
                if (num12 == 0)
                {
                    x = 0f;
                }
                value3.m_Amount += num11;
                value3.m_Amount -= num18;
                short num20 = (short)Mathf.RoundToInt(math.sign(value3.m_Max / 8 - value3.m_Amount));
                value3.m_Amount += num20;
                num6 += num11 + Math.Max((short)0, num20);
                num5 += num18 + Math.Max(0, -num20);
                num7 += value3.m_Amount;
                m_SoilWaterTextureData[j] = (0f - x) / m_ShaderUpdatesPerSoilUpdate + num19;
                m_SoilWaterMap[j] = value3;
            }
            tmp.Dispose();
        }
    }

}
