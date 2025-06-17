// Plik: TeamsManager.UI/Services/MsalAuthService.cs
using Microsoft.Identity.Client;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using TeamsManager.UI.Services.Abstractions;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Services.Configuration;
using TeamsManager.UI.Models.Configuration;
using Microsoft.Identity.Client.Broker;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.UI.Services
{
    // Definicje klas konfiguracyjnych
    public class MsalUiAppConfiguration
    {
        public AzureAdUiConfig AzureAd { get; set; } = new AzureAdUiConfig();
        public string[] Scopes { get; set; } = new string[] { "User.Read" };
    }

    public class AzureAdUiConfig
    {
        public string Instance { get; set; } = "https://login.microsoftonline.com/";
        public string? TenantId { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? RedirectUri { get; set; }
        public string? ApiScope { get; set; }
        public string? ApiBaseUrl { get; set; }
    }

    /// <summary>
    /// Serwis autentykacji MSAL (Microsoft Authentication Library)
    /// Obsługuje logowanie i zarządzanie tokenami Microsoft Identity Platform
    /// </summary>
    public class MsalAuthService : IMsalAuthService
    {
        private readonly IPublicClientApplication _publicClientApp;
        private readonly ILogger<MsalAuthService> _logger;
        private readonly GraphApiConfiguration _graphConfig;

        public MsalAuthService(
            IPublicClientApplication publicClientApp, 
            ILogger<MsalAuthService> logger,
            GraphApiConfiguration? graphConfig = null)
        {
            _publicClientApp = publicClientApp ?? throw new ArgumentNullException(nameof(publicClientApp));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _graphConfig = graphConfig ?? new GraphApiConfiguration();
        }

        /// <summary>
        /// Pobiera handle głównego okna aplikacji
        /// </summary>
        private IntPtr GetMainWindowHandle()
        {
            try
            {
                if (System.Windows.Application.Current?.MainWindow != null)
                {
                    var helper = new WindowInteropHelper(System.Windows.Application.Current.MainWindow);
                    return helper.Handle;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot get MainWindow handle, using IntPtr.Zero");
            }
            
            return IntPtr.Zero;
        }

        private void HandleMissingConfiguration(string message)
        {
            _logger.LogCritical("MSAL configuration error: {Message}", message);
            
            // Bezpieczne wyświetlenie MessageBox zgodnie z wzorcem obsługi błędów
            try
            {
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            message + "\nAplikacja może nie działać poprawnie. Skonfiguruj ją lub skontaktuj się z administratorem.",
                            "Błąd Konfiguracji MSAL", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error);
                    });
                }
                else
                {
                    _logger.LogWarning("Cannot show MessageBox - Application.Current or Dispatcher is null");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing configuration error MessageBox");
            }
        }

        public async Task<AuthenticationResult?> AcquireTokenInteractiveAsync(Window window)
        {
            // Sprawdź argumenty zgodnie z wzorcem implementacyjnym
            if (window == null)
                throw new ArgumentNullException(nameof(window));
            
            if (_publicClientApp == null) // Sprawdź, czy _publicClientApp zostało poprawnie zainicjowane
            {
                HandleMissingConfiguration("MSAL nie został poprawnie zainicjowany z powodu braku konfiguracji (ClientId/TenantId). Logowanie niemożliwe.");
                return null;
            }

            AuthenticationResult? authResult = null;
            
            try
            {
                // Najpierw próbuj SSO z istniejącymi kontami
                var accounts = await _publicClientApp.GetAccountsAsync();
                IAccount? accountToUse = accounts.FirstOrDefault();
                
                // Jeśli nie ma cached accounts, spróbuj z Windows OS account
                if (accountToUse == null)
                {
                    accountToUse = PublicClientApplication.OperatingSystemAccount;
                    _logger.LogDebug("MSAL: No cached accounts, trying Windows OS account for SSO");
                }
                else
                {
                    _logger.LogDebug("MSAL: Found cached account: {Username}", accountToUse.Username);
                }

                // Próba silent authentication (SSO)
                authResult = await _publicClientApp.AcquireTokenSilent(_graphConfig.Scopes.ReadOnlyScopes, accountToUse).ExecuteAsync();
                _logger.LogInformation("MSAL: Token acquired silently via SSO for user: {Username}", authResult.Account?.Username);
            }
            catch (MsalUiRequiredException ex)
            {
                _logger.LogDebug("MSAL: Silent auth failed, interactive authentication required. Reason: {Reason}", ex.ErrorCode);
                
                try
                {
                    // Fallback do interactive authentication z WAM - WYMUŚ wybór konta
                    authResult = await _publicClientApp.AcquireTokenInteractive(_graphConfig.Scopes.ReadOnlyScopes)
                                                   .WithPrompt(Prompt.ForceLogin) // WYMUŚ pełne logowanie (zmiana z SelectAccount)
                                                   .WithParentActivityOrWindow(new WindowInteropHelper(window).Handle)
                                                   .ExecuteAsync();
                                                   
                    _logger.LogInformation("MSAL: Token acquired interactively via WAM for user: {Username}", authResult.Account?.Username);
                }
                catch (MsalException msalEx)
                {
                    _logger.LogError(msalEx, "MSAL Error during interactive authentication: {ErrorCode}", msalEx.ErrorCode);
                    MessageBox.Show($"Błąd logowania MSAL: {msalEx.Message}\n\nKod błędu: {msalEx.ErrorCode}", 
                                   "Błąd Logowania", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MSAL: Unexpected error during authentication");
                MessageBox.Show($"Nieoczekiwany błąd MSAL: {ex.Message}", "Błąd Logowania", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            if (authResult != null)
            {
                _logger.LogDebug("MSAL: Authentication successful - User: {Username}, Tenant: {TenantId}", 
                               authResult.Account?.Username, authResult.TenantId);
                System.Diagnostics.Debug.WriteLine($"MSAL Access Token (fragment): {authResult.AccessToken?.Substring(0, Math.Min(authResult.AccessToken.Length, 20))}...");
            }
            
            return authResult;
        }

        public async Task SignOutAsync()
        {
            if (_publicClientApp == null)
            {
                System.Diagnostics.Debug.WriteLine("MSAL SignOut: PCA not properly initialized.");
                return;
            }
            
            // Wyczyść wszystkie cached accounts
            var accounts = await _publicClientApp.GetAccountsAsync();
            foreach (var account in accounts)
            {
                await _publicClientApp.RemoveAsync(account);
                _logger.LogDebug("MSAL: Removed cached account: {Username}", account.Username);
            }
            
            _logger.LogInformation("MSAL: All accounts signed out and cache cleared");
            System.Diagnostics.Debug.WriteLine("MSAL: User signed out.");
        }

        public async Task<string?> AcquireGraphTokenAsync()
        {
            if (_publicClientApp == null)
            {
                System.Diagnostics.Debug.WriteLine("MSAL AcquireGraphToken: PCA not properly initialized.");
                return null;
            }

            try
            {
                var accounts = await _publicClientApp.GetAccountsAsync();
                IAccount? firstAccount = accounts.FirstOrDefault();

                // Spróbuj pobrać token z cache
                var result = await _publicClientApp.AcquireTokenSilent(_graphConfig.Scopes.ReadOnlyScopes, firstAccount).ExecuteAsync();
                
                System.Diagnostics.Debug.WriteLine($"MSAL: Graph token acquired silently. Scopes: {string.Join(", ", result.Scopes)}");
                return result.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                System.Diagnostics.Debug.WriteLine("MSAL: Graph token requires user interaction");
                return null; // Nie możemy w tym momencie wyświetlić UI
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MSAL Error acquiring Graph token: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> AcquireGraphTokenInteractiveAsync(Window window)
        {
            // Sprawdź argumenty zgodnie z wzorcem implementacyjnym
            if (window == null)
                throw new ArgumentNullException(nameof(window));
            
            if (_publicClientApp == null)
            {
                System.Diagnostics.Debug.WriteLine("MSAL AcquireGraphTokenInteractive: PCA not properly initialized.");
                return null;
            }

            try
            {
                var accounts = await _publicClientApp.GetAccountsAsync();
                IAccount? firstAccount = accounts.FirstOrDefault();

                var result = await _publicClientApp.AcquireTokenInteractive(_graphConfig.Scopes.ReadOnlyScopes)
                                               .WithAccount(firstAccount)
                                               .WithParentActivityOrWindow(new WindowInteropHelper(window).Handle)
                                               .ExecuteAsync();
                
                System.Diagnostics.Debug.WriteLine($"MSAL: Graph token acquired interactively. Scopes: {string.Join(", ", result.Scopes)}");
                return result.AccessToken;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MSAL Error acquiring Graph token interactively: {ex.Message}");
                return null;
            }
        }

        public async Task<AuthenticationResult?> AcquireTokenSilentAsync()
        {
            if (_publicClientApp == null)
            {
                _logger.LogWarning("MSAL AcquireTokenSilent: PCA not properly initialized.");
                return null;
            }

            try
            {
                var accounts = await _publicClientApp.GetAccountsAsync();
                IAccount? accountToUse = accounts.FirstOrDefault();

                // Jeśli nie ma cached accounts, spróbuj z Windows OS account (SSO)
                if (accountToUse == null)
                {
                    accountToUse = PublicClientApplication.OperatingSystemAccount;
                    _logger.LogDebug("MSAL: No cached accounts, trying Windows OS account for SSO");
                }

                // Spróbuj pobrać token z cache lub SSO
                var result = await _publicClientApp.AcquireTokenSilent(_graphConfig.Scopes.ReadOnlyScopes, accountToUse).ExecuteAsync();
                
                _logger.LogDebug("MSAL: Token acquired silently for user: {Username}", result.Account?.Username);
                return result;
            }
            catch (MsalUiRequiredException)
            {
                _logger.LogDebug("MSAL AcquireTokenSilent: UI interaction required");
                return null; // Wymagana interakcja użytkownika
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MSAL Error acquiring token silently");
                return null;
            }
        }

        public async Task<string?> GetAccessTokenAsync()
        {
            var result = await AcquireTokenSilentAsync();
            return result?.AccessToken;
        }
    }
}
