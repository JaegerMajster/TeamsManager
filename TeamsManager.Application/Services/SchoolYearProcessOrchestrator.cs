using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Application.Services
{
    /// <summary>
    /// Orkiestrator procesów szkolnych odpowiedzialny za zarządzanie kompleksowymi operacjami
    /// </summary>
    public class SchoolYearProcessOrchestrator : ISchoolYearProcessOrchestrator
    {
        private readonly ITeamService _teamService;
        private readonly ITeamTemplateService _teamTemplateService;
        private readonly ISchoolYearService _schoolYearService;
        private readonly IGraphBulkOperationsService _bulkOperationsService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<SchoolYearProcessOrchestrator> _logger;
        private readonly SemaphoreSlim _processSemaphore;

        // Thread-safe słowniki dla zarządzania aktywnymi procesami
        private readonly ConcurrentDictionary<string, SchoolYearProcessStatus> _activeProcesses;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens;

        public SchoolYearProcessOrchestrator(
            ITeamService teamService,
            ITeamTemplateService teamTemplateService,
            ISchoolYearService schoolYearService,
            IGraphBulkOperationsService bulkOperationsService,
            INotificationService notificationService,
            ILogger<SchoolYearProcessOrchestrator> logger)
        {
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _teamTemplateService = teamTemplateService ?? throw new ArgumentNullException(nameof(teamTemplateService));
            _schoolYearService = schoolYearService ?? throw new ArgumentNullException(nameof(schoolYearService));
            _bulkOperationsService = bulkOperationsService ?? throw new ArgumentNullException(nameof(bulkOperationsService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _processSemaphore = new SemaphoreSlim(2, 2); // Limit równoległych procesów
            _activeProcesses = new ConcurrentDictionary<string, SchoolYearProcessStatus>();
            _cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        }

        public async Task<BulkOperationResult> CreateTeamsForNewSchoolYearAsync(
            string schoolYearId, 
            string[] templateIds, 
            string apiAccessToken,
            SchoolYearProcessOptions? options = null)
        {
            var processId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();
            
            _logger.LogInformation("Orkiestrator: Rozpoczynam tworzenie zespołów dla roku szkolnego {SchoolYearId}", schoolYearId);

            try
            {
                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(schoolYearId))
                {
                    _logger.LogWarning("Orkiestrator: Pusty ID roku szkolnego");
                    return new BulkOperationResult
                    {
                        Success = false,
                        IsSuccess = false,
                        ErrorMessage = "ID roku szkolnego jest wymagane",
                        Errors = new List<BulkOperationError>
                        {
                            new BulkOperationError { Message = "ID roku szkolnego jest wymagane", Operation = "Validation" }
                        },
                        SuccessfulOperations = new List<BulkOperationSuccess>()
                    };
                }

                if (templateIds?.Any() != true)
                {
                    _logger.LogWarning("Orkiestrator: Pusta lista szablonów");
                    return new BulkOperationResult
                    {
                        Success = false,
                        IsSuccess = false,
                        ErrorMessage = "Lista szablonów jest wymagana",
                        Errors = new List<BulkOperationError>
                        {
                            new BulkOperationError { Message = "Lista szablonów jest wymagana", Operation = "Validation" }
                        },
                        SuccessfulOperations = new List<BulkOperationSuccess>()
                    };
                }

                // Rejestracja procesu
                _cancellationTokens[processId] = cts;
                var processStatus = new SchoolYearProcessStatus
                {
                    ProcessId = processId,
                    Status = "Running",
                    StartedAt = DateTime.UtcNow,
                    TotalItems = templateIds!.Length,
                    ProcessedItems = 0,
                    FailedItems = 0,
                    CurrentOperation = "Inicjalizacja"
                };
                _activeProcesses[processId] = processStatus;

                await _processSemaphore.WaitAsync(cts.Token);

                try
                {
                    // Sprawdź rok szkolny
                    var schoolYear = await _schoolYearService.GetByIdAsync(schoolYearId);
                    if (schoolYear == null)
                    {
                        throw new InvalidOperationException($"Rok szkolny o ID '{schoolYearId}' nie istnieje");
                    }

                    processStatus.CurrentOperation = "Walidacja szablonów";
                    await UpdateProcessStatusAsync(processId, processStatus);

                    // Walidacja szablonów
                    var templates = new List<TeamTemplate>();
                    foreach (var templateId in templateIds)
                    {
                        var template = await _teamTemplateService.GetByIdAsync(templateId);
                        if (template == null)
                        {
                            _logger.LogWarning("Orkiestrator: Szablon {TemplateId} nie istnieje", templateId);
                            continue;
                        }
                        templates.Add(template);
                    }

                    if (!templates.Any())
                    {
                        throw new InvalidOperationException("Żaden z podanych szablonów nie istnieje");
                    }

                    processStatus.CurrentOperation = "Tworzenie zespołów";
                    processStatus.TotalItems = templates.Count;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    var successfulOperations = new List<BulkOperationSuccess>();
                    var errors = new List<BulkOperationError>();

                    // Przetwarzanie szablonów
                    foreach (var template in templates)
                    {
                        if (cts.Token.IsCancellationRequested)
                            break;

                        try
                        {
                            // Generowanie planu tworzenia zespołu
                            var teamPlans = await GenerateTeamCreationPlansAsync(template, schoolYear);
                            
                            foreach (var plan in teamPlans)
                            {
                                try
                                {
                                    // Symulacja tworzenia zespołu (zastępuje rzeczywiste wywołanie API)
                                    await SimulateTeamCreationAsync(plan, apiAccessToken);
                                    
                                    successfulOperations.Add(new BulkOperationSuccess
                                    {
                                        Operation = "CreateTeam",
                                        EntityId = plan.TeamId,
                                        Message = $"Zespół '{plan.TeamName}' utworzony pomyślnie (symulacja)"
                                    });

                                    processStatus.ProcessedItems++;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Orkiestrator: Błąd podczas tworzenia zespołu dla planu {PlanId}", plan.TeamId);
                                    errors.Add(new BulkOperationError
                                    {
                                        Operation = "CreateTeam",
                                        EntityId = plan.TeamId,
                                        Message = ex.Message
                                    });
                                    processStatus.FailedItems++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Orkiestrator: Błąd podczas przetwarzania szablonu {TemplateId}", template.Id);
                            errors.Add(new BulkOperationError
                            {
                                Operation = "ProcessTemplate",
                                EntityId = template.Id,
                                Message = ex.Message
                            });
                            processStatus.FailedItems++;
                        }

                        await UpdateProcessStatusAsync(processId, processStatus);
                    }

                    // Finalizacja procesu
                    processStatus.Status = errors.Any() ? "Completed with errors" : "Completed";
                    processStatus.CompletedAt = DateTime.UtcNow;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    var result = new BulkOperationResult
                    {
                        Success = !errors.Any(),
                        IsSuccess = !errors.Any(),
                        SuccessfulOperations = successfulOperations,
                        Errors = errors,
                        ProcessedAt = DateTime.UtcNow,
                        OperationType = "CreateTeamsForNewSchoolYear"
                    };

                    await SendNotificationAsync($"Orkiestrator: Zakończono tworzenie zespołów. Sukces: {successfulOperations.Count}, Błędy: {errors.Count}");

                    return result;
                }
                finally
                {
                    _processSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Orkiestrator: Krytyczny błąd podczas tworzenia zespołów dla roku {SchoolYearId}", schoolYearId);
                
                if (_activeProcesses.TryGetValue(processId, out var status))
                {
                    status.Status = "Failed";
                    status.CompletedAt = DateTime.UtcNow;
                    status.ErrorMessage = ex.Message;
                }

                return new BulkOperationResult
                {
                    Success = false,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    Errors = new List<BulkOperationError>
                    {
                        new BulkOperationError { Message = ex.Message, Operation = "CreateTeamsForNewSchoolYear", Exception = ex }
                    },
                    SuccessfulOperations = new List<BulkOperationSuccess>(),
                    ProcessedAt = DateTime.UtcNow
                };
            }
            finally
            {
                // Cleanup
                _cancellationTokens.TryRemove(processId, out _);
                if (!_activeProcesses.TryGetValue(processId, out var finalStatus) || 
                    finalStatus.CompletedAt.HasValue && DateTime.UtcNow.Subtract(finalStatus.CompletedAt.Value).TotalMinutes > 5)
                {
                    _activeProcesses.TryRemove(processId, out _);
                }
            }
        }

        public async Task<BulkOperationResult> ArchiveTeamsFromPreviousSchoolYearAsync(
            string previousSchoolYearId, 
            string apiAccessToken, 
            SchoolYearProcessOptions? options = null)
        {
            options ??= new SchoolYearProcessOptions();
            
            _logger.LogInformation("Orkiestrator: Rozpoczynam archiwizację zespołów z roku {SchoolYearId}", previousSchoolYearId);

            try
            {
                // Pobierz zespoły z poprzedniego roku
                var teams = await _teamService.GetTeamsBySchoolYearAsync(previousSchoolYearId);
                var activeTeams = teams.Where(t => t.IsActive).ToArray();

                if (!activeTeams.Any())
                {
                    _logger.LogInformation("Orkiestrator: Brak aktywnych zespołów do archiwizacji w roku {SchoolYearId}", previousSchoolYearId);
                    return new BulkOperationResult
                    {
                        Success = true,
                        IsSuccess = true,
                        SuccessfulOperations = new List<BulkOperationSuccess>(),
                        Errors = new List<BulkOperationError>()
                    };
                }

                var teamIds = activeTeams.Select(t => t.Id).ToArray();
                
                if (options.DryRun)
                {
                    _logger.LogInformation("Orkiestrator: DryRun - symulacja archiwizacji {Count} zespołów", teamIds.Length);
                    return new BulkOperationResult
                    {
                        Success = true,
                        IsSuccess = true,
                        SuccessfulOperations = teamIds.Select(id => new BulkOperationSuccess
                        {
                            Operation = "ArchiveTeam",
                            EntityId = id,
                            Message = "Archiwizacja zespołu (DryRun)"
                        }).ToList(),
                        Errors = new List<BulkOperationError>()
                    };
                }

                return await _bulkOperationsService.ArchiveTeamsAsync(teamIds, apiAccessToken, options.BatchSize)
                    .ContinueWith(task => 
                    {
                        if (task.IsCompletedSuccessfully)
                        {
                            var graphResult = task.Result;
                            // Konwersja GraphBulkResult na BulkOperationResult dla kompatybilności z orkiestratorem
                            return new BulkOperationResult
                            {
                                Success = graphResult.Success,
                                IsSuccess = graphResult.Success,
                                ErrorMessage = graphResult.ErrorMessage,
                                ProcessedAt = DateTime.UtcNow,
                                OperationType = "ArchiveTeamsFromPreviousSchoolYear",
                                SuccessfulOperations = graphResult.SuccessfulOperations.Select(s => new BulkOperationSuccess
                                {
                                    Operation = s.Operation,
                                    EntityId = s.EntityId,
                                    EntityName = s.EntityName,
                                    Message = s.Message,
                                    AdditionalData = s.AdditionalData
                                }).ToList(),
                                Errors = graphResult.Errors.Select(e => new BulkOperationError
                                {
                                    Operation = e.Operation,
                                    EntityId = e.EntityId,
                                    EntityName = e.EntityName,
                                    Message = e.Message,
                                    Exception = e.Exception,
                                    AdditionalData = e.AdditionalData
                                }).ToList()
                            };
                        }
                        else
                        {
                            return new BulkOperationResult
                            {
                                Success = false,
                                IsSuccess = false,
                                ErrorMessage = task.Exception?.GetBaseException().Message ?? "Nieznany błąd",
                                Errors = new List<BulkOperationError>
                                {
                                    new BulkOperationError 
                                    { 
                                        Message = task.Exception?.GetBaseException().Message ?? "Nieznany błąd", 
                                        Operation = "ArchiveTeamsFromPreviousSchoolYear",
                                        Exception = task.Exception?.GetBaseException()
                                    }
                                }
                            };
                        }
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Orkiestrator: Błąd podczas archiwizacji zespołów z roku {SchoolYearId}", previousSchoolYearId);
                return new BulkOperationResult
                {
                    Success = false,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    Errors = new List<BulkOperationError>
                    {
                        new BulkOperationError { Message = ex.Message, Operation = "ArchiveTeamsFromPreviousSchoolYear", Exception = ex }
                    }
                };
            }
        }

        public async Task<BulkOperationResult> TransitionToNewSchoolYearAsync(
            string previousSchoolYearId,
            string newSchoolYearId,
            string[] templateIds,
            string apiAccessToken,
            SchoolYearProcessOptions? options = null)
        {
            _logger.LogInformation("Orkiestrator: Rozpoczynam przejście z roku {PreviousYear} do {NewYear}", 
                previousSchoolYearId, newSchoolYearId);

            var allSuccesses = new List<BulkOperationSuccess>();
            var allErrors = new List<BulkOperationError>();

            try
            {
                // Krok 1: Archiwizacja starych zespołów
                var archiveResult = await ArchiveTeamsFromPreviousSchoolYearAsync(previousSchoolYearId, apiAccessToken, options);
                allSuccesses.AddRange(archiveResult.SuccessfulOperations);
                allErrors.AddRange(archiveResult.Errors);

                // Krok 2: Tworzenie nowych zespołów
                var createResult = await CreateTeamsForNewSchoolYearAsync(newSchoolYearId, templateIds, apiAccessToken, options);
                allSuccesses.AddRange(createResult.SuccessfulOperations);
                allErrors.AddRange(createResult.Errors);

                return new BulkOperationResult
                {
                    Success = !allErrors.Any(),
                    IsSuccess = !allErrors.Any(),
                    SuccessfulOperations = allSuccesses,
                    Errors = allErrors,
                    ProcessedAt = DateTime.UtcNow,
                    OperationType = "TransitionToNewSchoolYear"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Orkiestrator: Krytyczny błąd podczas przejścia między latami szkolnymi");
                return new BulkOperationResult
                {
                    Success = false,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    SuccessfulOperations = allSuccesses,
                    Errors = allErrors.Concat(new[] { new BulkOperationError 
                    { 
                        Message = ex.Message, 
                        Operation = "TransitionToNewSchoolYear", 
                        Exception = ex 
                    } }).ToList()
                };
            }
        }

        public async Task<IEnumerable<SchoolYearProcessStatus>> GetActiveProcessesStatusAsync()
        {
            return await Task.FromResult(_activeProcesses.Values.AsEnumerable());
        }

        public async Task<bool> CancelProcessAsync(string processId)
        {
            if (_cancellationTokens.TryGetValue(processId, out var cts))
            {
                cts.Cancel();
                
                if (_activeProcesses.TryGetValue(processId, out var status))
                {
                    status.Status = "Cancelled";
                    status.CompletedAt = DateTime.UtcNow;
                }

                _logger.LogInformation("Orkiestrator: Anulowano proces {ProcessId}", processId);
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        // Metody pomocnicze

        private async Task<List<TeamCreationPlan>> GenerateTeamCreationPlansAsync(TeamTemplate template, SchoolYear schoolYear)
        {
            var plans = new List<TeamCreationPlan>();

            // Generowanie planów na podstawie szablonu

            var plan = new TeamCreationPlan
            {
                TeamId = Guid.NewGuid().ToString(),
                TeamName = $"{template.Name} - {schoolYear.Name}",
                TemplateId = template.Id,
                SchoolYearId = schoolYear.Id
            };

            plans.Add(plan);

            _logger.LogDebug("Orkiestrator: Wygenerowano {Count} planów dla szablonu {TemplateId}", plans.Count, template.Id);
            return await Task.FromResult(plans);
        }

        private async Task SimulateTeamCreationAsync(TeamCreationPlan plan, string apiAccessToken)
        {
            _logger.LogDebug("Orkiestrator: Tworzenie zespołu {TeamId} - {TeamName}", plan.TeamId, plan.TeamName);
            
            await Task.Delay(100);
        }

        private async Task UpdateProcessStatusAsync(string processId, SchoolYearProcessStatus status)
        {
            _activeProcesses[processId] = status;
            
            // Powiadomienie o postępie
            await SendNotificationAsync($"Proces {processId}: {status.CurrentOperation} - {status.ProcessedItems}/{status.TotalItems}");
        }

        private async Task SendNotificationAsync(string message)
        {
            try
            {
                _logger.LogInformation("Orkiestrator: {Message}", message);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Orkiestrator: Nie udało się wysłać powiadomienia: {Message}", message);
            }
        }

        private async Task<List<OperationHistory>> GetProcessHistoryAsync(string processId, int maxResults = 100)
        {
            
            var history = new List<OperationHistory>();
            
            if (_activeProcesses.TryGetValue(processId, out var status))
            {
                var entry = new OperationHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = OperationType.BulkTeamCreation,
                    TargetEntityType = "Process",
                    TargetEntityId = processId,
                    OperationDetails = System.Text.Json.JsonSerializer.Serialize(new 
                    { 
                        ProcessStatus = status.Status,
                        StartTime = status.StartedAt,
                        ProcessedItems = status.ProcessedItems,
                        TotalItems = status.TotalItems
                    }),
                    CreatedBy = "System",
                    StartedAt = status.StartedAt,
                    Status = status.Status == "Completed" ? OperationStatus.Completed : OperationStatus.InProgress
                };

                history.Add(entry);
            }

            return await Task.FromResult(history);
        }

        public void Dispose()
        {
            foreach (var cts in _cancellationTokens.Values)
            {
                cts?.Dispose();
            }
            _processSemaphore?.Dispose();
        }
    }

    /// <summary>
    /// Plan tworzenia pojedynczego zespołu
    /// </summary>
    public class TeamCreationPlan
    {
        public string TeamId { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string TemplateId { get; set; } = string.Empty;
        public string SchoolYearId { get; set; } = string.Empty;
    }
} 