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
    public string? RedirectUri { get; set; }
    public string? ApiScope { get; set; }
    public string? ApiBaseUrl { get; set; }
}

    public class MsalAuthService : IMsalAuthService
    {
        private readonly IPublicClientApplication? _pca;
        private readonly string[]? _scopes;
        private readonly ILogger<MsalAuthService> _logger;
        private readonly IMsalConfigurationProvider _configProvider;

        public MsalAuthService(
            IMsalConfigurationProvider configProvider,
            ILogger<MsalAuthService> logger)
        {
            _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var config = _configProvider.GetConfiguration();
            
            // Walidacja - zachowaj obecną logikę
            if (!config.IsValid())
            {
                _logger.LogError("MSAL configuration is invalid");
                HandleMissingConfiguration(
                    "Kluczowe ustawienia Azure AD (ClientId, TenantId, Scopes) nie zostały poprawnie załadowane dla MSAL.");
                _pca = null;
                _scopes = Array.Empty<string>();
                return;
            }

            _scopes = config.Scopes;
            
            // Debug: Wyświetl scopes
            _logger.LogDebug("MSAL Config (UI): Loaded Scopes: [{Scopes}]", string.Join(", ", _scopes));

            // Budowanie PCA z WAM support
            var pcaBuilder = PublicClientApplicationBuilder
                .Create(config.AzureAd.ClientId!)
                .WithAuthority($"{config.AzureAd.Instance?.TrimEnd('/')}/{config.AzureAd.TenantId}/v2.0");

            // Konfiguracja WAM Broker
            BrokerOptions brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows)
            {
                Title = "TeamsManager",
                ListOperatingSystemAccounts = true // Pokazuj konta Windows w account picker
            };
            
            pcaBuilder.WithBroker(brokerOptions);
            _logger.LogDebug("MSAL: WAM Broker enabled with title 'TeamsManager'");

            // Redirect URI - dla WAM potrzebujemy specjalny format
            if (!string.IsNullOrWhiteSpace(config.AzureAd.RedirectUri))
            {
                pcaBuilder.WithRedirectUri(config.AzureAd.RedirectUri);
                _logger.LogDebug("Using RedirectUri from config: {RedirectUri}", config.AzureAd.RedirectUri);
            }
            else
            {
                // Dla WAM dodajemy specjalny redirect URI
                var wamRedirectUri = $"ms-appx-web://microsoft.aad.brokerplugin/{config.AzureAd.ClientId}";
                pcaBuilder.WithRedirectUri(wamRedirectUri);
                _logger.LogDebug("Using WAM RedirectUri: {RedirectUri}", wamRedirectUri);
            }

            // Parent window handle będzie ustawiony przy każdym wywołaniu
            pcaBuilder.WithParentActivityOrWindow(() => GetMainWindowHandle());

            _pca = pcaBuilder.Build();
            _logger.LogInformation("MSAL initialized with WAM support");
            
            // Włącz trwałe przechowywanie tokenów z szyfrowaniem
            _ = Task.Run(async () =>
            {
                await MsalCacheHelper.EnableTokenCacheSerializationAsync(_pca, _logger);
            });
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
            
        if (_pca == null) // Sprawdź, czy _pca zostało poprawnie zainicjowane
        {
            HandleMissingConfiguration("MSAL nie został poprawnie zainicjowany z powodu braku konfiguracji (ClientId/TenantId). Logowanie niemożliwe.");
            return null;
        }

        AuthenticationResult? authResult = null;
        
        try
        {
            // Najpierw próbuj SSO z istniejącymi kontami
            var accounts = await _pca.GetAccountsAsync();
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
            authResult = await _pca.AcquireTokenSilent(_scopes, accountToUse).ExecuteAsync();
            _logger.LogInformation("MSAL: Token acquired silently via SSO for user: {Username}", authResult.Account?.Username);
        }
        catch (MsalUiRequiredException ex)
        {
            _logger.LogDebug("MSAL: Silent auth failed, interactive authentication required. Reason: {Reason}", ex.ErrorCode);
            
            try
            {
                // Fallback do interactive authentication z WAM - WYMUŚ wybór konta
                authResult = await _pca.AcquireTokenInteractive(_scopes)
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
        if (_pca == null)
        {
            System.Diagnostics.Debug.WriteLine("MSAL SignOut: PCA not properly initialized.");
            return;
        }
        
        // Wyczyść wszystkie cached accounts
        var accounts = await _pca.GetAccountsAsync();
        foreach (var account in accounts)
        {
            await _pca.RemoveAsync(account);
            _logger.LogDebug("MSAL: Removed cached account: {Username}", account.Username);
        }
        
        _logger.LogInformation("MSAL: All accounts signed out and cache cleared");
        System.Diagnostics.Debug.WriteLine("MSAL: User signed out.");
    }

    public async Task<string?> AcquireGraphTokenAsync()
    {
        if (_pca == null)
        {
            System.Diagnostics.Debug.WriteLine("MSAL AcquireGraphToken: PCA not properly initialized.");
            return null;
        }

        // Microsoft Graph scopes
        string[] graphScopes = { "https://graph.microsoft.com/User.Read", "https://graph.microsoft.com/User.ReadBasic.All" };
        
        try
        {
            var accounts = await _pca.GetAccountsAsync();
            IAccount? firstAccount = accounts.FirstOrDefault();

            // Spr�buj pobra� token z cache
            var result = await _pca.AcquireTokenSilent(graphScopes, firstAccount).ExecuteAsync();
            
            System.Diagnostics.Debug.WriteLine($"MSAL: Graph token acquired silently. Scopes: {string.Join(", ", result.Scopes)}");
            return result.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            System.Diagnostics.Debug.WriteLine("MSAL: Graph token requires user interaction");
            return null; // Nie mo�emy w tym momencie wy�wietli� UI
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
            
        if (_pca == null)
        {
            System.Diagnostics.Debug.WriteLine("MSAL AcquireGraphTokenInteractive: PCA not properly initialized.");
            return null;
        }

        // Microsoft Graph scopes
        string[] graphScopes = { "https://graph.microsoft.com/User.Read", "https://graph.microsoft.com/User.ReadBasic.All" };
        
        try
        {
            var accounts = await _pca.GetAccountsAsync();
            IAccount? firstAccount = accounts.FirstOrDefault();

            var result = await _pca.AcquireTokenInteractive(graphScopes)
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
        if (_pca == null)
        {
            _logger.LogWarning("MSAL AcquireTokenSilent: PCA not properly initialized.");
            return null;
        }

        try
        {
            var accounts = await _pca.GetAccountsAsync();
            IAccount? accountToUse = accounts.FirstOrDefault();

            // Jeśli nie ma cached accounts, spróbuj z Windows OS account (SSO)
            if (accountToUse == null)
            {
                accountToUse = PublicClientApplication.OperatingSystemAccount;
                _logger.LogDebug("MSAL: No cached accounts, trying Windows OS account for SSO");
            }

            // Spróbuj pobrać token z cache lub SSO
            var result = await _pca.AcquireTokenSilent(_scopes, accountToUse).ExecuteAsync();
            
            _logger.LogDebug("MSAL: Token acquired silently for user: {Username}", result.Account?.Username);
            return result;
        }
        catch (MsalUiRequiredException)
        {
            _logger.LogDebug("MSAL AcquireTokenSilent: UI interaction required");
            return null; // Wymagana interakcja u�ytkownika
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
