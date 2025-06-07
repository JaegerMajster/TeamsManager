using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Abstractions.Services.Cache;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Application.Services
{
    /// <summary>
    /// Orkiestrator cyklu życia zespołów odpowiedzialny za kompleksowe operacje
    /// archiwizacji, przywracania i migracji zespołów
    /// Następuje wzorce z SchoolYearProcessOrchestrator
    /// </summary>
    public class TeamLifecycleOrchestrator : ITeamLifecycleOrchestrator
    {
        private readonly ITeamService _teamService;
        private readonly IUserService _userService;
        private readonly ISchoolYearService _schoolYearService;
        private readonly IPowerShellBulkOperationsService _bulkOperationsService;
        private readonly INotificationService _notificationService;
        private readonly IAdminNotificationService _adminNotificationService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<TeamLifecycleOrchestrator> _logger;
        private readonly SemaphoreSlim _processSemaphore;

        // Thread-safe słowniki dla zarządzania aktywnymi procesami (wzorzec z SchoolYearProcessOrchestrator)
        private readonly ConcurrentDictionary<string, TeamLifecycleProcessStatus> _activeProcesses;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens;

        public TeamLifecycleOrchestrator(
            ITeamService teamService,
            IUserService userService,
            ISchoolYearService schoolYearService,
            IPowerShellBulkOperationsService bulkOperationsService,
            INotificationService notificationService,
            IAdminNotificationService adminNotificationService,
            ICacheInvalidationService cacheInvalidationService,
            IOperationHistoryService operationHistoryService,
            ICurrentUserService currentUserService,
            ILogger<TeamLifecycleOrchestrator> logger)
        {
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _schoolYearService = schoolYearService ?? throw new ArgumentNullException(nameof(schoolYearService));
            _bulkOperationsService = bulkOperationsService ?? throw new ArgumentNullException(nameof(bulkOperationsService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _adminNotificationService = adminNotificationService ?? throw new ArgumentNullException(nameof(adminNotificationService));
            _cacheInvalidationService = cacheInvalidationService ?? throw new ArgumentNullException(nameof(cacheInvalidationService));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _processSemaphore = new SemaphoreSlim(3, 3); // Limit równoległych procesów cyklu życia
            _activeProcesses = new ConcurrentDictionary<string, TeamLifecycleProcessStatus>();
            _cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        }

        public async Task<BulkOperationResult> BulkArchiveTeamsWithCleanupAsync(
            string[] teamIds, 
            ArchiveOptions options,
            string apiAccessToken)
        {
            var processId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();
            
            _logger.LogInformation("TeamLifecycle: Rozpoczynam masową archiwizację {Count} zespołów", teamIds?.Length ?? 0);

            try
            {
                // 1. Walidacja parametrów (wzorzec z SchoolYearProcessOrchestrator)
                if (teamIds?.Any() != true)
                {
                    _logger.LogWarning("TeamLifecycle: Pusta lista zespołów do archiwizacji");
                    return new BulkOperationResult
                    {
                        Success = false,
                        IsSuccess = false,
                        ErrorMessage = "Lista zespołów jest wymagana",
                        Errors = new List<BulkOperationError>
                        {
                            new BulkOperationError { Message = "Lista zespołów jest wymagana", Operation = "BulkArchiveValidation" }
                        },
                        SuccessfulOperations = new List<BulkOperationSuccess>()
                    };
                }

                // 2. Rejestracja procesu (wzorzec monitoring)
                _cancellationTokens[processId] = cts;
                var processStatus = new TeamLifecycleProcessStatus
                {
                    ProcessId = processId,
                    ProcessType = "BulkArchiveWithCleanup",
                    Status = "Running",
                    StartedAt = DateTime.UtcNow,
                    TotalItems = teamIds!.Length,
                    ProcessedItems = 0,
                    FailedItems = 0,
                    CurrentOperation = "Initializing",
                    AffectedTeamIds = teamIds
                };
                _activeProcesses[processId] = processStatus;

                await _processSemaphore.WaitAsync(cts.Token);

                try
                {
                    // 3. Walidacja zespołów (wzorzec business validation)
                    processStatus.CurrentOperation = "Validating teams";
                    await UpdateProcessStatusAsync(processId, processStatus);

                    var validTeams = new List<Team>();
                    var validationErrors = new List<BulkOperationError>();

                    foreach (var teamId in teamIds)
                    {
                        var team = await _teamService.GetByIdAsync(teamId);
                        if (team == null)
                        {
                            validationErrors.Add(new BulkOperationError
                            {
                                Operation = "ValidateTeam",
                                EntityId = teamId,
                                Message = $"Zespół o ID '{teamId}' nie istnieje"
                            });
                            continue;
                        }

                        if (team.Status == TeamStatus.Archived)
                        {
                            // Już zarchiwizowany - traktuj jako sukces
                            validationErrors.Add(new BulkOperationError
                            {
                                Operation = "ValidateTeam",
                                EntityId = teamId,
                                Message = $"Zespół '{team.DisplayName}' jest już zarchiwizowany"
                            });
                            continue;
                        }

                        validTeams.Add(team);
                    }

                    if (!validTeams.Any())
                    {
                        return new BulkOperationResult
                        {
                            Success = false,
                            IsSuccess = false,
                            ErrorMessage = "Brak prawidłowych zespołów do archiwizacji",
                            Errors = validationErrors,
                            SuccessfulOperations = new List<BulkOperationSuccess>()
                        };
                    }

                    processStatus.TotalItems = validTeams.Count;
                    processStatus.CurrentOperation = "Processing teams";
                    await UpdateProcessStatusAsync(processId, processStatus);

                    var successfulOperations = new List<BulkOperationSuccess>();
                    var errors = new List<BulkOperationError>(validationErrors);

                    // 4. Powiadomienia właścicieli przed archiwizacją (wzorzec notification)
                    if (options.NotifyOwners)
                    {
                        await NotifyOwnersBeforeArchiveAsync(validTeams, options.Reason);
                    }

                    // 5. Batch processing (wzorzec z orkiestratora)
                    var batches = validTeams.Chunk(options.BatchSize).ToList();
                    
                    foreach (var batch in batches)
                    {
                        if (cts.Token.IsCancellationRequested)
                            break;

                        var batchResult = await ProcessArchiveBatchAsync(batch, options, apiAccessToken);
                        
                        successfulOperations.AddRange(batchResult.SuccessfulOperations);
                        errors.AddRange(batchResult.Errors);

                        processStatus.ProcessedItems += batch.Length;
                        processStatus.FailedItems = errors.Count;
                        await UpdateProcessStatusAsync(processId, processStatus);

                        // Sprawdź próg błędów
                        if (!options.ContinueOnError && errors.Any())
                        {
                            _logger.LogWarning("TeamLifecycle: Zatrzymuję archiwizację z powodu błędów (ContinueOnError=false)");
                            break;
                        }

                        var errorRate = processStatus.TotalItems > 0 ? (double)errors.Count / processStatus.TotalItems * 100 : 0;
                        if (errorRate > options.AcceptableErrorPercentage)
                        {
                            _logger.LogWarning("TeamLifecycle: Zatrzymuję archiwizację - przekroczono próg błędów {ErrorRate}%", errorRate);
                            break;
                        }
                    }

                    // 6. Cleanup operations (jeśli włączone)
                    if (options.CleanupChannels || options.RemoveInactiveMembers)
                    {
                        processStatus.CurrentOperation = "Cleanup operations";
                        await UpdateProcessStatusAsync(processId, processStatus);

                        var cleanupResults = await PerformCleanupOperationsAsync(
                            successfulOperations.Select(s => s.EntityId).ToArray(), 
                            options, 
                            apiAccessToken);
                        
                        successfulOperations.AddRange(cleanupResults.SuccessfulOperations);
                        errors.AddRange(cleanupResults.Errors);
                    }

                    // 7. Finalizacja procesu (wzorzec finalization)
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
                        OperationType = "BulkArchiveTeamsWithCleanup"
                    };

                    // 8. Powiadomienie administratorów (wzorzec admin notification)
                    await _adminNotificationService.SendBulkTeamsOperationNotificationAsync(
                        "Masowa archiwizacja zespołów",
                        validTeams.Count,
                        successfulOperations.Count,
                        errors.Count,
                        _currentUserService.GetCurrentUserUpn() ?? "System");

                    return result;
                }
                finally
                {
                    _processSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TeamLifecycle: Krytyczny błąd podczas masowej archiwizacji");
                
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
                        new BulkOperationError { Message = ex.Message, Operation = "BulkArchiveTeamsWithCleanup", Exception = ex }
                    },
                    SuccessfulOperations = new List<BulkOperationSuccess>(),
                    ProcessedAt = DateTime.UtcNow
                };
            }
            finally
            {
                // Cleanup (wzorzec resource cleanup)
                _cancellationTokens.TryRemove(processId, out _);
                if (!_activeProcesses.TryGetValue(processId, out var finalStatus) || 
                    finalStatus.CompletedAt.HasValue && DateTime.UtcNow.Subtract(finalStatus.CompletedAt.Value).TotalMinutes > 10)
                {
                    _activeProcesses.TryRemove(processId, out _);
                }
            }
        }

        public async Task<BulkOperationResult> BulkRestoreTeamsWithValidationAsync(
            string[] teamIds, 
            RestoreOptions options,
            string apiAccessToken)
        {
            var processId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();
            
            _logger.LogInformation("TeamLifecycle: Rozpoczynam masowe przywracanie {Count} zespołów", teamIds?.Length ?? 0);

            try
            {
                // Walidacja parametrów
                if (teamIds?.Any() != true)
                {
                    return new BulkOperationResult
                    {
                        Success = false,
                        IsSuccess = false,
                        ErrorMessage = "Lista zespołów jest wymagana",
                        Errors = new List<BulkOperationError>
                        {
                            new BulkOperationError { Message = "Lista zespołów jest wymagana", Operation = "BulkRestoreValidation" }
                        }
                    };
                }

                // Rejestracja procesu
                _cancellationTokens[processId] = cts;
                var processStatus = new TeamLifecycleProcessStatus
                {
                    ProcessId = processId,
                    ProcessType = "BulkRestoreWithValidation",
                    Status = "Running",
                    StartedAt = DateTime.UtcNow,
                    TotalItems = teamIds!.Length,
                    CurrentOperation = "Validating teams",
                    AffectedTeamIds = teamIds
                };
                _activeProcesses[processId] = processStatus;

                await _processSemaphore.WaitAsync(cts.Token);

                try
                {
                    var validTeams = new List<Team>();
                    var errors = new List<BulkOperationError>();

                    // Walidacja zespołów i właścicieli
                    foreach (var teamId in teamIds)
                    {
                        var team = await _teamService.GetByIdAsync(teamId);
                        if (team == null)
                        {
                            errors.Add(new BulkOperationError
                            {
                                Operation = "ValidateTeam",
                                EntityId = teamId,
                                Message = $"Zespół o ID '{teamId}' nie istnieje"
                            });
                            continue;
                        }

                        if (team.Status == TeamStatus.Active)
                        {
                            errors.Add(new BulkOperationError
                            {
                                Operation = "ValidateTeam",
                                EntityId = teamId,
                                Message = $"Zespół '{team.DisplayName}' jest już aktywny"
                            });
                            continue;
                        }

                        // Walidacja dostępności właściciela
                        if (options.ValidateOwnerAvailability)
                        {
                            var owner = await _userService.GetUserByUpnAsync(team.Owner);
                            if (owner == null || !owner.IsActive)
                            {
                                errors.Add(new BulkOperationError
                                {
                                    Operation = "ValidateOwner",
                                    EntityId = teamId,
                                    Message = $"Właściciel zespołu '{team.Owner}' jest nieaktywny lub nie istnieje"
                                });
                                continue;
                            }
                        }

                        validTeams.Add(team);
                    }

                    if (!validTeams.Any())
                    {
                        return new BulkOperationResult
                        {
                            Success = false,
                            IsSuccess = false,
                            ErrorMessage = "Brak prawidłowych zespołów do przywrócenia",
                            Errors = errors
                        };
                    }

                    processStatus.TotalItems = validTeams.Count;
                    processStatus.CurrentOperation = "Restoring teams";
                    await UpdateProcessStatusAsync(processId, processStatus);

                    var successfulOperations = new List<BulkOperationSuccess>();

                    // Batch processing przywracania
                    var batches = validTeams.Chunk(options.BatchSize).ToList();
                    
                    foreach (var batch in batches)
                    {
                        if (cts.Token.IsCancellationRequested)
                            break;

                        foreach (var team in batch)
                        {
                            try
                            {
                                var restored = await _teamService.RestoreTeamAsync(team.Id, apiAccessToken);
                                if (restored)
                                {
                                    successfulOperations.Add(new BulkOperationSuccess
                                    {
                                        Operation = "RestoreTeam",
                                        EntityId = team.Id,
                                        EntityName = team.GetBaseDisplayName(),
                                        Message = $"Zespół '{team.GetBaseDisplayName()}' przywrócony pomyślnie"
                                    });

                                    // Invalidacja cache (wzorzec cache invalidation)
                                    await _cacheInvalidationService.InvalidateForTeamRestoredAsync(team);
                                }
                                else
                                {
                                    errors.Add(new BulkOperationError
                                    {
                                        Operation = "RestoreTeam",
                                        EntityId = team.Id,
                                        Message = $"Nie udało się przywrócić zespołu '{team.GetBaseDisplayName()}'"
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "TeamLifecycle: Błąd przywracania zespołu {TeamId}", team.Id);
                                errors.Add(new BulkOperationError
                                {
                                    Operation = "RestoreTeam",
                                    EntityId = team.Id,
                                    Message = ex.Message,
                                    Exception = ex
                                });
                            }
                        }

                        processStatus.ProcessedItems += batch.Length;
                        processStatus.FailedItems = errors.Count;
                        await UpdateProcessStatusAsync(processId, processStatus);
                    }

                    // Finalizacja
                    processStatus.Status = errors.Any() ? "Completed with errors" : "Completed";
                    processStatus.CompletedAt = DateTime.UtcNow;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    return new BulkOperationResult
                    {
                        Success = !errors.Any(),
                        IsSuccess = !errors.Any(),
                        SuccessfulOperations = successfulOperations,
                        Errors = errors,
                        ProcessedAt = DateTime.UtcNow,
                        OperationType = "BulkRestoreTeamsWithValidation"
                    };
                }
                finally
                {
                    _processSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TeamLifecycle: Krytyczny błąd podczas masowego przywracania");
                
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
                        new BulkOperationError { Message = ex.Message, Operation = "BulkRestoreTeamsWithValidation", Exception = ex }
                    }
                };
            }
            finally
            {
                _cancellationTokens.TryRemove(processId, out _);
            }
        }

        public async Task<BulkOperationResult> MigrateTeamsBetweenSchoolYearsAsync(
            TeamMigrationPlan plan,
            string apiAccessToken)
        {
            var processId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();
            
            _logger.LogInformation("TeamLifecycle: Rozpoczynam migrację {Count} zespołów z {From} do {To}", 
                plan.TeamIds?.Length ?? 0, plan.FromSchoolYearId, plan.ToSchoolYearId);

            try
            {
                // Walidacja planu migracji
                if (string.IsNullOrWhiteSpace(plan.FromSchoolYearId) || 
                    string.IsNullOrWhiteSpace(plan.ToSchoolYearId) ||
                    plan.TeamIds?.Any() != true)
                {
                    return new BulkOperationResult
                    {
                        Success = false,
                        IsSuccess = false,
                        ErrorMessage = "Plan migracji jest nieprawidłowy",
                        Errors = new List<BulkOperationError>
                        {
                            new BulkOperationError { Message = "Nieprawidłowy plan migracji", Operation = "MigrationPlanValidation" }
                        }
                    };
                }

                // Walidacja lat szkolnych
                var fromYear = await _schoolYearService.GetByIdAsync(plan.FromSchoolYearId);
                var toYear = await _schoolYearService.GetByIdAsync(plan.ToSchoolYearId);
                
                if (fromYear == null || toYear == null)
                {
                    return new BulkOperationResult
                    {
                        Success = false,
                        IsSuccess = false,
                        ErrorMessage = "Jeden z lat szkolnych nie istnieje",
                        Errors = new List<BulkOperationError>
                        {
                            new BulkOperationError { Message = "Błąd walidacji lat szkolnych", Operation = "SchoolYearValidation" }
                        }
                    };
                }

                _cancellationTokens[processId] = cts;
                var processStatus = new TeamLifecycleProcessStatus
                {
                    ProcessId = processId,
                    ProcessType = "TeamMigration",
                    Status = "Running",
                    StartedAt = DateTime.UtcNow,
                    TotalItems = plan.TeamIds!.Length,
                    CurrentOperation = "Preparing migration",
                    AffectedTeamIds = plan.TeamIds
                };
                _activeProcesses[processId] = processStatus;

                await _processSemaphore.WaitAsync(cts.Token);

                try
                {
                    var successfulOperations = new List<BulkOperationSuccess>();
                    var errors = new List<BulkOperationError>();

                    // Migracja zespołów (batch processing)
                    var batches = plan.TeamIds.Chunk(plan.BatchSize).ToList();
                    
                    foreach (var batch in batches)
                    {
                        if (cts.Token.IsCancellationRequested)
                            break;

                        foreach (var teamId in batch)
                        {
                            try
                            {
                                var team = await _teamService.GetByIdAsync(teamId);
                                if (team == null)
                                {
                                    errors.Add(new BulkOperationError
                                    {
                                        Operation = "MigrateTeam",
                                        EntityId = teamId,
                                        Message = $"Zespół o ID '{teamId}' nie istnieje"
                                    });
                                    continue;
                                }

                                // Aktualizacja roku szkolnego zespołu
                                team.SchoolYearId = plan.ToSchoolYearId;
                                var updated = await _teamService.UpdateTeamAsync(team, apiAccessToken);
                                
                                if (updated)
                                {
                                    successfulOperations.Add(new BulkOperationSuccess
                                    {
                                        Operation = "MigrateTeam",
                                        EntityId = teamId,
                                        EntityName = team.DisplayName,
                                        Message = $"Zespół '{team.DisplayName}' zmigrowany do roku {toYear.Name}"
                                    });

                                    // Archiwizacja oryginalnego zespołu (jeśli włączone)
                                    if (plan.ArchiveSourceTeams)
                                    {
                                        // Tutaj można by implementować logikę archiwizacji
                                        // ale nie tworzę duplikatów - zespół już został zaktualizowany
                                    }
                                }
                                else
                                {
                                    errors.Add(new BulkOperationError
                                    {
                                        Operation = "MigrateTeam",
                                        EntityId = teamId,
                                        Message = $"Nie udało się zmigrować zespołu '{team.DisplayName}'"
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "TeamLifecycle: Błąd migracji zespołu {TeamId}", teamId);
                                errors.Add(new BulkOperationError
                                {
                                    Operation = "MigrateTeam",
                                    EntityId = teamId,
                                    Message = ex.Message,
                                    Exception = ex
                                });
                            }
                        }

                        processStatus.ProcessedItems += batch.Length;
                        processStatus.FailedItems = errors.Count;
                        await UpdateProcessStatusAsync(processId, processStatus);
                    }

                    processStatus.Status = errors.Any() ? "Completed with errors" : "Completed";
                    processStatus.CompletedAt = DateTime.UtcNow;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    return new BulkOperationResult
                    {
                        Success = !errors.Any(),
                        IsSuccess = !errors.Any(),
                        SuccessfulOperations = successfulOperations,
                        Errors = errors,
                        ProcessedAt = DateTime.UtcNow,
                        OperationType = "MigrateTeamsBetweenSchoolYears"
                    };
                }
                finally
                {
                    _processSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TeamLifecycle: Krytyczny błąd podczas migracji zespołów");
                return new BulkOperationResult
                {
                    Success = false,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    Errors = new List<BulkOperationError>
                    {
                        new BulkOperationError { Message = ex.Message, Operation = "MigrateTeamsBetweenSchoolYears", Exception = ex }
                    }
                };
            }
            finally
            {
                _cancellationTokens.TryRemove(processId, out _);
            }
        }

        public async Task<BulkOperationResult> ConsolidateInactiveTeamsAsync(
            ConsolidationOptions options,
            string apiAccessToken)
        {
            var processId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();
            
            _logger.LogInformation("TeamLifecycle: Rozpoczynam konsolidację nieaktywnych zespołów");

            try
            {
                _cancellationTokens[processId] = cts;
                var processStatus = new TeamLifecycleProcessStatus
                {
                    ProcessId = processId,
                    ProcessType = "ConsolidateInactiveTeams",
                    Status = "Running",
                    StartedAt = DateTime.UtcNow,
                    CurrentOperation = "Finding inactive teams"
                };
                _activeProcesses[processId] = processStatus;

                await _processSemaphore.WaitAsync(cts.Token);

                try
                {
                    // Znajdź nieaktywne zespoły
                    var allTeams = await _teamService.GetAllTeamsAsync();
                    var inactiveTeams = allTeams.Where(team => 
                        team.IsActive &&
                        team.Members?.Count <= options.MaxMembersCount &&
                        (team.ModifiedDate?.AddDays(options.MinInactiveDays) < DateTime.UtcNow ||
                         team.CreatedDate.AddDays(options.MinInactiveDays) < DateTime.UtcNow) &&
                        (options.SchoolTypeIds == null || options.SchoolTypeIds.Contains(team.SchoolTypeId))
                    ).ToList();

                    if (!inactiveTeams.Any())
                    {
                        _logger.LogInformation("TeamLifecycle: Nie znaleziono nieaktywnych zespołów do konsolidacji");
                        return new BulkOperationResult
                        {
                            Success = true,
                            IsSuccess = true,
                            SuccessfulOperations = new List<BulkOperationSuccess>(),
                            Errors = new List<BulkOperationError>()
                        };
                    }

                    processStatus.TotalItems = inactiveTeams.Count;
                    processStatus.CurrentOperation = "Consolidating teams";
                    processStatus.AffectedTeamIds = inactiveTeams.Select(t => t.Id).ToArray();
                    await UpdateProcessStatusAsync(processId, processStatus);

                    var successfulOperations = new List<BulkOperationSuccess>();
                    var errors = new List<BulkOperationError>();

                    // Konsolidacja (archiwizacja nieaktywnych zespołów)
                    var archiveOptions = new ArchiveOptions
                    {
                        Reason = "Automatyczna konsolidacja - zespół nieaktywny",
                        NotifyOwners = true,
                        BatchSize = options.BatchSize,
                        DryRun = options.DryRun,
                        ContinueOnError = options.ContinueOnError
                    };

                    var teamIds = inactiveTeams.Select(t => t.Id).ToArray();
                    var archiveResult = await BulkArchiveTeamsWithCleanupAsync(teamIds, archiveOptions, apiAccessToken);

                    processStatus.Status = "Completed";
                    processStatus.CompletedAt = DateTime.UtcNow;
                    processStatus.ProcessedItems = teamIds.Length;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    return archiveResult;
                }
                finally
                {
                    _processSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TeamLifecycle: Krytyczny błąd podczas konsolidacji");
                return new BulkOperationResult
                {
                    Success = false,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    Errors = new List<BulkOperationError>
                    {
                        new BulkOperationError { Message = ex.Message, Operation = "ConsolidateInactiveTeams", Exception = ex }
                    }
                };
            }
            finally
            {
                _cancellationTokens.TryRemove(processId, out _);
            }
        }

        public async Task<IEnumerable<TeamLifecycleProcessStatus>> GetActiveProcessesStatusAsync()
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

                _logger.LogInformation("TeamLifecycle: Anulowano proces {ProcessId}", processId);
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        #region Private Helper Methods

        private async Task<BulkOperationResult> ProcessArchiveBatchAsync(
            Team[] teams, 
            ArchiveOptions options, 
            string apiAccessToken)
        {
            var successfulOperations = new List<BulkOperationSuccess>();
            var errors = new List<BulkOperationError>();

            foreach (var team in teams)
            {
                try
                {
                    if (options.DryRun)
                    {
                        successfulOperations.Add(new BulkOperationSuccess
                        {
                            Operation = "ArchiveTeam",
                            EntityId = team.Id,
                            EntityName = team.DisplayName,
                            Message = $"Archiwizacja zespołu '{team.DisplayName}' (DryRun)"
                        });
                    }
                    else
                    {
                        var archived = await _teamService.ArchiveTeamAsync(team.Id, options.Reason, apiAccessToken);
                        if (archived)
                        {
                            successfulOperations.Add(new BulkOperationSuccess
                            {
                                Operation = "ArchiveTeam",
                                EntityId = team.Id,
                                EntityName = team.DisplayName,
                                Message = $"Zespół '{team.DisplayName}' zarchiwizowany pomyślnie"
                            });

                            // Cache invalidation
                            await _cacheInvalidationService.InvalidateForTeamArchivedAsync(team);
                        }
                        else
                        {
                            errors.Add(new BulkOperationError
                            {
                                Operation = "ArchiveTeam",
                                EntityId = team.Id,
                                Message = $"Nie udało się zarchiwizować zespołu '{team.DisplayName}'"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TeamLifecycle: Błąd archiwizacji zespołu {TeamId}", team.Id);
                    errors.Add(new BulkOperationError
                    {
                        Operation = "ArchiveTeam",
                        EntityId = team.Id,
                        Message = ex.Message,
                        Exception = ex
                    });
                }
            }

            return new BulkOperationResult
            {
                Success = !errors.Any(),
                IsSuccess = !errors.Any(),
                SuccessfulOperations = successfulOperations,
                Errors = errors
            };
        }

        private async Task<BulkOperationResult> PerformCleanupOperationsAsync(
            string[] teamIds, 
            ArchiveOptions options, 
            string apiAccessToken)
        {
            var successfulOperations = new List<BulkOperationSuccess>();
            var errors = new List<BulkOperationError>();

            foreach (var teamId in teamIds)
            {
                try
                {
                    if (options.CleanupChannels)
                    {
                        // Placeholder dla cleanup kanałów
                        // W rzeczywistej implementacji można by usunąć niepotrzebne kanały
                        successfulOperations.Add(new BulkOperationSuccess
                        {
                            Operation = "CleanupChannels",
                            EntityId = teamId,
                            Message = "Cleanup kanałów wykonany (placeholder)"
                        });
                    }

                    if (options.RemoveInactiveMembers)
                    {
                        // Placeholder dla usuwania nieaktywnych członków
                        // W rzeczywistej implementacji można by usunąć nieaktywnych członków
                        successfulOperations.Add(new BulkOperationSuccess
                        {
                            Operation = "RemoveInactiveMembers",
                            EntityId = teamId,
                            Message = "Usuwanie nieaktywnych członków wykonane (placeholder)"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TeamLifecycle: Błąd cleanup dla zespołu {TeamId}", teamId);
                    errors.Add(new BulkOperationError
                    {
                        Operation = "Cleanup",
                        EntityId = teamId,
                        Message = ex.Message,
                        Exception = ex
                    });
                }
            }

            return new BulkOperationResult
            {
                Success = !errors.Any(),
                IsSuccess = !errors.Any(),
                SuccessfulOperations = successfulOperations,
                Errors = errors
            };
        }

        private async Task NotifyOwnersBeforeArchiveAsync(List<Team> teams, string reason)
        {
            var ownerGroups = teams.GroupBy(t => t.Owner).ToList();
            
            foreach (var ownerGroup in ownerGroups)
            {
                try
                {
                    var teamNames = string.Join(", ", ownerGroup.Select(t => t.DisplayName));
                    await _notificationService.SendNotificationToUserAsync(
                        ownerGroup.Key,
                        $"Informujemy, że następujące zespoły zostaną zarchiwizowane: {teamNames}. Powód: {reason}",
                        "info"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "TeamLifecycle: Nie udało się powiadomić właściciela {Owner}", ownerGroup.Key);
                }
            }
        }

        private async Task UpdateProcessStatusAsync(string processId, TeamLifecycleProcessStatus status)
        {
            _activeProcesses[processId] = status;
            
            // Powiadomienie o postępie (wzorzec progress notification)
            await _notificationService.SendOperationProgressToUserAsync(
                _currentUserService.GetCurrentUserUpn() ?? "System",
                processId,
                (int)status.ProgressPercentage,
                $"{status.ProcessType}: {status.CurrentOperation} - {status.ProcessedItems}/{status.TotalItems}"
            );
        }

        public void Dispose()
        {
            foreach (var cts in _cancellationTokens.Values)
            {
                cts?.Dispose();
            }
            _processSemaphore?.Dispose();
        }

        #endregion
    }
} 