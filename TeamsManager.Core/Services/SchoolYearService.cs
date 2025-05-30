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
    public class SchoolYearService : ISchoolYearService
    {
        private readonly ISchoolYearRepository _schoolYearRepository;
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SchoolYearService> _logger;

        public SchoolYearService(
            ISchoolYearRepository schoolYearRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<SchoolYearService> logger)
        {
            _schoolYearRepository = schoolYearRepository ?? throw new ArgumentNullException(nameof(schoolYearRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SchoolYear?> GetSchoolYearByIdAsync(string schoolYearId)
        {
            _logger.LogInformation("Pobieranie roku szkolnego o ID: {SchoolYearId}", schoolYearId);
            return await _schoolYearRepository.GetByIdAsync(schoolYearId);
        }

        public async Task<IEnumerable<SchoolYear>> GetAllActiveSchoolYearsAsync()
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych lat szkolnych.");
            return await _schoolYearRepository.FindAsync(sy => sy.IsActive);
        }

        public async Task<SchoolYear?> GetCurrentSchoolYearAsync()
        {
            _logger.LogInformation("Pobieranie bieżącego roku szkolnego.");
            return await _schoolYearRepository.GetCurrentSchoolYearAsync();
        }

        public async Task<bool> SetCurrentSchoolYearAsync(string schoolYearId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_set_current_sy";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SchoolYearSetAsCurrent,
                TargetEntityType = nameof(SchoolYear),
                TargetEntityId = schoolYearId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie ustawiania roku szkolnego ID: {SchoolYearId} jako bieżący, przez {User}", schoolYearId, currentUserUpn);

            try
            {
                var newCurrentSchoolYear = await _schoolYearRepository.GetByIdAsync(schoolYearId);
                if (newCurrentSchoolYear == null || !newCurrentSchoolYear.IsActive)
                {
                    operation.MarkAsFailed($"Rok szkolny o ID '{schoolYearId}' nie istnieje lub jest nieaktywny.");
                    _logger.LogWarning("Nie można ustawić roku szkolnego ID {SchoolYearId} jako bieżący - nie istnieje lub nieaktywny.", schoolYearId);
                    return false;
                }
                operation.TargetEntityName = newCurrentSchoolYear.Name;

                // Rozpoczynamy transakcję (koncepcyjnie, SaveChanges będzie na wyższym poziomie)
                // W przypadku błędu, idealnie byłoby wycofać wszystkie zmiany.

                var currentlyActiveYears = await _schoolYearRepository.FindAsync(sy => sy.IsCurrent && sy.Id != schoolYearId && sy.IsActive);
                bool changesMade = false;

                foreach (var oldCurrentYear in currentlyActiveYears)
                {
                    oldCurrentYear.IsCurrent = false;
                    oldCurrentYear.MarkAsModified(currentUserUpn);
                    _schoolYearRepository.Update(oldCurrentYear);
                    changesMade = true;
                    _logger.LogInformation("Rok szkolny {OldSchoolYearName} (ID: {OldSchoolYearId}) został odznaczony jako bieżący.", oldCurrentYear.Name, oldCurrentYear.Id);
                }

                if (!newCurrentSchoolYear.IsCurrent)
                {
                    newCurrentSchoolYear.IsCurrent = true;
                    newCurrentSchoolYear.MarkAsModified(currentUserUpn);
                    _schoolYearRepository.Update(newCurrentSchoolYear);
                    changesMade = true;
                    _logger.LogInformation("Rok szkolny {NewSchoolYearName} (ID: {NewSchoolYearId}) został ustawiony jako bieżący.", newCurrentSchoolYear.Name, newCurrentSchoolYear.Id);
                }
                else
                {
                    _logger.LogInformation("Rok szkolny {NewSchoolYearName} (ID: {NewSchoolYearId}) był już ustawiony jako bieżący.", newCurrentSchoolYear.Name, newCurrentSchoolYear.Id);
                    // Jeśli nic innego się nie zmieniło, a był już current, to operacja jest "udana" bez zmian.
                    if (!changesMade) operation.MarkAsCompleted($"Rok szkolny '{newCurrentSchoolYear.Name}' (ID: {schoolYearId}) był już bieżący. Brak zmian.");
                }

                if (changesMade)
                {
                    operation.MarkAsCompleted($"Rok szkolny '{newCurrentSchoolYear.Name}' (ID: {schoolYearId}) ustawiony jako bieżący. Poprzednie odznaczone.");
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas ustawiania roku szkolnego ID {SchoolYearId} jako bieżący. Wiadomość: {ErrorMessage}", schoolYearId, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<SchoolYear?> CreateSchoolYearAsync(
            string name,
            DateTime startDate,
            DateTime endDate,
            string? description = null,
            DateTime? firstSemesterStart = null,
            DateTime? firstSemesterEnd = null,
            DateTime? secondSemesterStart = null,
            DateTime? secondSemesterEnd = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SchoolYearCreated,
                TargetEntityType = nameof(SchoolYear),
                TargetEntityName = name,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Rozpoczynanie tworzenia roku szkolnego: {Name} przez {User}", name, currentUserUpn);

                if (string.IsNullOrWhiteSpace(name))
                {
                    operation.MarkAsFailed("Nazwa roku szkolnego nie może być pusta.");
                    _logger.LogError("Nie można utworzyć roku szkolnego: Nazwa jest pusta.");
                    return null;
                }
                if (startDate >= endDate)
                {
                    operation.MarkAsFailed("Data rozpoczęcia musi być wcześniejsza niż data zakończenia.");
                    _logger.LogError("Nie można utworzyć roku szkolnego: Data rozpoczęcia ({StartDate}) nie jest wcześniejsza niż data zakończenia ({EndDate}).", startDate, endDate);
                    return null;
                }

                var existing = await _schoolYearRepository.GetSchoolYearByNameAsync(name);
                if (existing != null && existing.IsActive) // Sprawdzamy tylko aktywne, czy można mieć nieaktywny o tej samej nazwie?
                {
                    operation.MarkAsFailed($"Aktywny rok szkolny o nazwie '{name}' już istnieje.");
                    _logger.LogWarning("Aktywny rok szkolny o nazwie '{Name}' już istnieje.", name);
                    return null;
                }

                var newSchoolYear = new SchoolYear
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    StartDate = startDate.Date, // Przechowujemy tylko część datową
                    EndDate = endDate.Date,     // Przechowujemy tylko część datową
                    Description = description ?? string.Empty,
                    FirstSemesterStart = firstSemesterStart?.Date,
                    FirstSemesterEnd = firstSemesterEnd?.Date,
                    SecondSemesterStart = secondSemesterStart?.Date,
                    SecondSemesterEnd = secondSemesterEnd?.Date,
                    IsCurrent = false, // Nowy rok nie jest domyślnie bieżący
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };

                await _schoolYearRepository.AddAsync(newSchoolYear);
                // SaveChangesAsync na wyższym poziomie

                operation.TargetEntityId = newSchoolYear.Id;
                operation.MarkAsCompleted($"Rok szkolny '{newSchoolYear.Name}' (ID: {newSchoolYear.Id}) przygotowany do utworzenia.");
                _logger.LogInformation("Rok szkolny '{Name}' pomyślnie przygotowany do zapisu. ID: {SchoolYearId}", name, newSchoolYear.Id);
                return newSchoolYear;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia roku szkolnego {Name}. Wiadomość: {ErrorMessage}", name, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return null;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> UpdateSchoolYearAsync(SchoolYear schoolYearToUpdate)
        {
            if (schoolYearToUpdate == null || string.IsNullOrEmpty(schoolYearToUpdate.Id))
                throw new ArgumentNullException(nameof(schoolYearToUpdate), "Obiekt roku szkolnego lub jego ID nie może być null/pusty.");

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SchoolYearUpdated,
                TargetEntityType = nameof(SchoolYear),
                TargetEntityId = schoolYearToUpdate.Id,
                TargetEntityName = schoolYearToUpdate.Name, // Nazwa PRZED modyfikacją
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie aktualizacji roku szkolnego ID: {SchoolYearId} przez {User}", schoolYearToUpdate.Id, currentUserUpn);

            try
            {
                var existingSchoolYear = await _schoolYearRepository.GetByIdAsync(schoolYearToUpdate.Id);
                if (existingSchoolYear == null)
                {
                    operation.MarkAsFailed($"Rok szkolny o ID '{schoolYearToUpdate.Id}' nie istnieje.");
                    _logger.LogWarning("Nie można zaktualizować roku szkolnego ID {SchoolYearId} - nie istnieje.", schoolYearToUpdate.Id);
                    return false;
                }
                operation.TargetEntityName = existingSchoolYear.Name; // Rzeczywista nazwa przed zmianą

                if (string.IsNullOrWhiteSpace(schoolYearToUpdate.Name) || schoolYearToUpdate.StartDate >= schoolYearToUpdate.EndDate)
                {
                    operation.MarkAsFailed("Niepoprawne dane wejściowe (nazwa, daty).");
                    _logger.LogError("Błąd walidacji przy aktualizacji roku szkolnego: {SchoolYearId}", schoolYearToUpdate.Id);
                    return false;
                }

                if (existingSchoolYear.Name != schoolYearToUpdate.Name)
                {
                    var conflicting = await _schoolYearRepository.GetSchoolYearByNameAsync(schoolYearToUpdate.Name);
                    if (conflicting != null && conflicting.Id != existingSchoolYear.Id)
                    {
                        operation.MarkAsFailed($"Rok szkolny o nazwie '{schoolYearToUpdate.Name}' już istnieje.");
                        _logger.LogWarning("Rok szkolny o nazwie '{Name}' już istnieje (inny ID).", schoolYearToUpdate.Name);
                        return false;
                    }
                }

                existingSchoolYear.Name = schoolYearToUpdate.Name;
                existingSchoolYear.StartDate = schoolYearToUpdate.StartDate.Date;
                existingSchoolYear.EndDate = schoolYearToUpdate.EndDate.Date;
                existingSchoolYear.Description = schoolYearToUpdate.Description ?? string.Empty;
                existingSchoolYear.FirstSemesterStart = schoolYearToUpdate.FirstSemesterStart?.Date;
                existingSchoolYear.FirstSemesterEnd = schoolYearToUpdate.FirstSemesterEnd?.Date;
                existingSchoolYear.SecondSemesterStart = schoolYearToUpdate.SecondSemesterStart?.Date;
                existingSchoolYear.SecondSemesterEnd = schoolYearToUpdate.SecondSemesterEnd?.Date;
                // IsCurrent jest zarządzane przez SetCurrentSchoolYearAsync, nie pozwalamy na bezpośrednią zmianę tutaj
                // existingSchoolYear.IsCurrent = schoolYearToUpdate.IsCurrent; 
                existingSchoolYear.IsActive = schoolYearToUpdate.IsActive;
                existingSchoolYear.MarkAsModified(currentUserUpn);

                _schoolYearRepository.Update(existingSchoolYear);
                // SaveChangesAsync na wyższym poziomie
                operation.TargetEntityName = existingSchoolYear.Name; // Nazwa po zmianie
                operation.MarkAsCompleted($"Rok szkolny ID: {existingSchoolYear.Id} przygotowany do aktualizacji.");
                _logger.LogInformation("Rok szkolny ID: {SchoolYearId} pomyślnie przygotowany do aktualizacji.", existingSchoolYear.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji roku szkolnego ID {SchoolYearId}. Wiadomość: {ErrorMessage}", schoolYearToUpdate.Id, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> DeleteSchoolYearAsync(string schoolYearId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SchoolYearDeleted,
                TargetEntityType = nameof(SchoolYear),
                TargetEntityId = schoolYearId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie usuwania (dezaktywacji) roku szkolnego ID: {SchoolYearId} przez {User}", schoolYearId, currentUserUpn);

            try
            {
                var schoolYear = await _schoolYearRepository.GetByIdAsync(schoolYearId);
                if (schoolYear == null)
                {
                    operation.MarkAsFailed($"Rok szkolny o ID '{schoolYearId}' nie istnieje.");
                    _logger.LogWarning("Nie można usunąć roku szkolnego ID {SchoolYearId} - nie istnieje.", schoolYearId);
                    return false;
                }
                if (!schoolYear.IsActive)
                {
                    operation.MarkAsCompleted($"Rok szkolny '{schoolYear.Name}' był już nieaktywny.");
                    _logger.LogInformation("Rok szkolny ID {SchoolYearId} był już nieaktywny.", schoolYearId);
                    return true;
                }
                if (schoolYear.IsCurrent)
                {
                    operation.MarkAsFailed("Nie można usunąć (dezaktywować) bieżącego roku szkolnego. Najpierw ustaw inny rok jako bieżący.");
                    _logger.LogWarning("Nie można usunąć/dezaktywować bieżącego roku szkolnego ID {SchoolYearId}.", schoolYearId);
                    return false;
                }
                operation.TargetEntityName = schoolYear.Name;

                // TODO: Sprawdzenie, czy rok szkolny nie jest używany przez aktywne zespoły.
                // Zgodnie z DbContext, relacja Team.SchoolYearId ma OnDelete.SetNull.
                // Jeśli chcemy zapobiec dezaktywacji roku z aktywnymi zespołami, musimy dodać tu logikę.
                // Na razie pozwalamy na soft delete.

                schoolYear.MarkAsDeleted(currentUserUpn); // Ustawia IsActive = false
                _schoolYearRepository.Update(schoolYear);
                // SaveChangesAsync na wyższym poziomie

                operation.MarkAsCompleted($"Rok szkolny '{schoolYear.Name}' (ID: {schoolYearId}) oznaczony jako usunięty.");
                _logger.LogInformation("Rok szkolny ID {SchoolYearId} pomyślnie oznaczony jako usunięty.", schoolYearId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania roku szkolnego ID {SchoolYearId}. Wiadomość: {ErrorMessage}", schoolYearId, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        // Metoda pomocnicza do zapisu OperationHistory (taka sama jak w innych serwisach)
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
                existingLog.TargetEntityName = operation.TargetEntityName;
                existingLog.TargetEntityId = operation.TargetEntityId;
                existingLog.ProcessedItems = operation.ProcessedItems;
                existingLog.FailedItems = operation.FailedItems;
                existingLog.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_log_update");
                _operationHistoryRepository.Update(existingLog);
            }
            // SaveChangesAsync będzie na wyższym poziomie
        }
    }
}