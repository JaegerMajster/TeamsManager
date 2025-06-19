using System.Collections.Generic;

namespace TeamsManager.UI.Models.Configuration
{
    public class AzureAdConfiguration : BaseConfiguration
    {
        public string TenantId { get; set; } = string.Empty;
        public UiClientSettings Ui { get; set; } = new();
        public ApiClientSettings Api { get; set; } = new();
        public GraphSettings Graph { get; set; } = new();
        
        public override bool IsValid()
        {
            return base.IsValid() && 
                   !string.IsNullOrEmpty(TenantId) &&
                   !string.IsNullOrEmpty(Ui.ClientId) &&
                   !string.IsNullOrEmpty(Api.ClientId) &&
                   !string.IsNullOrEmpty(Api.ClientSecret);
        }
    }

    public class UiClientSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = "http://localhost";
        public List<string> Scopes { get; set; } = new();
    }

    public class ApiClientSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
    }

    public class GraphSettings
    {
        public string BaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";
        public List<string> Scopes { get; set; } = new()
        {
            "https://graph.microsoft.com/User.Read",
            "https://graph.microsoft.com/Team.ReadBasic.All"
        };
    }
} 