// Plik: TeamsManager.Core/Services/UserService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using Microsoft.Identity.Client; // NOWE: Dla IConfidentialClientApplication i UserAssertion

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Serwis odpowiedzialny za logikę biznesową użytkowników.
    /// Implementuje cache'owanie dla często odpytywanych danych.
    /// </summary>
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IGenericRepository<Department> _departmentRepository;
        private readonly IGenericRepository<UserSchoolType> _userSchoolTypeRepository;
        private readonly IGenericRepository<SchoolType> _schoolTypeRepository;
        private readonly IGenericRepository<UserSubject> _userSubjectRepository;
        private readonly IGenericRepository<Subject> _subjectRepository;
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<UserService> _logger;
        private readonly IMemoryCache _cache;
        private readonly ISubjectService _subjectService;
        private readonly IPowerShellService _powerShellService;
        private readonly IConfidentialClientApplication _confidentialClientApplication; // NOWE

        // Definicje kluczy cache
        private const string AllActiveUsersCacheKey = "Users_AllActive";
        private const string UserByIdCacheKeyPrefix = "User_Id_";
        private const string UserByUpnCacheKeyPrefix = "User_Upn_";
        private const string UsersByRoleCacheKeyPrefix = "Users_Role_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(15);

        private static CancellationTokenSource _usersCacheTokenSource = new CancellationTokenSource();

        // NOWE: Domyślne zakresy dla Microsoft Graph dla operacji na użytkownikach
        private readonly string[] _graphUserReadScopes = new[] { "User.Read.All" };
        private readonly string[] _graphUserReadWriteScopes = new[] { "User.ReadWrite.All", "Directory.ReadWrite.All" };


        /// <summary>
        /// Konstruktor serwisu użytkowników.
        /// </summary>
        public UserService(
            IUserRepository userRepository,
            IGenericRepository<Department> departmentRepository,
            IGenericRepository<UserSchoolType> userSchoolTypeRepository,
            IGenericRepository<SchoolType> schoolTypeRepository,
            IGenericRepository<UserSubject> userSubjectRepository,
            IGenericRepository<Subject> subjectRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<UserService> logger,
            IMemoryCache memoryCache,
            ISubjectService subjectService,
            IPowerShellService powerShellService,
            IConfidentialClientApplication confidentialClientApplication) // NOWE
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
            _userSchoolTypeRepository = userSchoolTypeRepository ?? throw new ArgumentNullException(nameof(userSchoolTypeRepository));
            _schoolTypeRepository = schoolTypeRepository ?? throw new ArgumentNullException(nameof(schoolTypeRepository));
            _userSubjectRepository = userSubjectRepository ?? throw new ArgumentNullException(nameof(userSubjectRepository));
            _subjectRepository = subjectRepository ?? throw new ArgumentNullException(nameof(subjectRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _subjectService = subjectService ?? throw new ArgumentNullException(nameof(subjectService));
            _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
            _confidentialClientApplication = confidentialClientApplication ?? throw new ArgumentNullException(nameof(confidentialClientApplication)); // NOWE
        }

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_usersCacheTokenSource.Token));
        }

        // NOWE: Metoda pomocnicza do obsługi OBO i połączenia z Graph przez PowerShellService
        private async Task<bool> ConnectToGraphOnBehalfOfUserAsync(string? apiAccessToken, string[] scopes)
        {
            if (string.IsNullOrEmpty(apiAccessToken))
            {
                _logger.LogWarning("ConnectToGraphOnBehalfOfUserAsync: Token dostępu API (apiAccessToken) jest pusty lub null.");
                return false;
            }

            try
            {
                var userAssertion = new UserAssertion(apiAccessToken);
                _logger.LogDebug("ConnectToGraphOnBehalfOfUserAsync: Próba uzyskania tokenu OBO dla zakresów: {Scopes}", string.Join(", ", scopes));

                var authResult = await _confidentialClientApplication.AcquireTokenOnBehalfOf(scopes, userAssertion)
                    .ExecuteAsync();

                if (string.IsNullOrEmpty(authResult.AccessToken))
                {
                    _logger.LogError("ConnectToGraphOnBehalfOfUserAsync: Nie udało się uzyskać tokenu dostępu do Graph w przepływie OBO (authResult.AccessToken jest pusty).");
                    return false;
                }
                _logger.LogInformation("ConnectToGraphOnBehalfOfUserAsync: Pomyślnie uzyskano token OBO dla Graph.");
                return await _powerShellService.ConnectWithAccessTokenAsync(authResult.AccessToken, scopes);
            }
            // POPRAWKA BŁĘDU CS1061 (linia 120): Zamiast ex.SubError używamy ex.Classification
            catch (MsalUiRequiredException ex)
            {
                _logger.LogError(ex, "ConnectToGraphOnBehalfOfUserAsync: Wymagana interakcja użytkownika lub zgoda (MsalUiRequiredException) w przepływie OBO. Scopes: {Scopes}. Błąd: {Classification}. Szczegóły: {MsalErrorMessage}", string.Join(", ", scopes), ex.Classification, ex.Message);
                return false;
            }
            catch (MsalServiceException ex)
            {
                _logger.LogError(ex, "ConnectToGraphOnBehalfOfUserAsync: Błąd usługi MSAL podczas próby uzyskania tokenu OBO dla scopes: {Scopes}. Kod błędu: {MsalErrorCode}. Szczegóły: {MsalErrorMessage}", string.Join(", ", scopes), ex.ErrorCode, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConnectToGraphOnBehalfOfUserAsync: Nieoczekiwany błąd podczas uzyskiwania tokenu OBO dla scopes: {Scopes}.", string.Join(", ", scopes));
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<User?> GetUserByIdAsync(string userId, bool forceRefresh = false, string? apiAccessToken = null) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Pobieranie użytkownika o ID: {UserId}. Wymuszenie odświeżenia: {ForceRefresh}", userId, forceRefresh);
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Próba pobrania użytkownika z pustym ID.");
                return null;
            }
            string cacheKey = UserByIdCacheKeyPrefix + userId;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out User? cachedUser))
            {
                _logger.LogDebug("Użytkownik ID: {UserId} znaleziony w cache.", userId);
                return cachedUser;
            }
            _logger.LogDebug("Użytkownik ID: {UserId} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", userId);

            // ZMIANA: Logika połączenia z Graph przez OBO
            if (!string.IsNullOrEmpty(apiAccessToken))
            {
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphUserReadScopes))
                {
                    _logger.LogError("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie GetUserByIdAsync.");
                    // Kontynuujemy próbę pobrania z lokalnej bazy
                }
                else
                {
                    // Opcjonalnie: Zaktualizuj lokalną bazę danych na podstawie danych z Graph
                    // var psUser = await _powerShellService.GetM365UserAsync(userId); // PowerShellService powinien przyjmować ID lub UPN
                    // if (psUser != null) { /* logika aktualizacji lokalnego użytkownika */ }
                }
            }

            var userFromDb = await _userRepository.GetByIdAsync(userId);
            if (userFromDb != null && userFromDb.IsActive)
            {
                var cacheEntryOptions = GetDefaultCacheEntryOptions();
                _cache.Set(cacheKey, userFromDb, cacheEntryOptions);
                _logger.LogDebug("Użytkownik ID: {UserId} dodany/zaktualizowany w cache po ID.", userId);
                if (!string.IsNullOrWhiteSpace(userFromDb.UPN))
                {
                    string upnCacheKey = UserByUpnCacheKeyPrefix + userFromDb.UPN;
                    _cache.Set(upnCacheKey, userFromDb, cacheEntryOptions);
                    _logger.LogDebug("Użytkownik (ID: {UserId}, UPN: {UPN}) zaktualizowany/dodany w cache po UPN.", userId, userFromDb.UPN);
                }
            }
            else
            {
                _cache.Remove(cacheKey);
                if (userFromDb != null && !userFromDb.IsActive)
                {
                    _logger.LogDebug("Użytkownik ID: {UserId} jest nieaktywny, nie zostanie zcache'owany po ID.", userId);
                    return null;
                }
            }
            return userFromDb;
        }

        /// <inheritdoc />
        public async Task<User?> GetUserByUpnAsync(string upn, bool forceRefresh = false, string? apiAccessToken = null) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Pobieranie użytkownika o UPN: {UPN}. Wymuszenie odświeżenia: {ForceRefresh}", upn, forceRefresh);
            if (string.IsNullOrWhiteSpace(upn))
            {
                _logger.LogWarning("Próba pobrania użytkownika z pustym UPN.");
                return null;
            }
            string upnCacheKey = UserByUpnCacheKeyPrefix + upn;

            if (!forceRefresh && _cache.TryGetValue(upnCacheKey, out User? cachedUser))
            {
                _logger.LogDebug("Użytkownik UPN: {UPN} znaleziony w cache.", upn);
                return cachedUser;
            }
            _logger.LogDebug("Użytkownik UPN: {UPN} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", upn);

            // ZMIANA: Logika połączenia z Graph przez OBO
            if (!string.IsNullOrEmpty(apiAccessToken))
            {
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphUserReadScopes))
                {
                    _logger.LogError("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie GetUserByUpnAsync.");
                }
                // Opcjonalna synchronizacja z Graph
            }

            var userFromDbBase = await _userRepository.GetUserByUpnAsync(upn);
            if (userFromDbBase == null)
            {
                _logger.LogInformation("Nie znaleziono użytkownika o UPN: {UPN} w repozytorium.", upn);
                _cache.Set(upnCacheKey, (User?)null, TimeSpan.FromMinutes(1));
                return null;
            }
            var userFromDbFull = await GetUserByIdAsync(userFromDbBase.Id, forceRefresh: true, apiAccessToken: apiAccessToken); // ZMIANA: przekazanie apiAccessToken
            return userFromDbFull;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<User>> GetAllActiveUsersAsync(bool forceRefresh = false, string? apiAccessToken = null) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych użytkowników. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);
            if (!forceRefresh && _cache.TryGetValue(AllActiveUsersCacheKey, out IEnumerable<User>? cachedUsers) && cachedUsers != null)
            {
                _logger.LogDebug("Wszyscy aktywni użytkownicy znalezieni w cache.");
                return cachedUsers;
            }
            _logger.LogDebug("Wszyscy aktywni użytkownicy nie znalezieni w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");

            // ZMIANA: Logika połączenia z Graph przez OBO
            if (!string.IsNullOrEmpty(apiAccessToken))
            {
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphUserReadScopes))
                {
                    _logger.LogError("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie GetAllActiveUsersAsync.");
                }
                // Opcjonalna synchronizacja wszystkich użytkowników
            }

            var usersFromDb = await _userRepository.FindAsync(u => u.IsActive);
            _cache.Set(AllActiveUsersCacheKey, usersFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Wszyscy aktywni użytkownicy dodani do cache.");
            return usersFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role, bool forceRefresh = false, string? apiAccessToken = null) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Pobieranie użytkowników o roli: {Role}. Wymuszenie odświeżenia: {ForceRefresh}", role, forceRefresh);
            string cacheKey = UsersByRoleCacheKeyPrefix + role.ToString();

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<User>? cachedUsers) && cachedUsers != null)
            {
                _logger.LogDebug("Użytkownicy o roli {Role} znalezieni w cache.", role);
                return cachedUsers;
            }
            _logger.LogDebug("Użytkownicy o roli {Role} nie znalezieni w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", role);

            // ZMIANA: Logika połączenia z Graph przez OBO
            if (!string.IsNullOrEmpty(apiAccessToken))
            {
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphUserReadScopes))
                {
                    _logger.LogError("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie GetUsersByRoleAsync.");
                }
                // Opcjonalna synchronizacja użytkowników po roli
            }

            var usersFromDb = await _userRepository.GetUsersByRoleAsync(role);
            _cache.Set(cacheKey, usersFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Użytkownicy o roli {Role} dodani do cache.", role);
            return usersFromDb;
        }

        /// <inheritdoc />
        public async Task<User?> CreateUserAsync(
            string firstName,
            string lastName,
            string upn,
            UserRole role,
            string departmentId,
            string password,
            string apiAccessToken, // ZMIANA: accessToken -> apiAccessToken
            bool sendWelcomeEmail = false)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation";
            var operation = new OperationHistory { /* ... */ Id = Guid.NewGuid().ToString(), Type = OperationType.UserCreated, TargetEntityType = nameof(User), TargetEntityName = $"{firstName} {lastName} ({upn})", CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Rozpoczynanie tworzenia użytkownika: {FirstName} {LastName} ({UPN}) przez {User}", firstName, lastName, upn, currentUserUpn);
                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(upn) || string.IsNullOrWhiteSpace(password))
                {
                    operation.MarkAsFailed("Imię, nazwisko, UPN i hasło są wymagane.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można utworzyć użytkownika: Imię, nazwisko, UPN lub hasło są puste.");
                    return null;
                }
                var existingUser = await _userRepository.GetUserByUpnAsync(upn);
                if (existingUser != null)
                {
                    operation.MarkAsFailed($"Użytkownik o UPN '{upn}' już istnieje.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można utworzyć użytkownika: Użytkownik o UPN {UPN} już istnieje.", upn);
                    return null;
                }
                var department = await _departmentRepository.GetByIdAsync(departmentId);
                if (department == null || !department.IsActive)
                {
                    operation.MarkAsFailed($"Dział o ID '{departmentId}' nie istnieje lub jest nieaktywny.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można utworzyć użytkownika: Dział o ID {DepartmentId} nie istnieje lub jest nieaktywny.", departmentId);
                    return null;
                }

                // ZMIANA: Użycie nowej metody pomocniczej
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphUserReadWriteScopes))
                {
                    operation.MarkAsFailed("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie CreateUserAsync (OBO).");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można utworzyć użytkownika: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    return null;
                }

                string? externalUserId = await _powerShellService.Users.CreateM365UserAsync($"{firstName} {lastName}", upn, password, accountEnabled: true);
                if (string.IsNullOrEmpty(externalUserId))
                {
                    operation.MarkAsFailed("Nie udało się utworzyć użytkownika w Microsoft 365.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie udało się utworzyć użytkownika {UPN} w Microsoft 365.", upn);
                    return null;
                }

                var newUser = new User { /* ... inicjalizacja pól ... */ Id = Guid.NewGuid().ToString(), FirstName = firstName, LastName = lastName, UPN = upn, Role = role, DepartmentId = departmentId, Department = department, ExternalId = externalUserId, CreatedBy = currentUserUpn, IsActive = true };
                await _userRepository.AddAsync(newUser);
                operation.TargetEntityId = newUser.Id;
                operation.MarkAsCompleted($"Użytkownik ID: {newUser.Id} utworzony lokalnie i w M365. External ID: {externalUserId}");
                _logger.LogInformation("Użytkownik {FirstName} {LastName} ({UPN}) pomyślnie utworzony. ID: {UserId}, External ID: {ExternalUserId}", firstName, lastName, upn, newUser.Id, externalUserId);
                InvalidateUserCache(userId: newUser.Id, upn: newUser.UPN, role: newUser.Role, invalidateAllGlobalLists: true);
                if (sendWelcomeEmail) { _logger.LogInformation("TODO: Wysłanie emaila powitalnego do {UPN}", upn); }
                return newUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia użytkownika {UPN}.", upn);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return null;
            }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        public async Task<bool> UpdateUserAsync(User userToUpdate, string apiAccessToken) // ZMIANA: accessToken -> apiAccessToken
        {
            if (userToUpdate == null || string.IsNullOrWhiteSpace(userToUpdate.Id))
            {
                _logger.LogError("Próba aktualizacji użytkownika z nieprawidłowymi danymi (null lub brak ID).");
                throw new ArgumentNullException(nameof(userToUpdate));
            }
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory { /*...*/ Id = Guid.NewGuid().ToString(), Type = OperationType.UserUpdated, TargetEntityType = nameof(User), TargetEntityId = userToUpdate.Id, CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();
            string? oldUpn = null; UserRole? oldRole = null; bool oldIsActive = false;

            try
            {
                _logger.LogInformation("Rozpoczynanie aktualizacji użytkownika ID: {UserId} przez {User}", userToUpdate.Id, currentUserUpn);
                var existingUser = await _userRepository.GetByIdAsync(userToUpdate.Id);
                if (existingUser == null)
                {
                    operation.MarkAsFailed($"Użytkownik o ID '{userToUpdate.Id}' nie został znaleziony.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można zaktualizować użytkownika: Użytkownik o ID {UserId} nie istnieje.", userToUpdate.Id);
                    return false;
                }
                operation.TargetEntityName = $"{existingUser.FirstName} {existingUser.LastName} ({existingUser.UPN})";
                oldUpn = existingUser.UPN; oldRole = existingUser.Role; oldIsActive = existingUser.IsActive;

                if (!string.Equals(existingUser.UPN, userToUpdate.UPN, StringComparison.OrdinalIgnoreCase))
                {
                    var userWithSameUpn = await _userRepository.GetUserByUpnAsync(userToUpdate.UPN);
                    if (userWithSameUpn != null && userWithSameUpn.Id != userToUpdate.Id)
                    {
                        operation.MarkAsFailed($"UPN '{userToUpdate.UPN}' już istnieje w systemie.");
                        await SaveOperationHistoryAsync(operation);
                        _logger.LogError("Nie można zaktualizować użytkownika: UPN {UPN} już istnieje.", userToUpdate.UPN);
                        return false;
                    }
                }
                var department = await _departmentRepository.GetByIdAsync(userToUpdate.DepartmentId);
                if (department == null || !department.IsActive)
                {
                    operation.MarkAsFailed($"Dział o ID '{userToUpdate.DepartmentId}' nie istnieje lub jest nieaktywny.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można zaktualizować użytkownika: Dział o ID {DepartmentId} nie istnieje lub jest nieaktywny.", userToUpdate.DepartmentId);
                    return false;
                }

                // ZMIANA: Użycie nowej metody pomocniczej
                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphUserReadWriteScopes))
                {
                    operation.MarkAsFailed("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie UpdateUserAsync (OBO).");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można zaktualizować użytkownika: Nie udało się połączyć z Microsoft Graph API (OBO).");
                    return false;
                }

                bool psSuccess = await _powerShellService.Users.UpdateM365UserPropertiesAsync(userToUpdate.UPN, userToUpdate.Department?.Name, userToUpdate.Position, userToUpdate.FirstName, userToUpdate.LastName);
                if (!psSuccess)
                {
                    operation.MarkAsFailed("Nie udało się zaktualizować użytkownika w Microsoft 365.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie udało się zaktualizować użytkownika {UPN} w Microsoft 365.", userToUpdate.UPN);
                    return false;
                }
                if (!string.Equals(existingUser.UPN, userToUpdate.UPN, StringComparison.OrdinalIgnoreCase))
                {
                    bool upnUpdateSuccess = await _powerShellService.Users.UpdateM365UserPrincipalNameAsync(existingUser.UPN, userToUpdate.UPN);
                    if (!upnUpdateSuccess)
                    {
                        operation.MarkAsFailed($"Nie udało się zaktualizować UPN użytkownika w Microsoft 365 z '{existingUser.UPN}' na '{userToUpdate.UPN}'.");
                        await SaveOperationHistoryAsync(operation);
                        _logger.LogError("Nie udało się zaktualizować UPN użytkownika w Microsoft 365 z '{OldUpn}' na '{NewUpn}'.", existingUser.UPN, userToUpdate.UPN);
                        return false;
                    }
                }

                existingUser.FirstName = userToUpdate.FirstName; /* ... pozostałe pola ... */
                existingUser.LastName = userToUpdate.LastName;
                existingUser.UPN = userToUpdate.UPN;
                existingUser.Role = userToUpdate.Role;
                existingUser.DepartmentId = userToUpdate.DepartmentId;
                existingUser.Department = department;
                existingUser.Phone = userToUpdate.Phone;
                existingUser.AlternateEmail = userToUpdate.AlternateEmail;
                existingUser.ExternalId = userToUpdate.ExternalId;
                existingUser.BirthDate = userToUpdate.BirthDate;
                existingUser.EmploymentDate = userToUpdate.EmploymentDate;
                existingUser.Position = userToUpdate.Position;
                existingUser.Notes = userToUpdate.Notes;
                existingUser.IsSystemAdmin = userToUpdate.IsSystemAdmin;
                existingUser.IsActive = userToUpdate.IsActive;

                existingUser.MarkAsModified(currentUserUpn);
                _userRepository.Update(existingUser);
                operation.TargetEntityName = $"{existingUser.FirstName} {existingUser.LastName} ({existingUser.UPN})";
                operation.MarkAsCompleted("Użytkownik zaktualizowany lokalnie i w M365.");
                _logger.LogInformation("Użytkownik ID: {UserId} pomyślnie zaktualizowany.", userToUpdate.Id);
                InvalidateUserCache(userId: existingUser.Id, upn: existingUser.UPN, role: existingUser.Role, oldUpnIfChanged: oldUpn, oldRoleIfChanged: oldRole, isActiveChanged: oldIsActive != existingUser.IsActive, invalidateAllGlobalLists: true);
                return true;
            }
            catch (Exception ex) { /* ... */ _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji użytkownika ID {UserId}.", userToUpdate.Id); operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return false; }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        public async Task<bool> DeactivateUserAsync(string userId, string apiAccessToken, bool deactivateM365Account = true) // ZMIANA: accessToken -> apiAccessToken
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_deactivate";
            var operation = new OperationHistory { /*...*/ Id = Guid.NewGuid().ToString(), Type = OperationType.UserDeactivated, TargetEntityType = nameof(User), TargetEntityId = userId, CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();
            User? user = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie dezaktywacji użytkownika ID: {UserId} przez {User}", userId, currentUserUpn);
                var foundUsers = await _userRepository.FindAsync(u => u.Id == userId);
                user = foundUsers.FirstOrDefault();
                if (user == null) { /*...*/ operation.MarkAsFailed($"Użytkownik o ID '{userId}' nie został znaleziony."); await SaveOperationHistoryAsync(operation); _logger.LogError("Nie można zdezaktywować użytkownika: Użytkownik o ID {UserId} nie istnieje.", userId); return false; }
                operation.TargetEntityName = user.FullName;
                if (!user.IsActive) { /*...*/ operation.MarkAsCompleted($"Użytkownik o ID '{userId}' był już nieaktywny."); await SaveOperationHistoryAsync(operation); _logger.LogWarning("Użytkownik o ID {UserId} jest już nieaktywny.", userId); InvalidateUserCache(userId: user.Id, upn: user.UPN, role: user.Role, isActiveChanged: false, invalidateAllGlobalLists: true); return true; }

                if (deactivateM365Account)
                {
                    // ZMIANA: Użycie nowej metody pomocniczej
                    if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphUserReadWriteScopes))
                    {
                        operation.MarkAsFailed("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie DeactivateUserAsync (OBO).");
                        await SaveOperationHistoryAsync(operation);
                        _logger.LogError("Nie można zdezaktywować konta M365: Nie udało się połączyć z Microsoft Graph API (OBO).");
                        return false;
                    }
                    bool psSuccess = await _powerShellService.Users.SetM365UserAccountStateAsync(user.UPN, false);
                    if (!psSuccess) { /*...*/ operation.MarkAsFailed($"Nie udało się zdezaktywować konta użytkownika '{user.UPN}' w Microsoft 365."); await SaveOperationHistoryAsync(operation); _logger.LogError("Nie udało się zdezaktywować konta użytkownika {UPN} w Microsoft 365.", user.UPN); return false; }
                }

                user.MarkAsDeleted(currentUserUpn);
                _userRepository.Update(user);
                operation.MarkAsCompleted("Użytkownik zdezaktywowany lokalnie i opcjonalnie w M365.");
                _logger.LogInformation("Użytkownik ID: {UserId} pomyślnie zdezaktywowany.", userId);
                InvalidateUserCache(userId: user.Id, upn: user.UPN, role: user.Role, isActiveChanged: true, invalidateAllGlobalLists: true);
                return true;
            }
            catch (Exception ex) { /*...*/ _logger.LogError(ex, "Krytyczny błąd podczas dezaktywacji użytkownika ID {UserId}.", userId); operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return false; }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        public async Task<bool> ActivateUserAsync(string userId, string apiAccessToken, bool activateM365Account = true) // ZMIANA: accessToken -> apiAccessToken
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_activate";
            var operation = new OperationHistory { /*...*/ Id = Guid.NewGuid().ToString(), Type = OperationType.UserActivated, TargetEntityType = nameof(User), TargetEntityId = userId, CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();
            User? user = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie aktywacji użytkownika ID: {UserId} przez {User}", userId, currentUserUpn);
                var foundUsers = await _userRepository.FindAsync(u => u.Id == userId);
                user = foundUsers.FirstOrDefault();
                if (user == null) { /*...*/ operation.MarkAsFailed($"Użytkownik o ID '{userId}' nie został znaleziony."); await SaveOperationHistoryAsync(operation); _logger.LogError("Nie można aktywować użytkownika: Użytkownik o ID {UserId} nie istnieje.", userId); return false; }
                operation.TargetEntityName = user.FullName;
                if (user.IsActive) { /*...*/ operation.MarkAsCompleted("Użytkownik był już aktywny."); await SaveOperationHistoryAsync(operation); _logger.LogInformation("Użytkownik o ID {UserId} był już aktywny.", userId); InvalidateUserCache(userId: user.Id, upn: user.UPN, role: user.Role, isActiveChanged: false, invalidateAllGlobalLists: true); return true; }

                if (activateM365Account)
                {
                    // ZMIANA: Użycie nowej metody pomocniczej
                    if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphUserReadWriteScopes))
                    {
                        operation.MarkAsFailed("Nie udało się nawiązać połączenia z Microsoft Graph API w metodzie ActivateUserAsync (OBO).");
                        await SaveOperationHistoryAsync(operation);
                        _logger.LogError("Nie można aktywować konta M365: Nie udało się połączyć z Microsoft Graph API (OBO).");
                        return false;
                    }
                    bool psSuccess = await _powerShellService.Users.SetM365UserAccountStateAsync(user.UPN, true);
                    if (!psSuccess) { /*...*/ operation.MarkAsFailed($"Nie udało się aktywować konta użytkownika '{user.UPN}' w Microsoft 365."); await SaveOperationHistoryAsync(operation); _logger.LogError("Nie udało się aktywować konta użytkownika {UPN} w Microsoft 365.", user.UPN); return false; }
                }

                user.IsActive = true;
                user.MarkAsModified(currentUserUpn);
                _userRepository.Update(user);
                operation.MarkAsCompleted("Użytkownik aktywowany lokalnie i opcjonalnie w M365.");
                _logger.LogInformation("Użytkownik ID: {UserId} pomyślnie aktywowany.", userId);
                InvalidateUserCache(userId: user.Id, upn: user.UPN, role: user.Role, isActiveChanged: true, invalidateAllGlobalLists: true);
                return true;
            }
            catch (Exception ex) { /*...*/ _logger.LogError(ex, "Krytyczny błąd podczas aktywacji użytkownika ID {UserId}.", userId); operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return false; }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        // Metody AssignUserToSchoolTypeAsync, RemoveUserFromSchoolTypeAsync,
        // AssignTeacherToSubjectAsync, RemoveTeacherFromSubjectAsync
        // NIE wymagają `apiAccessToken`, ponieważ nie wywołują `_powerShellService`.
        // Pozostają bez zmian w kontekście OBO.

        /// <inheritdoc />
        public async Task<UserSchoolType?> AssignUserToSchoolTypeAsync(string userId, string schoolTypeId, DateTime assignedDate, DateTime? endDate = null, decimal? workloadPercentage = null, string? notes = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_assign_ust";
            var operation = new OperationHistory { /*...*/ Id = Guid.NewGuid().ToString(), Type = OperationType.UserSchoolTypeAssigned, TargetEntityType = nameof(UserSchoolType), CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();
            User? user = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie przypisania użytkownika {UserId} do typu szkoły {SchoolTypeId} przez {User}", userId, schoolTypeId, currentUserUpn);
                user = await _userRepository.GetByIdAsync(userId);
                var schoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeId);
                if (user == null || !user.IsActive) { /*...*/ operation.MarkAsFailed($"Użytkownik o ID '{userId}' nie został znaleziony lub jest nieaktywny."); await SaveOperationHistoryAsync(operation); _logger.LogError("Nie można przypisać użytkownika do typu szkoły: Użytkownik o ID {UserId} nie istnieje lub jest nieaktywny.", userId); return null; }
                if (schoolType == null || !schoolType.IsActive) { /*...*/ operation.MarkAsFailed($"Typ szkoły o ID '{schoolTypeId}' nie został znaleziony lub jest nieaktywny."); await SaveOperationHistoryAsync(operation); _logger.LogError("Nie można przypisać użytkownika do typu szkoły: Typ szkoły o ID {SchoolTypeId} nie istnieje lub jest nieaktywny.", schoolTypeId); return null; }
                operation.TargetEntityName = $"Przypisanie {user.UPN} do {schoolType.ShortName}";
                var existingAssignment = user.SchoolTypeAssignments.FirstOrDefault(ust => ust.SchoolTypeId == schoolTypeId && ust.IsActive && ust.IsCurrentlyActive);
                if (existingAssignment != null) { /*...*/ _logger.LogWarning("Użytkownik {UserId} jest już aktywnie przypisany do typu szkoły {SchoolTypeId}.", userId, schoolTypeId); operation.MarkAsFailed("Użytkownik już aktywnie przypisany do tego typu szkoły."); await SaveOperationHistoryAsync(operation); return existingAssignment; }
                var newUserSchoolType = new UserSchoolType { /* ... inicjalizacja ... */ Id = Guid.NewGuid().ToString(), UserId = userId, User = user, SchoolTypeId = schoolTypeId, SchoolType = schoolType, AssignedDate = assignedDate, EndDate = endDate, WorkloadPercentage = workloadPercentage, Notes = notes, IsCurrentlyActive = true, CreatedBy = currentUserUpn, IsActive = true };
                await _userSchoolTypeRepository.AddAsync(newUserSchoolType);
                operation.TargetEntityId = newUserSchoolType.Id;
                operation.MarkAsCompleted($"Przypisano użytkownika {userId} do typu szkoły {schoolTypeId}.");
                _logger.LogInformation("Użytkownik {UserId} pomyślnie przypisany do typu szkoły {SchoolTypeId}.", userId, schoolTypeId);
                InvalidateUserCache(userId: user.Id, upn: user.UPN, invalidateAllGlobalLists: false);
                return newUserSchoolType;
            }
            catch (Exception ex) { /*...*/ _logger.LogError(ex, "Krytyczny błąd podczas przypisania użytkownika ID {UserId} do typu szkoły ID {SchoolTypeId}.", userId, schoolTypeId); operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return null; }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveUserFromSchoolTypeAsync(string userSchoolTypeId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_ust";
            var operation = new OperationHistory { /*...*/ Id = Guid.NewGuid().ToString(), Type = OperationType.UserSchoolTypeRemoved, TargetEntityType = nameof(UserSchoolType), TargetEntityId = userSchoolTypeId, CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();
            UserSchoolType? assignment = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie usuwania przypisania UserSchoolType ID: {UserSchoolTypeId} przez {User}", userSchoolTypeId, currentUserUpn);
                assignment = await _userSchoolTypeRepository.GetByIdAsync(userSchoolTypeId);
                if (assignment == null) { /*...*/ operation.MarkAsFailed($"Przypisanie o ID '{userSchoolTypeId}' nie zostało znalezione."); await SaveOperationHistoryAsync(operation); _logger.LogError("Nie można usunąć przypisania: Przypisanie o ID {UserSchoolTypeId} nie istnieje.", userSchoolTypeId); return false; }
                var user = await _userRepository.GetByIdAsync(assignment.UserId);
                var schoolType = await _schoolTypeRepository.GetByIdAsync(assignment.SchoolTypeId);
                operation.TargetEntityName = $"Przypisanie {user?.UPN ?? assignment.UserId} do {schoolType?.ShortName ?? assignment.SchoolTypeId}";
                if (!assignment.IsActive) { /*...*/ operation.MarkAsCompleted($"Przypisanie {user?.UPN} do {schoolType?.ShortName} było już nieaktywne."); await SaveOperationHistoryAsync(operation); _logger.LogInformation("Przypisanie UserSchoolType ID {UserSchoolTypeId} było już nieaktywne.", userSchoolTypeId); if (user != null) InvalidateUserCache(userId: user.Id, upn: user.UPN, invalidateAllGlobalLists: false); return true; }
                assignment.MarkAsDeleted(currentUserUpn);
                _userSchoolTypeRepository.Update(assignment);
                operation.MarkAsCompleted($"Usunięto przypisanie UserSchoolType ID: {userSchoolTypeId}.");
                _logger.LogInformation("Przypisanie UserSchoolType ID: {UserSchoolTypeId} pomyślnie usunięte (oznaczone jako nieaktywne).", userSchoolTypeId);
                if (user != null) { InvalidateUserCache(userId: user.Id, upn: user.UPN, invalidateAllGlobalLists: false); }
                return true;
            }
            catch (Exception ex) { /*...*/ _logger.LogError(ex, "Krytyczny błąd podczas usuwania przypisania UserSchoolType ID {UserSchoolTypeId}.", userSchoolTypeId); operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return false; }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        /// <inheritdoc />
        public async Task<UserSubject?> AssignTeacherToSubjectAsync(string teacherId, string subjectId, DateTime assignedDate, string? notes = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_assign_usubj";
            var operation = new OperationHistory { /*...*/ Id = Guid.NewGuid().ToString(), Type = OperationType.UserSubjectAssigned, TargetEntityType = nameof(UserSubject), CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();
            User? teacher = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie przypisania nauczyciela {TeacherId} do przedmiotu {SubjectId} przez {User}", teacherId, subjectId, currentUserUpn);
                teacher = await _userRepository.GetByIdAsync(teacherId);
                var subject = await _subjectRepository.GetByIdAsync(subjectId);
                if (teacher == null || !teacher.IsActive || (teacher.Role != UserRole.Nauczyciel && teacher.Role != UserRole.Wicedyrektor && teacher.Role != UserRole.Dyrektor)) { /*...*/ operation.MarkAsFailed($"Użytkownik o ID '{teacherId}' nie został znaleziony, jest nieaktywny lub nie ma uprawnień do nauczania."); await SaveOperationHistoryAsync(operation); _logger.LogError("Nie można przypisać nauczyciela do przedmiotu: Użytkownik o ID {TeacherId} nie istnieje, jest nieaktywny lub nie ma odpowiedniej roli.", teacherId); return null; }
                if (subject == null || !subject.IsActive) { /*...*/ operation.MarkAsFailed($"Przedmiot o ID '{subjectId}' nie został znaleziony lub jest nieaktywny."); await SaveOperationHistoryAsync(operation); _logger.LogError("Nie można przypisać nauczyciela do przedmiotu: Przedmiot o ID {SubjectId} nie istnieje lub jest nieaktywny.", subjectId); return null; }
                operation.TargetEntityName = $"Przypisanie {teacher.UPN} do {subject.Name}";
                var existingAssignment = teacher.TaughtSubjects.FirstOrDefault(us => us.SubjectId == subjectId && us.IsActive);
                if (existingAssignment != null) { /*...*/ _logger.LogWarning("Nauczyciel {TeacherId} jest już aktywnie przypisany do przedmiotu {SubjectId}.", teacherId, subjectId); operation.MarkAsFailed("Nauczyciel już aktywnie przypisany do tego przedmiotu."); await SaveOperationHistoryAsync(operation); return existingAssignment; }
                var newUserSubject = new UserSubject { /* ... inicjalizacja ... */ Id = Guid.NewGuid().ToString(), UserId = teacherId, User = teacher, SubjectId = subjectId, Subject = subject, AssignedDate = assignedDate, Notes = notes, CreatedBy = currentUserUpn, IsActive = true };
                await _userSubjectRepository.AddAsync(newUserSubject);
                operation.TargetEntityId = newUserSubject.Id;
                operation.MarkAsCompleted($"Przypisano nauczyciela {teacherId} do przedmiotu {subjectId}.");
                _logger.LogInformation("Nauczyciel {TeacherId} pomyślnie przypisany do przedmiotu {SubjectId}.", teacherId, subjectId);
                InvalidateUserCache(userId: teacher.Id, upn: teacher.UPN, invalidateAllGlobalLists: false);
                await _subjectService.InvalidateTeachersCacheForSubjectAsync(subjectId);
                return newUserSubject;
            }
            catch (Exception ex) { /*...*/ _logger.LogError(ex, "Krytyczny błąd podczas przypisania nauczyciela ID {TeacherId} do przedmiotu ID {SubjectId}.", teacherId, subjectId); operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return null; }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveTeacherFromSubjectAsync(string userSubjectId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_usubj";
            var operation = new OperationHistory { /*...*/ Id = Guid.NewGuid().ToString(), Type = OperationType.UserSubjectRemoved, TargetEntityType = nameof(UserSubject), TargetEntityId = userSubjectId, CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();
            UserSubject? assignment = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie usuwania przypisania UserSubject ID: {UserSubjectId} przez {User}", userSubjectId, currentUserUpn);
                assignment = await _userSubjectRepository.GetByIdAsync(userSubjectId);
                if (assignment == null) { /*...*/ operation.MarkAsFailed($"Przypisanie o ID '{userSubjectId}' nie zostało znalezione."); await SaveOperationHistoryAsync(operation); _logger.LogError("Nie można usunąć przypisania: Przypisanie o ID {UserSubjectId} nie istnieje.", userSubjectId); return false; }
                var user = await _userRepository.GetByIdAsync(assignment.UserId);
                var subject = await _subjectRepository.GetByIdAsync(assignment.SubjectId);
                operation.TargetEntityName = $"Przypisanie {user?.UPN ?? assignment.UserId} do {subject?.Name ?? assignment.SubjectId}";
                if (!assignment.IsActive) { /*...*/ operation.MarkAsCompleted($"Przypisanie {user?.UPN} do {subject?.Name} było już nieaktywne."); await SaveOperationHistoryAsync(operation); _logger.LogInformation("Przypisanie UserSubject ID {UserSubjectId} było już nieaktywne.", userSubjectId); if (user != null) InvalidateUserCache(userId: user.Id, upn: user.UPN, invalidateAllGlobalLists: false); await _subjectService.InvalidateTeachersCacheForSubjectAsync(assignment.SubjectId); return true; }
                var subjectIdToInvalidate = assignment.SubjectId;
                assignment.MarkAsDeleted(currentUserUpn);
                _userSubjectRepository.Update(assignment);
                operation.MarkAsCompleted($"Usunięto przypisanie UserSubject ID: {userSubjectId}.");
                _logger.LogInformation("Przypisanie UserSubject ID: {UserSubjectId} pomyślnie usunięte (oznaczone jako nieaktywne).", userSubjectId);
                if (user != null) { InvalidateUserCache(userId: user.Id, upn: user.UPN, invalidateAllGlobalLists: false); }
                await _subjectService.InvalidateTeachersCacheForSubjectAsync(subjectIdToInvalidate);
                return true;
            }
            catch (Exception ex) { /*...*/ _logger.LogError(ex, "Krytyczny błąd podczas usuwania przypisania UserSubject ID {UserSubjectId}.", userSubjectId); operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return false; }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a użytkowników.");
            InvalidateUserCache(invalidateAll: true);
            _logger.LogInformation("Cache użytkowników został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        private void InvalidateUserCache(string? userId = null, string? upn = null, UserRole? role = null, string? oldUpnIfChanged = null, UserRole? oldRoleIfChanged = null, bool isActiveChanged = false, bool invalidateAllGlobalLists = false, bool invalidateAll = false)
        {
            _logger.LogDebug("Inwalidacja cache'u użytkowników. userId: {UserId}, upn: {UPN}, role: {Role}, oldUpn: {OldUpn}, oldRole: {OldRole}, isActiveChanged: {IsActiveChanged}, invalidateAllGlobalLists: {InvalidateAllGlobalLists}, invalidateAll: {InvalidateAll}", userId, upn, role, oldUpnIfChanged, oldRoleIfChanged, isActiveChanged, invalidateAllGlobalLists, invalidateAll);
            var oldTokenSource = Interlocked.Exchange(ref _usersCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested) { oldTokenSource.Cancel(); oldTokenSource.Dispose(); }
            _logger.LogDebug("Token cache'u dla użytkowników został zresetowany.");
            if (invalidateAll)
            {
                _cache.Remove(AllActiveUsersCacheKey);
                _logger.LogDebug("Usunięto z cache klucz: {CacheKey}", AllActiveUsersCacheKey);
                foreach (UserRole enumRole in Enum.GetValues(typeof(UserRole))) { _cache.Remove(UsersByRoleCacheKeyPrefix + enumRole.ToString()); }
                _logger.LogDebug("Usunięto z cache wszystkie klucze dla ról użytkowników (z powodu invalidateAll=true).");
            }
            if (!string.IsNullOrWhiteSpace(userId)) { _cache.Remove(UserByIdCacheKeyPrefix + userId); _logger.LogDebug("Usunięto z cache klucz: {CacheKey}{Id}", UserByIdCacheKeyPrefix, userId); }
            if (!string.IsNullOrWhiteSpace(upn)) { _cache.Remove(UserByUpnCacheKeyPrefix + upn); _logger.LogDebug("Usunięto z cache klucz: {CacheKey}{Upn}", UserByUpnCacheKeyPrefix, upn); }
            if (!string.IsNullOrWhiteSpace(oldUpnIfChanged) && oldUpnIfChanged != upn) { _cache.Remove(UserByUpnCacheKeyPrefix + oldUpnIfChanged); _logger.LogDebug("Usunięto z cache klucz dla starego UPN: {CacheKey}{OldUpn}", UserByUpnCacheKeyPrefix, oldUpnIfChanged); }
            if (invalidateAllGlobalLists || isActiveChanged)
            {
                _cache.Remove(AllActiveUsersCacheKey);
                _logger.LogDebug("Usunięto z cache klucz: {CacheKey} (z powodu invalidateAllGlobalLists lub isActiveChanged).", AllActiveUsersCacheKey);
                foreach (UserRole enumRole in Enum.GetValues(typeof(UserRole))) { _cache.Remove(UsersByRoleCacheKeyPrefix + enumRole.ToString()); }
                _logger.LogDebug("Usunięto z cache wszystkie klucze dla ról użytkowników (z powodu invalidateAllGlobalLists lub isActiveChanged).");
            }
            else
            {
                if (role.HasValue) { _cache.Remove(UsersByRoleCacheKeyPrefix + role.Value.ToString()); _logger.LogDebug("Usunięto z cache klucz dla roli: {CacheKey}{Role}", UsersByRoleCacheKeyPrefix, role.Value); }
                if (oldRoleIfChanged.HasValue && oldRoleIfChanged != role) { _cache.Remove(UsersByRoleCacheKeyPrefix + oldRoleIfChanged.Value.ToString()); _logger.LogDebug("Usunięto z cache klucz dla starej roli: {CacheKey}{OldRole}", UsersByRoleCacheKeyPrefix, oldRoleIfChanged.Value); }
            }
        }

        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id)) operation.Id = Guid.NewGuid().ToString();
            if (string.IsNullOrEmpty(operation.CreatedBy)) operation.CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system_log_save";
            if (operation.StartedAt == default(DateTime) && (operation.Status == OperationStatus.InProgress || operation.Status == OperationStatus.Pending || operation.Status == OperationStatus.Completed || operation.Status == OperationStatus.Failed))
            {
                if (operation.StartedAt == default(DateTime)) operation.StartedAt = DateTime.UtcNow;
                if (operation.Status == OperationStatus.Completed || operation.Status == OperationStatus.Failed || operation.Status == OperationStatus.Cancelled || operation.Status == OperationStatus.PartialSuccess)
                {
                    if (!operation.CompletedAt.HasValue) operation.CompletedAt = DateTime.UtcNow;
                    if (!operation.Duration.HasValue && operation.CompletedAt.HasValue) operation.Duration = operation.CompletedAt.Value - operation.StartedAt;
                }
            }
            await _operationHistoryRepository.AddAsync(operation);
            _logger.LogDebug("Zapisano nowy wpis historii operacji ID: {OperationId} dla użytkownika.", operation.Id);
        }
    }
}