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
    /// Serwis odpowiedzialny za logikę biznesową szablonów zespołów.
    /// Implementuje cache'owanie dla często odpytywanych danych.
    /// </summary>
    public class TeamTemplateService : ITeamTemplateService
    {
        private readonly ITeamTemplateRepository _teamTemplateRepository;
        private readonly IGenericRepository<SchoolType> _schoolTypeRepository;
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<TeamTemplateService> _logger;
        private readonly IMemoryCache _cache;

        // Klucze cache
        private const string AllTeamTemplatesCacheKey = "TeamTemplates_AllActive";
        private const string UniversalTeamTemplatesCacheKey = "TeamTemplates_UniversalActive";
        private const string TeamTemplatesBySchoolTypeIdCacheKeyPrefix = "TeamTemplates_BySchoolType_Id_";
        private const string DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix = "TeamTemplate_Default_BySchoolType_Id_";
        private const string TeamTemplateByIdCacheKeyPrefix = "TeamTemplate_Id_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromHours(1); // Szablony zmieniają się rzadko

        // Token do unieważniania cache'u dla szablonów zespołów
        private static CancellationTokenSource _teamTemplatesCacheTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Konstruktor serwisu szablonów zespołów.
        /// </summary>
        public TeamTemplateService(
            ITeamTemplateRepository teamTemplateRepository,
            IGenericRepository<SchoolType> schoolTypeRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<TeamTemplateService> logger,
            IMemoryCache memoryCache)
        {
            _teamTemplateRepository = teamTemplateRepository ?? throw new ArgumentNullException(nameof(teamTemplateRepository));
            _schoolTypeRepository = schoolTypeRepository ?? throw new ArgumentNullException(nameof(schoolTypeRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_teamTemplatesCacheTokenSource.Token));
        }

        /// <inheritdoc />
        public async Task<TeamTemplate?> GetTemplateByIdAsync(string templateId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie szablonu zespołu o ID: {TemplateId}. Wymuszenie odświeżenia: {ForceRefresh}", templateId, forceRefresh);
            if (string.IsNullOrWhiteSpace(templateId))
            {
                _logger.LogWarning("Próba pobrania szablonu z pustym ID.");
                return null;
            }

            string cacheKey = TeamTemplateByIdCacheKeyPrefix + templateId;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out TeamTemplate? cachedTemplate))
            {
                _logger.LogDebug("Szablon ID: {TemplateId} znaleziony w cache.", templateId);
                if (cachedTemplate != null && !cachedTemplate.IsUniversal && !string.IsNullOrEmpty(cachedTemplate.SchoolTypeId) && cachedTemplate.SchoolType == null)
                {
                    _logger.LogDebug("Dociąganie SchoolType dla szablonu {TemplateId} z cache.", templateId);
                    cachedTemplate.SchoolType = await _schoolTypeRepository.GetByIdAsync(cachedTemplate.SchoolTypeId);
                }
                return cachedTemplate;
            }

            _logger.LogDebug("Szablon ID: {TemplateId} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", templateId);
            var templateFromDb = await _teamTemplateRepository.GetByIdAsync(templateId);

            if (templateFromDb != null)
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
            }
            return templateFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<TeamTemplate>> GetAllActiveTemplatesAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych szablonów zespołów. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);

            if (!forceRefresh && _cache.TryGetValue(AllTeamTemplatesCacheKey, out IEnumerable<TeamTemplate>? cachedTemplates) && cachedTemplates != null)
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

            _cache.Set(AllTeamTemplatesCacheKey, templatesFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Wszystkie aktywne szablony dodane do cache.");
            return templatesFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<TeamTemplate>> GetUniversalTemplatesAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie aktywnych szablonów uniwersalnych. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);

            if (!forceRefresh && _cache.TryGetValue(UniversalTeamTemplatesCacheKey, out IEnumerable<TeamTemplate>? cachedTemplates) && cachedTemplates != null)
            {
                _logger.LogDebug("Uniwersalne szablony znalezione w cache.");
                return cachedTemplates;
            }

            _logger.LogDebug("Uniwersalne szablony nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var templatesFromDb = await _teamTemplateRepository.GetUniversalTemplatesAsync(); // Metoda repozytorium już filtruje po IsActive

            _cache.Set(UniversalTeamTemplatesCacheKey, templatesFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Uniwersalne szablony dodane do cache.");
            return templatesFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<TeamTemplate>> GetTemplatesBySchoolTypeAsync(string schoolTypeId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie aktywnych szablonów dla typu szkoły ID: {SchoolTypeId}. Wymuszenie odświeżenia: {ForceRefresh}", schoolTypeId, forceRefresh);
            if (string.IsNullOrWhiteSpace(schoolTypeId))
            {
                _logger.LogWarning("Próba pobrania szablonów dla pustego ID typu szkoły.");
                return Enumerable.Empty<TeamTemplate>();
            }

            string cacheKey = TeamTemplatesBySchoolTypeIdCacheKeyPrefix + schoolTypeId;
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<TeamTemplate>? cachedTemplates) && cachedTemplates != null)
            {
                _logger.LogDebug("Szablony dla typu szkoły ID: {SchoolTypeId} znalezione w cache.", schoolTypeId);
                return cachedTemplates;
            }

            _logger.LogDebug("Szablony dla typu szkoły ID: {SchoolTypeId} nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", schoolTypeId);
            var templatesFromDb = await _teamTemplateRepository.GetTemplatesBySchoolTypeAsync(schoolTypeId); // Metoda repozytorium już filtruje po IsActive

            // SchoolType powinien być już załadowany przez metodę repozytorium GetTemplatesBySchoolTypeAsync

            _cache.Set(cacheKey, templatesFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Szablony dla typu szkoły ID: {SchoolTypeId} dodane do cache.", schoolTypeId);
            return templatesFromDb;
        }

        /// <inheritdoc />
        public async Task<TeamTemplate?> GetDefaultTemplateForSchoolTypeAsync(string schoolTypeId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie domyślnego szablonu dla typu szkoły ID: {SchoolTypeId}. Wymuszenie odświeżenia: {ForceRefresh}", schoolTypeId, forceRefresh);
            if (string.IsNullOrWhiteSpace(schoolTypeId))
            {
                _logger.LogWarning("Próba pobrania domyślnego szablonu dla pustego ID typu szkoły.");
                return null;
            }

            string cacheKey = DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + schoolTypeId;
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out TeamTemplate? cachedTemplate)) // Może być null jeśli nie ma domyślnego
            {
                _logger.LogDebug("Domyślny szablon dla typu szkoły ID: {SchoolTypeId} (lub jego brak) znaleziony w cache.", schoolTypeId);
                return cachedTemplate;
            }

            _logger.LogDebug("Domyślny szablon dla typu szkoły ID: {SchoolTypeId} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", schoolTypeId);
            var templateFromDb = await _teamTemplateRepository.GetDefaultTemplateForSchoolTypeAsync(schoolTypeId); // Metoda repozytorium już filtruje po IsActive

            // SchoolType powinien być już załadowany przez metodę repozytorium GetDefaultTemplateForSchoolTypeAsync

            _cache.Set(cacheKey, templateFromDb, GetDefaultCacheEntryOptions()); // Cache'ujemy również null
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
                    schoolTypeId = null; // Upewniamy się, że jest null dla uniwersalnego
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
                    SchoolTypeId = schoolTypeId, // Używamy wyczyszczonego schoolTypeId
                    SchoolType = schoolType,     // I obiektu schoolType
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

                InvalidateCache(templateId: newTemplate.Id, schoolTypeId: newTemplate.SchoolTypeId, isUniversalOrAllAffected: true, isDefaultPotentiallyAffected: newTemplate.IsDefault, forSchoolTypeId: newTemplate.SchoolTypeId);
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

        /// <inheritdoc />
        public async Task<bool> UpdateTemplateAsync(TeamTemplate templateToUpdate)
        {
            if (templateToUpdate == null || string.IsNullOrEmpty(templateToUpdate.Id))
            {
                _logger.LogError("Próba aktualizacji szablonu zespołu z nieprawidłowymi danymi (null lub brak ID).");
                throw new ArgumentNullException(nameof(templateToUpdate), "Obiekt szablonu lub jego ID nie może być null/pusty.");
            }


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

            string? oldSchoolTypeId = null;
            bool oldIsDefault = false;
            bool oldIsUniversal = false;

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
                oldSchoolTypeId = existingTemplate.SchoolTypeId;
                oldIsDefault = existingTemplate.IsDefault;
                oldIsUniversal = existingTemplate.IsUniversal;


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
                else if (templateToUpdate.IsUniversal)
                {
                    templateToUpdate.SchoolTypeId = null; // Upewnijmy się, że jest null dla uniwersalnego
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
                            InvalidateCache(oldDefault.Id, oldDefault.SchoolTypeId, isDefaultPotentiallyAffected: true, forSchoolTypeId: oldDefault.SchoolTypeId);
                        }
                    }
                }

                existingTemplate.Name = templateToUpdate.Name;
                existingTemplate.Template = templateToUpdate.Template;
                existingTemplate.Description = templateToUpdate.Description;
                existingTemplate.IsDefault = templateToUpdate.IsDefault;
                existingTemplate.IsUniversal = templateToUpdate.IsUniversal;
                existingTemplate.SchoolTypeId = templateToUpdate.SchoolTypeId; // Już poprawnie ustawione wyżej
                existingTemplate.SchoolType = schoolType; // I powiązany obiekt
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

                // Inwalidacja cache
                InvalidateCache(existingTemplate.Id, existingTemplate.SchoolTypeId,
                                isUniversalOrAllAffected: existingTemplate.IsUniversal != oldIsUniversal,
                                isDefaultPotentiallyAffected: existingTemplate.IsDefault != oldIsDefault || (existingTemplate.IsDefault && existingTemplate.SchoolTypeId != oldSchoolTypeId),
                                forSchoolTypeId: existingTemplate.SchoolTypeId);
                if (oldSchoolTypeId != null && oldSchoolTypeId != existingTemplate.SchoolTypeId)
                {
                    InvalidateCache(schoolTypeId: oldSchoolTypeId, isDefaultPotentiallyAffected: oldIsDefault, forSchoolTypeId: oldSchoolTypeId);
                }

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

        /// <inheritdoc />
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
            TeamTemplate? template = null;
            try
            {
                template = await _teamTemplateRepository.GetByIdAsync(templateId);
                if (template == null)
                {
                    operation.MarkAsFailed($"Szablon o ID '{templateId}' nie istnieje.");
                    _logger.LogWarning("Nie można usunąć szablonu ID {TemplateId} - nie istnieje.", templateId);
                    return false;
                }
                operation.TargetEntityName = template.Name;

                if (!template.IsActive)
                {
                    operation.MarkAsCompleted($"Szablon '{template.Name}' był już nieaktywny. Brak akcji.");
                    _logger.LogInformation("Szablon ID {TemplateId} był już nieaktywny. Nie wykonano żadnej akcji.", templateId);
                    InvalidateCache(templateId, template.SchoolTypeId, template.IsUniversal, template.IsDefault, template.SchoolTypeId);
                    return true;
                }

                template.MarkAsDeleted(currentUserUpn);
                _teamTemplateRepository.Update(template);
                operation.MarkAsCompleted("Szablon oznaczony jako usunięty.");

                InvalidateCache(templateId, template.SchoolTypeId, template.IsUniversal, template.IsDefault, template.SchoolTypeId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania szablonu zespołu ID {TemplateId}. Wiadomość: {ErrorMessage}", templateId, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<string?> GenerateTeamNameFromTemplateAsync(string templateId, Dictionary<string, string> values)
        {
            _logger.LogInformation("Generowanie nazwy zespołu z szablonu ID: {TemplateId}", templateId);
            // Używamy metody GetTemplateByIdAsync, która korzysta z cache'u
            var template = await GetTemplateByIdAsync(templateId);
            if (template == null || !template.IsActive)
            {
                _logger.LogWarning("Nie można wygenerować nazwy: Szablon o ID {TemplateId} nie istnieje lub jest nieaktywny.", templateId);
                return null;
            }
            // Metoda GenerateTeamName w modelu TeamTemplate już inkrementuje UsageCount.
            // Jeśli chcemy, aby serwis był odpowiedzialny za utrwalenie tej zmiany,
            // musielibyśmy tu dodać _teamTemplateRepository.Update(template); i SaveChangesAsync() na wyższym poziomie.
            // Na razie zakładamy, że samo wywołanie GenerateTeamName jest wystarczające, a stan licznika
            // zostanie utrwalony przy następnej pełnej aktualizacji szablonu lub explicite.
            var generatedName = template.GenerateTeamName(values);
            // Po wygenerowaniu nazwy (co zmodyfikowało UsageCount i LastUsedDate),
            // powinniśmy zaktualizować obiekt w repozytorium.
            _teamTemplateRepository.Update(template);
            // I unieważnić jego wpis w cache, aby następne pobranie odzwierciedliło zmiany.
            InvalidateCache(templateId, template.SchoolTypeId, template.IsUniversal, template.IsDefault, template.SchoolTypeId);

            return generatedName;
        }

        /// <inheritdoc />
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
                // Używamy GetTemplateByIdAsync, które może skorzystać z cache'u
                var originalTemplate = await GetTemplateByIdAsync(originalTemplateId);
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

                InvalidateCache(templateId: clonedTemplate.Id, schoolTypeId: clonedTemplate.SchoolTypeId, isUniversalOrAllAffected: true, isDefaultPotentiallyAffected: clonedTemplate.IsDefault, forSchoolTypeId: clonedTemplate.SchoolTypeId);
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

        /// <inheritdoc />
        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a szablonów zespołów.");
            InvalidateCache(invalidateAll: true);
            _logger.LogInformation("Cache szablonów zespołów został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        // Prywatna metoda do unieważniania cache.
        private void InvalidateCache(string? templateId = null, string? schoolTypeId = null, bool isUniversalOrAllAffected = false, bool isDefaultPotentiallyAffected = false, string? forSchoolTypeId = null, bool invalidateAll = false)
        {
            _logger.LogDebug("Inwalidacja cache'u szablonów. templateId: {TemplateId}, schoolTypeId: {SchoolTypeId}, isUniversalOrAllAffected: {IsUniversalOrAllAffected}, isDefaultPotentiallyAffected: {IsDefaultPotentiallyAffected}, forSchoolTypeId: {ForSchoolTypeId}, invalidateAll: {InvalidateAll}",
                templateId, schoolTypeId, isUniversalOrAllAffected, isDefaultPotentiallyAffected, forSchoolTypeId, invalidateAll);

            var oldTokenSource = Interlocked.Exchange(ref _teamTemplatesCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
            _logger.LogDebug("Token cache'u dla szablonów zespołów został zresetowany.");


            // Granularne usuwanie jest mniej istotne przy użyciu CancellationTokenSource,
            // ale może być przydatne dla natychmiastowego usunięcia konkretnych kluczy, jeśli są znane.
            // Token zajmie się resztą przy następnym wygaśnięciu lub odwołaniu.
            if (invalidateAll)
            {
                _cache.Remove(AllTeamTemplatesCacheKey);
                _cache.Remove(UniversalTeamTemplatesCacheKey);
                _logger.LogDebug("Usunięto z cache klucze dla wszystkich szablonów i szablonów uniwersalnych.");
                // W przypadku `invalidateAll`, nie ma potrzeby usuwać bardziej specyficznych kluczy poniżej,
                // bo token i tak unieważni wszystko. Można by je tu pominąć.
            }

            if (!string.IsNullOrWhiteSpace(templateId))
            {
                _cache.Remove(TeamTemplateByIdCacheKeyPrefix + templateId);
                _logger.LogDebug("Usunięto z cache szablon o ID: {TemplateId}", templateId);
            }
            if (!string.IsNullOrWhiteSpace(schoolTypeId))
            {
                _cache.Remove(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + schoolTypeId);
                _logger.LogDebug("Usunięto z cache szablony dla typu szkoły ID: {SchoolTypeId}", schoolTypeId);
            }
            if (isDefaultPotentiallyAffected && !string.IsNullOrWhiteSpace(forSchoolTypeId))
            {
                _cache.Remove(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + forSchoolTypeId);
                _logger.LogDebug("Usunięto z cache domyślny szablon dla typu szkoły ID: {ForSchoolTypeId}", forSchoolTypeId);
            }
            if (isUniversalOrAllAffected && !invalidateAll) // Jeśli nie było już globalnej inwalidacji
            {
                _cache.Remove(AllTeamTemplatesCacheKey);
                _cache.Remove(UniversalTeamTemplatesCacheKey);
                _logger.LogDebug("Usunięto z cache klucze dla wszystkich szablonów i szablonów uniwersalnych z powodu flagi isUniversalOrAllAffected.");
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