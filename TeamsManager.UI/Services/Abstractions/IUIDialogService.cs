using System.Threading.Tasks;
using TeamsManager.UI.Models;

namespace TeamsManager.UI.Services.Abstractions
{
    /// <summary>
    /// Interfejs serwisu dialogów UI używany przez ViewModels
    /// </summary>
    public interface IUIDialogService
    {
        // ===== NOWY UNIWERSALNY SYSTEM DIALOGÓW =====
        
        /// <summary>
        /// Wyświetla uniwersalny dialog z opcjami konfiguracji
        /// </summary>
        Task<DialogResponse> ShowDialogAsync(DialogOptions options);

        /// <summary>
        /// Wyświetla dialog informacyjny (nowy system)
        /// </summary>
        Task<DialogResponse> ShowInformationAsync(string title, string message, string? details = null);

        /// <summary>
        /// Wyświetla dialog ostrzeżenia (nowy system)
        /// </summary>
        Task<DialogResponse> ShowWarningAsync(string title, string message, string? details = null);

        /// <summary>
        /// Wyświetla dialog błędu (nowy system)
        /// </summary>
        Task<DialogResponse> ShowErrorAsync(string title, string message, string? details = null);

        /// <summary>
        /// Wyświetla dialog sukcesu (nowy system)
        /// </summary>
        Task<DialogResponse> ShowSuccessAsync(string title, string message, string? details = null);

        /// <summary>
        /// Wyświetla dialog potwierdzenia (nowy system)
        /// </summary>
        Task<DialogResponse> ShowConfirmationAsync(string title, string message, string? details = null, 
            string? yesText = null, string? noText = null);

        /// <summary>
        /// Wyświetla dialog z niestandardowymi przyciskami
        /// </summary>
        Task<DialogResponse> ShowQuestionAsync(string title, string message, string? details = null,
            string? primaryText = null, string? secondaryText = null);

        // ===== STARY SYSTEM (ZACHOWANY DLA KOMPATYBILNOŚCI) =====
        
        /// <summary>
        /// Wyświetla dialog błędu (stary system - do usunięcia)
        /// </summary>
        [System.Obsolete("Użyj ShowErrorAsync zamiast tego")]
        Task ShowErrorDialog(string title, string message);

        /// <summary>
        /// Wyświetla dialog informacyjny (stary system - do usunięcia)
        /// </summary>
        [System.Obsolete("Użyj ShowInformationAsync zamiast tego")]
        Task ShowInfoDialog(string title, string message);

        /// <summary>
        /// Wyświetla dialog ostrzeżenia (stary system - do usunięcia)
        /// </summary>
        [System.Obsolete("Użyj ShowWarningAsync zamiast tego")]
        Task ShowWarningDialog(string title, string message);

        /// <summary>
        /// Wyświetla dialog sukcesu (stary system - do usunięcia)
        /// </summary>
        [System.Obsolete("Użyj ShowSuccessAsync zamiast tego")]
        Task ShowSuccessDialog(string title, string message);

        /// <summary>
        /// Wyświetla dialog potwierdzenia (stary system - do usunięcia)
        /// </summary>
        [System.Obsolete("Użyj ShowConfirmationAsync zamiast tego")]
        Task<bool> ShowConfirmationDialog(string title, string message);

        // ===== OVERLAY ŁADOWANIA =====

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