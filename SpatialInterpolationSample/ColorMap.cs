using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpatialInterpolationSample
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


        private WriteableBitmap bitmap;

        private static Color GetColor(in float ratio, Color[] colors)
        {
            if (colors == null || colors.Length == 0 || float.IsNaN(ratio))
                return System.Windows.Media.Colors.Transparent;
            if (colors.Length == 1)
                return colors[0];
            if (ratio == 1)
                return colors[colors.Length - 1];

            var levels = colors.Length - 1;
            var index = (int)Math.Floor(ratio * levels);
            var firstColor = colors[index];
            var secondColor = colors[index + 1];
            var gradient = ratio * levels - index;
            var reverse = 1 - gradient;

            var color = Color.FromScRgb(
                (float)(firstColor.ScA * reverse + secondColor.ScA * gradient),
                (float)(firstColor.ScR * reverse + secondColor.ScR * gradient),
                (float)(firstColor.ScG * reverse + secondColor.ScG * gradient),
                (float)(firstColor.ScB * reverse + secondColor.ScB * gradient));

            return color;
        }

        private void UpdateBitmap()
        {
            var dataSource = DataSource;
            var colors = Colors?.ToArray();

            if (dataSource == null || colors == null)
            {
                bitmap = null;
                return;
            }

            var width = dataSource.GetLength(1);
            var height = dataSource.GetLength(0);

            bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            var pixelBytes = bitmap.Format.BitsPerPixel / 8;
            var strideMargin = bitmap.BackBufferStride - width * pixelBytes;
            var maximum = Maximum;
            var minimum = Minimum;
            var range = maximum - minimum;
            var contourLevels = ContourLevels;

            var ratioMap = new float[height, width];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var value = dataSource[y, x];
                    ratioMap[y, x] = float.IsNaN(value) ? value : (Math.Max(minimum, Math.Min(maximum, value)) - minimum) / range;
                }
            }

            try
            {
                bitmap.Lock();
                unsafe
                {
                    IntPtr pBackBuffer = bitmap.BackBuffer;
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            var ratio = ratioMap[y, x];
                            var color = float.IsNaN(ratio)
                                ? System.Windows.Media.Colors.Transparent
                                : GetColor(ratio, colors);

                            int pixel = color.A << 24;
                            pixel |= color.R << 16;
                            pixel |= color.G << 8;
                            pixel |= color.B << 0;

                            //픽셀 값 지정
                            *(int*)pBackBuffer = pixel;

                            pBackBuffer += pixelBytes;

                            //경계값으로 변환
                            if (contourLevels > 0)
                                ratioMap[y, x] = (float)Math.Round(ratio * contourLevels) / contourLevels;
                        }
                        pBackBuffer += strideMargin;
                    }


                    if (contourLevels > 0)
                    {
                        //소벨 엣지 추출
                        var edgeLimitX = width - 1;
                        var edgeLimitY = height - 1;
                        var edgeMax = 1d / contourLevels * 4;
                        pBackBuffer = bitmap.BackBuffer + strideMargin;
                        for (int y = 1; y < edgeLimitY; y++)
                        {
                            pBackBuffer += pixelBytes;
                            var yPrev = y - 1;
                            var yNext = y + 1;
                            for (int x = 1; x < edgeLimitX; x++)
                            {
                                var xPrev = x - 1;
                                var xNext = x + 1;

                                var h = -ratioMap[yPrev, xPrev] + ratioMap[yPrev, xNext]
                                    - ratioMap[y, xPrev] * 2 + ratioMap[y, xNext] * 2
                                    - ratioMap[yNext, xPrev] + ratioMap[yNext, xNext];

                                var v = -ratioMap[yPrev, xPrev] - ratioMap[yPrev, x] * 2 - ratioMap[yPrev, xNext]
                                    + ratioMap[yNext, xPrev] + ratioMap[yNext, x] * 2 + ratioMap[y + 1, xNext];

                                var edgeRatio = Math.Min(1, Math.Sqrt(h * h + v * v) / edgeMax);
                                if (edgeRatio > 0)
                                {
                                    var oldRatio = 1 - edgeRatio;
                                    edgeRatio *= 0xff;

                                    var pixel = *(int*)pBackBuffer;
                                    var edgeColor = (int)(((pixel >> 24) & 0xff) * oldRatio + edgeRatio) << 24;
                                    edgeColor |= (int)(((pixel >> 16) & 0xff) * oldRatio + edgeRatio) << 16;
                                    edgeColor |= (int)(((pixel >> 8) & 0xff) * oldRatio + edgeRatio) << 8;
                                    edgeColor |= (int)(((pixel >> 0) & 0xff) * oldRatio + edgeRatio) << 0;
                                    *(int*)pBackBuffer = edgeColor;
                                }
                                pBackBuffer += pixelBytes;
                            }
                            pBackBuffer += pixelBytes + strideMargin;
                        }
                    }
                }

                bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                bitmap.Unlock();
            }

            bitmap.Freeze();
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
    }
}
