﻿using ComputeSharp;

namespace SpatialInterpolation.Shaders
{
    [ThreadGroupSize(DefaultThreadGroupSizes.XY)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct SpatialHeatMapShader(
        ReadWriteTexture2D<int> results,
        ReadOnlyTexture1D<float4> colors,
        ReadOnlyTexture1D<float> colorStops,
        ReadOnlyTexture2D<float> values,
        float4 contourColor,
        uint contourLevels,
        float maximum,
        float minimum) : IComputeShader
    {
        private static float ToSRgb(float value)
        {
            if (!(value > 0.0f))
                return 0;
            else if (value <= 0.0031308f)
                return (value * 12.92f);
            else if (value < 1.0)
                return 1.055f * Hlsl.Pow(value, 1.0f / 2.4f) - 0.055f;
            else
                return 1.0f;
        }

        private static float4 ToSRgba(float4 color)
            => new(ToSRgb(color.R), ToSRgb(color.G), ToSRgb(color.B), color.A);

        private float ToRatio(int2 id)
            => Hlsl.IsNaN(values[id]) ? 0.0f : Hlsl.Clamp((values[id] - minimum) / (maximum - minimum), 0, 1);

        private float GetCtLevel(int x, int y, float contour)
        {
            float ratio = Hlsl.Round(ToRatio(new int2(x, y)) * contourLevels) / contourLevels;
            return ratio > contour ? 0.7f : (ratio < contour ? 0.3f : 0.5f);
        }

        public void Execute()
        {
            int colorsCount = colors.Width;
            float4 result = new(0, 0, 0, 0);

            float ratio = ToRatio(ThreadIds.XY);

            if (colorsCount == 1)
            {
                result = colors[0];
            }
            else if (colorsCount > 1)
            {
                colorsCount--;

                int index;
                for (index = 0; index < colorsCount; index++)
                {
                    if (ratio < colorStops[index])
                        break;
                }
                if (index == 0)
                    result = colors[0];
                else
                {
                    float levelUnit = colorStops[index] - colorStops[index - 1];
                    float lerpSegment = (ratio - colorStops[index - 1]) / levelUnit;
                    result = Hlsl.Lerp(colors[index - 1], colors[index], lerpSegment);
                }
            }

            if (contourLevels > 0)
            {

                int width = values.Width;
                int height = values.Height;

                int xPrev = ThreadIds.X + (ThreadIds.X <= 0 ? 0 : -1);
                int xNext = ThreadIds.X + (ThreadIds.X >= width - 1 ? 0 : 1);
                int yPrev = ThreadIds.Y + (ThreadIds.Y <= 0 ? 0 : -1);
                int yNext = ThreadIds.Y + (ThreadIds.Y >= height - 1 ? 0 : 1);

                float contour = Hlsl.Round(ratio * contourLevels) / contourLevels;

                float h = -GetCtLevel(xPrev, yPrev, contour) + GetCtLevel(xNext, yPrev, contour)
                - GetCtLevel(xPrev, ThreadIds.Y, contour) * 2 + GetCtLevel(xNext, ThreadIds.Y, contour) * 2
                - GetCtLevel(xPrev, yNext, contour) + GetCtLevel(xNext, yNext, contour);

                float v = -GetCtLevel(xPrev, yPrev, contour) - GetCtLevel(ThreadIds.X, yPrev, contour) * 2 - GetCtLevel(xNext, yPrev, contour)
                + GetCtLevel(xPrev, yNext, contour) + GetCtLevel(ThreadIds.X, yNext, contour) * 2 + GetCtLevel(xNext, yNext, contour);

                float edgeRatio = Hlsl.Sqrt(h * h + v * v) * contourColor.A;
                if (edgeRatio > 0)
                    result = Hlsl.Lerp(result, new float4(contourColor.RGB, 1), edgeRatio);
            }

            result = ToSRgba(result);

            results[ThreadIds.XY] = ((int)(result.W * 255f) << 24)
                | ((int)(result.X * 255f) << 16)
                | ((int)(result.Y * 255f) << 8)
                | ((int)(result.Z * 255f));
        }
    }
}
