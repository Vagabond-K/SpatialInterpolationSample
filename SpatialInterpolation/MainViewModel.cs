using SpatialInterpolation.Interpolations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SpatialInterpolation
{
    class MainViewModel : NotifyPropertyChangeObject
    {
        public int Width { get => Get(800); set => Set(value); }
        public int Height { get => Get(600); set => Set(value); }

        public bool UseGPU { get => Get(false); set => Set(value); }
        public bool IsBusy { get => Get(false); set => Set(value); }
        public long Duration { get => Get(0L); set => Set(value); }

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

        private ISpatialInterpolation cpuInterpolation = new CpuIdwInterpolation { WeightPower = 2 };
        private ISpatialInterpolation gpuInterpolation = new GpuIdwInterpolation { WeightPower = 2 };
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
            var width = Width;
            var height = Height;
            var samples = Samples.Select(item => item.ToSpatialSample()).ToArray();
            var dataSource = new float[height, width];
            bool isRunnung = false;
            if (samples.Length == 0)
            {
                this.cancellation?.Cancel();
                DataSource = dataSource;
                IsBusy = false;
                return;
            }

            var cancellation = new CancellationTokenSource();
            this.cancellation?.Cancel();
            this.cancellation = cancellation;
            _ = Task.Delay(50).ContinueWith(task =>
            {
                lock (this)
                {
                    if (isRunnung)
                        IsBusy = true;
                }
            }, cancellation.Token);
            isRunnung = true;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await (UseGPU ? gpuInterpolation : cpuInterpolation).Interpolate(samples, dataSource, cancellation.Token);
            }
            catch
            {
                return;
            }
            lock (this)
            {
                isRunnung = false;
                if (!cancellation.IsCancellationRequested)
                {
                    DataSource = dataSource;
                    Duration = stopwatch.ElapsedMilliseconds;
                    IsBusy = false;
                }
            }
        }
    }
}
