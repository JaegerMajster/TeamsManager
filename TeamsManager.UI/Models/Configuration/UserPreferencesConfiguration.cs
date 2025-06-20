using System;
using TeamsManager.Core.Models.Configuration;

namespace TeamsManager.UI.Models.Configuration
{
    public class UserPreferencesConfiguration : BaseConfiguration
    {
        public UiPreferences Ui { get; set; } = new();
        public NotificationPreferences Notifications { get; set; } = new();
        public PerformancePreferences Performance { get; set; } = new();
    }

    public class UiPreferences
    {
        public string Theme { get; set; } = "Dark";
        public string Language { get; set; } = "pl-PL";
        public bool ShowWelcomeScreen { get; set; } = true;
        public bool AutoSaveChanges { get; set; } = true;
        public int RefreshIntervalSeconds { get; set; } = 30;
    }

    public class NotificationPreferences
    {
        public bool ShowDesktopNotifications { get; set; } = true;
        public bool ShowInAppNotifications { get; set; } = true;
        public bool PlaySounds { get; set; } = false;
        public bool EmailNotifications { get; set; } = false;
    }

    public class PerformancePreferences
    {
        public bool EnableCaching { get; set; } = true;
        public int CacheExpiryMinutes { get; set; } = 15;
        public bool PreloadData { get; set; } = true;
        public int MaxConcurrentOperations { get; set; } = 5;
    }
} 