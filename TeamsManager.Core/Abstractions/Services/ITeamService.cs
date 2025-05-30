using TeamsManager.Core.Models;
using TeamsManager.Core.Enums; // Dla TeamMemberRole
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za logikę biznesową związaną z zespołami (Team).
    /// </summary>
    public interface ITeamService
    {
        /// <summary>
        /// Asynchronicznie tworzy nowy zespół na podstawie podanych parametrów,
        /// wykonuje operację w Microsoft Teams oraz zapisuje encję w lokalnej bazie danych.
        /// Loguje operację w historii.
        /// </summary>
        /// <param name="displayName">Nazwa wyświetlana nowego zespołu.</param>
        /// <param name="description">Opis zespołu.</param>
        /// <param name="ownerUpn">UPN użytkownika, który zostanie właścicielem zespołu.</param>
        /// <param name="teamTemplateId">Opcjonalny identyfikator szablonu zespołu do użycia.</param>
        /// <param name="schoolTypeId">Opcjonalny identyfikator typu szkoły.</param>
        /// <param name="schoolYearId">Opcjonalny identyfikator roku szkolnego.</param>
        /// <param name="additionalTemplateValues">Opcjonalny słownik wartości do wypełnienia placeholderów w szablonie.</param>
        /// <returns>Utworzony obiekt Team lub null, jeśli operacja się nie powiodła.</returns>
        Task<Team?> CreateTeamAsync(
            string displayName,
            string description,
            string ownerUpn,
            TeamVisibility visibility,
            string? teamTemplateId = null,
            string? schoolTypeId = null,
            string? schoolYearId = null,
            Dictionary<string, string>? additionalTemplateValues = null);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie zespoły.
        /// </summary>
        /// <returns>Kolekcja wszystkich zespołów.</returns>
        Task<IEnumerable<Team>> GetAllTeamsAsync();

        /// <summary>
        /// Asynchronicznie pobiera zespół na podstawie jego ID, opcjonalnie dołączając powiązane encje.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu.</param>
        /// <param name="includeMembers">Czy dołączyć członków zespołu.</param>
        /// <param name="includeChannels">Czy dołączyć kanały zespołu.</param>
        /// <returns>Obiekt Team lub null, jeśli nie znaleziono.</returns>
        Task<Team?> GetTeamByIdAsync(string teamId, bool includeMembers = false, bool includeChannels = false);

        /// <summary>
        /// Asynchronicznie aktualizuje dane zespołu w Microsoft Teams i w lokalnej bazie danych.
        /// Loguje operację w historii.
        /// </summary>
        /// <param name="team">Obiekt Team z zaktualizowanymi danymi.</param>
        /// <returns>True, jeśli aktualizacja się powiodła.</returns>
        Task<bool> UpdateTeamAsync(Team team);

        /// <summary>
        /// Asynchronicznie archiwizuje zespół w Microsoft Teams i w lokalnej bazie danych.
        /// Loguje operację w historii.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu do archiwizacji.</param>
        /// <param name="reason">Powód archiwizacji.</param>
        /// <returns>True, jeśli archiwizacja się powiodła.</returns>
        Task<bool> ArchiveTeamAsync(string teamId, string reason);

        /// <summary>
        /// Asynchronicznie przywraca zarchiwizowany zespół w Microsoft Teams i w lokalnej bazie danych.
        /// Loguje operację w historii.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu do przywrócenia.</param>
        /// <returns>True, jeśli przywrócenie się powiodło.</returns>
        Task<bool> RestoreTeamAsync(string teamId);

        /// <summary>
        /// Asynchronicznie usuwa zespół (logicznie lub fizycznie, w zależności od konfiguracji)
        /// w Microsoft Teams i w lokalnej bazie danych. Loguje operację w historii.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu do usunięcia.</param>
        /// <returns>True, jeśli usunięcie się powiodło.</returns>
        Task<bool> DeleteTeamAsync(string teamId); // Na razie może być to "soft delete"

        /// <summary>
        /// Asynchronicznie dodaje użytkownika jako członka do zespołu.
        /// Loguje operację w historii.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu.</param>
        /// <param name="userUpn">UPN użytkownika do dodania.</param>
        /// <param name="role">Rola członka w zespole.</param>
        /// <returns>Utworzony obiekt TeamMember lub null, jeśli operacja się nie powiodła.</returns>
        Task<TeamMember?> AddMemberAsync(string teamId, string userUpn, TeamMemberRole role);

        /// <summary>
        /// Asynchronicznie usuwa członka z zespołu.
        /// Loguje operację w historii.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu.</param>
        /// <param name="userId">Identyfikator użytkownika do usunięcia.</param>
        /// <returns>True, jeśli usunięcie członka się powiodło.</returns>
        Task<bool> RemoveMemberAsync(string teamId, string userId);

        // Można dodać więcej metod, np. do zarządzania kanałami,
        // choć zarządzanie kanałami może też trafić do dedykowanego IChannelService.
    }
}