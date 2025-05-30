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
using System.Text.Json;
// using System.Text.Json; // Może być potrzebne, jeśli GetSettingValueAsync<T> będzie deserializować JSON

namespace TeamsManager.Core.Services
{
    public class ApplicationSettingService : IApplicationSettingService
    {
        private readonly IApplicationSettingRepository _settingsRepository;
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ApplicationSettingService> _logger;
        // TODO: Rozważyć implementację mechanizmu cache'owania (np. IMemoryCache)

        public ApplicationSettingService(
            IApplicationSettingRepository settingsRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<ApplicationSettingService> logger)
        {
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ApplicationSetting?> GetSettingByKeyAsync(string key)
        {
            _logger.LogInformation("Pobieranie ustawienia aplikacji o kluczu: {Key}", key);
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogWarning("Próba pobrania ustawienia z pustym kluczem.");
                return null;
            }
            return await _settingsRepository.GetSettingByKeyAsync(key);
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
                        // Bezpośrednie rzutowanie na string, a potem na T? jeśli T jest stringiem
                        if (typeof(T) == typeof(string))
                            return (T)(object)setting.GetStringValue(); // Rzutowanie przez object
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
                            // Próbujemy przekonwertować na T. Jeśli T jest DateTime, a parsedDateTime ma wartość, to się uda.
                            // Jeśli T jest DateTime?, to też się uda.
                            if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
                            {
                                return (T)(object)parsedDateTime.Value;
                            }
                        }
                        else // parsedDateTime jest null
                        {
                            // Jeśli T jest typem nullowalnym (np. DateTime?), możemy zwrócić null (co jest wartością defaultValue, jeśli nie podano innej).
                            // Jeśli T jest nienullowalnym DateTime, a oczekujemy null, to jest problem.
                            // Najbezpieczniej jest zwrócić defaultValue.
                            if (typeof(T) == typeof(DateTime?) || defaultValue != null)
                            {
                                return defaultValue;
                            }
                            // Jeśli T to DateTime (nienullowalne) i defaultValue to null,
                            // zwracamy default(DateTime) czyli DateTime.MinValue, logując ostrzeżenie.
                            _logger.LogWarning("Nie można przekonwertować wartości null z GetDateTimeValue() na nienullowalny typ {TypeName} dla klucza '{Key}'. Zwracanie DateTime.MinValue.", typeof(T).Name, key);
                            return default(T); // default(DateTime) to 01/01/0001
                        }
                        // Jeśli doszliśmy tutaj, to typ T nie jest ani DateTime ani DateTime?
                        _logger.LogWarning("Niezgodność typu dla ustawienia '{Key}'. Oczekiwano {ExpectedType}, znaleziono {ActualType} (DateTime).", key, typeof(T).Name, setting.Type);
                        return defaultValue;

                    case SettingType.Decimal:
                        if (typeof(T) == typeof(decimal) || typeof(T) == typeof(decimal?))
                            return (T)(object)setting.GetDecimalValue();
                        _logger.LogWarning("Niezgodność typu dla ustawienia '{Key}'. Oczekiwano {ExpectedType}, znaleziono {ActualType} (Decimal).", key, typeof(T).Name, setting.Type);
                        return defaultValue;

                    case SettingType.Json:
                        // Sprawdzamy, czy T jest typem referencyjnym (class) przed wywołaniem GetJsonValue<T>()
                        if (typeof(T).IsClass) // typeof(T).IsClass jest dobrym przybliżeniem dla "typ referencyjny"
                        {
                            // Możemy teraz bezpiecznie wywołać GetJsonValue<T>(), ale T nadal nie ma ograniczenia "class"
                            // Aby to zadziałało, musimy użyć refleksji lub rzutowania dynamicznego,
                            // albo zmodyfikować GetJsonValue<T>, aby nie miało ograniczenia 'class' (ale wtedy straci bezpieczeństwo).
                            // Prostsze rozwiązanie:
                            try
                            {
                                // Próbujemy deserializować bezpośrednio, jeśli T jest typem referencyjnym
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
            // TODO: Implementacja cache'owania z uwzględnieniem forceRefresh
            return await _settingsRepository.FindAsync(s => s.IsActive);
        }

        public async Task<IEnumerable<ApplicationSetting>> GetSettingsByCategoryAsync(string category, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie aktywnych ustawień aplikacji dla kategorii: {Category}. Wymuś odświeżenie: {ForceRefresh}", category, forceRefresh);
            if (string.IsNullOrWhiteSpace(category))
            {
                _logger.LogWarning("Próba pobrania ustawień dla pustej kategorii.");
                return Enumerable.Empty<ApplicationSetting>();
            }
            // TODO: Implementacja cache'owania z uwzględnieniem forceRefresh
            return await _settingsRepository.GetSettingsByCategoryAsync(category);
        }

        public async Task<bool> SaveSettingAsync(string key, string value, SettingType type, string? description = null, string? category = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_save_setting";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                TargetEntityType = nameof(ApplicationSetting),
                TargetEntityId = key, // Używamy klucza jako quasi-ID dla logu
                TargetEntityName = key,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            // Typ operacji będzie zależał od tego, czy tworzymy, czy aktualizujemy

            _logger.LogInformation("Zapisywanie ustawienia: Klucz={Key}, Wartość='{Value}', Typ={Type} przez {User}", key, value, type, currentUserUpn);

            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogError("Nie można zapisać ustawienia: Klucz nie może być pusty.");
                // Nie logujemy OperationHistory, bo operacja nie może być nawet rozpoczęta poprawnie
                return false;
            }

            var existingSetting = await _settingsRepository.GetSettingByKeyAsync(key);
            bool success = false;

            try
            {
                if (existingSetting != null)
                {
                    operation.Type = OperationType.ApplicationSettingUpdated;
                    operation.MarkAsStarted();
                    _logger.LogInformation("Aktualizowanie istniejącego ustawienia: {Key}", key);

                    existingSetting.Value = value;
                    existingSetting.Type = type;
                    if (description != null) existingSetting.Description = description; // Aktualizuj tylko jeśli podano
                    if (category != null) existingSetting.Category = category;     // Aktualizuj tylko jeśli podano
                    existingSetting.MarkAsModified(currentUserUpn);
                    _settingsRepository.Update(existingSetting);
                    operation.MarkAsCompleted($"Ustawienie '{key}' zaktualizowane.");
                }
                else
                {
                    operation.Type = OperationType.ApplicationSettingCreated;
                    operation.MarkAsStarted();
                    _logger.LogInformation("Tworzenie nowego ustawienia: {Key}", key);
                    var newSetting = new ApplicationSetting
                    {
                        Id = Guid.NewGuid().ToString(),
                        Key = key,
                        Value = value,
                        Type = type,
                        Description = description ?? string.Empty,
                        Category = category ?? "General",
                        IsRequired = false, // Domyślne wartości, można je dodać do parametrów metody
                        IsVisible = true,
                        CreatedBy = currentUserUpn,
                        IsActive = true
                    };
                    await _settingsRepository.AddAsync(newSetting);
                    operation.TargetEntityId = newSetting.Id; // Aktualizujemy ID w logu na ID encji
                    operation.MarkAsCompleted($"Ustawienie '{key}' utworzone.");
                }
                // SaveChangesAsync na wyższym poziomie
                success = true;
                _logger.LogInformation("Ustawienie '{Key}' pomyślnie przygotowane do zapisu.", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas zapisywania ustawienia {Key}. Wiadomość: {ErrorMessage}", key, ex.Message);
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
            var operation = new OperationHistory { /* ... Pełna inicjalizacja dla ApplicationSettingUpdated ... */ };
            operation.MarkAsStarted();
            _logger.LogInformation("Aktualizowanie obiektu ApplicationSetting ID: {SettingId}, Klucz: {Key}", settingToUpdate.Id, settingToUpdate.Key);

            try
            {
                var existingSetting = await _settingsRepository.GetByIdAsync(settingToUpdate.Id);
                if (existingSetting == null)
                {
                    operation.MarkAsFailed($"Ustawienie o ID '{settingToUpdate.Id}' nie istnieje.");
                    _logger.LogWarning("Nie można zaktualizować ustawienia ID {SettingId} - nie istnieje.", settingToUpdate.Id);
                    return false;
                }
                // Walidacja unikalności klucza, jeśli jest zmieniany
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

                // Mapowanie pól
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
                operation.MarkAsCompleted("Ustawienie aplikacji przygotowane do aktualizacji.");
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
            var operation = new OperationHistory { /* ... Pełna inicjalizacja dla ApplicationSettingDeleted ... */ };
            operation.MarkAsStarted();
            _logger.LogInformation("Usuwanie ustawienia o kluczu: {Key}", key);
            try
            {
                var setting = await _settingsRepository.GetSettingByKeyAsync(key);
                if (setting == null) { /* ... */ return false; }
                operation.TargetEntityId = setting.Id;
                operation.TargetEntityName = setting.Key;

                setting.MarkAsDeleted(currentUserUpn); // Soft delete
                _settingsRepository.Update(setting);
                operation.MarkAsCompleted("Ustawienie aplikacji oznaczone jako usunięte.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania ustawienia aplikacji o kluczu {Key}. Wiadomość: {ErrorMessage}", key, ex.Message);
                // Upewniamy się, że operation nie jest null, chociaż w tym przepływie powinno być zawsze zainicjowane
                // if (operation != null) // Można pominąć, jeśli operation jest zawsze inicjowane na początku
                // {
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                // }
                return false;
            }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Odświeżanie cache'a ustawień aplikacji (TODO: Implementacja).");
            // TODO: Implementacja logiki czyszczenia i ponownego ładowania cache'a
            return Task.CompletedTask;
        }

        // Metoda pomocnicza do zapisu OperationHistory (taka sama jak w innych serwisach)
        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id)) operation.Id = Guid.NewGuid().ToString();
            if (string.IsNullOrEmpty(operation.CreatedBy)) operation.CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system_log_save";

            var existingLog = await _operationHistoryRepository.GetByIdAsync(operation.Id);
            if (existingLog == null) await _operationHistoryRepository.AddAsync(operation);
            else { /* Logika aktualizacji existingLog */ _operationHistoryRepository.Update(existingLog); }
        }
    }
}