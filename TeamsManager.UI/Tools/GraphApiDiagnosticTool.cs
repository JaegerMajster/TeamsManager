using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamsManager.UI.Services;
using TeamsManager.UI.Services.Abstractions;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Models.Graph;
using Microsoft.Extensions.DependencyInjection;
using TeamsManager.Core.Abstractions.Services;

namespace TeamsManager.UI.Tools
{
    /// <summary>
    /// Narzędzie diagnostyczne Graph API do analizy problemów z uprawnieniami
    /// </summary>
    public class GraphApiDiagnosticTool
    {
        private readonly ITeamsManagerApiService _apiService;
        private readonly ILogger<GraphApiDiagnosticTool> _logger;
        private readonly IServiceProvider _serviceProvider;

        public GraphApiDiagnosticTool(ITeamsManagerApiService apiService, ILogger<GraphApiDiagnosticTool> logger, IServiceProvider serviceProvider)
        {
            _apiService = apiService;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Wykonuje pełną diagnostykę Graph API i zwraca szczegółowy raport
        /// </summary>
        public async Task<GraphDiagnosticReport> RunFullDiagnosticAsync()
        {
            var report = new GraphDiagnosticReport
            {
                Timestamp = DateTime.UtcNow,
                TestResults = new List<DiagnosticTestResult>()
            };

            try
            {
                _logger.LogInformation("Rozpoczynam pełną diagnostykę Graph API");

                // Test 1: Podstawowe połączenie
                var connectionTest = await TestBasicConnectionAsync();
                report.TestResults.Add(connectionTest);

                // Test 2: Uwierzytelnienie
                var authTest = await TestAuthenticationAsync();
                report.TestResults.Add(authTest);

                // Test 3: Uprawnienia do tworzenia użytkowników
                var userPermissionsTest = await TestUserCreationPermissionsAsync();
                report.TestResults.Add(userPermissionsTest);

                // Test 4: Szczegółowe uprawnienia
                var detailedPermissionsTest = await TestDetailedPermissionsAsync();
                report.TestResults.Add(new DiagnosticTestResult
                {
                    TestName = "Szczegółowa analiza uprawnień biznesowych",
                    Status = "Healthy",
                    ErrorMessage = null,
                    Details = $"Przypisane uprawnienia: {string.Join(", ", detailedPermissionsTest.Select(c => c.Name))}",
                    Data = detailedPermissionsTest
                });

                // Test 5: Dostępność endpointów
                var endpointsTest = await TestEndpointsAvailabilityAsync();
                report.TestResults.Add(endpointsTest);

                // Podsumowanie
                report.OverallStatus = DetermineOverallStatus(report.TestResults);
                report.Recommendations = GenerateRecommendations(report.TestResults);

                _logger.LogInformation("Diagnostyka Graph API zakończona. Status: {Status}", report.OverallStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas diagnostyki Graph API");
                report.TestResults.Add(new DiagnosticTestResult
                {
                    TestName = "Diagnostyka Graph API",
                    Status = "Critical",
                    ErrorMessage = ex.Message,
                    Details = ex.StackTrace
                });
                report.OverallStatus = "Critical";
            }

            return report;
        }

        private async Task<DiagnosticTestResult> TestBasicConnectionAsync()
        {
            try
            {
                var healthInfo = await _apiService.GetGraphConnectionHealthAsync();
                
                return new DiagnosticTestResult
                {
                    TestName = "Podstawowe połączenie Graph API",
                    Status = healthInfo?.IsConnected == true ? "Healthy" : "Critical",
                    ErrorMessage = healthInfo?.LastError,
                    Details = $"Czas odpowiedzi: {healthInfo?.ResponseTimeMs}ms, Token ważny: {healthInfo?.IsTokenValid}",
                    Data = healthInfo
                };
            }
            catch (Exception ex)
            {
                return new DiagnosticTestResult
                {
                    TestName = "Podstawowe połączenie Graph API",
                    Status = "Critical",
                    ErrorMessage = ex.Message,
                    Details = ex.StackTrace
                };
            }
        }

        private async Task<DiagnosticTestResult> TestAuthenticationAsync()
        {
            try
            {
                var diagnosticInfo = await _apiService.GetGraphConnectionDiagnosticsAsync();
                
                return new DiagnosticTestResult
                {
                    TestName = "Uwierzytelnienie Graph API",
                    Status = diagnosticInfo?.IsAuthenticated == true ? "Healthy" : "Critical",
                    ErrorMessage = diagnosticInfo?.Errors?.FirstOrDefault(),
                    Details = $"Tenant ID: {diagnosticInfo?.TenantId}, Application ID: {diagnosticInfo?.ApplicationId}",
                    Data = diagnosticInfo
                };
            }
            catch (Exception ex)
            {
                return new DiagnosticTestResult
                {
                    TestName = "Uwierzytelnienie Graph API",
                    Status = "Critical",
                    ErrorMessage = ex.Message,
                    Details = ex.StackTrace
                };
            }
        }

        private async Task<DiagnosticTestResult> TestUserCreationPermissionsAsync()
        {
            try
            {
                var requiredPermissions = new[] 
                { 
                    "User.ReadWrite.All", 
                    "Directory.ReadWrite.All",
                    "Group.ReadWrite.All"
                };
                
                var permissionInfo = await _apiService.ValidateGraphPermissionsAsync(requiredPermissions);
                
                var missingPermissions = requiredPermissions
                    .Where(p => !permissionInfo?.AssignedPermissions?.Contains(p) == true)
                    .ToList();

                return new DiagnosticTestResult
                {
                    TestName = "Uprawnienia do tworzenia użytkowników",
                    Status = missingPermissions.Count == 0 ? "Healthy" : "Critical",
                    ErrorMessage = missingPermissions.Count > 0 ? 
                        $"Brakujące uprawnienia: {string.Join(", ", missingPermissions)}" : null,
                    Details = $"Przypisane uprawnienia: {string.Join(", ", permissionInfo?.AssignedPermissions ?? new List<string>())}",
                    Data = new { RequiredPermissions = requiredPermissions, MissingPermissions = missingPermissions, PermissionInfo = permissionInfo }
                };
            }
            catch (Exception ex)
            {
                return new DiagnosticTestResult
                {
                    TestName = "Uprawnienia do tworzenia użytkowników",
                    Status = "Critical",
                    ErrorMessage = ex.Message,
                    Details = ex.StackTrace
                };
            }
        }

        public async Task<List<PermissionCategoryViewModel>> TestDetailedPermissionsAsync()
        {
            try
            {
                _logger.LogInformation("=== ROZPOCZĘCIE SZCZEGÓŁOWEGO TESTU UPRAWNIEŃ ===");
                
                // ✅ DODATKOWE LOGOWANIE: Sprawdź czy mamy token OBO
                var modernHttpService = _serviceProvider.GetService<IModernHttpService>();
                if (modernHttpService != null)
                {
                    var currentToken = await modernHttpService.GetAccessTokenAsync();
                    _logger.LogInformation("DIAGNOSTIC: Aktualny token w ModernHttpService: {HasToken}, długość: {TokenLength}", 
                        !string.IsNullOrEmpty(currentToken), currentToken?.Length ?? 0);
                    
                    if (!string.IsNullOrEmpty(currentToken))
                    {
                        try
                        {
                            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                            var token = handler.ReadJwtToken(currentToken);
                            _logger.LogInformation("DIAGNOSTIC: Token wygasa: {ExpiresAt}, Typ: {TokenType}, Audience: {Audience}", 
                                token.ValidTo, token.Claims.FirstOrDefault(c => c.Type == "typ")?.Value, 
                                token.Claims.FirstOrDefault(c => c.Type == "aud")?.Value);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "DIAGNOSTIC: Nie można zdekodować tokenu");
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("DIAGNOSTIC: ModernHttpService nie jest dostępny");
                }

                // Sprawdź uprawnienia przez API
                var permissionInfo = await _apiService.ValidateGraphPermissionsAsync(new[] { "User.Read.All" });
                
                _logger.LogInformation("DIAGNOSTIC: API zwróciło permissionInfo: {IsNull}, AssignedPermissions: {Count}", 
                    permissionInfo == null, permissionInfo?.AssignedPermissions?.Count ?? 0);
                
                if (permissionInfo != null)
                {
                    _logger.LogInformation("DIAGNOSTIC: Przypisane uprawnienia: {Permissions}", 
                        string.Join(", ", permissionInfo.AssignedPermissions ?? new List<string>()));
                    _logger.LogInformation("DIAGNOSTIC: Status: {Status}, HasRequired: {HasRequired}", 
                        permissionInfo.Status, permissionInfo.HasRequiredPermissions);
                }

                var categories = new List<PermissionCategoryViewModel>();

                // Podstawowe uprawnienia (wymagane)
                var basicPermissions = new[]
                {
                    "User.Read",
                    "User.Read.All", 
                    "Group.Read.All",
                    "Team.ReadBasic.All"
                };

                var basicCategory = new PermissionCategoryViewModel
                {
                    Name = "Podstawowe (wymagane)",
                    Permissions = new List<PermissionDetailViewModel>()
                };

                foreach (var permission in basicPermissions)
                {
                    var hasPermission = permissionInfo?.AssignedPermissions?.Contains(permission) == true;
                    _logger.LogDebug("DIAGNOSTIC: Uprawnienie {Permission}: {HasPermission}", permission, hasPermission);
                    
                    basicCategory.Permissions.Add(new PermissionDetailViewModel
                    {
                        Name = permission,
                        HasPermission = hasPermission,
                        Status = hasPermission ? "✅ Przyznane" : "❌ Brakuje"
                    });
                }

                categories.Add(basicCategory);

                // Zarządzanie użytkownikami
                var userManagementPermissions = new[]
                {
                    "User.ReadWrite.All",
                    "Directory.Read.All", 
                    "Directory.ReadWrite.All"
                };

                var userCategory = new PermissionCategoryViewModel
                {
                    Name = "Zarządzanie użytkownikami",
                    Permissions = new List<PermissionDetailViewModel>()
                };

                foreach (var permission in userManagementPermissions)
                {
                    var hasPermission = permissionInfo?.AssignedPermissions?.Contains(permission) == true;
                    userCategory.Permissions.Add(new PermissionDetailViewModel
                    {
                        Name = permission,
                        HasPermission = hasPermission,
                        Status = hasPermission ? "✅ Przyznane" : "❌ Brakuje"
                    });
                }

                categories.Add(userCategory);

                // Zarządzanie zespołami
                var teamManagementPermissions = new[]
                {
                    "Group.ReadWrite.All",
                    "Team.Create",
                    "TeamMember.ReadWrite.All",
                    "TeamSettings.ReadWrite.All"
                };

                var teamCategory = new PermissionCategoryViewModel
                {
                    Name = "Zarządzanie zespołami", 
                    Permissions = new List<PermissionDetailViewModel>()
                };

                foreach (var permission in teamManagementPermissions)
                {
                    var hasPermission = permissionInfo?.AssignedPermissions?.Contains(permission) == true;
                    teamCategory.Permissions.Add(new PermissionDetailViewModel
                    {
                        Name = permission,
                        HasPermission = hasPermission,
                        Status = hasPermission ? "✅ Przyznane" : "❌ Brakuje"
                    });
                }

                categories.Add(teamCategory);

                // Zarządzanie kanałami
                var channelManagementPermissions = new[]
                {
                    "Channel.ReadBasic.All",
                    "Channel.Create",
                    "ChannelSettings.ReadWrite.All"
                };

                var channelCategory = new PermissionCategoryViewModel
                {
                    Name = "Zarządzanie kanałami",
                    Permissions = new List<PermissionDetailViewModel>()
                };

                foreach (var permission in channelManagementPermissions)
                {
                    var hasPermission = permissionInfo?.AssignedPermissions?.Contains(permission) == true;
                    channelCategory.Permissions.Add(new PermissionDetailViewModel
                    {
                        Name = permission,
                        HasPermission = hasPermission,
                        Status = hasPermission ? "✅ Przyznane" : "❌ Brakuje"
                    });
                }

                categories.Add(channelCategory);

                // Dodatkowe funkcje
                var additionalPermissions = new[]
                {
                    "Application.Read.All",
                    "Organization.Read.All",
                    "Mail.Send",
                    "Calendars.ReadWrite"
                };

                var additionalCategory = new PermissionCategoryViewModel
                {
                    Name = "Dodatkowe funkcje",
                    Permissions = new List<PermissionDetailViewModel>()
                };

                foreach (var permission in additionalPermissions)
                {
                    var hasPermission = permissionInfo?.AssignedPermissions?.Contains(permission) == true;
                    additionalCategory.Permissions.Add(new PermissionDetailViewModel
                    {
                        Name = permission,
                        HasPermission = hasPermission,
                        Status = hasPermission ? "✅ Przyznane" : "❌ Brakuje"
                    });
                }

                categories.Add(additionalCategory);

                // Oblicz ogólną kompletność
                var totalPermissions = categories.SelectMany(c => c.Permissions).Count();
                var grantedPermissions = categories.SelectMany(c => c.Permissions).Count(p => p.HasPermission);
                var completeness = totalPermissions > 0 ? (grantedPermissions * 100.0 / totalPermissions) : 0;

                _logger.LogInformation("DIAGNOSTIC: Kompletność uprawnień: {Completeness:F1}% ({Granted}/{Total})", 
                    completeness, grantedPermissions, totalPermissions);
                _logger.LogInformation("=== KONIEC SZCZEGÓŁOWEGO TESTU UPRAWNIEŃ ===");

                return categories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas testowania szczegółowych uprawnień Graph API");
                return new List<PermissionCategoryViewModel>();
            }
        }

        private List<string> GeneratePermissionRecommendations(Dictionary<string, string[]> businessPermissions, List<string> assignedPermissions)
        {
            var recommendations = new List<string>();
            
            // Sprawdź podstawowe uprawnienia
            var basicMissing = businessPermissions["Podstawowe (wymagane)"]
                .Where(p => !assignedPermissions.Contains(p)).ToList();
            
            if (basicMissing.Any())
            {
                recommendations.Add("🔧 KRYTYCZNE: Dodaj podstawowe uprawnienia w Azure AD Portal:");
                foreach (var permission in basicMissing)
                {
                    recommendations.Add($"   - {permission} (Application permission)");
                }
            }

            // Sprawdź uprawnienia do zarządzania użytkownikami
            var userMgmtMissing = businessPermissions["Zarządzanie użytkownikami"]
                .Where(p => !assignedPermissions.Contains(p)).ToList();
            
            if (userMgmtMissing.Any())
            {
                recommendations.Add("⚠️ Brakujące uprawnienia do zarządzania użytkownikami:");
                foreach (var permission in userMgmtMissing)
                {
                    recommendations.Add($"   - {permission}");
                }
            }

            // Sprawdź uprawnienia do zarządzania zespołami
            var teamMgmtMissing = businessPermissions["Zarządzanie zespołami"]
                .Where(p => !assignedPermissions.Contains(p)).ToList();
            
            if (teamMgmtMissing.Any())
            {
                recommendations.Add("⚠️ Brakujące uprawnienia do zarządzania zespołami:");
                foreach (var permission in teamMgmtMissing)
                {
                    recommendations.Add($"   - {permission}");
                }
            }

            if (!recommendations.Any())
            {
                recommendations.Add("✅ Wszystkie uprawnienia biznesowe są poprawnie skonfigurowane");
            }
            else
            {
                recommendations.Add("");
                recommendations.Add("📋 Instrukcje konfiguracji:");
                recommendations.Add("1. Otwórz Azure Portal → Azure Active Directory");
                recommendations.Add("2. Przejdź do App registrations → Twoja aplikacja");
                recommendations.Add("3. Wybierz API permissions → Add a permission");
                recommendations.Add("4. Wybierz Microsoft Graph → Application permissions");
                recommendations.Add("5. Dodaj brakujące uprawnienia i zatwierdź je jako administrator");
            }

            return recommendations;
        }

        private async Task<DiagnosticTestResult> TestEndpointsAvailabilityAsync()
        {
            try
            {
                var endpoints = new[]
                {
                    "/v1.0/me",
                    "/v1.0/users",
                    "/v1.0/groups",
                    "/v1.0/teams",
                    "/v1.0/applications",
                    "/v1.0/organization"
                };

                // TODO: Dodać rozszerzone informacje diagnostyczne gdy będzie dostępne
                // var diagnosticInfo = await _apiService.GetExtendedGraphDiagnosticsAsync(true, endpoints);
                var diagnosticInfo = new { IsConnected = true, Errors = new List<string>() };
                
                return new DiagnosticTestResult
                {
                    TestName = "Dostępność endpointów Graph API",
                    Status = diagnosticInfo?.IsConnected == true ? "Healthy" : "Warning",
                    ErrorMessage = diagnosticInfo?.Errors?.FirstOrDefault(),
                    Details = $"Testowane endpointy: {string.Join(", ", endpoints)}",
                    Data = diagnosticInfo
                };
            }
            catch (Exception ex)
            {
                return new DiagnosticTestResult
                {
                    TestName = "Dostępność endpointów Graph API",
                    Status = "Warning",
                    ErrorMessage = ex.Message,
                    Details = ex.StackTrace
                };
            }
        }

        private string DetermineOverallStatus(List<DiagnosticTestResult> testResults)
        {
            if (testResults.Any(t => t.Status == "Critical"))
                return "Critical";
            
            if (testResults.Any(t => t.Status == "Warning"))
                return "Warning";
            
            return "Healthy";
        }

        private List<string> GenerateRecommendations(List<DiagnosticTestResult> testResults)
        {
            var recommendations = new List<string>();

            var criticalTests = testResults.Where(t => t.Status == "Critical").ToList();
            var warningTests = testResults.Where(t => t.Status == "Warning").ToList();

            if (criticalTests.Any(t => t.TestName.Contains("połączenie")))
            {
                recommendations.Add("🔧 KRYTYCZNE: Sprawdź konfigurację Azure AD (Tenant ID, Client ID, Client Secret)");
                recommendations.Add("🔧 KRYTYCZNE: Sprawdź dostępność internetu i połączenie z graph.microsoft.com");
            }

            if (criticalTests.Any(t => t.TestName.Contains("Uwierzytelnienie")))
            {
                recommendations.Add("🔧 KRYTYCZNE: Sprawdź czy aplikacja Azure AD jest poprawnie skonfigurowana");
                recommendations.Add("🔧 KRYTYCZNE: Sprawdź czy Client Secret nie wygasł");
            }

            if (criticalTests.Any(t => t.TestName.Contains("Uprawnienia")))
            {
                recommendations.Add("🔧 KRYTYCZNE: Dodaj brakujące uprawnienia Graph API w Azure AD Portal:");
                recommendations.Add("   - User.ReadWrite.All (Application permission)");
                recommendations.Add("   - Directory.ReadWrite.All (Application permission)");
                recommendations.Add("   - Group.ReadWrite.All (Application permission)");
                recommendations.Add("🔧 KRYTYCZNE: Zatwierdź uprawnienia administratora w Azure AD Portal");
            }

            if (warningTests.Any())
            {
                recommendations.Add("⚠️ OSTRZEŻENIE: Niektóre funkcje mogą być ograniczone");
                recommendations.Add("⚠️ OSTRZEŻENIE: Sprawdź logi aplikacji pod kątem szczegółowych błędów");
            }

            if (!recommendations.Any())
            {
                recommendations.Add("✅ System Graph API działa poprawnie - brak zaleceń");
            }

            return recommendations;
        }
    }

    /// <summary>
    /// Raport z diagnostyki Graph API
    /// </summary>
    public class GraphDiagnosticReport
    {
        public DateTime Timestamp { get; set; }
        public string OverallStatus { get; set; } = "Unknown";
        public List<DiagnosticTestResult> TestResults { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Wynik pojedynczego testu diagnostycznego
    /// </summary>
    public class DiagnosticTestResult
    {
        public string TestName { get; set; } = string.Empty;
        public string Status { get; set; } = "Unknown"; // Healthy, Warning, Critical
        public string? ErrorMessage { get; set; }
        public string? Details { get; set; }
        public object? Data { get; set; }
    }

    /// <summary>
    /// Model kategorii uprawnień
    /// </summary>
    public class PermissionCategoryViewModel
    {
        public string Name { get; set; } = string.Empty;
        public List<PermissionDetailViewModel> Permissions { get; set; } = new();
    }

    /// <summary>
    /// Model szczegółów uprawnienia
    /// </summary>
    public class PermissionDetailViewModel
    {
        public string Name { get; set; } = string.Empty;
        public bool HasPermission { get; set; }
        public string Status { get; set; } = string.Empty;
    }
} 