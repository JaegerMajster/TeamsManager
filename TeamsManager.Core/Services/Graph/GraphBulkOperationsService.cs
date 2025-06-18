using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Exceptions.Graph;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Services.Graph
{
    /// <summary>
    /// Serwis zarządzający operacjami masowymi w Microsoft 365 przez Graph API
    /// Implementacja operacji masowych z pełnym wsparciem dla Graph Batch API
    /// </summary>
    public class GraphBulkOperationsService : IGraphBulkOperationsService
    {
        private readonly IModernHttpService _httpService;
        private readonly IGraphConnectionService _connectionService;
        private readonly ILogger<GraphBulkOperationsService> _logger;
        private readonly GraphApiConfiguration _graphConfig;

        // Graph API Batch limits
        private const int MaxBatchSize = 20; // Graph API limit
        private const int MaxConcurrentBatches = 5;
        private const int DefaultRetryDelayMs = 1000;
        private const int MaxRetryDelayMs = 30000;

        public GraphBulkOperationsService(
            IModernHttpService httpService,
            IGraphConnectionService connectionService,
            ILogger<GraphBulkOperationsService> logger,
            GraphApiConfiguration? graphConfig = null)
        {
            _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _graphConfig = graphConfig ?? new GraphApiConfiguration();
        }

        #region Basic Bulk Operations

        /// <summary>
        /// Masowo dodaje użytkowników do zespołu
        /// Graph API Endpoint: POST /v1.0/$batch (with POST /v1.0/teams/{team-id}/members requests)
        /// </summary>
        public async Task<Dictionary<string, bool>> BulkAddUsersToTeamAsync(
            string teamId,
            List<string> userUpns,
            string role = "Member",
            IProgress<BulkOperationProgress>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));

            if (userUpns == null || !userUpns.Any())
                throw new ArgumentException("Lista użytkowników nie może być pusta", nameof(userUpns));

            _logger.LogInformation("Rozpoczynam masowe dodawanie {Count} użytkowników do zespołu {TeamId}", 
                userUpns.Count, teamId);

            var result = new Dictionary<string, bool>();
            var progressReporter = new BulkOperationProgress
            {
                TotalOperations = userUpns.Count,
                CurrentOperation = "Przygotowywanie operacji batch"
            };

            try
            {
                // Sprawdź token
                await _connectionService.EnsureValidTokenAsync();

                // Podziel na batche
                var batches = CreateBatches(userUpns, MaxBatchSize);
                var batchIndex = 0;

                foreach (var batch in batches)
                {
                    batchIndex++;
                    progressReporter.CurrentOperation = $"Przetwarzanie batch {batchIndex}/{batches.Count}";
                    progress?.Report(progressReporter);

                    var batchOperations = batch.Select(userUpn => 
                        GraphBatchOperation.CreatePost(
                            $"/v1.0/teams/{teamId}/members",
                            new
                            {
                                roles = new[] { role },
                                user = new { userPrincipalName = userUpn }
                            },
                            "AddTeamMember",
                            userUpn
                        )).ToList();

                    var batchResult = await ExecuteBatchOperationsAsync(batchOperations, 
                        await _connectionService.GetAccessTokenAsync());

                    // Przetwórz wyniki batch
                    foreach (var operation in batchOperations)
                    {
                        var userUpn = operation.EntityId!;
                        var batchResponse = batchResult.BatchResults.FirstOrDefault(r => r.Id == operation.Id);
                        
                        if (batchResponse?.IsSuccessful == true)
                        {
                            result[userUpn] = true;
                            progressReporter.SuccessfulOperations++;
                        }
                        else
                        {
                            result[userUpn] = false;
                            progressReporter.FailedOperations++;
                            _logger.LogWarning("Nie udało się dodać użytkownika {UserUpn} do zespołu {TeamId}: {Error}",
                                userUpn, teamId, batchResponse?.ErrorMessage ?? "Nieznany błąd");
                        }

                        progressReporter.CompletedOperations++;
                    }

                    progress?.Report(progressReporter);
                }

                _logger.LogInformation("Zakończono masowe dodawanie użytkowników do zespołu {TeamId}. " +
                    "Sukces: {Success}/{Total}", teamId, progressReporter.SuccessfulOperations, userUpns.Count);

                return result;
            }
            catch (GraphConnectionException ex)
            {
                // Zwróć false dla wszystkich użytkowników przed przekazaniem do GraphExceptionHandler
                foreach (var userUpn in userUpns.Where(u => !result.ContainsKey(u)))
                {
                    result[userUpn] = false;
                }
                
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => BulkAddUsersToTeamAsync(teamId, userUpns, role, progress),
                    _logger,
                    "BulkAddUsersToTeam",
                    defaultValue: result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas masowego dodawania użytkowników do zespołu {TeamId}", teamId);
                
                // Zwróć false dla wszystkich użytkowników, którzy nie zostali jeszcze przetworzeni
                foreach (var userUpn in userUpns.Where(u => !result.ContainsKey(u)))
                {
                    result[userUpn] = false;
                }

                return result;
            }
        }

        /// <summary>
        /// Masowo usuwa użytkowników z zespołu
        /// Graph API Endpoint: POST /v1.0/$batch (with DELETE /v1.0/teams/{team-id}/members/{membership-id} requests)
        /// </summary>
        public async Task<Dictionary<string, bool>> BulkRemoveUsersFromTeamAsync(
            string teamId,
            List<string> userUpns,
            IProgress<BulkOperationProgress>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));

            if (userUpns == null || !userUpns.Any())
                throw new ArgumentException("Lista użytkowników nie może być pusta", nameof(userUpns));

            _logger.LogInformation("Rozpoczynam masowe usuwanie {Count} użytkowników z zespołu {TeamId}", 
                userUpns.Count, teamId);

            var result = new Dictionary<string, bool>();
            var progressReporter = new BulkOperationProgress
            {
                TotalOperations = userUpns.Count,
                CurrentOperation = "Pobieranie członków zespołu"
            };

            try
            {
                // Sprawdź token
                await _connectionService.EnsureValidTokenAsync();

                // Najpierw pobierz wszystkich członków zespołu aby uzyskać membership-id
                var accessToken = await _connectionService.GetAccessTokenAsync();
                var headers = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {accessToken}"
                };
                
                var membersResponse = await _httpService.GetAsync(
                    $"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.TeamMembers(teamId)}",
                    headers);

                if (!membersResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Nie udało się pobrać członków zespołu {TeamId}: {StatusCode}", 
                        teamId, membersResponse.StatusCode);
                    
                    foreach (var userUpn in userUpns)
                        result[userUpn] = false;
                    
                    return result;
                }

                var membersContent = await membersResponse.Content.ReadAsStringAsync();
                var membersData = JsonSerializer.Deserialize<JsonElement>(membersContent);
                
                // Mapuj UPN na membership-id
                var membershipMap = new Dictionary<string, string>();
                
                if (membersData.TryGetProperty("value", out var membersArray))
                {
                    foreach (var member in membersArray.EnumerateArray())
                    {
                        if (member.TryGetProperty("id", out var idElement) &&
                            member.TryGetProperty("email", out var emailElement))
                        {
                            var membershipId = idElement.GetString();
                            var email = emailElement.GetString();
                            
                            if (!string.IsNullOrEmpty(membershipId) && !string.IsNullOrEmpty(email))
                            {
                                membershipMap[email.ToLowerInvariant()] = membershipId;
                            }
                        }
                        
                        // Sprawdź też userPrincipalName jeśli jest dostępne
                        if (member.TryGetProperty("id", out var idElement2) &&
                            member.TryGetProperty("userPrincipalName", out var upnElement))
                        {
                            var membershipId = idElement2.GetString();
                            var upn = upnElement.GetString();
                            
                            if (!string.IsNullOrEmpty(membershipId) && !string.IsNullOrEmpty(upn))
                            {
                                membershipMap[upn.ToLowerInvariant()] = membershipId;
                            }
                        }
                    }
                }

                progressReporter.CurrentOperation = "Przygotowywanie operacji batch";
                progress?.Report(progressReporter);

                // Podziel na batche
                var batches = CreateBatches(userUpns, MaxBatchSize);
                var batchIndex = 0;

                foreach (var batch in batches)
                {
                    batchIndex++;
                    progressReporter.CurrentOperation = $"Przetwarzanie batch {batchIndex}/{batches.Count}";
                    progress?.Report(progressReporter);

                    var batchOperations = new List<GraphBatchOperation>();

                    foreach (var userUpn in batch)
                    {
                        var normalizedUpn = userUpn.ToLowerInvariant();
                        
                        if (membershipMap.TryGetValue(normalizedUpn, out var membershipId))
                        {
                            batchOperations.Add(GraphBatchOperation.CreateDelete(
                                $"/v1.0/teams/{teamId}/members/{membershipId}",
                                "RemoveTeamMember",
                                userUpn
                            ));
                        }
                        else
                        {
                            // Użytkownik nie jest członkiem zespołu
                            result[userUpn] = false;
                            progressReporter.FailedOperations++;
                            progressReporter.CompletedOperations++;
                            
                            _logger.LogWarning("Użytkownik {UserUpn} nie jest członkiem zespołu {TeamId}", 
                                userUpn, teamId);
                        }
                    }

                    if (batchOperations.Any())
                    {
                        var batchResult = await ExecuteBatchOperationsAsync(batchOperations, 
                            await _connectionService.GetAccessTokenAsync());

                        // Przetwórz wyniki batch
                        foreach (var operation in batchOperations)
                        {
                            var userUpn = operation.EntityId!;
                            var batchResponse = batchResult.BatchResults.FirstOrDefault(r => r.Id == operation.Id);
                            
                            if (batchResponse?.IsSuccessful == true)
                            {
                                result[userUpn] = true;
                                progressReporter.SuccessfulOperations++;
                            }
                            else
                            {
                                result[userUpn] = false;
                                progressReporter.FailedOperations++;
                                _logger.LogWarning("Nie udało się usunąć użytkownika {UserUpn} z zespołu {TeamId}: {Error}",
                                    userUpn, teamId, batchResponse?.ErrorMessage ?? "Nieznany błąd");
                            }

                            progressReporter.CompletedOperations++;
                        }
                    }

                    progress?.Report(progressReporter);
                }

                _logger.LogInformation("Zakończono masowe usuwanie użytkowników z zespołu {TeamId}. " +
                    "Sukces: {Success}/{Total}", teamId, progressReporter.SuccessfulOperations, userUpns.Count);

                return result;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => BulkRemoveUsersFromTeamAsync(teamId, userUpns, progress),
                    _logger,
                    "BulkRemoveUsersFromTeam",
                    defaultValue: result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas masowego usuwania użytkowników z zespołu {TeamId}", teamId);
                
                // Zwróć false dla wszystkich użytkowników, którzy nie zostali jeszcze przetworzeni
                foreach (var userUpn in userUpns.Where(u => !result.ContainsKey(u)))
                {
                    result[userUpn] = false;
                }

                return result;
            }
        }

        /// <summary>
        /// Masowo archiwizuje zespoły
        /// Graph API Endpoint: POST /v1.0/$batch (with POST /v1.0/teams/{team-id}/archive requests)
        /// </summary>
        public async Task<Dictionary<string, bool>> BulkArchiveTeamsAsync(
            List<string> teamIds,
            IProgress<BulkOperationProgress>? progress = null)
        {
            if (teamIds == null || !teamIds.Any())
                throw new ArgumentException("Lista zespołów nie może być pusta", nameof(teamIds));

            _logger.LogInformation("Rozpoczynam masową archiwizację {Count} zespołów", teamIds.Count);

            var result = new Dictionary<string, bool>();
            var progressReporter = new BulkOperationProgress
            {
                TotalOperations = teamIds.Count,
                CurrentOperation = "Przygotowywanie operacji batch"
            };

            try
            {
                // Sprawdź token
                await _connectionService.EnsureValidTokenAsync();

                // Podziel na batche
                var batches = CreateBatches(teamIds, MaxBatchSize);
                var batchIndex = 0;

                foreach (var batch in batches)
                {
                    batchIndex++;
                    progressReporter.CurrentOperation = $"Przetwarzanie batch {batchIndex}/{batches.Count}";
                    progress?.Report(progressReporter);

                    var batchOperations = batch.Select(teamId => 
                        GraphBatchOperation.CreatePost(
                            $"/v1.0/teams/{teamId}/archive",
                            new { shouldSetSpoSiteReadOnlyForMembers = true }, // Opcjonalnie ustaw SharePoint jako read-only
                            "ArchiveTeam",
                            teamId
                        )).ToList();

                    var batchResult = await ExecuteBatchOperationsAsync(batchOperations, 
                        await _connectionService.GetAccessTokenAsync());

                    // Przetwórz wyniki batch
                    foreach (var operation in batchOperations)
                    {
                        var teamId = operation.EntityId!;
                        var batchResponse = batchResult.BatchResults.FirstOrDefault(r => r.Id == operation.Id);
                        
                        if (batchResponse?.IsSuccessful == true)
                        {
                            result[teamId] = true;
                            progressReporter.SuccessfulOperations++;
                        }
                        else
                        {
                            result[teamId] = false;
                            progressReporter.FailedOperations++;
                            _logger.LogWarning("Nie udało się zarchiwizować zespołu {TeamId}: {Error}",
                                teamId, batchResponse?.ErrorMessage ?? "Nieznany błąd");
                        }

                        progressReporter.CompletedOperations++;
                    }

                    progress?.Report(progressReporter);
                }

                _logger.LogInformation("Zakończono masową archiwizację zespołów. " +
                    "Sukces: {Success}/{Total}", progressReporter.SuccessfulOperations, teamIds.Count);

                return result;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => BulkArchiveTeamsAsync(teamIds, progress),
                    _logger,
                    "BulkArchiveTeams",
                    defaultValue: result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas masowej archiwizacji zespołów");
                
                // Zwróć false dla wszystkich zespołów, które nie zostały jeszcze przetworzone
                foreach (var teamId in teamIds.Where(t => !result.ContainsKey(t)))
                {
                    result[teamId] = false;
                }

                return result;
            }
        }

        #endregion

        #region Orchestrator Methods

        /// <summary>
        /// Masowo archiwizuje zespoły (wersja z batch size dla orkiestratora)
        /// Graph API Endpoint: POST /v1.0/$batch (max 20 requests per batch - Graph API limit)
        /// </summary>
        public async Task<GraphBulkResult> ArchiveTeamsAsync(string[] teamIds, string accessToken, int batchSize = 20)
        {
            if (teamIds == null || !teamIds.Any())
                throw new ArgumentException("Lista zespołów nie może być pusta", nameof(teamIds));

            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("Token dostępu nie może być pusty", nameof(accessToken));

            if (batchSize > MaxBatchSize)
                batchSize = MaxBatchSize;

            _logger.LogInformation("Rozpoczynam masową archiwizację {Count} zespołów z batch size {BatchSize}", 
                teamIds.Length, batchSize);

            var startTime = DateTime.UtcNow;

            try
            {
                var allResults = new List<GraphBatchOperationResult>();
                var batches = CreateBatches(teamIds.ToList(), batchSize);
                var successCount = 0;
                var errorCount = 0;

                foreach (var batch in batches)
                {
                    var batchOperations = batch.Select(teamId => 
                        GraphBatchOperation.CreatePost(
                            $"/v1.0/teams/{teamId}/archive",
                            new { shouldSetSpoSiteReadOnlyForMembers = true },
                            "ArchiveTeam",
                            teamId
                        )).ToList();

                    var batchResult = await ExecuteBatchOperationsAsync(batchOperations, accessToken);
                    
                    if (batchResult.Success)
                    {
                        allResults.AddRange(batchResult.BatchResults);
                        
                        foreach (var result in batchResult.BatchResults)
                        {
                            if (result.IsSuccessful)
                                successCount++;
                            else
                                errorCount++;
                        }
                    }
                    else
                    {
                        errorCount += batch.Count;
                        _logger.LogError("Błąd batch dla archiwizacji zespołów: {Error}", batchResult.ErrorMessage);
                    }
                }

                var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                var finalResult = GraphBulkResult.CreateSuccess("/$batch", "POST", executionTime);
                finalResult.BatchResults = allResults;
                finalResult.BatchId = Guid.NewGuid().ToString();

                // Dodaj statystyki do metadanych
                finalResult.AddMetadata("TotalTeams", teamIds.Length);
                finalResult.AddMetadata("SuccessfulArchives", successCount);
                finalResult.AddMetadata("FailedArchives", errorCount);
                finalResult.AddMetadata("BatchSize", batchSize);
                finalResult.AddMetadata("BatchCount", batches.Count);

                _logger.LogInformation("Zakończono masową archiwizację zespołów. " +
                    "Sukces: {Success}/{Total} w {ExecutionTime}ms", 
                    successCount, teamIds.Length, executionTime);

                return finalResult;
            }
            catch (GraphConnectionException ex)
            {
                var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                var result = await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => ArchiveTeamsAsync(teamIds, accessToken, batchSize),
                    _logger,
                    "ArchiveTeams",
                    defaultValue: GraphBulkResult.CreateError(ex.Message, "/$batch", "POST", null, executionTime));
                
                result.AddMetadata("TotalTeams", teamIds.Length);
                result.AddMetadata("BatchSize", batchSize);
                return result;
            }
            catch (Exception ex)
            {
                var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, "Błąd podczas masowej archiwizacji zespołów");
                
                var errorResult = GraphBulkResult.CreateError(ex.Message, "/$batch", "POST", 
                    null, executionTime);
                errorResult.AddMetadata("TotalTeams", teamIds.Length);
                errorResult.AddMetadata("BatchSize", batchSize);
                
                return errorResult;
            }
        }

        /// <summary>
        /// Masowo tworzy zespoły (dla orkiestratora)
        /// Graph API Endpoint: POST /v1.0/$batch (with POST /v1.0/teams requests)
        /// </summary>
        public async Task<GraphBulkResult> CreateTeamsAsync(GraphBatchOperation[] teamCreateRequests, string accessToken)
        {
            if (teamCreateRequests == null || !teamCreateRequests.Any())
                throw new ArgumentException("Lista żądań nie może być pusta", nameof(teamCreateRequests));

            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("Token dostępu nie może być pusty", nameof(accessToken));

            _logger.LogInformation("Rozpoczynam masowe tworzenie {Count} zespołów", teamCreateRequests.Length);

            var startTime = DateTime.UtcNow;

            try
            {
                var allResults = new List<GraphBatchOperationResult>();
                var batches = CreateBatches(teamCreateRequests.ToList(), MaxBatchSize);
                var successCount = 0;
                var errorCount = 0;

                foreach (var batch in batches)
                {
                    var batchResult = await ExecuteBatchOperationsAsync(batch, accessToken);
                    
                    if (batchResult.Success)
                    {
                        allResults.AddRange(batchResult.BatchResults);
                        
                        foreach (var result in batchResult.BatchResults)
                        {
                            if (result.IsSuccessful)
                            {
                                successCount++;
                                _logger.LogDebug("Pomyślnie utworzono zespół: {TeamId}", result.Id);
                            }
                            else
                            {
                                errorCount++;
                                _logger.LogWarning("Nie udało się utworzyć zespołu {TeamId}: {Error}", 
                                    result.Id, result.ErrorMessage);
                            }
                        }
                    }
                    else
                    {
                        errorCount += batch.Count;
                        _logger.LogError("Błąd batch dla tworzenia zespołów: {Error}", batchResult.ErrorMessage);
                    }
                }

                var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                var finalResult = GraphBulkResult.CreateSuccess("/$batch", "POST", executionTime);
                finalResult.BatchResults = allResults;
                finalResult.BatchId = Guid.NewGuid().ToString();

                // Dodaj statystyki do metadanych
                finalResult.AddMetadata("TotalTeamRequests", teamCreateRequests.Length);
                finalResult.AddMetadata("SuccessfulCreations", successCount);
                finalResult.AddMetadata("FailedCreations", errorCount);
                finalResult.AddMetadata("BatchCount", batches.Count);

                _logger.LogInformation("Zakończono masowe tworzenie zespołów. " +
                    "Sukces: {Success}/{Total} w {ExecutionTime}ms", 
                    successCount, teamCreateRequests.Length, executionTime);

                return finalResult;
            }
            catch (GraphConnectionException ex)
            {
                var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                var result = await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => CreateTeamsAsync(teamCreateRequests, accessToken),
                    _logger,
                    "CreateTeams",
                    defaultValue: GraphBulkResult.CreateError(ex.Message, "/$batch", "POST", null, executionTime));
                
                result.AddMetadata("TotalTeamRequests", teamCreateRequests.Length);
                return result;
            }
            catch (Exception ex)
            {
                var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, "Błąd podczas masowego tworzenia zespołów");
                
                var errorResult = GraphBulkResult.CreateError(ex.Message, "/$batch", "POST", 
                    null, executionTime);
                errorResult.AddMetadata("TotalTeamRequests", teamCreateRequests.Length);
                
                return errorResult;
            }
        }

        #endregion

        #region Advanced Bulk Operations

        /// <summary>
        /// Masowo aktualizuje właściwości użytkowników
        /// Graph API Endpoint: POST /v1.0/$batch (with PATCH /v1.0/users/{user-id} requests)
        /// </summary>
        public async Task<Dictionary<string, bool>> BulkUpdateUserPropertiesAsync(
            Dictionary<string, Dictionary<string, string>> userUpdates,
            IProgress<BulkOperationProgress>? progress = null)
        {
            if (userUpdates == null || !userUpdates.Any())
                throw new ArgumentException("Słownik aktualizacji nie może być pusty", nameof(userUpdates));

            _logger.LogInformation("Rozpoczynam masową aktualizację właściwości {Count} użytkowników", 
                userUpdates.Count);

            var result = new Dictionary<string, bool>();
            var progressReporter = new BulkOperationProgress
            {
                TotalOperations = userUpdates.Count,
                CurrentOperation = "Przygotowywanie operacji batch"
            };

            try
            {
                // Sprawdź token
                await _connectionService.EnsureValidTokenAsync();

                // Najpierw pobierz ID użytkowników na podstawie UPN
                var userIdMap = new Dictionary<string, string>();
                var userUpns = userUpdates.Keys.ToList();
                
                progressReporter.CurrentOperation = "Pobieranie ID użytkowników";
                progress?.Report(progressReporter);

                // Pobierz użytkowników w batchach aby uzyskać ich ID
                var userBatches = CreateBatches(userUpns, MaxBatchSize);
                
                foreach (var userBatch in userBatches)
                {
                    var getUserOperations = userBatch.Select(upn =>
                        GraphBatchOperation.CreateGet(
                            $"/v1.0/users/{upn}?$select=id,userPrincipalName",
                            "GetUser",
                            upn
                        )).ToList();

                    var getUserResult = await ExecuteBatchOperationsAsync(getUserOperations,
                        await _connectionService.GetAccessTokenAsync());

                    if (getUserResult.Success)
                    {
                        foreach (var operation in getUserOperations)
                        {
                            var userUpn = operation.EntityId!;
                            var batchResponse = getUserResult.BatchResults.FirstOrDefault(r => r.Id == operation.Id);

                            if (batchResponse?.IsSuccessful == true && batchResponse.Body != null)
                            {
                                var userData = JsonSerializer.Deserialize<JsonElement>(batchResponse.Body.ToString()!);
                                if (userData.TryGetProperty("id", out var idElement))
                                {
                                    var userId = idElement.GetString();
                                    if (!string.IsNullOrEmpty(userId))
                                    {
                                        userIdMap[userUpn] = userId;
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Nie udało się pobrać ID użytkownika {UserUpn}", userUpn);
                                result[userUpn] = false;
                                progressReporter.FailedOperations++;
                                progressReporter.CompletedOperations++;
                            }
                        }
                    }
                }

                // Teraz wykonaj aktualizacje właściwości
                var updateOperations = new List<GraphBatchOperation>();
                
                foreach (var userUpdate in userUpdates)
                {
                    var userUpn = userUpdate.Key;
                    var properties = userUpdate.Value;

                    if (userIdMap.TryGetValue(userUpn, out var userId) && properties.Any())
                    {
                        // Przygotuj dane do aktualizacji (tylko niepuste wartości)
                        var updateData = new Dictionary<string, object>();
                        
                        foreach (var prop in properties)
                        {
                            if (!string.IsNullOrWhiteSpace(prop.Value))
                            {
                                updateData[prop.Key] = prop.Value;
                            }
                        }

                        if (updateData.Any())
                        {
                            updateOperations.Add(GraphBatchOperation.CreatePatch(
                                $"/v1.0/users/{userId}",
                                updateData,
                                "UpdateUserProperties",
                                userUpn
                            ));
                        }
                        else
                        {
                            // Brak danych do aktualizacji
                            result[userUpn] = true; // Uznaj za sukces
                            progressReporter.SuccessfulOperations++;
                            progressReporter.CompletedOperations++;
                        }
                    }
                }

                // Wykonaj aktualizacje w batchach z parallel processing
                if (updateOperations.Any())
                {
                    progressReporter.CurrentOperation = "Wykonywanie aktualizacji właściwości";
                    progress?.Report(progressReporter);

                    var updateBatches = CreateBatches(updateOperations, MaxBatchSize);
                    var semaphore = new SemaphoreSlim(MaxConcurrentBatches, MaxConcurrentBatches);
                    var updateTasks = new List<Task>();

                    foreach (var batch in updateBatches)
                    {
                        var batchTask = ProcessUpdateBatchAsync(batch, result, progressReporter, progress, semaphore);
                        updateTasks.Add(batchTask);
                    }

                    await Task.WhenAll(updateTasks);
                }

                _logger.LogInformation("Zakończono masową aktualizację właściwości użytkowników. " +
                    "Sukces: {Success}/{Total}", progressReporter.SuccessfulOperations, userUpdates.Count);

                return result;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => BulkUpdateUserPropertiesAsync(userUpdates, progress),
                    _logger,
                    "BulkUpdateUserProperties",
                    defaultValue: result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas masowej aktualizacji właściwości użytkowników");
                
                // Zwróć false dla wszystkich użytkowników, którzy nie zostali jeszcze przetworzeni
                foreach (var userUpn in userUpdates.Keys.Where(u => !result.ContainsKey(u)))
                {
                    result[userUpn] = false;
                }

                return result;
            }
        }

        /// <summary>
        /// Archiwizuje zespół i dezaktywuje użytkowników, którzy są tylko w tym zespole
        /// Graph API Endpoints: POST /v1.0/teams/{team-id}/archive + PATCH /v1.0/users/{user-id}
        /// </summary>
        public async Task<Dictionary<string, bool>> ArchiveTeamAndDeactivateExclusiveUsersAsync(
            string teamId,
            IProgress<BulkOperationProgress>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));

            _logger.LogInformation("Rozpoczynam archiwizację zespołu {TeamId} z dezaktywacją ekskluzywnych użytkowników", 
                teamId);

            var result = new Dictionary<string, bool>();
            var progressReporter = new BulkOperationProgress
            {
                TotalOperations = 1, // Będzie aktualizowane po pobraniu członków
                CurrentOperation = "Pobieranie członków zespołu"
            };

            try
            {
                // Sprawdź token
                await _connectionService.EnsureValidTokenAsync();

                // 1. Pobierz członków zespołu
                var accessToken = await _connectionService.GetAccessTokenAsync();
                var headers = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {accessToken}"
                };
                
                var membersResponse = await _httpService.GetAsync(
                    $"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.TeamMembers(teamId)}",
                    headers);

                if (!membersResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Nie udało się pobrać członków zespołu {TeamId}: {StatusCode}", 
                        teamId, membersResponse.StatusCode);
                    result[teamId] = false;
                    return result;
                }

                var membersContent = await membersResponse.Content.ReadAsStringAsync();
                var membersData = JsonSerializer.Deserialize<JsonElement>(membersContent);
                
                var teamMembers = new List<string>();
                
                if (membersData.TryGetProperty("value", out var membersArray))
                {
                    foreach (var member in membersArray.EnumerateArray())
                    {
                        if (member.TryGetProperty("email", out var emailElement))
                        {
                            var email = emailElement.GetString();
                            if (!string.IsNullOrEmpty(email))
                                teamMembers.Add(email);
                        }
                        else if (member.TryGetProperty("userPrincipalName", out var upnElement))
                        {
                            var upn = upnElement.GetString();
                            if (!string.IsNullOrEmpty(upn))
                                teamMembers.Add(upn);
                        }
                    }
                }

                progressReporter.TotalOperations = 1 + teamMembers.Count; // Archiwizacja + dezaktywacja użytkowników
                progressReporter.CurrentOperation = "Sprawdzanie członkostwa użytkowników w innych zespołach";
                progress?.Report(progressReporter);

                // 2. Sprawdź dla każdego członka czy należy do innych zespołów
                var exclusiveUsers = new List<string>();
                
                foreach (var userUpn in teamMembers)
                {
                    try
                    {
                        var userTeamsResponse = await _httpService.GetAsync(
                            $"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.UserJoinedTeams(userUpn)}",
                            headers);

                        if (userTeamsResponse.IsSuccessStatusCode)
                        {
                            var userTeamsContent = await userTeamsResponse.Content.ReadAsStringAsync();
                            var userTeamsData = JsonSerializer.Deserialize<JsonElement>(userTeamsContent);
                            
                            var userTeamCount = 0;
                            if (userTeamsData.TryGetProperty("value", out var teamsArray))
                            {
                                userTeamCount = teamsArray.GetArrayLength();
                            }

                            // Jeśli użytkownik należy tylko do tego zespołu (lub do żadnego)
                            if (userTeamCount <= 1)
                            {
                                exclusiveUsers.Add(userUpn);
                                _logger.LogDebug("Użytkownik {UserUpn} będzie dezaktywowany (należy tylko do zespołu {TeamId})", 
                                    userUpn, teamId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Nie udało się sprawdzić członkostwa użytkownika {UserUpn}", userUpn);
                    }
                }

                // 3. Archiwizuj zespół
                progressReporter.CurrentOperation = "Archiwizacja zespołu";
                progress?.Report(progressReporter);

                var archiveResponse = await _httpService.PostAsync(
                    $"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.TeamArchive(teamId)}",
                    JsonSerializer.Serialize(new { shouldSetSpoSiteReadOnlyForMembers = true }),
                    headers);

                if (archiveResponse.IsSuccessStatusCode)
                {
                    result[teamId] = true;
                    progressReporter.SuccessfulOperations++;
                    _logger.LogInformation("Pomyślnie zarchiwizowano zespół {TeamId}", teamId);
                }
                else
                {
                    result[teamId] = false;
                    progressReporter.FailedOperations++;
                    _logger.LogError("Nie udało się zarchiwizować zespołu {TeamId}: {StatusCode}", 
                        teamId, archiveResponse.StatusCode);
                }

                progressReporter.CompletedOperations++;
                progress?.Report(progressReporter);

                // 4. Dezaktywuj ekskluzywnych użytkowników w batchach
                if (exclusiveUsers.Any())
                {
                    progressReporter.CurrentOperation = "Dezaktywacja ekskluzywnych użytkowników";
                    progress?.Report(progressReporter);

                    var deactivateOperations = exclusiveUsers.Select(userUpn =>
                        GraphBatchOperation.CreatePatch(
                            $"/v1.0/users/{userUpn}",
                            new { accountEnabled = false },
                            "DeactivateUser",
                            userUpn
                        )).ToList();

                    var deactivateBatches = CreateBatches(deactivateOperations, MaxBatchSize);

                    foreach (var batch in deactivateBatches)
                    {
                        var batchResult = await ExecuteBatchOperationsAsync(batch, 
                            await _connectionService.GetAccessTokenAsync());

                        foreach (var operation in batch)
                        {
                            var userUpn = operation.EntityId!;
                            var batchResponse = batchResult.BatchResults.FirstOrDefault(r => r.Id == operation.Id);
                            
                            if (batchResponse?.IsSuccessful == true)
                            {
                                result[userUpn] = true;
                                progressReporter.SuccessfulOperations++;
                                _logger.LogInformation("Pomyślnie dezaktywowano użytkownika {UserUpn}", userUpn);
                            }
                            else
                            {
                                result[userUpn] = false;
                                progressReporter.FailedOperations++;
                                _logger.LogWarning("Nie udało się dezaktywować użytkownika {UserUpn}: {Error}",
                                    userUpn, batchResponse?.ErrorMessage ?? "Nieznany błąd");
                            }

                            progressReporter.CompletedOperations++;
                        }

                        progress?.Report(progressReporter);
                    }
                }

                _logger.LogInformation("Zakończono archiwizację zespołu {TeamId} z dezaktywacją {Count} ekskluzywnych użytkowników. " +
                    "Sukces: {Success}/{Total}", teamId, exclusiveUsers.Count, 
                    progressReporter.SuccessfulOperations, progressReporter.TotalOperations);

                return result;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => ArchiveTeamAndDeactivateExclusiveUsersAsync(teamId, progress),
                    _logger,
                    "ArchiveTeamAndDeactivateExclusiveUsers",
                    defaultValue: result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas archiwizacji zespołu i dezaktywacji użytkowników {TeamId}", teamId);
                
                result["ArchiveTeam"] = false;
                return result;
            }
        }

        /// <summary>
        /// Synchronizuje członkostwo zespołu z docelową listą użytkowników (NOWA FUNKCJONALNOŚĆ)
        /// Graph API Endpoints: GET /v1.0/teams/{team-id}/members + batch operations for add/remove
        /// </summary>
        public async Task<GraphBulkResult> SynchronizeTeamMembershipAsync(
            string teamId,
            List<string> targetUserUpns,
            string defaultRole = "Member",
            IProgress<BulkOperationProgress>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));

            if (targetUserUpns == null)
                throw new ArgumentNullException(nameof(targetUserUpns));

            _logger.LogInformation("Rozpoczynam synchronizację członkostwa zespołu {TeamId} z {Count} docelowymi użytkownikami", 
                teamId, targetUserUpns.Count);

            var startTime = DateTime.UtcNow;
            var progressReporter = new BulkOperationProgress
            {
                TotalOperations = targetUserUpns.Count,
                CurrentOperation = "Pobieranie aktualnych członków zespołu"
            };

            try
            {
                // Sprawdź token
                await _connectionService.EnsureValidTokenAsync();

                // 1. Pobierz aktualnych członków zespołu
                var accessToken = await _connectionService.GetAccessTokenAsync();
                var headers = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {accessToken}"
                };
                
                var membersResponse = await _httpService.GetAsync(
                    $"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.TeamMembers(teamId)}",
                    headers);

                if (!membersResponse.IsSuccessStatusCode)
                {
                    var errorMessage = $"Nie udało się pobrać członków zespołu {teamId}: {membersResponse.StatusCode}";
                    _logger.LogError(errorMessage);
                    return GraphBulkResult.CreateError(errorMessage, "/v1.0/teams/{team-id}/members", "GET");
                }

                var membersContent = await membersResponse.Content.ReadAsStringAsync();
                var membersData = JsonSerializer.Deserialize<JsonElement>(membersContent);
                
                var currentMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var membershipMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                if (membersData.TryGetProperty("value", out var membersArray))
                {
                    foreach (var member in membersArray.EnumerateArray())
                    {
                        string? memberUpn = null;
                        string? membershipId = null;

                        if (member.TryGetProperty("email", out var emailElement))
                            memberUpn = emailElement.GetString();
                        else if (member.TryGetProperty("userPrincipalName", out var upnElement))
                            memberUpn = upnElement.GetString();

                        if (member.TryGetProperty("id", out var idElement))
                            membershipId = idElement.GetString();

                        if (!string.IsNullOrEmpty(memberUpn))
                        {
                            currentMembers.Add(memberUpn);
                            if (!string.IsNullOrEmpty(membershipId))
                                membershipMap[memberUpn] = membershipId;
                        }
                    }
                }

                progressReporter.CurrentOperation = "Analizowanie różnic w członkostwie";
                progress?.Report(progressReporter);

                // 2. Określ użytkowników do dodania i usunięcia
                var targetMembersSet = new HashSet<string>(targetUserUpns, StringComparer.OrdinalIgnoreCase);
                var usersToAdd = targetMembersSet.Except(currentMembers).ToList();
                var usersToRemove = currentMembers.Except(targetMembersSet).ToList();

                _logger.LogInformation("Synchronizacja zespołu {TeamId}: {AddCount} do dodania, {RemoveCount} do usunięcia", 
                    teamId, usersToAdd.Count, usersToRemove.Count);

                var allOperations = new List<GraphBatchOperation>();
                var allResults = new List<GraphBatchOperationResult>();

                // 3. Przygotuj operacje dodawania
                if (usersToAdd.Any())
                {
                    progressReporter.CurrentOperation = "Dodawanie nowych członków";
                    progress?.Report(progressReporter);

                    var addOperations = usersToAdd.Select(userUpn =>
                        GraphBatchOperation.CreatePost(
                            $"/v1.0/teams/{teamId}/members",
                            new
                            {
                                roles = new[] { defaultRole },
                                user = new { userPrincipalName = userUpn }
                            },
                            "AddTeamMember",
                            userUpn
                        )).ToList();

                    allOperations.AddRange(addOperations);

                    var addBatches = CreateBatches(addOperations, MaxBatchSize);
                    foreach (var batch in addBatches)
                    {
                        var batchResult = await ExecuteBatchOperationsAsync(batch, 
                            await _connectionService.GetAccessTokenAsync());
                        
                        if (batchResult.Success)
                            allResults.AddRange(batchResult.BatchResults);
                    }
                }

                // 4. Przygotuj operacje usuwania
                if (usersToRemove.Any())
                {
                    progressReporter.CurrentOperation = "Usuwanie niepotrzebnych członków";
                    progress?.Report(progressReporter);

                    var removeOperations = new List<GraphBatchOperation>();
                    
                    foreach (var userUpn in usersToRemove)
                    {
                        if (membershipMap.TryGetValue(userUpn, out var membershipId))
                        {
                            removeOperations.Add(GraphBatchOperation.CreateDelete(
                                $"/v1.0/teams/{teamId}/members/{membershipId}",
                                "RemoveTeamMember",
                                userUpn
                            ));
                        }
                    }

                    allOperations.AddRange(removeOperations);

                    var removeBatches = CreateBatches(removeOperations, MaxBatchSize);
                    foreach (var batch in removeBatches)
                    {
                        var batchResult = await ExecuteBatchOperationsAsync(batch, 
                            await _connectionService.GetAccessTokenAsync());
                        
                        if (batchResult.Success)
                            allResults.AddRange(batchResult.BatchResults);
                    }
                }

                var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                var finalResult = GraphBulkResult.CreateSuccess("/$batch", "POST", executionTime);
                finalResult.BatchResults = allResults;
                finalResult.BatchId = Guid.NewGuid().ToString();

                // Dodaj statystyki synchronizacji
                var successfulAdds = allResults.Count(r => r.IsSuccessful && 
                    allOperations.Any(op => op.Id == r.Id && op.OperationType == "AddTeamMember"));
                var successfulRemoves = allResults.Count(r => r.IsSuccessful && 
                    allOperations.Any(op => op.Id == r.Id && op.OperationType == "RemoveTeamMember"));

                finalResult.AddMetadata("TeamId", teamId);
                finalResult.AddMetadata("TargetMemberCount", targetUserUpns.Count);
                finalResult.AddMetadata("CurrentMemberCount", currentMembers.Count);
                finalResult.AddMetadata("UsersToAdd", usersToAdd.Count);
                finalResult.AddMetadata("UsersToRemove", usersToRemove.Count);
                finalResult.AddMetadata("SuccessfulAdds", successfulAdds);
                finalResult.AddMetadata("SuccessfulRemoves", successfulRemoves);
                finalResult.AddMetadata("TotalOperations", allOperations.Count);

                _logger.LogInformation("Zakończono synchronizację członkostwa zespołu {TeamId}. " +
                    "Dodano: {AddedCount}/{ToAddCount}, Usunięto: {RemovedCount}/{ToRemoveCount} w {ExecutionTime}ms", 
                    teamId, successfulAdds, usersToAdd.Count, successfulRemoves, usersToRemove.Count, executionTime);

                return finalResult;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex,
                    () => SynchronizeTeamMembershipAsync(teamId, targetUserUpns, defaultRole, progress),
                    _logger,
                    "SynchronizeTeamMembership",
                    defaultValue: GraphBulkResult.CreateError("GraphConnectionException", "/v1.0/teams/{team-id}/members", "SYNC"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas synchronizacji członkostwa zespołu {TeamId}", teamId);
                return GraphBulkResult.CreateError(ex.Message, "/v1.0/teams/{team-id}/members", "SYNC");
            }
        }

        #endregion

        #region Enhanced V2 Methods

        /// <summary>
        /// Masowo dodaje użytkowników do zespołu z zaawansowanym raportowaniem Graph API
        /// Graph API Endpoint: POST /v1.0/$batch z szczegółowym rate limiting
        /// </summary>
        public async Task<Dictionary<string, GraphBulkResult>> BulkAddUsersToTeamV2Async(
            string teamId,
            List<string> userUpns,
            string role = "Member",
            IProgress<BulkOperationProgress>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));

            if (userUpns == null || !userUpns.Any())
                throw new ArgumentException("Lista użytkowników nie może być pusta", nameof(userUpns));

            _logger.LogInformation("Rozpoczynam masowe dodawanie V2 {Count} użytkowników do zespołu {TeamId}", 
                userUpns.Count, teamId);

            var result = new Dictionary<string, GraphBulkResult>();
            var progressReporter = new BulkOperationProgress
            {
                TotalOperations = userUpns.Count,
                CurrentOperation = "Przygotowywanie operacji batch z retry logic"
            };

            try
            {
                // Sprawdź token
                await _connectionService.EnsureValidTokenAsync();

                // Podziel na batche
                var batches = CreateBatches(userUpns, MaxBatchSize);
                var batchIndex = 0;

                foreach (var batch in batches)
                {
                    batchIndex++;
                    progressReporter.CurrentOperation = $"Przetwarzanie batch {batchIndex}/{batches.Count} z retry logic";
                    progress?.Report(progressReporter);

                    var batchOperations = batch.Select(userUpn => 
                        GraphBatchOperation.CreatePost(
                            $"/v1.0/teams/{teamId}/members",
                            new
                            {
                                roles = new[] { role },
                                user = new { userPrincipalName = userUpn }
                            },
                            "AddTeamMember",
                            userUpn
                        )).ToList();

                    // Wykonaj batch z retry logic
                    var batchResult = await ExecuteBatchWithRetryAsync(batchOperations, 
                        await _connectionService.GetAccessTokenAsync());

                    // Przetwórz wyniki batch z szczegółowym raportowaniem
                    foreach (var operation in batchOperations)
                    {
                        var userUpn = operation.EntityId!;
                        var batchResponse = batchResult.BatchResults.FirstOrDefault(r => r.Id == operation.Id);
                        
                        var userResult = GraphBulkResult.CreateSuccess($"/v1.0/teams/{teamId}/members", "POST");
                        userResult.BatchId = batchResult.BatchId;
                        userResult.WasRetried = batchResult.WasRetried;
                        userResult.RetryCount = batchResult.RetryCount;
                        userResult.RateLimitInfo = batchResult.RateLimitInfo;

                        if (batchResponse?.IsSuccessful == true)
                        {
                            userResult.Success = true;
                            userResult.HttpStatusCode = (HttpStatusCode?)batchResponse.Status;
                            userResult.AddSuccess(new GraphBulkOperationSuccess
                            {
                                Operation = "AddTeamMember",
                                EntityId = userUpn,
                                EntityName = userUpn,
                                Message = "Pomyślnie dodano użytkownika do zespołu",
                                GraphEndpoint = $"/v1.0/teams/{teamId}/members",
                                HttpMethod = "POST",
                                HttpStatusCode = (HttpStatusCode?)batchResponse.Status
                            });

                            progressReporter.SuccessfulOperations++;
                        }
                        else
                        {
                            userResult.Success = false;
                            userResult.ErrorMessage = batchResponse?.ErrorMessage ?? "Nieznany błąd";
                            userResult.HttpStatusCode = (HttpStatusCode?)batchResponse?.Status;
                            userResult.AddError(new GraphBulkOperationError
                            {
                                Operation = "AddTeamMember",
                                EntityId = userUpn,
                                EntityName = userUpn,
                                Message = batchResponse?.ErrorMessage ?? "Nieznany błąd",
                                ErrorCode = batchResponse?.ErrorCode,
                                GraphEndpoint = $"/v1.0/teams/{teamId}/members",
                                HttpMethod = "POST",
                                HttpStatusCode = (HttpStatusCode?)batchResponse?.Status
                            });

                            progressReporter.FailedOperations++;
                            _logger.LogWarning("Nie udało się dodać użytkownika {UserUpn} do zespołu {TeamId}: {Error}",
                                userUpn, teamId, batchResponse?.ErrorMessage ?? "Nieznany błąd");
                        }

                        result[userUpn] = userResult;
                        progressReporter.CompletedOperations++;
                    }

                    progress?.Report(progressReporter);
                }

                _logger.LogInformation("Zakończono masowe dodawanie V2 użytkowników do zespołu {TeamId}. " +
                    "Sukces: {Success}/{Total}", teamId, progressReporter.SuccessfulOperations, userUpns.Count);

                return result;
            }
            catch (GraphConnectionException ex)
            {
                _logger.LogError(ex, "GraphConnectionException podczas masowego dodawania V2 użytkowników do zespołu {TeamId}", teamId);
                
                // Zwróć błąd dla wszystkich użytkowników, którzy nie zostali jeszcze przetworzeni
                foreach (var userUpn in userUpns.Where(u => !result.ContainsKey(u)))
                {
                    result[userUpn] = await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex,
                        () => Task.FromResult(GraphBulkResult.CreateError(ex.Message, $"/v1.0/teams/{teamId}/members", "POST")),
                        _logger,
                        "BulkAddUsersToTeamV2",
                        defaultValue: GraphBulkResult.CreateError(ex.Message, $"/v1.0/teams/{teamId}/members", "POST"));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas masowego dodawania V2 użytkowników do zespołu {TeamId}", teamId);
                
                // Zwróć błąd dla wszystkich użytkowników, którzy nie zostali jeszcze przetworzeni
                foreach (var userUpn in userUpns.Where(u => !result.ContainsKey(u)))
                {
                    result[userUpn] = GraphBulkResult.CreateError(ex.Message, 
                        $"/v1.0/teams/{teamId}/members", "POST");
                }

                return result;
            }
        }

        /// <summary>
        /// Masowo usuwa użytkowników z zespołu z zaawansowanym raportowaniem Graph API
        /// Graph API Endpoint: POST /v1.0/$batch z szczegółowym rate limiting
        /// </summary>
        public async Task<Dictionary<string, GraphBulkResult>> BulkRemoveUsersFromTeamV2Async(
            string teamId,
            List<string> userUpns,
            IProgress<BulkOperationProgress>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));

            if (userUpns == null || !userUpns.Any())
                throw new ArgumentException("Lista użytkowników nie może być pusta", nameof(userUpns));

            _logger.LogInformation("Rozpoczynam masowe usuwanie V2 {Count} użytkowników z zespołu {TeamId}", 
                userUpns.Count, teamId);

            var result = new Dictionary<string, GraphBulkResult>();
            var progressReporter = new BulkOperationProgress
            {
                TotalOperations = userUpns.Count,
                CurrentOperation = "Pobieranie członków zespołu"
            };

            try
            {
                // Sprawdź token
                await _connectionService.EnsureValidTokenAsync();

                // Najpierw pobierz wszystkich członków zespołu aby uzyskać membership-id
                var accessToken = await _connectionService.GetAccessTokenAsync();
                var headers = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {accessToken}"
                };
                
                var membersResponse = await _httpService.GetAsync(
                    $"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.TeamMembers(teamId)}",
                    headers);

                if (!membersResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Nie udało się pobrać członków zespołu {TeamId}: {StatusCode}", 
                        teamId, membersResponse.StatusCode);
                    
                    foreach (var userUpn in userUpns)
                    {
                        result[userUpn] = GraphBulkResult.CreateError(
                            $"Nie udało się pobrać członków zespołu: {membersResponse.StatusCode}",
                            $"/v1.0/teams/{teamId}/members", "GET", membersResponse.StatusCode);
                    }
                    
                    return result;
                }

                var membersContent = await membersResponse.Content.ReadAsStringAsync();
                var membersData = JsonSerializer.Deserialize<JsonElement>(membersContent);
                
                // Mapuj UPN na membership-id
                var membershipMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                if (membersData.TryGetProperty("value", out var membersArray))
                {
                    foreach (var member in membersArray.EnumerateArray())
                    {
                        string? memberUpn = null;
                        string? membershipId = null;

                        if (member.TryGetProperty("email", out var emailElement))
                            memberUpn = emailElement.GetString();
                        else if (member.TryGetProperty("userPrincipalName", out var upnElement))
                            memberUpn = upnElement.GetString();

                        if (member.TryGetProperty("id", out var idElement))
                            membershipId = idElement.GetString();

                        if (!string.IsNullOrEmpty(memberUpn) && !string.IsNullOrEmpty(membershipId))
                        {
                            membershipMap[memberUpn] = membershipId;
                        }
                    }
                }

                progressReporter.CurrentOperation = "Przygotowywanie operacji batch z retry logic";
                progress?.Report(progressReporter);

                // Podziel na batche
                var validUsers = userUpns.Where(upn => membershipMap.ContainsKey(upn)).ToList();
                var invalidUsers = userUpns.Except(validUsers).ToList();

                // Oznacz nieprawidłowych użytkowników jako błąd
                foreach (var userUpn in invalidUsers)
                {
                    result[userUpn] = GraphBulkResult.CreateError(
                        "Użytkownik nie jest członkiem zespołu",
                        $"/v1.0/teams/{teamId}/members", "DELETE", HttpStatusCode.NotFound);
                    progressReporter.FailedOperations++;
                    progressReporter.CompletedOperations++;
                }

                if (validUsers.Any())
                {
                    var batches = CreateBatches(validUsers, MaxBatchSize);
                    var batchIndex = 0;

                    foreach (var batch in batches)
                    {
                        batchIndex++;
                        progressReporter.CurrentOperation = $"Przetwarzanie batch {batchIndex}/{batches.Count} z retry logic";
                        progress?.Report(progressReporter);

                        var batchOperations = batch.Select(userUpn =>
                        {
                            var membershipId = membershipMap[userUpn];
                            return GraphBatchOperation.CreateDelete(
                                $"/v1.0/teams/{teamId}/members/{membershipId}",
                                "RemoveTeamMember",
                                userUpn
                            );
                        }).ToList();

                        // Wykonaj batch z retry logic
                        var batchResult = await ExecuteBatchWithRetryAsync(batchOperations, 
                            await _connectionService.GetAccessTokenAsync());

                        // Przetwórz wyniki batch z szczegółowym raportowaniem
                        foreach (var operation in batchOperations)
                        {
                            var userUpn = operation.EntityId!;
                            var batchResponse = batchResult.BatchResults.FirstOrDefault(r => r.Id == operation.Id);
                            
                            var userResult = GraphBulkResult.CreateSuccess($"/v1.0/teams/{teamId}/members/{membershipMap[userUpn]}", "DELETE");
                            userResult.BatchId = batchResult.BatchId;
                            userResult.WasRetried = batchResult.WasRetried;
                            userResult.RetryCount = batchResult.RetryCount;
                            userResult.RateLimitInfo = batchResult.RateLimitInfo;

                            if (batchResponse?.IsSuccessful == true)
                            {
                                userResult.Success = true;
                                userResult.HttpStatusCode = (HttpStatusCode?)batchResponse.Status;
                                userResult.AddSuccess(new GraphBulkOperationSuccess
                                {
                                    Operation = "RemoveTeamMember",
                                    EntityId = userUpn,
                                    EntityName = userUpn,
                                    Message = "Pomyślnie usunięto użytkownika z zespołu",
                                    GraphEndpoint = $"/v1.0/teams/{teamId}/members/{membershipMap[userUpn]}",
                                    HttpMethod = "DELETE",
                                    HttpStatusCode = (HttpStatusCode?)batchResponse.Status
                                });

                                progressReporter.SuccessfulOperations++;
                            }
                            else
                            {
                                userResult.Success = false;
                                userResult.ErrorMessage = batchResponse?.ErrorMessage ?? "Nieznany błąd";
                                userResult.HttpStatusCode = (HttpStatusCode?)batchResponse?.Status;
                                userResult.AddError(new GraphBulkOperationError
                                {
                                    Operation = "RemoveTeamMember",
                                    EntityId = userUpn,
                                    EntityName = userUpn,
                                    Message = batchResponse?.ErrorMessage ?? "Nieznany błąd",
                                    ErrorCode = batchResponse?.ErrorCode,
                                    GraphEndpoint = $"/v1.0/teams/{teamId}/members/{membershipMap[userUpn]}",
                                    HttpMethod = "DELETE",
                                    HttpStatusCode = (HttpStatusCode?)batchResponse?.Status
                                });

                                progressReporter.FailedOperations++;
                                _logger.LogWarning("Nie udało się usunąć użytkownika {UserUpn} z zespołu {TeamId}: {Error}",
                                    userUpn, teamId, batchResponse?.ErrorMessage ?? "Nieznany błąd");
                            }

                            result[userUpn] = userResult;
                            progressReporter.CompletedOperations++;
                        }

                        progress?.Report(progressReporter);
                    }
                }

                _logger.LogInformation("Zakończono masowe usuwanie V2 użytkowników z zespołu {TeamId}. " +
                    "Sukces: {Success}/{Total}", teamId, progressReporter.SuccessfulOperations, userUpns.Count);

                return result;
            }
            catch (GraphConnectionException ex)
            {
                _logger.LogError(ex, "GraphConnectionException podczas masowego usuwania V2 użytkowników z zespołu {TeamId}", teamId);
                
                // Zwróć błąd dla wszystkich użytkowników, którzy nie zostali jeszcze przetworzeni
                foreach (var userUpn in userUpns.Where(u => !result.ContainsKey(u)))
                {
                    result[userUpn] = await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex,
                        () => Task.FromResult(GraphBulkResult.CreateError(ex.Message, $"/v1.0/teams/{teamId}/members", "DELETE")),
                        _logger,
                        "BulkRemoveUsersFromTeamV2",
                        defaultValue: GraphBulkResult.CreateError(ex.Message, $"/v1.0/teams/{teamId}/members", "DELETE"));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas masowego usuwania V2 użytkowników z zespołu {TeamId}", teamId);
                
                // Zwróć błąd dla wszystkich użytkowników, którzy nie zostali jeszcze przetworzeni
                foreach (var userUpn in userUpns.Where(u => !result.ContainsKey(u)))
                {
                    result[userUpn] = GraphBulkResult.CreateError(ex.Message, 
                        $"/v1.0/teams/{teamId}/members", "DELETE");
                }

                return result;
            }
        }

        /// <summary>
        /// Masowo archiwizuje zespoły z zaawansowanym raportowaniem Graph API
        /// Graph API Endpoint: POST /v1.0/$batch z szczegółowym rate limiting
        /// </summary>
        public async Task<Dictionary<string, GraphBulkResult>> BulkArchiveTeamsV2Async(
            List<string> teamIds,
            IProgress<BulkOperationProgress>? progress = null)
        {
            if (teamIds == null || !teamIds.Any())
                throw new ArgumentException("Lista zespołów nie może być pusta", nameof(teamIds));

            _logger.LogInformation("Rozpoczynam masową archiwizację V2 {Count} zespołów", teamIds.Count);

            var result = new Dictionary<string, GraphBulkResult>();
            var progressReporter = new BulkOperationProgress
            {
                TotalOperations = teamIds.Count,
                CurrentOperation = "Przygotowywanie operacji batch z retry logic"
            };

            try
            {
                // Sprawdź token
                await _connectionService.EnsureValidTokenAsync();

                // Podziel na batche
                var batches = CreateBatches(teamIds, MaxBatchSize);
                var batchIndex = 0;

                foreach (var batch in batches)
                {
                    batchIndex++;
                    progressReporter.CurrentOperation = $"Przetwarzanie batch {batchIndex}/{batches.Count} z retry logic";
                    progress?.Report(progressReporter);

                    var batchOperations = batch.Select(teamId => 
                        GraphBatchOperation.CreatePost(
                            $"/v1.0/teams/{teamId}/archive",
                            new { shouldSetSpoSiteReadOnlyForMembers = true },
                            "ArchiveTeam",
                            teamId
                        )).ToList();

                    // Wykonaj batch z retry logic
                    var batchResult = await ExecuteBatchWithRetryAsync(batchOperations, 
                        await _connectionService.GetAccessTokenAsync());

                    // Przetwórz wyniki batch z szczegółowym raportowaniem
                    foreach (var operation in batchOperations)
                    {
                        var teamId = operation.EntityId!;
                        var batchResponse = batchResult.BatchResults.FirstOrDefault(r => r.Id == operation.Id);
                        
                        var teamResult = GraphBulkResult.CreateSuccess($"/v1.0/teams/{teamId}/archive", "POST");
                        teamResult.BatchId = batchResult.BatchId;
                        teamResult.WasRetried = batchResult.WasRetried;
                        teamResult.RetryCount = batchResult.RetryCount;
                        teamResult.RateLimitInfo = batchResult.RateLimitInfo;

                        if (batchResponse?.IsSuccessful == true)
                        {
                            teamResult.Success = true;
                            teamResult.HttpStatusCode = (HttpStatusCode?)batchResponse.Status;
                            teamResult.AddSuccess(new GraphBulkOperationSuccess
                            {
                                Operation = "ArchiveTeam",
                                EntityId = teamId,
                                EntityName = teamId,
                                Message = "Pomyślnie zarchiwizowano zespół",
                                GraphEndpoint = $"/v1.0/teams/{teamId}/archive",
                                HttpMethod = "POST",
                                HttpStatusCode = (HttpStatusCode?)batchResponse.Status
                            });

                            progressReporter.SuccessfulOperations++;
                        }
                        else
                        {
                            teamResult.Success = false;
                            teamResult.ErrorMessage = batchResponse?.ErrorMessage ?? "Nieznany błąd";
                            teamResult.HttpStatusCode = (HttpStatusCode?)batchResponse?.Status;
                            teamResult.AddError(new GraphBulkOperationError
                            {
                                Operation = "ArchiveTeam",
                                EntityId = teamId,
                                EntityName = teamId,
                                Message = batchResponse?.ErrorMessage ?? "Nieznany błąd",
                                ErrorCode = batchResponse?.ErrorCode,
                                GraphEndpoint = $"/v1.0/teams/{teamId}/archive",
                                HttpMethod = "POST",
                                HttpStatusCode = (HttpStatusCode?)batchResponse?.Status
                            });

                            progressReporter.FailedOperations++;
                            _logger.LogWarning("Nie udało się zarchiwizować zespołu {TeamId}: {Error}",
                                teamId, batchResponse?.ErrorMessage ?? "Nieznany błąd");
                        }

                        result[teamId] = teamResult;
                        progressReporter.CompletedOperations++;
                    }

                    progress?.Report(progressReporter);
                }

                _logger.LogInformation("Zakończono masową archiwizację V2 zespołów. " +
                    "Sukces: {Success}/{Total}", progressReporter.SuccessfulOperations, teamIds.Count);

                return result;
            }
            catch (GraphConnectionException ex)
            {
                _logger.LogError(ex, "GraphConnectionException podczas masowej archiwizacji V2 zespołów");
                
                // Zwróć błąd dla wszystkich zespołów, które nie zostały jeszcze przetworzone
                foreach (var teamId in teamIds.Where(t => !result.ContainsKey(t)))
                {
                    result[teamId] = await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex,
                        () => Task.FromResult(GraphBulkResult.CreateError(ex.Message, $"/v1.0/teams/{teamId}/archive", "POST")),
                        _logger,
                        "BulkArchiveTeamsV2",
                        defaultValue: GraphBulkResult.CreateError(ex.Message, $"/v1.0/teams/{teamId}/archive", "POST"));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas masowej archiwizacji V2 zespołów");
                
                // Zwróć błąd dla wszystkich zespołów, które nie zostały jeszcze przetworzone
                foreach (var teamId in teamIds.Where(t => !result.ContainsKey(t)))
                {
                    result[teamId] = GraphBulkResult.CreateError(ex.Message, 
                        $"/v1.0/teams/{teamId}/archive", "POST");
                }

                return result;
            }
        }

        #endregion

        #region Rate Limiting & Batch Management

        /// <summary>
        /// Sprawdza aktualny stan rate limiting dla Graph API
        /// Graph API Headers: X-RateLimit-Remaining, Retry-After
        /// </summary>
        public async Task<GraphRateLimitStatus> GetRateLimitStatusAsync()
        {
            _logger.LogDebug("Sprawdzam status rate limiting Graph API");

            try
            {
                // Sprawdź token
                await _connectionService.EnsureValidTokenAsync();

                // Wykonaj lekkie żądanie do Graph API aby sprawdzić nagłówki rate limiting
                var accessToken = await _connectionService.GetAccessTokenAsync();
                var headers = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {accessToken}"
                };
                var response = await _httpService.GetAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.Me}", headers);

                var rateLimitStatus = new GraphRateLimitStatus();

                if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingHeaders))
                {
                    var remainingHeader = remainingHeaders.FirstOrDefault();
                    if (remainingHeader != null && int.TryParse(remainingHeader, out var remaining))
                        rateLimitStatus.RemainingRequests = remaining;
                }

                if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetHeaders))
                {
                    var resetHeader = resetHeaders.FirstOrDefault();
                    if (resetHeader != null && long.TryParse(resetHeader, out var resetTimestamp))
                        rateLimitStatus.ResetTime = DateTimeOffset.FromUnixTimeSeconds(resetTimestamp).DateTime;
                }

                rateLimitStatus.IsLimitReached = response.StatusCode == HttpStatusCode.TooManyRequests;
                rateLimitStatus.LimitType = "Graph API";

                _logger.LogDebug("Status rate limiting: Pozostało {Remaining}/{Max} żądań", 
                    rateLimitStatus.RemainingRequests, rateLimitStatus.MaxRequests);

                return rateLimitStatus;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Błąd podczas sprawdzania statusu rate limiting", ex),
                    () => GetRateLimitStatusAsync(),
                    _logger,
                    "GetRateLimitStatus",
                    defaultValue: new GraphRateLimitStatus { IsLimitReached = false });
            }
        }

        /// <summary>
        /// Wykonuje operację batch z automatycznym rate limiting
        /// Graph API Endpoint: POST /v1.0/$batch
        /// </summary>
        public async Task<GraphBulkResult> ExecuteBatchOperationsAsync(
            List<GraphBatchOperation> batchOperations,
            string accessToken,
            bool respectRateLimit = true)
        {
            if (batchOperations == null)
                throw new ArgumentNullException(nameof(batchOperations));

            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("Token dostępu nie może być pusty", nameof(accessToken));

            // Dla pustej listy zwróć pusty wynik (zgodnie z best practices)
            if (!batchOperations.Any())
            {
                _logger.LogDebug("Otrzymano pustą listę operacji batch - zwracam pusty wynik");
                var emptyResult = GraphBulkResult.CreateSuccess("/$batch", "POST", 0);
                emptyResult.BatchId = Guid.NewGuid().ToString();
                return emptyResult;
            }

            // Jeśli batch jest większy niż maksymalny rozmiar, podziel na mniejsze batche
            if (batchOperations.Count > MaxBatchSize)
            {
                _logger.LogDebug("Dzielę duży batch {Count} operacji na mniejsze batche (max {MaxSize})", 
                    batchOperations.Count, MaxBatchSize);
                
                var chunkingStartTime = DateTime.UtcNow;
                var allResults = new List<GraphBatchOperationResult>();
                var batches = CreateBatches(batchOperations, MaxBatchSize);
                
                foreach (var batch in batches)
                {
                    var batchResult = await ExecuteBatchOperationsAsync(batch, accessToken, respectRateLimit);
                    allResults.AddRange(batchResult.BatchResults);
                }
                
                var executionTime = (long)(DateTime.UtcNow - chunkingStartTime).TotalMilliseconds;
                var finalResult = GraphBulkResult.CreateSuccess("/$batch", "POST", executionTime);
                finalResult.BatchId = Guid.NewGuid().ToString();
                finalResult.BatchResults.AddRange(allResults);
                
                return finalResult;
            }

            _logger.LogDebug("Wykonuję batch z {Count} operacjami", batchOperations.Count);

            var startTime = DateTime.UtcNow;

            try
            {
                // Sprawdź rate limiting jeśli wymagane
                if (respectRateLimit)
                {
                    var rateLimitStatus = await GetRateLimitStatusAsync();
                    if (rateLimitStatus.IsLimitReached && rateLimitStatus.RetryAfterSeconds.HasValue)
                    {
                        var delayMs = Math.Min(rateLimitStatus.RetryAfterSeconds.Value * 1000, MaxRetryDelayMs);
                        _logger.LogWarning("Rate limit osiągnięty, czekam {DelayMs}ms", delayMs);
                        await Task.Delay(delayMs);
                    }
                }

                // Przygotuj żądanie batch
                var batchRequest = new
                {
                    requests = batchOperations.Select(op => new
                    {
                        id = op.Id,
                        method = op.Method,
                        url = op.Url,
                        headers = op.Headers.Any() ? op.Headers : null,
                        body = op.Body
                    }).ToArray()
                };

                var json = JsonSerializer.Serialize(batchRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // Wykonaj żądanie batch
                var headers = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {accessToken}",
                    ["Content-Type"] = "application/json"
                };
                var response = await _httpService.PostAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.Batch}", 
                    json, headers);

                var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var batchResponse = JsonSerializer.Deserialize<GraphBatchResponse>(responseContent, 
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                    var result = GraphBulkResult.CreateSuccess("/$batch", "POST", executionTime);
                    result.BatchId = Guid.NewGuid().ToString();

                    if (batchResponse?.Responses != null)
                    {
                        foreach (var batchResponseItem in batchResponse.Responses)
                        {
                            result.BatchResults.Add(new GraphBatchOperationResult
                            {
                                Id = batchResponseItem.Id,
                                Status = batchResponseItem.Status,
                                Headers = batchResponseItem.Headers,
                                Body = batchResponseItem.Body
                            });
                        }
                    }

                    _logger.LogDebug("Batch wykonany pomyślnie w {ExecutionTime}ms", executionTime);
                    return result;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = $"Błąd batch API: {response.StatusCode} - {errorContent}";
                    
                    _logger.LogError("Błąd podczas wykonywania batch: {Error}", errorMessage);
                    return GraphBulkResult.CreateError(errorMessage, "/$batch", "POST", 
                        response.StatusCode, executionTime);
                }
            }
            catch (GraphConnectionException ex)
            {
                var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex,
                    () => ExecuteBatchOperationsAsync(batchOperations, accessToken, respectRateLimit),
                    _logger,
                    "ExecuteBatchOperations",
                    defaultValue: GraphBulkResult.CreateError(ex.Message, "/$batch", "POST", null, executionTime));
            }
            catch (Exception ex)
            {
                var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, "Wyjątek podczas wykonywania batch");
                return GraphBulkResult.CreateError(ex.Message, "/$batch", "POST", 
                    null, executionTime);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Dzieli listę na batche o określonym rozmiarze
        /// </summary>
        private List<List<T>> CreateBatches<T>(List<T> items, int batchSize)
        {
            var batches = new List<List<T>>();
            
            for (int i = 0; i < items.Count; i += batchSize)
            {
                var batch = items.Skip(i).Take(batchSize).ToList();
                batches.Add(batch);
            }

            return batches;
        }

        /// <summary>
        /// Oblicza opóźnienie dla retry z exponential backoff
        /// </summary>
        private int CalculateRetryDelay(int retryCount)
        {
            var delay = DefaultRetryDelayMs * Math.Pow(2, retryCount);
            return Math.Min((int)delay, MaxRetryDelayMs);
        }

        /// <summary>
        /// Przetwarza batch aktualizacji z parallel processing i rate limiting
        /// </summary>
        private async Task ProcessUpdateBatchAsync(
            List<GraphBatchOperation> batch,
            Dictionary<string, bool> result,
            BulkOperationProgress progressReporter,
            IProgress<BulkOperationProgress>? progress,
            SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            
            try
            {
                // Sprawdź rate limiting przed wykonaniem batch
                var rateLimitStatus = await GetRateLimitStatusAsync();
                if (rateLimitStatus.IsLimitReached && rateLimitStatus.RetryAfterSeconds.HasValue)
                {
                    var delayMs = Math.Min(rateLimitStatus.RetryAfterSeconds.Value * 1000, MaxRetryDelayMs);
                    _logger.LogWarning("Rate limit osiągnięty w parallel batch, czekam {DelayMs}ms", delayMs);
                    await Task.Delay(delayMs);
                }

                var batchResult = await ExecuteBatchOperationsAsync(batch, 
                    await _connectionService.GetAccessTokenAsync());

                lock (result)
                {
                    foreach (var operation in batch)
                    {
                        var userUpn = operation.EntityId!;
                        var batchResponse = batchResult.BatchResults.FirstOrDefault(r => r.Id == operation.Id);
                        
                        if (batchResponse?.IsSuccessful == true)
                        {
                            result[userUpn] = true;
                            progressReporter.SuccessfulOperations++;
                        }
                        else
                        {
                            result[userUpn] = false;
                            progressReporter.FailedOperations++;
                            _logger.LogWarning("Nie udało się zaktualizować właściwości użytkownika {UserUpn}: {Error}",
                                userUpn, batchResponse?.ErrorMessage ?? "Nieznany błąd");
                        }

                        progressReporter.CompletedOperations++;
                    }

                    progress?.Report(progressReporter);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas przetwarzania batch aktualizacji");
                
                lock (result)
                {
                    foreach (var operation in batch)
                    {
                        var userUpn = operation.EntityId!;
                        if (!result.ContainsKey(userUpn))
                        {
                            result[userUpn] = false;
                            progressReporter.FailedOperations++;
                            progressReporter.CompletedOperations++;
                        }
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Wykonuje batch operacji z zaawansowanym retry logic
        /// </summary>
        private async Task<GraphBulkResult> ExecuteBatchWithRetryAsync(
            List<GraphBatchOperation> batchOperations,
            string accessToken)
        {
            var maxRetries = 3;
            var retryCount = 0;
            GraphBulkResult? lastResult = null;

            while (retryCount <= maxRetries)
            {
                try
                {
                    var result = await ExecuteBatchOperationsAsync(batchOperations, accessToken, true);
                    
                    // Sprawdź czy wszystkie operacje się udały
                    var allSuccessful = result.BatchResults.All(r => r.IsSuccessful);
                    
                    if (allSuccessful || retryCount >= maxRetries)
                    {
                        result.WasRetried = retryCount > 0;
                        result.RetryCount = retryCount;
                        return result;
                    }

                    // Sprawdź które operacje się nie udały i czy można je powtórzyć
                    var failedOperations = new List<GraphBatchOperation>();
                    
                    foreach (var operation in batchOperations)
                    {
                        var batchResponse = result.BatchResults.FirstOrDefault(r => r.Id == operation.Id);
                        
                        if (batchResponse?.HasError == true)
                        {
                            // Sprawdź czy błąd można powtórzyć
                            var canRetry = batchResponse.Status == 429 || // Too Many Requests
                                          batchResponse.Status == 500 || // Internal Server Error
                                          batchResponse.Status == 502 || // Bad Gateway
                                          batchResponse.Status == 503 || // Service Unavailable
                                          batchResponse.Status == 504;   // Gateway Timeout

                            if (canRetry && retryCount < operation.MaxRetries)
                            {
                                failedOperations.Add(operation);
                            }
                        }
                    }

                    if (!failedOperations.Any())
                    {
                        // Brak operacji do powtórzenia
                        result.WasRetried = retryCount > 0;
                        result.RetryCount = retryCount;
                        return result;
                    }

                    lastResult = result;
                    batchOperations = failedOperations;
                    retryCount++;

                    // Oblicz opóźnienie z exponential backoff
                    var delayMs = CalculateRetryDelay(retryCount);
                    
                    _logger.LogWarning("Powtarzam {FailedCount} nieudanych operacji batch (próba {RetryCount}/{MaxRetries}), " +
                        "czekam {DelayMs}ms", failedOperations.Count, retryCount, maxRetries, delayMs);
                    
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas próby {RetryCount} wykonania batch", retryCount);
                    
                    if (retryCount >= maxRetries)
                    {
                        return GraphBulkResult.CreateError(ex.Message, "/$batch", "POST");
                    }

                    retryCount++;
                    var delayMs = CalculateRetryDelay(retryCount);
                    await Task.Delay(delayMs);
                }
            }

            // Zwróć ostatni wynik jeśli wszystkie próby się nie udały
            return lastResult ?? GraphBulkResult.CreateError("Wszystkie próby retry się nie udały", "/$batch", "POST");
        }

        #endregion
    }
} 