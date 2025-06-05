using System.Collections.Generic;
using System.Threading.Tasks;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Abstractions.Services.Cache
{
    /// <summary>
    /// Interfejs dla centralnego serwisu systematycznej inwalidacji cache
    /// ETAP 7/8: Systematyczna Inwalidacja Cache
    /// </summary>
    public interface ICacheInvalidationService
    {
        // TEAM OPERATIONS
        /// <summary>
        /// Inwalidacja cache po utworzeniu zespołu
        /// </summary>
        /// <param name="team">Utworzony zespół</param>
        Task InvalidateForTeamCreatedAsync(Team team);

        /// <summary>
        /// Inwalidacja cache po aktualizacji zespołu
        /// </summary>
        /// <param name="team">Zaktualizowany zespół</param>
        /// <param name="oldTeam">Stary stan zespołu (opcjonalny)</param>
        Task InvalidateForTeamUpdatedAsync(Team team, Team? oldTeam = null);

        /// <summary>
        /// Inwalidacja cache po archiwizacji zespołu
        /// </summary>
        /// <param name="team">Zarchiwizowany zespół z członkami</param>
        Task InvalidateForTeamArchivedAsync(Team team);

        /// <summary>
        /// Inwalidacja cache po przywróceniu zespołu z archiwum
        /// </summary>
        /// <param name="team">Przywrócony zespół</param>
        Task InvalidateForTeamRestoredAsync(Team team);

        /// <summary>
        /// Inwalidacja cache po usunięciu zespołu
        /// </summary>
        /// <param name="team">Usunięty zespół</param>
        Task InvalidateForTeamDeletedAsync(Team team);

        /// <summary>
        /// Inwalidacja cache po dodaniu członka do zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userId">ID użytkownika</param>
        Task InvalidateForTeamMemberAddedAsync(string teamId, string userId);

        /// <summary>
        /// Inwalidacja cache po usunięciu członka z zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userId">ID użytkownika</param>
        Task InvalidateForTeamMemberRemovedAsync(string teamId, string userId);

        /// <summary>
        /// Inwalidacja cache po masowych operacjach na członkach zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userIds">Lista ID użytkowników</param>
        Task InvalidateForTeamMembersBulkOperationAsync(string teamId, List<string> userIds);

        // USER OPERATIONS
        /// <summary>
        /// Inwalidacja cache po utworzeniu użytkownika
        /// </summary>
        /// <param name="user">Utworzony użytkownik</param>
        Task InvalidateForUserCreatedAsync(User user);

        /// <summary>
        /// Inwalidacja cache po aktualizacji użytkownika
        /// </summary>
        /// <param name="user">Zaktualizowany użytkownik</param>
        /// <param name="oldUser">Stary stan użytkownika (opcjonalny)</param>
        Task InvalidateForUserUpdatedAsync(User user, User? oldUser = null);

        /// <summary>
        /// Inwalidacja cache po aktywacji użytkownika
        /// </summary>
        /// <param name="user">Aktywowany użytkownik</param>
        Task InvalidateForUserActivatedAsync(User user);

        /// <summary>
        /// Inwalidacja cache po dezaktywacji użytkownika (z kaskadą)
        /// </summary>
        /// <param name="user">Dezaktywowany użytkownik z pełnymi danymi (przedmioty, zespoły)</param>
        Task InvalidateForUserDeactivatedAsync(User user);

        /// <summary>
        /// Inwalidacja cache po zmianie typu szkoły użytkownika
        /// </summary>
        /// <param name="userId">ID użytkownika</param>
        /// <param name="oldSchoolTypeId">Stary typ szkoły</param>
        /// <param name="newSchoolTypeId">Nowy typ szkoły</param>
        Task InvalidateForUserSchoolTypeChangedAsync(string userId, string? oldSchoolTypeId, string? newSchoolTypeId);

        /// <summary>
        /// Inwalidacja cache po dodaniu/usunięciu przedmiotu użytkownikowi
        /// </summary>
        /// <param name="userId">ID użytkownika</param>
        /// <param name="subjectId">ID przedmiotu</param>
        /// <param name="added">true = dodanie, false = usunięcie</param>
        Task InvalidateForUserSubjectChangedAsync(string userId, string subjectId, bool added);

        // CHANNEL OPERATIONS
        /// <summary>
        /// Inwalidacja cache po utworzeniu kanału
        /// </summary>
        /// <param name="channel">Utworzony kanał</param>
        Task InvalidateForChannelCreatedAsync(Channel channel);

        /// <summary>
        /// Inwalidacja cache po aktualizacji kanału
        /// </summary>
        /// <param name="channel">Zaktualizowany kanał</param>
        Task InvalidateForChannelUpdatedAsync(Channel channel);

        /// <summary>
        /// Inwalidacja cache po usunięciu kanału
        /// </summary>
        /// <param name="channel">Usunięty kanał</param>
        Task InvalidateForChannelDeletedAsync(Channel channel);

        // DEPARTMENT OPERATIONS
        /// <summary>
        /// Inwalidacja cache po zmianie w departamencie
        /// </summary>
        /// <param name="department">Zmieniony departament</param>
        /// <param name="oldDepartment">Stary stan departamentu (opcjonalny)</param>
        Task InvalidateForDepartmentChangedAsync(Department department, Department? oldDepartment = null);

        // SUBJECT OPERATIONS
        /// <summary>
        /// Inwalidacja cache po zmianie w przedmiocie
        /// </summary>
        /// <param name="subject">Zmieniony przedmiot</param>
        /// <param name="oldSubject">Stary stan przedmiotu (opcjonalny)</param>
        Task InvalidateForSubjectChangedAsync(Subject subject, Subject? oldSubject = null);

        // BATCH OPERATIONS
        /// <summary>
        /// Wykonuje batch inwalidację dla wielu operacji jednocześnie
        /// </summary>
        /// <param name="operationsMap">Mapa operacji → klucze do inwalidacji</param>
        Task InvalidateBatchAsync(Dictionary<string, List<string>> operationsMap);
    }
} 