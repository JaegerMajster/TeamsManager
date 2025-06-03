using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;

namespace TeamsManager.Core.Services.PowerShell
{
    /// <summary>
    /// Implementacja serwisu zarządzającego operacjami masowymi w Microsoft 365 przez PowerShell
    /// </summary>
    public class PowerShellBulkOperationsService : IPowerShellBulkOperationsService
    {
        private readonly IPowerShellConnectionService _connectionService;
        private readonly IPowerShellCacheService _cacheService;
        private readonly ICurrentUserService _currentUserService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<PowerShellBulkOperationsService> _logger;

        // Stałe konfiguracyjne
        private const int BatchSize = 50;

        // Semaphore dla kontroli współbieżności
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(3, 3);

        public PowerShellBulkOperationsService(
            IPowerShellConnectionService connectionService,
            IPowerShellCacheService cacheService,
            ICurrentUserService currentUserService,
            INotificationService notificationService,
            ILogger<PowerShellBulkOperationsService> logger)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Dictionary<string, bool>> BulkAddUsersToTeamAsync(
            string teamId,
            List<string> userUpns,
            string role = "Member")
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            var operationId = Guid.NewGuid().ToString();

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 5, 
                $"Rozpoczynanie masowego dodawania {userUpns?.Count ?? 0} użytkowników do zespołu...");
            await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                $"🚀 Rozpoczęto masowe dodawanie użytkowników do zespołu", "info");

            if (!_connectionService.ValidateRunspaceState())
            {
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "❌ Środowisko PowerShell nie jest gotowe", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona niepowodzeniem - błąd środowiska");
                return new Dictionary<string, bool>();
            }

            if (!userUpns?.Any() ?? true)
            {
                _logger.LogWarning("Lista użytkowników jest pusta.");
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "⚠️ Lista użytkowników jest pusta", "warning");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona - brak użytkowników do przetworzenia");
                return new Dictionary<string, bool>();
            }

            _logger.LogInformation("Masowe dodawanie {Count} użytkowników do zespołu {TeamId}",
                userUpns!.Count, teamId);

            var results = new Dictionary<string, bool>();

            // Podziel na partie
            var batches = userUpns
                .Select((upn, index) => new { upn, index })
                .GroupBy(x => x.index / BatchSize)
                .Select(g => g.Select(x => x.upn).ToList())
                .ToList();

            var totalBatches = batches.Count;
            var processedBatches = 0;

            foreach (var batch in batches)
            {
                await _semaphore.WaitAsync(); // Kontrola współbieżności
                try
                {
                    processedBatches++;
                    var progress = 5 + (int)((processedBatches / (float)totalBatches) * 85);
                    
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, progress, 
                        $"Przetwarzanie partii {processedBatches}/{totalBatches} ({batch.Count} użytkowników)...");

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

            var successCount = results.Count(r => r.Value);
            var failedCount = results.Count(r => !r.Value);

            _logger.LogInformation("Zakończono masowe dodawanie. Sukcesy: {Success}, Błędy: {Failed}",
                successCount, failedCount);

            // Invalidate cache dla zespołu
            _cacheService.InvalidateTeamCache(teamId);

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                $"Operacja zakończona: {successCount} sukcesów, {failedCount} błędów");
            await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                $"✅ Masowe dodawanie zakończone: {successCount} użytkowników dodanych, {failedCount} błędów", 
                failedCount > 0 ? "warning" : "success");

            return results;
        }

        public async Task<Dictionary<string, bool>> BulkRemoveUsersFromTeamAsync(
            string teamId,
            List<string> userUpns)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            var operationId = Guid.NewGuid().ToString();

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 5, 
                $"Rozpoczynanie masowego usuwania {userUpns?.Count ?? 0} użytkowników z zespołu...");
            await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                $"🚀 Rozpoczęto masowe usuwanie użytkowników z zespołu", "info");

            if (!_connectionService.ValidateRunspaceState())
            {
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "❌ Środowisko PowerShell nie jest gotowe", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona niepowodzeniem - błąd środowiska");
                return new Dictionary<string, bool>();
            }

            if (!userUpns?.Any() ?? true)
            {
                _logger.LogWarning("Lista użytkowników jest pusta.");
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "⚠️ Lista użytkowników jest pusta", "warning");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona - brak użytkowników do przetworzenia");
                return new Dictionary<string, bool>();
            }

            _logger.LogInformation("Masowe usuwanie {Count} użytkowników z zespołu {TeamId}",
                userUpns!.Count, teamId);

            var results = new Dictionary<string, bool>();

            // Podziel na partie
            var batches = userUpns
                .Select((upn, index) => new { upn, index })
                .GroupBy(x => x.index / BatchSize)
                .Select(g => g.Select(x => x.upn).ToList())
                .ToList();

            var totalBatches = batches.Count;
            var processedBatches = 0;

            foreach (var batch in batches)
            {
                await _semaphore.WaitAsync();
                try
                {
                    processedBatches++;
                    var progress = 5 + (int)((processedBatches / (float)totalBatches) * 85);
                    
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, progress, 
                        $"Przetwarzanie partii {processedBatches}/{totalBatches} ({batch.Count} użytkowników)...");

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

                // Krótka przerwa między partiami
                if (batch != batches.Last())
                {
                    await Task.Delay(1000);
                }
            }

            var successCount = results.Count(r => r.Value);
            var failedCount = results.Count(r => !r.Value);

            _logger.LogInformation("Zakończono masowe usuwanie. Sukcesy: {Success}, Błędy: {Failed}",
                successCount, failedCount);

            // Invalidate cache
            _cacheService.InvalidateTeamCache(teamId);

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                $"Operacja zakończona: {successCount} sukcesów, {failedCount} błędów");
            await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                $"✅ Masowe usuwanie zakończone: {successCount} użytkowników usuniętych, {failedCount} błędów", 
                failedCount > 0 ? "warning" : "success");

            return results;
        }

        public async Task<Dictionary<string, bool>> BulkArchiveTeamsAsync(List<string> teamIds)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            var operationId = Guid.NewGuid().ToString();

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 5, 
                $"Rozpoczynanie masowej archiwizacji {teamIds?.Count ?? 0} zespołów...");
            await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                $"🚀 Rozpoczęto masową archiwizację zespołów", "info");

            if (!_connectionService.ValidateRunspaceState())
            {
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "❌ Środowisko PowerShell nie jest gotowe", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona niepowodzeniem - błąd środowiska");
                return new Dictionary<string, bool>();
            }

            if (!teamIds?.Any() ?? true)
            {
                _logger.LogWarning("Lista zespołów jest pusta.");
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "⚠️ Lista zespołów jest pusta", "warning");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona - brak zespołów do przetworzenia");
                return new Dictionary<string, bool>();
            }

            _logger.LogInformation("Masowa archiwizacja {Count} zespołów", teamIds!.Count);

            var script = new StringBuilder();
            script.AppendLine("$results = @{}");

            var totalTeams = teamIds.Count;
            var processedTeams = 0;

            foreach (var teamId in teamIds)
            {
                processedTeams++;
                var progress = 30 + (int)((processedTeams / (float)totalTeams) * 50);
                
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, progress, 
                    $"Archiwizacja zespołu {processedTeams}/{totalTeams}...");

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

            var successCount = results.Count(r => r.Value);
            var failedCount = results.Count(r => !r.Value);

            _logger.LogInformation("Zakończono archiwizację. Sukcesy: {Success}, Błędy: {Failed}",
                successCount, failedCount);

            // Invalidate cache dla wszystkich zespołów
            _cacheService.InvalidateAllCache();

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                $"Operacja zakończona: {successCount} zarchiwizowanych, {failedCount} błędów");
            await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                $"✅ Masowa archiwizacja zakończona: {successCount} zespołów zarchiwizowanych, {failedCount} błędów", 
                failedCount > 0 ? "warning" : "success");

            return results;
        }

        public async Task<Dictionary<string, bool>> BulkUpdateUserPropertiesAsync(
            Dictionary<string, Dictionary<string, string>> userUpdates)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            var operationId = Guid.NewGuid().ToString();

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 5, 
                $"Rozpoczynanie masowej aktualizacji właściwości {userUpdates?.Count ?? 0} użytkowników...");
            await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                $"🚀 Rozpoczęto masową aktualizację właściwości użytkowników", "info");

            if (!_connectionService.ValidateRunspaceState())
            {
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "❌ Środowisko PowerShell nie jest gotowe", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona niepowodzeniem - błąd środowiska");
                return new Dictionary<string, bool>();
            }

            if (!userUpdates?.Any() ?? true)
            {
                _logger.LogWarning("Lista aktualizacji użytkowników jest pusta.");
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    "⚠️ Lista aktualizacji jest pusta", "warning");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona - brak użytkowników do przetworzenia");
                return new Dictionary<string, bool>();
            }

            _logger.LogInformation("Masowa aktualizacja właściwości dla {Count} użytkowników", userUpdates!.Count);

            var script = new StringBuilder();
            script.AppendLine("$results = @{}");

            var totalUsers = userUpdates.Count;
            var processedUsers = 0;

            foreach (var kvp in userUpdates)
            {
                var userUpn = kvp.Key;
                var properties = kvp.Value;

                processedUsers++;
                var progress = 20 + (int)((processedUsers / (float)totalUsers) * 60);
                
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, progress, 
                    $"Aktualizacja użytkownika {processedUsers}/{totalUsers}: {userUpn}...");

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

            var successCount = results.Count(r => r.Value);
            var failedCount = results.Count(r => !r.Value);

            _logger.LogInformation("Zakończono masową aktualizację. Sukcesy: {Success}, Błędy: {Failed}",
                successCount, failedCount);

            // Invalidate cache dla wszystkich zaktualizowanych użytkowników
            foreach (var userUpn in userUpdates.Keys)
            {
                _cacheService.InvalidateUserCache(userUpn: userUpn);
            }

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                $"Operacja zakończona: {successCount} zaktualizowanych, {failedCount} błędów");
            await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                $"✅ Masowa aktualizacja zakończona: {successCount} użytkowników zaktualizowanych, {failedCount} błędów", 
                failedCount > 0 ? "warning" : "success");

            return results;
        }

        public async Task<bool> ArchiveTeamAndDeactivateExclusiveUsersAsync(string teamId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            var operationId = Guid.NewGuid().ToString();

            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 5, 
                "Rozpoczynanie archiwizacji zespołu i dezaktywacji ekskluzywnych użytkowników...");
            await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                $"🚀 Rozpoczęto archiwizację zespołu i dezaktywację ekskluzywnych użytkowników", "info");

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

            _logger.LogInformation("Archiwizacja zespołu {TeamId} i dezaktywacja ekskluzywnych użytkowników", teamId);

            try
            {
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 20, 
                    "Pobieranie członków zespołu...");

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

                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 50, 
                    "Archiwizacja zespołu...");

                var results = await _connectionService.ExecuteScriptAsync(script);
                var result = results?.FirstOrDefault()?.BaseObject as Hashtable;

                var success = result?["Success"] as bool? ?? false;
                var deactivatedCount = Convert.ToInt32(result?["DeactivatedCount"] ?? 0);
                
                if (!success)
                {
                    var errors = result?["Errors"] as object[];
                    foreach (var error in errors ?? Array.Empty<object>())
                    {
                        _logger.LogError("Błąd operacji: {Error}", error);
                    }
                    
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        $"⚠️ Operacja zakończona z błędami. Dezaktywowano {deactivatedCount} użytkowników", "warning");
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                        $"✅ Zespół zarchiwizowany. Dezaktywowano {deactivatedCount} ekskluzywnych użytkowników", "success");
                }

                // Invalidate cache
                _cacheService.InvalidateTeamCache(teamId);
                _cacheService.InvalidateAllCache();

                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    $"Operacja zakończona. Dezaktywowano {deactivatedCount} użytkowników");

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas archiwizacji zespołu i dezaktywacji użytkowników");
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, 
                    $"❌ Błąd krytyczny podczas operacji: {ex.Message}", "error");
                await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100, 
                    "Operacja zakończona błędem krytycznym");
                return false;
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
                var cachedUserId = await _cacheService.GetUserIdAsync(upn);
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
                var cachedUserId = await _cacheService.GetUserIdAsync(upn);
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
    }
}