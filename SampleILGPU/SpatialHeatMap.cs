using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using SpatialInterpolation.Kernels;
using System;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpatialInterpolation
{
    public partial class SpatialHeatMap
    {
        private readonly object lockObject = new();


        public SpatialHeatMap()
        {
            Unloaded += OnUnloaded;
        }

        private Context context;
        private Device device;
        private Accelerator accelerator;
        private MemoryBuffer2D<int, Stride2D.DenseX> heatMapBuffer;
        private MemoryBuffer2D<float, Stride2D.DenseX> valuesBuffer;
        private MemoryBuffer1D<Vector4, Stride1D.Dense> colorsBuffer;
        private MemoryBuffer1D<float, Stride1D.Dense> offsetsBuffer;
        private Action<Index2D, SpatialHeatMapKernel> kernel;

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            heatMapBuffer?.Dispose();
            offsetsBuffer?.Dispose();
            colorsBuffer?.Dispose();
            valuesBuffer?.Dispose();
            accelerator?.Dispose();
            context?.Dispose();
        }

        private void UpdateBitmapInGPU()
        {
            lock (lockObject)
            {
                var dataSource = DataSource;
                var colors = GradientStops?.OrderBy(stop => stop.Offset)?.Select(stop => new Vector4(stop.Color.ScR, stop.Color.ScG, stop.Color.ScB, stop.Color.ScA))?.ToArray();
                var colorStops = GradientStops?.OrderBy(stop => stop.Offset)?.Select(stop => (float)stop.Offset)?.ToArray();

                if (dataSource == null || colors == null || colors.Length == 0)
                {
                    bitmap = null;
                    return;
                }

                var width = dataSource.GetLength(1);
                var height = dataSource.GetLength(0);

                if (bitmap == null || bitmap.PixelWidth != width || bitmap.PixelHeight != height)
                {
                    heatMapBuffer?.Dispose();
                    valuesBuffer?.Dispose();
                    bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                }

                if (context?.IsDisposed != false)
                {
                    accelerator?.Dispose();
                    context = Context.Create(b => b.AllAccelerators().EnableAlgorithms());
                    device = context.Devices.FirstOrDefault(device => device is not CPUDevice) ?? context.Devices.FirstOrDefault();
                }

                if (accelerator?.IsDisposed != false)
                {
                    accelerator = device.CreateAccelerator(context);
                    kernel = accelerator.LoadAutoGroupedStreamKernel<Index2D, SpatialHeatMapKernel>(SpatialHeatMapKernel.Execute);
                }

                if (heatMapBuffer?.IsDisposed != false)
                    heatMapBuffer = accelerator.Allocate2DDenseX<int>(new Index2D(height, width));
                if (valuesBuffer?.IsDisposed != false)
                    valuesBuffer = accelerator.Allocate2DDenseX<float>(new Index2D(height, width));
                if (colorsBuffer?.IsDisposed != false || colorsBuffer.Length != colors.Length)
                {
                    colorsBuffer?.Dispose();
                    colorsBuffer = accelerator.Allocate1D<Vector4>(colors.Length);
                }
                if (offsetsBuffer?.IsDisposed != false || offsetsBuffer.Length != colorStops.Length)
                {
                    offsetsBuffer?.Dispose();
                    offsetsBuffer = accelerator.Allocate1D<float>(colors.Length);
                }

                valuesBuffer.CopyFromCPU(dataSource);
                colorsBuffer.CopyFromCPU(colors);
                offsetsBuffer.CopyFromCPU(colorStops);

                var kernelData = new SpatialHeatMapKernel(
                    heatMapBuffer.View,
                    colorsBuffer.View,
                    offsetsBuffer.View,
                    valuesBuffer.View,
                    new Vector4(1, 1, 1, 1),
                    (uint)ContourLevels,
                    Maximum,
                    Minimum);
                kernel(new Index2D(height, width), kernelData);

                var results = heatMapBuffer.GetAsArray2D();
                bitmap.WritePixels(new Int32Rect(0, 0, width, height), results, width * bitmap.Format.BitsPerPixel / 8, 0);
            }
        }
    }
}
