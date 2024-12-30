using ILGPU;
using ILGPU.Runtime;
using System.Numerics;

namespace SpatialInterpolation.Kernels
{
    struct GpuIdwInterpolationKernel(
        ArrayView1D<SpatialSample, Stride1D.Dense> samples,
        ArrayView2D<float, Stride2D.DenseX> values,
        float searchRadius,
        float weightPower)
    {
        public void Execute(Index2D id)
        {
            var sum = 0f;
            var weights = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                var distance = Vector2.Distance(new Vector2(id.X, id.Y), new Vector2(samples[i].Y, samples[i].X));
                if (distance <= searchRadius)
                {
                    var weight = distance == 0 ? 1 : (1 / float.Pow(distance, weightPower));
                    sum += weight * samples[i].Value;
                    weights += weight;
                }
            }
            values[id] = sum / weights;
        }
    }
}
