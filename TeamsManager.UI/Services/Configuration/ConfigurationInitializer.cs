using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Models.Configuration;

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
                var appConfig = await _configManager.LoadApplicationConfigurationAsync();
                var loginConfig = await _configManager.LoadLoginSettingsAsync();
                
                return appConfig == null || loginConfig == null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd podczas sprawdzania czy wymagana jest inicjalizacja - zakładam że tak");
                return true;
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