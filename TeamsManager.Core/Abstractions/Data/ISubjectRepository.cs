// Plik: TeamsManager.Core/Abstractions/Data/ISubjectRepository.cs
using TeamsManager.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Data
{
    /// <summary>
    /// Interfejs repozytorium dla operacji na encji Subject,
    /// rozszerzający generyczne repozytorium o specyficzne metody.
    /// </summary>
    public interface ISubjectRepository : IGenericRepository<Subject>
    {
        /// <summary>
        /// Asynchronicznie pobiera przedmiot na podstawie jego unikalnego kodu,
        /// dołączając domyślnie szczegóły takie jak DefaultSchoolType.
        /// </summary>
        /// <param name="code">Kod przedmiotu.</param>
        /// <returns>Znaleziony przedmiot lub null, jeśli nie istnieje.</returns>
        Task<Subject?> GetByCodeAsync(string code);

        /// <summary>
        /// Asynchronicznie pobiera listę nauczycieli przypisanych do danego przedmiotu.
        /// </summary>
        /// <param name="subjectId">ID przedmiotu.</param>
        /// <returns>Kolekcja aktywnych nauczycieli przypisanych do przedmiotu.</returns>
        Task<IEnumerable<User>> GetTeachersAsync(string subjectId);

        /// <summary>
        /// Asynchronicznie pobiera przedmiot po jego ID, dołączając szczegóły
        /// takie jak DefaultSchoolType.
        /// </summary>
        /// <param name="subjectId">ID przedmiotu.</param>
        /// <returns>Znaleziony, aktywny przedmiot lub null.</returns>
        Task<Subject?> GetByIdWithDetailsAsync(string subjectId);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne przedmioty, dołączając szczegóły
        /// takie jak DefaultSchoolType.
        /// </summary>
        /// <returns>Kolekcja aktywnych przedmiotów ze szczegółami.</returns>
        Task<IEnumerable<Subject>> GetAllActiveWithDetailsAsync();
    }
}