using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using MapExtPDX.MapExt.Core;
//using static Game.Simulation.WindSimulationSystem;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    // cellcenter in bcjob
    [BurstCompile]
    public struct UpdateWindVelocityJob : IJobFor
    {
        public NativeArray<WindSimulationSystem.WindCell> m_Cells;

        [ReadOnly]
        public TerrainHeightData m_TerrainHeightData;

        [ReadOnly]
        public WaterSurfaceData<SurfaceWater> m_WaterSurfaceData;

        public float2 m_TerrainRange;

        public void Execute(int index)
        {
            // 重定由于Deserialize硬编码限制，暂不修改该
            int kTextureSize = XCellMapSystemRe.WindSystemkTextureSize;
            // 重定
            int3 kResolution = new(kTextureSize, kTextureSize, 16);

            // 核心修正：为了保持物理波速不变，耦合系数 kChangeFactor 必须按比例的平方反比衰减
            // 如果不平方，风力随尺寸增加仍会过大；如果修改 Slowdown，风力会过快衰减
            // 不更改kTextureSize时无需修改
            //int m_CellSizeScale = XCellMapSystemRe.CellMapTextureSizeMultiplier;
            float kChangeFactor = WindSimulationSystem.kChangeFactor /*/ (m_CellSizeScale * m_CellSizeScale)*/;

            // kTextureSize不变update_interval不变时该值不必改
            float kTerrainSlowdown = WindSimulationSystem.kTerrainSlowdown;
            // kTextureSize不变时该值不必改
            float kAirSlowdown = WindSimulationSystem.kAirSlowdown;

            int3 @int = new int3(index % kResolution.x, index / kResolution.x % kResolution.y, index / (kResolution.x * kResolution.y));
            bool3 @bool = new bool3(@int.x >= kResolution.x - 1, @int.y >= kResolution.y - 1, @int.z >= kResolution.z - 1);
            if (!@bool.x && !@bool.y && !@bool.z)
            {
                int3 position = new int3(@int.x, @int.y + 1, @int.z);
                int3 position2 = new int3(@int.x + 1, @int.y, @int.z);
                float3 cellCenter = XCellMapSystemRe.WindSimulationSystemGetCellCenter(index);
                cellCenter.y = math.lerp(m_TerrainRange.x, m_TerrainRange.y, (@int.z + 0.5f) / kResolution.z);
                float num = WaterUtils.SampleHeight(ref m_WaterSurfaceData, ref m_TerrainHeightData, cellCenter);
                float num2 = WaterUtils.SampleHeight(ref m_WaterSurfaceData, ref m_TerrainHeightData, cellCenter);
                float num3 = WaterUtils.SampleHeight(ref m_WaterSurfaceData, ref m_TerrainHeightData, cellCenter);
                float num4 = 65535f / (m_TerrainHeightData.scale.y * kResolution.z);
                float num5 = math.saturate((0.5f * (num4 + num + num2) - cellCenter.y) / num4);
                float num6 = math.saturate((0.5f * (num4 + num + num3) - cellCenter.y) / num4);
                WindSimulationSystem.WindCell value = m_Cells[index];
                WindSimulationSystem.WindCell cell = XCellMapSystemRe.WindSimulationSystemGetCell(new int3(@int.x, @int.y, @int.z + 1), m_Cells);
                WindSimulationSystem.WindCell cell2 = XCellMapSystemRe.WindSimulationSystemGetCell(position, m_Cells);
                WindSimulationSystem.WindCell cell3 = XCellMapSystemRe.WindSimulationSystemGetCell(position2, m_Cells);
                value.m_Velocities.x *= math.lerp(kAirSlowdown, kTerrainSlowdown, num6);
                value.m_Velocities.y *= math.lerp(kAirSlowdown, kTerrainSlowdown, num5);
                value.m_Velocities.z *= WindSimulationSystem.kVerticalSlowdown;
                value.m_Velocities.x += kChangeFactor * (1f - num6) * (value.m_Pressure - cell3.m_Pressure);
                value.m_Velocities.y += kChangeFactor * (1f - num5) * (value.m_Pressure - cell2.m_Pressure);
                value.m_Velocities.z += kChangeFactor * (value.m_Pressure - cell.m_Pressure);
                m_Cells[index] = value;
            }
        }


    }

    [BurstCompile]
    public struct UpdatePressureJob : IJobFor
    {
        public NativeArray<WindSimulationSystem.WindCell> m_Cells;

        public float2 m_Wind;

        public void Execute(int index)
        {
            // 重定
            int kTextureSize = XCellMapSystemRe.WindSystemkTextureSize;
            // 重定
            int3 kResolution = new(kTextureSize, kTextureSize, 16);

            int3 @int = new int3(index % kResolution.x, index / kResolution.x % kResolution.y, index / (kResolution.x * kResolution.y));
            bool3 @bool = new bool3(@int.x == 0, @int.y == 0, @int.z == 0);
            bool3 bool2 = new bool3(@int.x >= kResolution.x - 1, @int.y >= kResolution.y - 1, @int.z >= kResolution.z - 1);
            if (!bool2.x && !bool2.y && !bool2.z)
            {
                WindSimulationSystem.WindCell value = m_Cells[index];
                value.m_Pressure -= value.m_Velocities.x + value.m_Velocities.y + value.m_Velocities.z;
                if (!@bool.x)
                {
                    WindSimulationSystem.WindCell cell = XCellMapSystemRe.WindSimulationSystemGetCell(new int3(@int.x - 1, @int.y, @int.z), m_Cells);
                    value.m_Pressure += cell.m_Velocities.x;
                }

                if (!@bool.y)
                {
                    WindSimulationSystem.WindCell cell2 = XCellMapSystemRe.WindSimulationSystemGetCell(new int3(@int.x, @int.y - 1, @int.z), m_Cells);
                    value.m_Pressure += cell2.m_Velocities.y;
                }

                if (!@bool.z)
                {
                    WindSimulationSystem.WindCell cell3 = XCellMapSystemRe.WindSimulationSystemGetCell(new int3(@int.x, @int.y, @int.z - 1), m_Cells);
                    value.m_Pressure += cell3.m_Velocities.z;
                }

                m_Cells[index] = value;
            }

            if (@bool.x || @bool.y || bool2.x || bool2.y)
            {
                WindSimulationSystem.WindCell value2 = m_Cells[index];
                float num = math.dot(math.normalize(new float2(@int.x - kResolution.x / 2, @int.y - kResolution.y / 2)), math.normalize(m_Wind));
                float num2 = math.pow((1f + (float)@int.z) / (1f + (float)kResolution.z), 1f / 7f);
                float num3 = 0.1f * (2f - num);
                float num4 = (40f - 20f * (1f + num)) * math.length(m_Wind) * num2;
                value2.m_Pressure = ((num4 > value2.m_Pressure) ? math.min(num4, value2.m_Pressure + num3) : math.max(num4, value2.m_Pressure - num3));
                m_Cells[index] = value2;
            }
        }
    }

}

