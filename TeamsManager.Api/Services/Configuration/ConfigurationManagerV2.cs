using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Api.Models.Configuration;
using System.IO.Compression;

namespace TeamsManager.Api.Services.Configuration
{
    public class ConfigurationManagerV2 : IConfigurationManagerV2, IDisposable
    {
        private readonly AdvancedEncryptionService _encryption;
        private readonly ILogger<ConfigurationManagerV2> _logger;
        private readonly string _configPath;
        private FileSystemWatcher? _watcher;
        private bool _disposed;

        public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

        public ConfigurationManagerV2(
            AdvancedEncryptionService encryption,
            ILogger<ConfigurationManagerV2> logger)
        {
            _encryption = encryption ?? throw new ArgumentNullException(nameof(encryption));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TeamsManager");
            
            EnsureDirectoryStructure();
            InitializeFileWatcher();
        }

        public async Task<LoginSettingsConfiguration?> LoadLoginSettingsAsync()
        {
            return await GetConfigurationAsync<LoginSettingsConfiguration>("login-settings");
        }

        public async Task SaveLoginSettingsAsync(LoginSettingsConfiguration settings)
        {
            await SaveConfigurationAsync("login-settings", settings);
        }

        public async Task ClearLoginSettingsAsync()
        {
            try
            {
                var filePath = GetConfigFilePath("login-settings");
                if (File.Exists(filePath))
                {
                    // Backup przed usunięciem
                    await CreateBackupAsync("login-settings");
                    
                    File.Delete(filePath);
                    _logger.LogInformation("Wyczyszczono ustawienia logowania");
                    
                    // Powiadom o zmianie
                    ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs("login-settings", typeof(LoginSettingsConfiguration)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas czyszczenia ustawień logowania");
                throw;
            }
        }

        public async Task<ApplicationConfiguration?> LoadApplicationConfigurationAsync()
        {
            return await GetConfigurationAsync<ApplicationConfiguration>("application");
        }

        public async Task SaveApplicationConfigurationAsync(ApplicationConfiguration config)
        {
            await SaveConfigurationAsync("application", config);
        }

        public async Task<AzureAdConfiguration?> LoadAzureAdConfigurationAsync()
        {
            return await GetConfigurationAsync<AzureAdConfiguration>("azure-ad");
        }

        public async Task SaveAzureAdConfigurationAsync(AzureAdConfiguration config)
        {
            _logger.LogInformation("Rozpoczęcie zapisu konfiguracji Azure AD");
            
            if (config == null)
            {
                _logger.LogWarning("Konfiguracja Azure AD jest null - pomijam zapis");
                return;
            }
            
            _logger.LogInformation("Zapisywanie konfiguracji Azure AD...");
            
            await SaveConfigurationAsync("azure-ad", config);
            
            _logger.LogInformation("Konfiguracja Azure AD zapisana pomyślnie");
        }

        public async Task<T?> GetConfigurationAsync<T>(string configName) where T : class
        {
            try
            {
                var filePath = GetConfigFilePath(configName);
                
                if (!File.Exists(filePath))
                {
                    _logger.LogInformation("Plik konfiguracji {ConfigName} nie istnieje", configName);
                    return null;
                }

                var jsonContent = await File.ReadAllTextAsync(filePath);
                
                // Sprawdź czy plik jest zaszyfrowany
                if (IsEncrypted(configName))
                {
                    _logger.LogInformation("Odczytywanie zaszyfrowanego pliku {ConfigName}", configName);
                    
                    var encryptedData = JsonSerializer.Deserialize<EncryptedData>(jsonContent);
                    
                    if (encryptedData != null)
                    {
                        _logger.LogInformation("Rozpoczęcie odszyfrowywania danych");
                        
                        var decryptedJson = _encryption.Decrypt(encryptedData);
                        
                        // Użyj tej samej polityki nazewnictwa co przy zapisie
                        var jsonOptions = new JsonSerializerOptions 
                        { 
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        };
                        
                        return JsonSerializer.Deserialize<T>(decryptedJson, jsonOptions);
                    }
                    else
                    {
                        _logger.LogError("Nie udało się zdeserializować zaszyfrowanych danych");
                    }
                }
                else
                {
                    return JsonSerializer.Deserialize<T>(jsonContent);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wczytywania konfiguracji {ConfigName}", configName);
                throw;
            }
        }

        public async Task SaveConfigurationAsync<T>(string configName, T configuration) where T : class
        {
            try
            {
                _logger.LogInformation("Rozpoczęcie zapisu konfiguracji {ConfigName}", configName);
                
                if (configuration == null)
                    throw new ArgumentNullException(nameof(configuration));

                // Backup przed zapisem
                await CreateBackupAsync(configName);

                var filePath = GetConfigFilePath(configName);
                
                var jsonOptions = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string jsonContent;

                // Sprawdź czy plik powinien być zaszyfrowany
                if (IsEncrypted(configName))
                {
                    _logger.LogInformation("Konfiguracja {ConfigName} wymaga szyfrowania", configName);
                    var plainJson = JsonSerializer.Serialize(configuration, jsonOptions);
                    
                    var encryptedData = _encryption.Encrypt(plainJson);
                    _logger.LogInformation("Dane zaszyfrowane pomyślnie");
                    
                    jsonContent = JsonSerializer.Serialize(encryptedData, jsonOptions);
                    
                    await File.WriteAllTextAsync(filePath, jsonContent);
                    _logger.LogInformation("Zaszyfrowana konfiguracja zapisana do pliku");
                    
                    return;
                }
                else
                {
                    _logger.LogInformation("Konfiguracja {ConfigName} nie wymaga szyfrowania", configName);
                    jsonContent = JsonSerializer.Serialize(configuration, jsonOptions);
                }

                await File.WriteAllTextAsync(filePath, jsonContent);
                _logger.LogInformation("Konfiguracja zapisana do pliku");
                
                _logger.LogInformation("Konfiguracja {ConfigName} zapisana pomyślnie", configName);
                
                // Powiadom o zmianie
                ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(configName, typeof(T)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisywania konfiguracji {ConfigName}", configName);
                throw;
            }
        }

        public async Task<bool> ValidateConfigurationAsync()
        {
            try
            {
                var isValid = true;

                // Waliduj każdy typ konfiguracji
                var appConfig = await LoadApplicationConfigurationAsync();
                if (appConfig != null && !appConfig.IsValid())
                {
                    _logger.LogWarning("Konfiguracja aplikacji jest nieprawidłowa");
                    isValid = false;
                }

                var azureConfig = await LoadAzureAdConfigurationAsync();
                if (azureConfig != null && !azureConfig.IsValid())
                {
                    _logger.LogWarning("Konfiguracja Azure AD jest nieprawidłowa");
                    isValid = false;
                }

                var loginConfig = await LoadLoginSettingsAsync();
                if (loginConfig != null && !loginConfig.IsValid())
                {
                    _logger.LogWarning("Konfiguracja logowania jest nieprawidłowa");
                    isValid = false;
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas walidacji konfiguracji");
                return false;
            }
        }

        public async Task BackupConfigurationAsync()
        {
            try
            {
                var backupDir = Path.Combine(_configPath, "backups");
                Directory.CreateDirectory(backupDir);

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                var backupPath = Path.Combine(backupDir, $"config-backup-{timestamp}.zip");

                using var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create);
                
                // Backup wszystkich plików konfiguracyjnych
                var configDir = Path.Combine(_configPath, "config");
                var userDir = Path.Combine(_configPath, "user");

                await AddDirectoryToArchive(archive, configDir, "config");
                await AddDirectoryToArchive(archive, userDir, "user");

                _logger.LogInformation("Utworzono backup konfiguracji: {BackupPath}", backupPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas tworzenia backupu konfiguracji");
                throw;
            }
        }

        public async Task RestoreConfigurationAsync(string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                    throw new FileNotFoundException($"Plik backup nie istnieje: {backupPath}");

                // Backup obecnej konfiguracji przed restore
                await BackupConfigurationAsync();

                using var archive = ZipFile.OpenRead(backupPath);
                archive.ExtractToDirectory(_configPath, overwriteFiles: true);

                _logger.LogInformation("Przywrócono konfigurację z backupu: {BackupPath}", backupPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas przywracania konfiguracji z backupu");
                throw;
            }
        }

        private void EnsureDirectoryStructure()
        {
            var directories = new[]
            {
                Path.Combine(_configPath, "config"),
                Path.Combine(_configPath, "user"),
                Path.Combine(_configPath, "cache"),
                Path.Combine(_configPath, "logs"),
                Path.Combine(_configPath, "backups"),
                Path.Combine(_configPath, "data")
            };

            foreach (var dir in directories)
            {
                Directory.CreateDirectory(dir);
            }
        }

        private void InitializeFileWatcher()
        {
            try
            {
                _watcher = new FileSystemWatcher(_configPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
                };

                _watcher.Changed += OnConfigurationFileChanged;
                _watcher.Created += OnConfigurationFileChanged;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nie udało się zainicjalizować FileSystemWatcher");
            }
        }

        private void OnConfigurationFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Ignoruj zmiany w katalogu logs żeby uniknąć pętli z FileLoggerProvider
                if (e.FullPath.Contains("\\logs\\") || e.FullPath.Contains("/logs/"))
                {
                    return;
                }
                
                // Ignoruj zmiany w katalogu backups i cache
                if (e.FullPath.Contains("\\backups\\") || e.FullPath.Contains("/backups/") ||
                    e.FullPath.Contains("\\cache\\") || e.FullPath.Contains("/cache/"))
                {
                    return;
                }
                
                if (e.ChangeType == WatcherChangeTypes.Changed || e.ChangeType == WatcherChangeTypes.Created)
                {
                    var configName = Path.GetFileNameWithoutExtension(e.Name);
                    _logger.LogDebug("Wykryto zmianę w pliku konfiguracji: {ConfigName}", configName);
                    
                    // Powiadom o zmianie (bez określania typu)
                    ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(configName, typeof(object)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd podczas obsługi zmiany pliku konfiguracji");
            }
        }

        private string GetConfigFilePath(string configName)
        {
            var subDir = IsUserConfig(configName) ? "user" : "config";
            return Path.Combine(_configPath, subDir, $"{configName}.json");
        }

        private static bool IsEncrypted(string configName)
        {
            // Azure AD konfiguracja jest zawsze zaszyfrowana
            return configName == "azure-ad";
        }

        private static bool IsUserConfig(string configName)
        {
            // Konfiguracje użytkownika
            return configName is "login-settings" or "preferences" or "recent-activity";
        }

        private async Task CreateBackupAsync(string configName)
        {
            try
            {
                var filePath = GetConfigFilePath(configName);
                if (File.Exists(filePath))
                {
                    var backupDir = Path.Combine(_configPath, "backups");
                    Directory.CreateDirectory(backupDir);
                    
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                    var backupPath = Path.Combine(backupDir, $"{configName}-{timestamp}.json");
                    
                    File.Copy(filePath, backupPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nie udało się utworzyć backupu dla {ConfigName}", configName);
            }
        }

        private static async Task AddDirectoryToArchive(ZipArchive archive, string sourceDir, string entryPrefix)
        {
            if (!Directory.Exists(sourceDir))
                return;

            var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(sourceDir, file);
                var entryName = Path.Combine(entryPrefix, relativePath).Replace('\\', '/');
                
                var entry = archive.CreateEntry(entryName);
                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(file);
                await fileStream.CopyToAsync(entryStream);
            }
        }

        public async Task ReencryptForCurrentUserAsync(string configName)
        {
            try
            {
                _logger.LogInformation("Rozpoczęcie ponownego szyfrowania {ConfigName} dla bieżącego użytkownika", configName);
                
                if (!IsEncrypted(configName))
                {
                    _logger.LogWarning("Konfiguracja {ConfigName} nie wymaga szyfrowania", configName);
                    return;
                }

                var filePath = GetConfigFilePath(configName);
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Plik {ConfigName} nie istnieje", configName);
                    return;
                }

                // Wczytaj i odszyfruj obecne dane (z fallback)
                var jsonContent = await File.ReadAllTextAsync(filePath);
                var encryptedData = JsonSerializer.Deserialize<EncryptedData>(jsonContent);
                
                if (encryptedData == null)
                {
                    _logger.LogError("Nie można zdekodować danych z {ConfigName}", configName);
                    return;
                }

                var decryptedJson = _encryption.Decrypt(encryptedData);
                
                if (string.IsNullOrEmpty(decryptedJson) || decryptedJson == "{}")
                {
                    _logger.LogWarning("Nie udało się odzyskać danych z {ConfigName}", configName);
                    return;
                }

                // Ponownie zaszyfruj dla bieżącego użytkownika
                var newEncryptedData = _encryption.Encrypt(decryptedJson);
                
                // Backup przed zapisem
                await CreateBackupAsync(configName);
                
                // Zapisz ponownie zaszyfrowane dane
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var newJsonContent = JsonSerializer.Serialize(newEncryptedData, jsonOptions);
                
                await File.WriteAllTextAsync(filePath, newJsonContent);
                
                _logger.LogInformation("Pomyślnie ponownie zaszyfrowano {ConfigName} dla użytkownika {User}", configName, Environment.UserName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ponownego szyfrowania {ConfigName}", configName);
                throw;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _watcher?.Dispose();
                _disposed = true;
            }
        }
    }
} 