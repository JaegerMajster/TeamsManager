using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using MaterialDesignThemes.Wpf;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Implementacja serwisu dialogów UI
    /// </summary>
    public class UIDialogService : IUIDialogService
    {
        public async Task ShowErrorDialog(string title, string message)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        public async Task ShowInfoDialog(string title, string message)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        public async Task<bool> ShowConfirmationDialog(string title, string message)
        {
            return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                return result == MessageBoxResult.Yes;
            });
        }

        public void ShowLoadingOverlay(string message = "Ładowanie...")
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Znajdź główne okno lub aktywne okno
                var mainWindow = System.Windows.Application.Current.MainWindow ?? System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
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
                // Znajdź główne okno lub aktywne okno
                var mainWindow = System.Windows.Application.Current.MainWindow ?? System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
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
    }
} 
