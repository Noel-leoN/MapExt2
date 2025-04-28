#define UNITY_ASSERTIONS
using Colossal.Collections;
using Game.Prefabs;
using Game.Simulation;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapExtPDX
{
    [BurstCompile]
    public struct GroundWaterTickJob : IJob
    {
        public NativeArray<GroundWater> m_GroundWaterMap;

        public WaterPipeParameterData m_Parameters;

        private void HandlePollution(int index, int otherIndex, NativeArray<int2> tmp)
        {
            GroundWater groundWater = this.m_GroundWaterMap[index];
            GroundWater groundWater2 = this.m_GroundWaterMap[otherIndex];
            ref int2 reference = ref tmp.ElementAt(index);
            ref int2 reference2 = ref tmp.ElementAt(otherIndex);
            int num = groundWater.m_Polluted + groundWater2.m_Polluted;
            int num2 = groundWater.m_Amount + groundWater2.m_Amount;
            int num3 = math.clamp((((num2 > 0) ? (groundWater.m_Amount * num / num2) : 0) - groundWater.m_Polluted) / 4, -(groundWater2.m_Amount - groundWater2.m_Polluted) / 4, (groundWater.m_Amount - groundWater.m_Polluted) / 4);
            reference.y += num3;
            reference2.y -= num3;
            Assert.IsTrue(0 <= groundWater.m_Polluted + reference.y);
            Assert.IsTrue(groundWater.m_Polluted + reference.y <= groundWater.m_Amount);
            Assert.IsTrue(0 <= groundWater2.m_Polluted + reference2.y);
            Assert.IsTrue(groundWater2.m_Polluted + reference2.y <= groundWater2.m_Amount);
        }

        private void HandleFlow(int index, int otherIndex, NativeArray<int2> tmp)
        {
            GroundWater groundWater = this.m_GroundWaterMap[index];
            GroundWater groundWater2 = this.m_GroundWaterMap[otherIndex];
            ref int2 reference = ref tmp.ElementAt(index);
            ref int2 reference2 = ref tmp.ElementAt(otherIndex);
            Assert.IsTrue(groundWater2.m_Polluted + reference2.y <= groundWater2.m_Amount + reference2.x);
            Assert.IsTrue(groundWater.m_Polluted + reference.y <= groundWater.m_Amount + reference.x);
            float num = ((groundWater.m_Amount + reference.x != 0) ? (1f * (float)(groundWater.m_Polluted + reference.y) / (float)(groundWater.m_Amount + reference.x)) : 0f);
            float num2 = ((groundWater2.m_Amount + reference2.x != 0) ? (1f * (float)(groundWater2.m_Polluted + reference2.y) / (float)(groundWater2.m_Amount + reference2.x)) : 0f);
            int num3 = groundWater.m_Amount - groundWater.m_Max;
            int num4 = math.clamp((groundWater2.m_Amount - groundWater2.m_Max - num3) / 4, -groundWater.m_Amount / 4, groundWater2.m_Amount / 4);
            reference.x += num4;
            reference2.x -= num4;
            int num5 = 0;
            if (num4 > 0)
            {
                num5 = (int)((float)num4 * num2);
            }
            else if (num4 < 0)
            {
                num5 = (int)((float)num4 * num);
            }
            reference.y += num5;
            reference2.y -= num5;
            Assert.IsTrue(0 <= groundWater.m_Amount + reference.x);
            Assert.IsTrue(groundWater.m_Amount + reference.x <= groundWater.m_Max);
            Assert.IsTrue(0 <= groundWater2.m_Amount + reference2.x);
            Assert.IsTrue(groundWater2.m_Amount + reference2.x <= groundWater2.m_Max);
            Assert.IsTrue(0 <= groundWater.m_Polluted + reference.y);
            Assert.IsTrue(groundWater.m_Polluted + reference.y <= groundWater.m_Amount + reference.x);
            Assert.IsTrue(0 <= groundWater2.m_Polluted + reference2.y);
            Assert.IsTrue(groundWater2.m_Polluted + reference2.y <= groundWater2.m_Amount + reference2.x);
        }

        public void Execute()
        {
            NativeArray<int2> tmp = new NativeArray<int2>(this.m_GroundWaterMap.Length, Allocator.Temp);
            for (int i = 0; i < this.m_GroundWaterMap.Length; i++)
            {
                int num = i % GroundWaterSystem.kTextureSize;
                int num2 = i / GroundWaterSystem.kTextureSize;
                if (num < GroundWaterSystem.kTextureSize - 1)
                {
                    this.HandlePollution(i, i + 1, tmp);
                }
                if (num2 < GroundWaterSystem.kTextureSize - 1)
                {
                    this.HandlePollution(i, i + GroundWaterSystem.kTextureSize, tmp);
                }
            }
            for (int j = 0; j < this.m_GroundWaterMap.Length; j++)
            {
                int num3 = j % GroundWaterSystem.kTextureSize;
                int num4 = j / GroundWaterSystem.kTextureSize;
                if (num3 < GroundWaterSystem.kTextureSize - 1)
                {
                    this.HandleFlow(j, j + 1, tmp);
                }
                if (num4 < GroundWaterSystem.kTextureSize - 1)
                {
                    this.HandleFlow(j, j + GroundWaterSystem.kTextureSize, tmp);
                }
            }
            for (int k = 0; k < this.m_GroundWaterMap.Length; k++)
            {
                GroundWater value = this.m_GroundWaterMap[k];
                value.m_Amount = (short)math.min(value.m_Amount + tmp[k].x + Mathf.CeilToInt(this.m_Parameters.m_GroundwaterReplenish * (float)value.m_Max), value.m_Max);
                value.m_Polluted = (short)math.clamp(value.m_Polluted + tmp[k].y - this.m_Parameters.m_GroundwaterPurification, 0, value.m_Amount);
                this.m_GroundWaterMap[k] = value;
            }
            tmp.Dispose();
        }
    }

}
/*

[Preserve]
protected override void OnUpdate()
{
    GroundWaterTickJob groundWaterTickJob = default(GroundWaterTickJob);
    groundWaterTickJob.m_GroundWaterMap = base.m_Map;
    groundWaterTickJob.m_Parameters = this.m_ParameterQuery.GetSingleton<WaterPipeParameterData>();
    GroundWaterTickJob jobData = groundWaterTickJob;
    base.Dependency = IJobExtensions.Schedule(jobData, JobHandle.CombineDependencies(base.m_WriteDependencies, base.m_ReadDependencies, base.Dependency));
    base.AddWriter(base.Dependency);
    base.Dependency = JobHandle.CombineDependencies(base.m_ReadDependencies, base.m_WriteDependencies, base.Dependency);
}
*/