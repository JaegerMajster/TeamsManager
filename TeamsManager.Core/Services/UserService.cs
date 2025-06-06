﻿// Plik: TeamsManager.Core/Services/UserService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Abstractions.Services.Synchronization;
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
        private readonly ISubjectService _subjectService;
        private readonly IPowerShellService _powerShellService;
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly IPowerShellCacheService _powerShellCacheService;
        private readonly INotificationService _notificationService;
        private readonly IAdminNotificationService _adminNotificationService;
        private readonly IGraphSynchronizer<User> _userSynchronizer;
        private readonly IUnitOfWork _unitOfWork;

        // Definicje kluczy cache
        private const string AllActiveUsersCacheKey = "Users_AllActive";
        private const string UserByIdCacheKeyPrefix = "User_Id_";
        private const string UserByUpnCacheKeyPrefix = "User_Upn_";
        private const string UsersByRoleCacheKeyPrefix = "Users_Role_";

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
            ISubjectService subjectService,
            IPowerShellService powerShellService,
            IOperationHistoryService operationHistoryService,
            IPowerShellCacheService powerShellCacheService,
            INotificationService notificationService,
            IAdminNotificationService adminNotificationService,
            IGraphSynchronizer<User> userSynchronizer,
            IUnitOfWork unitOfWork)
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
            _subjectService = subjectService ?? throw new ArgumentNullException(nameof(subjectService));
            _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _powerShellCacheService = powerShellCacheService ?? throw new ArgumentNullException(nameof(powerShellCacheService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _adminNotificationService = adminNotificationService ?? throw new ArgumentNullException(nameof(adminNotificationService));
            _userSynchronizer = userSynchronizer ?? throw new ArgumentNullException(nameof(userSynchronizer));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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

            if (!forceRefresh && _powerShellCacheService.TryGetValue(cacheKey, out User? cachedUser))
            {
                _logger.LogDebug("Użytkownik ID: {UserId} znaleziony w cache.", userId);
                return cachedUser;
            }
            _logger.LogDebug("Użytkownik ID: {UserId} nie znaleziony w cache lub wymuszono odświeżenie.", userId);

            var userFromDb = await _userRepository.GetByIdAsync(userId);
            
            // NOWA LOGIKA: Synchronizacja z Graph jeśli mamy token
            if (!string.IsNullOrEmpty(apiAccessToken) && (forceRefresh || userFromDb == null))
            {
                try
                {
                    _logger.LogInformation("Próba pobrania użytkownika {UserId} z Microsoft Graph", userId);
                    
                    // Pobierz dane z Graph
                    var psUser = await _powerShellService.ExecuteWithAutoConnectAsync(
                        apiAccessToken,
                        async () => await _powerShellService.Users.GetM365UserByIdAsync(userId),
                        $"GetM365UserByIdAsync dla ID: {userId}"
                    );

                    if (psUser != null)
                    {
                        // Synchronizuj z lokalną bazą
                        if (await _userSynchronizer.RequiresSynchronizationAsync(psUser, userFromDb!))
                        {
                            _logger.LogInformation("Synchronizacja użytkownika {UserId} z Microsoft Graph", userId);
                            
                            await _unitOfWork.BeginTransactionAsync();
                            try
                            {
                                userFromDb = await _userSynchronizer.SynchronizeAsync(psUser, userFromDb);
                                
                                if (userFromDb.Id != userId && string.IsNullOrEmpty(userFromDb.Id))
                                {
                                    userFromDb.Id = userId; // Zachowaj lokalne ID
                                }
                                
                                if (string.IsNullOrEmpty(userFromDb.Id))
                                {
                                    await _unitOfWork.Users.AddAsync(userFromDb);
                                }
                                else
                                {
                                    _unitOfWork.Users.Update(userFromDb);
                                }
                                
                                await _unitOfWork.CommitAsync();
                                await _unitOfWork.CommitTransactionAsync();
                                
                                // Inwaliduj cache po synchronizacji
                                InvalidateUserCache(userId, userFromDb.UPN, invalidateAll: true);
                                
                                _logger.LogInformation("Synchronizacja użytkownika {UserId} zakończona pomyślnie", userId);
                            }
                            catch (Exception ex)
                            {
                                await _unitOfWork.RollbackAsync();
                                _logger.LogError(ex, "Błąd podczas synchronizacji użytkownika {UserId}", userId);
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas pobierania użytkownika {UserId} z Graph", userId);
                    // Kontynuuj z danymi lokalnymi jeśli są
                }
            }

            if (userFromDb != null && userFromDb.IsActive)
            {
                _powerShellCacheService.Set(cacheKey, userFromDb);
                _logger.LogDebug("Użytkownik ID: {UserId} dodany/zaktualizowany w cache po ID.", userId);
                if (!string.IsNullOrWhiteSpace(userFromDb.UPN))
                {
                    string upnCacheKey = UserByUpnCacheKeyPrefix + userFromDb.UPN;
                    _powerShellCacheService.Set(upnCacheKey, userFromDb);
                    _logger.LogDebug("Użytkownik (ID: {UserId}, UPN: {UPN}) zaktualizowany/dodany w cache po UPN.", userId, userFromDb.UPN);
                }
            }
            else if (userFromDb != null && !userFromDb.IsActive)
            {
                _logger.LogDebug("Użytkownik ID: {UserId} jest nieaktywny, nie zostanie zcache'owany.", userId);
                return null;
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

            if (!forceRefresh && _powerShellCacheService.TryGetValue(upnCacheKey, out User? cachedUser))
            {
                _logger.LogDebug("Użytkownik UPN: {UPN} znaleziony w cache.", upn);
                return cachedUser;
            }
            _logger.LogDebug("Użytkownik UPN: {UPN} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", upn);

            var userFromDbBase = await _userRepository.GetUserByUpnAsync(upn);
            if (userFromDbBase == null)
            {
                _logger.LogInformation("Nie znaleziono użytkownika o UPN: {UPN} w repozytorium.", upn);
                _powerShellCacheService.Set(upnCacheKey, (User?)null);
                return null;
            }
            var userFromDbFull = await GetUserByIdAsync(userFromDbBase.Id, forceRefresh: true, apiAccessToken: apiAccessToken); // ZMIANA: przekazanie apiAccessToken
            return userFromDbFull;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<User>> GetAllActiveUsersAsync(bool forceRefresh = false, string? apiAccessToken = null) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych użytkowników. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);
            if (!forceRefresh && _powerShellCacheService.TryGetValue(AllActiveUsersCacheKey, out IEnumerable<User>? cachedUsers) && cachedUsers != null)
            {
                _logger.LogDebug("Wszyscy aktywni użytkownicy znalezieni w cache.");
                return cachedUsers;
            }
            _logger.LogDebug("Wszyscy aktywni użytkownicy nie znalezieni w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");

            var usersFromDb = await _userRepository.FindAsync(u => u.IsActive);
            _powerShellCacheService.Set(AllActiveUsersCacheKey, usersFromDb);
            _logger.LogDebug("Wszyscy aktywni użytkownicy dodani do cache.");
            return usersFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role, bool forceRefresh = false, string? apiAccessToken = null) // ZMIANA: accessToken -> apiAccessToken
        {
            _logger.LogInformation("Pobieranie użytkowników o roli: {Role}. Wymuszenie odświeżenia: {ForceRefresh}", role, forceRefresh);
            string cacheKey = UsersByRoleCacheKeyPrefix + role.ToString();

            if (!forceRefresh && _powerShellCacheService.TryGetValue(cacheKey, out IEnumerable<User>? cachedUsers) && cachedUsers != null)
            {
                _logger.LogDebug("Użytkownicy o roli {Role} znalezieni w cache.", role);
                return cachedUsers;
            }
            _logger.LogDebug("Użytkownicy o roli {Role} nie znalezieni w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", role);

            var usersFromDb = await _userRepository.GetUsersByRoleAsync(role);
            _powerShellCacheService.Set(cacheKey, usersFromDb);
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
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_create";
            
            _logger.LogInformation("Rozpoczynanie tworzenia użytkownika {FirstName} {LastName} ({UPN}) w roli {Role}.", firstName, lastName, upn, role);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.UserCreated,
                nameof(User),
                targetEntityName: $"{firstName} {lastName} ({upn})"
            );

            try
            {
                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(upn) || string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogError("Nie można utworzyć użytkownika: Imię, nazwisko, UPN lub hasło są puste.");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Imię, nazwisko, UPN i hasło są wymagane."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się utworzyć użytkownika: Imię, nazwisko, UPN i hasło są wymagane.",
                        "error"
                    );
                    
                    return null;
                }
                var existingUser = await _userRepository.GetUserByUpnAsync(upn);
                if (existingUser != null)
                {
                    _logger.LogError("Nie można utworzyć użytkownika: Użytkownik o UPN {UPN} już istnieje.", upn);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Użytkownik o UPN '{upn}' już istnieje."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Nie udało się utworzyć użytkownika: UPN '{upn}' już istnieje.",
                        "error"
                    );
                    
                    return null;
                }
                var department = await _departmentRepository.GetByIdAsync(departmentId);
                if (department == null || !department.IsActive)
                {
                    _logger.LogError("Nie można utworzyć użytkownika: Dział o ID {DepartmentId} nie istnieje lub jest nieaktywny.", departmentId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Dział o ID '{departmentId}' nie istnieje lub jest nieaktywny."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się utworzyć użytkownika: Wybrany dział nie istnieje lub jest nieaktywny.",
                        "error"
                    );
                    
                    return null;
                }

                // Używamy ExecuteWithAutoConnectAsync dla utworzenia użytkownika w M365
                string? externalUserId = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _powerShellService.Users.CreateM365UserAsync($"{firstName} {lastName}", upn, password, accountEnabled: true),
                    $"Tworzenie użytkownika M365: {firstName} {lastName} ({upn})"
                );
                
                if (string.IsNullOrEmpty(externalUserId))
                {
                    _logger.LogError("Nie udało się utworzyć użytkownika {UPN} w Microsoft 365.", upn);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się utworzyć użytkownika w Microsoft 365."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Nie udało się utworzyć użytkownika {firstName} {lastName} w Microsoft 365.",
                        "error"
                    );
                    
                    return null;
                }

                var newUser = new User { /* ... inicjalizacja pól ... */ Id = Guid.NewGuid().ToString(), FirstName = firstName, LastName = lastName, UPN = upn, Role = role, DepartmentId = departmentId, Department = department, ExternalId = externalUserId, CreatedBy = currentUserUpn, IsActive = true };
                await _userRepository.AddAsync(newUser);
                _logger.LogInformation("Użytkownik {FirstName} {LastName} ({UPN}) pomyślnie utworzony. ID: {UserId}, External ID: {ExternalUserId}", firstName, lastName, upn, newUser.Id, externalUserId);
                InvalidateUserCache(userId: newUser.Id, upn: newUser.UPN, role: newUser.Role, invalidateAllGlobalLists: true);
                if (sendWelcomeEmail) { _logger.LogInformation("TODO: Wysłanie emaila powitalnego do {UPN}", upn); }
                
                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Użytkownik ID: {newUser.Id} utworzony lokalnie i w M365. External ID: {externalUserId}"
                );
                
                // Wysłanie powiadomienia o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Utworzono użytkownika: {newUser.FullName} ({newUser.UPN})",
                    "success"
                );
                
                // Powiadomienie do administratorów (asynchroniczne, nie blokuje operacji)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _adminNotificationService.SendUserCreatedNotificationAsync(
                            newUser.FullName,
                            newUser.UPN,
                            newUser.Role.ToString(),
                            currentUserUpn
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Błąd podczas wysyłania powiadomienia administratorskiego o utworzeniu użytkownika");
                    }
                });
                
                return newUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia użytkownika {UPN}.", upn);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn ?? "system",
                    $"Wystąpił błąd podczas tworzenia użytkownika: {ex.Message}",
                    "error"
                );
                
                // Powiadomienie o błędzie krytycznym do administratorów (asynchroniczne)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _adminNotificationService.SendCriticalErrorNotificationAsync(
                            "Tworzenie użytkownika",
                            ex.Message,
                            ex.StackTrace ?? "Brak informacji o stack trace",
                            $"Tworzenie użytkownika {firstName} {lastName} ({upn})",
                            currentUserUpn
                        );
                    }
                    catch (Exception notifEx)
                    {
                        _logger.LogError(notifEx, "Błąd podczas wysyłania powiadomienia o błędzie krytycznym");
                    }
                });
                
                return null;
            }
        }

        public async Task<bool> UpdateUserAsync(User userToUpdate, string apiAccessToken) // ZMIANA: accessToken -> apiAccessToken
        {
            if (userToUpdate == null || string.IsNullOrWhiteSpace(userToUpdate.Id))
            {
                _logger.LogError("Próba aktualizacji użytkownika z nieprawidłowymi danymi (null lub brak ID).");
                throw new ArgumentNullException(nameof(userToUpdate));
            }

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            _logger.LogInformation("Rozpoczynanie aktualizacji użytkownika ID: {UserId}", userToUpdate.Id);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.UserUpdated,
                nameof(User),
                targetEntityId: userToUpdate.Id
            );

            string? oldUpn = null; UserRole? oldRole = null; bool oldIsActive = false;

            try
            {
                var existingUser = await _userRepository.GetByIdAsync(userToUpdate.Id);
                if (existingUser == null)
                {
                    _logger.LogError("Nie można zaktualizować użytkownika: Użytkownik o ID {UserId} nie istnieje.", userToUpdate.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Użytkownik o ID '{userToUpdate.Id}' nie został znaleziony."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się zaktualizować użytkownika: Użytkownik nie został znaleziony.",
                        "error"
                    );
                    
                    return false;
                }
                oldUpn = existingUser.UPN; oldRole = existingUser.Role; oldIsActive = existingUser.IsActive;

                if (!string.Equals(existingUser.UPN, userToUpdate.UPN, StringComparison.OrdinalIgnoreCase))
                {
                    var userWithSameUpn = await _userRepository.GetUserByUpnAsync(userToUpdate.UPN);
                    if (userWithSameUpn != null && userWithSameUpn.Id != userToUpdate.Id)
                    {
                        _logger.LogError("Nie można zaktualizować użytkownika: UPN {UPN} już istnieje.", userToUpdate.UPN);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            $"UPN '{userToUpdate.UPN}' już istnieje w systemie."
                        );
                        
                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            $"Nie udało się zaktualizować użytkownika: UPN '{userToUpdate.UPN}' już istnieje.",
                            "error"
                        );
                        
                        return false;
                    }
                }
                var department = await _departmentRepository.GetByIdAsync(userToUpdate.DepartmentId);
                if (department == null || !department.IsActive)
                {
                    _logger.LogError("Nie można zaktualizować użytkownika: Dział o ID {DepartmentId} nie istnieje lub jest nieaktywny.", userToUpdate.DepartmentId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Dział o ID '{userToUpdate.DepartmentId}' nie istnieje lub jest nieaktywny."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się zaktualizować użytkownika: Wybrany dział nie istnieje lub jest nieaktywny.",
                        "error"
                    );
                    
                    return false;
                }

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

                // Aktualizacja użytkownika w M365 używając ExecuteWithAutoConnectAsync
                var m365UpdateSuccess = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () =>
                    {
                        // Aktualizuj podstawowe właściwości użytkownika
                        var updateResult = await _powerShellService.Users.UpdateM365UserPropertiesAsync(
                            existingUser.UPN,
                            department: department.Name,
                            jobTitle: existingUser.Position,
                            firstName: existingUser.FirstName,
                            lastName: existingUser.LastName
                        );

                        // Jeśli UPN się zmienił, zaktualizuj go
                        if (!string.Equals(oldUpn, existingUser.UPN, StringComparison.OrdinalIgnoreCase))
                        {
                            var upnUpdateResult = await _powerShellService.Users.UpdateM365UserPrincipalNameAsync(oldUpn!, existingUser.UPN);
                            return updateResult && upnUpdateResult;
                        }

                        return updateResult;
                    },
                    $"Update user {existingUser.UPN}"
                );

                if (!m365UpdateSuccess)
                {
                    _logger.LogError("Nie udało się zaktualizować użytkownika {UPN} w Microsoft 365.", existingUser.UPN);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się zaktualizować użytkownika w Microsoft 365."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Nie udało się zaktualizować użytkownika {existingUser.FullName} w Microsoft 365.",
                        "error"
                    );
                    
                    return false;
                }

                existingUser.MarkAsModified(currentUserUpn);
                _userRepository.Update(existingUser);
                _logger.LogInformation("Użytkownik ID: {UserId} pomyślnie zaktualizowany.", userToUpdate.Id);
                InvalidateUserCache(userId: existingUser.Id, upn: existingUser.UPN, role: existingUser.Role, oldUpnIfChanged: oldUpn, oldRoleIfChanged: oldRole, isActiveChanged: oldIsActive != existingUser.IsActive, invalidateAllGlobalLists: oldIsActive != existingUser.IsActive);
                
                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    "Użytkownik zaktualizowany lokalnie i w M365."
                );
                
                // Wysłanie powiadomienia o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Zaktualizowano użytkownika: {existingUser.FullName} ({existingUser.UPN})",
                    "success"
                );
                
                return true;
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji użytkownika ID {UserId}.", userToUpdate.Id); 
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn ?? "system",
                    $"Wystąpił błąd podczas aktualizacji użytkownika: {ex.Message}",
                    "error"
                );
                
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<UserSchoolType?> AssignUserToSchoolTypeAsync(string userId, string schoolTypeId, DateTime assignedDate, DateTime? endDate = null, decimal? workloadPercentage = null, string? notes = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_assign_ust";
            
            // 1. Inicjalizacja operacji historii
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.UserSchoolTypeAssigned,
                nameof(UserSchoolType)
            );
            
            User? user = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie przypisania użytkownika {UserId} do typu szkoły {SchoolTypeId} przez {User}", userId, schoolTypeId, currentUserUpn);
                
                user = await _userRepository.GetByIdAsync(userId);
                var schoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeId);
                
                if (user == null || !user.IsActive)
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Użytkownik o ID '{userId}' nie został znaleziony lub jest nieaktywny."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się przypisać użytkownika do typu szkoły.",
                        "error"
                    );
                    
                    _logger.LogError("Nie można przypisać użytkownika do typu szkoły: Użytkownik o ID {UserId} nie istnieje lub jest nieaktywny.", userId);
                    return null;
                }
                
                if (schoolType == null || !schoolType.IsActive)
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Typ szkoły o ID '{schoolTypeId}' nie został znaleziony lub jest nieaktywny."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się przypisać użytkownika do typu szkoły.",
                        "error"
                    );
                    
                    _logger.LogError("Nie można przypisać użytkownika do typu szkoły: Typ szkoły o ID {SchoolTypeId} nie istnieje lub jest nieaktywny.", schoolTypeId);
                    return null;
                }
                
                var existingAssignment = user.SchoolTypeAssignments.FirstOrDefault(ust => ust.SchoolTypeId == schoolTypeId && ust.IsActive && ust.IsCurrentlyActive);
                if (existingAssignment != null)
                {
                    _logger.LogWarning("Użytkownik {UserId} jest już aktywnie przypisany do typu szkoły {SchoolTypeId}.", userId, schoolTypeId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Użytkownik już aktywnie przypisany do tego typu szkoły."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Użytkownik {user.FullName} jest już przypisany do {schoolType.ShortName}.",
                        "error"
                    );
                    
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
                    IsCurrentlyActive = true,
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };
                
                await _userSchoolTypeRepository.AddAsync(newUserSchoolType);
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Przypisano użytkownika {user.FullName} do typu szkoły {schoolType.ShortName}."
                );
                
                _logger.LogInformation("Użytkownik {UserId} pomyślnie przypisany do typu szkoły {SchoolTypeId}.", userId, schoolTypeId);
                
                // Wysłanie powiadomień
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Przypisano użytkownika {user.FullName} do {schoolType.ShortName}",
                    "success"
                );
                
                // Powiadom użytkownika o przypisaniu
                if (!string.IsNullOrEmpty(user.UPN) && user.UPN != currentUserUpn)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        user.UPN,
                        "Przypisano Cię do typu szkoły",
                        $"Zostałeś przypisany do typu szkoły: {schoolType.FullName}"
                    );
                }
                
                InvalidateUserCache(userId: user.Id, upn: user.UPN, invalidateAllGlobalLists: false);
                
                return newUserSchoolType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas przypisania użytkownika ID {UserId} do typu szkoły ID {SchoolTypeId}.", userId, schoolTypeId);
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Wystąpił krytyczny błąd podczas przypisywania użytkownika do typu szkoły.",
                    "error"
                );
                
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveUserFromSchoolTypeAsync(string userSchoolTypeId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_ust";
            
            // 1. Inicjalizacja operacji historii
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.UserSchoolTypeRemoved,
                nameof(UserSchoolType),
                targetEntityId: userSchoolTypeId
            );
            
            UserSchoolType? assignment = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie usuwania przypisania UserSchoolType ID: {UserSchoolTypeId} przez {User}", userSchoolTypeId, currentUserUpn);
                
                assignment = await _userSchoolTypeRepository.GetByIdAsync(userSchoolTypeId);
                if (assignment == null)
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Przypisanie o ID '{userSchoolTypeId}' nie zostało znalezione."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się usunąć przypisania użytkownika do typu szkoły: Przypisanie nie zostało znalezione.",
                        "error"
                    );
                    
                    _logger.LogError("Nie można usunąć przypisania: Przypisanie o ID {UserSchoolTypeId} nie istnieje.", userSchoolTypeId);
                    return false;
                }
                
                var user = await _userRepository.GetByIdAsync(assignment.UserId);
                var schoolType = await _schoolTypeRepository.GetByIdAsync(assignment.SchoolTypeId);
                
                if (!assignment.IsActive)
                {
                    _logger.LogInformation("Przypisanie UserSchoolType ID {UserSchoolTypeId} było już nieaktywne.", userSchoolTypeId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Przypisanie {user?.UPN} do {schoolType?.ShortName} było już nieaktywne."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Przypisanie użytkownika {user?.FullName} do {schoolType?.ShortName} było już nieaktywne.",
                        "info"
                    );
                    
                    if (user != null) InvalidateUserCache(userId: user.Id, upn: user.UPN, invalidateAllGlobalLists: false);
                    return true;
                }
                
                assignment.MarkAsDeleted(currentUserUpn);
                _userSchoolTypeRepository.Update(assignment);
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Usunięto przypisanie UserSchoolType ID: {userSchoolTypeId}."
                );
                
                _logger.LogInformation("Przypisanie UserSchoolType ID: {UserSchoolTypeId} pomyślnie usunięte (oznaczone jako nieaktywne).", userSchoolTypeId);
                
                // Wysłanie powiadomień
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Usunięto przypisanie użytkownika {user?.FullName} do {schoolType?.ShortName}",
                    "success"
                );
                
                // Powiadom użytkownika o usunięciu przypisania
                if (user != null && !string.IsNullOrEmpty(user.UPN) && user.UPN != currentUserUpn)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        user.UPN,
                        "Usunięto Twoje przypisanie do typu szkoły",
                        $"Zostało usunięte Twoje przypisanie do typu szkoły: {schoolType?.FullName}"
                    );
                }
                
                if (user != null)
                {
                    InvalidateUserCache(userId: user.Id, upn: user.UPN, invalidateAllGlobalLists: false);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania przypisania UserSchoolType ID {UserSchoolTypeId}.", userSchoolTypeId);
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Wystąpił krytyczny błąd podczas usuwania przypisania użytkownika do typu szkoły.",
                    "error"
                );
                
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<UserSubject?> AssignTeacherToSubjectAsync(string teacherId, string subjectId, DateTime assignedDate, string? notes = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_assign_usubj";
            
            // 1. Inicjalizacja operacji historii
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.UserSubjectAssigned,
                nameof(UserSubject)
            );
            
            User? teacher = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie przypisania nauczyciela {TeacherId} do przedmiotu {SubjectId} przez {User}", teacherId, subjectId, currentUserUpn);
                
                teacher = await _userRepository.GetByIdAsync(teacherId);
                var subject = await _subjectRepository.GetByIdAsync(subjectId);
                
                if (teacher == null || !teacher.IsActive || (teacher.Role != UserRole.Nauczyciel && teacher.Role != UserRole.Wicedyrektor && teacher.Role != UserRole.Dyrektor))
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Użytkownik o ID '{teacherId}' nie został znaleziony, jest nieaktywny lub nie ma uprawnień do nauczania."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się przypisać nauczyciela do przedmiotu: Użytkownik nie istnieje lub nie ma uprawnień.",
                        "error"
                    );
                    
                    _logger.LogError("Nie można przypisać nauczyciela do przedmiotu: Użytkownik o ID {TeacherId} nie istnieje, jest nieaktywny lub nie ma odpowiedniej roli.", teacherId);
                    return null;
                }
                
                if (subject == null || !subject.IsActive)
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Przedmiot o ID '{subjectId}' nie został znaleziony lub jest nieaktywny."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się przypisać nauczyciela do przedmiotu: Przedmiot nie istnieje lub jest nieaktywny.",
                        "error"
                    );
                    
                    _logger.LogError("Nie można przypisać nauczyciela do przedmiotu: Przedmiot o ID {SubjectId} nie istnieje lub jest nieaktywny.", subjectId);
                    return null;
                }
                
                var existingAssignment = teacher.TaughtSubjects.FirstOrDefault(us => us.SubjectId == subjectId && us.IsActive);
                if (existingAssignment != null)
                {
                    _logger.LogWarning("Nauczyciel {TeacherId} jest już aktywnie przypisany do przedmiotu {SubjectId}.", teacherId, subjectId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nauczyciel już aktywnie przypisany do tego przedmiotu."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Nauczyciel {teacher.FullName} jest już przypisany do przedmiotu {subject.Name}.",
                        "error"
                    );
                    
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
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Przypisano nauczyciela {teacher.FullName} do przedmiotu {subject.Name}."
                );
                
                _logger.LogInformation("Nauczyciel {TeacherId} pomyślnie przypisany do przedmiotu {SubjectId}.", teacherId, subjectId);
                
                // Wysłanie powiadomień
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Przypisano nauczyciela {teacher.FullName} do przedmiotu {subject.Name}",
                    "success"
                );
                
                // Powiadom nauczyciela o przypisaniu
                if (!string.IsNullOrEmpty(teacher.UPN) && teacher.UPN != currentUserUpn)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        teacher.UPN,
                        "Przypisano Cię do przedmiotu",
                        $"Zostałeś przypisany do nauczania przedmiotu: {subject.Name}"
                    );
                }
                
                InvalidateUserCache(userId: teacher.Id, upn: teacher.UPN, invalidateAllGlobalLists: false);
                await _subjectService.InvalidateTeachersCacheForSubjectAsync(subjectId);
                
                return newUserSubject;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas przypisania nauczyciela ID {TeacherId} do przedmiotu ID {SubjectId}.", teacherId, subjectId);
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Wystąpił krytyczny błąd podczas przypisywania nauczyciela do przedmiotu.",
                    "error"
                );
                
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveTeacherFromSubjectAsync(string userSubjectId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_usubj";
            
            // 1. Inicjalizacja operacji historii
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.UserSubjectRemoved,
                nameof(UserSubject),
                targetEntityId: userSubjectId
            );
            
            UserSubject? assignment = null;
            try
            {
                _logger.LogInformation("Rozpoczynanie usuwania przypisania UserSubject ID: {UserSubjectId} przez {User}", userSubjectId, currentUserUpn);
                
                assignment = await _userSubjectRepository.GetByIdAsync(userSubjectId);
                if (assignment == null)
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Przypisanie o ID '{userSubjectId}' nie zostało znalezione."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się usunąć przypisania nauczyciela do przedmiotu: Przypisanie nie zostało znalezione.",
                        "error"
                    );
                    
                    _logger.LogError("Nie można usunąć przypisania: Przypisanie o ID {UserSubjectId} nie istnieje.", userSubjectId);
                    return false;
                }
                
                var user = await _userRepository.GetByIdAsync(assignment.UserId);
                var subject = await _subjectRepository.GetByIdAsync(assignment.SubjectId);
                
                if (!assignment.IsActive)
                {
                    _logger.LogInformation("Przypisanie UserSubject ID {UserSubjectId} było już nieaktywne.", userSubjectId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Przypisanie {user?.UPN} do {subject?.Name} było już nieaktywne."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Przypisanie nauczyciela {user?.FullName} do przedmiotu {subject?.Name} było już nieaktywne.",
                        "info"
                    );
                    
                    if (user != null) InvalidateUserCache(userId: user.Id, upn: user.UPN, invalidateAllGlobalLists: false);
                    await _subjectService.InvalidateTeachersCacheForSubjectAsync(assignment.SubjectId);
                    return true;
                }
                
                var subjectIdToInvalidate = assignment.SubjectId;
                assignment.MarkAsDeleted(currentUserUpn);
                _userSubjectRepository.Update(assignment);
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Usunięto przypisanie UserSubject ID: {userSubjectId}."
                );
                
                _logger.LogInformation("Przypisanie UserSubject ID: {UserSubjectId} pomyślnie usunięte (oznaczone jako nieaktywne).", userSubjectId);
                
                // Wysłanie powiadomień
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Usunięto przypisanie nauczyciela {user?.FullName} do przedmiotu {subject?.Name}",
                    "success"
                );
                
                // Powiadom nauczyciela o usunięciu przypisania
                if (user != null && !string.IsNullOrEmpty(user.UPN) && user.UPN != currentUserUpn)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        user.UPN,
                        "Usunięto Twoje przypisanie do przedmiotu",
                        $"Zostało usunięte Twoje przypisanie do nauczania przedmiotu: {subject?.Name}"
                    );
                }
                
                if (user != null)
                {
                    InvalidateUserCache(userId: user.Id, upn: user.UPN, invalidateAllGlobalLists: false);
                }
                await _subjectService.InvalidateTeachersCacheForSubjectAsync(subjectIdToInvalidate);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania przypisania UserSubject ID {UserSubjectId}.", userSubjectId);
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Wystąpił krytyczny błąd podczas usuwania przypisania nauczyciela do przedmiotu.",
                    "error"
                );
                
                return false;
            }
        }

        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Wymuszenie odświeżenia cache użytkowników.");
            InvalidateUserCache(invalidateAll: true);
            return Task.CompletedTask;
        }

        private void InvalidateUserCache(string? userId = null, string? upn = null, 
            UserRole? role = null, string? oldUpnIfChanged = null, 
            UserRole? oldRoleIfChanged = null, bool isActiveChanged = false, 
            bool invalidateAllGlobalLists = false, bool invalidateAll = false)
        {
            _logger.LogDebug("Delegowanie inwalidacji cache do PowerShellCacheService. " +
                "UserId: {UserId}, UPN: {UPN}, Role: {Role}, OldUpn: {OldUpn}, " +
                "OldRole: {OldRole}, IsActiveChanged: {IsActiveChanged}, " +
                "InvalidateAllGlobalLists: {InvalidateAllGlobalLists}, InvalidateAll: {InvalidateAll}",
                userId, upn, role, oldUpnIfChanged, oldRoleIfChanged, 
                isActiveChanged, invalidateAllGlobalLists, invalidateAll);

            if (invalidateAll)
            {
                // Pełne resetowanie cache tylko w skrajnych przypadkach
                _powerShellCacheService.InvalidateAllCache();
                _powerShellCacheService.InvalidateAllActiveUsersList();
                _powerShellCacheService.InvalidateUserListCache();
                
                // Usuń wszystkie klucze UserService
                foreach (UserRole roleToInvalidate in Enum.GetValues(typeof(UserRole)))
                {
                    _powerShellCacheService.InvalidateUsersByRole(roleToInvalidate);
                }
                
                _logger.LogInformation("Wykonano pełne resetowanie cache użytkowników.");
                return;
            }

            // Granularna inwalidacja - używamy kompleksowej metody
            _powerShellCacheService.InvalidateUserAndRelatedData(
                userId, 
                upn, 
                oldUpnIfChanged, 
                role, 
                oldRoleIfChanged
            );

            // Obsługa list globalnych
            if (invalidateAllGlobalLists || isActiveChanged)
            {
                _powerShellCacheService.InvalidateAllActiveUsersList();
                _powerShellCacheService.InvalidateUserListCache();
                
                _logger.LogDebug("Unieważniono globalne listy użytkowników " +
                    "(isActiveChanged: {IsActiveChanged}, invalidateAllGlobalLists: {InvalidateAllGlobalLists})",
                    isActiveChanged, invalidateAllGlobalLists);
            }

            // Specjalna obsługa dla ról nauczycielskich
            if (role.HasValue && IsTeachingRole(role.Value))
            {
                _powerShellCacheService.InvalidateUsersByRole(UserRole.Nauczyciel);
                _powerShellCacheService.InvalidateUsersByRole(UserRole.Wicedyrektor);
                _powerShellCacheService.InvalidateUsersByRole(UserRole.Dyrektor);
                
                _logger.LogDebug("Unieważniono cache dla wszystkich ról nauczycielskich.");
            }
            
            // Jeśli stara rola też była nauczycielska
            if (oldRoleIfChanged.HasValue && IsTeachingRole(oldRoleIfChanged.Value))
            {
                _powerShellCacheService.InvalidateUsersByRole(UserRole.Nauczyciel);
                _powerShellCacheService.InvalidateUsersByRole(UserRole.Wicedyrektor);
                _powerShellCacheService.InvalidateUsersByRole(UserRole.Dyrektor);
            }
        }

        // Metoda pomocnicza
        private bool IsTeachingRole(UserRole role)
        {
            return role == UserRole.Nauczyciel || 
                   role == UserRole.Wicedyrektor || 
                   role == UserRole.Dyrektor;
        }

        public async Task<bool> DeactivateUserAsync(string userId, string apiAccessToken, bool deactivateM365Account = true)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_deactivate";
            _logger.LogInformation("Rozpoczynanie dezaktywacji użytkownika ID: {UserId}", userId);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.UserDeactivated,
                nameof(User),
                targetEntityId: userId
            );

            User? user = null;
            try
            {
                var foundUsers = await _userRepository.FindAsync(u => u.Id == userId);
                user = foundUsers.FirstOrDefault();
                if (user == null) 
                { 
                    _logger.LogError("Nie można zdezaktywować użytkownika: Użytkownik o ID {UserId} nie istnieje.", userId); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Użytkownik o ID '{userId}' nie został znaleziony."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się zdezaktywować użytkownika: Użytkownik nie został znaleziony.",
                        "error"
                    );
                    
                    return false; 
                }
                
                if (!user.IsActive) 
                { 
                    _logger.LogWarning("Użytkownik o ID {UserId} jest już nieaktywny.", userId); 
                    // Używamy precyzyjnej inwalidacji - nie potrzebujemy invalidateAllGlobalLists gdy status się nie zmienił
                    InvalidateUserCache(userId: user.Id, upn: user.UPN, role: user.Role, isActiveChanged: false, invalidateAllGlobalLists: false); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Użytkownik o ID '{userId}' był już nieaktywny."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Użytkownik {user.FullName} był już nieaktywny.",
                        "info"
                    );
                    
                    return true; 
                }

                if (deactivateM365Account)
                {
                    // Używamy ExecuteWithAutoConnectAsync dla dezaktywacji konta M365
                    var psSuccess = await _powerShellService.ExecuteWithAutoConnectAsync(
                        apiAccessToken,
                        async () => await _powerShellService.Users.SetM365UserAccountStateAsync(user.UPN, false),
                        $"Dezaktywacja konta M365 użytkownika: {user.UPN}"
                    );
                    
                    if (psSuccess != true) 
                    { 
                        _logger.LogError("Nie udało się zdezaktywować konta użytkownika {UPN} w Microsoft 365.", user.UPN); 
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            $"Nie udało się zdezaktywować konta użytkownika '{user.UPN}' w Microsoft 365."
                        );
                        
                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            $"Nie udało się zdezaktywować konta użytkownika {user.FullName} w Microsoft 365.",
                            "error"
                        );
                        
                        return false; 
                    }
                }

                user.MarkAsDeleted(currentUserUpn);
                _userRepository.Update(user);
                _logger.LogInformation("Użytkownik ID: {UserId} pomyślnie zdezaktywowany.", userId);
                // Używamy precyzyjnej inwalidacji - isActiveChanged automatycznie obsłuży listy globalne
                InvalidateUserCache(userId: user.Id, upn: user.UPN, role: user.Role, isActiveChanged: true, invalidateAllGlobalLists: false);
                
                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    "Użytkownik zdezaktywowany lokalnie i opcjonalnie w M365."
                );
                
                // Wysłanie powiadomienia o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Zdezaktywowano użytkownika: {user.FullName} ({user.UPN})",
                    "success"
                );
                
                // Powiadomienie samego użytkownika o dezaktywacji
                if (!string.IsNullOrEmpty(user.UPN) && user.UPN != currentUserUpn)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        user.UPN,
                        "Twoje konto zostało zdezaktywowane",
                        $"Twoje konto w systemie zostało zdezaktywowane przez {currentUserUpn}."
                    );
                }
                
                return true;
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "Krytyczny błąd podczas dezaktywacji użytkownika ID {UserId}.", userId); 
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn ?? "system",
                    $"Wystąpił błąd podczas dezaktywacji użytkownika: {ex.Message}",
                    "error"
                );
                
                return false; 
            }
        }

        public async Task<bool> ActivateUserAsync(string userId, string apiAccessToken, bool activateM365Account = true)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_activate";
            _logger.LogInformation("Rozpoczynanie aktywacji użytkownika ID: {UserId}", userId);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.UserActivated,
                nameof(User),
                targetEntityId: userId
            );

            User? user = null;
            try
            {
                var foundUsers = await _userRepository.FindAsync(u => u.Id == userId);
                user = foundUsers.FirstOrDefault();
                if (user == null) 
                { 
                    _logger.LogError("Nie można aktywować użytkownika: Użytkownik o ID {UserId} nie istnieje.", userId); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Użytkownik o ID '{userId}' nie został znaleziony."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się aktywować użytkownika: Użytkownik nie został znaleziony.",
                        "error"
                    );
                    
                    return false; 
                }
                
                if (user.IsActive) 
                { 
                    _logger.LogInformation("Użytkownik o ID {UserId} był już aktywny.", userId); 
                    // Używamy precyzyjnej inwalidacji - nie potrzebujemy invalidateAllGlobalLists gdy status się nie zmienił
                    InvalidateUserCache(userId: user.Id, upn: user.UPN, role: user.Role, isActiveChanged: false, invalidateAllGlobalLists: false); 
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        "Użytkownik był już aktywny."
                    );
                    
                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Użytkownik {user.FullName} był już aktywny.",
                        "info"
                    );
                    
                    return true; 
                }

                if (activateM365Account)
                {
                    // Używamy ExecuteWithAutoConnectAsync dla aktywacji konta M365
                    var psSuccess = await _powerShellService.ExecuteWithAutoConnectAsync(
                        apiAccessToken,
                        async () => await _powerShellService.Users.SetM365UserAccountStateAsync(user.UPN, true),
                        $"Aktywacja konta M365 użytkownika: {user.UPN}"
                    );
                    
                    if (psSuccess != true) 
                    { 
                        _logger.LogError("Nie udało się aktywować konta użytkownika {UPN} w Microsoft 365.", user.UPN); 
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            $"Nie udało się aktywować konta użytkownika '{user.UPN}' w Microsoft 365."
                        );
                        
                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            $"Nie udało się aktywować konta użytkownika {user.FullName} w Microsoft 365.",
                            "error"
                        );
                        
                        return false; 
                    }
                }

                user.IsActive = true;
                user.MarkAsModified(currentUserUpn);
                _userRepository.Update(user);
                _logger.LogInformation("Użytkownik ID: {UserId} pomyślnie aktywowany.", userId);
                // Używamy precyzyjnej inwalidacji - isActiveChanged automatycznie obsłuży listy globalne
                InvalidateUserCache(userId: user.Id, upn: user.UPN, role: user.Role, isActiveChanged: true, invalidateAllGlobalLists: false);
                
                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    "Użytkownik aktywowany lokalnie i opcjonalnie w M365."
                );
                
                // Wysłanie powiadomienia o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Aktywowano użytkownika: {user.FullName} ({user.UPN})",
                    "success"
                );
                
                // Powiadomienie samego użytkownika o aktywacji
                if (!string.IsNullOrEmpty(user.UPN) && user.UPN != currentUserUpn)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        user.UPN,
                        "Twoje konto zostało aktywowane",
                        $"Twoje konto w systemie zostało aktywowane przez {currentUserUpn}."
                    );
                }
                
                return true;
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "Krytyczny błąd podczas aktywacji użytkownika ID {UserId}.", userId); 
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn ?? "system",
                    $"Wystąpił błąd podczas aktywacji użytkownika: {ex.Message}",
                    "error"
                );
                
                return false; 
            }
        }
    }
}