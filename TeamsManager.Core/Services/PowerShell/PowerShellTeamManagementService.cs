using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Enums;

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

                    // Invalidate cache
                    _cacheService.InvalidateTeamCache(teamId);
                    _cacheService.Remove(AllTeamsCacheKey);
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

                    // Invalidate cache
                    _cacheService.InvalidateTeamCache(teamId);

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

                    // Invalidate cache
                    _cacheService.InvalidateTeamCache(teamId);
                    _cacheService.InvalidateAllCache();

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

        #region Channel Operations

        public async Task<PSObject?> CreateTeamChannelAsync(
            string teamId, 
            string displayName, 
            bool isPrivate = false, 
            string? description = null)
        {
            if (!_connectionService.ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(displayName))
            {
                _logger.LogError("TeamID i DisplayName są wymagane.");
                return null;
            }

            _logger.LogInformation("Tworzenie kanału '{DisplayName}' w zespole {TeamId}. Prywatny: {IsPrivate}", 
                displayName, teamId, isPrivate);

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "TeamId", teamId },
                    { "DisplayName", displayName }
                };

                if (isPrivate)
                {
                    parameters.Add("MembershipType", "Private");
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    parameters.Add("Description", description);
                }

                var results = await _connectionService.ExecuteCommandWithRetryAsync("New-MgTeamChannel", parameters);

                // Invalidate channels cache for this team
                _cacheService.InvalidateChannelsForTeam(teamId);

                return results?.FirstOrDefault();
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