using Microsoft.Identity.Client;
using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using TeamsManager.UI.Views;
using TeamsManager.UI.Services;

namespace TeamsManager.UI
{
    public partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private readonly MsalAuthService? _msalAuthService;
        private AuthenticationResult? _authResult;
        private readonly HttpClient _httpClient = new HttpClient();
        private ManualTestingWindow? _manualTestingWindow;
        private readonly GraphUserProfileService _graphUserProfileService;

        public MainWindow()
        {
            InitializeComponent();

            // Inicjalizacja serwisu Graph
            _graphUserProfileService = new GraphUserProfileService();

            // Inicjalizacja serwisu MSAL z obsługą błędów
            try
            {
                _msalAuthService = new MsalAuthService();
            }
            catch (InvalidOperationException ex)
            {
                // Obsługa krytycznego błędu konfiguracji MSAL
                ShowErrorDialog(ex.Message + "\nAplikacja nie może kontynuować bez poprawnej konfiguracji logowania.",
                                "Krytyczny Błąd Konfiguracji");

                // Zablokowanie funkcjonalności logowania
                LoginButton.IsEnabled = false;
                LogoutButton.IsEnabled = false;
                ManualTestsButton.IsEnabled = false;
                UserDisplayNameTextBlock.Text = "Błąd konfiguracji";
                UserInfoTextBlock.Text = "MSAL nie został poprawnie skonfigurowany";
            }

            // Ustaw ciemny motyw dla tego okna
            this.SourceInitialized += (s, e) =>
            {
                var helper = new WindowInteropHelper(this);
                SetWindowToDarkMode(helper.Handle);
            };
        }

        private static void SetWindowToDarkMode(IntPtr handle)
        {
            try
            {
                int darkMode = 1;
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            }
            catch
            {
                // Jeśli nie działa na starszej wersji Windows, po prostu ignoruj
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Sprawdzenie, czy MSALService został poprawnie zainicjowany
            if (_msalAuthService == null)
            {
                ShowErrorDialog("Serwis autentykacji MSAL nie został poprawnie zainicjowany z powodu błędu konfiguracji.",
                                "Błąd");
                return;
            }

            // Pokaż loading overlay
            ShowLoading(true);

            var startTime = DateTime.Now;
            try
            {
                _authResult = await _msalAuthService.AcquireTokenInteractiveAsync(this);
                var duration = DateTime.Now - startTime;

                if (_authResult != null && !string.IsNullOrEmpty(_authResult.AccessToken))
                {
                    // Pobierz profil użytkownika z Microsoft Graph
                    await LoadUserProfileAsync();

                    // Animowana zmiana UI
                    await AnimateLoginSuccess();

                    // Przekaż wynik logowania do okna testów jeśli jest otwarte
                    if (_manualTestingWindow != null)
                    {
                        await _manualTestingWindow.SaveLoginResultToSession(true, 
                            $"Pomyślne logowanie użytkownika: {_authResult.Account?.Username}", duration);
                    }
                }
                else
                {
                    await AnimateLoginFailure();
                    
                    // Przekaż wynik logowania do okna testów jeśli jest otwarte
                    if (_manualTestingWindow != null)
                    {
                        await _manualTestingWindow.SaveLoginResultToSession(false, 
                            "Logowanie zostało anulowane lub nie powiodło się", duration);
                    }
                }
            }
            catch (Exception ex)
            {
                var duration = DateTime.Now - startTime;
                ShowErrorDialog($"Błąd logowania: {ex.Message}", "Błąd");
                await AnimateLoginFailure();

                // Przekaż wynik logowania do okna testów jeśli jest otwarte
                if (_manualTestingWindow != null)
                {
                    await _manualTestingWindow.SaveLoginResultToSession(false, 
                        $"Błąd podczas logowania: {ex.Message}", duration);
                }
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async System.Threading.Tasks.Task LoadUserProfileAsync()
        {
            if (_authResult?.AccessToken == null || _msalAuthService == null)
                return;

            try
            {
                // POPRAWKA: Pobierz osobny token dla Microsoft Graph API
                string? graphToken = null;
                
                // Najpierw spróbuj pobrać token Graph z cache (silent)
                graphToken = await _msalAuthService.AcquireGraphTokenAsync();
                
                if (string.IsNullOrEmpty(graphToken))
                {
                    // Jeśli nie udało się pobrać z cache, spróbuj interactywnie
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Pobieranie Graph token interactywnie...");
                    graphToken = await _msalAuthService.AcquireGraphTokenInteractiveAsync(this);
                }
                
                if (string.IsNullOrEmpty(graphToken))
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Nie udało się pobrać Graph token. Używam podstawowych informacji z MSAL.");
                    
                    // Fallback: użyj informacji z podstawowego tokenu MSAL
                    UserDisplayNameTextBlock.Text = _authResult.Account?.Username ?? "Użytkownik";
                    UserInfoTextBlock.Text = "Zalogowano (ograniczony dostęp)";
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Używam dedykowany Graph token");

                // Wykonaj test Graph API z dedykowanym tokenem Graph
                var testResult = await _graphUserProfileService.TestGraphAccessAsync(graphToken);
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Graph Test Result - Profile: {testResult.CanAccessProfile}, Photo: {testResult.CanAccessPhoto}");

                if (testResult.CanAccessProfile)
                {
                    // Pobierz prawdziwy profil użytkownika
                    var userProfile = await _graphUserProfileService.GetUserProfileAsync(graphToken);
                    if (userProfile != null)
                    {
                        UserDisplayNameTextBlock.Text = userProfile.DisplayName ?? "Nieznany użytkownik";
                        UserInfoTextBlock.Text = userProfile.Mail ?? userProfile.UserPrincipalName ?? "Zalogowano";

                        // Pobierz avatar użytkownika jeśli dostępny
                        if (testResult.CanAccessPhoto)
                        {
                            var userPhoto = await _graphUserProfileService.GetUserPhotoAsync(graphToken);
                            if (userPhoto != null)
                            {
                                // Animowana zmiana avatara
                                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.2));
                                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
                                
                                DefaultUserIcon.BeginAnimation(OpacityProperty, fadeOut);
                                fadeOut.Completed += (s, e) =>
                                {
                                    DefaultUserIcon.Visibility = Visibility.Collapsed;
                                    UserAvatarBrush.ImageSource = userPhoto;
                                    UserAvatarImage.Visibility = Visibility.Visible;
                                    UserAvatarImage.BeginAnimation(OpacityProperty, fadeIn);
                                };
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Profil załadowany: {userProfile.DisplayName}");
                        return;
                    }
                }

                // Jeśli nie udało się pobrać profilu, pokaż informacje z podstawowego tokenu
                UserDisplayNameTextBlock.Text = _authResult.Account?.Username ?? "Użytkownik";
                UserInfoTextBlock.Text = "Zalogowano (ograniczony dostęp do Graph)";
                
                if (!string.IsNullOrEmpty(testResult.ErrorMessage))
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Graph API Error: {testResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Błąd podczas ładowania profilu: {ex.Message}");
                UserDisplayNameTextBlock.Text = _authResult.Account?.Username ?? "Użytkownik";
                UserInfoTextBlock.Text = "Zalogowano";
            }
        }

        private async System.Threading.Tasks.Task AnimateLoginSuccess()
        {
            // Pokaż status indicator
            StatusIndicator.Visibility = Visibility.Visible;

            // Animacja fade dla tekstu
            var fadeAnimation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));

            // Zmień przyciski z animacją
            LoginButton.Visibility = Visibility.Collapsed;
            
            LogoutButton.Visibility = Visibility.Visible;
            LogoutButton.IsEnabled = true;
            LogoutButton.BeginAnimation(OpacityProperty, fadeAnimation);
            
            ManualTestsButton.Visibility = Visibility.Visible;
            ManualTestsButton.IsEnabled = true;
            ManualTestsButton.BeginAnimation(OpacityProperty, fadeAnimation);

            await System.Threading.Tasks.Task.Delay(300);
        }

        private async System.Threading.Tasks.Task AnimateLoginFailure()
        {
            ResetUserInterface();
            LogoutButton.IsEnabled = false;
            LogoutButton.Visibility = Visibility.Collapsed;
            LoginButton.IsEnabled = true;
            LoginButton.Visibility = Visibility.Visible;
            ManualTestsButton.IsEnabled = false;
            ManualTestsButton.Visibility = Visibility.Collapsed;

            // Delikatne potrząśnięcie karty
            var shakeAnimation = new ThicknessAnimation(
                new Thickness(0),
                new Thickness(5, 0, -5, 0),
                TimeSpan.FromSeconds(0.05))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };

            UserAvatarBorder.BeginAnimation(MarginProperty, shakeAnimation);
            await System.Threading.Tasks.Task.Delay(300);
        }

        private void ResetUserInterface()
        {
            UserDisplayNameTextBlock.Text = "Witaj!";
            UserInfoTextBlock.Text = "Zaloguj się, aby kontynuować";
            UserAvatarImage.Visibility = Visibility.Collapsed;
            DefaultUserIcon.Visibility = Visibility.Visible;
            UserAvatarBrush.ImageSource = null;
            StatusIndicator.Visibility = Visibility.Collapsed;
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLoading(true);

            // Sprawdzenie, czy serwis istnieje
            if (_msalAuthService != null)
            {
                await _msalAuthService.SignOutAsync();
            }

            _authResult = null;
            
            // Animowana zmiana UI
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.2));
            fadeOut.Completed += async (s, args) =>
            {
                ResetUserInterface();
                
                // Aktualizacja stanu przycisków
                LogoutButton.IsEnabled = false;
                LogoutButton.Visibility = Visibility.Collapsed;
                LoginButton.IsEnabled = true;
                LoginButton.Visibility = Visibility.Visible;
                ManualTestsButton.IsEnabled = false;
                ManualTestsButton.Visibility = Visibility.Collapsed;

                // Fade in
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
                LoginButton.BeginAnimation(OpacityProperty, fadeIn);
                
                ShowLoading(false);
            };

            UserDisplayNameTextBlock.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void ManualTestsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Sprawdź czy okno testów już istnieje i jest otwarte
                if (_manualTestingWindow == null || _manualTestingWindow.IsClosed)
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Tworzenie nowego okna testów");
                    // Przekaż kontekst użytkownika i serwis MSAL do okna testów
                    _manualTestingWindow = new ManualTestingWindow(_authResult, _msalAuthService);
                    
                    // Obsługa zamknięcia okna
                    _manualTestingWindow.Closed += (s, args) => 
                    {
                        System.Diagnostics.Debug.WriteLine("[MainWindow] Okno testów zostało zamknięte");
                        _manualTestingWindow = null;
                    };
                    
                    _manualTestingWindow.Show();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Aktywowanie istniejącego okna testów");
                    // Przenieś okno na pierwszy plan
                    _manualTestingWindow.WindowState = WindowState.Normal;
                    _manualTestingWindow.Activate();
                    _manualTestingWindow.Focus();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Błąd podczas otwierania okna testów: {ex}");
                ShowErrorDialog($"Błąd podczas otwierania okna testów manualnych: {ex.Message}", "Błąd");
            }
        }

        private void ShowLoading(bool show)
        {
            if (show)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 0.9, TimeSpan.FromSeconds(0.2));
                LoadingOverlay.BeginAnimation(OpacityProperty, fadeIn);
            }
            else
            {
                var fadeOut = new DoubleAnimation(0.9, 0, TimeSpan.FromSeconds(0.2));
                fadeOut.Completed += (s, e) => LoadingOverlay.Visibility = Visibility.Collapsed;
                LoadingOverlay.BeginAnimation(OpacityProperty, fadeOut);
            }
        }

        private void ShowErrorDialog(string message, string title)
        {
            // W przyszłości można użyć Material Design Dialog
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}