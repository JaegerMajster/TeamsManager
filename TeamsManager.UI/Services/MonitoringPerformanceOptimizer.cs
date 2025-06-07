using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Models.Monitoring;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Interfejs dla serwisu optymalizacji wydajności monitoringu
    /// </summary>
    public interface IMonitoringPerformanceOptimizer
    {
        /// <summary>
        /// Throttled i cached system health updates
        /// </summary>
        IObservable<SystemHealthData> OptimizedSystemHealthUpdates { get; }

        /// <summary>
        /// Batched performance metrics updates
        /// </summary>
        IObservable<SystemMetrics> OptimizedPerformanceMetricsUpdates { get; }

        /// <summary>
        /// Debounced alerts updates
        /// </summary>
        IObservable<IEnumerable<SystemAlert>> OptimizedAlertsUpdates { get; }

        /// <summary>
        /// Push new health data (będzie throttled i cached)
        /// </summary>
        void PushHealthData(SystemHealthData data);

        /// <summary>
        /// Push new metrics data (będzie batched)
        /// </summary>
        void PushMetricsData(SystemMetrics data);

        /// <summary>
        /// Push new alert (będzie debounced)
        /// </summary>
        void PushAlert(SystemAlert alert);

        /// <summary>
        /// Clear all cached data
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Get performance statistics
        /// </summary>
        OptimizationStatistics GetStatistics();
    }

    /// <summary>
    /// Serwis optymalizacji wydajności dla real-time monitoring
    /// Implementuje throttling, caching, batching i debouncing dla różnych typów danych
    /// </summary>
    public class MonitoringPerformanceOptimizer : IMonitoringPerformanceOptimizer, IDisposable
    {
        private readonly ILogger<MonitoringPerformanceOptimizer> _logger;
        private readonly IScheduler _scheduler;

        // Subjects for incoming data
        private readonly Subject<SystemHealthData> _healthDataSubject = new();
        private readonly Subject<SystemMetrics> _metricsDataSubject = new();
        private readonly Subject<SystemAlert> _alertsSubject = new();

        // Cached data
        private readonly ConcurrentDictionary<string, SystemHealthData> _healthDataCache = new();
        private readonly ConcurrentQueue<SystemMetrics> _metricsBuffer = new();
        private readonly ConcurrentDictionary<string, SystemAlert> _alertsCache = new();

        // Optimized observables
        private readonly IObservable<SystemHealthData> _optimizedHealthUpdates;
        private readonly IObservable<SystemMetrics> _optimizedMetricsUpdates;
        private readonly IObservable<IEnumerable<SystemAlert>> _optimizedAlertsUpdates;

        // Performance counters
        private long _healthDataPushCount = 0;
        private long _metricsDataPushCount = 0;
        private long _alertsPushCount = 0;
        private long _healthDataEmitCount = 0;
        private long _metricsDataEmitCount = 0;
        private long _alertsEmitCount = 0;

        // Configuration
        private readonly TimeSpan _healthThrottleInterval = TimeSpan.FromSeconds(2);
        private readonly TimeSpan _metricsBufferInterval = TimeSpan.FromSeconds(1);
        private readonly TimeSpan _alertsDebounceInterval = TimeSpan.FromSeconds(5);
        private readonly int _maxMetricsBufferSize = 10;
        private readonly int _maxAlertsBufferSize = 50;

        public MonitoringPerformanceOptimizer(
            ILogger<MonitoringPerformanceOptimizer> logger,
            IScheduler? scheduler = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scheduler = scheduler ?? TaskPoolScheduler.Default;

            // Initialize optimized observables
            _optimizedHealthUpdates = CreateOptimizedHealthUpdates();
            _optimizedMetricsUpdates = CreateOptimizedMetricsUpdates();
            _optimizedAlertsUpdates = CreateOptimizedAlertsUpdates();

            _logger.LogDebug("[PERF-OPTIMIZER] Initialized with health throttle: {HealthThrottle}s, metrics buffer: {MetricsBuffer}s, alerts debounce: {AlertsDebounce}s",
                _healthThrottleInterval.TotalSeconds,
                _metricsBufferInterval.TotalSeconds,
                _alertsDebounceInterval.TotalSeconds);
        }

        #region Public Properties

        public IObservable<SystemHealthData> OptimizedSystemHealthUpdates => _optimizedHealthUpdates;
        public IObservable<SystemMetrics> OptimizedPerformanceMetricsUpdates => _optimizedMetricsUpdates;
        public IObservable<IEnumerable<SystemAlert>> OptimizedAlertsUpdates => _optimizedAlertsUpdates;

        #endregion

        #region Public Methods

        public void PushHealthData(SystemHealthData data)
        {
            if (data == null) return;

            try
            {
                Interlocked.Increment(ref _healthDataPushCount);
                
                // Cache with component-based key for deduplication
                var key = GenerateHealthDataKey(data);
                _healthDataCache.AddOrUpdate(key, data, (k, existing) => 
                {
                    // Only update if data is newer or significantly different
                    return IsHealthDataSignificantlyDifferent(existing, data) ? data : existing;
                });

                _healthDataSubject.OnNext(data);
                
                _logger.LogTrace("[PERF-OPTIMIZER] Health data pushed. Cache size: {CacheSize}", _healthDataCache.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PERF-OPTIMIZER] Error pushing health data");
            }
        }

        public void PushMetricsData(SystemMetrics data)
        {
            if (data == null) return;

            try
            {
                Interlocked.Increment(ref _metricsDataPushCount);
                
                // Add to buffer for batching
                _metricsBuffer.Enqueue(data);
                
                // Limit buffer size
                while (_metricsBuffer.Count > _maxMetricsBufferSize)
                {
                    _metricsBuffer.TryDequeue(out _);
                }

                _metricsDataSubject.OnNext(data);
                
                _logger.LogTrace("[PERF-OPTIMIZER] Metrics data pushed. Buffer size: {BufferSize}", _metricsBuffer.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PERF-OPTIMIZER] Error pushing metrics data");
            }
        }

        public void PushAlert(SystemAlert alert)
        {
            if (alert == null) return;

            try
            {
                Interlocked.Increment(ref _alertsPushCount);
                
                // Cache alerts with deduplication
                var key = $"{alert.Component}_{alert.Level}_{alert.Message?.GetHashCode()}";
                _alertsCache.AddOrUpdate(key, alert, (k, existing) => alert);
                
                // Limit cache size
                if (_alertsCache.Count > _maxAlertsBufferSize)
                {
                    var oldestKey = _alertsCache.OrderBy(kvp => kvp.Value.Timestamp).First().Key;
                    _alertsCache.TryRemove(oldestKey, out _);
                }

                _alertsSubject.OnNext(alert);
                
                _logger.LogTrace("[PERF-OPTIMIZER] Alert pushed. Cache size: {CacheSize}", _alertsCache.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PERF-OPTIMIZER] Error pushing alert");
            }
        }

        public void ClearCache()
        {
            try
            {
                _healthDataCache.Clear();
                _alertsCache.Clear();
                
                // Clear metrics buffer
                while (_metricsBuffer.TryDequeue(out _)) { }
                
                _logger.LogDebug("[PERF-OPTIMIZER] All caches cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PERF-OPTIMIZER] Error clearing cache");
            }
        }

        public OptimizationStatistics GetStatistics()
        {
            return new OptimizationStatistics
            {
                HealthDataPushCount = _healthDataPushCount,
                HealthDataEmitCount = _healthDataEmitCount,
                HealthDataCacheSize = _healthDataCache.Count,
                HealthDataCompressionRatio = _healthDataPushCount > 0 ? (double)_healthDataEmitCount / _healthDataPushCount : 0,

                MetricsDataPushCount = _metricsDataPushCount,
                MetricsDataEmitCount = _metricsDataEmitCount,
                MetricsBufferSize = _metricsBuffer.Count,
                MetricsDataCompressionRatio = _metricsDataPushCount > 0 ? (double)_metricsDataEmitCount / _metricsDataPushCount : 0,

                AlertsPushCount = _alertsPushCount,
                AlertsEmitCount = _alertsEmitCount,
                AlertsCacheSize = _alertsCache.Count,
                AlertsCompressionRatio = _alertsPushCount > 0 ? (double)_alertsEmitCount / _alertsPushCount : 0,

                Timestamp = DateTime.UtcNow
            };
        }

        #endregion

        #region Private Methods

        private IObservable<SystemHealthData> CreateOptimizedHealthUpdates()
        {
            return _healthDataSubject
                .ObserveOn(_scheduler)
                .Throttle(_healthThrottleInterval)
                .Select(data => 
                {
                    Interlocked.Increment(ref _healthDataEmitCount);
                    
                    // Return the most recent data from cache
                    var latestData = _healthDataCache.Values
                        .OrderByDescending(d => d.LastUpdate)
                        .FirstOrDefault();
                    
                    _logger.LogTrace("[PERF-OPTIMIZER] Health data emitted (throttled). Data timestamp: {Timestamp}", 
                        latestData?.LastUpdate);
                    
                    return latestData ?? data;
                })
                .Where(data => data != null)
                .DistinctUntilChanged(new SystemHealthDataComparer())
                .Retry() // Auto-retry on errors
                .Publish()
                .RefCount();
        }

        private IObservable<SystemMetrics> CreateOptimizedMetricsUpdates()
        {
            return _metricsDataSubject
                .ObserveOn(_scheduler)
                .Buffer(_metricsBufferInterval)
                .Where(batch => batch.Any())
                .Select(batch => 
                {
                    Interlocked.Increment(ref _metricsDataEmitCount);
                    
                    // Return average of the batch for smoother visualization
                    var avgMetrics = CalculateAverageMetrics(batch);
                    
                    _logger.LogTrace("[PERF-OPTIMIZER] Metrics data emitted (batched). Batch size: {BatchSize}", batch.Count);
                    
                    return avgMetrics;
                })
                .Retry() // Auto-retry on errors
                .Publish()
                .RefCount();
        }

        private IObservable<IEnumerable<SystemAlert>> CreateOptimizedAlertsUpdates()
        {
            return _alertsSubject
                .ObserveOn(_scheduler)
                .Throttle(_alertsDebounceInterval)
                .Select(_ => 
                {
                    Interlocked.Increment(ref _alertsEmitCount);
                    
                    // Return all current alerts from cache
                    var currentAlerts = _alertsCache.Values
                        .OrderByDescending(a => a.Timestamp)
                        .ToList();
                    
                    _logger.LogTrace("[PERF-OPTIMIZER] Alerts emitted (debounced). Count: {AlertsCount}", currentAlerts.Count);
                    
                    return currentAlerts.AsEnumerable();
                })
                .Retry() // Auto-retry on errors
                .Publish()
                .RefCount();
        }

        private string GenerateHealthDataKey(SystemHealthData data)
        {
            // Generate key based on component statuses for deduplication
            var componentStatuses = string.Join("|", 
                data.Components.OrderBy(c => c.Name).Select(c => $"{c.Name}:{c.Status}"));
            return $"{data.OverallStatus}_{componentStatuses}";
        }

        private bool IsHealthDataSignificantlyDifferent(SystemHealthData existing, SystemHealthData new_data)
        {
            // Check if overall status changed
            if (existing.OverallStatus != new_data.OverallStatus)
                return true;

            // Check if any component status changed
            var existingComponentMap = existing.Components.ToDictionary(c => c.Name, c => c.Status);
            var newComponentMap = new_data.Components.ToDictionary(c => c.Name, c => c.Status);

            foreach (var kvp in newComponentMap)
            {
                if (!existingComponentMap.TryGetValue(kvp.Key, out var existingStatus) || 
                    existingStatus != kvp.Value)
                {
                    return true;
                }
            }

            // Check for timing threshold (update at least every 30 seconds)
            return DateTime.UtcNow - existing.LastUpdate > TimeSpan.FromSeconds(30);
        }

        private SystemMetrics CalculateAverageMetrics(IList<SystemMetrics> batch)
        {
            if (!batch.Any()) 
                return batch.First();

            // Calculate averages for smoother visualization
            return new SystemMetrics
            {
                CpuUsagePercent = batch.Average(m => m.CpuUsagePercent),
                MemoryUsagePercent = batch.Average(m => m.MemoryUsagePercent),
                DiskUsagePercent = batch.Average(m => m.DiskUsagePercent),
                NetworkThroughputMbps = batch.Average(m => m.NetworkThroughputMbps),
                ActiveConnections = (int)batch.Average(m => m.ActiveConnections),
                RequestsPerMinute = (int)batch.Average(m => m.RequestsPerMinute),
                AverageResponseTimeMs = batch.Average(m => m.AverageResponseTimeMs),
                ErrorRate = batch.Average(m => m.ErrorRate),
                Timestamp = batch.Max(m => m.Timestamp) // Use latest timestamp
            };
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            try
            {
                _healthDataSubject?.Dispose();
                _metricsDataSubject?.Dispose();
                _alertsSubject?.Dispose();
                
                ClearCache();
                
                _logger.LogDebug("[PERF-OPTIMIZER] Disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PERF-OPTIMIZER] Error during disposal");
            }
        }

        #endregion
    }

    /// <summary>
    /// Comparer for SystemHealthData to detect meaningful changes
    /// </summary>
    public class SystemHealthDataComparer : IEqualityComparer<SystemHealthData>
    {
        public bool Equals(SystemHealthData? x, SystemHealthData? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;

            // Compare overall status
            if (x.OverallStatus != y.OverallStatus) return false;

            // Compare component counts and statuses
            if (x.Components.Count != y.Components.Count) return false;

            var xComponents = x.Components.OrderBy(c => c.Name).ToList();
            var yComponents = y.Components.OrderBy(c => c.Name).ToList();

            for (int i = 0; i < xComponents.Count; i++)
            {
                if (xComponents[i].Name != yComponents[i].Name ||
                    xComponents[i].Status != yComponents[i].Status)
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(SystemHealthData obj)
        {
            if (obj == null) return 0;

            var hash = obj.OverallStatus.GetHashCode();
            foreach (var component in obj.Components.OrderBy(c => c.Name))
            {
                hash = HashCode.Combine(hash, component.Name, component.Status);
            }
            return hash;
        }
    }

    /// <summary>
    /// Statistics for monitoring optimization performance
    /// </summary>
    public class OptimizationStatistics
    {
        public long HealthDataPushCount { get; set; }
        public long HealthDataEmitCount { get; set; }
        public int HealthDataCacheSize { get; set; }
        public double HealthDataCompressionRatio { get; set; }

        public long MetricsDataPushCount { get; set; }
        public long MetricsDataEmitCount { get; set; }
        public int MetricsBufferSize { get; set; }
        public double MetricsDataCompressionRatio { get; set; }

        public long AlertsPushCount { get; set; }
        public long AlertsEmitCount { get; set; }
        public int AlertsCacheSize { get; set; }
        public double AlertsCompressionRatio { get; set; }

        public DateTime Timestamp { get; set; }

        public double OverallCompressionRatio => 
            (HealthDataPushCount + MetricsDataPushCount + AlertsPushCount) > 0 ?
            (double)(HealthDataEmitCount + MetricsDataEmitCount + AlertsEmitCount) / 
            (HealthDataPushCount + MetricsDataPushCount + AlertsPushCount) : 0;
    }
} 