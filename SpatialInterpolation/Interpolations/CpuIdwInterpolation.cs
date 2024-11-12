using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SpatialInterpolation.Interpolations
{
    public class CpuIdwInterpolation : ISpatialInterpolation
    {
        public float SearchRadius { get; set; } = float.PositiveInfinity;
        public float WeightPower { get; set; } = 1;

        public Task Interpolate(IEnumerable<SpatialSample> samples, float[,] target, CancellationToken cancellationToken)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (target == null) throw new ArgumentNullException(nameof(target));
            var sampleArray = samples.ToArray();
            if (sampleArray.Length == 0) throw new ArgumentOutOfRangeException(nameof(samples));
            return Task.Run(() =>
            {
                int width = target.GetLength(1);
                int height = target.GetLength(0);

                var searchRadius = SearchRadius;
                var weightPower = WeightPower;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var sum = 0f;
                        var weights = 0f;
                        foreach (var sample in sampleArray)
                        {
                            if (cancellationToken.IsCancellationRequested) return;
                            var distance = MathF.Sqrt(MathF.Pow(x - sample.X, 2) + MathF.Pow(y - sample.Y, 2));
                            if (distance <= searchRadius)
                            {
                                var weight = distance == 0 ? 1 : (1 / MathF.Pow(distance, weightPower));
                                sum += sample.Value * weight;
                                weights += weight;
                            }
                        }
                        target[y, x] = sum / weights;
                    }
                }
            }, cancellationToken);
        }
    }
}
