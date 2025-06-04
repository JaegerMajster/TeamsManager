using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TeamsManager.UI.Models.Configuration;
using TeamsManager.UI.Services.Configuration;
using TeamsManager.UI.Views.Configuration;

namespace TeamsManager.UI.ViewModels.Configuration
{
    /// <summary>
    /// ViewModel dla okna konfiguracji API
    /// </summary>
    public class ApiConfigurationViewModel : ConfigurationViewModelBase
    {
        private readonly ConfigurationManager _configManager;
        private string? _tenantId;
        private string? _apiClientId;
        private string? _apiClientSecret;
        private string? _apiAudience;
        private string? _apiScope;
        private string _apiBaseUrl = "https://localhost:7037";

        public event EventHandler<bool>? RequestClose;

        public ApiConfigurationViewModel(ConfigurationManager configManager)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));

            NextCommand = new RelayCommand(async _ => await NextAsync(), _ => IsValid);
            CancelCommand = new RelayCommand(_ => Cancel());
            ShowTenantIdHelpCommand = new RelayCommand(_ => ShowTenantIdHelp());

            // Nie ładuj konfiguracji w konstruktorze - może to powodować problemy
        }

        #region Properties

        public string? TenantId
        {
            get => _tenantId;
            set
            {
                _tenantId = value;
                OnPropertyChanged();
                Validate();
                UpdateDependentFields();
            }
        }

        public string? ApiClientId
        {
            get => _apiClientId;
            set
            {
                _apiClientId = value;
                OnPropertyChanged();
                Validate();
                UpdateDependentFields();
            }
        }

        public string? ApiClientSecret
        {
            get => _apiClientSecret;
            set
            {
                _apiClientSecret = value;
                OnPropertyChanged();
                Validate();
            }
        }

        public string? ApiAudience
        {
            get => _apiAudience;
            set
            {
                _apiAudience = value;
                OnPropertyChanged();
                Validate();
            }
        }

        public string? ApiScope
        {
            get => _apiScope;
            set
            {
                _apiScope = value;
                OnPropertyChanged();
                Validate();
            }
        }

        public string ApiBaseUrl
        {
            get => _apiBaseUrl;
            set
            {
                _apiBaseUrl = value;
                OnPropertyChanged();
                Validate();
            }
        }

        public ICommand ShowTenantIdHelpCommand { get; }

        #endregion

        #region Methods

        private async Task LoadExistingConfigurationAsync()
        {
            try
            {
                var config = await _configManager.LoadApiConfigAsync();
                if (config != null)
                {
                    TenantId = config.TenantId;
                    ApiClientId = config.ApiClientId;
                    ApiAudience = config.ApiAudience;
                    ApiScope = config.ApiScope;
                    ApiBaseUrl = config.ApiBaseUrl;
                    // Secret nie jest ładowany ze względów bezpieczeństwa
                }
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"Error loading configuration: {ex.Message}");
            }
        }

        private void UpdateDependentFields()
        {
            // Automatyczne sugerowanie wartości na podstawie Client ID
            if (!string.IsNullOrWhiteSpace(ApiClientId) && IsValidGuid(ApiClientId))
            {
                if (string.IsNullOrWhiteSpace(ApiAudience))
                {
                    ApiAudience = $"api://{ApiClientId}";
                }

                if (string.IsNullOrWhiteSpace(ApiScope))
                {
                    ApiScope = $"api://{ApiClientId}/access_as_user";
                }
            }
        }

        protected override void Validate()
        {
            ValidationErrors.Clear();

            // Tenant ID validation
            if (string.IsNullOrWhiteSpace(TenantId))
            {
                ValidationErrors.Add("Tenant ID jest wymagany");
            }
            else if (!IsValidGuid(TenantId))
            {
                ValidationErrors.Add("Tenant ID musi być prawidłowym GUID");
            }

            // API Client ID validation
            if (string.IsNullOrWhiteSpace(ApiClientId))
            {
                ValidationErrors.Add("Application (client) ID jest wymagany");
            }
            else if (!IsValidGuid(ApiClientId))
            {
                ValidationErrors.Add("Application (client) ID musi być prawidłowym GUID");
            }

            // Client Secret validation
            if (string.IsNullOrWhiteSpace(ApiClientSecret))
            {
                ValidationErrors.Add("Client Secret jest wymagany");
            }
            else if (ApiClientSecret.Length < 10)
            {
                ValidationErrors.Add("Client Secret wydaje się za krótki");
            }

            // API Audience validation
            if (string.IsNullOrWhiteSpace(ApiAudience))
            {
                ValidationErrors.Add("Application ID URI jest wymagany");
            }
            else if (!ApiAudience.StartsWith("api://"))
            {
                ValidationErrors.Add("Application ID URI powinien zaczynać się od 'api://'");
            }

            // API Scope validation
            if (string.IsNullOrWhiteSpace(ApiScope))
            {
                ValidationErrors.Add("API Scope jest wymagany");
            }
            else if (!ApiScope.Contains("/"))
            {
                ValidationErrors.Add("API Scope powinien zawierać '/' (np. api://guid/access_as_user)");
            }

            // API Base URL validation
            if (string.IsNullOrWhiteSpace(ApiBaseUrl))
            {
                ValidationErrors.Add("API Base URL jest wymagany");
            }
            else if (!IsValidUri(ApiBaseUrl))
            {
                ValidationErrors.Add("API Base URL musi być prawidłowym URL");
            }

            IsValid = !ValidationErrors.Any();
        }

        private async Task NextAsync()
        {
            try
            {
                // Zapisz konfigurację API
                var apiConfig = new ApiConfiguration
                {
                    TenantId = TenantId!,
                    ApiClientId = ApiClientId!,
                    ApiAudience = ApiAudience!,
                    ApiScope = ApiScope!,
                    ApiBaseUrl = ApiBaseUrl
                };

                await _configManager.SaveApiConfigAsync(apiConfig, ApiClientSecret!);

                // Przejdź do następnego kroku - UI Configuration
                var uiConfigWindow = new UiConfigurationWindow(TenantId!, ApiScope!);
                var uiResult = uiConfigWindow.ShowDialog();
                
                // Tylko zamknij to okno jeśli konfiguracja została ukończona pomyślnie
                // Jeśli user kliknął "Wstecz", UiConfigurationWindow już otworzyło nowe ApiConfigurationWindow
                if (uiResult == true)
                {
                    RequestClose?.Invoke(this, true);
                }
                // Jeśli uiResult == false/null (Wstecz lub Anuluj), nie rób nic - pozwól oknu zostać otwarte
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas zapisywania konfiguracji: {ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Cancel()
        {
            var result = MessageBox.Show(
                "Czy na pewno chcesz anulować konfigurację?",
                "Anuluj konfigurację",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                RequestClose?.Invoke(this, false);
            }
        }

        private void ShowTenantIdHelp()
        {
            MessageBox.Show(
                "Jak znaleźć Tenant ID:\n\n" +
                "1. Zaloguj się do Azure Portal\n" +
                "2. Przejdź do 'Microsoft Entra ID'\n" +
                "3. W sekcji 'Overview' znajdziesz 'Directory (tenant) ID'\n" +
                "4. Kliknij ikonę kopiowania obok ID\n\n" +
                "Format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
                "Pomoc - Tenant ID",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #endregion
    }
}