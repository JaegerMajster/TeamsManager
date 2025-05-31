using TeamsManager.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System; // Dla DateTime

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za logikę biznesową związaną z latami szkolnymi (SchoolYear).
    /// </summary>
    public interface ISchoolYearService
    {
        /// <summary>
        /// Asynchronicznie pobiera rok szkolny na podstawie jego ID.
        /// </summary>
        /// <param name="schoolYearId">Identyfikator roku szkolnego.</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Obiekt SchoolYear lub null, jeśli nie znaleziono.</returns>
        Task<SchoolYear?> GetSchoolYearByIdAsync(string schoolYearId, bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne lata szkolne.
        /// </summary>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja wszystkich aktywnych lat szkolnych.</returns>
        Task<IEnumerable<SchoolYear>> GetAllActiveSchoolYearsAsync(bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie pobiera bieżący rok szkolny (oznaczony jako IsCurrent = true).
        /// </summary>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Obiekt SchoolYear lub null, jeśli żaden rok nie jest ustawiony jako bieżący.</returns>
        Task<SchoolYear?> GetCurrentSchoolYearAsync(bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie ustawia dany rok szkolny jako bieżący.
        /// Zapewnia, że tylko jeden rok szkolny może być oznaczony jako bieżący.
        /// </summary>
        /// <param name="schoolYearId">Identyfikator roku szkolnego do ustawienia jako bieżący.</param>
        /// <returns>True, jeśli operacja się powiodła.</returns>
        Task<bool> SetCurrentSchoolYearAsync(string schoolYearId);

        /// <summary>
        /// Asynchronicznie tworzy nowy rok szkolny.
        /// </summary>
        /// <param name="name">Nazwa roku szkolnego (np. "2024/2025"). Musi być unikalna.</param>
        /// <param name="startDate">Data rozpoczęcia roku szkolnego.</param>
        /// <param name="endDate">Data zakończenia roku szkolnego.</param>
        /// <param name="description">Opcjonalny opis.</param>
        /// <param name="firstSemesterStart">Opcjonalna data rozpoczęcia pierwszego semestru.</param>
        /// <param name="firstSemesterEnd">Opcjonalna data zakończenia pierwszego semestru.</param>
        /// <param name="secondSemesterStart">Opcjonalna data rozpoczęcia drugiego semestru.</param>
        /// <param name="secondSemesterEnd">Opcjonalna data zakończenia drugiego semestru.</param>
        /// <returns>Utworzony obiekt SchoolYear lub null, jeśli operacja się nie powiodła (np. z powodu duplikatu nazwy).</returns>
        Task<SchoolYear?> CreateSchoolYearAsync(
            string name,
            DateTime startDate,
            DateTime endDate,
            string? description = null,
            DateTime? firstSemesterStart = null,
            DateTime? firstSemesterEnd = null,
            DateTime? secondSemesterStart = null,
            DateTime? secondSemesterEnd = null);

        /// <summary>
        /// Asynchronicznie aktualizuje dane istniejącego roku szkolnego.
        /// </summary>
        /// <param name="schoolYearToUpdate">Obiekt SchoolYear z zaktualizowanymi danymi.</param>
        /// <returns>True, jeśli aktualizacja się powiodła.</returns>
        Task<bool> UpdateSchoolYearAsync(SchoolYear schoolYearToUpdate);

        /// <summary>
        /// Asynchronicznie usuwa (logicznie) rok szkolny.
        /// </summary>
        /// <param name="schoolYearId">Identyfikator roku szkolnego do usunięcia.</param>
        /// <returns>True, jeśli usunięcie (dezaktywacja) się powiodło.</returns>
        Task<bool> DeleteSchoolYearAsync(string schoolYearId);

        /// <summary>
        /// Odświeża cache lat szkolnych (jeśli jest używany).
        /// </summary>
        Task RefreshCacheAsync();
    }
}