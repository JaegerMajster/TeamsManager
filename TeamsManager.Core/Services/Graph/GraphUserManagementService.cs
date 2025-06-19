using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Models;
using TeamsManager.Core.Exceptions;
using TeamsManager.Core.Exceptions.Graph;
using System.Text.Json;

namespace TeamsManager.Core.Services.Graph
{
    /// <summary>
    /// Serwis zarządzający użytkownikami, członkostwem w zespołach i licencjami w Microsoft 365 przez Graph API
    /// Implementacja zarządzania użytkownikami z pełnym wsparciem dla Graph API endpoints
    /// </summary>
    public class GraphUserManagementService : IGraphUserManagementService
    {
        private readonly IModernHttpService _httpService;
        private readonly IGraphConnectionService _connectionService;
        private readonly IGraphCacheService _cacheService;
        private readonly ILogger<GraphUserManagementService> _logger;
        private readonly GraphApiConfiguration _graphConfig;

        public GraphUserManagementService(
            IModernHttpService httpService,
            IGraphConnectionService connectionService,
            IGraphCacheService cacheService,
            ILogger<GraphUserManagementService> logger,
            GraphApiConfiguration? graphConfig = null)
        {
            _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _graphConfig = graphConfig ?? new GraphApiConfiguration();
        }

        #region User Operations

        /// <summary>
        /// Sprawdza czy system ma odpowiednie uprawnienia do tworzenia użytkowników
        /// Graph API Endpoint: GET /v1.0/me/memberOf (permissions check)
        /// </summary>
        public async Task<bool> ValidateUserCreationPermissionsAsync()
        {
            try
            {
                _logger.LogInformation("Sprawdzanie uprawnień do tworzenia użytkowników...");

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                // Sprawdzenie uprawnień przez próbę pobrania informacji o użytkowniku
                var response = await _httpService.GetAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.Me}");
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Uprawnienia do zarządzania użytkownikami zostały potwierdzone");
                    return true;
                }

                _logger.LogWarning("Brak uprawnień do zarządzania użytkownikami");
                return false;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Błąd podczas sprawdzania uprawnień do tworzenia użytkowników", ex),
                    () => ValidateUserCreationPermissionsAsync(),
                    _logger,
                    "ValidateUserCreationPermissions",
                    defaultValue: false);
            }
        }

        /// <summary>
        /// Tworzy nowego użytkownika w Microsoft 365
        /// Graph API Endpoint: POST /v1.0/users
        /// </summary>
        public async Task<GraphUser?> CreateM365UserAsync(
            string displayName,
            string userPrincipalName,
            string password,
            string? usageLocation = null,
            List<string>? licenseSkuIds = null,
            bool accountEnabled = true,
            string? department = null)
        {
            try
            {
                _logger.LogInformation("Tworzenie użytkownika {UserPrincipalName}...", userPrincipalName);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    _logger.LogWarning("Nazwa wyświetlana nie może być pusta");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(userPrincipalName))
                {
                    _logger.LogWarning("Główna nazwa użytkownika nie może być pusta");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogWarning("Hasło nie może być puste");
                    return null;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                // Przygotowanie danych użytkownika
                var userData = new
                {
                    displayName = displayName,
                    userPrincipalName = userPrincipalName,
                    mailNickname = GetMailNickname(userPrincipalName),
                    accountEnabled = accountEnabled,
                    passwordProfile = new
                    {
                        password = password,
                        forceChangePasswordNextSignIn = true
                    },
                    usageLocation = usageLocation ?? "PL",
                    department = department,
                    givenName = ExtractFirstName(displayName),
                    surname = ExtractLastName(displayName)
                };

                var json = JsonSerializer.Serialize(userData);
                var response = await _httpService.PostAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.Users}", json);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Nie udało się utworzyć użytkownika {UserPrincipalName}. Status: {StatusCode}, Error: {Error}", 
                        userPrincipalName, response.StatusCode, errorContent);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);
                var createdUser = MapToGraphUser(jsonDoc.RootElement);

                _logger.LogInformation("Pomyślnie utworzono użytkownika {UserPrincipalName} z ID {UserId}", 
                    userPrincipalName, createdUser.Id);

                // Przypisanie licencji jeśli podano
                if (licenseSkuIds != null && licenseSkuIds.Any() && !string.IsNullOrEmpty(createdUser.Id))
                {
                    _logger.LogInformation("Przypisywanie licencji do użytkownika {UserPrincipalName}...", userPrincipalName);
                    
                    foreach (var licenseSkuId in licenseSkuIds)
                    {
                        try
                        {
                            await AssignLicenseToUserInternalAsync(createdUser.Id, licenseSkuId);
                        }
                        catch (Exception licenseEx)
                        {
                            _logger.LogWarning(licenseEx, "Nie udało się przypisać licencji {LicenseSkuId} do użytkownika {UserPrincipalName}", 
                                licenseSkuId, userPrincipalName);
                        }
                    }
                }

                return createdUser;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException($"Błąd podczas tworzenia użytkownika {userPrincipalName}", ex),
                    () => CreateM365UserAsync(displayName, userPrincipalName, password, usageLocation, licenseSkuIds, accountEnabled, department),
                    _logger,
                    "CreateM365User",
                    defaultValue: null);
            }
        }

        /// <summary>
        /// Ustawia stan konta użytkownika (włączone/wyłączone)
        /// Graph API Endpoint: PATCH /v1.0/users/{user-id}
        /// </summary>
        public async Task<bool> SetM365UserAccountStateAsync(string userPrincipalName, bool isEnabled)
        {
            try
            {
                _logger.LogInformation("Ustawianie stanu konta użytkownika {UserPrincipalName} na {IsEnabled}...", userPrincipalName, isEnabled);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(userPrincipalName))
                {
                    _logger.LogWarning("Główna nazwa użytkownika nie może być pusta");
                    return false;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                // Przygotowanie danych do aktualizacji
                var updateData = new
                {
                    accountEnabled = isEnabled
                };

                var json = JsonSerializer.Serialize(updateData);
                var response = await _httpService.PatchAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.User(userPrincipalName)}", json);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Pomyślnie ustawiono stan konta użytkownika {UserPrincipalName} na {IsEnabled}", userPrincipalName, isEnabled);
                    return true;
                }

                _logger.LogWarning("Nie udało się ustawić stanu konta użytkownika {UserPrincipalName}. Status: {StatusCode}", 
                    userPrincipalName, response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException($"Błąd podczas ustawiania stanu konta użytkownika {userPrincipalName}", ex),
                    () => SetM365UserAccountStateAsync(userPrincipalName, isEnabled),
                    _logger,
                    "SetM365UserAccountState",
                    defaultValue: false);
            }
        }

        /// <summary>
        /// Trwale usuwa użytkownika z Microsoft 365 (hard delete)
        /// Graph API Endpoint: DELETE /v1.0/users/{user-id}
        /// </summary>
        public async Task<bool> DeleteM365UserAsync(string userPrincipalName)
        {
            try
            {
                _logger.LogInformation("Usuwanie użytkownika {UserPrincipalName}...", userPrincipalName);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(userPrincipalName))
                {
                    _logger.LogWarning("Główna nazwa użytkownika nie może być pusta");
                    return false;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                // Sprawdzenie czy użytkownik istnieje i czy jest dezaktywowany
                var user = await GetUserByUpnAsync(userPrincipalName);
                if (user == null)
                {
                    _logger.LogWarning("Użytkownik {UserPrincipalName} nie istnieje", userPrincipalName);
                    return false;
                }

                if (user.AccountEnabled)
                {
                    _logger.LogWarning("Nie można usunąć aktywnego użytkownika {UserPrincipalName}. Najpierw dezaktywuj konto.", userPrincipalName);
                    return false;
                }

                // Usunięcie użytkownika
                var response = await _httpService.DeleteAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.User(userPrincipalName)}");

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Pomyślnie usunięto użytkownika {UserPrincipalName}", userPrincipalName);
                    return true;
                }

                _logger.LogWarning("Nie udało się usunąć użytkownika {UserPrincipalName}. Status: {StatusCode}", 
                    userPrincipalName, response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException($"Błąd podczas usuwania użytkownika {userPrincipalName}", ex),
                    () => DeleteM365UserAsync(userPrincipalName),
                    _logger,
                    "DeleteM365User",
                    defaultValue: false);
            }
        }

        /// <summary>
        /// Aktualizuje UPN użytkownika
        /// Graph API Endpoint: PATCH /v1.0/users/{user-id}
        /// </summary>
        public async Task<bool> UpdateM365UserPrincipalNameAsync(string currentUpn, string newUpn)
        {
            try
            {
                _logger.LogInformation("Aktualizacja UPN użytkownika z {CurrentUpn} na {NewUpn}...", currentUpn, newUpn);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(currentUpn))
                {
                    _logger.LogWarning("Obecny UPN nie może być pusty");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(newUpn))
                {
                    _logger.LogWarning("Nowy UPN nie może być pusty");
                    return false;
                }

                if (string.Equals(currentUpn, newUpn, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("UPN użytkownika jest już ustawiony na {NewUpn}", newUpn);
                    return true;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                // Przygotowanie danych do aktualizacji
                var updateData = new
                {
                    userPrincipalName = newUpn,
                    mailNickname = GetMailNickname(newUpn)
                };

                var json = JsonSerializer.Serialize(updateData);
                var response = await _httpService.PatchAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.User(currentUpn)}", json);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Pomyślnie zaktualizowano UPN użytkownika z {CurrentUpn} na {NewUpn}", currentUpn, newUpn);
                    return true;
                }

                _logger.LogWarning("Nie udało się zaktualizować UPN użytkownika z {CurrentUpn} na {NewUpn}. Status: {StatusCode}", 
                    currentUpn, newUpn, response.StatusCode);
                return false;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => UpdateM365UserPrincipalNameAsync(currentUpn, newUpn),
                    _logger,
                    "UpdateM365UserPrincipalName",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji UPN użytkownika z {CurrentUpn} na {NewUpn}", currentUpn, newUpn);
                return false;
            }
        }

        /// <summary>
        /// Aktualizuje właściwości użytkownika
        /// Graph API Endpoint: PATCH /v1.0/users/{user-id}
        /// </summary>
        public async Task<bool> UpdateM365UserPropertiesAsync(
            string userUpn,
            string? department = null,
            string? jobTitle = null,
            string? firstName = null,
            string? lastName = null)
        {
            try
            {
                _logger.LogInformation("Aktualizacja właściwości użytkownika {UserUpn}...", userUpn);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(userUpn))
                {
                    _logger.LogWarning("UPN użytkownika nie może być pusty");
                    return false;
                }

                // Sprawdzenie czy są dane do aktualizacji
                if (string.IsNullOrWhiteSpace(department) && 
                    string.IsNullOrWhiteSpace(jobTitle) && 
                    string.IsNullOrWhiteSpace(firstName) && 
                    string.IsNullOrWhiteSpace(lastName))
                {
                    _logger.LogInformation("Brak danych do aktualizacji dla użytkownika {UserUpn}", userUpn);
                    return true;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                // Przygotowanie danych do aktualizacji (tylko niepuste wartości)
                var updateData = new Dictionary<string, object>();

                if (!string.IsNullOrWhiteSpace(department))
                    updateData["department"] = department;

                if (!string.IsNullOrWhiteSpace(jobTitle))
                    updateData["jobTitle"] = jobTitle;

                if (!string.IsNullOrWhiteSpace(firstName))
                    updateData["givenName"] = firstName;

                if (!string.IsNullOrWhiteSpace(lastName))
                    updateData["surname"] = lastName;

                // Aktualizacja displayName jeśli podano imię lub nazwisko
                if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
                {
                    var currentUser = await GetUserByUpnAsync(userUpn);
                    if (currentUser != null)
                    {
                        var newFirstName = !string.IsNullOrWhiteSpace(firstName) ? firstName : currentUser.GivenName;
                        var newLastName = !string.IsNullOrWhiteSpace(lastName) ? lastName : currentUser.Surname;
                        updateData["displayName"] = $"{newFirstName} {newLastName}".Trim();
                    }
                }

                if (!updateData.Any())
                {
                    _logger.LogInformation("Brak danych do aktualizacji dla użytkownika {UserUpn}", userUpn);
                    return true;
                }

                var json = JsonSerializer.Serialize(updateData);
                var response = await _httpService.PatchAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.User(userUpn)}", json);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Pomyślnie zaktualizowano właściwości użytkownika {UserUpn}", userUpn);
                    return true;
                }

                _logger.LogWarning("Nie udało się zaktualizować właściwości użytkownika {UserUpn}. Status: {StatusCode}", 
                    userUpn, response.StatusCode);
                return false;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => UpdateM365UserPropertiesAsync(userUpn, department, jobTitle, firstName, lastName),
                    _logger,
                    "UpdateM365UserProperties",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji właściwości użytkownika {UserUpn}", userUpn);
                return false;
            }
        }

        /// <summary>
        /// Pobiera wszystkich użytkowników
        /// Graph API Endpoint: GET /v1.0/users
        /// </summary>
        public async Task<List<GraphUser>?> GetAllUsersAsync(string? filter = null)
        {
            try
            {
                _logger.LogInformation("Pobieranie wszystkich użytkowników...");

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                // Budowanie URL z filtrem
                var url = $"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.Users}";
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    url += $"?$filter={Uri.EscapeDataString(filter)}";
                }

                var users = await GetAllUsersInternalAsync(url);
                
                _logger.LogInformation("Pobrano {Count} użytkowników", users?.Count ?? 0);
                return users;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetAllUsersAsync(filter),
                    _logger,
                    "GetAllUsers",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania wszystkich użytkowników");
                return null;
            }
        }

        /// <summary>
        /// Pobiera użytkowników nieaktywnych przez określoną liczbę dni
        /// Graph API Endpoint: GET /v1.0/users?$filter=signInActivity/lastSignInDateTime le {date}
        /// </summary>
        public async Task<List<GraphUser>?> GetInactiveUsersAsync(int daysInactive)
        {
            try
            {
                _logger.LogInformation("Pobieranie użytkowników nieaktywnych przez {DaysInactive} dni...", daysInactive);

                if (daysInactive <= 0)
                {
                    _logger.LogWarning("Liczba dni nieaktywności musi być większa od 0");
                    return null;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                var cutoffDate = DateTime.UtcNow.AddDays(-daysInactive).ToString("yyyy-MM-ddTHH:mm:ssZ");
                var filter = $"signInActivity/lastSignInDateTime le {cutoffDate}";
                var url = $"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.Users}?$filter={Uri.EscapeDataString(filter)}";

                var users = await GetAllUsersInternalAsync(url);
                
                _logger.LogInformation("Pobrano {Count} nieaktywnych użytkowników", users?.Count ?? 0);
                return users;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetInactiveUsersAsync(daysInactive),
                    _logger,
                    "GetInactiveUsers",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania nieaktywnych użytkowników");
                return null;
            }
        }

        /// <summary>
        /// Wyszukuje duplikaty użytkowników
        /// Graph API Endpoint: GET /v1.0/users (z analizą duplikatów)
        /// </summary>
        public async Task<List<GraphUser>?> FindDuplicateUsersAsync()
        {
            try
            {
                _logger.LogInformation("Wyszukiwanie duplikatów użytkowników...");

                var allUsers = await GetAllUsersAsync();
                if (allUsers == null || !allUsers.Any())
                {
                    _logger.LogInformation("Brak użytkowników do analizy duplikatów");
                    return new List<GraphUser>();
                }

                var duplicates = new List<GraphUser>();

                // Grupowanie po displayName i wyszukiwanie duplikatów
                var displayNameGroups = allUsers
                    .Where(u => !string.IsNullOrWhiteSpace(u.DisplayName))
                    .GroupBy(u => u.DisplayName.Trim().ToLowerInvariant())
                    .Where(g => g.Count() > 1);

                foreach (var group in displayNameGroups)
                {
                    duplicates.AddRange(group);
                }

                // Grupowanie po mail i wyszukiwanie duplikatów
                var mailGroups = allUsers
                    .Where(u => !string.IsNullOrWhiteSpace(u.Mail))
                    .GroupBy(u => u.Mail!.Trim().ToLowerInvariant())
                    .Where(g => g.Count() > 1);

                foreach (var group in mailGroups)
                {
                    foreach (var user in group)
                    {
                        if (!duplicates.Any(d => d.Id == user.Id))
                        {
                            duplicates.Add(user);
                        }
                    }
                }

                _logger.LogInformation("Znaleziono {Count} duplikatów użytkowników", duplicates.Count);
                return duplicates.Distinct().ToList();
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => FindDuplicateUsersAsync(),
                    _logger,
                    "FindDuplicateUsers",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wyszukiwania duplikatów użytkowników");
                return null;
            }
        }

        /// <summary>
        /// Pobiera szczegóły użytkownika z Microsoft 365 na podstawie jego unikalnego ID (ObjectId)
        /// Graph API Endpoint: GET /v1.0/users/{user-id}
        /// </summary>
        public async Task<GraphUser?> GetM365UserByIdAsync(string userId)
        {
            try
            {
                _logger.LogInformation("Pobieranie użytkownika po ID {UserId}...", userId);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(userId))
                {
                    _logger.LogWarning("ID użytkownika nie może być pusty");
                    return null;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                var response = await _httpService.GetAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.User(userId)}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Nie znaleziono użytkownika o ID {UserId}. Status: {StatusCode}", userId, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);
                var user = MapToGraphUser(jsonDoc.RootElement);

                _logger.LogInformation("Pobrano użytkownika {UserPrincipalName} po ID {UserId}", user.UserPrincipalName, userId);
                return user;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetM365UserByIdAsync(userId),
                    _logger,
                    "GetM365UserById",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania użytkownika po ID {UserId}", userId);
                return null;
            }
        }

        /// <summary>
        /// Pobiera wszystkich użytkowników, których konto jest włączone/wyłączone
        /// Graph API Endpoint: GET /v1.0/users?$filter=accountEnabled eq {value}
        /// </summary>
        public async Task<List<GraphUser>?> GetM365UsersByAccountEnabledStateAsync(bool accountEnabled)
        {
            try
            {
                _logger.LogInformation("Pobieranie użytkowników z stanem konta {AccountEnabled}...", accountEnabled);

                var filter = $"accountEnabled eq {accountEnabled.ToString().ToLower()}";
                return await GetAllUsersAsync(filter);
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetM365UsersByAccountEnabledStateAsync(accountEnabled),
                    _logger,
                    "GetM365UsersByAccountEnabledState",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania użytkowników z stanem konta {AccountEnabled}", accountEnabled);
                return null;
            }
        }

        /// <summary>
        /// Pobiera użytkownika M365 po UPN z cache i walidacją (P0-CRITICAL)
        /// Graph API Endpoint: GET /v1.0/users/{user-principal-name}
        /// </summary>
        public async Task<GraphUser?> GetM365UserAsync(string userUpn)
        {
            try
            {
                _logger.LogInformation("Pobieranie użytkownika {UserUpn}...", userUpn);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(userUpn))
                {
                    _logger.LogWarning("UPN użytkownika nie może być pusty");
                    return null;
                }

                return await GetUserByUpnAsync(userUpn);
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetM365UserAsync(userUpn),
                    _logger,
                    "GetM365User",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania użytkownika {UserUpn}", userUpn);
                return null;
            }
        }

        /// <summary>
        /// Wyszukuje użytkowników M365 z walidacją i cache (P0-CRITICAL)
        /// Graph API Endpoint: GET /v1.0/users?$search="displayName:{searchTerm}" OR $filter=startswith(displayName,'{searchTerm}')
        /// </summary>
        public async Task<List<GraphUser>?> SearchM365UsersAsync(string searchTerm)
        {
            try
            {
                _logger.LogInformation("Wyszukiwanie użytkowników z terminem '{SearchTerm}'...", searchTerm);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    _logger.LogWarning("Termin wyszukiwania nie może być pusty");
                    return null;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                // Próba wyszukiwania przez $search (wymaga ConsistencyLevel: eventual)
                var searchUrl = $"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.Users}?$search=\"displayName:{searchTerm}\"&$count=true";
                
                var searchResponse = await _httpService.GetAsync(searchUrl, new Dictionary<string, string>
                {
                    { "ConsistencyLevel", "eventual" }
                });

                List<GraphUser>? users = null;

                if (searchResponse.IsSuccessStatusCode)
                {
                    var content = await searchResponse.Content.ReadAsStringAsync();
                    users = ParseUsersFromResponse(content);
                }

                // Fallback do filtra jeśli search nie zadziałał
                if (users == null || !users.Any())
                {
                    _logger.LogInformation("Fallback do wyszukiwania przez filtr...");
                    var filter = $"startswith(displayName,'{searchTerm}') or startswith(givenName,'{searchTerm}') or startswith(surname,'{searchTerm}') or startswith(userPrincipalName,'{searchTerm}')";
                    users = await GetAllUsersAsync(filter);
                }

                _logger.LogInformation("Znaleziono {Count} użytkowników dla terminu '{SearchTerm}'", users?.Count ?? 0, searchTerm);
                return users;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => SearchM365UsersAsync(searchTerm),
                    _logger,
                    "SearchM365Users",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wyszukiwania użytkowników z terminem '{SearchTerm}'", searchTerm);
                return null;
            }
        }

        /// <summary>
        /// Pobiera użytkowników z określonego działu
        /// Graph API Endpoint: GET /v1.0/users?$filter=department eq '{department}'
        /// </summary>
        public async Task<List<GraphUser>?> GetUsersByDepartmentAsync(string department)
        {
            try
            {
                _logger.LogInformation("Pobieranie użytkowników z działu '{Department}'...", department);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(department))
                {
                    _logger.LogWarning("Nazwa działu nie może być pusta");
                    return null;
                }

                var filter = $"department eq '{department}'";
                return await GetAllUsersAsync(filter);
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetUsersByDepartmentAsync(department),
                    _logger,
                    "GetUsersByDepartment",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania użytkowników z działu '{Department}'", department);
                return null;
            }
        }

        /// <summary>
        /// Wylogowuje użytkownika ze wszystkich sesji
        /// Graph API Endpoint: POST /v1.0/users/{user-id}/revokeSignInSessions
        /// </summary>
        public async Task<bool> RevokeUserSignInSessionsAsync(string userUpn)
        {
            try
            {
                _logger.LogInformation("Wylogowywanie użytkownika {UserUpn} ze wszystkich sesji...", userUpn);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(userUpn))
                {
                    _logger.LogWarning("UPN użytkownika nie może być pusty");
                    return false;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                // Pobieranie użytkownika
                var user = await GetUserByUpnAsync(userUpn);
                if (user == null)
                {
                    _logger.LogWarning("Nie znaleziono użytkownika {UserUpn}", userUpn);
                    return false;
                }

                // Wylogowanie ze wszystkich sesji
                var response = await _httpService.PostAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.UserRevokeSignInSessions(user.Id)}", "{}");

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Pomyślnie wylogowano użytkownika {UserUpn} ze wszystkich sesji", userUpn);
                    return true;
                }

                _logger.LogWarning("Nie udało się wylogować użytkownika {UserUpn} ze wszystkich sesji. Status: {StatusCode}", 
                    userUpn, response.StatusCode);
                return false;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => RevokeUserSignInSessionsAsync(userUpn),
                    _logger,
                    "RevokeUserSignInSessions",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas unieważniania sesji logowania użytkownika {UserUpn}", userUpn);
                return false;
            }
        }

        #endregion

        #region Team Membership Operations

        /// <summary>
        /// Dodaje użytkownika do zespołu
        /// Graph API Endpoint: POST /v1.0/teams/{team-id}/members
        /// </summary>
        public async Task<bool> AddUserToTeamAsync(string teamId, string userUpn, string role)
        {
            try
            {
                _logger.LogInformation("Dodawanie użytkownika {UserUpn} do zespołu {TeamId} z rolą {Role}...", userUpn, teamId, role);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(teamId))
                {
                    _logger.LogWarning("Team ID nie może być pusty");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(userUpn))
                {
                    _logger.LogWarning("UPN użytkownika nie może być pusty");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(role))
                {
                    _logger.LogWarning("Rola nie może być pusta");
                    return false;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                // Pobieranie użytkownika
                var user = await GetUserByUpnAsync(userUpn);
                if (user == null)
                {
                    _logger.LogWarning("Nie znaleziono użytkownika {UserUpn}", userUpn);
                    return false;
                }

                // Przygotowanie danych członka
                var memberData = new
                {
                    odataType = "#microsoft.graph.aadUserConversationMember",
                    roles = new[] { role.ToLower() },
                    userId = user.Id
                };

                var json = JsonSerializer.Serialize(memberData);
                var response = await _httpService.PostAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.TeamMembers(teamId)}", json);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Pomyślnie dodano użytkownika {UserUpn} do zespołu {TeamId}", userUpn, teamId);
                    return true;
                }

                _logger.LogWarning("Nie udało się dodać użytkownika {UserUpn} do zespołu {TeamId}. Status: {StatusCode}", 
                    userUpn, teamId, response.StatusCode);
                return false;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => AddUserToTeamAsync(teamId, userUpn, role),
                    _logger,
                    "AddUserToTeam",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas dodawania użytkownika {UserUpn} do zespołu {TeamId}", userUpn, teamId);
                return false;
            }
        }

        /// <summary>
        /// Usuwa użytkownika z zespołu
        /// Graph API Endpoint: DELETE /v1.0/teams/{team-id}/members/{membership-id}
        /// </summary>
        public async Task<bool> RemoveUserFromTeamAsync(string teamId, string userUpn)
        {
            try
            {
                _logger.LogInformation("Usuwanie użytkownika {UserUpn} z zespołu {TeamId}...", userUpn, teamId);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(teamId))
                {
                    _logger.LogWarning("Team ID nie może być pusty");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(userUpn))
                {
                    _logger.LogWarning("UPN użytkownika nie może być pusty");
                    return false;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                // Znalezienie członka zespołu
                var member = await GetTeamMemberAsync(teamId, userUpn);
                if (member == null)
                {
                    _logger.LogWarning("Użytkownik {UserUpn} nie jest członkiem zespołu {TeamId}", userUpn, teamId);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(member.Id))
                {
                    _logger.LogWarning("Brak ID członka dla użytkownika {UserUpn} w zespole {TeamId}", userUpn, teamId);
                    return false;
                }

                // Usunięcie członka
                var response = await _httpService.DeleteAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.TeamMember(teamId, member.Id)}");

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Pomyślnie usunięto użytkownika {UserUpn} z zespołu {TeamId}", userUpn, teamId);
                    return true;
                }

                _logger.LogWarning("Nie udało się usunąć użytkownika {UserUpn} z zespołu {TeamId}. Status: {StatusCode}", 
                    userUpn, teamId, response.StatusCode);
                return false;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => RemoveUserFromTeamAsync(teamId, userUpn),
                    _logger,
                    "RemoveUserFromTeam",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania użytkownika {UserUpn} z zespołu {TeamId}", userUpn, teamId);
                return false;
            }
        }

        /// <summary>
        /// Pobiera wszystkich członków zespołu
        /// Graph API Endpoint: GET /v1.0/teams/{team-id}/members
        /// </summary>
        public async Task<List<GraphTeamMember>?> GetTeamMembersAsync(string teamId)
        {
            try
            {
                _logger.LogInformation("Pobieranie członków zespołu {TeamId}...", teamId);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(teamId))
                {
                    _logger.LogWarning("Team ID nie może być pusty");
                    return null;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                var response = await _httpService.GetAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.TeamMembers(teamId)}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Nie udało się pobrać członków zespołu {TeamId}. Status: {StatusCode}", teamId, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);
                var members = new List<GraphTeamMember>();

                if (jsonDoc.RootElement.TryGetProperty("value", out var valueElement))
                {
                    foreach (var memberElement in valueElement.EnumerateArray())
                    {
                        var member = MapToGraphTeamMember(memberElement);
                        if (member != null)
                        {
                            members.Add(member);
                        }
                    }
                }

                _logger.LogInformation("Pobrano {Count} członków zespołu {TeamId}", members.Count, teamId);
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
        /// Pobiera członka zespołu
        /// Graph API Endpoint: GET /v1.0/teams/{team-id}/members/{membership-id}
        /// </summary>
        public async Task<GraphTeamMember?> GetTeamMemberAsync(string teamId, string userUpn)
        {
            try
            {
                _logger.LogInformation("Pobieranie członka zespołu {UserUpn} z zespołu {TeamId}...", userUpn, teamId);

                var members = await GetTeamMembersAsync(teamId);
                if (members == null)
                {
                    return null;
                }

                // Wyszukiwanie członka po UPN lub Email
                var member = members.FirstOrDefault(m => 
                    string.Equals(m.User?.UserPrincipalName, userUpn, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(m.User?.Mail, userUpn, StringComparison.OrdinalIgnoreCase));

                if (member != null)
                {
                    _logger.LogInformation("Znaleziono członka zespołu {UserUpn} w zespole {TeamId}", userUpn, teamId);
                }
                else
                {
                    _logger.LogInformation("Nie znaleziono członka zespołu {UserUpn} w zespole {TeamId}", userUpn, teamId);
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
                _logger.LogError(ex, "Błąd podczas pobierania członka zespołu {UserUpn} z zespołu {TeamId}", userUpn, teamId);
                return null;
            }
        }

        #endregion

        #region License Operations

        /// <summary>
        /// Przypisuje licencję do użytkownika
        /// Graph API Endpoint: POST /v1.0/users/{user-id}/assignLicense
        /// </summary>
        public async Task<bool> AssignLicenseToUserAsync(string userUpn, string licenseSkuId)
        {
            try
            {
                _logger.LogInformation("Przypisywanie licencji {LicenseSkuId} do użytkownika {UserUpn}...", licenseSkuId, userUpn);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(userUpn))
                {
                    _logger.LogWarning("UPN użytkownika nie może być pusty");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(licenseSkuId))
                {
                    _logger.LogWarning("License SKU ID nie może być pusty");
                    return false;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                // Pobieranie użytkownika
                var user = await GetUserByUpnAsync(userUpn);
                if (user == null)
                {
                    _logger.LogWarning("Nie znaleziono użytkownika {UserUpn}", userUpn);
                    return false;
                }

                // Sprawdzenie czy użytkownik już ma tę licencję
                if (user.HasLicense(licenseSkuId))
                {
                    _logger.LogInformation("Użytkownik {UserUpn} już ma licencję {LicenseSkuId}", userUpn, licenseSkuId);
                    return true;
                }

                return await AssignLicenseToUserInternalAsync(user.Id!, licenseSkuId);
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => AssignLicenseToUserAsync(userUpn, licenseSkuId),
                    _logger,
                    "AssignLicenseToUser",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas przypisywania licencji {LicenseSkuId} użytkownikowi {UserUpn}", licenseSkuId, userUpn);
                return false;
            }
        }

        /// <summary>
        /// Usuwa licencję od użytkownika
        /// Graph API Endpoint: POST /v1.0/users/{user-id}/assignLicense (with removeLicenses)
        /// </summary>
        public async Task<bool> RemoveLicenseFromUserAsync(string userUpn, string licenseSkuId)
        {
            try
            {
                _logger.LogInformation("Usuwanie licencji {LicenseSkuId} od użytkownika {UserUpn}...", licenseSkuId, userUpn);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(userUpn))
                {
                    _logger.LogWarning("UPN użytkownika nie może być pusty");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(licenseSkuId))
                {
                    _logger.LogWarning("License SKU ID nie może być pusty");
                    return false;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                // Pobieranie użytkownika
                var user = await GetUserByUpnAsync(userUpn);
                if (user == null)
                {
                    _logger.LogWarning("Nie znaleziono użytkownika {UserUpn}", userUpn);
                    return false;
                }

                // Sprawdzenie czy użytkownik ma tę licencję
                if (!user.HasLicense(licenseSkuId))
                {
                    _logger.LogInformation("Użytkownik {UserUpn} nie ma licencji {LicenseSkuId}", userUpn, licenseSkuId);
                    return true;
                }

                // Przygotowanie danych do usunięcia licencji
                var licenseData = new
                {
                    addLicenses = new object[0],
                    removeLicenses = new[] { licenseSkuId }
                };

                var json = JsonSerializer.Serialize(licenseData);
                var response = await _httpService.PostAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.UserLicenseDetails(user.Id)}", json);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Pomyślnie usunięto licencję {LicenseSkuId} od użytkownika {UserUpn}", licenseSkuId, userUpn);
                    return true;
                }

                _logger.LogWarning("Nie udało się usunąć licencji {LicenseSkuId} od użytkownika {UserUpn}. Status: {StatusCode}", 
                    licenseSkuId, userUpn, response.StatusCode);
                return false;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => RemoveLicenseFromUserAsync(userUpn, licenseSkuId),
                    _logger,
                    "RemoveLicenseFromUser",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania licencji {LicenseSkuId} od użytkownika {UserUpn}", licenseSkuId, userUpn);
                return false;
            }
        }

        /// <summary>
        /// Pobiera licencje użytkownika
        /// Graph API Endpoint: GET /v1.0/users/{user-id}/licenseDetails
        /// </summary>
        public async Task<List<License>?> GetUserLicensesAsync(string userUpn)
        {
            try
            {
                _logger.LogInformation("Pobieranie licencji użytkownika {UserUpn}...", userUpn);

                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(userUpn))
                {
                    _logger.LogWarning("UPN użytkownika nie może być pusty");
                    return null;
                }

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                // Pobieranie użytkownika z licencjami
                var user = await GetUserByUpnAsync(userUpn);
                if (user == null)
                {
                    _logger.LogWarning("Nie znaleziono użytkownika {UserUpn}", userUpn);
                    return null;
                }

                // Pobieranie szczegółów licencji
                var response = await _httpService.GetAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.UserLicenseDetails(user.Id)}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Nie udało się pobrać licencji użytkownika {UserUpn}. Status: {StatusCode}", userUpn, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);
                var licenses = new List<License>();

                if (jsonDoc.RootElement.TryGetProperty("value", out var valueElement))
                {
                    foreach (var licenseElement in valueElement.EnumerateArray())
                    {
                        var license = MapToLicense(licenseElement);
                        if (license != null)
                        {
                            licenses.Add(license);
                        }
                    }
                }

                _logger.LogInformation("Pobrano {Count} licencji dla użytkownika {UserUpn}", licenses.Count, userUpn);
                return licenses;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetUserLicensesAsync(userUpn),
                    _logger,
                    "GetUserLicenses",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania licencji użytkownika {UserUpn}", userUpn);
                return null;
            }
        }

        /// <summary>
        /// Pobiera dostępne licencje M365 z cache (P0-CRITICAL)
        /// Graph API Endpoint: GET /v1.0/subscribedSkus
        /// </summary>
        public async Task<List<License>?> GetAvailableLicensesAsync()
        {
            try
            {
                _logger.LogInformation("Pobieranie dostępnych licencji M365...");

                // Sprawdzenie czy token jest ważny
                if (!await _connectionService.CheckTokenValidityAsync())
                {
                    _logger.LogWarning("Token nie jest ważny - odświeżanie...");
                    await _connectionService.RefreshTokenAsync();
                }

                var response = await _httpService.GetAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.SubscribedSkus}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Nie udało się pobrać dostępnych licencji. Status: {StatusCode}", response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);
                var licenses = new List<License>();

                if (jsonDoc.RootElement.TryGetProperty("value", out var valueElement))
                {
                    foreach (var skuElement in valueElement.EnumerateArray())
                    {
                        var license = MapSubscribedSkuToLicense(skuElement);
                        if (license != null)
                        {
                            licenses.Add(license);
                        }
                    }
                }

                _logger.LogInformation("Pobrano {Count} dostępnych licencji M365", licenses.Count);
                return licenses;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetAvailableLicensesAsync(),
                    _logger,
                    "GetAvailableLicenses",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania dostępnych licencji M365");
                return null;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Przypisuje licencję do użytkownika (wewnętrzna metoda)
        /// </summary>
        private async Task<bool> AssignLicenseToUserInternalAsync(string userId, string licenseSkuId)
        {
            try
            {
                var licenseData = new
                {
                    addLicenses = new[]
                    {
                        new
                        {
                            skuId = licenseSkuId,
                            disabledPlans = new string[0]
                        }
                    },
                    removeLicenses = new string[0]
                };

                var json = JsonSerializer.Serialize(licenseData);
                var response = await _httpService.PostAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.UserLicenseDetails(userId)}", json);

                return response.IsSuccessStatusCode;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => AssignLicenseToUserInternalAsync(userId, licenseSkuId),
                    _logger,
                    "AssignLicenseToUserInternal",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas przypisywania licencji {LicenseSkuId} do użytkownika {UserId}", licenseSkuId, userId);
                return false;
            }
        }

        /// <summary>
        /// Wyciąga mail nickname z UPN
        /// </summary>
        private string GetMailNickname(string userPrincipalName)
        {
            if (string.IsNullOrWhiteSpace(userPrincipalName))
                return string.Empty;

            var atIndex = userPrincipalName.IndexOf('@');
            return atIndex > 0 ? userPrincipalName.Substring(0, atIndex) : userPrincipalName;
        }

        /// <summary>
        /// Wyciąga imię z display name
        /// </summary>
        private string? ExtractFirstName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return null;

            var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : null;
        }

        /// <summary>
        /// Wyciąga nazwisko z display name
        /// </summary>
        private string? ExtractLastName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return null;

            var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : null;
        }

        /// <summary>
        /// Pobiera wszystkich użytkowników z obsługą paginacji
        /// </summary>
        private async Task<List<GraphUser>?> GetAllUsersInternalAsync(string url)
        {
            try
            {
                var users = new List<GraphUser>();
                var nextUrl = url;

                while (!string.IsNullOrEmpty(nextUrl))
                {
                    var response = await _httpService.GetAsync(nextUrl);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Nie udało się pobrać użytkowników. Status: {StatusCode}", response.StatusCode);
                        return null;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(content);

                    if (jsonDoc.RootElement.TryGetProperty("value", out var valueElement))
                    {
                        foreach (var userElement in valueElement.EnumerateArray())
                        {
                            var user = MapToGraphUser(userElement);
                            users.Add(user);
                        }
                    }

                    // Sprawdzenie czy są kolejne strony
                    nextUrl = null;
                    if (jsonDoc.RootElement.TryGetProperty("@odata.nextLink", out var nextLinkElement))
                    {
                        nextUrl = nextLinkElement.GetString();
                    }
                }

                return users;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetAllUsersInternalAsync(url),
                    _logger,
                    "GetAllUsersInternal",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania użytkowników z URL {Url}", url);
                return null;
            }
        }

        /// <summary>
        /// Parsuje odpowiedź Graph API do listy użytkowników
        /// </summary>
        private List<GraphUser>? ParseUsersFromResponse(string content)
        {
            try
            {
                var users = new List<GraphUser>();
                var jsonDoc = JsonDocument.Parse(content);

                if (jsonDoc.RootElement.TryGetProperty("value", out var valueElement))
                {
                    foreach (var userElement in valueElement.EnumerateArray())
                    {
                        var user = MapToGraphUser(userElement);
                        users.Add(user);
                    }
                }

                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas parsowania odpowiedzi użytkowników");
                return null;
            }
        }

        /// <summary>
        /// Pobiera użytkownika po UPN
        /// </summary>
        private async Task<GraphUser?> GetUserByUpnAsync(string userUpn)
        {
            try
            {
                // Sprawdź cache najpierw
                var cacheKey = $"graph:user:profile:{userUpn.ToLowerInvariant()}";
                if (_cacheService.TryGetValue<GraphUser>(cacheKey, out var cachedUser))
                {
                    _logger.LogDebug("Użytkownik {UserUpn} znaleziony w cache", userUpn);
                    return cachedUser;
                }

                var response = await _httpService.GetAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.UserByUpn(userUpn)}");
                
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);
                var user = MapToGraphUser(jsonDoc.RootElement);

                // Zapisz w cache z medium-term duration (15 minut) - profile użytkowników zmieniają się rzadko
                if (user != null)
                {
                    _cacheService.Set(cacheKey, user, _cacheService.GetMediumTermCacheOptions().AbsoluteExpirationRelativeToNow);
                    
                    // Zapisz również ID użytkownika dla User ID resolution cache
                    if (!string.IsNullOrEmpty(user.Id))
                    {
                        _cacheService.SetUserId(userUpn, user.Id);
                    }
                    
                    _logger.LogDebug("Użytkownik {UserUpn} zapisany w cache", userUpn);
                }

                return user;
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => GetUserByUpnAsync(userUpn),
                    _logger,
                    "GetUserByUpn",
                    defaultValue: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania użytkownika {UserUpn}", userUpn);
                return null;
            }
        }

        /// <summary>
        /// Mapuje dane Graph API na model GraphUser
        /// </summary>
        private GraphUser MapToGraphUser(JsonElement userElement)
        {
            var user = new GraphUser();

            if (userElement.TryGetProperty("id", out var idElement))
                user.Id = idElement.GetString();

            if (userElement.TryGetProperty("givenName", out var givenNameElement))
                user.GivenName = givenNameElement.GetString();

            if (userElement.TryGetProperty("surname", out var surnameElement))
                user.Surname = surnameElement.GetString();

            if (userElement.TryGetProperty("userPrincipalName", out var upnElement))
                user.UserPrincipalName = upnElement.GetString();

            if (userElement.TryGetProperty("mail", out var mailElement))
                user.Mail = mailElement.GetString();

            if (userElement.TryGetProperty("mailNickname", out var mailNicknameElement))
                user.MailNickname = mailNicknameElement.GetString();

            if (userElement.TryGetProperty("userType", out var userTypeElement))
                user.UserType = userTypeElement.GetString();

            if (userElement.TryGetProperty("accountEnabled", out var accountEnabledElement))
                user.AccountEnabled = accountEnabledElement.GetBoolean();

            if (userElement.TryGetProperty("createdDateTime", out var createdElement))
                user.CreatedDateTime = createdElement.GetDateTime();

            if (userElement.TryGetProperty("jobTitle", out var jobTitleElement))
                user.JobTitle = jobTitleElement.GetString();

            if (userElement.TryGetProperty("department", out var departmentElement))
                user.Department = departmentElement.GetString();

            if (userElement.TryGetProperty("companyName", out var companyNameElement))
                user.CompanyName = companyNameElement.GetString();

            if (userElement.TryGetProperty("officeLocation", out var officeLocationElement))
                user.OfficeLocation = officeLocationElement.GetString();

            if (userElement.TryGetProperty("businessPhones", out var businessPhonesElement) && businessPhonesElement.ValueKind == JsonValueKind.Array)
            {
                var phones = businessPhonesElement.EnumerateArray().Select(p => p.GetString()).Where(p => !string.IsNullOrEmpty(p)).ToList();
                if (phones.Any())
                    user.BusinessPhone = phones.First();
            }

            if (userElement.TryGetProperty("mobilePhone", out var mobilePhoneElement))
                user.MobilePhone = mobilePhoneElement.GetString();

            return user;
        }

        /// <summary>
        /// Mapuje dane Graph API na model License
        /// </summary>
        private License? MapToLicense(JsonElement licenseElement)
        {
            try
            {
                var license = new License();

                if (licenseElement.TryGetProperty("skuId", out var skuIdElement))
                    license.SkuId = skuIdElement.GetString();

                if (licenseElement.TryGetProperty("skuPartNumber", out var skuPartNumberElement))
                    license.SkuPartNumber = skuPartNumberElement.GetString();

                if (licenseElement.TryGetProperty("assignedDateTime", out var assignedDateTimeElement))
                    license.AssignedDateTime = assignedDateTimeElement.GetDateTime();

                if (licenseElement.TryGetProperty("state", out var stateElement))
                    license.State = stateElement.GetString();

                // Mapowanie wyłączonych planów
                if (licenseElement.TryGetProperty("disabledPlans", out var disabledPlansElement) && disabledPlansElement.ValueKind == JsonValueKind.Array)
                {
                    var disabledPlans = disabledPlansElement.EnumerateArray()
                        .Select(p => p.GetString())
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToList();
                    license.DisabledPlans = disabledPlans!;
                }

                return license;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas mapowania licencji");
                return null;
            }
        }

        /// <summary>
        /// Mapuje SubscribedSku na License
        /// </summary>
        private License? MapSubscribedSkuToLicense(JsonElement skuElement)
        {
            try
            {
                var license = new License();

                if (skuElement.TryGetProperty("skuId", out var skuIdElement))
                    license.SkuId = skuIdElement.GetString();

                if (skuElement.TryGetProperty("skuPartNumber", out var skuPartNumberElement))
                    license.SkuPartNumber = skuPartNumberElement.GetString();

                // Dla subscribedSkus stan jest zawsze "Active" jeśli SKU jest dostępny
                license.State = "Active";

                return license;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas mapowania SubscribedSku");
                return null;
            }
        }

        /// <summary>
        /// Mapuje dane Graph API na model GraphTeamMember
        /// </summary>
        private GraphTeamMember? MapToGraphTeamMember(JsonElement memberElement)
        {
            try
            {
                var member = new GraphTeamMember();

                if (memberElement.TryGetProperty("id", out var idElement))
                    member.Id = idElement.GetString();

                if (memberElement.TryGetProperty("displayName", out var displayNameElement))
                    member.DisplayName = displayNameElement.GetString();

                if (memberElement.TryGetProperty("roles", out var rolesElement) && rolesElement.ValueKind == JsonValueKind.Array)
                {
                    var roles = rolesElement.EnumerateArray().Select(r => r.GetString()).Where(r => !string.IsNullOrEmpty(r)).ToList();
                    member.Roles = roles!;
                }

                if (memberElement.TryGetProperty("userId", out var userIdElement))
                {
                    var userId = userIdElement.GetString();
                    if (!string.IsNullOrEmpty(userId))
                    {
                        // Utworzenie podstawowego obiektu GraphUser z ID
                        member.User = new GraphUser { Id = userId };
                    }
                }

                return member;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas mapowania członka zespołu");
                return null;
            }
        }

        #endregion
    }
}
