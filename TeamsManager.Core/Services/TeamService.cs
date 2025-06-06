// Plik: TeamsManager.Core/Services/TeamService.cs
using System;
using System.Collections.Generic;
using System.Linq;

using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Cache;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Abstractions.Services.Synchronization;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Exceptions.PowerShell;

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Serwis odpowiedzialny za logikę biznesową zespołów.
    /// Implementuje cache'owanie dla często odpytywanych danych.
    /// </summary>
    public class TeamService : ITeamService
    {
        private readonly ITeamRepository _teamRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGenericRepository<TeamMember> _teamMemberRepository;
        private readonly ITeamTemplateRepository _teamTemplateRepository;
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPowerShellTeamManagementService _powerShellTeamService;
        private readonly IPowerShellUserManagementService _powerShellUserService;
        private readonly IPowerShellBulkOperationsService _powerShellBulkOps;
        private readonly IPowerShellService _powerShellService;
        private readonly INotificationService _notificationService;
        private readonly IAdminNotificationService _adminNotificationService;
        private readonly ILogger<TeamService> _logger;
        private readonly IGenericRepository<SchoolType> _schoolTypeRepository;
        private readonly ISchoolYearRepository _schoolYearRepository;

        private readonly IOperationHistoryService _operationHistoryService; // Dodaj to do konstruktora
        private readonly IPowerShellCacheService _powerShellCacheService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        
        // NOWA zależność - Unit of Work (opcjonalna dla zachowania kompatybilności)
        private readonly IUnitOfWork? _unitOfWork;
        
        // NOWA zależność - Team Synchronizer (Etap 4/8)
        private readonly IGraphSynchronizer<Team> _teamSynchronizer;

        // Klucze cache (delegowane do PowerShellCacheService)
        private const string AllActiveTeamsCacheKey = "Teams_AllActive";
        private const string ActiveTeamsSpecificCacheKey = "Teams_Active";
        private const string ArchivedTeamsCacheKey = "Teams_Archived";
        private const string TeamsByOwnerCacheKeyPrefix = "Teams_ByOwner_";
        private const string TeamByIdCacheKeyPrefix = "Team_Id_";

        /// <summary>
        /// Konstruktor serwisu zespołów.
        /// </summary>
        public TeamService(
            ITeamRepository teamRepository,
            IUserRepository userRepository,
            IGenericRepository<TeamMember> teamMemberRepository,
            ITeamTemplateRepository teamTemplateRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            IPowerShellTeamManagementService powerShellTeamService,
            IPowerShellUserManagementService powerShellUserService,
            IPowerShellBulkOperationsService powerShellBulkOps,
            IPowerShellService powerShellService,
            INotificationService notificationService,
            IAdminNotificationService adminNotificationService,
            ILogger<TeamService> logger,
            IGenericRepository<SchoolType> schoolTypeRepository,
            ISchoolYearRepository schoolYearRepository,

            IOperationHistoryService operationHistoryService, // Dodaj to do konstruktora
            IPowerShellCacheService powerShellCacheService,
            ICacheInvalidationService cacheInvalidationService, // NOWE: Cache Invalidation Service (Etap 7/8)
            IUnitOfWork? unitOfWork = null, // NOWY parametr - opcjonalny dla kompatybilności
            IGraphSynchronizer<Team>? teamSynchronizer = null) // NOWY parametr - Team Synchronizer (Etap 4/8)
        {
            _teamRepository = teamRepository ?? throw new ArgumentNullException(nameof(teamRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _teamMemberRepository = teamMemberRepository ?? throw new ArgumentNullException(nameof(teamMemberRepository));
            _teamTemplateRepository = teamTemplateRepository ?? throw new ArgumentNullException(nameof(teamTemplateRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository)); // Zachowaj to dla specjalnych operacji
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _powerShellTeamService = powerShellTeamService ?? throw new ArgumentNullException(nameof(powerShellTeamService));
            _powerShellUserService = powerShellUserService ?? throw new ArgumentNullException(nameof(powerShellUserService));
            _powerShellBulkOps = powerShellBulkOps ?? throw new ArgumentNullException(nameof(powerShellBulkOps));
            _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _adminNotificationService = adminNotificationService ?? throw new ArgumentNullException(nameof(adminNotificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _schoolTypeRepository = schoolTypeRepository ?? throw new ArgumentNullException(nameof(schoolTypeRepository));
            _schoolYearRepository = schoolYearRepository ?? throw new ArgumentNullException(nameof(schoolYearRepository));

            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService)); // Zainicjalizuj to
            _powerShellCacheService = powerShellCacheService ?? throw new ArgumentNullException(nameof(powerShellCacheService));
            _cacheInvalidationService = cacheInvalidationService ?? throw new ArgumentNullException(nameof(cacheInvalidationService)); // NOWE: Cache Invalidation Service (Etap 7/8)
            _unitOfWork = unitOfWork; // NOWE: opcjonalne przypisanie Unit of Work
            _teamSynchronizer = teamSynchronizer ?? throw new ArgumentNullException(nameof(teamSynchronizer)); // NOWE: Team Synchronizer (Etap 4/8)
        }



        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache. Zwraca zespół tylko jeśli jego Status to Active.</remarks>
        public async Task<Team?> GetTeamByIdAsync(string teamId, bool includeMembers = false, bool includeChannels = false, bool forceRefresh = false, string? apiAccessToken = null) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Pobieranie zespołu o ID: {TeamId}. Synchronizacja: {HasToken}. Dołączanie członków: {IncludeMembers}, Dołączanie kanałów: {IncludeChannels}, Wymuszenie odświeżenia: {ForceRefresh}", teamId, !string.IsNullOrEmpty(apiAccessToken), includeMembers, includeChannels, forceRefresh);

            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogWarning("Próba pobrania zespołu z pustym ID.");
                return null;
            }

            string cacheKey = TeamByIdCacheKeyPrefix + teamId;
            Team? team;

            if (!forceRefresh && _powerShellCacheService.TryGetValue(cacheKey, out team) && team != null)
            {
                _logger.LogDebug("Zespół ID: {TeamId} znaleziony w cache.", teamId);
                if (team.Status != TeamStatus.Active)
                {
                    _logger.LogDebug("Zespół ID: {TeamId} znaleziony w cache, ale jego Status to {TeamStatus}. Zwracanie null.", teamId, team.Status);
                    return null;
                }
            }
            else
            {
                _logger.LogDebug("Zespół ID: {TeamId} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", teamId);

                // Najpierw pobierz z lokalnej bazy
                team = await _teamRepository.GetByIdAsync(teamId);

                // NOWA SEKCJA: Synchronizacja z Graph jeśli podano token
                if (!string.IsNullOrEmpty(apiAccessToken))
                {
                    try
                    {
                        // Pobierz dane z Graph
                        var graphTeam = await _powerShellService.ExecuteWithAutoConnectAsync(
                            apiAccessToken,
                            async () => await _powerShellTeamService.GetTeamAsync(teamId),
                            $"GetTeamAsync dla synchronizacji ID: {teamId}"
                        );
                        
                        if (graphTeam != null)
                        {
                            // Synchronizuj dane
                            team = await SynchronizeTeamWithGraphAsync(team, graphTeam, teamId);
                        }
                        else if (team != null)
                        {
                            // Zespół nie istnieje w Graph ale istnieje lokalnie
                            _logger.LogWarning("Zespół {TeamId} nie istnieje w Graph. Oznaczanie jako usunięty.", teamId);
                            await HandleDeletedTeamAsync(team);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Błąd podczas synchronizacji zespołu {TeamId} z Graph", teamId);
                        // W przypadku błędu, zwróć dane lokalne jeśli istnieją
                    }
                }

                if (team != null && team.IsActive)
                {
                    _powerShellCacheService.Set(cacheKey, team);
                    _logger.LogDebug("Zespół ID: {TeamId} dodany do cache.", teamId);
                }
                else
                {
                    _powerShellCacheService.Remove(cacheKey);
                    if (team != null && !team.IsActive)
                    {
                        _logger.LogDebug("Zespół ID: {TeamId} jest nieaktywny (Status != Active), nie zostanie zcache'owany.", teamId);
                        return null;
                    }
                }
            }
            return team;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Team>> GetAllTeamsAsync(bool forceRefresh = false, string? apiAccessToken = null) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Pobieranie wszystkich zespołów z Team.Status = Active. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);
            string cacheKey = AllActiveTeamsCacheKey;

            if (!forceRefresh && _powerShellCacheService.TryGetValue(cacheKey, out IEnumerable<Team>? cachedTeams) && cachedTeams != null)
            {
                _logger.LogDebug("Zespoły z Team.Status = Active znalezione w cache.");
                return cachedTeams;
            }
            _logger.LogDebug("Zespoły z Team.Status = Active nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");

            if (!string.IsNullOrEmpty(apiAccessToken))
            {
                // Można dodać logikę synchronizacji wszystkich zespołów z Graph w przyszłości
                // Obecnie brak potrzeby wywołania Graph API dla wszystkich zespołów
            }

            var teamsFromDb = await _teamRepository.FindAsync(t => t.Status == TeamStatus.Active);
            _powerShellCacheService.Set(cacheKey, teamsFromDb);
            _logger.LogDebug("Zespoły z Team.Status = Active dodane do cache.");
            return teamsFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Team>> GetActiveTeamsAsync(bool forceRefresh = false, string? apiAccessToken = null) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Pobieranie zespołów o statusie 'Active'. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);
            string cacheKey = ActiveTeamsSpecificCacheKey;

            if (!forceRefresh && _powerShellCacheService.TryGetValue(cacheKey, out IEnumerable<Team>? cachedTeams) && cachedTeams != null)
            {
                _logger.LogDebug("Zespoły o statusie 'Active' znalezione w cache. Liczba zespołów: {Count}", cachedTeams.Count());
                return cachedTeams;
            }
            _logger.LogDebug("Zespoły o statusie 'Active' nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium. Cache key: {CacheKey}", cacheKey);

            if (!string.IsNullOrEmpty(apiAccessToken))
            {
                // Można dodać logikę synchronizacji aktywnych zespołów z Graph w przyszłości
                // Obecnie brak potrzeby wywołania Graph API dla bulk operacji
            }

            var teamsFromDb = await _teamRepository.GetActiveTeamsAsync();
            _powerShellCacheService.Set(cacheKey, teamsFromDb);
            _logger.LogDebug("Zespoły o statusie 'Active' dodane do cache. Liczba zespołów: {Count}", teamsFromDb.Count());
            return teamsFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Team>> GetArchivedTeamsAsync(bool forceRefresh = false, string? apiAccessToken = null) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Pobieranie zespołów o statusie 'Archived'. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);
            string cacheKey = ArchivedTeamsCacheKey;

            if (!forceRefresh && _powerShellCacheService.TryGetValue(cacheKey, out IEnumerable<Team>? cachedTeams) && cachedTeams != null)
            {
                _logger.LogDebug("Zespoły o statusie 'Archived' znalezione w cache. Liczba zespołów: {Count}", cachedTeams.Count());
                return cachedTeams;
            }
            _logger.LogDebug("Zespoły o statusie 'Archived' nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium. Cache key: {CacheKey}", cacheKey);

            if (!string.IsNullOrEmpty(apiAccessToken))
            {
                // Można dodać logikę synchronizacji zarchiwizowanych zespołów z Graph w przyszłości
                // Obecnie brak potrzeby wywołania Graph API dla bulk operacji
            }
            var teamsFromDb = await _teamRepository.GetArchivedTeamsAsync();
            _powerShellCacheService.Set(cacheKey, teamsFromDb);
            _logger.LogDebug("Zespoły o statusie 'Archived' dodane do cache. Liczba zespołów: {Count}", teamsFromDb.Count());
            return teamsFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Team>> GetTeamsByOwnerAsync(string ownerUpn, bool forceRefresh = false, string? apiAccessToken = null) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Pobieranie zespołów dla właściciela: {OwnerUpn} (tylko te z Team.Status=Active). Wymuszenie odświeżenia: {ForceRefresh}", ownerUpn, forceRefresh);
            if (string.IsNullOrWhiteSpace(ownerUpn))
            {
                _logger.LogWarning("Próba pobrania zespołów dla pustego UPN właściciela.");
                return Enumerable.Empty<Team>();
            }
            string cacheKey = TeamsByOwnerCacheKeyPrefix + ownerUpn;

            if (!forceRefresh && _powerShellCacheService.TryGetValue(cacheKey, out IEnumerable<Team>? cachedTeams) && cachedTeams != null)
            {
                _logger.LogDebug("Zespoły (status Active) dla właściciela {OwnerUpn} znalezione w cache.", ownerUpn);
                return cachedTeams;
            }
            _logger.LogDebug("Zespoły (status Active) dla właściciela {OwnerUpn} nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", ownerUpn);

            if (!string.IsNullOrEmpty(apiAccessToken))
            {
                // Można dodać logikę synchronizacji zespołów właściciela z Graph w przyszłości
                // Obecnie brak potrzeby wywołania Graph API dla bulk operacji
            }
            var teamsFromDb = await _teamRepository.GetTeamsByOwnerAsync(ownerUpn);
            _powerShellCacheService.Set(cacheKey, teamsFromDb);
            _logger.LogDebug("Zespoły (status Active) dla właściciela {OwnerUpn} dodane do cache.", ownerUpn);
            return teamsFromDb;
        }

        /// <inheritdoc />
        public async Task<Team?> CreateTeamAsync(
            string displayName,
            string description,
            string ownerUpn,
            TeamVisibility visibility,
            string apiAccessToken, // ZMIANA: accessToken -> apiAccessToken
            string? teamTemplateId = null,
            string? schoolTypeId = null,
            string? schoolYearId = null,
            Dictionary<string, string>? additionalTemplateValues = null)
        {
            _logger.LogInformation("Rozpoczynanie tworzenia zespołu: '{DisplayName}'", displayName);

            // Używamy Unit of Work dla transakcyjności (jeśli dostępny)
            if (_unitOfWork != null)
            {
                await _unitOfWork.BeginTransactionAsync();
            }
            
            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.TeamCreated,
                nameof(Team),
                targetEntityName: displayName
            );

            Team? newTeam = null;

            try
            {
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    _logger.LogError("Nie można utworzyć zespołu: Nazwa wyświetlana jest pusta.");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nazwa wyświetlana zespołu nie może być pusta."
                    );
                    return null;
                }
                var ownerUser = await _userRepository.GetUserByUpnAsync(ownerUpn);
                if (ownerUser == null || !ownerUser.IsActive)
                {
                    _logger.LogError("Nie można utworzyć zespołu: Właściciel '{OwnerUPN}' nie istnieje lub jest nieaktywny.", ownerUpn);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Użytkownik właściciela '{ownerUpn}' nie istnieje lub jest nieaktywny."
                    );
                    return null;
                }

                string finalDisplayName = displayName;
                TeamTemplate? template = null;
                SchoolType? schoolType = null;
                SchoolYear? schoolYear = null;

                if (!string.IsNullOrEmpty(schoolTypeId))
                    schoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeId);
                if (!string.IsNullOrEmpty(schoolYearId))
                    schoolYear = await _schoolYearRepository.GetByIdAsync(schoolYearId);

                if (!string.IsNullOrEmpty(teamTemplateId))
                {
                    template = await _teamTemplateRepository.GetByIdAsync(teamTemplateId);
                    if (template != null && template.IsActive)
                    {
                        var valuesForTemplate = additionalTemplateValues ?? new Dictionary<string, string>();
                        if (!valuesForTemplate.ContainsKey("Nauczyciel")) valuesForTemplate["Nauczyciel"] = ownerUser.FullName;
                        if (schoolType != null && !valuesForTemplate.ContainsKey("TypSzkoly") && template.Placeholders.Contains("TypSzkoly")) valuesForTemplate["TypSzkoly"] = schoolType.ShortName;
                        if (schoolYear != null && !valuesForTemplate.ContainsKey("RokSzkolny") && template.Placeholders.Contains("RokSzkolny")) valuesForTemplate["RokSzkolny"] = schoolYear.Name;

                        finalDisplayName = template.GenerateTeamName(valuesForTemplate);
                        _teamTemplateRepository.Update(template);
                        _logger.LogInformation("Nazwa zespołu wygenerowana z szablonu '{TemplateName}': {FinalDisplayName}", template.Name, finalDisplayName);
                    }
                    else { _logger.LogWarning("Szablon o ID {TemplateId} nie istnieje lub jest nieaktywny. Użycie oryginalnej nazwy: {OriginalName}", teamTemplateId, displayName); }
                }

                // Refaktoryzowane wywołanie z ExecuteWithAutoConnectAsync
                string? externalTeamIdFromPS = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _powerShellTeamService.CreateTeamAsync(
                        finalDisplayName, description, ownerUser.UPN, visibility, template?.Template
                    ),
                    $"CreateTeamAsync dla zespołu '{finalDisplayName}'"
                );
                
                bool psSuccess = !string.IsNullOrEmpty(externalTeamIdFromPS);

                if (psSuccess)
                {
                    _logger.LogInformation("Zespół '{FinalDisplayName}' pomyślnie utworzony w Microsoft Teams. External ID: {ExternalTeamId}", finalDisplayName, externalTeamIdFromPS);
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation";
                    newTeam = new Team { /* ... inicjalizacja pól ... */ Id = Guid.NewGuid().ToString(), DisplayName = finalDisplayName, Description = description, Owner = ownerUser.UPN, Status = TeamStatus.Active, Visibility = visibility, TemplateId = template?.Id, SchoolTypeId = schoolTypeId, SchoolYearId = schoolYearId, ExternalId = externalTeamIdFromPS, SchoolType = schoolType, SchoolYear = schoolYear, Template = template };
                    if (schoolYear != null) newTeam.AcademicYear = schoolYear.Name;
                    var ownerMembership = new TeamMember { Id = Guid.NewGuid().ToString(), UserId = ownerUser.Id, TeamId = newTeam.Id, Role = ownerUser.DefaultTeamRole, AddedDate = DateTime.UtcNow, AddedBy = currentUserUpn, IsActive = true, IsApproved = !newTeam.RequiresApproval, ApprovedDate = !newTeam.RequiresApproval ? DateTime.UtcNow : null, ApprovedBy = !newTeam.RequiresApproval ? currentUserUpn : null, User = ownerUser, Team = newTeam };
                    newTeam.Members.Add(ownerMembership);
                    
                    // 3. Synchronizacja lokalnej bazy - użyj Unit of Work jeśli dostępny
                    if (_unitOfWork != null)
                    {
                        await _unitOfWork.Teams.AddAsync(newTeam);
                        
                        // Zatwierdzamy wszystkie zmiany transakcyjnie
                        await _unitOfWork.CommitAsync();
                        
                        // Zatwierdzamy transakcję
                        await _unitOfWork.CommitTransactionAsync();
                    }
                    else
                    {
                        await _teamRepository.AddAsync(newTeam);
                        // W starym podejściu SaveChangesAsync musi być wywołane wyżej (kontroler)
                    }
                    
                    _logger.LogInformation("Zespół '{FinalDisplayName}' pomyślnie utworzony i zapisany lokalnie. ID: {TeamId}", finalDisplayName, newTeam.Id);
                    
                    // 4. Powiadomienia
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Zespół '{finalDisplayName}' został utworzony pomyślnie.",
                        "success"
                    );
                    
                    // Powiadom właściciela jeśli to nie current user
                    if (ownerUser.UPN != currentUserUpn)
                    {
                        await _notificationService.SendNotificationToUserAsync(
                            ownerUser.UPN,
                            $"Został utworzony nowy zespół '{finalDisplayName}', którego jesteś właścicielem.",
                            "info"
                        );
                    }
                    
                    // Powiadomienie do administratorów (asynchroniczne, nie blokuje operacji)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _adminNotificationService.SendTeamCreatedNotificationAsync(
                                newTeam.DisplayName,
                                newTeam.Id,
                                currentUserUpn,
                                newTeam.Members?.Count ?? 0,
                                new Dictionary<string, object>
                                {
                                    ["Opis"] = newTeam.Description ?? "Brak",
                                    ["Widoczność"] = newTeam.Visibility.ToString(),
                                    ["Właściciele"] = newTeam.Members?.Count(m => m.Role == TeamMemberRole.Owner) ?? 0,
                                    ["Szablon"] = template?.Name ?? "Brak",
                                    ["Typ szkoły"] = schoolType?.FullName ?? "Brak",
                                    ["Rok szkolny"] = schoolYear?.Name ?? "Brak"
                                }
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Błąd podczas wysyłania powiadomienia administratorskiego o utworzeniu zespołu");
                        }
                    });
                    
                    // 5. Invalidacja cache i finalizacja audytu
                    await _cacheInvalidationService.InvalidateForTeamCreatedAsync(newTeam);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Zespół ID: {newTeam.Id}, External ID: {externalTeamIdFromPS}"
                    );
                    return newTeam;
                }
                else
                {
                    _logger.LogError("Błąd tworzenia zespołu '{FinalDisplayName}' w Microsoft Teams.", finalDisplayName);
                    
                    // Powiadomienie o błędzie
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Nie udało się utworzyć zespołu '{finalDisplayName}'.",
                        "error"
                    );
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się utworzyć zespołu w Microsoft Teams."
                    );
                    return null;
                }
            }
            catch (PowerShellConnectionException ex)
            {
                _logger.LogError(ex, "Nie można utworzyć zespołu: Błąd połączenia z Microsoft Graph API.");
                
                // Rollback transakcji w przypadku błędu
                if (_unitOfWork != null)
                {
                    await _unitOfWork.RollbackAsync();
                }
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    "Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie CreateTeamAsync."
                );
                
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    "Nie udało się utworzyć zespołu: Błąd połączenia z Microsoft Graph API.",
                    "error"
                );
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia zespołu {DisplayName}.", displayName);
                
                // Rollback transakcji w przypadku błędu
                if (_unitOfWork != null)
                {
                    await _unitOfWork.RollbackAsync();
                }
                
                // 6. Obsługa błędów krytycznych
                var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Wystąpił błąd podczas tworzenia zespołu: {ex.Message}",
                    "error"
                );
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                return null;
            }
        }

        public async Task<bool> UpdateTeamAsync(Team teamToUpdate, string apiAccessToken) // ZMIANA: accessToken -> apiAccessToken
        {
            if (teamToUpdate == null || string.IsNullOrWhiteSpace(teamToUpdate.Id))
            {
                _logger.LogError("Próba aktualizacji zespołu z nieprawidłowymi danymi (null lub brak ID).");
                throw new ArgumentNullException(nameof(teamToUpdate));
            }

            _logger.LogInformation("Rozpoczynanie aktualizacji zespołu ID: {TeamId}", teamToUpdate.Id);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.TeamUpdated,
                nameof(Team),
                targetEntityId: teamToUpdate.Id
            );

            Team? existingTeam = null;
            string? oldOwnerUpn = null;
            TeamStatus? oldStatus = null;

            try
            {
                var teams = await _teamRepository.FindAsync(t => t.Id == teamToUpdate.Id);
                existingTeam = teams.FirstOrDefault();
                if (existingTeam == null)
                {
                    _logger.LogWarning("Nie można zaktualizować zespołu ID {TeamId} - nie istnieje.", teamToUpdate.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Zespół o ID '{teamToUpdate.Id}' nie istnieje."
                    );
                    return false;
                }
                oldOwnerUpn = existingTeam.Owner;
                oldStatus = existingTeam.Status;

                if (existingTeam.Owner != teamToUpdate.Owner)
                {
                    var newOwnerUser = await _userRepository.GetUserByUpnAsync(teamToUpdate.Owner);
                    if (newOwnerUser == null || !newOwnerUser.IsActive)
                    {
                        _logger.LogError("Nie można zaktualizować zespołu: Nowy właściciel '{NewOwnerUPN}' nie istnieje lub jest nieaktywny.", teamToUpdate.Owner);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            $"Nowy właściciel '{teamToUpdate.Owner}' nie istnieje lub jest nieaktywny."
                        );
                        return false;
                    }
                }
                if (existingTeam.Status != teamToUpdate.Status)
                {
                    _logger.LogWarning("Próba zmiany statusu zespołu ID {TeamId} z {OldStatus} na {NewStatus} za pomocą metody UpdateTeamAsync jest ignorowana. Użyj ArchiveTeamAsync/RestoreTeamAsync do zmiany statusu.", existingTeam.Id, existingTeam.Status, teamToUpdate.Status);
                }

                bool psSuccess = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _powerShellTeamService.UpdateTeamPropertiesAsync(
                        existingTeam.ExternalId ?? teamToUpdate.Id,
                        teamToUpdate.DisplayName,
                        teamToUpdate.Description,
                        teamToUpdate.Visibility
                    ),
                    $"UpdateTeamPropertiesAsync dla ID: {existingTeam.Id}"
                );
                if (psSuccess)
                {
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
                    
                    // NOWE: Czyszczenie prefiksów z danych wejściowych
                    string cleanDisplayName = teamToUpdate.DisplayName;
                    string cleanDescription = teamToUpdate.Description;
                    
                    // Usuń prefiks jeśli występuje w danych wejściowych
                    const string ArchivePrefix = "ARCHIWALNY - ";
                    if (cleanDisplayName?.StartsWith(ArchivePrefix) == true)
                    {
                        cleanDisplayName = cleanDisplayName.Substring(ArchivePrefix.Length);
                        _logger.LogWarning("Usunięto niepożądany prefiks z nazwy zespołu ID {TeamId}. Oryginalna nazwa: '{Original}', Oczyszczona: '{Clean}'", 
                            existingTeam.Id, teamToUpdate.DisplayName, cleanDisplayName);
                    }
                    
                    if (cleanDescription?.StartsWith(ArchivePrefix) == true)
                    {
                        cleanDescription = cleanDescription.Substring(ArchivePrefix.Length);
                        _logger.LogWarning("Usunięto niepożądany prefiks z opisu zespołu ID {TeamId}", existingTeam.Id);
                    }
                    
                    // Przypisz oczyszczone wartości
                    existingTeam.DisplayName = cleanDisplayName;
                    existingTeam.Description = cleanDescription;
                    existingTeam.Owner = teamToUpdate.Owner;
                    existingTeam.Visibility = teamToUpdate.Visibility;
                    existingTeam.RequiresApproval = teamToUpdate.RequiresApproval;
                    existingTeam.MaxMembers = teamToUpdate.MaxMembers;
                    existingTeam.SchoolTypeId = teamToUpdate.SchoolTypeId;
                    existingTeam.SchoolYearId = teamToUpdate.SchoolYearId;
                    existingTeam.TemplateId = teamToUpdate.TemplateId;
                    existingTeam.AcademicYear = teamToUpdate.AcademicYear;
                    existingTeam.Semester = teamToUpdate.Semester;

                    existingTeam.MarkAsModified(currentUserUpn);
                    _teamRepository.Update(existingTeam);
                    _logger.LogInformation("Zespół ID: {TeamId} pomyślnie zaktualizowany.", existingTeam.Id);
                    await _cacheInvalidationService.InvalidateForTeamUpdatedAsync(existingTeam);
                    
                    // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Zespół ID: {existingTeam.Id} zaktualizowany."
                    );
                    return true;
                }
                else
                {
                    _logger.LogError("Błąd aktualizacji zespołu ID {TeamId} w Microsoft Teams.", existingTeam.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Błąd aktualizacji zespołu w Microsoft Teams."
                    );
                    return false;
                }
            }
            catch (PowerShellConnectionException ex)
            {
                _logger.LogError(ex, "Nie można zaktualizować zespołu: Błąd połączenia z Microsoft Graph API.");
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    "Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie UpdateTeamAsync."
                );
                
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    "Nie udało się zaktualizować zespołu: Błąd połączenia z Microsoft Graph API.",
                    "error"
                );
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji zespołu ID {TeamId}.", teamToUpdate.Id);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                return false;
            }
        }

        public async Task<bool> ArchiveTeamAsync(string teamId, string reason, string apiAccessToken) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Rozpoczynanie archiwizacji zespołu ID: {TeamId}", teamId);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.TeamArchived,
                nameof(Team),
                targetEntityId: teamId
            );

            Team? team = null;
            try
            {
                var teams = await _teamRepository.FindAsync(t => t.Id == teamId);
                team = teams.FirstOrDefault();
                if (team == null) 
                { 
                    _logger.LogWarning("Nie można zarchiwizować zespołu ID {TeamId} - nie istnieje.", teamId); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Zespół o ID '{teamId}' nie istnieje."
                    );
                    return false; 
                }
                
                if (team.Status == TeamStatus.Archived) 
                { 
                    _logger.LogInformation("Zespół ID {TeamId} był już zarchiwizowany.", teamId); 
                    await _cacheInvalidationService.InvalidateForTeamArchivedAsync(team); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Zespół '{team.DisplayName}' (ID: {team.Id}) był już zarchiwizowany."
                    );
                    return true; 
                }

                bool psSuccess = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _powerShellTeamService.ArchiveTeamAsync(team.ExternalId ?? team.Id),
                    $"ArchiveTeamAsync dla zespołu '{team.DisplayName}' (ID: {team.Id})"
                );
                if (psSuccess)
                {
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_archive";
                    var oldStatus = team.Status;
                    
                    // 3. Synchronizacja lokalnej bazy - użyj metody Archive z modelu
                    team.Archive(reason, currentUserUpn);
                    
                    // Opcjonalnie: dezaktywuj członkostwa
                    var activeMembers = await _teamMemberRepository.FindAsync(tm => tm.TeamId == teamId && tm.IsActive);
                    foreach (var member in activeMembers)
                    {
                        member.RemoveFromTeam("Zespół został zarchiwizowany", currentUserUpn);
                        _teamMemberRepository.Update(member);
                    }
                    
                    _teamRepository.Update(team);
                    var operationDetails = $"Zespół '{team.GetBaseDisplayName()}' zarchiwizowany jako '{team.DisplayName}'. Powód: {reason}";
                    _logger.LogInformation("Zespół ID {TeamId} pomyślnie zarchiwizowany.", teamId);
                    
                    // 4. Powiadomienia
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Zespół '{team.GetBaseDisplayName()}' został zarchiwizowany.",
                        "success"
                    );
                    
                    // Powiadom właściciela zespołu jeśli to nie current user
                    if (team.Owner != currentUserUpn)
                    {
                        await _notificationService.SendNotificationToUserAsync(
                            team.Owner,
                            $"Zespół '{team.GetBaseDisplayName()}' został zarchiwizowany przez {currentUserUpn}. Powód: {reason}",
                            "info"
                        );
                    }
                    
                    // 5. Invalidacja cache i finalizacja audytu
                    await _cacheInvalidationService.InvalidateForTeamArchivedAsync(team);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        operationDetails
                    );
                    return true;
                }
                else 
                { 
                    _logger.LogError("Błąd archiwizacji zespołu ID {TeamId} w Microsoft Teams.", teamId);
                    
                    // Powiadomienie o błędzie
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Nie udało się zarchiwizować zespołu '{team.DisplayName}'.",
                        "error"
                    );
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Błąd archiwizacji zespołu w Microsoft Teams."
                    );
                    return false; 
                }
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "Krytyczny błąd podczas archiwizacji zespołu ID {TeamId}.", teamId); 
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                return false; 
            }
        }

        public async Task<bool> RestoreTeamAsync(string teamId, string apiAccessToken) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Rozpoczynanie przywracania zespołu ID: {TeamId}", teamId);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.TeamUnarchived,
                nameof(Team),
                targetEntityId: teamId
            );

            Team? team = null;
            try
            {
                var teams = await _teamRepository.FindAsync(t => t.Id == teamId);
                team = teams.FirstOrDefault();
                if (team == null) 
                { 
                    _logger.LogWarning("Nie można przywrócić zespołu ID {TeamId} - nie istnieje.", teamId); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Zespół o ID '{teamId}' nie istnieje."
                    );
                    return false; 
                }
                
                if (team.Status == TeamStatus.Active) 
                { 
                    _logger.LogInformation("Zespół ID {TeamId} był już aktywny.", teamId); 
                    await _cacheInvalidationService.InvalidateForTeamRestoredAsync(team); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Zespół '{team.DisplayName}' (ID: {team.Id}) był już aktywny."
                    );
                    return true; 
                }

                bool psSuccess = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _powerShellTeamService.UnarchiveTeamAsync(team.ExternalId ?? team.Id),
                    $"UnarchiveTeamAsync dla zespołu '{team.DisplayName}' (ID: {team.Id})"
                );
                if (psSuccess)
                {
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_restore";
                    var oldStatus = team.Status;
                    team.Restore(currentUserUpn);
                    _teamRepository.Update(team);
                    _logger.LogInformation("Zespół ID {TeamId} pomyślnie przywrócony.", teamId);
                    await _cacheInvalidationService.InvalidateForTeamRestoredAsync(team);
                    
                    // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Zespół '{team.DisplayName}' (ID: {team.Id}) przywrócony."
                    );
                    return true;
                }
                else 
                { 
                    _logger.LogError("Błąd przywracania zespołu ID {TeamId} w Microsoft Teams.", teamId); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Błąd przywracania zespołu w Microsoft Teams."
                    );
                    return false; 
                }
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "Krytyczny błąd podczas przywracania zespołu ID {TeamId}.", teamId); 
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                return false; 
            }
        }

        public async Task<bool> DeleteTeamAsync(string teamId, string apiAccessToken) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Rozpoczynanie usuwania zespołu ID: {TeamId}", teamId);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.TeamDeleted,
                nameof(Team),
                targetEntityId: teamId
            );

            Team? team = null;
            try
            {
                var teams = await _teamRepository.FindAsync(t => t.Id == teamId);
                team = teams.FirstOrDefault();
                if (team == null) 
                { 
                    _logger.LogWarning("Nie można usunąć zespołu ID {TeamId} - nie istnieje.", teamId); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Zespół o ID '{teamId}' nie istnieje."
                    );
                    return false; 
                }

                bool psSuccess = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _powerShellTeamService.DeleteTeamAsync(team.ExternalId ?? team.Id),
                    $"DeleteTeamAsync dla zespołu '{team.DisplayName}' (ID: {team.Id})"
                );
                if (psSuccess)
                {
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete";
                    var oldStatus = team.Status;
                    // W przypadku rzeczywistego usunięcia z Graph, lokalnie oznaczamy jako zarchiwizowany/usunięty, aby zachować spójność i historię
                    team.Archive($"Usunięty przez {currentUserUpn} (operacja DeleteTeamAsync)", currentUserUpn);
                    _teamRepository.Update(team);
                    _logger.LogInformation("Zespół ID {TeamId} pomyślnie usunięty z Microsoft Teams (lokalnie zarchiwizowany).", teamId);
                    await _cacheInvalidationService.InvalidateForTeamDeletedAsync(team);
                    
                    // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Zespół '{team.DisplayName}' (ID: {teamId}) usunięty z Microsoft Teams (lokalnie zarchiwizowany)."
                    );
                    return true;
                }
                else 
                { 
                    _logger.LogError("Błąd usuwania zespołu ID {TeamId} w Microsoft Teams.", teamId); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Błąd usuwania zespołu w Microsoft Teams."
                    );
                    return false; 
                }
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania zespołu ID {TeamId}.", teamId); 
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                return false; 
            }
        }

        public async Task<TeamMember?> AddMemberAsync(string teamId, string userUpn, TeamMemberRole role, string apiAccessToken) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Dodawanie użytkownika {UserUPN} do zespołu ID {TeamId} z rolą {Role}", userUpn, teamId, role);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.MemberAdded,
                nameof(TeamMember),
                targetEntityName: $"Członek {userUpn} do zespołu {teamId}"
            );

            Team? team = null;
            User? user = null;
            try
            {
                team = await GetTeamByIdAsync(teamId, apiAccessToken: apiAccessToken); // Przekazanie apiAccessToken, aby GetTeamByIdAsync mogło potencjalnie użyć Graph
                if (team == null) 
                { 
                    _logger.LogWarning("Nie można dodać członka: Zespół o ID {TeamId} nie istnieje lub nie jest aktywny (status).", teamId); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Zespół o ID '{teamId}' nie istnieje lub nie jest aktywny (status)."
                    );
                    return null; 
                }
                
                user = await _userRepository.GetUserByUpnAsync(userUpn);
                if (user == null || !user.IsActive) 
                { 
                    _logger.LogWarning("Nie można dodać członka: Użytkownik o UPN {UserUPN} nie istnieje lub jest nieaktywny.", userUpn); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Użytkownik o UPN '{userUpn}' nie istnieje lub jest nieaktywny."
                    );
                    return null; 
                }
                
                if (team.HasMember(user.Id)) 
                { 
                    _logger.LogWarning("Nie można dodać członka: Użytkownik {UserUPN} jest już aktywnym członkiem zespołu {TeamDisplayName}.", userUpn, team.DisplayName); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Użytkownik '{userUpn}' jest już aktywnym członkiem zespołu '{team.DisplayName}'."
                    );
                    return team.GetMembership(user.Id); 
                }
                
                if (!team.CanAddMoreMembers()) 
                { 
                    _logger.LogWarning("Nie można dodać członka: Zespół {TeamDisplayName} osiągnął maksymalną liczbę członków.", team.DisplayName); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Zespół '{team.DisplayName}' osiągnął maksymalną liczbę członków."
                    );
                    return null; 
                }

                bool psSuccess = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _powerShellUserService.AddUserToTeamAsync(
                        team.ExternalId ?? team.Id, user.UPN, role.ToString()
                    ),
                    $"AddUserToTeamAsync dla użytkownika {user.UPN} do zespołu '{team.DisplayName}'"
                );
                if (psSuccess)
                {
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_add_member";
                    var newMember = new TeamMember { /* ... inicjalizacja pól ... */ Id = Guid.NewGuid().ToString(), UserId = user.Id, TeamId = team.Id, Role = role, AddedDate = DateTime.UtcNow, AddedBy = currentUserUpn, IsActive = true, IsApproved = !team.RequiresApproval, ApprovedDate = !team.RequiresApproval ? DateTime.UtcNow : null, ApprovedBy = !team.RequiresApproval ? currentUserUpn : null, User = user, Team = team };
                    await _teamMemberRepository.AddAsync(newMember);
                    _logger.LogInformation("Użytkownik {UserUPN} pomyślnie dodany do zespołu {TeamDisplayName}.", userUpn, team.DisplayName);
                    await _cacheInvalidationService.InvalidateForTeamMemberAddedAsync(teamId, user.Id);
                    
                    // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Użytkownik '{userUpn}' dodany do zespołu '{team.DisplayName}' jako {role}."
                    );
                    return newMember;
                }
                else 
                { 
                    _logger.LogError("Błąd dodawania użytkownika {UserUPN} do zespołu {TeamDisplayName} w Microsoft Teams.", userUpn, team.DisplayName); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Błąd dodawania członka do zespołu w Microsoft Teams."
                    );
                    return null; 
                }
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "Krytyczny błąd podczas dodawania członka {UserUPN} do zespołu {TeamId}.", userUpn, teamId); 
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                return null; 
            }
        }

        public async Task<bool> RemoveMemberAsync(string teamId, string userId, string apiAccessToken) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Usuwanie użytkownika ID {UserId} z zespołu ID {TeamId}", userId, teamId);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.MemberRemoved,
                nameof(TeamMember),
                targetEntityName: $"Członek {userId} z zespołu {teamId}"
            );

            Team? team = null;
            try
            {
                team = await GetTeamByIdAsync(teamId, apiAccessToken: apiAccessToken);
                if (team == null) 
                { 
                    _logger.LogWarning("Nie można usunąć członka: Zespół o ID {TeamId} nie istnieje lub nie jest aktywny (status).", teamId); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Zespół o ID '{teamId}' nie istnieje lub nie jest aktywny (status)."
                    );
                    return false; 
                }
                
                var memberToRemove = team.GetMembership(userId);
                if (memberToRemove == null) 
                { 
                    _logger.LogWarning("Nie można usunąć członka: Użytkownik ID {UserId} nie jest aktywnym członkiem zespołu {TeamDisplayName}.", userId, team.DisplayName); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Użytkownik o ID '{userId}' nie jest (aktywnym) członkiem zespołu '{team.DisplayName}'."
                    );
                    return false; 
                }
                
                if (memberToRemove.Role == TeamMemberRole.Owner && team.OwnerCount <= 1) 
                { 
                    _logger.LogWarning("Nie można usunąć członka: Użytkownik ID {UserId} jest ostatnim właścicielem zespołu {TeamDisplayName}.", userId, team.DisplayName); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie można usunąć ostatniego właściciela zespołu."
                    );
                    return false; 
                }

                bool psSuccess = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _powerShellUserService.RemoveUserFromTeamAsync(
                        team.ExternalId ?? team.Id, memberToRemove.User!.UPN
                    ),
                    $"RemoveUserFromTeamAsync dla użytkownika {memberToRemove.User!.UPN} z zespołu '{team.DisplayName}'"
                );
                if (psSuccess)
                {
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_member";
                    memberToRemove.RemoveFromTeam("Usunięty przez serwis", currentUserUpn);
                    _teamMemberRepository.Update(memberToRemove);
                    _logger.LogInformation("Użytkownik ID {UserId} pomyślnie usunięty z zespołu {TeamDisplayName}.", userId, team.DisplayName);
                    await _cacheInvalidationService.InvalidateForTeamMemberRemovedAsync(teamId, userId);
                    
                    // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Użytkownik '{memberToRemove.User?.UPN}' usunięty z zespołu '{team.DisplayName}'."
                    );
                    return true;
                }
                else 
                { 
                    _logger.LogError("Błąd usuwania użytkownika ID {UserId} z zespołu {TeamDisplayName} w Microsoft Teams.", userId, team.DisplayName); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Błąd usuwania członka z zespołu w Microsoft Teams."
                    );
                    return false; 
                }
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania członka ID {UserId} z zespołu ID {TeamId}.", userId, teamId); 
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                return false; 
            }
        }

        /// <summary>
        /// Synchronizuje pojedynczy zespół z danymi z Microsoft Graph.
        /// </summary>
        private async Task<Team?> SynchronizeTeamWithGraphAsync(Team? localTeam, dynamic graphTeam, string teamId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_sync";
            bool requiresSync = false;
            string syncDetails = "";
            
            // Mapowanie danych z Graph
            var graphData = new {
                DisplayName = GetPropertyValue<string>(graphTeam, "displayName"),
                Description = GetPropertyValue<string>(graphTeam, "description"),
                IsArchived = GetPropertyValue<bool>(graphTeam, "isArchived"),
                Visibility = GetPropertyValue<string>(graphTeam, "visibility")
            };
            
            if (localTeam == null)
            {
                // Nowy zespół znaleziony w Graph
                _logger.LogInformation("Znaleziono nowy zespół {TeamId} w Graph. Tworzenie lokalnie.", teamId);
                
                localTeam = new Team
                {
                    Id = teamId,
                    ExternalId = teamId,
                    DisplayName = graphData.DisplayName ?? "Nieznany zespół",
                    Description = graphData.Description ?? "",
                    Status = graphData.IsArchived ? TeamStatus.Archived : TeamStatus.Active,
                    Visibility = MapVisibility(graphData.Visibility),
                    CreatedBy = currentUserUpn,
                    CreatedDate = DateTime.UtcNow
                };
                
                // Zastosuj prefiks jeśli archived
                if (graphData.IsArchived)
                {
                    localTeam.Archive("Zarchiwizowany w Microsoft Teams", currentUserUpn);
                }
                
                await _teamRepository.AddAsync(localTeam);
                requiresSync = true;
                syncDetails = "Nowy zespół dodany z Graph";
            }
            else
            {
                // Sprawdź rozbieżności
                var changes = new List<string>();
                
                // Status
                bool graphArchived = graphData.IsArchived;
                bool localArchived = localTeam.Status == TeamStatus.Archived;
                
                if (graphArchived != localArchived)
                {
                    if (graphArchived)
                    {
                        localTeam.Archive("Synchronizacja z Microsoft Teams", currentUserUpn);
                        changes.Add("Status: Active -> Archived");
                    }
                    else
                    {
                        localTeam.Restore(currentUserUpn);
                        changes.Add("Status: Archived -> Active");
                    }
                    requiresSync = true;
                }
                
                // Nazwa (bez prefiksu)
                var baseDisplayName = localTeam.GetBaseDisplayName();
                if (graphData.DisplayName != baseDisplayName)
                {
                    changes.Add($"Nazwa: '{baseDisplayName}' -> '{graphData.DisplayName}'");
                    localTeam.DisplayName = localTeam.Status == TeamStatus.Archived 
                        ? $"ARCHIWALNY - {graphData.DisplayName}" 
                        : graphData.DisplayName;
                    requiresSync = true;
                }
                
                // Opis
                var baseDescription = localTeam.GetBaseDescription();
                if (graphData.Description != baseDescription)
                {
                    changes.Add("Opis zaktualizowany");
                    localTeam.Description = localTeam.Status == TeamStatus.Archived && !string.IsNullOrEmpty(graphData.Description)
                        ? $"ARCHIWALNY - {graphData.Description}" 
                        : graphData.Description ?? "";
                    requiresSync = true;
                }
                
                // Widoczność
                var mappedVisibility = MapVisibility(graphData.Visibility);
                if (mappedVisibility != localTeam.Visibility)
                {
                    changes.Add($"Widoczność: {localTeam.Visibility} -> {mappedVisibility}");
                    localTeam.Visibility = mappedVisibility;
                    requiresSync = true;
                }
                
                if (requiresSync)
                {
                    localTeam.MarkAsModified(currentUserUpn);
                    _teamRepository.Update(localTeam);
                    syncDetails = $"Zsynchronizowano: {string.Join(", ", changes)}";
                }
            }
            
            // Loguj synchronizację jeśli były zmiany
            if (requiresSync)
            {
                var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                    OperationType.TeamSynchronized,
                    nameof(Team),
                    targetEntityId: teamId,
                    targetEntityName: localTeam.DisplayName,
                    details: syncDetails
                );
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed
                );
                
                // Invaliduj cache
                _powerShellCacheService.Remove($"{TeamByIdCacheKeyPrefix}{teamId}");
                _logger.LogInformation("Zespół {TeamId} zsynchronizowany z Graph. Zmiany: {Changes}", teamId, syncDetails);
            }
            
            return localTeam;
        }

        /// <summary>
        /// Mapuje widoczność zespołu z Graph na enum.
        /// </summary>
        private TeamVisibility MapVisibility(string? graphVisibility)
        {
            return graphVisibility?.ToLower() switch
            {
                "public" => TeamVisibility.Public,
                "private" => TeamVisibility.Private,
                _ => TeamVisibility.Private
            };
        }

        /// <summary>
        /// Obsługuje zespół, który został usunięty z Microsoft Graph.
        /// </summary>
        private async Task HandleDeletedTeamAsync(Team team)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_sync";
            
            // Soft-delete w lokalnej bazie
            ((BaseEntity)team).IsActive = false;
            team.Archive("Zespół usunięty z Microsoft Teams", currentUserUpn);
            team.MarkAsModified(currentUserUpn);
            
            _teamRepository.Update(team);
            
            // Loguj operację
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.TeamDeleted,
                nameof(Team),
                targetEntityId: team.Id,
                targetEntityName: team.DisplayName,
                details: "Zespół oznaczony jako usunięty podczas synchronizacji z Graph"
            );
            
            await _operationHistoryService.UpdateOperationStatusAsync(
                operation.Id,
                OperationStatus.Completed
            );
            
            // Invaliduj cache
            _powerShellCacheService.Remove($"{TeamByIdCacheKeyPrefix}{team.Id}");
        }

        /// <summary>
        /// Pomocnicza metoda do pobierania właściwości z obiektów PowerShell.
        /// </summary>
        private TValue? GetPropertyValue<TValue>(dynamic graphObject, string propertyName)
        {
            try
            {
                if (graphObject == null) return default(TValue);
                
                var psObject = graphObject as System.Management.Automation.PSObject;
                if (psObject?.Properties[propertyName]?.Value is TValue value)
                {
                    return value;
                }
                
                return default(TValue);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nie udało się pobrać właściwości {PropertyName} z obiektu Graph", propertyName);
                return default(TValue);
            }
        }

        /// <summary>
        /// Synchronizuje wszystkie zespoły z Microsoft Graph.
        /// </summary>
        public async Task<Dictionary<string, string>> SynchronizeAllTeamsAsync(string apiAccessToken, IProgress<int>? progress = null)
        {
            _logger.LogInformation("Rozpoczynanie masowej synchronizacji zespołów z Microsoft Graph");
            
            var results = new Dictionary<string, string>();
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.BulkTeamsSynchronization,
                nameof(Team),
                details: "Masowa synchronizacja wszystkich zespołów"
            );
            
            try
            {
                // Pobierz wszystkie zespoły z Graph
                var graphTeams = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _powerShellTeamService.GetAllTeamsAsync(),
                    "GetAllTeamsAsync dla masowej synchronizacji"
                );
                
                if (graphTeams == null || !graphTeams.Any())
                {
                    _logger.LogWarning("Brak zespołów w Microsoft Graph");
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        "Brak zespołów do synchronizacji"
                    );
                    return results;
                }
                
                // Pobierz wszystkie lokalne zespoły
                var localTeams = await _teamRepository.GetAllAsync();
                var localTeamDict = localTeams.ToDictionary(t => t.ExternalId ?? t.Id);
                
                int processed = 0;
                int total = graphTeams.Count();
                
                // Synchronizuj każdy zespół
                foreach (var graphTeam in graphTeams)
                {
                    var teamId = "";
                    try
                    {
                        teamId = GetPropertyValue<string>(graphTeam, "id");
                        if (string.IsNullOrEmpty(teamId)) continue;
                        
                        var localTeam = localTeamDict.GetValueOrDefault(teamId);
                        var syncedTeam = await SynchronizeTeamWithGraphAsync(localTeam, graphTeam, teamId);
                        
                        results[teamId] = "Zsynchronizowany";
                        localTeamDict.Remove(teamId); // Usuń z listy lokalnych
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Błąd synchronizacji zespołu {TeamId}", teamId);
                        if (!string.IsNullOrEmpty(teamId))
                        {
                            results[teamId] = $"Błąd: {ex.Message}";
                        }
                    }
                    
                    processed++;
                    progress?.Report((processed * 100) / total);
                }
                
                // Oznacz pozostałe lokalne zespoły jako usunięte
                foreach (var orphanedTeam in localTeamDict.Values.Where(t => t.IsActive))
                {
                    await HandleDeletedTeamAsync(orphanedTeam);
                    results[orphanedTeam.Id] = "Oznaczony jako usunięty";
                }
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Zsynchronizowano {results.Count} zespołów"
                );
                
                // Invaliduj cały cache zespołów
                await RefreshCacheAsync();
                
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas masowej synchronizacji");
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    ex.Message,
                    ex.StackTrace
                );
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a zespołów.");
            await _cacheInvalidationService.InvalidateBatchAsync(new Dictionary<string, List<string>>
            {
                ["RefreshAllCache"] = new List<string> { "*" }
            });
            _logger.LogInformation("Cache zespołów został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
        }

        /// <summary>
        /// PRZESTARZAŁA METODA - zastąpiona przez CacheInvalidationService (Etap 7/8)
        /// </summary>
        [Obsolete("Użyj CacheInvalidationService zamiast tej metody")]
        private void InvalidateCache(string? teamId = null, string? ownerUpn = null, TeamStatus? oldStatus = null, TeamStatus? newStatus = null, string? oldOwnerUpnIfChanged = null, bool invalidateAll = false)
        {
            _logger.LogWarning("Użycie przestarzałej metody InvalidateCache. Należy zastąpić wywołaniami CacheInvalidationService.");

            if (invalidateAll)
            {
                // TYLKO dla RefreshCacheAsync() - globalne resetowanie
                _powerShellCacheService.InvalidateAllCache();
                _logger.LogDebug("Wykonano globalne resetowanie cache przez PowerShellCacheService.");
                return;
            }

            // GRANULARNA inwalidacja przez PowerShellCacheService (stara implementacja)
            _powerShellCacheService.InvalidateAllActiveTeamsList();
            _powerShellCacheService.InvalidateArchivedTeamsList();
            _powerShellCacheService.InvalidateTeamSpecificByStatus();

            if (!string.IsNullOrWhiteSpace(teamId))
            {
                _powerShellCacheService.InvalidateTeamById(teamId);
            }

            if (!string.IsNullOrWhiteSpace(ownerUpn))
            {
                _powerShellCacheService.InvalidateTeamsByOwner(ownerUpn);
            }

            if (!string.IsNullOrWhiteSpace(oldOwnerUpnIfChanged) && oldOwnerUpnIfChanged != ownerUpn)
            {
                _powerShellCacheService.InvalidateTeamsByOwner(oldOwnerUpnIfChanged);
            }

            // Inwalidacja według statusu zespołu
            if (oldStatus.HasValue)
            {
                _powerShellCacheService.InvalidateTeamsByStatus(oldStatus.Value);
            }

            if (newStatus.HasValue && newStatus != oldStatus)
            {
                _powerShellCacheService.InvalidateTeamsByStatus(newStatus.Value);
            }

            _logger.LogDebug("Wykonano granularną inwalidację cache zespołów przez PowerShellCacheService (stara implementacja).");
        }

        /// <summary>
        /// Asynchronicznie dodaje wielu użytkowników do zespołu (operacja masowa).
        /// </summary>
        public async Task<Dictionary<string, bool>> AddUsersToTeamAsync(string teamId, List<string> userUpns, string apiAccessToken)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            
            // 1. Audyt - poziom aplikacyjny
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.TeamMembersAdded, 
                nameof(Team), 
                targetEntityId: teamId,
                targetEntityName: $"Dodawanie {userUpns.Count} użytkowników do zespołu"
            );

            try
            {
                // Weryfikacja zespołu
                var team = await GetTeamByIdAsync(teamId, includeMembers: true, apiAccessToken: apiAccessToken);
                if (team == null)
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id, 
                        OperationStatus.Failed,
                        "Nie znaleziono zespołu"
                    );
                    return new Dictionary<string, bool>();
                }

                // 2. Wywołanie PowerShell z ExecuteWithAutoConnectAsync
                var psResults = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _powerShellBulkOps.BulkAddUsersToTeamAsync(
                        teamId, userUpns, "Member"
                    ),
                    $"BulkAddUsersToTeamAsync dla {userUpns.Count} użytkowników do zespołu ID: {teamId}"
                );

                // 3. Synchronizacja lokalnej bazy (tylko dla sukcesów)
                var syncSuccesses = 0;
                var syncFailures = 0;
                var addedUsers = new List<string>();

                foreach (var kvp in psResults?.Where(r => r.Value) ?? Enumerable.Empty<KeyValuePair<string, bool>>())
                {
                    try
                    {
                        var user = await _userRepository.GetUserByUpnAsync(kvp.Key);
                        if (user != null)
                        {
                            // Sprawdź czy użytkownik nie jest już członkiem
                            var existingMembership = await _teamMemberRepository
                                .FindAsync(tm => tm.TeamId == teamId && tm.UserId == user.Id);
                            var existing = existingMembership.FirstOrDefault();
                            
                            if (existing == null)
                            {
                                var newMember = new TeamMember
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    TeamId = teamId,
                                    UserId = user.Id,
                                    Role = TeamMemberRole.Member,
                                    AddedDate = DateTime.UtcNow,
                                    AddedBy = currentUserUpn,
                                    IsActive = true,
                                    IsApproved = !team.RequiresApproval,
                                    ApprovedDate = !team.RequiresApproval ? DateTime.UtcNow : null,
                                    ApprovedBy = !team.RequiresApproval ? currentUserUpn : null,
                                    CreatedBy = currentUserUpn,
                                    CreatedDate = DateTime.UtcNow
                                };
                                await _teamMemberRepository.AddAsync(newMember);
                                syncSuccesses++;
                                addedUsers.Add(user.DisplayName);
                            }
                            else if (!existing.IsActive)
                            {
                                // Reaktywuj istniejące członkostwo
                                existing.RestoreToTeam(currentUserUpn);
                                _teamMemberRepository.Update(existing);
                                syncSuccesses++;
                                addedUsers.Add(user.DisplayName);
                            }
                        }
                        else
                        {
                            syncFailures++;
                            _logger.LogWarning("User {Upn} not found in local database", kvp.Key);
                        }
                    }
                    catch (Exception ex)
                    {
                        syncFailures++;
                        _logger.LogError(ex, "Error syncing user {Upn} to team", kvp.Key);
                    }
                }

                // 4. Powiadomienia
                var psSuccessCount = psResults?.Count(r => r.Value) ?? 0;
                var psFailureCount = psResults?.Count(r => !r.Value) ?? 0;
                
                var message = $"Operacja dodawania użytkowników do zespołu '{team.DisplayName}' zakończona. " +
                             $"PowerShell: {psSuccessCount}/{userUpns.Count} sukces, " +
                             $"Synchronizacja: {syncSuccesses}/{psSuccessCount} sukces.";

                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    message,
                    psFailureCount == 0 && syncFailures == 0 ? "success" : "warning"
                );

                // 5. Finalizacja audytu
                var finalStatus = psFailureCount == 0 && syncFailures == 0 
                    ? OperationStatus.Completed 
                    : psFailureCount == userUpns.Count 
                        ? OperationStatus.Failed
                        : OperationStatus.PartialSuccess;

                var details = $"PowerShell: {psSuccessCount}/{userUpns.Count} sukces, {psFailureCount} błąd. " +
                             $"Synchronizacja: {syncSuccesses}/{psSuccessCount} sukces, {syncFailures} błąd.";

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, 
                    finalStatus,
                    details
                );

                // Powiadom właściciela zespołu o nowych członkach
                if (addedUsers.Any() && team.Owner != currentUserUpn)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        team.Owner,
                        $"Do zespołu '{team.DisplayName}' dodano {addedUsers.Count} nowych członków.",
                        "info"
                    );
                }

                // Invalidacja cache - przekaż listę ID dodanych użytkowników
                var addedUserIds = new List<string>();
                foreach (var kvp in psResults?.Where(r => r.Value) ?? Enumerable.Empty<KeyValuePair<string, bool>>())
                {
                    var user = await _userRepository.GetUserByUpnAsync(kvp.Key);
                    if (user != null)
                    {
                        addedUserIds.Add(user.Id);
                    }
                }
                await _cacheInvalidationService.InvalidateForTeamMembersBulkOperationAsync(teamId, addedUserIds);

                // Powiadomienie do administratorów (asynchroniczne, nie blokuje operacji)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _adminNotificationService.SendBulkUsersOperationNotificationAsync(
                            "Dodawanie użytkowników",
                            team.DisplayName,
                            userUpns.Count,
                            psSuccessCount,
                            psFailureCount,
                            currentUserUpn
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Błąd podczas wysyłania powiadomienia administratorskiego o masowym dodawaniu użytkowników");
                    }
                });

                return psResults ?? new Dictionary<string, bool>();
            }
            catch (PowerShellConnectionException ex)
            {
                _logger.LogError(ex, "Nie można dodać użytkowników: Błąd połączenia z Microsoft Graph API.");
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    "Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie AddUsersToTeamAsync."
                );
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Nie udało się dodać użytkowników: Błąd połączenia z Microsoft Graph API.",
                    "error"
                );

                return new Dictionary<string, bool>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding users to team {TeamId}", teamId);
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, 
                    OperationStatus.Failed, 
                    $"Błąd krytyczny: {ex.Message}",
                    ex.StackTrace
                );
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Wystąpił błąd podczas dodawania użytkowników do zespołu: {ex.Message}",
                    "error"
                );

                return new Dictionary<string, bool>();
            }
        }

        /// <summary>
        /// Asynchronicznie usuwa wielu użytkowników z zespołu (operacja masowa).
        /// </summary>
        public async Task<Dictionary<string, bool>> RemoveUsersFromTeamAsync(string teamId, List<string> userUpns, string reason, string apiAccessToken)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            
            // 1. Audyt
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.TeamMembersRemoved, 
                nameof(Team), 
                targetEntityId: teamId,
                targetEntityName: $"Usuwanie {userUpns.Count} użytkowników z zespołu"
            );

            try
            {
                // Weryfikacja zespołu
                var team = await GetTeamByIdAsync(teamId, includeMembers: true, apiAccessToken: apiAccessToken);
                if (team == null)
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id, 
                        OperationStatus.Failed,
                        "Nie znaleziono zespołu"
                    );
                    return new Dictionary<string, bool>();
                }

                // 2. Wywołanie PowerShell z ExecuteWithAutoConnectAsync
                var psResults = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _powerShellBulkOps.BulkRemoveUsersFromTeamAsync(
                        teamId, userUpns
                    ),
                    $"BulkRemoveUsersFromTeamAsync dla {userUpns.Count} użytkowników z zespołu ID: {teamId}"
                );

                // 3. Synchronizacja lokalnej bazy
                var syncSuccesses = 0;
                var syncFailures = 0;
                var removedUsers = new List<string>();

                foreach (var kvp in psResults?.Where(r => r.Value) ?? Enumerable.Empty<KeyValuePair<string, bool>>())
                {
                    try
                    {
                        var user = await _userRepository.GetUserByUpnAsync(kvp.Key);
                        if (user != null)
                        {
                            var memberships = await _teamMemberRepository
                                .FindAsync(tm => tm.TeamId == teamId && tm.UserId == user.Id && tm.IsActive);
                            var membership = memberships.FirstOrDefault();
                            
                            if (membership != null)
                            {
                                membership.RemoveFromTeam(reason, currentUserUpn);
                                _teamMemberRepository.Update(membership);
                                syncSuccesses++;
                                removedUsers.Add(user.DisplayName);
                            }
                            else
                            {
                                // Użytkownik został usunięty z Teams ale nie był w lokalnej bazie
                                _logger.LogWarning("Membership for {Upn} not found in local database", kvp.Key);
                                syncSuccesses++; // Liczymy jako sukces bo cel został osiągnięty
                            }
                        }
                        else
                        {
                            syncFailures++;
                            _logger.LogWarning("User {Upn} not found in local database", kvp.Key);
                        }
                    }
                    catch (Exception ex)
                    {
                        syncFailures++;
                        _logger.LogError(ex, "Error syncing removal of user {Upn}", kvp.Key);
                    }
                }

                // 4. Powiadomienia
                var psSuccessCount = psResults?.Count(r => r.Value) ?? 0;
                var psFailureCount = psResults?.Count(r => !r.Value) ?? 0;
                
                var message = $"Operacja usuwania użytkowników z zespołu '{team.DisplayName}' zakończona. " +
                             $"PowerShell: {psSuccessCount}/{userUpns.Count} sukces, " +
                             $"Synchronizacja: {syncSuccesses}/{psSuccessCount} sukces.";

                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    message,
                    psFailureCount == 0 && syncFailures == 0 ? "success" : "warning"
                );

                // 5. Finalizacja audytu
                var finalStatus = psFailureCount == 0 && syncFailures == 0 
                    ? OperationStatus.Completed 
                    : psFailureCount == userUpns.Count 
                        ? OperationStatus.Failed
                        : OperationStatus.PartialSuccess;

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, 
                    finalStatus,
                    $"PowerShell: {psSuccessCount}/{userUpns.Count}, Sync: {syncSuccesses}/{psSuccessCount}"
                );

                // Powiadom właściciela zespołu o usuniętych członkach
                if (removedUsers.Any() && team.Owner != currentUserUpn)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        team.Owner,
                        $"Z zespołu '{team.DisplayName}' usunięto {removedUsers.Count} członków.",
                        "info"
                    );
                }

                // Invalidacja cache - przekaż listę ID usuniętych użytkowników
                var removedUserIds = new List<string>();
                foreach (var kvp in psResults?.Where(r => r.Value) ?? Enumerable.Empty<KeyValuePair<string, bool>>())
                {
                    var user = await _userRepository.GetUserByUpnAsync(kvp.Key);
                    if (user != null)
                    {
                        removedUserIds.Add(user.Id);
                    }
                }
                await _cacheInvalidationService.InvalidateForTeamMembersBulkOperationAsync(teamId, removedUserIds);

                // Powiadomienie do administratorów (asynchroniczne, nie blokuje operacji)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _adminNotificationService.SendBulkUsersOperationNotificationAsync(
                            "Usuwanie użytkowników",
                            team.DisplayName,
                            userUpns.Count,
                            psSuccessCount,
                            psFailureCount,
                            currentUserUpn
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Błąd podczas wysyłania powiadomienia administratorskiego o masowym usuwaniu użytkowników");
                    }
                });

                return psResults ?? new Dictionary<string, bool>();
            }
            catch (PowerShellConnectionException ex)
            {
                _logger.LogError(ex, "Nie można usunąć użytkowników: Błąd połączenia z Microsoft Graph API.");
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    "Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie RemoveUsersFromTeamAsync."
                );
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Nie udało się usunąć użytkowników: Błąd połączenia z Microsoft Graph API.",
                    "error"
                );

                return new Dictionary<string, bool>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing users from team {TeamId}", teamId);
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, 
                    OperationStatus.Failed, 
                    $"Błąd krytyczny: {ex.Message}",
                    ex.StackTrace
                );
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Wystąpił błąd podczas usuwania użytkowników z zespołu: {ex.Message}",
                    "error"
                );

                return new Dictionary<string, bool>();
            }
        }
    }
}