using TeamsManager.Core.Models.Configuration;

namespace TeamsManager.Api.Services.Configuration
{
    /// <summary>
    /// Fabryka domyślnej konfiguracji aplikacji
    /// </summary>
    public static class DefaultApplicationConfig
    {
        /// <summary>
        /// Tworzy domyślną konfigurację aplikacji dla API
        /// </summary>
        public static ApplicationConfiguration CreateDefault()
        {
            return new ApplicationConfiguration
            {
                ApplicationName = "TeamsManager API",
                ApplicationVersion = "2.0.0",
                Environment = "Production",
                
                Logging = new LoggingSettings
                {
                    LogLevel = new Dictionary<string, string>
                    {
                        { "Default", "Information" },
                        { "Microsoft.AspNetCore", "Warning" },
                        { "Microsoft.EntityFrameworkCore", "Warning" },
                        { "System.Net.Http.HttpClient", "Warning" }
                    }
                },
                
                AllowedHosts = "*",
                
                ConnectionStrings = new ConnectionStringsSettings
                {
                    // ✅ NAPRAWKA: Używaj tej samej bazy danych co UI w AppData
                    DefaultConnection = GetMainAppDatabasePath()
                },
                
                ModernHttpResilience = new ModernHttpResilienceSettings
                {
                    MicrosoftGraph = new HttpClientResilienceSettings
                    {
                        Retry = new RetrySettings
                        {
                            MaxAttempts = 3,
                            BaseDelaySeconds = 1,
                            UseJitter = true,
                            BackoffType = "Exponential"
                        },
                        CircuitBreaker = new CircuitBreakerSettings
                        {
                            FailureRatio = 0.5,
                            MinimumThroughput = 10,
                            SamplingDurationSeconds = 30,
                            BreakDurationSeconds = 60
                        },
                        Timeout = new TimeoutSettings
                        {
                            TotalRequestTimeoutSeconds = 45
                        },
                        RateLimiter = new RateLimiterSettings
                        {
                            PermitLimit = 100,
                            WindowMinutes = 1
                        }
                    },
                    ExternalApis = new HttpClientResilienceSettings
                    {
                        Retry = new RetrySettings
                        {
                            MaxAttempts = 2,
                            BaseDelaySeconds = 2,
                            UseJitter = false,
                            BackoffType = "Linear"
                        },
                        CircuitBreaker = new CircuitBreakerSettings
                        {
                            FailureRatio = 0.7,
                            MinimumThroughput = 5,
                            SamplingDurationSeconds = 30,
                            BreakDurationSeconds = 30
                        },
                        Timeout = new TimeoutSettings
                        {
                            TotalRequestTimeoutSeconds = 15
                        }
                    }
                },
                
                AdminNotifications = new AdminNotificationsSettings
                {
                    Enabled = false,
                    SystemEmail = "system@teamsmanager.edu.pl",
                    SystemName = "TeamsManager System",
                    Environment = "Production",
                    AdminEmails = new List<string>
                    {
                        "admin1@teamsmanager.edu.pl",
                        "admin2@teamsmanager.edu.pl"
                    }
                },
                
                Cache = new CacheSettings
                {
                    DefaultExpirationMinutes = 15,
                    MaxSizeMB = 100,
                    UseDistributedCache = false
                },
                
                Database = new DatabaseSettings
                {
                    Provider = "SQLite",
                    ConnectionString = "",
                    ConnectionTimeoutSeconds = 30,
                    EnableSensitiveDataLogging = false
                }
            };
        }
        
        /// <summary>
        /// Tworzy konfigurację dla środowiska deweloperskiego
        /// </summary>
        public static ApplicationConfiguration CreateDevelopment()
        {
            var config = CreateDefault();
            
            config.Environment = "Development";
            config.Logging.LogLevel["Default"] = "Debug";
            config.Database.EnableSensitiveDataLogging = true;
            config.AdminNotifications.Enabled = true;
            config.AdminNotifications.Environment = "Development";
            config.AdminNotifications.AdminEmails = new List<string>
            {
                "dev-admin@teamsmanager.edu.pl"
            };
            
            return config;
        }
        
        /// <summary>
        /// Pobiera ścieżkę do bazy danych głównej aplikacji (ta sama co UI)
        /// </summary>
        private static string GetMainAppDatabasePath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dbPath = System.IO.Path.Combine(appDataPath, "TeamsManager", "data", "teamsmanager.db");
            return $"Data Source={dbPath}";
        }
    }
} 