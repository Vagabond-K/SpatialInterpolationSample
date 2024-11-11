using SpatialInterpolation.Interpolations;
using System;
using System.Linq;
using System.Windows;
using VagabondK.Windows;

namespace SpatialInterpolation
{
    public partial class MainWindow : ThemeWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            Title = $"{Title} - {typeof(MainWindow).Assembly.GetName().Name}";

            heatMap.MouseLeftButtonDown += (sender, e) =>
            {
                if (!heatMap.IsMouseCaptured)
                {
                    heatMap.CaptureMouse();
                    UpdatePoint(e.GetPosition(heatMap));
                }
            };
            heatMap.MouseMove += (sender, e) =>
            {
                if (heatMap.IsMouseCaptured)
                    UpdatePoint(e.GetPosition(heatMap));
            };
            heatMap.MouseLeftButtonUp += (sender, e) =>
            {
                if (heatMap.IsMouseCaptured)
                {
                    heatMap.ReleaseMouseCapture();
                    UpdatePoint(e.GetPosition(heatMap));
                }
            };

            var mainViewModel = new MainViewModel();

            //mainViewModel.Samples.Add(new Sample(mainViewModel.Width / 3, mainViewModel.Height / 3, 0));
            //mainViewModel.Samples.Add(new Sample(mainViewModel.Width * 2 / 3, mainViewModel.Height / 3, 25));
            //mainViewModel.Samples.Add(new Sample(mainViewModel.Width / 3, mainViewModel.Height * 2 / 3, 75));
            //mainViewModel.Samples.Add(new Sample(mainViewModel.Width * 2 / 3, mainViewModel.Height * 2 / 3, 100));
            var random = new Random(1234567890);
            var width = mainViewModel.Width;
            var height = mainViewModel.Height;
            var min = heatMap.Minimum;
            var max = heatMap.Maximum;
            for (int i = 0; i < 50; i++)
                mainViewModel.Samples.Add(new Sample(
                    (float)random.NextDouble() * width,
                    (float)random.NextDouble() * height,
                    (float)random.NextDouble() * (max - min) + min));

            mainViewModel.SelectedSample = mainViewModel.Samples.FirstOrDefault();

            DataContext = mainViewModel;
        }

        void UpdatePoint(Point point)
        {
            if (listBoxSamples.SelectedItem is Sample sample)
            {
                sample.X = (float)point.X;
                sample.Y = (float)point.Y;
            }
        }
    }
}