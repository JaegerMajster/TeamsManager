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
            
            // Walidacja - zachowaj obecn� logik�
            if (!config.IsValid())
            {
                _logger.LogError("MSAL configuration is invalid");
                HandleMissingConfiguration(
                    "Kluczowe ustawienia Azure AD (ClientId, TenantId, Scopes) nie zosta�y poprawnie za�adowane dla MSAL.");
                _pca = null;
                _scopes = Array.Empty<string>();
                return;
            }

            _scopes = config.Scopes;
            
            // Debug: Wy�wietl scopes
            _logger.LogDebug("MSAL Config (UI): Loaded Scopes: [{Scopes}]", string.Join(", ", _scopes));

            // Budowanie PCA - bez zmian w logice
            var pcaBuilder = PublicClientApplicationBuilder
                .Create(config.AzureAd.ClientId!)
                .WithAuthority($"{config.AzureAd.Instance?.TrimEnd('/')}/{config.AzureAd.TenantId}/v2.0");

            if (!string.IsNullOrWhiteSpace(config.AzureAd.RedirectUri))
            {
                pcaBuilder.WithRedirectUri(config.AzureAd.RedirectUri);
                _logger.LogDebug("Using RedirectUri from config: {RedirectUri}", config.AzureAd.RedirectUri);
            }
            else
            {
                pcaBuilder.WithDefaultRedirectUri();
                _logger.LogDebug("Using DefaultRedirectUri");
            }

            _pca = pcaBuilder.Build();
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
            
        if (_pca == null) // Sprawd�, czy _pca zosta�o poprawnie zainicjowane
        {
            HandleMissingConfiguration("MSAL nie zosta� poprawnie zainicjowany z powodu braku konfiguracji (ClientId/TenantId). Logowanie niemo�liwe.");
            return null;
        }

        AuthenticationResult? authResult = null;
        var accounts = await _pca.GetAccountsAsync();
        IAccount? firstAccount = accounts.FirstOrDefault();

        try
        {
            authResult = await _pca.AcquireTokenSilent(_scopes, firstAccount).ExecuteAsync();
            System.Diagnostics.Debug.WriteLine("MSAL: Token Acquired Silently.");
        }
        catch (MsalUiRequiredException)
        {
            System.Diagnostics.Debug.WriteLine("MSAL: UI required for auth. Acquiring token interactively.");
            try
            {
                authResult = await _pca.AcquireTokenInteractive(_scopes)
                                       .WithAccount(firstAccount)
                                       .WithParentActivityOrWindow(new WindowInteropHelper(window).Handle)
                                       .ExecuteAsync();
                System.Diagnostics.Debug.WriteLine("MSAL: Token Acquired Interactively.");
            }
            catch (MsalException msalEx)
            {
                System.Diagnostics.Debug.WriteLine($"MSAL Error Acquiring Token Interactively: {msalEx}");
                MessageBox.Show($"B��d logowania MSAL: {msalEx.Message}", "B��d Logowania", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MSAL Error Acquiring Token Silently: {ex}");
            MessageBox.Show($"B��d MSAL: {ex.Message}", "B��d Logowania", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }

        if (authResult != null)
        {
            System.Diagnostics.Debug.WriteLine($"MSAL Access Token (fragment): {authResult.AccessToken?.Substring(0, Math.Min(authResult.AccessToken.Length, 20))}...");
            System.Diagnostics.Debug.WriteLine($"MSAL User UPN: {authResult.Account?.Username}");
            System.Diagnostics.Debug.WriteLine($"MSAL Tenant ID from Token: {authResult.TenantId}");
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
        var accounts = await _pca.GetAccountsAsync();
        foreach (var account in accounts)
        {
            await _pca.RemoveAsync(account);
        }
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
            IAccount? firstAccount = accounts.FirstOrDefault();

            if (firstAccount == null)
            {
                _logger.LogDebug("MSAL AcquireTokenSilent: No cached account found");
                return null;
            }

            // Spr�buj pobra� token z cache
            var result = await _pca.AcquireTokenSilent(_scopes, firstAccount).ExecuteAsync();
            
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
