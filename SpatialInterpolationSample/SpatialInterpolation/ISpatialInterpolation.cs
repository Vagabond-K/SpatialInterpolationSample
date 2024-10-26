using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SpatialInterpolationSample.SpatialInterpolation
{
    public interface ISpatialInterpolation
    {
        Task<float[,]> Interpolate(int width, int height, IReadOnlyList<SpatialSample> samples, CancellationToken cancellationToken);
    }

    public static class SpatialInterpolationExtensions
    {
        public static Task<float[,]> Interpolate<T>(this T interpolation, int width, int height, IReadOnlyList<SpatialSample> samples)
            where T : ISpatialInterpolation => interpolation.Interpolate(width, height, samples, CancellationToken.None);
    }
}
