using ILGPU;
using ILGPU.Runtime;
using System.Numerics;

namespace SpatialInterpolation.Kernels
{
    struct GpuIdwInterpolationKernel(ArrayView2D<float, Stride2D.DenseY> results, ArrayView1D<Vector3, Stride1D.Dense> samples, float searchRadius, float weightPower)
    {
        private void ExecuteKernel(Index2D id)
        {
            float sum = 0f;
            float weights = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                var sample = samples[i];
                var distance = Vector2.Distance(new Vector2(id.X, id.Y), new Vector2(sample.Y, sample.X));
                if (distance <= searchRadius)
                {
                    var weight = distance == 0 ? 1 : (1 / float.Pow(distance, weightPower));
                    sum += sample.Z * weight;
                    weights += weight;
                }
            }

            results[id] = weights == 0 ? float.NaN : (float)(sum / weights);
        }

        public static void Execute(Index2D id, GpuIdwInterpolationKernel data) => data.ExecuteKernel(id);
    }
}
