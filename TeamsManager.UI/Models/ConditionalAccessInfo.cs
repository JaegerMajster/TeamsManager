using System;
using System.Collections.Generic;

namespace TeamsManager.UI.Models
{
    /// <summary>
    /// Informacje o Conditional Access i Claims z tokenu
    /// </summary>
    public class ConditionalAccessInfo
    {
        /// <summary>
        /// Czy sesja wymaga MFA
        /// </summary>
        public bool RequiresMfa { get; init; }
        
        /// <summary>
        /// Czy użytkownik przeszedł MFA w tej sesji
        /// </summary>
        public bool MfaCompleted { get; init; }
        
        /// <summary>
        /// Metody uwierzytelniania użyte w tej sesji
        /// </summary>
        public List<string> AuthenticationMethods { get; init; } = new();
        
        /// <summary>
        /// Czas ostatniego MFA
        /// </summary>
        public DateTime? LastMfaTime { get; init; }
        
        /// <summary>
        /// Czy urządzenie jest zarządzane przez organizację
        /// </summary>
        public bool IsManagedDevice { get; init; }
        
        /// <summary>
        /// Czy urządzenie jest zgodne z politykami
        /// </summary>
        public bool IsCompliantDevice { get; init; }
        
        /// <summary>
        /// Czy logowanie jest z zaufanej lokalizacji
        /// </summary>
        public bool IsTrustedLocation { get; init; }
        
        /// <summary>
        /// Risk level sesji/użytkownika (jeśli dostępny)
        /// </summary>
        public string? RiskLevel { get; init; }
        
        /// <summary>
        /// Aplikacje które wymagają dodatkowych uprawnień
        /// </summary>
        public List<string> RequiredApplications { get; init; } = new();
        
        /// <summary>
        /// Dodatkowe claims z tokenu
        /// </summary>
        public Dictionary<string, object> AdditionalClaims { get; init; } = new();
        
        /// <summary>
        /// Tenant ID
        /// </summary>
        public string? TenantId { get; init; }
        
        /// <summary>
        /// Object ID użytkownika
        /// </summary>
        public string? ObjectId { get; init; }
        
        /// <summary>
        /// Uprawnienia/scope przyznane w tym tokenie
        /// </summary>
        public List<string> GrantedScopes { get; init; } = new();
        
        /// <summary>
        /// Czas wygaśnięcia tokenu
        /// </summary>
        public DateTime? TokenExpiresOn { get; init; }
        
        /// <summary>
        /// Format jako string dla wyświetlania
        /// </summary>
        public override string ToString()
        {
            var details = new List<string>();
            
            if (RequiresMfa)
                details.Add($"🔐 MFA Required: {(MfaCompleted ? "✅ Completed" : "❌ Pending")}");
                
            if (AuthenticationMethods.Count > 0)
                details.Add($"🔑 Auth Methods: {string.Join(", ", AuthenticationMethods)}");
                
            if (IsManagedDevice)
                details.Add("💼 Managed Device");
                
            if (IsCompliantDevice)
                details.Add("✅ Compliant Device");
                
            if (IsTrustedLocation)
                details.Add("🌍 Trusted Location");
                
            if (!string.IsNullOrEmpty(RiskLevel))
                details.Add($"⚠️ Risk Level: {RiskLevel}");
                
            return string.Join(" | ", details);
        }
    }
} 