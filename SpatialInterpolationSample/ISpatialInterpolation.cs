using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SpatialInterpolation
{
    public interface ISpatialInterpolation
    {
        Task Interpolate(IEnumerable<SpatialSample> samples, float[,] target, CancellationToken cancellationToken);
    }

    public static class SpatialInterpolationExtensions
    {
        public static Task Interpolate<T>(this T interpolation, IEnumerable<SpatialSample> samples, float[,] target)
            where T : ISpatialInterpolation => interpolation.Interpolate(samples, target, CancellationToken.None);
    }
}
