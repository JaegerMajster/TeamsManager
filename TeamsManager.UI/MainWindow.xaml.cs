using Microsoft.Identity.Client;
using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using TeamsManager.UI.Views;
using TeamsManager.UI.Services;
using TeamsManager.UI.Services.Configuration;
using TeamsManager.UI.Views.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI
{
    public partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private readonly IMsalAuthService? _msalAuthService;
        private AuthenticationResult? _authResult;
        private readonly HttpClient _httpClient = new HttpClient();
        private ManualTestingWindow? _manualTestingWindow;
        private DashboardWindow? _dashboardWindow;
        private readonly IGraphUserProfileService _graphUserProfileService;

        /// <summary>
        /// Konstruktor MainWindow z Dependency Injection
        /// </summary>
        /// <param name="msalAuthService">Serwis autentykacji MSAL</param>
        /// <param name="graphUserProfileService">Serwis profilu użytkownika Graph</param>
        public MainWindow(IMsalAuthService msalAuthService, IGraphUserProfileService graphUserProfileService)
        {
            InitializeComponent();

            // Walidacja argumentów
            _msalAuthService = msalAuthService ?? throw new ArgumentNullException(nameof(msalAuthService));
            _graphUserProfileService = graphUserProfileService ?? throw new ArgumentNullException(nameof(graphUserProfileService));

            // Inicjalizacja UI na podstawie dostępności serwisów
            InitializeAuthenticationUI();

            // Ustaw ciemny motyw dla tego okna
            this.SourceInitialized += (s, e) =>
            {
                var helper = new WindowInteropHelper(this);
                SetWindowToDarkMode(helper.Handle);
            };
        }

        /// <summary>
        /// Inicjalizuje UI uwierzytelniania i sprawdza dostępność serwisów
        /// </summary>
        private void InitializeAuthenticationUI()
        {
            try
            {
                // Sprawdź czy serwisy są poprawnie skonfigurowane
                if (_msalAuthService == null)
                {
                    ShowErrorDialog("Serwis autentykacji MSAL nie został poprawnie zainicjowany.",
                                    "Krytyczny Błąd Konfiguracji");
                    DisableAuthenticationFeatures();
                    return;
                }

                // Serwisy są dostępne - normalna inicjalizacja
                System.Diagnostics.Debug.WriteLine("[MainWindow] Serwisy MSAL i Graph zainicjowane pomyślnie przez DI");
            }
            catch (Exception ex)
            {
                ShowErrorDialog($"Błąd podczas inicjalizacji: {ex.Message}",
                                "Błąd Konfiguracji");
                DisableAuthenticationFeatures();
            }
        }

        /// <summary>
        /// Wyłącza funkcje uwierzytelniania w przypadku błędów konfiguracji
        /// </summary>
        private void DisableAuthenticationFeatures()
        {
            LoginButton.IsEnabled = false;
            LogoutButton.IsEnabled = false;
            ManualTestsButton.IsEnabled = false;
            UserDisplayNameTextBlock.Text = "Błąd konfiguracji";
            UserInfoTextBlock.Text = "Serwisy nie zostały poprawnie skonfigurowane";
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

            DashboardButton.Visibility = Visibility.Visible;
            DashboardButton.IsEnabled = true;
            DashboardButton.BeginAnimation(OpacityProperty, fadeAnimation);

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

            DashboardButton.IsEnabled = false;
            DashboardButton.Visibility = Visibility.Collapsed;

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

                DashboardButton.IsEnabled = false;
                DashboardButton.Visibility = Visibility.Collapsed;

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
                    
                    try
                    {
                        // Próba utworzenia przez DI
                        _manualTestingWindow = App.ServiceProvider.GetRequiredService<ManualTestingWindow>();
                        System.Diagnostics.Debug.WriteLine("[MainWindow] ManualTestingWindow created via DI");
                    }
                    catch (Exception diEx)
                    {
                        // Fallback - jeśli DI nie zadziała
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] DI failed for ManualTestingWindow: {diEx.Message}");
                        ShowErrorDialog("Nie można otworzyć okna testów.\nSprawdź konfigurację aplikacji.", "Błąd");
                        return;
                    }
                    
                    // Ustaw kontekst użytkownika po utworzeniu
                    _manualTestingWindow.SetAuthenticationContext(_authResult);

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
                    
                    // Aktualizuj kontekst jeśli okno już istnieje
                    _manualTestingWindow.SetAuthenticationContext(_authResult);
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

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Sprawdź czy okno Dashboard już istnieje i jest otwarte
                if (_dashboardWindow == null || !_dashboardWindow.IsVisible)
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Tworzenie nowego okna Dashboard");
                    
                    try
                    {
                        // Tworzenie przez DI
                        _dashboardWindow = App.ServiceProvider.GetRequiredService<DashboardWindow>();

                        // Obsługa zamknięcia okna
                        _dashboardWindow.Closed += (s, args) =>
                        {
                            System.Diagnostics.Debug.WriteLine("[MainWindow] Okno Dashboard zostało zamknięte");
                            _dashboardWindow = null;
                        };

                        _dashboardWindow.Show();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Błąd tworzenia Dashboard przez DI: {ex.Message}");
                        // Fallback - tworzenie bezpośrednie
                        _dashboardWindow = new DashboardWindow();
                        _dashboardWindow.Show();
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Aktywowanie istniejącego okna Dashboard");
                    // Przenieś okno na pierwszy plan
                    _dashboardWindow.WindowState = WindowState.Normal;
                    _dashboardWindow.Activate();
                    _dashboardWindow.Focus();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Błąd podczas otwierania okna Dashboard: {ex}");
                ShowErrorDialog($"Błąd podczas otwierania Dashboard'a: {ex.Message}", "Błąd");
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

        /// <summary>
        /// Metoda do rekonfiguracji aplikacji - usuwa konfigurację i restartuje
        /// </summary>
        private void ReconfigureApplication()
        {
            var result = MessageBox.Show(
                "Czy chcesz zmienić konfigurację aplikacji?\n\nAplikacja zostanie zrestartowana.",
                "Zmiana konfiguracji",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Usuń pliki konfiguracyjne
                    var configManager = new Services.Configuration.ConfigurationManager();
                    configManager.DeleteConfiguration();

                    // Restart aplikacji
                    System.Diagnostics.Process.Start(System.Windows.Application.ResourceAssembly.Location);
                    System.Windows.Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Błąd podczas usuwania konfiguracji: {ex.Message}",
                        "Błąd",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        // Handler dla menu item (dodaj do MainWindow.xaml)
        private void MenuItem_Reconfigure_Click(object sender, RoutedEventArgs e)
        {
            ReconfigureApplication();
        }

        // Handler dla menu Exit
        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        // Handler dla menu About
        private void MenuItem_About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Teams Manager v1.0\n\n" +
                "Aplikacja do zarządzania Microsoft Teams\n" +
                "© 2024 Teams Manager\n\n" +
                "Powered by:\n" +
                "• Microsoft Graph API\n" +
                "• Azure Active Directory\n" +
                "• MSAL.NET",
                "O programie",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}