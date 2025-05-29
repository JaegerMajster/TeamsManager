using TeamsManager.Core.Models;
using System.Threading.Tasks;
using System.Collections.Generic; // Dla IEnumerable
using System; // Dla DateTime

namespace TeamsManager.Core.Abstractions.Data
{
    /// <summary>
    /// Interfejs repozytorium dla encji SchoolYear, rozszerzający IGenericRepository.
    /// </summary>
    public interface ISchoolYearRepository : IGenericRepository<SchoolYear>
    {
        /// <summary>
        /// Asynchronicznie pobiera bieżący rok szkolny (oznaczony jako IsCurrent = true).
        /// </summary>
        /// <returns>Obiekt SchoolYear reprezentujący bieżący rok szkolny lub null, jeśli żaden nie jest ustawiony.</returns>
        Task<SchoolYear?> GetCurrentSchoolYearAsync();

        /// <summary>
        /// Asynchronicznie pobiera rok szkolny na podstawie jego nazwy.
        /// </summary>
        /// <param name="name">Nazwa roku szkolnego (np. "2024/2025").</param>
        /// <returns>Obiekt SchoolYear lub null, jeśli nie znaleziono.</returns>
        Task<SchoolYear?> GetSchoolYearByNameAsync(string name);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie lata szkolne, które są aktywne w podanym dniu.
        /// </summary>
        /// <param name="date">Data, dla której sprawdzane są lata szkolne.</param>
        /// <returns>Kolekcja aktywnych lat szkolnych w danym dniu.</returns>
        Task<IEnumerable<SchoolYear>> GetSchoolYearsActiveOnDateAsync(DateTime date);
    }
}