using Microsoft.Extensions.Logging;
using TeamsManager.UI.Models.Configuration;
using TeamsManager.UI.Services.Abstractions;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.NetworkInformation;

namespace TeamsManager.UI.Services.Configuration
{
    public class ApiConfigurationService : IApiConfigurationService
    {
        private readonly IConfigurationManagerV2 _configManager;
        private readonly ILogger<ApiConfigurationService> _logger;
        private readonly HttpClient _httpClient;
        private readonly EmbeddedApiServer _embeddedApiServer;
        private ApiConfiguration? _cachedConfig;

        public ApiConfigurationService(
            IConfigurationManagerV2 configManager,
            ILogger<ApiConfigurationService> logger,
            HttpClient httpClient,
            EmbeddedApiServer embeddedApiServer)
        {
            _configManager = configManager;
            _logger = logger;
            _httpClient = httpClient;
            _embeddedApiServer = embeddedApiServer;
        }

        public async Task<ApiConfiguration> GetApiConfigurationAsync()
        {
            if (_cachedConfig != null)
            {
                // Sprawdź czy embedded server się uruchomił i zaktualizuj konfigurację
                if (_embeddedApiServer.IsRunning && !_cachedConfig.BaseUrl.Contains(_embeddedApiServer.HttpsPort.ToString()))
                {
                    _logger.LogInformation("🔄 EmbeddedApiServer uruchomiony - aktualizuję konfigurację API");
                    _cachedConfig = CreateEmbeddedServerConfiguration();
                    await SaveApiConfigurationAsync(_cachedConfig);
                }
                return _cachedConfig;
            }

            try
            {
                _cachedConfig = await _configManager.GetConfigurationAsync<ApiConfiguration>("api");
                
                if (_cachedConfig == null || !_cachedConfig.IsValid())
                {
                    _logger.LogInformation("Brak konfiguracji API - tworzę automatyczną");
                    _cachedConfig = await AutoDetectApiConfigurationAsync();
                    await SaveApiConfigurationAsync(_cachedConfig);
                }

                return _cachedConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania konfiguracji API");
                
                // Sprawdź embedded server jako fallback
                if (_embeddedApiServer.IsRunning)
                {
                    _logger.LogInformation("Używam EmbeddedApiServer jako fallback");
                    _cachedConfig = CreateEmbeddedServerConfiguration();
                    return _cachedConfig;
                }
                
                // Ostatni fallback do konfiguracji deweloperskiej
                _cachedConfig = ApiConfiguration.CreateDevelopment();
                return _cachedConfig;
            }
        }

        public async Task SaveApiConfigurationAsync(ApiConfiguration config)
        {
            try
            {
                await _configManager.SaveConfigurationAsync("api", config);
                _cachedConfig = config;
                _logger.LogInformation("Konfiguracja API zapisana: {BaseUrl}", config.BaseUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisywania konfiguracji API");
                throw;
            }
        }

        public async Task<ApiConfiguration> AutoDetectApiConfigurationAsync()
        {
            _logger.LogInformation("Rozpoczynam automatyczne wykrywanie konfiguracji API");

            // PIERWSZY PRIORYTET: Sprawdź czy EmbeddedApiServer działa
            if (_embeddedApiServer.IsRunning)
            {
                var embeddedUrl = _embeddedApiServer.BaseUrl;
                _logger.LogInformation("🎯 EmbeddedApiServer działa na: {Url}", embeddedUrl);
                
                if (await TestApiConnectionAsync(embeddedUrl))
                {
                    _logger.LogInformation("✅ EmbeddedApiServer odpowiada - używam go");
                    return CreateEmbeddedServerConfiguration();
                }
                else
                {
                    _logger.LogWarning("⚠️ EmbeddedApiServer działa ale nie odpowiada na health check");
                }
            }
            else
            {
                _logger.LogDebug("EmbeddedApiServer nie działa - sprawdzam zewnętrzne API");
            }

            // Lista możliwych URL-i do sprawdzenia (w kolejności priorytetowej)
            var candidateUrls = new[]
            {
                "https://localhost:7037",      // Development HTTPS
                "http://localhost:5182",       // Development HTTP
                "https://api.teamsmanager.edu.pl", // Production
                "https://teamsmanager-api.azurewebsites.net", // Azure
                "https://api-staging.teamsmanager.edu.pl"     // Staging
            };

            foreach (var url in candidateUrls)
            {
                _logger.LogDebug("Testuję połączenie z: {Url}", url);
                
                if (await TestApiConnectionAsync(url))
                {
                    _logger.LogInformation("✅ Znaleziono działające API: {Url}", url);
                    var config = ApiConfiguration.CreateAutoDetect(url);
                    return config;
                }
            }

            // Jeśli nic nie działa, sprawdź czy localhost jest dostępny
            if (await IsLocalhostPortAvailableAsync(7037))
            {
                _logger.LogWarning("Port 7037 jest dostępny, ale API nie odpowiada - używam konfiguracji deweloperskiej");
                return ApiConfiguration.CreateDevelopment();
            }

            _logger.LogWarning("Nie znaleziono działającego API - używam konfiguracji produkcyjnej jako fallback");
            return ApiConfiguration.CreateProduction();
        }

        public async Task<bool> TestApiConnectionAsync(string baseUrl)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                
                // Testujemy endpoint health check lub swagger
                var testUrls = new[]
                {
                    $"{baseUrl.TrimEnd('/')}/health",
                    $"{baseUrl.TrimEnd('/')}/swagger/index.html",
                    $"{baseUrl.TrimEnd('/')}/api/diagnostics/status"
                };

                foreach (var testUrl in testUrls)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(testUrl, cts.Token);
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogDebug("✅ API odpowiada na: {TestUrl}", testUrl);
                            return true;
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.LogDebug("⏱️ Timeout dla: {TestUrl}", testUrl);
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogDebug("❌ Błąd HTTP dla {TestUrl}: {Error}", testUrl, ex.Message);
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("❌ Błąd podczas testowania {BaseUrl}: {Error}", baseUrl, ex.Message);
                return false;
            }
        }

        public async Task<bool> IsApiAvailableAsync()
        {
            var config = await GetApiConfigurationAsync();
            return await TestApiConnectionAsync(config.BaseUrl);
        }

        private async Task<bool> IsLocalhostPortAvailableAsync(int port)
        {
            try
            {
                using var tcpClient = new System.Net.Sockets.TcpClient();
                await tcpClient.ConnectAsync("127.0.0.1", port);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private ApiConfiguration CreateEmbeddedServerConfiguration()
        {
            return new ApiConfiguration
            {
                BaseUrl = _embeddedApiServer.BaseUrl,
                TimeoutSeconds = 30,
                MaxRetryAttempts = 3,
                UseHttps = true,
                ValidateSslCertificates = false, // Dev certificates
                Environment = "Embedded"
            };
        }
    }
} 