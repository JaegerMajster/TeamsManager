using System.Threading.Tasks;
using TeamsManager.UI.Models;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Klasa pomocnicza z gotowymi metodami dla typowych scenariuszy dialogów
    /// </summary>
    public static class DialogHelpers
    {
        // ===== KOMUNIKATY BŁĘDÓW =====

        /// <summary>
        /// Wyświetla błąd walidacji
        /// </summary>
        public static async Task<DialogResponse> ShowValidationErrorAsync(IUIDialogService dialogService, 
            string message, string? details = null)
        {
            return await dialogService.ShowErrorAsync("Błąd walidacji", message, details);
        }

        /// <summary>
        /// Wyświetla błąd operacji
        /// </summary>
        public static async Task<DialogResponse> ShowOperationErrorAsync(IUIDialogService dialogService, 
            string operation, string error, string? details = null)
        {
            return await dialogService.ShowErrorAsync($"Błąd podczas {operation}", error, details);
        }

        /// <summary>
        /// Wyświetla błąd połączenia
        /// </summary>
        public static async Task<DialogResponse> ShowConnectionErrorAsync(IUIDialogService dialogService, 
            string? details = null)
        {
            return await dialogService.ShowErrorAsync(
                "Błąd połączenia", 
                "Nie można nawiązać połączenia z serwerem. Sprawdź połączenie internetowe i spróbuj ponownie.",
                details);
        }

        // ===== KOMUNIKATY SUKCESU =====

        /// <summary>
        /// Wyświetla sukces operacji
        /// </summary>
        public static async Task<DialogResponse> ShowOperationSuccessAsync(IUIDialogService dialogService, 
            string operation, string? details = null)
        {
            return await dialogService.ShowSuccessAsync($"Sukces", $"{operation} zakończono pomyślnie.", details);
        }

        /// <summary>
        /// Wyświetla sukces zapisu
        /// </summary>
        public static async Task<DialogResponse> ShowSaveSuccessAsync(IUIDialogService dialogService, 
            string itemName, string? details = null)
        {
            return await dialogService.ShowSuccessAsync(
                "Zapisano", 
                $"{itemName} został zapisany pomyślnie.",
                details);
        }

        // ===== OSTRZEŻENIA =====

        /// <summary>
        /// Wyświetla ostrzeżenie o konflikcie danych
        /// </summary>
        public static async Task<DialogResponse> ShowDataConflictWarningAsync(IUIDialogService dialogService, 
            string message, string? details = null)
        {
            return await dialogService.ShowWarningAsync("Konflikt danych", message, details);
        }

        /// <summary>
        /// Wyświetla ostrzeżenie o ograniczeniach
        /// </summary>
        public static async Task<DialogResponse> ShowConstraintWarningAsync(IUIDialogService dialogService, 
            string message, string? details = null)
        {
            return await dialogService.ShowWarningAsync("Ograniczenie systemu", message, details);
        }

        // ===== POTWIERDZENIA =====

        /// <summary>
        /// Wyświetla potwierdzenie usunięcia
        /// </summary>
        public static async Task<DialogResponse> ShowDeleteConfirmationAsync(IUIDialogService dialogService, 
            string itemName, string? additionalInfo = null)
        {
            var message = $"Czy na pewno chcesz usunąć \"{itemName}\"?";
            var details = additionalInfo != null 
                ? $"Ta operacja jest nieodwracalna.\n\n{additionalInfo}"
                : "Ta operacja jest nieodwracalna.";

            return await dialogService.ShowConfirmationAsync(
                "Potwierdzenie usunięcia", 
                message, 
                details,
                "Usuń", 
                "Anuluj");
        }

        /// <summary>
        /// Wyświetla potwierdzenie deaktywacji
        /// </summary>
        public static async Task<DialogResponse> ShowDeactivateConfirmationAsync(IUIDialogService dialogService, 
            string itemName, string? reason = null)
        {
            var message = $"Czy na pewno chcesz dezaktywować \"{itemName}\"?";
            var details = reason != null 
                ? $"Powód: {reason}\n\nElement będzie nadal widoczny, ale nieaktywny."
                : "Element będzie nadal widoczny, ale nieaktywny.";

            return await dialogService.ShowConfirmationAsync(
                "Potwierdzenie deaktywacji", 
                message, 
                details,
                "Dezaktywuj", 
                "Anuluj");
        }

        /// <summary>
        /// Wyświetla potwierdzenie zapisania zmian
        /// </summary>
        public static async Task<DialogResponse> ShowSaveChangesConfirmationAsync(IUIDialogService dialogService, 
            string? details = null)
        {
            return await dialogService.ShowConfirmationAsync(
                "Niezapisane zmiany", 
                "Masz niezapisane zmiany. Czy chcesz je zapisać?",
                details,
                "Zapisz", 
                "Odrzuć");
        }

        /// <summary>
        /// Wyświetla potwierdzenie anulowania operacji
        /// </summary>
        public static async Task<DialogResponse> ShowCancelOperationConfirmationAsync(IUIDialogService dialogService, 
            string operationName, string? details = null)
        {
            return await dialogService.ShowConfirmationAsync(
                "Anulowanie operacji", 
                $"Czy na pewno chcesz anulować {operationName}?",
                details ?? "Postęp zostanie utracony.",
                "Anuluj operację", 
                "Kontynuuj");
        }

        // ===== PYTANIA NIESTANDARDOWE =====

        /// <summary>
        /// Wyświetla pytanie o zastąpienie istniejących danych
        /// </summary>
        public static async Task<DialogResponse> ShowReplaceDataQuestionAsync(IUIDialogService dialogService, 
            string dataType, string? details = null)
        {
            return await dialogService.ShowQuestionAsync(
                "Zastąpienie danych", 
                $"Znaleziono istniejące dane typu \"{dataType}\". Co chcesz zrobić?",
                details,
                "Zastąp", 
                "Zachowaj istniejące");
        }

        /// <summary>
        /// Wyświetla pytanie o kontynuację przy błędach
        /// </summary>
        public static async Task<DialogResponse> ShowContinueWithErrorsQuestionAsync(IUIDialogService dialogService, 
            int errorCount, string? details = null)
        {
            var message = errorCount == 1 
                ? "Wystąpił błąd podczas operacji. Czy chcesz kontynuować?"
                : $"Wystąpiło {errorCount} błędów podczas operacji. Czy chcesz kontynuować?";

            return await dialogService.ShowQuestionAsync(
                "Błędy podczas operacji", 
                message,
                details,
                "Kontynuuj", 
                "Przerwij");
        }

        // ===== INFORMACJE =====

        /// <summary>
        /// Wyświetla informację o zakończeniu operacji
        /// </summary>
        public static async Task<DialogResponse> ShowOperationCompleteAsync(IUIDialogService dialogService, 
            string operationName, string summary, string? details = null)
        {
            return await dialogService.ShowInformationAsync(
                $"{operationName} zakończono", 
                summary,
                details);
        }

        /// <summary>
        /// Wyświetla informację o braku danych
        /// </summary>
        public static async Task<DialogResponse> ShowNoDataInfoAsync(IUIDialogService dialogService, 
            string dataType, string? suggestion = null)
        {
            var message = $"Nie znaleziono danych typu \"{dataType}\".";
            var details = suggestion != null ? $"Sugestia: {suggestion}" : null;

            return await dialogService.ShowInformationAsync("Brak danych", message, details);
        }


    }
} 