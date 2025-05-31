using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading; // Dodano dla CancellationTokenSource
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using Microsoft.Extensions.Primitives;

namespace TeamsManager.Core.Services
{
    public class ApplicationSettingService : IApplicationSettingService
    {
        private readonly IApplicationSettingRepository _settingsRepository;
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ApplicationSettingService> _logger;
        private readonly IMemoryCache _cache;

        // Definicje kluczy cache
        private const string AllSettingsCacheKey = "ApplicationSettings_All";
        private const string SettingsByCategoryCacheKeyPrefix = "ApplicationSettings_Category_";
        private const string SettingByKeyCacheKeyPrefix = "ApplicationSetting_Key_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(15);

        // Token do zarządzania unieważnianiem wpisów cache dla ustawień
        private static CancellationTokenSource _settingsCacheTokenSource = new CancellationTokenSource();

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

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_settingsCacheTokenSource.Token));
        }

        public async Task<ApplicationSetting?> GetSettingByKeyAsync(string key)
        {
            _logger.LogInformation("Pobieranie ustawienia aplikacji o kluczu: {Key}", key);
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogWarning("Próba pobrania ustawienia z pustym kluczem.");
                return null;
            }

            string cacheKey = SettingByKeyCacheKeyPrefix + key;

            if (_cache.TryGetValue(cacheKey, out ApplicationSetting? cachedSetting))
            {
                _logger.LogDebug("Ustawienie '{Key}' znalezione w cache.", key);
                return cachedSetting;
            }

            _logger.LogDebug("Ustawienie '{Key}' nie znalezione w cache. Pobieranie z repozytorium.", key);
            var settingFromDb = await _settingsRepository.GetSettingByKeyAsync(key);

            if (settingFromDb != null)
            {
                _cache.Set(cacheKey, settingFromDb, GetDefaultCacheEntryOptions());
                _logger.LogDebug("Ustawienie '{Key}' dodane do cache.", key);
            }

            return settingFromDb;
        }

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
                switch (setting.Type)
                {
                    case SettingType.String:
                        if (typeof(T) == typeof(string))
                            return (T)(object)setting.GetStringValue();
                        _logger.LogWarning("Niezgodność typu dla ustawienia '{Key}'. Oczekiwano {ExpectedType}, znaleziono {ActualType} (String).", key, typeof(T).Name, setting.Type);
                        return defaultValue;

                    case SettingType.Integer:
                        if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
                            return (T)(object)setting.GetIntValue();
                        _logger.LogWarning("Niezgodność typu dla ustawienia '{Key}'. Oczekiwano {ExpectedType}, znaleziono {ActualType} (Integer).", key, typeof(T).Name, setting.Type);
                        return defaultValue;

                    case SettingType.Boolean:
                        if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
                            return (T)(object)setting.GetBoolValue();
                        _logger.LogWarning("Niezgodność typu dla ustawienia '{Key}'. Oczekiwano {ExpectedType}, znaleziono {ActualType} (Boolean).", key, typeof(T).Name, setting.Type);
                        return defaultValue;

                    case SettingType.DateTime:
                        DateTime? parsedDateTime = setting.GetDateTimeValue();
                        if (parsedDateTime.HasValue)
                        {
                            if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
                            {
                                return (T)(object)parsedDateTime.Value;
                            }
                        }
                        else
                        {
                            if (typeof(T) == typeof(DateTime?) || defaultValue != null)
                            {
                                return defaultValue;
                            }
                            _logger.LogWarning("Nie można przekonwertować wartości null z GetDateTimeValue() na nienullowalny typ {TypeName} dla klucza '{Key}'. Zwracanie DateTime.MinValue.", typeof(T).Name, key);
                            return default(T);
                        }
                        _logger.LogWarning("Niezgodność typu dla ustawienia '{Key}'. Oczekiwano {ExpectedType}, znaleziono {ActualType} (DateTime).", key, typeof(T).Name, setting.Type);
                        return defaultValue;

                    case SettingType.Decimal:
                        if (typeof(T) == typeof(decimal) || typeof(T) == typeof(decimal?))
                            return (T)(object)setting.GetDecimalValue();
                        _logger.LogWarning("Niezgodność typu dla ustawienia '{Key}'. Oczekiwano {ExpectedType}, znaleziono {ActualType} (Decimal).", key, typeof(T).Name, setting.Type);
                        return defaultValue;

                    case SettingType.Json:
                        if (typeof(T).IsClass)
                        {
                            try
                            {
                                return JsonSerializer.Deserialize<T>(setting.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            }
                            catch (JsonException jsonEx)
                            {
                                _logger.LogWarning(jsonEx, "Błąd deserializacji JSON dla ustawienia '{Key}' do typu {ExpectedType}. Wartość JSON: '{JsonValue}'", key, typeof(T).Name, setting.Value);
                                return defaultValue;
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Nie można zdeserializować JSON do typu wartościowego '{ExpectedType}' dla ustawienia '{Key}'.", typeof(T).Name, key);
                            return defaultValue;
                        }
                    default:
                        _logger.LogWarning("Nieznany SettingType '{ActualType}' dla ustawienia '{Key}'.", setting.Type, key);
                        return defaultValue;
                }
            }
            catch (InvalidCastException ex)
            {
                _logger.LogError(ex, "Błąd rzutowania typu dla ustawienia '{Key}'. Oczekiwano {ExpectedType}, znaleziono {ActualType}. Wartość: '{Value}'",
                                 key, typeof(T).Name, setting.Type, setting.Value);
                return defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas pobierania i konwersji ustawienia '{Key}'. Wartość: '{Value}'", key, setting.Value);
                return defaultValue;
            }
        }

        public async Task<IEnumerable<ApplicationSetting>> GetAllSettingsAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych ustawień aplikacji. Wymuś odświeżenie: {ForceRefresh}", forceRefresh);

            if (forceRefresh)
            {
                _logger.LogDebug("Wymuszono odświeżenie dla wszystkich ustawień. Usuwanie z cache.");
                _cache.Remove(AllSettingsCacheKey); // Usuwamy stary wpis, jeśli istniał
            }
            else if (_cache.TryGetValue(AllSettingsCacheKey, out IEnumerable<ApplicationSetting>? cachedSettings) && cachedSettings != null)
            {
                _logger.LogDebug("Wszystkie ustawienia znalezione w cache.");
                return cachedSettings;
            }

            _logger.LogDebug("Wszystkie ustawienia nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var settingsFromDb = await _settingsRepository.FindAsync(s => s.IsActive);

            _cache.Set(AllSettingsCacheKey, settingsFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Wszystkie ustawienia dodane do cache.");

            return settingsFromDb;
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsByCategoryAsync(string category, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie aktywnych ustawień aplikacji dla kategorii: {Category}. Wymuś odświeżenie: {ForceRefresh}", category, forceRefresh);
            if (string.IsNullOrWhiteSpace(category))
            {
                _logger.LogWarning("Próba pobrania ustawień dla pustej kategorii.");
                return Enumerable.Empty<ApplicationSetting>();
            }

            string cacheKey = SettingsByCategoryCacheKeyPrefix + category;

            if (forceRefresh)
            {
                _logger.LogDebug("Wymuszono odświeżenie dla kategorii '{Category}'. Usuwanie z cache.", category);
                _cache.Remove(cacheKey);
            }
            else if (_cache.TryGetValue(cacheKey, out IEnumerable<ApplicationSetting>? cachedSettings) && cachedSettings != null)
            {
                _logger.LogDebug("Ustawienia dla kategorii '{Category}' znalezione w cache.", category);
                return cachedSettings;
            }

            _logger.LogDebug("Ustawienia dla kategorii '{Category}' nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", category);
            var settingsFromDb = await _settingsRepository.GetSettingsByCategoryAsync(category);

            _cache.Set(cacheKey, settingsFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Ustawienia dla kategorii '{Category}' dodane do cache.", category);

            return settingsFromDb;
        }

        public async Task<bool> SaveSettingAsync(string key, string value, SettingType type, string? description = null, string? category = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_save_setting";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                TargetEntityType = nameof(ApplicationSetting),
                TargetEntityId = key,
                TargetEntityName = key,
                CreatedBy = currentUserUpn,
                IsActive = true
            };

            _logger.LogInformation("Zapisywanie ustawienia: Klucz={Key}, Wartość='{Value}', Typ={Type} przez {User}", key, value, type, currentUserUpn);

            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogError("Nie można zapisać ustawienia: Klucz nie może być pusty.");
                operation.Type = OperationType.ApplicationSettingUpdated;
                operation.MarkAsFailed("Klucz ustawienia nie może być pusty.");
                await SaveOperationHistoryAsync(operation);
                return false;
            }
            operation.MarkAsStarted();

            var existingSetting = await _settingsRepository.GetSettingByKeyAsync(key);
            bool success = false;
            string? oldCategory = existingSetting?.Category;

            try
            {
                if (existingSetting != null)
                {
                    operation.Type = OperationType.ApplicationSettingUpdated;
                    operation.TargetEntityId = existingSetting.Id;
                    _logger.LogInformation("Aktualizowanie istniejącego ustawienia: {Key}", key);

                    existingSetting.Value = value;
                    existingSetting.Type = type;
                    if (description != null) existingSetting.Description = description;
                    if (category != null) existingSetting.Category = category;
                    existingSetting.MarkAsModified(currentUserUpn);
                    _settingsRepository.Update(existingSetting);
                    operation.MarkAsCompleted($"Ustawienie '{key}' zaktualizowane.");
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
                        IsRequired = false,
                        IsVisible = true,
                        CreatedBy = currentUserUpn,
                        IsActive = true
                    };
                    await _settingsRepository.AddAsync(newSetting);
                    operation.TargetEntityId = newSetting.Id;
                    operation.MarkAsCompleted($"Ustawienie '{key}' utworzone.");
                    existingSetting = newSetting;
                }
                success = true;
                _logger.LogInformation("Ustawienie '{Key}' pomyślnie przygotowane do zapisu.", key);

                InvalidateSettingCache(existingSetting.Key, existingSetting.Category, oldCategory);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas zapisywania ustawienia {Key}. Wiadomość: {ErrorMessage}", key, ex.Message);
                if (operation.Type == default(OperationType))
                {
                    operation.Type = existingSetting != null ? OperationType.ApplicationSettingUpdated : OperationType.ApplicationSettingCreated;
                }
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                success = false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
            return success;
        }

        public async Task<bool> UpdateSettingAsync(ApplicationSetting settingToUpdate)
        {
            if (settingToUpdate == null || string.IsNullOrEmpty(settingToUpdate.Id) || string.IsNullOrEmpty(settingToUpdate.Key))
                throw new ArgumentNullException(nameof(settingToUpdate), "Obiekt ustawienia, jego ID lub Klucz nie może być null/pusty.");

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
                operation.TargetEntityName = existingSetting.Key;
                oldKey = existingSetting.Key;
                oldCategory = existingSetting.Category;

                if (existingSetting.Key != settingToUpdate.Key)
                {
                    var conflicting = await _settingsRepository.GetSettingByKeyAsync(settingToUpdate.Key);
                    if (conflicting != null && conflicting.Id != existingSetting.Id)
                    {
                        operation.MarkAsFailed($"Ustawienie o kluczu '{settingToUpdate.Key}' już istnieje.");
                        _logger.LogError("Nie można zaktualizować ustawienia: Klucz '{Key}' już istnieje.", settingToUpdate.Key);
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
                operation.TargetEntityName = existingSetting.Key;
                operation.MarkAsCompleted("Ustawienie aplikacji przygotowane do aktualizacji.");

                InvalidateSettingCache(existingSetting.Key, existingSetting.Category, oldCategory);
                if (oldKey != null && oldKey != existingSetting.Key)
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

        public async Task<bool> DeleteSettingAsync(string key)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete_setting";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.ApplicationSettingDeleted,
                TargetEntityType = nameof(ApplicationSetting),
                TargetEntityId = key,
                TargetEntityName = key,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Usuwanie ustawienia o kluczu: {Key}", key);
            string? categoryOfDeletedSetting = null;
            string? idOfDeletedSetting = null;

            try
            {
                var setting = await _settingsRepository.GetSettingByKeyAsync(key);
                if (setting == null)
                {
                    operation.MarkAsFailed($"Ustawienie o kluczu '{key}' nie zostało znalezione.");
                    _logger.LogWarning("Nie można usunąć ustawienia: Klucz {Key} nie znaleziony.", key);
                    return false;
                }

                operation.TargetEntityId = setting.Id;
                categoryOfDeletedSetting = setting.Category;
                idOfDeletedSetting = setting.Id;


                if (!setting.IsActive)
                {
                    operation.MarkAsCompleted($"Ustawienie '{key}' było już nieaktywne. Brak akcji.");
                    _logger.LogInformation("Ustawienie o kluczu {Key} było już nieaktywne.", key);
                    InvalidateSettingCache(key, categoryOfDeletedSetting);
                    return true;
                }

                setting.MarkAsDeleted(currentUserUpn);
                _settingsRepository.Update(setting);
                operation.MarkAsCompleted("Ustawienie aplikacji oznaczone jako usunięte.");

                InvalidateSettingCache(key, categoryOfDeletedSetting);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania ustawienia aplikacji o kluczu {Key}. Wiadomość: {ErrorMessage}", key, ex.Message);
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

        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a ustawień aplikacji.");

            // Anuluj stary token, co spowoduje unieważnienie wszystkich powiązanych wpisów
            var oldTokenSource = Interlocked.Exchange(ref _settingsCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose(); // Zwolnij zasoby starego tokenu
                _logger.LogInformation("Stary CancellationTokenSource dla cache'a ustawień został anulowany i usunięty.");
            }
            else
            {
                _logger.LogDebug("Nie było aktywnego CancellationTokenSource do anulowania lub był już anulowany.");
            }

            _logger.LogInformation("Cache ustawień aplikacji został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        private void InvalidateSettingCache(string key, string? category, string? oldCategory = null)
        {
            _logger.LogDebug("Inwalidacja cache dla klucza: {Key}, kategoria: {Category}, stara kategoria: {OldCategory}", key, category, oldCategory);
            _cache.Remove(SettingByKeyCacheKeyPrefix + key);
            if (!string.IsNullOrWhiteSpace(category))
            {
                _cache.Remove(SettingsByCategoryCacheKeyPrefix + category);
            }
            if (!string.IsNullOrWhiteSpace(oldCategory) && oldCategory != category)
            {
                _cache.Remove(SettingsByCategoryCacheKeyPrefix + oldCategory);
            }
            _cache.Remove(AllSettingsCacheKey);
            _logger.LogInformation("Cache dla ustawienia '{Key}' i powiązanych kategorii został zinvalidowany.", key);
            // Nie ma potrzeby wywoływania _settingsCacheTokenSource.Cancel() tutaj,
            // ponieważ to unieważniłoby *wszystkie* ustawienia, a chcemy tylko te konkretne.
            // RefreshCacheAsync() służy do globalnego resetu.
        }


        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id)) operation.Id = Guid.NewGuid().ToString();
            if (string.IsNullOrEmpty(operation.CreatedBy))
                operation.CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system_log_save";

            var existingLog = await _operationHistoryRepository.GetByIdAsync(operation.Id);
            if (existingLog == null)
            {
                if (operation.StartedAt == default(DateTime) && (operation.Status == OperationStatus.InProgress || operation.Status == OperationStatus.Pending))
                {
                    operation.StartedAt = DateTime.UtcNow;
                }
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
