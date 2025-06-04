// Plik: TeamsManager.Core/Services/TeamService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using Microsoft.Identity.Client; // NOWE: Dla IConfidentialClientApplication i UserAssertion

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
        private readonly INotificationService _notificationService;
        private readonly ILogger<TeamService> _logger;
        private readonly IGenericRepository<SchoolType> _schoolTypeRepository;
        private readonly ISchoolYearRepository _schoolYearRepository;
        private readonly IMemoryCache _cache;
        private readonly IConfidentialClientApplication _confidentialClientApplication; // NOWE
        private readonly IOperationHistoryService _operationHistoryService; // Dodaj to pole

        // Definicje kluczy cache
        private const string AllActiveTeamsCacheKey = "Teams_AllActive";
        private const string ActiveTeamsSpecificCacheKey = "Teams_Active";
        private const string ArchivedTeamsCacheKey = "Teams_Archived";
        private const string TeamsByOwnerCacheKeyPrefix = "Teams_ByOwner_";
        private const string TeamByIdCacheKeyPrefix = "Team_Id_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(15);

        private static CancellationTokenSource _teamsCacheTokenSource = new CancellationTokenSource();

        // NOWE: Domyślne zakresy dla Microsoft Graph
        private readonly string[] _graphReadScopes = new[] { "Group.Read.All", "User.Read.All" };
        private readonly string[] _graphReadWriteScopes = new[] { "Group.ReadWrite.All", "User.Read.All", "Directory.Read.All" }; // Directory.Read.All może być potrzebne do weryfikacji użytkowników i grup

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
            INotificationService notificationService,
            ILogger<TeamService> logger,
            IGenericRepository<SchoolType> schoolTypeRepository,
            ISchoolYearRepository schoolYearRepository,
            IMemoryCache memoryCache,
            IConfidentialClientApplication confidentialClientApplication, // NOWE
            IOperationHistoryService operationHistoryService) // Dodaj to do konstruktora
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
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _schoolTypeRepository = schoolTypeRepository ?? throw new ArgumentNullException(nameof(schoolTypeRepository));
            _schoolYearRepository = schoolYearRepository ?? throw new ArgumentNullException(nameof(schoolYearRepository));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _confidentialClientApplication = confidentialClientApplication ?? throw new ArgumentNullException(nameof(confidentialClientApplication)); // NOWE
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService)); // Zainicjalizuj to
        }

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_teamsCacheTokenSource.Token));
        }

        // NOWE: Metoda pomocnicza do obsługi OBO i połączenia z Graph przez PowerShellService
        private async Task<bool> ConnectToGraphOnBehalfOfUserAsync(string? apiAccessToken, string[] scopes)
        {
            if (string.IsNullOrEmpty(apiAccessToken))
            {
                _logger.LogWarning("ConnectToGraphOnBehalfOfUserAsync: Token dostępu API (apiAccessToken) jest pusty lub null.");
                return false;
            }

            try
            {
                var userAssertion = new UserAssertion(apiAccessToken);
                _logger.LogDebug("ConnectToGraphOnBehalfOfUserAsync: Próba uzyskania tokenu OBO dla zakresów: {Scopes}", string.Join(", ", scopes));

                var authResult = await _confidentialClientApplication.AcquireTokenOnBehalfOf(scopes, userAssertion)
                    .ExecuteAsync();

                if (string.IsNullOrEmpty(authResult.AccessToken))
                {
                    _logger.LogError("ConnectToGraphOnBehalfOfUserAsync: Nie udało się uzyskać tokenu dostępu do Graph w przepływie OBO (authResult.AccessToken jest pusty).");
                    return false;
                }
                _logger.LogInformation("ConnectToGraphOnBehalfOfUserAsync: Pomyślnie uzyskano token OBO dla Graph.");
                // Powiadamianie usług PowerShell o nowym tokenie zostanie zaimplementowane w przyszłości
                return true;
            }
            // POPRAWKA BŁĘDU CS1061 (linia 117): Zamiast ex.SubError używamy ex.Classification
            catch (MsalUiRequiredException ex)
            {
                _logger.LogError(ex, "ConnectToGraphOnBehalfOfUserAsync: Wymagana interakcja użytkownika lub zgoda (MsalUiRequiredException) w przepływie OBO. Scopes: {Scopes}. Błąd: {Classification}. Szczegóły: {MsalErrorMessage}", string.Join(", ", scopes), ex.Classification, ex.Message);
                return false;
            }
            catch (MsalServiceException ex)
            {
                _logger.LogError(ex, "ConnectToGraphOnBehalfOfUserAsync: Błąd usługi MSAL podczas próby uzyskania tokenu OBO dla scopes: {Scopes}. Kod błędu: {MsalErrorCode}. Szczegóły: {MsalErrorMessage}", string.Join(", ", scopes), ex.ErrorCode, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConnectToGraphOnBehalfOfUserAsync: Nieoczekiwany błąd podczas uzyskiwania tokenu OBO dla scopes: {Scopes}.", string.Join(", ", scopes));
                return false;
            }
        }


        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache. Zwraca zespół tylko jeśli jego Status to Active.</remarks>
        public async Task<Team?> GetTeamByIdAsync(string teamId, bool includeMembers = false, bool includeChannels = false, bool forceRefresh = false, string? apiAccessToken = null) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Pobieranie zespołu o ID: {TeamId}. Dołączanie członków: {IncludeMembers}, Dołączanie kanałów: {IncludeChannels}, Wymuszenie odświeżenia: {ForceRefresh}", teamId, includeMembers, includeChannels, forceRefresh);

            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogWarning("Próba pobrania zespołu z pustym ID.");
                return null;
            }

            string cacheKey = TeamByIdCacheKeyPrefix + teamId;
            Team? team;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out team) && team != null)
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

                // ZMIANA: Logika połączenia z Graph przez OBO
                if (!string.IsNullOrEmpty(apiAccessToken))
                {
                    if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphReadScopes)) // Użycie _graphReadScopes
                    {
                        _logger.LogError("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie GetTeamByIdAsync.");
                        // Rozważ, czy w tym przypadku zwrócić błąd, czy próbować tylko z lokalnej bazy
                        // Na razie, jeśli połączenie z Graph się nie powiedzie, nadal próbujemy z lokalnej bazy.
                    }
                    else
                    {
                        var psTeam = await _powerShellTeamService.GetTeamAsync(teamId);
                        if (psTeam != null)
                        {
                            _logger.LogDebug("Zespół ID: {TeamId} znaleziony w Graph API. Informacje z Graph mogą być użyte do aktualizacji lokalnej bazy (logika niezaimplementowana).", teamId);
                        }
                        else
                        {
                            _logger.LogWarning("Zespół ID: {TeamId} nie znaleziony w Graph API.", teamId);
                        }
                    }
                }

                team = await _teamRepository.GetByIdAsync(teamId);

                if (team != null && team.IsActive)
                {
                    _cache.Set(cacheKey, team, GetDefaultCacheEntryOptions());
                    _logger.LogDebug("Zespół ID: {TeamId} dodany do cache.", teamId);
                }
                else
                {
                    _cache.Remove(cacheKey);
                    if (team != null && !team.IsActive)
                    {
                        _logger.LogDebug("Zespół ID: {TeamId} jest nieaktywny (Status != Active), nie zostanie zcache'owany po ID i nie zostanie zwrócony przez tę metodę.", teamId);
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

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<Team>? cachedTeams) && cachedTeams != null)
            {
                _logger.LogDebug("Zespoły z Team.Status = Active znalezione w cache.");
                return cachedTeams;
            }
            _logger.LogDebug("Zespoły z Team.Status = Active nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");

            if (!string.IsNullOrEmpty(apiAccessToken))
            {
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphReadScopes)) // Użycie _graphReadScopes
                {
                    _logger.LogError("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie GetAllTeamsAsync.");
                }
                // Można dodać logikę synchronizacji wszystkich zespołów z Graph
            }

            var teamsFromDb = await _teamRepository.FindAsync(t => t.IsActive);
            _cache.Set(cacheKey, teamsFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Zespoły z Team.Status = Active dodane do cache.");
            return teamsFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Team>> GetActiveTeamsAsync(bool forceRefresh = false, string? apiAccessToken = null) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Pobieranie zespołów o statusie 'Active'. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);
            string cacheKey = ActiveTeamsSpecificCacheKey;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<Team>? cachedTeams) && cachedTeams != null)
            {
                _logger.LogDebug("Zespoły o statusie 'Active' znalezione w cache. Liczba zespołów: {Count}", cachedTeams.Count());
                return cachedTeams;
            }
            _logger.LogDebug("Zespoły o statusie 'Active' nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium. Cache key: {CacheKey}", cacheKey);

            if (!string.IsNullOrEmpty(apiAccessToken))
            {
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphReadScopes))
                {
                    _logger.LogError("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie GetActiveTeamsAsync.");
                }
            }

            var teamsFromDb = await _teamRepository.GetActiveTeamsAsync();
            _cache.Set(cacheKey, teamsFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Zespoły o statusie 'Active' dodane do cache. Liczba zespołów: {Count}", teamsFromDb.Count());
            return teamsFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Team>> GetArchivedTeamsAsync(bool forceRefresh = false, string? apiAccessToken = null) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Pobieranie zespołów o statusie 'Archived'. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);
            string cacheKey = ArchivedTeamsCacheKey;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<Team>? cachedTeams) && cachedTeams != null)
            {
                _logger.LogDebug("Zespoły o statusie 'Archived' znalezione w cache. Liczba zespołów: {Count}", cachedTeams.Count());
                return cachedTeams;
            }
            _logger.LogDebug("Zespoły o statusie 'Archived' nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium. Cache key: {CacheKey}", cacheKey);

            if (!string.IsNullOrEmpty(apiAccessToken))
            {
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphReadScopes))
                {
                    _logger.LogError("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie GetArchivedTeamsAsync.");
                }
            }
            var teamsFromDb = await _teamRepository.GetArchivedTeamsAsync();
            _cache.Set(cacheKey, teamsFromDb, GetDefaultCacheEntryOptions());
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

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<Team>? cachedTeams) && cachedTeams != null)
            {
                _logger.LogDebug("Zespoły (status Active) dla właściciela {OwnerUpn} znalezione w cache.", ownerUpn);
                return cachedTeams;
            }
            _logger.LogDebug("Zespoły (status Active) dla właściciela {OwnerUpn} nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", ownerUpn);

            if (!string.IsNullOrEmpty(apiAccessToken))
            {
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphReadScopes))
                {
                    _logger.LogError("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie GetTeamsByOwnerAsync.");
                }
            }
            var teamsFromDb = await _teamRepository.GetTeamsByOwnerAsync(ownerUpn);
            _cache.Set(cacheKey, teamsFromDb, GetDefaultCacheEntryOptions());
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

                // ZMIANA: Użycie nowej metody pomocniczej
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphReadWriteScopes))
                {
                    _logger.LogError("Nie można utworzyć zespołu: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie CreateTeamAsync (OBO)."
                    );
                    return null;
                }
                // Reszta logiki tworzenia zespołu jak wcześniej...
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

                // Poprawione wywołanie
                string? externalTeamIdFromPS = await _powerShellTeamService.CreateTeamAsync(finalDisplayName, description, ownerUser.UPN, visibility, template?.Template);
                bool psSuccess = !string.IsNullOrEmpty(externalTeamIdFromPS);

                if (psSuccess)
                {
                    _logger.LogInformation("Zespół '{FinalDisplayName}' pomyślnie utworzony w Microsoft Teams. External ID: {ExternalTeamId}", finalDisplayName, externalTeamIdFromPS);
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation";
                    newTeam = new Team { /* ... inicjalizacja pól ... */ Id = Guid.NewGuid().ToString(), DisplayName = finalDisplayName, Description = description, Owner = ownerUser.UPN, Status = TeamStatus.Active, Visibility = visibility, TemplateId = template?.Id, SchoolTypeId = schoolTypeId, SchoolYearId = schoolYearId, ExternalId = externalTeamIdFromPS, SchoolType = schoolType, SchoolYear = schoolYear, Template = template };
                    if (schoolYear != null) newTeam.AcademicYear = schoolYear.Name;
                    var ownerMembership = new TeamMember { Id = Guid.NewGuid().ToString(), UserId = ownerUser.Id, TeamId = newTeam.Id, Role = ownerUser.DefaultTeamRole, AddedDate = DateTime.UtcNow, AddedBy = currentUserUpn, IsActive = true, IsApproved = !newTeam.RequiresApproval, ApprovedDate = !newTeam.RequiresApproval ? DateTime.UtcNow : null, ApprovedBy = !newTeam.RequiresApproval ? currentUserUpn : null, User = ownerUser, Team = newTeam };
                    newTeam.Members.Add(ownerMembership);
                    
                    // 3. Synchronizacja lokalnej bazy
                    await _teamRepository.AddAsync(newTeam);
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
                    
                    // 5. Invalidacja cache i finalizacja audytu
                    InvalidateCache(teamId: newTeam.Id, ownerUpn: newTeam.Owner, newStatus: newTeam.Status, invalidateAll: true);
                    
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia zespołu {DisplayName}.", displayName);
                
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

                // ZMIANA: Użycie nowej metody pomocniczej
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphReadWriteScopes))
                {
                    _logger.LogError("Nie można zaktualizować zespołu: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie UpdateTeamAsync (OBO)."
                    );
                    return false;
                }

                bool psSuccess = await _powerShellTeamService.UpdateTeamPropertiesAsync(existingTeam.ExternalId ?? teamToUpdate.Id, teamToUpdate.DisplayName, teamToUpdate.Description, teamToUpdate.Visibility);
                if (psSuccess)
                {
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
                    existingTeam.DisplayName = teamToUpdate.DisplayName;
                    existingTeam.Description = teamToUpdate.Description;
                    existingTeam.Owner = teamToUpdate.Owner;
                    existingTeam.Visibility = teamToUpdate.Visibility;
                    // ... inne pola ...
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
                    InvalidateCache(teamId: existingTeam.Id, ownerUpn: existingTeam.Owner, oldStatus: oldStatus, newStatus: existingTeam.Status, oldOwnerUpnIfChanged: oldOwnerUpn, invalidateAll: true);
                    
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
                    InvalidateCache(teamId: teamId, ownerUpn: team.Owner, oldStatus: TeamStatus.Archived, newStatus: TeamStatus.Archived, invalidateAll: true); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Zespół '{team.DisplayName}' (ID: {team.Id}) był już zarchiwizowany."
                    );
                    return true; 
                }

                // ZMIANA: Użycie nowej metody pomocniczej
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphReadWriteScopes))
                {
                    _logger.LogError("Nie można zarchiwizować zespołu: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie ArchiveTeamAsync (OBO)."
                    );
                    return false;
                }

                bool psSuccess = await _powerShellTeamService.ArchiveTeamAsync(team.ExternalId ?? team.Id);
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
                    InvalidateCache(teamId: teamId, ownerUpn: team.Owner, oldStatus: oldStatus, newStatus: team.Status, invalidateAll: true);
                    
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
                    InvalidateCache(teamId: teamId, ownerUpn: team.Owner, oldStatus: TeamStatus.Active, newStatus: TeamStatus.Active, invalidateAll: true); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Zespół '{team.DisplayName}' (ID: {team.Id}) był już aktywny."
                    );
                    return true; 
                }

                // ZMIANA: Użycie nowej metody pomocniczej
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphReadWriteScopes))
                {
                    _logger.LogError("Nie można przywrócić zespołu: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie RestoreTeamAsync (OBO)."
                    );
                    return false;
                }

                bool psSuccess = await _powerShellTeamService.UnarchiveTeamAsync(team.ExternalId ?? team.Id);
                if (psSuccess)
                {
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_restore";
                    var oldStatus = team.Status;
                    team.Restore(currentUserUpn);
                    _teamRepository.Update(team);
                    _logger.LogInformation("Zespół ID {TeamId} pomyślnie przywrócony.", teamId);
                    InvalidateCache(teamId: teamId, ownerUpn: team.Owner, oldStatus: oldStatus, newStatus: team.Status, invalidateAll: true);
                    
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

                // ZMIANA: Użycie nowej metody pomocniczej
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphReadWriteScopes)) // Potrzebne Group.ReadWrite.All do usunięcia
                {
                    _logger.LogError("Nie można usunąć zespołu: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie DeleteTeamAsync (OBO)."
                    );
                    return false;
                }

                bool psSuccess = await _powerShellTeamService.DeleteTeamAsync(team.ExternalId ?? team.Id);
                if (psSuccess)
                {
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete";
                    var oldStatus = team.Status;
                    // W przypadku rzeczywistego usunięcia z Graph, lokalnie oznaczamy jako zarchiwizowany/usunięty, aby zachować spójność i historię
                    team.Archive($"Usunięty przez {currentUserUpn} (operacja DeleteTeamAsync)", currentUserUpn);
                    _teamRepository.Update(team);
                    _logger.LogInformation("Zespół ID {TeamId} pomyślnie usunięty z Microsoft Teams (lokalnie zarchiwizowany).", teamId);
                    InvalidateCache(teamId: teamId, ownerUpn: team.Owner, oldStatus: oldStatus, newStatus: team.Status, invalidateAll: true);
                    
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

                // ZMIANA: Użycie nowej metody pomocniczej
                // Zakresy dla dodawania członka
                var memberAddScopes = new[] { "GroupMember.ReadWrite.All", "User.Read.All" }; // User.Read.All do weryfikacji UPN jeśli PowerShellService tego wymaga
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, memberAddScopes))
                {
                    _logger.LogError("Nie można dodać członka: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie AddMemberAsync (OBO)."
                    );
                    return null;
                }

                bool psSuccess = await _powerShellUserService.AddUserToTeamAsync(team.ExternalId ?? team.Id, user.UPN, role.ToString());
                if (psSuccess)
                {
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_add_member";
                    var newMember = new TeamMember { /* ... inicjalizacja pól ... */ Id = Guid.NewGuid().ToString(), UserId = user.Id, TeamId = team.Id, Role = role, AddedDate = DateTime.UtcNow, AddedBy = currentUserUpn, IsActive = true, IsApproved = !team.RequiresApproval, ApprovedDate = !team.RequiresApproval ? DateTime.UtcNow : null, ApprovedBy = !team.RequiresApproval ? currentUserUpn : null, User = user, Team = team };
                    await _teamMemberRepository.AddAsync(newMember);
                    _logger.LogInformation("Użytkownik {UserUPN} pomyślnie dodany do zespołu {TeamDisplayName}.", userUpn, team.DisplayName);
                    InvalidateCache(teamId: teamId, ownerUpn: team.Owner, invalidateAll: false);
                    
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

                // ZMIANA: Użycie nowej metody pomocniczej
                var memberRemoveScopes = new[] { "GroupMember.ReadWrite.All", "User.Read.All" };
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, memberRemoveScopes))
                {
                    _logger.LogError("Nie można usunąć członka: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie RemoveMemberAsync (OBO)."
                    );
                    return false;
                }

                // Poprawione wywołanie
                bool psSuccess = await _powerShellUserService.RemoveUserFromTeamAsync(team.ExternalId ?? team.Id, memberToRemove.User!.UPN);
                if (psSuccess)
                {
                    var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_member";
                    memberToRemove.RemoveFromTeam("Usunięty przez serwis", currentUserUpn);
                    _teamMemberRepository.Update(memberToRemove);
                    _logger.LogInformation("Użytkownik ID {UserId} pomyślnie usunięty z zespołu {TeamDisplayName}.", userId, team.DisplayName);
                    InvalidateCache(teamId: teamId, ownerUpn: team.Owner, invalidateAll: false);
                    
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

        /// <inheritdoc />
        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a zespołów.");
            InvalidateCache(invalidateAll: true);
            _logger.LogInformation("Cache zespołów został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        private void InvalidateCache(string? teamId = null, string? ownerUpn = null, TeamStatus? oldStatus = null, TeamStatus? newStatus = null, string? oldOwnerUpnIfChanged = null, bool invalidateAll = false)
        {
            _logger.LogDebug("Inwalidacja cache'u zespołów. teamId: {TeamId}, ownerUpn: {OwnerUpn}, oldStatus: {OldStatus}, newStatus: {NewStatus}, oldOwnerUpnIfChanged: {OldOwner}, invalidateAll: {InvalidateAll}",
                teamId, ownerUpn, oldStatus, newStatus, oldOwnerUpnIfChanged, invalidateAll);

            var oldTokenSource = Interlocked.Exchange(ref _teamsCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
            _logger.LogDebug("Token cache'u dla zespołów został zresetowany.");

            _cache.Remove(AllActiveTeamsCacheKey);
            _logger.LogDebug("Usunięto z cache klucz: {CacheKey}", AllActiveTeamsCacheKey);
            _cache.Remove(ActiveTeamsSpecificCacheKey);
            _logger.LogDebug("Usunięto z cache klucz: {CacheKey}", ActiveTeamsSpecificCacheKey);
            _cache.Remove(ArchivedTeamsCacheKey);
            _logger.LogDebug("Usunięto z cache klucz: {CacheKey}", ArchivedTeamsCacheKey);

            if (invalidateAll)
            {
                _logger.LogDebug("Globalna inwalidacja (invalidateAll=true) dla cache'u zespołów.");
            }

            if (!string.IsNullOrWhiteSpace(teamId))
            {
                _cache.Remove(TeamByIdCacheKeyPrefix + teamId);
                _logger.LogDebug("Usunięto z cache klucz: {CacheKey}{Id}", TeamByIdCacheKeyPrefix, teamId);
            }

            if (!string.IsNullOrWhiteSpace(ownerUpn))
            {
                _cache.Remove(TeamsByOwnerCacheKeyPrefix + ownerUpn);
                _logger.LogDebug("Usunięto z cache klucz: {CacheKey}{Upn}", TeamsByOwnerCacheKeyPrefix, ownerUpn);
            }

            if (!string.IsNullOrWhiteSpace(oldOwnerUpnIfChanged) && oldOwnerUpnIfChanged != ownerUpn)
            {
                _cache.Remove(TeamsByOwnerCacheKeyPrefix + oldOwnerUpnIfChanged);
                _logger.LogDebug("Usunięto z cache klucz dla starego właściciela: {CacheKey}{Upn}", TeamsByOwnerCacheKeyPrefix, oldOwnerUpnIfChanged);
            }
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

                // ZMIANA: Użycie nowej metody pomocniczej
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, new[] { "GroupMember.ReadWrite.All", "User.Read.All" }))
                {
                    _logger.LogError("Nie można dodać użytkowników: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie AddUsersToTeamAsync (OBO)."
                    );
                    return new Dictionary<string, bool>();
                }

                // 2. Wywołanie PowerShell
                var psResults = await _powerShellBulkOps.BulkAddUsersToTeamAsync(
                    teamId, userUpns, "Member"
                );

                // 3. Synchronizacja lokalnej bazy (tylko dla sukcesów)
                var syncSuccesses = 0;
                var syncFailures = 0;
                var addedUsers = new List<string>();

                foreach (var kvp in psResults.Where(r => r.Value))
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
                var psSuccessCount = psResults.Count(r => r.Value);
                var psFailureCount = psResults.Count(r => !r.Value);
                
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

                // Invalidacja cache
                InvalidateCache(teamId: teamId, ownerUpn: team.Owner, invalidateAll: false);

                return psResults;
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

                // ZMIANA: Użycie nowej metody pomocniczej
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, new[] { "GroupMember.ReadWrite.All", "User.Read.All" }))
                {
                    _logger.LogError("Nie można usunąć użytkowników: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie RemoveUsersFromTeamAsync (OBO)."
                    );
                    return new Dictionary<string, bool>();
                }

                // 2. Wywołanie PowerShell
                var psResults = await _powerShellBulkOps.BulkRemoveUsersFromTeamAsync(
                    teamId, userUpns
                );

                // 3. Synchronizacja lokalnej bazy
                var syncSuccesses = 0;
                var syncFailures = 0;
                var removedUsers = new List<string>();

                foreach (var kvp in psResults.Where(r => r.Value))
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
                var psSuccessCount = psResults.Count(r => r.Value);
                var psFailureCount = psResults.Count(r => !r.Value);
                
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

                // Invalidacja cache
                InvalidateCache(teamId: teamId, ownerUpn: team.Owner, invalidateAll: false);

                return psResults;
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