using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace SpatialInterpolation
{
    public partial class SpatialHeatMap
    {
        readonly struct Color4
        {
            public Color4(float r, float g, float b, float a)
            {
                R = r;
                G = g;
                B = b;
                A = a;
            }

            public float R { get; }
            public float G { get; }
            public float B { get; }
            public float A { get; }
        }

        readonly struct HeatMapParameters
        {
            public HeatMapParameters(Color contourColor, in uint levels, in float maximum, in float minimum)
            {
                Maximum = maximum;
                Minimum = minimum;
                Levels = levels;
                ContourColor = new Color4(
                    contourColor.R / 255f,
                    contourColor.G / 255f,
                    contourColor.B / 255f,
                    contourColor.A / 255f);
            }

            public Color4 ContourColor { get; }
            public uint Levels { get; }
            public float Maximum { get; }
            public float Minimum { get; }
        }

        public SpatialHeatMap()
        {
            Unloaded += OnUnloaded;
        }

        private readonly object lockObject = new object();
        private ShaderBytecode heatMapCode;
        private Device device;
        private ComputeShader shader;
        private Buffer parametersBuffer;
        private ShaderResourceView colorsView;
        private ShaderResourceView offsetsView;
        private ShaderResourceView valuesView;
        private UnorderedAccessView heatMapView;
        private Texture2D heatMapTexture;

        private void DisposeColorsResources()
        {
            lock (lockObject)
            {
                var resource = colorsView?.Resource;
                Utilities.Dispose(ref colorsView);
                Utilities.Dispose(ref resource);

                resource = offsetsView?.Resource;
                Utilities.Dispose(ref offsetsView);
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

                resource = heatMapView?.Resource;
                Utilities.Dispose(ref heatMapView);
                Utilities.Dispose(ref resource);

                Utilities.Dispose(ref heatMapTexture);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DisposeColorsResources();
            DisposeSizeResources();
            Utilities.Dispose(ref parametersBuffer);
            Utilities.Dispose(ref heatMapCode);
            Utilities.Dispose(ref shader);
            Utilities.Dispose(ref device);
        }

        private void UpdateBitmapInGPU()
        {
            lock (lockObject)
            {
                var values = DataSource;
                var colors = GradientStops?.OrderBy(stop => stop.Offset)?.Select(stop => new Color4(stop.Color.ScR, stop.Color.ScG, stop.Color.ScB, stop.Color.ScA))?.ToArray();
                var offsets = GradientStops?.OrderBy(stop => stop.Offset)?.Select(stop => (float)stop.Offset)?.ToArray();

                if (values == null || colors == null || colors.Length == 0)
                {
                    bitmap = null;
                    return;
                }

                var parameters = new HeatMapParameters(Colors.White, (uint)ContourLevels, Maximum, Minimum);
                var width = values.GetLength(1);
                var height = values.GetLength(0);

                if (device?.IsDisposed != false)
                {
                    DisposeSizeResources();
                    DisposeColorsResources();
                    Utilities.Dispose(ref shader);
                    Utilities.Dispose(ref device);
                    device = new Device(DriverType.Hardware, DeviceCreationFlags.None);
                }

                var context = device.ImmediateContext;

                if (heatMapCode == null)
                    heatMapCode = ShaderUtilities.LoadShaderByteCode(new Uri($"pack://application:,,,/{typeof(SpatialHeatMap).Assembly.GetName().Name};component/Shaders/{nameof(SpatialHeatMap)}.hlsl"));

                if (shader?.IsDisposed != false)
                {
                    shader = new ComputeShader(device, heatMapCode);
                    context.ComputeShader.Set(shader);
                }

                if (!(colorsView?.Resource is Texture1D tex) || tex.Description.Width != colors.Length)
                    DisposeColorsResources();

                if (bitmap == null || bitmap.PixelWidth != width || bitmap.PixelHeight != height)
                {
                    DisposeSizeResources();
                    bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                }

                if (colorsView?.Resource?.IsDisposed != false || colorsView?.IsDisposed != false)
                {
                    colorsView = new ShaderResourceView(device, device.CreateTexture1D(colors.Length, SharpDX.DXGI.Format.R32G32B32A32_Float));
                    context.ComputeShader.SetShaderResource(0, colorsView);
                }
                if (offsetsView?.Resource?.IsDisposed != false || offsetsView?.IsDisposed != false)
                {
                    offsetsView = new ShaderResourceView(device, device.CreateTexture1D(colors.Length, SharpDX.DXGI.Format.R32_Float));
                    context.ComputeShader.SetShaderResource(1, offsetsView);
                }
                if (valuesView?.Resource?.IsDisposed != false || valuesView?.IsDisposed != false)
                {
                    valuesView = new ShaderResourceView(device, device.CreateTexture2D(width, height, SharpDX.DXGI.Format.R32_Float));
                    context.ComputeShader.SetShaderResource(2, valuesView);
                }
                if (heatMapView?.Resource?.IsDisposed != false || heatMapView?.IsDisposed != false)
                {
                    heatMapView = new UnorderedAccessView(device, device.CreateTexture2D(width, height, SharpDX.DXGI.Format.R32_SInt, BindFlags.UnorderedAccess));
                    context.ComputeShader.SetUnorderedAccessView(0, heatMapView);
                }
                if (parametersBuffer?.IsDisposed != false)
                {
                    parametersBuffer = new Buffer(device, new BufferDescription((int)Math.Ceiling(Marshal.SizeOf<HeatMapParameters>() / 16d) * 16, BindFlags.ConstantBuffer, ResourceUsage.Default));
                    context.ComputeShader.SetConstantBuffer(0, parametersBuffer);
                }
                if (heatMapTexture?.IsDisposed != false)
                    heatMapTexture = device.CreateTexture2D(width, height, SharpDX.DXGI.Format.R32_SInt, BindFlags.None, ResourceUsage.Staging, CpuAccessFlags.Read);

                unsafe
                {
                    fixed (float* valuesPtr = values)
                    {
                        context.UpdateSubresource(valuesView.Resource, 0, null, new IntPtr(valuesPtr), width * sizeof(float), 0);
                    }
                }

                context.UpdateSubresource(colors, colorsView.Resource);
                context.UpdateSubresource(offsets, offsetsView.Resource);
                context.UpdateSubresource(ref parameters, parametersBuffer);

                context.Dispatch(Math.Max(1, (width + 31) / 32), Math.Max(1, (height + 31) / 32), 1);
                context.CopyResource(heatMapView.Resource, heatMapTexture);

                var dataBox = context.MapSubresource(heatMapTexture, 0, MapMode.Read, MapFlags.None);

                var sourcePtr = dataBox.DataPointer;
                var bitmapPtr = bitmap.BackBuffer;
                var rowPitch = bitmap.BackBufferStride;
                bitmap.Lock();
                for (int i = 0; i < height; i++)
                {
                    Utilities.CopyMemory(bitmapPtr, sourcePtr, rowPitch);
                    sourcePtr = IntPtr.Add(sourcePtr, dataBox.RowPitch);
                    bitmapPtr = IntPtr.Add(bitmapPtr, rowPitch);
                }
                context.UnmapSubresource(heatMapTexture, 0);
                bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                bitmap.Unlock();
            }
        }
    }
}
