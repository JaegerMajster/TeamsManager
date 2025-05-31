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

        // Klucze cache
        private const string AllActiveTeamsCacheKey = "Teams_AllActive"; // Klucz dla GetAllTeamsAsync (które zwraca aktywne)
        private const string ActiveTeamsSpecificCacheKey = "Teams_Active"; // Dedykowany klucz dla GetActiveTeamsAsync
        private const string ArchivedTeamsCacheKey = "Teams_Archived";
        private const string TeamsByOwnerCacheKeyPrefix = "Teams_ByOwner_";
        private const string TeamByIdCacheKeyPrefix = "Team_Id_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(15);

        // Token do unieważniania cache'u dla zespołów
        private static CancellationTokenSource _teamsCacheTokenSource = new CancellationTokenSource();

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
            IMemoryCache memoryCache)
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
        }

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_teamsCacheTokenSource.Token));
        }

        /// <inheritdoc />
        public async Task<Team?> CreateTeamAsync(
            string displayName,
            string description,
            string ownerUpn,
            TeamVisibility visibility,
            string? teamTemplateId = null,
            string? schoolTypeId = null,
            string? schoolYearId = null,
            Dictionary<string, string>? additionalTemplateValues = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.TeamCreated,
                TargetEntityType = nameof(Team),
                TargetEntityName = displayName, // Początkowa nazwa, może ulec zmianie przez szablon
                CreatedBy = currentUserUpn,
                IsActive = true
            };
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
                        // Aktualizacja szablonu w repozytorium, aby zapisać UsageCount i LastUsedDate
                        _teamTemplateRepository.Update(template);
                        _logger.LogInformation("Nazwa zespołu wygenerowana z szablonu '{TemplateName}': {FinalDisplayName}", template.Name, finalDisplayName);
                    }
                    else
                    {
                        _logger.LogWarning("Szablon o ID {TemplateId} nie istnieje lub jest nieaktywny. Użycie oryginalnej nazwy: {OriginalName}", teamTemplateId, displayName);
                    }
                }
                operation.TargetEntityName = finalDisplayName;

                string? externalTeamIdFromPS = await _powerShellService.CreateTeamAsync(
                    finalDisplayName,
                    description,
                    ownerUser.UPN,
                    visibility,
                    template?.Id // Lub inna właściwość szablonu, jeśli PowerShell tego wymaga
                );
                bool psSuccess = !string.IsNullOrEmpty(externalTeamIdFromPS);

                if (psSuccess)
                {
                    _logger.LogInformation("Zespół '{FinalDisplayName}' pomyślnie utworzony w Microsoft Teams. External ID: {ExternalTeamId}", finalDisplayName, externalTeamIdFromPS);
                    newTeam = new Team
                    {
                        Id = Guid.NewGuid().ToString(),
                        DisplayName = finalDisplayName,
                        Description = description,
                        Owner = ownerUser.UPN,
                        Status = TeamStatus.Active,
                        Visibility = visibility,
                        TemplateId = template?.Id,
                        SchoolTypeId = schoolTypeId,
                        SchoolYearId = schoolYearId,
                        ExternalId = externalTeamIdFromPS,
                        CreatedBy = currentUserUpn,
                        IsActive = true,
                        SchoolType = schoolType, // Dla spójności obiektu
                        SchoolYear = schoolYear, // Dla spójności obiektu
                        Template = template      // Dla spójności obiektu
                    };
                    if (schoolYear != null) newTeam.AcademicYear = schoolYear.Name;

                    var ownerMembership = new TeamMember
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = ownerUser.Id,
                        TeamId = newTeam.Id,
                        Role = ownerUser.DefaultTeamRole,
                        AddedDate = DateTime.UtcNow,
                        AddedBy = currentUserUpn,
                        IsActive = true,
                        IsApproved = true,
                        CreatedBy = currentUserUpn,
                        User = ownerUser,
                        Team = newTeam
                    };
                    newTeam.Members.Add(ownerMembership);

                    await _teamRepository.AddAsync(newTeam);

                    operation.TargetEntityId = newTeam.Id;
                    operation.MarkAsCompleted($"Zespół ID: {newTeam.Id}, External ID: {externalTeamIdFromPS}");
                    _logger.LogInformation("Zespół '{FinalDisplayName}' pomyślnie utworzony i zapisany lokalnie. ID: {TeamId}", finalDisplayName, newTeam.Id);

                    InvalidateCache(newTeam.Id, newTeam.Owner, newStatus: newTeam.Status, allTeamsAffected: true);
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

        /// <inheritdoc />
        public async Task<IEnumerable<Team>> GetAllTeamsAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych zespołów. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);
            // Obecna implementacja _teamRepository.FindAsync(t => t.IsActive) zwraca tylko aktywne.
            // Jeśli "GetAllTeamsAsync" ma oznaczać *wszystkie* (nie tylko aktywne rekordy), predykat powinien być inny
            // lub metoda w repozytorium powinna być dedykowana. Na razie trzymamy się logiki "aktywne rekordy".
            string cacheKey = AllActiveTeamsCacheKey;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<Team>? cachedTeams) && cachedTeams != null)
            {
                _logger.LogDebug("Wszystkie aktywne zespoły znalezione w cache.");
                return cachedTeams;
            }

            _logger.LogDebug("Wszystkie aktywne zespoły nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var teamsFromDb = await _teamRepository.FindAsync(t => t.IsActive); // Filtruje po BaseEntity.IsActive

            _cache.Set(cacheKey, teamsFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Wszystkie aktywne zespoły dodane do cache.");
            return teamsFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Team>> GetActiveTeamsAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie zespołów o statusie 'Active'. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);
            string cacheKey = ActiveTeamsSpecificCacheKey;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<Team>? cachedTeams) && cachedTeams != null)
            {
                _logger.LogDebug("Zespoły o statusie 'Active' znalezione w cache.");
                return cachedTeams;
            }

            _logger.LogDebug("Zespoły o statusie 'Active' nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var teamsFromDb = await _teamRepository.GetActiveTeamsAsync(); // Metoda z repozytorium

            _cache.Set(cacheKey, teamsFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Zespoły o statusie 'Active' dodane do cache.");
            return teamsFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Team>> GetArchivedTeamsAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie zespołów o statusie 'Archived'. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);
            string cacheKey = ArchivedTeamsCacheKey;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<Team>? cachedTeams) && cachedTeams != null)
            {
                _logger.LogDebug("Zespoły o statusie 'Archived' znalezione w cache.");
                return cachedTeams;
            }

            _logger.LogDebug("Zespoły o statusie 'Archived' nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var teamsFromDb = await _teamRepository.GetArchivedTeamsAsync(); // Metoda z repozytorium

            _cache.Set(cacheKey, teamsFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Zespoły o statusie 'Archived' dodane do cache.");
            return teamsFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Team>> GetTeamsByOwnerAsync(string ownerUpn, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie zespołów dla właściciela: {OwnerUpn}. Wymuszenie odświeżenia: {ForceRefresh}", ownerUpn, forceRefresh);
            if (string.IsNullOrWhiteSpace(ownerUpn))
            {
                _logger.LogWarning("Próba pobrania zespołów dla pustego UPN właściciela.");
                return Enumerable.Empty<Team>();
            }
            string cacheKey = TeamsByOwnerCacheKeyPrefix + ownerUpn;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<Team>? cachedTeams) && cachedTeams != null)
            {
                _logger.LogDebug("Zespoły dla właściciela {OwnerUpn} znalezione w cache.", ownerUpn);
                return cachedTeams;
            }

            _logger.LogDebug("Zespoły dla właściciela {OwnerUpn} nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", ownerUpn);
            var teamsFromDb = await _teamRepository.GetTeamsByOwnerAsync(ownerUpn);

            _cache.Set(cacheKey, teamsFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Zespoły dla właściciela {OwnerUpn} dodane do cache.", ownerUpn);
            return teamsFromDb;
        }

        /// <inheritdoc />
        public async Task<Team?> GetTeamByIdAsync(string teamId, bool includeMembers = false, bool includeChannels = false, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie zespołu o ID: {TeamId}. Dołączanie członków: {IncludeMembers}, Dołączanie kanałów: {IncludeChannels}, Wymuszenie odświeżenia: {ForceRefresh}", teamId, includeMembers, includeChannels, forceRefresh);
            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogWarning("Próba pobrania zespołu z pustym ID.");
                return null;
            }

            // Klucz cache będzie zawsze ten sam dla danego ID, niezależnie od flag include*.
            // Repozytorium TeamRepository.GetByIdAsync domyślnie dołącza Members.User i Channels.
            string cacheKey = TeamByIdCacheKeyPrefix + teamId;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out Team? cachedTeam))
            {
                _logger.LogDebug("Zespół ID: {TeamId} znaleziony w cache.", teamId);
                // Jeśli cache'owany obiekt nie zawierałby domyślnie Members/Channels, tutaj byłaby logika ich dociągania.
                // Ale TeamRepository.GetByIdAsync je dołącza, więc obiekt w cache powinien być kompletny.
                return cachedTeam;
            }

            _logger.LogDebug("Zespół ID: {TeamId} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", teamId);
            var teamFromDb = await _teamRepository.GetByIdAsync(teamId);

            if (teamFromDb != null)
            {
                _cache.Set(cacheKey, teamFromDb, GetDefaultCacheEntryOptions());
                _logger.LogDebug("Zespół ID: {TeamId} dodany do cache.", teamId);
            }
            else
            {
                _cache.Remove(cacheKey);
            }
            return teamFromDb;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateTeamAsync(Team teamToUpdate)
        {
            if (teamToUpdate == null || string.IsNullOrWhiteSpace(teamToUpdate.Id))
            {
                _logger.LogError("Próba aktualizacji zespołu z nieprawidłowymi danymi (null lub brak ID).");
                throw new ArgumentNullException(nameof(teamToUpdate));
            }


            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.TeamUpdated,
                TargetEntityType = nameof(Team),
                TargetEntityId = teamToUpdate.Id,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie aktualizacji zespołu ID: {TeamId} przez {User}", teamToUpdate.Id, currentUserUpn);

            Team? existingTeam = null;
            string? oldOwnerUpn = null;
            TeamStatus? oldStatus = null;

            try
            {
                existingTeam = await _teamRepository.GetByIdAsync(teamToUpdate.Id);
                if (existingTeam == null || !existingTeam.IsActive) // Sprawdzamy też IsActive rekordu
                {
                    operation.MarkAsFailed($"Zespół o ID '{teamToUpdate.Id}' nie istnieje lub jest nieaktywny (rekord).");
                    _logger.LogWarning("Nie można zaktualizować zespołu ID {TeamId} - nie istnieje lub jest nieaktywny (rekord).", teamToUpdate.Id);
                    return false;
                }
                operation.TargetEntityName = existingTeam.DisplayName; // Nazwa przed aktualizacją
                oldOwnerUpn = existingTeam.Owner;
                oldStatus = existingTeam.Status;

                // TODO: PowerShellService call - Aktualizacja w Microsoft Teams
                // bool psSuccess = await _powerShellService.UpdateTeamPropertiesAsync(existingTeam.ExternalId, teamToUpdate.DisplayName, teamToUpdate.Description);
                bool psSuccess = true;

                if (psSuccess)
                {
                    existingTeam.DisplayName = teamToUpdate.DisplayName;
                    existingTeam.Description = teamToUpdate.Description;
                    existingTeam.Owner = teamToUpdate.Owner;
                    existingTeam.SchoolTypeId = teamToUpdate.SchoolTypeId;
                    existingTeam.SchoolYearId = teamToUpdate.SchoolYearId;
                    existingTeam.TemplateId = teamToUpdate.TemplateId;
                    existingTeam.Visibility = teamToUpdate.Visibility;
                    existingTeam.RequiresApproval = teamToUpdate.RequiresApproval;
                    // Należy zaktualizować inne potrzebne właściwości
                    existingTeam.MarkAsModified(currentUserUpn);

                    _teamRepository.Update(existingTeam);

                    operation.TargetEntityName = existingTeam.DisplayName; // Nazwa po aktualizacji
                    operation.MarkAsCompleted($"Zespół ID: {existingTeam.Id} zaktualizowany.");
                    _logger.LogInformation("Zespół ID: {TeamId} pomyślnie zaktualizowany.", existingTeam.Id);

                    InvalidateCache(existingTeam.Id, existingTeam.Owner, oldStatus, existingTeam.Status, allTeamsAffected: true);
                    if (oldOwnerUpn != null && oldOwnerUpn != existingTeam.Owner)
                    {
                        InvalidateCache(ownerUpn: oldOwnerUpn, allTeamsAffected: true); // Inwaliduj też dla starego właściciela
                    }
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

        /// <inheritdoc />
        public async Task<bool> ArchiveTeamAsync(string teamId, string reason)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_archive";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.TeamArchived,
                TargetEntityType = nameof(Team),
                TargetEntityId = teamId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            Team? team = null;
            try
            {
                team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null)
                {
                    operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje.");
                    _logger.LogWarning("Nie można zarchiwizować zespołu ID {TeamId} - nie istnieje.", teamId);
                    return false;
                }
                operation.TargetEntityName = team.DisplayName;

                if (team.Status == TeamStatus.Archived)
                {
                    operation.MarkAsCompleted($"Zespół '{team.DisplayName}' (ID: {team.Id}) był już zarchiwizowany.");
                    _logger.LogInformation("Zespół ID {TeamId} był już zarchiwizowany.", teamId);
                    InvalidateCache(teamId, team.Owner, oldStatus: TeamStatus.Archived, newStatus: TeamStatus.Archived, allTeamsAffected: true);
                    return true;
                }

                // TODO: PowerShellService call - Archiwizacja w Microsoft Teams
                bool psSuccess = await _powerShellService.ArchiveTeamAsync(team.ExternalId ?? team.Id);

                if (psSuccess)
                {
                    var oldDisplayName = team.DisplayName; // Zapisz starą nazwę przed modyfikacją
                    team.Archive(reason, currentUserUpn);
                    _teamRepository.Update(team);

                    operation.TargetEntityName = oldDisplayName; // Logujemy oryginalną nazwę
                    operation.OperationDetails = $"Zespół '{oldDisplayName}' zarchiwizowany jako '{team.DisplayName}'. Powód: {reason}";
                    operation.MarkAsCompleted(operation.OperationDetails);
                    _logger.LogInformation("Zespół ID {TeamId} pomyślnie zarchiwizowany.", teamId);

                    InvalidateCache(teamId, team.Owner, oldStatus: TeamStatus.Active, newStatus: TeamStatus.Archived, allTeamsAffected: true);
                    return true;
                }
                else
                {
                    operation.MarkAsFailed("Błąd archiwizacji zespołu w Microsoft Teams.");
                    _logger.LogError("Błąd archiwizacji zespołu ID {TeamId} w Microsoft Teams.", teamId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas archiwizacji zespołu ID {TeamId}.", teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> RestoreTeamAsync(string teamId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_restore";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.TeamUnarchived,
                TargetEntityType = nameof(Team),
                TargetEntityId = teamId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            Team? team = null;
            try
            {
                team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null)
                {
                    operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje.");
                    _logger.LogWarning("Nie można przywrócić zespołu ID {TeamId} - nie istnieje.", teamId);
                    return false;
                }

                string originalNameBeforeArchiveAttempt = team.DisplayName.StartsWith("ARCHIWALNY - ")
                    ? team.DisplayName.Substring("ARCHIWALNY - ".Length)
                    : team.DisplayName;
                operation.TargetEntityName = originalNameBeforeArchiveAttempt;


                if (team.Status == TeamStatus.Active)
                {
                    operation.MarkAsCompleted($"Zespół '{team.DisplayName}' (ID: {team.Id}) był już aktywny.");
                    _logger.LogInformation("Zespół ID {TeamId} był już aktywny.", teamId);
                    InvalidateCache(teamId, team.Owner, oldStatus: TeamStatus.Active, newStatus: TeamStatus.Active, allTeamsAffected: true);
                    return true;
                }

                // TODO: PowerShellService call - Przywrócenie w Microsoft Teams
                bool psSuccess = await _powerShellService.UnarchiveTeamAsync(team.ExternalId ?? team.Id);

                if (psSuccess)
                {
                    team.Restore(currentUserUpn);
                    _teamRepository.Update(team);

                    operation.TargetEntityName = team.DisplayName; // Nazwa po przywróceniu
                    operation.MarkAsCompleted($"Zespół '{team.DisplayName}' (ID: {team.Id}) przywrócony.");
                    _logger.LogInformation("Zespół ID {TeamId} pomyślnie przywrócony.", teamId);

                    InvalidateCache(teamId, team.Owner, oldStatus: TeamStatus.Archived, newStatus: TeamStatus.Active, allTeamsAffected: true);
                    return true;
                }
                else
                {
                    operation.MarkAsFailed("Błąd przywracania zespołu w Microsoft Teams.");
                    _logger.LogError("Błąd przywracania zespołu ID {TeamId} w Microsoft Teams.", teamId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas przywracania zespołu ID {TeamId}.", teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteTeamAsync(string teamId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.TeamDeleted,
                TargetEntityType = nameof(Team),
                TargetEntityId = teamId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie usuwania zespołu ID: {TeamId} przez {User}", teamId, currentUserUpn);
            Team? team = null;
            try
            {
                team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null)
                {
                    operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje.");
                    _logger.LogWarning("Nie można usunąć zespołu ID {TeamId} - nie istnieje.", teamId);
                    return false;
                }
                operation.TargetEntityName = team.DisplayName;

                // TODO: PowerShellService call - Usunięcie zespołu w Microsoft Teams
                bool psSuccess = await _powerShellService.DeleteTeamAsync(team.ExternalId ?? team.Id);

                if (psSuccess)
                {
                    var oldStatus = team.Status;
                    team.MarkAsDeleted(currentUserUpn);
                    _teamRepository.Update(team);

                    operation.MarkAsCompleted($"Zespół '{team.DisplayName}' (ID: {teamId}) oznaczony jako usunięty.");
                    _logger.LogInformation("Zespół ID {TeamId} pomyślnie oznaczony jako usunięty.", teamId);
                    InvalidateCache(teamId, team.Owner, oldStatus: oldStatus, allTeamsAffected: true);
                    return true;
                }
                else
                {
                    operation.MarkAsFailed("Błąd usuwania zespołu w Microsoft Teams.");
                    _logger.LogError("Błąd usuwania zespołu ID {TeamId} w Microsoft Teams.", teamId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania zespołu ID {TeamId}.", teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<TeamMember?> AddMemberAsync(string teamId, string userUpn, TeamMemberRole role)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_add_member";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.MemberAdded,
                TargetEntityType = nameof(TeamMember),
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            Team? team = null;
            User? user = null;

            try
            {
                _logger.LogInformation("Dodawanie użytkownika {UserUPN} do zespołu ID {TeamId} z rolą {Role} przez {CurrentUser}", userUpn, teamId, role, currentUserUpn);

                team = await _teamRepository.GetByIdAsync(teamId); // Powinno dołączyć Members
                if (team == null || !team.IsActive)
                {
                    operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje lub jest nieaktywny.");
                    _logger.LogWarning("Nie można dodać członka: Zespół o ID {TeamId} nie istnieje lub jest nieaktywny.", teamId);
                    return null;
                }
                operation.TargetEntityName = $"Członek {userUpn} do zespołu {team.DisplayName}";

                user = await _userRepository.GetUserByUpnAsync(userUpn);
                if (user == null || !user.IsActive)
                {
                    operation.MarkAsFailed($"Użytkownik o UPN '{userUpn}' nie istnieje lub jest nieaktywny.");
                    _logger.LogWarning("Nie można dodać członka: Użytkownik o UPN {UserUPN} nie istnieje lub jest nieaktywny.", userUpn);
                    return null;
                }

                if (team.HasMember(user.Id))
                {
                    operation.MarkAsFailed($"Użytkownik '{userUpn}' jest już członkiem zespołu '{team.DisplayName}'.");
                    _logger.LogWarning("Nie można dodać członka: Użytkownik {UserUPN} jest już członkiem zespołu {TeamDisplayName}.", userUpn, team.DisplayName);
                    return team.GetMembership(user.Id);
                }

                if (!team.CanAddMoreMembers())
                {
                    operation.MarkAsFailed($"Zespół '{team.DisplayName}' osiągnął maksymalną liczbę członków.");
                    _logger.LogWarning("Nie można dodać członka: Zespół {TeamDisplayName} osiągnął maksymalną liczbę członków.", team.DisplayName);
                    return null;
                }

                // TODO: PowerShellService call - Dodanie członka w Microsoft Teams
                bool psSuccess = await _powerShellService.AddUserToTeamAsync(team.ExternalId ?? team.Id, user.UPN, role.ToString());

                if (psSuccess)
                {
                    var newMember = new TeamMember
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = user.Id,
                        TeamId = team.Id,
                        Role = role,
                        AddedDate = DateTime.UtcNow,
                        AddedBy = currentUserUpn,
                        IsActive = true,
                        IsApproved = !team.RequiresApproval,
                        ApprovedDate = !team.RequiresApproval ? DateTime.UtcNow : null,
                        ApprovedBy = !team.RequiresApproval ? currentUserUpn : null,
                        CreatedBy = currentUserUpn,
                        User = user,
                        Team = team
                    };

                    // Dodanie do kolekcji i zapis przez repozytorium TeamMember
                    // lub przez aktualizację Team, jeśli kaskada jest skonfigurowana.
                    // Bezpieczniej jest dodać przez dedykowane repozytorium TeamMember.
                    await _teamMemberRepository.AddAsync(newMember);
                    // Jeśli _teamMemberRepository.AddAsync nie robi SaveChanges,
                    // to Team.Members może nie być zaktualizowane w obiekcie team pobranym z repozytorium.
                    // Dla spójności obiektu 'team' można go dodać do kolekcji, jeśli nie jest śledzony przez kontekst w inny sposób.
                    // team.Members.Add(newMember); // Opcjonalne, jeśli Team jest śledzony i zmiana kolekcji zostanie wykryta.

                    operation.TargetEntityId = newMember.Id;
                    operation.MarkAsCompleted($"Użytkownik '{userUpn}' dodany do zespołu '{team.DisplayName}' jako {role}.");
                    _logger.LogInformation("Użytkownik {UserUPN} pomyślnie dodany do zespołu {TeamDisplayName}.", userUpn, team.DisplayName);

                    InvalidateCache(teamId, team.Owner); // Inwaliduj cache dla tego zespołu
                    return newMember;
                }
                else
                {
                    operation.MarkAsFailed("Błąd dodawania członka do zespołu w Microsoft Teams.");
                    _logger.LogError("Błąd dodawania użytkownika {UserUPN} do zespołu {TeamDisplayName} w Microsoft Teams.", userUpn, team.DisplayName);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas dodawania członka {UserUPN} do zespołu {TeamId}.", userUpn, teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return null;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveMemberAsync(string teamId, string userId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_member";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.MemberRemoved,
                TargetEntityType = nameof(TeamMember),
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            Team? team = null;

            try
            {
                _logger.LogInformation("Usuwanie użytkownika ID {UserId} z zespołu ID {TeamId} przez {CurrentUser}", userId, teamId, currentUserUpn);

                team = await _teamRepository.GetByIdAsync(teamId); // GetByIdAsync repozytorium dołącza Members
                if (team == null || !team.IsActive)
                {
                    operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje lub jest nieaktywny.");
                    _logger.LogWarning("Nie można usunąć członka: Zespół o ID {TeamId} nie istnieje lub jest nieaktywny.", teamId);
                    return false;
                }

                var memberToRemove = team.GetMembership(userId);
                if (memberToRemove == null)
                {
                    operation.MarkAsFailed($"Użytkownik o ID '{userId}' nie jest (aktywnym) członkiem zespołu '{team.DisplayName}'.");
                    _logger.LogWarning("Nie można usunąć członka: Użytkownik ID {UserId} nie jest aktywnym członkiem zespołu {TeamDisplayName}.", userId, team.DisplayName);
                    return false;
                }
                operation.TargetEntityId = memberToRemove.Id;
                operation.TargetEntityName = $"{memberToRemove.User?.UPN ?? userId} z {team.DisplayName}";

                if (memberToRemove.Role == TeamMemberRole.Owner && team.OwnerCount <= 1)
                {
                    operation.MarkAsFailed("Nie można usunąć ostatniego właściciela zespołu.");
                    _logger.LogWarning("Nie można usunąć członka: Użytkownik ID {UserId} jest ostatnim właścicielem zespołu {TeamDisplayName}.", userId, team.DisplayName);
                    return false;
                }

                // TODO: PowerShellService call - Usunięcie członka w Microsoft Teams
                bool psSuccess = await _powerShellService.RemoveUserFromTeamAsync(team.ExternalId ?? team.Id, memberToRemove.User!.UPN); // Zakładamy, że User nie jest null

                if (psSuccess)
                {
                    memberToRemove.RemoveFromTeam("Usunięty przez serwis", currentUserUpn);
                    _teamMemberRepository.Update(memberToRemove);
                    // team.Members.Remove(memberToRemove); // Usunięcie z kolekcji, jeśli EF Core nie robi tego automatycznie
                    // _teamRepository.Update(team); // Oznacz zespół jako zmodyfikowany

                    operation.MarkAsCompleted($"Użytkownik '{memberToRemove.User?.UPN}' usunięty z zespołu '{team.DisplayName}'.");
                    _logger.LogInformation("Użytkownik ID {UserId} pomyślnie usunięty z zespołu {TeamDisplayName}.", userId, team.DisplayName);

                    InvalidateCache(teamId, team.Owner); // Inwaliduj cache dla tego zespołu
                    return true;
                }
                else
                {
                    operation.MarkAsFailed("Błąd usuwania członka z zespołu w Microsoft Teams.");
                    _logger.LogError("Błąd usuwania użytkownika ID {UserId} z zespołu {TeamDisplayName} w Microsoft Teams.", userId, team.DisplayName);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania członka ID {UserId} z zespołu ID {TeamId}.", userId, teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
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

        // Prywatna metoda do unieważniania cache.
        private void InvalidateCache(string? teamId = null, string? ownerUpn = null, TeamStatus? oldStatus = null, TeamStatus? newStatus = null, bool allTeamsAffected = false, bool invalidateAll = false)
        {
            _logger.LogDebug("Inwalidacja cache'u zespołów. teamId: {TeamId}, ownerUpn: {OwnerUpn}, oldStatus: {OldStatus}, newStatus: {NewStatus}, allTeamsAffected: {AllTeamsAffected}, invalidateAll: {InvalidateAll}",
                teamId, ownerUpn, oldStatus, newStatus, allTeamsAffected, invalidateAll);

            var oldTokenSource = Interlocked.Exchange(ref _teamsCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
            _logger.LogDebug("Token cache'u dla zespołów został zresetowany.");

            // Reset tokenu powinien unieważnić wszystkie powiązane wpisy.
            // Poniższe jawne usuwanie jest dla pewności i natychmiastowego efektu,
            // jeśli token działa z opóźnieniem lub chcemy być bardzo precyzyjni.

            if (invalidateAll) // Używane przy Create, Delete, lub globalnym Refresh
            {
                _cache.Remove(AllActiveTeamsCacheKey);
                _cache.Remove(ActiveTeamsSpecificCacheKey);
                _cache.Remove(ArchivedTeamsCacheKey);
                // Nie usuwamy kluczy per-właściciel globalnie, bo to byłoby zbyt kosztowne,
                // chyba że mamy mechanizm śledzenia wszystkich użytych kluczy ownerUpn.
                // Token powinien sobie z tym poradzić.
                _logger.LogDebug("Usunięto z cache klucze dla list zespołów (AllActive, Active, Archived).");
            }

            if (!string.IsNullOrWhiteSpace(teamId))
            {
                _cache.Remove(TeamByIdCacheKeyPrefix + teamId);
                _logger.LogDebug("Usunięto z cache zespół o ID: {TeamId}", teamId);
            }

            if (!string.IsNullOrWhiteSpace(ownerUpn))
            {
                _cache.Remove(TeamsByOwnerCacheKeyPrefix + ownerUpn);
                _logger.LogDebug("Usunięto z cache zespoły dla właściciela: {OwnerUpn}", ownerUpn);
            }

            // Jeśli status się zmienił, co wpływa na listy Active/Archived
            if (oldStatus.HasValue && newStatus.HasValue && oldStatus != newStatus || allTeamsAffected && !invalidateAll)
            {
                _cache.Remove(ActiveTeamsSpecificCacheKey);
                _cache.Remove(ArchivedTeamsCacheKey);
                _cache.Remove(AllActiveTeamsCacheKey); // Ogólna lista też może być dotknięta (jeśli filtruje po statusie niejawnie)
                _logger.LogDebug("Usunięto z cache klucze dla list aktywnych i zarchiwizowanych zespołów z powodu zmiany statusu lub flagi allTeamsAffected.");
            }
            // Uwaga: Zmiany w kanałach lub członkach (poza dodaniem/usunięciem członka bezpośrednio przez TeamService)
            // które modyfikują obiekt Team w cache (jeśli GetTeamByIdAsync cache'uje z tymi zależnościami),
            // powinny również prowadzić do inwalidacji TeamByIdCacheKeyPrefix + teamId.
            // To wymagałoby komunikacji między serwisami lub bardziej zaawansowanego mechanizmu inwalidacji.
        }

        // Metoda pomocnicza do zapisu OperationHistory
        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id)) operation.Id = Guid.NewGuid().ToString();
            if (string.IsNullOrEmpty(operation.CreatedBy))
                operation.CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system_log_save";

            if (operation.StartedAt == default(DateTime) &&
                (operation.Status == OperationStatus.InProgress || operation.Status == OperationStatus.Pending || operation.Status == OperationStatus.Completed || operation.Status == OperationStatus.Failed))
            {
                if (operation.StartedAt == default(DateTime)) operation.StartedAt = DateTime.UtcNow;
                if (operation.Status == OperationStatus.Completed || operation.Status == OperationStatus.Failed || operation.Status == OperationStatus.Cancelled || operation.Status == OperationStatus.PartialSuccess)
                {
                    if (!operation.CompletedAt.HasValue) operation.CompletedAt = DateTime.UtcNow;
                    if (!operation.Duration.HasValue && operation.CompletedAt.HasValue) operation.Duration = operation.CompletedAt.Value - operation.StartedAt;
                }
            }

            await _operationHistoryRepository.AddAsync(operation);
            _logger.LogDebug("Zapisano nowy wpis historii operacji ID: {OperationId} dla zespołu.", operation.Id);
        }
    }
}