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

        // Klucze cache
        private const string AllActiveUsersCacheKey = "Users_AllActive";
        private const string UserByIdCacheKeyPrefix = "User_Id_";
        private const string UserByUpnCacheKeyPrefix = "User_Upn_";
        private const string UsersByRoleCacheKeyPrefix = "Users_Role_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(15);

        // Token do unieważniania cache'u dla użytkowników
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
            IGenericRepository<Subject> subjectRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<UserService> logger,
            IMemoryCache memoryCache)
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
        }

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_usersCacheTokenSource.Token));
        }

        /// <inheritdoc />
        public async Task<User?> GetUserByIdAsync(string userId, bool forceRefresh = false)
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
            // Repozytorium UserRepository.GetByIdAsync powinno już dołączać niezbędne dane (Department, TeamMemberships itp.)
            var userFromDb = await _userRepository.GetByIdAsync(userId);

            if (userFromDb != null)
            {
                _cache.Set(cacheKey, userFromDb, GetDefaultCacheEntryOptions());
                // Dodatkowo cache'ujemy pod kluczem UPN, jeśli jeszcze nie ma, dla spójności
                string upnCacheKey = UserByUpnCacheKeyPrefix + userFromDb.UPN;
                if (!_cache.TryGetValue(upnCacheKey, out User? _)) // Sprawdzamy czy już nie ma, żeby nie nadpisywać bez potrzeby jeśli był z innego źródła
                {
                    _cache.Set(upnCacheKey, userFromDb, GetDefaultCacheEntryOptions());
                }
                _logger.LogDebug("Użytkownik ID: {UserId} (UPN: {UPN}) dodany do cache.", userId, userFromDb.UPN);
            }
            else
            {
                _cache.Remove(cacheKey); // Usuń, jeśli nie znaleziono
            }
            return userFromDb;
        }

        /// <inheritdoc />
        public async Task<User?> GetUserByUpnAsync(string upn, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie użytkownika o UPN: {UPN}. Wymuszenie odświeżenia: {ForceRefresh}", upn, forceRefresh);
            if (string.IsNullOrWhiteSpace(upn))
            {
                _logger.LogWarning("Próba pobrania użytkownika z pustym UPN.");
                return null;
            }

            string cacheKey = UserByUpnCacheKeyPrefix + upn;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out User? cachedUser))
            {
                _logger.LogDebug("Użytkownik UPN: {UPN} znaleziony w cache.", upn);
                return cachedUser;
            }

            _logger.LogDebug("Użytkownik UPN: {UPN} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", upn);
            // Repozytorium UserRepository.GetUserByUpnAsync może nie dołączać wszystkich danych tak jak GetByIdAsync.
            // Aby zapewnić spójność cache'owanego obiektu, po pobraniu przez UPN, wołamy GetByIdAsync.
            var userFromDbBase = await _userRepository.GetUserByUpnAsync(upn);
            if (userFromDbBase == null)
            {
                _cache.Remove(cacheKey); // Usuń, jeśli nie znaleziono
                return null;
            }

            // Pobierz pełny obiekt przez ID, aby mieć spójne dane w cache (z zależnościami)
            var userFromDbFull = await GetUserByIdAsync(userFromDbBase.Id, forceRefresh: true); // forceRefresh tutaj, aby na pewno pobrać z DB i zaktualizować cache ID

            if (userFromDbFull != null)
            {
                // Cache'ujemy pełny obiekt pod kluczem UPN
                _cache.Set(cacheKey, userFromDbFull, GetDefaultCacheEntryOptions());
                _logger.LogDebug("Użytkownik UPN: {UPN} (ID: {UserId}) dodany do cache.", upn, userFromDbFull.Id);
            }
            // Jeśli userFromDbFull jest null (co nie powinno się zdarzyć, jeśli userFromDbBase istniał), cache UPN zostanie obsłużony przez GetUserByIdAsync

            return userFromDbFull;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<User>> GetAllActiveUsersAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych użytkowników. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);

            if (!forceRefresh && _cache.TryGetValue(AllActiveUsersCacheKey, out IEnumerable<User>? cachedUsers) && cachedUsers != null)
            {
                _logger.LogDebug("Wszyscy aktywni użytkownicy znalezieni w cache.");
                return cachedUsers;
            }

            _logger.LogDebug("Wszyscy aktywni użytkownicy nie znalezieni w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var usersFromDb = await _userRepository.FindAsync(u => u.IsActive);

            // Potencjalnie można tu dociągnąć zależności dla każdego użytkownika, jeśli GetAllAsync ich nie ładuje,
            // ale może to być kosztowne dla dużej liczby użytkowników.
            // Zwykle listy nie zawierają pełnych obiektów z wszystkimi zależnościami.

            _cache.Set(AllActiveUsersCacheKey, usersFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Wszyscy aktywni użytkownicy dodani do cache.");
            return usersFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie użytkowników o roli: {Role}. Wymuszenie odświeżenia: {ForceRefresh}", role, forceRefresh);
            string cacheKey = UsersByRoleCacheKeyPrefix + role.ToString();

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<User>? cachedUsers) && cachedUsers != null)
            {
                _logger.LogDebug("Użytkownicy o roli {Role} znalezieni w cache.", role);
                return cachedUsers;
            }

            _logger.LogDebug("Użytkownicy o roli {Role} nie znalezieni w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", role);
            var usersFromDb = await _userRepository.GetUsersByRoleAsync(role); // Metoda repozytorium już filtruje po IsActive

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

                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(upn))
                {
                    operation.MarkAsFailed("Imię, nazwisko i UPN są wymagane.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można utworzyć użytkownika: Imię, nazwisko lub UPN są puste.");
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

                // TODO: PowerShellService call - Utworzenie użytkownika w Microsoft 365
                bool psSuccess = true;

                if (psSuccess)
                {
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

                    operation.TargetEntityId = newUser.Id;
                    operation.MarkAsCompleted($"Użytkownik ID: {newUser.Id} przygotowany do utworzenia.");
                    _logger.LogInformation("Użytkownik {FirstName} {LastName} ({UPN}) pomyślnie przygotowany do zapisu. ID: {UserId}", firstName, lastName, upn, newUser.Id);

                    InvalidateCache(newUser.Id, newUser.UPN, newUser.Role, allUsersAffected: true, specificRoleAffected: true);

                    if (sendWelcomeEmail)
                    {
                        _logger.LogInformation("TODO: Wysłanie emaila powitalnego do {UPN}", upn);
                    }
                    return newUser;
                }
                else
                {
                    operation.MarkAsFailed("Nie udało się utworzyć użytkownika w systemie zewnętrznym (np. M365).");
                    _logger.LogError("Błąd tworzenia użytkownika {UPN} w systemie zewnętrznym.", upn);
                    await SaveOperationHistoryAsync(operation);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia użytkownika {UPN}.", upn);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateUserAsync(User userToUpdate)
        {
            if (userToUpdate == null || string.IsNullOrWhiteSpace(userToUpdate.Id))
            {
                _logger.LogError("Próba aktualizacji użytkownika z nieprawidłowymi danymi (null lub brak ID).");
                throw new ArgumentNullException(nameof(userToUpdate));
            }


            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.UserUpdated,
                TargetEntityType = nameof(User),
                TargetEntityId = userToUpdate.Id,
                TargetEntityName = $"{userToUpdate.FirstName} {userToUpdate.LastName} ({userToUpdate.UPN})",
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            string? oldUpn = null;
            UserRole? oldRole = null;

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
                operation.TargetEntityName = $"{existingUser.FirstName} {existingUser.LastName} ({existingUser.UPN})"; // Nazwa przed modyfikacją
                oldUpn = existingUser.UPN;
                oldRole = existingUser.Role;

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

                // Mapowanie właściwości - można użyć AutoMappera lub mapować ręcznie
                existingUser.FirstName = userToUpdate.FirstName;
                existingUser.LastName = userToUpdate.LastName;
                existingUser.UPN = userToUpdate.UPN;
                existingUser.Role = userToUpdate.Role;
                existingUser.DepartmentId = userToUpdate.DepartmentId; // Należy upewnić się, że Department jest załadowany, jeśli logika na nim polega
                existingUser.Phone = userToUpdate.Phone;
                existingUser.AlternateEmail = userToUpdate.AlternateEmail;
                existingUser.ExternalId = userToUpdate.ExternalId;
                existingUser.BirthDate = userToUpdate.BirthDate;
                existingUser.EmploymentDate = userToUpdate.EmploymentDate;
                existingUser.Position = userToUpdate.Position;
                existingUser.Notes = userToUpdate.Notes;
                existingUser.IsSystemAdmin = userToUpdate.IsSystemAdmin;
                // IsActive jest zarządzane przez DeactivateUserAsync/ActivateUserAsync
                // existingUser.IsActive = userToUpdate.IsActive; 

                existingUser.MarkAsModified(currentUserUpn);
                _userRepository.Update(existingUser);

                operation.TargetEntityName = $"{existingUser.FirstName} {existingUser.LastName} ({existingUser.UPN})"; // Nazwa po modyfikacji
                operation.MarkAsCompleted("Użytkownik przygotowany do aktualizacji.");
                await SaveOperationHistoryAsync(operation);
                _logger.LogInformation("Użytkownik ID: {UserId} pomyślnie zaktualizowany.", userToUpdate.Id);

                InvalidateCache(existingUser.Id, existingUser.UPN, existingUser.Role, allUsersAffected: true, specificRoleAffected: true);
                if (oldUpn != null && oldUpn != existingUser.UPN) InvalidateCache(upn: oldUpn);
                if (oldRole.HasValue && oldRole.Value != existingUser.Role) InvalidateCache(role: oldRole.Value, specificRoleAffected: true);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji użytkownika ID {UserId}.", userToUpdate.Id);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeactivateUserAsync(string userId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_deactivate";
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
                user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    operation.MarkAsFailed($"Użytkownik o ID '{userId}' nie został znaleziony.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można zdezaktywować użytkownika: Użytkownik o ID {UserId} nie istnieje.", userId);
                    return false;
                }
                operation.TargetEntityName = user.FullName;

                if (!user.IsActive)
                {
                    operation.MarkAsFailed($"Użytkownik o ID '{userId}' jest już nieaktywny.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogWarning("Użytkownik o ID {UserId} jest już nieaktywny.", userId);
                    InvalidateCache(user.Id, user.UPN, user.Role, allUsersAffected: true, specificRoleAffected: true); // Mimo wszystko odśwież cache
                    return false;
                }

                // TODO: PowerShellService call - Disable M365 account if needed
                bool psSuccess = true;

                if (psSuccess)
                {
                    user.MarkAsDeleted(currentUserUpn); // Ustawia IsActive = false
                    _userRepository.Update(user);

                    operation.MarkAsCompleted("Użytkownik zdezaktywowany.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogInformation("Użytkownik ID: {UserId} pomyślnie zdezaktywowany.", userId);
                    InvalidateCache(user.Id, user.UPN, user.Role, allUsersAffected: true, specificRoleAffected: true);
                    return true;
                }
                else
                {
                    operation.MarkAsFailed("Nie udało się zdezaktywować użytkownika w systemie zewnętrznym.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Błąd dezaktywacji użytkownika ID: {UserId} w systemie zewnętrznym.", userId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas dezaktywacji użytkownika ID {UserId}.", userId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ActivateUserAsync(string userId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_activate";
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
                user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    operation.MarkAsFailed($"Użytkownik o ID '{userId}' nie został znaleziony.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można aktywować użytkownika: Użytkownik o ID {UserId} nie istnieje.", userId);
                    return false;
                }
                operation.TargetEntityName = user.FullName;

                if (user.IsActive)
                {
                    operation.MarkAsCompleted("Użytkownik był już aktywny.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogInformation("Użytkownik o ID {UserId} był już aktywny.", userId);
                    InvalidateCache(user.Id, user.UPN, user.Role, allUsersAffected: true, specificRoleAffected: true); // Mimo wszystko odśwież cache
                    return true;
                }

                // TODO: PowerShellService call - Enable M365 account if needed
                bool psSuccess = true;

                if (psSuccess)
                {
                    user.IsActive = true;
                    user.MarkAsModified(currentUserUpn);
                    _userRepository.Update(user);

                    operation.MarkAsCompleted("Użytkownik aktywowany.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogInformation("Użytkownik ID: {UserId} pomyślnie aktywowany.", userId);
                    InvalidateCache(user.Id, user.UPN, user.Role, allUsersAffected: true, specificRoleAffected: true);
                    return true;
                }
                else
                {
                    operation.MarkAsFailed("Nie udało się aktywować użytkownika w systemie zewnętrznym.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Błąd aktywacji użytkownika ID: {UserId} w systemie zewnętrznym.", userId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktywacji użytkownika ID {UserId}.", userId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<UserSchoolType?> AssignUserToSchoolTypeAsync(string userId, string schoolTypeId, DateTime assignedDate, DateTime? endDate = null, decimal? workloadPercentage = null, string? notes = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_assign_ust";
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
                user = await _userRepository.GetByIdAsync(userId); // Powinno załadować SchoolTypeAssignments
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

                var existingAssignment = user.SchoolTypeAssignments.FirstOrDefault(ust => ust.SchoolTypeId == schoolTypeId && ust.IsActive && ust.IsCurrentlyActive);
                if (existingAssignment != null)
                {
                    _logger.LogWarning("Użytkownik {UserId} jest już aktywnie przypisany do typu szkoły {SchoolTypeId}.", userId, schoolTypeId);
                    operation.MarkAsFailed("Użytkownik już aktywnie przypisany do tego typu szkoły.");
                    await SaveOperationHistoryAsync(operation);
                    return existingAssignment;
                }

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
                    IsCurrentlyActive = true, // Domyślnie aktywne
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };

                await _userSchoolTypeRepository.AddAsync(newUserSchoolType);
                // user.SchoolTypeAssignments.Add(newUserSchoolType); // Jeśli relacja jest zarządzana przez EF Core
                // _userRepository.Update(user);

                operation.TargetEntityId = newUserSchoolType.Id;
                operation.MarkAsCompleted($"Przypisano użytkownika {userId} do typu szkoły {schoolTypeId}.");
                await SaveOperationHistoryAsync(operation);
                _logger.LogInformation("Użytkownik {UserId} pomyślnie przypisany do typu szkoły {SchoolTypeId}.", userId, schoolTypeId);

                InvalidateCache(userId, user.UPN); // Unieważnij cache dla tego użytkownika
                return newUserSchoolType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas przypisania użytkownika ID {UserId} do typu szkoły ID {SchoolTypeId}.", userId, schoolTypeId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveUserFromSchoolTypeAsync(string userSchoolTypeId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_ust";
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
                assignment = await _userSchoolTypeRepository.GetByIdAsync(userSchoolTypeId);
                if (assignment == null)
                {
                    operation.MarkAsFailed($"Przypisanie o ID '{userSchoolTypeId}' nie zostało znalezione.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie można usunąć przypisania: Przypisanie o ID {UserSchoolTypeId} nie istnieje.", userSchoolTypeId);
                    return false;
                }
                operation.TargetEntityName = $"Przypisanie {assignment.UserId} do {assignment.SchoolTypeId}";

                assignment.MarkAsDeleted(currentUserUpn); // Ustawia IsActive = false
                _userSchoolTypeRepository.Update(assignment);

                operation.MarkAsCompleted($"Usunięto przypisanie UserSchoolType ID: {userSchoolTypeId}.");
                await SaveOperationHistoryAsync(operation);
                _logger.LogInformation("Przypisanie UserSchoolType ID: {UserSchoolTypeId} pomyślnie usunięte.", userSchoolTypeId);

                // Pobierz UPN użytkownika, którego dotyczyło przypisanie
                var user = await _userRepository.GetByIdAsync(assignment.UserId);
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
                await SaveOperationHistoryAsync(operation);
                return false;
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
                teacher = await _userRepository.GetByIdAsync(teacherId); // Powinno załadować TaughtSubjects
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
                // teacher.TaughtSubjects.Add(newUserSubject); // Jeśli relacja jest zarządzana przez EF Core
                // _userRepository.Update(teacher);

                operation.TargetEntityId = newUserSubject.Id;
                operation.MarkAsCompleted($"Przypisano nauczyciela {teacherId} do przedmiotu {subjectId}.");
                await SaveOperationHistoryAsync(operation);
                _logger.LogInformation("Nauczyciel {TeacherId} pomyślnie przypisany do przedmiotu {SubjectId}.", teacherId, subjectId);

                InvalidateCache(teacherId, teacher.UPN);
                // Należy również rozważyć inwalidację cache dla SubjectService.GetTeachersForSubjectAsync(subjectId)
                // TODO: Rozważyć mechanizm inwalidacji między-serwisowej lub przenieść logikę zarządzania UserSubject do SubjectService
                _logger.LogWarning("TODO: Należy zaimplementować inwalidację cache dla listy nauczycieli przedmiotu (SubjectService) po przypisaniu nauczyciela.");

                return newUserSubject;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas przypisania nauczyciela ID {TeacherId} do przedmiotu ID {SubjectId}.", teacherId, subjectId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return null;
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
                // Dostęp do User.UPN i Subject.Name po załadowaniu assignment.User i assignment.Subject
                var user = await _userRepository.GetByIdAsync(assignment.UserId);
                var subject = await _subjectRepository.GetByIdAsync(assignment.SubjectId);
                operation.TargetEntityName = $"Przypisanie {user?.UPN ?? assignment.UserId} do {subject?.Name ?? assignment.SubjectId}";


                assignment.MarkAsDeleted(currentUserUpn); // Ustawia IsActive = false
                _userSubjectRepository.Update(assignment);

                operation.MarkAsCompleted($"Usunięto przypisanie UserSubject ID: {userSubjectId}.");
                await SaveOperationHistoryAsync(operation);
                _logger.LogInformation("Przypisanie UserSubject ID: {UserSubjectId} pomyślnie usunięte.", userSubjectId);

                if (user != null)
                {
                    InvalidateCache(user.Id, user.UPN);
                }
                // Należy również rozważyć inwalidację cache dla SubjectService.GetTeachersForSubjectAsync(assignment.SubjectId)
                _logger.LogWarning("TODO: Należy zaimplementować inwalidację cache dla listy nauczycieli przedmiotu (SubjectService) po usunięciu przypisania nauczyciela.");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania przypisania UserSubject ID {UserSubjectId}.", userSubjectId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return false;
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

        // Prywatna metoda do unieważniania cache.
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
                // Przy pełnej inwalidacji można by też usunąć wszystkie klucze po rolach,
                // ale reset tokenu powinien o to zadbać.
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
            else if (allUsersAffected && !invalidateAll) // Jeśli nie było globalnej inwalidacji, a dotyczy wszystkich
            {
                _cache.Remove(AllActiveUsersCacheKey);
                _logger.LogDebug("Usunięto z cache klucz dla wszystkich aktywnych użytkowników z powodu flagi allUsersAffected.");
                // Można by iterować po wszystkich rolach i usuwać UsersByRoleCacheKeyPrefix + r.ToString()
                // ale reset tokenu powinien być wystarczający.
            }
        }

        // Metoda pomocnicza do zapisu OperationHistory
        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id)) operation.Id = Guid.NewGuid().ToString();
            if (string.IsNullOrEmpty(operation.CreatedBy))
                operation.CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system_log_save";

            var existingLog = await _operationHistoryRepository.GetByIdAsync(operation.Id);
            if (existingLog == null)
            {
                await _operationHistoryRepository.AddAsync(operation);
            }
            else
            {
                existingLog.Status = operation.Status;
                existingLog.CompletedAt = operation.CompletedAt;
                existingLog.Duration = operation.Duration;
                existingLog.ErrorMessage = operation.ErrorMessage;
                existingLog.ErrorStackTrace = operation.ErrorStackTrace;
                existingLog.OperationDetails = operation.OperationDetails;
                existingLog.TargetEntityName = operation.TargetEntityName;
                existingLog.TargetEntityId = operation.TargetEntityId;
                existingLog.Type = operation.Type;
                existingLog.ProcessedItems = operation.ProcessedItems;
                existingLog.FailedItems = operation.FailedItems;
                existingLog.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_log_update");
                _operationHistoryRepository.Update(existingLog);
            }
        }
    }
}