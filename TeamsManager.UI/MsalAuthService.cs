// Plik: TeamsManager.UI/Services/MsalAuthService.cs
using Microsoft.Identity.Client;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

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

public class MsalAuthService
{
    private readonly IPublicClientApplication _pca;
    private readonly string[]? _scopes;
    private readonly string _clientId;
    private readonly string _tenantId;

    private const string AppDataFolderName = "TeamsManager";
    private const string ConfigFileNameInAppData = "oauth_config.json";
    private const string DeveloperConfigFileName = "msalconfig.developer.json"; // Lokalny plik deweloperski

    public MsalAuthService()
    {
        MsalUiAppConfiguration config = LoadConfiguration();

        _clientId = config.AzureAd.ClientId ?? string.Empty;
        _tenantId = config.AzureAd.TenantId ?? string.Empty;
        _scopes = config.Scopes ?? new string[] { string.Empty };

        // Debug: Wyświetl scopes
        System.Diagnostics.Debug.WriteLine($"MSAL Config (UI): Loaded Scopes: [{string.Join(", ", _scopes)}]");

        if (string.IsNullOrWhiteSpace(_clientId) || string.IsNullOrWhiteSpace(_tenantId) || _scopes == null || _scopes.Length == 0 || string.IsNullOrWhiteSpace(_scopes[0]))
        {
            HandleMissingConfiguration($"Kluczowe ustawienia Azure AD (ClientId, TenantId, Scopes) nie zostały poprawnie załadowane dla MSAL. Sprawdź konfigurację aplikacji (np. w %APPDATA%\\{AppDataFolderName}\\{ConfigFileNameInAppData} lub plik deweloperski '{DeveloperConfigFileName}').");
            // W zależności od strategii, można rzucić wyjątek lub pozostawić _pca jako null
            // Jeśli rzucisz wyjątek, aplikacja prawdopodobnie się nie uruchomi bez konfiguracji.
            // Jeśli nie, metody AcquireToken* muszą sprawdzać _pca.
            _pca = null!; // Jawne przypisanie null, aby uniknąć błędu "Use of unassigned local variable" jeśli nie rzucamy wyjątku
            return; // Zakończ konstruktor, jeśli konfiguracja jest niepoprawna
        }

        var pcaBuilder = PublicClientApplicationBuilder.Create(_clientId)
            .WithAuthority($"{config.AzureAd.Instance.TrimEnd('/')}/{_tenantId}/v2.0");

        if (!string.IsNullOrWhiteSpace(config.AzureAd.RedirectUri))
        {
            pcaBuilder.WithRedirectUri(config.AzureAd.RedirectUri);
            System.Diagnostics.Debug.WriteLine($"MSAL Config (UI): Using RedirectUri from config: {config.AzureAd.RedirectUri}");
        }
        else
        {
            pcaBuilder.WithDefaultRedirectUri();
            System.Diagnostics.Debug.WriteLine("MSAL Config (UI): Using DefaultRedirectUri.");
        }
        _pca = pcaBuilder.Build();
    }

    private MsalUiAppConfiguration LoadConfiguration()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string configDirInAppData = Path.Combine(appDataPath, AppDataFolderName);
        string configFileInAppDataPath = Path.Combine(configDirInAppData, ConfigFileNameInAppData);

        MsalUiAppConfiguration? loadedConfig = null;

        // 1. Spróbuj wczytać z %APPDATA%\TeamsManager\oauth_config.json
        if (File.Exists(configFileInAppDataPath))
        {
            try
            {
                string json = File.ReadAllText(configFileInAppDataPath);
                loadedConfig = JsonSerializer.Deserialize<MsalUiAppConfiguration>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (IsUiConfigurationValid(loadedConfig))
                {
                    System.Diagnostics.Debug.WriteLine($"MSAL Config (UI): Loaded from user's AppData: '{configFileInAppDataPath}'. ClientId: {loadedConfig?.AzureAd.ClientId}");
                    return loadedConfig!;
                }
                System.Diagnostics.Debug.WriteLine($"MSAL Config (UI): Data in '{configFileInAppDataPath}' is invalid. Falling back...");
                loadedConfig = null; // Resetuj, aby przejść do fallbacku
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MSAL Config (UI): Error loading from AppData '{configFileInAppDataPath}': {ex.Message}. Falling back...");
                loadedConfig = null;
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"MSAL Config (UI): Config file not found in AppData: '{configFileInAppDataPath}'. Checking for developer config...");
        }

        // 2. Fallback dla dewelopera: spróbuj wczytać z msalconfig.developer.json (w katalogu aplikacji)
        if (File.Exists(DeveloperConfigFileName))
        {
            try
            {
                string json = File.ReadAllText(DeveloperConfigFileName);
                loadedConfig = JsonSerializer.Deserialize<MsalUiAppConfiguration>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (IsUiConfigurationValid(loadedConfig))
                {
                    System.Diagnostics.Debug.WriteLine($"MSAL Config (UI): Loaded from developer file: '{DeveloperConfigFileName}'. ClientId: {loadedConfig?.AzureAd.ClientId}");

                    // Opcjonalnie: Przy pierwszym udanym wczytaniu z pliku deweloperskiego,
                    // można zapisać tę konfigurację do %APPDATA% dla przyszłych uruchomień.
                    // To ułatwi "pierwsze uruchomienie" w środowisku deweloperskim.
                    try
                    {
                        if (!Directory.Exists(configDirInAppData))
                        {
                            Directory.CreateDirectory(configDirInAppData);
                        }
                        // Zapisz tylko jeśli plik w AppData jeszcze nie istnieje lub jeśli chcemy go nadpisać
                        if (!File.Exists(configFileInAppDataPath)) // lub jakaś inna logika decydująca o zapisie
                        {
                            string defaultConfigJson = JsonSerializer.Serialize(loadedConfig, new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true });
                            File.WriteAllText(configFileInAppDataPath, defaultConfigJson);
                            System.Diagnostics.Debug.WriteLine($"MSAL Config (UI): Copied developer config to '{configFileInAppDataPath}'.");
                        }
                    }
                    catch (Exception exCopy)
                    {
                        System.Diagnostics.Debug.WriteLine($"MSAL Config (UI): Failed to copy dev config to AppData: {exCopy.Message}");
                    }
                    return loadedConfig!;
                }
                System.Diagnostics.Debug.WriteLine($"MSAL Config (UI): Data in '{DeveloperConfigFileName}' is invalid. Falling back...");
                loadedConfig = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MSAL Config (UI): Error loading developer file '{DeveloperConfigFileName}': {ex.Message}. Falling back...");
                loadedConfig = null;
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"MSAL Config (UI): Developer config file '{DeveloperConfigFileName}' not found.");
        }

        // Jeśli konfiguracja nie została załadowana
        return new MsalUiAppConfiguration(); // Zwraca pustą konfigurację, błąd zostanie obsłużony w konstruktorze
    }

    private bool IsUiConfigurationValid(MsalUiAppConfiguration? config)
    {
        return config != null &&
               config.AzureAd != null &&
               !string.IsNullOrWhiteSpace(config.AzureAd.ClientId) &&
               !string.IsNullOrWhiteSpace(config.AzureAd.TenantId) &&
               config.Scopes != null &&
               config.Scopes.Length > 0 &&
               !string.IsNullOrWhiteSpace(config.Scopes[0]);
    }

    private void HandleMissingConfiguration(string message)
    {
        System.Diagnostics.Debug.WriteLine($"Krytyczny błąd konfiguracji MSAL (UI): {message}");
        MessageBox.Show(message + "\nAplikacja może nie działać poprawnie. Skonfiguruj ją lub skontaktuj się z administratorem.",
                        "Błąd Konfiguracji MSAL", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public async Task<AuthenticationResult?> AcquireTokenInteractiveAsync(Window window)
    {
        if (_pca == null) // Sprawdź, czy _pca zostało poprawnie zainicjowane
        {
            HandleMissingConfiguration("MSAL nie został poprawnie zainicjowany z powodu braku konfiguracji (ClientId/TenantId). Logowanie niemożliwe.");
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
                MessageBox.Show($"Błąd logowania MSAL: {msalEx.Message}", "Błąd Logowania", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MSAL Error Acquiring Token Silently: {ex}");
            MessageBox.Show($"Błąd MSAL: {ex.Message}", "Błąd Logowania", MessageBoxButton.OK, MessageBoxImage.Error);
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

            // Spróbuj pobrać token z cache
            var result = await _pca.AcquireTokenSilent(graphScopes, firstAccount).ExecuteAsync();
            
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
}