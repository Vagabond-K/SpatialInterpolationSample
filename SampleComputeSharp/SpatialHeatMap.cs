using ComputeSharp;
using SpatialInterpolation.Shaders;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpatialInterpolation
{
    public partial class SpatialHeatMap
    {
        public SpatialHeatMap()
        {
            Unloaded += OnUnloaded;
        }

        private readonly object lockObject = new();
        private ReadWriteTexture2D<int> heatMapBuffer;
        private ReadOnlyTexture1D<float4> colorsBuffer;
        private ReadOnlyTexture1D<float> offsetsBuffer;
        private ReadOnlyTexture2D<float> valuesBuffer;

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            heatMapBuffer?.Dispose();
            colorsBuffer?.Dispose();
            offsetsBuffer?.Dispose();
            valuesBuffer?.Dispose();
        }

        private void UpdateBitmapInGPU()
        {
            lock (lockObject)
            {
                var values = DataSource;
                var colors = GradientStops?.OrderBy(stop => stop.Offset)?.Select(stop => new float4(stop.Color.ScR, stop.Color.ScG, stop.Color.ScB, stop.Color.ScA))?.ToArray();
                var offsets = GradientStops?.OrderBy(stop => stop.Offset)?.Select(stop => (float)stop.Offset)?.ToArray();

                if (values == null || colors == null || colors.Length == 0)
                {
                    bitmap = null;
                    return;
                }

                var width = values.GetLength(1);
                var height = values.GetLength(0);

                if (bitmap == null || bitmap.PixelWidth != width || bitmap.PixelHeight != height)
                {
                    heatMapBuffer?.Dispose();
                    heatMapBuffer = null;
                    valuesBuffer?.Dispose();
                    valuesBuffer = null;
                    bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                }

                var device = GraphicsDevice.GetDefault();

                heatMapBuffer ??= device.AllocateReadWriteTexture2D<int>(width, height);
                valuesBuffer ??= device.AllocateReadOnlyTexture2D<float>(width, height);
                if (colorsBuffer == null || colorsBuffer.Width != colors.Length)
                {
                    colorsBuffer?.Dispose();
                    colorsBuffer = device.AllocateReadOnlyTexture1D<float4>(colors.Length);
                }
                if (offsetsBuffer == null || offsetsBuffer.Width != colors.Length)
                {
                    offsetsBuffer?.Dispose();
                    offsetsBuffer = device.AllocateReadOnlyTexture1D<float>(colors.Length);
                }
                valuesBuffer.CopyFrom(values);
                colorsBuffer.CopyFrom(colors);
                offsetsBuffer.CopyFrom(offsets);

                device.For(width, height, new SpatialHeatMapShader(
                    heatMapBuffer,
                    colorsBuffer,
                    offsetsBuffer,
                    valuesBuffer,
                    new float4(1, 1, 1, 1),
                    (uint)ContourLevels,
                    Maximum,
                    Minimum));

                unsafe
                {
                    bitmap.Lock();
                    Bgra32* pointer = (Bgra32*)bitmap.BackBuffer;
                    Span<int> results = new(pointer, width * height);
                    heatMapBuffer.CopyTo(results);
                    bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                    bitmap.Unlock();
                }
            }
        }
    }
}
