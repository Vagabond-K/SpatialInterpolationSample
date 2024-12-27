using System;

namespace SpatialInterpolation
{
    static class MathF
    {
        public static float Sqrt(in float f) => (float)Math.Sqrt(f);
        public static float Pow(in float x, in float y) => (float)Math.Pow(x, y);
        public static float Min(in float x, in float y) => (float)Math.Min(x, y);
        public static float Max(in float x, in float y) => (float)Math.Max(x, y);
    }
}
