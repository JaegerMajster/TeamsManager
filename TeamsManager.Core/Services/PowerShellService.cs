// Plik: TeamsManager.Core/Services/PowerShellService.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System.Collections;
using System.Threading;
using Microsoft.Extensions.Primitives;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions;

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Serwis odpowiedzialny za interakcję z PowerShell, głównie w kontekście Microsoft Teams i Azure AD.
    /// Zoptymalizowany dla tenantów średniej wielkości (do 1000 użytkowników).
    /// </summary>
    public class PowerShellService : IPowerShellService
    {
        private readonly ILogger<PowerShellService> _logger;
        private readonly IMemoryCache _cache;
        private readonly ICurrentUserService _currentUserService;
        private readonly IOperationHistoryRepository _operationHistoryRepository;

        private Runspace? _runspace;
        private bool _isConnected = false;
        private bool _disposed = false;

        // Definicje kluczy cache - zgodnie z wzorcem z innych serwisów
        private const string GraphContextCacheKey = "PowerShell_GraphContext";
        private const string UserIdCacheKeyPrefix = "PowerShell_UserId_";
        private const string UserUpnCacheKeyPrefix = "PowerShell_UserUpn_";
        private const string TeamDetailsCacheKeyPrefix = "PowerShell_Team_";
        private const string AllTeamsCacheKey = "PowerShell_Teams_All";
        private const string TeamChannelsCacheKeyPrefix = "PowerShell_TeamChannels_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(15);
        private readonly TimeSpan _shortCacheDuration = TimeSpan.FromMinutes(5);

        // Token do zarządzania unieważnianiem wpisów cache
        private static CancellationTokenSource _powerShellCacheTokenSource = new CancellationTokenSource();

        // Stałe konfiguracyjne
        private const string DefaultUsageLocation = "PL";
        private const int BatchSize = 50;
        private const int MaxRetryAttempts = 3;

        // Semaphore dla kontroli współbieżności
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(3, 3);

        /// <summary>
        /// Konstruktor serwisu PowerShell.
        /// </summary>
        public PowerShellService(
            ILogger<PowerShellService> logger,
            IMemoryCache memoryCache,
            ICurrentUserService currentUserService,
            IOperationHistoryRepository operationHistoryRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));

            InitializeRunspace();
        }

        /// <summary>
        /// Zwraca domyślne opcje cache'a z tokenem unieważniania.
        /// </summary>
        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_powerShellCacheTokenSource.Token));
        }

        /// <summary>
        /// Zwraca krótkie opcje cache'a z tokenem unieważniania.
        /// </summary>
        private MemoryCacheEntryOptions GetShortCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_shortCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_powerShellCacheTokenSource.Token));
        }

        private void InitializeRunspace()
        {
            try
            {
                var initialSessionState = InitialSessionState.CreateDefault2();
                _runspace = RunspaceFactory.CreateRunspace(initialSessionState);
                _runspace.Open();
                _logger.LogInformation("Środowisko PowerShell zostało zainicjalizowane poprawnie.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie udało się zainicjalizować środowiska PowerShell. Próba inicjalizacji w trybie podstawowym.");
                _runspace = null;
                try
                {
                    _runspace = RunspaceFactory.CreateRunspace();
                    _runspace.Open();
                    _logger.LogInformation("Środowisko PowerShell zostało zainicjalizowane w trybie podstawowym.");
                }
                catch (Exception basicEx)
                {
                    _logger.LogError(basicEx, "Nie udało się zainicjalizować środowiska PowerShell nawet w trybie podstawowym.");
                    _runspace = null;
                }
            }
        }

        /// <inheritdoc />
        public bool IsConnected => _isConnected;

        /// <inheritdoc />
        public async Task<bool> ConnectWithAccessTokenAsync(string accessToken, string[]? scopes = null)
        {
            if (_runspace == null || _runspace.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                _logger.LogError("Nie można połączyć z Microsoft Graph: środowisko PowerShell nie jest poprawnie zainicjalizowane.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("Nie można połączyć z Microsoft Graph: token dostępu nie może być pusty.");
                return false;
            }

            return await Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("Próba połączenia z Microsoft Graph API. Scopes: [{Scopes}]",
                        scopes != null ? string.Join(", ", scopes) : "Brak");

                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;

                        // Import modułów
                        ps.AddScript(@"
                            Import-Module Microsoft.Graph.Authentication -ErrorAction SilentlyContinue
                            Import-Module Microsoft.Graph.Users -ErrorAction SilentlyContinue
                            Import-Module Microsoft.Graph.Teams -ErrorAction SilentlyContinue
                        ");
                        ps.Invoke();
                        ps.Commands.Clear();

                        // Połączenie
                        var command = ps.AddCommand("Connect-MgGraph")
                                       .AddParameter("AccessToken", accessToken)
                                       .AddParameter("ErrorAction", "Stop");

                        if (scopes?.Length > 0)
                        {
                            command.AddParameter("Scopes", scopes);
                        }

                        ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas łączenia: {Error}", error.ToString());
                            }
                            return false;
                        }

                        // Weryfikacja połączenia i cache kontekstu
                        ps.Commands.Clear();
                        var contextCheckResult = ps.AddCommand("Get-MgContext").Invoke();

                        if (!contextCheckResult.Any())
                        {
                            _logger.LogError("Połączenie z Microsoft Graph nie zostało ustanowione.");
                            return false;
                        }

                        _isConnected = true;

                        // Cache kontekstu
                        var context = contextCheckResult.First();
                        _cache.Set(GraphContextCacheKey, context, GetDefaultCacheEntryOptions());

                        _logger.LogInformation("Pomyślnie połączono z Microsoft Graph API.");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się połączyć z Microsoft Graph API.");
                    _isConnected = false;
                    return false;
                }
            });
        }

        /// <summary>
        /// Pobiera ID użytkownika z cache lub Graph.
        /// </summary>
        private async Task<string?> GetUserIdAsync(string userUpn, bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(userUpn))
            {
                _logger.LogWarning("Próba pobrania ID użytkownika z pustym UPN.");
                return null;
            }

            string cacheKey = UserIdCacheKeyPrefix + userUpn;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out string? cachedId))
            {
                _logger.LogDebug("ID użytkownika {UserUpn} znalezione w cache.", userUpn);
                return cachedId;
            }

            _logger.LogDebug("ID użytkownika {UserUpn} nie znalezione w cache lub wymuszono odświeżenie.", userUpn);

            try
            {
                var script = $@"
                    $user = Get-MgUser -UserId '{userUpn.Replace("'", "''")}' -ErrorAction Stop
                    if ($user) {{ $user.Id }} else {{ $null }}
                ";

                var results = await ExecuteScriptAsync(script);
                var userId = results?.FirstOrDefault()?.BaseObject?.ToString();

                if (!string.IsNullOrEmpty(userId))
                {
                    _cache.Set(cacheKey, userId, GetDefaultCacheEntryOptions());

                    // Cache też po UPN
                    string upnCacheKey = UserUpnCacheKeyPrefix + userUpn;
                    _cache.Set(upnCacheKey, userId, GetDefaultCacheEntryOptions());

                    _logger.LogDebug("ID użytkownika {UserUpn} zapisane w cache.", userUpn);
                }
                else
                {
                    // Cache negatywny wynik na krótko
                    _cache.Set(cacheKey, (string?)null, TimeSpan.FromMinutes(1));
                }

                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania ID użytkownika {UserUpn}", userUpn);
                return null;
            }
        }

        /// <summary>
        /// Pomocnicza metoda generująca skrypt pobierania ID użytkownika.
        /// </summary>
        private string GetUserIdScript(string userUpn)
        {
            return $@"
                $graphUser = Get-MgUser -UserId '{userUpn.Replace("'", "''")}' -ErrorAction Stop
                if (-not $graphUser) {{ throw 'Użytkownik nie znaleziony w Azure AD.' }}
                $userId = $graphUser.Id
            ";
        }

        /// <summary>
        /// Wykonuje komendę PowerShell z retry logic.
        /// </summary>
        private async Task<Collection<PSObject>?> ExecuteCommandWithRetryAsync(
            string commandName,
            Dictionary<string, object>? parameters = null,
            int maxRetries = MaxRetryAttempts)
        {
            if (!ValidateRunspaceState()) return null;

            int attempt = 0;
            Exception? lastException = null;

            while (attempt < maxRetries)
            {
                try
                {
                    attempt++;
                    _logger.LogDebug("Wykonywanie komendy PowerShell: {CommandName}, próba {Attempt}/{MaxRetries}",
                        commandName, attempt, maxRetries);

                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        var command = ps.AddCommand(commandName)
                                       .AddParameter("ErrorAction", "Stop");

                        if (parameters != null)
                        {
                            foreach (var param in parameters)
                            {
                                command.AddParameter(param.Key, param.Value);
                            }
                        }

                        var results = ps.Invoke();

                        if (ps.HadErrors)
                        {
                            var errors = string.Join("; ", ps.Streams.Error.Select(e => e.ToString()));
                            throw new InvalidOperationException($"PowerShell errors: {errors}");
                        }

                        _logger.LogDebug("Komenda '{CommandName}' wykonana. Wyniki: {Count}",
                            commandName, results.Count);
                        return results;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (IsTransientError(ex) && attempt < maxRetries)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // Exponential backoff
                        _logger.LogWarning(ex,
                            "Próba {Attempt}/{MaxRetries} wykonania komendy '{CommandName}' nie powiodła się. Ponawianie za {Delay}s",
                            attempt, maxRetries, commandName, delay.TotalSeconds);

                        await Task.Delay(delay);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            _logger.LogError(lastException, "Nie udało się wykonać komendy '{CommandName}' po {MaxRetries} próbach.",
                commandName, maxRetries);
            return null;
        }

        /// <summary>
        /// Sprawdza czy błąd jest przejściowy i warto ponawiać.
        /// </summary>
        private bool IsTransientError(Exception ex)
        {
            return ex.Message.Contains("throttl", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("temporarily", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Waliduje stan runspace.
        /// </summary>
        private bool ValidateRunspaceState()
        {
            if (_runspace == null || _runspace.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                _logger.LogError("Środowisko PowerShell nie jest zainicjalizowane.");
                return false;
            }

            if (!_isConnected)
            {
                _logger.LogWarning("Brak aktywnego połączenia z Microsoft Graph.");
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public async Task<string?> CreateTeamAsync(
            string displayName,
            string description,
            string ownerUpn,
            TeamVisibility visibility = TeamVisibility.Private,
            string? template = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_create_team";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.TeamCreated,
                TargetEntityType = "Team",
                TargetEntityName = displayName,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                if (!ValidateRunspaceState())
                {
                    operation.MarkAsFailed("Środowisko PowerShell nie jest gotowe.");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(ownerUpn))
                {
                    operation.MarkAsFailed("DisplayName i OwnerUpn są wymagane.");
                    _logger.LogError("DisplayName i OwnerUpn są wymagane.");
                    return null;
                }

                // Pobierz ID właściciela z cache lub Graph
                var ownerId = await GetUserIdAsync(ownerUpn);
                if (string.IsNullOrEmpty(ownerId))
                {
                    operation.MarkAsFailed($"Nie znaleziono właściciela {ownerUpn}");
                    _logger.LogError("Nie znaleziono właściciela {OwnerUpn}", ownerUpn);
                    return null;
                }

                _logger.LogInformation("Tworzenie zespołu '{DisplayName}' dla właściciela {OwnerUpn}",
                    displayName, ownerUpn);

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

                var results = await ExecuteScriptAsync(scriptBuilder.ToString());
                var teamId = results?.FirstOrDefault()?.BaseObject?.ToString();

                if (!string.IsNullOrEmpty(teamId))
                {
                    operation.TargetEntityId = teamId;
                    operation.MarkAsCompleted($"Zespół utworzony z ID: {teamId}");
                    _logger.LogInformation("Utworzono zespół '{DisplayName}' o ID: {TeamId}",
                        displayName, teamId);

                    // Invalidate cache
                    InvalidatePowerShellCache(teamId: teamId);
                }
                else
                {
                    operation.MarkAsFailed("Nie otrzymano ID zespołu.");
                }

                return teamId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd tworzenia zespołu '{DisplayName}'", displayName);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return null;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <summary>
        /// Mapuje nazwy szablonów na identyfikatory Graph.
        /// </summary>
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

        /// <inheritdoc />
        public async Task<bool> UpdateTeamPropertiesAsync(
            string teamId,
            string? newDisplayName = null,
            string? newDescription = null,
            TeamVisibility? newVisibility = null)
        {
            if (!ValidateRunspaceState()) return false;

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

            if (newDisplayName != null) parameters.Add("DisplayName", newDisplayName);
            if (newDescription != null) parameters.Add("Description", newDescription);
            if (newVisibility.HasValue) parameters.Add("Visibility", newVisibility.Value.ToString());

            try
            {
                var results = await ExecuteCommandWithRetryAsync("Update-MgTeam", parameters);
                _logger.LogInformation("Zaktualizowano właściwości zespołu {TeamId}", teamId);

                // Invalidate cache
                InvalidatePowerShellCache(teamId: teamId);

                return results != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd aktualizacji zespołu {TeamId}", teamId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ArchiveTeamAsync(string teamId)
        {
            if (!ValidateRunspaceState()) return false;

            return await UpdateTeamArchiveStateAsync(teamId, true);
        }

        /// <inheritdoc />
        public async Task<bool> UnarchiveTeamAsync(string teamId)
        {
            if (!ValidateRunspaceState()) return false;

            return await UpdateTeamArchiveStateAsync(teamId, false);
        }

        /// <summary>
        /// Pomocnicza metoda do zmiany stanu archiwizacji.
        /// </summary>
        private async Task<bool> UpdateTeamArchiveStateAsync(string teamId, bool archived)
        {
            if (string.IsNullOrEmpty(teamId))
            {
                _logger.LogError("TeamID nie może być puste.");
                return false;
            }

            var action = archived ? "archiwizacji" : "przywracania";
            _logger.LogInformation("Rozpoczynanie {Action} zespołu {TeamId}", action, teamId);

            var parameters = new Dictionary<string, object>
            {
                { "GroupId", teamId },
                { "IsArchived", archived }
            };

            try
            {
                var results = await ExecuteCommandWithRetryAsync("Update-MgTeam", parameters);
                _logger.LogInformation("Pomyślnie wykonano {Action} zespołu {TeamId}", action, teamId);

                // Invalidate cache
                InvalidatePowerShellCache(teamId: teamId);

                return results != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas {Action} zespołu {TeamId}", action, teamId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteTeamAsync(string teamId)
        {
            if (!ValidateRunspaceState()) return false;

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
                var results = await ExecuteCommandWithRetryAsync("Remove-MgGroup", parameters);
                _logger.LogInformation("Pomyślnie usunięto zespół {TeamId}", teamId);

                // Invalidate cache
                InvalidatePowerShellCache(teamId: teamId, invalidateAll: true);

                return results != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd usuwania zespołu {TeamId}", teamId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> AddUserToTeamAsync(string teamId, string userUpn, string role)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_add_user";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.MemberAdded,
                TargetEntityType = "TeamMember",
                TargetEntityId = teamId,
                TargetEntityName = $"{userUpn} -> Team {teamId}",
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                if (!ValidateRunspaceState())
                {
                    operation.MarkAsFailed("Środowisko PowerShell nie jest gotowe.");
                    return false;
                }

                if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(userUpn) || string.IsNullOrEmpty(role))
                {
                    operation.MarkAsFailed("TeamID, UserUPN i Role są wymagane.");
                    _logger.LogError("TeamID, UserUPN i Role są wymagane.");
                    return false;
                }

                if (!role.Equals("Owner", StringComparison.OrdinalIgnoreCase) &&
                    !role.Equals("Member", StringComparison.OrdinalIgnoreCase))
                {
                    operation.MarkAsFailed($"Nieprawidłowa rola '{role}'.");
                    _logger.LogError("Nieprawidłowa rola '{Role}'. Dozwolone: Owner, Member.", role);
                    return false;
                }

                // Pobierz ID użytkownika z cache
                var userId = await GetUserIdAsync(userUpn);
                if (string.IsNullOrEmpty(userId))
                {
                    operation.MarkAsFailed($"Nie znaleziono użytkownika {userUpn}");
                    _logger.LogError("Nie znaleziono użytkownika {UserUpn}", userUpn);
                    return false;
                }

                _logger.LogInformation("Dodawanie użytkownika {UserUpn} do zespołu {TeamId} jako {Role}",
                    userUpn, teamId, role);

                var cmdlet = role.Equals("Owner", StringComparison.OrdinalIgnoreCase)
                    ? "Add-MgTeamOwner"
                    : "Add-MgTeamMember";

                var script = $"{cmdlet} -TeamId '{teamId}' -UserId '{userId}' -ErrorAction Stop";
                var results = await ExecuteScriptAsync(script);

                if (results != null)
                {
                    operation.MarkAsCompleted("Użytkownik dodany do zespołu.");
                    _logger.LogInformation("Pomyślnie dodano użytkownika {UserUpn} do zespołu {TeamId}",
                        userUpn, teamId);

                    // Invalidate team cache
                    InvalidatePowerShellCache(teamId: teamId);
                    return true;
                }
                else
                {
                    operation.MarkAsFailed("Błąd podczas dodawania użytkownika.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd dodawania użytkownika {UserUpn} do zespołu {TeamId}",
                    userUpn, teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveUserFromTeamAsync(string teamId, string userUpn)
        {
            if (!ValidateRunspaceState()) return false;

            if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(userUpn))
            {
                _logger.LogError("TeamID i UserUPN są wymagane.");
                return false;
            }

            var userId = await GetUserIdAsync(userUpn);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError("Nie znaleziono użytkownika {UserUpn}", userUpn);
                return false;
            }

            _logger.LogInformation("Usuwanie użytkownika {UserUpn} z zespołu {TeamId}", userUpn, teamId);

            try
            {
                var script = $@"
                    $teamId = '{teamId.Replace("'", "''")}'
                    $userId = '{userId}'
                    
                    $isOwner = (Get-MgTeamOwner -TeamId $teamId | Where-Object Id -eq $userId) -ne $null
                    $isMember = (Get-MgTeamMember -TeamId $teamId | Where-Object Id -eq $userId) -ne $null
                    
                    if ($isOwner) {{
                        Remove-MgTeamOwner -TeamId $teamId -UserId $userId -Confirm:$false -ErrorAction Stop
                    }} elseif ($isMember) {{
                        Remove-MgTeamMember -TeamId $teamId -UserId $userId -Confirm:$false -ErrorAction Stop
                    }}
                    
                    $true
                ";

                var results = await ExecuteScriptAsync(script);
                var success = results?.FirstOrDefault()?.BaseObject as bool? ?? false;

                if (success)
                {
                    _logger.LogInformation("Pomyślnie usunięto użytkownika {UserUpn} z zespołu {TeamId}",
                        userUpn, teamId);

                    // Invalidate cache
                    InvalidatePowerShellCache(teamId: teamId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd usuwania użytkownika {UserUpn} z zespołu {TeamId}",
                    userUpn, teamId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<string?> CreateM365UserAsync(
            string displayName,
            string userPrincipalName,
            string password,
            string? usageLocation = null,
            List<string>? licenseSkuIds = null,
            bool accountEnabled = true)
        {
            if (!ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(userPrincipalName) ||
                string.IsNullOrWhiteSpace(password))
            {
                _logger.LogError("DisplayName, UserPrincipalName i Password są wymagane.");
                return null;
            }

            // Użyj domyślnej lokalizacji jeśli nie podano
            usageLocation ??= DefaultUsageLocation;

            _logger.LogInformation("Tworzenie użytkownika M365: {UserPrincipalName}", userPrincipalName);

            try
            {
                var script = $@"
                    $passwordProfile = @{{
                        password = '{password.Replace("'", "''")}'
                        forceChangePasswordNextSignIn = $false
                    }}
                    
                    $user = New-MgUser `
                        -DisplayName '{displayName.Replace("'", "''")}' `
                        -UserPrincipalName '{userPrincipalName.Replace("'", "''")}' `
                        -MailNickname '{userPrincipalName.Split('@')[0].Replace("'", "''")}' `
                        -PasswordProfile $passwordProfile `
                        -AccountEnabled ${accountEnabled} `
                        -UsageLocation '{usageLocation.Replace("'", "''")}' `
                        -ErrorAction Stop
                    
                    $user.Id
                ";

                var results = await ExecuteScriptAsync(script);
                var userId = results?.FirstOrDefault()?.BaseObject?.ToString();

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("Nie udało się utworzyć użytkownika {UserPrincipalName}",
                        userPrincipalName);
                    return null;
                }

                // Przypisz licencje jeśli podano
                if (licenseSkuIds?.Count > 0)
                {
                    await AssignLicensesToUserAsync(userId, licenseSkuIds);
                }

                _logger.LogInformation("Utworzono użytkownika {UserPrincipalName} o ID: {UserId}",
                    userPrincipalName, userId);

                // Invalidate user cache
                InvalidatePowerShellCache(userId: userId, userUpn: userPrincipalName);

                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd tworzenia użytkownika {UserPrincipalName}", userPrincipalName);
                return null;
            }
        }

        /// <summary>
        /// Pomocnicza metoda do przypisywania wielu licencji.
        /// </summary>
        private async Task<bool> AssignLicensesToUserAsync(string userId, List<string> licenseSkuIds)
        {
            try
            {
                var addLicenses = string.Join(",",
                    licenseSkuIds.Select(id => $"@{{SkuId='{id}'}}"));

                var script = $@"
                    Set-MgUserLicense -UserId '{userId}' `
                        -AddLicenses @({addLicenses}) `
                        -RemoveLicenses @() `
                        -ErrorAction Stop
                ";

                await ExecuteScriptAsync(script);
                _logger.LogInformation("Przypisano {Count} licencji do użytkownika {UserId}",
                    licenseSkuIds.Count, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd przypisywania licencji do użytkownika {UserId}", userId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> SetM365UserAccountStateAsync(string userPrincipalName, bool isEnabled)
        {
            if (!ValidateRunspaceState()) return false;

            if (string.IsNullOrWhiteSpace(userPrincipalName))
            {
                _logger.LogError("UserPrincipalName jest wymagany.");
                return false;
            }

            _logger.LogInformation("Zmiana stanu konta {UserPrincipalName} na: {IsEnabled}",
                userPrincipalName, isEnabled ? "włączone" : "wyłączone");

            var parameters = new Dictionary<string, object>
            {
                { "UserId", userPrincipalName },
                { "AccountEnabled", isEnabled }
            };

            try
            {
                var results = await ExecuteCommandWithRetryAsync("Update-MgUser", parameters);

                // Invalidate user cache
                InvalidatePowerShellCache(userUpn: userPrincipalName);

                return results != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd zmiany stanu konta {UserPrincipalName}", userPrincipalName);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateM365UserPrincipalNameAsync(string currentUpn, string newUpn)
        {
            if (!ValidateRunspaceState()) return false;

            if (string.IsNullOrWhiteSpace(currentUpn) || string.IsNullOrWhiteSpace(newUpn))
            {
                _logger.LogError("currentUpn i newUpn są wymagane.");
                return false;
            }

            _logger.LogInformation("Aktualizacja UPN użytkownika z '{CurrentUpn}' na '{NewUpn}'", currentUpn, newUpn);

            var parameters = new Dictionary<string, object>
            {
                { "UserId", currentUpn },
                { "UserPrincipalName", newUpn }
            };

            try
            {
                var results = await ExecuteCommandWithRetryAsync("Update-MgUser", parameters);

                // Invalidate cache for both UPNs
                InvalidatePowerShellCache(userUpn: currentUpn);
                InvalidatePowerShellCache(userUpn: newUpn);

                return results != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd aktualizacji UPN użytkownika z '{CurrentUpn}' na '{NewUpn}'", currentUpn, newUpn);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateM365UserPropertiesAsync(
            string userUpn,
            string? department = null,
            string? jobTitle = null,
            string? firstName = null,
            string? lastName = null)
        {
            if (!ValidateRunspaceState()) return false;

            if (string.IsNullOrWhiteSpace(userUpn))
            {
                _logger.LogError("userUpn jest wymagany.");
                return false;
            }

            // Sprawdzenie czy są jakiekolwiek zmiany do wprowadzenia
            if (department == null && jobTitle == null && firstName == null && lastName == null)
            {
                _logger.LogInformation("Brak właściwości do aktualizacji dla użytkownika: {UserUpn}.", userUpn);
                return true;
            }

            _logger.LogInformation("Aktualizacja właściwości użytkownika: {UserUpn}", userUpn);

            var parameters = new Dictionary<string, object>
            {
                { "UserId", userUpn }
            };

            if (!string.IsNullOrWhiteSpace(department))
                parameters.Add("Department", department);
            if (!string.IsNullOrWhiteSpace(jobTitle))
                parameters.Add("JobTitle", jobTitle);
            if (!string.IsNullOrWhiteSpace(firstName))
                parameters.Add("GivenName", firstName);
            if (!string.IsNullOrWhiteSpace(lastName))
                parameters.Add("Surname", lastName);

            try
            {
                var results = await ExecuteCommandWithRetryAsync("Update-MgUser", parameters);

                // Invalidate user cache
                InvalidatePowerShellCache(userUpn: userUpn);

                return results != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd aktualizacji właściwości użytkownika: {UserUpn}", userUpn);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<PSObject?> GetTeamChannelAsync(string teamId, string channelDisplayName)
        {
            if (!ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(channelDisplayName))
            {
                _logger.LogError("TeamID i ChannelDisplayName są wymagane.");
                return null;
            }

            _logger.LogInformation("Pobieranie kanału '{ChannelDisplayName}' dla zespołu {TeamId}", channelDisplayName, teamId);

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
                    _logger.LogInformation("Kanał '{ChannelDisplayName}' nie znaleziony w zespole {TeamId}", channelDisplayName, teamId);
                }

                return foundChannel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania kanału '{ChannelDisplayName}' dla zespołu {TeamId}", channelDisplayName, teamId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<PSObject?> CreateTeamChannelAsync(string teamId, string displayName, bool isPrivate = false, string? description = null)
        {
            if (!ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(displayName))
            {
                _logger.LogError("TeamID i DisplayName są wymagane.");
                return null;
            }

            _logger.LogInformation("Tworzenie kanału '{DisplayName}' w zespole {TeamId}. Prywatny: {IsPrivate}", displayName, teamId, isPrivate);

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

                var results = await ExecuteCommandWithRetryAsync("New-MgTeamChannel", parameters);

                // Invalidate channels cache for this team
                InvalidatePowerShellCache(teamId: teamId);

                return results?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie udało się utworzyć kanału '{DisplayName}' w zespole {TeamId}", displayName, teamId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateTeamChannelAsync(
            string teamId,
            string channelId, // ZMIANA: Zamiast currentDisplayName
            string? newDisplayName = null,
            string? newDescription = null)
        {
            if (!ValidateRunspaceState()) return false;

            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(channelId)) // ZMIANA: Sprawdzenie channelId
            {
                _logger.LogError("TeamID i ChannelID są wymagane do aktualizacji kanału.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(newDisplayName) && newDescription == null) // newDescription może być pusty, jeśli chcemy wyczyścić
            {
                _logger.LogInformation("Brak właściwości do aktualizacji dla kanału ID '{ChannelId}' w zespole {TeamId}.", channelId, teamId);
                return true;
            }

            _logger.LogInformation("Aktualizowanie kanału ID '{ChannelId}' w zespole {TeamId}. Nowa nazwa: '{NewDisplayName}', Nowy opis: '{NewDescription}'",
                channelId, teamId, newDisplayName ?? "bez zmian", newDescription ?? "bez zmian");

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "TeamId", teamId },
                    { "ChannelId", channelId } // Używamy ChannelId
                };

                if (!string.IsNullOrWhiteSpace(newDisplayName))
                {
                    parameters.Add("DisplayName", newDisplayName);
                }

                if (newDescription != null) // Pozwalamy na ustawienie pustego opisu
                {
                    parameters.Add("Description", newDescription);
                }

                var results = await ExecuteCommandWithRetryAsync("Update-MgTeamChannel", parameters);

                if (results != null)
                {
                    InvalidatePowerShellCache(teamId: teamId); // Unieważnij cache kanałów dla tego zespołu
                    _logger.LogInformation("Pomyślnie zaktualizowano kanał ID '{ChannelId}' w zespole {TeamId}.", channelId, teamId);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie udało się zaktualizować kanału ID '{ChannelId}' w zespole {TeamId}", channelId, teamId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveTeamChannelAsync(
            string teamId,
            string channelId) // ZMIANA: Zamiast channelDisplayName
        {
            if (!ValidateRunspaceState()) return false;

            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(channelId)) // ZMIANA: Sprawdzenie channelId
            {
                _logger.LogError("TeamID i ChannelID są wymagane do usunięcia kanału.");
                return false;
            }

            // UWAGA: Logika sprawdzająca, czy kanał jest "General" lub "Ogólny"
            // powinna być teraz zrealizowana w serwisie wywołującym (ChannelService)
            // przed wywołaniem tej metody, na podstawie `DisplayName` pobranego np. z `GetTeamChannelAsync`.
            // PowerShellService operuje teraz na `channelId`.
            _logger.LogInformation("Usuwanie kanału ID '{ChannelId}' z zespołu {TeamId}", channelId, teamId);

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "TeamId", teamId },
                    { "ChannelId", channelId }, // Używamy ChannelId
                    { "Confirm", false } // Pomija potwierdzenie w PowerShell
                };

                var results = await ExecuteCommandWithRetryAsync("Remove-MgTeamChannel", parameters);

                if (results != null)
                {
                    InvalidatePowerShellCache(teamId: teamId); // Unieważnij cache kanałów dla tego zespołu
                    _logger.LogInformation("Pomyślnie usunięto kanał ID '{ChannelId}' z zespołu {TeamId}.", channelId, teamId);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie udało się usunąć kanału ID '{ChannelId}' z zespołu {TeamId}", channelId, teamId);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<Collection<PSObject>?> GetTeamChannelsAsync(string teamId)
        {
            if (!ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogError("TeamID nie może być puste.");
                return null;
            }

            string cacheKey = TeamChannelsCacheKeyPrefix + teamId;

            if (_cache.TryGetValue(cacheKey, out Collection<PSObject>? cachedChannels))
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

                var results = await ExecuteCommandWithRetryAsync("Get-MgTeamChannel", parameters);

                if (results != null)
                {
                    _cache.Set(cacheKey, results, GetShortCacheEntryOptions());
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania kanałów dla zespołu {TeamId}", teamId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<PSObject?> GetTeamAsync(string teamId)
        {
            if (!ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogError("TeamID nie może być puste.");
                return null;
            }

            string cacheKey = TeamDetailsCacheKeyPrefix + teamId;

            if (_cache.TryGetValue(cacheKey, out PSObject? cachedTeam))
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

                var results = await ExecuteCommandWithRetryAsync("Get-MgTeam", parameters);
                var team = results?.FirstOrDefault();

                if (team != null)
                {
                    _cache.Set(cacheKey, team, GetDefaultCacheEntryOptions());
                }

                return team;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania zespołu {TeamId}", teamId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<Collection<PSObject>?> GetAllTeamsAsync()
        {
            if (!ValidateRunspaceState()) return null;

            if (_cache.TryGetValue(AllTeamsCacheKey, out Collection<PSObject>? cachedTeams))
            {
                _logger.LogDebug("Wszystkie zespoły znalezione w cache.");
                return cachedTeams;
            }

            _logger.LogInformation("Pobieranie wszystkich zespołów");

            try
            {
                var results = await ExecuteCommandWithRetryAsync("Get-MgTeam");

                if (results != null)
                {
                    _cache.Set(AllTeamsCacheKey, results, GetDefaultCacheEntryOptions());
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania wszystkich zespołów");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<Collection<PSObject>?> GetTeamsByOwnerAsync(string ownerUpn)
        {
            if (!ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(ownerUpn))
            {
                _logger.LogError("OwnerUpn nie może być puste.");
                return null;
            }

            _logger.LogInformation("Pobieranie zespołów dla właściciela: {OwnerUpn}", ownerUpn);

            try
            {
                var userId = await GetUserIdAsync(ownerUpn);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("Nie znaleziono użytkownika {OwnerUpn}", ownerUpn);
                    return null;
                }

                var script = $"Get-MgUserOwnedTeam -UserId '{userId}' -ErrorAction Stop";
                return await ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania zespołów dla właściciela {OwnerUpn}", ownerUpn);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<Collection<PSObject>?> GetTeamMembersAsync(string teamId)
        {
            if (!ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogError("TeamID nie może być puste.");
                return null;
            }

            _logger.LogInformation("Pobieranie wszystkich członków zespołu {TeamId}", teamId);

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "TeamId", teamId }
                };

                return await ExecuteCommandWithRetryAsync("Get-MgTeamMember", parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania członków zespołu {TeamId}", teamId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<PSObject?> GetTeamMemberAsync(string teamId, string userUpn)
        {
            if (!ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(userUpn))
            {
                _logger.LogError("TeamID i UserUpn są wymagane.");
                return null;
            }

            _logger.LogInformation("Pobieranie członka {UserUpn} z zespołu {TeamId}", userUpn, teamId);

            try
            {
                var userId = await GetUserIdAsync(userUpn);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("Nie znaleziono użytkownika {UserUpn}", userUpn);
                    return null;
                }

                var script = $"Get-MgTeamMember -TeamId '{teamId}' -UserId '{userId}' -ErrorAction Stop";
                var results = await ExecuteScriptAsync(script);
                return results?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania członka {UserUpn} z zespołu {TeamId}", userUpn, teamId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> AssignLicenseToUserAsync(string userUpn, string licenseSkuId)
        {
            if (!ValidateRunspaceState()) return false;

            if (string.IsNullOrWhiteSpace(userUpn) || string.IsNullOrWhiteSpace(licenseSkuId))
            {
                _logger.LogError("UserUpn i LicenseSkuId są wymagane.");
                return false;
            }

            _logger.LogInformation("Przypisywanie licencji {LicenseSkuId} do użytkownika {UserUpn}", licenseSkuId, userUpn);

            try
            {
                var userId = await GetUserIdAsync(userUpn);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("Nie znaleziono użytkownika {UserUpn}", userUpn);
                    return false;
                }

                var script = $@"
                    $addLicenses = @(@{{SkuId='{licenseSkuId}'}})
                    Set-MgUserLicense -UserId '{userId}' -AddLicenses $addLicenses -RemoveLicenses @() -ErrorAction Stop
                    $true
                ";

                var results = await ExecuteScriptAsync(script);
                var success = results?.FirstOrDefault()?.BaseObject as bool? ?? false;

                if (success)
                {
                    _logger.LogInformation("Pomyślnie przypisano licencję {LicenseSkuId} do użytkownika {UserUpn}", licenseSkuId, userUpn);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd przypisywania licencji {LicenseSkuId} do użytkownika {UserUpn}", licenseSkuId, userUpn);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveLicenseFromUserAsync(string userUpn, string licenseSkuId)
        {
            if (!ValidateRunspaceState()) return false;

            if (string.IsNullOrWhiteSpace(userUpn) || string.IsNullOrWhiteSpace(licenseSkuId))
            {
                _logger.LogError("UserUpn i LicenseSkuId są wymagane.");
                return false;
            }

            _logger.LogInformation("Usuwanie licencji {LicenseSkuId} od użytkownika {UserUpn}", licenseSkuId, userUpn);

            try
            {
                var userId = await GetUserIdAsync(userUpn);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("Nie znaleziono użytkownika {UserUpn}", userUpn);
                    return false;
                }

                var script = $@"
                    $removeLicenses = @('{licenseSkuId}')
                    Set-MgUserLicense -UserId '{userId}' -AddLicenses @() -RemoveLicenses $removeLicenses -ErrorAction Stop
                    $true
                ";

                var results = await ExecuteScriptAsync(script);
                var success = results?.FirstOrDefault()?.BaseObject as bool? ?? false;

                if (success)
                {
                    _logger.LogInformation("Pomyślnie usunięto licencję {LicenseSkuId} od użytkownika {UserUpn}", licenseSkuId, userUpn);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd usuwania licencji {LicenseSkuId} od użytkownika {UserUpn}", licenseSkuId, userUpn);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<Collection<PSObject>?> GetUserLicensesAsync(string userUpn)
        {
            if (!ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(userUpn))
            {
                _logger.LogError("UserUpn jest wymagany.");
                return null;
            }

            _logger.LogInformation("Pobieranie licencji dla użytkownika {UserUpn}", userUpn);

            try
            {
                var userId = await GetUserIdAsync(userUpn);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("Nie znaleziono użytkownika {UserUpn}", userUpn);
                    return null;
                }

                var script = $"Get-MgUserLicenseDetail -UserId '{userId}' -ErrorAction Stop";
                return await ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania licencji dla użytkownika {UserUpn}", userUpn);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<Collection<PSObject>?> GetAllUsersAsync(string? filter = null)
        {
            if (!ValidateRunspaceState()) return null;

            _logger.LogInformation("Pobieranie wszystkich użytkowników. Filtr: '{Filter}'", filter ?? "Brak");

            try
            {
                // Dla 1000 użytkowników paginacja jest opcjonalna, ale dodajmy ją dla skalowalności
                var script = new StringBuilder();
                script.AppendLine("$allUsers = @()");
                script.AppendLine("$pageSize = 999"); // Max dla Graph
                script.AppendLine("$uri = 'https://graph.microsoft.com/v1.0/users?$top=' + $pageSize");

                if (!string.IsNullOrEmpty(filter))
                {
                    script.AppendLine($"$uri += '&$filter={Uri.EscapeDataString(filter)}'");
                }

                script.AppendLine(@"
                    do {
                        $response = Invoke-MgGraphRequest -Uri $uri -Method GET
                        $allUsers += $response.value
                        $uri = $response.'@odata.nextLink'
                    } while ($uri)
                    
                    $allUsers | ForEach-Object { 
                        [PSCustomObject]$_ 
                    }
                ");

                return await ExecuteScriptAsync(script.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania użytkowników");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<Collection<PSObject>?> GetInactiveUsersAsync(int daysInactive)
        {
            if (!ValidateRunspaceState()) return null;

            if (daysInactive < 0)
            {
                _logger.LogError("Liczba dni nieaktywności nie może być ujemna.");
                return null;
            }

            _logger.LogInformation("Pobieranie użytkowników nieaktywnych przez {Days} dni", daysInactive);

            try
            {
                // Sprawdź uprawnienia
                var hasPermission = await CheckGraphPermissionAsync("AuditLog.Read.All");
                if (!hasPermission)
                {
                    _logger.LogWarning("Brak uprawnień AuditLog.Read.All. SignInActivity może być niedostępne.");
                }

                var script = $@"
                    $inactiveThreshold = (Get-Date).AddDays(-{daysInactive})
                    $users = Get-MgUser -All -Property Id,UserPrincipalName,DisplayName,SignInActivity,AccountEnabled -PageSize 999
                    
                    $inactiveUsers = $users | Where-Object {{
                        -not $_.SignInActivity -or 
                        $_.SignInActivity.LastSignInDateTime -lt $inactiveThreshold
                    }}
                    
                    $inactiveUsers | Select-Object Id, UserPrincipalName, DisplayName, AccountEnabled,
                        @{{N='LastSignInDateTime'; E={{
                            if ($_.SignInActivity) {{ 
                                $_.SignInActivity.LastSignInDateTime 
                            }} else {{ 
                                'Never' 
                            }}
                        }}}}
                ";

                return await ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania nieaktywnych użytkowników");
                return null;
            }
        }

        /// <summary>
        /// Sprawdza czy aplikacja ma określone uprawnienie Graph.
        /// </summary>
        private async Task<bool> CheckGraphPermissionAsync(string permission)
        {
            try
            {
                var script = "(Get-MgContext).Scopes -contains '" + permission + "'";
                var results = await ExecuteScriptAsync(script);
                return results?.FirstOrDefault()?.BaseObject as bool? ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, bool>> BulkAddUsersToTeamAsync(
            string teamId,
            List<string> userUpns,
            string role = "Member")
        {
            if (!ValidateRunspaceState())
                return new Dictionary<string, bool>();

            if (!userUpns?.Any() ?? true)
            {
                _logger.LogWarning("Lista użytkowników jest pusta.");
                return new Dictionary<string, bool>();
            }

            _logger.LogInformation("Masowe dodawanie {Count} użytkowników do zespołu {TeamId}",
                userUpns!.Count, teamId);

            var results = new Dictionary<string, bool>();

            // Podziel na partie
            var batches = userUpns
                .Select((upn, index) => new { upn, index })
                .GroupBy(x => x.index / BatchSize)
                .Select(g => g.Select(x => x.upn).ToList());

            foreach (var batch in batches)
            {
                await _semaphore.WaitAsync(); // Kontrola współbieżności
                try
                {
                    var batchResults = await ProcessUserBatchAsync(teamId, batch, role);
                    foreach (var result in batchResults)
                    {
                        results[result.Key] = result.Value;
                    }
                }
                finally
                {
                    _semaphore.Release();
                }

                // Krótka przerwa między partiami
                if (batch != batches.Last())
                {
                    await Task.Delay(1000);
                }
            }

            _logger.LogInformation("Zakończono masowe dodawanie. Sukcesy: {Success}, Błędy: {Failed}",
                results.Count(r => r.Value), results.Count(r => !r.Value));

            // Invalidate cache dla zespołu
            InvalidatePowerShellCache(teamId: teamId);

            return results;
        }

        /// <summary>
        /// Przetwarza pojedynczą partię użytkowników.
        /// </summary>
        private async Task<Dictionary<string, bool>> ProcessUserBatchAsync(
            string teamId,
            List<string> userUpns,
            string role)
        {
            var scriptBuilder = new StringBuilder();
            scriptBuilder.AppendLine($"$teamId = '{teamId}'");
            scriptBuilder.AppendLine("$results = @{}");
            scriptBuilder.AppendLine();

            // Pobierz ID wszystkich użytkowników w partii
            scriptBuilder.AppendLine("$userIds = @{}");
            foreach (var upn in userUpns)
            {
                // Sprawdź cache przed dodaniem do skryptu
                var cachedUserId = await GetUserIdAsync(upn);
                if (!string.IsNullOrEmpty(cachedUserId))
                {
                    scriptBuilder.AppendLine($"$userIds['{upn.Replace("'", "''")}'] = '{cachedUserId}'");
                }
                else
                {
                    scriptBuilder.AppendLine($@"
                        try {{
                            $user = Get-MgUser -UserId '{upn.Replace("'", "''")}' -ErrorAction Stop
                            $userIds['{upn.Replace("'", "''")}'] = $user.Id
                        }} catch {{
                            $results['{upn.Replace("'", "''")}'] = $false
                            Write-Warning ""Użytkownik {upn} nie znaleziony""
                        }}
                    ");
                }
            }

            // Dodaj użytkowników do zespołu
            var cmdlet = role.Equals("Owner", StringComparison.OrdinalIgnoreCase)
                ? "Add-MgTeamOwner"
                : "Add-MgTeamMember";

            scriptBuilder.AppendLine($@"
                foreach ($upn in $userIds.Keys) {{
                    try {{
                        {cmdlet} -TeamId $teamId -UserId $userIds[$upn] -ErrorAction Stop
                        $results[$upn] = $true
                    }} catch {{
                        $results[$upn] = $false
                        Write-Warning ""Błąd dodawania $upn : $_""
                    }}
                }}
                $results
            ");

            var scriptResults = await ExecuteScriptAsync(scriptBuilder.ToString());
            var results = new Dictionary<string, bool>();

            if (scriptResults?.FirstOrDefault()?.BaseObject is Hashtable hashtable)
            {
                foreach (DictionaryEntry entry in hashtable)
                {
                    if (entry.Key?.ToString() is string key && entry.Value is bool value)
                    {
                        results[key] = value;
                    }
                }
            }

            return results;
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, bool>> BulkArchiveTeamsAsync(List<string> teamIds)
        {
            if (!ValidateRunspaceState())
                return new Dictionary<string, bool>();

            if (!teamIds?.Any() ?? true)
            {
                _logger.LogWarning("Lista zespołów jest pusta.");
                return new Dictionary<string, bool>();
            }

            _logger.LogInformation("Masowa archiwizacja {Count} zespołów", teamIds!.Count);

            var script = new StringBuilder();
            script.AppendLine("$results = @{}");

            foreach (var teamId in teamIds)
            {
                script.AppendLine($@"
                    try {{
                        Update-MgTeam -GroupId '{teamId}' -IsArchived $true -ErrorAction Stop
                        $results['{teamId}'] = $true
                    }} catch {{
                        $results['{teamId}'] = $false
                        Write-Warning ""Błąd archiwizacji zespołu {teamId}: $_""
                    }}
                ");
            }

            script.AppendLine("$results");

            var scriptResults = await ExecuteScriptAsync(script.ToString());
            var results = new Dictionary<string, bool>();

            if (scriptResults?.FirstOrDefault()?.BaseObject is Hashtable hashtable)
            {
                foreach (DictionaryEntry entry in hashtable)
                {
                    if (entry.Key?.ToString() is string key && entry.Value is bool value)
                    {
                        results[key] = value;
                    }
                }
            }

            _logger.LogInformation("Zakończono archiwizację. Sukcesy: {Success}, Błędy: {Failed}",
                results.Count(r => r.Value), results.Count(r => !r.Value));

            // Invalidate cache dla wszystkich zespołów
            InvalidatePowerShellCache(invalidateAll: true);

            return results;
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, bool>> BulkRemoveUsersFromTeamAsync(
            string teamId,
            List<string> userUpns)
        {
            if (!ValidateRunspaceState())
                return new Dictionary<string, bool>();

            if (!userUpns?.Any() ?? true)
            {
                _logger.LogWarning("Lista użytkowników jest pusta.");
                return new Dictionary<string, bool>();
            }

            _logger.LogInformation("Masowe usuwanie {Count} użytkowników z zespołu {TeamId}",
                userUpns!.Count, teamId);

            var results = new Dictionary<string, bool>();

            // Podziel na partie
            var batches = userUpns
                .Select((upn, index) => new { upn, index })
                .GroupBy(x => x.index / BatchSize)
                .Select(g => g.Select(x => x.upn).ToList());

            foreach (var batch in batches)
            {
                await _semaphore.WaitAsync();
                try
                {
                    var batchResults = await ProcessUserRemovalBatchAsync(teamId, batch);
                    foreach (var result in batchResults)
                    {
                        results[result.Key] = result.Value;
                    }
                }
                finally
                {
                    _semaphore.Release();
                }

                if (batch != batches.Last())
                {
                    await Task.Delay(1000);
                }
            }

            _logger.LogInformation("Zakończono masowe usuwanie. Sukcesy: {Success}, Błędy: {Failed}",
                results.Count(r => r.Value), results.Count(r => !r.Value));

            // Invalidate cache dla zespołu
            InvalidatePowerShellCache(teamId: teamId);

            return results;
        }

        /// <summary>
        /// Przetwarza pojedynczą partię użytkowników do usunięcia.
        /// </summary>
        private async Task<Dictionary<string, bool>> ProcessUserRemovalBatchAsync(
            string teamId,
            List<string> userUpns)
        {
            var scriptBuilder = new StringBuilder();
            scriptBuilder.AppendLine($"$teamId = '{teamId}'");
            scriptBuilder.AppendLine("$results = @{}");
            scriptBuilder.AppendLine();

            // Pobierz ID wszystkich użytkowników w partii
            scriptBuilder.AppendLine("$userIds = @{}");
            foreach (var upn in userUpns)
            {
                var cachedUserId = await GetUserIdAsync(upn);
                if (!string.IsNullOrEmpty(cachedUserId))
                {
                    scriptBuilder.AppendLine($"$userIds['{upn.Replace("'", "''")}'] = '{cachedUserId}'");
                }
                else
                {
                    scriptBuilder.AppendLine($@"
                        try {{
                            $user = Get-MgUser -UserId '{upn.Replace("'", "''")}' -ErrorAction Stop
                            $userIds['{upn.Replace("'", "''")}'] = $user.Id
                        }} catch {{
                            $results['{upn.Replace("'", "''")}'] = $false
                            Write-Warning ""Użytkownik {upn} nie znaleziony""
                        }}
                    ");
                }
            }

            // Usuń użytkowników z zespołu
            scriptBuilder.AppendLine(@"
                $teamOwners = Get-MgTeamOwner -TeamId $teamId | Select-Object -ExpandProperty Id
                $teamMembers = Get-MgTeamMember -TeamId $teamId | Select-Object -ExpandProperty Id
                
                foreach ($upn in $userIds.Keys) {
                    $userId = $userIds[$upn]
                    try {
                        if ($userId -in $teamOwners) {
                            Remove-MgTeamOwner -TeamId $teamId -UserId $userId -Confirm:$false -ErrorAction Stop
                            $results[$upn] = $true
                        } elseif ($userId -in $teamMembers) {
                            Remove-MgTeamMember -TeamId $teamId -UserId $userId -Confirm:$false -ErrorAction Stop
                            $results[$upn] = $true
                        } else {
                            $results[$upn] = $true  # Użytkownik już nie jest członkiem
                        }
                    } catch {
                        $results[$upn] = $false
                        Write-Warning ""Błąd usuwania $upn : $_""
                    }
                }
                $results
            ");

            var scriptResults = await ExecuteScriptAsync(scriptBuilder.ToString());
            var results = new Dictionary<string, bool>();

            if (scriptResults?.FirstOrDefault()?.BaseObject is Hashtable hashtable)
            {
                foreach (DictionaryEntry entry in hashtable)
                {
                    if (entry.Key?.ToString() is string key && entry.Value is bool value)
                    {
                        results[key] = value;
                    }
                }
            }

            return results;
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, bool>> BulkUpdateUserPropertiesAsync(
            Dictionary<string, Dictionary<string, string>> userUpdates)
        {
            if (!ValidateRunspaceState())
                return new Dictionary<string, bool>();

            if (!userUpdates?.Any() ?? true)
            {
                _logger.LogWarning("Lista aktualizacji użytkowników jest pusta.");
                return new Dictionary<string, bool>();
            }

            _logger.LogInformation("Masowa aktualizacja właściwości dla {Count} użytkowników", userUpdates!.Count);

            var script = new StringBuilder();
            script.AppendLine("$results = @{}");

            foreach (var kvp in userUpdates)
            {
                var userUpn = kvp.Key;
                var properties = kvp.Value;

                script.AppendLine($@"
                    try {{
                        $params = @{{}}
                ");

                foreach (var prop in properties)
                {
                    switch (prop.Key.ToLower())
                    {
                        case "department":
                            script.AppendLine($"        $params['Department'] = '{prop.Value.Replace("'", "''")}'");
                            break;
                        case "jobtitle":
                            script.AppendLine($"        $params['JobTitle'] = '{prop.Value.Replace("'", "''")}'");
                            break;
                        case "firstname":
                        case "givenname":
                            script.AppendLine($"        $params['GivenName'] = '{prop.Value.Replace("'", "''")}'");
                            break;
                        case "lastname":
                        case "surname":
                            script.AppendLine($"        $params['Surname'] = '{prop.Value.Replace("'", "''")}'");
                            break;
                    }
                }

                script.AppendLine($@"
                        Update-MgUser -UserId '{userUpn.Replace("'", "''")}' @params -ErrorAction Stop
                        $results['{userUpn.Replace("'", "''")}'] = $true
                    }} catch {{
                        $results['{userUpn.Replace("'", "''")}'] = $false
                        Write-Warning ""Błąd aktualizacji użytkownika {userUpn}: $_""
                    }}
                ");
            }

            script.AppendLine("$results");

            var scriptResults = await ExecuteScriptAsync(script.ToString());
            var results = new Dictionary<string, bool>();

            if (scriptResults?.FirstOrDefault()?.BaseObject is Hashtable hashtable)
            {
                foreach (DictionaryEntry entry in hashtable)
                {
                    if (entry.Key?.ToString() is string key && entry.Value is bool value)
                    {
                        results[key] = value;
                    }
                }
            }

            _logger.LogInformation("Zakończono masową aktualizację. Sukcesy: {Success}, Błędy: {Failed}",
                results.Count(r => r.Value), results.Count(r => !r.Value));

            // Invalidate cache dla wszystkich zaktualizowanych użytkowników
            foreach (var userUpn in userUpdates.Keys)
            {
                InvalidatePowerShellCache(userUpn: userUpn);
            }

            return results;
        }

        /// <inheritdoc />
        public async Task<Collection<PSObject>?> FindDuplicateUsersAsync()
        {
            if (!ValidateRunspaceState()) return null;

            _logger.LogInformation("Wyszukiwanie duplikatów użytkowników");

            try
            {
                var script = @"
                    $users = Get-MgUser -All -Property DisplayName,UserPrincipalName,Mail,Department -PageSize 999
                    
                    # Grupuj po DisplayName
                    $duplicates = $users | Group-Object DisplayName | Where-Object { $_.Count -gt 1 }
                    
                    $results = $duplicates | ForEach-Object {
                        [PSCustomObject]@{
                            DisplayName = $_.Name
                            Count = $_.Count
                            Users = $_.Group | Select-Object UserPrincipalName, Mail, Department, Id
                        }
                    }
                    
                    # Dodaj też duplikaty po Mail (jeśli istnieje)
                    $mailDuplicates = $users | Where-Object { $_.Mail } | 
                        Group-Object Mail | Where-Object { $_.Count -gt 1 }
                    
                    $mailDuplicates | ForEach-Object {
                        $results += [PSCustomObject]@{
                            DuplicateType = 'Email'
                            Value = $_.Name
                            Count = $_.Count
                            Users = $_.Group | Select-Object UserPrincipalName, DisplayName, Department, Id
                        }
                    }
                    
                    $results
                ";

                return await ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd wyszukiwania duplikatów");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ArchiveTeamAndDeactivateExclusiveUsersAsync(string teamId)
        {
            if (!ValidateRunspaceState()) return false;

            if (string.IsNullOrEmpty(teamId))
            {
                _logger.LogError("TeamID nie może być puste.");
                return false;
            }

            _logger.LogInformation("Archiwizacja zespołu {TeamId} i dezaktywacja ekskluzywnych użytkowników", teamId);

            try
            {
                var script = $@"
                    $teamId = '{teamId}'
                    $errors = @()
                    
                    # Pobierz członków zespołu
                    $members = Get-MgTeamMember -TeamId $teamId -All
                    
                    # Archiwizuj zespół
                    try {{
                        Update-MgTeam -GroupId $teamId -IsArchived $true -ErrorAction Stop
                        Write-Host ""Zespół zarchiwizowany""
                    }} catch {{
                        $errors += ""Błąd archiwizacji zespołu: $_""
                    }}
                    
                    # Dla każdego członka sprawdź inne zespoły
                    foreach ($member in $members) {{
                        try {{
                            # Pobierz wszystkie zespoły użytkownika
                            $userTeams = Get-MgUserMemberOf -UserId $member.Id -Filter ""resourceProvisioningOptions/Any(x:x eq 'Team')""
                            
                            # Jeśli użytkownik jest tylko w tym zespole
                            if ($userTeams.Count -eq 1 -and $userTeams[0].Id -eq $teamId) {{
                                Update-MgUser -UserId $member.Id -AccountEnabled $false -ErrorAction Stop
                                Write-Host ""Dezaktywowano użytkownika: $($member.DisplayName)""
                            }}
                        }} catch {{
                            $errors += ""Błąd przetwarzania użytkownika $($member.Id): $_""
                        }}
                    }}
                    
                    @{{
                        Success = $errors.Count -eq 0
                        Errors = $errors
                    }}
                ";

                var results = await ExecuteScriptAsync(script);
                var result = results?.FirstOrDefault()?.BaseObject as Hashtable;

                var success = result?["Success"] as bool? ?? false;
                if (!success)
                {
                    var errors = result?["Errors"] as object[];
                    foreach (var error in errors ?? Array.Empty<object>())
                    {
                        _logger.LogError("Błąd operacji: {Error}", error);
                    }
                }

                // Invalidate cache
                InvalidatePowerShellCache(teamId: teamId, invalidateAll: true);

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas archiwizacji zespołu i dezaktywacji użytkowników");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<Collection<PSObject>?> ExecuteScriptAsync(
            string script,
            Dictionary<string, object>? parameters = null)
        {
            if (_runspace == null || _runspace.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                _logger.LogError("Środowisko PowerShell nie jest zainicjalizowane.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(script))
            {
                _logger.LogError("Skrypt nie może być pusty.");
                return null;
            }

            // Podstawowa sanityzacja
            if (script.Contains("`;") || script.Contains("$(") || script.Contains("${"))
            {
                _logger.LogWarning("Wykryto potencjalnie niebezpieczne znaki w skrypcie.");
            }

            _logger.LogDebug("Wykonywanie skryptu PowerShell ({Length} znaków)", script.Length);

            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        ps.AddScript(script);

                        if (parameters != null)
                        {
                            ps.AddParameters(parameters);
                        }

                        var results = ps.Invoke();

                        // Logowanie strumieni
                        LogPowerShellStreams(ps);

                        if (ps.HadErrors && results.Count == 0)
                        {
                            _logger.LogError("Skrypt zakończył się błędami.");
                            return null;
                        }

                        _logger.LogDebug("Skrypt wykonany. Wyniki: {Count}", results.Count);
                        return results;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd wykonywania skryptu PowerShell");
                    return null;
                }
            });
        }

        /// <summary>
        /// Loguje strumienie PowerShell.
        /// </summary>
        private void LogPowerShellStreams(PowerShell ps)
        {
            foreach (var error in ps.Streams.Error)
            {
                _logger.LogError("PowerShell Error: {Error}", error.ToString());
            }

            foreach (var warning in ps.Streams.Warning)
            {
                _logger.LogWarning("PowerShell Warning: {Warning}", warning.ToString());
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                foreach (var info in ps.Streams.Information)
                {
                    _logger.LogDebug("PowerShell Info: {Info}", info.ToString());
                }
            }
        }

        /// <summary>
        /// Unieważnia cache dla PowerShell.
        /// </summary>
        private void InvalidatePowerShellCache(
            string? userId = null,
            string? userUpn = null,
            string? teamId = null,
            bool invalidateAll = false)
        {
            _logger.LogDebug("Inwalidacja cache PowerShell. userId: {UserId}, userUpn: {UserUpn}, teamId: {TeamId}, invalidateAll: {InvalidateAll}",
                userId, userUpn, teamId, invalidateAll);

            // 1. Zresetuj CancellationTokenSource
            var oldTokenSource = Interlocked.Exchange(ref _powerShellCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
            _logger.LogDebug("Token cache dla PowerShell został zresetowany.");

            // 2. Usuń konkretne klucze
            if (!string.IsNullOrWhiteSpace(userId))
            {
                _cache.Remove(UserIdCacheKeyPrefix + userId);
            }

            if (!string.IsNullOrWhiteSpace(userUpn))
            {
                _cache.Remove(UserIdCacheKeyPrefix + userUpn);
                _cache.Remove(UserUpnCacheKeyPrefix + userUpn);
            }

            if (!string.IsNullOrWhiteSpace(teamId))
            {
                _cache.Remove(TeamDetailsCacheKeyPrefix + teamId);
                _cache.Remove(TeamChannelsCacheKeyPrefix + teamId);
            }

            // 3. Jeśli invalidateAll, usuń też kontekst i listy
            if (invalidateAll)
            {
                _cache.Remove(GraphContextCacheKey);
                _cache.Remove(AllTeamsCacheKey);
                _logger.LogDebug("Usunięto wszystkie klucze cache PowerShell.");
            }
        }

        /// <summary>
        /// Zapisuje historię operacji.
        /// </summary>
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

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _semaphore?.Dispose();
                DisconnectFromGraph();
                _runspace?.Dispose();
            }

            _disposed = true;
        }

        private void DisconnectFromGraph()
        {
            if (!_isConnected || _runspace == null) return;

            try
            {
                using var ps = PowerShell.Create();
                ps.Runspace = _runspace;
                ps.AddScript("Disconnect-MgGraph -ErrorAction SilentlyContinue");
                ps.Invoke();
                _logger.LogInformation("Rozłączono z Microsoft Graph.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas rozłączania z Graph");
            }
            finally
            {
                _isConnected = false;
                InvalidatePowerShellCache(invalidateAll: true);
            }
        }
    }
}