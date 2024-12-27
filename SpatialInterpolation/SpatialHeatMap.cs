using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpatialInterpolation
{
    public partial class SpatialHeatMap : FrameworkElement
    {
        static SpatialHeatMap()
        {
            DataSourceProperty = RegisterProperty(nameof(DataSource), typeof(float[,]), null, FrameworkPropertyMetadataOptions.AffectsRender, OnPropertyChanged);
            MaximumProperty = RegisterProperty(nameof(Maximum), typeof(float), 100f, FrameworkPropertyMetadataOptions.AffectsRender, OnPropertyChanged);
            MinimumProperty = RegisterProperty(nameof(Minimum), typeof(float), 0f, FrameworkPropertyMetadataOptions.AffectsRender, OnPropertyChanged);
            ContourLevelsProperty = RegisterProperty(nameof(ContourLevels), typeof(int), 10, FrameworkPropertyMetadataOptions.AffectsRender, OnPropertyChanged);
            GradientStopsProperty = RegisterProperty(nameof(GradientStops), typeof(GradientStopCollection), null, FrameworkPropertyMetadataOptions.AffectsRender, OnPropertyChanged);
            UseGPUProperty = RegisterProperty(nameof(UseGPU), typeof(bool), true);
        }

        private static DependencyProperty RegisterProperty(string name, Type type, object defaultValue, FrameworkPropertyMetadataOptions flags = FrameworkPropertyMetadataOptions.None, PropertyChangedCallback propertyChangedCallback = null)
            => DependencyProperty.Register(name, type, typeof(SpatialHeatMap), new FrameworkPropertyMetadata(defaultValue, flags, propertyChangedCallback));

        public static readonly DependencyProperty DataSourceProperty;
        public static readonly DependencyProperty MaximumProperty;
        public static readonly DependencyProperty MinimumProperty;
        public static readonly DependencyProperty ContourLevelsProperty;
        public static readonly DependencyProperty GradientStopsProperty;
        public static readonly DependencyProperty UseGPUProperty;

        public float[,] DataSource { get => (float[,])GetValue(DataSourceProperty); set => SetValue(DataSourceProperty, value); }
        public float Maximum { get => (float)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public float Minimum { get => (float)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public int ContourLevels { get => (int)GetValue(ContourLevelsProperty); set => SetValue(ContourLevelsProperty, value); }
        public GradientStopCollection GradientStops { get => (GradientStopCollection)GetValue(GradientStopsProperty); set => SetValue(GradientStopsProperty, value); }
        public bool UseGPU { get => (bool)GetValue(UseGPUProperty); set => SetValue(UseGPUProperty, value); }

        private static void OnPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
            => (dependencyObject as SpatialHeatMap)?.UpdateBitmap();

        private WriteableBitmap bitmap;

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

        private void UpdateBitmap()
        {
            if (UseGPU)
                UpdateBitmapInGPU();
            else
                UpdateBitmapInCPU();
        }

        private static float ToOffset(in float value, in float maximum, in float minimum)
            => float.IsNaN(value) ? 0.0f : MathF.Max(0f, MathF.Min(1f, (value - minimum) / (maximum - minimum)));

        private static Color ToColor(in float offset, Color[] colors, float[] offsets)
        {
            int ranges = colors.Length - 1;

            int index;
            for (index = 0; index < ranges; index++)
                if (offset < offsets[index])
                    break;

            if (index == 0 || offsets[index] <= offset) return colors[index];

            float range = offsets[index] - offsets[index - 1];
            float alpha = (offset - offsets[index - 1]) / range;
            var colorA = colors[index];
            var colorB = colors[index - 1];

            return colorA * alpha + colorB * (1 - alpha);    //Alpha compositing
        }

        private static float Posterize(in float offset, in int levels) => (float)Math.Floor(offset * levels) / levels;

        private static float GetLineOpacity(float[,] poster, in int currX, in int currY, in int levels)
        {
            int prevX = currX + (currX <= 0 ? 0 : -1);
            int prevY = currY + (currY <= 0 ? 0 : -1);
            int nextX = currX + (currX >= poster.GetLength(1) - 1 ? 0 : 1);
            int nextY = currY + (currY >= poster.GetLength(0) - 1 ? 0 : 1);

            var gX = poster[prevY, prevX] - poster[prevY, nextX]
                + (poster[currY, prevX] - poster[currY, nextX]) * 2
                + poster[nextY, prevX] - poster[nextY, nextX];

            var gY = poster[prevY, prevX] - poster[nextY, prevX]
                + (poster[prevY, currX] - poster[nextY, currX]) * 2
                + poster[prevY, nextX] - poster[nextY, nextX];

            gX *= levels / 4f;
            gY *= levels / 4f;

            return MathF.Sqrt(gX * gX + gY * gY);
        }

        private void UpdateBitmapInCPU()
        {
            var values = DataSource;
            var colors = GradientStops?.OrderBy(item => item.Offset)?.Select(item => item.Color)?.ToArray();
            var offsets = GradientStops?.OrderBy(item => item.Offset)?.Select(item => (float)item.Offset)?.ToArray();

            if (values == null || colors == null || colors.Length == 0)
            {
                bitmap = null;
                return;
            }

            var width = values.GetLength(1);
            var height = values.GetLength(0);
            var maximum = Maximum;
            var minimum = Minimum;
            var levels = ContourLevels;

            var posterized = new float[height, width];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    posterized[y, x] = Posterize(ToOffset(values[y, x], maximum, minimum), levels);

            if (bitmap == null || bitmap.PixelWidth != width || bitmap.PixelHeight != height)
                bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            bitmap.Lock();
            unsafe
            {
                int* pixel = (int*)bitmap.BackBuffer.ToPointer();
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        var color = ToColor(ToOffset(values[y, x], maximum, minimum), colors, offsets);
                        var lineOpacity = GetLineOpacity(posterized, x, y, levels);
                        var result = Colors.White * lineOpacity + color * (1 - lineOpacity);

                        *pixel++ = result.A << 24 | result.R << 16 | result.G << 8 | result.B;
                    }
            }
            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            bitmap.Unlock();
        }
    }
}
