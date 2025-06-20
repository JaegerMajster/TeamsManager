using System.ComponentModel.DataAnnotations;

namespace TeamsManager.Core.Models.Configuration
{
    /// <summary>
    /// Konfiguracja aplikacji - ustawienia ogólne, połączenia, resilience, powiadomienia
    /// </summary>
    public class ApplicationConfiguration : BaseConfiguration
    {
        /// <summary>
        /// Nazwa aplikacji
        /// </summary>
        public string ApplicationName { get; set; } = "TeamsManager";

        /// <summary>
        /// Wersja aplikacji
        /// </summary>
        public string ApplicationVersion { get; set; } = "2.0";

        /// <summary>
        /// Środowisko (Development, Staging, Production)
        /// </summary>
        public string Environment { get; set; } = "Production";

        /// <summary>
        /// Ustawienia logowania
        /// </summary>
        public LoggingSettings Logging { get; set; } = new();

        /// <summary>
        /// Ustawienia cache
        /// </summary>
        public CacheSettings Cache { get; set; } = new();

        /// <summary>
        /// Ustawienia bazy danych
        /// </summary>
        public DatabaseSettings Database { get; set; } = new();

        /// <summary>
        /// Dozwolone hosty
        /// </summary>
        public string AllowedHosts { get; set; } = "*";

        /// <summary>
        /// Stringi połączeń z bazami danych
        /// </summary>
        public ConnectionStringsSettings ConnectionStrings { get; set; } = new();

        /// <summary>
        /// Ustawienia resilience dla HTTP klientów
        /// </summary>
        public ModernHttpResilienceSettings ModernHttpResilience { get; set; } = new();

        /// <summary>
        /// Ustawienia powiadomień administracyjnych
        /// </summary>
        public AdminNotificationsSettings AdminNotifications { get; set; } = new();

        /// <summary>
        /// Sprawdza czy konfiguracja aplikacji jest prawidłowa
        /// </summary>
        /// <returns>True jeśli konfiguracja jest prawidłowa</returns>
        public override bool IsValid()
        {
            return base.IsValid() &&
                   !string.IsNullOrEmpty(ApplicationName) &&
                   !string.IsNullOrEmpty(ApplicationVersion) &&
                   !string.IsNullOrEmpty(Environment) &&
                   !string.IsNullOrWhiteSpace(ConnectionStrings.DefaultConnection) &&
                   Logging.IsValid() &&
                   ModernHttpResilience.IsValid() &&
                   AdminNotifications.IsValid();
        }
    }

    /// <summary>
    /// Ustawienia logowania
    /// </summary>
    public class LoggingSettings
    {
        /// <summary>
        /// Poziomy logowania dla różnych kategorii
        /// </summary>
        public Dictionary<string, string> LogLevel { get; set; } = new()
        {
            { "Default", "Information" },
            { "Microsoft.AspNetCore", "Warning" }
        };

        public bool IsValid() => LogLevel.Any();
    }

    /// <summary>
    /// Ustawienia cache
    /// </summary>
    public class CacheSettings
    {
        /// <summary>
        /// Domyślny czas wygaśnięcia cache w minutach
        /// </summary>
        public int DefaultExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// Maksymalny rozmiar cache w MB
        /// </summary>
        public int MaxSizeMB { get; set; } = 100;

        /// <summary>
        /// Czy używać distributed cache
        /// </summary>
        public bool UseDistributedCache { get; set; } = false;
    }

    /// <summary>
    /// Ustawienia bazy danych
    /// </summary>
    public class DatabaseSettings
    {
        /// <summary>
        /// Typ bazy danych (SQLite, SqlServer)
        /// </summary>
        public string Provider { get; set; } = "SQLite";

        /// <summary>
        /// Connection string (może być zaszyfrowany)
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Timeout połączenia w sekundach
        /// </summary>
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Czy włączyć sensitive data logging
        /// </summary>
        public bool EnableSensitiveDataLogging { get; set; } = false;
    }

    /// <summary>
    /// Stringi połączeń
    /// </summary>
    public class ConnectionStringsSettings
    {
        /// <summary>
        /// Główne połączenie z bazą danych
        /// </summary>
        [Required]
        public string DefaultConnection { get; set; } = "Data Source=teamsmanager.db";
    }

    /// <summary>
    /// Ustawienia resilience dla HTTP klientów
    /// </summary>
    public class ModernHttpResilienceSettings
    {
        /// <summary>
        /// Ustawienia dla Microsoft Graph API
        /// </summary>
        public HttpClientResilienceSettings MicrosoftGraph { get; set; } = new()
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
        };

        /// <summary>
        /// Ustawienia dla zewnętrznych API
        /// </summary>
        public HttpClientResilienceSettings ExternalApis { get; set; } = new()
        {
            Retry = new RetrySettings
            {
                MaxAttempts = 2,
                BaseDelaySeconds = 2
            },
            CircuitBreaker = new CircuitBreakerSettings
            {
                FailureRatio = 0.7,
                BreakDurationSeconds = 30
            },
            Timeout = new TimeoutSettings
            {
                TotalRequestTimeoutSeconds = 15
            }
        };

        public bool IsValid() => MicrosoftGraph.IsValid() && ExternalApis.IsValid();
    }

    /// <summary>
    /// Ustawienia resilience dla pojedynczego HTTP klienta
    /// </summary>
    public class HttpClientResilienceSettings
    {
        public RetrySettings Retry { get; set; } = new();
        public CircuitBreakerSettings CircuitBreaker { get; set; } = new();
        public TimeoutSettings Timeout { get; set; } = new();
        public RateLimiterSettings? RateLimiter { get; set; }

        public bool IsValid() => Retry.IsValid() && CircuitBreaker.IsValid() && Timeout.IsValid();
    }

    /// <summary>
    /// Ustawienia retry
    /// </summary>
    public class RetrySettings
    {
        public int MaxAttempts { get; set; } = 3;
        public int BaseDelaySeconds { get; set; } = 1;
        public bool UseJitter { get; set; } = true;
        public string BackoffType { get; set; } = "Exponential";

        public bool IsValid() => MaxAttempts > 0 && BaseDelaySeconds > 0;
    }

    /// <summary>
    /// Ustawienia circuit breaker
    /// </summary>
    public class CircuitBreakerSettings
    {
        public double FailureRatio { get; set; } = 0.5;
        public int MinimumThroughput { get; set; } = 10;
        public int SamplingDurationSeconds { get; set; } = 30;
        public int BreakDurationSeconds { get; set; } = 60;

        public bool IsValid() => FailureRatio > 0 && FailureRatio < 1 && 
                                MinimumThroughput > 0 && 
                                SamplingDurationSeconds > 0 && 
                                BreakDurationSeconds > 0;
    }

    /// <summary>
    /// Ustawienia timeout
    /// </summary>
    public class TimeoutSettings
    {
        public int TotalRequestTimeoutSeconds { get; set; } = 30;

        public bool IsValid() => TotalRequestTimeoutSeconds > 0;
    }

    /// <summary>
    /// Ustawienia rate limiter
    /// </summary>
    public class RateLimiterSettings
    {
        public int PermitLimit { get; set; } = 100;
        public int WindowMinutes { get; set; } = 1;

        public bool IsValid() => PermitLimit > 0 && WindowMinutes > 0;
    }

    /// <summary>
    /// Ustawienia powiadomień administracyjnych
    /// </summary>
    public class AdminNotificationsSettings
    {
        public bool Enabled { get; set; } = false;
        public string SystemEmail { get; set; } = "system@teamsmanager.edu.pl";
        public string SystemName { get; set; } = "TeamsManager System";
        public string Environment { get; set; } = "Production";
        public List<string> AdminEmails { get; set; } = new();

        public bool IsValid() => !string.IsNullOrWhiteSpace(SystemEmail) && 
                                !string.IsNullOrWhiteSpace(SystemName) && 
                                !string.IsNullOrWhiteSpace(Environment);
    }
} 