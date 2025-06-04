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
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Services.Cache;

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Serwis odpowiedzialny za logikę biznesową szablonów zespołów.
    /// Implementuje cache'owanie dla często odpytywanych danych.
    /// </summary>
    public class TeamTemplateService : ITeamTemplateService
    {
        private readonly ITeamTemplateRepository _teamTemplateRepository;
        private readonly IGenericRepository<SchoolType> _schoolTypeRepository; // Potrzebne do walidacji i dociągania SchoolType
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<TeamTemplateService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IPowerShellCacheService _powerShellCacheService;

        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromHours(1); // Szablony zmieniają się rzadko

        // Token do unieważniania cache'u dla szablonów zespołów

        /// <summary>
        /// Konstruktor serwisu szablonów zespołów.
        /// </summary>
        public TeamTemplateService(
            ITeamTemplateRepository teamTemplateRepository,
            IGenericRepository<SchoolType> schoolTypeRepository,
            IOperationHistoryService operationHistoryService,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            ILogger<TeamTemplateService> logger,
            IMemoryCache memoryCache,
            IPowerShellCacheService powerShellCacheService)
        {
            _teamTemplateRepository = teamTemplateRepository ?? throw new ArgumentNullException(nameof(teamTemplateRepository));
            _schoolTypeRepository = schoolTypeRepository ?? throw new ArgumentNullException(nameof(schoolTypeRepository));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _powerShellCacheService = powerShellCacheService ?? throw new ArgumentNullException(nameof(powerShellCacheService));
        }

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            // Delegacja do PowerShellCacheService
            return _powerShellCacheService.GetDefaultCacheEntryOptions();
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<TeamTemplate?> GetTemplateByIdAsync(string templateId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie szablonu zespołu o ID: {TemplateId}. Wymuszenie odświeżenia: {ForceRefresh}", templateId, forceRefresh);
            if (string.IsNullOrWhiteSpace(templateId))
            {
                _logger.LogWarning("Próba pobrania szablonu z pustym ID.");
                return null;
            }

            string cacheKey = TeamTemplateCacheKeys.TeamTemplateById(templateId);

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out TeamTemplate? cachedTemplate))
            {
                _logger.LogDebug("Szablon ID: {TemplateId} znaleziony w cache.", templateId);
                // Dociągnięcie SchoolType, jeśli nie było załadowane, a jest potrzebne
                if (cachedTemplate != null && !cachedTemplate.IsUniversal && !string.IsNullOrEmpty(cachedTemplate.SchoolTypeId) && cachedTemplate.SchoolType == null)
                {
                    _logger.LogDebug("Dociąganie SchoolType dla szablonu {TemplateId} z cache.", templateId);
                    cachedTemplate.SchoolType = await _schoolTypeRepository.GetByIdAsync(cachedTemplate.SchoolTypeId);
                }
                return cachedTemplate;
            }

            _logger.LogDebug("Szablon ID: {TemplateId} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", templateId);
            // Repozytorium powinno załadować SchoolType, jeśli jest to część domyślnego zapytania GetByIdAsync dla TeamTemplate
            // lub jeśli mamy dedykowaną metodę GetByIdWithDetailsAsync w ITeamTemplateRepository
            var templateFromDb = await _teamTemplateRepository.GetByIdAsync(templateId); // Zakładamy, że to ładuje SchoolType lub jest OK

            if (templateFromDb != null && templateFromDb.IsActive) // Cache'ujemy tylko aktywne szablony
            {
                if (!templateFromDb.IsUniversal && !string.IsNullOrEmpty(templateFromDb.SchoolTypeId) && templateFromDb.SchoolType == null)
                {
                    templateFromDb.SchoolType = await _schoolTypeRepository.GetByIdAsync(templateFromDb.SchoolTypeId);
                }
                _cache.Set(cacheKey, templateFromDb, GetDefaultCacheEntryOptions());
                _logger.LogDebug("Szablon ID: {TemplateId} dodany do cache.", templateId);
            }
            else
            {
                _cache.Remove(cacheKey);
                if (templateFromDb != null && !templateFromDb.IsActive)
                {
                    _logger.LogDebug("Szablon ID: {TemplateId} jest nieaktywny, nie zostanie zcache'owany po ID.", templateId);
                    return null;
                }
            }
            return templateFromDb;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<IEnumerable<TeamTemplate>> GetAllActiveTemplatesAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych szablonów zespołów. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);

            if (!forceRefresh && _cache.TryGetValue(TeamTemplateCacheKeys.AllActiveTeamTemplates, out IEnumerable<TeamTemplate>? cachedTemplates) && cachedTemplates != null)
            {
                _logger.LogDebug("Wszystkie aktywne szablony znalezione w cache.");
                return cachedTemplates;
            }

            _logger.LogDebug("Wszystkie aktywne szablony nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var templatesFromDb = await _teamTemplateRepository.FindAsync(t => t.IsActive);

            foreach (var template in templatesFromDb.Where(t => !t.IsUniversal && !string.IsNullOrEmpty(t.SchoolTypeId) && t.SchoolType == null))
            {
                template.SchoolType = await _schoolTypeRepository.GetByIdAsync(template.SchoolTypeId!);
            }

            _cache.Set(TeamTemplateCacheKeys.AllActiveTeamTemplates, templatesFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Wszystkie aktywne szablony dodane do cache.");
            return templatesFromDb;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<IEnumerable<TeamTemplate>> GetUniversalTemplatesAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie aktywnych szablonów uniwersalnych. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);

            if (!forceRefresh && _cache.TryGetValue(TeamTemplateCacheKeys.UniversalTeamTemplates, out IEnumerable<TeamTemplate>? cachedTemplates) && cachedTemplates != null)
            {
                _logger.LogDebug("Uniwersalne szablony znalezione w cache.");
                return cachedTemplates;
            }

            _logger.LogDebug("Uniwersalne szablony nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var templatesFromDb = await _teamTemplateRepository.GetUniversalTemplatesAsync(); // Metoda repozytorium już filtruje po IsActive i IsUniversal

            _cache.Set(TeamTemplateCacheKeys.UniversalTeamTemplates, templatesFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Uniwersalne szablony dodane do cache.");
            return templatesFromDb;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<IEnumerable<TeamTemplate>> GetTemplatesBySchoolTypeAsync(string schoolTypeId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie aktywnych szablonów dla typu szkoły ID: {SchoolTypeId}. Wymuszenie odświeżenia: {ForceRefresh}", schoolTypeId, forceRefresh);
            if (string.IsNullOrWhiteSpace(schoolTypeId))
            {
                _logger.LogWarning("Próba pobrania szablonów dla pustego ID typu szkoły.");
                return Enumerable.Empty<TeamTemplate>();
            }

            string cacheKey = TeamTemplateCacheKeys.TeamTemplatesBySchoolType(schoolTypeId);
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<TeamTemplate>? cachedTemplates) && cachedTemplates != null)
            {
                _logger.LogDebug("Szablony dla typu szkoły ID: {SchoolTypeId} znalezione w cache.", schoolTypeId);
                return cachedTemplates;
            }

            _logger.LogDebug("Szablony dla typu szkoły ID: {SchoolTypeId} nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", schoolTypeId);
            var templatesFromDb = await _teamTemplateRepository.GetTemplatesBySchoolTypeAsync(schoolTypeId); // Metoda repozytorium już filtruje po IsActive i SchoolTypeId

            _cache.Set(cacheKey, templatesFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Szablony dla typu szkoły ID: {SchoolTypeId} dodane do cache.", schoolTypeId);
            return templatesFromDb;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<TeamTemplate?> GetDefaultTemplateForSchoolTypeAsync(string schoolTypeId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie domyślnego szablonu dla typu szkoły ID: {SchoolTypeId}. Wymuszenie odświeżenia: {ForceRefresh}", schoolTypeId, forceRefresh);
            if (string.IsNullOrWhiteSpace(schoolTypeId))
            {
                _logger.LogWarning("Próba pobrania domyślnego szablonu dla pustego ID typu szkoły.");
                return null;
            }

            string cacheKey = TeamTemplateCacheKeys.DefaultTeamTemplateBySchoolType(schoolTypeId);
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out TeamTemplate? cachedTemplate))
            {
                _logger.LogDebug("Domyślny szablon dla typu szkoły ID: {SchoolTypeId} (lub jego brak) znaleziony w cache.", schoolTypeId);
                return cachedTemplate;
            }

            _logger.LogDebug("Domyślny szablon dla typu szkoły ID: {SchoolTypeId} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", schoolTypeId);
            var templateFromDb = await _teamTemplateRepository.GetDefaultTemplateForSchoolTypeAsync(schoolTypeId);

            _cache.Set(cacheKey, templateFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Domyślny szablon dla typu szkoły ID: {SchoolTypeId} (lub jego brak) dodany do cache.", schoolTypeId);
            return templateFromDb;
        }

        /// <inheritdoc />
        public async Task<TeamTemplate?> CreateTemplateAsync(
            string name,
            string templateContent,
            string description,
            bool isUniversal,
            string? schoolTypeId = null,
            string category = "Ogólne")
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Rozpoczynanie tworzenia szablonu zespołu: '{TemplateName}' przez {User}", name, currentUserUpn);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.TeamTemplateCreated,
                nameof(TeamTemplate),
                targetEntityName: name
            );

            try
            {
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(templateContent))
                {
                    _logger.LogError("Nie można utworzyć szablonu: Nazwa lub zawartość są puste.");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nazwa szablonu i jego zawartość (wzorzec) są wymagane."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można utworzyć szablonu: nazwa i zawartość są wymagane",
                        "error"
                    );
                    return null;
                }

                if (!isUniversal && string.IsNullOrWhiteSpace(schoolTypeId))
                {
                    _logger.LogError("Nie można utworzyć szablonu: Dla szablonu nieuniwersalnego SchoolTypeId jest wymagany.");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Dla szablonu nieuniwersalnego wymagany jest typ szkoły (SchoolTypeId)."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można utworzyć szablonu: dla szablonu nieuniwersalnego wymagany jest typ szkoły",
                        "error"
                    );
                    return null;
                }

                SchoolType? schoolType = null;
                if (!isUniversal && !string.IsNullOrWhiteSpace(schoolTypeId))
                {
                    schoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeId);
                    if (schoolType == null || !schoolType.IsActive)
                    {
                        _logger.LogWarning("Nie można utworzyć szablonu: Typ szkoły ID {SchoolTypeId} nie istnieje lub jest nieaktywny.", schoolTypeId);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            $"Typ szkoły o ID '{schoolTypeId}' podany dla szablonu nie istnieje lub jest nieaktywny."
                        );

                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            "Nie można utworzyć szablonu: wybrany typ szkoły nie istnieje lub jest nieaktywny",
                            "error"
                        );
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
                    _logger.LogError("Nie można utworzyć szablonu '{TemplateName}': Błędy walidacji wzorca: {ValidationErrors}", name, errorsString);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Błędy walidacji szablonu: {errorsString}"
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Nie można utworzyć szablonu: błędy walidacji wzorca - {errorsString}",
                        "error"
                    );
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
                    SchoolTypeId = schoolTypeId,
                    SchoolType = schoolType,
                    Category = category,
                    Language = "Polski",
                    Separator = " - ",
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };

                await _teamTemplateRepository.AddAsync(newTemplate);

                // NOWA LINIA - zapewnienie zapisu do bazy
                await _teamTemplateRepository.SaveChangesAsync();

                _logger.LogInformation("Szablon zespołu '{TemplateName}' pomyślnie przygotowany do zapisu. ID: {TemplateId}", name, newTemplate.Id);

                InvalidateCache(templateId: newTemplate.Id, schoolTypeId: newTemplate.SchoolTypeId);
                
                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Szablon zespołu '{newTemplate.Name}' utworzony pomyślnie"
                );

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Szablon zespołu '{newTemplate.Name}' został utworzony",
                    "success"
                );
                return newTemplate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia szablonu zespołu {TemplateName}. Wiadomość: {ErrorMessage}", name, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );

                // 4. Powiadomienie o błędzie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Nie udało się utworzyć szablonu zespołu: {ex.Message}",
                    "error"
                );
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateTemplateAsync(TeamTemplate templateToUpdate)
        {
            if (templateToUpdate == null || string.IsNullOrEmpty(templateToUpdate.Id))
            {
                _logger.LogError("Próba aktualizacji szablonu zespołu z nieprawidłowymi danymi (null lub brak ID).");
                throw new ArgumentNullException(nameof(templateToUpdate), "Obiekt szablonu lub jego ID nie może być null/pusty.");
            }

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Aktualizowanie szablonu ID: {TemplateId}", templateToUpdate.Id);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.TeamTemplateUpdated,
                nameof(TeamTemplate),
                targetEntityId: templateToUpdate.Id,
                targetEntityName: templateToUpdate.Name
            );

            string? oldSchoolTypeId = null;
            bool oldIsDefault = false;
            bool oldIsUniversal = false;

            try
            {
                var existingTemplate = await _teamTemplateRepository.GetByIdAsync(templateToUpdate.Id);
                if (existingTemplate == null)
                {
                    _logger.LogWarning("Nie można zaktualizować szablonu ID {TemplateId} - nie istnieje.", templateToUpdate.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Szablon nie istnieje."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można zaktualizować szablonu: nie istnieje w systemie",
                        "error"
                    );
                    return false;
                }
                
                oldSchoolTypeId = existingTemplate.SchoolTypeId;
                oldIsDefault = existingTemplate.IsDefault;
                oldIsUniversal = existingTemplate.IsUniversal;

                if (string.IsNullOrWhiteSpace(templateToUpdate.Name) || string.IsNullOrWhiteSpace(templateToUpdate.Template))
                {
                    _logger.LogError("Błąd aktualizacji szablonu {TemplateId}: Nazwa lub wzorzec puste.", templateToUpdate.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nazwa i wzorzec szablonu są wymagane."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można zaktualizować szablonu: nazwa i wzorzec są wymagane",
                        "error"
                    );
                    return false;
                }
                
                if (!templateToUpdate.IsUniversal && string.IsNullOrWhiteSpace(templateToUpdate.SchoolTypeId))
                {
                    _logger.LogError("Błąd aktualizacji szablonu {TemplateId}: Brak SchoolTypeId dla szablonu nieuniwersalnego.", templateToUpdate.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Dla szablonu nieuniwersalnego wymagany jest typ szkoły (SchoolTypeId)."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można zaktualizować szablonu: dla szablonu nieuniwersalnego wymagany jest typ szkoły",
                        "error"
                    );
                    return false;
                }
                
                var validationErrors = templateToUpdate.ValidateTemplate();
                if (validationErrors.Any())
                {
                    string errorsString = string.Join("; ", validationErrors);
                    _logger.LogError("Błąd aktualizacji szablonu {TemplateId}: Błędy walidacji wzorca.", templateToUpdate.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Błędy walidacji szablonu: {errorsString}"
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Nie można zaktualizować szablonu: błędy walidacji wzorca - {errorsString}",
                        "error"
                    );
                    return false;
                }

                SchoolType? schoolType = null;
                if (!templateToUpdate.IsUniversal && !string.IsNullOrWhiteSpace(templateToUpdate.SchoolTypeId))
                {
                    schoolType = await _schoolTypeRepository.GetByIdAsync(templateToUpdate.SchoolTypeId);
                    if (schoolType == null || !schoolType.IsActive)
                    {
                        _logger.LogWarning("Błąd aktualizacji szablonu {TemplateId}: Podany typ szkoły ID {SchoolTypeId} nieprawidłowy.", templateToUpdate.Id, templateToUpdate.SchoolTypeId);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            $"Typ szkoły o ID '{templateToUpdate.SchoolTypeId}' nie istnieje lub jest nieaktywny."
                        );

                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            "Nie można zaktualizować szablonu: wybrany typ szkoły nie istnieje lub jest nieaktywny",
                            "error"
                        );
                        return false;
                    }
                }
                else if (templateToUpdate.IsUniversal)
                {
                    templateToUpdate.SchoolTypeId = null;
                    schoolType = null;
                }

                // Logika obsługi zmiany flagi IsDefault
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
                            InvalidateCache(templateId: oldDefault.Id, schoolTypeId: oldDefault.SchoolTypeId);
                        }
                    }
                }

                existingTemplate.Name = templateToUpdate.Name;
                existingTemplate.Template = templateToUpdate.Template;
                existingTemplate.Description = templateToUpdate.Description;
                existingTemplate.IsDefault = templateToUpdate.IsDefault;
                existingTemplate.IsUniversal = templateToUpdate.IsUniversal;
                existingTemplate.SchoolTypeId = templateToUpdate.SchoolTypeId;
                existingTemplate.SchoolType = schoolType;
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

                // NOWA LINIA - zapewnienie zapisu do bazy
                await _teamTemplateRepository.SaveChangesAsync();

                InvalidateCache(templateId: existingTemplate.Id,
                                schoolTypeId: existingTemplate.SchoolTypeId,
                                oldSchoolTypeId: oldSchoolTypeId);

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Szablon '{existingTemplate.Name}' zaktualizowany pomyślnie"
                );

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Szablon '{existingTemplate.Name}' został zaktualizowany",
                    "success"
                );
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji szablonu ID {TemplateId}. Wiadomość: {ErrorMessage}", templateToUpdate.Id, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );

                // 4. Powiadomienie o błędzie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Błąd podczas aktualizacji szablonu: {ex.Message}",
                    "error"
                );
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteTemplateAsync(string templateId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Usuwanie (dezaktywacja) szablonu ID: {TemplateId}", templateId);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.TeamTemplateDeleted,
                nameof(TeamTemplate),
                targetEntityId: templateId
            );

            TeamTemplate? template = null;
            try
            {
                template = await _teamTemplateRepository.GetByIdAsync(templateId);
                if (template == null)
                {
                    _logger.LogWarning("Nie można usunąć szablonu ID {TemplateId} - nie istnieje.", templateId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Szablon o ID '{templateId}' nie istnieje."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można usunąć szablonu: nie istnieje w systemie",
                        "error"
                    );
                    return false;
                }

                if (!template.IsActive)
                {
                    _logger.LogInformation("Szablon ID {TemplateId} był już nieaktywny. Nie wykonano żadnej akcji.", templateId);
                    InvalidateCache(templateId: templateId, schoolTypeId: template.SchoolTypeId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Szablon '{template.Name}' był już nieaktywny."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Szablon '{template.Name}' był już nieaktywny",
                        "info"
                    );
                    return true;
                }

                template.MarkAsDeleted(currentUserUpn);
                _teamTemplateRepository.Update(template);

                // NOWA LINIA - zapewnienie zapisu do bazy
                await _teamTemplateRepository.SaveChangesAsync();

                InvalidateCache(templateId: templateId, schoolTypeId: template.SchoolTypeId);
                
                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Szablon '{template.Name}' oznaczony jako usunięty."
                );

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Szablon '{template.Name}' został usunięty",
                    "success"
                );
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania szablonu zespołu ID {TemplateId}. Wiadomość: {ErrorMessage}", templateId, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );

                // 4. Powiadomienie o błędzie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Błąd podczas usuwania szablonu: {ex.Message}",
                    "error"
                );
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<string?> GenerateTeamNameFromTemplateAsync(string templateId, Dictionary<string, string> values)
        {
            _logger.LogInformation("Generowanie nazwy zespołu z szablonu ID: {TemplateId}", templateId);
            var template = await GetTemplateByIdAsync(templateId);
            if (template == null || !template.IsActive)
            {
                _logger.LogWarning("Nie można wygenerować nazwy: Szablon o ID {TemplateId} nie istnieje lub jest nieaktywny.", templateId);
                return null;
            }

            var generatedName = template.GenerateTeamName(values);

            _teamTemplateRepository.Update(template);

            // NOWA LINIA - zapewnienie zapisu do bazy
            await _teamTemplateRepository.SaveChangesAsync();

            // Log informacyjny o zapisie
            _logger.LogDebug("Statystyki użycia szablonu ID: {TemplateId} zapisane do bazy. UsageCount: {UsageCount}, LastUsedDate: {LastUsedDate}", 
                template.Id, template.UsageCount, template.LastUsedDate);

            // Tylko szablon został zmodyfikowany, nie trzeba inwalidować list
            InvalidateCache(templateId: template.Id, onlyTemplateCache: true);

            return generatedName;
        }

        /// <inheritdoc />
        public async Task<TeamTemplate?> CloneTemplateAsync(string originalTemplateId, string newTemplateName)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Klonowanie szablonu ID: {OriginalTemplateId} do nowego szablonu o nazwie: {NewTemplateName}", originalTemplateId, newTemplateName);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.TeamTemplateCloned,
                nameof(TeamTemplate),
                targetEntityId: originalTemplateId,
                targetEntityName: $"Klonowanie -> {newTemplateName}"
            );

            try
            {
                var originalTemplate = await GetTemplateByIdAsync(originalTemplateId);
                if (originalTemplate == null)
                {
                    _logger.LogWarning("Nie można sklonować szablonu: Oryginalny szablon ID {OriginalTemplateId} nie istnieje.", originalTemplateId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Oryginalny szablon nie istnieje."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można sklonować szablonu: oryginalny szablon nie istnieje",
                        "error"
                    );
                    return null;
                }

                if (string.IsNullOrWhiteSpace(newTemplateName))
                {
                    _logger.LogError("Nie można sklonować szablonu: Nowa nazwa jest pusta.");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nowa nazwa szablonu nie może być pusta."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można sklonować szablonu: nowa nazwa nie może być pusta",
                        "error"
                    );
                    return null;
                }

                var existingWithName = (await _teamTemplateRepository.FindAsync(t => t.Name == newTemplateName && t.IsActive)).FirstOrDefault();
                if (existingWithName != null)
                {
                    _logger.LogWarning("Nie można sklonować szablonu: Szablon o nazwie '{NewTemplateName}' już istnieje i jest aktywny.", newTemplateName);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Szablon o nazwie '{newTemplateName}' już istnieje i jest aktywny."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Nie można sklonować szablonu: nazwa '{newTemplateName}' już istnieje",
                        "error"
                    );
                    return null;
                }

                var clonedTemplate = originalTemplate.Clone(newTemplateName);
                clonedTemplate.CreatedBy = currentUserUpn;
                clonedTemplate.Id = Guid.NewGuid().ToString();

                await _teamTemplateRepository.AddAsync(clonedTemplate);

                // NOWA LINIA - zapewnienie zapisu do bazy
                await _teamTemplateRepository.SaveChangesAsync();

                _logger.LogInformation("Szablon '{OriginalTemplateName}' pomyślnie sklonowany jako '{NewTemplateName}'. Nowy ID: {ClonedTemplateId}", originalTemplate.Name, newTemplateName, clonedTemplate.Id);

                InvalidateCache(templateId: clonedTemplate.Id, schoolTypeId: clonedTemplate.SchoolTypeId);
                
                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Szablon '{originalTemplate.Name}' sklonowany jako '{clonedTemplate.Name}'"
                );

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Szablon '{originalTemplate.Name}' został sklonowany jako '{clonedTemplate.Name}'",
                    "success"
                );
                return clonedTemplate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas klonowania szablonu ID {OriginalTemplateId}. Wiadomość: {ErrorMessage}", originalTemplateId, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );

                // 4. Powiadomienie o błędzie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Nie udało się sklonować szablonu: {ex.Message}",
                    "error"
                );
                return null;
            }
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda unieważnia globalny cache dla szablonów zespołów.</remarks>
        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a szablonów zespołów.");
            InvalidateCache(invalidateAll: true);
            _logger.LogInformation("Cache szablonów zespołów został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Unieważnia cache dla szablonów zespołów w sposób granularny.
        /// Deleguje zarządzanie do PowerShellCacheService eliminując problem "Thundering Herd".
        /// </summary>
        /// <param name="templateId">ID szablonu, którego specyficzny cache ma być usunięty (opcjonalnie).</param>
        /// <param name="schoolTypeId">ID typu szkoły, którego cache szablonów ma być usunięty (opcjonalnie).</param>
        /// <param name="isUniversal">Czy operacja dotyczyła szablonu uniwersalnego (opcjonalnie).</param>
        /// <param name="isDefault">Czy operacja dotyczyła szablonu domyślnego (opcjonalnie).</param>
        /// <param name="oldSchoolTypeId">Poprzedni ID typu szkoły, jeśli uległ zmianie (opcjonalnie).</param>
        /// <param name="oldIsUniversal">Poprzedni stan flagi IsUniversal, jeśli uległ zmianie (opcjonalnie).</param>
        /// <param name="oldIsDefault">Poprzedni stan flagi IsDefault, jeśli uległ zmianie (opcjonalnie).</param>
        /// <param name="invalidateAll">Czy unieważnić wszystkie klucze związane z szablonami (opcjonalnie, domyślnie false).</param>
        /// <param name="onlyTemplateCache">Czy inwalidować tylko cache konkretnego szablonu, bez list (domyślnie false).</param>
        private void InvalidateCache(
            string? templateId = null,
            string? schoolTypeId = null,
            bool? isUniversal = null,
            bool? isDefault = null,
            string? oldSchoolTypeId = null,
            bool? oldIsUniversal = null,
            bool? oldIsDefault = null,
            bool invalidateAll = false,
            bool onlyTemplateCache = false)
        {
            _logger.LogDebug("Granularna inwalidacja cache szablonów. templateId: {TemplateId}, schoolTypeId: {SchoolTypeId}, " +
                "isUniversal: {IsUniversal}, isDefault: {IsDefault}, oldSchoolTypeId: {OldSchoolTypeId}, " +
                "oldIsUniversal: {OldIsUniversal}, oldIsDefault: {OldIsDefault}, invalidateAll: {InvalidateAll}, onlyTemplateCache: {OnlyTemplateCache}",
                templateId, schoolTypeId, isUniversal, isDefault, oldSchoolTypeId, oldIsUniversal, oldIsDefault, invalidateAll, onlyTemplateCache);

            // Jeśli wymuszono pełną inwalidację (tylko dla RefreshCacheAsync)
            if (invalidateAll)
            {
                _logger.LogDebug("Wymuszono pełną inwalidację cache (invalidateAll=true)");
                _powerShellCacheService.InvalidateAllCache();
                return; // Pełna inwalidacja obejmuje wszystko
            }
            
            // Inwalidacja konkretnego szablonu (zawsze gdy podano templateId)
            if (!string.IsNullOrWhiteSpace(templateId))
            {
                _powerShellCacheService.InvalidateTeamTemplateById(templateId);
            }
            
            // Jeśli tylko cache szablonu (GenerateTeamNameFromTemplateAsync), nie inwaliduj list
            if (onlyTemplateCache)
            {
                return;
            }
            
            // Granularna inwalidacja - inwaliduj listy dla operacji strukturalnych
            _powerShellCacheService.InvalidateAllActiveTeamTemplatesList();
            
            // Inwalidacja według typu szkoły (gdy operacja wpływa na listy typu szkoły)
            if (!string.IsNullOrWhiteSpace(schoolTypeId))
            {
                _powerShellCacheService.InvalidateTeamTemplatesBySchoolType(schoolTypeId);
            }
            
            // Inwalidacja starego typu szkoły (przy zmianie)
            if (!string.IsNullOrWhiteSpace(oldSchoolTypeId) && oldSchoolTypeId != schoolTypeId)
            {
                _powerShellCacheService.InvalidateTeamTemplatesBySchoolType(oldSchoolTypeId);
            }
        }
    }
}