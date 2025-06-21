using TeamsManager.Core.Models.Configuration;

namespace TeamsManager.UI.Models.Configuration
{
    public class DatabaseConfiguration : BaseConfiguration
    {
        public string Provider { get; set; } = "SQLite";
        public string ConnectionString { get; set; } = GetMainAppDatabasePath();
        
        /// <summary>
        /// Pobiera ścieżkę do bazy danych głównej aplikacji (ta sama co UI)
        /// </summary>
        private static string GetMainAppDatabasePath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dbPath = System.IO.Path.Combine(appDataPath, "TeamsManager", "data", "teamsmanager.db");
            return $"Data Source={dbPath}";
        }
        public MigrationSettings Migrations { get; set; } = new();
        public PerformanceSettings Performance { get; set; } = new();
    }

    public class MigrationSettings
    {
        public bool AutoApply { get; set; } = true;
        public bool BackupBeforeMigration { get; set; } = true;
    }

    public class PerformanceSettings
    {
        public int ConnectionPoolSize { get; set; } = 10;
        public int CommandTimeout { get; set; } = 30;
    }
} 