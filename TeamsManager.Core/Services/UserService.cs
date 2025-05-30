using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;         // Dla ICurrentUserService
using TeamsManager.Core.Abstractions.Data;    // Dla interfejsów repozytoriów
using TeamsManager.Core.Abstractions.Services; // Dla IUserService
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IGenericRepository<Department> _departmentRepository;
        private readonly IGenericRepository<UserSchoolType> _userSchoolTypeRepository;
        private readonly IGenericRepository<SchoolType> _schoolTypeRepository; // Potrzebne dla AssignUserToSchoolTypeAsync
        private readonly IGenericRepository<UserSubject> _userSubjectRepository;
        private readonly IGenericRepository<Subject> _subjectRepository; // Potrzebne dla AssignTeacherToSubjectAsync
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<UserService> _logger;
        // USUNIĘTO: private readonly TeamsManagerDbContext _dbContext;

        public UserService(
            IUserRepository userRepository,
            IGenericRepository<Department> departmentRepository,
            IGenericRepository<UserSchoolType> userSchoolTypeRepository,
            IGenericRepository<SchoolType> schoolTypeRepository, // Dodano
            IGenericRepository<UserSubject> userSubjectRepository,
            IGenericRepository<Subject> subjectRepository,       // Dodano
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<UserService> logger)
        // USUNIĘTO: TeamsManagerDbContext dbContext
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
            _userSchoolTypeRepository = userSchoolTypeRepository ?? throw new ArgumentNullException(nameof(userSchoolTypeRepository));
            _schoolTypeRepository = schoolTypeRepository ?? throw new ArgumentNullException(nameof(schoolTypeRepository)); // Dodano
            _userSubjectRepository = userSubjectRepository ?? throw new ArgumentNullException(nameof(userSubjectRepository));
            _subjectRepository = subjectRepository ?? throw new ArgumentNullException(nameof(subjectRepository));       // Dodano
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // USUNIĘTO: _dbContext = dbContext ...
        }

        public async Task<User?> GetUserByIdAsync(string userId)
        {
            _logger.LogInformation("Pobieranie użytkownika o ID: {UserId}", userId);
            return await _userRepository.GetByIdAsync(userId); // Zakładamy, że GetByIdAsync w repozytorium dołącza potrzebne dane
        }

        public async Task<User?> GetUserByUpnAsync(string upn)
        {
            _logger.LogInformation("Pobieranie użytkownika o UPN: {UPN}", upn);
            return await _userRepository.GetUserByUpnAsync(upn);
        }

        public async Task<IEnumerable<User>> GetAllActiveUsersAsync()
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych użytkowników.");
            return await _userRepository.FindAsync(u => u.IsActive);
        }

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
                    await SaveOperationHistoryAsync(operation); // SaveChanges będzie na wyższym poziomie
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
                bool psSuccess = true; // Symulacja

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
                        Department = department, // Dla spójności obiektu, EF Core zarządza tym przy zapisie FK
                        CreatedBy = currentUserUpn, // Ustawiane przez SetAuditFields w DbContext, ale tu też można
                        IsActive = true
                    };

                    await _userRepository.AddAsync(newUser);
                    // USUNIĘTO: await _dbContext.SaveChangesAsync();

                    operation.TargetEntityId = newUser.Id;
                    operation.MarkAsCompleted($"Użytkownik ID: {newUser.Id} przygotowany do utworzenia.");
                    _logger.LogInformation("Użytkownik {FirstName} {LastName} ({UPN}) pomyślnie przygotowany do zapisu. ID: {UserId}", firstName, lastName, upn, newUser.Id);
                    await SaveOperationHistoryAsync(operation);

                    if (sendWelcomeEmail)
                    {
                        _logger.LogInformation("TODO: Wysłanie emaila powitalnego do {UPN}", upn);
                    }
                    return newUser; // Zwracamy obiekt przygotowany do zapisu
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

        public async Task<bool> UpdateUserAsync(User userToUpdate)
        {
            if (userToUpdate == null) throw new ArgumentNullException(nameof(userToUpdate));
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory { /* ... inicjalizacja ... */ };
            // ... (logika jak w poprzedniej wersji, ale bez SaveChanges) ...
            try
            {
                var existingUser = await _userRepository.GetByIdAsync(userToUpdate.Id);
                if (existingUser == null) { /* ... obsługa błędu ... */ return false; }

                // Mapowanie właściwości (bez zmiany UPN na razie)
                existingUser.FirstName = userToUpdate.FirstName;
                existingUser.LastName = userToUpdate.LastName;
                existingUser.Role = userToUpdate.Role;
                existingUser.DepartmentId = userToUpdate.DepartmentId;
                // ... itd.
                existingUser.MarkAsModified(currentUserUpn);
                _userRepository.Update(existingUser);
                // USUNIĘTO: await _dbContext.SaveChangesAsync();
                operation.MarkAsCompleted("Użytkownik przygotowany do aktualizacji.");
                await SaveOperationHistoryAsync(operation);
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

        public async Task<bool> DeactivateUserAsync(string userId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_deactivate";
            var operation = new OperationHistory { /* ... */};
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || !user.IsActive) { /* ... obsługa ... */ return false; }

                // TODO: PowerShellService call - Disable M365 account if needed
                bool psSuccess = true; // Symulacja

                if (psSuccess)
                {
                    user.MarkAsDeleted(currentUserUpn); // Ustawia IsActive = false
                    _userRepository.Update(user);
                    // USUNIĘTO: await _dbContext.SaveChangesAsync();
                    operation.MarkAsCompleted("Użytkownik zdezaktywowany.");
                    await SaveOperationHistoryAsync(operation);
                    return true;
                }
                // ... obsługa błędu psSuccess ...
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas dezaktywacji użytkownika ID {UserId}.", userId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return false;
            }
        }

        public async Task<bool> ActivateUserAsync(string userId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_activate";
            var operation = new OperationHistory { /* ... */};
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null) { /* ... */ return false; }
                if (user.IsActive) { /* już aktywny */ operation.MarkAsCompleted("Użytkownik był już aktywny."); await SaveOperationHistoryAsync(operation); return true; }

                // TODO: PowerShellService call - Enable M365 account if needed
                bool psSuccess = true;

                if (psSuccess)
                {
                    user.IsActive = true; // Aktywacja
                    user.MarkAsModified(currentUserUpn);
                    _userRepository.Update(user);
                    // USUNIĘTO: await _dbContext.SaveChangesAsync();
                    operation.MarkAsCompleted("Użytkownik aktywowany.");
                    await SaveOperationHistoryAsync(operation);
                    return true;
                }
                // ...
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktywacji użytkownika ID {UserId}.", userId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return false;
            }
        }

        public async Task<UserSchoolType?> AssignUserToSchoolTypeAsync(string userId, string schoolTypeId, DateTime assignedDate, DateTime? endDate = null, decimal? workloadPercentage = null, string? notes = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_assign_ust";
            var operation = new OperationHistory { /* ... */};
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                var schoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeId);

                if (user == null || !user.IsActive) { /* ... obsługa, log ... */ return null; }
                if (schoolType == null || !schoolType.IsActive) { /* ... obsługa, log ... */ return null; }

                // Sprawdź, czy takie przypisanie już nie istnieje (aktywne)
                var existingAssignment = await _userSchoolTypeRepository.FindAsync(ust => ust.UserId == userId && ust.SchoolTypeId == schoolTypeId && ust.IsActive && ust.IsCurrentlyActive);
                if (existingAssignment.Any())
                {
                    _logger.LogWarning("Użytkownik {UserId} jest już aktywnie przypisany do typu szkoły {SchoolTypeId}.", userId, schoolTypeId);
                    operation.MarkAsFailed("Użytkownik już aktywnie przypisany.");
                    await SaveOperationHistoryAsync(operation);
                    return existingAssignment.First(); // Zwróć istniejące
                }

                var newUserSchoolType = new UserSchoolType
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    User = user, // Dla EF Core
                    SchoolTypeId = schoolTypeId,
                    SchoolType = schoolType, // Dla EF Core
                    AssignedDate = assignedDate,
                    EndDate = endDate,
                    WorkloadPercentage = workloadPercentage,
                    Notes = notes,
                    IsCurrentlyActive = true, // Domyślnie nowe przypisanie jest bieżąco aktywne
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };
                await _userSchoolTypeRepository.AddAsync(newUserSchoolType);
                // USUNIĘTO: await _dbContext.SaveChangesAsync();
                operation.MarkAsCompleted($"Przypisano użytkownika {userId} do typu szkoły {schoolTypeId}.");
                await SaveOperationHistoryAsync(operation);
                return newUserSchoolType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas przypisania użytkownika do typu szkoły ID {SchoolTypeId}.", schoolTypeId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return null;
            }
        }

        public async Task<bool> RemoveUserFromSchoolTypeAsync(string userSchoolTypeId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_ust";
            var operation = new OperationHistory { /* ... */};
            try
            {
                var assignment = await _userSchoolTypeRepository.GetByIdAsync(userSchoolTypeId);
                if (assignment == null) { /* ... */ return false; }

                // Soft delete przypisania
                assignment.MarkAsDeleted(currentUserUpn); // IsActive = false
                _userSchoolTypeRepository.Update(assignment);
                // USUNIĘTO: await _dbContext.SaveChangesAsync();
                operation.MarkAsCompleted($"Usunięto przypisanie UserSchoolType ID: {userSchoolTypeId}.");
                await SaveOperationHistoryAsync(operation);
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

        public async Task<UserSubject?> AssignTeacherToSubjectAsync(string teacherId, string subjectId, DateTime assignedDate, string? notes = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_assign_usubj";
            var operation = new OperationHistory { /* ... */};
            try
            {
                var teacher = await _userRepository.GetByIdAsync(teacherId);
                var subject = await _subjectRepository.GetByIdAsync(subjectId);

                if (teacher == null || !teacher.IsActive || (teacher.Role != UserRole.Nauczyciel && teacher.Role != UserRole.Wicedyrektor && teacher.Role != UserRole.Dyrektor))
                { /* ... obsługa, log, nauczyciel musi istnieć i być aktywny i mieć odpowiednią rolę ... */ return null; }
                if (subject == null || !subject.IsActive) { /* ... obsługa, log ... */ return null; }

                var existingAssignment = await _userSubjectRepository.FindAsync(us => us.UserId == teacherId && us.SubjectId == subjectId && us.IsActive);
                if (existingAssignment.Any())
                {
                    _logger.LogWarning("Nauczyciel {TeacherId} jest już przypisany do przedmiotu {SubjectId}.", teacherId, subjectId);
                    operation.MarkAsFailed("Nauczyciel już przypisany do przedmiotu.");
                    await SaveOperationHistoryAsync(operation);
                    return existingAssignment.First();
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
                // USUNIĘTO: await _dbContext.SaveChangesAsync();
                operation.MarkAsCompleted($"Przypisano nauczyciela {teacherId} do przedmiotu {subjectId}.");
                await SaveOperationHistoryAsync(operation);
                return newUserSubject;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas przypisania nauczyciela do przedmiotu ID {SubjectId}.", subjectId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace);
                await SaveOperationHistoryAsync(operation);
                return null;
            }
        }

        public async Task<bool> RemoveTeacherFromSubjectAsync(string userSubjectId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_usubj";
            var operation = new OperationHistory { /* ... */};
            try
            {
                var assignment = await _userSubjectRepository.GetByIdAsync(userSubjectId);
                if (assignment == null) { /* ... */ return false; }

                assignment.MarkAsDeleted(currentUserUpn);
                _userSubjectRepository.Update(assignment);
                // USUNIĘTO: await _dbContext.SaveChangesAsync();
                operation.MarkAsCompleted($"Usunięto przypisanie UserSubject ID: {userSubjectId}.");
                await SaveOperationHistoryAsync(operation);
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

        // Metoda pomocnicza do zapisu OperationHistory (bez SaveChanges)
        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id))
            {
                operation.Id = Guid.NewGuid().ToString();
            }

            // Sprawdzamy czy repozytorium już śledzi ten obiekt, jeśli tak, Update
            // Jeśli nie, sprawdzamy czy istnieje w bazie, jeśli tak, Update
            // Jeśli nie istnieje w bazie, AddAsync
            // To jest uproszczenie, pełna logika śledzenia może być bardziej złożona.
            // Na razie zakładamy, że jeśli ID jest i go nie ma, to dodajemy, inaczej Update.
            var existingLog = await _operationHistoryRepository.GetByIdAsync(operation.Id);
            if (existingLog == null)
            {
                await _operationHistoryRepository.AddAsync(operation);
            }
            else
            {
                // Jeśli aktualizujemy, trzeba by przepisać właściwości z 'operation' do 'existingLog'
                // lub upewnić się, że _operationHistoryRepository.Update poprawnie obsługuje odłączone encje.
                // Dla uproszczenia:
                existingLog.Status = operation.Status;
                existingLog.CompletedAt = operation.CompletedAt;
                existingLog.Duration = operation.Duration;
                existingLog.ErrorMessage = operation.ErrorMessage;
                existingLog.ErrorStackTrace = operation.ErrorStackTrace;
                existingLog.OperationDetails = operation.OperationDetails;
                existingLog.ProcessedItems = operation.ProcessedItems;
                existingLog.FailedItems = operation.FailedItems;
                existingLog.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_log_update");
                _operationHistoryRepository.Update(existingLog);
            }
            // USUNIĘTO: await _dbContext.SaveChangesAsync(); // Zapis logu będzie na wyższym poziomie
        }
    }
}