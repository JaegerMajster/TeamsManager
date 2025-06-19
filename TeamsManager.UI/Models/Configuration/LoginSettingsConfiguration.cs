using System;

namespace TeamsManager.UI.Models.Configuration
{
    public class LoginSettingsConfiguration : BaseConfiguration
    {
        public bool RememberMe { get; set; } = true;
        public bool AutoLogin { get; set; } = true;
        public string? LastUserEmail { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public bool UseWindowsHello { get; set; } = true;
        public bool UseBroker { get; set; } = true;
        public int SessionTimeoutMinutes { get; set; } = 480; // 8 godzin
    }
} 