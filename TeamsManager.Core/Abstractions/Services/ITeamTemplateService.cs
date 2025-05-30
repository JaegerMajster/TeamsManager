using TeamsManager.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za logikę biznesową związaną z szablonami zespołów (TeamTemplate).
    /// </summary>
    public interface ITeamTemplateService
    {
        /// <summary>
        /// Asynchronicznie pobiera szablon na podstawie jego ID.
        /// </summary>
        /// <param name="templateId">Identyfikator szablonu.</param>
        /// <returns>Obiekt TeamTemplate lub null, jeśli nie znaleziono.</returns>
        Task<TeamTemplate?> GetTemplateByIdAsync(string templateId);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne szablony.
        /// </summary>
        /// <returns>Kolekcja wszystkich aktywnych szablonów.</returns>
        Task<IEnumerable<TeamTemplate>> GetAllActiveTemplatesAsync();

        /// <summary>
        /// Asynchronicznie pobiera wszystkie szablony uniwersalne.
        /// </summary>
        /// <returns>Kolekcja aktywnych szablonów uniwersalnych.</returns>
        Task<IEnumerable<TeamTemplate>> GetUniversalTemplatesAsync();

        /// <summary>
        /// Asynchronicznie pobiera wszystkie szablony przypisane do konkretnego typu szkoły.
        /// </summary>
        /// <param name="schoolTypeId">Identyfikator typu szkoły.</param>
        /// <returns>Kolekcja aktywnych szablonów dla danego typu szkoły.</returns>
        Task<IEnumerable<TeamTemplate>> GetTemplatesBySchoolTypeAsync(string schoolTypeId);

        /// <summary>
        /// Asynchronicznie pobiera domyślny szablon dla danego typu szkoły.
        /// </summary>
        /// <param name="schoolTypeId">Identyfikator typu szkoły.</param>
        /// <returns>Obiekt TeamTemplate lub null.</returns>
        Task<TeamTemplate?> GetDefaultTemplateForSchoolTypeAsync(string schoolTypeId);

        /// <summary>
        /// Asynchronicznie tworzy nowy szablon zespołu.
        /// </summary>
        /// <param name="name">Nazwa szablonu.</param>
        /// <param name="templateContent">Wzorzec nazwy z placeholderami.</param>
        /// <param name="description">Opis szablonu.</param>
        /// <param name="isUniversal">Czy szablon jest uniwersalny.</param>
        /// <param name="schoolTypeId">Opcjonalny ID typu szkoły (jeśli nie jest uniwersalny).</param>
        /// <param name="category">Kategoria szablonu.</param>
        /// <returns>Utworzony obiekt TeamTemplate lub null, jeśli operacja się nie powiodła.</returns>
        Task<TeamTemplate?> CreateTemplateAsync(
            string name,
            string templateContent,
            string description,
            bool isUniversal,
            string? schoolTypeId = null,
            string category = "Ogólne");

        /// <summary>
        /// Asynchronicznie aktualizuje dane istniejącego szablonu zespołu.
        /// </summary>
        /// <param name="templateToUpdate">Obiekt TeamTemplate z zaktualizowanymi danymi.</param>
        /// <returns>True, jeśli aktualizacja się powiodła.</returns>
        Task<bool> UpdateTemplateAsync(TeamTemplate templateToUpdate);

        /// <summary>
        /// Asynchronicznie usuwa (logicznie) szablon zespołu.
        /// </summary>
        /// <param name="templateId">Identyfikator szablonu do usunięcia.</param>
        /// <returns>True, jeśli usunięcie (dezaktywacja) się powiodło.</returns>
        Task<bool> DeleteTemplateAsync(string templateId);

        /// <summary>
        /// Asynchronicznie generuje przykładową nazwę zespołu na podstawie szablonu i dostarczonych wartości.
        /// </summary>
        /// <param name="templateId">Identyfikator szablonu.</param>
        /// <param name="values">Słownik wartości dla placeholderów.</param>
        /// <returns>Wygenerowana nazwa zespołu lub null, jeśli szablon nie istnieje.</returns>
        Task<string?> GenerateTeamNameFromTemplateAsync(string templateId, Dictionary<string, string> values);

        /// <summary>
        /// Asynchronicznie klonuje istniejący szablon, nadając mu nową nazwę.
        /// </summary>
        /// <param name="originalTemplateId">ID oryginalnego szablonu do sklonowania.</param>
        /// <param name="newTemplateName">Nowa nazwa dla sklonowanego szablonu.</param>
        /// <returns>Sklonowany obiekt TeamTemplate lub null, jeśli operacja się nie powiodła.</returns>
        Task<TeamTemplate?> CloneTemplateAsync(string originalTemplateId, string newTemplateName);
    }
}