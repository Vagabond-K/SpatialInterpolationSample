using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SpatialInterpolationSample
{
    class PointXConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var from = values[0] as UIElement;
            var to = values[1] as UIElement;
            return from.TranslatePoint(new Point(values[2].To<double>(), 0), to).X;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    class PointYConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var from = values[0] as UIElement;
            var to = values[1] as UIElement;
            return from.TranslatePoint(new Point(0, values[2].To<double>()), to).Y;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
