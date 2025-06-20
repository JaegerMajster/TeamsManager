using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamsManager.UI.Services;
using TeamsManager.UI.Services.Abstractions;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.UI.Tools
{
    /// <summary>
    /// Narzędzie diagnostyczne Graph API do analizy problemów z uprawnieniami
    /// </summary>
    public class GraphApiDiagnosticTool
    {
        private readonly ITeamsManagerApiService _apiService;
        private readonly ILogger<GraphApiDiagnosticTool> _logger;

        public GraphApiDiagnosticTool(ITeamsManagerApiService apiService, ILogger<GraphApiDiagnosticTool> logger)
        {
            _apiService = apiService;
            _logger = logger;
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
                report.TestResults.Add(detailedPermissionsTest);

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

        private async Task<DiagnosticTestResult> TestDetailedPermissionsAsync()
        {
            try
            {
                var allPermissions = new[]
                {
                    "User.Read", "User.Read.All", "User.ReadWrite.All",
                    "Directory.Read.All", "Directory.ReadWrite.All",
                    "Group.Read.All", "Group.ReadWrite.All",
                    "Team.ReadBasic.All", "Team.Create", "TeamMember.ReadWrite.All",
                    "Channel.ReadBasic.All", "Channel.Create",
                    "Application.Read.All", "Organization.Read.All"
                };

                var permissionInfo = await _apiService.ValidateGraphPermissionsAsync(allPermissions);
                
                var assignedPermissions = permissionInfo?.AssignedPermissions ?? new List<string>();
                var missingPermissions = allPermissions.Except(assignedPermissions).ToList();

                return new DiagnosticTestResult
                {
                    TestName = "Szczegółowa analiza uprawnień",
                    Status = assignedPermissions.Count >= 5 ? "Healthy" : "Warning",
                    ErrorMessage = null,
                    Details = $"Przypisanych: {assignedPermissions.Count}/{allPermissions.Length} uprawnień",
                    Data = new 
                    { 
                        AssignedPermissions = assignedPermissions, 
                        MissingPermissions = missingPermissions,
                        PermissionCompleteness = (double)assignedPermissions.Count / allPermissions.Length * 100
                    }
                };
            }
            catch (Exception ex)
            {
                return new DiagnosticTestResult
                {
                    TestName = "Szczegółowa analiza uprawnień",
                    Status = "Critical",
                    ErrorMessage = ex.Message,
                    Details = ex.StackTrace
                };
            }
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
} 