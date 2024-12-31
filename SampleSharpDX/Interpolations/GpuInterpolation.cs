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
        private UnorderedAccessView valuesView;
        private Texture2D stagingTexture;

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
                var resource = valuesView?.Resource;
                Utilities.Dispose(ref valuesView);
                Utilities.Dispose(ref resource);

                Utilities.Dispose(ref stagingTexture);
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

        public Task Interpolate(IEnumerable<SpatialSample> samples, float[,] values, CancellationToken cancellationToken)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (values == null) throw new ArgumentNullException(nameof(values));
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
                        device = new Device(DriverType.Hardware, DeviceCreationFlags.None);
                    }

                    var context = device.ImmediateContext;

                    if (shader?.IsDisposed != false)
                    {
                        shader = new ComputeShader(device, shaderByteCode);
                        context.ComputeShader.Set(shader);
                    }

                    int width = values.GetLength(1);
                    int height = values.GetLength(0);
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
                    {
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
                        context.ComputeShader.SetShaderResource(0, samplesView);
                    }
                    if (valuesView?.Resource?.IsDisposed != false || valuesView?.IsDisposed != false)
                    {
                        valuesView = new UnorderedAccessView(device, device.CreateTexture2D(width, height, SharpDX.DXGI.Format.R32_Float, BindFlags.UnorderedAccess));
                        context.ComputeShader.SetUnorderedAccessView(0, valuesView);
                    }
                    if (stagingTexture?.IsDisposed != false)
                        stagingTexture = device.CreateTexture2D(width, height, SharpDX.DXGI.Format.R32_Float, BindFlags.None, ResourceUsage.Staging, CpuAccessFlags.Read);

                    context.UpdateSubresource(sampleArray, samplesView.Resource);

                    Configure(device, samples, values);

                    context.Dispatch(Math.Max(1, (width + 31) / 32), Math.Max(1, (height + 31) / 32), 1);
                    context.CopyResource(valuesView.Resource, stagingTexture);

                    var dataBox = context.MapSubresource(stagingTexture, 0, MapMode.Read, MapFlags.None);
                    var sourcePtr = dataBox.DataPointer;

                    unsafe
                    {
                        fixed (float* startPtr = values)
                        {
                            //Utilities.CopyMemory(new IntPtr(startPtr), sourcePtr, width * height * sizeof(float));
                            //dataBox.RowPitch와 width * sizeof(float)가 일치하지 않을 수 있으므로 위의 코드는 사용할 수 없음
                            //대신 아래의 코드를 사용해야 함

                            var valuesPtr = new IntPtr(startPtr);
                            var rowPitch = width * sizeof(float);
                            for (int i = 0; i < height; i++)
                            {
                                Utilities.CopyMemory(valuesPtr, sourcePtr, rowPitch);
                                sourcePtr = IntPtr.Add(sourcePtr, dataBox.RowPitch);
                                valuesPtr = IntPtr.Add(valuesPtr, rowPitch);
                            }
                        }
                    }

                    context.UnmapSubresource(stagingTexture, 0);
                }
            }, cancellationToken);
        }

    }
}
