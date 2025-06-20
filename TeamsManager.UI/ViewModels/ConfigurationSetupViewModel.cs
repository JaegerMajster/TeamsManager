using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Services.Configuration;
using TeamsManager.Core.Models.Configuration;
using TeamsManager.UI.ViewModels;
using System.Collections.Generic;

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

            SaveCommand = new AsyncRelayCommand(SaveConfigurationAsync, _ => CanSave);
            CancelCommand = new RelayCommand(() => 
            {
                _logger.LogInformation("🔘 PRZYCISK ANULUJ - użytkownik wcisnął przycisk ANULUJ");
                RequestClose?.Invoke();
            });
            ValidateCommand = new AsyncRelayCommand(ValidateConfigurationAsync);
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
            ShowConfigCommand = new AsyncRelayCommand(ShowConfigurationAsync);

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
                    _clientSecret = azureConfig.Api.ClientSecret ?? string.Empty;
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
                    _applicationName = appConfig.ApplicationName ?? "TeamsManager";
                    _version = appConfig.ApplicationVersion ?? "2.0";
                    _environment = appConfig.Environment ?? "Production";
                    // Core nie ma zagnieżdżonych Api settings - używamy wartości domyślnych
                    _apiBaseUrl = "https://api.teamsmanager.edu.pl";
                    _apiTimeout = 30;
                    
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
            _logger.LogInformation("Użytkownik wcisnął przycisk ZAPISZ");
            
            try
            {
                IsLoading = true;
                StatusMessage = "Zapisywanie konfiguracji...";
                
                _logger.LogInformation("Rozpoczęcie zapisywania konfiguracji");
                
                // ZAPISUJEMY TYLKO NIEPUSTE POLA Z FORMULARZA
                _logger.LogInformation("Analiza pól formularza...");

                // Sprawdź które pola Azure AD są wypełnione
                bool hasAzureAdData = !string.IsNullOrWhiteSpace(UiClientId) || 
                                     !string.IsNullOrWhiteSpace(ApiClientId) || 
                                     !string.IsNullOrWhiteSpace(TenantId) || 
                                     !string.IsNullOrWhiteSpace(ClientSecret) || 
                                     !string.IsNullOrWhiteSpace(Audience);

                AzureAdConfiguration? azureConfig = null;
                if (hasAzureAdData)
                {
                    _logger.LogInformation("Znaleziono wypełnione pola Azure AD - tworzę konfigurację");
                    
                    azureConfig = new AzureAdConfiguration();
                    
                    // Zapisz tylko niepuste pola
                    if (!string.IsNullOrWhiteSpace(TenantId))
                    {
                        azureConfig.TenantId = TenantId;
                        _logger.LogInformation("Ustawiono TenantId");
                    }
                    
                    // UI Client Settings - tylko jeśli UiClientId jest wypełnione
                    if (!string.IsNullOrWhiteSpace(UiClientId))
                    {
                        azureConfig.Ui = new UiClientSettings
                        {
                            ClientId = UiClientId
                        };
                        _logger.LogInformation("Ustawiono UI ClientId");
                    }
                    
                    // API Client Settings - tylko jeśli któreś z pól API jest wypełnione
                    if (!string.IsNullOrWhiteSpace(ApiClientId) || !string.IsNullOrWhiteSpace(ClientSecret) || !string.IsNullOrWhiteSpace(Audience))
                    {
                        azureConfig.Api = new ApiClientSettings();
                        
                        if (!string.IsNullOrWhiteSpace(ApiClientId))
                        {
                            azureConfig.Api.ClientId = ApiClientId;
                            _logger.LogInformation("Ustawiono API ClientId");
                        }
                        
                        if (!string.IsNullOrWhiteSpace(ClientSecret))
                        {
                            azureConfig.Api.ClientSecret = ClientSecret;
                            _logger.LogInformation("Ustawiono ClientSecret");
                        }
                        
                        if (!string.IsNullOrWhiteSpace(Audience))
                        {
                            azureConfig.Api.Audience = Audience;
                            _logger.LogInformation("Ustawiono Audience");
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("Wszystkie pola Azure AD są puste - pomijam zapis Azure AD Configuration");
                }

                // Sprawdź które pola Application są wypełnione (nie domyślne)
                bool hasAppData = (!string.IsNullOrWhiteSpace(ApplicationName) && ApplicationName != "TeamsManager") ||
                                  (!string.IsNullOrWhiteSpace(Version) && Version != "2.0") ||
                                  (!string.IsNullOrWhiteSpace(Environment) && Environment != "Production");

                ApplicationConfiguration? appConfig = null;
                if (hasAppData)
                {
                    _logger.LogInformation("Znaleziono wypełnione pola Application - tworzę konfigurację");
                    
                    appConfig = new ApplicationConfiguration();
                    
                    // Zapisz tylko pola różne od domyślnych
                    if (!string.IsNullOrWhiteSpace(Environment) && Environment != "Production")
                    {
                        appConfig.Environment = Environment;
                        _logger.LogInformation("Ustawiono Environment (różne od domyślnego)");
                    }
                    
                    // Application Settings - ustaw bezpośrednio na właściwościach
                    if (!string.IsNullOrWhiteSpace(ApplicationName) && ApplicationName != "TeamsManager")
                    {
                        appConfig.ApplicationName = ApplicationName;
                        _logger.LogInformation("Ustawiono Application Name (różne od domyślnego)");
                    }
                    
                    if (!string.IsNullOrWhiteSpace(Version) && Version != "2.0")
                    {
                        appConfig.ApplicationVersion = Version;
                        _logger.LogInformation("Ustawiono Version (różne od domyślnego)");
                    }
                    
                    // Note: Core nie ma zagnieżdżonych Api settings - używa ConnectionStrings
                    _logger.LogInformation("API settings będą zarządzane przez ConnectionStrings w Core");
                }
                else
                {
                    _logger.LogInformation("Wszystkie pola Application mają wartości domyślne - pomijam zapis Application Configuration");
                }

                // Zapisz konfiguracje tylko jeśli mają dane
                if (azureConfig != null)
                {
                    _logger.LogInformation("Zapisywanie Azure AD Configuration");
                    await _configManager.SaveAzureAdConfigurationAsync(azureConfig);
                    _logger.LogInformation("Azure AD Configuration zapisana pomyślnie");
                }
                else
                {
                    _logger.LogInformation("Pomijam zapis Azure AD Configuration - brak danych do zapisu");
                }
                
                if (appConfig != null)
                {
                    _logger.LogInformation("Zapisywanie Application Configuration");
                    await _configManager.SaveApplicationConfigurationAsync(appConfig);
                    _logger.LogInformation("Application Configuration zapisana pomyślnie");
                }
                else
                {
                    _logger.LogInformation("Pomijam zapis Application Configuration - brak danych do zapisu");
                }

                HasChanges = false;
                
                // Ustal komunikat o statusie na podstawie tego co zostało zapisane
                if (azureConfig != null && appConfig != null)
                {
                    StatusMessage = "Konfiguracja zapisana pomyślnie";
                    _logger.LogInformation("Wszystkie konfiguracje zostały zapisane pomyślnie");
                }
                else if (azureConfig != null)
                {
                    StatusMessage = "Konfiguracja Azure AD zapisana pomyślnie";
                    _logger.LogInformation("Konfiguracja Azure AD zapisana pomyślnie");
                }
                else if (appConfig != null)
                {
                    StatusMessage = "Konfiguracja aplikacji zapisana pomyślnie";
                    _logger.LogInformation("Konfiguracja aplikacji zapisana pomyślnie");
                }
                else
                {
                    StatusMessage = "Brak danych do zapisu - wszystkie pola są puste";
                    _logger.LogInformation("Brak danych do zapisu - wszystkie pola są puste lub mają wartości domyślne");
                }
                
                // Walidacja po zapisie
                await ValidateConfigurationAsync();
                
                // Sprawdź czy konfiguracja jest teraz kompletna
                if (ValidationResult.IsValid)
                {
                    _logger.LogInformation("✅ Konfiguracja jest kompletna - zamykam okno i kontynuuję do głównej aplikacji");
                    
                    // Opóźnienie żeby użytkownik zobaczył komunikat o sukcesie
                    await Task.Delay(1000);
                    
                    // Zamknij okno konfiguracji
                    RequestClose?.Invoke();
                }
                else
                {
                    _logger.LogInformation("⚠️ Konfiguracja nadal niekompletna - pozostawiam okno otwarte");
                    StatusMessage = "Konfiguracja zapisana, ale nadal niekompletna - uzupełnij brakujące pola";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisywania konfiguracji");
                StatusMessage = $"Błąd podczas zapisywania: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ValidateConfigurationAsync()
        {
            _logger.LogInformation("Użytkownik wcisnął przycisk WALIDUJ");
            
            try
            {
                IsLoading = true;
                StatusMessage = "Walidacja konfiguracji...";
                
                _logger.LogInformation("Rozpoczęcie walidacji konfiguracji");
                
                var result = new ValidationResult();
                
                // Walidacja danych z formularza
                _logger.LogInformation("Sprawdzanie wypełnienia pól formularza");

                // Walidacja Azure AD - sprawdź tylko dane z formularza
                if (string.IsNullOrWhiteSpace(UiClientId))
                    result.MissingConfigurations.Add("Azure AD: UI Client ID nie jest ustawiony");
                if (string.IsNullOrWhiteSpace(ApiClientId))
                    result.MissingConfigurations.Add("Azure AD: API Client ID nie jest ustawiony");
                if (string.IsNullOrWhiteSpace(TenantId))
                    result.MissingConfigurations.Add("Azure AD: Tenant ID nie jest ustawiony");
                if (string.IsNullOrWhiteSpace(Audience))
                    result.MissingConfigurations.Add("Azure AD: Audience nie jest ustawione");
                if (string.IsNullOrWhiteSpace(ClientSecret))
                    result.MissingConfigurations.Add("Azure AD: Client Secret nie jest ustawiony");

                _logger.LogInformation("Sprawdzanie pól aplikacji");

                // Ostrzeżenia aplikacji
                if (Environment == "Production" && string.IsNullOrWhiteSpace(ApiBaseUrl))
                    result.Warnings.Add("API Base URL nie jest ustawiony dla środowiska Production");

                if (ApiTimeout < 30)
                    result.Warnings.Add("Timeout API poniżej 30 sekund może powodować problemy z długimi operacjami");

                // Podsumowanie
                if (!result.MissingConfigurations.Any() && !result.Warnings.Any())
                {
                    result.Summary = "Konfiguracja jest kompletna i poprawna";
                    result.IsValid = true;
                }
                else if (!result.MissingConfigurations.Any())
                {
                    result.Summary = $"Konfiguracja jest kompletna, ale zawiera {result.Warnings.Count} ostrzeżeń";
                    result.IsValid = true;
                }
                else
                {
                    result.Summary = $"Konfiguracja niekompletna - brakuje {result.MissingConfigurations.Count} ustawień";
                    result.IsValid = false;
                }

                _logger.LogInformation("Walidacja zakończona - Status: {IsValid}", result.IsValid ? "Poprawna" : "Niepoprawna");
                _logger.LogInformation("Brakujące pola: {MissingCount}, Ostrzeżenia: {WarningsCount}", 
                    result.MissingConfigurations.Count, result.Warnings.Count);

                ValidationResult = result;
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas walidacji konfiguracji");
                throw;
            }
            finally
            {
                IsLoading = false;
                _logger.LogInformation("Zakończenie operacji walidacji");
            }
        }

        private async Task TestConnectionAsync()
        {
            _logger.LogInformation("Użytkownik wcisnął przycisk TESTUJ");
            
            try
            {
                IsLoading = true;
                StatusMessage = "Testowanie połączenia...";
                
                _logger.LogInformation("Rozpoczęcie testowania połączenia");
                
                var testResults = new List<string>();
                
                // Test 1: Sprawdź czy wszystkie pola są wypełnione
                if (string.IsNullOrWhiteSpace(UiClientId) || 
                    string.IsNullOrWhiteSpace(ApiClientId) || 
                    string.IsNullOrWhiteSpace(TenantId) || 
                    string.IsNullOrWhiteSpace(ClientSecret) || 
                    string.IsNullOrWhiteSpace(Audience))
                {
                    testResults.Add("❌ Nie wszystkie pola są wypełnione - uzupełnij konfigurację przed testem");
                }
                else
                {
                    testResults.Add("✅ Wszystkie wymagane pola są wypełnione");
                    
                    // Test 2: Sprawdź format Tenant ID (GUID)
                    if (Guid.TryParse(TenantId, out _))
                    {
                        testResults.Add("✅ Tenant ID ma poprawny format GUID");
                    }
                    else
                    {
                        testResults.Add("❌ Tenant ID nie ma poprawnego formatu GUID");
                    }
                    
                    // Test 3: Sprawdź format Client ID (GUID)
                    if (Guid.TryParse(UiClientId, out _) && Guid.TryParse(ApiClientId, out _))
                    {
                        testResults.Add("✅ Client ID mają poprawny format GUID");
                    }
                    else
                    {
                        testResults.Add("❌ Client ID nie mają poprawnego formatu GUID");
                    }
                    
                    // Test 4: Sprawdź format Audience
                    if (Audience.StartsWith("api://") && Audience.Contains(ApiClientId))
                    {
                        testResults.Add("✅ Audience ma poprawny format api://[API Client ID]");
                    }
                    else
                    {
                        testResults.Add("❌ Audience powinno mieć format api://[API Client ID]");
                    }
                    
                    // Test 5: Sprawdź długość Client Secret
                    if (ClientSecret.Length >= 32)
                    {
                        testResults.Add("✅ Client Secret ma odpowiednią długość");
                    }
                    else
                    {
                        testResults.Add("❌ Client Secret wydaje się za krótki (powinien mieć co najmniej 32 znaki)");
                    }
                    
                    // Test 6: Symulacja próby połączenia z Microsoft Entra ID
                    testResults.Add("🔄 Testowanie dostępności Microsoft Entra ID...");
                    await Task.Delay(1000); // Symulacja testu
                    
                    try
                    {
                        // Sprawdź czy można dotrzeć do endpointu Microsoft
                        var httpClient = new System.Net.Http.HttpClient();
                        httpClient.Timeout = TimeSpan.FromSeconds(10);
                        
                        var response = await httpClient.GetAsync($"https://login.microsoftonline.com/{TenantId}/.well-known/openid-configuration");
                        if (response.IsSuccessStatusCode)
                        {
                            testResults.Add("✅ Microsoft Entra ID endpoint jest dostępny");
                        }
                        else
                        {
                            testResults.Add($"❌ Microsoft Entra ID endpoint niedostępny (HTTP {response.StatusCode})");
                        }
                    }
                    catch (Exception ex)
                    {
                        testResults.Add($"❌ Błąd połączenia z Microsoft Entra ID: {ex.Message}");
                    }
                }
                
                // Pokaż wyniki testów
                var resultText = "=== WYNIKI TESTÓW POŁĄCZENIA ===\n\n" + string.Join("\n", testResults);
                
                if (testResults.Any(r => r.StartsWith("❌")))
                {
                    resultText += "\n\n⚠️ Znaleziono problemy z konfiguracją. Sprawdź powyższe błędy i popraw konfigurację.";
                    StatusMessage = "Test połączenia - znaleziono problemy";
                }
                else
                {
                    resultText += "\n\n✅ Wszystkie testy przeszły pomyślnie! Konfiguracja wygląda poprawnie.";
                    StatusMessage = "Test połączenia zakończony pomyślnie";
                }
                
                System.Windows.MessageBox.Show(resultText, "Wyniki testów połączenia", 
                    System.Windows.MessageBoxButton.OK, 
                    testResults.Any(r => r.StartsWith("❌")) ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);
                
                _logger.LogInformation("Test połączenia zakończony");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas testowania połączenia");
                StatusMessage = $"Błąd testu: {ex.Message}";
                
                System.Windows.MessageBox.Show($"Błąd podczas testowania połączenia:\n\n{ex.Message}", 
                    "Błąd testu połączenia", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                _logger.LogInformation("Zakończenie operacji testowania");
            }
        }

        private async Task ShowConfigurationAsync()
        {
            _logger.LogInformation("Użytkownik wcisnął przycisk POKAŻ");
            
            try
            {
                _logger.LogInformation("Rozpoczęcie wyświetlania informacji o konfiguracji");
                
                IsLoading = true;
                StatusMessage = "Odczytywanie informacji o konfiguracji...";

                // Przygotuj tekst do wyświetlenia
                var configText = "=== INFORMACJE O KONFIGURACJI TEAMSMANAGER ===\n\n";
                
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

                // Sprawdź czy pliki można odczytać
                try
                {
                    var azureConfig = await _configManager.LoadAzureAdConfigurationAsync();
                    var appConfig = await _configManager.LoadApplicationConfigurationAsync();

                    configText += "📊 STATUS KONFIGURACJI:\n";
                    configText += $"Azure AD Configuration: {(azureConfig != null ? "ZAŁADOWANA POMYŚLNIE" : "BŁĄD ODCZYTU")}\n";
                    configText += $"Application Configuration: {(appConfig != null ? "ZAŁADOWANA POMYŚLNIE" : "BŁĄD ODCZYTU")}\n\n";
                    
                    if (azureConfig != null)
                    {
                        var fieldsCount = 0;
                        if (!string.IsNullOrEmpty(azureConfig.TenantId)) fieldsCount++;
                        if (!string.IsNullOrEmpty(azureConfig.Ui?.ClientId)) fieldsCount++;
                        if (!string.IsNullOrEmpty(azureConfig.Api?.ClientId)) fieldsCount++;
                        if (!string.IsNullOrEmpty(azureConfig.Api?.ClientSecret)) fieldsCount++;
                        if (!string.IsNullOrEmpty(azureConfig.Api?.Audience)) fieldsCount++;
                        
                        configText += $"🔐 Azure AD - wypełnione pola: {fieldsCount}/5\n";
                        configText += $"   • Tenant ID: {(!string.IsNullOrEmpty(azureConfig.TenantId) ? "USTAWIONE" : "BRAK")}\n";
                        configText += $"   • UI Client ID: {(!string.IsNullOrEmpty(azureConfig.Ui?.ClientId) ? "USTAWIONE" : "BRAK")}\n";
                        configText += $"   • API Client ID: {(!string.IsNullOrEmpty(azureConfig.Api?.ClientId) ? "USTAWIONE" : "BRAK")}\n";
                        configText += $"   • Client Secret: {(!string.IsNullOrEmpty(azureConfig.Api?.ClientSecret) ? "USTAWIONE" : "BRAK")}\n";
                        configText += $"   • Audience: {(!string.IsNullOrEmpty(azureConfig.Api?.Audience) ? "USTAWIONE" : "BRAK")}\n\n";
                    }
                    
                    if (appConfig != null)
                    {
                        configText += $"⚙️ Application Configuration:\n";
                        configText += $"   • Environment: {(!string.IsNullOrEmpty(appConfig.Environment) ? appConfig.Environment : "DOMYŚLNE (Production)")}\n";
                        configText += $"   • Application Name: {(appConfig.ApplicationName ?? "DOMYŚLNE (TeamsManager)")}\n";
                        configText += $"   • Version: {(appConfig.ApplicationVersion ?? "DOMYŚLNE (2.0)")}\n";
                        configText += $"   • Connection String: {(!string.IsNullOrEmpty(appConfig.ConnectionStrings?.DefaultConnection) ? "USTAWIONE" : "DOMYŚLNE")}\n";
                    }
                }
                catch (Exception loadEx)
                {
                    _logger.LogError(loadEx, "Błąd podczas ładowania konfiguracji do wyświetlenia");
                    
                    configText += "❌ BŁĄD ODCZYTU KONFIGURACJI:\n";
                    configText += $"Szczegóły: {loadEx.Message}\n\n";
                    configText += "💡 MOŻLIWE PRZYCZYNY:\n";
                    configText += "• Konfiguracja może być zaszyfrowana dla innego użytkownika\n";
                    configText += "• Plik konfiguracji może być uszkodzony\n";
                    configText += "• Brak uprawnień do odczytu pliku\n\n";
                    configText += "🔧 ROZWIĄZANIA:\n";
                    configText += "• Spróbuj ponownie skonfigurować aplikację\n";
                    configText += "• Skontaktuj się z administratorem systemu\n";
                }

                // Pokaż w MessageBox
                System.Windows.MessageBox.Show(configText, "Informacje o konfiguracji TeamsManager", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                
                StatusMessage = "Informacje o konfiguracji wyświetlone";
                _logger.LogInformation("Informacje o konfiguracji zostały wyświetlone użytkownikowi");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wyświetlania informacji o konfiguracji");
                StatusMessage = $"Błąd wyświetlania: {ex.Message}";
                
                System.Windows.MessageBox.Show($"Błąd podczas wyświetlania informacji o konfiguracji:\n\n{ex.Message}", 
                    "Błąd", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                _logger.LogInformation("Zakończenie operacji wyświetlania");
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
