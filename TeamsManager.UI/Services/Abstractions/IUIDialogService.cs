using System.Threading.Tasks;

namespace TeamsManager.UI.Services.Abstractions
{
    /// <summary>
    /// Interfejs serwisu dialogów UI używany przez ViewModels
    /// </summary>
    public interface IUIDialogService
    {
        /// <summary>
        /// Wyświetla dialog błędu
        /// </summary>
        Task ShowErrorDialog(string title, string message);

        /// <summary>
        /// Wyświetla dialog informacyjny
        /// </summary>
        Task ShowInfoDialog(string title, string message);

        /// <summary>
        /// Wyświetla dialog potwierdzenia
        /// </summary>
        Task<bool> ShowConfirmationDialog(string title, string message);

        /// <summary>
        /// Wyświetla overlay ładowania
        /// </summary>
        void ShowLoadingOverlay(string message = "Ładowanie...");

        /// <summary>
        /// Ukrywa overlay ładowania
        /// </summary>
        void HideLoadingOverlay();
    }
} 