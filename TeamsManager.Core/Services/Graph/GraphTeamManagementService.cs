using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Exceptions.Graph;

namespace TeamsManager.Core.Services.Graph
{
    /// <summary>
    /// Serwis zarządzania zespołami Microsoft Teams przez Graph API.
    /// TASK 2.2.1 - Utworzenie GraphTeamManagementService.
    /// Implementuje IGraphTeamManagementService z pełnym wsparciem dla Graph API endpoints.
    /// </summary>
    public class GraphTeamManagementService : IGraphTeamManagementService
    {
        private readonly IModernHttpService _httpService;
        private readonly IGraphConnectionService _connectionService;
        private readonly IGraphCacheService _cacheService;
        private readonly ILogger<GraphTeamManagementService> _logger;

        public GraphTeamManagementService(
            IModernHttpService httpService,
            IGraphConnectionService connectionService,
            IGraphCacheService cacheService,
            ILogger<GraphTeamManagementService> logger)
        {
            _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Team Operations

        /// <summary>
        /// TASK 2.2.2 - Implementacja POST /v1.0/teams - tworzenie zespołów
        /// Graph API Endpoint: POST /v1.0/teams
        /// </summary>
        public async Task<GraphTeam?> CreateTeamAsync(
            string displayName,
            string description,
            string ownerUpn,
            TeamVisibility visibility = TeamVisibility.Private,
            string? template = null)
        {
            try
            {
                _logger.LogDebug("Tworzenie zespołu: {DisplayName}, Owner: {OwnerUpn}, Visibility: {Visibility}", 
                    (object)displayName, (object)ownerUpn, (object)visibility);

                if (string.IsNullOrEmpty(displayName))
                {
                    throw new ArgumentException("Nazwa zespołu nie może być pusta", nameof(displayName));
                }

                if (string.IsNullOrEmpty(description))
                {
                    throw new ArgumentException("Opis zespołu nie może być pusty", nameof(description));
                }

                if (string.IsNullOrEmpty(ownerUpn))
                {
                    throw new ArgumentException("UPN właściciela nie może być pusty", nameof(ownerUpn));
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                // Najpierw znajdź ID użytkownika na podstawie UPN
                var ownerUser = await GetUserByUpnAsync(ownerUpn);
                if (ownerUser == null)
                {
                    throw new GraphApiException($"Nie można znaleźć użytkownika o UPN: {ownerUpn}");
                }

                // Przygotuj dane zespołu zgodnie z Graph API
                var teamData = new
                {
                    template = template ?? "@microsoft.graph.teamsTemplate",
                    displayName = displayName,
                    description = description,
                    visibility = visibility == TeamVisibility.Public ? "public" : "private",
                    members = new[]
                    {
                        new
                        {
                            odataType = "#microsoft.graph.aadUserConversationMember",
                            roles = new[] { "owner" },
                            userId = ownerUser.Id
                        }
                    }
                };

                _logger.LogDebug("Wysyłanie żądania utworzenia zespołu do Graph API");

                // Wyślij żądanie do Graph API
                var endpoint = "/v1.0/teams";
                var response = await _httpService.PostAsync<object, dynamic>(endpoint, teamData);

                if (response == null)
                {
                    _logger.LogError("Brak odpowiedzi z Graph API podczas tworzenia zespołu");
                    return null;
                }

                // Graph API zwraca 202 Accepted i Location header z URL do sprawdzenia statusu
                // Musimy poczekać na zakończenie operacji asynchronicznej
                var teamId = await WaitForTeamCreationAsync(response);
                if (string.IsNullOrEmpty(teamId))
                {
                    _logger.LogError("Nie udało się uzyskać ID utworzonego zespołu");
                    return null;
                }

                // Pobierz szczegóły utworzonego zespołu
                var createdTeam = await GetTeamAsync(teamId);
                if (createdTeam != null)
                {
                    _logger.LogInformation("Zespół {DisplayName} został utworzony z ID: {TeamId}", (object)displayName, (object)teamId);
                }

                return createdTeam;
            }
            catch (ArgumentException)
            {
                // Re-throw ArgumentException (validation errors) - nie loguj ich jako błędy
                throw;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => CreateTeamAsync(displayName, description, ownerUpn, visibility, template),
                    _logger,
                    "CreateTeam",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas tworzenia zespołu {DisplayName}", displayName);
                return null;
            }
        }

        /// <summary>
        /// TASK 2.2.3 - Implementacja PATCH /v1.0/teams/{id} - aktualizacja zespołów
        /// Graph API Endpoint: PATCH /v1.0/teams/{team-id}
        /// </summary>
        public async Task<bool> UpdateTeamPropertiesAsync(
            string teamId,
            string? newDisplayName = null,
            string? newDescription = null,
            TeamVisibility? newVisibility = null)
        {
            try
            {
                _logger.LogDebug("Aktualizacja zespołu: {TeamId}, DisplayName: {DisplayName}, Description: {Description}, Visibility: {Visibility}", 
                    (object)teamId, (object)newDisplayName, (object)newDescription, (object)newVisibility);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                // Sprawdź czy są jakieś dane do aktualizacji
                if (string.IsNullOrEmpty(newDisplayName) && 
                    string.IsNullOrEmpty(newDescription) && 
                    !newVisibility.HasValue)
                {
                    _logger.LogWarning("Brak danych do aktualizacji zespołu {TeamId}", teamId);
                    return false;
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                // Przygotuj dane do aktualizacji
                var updateData = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(newDisplayName))
                {
                    updateData["displayName"] = newDisplayName;
                }

                if (!string.IsNullOrEmpty(newDescription))
                {
                    updateData["description"] = newDescription;
                }

                // Visibility wymaga aktualizacji przez Groups API, nie Teams API
                if (newVisibility.HasValue)
                {
                    var visibilityValue = newVisibility.Value == TeamVisibility.Public ? "Public" : "Private";
                    
                    // Aktualizuj visibility przez Groups API
                    var groupUpdateData = new Dictionary<string, object>
                    {
                        ["visibility"] = visibilityValue
                    };

                    var groupEndpoint = $"/v1.0/groups/{teamId}";
                    await _httpService.PatchAsync<object>(groupEndpoint, groupUpdateData);
                    
                    _logger.LogDebug("Visibility zespołu {TeamId} została zaktualizowana na {Visibility}", teamId, visibilityValue);
                }

                // Jeśli są dane do aktualizacji w Teams API
                if (updateData.Count > 0)
                {
                    var endpoint = $"/v1.0/teams/{teamId}";
                    await _httpService.PatchAsync<object>(endpoint, updateData);
                    
                    _logger.LogDebug("Właściwości zespołu {TeamId} zostały zaktualizowane", teamId);
                }

                _logger.LogInformation("Zespół {TeamId} został pomyślnie zaktualizowany", teamId);
                return true;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => UpdateTeamPropertiesAsync(teamId, newDisplayName, newDescription, newVisibility),
                    _logger,
                    "UpdateTeamProperties",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji właściwości zespołu {TeamId}", teamId);
                return false;
            }
        }

        /// <summary>
        /// Archiwizuje zespół - Graph API: POST /v1.0/teams/{team-id}/archive
        /// </summary>
        public async Task<bool> ArchiveTeamAsync(string teamId)
        {
            try
            {
                _logger.LogDebug("Archiwizowanie zespołu: {TeamId}", teamId);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                var endpoint = $"/v1.0/teams/{teamId}/archive";
                await _httpService.PostAsync<object, object>(endpoint, new { });

                _logger.LogDebug("Zespół {TeamId} został zarchiwizowany", teamId);
                return true;
            }
            catch (ArgumentException)
            {
                // Re-throw ArgumentException (validation errors)
                throw;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => ArchiveTeamAsync(teamId),
                    _logger,
                    "ArchiveTeam",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas archiwizowania zespołu {TeamId}", teamId);
                return false;
            }
        }

        /// <summary>
        /// Przywraca zespół z archiwum - Graph API: POST /v1.0/teams/{team-id}/unarchive
        /// </summary>
        public async Task<bool> UnarchiveTeamAsync(string teamId)
        {
            try
            {
                _logger.LogDebug("Przywracanie zespołu z archiwum: {TeamId}", teamId);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                var endpoint = $"/v1.0/teams/{teamId}/unarchive";
                await _httpService.PostAsync<object, object>(endpoint, new { });

                _logger.LogDebug("Zespół {TeamId} został przywrócony z archiwum", teamId);
                return true;
            }
            catch (ArgumentException)
            {
                // Re-throw ArgumentException (validation errors)
                throw;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => UnarchiveTeamAsync(teamId),
                    _logger,
                    "UnarchiveTeam",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas przywracania zespołu {TeamId} z archiwum", teamId);
                return false;
            }
        }

        /// <summary>
        /// Usuwa zespół - Graph API: DELETE /v1.0/groups/{group-id}
        /// </summary>
        public async Task<bool> DeleteTeamAsync(string teamId)
        {
            try
            {
                _logger.LogDebug("Usuwanie zespołu: {TeamId}", teamId);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                // Teams są usuwane przez Groups API
                var endpoint = $"/v1.0/groups/{teamId}";
                await _httpService.DeleteAsync(endpoint);

                _logger.LogDebug("Zespół {TeamId} został usunięty", teamId);
                return true;
            }
            catch (ArgumentException)
            {
                // Re-throw ArgumentException (validation errors)
                throw;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => DeleteTeamAsync(teamId),
                    _logger,
                    "DeleteTeam",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania zespołu {TeamId}", teamId);
                return false;
            }
        }

        /// <summary>
        /// TASK 2.2.4 - Implementacja GET /v1.0/teams - pobieranie zespołów
        /// Graph API Endpoint: GET /v1.0/teams/{team-id}
        /// TASK 2.5.2 - Dodano cache dla Graph API responses
        /// </summary>
        public async Task<GraphTeam?> GetTeamAsync(string teamId)
        {
            try
            {
                _logger.LogDebug("Pobieranie zespołu: {TeamId}", teamId);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                // Sprawdź cache najpierw
                var cacheKey = $"graph:team:{teamId}";
                if (_cacheService.TryGetValue<GraphTeam>(cacheKey, out var cachedTeam))
                {
                    _logger.LogDebug("Zespół {TeamId} znaleziony w cache", teamId);
                    return cachedTeam;
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                var endpoint = $"/v1.0/teams/{teamId}";
                var response = await _httpService.GetAsync<dynamic>(endpoint);

                if (response == null)
                {
                    _logger.LogWarning("Zespół {TeamId} nie został znaleziony", teamId);
                    return null;
                }

                var team = MapToGraphTeam(response);
                if (team != null)
                {
                    // Pobierz dodatkowe informacje z Groups API
                    await EnrichTeamWithGroupInfoAsync(team);
                    
                    // Zapisz w cache z medium-term duration (15 minut)
                    _cacheService.Set(cacheKey, team, _cacheService.GetMediumTermCacheOptions().AbsoluteExpirationRelativeToNow);
                    
                    _logger.LogDebug("Zespół {TeamId} został pobrany i zapisany w cache: {DisplayName}", (object)teamId, (object)(team.DisplayName ?? ""));
                }

                return team;
            }
            catch (ArgumentException)
            {
                // Re-throw ArgumentException (validation errors)
                throw;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetTeamAsync(teamId),
                    _logger,
                    "GetTeam",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania zespołu {TeamId}", teamId);
                return null;
            }
        }

        /// <summary>
        /// TASK 2.2.4 - Implementacja GET /v1.0/teams - pobieranie zespołów
        /// Graph API Endpoint: GET /v1.0/me/joinedTeams
        /// </summary>
        public async Task<List<GraphTeam>?> GetAllTeamsAsync()
        {
            try
            {
                _logger.LogDebug("Pobieranie wszystkich zespołów");

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                // Pobierz zespoły do których użytkownik należy
                var endpoint = "/v1.0/me/joinedTeams";
                var response = await _httpService.GetAsync<dynamic>(endpoint);

                if (response?.value == null)
                {
                    _logger.LogWarning("Brak zespołów dla bieżącego użytkownika");
                    return new List<GraphTeam>();
                }

                var teams = new List<GraphTeam>();
                foreach (var teamData in response.value)
                {
                    var team = MapToGraphTeam(teamData);
                    if (team != null)
                    {
                        // Wzbogać informacje o zespole
                        await EnrichTeamWithGroupInfoAsync(team);
                        teams.Add(team);
                    }
                }

                _logger.LogDebug("Pobrano {Count} zespołów", teams.Count);
                return teams;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetAllTeamsAsync(),
                    _logger,
                    "GetAllTeams",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania wszystkich zespołów");
                return null;
            }
        }

        /// <summary>
        /// TASK 2.2.4 - Implementacja GET /v1.0/teams - pobieranie zespołów
        /// Graph API Endpoint: GET /v1.0/users/{user-id}/ownedObjects
        /// </summary>
        public async Task<List<GraphTeam>?> GetTeamsByOwnerAsync(string ownerUpn)
        {
            try
            {
                _logger.LogDebug("Pobieranie zespołów właściciela: {OwnerUpn}", ownerUpn);

                if (string.IsNullOrEmpty(ownerUpn))
                {
                    throw new ArgumentException("Owner UPN nie może być pusty", nameof(ownerUpn));
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                // Najpierw pobierz użytkownika
                var owner = await GetUserByUpnAsync(ownerUpn);
                if (owner == null)
                {
                    _logger.LogWarning("Nie można znaleźć użytkownika {OwnerUpn}", ownerUpn);
                    return new List<GraphTeam>();
                }

                // Pobierz obiekty należące do użytkownika (grupy)
                var endpoint = $"/v1.0/users/{owner.Id}/ownedObjects/microsoft.graph.group";
                var response = await _httpService.GetAsync<dynamic>(endpoint);

                if (response?.value == null)
                {
                    _logger.LogWarning("Brak grup należących do użytkownika {OwnerUpn}", ownerUpn);
                    return new List<GraphTeam>();
                }

                var teams = new List<GraphTeam>();
                foreach (var groupData in response.value)
                {
                    // Sprawdź czy grupa ma zespół
                    var groupId = groupData.id?.ToString();
                    if (!string.IsNullOrEmpty(groupId))
                    {
                        var team = await GetTeamAsync(groupId);
                        if (team != null)
                        {
                            teams.Add(team);
                        }
                    }
                }

                _logger.LogDebug("Pobrano {Count} zespołów dla właściciela {OwnerUpn}", teams.Count, ownerUpn);
                return teams;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetTeamsByOwnerAsync(ownerUpn),
                    _logger,
                    "GetTeamsByOwner",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania zespołów właściciela {OwnerUpn}", ownerUpn);
                return null;
            }
        }

        #endregion

        #region Team Member Management

        /// <summary>
        /// TASK 2.2.6 - Pobiera wszystkich członków zespołu
        /// Graph API Endpoint: GET /v1.0/teams/{team-id}/members
        /// TASK 2.5.2 - Dodano cache dla Graph API responses
        /// </summary>
        public async Task<List<GraphTeamMember>?> GetTeamMembersAsync(string teamId)
        {
            try
            {
                _logger.LogDebug("Pobieranie członków zespołu: {TeamId}", teamId);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                // Sprawdź cache najpierw - członkowie zmieniają się często, więc short-term cache
                var cacheKey = $"graph:team:members:{teamId}";
                if (_cacheService.TryGetValue<List<GraphTeamMember>>(cacheKey, out var cachedMembers))
                {
                    _logger.LogDebug("Członkowie zespołu {TeamId} znalezieni w cache ({Count} członków)", teamId, cachedMembers?.Count ?? 0);
                    return cachedMembers;
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                var endpoint = $"/v1.0/teams/{teamId}/members";
                var response = await _httpService.GetAsync<dynamic>(endpoint);

                if (response?.value == null)
                {
                    _logger.LogWarning("Brak członków w zespole {TeamId}", teamId);
                    var emptyList = new List<GraphTeamMember>();
                    // Cache empty result for short time to avoid repeated calls
                    _cacheService.Set(cacheKey, emptyList, _cacheService.GetShortTermCacheOptions().AbsoluteExpirationRelativeToNow);
                    return emptyList;
                }

                var members = new List<GraphTeamMember>();
                foreach (var memberData in response.value)
                {
                    var member = MapToGraphTeamMember(memberData);
                    if (member != null)
                    {
                        members.Add(member);
                    }
                }

                // Zapisz w cache z short-term duration (5 minut) - członkowie zmieniają się często
                _cacheService.Set(cacheKey, members, _cacheService.GetShortTermCacheOptions().AbsoluteExpirationRelativeToNow);

                _logger.LogDebug("Pobrano {Count} członków dla zespołu {TeamId} i zapisano w cache", members.Count, teamId);
                return members;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetTeamMembersAsync(teamId),
                    _logger,
                    "GetTeamMembers",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania członków zespołu {TeamId}", teamId);
                return null;
            }
        }

        /// <summary>
        /// TASK 2.2.6 - Pobiera pojedynczego członka zespołu
        /// Graph API Endpoint: GET /v1.0/teams/{team-id}/members
        /// </summary>
        public async Task<GraphTeamMember?> GetTeamMemberAsync(string teamId, string userUpn)
        {
            try
            {
                _logger.LogDebug("Pobieranie członka zespołu: {TeamId}, {UserUpn}", teamId, userUpn);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                if (string.IsNullOrEmpty(userUpn))
                {
                    throw new ArgumentException("User UPN nie może być pusty", nameof(userUpn));
                }

                // Pobierz wszystkich członków i znajdź odpowiedniego
                var members = await GetTeamMembersAsync(teamId);
                if (members == null)
                {
                    return null;
                }

                var member = members.FirstOrDefault(m => 
                    string.Equals(m.Email, userUpn, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(m.User?.UserPrincipalName, userUpn, StringComparison.OrdinalIgnoreCase));

                if (member != null)
                {
                    _logger.LogDebug("Członek {UserUpn} znaleziony w zespole {TeamId}", userUpn, teamId);
                }
                else
                {
                    _logger.LogDebug("Członek {UserUpn} nie znaleziony w zespole {TeamId}", userUpn, teamId);
                }

                return member;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetTeamMemberAsync(teamId, userUpn),
                    _logger,
                    "GetTeamMember",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania członka {UserUpn} z zespołu {TeamId}", userUpn, teamId);
                return null;
            }
        }

        /// <summary>
        /// TASK 2.2.6 - Implementacja POST /v1.0/teams/{id}/members - dodawanie członków
        /// Graph API Endpoint: POST /v1.0/teams/{team-id}/members
        /// </summary>
        public async Task<bool> AddTeamMemberAsync(string teamId, string userUpn, string role = "Member")
        {
            try
            {
                _logger.LogDebug("Dodawanie członka do zespołu: {TeamId}, {UserUpn}, {Role}", teamId, userUpn, role);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                if (string.IsNullOrEmpty(userUpn))
                {
                    throw new ArgumentException("User UPN nie może być pusty", nameof(userUpn));
                }

                if (string.IsNullOrEmpty(role))
                {
                    throw new ArgumentException("Rola nie może być pusta", nameof(role));
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                // Najpierw pobierz użytkownika
                var user = await GetUserByUpnAsync(userUpn);
                if (user == null)
                {
                    throw new GraphApiException($"Nie można znaleźć użytkownika o UPN: {userUpn}");
                }

                // Sprawdź czy użytkownik już jest członkiem
                var existingMember = await GetTeamMemberAsync(teamId, userUpn);
                if (existingMember != null)
                {
                    _logger.LogWarning("Użytkownik {UserUpn} już jest członkiem zespołu {TeamId}", userUpn, teamId);
                    return false;
                }

                // Przygotuj dane członka zgodnie z Graph API
                var memberData = new
                {
                    odataType = "#microsoft.graph.aadUserConversationMember",
                    roles = role.ToLower() == "owner" ? new[] { "owner" } : new[] { "member" },
                    userId = user.Id
                };

                _logger.LogDebug("Wysyłanie żądania dodania członka do Graph API");

                // Wyślij żądanie do Graph API
                var endpoint = $"/v1.0/teams/{teamId}/members";
                await _httpService.PostAsync<object, object>(endpoint, memberData);

                // Unieważnij cache członków zespołu po dodaniu nowego członka
                _cacheService.InvalidateTeamCache(teamId);

                _logger.LogInformation("Członek {UserUpn} został dodany do zespołu {TeamId} z rolą {Role}", userUpn, teamId, role);
                return true;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => AddTeamMemberAsync(teamId, userUpn, role),
                    _logger,
                    "AddTeamMember",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas dodawania członka {UserUpn} do zespołu {TeamId}", userUpn, teamId);
                return false;
            }
        }

        /// <summary>
        /// TASK 2.2.7 - Implementacja DELETE /v1.0/teams/{id}/members/{userId} - usuwanie członków
        /// Graph API Endpoint: DELETE /v1.0/teams/{team-id}/members/{membership-id}
        /// </summary>
        public async Task<bool> RemoveTeamMemberAsync(string teamId, string userUpn)
        {
            try
            {
                _logger.LogDebug("Usuwanie członka z zespołu: {TeamId}, {UserUpn}", teamId, userUpn);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                if (string.IsNullOrEmpty(userUpn))
                {
                    throw new ArgumentException("User UPN nie może być pusty", nameof(userUpn));
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                // Najpierw znajdź członka zespołu
                var member = await GetTeamMemberAsync(teamId, userUpn);
                if (member == null)
                {
                    _logger.LogWarning("Użytkownik {UserUpn} nie jest członkiem zespołu {TeamId}", userUpn, teamId);
                    return false;
                }

                if (string.IsNullOrEmpty(member.Id))
                {
                    _logger.LogError("Brak ID członka dla użytkownika {UserUpn} w zespole {TeamId}", userUpn, teamId);
                    return false;
                }

                _logger.LogDebug("Wysyłanie żądania usunięcia członka do Graph API");

                // Wyślij żądanie usunięcia do Graph API
                var endpoint = $"/v1.0/teams/{teamId}/members/{member.Id}";
                await _httpService.DeleteAsync(endpoint);

                // Unieważnij cache członków zespołu po usunięciu członka
                _cacheService.InvalidateTeamCache(teamId);

                _logger.LogInformation("Członek {UserUpn} został usunięty z zespołu {TeamId}", userUpn, teamId);
                return true;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => RemoveTeamMemberAsync(teamId, userUpn),
                    _logger,
                    "RemoveTeamMember",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania członka {UserUpn} z zespołu {TeamId}", userUpn, teamId);
                return false;
            }
        }

        /// <summary>
        /// Zmienia rolę członka zespołu - Graph API: PATCH /v1.0/teams/{team-id}/members/{membership-id}
        /// </summary>
        public async Task<bool> UpdateTeamMemberRoleAsync(string teamId, string userUpn, string newRole)
        {
            try
            {
                _logger.LogDebug("Zmiana roli członka zespołu: {TeamId}, {UserUpn}, {NewRole}", teamId, userUpn, newRole);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                if (string.IsNullOrEmpty(userUpn))
                {
                    throw new ArgumentException("User UPN nie może być pusty", nameof(userUpn));
                }

                if (string.IsNullOrEmpty(newRole))
                {
                    throw new ArgumentException("Nowa rola nie może być pusta", nameof(newRole));
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                // Najpierw znajdź membership ID użytkownika
                var member = await GetTeamMemberAsync(teamId, userUpn);
                if (member == null)
                {
                    _logger.LogWarning("Użytkownik {UserUpn} nie jest członkiem zespołu {TeamId}", userUpn, teamId);
                    return false;
                }

                var updateData = new
                {
                    roles = new[] { newRole }
                };

                var endpoint = $"/v1.0/teams/{teamId}/members/{member.Id}";
                await _httpService.PatchAsync<object>(endpoint, updateData);

                _logger.LogDebug("Rola członka {UserUpn} w zespole {TeamId} została zmieniona na {NewRole}", userUpn, teamId, newRole);
                return true;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => UpdateTeamMemberRoleAsync(teamId, userUpn, newRole),
                    _logger,
                    "UpdateTeamMemberRole",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zmiany roli członka {UserUpn} w zespole {TeamId}", userUpn, teamId);
                return false;
            }
        }

        /// <summary>
        /// Weryfikuje uprawnienia Graph API dla operacji zespołów
        /// </summary>
        public async Task<bool> VerifyGraphPermissionsAsync()
        {
            try
            {
                _logger.LogDebug("Weryfikacja uprawnień Graph API dla operacji zespołów");

                var permissionInfo = await _connectionService.GetPermissionInfoAsync();
                if (permissionInfo == null)
                {
                    _logger.LogWarning("Nie można pobrać informacji o uprawnieniach");
                    return false;
                }

                // Sprawdź wymagane uprawnienia dla operacji zespołów
                var requiredPermissions = new[]
                {
                    "Team.ReadBasic.All",
                    "Team.Create",
                    "TeamMember.ReadWrite.All",
                    "Group.Read.All",
                    "Group.ReadWrite.All"
                };

                var hasAllRequired = requiredPermissions.All(p => permissionInfo.HasPermission(p));
                
                _logger.LogDebug("Weryfikacja uprawnień zakończona: {HasAllRequired}", hasAllRequired);
                return hasAllRequired;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => VerifyGraphPermissionsAsync(),
                    _logger,
                    "VerifyGraphPermissions",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas weryfikacji uprawnień Graph API");
                return false;
            }
        }

        #endregion

        #region Diagnostic Operations

        /// <summary>
        /// Testuje połączenie z Graph API
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                _logger.LogDebug("Test połączenia z Graph API");

                var healthInfo = await _connectionService.GetConnectionHealthAsync();
                var isConnected = healthInfo?.IsConnected == true;

                _logger.LogDebug("Test połączenia zakończony: {IsConnected}", isConnected);
                return isConnected;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Błąd podczas testu połączenia z Graph API", ex),
                    () => TestConnectionAsync(),
                    _logger,
                    "TestConnection",
                    defaultValue: false);
            }
        }

        /// <summary>
        /// Waliduje uprawnienia Graph API
        /// </summary>
        public async Task<Dictionary<string, bool>> ValidatePermissionsAsync()
        {
            try
            {
                _logger.LogDebug("Walidacja uprawnień Graph API");

                var result = new Dictionary<string, bool>();
                var permissionInfo = await _connectionService.GetPermissionInfoAsync();

                if (permissionInfo == null)
                {
                    _logger.LogWarning("Nie można pobrać informacji o uprawnieniach");
                    return result;
                }

                // Sprawdź kluczowe uprawnienia dla zespołów
                var permissions = new[]
                {
                    "Team.ReadBasic.All",
                    "Team.Create", 
                    "TeamMember.ReadWrite.All",
                    "Channel.ReadBasic.All",
                    "Channel.Create",
                    "Group.Read.All",
                    "Group.ReadWrite.All"
                };

                foreach (var permission in permissions)
                {
                    result[permission] = permissionInfo.HasPermission(permission);
                }

                _logger.LogDebug("Walidacja uprawnień zakończona: {ValidPermissions}/{TotalPermissions}", 
                    result.Count(kvp => kvp.Value), result.Count);

                return result;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Błąd podczas walidacji uprawnień Graph API", ex),
                    () => ValidatePermissionsAsync(),
                    _logger,
                    "ValidatePermissions",
                    defaultValue: new Dictionary<string, bool>());
            }
        }

        /// <summary>
        /// Pobiera informacje diagnostyczne o Graph API
        /// </summary>
        public async Task<GraphDiagnosticInfo?> GetSystemInfoAsync()
        {
            try
            {
                _logger.LogDebug("Pobieranie informacji diagnostycznych Graph API");

                var diagnosticInfo = await _connectionService.GetDiagnosticInfoAsync();
                
                if (diagnosticInfo != null)
                {
                    // Dodaj informacje specyficzne dla zespołów
                    diagnosticInfo.AdditionalInfo["TeamsServiceReady"] = await TestConnectionAsync();
                    diagnosticInfo.AdditionalInfo["TeamsPermissionsValid"] = await VerifyGraphPermissionsAsync();
                }

                _logger.LogDebug("Informacje diagnostyczne pobrane: {Status}", diagnosticInfo?.Status);
                return diagnosticInfo;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Błąd podczas pobierania informacji diagnostycznych", ex),
                    () => GetSystemInfoAsync(),
                    _logger,
                    "GetSystemInfo",
                    defaultValue: null);
            }
        }

        /// <summary>
        /// Pobiera wersję Graph API
        /// </summary>
        public async Task<GraphDiagnosticInfo?> GetGraphVersionAsync()
        {
            try
            {
                _logger.LogDebug("Pobieranie wersji Graph API");

                var diagnosticInfo = await _connectionService.GetDiagnosticInfoAsync();
                
                if (diagnosticInfo != null)
                {
                    // Graph API zawsze używa v1.0
                    diagnosticInfo.GraphApiVersion = "v1.0";
                    diagnosticInfo.AdditionalInfo["GraphApiEndpoint"] = "https://graph.microsoft.com/v1.0";
                    diagnosticInfo.AdditionalInfo["TeamsApiSupported"] = true;
                }

                _logger.LogDebug("Wersja Graph API: {Version}", diagnosticInfo?.GraphApiVersion);
                return diagnosticInfo;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Błąd podczas pobierania wersji Graph API", ex),
                    () => GetGraphVersionAsync(),
                    _logger,
                    "GetGraphVersion",
                    defaultValue: null);
            }
        }

        #endregion

        #region Channel Operations

        /// <summary>
        /// TASK 2.2.5 - Implementacja POST /v1.0/teams/{id}/channels - tworzenie kanałów
        /// Graph API Endpoint: POST /v1.0/teams/{team-id}/channels
        /// </summary>
        public async Task<GraphChannel?> CreateTeamChannelAsync(
            string teamId, 
            string displayName, 
            bool isPrivate = false, 
            string? description = null)
        {
            try
            {
                _logger.LogDebug("Tworzenie kanału: {TeamId}, {DisplayName}, Private: {IsPrivate}", 
                    (object)teamId, (object)displayName, (object)isPrivate);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                if (string.IsNullOrEmpty(displayName))
                {
                    throw new ArgumentException("Nazwa kanału nie może być pusta", nameof(displayName));
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                // Przygotuj dane kanału zgodnie z Graph API
                var channelData = new Dictionary<string, object>
                {
                    ["displayName"] = displayName,
                    ["membershipType"] = isPrivate ? "private" : "standard"
                };

                if (!string.IsNullOrEmpty(description))
                {
                    channelData["description"] = description;
                }

                _logger.LogDebug("Wysyłanie żądania utworzenia kanału do Graph API");

                // Wyślij żądanie do Graph API
                var endpoint = $"/v1.0/teams/{teamId}/channels";
                var response = await _httpService.PostAsync<object, dynamic>(endpoint, channelData);

                if (response == null)
                {
                    _logger.LogError("Brak odpowiedzi z Graph API podczas tworzenia kanału");
                    return null;
                }

                // Mapuj odpowiedź na GraphChannel
                var channel = MapToGraphChannel(response, teamId);
                if (channel != null)
                {
                    _logger.LogInformation("Kanał {DisplayName} został utworzony w zespole {TeamId} z ID: {ChannelId}", 
                        (object)displayName, (object)teamId, (object)(channel.Id ?? ""));
                }

                return channel;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException($"Błąd podczas tworzenia kanału {displayName} w zespole {teamId}", ex),
                    () => CreateTeamChannelAsync(teamId, displayName, isPrivate, description),
                    _logger,
                    "CreateTeamChannel",
                    defaultValue: null);
            }
        }

        /// <summary>
        /// Aktualizuje właściwości kanału - Graph API: PATCH /v1.0/teams/{team-id}/channels/{channel-id}
        /// </summary>
        public async Task<bool> UpdateTeamChannelAsync(
            string teamId,
            string channelId,
            string? newDisplayName = null,
            string? newDescription = null)
        {
            try
            {
                _logger.LogDebug("Aktualizacja kanału: {TeamId}/{ChannelId}", teamId, channelId);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                if (string.IsNullOrEmpty(channelId))
                {
                    throw new ArgumentException("Channel ID nie może być pusty", nameof(channelId));
                }

                if (string.IsNullOrEmpty(newDisplayName) && string.IsNullOrEmpty(newDescription))
                {
                    _logger.LogWarning("Brak danych do aktualizacji kanału");
                    return false;
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                var updateData = new Dictionary<string, object>();
                
                if (!string.IsNullOrEmpty(newDisplayName))
                {
                    updateData["displayName"] = newDisplayName;
                }

                if (!string.IsNullOrEmpty(newDescription))
                {
                    updateData["description"] = newDescription;
                }

                var endpoint = $"/v1.0/teams/{teamId}/channels/{channelId}";
                await _httpService.PatchAsync<object>(endpoint, updateData);

                _logger.LogDebug("Kanał {ChannelId} w zespole {TeamId} został zaktualizowany", channelId, teamId);
                return true;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException($"Błąd podczas aktualizacji kanału {channelId} w zespole {teamId}", ex),
                    () => UpdateTeamChannelAsync(teamId, channelId, newDisplayName, newDescription),
                    _logger,
                    "UpdateTeamChannel",
                    defaultValue: false);
            }
        }

        /// <summary>
        /// Usuwa kanał z zespołu - Graph API: DELETE /v1.0/teams/{team-id}/channels/{channel-id}
        /// </summary>
        public async Task<bool> RemoveTeamChannelAsync(string teamId, string channelId)
        {
            try
            {
                _logger.LogDebug("Usuwanie kanału: {TeamId}/{ChannelId}", teamId, channelId);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                if (string.IsNullOrEmpty(channelId))
                {
                    throw new ArgumentException("Channel ID nie może być pusty", nameof(channelId));
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                var endpoint = $"/v1.0/teams/{teamId}/channels/{channelId}";
                await _httpService.DeleteAsync(endpoint);

                _logger.LogDebug("Kanał {ChannelId} został usunięty z zespołu {TeamId}", channelId, teamId);
                return true;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException($"Błąd podczas usuwania kanału {channelId} z zespołu {teamId}", ex),
                    () => RemoveTeamChannelAsync(teamId, channelId),
                    _logger,
                    "RemoveTeamChannel",
                    defaultValue: false);
            }
        }

        /// <summary>
        /// Pobiera wszystkie kanały zespołu - Graph API: GET /v1.0/teams/{team-id}/channels
        /// </summary>
        public async Task<List<GraphChannel>?> GetTeamChannelsAsync(string teamId)
        {
            try
            {
                _logger.LogDebug("Pobieranie kanałów zespołu: {TeamId}", teamId);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                var endpoint = $"/v1.0/teams/{teamId}/channels";
                var response = await _httpService.GetAsync<dynamic>(endpoint);

                if (response?.value == null)
                {
                    _logger.LogWarning("Brak kanałów w zespole {TeamId}", teamId);
                    return new List<GraphChannel>();
                }

                var channels = new List<GraphChannel>();
                foreach (var channelData in response.value)
                {
                    var channel = MapToGraphChannel(channelData, teamId);
                    if (channel != null)
                    {
                        channels.Add(channel);
                    }
                }

                _logger.LogDebug("Pobrano {Count} kanałów dla zespołu {TeamId}", channels.Count, teamId);
                return channels;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException($"Błąd podczas pobierania kanałów zespołu {teamId}", ex),
                    () => GetTeamChannelsAsync(teamId),
                    _logger,
                    "GetTeamChannels",
                    defaultValue: null);
            }
        }

        /// <summary>
        /// Pobiera kanał zespołu po nazwie
        /// </summary>
        public async Task<GraphChannel?> GetTeamChannelAsync(string teamId, string channelDisplayName)
        {
            try
            {
                _logger.LogDebug("Pobieranie kanału po nazwie: {TeamId}/{ChannelName}", teamId, channelDisplayName);

                var channels = await GetTeamChannelsAsync(teamId);
                if (channels == null)
                {
                    return null;
                }

                var channel = channels.FirstOrDefault(c => 
                    string.Equals(c.DisplayName, channelDisplayName, StringComparison.OrdinalIgnoreCase));

                _logger.LogDebug("Kanał {ChannelName} w zespole {TeamId}: {Found}", 
                    channelDisplayName, teamId, channel != null ? "znaleziony" : "nie znaleziony");

                return channel;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException($"Błąd podczas pobierania kanału {channelDisplayName} z zespołu {teamId}", ex),
                    () => GetTeamChannelAsync(teamId, channelDisplayName),
                    _logger,
                    "GetTeamChannel",
                    defaultValue: null);
            }
        }

        /// <summary>
        /// Pobiera kanał zespołu po ID
        /// </summary>
        public async Task<GraphChannel?> GetTeamChannelByIdAsync(string teamId, string channelId)
        {
            try
            {
                _logger.LogDebug("Pobieranie kanału po ID: {TeamId}/{ChannelId}", teamId, channelId);

                if (string.IsNullOrEmpty(teamId))
                {
                    throw new ArgumentException("Team ID nie może być pusty", nameof(teamId));
                }

                if (string.IsNullOrEmpty(channelId))
                {
                    throw new ArgumentException("Channel ID nie może być pusty", nameof(channelId));
                }

                // Sprawdź połączenie
                if (!await _connectionService.IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await _connectionService.RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                var endpoint = $"/v1.0/teams/{teamId}/channels/{channelId}";
                var response = await _httpService.GetAsync<dynamic>(endpoint);

                if (response == null)
                {
                    _logger.LogWarning("Kanał {ChannelId} nie został znaleziony w zespole {TeamId}", channelId, teamId);
                    return null;
                }

                var channel = MapToGraphChannel(response, teamId);
                _logger.LogDebug("Kanał {ChannelId} w zespole {TeamId} został pobrany", channelId, teamId);
                return channel;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException($"Błąd podczas pobierania kanału {channelId} z zespołu {teamId}", ex),
                    () => GetTeamChannelByIdAsync(teamId, channelId),
                    _logger,
                    "GetTeamChannelById",
                    defaultValue: null);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Pobiera użytkownika na podstawie UPN
        /// </summary>
        private async Task<GraphUser?> GetUserByUpnAsync(string upn)
        {
            try
            {
                _logger.LogDebug("Pobieranie użytkownika po UPN: {Upn}", upn);

                var endpoint = $"/v1.0/users/{upn}";
                var response = await _httpService.GetAsync<dynamic>(endpoint);

                if (response == null)
                {
                    _logger.LogWarning("Użytkownik {Upn} nie został znaleziony", upn);
                    return null;
                }

                return new GraphUser
                {
                    Id = response.id?.ToString(),
                    UserPrincipalName = response.userPrincipalName?.ToString(),
                    DisplayName = response.displayName?.ToString(),
                    Mail = response.mail?.ToString(),
                    GivenName = response.givenName?.ToString(),
                    Surname = response.surname?.ToString()
                };
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex,
                    () => GetUserByUpnAsync(upn),
                    _logger,
                    "GetUserByUpn",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania użytkownika {Upn}", upn);
                return null;
            }
        }

        /// <summary>
        /// Oczekuje na zakończenie tworzenia zespołu (Graph API operacja asynchroniczna)
        /// </summary>
        private async Task<string?> WaitForTeamCreationAsync(dynamic response)
        {
            try
            {
                _logger.LogDebug("Oczekiwanie na zakończenie tworzenia zespołu");

                // Graph API może zwrócić różne formaty odpowiedzi
                // Sprawdź czy mamy bezpośrednio ID zespołu
                if (response?.id != null)
                {
                    return response.id.ToString();
                }

                // Jeśli nie ma bezpośredniego ID, spróbuj wyciągnąć z Location header
                // To wymaga dodatkowej implementacji w IModernHttpService
                // Na razie zwróć null i zaloguj ostrzeżenie
                _logger.LogWarning("Nie można wyciągnąć ID zespołu z odpowiedzi Graph API");
                
                // Alternatywnie, możemy spróbować poczekać i sprawdzić ostatnio utworzone zespoły
                await Task.Delay(5000); // Poczekaj 5 sekund na utworzenie zespołu
                
                // Spróbuj znaleźć zespół po nazwie (nie jest to idealne, ale może działać)
                var teams = await GetAllTeamsAsync();
                if (teams != null)
                {
                    var recentTeam = teams
                        .Where(t => t.CreatedDateTime.HasValue && 
                                   t.CreatedDateTime.Value > DateTime.UtcNow.AddMinutes(-5))
                        .OrderByDescending(t => t.CreatedDateTime)
                        .FirstOrDefault();
                    
                    if (recentTeam != null)
                    {
                        _logger.LogDebug("Znaleziono prawdopodobnie utworzony zespół: {TeamId}", recentTeam.Id);
                        return recentTeam.Id;
                    }
                }

                return null;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync<string?>(ex,
                    async () => await WaitForTeamCreationAsync(response),
                    _logger,
                    "WaitForTeamCreation",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas oczekiwania na utworzenie zespołu");
                return null;
            }
        }

        /// <summary>
        /// Mapuje dane z Graph API na model GraphTeam
        /// </summary>
        private GraphTeam? MapToGraphTeam(dynamic teamData)
        {
            try
            {
                if (teamData == null)
                {
                    return null;
                }

                return new GraphTeam
                {
                    Id = teamData.id?.ToString(),
                    DisplayName = teamData.displayName?.ToString(),
                    Description = teamData.description?.ToString(),
                    Mail = teamData.mail?.ToString(),
                    MailNickname = teamData.mailNickname?.ToString(),
                    WebUrl = teamData.webUrl?.ToString(),
                    Classification = teamData.classification?.ToString(),
                    CreatedDateTime = teamData.createdDateTime != null ? 
                        DateTime.Parse(teamData.createdDateTime.ToString()) : null,
                    TenantId = teamData.tenantId?.ToString(),
                    IsActive = teamData.isArchived != null ? !bool.Parse(teamData.isArchived.ToString()) : true,
                    // Mapowanie ustawień zespołu jeśli są dostępne
                    Settings = MapTeamSettings(teamData.memberSettings),
                    GuestSettings = MapGuestSettings(teamData.guestSettings),
                    MemberSettings = MapMemberSettings(teamData.memberSettings),
                    MessagingSettings = MapMessagingSettings(teamData.messagingSettings),
                    FunSettings = MapFunSettings(teamData.funSettings),
                    DiscoverySettings = MapDiscoverySettings(teamData.discoverySettings)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd podczas mapowania danych zespołu");
                return null;
            }
        }

        /// <summary>
        /// Wzbogaca zespół o dodatkowe informacje z Groups API
        /// </summary>
        private async Task EnrichTeamWithGroupInfoAsync(GraphTeam team)
        {
            try
            {
                if (team?.Id == null)
                {
                    return;
                }

                _logger.LogDebug("Wzbogacanie informacji o zespole {TeamId}", team.Id);

                // Pobierz informacje o grupie
                var groupEndpoint = $"/v1.0/groups/{team.Id}";
                var groupResponse = await _httpService.GetAsync<dynamic>(groupEndpoint);

                if (groupResponse != null)
                {
                    // Uzupełnij brakujące informacje
                    team.Mail ??= groupResponse.mail?.ToString();
                    team.MailNickname ??= groupResponse.mailNickname?.ToString();
                    team.Classification ??= groupResponse.classification?.ToString();
                    team.CreatedDateTime ??= groupResponse.createdDateTime != null ? 
                        DateTime.Parse(groupResponse.createdDateTime.ToString()) : null;
                    
                    // Pobierz liczbę członków
                    var membersEndpoint = $"/v1.0/groups/{team.Id}/members/$count";
                    try
                    {
                        var memberCountResponse = await _httpService.GetAsync<int>(membersEndpoint);
                        team.MemberCount = memberCountResponse;
                    }
                    catch
                    {
                        // Jeśli nie można pobrać liczby członków, zostaw domyślną wartość
                        _logger.LogDebug("Nie można pobrać liczby członków dla zespołu {TeamId}", team.Id);
                    }

                    // Pobierz liczbę właścicieli
                    var ownersEndpoint = $"/v1.0/groups/{team.Id}/owners/$count";
                    try
                    {
                        var ownerCountResponse = await _httpService.GetAsync<int>(ownersEndpoint);
                        team.OwnerCount = ownerCountResponse;
                    }
                    catch
                    {
                        // Jeśli nie można pobrać liczby właścicieli, zostaw domyślną wartość
                        _logger.LogDebug("Nie można pobrać liczby właścicieli dla zespołu {TeamId}", team.Id);
                    }
                }
            }
            catch (GraphConnectionException ex)
            {
                await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex,
                    () => EnrichTeamWithGroupInfoAsync(team),
                    _logger,
                    "EnrichTeamWithGroupInfo",
                    defaultValue: Task.CompletedTask);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd podczas wzbogacania informacji o zespole {TeamId}", team?.Id);
            }
        }

        /// <summary>
        /// Mapuje ustawienia zespołu
        /// </summary>
        private GraphTeamSettings? MapTeamSettings(dynamic settingsData)
        {
            if (settingsData == null) return null;

            return new GraphTeamSettings
            {
                AllowCreateUpdateChannels = settingsData.allowCreateUpdateChannels,
                AllowDeleteChannels = settingsData.allowDeleteChannels,
                AllowAddRemoveApps = settingsData.allowAddRemoveApps,
                AllowCreateUpdateRemoveTabs = settingsData.allowCreateUpdateRemoveTabs,
                AllowCreateUpdateRemoveConnectors = settingsData.allowCreateUpdateRemoveConnectors
            };
        }

        /// <summary>
        /// Mapuje ustawienia gości
        /// </summary>
        private GraphTeamGuestSettings? MapGuestSettings(dynamic guestData)
        {
            if (guestData == null) return null;

            return new GraphTeamGuestSettings
            {
                AllowCreateUpdateChannels = guestData.allowCreateUpdateChannels,
                AllowDeleteChannels = guestData.allowDeleteChannels
            };
        }

        /// <summary>
        /// Mapuje ustawienia członków
        /// </summary>
        private GraphTeamMemberSettings? MapMemberSettings(dynamic memberData)
        {
            if (memberData == null) return null;

            return new GraphTeamMemberSettings
            {
                AllowAddRemoveApps = memberData.allowAddRemoveApps,
                AllowCreateUpdateRemoveTabs = memberData.allowCreateUpdateRemoveTabs,
                AllowCreateUpdateRemoveConnectors = memberData.allowCreateUpdateRemoveConnectors
            };
        }

        /// <summary>
        /// Mapuje ustawienia wiadomości
        /// </summary>
        private GraphTeamMessagingSettings? MapMessagingSettings(dynamic messagingData)
        {
            if (messagingData == null) return null;

            return new GraphTeamMessagingSettings
            {
                AllowUserEditMessages = messagingData.allowUserEditMessages,
                AllowUserDeleteMessages = messagingData.allowUserDeleteMessages,
                AllowOwnerDeleteMessages = messagingData.allowOwnerDeleteMessages,
                AllowTeamMentions = messagingData.allowTeamMentions,
                AllowChannelMentions = messagingData.allowChannelMentions
            };
        }

        /// <summary>
        /// Mapuje ustawienia zabawy
        /// </summary>
        private GraphTeamFunSettings? MapFunSettings(dynamic funData)
        {
            if (funData == null) return null;

            return new GraphTeamFunSettings
            {
                AllowGiphy = funData.allowGiphy,
                GiphyContentRating = funData.giphyContentRating?.ToString(),
                AllowStickersAndMemes = funData.allowStickersAndMemes,
                AllowCustomMemes = funData.allowCustomMemes
            };
        }

        /// <summary>
        /// Mapuje ustawienia odkrywania
        /// </summary>
        private GraphTeamDiscoverySettings? MapDiscoverySettings(dynamic discoveryData)
        {
            if (discoveryData == null) return null;

            return new GraphTeamDiscoverySettings
            {
                ShowInTeamsSearchResults = discoveryData.showInTeamsSearchResults
            };
        }

        /// <summary>
        /// Mapuje dane z Graph API na model GraphTeamMember
        /// </summary>
        private GraphTeamMember? MapToGraphTeamMember(dynamic memberData)
        {
            try
            {
                if (memberData == null)
                {
                    return null;
                }

                var member = new GraphTeamMember
                {
                    Id = memberData.id?.ToString(),
                    UserId = memberData.userId?.ToString(),
                    Email = memberData.email?.ToString(),
                    DisplayName = memberData.displayName?.ToString(),
                    Role = memberData.roles != null && memberData.roles.Count > 0 ? 
                        memberData.roles[0]?.ToString() : "member",
                    IsActive = true
                };

                // Jeśli są dostępne dodatkowe informacje o użytkowniku
                if (memberData.user != null)
                {
                    member.User = new GraphUser
                    {
                        Id = memberData.user.id?.ToString(),
                        UserPrincipalName = memberData.user.userPrincipalName?.ToString(),
                        DisplayName = memberData.user.displayName?.ToString(),
                        Mail = memberData.user.mail?.ToString(),
                        GivenName = memberData.user.givenName?.ToString(),
                        Surname = memberData.user.surname?.ToString()
                    };
                }

                return member;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd podczas mapowania danych członka zespołu");
                return null;
            }
        }

        /// <summary>
        /// Mapuje dane z Graph API na model GraphChannel
        /// </summary>
        private GraphChannel? MapToGraphChannel(dynamic channelData, string teamId)
        {
            try
            {
                if (channelData == null)
                {
                    return null;
                }

                return new GraphChannel
                {
                    Id = channelData.id?.ToString(),
                    TeamId = teamId,
                    DisplayName = channelData.displayName?.ToString(),
                    Description = channelData.description?.ToString(),
                    Email = channelData.email?.ToString(),
                    WebUrl = channelData.webUrl?.ToString(),
                    MembershipType = channelData.membershipType?.ToString(),
                    CreatedDateTime = channelData.createdDateTime != null ? 
                        DateTime.Parse(channelData.createdDateTime.ToString()) : null,
                    TenantId = channelData.tenantId?.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd podczas mapowania danych kanału");
                return null;
            }
        }

        #endregion
    }
}