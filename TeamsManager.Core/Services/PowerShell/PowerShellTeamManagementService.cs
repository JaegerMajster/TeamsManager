using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Services.PowerShell
{
    /// <summary>
    /// Implementacja serwisu zarządzającego zespołami i kanałami w Microsoft Teams przez PowerShell
    /// </summary>
    public class PowerShellTeamManagementService : IPowerShellTeamManagementService
    {
        private readonly IPowerShellConnectionService _connectionService;
        private readonly IPowerShellCacheService _cacheService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly INotificationService _notificationService;
        private readonly ILogger<PowerShellTeamManagementService> _logger;

        // Stałe
        private const int MaxRetryAttempts = 3;
        private const string TeamDetailsCacheKeyPrefix = "PowerShell_Team_";
        private const string AllTeamsCacheKey = "PowerShell_Teams_All";
        private const string TeamChannelsCacheKeyPrefix = "PowerShell_TeamChannels_";

        public PowerShellTeamManagementService(
            IPowerShellConnectionService connectionService,
            IPowerShellCacheService cacheService,
            ICurrentUserService currentUserService,
            IOperationHistoryRepository operationHistoryRepository,
            INotificationService notificationService,
            ILogger<PowerShellTeamManagementService> logger)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
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
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_create_team";
            var operationId = Guid.NewGuid().ToString();
            var operation = new OperationHistory
            {
                Id = operationId,
                Type = OperationType.TeamCreated,
                TargetEntityType = "Team",
                TargetEntityName = displayName,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            // Powiadomienie o rozpoczęciu operacji
            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 5, 
                $"Rozpoczynanie tworzenia zespołu '{displayName}'...");
            await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                $"🚀 Rozpoczęto tworzenie zespołu '{displayName}'", "info");

            try
            {
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 10, 
                    "Weryfikacja środowiska PowerShell...");

                if (!_connectionService.ValidateRunspaceState())
                {
                    operation.MarkAsFailed("Środowisko PowerShell nie jest gotowe.");
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        "❌ Środowisko PowerShell nie jest gotowe", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Operacja zakończona niepowodzeniem - błąd środowiska");
                    return null;
                }

                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 20, 
                    "Weryfikacja parametrów wejściowych...");

                if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(ownerUpn))
                {
                    operation.MarkAsFailed("DisplayName i OwnerUpn są wymagane.");
                    _logger.LogError("DisplayName i OwnerUpn są wymagane.");
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        "❌ Nazwa zespołu i właściciel są wymagane", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Operacja zakończona niepowodzeniem - nieprawidłowe parametry");
                    return null;
                }

                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 30, 
                    $"Wyszukiwanie właściciela zespołu: {ownerUpn}...");

                // Pobierz ID właściciela z cache lub Graph
                var ownerId = await _cacheService.GetUserIdAsync(ownerUpn);
                if (string.IsNullOrEmpty(ownerId))
                {
                    operation.MarkAsFailed($"Nie znaleziono właściciela {ownerUpn}");
                    _logger.LogError("Nie znaleziono właściciela {OwnerUpn}", ownerUpn);
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        $"❌ Nie znaleziono właściciela zespołu: {ownerUpn}", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Operacja zakończona niepowodzeniem - nie znaleziono właściciela");
                    return null;
                }

                _logger.LogInformation("Tworzenie zespołu '{DisplayName}' dla właściciela {OwnerUpn}",
                    displayName, ownerUpn);

                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 50, 
                    "Przygotowywanie skryptu tworzenia zespołu...");

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
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 60, 
                        $"Konfiguracja szablonu zespołu: {template}...");
                }

                scriptBuilder.AppendLine("}");
                scriptBuilder.AppendLine();
                scriptBuilder.AppendLine("$newTeam = New-MgTeam -BodyParameter $teamBody -ErrorAction Stop");
                scriptBuilder.AppendLine("$newTeam.Id");

                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 70, 
                    "Tworzenie zespołu w Microsoft 365...");

                var results = await _connectionService.ExecuteScriptAsync(scriptBuilder.ToString());
                var teamId = results?.FirstOrDefault()?.BaseObject?.ToString();

                if (!string.IsNullOrEmpty(teamId))
                {
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 90, 
                        "Zapisywanie informacji o zespole...");

                    operation.TargetEntityId = teamId;
                    operation.MarkAsCompleted($"Zespół utworzony z ID: {teamId}");
                    _logger.LogInformation("Utworzono zespół '{DisplayName}' o ID: {TeamId}",
                        displayName, teamId);

                    // Invalidate cache
                    _cacheService.InvalidateTeamCache(teamId);
                    _cacheService.Remove(AllTeamsCacheKey);

                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Zespół utworzony pomyślnie!");
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        $"✅ Zespół '{displayName}' został utworzony pomyślnie (ID: {teamId})", "success");
                }
                else
                {
                    operation.MarkAsFailed("Nie otrzymano ID zespołu.");
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        $"❌ Nie udało się utworzyć zespołu '{displayName}' - brak ID zespołu", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Operacja zakończona niepowodzeniem - brak ID zespołu");
                }

                return teamId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd tworzenia zespołu '{DisplayName}'", displayName);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    $"❌ Błąd krytyczny podczas tworzenia zespołu '{displayName}': {ex.Message}", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona błędem krytycznym");
                return null;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> UpdateTeamPropertiesAsync(
            string teamId,
            string? newDisplayName = null,
            string? newDescription = null,
            TeamVisibility? newVisibility = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            var operationId = Guid.NewGuid().ToString();

            // Powiadomienie o rozpoczęciu operacji
            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 5, 
                $"Rozpoczynanie aktualizacji właściwości zespołu {teamId}...");
            await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                $"🔄 Rozpoczęto aktualizację właściwości zespołu", "info");

            if (!_connectionService.ValidateRunspaceState())
            {
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "❌ Środowisko PowerShell nie jest gotowe", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona niepowodzeniem - błąd środowiska");
                return false;
            }

            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogError("TeamID nie może być puste.");
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "❌ ID zespołu nie może być puste", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona niepowodzeniem - brak ID zespołu");
                return false;
            }

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 20, 
                "Weryfikacja parametrów aktualizacji...");

            // Sprawdź czy są jakieś zmiany
            if (newDisplayName == null && newDescription == null && newVisibility == null)
            {
                _logger.LogInformation("Brak właściwości do aktualizacji dla zespołu {TeamId}.", teamId);
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "ℹ️ Brak właściwości do aktualizacji", "warning");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona - brak zmian do wprowadzenia");
                return true;
            }

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 40, 
                "Przygotowywanie parametrów aktualizacji...");

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
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 70, 
                    $"Aktualizacja zespołu w Microsoft 365: {string.Join(", ", changes)}...");

                var results = await _connectionService.ExecuteCommandWithRetryAsync("Update-MgTeam", parameters);
                
                if (results != null)
                {
                    _logger.LogInformation("Zaktualizowano właściwości zespołu {TeamId}", teamId);

                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 90, 
                        "Unieważnianie cache...");

                    // Invalidate cache
                    _cacheService.InvalidateTeamCache(teamId);

                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Właściwości zespołu zaktualizowane pomyślnie!");
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        $"✅ Właściwości zespołu zaktualizowane: {string.Join(", ", changes)}", "success");

                    return true;
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        "❌ Nie udało się zaktualizować właściwości zespołu", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Operacja zakończona niepowodzeniem");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd aktualizacji zespołu {TeamId}", teamId);
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    $"❌ Błąd podczas aktualizacji zespołu: {ex.Message}", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona błędem krytycznym");
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
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            var operationId = Guid.NewGuid().ToString();

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 5, 
                "Rozpoczynanie usuwania zespołu...");

            if (!_connectionService.ValidateRunspaceState())
            {
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "❌ Środowisko PowerShell nie jest gotowe", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona niepowodzeniem - błąd środowiska");
                return false;
            }

            if (string.IsNullOrEmpty(teamId))
            {
                _logger.LogError("TeamID nie może być puste.");
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "❌ ID zespołu nie może być puste", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona niepowodzeniem - brak ID zespołu");
                return false;
            }

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 30, 
                "Przygotowywanie do usunięcia...");

            _logger.LogInformation("Usuwanie zespołu {TeamId}", teamId);

            var parameters = new Dictionary<string, object>
            {
                { "GroupId", teamId },
                { "Confirm", false }
            };

            try
            {
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 70, 
                    "Usuwanie zespołu z Microsoft 365...");

                var results = await _connectionService.ExecuteCommandWithRetryAsync("Remove-MgGroup", parameters);
                
                if (results != null)
                {
                    _logger.LogInformation("Pomyślnie usunięto zespół {TeamId}", teamId);

                    // Invalidate cache
                    _cacheService.InvalidateTeamCache(teamId);
                    _cacheService.InvalidateAllCache();

                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Zespół usunięty pomyślnie!");
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        "✅ Zespół został usunięty pomyślnie", "success");

                    return true;
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        "❌ Nie udało się usunąć zespołu", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Operacja zakończona niepowodzeniem");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd usuwania zespołu {TeamId}", teamId);
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    $"❌ Błąd podczas usuwania zespołu: {ex.Message}", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona błędem krytycznym");
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
                var userId = await _cacheService.GetUserIdAsync(ownerUpn);
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
                _cacheService.InvalidateTeamCache(teamId);

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
                    _cacheService.InvalidateTeamCache(teamId);
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
                    _cacheService.InvalidateTeamCache(teamId);
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

        #endregion

        #region Private Methods

        private async Task<bool> UpdateTeamArchiveStateAsync(string teamId, bool archived)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            var operationId = Guid.NewGuid().ToString();
            var action = archived ? "archiwizacji" : "przywracania";

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 5, 
                $"Rozpoczynanie {action} zespołu...");

            if (string.IsNullOrEmpty(teamId))
            {
                _logger.LogError("TeamID nie może być puste.");
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "❌ ID zespołu nie może być puste", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona niepowodzeniem - brak ID zespołu");
                return false;
            }

            _logger.LogInformation("Rozpoczynanie {Action} zespołu {TeamId}", action, teamId);

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 50, 
                "Aktualizacja stanu zespołu...");

            var parameters = new Dictionary<string, object>
            {
                { "GroupId", teamId },
                { "IsArchived", archived }
            };

            try
            {
                var results = await _connectionService.ExecuteCommandWithRetryAsync("Update-MgTeam", parameters);
                
                if (results != null)
                {
                    _logger.LogInformation("Pomyślnie wykonano {Action} zespołu {TeamId}", action, teamId);

                    // Invalidate cache
                    _cacheService.InvalidateTeamCache(teamId);

                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        $"Zespół został {(archived ? "zarchiwizowany" : "przywrócony")} pomyślnie!");
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        $"✅ Zespół został {(archived ? "zarchiwizowany" : "przywrócony")} pomyślnie", "success");

                    return true;
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        $"❌ Nie udało się {(archived ? "zarchiwizować" : "przywrócić")} zespołu", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Operacja zakończona niepowodzeniem");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas {Action} zespołu {TeamId}", action, teamId);
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    $"❌ Błąd podczas {action} zespołu: {ex.Message}", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona błędem krytycznym");
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

        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id))
                operation.Id = Guid.NewGuid().ToString();

            if (string.IsNullOrEmpty(operation.CreatedBy))
                operation.CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system_powershell";

            if (operation.StartedAt == default(DateTime))
            {
                operation.StartedAt = DateTime.UtcNow;
            }

            if ((operation.Status == OperationStatus.Completed ||
                 operation.Status == OperationStatus.Failed ||
                 operation.Status == OperationStatus.Cancelled ||
                 operation.Status == OperationStatus.PartialSuccess) &&
                !operation.CompletedAt.HasValue)
            {
                operation.CompletedAt = DateTime.UtcNow;
                operation.Duration = operation.CompletedAt.Value - operation.StartedAt;
            }

            await _operationHistoryRepository.AddAsync(operation);
            _logger.LogDebug("Zapisano historię operacji ID: {OperationId} dla PowerShell.", operation.Id);
        }

        #endregion
    }
}