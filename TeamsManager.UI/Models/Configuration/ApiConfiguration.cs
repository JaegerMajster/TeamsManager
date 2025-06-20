using TeamsManager.Core.Models.Configuration;

namespace TeamsManager.UI.Models.Configuration
{
    /// <summary>
    /// Konfiguracja połączenia z TeamsManager API
    /// </summary>
    public class ApiConfiguration : BaseConfiguration
    {
        /// <summary>
        /// Bazowy URL API (bez /api/)
        /// Development: https://localhost:7037
        /// Production: https://api.teamsmanager.edu.pl
        /// </summary>
        public string BaseUrl { get; set; } = "https://localhost:7037";

        /// <summary>
        /// Timeout żądań w sekundach
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Maksymalna liczba prób ponowienia
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Czy używać HTTPS (zawsze true w production)
        /// </summary>
        public bool UseHttps { get; set; } = true;

        /// <summary>
        /// Czy sprawdzać certyfikaty SSL (false tylko w development)
        /// </summary>
        public bool ValidateSslCertificates { get; set; } = true;

        /// <summary>
        /// Środowisko (Development, Staging, Production)
        /// </summary>
        public string Environment { get; set; } = "Development";

        /// <summary>
        /// Czy API jest hostowane lokalnie
        /// </summary>
        public bool IsLocalhost => BaseUrl.Contains("localhost") || BaseUrl.Contains("127.0.0.1");

        /// <summary>
        /// Pełny URL API z /api/
        /// </summary>
        public string ApiUrl => BaseUrl.TrimEnd('/') + "/api/";

        /// <summary>
        /// URL diagnostyki
        /// </summary>
        public string DiagnosticsUrl => BaseUrl.TrimEnd('/') + "/api/diagnostics/";

        /// <summary>
        /// URL Swagger (tylko development)
        /// </summary>
        public string SwaggerUrl => BaseUrl.TrimEnd('/') + "/swagger/";

        public override bool IsValid()
        {
            return base.IsValid() && 
                   !string.IsNullOrEmpty(BaseUrl) && 
                   Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) &&
                   TimeoutSeconds > 0 &&
                   MaxRetryAttempts >= 0;
        }

        /// <summary>
        /// Tworzy konfigurację dla środowiska deweloperskiego
        /// </summary>
        public static ApiConfiguration CreateDevelopment()
        {
            return new ApiConfiguration
            {
                BaseUrl = "https://localhost:7037",
                TimeoutSeconds = 30,
                MaxRetryAttempts = 3,
                UseHttps = true,
                ValidateSslCertificates = false, // Dev certificates
                Environment = "Development"
            };
        }

        /// <summary>
        /// Tworzy konfigurację dla środowiska produkcyjnego
        /// </summary>
        public static ApiConfiguration CreateProduction(string productionUrl = "https://api.teamsmanager.edu.pl")
        {
            return new ApiConfiguration
            {
                BaseUrl = productionUrl,
                TimeoutSeconds = 45,
                MaxRetryAttempts = 5,
                UseHttps = true,
                ValidateSslCertificates = true,
                Environment = "Production"
            };
        }

        /// <summary>
        /// Automatycznie wykrywa środowisko na podstawie URL
        /// </summary>
        public static ApiConfiguration CreateAutoDetect(string baseUrl)
        {
            if (baseUrl.Contains("localhost") || baseUrl.Contains("127.0.0.1"))
            {
                var config = CreateDevelopment();
                config.BaseUrl = baseUrl;
                return config;
            }
            else
            {
                return CreateProduction(baseUrl);
            }
        }
    }
} 