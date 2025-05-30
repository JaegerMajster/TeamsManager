using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Services
{
    public class DepartmentService : IDepartmentService
    {
        private readonly IGenericRepository<Department> _departmentRepository;
        private readonly IUserRepository _userRepository; // Potrzebne do sprawdzenia, czy dział ma użytkowników przed usunięciem
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DepartmentService> _logger;

        public DepartmentService(
            IGenericRepository<Department> departmentRepository,
            IUserRepository userRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<DepartmentService> logger)
        {
            _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Department?> GetDepartmentByIdAsync(string departmentId, bool includeSubDepartments = false, bool includeUsers = false)
        {
            _logger.LogInformation("Pobieranie działu o ID: {DepartmentId}. Dołączanie poddziałów: {IncludeSubDepartments}, Dołączanie użytkowników: {IncludeUsers}",
                                departmentId, includeSubDepartments, includeUsers);

            var department = await _departmentRepository.GetByIdAsync(departmentId);

            if (department != null)
            {
                if (includeSubDepartments)
                {
                    // Ładujemy poddziały poprzez dodatkowe zapytanie
                    var subDepartments = await GetSubDepartmentsAsync(departmentId);
                    department.SubDepartments = subDepartments.ToList();
                    _logger.LogInformation("Załadowano {Count} poddziałów dla działu {DepartmentId}", subDepartments.Count(), departmentId);
                }
                if (includeUsers)
                {
                    // Ładujemy użytkowników poprzez dodatkowe zapytanie
                    var users = await GetUsersInDepartmentAsync(departmentId);
                    department.Users = users.ToList();
                    _logger.LogInformation("Załadowano {Count} użytkowników dla działu {DepartmentId}", users.Count(), departmentId);
                }
            }
            return department;
        }

        public async Task<IEnumerable<Department>> GetAllDepartmentsAsync(bool onlyRootDepartments = false)
        {
            _logger.LogInformation("Pobieranie wszystkich działów. Tylko główne: {OnlyRoot}", onlyRootDepartments);
            if (onlyRootDepartments)
            {
                return await _departmentRepository.FindAsync(d => d.IsActive && d.ParentDepartmentId == null);
            }
            return await _departmentRepository.FindAsync(d => d.IsActive);
        }

        public async Task<Department?> CreateDepartmentAsync(
            string name,
            string description,
            string? parentDepartmentId = null,
            string? departmentCode = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.DepartmentCreated,
                TargetEntityType = nameof(Department),
                TargetEntityName = name,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Rozpoczynanie tworzenia działu: '{DepartmentName}' przez {User}", name, currentUserUpn);

                if (string.IsNullOrWhiteSpace(name))
                {
                    operation.MarkAsFailed("Nazwa działu nie może być pusta.");
                    _logger.LogError("Nie można utworzyć działu: Nazwa jest pusta.");
                    return null; // Zapis OperationHistory w bloku finally
                }

                // Opcjonalna walidacja unikalności nazwy lub kodu, jeśli wymagane
                // var existingDepartment = await _departmentRepository.FindAsync(d => d.Name == name || (departmentCode != null && d.DepartmentCode == departmentCode));
                // if (existingDepartment.Any()) { /* obsługa błędu */ }

                Department? parentDepartment = null;
                if (!string.IsNullOrEmpty(parentDepartmentId))
                {
                    parentDepartment = await _departmentRepository.GetByIdAsync(parentDepartmentId);
                    if (parentDepartment == null)
                    {
                        operation.MarkAsFailed($"Dział nadrzędny o ID '{parentDepartmentId}' nie istnieje.");
                        _logger.LogWarning("Nie można utworzyć działu: Dział nadrzędny o ID {ParentDepartmentId} nie istnieje.", parentDepartmentId);
                        return null;
                    }
                }

                var newDepartment = new Department
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Description = description,
                    ParentDepartmentId = parentDepartmentId,
                    ParentDepartment = parentDepartment, // Dla EF Core
                    DepartmentCode = departmentCode,
                    CreatedBy = currentUserUpn,
                    IsActive = true
                    // SortOrder, Email, Phone, Location można ustawić później lub dodać do parametrów
                };

                await _departmentRepository.AddAsync(newDepartment);
                // SaveChangesAsync będzie na wyższym poziomie

                operation.TargetEntityId = newDepartment.Id;
                operation.MarkAsCompleted($"Dział ID: {newDepartment.Id} przygotowany do utworzenia.");
                _logger.LogInformation("Dział '{DepartmentName}' pomyślnie przygotowany do zapisu. ID: {DepartmentId}", name, newDepartment.Id);
                return newDepartment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia działu {DepartmentName}. Wiadomość: {ErrorMessage}", name, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return null;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> UpdateDepartmentAsync(Department departmentToUpdate)
        {
            if (departmentToUpdate == null || string.IsNullOrEmpty(departmentToUpdate.Id))
                throw new ArgumentNullException(nameof(departmentToUpdate), "Obiekt działu lub jego ID nie może być null/pusty.");

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.DepartmentUpdated,
                TargetEntityType = nameof(Department),
                TargetEntityId = departmentToUpdate.Id,
                TargetEntityName = departmentToUpdate.Name,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Aktualizowanie działu ID: {DepartmentId}", departmentToUpdate.Id);

            try
            {
                var existingDepartment = await _departmentRepository.GetByIdAsync(departmentToUpdate.Id);
                if (existingDepartment == null)
                {
                    operation.MarkAsFailed("Dział nie istnieje.");
                    _logger.LogWarning("Nie można zaktualizować działu ID {DepartmentId} - nie istnieje.", departmentToUpdate.Id);
                    return false;
                }

                // Walidacja - sprawdzenie czy nie próbuje się ustawić siebie jako rodzica (cykliczna zależność)
                if (!string.IsNullOrEmpty(departmentToUpdate.ParentDepartmentId))
                {
                    if (departmentToUpdate.ParentDepartmentId == departmentToUpdate.Id)
                    {
                        operation.MarkAsFailed("Dział nie może być swoim własnym rodzicem.");
                        _logger.LogWarning("Próba ustawienia działu {DepartmentId} jako swojego własnego rodzica.", departmentToUpdate.Id);
                        return false;
                    }

                    var parentDepartment = await _departmentRepository.GetByIdAsync(departmentToUpdate.ParentDepartmentId);
                    if (parentDepartment == null || !parentDepartment.IsActive)
                    {
                        operation.MarkAsFailed($"Dział nadrzędny o ID '{departmentToUpdate.ParentDepartmentId}' nie istnieje lub jest nieaktywny.");
                        _logger.LogWarning("Dział nadrzędny {ParentDepartmentId} nie istnieje lub jest nieaktywny.", departmentToUpdate.ParentDepartmentId);
                        return false;
                    }

                    // Sprawdzenie cyklicznej zależności - czy parent nie jest potomkiem tego działu
                    if (await IsDescendantAsync(departmentToUpdate.ParentDepartmentId, departmentToUpdate.Id))
                    {
                        operation.MarkAsFailed("Nie można ustawić działu jako rodzica, ponieważ spowodowałoby to cykliczną zależność.");
                        _logger.LogWarning("Próba utworzenia cyklicznej zależności między działami {DepartmentId} i {ParentDepartmentId}.", 
                            departmentToUpdate.Id, departmentToUpdate.ParentDepartmentId);
                        return false;
                    }
                }

                existingDepartment.Name = departmentToUpdate.Name;
                existingDepartment.Description = departmentToUpdate.Description;
                existingDepartment.ParentDepartmentId = departmentToUpdate.ParentDepartmentId;
                existingDepartment.DepartmentCode = departmentToUpdate.DepartmentCode;
                existingDepartment.Email = departmentToUpdate.Email;
                existingDepartment.Phone = departmentToUpdate.Phone;
                existingDepartment.Location = departmentToUpdate.Location;
                existingDepartment.SortOrder = departmentToUpdate.SortOrder;
                existingDepartment.MarkAsModified(currentUserUpn);

                _departmentRepository.Update(existingDepartment);
                operation.MarkAsCompleted("Dział przygotowany do aktualizacji.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji działu ID {DepartmentId}. Wiadomość: {ErrorMessage}", departmentToUpdate.Id, ex.Message);
                operation.MarkAsFailed($"Błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> DeleteDepartmentAsync(string departmentId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.DepartmentDeleted,
                TargetEntityType = nameof(Department),
                TargetEntityId = departmentId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Usuwanie działu ID: {DepartmentId}", departmentId);

            try
            {
                var department = await _departmentRepository.GetByIdAsync(departmentId);
                if (department == null)
                {
                    operation.MarkAsFailed("Dział nie istnieje.");
                    _logger.LogWarning("Nie można usunąć działu ID {DepartmentId} - nie istnieje.", departmentId);
                    return false;
                }
                operation.TargetEntityName = department.Name;

                // Sprawdzenie, czy dział nie ma poddziałów (zgodnie z OnDelete.Restrict w DbContext)
                var subDepartments = await GetSubDepartmentsAsync(departmentId);
                if (subDepartments.Any())
                {
                    operation.MarkAsFailed("Nie można usunąć działu, ponieważ ma przypisane poddziały.");
                    _logger.LogWarning("Nie można usunąć działu ID {DepartmentId} - ma poddziały.", departmentId);
                    return false;
                }

                // Sprawdzenie, czy dział nie ma przypisanych użytkowników (zgodnie z OnDelete.Restrict w DbContext)
                var usersInDepartment = await _userRepository.FindAsync(u => u.DepartmentId == departmentId && u.IsActive);
                if (usersInDepartment.Any())
                {
                    operation.MarkAsFailed("Nie można usunąć działu, ponieważ ma przypisanych aktywnych użytkowników.");
                    _logger.LogWarning("Nie można usunąć działu ID {DepartmentId} - ma aktywnych użytkowników.", departmentId);
                    return false;
                }

                department.MarkAsDeleted(currentUserUpn); // Soft delete
                _departmentRepository.Update(department);
                // SaveChangesAsync na wyższym poziomie
                operation.MarkAsCompleted("Dział oznaczony jako usunięty.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania działu ID {DepartmentId}. Wiadomość: {ErrorMessage}", departmentId, ex.Message);
                operation.MarkAsFailed($"Błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<IEnumerable<Department>> GetSubDepartmentsAsync(string parentDepartmentId)
        {
            _logger.LogInformation("Pobieranie poddziałów dla działu ID: {ParentDepartmentId}", parentDepartmentId);
            return await _departmentRepository.FindAsync(d => d.ParentDepartmentId == parentDepartmentId && d.IsActive);
        }

        public async Task<IEnumerable<User>> GetUsersInDepartmentAsync(string departmentId)
        {
            _logger.LogInformation("Pobieranie użytkowników dla działu ID: {DepartmentId}", departmentId);
            return await _userRepository.FindAsync(u => u.DepartmentId == departmentId && u.IsActive);
        }

        // Metoda pomocnicza do zapisu OperationHistory (bez SaveChanges)
        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id))
            {
                operation.Id = Guid.NewGuid().ToString();
            }
            if (await _operationHistoryRepository.GetByIdAsync(operation.Id) == null)
            {
                await _operationHistoryRepository.AddAsync(operation);
            }
            else
            {
                var existingLog = await _operationHistoryRepository.GetByIdAsync(operation.Id); // Pobierz ponownie, aby upewnić się, że jest śledzony
                if (existingLog != null)
                {
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
                else
                {
                    // To nie powinno się zdarzyć, jeśli ID jest takie samo
                    await _operationHistoryRepository.AddAsync(operation);
                }
            }
            // SaveChangesAsync będzie na wyższym poziomie
        }

        // Metoda pomocnicza do sprawdzenia cyklicznej zależności
        private async Task<bool> IsDescendantAsync(string potentialAncestorId, string departmentId)
        {
            var subDepartments = await GetSubDepartmentsAsync(departmentId);
            foreach (var subDept in subDepartments)
            {
                if (subDept.Id == potentialAncestorId)
                    return true;
                
                if (await IsDescendantAsync(potentialAncestorId, subDept.Id))
                    return true;
            }
            return false;
        }
    }
}