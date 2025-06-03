using System;
using System.Collections.Generic;
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

namespace TeamsManager.Core.Services.PowerShellServices
{
    /// <summary>
    /// Implementacja serwisu zarządzającego użytkownikami, członkostwem w zespołach i licencjami w Microsoft 365 przez PowerShell
    /// </summary>
    public class PowerShellUserManagementService : IPowerShellUserManagementService
    {
        private readonly IPowerShellConnectionService _connectionService;
        private readonly IPowerShellCacheService _cacheService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly INotificationService _notificationService;
        private readonly ILogger<PowerShellUserManagementService> _logger;

        // Stałe
        private const string DefaultUsageLocation = "PL";

        public PowerShellUserManagementService(
            IPowerShellConnectionService connectionService,
            IPowerShellCacheService cacheService,
            ICurrentUserService currentUserService,
            IOperationHistoryRepository operationHistoryRepository,
            INotificationService notificationService,
            ILogger<PowerShellUserManagementService> logger)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region User Operations

        public async Task<string?> CreateM365UserAsync(
            string displayName,
            string userPrincipalName,
            string password,
            string? usageLocation = null,
            List<string>? licenseSkuIds = null,
            bool accountEnabled = true)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            var operationId = Guid.NewGuid().ToString();

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 5, 
                "Rozpoczynanie tworzenia użytkownika M365...");
            await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                $"🚀 Rozpoczęto tworzenie użytkownika '{displayName}'", "info");

            if (!_connectionService.ValidateRunspaceState())
            {
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "❌ Środowisko PowerShell nie jest gotowe", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona niepowodzeniem - błąd środowiska");
                return null;
            }

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 20, 
                "Weryfikacja parametrów użytkownika...");

            if (string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(userPrincipalName) ||
                string.IsNullOrWhiteSpace(password))
            {
                _logger.LogError("DisplayName, UserPrincipalName i Password są wymagane.");
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "❌ Nazwa, UPN i hasło są wymagane", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona niepowodzeniem - nieprawidłowe parametry");
                return null;
            }

            // Użyj domyślnej lokalizacji jeśli nie podano
            usageLocation ??= DefaultUsageLocation;

            _logger.LogInformation("Tworzenie użytkownika M365: {UserPrincipalName}", userPrincipalName);

            try
            {
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 50, 
                    "Tworzenie konta w Azure AD...");

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

                var results = await _connectionService.ExecuteScriptAsync(script);
                var userId = results?.FirstOrDefault()?.BaseObject?.ToString();

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("Nie udało się utworzyć użytkownika {UserPrincipalName}",
                        userPrincipalName);
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        $"❌ Nie udało się utworzyć użytkownika '{userPrincipalName}'", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Operacja zakończona niepowodzeniem - błąd tworzenia użytkownika");
                    return null;
                }

                // Przypisz licencje jeśli podano
                if (licenseSkuIds?.Count > 0)
                {
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 80, 
                        "Przypisywanie licencji...");
                    await AssignLicensesToUserAsync(userId, licenseSkuIds);
                }

                _logger.LogInformation("Utworzono użytkownika {UserPrincipalName} o ID: {UserId}",
                    userPrincipalName, userId);

                // Invalidate user cache
                _cacheService.InvalidateUserCache(userId: userId, userUpn: userPrincipalName);

                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Użytkownik utworzony pomyślnie!");
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    $"✅ Użytkownik '{displayName}' ({userPrincipalName}) został utworzony pomyślnie", "success");

                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd tworzenia użytkownika {UserPrincipalName}", userPrincipalName);
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    $"❌ Błąd krytyczny podczas tworzenia użytkownika: {ex.Message}", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona błędem krytycznym");
                return null;
            }
        }

        public async Task<bool> SetM365UserAccountStateAsync(string userPrincipalName, bool isEnabled)
        {
            if (!_connectionService.ValidateRunspaceState()) return false;

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
                var results = await _connectionService.ExecuteCommandWithRetryAsync("Update-MgUser", parameters);

                // Invalidate user cache
                _cacheService.InvalidateUserCache(userUpn: userPrincipalName);

                return results != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd zmiany stanu konta {UserPrincipalName}", userPrincipalName);
                return false;
            }
        }

        public async Task<bool> UpdateM365UserPrincipalNameAsync(string currentUpn, string newUpn)
        {
            if (!_connectionService.ValidateRunspaceState()) return false;

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
                var results = await _connectionService.ExecuteCommandWithRetryAsync("Update-MgUser", parameters);

                // Invalidate cache for both UPNs
                _cacheService.InvalidateUserCache(userUpn: currentUpn);
                _cacheService.InvalidateUserCache(userUpn: newUpn);

                return results != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd aktualizacji UPN użytkownika z '{CurrentUpn}' na '{NewUpn}'", currentUpn, newUpn);
                return false;
            }
        }

        public async Task<bool> UpdateM365UserPropertiesAsync(
            string userUpn,
            string? department = null,
            string? jobTitle = null,
            string? firstName = null,
            string? lastName = null)
        {
            if (!_connectionService.ValidateRunspaceState()) return false;

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
                var results = await _connectionService.ExecuteCommandWithRetryAsync("Update-MgUser", parameters);

                // Invalidate user cache
                _cacheService.InvalidateUserCache(userUpn: userUpn);

                return results != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd aktualizacji właściwości użytkownika: {UserUpn}", userUpn);
                return false;
            }
        }

        public async Task<Collection<PSObject>?> GetAllUsersAsync(string? filter = null)
        {
            if (!_connectionService.ValidateRunspaceState()) return null;

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

                return await _connectionService.ExecuteScriptAsync(script.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania użytkowników");
                return null;
            }
        }

        public async Task<Collection<PSObject>?> GetInactiveUsersAsync(int daysInactive)
        {
            if (!_connectionService.ValidateRunspaceState()) return null;

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

                return await _connectionService.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania nieaktywnych użytkowników");
                return null;
            }
        }

        public async Task<Collection<PSObject>?> FindDuplicateUsersAsync()
        {
            if (!_connectionService.ValidateRunspaceState()) return null;

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

                return await _connectionService.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd wyszukiwania duplikatów");
                return null;
            }
        }

        #endregion

        #region Team Membership Operations

        public async Task<bool> AddUserToTeamAsync(string teamId, string userUpn, string role)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_add_user";
            var operationId = Guid.NewGuid().ToString();
            var operation = new OperationHistory
            {
                Id = operationId,
                Type = OperationType.MemberAdded,
                TargetEntityType = "TeamMember",
                TargetEntityId = teamId,
                TargetEntityName = $"{userUpn} -> Team {teamId}",
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 5, 
                "Rozpoczynanie dodawania użytkownika...");

            try
            {
                if (!_connectionService.ValidateRunspaceState())
                {
                    operation.MarkAsFailed("Środowisko PowerShell nie jest gotowe.");
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        "❌ Środowisko PowerShell nie jest gotowe", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Operacja zakończona niepowodzeniem - błąd środowiska");
                    return false;
                }

                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 20, 
                    "Weryfikacja parametrów...");

                if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(userUpn) || string.IsNullOrEmpty(role))
                {
                    operation.MarkAsFailed("TeamID, UserUPN i Role są wymagane.");
                    _logger.LogError("TeamID, UserUPN i Role są wymagane.");
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        "❌ ID zespołu, UPN użytkownika i rola są wymagane", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Operacja zakończona niepowodzeniem - nieprawidłowe parametry");
                    return false;
                }

                if (!role.Equals("Owner", StringComparison.OrdinalIgnoreCase) &&
                    !role.Equals("Member", StringComparison.OrdinalIgnoreCase))
                {
                    operation.MarkAsFailed($"Nieprawidłowa rola '{role}'.");
                    _logger.LogError("Nieprawidłowa rola '{Role}'. Dozwolone: Owner, Member.", role);
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        $"❌ Nieprawidłowa rola '{role}'. Dozwolone: Owner, Member", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Operacja zakończona niepowodzeniem - nieprawidłowa rola");
                    return false;
                }

                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 40, 
                    "Wyszukiwanie użytkownika...");

                // Pobierz ID użytkownika z cache
                var userId = await _cacheService.GetUserIdAsync(userUpn);
                if (string.IsNullOrEmpty(userId))
                {
                    operation.MarkAsFailed($"Nie znaleziono użytkownika {userUpn}");
                    _logger.LogError("Nie znaleziono użytkownika {UserUpn}", userUpn);
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        $"❌ Nie znaleziono użytkownika {userUpn}", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Operacja zakończona niepowodzeniem - nie znaleziono użytkownika");
                    return false;
                }

                _logger.LogInformation("Dodawanie użytkownika {UserUpn} do zespołu {TeamId} jako {Role}",
                    userUpn, teamId, role);

                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 60, 
                    "Dodawanie do zespołu...");

                var cmdlet = role.Equals("Owner", StringComparison.OrdinalIgnoreCase)
                    ? "Add-MgTeamOwner"
                    : "Add-MgTeamMember";

                var script = $"{cmdlet} -TeamId '{teamId}' -UserId '{userId}' -ErrorAction Stop";
                var results = await _connectionService.ExecuteScriptAsync(script);

                if (results != null)
                {
                    operation.MarkAsCompleted("Użytkownik dodany do zespołu.");
                    _logger.LogInformation("Pomyślnie dodano użytkownika {UserUpn} do zespołu {TeamId}",
                        userUpn, teamId);

                    // Invalidate team cache
                    _cacheService.InvalidateTeamCache(teamId);

                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Użytkownik dodany pomyślnie!");
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        $"✅ Użytkownik {userUpn} został dodany do zespołu jako {role}", "success");

                    return true;
                }
                else
                {
                    operation.MarkAsFailed("Błąd podczas dodawania użytkownika.");
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        "❌ Nie udało się dodać użytkownika do zespołu", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Operacja zakończona niepowodzeniem");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd dodawania użytkownika {UserUpn} do zespołu {TeamId}",
                    userUpn, teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    $"❌ Błąd krytyczny podczas dodawania użytkownika: {ex.Message}", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona błędem krytycznym");
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> RemoveUserFromTeamAsync(string teamId, string userUpn)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            var operationId = Guid.NewGuid().ToString();

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 5, 
                "Rozpoczynanie usuwania użytkownika...");

            if (!_connectionService.ValidateRunspaceState())
            {
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "❌ Środowisko PowerShell nie jest gotowe", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona niepowodzeniem - błąd środowiska");
                return false;
            }

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 20, 
                "Weryfikacja parametrów...");

            if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(userUpn))
            {
                _logger.LogError("TeamID i UserUPN są wymagane.");
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "❌ ID zespołu i UPN użytkownika są wymagane", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona niepowodzeniem - nieprawidłowe parametry");
                return false;
            }

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 40, 
                "Wyszukiwanie użytkownika...");

            var userId = await _cacheService.GetUserIdAsync(userUpn);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError("Nie znaleziono użytkownika {UserUpn}", userUpn);
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    $"❌ Nie znaleziono użytkownika {userUpn}", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona niepowodzeniem - nie znaleziono użytkownika");
                return false;
            }

            _logger.LogInformation("Usuwanie użytkownika {UserUpn} z zespołu {TeamId}", userUpn, teamId);

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 60, 
                "Usuwanie z zespołu...");

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

                var results = await _connectionService.ExecuteScriptAsync(script);
                var success = results?.FirstOrDefault()?.BaseObject as bool? ?? false;

                if (success)
                {
                    _logger.LogInformation("Pomyślnie usunięto użytkownika {UserUpn} z zespołu {TeamId}",
                        userUpn, teamId);

                    // Invalidate cache
                    _cacheService.InvalidateTeamCache(teamId);

                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Użytkownik usunięty pomyślnie!");
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        $"✅ Użytkownik {userUpn} został usunięty z zespołu", "success");
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        "❌ Nie udało się usunąć użytkownika z zespołu", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                        "Operacja zakończona niepowodzeniem");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd usuwania użytkownika {UserUpn} z zespołu {TeamId}",
                    userUpn, teamId);
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    $"❌ Błąd podczas usuwania użytkownika: {ex.Message}", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona błędem krytycznym");
                return false;
            }
        }

        public async Task<Collection<PSObject>?> GetTeamMembersAsync(string teamId)
        {
            if (!_connectionService.ValidateRunspaceState()) return null;

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

                return await _connectionService.ExecuteCommandWithRetryAsync("Get-MgTeamMember", parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania członków zespołu {TeamId}", teamId);
                return null;
            }
        }

        public async Task<PSObject?> GetTeamMemberAsync(string teamId, string userUpn)
        {
            if (!_connectionService.ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(userUpn))
            {
                _logger.LogError("TeamID i UserUpn są wymagane.");
                return null;
            }

            _logger.LogInformation("Pobieranie członka {UserUpn} z zespołu {TeamId}", userUpn, teamId);

            try
            {
                var userId = await _cacheService.GetUserIdAsync(userUpn);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("Nie znaleziono użytkownika {UserUpn}", userUpn);
                    return null;
                }

                var script = $"Get-MgTeamMember -TeamId '{teamId}' -UserId '{userId}' -ErrorAction Stop";
                var results = await _connectionService.ExecuteScriptAsync(script);
                return results?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania członka {UserUpn} z zespołu {TeamId}", userUpn, teamId);
                return null;
            }
        }

        #endregion

        #region License Operations

        public async Task<bool> AssignLicenseToUserAsync(string userUpn, string licenseSkuId)
        {
            if (!_connectionService.ValidateRunspaceState()) return false;

            if (string.IsNullOrWhiteSpace(userUpn) || string.IsNullOrWhiteSpace(licenseSkuId))
            {
                _logger.LogError("UserUpn i LicenseSkuId są wymagane.");
                return false;
            }

            _logger.LogInformation("Przypisywanie licencji {LicenseSkuId} do użytkownika {UserUpn}", licenseSkuId, userUpn);

            try
            {
                var userId = await _cacheService.GetUserIdAsync(userUpn);
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

                var results = await _connectionService.ExecuteScriptAsync(script);
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

        public async Task<bool> RemoveLicenseFromUserAsync(string userUpn, string licenseSkuId)
        {
            if (!_connectionService.ValidateRunspaceState()) return false;

            if (string.IsNullOrWhiteSpace(userUpn) || string.IsNullOrWhiteSpace(licenseSkuId))
            {
                _logger.LogError("UserUpn i LicenseSkuId są wymagane.");
                return false;
            }

            _logger.LogInformation("Usuwanie licencji {LicenseSkuId} od użytkownika {UserUpn}", licenseSkuId, userUpn);

            try
            {
                var userId = await _cacheService.GetUserIdAsync(userUpn);
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

                var results = await _connectionService.ExecuteScriptAsync(script);
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

        public async Task<Collection<PSObject>?> GetUserLicensesAsync(string userUpn)
        {
            if (!_connectionService.ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(userUpn))
            {
                _logger.LogError("UserUpn jest wymagany.");
                return null;
            }

            _logger.LogInformation("Pobieranie licencji dla użytkownika {UserUpn}", userUpn);

            try
            {
                var userId = await _cacheService.GetUserIdAsync(userUpn);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("Nie znaleziono użytkownika {UserUpn}", userUpn);
                    return null;
                }

                var script = $"Get-MgUserLicenseDetail -UserId '{userId}' -ErrorAction Stop";
                return await _connectionService.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania licencji dla użytkownika {UserUpn}", userUpn);
                return null;
            }
        }

        #endregion

        #region Private Methods

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

                await _connectionService.ExecuteScriptAsync(script);
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

        private async Task<bool> CheckGraphPermissionAsync(string permission)
        {
            try
            {
                var script = "(Get-MgContext).Scopes -contains '" + permission + "'";
                var results = await _connectionService.ExecuteScriptAsync(script);
                return results?.FirstOrDefault()?.BaseObject as bool? ?? false;
            }
            catch
            {
                return false;
            }
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