using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using SpatialInterpolation.Kernels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace SpatialInterpolation.Interpolations
{
    public class GpuIdwInterpolation : ISpatialInterpolation, IDisposable
    {
        public float SearchRadius { get; set; } = float.PositiveInfinity;
        public float WeightPower { get; set; } = 1;

        private readonly object lockObject = new();
        private Context context;
        private Device device;
        private Accelerator accelerator;
        private MemoryBuffer2D<float, Stride2D.DenseX> resultsBuffer;
        private MemoryBuffer1D<Vector3, Stride1D.Dense> samplesBuffer;
        private Action<Index2D, GpuIdwInterpolationKernel> kernel;
        private bool disposedValue;

        public Task Interpolate(IEnumerable<SpatialSample> samples, float[,] target, CancellationToken cancellationToken)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (target == null) throw new ArgumentNullException(nameof(target));
            var sampleArray = samples.Select(sample => new Vector3(sample.X, sample.Y, sample.Value)).ToArray();
            if (sampleArray.Length == 0) throw new ArgumentOutOfRangeException(nameof(samples));
            return Task.Run(() =>
            {
                lock (lockObject)
                {
                    int width = target.GetLength(1);
                    int height = target.GetLength(0);

                    if (context?.IsDisposed != false)
                    {
                        accelerator?.Dispose();
                        context = Context.Create(b => b.AllAccelerators().EnableAlgorithms());
                        device = context.Devices.FirstOrDefault(device => device is not CPUDevice) ?? context.Devices.FirstOrDefault();
                    }

                    if (accelerator?.IsDisposed != false)
                    {
                        accelerator = device.CreateAccelerator(context);
                        kernel = accelerator.LoadAutoGroupedStreamKernel<Index2D, GpuIdwInterpolationKernel>(GpuIdwInterpolationKernel.Execute);
                    }

                    if (resultsBuffer == null || resultsBuffer.Extent.X != height || resultsBuffer.Extent.Y != width)
                    {
                        resultsBuffer?.Dispose();
                        resultsBuffer = accelerator.Allocate2DDenseX<float>(new Index2D(height, width));
                    }
                    if (samplesBuffer == null || samplesBuffer.Length != sampleArray.Length)
                    {
                        samplesBuffer?.Dispose();
                        samplesBuffer = accelerator.Allocate1D<Vector3>(sampleArray.Length);
                    }

                    samplesBuffer.CopyFromCPU(sampleArray);

                    var kernelData = new GpuIdwInterpolationKernel(resultsBuffer.View, samplesBuffer.View, SearchRadius, WeightPower);
                    kernel(new Index2D(height, width), kernelData);

                    resultsBuffer.CopyToCPU(target);
                }
            }, cancellationToken);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    resultsBuffer?.Dispose();
                    samplesBuffer?.Dispose();
                    accelerator?.Dispose();
                    context?.Dispose();
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
