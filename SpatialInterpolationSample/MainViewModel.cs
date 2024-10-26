using SpatialInterpolationSample.SpatialInterpolation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Input;

namespace SpatialInterpolationSample
{
    class MainViewModel : NotifyPropertyChangeObject
    {
        public int Width { get => Get(800); set => Set(value); }
        public int Height { get => Get(600); set => Set(value); }

        public bool IsBusy { get => Get(false); set => Set(value); }

        public float[,] DataSource { get => Get<float[,]>(); set => Set(value); }

        public IList<Sample> Samples => Get(() =>
        {
            var result = new ObservableCollection<Sample>();
            result.CollectionChanged += OnSamplesCollectionChanged;
            return result;
        });

        public Sample SelectedSample { get => Get<Sample>(); set => Set(value); }
        public ICommand AddSampleCommand => GetCommand(() => Samples.Add(new Sample(Width / 2, Height / 2, 0)));
        public ICommand RemoveSampleCommand => GetCommand(() => Samples.Remove(SelectedSample), () => SelectedSample != null);

        private ISpatialInterpolation interpolation = new IdwInterpolation { WeightPower = 1 };
        private CancellationTokenSource cancellation;

        private void OnSamplesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            e.NewItems?.Cast<Sample>()?.AsParallel()?.ForAll(item => item.PropertyChanged += OnSamplePropertyChanged);
            e.OldItems?.Cast<Sample>()?.AsParallel()?.ForAll(item => item.PropertyChanged -= OnSamplePropertyChanged);
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    SelectedSample = e.NewItems?.Cast<Sample>()?.LastOrDefault();
                    break;
                case NotifyCollectionChangedAction.Remove:
                    SelectedSample = Samples.Count > 0 ? Samples[Math.Min(Samples.Count - 1, e.OldStartingIndex)] : null;
                    break;
            }
            UpdateDataSource();
        }

        private void OnSamplePropertyChanged(object sender, PropertyChangedEventArgs e) => UpdateDataSource();

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            switch (e.PropertyName)
            {
                case nameof(Width):
                case nameof(Height):
                    UpdateDataSource();
                    break;
                case nameof(SelectedSample):
                    (RemoveSampleCommand as IInstantCommand)?.RaiseCanExecuteChanged();
                    break;
            }
        }

        private async void UpdateDataSource()
        {
            IsBusy = true;
            cancellation?.Cancel();
            cancellation = new CancellationTokenSource();
            try
            {
                var dataSource = await interpolation.Interpolate(Width, Height, Samples.Select(item => item.ToSpatialSample()).ToArray(), cancellation.Token);
                if (dataSource != null)
                {
                    DataSource = dataSource;
                    IsBusy = false;
                }
            }
            catch { }
        }
    }
}
