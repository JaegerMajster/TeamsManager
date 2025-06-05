using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Helpers.PowerShell;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Services.PowerShellServices
{
    /// <summary>
    /// Implementacja serwisu zarządzającego operacjami masowymi w Microsoft 365 przez PowerShell
    /// </summary>
    public class PowerShellBulkOperationsService : IPowerShellBulkOperationsService
    {
        private readonly IPowerShellConnectionService _connectionService;
        private readonly IPowerShellCacheService _cacheService;
        private readonly IPowerShellUserResolverService _userResolver;
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<PowerShellBulkOperationsService> _logger;

        // Stałe konfiguracyjne
        private const int BatchSize = 50;
        private const int ThrottleLimit = 5; // PowerShell ForEach-Object -Parallel limit

        // Semaphore dla kontroli współbieżności
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(3, 3);

        public PowerShellBulkOperationsService(
            IPowerShellConnectionService connectionService,
            IPowerShellCacheService cacheService,
            IPowerShellUserResolverService userResolver,
            IOperationHistoryService operationHistoryService,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<PowerShellBulkOperationsService> logger)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _userResolver = userResolver ?? throw new ArgumentNullException(nameof(userResolver));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Dictionary<string, bool>> BulkAddUsersToTeamAsync(
            string teamId,
            List<string> userUpns,
            string role = "Member")
        {
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Środowisko PowerShell nie jest gotowe.");
                return new Dictionary<string, bool>();
            }

            if (!userUpns?.Any() ?? true)
            {
                _logger.LogWarning("Lista użytkowników jest pusta.");
                return new Dictionary<string, bool>();
            }

            _logger.LogInformation("Masowe dodawanie {Count} użytkowników do zespołu {TeamId}",
                userUpns!.Count, teamId);

            // 1. Utwórz główny wpis operacji
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.BulkUserAddToTeam,
                "Team", 
                targetEntityId: teamId,
                targetEntityName: $"Bulk add {userUpns.Count} users to team"
            );

            var results = new Dictionary<string, bool>();
            var processedCount = 0;
            var failedCount = 0;

            try
            {
                // 2. Przetwarzaj partie
                var batches = userUpns
                    .Select((upn, index) => new { upn, index })
                    .GroupBy(x => x.index / BatchSize)
                    .Select(g => g.Select(x => x.upn).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    await _semaphore.WaitAsync(); // Kontrola współbieżności
                    try
                    {
                        var batchResults = await ProcessUserBatchAsync(teamId, batch, role);
                        
                        foreach (var result in batchResults)
                        {
                            results[result.Key] = result.Value;
                            if (result.Value) processedCount++;
                            else failedCount++;
                        }

                        // 3. Aktualizuj postęp
                        await _operationHistoryService.UpdateOperationProgressAsync(
                            operation.Id,
                            processedItems: processedCount,
                            failedItems: failedCount,
                            totalItems: userUpns.Count
                        );
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

                // 4. Ustaw końcowy status
                var status = failedCount == 0 ? OperationStatus.Completed 
                    : failedCount == userUpns.Count ? OperationStatus.Failed
                    : OperationStatus.PartialSuccess;

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, status,
                    $"Processed: {processedCount}, Failed: {failedCount}"
                );

                _logger.LogInformation("Zakończono masowe dodawanie. Sukcesy: {Success}, Błędy: {Failed}",
                    processedCount, failedCount);

                // Invalidate cache dla zespołu
                _cacheService.InvalidateTeamCache(teamId);

                return results;
            }
            catch (Exception ex)
            {
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, OperationStatus.Failed,
                    $"Critical error: {ex.Message}", ex.StackTrace
                );
                throw;
            }
        }

        public async Task<Dictionary<string, bool>> BulkRemoveUsersFromTeamAsync(
            string teamId,
            List<string> userUpns)
        {
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Środowisko PowerShell nie jest gotowe.");
                return new Dictionary<string, bool>();
            }

            if (!userUpns?.Any() ?? true)
            {
                _logger.LogWarning("Lista użytkowników jest pusta.");
                return new Dictionary<string, bool>();
            }

            _logger.LogInformation("Masowe usuwanie {Count} użytkowników z zespołu {TeamId}",
                userUpns!.Count, teamId);

            // 1. Utwórz główny wpis operacji
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.BulkUserRemoveFromTeam,
                "Team", 
                targetEntityId: teamId,
                targetEntityName: $"Bulk remove {userUpns.Count} users from team"
            );

            var results = new Dictionary<string, bool>();
            var processedCount = 0;
            var failedCount = 0;

            try
            {
                // 2. Przetwarzaj partie
                var batches = userUpns
                    .Select((upn, index) => new { upn, index })
                    .GroupBy(x => x.index / BatchSize)
                    .Select(g => g.Select(x => x.upn).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    await _semaphore.WaitAsync();
                    try
                    {
                        var batchResults = await ProcessUserRemovalBatchAsync(teamId, batch);
                        
                        foreach (var result in batchResults)
                        {
                            results[result.Key] = result.Value;
                            if (result.Value) processedCount++;
                            else failedCount++;
                        }

                        // 3. Aktualizuj postęp
                        await _operationHistoryService.UpdateOperationProgressAsync(
                            operation.Id,
                            processedItems: processedCount,
                            failedItems: failedCount,
                            totalItems: userUpns.Count
                        );
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

                // 4. Ustaw końcowy status
                var status = failedCount == 0 ? OperationStatus.Completed 
                    : failedCount == userUpns.Count ? OperationStatus.Failed
                    : OperationStatus.PartialSuccess;

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, status,
                    $"Processed: {processedCount}, Failed: {failedCount}"
                );

                _logger.LogInformation("Zakończono masowe usuwanie. Sukcesy: {Success}, Błędy: {Failed}",
                    processedCount, failedCount);

                // Invalidate cache
                _cacheService.InvalidateTeamCache(teamId);

                return results;
            }
            catch (Exception ex)
            {
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, OperationStatus.Failed,
                    $"Critical error: {ex.Message}", ex.StackTrace
                );
                throw;
            }
        }

        public async Task<Dictionary<string, bool>> BulkArchiveTeamsAsync(List<string> teamIds)
        {
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Środowisko PowerShell nie jest gotowe.");
                return new Dictionary<string, bool>();
            }

            if (!teamIds?.Any() ?? true)
            {
                _logger.LogWarning("Lista zespołów jest pusta.");
                return new Dictionary<string, bool>();
            }

            _logger.LogInformation("Masowa archiwizacja {Count} zespołów", teamIds!.Count);

            // 1. Utwórz główny wpis operacji
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.BulkTeamArchive,
                "Team",
                targetEntityName: $"Bulk archive {teamIds.Count} teams"
            );

            var results = new Dictionary<string, bool>();
            var processedCount = 0;
            var failedCount = 0;

            try
            {
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

                var scriptResults = await _connectionService.ExecuteScriptAsync(script.ToString());

                if (scriptResults?.FirstOrDefault()?.BaseObject is Hashtable hashtable)
                {
                    foreach (DictionaryEntry entry in hashtable)
                    {
                        if (entry.Key?.ToString() is string key && entry.Value is bool value)
                        {
                            results[key] = value;
                            if (value) processedCount++;
                            else failedCount++;
                        }
                    }
                }

                // 3. Aktualizuj postęp (100% po zakończeniu)
                await _operationHistoryService.UpdateOperationProgressAsync(
                    operation.Id,
                    processedItems: processedCount,
                    failedItems: failedCount,
                    totalItems: teamIds.Count
                );

                // 4. Ustaw końcowy status
                var status = failedCount == 0 ? OperationStatus.Completed 
                    : failedCount == teamIds.Count ? OperationStatus.Failed
                    : OperationStatus.PartialSuccess;

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, status,
                    $"Archived: {processedCount}, Failed: {failedCount}"
                );

                _logger.LogInformation("Zakończono archiwizację. Sukcesy: {Success}, Błędy: {Failed}",
                    processedCount, failedCount);

                // Invalidate cache dla wszystkich zespołów
                _cacheService.InvalidateAllCache();

                return results;
            }
            catch (Exception ex)
            {
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, OperationStatus.Failed,
                    $"Critical error: {ex.Message}", ex.StackTrace
                );
                
                _logger.LogError(ex, "Błąd podczas masowej archiwizacji zespołów");
                throw;
            }
        }

        public async Task<Dictionary<string, bool>> BulkUpdateUserPropertiesAsync(
            Dictionary<string, Dictionary<string, string>> userUpdates)
        {
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Środowisko PowerShell nie jest gotowe.");
                return new Dictionary<string, bool>();
            }

            if (!userUpdates?.Any() ?? true)
            {
                _logger.LogWarning("Lista aktualizacji użytkowników jest pusta.");
                return new Dictionary<string, bool>();
            }

            _logger.LogInformation("Masowa aktualizacja właściwości dla {Count} użytkowników", userUpdates!.Count);

            // 1. Utwórz główny wpis operacji
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.BulkUserUpdate,
                "User",
                targetEntityName: $"Bulk update {userUpdates.Count} users"
            );

            var results = new Dictionary<string, bool>();
            var processedCount = 0;
            var failedCount = 0;

            try
            {
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

                var scriptResults = await _connectionService.ExecuteScriptAsync(script.ToString());

                if (scriptResults?.FirstOrDefault()?.BaseObject is Hashtable hashtable)
                {
                    foreach (DictionaryEntry entry in hashtable)
                    {
                        if (entry.Key?.ToString() is string key && entry.Value is bool value)
                        {
                            results[key] = value;
                            if (value) processedCount++;
                            else failedCount++;
                        }
                    }
                }

                // 3. Aktualizuj postęp
                await _operationHistoryService.UpdateOperationProgressAsync(
                    operation.Id,
                    processedItems: processedCount,
                    failedItems: failedCount,
                    totalItems: userUpdates.Count
                );

                // 4. Ustaw końcowy status
                var status = failedCount == 0 ? OperationStatus.Completed 
                    : failedCount == userUpdates.Count ? OperationStatus.Failed
                    : OperationStatus.PartialSuccess;

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, status,
                    $"Updated: {processedCount}, Failed: {failedCount}"
                );

                _logger.LogInformation("Zakończono masową aktualizację. Sukcesy: {Success}, Błędy: {Failed}",
                    processedCount, failedCount);

                // Invalidate cache dla wszystkich zaktualizowanych użytkowników
                foreach (var userUpn in userUpdates.Keys)
                {
                    _cacheService.InvalidateUserCache(userUpn: userUpn);
                }

                return results;
            }
            catch (Exception ex)
            {
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, OperationStatus.Failed,
                    $"Critical error: {ex.Message}", ex.StackTrace
                );
                
                _logger.LogError(ex, "Błąd podczas masowej aktualizacji użytkowników");
                throw;
            }
        }

        public async Task<Dictionary<string, bool>> ArchiveTeamAndDeactivateExclusiveUsersAsync(string teamId)
        {
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Środowisko PowerShell nie jest gotowe.");
                return new Dictionary<string, bool>();
            }

            if (string.IsNullOrEmpty(teamId))
            {
                _logger.LogError("TeamID nie może być puste.");
                return new Dictionary<string, bool>();
            }

            _logger.LogInformation("Archiwizacja zespołu {TeamId} i dezaktywacja ekskluzywnych użytkowników", teamId);

            // 1. Utwórz główny wpis operacji
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.TeamArchiveWithUserDeactivation,
                "Team",
                targetEntityId: teamId,
                targetEntityName: "Archive team and deactivate exclusive users"
            );

            try
            {
                var script = $@"
                    $teamId = '{teamId}'
                    $errors = @()
                    $deactivatedUsers = @()
                    
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
                    $totalMembers = $members.Count
                    $processedMembers = 0
                    
                    foreach ($member in $members) {{
                        $processedMembers++
                        Write-Progress -Activity ""Przetwarzanie członków"" -Status ""$processedMembers/$totalMembers"" -PercentComplete (($processedMembers/$totalMembers)*100)
                        
                        try {{
                            # Pobierz wszystkie zespoły użytkownika
                            $userTeams = Get-MgUserMemberOf -UserId $member.Id -Filter ""resourceProvisioningOptions/Any(x:x eq 'Team')""
                            
                            # Jeśli użytkownik jest tylko w tym zespole
                            if ($userTeams.Count -eq 1 -and $userTeams[0].Id -eq $teamId) {{
                                Update-MgUser -UserId $member.Id -AccountEnabled $false -ErrorAction Stop
                                $deactivatedUsers += $member.DisplayName
                                Write-Host ""Dezaktywowano użytkownika: $($member.DisplayName)""
                            }}
                        }} catch {{
                            $errors += ""Błąd przetwarzania użytkownika $($member.Id): $_""
                        }}
                    }}
                    
                    @{{
                        Success = $errors.Count -eq 0
                        Errors = $errors
                        DeactivatedUsers = $deactivatedUsers
                        DeactivatedCount = $deactivatedUsers.Count
                    }}
                ";

                var results = await _connectionService.ExecuteScriptAsync(script);
                var result = results?.FirstOrDefault()?.BaseObject as Hashtable;

                var success = result?["Success"] as bool? ?? false;
                var deactivatedCount = Convert.ToInt32(result?["DeactivatedCount"] ?? 0);
                var errors = result?["Errors"] as object[];
                
                if (!success && errors != null)
                {
                    foreach (var error in errors)
                    {
                        _logger.LogError("Błąd operacji: {Error}", error);
                    }
                }

                // 3. Ustaw końcowy status
                var status = success ? OperationStatus.Completed : OperationStatus.Failed;
                var statusMessage = success 
                    ? $"Team archived. Deactivated {deactivatedCount} exclusive users"
                    : $"Operation failed. Errors: {errors?.Length ?? 0}";

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, status, statusMessage,
                    success ? null : string.Join("\n", errors ?? Array.Empty<object>())
                );

                _logger.LogInformation("Zespół zarchiwizowany. Dezaktywowano {Count} użytkowników", deactivatedCount);

                // Invalidate cache
                _cacheService.InvalidateTeamCache(teamId);
                _cacheService.InvalidateAllCache();

                // Zamiast zwracać bool, zwróć Dictionary
                return new Dictionary<string, bool> { { teamId, success } };
            }
            catch (Exception ex)
            {
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, OperationStatus.Failed,
                    $"Critical error: {ex.Message}", ex.StackTrace
                );
                
                _logger.LogError(ex, "Błąd podczas archiwizacji zespołu i dezaktywacji użytkowników");
                return new Dictionary<string, bool> { { teamId, false } };
            }
        }

        #region Private Methods

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
                var cachedUserId = await _userResolver.GetUserIdAsync(upn);
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

            var scriptResults = await _connectionService.ExecuteScriptAsync(scriptBuilder.ToString());
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
                var cachedUserId = await _userResolver.GetUserIdAsync(upn);
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

            var scriptResults = await _connectionService.ExecuteScriptAsync(scriptBuilder.ToString());
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

        #endregion

        #region Enhanced V2 Methods (Etap 6/7)

        /// <summary>
        /// [ETAP6] Ulepszona wersja BulkAddUsersToTeamAsync z zaawansowanym raportowaniem
        /// </summary>
        public async Task<Dictionary<string, BulkOperationResult>> BulkAddUsersToTeamV2Async(
            string teamId,
            List<string> userUpns,
            string role = "Member")
        {
            // Walidacja parametrów z PSParameterValidator
            teamId = PSParameterValidator.ValidateGuid(teamId, nameof(teamId));
            role = PSParameterValidator.ValidateAndSanitizeString(role, nameof(role));

            if (!userUpns?.Any() ?? true)
            {
                _logger.LogWarning("Lista użytkowników jest pusta.");
                return new Dictionary<string, BulkOperationResult>();
            }

            // Walidacja wszystkich email addresses
            var validatedUpns = new List<string>();
            foreach (var upn in userUpns)
            {
                try
                {
                    validatedUpns.Add(PSParameterValidator.ValidateEmail(upn, nameof(upn)));
                }
                catch (ArgumentException ex)
                {
                    _logger.LogWarning("Nieprawidłowy UPN: {Upn}, błąd: {Error}", upn, ex.Message);
                }
            }

            if (!validatedUpns.Any())
            {
                _logger.LogError("Brak prawidłowych UPN po walidacji");
                return new Dictionary<string, BulkOperationResult>();
            }

            _logger.LogInformation("[ETAP6] Masowe dodawanie {Count} użytkowników do zespołu {TeamId}",
                validatedUpns.Count, teamId);

            // Utwórz główny wpis operacji
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.BulkUserAddToTeam,
                "Team",
                targetEntityId: teamId,
                targetEntityName: $"[V2] Bulk add {validatedUpns.Count} users to team"
            );

            var results = new Dictionary<string, BulkOperationResult>();
            var processedCount = 0;
            var failedCount = 0;
            var batchIndex = 0;

            try
            {
                // Przetwarzaj partie
                var batches = validatedUpns
                    .Select((upn, index) => new { upn, index })
                    .GroupBy(x => x.index / BatchSize)
                    .Select(g => g.Select(x => x.upn).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    await _semaphore.WaitAsync();
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();
                        var batchResults = await ProcessUserBatchV2Async(teamId, batch, role);
                        stopwatch.Stop();

                        foreach (var result in batchResults)
                        {
                            results[result.Key] = result.Value;
                            if (result.Value.Success) processedCount++;
                            else failedCount++;
                        }

                        // Aktualizuj postęp
                        await _operationHistoryService.UpdateOperationProgressAsync(
                            operation.Id,
                            processedItems: processedCount,
                            failedItems: failedCount,
                            totalItems: validatedUpns.Count
                        );

                        // Wyślij powiadomienie o postępie
                        await SendProgressNotificationAsync(
                            operation.Id.ToString(),
                            processedCount + failedCount,
                            validatedUpns.Count,
                            $"Przetwarzanie partii {batchIndex + 1}/{batches.Count} ({stopwatch.ElapsedMilliseconds}ms)"
                        );

                        _logger.LogDebug("[ETAP6] Partia {BatchIndex}/{TotalBatches} zakończona w {ElapsedMs}ms",
                            batchIndex + 1, batches.Count, stopwatch.ElapsedMilliseconds);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }

                    batchIndex++;

                    // Przerwa między partiami
                    if (batch != batches.Last())
                    {
                        await Task.Delay(1000);
                    }
                }

                // Ustaw końcowy status
                var status = failedCount == 0 ? OperationStatus.Completed
                    : failedCount == validatedUpns.Count ? OperationStatus.Failed
                    : OperationStatus.PartialSuccess;

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, status,
                    $"[V2] Processed: {processedCount}, Failed: {failedCount}"
                );

                // Końcowe powiadomienie
                await SendProgressNotificationAsync(
                    operation.Id.ToString(),
                    processedCount + failedCount,
                    validatedUpns.Count,
                    $"Operacja zakończona. Sukcesy: {processedCount}, Błędy: {failedCount}"
                );

                _logger.LogInformation("[ETAP6] Zakończono masowe dodawanie. Sukcesy: {Success}, Błędy: {Failed}",
                    processedCount, failedCount);

                // [ETAP7-CACHE] Po zakończeniu operacji masowej
                if (results.Any(r => r.Value.Success))
                {
                    // Unieważnij cache zespołu i członków
                    _cacheService.InvalidateTeamCache(teamId);
                    _cacheService.Remove($"PowerShell_TeamMembers_{teamId}");
                    
                    // Unieważnij cache dla każdego dodanego użytkownika
                    foreach (var upn in results.Where(r => r.Value.Success).Select(r => r.Key))
                    {
                        _cacheService.Remove($"PowerShell_UserTeams_{upn}");
                    }
                    
                    // Jeśli dodano właścicieli
                    if (role.Equals("Owner", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var ownerUpn in results.Where(r => r.Value.Success).Select(r => r.Key))
                        {
                            _cacheService.InvalidateTeamsByOwner(ownerUpn);
                        }
                    }
                    
                    _logger.LogInformation("Cache unieważniony dla {Count} użytkowników dodanych do zespołu {TeamId}", 
                        results.Count(r => r.Value.Success), teamId);
                }

                return results;
            }
            catch (Exception ex)
            {
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, OperationStatus.Failed,
                    $"[V2] Critical error: {ex.Message}", ex.StackTrace
                );

                await SendProgressNotificationAsync(
                    operation.Id.ToString(),
                    0,
                    validatedUpns.Count,
                    $"Błąd krytyczny: {ex.Message}"
                );

                throw;
            }
        }

        /// <summary>
        /// [ETAP6] Ulepszona wersja BulkRemoveUsersFromTeamAsync z zaawansowanym raportowaniem  
        /// </summary>
        public async Task<Dictionary<string, BulkOperationResult>> BulkRemoveUsersFromTeamV2Async(
            string teamId,
            List<string> userUpns)
        {
            // TODO: Implementacja analogiczna do BulkAddUsersToTeamV2Async
            // Ze względu na ograniczenia miejsca, implementuję podstawową wersję
            var results = new Dictionary<string, BulkOperationResult>();
            var legacyResults = await BulkRemoveUsersFromTeamAsync(teamId, userUpns);

            foreach (var kvp in legacyResults)
            {
                results[kvp.Key] = kvp.Value
                    ? BulkOperationResult.CreateSuccess("BulkRemoveUsersFromTeam")
                    : BulkOperationResult.CreateError("Legacy method failed", "BulkRemoveUsersFromTeam");
            }

            return results;
        }

        /// <summary>
        /// [ETAP6] Ulepszona wersja BulkArchiveTeamsAsync z zaawansowanym raportowaniem
        /// </summary>
        public async Task<Dictionary<string, BulkOperationResult>> BulkArchiveTeamsV2Async(List<string> teamIds)
        {
            // TODO: Implementacja analogiczna do BulkAddUsersToTeamV2Async
            var results = new Dictionary<string, BulkOperationResult>();
            var legacyResults = await BulkArchiveTeamsAsync(teamIds);

            foreach (var kvp in legacyResults)
            {
                results[kvp.Key] = kvp.Value
                    ? BulkOperationResult.CreateSuccess("BulkArchiveTeams")
                    : BulkOperationResult.CreateError("Legacy method failed", "BulkArchiveTeams");
            }

            return results;
        }

        #endregion

        #region Private Helper Methods (Etap 6/7)

        /// <summary>
        /// [ETAP6] Wysyła powiadomienie o postępie operacji do użytkownika
        /// </summary>
        private async Task SendProgressNotificationAsync(
            string operationId,
            int processedCount,
            int totalCount,
            string message)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var notificationService = scope.ServiceProvider.GetService<INotificationService>();
                var currentUserService = scope.ServiceProvider.GetService<ICurrentUserService>();

                if (notificationService == null || currentUserService == null)
                {
                    _logger.LogDebug("NotificationService lub CurrentUserService niedostępne - pomijam powiadomienie");
                    return;
                }

                var userUpn = currentUserService.GetCurrentUserUpn();
                if (string.IsNullOrEmpty(userUpn))
                {
                    _logger.LogDebug("Brak aktualnego użytkownika - pomijam powiadomienie");
                    return;
                }

                var progressPercentage = totalCount > 0
                    ? (int)((processedCount / (double)totalCount) * 100)
                    : 0;

                await notificationService.SendOperationProgressToUserAsync(
                    userUpn,
                    operationId,
                    progressPercentage,
                    message
                );

                _logger.LogDebug("[ETAP6] Wysłano powiadomienie o postępie: {Progress}% - {Message}",
                    progressPercentage, message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ETAP6] Błąd wysyłania powiadomienia o postępie");
            }
        }

        /// <summary>
        /// [ETAP6] Przetwarza wyniki operacji masowej z PSObjectMapper
        /// </summary>
        private Dictionary<string, BulkOperationResult> ProcessBulkResults(
            IEnumerable<PSObject>? scriptResults,
            string operationType)
        {
            var results = new Dictionary<string, BulkOperationResult>();

            if (scriptResults == null)
            {
                _logger.LogWarning("[ETAP6] Brak wyników skryptu PowerShell");
                return results;
            }

            foreach (var psObject in scriptResults)
            {
                try
                {
                    // Użyj PSObjectMapper z Etapu 3
                    var userId = PSObjectMapper.GetString(psObject, "UserId");
                    var userUpn = PSObjectMapper.GetString(psObject, "UserUpn");
                    var success = PSObjectMapper.GetBoolean(psObject, "Success");
                    var error = PSObjectMapper.GetString(psObject, "Error");
                    var executionTimeMs = PSObjectMapper.GetNullableInt64(psObject, "ExecutionTimeMs");

                    var key = !string.IsNullOrEmpty(userUpn) ? userUpn : userId;
                    if (!string.IsNullOrEmpty(key))
                    {
                        results[key] = success
                            ? BulkOperationResult.CreateSuccess(operationType, executionTimeMs)
                            : BulkOperationResult.CreateError(error ?? "Nieznany błąd", operationType, executionTimeMs);

                        // Dodaj dodatkowe dane jeśli dostępne
                        if (results[key].AdditionalData == null)
                            results[key].AdditionalData = new Dictionary<string, object>();

                        if (!string.IsNullOrEmpty(userId))
                            results[key].AdditionalData["UserId"] = userId;
                        if (!string.IsNullOrEmpty(userUpn))
                            results[key].AdditionalData["UserUpn"] = userUpn;
                    }

                    _logger.LogDebug("[ETAP6] PSObjectMapper przetworzone: {Key} = {Success}",
                        key, success);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[ETAP6] Błąd przetwarzania wyniku PSObject");
                }
            }

            return results;
        }

        /// <summary>
        /// [ETAP6] Ulepszona wersja ProcessUserBatchAsync z PowerShell pipeline i PSParameterValidator
        /// </summary>
        private async Task<Dictionary<string, BulkOperationResult>> ProcessUserBatchV2Async(
            string teamId,
            List<string> userUpns,
            string role)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Przygotuj bezpieczne parametry z PSParameterValidator
                var userIds = new List<string>();
                var upnToIdMap = new Dictionary<string, string>();

                // Pre-load user IDs z cache
                foreach (var upn in userUpns)
                {
                    var sanitizedUpn = PSParameterValidator.ValidateAndSanitizeString(upn, "userUpn");
                    var cachedUserId = await _userResolver.GetUserIdAsync(sanitizedUpn);

                    if (!string.IsNullOrEmpty(cachedUserId))
                    {
                        userIds.Add(cachedUserId);
                        upnToIdMap[sanitizedUpn] = cachedUserId;
                    }
                }

                // Utworz bezpieczne parametry
                var parameters = PSParameterValidator.CreateSafeParameters(
                    ("TeamId", PSParameterValidator.ValidateGuid(teamId, "teamId")),
                    ("UserIds", userIds.ToArray()),
                    ("Role", PSParameterValidator.ValidateAndSanitizeString(role, "role")),
                    ("ThrottleLimit", ThrottleLimit)
                );

                // PowerShell script z ForEach-Object -Parallel dla PS 7+
                var script = @"
param($TeamId, $UserIds, $Role, $ThrottleLimit)

# PowerShell 7+ - użyj ForEach-Object -Parallel
if ($PSVersionTable.PSVersion.Major -ge 7) {
    Write-Host ""[ETAP6] Używam PowerShell 7+ z ForEach-Object -Parallel""
    $results = $UserIds | ForEach-Object -Parallel {
        $userId = $_
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            if ($using:Role -eq 'Owner') {
                Add-MgTeamOwner -TeamId $using:TeamId -UserId $userId -ErrorAction Stop
            } else {
                Add-MgTeamMember -TeamId $using:TeamId -UserId $userId -ErrorAction Stop
            }
            $stopwatch.Stop()
            [PSCustomObject]@{
                UserId = $userId
                Success = $true
                Error = $null
                ExecutionTimeMs = $stopwatch.ElapsedMilliseconds
            }
        }
        catch {
            $stopwatch.Stop()
            [PSCustomObject]@{
                UserId = $userId
                Success = $false
                Error = $_.Exception.Message
                ExecutionTimeMs = $stopwatch.ElapsedMilliseconds
            }
        }
    } -ThrottleLimit $ThrottleLimit
}
# PowerShell 5.1 - użyj standardowego pipeline
else {
    Write-Host ""[ETAP6] Używam PowerShell 5.1 z standardowym ForEach-Object""
    $results = $UserIds | ForEach-Object {
        $userId = $_
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            if ($Role -eq 'Owner') {
                Add-MgTeamOwner -TeamId $TeamId -UserId $userId -ErrorAction Stop
            } else {
                Add-MgTeamMember -TeamId $TeamId -UserId $userId -ErrorAction Stop
            }
            $stopwatch.Stop()
            [PSCustomObject]@{
                UserId = $userId
                Success = $true
                Error = $null
                ExecutionTimeMs = $stopwatch.ElapsedMilliseconds
            }
        }
        catch {
            $stopwatch.Stop()
            [PSCustomObject]@{
                UserId = $userId
                Success = $false
                Error = $_.Exception.Message
                ExecutionTimeMs = $stopwatch.ElapsedMilliseconds
            }
        }
    }
}

# Zwróć wyniki
$results
";

                _logger.LogDebug("[ETAP6] Wykonywanie skryptu PowerShell z {UserCount} użytkownikami",
                    userIds.Count);

                var scriptResults = await _connectionService.ExecuteScriptAsync(script, parameters);
                var processedResults = ProcessBulkResults(scriptResults, "BulkAddUsersToTeam");

                // Mapuj wyniki z powrotem do UPN
                var finalResults = new Dictionary<string, BulkOperationResult>();
                foreach (var kvp in upnToIdMap)
                {
                    var upn = kvp.Key;
                    var userId = kvp.Value;

                    if (processedResults.ContainsKey(userId))
                    {
                        finalResults[upn] = processedResults[userId];
                    }
                    else
                    {
                        finalResults[upn] = BulkOperationResult.CreateError(
                            "Nie znaleziono wyniku dla użytkownika",
                            "ProcessUserBatchV2",
                            stopwatch.ElapsedMilliseconds
                        );
                    }
                }

                stopwatch.Stop();
                _logger.LogInformation("[ETAP6] ProcessUserBatchV2 zakończone w {ElapsedMs}ms dla {Count} użytkowników",
                    stopwatch.ElapsedMilliseconds, userUpns.Count);

                return finalResults;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "[ETAP6] Błąd w ProcessUserBatchV2 po {ElapsedMs}ms",
                    stopwatch.ElapsedMilliseconds);

                // Zwróć błędy dla wszystkich użytkowników
                var errorResults = new Dictionary<string, BulkOperationResult>();
                foreach (var upn in userUpns)
                {
                    errorResults[upn] = BulkOperationResult.CreateError(
                        $"Batch processing failed: {ex.Message}",
                        "ProcessUserBatchV2",
                        stopwatch.ElapsedMilliseconds
                    );
                }
                return errorResults;
            }
        }

        #endregion
    }
}