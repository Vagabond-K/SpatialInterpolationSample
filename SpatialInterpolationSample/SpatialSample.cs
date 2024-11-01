namespace SpatialInterpolation
{
    public readonly struct SpatialSample
    {
        public SpatialSample(in float x, in float y, in float value)
        {
            X = x;
            Y = y;
            Value = value;
        }

        public float X { get; }
        public float Y { get; }
        public float Value { get; }
    }
}
