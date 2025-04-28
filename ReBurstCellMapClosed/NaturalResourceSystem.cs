#define UNITY_ASSERTIONS
using System.Runtime.CompilerServices;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game.Common;
using Game.Prefabs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Scripting;

namespace Game.Simulation
{
	[CompilerGenerated]
	public class NaturalResourceSystem : CellMapSystem<NaturalResourceCell>, IJobSerializable
	{
		[BurstCompile]
		private struct RegenerateFertilityJob : IJobParallelFor
		{
			[ReadOnly]
			public int m_RegenerationRate;

			[ReadOnly]
			public float m_PollutionRate;

			public CellMapData<NaturalResourceCell> m_CellData;

			[ReadOnly]
			public CellMapData<GroundPollution> m_PollutionData;

			public RandomSeed m_RandomSeed;

			public void Execute(int index)
			{
				NaturalResourceCell value = this.m_CellData.m_Buffer[index];
				GroundPollution groundPollution = this.m_PollutionData.m_Buffer[index];
				Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(1 + index);
				value.m_Fertility.m_Used = (ushort)math.min(value.m_Fertility.m_Base, math.max(0, value.m_Fertility.m_Used - this.m_RegenerationRate + MathUtils.RoundToIntRandom(ref random, (float)groundPollution.m_Pollution * this.m_PollutionRate)));
				this.m_CellData.m_Buffer[index] = value;
			}
		}

		public const int MAX_BASE_RESOURCES = 10000;

		public const int FERTILITY_REGENERATION_RATE = 800;

		public const int UPDATES_PER_DAY = 8;

		public static readonly int kTextureSize = 256;

		public GroundPollutionSystem m_GroundPollutionSystem;

		private EntityQuery m_PollutionParameterQuery;

		public int2 TextureSize => new int2(NaturalResourceSystem.kTextureSize, NaturalResourceSystem.kTextureSize);

		public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 32768;
		}

		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_GroundPollutionSystem = base.World.GetOrCreateSystemManaged<GroundPollutionSystem>();
			this.m_PollutionParameterQuery = base.GetEntityQuery(ComponentType.ReadOnly<PollutionParameterData>());
			base.CreateTextures(NaturalResourceSystem.kTextureSize);
		}

		public override JobHandle SetDefaults(Context context)
		{
			JobHandle result = base.SetDefaults(context);
			if (context.purpose == Purpose.NewGame)
			{
				result.Complete();
				float3 float4 = default(float3);
				for (int i = 0; i < base.m_Map.Length; i++)
				{
					float num = (float)(i % NaturalResourceSystem.kTextureSize) / (float)NaturalResourceSystem.kTextureSize;
					float num2 = (float)(i / NaturalResourceSystem.kTextureSize) / (float)NaturalResourceSystem.kTextureSize;
					float3 @float = new float3(6.1f, 13.9f, 10.7f);
					float3 float2 = num * @float;
					float3 float3 = num2 * @float;
					float4.x = Mathf.PerlinNoise(float2.x, float3.x);
					float4.y = Mathf.PerlinNoise(float2.y, float3.y);
					float4.z = Mathf.PerlinNoise(float2.z, float3.z);
					float4 = (float4 - new float3(0.4f, 0.7f, 0.7f)) * new float3(5f, 10f, 10f);
					float4 = 10000f * math.saturate(float4);
					NaturalResourceCell value = default(NaturalResourceCell);
					value.m_Fertility.m_Base = (ushort)float4.x;
					value.m_Ore.m_Base = (ushort)float4.y;
					value.m_Oil.m_Base = (ushort)float4.z;
					base.m_Map[i] = value;
				}
			}
			return result;
		}

		[Preserve]
		protected override void OnUpdate()
		{
			Assert.AreEqual(GroundPollutionSystem.kTextureSize, NaturalResourceSystem.kTextureSize, "Ground pollution and Natural resources need to have the same resolution");
			RegenerateFertilityJob jobData = default(RegenerateFertilityJob);
			jobData.m_RegenerationRate = 100;
			jobData.m_PollutionRate = this.m_PollutionParameterQuery.GetSingleton<PollutionParameterData>().m_FertilityGroundMultiplier / 8f;
			jobData.m_CellData = base.GetData(readOnly: false, out var dependencies);
			jobData.m_PollutionData = this.m_GroundPollutionSystem.GetData(readOnly: true, out var dependencies2);
			jobData.m_RandomSeed = RandomSeed.Next();
			JobHandle jobHandle = IJobParallelForExtensions.Schedule(jobData, NaturalResourceSystem.kTextureSize * NaturalResourceSystem.kTextureSize, NaturalResourceSystem.kTextureSize, JobHandle.CombineDependencies(dependencies2, dependencies));
			base.AddWriter(jobHandle);
			this.m_GroundPollutionSystem.AddReader(jobHandle);
		}

		public float ResourceAmountToArea(float amount)
		{
			float2 @float = (float2)CellMapSystem<NaturalResourceCell>.kMapSize / (float2)this.TextureSize;
			return amount * @float.x * @float.y / 10000f;
		}

		[Preserve]
		public NaturalResourceSystem()
		{
		}
	}
}
