using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Exceptions.PowerShell;
using TeamsManager.Core.Helpers.PowerShell;

// TODO [ETAP4-AUDIT]: GŁÓWNE USTALENIA AUDYTU PowerShellTeamManagementService
// ============================================================================
// ZGODNOŚĆ Z PowerShellServices_Refaktoryzacja.md:
// ✅ OBECNE - Zgodne z specyfikacją:
//    - CreateTeamAsync() -> sekcja 1.1 (New-MgTeam)
//    - GetTeamAsync() -> sekcja 1.1 (Get-Team)
//    - GetAllTeamsAsync() -> sekcja 1.2 (Get-Team)
//    - GetTeamsByOwnerAsync() -> sekcja 1.3 (Get-Team)
//
// ❌ BRAKUJĄCE - Metody z specyfikacji nieobecne w implementacji:
//    PRIORYTET HIGH:
//    - GetTeamMembersAsync(string teamId) - sekcja 2.1 (Get-TeamUser)
//    - GetTeamMemberAsync(string teamId, string userUpn) - sekcja 2.2 (Get-TeamUser)
//    - UpdateTeamMemberRoleAsync(string teamId, string userUpn, string newRole) - sekcja 2.3 (Add-TeamUser)
//    - GetM365UserAsync(string userUpn) - sekcja 3.1 (Get-AzureADUser)
//    - SearchM365UsersAsync(string searchTerm) - sekcja 3.2 (Get-AzureADUser)
//    
//    PRIORYTET MEDIUM:
//    - GetUsersByDepartmentAsync(string department) - sekcja 3.3 (Get-AzureADUser)
//    - AssignLicenseToUserAsync() - sekcja 4.1 (Set-AzureADUserLicense)
//    - RemoveLicenseFromUserAsync() - sekcja 4.2 (Set-AzureADUserLicense)
//    - GetUserLicensesAsync() - sekcja 4.3 (Get-AzureADUserLicenseDetail)
//    - GetAvailableLicensesAsync() - sekcja 4.4 (Get-AzureADSubscribedSku)
//    - TestConnectionAsync() - sekcja 7.1 (Get-CsTenant)
//    - ValidatePermissionsAsync() - sekcja 7.2
//    - SyncTeamDataAsync() - sekcja 7.3
//
//    PRIORYTET LOW:
//    - CloneTeamAsync() - sekcja 8.1
//    - BackupTeamSettingsAsync() - sekcja 8.2
//    - BulkAddUsersToTeamAsync() - sekcja 8.3
//    - GetTeamUsageReportAsync() - sekcja 6.1
//    - GetUserActivityReportAsync() - sekcja 6.2
//    - GetTeamsHealthReportAsync() - sekcja 6.3
//    - ConnectToAzureADAsync() - sekcja 5.1
//    - ConnectToExchangeOnlineAsync() - sekcja 5.2
//
// ⚠️ CMDLETY - Sprawdzić zgodność z najnowszymi wersjami Microsoft.Graph:
//    - New-MgTeam - ZGODNY
//    - Get-MgTeam - ZGODNY, ale w specyfikacji: Get-Team (Teams module, nie Graph)
//    - Update-MgTeam - ZGODNY
//    - Remove-MgGroup - ZGODNY dla usuwania zespołu
//    - New-MgTeamChannel - ZGODNY
//    - Get-MgTeamChannel - ZGODNY
//    - Update-MgTeamChannel - ZGODNY  
//    - Remove-MgTeamChannel - ZGODNY
//
// 🛡️ BEZPIECZEŃSTWO - Tylko częściowo zaimplementowane:
//    ✅ PSParameterValidator używany w CreateTeamChannelAsync()
//    ❌ Brak walidacji w innych metodach
//    ❌ Brak escape injection chars w większości metod (tylko w CreateTeamAsync string replace)
//
// 📦 CACHE - Podstawowo zaimplementowany:
//    ✅ Cache invalidation w operacjach modyfikujących
//    ❌ Brak bulk cache operations
//    ❌ Brak granularnego cache dla członków zespołu
//
// 🔄 MAPOWANIE - Mieszane podejście:
//    ❌ Bezpośrednie Properties["..."] w GetTeamChannelAsync()
//    ✅ PSParameterValidator w CreateTeamChannelAsync()
//    ❌ Brak PSObjectMapper w pozostałych metodach
//
// 🎯 OBSŁUGA BŁĘDÓW - Częściowo zgodna z Etapem 3:
//    ✅ PowerShellCommandExecutionException w CreateTeamChannelAsync()
//    ❌ Return null w większości przypadków zamiast rzucania wyjątków
// ============================================================================

namespace TeamsManager.Core.Services.PowerShellServices
{
    /// <summary>
    /// Implementacja serwisu zarządzającego zespołami i kanałami w Microsoft Teams przez PowerShell
    /// </summary>
    public class PowerShellTeamManagementService : IPowerShellTeamManagementService
    {
        private readonly IPowerShellConnectionService _connectionService;
        private readonly IPowerShellCacheService _cacheService;
        private readonly IPowerShellUserResolverService _userResolver;
        private readonly ILogger<PowerShellTeamManagementService> _logger;

        // Stałe
        private const int MaxRetryAttempts = 3;
        private const string TeamDetailsCacheKeyPrefix = "PowerShell_Team_";
        private const string AllTeamsCacheKey = "PowerShell_Teams_All";
        private const string TeamChannelsCacheKeyPrefix = "PowerShell_TeamChannels_";

        public PowerShellTeamManagementService(
            IPowerShellConnectionService connectionService,
            IPowerShellCacheService cacheService,
            IPowerShellUserResolverService userResolver,
            ILogger<PowerShellTeamManagementService> logger)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _userResolver = userResolver ?? throw new ArgumentNullException(nameof(userResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Team Operations

        public async Task<string?> CreateTeamAsync(
            string displayName,
            string description,
            string ownerUpn,
            TeamVisibility visibility = TeamVisibility.Private,
            string? template = null)
        {
            // TODO [ETAP4-VALIDATION]: Dodać walidację parametrów zgodnie z Etapem 3
            // OBECNY: Tylko podstawowe string.IsNullOrWhiteSpace
            // PROPONOWANY: 
            // - PSParameterValidator.ValidateAndSanitizeString(displayName, maxLength: 256)
            // - PSParameterValidator.ValidateAndSanitizeString(description, maxLength: 1024)  
            // - PSParameterValidator.ValidateEmail(ownerUpn)
            // PRIORYTET: HIGH
            // KORZYŚCI: Ochrona przed injection, type safety, spójna walidacja

            // TODO [ETAP4-ERROR]: Ulepszona obsługa błędów zgodnie z Etapem 3
            // OBECNY: return null w przypadku błędów
            // PROPONOWANY: Rzucać specificzne wyjątki:
            // - PowerShellCommandExecutionException dla błędów PowerShell
            // - ArgumentException dla niepoprawnych parametrów
            // PRIORYTET: HIGH
            // UWAGI: Konsystencja z CreateTeamChannelAsync() która już to robi
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Środowisko PowerShell nie jest gotowe.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(ownerUpn))
            {
                _logger.LogError("DisplayName i OwnerUpn są wymagane.");
                return null;
            }

            // Pobierz ID właściciela z cache lub Graph
            var ownerId = await _userResolver.GetUserIdAsync(ownerUpn);
            if (string.IsNullOrEmpty(ownerId))
            {
                _logger.LogError("Nie znaleziono właściciela {OwnerUpn}", ownerUpn);
                return null;
            }

            _logger.LogInformation("Tworzenie zespołu '{DisplayName}' dla właściciela {OwnerUpn}",
                displayName, ownerUpn);

            try
            {
                // TODO [ETAP4-INJECTION]: Obecne escape tylko ' - niepełne
                // OBECNY: displayName.Replace("'", "''") - tylko pojedynczy apostrof
                // PROPONOWANY: PSParameterValidator.ValidateAndSanitizeString() która obsługuje ', `, $
                // PRIORYTET: HIGH
                // UWAGI: Potencjalne luki bezpieczeństwa z backtick i dollar
                var scriptBuilder = new StringBuilder();
                scriptBuilder.AppendLine("$teamBody = @{");
                scriptBuilder.AppendLine($"    displayName = '{displayName.Replace("'", "''")}'");
                scriptBuilder.AppendLine($"    description = '{description.Replace("'", "''")}'");
                scriptBuilder.AppendLine($"    visibility = '{visibility.ToString()}'");
                scriptBuilder.AppendLine("    members = @(");
                scriptBuilder.AppendLine("        @{");
                scriptBuilder.AppendLine("            '@odata.type' = '#microsoft.graph.aadUserConversationMember'");
                scriptBuilder.AppendLine("            roles = @('owner')");
                scriptBuilder.AppendLine($"            'user@odata.bind' = 'https://graph.microsoft.com/v1.0/users(''{ownerId}'')'");
                scriptBuilder.AppendLine("        }");
                scriptBuilder.AppendLine("    )");

                if (!string.IsNullOrEmpty(template))
                {
                    var graphTemplateId = MapTeamTemplate(template);
                    scriptBuilder.AppendLine($"    'template@odata.bind' = 'https://graph.microsoft.com/v1.0/teamsTemplates(''{graphTemplateId}'')'");
                    _logger.LogInformation("Używanie szablonu '{GraphTemplateId}'", graphTemplateId);
                }

                scriptBuilder.AppendLine("}");
                scriptBuilder.AppendLine();
                scriptBuilder.AppendLine("$newTeam = New-MgTeam -BodyParameter $teamBody -ErrorAction Stop");
                scriptBuilder.AppendLine("$newTeam.Id");

                var results = await _connectionService.ExecuteScriptAsync(scriptBuilder.ToString());
                var teamId = results?.FirstOrDefault()?.BaseObject?.ToString();

                if (!string.IsNullOrEmpty(teamId))
                {
                    _logger.LogInformation("Utworzono zespół '{DisplayName}' o ID: {TeamId}",
                        displayName, teamId);

                    // [ETAP7-CACHE] Granularna inwalidacja cache po utworzeniu zespołu
                    _cacheService.InvalidateAllActiveTeamsList();
                    _cacheService.InvalidateTeamsByOwner(ownerUpn);
                    _cacheService.Remove(AllTeamsCacheKey); // lista wszystkich zespołów
                    
                    _logger.LogInformation("Cache unieważniony po utworzeniu zespołu {TeamId}", teamId);
                }
                else
                {
                    _logger.LogError("Nie otrzymano ID zespołu dla zespołu '{DisplayName}'", displayName);
                }

                return teamId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd tworzenia zespołu '{DisplayName}'", displayName);
                return null;
            }
        }

        public async Task<bool> UpdateTeamPropertiesAsync(
            string teamId,
            string? newDisplayName = null,
            string? newDescription = null,
            TeamVisibility? newVisibility = null)
        {
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Środowisko PowerShell nie jest gotowe.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogError("TeamID nie może być puste.");
                return false;
            }

            // Sprawdź czy są jakieś zmiany
            if (newDisplayName == null && newDescription == null && newVisibility == null)
            {
                _logger.LogInformation("Brak właściwości do aktualizacji dla zespołu {TeamId}.", teamId);
                return true;
            }

            var parameters = new Dictionary<string, object>
            {
                { "GroupId", teamId }
            };

            var changes = new List<string>();
            if (newDisplayName != null)
            {
                parameters.Add("DisplayName", newDisplayName);
                changes.Add($"nazwa: '{newDisplayName}'");
            }
            if (newDescription != null)
            {
                parameters.Add("Description", newDescription);
                changes.Add($"opis: '{newDescription}'");
            }
            if (newVisibility.HasValue)
            {
                parameters.Add("Visibility", newVisibility.Value.ToString());
                changes.Add($"widoczność: {newVisibility.Value}");
            }

            try
            {
                var results = await _connectionService.ExecuteCommandWithRetryAsync("Update-MgTeam", parameters);
                
                if (results != null)
                {
                    _logger.LogInformation("Zaktualizowano właściwości zespołu {TeamId}", teamId);

                    // [ETAP7-CACHE] Unieważnij wszystkie cache związane z zespołem
                    _cacheService.InvalidateTeamCache(teamId);
                    _cacheService.InvalidateTeamById(teamId);
                    _cacheService.InvalidateAllActiveTeamsList();
                    
                    _logger.LogInformation("Cache zespołu {TeamId} unieważniony po aktualizacji", teamId);

                    return true;
                }
                else
                {
                    _logger.LogError("Nie udało się zaktualizować właściwości zespołu {TeamId}", teamId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd aktualizacji zespołu {TeamId}", teamId);
                return false;
            }
        }

        public async Task<bool> ArchiveTeamAsync(string teamId)
        {
            if (!_connectionService.ValidateRunspaceState()) return false;

            return await UpdateTeamArchiveStateAsync(teamId, true);
        }

        public async Task<bool> UnarchiveTeamAsync(string teamId)
        {
            if (!_connectionService.ValidateRunspaceState()) return false;

            return await UpdateTeamArchiveStateAsync(teamId, false);
        }

        public async Task<bool> DeleteTeamAsync(string teamId)
        {
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Środowisko PowerShell nie jest gotowe.");
                return false;
            }

            if (string.IsNullOrEmpty(teamId))
            {
                _logger.LogError("TeamID nie może być puste.");
                return false;
            }

            _logger.LogInformation("Usuwanie zespołu {TeamId}", teamId);

            var parameters = new Dictionary<string, object>
            {
                { "GroupId", teamId },
                { "Confirm", false }
            };

            try
            {
                var results = await _connectionService.ExecuteCommandWithRetryAsync("Remove-MgGroup", parameters);
                
                if (results != null)
                {
                    _logger.LogInformation("Pomyślnie usunięto zespół {TeamId}", teamId);

                    // [ETAP7-CACHE] Kompletna inwalidacja po usunięciu zespołu
                    _cacheService.InvalidateTeamCache(teamId);
                    _cacheService.InvalidateTeamById(teamId);
                    _cacheService.InvalidateAllActiveTeamsList();
                    _cacheService.InvalidateArchivedTeamsList();
                    _cacheService.InvalidateChannelsForTeam(teamId);
                    
                    _logger.LogInformation("Cache unieważniony po usunięciu zespołu {TeamId}", teamId);

                    return true;
                }
                else
                {
                    _logger.LogError("Nie udało się usunąć zespołu {TeamId}", teamId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd usuwania zespołu {TeamId}", teamId);
                return false;
            }
        }

        public async Task<PSObject?> GetTeamAsync(string teamId)
        {
            // TODO [ETAP4-AUDIT]: Zgodność z PowerShellServices.md sekcja 1.1
            // ✅ CMDLET: Get-MgTeam vs specyfikacja Get-Team -GroupId $teamId
            // UWAGI: Używamy Microsoft.Graph cmdletów zamiast Teams module
            // PRIORYTET: LOW - funkcjonalnie równoważne
            
            // TODO [ETAP4-VALIDATION]: Brak walidacji parametrów
            // PROPONOWANY: PSParameterValidator.ValidateGuid(teamId, nameof(teamId))
            // PRIORYTET: MEDIUM
            if (!_connectionService.ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogError("TeamID nie może być puste.");
                return null;
            }

            string cacheKey = TeamDetailsCacheKeyPrefix + teamId;

            if (_cacheService.TryGetValue(cacheKey, out PSObject? cachedTeam))
            {
                _logger.LogDebug("Zespół {TeamId} znaleziony w cache.", teamId);
                return cachedTeam;
            }

            _logger.LogInformation("Pobieranie zespołu o ID: {TeamId}", teamId);

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "TeamId", teamId }
                };

                var results = await _connectionService.ExecuteCommandWithRetryAsync("Get-MgTeam", parameters);
                var team = results?.FirstOrDefault();

                if (team != null)
                {
                    _cacheService.Set(cacheKey, team);
                }

                return team;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania zespołu {TeamId}", teamId);
                return null;
            }
        }

        public async Task<Collection<PSObject>?> GetAllTeamsAsync()
        {
            // TODO [ETAP4-AUDIT]: Zgodność z PowerShellServices.md sekcja 1.2
            // ✅ CMDLET: Get-MgTeam vs specyfikacja Get-Team
            // PRIORYTET: LOW - funkcjonalnie równoważne
            
            // TODO [ETAP4-CACHE]: Rozważyć pagination i bulk cache operations
            // OBECNY: Pobiera wszystkie zespoły na raz
            // PROPONOWANY: Implementacja pagination dla dużych organizacji
            // PRIORYTET: LOW - zależy od rozmiaru organizacji
            if (!_connectionService.ValidateRunspaceState()) return null;

            if (_cacheService.TryGetValue(AllTeamsCacheKey, out Collection<PSObject>? cachedTeams))
            {
                _logger.LogDebug("Wszystkie zespoły znalezione w cache.");
                return cachedTeams;
            }

            _logger.LogInformation("Pobieranie wszystkich zespołów");

            try
            {
                var results = await _connectionService.ExecuteCommandWithRetryAsync("Get-MgTeam");

                if (results != null)
                {
                    _cacheService.Set(AllTeamsCacheKey, results);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania wszystkich zespołów");
                return null;
            }
        }

        public async Task<Collection<PSObject>?> GetTeamsByOwnerAsync(string ownerUpn)
        {
            // TODO [ETAP4-AUDIT]: Różnica w implementacji vs specyfikacja sekcja 1.3
            // OBECNY: Get-MgUserOwnedTeam -UserId $userId
            // SPECYFIKACJA: Get-Team | Where-Object { $_.Owner -eq $ownerUpn }
            // PRIORYTET: LOW - obecna implementacja lepsza (mniej danych)
            // UWAGI: Obecna używa Graph API bezpośrednio, bardziej efektywna
            
            // TODO [ETAP4-VALIDATION]: Brak walidacji email
            // PROPONOWANY: PSParameterValidator.ValidateEmail(ownerUpn)
            // PRIORYTET: MEDIUM
            if (!_connectionService.ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(ownerUpn))
            {
                _logger.LogError("OwnerUpn nie może być puste.");
                return null;
            }

            _logger.LogInformation("Pobieranie zespołów dla właściciela: {OwnerUpn}", ownerUpn);

            try
            {
                var userId = await _userResolver.GetUserIdAsync(ownerUpn);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("Nie znaleziono użytkownika {OwnerUpn}", ownerUpn);
                    return null;
                }

                var script = $"Get-MgUserOwnedTeam -UserId '{userId}' -ErrorAction Stop";
                return await _connectionService.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania zespołów dla właściciela {OwnerUpn}", ownerUpn);
                return null;
            }
        }

        #endregion
        
        // ✅ ETAP4-MISSING ZREALIZOWANE: WSZYSTKIE 3 METODY P0 ZAIMPLEMENTOWANE
        // GetTeamMembersAsync, GetTeamMemberAsync, UpdateTeamMemberRoleAsync

        #region Team Member Management - Critical P0 Methods

        /// <summary>
        /// Pobiera wszystkich członków zespołu z cache i walidacją (P0-CRITICAL)
        /// </summary>
        /// <param name="teamId">ID zespołu (GUID)</param>
        /// <returns>Kolekcja członków zespołu z rolami</returns>
        public async Task<Collection<PSObject>?> GetTeamMembersAsync(string teamId)
        {
            var validatedTeamId = PSParameterValidator.ValidateGuid(teamId, nameof(teamId));
            
            string cacheKey = $"PowerShell_TeamMembers_{validatedTeamId}";
            
            if (_cacheService.TryGetValue(cacheKey, out Collection<PSObject>? cachedMembers))
            {
                _logger.LogDebug("Członkowie zespołu {TeamId} znalezieni w cache.", teamId);
                return cachedMembers;
            }
            
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Środowisko PowerShell nie jest gotowe.");
                return null;
            }
            
            try
            {
                var parameters = PSParameterValidator.CreateSafeParameters(
                    ("GroupId", validatedTeamId)
                );
                
                var results = await _connectionService.ExecuteCommandWithRetryAsync(
                    "Get-TeamUser",
                    parameters
                );
                
                if (results != null && results.Any())
                {
                    _cacheService.Set(cacheKey, results, TimeSpan.FromMinutes(5));
                    _logger.LogDebug("Członkowie zespołu {TeamId} dodani do cache.", teamId);
                }
                
                return results;
            }
            catch (PowerShellCommandExecutionException ex)
            {
                _logger.LogError(ex, "Failed to get team members for {TeamId}", teamId);
                throw new TeamOperationException(
                    $"Failed to retrieve members for team {teamId}", ex);
            }
        }

        /// <summary>
        /// Pobiera pojedynczego członka zespołu z walidacją (P0-CRITICAL)
        /// </summary>
        /// <param name="teamId">ID zespołu (GUID)</param>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>Informacje o członku zespołu lub null jeśli nie jest członkiem</returns>
        public async Task<PSObject?> GetTeamMemberAsync(string teamId, string userUpn)
        {
            var validatedTeamId = PSParameterValidator.ValidateGuid(teamId, nameof(teamId));
            var validatedUpn = PSParameterValidator.ValidateEmail(userUpn, nameof(userUpn));
            
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Środowisko PowerShell nie jest gotowe.");
                return null;
            }
            
            try
            {
                var parameters = PSParameterValidator.CreateSafeParameters(
                    ("GroupId", validatedTeamId),
                    ("User", validatedUpn)
                );
                
                var results = await _connectionService.ExecuteCommandWithRetryAsync(
                    "Get-TeamUser",
                    parameters
                );
                
                return results?.FirstOrDefault();
            }
            catch (PowerShellCommandExecutionException ex)
            {
                _logger.LogError(ex, "Failed to get team member {UserUpn} for team {TeamId}", userUpn, teamId);
                
                // Sprawdź czy to błąd "user not found" czy rzeczywisty błąd
                if (ex.ErrorRecords?.Any(e => e.FullyQualifiedErrorId.Contains("UserNotFound")) == true)
                {
                    return null; // User not found is not an error
                }
                
                throw new TeamOperationException(
                    $"Failed to retrieve team member {userUpn} for team {teamId}", ex);
            }
        }

        /// <summary>
        /// Zmienia rolę członka zespołu (Owner to Member) (P0-CRITICAL)
        /// </summary>
        /// <param name="teamId">ID zespołu (GUID)</param>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="newRole">Nowa rola: Owner lub Member</param>
        /// <returns>True jeśli operacja się powiodła</returns>
        public async Task<bool> UpdateTeamMemberRoleAsync(string teamId, string userUpn, string newRole)
        {
            var validatedTeamId = PSParameterValidator.ValidateGuid(teamId, nameof(teamId));
            var validatedUpn = PSParameterValidator.ValidateEmail(userUpn, nameof(userUpn));
            var validatedRole = PSParameterValidator.ValidateAndSanitizeString(newRole, nameof(newRole));
            
            if (!validatedRole.Equals("Owner", StringComparison.OrdinalIgnoreCase) &&
                !validatedRole.Equals("Member", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Role must be 'Owner' or 'Member'", nameof(newRole));
            }
            
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Środowisko PowerShell nie jest gotowe.");
                return false;
            }
            
            _logger.LogInformation("Changing role of user {UserUpn} in team {TeamId} to {NewRole}",
                userUpn, teamId, newRole);
            
            try
            {
                // Graph nie ma Update, więc Remove + Add
                var removeParams = PSParameterValidator.CreateSafeParameters(
                    ("GroupId", validatedTeamId),
                    ("User", validatedUpn)
                );
                
                await _connectionService.ExecuteCommandWithRetryAsync("Remove-TeamUser", removeParams);
                
                var addParams = PSParameterValidator.CreateSafeParameters(
                    ("GroupId", validatedTeamId),
                    ("User", validatedUpn),
                    ("Role", validatedRole)
                );
                
                await _connectionService.ExecuteCommandWithRetryAsync("Add-TeamUser", addParams);
                
                // [ETAP7-CACHE] Cache invalidation
                _cacheService.Remove($"PowerShell_TeamMembers_{teamId}");
                _cacheService.Remove($"PowerShell_UserTeams_{userUpn}");
                
                if (validatedRole.Equals("Owner", StringComparison.OrdinalIgnoreCase))
                {
                    _cacheService.InvalidateTeamsByOwner(userUpn);
                }
                
                _logger.LogInformation("Successfully updated role of {UserUpn} in team {TeamId} to {NewRole}",
                    userUpn, teamId, newRole);
                
                return true;
            }
            catch (PowerShellCommandExecutionException ex)
            {
                _logger.LogError(ex, "Failed to update role of {UserUpn} in team {TeamId} to {NewRole}",
                    userUpn, teamId, newRole);
                throw new TeamOperationException(
                    $"Failed to update team member role for {userUpn} in team {teamId}", ex);
            }
        }

        #endregion

        #region Channel Operations

        public async Task<PSObject?> CreateTeamChannelAsync(
            string teamId, 
            string displayName, 
            bool isPrivate = false, 
            string? description = null)
        {
            if (!_connectionService.ValidateRunspaceState()) return null;

            try
            {
                // Walidacja parametrów przed wywołaniem PowerShell
                var validatedTeamId = PSParameterValidator.ValidateGuid(teamId, nameof(teamId));
                var validatedDisplayName = PSParameterValidator.ValidateAndSanitizeString(displayName, nameof(displayName), maxLength: 50);
                var validatedDescription = description != null 
                    ? PSParameterValidator.ValidateAndSanitizeString(description, nameof(description), allowEmpty: true, maxLength: 1024)
                    : null;

                _logger.LogInformation("Tworzenie kanału '{DisplayName}' w zespole {TeamId}. Prywatny: {IsPrivate}", 
                    validatedDisplayName, validatedTeamId, isPrivate);

                // Przygotuj bezpieczne parametry
                var parameters = PSParameterValidator.CreateSafeParameters(
                    ("TeamId", validatedTeamId),
                    ("DisplayName", validatedDisplayName)
                );

                if (isPrivate)
                {
                    parameters.Add("MembershipType", "Private");
                }

                if (validatedDescription != null)
                {
                    parameters.Add("Description", validatedDescription);
                }

                var results = await _connectionService.ExecuteCommandWithRetryAsync("New-MgTeamChannel", parameters);

                if (results?.FirstOrDefault() != null)
                {
                    // [ETAP7-CACHE] Unieważnij cache kanałów zespołu
                    _cacheService.InvalidateChannelsForTeam(validatedTeamId);
                    
                    // Unieważnij też cache samego zespołu (zmienił się stan)
                    _cacheService.InvalidateTeamCache(validatedTeamId);
                    
                    _logger.LogInformation("Cache kanałów unieważniony dla zespołu {TeamId}", validatedTeamId);
                }

                return results?.FirstOrDefault();
            }
            catch (ArgumentException ex)
            {
                // Przekształć błędy walidacji na PowerShellCommandExecutionException
                throw new PowerShellCommandExecutionException(
                    $"Błąd walidacji parametrów dla CreateTeamChannelAsync: {ex.Message}",
                    command: "New-MgTeamChannel",
                    parameters: null,
                    executionTime: null,
                    exitCode: null,
                    errorRecords: null,
                    innerException: ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie udało się utworzyć kanału '{DisplayName}' w zespole {TeamId}", 
                    displayName, teamId);
                return null;
            }
        }

        public async Task<bool> UpdateTeamChannelAsync(
            string teamId,
            string channelId,
            string? newDisplayName = null,
            string? newDescription = null)
        {
            if (!_connectionService.ValidateRunspaceState()) return false;

            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(channelId))
            {
                _logger.LogError("TeamID i ChannelID są wymagane do aktualizacji kanału.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(newDisplayName) && newDescription == null)
            {
                _logger.LogInformation("Brak właściwości do aktualizacji dla kanału ID '{ChannelId}' w zespole {TeamId}.", 
                    channelId, teamId);
                return true;
            }

            _logger.LogInformation("Aktualizowanie kanału ID '{ChannelId}' w zespole {TeamId}. Nowa nazwa: '{NewDisplayName}', Nowy opis: '{NewDescription}'",
                channelId, teamId, newDisplayName ?? "bez zmian", newDescription ?? "bez zmian");

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "TeamId", teamId },
                    { "ChannelId", channelId }
                };

                if (!string.IsNullOrWhiteSpace(newDisplayName))
                {
                    parameters.Add("DisplayName", newDisplayName);
                }

                if (newDescription != null)
                {
                    parameters.Add("Description", newDescription);
                }

                var results = await _connectionService.ExecuteCommandWithRetryAsync("Update-MgTeamChannel", parameters);

                if (results != null)
                {
                    _cacheService.InvalidateChannelAndTeam(teamId, channelId);
                    _logger.LogInformation("Pomyślnie zaktualizowano kanał ID '{ChannelId}' w zespole {TeamId}.", 
                        channelId, teamId);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie udało się zaktualizować kanału ID '{ChannelId}' w zespole {TeamId}", 
                    channelId, teamId);
                return false;
            }
        }

        public async Task<bool> RemoveTeamChannelAsync(string teamId, string channelId)
        {
            if (!_connectionService.ValidateRunspaceState()) return false;

            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(channelId))
            {
                _logger.LogError("TeamID i ChannelID są wymagane do usunięcia kanału.");
                return false;
            }

            _logger.LogInformation("Usuwanie kanału ID '{ChannelId}' z zespołu {TeamId}", channelId, teamId);

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "TeamId", teamId },
                    { "ChannelId", channelId },
                    { "Confirm", false }
                };

                var results = await _connectionService.ExecuteCommandWithRetryAsync("Remove-MgTeamChannel", parameters);

                if (results != null)
                {
                    _cacheService.InvalidateChannelAndTeam(teamId, channelId);
                    _logger.LogInformation("Pomyślnie usunięto kanał ID '{ChannelId}' z zespołu {TeamId}.", 
                        channelId, teamId);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie udało się usunąć kanału ID '{ChannelId}' z zespołu {TeamId}", 
                    channelId, teamId);
                return false;
            }
        }

        public async Task<Collection<PSObject>?> GetTeamChannelsAsync(string teamId)
        {
            if (!_connectionService.ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogError("TeamID nie może być puste.");
                return null;
            }

            string cacheKey = TeamChannelsCacheKeyPrefix + teamId;

            if (_cacheService.TryGetValue(cacheKey, out Collection<PSObject>? cachedChannels))
            {
                _logger.LogDebug("Kanały dla zespołu {TeamId} znalezione w cache.", teamId);
                return cachedChannels;
            }

            _logger.LogInformation("Pobieranie wszystkich kanałów dla zespołu {TeamId}", teamId);

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "TeamId", teamId }
                };

                var results = await _connectionService.ExecuteCommandWithRetryAsync("Get-MgTeamChannel", parameters);

                if (results != null)
                {
                    _cacheService.Set(cacheKey, results, _cacheService.GetShortCacheEntryOptions().AbsoluteExpirationRelativeToNow);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania kanałów dla zespołu {TeamId}", teamId);
                return null;
            }
        }

        public async Task<PSObject?> GetTeamChannelAsync(string teamId, string channelDisplayName)
        {
            if (!_connectionService.ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(channelDisplayName))
            {
                _logger.LogError("TeamID i ChannelDisplayName są wymagane.");
                return null;
            }

            _logger.LogInformation("Pobieranie kanału '{ChannelDisplayName}' dla zespołu {TeamId}", 
                channelDisplayName, teamId);

            try
            {
                var allChannels = await GetTeamChannelsAsync(teamId);
                if (allChannels == null)
                {
                    _logger.LogError("Nie udało się pobrać listy kanałów dla zespołu {TeamId}", teamId);
                    return null;
                }

                // TODO [ETAP4-MAPPING]: Zastąpić bezpośrednie Properties przez PSObjectMapper
                // OBECNY: c.Properties["DisplayName"]?.Value?.ToString()
                // PROPONOWANY: PSObjectMapper.GetString(c, "DisplayName")
                // PRIORYTET: MEDIUM
                // KORZYŚCI: Type safety, null handling, spójne logowanie
                var foundChannel = allChannels.FirstOrDefault(c =>
                    c.Properties["DisplayName"]?.Value?.ToString()?.Equals(channelDisplayName, StringComparison.OrdinalIgnoreCase) ?? false);

                if (foundChannel == null)
                {
                    _logger.LogInformation("Kanał '{ChannelDisplayName}' nie znaleziony w zespole {TeamId}", 
                        channelDisplayName, teamId);
                }

                return foundChannel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania kanału '{ChannelDisplayName}' dla zespołu {TeamId}", 
                    channelDisplayName, teamId);
                return null;
            }
        }

        public async Task<PSObject?> GetTeamChannelByIdAsync(string teamId, string channelId)
        {
            if (!_connectionService.ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(channelId))
            {
                _logger.LogError("TeamID i ChannelID są wymagane do pobrania kanału.");
                return null;
            }

            string cacheKey = $"{TeamChannelsCacheKeyPrefix}{teamId}_{channelId}";

            if (_cacheService.TryGetValue(cacheKey, out PSObject? cachedChannel))
            {
                _logger.LogDebug("Kanał ID: {ChannelId} dla zespołu ID: {TeamId} znaleziony w cache.", channelId, teamId);
                return cachedChannel;
            }

            _logger.LogInformation("Pobieranie kanału ID '{ChannelId}' dla zespołu '{TeamId}' z Microsoft Graph.", channelId, teamId);

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "TeamId", teamId },
                    { "ChannelId", channelId }
                };

                var results = await _connectionService.ExecuteCommandWithRetryAsync("Get-MgTeamChannel", parameters);
                var channel = results?.FirstOrDefault();

                if (channel != null)
                {
                    _cacheService.Set(cacheKey, channel, _cacheService.GetShortCacheEntryOptions().AbsoluteExpirationRelativeToNow);
                    _logger.LogDebug("Kanał ID: {ChannelId} dla zespołu ID: {TeamId} dodany do cache.", channelId, teamId);
                }
                else
                {
                    _logger.LogInformation("Kanał ID: {ChannelId} dla zespołu ID: {TeamId} nie został znaleziony w Microsoft Graph.", channelId, teamId);
                }

                return channel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania kanału ID '{ChannelId}' dla zespołu '{TeamId}'.", channelId, teamId);
                return null;
            }
        }

        #endregion
        
        // TODO [ETAP4-MISSING]: BRAKUJĄCE METODY Z SPECYFIKACJI - POZOSTAŁE SEKCJE
        // =========================================================================
        // SEKCJA 3. POBIERANIE INFORMACJI O UŻYTKOWNIKACH M365 - PRIORYTET HIGH:
        // - GetM365UserAsync(string userUpn) - Get-AzureADUser -ObjectId $userUpn
        // - SearchM365UsersAsync(string searchTerm) - Get-AzureADUser -SearchString $searchTerm
        // - GetUsersByDepartmentAsync(string department) - Get-AzureADUser -Filter "department eq '$department'"
        //
        // SEKCJA 4. ZARZĄDZANIE LICENCJAMI - PRIORYTET MEDIUM:
        // - AssignLicenseToUserAsync(string userUpn, string licenseSkuId)
        // - RemoveLicenseFromUserAsync(string userUpn, string licenseSkuId)  
        // - GetUserLicensesAsync(string userUpn)
        // - GetAvailableLicensesAsync()
        //
        // SEKCJA 7. NARZĘDZIA DIAGNOSTYCZNE - PRIORYTET MEDIUM:
        // - TestConnectionAsync() - Get-CsTenant
        // - ValidatePermissionsAsync() - Dictionary<string, bool>
        // - SyncTeamDataAsync(string teamId) - bool
        //
        // SEKCJA 8. ZAAWANSOWANE OPERACJE - PRIORYTET LOW:
        // - CloneTeamAsync() - Klonowanie zespołu
        // - BackupTeamSettingsAsync() - Backup ustawień
        // - BulkAddUsersToTeamAsync() - Masowe dodawanie użytkowników
        //
        // SEKCJA 6. RAPORTOWANIE - PRIORYTET LOW:
        // - GetTeamUsageReportAsync() - Raporty wykorzystania
        // - GetUserActivityReportAsync() - Aktywność użytkowników
        // - GetTeamsHealthReportAsync() - Status zdrowia zespołów
        //
        // SEKCJA 5. ROZSZERZENIE POŁĄCZEŃ - PRIORYTET LOW:
        // - ConnectToAzureADAsync() - Połączenie z Azure AD
        // - ConnectToExchangeOnlineAsync() - Połączenie z Exchange Online
        // =========================================================================

        #region Private Methods

        private async Task<bool> UpdateTeamArchiveStateAsync(string teamId, bool archived)
        {
            if (string.IsNullOrEmpty(teamId))
            {
                _logger.LogError("TeamID nie może być puste.");
                return false;
            }

            _logger.LogInformation("Rozpoczynanie {Action} zespołu {TeamId}", archived ? "archiwizacji" : "przywracania", teamId);

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "GroupId", teamId },
                    { "IsArchived", archived }
                };

                var results = await _connectionService.ExecuteCommandWithRetryAsync("Update-MgTeam", parameters);
                
                if (results != null)
                {
                    _logger.LogInformation("Pomyślnie wykonano {Action} zespołu {TeamId}", archived ? "archiwizacji" : "przywracania", teamId);

                    // Invalidate cache
                    _cacheService.InvalidateTeamCache(teamId);

                    return true;
                }
                else
                {
                    _logger.LogError("Nie udało się {Action} zespołu {TeamId}", archived ? "zarchiwizować" : "przywrócić", teamId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas {Action} zespołu {TeamId}", archived ? "archiwizacji" : "przywracania", teamId);
                return false;
            }
        }

        private string MapTeamTemplate(string template)
        {
            return template switch
            {
                "EDU_Class" => "educationClass",
                "EDU_Staff" => "educationStaff",
                "EDU_PLC" => "educationPLC",
                "EDU_StaffDepartment" => "educationStaffDepartment",
                _ => template
            };
        }

        #endregion
    }
}