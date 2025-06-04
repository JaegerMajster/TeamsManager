using TeamsManager.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Data
{
    /// <summary>
    /// Interfejs repozytorium dla encji TeamTemplate, rozszerzający IGenericRepository.
    /// </summary>
    public interface ITeamTemplateRepository : IGenericRepository<TeamTemplate>
    {
        /// <summary>
        /// Asynchronicznie pobiera domyślny szablon dla danego typu szkoły (jeśli istnieje).
        /// </summary>
        /// <param name="schoolTypeId">Identyfikator typu szkoły.</param>
        /// <returns>Obiekt TeamTemplate lub null, jeśli nie znaleziono domyślnego szablonu dla tego typu szkoły.</returns>
        Task<TeamTemplate?> GetDefaultTemplateForSchoolTypeAsync(string schoolTypeId);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie szablony uniwersalne.
        /// </summary>
        /// <returns>Kolekcja uniwersalnych szablonów TeamTemplate.</returns>
        Task<IEnumerable<TeamTemplate>> GetUniversalTemplatesAsync();

        /// <summary>
        /// Asynchronicznie pobiera wszystkie szablony przypisane do konkretnego typu szkoły.
        /// </summary>
        /// <param name="schoolTypeId">Identyfikator typu szkoły.</param>
        /// <returns>Kolekcja szablonów TeamTemplate przypisanych do danego typu szkoły.</returns>
        Task<IEnumerable<TeamTemplate>> GetTemplatesBySchoolTypeAsync(string schoolTypeId);

        /// <summary>
        /// Asynchronicznie wyszukuje szablony na podstawie fragmentu nazwy lub opisu.
        /// </summary>
        /// <param name="searchTerm">Fragment tekstu do wyszukania.</param>
        /// <returns>Kolekcja pasujących szablonów TeamTemplate.</returns>
        Task<IEnumerable<TeamTemplate>> SearchTemplatesAsync(string searchTerm);

        /// <summary>
        /// Asynchronicznie zapisuje wszystkie zmiany do bazy danych.
        /// </summary>
        /// <returns>Liczba zmienionych wpisów w bazie danych.</returns>
        Task<int> SaveChangesAsync();

        // Metoda do "budowania" nowego szablonu (czyli jego tworzenia z walidacją)
        // mogłaby być częścią serwisu TeamTemplateService, który używałby tego repozytorium do zapisu.
        // Repozytorium jest odpowiedzialne za utrwalanie i pobieranie danych.
        // Logika biznesowa "budowania" (np. walidacja, ustawianie wartości domyślnych)
        // zwykle znajduje się w serwisie lub w samej encji (jeśli dotyczy jej wewnętrznego stanu).

        // Jeśli jednak "budowanie" oznacza tu bardziej złożone zapytanie tworzące predefiniowany szablon,
        // to mogłoby tu trafić. Na razie skupmy się na metodach pobierania.
    }
}