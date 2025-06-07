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
    /// ViewModel dla okna konfiguracji UI
    /// </summary>
    public class UiConfigurationViewModel : ConfigurationViewModelBase
    {
        private readonly ConfigurationManager _configManager;
        private readonly string _presetTenantId;
        private readonly string _presetApiScope;

        private string? _clientId;
        private string? _tenantId;
        private string _redirectUri = "http://localhost";
        private string? _apiScope;
        private string _instance = "https://login.microsoftonline.com/";
        private string _apiBaseUrl = "https://localhost:7037";

        public event EventHandler<bool>? RequestClose;
        public event EventHandler? RequestNavigateBack;

        public UiConfigurationViewModel(string tenantId, string apiScope)
        {
            _configManager = new ConfigurationManager();
            _presetTenantId = tenantId;
            _presetApiScope = apiScope;

            // Ustaw wartości przekazane z poprzedniego kroku
            TenantId = tenantId;
            ApiScope = apiScope;

            NextCommand = new RelayCommand(async _ => await NextAsync(), _ => IsValid);
            BackCommand = new RelayCommand(_ => NavigateBack());
            CancelCommand = new RelayCommand(_ => Cancel());

            // Załaduj istniejącą konfigurację jeśli istnieje
            Task.Run(async () => await LoadExistingConfiguration());
        }

        #region Properties

        public string? ClientId
        {
            get => _clientId;
            set
            {
                _clientId = value;
                OnPropertyChanged();
                Validate();
            }
        }

        public string? TenantId
        {
            get => _tenantId;
            set
            {
                _tenantId = value;
                OnPropertyChanged();
                Validate();
            }
        }

        public string RedirectUri
        {
            get => _redirectUri;
            set
            {
                _redirectUri = value;
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

        public string Instance
        {
            get => _instance;
            set
            {
                _instance = value;
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

        #endregion

        #region Methods

        private async Task LoadExistingConfiguration()
        {
            try
            {
                var config = await _configManager.LoadOAuthConfigAsync();
                if (config != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        ClientId = config.AzureAd.ClientId;
                        RedirectUri = config.AzureAd.RedirectUri;
                        Instance = config.AzureAd.Instance;
                        // TenantId i ApiScope pozostają z wartości przekazanych
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading configuration: {ex.Message}");
            }
        }

        protected override void Validate()
        {
            ValidationErrors.Clear();

            // Client ID validation
            if (string.IsNullOrWhiteSpace(ClientId))
            {
                ValidationErrors.Add("Application (client) ID jest wymagany");
            }
            else if (!IsValidGuid(ClientId))
            {
                ValidationErrors.Add("Application (client) ID musi być prawidłowym GUID");
            }
            else if (ClientId == _presetApiScope?.Split('/')[2]) // Sprawdź czy nie jest to samo co API Client ID
            {
                ValidationErrors.Add("Client ID aplikacji UI musi być inny niż Client ID API!");
            }

            // Tenant ID validation (should match the one from API config)
            if (string.IsNullOrWhiteSpace(TenantId))
            {
                ValidationErrors.Add("Tenant ID jest wymagany");
            }
            else if (!IsValidGuid(TenantId))
            {
                ValidationErrors.Add("Tenant ID musi być prawidłowym GUID");
            }
            else if (TenantId != _presetTenantId)
            {
                ValidationErrors.Add("Tenant ID musi być taki sam jak w konfiguracji API");
            }

            // Redirect URI validation
            if (string.IsNullOrWhiteSpace(RedirectUri))
            {
                ValidationErrors.Add("Redirect URI jest wymagany");
            }
            else if (!IsValidUri(RedirectUri))
            {
                ValidationErrors.Add("Redirect URI musi być prawidłowym URL");
            }

            // API Scope validation
            if (string.IsNullOrWhiteSpace(ApiScope))
            {
                ValidationErrors.Add("API Scope jest wymagany");
            }
            else if (!ApiScope.StartsWith("api://"))
            {
                ValidationErrors.Add("API Scope powinien zaczynać się od 'api://'");
            }

            // Instance validation
            if (string.IsNullOrWhiteSpace(Instance))
            {
                ValidationErrors.Add("Authority Instance jest wymagany");
            }
            else if (!IsValidUri(Instance))
            {
                ValidationErrors.Add("Authority Instance musi być prawidłowym URL");
            }

            IsValid = !ValidationErrors.Any();
        }

        private async Task NextAsync()
        {
            try
            {
                // Zapisz konfigurację OAuth (UI) używając nowej struktury
                var oauthConfig = new OAuthConfiguration
                {
                    Scopes = new List<string> { ApiScope! },
                    AzureAd = new AzureAdConfiguration
                    {
                        TenantId = TenantId!,
                        ClientId = ClientId!,
                        Instance = Instance,
                        RedirectUri = RedirectUri,
                        ApiScope = ApiScope!,
                        ApiBaseUrl = ApiBaseUrl
                    }
                };

                await _configManager.SaveOAuthConfigAsync(oauthConfig);

                // Przejdź do następnego kroku - Test Connection
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var testWindow = new TestConnectionWindow();
                    testWindow.Show();
                    RequestClose?.Invoke(this, true);
                });
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

        private void NavigateBack()
        {
            var result = MessageBox.Show(
                "Czy na pewno chcesz wrócić do poprzedniego kroku?\nZmiany w tym oknie nie zostaną zapisane.",
                "Powrót",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                RequestNavigateBack?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Cancel()
        {
            var result = MessageBox.Show(
                "Czy na pewno chcesz anulować konfigurację?\nWszystkie wprowadzone dane zostaną utracone.",
                "Anuluj konfigurację",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                RequestClose?.Invoke(this, false);
            }
        }

        #endregion
    }
}