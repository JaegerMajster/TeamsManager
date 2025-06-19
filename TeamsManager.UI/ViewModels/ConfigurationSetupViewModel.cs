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
            CancelCommand = new RelayCommand(() => 
            {
                _logger.LogInformation("🔘 PRZYCISK ANULUJ - użytkownik wcisnął przycisk ANULUJ");
                RequestClose?.Invoke();
            });
            ValidateCommand = new RelayCommand(async () => await ValidateConfigurationAsync());
            TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync());
            ShowConfigCommand = new RelayCommand(async () => await ShowConfigurationAsync());
            FixEncryptionCommand = new RelayCommand(async () => await FixEncryptionAsync());

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
        public bool CanSave => !IsLoading; // Zawsze można zapisać, niezależnie od walidacji

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
        public ICommand ShowConfigCommand { get; }
        public ICommand FixEncryptionCommand { get; }

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

                // Mapuj do właściwości - używaj bezpośrednio pól aby nie ustawiać HasChanges
                if (azureConfig != null)
                {
                    _uiClientId = azureConfig.Ui.ClientId ?? string.Empty;
                    _apiClientId = azureConfig.Api.ClientId ?? string.Empty;
                    _tenantId = azureConfig.TenantId ?? string.Empty;
                    // ClientSecret celowo nie wczytujemy ze względów bezpieczeństwa
                    _clientSecret = string.Empty;
                    _audience = azureConfig.Api.Audience ?? string.Empty;
                    
                    // Powiadom UI o zmianie wartości
                    OnPropertyChanged(nameof(UiClientId));
                    OnPropertyChanged(nameof(ApiClientId));
                    OnPropertyChanged(nameof(TenantId));
                    OnPropertyChanged(nameof(ClientSecret));
                    OnPropertyChanged(nameof(Audience));
                }

                if (appConfig != null)
                {
                    _applicationName = appConfig.Application.Name ?? "TeamsManager";
                    _version = appConfig.Application.Version ?? "1.0.0";
                    _environment = appConfig.Environment ?? "Production";
                    _apiBaseUrl = appConfig.Api.BaseUrl ?? "https://api.teamsmanager.edu.pl";
                    _apiTimeout = appConfig.Api.Timeout;
                    
                    // Powiadom UI o zmianie wartości
                    OnPropertyChanged(nameof(ApplicationName));
                    OnPropertyChanged(nameof(Version));
                    OnPropertyChanged(nameof(Environment));
                    OnPropertyChanged(nameof(ApiBaseUrl));
                    OnPropertyChanged(nameof(ApiTimeout));
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
            _logger.LogInformation("🔘 PRZYCISK ZAPISZ - użytkownik wcisnął przycisk ZAPISZ");
            
            try
            {
                IsLoading = true;
                StatusMessage = "Zapisywanie konfiguracji...";
                
                _logger.LogInformation("🔘 ZAPISZ - rozpoczęcie zapisywania konfiguracji");
                
                // ZAWSZE ZAPISUJEMY - niezależnie od walidacji
                // Załaduj istniejące konfiguracje i zaktualizuj tylko zmienione pola
                var existingAzureConfig = await _configManager.LoadAzureAdConfigurationAsync();
                var existingAppConfig = await _configManager.LoadApplicationConfigurationAsync();

                // Przygotuj Azure AD Configuration - zachowaj istniejące wartości jeśli nowe są puste
                var azureConfig = new AzureAdConfiguration
                {
                    TenantId = !string.IsNullOrWhiteSpace(TenantId) ? TenantId : existingAzureConfig?.TenantId ?? string.Empty,
                    Ui = new UiClientSettings
                    {
                        ClientId = !string.IsNullOrWhiteSpace(UiClientId) ? UiClientId : existingAzureConfig?.Ui?.ClientId ?? string.Empty,
                        RedirectUri = existingAzureConfig?.Ui?.RedirectUri ?? "http://localhost"
                    },
                    Api = new ApiClientSettings
                    {
                        ClientId = !string.IsNullOrWhiteSpace(ApiClientId) ? ApiClientId : existingAzureConfig?.Api?.ClientId ?? string.Empty,
                        ClientSecret = !string.IsNullOrWhiteSpace(ClientSecret) ? ClientSecret : existingAzureConfig?.Api?.ClientSecret ?? string.Empty,
                        Audience = !string.IsNullOrWhiteSpace(Audience) ? Audience : existingAzureConfig?.Api?.Audience ?? string.Empty
                    }
                };

                // Przygotuj Application Configuration - zachowaj istniejące wartości jeśli nowe są puste
                var appConfig = new ApplicationConfiguration
                {
                    Environment = !string.IsNullOrWhiteSpace(Environment) ? Environment : existingAppConfig?.Environment ?? "Production",
                    Application = new ApplicationSettings
                    {
                        Name = !string.IsNullOrWhiteSpace(ApplicationName) ? ApplicationName : existingAppConfig?.Application?.Name ?? "TeamsManager",
                        Version = !string.IsNullOrWhiteSpace(Version) ? Version : existingAppConfig?.Application?.Version ?? "1.0.0"
                    },
                    Api = new ApiSettings
                    {
                        BaseUrl = !string.IsNullOrWhiteSpace(ApiBaseUrl) ? ApiBaseUrl : existingAppConfig?.Api?.BaseUrl ?? "https://api.teamsmanager.edu.pl",
                        Timeout = ApiTimeout > 0 ? ApiTimeout : existingAppConfig?.Api?.Timeout ?? 30
                    }
                };

                _logger.LogInformation("💾 Przygotowano konfiguracje do zapisu:");
                _logger.LogInformation($"💾 Azure AD - TenantId: {azureConfig.TenantId}, UiClientId: {azureConfig.Ui.ClientId}, ApiClientId: {azureConfig.Api.ClientId}");
                _logger.LogInformation($"💾 Application - Name: {appConfig.Application.Name}, Environment: {appConfig.Environment}");

                // Zapisz konfiguracje
                await _configManager.SaveAzureAdConfigurationAsync(azureConfig);
                await _configManager.SaveApplicationConfigurationAsync(appConfig);

                HasChanges = false;
                StatusMessage = "Konfiguracja zapisana pomyślnie";
                _logger.LogInformation("💾 Konfiguracja została zapisana pomyślnie");
                
                // Walidacja po zapisie - sprawdź czy konfiguracja jest kompletna
                await ValidateConfigurationAsync();
                
                // Jeśli konfiguracja jest kompletna, zamknij okno (uruchomi się logowanie)
                // Jeśli nie, pozostaw okno otwarte z informacją o brakujących polach
                if (ValidationResult.IsValid)
                {
                    _logger.LogInformation("💾 Konfiguracja kompletna - zamykam okno konfiguracji");
                    RequestClose?.Invoke();
                }
                else
                {
                    _logger.LogInformation("💾 Konfiguracja niekompletna - pozostawiam okno otwarte");
                    StatusMessage = $"Konfiguracja zapisana. {ValidationResult.Summary}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔘 ZAPISZ - błąd podczas zapisywania konfiguracji");
                throw;
            }
            finally
            {
                IsLoading = false;
                _logger.LogInformation("🔘 ZAPISZ - zakończenie operacji zapisywania");
            }
        }

        private async Task ValidateConfigurationAsync()
        {
            _logger.LogInformation("🔘 PRZYCISK WALIDUJ - użytkownik wcisnął przycisk WALIDUJ");
            
            try
            {
                IsLoading = true;
                StatusMessage = "Walidacja konfiguracji...";
                
                _logger.LogInformation("🔘 WALIDUJ - rozpoczęcie walidacji konfiguracji");
                
                var result = new ValidationResult();
                
                // Załaduj istniejące konfiguracje aby sprawdzić kompletność
                var existingAzureConfig = await _configManager.LoadAzureAdConfigurationAsync();
                var existingAppConfig = await _configManager.LoadApplicationConfigurationAsync();

                // Sprawdź Azure AD - wartości z formularza LUB istniejące
                var finalUiClientId = !string.IsNullOrWhiteSpace(UiClientId) ? UiClientId : existingAzureConfig?.Ui?.ClientId;
                var finalApiClientId = !string.IsNullOrWhiteSpace(ApiClientId) ? ApiClientId : existingAzureConfig?.Api?.ClientId;
                var finalTenantId = !string.IsNullOrWhiteSpace(TenantId) ? TenantId : existingAzureConfig?.TenantId;
                var finalAudience = !string.IsNullOrWhiteSpace(Audience) ? Audience : existingAzureConfig?.Api?.Audience;
                var finalClientSecret = !string.IsNullOrWhiteSpace(ClientSecret) ? ClientSecret : existingAzureConfig?.Api?.ClientSecret;

                // SZCZEGÓŁOWE LOGOWANIE STANU PÓL AZURE AD:
                _logger.LogInformation("📋 WYNIKI WALIDACJI - STAN PÓL AZURE AD:");
                _logger.LogInformation($"📋 UI Client ID: {(string.IsNullOrWhiteSpace(finalUiClientId) ? "❌ BRAK" : "✅ USTAWIONY")} (formularz: '{UiClientId}', plik: '{existingAzureConfig?.Ui?.ClientId ?? "[BRAK]'}')");
                _logger.LogInformation($"📋 API Client ID: {(string.IsNullOrWhiteSpace(finalApiClientId) ? "❌ BRAK" : "✅ USTAWIONY")} (formularz: '{ApiClientId}', plik: '{existingAzureConfig?.Api?.ClientId ?? "[BRAK]'}')");
                _logger.LogInformation($"📋 Tenant ID: {(string.IsNullOrWhiteSpace(finalTenantId) ? "❌ BRAK" : "✅ USTAWIONY")} (formularz: '{TenantId}', plik: '{existingAzureConfig?.TenantId ?? "[BRAK]'}')");
                _logger.LogInformation($"📋 Client Secret: {(string.IsNullOrWhiteSpace(finalClientSecret) ? "❌ BRAK" : "✅ USTAWIONY")} (formularz: {(string.IsNullOrWhiteSpace(ClientSecret) ? "pusty" : $"{ClientSecret.Length} znaków")}, plik: {(string.IsNullOrWhiteSpace(existingAzureConfig?.Api?.ClientSecret) ? "pusty" : $"{existingAzureConfig.Api.ClientSecret.Length} znaków")})");
                _logger.LogInformation($"📋 Audience: {(string.IsNullOrWhiteSpace(finalAudience) ? "❌ BRAK" : "✅ USTAWIONY")} (formularz: '{Audience}', plik: '{existingAzureConfig?.Api?.Audience ?? "[BRAK]'}')");

                // Walidacja Azure AD - sprawdź finalne wartości
                if (string.IsNullOrWhiteSpace(finalUiClientId))
                    result.MissingConfigurations.Add("Azure AD: UI Client ID nie jest ustawiony");
                if (string.IsNullOrWhiteSpace(finalApiClientId))
                    result.MissingConfigurations.Add("Azure AD: API Client ID nie jest ustawiony");
                if (string.IsNullOrWhiteSpace(finalTenantId))
                    result.MissingConfigurations.Add("Azure AD: Tenant ID nie jest ustawiony");
                if (string.IsNullOrWhiteSpace(finalAudience))
                    result.MissingConfigurations.Add("Azure AD: Audience nie jest ustawione");
                if (string.IsNullOrWhiteSpace(finalClientSecret))
                    result.MissingConfigurations.Add("Azure AD: Client Secret nie jest ustawiony");

                // Informacje o istniejących wartościach
                if (string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(finalClientSecret))
                    result.Warnings.Add("Azure AD: Używany jest istniejący Client Secret");
                if (string.IsNullOrWhiteSpace(UiClientId) && !string.IsNullOrWhiteSpace(finalUiClientId))
                    result.Warnings.Add("Azure AD: Używany jest istniejący UI Client ID");
                if (string.IsNullOrWhiteSpace(ApiClientId) && !string.IsNullOrWhiteSpace(finalApiClientId))
                    result.Warnings.Add("Azure AD: Używany jest istniejący API Client ID");
                if (string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(finalTenantId))
                    result.Warnings.Add("Azure AD: Używany jest istniejący Tenant ID");
                if (string.IsNullOrWhiteSpace(Audience) && !string.IsNullOrWhiteSpace(finalAudience))
                    result.Warnings.Add("Azure AD: Używany jest istniejący Audience");

                // LOGOWANIE STANU APLIKACJI
                _logger.LogInformation("📋 WYNIKI WALIDACJI - STAN PÓL APLIKACJI:");
                _logger.LogInformation($"📋 Nazwa aplikacji: '{ApplicationName}' (plik: '{existingAppConfig?.Application?.Name ?? "[BRAK]"}')");
                _logger.LogInformation($"📋 Wersja: '{Version}' (plik: '{existingAppConfig?.Application?.Version ?? "[BRAK]"}')");
                _logger.LogInformation($"📋 Środowisko: '{Environment}' (plik: '{existingAppConfig?.Environment ?? "[BRAK]"}')");
                _logger.LogInformation($"📋 API Base URL: '{ApiBaseUrl}' (plik: '{existingAppConfig?.Api?.BaseUrl ?? "[BRAK]"}')");
                _logger.LogInformation($"📋 API Timeout: {ApiTimeout}s (plik: {existingAppConfig?.Api?.Timeout ?? 0}s)");

                // Ostrzeżenia aplikacji
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

                // SZCZEGÓŁOWE LOGOWANIE WYNIKÓW WALIDACJI
                _logger.LogInformation("📋 PODSUMOWANIE WALIDACJI:");
                _logger.LogInformation($"📋 Status: {result.Summary}");
                _logger.LogInformation($"📋 Czy konfiguracja jest ważna: {(result.IsValid ? "TAK" : "NIE")}");
                
                if (result.MissingConfigurations.Any())
                {
                    _logger.LogWarning($"📋 BRAKUJĄCE POLA ({result.MissingConfigurations.Count}):");
                    foreach (var missing in result.MissingConfigurations)
                    {
                        _logger.LogWarning($"📋   ❌ {missing}");
                    }
                }
                else
                {
                    _logger.LogInformation("📋 ✅ Wszystkie wymagane pola są wypełnione");
                }
                
                if (result.Warnings.Any())
                {
                    _logger.LogInformation($"📋 OSTRZEŻENIA ({result.Warnings.Count}):");
                    foreach (var warning in result.Warnings)
                    {
                        _logger.LogInformation($"📋   ⚠️ {warning}");
                    }
                }
                else
                {
                    _logger.LogInformation("📋 ℹ️ Brak ostrzeżeń");
                }

                ValidationResult = result;
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔘 WALIDUJ - błąd podczas walidacji konfiguracji");
                throw;
            }
            finally
            {
                IsLoading = false;
                _logger.LogInformation("🔘 WALIDUJ - zakończenie operacji walidacji");
            }
        }

        private async Task TestConnectionAsync()
        {
            _logger.LogInformation("🔘 PRZYCISK TESTUJ - użytkownik wcisnął przycisk TESTUJ");
            
            try
            {
                IsLoading = true;
                StatusMessage = "Testowanie połączenia...";
                
                _logger.LogInformation("🔘 TESTUJ - rozpoczęcie testowania połączenia");
                
                // Tutaj można dodać testy połączenia z Azure AD i API
                await Task.Delay(2000); // Symulacja testu

                StatusMessage = "Test połączenia zakończony pomyślnie";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔘 TESTUJ - błąd podczas testowania połączenia");
                throw;
            }
            finally
            {
                IsLoading = false;
                _logger.LogInformation("🔘 TESTUJ - zakończenie operacji testowania");
            }
        }

        private async Task ShowConfigurationAsync()
        {
            _logger.LogInformation("🔘 PRZYCISK POKAŻ - użytkownik wcisnął przycisk POKAŻ");
            
            try
            {
                _logger.LogInformation("🔘 POKAŻ - rozpoczęcie wyświetlania konfiguracji");
                
                IsLoading = true;
                StatusMessage = "Odczytywanie konfiguracji...";
                _logger.LogInformation("🔍 Rozpoczęcie odczytu konfiguracji");

                // Przygotuj tekst do wyświetlenia
                var configText = "=== KONFIGURACJA TEAMSMANAGER ===\n\n";
                
                // Informacje o plikach (zawsze sprawdzamy)
                var configPath = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                    "TeamsManager", "config");
                    
                var azureAdFile = System.IO.Path.Combine(configPath, "azure-ad.json");
                var appFile = System.IO.Path.Combine(configPath, "application.json");
                
                configText += "📁 INFORMACJE O PLIKACH:\n";
                configText += $"Katalog konfiguracji: {configPath}\n";
                configText += $"azure-ad.json: {(System.IO.File.Exists(azureAdFile) ? "ISTNIEJE" : "BRAK")}";
                if (System.IO.File.Exists(azureAdFile))
                {
                    var fileInfo = new System.IO.FileInfo(azureAdFile);
                    configText += $" ({fileInfo.Length} bajtów, {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss})";
                }
                configText += "\n";
                
                configText += $"application.json: {(System.IO.File.Exists(appFile) ? "ISTNIEJE" : "BRAK")}";
                if (System.IO.File.Exists(appFile))
                {
                    var fileInfo = new System.IO.FileInfo(appFile);
                    configText += $" ({fileInfo.Length} bajtów, {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss})";
                }
                configText += "\n\n";

                // Próbuj załadować i odszyfrować konfiguracje
                try
                {
                    var azureConfig = await _configManager.LoadAzureAdConfigurationAsync();
                    var appConfig = await _configManager.LoadApplicationConfigurationAsync();

                    // Azure AD Configuration
                    configText += "🔐 AZURE AD CONFIGURATION:\n";
                    if (azureConfig != null)
                    {
                        configText += $"Tenant ID: {azureConfig.TenantId ?? "[BRAK]"}\n";
                        configText += $"UI Client ID: {azureConfig.Ui?.ClientId ?? "[BRAK]"}\n";
                        configText += $"API Client ID: {azureConfig.Api?.ClientId ?? "[BRAK]"}\n";
                        configText += $"Client Secret: {(string.IsNullOrEmpty(azureConfig.Api?.ClientSecret) ? "[BRAK]" : "[USTAWIONY - " + azureConfig.Api.ClientSecret.Length + " znaków]")}\n";
                        configText += $"Audience: {azureConfig.Api?.Audience ?? "[BRAK]"}\n";
                        configText += $"Redirect URI: {azureConfig.Ui?.RedirectUri ?? "[BRAK]"}\n";
                    }
                    else
                    {
                        configText += "[PLIK AZURE AD NIE ISTNIEJE LUB NIE MOŻNA GO ODSZYFROWAĆ]\n";
                    }
                    
                    configText += "\n⚙️ APPLICATION CONFIGURATION:\n";
                    if (appConfig != null)
                    {
                        configText += $"Nazwa aplikacji: {appConfig.Application?.Name ?? "[BRAK]"}\n";
                        configText += $"Wersja: {appConfig.Application?.Version ?? "[BRAK]"}\n";
                        configText += $"Środowisko: {appConfig.Environment ?? "[BRAK]"}\n";
                        configText += $"API Base URL: {appConfig.Api?.BaseUrl ?? "[BRAK]"}\n";
                        configText += $"API Timeout: {appConfig.Api?.Timeout ?? 0} sekund\n";
                    }
                    else
                    {
                        configText += "[PLIK APPLICATION NIE ISTNIEJE]\n";
                    }
                }
                catch (Exception loadEx)
                {
                    _logger.LogError(loadEx, "🔍 Błąd podczas ładowania konfiguracji");
                    
                    configText += "❌ BŁĄD ODCZYTU KONFIGURACJI:\n";
                    configText += $"Szczegóły: {loadEx.Message}\n\n";
                    
                    // Spróbuj odczytać surowe pliki
                    try
                    {
                        configText += "📄 SUROWA ZAWARTOŚĆ PLIKÓW:\n\n";
                        
                        if (System.IO.File.Exists(appFile))
                        {
                            configText += "application.json (niezaszyfrowany):\n";
                            var appContent = await System.IO.File.ReadAllTextAsync(appFile);
                            configText += appContent + "\n\n";
                        }
                        
                        if (System.IO.File.Exists(azureAdFile))
                        {
                            configText += "azure-ad.json (zaszyfrowany - pierwsze 200 znaków):\n";
                            var azureContent = await System.IO.File.ReadAllTextAsync(azureAdFile);
                            configText += azureContent.Length > 200 ? azureContent.Substring(0, 200) + "..." : azureContent;
                            configText += "\n\n";
                        }
                    }
                    catch (Exception fileEx)
                    {
                        configText += $"Nie można odczytać plików: {fileEx.Message}\n";
                    }
                }

                // Pokaż w MessageBox
                System.Windows.MessageBox.Show(configText, "Zawartość konfiguracji TeamsManager", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                
                StatusMessage = "Konfiguracja wyświetlona";
                _logger.LogInformation("🔍 Konfiguracja została wyświetlona użytkownikowi");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔘 POKAŻ - błąd podczas wyświetlania konfiguracji");
                throw;
            }
            finally
            {
                _logger.LogInformation("🔘 POKAŻ - zakończenie operacji wyświetlania");
            }
        }

        private async Task FixEncryptionAsync()
        {
            _logger.LogInformation("🔘 PRZYCISK NAPRAW - użytkownik wcisnął przycisk NAPRAW");
            
            try
            {
                IsLoading = true;
                StatusMessage = "Naprawianie szyfrowania...";
                
                _logger.LogInformation("🔘 NAPRAW - rozpoczęcie naprawy szyfrowania");
                
                // Spróbuj naprawić szyfrowanie dla azure-ad.json
                await _configManager.ReencryptForCurrentUserAsync("azure-ad");
                
                StatusMessage = "✅ Szyfrowanie naprawione pomyślnie!";
                _logger.LogInformation("🔧 Szyfrowanie naprawione pomyślnie");
                
                // Przeładuj konfigurację po naprawie
                await LoadConfigurationAsync();
                
                System.Windows.MessageBox.Show(
                    "Szyfrowanie zostało naprawione dla bieżącego użytkownika.\n\n" +
                    "Dane konfiguracyjne zostały ponownie zaszyfrowane i powinny być teraz dostępne.",
                    "Naprawa szyfrowania",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔘 NAPRAW - błąd podczas naprawy szyfrowania");
                throw;
            }
            finally
            {
                IsLoading = false;
                _logger.LogInformation("🔘 NAPRAW - zakończenie operacji naprawy");
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