using Microsoft.Identity.Client;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
                MessageBox.Show(ex.Message + "\nAplikacja nie może kontynuować bez poprawnej konfiguracji logowania.",
                                "Krytyczny Błąd Konfiguracji", MessageBoxButton.OK, MessageBoxImage.Error);

                // Zablokowanie funkcjonalności logowania
                LoginButton.IsEnabled = false;
                LogoutButton.IsEnabled = false;
                ManualTestsButton.IsEnabled = false;
                UserDisplayNameTextBlock.Text = "Błąd konfiguracji MSAL";
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
                MessageBox.Show("Serwis autentykacji MSAL nie został poprawnie zainicjowany z powodu błędu konfiguracji.",
                                "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var startTime = DateTime.Now;
            try
            {
                _authResult = await _msalAuthService.AcquireTokenInteractiveAsync(this);
                var duration = DateTime.Now - startTime;

                if (_authResult != null && !string.IsNullOrEmpty(_authResult.AccessToken))
                {
                    // Pobierz profil użytkownika z Microsoft Graph
                    await LoadUserProfileAsync();

                    // Aktualizacja stanu przycisków
                    LogoutButton.IsEnabled = true;
                    LoginButton.IsEnabled = false;
                    ManualTestsButton.IsEnabled = true; // Aktywuj przycisk testów po zalogowaniu

                    // Przekaż wynik logowania do okna testów jeśli jest otwarte
                    if (_manualTestingWindow != null)
                    {
                        await _manualTestingWindow.SaveLoginResultToSession(true, 
                            $"Pomyślne logowanie użytkownika: {_authResult.Account?.Username}", duration);
                    }

                    // Dostosuj rozmiar okna do nowej zawartości
                    this.SizeToContent = SizeToContent.Height;
                    await System.Threading.Tasks.Task.Delay(50);
                    this.SizeToContent = SizeToContent.Manual;
                }
                else
                {
                    ResetUserInterface();
                    LogoutButton.IsEnabled = false;
                    LoginButton.IsEnabled = true;
                    ManualTestsButton.IsEnabled = false; // Dezaktywuj przycisk testów jeśli logowanie nie powiodło się

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
                MessageBox.Show($"Błąd logowania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetUserInterface();
                LogoutButton.IsEnabled = false;
                LoginButton.IsEnabled = true;
                ManualTestsButton.IsEnabled = false; // Dezaktywuj przycisk testów przy błędzie

                // Przekaż wynik logowania do okna testów jeśli jest otwarte
                if (_manualTestingWindow != null)
                {
                    await _manualTestingWindow.SaveLoginResultToSession(false, 
                        $"Błąd podczas logowania: {ex.Message}", duration);
                }
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
                    UserInfoTextBlock.Text = "Brak dostępu do Microsoft Graph - brak odpowiedniego tokenu";
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
                        UserInfoTextBlock.Text = $"Zalogowano jako: {userProfile.DisplayName}";

                        // Pobierz avatar użytkownika jeśli dostępny
                        if (testResult.CanAccessPhoto)
                        {
                            var userPhoto = await _graphUserProfileService.GetUserPhotoAsync(graphToken);
                            if (userPhoto != null)
                            {
                                DefaultUserIcon.Visibility = System.Windows.Visibility.Hidden;
                                UserAvatarImage.Fill = new ImageBrush
                                {
                                    ImageSource = userPhoto,
                                    Stretch = Stretch.UniformToFill
                                };
                                UserAvatarImage.Visibility = System.Windows.Visibility.Visible;
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Profil załadowany: {userProfile.DisplayName}");
                        return;
                    }
                }

                // Jeśli nie udało się pobrać profilu, pokaż informacje z podstawowego tokenu
                UserDisplayNameTextBlock.Text = _authResult.Account?.Username ?? "Użytkownik";
                UserInfoTextBlock.Text = $"Brak dostępu do Microsoft Graph. Status: /me={testResult.MeEndpointStatus}, /photo={testResult.PhotoEndpointStatus}";
                
                if (!string.IsNullOrEmpty(testResult.ErrorMessage))
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Graph API Error: {testResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Błąd podczas ładowania profilu: {ex.Message}");
                UserDisplayNameTextBlock.Text = _authResult.Account?.Username ?? "Użytkownik";
                UserInfoTextBlock.Text = $"Błąd podczas ładowania profilu: {ex.Message}";
            }
        }

        private void ResetUserInterface()
        {
            UserDisplayNameTextBlock.Text = "Niezalogowany";
            UserInfoTextBlock.Text = "";
            UserInfoTextBlock.Visibility = Visibility.Collapsed;
            UserAvatarImage.Visibility = Visibility.Collapsed;
            DefaultUserIcon.Visibility = Visibility.Visible;
            UserAvatarBrush.ImageSource = null;
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            // Sprawdzenie, czy serwis istnieje
            if (_msalAuthService != null)
            {
                await _msalAuthService.SignOutAsync();
            }

            _authResult = null;
            ResetUserInterface();

            // Aktualizacja stanu przycisków
            LogoutButton.IsEnabled = false;
            LoginButton.IsEnabled = true;
            ManualTestsButton.IsEnabled = false; // Dezaktywuj przycisk testów po wylogowaniu

            // Dostosuj rozmiar okna do nowej zawartości
            this.SizeToContent = SizeToContent.Height;
            await System.Threading.Tasks.Task.Delay(50);
            this.SizeToContent = SizeToContent.Manual;
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
                MessageBox.Show($"Błąd podczas otwierania okna testów manualnych: {ex.Message}", 
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}