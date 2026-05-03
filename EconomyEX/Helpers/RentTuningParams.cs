using Unity.Mathematics;

namespace EconomyEX.Helpers
{
    /// <summary>
    /// 租金公式调节参数 (Burst 兼容值类型)
    /// 通过 OnUpdate() 从 ModSettings 读取后传入 AdjustRentJob。
    /// 各分量: x=住宅(Residential), y=商业(Commercial), z=工业(Industrial)
    /// </summary>
    public struct RentTuningParams
    {
        /// <summary>租金总乘数 (百分比转小数，默认1.0)</summary>
        public float3 RentMultiplier;

        /// <summary>地价贡献系数 (百分比转小数，默认1.0)</summary>
        public float3 LandValueFactor;

        /// <summary>等级贡献系数 (百分比转小数，默认1.0)</summary>
        public float3 LevelFactor;

        /// <summary>创建默认参数 (全部 1.0)</summary>
        public static RentTuningParams Default => new RentTuningParams
        {
            RentMultiplier = new float3(1f, 1f, 1f),
            LandValueFactor = new float3(1f, 1f, 1f),
            LevelFactor = new float3(1f, 1f, 1f),
        };
    }
}
