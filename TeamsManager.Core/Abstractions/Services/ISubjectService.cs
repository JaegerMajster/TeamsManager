using TeamsManager.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za logikę biznesową związaną z przedmiotami (Subject).
    /// </summary>
    public interface ISubjectService
    {
        /// <summary>
        /// Asynchronicznie pobiera przedmiot na podstawie jego ID.
        /// </summary>
        /// <param name="subjectId">Identyfikator przedmiotu.</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Obiekt Subject lub null, jeśli nie znaleziono.</returns>
        Task<Subject?> GetSubjectByIdAsync(string subjectId, bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne przedmioty.
        /// </summary>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja wszystkich aktywnych przedmiotów.</returns>
        Task<IEnumerable<Subject>> GetAllActiveSubjectsAsync(bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie tworzy nowy przedmiot.
        /// </summary>
        /// <param name="name">Nazwa przedmiotu.</param>
        /// <param name="code">Opcjonalny kod przedmiotu.</param>
        /// <param name="description">Opcjonalny opis przedmiotu.</param>
        /// <param name="hours">Opcjonalna liczba godzin.</param>
        /// <param name="defaultSchoolTypeId">Opcjonalny ID domyślnego typu szkoły.</param>
        /// <param name="category">Opcjonalna kategoria przedmiotu.</param>
        /// <returns>Utworzony obiekt Subject lub null, jeśli operacja się nie powiodła.</returns>
        Task<Subject?> CreateSubjectAsync(
            string name,
            string? code = null,
            string? description = null,
            int? hours = null,
            string? defaultSchoolTypeId = null,
            string? category = null);

        /// <summary>
        /// Asynchronicznie aktualizuje dane istniejącego przedmiotu.
        /// </summary>
        /// <param name="subjectToUpdate">Obiekt Subject z zaktualizowanymi danymi.</param>
        /// <returns>True, jeśli aktualizacja się powiodła.</returns>
        Task<bool> UpdateSubjectAsync(Subject subjectToUpdate);

        /// <summary>
        /// Asynchronicznie usuwa (logicznie) przedmiot.
        /// Usuwa również powiązane przypisania nauczycieli do tego przedmiotu (UserSubject).
        /// </summary>
        /// <param name="subjectId">Identyfikator przedmiotu do usunięcia.</param>
        /// <returns>True, jeśli usunięcie (dezaktywacja) się powiodło.</returns>
        Task<bool> DeleteSubjectAsync(string subjectId);

        /// <summary>
        /// Asynchronicznie pobiera wszystkich aktywnych nauczycieli przypisanych do danego przedmiotu.
        /// </summary>
        /// <param name="subjectId">Identyfikator przedmiotu.</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja użytkowników (nauczycieli) przypisanych do przedmiotu.</returns>
        Task<IEnumerable<User>> GetTeachersForSubjectAsync(string subjectId, bool forceRefresh = false);

        /// <summary>
        /// Odświeża cache przedmiotów (jeśli jest używany).
        /// </summary>
        Task RefreshCacheAsync();
    }
}