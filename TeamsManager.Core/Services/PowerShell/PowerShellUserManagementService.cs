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

namespace TeamsManager.Core.Services.PowerShellServices
{
    /// <summary>
    /// Implementacja serwisu zarządzającego użytkownikami, członkostwem w zespołach i licencjami w Microsoft 365 przez PowerShell
    /// </summary>
    public class PowerShellUserManagementService : IPowerShellUserManagementService
    {
        private readonly IPowerShellConnectionService _connectionService;
        private readonly IPowerShellCacheService _cacheService;
        private readonly IPowerShellUserResolverService _userResolver;
        private readonly ILogger<PowerShellUserManagementService> _logger;

        // Stałe
        private const string DefaultUsageLocation = "PL";

        public PowerShellUserManagementService(
            IPowerShellConnectionService connectionService,
            IPowerShellCacheService cacheService,
            IPowerShellUserResolverService userResolver,
            ILogger<PowerShellUserManagementService> logger)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _userResolver = userResolver ?? throw new ArgumentNullException(nameof(userResolver));
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
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Środowisko PowerShell nie jest gotowe.");
                return null;
            }

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

                var results = await _connectionService.ExecuteScriptAsync(script);
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
                _cacheService.InvalidateUserCache(userId: userId, userUpn: userPrincipalName);

                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd tworzenia użytkownika {UserPrincipalName}", userPrincipalName);
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

        public async Task<PSObject?> GetM365UserByIdAsync(string userId)
        {
            if (!_connectionService.ValidateRunspaceState()) return null;

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogError("ID użytkownika nie może być puste.");
                return null;
            }

            // Tworzenie klucza cache dla użytkownika po ID
            string cacheKey = $"PowerShell_M365User_Id_{userId}";

            // Próba pobrania z cache
            if (_cacheService.TryGetValue(cacheKey, out PSObject? cachedUser))
            {
                _logger.LogDebug("Użytkownik ID: {UserId} znaleziony w cache PowerShell.", userId);
                return cachedUser;
            }

            _logger.LogInformation("Pobieranie użytkownika ID '{UserId}' z Microsoft 365.", userId);

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "UserId", userId }
                };

                var results = await _connectionService.ExecuteCommandWithRetryAsync("Get-MgUser", parameters);
                var user = results?.FirstOrDefault();

                if (user != null)
                {
                    _cacheService.Set<PSObject>(cacheKey, user);
                    _logger.LogDebug("Użytkownik ID: {UserId} dodany do cache PowerShell.", userId);
                }
                else
                {
                    _cacheService.Set<PSObject?>(cacheKey, null, TimeSpan.FromMinutes(1));
                }
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania użytkownika ID '{UserId}'.", userId);
                _cacheService.Set<PSObject?>(cacheKey, null, TimeSpan.FromMinutes(1));
                return null;
            }
        }

        public async Task<Collection<PSObject>?> GetM365UsersByAccountEnabledStateAsync(bool accountEnabled)
        {
            if (!_connectionService.ValidateRunspaceState()) return null;

            string cacheKey = $"PowerShell_M365Users_AccountEnabled_{accountEnabled}";

            if (_cacheService.TryGetValue(cacheKey, out Collection<PSObject>? cachedUsers))
            {
                _logger.LogDebug("Lista użytkowników z accountEnabled='{AccountEnabled}' znaleziona w cache PowerShell.", accountEnabled);
                return cachedUsers;
            }

            _logger.LogInformation("Pobieranie użytkowników z accountEnabled='{AccountEnabled}' z Microsoft 365.", accountEnabled);

            try
            {
                var script = $@"
            Get-MgUser -All -Property Id,DisplayName,UserPrincipalName,AccountEnabled,Mail,Department,JobTitle -Filter ""accountEnabled eq {accountEnabled.ToString().ToLower()}"" -PageSize 999
        ";
                
                var results = await _connectionService.ExecuteScriptAsync(script);

                if (results != null)
                {
                    _cacheService.Set<Collection<PSObject>>(cacheKey, results);
                    _logger.LogDebug("Lista użytkowników z accountEnabled='{AccountEnabled}' dodana do cache PowerShell.", accountEnabled);
                }
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania użytkowników z accountEnabled='{AccountEnabled}'.", accountEnabled);
                return null;
            }
        }

        #endregion

        #region Team Membership Operations

        public async Task<bool> AddUserToTeamAsync(string teamId, string userUpn, string role)
        {
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Środowisko PowerShell nie jest gotowe.");
                return false;
            }

            if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(userUpn) || string.IsNullOrEmpty(role))
            {
                _logger.LogError("TeamID, UserUPN i Role są wymagane.");
                return false;
            }

            if (!role.Equals("Owner", StringComparison.OrdinalIgnoreCase) &&
                !role.Equals("Member", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Nieprawidłowa rola '{Role}'. Dozwolone: Owner, Member.", role);
                return false;
            }

            // Pobierz ID użytkownika z cache
            var userId = await _userResolver.GetUserIdAsync(userUpn);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError("Nie znaleziono użytkownika {UserUpn}", userUpn);
                return false;
            }

            _logger.LogInformation("Dodawanie użytkownika {UserUpn} do zespołu {TeamId} jako {Role}",
                userUpn, teamId, role);

            try
            {
                var cmdlet = role.Equals("Owner", StringComparison.OrdinalIgnoreCase)
                    ? "Add-MgTeamOwner"
                    : "Add-MgTeamMember";

                var script = $"{cmdlet} -TeamId '{teamId}' -UserId '{userId}' -ErrorAction Stop";
                var results = await _connectionService.ExecuteScriptAsync(script);

                if (results != null)
                {
                    _logger.LogInformation("Pomyślnie dodano użytkownika {UserUpn} do zespołu {TeamId}",
                        userUpn, teamId);

                    // Invalidate team cache
                    _cacheService.InvalidateTeamCache(teamId);

                    return true;
                }
                else
                {
                    _logger.LogError("Nie udało się dodać użytkownika {UserUpn} do zespołu {TeamId}",
                        userUpn, teamId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd dodawania użytkownika {UserUpn} do zespołu {TeamId}",
                    userUpn, teamId);
                return false;
            }
        }

        public async Task<bool> RemoveUserFromTeamAsync(string teamId, string userUpn)
        {
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Środowisko PowerShell nie jest gotowe.");
                return false;
            }

            if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(userUpn))
            {
                _logger.LogError("TeamID i UserUPN są wymagane.");
                return false;
            }

            var userId = await _userResolver.GetUserIdAsync(userUpn);
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

                var results = await _connectionService.ExecuteScriptAsync(script);
                var success = results?.FirstOrDefault()?.BaseObject as bool? ?? false;

                if (success)
                {
                    _logger.LogInformation("Pomyślnie usunięto użytkownika {UserUpn} z zespołu {TeamId}",
                        userUpn, teamId);

                    // Invalidate cache
                    _cacheService.InvalidateTeamCache(teamId);
                }
                else
                {
                    _logger.LogError("Nie udało się usunąć użytkownika {UserUpn} z zespołu {TeamId}",
                        userUpn, teamId);
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
                var userId = await _userResolver.GetUserIdAsync(userUpn);
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
                var userId = await _userResolver.GetUserIdAsync(userUpn);
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
                var userId = await _userResolver.GetUserIdAsync(userUpn);
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
                var userId = await _userResolver.GetUserIdAsync(userUpn);
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

        #endregion
    }
}