namespace TeamsManager.Api.Models.Configuration
{
    public class DatabaseConfiguration : BaseConfiguration
    {
        public string Provider { get; set; } = "SQLite";
        public string ConnectionString { get; set; } = "Data Source=%APPDATA%\\TeamsManager\\data\\teamsmanager.db";
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
