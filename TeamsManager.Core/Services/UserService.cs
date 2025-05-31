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
using TeamsManager.Core.Abstractions.Services; // Dodano dla ISubjectService
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

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
        private readonly ISubjectService _subjectService; // NOWA ZALEŻNOŚĆ

        // Klucze cache
        private const string AllActiveUsersCacheKey = "Users_AllActive";
        private const string UserByIdCacheKeyPrefix = "User_Id_";
        private const string UserByUpnCacheKeyPrefix = "User_Upn_";
        private const string UsersByRoleCacheKeyPrefix = "Users_Role_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(15);

        private static CancellationTokenSource _usersCacheTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Konstruktor serwisu użytkowników.
        /// </summary>
        public UserService(
            IUserRepository userRepository,
            IGenericRepository<Department> departmentRepository,
            IGenericRepository<UserSchoolType> userSchoolTypeRepository,
            IGenericRepository<SchoolType> schoolTypeRepository,
            IGenericRepository<UserSubject> userSubjectRepository,
            IGenericRepository<Subject> subjectRepository, // Zachowujemy, jeśli jest używane gdzie indziej
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<UserService> logger,
            IMemoryCache memoryCache,
            ISubjectService subjectService) // NOWY PARAMETR
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
            _subjectService = subjectService ?? throw new ArgumentNullException(nameof(subjectService)); // NOWE PRZYPISANIE
        }

        /// <summary>
        /// Zwraca domyślne opcje konfiguracyjne dla wpisów cache'a.
        /// Ustawia czas wygaśnięcia i token anulowania do globalnej inwalidacji.
        /// </summary>
        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration) // Użyj SetAbsoluteExpiration lub SetSlidingExpiration
                .AddExpirationToken(new CancellationChangeToken(_usersCacheTokenSource.Token));
        }

        /// <inheritdoc />
        public async Task<User?> GetUserByIdAsync(string userId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie użytkownika o ID: {UserId}. Wymuszenie odświeżenia: {ForceRefresh}", userId, forceRefresh);

            // Walidacja parametrów wejściowych
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Próba pobrania użytkownika z pustym ID.");
                return null;
            }

            string cacheKey = UserByIdCacheKeyPrefix + userId;

            // Sprawdź cache, jeśli nie wymuszono odświeżenia
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out User? cachedUser))
            {
                _logger.LogDebug("Użytkownik ID: {UserId} znaleziony w cache.", userId);
                return cachedUser;
            }

            // Pobierz z bazy danych
            _logger.LogDebug("Użytkownik ID: {UserId} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", userId);
            var userFromDb = await _userRepository.GetByIdAsync(userId);

            if (userFromDb != null)
            {
                // Zapisz do cache pod kluczem ID
                var cacheEntryOptions = GetDefaultCacheEntryOptions();
                _cache.Set(cacheKey, userFromDb, cacheEntryOptions);

                // Dodatkowo zapisz do cache pod kluczem UPN, jeśli UPN istnieje
                if (!string.IsNullOrWhiteSpace(userFromDb.UPN))
                {
                    string upnCacheKey = UserByUpnCacheKeyPrefix + userFromDb.UPN;
                    _cache.Set(upnCacheKey, userFromDb, cacheEntryOptions);
                    _logger.LogDebug("Użytkownik (ID: {UserId}, UPN: {UPN}) zaktualizowany/dodany w cache po UPN (z GetUserByIdAsync).", userId, userFromDb.UPN);
                }
                _logger.LogDebug("Użytkownik ID: {UserId} dodany/zaktualizowany w cache po ID.", userId);
            }
            else
            {
                // Usuń z cache, jeśli użytkownik nie istnieje
                _cache.Remove(cacheKey);
            }

            return userFromDb;
        }

        /// <inheritdoc />
        public async Task<User?> GetUserByUpnAsync(string upn, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie użytkownika o UPN: {UPN}. Wymuszenie odświeżenia: {ForceRefresh}", upn, forceRefresh);

            // Walidacja parametrów wejściowych
            if (string.IsNullOrWhiteSpace(upn))
            {
                _logger.LogWarning("Próba pobrania użytkownika z pustym UPN.");
                return null;
            }

            string upnCacheKey = UserByUpnCacheKeyPrefix + upn;

            // Sprawdź cache, jeśli nie wymuszono odświeżenia
            if (!forceRefresh && _cache.TryGetValue(upnCacheKey, out User? cachedUser))
            {
                _logger.LogDebug("Użytkownik UPN: {UPN} znaleziony w cache.", upn);
                return cachedUser;
            }

            // Pobierz podstawowe dane z bazy danych
            _logger.LogDebug("Użytkownik UPN: {UPN} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", upn);
            var userFromDbBase = await _userRepository.GetUserByUpnAsync(upn);

            if (userFromDbBase == null)
            {
                _logger.LogInformation("Nie znaleziono użytkownika o UPN: {UPN} w repozytorium.", upn);
                // Cache'uj brak wyniku na krótko, aby uniknąć częstych zapytań do bazy
                _cache.Set(upnCacheKey, (User?)null, TimeSpan.FromMinutes(1));
                return null;
            }

            // Pobierz pełny obiekt przez ID, aby mieć spójne dane w cache (z zależnościami)
            // GetUserByIdAsync zajmie się cachowaniem (również po UPN)
            var userFromDbFull = await GetUserByIdAsync(userFromDbBase.Id, forceRefresh: true);

            return userFromDbFull;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<User>> GetAllActiveUsersAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych użytkowników. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);

            // Sprawdź cache, jeśli nie wymuszono odświeżenia
            if (!forceRefresh && _cache.TryGetValue(AllActiveUsersCacheKey, out IEnumerable<User>? cachedUsers) && cachedUsers != null)
            {
                _logger.LogDebug("Wszyscy aktywni użytkownicy znalezieni w cache.");
                return cachedUsers;
            }

            // Pobierz z bazy danych
            _logger.LogDebug("Wszyscy aktywni użytkownicy nie znalezieni w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var usersFromDb = await _userRepository.FindAsync(u => u.IsActive);

            // Zapisz do cache
            _cache.Set(AllActiveUsersCacheKey, usersFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Wszyscy aktywni użytkownicy dodani do cache.");

            return usersFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie użytkowników o roli: {Role}. Wymuszenie odświeżenia: {ForceRefresh}", role, forceRefresh);
            string cacheKey = UsersByRoleCacheKeyPrefix + role.ToString();

            // Sprawdź cache, jeśli nie wymuszono odświeżenia
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<User>? cachedUsers) && cachedUsers != null)
            {
                _logger.LogDebug("Użytkownicy o roli {Role} znalezieni w cache.", role);
                return cachedUsers;
            }

            // Pobierz z bazy danych
            _logger.LogDebug("Użytkownicy o roli {Role} nie znalezieni w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", role);
            var usersFromDb = await _userRepository.GetUsersByRoleAsync(role);

            // Zapisz do cache
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
            bool sendWelcomeEmail = false)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation";

            // Przygotowanie obiektu historii operacji
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.UserCreated,
                TargetEntityType = nameof(User),
                TargetEntityName = $"{firstName} {lastName} ({upn})",
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Rozpoczynanie tworzenia użytkownika: {FirstName} {LastName} ({UPN}) przez {User}", firstName, lastName, upn, currentUserUpn);

                // Walidacja podstawowych danych
                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(upn))
                {
                    operation.MarkAsFailed("Imię, nazwisko i UPN są wymagane.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można utworzyć użytkownika: Imię, nazwisko lub UPN są puste.");
                    return null;
                }

                // Sprawdź czy użytkownik o podanym UPN już istnieje
                var existingUser = await _userRepository.GetUserByUpnAsync(upn);
                if (existingUser != null)
                {
                    operation.MarkAsFailed($"Użytkownik o UPN '{upn}' już istnieje.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można utworzyć użytkownika: Użytkownik o UPN {UPN} już istnieje.", upn);
                    return null;
                }

                // Sprawdź czy dział istnieje i jest aktywny
                var department = await _departmentRepository.GetByIdAsync(departmentId);
                if (department == null || !department.IsActive)
                {
                    operation.MarkAsFailed($"Dział o ID '{departmentId}' nie istnieje lub jest nieaktywny.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można utworzyć użytkownika: Dział o ID {DepartmentId} nie istnieje lub jest nieaktywny.", departmentId);
                    return null;
                }

                // Utworzenie nowego użytkownika
                var newUser = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    FirstName = firstName,
                    LastName = lastName,
                    UPN = upn,
                    Role = role,
                    DepartmentId = departmentId,
                    Department = department,
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };

                await _userRepository.AddAsync(newUser);
                // SaveChangesAsync() będzie wołane na wyższym poziomie (np. UnitOfWork)

                // Aktualizacja historii operacji
                operation.TargetEntityId = newUser.Id;
                operation.MarkAsCompleted($"Użytkownik ID: {newUser.Id} utworzony.");
                _logger.LogInformation("Użytkownik {FirstName} {LastName} ({UPN}) pomyślnie utworzony. ID: {UserId}", firstName, lastName, upn, newUser.Id);

                // Inwalidacja cache'a - nowy użytkownik wpływa na listy użytkowników
                InvalidateCache(newUser.Id, newUser.UPN, newUser.Role, allUsersAffected: true, specificRoleAffected: true);
                _cache.Remove(AllActiveUsersCacheKey); // Bezpośrednie usunięcie dla pewności

                // Opcjonalne wysłanie emaila powitalnego
                if (sendWelcomeEmail)
                {
                    _logger.LogInformation("TODO: Wysłanie emaila powitalnego do {UPN}", upn);
                }

                return newUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia użytkownika {UPN}.", upn);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return null; // Zwracamy null, bo operacja się nie powiodła
            }
            finally // Zapewnij zapisanie historii nawet w przypadku wyjątku
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateUserAsync(User userToUpdate)
        {
            // Walidacja parametrów wejściowych
            if (userToUpdate == null || string.IsNullOrWhiteSpace(userToUpdate.Id))
            {
                _logger.LogError("Próba aktualizacji użytkownika z nieprawidłowymi danymi (null lub brak ID).");
                throw new ArgumentNullException(nameof(userToUpdate));
            }

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";

            // Przygotowanie obiektu historii operacji
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.UserUpdated,
                TargetEntityType = nameof(User),
                TargetEntityId = userToUpdate.Id,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            // Przechowywanie starych wartości do porównania i inwalidacji cache
            string? oldUpn = null;
            UserRole? oldRole = null;

            try
            {
                _logger.LogInformation("Rozpoczynanie aktualizacji użytkownika ID: {UserId} przez {User}", userToUpdate.Id, currentUserUpn);

                // Pobierz istniejącego użytkownika
                var existingUser = await _userRepository.GetByIdAsync(userToUpdate.Id);
                if (existingUser == null)
                {
                    operation.MarkAsFailed($"Użytkownik o ID '{userToUpdate.Id}' nie został znaleziony.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można zaktualizować użytkownika: Użytkownik o ID {UserId} nie istnieje.", userToUpdate.Id);
                    return false;
                }

                // Zapisz stare wartości i ustaw nazwę encji dla historii
                operation.TargetEntityName = $"{existingUser.FirstName} {existingUser.LastName} ({existingUser.UPN})";
                oldUpn = existingUser.UPN;
                oldRole = existingUser.Role;

                // Sprawdź unikalność UPN, jeśli się zmienił
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

                // Sprawdź czy nowy dział istnieje i jest aktywny
                var department = await _departmentRepository.GetByIdAsync(userToUpdate.DepartmentId);
                if (department == null || !department.IsActive)
                {
                    operation.MarkAsFailed($"Dział o ID '{userToUpdate.DepartmentId}' nie istnieje lub jest nieaktywny.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można zaktualizować użytkownika: Dział o ID {DepartmentId} nie istnieje lub jest nieaktywny.", userToUpdate.DepartmentId);
                    return false;
                }

                // Aktualizacja wszystkich pól użytkownika
                existingUser.FirstName = userToUpdate.FirstName;
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
                // SaveChangesAsync na wyższym poziomie

                // Aktualizacja historii operacji
                operation.TargetEntityName = $"{existingUser.FirstName} {existingUser.LastName} ({existingUser.UPN})";
                operation.MarkAsCompleted("Użytkownik zaktualizowany.");
                _logger.LogInformation("Użytkownik ID: {UserId} pomyślnie zaktualizowany.", userToUpdate.Id);

                // TUTAJ JEST INWALIDACJA CACHE'A - DODAJMY LOGI DEBUG
                _logger.LogDebug("Rozpoczynanie inwalidacji cache'a dla użytkownika ID: {UserId}, UPN: {UPN}, stara rola: {OldRole}",
                    existingUser.Id, oldUpn, oldRole);

                // ** BEZPOŚREDNIE WYWOŁANIE Remove BEZ PRZECHODZENIA PRZEZ InvalidateCache **
                // To jest debugging - sprawdźmy czy problem jest w metodzie InvalidateCache
                var userCacheKey = UserByIdCacheKeyPrefix + existingUser.Id;
                _cache.Remove(userCacheKey);
                _logger.LogDebug("Bezpośrednio usunięto z cache klucz: {CacheKey}", userCacheKey);

                // Usuń też inne klucze bezpośrednio
                _cache.Remove(AllActiveUsersCacheKey);
                _logger.LogDebug("Bezpośrednio usunięto AllActiveUsersCacheKey");

                if (!string.IsNullOrWhiteSpace(oldUpn))
                {
                    var oldUpnCacheKey = UserByUpnCacheKeyPrefix + oldUpn;
                    _cache.Remove(oldUpnCacheKey);
                    _logger.LogDebug("Bezpośrednio usunięto z cache stary UPN klucz: {CacheKey}", oldUpnCacheKey);
                }

                if (!string.IsNullOrWhiteSpace(existingUser.UPN) && !string.Equals(oldUpn, existingUser.UPN, StringComparison.OrdinalIgnoreCase))
                {
                    var newUpnCacheKey = UserByUpnCacheKeyPrefix + existingUser.UPN;
                    _cache.Remove(newUpnCacheKey);
                    _logger.LogDebug("Bezpośrednio usunięto z cache nowy UPN klucz: {CacheKey}", newUpnCacheKey);
                }

                if (oldRole.HasValue)
                {
                    var oldRoleCacheKey = UsersByRoleCacheKeyPrefix + oldRole.Value.ToString();
                    _cache.Remove(oldRoleCacheKey);
                    _logger.LogDebug("Bezpośrednio usunięto z cache starą rolę klucz: {CacheKey}", oldRoleCacheKey);
                }

                if (oldRole.HasValue && oldRole.Value != existingUser.Role)
                {
                    var newRoleCacheKey = UsersByRoleCacheKeyPrefix + existingUser.Role.ToString();
                    _cache.Remove(newRoleCacheKey);
                    _logger.LogDebug("Bezpośrednio usunięto z cache nową rolę klucz: {CacheKey}", newRoleCacheKey);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji użytkownika ID {UserId}.", userToUpdate.Id);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeactivateUserAsync(string userId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_deactivate";

            // Przygotowanie obiektu historii operacji
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.UserDeactivated,
                TargetEntityType = nameof(User),
                TargetEntityId = userId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            User? user = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie dezaktywacji użytkownika ID: {UserId} przez {User}", userId, currentUserUpn);

                // Pobierz użytkownika
                user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    operation.MarkAsFailed($"Użytkownik o ID '{userId}' nie został znaleziony.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można zdezaktywować użytkownika: Użytkownik o ID {UserId} nie istnieje.", userId);
                    return false;
                }
                operation.TargetEntityName = user.FullName;

                // Sprawdź czy użytkownik nie jest już nieaktywny
                if (!user.IsActive)
                {
                    operation.MarkAsCompleted($"Użytkownik o ID '{userId}' był już nieaktywny.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogWarning("Użytkownik o ID {UserId} jest już nieaktywny.", userId);

                    // Mimo wszystko odśwież cache, bo testy mogą tego oczekiwać
                    InvalidateCache(user.Id, user.UPN, user.Role, allUsersAffected: true, specificRoleAffected: true);
                    _cache.Remove(AllActiveUsersCacheKey);
                    _logger.LogDebug("Jawnie usunięto AllActiveUsersCacheKey, użytkownik był już nieaktywny.");
                    return true; // Operacja "udana", bo stan docelowy osiągnięty
                }

                // Dezaktywuj użytkownika
                user.MarkAsDeleted(currentUserUpn);
                _userRepository.Update(user);
                // SaveChangesAsync na wyższym poziomie

                // Aktualizacja historii operacji
                operation.MarkAsCompleted("Użytkownik zdezaktywowany.");
                _logger.LogInformation("Użytkownik ID: {UserId} pomyślnie zdezaktywowany.", userId);

                // Inwalidacja cache'a - użytkownik został zdezaktywowany
                InvalidateCache(user.Id, user.UPN, user.Role, allUsersAffected: true, specificRoleAffected: true);
                _cache.Remove(AllActiveUsersCacheKey);
                _logger.LogDebug("Jawnie usunięto AllActiveUsersCacheKey po dezaktywacji użytkownika.");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas dezaktywacji użytkownika ID {UserId}.", userId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> ActivateUserAsync(string userId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_activate";

            // Przygotowanie obiektu historii operacji
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.UserActivated,
                TargetEntityType = nameof(User),
                TargetEntityId = userId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            User? user = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie aktywacji użytkownika ID: {UserId} przez {User}", userId, currentUserUpn);

                // Pobierz użytkownika
                user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    operation.MarkAsFailed($"Użytkownik o ID '{userId}' nie został znaleziony.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można aktywować użytkownika: Użytkownik o ID {UserId} nie istnieje.", userId);
                    return false;
                }
                operation.TargetEntityName = user.FullName;

                // Sprawdź czy użytkownik nie jest już aktywny
                if (user.IsActive)
                {
                    operation.MarkAsCompleted("Użytkownik był już aktywny.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogInformation("Użytkownik o ID {UserId} był już aktywny.", userId);

                    // Inwalidacja cache'a dla spójności
                    InvalidateCache(user.Id, user.UPN, user.Role, allUsersAffected: true, specificRoleAffected: true);
                    _cache.Remove(AllActiveUsersCacheKey); // Dla spójności z DeactivateUserAsync
                    return true;
                }

                // Aktywuj użytkownika
                user.IsActive = true;
                user.MarkAsModified(currentUserUpn);
                _userRepository.Update(user);
                // SaveChangesAsync na wyższym poziomie

                // Aktualizacja historii operacji
                operation.MarkAsCompleted("Użytkownik aktywowany.");
                _logger.LogInformation("Użytkownik ID: {UserId} pomyślnie aktywowany.", userId);

                // Inwalidacja cache'a - użytkownik został aktywowany
                InvalidateCache(user.Id, user.UPN, user.Role, allUsersAffected: true, specificRoleAffected: true);
                _cache.Remove(AllActiveUsersCacheKey); // Dla spójności z DeactivateUserAsync

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktywacji użytkownika ID {UserId}.", userId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<UserSchoolType?> AssignUserToSchoolTypeAsync(string userId, string schoolTypeId, DateTime assignedDate, DateTime? endDate = null, decimal? workloadPercentage = null, string? notes = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_assign_ust";

            // Przygotowanie obiektu historii operacji
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.UserSchoolTypeAssigned,
                TargetEntityType = nameof(UserSchoolType),
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            User? user = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie przypisania użytkownika {UserId} do typu szkoły {SchoolTypeId} przez {User}", userId, schoolTypeId, currentUserUpn);

                // Pobierz i zwaliduj użytkownika oraz typ szkoły
                user = await _userRepository.GetByIdAsync(userId);
                var schoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeId);

                if (user == null || !user.IsActive)
                {
                    operation.MarkAsFailed($"Użytkownik o ID '{userId}' nie został znaleziony lub jest nieaktywny.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można przypisać użytkownika do typu szkoły: Użytkownik o ID {UserId} nie istnieje lub jest nieaktywny.", userId);
                    return null;
                }
                if (schoolType == null || !schoolType.IsActive)
                {
                    operation.MarkAsFailed($"Typ szkoły o ID '{schoolTypeId}' nie został znaleziony lub jest nieaktywny.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można przypisać użytkownika do typu szkoły: Typ szkoły o ID {SchoolTypeId} nie istnieje lub jest nieaktywny.", schoolTypeId);
                    return null;
                }
                operation.TargetEntityName = $"Przypisanie {user.UPN} do {schoolType.ShortName}";

                // Sprawdź czy przypisanie już istnieje
                var existingAssignment = user.SchoolTypeAssignments.FirstOrDefault(ust => ust.SchoolTypeId == schoolTypeId && ust.IsActive && ust.IsCurrentlyActive);
                if (existingAssignment != null)
                {
                    _logger.LogWarning("Użytkownik {UserId} jest już aktywnie przypisany do typu szkoły {SchoolTypeId}.", userId, schoolTypeId);
                    operation.MarkAsFailed("Użytkownik już aktywnie przypisany do tego typu szkoły.");
                    await SaveOperationHistoryAsync(operation);
                    return existingAssignment;
                }

                // Utworzenie nowego przypisania
                var newUserSchoolType = new UserSchoolType
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    User = user,
                    SchoolTypeId = schoolTypeId,
                    SchoolType = schoolType,
                    AssignedDate = assignedDate,
                    EndDate = endDate,
                    WorkloadPercentage = workloadPercentage,
                    Notes = notes,
                    IsCurrentlyActive = true,
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };

                await _userSchoolTypeRepository.AddAsync(newUserSchoolType);
                // SaveChangesAsync() na wyższym poziomie

                // Aktualizacja historii operacji
                operation.TargetEntityId = newUserSchoolType.Id;
                operation.MarkAsCompleted($"Przypisano użytkownika {userId} do typu szkoły {schoolTypeId}.");
                _logger.LogInformation("Użytkownik {UserId} pomyślnie przypisany do typu szkoły {SchoolTypeId}.", userId, schoolTypeId);

                // Inwalidacja cache'a użytkownika (jego przypisania się zmieniły)
                InvalidateCache(userId, user.UPN);

                return newUserSchoolType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas przypisania użytkownika ID {UserId} do typu szkoły ID {SchoolTypeId}.", userId, schoolTypeId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return null;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveUserFromSchoolTypeAsync(string userSchoolTypeId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_ust";

            // Przygotowanie obiektu historii operacji
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.UserSchoolTypeRemoved,
                TargetEntityType = nameof(UserSchoolType),
                TargetEntityId = userSchoolTypeId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            UserSchoolType? assignment = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie usuwania przypisania UserSchoolType ID: {UserSchoolTypeId} przez {User}", userSchoolTypeId, currentUserUpn);

                // Pobierz przypisanie
                assignment = await _userSchoolTypeRepository.GetByIdAsync(userSchoolTypeId);
                if (assignment == null)
                {
                    operation.MarkAsFailed($"Przypisanie o ID '{userSchoolTypeId}' nie zostało znalezione.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można usunąć przypisania: Przypisanie o ID {UserSchoolTypeId} nie istnieje.", userSchoolTypeId);
                    return false;
                }

                // Pobierz powiązane obiekty dla celów logowania i cache'a
                var user = await _userRepository.GetByIdAsync(assignment.UserId);
                var schoolType = await _schoolTypeRepository.GetByIdAsync(assignment.SchoolTypeId);
                operation.TargetEntityName = $"Przypisanie {user?.UPN ?? assignment.UserId} do {schoolType?.ShortName ?? assignment.SchoolTypeId}";

                // Oznacz przypisanie jako usunięte
                assignment.MarkAsDeleted(currentUserUpn);
                _userSchoolTypeRepository.Update(assignment);
                // SaveChangesAsync na wyższym poziomie

                // Aktualizacja historii operacji
                operation.MarkAsCompleted($"Usunięto przypisanie UserSchoolType ID: {userSchoolTypeId}.");
                _logger.LogInformation("Przypisanie UserSchoolType ID: {UserSchoolTypeId} pomyślnie usunięte.", userSchoolTypeId);

                // Inwalidacja cache'a użytkownika (jego przypisania się zmieniły)
                if (user != null)
                {
                    InvalidateCache(user.Id, user.UPN);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania przypisania UserSchoolType ID {UserSchoolTypeId}.", userSchoolTypeId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<UserSubject?> AssignTeacherToSubjectAsync(string teacherId, string subjectId, DateTime assignedDate, string? notes = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_assign_usubj";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.UserSubjectAssigned,
                TargetEntityType = nameof(UserSubject),
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            User? teacher = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie przypisania nauczyciela {TeacherId} do przedmiotu {SubjectId} przez {User}", teacherId, subjectId, currentUserUpn);
                teacher = await _userRepository.GetByIdAsync(teacherId);
                var subject = await _subjectRepository.GetByIdAsync(subjectId);

                if (teacher == null || !teacher.IsActive || (teacher.Role != UserRole.Nauczyciel && teacher.Role != UserRole.Wicedyrektor && teacher.Role != UserRole.Dyrektor))
                {
                    operation.MarkAsFailed($"Użytkownik o ID '{teacherId}' nie został znaleziony, jest nieaktywny lub nie ma uprawnień do nauczania.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można przypisać nauczyciela do przedmiotu: Użytkownik o ID {TeacherId} nie istnieje, jest nieaktywny lub nie ma odpowiedniej roli.", teacherId);
                    return null;
                }
                if (subject == null || !subject.IsActive)
                {
                    operation.MarkAsFailed($"Przedmiot o ID '{subjectId}' nie został znaleziony lub jest nieaktywny.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można przypisać nauczyciela do przedmiotu: Przedmiot o ID {SubjectId} nie istnieje lub jest nieaktywny.", subjectId);
                    return null;
                }
                operation.TargetEntityName = $"Przypisanie {teacher.UPN} do {subject.Name}";

                var existingAssignment = teacher.TaughtSubjects.FirstOrDefault(us => us.SubjectId == subjectId && us.IsActive);
                if (existingAssignment != null)
                {
                    _logger.LogWarning("Nauczyciel {TeacherId} jest już przypisany do przedmiotu {SubjectId}.", teacherId, subjectId);
                    operation.MarkAsFailed("Nauczyciel już przypisany do tego przedmiotu.");
                    await SaveOperationHistoryAsync(operation);
                    return existingAssignment;
                }

                var newUserSubject = new UserSubject
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = teacherId,
                    User = teacher,
                    SubjectId = subjectId,
                    Subject = subject,
                    AssignedDate = assignedDate,
                    Notes = notes,
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };
                await _userSubjectRepository.AddAsync(newUserSubject);

                operation.TargetEntityId = newUserSubject.Id;
                operation.MarkAsCompleted($"Przypisano nauczyciela {teacherId} do przedmiotu {subjectId}.");
                _logger.LogInformation("Nauczyciel {TeacherId} pomyślnie przypisany do przedmiotu {SubjectId}.", teacherId, subjectId);

                InvalidateCache(teacherId, teacher.UPN);
                // NOWE: Inwalidacja cache dla listy nauczycieli tego przedmiotu
                await _subjectService.InvalidateTeachersCacheForSubjectAsync(subjectId);
                _logger.LogDebug("Zainicjowano inwalidację cache listy nauczycieli dla przedmiotu ID: {SubjectId} po przypisaniu nauczyciela.", subjectId);

                return newUserSubject;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas przypisania nauczyciela ID {TeacherId} do przedmiotu ID {SubjectId}.", teacherId, subjectId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return null;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveTeacherFromSubjectAsync(string userSubjectId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_usubj";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.UserSubjectRemoved,
                TargetEntityType = nameof(UserSubject),
                TargetEntityId = userSubjectId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            UserSubject? assignment = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie usuwania przypisania UserSubject ID: {UserSubjectId} przez {User}", userSubjectId, currentUserUpn);
                assignment = await _userSubjectRepository.GetByIdAsync(userSubjectId);
                if (assignment == null)
                {
                    operation.MarkAsFailed($"Przypisanie o ID '{userSubjectId}' nie zostało znalezione.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można usunąć przypisania: Przypisanie o ID {UserSubjectId} nie istnieje.", userSubjectId);
                    return false;
                }

                var user = await _userRepository.GetByIdAsync(assignment.UserId);
                var subject = await _subjectRepository.GetByIdAsync(assignment.SubjectId);
                operation.TargetEntityName = $"Przypisanie {user?.UPN ?? assignment.UserId} do {subject?.Name ?? assignment.SubjectId}";

                var subjectIdToInvalidate = assignment.SubjectId; // Zapisz ID przedmiotu przed usunięciem przypisania

                assignment.MarkAsDeleted(currentUserUpn);
                _userSubjectRepository.Update(assignment);

                operation.MarkAsCompleted($"Usunięto przypisanie UserSubject ID: {userSubjectId}.");
                _logger.LogInformation("Przypisanie UserSubject ID: {UserSubjectId} pomyślnie usunięte.", userSubjectId);

                if (user != null)
                {
                    InvalidateCache(user.Id, user.UPN);
                }
                // NOWE: Inwalidacja cache dla listy nauczycieli tego przedmiotu
                await _subjectService.InvalidateTeachersCacheForSubjectAsync(subjectIdToInvalidate);
                _logger.LogDebug("Zainicjowano inwalidację cache listy nauczycieli dla przedmiotu ID: {SubjectId} po usunięciu przypisania nauczyciela.", subjectIdToInvalidate);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania przypisania UserSubject ID {UserSubjectId}.", userSubjectId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a użytkowników.");
            InvalidateCache(invalidateAll: true);
            _logger.LogInformation("Cache użytkowników został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        private void InvalidateCache(string? userId = null, string? upn = null, UserRole? role = null, bool allUsersAffected = false, bool specificRoleAffected = false, bool invalidateAll = false)
        {
            _logger.LogDebug("Inwalidacja cache'u użytkowników. userId: {UserId}, upn: {UPN}, role: {Role}, allUsersAffected: {AllUsersAffected}, specificRoleAffected: {SpecificRoleAffected}, invalidateAll: {InvalidateAll}",
                userId, upn, role, allUsersAffected, specificRoleAffected, invalidateAll);

            var oldTokenSource = Interlocked.Exchange(ref _usersCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
            _logger.LogDebug("Token cache'u dla użytkowników został zresetowany.");

            if (invalidateAll)
            {
                _cache.Remove(AllActiveUsersCacheKey);
                _logger.LogDebug("Usunięto z cache klucz dla wszystkich aktywnych użytkowników.");

                if (!string.IsNullOrWhiteSpace(userId)) _cache.Remove(UserByIdCacheKeyPrefix + userId);
                if (!string.IsNullOrWhiteSpace(upn)) _cache.Remove(UserByUpnCacheKeyPrefix + upn);
                if (role.HasValue) _cache.Remove(UsersByRoleCacheKeyPrefix + role.Value.ToString());
                return;
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                _cache.Remove(UserByIdCacheKeyPrefix + userId);
                _logger.LogDebug("Usunięto z cache użytkownika o ID: {UserId}", userId);
            }

            if (!string.IsNullOrWhiteSpace(upn))
            {
                _cache.Remove(UserByUpnCacheKeyPrefix + upn);
                _logger.LogDebug("Usunięto z cache użytkownika o UPN: {UPN}", upn);
            }

            if (specificRoleAffected && role.HasValue)
            {
                _cache.Remove(UsersByRoleCacheKeyPrefix + role.Value.ToString());
                _logger.LogDebug("Usunięto z cache użytkowników dla roli: {Role}", role.Value);
            }

            if (allUsersAffected)
            {
                _cache.Remove(AllActiveUsersCacheKey);
                _logger.LogDebug("Usunięto z cache klucz dla wszystkich aktywnych użytkowników z powodu flagi allUsersAffected.");
            }
        }

        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id))
                operation.Id = Guid.NewGuid().ToString();
            if (string.IsNullOrEmpty(operation.CreatedBy))
                operation.CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system_log_save";

            if (operation.StartedAt == default(DateTime) &&
                (operation.Status == OperationStatus.InProgress || operation.Status == OperationStatus.Pending || operation.Status == OperationStatus.Completed || operation.Status == OperationStatus.Failed))
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