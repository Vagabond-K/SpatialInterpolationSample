using ComputeSharp;

namespace SpatialInterpolation.Shaders
{
    [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct SpatialHeatMapShader(
        ReadWriteTexture2D<int> heatMap,
        ReadOnlyTexture1D<float4> colors,
        ReadOnlyTexture1D<float> offsets,
        ReadOnlyTexture2D<float> values,
        float4 contourColor,
        uint levels,
        float maximum,
        float minimum) : IComputeShader
    {
        private static float ToSRgb(float value)
        {
            if (!(value > 0.0f))
                return 0;
            else if (value <= 0.0031308f)
                return value * 12.92f;
            else if (value < 1.0)
                return 1.055f * Hlsl.Pow(value, 1.0f / 2.4f) - 0.055f;
            else
                return 1.0f;
        }

        private static float4 ToSRgba(float4 color)
            => new(ToSRgb(color.R), ToSRgb(color.G), ToSRgb(color.B), color.A);

        private float ToOffset(int2 id)
            => Hlsl.IsNaN(values[id]) ? 0.0f : Hlsl.Clamp((values[id] - minimum) / (maximum - minimum), 0, 1);

        private float4 ToColor(float offset)
        {
            var ranges = colors.Width - 1;

            int index;
            for (index = 0; index < ranges; index++)
                if (offset < offsets[index])
                    break;

            if (index == 0 || offsets[index] <= offset) return colors[index];

            var range = offsets[index] - offsets[index - 1];
            var alpha = (offset - offsets[index - 1]) / range;
            var colorA = colors[index];
            var colorB = colors[index - 1];

            return Hlsl.Lerp(colorB, colorA, alpha);
        }

        private float Posterize(int x, int y)
            => Hlsl.Floor(ToOffset(new int2(x, y)) * levels) / levels;

        private float GetLineOpacity(int currX, int currY)
        {
            if (levels < 2) return 0;

            var prevX = currX + (currX <= 0 ? 0 : -1);
            var prevY = currY + (currY <= 0 ? 0 : -1);
            var nextX = currX + (currX >= values.Width - 1 ? 0 : 1);
            var nextY = currY + (currY >= values.Height - 1 ? 0 : 1);

            var gX = Posterize(prevX, prevY) - Posterize(nextX, prevY)
                + (Posterize(prevX, currY) - Posterize(nextX, currY)) * 2
                + Posterize(prevX, nextY) - Posterize(nextX, nextY);

            var gY = Posterize(prevX, prevY) - Posterize(prevX, nextY)
                + (Posterize(currX, prevY) - Posterize(currX, nextY)) * 2
                + Posterize(nextX, prevY) - Posterize(nextX, nextY);

            gX *= levels / 4f;
            gY *= levels / 4f;

            return Hlsl.Sqrt(gX * gX + gY * gY);
        }

        public void Execute()
        {
            var color = ToColor(ToOffset(ThreadIds.XY));
            var lineOpacity = GetLineOpacity(ThreadIds.X, ThreadIds.Y) * contourColor.A;
            var result = Hlsl.Lerp(color, new float4(contourColor.RGB, 1), lineOpacity);

            result = ToSRgba(result);
            heatMap[ThreadIds.XY] = ((int)(result.W * 255f) << 24)
                | ((int)(result.X * 255f) << 16)
                | ((int)(result.Y * 255f) << 8)
                | ((int)(result.Z * 255f));
        }
    }
}
