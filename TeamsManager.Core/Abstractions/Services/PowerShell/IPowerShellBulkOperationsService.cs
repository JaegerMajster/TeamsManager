using System.Collections.Generic;
using System.Threading.Tasks;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Abstractions.Services.PowerShell
{
    /// <summary>
    /// Serwis zarządzający operacjami masowymi w Microsoft 365 przez PowerShell
    /// </summary>
    public interface IPowerShellBulkOperationsService
    {
        /// <summary>
        /// Masowo dodaje użytkowników do zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userUpns">Lista UPN użytkowników do dodania</param>
        /// <param name="role">Rola użytkowników (Member/Owner)</param>
        /// <returns>Słownik z wynikami operacji dla każdego użytkownika</returns>
        Task<Dictionary<string, bool>> BulkAddUsersToTeamAsync(
            string teamId,
            List<string> userUpns,
            string role = "Member");

        /// <summary>
        /// Masowo usuwa użytkowników z zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userUpns">Lista UPN użytkowników do usunięcia</param>
        /// <returns>Słownik z wynikami operacji dla każdego użytkownika</returns>
        Task<Dictionary<string, bool>> BulkRemoveUsersFromTeamAsync(
            string teamId,
            List<string> userUpns);

        /// <summary>
        /// Masowo archiwizuje zespoły
        /// </summary>
        /// <param name="teamIds">Lista ID zespołów do archiwizacji</param>
        /// <returns>Słownik z wynikami operacji dla każdego zespołu</returns>
        Task<Dictionary<string, bool>> BulkArchiveTeamsAsync(List<string> teamIds);

        /// <summary>
        /// Masowo aktualizuje właściwości użytkowników
        /// </summary>
        /// <param name="userUpdates">Słownik gdzie klucz to UPN użytkownika, a wartość to słownik właściwości do aktualizacji</param>
        /// <returns>Słownik z wynikami operacji dla każdego użytkownika</returns>
        Task<Dictionary<string, bool>> BulkUpdateUserPropertiesAsync(
            Dictionary<string, Dictionary<string, string>> userUpdates);

        /// <summary>
        /// Archiwizuje zespół i dezaktywuje użytkowników, którzy są tylko w tym zespole
        /// </summary>
        /// <param name="teamId">ID zespołu do archiwizacji</param>
        /// <returns>Słownik z wynikiem operacji dla zespołu</returns>
        Task<Dictionary<string, bool>> ArchiveTeamAndDeactivateExclusiveUsersAsync(string teamId);

        #region Enhanced V2 Methods with BulkOperationResult (Etap 6/7)

        /// <summary>
        /// [ETAP6] Masowo dodaje użytkowników do zespołu z zaawansowanym raportowaniem
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userUpns">Lista UPN użytkowników do dodania</param>
        /// <param name="role">Rola użytkowników (Member/Owner)</param>
        /// <returns>Słownik z szczegółowymi wynikami operacji dla każdego użytkownika</returns>
        Task<Dictionary<string, BulkOperationResult>> BulkAddUsersToTeamV2Async(
            string teamId,
            List<string> userUpns,
            string role = "Member");

        /// <summary>
        /// [ETAP6] Masowo usuwa użytkowników z zespołu z zaawansowanym raportowaniem
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userUpns">Lista UPN użytkowników do usunięcia</param>
        /// <returns>Słownik z szczegółowymi wynikami operacji dla każdego użytkownika</returns>
        Task<Dictionary<string, BulkOperationResult>> BulkRemoveUsersFromTeamV2Async(
            string teamId,
            List<string> userUpns);

        /// <summary>
        /// [ETAP6] Masowo archiwizuje zespoły z zaawansowanym raportowaniem
        /// </summary>
        /// <param name="teamIds">Lista ID zespołów do archiwizacji</param>
        /// <returns>Słownik z szczegółowymi wynikami operacji dla każdego zespołu</returns>
        Task<Dictionary<string, BulkOperationResult>> BulkArchiveTeamsV2Async(List<string> teamIds);

        #endregion
    }
}