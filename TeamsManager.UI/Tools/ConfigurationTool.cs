using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Services.Configuration;

namespace TeamsManager.UI.Tools
{
    public class ConfigurationTool
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConfigurationTool> _logger;

        public ConfigurationTool(IServiceProvider serviceProvider, ILogger<ConfigurationTool> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task BackupAsync()
        {
            try
            {
                Console.WriteLine("🔄 Tworzenie backupu konfiguracji...");
                
                var configManager = _serviceProvider.GetRequiredService<IConfigurationManagerV2>();
                await configManager.BackupConfigurationAsync();
                
                Console.WriteLine("✅ Backup konfiguracji utworzony pomyślnie");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd podczas tworzenia backupu: {ex.Message}");
                _logger.LogError(ex, "Błąd podczas tworzenia backupu konfiguracji");
            }
        }

        public async Task RestoreAsync(string backupPath)
        {
            try
            {
                Console.WriteLine($"🔄 Przywracanie konfiguracji z: {backupPath}");
                
                if (!File.Exists(backupPath))
                {
                    Console.WriteLine($"❌ Plik backup nie istnieje: {backupPath}");
                    return;
                }

                var configManager = _serviceProvider.GetRequiredService<IConfigurationManagerV2>();
                await configManager.RestoreConfigurationAsync(backupPath);
                
                Console.WriteLine("✅ Konfiguracja przywrócona pomyślnie");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd podczas przywracania: {ex.Message}");
                _logger.LogError(ex, "Błąd podczas przywracania konfiguracji");
            }
        }

        public async Task ValidateAsync()
        {
            try
            {
                Console.WriteLine("🔄 Walidacja konfiguracji...");
                
                var configManager = _serviceProvider.GetRequiredService<IConfigurationManagerV2>();
                var isValid = await configManager.ValidateConfigurationAsync();
                
                if (isValid)
                {
                    Console.WriteLine("✅ Konfiguracja jest prawidłowa");
                }
                else
                {
                    Console.WriteLine("❌ Konfiguracja zawiera błędy");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd podczas walidacji: {ex.Message}");
                _logger.LogError(ex, "Błąd podczas walidacji konfiguracji");
            }
        }

        public async Task MigrateAsync()
        {
            try
            {
                Console.WriteLine("🔄 Migracja konfiguracji...");
                
                var initializer = _serviceProvider.GetRequiredService<ConfigurationInitializer>();
                await initializer.InitializeAsync();
                
                Console.WriteLine("✅ Migracja zakończona pomyślnie");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd podczas migracji: {ex.Message}");
                _logger.LogError(ex, "Błąd podczas migracji konfiguracji");
            }
        }

        public async Task ResetAsync()
        {
            try
            {
                Console.WriteLine("🔄 Resetowanie konfiguracji do domyślnych wartości...");
                
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TeamsManager");

                // Backup przed resetem
                var configManager = _serviceProvider.GetRequiredService<IConfigurationManagerV2>();
                await configManager.BackupConfigurationAsync();
                
                // Usuń istniejące pliki konfiguracyjne
                var configDir = Path.Combine(configPath, "config");
                var userDir = Path.Combine(configPath, "user");
                
                if (Directory.Exists(configDir))
                    Directory.Delete(configDir, true);
                if (Directory.Exists(userDir))
                    Directory.Delete(userDir, true);

                // Zainicjalizuj domyślną konfigurację
                var initializer = _serviceProvider.GetRequiredService<ConfigurationInitializer>();
                await initializer.InitializeAsync();
                
                Console.WriteLine("✅ Konfiguracja zresetowana do domyślnych wartości");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd podczas resetowania: {ex.Message}");
                _logger.LogError(ex, "Błąd podczas resetowania konfiguracji");
            }
        }

        public async Task InfoAsync()
        {
            try
            {
                Console.WriteLine("📋 Informacje o konfiguracji TeamsManager V2.0");
                Console.WriteLine(new string('=', 50));
                
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TeamsManager");

                Console.WriteLine($"📁 Lokalizacja: {configPath}");
                Console.WriteLine($"📅 Data sprawdzenia: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();

                // Sprawdź strukturę katalogów
                var directories = new[] { "config", "user", "cache", "logs", "backups", "data" };
                Console.WriteLine("📂 Struktura katalogów:");
                foreach (var dir in directories)
                {
                    var fullPath = Path.Combine(configPath, dir);
                    var exists = Directory.Exists(fullPath);
                    var status = exists ? "✅" : "❌";
                    Console.WriteLine($"   {status} {dir}/");
                }

                Console.WriteLine();

                // Sprawdź pliki konfiguracyjne
                var configFiles = new[] { "application.json", "azure-ad.json", "database.json", "features.json" };
                var userFiles = new[] { "login-settings.json", "preferences.json" };

                Console.WriteLine("📄 Pliki konfiguracyjne:");
                foreach (var file in configFiles)
                {
                    var fullPath = Path.Combine(configPath, "config", file);
                    var exists = File.Exists(fullPath);
                    var status = exists ? "✅" : "❌";
                    var encrypted = file == "azure-ad.json" ? " 🔒" : "";
                    Console.WriteLine($"   {status} config/{file}{encrypted}");
                }

                Console.WriteLine("👤 Pliki użytkownika:");
                foreach (var file in userFiles)
                {
                    var fullPath = Path.Combine(configPath, "user", file);
                    var exists = File.Exists(fullPath);
                    var status = exists ? "✅" : "❌";
                    Console.WriteLine($"   {status} user/{file}");
                }

                // Walidacja
                Console.WriteLine();
                var configManager = _serviceProvider.GetRequiredService<IConfigurationManagerV2>();
                var isValid = await configManager.ValidateConfigurationAsync();
                var validationStatus = isValid ? "✅ Prawidłowa" : "❌ Zawiera błędy";
                Console.WriteLine($"🔍 Status walidacji: {validationStatus}");
                
                Console.WriteLine(new string('=', 50));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd podczas pobierania informacji: {ex.Message}");
                _logger.LogError(ex, "Błąd podczas pobierania informacji o konfiguracji");
            }
        }
    }
} 