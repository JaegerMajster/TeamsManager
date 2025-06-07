using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TeamsManager.UI.ViewModels.Import;

namespace TeamsManager.UI.UserControls.Import
{
    /// <summary>
    /// Interaction logic for FileSelectionStep.xaml
    /// </summary>
    public partial class FileSelectionStep : UserControl
    {
        public FileSelectionStep()
        {
            InitializeComponent();
        }

        private void OnFileDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                    if (files.Length > 0 && DataContext is ImportFileSelectionViewModel viewModel)
                    {
                        _ = viewModel.HandleFileDropAsync(files[0]);
                    }
                }

                // Reset visual state
                ResetDropBorderStyle();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnFileDrop: {ex.Message}");
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                    if (files.Length > 0)
                    {
                        var extension = Path.GetExtension(files[0]).ToLowerInvariant();
                        var allowedExtensions = new[] { ".csv", ".xlsx", ".xls" };
                        
                        if (System.Array.Exists(allowedExtensions, ext => ext == extension))
                        {
                            e.Effects = DragDropEffects.Copy;
                            SetDropBorderHighlight(true);
                        }
                        else
                        {
                            e.Effects = DragDropEffects.None;
                            SetDropBorderHighlight(false, true);
                        }
                    }
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }

                e.Handled = true;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnDragEnter: {ex.Message}");
            }
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            try
            {
                ResetDropBorderStyle();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnDragLeave: {ex.Message}");
            }
        }

        private void SetDropBorderHighlight(bool isValid, bool isError = false)
        {
            if (DropBorder == null) return;

            if (isError)
            {
                DropBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF5252")!);
                DropBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#33FF5252")!);
            }
            else if (isValid)
            {
                DropBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF0078D4")!);
                DropBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#330078D4")!);
            }
        }

        private void ResetDropBorderStyle()
        {
            if (DropBorder == null) return;

            DropBorder.ClearValue(Border.BorderBrushProperty);
            DropBorder.ClearValue(Border.BackgroundProperty);
        }
    }
} 