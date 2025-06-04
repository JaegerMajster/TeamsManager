using Microsoft.AspNetCore.Mvc;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Enums;

namespace TeamsManager.Api.Controllers
{
    /// <summary>
    /// Kontroler diagnostyczny do weryfikacji konfiguracji systemu
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)] // Ukryj w Swagger
    public class DiagnosticsController : ControllerBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DiagnosticsController> _logger;

        public DiagnosticsController(
            IServiceProvider serviceProvider,
            ILogger<DiagnosticsController> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Weryfikuje poprawność konfiguracji Dependency Injection
        /// </summary>
        /// <returns>Status wszystkich krytycznych serwisów</returns>
        [HttpGet("verify-di")]
        public IActionResult VerifyDependencyInjection()
        {
            var results = new Dictionary<string, bool>();
            
            // Lista wszystkich krytycznych serwisów do sprawdzenia
            var servicesToCheck = new Dictionary<string, Type>
            {
                // Serwisy infrastrukturalne
                ["IOperationHistoryService"] = typeof(IOperationHistoryService),
                ["INotificationService"] = typeof(INotificationService),
                ["ICurrentUserService"] = typeof(ICurrentUserService),
                
                // PowerShell Services
                ["IPowerShellConnectionService"] = typeof(IPowerShellConnectionService),
                ["IPowerShellCacheService"] = typeof(IPowerShellCacheService),
                ["IPowerShellTeamManagementService"] = typeof(IPowerShellTeamManagementService),
                ["IPowerShellUserManagementService"] = typeof(IPowerShellUserManagementService),
                ["IPowerShellBulkOperationsService"] = typeof(IPowerShellBulkOperationsService),
                ["IPowerShellService"] = typeof(IPowerShellService),
                
                // Serwisy aplikacyjne
                ["ITeamService"] = typeof(ITeamService),
                ["IUserService"] = typeof(IUserService),
                ["IDepartmentService"] = typeof(IDepartmentService),
                ["IChannelService"] = typeof(IChannelService),
                ["ISubjectService"] = typeof(ISubjectService),
                ["IApplicationSettingService"] = typeof(IApplicationSettingService),
                ["ISchoolTypeService"] = typeof(ISchoolTypeService),
                ["ISchoolYearService"] = typeof(ISchoolYearService),
                ["ITeamTemplateService"] = typeof(ITeamTemplateService),
                
                // Repozytoria
                ["IOperationHistoryRepository"] = typeof(IOperationHistoryRepository),
                ["IUserRepository"] = typeof(IUserRepository),
                ["ITeamRepository"] = typeof(ITeamRepository)
            };

            foreach (var kvp in servicesToCheck)
            {
                try
                {
                    var service = _serviceProvider.GetService(kvp.Value);
                    results[kvp.Key] = service != null;
                    
                    if (service == null)
                    {
                        _logger.LogWarning($"Service {kvp.Key} is not registered");
                    }
                    else
                    {
                        _logger.LogDebug($"Service {kvp.Key} successfully resolved");
                    }
                }
                catch (Exception ex)
                {
                    results[kvp.Key] = false;
                    _logger.LogError(ex, $"Error resolving {kvp.Key}");
                }
            }

            var allServicesRegistered = results.All(r => r.Value);
            var successCount = results.Count(r => r.Value);
            var totalCount = results.Count;
            
            _logger.LogInformation($"DI Verification: {successCount}/{totalCount} services registered successfully");
            
            return Ok(new
            {
                AllServicesRegistered = allServicesRegistered,
                SuccessfulServices = successCount,
                TotalServices = totalCount,
                SuccessRate = Math.Round((double)successCount / totalCount * 100, 2),
                Results = results,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Testuje kompletny przepływ operacji z audytem i powiadomieniami
        /// </summary>
        /// <returns>Wynik testu przepływu</returns>
        [HttpGet("test-flow")]
        public async Task<IActionResult> TestCompleteFlow()
        {
            try
            {
                _logger.LogInformation("Starting complete flow test");

                // Test tworzenia wpisu historii operacji
                var historyService = _serviceProvider.GetRequiredService<IOperationHistoryService>();
                var operation = await historyService.CreateNewOperationEntryAsync(
                    OperationType.TeamCreated,
                    "Team",
                    "test-team-id",
                    "Test Team"
                );

                _logger.LogInformation($"Created operation entry with ID: {operation.Id}");

                // Test powiadomień
                var notificationService = _serviceProvider.GetRequiredService<INotificationService>();
                await notificationService.SendNotificationToUserAsync(
                    "test@user.com",
                    "Test operation completed",
                    "success"
                );

                _logger.LogInformation("Sent test notification");

                // Aktualizacja statusu
                await historyService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    "Test completed successfully"
                );

                _logger.LogInformation($"Updated operation {operation.Id} status to Completed");

                return Ok(new
                {
                    Success = true,
                    OperationId = operation.Id,
                    Message = "Complete flow test successful",
                    Steps = new[]
                    {
                        "✅ Created operation history entry",
                        "✅ Sent notification",
                        "✅ Updated operation status"
                    },
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during flow test");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Sprawdza stan systemu i kluczowych komponentów
        /// </summary>
        /// <returns>Stan systemu</returns>
        [HttpGet("system-status")]
        public IActionResult SystemStatus()
        {
            try
            {
                var systemInfo = new
                {
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                    MachineName = Environment.MachineName,
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingSet = Environment.WorkingSet,
                    TickCount = Environment.TickCount64,
                    ClrVersion = Environment.Version.ToString(),
                    OsVersion = Environment.OSVersion.ToString(),
                    UserDomainName = Environment.UserDomainName,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(new
                {
                    Status = "Healthy",
                    Message = "System is operational",
                    SystemInformation = systemInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system status");
                return StatusCode(500, new
                {
                    Status = "Unhealthy",
                    Error = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }
} 