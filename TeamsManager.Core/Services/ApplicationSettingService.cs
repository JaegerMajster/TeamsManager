using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    /// Serwis odpowiedzialny za zarządzanie ustawieniami aplikacji.
    /// Implementuje mechanizm cache'owania dla poprawy wydajności.
    /// </summary>
    public class ApplicationSettingService : IApplicationSettingService
    {
        private readonly IApplicationSettingRepository _settingsRepository;
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ApplicationSettingService> _logger;
        private readonly IMemoryCache _cache;

        // Definicje kluczy cache
        private const string AllSettingsCacheKey = "ApplicationSettings_AllActive"; // Zmieniono nazwę dla spójności
        private const string SettingsByCategoryCacheKeyPrefix = "ApplicationSettings_Category_";
        private const string SettingByKeyCacheKeyPrefix = "ApplicationSetting_Key_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(15);

        // Token do zarządzania unieważnianiem wpisów cache dla ustawień
        private static CancellationTokenSource _settingsCacheTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Konstruktor serwisu ustawień aplikacji.
        /// </summary>
        public ApplicationSettingService(
            IApplicationSettingRepository settingsRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<ApplicationSettingService> logger,
            IMemoryCache memoryCache)
        {
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        // Generuje domyślne opcje dla wpisu w pamięci podręcznej.
        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_settingsCacheTokenSource.Token));
        }

        /// <inheritdoc />
        public async Task<ApplicationSetting?> GetSettingByKeyAsync(string key, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie ustawienia aplikacji o kluczu: {Key}. Wymuszenie odświeżenia: {ForceRefresh}", key, forceRefresh);
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogWarning("Próba pobrania ustawienia z pustym kluczem.");
                return null;
            }

            string cacheKey = SettingByKeyCacheKeyPrefix + key;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out ApplicationSetting? cachedSetting))
            {
                _logger.LogDebug("Ustawienie '{Key}' znalezione w cache.", key);
                return cachedSetting;
            }

            _logger.LogDebug("Ustawienie '{Key}' nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", key);
            var settingFromDb = await _settingsRepository.GetSettingByKeyAsync(key); // Zakładamy, że repozytorium zwraca aktywne

            if (settingFromDb != null)
            {
                _cache.Set(cacheKey, settingFromDb, GetDefaultCacheEntryOptions());
                _logger.LogDebug("Ustawienie '{Key}' dodane do cache.", key);
            }
            else
            {
                _cache.Remove(cacheKey); // Jeśli nie znaleziono, usuń stary wpis z cache
            }

            return settingFromDb;
        }

        /// <inheritdoc />
        public async Task<T?> GetSettingValueAsync<T>(string key, T? defaultValue = default)
        {
            _logger.LogDebug("Pobieranie wartości ustawienia o kluczu: {Key} jako typ {TypeName}", key, typeof(T).Name);
            var setting = await GetSettingByKeyAsync(key);

            if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
            {
                _logger.LogDebug("Ustawienie o kluczu '{Key}' nie znalezione lub pusta wartość. Zwracanie wartości domyślnej: {DefaultValue}", key, defaultValue);
                return defaultValue;
            }

            try
            {
                // Pobierz typ bazowy dla nullable types
                var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

                // Konwersja typów jest wykonywana przez metody w modelu ApplicationSetting
                return setting.Type switch
                {
                    SettingType.String when targetType == typeof(string) => (T)(object)setting.GetStringValue(),
                    SettingType.Integer when targetType == typeof(int) => (T)(object)setting.GetIntValue(),
                    SettingType.Boolean when targetType == typeof(bool) => (T)(object)setting.GetBoolValue(),
                    SettingType.DateTime when targetType == typeof(DateTime) => (T?)(object?)setting.GetDateTimeValue() ?? defaultValue,
                    SettingType.Decimal when targetType == typeof(decimal) => (T)(object)setting.GetDecimalValue(),
                    SettingType.Json when !string.IsNullOrWhiteSpace(setting.Value) =>
                        JsonSerializer.Deserialize<T>(setting.Value),
                    _ => LogTypeMismatchAndReturnDefault(key, setting.Type, defaultValue)
                };
            }
            catch (InvalidCastException ex)
            {
                _logger.LogError(ex, "Błąd rzutowania typu dla ustawienia '{Key}'. Oczekiwano {ExpectedType}, znaleziono {ActualType}. Wartość: '{Value}'",
                                 key, typeof(T).Name, setting.Type, setting.Value);
                return defaultValue;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Błąd deserializacji JSON dla ustawienia '{Key}'. Wartość: '{Value}'", key, setting.Value);
                return defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas pobierania i konwersji ustawienia '{Key}'. Wartość: '{Value}'", key, setting.Value);
                return defaultValue;
            }
        }

        // Metoda pomocnicza do logowania i zwracania wartości domyślnej przy niezgodności typów
        private T? LogTypeMismatchAndReturnDefault<T>(string key, SettingType actualType, T? defaultValue)
        {
            _logger.LogWarning("Niezgodność typu dla ustawienia '{Key}'. Oczekiwano {ExpectedType}, znaleziono {ActualType}.", key, typeof(T).Name, actualType);
            return defaultValue;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ApplicationSetting>> GetAllSettingsAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych ustawień aplikacji. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);

            if (!forceRefresh && _cache.TryGetValue(AllSettingsCacheKey, out IEnumerable<ApplicationSetting>? cachedSettings) && cachedSettings != null)
            {
                _logger.LogDebug("Wszystkie aktywne ustawienia znalezione w cache.");
                return cachedSettings;
            }

            _logger.LogDebug("Wszystkie aktywne ustawienia nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var settingsFromDb = await _settingsRepository.FindAsync(s => s.IsActive);

            _cache.Set(AllSettingsCacheKey, settingsFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Wszystkie aktywne ustawienia dodane do cache.");

            return settingsFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ApplicationSetting>> GetSettingsByCategoryAsync(string category, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie aktywnych ustawień aplikacji dla kategorii: {Category}. Wymuszenie odświeżenia: {ForceRefresh}", category, forceRefresh);
            if (string.IsNullOrWhiteSpace(category))
            {
                _logger.LogWarning("Próba pobrania ustawień dla pustej kategorii.");
                return Enumerable.Empty<ApplicationSetting>();
            }

            string cacheKey = SettingsByCategoryCacheKeyPrefix + category;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<ApplicationSetting>? cachedSettings) && cachedSettings != null)
            {
                _logger.LogDebug("Ustawienia dla kategorii '{Category}' znalezione w cache.", category);
                return cachedSettings;
            }

            _logger.LogDebug("Ustawienia dla kategorii '{Category}' nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", category);
            var settingsFromDb = await _settingsRepository.GetSettingsByCategoryAsync(category); // Repozytorium powinno filtrować po IsActive

            _cache.Set(cacheKey, settingsFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Ustawienia dla kategorii '{Category}' dodane do cache.", category);

            return settingsFromDb;
        }

        /// <inheritdoc />
        public async Task<bool> SaveSettingAsync(string key, string value, SettingType type, string? description = null, string? category = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_save_setting";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                TargetEntityType = nameof(ApplicationSetting),
                TargetEntityId = key, // Klucz jako tymczasowe ID, jeśli ustawienie jest nowe
                TargetEntityName = key,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            // Typ operacji zostanie ustawiony poniżej
            _logger.LogInformation("Zapisywanie ustawienia: Klucz={Key}, Wartość='{Value}', Typ={Type} przez {User}", key, value, type, currentUserUpn);

            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogError("Nie można zapisać ustawienia: Klucz nie może być pusty.");
                // Ustawiamy typ przed oznaczeniem jako nieudane
                operation.Type = OperationType.ApplicationSettingUpdated; // Lub Created, zależnie od intencji
                operation.MarkAsFailed("Klucz ustawienia nie może być pusty.");
                await SaveOperationHistoryAsync(operation);
                return false;
            }
            operation.MarkAsStarted();

            var existingSetting = await _settingsRepository.GetSettingByKeyAsync(key);
            bool success = false;
            string? oldCategory = existingSetting?.Category;
            string? actualSettingId = existingSetting?.Id;

            try
            {
                if (existingSetting != null)
                {
                    operation.Type = OperationType.ApplicationSettingUpdated;
                    operation.TargetEntityId = existingSetting.Id; // Użyj rzeczywistego ID
                    _logger.LogInformation("Aktualizowanie istniejącego ustawienia: {Key}", key);

                    existingSetting.Value = value;
                    existingSetting.Type = type;
                    if (description != null) existingSetting.Description = description;
                    if (category != null) existingSetting.Category = category; // Aktualizacja kategorii
                    existingSetting.MarkAsModified(currentUserUpn);
                    _settingsRepository.Update(existingSetting);
                    operation.MarkAsCompleted($"Ustawienie '{key}' zaktualizowane.");
                    actualSettingId = existingSetting.Id;
                }
                else
                {
                    operation.Type = OperationType.ApplicationSettingCreated;
                    _logger.LogInformation("Tworzenie nowego ustawienia: {Key}", key);
                    var newSetting = new ApplicationSetting
                    {
                        Id = Guid.NewGuid().ToString(),
                        Key = key,
                        Value = value,
                        Type = type,
                        Description = description ?? string.Empty,
                        Category = category ?? "General",
                        IsRequired = false, // Domyślne wartości
                        IsVisible = true,   // Domyślne wartości
                        CreatedBy = currentUserUpn, // Ustawiane też przez DbContext.SetAuditFields
                        IsActive = true
                    };
                    await _settingsRepository.AddAsync(newSetting);
                    operation.TargetEntityId = newSetting.Id; // Użyj rzeczywistego ID
                    operation.MarkAsCompleted($"Ustawienie '{key}' utworzone.");
                    actualSettingId = newSetting.Id;
                    // Dla nowego ustawienia nie ma 'starej' kategorii, więc oldCategory pozostaje null (lub kategorią nowego, jeśli chcemy inwalidować też nową)
                    oldCategory = null; // Lub newSetting.Category, jeśli tak ma działać inwalidacja
                }
                success = true;
                _logger.LogInformation("Ustawienie '{Key}' pomyślnie przygotowane do zapisu.", key);

                // Inwalidacja cache po udanej operacji
                InvalidateSettingCache(key, category ?? existingSetting?.Category, oldCategory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas zapisywania ustawienia {Key}. Wiadomość: {ErrorMessage}", key, ex.Message);
                if (operation.Type == default(OperationType)) // Upewnij się, że typ jest ustawiony przed MarkAsFailed
                {
                    operation.Type = existingSetting != null ? OperationType.ApplicationSettingUpdated : OperationType.ApplicationSettingCreated;
                }
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                success = false;
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(actualSettingId) && operation.TargetEntityId != actualSettingId)
                {
                    operation.TargetEntityId = actualSettingId; // Upewnij się, że ID jest poprawne dla historii
                }
                await SaveOperationHistoryAsync(operation);
            }
            return success;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSettingAsync(ApplicationSetting settingToUpdate)
        {
            if (settingToUpdate == null || string.IsNullOrEmpty(settingToUpdate.Id) || string.IsNullOrEmpty(settingToUpdate.Key))
            {
                _logger.LogError("Próba aktualizacji ustawienia aplikacji z nieprawidłowymi danymi (null, brak ID lub Klucza).");
                throw new ArgumentNullException(nameof(settingToUpdate), "Obiekt ustawienia, jego ID lub Klucz nie może być null/pusty.");
            }

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.ApplicationSettingUpdated,
                TargetEntityType = nameof(ApplicationSetting),
                TargetEntityId = settingToUpdate.Id,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Aktualizowanie obiektu ApplicationSetting ID: {SettingId}, Klucz: {Key}", settingToUpdate.Id, settingToUpdate.Key);

            string? oldKey = null;
            string? oldCategory = null;

            try
            {
                var existingSetting = await _settingsRepository.GetByIdAsync(settingToUpdate.Id);
                if (existingSetting == null)
                {
                    operation.MarkAsFailed($"Ustawienie o ID '{settingToUpdate.Id}' nie istnieje.");
                    _logger.LogWarning("Nie można zaktualizować ustawienia ID {SettingId} - nie istnieje.", settingToUpdate.Id);
                    return false;
                }
                operation.TargetEntityName = existingSetting.Key; // Nazwa przed modyfikacją
                oldKey = existingSetting.Key;
                oldCategory = existingSetting.Category;

                // Sprawdzenie unikalności nowego klucza, jeśli został zmieniony
                if (existingSetting.Key != settingToUpdate.Key)
                {
                    var conflicting = await _settingsRepository.GetSettingByKeyAsync(settingToUpdate.Key);
                    if (conflicting != null && conflicting.Id != existingSetting.Id) // Upewnij się, że konflikt nie jest z samym sobą
                    {
                        operation.MarkAsFailed($"Ustawienie o kluczu '{settingToUpdate.Key}' już istnieje.");
                        _logger.LogError("Nie można zaktualizować ustawienia: Klucz '{Key}' już istnieje.", settingToUpdate.Key);
                        return false;
                    }
                }

                // Mapowanie właściwości
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
                existingSetting.IsActive = settingToUpdate.IsActive; // Pozwalamy na zmianę IsActive
                existingSetting.MarkAsModified(currentUserUpn);

                _settingsRepository.Update(existingSetting);
                operation.TargetEntityName = existingSetting.Key; // Nazwa po modyfikacji
                operation.MarkAsCompleted("Ustawienie aplikacji przygotowane do aktualizacji.");

                InvalidateSettingCache(existingSetting.Key, existingSetting.Category, oldCategory);
                if (oldKey != null && oldKey != existingSetting.Key) // Jeśli klucz się zmienił, trzeba też usunąć stary klucz z cache
                {
                    _cache.Remove(SettingByKeyCacheKeyPrefix + oldKey);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji ApplicationSetting ID {SettingId}. Wiadomość: {ErrorMessage}", settingToUpdate.Id, ex.Message);
                operation.MarkAsFailed($"Błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteSettingAsync(string key)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete_setting";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.ApplicationSettingDeleted,
                TargetEntityType = nameof(ApplicationSetting),
                TargetEntityId = key, // Używamy klucza jako tymczasowego ID, jeśli obiekt nie zostanie znaleziony
                TargetEntityName = key,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Usuwanie ustawienia o kluczu: {Key}", key);
            ApplicationSetting? setting = null;

            try
            {
                setting = await _settingsRepository.GetSettingByKeyAsync(key);
                if (setting == null)
                {
                    operation.MarkAsFailed($"Ustawienie o kluczu '{key}' nie zostało znalezione.");
                    _logger.LogWarning("Nie można usunąć ustawienia: Klucz {Key} nie znaleziony.", key);
                    return false;
                }

                operation.TargetEntityId = setting.Id; // Używamy rzeczywistego ID encji dla historii

                if (!setting.IsActive)
                {
                    operation.MarkAsCompleted($"Ustawienie '{key}' było już nieaktywne. Brak akcji.");
                    _logger.LogInformation("Ustawienie o kluczu {Key} było już nieaktywne.", key);
                    InvalidateSettingCache(key, setting.Category); // Mimo wszystko odświeżamy cache
                    return true;
                }

                setting.MarkAsDeleted(currentUserUpn); // Ustawia IsActive = false
                _settingsRepository.Update(setting);
                operation.MarkAsCompleted("Ustawienie aplikacji oznaczone jako usunięte.");

                InvalidateSettingCache(key, setting.Category);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania ustawienia aplikacji o kluczu {Key}. Wiadomość: {ErrorMessage}", key, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a ustawień aplikacji.");
            InvalidateSettingCache(invalidateAll: true);
            _logger.LogInformation("Cache ustawień aplikacji został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        // Prywatna metoda do unieważniania cache.
        private void InvalidateSettingCache(string? key = null, string? category = null, string? oldCategory = null, bool invalidateAll = false)
        {
            _logger.LogDebug("Inwalidacja cache'u ustawień. Klucz: {Key}, Kategoria: {Category}, Stara kategoria: {OldCategory}, Inwaliduj wszystko: {InvalidateAll}",
                             key, category, oldCategory, invalidateAll);

            // Główny mechanizm inwalidacji poprzez CancellationToken
            var oldTokenSource = Interlocked.Exchange(ref _settingsCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
            _logger.LogDebug("Token cache'u dla ustawień aplikacji został zresetowany.");

            // Dodatkowe, bardziej granularne usuwanie kluczy dla natychmiastowego efektu, jeśli jest to pożądane.
            // Reset tokenu powinien docelowo unieważnić wszystkie zależne wpisy.
            if (invalidateAll)
            {
                _cache.Remove(AllSettingsCacheKey); // Jawne usunięcie klucza listy wszystkich
                _logger.LogDebug("Usunięto z cache klucz dla wszystkich ustawień aplikacji.");
                // Można by iterować po znanych kategoriach i usuwać ich klucze, ale token jest bardziej globalny.
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                _cache.Remove(SettingByKeyCacheKeyPrefix + key);
                _logger.LogDebug("Usunięto z cache ustawienie o kluczu: {Key}", key);
            }
            if (!string.IsNullOrWhiteSpace(category))
            {
                _cache.Remove(SettingsByCategoryCacheKeyPrefix + category);
                _logger.LogDebug("Usunięto z cache ustawienia dla kategorii: {Category}", category);
            }
            if (!string.IsNullOrWhiteSpace(oldCategory) && oldCategory != category)
            {
                _cache.Remove(SettingsByCategoryCacheKeyPrefix + oldCategory);
                _logger.LogDebug("Usunięto z cache ustawienia dla starej kategorii: {OldCategory}", oldCategory);
            }
            if (!invalidateAll) // Jeśli nie było globalnej inwalidacji, a coś się zmieniło, warto też AllSettings usunąć
            {
                _cache.Remove(AllSettingsCacheKey);
                _logger.LogDebug("Usunięto z cache klucz dla wszystkich ustawień aplikacji (prewencyjnie).");
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
                // Ustawienie StartedAt, jeśli operacja jest rozpoczynana i nie ma jeszcze tej daty
                if (operation.StartedAt == default(DateTime) && (operation.Status == OperationStatus.InProgress || operation.Status == OperationStatus.Pending))
                {
                    operation.StartedAt = DateTime.UtcNow;
                }
                await _operationHistoryRepository.AddAsync(operation);
            }
            else
            {
                // Aktualizacja istniejącego logu
                existingLog.Status = operation.Status;
                existingLog.CompletedAt = operation.CompletedAt;
                existingLog.Duration = operation.Duration;
                existingLog.ErrorMessage = operation.ErrorMessage;
                existingLog.ErrorStackTrace = operation.ErrorStackTrace;
                existingLog.OperationDetails = operation.OperationDetails;
                existingLog.TargetEntityName = operation.TargetEntityName; // Aktualizuj na wypadek zmiany nazwy
                existingLog.TargetEntityId = operation.TargetEntityId;     // Aktualizuj na wypadek zmiany (chociaż rzadkie)
                existingLog.Type = operation.Type;                         // Aktualizuj na wypadek zmiany
                existingLog.ProcessedItems = operation.ProcessedItems;
                existingLog.FailedItems = operation.FailedItems;
                // Pozostałe pola, które mogą być aktualizowane
                existingLog.ParentOperationId = operation.ParentOperationId;
                existingLog.SequenceNumber = operation.SequenceNumber;
                existingLog.TotalItems = operation.TotalItems;
                existingLog.UserIpAddress = operation.UserIpAddress;
                existingLog.UserAgent = operation.UserAgent;
                existingLog.SessionId = operation.SessionId;
                existingLog.Tags = operation.Tags;

                existingLog.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_log_update");
                _operationHistoryRepository.Update(existingLog);
            }
        }
    }
}