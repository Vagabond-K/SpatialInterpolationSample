using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace SpatialInterpolation
{
    public class ColorMap : FrameworkElement
    {
        static ColorMap()
        {
            DataSourceProperty = RegisterProperty(nameof(DataSource), typeof(float[,]), null, FrameworkPropertyMetadataOptions.AffectsRender, OnPropertyChanged);
            MaximumProperty = RegisterProperty(nameof(Maximum), typeof(float), 100f, FrameworkPropertyMetadataOptions.AffectsRender, OnPropertyChanged);
            MinimumProperty = RegisterProperty(nameof(Minimum), typeof(float), 0f, FrameworkPropertyMetadataOptions.AffectsRender, OnPropertyChanged);
            ContourLevelsProperty = RegisterProperty(nameof(ContourLevels), typeof(int), 10, FrameworkPropertyMetadataOptions.AffectsRender, OnPropertyChanged);
            ColorsProperty = RegisterProperty(nameof(Colors), typeof(IReadOnlyCollection<Color>), null, FrameworkPropertyMetadataOptions.None, OnColorsChanged);
            LegendGradientStopsProperty = RegisterProperty(nameof(LegendGradientStops), typeof(GradientStopCollection), null);
        }

        private static DependencyProperty RegisterProperty(string name, Type type, object defaultValue, FrameworkPropertyMetadataOptions flags = FrameworkPropertyMetadataOptions.None, PropertyChangedCallback propertyChangedCallback = null)
            => DependencyProperty.Register(name, type, typeof(ColorMap), new FrameworkPropertyMetadata(defaultValue, flags, propertyChangedCallback));

        public static readonly DependencyProperty DataSourceProperty;
        public static readonly DependencyProperty MaximumProperty;
        public static readonly DependencyProperty MinimumProperty;
        public static readonly DependencyProperty ContourLevelsProperty;
        public static readonly DependencyProperty ColorsProperty;
        public static readonly DependencyProperty LegendGradientStopsProperty;

        public ColorMap()
        {
            Unloaded += OnUnloaded;
        }

        public float[,] DataSource { get => (float[,])GetValue(DataSourceProperty); set => SetValue(DataSourceProperty, value); }
        public float Maximum { get => (float)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public float Minimum { get => (float)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public int ContourLevels { get => (int)GetValue(ContourLevelsProperty); set => SetValue(ContourLevelsProperty, value); }
        public IReadOnlyCollection<Color> Colors { get => (IReadOnlyCollection<Color>)GetValue(ColorsProperty); set => SetValue(ColorsProperty, value); }
        public GradientStopCollection LegendGradientStops { get => (GradientStopCollection)GetValue(LegendGradientStopsProperty); set => SetValue(LegendGradientStopsProperty, value); }

        private static void OnColorsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is ColorMap colorMap)
            {
                colorMap.LegendGradientStops = new GradientStopCollection(colorMap.Colors.Select((color, i) => new GradientStop(color, 1d - (double)i / (colorMap.Colors.Count - 1))));
                colorMap.UpdateBitmap();
            }
        }

        private static void OnPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
            => (dependencyObject as ColorMap)?.UpdateBitmap();

        private readonly object lockObject = new object();
        private ShaderBytecode colorMapCode = ShaderUtilities.LoadShaderByteCode(new Uri($"pack://application:,,,/{typeof(ColorMap).Assembly.GetName().Name};component/Shaders/{nameof(ColorMap)}.hlsl"));
        private WriteableBitmap bitmap;
        private Device device;
        private ComputeShader shader;
        private Buffer parametersBuffer;
        private ShaderResourceView colorsView;
        private ShaderResourceView valuesView;
        private UnorderedAccessView colorMapView;
        private Texture2D resultsTexture;

        private void DisposeColorsResources()
        {
            lock (lockObject)
            {
                var resource = colorsView?.Resource;
                Utilities.Dispose(ref colorsView);
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

                resource = colorMapView?.Resource;
                Utilities.Dispose(ref colorMapView);
                Utilities.Dispose(ref resource);

                Utilities.Dispose(ref resultsTexture);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DisposeColorsResources();
            DisposeSizeResources();
            Utilities.Dispose(ref parametersBuffer);
            Utilities.Dispose(ref colorMapCode);
            Utilities.Dispose(ref shader);
            Utilities.Dispose(ref device);
        }

        private void UpdateBitmap()
        {
            lock (lockObject)
            {
                var dataSource = DataSource;
                var colors = Colors?.Select(color => (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B)?.ToArray();

                if (dataSource == null || colors == null)
                {
                    bitmap = null;
                    return;
                }

                var parameters = new ColorMapParameters(System.Windows.Media.Colors.White, (uint)ContourLevels, Maximum, Minimum);
                var width = dataSource.GetLength(1);
                var height = dataSource.GetLength(0);

                if (device?.IsDisposed != false)
                {
                    DisposeSizeResources();
                    DisposeColorsResources();
                    Utilities.Dispose(ref shader);
                    Utilities.Dispose(ref device);
                    device = new Device(DriverType.Hardware, DeviceCreationFlags.Debug);
                }

                if (shader?.IsDisposed != false)
                {
                    shader = new ComputeShader(device, colorMapCode);
                    device.ImmediateContext.ComputeShader.Set(shader);
                }

                if (parametersBuffer?.IsDisposed != false)
                    parametersBuffer = new Buffer(device, new BufferDescription((int)Math.Ceiling(Marshal.SizeOf<ColorMapParameters>() / 16d) * 16, BindFlags.ConstantBuffer, ResourceUsage.Default));

                if (!(colorsView?.Resource is Texture1D tex) || tex.Description.Width != colors.Length)
                    DisposeColorsResources();

                if (bitmap == null || bitmap.PixelWidth != width || bitmap.PixelHeight != height)
                {
                    DisposeSizeResources();
                    bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                }

                if (colorsView?.Resource?.IsDisposed != false || colorsView?.IsDisposed != false)
                    colorsView = new ShaderResourceView(device, device.CreateTexture1D(colors.Length, SharpDX.DXGI.Format.B8G8R8A8_UNorm));
                if (valuesView?.Resource?.IsDisposed != false || valuesView?.IsDisposed != false)
                    valuesView = new ShaderResourceView(device, device.CreateTexture2D(width, height, SharpDX.DXGI.Format.R32_Float));
                if (colorMapView?.Resource?.IsDisposed != false || colorMapView?.IsDisposed != false)
                    colorMapView = new UnorderedAccessView(device, device.CreateTexture2D(width, height, SharpDX.DXGI.Format.B8G8R8A8_UNorm, BindFlags.UnorderedAccess));
                if (resultsTexture?.IsDisposed != false)
                    resultsTexture = device.CreateTexture2D(width, height, SharpDX.DXGI.Format.B8G8R8A8_UNorm, BindFlags.None, ResourceUsage.Staging, CpuAccessFlags.Read);

                var context = device.ImmediateContext;

                unsafe
                {
                    fixed (float* dataPointer = dataSource)
                    {
                        context.UpdateSubresource(valuesView.Resource, 0, null, new IntPtr(dataPointer), width * sizeof(float), 0);
                    }
                }

                context.UpdateSubresource(ref parameters, parametersBuffer);
                context.UpdateSubresource(colors, colorsView.Resource);
                context.ComputeShader.SetConstantBuffer(0, parametersBuffer);
                context.ComputeShader.SetShaderResource(0, valuesView);
                context.ComputeShader.SetShaderResource(1, colorsView);
                context.ComputeShader.SetUnorderedAccessView(0, colorMapView);

                context.Dispatch(Math.Max(1, (width + 31) / 32), Math.Max(1, (height + 31) / 32), 1);
                context.CopyResource(colorMapView.Resource, resultsTexture);

                var dataBox = context.MapSubresource(resultsTexture, 0, MapMode.Read, MapFlags.None);

                var source = dataBox.DataPointer;
                var dest = bitmap.BackBuffer;
                var destStride = bitmap.BackBufferStride;
                bitmap.Lock();
                for (int i = 0; i < height; i++)
                {
                    Utilities.CopyMemory(dest, source, destStride);
                    source = IntPtr.Add(source, dataBox.RowPitch);
                    dest = IntPtr.Add(dest, destStride);
                }
                context.UnmapSubresource(resultsTexture, 0);
                bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                bitmap.Unlock();
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (bitmap == null)
                UpdateBitmap();
            if (bitmap != null)
            {
                drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
                drawingContext.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            }
        }

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

        readonly struct ColorMapParameters
        {
            public ColorMapParameters(Color contourColor, in uint contourLevels, in float maximum, in float minimum)
            {
                Maximum = maximum;
                Minimum = minimum;
                ContourLevels = contourLevels;
                ContourColor = new Color4(
                    contourColor.R / 255f,
                    contourColor.G / 255f,
                    contourColor.B / 255f,
                    contourColor.A / 255f);
            }

            public Color4 ContourColor { get; }
            public uint ContourLevels { get; }
            public float Maximum { get; }
            public float Minimum { get; }
        }
    }
}
