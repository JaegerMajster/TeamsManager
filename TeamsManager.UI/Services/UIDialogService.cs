using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using MaterialDesignThemes.Wpf;
using TeamsManager.UI.Services.Abstractions;
using TeamsManager.UI.Models;
using TeamsManager.UI.Views.Dialogs;
using TeamsManager.UI.ViewModels.Dialogs;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Implementacja serwisu dialogów UI
    /// </summary>
    public class UIDialogService : IUIDialogService
    {
        // ===== NOWY UNIWERSALNY SYSTEM DIALOGÓW =====

        public async Task<DialogResponse> ShowDialogAsync(DialogOptions options)
        {
            return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var dialog = new UniversalDialog();
                var viewModel = App.ServiceProvider.GetRequiredService<UniversalDialogViewModel>();
                
                viewModel.Initialize(options);
                dialog.DataContext = viewModel;
                
                // Ustaw właściciela na aktywne okno
                var activeWindow = GetActiveWindow();
                if (activeWindow != null)
                {
                    dialog.Owner = activeWindow;
                }

                // Włącz overlay na głównym oknie
                SetMainWindowDialogOverlay(true);

                try
                {
                    // Pokaż dialog modalnie
                    dialog.ShowDialog();

                    // Zwróć wynik
                    return dialog.Result ?? new DialogResponse 
                    { 
                        Result = DialogResult.Cancel,
                        DisplayTime = TimeSpan.Zero
                    };
                }
                finally
                {
                    // Wyłącz overlay na głównym oknie
                    SetMainWindowDialogOverlay(false);
                }
            });
        }

        public async Task<DialogResponse> ShowInformationAsync(string title, string message, string? details = null)
        {
            var options = new DialogOptions
            {
                Type = DialogType.Information,
                Title = title,
                Message = message,
                Details = details,
                ShowSecondaryButton = false
            };

            return await ShowDialogAsync(options);
        }

        public async Task<DialogResponse> ShowWarningAsync(string title, string message, string? details = null)
        {
            var options = new DialogOptions
            {
                Type = DialogType.Warning,
                Title = title,
                Message = message,
                Details = details,
                ShowSecondaryButton = false
            };

            return await ShowDialogAsync(options);
        }

        public async Task<DialogResponse> ShowErrorAsync(string title, string message, string? details = null)
        {
            var options = new DialogOptions
            {
                Type = DialogType.Error,
                Title = title,
                Message = message,
                Details = details,
                ShowSecondaryButton = false
            };

            return await ShowDialogAsync(options);
        }

        public async Task<DialogResponse> ShowSuccessAsync(string title, string message, string? details = null)
        {
            var options = new DialogOptions
            {
                Type = DialogType.Success,
                Title = title,
                Message = message,
                Details = details,
                ShowSecondaryButton = false
            };

            return await ShowDialogAsync(options);
        }

        public async Task<DialogResponse> ShowConfirmationAsync(string title, string message, string? details = null, 
            string? yesText = null, string? noText = null)
        {
            var options = new DialogOptions
            {
                Type = DialogType.Confirmation,
                Title = title,
                Message = message,
                Details = details,
                ShowSecondaryButton = true,
                PrimaryButtonText = yesText,
                SecondaryButtonText = noText,
                IsPrimaryDefault = false, // W potwierdzeniach lepiej nie mieć domyślnego "Tak"
                IsSecondaryCancel = true
            };

            return await ShowDialogAsync(options);
        }

        public async Task<DialogResponse> ShowQuestionAsync(string title, string message, string? details = null,
            string? primaryText = null, string? secondaryText = null)
        {
            var options = new DialogOptions
            {
                Type = DialogType.Question,
                Title = title,
                Message = message,
                Details = details,
                ShowSecondaryButton = !string.IsNullOrEmpty(secondaryText),
                PrimaryButtonText = primaryText,
                SecondaryButtonText = secondaryText,
                IsPrimaryDefault = true,
                IsSecondaryCancel = true
            };

            return await ShowDialogAsync(options);
        }

        // ===== STARY SYSTEM (ZACHOWANY DLA KOMPATYBILNOŚCI) =====

        [Obsolete("Użyj ShowErrorAsync zamiast tego")]
        public async Task ShowErrorDialog(string title, string message)
        {
            await ShowErrorAsync(title, message);
        }

        [Obsolete("Użyj ShowInformationAsync zamiast tego")]
        public async Task ShowInfoDialog(string title, string message)
        {
            await ShowInformationAsync(title, message);
        }

        [Obsolete("Użyj ShowWarningAsync zamiast tego")]
        public async Task ShowWarningDialog(string title, string message)
        {
            await ShowWarningAsync(title, message);
        }

        [Obsolete("Użyj ShowSuccessAsync zamiast tego")]
        public async Task ShowSuccessDialog(string title, string message)
        {
            await ShowSuccessAsync(title, message);
        }

        [Obsolete("Użyj ShowConfirmationAsync zamiast tego")]
        public async Task<bool> ShowConfirmationDialog(string title, string message)
        {
            var response = await ShowConfirmationAsync(title, message);
            return response.IsPrimary;
        }

        // ===== OVERLAY ŁADOWANIA =====

        public void ShowLoadingOverlay(string message = "Ładowanie...")
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Znajdź główne okno (MainShellWindow) lub aktywne okno
                var mainWindow = System.Windows.Application.Current.Windows
                    .OfType<Views.Shell.MainShellWindow>()
                    .FirstOrDefault() ?? System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                if (mainWindow != null)
                {
                    // Znajdź element LoadingOverlay w oknie
                    var loadingOverlay = mainWindow.FindName("LoadingOverlay") as FrameworkElement;
                    if (loadingOverlay != null)
                    {
                        loadingOverlay.Visibility = Visibility.Visible;
                        var fadeIn = new DoubleAnimation(0, 0.9, TimeSpan.FromSeconds(0.2));
                        loadingOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                    }
                }
            });
        }

        public void HideLoadingOverlay()
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Znajdź główne okno (MainShellWindow) lub aktywne okno
                var mainWindow = System.Windows.Application.Current.Windows
                    .OfType<Views.Shell.MainShellWindow>()
                    .FirstOrDefault() ?? System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                if (mainWindow != null)
                {
                    // Znajdź element LoadingOverlay w oknie
                    var loadingOverlay = mainWindow.FindName("LoadingOverlay") as FrameworkElement;
                    if (loadingOverlay != null)
                    {
                        var fadeOut = new DoubleAnimation(0.9, 0, TimeSpan.FromSeconds(0.2));
                        fadeOut.Completed += (s, e) => loadingOverlay.Visibility = Visibility.Collapsed;
                        loadingOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                    }
                }
            });
        }

        // ===== METODY POMOCNICZE =====

        private Window? GetActiveWindow()
        {
            // Najpierw spróbuj znaleźć MainShellWindow
            var mainWindow = System.Windows.Application.Current.Windows
                .OfType<Views.Shell.MainShellWindow>()
                .FirstOrDefault();

            if (mainWindow != null)
                return mainWindow;

            // Jeśli nie ma MainShellWindow, znajdź aktywne okno
            return System.Windows.Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current.MainWindow;
        }

        private void SetMainWindowDialogOverlay(bool isVisible)
        {
            // Znajdź główne okno (MainShellWindow)
            var mainWindow = System.Windows.Application.Current.Windows
                .OfType<Views.Shell.MainShellWindow>()
                .FirstOrDefault();

            if (mainWindow?.DataContext is ViewModels.Shell.MainShellViewModel mainViewModel)
            {
                mainViewModel.IsDialogOpen = isVisible;
            }
        }
    }
} 
