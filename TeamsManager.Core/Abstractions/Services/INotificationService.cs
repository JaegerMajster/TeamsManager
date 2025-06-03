// Plik: TeamsManager.Core/Abstractions/Services/INotificationService.cs
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za wysyłanie powiadomień,
    /// np. do klienta UI poprzez SignalR lub innymi kanałami.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Wysyła powiadomienie o postępie operacji do określonego użytkownika.
        /// </summary>
        /// <param name="userUpn">UPN użytkownika, do którego ma trafić powiadomienie.</param>
        /// <param name="operationId">Unikalny identyfikator operacji.</param>
        /// <param name="progressPercentage">Postęp operacji w procentach (0-100).</param>
        /// <param name="message">Wiadomość opisująca aktualny stan/krok operacji.</param>
        Task SendOperationProgressToUserAsync(string userUpn, string operationId, int progressPercentage, string message);

        /// <summary>
        /// Wysyła ogólne powiadomienie do określonego użytkownika.
        /// </summary>
        /// <param name="userUpn">UPN użytkownika, do którego ma trafić powiadomienie.</param>
        /// <param name="message">Treść powiadomienia.</param>
        /// <param name="type">Typ powiadomienia (np. "info", "warning", "error") używany do stylizacji po stronie klienta.</param>
        Task SendNotificationToUserAsync(string userUpn, string message, string type);
    }
}