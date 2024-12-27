using ComputeSharp;
using SpatialInterpolation.Shaders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SpatialInterpolation.Interpolations
{
    public class GpuIdwInterpolation : ISpatialInterpolation, IDisposable
    {
        public float SearchRadius { get; set; } = float.PositiveInfinity;
        public float WeightPower { get; set; } = 1;

        private readonly object lockObject = new();
        private ReadWriteTexture2D<float> valuesBuffer;
        private ReadOnlyBuffer<float3> samplesBuffer;
        private bool disposedValue;

        public Task Interpolate(IEnumerable<SpatialSample> samples, float[,] values, CancellationToken cancellationToken)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (values == null) throw new ArgumentNullException(nameof(values));
            var sampleArray = samples.Select(sample => new float3(sample.X, sample.Y, sample.Value)).ToArray();
            if (sampleArray.Length == 0) throw new ArgumentOutOfRangeException(nameof(samples));
            return Task.Run(() =>
            {
                lock (lockObject)
                {
                    int width = values.GetLength(1);
                    int height = values.GetLength(0);

                    var device = GraphicsDevice.GetDefault();
                    if (valuesBuffer == null || valuesBuffer.Width != width || valuesBuffer.Height != height)
                    {
                        valuesBuffer?.Dispose();
                        valuesBuffer = device.AllocateReadWriteTexture2D<float>(width, height);
                    }
                    if (samplesBuffer == null || samplesBuffer.Length != sampleArray.Length)
                    {
                        samplesBuffer?.Dispose();
                        samplesBuffer = device.AllocateReadOnlyBuffer<float3>(sampleArray.Length);
                    }

                    samplesBuffer.CopyFrom(sampleArray);
                    device.For(width, height, new GpuIdwInterpolationShader(valuesBuffer, samplesBuffer, SearchRadius, WeightPower));
                    valuesBuffer.CopyTo(values);
                }
            }, cancellationToken);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    valuesBuffer?.Dispose();
                    samplesBuffer?.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
