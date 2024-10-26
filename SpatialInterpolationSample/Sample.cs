using SpatialInterpolationSample.SpatialInterpolation;
using System;

namespace SpatialInterpolationSample
{
    class Sample : NotifyPropertyChangeObject
    {
        public Sample(float x, float y, float value)
        {
            X = x;
            Y = y;
            Value = value;
        }

        public float X { get => Get(0f); set => Set(value); }
        public float Y { get => Get(0f); set => Set(value); }
        public float Value { get => Get(0f); set => Set(value); }

        public SpatialSample ToSpatialSample() => new SpatialSample(X, Y, Value);
    }
}
