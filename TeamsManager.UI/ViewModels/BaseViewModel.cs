using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.ViewModels
{
    /// <summary>
    /// Bazowa klasa dla wszystkich ViewModeli z implementacją INotifyPropertyChanged i metod UI
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        protected IUIDialogService? UIDialogService { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #region UI Helper Methods

        /// <summary>
        /// Wyświetla dialog błędu
        /// </summary>
        protected async Task ShowErrorDialog(string title, string message)
        {
            if (UIDialogService != null)
                await UIDialogService.ShowErrorDialog(title, message);
            else
                System.Diagnostics.Debug.WriteLine($"[ERROR] {title}: {message}");
        }

        /// <summary>
        /// Wyświetla dialog informacyjny
        /// </summary>
        protected async Task ShowInfoDialog(string title, string message)
        {
            if (UIDialogService != null)
                await UIDialogService.ShowInfoDialog(title, message);
            else
                System.Diagnostics.Debug.WriteLine($"[INFO] {title}: {message}");
        }

        /// <summary>
        /// Wyświetla dialog ostrzeżenia
        /// </summary>
        protected async Task ShowWarningDialog(string title, string message)
        {
            if (UIDialogService != null)
                await UIDialogService.ShowWarningDialog(title, message);
            else
                System.Diagnostics.Debug.WriteLine($"[WARNING] {title}: {message}");
        }

        /// <summary>
        /// Wyświetla dialog sukcesu
        /// </summary>
        protected async Task ShowSuccessDialog(string title, string message)
        {
            if (UIDialogService != null)
                await UIDialogService.ShowSuccessDialog(title, message);
            else
                System.Diagnostics.Debug.WriteLine($"[SUCCESS] {title}: {message}");
        }

        /// <summary>
        /// Wyświetla dialog potwierdzenia
        /// </summary>
        protected async Task<bool> ShowConfirmationDialog(string title, string message)
        {
            if (UIDialogService != null)
                return await UIDialogService.ShowConfirmationDialog(title, message);
            else
            {
                System.Diagnostics.Debug.WriteLine($"[CONFIRM] {title}: {message}");
                return true; // Fallback dla testów
            }
        }

        /// <summary>
        /// Wyświetla overlay ładowania
        /// </summary>
        protected void ShowLoadingOverlay(string message = "Ładowanie...")
        {
            UIDialogService?.ShowLoadingOverlay(message);
        }

        /// <summary>
        /// Ukrywa overlay ładowania
        /// </summary>
        protected void HideLoadingOverlay()
        {
            UIDialogService?.HideLoadingOverlay();
        }

        #endregion
    }
} 