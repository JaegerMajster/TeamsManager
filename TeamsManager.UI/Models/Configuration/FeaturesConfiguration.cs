using TeamsManager.Core.Models.Configuration;

namespace TeamsManager.UI.Models.Configuration
{
    public class FeaturesConfiguration : BaseConfiguration
    {
        public FeatureFlags Features { get; set; } = new();
        public LimitSettings Limits { get; set; } = new();
    }

    public class FeatureFlags
    {
        public bool BulkOperations { get; set; } = true;
        public bool AdvancedReporting { get; set; } = true;
        public bool RealTimeSync { get; set; } = false;
        public bool BetaFeatures { get; set; } = false;
        public bool Telemetry { get; set; } = false;
    }

    public class LimitSettings
    {
        public int MaxBulkOperations { get; set; } = 1000;
        public int MaxConcurrentRequests { get; set; } = 10;
        public int MaxLogFileSizeMB { get; set; } = 100;
    }
} 