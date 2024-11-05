using ILGPU;
using ILGPU.Runtime;
using System.Numerics;

namespace SpatialInterpolation.Kernels
{
    struct SpatialHeatMapKernel(ArrayView2D<int, Stride2D.DenseX> results, ArrayView1D<Vector4, Stride1D.Dense> colorLevels, ArrayView2D<float, Stride2D.DenseX> values, Vector4 contourColor, uint contourLevels, float maximum, float minimum)
    {
        private readonly float ToRatio(Index2D id)
            => float.IsNaN(values[id]) ? 0.0f : float.Max(0f, float.Min(1f, (values[id] - minimum) / (maximum - minimum)));

        private static float ToSRgb(float value)
        {
            if (!(value > 0.0f))
                return 0;
            else if (value <= 0.0031308f)
                return (value * 12.92f);
            else if (value < 1.0)
                return ((1.055f * float.Pow(value, 1.0f / 2.4f)) - 0.055f);
            else
                return 1.0f;
        }
        private static Vector4 ToSRgba(Vector4 color)
            => new(ToSRgb(color.X), ToSRgb(color.Y), ToSRgb(color.Z), color.W);

        private float GetCtLevel(int x, int y, float contour)
        {
            float ratio = float.Floor(ToRatio(new Index2D(x, y)) * contourLevels + 0.5f) / contourLevels;
            return ratio > contour ? 0.7f : (ratio < contour ? 0.3f : 0.5f);
        }

        private void ExecuteKernel(Index2D id)
        {
            int colorsCount = (int)colorLevels.Length;
            Vector4 result = new(0, 0, 0, 0);

            float ratio = ToRatio(id);

            if (colorsCount == 1)
            {
                result = colorLevels[0];
            }
            else if (colorsCount > 1)
            {
                colorsCount -= 1;
                float levelUnit = 1.0f / colorsCount;
                int index = (int)float.Floor(ratio * colorsCount);
                float lerpSegment = (ratio - levelUnit * index) / levelUnit;
                result = Vector4.Lerp(colorLevels[index], colorLevels[index + 1], lerpSegment);
            }

            if (contourLevels > 0)
            {
                int width = (int)values.Extent.X;
                int height = (int)values.Extent.Y;

                int xPrev = id.X + (id.X <= 0 ? 0 : -1);
                int xNext = id.X + (id.X >= width - 1 ? 0 : 1);
                int yPrev = id.Y + (id.Y <= 0 ? 0 : -1);
                int yNext = id.Y + (id.Y >= height - 1 ? 0 : 1);

                float contour = float.Floor(ratio * contourLevels + 0.5f) / contourLevels;

                float h = -GetCtLevel(xPrev, yPrev, contour) + GetCtLevel(xNext, yPrev, contour)
                - GetCtLevel(xPrev, id.Y, contour) * 2 + GetCtLevel(xNext, id.Y, contour) * 2
                - GetCtLevel(xPrev, yNext, contour) + GetCtLevel(xNext, yNext, contour);

                float v = -GetCtLevel(xPrev, yPrev, contour) - GetCtLevel(id.X, yPrev, contour) * 2 - GetCtLevel(xNext, yPrev, contour)
                + GetCtLevel(xPrev, yNext, contour) + GetCtLevel(id.X, yNext, contour) * 2 + GetCtLevel(xNext, yNext, contour);

                float edgeRatio = float.Sqrt(h * h + v * v) * contourColor.W;
                if (edgeRatio > 0)
                    result = Vector4.Lerp(result, new Vector4(contourColor.X, contourColor.Y, contourColor.Z, 1), edgeRatio);
            }

            result = ToSRgba(result);

            results[id] = ((int)(result.W * 255f) << 24)
                | ((int)(result.Z * 255f) << 16)
                | ((int)(result.Y * 255f) << 8)
                | ((int)(result.X * 255f));
        }

        public static void Execute(Index2D id, SpatialHeatMapKernel data) => data.ExecuteKernel(id);
    }
}
