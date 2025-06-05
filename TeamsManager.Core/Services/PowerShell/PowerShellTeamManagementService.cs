using System;
using System.Collections;
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
            // [ETAP3] Walidacja parametrów z PSParameterValidator
            var validatedDisplayName = PSParameterValidator.ValidateAndSanitizeString(displayName, nameof(displayName), maxLength: 256);
            var validatedDescription = PSParameterValidator.ValidateAndSanitizeString(description, nameof(description), maxLength: 1024, allowEmpty: true);
            var validatedOwnerUpn = PSParameterValidator.ValidateEmail(ownerUpn, nameof(ownerUpn));
            
            if (!_connectionService.ValidateRunspaceState())
            {
                throw new PowerShellConnectionException("PowerShell runspace is not ready");
            }

            // Pobierz ID właściciela z cache lub Graph
            var ownerId = await _userResolver.GetUserIdAsync(validatedOwnerUpn);
            if (string.IsNullOrEmpty(ownerId))
            {
                throw new PowerShellCommandExecutionException(
                    $"Owner user not found: {validatedOwnerUpn}",
                    command: "UserResolver.GetUserIdAsync",
                    innerException: null);
            }

            _logger.LogInformation("Tworzenie zespołu '{DisplayName}' dla właściciela {OwnerUpn}",
                validatedDisplayName, validatedOwnerUpn);

            try
            {
                // [ETAP3] Używam zwalidowanych i zsanityzowanych parametrów
                var scriptBuilder = new StringBuilder();
                scriptBuilder.AppendLine("$teamBody = @{");
                scriptBuilder.AppendLine($"    displayName = '{validatedDisplayName}'");
                scriptBuilder.AppendLine($"    description = '{validatedDescription}'");
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
                        validatedDisplayName, teamId);

                    // [ETAP3] Granularna inwalidacja cache po utworzeniu zespołu
                    _cacheService.InvalidateAllActiveTeamsList();
                    _cacheService.InvalidateTeamsByOwner(validatedOwnerUpn);
                    _cacheService.Remove(AllTeamsCacheKey); // lista wszystkich zespołów
                    
                    _logger.LogInformation("Cache unieważniony po utworzeniu zespołu {TeamId}", teamId);

                    return teamId;
                }
                else
                {
                    throw new PowerShellCommandExecutionException(
                        $"Failed to create team '{validatedDisplayName}' - no ID returned",
                        command: "New-MgTeam",
                        innerException: null);
                }
            }
            catch (PowerShellCommandExecutionException)
            {
                throw; // Re-throw PowerShell exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd tworzenia zespołu '{DisplayName}'", validatedDisplayName);
                throw new PowerShellCommandExecutionException(
                    $"Failed to create team '{validatedDisplayName}'",
                    command: "New-MgTeam",
                    parameters: null,
                    executionTime: null,
                    exitCode: null,
                    errorRecords: null,
                    innerException: ex);
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
            // [ETAP4] Walidacja parametrów z PSParameterValidator
            var validatedTeamId = PSParameterValidator.ValidateGuid(teamId, nameof(teamId));
            
            if (!_connectionService.ValidateRunspaceState())
            {
                throw new PowerShellConnectionException("PowerShell runspace is not ready");
            }

            string cacheKey = TeamDetailsCacheKeyPrefix + validatedTeamId;

            if (_cacheService.TryGetValue(cacheKey, out PSObject? cachedTeam))
            {
                _logger.LogDebug("Zespół {TeamId} znaleziony w cache.", validatedTeamId);
                return cachedTeam;
            }

            _logger.LogInformation("Pobieranie zespołu o ID: {TeamId}", validatedTeamId);

            try
            {
                var parameters = PSParameterValidator.CreateSafeParameters(
                    ("TeamId", validatedTeamId)
                );

                var results = await _connectionService.ExecuteCommandWithRetryAsync("Get-MgTeam", parameters);
                var team = results?.FirstOrDefault();

                if (team != null)
                {
                    _cacheService.Set(cacheKey, team);
                    _logger.LogInformation("Zespół {TeamId} znaleziony i dodany do cache.", validatedTeamId);
                }

                return team;
            }
            catch (PowerShellCommandExecutionException)
            {
                throw; // Re-throw PowerShell exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania zespołu {TeamId}", validatedTeamId);
                throw new PowerShellCommandExecutionException(
                    $"Failed to retrieve team {validatedTeamId}",
                    command: "Get-MgTeam",
                    innerException: ex);
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
            // [ETAP4] Walidacja parametrów z PSParameterValidator
            var validatedOwnerUpn = PSParameterValidator.ValidateEmail(ownerUpn, nameof(ownerUpn));
            
            if (!_connectionService.ValidateRunspaceState())
            {
                throw new PowerShellConnectionException("PowerShell runspace is not ready");
            }

            _logger.LogInformation("Pobieranie zespołów dla właściciela: {OwnerUpn}", validatedOwnerUpn);

            try
            {
                var userId = await _userResolver.GetUserIdAsync(validatedOwnerUpn);
                if (string.IsNullOrEmpty(userId))
                {
                    throw new PowerShellCommandExecutionException(
                        $"Owner user not found: {validatedOwnerUpn}",
                        command: "UserResolver.GetUserIdAsync",
                        innerException: null);
                }

                var script = $"Get-MgUserOwnedTeam -UserId '{userId}' -ErrorAction Stop";
                var results = await _connectionService.ExecuteScriptAsync(script);
                
                if (results != null)
                {
                    _logger.LogInformation("Znaleziono {Count} zespołów dla właściciela {OwnerUpn}", 
                        results.Count, validatedOwnerUpn);
                }
                
                return results;
            }
            catch (PowerShellCommandExecutionException)
            {
                throw; // Re-throw PowerShell exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania zespołów dla właściciela {OwnerUpn}", validatedOwnerUpn);
                throw new PowerShellCommandExecutionException(
                    $"Failed to retrieve teams for owner {validatedOwnerUpn}",
                    command: "Get-MgUserOwnedTeam",
                    innerException: ex);
            }
        }

        #endregion
        
        // ✅ ETAP4-MISSING ZREALIZOWANE: WSZYSTKIE 3 METODY P0 ZAIMPLEMENTOWANE
        // GetTeamMembersAsync, GetTeamMemberAsync, UpdateTeamMemberRoleAsync

        #region Team Member Management - Critical P0 Methods

        /// <summary>
        /// Pobiera wszystkich członków zespołu z cache i walidacją (P0-CRITICAL)
        /// MIGRACJA ETAP 5/6: Teams module → Microsoft.Graph API
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
                // ETAP 5/6: Migracja Get-TeamUser → Get-MgTeamMember
                var parameters = PSParameterValidator.CreateSafeParameters(
                    ("TeamId", validatedTeamId), // Graph używa TeamId zamiast GroupId
                    ("All", true) // Pobierz wszystkich członków
                );
                
                var results = await _connectionService.ExecuteCommandWithRetryAsync(
                    "Get-MgTeamMember",
                    parameters
                );
                
                if (results != null && results.Any())
                {
                    _cacheService.Set(cacheKey, results, TimeSpan.FromMinutes(5));
                    _logger.LogDebug("Członkowie zespołu {TeamId} dodani do cache (Graph API).", teamId);
                }
                
                return results;
            }
            catch (PowerShellCommandExecutionException ex)
            {
                _logger.LogError(ex, "Failed to get team members for {TeamId} using Graph API", teamId);
                throw new TeamOperationException(
                    $"Failed to retrieve members for team {teamId} using Graph API", ex);
            }
        }

        /// <summary>
        /// Pobiera pojedynczego członka zespołu z walidacją (P0-CRITICAL)
        /// MIGRACJA ETAP 5/6: Teams module → Microsoft.Graph API
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
                // ETAP 5/6: Pobierz userId z Graph (Graph API wymaga ID, nie UPN)
                var userId = await _userResolver.GetUserIdAsync(validatedUpn);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Cannot find user {UserUpn} in Microsoft Graph", userUpn);
                    return null;
                }
                
                // ETAP 5/6: Migracja Get-TeamUser → Get-MgTeamMember z userId
                var script = $"Get-MgTeamMember -TeamId '{validatedTeamId}' -UserId '{userId}' -ErrorAction Stop";
                
                var results = await _connectionService.ExecuteScriptAsync(script);
                
                return results?.FirstOrDefault();
            }
            catch (PowerShellCommandExecutionException ex)
            {
                _logger.LogError(ex, "Failed to get team member {UserUpn} for team {TeamId} using Graph API", userUpn, teamId);
                
                // Sprawdź czy to błąd "user not found" czy rzeczywisty błąd
                if (ex.ErrorRecords?.Any(e => e.FullyQualifiedErrorId.Contains("UserNotFound") || 
                                               e.FullyQualifiedErrorId.Contains("MemberNotFound")) == true)
                {
                    return null; // User not found is not an error
                }
                
                throw new TeamOperationException(
                    $"Failed to retrieve team member {userUpn} for team {teamId} using Graph API", ex);
            }
        }

        /// <summary>
        /// Zmienia rolę członka zespołu (Owner to Member) (P0-CRITICAL)
        /// MIGRACJA ETAP 5/6: Teams module → Microsoft.Graph API
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
            
            _logger.LogInformation("Changing role of user {UserUpn} in team {TeamId} to {NewRole} using Microsoft.Graph API",
                userUpn, teamId, newRole);
            
            try
            {
                // ETAP 5/6: Krok 1 - Pobierz userId z Graph (nie UPN)
                var userId = await _userResolver.GetUserIdAsync(validatedUpn);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("Cannot find user {UserUpn} in Microsoft Graph", userUpn);
                    return false;
                }
                
                // ETAP 5/6: Krok 2 - Remove z Microsoft.Graph
                var removeParams = PSParameterValidator.CreateSafeParameters(
                    ("GroupId", validatedTeamId),
                    ("DirectoryObjectId", userId) // Graph używa ID, nie UPN
                );
                
                await _connectionService.ExecuteCommandWithRetryAsync("Remove-MgGroupMember", removeParams);
                
                // ETAP 5/6: Krok 3 - Add z Microsoft.Graph jako konwersacyjny członek zespołu
                var memberScript = $@"
$memberToAdd = @{{
    '@odata.type' = '#microsoft.graph.aadUserConversationMember'
    roles = @('{validatedRole.ToLowerInvariant()}')
    'user@odata.bind' = 'https://graph.microsoft.com/v1.0/users(''{userId}'')'
}}
New-MgTeamMember -TeamId '{validatedTeamId}' -BodyParameter $memberToAdd -ErrorAction Stop
";
                
                await _connectionService.ExecuteScriptAsync(memberScript);
                
                // [ETAP7-CACHE] Cache invalidation
                _cacheService.Remove($"PowerShell_TeamMembers_{teamId}");
                _cacheService.Remove($"PowerShell_UserTeams_{userUpn}");
                
                if (validatedRole.Equals("Owner", StringComparison.OrdinalIgnoreCase))
                {
                    _cacheService.InvalidateTeamsByOwner(userUpn);
                }
                
                _logger.LogInformation("Successfully updated role of {UserUpn} in team {TeamId} to {NewRole} using Graph API",
                    userUpn, teamId, newRole);
                
                return true;
            }
            catch (PowerShellCommandExecutionException ex)
            {
                _logger.LogError(ex, "Failed to update role of {UserUpn} in team {TeamId} to {NewRole} using Graph API",
                    userUpn, teamId, newRole);
                throw new TeamOperationException(
                    $"Failed to update team member role for {userUpn} in team {teamId} using Graph API", ex);
            }
        }

        /// <summary>
        /// Weryfikuje uprawnienia Microsoft.Graph dla operacji Team Members
        /// ETAP 5/6: Diagnostyka uprawnień Graph API
        /// </summary>
        /// <returns>True jeśli wymagane uprawnienia są dostępne</returns>
        public async Task<bool> VerifyGraphPermissionsAsync()
        {
            try
            {
                var script = @"
$context = Get-MgContext
if ($context -eq $null) {
    Write-Output 'NotConnected'
    return
}

$requiredScopes = @('TeamMember.ReadWrite.All', 'Group.ReadWrite.All', 'User.Read.All')
$availableScopes = $context.Scopes

foreach ($scope in $requiredScopes) {
    if ($availableScopes -contains $scope) {
        Write-Output ""Found: $scope""
    }
}

$availableScopes | Where-Object { 
    $_ -like '*TeamMember*' -or 
    $_ -like '*Group.ReadWrite*' -or
    $_ -like '*User.Read*'
} | ForEach-Object { Write-Output ""Available: $_"" }
";
                
                var results = await _connectionService.ExecuteScriptAsync(script);
                var permissions = results?.Select(r => r.BaseObject?.ToString()).ToList() ?? new List<string>();
                
                _logger.LogInformation("Graph API permissions check: {Permissions}", 
                    string.Join(", ", permissions));
                
                var hasRequiredPermissions = permissions.Any(p => 
                    p.Contains("TeamMember.ReadWrite") || 
                    p.Contains("Group.ReadWrite"));
                
                if (!hasRequiredPermissions)
                {
                    _logger.LogWarning("Missing required Graph API permissions for team member operations. " +
                                     "Required: TeamMember.ReadWrite.All or Group.ReadWrite.All");
                }
                
                return hasRequiredPermissions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify Graph API permissions");
                return false;
            }
        }

        #endregion

        #region Diagnostic Operations - HIGH Priority

        /// <summary>
        /// [ETAP3] Testuje połączenie z Microsoft Graph
        /// </summary>
        /// <returns>True jeśli połączenie aktywne</returns>
        public async Task<bool> TestConnectionAsync()
        {
            if (!_connectionService.ValidateRunspaceState())
            {
                return false;
            }
            
            try
            {
                // Użyj Get-MgOrganization zamiast Get-CsTenant (Graph API)
                var results = await _connectionService.ExecuteCommandWithRetryAsync("Get-MgOrganization");
                return results != null && results.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection test failed");
                return false;
            }
        }

        /// <summary>
        /// [ETAP3] Waliduje uprawnienia Graph API
        /// </summary>
        /// <returns>Słownik uprawnień i ich statusu</returns>
        public async Task<Dictionary<string, bool>> ValidatePermissionsAsync()
        {
            var permissions = new Dictionary<string, bool>();
            
            if (!_connectionService.ValidateRunspaceState())
            {
                throw new PowerShellConnectionException("PowerShell runspace is not ready");
            }
            
            try
            {
                var script = @"
$context = Get-MgContext
if ($context -eq $null) {
    @{}
} else {
    $requiredScopes = @(
        'Team.ReadWrite.All',
        'TeamMember.ReadWrite.All', 
        'Group.ReadWrite.All',
        'User.Read.All',
        'Channel.ReadWrite.All'
    )
    
    $result = @{}
    foreach ($scope in $requiredScopes) {
        $result[$scope] = $context.Scopes -contains $scope
    }
    $result
}";
                
                var results = await _connectionService.ExecuteScriptAsync(script);
                if (results?.FirstOrDefault() != null)
                {
                    var resultObj = results.First();
                    if (resultObj.BaseObject is Hashtable ht)
                    {
                        foreach (DictionaryEntry entry in ht)
                        {
                            permissions[entry.Key.ToString()] = Convert.ToBoolean(entry.Value);
                        }
                    }
                }
                
                return permissions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate permissions");
                throw new PowerShellCommandExecutionException(
                    "Failed to validate Graph API permissions",
                    command: "Get-MgContext",
                    innerException: ex);
            }
        }

        #endregion

        #region Diagnostic Operations - Phase 2 (ETAP4)

        /// <summary>
        /// [ETAP4] Pobiera informacje o systemie PowerShell i środowisku
        /// </summary>
        /// <returns>Obiekt PSObject z informacjami systemowymi lub null</returns>
        public async Task<PSObject?> GetSystemInfoAsync()
        {
            if (!_connectionService.ValidateRunspaceState())
            {
                throw new PowerShellConnectionException("PowerShell runspace is not ready");
            }

            _logger.LogInformation("Pobieranie informacji o systemie PowerShell");

            try
            {
                var script = @"
$systemInfo = @{
    PowerShellVersion = $PSVersionTable.PSVersion.ToString()
    PowerShellEdition = $PSVersionTable.PSEdition
    HostName = $Host.Name
    HostVersion = $Host.Version.ToString()
    OSVersion = [System.Environment]::OSVersion.ToString()
    MachineName = [System.Environment]::MachineName
    UserName = [System.Environment]::UserName
    WorkingSet = [System.GC]::GetTotalMemory($false)
    ProcessorCount = [System.Environment]::ProcessorCount
    ExecutionPolicy = Get-ExecutionPolicy
    CurrentCulture = (Get-Culture).Name
    TimeZone = (Get-TimeZone).Id
    DotNetVersion = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
}
$systemInfo
";

                var results = await _connectionService.ExecuteScriptAsync(script);
                var systemInfo = results?.FirstOrDefault();

                if (systemInfo != null)
                {
                    _logger.LogInformation("Informacje o systemie PowerShell zostały pobrane pomyślnie");
                    
                    // Log some key system info for debugging
                    try
                    {
                        var psVersion = PSObjectMapper.GetString(systemInfo, "PowerShellVersion") ?? "Unknown";
                        var osVersion = PSObjectMapper.GetString(systemInfo, "OSVersion") ?? "Unknown";
                        _logger.LogDebug("System: PowerShell {PowerShellVersion}, OS {OSVersion}", psVersion, osVersion);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract system info details for logging");
                    }
                }

                return systemInfo;
            }
            catch (PowerShellCommandExecutionException)
            {
                throw; // Re-throw PowerShell exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get system information");
                throw new PowerShellCommandExecutionException(
                    "Failed to retrieve system information",
                    command: "Get-SystemInfo",
                    innerException: ex);
            }
        }

        /// <summary>
        /// [ETAP4] Pobiera wersję PowerShell i zainstalowanych modułów
        /// </summary>
        /// <returns>Obiekt PSObject z informacjami o wersji lub null</returns>
        public async Task<PSObject?> GetPowerShellVersionAsync()
        {
            if (!_connectionService.ValidateRunspaceState())
            {
                throw new PowerShellConnectionException("PowerShell runspace is not ready");
            }

            _logger.LogInformation("Pobieranie wersji PowerShell i modułów");

            try
            {
                var script = @"
$versionInfo = @{
    PSVersionTable = $PSVersionTable
    ImportantModules = @()
}

# Sprawdź kluczowe moduły
$importantModuleNames = @('Microsoft.Graph', 'Microsoft.Graph.Teams', 'Microsoft.Graph.Users', 'Microsoft.Graph.Groups')
foreach ($moduleName in $importantModuleNames) {
    $module = Get-Module -Name $moduleName -ListAvailable | Select-Object -First 1
    if ($module) {
        $versionInfo.ImportantModules += @{
            Name = $module.Name
            Version = $module.Version.ToString()
            Path = $module.ModuleBase
            Author = $module.Author
        }
    } else {
        $versionInfo.ImportantModules += @{
            Name = $moduleName
            Version = 'Not Installed'
            Path = $null
            Author = $null
        }
    }
}

$versionInfo
";

                var results = await _connectionService.ExecuteScriptAsync(script);
                var versionInfo = results?.FirstOrDefault();

                if (versionInfo != null)
                {
                    _logger.LogInformation("Informacje o wersji PowerShell zostały pobrane pomyślnie");
                    
                    // Log key module versions for debugging
                    try
                    {
                        var psVersion = PSObjectMapper.GetString(versionInfo, "PSVersionTable.PSVersion") ?? "Unknown";
                        _logger.LogDebug("PowerShell version: {PowerShellVersion}", psVersion);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract version info details for logging");
                    }
                }

                return versionInfo;
            }
            catch (PowerShellCommandExecutionException)
            {
                throw; // Re-throw PowerShell exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get PowerShell version information");
                throw new PowerShellCommandExecutionException(
                    "Failed to retrieve PowerShell version information",
                    command: "Get-PowerShellVersion",
                    innerException: ex);
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
        // - SyncTeamDataAsync() - bool
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