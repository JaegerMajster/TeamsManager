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
    public class SchoolTypeService : ISchoolTypeService
    {
        private readonly IGenericRepository<SchoolType> _schoolTypeRepository;
        private readonly IUserRepository _userRepository; // Do zarządzania wicedyrektorami
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SchoolTypeService> _logger;
        // Brak bezpośredniej zależności od DbContext

        public SchoolTypeService(
            IGenericRepository<SchoolType> schoolTypeRepository,
            IUserRepository userRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<SchoolTypeService> logger)
        {
            _schoolTypeRepository = schoolTypeRepository ?? throw new ArgumentNullException(nameof(schoolTypeRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SchoolType?> GetSchoolTypeByIdAsync(string schoolTypeId)
        {
            _logger.LogInformation("Pobieranie typu szkoły o ID: {SchoolTypeId}", schoolTypeId);
            // GenericRepository.GetByIdAsync nie dołącza relacji SupervisingViceDirectors domyślnie.
            // Jeśli potrzebne, musimy stworzyć ISchoolTypeRepository z dedykowaną metodą
            // lub załadować jawnie na wyższym poziomie, jeśli serwis zwraca tylko ID.
            // Na razie zwracamy podstawowy obiekt.
            return await _schoolTypeRepository.GetByIdAsync(schoolTypeId);
        }

        public async Task<IEnumerable<SchoolType>> GetAllActiveSchoolTypesAsync()
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych typów szkół.");
            return await _schoolTypeRepository.FindAsync(st => st.IsActive);
        }

        public async Task<SchoolType?> CreateSchoolTypeAsync(
            string shortName,
            string fullName,
            string description,
            string? colorCode = null,
            int sortOrder = 0)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SchoolTypeCreated,
                TargetEntityType = nameof(SchoolType),
                TargetEntityName = $"{shortName} - {fullName}",
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Rozpoczynanie tworzenia typu szkoły: {ShortName} - {FullName} przez {User}", shortName, fullName, currentUserUpn);

                if (string.IsNullOrWhiteSpace(shortName) || string.IsNullOrWhiteSpace(fullName))
                {
                    operation.MarkAsFailed("Skrócona nazwa i pełna nazwa typu szkoły są wymagane.");
                    _logger.LogError("Nie można utworzyć typu szkoły: Skrócona nazwa lub pełna nazwa są puste.");
                    await SaveOperationHistoryAsync(operation);
                    return null;
                }

                var existingSchoolType = (await _schoolTypeRepository.FindAsync(st => st.ShortName == shortName && st.IsActive)).FirstOrDefault();
                if (existingSchoolType != null)
                {
                    operation.MarkAsFailed($"Typ szkoły o skróconej nazwie '{shortName}' już istnieje i jest aktywny.");
                    _logger.LogError("Nie można utworzyć typu szkoły: Aktywny typ szkoły o skróconej nazwie {ShortName} już istnieje.", shortName);
                    await SaveOperationHistoryAsync(operation);
                    return null;
                }

                var newSchoolType = new SchoolType
                {
                    Id = Guid.NewGuid().ToString(),
                    ShortName = shortName,
                    FullName = fullName,
                    Description = description,
                    ColorCode = colorCode,
                    SortOrder = sortOrder,
                    CreatedBy = currentUserUpn, // Ustawiane również przez DbContext, ale tu dla spójności logu
                    IsActive = true
                };

                await _schoolTypeRepository.AddAsync(newSchoolType);
                // SaveChangesAsync będzie na wyższym poziomie (np. Unit of Work lub kontroler API)

                operation.TargetEntityId = newSchoolType.Id;
                operation.MarkAsCompleted($"Typ szkoły ID: {newSchoolType.Id} ('{newSchoolType.ShortName}') przygotowany do utworzenia.");
                _logger.LogInformation("Typ szkoły '{FullName}' pomyślnie przygotowany do zapisu. ID: {SchoolTypeId}", fullName, newSchoolType.Id);
                return newSchoolType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia typu szkoły '{FullName}'. Wiadomość: {ErrorMessage}", fullName, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return null;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> UpdateSchoolTypeAsync(SchoolType schoolTypeToUpdate)
        {
            if (schoolTypeToUpdate == null || string.IsNullOrEmpty(schoolTypeToUpdate.Id))
                throw new ArgumentNullException(nameof(schoolTypeToUpdate), "Obiekt typu szkoły lub jego ID nie może być null/pusty.");

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SchoolTypeUpdated,
                TargetEntityType = nameof(SchoolType),
                TargetEntityId = schoolTypeToUpdate.Id,
                TargetEntityName = schoolTypeToUpdate.FullName, // Nazwa PRZED modyfikacją
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie aktualizacji typu szkoły ID: {SchoolTypeId} przez {User}", schoolTypeToUpdate.Id, currentUserUpn);

            try
            {
                var existingSchoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeToUpdate.Id);
                if (existingSchoolType == null)
                {
                    operation.MarkAsFailed($"Typ szkoły o ID '{schoolTypeToUpdate.Id}' nie istnieje.");
                    _logger.LogWarning("Nie można zaktualizować typu szkoły ID {SchoolTypeId} - nie istnieje.", schoolTypeToUpdate.Id);
                    return false;
                }
                operation.TargetEntityName = existingSchoolType.FullName; // Rzeczywista nazwa przed zmianą

                // Walidacja unikalności ShortName, jeśli jest zmieniana
                if (existingSchoolType.ShortName != schoolTypeToUpdate.ShortName)
                {
                    var conflicting = (await _schoolTypeRepository.FindAsync(st => st.Id != existingSchoolType.Id && st.ShortName == schoolTypeToUpdate.ShortName && st.IsActive)).FirstOrDefault();
                    if (conflicting != null)
                    {
                        operation.MarkAsFailed($"Typ szkoły o skróconej nazwie '{schoolTypeToUpdate.ShortName}' już istnieje.");
                        _logger.LogError("Nie można zaktualizować typu szkoły: Skrócona nazwa '{ShortName}' już istnieje.", schoolTypeToUpdate.ShortName);
                        return false;
                    }
                }

                existingSchoolType.ShortName = schoolTypeToUpdate.ShortName;
                existingSchoolType.FullName = schoolTypeToUpdate.FullName;
                existingSchoolType.Description = schoolTypeToUpdate.Description;
                existingSchoolType.ColorCode = schoolTypeToUpdate.ColorCode;
                existingSchoolType.SortOrder = schoolTypeToUpdate.SortOrder;
                existingSchoolType.IsActive = schoolTypeToUpdate.IsActive; // Pozwalamy na zmianę IsActive
                existingSchoolType.MarkAsModified(currentUserUpn);

                _schoolTypeRepository.Update(existingSchoolType);
                // SaveChangesAsync na wyższym poziomie
                operation.TargetEntityName = existingSchoolType.FullName; // Nazwa po zmianie
                operation.MarkAsCompleted($"Typ szkoły ID: {existingSchoolType.Id} przygotowany do aktualizacji.");
                _logger.LogInformation("Typ szkoły ID: {SchoolTypeId} pomyślnie przygotowany do aktualizacji.", existingSchoolType.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji typu szkoły ID {SchoolTypeId}. Wiadomość: {ErrorMessage}", schoolTypeToUpdate.Id, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> DeleteSchoolTypeAsync(string schoolTypeId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SchoolTypeDeleted,
                TargetEntityType = nameof(SchoolType),
                TargetEntityId = schoolTypeId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie usuwania (dezaktywacji) typu szkoły ID: {SchoolTypeId} przez {User}", schoolTypeId, currentUserUpn);

            try
            {
                var schoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeId);
                if (schoolType == null)
                {
                    operation.MarkAsFailed($"Typ szkoły o ID '{schoolTypeId}' nie istnieje.");
                    _logger.LogWarning("Nie można usunąć typu szkoły ID {SchoolTypeId} - nie istnieje.", schoolTypeId);
                    return false;
                }
                if (!schoolType.IsActive)
                {
                    operation.MarkAsCompleted($"Typ szkoły '{schoolType.FullName}' był już nieaktywny.");
                    _logger.LogInformation("Typ szkoły ID {SchoolTypeId} był już nieaktywny.", schoolTypeId);
                    return true;
                }
                operation.TargetEntityName = schoolType.FullName;

                // W DbContext relacje z Team, TeamTemplate, Subject mają OnDelete.SetNull,
                // a UserSchoolType i UserSchoolTypeSupervision (niejawna) mają OnDelete.Cascade.
                // "Miękkie" usunięcie SchoolType nie wywoła tych kaskad automatycznie,
                // ale powiązane encje będą wskazywać na nieaktywny SchoolType, co może być OK.
                // Jeśli chcemy "twarde" usunięcie z kaskadami, to Delete(schoolType), a nie MarkAsDeleted.
                // Na razie trzymamy się "soft delete" dla SchoolType.

                schoolType.MarkAsDeleted(currentUserUpn); // Ustawia IsActive = false
                _schoolTypeRepository.Update(schoolType);
                // SaveChangesAsync na wyższym poziomie

                operation.MarkAsCompleted($"Typ szkoły '{schoolType.FullName}' (ID: {schoolTypeId}) oznaczony jako usunięty.");
                _logger.LogInformation("Typ szkoły ID {SchoolTypeId} pomyślnie oznaczony jako usunięty.", schoolTypeId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania typu szkoły ID {SchoolTypeId}. Wiadomość: {ErrorMessage}", schoolTypeId, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> AssignViceDirectorToSchoolTypeAsync(string viceDirectorUserId, string schoolTypeId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_assign_vd";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.UserAssignedToSchoolType,
                TargetEntityType = "UserSchoolTypeSupervision",
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Przypisywanie wicedyrektora {ViceDirectorUserId} do typu szkoły {SchoolTypeId} przez {User}", viceDirectorUserId, schoolTypeId, currentUserUpn);

                var viceDirector = await _userRepository.GetByIdAsync(viceDirectorUserId);
                var schoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeId);

                if (viceDirector == null || !viceDirector.IsActive)
                {
                    operation.MarkAsFailed($"Użytkownik (wicedyrektor) o ID '{viceDirectorUserId}' nie istnieje lub jest nieaktywny.");
                    _logger.LogWarning("Nie można przypisać wicedyrektora: Użytkownik ID {ViceDirectorUserId} nie istnieje lub nieaktywny.", viceDirectorUserId);
                    return false;
                }
                if (viceDirector.Role != UserRole.Wicedyrektor && viceDirector.Role != UserRole.Dyrektor && !viceDirector.IsSystemAdmin)
                {
                    operation.MarkAsFailed($"Użytkownik '{viceDirector.UPN}' nie ma uprawnień wicedyrektora lub dyrektora.");
                    _logger.LogWarning("Nie można przypisać wicedyrektora: Użytkownik {ViceDirectorUPN} nie jest wicedyrektorem/dyrektorem.", viceDirector.UPN);
                    return false;
                }
                if (schoolType == null || !schoolType.IsActive)
                {
                    operation.MarkAsFailed($"Typ szkoły o ID '{schoolTypeId}' nie istnieje lub jest nieaktywny.");
                    _logger.LogWarning("Nie można przypisać wicedyrektora: Typ szkoły ID {SchoolTypeId} nie istnieje lub nieaktywny.", schoolTypeId);
                    return false;
                }
                operation.TargetEntityType = nameof(SchoolType); // Lub UserSchoolTypeSupervision
                operation.TargetEntityId = schoolTypeId;
                operation.TargetEntityName = $"Nadzór {viceDirector.UPN} nad {schoolType.ShortName}";

                // EF Core zarządza tabelą pośrednią dla relacji M:N User.SupervisedSchoolTypes <-> SchoolType.SupervisingViceDirectors.
                // Aby dodać powiązanie, musimy załadować kolekcje lub użyć EF.Core API do śledzenia.
                // Bezpośrednie dodanie do kolekcji powinno działać, jeśli obiekty są śledzone.
                // Potrzebujemy pełnego obiektu User z załadowaną kolekcją SupervisedSchoolTypes.
                var viceDirectorWithDetails = await _userRepository.GetByIdAsync(viceDirectorUserId); // Zakładamy, że GetByIdAsync dołącza SupervisedSchoolTypes
                if (viceDirectorWithDetails == null) return false; // Już sprawdzane, ale dla pewności

                if (viceDirectorWithDetails.SupervisedSchoolTypes.Any(st => st.Id == schoolTypeId))
                {
                    operation.MarkAsCompleted("Wicedyrektor był już przypisany do nadzoru tego typu szkoły.");
                    _logger.LogInformation("Wicedyrektor {ViceDirectorUPN} był już przypisany do nadzoru typu szkoły {SchoolTypeName}", viceDirector.UPN, schoolType.ShortName);
                    return true; // Uznajemy za sukces, bo cel osiągnięty
                }

                viceDirectorWithDetails.SupervisedSchoolTypes.Add(schoolType);
                // _userRepository.Update(viceDirectorWithDetails); // Oznaczamy User jako zmodyfikowany
                // Lub, jeśli relacja jest skonfigurowana dwukierunkowo w SchoolType:
                // schoolType.SupervisingViceDirectors.Add(viceDirectorWithDetails);
                // _schoolTypeRepository.Update(schoolType);

                // W przypadku niejawnej tabeli łączącej, EF Core wykryje zmianę w kolekcji nawigacyjnej
                // i utworzy odpowiedni wpis w tabeli UserSchoolTypeSupervision podczas SaveChanges.
                // Wystarczy oznaczyć jedną ze stron relacji jako zmodyfikowaną, jeśli obiekt był już śledzony.
                // Jeśli viceDirectorWithDetails został świeżo pobrany, może być już śledzony.
                // W przypadku wątpliwości, jawne Update jest bezpieczniejsze.
                _userRepository.Update(viceDirectorWithDetails);


                // SaveChangesAsync na wyższym poziomie

                operation.MarkAsCompleted("Wicedyrektor pomyślnie przypisany do nadzoru typu szkoły.");
                _logger.LogInformation("Wicedyrektor {ViceDirectorUPN} pomyślnie przypisany do nadzoru typu szkoły {SchoolTypeName}", viceDirector.UPN, schoolType.ShortName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas przypisywania wicedyrektora {ViceDirectorUserId} do typu szkoły {SchoolTypeId}. Wiadomość: {ErrorMessage}", viceDirectorUserId, schoolTypeId, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> RemoveViceDirectorFromSchoolTypeAsync(string viceDirectorUserId, string schoolTypeId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_vd";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.UserRemovedFromSchoolType,
                TargetEntityType = "UserSchoolTypeSupervision",
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            try
            {
                _logger.LogInformation("Usuwanie nadzoru wicedyrektora {ViceDirectorUserId} z typu szkoły {SchoolTypeId} przez {User}", viceDirectorUserId, schoolTypeId, currentUserUpn);

                var viceDirector = await _userRepository.GetByIdAsync(viceDirectorUserId); // Powinien dołączyć SupervisedSchoolTypes
                var schoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeId);

                if (viceDirector == null || schoolType == null)
                {
                    operation.MarkAsFailed("Wicedyrektor lub typ szkoły nie istnieje.");
                    _logger.LogWarning("Nie można usunąć nadzoru: Wicedyrektor ID {ViceDirectorUserId} lub Typ Szkoły ID {SchoolTypeId} nie istnieje.", viceDirectorUserId, schoolTypeId);
                    return false;
                }
                operation.TargetEntityType = nameof(SchoolType);
                operation.TargetEntityId = schoolTypeId;
                operation.TargetEntityName = $"Usunięcie nadzoru {viceDirector.UPN} nad {schoolType.ShortName}";

                // Sprawdzenie, czy przypisanie istnieje
                var supervisedSchoolTypeToRemove = viceDirector.SupervisedSchoolTypes.FirstOrDefault(st => st.Id == schoolTypeId);
                if (supervisedSchoolTypeToRemove == null)
                {
                    operation.MarkAsCompleted("Wicedyrektor nie nadzorował tego typu szkoły.");
                    _logger.LogInformation("Wicedyrektor {ViceDirectorUPN} nie nadzorował typu szkoły {SchoolTypeName}.", viceDirector.UPN, schoolType.ShortName);
                    return true; // Operacja "udana", bo nie było czego usuwać
                }

                viceDirector.SupervisedSchoolTypes.Remove(supervisedSchoolTypeToRemove);
                _userRepository.Update(viceDirector); // Oznaczamy User jako zmodyfikowany
                // SaveChangesAsync na wyższym poziomie

                operation.MarkAsCompleted("Pomyślnie usunięto przypisanie wicedyrektora z nadzoru typu szkoły.");
                _logger.LogInformation("Pomyślnie usunięto nadzór wicedyrektora {ViceDirectorUPN} z typu szkoły {SchoolTypeName}.", viceDirector.UPN, schoolType.ShortName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania nadzoru wicedyrektora {ViceDirectorUserId} z typu szkoły {SchoolTypeId}. Wiadomość: {ErrorMessage}", viceDirectorUserId, schoolTypeId, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        // Metoda pomocnicza do zapisu OperationHistory (bez SaveChanges)
        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id)) operation.Id = Guid.NewGuid().ToString();

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
                existingLog.TargetEntityName = operation.TargetEntityName; // Aktualizuj, jeśli się zmieniło
                existingLog.TargetEntityId = operation.TargetEntityId;   // Aktualizuj, jeśli się zmieniło
                existingLog.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_log_update");
                _operationHistoryRepository.Update(existingLog);
            }
            // SaveChangesAsync będzie na wyższym poziomie
        }
    }
}