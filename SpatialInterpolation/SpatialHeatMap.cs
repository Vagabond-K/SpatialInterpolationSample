using System;
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
        }

        private static DependencyProperty RegisterProperty(string name, Type type, object defaultValue, FrameworkPropertyMetadataOptions flags = FrameworkPropertyMetadataOptions.None, PropertyChangedCallback propertyChangedCallback = null)
            => DependencyProperty.Register(name, type, typeof(SpatialHeatMap), new FrameworkPropertyMetadata(defaultValue, flags, propertyChangedCallback));

        public static readonly DependencyProperty DataSourceProperty;
        public static readonly DependencyProperty MaximumProperty;
        public static readonly DependencyProperty MinimumProperty;
        public static readonly DependencyProperty ContourLevelsProperty;
        public static readonly DependencyProperty GradientStopsProperty;

        public float[,] DataSource { get => (float[,])GetValue(DataSourceProperty); set => SetValue(DataSourceProperty, value); }
        public float Maximum { get => (float)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public float Minimum { get => (float)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public int ContourLevels { get => (int)GetValue(ContourLevelsProperty); set => SetValue(ContourLevelsProperty, value); }
        public GradientStopCollection GradientStops { get => (GradientStopCollection)GetValue(GradientStopsProperty); set => SetValue(GradientStopsProperty, value); }

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
    }
}
