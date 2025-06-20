using System;

namespace TeamsManager.Api.Models.Configuration
{
    public abstract class BaseConfiguration
    {
        public string Version { get; set; } = "2.0";
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public string ConfigType { get; set; } = string.Empty;
        
        protected BaseConfiguration()
        {
            ConfigType = GetType().Name;
        }
        
        public virtual bool IsValid()
        {
            return !string.IsNullOrEmpty(Version) && !string.IsNullOrEmpty(ConfigType);
        }
        
        public void Touch()
        {
            LastModified = DateTime.UtcNow;
        }
    }
} 
