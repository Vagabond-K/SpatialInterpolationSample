using ComputeSharp;

namespace SpatialInterpolation.Shaders
{
    [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct GpuIdwInterpolationShader(
        ReadWriteTexture2D<float> values,
        ReadOnlyBuffer<float3> samples,
        float searchRadius,
        float weightPower) : IComputeShader
    {
        public void Execute()
        {
            var sum = 0f;
            var weights = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                var distance = Hlsl.Distance(ThreadIds.XY, samples[i].XY);
                if (distance <= searchRadius)
                {
                    var weight = distance == 0 ? 1 : (1 / Hlsl.Pow(distance, weightPower));
                    sum += weight * samples[i].Z;
                    weights += weight;
                }
            }
            values[ThreadIds.XY] = sum / weights;
        }
    }
}
