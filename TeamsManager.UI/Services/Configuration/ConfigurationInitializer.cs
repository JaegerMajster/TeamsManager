using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Models.Configuration;
using System.Collections.Generic;

namespace TeamsManager.UI.Services.Configuration
{
    public class ConfigurationInitializer
    {
        private readonly IConfigurationManagerV2 _configManager;
        private readonly ILogger<ConfigurationInitializer> _logger;

        public ConfigurationInitializer(
            IConfigurationManagerV2 configManager,
            ILogger<ConfigurationInitializer> logger)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Rozpoczęto inicjalizację systemu konfiguracji V2.0");

                await EnsureApplicationConfigurationAsync();
                await EnsureAzureAdConfigurationAsync();
                await EnsureUserPreferencesAsync();
                await EnsureLoginSettingsAsync();
                await EnsureDatabaseConfigurationAsync();
                await EnsureFeaturesConfigurationAsync();

                _logger.LogInformation("Inicjalizacja systemu konfiguracji V2.0 zakończona pomyślnie");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas inicjalizacji systemu konfiguracji");
                throw;
            }
        }

        public async Task<bool> RequiresInitializationAsync()
        {
            try
            {
                // Sprawdź czy podstawowe pliki konfiguracyjne istnieją
                var appConfig = await SafeLoadConfigurationAsync<ApplicationConfiguration>("application");
                var azureConfig = await SafeLoadConfigurationAsync<AzureAdConfiguration>("azure-ad");
                var loginConfig = await SafeLoadConfigurationAsync<LoginSettingsConfiguration>("login-settings");
                
                // Jeśli którakolwiek z podstawowych konfiguracji nie istnieje, wymagana jest inicjalizacja
                bool requiresInit = appConfig == null || azureConfig == null || loginConfig == null;
                
                if (requiresInit)
                {
                    _logger.LogInformation("Wymagana inicjalizacja konfiguracji - brakujące pliki: " +
                        $"App: {appConfig == null}, Azure: {azureConfig == null}, Login: {loginConfig == null}");
                }
                
                return requiresInit;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd podczas sprawdzania czy wymagana jest inicjalizacja - zakładam że tak");
                return true;
            }
        }

        private async Task<T?> SafeLoadConfigurationAsync<T>(string configName) where T : class
        {
            try
            {
                return await _configManager.GetConfigurationAsync<T>(configName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nie udało się załadować konfiguracji {ConfigName} - prawdopodobnie nie istnieje", configName);
                return null;
            }
        }

        public async Task InitializeDefaultConfigurationAsync()
        {
            await InitializeAsync();
        }

        private async Task EnsureApplicationConfigurationAsync()
        {
            var config = await _configManager.LoadApplicationConfigurationAsync();
            if (config == null)
            {
                _logger.LogInformation("Tworzę domyślną konfigurację aplikacji");
                
                config = new ApplicationConfiguration
                {
                    Environment = "Production",
                    Application = new ApplicationSettings
                    {
                        Name = "TeamsManager",
                        Version = "1.0.0",
                        AutoUpdate = true,
                        TelemetryEnabled = false
                    },
                    Api = new ApiSettings
                    {
                        BaseUrl = "https://localhost:7037",
                        Timeout = 30,
                        RetryAttempts = 3,
                        HealthCheckInterval = 300
                    },
                    Security = new SecuritySettings
                    {
                        EncryptionKeyRotationDays = 90,
                        TokenCacheExpiryHours = 24,
                        RequireSecureConnection = true
                    }
                };

                await _configManager.SaveApplicationConfigurationAsync(config);
            }
        }

        private async Task EnsureAzureAdConfigurationAsync()
        {
            var config = await SafeLoadConfigurationAsync<AzureAdConfiguration>("azure-ad");
            if (config == null)
            {
                _logger.LogInformation("Tworzę pustą konfigurację Azure AD do wypełnienia przez użytkownika");
                
                config = new AzureAdConfiguration
                {
                    TenantId = string.Empty,
                    Ui = new UiClientSettings
                    {
                        ClientId = string.Empty,
                        RedirectUri = "http://localhost",
                        Scopes = new List<string>
                        {
                            "https://graph.microsoft.com/User.Read",
                            "https://graph.microsoft.com/Team.ReadBasic.All"
                        }
                    },
                    Api = new ApiClientSettings
                    {
                        ClientId = string.Empty,
                        ClientSecret = string.Empty,
                        Audience = string.Empty
                    },
                    Graph = new GraphSettings
                    {
                        BaseUrl = "https://graph.microsoft.com/v1.0",
                        Scopes = new List<string>
                        {
                            "https://graph.microsoft.com/User.Read",
                            "https://graph.microsoft.com/Team.ReadBasic.All"
                        }
                    }
                };

                await _configManager.SaveConfigurationAsync("azure-ad", config);
            }
        }

        private async Task EnsureUserPreferencesAsync()
        {
            var config = await _configManager.GetConfigurationAsync<UserPreferencesConfiguration>("preferences");
            if (config == null)
            {
                _logger.LogInformation("Tworzę domyślne preferencje użytkownika");
                
                config = new UserPreferencesConfiguration
                {
                    Ui = new UiPreferences
                    {
                        Theme = "Dark",
                        Language = "pl-PL",
                        ShowWelcomeScreen = true,
                        AutoSaveChanges = true,
                        RefreshIntervalSeconds = 30
                    },
                    Notifications = new NotificationPreferences
                    {
                        ShowDesktopNotifications = true,
                        ShowInAppNotifications = true,
                        PlaySounds = false,
                        EmailNotifications = false
                    },
                    Performance = new PerformancePreferences
                    {
                        EnableCaching = true,
                        CacheExpiryMinutes = 15,
                        PreloadData = true,
                        MaxConcurrentOperations = 5
                    }
                };

                await _configManager.SaveConfigurationAsync("preferences", config);
            }
        }

        private async Task EnsureLoginSettingsAsync()
        {
            var config = await _configManager.LoadLoginSettingsAsync();
            if (config == null)
            {
                _logger.LogInformation("Tworzę domyślne ustawienia logowania");
                
                config = new LoginSettingsConfiguration
                {
                    RememberMe = true,
                    AutoLogin = true,
                    UseWindowsHello = true,
                    UseBroker = true,
                    SessionTimeoutMinutes = 480
                };

                await _configManager.SaveLoginSettingsAsync(config);
            }
        }

        private async Task EnsureDatabaseConfigurationAsync()
        {
            var config = await _configManager.GetConfigurationAsync<DatabaseConfiguration>("database");
            if (config == null)
            {
                _logger.LogInformation("Tworzę domyślną konfigurację bazy danych");
                
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dbPath = System.IO.Path.Combine(appDataPath, "TeamsManager", "data", "teamsmanager.db");
                
                config = new DatabaseConfiguration
                {
                    Provider = "SQLite",
                    ConnectionString = $"Data Source={dbPath}",
                    Migrations = new MigrationSettings
                    {
                        AutoApply = true,
                        BackupBeforeMigration = true
                    },
                    Performance = new PerformanceSettings
                    {
                        ConnectionPoolSize = 10,
                        CommandTimeout = 30
                    }
                };

                await _configManager.SaveConfigurationAsync("database", config);
            }
        }

        private async Task EnsureFeaturesConfigurationAsync()
        {
            var config = await _configManager.GetConfigurationAsync<FeaturesConfiguration>("features");
            if (config == null)
            {
                _logger.LogInformation("Tworzę domyślną konfigurację funkcji");
                
                config = new FeaturesConfiguration
                {
                    Features = new FeatureFlags
                    {
                        BulkOperations = true,
                        AdvancedReporting = true,
                        RealTimeSync = false,
                        BetaFeatures = false,
                        Telemetry = false
                    },
                    Limits = new LimitSettings
                    {
                        MaxBulkOperations = 1000,
                        MaxConcurrentRequests = 10,
                        MaxLogFileSizeMB = 100
                    }
                };

                await _configManager.SaveConfigurationAsync("features", config);
            }
        }
    }
} 