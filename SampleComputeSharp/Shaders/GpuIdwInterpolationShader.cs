using ComputeSharp;

namespace SpatialInterpolation.Shaders
{
    [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct GpuIdwInterpolationShader(
        ReadWriteTexture2D<float> results,
        ReadOnlyBuffer<float3> samples,
        float searchRadius,
        float weightPower) : IComputeShader
    {
        public void Execute()
        {
            float sum = 0f;
            float weights = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                var sample = samples[i];
                var distance = Hlsl.Distance(ThreadIds.XY, sample.XY);
                if (distance <= searchRadius)
                {
                    var weight = distance == 0 ? 1 : (1 / Hlsl.Pow(distance, weightPower));
                    sum += sample.Z * weight;
                    weights += weight;
                }
            }

            results[ThreadIds.XY] = weights == 0 ? float.NaN : (float)(sum / weights);
        }
    }
}
