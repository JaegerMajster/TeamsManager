using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za logikę biznesową związaną z użytkownikami (User).
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Asynchronicznie pobiera użytkownika na podstawie jego ID.
        /// </summary>
        /// <param name="userId">Identyfikator użytkownika.</param>
        /// <returns>Obiekt User lub null, jeśli nie znaleziono.</returns>
        Task<User?> GetUserByIdAsync(string userId);

        /// <summary>
        /// Asynchronicznie pobiera użytkownika na podstawie jego User Principal Name (UPN).
        /// </summary>
        /// <param name="upn">UPN użytkownika.</param>
        /// <returns>Obiekt User lub null, jeśli nie znaleziono.</returns>
        Task<User?> GetUserByUpnAsync(string upn);

        /// <summary>
        /// Asynchronicznie pobiera wszystkich aktywnych użytkowników.
        /// </summary>
        /// <returns>Kolekcja wszystkich aktywnych użytkowników.</returns>
        Task<IEnumerable<User>> GetAllActiveUsersAsync();

        /// <summary>
        /// Asynchronicznie tworzy nowego użytkownika.
        /// </summary>
        /// <param name="firstName">Imię użytkownika.</param>
        /// <param name="lastName">Nazwisko użytkownika.</param>
        /// <param name="upn">UPN użytkownika.</param>
        /// <param name="role">Rola systemowa użytkownika.</param>
        /// <param name="departmentId">Identyfikator działu, do którego użytkownik ma być przypisany.</param>
        /// <param name="sendWelcomeEmail">Opcjonalnie, czy wysłać email powitalny (logika do implementacji).</param>
        /// <returns>Utworzony obiekt User lub null, jeśli operacja się nie powiodła.</returns>
        Task<User?> CreateUserAsync(
            string firstName,
            string lastName,
            string upn,
            UserRole role,
            string departmentId,
            bool sendWelcomeEmail = false);

        /// <summary>
        /// Asynchronicznie aktualizuje dane istniejącego użytkownika.
        /// </summary>
        /// <param name="userToUpdate">Obiekt User z zaktualizowanymi danymi.</param>
        /// <returns>True, jeśli aktualizacja się powiodła.</returns>
        Task<bool> UpdateUserAsync(User userToUpdate);

        /// <summary>
        /// Asynchronicznie dezaktywuje użytkownika (soft delete).
        /// </summary>
        /// <param name="userId">Identyfikator użytkownika do dezaktywacji.</param>
        /// <returns>True, jeśli dezaktywacja się powiodła.</returns>
        Task<bool> DeactivateUserAsync(string userId);

        /// <summary>
        /// Asynchronicznie aktywuje użytkownika (cofa soft delete).
        /// </summary>
        /// <param name="userId">Identyfikator użytkownika do aktywacji.</param>
        /// <returns>True, jeśli aktywacja się powiodła.</returns>
        Task<bool> ActivateUserAsync(string userId);

        /// <summary>
        /// Asynchronicznie przypisuje użytkownika do typu szkoły.
        /// </summary>
        /// <param name="userId">Identyfikator użytkownika.</param>
        /// <param name="schoolTypeId">Identyfikator typu szkoły.</param>
        /// <param name="assignedDate">Data przypisania.</param>
        /// <param name="endDate">Opcjonalna data zakończenia przypisania.</param>
        /// <param name="workloadPercentage">Opcjonalny procent etatu.</param>
        /// <param name="notes">Opcjonalne notatki.</param>
        /// <returns>Utworzony obiekt UserSchoolType lub null.</returns>
        Task<UserSchoolType?> AssignUserToSchoolTypeAsync(
            string userId,
            string schoolTypeId,
            DateTime assignedDate,
            DateTime? endDate = null,
            decimal? workloadPercentage = null,
            string? notes = null);

        /// <summary>
        /// Asynchronicznie usuwa przypisanie użytkownika do typu szkoły.
        /// </summary>
        /// <param name="userSchoolTypeId">Identyfikator przypisania UserSchoolType.</param>
        /// <returns>True, jeśli usunięcie się powiodło.</returns>
        Task<bool> RemoveUserFromSchoolTypeAsync(string userSchoolTypeId);

        /// <summary>
        /// Asynchronicznie przypisuje nauczyciela do przedmiotu.
        /// </summary>
        /// <param name="teacherId">Identyfikator nauczyciela.</param>
        /// <param name="subjectId">Identyfikator przedmiotu.</param>
        /// <param name="assignedDate">Data przypisania.</param>
        /// <param name="notes">Opcjonalne notatki.</param>
        /// <returns>Utworzony obiekt UserSubject lub null.</returns>
        Task<UserSubject?> AssignTeacherToSubjectAsync(
            string teacherId,
            string subjectId,
            DateTime assignedDate,
            string? notes = null);

        /// <summary>
        /// Asynchronicznie usuwa przypisanie nauczyciela do przedmiotu.
        /// </summary>
        /// <param name="userSubjectId">Identyfikator przypisania UserSubject.</param>
        /// <returns>True, jeśli usunięcie się powiodło.</returns>
        Task<bool> RemoveTeacherFromSubjectAsync(string userSubjectId);

        // TODO: Rozważyć metody do importu użytkowników z CSV
        // Task<IEnumerable<User>> ImportUsersFromCsvAsync(string filePath);
    }
}