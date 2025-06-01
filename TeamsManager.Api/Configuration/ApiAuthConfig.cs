using Microsoft.Extensions.Configuration; // Dla IConfiguration
using System;
using System.IO;
using System.Text.Json;

namespace TeamsManager.Api.Configuration
{
    public class ApiAuthConfig
    {
        // Klasy konfiguracyjne dla API (mogą być w osobnym pliku, ale tu dla prostoty)
        public class ApiAzureAdConfig
        {
            public string Instance { get; set; } = "https://login.microsoftonline.com/";
            public string? TenantId { get; set; }
            public string? ClientId { get; set; } // To będzie Audience dla API (czyli ClientID aplikacji WPF)
        }

        public class ApiOAuthConfig
        {
            public ApiAzureAdConfig AzureAd { get; set; } = new ApiAzureAdConfig();
        }

        // Klasa pomocnicza do deserializacji pliku oauth_config.json (z UI)
        // Używamy jej, bo API będzie czytać ten sam plik, co UI
        // Nazwy właściwości muszą pasować do pliku JSON (PropertyNameCaseInsensitive pomoże)
        public class UiMsalConfigForApiDeserialization
        {
            public UiAzureAdConfigForApiDeserialization AzureAd { get; set; } = new UiAzureAdConfigForApiDeserialization();
            // Scopes i RedirectUri nie są potrzebne API do walidacji tokenów, więc można je pominąć
        }

        public class UiAzureAdConfigForApiDeserialization
        {
            public string Instance { get; set; } = "https://login.microsoftonline.com/";
            public string? TenantId { get; set; }
            public string? ClientId { get; set; } // ClientID aplikacji WPF
        }

        // Funkcja pomocnicza do wczytania konfiguracji OAuth dla API
        public static ApiOAuthConfig LoadApiOAuthConfig(IConfiguration traditionalConfiguration)
        {
            const string AppDataFolderName = "TeamsManager";
            const string ConfigFileName = "oauth_config.json"; // Ten sam plik co dla UI
            ApiOAuthConfig? configFromAppData = null;

            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string configDir = Path.Combine(appDataPath, AppDataFolderName);
                string configFileInAppDataPath = Path.Combine(configDir, ConfigFileName);

                if (File.Exists(configFileInAppDataPath))
                {
                    string json = File.ReadAllText(configFileInAppDataPath);
                    var uiConfigFileStructure = JsonSerializer.Deserialize<UiMsalConfigForApiDeserialization>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (uiConfigFileStructure?.AzureAd != null &&
                        !string.IsNullOrWhiteSpace(uiConfigFileStructure.AzureAd.ClientId) &&
                        !string.IsNullOrWhiteSpace(uiConfigFileStructure.AzureAd.TenantId))
                    {
                        configFromAppData = new ApiOAuthConfig
                        {
                            AzureAd = new ApiAzureAdConfig
                            {
                                Instance = uiConfigFileStructure.AzureAd.Instance,
                                TenantId = uiConfigFileStructure.AzureAd.TenantId,
                                ClientId = uiConfigFileStructure.AzureAd.ClientId // To jest ClientID aplikacji WPF, które API użyje jako Audience
                            }
                        };
                        System.Diagnostics.Debug.WriteLine($"OAuth Config (API): Loaded from AppData '{configFileInAppDataPath}'. TenantId: {configFromAppData.AzureAd.TenantId}, Audience (UI ClientId): {configFromAppData.AzureAd.ClientId}");
                        return configFromAppData;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"OAuth Config (API): Data in '{configFileInAppDataPath}' is invalid or incomplete. Falling back.");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"OAuth Config (API): File '{configFileInAppDataPath}' not found in AppData. Falling back to IConfiguration (UserSecrets/appsettings).");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OAuth Config (API): Error loading from AppData '{ex.Message}'. Falling back to IConfiguration (UserSecrets/appsettings).");
            }

            // Fallback na tradycyjną konfigurację z IConfiguration (appsettings.json / User Secrets)
            System.Diagnostics.Debug.WriteLine($"OAuth Config (API): Loading from IConfiguration (UserSecrets/appsettings.json).");
            return new ApiOAuthConfig
            {
                AzureAd = new ApiAzureAdConfig
                {
                    Instance = traditionalConfiguration["AzureAd:Instance"] ?? "https://login.microsoftonline.com/",
                    TenantId = traditionalConfiguration["AzureAd:TenantId"], // Odczyt z UserSecrets/appsettings
                    ClientId = traditionalConfiguration["AzureAd:ClientId"]  // ClientId aplikacji WPF (Audience) z UserSecrets/appsettings
                }
            };
        }
    }
}
