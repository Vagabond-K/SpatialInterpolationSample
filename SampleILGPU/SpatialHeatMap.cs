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
        private MemoryBuffer2D<int, Stride2D.DenseX> resultsBuffer;
        private MemoryBuffer2D<float, Stride2D.DenseX> valuesBuffer;
        private MemoryBuffer1D<Vector4, Stride1D.Dense> colorsBuffer;
        private Action<Index2D, SpatialHeatMapKernel> kernel;

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            resultsBuffer?.Dispose();
            colorsBuffer?.Dispose();
            valuesBuffer?.Dispose();
            accelerator?.Dispose();
            context?.Dispose();
        }

        private void UpdateBitmap()
        {
            lock (lockObject)
            {

                var dataSource = DataSource;
                var colors = Colors?.Select(color => new Vector4(color.ScR, color.ScG, color.ScB, color.ScA)).ToArray();

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
                    valuesBuffer?.Dispose();
                    bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                }

                if (context?.IsDisposed != false)
                {
                    accelerator?.Dispose();
                    context = Context.Create(b => b.AllAccelerators().EnableAlgorithms());
                    device = context.Devices.FirstOrDefault(device => !(device is CPUDevice)) ?? context.Devices.FirstOrDefault();
                }

                if (accelerator?.IsDisposed != false)
                {
                    accelerator = device.CreateAccelerator(context);
                    kernel = accelerator.LoadAutoGroupedStreamKernel<Index2D, SpatialHeatMapKernel>(SpatialHeatMapKernel.Execute);
                }

                if (resultsBuffer?.IsDisposed != false)
                    resultsBuffer = accelerator.Allocate2DDenseX<int>(new Index2D(height, width));
                if (valuesBuffer?.IsDisposed != false)
                    valuesBuffer = accelerator.Allocate2DDenseX<float>(new Index2D(height, width));
                if (colorsBuffer?.IsDisposed != false || colorsBuffer.Length != colors.Length)
                {
                    colorsBuffer?.Dispose();
                    colorsBuffer = accelerator.Allocate1D<Vector4>(colors.Length);
                }

                valuesBuffer.CopyFromCPU(dataSource);
                colorsBuffer.CopyFromCPU(colors);

                var kernelData = new SpatialHeatMapKernel(resultsBuffer.View, colorsBuffer.View, valuesBuffer.View, new Vector4(1, 1, 1, 1), (uint)ContourLevels, Maximum, Minimum);
                kernel(new Index2D(height, width), kernelData);

                var results = resultsBuffer.GetAsArray2D();
                bitmap.WritePixels(new Int32Rect(0, 0, width, height), results, width * bitmap.Format.BitsPerPixel / 8, 0);
            }
        }
    }
}
