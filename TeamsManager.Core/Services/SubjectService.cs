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
    public class SubjectService : ISubjectService
    {
        private readonly IGenericRepository<Subject> _subjectRepository;
        private readonly IGenericRepository<SchoolType> _schoolTypeRepository; // Do walidacji DefaultSchoolTypeId
        private readonly IGenericRepository<UserSubject> _userSubjectRepository; // Do GetTeachersForSubjectAsync
        private readonly IUserRepository _userRepository; // Do GetTeachersForSubjectAsync
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SubjectService> _logger;

        public SubjectService(
            IGenericRepository<Subject> subjectRepository,
            IGenericRepository<SchoolType> schoolTypeRepository,
            IGenericRepository<UserSubject> userSubjectRepository,
            IUserRepository userRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<SubjectService> logger)
        {
            _subjectRepository = subjectRepository ?? throw new ArgumentNullException(nameof(subjectRepository));
            _schoolTypeRepository = schoolTypeRepository ?? throw new ArgumentNullException(nameof(schoolTypeRepository));
            _userSubjectRepository = userSubjectRepository ?? throw new ArgumentNullException(nameof(userSubjectRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Subject?> GetSubjectByIdAsync(string subjectId)
        {
            _logger.LogInformation("Pobieranie przedmiotu o ID: {SubjectId}", subjectId);
            // GetByIdAsync z GenericRepository nie dołącza DefaultSchoolType domyślnie.
            // Jeśli potrzebne, musielibyśmy stworzyć ISubjectRepository z dedykowaną metodą
            // lub załadować jawnie na wyższym poziomie.
            var subject = await _subjectRepository.GetByIdAsync(subjectId);
            if (subject != null && !string.IsNullOrEmpty(subject.DefaultSchoolTypeId) && subject.DefaultSchoolType == null)
            {
                // Próba doładowania, jeśli DefaultSchoolTypeId jest, a DefaultSchoolType nie (choć repo powinno to robić)
                subject.DefaultSchoolType = await _schoolTypeRepository.GetByIdAsync(subject.DefaultSchoolTypeId);
            }
            return subject;
        }

        public async Task<IEnumerable<Subject>> GetAllActiveSubjectsAsync()
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych przedmiotów.");
            // Można rozważyć dołączenie DefaultSchoolType, jeśli jest często potrzebne
            // return await _subjectRepository.FindAsync(s => s.IsActive, q => q.Include(s => s.DefaultSchoolType));
            return await _subjectRepository.FindAsync(s => s.IsActive);
        }

        public async Task<Subject?> CreateSubjectAsync(
            string name,
            string? code = null,
            string? description = null,
            int? hours = null,
            string? defaultSchoolTypeId = null,
            string? category = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SubjectCreated,
                TargetEntityType = nameof(Subject),
                TargetEntityName = name,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Rozpoczynanie tworzenia przedmiotu: '{SubjectName}' przez {User}", name, currentUserUpn);

                if (string.IsNullOrWhiteSpace(name))
                {
                    operation.MarkAsFailed("Nazwa przedmiotu nie może być pusta.");
                    _logger.LogError("Nie można utworzyć przedmiotu: Nazwa jest pusta.");
                    return null; // Zapis OperationHistory w bloku finally
                }

                // Opcjonalna walidacja unikalności nazwy lub kodu
                if (!string.IsNullOrWhiteSpace(code))
                {
                    var existingByCode = (await _subjectRepository.FindAsync(s => s.Code == code && s.IsActive)).FirstOrDefault();
                    if (existingByCode != null)
                    {
                        operation.MarkAsFailed($"Przedmiot o kodzie '{code}' już istnieje.");
                        _logger.LogError("Nie można utworzyć przedmiotu: Przedmiot o kodzie {SubjectCode} już istnieje.", code);
                        return null;
                    }
                }

                SchoolType? defaultSchoolType = null;
                if (!string.IsNullOrEmpty(defaultSchoolTypeId))
                {
                    defaultSchoolType = await _schoolTypeRepository.GetByIdAsync(defaultSchoolTypeId);
                    if (defaultSchoolType == null || !defaultSchoolType.IsActive)
                    {
                        _logger.LogWarning("Podany domyślny typ szkoły (ID: {DefaultSchoolTypeId}) dla przedmiotu '{SubjectName}' nie istnieje lub jest nieaktywny. Pole zostanie zignorowane.", defaultSchoolTypeId, name);
                        defaultSchoolTypeId = null; // Zignoruj niepoprawne ID
                        defaultSchoolType = null;
                    }
                }

                var newSubject = new Subject
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Code = code,
                    Description = description,
                    Hours = hours,
                    DefaultSchoolTypeId = defaultSchoolTypeId,
                    DefaultSchoolType = defaultSchoolType, // Dla EF Core
                    Category = category,
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };

                await _subjectRepository.AddAsync(newSubject);
                // SaveChangesAsync na wyższym poziomie

                operation.TargetEntityId = newSubject.Id;
                operation.MarkAsCompleted($"Przedmiot ID: {newSubject.Id} ('{newSubject.Name}') przygotowany do utworzenia.");
                _logger.LogInformation("Przedmiot '{SubjectName}' pomyślnie przygotowany do zapisu. ID: {SubjectId}", name, newSubject.Id);
                return newSubject;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia przedmiotu {SubjectName}. Wiadomość: {ErrorMessage}", name, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return null;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> UpdateSubjectAsync(Subject subjectToUpdate)
        {
            if (subjectToUpdate == null || string.IsNullOrEmpty(subjectToUpdate.Id))
                throw new ArgumentNullException(nameof(subjectToUpdate), "Obiekt przedmiotu lub jego ID nie może być null/pusty.");

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SubjectUpdated,
                TargetEntityType = nameof(Subject),
                TargetEntityId = subjectToUpdate.Id,
                TargetEntityName = subjectToUpdate.Name,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie aktualizacji przedmiotu ID: {SubjectId} przez {User}", subjectToUpdate.Id, currentUserUpn);

            try
            {
                var existingSubject = await _subjectRepository.GetByIdAsync(subjectToUpdate.Id);
                if (existingSubject == null)
                {
                    operation.MarkAsFailed($"Przedmiot o ID '{subjectToUpdate.Id}' nie istnieje.");
                    _logger.LogWarning("Nie można zaktualizować przedmiotu ID {SubjectId} - nie istnieje.", subjectToUpdate.Id);
                    return false;
                }
                operation.TargetEntityName = existingSubject.Name; // Rzeczywista nazwa przed zmianą

                if (string.IsNullOrWhiteSpace(subjectToUpdate.Name))
                {
                    operation.MarkAsFailed("Nazwa przedmiotu nie może być pusta.");
                    _logger.LogError("Błąd walidacji przy aktualizacji przedmiotu: Nazwa pusta.");
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(subjectToUpdate.Code) && existingSubject.Code != subjectToUpdate.Code)
                {
                    var conflicting = (await _subjectRepository.FindAsync(s => s.Id != existingSubject.Id && s.Code == subjectToUpdate.Code && s.IsActive)).FirstOrDefault();
                    if (conflicting != null)
                    {
                        operation.MarkAsFailed($"Przedmiot o kodzie '{subjectToUpdate.Code}' już istnieje.");
                        _logger.LogError("Nie można zaktualizować przedmiotu: Kod '{SubjectCode}' już istnieje.", subjectToUpdate.Code);
                        return false;
                    }
                }

                SchoolType? defaultSchoolType = null;
                if (!string.IsNullOrEmpty(subjectToUpdate.DefaultSchoolTypeId))
                {
                    defaultSchoolType = await _schoolTypeRepository.GetByIdAsync(subjectToUpdate.DefaultSchoolTypeId);
                    if (defaultSchoolType == null || !defaultSchoolType.IsActive)
                    {
                        _logger.LogWarning("Podany domyślny typ szkoły (ID: {DefaultSchoolTypeId}) dla przedmiotu '{SubjectName}' nie istnieje lub jest nieaktywny. Zmiana nie zostanie zastosowana dla DefaultSchoolTypeId.", subjectToUpdate.DefaultSchoolTypeId, subjectToUpdate.Name);
                        subjectToUpdate.DefaultSchoolTypeId = existingSubject.DefaultSchoolTypeId; // Przywróć stare ID
                        subjectToUpdate.DefaultSchoolType = existingSubject.DefaultSchoolType; // Przywróć stary obiekt
                    }
                    else
                    {
                        existingSubject.DefaultSchoolType = defaultSchoolType; // Ustaw nowy obiekt
                    }
                }
                else
                {
                    existingSubject.DefaultSchoolType = null; // Usuń powiązanie jeśli ID jest puste
                }

                existingSubject.Name = subjectToUpdate.Name;
                existingSubject.Code = subjectToUpdate.Code;
                existingSubject.Description = subjectToUpdate.Description;
                existingSubject.Hours = subjectToUpdate.Hours;
                existingSubject.DefaultSchoolTypeId = subjectToUpdate.DefaultSchoolTypeId;
                existingSubject.Category = subjectToUpdate.Category;
                existingSubject.IsActive = subjectToUpdate.IsActive;
                existingSubject.MarkAsModified(currentUserUpn);

                _subjectRepository.Update(existingSubject);
                // SaveChangesAsync na wyższym poziomie
                operation.TargetEntityName = existingSubject.Name; // Nazwa po zmianie
                operation.MarkAsCompleted($"Przedmiot ID: {existingSubject.Id} przygotowany do aktualizacji.");
                _logger.LogInformation("Przedmiot ID: {SubjectId} pomyślnie przygotowany do aktualizacji.", existingSubject.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji przedmiotu ID {SubjectId}. Wiadomość: {ErrorMessage}", subjectToUpdate.Id, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> DeleteSubjectAsync(string subjectId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete";
            var operation = new OperationHistory { /* ... Inicjalizacja dla SubjectDeleted ... */ };
            operation.MarkAsStarted();
            _logger.LogInformation("Usuwanie (dezaktywacja) przedmiotu ID: {SubjectId}", subjectId);
            try
            {
                var subject = await _subjectRepository.GetByIdAsync(subjectId);
                if (subject == null) { /* ... */ return false; }
                operation.TargetEntityName = subject.Name;

                // Zgodnie z DbContext, relacja UserSubject.SubjectId ma OnDelete.Cascade.
                // "Miękkie" usunięcie Subject nie usunie automatycznie UserSubject.
                // Jeśli chcemy usunąć przypisania nauczycieli, musimy to zrobić jawnie.
                // Na razie tylko "soft delete" samego przedmiotu.
                var assignments = await _userSubjectRepository.FindAsync(us => us.SubjectId == subjectId && us.IsActive);
                if (assignments.Any())
                {
                    // Można zdecydować, czy dezaktywować przypisania, czy zabronić usuwania przedmiotu.
                    // Na razie dezaktywujemy przypisania.
                    foreach (var assignment in assignments)
                    {
                        assignment.MarkAsDeleted(currentUserUpn);
                        _userSubjectRepository.Update(assignment);
                    }
                    _logger.LogInformation("Zdezaktywowano {Count} przypisań nauczycieli do przedmiotu {SubjectId}.", assignments.Count(), subjectId);
                }

                subject.MarkAsDeleted(currentUserUpn);
                _subjectRepository.Update(subject);
                operation.MarkAsCompleted("Przedmiot oznaczony jako usunięty wraz z przypisaniami.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania przedmiotu ID {SubjectId}. Wiadomość: {ErrorMessage}", subjectId, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        public async Task<IEnumerable<User>> GetTeachersForSubjectAsync(string subjectId)
        {
            _logger.LogInformation("Pobieranie nauczycieli dla przedmiotu ID: {SubjectId}", subjectId);
            var subject = await _subjectRepository.GetByIdAsync(subjectId);
            if (subject == null || !subject.IsActive)
            {
                _logger.LogWarning("Przedmiot o ID {SubjectId} nie istnieje lub jest nieaktywny.", subjectId);
                return Enumerable.Empty<User>();
            }

            // Pobieramy aktywne przypisania UserSubject dla danego przedmiotu
            var assignments = await _userSubjectRepository.FindAsync(us =>
                us.SubjectId == subjectId &&
                us.IsActive &&
                us.User != null && // Upewniamy się, że User jest załadowany (choć repo powinno to robić)
                us.User.IsActive); // I że sam użytkownik jest aktywny

            // Zwracamy listę unikalnych, aktywnych użytkowników z tych przypisań
            return assignments.Select(us => us.User).Where(u => u != null).Distinct().ToList()!;
        }

        // Metoda pomocnicza do zapisu OperationHistory (taka sama jak w innych serwisach)
        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id)) operation.Id = Guid.NewGuid().ToString();
            var existingLog = await _operationHistoryRepository.GetByIdAsync(operation.Id);
            if (existingLog == null) await _operationHistoryRepository.AddAsync(operation);
            else { /* Logika aktualizacji existingLog */ _operationHistoryRepository.Update(existingLog); }
        }
    }
}