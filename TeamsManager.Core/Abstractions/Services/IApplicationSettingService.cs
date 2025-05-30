using TeamsManager.Core.Models;
using TeamsManager.Core.Enums; // Dla SettingType
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za zarządzanie ustawieniami aplikacji (ApplicationSetting).
    /// Może implementować mechanizm cache'owania dla poprawy wydajności.
    /// </summary>
    public interface IApplicationSettingService
    {
        /// <summary>
        /// Asynchronicznie pobiera wartość ustawienia o podanym kluczu.
        /// Próbuje przekonwertować wartość na typ <typeparamref name="T"/>.
        /// Może korzystać z cache'a.
        /// </summary>
        /// <typeparam name="T">Oczekiwany typ wartości ustawienia (np. string, int, bool, decimal, DateTime).</typeparam>
        /// <param name="key">Klucz ustawienia.</param>
        /// <param name="defaultValue">Wartość domyślna, jeśli ustawienie nie zostanie znalezione lub konwersja się nie powiedzie.</param>
        /// <returns>Wartość ustawienia przekonwertowana na typ <typeparamref name="T"/> lub wartość domyślna.</returns>
        Task<T?> GetSettingValueAsync<T>(string key, T? defaultValue = default);

        /// <summary>
        /// Asynchronicznie pobiera obiekt ApplicationSetting na podstawie jego klucza.
        /// Może korzystać z cache'a.
        /// </summary>
        /// <param name="key">Klucz ustawienia.</param>
        /// <returns>Obiekt ApplicationSetting lub null, jeśli nie znaleziono.</returns>
        Task<ApplicationSetting?> GetSettingByKeyAsync(string key);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne ustawienia aplikacji.
        /// </summary>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache'a.</param>
        /// <returns>Kolekcja wszystkich aktywnych ustawień aplikacji.</returns>
        Task<IEnumerable<ApplicationSetting>> GetAllSettingsAsync(bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne ustawienia aplikacji z danej kategorii.
        /// </summary>
        /// <param name="category">Nazwa kategorii.</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache'a.</param>
        /// <returns>Kolekcja aktywnych ustawień z danej kategorii.</returns>
        Task<IEnumerable<ApplicationSetting>> GetSettingsByCategoryAsync(string category, bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie aktualizuje wartość istniejącego ustawienia lub tworzy nowe, jeśli nie istnieje.
        /// </summary>
        /// <param name="key">Klucz ustawienia.</param>
        /// <param name="value">Nowa wartość ustawienia (jako string).</param>
        /// <param name="type">Typ danych wartości.</param>
        /// <param name="description">Opcjonalny opis.</param>
        /// <param name="category">Opcjonalna kategoria.</param>
        /// <returns>True, jeśli operacja się powiodła.</returns>
        Task<bool> SaveSettingAsync(string key, string value, SettingType type, string? description = null, string? category = null);

        /// <summary>
        /// Asynchronicznie aktualizuje obiekt ApplicationSetting.
        /// </summary>
        /// <param name="settingToUpdate">Obiekt ApplicationSetting z zaktualizowanymi danymi.</param>
        /// <returns>True, jeśli aktualizacja się powiodła.</returns>
        Task<bool> UpdateSettingAsync(ApplicationSetting settingToUpdate);


        /// <summary>
        /// Usuwa (logicznie lub fizycznie) ustawienie o podanym kluczu.
        /// </summary>
        /// <param name="key">Klucz ustawienia do usunięcia.</param>
        /// <returns>True, jeśli usunięcie się powiodło.</returns>
        Task<bool> DeleteSettingAsync(string key);

        /// <summary>
        /// Odświeża cache ustawień (jeśli jest używany).
        /// </summary>
        Task RefreshCacheAsync();
    }
}