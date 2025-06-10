using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Models.Configuration;
using TeamsManager.UI.Services;
using TeamsManager.UI.Services.Abstractions;
using TeamsManager.UI.Services.Configuration;

namespace TeamsManager.UI.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly IMsalAuthService _msalAuthService;
        private readonly ConfigurationManager _configManager;
        private readonly ILogger<LoginViewModel> _logger;
        
        private bool _isLoading;
        // Usunięte checkboxy - WAM automatycznie zarządza tokenami
        private string? _statusMessage;
        private string? _userEmail;
        private bool _canLogin = true;

        public LoginViewModel(
            IMsalAuthService msalAuthService,
            ConfigurationManager configManager,
            ILogger<LoginViewModel> logger)
        {
            _msalAuthService = msalAuthService ?? throw new ArgumentNullException(nameof(msalAuthService));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Inicjalizacja komend
            LoginCommand = new RelayCommand(async _ => await LoginAsync(), _ => CanLogin);
            CancelCommand = new RelayCommand(_ => OnCancelRequested());
            ClearSettingsCommand = new RelayCommand(async _ => await ClearSettingsAsync());
            
            // Wczytaj zapisane ustawienia
            _ = LoadSavedSettingsAsync();
        }

        #region Properties

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanLogin));
            }
        }

        // Usunięte właściwości checkboxów - WAM automatycznie zarządza stanem logowania

        public string? StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public string? UserEmail
        {
            get => _userEmail;
            set
            {
                _userEmail = value;
                OnPropertyChanged();
            }
        }

        public bool CanLogin => !IsLoading && _canLogin;

        #endregion

        #region Commands

        public ICommand LoginCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ClearSettingsCommand { get; }

        #endregion

        #region Events

        public event EventHandler<bool>? LoginCompleted;
        public event EventHandler? CancelRequested;

        #endregion

        #region Methods

        private async Task LoadSavedSettingsAsync()
        {
            try
            {
                var settings = await _configManager.LoadLoginSettingsAsync();
                if (settings != null)
                {
                    UserEmail = settings.LastUserEmail;
                    
                    if (!string.IsNullOrEmpty(UserEmail))
                    {
                        StatusMessage = $"Ostatnie logowanie: {UserEmail}";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wczytywania ustawień logowania");
            }
        }

        private async Task LoginAsync()
        {
            IsLoading = true;
            StatusMessage = "Logowanie...";
            _canLogin = false;
            
            try
            {
                _logger.LogDebug("Rozpoczęto proces logowania");
                
                // Znajdź okno LoginWindow
                var loginWindow = System.Windows.Application.Current.Windows.OfType<Views.LoginWindow>().FirstOrDefault();
                
                // Wykonaj logowanie przez MSAL
                var authResult = await _msalAuthService.AcquireTokenInteractiveAsync(loginWindow);
                
                if (authResult != null && !string.IsNullOrEmpty(authResult.AccessToken))
                {
                    _logger.LogInformation("Pomyślne logowanie użytkownika: {UserEmail}", 
                        authResult.Account?.Username);
                    
                    // Zapisz informację o ostatnim logowaniu (WAM zarządza tokenami automatycznie)
                    var settings = new LoginSettings
                    {
                        RememberMe = true, // WAM automatycznie "pamięta"
                        AutoLogin = true,  // WAM automatycznie obsługuje SSO
                        LastUserEmail = authResult.Account?.Username,
                        LastLoginDate = DateTime.Now
                    };
                    
                    await _configManager.SaveLoginSettingsAsync(settings);
                    _logger.LogDebug("Zapisano informacje o ostatnim logowaniu");
                    
                    StatusMessage = "Logowanie zakończone pomyślnie!";
                    await Task.Delay(500); // Krótka pauza dla UX
                    
                    // Zgłoś sukces
                    LoginCompleted?.Invoke(this, true);
                }
                else
                {
                    StatusMessage = "Logowanie anulowane";
                    _canLogin = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas logowania");
                StatusMessage = $"Błąd: {ex.Message}";
                _canLogin = true;
                
                // Zgłoś niepowodzenie
                LoginCompleted?.Invoke(this, false);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ClearSettingsAsync()
        {
            try
            {
                await _configManager.ClearLoginSettingsAsync();
                UserEmail = null;
                StatusMessage = "Wyczyszczono zapisane dane";
                
                _logger.LogInformation("Wyczyszczono zapisane ustawienia logowania");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas czyszczenia ustawień");
                StatusMessage = "Błąd podczas czyszczenia danych";
            }
        }

        private void OnCancelRequested()
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
} 