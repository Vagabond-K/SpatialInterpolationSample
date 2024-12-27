using ILGPU;
using ILGPU.Runtime;
using System.Numerics;

namespace SpatialInterpolation.Kernels
{
    struct SpatialHeatMapKernel(
        ArrayView2D<int, Stride2D.DenseX> heatMap,
        ArrayView1D<Vector4, Stride1D.Dense> colors,
        ArrayView1D<float, Stride1D.Dense> offsets,
        ArrayView2D<float, Stride2D.DenseX> values,
        Vector4 contourColor,
        uint levels,
        float maximum,
        float minimum)
    {
        private static float ToSRgb(float value)
        {
            if (!(value > 0.0f))
                return 0;
            else if (value <= 0.0031308f)
                return value * 12.92f;
            else if (value < 1.0)
                return 1.055f * float.Pow(value, 1.0f / 2.4f) - 0.055f;
            else
                return 1.0f;
        }

        private static Vector4 ToSRgba(Vector4 color)
            => new(ToSRgb(color.X), ToSRgb(color.Y), ToSRgb(color.Z), color.W);

        private readonly float ToOffset(Index2D id)
            => float.IsNaN(values[id]) ? 0.0f : float.Max(0f, float.Min(1f, (values[id] - minimum) / (maximum - minimum)));

        private readonly Vector4 ToColor(float offset)
        {
            var ranges = colors.Length - 1;

            int index;
            for (index = 0; index < ranges; index++)
                if (offset < offsets[index])
                    break;

            if (index == 0 || offsets[index] <= offset) return colors[index];
            var range = offsets[index] - offsets[index - 1];
            var alpha = (offset - offsets[index - 1]) / range;
            var colorA = colors[index];
            var colorB = colors[index - 1];

            return Vector4.Lerp(colorB, colorA, alpha);
        }

        private readonly float Posterize(int x, int y)
            => float.Floor(ToOffset(new Index2D(x, y)) * levels) / levels;

        private readonly float GetLineOpacity(int currX, int currY)
        {
            if (levels < 2) return 0;

            var prevX = currX + (currX <= 0 ? 0 : -1);
            var prevY = currY + (currY <= 0 ? 0 : -1);
            var nextX = currX + (currX >= values.IntExtent.X - 1 ? 0 : 1);
            var nextY = currY + (currY >= values.IntExtent.Y - 1 ? 0 : 1);

            var gX = Posterize(prevX, prevY) - Posterize(nextX, prevY)
                + (Posterize(prevX, currY) - Posterize(nextX, currY)) * 2
                + Posterize(prevX, nextY) - Posterize(nextX, nextY);

            var gY = Posterize(prevX, prevY) - Posterize(prevX, nextY)
                + (Posterize(currX, prevY) - Posterize(currX, nextY)) * 2
                + Posterize(nextX, prevY) - Posterize(nextX, nextY);

            gX *= levels / 4f;
            gY *= levels / 4f;

            return float.Sqrt(gX * gX + gY * gY);
        }

        private void ExecuteKernel(Index2D id)
        {
            var color = ToColor(ToOffset(id));
            float lineOpacity = GetLineOpacity(id.X, id.Y) * contourColor.W;
            var result = Vector4.Lerp(color, new Vector4(contourColor.X, contourColor.Y, contourColor.Z, 1), lineOpacity);

            result = ToSRgba(result);
            heatMap[id] = ((int)(result.W * 255f) << 24)
                | ((int)(result.X * 255f) << 16)
                | ((int)(result.Y * 255f) << 8)
                | ((int)(result.Z * 255f));
        }

        public static void Execute(Index2D id, SpatialHeatMapKernel data) => data.ExecuteKernel(id);
    }
}
