using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using TeamsManager.UI.Models;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Analyzer do parsowania informacji o Conditional Access z tokenów MSAL
    /// </summary>
    public class ConditionalAccessAnalyzer
    {
        private readonly ILogger<ConditionalAccessAnalyzer> _logger;

        public ConditionalAccessAnalyzer(ILogger<ConditionalAccessAnalyzer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Analizuje token i wydobywa informacje o Conditional Access
        /// </summary>
        public ConditionalAccessInfo AnalyzeToken(AuthenticationResult authResult)
        {
            if (authResult?.AccessToken == null)
            {
                _logger.LogWarning("Cannot analyze token - AuthenticationResult or AccessToken is null");
                return new ConditionalAccessInfo();
            }

            try
            {
                // Parsuj JWT token
                var jwtHandler = new JwtSecurityTokenHandler();
                var jwtToken = jwtHandler.ReadJwtToken(authResult.AccessToken);
                
                _logger.LogDebug("Analyzing JWT token with {ClaimsCount} claims", jwtToken.Claims.Count());

                // Wydobądź informacje z claims
                var info = new ConditionalAccessInfo
                {
                    TenantId = GetClaimValue(jwtToken, "tid"),
                    ObjectId = GetClaimValue(jwtToken, "oid"),
                    GrantedScopes = authResult.Scopes?.ToList() ?? new List<string>(),
                    TokenExpiresOn = authResult.ExpiresOn.DateTime,
                    AdditionalClaims = ExtractAllClaims(jwtToken),
                    
                    // MFA Claims
                    RequiresMfa = HasClaim(jwtToken, "amr") || HasClaim(jwtToken, "mfa_required"),
                    MfaCompleted = GetAuthenticationMethods(jwtToken).Any(m => 
                        m.Contains("mfa", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("otp", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("sms", StringComparison.OrdinalIgnoreCase) ||
                        m.Contains("phone", StringComparison.OrdinalIgnoreCase)),
                    
                    AuthenticationMethods = GetAuthenticationMethods(jwtToken),
                    LastMfaTime = ParseMfaTime(jwtToken),
                    
                    // Device Claims
                    IsManagedDevice = HasClaimValue(jwtToken, "deviceid") || HasClaimValue(jwtToken, "managed_device"),
                    IsCompliantDevice = HasClaimValue(jwtToken, "compliant_device") || 
                                       GetClaimValue(jwtToken, "device_compliance")?.ToLower() == "compliant",
                    
                    // Location and Risk
                    IsTrustedLocation = HasClaimValue(jwtToken, "trusted_location") ||
                                       GetClaimValue(jwtToken, "location_claim")?.ToLower() == "trusted",
                    RiskLevel = GetClaimValue(jwtToken, "risk_level") ?? GetClaimValue(jwtToken, "signin_risk"),
                    
                    // Additional Apps
                    RequiredApplications = GetRequiredApplications(jwtToken)
                };

                _logger.LogInformation("Conditional Access analysis completed: {Info}", info.ToString());
                return info;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing JWT token for Conditional Access info");
                return new ConditionalAccessInfo();
            }
        }

        /// <summary>
        /// Wydobądź wartość claim
        /// </summary>
        private string? GetClaimValue(JwtSecurityToken token, string claimType)
        {
            return token.Claims.FirstOrDefault(c => 
                c.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        /// <summary>
        /// Sprawdź czy claim istnieje
        /// </summary>
        private bool HasClaim(JwtSecurityToken token, string claimType)
        {
            return token.Claims.Any(c => 
                c.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Sprawdź czy claim ma niepustą wartość
        /// </summary>
        private bool HasClaimValue(JwtSecurityToken token, string claimType)
        {
            var value = GetClaimValue(token, claimType);
            return !string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// Wydobądź metody uwierzytelniania z AMR claim
        /// </summary>
        private List<string> GetAuthenticationMethods(JwtSecurityToken token)
        {
            var methods = new List<string>();
            
            // AMR (Authentication Method Reference) claim
            var amrClaims = token.Claims.Where(c => 
                c.Type.Equals("amr", StringComparison.OrdinalIgnoreCase));
            
            foreach (var claim in amrClaims)
            {
                if (!string.IsNullOrWhiteSpace(claim.Value))
                {
                    methods.Add(claim.Value);
                }
            }

            // Jeśli nie ma AMR, sprawdź inne możliwe claims
            if (!methods.Any())
            {
                var authMethod = GetClaimValue(token, "auth_method");
                if (!string.IsNullOrWhiteSpace(authMethod))
                {
                    methods.Add(authMethod);
                }
            }

            return methods;
        }

        /// <summary>
        /// Parsuj czas ostatniego MFA
        /// </summary>
        private DateTime? ParseMfaTime(JwtSecurityToken token)
        {
            var mfaTimeClaim = GetClaimValue(token, "mfa_time") ?? GetClaimValue(token, "last_mfa");
            
            if (string.IsNullOrWhiteSpace(mfaTimeClaim))
                return null;

            // Próbuj różne formaty
            if (long.TryParse(mfaTimeClaim, out var unixTimestamp))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
            }

            if (DateTime.TryParse(mfaTimeClaim, out var dateTime))
            {
                return dateTime;
            }

            return null;
        }

        /// <summary>
        /// Wydobądź aplikacje wymagające dodatkowych uprawnień
        /// </summary>
        private List<string> GetRequiredApplications(JwtSecurityToken token)
        {
            var apps = new List<string>();
            
            var appClaim = GetClaimValue(token, "required_apps") ?? GetClaimValue(token, "app_displayname");
            if (!string.IsNullOrWhiteSpace(appClaim))
            {
                // Może być lista oddzielona przecinkami
                apps.AddRange(appClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(s => s.Trim()));
            }

            return apps;
        }

        /// <summary>
        /// Wydobądź wszystkie claims jako słownik
        /// </summary>
        private Dictionary<string, object> ExtractAllClaims(JwtSecurityToken token)
        {
            var claims = new Dictionary<string, object>();
            
            foreach (var claim in token.Claims)
            {
                if (claims.ContainsKey(claim.Type))
                {
                    // Jeśli claim już istnieje, zrób z niego listę
                    if (claims[claim.Type] is List<string> list)
                    {
                        list.Add(claim.Value);
                    }
                    else
                    {
                        claims[claim.Type] = new List<string> { claims[claim.Type].ToString()!, claim.Value };
                    }
                }
                else
                {
                    claims[claim.Type] = claim.Value;
                }
            }

            return claims;
        }

        /// <summary>
        /// Sprawdź czy token wymaga odświeżenia wkrótce
        /// </summary>
        public bool ShouldRefreshToken(ConditionalAccessInfo info, TimeSpan threshold)
        {
            if (info.TokenExpiresOn == null)
                return false;

            return info.TokenExpiresOn.Value.Subtract(DateTime.UtcNow) < threshold;
        }

        /// <summary>
        /// Uzyskaj podsumowanie bezpieczeństwa sesji
        /// </summary>
        public string GetSecuritySummary(ConditionalAccessInfo info)
        {
            var securityLevel = "Unknown";
            var factors = new List<string>();

            if (info.MfaCompleted)
            {
                factors.Add("MFA");
                securityLevel = "High";
            }
            else if (info.RequiresMfa)
            {
                securityLevel = "Medium (MFA Pending)";
            }
            else
            {
                securityLevel = "Basic";
            }

            if (info.IsManagedDevice)
                factors.Add("Managed Device");
                
            if (info.IsCompliantDevice)
                factors.Add("Compliant Device");
                
            if (info.IsTrustedLocation)
                factors.Add("Trusted Location");

            var summary = $"Security Level: {securityLevel}";
            if (factors.Any())
                summary += $" ({string.Join(", ", factors)})";

            return summary;
        }
    }
} 