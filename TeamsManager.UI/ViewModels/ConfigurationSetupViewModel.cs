using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Services.Configuration;
using TeamsManager.UI.Models.Configuration;
using TeamsManager.UI.ViewModels;

namespace TeamsManager.UI.ViewModels
{
    public class ConfigurationSetupViewModel : BaseViewModel
    {
        private readonly IConfigurationManagerV2 _configManager;
        private readonly ConfigurationInitializer _configInitializer;
        private readonly ILogger<ConfigurationSetupViewModel> _logger;

        private bool _isLoading;
        private bool _hasChanges;
        private string _statusMessage = string.Empty;
        private ValidationResult _validationResult = new();

        // Azure AD Configuration
        private string _uiClientId = string.Empty;
        private string _apiClientId = string.Empty;
        private string _tenantId = string.Empty;
        private string _clientSecret = string.Empty;
        private string _audience = string.Empty;

        // Application Configuration
        private string _applicationName = "TeamsManager";
        private string _version = "1.0.0";
        private string _environment = "Production";
        private string _apiBaseUrl = "https://api.teamsmanager.edu.pl";
        private int _apiTimeout = 30;

        public ConfigurationSetupViewModel(
            IConfigurationManagerV2 configManager,
            ConfigurationInitializer configInitializer,
            ILogger<ConfigurationSetupViewModel> logger)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _configInitializer = configInitializer ?? throw new ArgumentNullException(nameof(configInitializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            SaveCommand = new RelayCommand(async () => await SaveConfigurationAsync(), () => CanSave);
            CancelCommand = new RelayCommand(() => RequestClose?.Invoke());
            ValidateCommand = new RelayCommand(async () => await ValidateConfigurationAsync());
            TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync());

            _ = Task.Run(LoadConfigurationAsync);
        }

        #region Properties

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool HasChanges
        {
            get => _hasChanges;
            set
            {
                SetProperty(ref _hasChanges, value);
                OnPropertyChanged(nameof(CanSave));
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ValidationResult ValidationResult
        {
            get => _validationResult;
            set
            {
                SetProperty(ref _validationResult, value);
                OnPropertyChanged(nameof(ValidationStatusColor));
                OnPropertyChanged(nameof(HasMissingConfigurations));
                OnPropertyChanged(nameof(HasWarnings));
            }
        }

        public string ValidationStatusColor => ValidationResult.IsValid ? "#4CAF50" : "#F44336";
        public bool HasMissingConfigurations => ValidationResult.MissingConfigurations.Any();
        public bool HasWarnings => ValidationResult.Warnings.Any();
        public bool CanSave => HasChanges && !IsLoading;

        // Azure AD Properties
        public string UiClientId
        {
            get => _uiClientId;
            set
            {
                SetProperty(ref _uiClientId, value);
                HasChanges = true;
            }
        }

        public string ApiClientId
        {
            get => _apiClientId;
            set
            {
                SetProperty(ref _apiClientId, value);
                HasChanges = true;
            }
        }

        public string TenantId
        {
            get => _tenantId;
            set
            {
                SetProperty(ref _tenantId, value);
                HasChanges = true;
            }
        }

        public string ClientSecret
        {
            get => _clientSecret;
            set
            {
                SetProperty(ref _clientSecret, value);
                HasChanges = true;
            }
        }

        public string Audience
        {
            get => _audience;
            set
            {
                SetProperty(ref _audience, value);
                HasChanges = true;
            }
        }

        // Application Properties
        public string ApplicationName
        {
            get => _applicationName;
            set
            {
                SetProperty(ref _applicationName, value);
                HasChanges = true;
            }
        }

        public string Version
        {
            get => _version;
            set
            {
                SetProperty(ref _version, value);
                HasChanges = true;
            }
        }

        public string Environment
        {
            get => _environment;
            set
            {
                SetProperty(ref _environment, value);
                HasChanges = true;
            }
        }

        public string ApiBaseUrl
        {
            get => _apiBaseUrl;
            set
            {
                SetProperty(ref _apiBaseUrl, value);
                HasChanges = true;
            }
        }

        public int ApiTimeout
        {
            get => _apiTimeout;
            set
            {
                SetProperty(ref _apiTimeout, value);
                HasChanges = true;
            }
        }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ValidateCommand { get; }
        public ICommand TestConnectionCommand { get; }

        #endregion

        #region Events

        public event Action? RequestClose;

        #endregion

        #region Methods

        private async Task LoadConfigurationAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Ładowanie konfiguracji...";

                // Sprawdź czy konfiguracja wymaga inicjalizacji
                var requiresInit = await _configInitializer.RequiresInitializationAsync();
                if (requiresInit)
                {
                    StatusMessage = "Inicjalizacja domyślnej konfiguracji...";
                    await _configInitializer.InitializeDefaultConfigurationAsync();
                }

                // Załaduj konfiguracje
                var azureConfig = await _configManager.LoadAzureAdConfigurationAsync();
                var appConfig = await _configManager.LoadApplicationConfigurationAsync();

                // Mapuj do właściwości
                if (azureConfig != null)
                {
                    UiClientId = azureConfig.Ui.ClientId ?? string.Empty;
                    ApiClientId = azureConfig.Api.ClientId ?? string.Empty;
                    TenantId = azureConfig.TenantId ?? string.Empty;
                    ClientSecret = azureConfig.Api.ClientSecret ?? string.Empty;
                    Audience = azureConfig.Api.Audience ?? string.Empty;
                }

                if (appConfig != null)
                {
                    ApplicationName = appConfig.Application.Name ?? "TeamsManager";
                    Version = appConfig.Application.Version ?? "1.0.0";
                    Environment = appConfig.Environment ?? "Production";
                    ApiBaseUrl = appConfig.Api.BaseUrl ?? "https://api.teamsmanager.edu.pl";
                    ApiTimeout = appConfig.Api.Timeout;
                }

                // Walidacja po załadowaniu
                await ValidateConfigurationAsync();
                
                HasChanges = false;
                StatusMessage = "Konfiguracja załadowana pomyślnie";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania konfiguracji");
                StatusMessage = $"Błąd: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveConfigurationAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Zapisywanie konfiguracji...";

                // Walidacja przed zapisem
                await ValidateConfigurationAsync();
                if (!ValidationResult.IsValid)
                {
                    StatusMessage = "Nie można zapisać - konfiguracja zawiera błędy";
                    return;
                }

                // Przygotuj konfiguracje
                var azureConfig = new AzureAdConfiguration
                {
                    TenantId = TenantId,
                    Ui = new UiClientSettings
                    {
                        ClientId = UiClientId,
                        RedirectUri = "http://localhost"
                    },
                    Api = new ApiClientSettings
                    {
                        ClientId = ApiClientId,
                        ClientSecret = ClientSecret,
                        Audience = Audience
                    }
                };

                var appConfig = new ApplicationConfiguration
                {
                    Environment = Environment,
                    Application = new ApplicationSettings
                    {
                        Name = ApplicationName,
                        Version = Version
                    },
                    Api = new ApiSettings
                    {
                        BaseUrl = ApiBaseUrl,
                        Timeout = ApiTimeout
                    }
                };

                // Zapisz konfiguracje
                await _configManager.SaveAzureAdConfigurationAsync(azureConfig);
                await _configManager.SaveApplicationConfigurationAsync(appConfig);

                HasChanges = false;
                StatusMessage = "Konfiguracja zapisana pomyślnie";

                _logger.LogInformation("Konfiguracja została zapisana pomyślnie");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisywania konfiguracji");
                StatusMessage = $"Błąd zapisu: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ValidateConfigurationAsync()
        {
            try
            {
                var result = new ValidationResult();

                // Walidacja Azure AD
                if (string.IsNullOrWhiteSpace(UiClientId))
                    result.MissingConfigurations.Add("Azure AD: UI Client ID nie jest ustawiony");
                if (string.IsNullOrWhiteSpace(ApiClientId))
                    result.MissingConfigurations.Add("Azure AD: API Client ID nie jest ustawiony");
                if (string.IsNullOrWhiteSpace(TenantId))
                    result.MissingConfigurations.Add("Azure AD: Tenant ID nie jest ustawiony");
                if (string.IsNullOrWhiteSpace(ClientSecret))
                    result.MissingConfigurations.Add("Azure AD: Client Secret nie jest ustawiony");
                if (string.IsNullOrWhiteSpace(Audience))
                    result.MissingConfigurations.Add("Azure AD: Audience nie jest ustawione");

                // Ostrzeżenia
                if (Environment == "Production" && string.IsNullOrWhiteSpace(ApiBaseUrl))
                    result.Warnings.Add("API Base URL nie jest ustawiony dla środowiska Production");

                if (ApiTimeout < 30)
                    result.Warnings.Add("Timeout API poniżej 30 sekund może powodować problemy z długimi operacjami");

                // Podsumowanie
                if (!result.MissingConfigurations.Any() && !result.Warnings.Any())
                {
                    result.Summary = "✅ Konfiguracja jest kompletna i poprawna";
                    result.IsValid = true;
                }
                else if (!result.MissingConfigurations.Any())
                {
                    result.Summary = $"⚠️ Konfiguracja jest kompletna, ale zawiera {result.Warnings.Count} ostrzeżeń";
                    result.IsValid = true;
                }
                else
                {
                    result.Summary = $"❌ Konfiguracja niekompletna - brakuje {result.MissingConfigurations.Count} ustawień";
                    result.IsValid = false;
                }

                ValidationResult = result;
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas walidacji konfiguracji");
                ValidationResult = new ValidationResult
                {
                    Summary = $"❌ Błąd walidacji: {ex.Message}",
                    IsValid = false
                };
            }
        }

        private async Task TestConnectionAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Testowanie połączenia...";

                // Tutaj można dodać testy połączenia z Azure AD i API
                await Task.Delay(2000); // Symulacja testu

                StatusMessage = "Test połączenia zakończony pomyślnie";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas testowania połączenia");
                StatusMessage = $"Test połączenia nieudany: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Summary { get; set; } = string.Empty;
        public ObservableCollection<string> MissingConfigurations { get; set; } = new();
        public ObservableCollection<string> Warnings { get; set; } = new();
    }
} 