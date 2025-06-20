using System;

namespace TeamsManager.Api.Models.Configuration
{
    public class ApplicationConfiguration : BaseConfiguration
    {
        public string Environment { get; set; } = "Production";
        public ApplicationSettings Application { get; set; } = new();
        public ApiSettings Api { get; set; } = new();
        public SecuritySettings Security { get; set; } = new();
    }

    public class ApplicationSettings
    {
        public string Name { get; set; } = "TeamsManager";
        public string Version { get; set; } = "1.0.0";
        public bool AutoUpdate { get; set; } = true;
        public bool TelemetryEnabled { get; set; } = false;
    }

    public class ApiSettings
    {
        public string BaseUrl { get; set; } = "https://api.teamsmanager.edu.pl";
        public int Timeout { get; set; } = 30;
        public int RetryAttempts { get; set; } = 3;
        public int HealthCheckInterval { get; set; } = 300;
    }

    public class SecuritySettings
    {
        public int EncryptionKeyRotationDays { get; set; } = 90;
        public int TokenCacheExpiryHours { get; set; } = 24;
        public bool RequireSecureConnection { get; set; } = true;
    }
} 
