using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Game.Simulation.WindSimulationSystem;

/*
		public struct WindCell : ISerializable
		{
			public float m_Pressure;

			public float3 m_Velocities;

			public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
			{
				writer.Write(this.m_Pressure);
				writer.Write(this.m_Velocities);
			}

			public void Deserialize<TReader>(TReader reader) where TReader : IReader
			{
				reader.Read(out this.m_Pressure);
				reader.Read(out this.m_Velocities);
			}
		}
*/

namespace MapExtPDX
{
    // cellcenter in bcjob
    [BurstCompile]
    public struct UpdateWindVelocityJob : IJobFor
    {
        public NativeArray<WindCell> m_Cells;

        [ReadOnly]
        public TerrainHeightData m_TerrainHeightData;

        [ReadOnly]
        public WaterSurfaceData m_WaterSurfaceData;

        public float2 m_TerrainRange;

        public void Execute(int index)
        {
            int3 @int = new int3(index % WindSimulationSystem.kResolution.x, index / WindSimulationSystem.kResolution.x % WindSimulationSystem.kResolution.y, index / (WindSimulationSystem.kResolution.x * WindSimulationSystem.kResolution.y));
            bool3 @bool = new bool3(@int.x >= WindSimulationSystem.kResolution.x - 1, @int.y >= WindSimulationSystem.kResolution.y - 1, @int.z >= WindSimulationSystem.kResolution.z - 1);
            if (!@bool.x && !@bool.y && !@bool.z)
            {
                int3 position = new int3(@int.x, @int.y + 1, @int.z);
                int3 position2 = new int3(@int.x + 1, @int.y, @int.z);
                float3 cellCenter = WindSimulationSystem.GetCellCenter(index);
                cellCenter.y = math.lerp(this.m_TerrainRange.x, this.m_TerrainRange.y, ((float)@int.z + 0.5f) / (float)WindSimulationSystem.kResolution.z);
                float num = WaterUtils.SampleHeight(ref this.m_WaterSurfaceData, ref this.m_TerrainHeightData, cellCenter);
                float num2 = WaterUtils.SampleHeight(ref this.m_WaterSurfaceData, ref this.m_TerrainHeightData, cellCenter);
                float num3 = WaterUtils.SampleHeight(ref this.m_WaterSurfaceData, ref this.m_TerrainHeightData, cellCenter);
                float num4 = 65535f / (this.m_TerrainHeightData.scale.y * (float)WindSimulationSystem.kResolution.z);
                float num5 = math.saturate((0.5f * (num4 + num + num2) - cellCenter.y) / num4);
                float num6 = math.saturate((0.5f * (num4 + num + num3) - cellCenter.y) / num4);
                WindCell value = this.m_Cells[index];
                WindCell cell = WindSimulationSystem.GetCell(new int3(@int.x, @int.y, @int.z + 1), this.m_Cells);
                WindCell cell2 = WindSimulationSystem.GetCell(position, this.m_Cells);
                WindCell cell3 = WindSimulationSystem.GetCell(position2, this.m_Cells);
                value.m_Velocities.x *= math.lerp(WindSimulationSystem.kAirSlowdown, WindSimulationSystem.kTerrainSlowdown, num6);
                value.m_Velocities.y *= math.lerp(WindSimulationSystem.kAirSlowdown, WindSimulationSystem.kTerrainSlowdown, num5);
                value.m_Velocities.z *= WindSimulationSystem.kVerticalSlowdown;
                value.m_Velocities.x += WindSimulationSystem.kChangeFactor * (1f - num6) * (value.m_Pressure - cell3.m_Pressure);
                value.m_Velocities.y += WindSimulationSystem.kChangeFactor * (1f - num5) * (value.m_Pressure - cell2.m_Pressure);
                value.m_Velocities.z += WindSimulationSystem.kChangeFactor * (value.m_Pressure - cell.m_Pressure);
                this.m_Cells[index] = value;
            }
        }
    }

}
// no cell in bcjob
/*
[BurstCompile]
public struct UpdatePressureJob : IJobFor
{
    public NativeArray<WindCell> m_Cells;

    public float2 m_Wind;

    public void Execute(int index)
    {
        int3 @int = new int3(index % WindSimulationSystem.kResolution.x, index / WindSimulationSystem.kResolution.x % WindSimulationSystem.kResolution.y, index / (WindSimulationSystem.kResolution.x * WindSimulationSystem.kResolution.y));
        bool3 @bool = new bool3(@int.x == 0, @int.y == 0, @int.z == 0);
        bool3 bool2 = new bool3(@int.x >= WindSimulationSystem.kResolution.x - 1, @int.y >= WindSimulationSystem.kResolution.y - 1, @int.z >= WindSimulationSystem.kResolution.z - 1);
        if (!bool2.x && !bool2.y && !bool2.z)
        {
            WindCell value = this.m_Cells[index];
            value.m_Pressure -= value.m_Velocities.x + value.m_Velocities.y + value.m_Velocities.z;
            if (!@bool.x)
            {
                WindCell cell = WindSimulationSystem.GetCell(new int3(@int.x - 1, @int.y, @int.z), this.m_Cells);
                value.m_Pressure += cell.m_Velocities.x;
            }
            if (!@bool.y)
            {
                WindCell cell2 = WindSimulationSystem.GetCell(new int3(@int.x, @int.y - 1, @int.z), this.m_Cells);
                value.m_Pressure += cell2.m_Velocities.y;
            }
            if (!@bool.z)
            {
                WindCell cell3 = WindSimulationSystem.GetCell(new int3(@int.x, @int.y, @int.z - 1), this.m_Cells);
                value.m_Pressure += cell3.m_Velocities.z;
            }
            this.m_Cells[index] = value;
        }
        if (@bool.x || @bool.y || bool2.x || bool2.y)
        {
            WindCell value2 = this.m_Cells[index];
            float num = math.dot(math.normalize(new float2(@int.x - WindSimulationSystem.kResolution.x / 2, @int.y - WindSimulationSystem.kResolution.y / 2)), math.normalize(this.m_Wind));
            float num2 = math.pow((1f + (float)@int.z) / (1f + (float)WindSimulationSystem.kResolution.z), 1f / 7f);
            float num3 = 0.1f * (2f - num);
            float num4 = (40f - 20f * (1f + num)) * math.length(this.m_Wind) * num2;
            value2.m_Pressure = ((num4 > value2.m_Pressure) ? math.min(num4, value2.m_Pressure + num3) : math.max(num4, value2.m_Pressure - num3));
            this.m_Cells[index] = value2;
        }
    }
}
*/

/*
		[Preserve]
		protected override void OnUpdate()
		{
			if (this.m_TerrainSystem.heightmap != null)
			{
				this.m_Odd = !this.m_Odd;
				if (!this.m_Odd)
				{
					TerrainHeightData data = this.m_TerrainSystem.GetHeightData();
					float x = TerrainUtils.ToWorldSpace(ref data, 0f);
					float y = TerrainUtils.ToWorldSpace(ref data, 65535f);
					float2 terrainRange = new float2(x, y);
					UpdateWindVelocityJob updateWindVelocityJob = default(UpdateWindVelocityJob);
					updateWindVelocityJob.m_Cells = this.m_Cells;
					updateWindVelocityJob.m_TerrainHeightData = data;
					updateWindVelocityJob.m_WaterSurfaceData = this.m_WaterSystem.GetSurfaceData(out var deps);
					updateWindVelocityJob.m_TerrainRange = terrainRange;
					UpdateWindVelocityJob jobData = updateWindVelocityJob;
					this.m_Deps = jobData.Schedule(WindSimulationSystem.kResolution.x * WindSimulationSystem.kResolution.y * WindSimulationSystem.kResolution.z, JobHandle.CombineDependencies(this.m_Deps, deps, base.Dependency));
					this.m_WaterSystem.AddSurfaceReader(this.m_Deps);
					this.m_TerrainSystem.AddCPUHeightReader(this.m_Deps);
				}
				else
				{
					UpdatePressureJob updatePressureJob = default(UpdatePressureJob);
					updatePressureJob.m_Cells = this.m_Cells;
					updatePressureJob.m_Wind = this.constantWind / 10f;
					UpdatePressureJob jobData2 = updatePressureJob;
					this.m_Deps = jobData2.Schedule(WindSimulationSystem.kResolution.x * WindSimulationSystem.kResolution.y * WindSimulationSystem.kResolution.z, JobHandle.CombineDependencies(this.m_Deps, base.Dependency));
				}
				base.Dependency = this.m_Deps;
			}
		}
*/