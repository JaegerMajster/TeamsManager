using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Models;
using System.IO;
using Microsoft.Win32;
using System.Text;
using System.Globalization;
using System.Linq;

namespace TeamsManager.UI.ViewModels.Subjects
{
    public class SubjectImportViewModel : INotifyPropertyChanged
    {
        private readonly ILogger<SubjectImportViewModel> _logger;
        
        private int _currentStep;
        private string? _fileName;
        private string? _filePath;
        private long _fileSize;
        private bool _hasFile;
        private ObservableCollection<ColumnMapping> _columnMappings = new();
        private ObservableCollection<SubjectPreview> _previewSubjects = new();
        private ObservableCollection<string> _availableColumns = new();
        private bool _isLoading;
        
        public SubjectImportViewModel(ILogger<SubjectImportViewModel> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            InitializeCommands();
            InitializeColumnMappings();
        }
        
        #region Properties
        
        public int CurrentStep
        {
            get => _currentStep;
            set 
            { 
                _currentStep = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(NextButtonText));
            }
        }
        
        public string? FileName
        {
            get => _fileName;
            set 
            { 
                _fileName = value; 
                OnPropertyChanged();
            }
        }
        
        public string? FilePath
        {
            get => _filePath;
            set 
            { 
                _filePath = value; 
                OnPropertyChanged();
            }
        }
        
        public long FileSize
        {
            get => _fileSize;
            set 
            { 
                _fileSize = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileSizeText));
            }
        }
        
        public bool HasFile
        {
            get => _hasFile;
            set 
            { 
                _hasFile = value; 
                OnPropertyChanged();
            }
        }
        
        public bool IsLoading
        {
            get => _isLoading;
            set 
            { 
                _isLoading = value; 
                OnPropertyChanged();
            }
        }
        
        public ObservableCollection<ColumnMapping> ColumnMappings
        {
            get => _columnMappings;
            set 
            { 
                _columnMappings = value; 
                OnPropertyChanged();
            }
        }
        
        public ObservableCollection<SubjectPreview> PreviewSubjects
        {
            get => _previewSubjects;
            set 
            { 
                _previewSubjects = value; 
                OnPropertyChanged();
            }
        }
        
        public ObservableCollection<string> AvailableColumns
        {
            get => _availableColumns;
            set 
            { 
                _availableColumns = value; 
                OnPropertyChanged();
            }
        }
        
        public string FileSizeText => FormatFileSize(FileSize);
        
        public bool CanGoBack => CurrentStep > 0;
        
        public string NextButtonText => CurrentStep switch
        {
            0 => "DALEJ",
            1 => "PODGLĄD",
            2 => "IMPORTUJ",
            _ => "ZAMKNIJ"
        };
        
        #endregion
        
        #region Commands
        
        public ICommand BrowseFileCommand { get; private set; } = null!;
        public ICommand RemoveFileCommand { get; private set; } = null!;
        public ICommand NextStepCommand { get; private set; } = null!;
        public ICommand PreviousStepCommand { get; private set; } = null!;
        
        #endregion
        
        #region Public Methods
        
        public async Task InitializeAsync()
        {
            CurrentStep = 0;
            await Task.CompletedTask;
        }
        
        #endregion
        
        #region Private Methods
        
        private void InitializeCommands()
        {
            BrowseFileCommand = new RelayCommand(_ => BrowseFile());
            RemoveFileCommand = new RelayCommand(_ => RemoveFile());
            NextStepCommand = new RelayCommand(async _ => await NextStepAsync(), _ => CanGoNext());
            PreviousStepCommand = new RelayCommand(_ => PreviousStep());
        }
        
        private void InitializeColumnMappings()
        {
            ColumnMappings = new ObservableCollection<ColumnMapping>
            {
                new ColumnMapping { TargetField = "Nazwa", IsRequired = true },
                new ColumnMapping { TargetField = "Kod", IsRequired = false },
                new ColumnMapping { TargetField = "Opis", IsRequired = false },
                new ColumnMapping { TargetField = "Liczba godzin", IsRequired = false },
                new ColumnMapping { TargetField = "Kategoria", IsRequired = false }
            };
        }
        
        private void BrowseFile()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Wybierz plik CSV",
                    Filter = "Pliki CSV (*.csv)|*.csv|Wszystkie pliki (*.*)|*.*",
                    FilterIndex = 1,
                    Multiselect = false
                };
                
                if (openFileDialog.ShowDialog() == true)
                {
                    LoadFile(openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wybierania pliku");
            }
        }
        
        private void LoadFile(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    _logger.LogWarning("Plik nie istnieje: {FilePath}", filePath);
                    return;
                }
                
                FilePath = filePath;
                FileName = fileInfo.Name;
                FileSize = fileInfo.Length;
                HasFile = true;
                
                // Load and parse CSV headers
                LoadCsvHeaders();
                
                _logger.LogInformation("Załadowano plik: {FileName} ({FileSize} bajtów)", FileName, FileSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania pliku: {FilePath}", filePath);
                RemoveFile();
            }
        }
        
        private void LoadCsvHeaders()
        {
            try
            {
                if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
                    return;
                
                using var reader = new StreamReader(FilePath, Encoding.UTF8);
                var firstLine = reader.ReadLine();
                
                if (!string.IsNullOrEmpty(firstLine))
                {
                    var headers = firstLine.Split(',')
                        .Select(h => h.Trim().Trim('"'))
                        .Where(h => !string.IsNullOrWhiteSpace(h))
                        .ToList();
                    
                    AvailableColumns.Clear();
                    AvailableColumns.Add("-- Nie mapuj --");
                    foreach (var header in headers)
                    {
                        AvailableColumns.Add(header);
                    }
                    
                    // Update column mappings with available columns
                    foreach (var mapping in ColumnMappings)
                    {
                        mapping.AvailableColumns = AvailableColumns;
                    }
                    
                    _logger.LogInformation("Znaleziono {Count} kolumn w pliku CSV", headers.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas parsowania nagłówków CSV");
            }
        }
        
        private void RemoveFile()
        {
            FilePath = null;
            FileName = null;
            FileSize = 0;
            HasFile = false;
            AvailableColumns.Clear();
            PreviewSubjects.Clear();
        }
        
        private bool CanGoNext()
        {
            return CurrentStep switch
            {
                0 => HasFile,
                1 => ColumnMappings.Any(m => m.IsRequired && !string.IsNullOrEmpty(m.SelectedColumn) && m.SelectedColumn != "-- Nie mapuj --"),
                2 => PreviewSubjects.Any(p => p.ShouldImport),
                _ => false
            };
        }
        
        private async Task NextStepAsync()
        {
            try
            {
                IsLoading = true;
                
                switch (CurrentStep)
                {
                    case 0:
                        CurrentStep = 1;
                        break;
                    case 1:
                        await GeneratePreviewAsync();
                        CurrentStep = 2;
                        break;
                    case 2:
                        await ImportSubjectsAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas przechodzenia do następnego kroku");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private void PreviousStep()
        {
            if (CurrentStep > 0)
            {
                CurrentStep--;
            }
        }
        
        private async Task GeneratePreviewAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
                    return;
                
                PreviewSubjects.Clear();
                
                using var reader = new StreamReader(FilePath, Encoding.UTF8);
                var lines = await reader.ReadToEndAsync();
                var csvLines = lines.Split('\n').Skip(1).Take(100); // Skip header, take first 100 rows
                
                foreach (var line in csvLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    var values = line.Split(',').Select(v => v.Trim().Trim('"')).ToArray();
                    var preview = CreateSubjectPreview(values);
                    
                    if (preview != null)
                    {
                        PreviewSubjects.Add(preview);
                    }
                }
                
                _logger.LogInformation("Wygenerowano podgląd dla {Count} przedmiotów", PreviewSubjects.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas generowania podglądu");
            }
        }
        
        private SubjectPreview? CreateSubjectPreview(string[] values)
        {
            try
            {
                var preview = new SubjectPreview();
                
                foreach (var mapping in ColumnMappings)
                {
                    if (string.IsNullOrEmpty(mapping.SelectedColumn) || mapping.SelectedColumn == "-- Nie mapuj --")
                        continue;
                    
                    var columnIndex = AvailableColumns.IndexOf(mapping.SelectedColumn) - 1; // -1 because first item is "-- Nie mapuj --"
                    if (columnIndex >= 0 && columnIndex < values.Length)
                    {
                        var value = values[columnIndex];
                        
                        switch (mapping.TargetField)
                        {
                            case "Nazwa":
                                preview.Name = value;
                                break;
                            case "Kod":
                                preview.Code = value;
                                break;
                            case "Opis":
                                preview.Description = value;
                                break;
                            case "Liczba godzin":
                                if (int.TryParse(value, out var hours))
                                    preview.Hours = hours;
                                break;
                            case "Kategoria":
                                preview.Category = value;
                                break;
                        }
                    }
                }
                
                // Validate preview
                if (string.IsNullOrWhiteSpace(preview.Name))
                {
                    preview.ValidationStatus = "Błąd: Brak nazwy";
                    preview.ShouldImport = false;
                }
                else
                {
                    preview.ValidationStatus = "Gotowy do importu";
                    preview.ShouldImport = true;
                }
                
                return preview;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas tworzenia podglądu przedmiotu");
                return null;
            }
        }
        
        private async Task ImportSubjectsAsync()
        {
            try
            {
                var subjectsToImport = PreviewSubjects.Where(p => p.ShouldImport).ToList();
                _logger.LogInformation("Rozpoczynanie importu {Count} przedmiotów", subjectsToImport.Count);
                
                // TODO: Implement actual import logic
                // This would involve sending POST requests to the API for each subject
                
                await Task.Delay(2000); // Simulate import process
                
                _logger.LogInformation("Import zakończony pomyślnie");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas importu przedmiotów");
                throw;
            }
        }
        
        private static string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            
            return $"{size:0.##} {sizes[order]}";
        }
        
        #endregion
        
        #region INotifyPropertyChanged
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
    
    // Helper classes for CSV import
    public class ColumnMapping : INotifyPropertyChanged
    {
        private string _targetField = string.Empty;
        private string? _selectedColumn;
        private bool _isRequired;
        private ObservableCollection<string> _availableColumns = new();
        
        public string TargetField
        {
            get => _targetField;
            set 
            { 
                _targetField = value; 
                OnPropertyChanged();
            }
        }
        
        public string? SelectedColumn
        {
            get => _selectedColumn;
            set 
            { 
                _selectedColumn = value; 
                OnPropertyChanged();
            }
        }
        
        public bool IsRequired
        {
            get => _isRequired;
            set 
            { 
                _isRequired = value; 
                OnPropertyChanged();
            }
        }
        
        public ObservableCollection<string> AvailableColumns
        {
            get => _availableColumns;
            set 
            { 
                _availableColumns = value; 
                OnPropertyChanged();
            }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    public class SubjectPreview : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string? _code;
        private string? _description;
        private int? _hours;
        private string? _category;
        private string _validationStatus = string.Empty;
        private bool _shouldImport = true;
        
        public string Name
        {
            get => _name;
            set 
            { 
                _name = value; 
                OnPropertyChanged();
            }
        }
        
        public string? Code
        {
            get => _code;
            set 
            { 
                _code = value; 
                OnPropertyChanged();
            }
        }
        
        public string? Description
        {
            get => _description;
            set 
            { 
                _description = value; 
                OnPropertyChanged();
            }
        }
        
        public int? Hours
        {
            get => _hours;
            set 
            { 
                _hours = value; 
                OnPropertyChanged();
            }
        }
        
        public string? Category
        {
            get => _category;
            set 
            { 
                _category = value; 
                OnPropertyChanged();
            }
        }
        
        public string ValidationStatus
        {
            get => _validationStatus;
            set 
            { 
                _validationStatus = value; 
                OnPropertyChanged();
            }
        }
        
        public bool ShouldImport
        {
            get => _shouldImport;
            set 
            { 
                _shouldImport = value; 
                OnPropertyChanged();
            }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 