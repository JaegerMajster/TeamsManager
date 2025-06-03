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
        private readonly IPowerShellService _powerShellService;
        private readonly ILogger<TeamService> _logger;
        private readonly IGenericRepository<SchoolType> _schoolTypeRepository;
        private readonly ISchoolYearRepository _schoolYearRepository;
        private readonly IMemoryCache _cache;
        private readonly IConfidentialClientApplication _confidentialClientApplication; // NOWE

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
            IPowerShellService powerShellService,
            ILogger<TeamService> logger,
            IGenericRepository<SchoolType> schoolTypeRepository,
            ISchoolYearRepository schoolYearRepository,
            IMemoryCache memoryCache,
            IConfidentialClientApplication confidentialClientApplication) // NOWE
        {
            _teamRepository = teamRepository ?? throw new ArgumentNullException(nameof(teamRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _teamMemberRepository = teamMemberRepository ?? throw new ArgumentNullException(nameof(teamMemberRepository));
            _teamTemplateRepository = teamTemplateRepository ?? throw new ArgumentNullException(nameof(teamTemplateRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _schoolTypeRepository = schoolTypeRepository ?? throw new ArgumentNullException(nameof(schoolTypeRepository));
            _schoolYearRepository = schoolYearRepository ?? throw new ArgumentNullException(nameof(schoolYearRepository));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _confidentialClientApplication = confidentialClientApplication ?? throw new ArgumentNullException(nameof(confidentialClientApplication)); // NOWE
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
                return await _powerShellService.ConnectWithAccessTokenAsync(authResult.AccessToken, scopes);
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
                        var psTeam = await _powerShellService.GetTeamAsync(teamId);
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
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation";
            var operation = new OperationHistory {/*...*/ Id = Guid.NewGuid().ToString(), Type = OperationType.TeamCreated, TargetEntityType = nameof(Team), TargetEntityName = displayName, CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();
            Team? newTeam = null;

            try
            {
                _logger.LogInformation("Rozpoczynanie tworzenia zespołu: '{DisplayName}' przez użytkownika {User}", displayName, currentUserUpn);
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    operation.MarkAsFailed("Nazwa wyświetlana zespołu nie może być pusta.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można utworzyć zespołu: Nazwa wyświetlana jest pusta.");
                    return null;
                }
                var ownerUser = await _userRepository.GetUserByUpnAsync(ownerUpn);
                if (ownerUser == null || !ownerUser.IsActive)
                {
                    operation.MarkAsFailed($"Użytkownik właściciela '{ownerUpn}' nie istnieje lub jest nieaktywny.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można utworzyć zespołu: Właściciel '{OwnerUPN}' nie istnieje lub jest nieaktywny.", ownerUpn);
                    return null;
                }

                // ZMIANA: Użycie nowej metody pomocniczej
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphReadWriteScopes))
                {
                    operation.MarkAsFailed("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie CreateTeamAsync (OBO).");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można utworzyć zespołu: Nie udało się połączyć z Microsoft Graph API (OBO).");
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
                operation.TargetEntityName = finalDisplayName;

                // Poprawione wywołanie
                string? externalTeamIdFromPS = await _powerShellService.Teams.CreateTeamAsync(finalDisplayName, description, ownerUser.UPN, visibility, template?.Template);
                bool psSuccess = !string.IsNullOrEmpty(externalTeamIdFromPS);

                if (psSuccess)
                {
                    _logger.LogInformation("Zespół '{FinalDisplayName}' pomyślnie utworzony w Microsoft Teams. External ID: {ExternalTeamId}", finalDisplayName, externalTeamIdFromPS);
                    newTeam = new Team { /* ... inicjalizacja pól ... */ Id = Guid.NewGuid().ToString(), DisplayName = finalDisplayName, Description = description, Owner = ownerUser.UPN, Status = TeamStatus.Active, Visibility = visibility, TemplateId = template?.Id, SchoolTypeId = schoolTypeId, SchoolYearId = schoolYearId, ExternalId = externalTeamIdFromPS, SchoolType = schoolType, SchoolYear = schoolYear, Template = template };
                    if (schoolYear != null) newTeam.AcademicYear = schoolYear.Name;
                    var ownerMembership = new TeamMember { Id = Guid.NewGuid().ToString(), UserId = ownerUser.Id, TeamId = newTeam.Id, Role = ownerUser.DefaultTeamRole, AddedDate = DateTime.UtcNow, AddedBy = currentUserUpn, IsActive = true, IsApproved = !newTeam.RequiresApproval, ApprovedDate = !newTeam.RequiresApproval ? DateTime.UtcNow : null, ApprovedBy = !newTeam.RequiresApproval ? currentUserUpn : null, User = ownerUser, Team = newTeam };
                    newTeam.Members.Add(ownerMembership);
                    await _teamRepository.AddAsync(newTeam);
                    operation.TargetEntityId = newTeam.Id;
                    operation.MarkAsCompleted($"Zespół ID: {newTeam.Id}, External ID: {externalTeamIdFromPS}");
                    _logger.LogInformation("Zespół '{FinalDisplayName}' pomyślnie utworzony i zapisany lokalnie. ID: {TeamId}", finalDisplayName, newTeam.Id);
                    InvalidateCache(teamId: newTeam.Id, ownerUpn: newTeam.Owner, newStatus: newTeam.Status, invalidateAll: true);
                    return newTeam;
                }
                else
                {
                    operation.MarkAsFailed("Nie udało się utworzyć zespołu w Microsoft Teams.");
                    _logger.LogError("Błąd tworzenia zespołu '{FinalDisplayName}' w Microsoft Teams.", finalDisplayName);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia zespołu {DisplayName}.", displayName);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return null;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> UpdateTeamAsync(Team teamToUpdate, string apiAccessToken) // ZMIANA: accessToken -> apiAccessToken
        {
            if (teamToUpdate == null || string.IsNullOrWhiteSpace(teamToUpdate.Id))
            {
                _logger.LogError("Próba aktualizacji zespołu z nieprawidłowymi danymi (null lub brak ID).");
                throw new ArgumentNullException(nameof(teamToUpdate));
            }
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory { /*...*/ Id = Guid.NewGuid().ToString(), Type = OperationType.TeamUpdated, TargetEntityType = nameof(Team), TargetEntityId = teamToUpdate.Id, CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie aktualizacji zespołu ID: {TeamId} przez {User}", teamToUpdate.Id, currentUserUpn);
            Team? existingTeam = null;
            string? oldOwnerUpn = null;
            TeamStatus? oldStatus = null;

            try
            {
                var teams = await _teamRepository.FindAsync(t => t.Id == teamToUpdate.Id);
                existingTeam = teams.FirstOrDefault();
                if (existingTeam == null)
                {
                    operation.MarkAsFailed($"Zespół o ID '{teamToUpdate.Id}' nie istnieje.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogWarning("Nie można zaktualizować zespołu ID {TeamId} - nie istnieje.", teamToUpdate.Id);
                    return false;
                }
                operation.TargetEntityName = existingTeam.DisplayName;
                oldOwnerUpn = existingTeam.Owner;
                oldStatus = existingTeam.Status;

                if (existingTeam.Owner != teamToUpdate.Owner)
                {
                    var newOwnerUser = await _userRepository.GetUserByUpnAsync(teamToUpdate.Owner);
                    if (newOwnerUser == null || !newOwnerUser.IsActive)
                    {
                        operation.MarkAsFailed($"Nowy właściciel '{teamToUpdate.Owner}' nie istnieje lub jest nieaktywny.");
                        await SaveOperationHistoryAsync(operation);
                        _logger.LogError("Nie można zaktualizować zespołu: Nowy właściciel '{NewOwnerUPN}' nie istnieje lub jest nieaktywny.", teamToUpdate.Owner);
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
                    operation.MarkAsFailed("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie UpdateTeamAsync (OBO).");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można zaktualizować zespołu: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    return false;
                }

                bool psSuccess = await _powerShellService.Teams.UpdateTeamPropertiesAsync(existingTeam.ExternalId ?? teamToUpdate.Id, teamToUpdate.DisplayName, teamToUpdate.Description, teamToUpdate.Visibility);
                if (psSuccess)
                {
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
                    operation.TargetEntityName = existingTeam.DisplayName;
                    operation.MarkAsCompleted($"Zespół ID: {existingTeam.Id} zaktualizowany.");
                    _logger.LogInformation("Zespół ID: {TeamId} pomyślnie zaktualizowany.", existingTeam.Id);
                    InvalidateCache(teamId: existingTeam.Id, ownerUpn: existingTeam.Owner, oldStatus: oldStatus, newStatus: existingTeam.Status, oldOwnerUpnIfChanged: oldOwnerUpn, invalidateAll: true);
                    return true;
                }
                else
                {
                    operation.MarkAsFailed("Błąd aktualizacji zespołu w Microsoft Teams.");
                    _logger.LogError("Błąd aktualizacji zespołu ID {TeamId} w Microsoft Teams.", existingTeam.Id);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji zespołu ID {TeamId}.", teamToUpdate.Id);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> ArchiveTeamAsync(string teamId, string reason, string apiAccessToken) // ZMIANA: accessToken -> apiAccessToken
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_archive";
            var operation = new OperationHistory { /*...*/ Id = Guid.NewGuid().ToString(), Type = OperationType.TeamArchived, TargetEntityType = nameof(Team), TargetEntityId = teamId, CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();
            Team? team = null;
            try
            {
                var teams = await _teamRepository.FindAsync(t => t.Id == teamId);
                team = teams.FirstOrDefault();
                if (team == null) { /*...*/ operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje."); await SaveOperationHistoryAsync(operation); _logger.LogWarning("Nie można zarchiwizować zespołu ID {TeamId} - nie istnieje.", teamId); return false; }
                operation.TargetEntityName = team.GetBaseDisplayName();
                if (team.Status == TeamStatus.Archived) { /*...*/ operation.MarkAsCompleted($"Zespół '{team.DisplayName}' (ID: {team.Id}) był już zarchiwizowany."); await SaveOperationHistoryAsync(operation); _logger.LogInformation("Zespół ID {TeamId} był już zarchiwizowany.", teamId); InvalidateCache(teamId: teamId, ownerUpn: team.Owner, oldStatus: TeamStatus.Archived, newStatus: TeamStatus.Archived, invalidateAll: true); return true; }

                // ZMIANA: Użycie nowej metody pomocniczej
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphReadWriteScopes))
                {
                    operation.MarkAsFailed("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie ArchiveTeamAsync (OBO).");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można zarchiwizować zespołu: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    return false;
                }

                bool psSuccess = await _powerShellService.Teams.ArchiveTeamAsync(team.ExternalId ?? team.Id);
                if (psSuccess)
                {
                    var oldStatus = team.Status;
                    team.Archive(reason, currentUserUpn);
                    _teamRepository.Update(team);
                    operation.OperationDetails = $"Zespół '{team.GetBaseDisplayName()}' zarchiwizowany jako '{team.DisplayName}'. Powód: {reason}";
                    operation.MarkAsCompleted(operation.OperationDetails);
                    _logger.LogInformation("Zespół ID {TeamId} pomyślnie zarchiwizowany.", teamId);
                    InvalidateCache(teamId: teamId, ownerUpn: team.Owner, oldStatus: oldStatus, newStatus: team.Status, invalidateAll: true);
                    return true;
                }
                else { /*...*/ operation.MarkAsFailed("Błąd archiwizacji zespołu w Microsoft Teams."); await SaveOperationHistoryAsync(operation); _logger.LogError("Błąd archiwizacji zespołu ID {TeamId} w Microsoft Teams.", teamId); return false; }
            }
            catch (Exception ex) { /*...*/ _logger.LogError(ex, "Krytyczny błąd podczas archiwizacji zespołu ID {TeamId}.", teamId); operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return false; }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        public async Task<bool> RestoreTeamAsync(string teamId, string apiAccessToken) // ZMIANA: accessToken -> apiAccessToken
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_restore";
            var operation = new OperationHistory { /*...*/ Id = Guid.NewGuid().ToString(), Type = OperationType.TeamUnarchived, TargetEntityType = nameof(Team), TargetEntityId = teamId, CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();
            Team? team = null;
            try
            {
                var teams = await _teamRepository.FindAsync(t => t.Id == teamId);
                team = teams.FirstOrDefault();
                if (team == null) { /*...*/ operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje."); await SaveOperationHistoryAsync(operation); _logger.LogWarning("Nie można przywrócić zespołu ID {TeamId} - nie istnieje.", teamId); return false; }
                operation.TargetEntityName = team.GetBaseDisplayName();
                if (team.Status == TeamStatus.Active) { /*...*/ operation.MarkAsCompleted($"Zespół '{team.DisplayName}' (ID: {team.Id}) był już aktywny."); await SaveOperationHistoryAsync(operation); _logger.LogInformation("Zespół ID {TeamId} był już aktywny.", teamId); InvalidateCache(teamId: teamId, ownerUpn: team.Owner, oldStatus: TeamStatus.Active, newStatus: TeamStatus.Active, invalidateAll: true); return true; }

                // ZMIANA: Użycie nowej metody pomocniczej
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphReadWriteScopes))
                {
                    operation.MarkAsFailed("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie RestoreTeamAsync (OBO).");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można przywrócić zespołu: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    return false;
                }

                bool psSuccess = await _powerShellService.Teams.UnarchiveTeamAsync(team.ExternalId ?? team.Id);
                if (psSuccess)
                {
                    var oldStatus = team.Status;
                    team.Restore(currentUserUpn);
                    _teamRepository.Update(team);
                    operation.TargetEntityName = team.DisplayName;
                    operation.MarkAsCompleted($"Zespół '{team.DisplayName}' (ID: {team.Id}) przywrócony.");
                    _logger.LogInformation("Zespół ID {TeamId} pomyślnie przywrócony.", teamId);
                    InvalidateCache(teamId: teamId, ownerUpn: team.Owner, oldStatus: oldStatus, newStatus: team.Status, invalidateAll: true);
                    return true;
                }
                else { /*...*/ operation.MarkAsFailed("Błąd przywracania zespołu w Microsoft Teams."); await SaveOperationHistoryAsync(operation); _logger.LogError("Błąd przywracania zespołu ID {TeamId} w Microsoft Teams.", teamId); return false; }
            }
            catch (Exception ex) { /*...*/ _logger.LogError(ex, "Krytyczny błąd podczas przywracania zespołu ID {TeamId}.", teamId); operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return false; }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        public async Task<bool> DeleteTeamAsync(string teamId, string apiAccessToken) // ZMIANA: accessToken -> apiAccessToken
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete";
            var operation = new OperationHistory { /*...*/ Id = Guid.NewGuid().ToString(), Type = OperationType.TeamDeleted, TargetEntityType = nameof(Team), TargetEntityId = teamId, CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie usuwania zespołu ID: {TeamId} przez {User}", teamId, currentUserUpn);
            Team? team = null;
            try
            {
                var teams = await _teamRepository.FindAsync(t => t.Id == teamId);
                team = teams.FirstOrDefault();
                if (team == null) { /*...*/ operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje."); await SaveOperationHistoryAsync(operation); _logger.LogWarning("Nie można usunąć zespołu ID {TeamId} - nie istnieje.", teamId); return false; }
                operation.TargetEntityName = team.GetBaseDisplayName();

                // ZMIANA: Użycie nowej metody pomocniczej
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphReadWriteScopes)) // Potrzebne Group.ReadWrite.All do usunięcia
                {
                    operation.MarkAsFailed("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie DeleteTeamAsync (OBO).");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można usunąć zespołu: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    return false;
                }

                bool psSuccess = await _powerShellService.Teams.DeleteTeamAsync(team.ExternalId ?? team.Id);
                if (psSuccess)
                {
                    var oldStatus = team.Status;
                    // W przypadku rzeczywistego usunięcia z Graph, lokalnie oznaczamy jako zarchiwizowany/usunięty, aby zachować spójność i historię
                    team.Archive($"Usunięty przez {currentUserUpn} (operacja DeleteTeamAsync)", currentUserUpn);
                    _teamRepository.Update(team);
                    operation.MarkAsCompleted($"Zespół '{team.DisplayName}' (ID: {teamId}) usunięty z Microsoft Teams (lokalnie zarchiwizowany).");
                    _logger.LogInformation("Zespół ID {TeamId} pomyślnie usunięty z Microsoft Teams (lokalnie zarchiwizowany).", teamId);
                    InvalidateCache(teamId: teamId, ownerUpn: team.Owner, oldStatus: oldStatus, newStatus: team.Status, invalidateAll: true);
                    return true;
                }
                else { /*...*/ operation.MarkAsFailed("Błąd usuwania zespołu w Microsoft Teams."); await SaveOperationHistoryAsync(operation); _logger.LogError("Błąd usuwania zespołu ID {TeamId} w Microsoft Teams.", teamId); return false; }
            }
            catch (Exception ex) { /*...*/ _logger.LogError(ex, "Krytyczny błąd podczas usuwania zespołu ID {TeamId}.", teamId); operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return false; }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        public async Task<TeamMember?> AddMemberAsync(string teamId, string userUpn, TeamMemberRole role, string apiAccessToken) // ZMIANA: accessToken -> apiAccessToken
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_add_member";
            var operation = new OperationHistory { /*...*/ Id = Guid.NewGuid().ToString(), Type = OperationType.MemberAdded, CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();
            Team? team = null;
            User? user = null;
            try
            {
                _logger.LogInformation("Dodawanie użytkownika {UserUPN} do zespołu ID {TeamId} z rolą {Role} przez {CurrentUser}", userUpn, teamId, role, currentUserUpn);
                team = await GetTeamByIdAsync(teamId, apiAccessToken: apiAccessToken); // Przekazanie apiAccessToken, aby GetTeamByIdAsync mogło potencjalnie użyć Graph
                if (team == null) { /*...*/ operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje lub nie jest aktywny (status)."); await SaveOperationHistoryAsync(operation); _logger.LogWarning("Nie można dodać członka: Zespół o ID {TeamId} nie istnieje lub nie jest aktywny (status).", teamId); return null; }
                operation.TargetEntityName = $"Członek {userUpn} do zespołu {team.DisplayName}";
                user = await _userRepository.GetUserByUpnAsync(userUpn);
                if (user == null || !user.IsActive) { /*...*/ operation.MarkAsFailed($"Użytkownik o UPN '{userUpn}' nie istnieje lub jest nieaktywny."); await SaveOperationHistoryAsync(operation); _logger.LogWarning("Nie można dodać członka: Użytkownik o UPN {UserUPN} nie istnieje lub jest nieaktywny.", userUpn); return null; }
                if (team.HasMember(user.Id)) { /*...*/ operation.MarkAsFailed($"Użytkownik '{userUpn}' jest już aktywnym członkiem zespołu '{team.DisplayName}'."); await SaveOperationHistoryAsync(operation); _logger.LogWarning("Nie można dodać członka: Użytkownik {UserUPN} jest już aktywnym członkiem zespołu {TeamDisplayName}.", userUpn, team.DisplayName); return team.GetMembership(user.Id); }
                if (!team.CanAddMoreMembers()) { /*...*/ operation.MarkAsFailed($"Zespół '{team.DisplayName}' osiągnął maksymalną liczbę członków."); await SaveOperationHistoryAsync(operation); _logger.LogWarning("Nie można dodać członka: Zespół {TeamDisplayName} osiągnął maksymalną liczbę członków.", team.DisplayName); return null; }

                // ZMIANA: Użycie nowej metody pomocniczej
                // Zakresy dla dodawania członka
                var memberAddScopes = new[] { "GroupMember.ReadWrite.All", "User.Read.All" }; // User.Read.All do weryfikacji UPN jeśli PowerShellService tego wymaga
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, memberAddScopes))
                {
                    operation.MarkAsFailed("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie AddMemberAsync (OBO).");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można dodać członka: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    return null;
                }

                bool psSuccess = await _powerShellService.Users.AddUserToTeamAsync(team.ExternalId ?? team.Id, user.UPN, role.ToString());
                if (psSuccess)
                {
                    var newMember = new TeamMember { /* ... inicjalizacja pól ... */ Id = Guid.NewGuid().ToString(), UserId = user.Id, TeamId = team.Id, Role = role, AddedDate = DateTime.UtcNow, AddedBy = currentUserUpn, IsActive = true, IsApproved = !team.RequiresApproval, ApprovedDate = !team.RequiresApproval ? DateTime.UtcNow : null, ApprovedBy = !team.RequiresApproval ? currentUserUpn : null, User = user, Team = team };
                    await _teamMemberRepository.AddAsync(newMember);
                    operation.TargetEntityId = newMember.Id;
                    operation.MarkAsCompleted($"Użytkownik '{userUpn}' dodany do zespołu '{team.DisplayName}' jako {role}.");
                    _logger.LogInformation("Użytkownik {UserUPN} pomyślnie dodany do zespołu {TeamDisplayName}.", userUpn, team.DisplayName);
                    InvalidateCache(teamId: teamId, ownerUpn: team.Owner, invalidateAll: false);
                    return newMember;
                }
                else { /*...*/ operation.MarkAsFailed("Błąd dodawania członka do zespołu w Microsoft Teams."); await SaveOperationHistoryAsync(operation); _logger.LogError("Błąd dodawania użytkownika {UserUPN} do zespołu {TeamDisplayName} w Microsoft Teams.", userUpn, team.DisplayName); return null; }
            }
            catch (Exception ex) { /*...*/ _logger.LogError(ex, "Krytyczny błąd podczas dodawania członka {UserUPN} do zespołu {TeamId}.", userUpn, teamId); operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return null; }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        public async Task<bool> RemoveMemberAsync(string teamId, string userId, string apiAccessToken) // ZMIANA: accessToken -> apiAccessToken
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_member";
            var operation = new OperationHistory { /*...*/ Id = Guid.NewGuid().ToString(), Type = OperationType.MemberRemoved, CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();
            Team? team = null;
            try
            {
                _logger.LogInformation("Usuwanie użytkownika ID {UserId} z zespołu ID {TeamId} przez {CurrentUser}", userId, teamId, currentUserUpn);
                team = await GetTeamByIdAsync(teamId, apiAccessToken: apiAccessToken);
                if (team == null) { /*...*/ operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje lub nie jest aktywny (status)."); await SaveOperationHistoryAsync(operation); _logger.LogWarning("Nie można usunąć członka: Zespół o ID {TeamId} nie istnieje lub nie jest aktywny (status).", teamId); return false; }
                var memberToRemove = team.GetMembership(userId);
                if (memberToRemove == null) { /*...*/ operation.MarkAsFailed($"Użytkownik o ID '{userId}' nie jest (aktywnym) członkiem zespołu '{team.DisplayName}'."); await SaveOperationHistoryAsync(operation); _logger.LogWarning("Nie można usunąć członka: Użytkownik ID {UserId} nie jest aktywnym członkiem zespołu {TeamDisplayName}.", userId, team.DisplayName); return false; }
                operation.TargetEntityId = memberToRemove.Id;
                operation.TargetEntityName = $"{memberToRemove.User?.UPN ?? userId} z {team.DisplayName}";
                if (memberToRemove.Role == TeamMemberRole.Owner && team.OwnerCount <= 1) { /*...*/ operation.MarkAsFailed("Nie można usunąć ostatniego właściciela zespołu."); await SaveOperationHistoryAsync(operation); _logger.LogWarning("Nie można usunąć członka: Użytkownik ID {UserId} jest ostatnim właścicielem zespołu {TeamDisplayName}.", userId, team.DisplayName); return false; }

                // ZMIANA: Użycie nowej metody pomocniczej
                var memberRemoveScopes = new[] { "GroupMember.ReadWrite.All", "User.Read.All" };
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, memberRemoveScopes))
                {
                    operation.MarkAsFailed("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie RemoveMemberAsync (OBO).");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można usunąć członka: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    return false;
                }

                // Poprawione wywołanie
                bool psSuccess = await _powerShellService.Users.RemoveUserFromTeamAsync(team.ExternalId ?? team.Id, memberToRemove.User!.UPN);
                if (psSuccess)
                {
                    memberToRemove.RemoveFromTeam("Usunięty przez serwis", currentUserUpn);
                    _teamMemberRepository.Update(memberToRemove);
                    operation.MarkAsCompleted($"Użytkownik '{memberToRemove.User?.UPN}' usunięty z zespołu '{team.DisplayName}'.");
                    _logger.LogInformation("Użytkownik ID {UserId} pomyślnie usunięty z zespołu {TeamDisplayName}.", userId, team.DisplayName);
                    InvalidateCache(teamId: teamId, ownerUpn: team.Owner, invalidateAll: false);
                    return true;
                }
                else { /*...*/ operation.MarkAsFailed("Błąd usuwania członka z zespołu w Microsoft Teams."); await SaveOperationHistoryAsync(operation); _logger.LogError("Błąd usuwania użytkownika ID {UserId} z zespołu {TeamDisplayName} w Microsoft Teams.", userId, team.DisplayName); return false; }
            }
            catch (Exception ex) { /*...*/ _logger.LogError(ex, "Krytyczny błąd podczas usuwania członka ID {UserId} z zespołu ID {TeamId}.", userId, teamId); operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return false; }
            finally { await SaveOperationHistoryAsync(operation); }
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

        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id)) operation.Id = Guid.NewGuid().ToString();
            if (string.IsNullOrEmpty(operation.CreatedBy))
                operation.CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system_log_save";

            if (operation.StartedAt == default(DateTime) &&
                (operation.Status == OperationStatus.InProgress || operation.Status == OperationStatus.Pending || operation.Status == OperationStatus.Completed || operation.Status == OperationStatus.Failed || operation.Status == OperationStatus.Cancelled || operation.Status == OperationStatus.PartialSuccess))
            {
                if (operation.StartedAt == default(DateTime)) operation.StartedAt = DateTime.UtcNow;
                if (operation.Status == OperationStatus.Completed || operation.Status == OperationStatus.Failed || operation.Status == OperationStatus.Cancelled || operation.Status == OperationStatus.PartialSuccess)
                {
                    if (!operation.CompletedAt.HasValue) operation.CompletedAt = DateTime.UtcNow;
                    if (!operation.Duration.HasValue && operation.CompletedAt.HasValue && operation.StartedAt != default(DateTime))
                    {
                        operation.Duration = operation.CompletedAt.Value - operation.StartedAt;
                    }
                }
            }
            await _operationHistoryRepository.AddAsync(operation);
            _logger.LogDebug("Zapisano nowy wpis historii operacji ID: {OperationId} dla zespołu.", operation.Id);
        }
    }
}