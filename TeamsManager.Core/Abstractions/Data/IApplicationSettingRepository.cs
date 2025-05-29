using TeamsManager.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Data
{
    /// <summary>
    /// Interfejs repozytorium dla encji ApplicationSetting, rozszerzający IGenericRepository.
    /// </summary>
    public interface IApplicationSettingRepository : IGenericRepository<ApplicationSetting>
    {
        /// <summary>
        /// Asynchronicznie pobiera ustawienie aplikacji na podstawie jego unikalnego klucza.
        /// </summary>
        /// <param name="key">Unikalny klucz ustawienia.</param>
        /// <returns>Obiekt ApplicationSetting lub null, jeśli ustawienie o danym kluczu nie istnieje.</returns>
        Task<ApplicationSetting?> GetSettingByKeyAsync(string key);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie ustawienia aplikacji należące do określonej kategorii.
        /// </summary>
        /// <param name="category">Nazwa kategorii.</param>
        /// <returns>Kolekcja ustawień aplikacji z danej kategorii.</returns>
        Task<IEnumerable<ApplicationSetting>> GetSettingsByCategoryAsync(string category);
    }
}