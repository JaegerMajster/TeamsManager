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
                    schoolTypeId = null;
                }

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
                    IsDefault = false,
                    IsUniversal = isUniversal,
                    SchoolTypeId = isUniversal ? null : schoolTypeId,
                    SchoolType = isUniversal ? null : schoolType,
                    Category = category,
                    Language = "Polski",
                    Separator = " - ",
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };

                await _teamTemplateRepository.AddAsync(newTemplate);

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
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.TeamTemplateUpdated,
                TargetEntityType = nameof(TeamTemplate),
                TargetEntityId = templateToUpdate.Id,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
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
                var validationErrors = templateToUpdate.ValidateTemplate();
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

                if (templateToUpdate.IsDefault && !existingTemplate.IsDefault)
                {
                    if (!templateToUpdate.IsUniversal && !string.IsNullOrEmpty(templateToUpdate.SchoolTypeId))
                    {
                        var otherDefaultTemplates = await _teamTemplateRepository.FindAsync(t =>
                            t.SchoolTypeId == templateToUpdate.SchoolTypeId &&
                            t.IsDefault &&
                            t.Id != templateToUpdate.Id &&
                            t.IsActive);

                        foreach (var oldDefault in otherDefaultTemplates)
                        {
                            oldDefault.IsDefault = false;
                            oldDefault.MarkAsModified(currentUserUpn);
                            _teamTemplateRepository.Update(oldDefault);
                            _logger.LogInformation("Odznaczono szablon ID: {OldDefaultTemplateId} jako domyślny dla typu szkoły ID: {SchoolTypeId}.", oldDefault.Id, templateToUpdate.SchoolTypeId);
                        }
                    }
                }

                existingTemplate.Name = templateToUpdate.Name;
                existingTemplate.Template = templateToUpdate.Template;
                existingTemplate.Description = templateToUpdate.Description;
                existingTemplate.IsDefault = templateToUpdate.IsDefault;
                existingTemplate.IsUniversal = templateToUpdate.IsUniversal;
                existingTemplate.SchoolTypeId = templateToUpdate.IsUniversal ? null : templateToUpdate.SchoolTypeId;
                existingTemplate.SchoolType = templateToUpdate.IsUniversal ? null : schoolType;
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
                operation.TargetEntityName = existingTemplate.Name;
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
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.TeamTemplateDeleted,
                TargetEntityType = nameof(TeamTemplate),
                TargetEntityId = templateId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Usuwanie (dezaktywacja) szablonu ID: {TemplateId}", templateId);
            try
            {
                var template = await _teamTemplateRepository.GetByIdAsync(templateId);
                if (template == null)
                {
                    operation.MarkAsFailed($"Szablon o ID '{templateId}' nie istnieje.");
                    _logger.LogWarning("Nie można usunąć szablonu ID {TemplateId} - nie istnieje.", templateId);
                    return false; // Dodano return false
                }
                operation.TargetEntityName = template.Name;

                // --- POCZĄTEK POPRAWIONEJ LOGIKI ---
                if (!template.IsActive)
                {
                    operation.MarkAsCompleted($"Szablon '{template.Name}' był już nieaktywny. Brak akcji.");
                    _logger.LogInformation("Szablon ID {TemplateId} był już nieaktywny. Nie wykonano żadnej akcji.", templateId);
                    return true; // Uznajemy za sukces, bo cel (nieaktywny szablon) jest osiągnięty
                }
                // --- KONIEC POPRAWIONEJ LOGIKI ---


                template.MarkAsDeleted(currentUserUpn);
                _teamTemplateRepository.Update(template);
                operation.MarkAsCompleted("Szablon oznaczony jako usunięty.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania szablonu zespołu ID {TemplateId}. Wiadomość: {ErrorMessage}", templateId, ex.Message);
                if (operation != null)
                {
                    operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                }
                return false;
            }
            finally
            {
                if (operation != null)
                {
                    await SaveOperationHistoryAsync(operation);
                }
            }
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
            return template.GenerateTeamName(values);
        }

        public async Task<TeamTemplate?> CloneTemplateAsync(string originalTemplateId, string newTemplateName)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_clone";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.TeamTemplateCloned,
                TargetEntityType = nameof(TeamTemplate),
                TargetEntityId = originalTemplateId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
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
                operation.TargetEntityName = $"Klonowanie: {originalTemplate.Name} -> {newTemplateName}";

                if (string.IsNullOrWhiteSpace(newTemplateName))
                {
                    operation.MarkAsFailed("Nowa nazwa szablonu nie może być pusta.");
                    _logger.LogError("Nie można sklonować szablonu: Nowa nazwa jest pusta.");
                    return null;
                }

                var existingWithName = (await _teamTemplateRepository.FindAsync(t => t.Name == newTemplateName && t.IsActive)).FirstOrDefault();
                if (existingWithName != null)
                {
                    operation.MarkAsFailed($"Szablon o nazwie '{newTemplateName}' już istnieje i jest aktywny. Klonowanie przerwane.");
                    _logger.LogWarning("Nie można sklonować szablonu: Szablon o nazwie '{NewTemplateName}' już istnieje i jest aktywny.", newTemplateName);
                    return null;
                }

                var clonedTemplate = originalTemplate.Clone(newTemplateName);
                clonedTemplate.CreatedBy = currentUserUpn;
                clonedTemplate.Id = Guid.NewGuid().ToString();

                await _teamTemplateRepository.AddAsync(clonedTemplate);

                operation.OperationDetails = $"Szablon ID: {originalTemplateId} ('{originalTemplate.Name}') sklonowany do nowego szablonu ID: {clonedTemplate.Id} ('{clonedTemplate.Name}').";
                operation.MarkAsCompleted(operation.OperationDetails);
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
