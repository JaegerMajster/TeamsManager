using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Services
{
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
            ISchoolYearRepository schoolYearRepository)
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
        }

        public async Task<Team?> CreateTeamAsync(
            string displayName,
            string description,
            string ownerUpn,
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
                TargetEntityName = displayName,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

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

                var ownerUser = await _userRepository.GetByIdAsync(ownerUpn); // Zmienione na GetByIdAsync, zakładając, że ownerUpn to ID użytkownika
                                                                              // Jeśli ownerUpn to UPN, użyj _userRepository.GetUserByUpnAsync(ownerUpn);
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
                        _logger.LogInformation("Nazwa zespołu wygenerowana z szablonu '{TemplateName}': {FinalDisplayName}", template.Name, finalDisplayName);
                    }
                    else
                    {
                        _logger.LogWarning("Szablon o ID {TemplateId} nie istnieje lub jest nieaktywny. Użycie oryginalnej nazwy: {OriginalName}", teamTemplateId, displayName);
                    }
                }
                operation.TargetEntityName = finalDisplayName;

                // TODO: PowerShellService call - Rzeczywiste utworzenie zespołu w Microsoft Teams
                // string? externalTeamId = await _powerShellService.CreateTeam(finalDisplayName, description, ownerUser.UPN); // ownerUser.UPN zamiast ownerUpn
                string? externalTeamId = $"sim_ext_{Guid.NewGuid()}"; // Symulacja
                bool psSuccess = !string.IsNullOrEmpty(externalTeamId);

                if (psSuccess)
                {
                    _logger.LogInformation("Zespół '{FinalDisplayName}' pomyślnie utworzony w Microsoft Teams (symulacja). External ID: {ExternalTeamId}", finalDisplayName, externalTeamId);
                    var newTeam = new Team
                    {
                        Id = Guid.NewGuid().ToString(),
                        DisplayName = finalDisplayName,
                        Description = description,
                        Owner = ownerUser.UPN, // Używamy UPN z obiektu ownerUser
                        Status = TeamStatus.Active,
                        TemplateId = template?.Id,
                        SchoolTypeId = schoolTypeId,
                        SchoolYearId = schoolYearId,
                        ExternalId = externalTeamId,
                        CreatedBy = currentUserUpn,
                        IsActive = true
                    };
                    if (schoolYear != null) newTeam.AcademicYear = schoolYear.Name;

                    var ownerMembership = new TeamMember
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = ownerUser.Id,
                        TeamId = newTeam.Id,
                        Role = ownerUser.DefaultTeamRole, // Używamy domyślnej roli z User
                        AddedDate = DateTime.UtcNow,
                        AddedBy = currentUserUpn,
                        IsActive = true,
                        IsApproved = true,
                        CreatedBy = currentUserUpn,
                        User = ownerUser, // Przypisanie obiektu nawigacyjnego
                        Team = newTeam    // Przypisanie obiektu nawigacyjnego
                    };
                    newTeam.Members.Add(ownerMembership);

                    await _teamRepository.AddAsync(newTeam);

                    operation.TargetEntityId = newTeam.Id;
                    operation.MarkAsCompleted($"Zespół ID: {newTeam.Id}, External ID: {externalTeamId}");
                    _logger.LogInformation("Zespół '{FinalDisplayName}' pomyślnie utworzony i zapisany lokalnie. ID: {TeamId}", finalDisplayName, newTeam.Id);
                    await SaveOperationHistoryAsync(operation); // Przeniesione do metody pomocniczej
                    return newTeam;
                }
                else
                {
                    operation.MarkAsFailed("Nie udało się utworzyć zespołu w Microsoft Teams (symulacja zwróciła błąd).");
                    _logger.LogError("Błąd tworzenia zespołu '{FinalDisplayName}' w Microsoft Teams (symulacja).", finalDisplayName);
                    await SaveOperationHistoryAsync(operation);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia zespołu {DisplayName}.", displayName);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return null;
            }
        }

        public async Task<IEnumerable<Team>> GetAllTeamsAsync()
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych zespołów.");
            return await _teamRepository.FindAsync(t => t.IsActive);
        }

        public async Task<Team?> GetTeamByIdAsync(string teamId, bool includeMembers = false, bool includeChannels = false)
        {
            _logger.LogInformation("Pobieranie zespołu o ID: {TeamId}. Dołączanie członków: {IncludeMembers}, Dołączanie kanałów: {IncludeChannels}", teamId, includeMembers, includeChannels);

            var team = await _teamRepository.GetByIdAsync(teamId); // Ta metoda w TeamRepository już robi Include

            // Jeśli chcemy bardziej granularne Include, to trzeba dodać metody do ITeamRepository
            // np. GetByIdWithMembersAsync, GetByIdWithChannelsAsync, GetByIdWithAllDetailsAsync
            // Poniższa logika jest zbędna, jeśli TeamRepository.GetByIdAsync robi odpowiednie Include
            /*
            if (team != null) {
                if (includeMembers && (team.Members == null || !team.Members.Any())) {
                    // TODO: Jak załadować jawnie bez DbContext? Przez dedykowaną metodę repozytorium.
                    // np. team.Members = await _teamMemberRepository.GetMembersForTeamAsync(teamId);
                }
                if (includeChannels && (team.Channels == null || !team.Channels.Any())) {
                    // np. team.Channels = await _channelRepository.GetChannelsForTeamAsync(teamId);
                }
            }
            */
            return team;
        }

        public async Task<bool> UpdateTeamAsync(Team teamToUpdate)
        {
            if (teamToUpdate == null) throw new ArgumentNullException(nameof(teamToUpdate));

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.TeamUpdated,
                TargetEntityType = nameof(Team),
                TargetEntityId = teamToUpdate.Id,
                TargetEntityName = teamToUpdate.DisplayName, // Nazwa PRZED potencjalną modyfikacją
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie aktualizacji zespołu ID: {TeamId} przez {User}", teamToUpdate.Id, currentUserUpn);

            try
            {
                var existingTeam = await _teamRepository.GetByIdAsync(teamToUpdate.Id);
                if (existingTeam == null || !existingTeam.IsActive)
                {
                    operation.MarkAsFailed($"Zespół o ID '{teamToUpdate.Id}' nie istnieje lub jest nieaktywny.");
                    _logger.LogWarning("Nie można zaktualizować zespołu ID {TeamId} - nie istnieje lub jest nieaktywny.", teamToUpdate.Id);
                    await SaveOperationHistoryAsync(operation);
                    return false;
                }

                // TODO: Walidacja danych w teamToUpdate
                // TODO: PowerShellService call - Aktualizacja w Microsoft Teams (jeśli potrzebne)
                // bool psSuccess = await _powerShellService.UpdateTeamPropertiesAsync(existingTeam.ExternalId, teamToUpdate.DisplayName, teamToUpdate.Description);
                bool psSuccess = true; // Symulacja

                if (psSuccess)
                {
                    existingTeam.DisplayName = teamToUpdate.DisplayName;
                    existingTeam.Description = teamToUpdate.Description;
                    existingTeam.Owner = teamToUpdate.Owner; // Upewnij się, że zmiana właściciela jest obsługiwana
                    existingTeam.SchoolTypeId = teamToUpdate.SchoolTypeId;
                    existingTeam.SchoolYearId = teamToUpdate.SchoolYearId;
                    existingTeam.TemplateId = teamToUpdate.TemplateId;
                    // ... aktualizuj inne właściwości według potrzeb ...
                    existingTeam.MarkAsModified(currentUserUpn);

                    _teamRepository.Update(existingTeam);

                    operation.TargetEntityName = existingTeam.DisplayName; // Nazwa PO aktualizacji
                    operation.MarkAsCompleted($"Zespół ID: {existingTeam.Id} zaktualizowany.");
                    _logger.LogInformation("Zespół ID: {TeamId} pomyślnie zaktualizowany.", existingTeam.Id);
                    await SaveOperationHistoryAsync(operation);
                    return true;
                }
                else
                {
                    operation.MarkAsFailed("Błąd aktualizacji zespołu w Microsoft Teams (symulacja).");
                    _logger.LogError("Błąd aktualizacji zespołu ID {TeamId} w Microsoft Teams (symulacja).", existingTeam.Id);
                    await SaveOperationHistoryAsync(operation);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji zespołu ID {TeamId}.", teamToUpdate.Id);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return false;
            }
        }

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

            try
            {
                var team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null)
                {
                    operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje.");
                    _logger.LogWarning("Nie można zarchiwizować zespołu ID {TeamId} - nie istnieje.", teamId);
                    await SaveOperationHistoryAsync(operation);
                    return false;
                }
                if (team.Status == TeamStatus.Archived)
                {
                    operation.MarkAsCompleted($"Zespół '{team.DisplayName}' (ID: {team.Id}) był już zarchiwizowany.");
                    _logger.LogInformation("Zespół ID {TeamId} był już zarchiwizowany.", teamId);
                    await SaveOperationHistoryAsync(operation);
                    return true;
                }
                operation.TargetEntityName = team.DisplayName;

                // TODO: PowerShellService call - Archiwizacja w Microsoft Teams
                // bool psSuccess = await _powerShellService.ArchiveTeamAsync(team.ExternalId ?? team.Id);
                bool psSuccess = true; // Symulacja

                if (psSuccess)
                {
                    team.Archive(reason, currentUserUpn);
                    _teamRepository.Update(team);

                    operation.MarkAsCompleted($"Zespół '{team.DisplayName}' (oryg. '{operation.TargetEntityName}') zarchiwizowany. Powód: {reason}");
                    _logger.LogInformation("Zespół ID {TeamId} pomyślnie zarchiwizowany.", teamId);
                    await SaveOperationHistoryAsync(operation);
                    return true;
                }
                else
                {
                    operation.MarkAsFailed("Błąd archiwizacji zespołu w Microsoft Teams (symulacja).");
                    _logger.LogError("Błąd archiwizacji zespołu ID {TeamId} w Microsoft Teams.", teamId);
                    await SaveOperationHistoryAsync(operation);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas archiwizacji zespołu ID {TeamId}.", teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return false;
            }
        }

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

            try
            {
                var team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null)
                {
                    operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje.");
                    _logger.LogWarning("Nie można przywrócić zespołu ID {TeamId} - nie istnieje.", teamId);
                    await SaveOperationHistoryAsync(operation);
                    return false;
                }
                if (team.Status == TeamStatus.Active)
                {
                    operation.MarkAsCompleted($"Zespół '{team.DisplayName}' (ID: {team.Id}) był już aktywny.");
                    _logger.LogInformation("Zespół ID {TeamId} był już aktywny.", teamId);
                    await SaveOperationHistoryAsync(operation);
                    return true;
                }
                operation.TargetEntityName = team.DisplayName;

                // TODO: PowerShellService call - Przywrócenie w Microsoft Teams
                // bool psSuccess = await _powerShellService.RestoreTeamAsync(team.ExternalId ?? team.Id);
                bool psSuccess = true; // Symulacja

                if (psSuccess)
                {
                    team.Restore(currentUserUpn);
                    _teamRepository.Update(team);

                    operation.MarkAsCompleted($"Zespół '{team.DisplayName}' (ID: {team.Id}) przywrócony.");
                    _logger.LogInformation("Zespół ID {TeamId} pomyślnie przywrócony.", teamId);
                    await SaveOperationHistoryAsync(operation);
                    return true;
                }
                else
                {
                    operation.MarkAsFailed("Błąd przywracania zespołu w Microsoft Teams (symulacja).");
                    _logger.LogError("Błąd przywracania zespołu ID {TeamId} w Microsoft Teams.", teamId);
                    await SaveOperationHistoryAsync(operation);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas przywracania zespołu ID {TeamId}.", teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return false;
            }
        }

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

            try
            {
                var team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null)
                {
                    operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje.");
                    _logger.LogWarning("Nie można usunąć zespołu ID {TeamId} - nie istnieje.", teamId);
                    await SaveOperationHistoryAsync(operation);
                    return false;
                }
                operation.TargetEntityName = team.DisplayName;

                // TODO: PowerShellService call - Usunięcie zespołu w Microsoft Teams
                // bool psSuccess = await _powerShellService.DeleteTeamAsync(team.ExternalId ?? team.Id);
                bool psSuccess = true; // Symulacja

                if (psSuccess)
                {
                    // Soft delete - oznaczamy jako nieaktywny
                    team.MarkAsDeleted(currentUserUpn); // Używa metody z BaseEntity
                    _teamRepository.Update(team); // Oznacza encję jako zmodyfikowaną (IsActive = false)

                    operation.MarkAsCompleted($"Zespół '{team.DisplayName}' (ID: {teamId}) oznaczony jako usunięty.");
                    _logger.LogInformation("Zespół ID {TeamId} pomyślnie oznaczony jako usunięty.", teamId);
                    await SaveOperationHistoryAsync(operation);
                    return true;
                }
                else
                {
                    operation.MarkAsFailed("Błąd usuwania zespołu w Microsoft Teams (symulacja).");
                    _logger.LogError("Błąd usuwania zespołu ID {TeamId} w Microsoft Teams (symulacja).", teamId);
                    await SaveOperationHistoryAsync(operation);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania zespołu ID {TeamId}.", teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return false;
            }
        }

        public async Task<TeamMember?> AddMemberAsync(string teamId, string userUpn, TeamMemberRole role)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_add_member";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.MemberAdded,
                TargetEntityType = nameof(TeamMember),
                // TargetEntityName można ustawić na $"{userUpn} to team {teamId}"
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Dodawanie użytkownika {UserUPN} do zespołu ID {TeamId} z rolą {Role} przez {CurrentUser}", userUpn, teamId, role, currentUserUpn);

                var team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null || !team.IsActive)
                {
                    operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje lub jest nieaktywny.");
                    _logger.LogWarning("Nie można dodać członka: Zespół o ID {TeamId} nie istnieje lub jest nieaktywny.", teamId);
                    await SaveOperationHistoryAsync(operation);
                    return null;
                }
                operation.TargetEntityName = $"Członek {userUpn} do zespołu {team.DisplayName}";


                var user = await _userRepository.GetUserByUpnAsync(userUpn);
                if (user == null || !user.IsActive)
                {
                    operation.MarkAsFailed($"Użytkownik o UPN '{userUpn}' nie istnieje lub jest nieaktywny.");
                    _logger.LogWarning("Nie można dodać członka: Użytkownik o UPN {UserUPN} nie istnieje lub jest nieaktywny.", userUpn);
                    await SaveOperationHistoryAsync(operation);
                    return null;
                }

                if (team.HasMember(user.Id))
                {
                    operation.MarkAsFailed($"Użytkownik '{userUpn}' jest już członkiem zespołu '{team.DisplayName}'.");
                    _logger.LogWarning("Nie można dodać członka: Użytkownik {UserUPN} jest już członkiem zespołu {TeamDisplayName}.", userUpn, team.DisplayName);
                    await SaveOperationHistoryAsync(operation);
                    return team.GetMembership(user.Id); // Zwróć istniejące członkostwo
                }

                if (!team.CanAddMoreMembers())
                {
                    operation.MarkAsFailed($"Zespół '{team.DisplayName}' osiągnął maksymalną liczbę członków.");
                    _logger.LogWarning("Nie można dodać członka: Zespół {TeamDisplayName} osiągnął maksymalną liczbę członków.", team.DisplayName);
                    await SaveOperationHistoryAsync(operation);
                    return null;
                }

                // TODO: PowerShellService call - Dodanie członka w Microsoft Teams
                // bool psSuccess = await _powerShellService.AddMemberToTeamAsync(team.ExternalId ?? team.Id, user.UPN, role);
                bool psSuccess = true; // Symulacja

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
                        IsApproved = !team.RequiresApproval, // Zatwierdź automatycznie, jeśli zespół tego nie wymaga
                        ApprovedDate = !team.RequiresApproval ? DateTime.UtcNow : null,
                        ApprovedBy = !team.RequiresApproval ? currentUserUpn : null,
                        CreatedBy = currentUserUpn,
                        User = user, // Dla EF Core
                        Team = team  // Dla EF Core
                    };
                    // Zamiast _teamMemberRepository.AddAsync(newMember) i SaveChanges,
                    // jeśli mamy relację Team -> Members skonfigurowaną z Cascade.All,
                    // dodanie do kolekcji zespołu i zapisanie zespołu powinno wystarczyć.
                    // Jednak jawne dodanie TeamMember do jego repozytorium jest bezpieczniejsze
                    // lub upewnienie się, że jest dodane do kolekcji Team.Members PRZED zapisem Team.
                    team.Members.Add(newMember); // Dodaj do kolekcji w zespole
                                                 //_teamRepository.Update(team); // Oznacz zespół jako zmodyfikowany (bo dodano członka)
                                                 // lub jeśli AddAsync dla TeamMember jest osobną operacją:
                    await _teamMemberRepository.AddAsync(newMember);

                    operation.TargetEntityId = newMember.Id;
                    operation.MarkAsCompleted($"Użytkownik '{userUpn}' dodany do zespołu '{team.DisplayName}' jako {role}.");
                    _logger.LogInformation("Użytkownik {UserUPN} pomyślnie dodany do zespołu {TeamDisplayName}.", userUpn, team.DisplayName);
                    await SaveOperationHistoryAsync(operation);
                    return newMember;
                }
                else
                {
                    operation.MarkAsFailed("Błąd dodawania członka do zespołu w Microsoft Teams (symulacja).");
                    _logger.LogError("Błąd dodawania użytkownika {UserUPN} do zespołu {TeamDisplayName} w Microsoft Teams.", userUpn, team.DisplayName);
                    await SaveOperationHistoryAsync(operation);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas dodawania członka {UserUPN} do zespołu {TeamId}.", userUpn, teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return null;
            }
        }

        public async Task<bool> RemoveMemberAsync(string teamId, string userId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_member";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.MemberRemoved,
                TargetEntityType = nameof(TeamMember),
                // TargetEntityId będzie ID członkostwa, TargetEntityName może być UPN@TeamName
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Usuwanie użytkownika ID {UserId} z zespołu ID {TeamId} przez {CurrentUser}", userId, teamId, currentUserUpn);

                var team = await _teamRepository.GetByIdAsync(teamId); // GetByIdAsync z repozytorium powinno dołączyć Members
                if (team == null || !team.IsActive)
                {
                    operation.MarkAsFailed($"Zespół o ID '{teamId}' nie istnieje lub jest nieaktywny.");
                    _logger.LogWarning("Nie można usunąć członka: Zespół o ID {TeamId} nie istnieje lub jest nieaktywny.", teamId);
                    await SaveOperationHistoryAsync(operation);
                    return false;
                }

                var memberToRemove = team.GetMembership(userId); // Używa metody z Team.cs, która sprawdza aktywność
                if (memberToRemove == null)
                {
                    operation.MarkAsFailed($"Użytkownik o ID '{userId}' nie jest (aktywnym) członkiem zespołu '{team.DisplayName}'.");
                    _logger.LogWarning("Nie można usunąć członka: Użytkownik ID {UserId} nie jest aktywnym członkiem zespołu {TeamDisplayName}.", userId, team.DisplayName);
                    await SaveOperationHistoryAsync(operation);
                    return false;
                }
                operation.TargetEntityId = memberToRemove.Id;
                operation.TargetEntityName = $"{memberToRemove.User?.UPN} z {team.DisplayName}";


                // Nie można usunąć ostatniego właściciela
                if (memberToRemove.Role == TeamMemberRole.Owner && team.OwnerCount <= 1)
                {
                    operation.MarkAsFailed("Nie można usunąć ostatniego właściciela zespołu.");
                    _logger.LogWarning("Nie można usunąć członka: Użytkownik ID {UserId} jest ostatnim właścicielem zespołu {TeamDisplayName}.", userId, team.DisplayName);
                    await SaveOperationHistoryAsync(operation);
                    return false;
                }

                // TODO: PowerShellService call - Usunięcie członka w Microsoft Teams
                // bool psSuccess = await _powerShellService.RemoveMemberFromTeamAsync(team.ExternalId ?? team.Id, memberToRemove.User.UPN);
                bool psSuccess = true; // Symulacja

                if (psSuccess)
                {
                    // Soft delete członkostwa
                    memberToRemove.RemoveFromTeam("Usunięty przez serwis", currentUserUpn);
                    _teamMemberRepository.Update(memberToRemove);
                    // LUB twarde usunięcie:
                    // await _teamMemberRepository.DeleteAsync(memberToRemove.Id);

                    operation.MarkAsCompleted($"Użytkownik '{memberToRemove.User?.UPN}' usunięty z zespołu '{team.DisplayName}'.");
                    _logger.LogInformation("Użytkownik ID {UserId} pomyślnie usunięty z zespołu {TeamDisplayName}.", userId, team.DisplayName);
                    await SaveOperationHistoryAsync(operation);
                    return true;
                }
                else
                {
                    operation.MarkAsFailed("Błąd usuwania członka z zespołu w Microsoft Teams (symulacja).");
                    _logger.LogError("Błąd usuwania użytkownika ID {UserId} z zespołu {TeamDisplayName} w Microsoft Teams.", userId, team.DisplayName);
                    await SaveOperationHistoryAsync(operation);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania członka ID {UserId} z zespołu ID {TeamId}.", userId, teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return false;
            }
        }

        // Metoda pomocnicza do zapisu OperationHistory
        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id)) // Upewnij się, że ID jest ustawione
            {
                operation.Id = Guid.NewGuid().ToString();
            }
            if (await _operationHistoryRepository.GetByIdAsync(operation.Id) == null)
            {
                await _operationHistoryRepository.AddAsync(operation);
            }
            else
            {
                _operationHistoryRepository.Update(operation);
            }
        }
    }
}