using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Helper do zarządzania trwałym, zaszyfrowanym cache tokenów MSAL
    /// </summary>
    public static class MsalCacheHelper
    {
        private static readonly string CacheFileName = "teamsmanager_msal_cache.bin3";
        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TeamsManager", "TokenCache");

        private static readonly string KeyChainServiceName = "TeamsManager_TokenCache";
        private static readonly string KeyChainAccountName = "MSALCache";
        
        private static readonly string LinuxKeyRingSchema = "com.teamsmanager.tokencache";
        private static readonly string LinuxKeyRingCollection = "default";

        /// <summary>
        /// Włącza trwałe przechowywanie tokenów z szyfrowaniem
        /// </summary>
        /// <param name="pca">Public Client Application</param>
        /// <param name="logger">Logger do rejestrowania operacji</param>
        public static async Task EnableTokenCacheSerializationAsync(
            IPublicClientApplication pca, 
            ILogger logger)
        {
            try
            {
                // Upewnij się że katalog istnieje
                Directory.CreateDirectory(CacheDir);
                
                // Konfiguracja storage properties dla różnych platform
                var storageProperties = new StorageCreationPropertiesBuilder(CacheFileName, CacheDir)
                    .WithLinuxKeyring(
                        LinuxKeyRingSchema, 
                        LinuxKeyRingCollection, 
                        "TeamsManager Token Cache",
                        new KeyValuePair<string, string>("application", "TeamsManager"),
                        new KeyValuePair<string, string>("service", "MSAL"))
                    .WithMacKeyChain(KeyChainServiceName, KeyChainAccountName)
                    .Build();

                // Utwórz cache helper z enkrypcją
                var cacheHelper = await Microsoft.Identity.Client.Extensions.Msal.MsalCacheHelper
                    .CreateAsync(storageProperties);

                // Zarejestruj cache w MSAL
                cacheHelper.RegisterCache(pca.UserTokenCache);
                
                logger.LogInformation("MSAL Token Cache Serialization enabled. Cache location: {CacheLocation}", 
                                    Path.Combine(CacheDir, CacheFileName));
                
                // Opcjonalnie: zarejestruj eventy cache
                RegisterCacheEvents(pca.UserTokenCache, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enable MSAL token cache serialization. Using in-memory cache only.");
                // Nie rzucamy wyjątku - aplikacja może działać bez persistent cache
            }
        }

        /// <summary>
        /// Rejestruje eventy cache dla monitorowania (opcjonalne)
        /// </summary>
        private static void RegisterCacheEvents(ITokenCache tokenCache, ILogger logger)
        {
            tokenCache.SetBeforeAccessAsync(async (args) =>
            {
                logger.LogDebug("MSAL Cache: Before access - Account: {Account}, Scopes: {Scopes}", 
                              args.Account?.Username ?? "None", 
                              string.Join(", ", args.RequestScopes ?? new string[0]));
                await Task.CompletedTask;
            });

            tokenCache.SetAfterAccessAsync(async (args) =>
            {
                if (args.HasStateChanged)
                {
                    logger.LogDebug("MSAL Cache: After access - Cache state changed, tokens updated");
                }
                await Task.CompletedTask;
            });

            tokenCache.SetBeforeWriteAsync(async (args) =>
            {
                logger.LogDebug("MSAL Cache: Before write - Persisting token cache to disk");
                await Task.CompletedTask;
            });
        }

        /// <summary>
        /// Wyczyść cache tokenów (dla troubleshooting)
        /// </summary>
        public static async Task ClearTokenCacheAsync(ILogger logger)
        {
            try
            {
                var cacheFilePath = Path.Combine(CacheDir, CacheFileName);
                
                if (File.Exists(cacheFilePath))
                {
                    File.Delete(cacheFilePath);
                    logger.LogInformation("MSAL Token Cache cleared: {CacheFile}", cacheFilePath);
                }
                
                // Wyczyść też katalog jeśli jest pusty
                if (Directory.Exists(CacheDir) && Directory.GetFiles(CacheDir).Length == 0)
                {
                    Directory.Delete(CacheDir);
                    logger.LogInformation("MSAL Cache directory removed: {CacheDir}", CacheDir);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to clear MSAL token cache");
            }
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// Sprawdź status cache
        /// </summary>
        public static (bool Exists, long SizeBytes, DateTime LastModified) GetCacheStatus()
        {
            try
            {
                var cacheFilePath = Path.Combine(CacheDir, CacheFileName);
                
                if (File.Exists(cacheFilePath))
                {
                    var fileInfo = new FileInfo(cacheFilePath);
                    return (true, fileInfo.Length, fileInfo.LastWriteTime);
                }
            }
            catch
            {
                // Ignore errors
            }
            
            return (false, 0, DateTime.MinValue);
        }
    }
} 