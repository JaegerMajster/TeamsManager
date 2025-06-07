using System;
using ReactiveUI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Services;
using TeamsManager.UI.Models.Monitoring;
using TeamsManager.Core.Models;

namespace TeamsManager.UI.ViewModels.Monitoring.Widgets
{
    /// <summary>
    /// ViewModel dla zaawansowanego widgetu wykresów wydajności systemu
    /// Obsługuje real-time charting z LiveCharts2
    /// </summary>
    public class AdvancedPerformanceChartWidgetViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ISignalRService _signalRService;
        private readonly IMonitoringDataService _monitoringDataService;
        private readonly ILogger<AdvancedPerformanceChartWidgetViewModel> _logger;
        private readonly CompositeDisposable _disposables = new();

        // Chart data storage (ostatnie 100 pomiarów)
        private readonly ObservableCollection<DateTimePoint> _cpuData = new();
        private readonly ObservableCollection<DateTimePoint> _memoryData = new();
        private readonly ObservableCollection<DateTimePoint> _networkData = new();
        private readonly ObservableCollection<DateTimePoint> _responseTimeData = new();

        // Current values
        private double _currentCpuUsage = 0;
        private double _currentMemoryUsage = 0;
        private double _currentNetworkThroughput = 0;
        private double _currentResponseTime = 0;

        // UI State
        private bool _isLoading = true;
        private bool _hasError = false;
        private int _selectedChartType = 0; // 0=Line, 1=Column, 2=Area
        private int _selectedTimeRange = 0; // 0=1h, 1=6h, 2=24h, 3=7d

        public AdvancedPerformanceChartWidgetViewModel(
            ISignalRService signalRService,
            IMonitoringDataService monitoringDataService,
            ILogger<AdvancedPerformanceChartWidgetViewModel> logger)
        {
            _signalRService = signalRService ?? throw new ArgumentNullException(nameof(signalRService));
            _monitoringDataService = monitoringDataService ?? throw new ArgumentNullException(nameof(monitoringDataService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeCommands();
            InitializeChart();
            SubscribeToUpdates();
            LoadInitialData();
        }

        #region Properties

        public double CurrentCpuUsage
        {
            get => _currentCpuUsage;
            set
            {
                _currentCpuUsage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CpuUsageColor));
            }
        }

        public double CurrentMemoryUsage
        {
            get => _currentMemoryUsage;
            set
            {
                _currentMemoryUsage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MemoryUsageColor));
            }
        }

        public double CurrentNetworkThroughput
        {
            get => _currentNetworkThroughput;
            set
            {
                _currentNetworkThroughput = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NetworkUsageColor));
            }
        }

        public double CurrentResponseTime
        {
            get => _currentResponseTime;
            set
            {
                _currentResponseTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ResponseTimeColor));
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public bool HasError
        {
            get => _hasError;
            set
            {
                _hasError = value;
                OnPropertyChanged();
            }
        }

        public int SelectedChartType
        {
            get => _selectedChartType;
            set
            {
                _selectedChartType = value;
                OnPropertyChanged();
                UpdateChartType();
            }
        }

        public int SelectedTimeRange
        {
            get => _selectedTimeRange;
            set
            {
                _selectedTimeRange = value;
                OnPropertyChanged();
                UpdateTimeRange();
            }
        }

        // Color properties for metrics based on thresholds
        public Brush CpuUsageColor => GetMetricColor(CurrentCpuUsage);
        public Brush MemoryUsageColor => GetMetricColor(CurrentMemoryUsage);
        public Brush NetworkUsageColor => GetNetworkColor(CurrentNetworkThroughput);
        public Brush ResponseTimeColor => GetResponseTimeColor(CurrentResponseTime);

        // Chart properties
        public ObservableCollection<ISeries> ChartSeries { get; private set; } = new();
        public ObservableCollection<Axis> XAxes { get; private set; } = new();
        public ObservableCollection<Axis> YAxes { get; private set; } = new();

        // Commands
        public ICommand RefreshCommand { get; private set; } = null!;

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            RefreshCommand = new AsyncRelayCommand(async () => await RefreshDataAsync());
        }

        private void InitializeChart()
        {
            // Configure X axis (time)
            XAxes.Add(new Axis
            {
                Name = "Czas",
                NameTextSize = 14,
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                NamePaint = new SolidColorPaint(SKColors.Gray),
                SeparatorsPaint = new SolidColorPaint(SKColors.LightGray) { StrokeThickness = 1 },
                Labeler = value => new DateTime((long)value).ToString("HH:mm:ss"),
                MinStep = TimeSpan.FromMinutes(1).Ticks
            });

            // Configure Y axis (percentage)
            YAxes.Add(new Axis
            {
                Name = "Wykorzystanie (%)",
                NameTextSize = 14,
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                NamePaint = new SolidColorPaint(SKColors.Gray),
                SeparatorsPaint = new SolidColorPaint(SKColors.LightGray) { StrokeThickness = 1 },
                MinLimit = 0,
                MaxLimit = 100,
                Labeler = value => $"{value:F0}%"
            });

            UpdateChartType();
        }

        private void UpdateChartType()
        {
            ChartSeries.Clear();

            switch (SelectedChartType)
            {
                case 0: // Line Chart
                    CreateLineChart();
                    break;
                case 1: // Column Chart
                    CreateColumnChart();
                    break;
                case 2: // Area Chart
                    CreateAreaChart();
                    break;
            }
        }

        private void CreateLineChart()
        {
            ChartSeries.Add(new LineSeries<DateTimePoint>
            {
                Name = "CPU",
                Values = _cpuData,
                Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 },
                Fill = null,
                GeometryStroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.DodgerBlue),
                GeometrySize = 4
            });

            ChartSeries.Add(new LineSeries<DateTimePoint>
            {
                Name = "RAM",
                Values = _memoryData,
                Stroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 2 },
                Fill = null,
                GeometryStroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.Orange),
                GeometrySize = 4
            });

            ChartSeries.Add(new LineSeries<DateTimePoint>
            {
                Name = "Sieć (Mbps/10)",
                Values = _networkData,
                Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 },
                Fill = null,
                GeometryStroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.Green),
                GeometrySize = 4
            });
        }

        private void CreateColumnChart()
        {
            ChartSeries.Add(new ColumnSeries<DateTimePoint>
            {
                Name = "CPU",
                Values = _cpuData,
                Fill = new SolidColorPaint(SKColors.DodgerBlue),
                Stroke = new SolidColorPaint(SKColors.DarkBlue) { StrokeThickness = 1 }
            });

            ChartSeries.Add(new ColumnSeries<DateTimePoint>
            {
                Name = "RAM",
                Values = _memoryData,
                Fill = new SolidColorPaint(SKColors.Orange),
                Stroke = new SolidColorPaint(SKColors.DarkOrange) { StrokeThickness = 1 }
            });
        }

        private void CreateAreaChart()
        {
            ChartSeries.Add(new LineSeries<DateTimePoint>
            {
                Name = "CPU",
                Values = _cpuData,
                Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(SKColors.DodgerBlue.WithAlpha(100)),
                GeometryStroke = null,
                GeometryFill = null,
                GeometrySize = 0
            });

            ChartSeries.Add(new LineSeries<DateTimePoint>
            {
                Name = "RAM",
                Values = _memoryData,
                Stroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(SKColors.Orange.WithAlpha(100)),
                GeometryStroke = null,
                GeometryFill = null,
                GeometrySize = 0
            });
        }

        private void UpdateTimeRange()
        {
            // Implement time range filtering logic
            var hoursToShow = SelectedTimeRange switch
            {
                0 => 1,   // 1 hour
                1 => 6,   // 6 hours
                2 => 24,  // 24 hours
                3 => 168, // 7 days
                _ => 1
            };

            var cutoffTime = DateTime.UtcNow.AddHours(-hoursToShow);

            // Filter data based on time range
            FilterDataByTimeRange(cutoffTime);
        }

        private void FilterDataByTimeRange(DateTime cutoffTime)
        {
            // Remove old data points
            var cutoffTicks = cutoffTime.Ticks;

            RemoveOldPoints(_cpuData, cutoffTicks);
            RemoveOldPoints(_memoryData, cutoffTicks);
            RemoveOldPoints(_networkData, cutoffTicks);
            RemoveOldPoints(_responseTimeData, cutoffTicks);
        }

        private void RemoveOldPoints(ObservableCollection<DateTimePoint> collection, long cutoffTicks)
        {
            for (int i = collection.Count - 1; i >= 0; i--)
            {
                if (collection[i].DateTime.Ticks < cutoffTicks)
                {
                    collection.RemoveAt(i);
                }
            }
        }

        private Brush GetMetricColor(double value)
        {
            return value switch
            {
                <= 60 => Brushes.Green,
                <= 80 => Brushes.Orange,
                _ => Brushes.Red
            };
        }

        private Brush GetNetworkColor(double mbps)
        {
            return mbps switch
            {
                >= 100 => Brushes.Green,
                >= 50 => Brushes.Orange,
                _ => Brushes.Red
            };
        }

        private Brush GetResponseTimeColor(double ms)
        {
            return ms switch
            {
                <= 100 => Brushes.Green,
                <= 500 => Brushes.Orange,
                _ => Brushes.Red
            };
        }

        private void SubscribeToUpdates()
        {
            // Subscribe to real-time performance updates
            Observable.Empty<SystemMetrics>() // _signalRService.PerformanceMetricsUpdates
                .ObserveOn(System.Reactive.Concurrency.Scheduler.CurrentThread)
                .Subscribe(
                    metrics => UpdateWithNewMetrics(metrics),
                    error => 
                    {
                        _logger.LogError(error, "[ADVANCED-PERF-CHART] Error in performance updates subscription");
                        HasError = true;
                    })
                .DisposeWith(_disposables);
        }

        private void UpdateWithNewMetrics(SystemMetrics metrics)
        {
            try
            {
                var now = DateTime.UtcNow;
                var point = new DateTimePoint(now, 0); // Will be updated below

                // Update current values
                CurrentCpuUsage = metrics.CpuUsagePercent;
                CurrentMemoryUsage = metrics.MemoryUsagePercent;
                CurrentNetworkThroughput = metrics.NetworkThroughputMbps;
                CurrentResponseTime = metrics.AverageResponseTimeMs;

                // Add new data points to chart
                AddDataPoint(_cpuData, now, metrics.CpuUsagePercent);
                AddDataPoint(_memoryData, now, metrics.MemoryUsagePercent);
                AddDataPoint(_networkData, now, metrics.NetworkThroughputMbps / 10); // Scale for chart
                AddDataPoint(_responseTimeData, now, Math.Min(100, metrics.AverageResponseTimeMs / 10)); // Scale and cap

                // Limit data points (keep last 100)
                LimitDataPoints(_cpuData);
                LimitDataPoints(_memoryData);
                LimitDataPoints(_networkData);
                LimitDataPoints(_responseTimeData);

                HasError = false;
                IsLoading = false;

                _logger.LogDebug("[ADVANCED-PERF-CHART] Updated with new metrics: CPU={CpuUsage:F1}%, RAM={MemoryUsage:F1}%", 
                    metrics.CpuUsagePercent, metrics.MemoryUsagePercent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ADVANCED-PERF-CHART] Error updating chart with new metrics");
                HasError = true;
            }
        }

        private void AddDataPoint(ObservableCollection<DateTimePoint> collection, DateTime time, double value)
        {
            var point = new DateTimePoint(time, value);
            collection.Add(point);
        }

        private void LimitDataPoints(ObservableCollection<DateTimePoint> collection)
        {
            while (collection.Count > 100)
            {
                collection.RemoveAt(0);
            }
        }

        private async Task LoadInitialData()
        {
            try
            {
                IsLoading = true;
                HasError = false;

                _logger.LogDebug("[ADVANCED-PERF-CHART] Loading initial performance data");

                var metrics = await _monitoringDataService.GetPerformanceMetricsAsync();
                if (metrics != null)
                {
                    UpdateWithNewMetrics(metrics);
                }

                _logger.LogDebug("[ADVANCED-PERF-CHART] Initial data loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ADVANCED-PERF-CHART] Error loading initial data");
                HasError = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshDataAsync()
        {
            await LoadInitialData();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _disposables?.Dispose();
        }

        #endregion
    }
} 
