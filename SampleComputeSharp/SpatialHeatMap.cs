﻿using ComputeSharp;
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
        private ReadWriteTexture2D<Bgra32, float4> resultsBuffer;
        private ReadOnlyTexture1D<Bgra32, float4> colorsBuffer;
        private ReadOnlyTexture1D<float> colorStopsBuffer;
        private ReadOnlyTexture2D<float> valuesBuffer;

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            resultsBuffer?.Dispose();
            colorsBuffer?.Dispose();
            colorStopsBuffer?.Dispose();
            valuesBuffer?.Dispose();
        }

        private void UpdateBitmap()
        {
            lock (lockObject)
            {
                var dataSource = DataSource;
                var colors = GradientStops?.OrderBy(stop => stop.Offset).Select(stop => new Bgra32(stop.Color.R, stop.Color.G, stop.Color.B, stop.Color.A))?.ToArray();
                var colorStops = GradientStops?.OrderBy(stop => stop.Offset).Select(stop => (float)stop.Offset)?.ToArray();

                if (dataSource == null || colors == null)
                {
                    bitmap = null;
                    return;
                }

                var width = dataSource.GetLength(1);
                var height = dataSource.GetLength(0);

                if (bitmap == null || bitmap.PixelWidth != width || bitmap.PixelHeight != height)
                {
                    resultsBuffer?.Dispose();
                    resultsBuffer = null;
                    valuesBuffer?.Dispose();
                    valuesBuffer = null;
                    bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                }

                var device = GraphicsDevice.GetDefault();

                if (resultsBuffer == null)
                    resultsBuffer = device.AllocateReadWriteTexture2D<Bgra32, float4>(width, height);
                if (valuesBuffer == null)
                    valuesBuffer = device.AllocateReadOnlyTexture2D<float>(width, height);
                if (colorsBuffer == null || colorsBuffer.Width != colors.Length)
                    colorsBuffer = device.AllocateReadOnlyTexture1D<Bgra32, float4>(colors.Length);
                if (colorStopsBuffer == null || colorStopsBuffer.Width != colors.Length)
                    colorStopsBuffer = device.AllocateReadOnlyTexture1D<float>(colors.Length);

                valuesBuffer.CopyFrom(dataSource);
                colorsBuffer.CopyFrom(colors);
                colorStopsBuffer.CopyFrom(colorStops);

                device.For(width, height, new SpatialHeatMapShader(resultsBuffer, colorsBuffer, colorStopsBuffer, valuesBuffer, new float4(1, 1, 1, 1), (uint)ContourLevels, Maximum, Minimum));

                unsafe
                {
                    bitmap.Lock();
                    Bgra32* pointer = (Bgra32*)bitmap.BackBuffer;
                    Span<Bgra32> results = new(pointer, width * height);
                    resultsBuffer.CopyTo(results);
                    bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                    bitmap.Unlock();
                }
            }
        }
    }
}
