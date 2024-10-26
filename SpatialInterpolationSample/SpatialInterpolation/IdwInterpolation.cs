using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SpatialInterpolationSample.SpatialInterpolation
{
    public class IdwInterpolation : ISpatialInterpolation
    {
        public float SearchRadius { get; set; } = float.PositiveInfinity;
        public float WeightPower { get; set; } = 1;

        public Task<float[,]> Interpolate(int width, int height, IReadOnlyList<SpatialSample> samples, CancellationToken cancellationToken)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            var searchRadius = SearchRadius;
            var weightPower = WeightPower;
            float[,] results = new float[height, width];

            return Task.Run(() =>
            {
                if (samples == null || samples.Count == 0)
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            if (cancellationToken.IsCancellationRequested) return null;
                            results.SetValue(float.NaN, y, x);
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            var sum = 0d;
                            var weights = 0d;
                            foreach (var sample in samples)
                            {
                                if (cancellationToken.IsCancellationRequested) return null;
                                var distance = Math.Sqrt(Math.Pow(x - sample.X, 2) + Math.Pow(y - sample.Y, 2));
                                if (distance <= searchRadius)
                                {
                                    var weight = distance == 0 ? 1 : (1 / Math.Pow(distance, weightPower));
                                    sum += sample.Value * weight;
                                    weights += weight;
                                }
                            }
                            results[y, x] = weights == 0 ? float.NaN : (float)(sum / weights);
                        }
                    }
                }
                return results;

            }, cancellationToken);
        }
    }
}
