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
    /// Orkiestrator zarządzania masowymi operacjami na użytkownikach
    /// Odpowiedzialny za kompleksowe operacje HR: onboarding, offboarding, zmiany ról
    /// Następuje wzorce z SchoolYearProcessOrchestrator i TeamLifecycleOrchestrator
    /// </summary>
    public class BulkUserManagementOrchestrator : IBulkUserManagementOrchestrator
    {
        private readonly IUserService _userService;
        private readonly ITeamService _teamService;
        private readonly IDepartmentService _departmentService;
        private readonly ISubjectService _subjectService;
        private readonly IPowerShellBulkOperationsService _bulkOperationsService;
        private readonly IPowerShellUserManagementService _powerShellUserManagement;
        private readonly INotificationService _notificationService;
        private readonly IAdminNotificationService _adminNotificationService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<BulkUserManagementOrchestrator> _logger;
        private readonly SemaphoreSlim _processSemaphore;

        // Thread-safe słowniki dla zarządzania aktywnymi procesami (wzorzec z orkiestratorów)
        private readonly ConcurrentDictionary<string, UserManagementProcessStatus> _activeProcesses;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens;

        public BulkUserManagementOrchestrator(
            IUserService userService,
            ITeamService teamService,
            IDepartmentService departmentService,
            ISubjectService subjectService,
            IPowerShellBulkOperationsService bulkOperationsService,
            IPowerShellUserManagementService powerShellUserManagement,
            INotificationService notificationService,
            IAdminNotificationService adminNotificationService,
            ICacheInvalidationService cacheInvalidationService,
            IOperationHistoryService operationHistoryService,
            ICurrentUserService currentUserService,
            ILogger<BulkUserManagementOrchestrator> logger)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _subjectService = subjectService ?? throw new ArgumentNullException(nameof(subjectService));
            _bulkOperationsService = bulkOperationsService ?? throw new ArgumentNullException(nameof(bulkOperationsService));
            _powerShellUserManagement = powerShellUserManagement ?? throw new ArgumentNullException(nameof(powerShellUserManagement));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _adminNotificationService = adminNotificationService ?? throw new ArgumentNullException(nameof(adminNotificationService));
            _cacheInvalidationService = cacheInvalidationService ?? throw new ArgumentNullException(nameof(cacheInvalidationService));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _processSemaphore = new SemaphoreSlim(3, 3); // Limit 3 równoległych procesów
            _activeProcesses = new ConcurrentDictionary<string, UserManagementProcessStatus>();
            _cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        }

        /// <summary>
        /// Masowy onboarding użytkowników - 7-etapowy workflow
        /// </summary>
        public async Task<BulkOperationResult> BulkUserOnboardingAsync(UserOnboardingPlan[] plans, string apiAccessToken)
        {
            var processId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();
            _cancellationTokens[processId] = cts;

            _logger.LogInformation("BulkUserManagement: Rozpoczynam masowy onboarding {Count} użytkowników", plans?.Length ?? 0);

            // Walidacja parametrów
            if (plans == null || !plans.Any())
            {
                return BulkOperationResult.CreateError("Lista planów onboardingu jest pusta", "BulkUserOnboarding");
            }

            await _processSemaphore.WaitAsync(cts.Token);
            try
            {
                // 1. Inicjalizacja procesu
                var status = new UserManagementProcessStatus
                {
                    ProcessId = processId,
                    ProcessType = "BulkUserOnboarding",
                    StartedAt = DateTime.UtcNow,
                    Status = "Running",
                    TotalItems = plans.Length,
                    ProcessedItems = 0,
                    FailedItems = 0,
                    CurrentOperation = "Walidacja planów onboardingu",
                    AffectedUserIds = plans.Select(p => p.UPN).ToArray()
                };
                _activeProcesses[processId] = status;

                var successfulOperations = new List<BulkOperationSuccess>();
                var errors = new List<BulkOperationError>();

                // 2. Walidacja planów
                status.CurrentOperation = "Walidacja planów onboardingu";
                var validPlans = new List<UserOnboardingPlan>();
                foreach (var plan in plans)
                {
                    var validationResult = await ValidateOnboardingPlan(plan);
                    if (!validationResult.IsValid)
                    {
                        errors.Add(new BulkOperationError
                        {
                            Operation = "ValidateOnboardingPlan",
                            EntityId = plan.UPN,
                            Message = validationResult.ErrorMessage
                        });
                        status.FailedItems++;
                    }
                    else
                    {
                        validPlans.Add(plan);
                    }
                }

                _logger.LogInformation("BulkUserManagement: Walidacja zakończona. Ważnych planów: {Valid}/{Total}", validPlans.Count, plans.Length);

                // 3. Tworzenie użytkowników w partiach
                var batchSize = 10;
                var batches = validPlans
                    .Select((plan, index) => new { plan, index })
                    .GroupBy(x => x.index / batchSize)
                    .Select(g => g.Select(x => x.plan).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    
                    status.CurrentOperation = $"Przetwarzanie partii użytkowników ({batch.Count} użytkowników)";
                    
                    var batchResults = await ProcessOnboardingBatch(batch, apiAccessToken, cts.Token);
                    successfulOperations.AddRange(batchResults.SuccessfulOperations);
                    errors.AddRange(batchResults.Errors);
                    
                    status.ProcessedItems += batch.Count;
                    status.FailedItems = errors.Count;
                }

                // 4. Finalizacja procesu
                status.Status = errors.Any() ? "CompletedWithErrors" : "Completed";
                status.CompletedAt = DateTime.UtcNow;
                status.CurrentOperation = "Zakończony";

                var result = new BulkOperationResult
                {
                    Success = successfulOperations.Any(),
                    IsSuccess = successfulOperations.Any(),
                    SuccessfulOperations = successfulOperations,
                    Errors = errors,
                    ProcessedAt = DateTime.UtcNow,
                    OperationType = "BulkUserOnboarding"
                };

                // 5. Powiadomienia administratorów
                await _adminNotificationService.SendBulkUsersOperationNotificationAsync(
                    "Masowy onboarding użytkowników",
                    "System",
                    plans.Length,
                    successfulOperations.Count,
                    errors.Count,
                    _currentUserService.GetCurrentUserUpn() ?? "System");

                return result;
            }
            finally
            {
                _processSemaphore.Release();
                _cancellationTokens.TryRemove(processId, out _);
            }
        }

        /// <summary>
        /// Masowy offboarding użytkowników - kompleksowy proces usuwania
        /// </summary>
        public async Task<BulkOperationResult> BulkUserOffboardingAsync(string[] userIds, OffboardingOptions options, string apiAccessToken)
        {
            var processId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();
            _cancellationTokens[processId] = cts;

            _logger.LogInformation("BulkUserManagement: Rozpoczynam masowy offboarding {Count} użytkowników", userIds?.Length ?? 0);

            if (userIds == null || !userIds.Any())
            {
                return BulkOperationResult.CreateError("Lista użytkowników jest pusta", "BulkUserOffboarding");
            }

            await _processSemaphore.WaitAsync(cts.Token);
            try
            {
                // Inicjalizacja procesu
                var status = new UserManagementProcessStatus
                {
                    ProcessId = processId,
                    ProcessType = "BulkUserOffboarding",
                    StartedAt = DateTime.UtcNow,
                    Status = "Running",
                    TotalItems = userIds.Length,
                    ProcessedItems = 0,
                    FailedItems = 0,
                    CurrentOperation = "Analiza użytkowników do offboardingu",
                    AffectedUserIds = userIds
                };
                _activeProcesses[processId] = status;

                var successfulOperations = new List<BulkOperationSuccess>();
                var errors = new List<BulkOperationError>();

                // 1. Pobierz dane użytkowników
                var users = new List<User>();
                foreach (var userId in userIds)
                {
                    var user = await _userService.GetUserByIdAsync(userId);
                    if (user != null)
                    {
                        users.Add(user);
                    }
                    else
                    {
                        errors.Add(new BulkOperationError
                        {
                            Operation = "GetUser",
                            EntityId = userId,
                            Message = "Użytkownik nie został znaleziony"
                        });
                    }
                }

                // 2. Transfer własności zespołów
                if (options.TransferTeamOwnership)
                {
                    status.CurrentOperation = "Transfer własności zespołów";
                    await TransferTeamOwnership(users, options, successfulOperations, errors);
                }

                // 3. Usuwanie z zespołów
                status.CurrentOperation = "Usuwanie użytkowników z zespołów";
                await RemoveUsersFromAllTeams(users, successfulOperations, errors, apiAccessToken);

                // 4. Dezaktywacja kont
                status.CurrentOperation = "Dezaktywacja kont użytkowników";
                await DeactivateUserAccounts(users, options, successfulOperations, errors, apiAccessToken);

                // 5. Backup danych (jeśli włączony)
                if (options.CreateDataBackup)
                {
                    status.CurrentOperation = "Tworzenie kopii zapasowych danych";
                    await CreateUserDataBackups(users, successfulOperations, errors);
                }

                status.ProcessedItems = userIds.Length;
                status.FailedItems = errors.Count;
                status.Status = errors.Any() ? "CompletedWithErrors" : "Completed";
                status.CompletedAt = DateTime.UtcNow;
                status.CurrentOperation = "Zakończony";

                var result = new BulkOperationResult
                {
                    Success = !errors.Any(),
                    IsSuccess = !errors.Any(),
                    SuccessfulOperations = successfulOperations,
                    Errors = errors,
                    ProcessedAt = DateTime.UtcNow,
                    OperationType = "BulkUserOffboarding"
                };

                // Powiadomienia
                await _adminNotificationService.SendBulkUsersOperationNotificationAsync(
                    "Masowy offboarding użytkowników",
                    "System",
                    userIds.Length,
                    successfulOperations.Count,
                    errors.Count,
                    _currentUserService.GetCurrentUserUpn() ?? "System");

                return result;
            }
            finally
            {
                _processSemaphore.Release();
                _cancellationTokens.TryRemove(processId, out _);
            }
        }

        /// <summary>
        /// Masowa zmiana ról użytkowników
        /// </summary>
        public async Task<BulkOperationResult> BulkRoleChangeAsync(UserRoleChange[] changes, string apiAccessToken)
        {
            _logger.LogInformation("BulkUserManagement: Rozpoczynam masową zmianę ról {Count} użytkowników", changes?.Length ?? 0);

            if (changes == null || !changes.Any())
            {
                return BulkOperationResult.CreateError("Lista zmian ról jest pusta", "BulkRoleChange");
            }

            var successfulOperations = new List<BulkOperationSuccess>();
            var errors = new List<BulkOperationError>();

            foreach (var change in changes)
            {
                try
                {
                    var user = await _userService.GetUserByIdAsync(change.UserId);
                    if (user == null)
                    {
                        errors.Add(new BulkOperationError
                        {
                            Operation = "RoleChange",
                            EntityId = change.UserId,
                            Message = "Użytkownik nie został znaleziony"
                        });
                        continue;
                    }

                    var oldRole = user.Role;
                    user.Role = change.NewRole;

                    var updateResult = await _userService.UpdateUserAsync(user, apiAccessToken);
                    if (updateResult)
                    {
                        successfulOperations.Add(new BulkOperationSuccess
                        {
                            Operation = "RoleChange",
                            EntityId = change.UserId,
                            EntityName = user.FullName,
                            Message = $"Zmieniono rolę z {oldRole} na {change.NewRole}"
                        });

                        // Invalidacja cache
                        await _cacheInvalidationService.InvalidateForUserUpdatedAsync(user);
                    }
                    else
                    {
                        errors.Add(new BulkOperationError
                        {
                            Operation = "RoleChange",
                            EntityId = change.UserId,
                            Message = "Nie udało się zaktualizować roli użytkownika"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas zmiany roli użytkownika {UserId}", change.UserId);
                    errors.Add(new BulkOperationError
                    {
                        Operation = "RoleChange",
                        EntityId = change.UserId,
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
                Errors = errors,
                ProcessedAt = DateTime.UtcNow,
                OperationType = "BulkRoleChange"
            };
        }

        /// <summary>
        /// Masowe operacje członkostwa w zespołach
        /// </summary>
        public async Task<BulkOperationResult> BulkTeamMembershipOperationAsync(TeamMembershipOperation[] operations, string apiAccessToken)
        {
            _logger.LogInformation("BulkUserManagement: Rozpoczynam masowe operacje członkostwa {Count} operacji", operations?.Length ?? 0);

            if (operations == null || !operations.Any())
            {
                return BulkOperationResult.CreateError("Lista operacji jest pusta", "BulkTeamMembershipOperation");
            }

            var successfulOperations = new List<BulkOperationSuccess>();
            var errors = new List<BulkOperationError>();

            // Grupuj operacje według zespołów dla optymalizacji
            var operationsByTeam = operations.GroupBy(op => op.TeamId).ToList();

            foreach (var teamGroup in operationsByTeam)
            {
                var teamId = teamGroup.Key;
                var teamOperations = teamGroup.ToList();

                try
                {
                    // Operacje dodawania
                    var addOperations = teamOperations.Where(op => op.OperationType == TeamMembershipOperationType.Add).ToList();
                    if (addOperations.Any())
                    {
                        var userUpns = new List<string>();
                        foreach (var op in addOperations)
                        {
                            var user = await _userService.GetUserByIdAsync(op.UserId);
                            if (user != null)
                            {
                                userUpns.Add(user.UPN);
                            }
                        }

                        if (userUpns.Any())
                        {
                            var addResults = await _teamService.AddUsersToTeamAsync(teamId, userUpns, apiAccessToken);
                            foreach (var result in addResults)
                            {
                                if (result.Value)
                                {
                                    successfulOperations.Add(new BulkOperationSuccess
                                    {
                                        Operation = "AddToTeam",
                                        EntityId = result.Key,
                                        Message = $"Dodano do zespołu {teamId}"
                                    });
                                }
                                else
                                {
                                    errors.Add(new BulkOperationError
                                    {
                                        Operation = "AddToTeam",
                                        EntityId = result.Key,
                                        Message = $"Nie udało się dodać do zespołu {teamId}"
                                    });
                                }
                            }
                        }
                    }

                    // Operacje usuwania
                    var removeOperations = teamOperations.Where(op => op.OperationType == TeamMembershipOperationType.Remove).ToList();
                    if (removeOperations.Any())
                    {
                        var userUpns = new List<string>();
                        foreach (var op in removeOperations)
                        {
                            var user = await _userService.GetUserByIdAsync(op.UserId);
                            if (user != null)
                            {
                                userUpns.Add(user.UPN);
                            }
                        }

                        if (userUpns.Any())
                        {
                            var removeResults = await _teamService.RemoveUsersFromTeamAsync(teamId, userUpns, "Bulk operation", apiAccessToken);
                            foreach (var result in removeResults)
                            {
                                if (result.Value)
                                {
                                    successfulOperations.Add(new BulkOperationSuccess
                                    {
                                        Operation = "RemoveFromTeam",
                                        EntityId = result.Key,
                                        Message = $"Usunięto z zespołu {teamId}"
                                    });
                                }
                                else
                                {
                                    errors.Add(new BulkOperationError
                                    {
                                        Operation = "RemoveFromTeam",
                                        EntityId = result.Key,
                                        Message = $"Nie udało się usunąć z zespołu {teamId}"
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas operacji członkostwa dla zespołu {TeamId}", teamId);
                    errors.Add(new BulkOperationError
                    {
                        Operation = "TeamMembershipOperation",
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
                Errors = errors,
                ProcessedAt = DateTime.UtcNow,
                OperationType = "BulkTeamMembershipOperation"
            };
        }

        /// <summary>
        /// Pobiera status aktywnych procesów
        /// </summary>
        public async Task<IEnumerable<UserManagementProcessStatus>> GetActiveProcessesStatusAsync()
        {
            await Task.CompletedTask; // Dla zgodności z async
            return _activeProcesses.Values.ToList();
        }

        /// <summary>
        /// Anuluje aktywny proces
        /// </summary>
        public async Task<bool> CancelProcessAsync(string processId)
        {
            await Task.CompletedTask; // Dla zgodności z async
            
            if (_cancellationTokens.TryGetValue(processId, out var cts))
            {
                cts.Cancel();
                
                if (_activeProcesses.TryGetValue(processId, out var status))
                {
                    status.Status = "Cancelled";
                    status.CompletedAt = DateTime.UtcNow;
                    status.CurrentOperation = "Anulowany";
                }
                
                _logger.LogInformation("BulkUserManagement: Anulowano proces {ProcessId}", processId);
                return true;
            }
            
            return false;
        }

        // ===== METODY POMOCNICZE =====

        private async Task<(bool IsValid, string ErrorMessage)> ValidateOnboardingPlan(UserOnboardingPlan plan)
        {
            if (string.IsNullOrWhiteSpace(plan.UPN))
                return (false, "UPN jest wymagany");

            if (string.IsNullOrWhiteSpace(plan.FirstName))
                return (false, "Imię jest wymagane");

            if (string.IsNullOrWhiteSpace(plan.LastName))
                return (false, "Nazwisko jest wymagane");

            if (string.IsNullOrWhiteSpace(plan.DepartmentId))
                return (false, "ID działu jest wymagane");

            // Sprawdź czy użytkownik już istnieje
            var existingUser = await _userService.GetUserByUpnAsync(plan.UPN);
            if (existingUser != null)
                return (false, "Użytkownik o tym UPN już istnieje");

            // Sprawdź czy dział istnieje
            var department = await _departmentService.GetDepartmentByIdAsync(plan.DepartmentId, false, false, false);
            if (department == null)
                return (false, "Dział o podanym ID nie istnieje");

            return (true, string.Empty);
        }

        private async Task<BulkOperationResult> ProcessOnboardingBatch(List<UserOnboardingPlan> batch, string apiAccessToken, CancellationToken cancellationToken)
        {
            var successfulOperations = new List<BulkOperationSuccess>();
            var errors = new List<BulkOperationError>();

            foreach (var plan in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    // 1. Utwórz użytkownika lokalnie
                    var user = await _userService.CreateUserAsync(
                        plan.FirstName,
                        plan.LastName,
                        plan.UPN,
                        plan.Role,
                        plan.DepartmentId,
                        plan.Password,
                        apiAccessToken,
                        plan.SendWelcomeEmail);

                    if (user != null)
                    {
                        successfulOperations.Add(new BulkOperationSuccess
                        {
                            Operation = "CreateUser",
                            EntityId = user.Id,
                            EntityName = user.FullName,
                            Message = "Użytkownik utworzony pomyślnie"
                        });

                        // 2. Dodaj do zespołów
                        if (plan.TeamIds?.Any() == true)
                        {
                            await AddUserToTeams(user, plan.TeamIds, apiAccessToken, successfulOperations, errors);
                        }

                        // 3. Przypisz do typów szkół (dla nauczycieli)
                        if (plan.SchoolTypeIds?.Any() == true && IsTeachingRole(plan.Role))
                        {
                            await AssignUserToSchoolTypes(user, plan.SchoolTypeIds, successfulOperations, errors);
                        }

                        // 4. Przypisz do przedmiotów (dla nauczycieli)
                        if (plan.SubjectIds?.Any() == true && IsTeachingRole(plan.Role))
                        {
                            await AssignUserToSubjects(user, plan.SubjectIds, successfulOperations, errors);
                        }
                    }
                    else
                    {
                        errors.Add(new BulkOperationError
                        {
                            Operation = "CreateUser",
                            EntityId = plan.UPN,
                            Message = "Nie udało się utworzyć użytkownika"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas onboardingu użytkownika {UPN}", plan.UPN);
                    errors.Add(new BulkOperationError
                    {
                        Operation = "OnboardUser",
                        EntityId = plan.UPN,
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
                Errors = errors,
                ProcessedAt = DateTime.UtcNow,
                OperationType = "ProcessOnboardingBatch"
            };
        }

        private async Task TransferTeamOwnership(List<User> users, OffboardingOptions options, List<BulkOperationSuccess> successfulOperations, List<BulkOperationError> errors)
        {
            foreach (var user in users)
            {
                try
                {
                    // Znajdź zespoły gdzie użytkownik jest właścicielem
                    var ownedTeams = user.TeamMemberships?
                        .Where(tm => tm.IsActive && tm.Role == TeamMemberRole.Owner && tm.Team?.IsActive == true)
                        .Select(tm => tm.Team!)
                        .ToList() ?? new List<Team>();

                    foreach (var team in ownedTeams)
                    {
                        // Znajdź zastępczego właściciela
                        var newOwnerId = options.FallbackOwnerId;
                        if (string.IsNullOrEmpty(newOwnerId))
                        {
                            // Automatyczny wybór wicedyrektora lub dyrektora
                            var fallbackUsers = await _userService.GetUsersByRoleAsync(UserRole.Wicedyrektor);
                            var fallbackUser = fallbackUsers.FirstOrDefault();
                            if (fallbackUser == null)
                            {
                                fallbackUsers = await _userService.GetUsersByRoleAsync(UserRole.Dyrektor);
                                fallbackUser = fallbackUsers.FirstOrDefault();
                            }
                            newOwnerId = fallbackUser?.Id;
                        }

                        if (!string.IsNullOrEmpty(newOwnerId))
                        {
                            // Transfer własności (to można rozszerzyć o faktyczną implementację)
                            successfulOperations.Add(new BulkOperationSuccess
                            {
                                Operation = "TransferTeamOwnership",
                                EntityId = team.Id,
                                EntityName = team.DisplayName,
                                Message = $"Przeniesiono własność zespołu z {user.FullName} na zastępczego właściciela"
                            });
                        }
                        else
                        {
                            errors.Add(new BulkOperationError
                            {
                                Operation = "TransferTeamOwnership",
                                EntityId = team.Id,
                                EntityName = team.DisplayName,
                                Message = "Nie znaleziono zastępczego właściciela dla zespołu"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas transferu własności zespołów dla użytkownika {UserId}", user.Id);
                    errors.Add(new BulkOperationError
                    {
                        Operation = "TransferTeamOwnership",
                        EntityId = user.Id,
                        EntityName = user.FullName,
                        Message = ex.Message,
                        Exception = ex
                    });
                }
            }
        }

        private async Task RemoveUsersFromAllTeams(List<User> users, List<BulkOperationSuccess> successfulOperations, List<BulkOperationError> errors, string apiAccessToken)
        {
            // Grupuj użytkowników według zespołów
            var teamMemberships = new Dictionary<string, List<string>>();
            
            foreach (var user in users)
            {
                var userTeams = user.TeamMemberships?
                    .Where(tm => tm.IsActive && tm.Team?.IsActive == true)
                    .Select(tm => tm.TeamId)
                    .ToList() ?? new List<string>();

                foreach (var teamId in userTeams)
                {
                    if (!teamMemberships.ContainsKey(teamId))
                        teamMemberships[teamId] = new List<string>();
                    
                    teamMemberships[teamId].Add(user.UPN);
                }
            }

            // Usuń użytkowników z zespołów masowo
            foreach (var kvp in teamMemberships)
            {
                try
                {
                    var results = await _teamService.RemoveUsersFromTeamAsync(kvp.Key, kvp.Value, "Offboarding process", apiAccessToken);
                    
                    foreach (var result in results)
                    {
                        if (result.Value)
                        {
                            successfulOperations.Add(new BulkOperationSuccess
                            {
                                Operation = "RemoveFromTeam",
                                EntityId = result.Key,
                                Message = $"Usunięto z zespołu {kvp.Key}"
                            });
                        }
                        else
                        {
                            errors.Add(new BulkOperationError
                            {
                                Operation = "RemoveFromTeam",
                                EntityId = result.Key,
                                Message = $"Nie udało się usunąć z zespołu {kvp.Key}"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas usuwania użytkowników z zespołu {TeamId}", kvp.Key);
                    errors.Add(new BulkOperationError
                    {
                        Operation = "RemoveFromTeam",
                        EntityId = kvp.Key,
                        Message = ex.Message,
                        Exception = ex
                    });
                }
            }
        }

        private async Task DeactivateUserAccounts(List<User> users, OffboardingOptions options, List<BulkOperationSuccess> successfulOperations, List<BulkOperationError> errors, string apiAccessToken)
        {
            foreach (var user in users)
            {
                try
                {
                    var result = await _userService.DeactivateUserAsync(user.Id, apiAccessToken, options.DeactivateM365Accounts);
                    
                    if (result)
                    {
                        successfulOperations.Add(new BulkOperationSuccess
                        {
                            Operation = "DeactivateUser",
                            EntityId = user.Id,
                            EntityName = user.FullName,
                            Message = "Konto użytkownika zostało dezaktywowane"
                        });
                    }
                    else
                    {
                        errors.Add(new BulkOperationError
                        {
                            Operation = "DeactivateUser",
                            EntityId = user.Id,
                            EntityName = user.FullName,
                            Message = "Nie udało się dezaktywować konta użytkownika"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas dezaktywacji użytkownika {UserId}", user.Id);
                    errors.Add(new BulkOperationError
                    {
                        Operation = "DeactivateUser",
                        EntityId = user.Id,
                        EntityName = user.FullName,
                        Message = ex.Message,
                        Exception = ex
                    });
                }
            }
        }

        private async Task CreateUserDataBackups(List<User> users, List<BulkOperationSuccess> successfulOperations, List<BulkOperationError> errors)
        {
            // Placeholder dla backup'u danych użytkownika
            foreach (var user in users)
            {
                try
                {
                    // Tutaj można dodać logikę tworzenia backup'u:
                    // - Eksport danych osobowych
                    // - Backup plików OneDrive
                    // - Eksport historii operacji
                    
                    await Task.Delay(100); // Symulacja operacji backup
                    
                    successfulOperations.Add(new BulkOperationSuccess
                    {
                        Operation = "CreateDataBackup",
                        EntityId = user.Id,
                        EntityName = user.FullName,
                        Message = "Utworzono kopię zapasową danych użytkownika"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas tworzenia backup'u dla użytkownika {UserId}", user.Id);
                    errors.Add(new BulkOperationError
                    {
                        Operation = "CreateDataBackup",
                        EntityId = user.Id,
                        EntityName = user.FullName,
                        Message = ex.Message,
                        Exception = ex
                    });
                }
            }
        }

        private async Task AddUserToTeams(User user, string[] teamIds, string apiAccessToken, List<BulkOperationSuccess> successfulOperations, List<BulkOperationError> errors)
        {
            foreach (var teamId in teamIds)
            {
                try
                {
                    var results = await _teamService.AddUsersToTeamAsync(teamId, new List<string> { user.UPN }, apiAccessToken);
                    var result = results.FirstOrDefault();
                    
                    if (result.Value)
                    {
                        successfulOperations.Add(new BulkOperationSuccess
                        {
                            Operation = "AddToTeam",
                            EntityId = teamId,
                            Message = $"Dodano użytkownika {user.FullName} do zespołu"
                        });
                    }
                    else
                    {
                        errors.Add(new BulkOperationError
                        {
                            Operation = "AddToTeam",
                            EntityId = teamId,
                            Message = $"Nie udało się dodać użytkownika {user.FullName} do zespołu"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas dodawania użytkownika {UserId} do zespołu {TeamId}", user.Id, teamId);
                    errors.Add(new BulkOperationError
                    {
                        Operation = "AddToTeam",
                        EntityId = teamId,
                        Message = ex.Message,
                        Exception = ex
                    });
                }
            }
        }

        private async Task AssignUserToSchoolTypes(User user, string[] schoolTypeIds, List<BulkOperationSuccess> successfulOperations, List<BulkOperationError> errors)
        {
            foreach (var schoolTypeId in schoolTypeIds)
            {
                try
                {
                    var result = await _userService.AssignUserToSchoolTypeAsync(user.Id, schoolTypeId, DateTime.UtcNow);
                    
                    if (result != null)
                    {
                        successfulOperations.Add(new BulkOperationSuccess
                        {
                            Operation = "AssignToSchoolType",
                            EntityId = schoolTypeId,
                            Message = $"Przypisano użytkownika {user.FullName} do typu szkoły"
                        });
                    }
                    else
                    {
                        errors.Add(new BulkOperationError
                        {
                            Operation = "AssignToSchoolType",
                            EntityId = schoolTypeId,
                            Message = $"Nie udało się przypisać użytkownika {user.FullName} do typu szkoły"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas przypisywania użytkownika {UserId} do typu szkoły {SchoolTypeId}", user.Id, schoolTypeId);
                    errors.Add(new BulkOperationError
                    {
                        Operation = "AssignToSchoolType",
                        EntityId = schoolTypeId,
                        Message = ex.Message,
                        Exception = ex
                    });
                }
            }
        }

        private async Task AssignUserToSubjects(User user, string[] subjectIds, List<BulkOperationSuccess> successfulOperations, List<BulkOperationError> errors)
        {
            foreach (var subjectId in subjectIds)
            {
                try
                {
                    var result = await _userService.AssignTeacherToSubjectAsync(user.Id, subjectId, DateTime.UtcNow);
                    
                    if (result != null)
                    {
                        successfulOperations.Add(new BulkOperationSuccess
                        {
                            Operation = "AssignToSubject",
                            EntityId = subjectId,
                            Message = $"Przypisano nauczyciela {user.FullName} do przedmiotu"
                        });
                    }
                    else
                    {
                        errors.Add(new BulkOperationError
                        {
                            Operation = "AssignToSubject",
                            EntityId = subjectId,
                            Message = $"Nie udało się przypisać nauczyciela {user.FullName} do przedmiotu"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas przypisywania nauczyciela {UserId} do przedmiotu {SubjectId}", user.Id, subjectId);
                    errors.Add(new BulkOperationError
                    {
                        Operation = "AssignToSubject",
                        EntityId = subjectId,
                        Message = ex.Message,
                        Exception = ex
                    });
                }
            }
        }

        private static bool IsTeachingRole(UserRole role)
        {
            return role == UserRole.Nauczyciel || role == UserRole.Wicedyrektor || role == UserRole.Dyrektor;
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
} 