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
            var random = new Random();
            for (int i = 0; i < 50; i++)
            {
                mainViewModel.Samples.Add(new Sample((float)random.NextDouble() * mainViewModel.Width, (float)random.NextDouble() * mainViewModel.Height, (float)random.NextDouble() * 100));
            }
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