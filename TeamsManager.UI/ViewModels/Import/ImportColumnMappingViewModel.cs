using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Models.Import;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.ViewModels.Import
{
    /// <summary>
    /// ViewModel dla kroku mapowania kolumn w wizardzie importu
    /// </summary>
    public class ImportColumnMappingViewModel : BaseViewModel
    {
        private readonly ILogger<ImportColumnMappingViewModel> _logger;

        private Stream? _fileStream;
        private ImportDataType _dataType;
        private ImportOptions? _importOptions;
        private bool _hasValidMapping = false;

        public ImportColumnMappingViewModel(
            ILogger<ImportColumnMappingViewModel> logger,
            IUIDialogService uiDialogService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            UIDialogService = uiDialogService ?? throw new ArgumentNullException(nameof(uiDialogService));

            // Komendy
            AutoDetectMappingCommand = new AsyncRelayCommand(AutoDetectMappingAsync, _ => DetectedColumns.Any());
            ClearMappingCommand = new RelayCommand(ClearMapping);
            RefreshPreviewCommand = new AsyncRelayCommand(RefreshPreviewAsync, _ => HasValidMapping);

            // Subskrypcje na zmiany mapowania
            ColumnMappings.CollectionChanged += (s, e) => UpdateMappingValidation();
        }

        #region Properties

        public bool HasValidMapping
        {
            get => _hasValidMapping;
            private set
            {
                if (SetProperty(ref _hasValidMapping, value))
                {
                    RaiseMappingChanged();
                }
            }
        }

        // Kolekcje
        public ObservableCollection<string> DetectedColumns { get; } = new();
        public ObservableCollection<ColumnMappingModel> ColumnMappings { get; } = new();
        public ObservableCollection<string> TargetFields { get; } = new();
        public ObservableCollection<PreviewRowModel> PreviewData { get; } = new();

        #endregion

        #region Commands

        public ICommand AutoDetectMappingCommand { get; }
        public ICommand ClearMappingCommand { get; }
        public ICommand RefreshPreviewCommand { get; }

        #endregion

        #region Events

        public event EventHandler? MappingChanged;

        #endregion

        #region Public Methods

        public void LoadFileData(Stream fileStream, ImportDataType dataType, ImportOptions importOptions)
        {
            try
            {
                _fileStream = fileStream;
                _dataType = dataType;
                _importOptions = importOptions;

                // Wykryj kolumny w pliku
                DetectColumnsInFile();

                // Załaduj dostępne pola docelowe
                LoadTargetFields();

                // Auto-wykryj mapowanie
                _ = AutoDetectMappingAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania danych pliku dla mapowania kolumn");
                ShowErrorDialog("Błąd", $"Nie można załadować danych z pliku: {ex.Message}");
            }
        }

        public Dictionary<string, string> GetColumnMappings()
        {
            return ColumnMappings
                .Where(cm => !string.IsNullOrEmpty(cm.TargetField))
                .ToDictionary(cm => cm.SourceColumn, cm => cm.TargetField);
        }

        #endregion

        #region Private Methods

        private void DetectColumnsInFile()
        {
            try
            {
                if (_fileStream == null || _importOptions == null) return;

                DetectedColumns.Clear();
                ColumnMappings.Clear();

                _fileStream.Position = 0;
                using var reader = new StreamReader(_fileStream, Encoding.GetEncoding(_importOptions.Encoding), leaveOpen: true);

                var firstLine = reader.ReadLine();
                if (string.IsNullOrEmpty(firstLine)) return;

                var columns = firstLine.Split(_importOptions.CsvDelimiter);
                var sampleLine = reader.ReadLine(); // Druga linia jako przykład danych
                var sampleValues = sampleLine?.Split(_importOptions.CsvDelimiter) ?? new string[columns.Length];

                for (int i = 0; i < columns.Length; i++)
                {
                    var columnName = _importOptions.HasHeaders ? columns[i].Trim() : $"Kolumna {i + 1}";
                    var sampleValue = i < sampleValues.Length ? sampleValues[i].Trim() : "";

                    DetectedColumns.Add(columnName);
                    ColumnMappings.Add(new ColumnMappingModel
                    {
                        SourceColumn = columnName,
                        SampleValue = sampleValue,
                        TargetField = "",
                        IsRequired = false
                    });
                }

                _logger.LogInformation("Wykryto {Count} kolumn w pliku", DetectedColumns.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wykrywania kolumn w pliku");
                throw;
            }
        }

        private void LoadTargetFields()
        {
            TargetFields.Clear();

            var fields = _dataType switch
            {
                ImportDataType.Users => new[]
                {
                    "FirstName|Imię|string|true|Imię użytkownika",
                    "LastName|Nazwisko|string|true|Nazwisko użytkownika", 
                    "UPN|Email/UPN|string|true|Główny adres email (UPN)",
                    "DepartmentName|Nazwa działu|string|false|Nazwa działu użytkownika",
                    "Role|Rola|enum|false|Rola użytkownika (Uczen, Nauczyciel, Administrator)",
                    "Phone|Telefon|string|false|Numer telefonu",
                    "AlternateEmail|Email dodatkowy|string|false|Dodatkowy adres email"
                },
                ImportDataType.Teams => new[]
                {
                    "Name|Nazwa zespołu|string|true|Nazwa zespołu Teams",
                    "Description|Opis|string|false|Opis zespołu",
                    "TeamType|Typ zespołu|enum|true|Typ zespołu (Class, Staff, Other)",
                    "Privacy|Prywatność|enum|false|Poziom prywatności (Public, Private)",
                    "OwnerUPN|Właściciel|string|false|UPN właściciela zespołu"
                },
                ImportDataType.Departments => new[]
                {
                    "Name|Nazwa|string|true|Nazwa działu",
                    "Code|Kod|string|true|Kod działu",
                    "Description|Opis|string|false|Opis działu",
                    "ParentCode|Kod nadrzędny|string|false|Kod działu nadrzędnego"
                },
                ImportDataType.Subjects => new[]
                {
                    "Name|Nazwa|string|true|Nazwa przedmiotu",
                    "Code|Kod|string|true|Kod przedmiotu",
                    "Hours|Godziny|number|false|Liczba godzin tygodniowo",
                    "Description|Opis|string|false|Opis przedmiotu"
                },
                _ => Array.Empty<string>()
            };

            foreach (var fieldDef in fields)
            {
                var parts = fieldDef.Split('|');
                if (parts.Length >= 5)
                {
                    TargetFields.Add(parts[0]); // Tylko nazwa pola dla ComboBox
                }
            }

            // Zaaktualizuj informacje o polach w ColumnMappings
            UpdateFieldDescriptions(fields);
        }

        private void UpdateFieldDescriptions(string[] fieldDefinitions)
        {
            var fieldInfo = fieldDefinitions.ToDictionary(
                def => def.Split('|')[0],
                def =>
                {
                    var parts = def.Split('|');
                    return new
                    {
                        DisplayName = parts[1],
                        Type = parts[2],
                        IsRequired = bool.Parse(parts[3]),
                        Description = parts[4]
                    };
                });

            foreach (var mapping in ColumnMappings)
            {
                mapping.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ColumnMappingModel.TargetField) && s is ColumnMappingModel cm)
                    {
                        if (fieldInfo.TryGetValue(cm.TargetField, out var info))
                        {
                            cm.IsRequired = info.IsRequired;
                            cm.FieldDescription = info.Description;
                            cm.FieldType = info.Type;
                        }
                        else
                        {
                            cm.IsRequired = false;
                            cm.FieldDescription = "";
                            cm.FieldType = "";
                        }
                        UpdateMappingValidation();
                    }
                };
            }
        }

        private async Task AutoDetectMappingAsync()
        {
            try
            {
                ShowLoadingOverlay("Automatyczne wykrywanie mapowania...");

                await Task.Run(() =>
                {
                    foreach (var mapping in ColumnMappings)
                    {
                        var normalizedColumn = mapping.SourceColumn.ToLowerInvariant()
                            .Replace(" ", "")
                            .Replace("_", "")
                            .Replace("-", "");

                        // Inteligentne mapowanie na podstawie nazw kolumn
                        var detectedField = normalizedColumn switch
                        {
                            var n when n.Contains("firstname") || n.Contains("imie") || n.Contains("name") && n.Contains("first") => "FirstName",
                            var n when n.Contains("lastname") || n.Contains("nazwisko") || n.Contains("name") && n.Contains("last") => "LastName",
                            var n when n.Contains("email") || n.Contains("upn") || n.Contains("mail") => "UPN",
                            var n when n.Contains("department") || n.Contains("dzial") || n.Contains("dept") => "DepartmentName",
                            var n when n.Contains("role") || n.Contains("rola") || n.Contains("stanowisko") => "Role",
                            var n when n.Contains("phone") || n.Contains("telefon") || n.Contains("tel") => "Phone",
                            var n when n.Contains("description") || n.Contains("opis") || n.Contains("desc") => "Description",
                            var n when n.Contains("code") || n.Contains("kod") => "Code",
                            var n when n.Contains("hours") || n.Contains("godziny") || n.Contains("godz") => "Hours",
                            var n when n.Contains("type") || n.Contains("typ") => "TeamType",
                            var n when n.Contains("privacy") || n.Contains("prywatnosc") => "Privacy",
                            var n when n.Contains("owner") || n.Contains("wlasciciel") => "OwnerUPN",
                            _ => ""
                        };

                        if (!string.IsNullOrEmpty(detectedField) && TargetFields.Contains(detectedField))
                        {
                            mapping.TargetField = detectedField;
                        }
                    }
                });

                // Odśwież podgląd po auto-mapowaniu
                await RefreshPreviewAsync();

                _logger.LogInformation("Automatyczne wykrywanie mapowania zakończone");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas automatycznego wykrywania mapowania");
                await ShowErrorDialog("Błąd", $"Nie można automatycznie wykryć mapowania: {ex.Message}");
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        private void ClearMapping()
        {
            foreach (var mapping in ColumnMappings)
            {
                mapping.TargetField = "";
            }

            PreviewData.Clear();
            _logger.LogInformation("Wyczyszczono mapowanie kolumn");
        }

        private async Task RefreshPreviewAsync()
        {
            try
            {
                if (_fileStream == null || _importOptions == null) return;

                ShowLoadingOverlay("Generowanie podglądu danych...");

                PreviewData.Clear();

                await Task.Run(() =>
                {
                    _fileStream.Position = 0;
                    using var reader = new StreamReader(_fileStream, Encoding.GetEncoding(_importOptions.Encoding), leaveOpen: true);

                    // Pomiń nagłówek jeśli istnieje
                    if (_importOptions.HasHeaders)
                    {
                        reader.ReadLine();
                    }

                    var previewCount = 0;
                    var maxPreview = 10; // Pokaż tylko pierwsze 10 wierszy
                    var mappings = GetColumnMappings();

                    string? line;
                    while ((line = reader.ReadLine()) != null && previewCount < maxPreview)
                    {
                        var values = line.Split(_importOptions.CsvDelimiter);
                        var previewRow = new PreviewRowModel
                        {
                            RowNumber = previewCount + 1,
                            Values = new Dictionary<string, object>()
                        };

                        // Mapuj wartości zgodnie z mapowaniem
                        for (int i = 0; i < Math.Min(values.Length, DetectedColumns.Count); i++)
                        {
                            var sourceColumn = DetectedColumns[i];
                            var value = values[i].Trim();

                            if (mappings.TryGetValue(sourceColumn, out var targetField))
                            {
                                previewRow.Values[targetField] = value;
                            }
                            else
                            {
                                previewRow.Values[sourceColumn] = value;
                            }
                        }

                        System.Windows.Application.Current.Dispatcher.Invoke(() => PreviewData.Add(previewRow));
                        previewCount++;
                    }
                });

                _logger.LogInformation("Wygenerowano podgląd dla {Count} wierszy", PreviewData.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas generowania podglądu danych");
                await ShowErrorDialog("Błąd", $"Nie można wygenerować podglądu: {ex.Message}");
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        private void UpdateMappingValidation()
        {
            // Sprawdź czy wszystkie wymagane pola są zmapowane
            var requiredFields = ColumnMappings
                .Where(cm => cm.IsRequired)
                .Select(cm => cm.TargetField)
                .Where(tf => !string.IsNullOrEmpty(tf))
                .ToHashSet();

            var mappedRequiredFields = ColumnMappings
                .Where(cm => cm.IsRequired && !string.IsNullOrEmpty(cm.TargetField))
                .Count();

            var totalRequiredFields = _dataType switch
            {
                ImportDataType.Users => 3, // FirstName, LastName, UPN
                ImportDataType.Teams => 2, // Name, TeamType
                ImportDataType.Departments => 2, // Name, Code
                ImportDataType.Subjects => 2, // Name, Code
                _ => 1
            };

            HasValidMapping = mappedRequiredFields >= totalRequiredFields;

            _logger.LogDebug("Walidacja mapowania: {Mapped}/{Required} wymaganych pól zmapowanych", 
                mappedRequiredFields, totalRequiredFields);
        }

        private void RaiseMappingChanged()
        {
            MappingChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion


    }
} 
