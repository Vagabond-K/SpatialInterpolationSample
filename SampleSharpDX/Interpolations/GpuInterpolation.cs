using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace SpatialInterpolation.Interpolations
{
    public abstract class GpuInterpolation : ISpatialInterpolation, IDisposable
    {
        protected GpuInterpolation()
        {
            shaderByteCode = CreateShaderBytecode();
        }

        private readonly object lockObject = new object();
        private bool disposedValue;

        private ShaderBytecode shaderByteCode;
        private Device device;
        private ComputeShader shader;
        private ShaderResourceView samplesView;
        private UnorderedAccessView interpolationView;
        private Texture2D resultsTexture;

        private int lastSampleCount;
        private int lastWidth;
        private int lastHeight;

        protected abstract ShaderBytecode CreateShaderBytecode();
        protected virtual void Configure(Device device, IEnumerable<SpatialSample> samples, float[,] target) { }

        private void DisposeSamplesResources()
        {
            lock (lockObject)
            {
                var resource = samplesView?.Resource;
                Utilities.Dispose(ref samplesView);
                Utilities.Dispose(ref resource);
            }
        }

        private void DisposeSizeResources()
        {
            lock (lockObject)
            {
                var resource = interpolationView?.Resource;
                Utilities.Dispose(ref interpolationView);
                Utilities.Dispose(ref resource);

                Utilities.Dispose(ref resultsTexture);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    DisposeSamplesResources();
                    DisposeSizeResources();
                    Utilities.Dispose(ref shaderByteCode);
                    Utilities.Dispose(ref shader);
                    Utilities.Dispose(ref device);
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public Task Interpolate(IEnumerable<SpatialSample> samples, float[,] target, CancellationToken cancellationToken)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (target == null) throw new ArgumentNullException(nameof(target));
            var sampleArray = samples.ToArray();
            if (sampleArray.Length == 0) throw new ArgumentOutOfRangeException(nameof(samples));
            return Task.Run(() =>
            {
                lock (lockObject)
                {
                    if (device?.IsDisposed != false)
                    {
                        DisposeSizeResources();
                        DisposeSamplesResources();
                        Utilities.Dispose(ref shader);
                        Utilities.Dispose(ref device);
                        device = new Device(DriverType.Hardware, DeviceCreationFlags.Debug);
                    }

                    if (shader?.IsDisposed != false)
                    {
                        shader = new ComputeShader(device, shaderByteCode);
                        device.ImmediateContext.ComputeShader.Set(shader);
                    }

                    int width = target.GetLength(1);
                    int height = target.GetLength(0);
                    var sampleSize = Marshal.SizeOf<SpatialSample>();

                    if (lastSampleCount != sampleArray.Length)
                    {
                        lastSampleCount = sampleArray.Length;
                        DisposeSamplesResources();
                    }

                    if (lastWidth != width || lastHeight != height)
                    {
                        lastWidth = width;
                        lastHeight = height;
                        DisposeSizeResources();
                    }

                    if (samplesView?.Resource?.IsDisposed != false || samplesView?.IsDisposed != false)
                        samplesView = new ShaderResourceView(device, new Buffer(device,
                            new BufferDescription(sampleArray.Length * sampleSize, 
                            ResourceUsage.Default, BindFlags.ShaderResource, CpuAccessFlags.None, ResourceOptionFlags.BufferStructured, sampleSize)),
                            new ShaderResourceViewDescription
                            {
                                Format = SharpDX.DXGI.Format.Unknown,
                                Dimension = ShaderResourceViewDimension.Buffer,
                                Buffer = new ShaderResourceViewDescription.BufferResource()
                                {
                                    ElementWidth = sampleArray.Length,
                                }
                            });
                    if (interpolationView?.Resource?.IsDisposed != false || interpolationView?.IsDisposed != false)
                        interpolationView = new UnorderedAccessView(device, device.CreateTexture2D(width, height, SharpDX.DXGI.Format.R32_Float, BindFlags.UnorderedAccess));
                    if (resultsTexture?.IsDisposed != false)
                        resultsTexture = device.CreateTexture2D(width, height, SharpDX.DXGI.Format.R32_Float, BindFlags.None, ResourceUsage.Staging, CpuAccessFlags.Read);

                    var context = device.ImmediateContext;

                    context.ComputeShader.Set(shader);
                    context.UpdateSubresource(sampleArray, samplesView.Resource);
                    context.ComputeShader.SetShaderResource(0, samplesView);
                    context.ComputeShader.SetUnorderedAccessView(0, interpolationView);

                    Configure(device, samples, target);

                    context.Dispatch(Math.Max(1, (width + 31) / 32), Math.Max(1, (height + 31) / 32), 1);
                    context.CopyResource(interpolationView.Resource, resultsTexture);

                    var dataBox = context.MapSubresource(resultsTexture, 0, MapMode.Read, MapFlags.None);
                    var source = dataBox.DataPointer;

                    unsafe
                    {
                        fixed (float* dataPointer = target)
                        {
                            var dest = new IntPtr(dataPointer);
                            var destStride = width * sizeof(float);
                            for (int i = 0; i < height; i++)
                            {
                                Utilities.CopyMemory(dest, source, destStride);
                                source = IntPtr.Add(source, dataBox.RowPitch);
                                dest = IntPtr.Add(dest, destStride);
                            }
                        }
                    }

                    context.UnmapSubresource(resultsTexture, 0);
                }
            }, cancellationToken);
        }

    }
}
