using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Identity.Client;
using TeamsManager.UI.Models.Configuration;
using TeamsManager.UI.Services.Configuration;

namespace TeamsManager.UI.ViewModels.Configuration
{
    /// <summary>
    /// ViewModel dla okna testowania połączenia
    /// </summary>
    public class TestConnectionViewModel : INotifyPropertyChanged
    {
        private readonly ConfigurationManager _configManager;
        private readonly ConfigurationValidator _validator;
        private OAuthConfiguration? _oauthConfig;
        private ApiConfiguration? _apiConfig;

        private bool _isInitialState = true;
        private bool _isTesting = false;
        private bool _isSuccess = false;
        private bool _isError = false;
        private string _currentTestStep = "";
        private string _configSummary = "";
        private ObservableCollection<TestStep> _testSteps;
        private ObservableCollection<string> _errorMessages;

        public event EventHandler<bool>? RequestClose;
        public event EventHandler? RequestNavigateBack;
        public event EventHandler? RequestRestart;

        public TestConnectionViewModel()
        {
            _configManager = new ConfigurationManager();
            _validator = new ConfigurationValidator(_configManager);

            _testSteps = new ObservableCollection<TestStep>
            {
                new TestStep { Name = "Ładowanie konfiguracji", Status = TestStepStatus.Pending },
                new TestStep { Name = "Weryfikacja parametrów", Status = TestStepStatus.Pending },
                new TestStep { Name = "Logowanie do Azure AD", Status = TestStepStatus.Pending },
                new TestStep { Name = "Sprawdzanie uprawnień Microsoft Graph", Status = TestStepStatus.Pending },
                new TestStep { Name = "Testowanie połączenia z API", Status = TestStepStatus.Pending },
                new TestStep { Name = "Weryfikacja On-Behalf-Of flow", Status = TestStepStatus.Pending }
            };

            _errorMessages = new ObservableCollection<string>();

            TestConnectionCommand = new RelayCommand(async _ => await TestConnectionAsync());
            FinishCommand = new RelayCommand(_ => Finish());
            BackCommand = new RelayCommand(_ => NavigateBack());
            CancelCommand = new RelayCommand(_ => Cancel());
        }

        #region Properties

        public bool IsInitialState
        {
            get => _isInitialState;
            set { _isInitialState = value; OnPropertyChanged(); }
        }

        public bool IsTesting
        {
            get => _isTesting;
            set { _isTesting = value; OnPropertyChanged(); UpdateButtonVisibility(); }
        }

        public bool IsSuccess
        {
            get => _isSuccess;
            set { _isSuccess = value; OnPropertyChanged(); }
        }

        public bool IsError
        {
            get => _isError;
            set { _isError = value; OnPropertyChanged(); }
        }

        public string CurrentTestStep
        {
            get => _currentTestStep;
            set { _currentTestStep = value; OnPropertyChanged(); }
        }

        public string ConfigSummary
        {
            get => _configSummary;
            set { _configSummary = value; OnPropertyChanged(); }
        }

        public ObservableCollection<TestStep> TestSteps
        {
            get => _testSteps;
            set { _testSteps = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> ErrorMessages
        {
            get => _errorMessages;
            set { _errorMessages = value; OnPropertyChanged(); }
        }

        // Button visibility
        public bool CanCancel => !_isTesting;
        public bool CanGoBack => !_isTesting && !_isSuccess;
        public bool CanTest => _isInitialState || _isError;
        public bool CanFinish => _isSuccess;

        // Commands
        public ICommand TestConnectionCommand { get; }
        public ICommand FinishCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Methods

        private async Task TestConnectionAsync()
        {
            IsInitialState = false;
            IsTesting = true;
            IsError = false;
            IsSuccess = false;
            ErrorMessages.Clear();

            // Reset all steps
            foreach (var step in TestSteps)
            {
                step.Status = TestStepStatus.Pending;
            }

            try
            {
                // Step 1: Load configuration
                await UpdateTestStep(0, TestStepStatus.InProgress, "Ładowanie konfiguracji...");
                await Task.Delay(500); // Simulate work

                _oauthConfig = await _configManager.LoadOAuthConfigAsync();
                _apiConfig = await _configManager.LoadApiConfigAsync();

                if (_oauthConfig == null || _apiConfig == null)
                {
                    throw new Exception("Nie znaleziono plików konfiguracyjnych");
                }

                await UpdateTestStep(0, TestStepStatus.Success);

                // Step 2: Validate configuration
                await UpdateTestStep(1, TestStepStatus.InProgress, "Weryfikacja parametrów konfiguracji...");
                await Task.Delay(500);

                var validationResult = await _validator.ValidateFullConfigurationAsync();
                if (!validationResult.IsValid)
                {
                    throw new Exception($"Błędy walidacji: {string.Join(", ", validationResult.Errors)}");
                }

                await UpdateTestStep(1, TestStepStatus.Success);

                // Step 3: Test Azure AD login
                await UpdateTestStep(2, TestStepStatus.InProgress, "Logowanie do Azure AD...");

                var authResult = await TestAzureADLogin();
                if (authResult == null)
                {
                    throw new Exception("Nie udało się zalogować do Azure AD");
                }

                await UpdateTestStep(2, TestStepStatus.Success);

                // Step 4: Test Microsoft Graph permissions
                await UpdateTestStep(3, TestStepStatus.InProgress, "Sprawdzanie uprawnień Microsoft Graph...");

                await TestGraphPermissions(authResult.AccessToken);

                await UpdateTestStep(3, TestStepStatus.Success);

                // Step 5: Test API connection
                await UpdateTestStep(4, TestStepStatus.InProgress, "Testowanie połączenia z TeamsManager API...");

                await TestApiConnection(authResult.AccessToken);

                await UpdateTestStep(4, TestStepStatus.Success);

                // Step 6: Test OBO flow
                await UpdateTestStep(5, TestStepStatus.InProgress, "Weryfikacja On-Behalf-Of flow...");

                await TestOnBehalfOfFlow(authResult.AccessToken);

                await UpdateTestStep(5, TestStepStatus.Success);

                // Success!
                CurrentTestStep = "Test zakończony pomyślnie!";
                GenerateConfigSummary();
                IsSuccess = true;
            }
            catch (Exception ex)
            {
                // Mark current step as failed
                var currentStep = TestSteps.FirstOrDefault(s => s.Status == TestStepStatus.InProgress);
                if (currentStep != null)
                {
                    currentStep.Status = TestStepStatus.Failed;
                }

                ErrorMessages.Add($"❌ {ex.Message}");

                if (ex.InnerException != null)
                {
                    ErrorMessages.Add($"   Szczegóły: {ex.InnerException.Message}");
                }

                CurrentTestStep = "Test zakończony niepowodzeniem";
                IsError = true;
            }
            finally
            {
                IsTesting = false;
            }
        }

        private async Task UpdateTestStep(int index, TestStepStatus status, string? message = null)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (index < TestSteps.Count)
                {
                    TestSteps[index].Status = status;
                    if (!string.IsNullOrEmpty(message))
                    {
                        CurrentTestStep = message;
                    }
                }
            });
        }

        private async Task<AuthenticationResult?> TestAzureADLogin()
        {
            var app = PublicClientApplicationBuilder
                .Create(_oauthConfig!.AzureAd.ClientId)
                .WithAuthority($"{_oauthConfig.AzureAd.Instance}{_oauthConfig.AzureAd.TenantId}")
                .WithRedirectUri(_oauthConfig.AzureAd.RedirectUri)
                .Build();

            var scopes = new[] { _oauthConfig.AzureAd.ApiScope };

            try
            {
                // Try silent authentication first
                var accounts = await app.GetAccountsAsync();
                if (accounts.Any())
                {
                    return await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                        .ExecuteAsync();
                }

                // Interactive login
                return await app.AcquireTokenInteractive(scopes)
                    .WithPrompt(Microsoft.Identity.Client.Prompt.SelectAccount)
                    .ExecuteAsync();
            }
            catch (MsalException msalEx)
            {
                throw new Exception($"Błąd MSAL: {msalEx.ErrorCode} - {msalEx.Message}", msalEx);
            }
        }

        private async Task TestGraphPermissions(string accessToken)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Test basic user profile access
            var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Brak dostępu do Microsoft Graph: {response.StatusCode}");
            }

            await Task.Delay(500); // Simulate additional checks
        }

        private async Task TestApiConnection(string accessToken)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            try
            {
                var response = await httpClient.GetAsync($"{_oauthConfig!.AzureAd.ApiBaseUrl}/api/configuration/test");

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    throw new Exception($"API zwróciło błąd {response.StatusCode}: {content}");
                }
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Nie można połączyć się z API pod adresem {_oauthConfig!.AzureAd.ApiBaseUrl}. Upewnij się, że API jest uruchomione.", ex);
            }
        }

        private async Task TestOnBehalfOfFlow(string accessToken)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.PostAsync($"{_oauthConfig!.AzureAd.ApiBaseUrl}/api/configuration/validate-obo", null);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("On-Behalf-Of flow nie działa poprawnie. Sprawdź konfigurację API.");
            }

            await Task.Delay(500);
        }

        private void GenerateConfigSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Tenant ID: {_oauthConfig?.AzureAd.TenantId}");
            sb.AppendLine($"UI Client ID: {_oauthConfig?.AzureAd.ClientId}");
            sb.AppendLine($"API Base URL: {_oauthConfig?.AzureAd.ApiBaseUrl}");
            sb.AppendLine($"Redirect URI: {_oauthConfig?.AzureAd.RedirectUri}");
            sb.AppendLine($"API Scope: {_oauthConfig?.AzureAd.ApiScope}");
            sb.AppendLine($"\nKonfiguracja zapisana w:");
            sb.AppendLine($"  %APPDATA%\\TeamsManager\\oauth_config.json");

            ConfigSummary = sb.ToString();
        }

        private void UpdateButtonVisibility()
        {
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanTest));
            OnPropertyChanged(nameof(CanFinish));
        }

        private void Finish()
        {
            var result = MessageBox.Show(
                "Konfiguracja została zakończona pomyślnie!\n\nAplikacja zostanie teraz zrestartowana.",
                "Sukces",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            RequestRestart?.Invoke(this, EventArgs.Empty);
        }

        private void NavigateBack()
        {
            RequestNavigateBack?.Invoke(this, EventArgs.Empty);
        }

        private void Cancel()
        {
            var result = MessageBox.Show(
                "Czy na pewno chcesz anulować konfigurację?",
                "Anuluj",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                RequestClose?.Invoke(this, false);
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public class TestStep : INotifyPropertyChanged
    {
        private string _name = "";
        private TestStepStatus _status;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public TestStepStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum TestStepStatus
    {
        Pending,
        InProgress,
        Success,
        Failed
    }
}