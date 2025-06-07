using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Models.Import;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.ViewModels.Import
{
    /// <summary>
    /// ViewModel dla kroku wyboru pliku i typu danych importu
    /// </summary>
    public class ImportFileSelectionViewModel : BaseViewModel, IDisposable
    {
        private readonly ILogger<ImportFileSelectionViewModel> _logger;

        private ImportDataTypeModel? _selectedDataType;
        private string _fileName = string.Empty;
        private string _fileSize = string.Empty;
        private bool _hasFile = false;
        private Stream? _fileStream;

        public ImportFileSelectionViewModel(
            ILogger<ImportFileSelectionViewModel> logger,
            IUIDialogService uiDialogService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            UIDialogService = uiDialogService ?? throw new ArgumentNullException(nameof(uiDialogService));

            // Komendy
            BrowseFileCommand = new AsyncRelayCommand(BrowseFileAsync);
            RemoveFileCommand = new RelayCommand(RemoveFile);
            DownloadTemplateCommand = new AsyncRelayCommand(DownloadTemplateAsync, _ => SelectedDataType != null);

            // Opcje importu z domyślnymi wartościami
            ImportOptions = new ImportOptions
            {
                DryRun = false,
                UpdateExisting = true,
                HasHeaders = true,
                CsvDelimiter = ';',
                Encoding = "UTF-8",
                MaxFileSizeMB = 50
            };

            // Inicjalizuj kodowania
            InitializeEncodings();
        }

        #region Properties

        public ImportDataTypeModel? SelectedDataType
        {
            get => _selectedDataType;
            set
            {
                if (SetProperty(ref _selectedDataType, value))
                {
                    OnPropertyChanged(nameof(HasValidFile));
                    OnPropertyChanged(nameof(AllowedExtensions));
                    UpdateFileValidation();
                    RaiseFileChanged();
                }
            }
        }

        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        public string FileSize
        {
            get => _fileSize;
            set => SetProperty(ref _fileSize, value);
        }

        public bool HasFile
        {
            get => _hasFile;
            set
            {
                if (SetProperty(ref _hasFile, value))
                {
                    OnPropertyChanged(nameof(HasValidFile));
                    RaiseFileChanged();
                }
            }
        }

        public Stream? FileStream
        {
            get => _fileStream;
            private set => _fileStream = value;
        }

        public ImportOptions ImportOptions { get; }

        public bool HasValidFile => HasFile && SelectedDataType != null && IsFileFormatValid();

        public string AllowedExtensions => SelectedDataType?.SupportedFormats != null
            ? string.Join(", ", SelectedDataType.SupportedFormats)
            : ".csv, .xlsx, .xls";

        // Kolekcje
        public ObservableCollection<string> Encodings { get; } = new();

        #endregion

        #region Commands

        public ICommand BrowseFileCommand { get; }
        public ICommand RemoveFileCommand { get; }
        public ICommand DownloadTemplateCommand { get; }

        #endregion

        #region Events

        public event EventHandler? FileChanged;

        #endregion

        #region Public Methods

        public async Task HandleFileDropAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    await ShowErrorDialog("Błąd pliku", "Podany plik nie istnieje.");
                    return;
                }

                await LoadFileAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania upuszczonego pliku: {FilePath}", filePath);
                await ShowErrorDialog("Błąd ładowania pliku", $"Nie można załadować pliku: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods

        private void InitializeEncodings()
        {
            Encodings.Clear();
            Encodings.Add("UTF-8");
            Encodings.Add("UTF-16");
            Encodings.Add("ASCII");
            Encodings.Add("Windows-1250");
            Encodings.Add("ISO-8859-1");
            Encodings.Add("ISO-8859-2");
        }

        private async Task BrowseFileAsync()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Wybierz plik do importu",
                    Multiselect = false,
                    CheckFileExists = true
                };

                // Ustaw filtr na podstawie wybranego typu danych
                if (SelectedDataType != null)
                {
                    var extensions = SelectedDataType.SupportedFormats;
                    var filterParts = extensions.Select(ext => $"*{ext}").ToArray();
                    var filterString = string.Join(";", filterParts);
                    dialog.Filter = $"Pliki importu ({filterString})|{filterString}|Wszystkie pliki (*.*)|*.*";
                }
                else
                {
                    dialog.Filter = "Pliki CSV (*.csv)|*.csv|Pliki Excel (*.xlsx;*.xls)|*.xlsx;*.xls|Wszystkie pliki (*.*)|*.*";
                }

                if (dialog.ShowDialog() == true)
                {
                    await LoadFileAsync(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wybierania pliku");
                await ShowErrorDialog("Błąd", $"Nie można otworzyć pliku: {ex.Message}");
            }
        }

        private async Task LoadFileAsync(string filePath)
        {
            try
            {
                ShowLoadingOverlay("Ładowanie pliku...");

                // Sprawdź rozmiar pliku
                var fileInfo = new FileInfo(filePath);
                var maxSizeBytes = ImportOptions.MaxFileSizeMB * 1024 * 1024;
                
                if (fileInfo.Length > maxSizeBytes)
                {
                    await ShowErrorDialog("Plik zbyt duży", 
                        $"Maksymalny rozmiar pliku to {ImportOptions.MaxFileSizeMB}MB.\n" +
                        $"Rozmiar wybranego pliku: {FormatFileSize(fileInfo.Length)}");
                    return;
                }

                // Sprawdź rozszerzenie
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (SelectedDataType != null && !SelectedDataType.SupportedFormats.Contains(extension))
                {
                    await ShowErrorDialog("Nieobsługiwany format", 
                        $"Wybrany typ danych obsługuje tylko pliki: {AllowedExtensions}\n" +
                        $"Wybrany plik ma rozszerzenie: {extension}");
                    return;
                }

                // Załaduj plik do pamięci
                _fileStream?.Dispose();
                _fileStream = new MemoryStream();
                
                using (var sourceStream = File.OpenRead(filePath))
                {
                    await sourceStream.CopyToAsync(_fileStream);
                }
                
                _fileStream.Position = 0;

                // Ustaw właściwości
                FileName = Path.GetFileName(filePath);
                FileSize = FormatFileSize(fileInfo.Length);
                HasFile = true;

                _logger.LogInformation("Załadowano plik: {FileName} ({FileSize})", FileName, FileSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania pliku: {FilePath}", filePath);
                await ShowErrorDialog("Błąd ładowania", $"Nie można załadować pliku: {ex.Message}");
                RemoveFile();
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        private void RemoveFile()
        {
            _fileStream?.Dispose();
            _fileStream = null;
            FileName = string.Empty;
            FileSize = string.Empty;
            HasFile = false;

            _logger.LogInformation("Usunięto załadowany plik");
        }

        private async Task DownloadTemplateAsync()
        {
            try
            {
                if (SelectedDataType == null) return;

                ShowLoadingOverlay("Generowanie szablonu...");

                // TODO: Implement template download via API
                await ShowInfoDialog("Szablon", 
                    $"Funkcja pobierania szablonu dla typu '{SelectedDataType.DisplayName}' " +
                    "zostanie zaimplementowana w przyszłych wersjach.\n\n" +
                    $"Przykładowy plik: {SelectedDataType.SampleFileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania szablonu dla {DataType}", SelectedDataType?.Name);
                await ShowErrorDialog("Błąd", $"Nie można pobrać szablonu: {ex.Message}");
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        private bool IsFileFormatValid()
        {
            if (!HasFile || SelectedDataType == null || string.IsNullOrEmpty(FileName))
                return false;

            var extension = Path.GetExtension(FileName).ToLowerInvariant();
            return SelectedDataType.SupportedFormats.Contains(extension);
        }

        private void UpdateFileValidation()
        {
            OnPropertyChanged(nameof(HasValidFile));
        }

        private void RaiseFileChanged()
        {
            FileChanged?.Invoke(this, EventArgs.Empty);
        }

        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            return $"{number:n1} {suffixes[counter]}";
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _fileStream?.Dispose();
        }

        #endregion
    }
} 