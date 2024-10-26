using System.Linq;
using System.Windows;
using VagabondK.Windows;

namespace SpatialInterpolationSample
{
    public partial class MainWindow : ThemeWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            colorMap.MouseLeftButtonDown += (sender, e) =>
            {
                if (!colorMap.IsMouseCaptured)
                {
                    colorMap.CaptureMouse();
                    UpdatePoint(e.GetPosition(colorMap));
                }
            };
            colorMap.MouseMove += (sender, e) =>
            {
                if (colorMap.IsMouseCaptured)
                    UpdatePoint(e.GetPosition(colorMap));
            };
            colorMap.MouseLeftButtonUp += (sender, e) =>
            {
                if (colorMap.IsMouseCaptured)
                {
                    colorMap.ReleaseMouseCapture();
                    UpdatePoint(e.GetPosition(colorMap));
                }
            };

            var mainViewModel = new MainViewModel();
            mainViewModel.Samples.Add(new Sample(mainViewModel.Width / 3, mainViewModel.Height / 3, 0));
            mainViewModel.Samples.Add(new Sample(mainViewModel.Width * 2 / 3, mainViewModel.Height / 3, 25));
            mainViewModel.Samples.Add(new Sample(mainViewModel.Width / 3, mainViewModel.Height * 2 / 3, 75));
            mainViewModel.Samples.Add(new Sample(mainViewModel.Width * 2 / 3, mainViewModel.Height * 2 / 3, 100));
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
