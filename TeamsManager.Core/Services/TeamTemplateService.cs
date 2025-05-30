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
    public class TeamTemplateService : ITeamTemplateService
    {
        private readonly ITeamTemplateRepository _teamTemplateRepository;
        private readonly IGenericRepository<SchoolType> _schoolTypeRepository; // Potrzebne do walidacji SchoolTypeId
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<TeamTemplateService> _logger;
        // Brak bezpośredniej zależności od DbContext

        public TeamTemplateService(
            ITeamTemplateRepository teamTemplateRepository,
            IGenericRepository<SchoolType> schoolTypeRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<TeamTemplateService> logger)
        {
            _teamTemplateRepository = teamTemplateRepository ?? throw new ArgumentNullException(nameof(teamTemplateRepository));
            _schoolTypeRepository = schoolTypeRepository ?? throw new ArgumentNullException(nameof(schoolTypeRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TeamTemplate?> GetTemplateByIdAsync(string templateId)
        {
            _logger.LogInformation("Pobieranie szablonu zespołu o ID: {TemplateId}", templateId);
            // Repozytorium ITeamTemplateRepository może mieć własną logikę dołączania,
            // lub GetByIdAsync z GenericRepository zwróci podstawowy obiekt.
            // Jeśli SchoolType jest potrzebny, a nie jest dołączany, można go doładować tutaj.
            var template = await _teamTemplateRepository.GetByIdAsync(templateId);
            if (template != null && !template.IsUniversal && !string.IsNullOrEmpty(template.SchoolTypeId) && template.SchoolType == null)
            {
                template.SchoolType = await _schoolTypeRepository.GetByIdAsync(template.SchoolTypeId);
            }
            return template;
        }

        public async Task<IEnumerable<TeamTemplate>> GetAllActiveTemplatesAsync()
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych szablonów zespołów.");
            // Rozważ dołączenie SchoolType, jeśli jest często potrzebne
            // return await _teamTemplateRepository.FindAsync(t => t.IsActive, q => q.Include(t => t.SchoolType));
            return await _teamTemplateRepository.FindAsync(t => t.IsActive);
        }

        public async Task<IEnumerable<TeamTemplate>> GetUniversalTemplatesAsync()
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych szablonów uniwersalnych.");
            return await _teamTemplateRepository.GetUniversalTemplatesAsync();
        }

        public async Task<IEnumerable<TeamTemplate>> GetTemplatesBySchoolTypeAsync(string schoolTypeId)
        {
            _logger.LogInformation("Pobieranie aktywnych szablonów dla typu szkoły ID: {SchoolTypeId}", schoolTypeId);
            return await _teamTemplateRepository.GetTemplatesBySchoolTypeAsync(schoolTypeId);
        }

        public async Task<TeamTemplate?> GetDefaultTemplateForSchoolTypeAsync(string schoolTypeId)
        {
            _logger.LogInformation("Pobieranie domyślnego szablonu dla typu szkoły ID: {SchoolTypeId}", schoolTypeId);
            return await _teamTemplateRepository.GetDefaultTemplateForSchoolTypeAsync(schoolTypeId);
        }

        public async Task<TeamTemplate?> CreateTemplateAsync(
            string name,
            string templateContent,
            string description,
            bool isUniversal,
            string? schoolTypeId = null,
            string category = "Ogólne")
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.TeamTemplateCreated,
                TargetEntityType = nameof(TeamTemplate),
                TargetEntityName = name,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Rozpoczynanie tworzenia szablonu zespołu: '{TemplateName}' przez {User}", name, currentUserUpn);

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(templateContent))
                {
                    operation.MarkAsFailed("Nazwa szablonu i jego zawartość (wzorzec) są wymagane.");
                    _logger.LogError("Nie można utworzyć szablonu: Nazwa lub zawartość są puste.");
                    return null;
                }

                if (!isUniversal && string.IsNullOrWhiteSpace(schoolTypeId))
                {
                    operation.MarkAsFailed("Dla szablonu nieuniwersalnego wymagany jest typ szkoły (SchoolTypeId).");
                    _logger.LogError("Nie można utworzyć szablonu: Dla szablonu nieuniwersalnego SchoolTypeId jest wymagany.");
                    return null;
                }

                SchoolType? schoolType = null;
                if (!isUniversal && !string.IsNullOrWhiteSpace(schoolTypeId))
                {
                    schoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeId);
                    if (schoolType == null || !schoolType.IsActive)
                    {
                        operation.MarkAsFailed($"Typ szkoły o ID '{schoolTypeId}' podany dla szablonu nie istnieje lub jest nieaktywny.");
                        _logger.LogWarning("Nie można utworzyć szablonu: Typ szkoły ID {SchoolTypeId} nie istnieje lub jest nieaktywny.", schoolTypeId);
                        return null;
                    }
                }
                else if (isUniversal)
                {
                    schoolTypeId = null; // Upewnij się, że jest null dla uniwersalnego
                }

                // Walidacja samego szablonu
                var tempTemplateForValidation = new TeamTemplate { Template = templateContent };
                var validationErrors = tempTemplateForValidation.ValidateTemplate();
                if (validationErrors.Any())
                {
                    string errorsString = string.Join("; ", validationErrors);
                    operation.MarkAsFailed($"Błędy walidacji szablonu: {errorsString}");
                    _logger.LogError("Nie można utworzyć szablonu '{TemplateName}': Błędy walidacji wzorca: {ValidationErrors}", name, errorsString);
                    return null;
                }

                var newTemplate = new TeamTemplate
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Template = templateContent,
                    Description = description,
                    IsDefault = false, // Nowy szablon nie jest domyślnie domyślny
                    IsUniversal = isUniversal,
                    SchoolTypeId = isUniversal ? null : schoolTypeId,
                    SchoolType = isUniversal ? null : schoolType,
                    Category = category,
                    Language = "Polski", // Domyślna wartość lub z parametrów
                    Separator = " - ",   // Domyślna wartość lub z parametrów
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };

                await _teamTemplateRepository.AddAsync(newTemplate);
                // SaveChangesAsync na wyższym poziomie

                operation.TargetEntityId = newTemplate.Id;
                operation.MarkAsCompleted($"Szablon zespołu ID: {newTemplate.Id} ('{newTemplate.Name}') przygotowany do utworzenia.");
                _logger.LogInformation("Szablon zespołu '{TemplateName}' pomyślnie przygotowany do zapisu. ID: {TemplateId}", name, newTemplate.Id);
                return newTemplate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia szablonu zespołu {TemplateName}. Wiadomość: {ErrorMessage}", name, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return null;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> UpdateTemplateAsync(TeamTemplate templateToUpdate)
        {
            if (templateToUpdate == null || string.IsNullOrEmpty(templateToUpdate.Id))
                throw new ArgumentNullException(nameof(templateToUpdate), "Obiekt szablonu lub jego ID nie może być null/pusty.");

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory { /* Pełna inicjalizacja dla TeamTemplateUpdated */};
            operation.MarkAsStarted();
            _logger.LogInformation("Aktualizowanie szablonu ID: {TemplateId}", templateToUpdate.Id);

            try
            {
                var existingTemplate = await _teamTemplateRepository.GetByIdAsync(templateToUpdate.Id);
                if (existingTemplate == null)
                {
                    operation.MarkAsFailed("Szablon nie istnieje.");
                    _logger.LogWarning("Nie można zaktualizować szablonu ID {TemplateId} - nie istnieje.", templateToUpdate.Id);
                    return false;
                }
                operation.TargetEntityName = existingTemplate.Name;

                // Walidacja
                if (string.IsNullOrWhiteSpace(templateToUpdate.Name) || string.IsNullOrWhiteSpace(templateToUpdate.Template))
                {
                    operation.MarkAsFailed("Nazwa i wzorzec szablonu są wymagane.");
                    _logger.LogError("Błąd aktualizacji szablonu {TemplateId}: Nazwa lub wzorzec puste.", templateToUpdate.Id);
                    return false;
                }
                if (!templateToUpdate.IsUniversal && string.IsNullOrWhiteSpace(templateToUpdate.SchoolTypeId))
                {
                    operation.MarkAsFailed("Dla szablonu nieuniwersalnego wymagany jest typ szkoły (SchoolTypeId).");
                    _logger.LogError("Błąd aktualizacji szablonu {TemplateId}: Brak SchoolTypeId dla szablonu nieuniwersalnego.", templateToUpdate.Id);
                    return false;
                }
                var validationErrors = templateToUpdate.ValidateTemplate(); // Waliduj zaktualizowany wzorzec
                if (validationErrors.Any())
                {
                    operation.MarkAsFailed($"Błędy walidacji szablonu: {string.Join("; ", validationErrors)}");
                    _logger.LogError("Błąd aktualizacji szablonu {TemplateId}: Błędy walidacji wzorca.", templateToUpdate.Id);
                    return false;
                }

                SchoolType? schoolType = null;
                if (!templateToUpdate.IsUniversal && !string.IsNullOrWhiteSpace(templateToUpdate.SchoolTypeId))
                {
                    schoolType = await _schoolTypeRepository.GetByIdAsync(templateToUpdate.SchoolTypeId);
                    if (schoolType == null || !schoolType.IsActive)
                    {
                        operation.MarkAsFailed($"Typ szkoły o ID '{templateToUpdate.SchoolTypeId}' nie istnieje lub jest nieaktywny.");
                        _logger.LogWarning("Błąd aktualizacji szablonu {TemplateId}: Podany typ szkoły ID {SchoolTypeId} nieprawidłowy.", templateToUpdate.Id, templateToUpdate.SchoolTypeId);
                        return false;
                    }
                }


                // Aktualizacja pól
                existingTemplate.Name = templateToUpdate.Name;
                existingTemplate.Template = templateToUpdate.Template;
                existingTemplate.Description = templateToUpdate.Description;
                existingTemplate.IsDefault = templateToUpdate.IsDefault;
                existingTemplate.IsUniversal = templateToUpdate.IsUniversal;
                existingTemplate.SchoolTypeId = templateToUpdate.IsUniversal ? null : templateToUpdate.SchoolTypeId;
                existingTemplate.SchoolType = templateToUpdate.IsUniversal ? null : schoolType; // Ustawiamy na podstawie pobranego obiektu
                existingTemplate.ExampleOutput = templateToUpdate.ExampleOutput;
                existingTemplate.Category = templateToUpdate.Category;
                existingTemplate.Language = templateToUpdate.Language;
                existingTemplate.MaxLength = templateToUpdate.MaxLength;
                existingTemplate.RemovePolishChars = templateToUpdate.RemovePolishChars;
                existingTemplate.Prefix = templateToUpdate.Prefix;
                existingTemplate.Suffix = templateToUpdate.Suffix;
                existingTemplate.Separator = templateToUpdate.Separator;
                existingTemplate.SortOrder = templateToUpdate.SortOrder;
                existingTemplate.IsActive = templateToUpdate.IsActive;
                existingTemplate.MarkAsModified(currentUserUpn);

                _teamTemplateRepository.Update(existingTemplate);
                operation.MarkAsCompleted("Szablon przygotowany do aktualizacji.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji szablonu ID {TemplateId}. Wiadomość: {ErrorMessage}", templateToUpdate.Id, ex.Message);
                operation.MarkAsFailed($"Błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        public async Task<bool> DeleteTemplateAsync(string templateId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete";
            var operation = new OperationHistory { /* ... Inicjalizacja dla TeamTemplateDeleted ... */};
            operation.MarkAsStarted();
            try
            {
                var template = await _teamTemplateRepository.GetByIdAsync(templateId);
                if (template == null) { /* ... */ return false; }
                operation.TargetEntityName = template.Name;

                // TODO: Sprawdzić, czy szablon nie jest używany przez aktywne zespoły.
                // W DbContext relacja Team.TemplateId ma OnDelete.SetNull,
                // więc usunięcie szablonu ustawi TemplateId na null w zespołach.
                // Rozważenie logiki biznesowej, czy można usuwać szablon w użyciu.

                template.MarkAsDeleted(currentUserUpn);
                _teamTemplateRepository.Update(template);
                operation.MarkAsCompleted("Szablon oznaczony jako usunięty.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania szablonu zespołu ID {TemplateId}. Wiadomość: {ErrorMessage}", templateId, ex.Message);
                // Upewniamy się, że operation nie jest null, chociaż w tym przepływie powinno być zawsze zainicjowane
                if (operation != null)
                {
                    operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                }
                return false;
            }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        public async Task<string?> GenerateTeamNameFromTemplateAsync(string templateId, Dictionary<string, string> values)
        {
            _logger.LogInformation("Generowanie nazwy zespołu z szablonu ID: {TemplateId}", templateId);
            var template = await _teamTemplateRepository.GetByIdAsync(templateId);
            if (template == null || !template.IsActive)
            {
                _logger.LogWarning("Nie można wygenerować nazwy: Szablon o ID {TemplateId} nie istnieje lub jest nieaktywny.", templateId);
                return null;
            }
            // Metoda GenerateTeamName z TeamTemplate już wywołuje IncrementUsage.
            return template.GenerateTeamName(values);
        }

        public async Task<TeamTemplate?> CloneTemplateAsync(string originalTemplateId, string newTemplateName)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_clone";
            var operation = new OperationHistory { /* ... Inicjalizacja dla TeamTemplateCloned ... */ };
            operation.MarkAsStarted();
            _logger.LogInformation("Klonowanie szablonu ID: {OriginalTemplateId} do nowego szablonu o nazwie: {NewTemplateName}", originalTemplateId, newTemplateName);

            try
            {
                var originalTemplate = await _teamTemplateRepository.GetByIdAsync(originalTemplateId);
                if (originalTemplate == null)
                {
                    operation.MarkAsFailed("Oryginalny szablon nie istnieje.");
                    _logger.LogWarning("Nie można sklonować szablonu: Oryginalny szablon ID {OriginalTemplateId} nie istnieje.", originalTemplateId);
                    return null;
                }
                operation.TargetEntityId = originalTemplateId; // Logujemy na oryginalnym szablonie
                operation.TargetEntityName = $"Klonowanie: {originalTemplate.Name} -> {newTemplateName}";

                if (string.IsNullOrWhiteSpace(newTemplateName))
                {
                    operation.MarkAsFailed("Nowa nazwa szablonu nie może być pusta.");
                    _logger.LogError("Nie można sklonować szablonu: Nowa nazwa jest pusta.");
                    return null;
                }

                // Sprawdzenie, czy nazwa dla klona nie jest już zajęta (opcjonalnie)
                // var existingWithName = (await _teamTemplateRepository.FindAsync(t => t.Name == newTemplateName && t.IsActive)).FirstOrDefault();
                // if (existingWithName != null) { /* obsługa błędu */ }

                var clonedTemplate = originalTemplate.Clone(newTemplateName); // Użycie metody Clone z modelu
                clonedTemplate.CreatedBy = currentUserUpn; // Nadpisujemy pola audytu dla nowego obiektu
                clonedTemplate.Id = Guid.NewGuid().ToString(); // Nowe ID dla klona

                await _teamTemplateRepository.AddAsync(clonedTemplate);
                // SaveChangesAsync na wyższym poziomie

                operation.MarkAsCompleted($"Szablon ID: {originalTemplateId} sklonowany do nowego szablonu ID: {clonedTemplate.Id} ('{newTemplateName}').");
                _logger.LogInformation("Szablon '{OriginalName}' pomyślnie sklonowany do '{NewName}'. Nowe ID: {NewId}", originalTemplate.Name, newTemplateName, clonedTemplate.Id);
                return clonedTemplate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas klonowania szablonu ID {OriginalTemplateId}. Wiadomość: {ErrorMessage}", originalTemplateId, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return null;
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
            if (existingLog == null) await _operationHistoryRepository.AddAsync(operation);
            else { /* Logika aktualizacji existingLog */ _operationHistoryRepository.Update(existingLog); }
        }
    }
}