using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Serwis odpowiedzialny za zarządzanie ustawieniami aplikacji.
    /// Implementuje mechanizm cache'owania dla poprawy wydajności.
    /// </summary>
    public class ApplicationSettingService : IApplicationSettingService
    {
        private readonly IApplicationSettingRepository _settingsRepository;
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ApplicationSettingService> _logger;
        private readonly IPowerShellCacheService _powerShellCacheService;

        // Definicje kluczy cache
        private const string AllSettingsCacheKey = "ApplicationSettings_AllActive";
        private const string SettingsByCategoryCacheKeyPrefix = "ApplicationSettings_Category_";
        private const string SettingByKeyCacheKeyPrefix = "ApplicationSetting_Key_";

        /// <summary>
        /// Konstruktor serwisu ustawień aplikacji.
        /// </summary>
        public ApplicationSettingService(
            IApplicationSettingRepository settingsRepository,
            IOperationHistoryService operationHistoryService,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            ILogger<ApplicationSettingService> logger,
            IPowerShellCacheService powerShellCacheService)
        {
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _powerShellCacheService = powerShellCacheService ?? throw new ArgumentNullException(nameof(powerShellCacheService));
        }



        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<ApplicationSetting?> GetSettingByKeyAsync(string key, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie ustawienia aplikacji o kluczu: {Key}. Wymuszenie odświeżenia: {ForceRefresh}", key, forceRefresh); //
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogWarning("Próba pobrania ustawienia z pustym kluczem."); //
                return null;
            }

            string cacheKey = SettingByKeyCacheKeyPrefix + key;

            if (!forceRefresh && _powerShellCacheService.TryGetValue(cacheKey, out ApplicationSetting? cachedSetting))
            {
                _logger.LogDebug("Ustawienie '{Key}' znalezione w cache.", key); //
                return cachedSetting;
            }

            _logger.LogDebug("Ustawienie '{Key}' nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", key); //
            var settingFromDb = await _settingsRepository.GetSettingByKeyAsync(key);

            if (settingFromDb != null)
            {
                _powerShellCacheService.Set(cacheKey, settingFromDb);
                _logger.LogDebug("Ustawienie '{Key}' dodane do cache.", key); //
            }
            else
            {
                _powerShellCacheService.Remove(cacheKey);
            }

            return settingFromDb;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Zmiany w danych mogą nie być widoczne natychmiast bez odświeżenia cache'u.</remarks>
        public async Task<T?> GetSettingValueAsync<T>(string key, T? defaultValue = default)
        {
            _logger.LogDebug("Pobieranie wartości ustawienia o kluczu: {Key} jako typ {TypeName}", key, typeof(T).Name); //
            var setting = await GetSettingByKeyAsync(key);

            if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
            {
                _logger.LogDebug("Ustawienie o kluczu '{Key}' nie znalezione lub pusta wartość. Zwracanie wartości domyślnej: {DefaultValue}", key, defaultValue); //
                return defaultValue;
            }

            try
            {
                var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

                return setting.Type switch //
                {
                    SettingType.String when targetType == typeof(string) => (T)(object)setting.GetStringValue(), //
                    SettingType.Integer when targetType == typeof(int) => (T)(object)setting.GetIntValue(), //
                    SettingType.Boolean when targetType == typeof(bool) => (T)(object)setting.GetBoolValue(), //
                    SettingType.DateTime when targetType == typeof(DateTime) => (T?)(object?)setting.GetDateTimeValue() ?? defaultValue, //
                    SettingType.Decimal when targetType == typeof(decimal) => (T)(object)setting.GetDecimalValue(), //
                    SettingType.Json when !string.IsNullOrWhiteSpace(setting.Value) => //
                        JsonSerializer.Deserialize<T>(setting.Value), //
                    _ => LogTypeMismatchAndReturnDefault(key, setting.Type, defaultValue) //
                };
            }
            catch (InvalidCastException ex)
            {
                _logger.LogError(ex, "Błąd rzutowania typu dla ustawienia '{Key}'. Oczekiwano {ExpectedType}, znaleziono {ActualType}. Wartość: '{Value}'", //
                                 key, typeof(T).Name, setting.Type, setting.Value);
                return defaultValue;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Błąd deserializacji JSON dla ustawienia '{Key}'. Wartość: '{Value}'", key, setting.Value); //
                return defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas pobierania i konwersji ustawienia '{Key}'. Wartość: '{Value}'", key, setting.Value); //
                return defaultValue;
            }
        }

        private T? LogTypeMismatchAndReturnDefault<T>(string key, SettingType actualType, T? defaultValue)
        {
            _logger.LogWarning("Niezgodność typu dla ustawienia '{Key}'. Oczekiwano {ExpectedType}, znaleziono {ActualType}.", key, typeof(T).Name, actualType); //
            return defaultValue;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<IEnumerable<ApplicationSetting>> GetAllSettingsAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych ustawień aplikacji. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh); //

            if (!forceRefresh && _powerShellCacheService.TryGetValue(AllSettingsCacheKey, out IEnumerable<ApplicationSetting>? cachedSettings) && cachedSettings != null)
            {
                _logger.LogDebug("Wszystkie aktywne ustawienia znalezione w cache."); //
                return cachedSettings;
            }

            _logger.LogDebug("Wszystkie aktywne ustawienia nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium."); //
            var settingsFromDb = await _settingsRepository.FindAsync(s => s.IsActive); //

            _powerShellCacheService.Set(AllSettingsCacheKey, settingsFromDb);
            _logger.LogDebug("Wszystkie aktywne ustawienia dodane do cache."); //

            return settingsFromDb;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<IEnumerable<ApplicationSetting>> GetSettingsByCategoryAsync(string category, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie aktywnych ustawień aplikacji dla kategorii: {Category}. Wymuszenie odświeżenia: {ForceRefresh}", category, forceRefresh); //
            if (string.IsNullOrWhiteSpace(category))
            {
                _logger.LogWarning("Próba pobrania ustawień dla pustej kategorii."); //
                return Enumerable.Empty<ApplicationSetting>();
            }

            string cacheKey = SettingsByCategoryCacheKeyPrefix + category;

            if (!forceRefresh && _powerShellCacheService.TryGetValue(cacheKey, out IEnumerable<ApplicationSetting>? cachedSettings) && cachedSettings != null)
            {
                _logger.LogDebug("Ustawienia dla kategorii '{Category}' znalezione w cache.", category); //
                return cachedSettings;
            }

            _logger.LogDebug("Ustawienia dla kategorii '{Category}' nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", category); //
            var settingsFromDb = await _settingsRepository.GetSettingsByCategoryAsync(category);

            _powerShellCacheService.Set(cacheKey, settingsFromDb);
            _logger.LogDebug("Ustawienia dla kategorii '{Category}' dodane do cache.", category); //

            return settingsFromDb;
        }

        /// <inheritdoc />
        public async Task<bool> SaveSettingAsync(string key, string value, SettingType type, string? description = null, string? category = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Zapisywanie ustawienia: Klucz={Key}, Wartość='{Value}', Typ={Type}", key, value, type);

            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogError("Nie można zapisać ustawienia: Klucz nie może być pusty.");
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Nie można zapisać ustawienia: Klucz nie może być pusty",
                    "error"
                );
                return false;
            }

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.ApplicationSettingCreated, // Domyślnie na Created, zmieni się w logice
                nameof(ApplicationSetting),
                targetEntityId: key,
                targetEntityName: key
            );

            try
            {
                var existingSetting = await _settingsRepository.GetSettingByKeyAsync(key);
                string? oldCategory = existingSetting?.Category;
                bool isUpdate = existingSetting != null;

                if (isUpdate)
                {
                    // Zmiana typu operacji na Update
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.InProgress,
                        $"Aktualizowanie istniejącego ustawienia: {key}"
                    );

                    _logger.LogInformation("Aktualizowanie istniejącego ustawienia: {Key}", key);

                    existingSetting.Value = value;
                    existingSetting.Type = type;
                    if (description != null) existingSetting.Description = description;
                    if (category != null) existingSetting.Category = category;
                    existingSetting.MarkAsModified(currentUserUpn);
                    _settingsRepository.Update(existingSetting);
                }
                else
                {
                    _logger.LogInformation("Tworzenie nowego ustawienia: {Key}", key);
                    var newSetting = new ApplicationSetting
                    {
                        Id = Guid.NewGuid().ToString(),
                        Key = key,
                        Value = value,
                        Type = type,
                        Description = description ?? string.Empty,
                        Category = category ?? "General",
                        IsRequired = false,
                        IsVisible = true,
                        CreatedBy = currentUserUpn,
                        IsActive = true
                    };
                    await _settingsRepository.AddAsync(newSetting);
                    oldCategory = null;
                }

                _logger.LogInformation("Ustawienie '{Key}' pomyślnie przygotowane do zapisu.", key);

                InvalidateSettingCache(key, category ?? existingSetting?.Category ?? "General", oldCategory);

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                var statusMessage = isUpdate ? $"Ustawienie '{key}' zaktualizowane." : $"Ustawienie '{key}' utworzone.";
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    statusMessage
                );

                // 3. Powiadomienie o sukcesie
                var userMessage = isUpdate ? $"Ustawienie '{key}' zostało zaktualizowane" : $"Ustawienie '{key}' zostało utworzone";
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    userMessage,
                    "success"
                );

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas zapisywania ustawienia {Key}. Wiadomość: {ErrorMessage}", key, ex.Message);

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
                    $"Błąd podczas zapisywania ustawienia '{key}': {ex.Message}",
                    "error"
                );

                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSettingAsync(ApplicationSetting settingToUpdate)
        {
            if (settingToUpdate == null || string.IsNullOrEmpty(settingToUpdate.Id) || string.IsNullOrEmpty(settingToUpdate.Key))
            {
                _logger.LogError("Próba aktualizacji ustawienia aplikacji z nieprawidłowymi danymi (null, brak ID lub Klucza).");
                throw new ArgumentNullException(nameof(settingToUpdate), "Obiekt ustawienia, jego ID lub Klucz nie może być null/pusty.");
            }

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Aktualizowanie obiektu ApplicationSetting ID: {SettingId}, Klucz: {Key}", settingToUpdate.Id, settingToUpdate.Key);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.ApplicationSettingUpdated,
                nameof(ApplicationSetting),
                targetEntityId: settingToUpdate.Id,
                targetEntityName: settingToUpdate.Key
            );

            try
            {
                string? oldKey = null;
                string? oldCategory = null;

                var existingSetting = await _settingsRepository.GetByIdAsync(settingToUpdate.Id);
                if (existingSetting == null)
                {
                    _logger.LogWarning("Nie można zaktualizować ustawienia ID {SettingId} - nie istnieje.", settingToUpdate.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Ustawienie o ID '{settingToUpdate.Id}' nie istnieje."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Nie można zaktualizować ustawienia: nie istnieje w systemie",
                        "error"
                    );
                    return false;
                }

                oldKey = existingSetting.Key;
                oldCategory = existingSetting.Category;

                if (existingSetting.Key != settingToUpdate.Key)
                {
                    var conflicting = await _settingsRepository.GetSettingByKeyAsync(settingToUpdate.Key);
                    if (conflicting != null && conflicting.Id != existingSetting.Id)
                    {
                        _logger.LogError("Nie można zaktualizować ustawienia: Klucz '{Key}' już istnieje.", settingToUpdate.Key);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            $"Ustawienie o kluczu '{settingToUpdate.Key}' już istnieje."
                        );

                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            $"Nie można zaktualizować ustawienia: klucz '{settingToUpdate.Key}' już istnieje",
                            "error"
                        );
                        return false;
                    }
                }

                existingSetting.Key = settingToUpdate.Key;
                existingSetting.Value = settingToUpdate.Value;
                existingSetting.Description = settingToUpdate.Description;
                existingSetting.Type = settingToUpdate.Type;
                existingSetting.Category = settingToUpdate.Category;
                existingSetting.IsRequired = settingToUpdate.IsRequired;
                existingSetting.IsVisible = settingToUpdate.IsVisible;
                existingSetting.DefaultValue = settingToUpdate.DefaultValue;
                existingSetting.ValidationPattern = settingToUpdate.ValidationPattern;
                existingSetting.ValidationMessage = settingToUpdate.ValidationMessage;
                existingSetting.DisplayOrder = settingToUpdate.DisplayOrder;
                existingSetting.IsActive = settingToUpdate.IsActive;
                existingSetting.MarkAsModified(currentUserUpn);

                _settingsRepository.Update(existingSetting);

                InvalidateSettingCache(existingSetting.Key, existingSetting.Category, oldCategory);
                if (oldKey != null && oldKey != existingSetting.Key)
                {
                    _powerShellCacheService.InvalidateSettingByKey(oldKey);
                }

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    "Ustawienie aplikacji przygotowane do aktualizacji."
                );

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Ustawienie '{existingSetting.Key}' zostało zaktualizowane",
                    "success"
                );
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji ApplicationSetting ID {SettingId}. Wiadomość: {ErrorMessage}", settingToUpdate.Id, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Błąd: {ex.Message}",
                    ex.StackTrace
                );

                // 4. Powiadomienie o błędzie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Błąd podczas aktualizacji ustawienia: {ex.Message}",
                    "error"
                );
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteSettingAsync(string key)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Usuwanie ustawienia o kluczu: {Key}", key);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.ApplicationSettingDeleted,
                nameof(ApplicationSetting),
                targetEntityId: key,
                targetEntityName: key
            );

            try
            {
                var setting = await _settingsRepository.GetSettingByKeyAsync(key);
                if (setting == null)
                {
                    _logger.LogWarning("Nie można usunąć ustawienia: Klucz {Key} nie znaleziony.", key);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Ustawienie o kluczu '{key}' nie zostało znalezione."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Nie można usunąć ustawienia: klucz '{key}' nie został znaleziony",
                        "error"
                    );
                    return false;
                }

                if (!setting.IsActive)
                {
                    _logger.LogInformation("Ustawienie o kluczu {Key} było już nieaktywne.", key);
                    InvalidateSettingCache(key, setting.Category);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Ustawienie '{key}' było już nieaktywne. Brak akcji."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Ustawienie '{key}' było już nieaktywne",
                        "info"
                    );
                    return true;
                }

                setting.MarkAsDeleted(currentUserUpn);
                _settingsRepository.Update(setting);

                InvalidateSettingCache(key, setting.Category);

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    "Ustawienie aplikacji oznaczone jako usunięte."
                );

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Ustawienie '{key}' zostało usunięte",
                    "success"
                );
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania ustawienia aplikacji o kluczu {Key}. Wiadomość: {ErrorMessage}", key, ex.Message);
                
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
                    $"Błąd podczas usuwania ustawienia '{key}': {ex.Message}",
                    "error"
                );
                return false;
            }
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda unieważnia globalny cache dla ustawień aplikacji.</remarks>
        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a ustawień aplikacji."); //
            InvalidateSettingCache(invalidateAll: true); //
            _logger.LogInformation("Cache ustawień aplikacji został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu."); //
            return Task.CompletedTask;
        }

        /// <summary>
        /// Unieważnia cache dla ustawień aplikacji w sposób granularny.
        /// Deleguje inwalidację do PowerShellCacheService.
        /// </summary>
        /// <param name="key">Klucz konkretnego ustawienia do usunięcia z cache (opcjonalnie).</param>
        /// <param name="category">Kategoria, dla której ustawienia mają być usunięte z cache (opcjonalnie).</param>
        /// <param name="oldCategory">Poprzednia kategoria ustawienia, jeśli była zmieniana (opcjonalnie).</param>
        /// <param name="invalidateAll">Czy unieważnić wszystkie klucze związane z ustawieniami (opcjonalnie, domyślnie false).</param>
        private void InvalidateSettingCache(string? key = null, string? category = null, string? oldCategory = null, bool invalidateAll = false)
        {
            _logger.LogDebug("Inwalidacja cache'u ustawień. Klucz: {Key}, Kategoria: {Category}, Stara kategoria: {OldCategory}, Inwaliduj wszystko: {InvalidateAll}",
                             key, category, oldCategory, invalidateAll);

            if (invalidateAll)
            {
                // TYLKO dla RefreshCacheAsync() - globalne resetowanie
                _powerShellCacheService.InvalidateAllCache();
                _logger.LogDebug("Wykonano globalne resetowanie cache przez PowerShellCacheService.");
                return;
            }

            // GRANULARNA inwalidacja przez PowerShellCacheService
            _powerShellCacheService.InvalidateAllActiveSettingsList();
            _logger.LogDebug("Unieważniono listę wszystkich aktywnych ustawień.");

            if (!string.IsNullOrWhiteSpace(key))
            {
                _powerShellCacheService.InvalidateSettingByKey(key);
                _logger.LogDebug("Unieważniono cache ustawienia o kluczu: {Key}", key);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                _powerShellCacheService.InvalidateSettingsByCategory(category);
                _logger.LogDebug("Unieważniono cache ustawień dla kategorii: {Category}", category);
            }

            // Jeśli kategoria została zmieniona, usuń cache także dla starej kategorii
            if (!string.IsNullOrWhiteSpace(oldCategory) && oldCategory != category)
            {
                _powerShellCacheService.InvalidateSettingsByCategory(oldCategory);
                _logger.LogDebug("Unieważniono cache ustawień dla starej kategorii: {OldCategory}", oldCategory);
            }
        }
    }
}