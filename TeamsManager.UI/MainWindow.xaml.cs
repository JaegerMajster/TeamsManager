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

        public MainWindow()
        {
            InitializeComponent();

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
                TestApiButton.IsEnabled = false;
                LogoutButton.IsEnabled = false;
                UserInfoTextBlock.Text = "Błąd konfiguracji MSAL";
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

            try
            {
                _authResult = await _msalAuthService.AcquireTokenInteractiveAsync(this);

                if (_authResult != null && !string.IsNullOrEmpty(_authResult.AccessToken))
                {
                    // Bezpieczne wyświetlanie fragmentu tokenu
                    string tokenFragment = _authResult.AccessToken.Substring(0, Math.Min(_authResult.AccessToken.Length, 20));
                    UserInfoTextBlock.Text = $"Zalogowano jako: {_authResult.Account?.Username}\nToken (fragment): {tokenFragment}...";

                    // Aktualizacja stanu przycisków
                    LogoutButton.IsEnabled = true;
                    LoginButton.IsEnabled = false;
                    TestApiButton.IsEnabled = true;

                    // Dostosuj rozmiar okna do nowej zawartości
                    this.SizeToContent = SizeToContent.Height;
                    await System.Threading.Tasks.Task.Delay(50);
                    this.SizeToContent = SizeToContent.Manual;
                }
                else
                {
                    UserInfoTextBlock.Text = "Logowanie nie powiodło się lub zostało anulowane.";
                    LogoutButton.IsEnabled = false;
                    LoginButton.IsEnabled = true;
                    TestApiButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd logowania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                UserInfoTextBlock.Text = "Wystąpił błąd podczas logowania.";
                LogoutButton.IsEnabled = false;
                LoginButton.IsEnabled = true;
                TestApiButton.IsEnabled = false;
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            // Sprawdzenie, czy serwis istnieje
            if (_msalAuthService != null)
            {
                await _msalAuthService.SignOutAsync();
            }

            _authResult = null;
            UserInfoTextBlock.Text = "Niezalogowany";

            // Aktualizacja stanu przycisków
            LogoutButton.IsEnabled = false;
            LoginButton.IsEnabled = true;
            TestApiButton.IsEnabled = false;

            // Dostosuj rozmiar okna do nowej zawartości
            this.SizeToContent = SizeToContent.Height;
            await System.Threading.Tasks.Task.Delay(50);
            this.SizeToContent = SizeToContent.Manual;
        }

        private async void TestApiButton_Click(object sender, RoutedEventArgs e)
        {
            if (_authResult == null || string.IsNullOrEmpty(_authResult.AccessToken))
            {
                MessageBox.Show("Nie jesteś zalogowany lub token dostępu jest niedostępny.",
                                "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // TODO: Warto przenieść ten adres URL do pliku konfiguracyjnego aplikacji UI lub ustawień
            string apiUrl = "https://localhost:7037/api/TestAuth/whoami"; // Upewnij się, że port jest poprawny!

            try
            {
                // Ustawienie nagłówka autoryzacji
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _authResult.AccessToken);

                UserInfoTextBlock.Text = $"Wysyłanie żądania do: {apiUrl}...";

                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Możesz zdeserializować odpowiedź, jeśli API zwraca strukturyzowany JSON
                    MessageBox.Show($"Odpowiedź z API (WhoAmI):\nStatus: {response.StatusCode}\nTreść:\n{responseBody}",
                                    "Sukces API", MessageBoxButton.OK, MessageBoxImage.Information);
                    UserInfoTextBlock.Text = $"Odpowiedź z API (sukces): {response.StatusCode}";
                }
                else
                {
                    MessageBox.Show($"Błąd wywołania API (WhoAmI):\nStatus: {response.StatusCode}\nOdpowiedź: {responseBody}",
                                    "Błąd API", MessageBoxButton.OK, MessageBoxImage.Error);
                    UserInfoTextBlock.Text = $"Odpowiedź z API (błąd): {response.StatusCode}";
                }
            }
            catch (HttpRequestException httpEx)
            {
                MessageBox.Show($"Błąd połączenia z API (WhoAmI): {httpEx.Message}\nUpewnij się, że API ({apiUrl}) jest uruchomione i dostępne.",
                                "Błąd Połączenia API", MessageBoxButton.OK, MessageBoxImage.Error);
                UserInfoTextBlock.Text = "Błąd połączenia z API.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wyjątek podczas wywołania API (WhoAmI): {ex.Message}",
                                "Wyjątek API", MessageBoxButton.OK, MessageBoxImage.Error);
                UserInfoTextBlock.Text = "Wyjątek podczas wywołania API.";
            }
        }
    }
}