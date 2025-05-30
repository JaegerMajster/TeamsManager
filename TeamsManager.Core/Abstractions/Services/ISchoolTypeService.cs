using TeamsManager.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za logikę biznesową związaną z typami szkół (SchoolType).
    /// </summary>
    public interface ISchoolTypeService
    {
        /// <summary>
        /// Asynchronicznie pobiera typ szkoły na podstawie jego ID.
        /// </summary>
        /// <param name="schoolTypeId">Identyfikator typu szkoły.</param>
        /// <returns>Obiekt SchoolType lub null, jeśli nie znaleziono.</returns>
        Task<SchoolType?> GetSchoolTypeByIdAsync(string schoolTypeId);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne typy szkół.
        /// </summary>
        /// <returns>Kolekcja wszystkich aktywnych typów szkół.</returns>
        Task<IEnumerable<SchoolType>> GetAllActiveSchoolTypesAsync();

        /// <summary>
        /// Asynchronicznie tworzy nowy typ szkoły.
        /// </summary>
        /// <param name="shortName">Skrócona nazwa typu szkoły (unikalna).</param>
        /// <param name="fullName">Pełna nazwa typu szkoły.</param>
        /// <param name="description">Opis typu szkoły.</param>
        /// <param name="colorCode">Opcjonalny kod koloru dla UI.</param>
        /// <param name="sortOrder">Opcjonalna kolejność sortowania.</param>
        /// <returns>Utworzony obiekt SchoolType lub null, jeśli operacja się nie powiodła (np. z powodu duplikatu ShortName).</returns>
        Task<SchoolType?> CreateSchoolTypeAsync(
            string shortName,
            string fullName,
            string description,
            string? colorCode = null,
            int sortOrder = 0);

        /// <summary>
        /// Asynchronicznie aktualizuje dane istniejącego typu szkoły.
        /// </summary>
        /// <param name="schoolTypeToUpdate">Obiekt SchoolType z zaktualizowanymi danymi.</param>
        /// <returns>True, jeśli aktualizacja się powiodła.</returns>
        Task<bool> UpdateSchoolTypeAsync(SchoolType schoolTypeToUpdate);

        /// <summary>
        /// Asynchronicznie usuwa (logicznie) typ szkoły.
        /// Uwaga: Należy rozważyć, co z powiązanymi zespołami, szablonami, przypisaniami nauczycieli.
        /// </summary>
        /// <param name="schoolTypeId">Identyfikator typu szkoły do usunięcia.</param>
        /// <returns>True, jeśli usunięcie (dezaktywacja) się powiodło.</returns>
        Task<bool> DeleteSchoolTypeAsync(string schoolTypeId);

        /// <summary>
        /// Asynchronicznie przypisuje wicedyrektora jako nadzorującego dany typ szkoły.
        /// </summary>
        /// <param name="viceDirectorUserId">ID użytkownika (wicedyrektora).</param>
        /// <param name="schoolTypeId">ID typu szkoły.</param>
        /// <returns>True, jeśli przypisanie się powiodło.</returns>
        Task<bool> AssignViceDirectorToSchoolTypeAsync(string viceDirectorUserId, string schoolTypeId);

        /// <summary>
        /// Asynchronicznie usuwa przypisanie wicedyrektora od nadzorowania danego typu szkoły.
        /// </summary>
        /// <param name="viceDirectorUserId">ID użytkownika (wicedyrektora).</param>
        /// <param name="schoolTypeId">ID typu szkoły.</param>
        /// <returns>True, jeśli usunięcie przypisania się powiodło.</returns>
        Task<bool> RemoveViceDirectorFromSchoolTypeAsync(string viceDirectorUserId, string schoolTypeId);
    }
}