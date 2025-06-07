using TeamsManager.Core.Enums;

namespace TeamsManager.UI.Models.Monitoring
{
    public class SystemHealthData
    {
        public HealthCheck OverallStatus { get; set; }
        public DateTime LastUpdate { get; set; }
        public List<HealthComponent> Components { get; set; } = new();
    }
    
    public class HealthComponent
    {
        public string Name { get; set; } = string.Empty;
        public HealthCheck Status { get; set; }
        public string Description { get; set; } = string.Empty;
        public TimeSpan ResponseTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    public class SystemMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsagePercent { get; set; }
        public double DiskUsagePercent { get; set; }
        public double NetworkThroughputMbps { get; set; }
        public int ActiveConnections { get; set; }
        public int RequestsPerMinute { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public double ErrorRate { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public class ActiveOperationData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public OperationStatus Status { get; set; }
        public double Progress { get; set; }
        public string User { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public bool CanCancel { get; set; }
        public string Details { get; set; } = string.Empty;
    }
    
    public class SystemAlert
    {
        public string Id { get; set; } = string.Empty;
        public AlertLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsAcknowledged { get; set; }
        public string Details { get; set; } = string.Empty;
    }
    
    public class MonitoringDashboardSummary
    {
        public SystemHealthData SystemHealth { get; set; } = new();
        public SystemMetrics PerformanceMetrics { get; set; } = new();
        public List<ActiveOperationData> ActiveOperations { get; set; } = new();
        public List<SystemAlert> RecentAlerts { get; set; } = new();
        public DateTime LastUpdate { get; set; }
    }
    
    public enum AlertLevel
    {
        Info,
        Warning,
        Error,
        Critical
    }
    
    public enum HealthCheck
    {
        Healthy,
        Warning,
        Critical,
        Unknown
    }
} 